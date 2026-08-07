using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Jibbers.MapTools
{

    public class ColliderPivotFixer : MonoBehaviour
    {
        public enum AlongAxis { Y, Z }
        public enum SegmentOrder { ByName, NearestChain }

        [Tooltip("Local axis of each collider that should point along the rail toward the next segment. ArchBuilder segments use Y.")]
        public AlongAxis alongAxis = AlongAxis.Y;
        [Tooltip("ByName = chain segments in name order (Blender's numbered names). NearestChain = start at one end and always walk to the nearest remaining center.")]
        public SegmentOrder segmentOrder = SegmentOrder.ByName;
        [Tooltip("Extra rotation applied to every generated pivot, in degrees.")]
        public Vector3 rotationOffset = Vector3.zero;

#if UNITY_EDITOR
        class Segment
        {
            public Transform transform;
            public List<MeshCollider> colliders = new List<MeshCollider>();
            public Matrix4x4 oldMatrix;
            public Vector3 center;
            public Quaternion rotation;
        }

        public void GeneratePivots()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[ColliderPivotFixer] Cannot run in Prefab Mode. Open the object in a scene instead - the baked meshes are saved into the scene file.");
                return;
            }

            var byTransform = new Dictionary<Transform, Segment>();
            foreach (var mc in GetComponentsInChildren<MeshCollider>(true))
            {
                if (mc.sharedMesh == null) continue;
                if (!mc.sharedMesh.isReadable)
                {
                    Debug.LogError($"[ColliderPivotFixer] Collider mesh '{mc.sharedMesh.name}' on '{mc.gameObject.name}' is not readable. Enable Read/Write in its import settings.");
                    continue;
                }
                var filter = mc.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh != mc.sharedMesh && !filter.sharedMesh.isReadable)
                {
                    Debug.LogError($"[ColliderPivotFixer] Render mesh '{filter.sharedMesh.name}' on '{mc.gameObject.name}' is not readable. Enable Read/Write in its import settings.");
                    continue;
                }
                if (!byTransform.TryGetValue(mc.transform, out var seg))
                {
                    seg = new Segment { transform = mc.transform, oldMatrix = mc.transform.localToWorldMatrix };
                    byTransform.Add(mc.transform, seg);
                }
                seg.colliders.Add(mc);
            }

            var segments = new List<Segment>(byTransform.Values);
            if (segments.Count == 0)
            {
                Debug.LogWarning($"[ColliderPivotFixer] No usable MeshColliders found under '{name}'.");
                return;
            }

            foreach (var seg in segments)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (var mc in seg.colliders)
                {
                    foreach (var v in mc.sharedMesh.vertices)
                    {
                        sum += seg.oldMatrix.MultiplyPoint3x4(v);
                        count++;
                    }
                }
                seg.center = sum / count;
            }

            OrderSegments(segments);

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments.Count == 1)
                {
                    segments[i].rotation = segments[i].transform.rotation;
                    continue;
                }
                Vector3 prev = segments[Mathf.Max(i - 1, 0)].center;
                Vector3 next = segments[Mathf.Min(i + 1, segments.Count - 1)].center;
                segments[i].rotation = BuildRotation(next - prev) * Quaternion.Euler(rotationOffset);
            }

            int changed = 0;
            foreach (var seg in segments)
                if (Rebake(seg)) changed++;

            Debug.Log($"[ColliderPivotFixer] Generated pivots for {changed} of {segments.Count} collider segment(s) under '{name}'.");
        }

        void OrderSegments(List<Segment> segments)
        {
            if (segmentOrder == SegmentOrder.ByName)
            {
                segments.Sort((a, b) => EditorUtility.NaturalCompare(a.transform.name, b.transform.name));
                return;
            }

            Vector3 mean = Vector3.zero;
            foreach (var s in segments) mean += s.center;
            mean /= segments.Count;

            int start = 0;
            float far = -1f;
            for (int i = 0; i < segments.Count; i++)
            {
                float d = (segments[i].center - mean).sqrMagnitude;
                if (d > far) { far = d; start = i; }
            }

            var remaining = new List<Segment>(segments);
            var ordered = new List<Segment> { remaining[start] };
            remaining.RemoveAt(start);
            while (remaining.Count > 0)
            {
                Vector3 cur = ordered[ordered.Count - 1].center;
                int nearest = 0;
                float best = float.MaxValue;
                for (int i = 0; i < remaining.Count; i++)
                {
                    float d = (remaining[i].center - cur).sqrMagnitude;
                    if (d < best) { best = d; nearest = i; }
                }
                ordered.Add(remaining[nearest]);
                remaining.RemoveAt(nearest);
            }

            segments.Clear();
            segments.AddRange(ordered);
        }

        Quaternion BuildRotation(Vector3 dir)
        {
            Vector3 t = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, t);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.forward, t);
            up.Normalize();
            return alongAxis == AlongAxis.Y ? Quaternion.LookRotation(up, t) : Quaternion.LookRotation(t, up);
        }

        bool Rebake(Segment seg)
        {
            Matrix4x4 target = Matrix4x4.TRS(seg.center, seg.rotation, Vector3.one);
            Matrix4x4 delta = target.inverse * seg.oldMatrix;
            if (IsIdentity(delta)) return false;

            Transform tr = seg.transform;
            Undo.RecordObject(tr, "Generate Pivots");

            var childPoses = new List<(Transform child, Vector3 pos, Quaternion rot)>();
            foreach (Transform child in tr)
                childPoses.Add((child, child.position, child.rotation));

            tr.SetPositionAndRotation(seg.center, seg.rotation);
            Matrix4x4 parent = tr.parent != null ? tr.parent.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 local = parent.inverse * target;
            tr.localScale = new Vector3(
                ((Vector3)local.GetColumn(0)).magnitude,
                ((Vector3)local.GetColumn(1)).magnitude,
                ((Vector3)local.GetColumn(2)).magnitude);

            foreach (var (child, pos, rot) in childPoses)
            {
                Undo.RecordObject(child, "Generate Pivots");
                child.SetPositionAndRotation(pos, rot);
            }

            var baked = new Dictionary<Mesh, Mesh>();
            foreach (var mc in seg.colliders)
            {
                Undo.RecordObject(mc, "Generate Pivots");
                mc.sharedMesh = BakeMesh(mc.sharedMesh, delta, baked);
            }

            var filter = tr.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
            {
                Undo.RecordObject(filter, "Generate Pivots");
                filter.sharedMesh = BakeMesh(filter.sharedMesh, delta, baked);
            }

            return true;
        }

        static bool IsIdentity(Matrix4x4 m)
        {
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(m[i] - Matrix4x4.identity[i]) > 1e-5f) return false;
            return true;
        }

        static Mesh BakeMesh(Mesh src, Matrix4x4 delta, Dictionary<Mesh, Mesh> cache)
        {
            if (cache.TryGetValue(src, out var existing)) return existing;

            var mesh = Instantiate(src);
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
                verts[i] = delta.MultiplyPoint3x4(verts[i]);
            mesh.vertices = verts;

            Matrix4x4 nrmMat = delta.inverse.transpose;
            var normals = mesh.normals;
            if (normals != null && normals.Length > 0)
            {
                for (int i = 0; i < normals.Length; i++)
                    normals[i] = nrmMat.MultiplyVector(normals[i]).normalized;
                mesh.normals = normals;
            }

            var tangents = mesh.tangents;
            if (tangents != null && tangents.Length > 0)
            {
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector3 t = delta.MultiplyVector(tangents[i]);
                    t.Normalize();
                    tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                }
                mesh.tangents = tangents;
            }

            mesh.RecalculateBounds();
            mesh.name = src.name + "_pivot_" + HashMatrix(delta);
            Undo.RegisterCreatedObjectUndo(mesh, "Generate Pivots");
            cache.Add(src, mesh);
            return mesh;
        }

        static string HashMatrix(Matrix4x4 m)
        {
            uint h = 2166136261u;
            for (int i = 0; i < 16; i++)
            {
                h ^= (uint)Mathf.RoundToInt(m[i] * 1000f);
                h *= 16777619u;
            }
            return h.ToString("x8");
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ColliderPivotFixer))]
    public class ColliderPivotFixerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var fixer = (ColliderPivotFixer)target;
            bool prefabMode = PrefabStageUtility.GetCurrentPrefabStage() != null;
            if (prefabMode)
                EditorGUILayout.HelpBox("Open this object in a scene to generate pivots. The baked meshes are stored in the scene file and cannot be saved inside a prefab asset.", MessageType.Error);
            else if (PrefabUtility.IsPartOfPrefabInstance(fixer.gameObject))
                EditorGUILayout.HelpBox("This is a prefab instance. The fixed pivots and baked meshes live in this scene only - do not apply these overrides back to the prefab asset.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(prefabMode))
                if (GUILayout.Button("Generate Pivots"))
                    fixer.GeneratePivots();
        }
    }
#endif

}
