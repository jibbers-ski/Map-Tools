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
            DrawDefaultInspector();

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

        [Header("Terrain Chunks")]
        public List<MapTerrainChunk> chunks;

        [Header("Debug")]
        public bool enableLogs;

        void Awake() {
            if(autoExportOnAwake)
                Export();
            Destroy(gameObject);
        }

        void Log(string msg) { if (enableLogs) Debug.Log($"[MapExporter] {msg}"); }

        public void Export()
        {
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

            var meshLibrary    = new Dictionary<string, MeshData>();
            var textureLibrary = new Dictionary<string, TextureData>();

            var chunkDatas = new MapTerrainChunkData[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                Log($"Chunk {i}: terrain '{chunks[i].terrain.name}', heightmap {chunks[i].terrain.terrainData.heightmapResolution}");
                chunkDatas[i] = new MapTerrainChunkData(chunks[i]);

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

                    foreach (var mf in obj.GetComponentsInChildren<MeshFilter>())
                    {
                        var mr = mf.GetComponent<MeshRenderer>();
                        var mats = mr != null ? mr.sharedMaterials : null;
                        Log($"    MeshFilter on '{mf.gameObject.name}': mesh={mf.sharedMesh?.name ?? "null"}, mats={(mats != null ? mats.Length : 0)}");
                        var part = ExtractPart(mf.sharedMesh, mats,
                            mf.transform, root, meshLibrary, textureLibrary);
                        if (part != null) partsList.Add(part);
                    }

                    foreach (var smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mats = smr.sharedMaterials;
                        Log($"    SkinnedMeshRenderer on '{smr.gameObject.name}': mesh={smr.sharedMesh?.name ?? "null"}, mats={(mats != null ? mats.Length : 0)}");
                        var part = ExtractPart(smr.sharedMesh, mats,
                            smr.transform, root, meshLibrary, textureLibrary);
                        if (part != null) partsList.Add(part);
                    }

                    if (partsList.Count == 0)
                    {
                        Log($"    Skipped — no valid mesh parts");
                        continue;
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

                    Log($"    Exported: {partsList.Count} part(s), {colliderList.Count} collider(s)");

                    customObjs.Add(new CustomMapObjectData
                    {
                        surfaceType      = obj.surfaceType,
                        canStabilize     = obj.canStabilize,
                        canRotate        = obj.canRotate,
                        canMagnetize     = obj.canMagnetize,
                        intendedUpMethod = obj.intendedUpMethod,
                        position  = root.position,
                        rotation  = root.rotation.eulerAngles,
                        scale     = root.lossyScale,
                        parts     = partsList.ToArray(),
                        colliders = colliderList.Count > 0 ? colliderList.ToArray() : null,
                    });
                }

                chunkDatas[i].customObjects = customObjs.Count > 0 ? customObjs.ToArray() : null;
            }

            Log($"Libraries: {meshLibrary.Count} mesh(es), {textureLibrary.Count} texture(s)");
            foreach (var kv in meshLibrary)
                Log($"  Mesh '{kv.Key}': {kv.Value.vertexCount} verts, {kv.Value.triangleCount / 3} tris");
            foreach (var kv in textureLibrary)
                Log($"  Texture '{kv.Key}': {kv.Value.width}x{kv.Value.height}");

            var map = new MapData() {
                name = mapName,
                id = idOverride,
                camStartPosition = camStartPosition,
                allowBackgroundMountains = allowBackgroundMountains,
                chunks = chunkDatas,
                spawnPoints = spawnPoints.Select(s => new SpawnPointData(s)).ToArray(),
                meshLibrary    = meshLibrary.Count > 0    ? meshLibrary    : null,
                textureLibrary = textureLibrary.Count > 0 ? textureLibrary : null,
            };

            var serializer = new JsonSerializer { EnableCompression = true };
            serializer.Begin(true);
            map.Serialize(serializer);
            serializer.Close();

            var dirPath = Utility.DataPath + "Maps/";
            Directory.CreateDirectory(dirPath);

            var filePath = dirPath + map.id + ".jbrmap";
            File.WriteAllText(filePath, (string) serializer.Data);
            Debug.Log("Saved to: " + filePath);
        }

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
                meshLibrary[meshKey] = new MeshData(mesh);

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
                    renderMode      = GetRenderMode(mat),
                    alphaCutoff     = mat != null && mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f,
                    tiling          = mat != null && mat.HasProperty("_BaseMap")   ? mat.GetTextureScale("_BaseMap")  : Vector2.one,
                    offset          = mat != null && mat.HasProperty("_BaseMap")   ? mat.GetTextureOffset("_BaseMap") : Vector2.zero,
                    cullMode        = mat != null && mat.HasProperty("_Cull")      ? (int) mat.GetFloat("_Cull")      : 2,
                    baseColor       = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")       : Color.white,
                };
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
                library[key] = new TextureData(tex);
            return key;
        }

        static string GetAssetKey(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return asset.name;

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
                        meshLib[key] = new MeshData(mc.sharedMesh);
                    cd.meshRef = key;
                    break;
                default:
                    return null;
            }

            cd.localPosition = t.TransformPoint(colliderCenter);

            return cd;
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

                var child = terrain.transform.childCount > 0 ? terrain.transform.GetChild(0) : null;
                if(child && child.name.ToLower().Contains("objects"))
                    chunk.mapObjectContainer = child;

                Log($"  Chunk '{terrain.name}': snowMask={chunk.snowMask?.name ?? "none"}, objectContainer={chunk.mapObjectContainer?.name ?? "none"}");
                chunks.Add(chunk);
            }

            Log($"AutoImport complete: {chunks.Count} chunk(s)");
        }
    }

}
