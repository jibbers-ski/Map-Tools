using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(TerrainSplitter))]
    public class TerrainSplitterEditor : Editor
    {
        bool armed;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var t = (TerrainSplitter) target;

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Splits this terrain into splits×splits smaller terrains. World layout is preserved exactly.\n" +
                "(heightmapResolution-1) and snow-mask dimensions must be divisible by 'splits'.\n" +
                "MapObjects and CustomMapObjects are reparented based on world position.",
                MessageType.Info);

            if (!armed)
            {
                if (GUILayout.Button("Split Terrain"))
                    armed = true;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"This will split '{t.gameObject.name}' into {t.splits}×{t.splits} = {t.splits * t.splits} tiles. " +
                    "Source terrain stays in the scene (disabled). Not directly undoable.",
                    MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm")) { t.Split(); armed = false; }
                if (GUILayout.Button("Cancel"))  armed = false;
                EditorGUILayout.EndHorizontal();
            }
        }
    }
#endif

    [RequireComponent(typeof(Terrain))]
    public class TerrainSplitter : MonoBehaviour
    {
        [Min(1)] public int splits = 2;

        [Tooltip("Disable the source terrain after splitting (kept around for reference / undo).")]
        public bool disableSourceAfterSplit = true;

        public void Split()
        {
#if UNITY_EDITOR
            var srcTerrain = GetComponent<Terrain>();
            var srcData = srcTerrain.terrainData;
            var srcMat = srcTerrain.materialTemplate;

            int srcHRes = srcData.heightmapResolution;

            if (splits < 1) { Debug.LogError("[TerrainSplitter] splits must be >= 1"); return; }
            if ((srcHRes - 1) % splits != 0)
            {
                Debug.LogError($"[TerrainSplitter] (heightmapResolution-1)={srcHRes-1} must be divisible by splits={splits}");
                return;
            }

            Texture2D srcSnowMask = null;
            if (srcMat != null && srcMat.HasProperty("_SnowMask"))
                srcSnowMask = srcMat.GetTexture("_SnowMask") as Texture2D;

            if (srcSnowMask != null)
            {
                if (!srcSnowMask.isReadable)
                {
                    Debug.LogError($"[TerrainSplitter] Snow mask '{srcSnowMask.name}' must be readable. Enable Read/Write in import settings.");
                    return;
                }
                if (srcSnowMask.width % splits != 0 || srcSnowMask.height % splits != 0)
                {
                    Debug.LogError($"[TerrainSplitter] Snow mask {srcSnowMask.width}×{srcSnowMask.height} must be divisible by splits={splits}");
                    return;
                }
            }

            Vector3 srcPos = srcTerrain.transform.position;
            Vector3 srcSize = srcData.size;
            int tileHRes = ((srcHRes - 1) / splits) + 1;
            Vector3 tileSize = new Vector3(srcSize.x / splits, srcSize.y, srcSize.z / splits);

            float[,] srcHeights = srcData.GetHeights(0, 0, srcHRes, srcHRes);

            Color[] srcMaskPixels = null;
            int srcMaskW = 0, srcMaskH = 0, tileMaskW = 0, tileMaskH = 0;
            if (srcSnowMask != null)
            {
                srcMaskPixels = srcSnowMask.GetPixels();
                srcMaskW = srcSnowMask.width;
                srcMaskH = srcSnowMask.height;
                tileMaskW = srcMaskW / splits;
                tileMaskH = srcMaskH / splits;
            }

            var exporter = GetComponentInParent<MapExporter>();
            MapTerrainChunk srcChunk = null;
            int srcChunkIdx = -1;
            if (exporter != null && exporter.chunks != null)
            {
                for (int k = 0; k < exporter.chunks.Count; k++)
                    if (exporter.chunks[k].terrain == srcTerrain)
                    {
                        srcChunk = exporter.chunks[k];
                        srcChunkIdx = k;
                        break;
                    }
            }

            var allMapObjects    = srcTerrain.GetComponentsInChildren<MapObject>(true);
            var allCustomObjects = srcTerrain.GetComponentsInChildren<CustomMapObject>(true);

            Transform parent = transform.parent;
            var newChunks = new List<MapTerrainChunk>(splits * splits);
            var newTerrains = new Terrain[splits * splits];

            for (int i = 0; i < splits; i++)
            {
                for (int j = 0; j < splits; j++)
                {
                    var newData = new TerrainData
                    {
                        heightmapResolution = tileHRes,
                        size                = tileSize,
                    };
                    newData.name = srcTerrain.name + $"_{i}_{j}_TerrainData";

                    int xOff = i * (tileHRes - 1);
                    int yOff = j * (tileHRes - 1);
                    var tileHeights = new float[tileHRes, tileHRes];
                    for (int y = 0; y < tileHRes; y++)
                        for (int x = 0; x < tileHRes; x++)
                            tileHeights[y, x] = srcHeights[yOff + y, xOff + x];
                    newData.SetHeights(0, 0, tileHeights);

                    var go = Terrain.CreateTerrainGameObject(newData);
                    go.name = srcTerrain.name + $"_{i}_{j}";
                    go.transform.parent = parent;
                    go.transform.position = srcPos + new Vector3(i * tileSize.x, 0, j * tileSize.z);

                    var newTerrain = go.GetComponent<Terrain>();
                    newTerrains[i * splits + j] = newTerrain;

                    Texture2D newMask = null;
                    if (srcMat != null)
                    {
                        var newMat = new Material(srcMat);
                        newMat.name = srcMat.name.Replace(" (Instance)", "") + $" {i}_{j} (Instance)";
                        newTerrain.materialTemplate = newMat;

                        if (srcMaskPixels != null)
                        {
                            newMask = new Texture2D(tileMaskW, tileMaskH, TextureFormat.RGBA32, false);
                            newMask.name = srcSnowMask.name + $"_{i}_{j}";
                            var tilePixels = new Color[tileMaskW * tileMaskH];
                            int srcJ = splits - 1 - j;
                            for (int y = 0; y < tileMaskH; y++)
                                for (int x = 0; x < tileMaskW; x++)
                                {
                                    int sx = i * tileMaskW + x;
                                    int sy = srcJ * tileMaskH + y;
                                    tilePixels[y * tileMaskW + x] = srcMaskPixels[sy * srcMaskW + sx];
                                }
                            newMask.SetPixels(tilePixels);
                            newMask.Apply();

                            newMat.SetTexture("_SnowMask", newMask);
                        }
                    }

                    var moContainer = new GameObject("MapObjects").transform;
                    moContainer.SetParent(go.transform, false);

                    newChunks.Add(new MapTerrainChunk
                    {
                        terrain            = newTerrain,
                        mapObjectContainer = moContainer,
                        snowMask           = newMask,
                        repeats            = srcChunk != null ? srcChunk.repeats      : 1,
                        repeatOffset       = srcChunk != null ? srcChunk.repeatOffset : Vector3.zero,
                    });
                }
            }

            foreach (var mo in allMapObjects)
            {
                var dst = newChunks[TileIndex(mo.transform.position, srcPos, tileSize)].mapObjectContainer;
                mo.transform.SetParent(dst, true);
            }
            foreach (var co in allCustomObjects)
            {
                var dst = newChunks[TileIndex(co.transform.position, srcPos, tileSize)].mapObjectContainer;
                co.transform.SetParent(dst, true);
            }

            if (exporter != null && srcChunkIdx >= 0)
            {
                exporter.chunks.RemoveAt(srcChunkIdx);
                for (int k = 0; k < newChunks.Count; k++)
                    exporter.chunks.Insert(srcChunkIdx + k, newChunks[k]);
                EditorUtility.SetDirty(exporter);
            }

            if (disableSourceAfterSplit)
                srcTerrain.gameObject.SetActive(false);

            Debug.Log($"[TerrainSplitter] Split '{srcTerrain.name}' into {splits}×{splits} = {splits * splits} tiles. " +
                $"Reparented {allMapObjects.Length} MapObject(s) and {allCustomObjects.Length} CustomMapObject(s). " +
                "New TerrainData and SnowMask are scene-embedded; use BetterTerrainEditor's 'Save as Asset' buttons to extract them into asset files.");
#endif
        }

        int TileIndex(Vector3 worldPos, Vector3 srcPos, Vector3 tileSize)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - srcPos.x) / tileSize.x), 0, splits - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt((worldPos.z - srcPos.z) / tileSize.z), 0, splits - 1);
            return i * splits + j;
        }
    }

}
