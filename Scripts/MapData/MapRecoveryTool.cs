#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class MapRecoveryTool : EditorWindow
    {

        string filePath;
        MapData map;
        Vector2 scroll;

        [MenuItem("Jibbers/Map Recovery")]
        static void Open() => GetWindow<MapRecoveryTool>("Map Recovery");

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(filePath) ? "No file selected" : filePath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var picked = EditorUtility.OpenFilePanel("Select .jbrmap", Utility.DataPath + "Maps", "jbrmap");
                if (!string.IsNullOrEmpty(picked))
                {
                    filePath = picked;
                    map = null;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(filePath) && GUILayout.Button("Load & List Terrains"))
                Load();

            if (map == null)
                return;

            EditorGUILayout.LabelField($"{map.name}  (version {map.version})", EditorStyles.boldLabel);

            if (map.chunks == null || map.chunks.Length == 0)
            {
                EditorGUILayout.HelpBox("No terrain chunks in this map.", MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < map.chunks.Length; i++)
            {
                var chunk = map.chunks[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Chunk {i}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"position {chunk.position}   size {chunk.size}");
                EditorGUILayout.LabelField($"heightmap {chunk.heightmapResolution}   data {chunk.terrainData?.Length ?? 0:N0} bytes   trees {chunk.treePrototypes?.Length ?? 0}");
                if (GUILayout.Button("Restore into TerrainData asset..."))
                    Restore(chunk);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        void Load()
        {
            var data = File.ReadAllText(filePath);

            var serializer = new JsonSerializer();
            serializer.Begin(false, data);

            map = new MapData();
            map.Serialize(serializer);

            serializer.Close();
        }

        void Restore(MapTerrainChunkData chunk)
        {
            var target = EditorUtility.SaveFilePanelInProject("Write chunk into TerrainData asset", "", "asset",
                "Pick the TerrainData asset to overwrite. Its GUID and references stay intact.");
            if (string.IsNullOrEmpty(target))
                return;

            if (!EditorUtility.DisplayDialog("Overwrite asset?",
                $"Write this chunk's heightmap into:\n{target}\n\nThe file's current content will be replaced.", "Overwrite", "Cancel"))
                return;

            var terrainData = new TerrainData() {
                heightmapResolution = chunk.heightmapResolution,
                size = chunk.size,
            };
            ApplyHeightmapR16(terrainData, chunk.terrainData);
            var (protos, instances, skipped) = ApplyTrees(terrainData, chunk.treePrototypes);

            AssetDatabase.CreateAsset(terrainData, target);
            AssetDatabase.SaveAssets();

            var summary = $"{target}\nheightmap {terrainData.heightmapResolution}, size {terrainData.size}\ntrees: {protos} prototype(s), {instances} instance(s)";
            if (skipped > 0)
                summary += $"\nskipped {skipped} mesh-based prototype(s) — only MapObject placeholder trees can be stored in a standalone asset";
            EditorUtility.DisplayDialog("Restored", summary, "OK");
        }

        static (int protos, int instances, int skipped) ApplyTrees(TerrainData data, TreePrototypeData[] treePrototypes)
        {
            if (treePrototypes == null || treePrototypes.Length == 0)
                return (0, 0, 0);

            var objPrefabs = Resources.LoadAll<Transform>("MapObjects/Placeholders/");
            var objPrefabDict = new Dictionary<string, Transform>();
            foreach (var prefab in objPrefabs)
                objPrefabDict[prefab.name.Replace("_placeholder", "")] = prefab;

            var protoList = new List<TreePrototype>();
            var instList = new List<TreeInstance>();
            var floatBuf = new float[6];
            var skipped = 0;

            foreach (var protoData in treePrototypes)
            {
                if (string.IsNullOrEmpty(protoData.objectId) || !objPrefabDict.TryGetValue(protoData.objectId, out var objPrefab))
                {
                    skipped++;
                    continue;
                }

                int protoIndex = protoList.Count;
                protoList.Add(new TreePrototype { prefab = objPrefab.gameObject });

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

            data.treePrototypes = protoList.ToArray();
            data.treeInstances = instList.ToArray();
            return (protoList.Count, instList.Count, skipped);
        }

        static void ApplyHeightmapR16(TerrainData data, byte[] bytes)
        {
            var res = data.heightmapResolution;
            var heights = new float[res, res];
            var idx = 0;

            for (int y = 0; y < res; y++) for (int x = 0; x < res; x++)
            {
                if (idx + 1 >= bytes.Length)
                    break;

                ushort value = (ushort)(bytes[idx] | (bytes[idx + 1] << 8));
                heights[y, x] = value / 65535f;
                idx += 2;
            }

            data.SetHeights(0, 0, heights);
        }

    }

}
#endif
