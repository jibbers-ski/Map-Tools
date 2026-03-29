using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jibbers.MapTools
{

    #if UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(MapExporter))]
    public class MapExporterEditor : Editor
    {

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var exporter = (MapExporter) target;

            if (GUILayout.Button("Auto Import Chunks"))
                exporter.AutoImport();

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
        [SerializeField] bool autoExportOnAwake;

        [Header("Misc")]
        [SerializeField] Vector3 camStartPosition;
        [SerializeField] bool allowBackgroundMountains;

        [Header("Terrain Chunks")]
        public List<MapTerrainChunk> chunks;

        void Awake() {
            if(autoExportOnAwake)
                Export();
            Destroy(gameObject);
        }

        public void Export()
        {
            var spawnPoints = GetComponentsInChildren<MapSpawnPoint>();
            if(string.IsNullOrEmpty(idOverride))
                idOverride = Utility.NewGuid;

            var map = new MapData() {
                name = mapName,
                id = idOverride,
                camStartPosition = camStartPosition,
                allowBackgroundMountains = allowBackgroundMountains,
                chunks = chunks.Select(c => new MapTerrainChunkData(c)).ToArray(),
                spawnPoints = spawnPoints.Select(s => new SpawnPointData(s)).ToArray(),
            };

            var serializer = new JsonSerializer();
            serializer.Begin(true);
            map.Serialize(serializer);
            serializer.Close();

            var dirPath = Utility.DataPath + "Maps/";
            Directory.CreateDirectory(dirPath);

            var filePath = dirPath + map.id + ".jbrmap";
            File.WriteAllText(filePath, (string) serializer.Data);
            Debug.Log("Saved to: " + filePath);
        }

        public void AutoImport()
        {
            chunks.Clear();
            foreach(var terrain in transform.GetComponentsInChildren<Terrain>())
            {
                var chunk = new MapTerrainChunk()
                {
                    terrain = terrain
                };

                var material = terrain.materialTemplate;
                chunk.snowMask = material.GetTexture("_SnowMask") as Texture2D;

                var child = terrain.transform.childCount > 0 ? terrain.transform.GetChild(0) : null;
                if(child && child.name.ToLower().Contains("objects"))
                    chunk.mapObjectContainer = child;

                chunks.Add(chunk);
            }
        }
    }

}
