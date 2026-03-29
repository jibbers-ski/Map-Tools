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
                }

                terrain.Flush();

                var mapObjects = new GameObject("MapObjects").transform;
                mapObjects.parent = terrainGO.transform;
                foreach(var mapObject in chunk.objects)
                {
                    var prefab = objPrefabDict[mapObject.id];
                    var newObj = Instantiate(prefab, mapObjects);

                    newObj.transform.localScale = mapObject.scale;
                    newObj.transform.position = mapObject.position;
                    newObj.transform.rotation = Quaternion.Euler(mapObject.rotation);
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
    }

}
