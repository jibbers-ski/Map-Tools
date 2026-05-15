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

                        foreach (var part in obj.parts)
                        {
                            if (string.IsNullOrEmpty(part.meshRef) || !meshes.ContainsKey(part.meshRef)) continue;

                            var partGO = new GameObject(part.meshRef);
                            partGO.transform.parent = rootGO.transform;
                            partGO.transform.position = part.localPosition;
                            partGO.transform.rotation = Quaternion.Euler(part.localRotation);
                            partGO.transform.localScale = part.localScale;

                            partGO.AddComponent<MeshFilter>().sharedMesh = meshes[part.meshRef];

                            string matKey = $"{part.baseTexRef}|{part.metallicTexRef}|{part.roughnessTexRef}|{part.normalTexRef}";
                            if (!matCache.TryGetValue(matKey, out Material mat))
                            {
                                mat = customObjectMaterial != null
                                    ? new Material(customObjectMaterial)
                                    : new Material(Shader.Find("Custom/CustomObjectLit"));
                                SetTex(mat, "_BaseMap",        part.baseTexRef,      textures);
                                SetTex(mat, "_MetallicMap",    part.metallicTexRef,  textures);
                                SetTex(mat, "_RoughnessMap",   part.roughnessTexRef, textures);
                                SetTex(mat, "_NormalMap",      part.normalTexRef,    textures);
                                matCache[matKey] = mat;
                            }

                            partGO.AddComponent<MeshRenderer>().sharedMaterial = mat;
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

        static void SetTex(Material mat, string prop, string texRef, Dictionary<string, Texture2D> lib)
        {
            if (!string.IsNullOrEmpty(texRef) && lib.TryGetValue(texRef, out var tex))
                mat.SetTexture(prop, tex);
        }
    }

}
