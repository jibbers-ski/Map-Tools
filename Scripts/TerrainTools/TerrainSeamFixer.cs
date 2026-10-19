using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(TerrainSeamFixer))]
    public class TerrainSeamFixerEditor : Editor
    {
        static readonly GUIContent[] edgeLabels =
        {
            new GUIContent("+X (East)"),
            new GUIContent("-X (West)"),
            new GUIContent("+Z (North)"),
            new GUIContent("-Z (South)"),
        };

        bool fixArmed;

        public override void OnInspectorGUI()
        {
            var fixer = (TerrainSeamFixer) target;
            serializedObject.Update();

            EditorGUILayout.LabelField("Seam", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("otherTerrain"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("otherTerrainOffset"));

            EditorGUILayout.BeginHorizontal();
            var chunk = fixer.FindExporterChunk();
            using (new EditorGUI.DisabledScope(chunk == null || chunk.repeatOffset == Vector3.zero))
            {
                if (GUILayout.Button(new GUIContent("Use Exporter Repeat",
                    "Target this terrain against itself, offset by the repeat offset of its MapExporter chunk.")))
                {
                    Undo.RecordObject(fixer, "Configure Seam From Repeat");
                    fixer.ConfigureFromExporterRepeat(chunk);
                    EditorUtility.SetDirty(fixer);
                }
            }
            using (new EditorGUI.DisabledScope(fixer.otherTerrain == null))
            {
                if (GUILayout.Button(new GUIContent("Detect Edges",
                    "Pick the pair of edges with the smallest gap between their planes.")))
                {
                    Undo.RecordObject(fixer, "Detect Seam Edges");
                    if (!fixer.DetectEdges())
                        Debug.LogWarning("[TerrainSeamFixer] No overlapping edge pair found — check positions and offset.", fixer);
                    EditorUtility.SetDirty(fixer);
                }
            }
            EditorGUILayout.EndHorizontal();

            DrawEdgePopup("thisEdge", "This Edge", "Edge of this terrain that lies on the seam.");
            DrawEdgePopup("otherEdge", "Other Edge", "Edge of the other terrain that lies on the seam.");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Smoothing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothHeightmap"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothSnowMask"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothSnowMask2"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            var geo = fixer.ComputeGeometry();
            if (!geo.valid)
            {
                EditorGUILayout.HelpBox(geo.error, MessageType.Error);
                fixArmed = false;
            }
            else
            {
                var lines = new List<string>
                {
                    $"Overlap {geo.overlapEnd - geo.overlapStart:F1} m along {(geo.axisX ? "Z" : "X")}, edge gap {geo.gap:F2} m."
                };
                if (!string.IsNullOrEmpty(geo.warning))
                    lines.Add(geo.warning);
                var self = fixer.GetComponent<Terrain>();
                if (fixer.smoothHeightmap)
                    lines.Add($"Heightmap: {self.terrainData.heightmapResolution} ↔ {fixer.otherTerrain.terrainData.heightmapResolution}");
                AddMaskLine(lines, "Snow mask", "_SnowMask", fixer.smoothSnowMask, self, fixer.otherTerrain);
                AddMaskLine(lines, "Flow mask", "_SnowMask2", fixer.smoothSnowMask2, self, fixer.otherTerrain);
                EditorGUILayout.HelpBox(string.Join("\n", lines),
                    string.IsNullOrEmpty(geo.warning) ? MessageType.Info : MessageType.Warning);
            }

            if (!fixArmed)
            {
                using (new EditorGUI.DisabledScope(!geo.valid))
                    if (GUILayout.Button("Fix Seam"))
                        fixArmed = true;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"This will blend heightmap and mask data of both sides toward a shared seam profile over {fixer.smoothDistance:F0} m. " +
                    "Undo is supported; consider exporting the map first (.jbrmap is the terrain backup).",
                    MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm"))
                {
                    fixer.FixSeam();
                    fixArmed = false;
                }
                if (GUILayout.Button("Cancel"))
                    fixArmed = false;
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawEdgePopup(string propName, string label, string tooltip)
        {
            var prop = serializedObject.FindProperty(propName);
            prop.enumValueIndex = EditorGUILayout.Popup(new GUIContent(label, tooltip), prop.enumValueIndex, edgeLabels);
        }

        static void AddMaskLine(List<string> lines, string label, string prop, bool enabled, Terrain a, Terrain b)
        {
            if (!enabled) return;
            var ta = TerrainSeamFixer.GetMask(a, prop);
            var tb = TerrainSeamFixer.GetMask(b, prop);
            if (ta == null || tb == null)
                lines.Add($"{label}: missing on {(ta == null ? "this" : "the other")} terrain — will be skipped.");
            else if (!ta.isReadable || !tb.isReadable)
                lines.Add($"{label}: not readable — will be skipped.");
            else
                lines.Add($"{label}: {ta.width}×{ta.height} ↔ {tb.width}×{tb.height}");
        }
    }
#endif

    [RequireComponent(typeof(Terrain))]
    public class TerrainSeamFixer : MonoBehaviour
    {
        public enum TerrainEdge { XPlus, XMinus, ZPlus, ZMinus }

        [Tooltip("Terrain on the other side of the seam. Assign this same terrain to fix a repeating chunk against its own copies.")]
        public Terrain otherTerrain;
        [Tooltip("Virtual world offset applied to the other terrain before matching the edges. For a repeated chunk this is the exporter's repeat offset.")]
        public Vector3 otherTerrainOffset;
        [Tooltip("Edge of this terrain that lies on the seam.")]
        public TerrainEdge thisEdge = TerrainEdge.XPlus;
        [Tooltip("Edge of the other terrain that lies on the seam.")]
        public TerrainEdge otherEdge = TerrainEdge.XMinus;
        [Min(0.5f)]
        [Tooltip("Distance in meters over which each terrain blends toward the shared seam profile.")]
        public float smoothDistance = 5f;
        [Tooltip("Blend the heightmaps so heights and slopes match across the seam.")]
        public bool smoothHeightmap = true;
        [Tooltip("Blend the snow mask across the seam (snow coverage, stripe coverage, powder depth). Marking colors are never blended.")]
        public bool smoothSnowMask = true;
        [Tooltip("Blend the SnowMask2 flow direction across the seam (vector-space, wrap-safe).")]
        public bool smoothSnowMask2 = true;

        public struct SeamGeometry
        {
            public bool valid;
            public string error;
            public string warning;
            public bool axisX;
            public float overlapStart, overlapEnd, gap, seamMid;
        }

        struct Side
        {
            public Terrain terrain;
            public TerrainEdge edge;
            public Vector3 offset;
            public Vector3 basePos;
            public Vector3 size;

            public Side(Terrain t, TerrainEdge e, Vector3 virtualOffset)
            {
                terrain = t;
                edge = e;
                offset = virtualOffset;
                basePos = t.transform.position + virtualOffset;
                size = t.terrainData.size;
            }

            public bool AxisX => edge == TerrainEdge.XPlus || edge == TerrainEdge.XMinus;
            public int OutwardSign => edge == TerrainEdge.XPlus || edge == TerrainEdge.ZPlus ? 1 : -1;
            public float PerpSize => AxisX ? size.x : size.z;
            public float AlongSize => AxisX ? size.z : size.x;
            public float AlongStart => AxisX ? basePos.z : basePos.x;
            public float AlongEnd => AlongStart + AlongSize;

            public float EdgePlane
            {
                get
                {
                    switch (edge)
                    {
                        case TerrainEdge.XPlus:  return basePos.x + size.x;
                        case TerrainEdge.XMinus: return basePos.x;
                        case TerrainEdge.ZPlus:  return basePos.z + size.z;
                        default:                 return basePos.z;
                    }
                }
            }
        }

        class EdgeProfile
        {
            public float[] value, slope;
            public float start, spacing;

            public float Value(float t) => SampleArr(value, (t - start) / spacing);
            public float Slope(float t) => SampleArr(slope, (t - start) / spacing);
        }

        class MaskEdge
        {
            public float[] r, b, a;
            public Vector2[] flow;
            public float start, spacing;

            public float U(float t) => (t - start) / spacing;
        }

        static bool IsXEdge(TerrainEdge e) => e == TerrainEdge.XPlus || e == TerrainEdge.XMinus;

        public static Texture2D GetMask(Terrain t, string prop)
        {
            var mat = t != null ? t.materialTemplate : null;
            if (mat == null || !mat.HasProperty(prop)) return null;
            return mat.GetTexture(prop) as Texture2D;
        }

        public MapTerrainChunk FindExporterChunk()
        {
            var terrain = GetComponent<Terrain>();
            var exporter = GetComponentInParent<MapExporter>();
            if (terrain == null || exporter == null || exporter.chunks == null) return null;
            foreach (var c in exporter.chunks)
                if (c.terrain == terrain)
                    return c;
            return null;
        }

        public void ConfigureFromExporterRepeat(MapTerrainChunk chunk)
        {
            otherTerrain = GetComponent<Terrain>();
            otherTerrainOffset = chunk.repeatOffset;
            var o = chunk.repeatOffset;
            if (Mathf.Abs(o.x) >= Mathf.Abs(o.z))
            {
                thisEdge  = o.x >= 0 ? TerrainEdge.XPlus  : TerrainEdge.XMinus;
                otherEdge = o.x >= 0 ? TerrainEdge.XMinus : TerrainEdge.XPlus;
            }
            else
            {
                thisEdge  = o.z >= 0 ? TerrainEdge.ZPlus  : TerrainEdge.ZMinus;
                otherEdge = o.z >= 0 ? TerrainEdge.ZMinus : TerrainEdge.ZPlus;
            }
        }

        public bool DetectEdges()
        {
            var self = GetComponent<Terrain>();
            if (self == null || self.terrainData == null || otherTerrain == null || otherTerrain.terrainData == null)
                return false;
            float bestGap = float.MaxValue;
            bool found = false;
            foreach (TerrainEdge ea in System.Enum.GetValues(typeof(TerrainEdge)))
            foreach (TerrainEdge eb in System.Enum.GetValues(typeof(TerrainEdge)))
            {
                if (IsXEdge(ea) != IsXEdge(eb)) continue;
                if (self == otherTerrain && ea == eb) continue;
                var a = new Side(self, ea, Vector3.zero);
                var b = new Side(otherTerrain, eb, otherTerrainOffset);
                float overlap = Mathf.Min(a.AlongEnd, b.AlongEnd) - Mathf.Max(a.AlongStart, b.AlongStart);
                if (overlap <= 0.5f) continue;
                float gap = Mathf.Abs(a.EdgePlane - b.EdgePlane);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    thisEdge = ea;
                    otherEdge = eb;
                    found = true;
                }
            }
            return found;
        }

        public SeamGeometry ComputeGeometry()
        {
            var g = new SeamGeometry();
            var self = GetComponent<Terrain>();
            if (self == null || self.terrainData == null)
            {
                g.error = "No terrain data on this GameObject.";
                return g;
            }
            if (otherTerrain == null || otherTerrain.terrainData == null)
            {
                g.error = "Assign the other terrain. For a repeated chunk, assign this same terrain and set the repeat offset.";
                return g;
            }
            if (IsXEdge(thisEdge) != IsXEdge(otherEdge))
            {
                g.error = "Both edges must be on the same axis.";
                return g;
            }
            if (otherTerrain == self && otherTerrainOffset == Vector3.zero)
            {
                g.error = "Fixing a terrain against itself needs a non-zero offset (the repeat offset).";
                return g;
            }

            var a = new Side(self, thisEdge, Vector3.zero);
            var b = new Side(otherTerrain, otherEdge, otherTerrainOffset);
            g.axisX = a.AxisX;
            g.overlapStart = Mathf.Max(a.AlongStart, b.AlongStart);
            g.overlapEnd = Mathf.Min(a.AlongEnd, b.AlongEnd);
            if (g.overlapEnd - g.overlapStart <= 0.01f)
            {
                g.error = "The selected edges do not overlap along the seam.";
                return g;
            }
            g.gap = Mathf.Abs(a.EdgePlane - b.EdgePlane);
            g.seamMid = 0.5f * (a.EdgePlane + b.EdgePlane);
            if (g.gap > smoothDistance)
            {
                g.error = $"Edge planes are {g.gap:F1} m apart — wrong edges or offset?";
                return g;
            }
            if (g.gap > 0.05f)
                g.warning = $"Edge planes are {g.gap:F2} m apart; the seam profile will bridge the gap.";
            g.valid = true;
            return g;
        }

        float ClampDistance(Side s, bool sameData) =>
            Mathf.Min(smoothDistance, sameData ? s.PerpSize * 0.5f : s.PerpSize);

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float SampleArr(float[] arr, float u)
        {
            if (arr.Length == 1) return arr[0];
            u = Mathf.Clamp(u, 0f, arr.Length - 1f);
            int i = Mathf.Min((int) u, arr.Length - 2);
            return Mathf.Lerp(arr[i], arr[i + 1], u - i);
        }

        static Vector2 SampleVec(Vector2[] arr, float u)
        {
            if (arr.Length == 1) return arr[0];
            u = Mathf.Clamp(u, 0f, arr.Length - 1f);
            int i = Mathf.Min((int) u, arr.Length - 2);
            return Vector2.Lerp(arr[i], arr[i + 1], u - i);
        }

        static Vector2 FlowVec(float r)
        {
            float ang = r * 2f * Mathf.PI;
            return new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
        }

        static float FlowR(Vector2 v)
        {
            float ang = Mathf.Atan2(v.x, v.y);
            if (ang < 0f) ang += 2f * Mathf.PI;
            return ang / (2f * Mathf.PI);
        }

#if UNITY_EDITOR
        public void FixSeam()
        {
            var geo = ComputeGeometry();
            if (!geo.valid)
            {
                Debug.LogError($"[TerrainSeamFixer] {geo.error}", this);
                return;
            }

            var self = GetComponent<Terrain>();
            var a = new Side(self, thisEdge, Vector3.zero);
            var b = new Side(otherTerrain, otherEdge, otherTerrainOffset);
            int refSign = a.OutwardSign;
            int bSign = b.OutwardSign * refSign;
            bool sameData = self.terrainData == otherTerrain.terrainData;
            var parts = new List<string>();

            if (smoothHeightmap)
            {
                int resA = self.terrainData.heightmapResolution;
                int resB = otherTerrain.terrainData.heightmapResolution;
                var ha = self.terrainData.GetHeights(0, 0, resA, resA);
                var hb = sameData ? ha : otherTerrain.terrainData.GetHeights(0, 0, resB, resB);

                var pa = BuildHeightProfile(a, ha, smoothDistance);
                var pb = BuildHeightProfile(b, hb, smoothDistance);
                var seam = BuildSeamProfile(pa, pb, bSign, geo.overlapStart, geo.overlapEnd);

                int clamped = 0;
                clamped += BlendHeights(a, ha, ClampDistance(a, sameData), geo, seam, refSign);
                clamped += BlendHeights(b, hb, ClampDistance(b, sameData), geo, seam, refSign);

                UnityEditor.Undo.RegisterCompleteObjectUndo(self.terrainData, "Fix Terrain Seam");
                if (!sameData)
                    UnityEditor.Undo.RegisterCompleteObjectUndo(otherTerrain.terrainData, "Fix Terrain Seam");
                self.terrainData.SetHeights(0, 0, ha);
                if (!sameData)
                    otherTerrain.terrainData.SetHeights(0, 0, hb);

                parts.Add(clamped > 0 ? $"heightmap ({clamped} samples clamped)" : "heightmap");
                if (clamped > 0)
                    Debug.LogWarning($"[TerrainSeamFixer] {clamped} seam samples exceeded a terrain's height range and were clamped — add head room with TerrainHeightChanger.", this);
            }

            if (smoothSnowMask && BlendMaskPair(a, b, "_SnowMask", false, geo,
                    ClampDistance(a, sameData), ClampDistance(b, sameData)))
                parts.Add("snow mask");
            if (smoothSnowMask2 && BlendMaskPair(a, b, "_SnowMask2", true, geo,
                    ClampDistance(a, sameData), ClampDistance(b, sameData)))
                parts.Add("flow mask");

            Debug.Log(parts.Count > 0
                ? $"[TerrainSeamFixer] Seam blended over {smoothDistance:F0} m: {string.Join(", ", parts)}."
                : "[TerrainSeamFixer] Nothing was blended — enable a smoothing target or assign masks.", this);
        }

        static EdgeProfile BuildHeightProfile(Side s, float[,] h, float dist)
        {
            int res = s.terrain.terrainData.heightmapResolution;
            float perpTexel = s.PerpSize / (res - 1);
            int k = Mathf.Clamp(Mathf.RoundToInt(dist * 0.25f / perpTexel), 1, 8);
            var e = new EdgeProfile
            {
                value = new float[res],
                slope = new float[res],
                start = s.AlongStart,
                spacing = s.AlongSize / (res - 1),
            };
            for (int i = 0; i < res; i++)
            {
                float nEdge, nInner;
                switch (s.edge)
                {
                    case TerrainEdge.XPlus:  nEdge = h[i, res - 1]; nInner = h[i, res - 1 - k]; break;
                    case TerrainEdge.XMinus: nEdge = h[i, 0];       nInner = h[i, k];           break;
                    case TerrainEdge.ZPlus:  nEdge = h[res - 1, i]; nInner = h[res - 1 - k, i]; break;
                    default:                 nEdge = h[0, i];       nInner = h[k, i];           break;
                }
                e.value[i] = s.basePos.y + nEdge * s.size.y;
                e.slope[i] = (nEdge - nInner) * s.size.y / (k * perpTexel);
            }
            return e;
        }

        static EdgeProfile BuildSeamProfile(EdgeProfile pa, EdgeProfile pb, int bSign, float t0, float t1)
        {
            var coarse = pa.spacing >= pb.spacing ? pa : pb;
            int i0 = Mathf.FloorToInt((t0 - coarse.start) / coarse.spacing);
            int i1 = Mathf.CeilToInt((t1 - coarse.start) / coarse.spacing);
            int n = Mathf.Max(2, i1 - i0 + 1);
            var seam = new EdgeProfile
            {
                value = new float[n],
                slope = new float[n],
                start = coarse.start + i0 * coarse.spacing,
                spacing = coarse.spacing,
            };
            for (int j = 0; j < n; j++)
            {
                float t = seam.start + j * seam.spacing;
                seam.value[j] = 0.5f * (pa.Value(t) + pb.Value(t));
                seam.slope[j] = 0.5f * (pa.Slope(t) + bSign * pb.Slope(t));
            }
            return seam;
        }

        static int BlendHeights(Side s, float[,] h, float dist, SeamGeometry geo, EdgeProfile seam, int refSign)
        {
            int res = s.terrain.terrainData.heightmapResolution;
            float texX = s.size.x / (res - 1);
            float texZ = s.size.z / (res - 1);
            bool axisX = s.AxisX;
            float plane = s.EdgePlane;
            int outward = s.OutwardSign;
            float perpTexel = axisX ? texX : texZ;
            float alongEps = 0.5f * (axisX ? texZ : texX);
            int clamped = 0;
            for (int iy = 0; iy < res; iy++)
            {
                float z = s.basePos.z + iy * texZ;
                for (int ix = 0; ix < res; ix++)
                {
                    int perpIndex = axisX
                        ? (s.edge == TerrainEdge.XPlus ? res - 1 - ix : ix)
                        : (s.edge == TerrainEdge.ZPlus ? res - 1 - iy : iy);
                    float d = perpIndex * perpTexel;
                    if (d >= dist) continue;
                    float x = s.basePos.x + ix * texX;
                    float t = axisX ? z : x;
                    if (t < geo.overlapStart - alongEps || t > geo.overlapEnd + alongEps) continue;
                    float p = plane - d * outward;
                    float w = 1f - Smooth01(d / dist);
                    float target = seam.Value(t) + (p - geo.seamMid) * refSign * seam.Slope(t);
                    float n = (target - s.basePos.y) / s.size.y;
                    if (n < -1e-4f || n > 1f + 1e-4f) clamped++;
                    h[iy, ix] = Mathf.Lerp(h[iy, ix], Mathf.Clamp01(n), w);
                }
            }
            return clamped;
        }

        static bool BlendMaskPair(Side a, Side b, string prop, bool flow, SeamGeometry geo, float distA, float distB)
        {
            var texA = GetMask(a.terrain, prop);
            var texB = GetMask(b.terrain, prop);
            if (texA == null || texB == null) return false;
            if (texA.width < 2 || texA.height < 2 || texB.width < 2 || texB.height < 2) return false;
            if (!texA.isReadable || !texB.isReadable)
            {
                Debug.LogError($"[TerrainSeamFixer] {prop} must be readable to blend the seam.");
                return false;
            }

            bool sameTex = texA == texB;
            var pxA = texA.GetPixels();
            var pxB = sameTex ? pxA : texB.GetPixels();

            var pa = BuildMaskEdge(a, pxA, texA.width, texA.height, flow);
            var pb = BuildMaskEdge(b, pxB, texB.width, texB.height, flow);
            var seam = BuildMaskSeam(pa, pb, flow, geo.overlapStart, geo.overlapEnd);

            BlendMask(a, pxA, texA.width, texA.height, flow, seam, distA, geo);
            BlendMask(b, pxB, texB.width, texB.height, flow, seam, distB, geo);

            UnityEditor.Undo.RegisterCompleteObjectUndo(texA, "Fix Terrain Seam");
            if (!sameTex)
                UnityEditor.Undo.RegisterCompleteObjectUndo(texB, "Fix Terrain Seam");
            texA.SetPixels(pxA);
            texA.Apply(false);
            UnityEditor.EditorUtility.SetDirty(texA);
            if (!sameTex)
            {
                texB.SetPixels(pxB);
                texB.Apply(false);
                UnityEditor.EditorUtility.SetDirty(texB);
            }
            return true;
        }

        static MaskEdge BuildMaskEdge(Side s, Color[] px, int mw, int mh, bool flow)
        {
            bool axisX = s.AxisX;
            int n = axisX ? mh : mw;
            var e = new MaskEdge { start = s.AlongStart, spacing = s.AlongSize / (n - 1) };
            if (flow) e.flow = new Vector2[n];
            else { e.r = new float[n]; e.b = new float[n]; e.a = new float[n]; }
            for (int i = 0; i < n; i++)
            {
                int idx;
                if (axisX)
                {
                    int py = mh - 1 - i;
                    int pxE = s.edge == TerrainEdge.XPlus ? mw - 1 : 0;
                    idx = py * mw + pxE;
                }
                else
                {
                    int pyE = s.edge == TerrainEdge.ZPlus ? 0 : mh - 1;
                    idx = pyE * mw + i;
                }
                var c = px[idx];
                if (flow) e.flow[i] = FlowVec(c.r);
                else
                {
                    e.r[i] = c.r;
                    e.b[i] = c.b;
                    e.a[i] = c.a;
                }
            }
            return e;
        }

        static MaskEdge BuildMaskSeam(MaskEdge pa, MaskEdge pb, bool flow, float t0, float t1)
        {
            var coarse = pa.spacing >= pb.spacing ? pa : pb;
            int i0 = Mathf.FloorToInt((t0 - coarse.start) / coarse.spacing);
            int i1 = Mathf.CeilToInt((t1 - coarse.start) / coarse.spacing);
            int n = Mathf.Max(2, i1 - i0 + 1);
            var seam = new MaskEdge { start = coarse.start + i0 * coarse.spacing, spacing = coarse.spacing };
            if (flow) seam.flow = new Vector2[n];
            else { seam.r = new float[n]; seam.b = new float[n]; seam.a = new float[n]; }
            for (int j = 0; j < n; j++)
            {
                float t = seam.start + j * seam.spacing;
                if (flow)
                {
                    var v = SampleVec(pa.flow, pa.U(t)) + SampleVec(pb.flow, pb.U(t));
                    seam.flow[j] = v.sqrMagnitude > 1e-8f ? v.normalized : SampleVec(pa.flow, pa.U(t));
                }
                else
                {
                    seam.r[j] = 0.5f * (SampleArr(pa.r, pa.U(t)) + SampleArr(pb.r, pb.U(t)));
                    seam.b[j] = 0.5f * (SampleArr(pa.b, pa.U(t)) + SampleArr(pb.b, pb.U(t)));
                    seam.a[j] = 0.5f * (SampleArr(pa.a, pa.U(t)) + SampleArr(pb.a, pb.U(t)));
                }
            }
            return seam;
        }

        static void BlendMask(Side s, Color[] px, int mw, int mh, bool flow, MaskEdge seam, float dist, SeamGeometry geo)
        {
            bool axisX = s.AxisX;
            float texX = s.size.x / (mw - 1);
            float texZ = s.size.z / (mh - 1);
            float zEnd = s.basePos.z + s.size.z;
            float perpTexel = axisX ? texX : texZ;
            float alongEps = 0.5f * (axisX ? texZ : texX);
            for (int py = 0; py < mh; py++)
            {
                float z = zEnd - py * texZ;
                for (int ix = 0; ix < mw; ix++)
                {
                    int perpIndex = axisX
                        ? (s.edge == TerrainEdge.XPlus ? mw - 1 - ix : ix)
                        : (s.edge == TerrainEdge.ZPlus ? py : mh - 1 - py);
                    float d = perpIndex * perpTexel;
                    if (d >= dist) continue;
                    float x = s.basePos.x + ix * texX;
                    float t = axisX ? z : x;
                    if (t < geo.overlapStart - alongEps || t > geo.overlapEnd + alongEps) continue;
                    float w = 1f - Smooth01(d / dist);
                    float u = seam.U(t);
                    int idx = py * mw + ix;
                    var c = px[idx];
                    if (flow)
                    {
                        var v = FlowVec(c.r) * (1f - w) + SampleVec(seam.flow, u) * w;
                        if (v.sqrMagnitude > 1e-8f)
                            c.r = FlowR(v);
                    }
                    else
                    {
                        c.r = Mathf.Lerp(c.r, SampleArr(seam.r, u), w);
                        c.b = Mathf.Lerp(c.b, SampleArr(seam.b, u), w);
                        c.a = Mathf.Lerp(c.a, SampleArr(seam.a, u), w);
                    }
                    px[idx] = c;
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            var geo = ComputeGeometry();
            if (!geo.valid) return;
            var self = GetComponent<Terrain>();
            bool sameData = self.terrainData == otherTerrain.terrainData;
            var a = new Side(self, thisEdge, Vector3.zero);
            var b = new Side(otherTerrain, otherEdge, otherTerrainOffset);
            DrawZone(a, geo, ClampDistance(a, sameData), new Color(0f, 0.85f, 1f));
            DrawZone(b, geo, ClampDistance(b, sameData), new Color(1f, 0.6f, 0f));
        }

        static void DrawZone(Side s, SeamGeometry geo, float dist, Color color)
        {
            Gizmos.color = color;
            const int segments = 32;
            Vector3 prevEdge = default, prevInner = default;
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(geo.overlapStart, geo.overlapEnd, i / (float) segments);
                var edge = ZonePoint(s, t, 0.05f);
                var inner = ZonePoint(s, t, dist);
                if (i > 0)
                {
                    Gizmos.DrawLine(prevEdge, edge);
                    Gizmos.DrawLine(prevInner, inner);
                }
                if (i == 0 || i == segments)
                    Gizmos.DrawLine(edge, inner);
                prevEdge = edge;
                prevInner = inner;
            }
        }

        static Vector3 ZonePoint(Side s, float t, float d)
        {
            float p = s.EdgePlane - d * s.OutwardSign;
            float x = s.AxisX ? p : t;
            float z = s.AxisX ? t : p;
            var local = new Vector3(x, 0f, z) - s.offset;
            float y = s.terrain.transform.position.y + s.offset.y + s.terrain.SampleHeight(local) + 0.3f;
            return new Vector3(x, y, z);
        }
#endif
    }

}
