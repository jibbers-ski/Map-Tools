using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jibbers.MapTools
{

    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.ShortcutManagement;

    public static class MapExporterShortcuts
    {
        [Shortcut("Map Tools/Export Map", KeyCode.E, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void Export()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;
            var exporter = go.GetComponentInParent<MapExporter>();
            if (exporter == null) return;
            exporter.Export();
        }
    }

    [CustomEditor(typeof(MapExporter))]
    public class MapExporterEditor : Editor
    {
        bool autoImportArmed;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bool timeEnabled = false;
            bool rawTime = false;
            var iter = serializedObject.GetIterator();
            iter.NextVisible(true);
            while (iter.NextVisible(false))
            {
                if (iter.name == "overrideTime")
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Time of Day", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(iter, GUILayout.Width(EditorGUIUtility.labelWidth + 20));
                    timeEnabled = iter.boolValue;
                    var rawProp = serializedObject.FindProperty("rawTime");
                    EditorGUI.BeginDisabledGroup(!timeEnabled);
                    rawProp.boolValue = EditorGUILayout.ToggleLeft(new GUIContent("Edit Raw Time Value",
                        "Author the raw 0-1 dayNightT value directly instead of a 24h clock time."), rawProp.boolValue);
                    rawTime = rawProp.boolValue;
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                }
                else if (iter.name == "rawTime")
                {
                }
                else if (iter.name == "dayTimeHours")
                {
                    EditorGUI.BeginDisabledGroup(!timeEnabled);
                    if (rawTime)
                    {
                        EditorGUILayout.Slider(serializedObject.FindProperty("dayTime"), 0f, 1f, new GUIContent("Day Night T"));
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(iter, new GUIContent("Day Time (24h)"));
                        int minutes = Mathf.RoundToInt(Mathf.Max(0f, iter.floatValue) * 60) % 1440;
                        EditorGUILayout.LabelField(iter.floatValue >= 0 ? $"{minutes / 60:00}:{minutes % 60:00}" : "legacy", GUILayout.Width(60));
                        EditorGUILayout.EndHorizontal();
                        if (timeEnabled && iter.floatValue < 0)
                            EditorGUILayout.HelpBox("Still exporting the old 0-1 time value. Move the slider to author the time as a 24h clock instead.", MessageType.Info);
                    }
                    EditorGUI.EndDisabledGroup();
                }
                else if (iter.name == "sunAngle")
                {
                    EditorGUI.BeginDisabledGroup(!timeEnabled);
                    EditorGUILayout.PropertyField(iter);
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    EditorGUILayout.PropertyField(iter, true);
                }
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(20);

            var exporter = (MapExporter) target;

            if (!autoImportArmed)
            {
                if (GUILayout.Button("Auto Import Chunks"))
                    autoImportArmed = true;
            }
            else
            {
                EditorGUILayout.HelpBox("This will overwrite your current chunks list.", MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm"))
                {
                    exporter.AutoImport();
                    autoImportArmed = false;
                }
                if (GUILayout.Button("Cancel"))
                    autoImportArmed = false;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(20);

            if (GUILayout.Button("Export"))
                exporter.Export();
        }
    }
    #endif

    public class MapExporter : MonoBehaviour
    {
        [SerializeField] string mapName = "Cool Map Name";
        [SerializeField] string idOverride;
        public bool autoExportOnAwake;

        [Header("Misc")]
        [SerializeField] Vector3 camStartPosition;
        [SerializeField] bool allowBackgroundMountains = true;

        [SerializeField] bool overrideTime;
        [SerializeField] bool rawTime;
        [Range(0, 24)] public float dayTimeHours = -1;
        [Range(0, 360)] public float sunAngle;
        [HideInInspector] public float dayTime;

        [Header("Terrain Chunks")]
        public List<MapTerrainChunk> chunks;

        [Header("Debug")]
        public bool enableLogs;
        public bool enableBreakdown;

        static long s_timeMeshPack;
        static long s_timeTextureEncode;
        static long s_timeChunkMeta;

        void Awake() {
            if(autoExportOnAwake)
                Export();
            Destroy(gameObject);
        }

        void Log(string msg) { if (enableLogs) Debug.Log($"[MapExporter] {msg}"); }

        public void Export()
        {
            s_timeMeshPack = 0;
            s_timeTextureEncode = 0;
            s_timeChunkMeta = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long phaseStart = 0;

            var spawnPoints = GetComponentsInChildren<MapSpawnPoint>();
            if(string.IsNullOrEmpty(idOverride))
                idOverride = Utility.NewGuid;

            Log($"Exporting '{mapName}' (id: {idOverride}) — {chunks.Count} chunk(s), {spawnPoints.Length} spawn point(s)");

            bool hasScaleError = false;
            foreach (var obj in GetComponentsInChildren<MapObject>())
            {
                Vector3 s = obj.transform.lossyScale;
                if (s.x < 0 || s.y < 0 || s.z < 0)
                {
                    Debug.LogError($"[MapExporter] MapObject '{obj.gameObject.name}' has negative scale {s}. Fix it before exporting.", obj.gameObject);
                    hasScaleError = true;
                }
                if (obj.forceUniformScale && (Mathf.Abs(s.x - s.y) > 0.001f || Mathf.Abs(s.y - s.z) > 0.001f))
                {
                    Debug.LogError($"[MapExporter] MapObject '{obj.gameObject.name}' must be scaled uniformly but is {s}. Fix it before exporting.", obj.gameObject);
                    hasScaleError = true;
                }
            }
            foreach (var obj in GetComponentsInChildren<CustomMapObject>())
            {
                foreach (var t in obj.GetComponentsInChildren<Transform>())
                {
                    Vector3 s = t.lossyScale;
                    if (s.x < 0 || s.y < 0 || s.z < 0)
                    {
                        Debug.LogError($"[MapExporter] CustomMapObject '{t.gameObject.name}' (under '{obj.gameObject.name}') has negative scale {s}. Fix it before exporting.", t.gameObject);
                        hasScaleError = true;
                    }
                }
            }
            if (hasScaleError) return;
            long validationMs = sw.ElapsedMilliseconds;
            phaseStart = sw.ElapsedMilliseconds;

            var meshLibrary    = new Dictionary<string, MeshData>();
            var textureLibrary = new Dictionary<string, TextureData>();

            var chunkDatas = new MapTerrainChunkData[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                Log($"Chunk {i}: terrain '{chunks[i].terrain.name}', heightmap {chunks[i].terrain.terrainData.heightmapResolution}");
                var metaSw = System.Diagnostics.Stopwatch.StartNew();
                chunkDatas[i] = new MapTerrainChunkData(chunks[i]);
                s_timeChunkMeta += metaSw.ElapsedMilliseconds;

                var customObjs = new List<CustomMapObjectData>();
                var found = new HashSet<CustomMapObject>(chunks[i].terrain.GetComponentsInChildren<CustomMapObject>());
                if (chunks[i].mapObjectContainer != null)
                    foreach (var o in chunks[i].mapObjectContainer.GetComponentsInChildren<CustomMapObject>())
                        found.Add(o);

                Log($"  Found {found.Count} CustomMapObject(s) in chunk {i}");

                foreach (var obj in found)
                {
                    Transform root = obj.transform;
                    Log($"  CustomMapObject '{obj.name}' (surface: {obj.surfaceType})");
                    var partsList = new List<CustomMapObjectPartData>();

                    var lodGroupsInObj = obj.GetComponentsInChildren<LODGroup>();
                    var rendererLodMap = new Dictionary<Renderer, (int groupIndex, int lodIndex)>();
                    var lodGroupDataList = new List<LODGroupData>();
                    for (int g = 0; g < lodGroupsInObj.Length; g++)
                    {
                        var lg = lodGroupsInObj[g];
                        var lods = lg.GetLODs();
                        var transitions = new float[lods.Length];
                        var fadeWidths  = new float[lods.Length];
                        for (int l = 0; l < lods.Length; l++)
                        {
                            transitions[l] = lods[l].screenRelativeTransitionHeight;
                            fadeWidths[l]  = lods[l].fadeTransitionWidth;
                            if (lods[l].renderers == null) continue;
                            foreach (var r in lods[l].renderers)
                                if (r != null)
                                    rendererLodMap[r] = (g, l);
                        }

                        lodGroupDataList.Add(new LODGroupData {
                            localPosition       = root.InverseTransformPoint(lg.transform.position),
                            localReferencePoint = lg.localReferencePoint,
                            size                = lg.size,
                            transitions         = transitions,
                            fadeWidths          = fadeWidths,
                            fadeMode            = (int) lg.fadeMode,
                            animateCrossFading  = lg.animateCrossFading,
                        });
                        Log($"    LODGroup '{lg.gameObject.name}': {lods.Length} level(s), fade={lg.fadeMode}");
                    }

                    foreach (var mf in obj.GetComponentsInChildren<MeshFilter>())
                    {
                        var mr = mf.GetComponent<MeshRenderer>();
                        var mats = mr != null ? mr.sharedMaterials : null;
                        Log($"    MeshFilter on '{mf.gameObject.name}': mesh={mf.sharedMesh?.name ?? "null"}, mats={(mats != null ? mats.Length : 0)}");
                        var part = ExtractPart(mf.sharedMesh, mats,
                            mf.transform, root, meshLibrary, textureLibrary);
                        if (part != null)
                        {
                            if (mr != null)
                            {
                                part.shadowCastingMode = (int) mr.shadowCastingMode;
                                if (rendererLodMap.TryGetValue(mr, out var lod))
                                {
                                    part.lodGroupIndex = lod.groupIndex;
                                    part.lodIndex     = lod.lodIndex;
                                }
                            }
                            partsList.Add(part);
                        }
                    }

                    foreach (var smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mats = smr.sharedMaterials;
                        Log($"    SkinnedMeshRenderer on '{smr.gameObject.name}': mesh={smr.sharedMesh?.name ?? "null"}, mats={(mats != null ? mats.Length : 0)}");
                        var part = ExtractPart(smr.sharedMesh, mats,
                            smr.transform, root, meshLibrary, textureLibrary);
                        if (part != null)
                        {
                            part.shadowCastingMode = (int) smr.shadowCastingMode;
                            if (rendererLodMap.TryGetValue(smr, out var lod))
                            {
                                part.lodGroupIndex = lod.groupIndex;
                                part.lodIndex     = lod.lodIndex;
                            }
                            partsList.Add(part);
                        }
                    }

                    var colliderList = new List<ColliderData>();
                    foreach (var col in obj.GetComponentsInChildren<Collider>())
                    {
                        var cd = ColliderFromUnity(col, root, meshLibrary);
                        if (cd == null) continue;

                        cd.surfaceType = obj.surfaceType;
                        if (Enum.TryParse<SurfaceType>(col.gameObject.tag, out var tagSurface))
                            cd.surfaceType = tagSurface;

                        Log($"    Collider on '{col.gameObject.name}': {cd.shape}, surface={cd.surfaceType}");
                        colliderList.Add(cd);
                    }

                    var lightList = new List<LightData>();
                    foreach (var lt in obj.GetComponentsInChildren<Light>())
                    {
                        if (lt.type != LightType.Point && lt.type != LightType.Spot) continue;
                        lightList.Add(new LightData
                        {
                            type            = (int) lt.type,
                            localPosition   = lt.transform.position,
                            localRotation   = lt.transform.rotation.eulerAngles,
                            color           = lt.color,
                            intensity       = lt.intensity,
                            range           = lt.range,
                            spotAngle       = lt.spotAngle,
                            innerSpotAngle  = lt.innerSpotAngle,
                            shadows         = (int) lt.shadows,
                            shadowStrength  = lt.shadowStrength,
                        });
                        Log($"    Light on '{lt.gameObject.name}': type={lt.type}, range={lt.range}");
                    }

                    if (partsList.Count == 0 && lightList.Count == 0)
                    {
                        Log($"    Skipped — no valid mesh parts or lights");
                        continue;
                    }

                    Log($"    Exported: {partsList.Count} part(s), {colliderList.Count} collider(s), {lightList.Count} light(s)");

                    customObjs.Add(new CustomMapObjectData
                    {
                        surfaceType      = obj.surfaceType,
                        canStabilize     = obj.canStabilize,
                        canRotate        = obj.canRotate,
                        canMagnetize     = obj.canMagnetize,
                        intendedUpMethod = obj.intendedUpMethod,
                        disableDistanceCulling = obj.disableDistanceCulling,
                        timedVisibility  = obj.timedVisibility,
                        visibleFromHour  = obj.visibleFromHour,
                        visibleUntilHour = obj.visibleUntilHour,
                        position  = root.position,
                        rotation  = root.rotation.eulerAngles,
                        scale     = root.lossyScale,
                        parts     = partsList.ToArray(),
                        colliders = colliderList.Count > 0 ? colliderList.ToArray() : null,
                        lodGroups = lodGroupDataList.Count > 0 ? lodGroupDataList.ToArray() : null,
                        lights    = lightList.Count > 0 ? lightList.ToArray() : null,
                    });
                }

                chunkDatas[i].customObjects = customObjs.Count > 0 ? customObjs.ToArray() : null;
                ExtractTrees(chunks[i].terrain, chunkDatas[i], meshLibrary, textureLibrary);
            }

            Log($"Libraries: {meshLibrary.Count} mesh(es), {textureLibrary.Count} texture(s)");
            foreach (var kv in meshLibrary)
                Log($"  Mesh '{kv.Key}': {kv.Value.vertexCount} verts, {kv.Value.triangleCount / 3} tris");
            foreach (var kv in textureLibrary)
                Log($"  Texture '{kv.Key}': {kv.Value.width}x{kv.Value.height}");

            long extractMs = sw.ElapsedMilliseconds - phaseStart;
            phaseStart = sw.ElapsedMilliseconds;

            foreach (var td in textureLibrary.Values)
                td.FinishEncode();
            foreach (var cd in chunkDatas)
                cd.snowMaskData?.FinishEncode();
            long encodeWaitMs = sw.ElapsedMilliseconds - phaseStart;
            phaseStart = sw.ElapsedMilliseconds;

            var map = new MapData() {
                name = mapName,
                id = idOverride,
                camStartPosition = camStartPosition,
                allowBackgroundMountains = allowBackgroundMountains,
                overrideTime = overrideTime,
                dayTime = dayTime,
                dayTimeHours = rawTime ? -1 : dayTimeHours,
                sunAngle = sunAngle,
                chunks = chunkDatas,
                spawnPoints = spawnPoints.Select(s => new SpawnPointData(s)).ToArray(),
                meshLibrary    = meshLibrary.Count > 0    ? meshLibrary    : null,
                textureLibrary = textureLibrary.Count > 0 ? textureLibrary : null,
            };

            var serializer = new JsonSerializer { EnableCompression = true };
            serializer.Begin(true);
            map.Serialize(serializer);
            serializer.Close();
            string serializedData = (string) serializer.Data;
            long serializeMs = sw.ElapsedMilliseconds - phaseStart;
            phaseStart = sw.ElapsedMilliseconds;

            var dirPath = Utility.DataPath + "Maps/";
            Directory.CreateDirectory(dirPath);

            var filePath = dirPath + map.id + ".jbrmap";
            File.WriteAllText(filePath, serializedData);
            long writeMs = sw.ElapsedMilliseconds - phaseStart;
            Debug.Log("Saved to: " + filePath);

            if (enableBreakdown)
                LogBreakdown(filePath, chunkDatas, meshLibrary, textureLibrary, spawnPoints.Length,
                    validationMs, extractMs, encodeWaitMs, serializeMs, writeMs);
        }

        static void LogBreakdown(string filePath, MapTerrainChunkData[] chunks,
            Dictionary<string, MeshData> meshLib, Dictionary<string, TextureData> texLib, int spawnCount,
            long validationMs, long extractMs, long encodeWaitMs, long serializeMs, long writeMs)
        {
            long terrainBytes = 0, snowMaskBytes = 0;
            int customObjCount = 0, mapObjCount = 0;
            foreach (var c in chunks)
            {
                terrainBytes  += c.terrainData?.Length ?? 0;
                snowMaskBytes += c.snowMaskData?.data?.Length ?? 0;
                customObjCount += c.customObjects?.Length ?? 0;
                mapObjCount    += c.objects?.Length ?? 0;
            }

            long meshVerts = 0, meshNormals = 0, meshTangents = 0, meshColors = 0, meshUVs = 0, meshTris = 0;
            foreach (var kv in meshLib)
            {
                var m = kv.Value;
                meshVerts    += m.vertexData?.Length ?? 0;
                meshNormals  += m.normalData?.Length ?? 0;
                meshTangents += m.tangentData?.Length ?? 0;
                meshColors   += m.colorData?.Length ?? 0;
                meshUVs      += (m.uvData?.Length ?? 0) + (m.uv2Data?.Length ?? 0)
                              + (m.uv3Data?.Length ?? 0) + (m.uv4Data?.Length ?? 0);
                if (m.submeshTriangleData != null)
                    foreach (var t in m.submeshTriangleData)
                        meshTris += t?.Length ?? 0;
            }
            long meshTotal = meshVerts + meshNormals + meshTangents + meshColors + meshUVs + meshTris;

            long texBytes = 0;
            foreach (var kv in texLib)
                texBytes += kv.Value.data?.Length ?? 0;

            long rawTotal = terrainBytes + snowMaskBytes + meshTotal + texBytes;
            long fileSize = new FileInfo(filePath).Length;

            long totalMs = validationMs + extractMs + encodeWaitMs + serializeMs + writeMs;
            Debug.Log(
                $"[MapExporter] Breakdown:\n" +
                $"  File on disk:       {FmtBytes(fileSize)}\n" +
                $"  Raw payload total:  {FmtBytes(rawTotal)} (pre-compression)\n" +
                $"  Terrain heightmaps: {FmtBytes(terrainBytes)} ({chunks.Length} chunk(s))\n" +
                $"  Snow masks:         {FmtBytes(snowMaskBytes)}\n" +
                $"  Texture library:    {FmtBytes(texBytes)} ({texLib.Count} texture(s))\n" +
                $"  Mesh library:       {FmtBytes(meshTotal)} ({meshLib.Count} mesh(es))\n" +
                $"    Vertices:         {FmtBytes(meshVerts)}\n" +
                $"    Normals:          {FmtBytes(meshNormals)}\n" +
                $"    Tangents:         {FmtBytes(meshTangents)}\n" +
                $"    Colors:           {FmtBytes(meshColors)}\n" +
                $"    UVs (all sets):   {FmtBytes(meshUVs)}\n" +
                $"    Triangles:        {FmtBytes(meshTris)}\n" +
                $"  Map objects:        {mapObjCount}\n" +
                $"  Custom objects:     {customObjCount}\n" +
                $"  Spawn points:       {spawnCount}\n" +
                $"  Timing:\n" +
                $"    Validation:       {FmtTime(validationMs)}\n" +
                $"    Chunk extract:    {FmtTime(extractMs)}\n" +
                $"      Chunk meta:     {FmtTime(s_timeChunkMeta)} (heightmap, snow mask, map objects)\n" +
                $"      Mesh packing:   {FmtTime(s_timeMeshPack)}\n" +
                $"      Texture capture:{FmtTime(s_timeTextureEncode)} (pixel readback, PNG encode runs in background)\n" +
                $"    PNG encode wait:  {FmtTime(encodeWaitMs)} (parallel, overlapped with extract)\n" +
                $"    Serialization:    {FmtTime(serializeMs)} (JSON + gzip + base64)\n" +
                $"    File write:       {FmtTime(writeMs)}\n" +
                $"    Total:            {FmtTime(totalMs)}"
            );
        }

        static string FmtTime(long ms)
        {
            if (ms < 1000) return ms + " ms";
            if (ms < 60_000) return (ms / 1000.0).ToString("F1") + " s";
            long mins = ms / 60_000;
            long secs = (ms / 1000) % 60;
            return mins + "m " + secs.ToString("00") + "s";
        }

        static string FmtBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            return (bytes / 1048576.0).ToString("F2") + " MB";
        }

        static readonly HashSet<string> coreMaterialProperties = new HashSet<string> {
            "_BaseMap", "_BaseColor",
            "_MetallicMap", "_Metallic",
            "_RoughnessMap", "_Smoothness",
            "_NormalMap",
            "_EmissionMap", "_EmissionColor",
            "_Cull", "_Cutoff",
            "_SrcBlend", "_DstBlend", "_ZWrite", "_RenderMode",
        };

        static CustomMapObjectPartData ExtractPart(Mesh mesh, Material[] mats, Transform child, Transform root,
            Dictionary<string, MeshData> meshLibrary, Dictionary<string, TextureData> textureLibrary)
        {
            if (mesh == null) return null;
            if (!mesh.isReadable)
            {
                Debug.LogError($"[MapExporter] Mesh '{mesh.name}' on '{child.gameObject.name}' is not readable. Enable Read/Write in import settings.");
                return null;
            }

            string meshKey = GetAssetKey(mesh);
            if (!meshLibrary.ContainsKey(meshKey))
            {
                var msw = System.Diagnostics.Stopwatch.StartNew();
                meshLibrary[meshKey] = new MeshData(mesh);
                s_timeMeshPack += msw.ElapsedMilliseconds;
            }

            int matCount  = mats != null ? mats.Length : 0;
            int slotCount = Mathf.Max(1, Mathf.Min(mesh.subMeshCount, matCount));
            var materials = new CustomMapObjectMaterialData[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var mat = i < matCount ? mats[i] : null;
                materials[i] = new CustomMapObjectMaterialData
                {
                    baseTexRef      = ExtractTexKey(mat, "_BaseMap",      textureLibrary),
                    metallicTexRef  = ExtractTexKey(mat, "_MetallicMap",  textureLibrary),
                    roughnessTexRef = ExtractTexKey(mat, "_RoughnessMap", textureLibrary),
                    normalTexRef    = ExtractTexKey(mat, "_NormalMap",    textureLibrary),
                    emissionTexRef  = ExtractTexKey(mat, "_EmissionMap",  textureLibrary),
                    renderMode      = GetRenderMode(mat),
                    alphaCutoff     = mat != null && mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f,
                    tiling          = mat != null && mat.HasProperty("_BaseMap")     ? mat.GetTextureScale("_BaseMap")  : Vector2.one,
                    offset          = mat != null && mat.HasProperty("_BaseMap")     ? mat.GetTextureOffset("_BaseMap") : Vector2.zero,
                    cullMode        = mat != null && mat.HasProperty("_Cull")        ? (int) mat.GetFloat("_Cull")      : 2,
                    baseColor       = mat != null && mat.HasProperty("_BaseColor")   ? mat.GetColor("_BaseColor")       : Color.white,
                    emissionColor   = mat != null && mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black,
                    metallic        = mat != null && mat.HasProperty("_Metallic")    ? mat.GetFloat("_Metallic")        : 0f,
                    smoothness      = mat != null && mat.HasProperty("_Smoothness")  ? mat.GetFloat("_Smoothness")      : 0f,
                    lit             = mat == null || mat.shader == null || mat.shader.name != "Custom/CustomObjectUnlit",
                };
                ExtractExtras(mat, materials[i], textureLibrary);
            }

            var part = new CustomMapObjectPartData
            {
                meshRef       = meshKey,
                localPosition = child.position,
                localRotation = child.rotation.eulerAngles,
                localScale    = child.lossyScale,
                materials     = materials,
            };

            return part;
        }

        static void ExtractExtras(Material mat, CustomMapObjectMaterialData md, Dictionary<string, TextureData> textureLibrary)
        {
            if (mat == null || mat.shader == null) return;

            var extras = new Dictionary<string, MaterialPropertyData>();
            int propCount = mat.shader.GetPropertyCount();
            for (int p = 0; p < propCount; p++)
            {
                string name = mat.shader.GetPropertyName(p);
                if (coreMaterialProperties.Contains(name)) continue;

                var ptype = mat.shader.GetPropertyType(p);
                switch (ptype)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        extras[name] = new MaterialPropertyData { type = 0, floatValue = mat.GetFloat(name) };
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        var col = mat.GetColor(name);
                        extras[name] = new MaterialPropertyData { type = 1, vectorValue = new Vector4(col.r, col.g, col.b, col.a) };
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        extras[name] = new MaterialPropertyData { type = 2, vectorValue = mat.GetVector(name) };
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        var tex = mat.GetTexture(name) as Texture2D;
                        if (tex == null) break;
                        string texKey = GetAssetKey(tex);
                        if (!textureLibrary.ContainsKey(texKey))
                        {
                            var etsw = System.Diagnostics.Stopwatch.StartNew();
                            textureLibrary[texKey] = new TextureData(tex);
                            s_timeTextureEncode += etsw.ElapsedMilliseconds;
                        }
                        extras[name] = new MaterialPropertyData { type = 3, textureRef = texKey };
                        break;
                }
            }
            md.extraProps = extras.Count > 0 ? extras : null;
            md.keywords = mat.shaderKeywords;
        }

        static void ExtractTrees(Terrain terrain, MapTerrainChunkData chunkData,
            Dictionary<string, MeshData> meshLibrary, Dictionary<string, TextureData> textureLibrary)
        {
            var tdata = terrain.terrainData;
            var protos = tdata.treePrototypes;
            var insts  = tdata.treeInstances;
            if (protos == null || protos.Length == 0) return;

            var instancesByProto = new List<TreeInstance>[protos.Length];
            for (int i = 0; i < protos.Length; i++)
                instancesByProto[i] = new List<TreeInstance>();
            if (insts != null)
                foreach (var inst in insts)
                    if (inst.prototypeIndex >= 0 && inst.prototypeIndex < protos.Length)
                        instancesByProto[inst.prototypeIndex].Add(inst);

            var protoDataList = new List<TreePrototypeData>();
            for (int p = 0; p < protos.Length; p++)
            {
                var prefab = protos[p].prefab;
                if (prefab == null || instancesByProto[p].Count == 0) continue;
                protoDataList.Add(BuildTreePrototype(prefab, instancesByProto[p], meshLibrary, textureLibrary));
            }
            chunkData.treePrototypes = protoDataList.Count > 0 ? protoDataList.ToArray() : null;
        }

        static TreePrototypeData BuildTreePrototype(GameObject prefab, List<TreeInstance> instances,
            Dictionary<string, MeshData> meshLibrary, Dictionary<string, TextureData> textureLibrary)
        {
            var data = new TreePrototypeData();
            var root = prefab.transform;

            var mapObject = prefab.GetComponent<MapObject>();
            if (mapObject != null && !string.IsNullOrEmpty(mapObject.id))
            {
                data.objectId = mapObject.id;
                PackTreeInstances(data, instances);
                return data;
            }

            var lodGroupsInObj = prefab.GetComponentsInChildren<LODGroup>(true);
            var rendererLodMap = new Dictionary<Renderer, (int groupIndex, int lodIndex)>();
            var lodGroupDataList = new List<LODGroupData>();
            for (int g = 0; g < lodGroupsInObj.Length; g++)
            {
                var lg = lodGroupsInObj[g];
                var lods = lg.GetLODs();
                var transitions = new float[lods.Length];
                var fadeWidths  = new float[lods.Length];
                for (int l = 0; l < lods.Length; l++)
                {
                    transitions[l] = lods[l].screenRelativeTransitionHeight;
                    fadeWidths[l]  = lods[l].fadeTransitionWidth;
                    if (lods[l].renderers == null) continue;
                    foreach (var r in lods[l].renderers)
                        if (r != null)
                            rendererLodMap[r] = (g, l);
                }
                lodGroupDataList.Add(new LODGroupData {
                    localPosition       = root.InverseTransformPoint(lg.transform.position),
                    localReferencePoint = lg.localReferencePoint,
                    size                = lg.size,
                    transitions         = transitions,
                    fadeWidths          = fadeWidths,
                    fadeMode            = (int) lg.fadeMode,
                    animateCrossFading  = lg.animateCrossFading,
                });
            }

            var partsList = new List<CustomMapObjectPartData>();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                var mats = mr != null ? mr.sharedMaterials : null;
                var part = ExtractPart(mf.sharedMesh, mats, mf.transform, root, meshLibrary, textureLibrary);
                if (part == null) continue;
                part.localPosition = root.InverseTransformPoint(mf.transform.position);
                part.localRotation = (Quaternion.Inverse(root.rotation) * mf.transform.rotation).eulerAngles;
                var childScale = mf.transform.lossyScale;
                var rootScale  = root.lossyScale;
                part.localScale = new Vector3(
                    rootScale.x != 0f ? childScale.x / rootScale.x : childScale.x,
                    rootScale.y != 0f ? childScale.y / rootScale.y : childScale.y,
                    rootScale.z != 0f ? childScale.z / rootScale.z : childScale.z);
                if (mr != null)
                {
                    part.shadowCastingMode = (int) mr.shadowCastingMode;
                    if (rendererLodMap.TryGetValue(mr, out var lod))
                    {
                        part.lodGroupIndex = lod.groupIndex;
                        part.lodIndex      = lod.lodIndex;
                    }
                }
                partsList.Add(part);
            }

            data.parts = partsList.ToArray();
            data.lodGroups = lodGroupDataList.Count > 0 ? lodGroupDataList.ToArray() : null;

            var colliderList = new List<ColliderData>();
            foreach (var col in prefab.GetComponentsInChildren<Collider>(true))
            {
                var cd = ColliderFromUnity(col, root, meshLibrary);
                if (cd == null) continue;
                cd.surfaceType   = SurfaceType.Generic;
                cd.localPosition = root.InverseTransformPoint(cd.localPosition);
                cd.localRotation = (Quaternion.Inverse(root.rotation) * col.transform.rotation).eulerAngles;
                var colScale  = col.transform.lossyScale;
                var rootScale = root.lossyScale;
                cd.localScale = new Vector3(
                    rootScale.x != 0f ? colScale.x / rootScale.x : colScale.x,
                    rootScale.y != 0f ? colScale.y / rootScale.y : colScale.y,
                    rootScale.z != 0f ? colScale.z / rootScale.z : colScale.z);
                colliderList.Add(cd);
            }
            data.colliders = colliderList.Count > 0 ? colliderList.ToArray() : null;

            PackTreeInstances(data, instances);
            return data;
        }

        static void PackTreeInstances(TreePrototypeData data, List<TreeInstance> instances)
        {
            var floatBuf = new float[6];
            data.instances = new byte[instances.Count * TreePrototypeData.InstanceStride];
            for (int i = 0; i < instances.Count; i++)
            {
                int o = i * TreePrototypeData.InstanceStride;
                var inst = instances[i];
                floatBuf[0] = inst.position.x;
                floatBuf[1] = inst.position.y;
                floatBuf[2] = inst.position.z;
                floatBuf[3] = inst.widthScale;
                floatBuf[4] = inst.heightScale;
                floatBuf[5] = inst.rotation;
                Buffer.BlockCopy(floatBuf, 0, data.instances, o, 24);
                Color32 c = inst.color;
                data.instances[o + 24] = c.r;
                data.instances[o + 25] = c.g;
                data.instances[o + 26] = c.b;
                data.instances[o + 27] = c.a;
            }
        }

        static CustomObjectRenderMode GetRenderMode(Material mat)
        {
            if (mat == null) return CustomObjectRenderMode.Opaque;
            if (mat.renderQueue >= (int) UnityEngine.Rendering.RenderQueue.Transparent) return CustomObjectRenderMode.Transparent;
            if (mat.IsKeywordEnabled("_ALPHATEST_ON")) return CustomObjectRenderMode.AlphaClip;
            return CustomObjectRenderMode.Opaque;
        }

        static string ExtractTexKey(Material mat, string prop, Dictionary<string, TextureData> library)
        {
            if (mat == null || !mat.HasProperty(prop)) return null;
            var tex = mat.GetTexture(prop) as Texture2D;
            if (tex == null) return null;

            string key = GetAssetKey(tex);
            if (!library.ContainsKey(key))
            {
                var tsw = System.Diagnostics.Stopwatch.StartNew();
                library[key] = new TextureData(tex);
                s_timeTextureEncode += tsw.ElapsedMilliseconds;
            }
            return key;
        }

        static string GetAssetKey(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return asset.name;

            if (path == "Library/unity default resources" || path == "Resources/unity_builtin_extra")
                return "builtin/" + asset.name;

            if (path.StartsWith("Assets/"))
                path = path.Substring(7);

            int dot = path.LastIndexOf('.');
            if (dot > 0)
                path = path.Substring(0, dot);

            if (UnityEditor.AssetDatabase.IsSubAsset(asset))
                path += "/" + asset.name;

            return path.Replace('\\', '/');
#else
            return asset.name;
#endif
        }

        static ColliderData ColliderFromUnity(Collider col, Transform root, Dictionary<string, MeshData> meshLib)
        {
            var cd = new ColliderData();
            Transform t = col.transform;

            cd.localRotation = t.rotation.eulerAngles;
            cd.localScale    = t.lossyScale;

            Vector3 colliderCenter = Vector3.zero;

            switch (col)
            {
                case BoxCollider box:
                    cd.shape  = ColliderShape.Box;
                    cd.center = box.center;
                    cd.size   = box.size;
                    colliderCenter = box.center;
                    break;
                case SphereCollider sphere:
                    cd.shape  = ColliderShape.Sphere;
                    cd.center = sphere.center;
                    cd.radius = sphere.radius;
                    colliderCenter = sphere.center;
                    break;
                case CapsuleCollider capsule:
                    cd.shape     = ColliderShape.Capsule;
                    cd.center    = capsule.center;
                    cd.radius    = capsule.radius;
                    cd.height    = capsule.height;
                    cd.direction = capsule.direction;
                    colliderCenter = capsule.center;
                    break;
                case MeshCollider mc:
                    if (mc.sharedMesh == null) return null;
                    if (!mc.convex)
                    {
                        Debug.LogError($"[MapExporter] MeshCollider on '{mc.gameObject.name}' must be convex. Enable Convex on the MeshCollider component.");
                        return null;
                    }
                    if (!mc.sharedMesh.isReadable)
                    {
                        Debug.LogError($"[MapExporter] Collider mesh '{mc.sharedMesh.name}' on '{mc.gameObject.name}' is not readable. Enable Read/Write in import settings.");
                        return null;
                    }
                    cd.shape = ColliderShape.Mesh;
                    string key = GetAssetKey(mc.sharedMesh);
                    if (!meshLib.ContainsKey(key))
                    {
                        var cmsw = System.Diagnostics.Stopwatch.StartNew();
                        meshLib[key] = new MeshData(mc.sharedMesh);
                        s_timeMeshPack += cmsw.ElapsedMilliseconds;
                    }
                    cd.meshRef = key;
                    break;
                default:
                    return null;
            }

            cd.localPosition = t.TransformPoint(colliderCenter);

            return cd;
        }

        public void PopulateFromImport(MapData map, List<MapTerrainChunk> importedChunks)
        {
            mapName = map.name;
            idOverride = map.id;
            camStartPosition = map.camStartPosition;
            allowBackgroundMountains = map.allowBackgroundMountains;
            overrideTime = map.overrideTime;
            dayTime = map.dayTime;
            dayTimeHours = map.dayTimeHours;
            rawTime = map.overrideTime && map.dayTimeHours < 0;
            sunAngle = map.sunAngle;
            chunks = importedChunks;
        }

        public void AutoImport()
        {
            chunks.Clear();
            var terrains = transform.GetComponentsInChildren<Terrain>();
            Log($"AutoImport: found {terrains.Length} terrain(s) under '{gameObject.name}'");

            foreach(var terrain in terrains)
            {
                var chunk = new MapTerrainChunk()
                {
                    terrain = terrain
                };

                var material = terrain.materialTemplate;
                chunk.snowMask = material != null ? material.GetTexture("_SnowMask") as Texture2D : null;
                chunk.snowMask2 = material != null && material.HasProperty("_SnowMask2") ? material.GetTexture("_SnowMask2") as Texture2D : null;

                var child = terrain.transform.childCount > 0 ? terrain.transform.GetChild(0) : null;
                if(child && child.name.ToLower().Contains("objects"))
                    chunk.mapObjectContainer = child;

                Log($"  Chunk '{terrain.name}': snowMask={chunk.snowMask?.name ?? "none"}, snowMask2={chunk.snowMask2?.name ?? "none"}, objectContainer={chunk.mapObjectContainer?.name ?? "none"}");
                chunks.Add(chunk);
            }

            Log($"AutoImport complete: {chunks.Count} chunk(s)");
        }
    }

}
