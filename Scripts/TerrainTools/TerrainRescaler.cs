using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(TerrainRescaler))]
    [CanEditMultipleObjects]
    public class TerrainRescalerEditor : Editor
    {
        bool armed;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("heightmapScale"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("snowMaskScale"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Rescales heightmap and/or snow mask without changing world position or extents.\n" +
                "Heightmap (N-1) is multiplied; result must stay 2^k+1 (works as long as the source is too).\n" +
                "Snow-mask R/B/A bilinear, G (discrete marking bucket) nearest-neighbour.\n" +
                "Multi-select supported: change settings on all selected rescalers, then Rescale.",
                MessageType.Info);

            string label = targets.Length > 1 ? $"Rescale {targets.Length} Terrains" : "Rescale Terrain";

            if (!armed)
            {
                if (GUILayout.Button(label))
                    armed = true;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"This will rescale {targets.Length} terrain{(targets.Length > 1 ? "s" : "")}. " +
                    "Snow-mask resize replaces the texture asset on disk and is not undoable.",
                    MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm"))
                {
                    foreach (var t in targets)
                        ((TerrainRescaler) t).Rescale();
                    armed = false;
                }
                if (GUILayout.Button("Cancel"))
                    armed = false;
                EditorGUILayout.EndHorizontal();
            }
        }
    }
