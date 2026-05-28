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
                    meshes[kv.Key] = kv.Value.GetMesh();

            var textures = new Dictionary<string, Texture2D>();
            if (map.textureLibrary != null)
                foreach (var kv in map.textureLibrary)
                    textures[kv.Key] = kv.Value.GetTexture();

            var matCache = new Dictionary<string, Material>();

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

                if(chunk.snowMaskData != null)
                {
                    var snowMask = chunk.snowMaskData.GetTexture();
                    terrain.materialTemplate.SetTexture("_SnowMask", snowMask);
                    if (terrain.materialTemplate.HasProperty("_UseMasks"))
                        terrain.materialTemplate.SetFloat("_UseMasks", 1f);
                }

                if (chunk.snowMask4Channel && terrain.materialTemplate.HasProperty("_SnowMask4Channel"))
                    terrain.materialTemplate.SetFloat("_SnowMask4Channel", 1f);

                terrain.Flush();

                var mapObjects = new GameObject("MapObjects").transform;
                mapObjects.parent = terrainGO.transform;
                foreach(var mapObject in chunk.objects)
                {
                    var prefab = objPrefabDict[mapObject.id];
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
                        if (obj.parts == null || obj.parts.Length == 0) continue;

                        var rootGO = new GameObject("CustomObject");
                        rootGO.transform.parent = customRoot;
                        rootGO.transform.position = obj.position;
                        rootGO.transform.rotation = Quaternion.Euler(obj.rotation);

                        var partRenderersByLodGroup = new Dictionary<int, List<(int lodIndex, Renderer renderer)>>();

                        foreach (var part in obj.parts)
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
                            {
                                var md = part.materials[mi];
                                string matKey = $"{md.baseTexRef}|{md.metallicTexRef}|{md.roughnessTexRef}|{md.normalTexRef}|{md.emissionTexRef}|{(int) md.renderMode}|{md.alphaCutoff}|{md.tiling}|{md.offset}|{md.cullMode}|{md.baseColor}|{md.emissionColor}|{md.lit}";
                                if (!matCache.TryGetValue(matKey, out Material mat))
                                {
                                    if (md.lit)
                                        mat = customObjectMaterial != null
                                            ? new Material(customObjectMaterial)
                                            : new Material(Shader.Find("Custom/CustomObjectLit"));
                                    else
                                        mat = customObjectUnlitMaterial != null
                                            ? new Material(customObjectUnlitMaterial)
                                            : new Material(Shader.Find("Custom/CustomObjectUnlit"));
                                    SetTex(mat, "_BaseMap",        md.baseTexRef,      textures);
                                    SetTex(mat, "_MetallicMap",    md.metallicTexRef,  textures);
                                    SetTex(mat, "_RoughnessMap",   md.roughnessTexRef, textures);
                                    SetTex(mat, "_NormalMap",      md.normalTexRef,    textures);
                                    SetTex(mat, "_EmissionMap",    md.emissionTexRef,  textures);
                                    if (mat.HasProperty("_BaseMap"))
                                    {
                                        mat.SetTextureScale("_BaseMap",  md.tiling);
                                        mat.SetTextureOffset("_BaseMap", md.offset);
                                    }
                                    if (mat.HasProperty("_Cull"))          mat.SetFloat("_Cull",          md.cullMode);
                                    if (mat.HasProperty("_BaseColor"))     mat.SetColor("_BaseColor",     md.baseColor);
                                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", md.emissionColor);
                                    ApplyRenderMode(mat, md.renderMode, md.alphaCutoff);
                                    mat.enableInstancing = true;
                                    matCache[matKey] = mat;
                                }
                                mats[mi] = mat;
                            }

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
                                    lods[i] = new LOD(lgd.transitions[i], rs);
                                }
                                lodGroup.SetLODs(lods);
                            }
                        }
                    }
                }
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
    }

}
