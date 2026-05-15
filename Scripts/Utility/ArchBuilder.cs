using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Jibbers.MapTools
{

    [ExecuteAlways]
    public class ArchBuilder : MonoBehaviour
    {
        public enum SegmentShape { Cylinder, Box }

        [Header("Arch Shape")]
        [Min(0.01f)] public float archWidth = 4f;
        [Min(0.01f)] public float archHeight = 2f;
        [Range(0.05f, 1f)] public float archCoverage = 1f;
        [Tooltip("Curve exponent. 1 = ellipse, <1 = flatter/rounder top, >1 = pointier.")]
        [Min(0.01f)] public float archMultiplier = 1f;
        public bool invert = false;

        [Header("Segments")]
        public SegmentShape shape = SegmentShape.Cylinder;
        [Min(1)] public int segmentCount = 12;
        [Min(0.001f)] public float cylinderRadius = 0.1f;
        [Tooltip("Box cross-section half-extents (X, Z). Y = segment length along the arc.")]
        public Vector2 boxHalfExtents = new Vector2(0.1f, 0.1f);
        [Tooltip("0 = auto-fit length to arc segment.")]
        [Min(0f)] public float segmentLengthOverride = 0f;
        [Tooltip("Extra length added to each segment so they overlap their neighbors.")]
        public float overlap = 0f;
        [Tooltip("Per-segment rotation offset in degrees. X = pitch, Y = roll, Z = yaw.")]
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Twist")]
        [Tooltip("Total perpendicular offset from one end of the arch to the other, in world units. Negative flips direction.")]
        public float twist = 0f;

        public bool addMesh = false;
        public Material meshMaterial;

        // Cached per-rebuild scaling (unit-curve -> world dimensions)
        float _scaleX = 1f, _scaleY = 1f, _baselineY = 0f;
        float _t0 = 0f, _t1 = Mathf.PI;

        static Mesh _capsuleMesh, _cubeMesh;

        void OnEnable()   { ScheduleRebuild(); }
        void OnValidate() { ScheduleRebuild(); }

        void ScheduleRebuild()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= Rebuild;
            EditorApplication.delayCall += Rebuild;
#else
            Rebuild();
#endif
        }

        void Rebuild()
        {
            if (this == null || !enabled) return;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            float t0 = Mathf.PI * 0.5f * (1f - archCoverage);
            float t1 = Mathf.PI - t0;
            _t0 = t0;
            _t1 = t1;
            float sign = invert ? -1f : 1f;

            Vector3 endU  = SampleUnit(t0);
            Vector3 peakU = SampleUnit(Mathf.PI * 0.5f);
            float naturalHalfWidth = -endU.x;
            float naturalHeight    = peakU.y - endU.y;
            _scaleX    = naturalHalfWidth > 1e-5f ? (archWidth * 0.5f) / naturalHalfWidth : 1f;
            _scaleY    = naturalHeight    > 1e-5f ?  archHeight        / naturalHeight    : 1f;
            _baselineY = endU.y;

            for (int i = 0; i < segmentCount; i++)
            {
                float a = (i + 0.5f) / segmentCount;
                float t = Mathf.Lerp(t0, t1, a);

                Vector3 pos     = SamplePoint(t, sign);
                Vector3 tangent = SampleTangent(t, sign);

                float length;
                if (segmentLengthOverride > 0f)
                {
                    length = segmentLengthOverride;
                }
                else
                {
                    Vector3 p0 = SamplePoint(Mathf.Lerp(t0, t1, i / (float) segmentCount), sign);
                    Vector3 p1 = SamplePoint(Mathf.Lerp(t0, t1, (i + 1) / (float) segmentCount), sign);
                    length = Vector3.Distance(p0, p1);
                }
                length += overlap;

                var go = new GameObject($"ArchSegment_{i:00}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = pos;
                Vector3 normal = Vector3.forward - Vector3.Dot(Vector3.forward, tangent) * tangent;
                normal = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.right;
                go.transform.localRotation = Quaternion.LookRotation(normal, tangent)
                                           * Quaternion.Euler(rotationOffset);

                Vector3 scl;
                if (shape == SegmentShape.Cylinder)
                {
                    var col = go.AddComponent<CapsuleCollider>();
                    col.radius    = 0.5f;
                    col.height    = 2f;
                    col.direction = 1;
                    scl = new Vector3(cylinderRadius * 2f, length * 0.5f, cylinderRadius * 2f);
                }
                else
                {
                    var col = go.AddComponent<BoxCollider>();
                    col.size = Vector3.one;
                    scl = new Vector3(boxHalfExtents.x * 2f, length, boxHalfExtents.y * 2f);
                }
                go.transform.localScale = scl;

#if UNITY_EDITOR
                if (addMesh)
                {
                    var mesh = GetPrimitiveMesh(shape == SegmentShape.Cylinder ? PrimitiveType.Capsule : PrimitiveType.Cube);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = meshMaterial != null
                        ? meshMaterial
                        : AssetDatabase.LoadAssetAtPath<Material>(
                            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat"
                        );
                }
#endif
            }
        }

        static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            if (type == PrimitiveType.Capsule)
            {
                if (_capsuleMesh != null) return _capsuleMesh;
                var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _capsuleMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
                return _capsuleMesh;
            }
            else
            {
                if (_cubeMesh != null) return _cubeMesh;
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
                return _cubeMesh;
            }
        }

        Vector3 SampleUnit(float t)
        {
            float s = Mathf.Max(0f, Mathf.Sin(t));
            float yShape = Mathf.Pow(s, archMultiplier);
            return new Vector3(-Mathf.Cos(t) * 0.5f, yShape, 0f);
        }

        Vector3 SamplePoint(float t, float sign)
        {
            Vector3 u = SampleUnit(t);
            float a = _t1 > _t0 ? (t - _t0) / (_t1 - _t0) : 0.5f;
            return new Vector3(u.x * _scaleX,
                               (u.y - _baselineY) * _scaleY * sign,
                               (a - 0.5f) * twist);
        }

        Vector3 SampleTangent(float t, float sign)
        {
            const float h = 0.001f;
            return (SamplePoint(t + h, sign) - SamplePoint(t - h, sign)).normalized;
        }
    }

}
