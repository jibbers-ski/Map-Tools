using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jibbers.MapTools
{

    #if UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(MapImporter))]
    public class MapImporterEditor : Editor
    {

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(20);

            var importer = (MapImporter) target;
            if (GUILayout.Button("Import"))
                importer.Import();
        }
    }
    #endif

    public class MapImporter : MonoBehaviour
    {

        [SerializeField] string mapFileName;
        [SerializeField] Material material;
        [SerializeField] Material customObjectMaterial;
        [SerializeField] Material customObjectUnlitMaterial;

        public void Import()
        {
            if(transform.childCount > 0)
            {
                Debug.LogError("Please manually delete all children of this object and try again");
                return;
            }

            var filePath = Utility.DataPath + "Maps/" + mapFileName + ".jbrmap";
            if(!File.Exists(filePath))
            {
                Debug.LogError("File doesn't exist: " + filePath);
                return;
            }

            var data = File.ReadAllText(filePath);

            var serializer = new JsonSerializer();
            serializer.Begin(false, data);

            var map = new MapData();
            map.Serialize(serializer);

            serializer.Close();

            SpawnMap(map);
        }

        public void SpawnMap(MapData map)
        {
            var objPrefabs = Resources.LoadAll<Transform>("MapObjects/Placeholders/");
            var objPrefabDict = new Dictionary<string,Transform>();
            foreach(var prefab in objPrefabs)
                objPrefabDict[prefab.name.Replace("_placeholder","")] = prefab;

            var meshes = new Dictionary<string, Mesh>();
            if (map.meshLibrary != null)
                foreach (var kv in map.meshLibrary)
                {
                    var mesh = kv.Value.GetMesh();
                    mesh.name = kv.Key;
                    meshes[kv.Key] = mesh;
                }

            var textures = new Dictionary<string, Texture2D>();
            if (map.textureLibrary != null)
                foreach (var kv in map.textureLibrary)
                {
                    var tex = kv.Value.GetTexture();
                    tex.name = kv.Key;
                    textures[kv.Key] = tex;
                }

            var matCache = new Dictionary<string, Material>();
            var chunksList = new List<MapTerrainChunk>();

            foreach(var chunk in map.chunks)
            {
                var terrainData = new TerrainData() {
                    heightmapResolution = chunk.heightmapResolution,
                    size = chunk.size,
                };

                var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
                terrainGO.transform.position = chunk.position;

                var terrain = terrainGO.GetComponent<Terrain>();
                terrain.ImportHeightmapR16(chunk.terrainData);

                terrain.transform.position = chunk.position;
                terrain.transform.parent = transform;

                terrain.materialTemplate = new Material(material);

                Texture2D snowMaskTex = null;
                if(chunk.snowMaskData != null)
                {
                    chunk.snowMaskData.compression = 0;
                    snowMaskTex = chunk.snowMaskData.GetTexture(forceMips: true);
                    terrain.materialTemplate.SetTexture("_SnowMask", snowMaskTex);
                    if (terrain.materialTemplate.HasProperty("_UseMasks"))
                        terrain.materialTemplate.SetFloat("_UseMasks", 1f);
                }

                if (terrain.materialTemplate.HasProperty("_SnowMask4Channel"))
                    terrain.materialTemplate.SetFloat("_SnowMask4Channel", chunk.snowMask4Channel ? 1f : 0f);

                Texture2D snowMask2Tex = null;
                if (chunk.snowMask2Data != null && terrain.materialTemplate.HasProperty("_SnowMask2"))
                {
                    chunk.snowMask2Data.compression = 0;
                    snowMask2Tex = chunk.snowMask2Data.GetTexture();
                    terrain.materialTemplate.SetTexture("_SnowMask2", snowMask2Tex);
                }

                ApplyThirdLayer(terrain.materialTemplate, map.version, snowMask2Tex != null);

                terrain.Flush();

                var mapObjects = new GameObject("MapObjects").transform;
                mapObjects.parent = terrainGO.transform;
                foreach(var mapObject in chunk.objects)
                {
                    if (!objPrefabDict.TryGetValue(mapObject.id, out var prefab))
                    {
                        Debug.LogError($"[MapImporter] Unknown map object id '{mapObject.id}', skipping object");
                        continue;
                    }
                    #if UNITY_EDITOR
                        var newObj = (Transform)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, mapObjects);
                    #else
                        var newObj = Instantiate(prefab, mapObjects);
                    #endif
                    newObj.transform.localScale = mapObject.scale;
                    newObj.transform.position = mapObject.position;
                    newObj.transform.rotation = Quaternion.Euler(mapObject.rotation);

                    if (mapObject.parameters != null && mapObject.parameters.Count > 0)
                    {
                        var mapObjComp = newObj.GetComponent<MapObject>();
                        if (mapObjComp != null && mapObjComp.parameters != null)
                        {
                            foreach (var existing in mapObjComp.parameters)
                            {
                                if (mapObject.parameters.TryGetValue(existing.name, out var imported))
                                {
                                    existing.type        = imported.type;
                                    existing.intValue    = imported.intValue;
                                    existing.floatValue  = imported.floatValue;
                                    existing.boolValue   = imported.boolValue;
                                    existing.stringValue = imported.stringValue;
                                }
                            }
                            #if UNITY_EDITOR
                            UnityEditor.EditorUtility.SetDirty(mapObjComp);
                            #endif
                        }
                    }
                }

                if (chunk.customObjects != null && chunk.customObjects.Length > 0)
                {
                    var customRoot = new GameObject("CustomObjects").transform;
                    customRoot.parent = terrainGO.transform;

                    foreach (var obj in chunk.customObjects)
                    {
                        bool hasParts = obj.parts != null && obj.parts.Length > 0;
                        bool hasLights = obj.lights != null && obj.lights.Length > 0;
                        if (!hasParts && !hasLights) continue;

                        var rootGO = new GameObject("CustomObject");
                        rootGO.transform.parent = customRoot;
                        rootGO.transform.position = obj.position;
                        rootGO.transform.rotation = Quaternion.Euler(obj.rotation);

                        var cmo = rootGO.AddComponent<CustomMapObject>();
                        cmo.surfaceType      = obj.surfaceType;
                        cmo.canStabilize     = obj.canStabilize;
                        cmo.canRotate        = obj.canRotate;
                        cmo.canMagnetize     = obj.canMagnetize;
                        cmo.intendedUpMethod = obj.intendedUpMethod;
                        cmo.disableDistanceCulling = obj.disableDistanceCulling;
                        cmo.timedVisibility  = obj.timedVisibility;
                        cmo.visibleFromHour  = obj.visibleFromHour;
                        cmo.visibleUntilHour = obj.visibleUntilHour;

                        var partRenderersByLodGroup = new Dictionary<int, List<(int lodIndex, Renderer renderer)>>();

                        if (hasParts) foreach (var part in obj.parts)
                        {
                            if (string.IsNullOrEmpty(part.meshRef) || !meshes.ContainsKey(part.meshRef)) continue;

                            var partGO = new GameObject(part.meshRef);
                            partGO.transform.parent = rootGO.transform;
                            partGO.transform.position = part.localPosition;
                            partGO.transform.rotation = Quaternion.Euler(part.localRotation);
                            partGO.transform.localScale = part.localScale;

                            partGO.AddComponent<MeshFilter>().sharedMesh = meshes[part.meshRef];

                            int matCount = part.materials != null ? part.materials.Length : 0;
                            var mats = new Material[matCount];
                            for (int mi = 0; mi < matCount; mi++)
                                mats[mi] = GetOrCreateMaterial(part.materials[mi], textures, matCache);

                            var partRenderer = partGO.AddComponent<MeshRenderer>();
                            partRenderer.sharedMaterials = mats;
                            partRenderer.shadowCastingMode = (UnityEngine.Rendering.ShadowCastingMode) part.shadowCastingMode;

                            if (part.lodGroupIndex >= 0)
                            {
                                if (!partRenderersByLodGroup.TryGetValue(part.lodGroupIndex, out var list))
                                    partRenderersByLodGroup[part.lodGroupIndex] = list = new List<(int, Renderer)>();
                                list.Add((part.lodIndex, partRenderer));
                            }
                        }

                        if (obj.lodGroups != null && obj.lodGroups.Length > 0)
                        {
                            for (int g = 0; g < obj.lodGroups.Length; g++)
                            {
                                var lgd = obj.lodGroups[g];
                                var lgGO = new GameObject("LODGroup");
                                lgGO.transform.SetParent(rootGO.transform, false);
                                lgGO.transform.localPosition = lgd.localPosition;

                                var lodGroup = lgGO.AddComponent<LODGroup>();
                                lodGroup.localReferencePoint = lgd.localReferencePoint;
                                lodGroup.size = lgd.size;
                                lodGroup.fadeMode = (LODFadeMode) lgd.fadeMode;
                                lodGroup.animateCrossFading = lgd.animateCrossFading;

                                var byLod = new Dictionary<int, List<Renderer>>();
                                if (partRenderersByLodGroup.TryGetValue(g, out var entries))
                                {
                                    foreach (var (lodIdx, renderer) in entries)
                                    {
                                        renderer.transform.SetParent(lgGO.transform, true);
                                        if (!byLod.TryGetValue(lodIdx, out var rs))
                                            byLod[lodIdx] = rs = new List<Renderer>();
                                        rs.Add(renderer);
                                    }
                                }

                                int lodCount = lgd.transitions != null ? lgd.transitions.Length : 0;
                                var lods = new LOD[lodCount];
                                for (int i = 0; i < lodCount; i++)
                                {
                                    var rs = byLod.TryGetValue(i, out var list) ? list.ToArray() : Array.Empty<Renderer>();
                                    var lod = new LOD(lgd.transitions[i], rs);
                                    lod.fadeTransitionWidth = lgd.fadeWidths != null && i < lgd.fadeWidths.Length ? lgd.fadeWidths[i] : 0f;
                                    lods[i] = lod;
                                }
                                lodGroup.SetLODs(lods);
                            }
                        }

                        if (hasLights) foreach (var ld in obj.lights)
                        {
                            var lightGO = new GameObject("Light");
                            lightGO.transform.parent = rootGO.transform;
                            lightGO.transform.position = ld.localPosition;
                            lightGO.transform.rotation = Quaternion.Euler(ld.localRotation);

                            var light = lightGO.AddComponent<Light>();
                            light.type            = (LightType) ld.type;
                            light.color           = ld.color;
                            light.intensity       = ld.intensity;
                            light.range           = ld.range;
                            light.spotAngle       = ld.spotAngle;
                            light.innerSpotAngle  = ld.innerSpotAngle;
                            light.shadows         = (LightShadows) ld.shadows;
                            light.shadowStrength  = ld.shadowStrength;
                        }

                        if (obj.colliders != null) foreach (var cd in obj.colliders)
                        {
                            var colGO = new GameObject(ColliderName(cd.shape));
                            colGO.transform.parent = rootGO.transform;
                            colGO.transform.position = cd.localPosition;
                            colGO.transform.rotation = Quaternion.Euler(cd.localRotation);
                            colGO.transform.localScale = cd.localScale;
                            AddColliderComponent(colGO, cd, meshes);
                        }
                    }
                }

                SpawnTrees(chunk, terrain, terrainGO.transform, meshes, textures, matCache, objPrefabDict);

                chunksList.Add(new MapTerrainChunk {
                    terrain = terrain,
                    mapObjectContainer = mapObjects,
                    snowMask = snowMaskTex,
                    snowMask2 = snowMask2Tex,
                    repeats = chunk.repeats,
                    repeatOffset = chunk.repeatOffset,
                });
            }

            var spawnPoints = new GameObject("SpawnPoints");
            spawnPoints.transform.parent = transform;
            foreach(var spawnPoint in map.spawnPoints)
            {
                var newSpawnPoint = new GameObject(spawnPoint.name).AddComponent<MapSpawnPoint>();
                newSpawnPoint.transform.parent = spawnPoints.transform;
                newSpawnPoint.transform.position = spawnPoint.position;
                newSpawnPoint.transform.rotation = Quaternion.Euler(spawnPoint.rotation);
                newSpawnPoint.velocity = spawnPoint.velocity;
            }

            var exporter = GetComponent<MapExporter>();
            if (exporter == null)
                exporter = gameObject.AddComponent<MapExporter>();
            exporter.PopulateFromImport(map, chunksList);
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(exporter);
            #endif
        }

        // Third (powder) layer. v0.4+ maps author the powder mask in the snow-mask alpha channel;
        // flow direction rides SnowMask2 R (angle), applied per layer via the exported flags.
        // Neutralise any built-in third data carried by the shared terrain material so a map's
        // powder/flow comes only from its own masks.
        static void ApplyThirdLayer(Material mat, string version, bool hasFlowMask2)
        {
            if (mat.HasProperty("_ThirdMaskTex"))     mat.SetTexture("_ThirdMaskTex", Texture2D.blackTexture);
            if (mat.HasProperty("_FlowFromMask2"))    mat.SetFloat("_FlowFromMask2", hasFlowMask2 ? 1f : 0f);
            if (mat.HasProperty("_SnowFlowEnabled"))  mat.SetFloat("_SnowFlowEnabled", hasFlowMask2 ? 1f : 0f);
            if (mat.HasProperty("_ThirdFlowEnabled")) mat.SetFloat("_ThirdFlowEnabled", hasFlowMask2 ? 1f : 0f);
            if (mat.HasProperty("_ThirdFromAlpha"))   mat.SetFloat("_ThirdFromAlpha", MapSupportsThird(version) ? 1f : 0f);
        }

        static bool MapSupportsThird(string version)
            => System.Version.TryParse(version, out var v) && v >= new System.Version(0, 4);

        static string ColliderName(ColliderShape shape) => shape switch {
            ColliderShape.Box     => "BoxCollider",
            ColliderShape.Sphere  => "SphereCollider",
            ColliderShape.Capsule => "CapsuleCollider",
            ColliderShape.Mesh    => "MeshCollider",
            _                     => "Collider",
        };

        static void AddColliderComponent(GameObject colGO, ColliderData cd, Dictionary<string, Mesh> meshes)
        {
            switch (cd.shape)
            {
                case ColliderShape.Box:
                    colGO.AddComponent<BoxCollider>().size = cd.size;
                    break;
                case ColliderShape.Sphere:
                    colGO.AddComponent<SphereCollider>().radius = cd.radius;
                    break;
                case ColliderShape.Capsule:
                    var cap = colGO.AddComponent<CapsuleCollider>();
                    cap.radius    = cd.radius;
                    cap.height    = cd.height;
                    cap.direction = cd.direction;
                    break;
                case ColliderShape.Mesh:
                    if (!string.IsNullOrEmpty(cd.meshRef) && meshes.TryGetValue(cd.meshRef, out var colMesh))
                    {
                        var mc = colGO.AddComponent<MeshCollider>();
                        mc.sharedMesh = colMesh;
                        mc.convex = true;
                    }
                    break;
            }
        }

        static void ApplyRenderMode(Material mat, CustomObjectRenderMode mode, float alphaCutoff)
        {
            switch (mode)
            {
                case CustomObjectRenderMode.Opaque:
                    mat.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1f);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Geometry;
                    break;
                case CustomObjectRenderMode.AlphaClip:
                    mat.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetFloat("_Cutoff", alphaCutoff);
                    mat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.AlphaTest;
                    break;
                case CustomObjectRenderMode.Transparent:
                    mat.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }
        }

        static void SetTex(Material mat, string prop, string texRef, Dictionary<string, Texture2D> lib)
        {
            if (!string.IsNullOrEmpty(texRef) && lib.TryGetValue(texRef, out var tex))
                mat.SetTexture(prop, tex);
        }

        static string ExtrasKey(CustomMapObjectMaterialData md)
        {
            var sb = new System.Text.StringBuilder();
            if (md.extraProps != null)
                foreach (var kv in md.extraProps.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    sb.Append(kv.Key).Append('=');
                    switch (kv.Value.type)
                    {
                        case 0: sb.Append(kv.Value.floatValue); break;
                        case 1:
                        case 2: sb.Append(kv.Value.vectorValue); break;
                        case 3: sb.Append(kv.Value.textureRef ?? ""); break;
                    }
                    sb.Append(';');
                }
            if (md.keywords != null)
                foreach (var kw in md.keywords.OrderBy(x => x, StringComparer.Ordinal))
                    sb.Append(kw).Append(';');
            return sb.ToString();
        }

        void SpawnTrees(MapTerrainChunkData chunk, Terrain terrain, Transform terrainTransform,
            Dictionary<string, Mesh> meshes, Dictionary<string, Texture2D> textures,
            Dictionary<string, Material> matCache, Dictionary<string, Transform> objPrefabDict)
        {
            if (chunk.treePrototypes == null || chunk.treePrototypes.Length == 0) return;

            var prototypesContainer = new GameObject("TreePrototypes").transform;
            prototypesContainer.SetParent(terrainTransform, false);
            prototypesContainer.gameObject.SetActive(false);

            var protoList = new List<TreePrototype>();
            var instList  = new List<TreeInstance>();
            var floatBuf  = new float[6];

            foreach (var protoData in chunk.treePrototypes)
            {
                GameObject protoGO;
                if (!string.IsNullOrEmpty(protoData.objectId))
                {
                    if (!objPrefabDict.TryGetValue(protoData.objectId, out var objPrefab))
                    {
                        Debug.LogError($"[MapImporter] Unknown tree map object id '{protoData.objectId}', skipping prototype");
                        continue;
                    }
                    protoGO = objPrefab.gameObject;
                }
                else
                    protoGO = BuildTreePrototype(protoData, prototypesContainer, meshes, textures, matCache);
                if (protoGO == null) continue;

                int protoIndex = protoList.Count;
                protoList.Add(new TreePrototype { prefab = protoGO });

                int count = protoData.InstanceCount;
                for (int i = 0; i < count; i++)
                {
                    int o = i * TreePrototypeData.InstanceStride;
                    Buffer.BlockCopy(protoData.instances, o, floatBuf, 0, 24);
                    instList.Add(new TreeInstance {
                        position    = new Vector3(floatBuf[0], floatBuf[1], floatBuf[2]),
                        widthScale  = floatBuf[3],
                        heightScale = floatBuf[4],
                        rotation    = floatBuf[5],
                        color = new Color32(
                            protoData.instances[o + 24],
                            protoData.instances[o + 25],
                            protoData.instances[o + 26],
                            protoData.instances[o + 27]),
                        lightmapColor = Color.white,
                        prototypeIndex = protoIndex,
                    });
                }
            }

            terrain.terrainData.treePrototypes = protoList.ToArray();
            terrain.terrainData.treeInstances  = instList.ToArray();
            terrain.Flush();
        }

        const float TreeCullScreenHeight = 0.01f;

        GameObject BuildTreePrototype(TreePrototypeData protoData, Transform container,
            Dictionary<string, Mesh> meshes, Dictionary<string, Texture2D> textures, Dictionary<string, Material> matCache)
        {
            if (protoData.parts == null || protoData.parts.Length == 0) return null;

            var protoGO = new GameObject("TreePrototype");
            protoGO.transform.SetParent(container, false);

            var allRenderers   = new List<Renderer>();
            var renderersByLod = new Dictionary<int, List<Renderer>>();
            var unassigned     = new List<Renderer>();

            foreach (var part in protoData.parts)
            {
                if (string.IsNullOrEmpty(part.meshRef) || !meshes.ContainsKey(part.meshRef)) continue;

                var partGO = new GameObject(part.meshRef);
                partGO.transform.SetParent(protoGO.transform, false);
                partGO.transform.localPosition = part.localPosition;
                partGO.transform.localRotation = Quaternion.Euler(part.localRotation);
                partGO.transform.localScale    = part.localScale;

                partGO.AddComponent<MeshFilter>().sharedMesh = meshes[part.meshRef];

                int matCount = part.materials != null ? part.materials.Length : 0;
                var mats = new Material[matCount];
                for (int mi = 0; mi < matCount; mi++)
                    mats[mi] = GetOrCreateMaterial(part.materials[mi], textures, matCache);

                var partRenderer = partGO.AddComponent<MeshRenderer>();
                partRenderer.sharedMaterials = mats;
                partRenderer.shadowCastingMode = (UnityEngine.Rendering.ShadowCastingMode) part.shadowCastingMode;

                allRenderers.Add(partRenderer);
                if (part.lodGroupIndex >= 0)
                {
                    if (!renderersByLod.TryGetValue(part.lodIndex, out var list))
                        renderersByLod[part.lodIndex] = list = new List<Renderer>();
                    list.Add(partRenderer);
                }
                else
                    unassigned.Add(partRenderer);
            }

            if (allRenderers.Count == 0)
            {
                if (Application.isPlaying) Destroy(protoGO);
                else DestroyImmediate(protoGO);
                return null;
            }

            if (protoData.colliders != null) foreach (var cd in protoData.colliders)
            {
                var colGO = new GameObject(ColliderName(cd.shape));
                colGO.transform.SetParent(protoGO.transform, false);
                colGO.transform.localPosition = cd.localPosition;
                colGO.transform.localRotation = Quaternion.Euler(cd.localRotation);
                colGO.transform.localScale    = cd.localScale;
                AddColliderComponent(colGO, cd, meshes);
            }

            var lodGroup = protoGO.AddComponent<LODGroup>();
            var lgd = protoData.lodGroups != null && protoData.lodGroups.Length > 0 ? protoData.lodGroups[0] : null;
            if (lgd != null && lgd.transitions != null && lgd.transitions.Length > 0)
            {
                lodGroup.localReferencePoint = lgd.localReferencePoint;
                lodGroup.size = lgd.size;
                lodGroup.fadeMode = (LODFadeMode) lgd.fadeMode;
                lodGroup.animateCrossFading = lgd.animateCrossFading;

                var lods = new LOD[lgd.transitions.Length];
                for (int i = 0; i < lods.Length; i++)
                {
                    renderersByLod.TryGetValue(i, out var list);
                    var rs = list ?? new List<Renderer>();
                    if (i == 0) rs.AddRange(unassigned);
                    var lod = new LOD(lgd.transitions[i], rs.ToArray());
                    lod.fadeTransitionWidth = lgd.fadeWidths != null && i < lgd.fadeWidths.Length ? lgd.fadeWidths[i] : 0f;
                    lods[i] = lod;
                }
                lodGroup.SetLODs(lods);
            }
            else
            {
                lodGroup.SetLODs(new[] { new LOD(TreeCullScreenHeight, allRenderers.ToArray()) });
                lodGroup.RecalculateBounds();
            }

            return protoGO;
        }

        Material GetOrCreateMaterial(CustomMapObjectMaterialData md,
            Dictionary<string, Texture2D> textures, Dictionary<string, Material> matCache)
        {
            string matKey = $"{md.baseTexRef}|{md.metallicTexRef}|{md.roughnessTexRef}|{md.normalTexRef}|{md.emissionTexRef}|{(int) md.renderMode}|{md.alphaCutoff}|{md.tiling}|{md.offset}|{md.cullMode}|{md.baseColor}|{md.emissionColor}|{md.metallic}|{md.smoothness}|{md.lit}|{ExtrasKey(md)}";
            if (matCache.TryGetValue(matKey, out Material mat))
                return mat;

            if (md.lit)
                mat = customObjectMaterial != null
                    ? new Material(customObjectMaterial)
                    : new Material(Shader.Find("Custom/CustomObjectLit"));
            else
                mat = customObjectUnlitMaterial != null
                    ? new Material(customObjectUnlitMaterial)
                    : new Material(Shader.Find("Custom/CustomObjectUnlit"));
            SetTex(mat, "_BaseMap",      md.baseTexRef,      textures);
            SetTex(mat, "_MetallicMap",  md.metallicTexRef,  textures);
            SetTex(mat, "_RoughnessMap", md.roughnessTexRef, textures);
            SetTex(mat, "_NormalMap",    md.normalTexRef,    textures);
            SetTex(mat, "_EmissionMap",  md.emissionTexRef,  textures);
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureScale("_BaseMap",  md.tiling);
                mat.SetTextureOffset("_BaseMap", md.offset);
            }
            if (mat.HasProperty("_Cull"))          mat.SetFloat("_Cull",          md.cullMode);
            if (mat.HasProperty("_BaseColor"))     mat.SetColor("_BaseColor",     md.baseColor);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", md.emissionColor);
            if (mat.HasProperty("_Metallic"))      mat.SetFloat("_Metallic",      md.metallic);
            if (mat.HasProperty("_Smoothness"))    mat.SetFloat("_Smoothness",    md.smoothness);
            ApplyRenderMode(mat, md.renderMode, md.alphaCutoff);
            ApplyExtras(mat, md, textures);
            mat.enableInstancing = true;
            matCache[matKey] = mat;
            return mat;
        }

        static void ApplyExtras(Material mat, CustomMapObjectMaterialData md, Dictionary<string, Texture2D> textures)
        {
            if (md.extraProps != null)
                foreach (var kv in md.extraProps)
                {
                    if (!mat.HasProperty(kv.Key)) continue;
                    var prop = kv.Value;
                    switch (prop.type)
                    {
                        case 0: mat.SetFloat(kv.Key, prop.floatValue); break;
                        case 1: mat.SetColor(kv.Key, new Color(prop.vectorValue.x, prop.vectorValue.y, prop.vectorValue.z, prop.vectorValue.w)); break;
                        case 2: mat.SetVector(kv.Key, prop.vectorValue); break;
                        case 3:
                            if (!string.IsNullOrEmpty(prop.textureRef) && textures.TryGetValue(prop.textureRef, out var tex))
                                mat.SetTexture(kv.Key, tex);
                            break;
                    }
                }
            if (md.keywords != null)
                foreach (var kw in md.keywords)
                    if (!string.IsNullOrEmpty(kw))
                        mat.EnableKeyword(kw);
        }
    }

}
