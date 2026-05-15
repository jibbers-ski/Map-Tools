using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(TerrainSmoother))]
    [CanEditMultipleObjects]
    public class TerrainSmootherEditor : Editor
    {
        int  smoothRadius      = 3;
        bool smoothOnlySnow;
        bool smoothArmed;

        int  smoothPaintRadius  = 3;
        int  smoothPaintChannel;
        bool smoothPaintArmed;
        static readonly string[] smoothPaintChannelLabels = { "R (Snow)", "G (Marking Color)", "B (Marking Coverage)", "A (Alpha)" };

        public override void OnInspectorGUI()
        {
            DrawSmoothSection();
            DrawSmoothPaintSection();
        }

        void DrawSmoothSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Smooth Terrain", EditorStyles.boldLabel);
            smoothRadius   = EditorGUILayout.IntSlider("Radius",   smoothRadius, 1, 50);
            smoothOnlySnow = EditorGUILayout.Toggle  ("Only Snow", smoothOnlySnow);

            string buttonLabel = targets.Length > 1 ? $"Smooth {targets.Length} Terrains" : "Smooth Terrain";

            if (!smoothArmed)
            {
                if (GUILayout.Button(buttonLabel))
                    smoothArmed = true;
            }
            else
            {
                string scope = targets.Length > 1 ? $"{targets.Length} terrains" : "the heightmap";
                EditorGUILayout.HelpBox(
                    smoothOnlySnow
                        ? $"This will smooth {scope} (radius {smoothRadius}) only where snow is painted."
                        : $"This will smooth {scope} (radius {smoothRadius}).",
                    MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm"))
                {
                    if (targets.Length > 1)
                    {
                        var smoothers = new TerrainSmoother[targets.Length];
                        for (int i = 0; i < targets.Length; i++)
                            smoothers[i] = (TerrainSmoother) targets[i];
                        TerrainSmoother.SmoothMultiple(smoothers, smoothRadius, smoothOnlySnow);
                    }
                    else
                    {
                        ((TerrainSmoother) target).Smooth(smoothRadius, smoothOnlySnow);
                    }
                    smoothArmed = false;
                }
                if (GUILayout.Button("Cancel"))
                    smoothArmed = false;
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawSmoothPaintSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Smooth Paint", EditorStyles.boldLabel);
            smoothPaintRadius  = EditorGUILayout.IntSlider("Radius",  smoothPaintRadius, 1, 50);
            smoothPaintChannel = EditorGUILayout.Popup    ("Channel", smoothPaintChannel, smoothPaintChannelLabels);

            string buttonLabel = targets.Length > 1 ? $"Smooth Paint ({targets.Length})" : "Smooth Paint";

            if (!smoothPaintArmed)
            {
                if (GUILayout.Button(buttonLabel))
                    smoothPaintArmed = true;
            }
            else
            {
                string scope = targets.Length > 1 ? $"{targets.Length} snow masks" : "the snow mask";
                EditorGUILayout.HelpBox(
                    $"This will smooth the {smoothPaintChannelLabels[smoothPaintChannel]} channel of {scope} (radius {smoothPaintRadius}).",
                    MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm"))
                {
                    foreach (var t in targets)
                        ((TerrainSmoother) t).SmoothPaint(smoothPaintRadius, smoothPaintChannel);
                    smoothPaintArmed = false;
                }
                if (GUILayout.Button("Cancel"))
                    smoothPaintArmed = false;
                EditorGUILayout.EndHorizontal();
            }
        }
    }
#endif

    [RequireComponent(typeof(Terrain))]
    public class TerrainSmoother : MonoBehaviour
    {
        public void Smooth(int radius, bool onlySnow)
        {
            var terrain = GetComponent<Terrain>();
            var data = terrain.terrainData;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Smooth Terrain");
#endif
            int res = data.heightmapResolution;
            float[,] src = data.GetHeights(0, 0, res, res);

            Color[] maskPixels = null;
            int maskW = 0, maskH = 0;
            if (onlySnow)
            {
                var mat = terrain.materialTemplate;
                if (mat != null && mat.HasProperty("_SnowMask"))
                {
                    var maskTex = mat.GetTexture("_SnowMask") as Texture2D;
                    if (maskTex != null && maskTex.isReadable)
                    {
                        maskPixels = maskTex.GetPixels();
                        maskW = maskTex.width;
                        maskH = maskTex.height;
                    }
                    else if (maskTex != null)
                    {
                        Debug.LogWarning("[TerrainSmoother] Snow mask not readable — smoothing whole terrain.");
                    }
                }
            }

            int r = Mathf.Max(1, radius);
            float[,] tmp = new float[res, res];

            for (int y = 0; y < res; y++)
            {
                float sum = 0;
                int count = 0;
                for (int x = 0; x <= Mathf.Min(r, res - 1); x++) { sum += src[y, x]; count++; }
                for (int x = 0; x < res; x++)
                {
                    tmp[y, x] = sum / count;
                    int xNew = x + r + 1;
                    if (xNew < res) { sum += src[y, xNew]; count++; }
                    int xOld = x - r;
                    if (xOld >= 0)  { sum -= src[y, xOld]; count--; }
                }
            }

            float[,] dst = new float[res, res];
            for (int x = 0; x < res; x++)
            {
                float sum = 0;
                int count = 0;
                for (int y = 0; y <= Mathf.Min(r, res - 1); y++) { sum += tmp[y, x]; count++; }
                for (int y = 0; y < res; y++)
                {
                    dst[y, x] = sum / count;
                    int yNew = y + r + 1;
                    if (yNew < res) { sum += tmp[yNew, x]; count++; }
                    int yOld = y - r;
                    if (yOld >= 0)  { sum -= tmp[yOld, x]; count--; }
                }
            }

            if (maskPixels != null)
            {
                for (int y = 0; y < res; y++)
                {
                    float v = (float) y / (res - 1);
                    float my = (1f - v) * (maskH - 1);
                    int my0 = Mathf.FloorToInt(my);
                    int my1 = Mathf.Min(my0 + 1, maskH - 1);
                    float ty = my - my0;
                    for (int x = 0; x < res; x++)
                    {
                        float u = (float) x / (res - 1);
                        float mx = u * (maskW - 1);
                        int mx0 = Mathf.FloorToInt(mx);
                        int mx1 = Mathf.Min(mx0 + 1, maskW - 1);
                        float tx = mx - mx0;
                        float c00 = maskPixels[my0 * maskW + mx0].r;
                        float c01 = maskPixels[my0 * maskW + mx1].r;
                        float c10 = maskPixels[my1 * maskW + mx0].r;
                        float c11 = maskPixels[my1 * maskW + mx1].r;
                        float snow = Mathf.Lerp(Mathf.Lerp(c00, c01, tx), Mathf.Lerp(c10, c11, tx), ty);
                        dst[y, x] = Mathf.Lerp(src[y, x], dst[y, x], snow);
                    }
                }
            }

            data.SetHeights(0, 0, dst);
        }

        public static void SmoothMultiple(TerrainSmoother[] smoothers, int radius, bool onlySnow)
        {
#if UNITY_EDITOR
            int N = smoothers.Length;
            if (N == 0) return;

            var terrains = new Terrain[N];
            for (int i = 0; i < N; i++) terrains[i] = smoothers[i].GetComponent<Terrain>();

            foreach (var t in terrains)
                UnityEditor.Undo.RegisterCompleteObjectUndo(t.terrainData, "Smooth Terrain");

            var src  = new float[N][,];
            var tmp  = new float[N][,];
            var dst  = new float[N][,];
            var hRes = new int[N];
            var wRes = new int[N];
            for (int i = 0; i < N; i++)
            {
                int n = terrains[i].terrainData.heightmapResolution;
                hRes[i] = n; wRes[i] = n;
                src[i] = terrains[i].terrainData.GetHeights(0, 0, n, n);
                tmp[i] = new float[n, n];
                dst[i] = new float[n, n];
            }

            var leftN  = new int[N]; var rightN = new int[N];
            var downN  = new int[N]; var upN    = new int[N];
            for (int i = 0; i < N; i++) { leftN[i] = rightN[i] = downN[i] = upN[i] = -1; }
            const float epsilon = 0.5f;
            for (int i = 0; i < N; i++)
            {
                var posI  = terrains[i].transform.position;
                var sizeI = terrains[i].terrainData.size;
                for (int j = 0; j < N; j++)
                {
                    if (i == j) continue;
                    var posJ  = terrains[j].transform.position;
                    var sizeJ = terrains[j].terrainData.size;
                    float dx = posJ.x - posI.x;
                    float dz = posJ.z - posI.z;
                    if      (Mathf.Abs(dx - sizeI.x) < epsilon && Mathf.Abs(dz) < epsilon) rightN[i] = j;
                    else if (Mathf.Abs(dx + sizeJ.x) < epsilon && Mathf.Abs(dz) < epsilon) leftN[i]  = j;
                    else if (Mathf.Abs(dz - sizeI.z) < epsilon && Mathf.Abs(dx) < epsilon) upN[i]    = j;
                    else if (Mathf.Abs(dz + sizeJ.z) < epsilon && Mathf.Abs(dx) < epsilon) downN[i]  = j;
                }
            }

            int r = Mathf.Max(1, radius);

            for (int i = 0; i < N; i++)
            {
                int h = hRes[i], w = wRes[i];
                int extW = w + 2 * r;
                var ext = new float[extW];
                for (int y = 0; y < h; y++)
                {
                    for (int k = 0; k < extW; k++) ext[k] = float.NaN;
                    for (int x = 0; x < w; x++) ext[r + x] = src[i][y, x];
                    if (leftN[i] >= 0 && y < hRes[leftN[i]])
                    {
                        int li = leftN[i];
                        for (int k = 1; k <= r; k++)
                        {
                            int lx = wRes[li] - 1 - k;
                            if (lx >= 0) ext[r - k] = src[li][y, lx];
                        }
                    }
                    if (rightN[i] >= 0 && y < hRes[rightN[i]])
                    {
                        int ri = rightN[i];
                        for (int k = 1; k <= r; k++)
                        {
                            int rx = k;
                            if (rx < wRes[ri]) ext[r + w + k - 1] = src[ri][y, rx];
                        }
                    }

                    float sum = 0;
                    int count = 0;
                    for (int k = 0; k <= 2 * r; k++)
                        if (!float.IsNaN(ext[k])) { sum += ext[k]; count++; }
                    for (int x = 0; x < w; x++)
                    {
                        tmp[i][y, x] = count > 0 ? sum / count : src[i][y, x];
                        if (!float.IsNaN(ext[x])) { sum -= ext[x]; count--; }
                        int newK = x + 2 * r + 1;
                        if (newK < extW && !float.IsNaN(ext[newK])) { sum += ext[newK]; count++; }
                    }
                }
            }

            for (int i = 0; i < N; i++)
            {
                int h = hRes[i], w = wRes[i];
                int extH = h + 2 * r;
                var ext = new float[extH];
                for (int x = 0; x < w; x++)
                {
                    for (int k = 0; k < extH; k++) ext[k] = float.NaN;
                    for (int y = 0; y < h; y++) ext[r + y] = tmp[i][y, x];
                    if (downN[i] >= 0 && x < wRes[downN[i]])
                    {
                        int di = downN[i];
                        for (int k = 1; k <= r; k++)
                        {
                            int dy = hRes[di] - 1 - k;
                            if (dy >= 0) ext[r - k] = tmp[di][dy, x];
                        }
                    }
                    if (upN[i] >= 0 && x < wRes[upN[i]])
                    {
                        int ui = upN[i];
                        for (int k = 1; k <= r; k++)
                        {
                            int uy = k;
                            if (uy < hRes[ui]) ext[r + h + k - 1] = tmp[ui][uy, x];
                        }
                    }

                    float sum = 0;
                    int count = 0;
                    for (int k = 0; k <= 2 * r; k++)
                        if (!float.IsNaN(ext[k])) { sum += ext[k]; count++; }
                    for (int y = 0; y < h; y++)
                    {
                        dst[i][y, x] = count > 0 ? sum / count : tmp[i][y, x];
                        if (!float.IsNaN(ext[y])) { sum -= ext[y]; count--; }
                        int newK = y + 2 * r + 1;
                        if (newK < extH && !float.IsNaN(ext[newK])) { sum += ext[newK]; count++; }
                    }
                }
            }

            if (onlySnow)
            {
                for (int i = 0; i < N; i++)
                {
                    int h = hRes[i], w = wRes[i];
                    Color[] maskPixels = null;
                    int maskW = 0, maskH = 0;
                    var mat = terrains[i].materialTemplate;
                    if (mat != null && mat.HasProperty("_SnowMask"))
                    {
                        var maskTex = mat.GetTexture("_SnowMask") as Texture2D;
                        if (maskTex != null && maskTex.isReadable)
                        {
                            maskPixels = maskTex.GetPixels();
                            maskW = maskTex.width;
                            maskH = maskTex.height;
                        }
                    }
                    if (maskPixels == null) continue;

                    for (int y = 0; y < h; y++)
                    {
                        float v = (float) y / (h - 1);
                        float my = (1f - v) * (maskH - 1);
                        int my0 = Mathf.FloorToInt(my);
                        int my1 = Mathf.Min(my0 + 1, maskH - 1);
                        float ty = my - my0;
                        for (int x = 0; x < w; x++)
                        {
                            float u = (float) x / (w - 1);
                            float mx = u * (maskW - 1);
                            int mx0 = Mathf.FloorToInt(mx);
                            int mx1 = Mathf.Min(mx0 + 1, maskW - 1);
                            float tx = mx - mx0;
                            float c00 = maskPixels[my0 * maskW + mx0].r;
                            float c01 = maskPixels[my0 * maskW + mx1].r;
                            float c10 = maskPixels[my1 * maskW + mx0].r;
                            float c11 = maskPixels[my1 * maskW + mx1].r;
                            float snow = Mathf.Lerp(Mathf.Lerp(c00, c01, tx), Mathf.Lerp(c10, c11, tx), ty);
                            dst[i][y, x] = Mathf.Lerp(src[i][y, x], dst[i][y, x], snow);
                        }
                    }
                }
            }

            for (int i = 0; i < N; i++)
                terrains[i].terrainData.SetHeights(0, 0, dst[i]);
#endif
        }

        public void SmoothPaint(int radius, int channel)
        {
#if UNITY_EDITOR
            var terrain = GetComponent<Terrain>();
            var mat = terrain != null ? terrain.materialTemplate : null;
            if (mat == null || !mat.HasProperty("_SnowMask"))
            {
                Debug.LogWarning($"[TerrainSmoother] '{(terrain != null ? terrain.name : "?")}' has no _SnowMask property — skipped.");
                return;
            }
            var tex = mat.GetTexture("_SnowMask") as Texture2D;
            if (tex == null)
            {
                Debug.LogWarning($"[TerrainSmoother] '{terrain.name}' has no snow mask assigned — skipped.");
                return;
            }
            if (!tex.isReadable)
            {
                Debug.LogError($"[TerrainSmoother] Snow mask '{tex.name}' must be readable. Enable Read/Write in import settings.");
                return;
            }

            UnityEditor.Undo.RegisterCompleteObjectUndo(tex, "Smooth Paint");

            int w = tex.width;
            int h = tex.height;
            var pixels = tex.GetPixels();
            int r = Mathf.Max(1, radius);

            if (channel == 1)
            {
                SmoothPaintGBuckets(pixels, w, h, r);
            }
            else
            {
                float[] src = new float[w * h];
                for (int i = 0; i < pixels.Length; i++)
                {
                    switch (channel)
                    {
                        case 0: src[i] = pixels[i].r; break;
                        case 2: src[i] = pixels[i].b; break;
                        default: src[i] = pixels[i].a; break;
                    }
                }

                var tmp = new float[w * h];
                BoxBlurFlat(src, tmp, w, h, r);

                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    switch (channel)
                    {
                        case 0: c.r = src[i]; break;
                        case 2: c.b = src[i]; break;
                        default: c.a = src[i]; break;
                    }
                    pixels[i] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            UnityEditor.EditorUtility.SetDirty(tex);
#endif
        }

        static void SmoothPaintGBuckets(Color[] pixels, int w, int h, int radius)
        {
            float[] buckets = { 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f, 0.45f, 0.50f, 0.55f, 0.60f, 0.65f, 0.70f, 0.75f, 0.80f };
            int n = buckets.Length;

            // Tolerance is half the spacing (0.05) so each pixel falls into exactly one bucket.
            var masks = new float[n][];
            for (int b = 0; b < n; b++)
            {
                masks[b] = new float[w * h];
                for (int i = 0; i < pixels.Length; i++)
                    masks[b][i] = Mathf.Abs(pixels[i].g - buckets[b]) < 0.025f ? 1f : 0f;
            }

            var tmp = new float[w * h];
            for (int b = 0; b < n; b++)
                BoxBlurFlat(masks[b], tmp, w, h, radius);

            for (int i = 0; i < pixels.Length; i++)
            {
                int winner = -1;
                float winnerVal = 0.5f;
                for (int b = 0; b < n; b++)
                {
                    if (masks[b][i] > winnerVal)
                    {
                        winnerVal = masks[b][i];
                        winner = b;
                    }
                }
                var c = pixels[i];
                c.g = winner >= 0 ? buckets[winner] : 0f;
                pixels[i] = c;
            }
        }

        static void BoxBlurFlat(float[] arr, float[] tmp, int w, int h, int r)
        {
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0;
                int count = 0;
                for (int x = 0; x <= Mathf.Min(r, w - 1); x++) { sum += arr[row + x]; count++; }
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x] = sum / count;
                    int xNew = x + r + 1;
                    if (xNew < w) { sum += arr[row + xNew]; count++; }
                    int xOld = x - r;
                    if (xOld >= 0)  { sum -= arr[row + xOld]; count--; }
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0;
                int count = 0;
                for (int y = 0; y <= Mathf.Min(r, h - 1); y++) { sum += tmp[y * w + x]; count++; }
                for (int y = 0; y < h; y++)
                {
                    arr[y * w + x] = sum / count;
                    int yNew = y + r + 1;
                    if (yNew < h) { sum += tmp[yNew * w + x]; count++; }
                    int yOld = y - r;
                    if (yOld >= 0)  { sum -= tmp[yOld * w + x]; count--; }
                }
            }
        }
    }

}
