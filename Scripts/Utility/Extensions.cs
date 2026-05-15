using System;
using UnityEngine;

namespace Jibbers.MapTools
{

    public static partial class Extensions
    {

        public static byte[] ExportHeightmapR16(this Terrain terrain)
        {
            var data = terrain.terrainData;

            int w = data.heightmapResolution;
            int h = data.heightmapResolution;

            float[,] heights = data.GetHeights(0, 0, w, h);

            byte[] bytes = new byte[w * h * 2];
            int index = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    ushort value = (ushort)Mathf.Clamp(
                        heights[y, x] * 65535f,
                        0,
                        65535
                    );

                    bytes[index++] = (byte)(value & 0xFF);
                    bytes[index++] = (byte)(value >> 8);
                }
            }

            return bytes;
        }

        public static void ImportHeightmapR16(this Terrain terrain, byte[] bytes, bool noBorder = false)
        {
            var data = terrain.terrainData;

            var unityRes = data.heightmapResolution;
            var rawRes = noBorder ? unityRes - 1 : unityRes;

            var heights = new float[unityRes, unityRes];
            var idx = 0;

            for (int y = 0; y < rawRes; y++) for (int x = 0; x < rawRes; x++)
            {
                if (idx + 1 >= bytes.Length)
                    break;

                ushort value = (ushort)(bytes[idx] | (bytes[idx + 1] << 8));
                heights[y, x] = value / 65535f;
                idx += 2;
            }

            if (noBorder)
            {
                for (int y = 0; y < rawRes; y++)
                    heights[y, rawRes] = heights[y, rawRes - 1];
                for (int x = 0; x < rawRes; x++)
                    heights[rawRes, x] = heights[rawRes - 1, x];
                heights[rawRes, rawRes] = heights[rawRes - 1, rawRes - 1];
            }

            data.SetHeights(0, 0, heights);
        }
        
        public static float EvaluateRepeated(this AnimationCurve curve, float t, int repeats = 1)
        {
            if (repeats <= 1) return curve.Evaluate(t);

            var sectionWidth = 1f / repeats;

            int section = repeats - 1 - (int) (t / sectionWidth);
            float sectionT = t % sectionWidth * repeats;

            var start = section * sectionWidth;
            var end = (section+1) * sectionWidth;

            return Mathf.Lerp(start, end, curve.Evaluate(sectionT));
        }

        public static float EvaluateRepeatedScaling(this AnimationCurve curve, float t, float scalePerRepeat, int repeats = 1)
        {
            var total = 0f;
            var current = 1f;
            var scales = new float[repeats];

            for(int i = 0; i < repeats; ++i)
            {
                total += current;
                scales[i] = current;
                current *= scalePerRepeat;
            }

            for(int i = 0; i < repeats; ++i)
                scales[i] /= total;

            current = 0;
            int section = repeats-1;

            for(int i = 0; i < repeats; ++i)
            {
                var next = current + scales[i];
                if(t < next)
                {
                    section = i;
                    break;
                }
                current = next;
            }

            var start = current;
            var end = current + scales[section];

            var sectionT = Mathf.InverseLerp(start, end, t);

            start = 1 - start;
            end = 1 - end;

            return Mathf.Lerp(end, start, curve.Evaluate(sectionT));
        }

    }

}