#endif

    public enum TerrainRescaleFactor
    {
        [InspectorName("0.25x")] Quarter,
        [InspectorName("0.5x")]  Half,
        [InspectorName("1x")]    Same,
        [InspectorName("2x")]    Double,
        [InspectorName("4x")]    Quadruple,
    }

    [RequireComponent(typeof(Terrain))]
    public class TerrainRescaler : MonoBehaviour
    {
        public TerrainRescaleFactor heightmapScale = TerrainRescaleFactor.Same;
        public TerrainRescaleFactor snowMaskScale  = TerrainRescaleFactor.Same;

        static float Multiplier(TerrainRescaleFactor f)
        {
            switch (f)
            {
                case TerrainRescaleFactor.Quarter:   return 0.25f;
                case TerrainRescaleFactor.Half:      return 0.5f;
                case TerrainRescaleFactor.Double:    return 2f;
                case TerrainRescaleFactor.Quadruple: return 4f;
                default:                             return 1f;
            }
        }

        public void Rescale()
        {
#if UNITY_EDITOR
            var terrain = GetComponent<Terrain>();
            var data = terrain.terrainData;
            bool didSomething = false;

            float hMult = Multiplier(heightmapScale);
            if (hMult != 1f)
            {
                int srcRes = data.heightmapResolution;
                int dstRes = Mathf.RoundToInt((srcRes - 1) * hMult) + 1;
                int n = dstRes - 1;
                if (n <= 0 || (n & (n - 1)) != 0)
                {
                    Debug.LogError($"[TerrainRescaler] Source heightmapResolution {srcRes} can't scale to a valid 2^k+1 by {hMult}x (would be {dstRes}).");
                    return;
                }

                Undo.RegisterCompleteObjectUndo(data, "Rescale Heightmap");

                Vector3 originalSize = data.size;
                Vector3 originalPos  = terrain.transform.position;

                float[,] src = data.GetHeights(0, 0, srcRes, srcRes);
                var dst = new float[dstRes, dstRes];
                for (int y = 0; y < dstRes; y++)
                {
                    float sy = (float) y / (dstRes - 1) * (srcRes - 1);
                    int y0 = Mathf.FloorToInt(sy);
                    int y1 = Mathf.Min(y0 + 1, srcRes - 1);
                    float ty = sy - y0;
                    for (int x = 0; x < dstRes; x++)
                    {
                        float sx = (float) x / (dstRes - 1) * (srcRes - 1);
                        int x0 = Mathf.FloorToInt(sx);
                        int x1 = Mathf.Min(x0 + 1, srcRes - 1);
                        float tx = sx - x0;

                        float v0 = Mathf.Lerp(src[y0, x0], src[y0, x1], tx);
                        float v1 = Mathf.Lerp(src[y1, x0], src[y1, x1], tx);
                        dst[y, x] = Mathf.Lerp(v0, v1, ty);
                    }
                }

                data.heightmapResolution = dstRes;
                data.size = originalSize;
                terrain.transform.position = originalPos;
                data.SetHeights(0, 0, dst);
                EditorUtility.SetDirty(data);
                didSomething = true;
            }

            float mMult = Multiplier(snowMaskScale);
            if (mMult != 1f)
            {
                var mat = terrain.materialTemplate;
                if (mat == null || !mat.HasProperty("_SnowMask"))
                {
                    Debug.LogWarning("[TerrainRescaler] Terrain material has no _SnowMask property — skipped.");
                }
                else
                {
                    var oldMask = mat.GetTexture("_SnowMask") as Texture2D;
                    if (oldMask == null)
                    {
                        Debug.LogWarning("[TerrainRescaler] No snow-mask texture assigned — skipped.");
                    }
                    else
                    {
                        if (!oldMask.isReadable)
                        {
                            Debug.LogError($"[TerrainRescaler] Snow mask '{oldMask.name}' must be readable. Enable Read/Write in import settings.");
                            return;
                        }

                        int dstW = Mathf.Max(1, Mathf.RoundToInt(oldMask.width  * mMult));
                        int dstH = Mathf.Max(1, Mathf.RoundToInt(oldMask.height * mMult));
                        if (dstW == oldMask.width && dstH == oldMask.height)
                        {
                            Debug.LogWarning("[TerrainRescaler] Snow-mask scale rounded to same size — skipped.");
                        }
                        else
                        {
                            var srcPixels = oldMask.GetPixels();
                            int srcW = oldMask.width;
                            int srcH = oldMask.height;

                            var newPixels = new Color[dstW * dstH];
                            for (int y = 0; y < dstH; y++)
                            {
                                float sy = (float) y / (dstH - 1) * (srcH - 1);
                                int y0 = Mathf.FloorToInt(sy);
                                int y1 = Mathf.Min(y0 + 1, srcH - 1);
                                float ty = sy - y0;
                                int yN = Mathf.Clamp(Mathf.RoundToInt(sy), 0, srcH - 1);

                                for (int x = 0; x < dstW; x++)
                                {
                                    float sx = (float) x / (dstW - 1) * (srcW - 1);
                                    int x0 = Mathf.FloorToInt(sx);
                                    int x1 = Mathf.Min(x0 + 1, srcW - 1);
                                    float tx = sx - x0;
                                    int xN = Mathf.Clamp(Mathf.RoundToInt(sx), 0, srcW - 1);

                                    Color c00 = srcPixels[y0 * srcW + x0];
                                    Color c01 = srcPixels[y0 * srcW + x1];
                                    Color c10 = srcPixels[y1 * srcW + x0];
                                    Color c11 = srcPixels[y1 * srcW + x1];

                                    float r = Mathf.Lerp(Mathf.Lerp(c00.r, c01.r, tx), Mathf.Lerp(c10.r, c11.r, tx), ty);
                                    float b = Mathf.Lerp(Mathf.Lerp(c00.b, c01.b, tx), Mathf.Lerp(c10.b, c11.b, tx), ty);
                                    float a = Mathf.Lerp(Mathf.Lerp(c00.a, c01.a, tx), Mathf.Lerp(c10.a, c11.a, tx), ty);
                                    float g = srcPixels[yN * srcW + xN].g; // nearest

                                    newPixels[y * dstW + x] = new Color(r, g, b, a);
                                }
                            }

                            string oldPath = AssetDatabase.GetAssetPath(oldMask);
                            string oldName = oldMask.name;

                            var newMask = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
                            newMask.name = oldName;
                            newMask.SetPixels(newPixels);
                            newMask.Apply();

                            if (!string.IsNullOrEmpty(oldPath))
                            {
                                AssetDatabase.DeleteAsset(oldPath);
                                AssetDatabase.CreateAsset(newMask, oldPath);
                            }

                            Undo.RecordObject(mat, "Rescale Snow Mask");
                            mat.SetTexture("_SnowMask", newMask);
                            EditorUtility.SetDirty(mat);

                            var exporter = GetComponentInParent<MapExporter>();
                            if (exporter != null && exporter.chunks != null)
                            {
                                foreach (var chunk in exporter.chunks)
                                    if (chunk.terrain == terrain)
                                    {
                                        chunk.snowMask = newMask;
                                        EditorUtility.SetDirty(exporter);
                                        break;
                                    }
                            }

                            didSomething = true;
                        }
                    }
                }
            }

            if (didSomething)
            {
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(terrain);
                Debug.Log($"[TerrainRescaler] Rescaled '{terrain.name}'. Heightmap×{hMult}, SnowMask×{mMult}.");
            }
            else
            {
                Debug.Log("[TerrainRescaler] Nothing to do — both factors are 1x.");
            }
#endif
        }
    }

}
