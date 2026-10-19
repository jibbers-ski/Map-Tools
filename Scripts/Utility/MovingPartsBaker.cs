using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Jibbers.MapTools
{

    public class MovingPartsBaker : MonoBehaviour
    {
        public const float DrivePeriod = 1000f;
        public const float BandMagic = 1000000f;
        public const float MaxSwingAngle = 180f;
        public const float MaxBobDistance = 2.5f;
        public const string ShaderName = "Custom/CustomObjectLit";

        [HideInInspector] public Mesh bakedMesh;
        [HideInInspector] public Mesh originalMesh;
        [HideInInspector] public Material[] originalMaterials;
        [HideInInspector] public bool hadRenderer;
        [HideInInspector] public List<GameObject> disabledSources = new();

        public bool IsBaked => bakedMesh;

#if UNITY_EDITOR
        public void Bake()
        {
            if (IsBaked) Unbake();

            var sources = new List<(MeshFilter filter, Material[] materials)>();
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh || !filter.TryGetComponent(out MeshRenderer renderer)) continue;
                if (!filter.sharedMesh.isReadable)
                {
                    Debug.LogError($"[MovingParts] Mesh '{filter.sharedMesh.name}' on '{filter.name}' is not readable. Enable Read/Write in its import settings.", filter);
                    return;
                }
                sources.Add((filter, renderer.sharedMaterials));
            }
            if (sources.Count == 0)
            {
                Debug.LogWarning($"[MovingParts] No meshes found under '{name}'.", this);
                return;
            }

            var markers = new List<MovingPart>(GetComponentsInChildren<MovingPart>()).FindAll(m => !m.exclude);
            var regionMarkers = markers.FindAll(m => !m.HasMeshes);

            var vertices  = new List<Vector3>();
            var normals   = new List<Vector3>();
            var tangents  = new List<Vector4>();
            var uv        = new List<Vector2>();
            var partIndex = new List<int>();
            var materials = new List<Material>();
            var triangles = new List<List<int>>();
            bool missingNormals = false, missingTangents = false;
            var worldToRoot = transform.worldToLocalMatrix;

            foreach (var (filter, mats) in sources)
            {
                var mesh = filter.sharedMesh;
                var delta = worldToRoot * filter.transform.localToWorldMatrix;
                var normalMatrix = delta.inverse.transpose;
                bool flip = delta.determinant < 0f;
                int offset = vertices.Count;
                int ownPart = markers.IndexOf(FindPart(filter.transform));

                var srcVertices = mesh.vertices;
                var srcNormals  = mesh.normals;
                var srcTangents = mesh.tangents;
                var srcUv       = mesh.uv;
                missingNormals  |= srcNormals.Length != srcVertices.Length;
                missingTangents |= srcTangents.Length != srcVertices.Length;
                for (int i = 0; i < srcVertices.Length; i++)
                {
                    vertices.Add(delta.MultiplyPoint3x4(srcVertices[i]));
                    normals.Add(i < srcNormals.Length ? normalMatrix.MultiplyVector(srcNormals[i]).normalized : Vector3.up);
                    if (i < srcTangents.Length)
                    {
                        var t = delta.MultiplyVector(srcTangents[i]).normalized;
                        tangents.Add(new Vector4(t.x, t.y, t.z, flip ? -srcTangents[i].w : srcTangents[i].w));
                    }
                    else tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                    uv.Add(i < srcUv.Length ? srcUv[i] : Vector2.zero);
                    partIndex.Add(-1);
                }

                if (ownPart >= 0 || regionMarkers.Count > 0)
                    Capture(regionMarkers, markers, mesh, vertices, partIndex, offset, ownPart);

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var material = s < mats.Length ? mats[s] : null;
                    int index = materials.IndexOf(material);
                    if (index < 0)
                    {
                        index = materials.Count;
                        materials.Add(material);
                        triangles.Add(new List<int>());
                    }
                    var tris = mesh.GetTriangles(s);
                    var list = triangles[index];
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        list.Add(tris[t] + offset);
                        list.Add(tris[t + (flip ? 2 : 1)] + offset);
                        list.Add(tris[t + (flip ? 1 : 2)] + offset);
                    }
                }
            }

            var pivots = new Vector3[markers.Count];
            var yawPitches = new Vector2[markers.Count];
            var speeds = new float[markers.Count];
            var motionColors = new Color32[markers.Count];
            var report = ComputeParts(markers, vertices, partIndex, pivots, yawPitches, speeds, motionColors);

            var uv2 = new List<Vector2>(vertices.Count);
            var uv3 = new List<Vector2>(vertices.Count);
            var uv4 = new List<Vector2>(vertices.Count);
            var colors = new List<Color32>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                int p = partIndex[i];
                if (p < 0 || speeds[p] == 0f)
                {
                    uv2.Add(Vector2.zero);
                    uv3.Add(Vector2.zero);
                    uv4.Add(Vector2.zero);
                    colors.Add(new Color32(0, 0, 0, 255));
                    continue;
                }
                uv2.Add(new Vector2(pivots[p].x, pivots[p].y));
                uv3.Add(new Vector2(pivots[p].z, speeds[p]));
                uv4.Add(yawPitches[p]);
                colors.Add(motionColors[p]);
            }

            var baked = new Mesh { indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            baked.SetVertices(vertices);
            baked.SetNormals(normals);
            baked.SetTangents(tangents);
            baked.SetUVs(0, uv);
            baked.SetUVs(1, uv2);
            baked.SetUVs(2, uv3);
            baked.SetUVs(3, uv4);
            baked.SetColors(colors);
            baked.subMeshCount = materials.Count;
            for (int i = 0; i < materials.Count; i++)
                baked.SetTriangles(triangles[i], i);
            if (missingNormals) baked.RecalculateNormals();
            if (missingTangents) baked.RecalculateTangents();
            baked.RecalculateBounds();
            baked.name = name + "_mov_" + Hash(vertices, uv2, uv3, uv4, colors);

            var stage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (stage != null)
                AssetDatabase.CreateAsset(baked, System.IO.Path.ChangeExtension(stage.assetPath, null) + "_" + baked.name + ".asset");
            else
                Undo.RegisterCreatedObjectUndo(baked, "Bake Moving Parts");

            Undo.RecordObject(this, "Bake Moving Parts");
            foreach (var (filter, mats) in sources)
            {
                if (filter.gameObject == gameObject)
                {
                    hadRenderer = true;
                    originalMesh = filter.sharedMesh;
                    originalMaterials = mats;
                    continue;
                }
                if (filter.GetComponentInChildren<Collider>())
                    Debug.LogWarning($"[MovingParts] '{filter.name}' carries a collider that is disabled by the bake - keep colliders on objects that are not baked.", filter);
                Undo.RecordObject(filter.gameObject, "Bake Moving Parts");
                filter.gameObject.SetActive(false);
                disabledSources.Add(filter.gameObject);
            }

            if (!TryGetComponent(out MeshFilter rootFilter)) rootFilter = Undo.AddComponent<MeshFilter>(gameObject);
            if (!TryGetComponent(out MeshRenderer rootRenderer)) rootRenderer = Undo.AddComponent<MeshRenderer>(gameObject);
            Undo.RecordObject(rootFilter, "Bake Moving Parts");
            Undo.RecordObject(rootRenderer, "Bake Moving Parts");
            rootFilter.sharedMesh = baked;
            rootRenderer.sharedMaterials = materials.ToArray();
            bakedMesh = baked;

            foreach (var material in materials)
            {
                if (!material) continue;
                if (material.shader.name != ShaderName)
                {
                    Debug.LogWarning($"[MovingParts] Material '{material.name}' does not use {ShaderName}; its parts will not move.", material);
                    continue;
                }
                if (material.IsKeywordEnabled("_MOVING_PARTS")) continue;
                Undo.RecordObject(material, "Bake Moving Parts");
                material.SetFloat("_UseMovingParts", 1f);
                material.EnableKeyword("_MOVING_PARTS");
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"[MovingParts] Baked {sources.Count} mesh(es) into '{baked.name}' ({vertices.Count} vertices, {materials.Count} material(s)).{report}", this);
        }

        public void Unbake()
        {
            Undo.RecordObject(this, "Unbake Moving Parts");
            foreach (var source in disabledSources)
            {
                if (!source) continue;
                Undo.RecordObject(source, "Unbake Moving Parts");
                source.SetActive(true);
            }
            disabledSources.Clear();

            foreach (var marker in GetComponentsInChildren<MovingPart>(true))
            {
                Undo.RecordObject(marker, "Unbake Moving Parts");
                marker.baked = false;
            }

            var rootFilter = GetComponent<MeshFilter>();
            var rootRenderer = GetComponent<MeshRenderer>();
            if (hadRenderer)
            {
                if (rootFilter)
                {
                    Undo.RecordObject(rootFilter, "Unbake Moving Parts");
                    rootFilter.sharedMesh = originalMesh;
                }
                if (rootRenderer)
                {
                    Undo.RecordObject(rootRenderer, "Unbake Moving Parts");
                    rootRenderer.sharedMaterials = originalMaterials;
                }
            }
            else
            {
                if (rootRenderer) Undo.DestroyObjectImmediate(rootRenderer);
                if (rootFilter) Undo.DestroyObjectImmediate(rootFilter);
            }

            if (bakedMesh && !EditorUtility.IsPersistent(bakedMesh))
                Undo.DestroyObjectImmediate(bakedMesh);
            bakedMesh = null;
            originalMesh = null;
            originalMaterials = null;
            hadRenderer = false;
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        MovingPart FindPart(Transform t)
        {
            for (; t; t = t.parent)
            {
                if (t.TryGetComponent(out MovingPart part) && !part.exclude) return part;
                if (t == transform) break;
            }
            return null;
        }

        void Capture(List<MovingPart> regionMarkers, List<MovingPart> markers, Mesh mesh, List<Vector3> vertices, List<int> partIndex, int offset, int ownPart)
        {
            int count = mesh.vertexCount;
            var world = new Vector3[count];
            for (int i = 0; i < count; i++)
                world[i] = transform.TransformPoint(vertices[offset + i]);

            int[] islands = null;
            int islandCount = 0;
            Vector3[] islandCenters = null;
            void EnsureIslands()
            {
                if (islands != null) return;
                islands = ComputeIslands(mesh, out islandCount);
                var min = new Vector3[islandCount];
                var max = new Vector3[islandCount];
                for (int k = 0; k < islandCount; k++)
                {
                    min[k] = Vector3.positiveInfinity;
                    max[k] = Vector3.negativeInfinity;
                }
                for (int i = 0; i < count; i++)
                {
                    min[islands[i]] = Vector3.Min(min[islands[i]], world[i]);
                    max[islands[i]] = Vector3.Max(max[islands[i]], world[i]);
                }
                islandCenters = new Vector3[islandCount];
                for (int k = 0; k < islandCount; k++)
                    islandCenters[k] = (min[k] + max[k]) * 0.5f;
            }

            var captured = new bool[count];
            void Apply(MovingPart marker, int p, bool whole)
            {
                bool byIsland = marker.capture == MovingPart.CaptureMode.Islands;
                if (!whole && byIsland) EnsureIslands();
                for (int i = 0; i < count; i++)
                    captured[i] = whole || marker.Contains(byIsland ? islandCenters[islands[i]] : world[i]);

                foreach (var exclusion in marker.GetComponentsInChildren<MovingPart>())
                {
                    if (!exclusion.exclude) continue;
                    if (exclusion.capture == MovingPart.CaptureMode.Islands)
                    {
                        EnsureIslands();
                        var hit = new bool[islandCount];
                        for (int i = 0; i < count; i++)
                            if (exclusion.Contains(world[i])) hit[islands[i]] = true;
                        for (int i = 0; i < count; i++)
                            if (hit[islands[i]]) captured[i] = false;
                    }
                    else
                        for (int i = 0; i < count; i++)
                            if (exclusion.Contains(world[i])) captured[i] = false;
                }

                for (int i = 0; i < count; i++)
                    if (captured[i] && partIndex[offset + i] < 0)
                        partIndex[offset + i] = p;
            }

            if (ownPart >= 0)
            {
                Apply(markers[ownPart], ownPart, true);
                return;
            }
            foreach (var marker in regionMarkers)
                Apply(marker, markers.IndexOf(marker), false);
        }

        static int[] ComputeIslands(Mesh mesh, out int islandCount)
        {
            var verts = mesh.vertices;
            var parent = new int[verts.Length];
            var byPosition = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < verts.Length; i++)
            {
                var key = Vector3Int.RoundToInt(verts[i] * 10000f);
                if (byPosition.TryGetValue(key, out int first)) parent[i] = first;
                else
                {
                    byPosition[key] = i;
                    parent[i] = i;
                }
            }

            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }

            var tris = mesh.triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = Find(tris[t]);
                parent[Find(tris[t + 1])] = a;
                parent[Find(tris[t + 2])] = Find(a);
            }

            var ids = new Dictionary<int, int>();
            var islands = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                int root = Find(i);
                if (!ids.TryGetValue(root, out int id))
                    ids[root] = id = ids.Count;
                islands[i] = id;
            }
            islandCount = ids.Count;
            return islands;
        }

        string ComputeParts(List<MovingPart> markers, List<Vector3> vertices, List<int> partIndex, Vector3[] pivots, Vector2[] yawPitches, float[] speeds, Color32[] motionColors)
        {
            int n = markers.Count;
            var min = new Vector3[n];
            var max = new Vector3[n];
            var counts = new int[n];
            var worldPivots = new Vector3[n];
            var radii = new float[n];
            for (int p = 0; p < n; p++)
            {
                min[p] = Vector3.positiveInfinity;
                max[p] = Vector3.negativeInfinity;
            }
            for (int i = 0; i < vertices.Count; i++)
            {
                int p = partIndex[i];
                if (p < 0) continue;
                var w = transform.TransformPoint(vertices[i]);
                min[p] = Vector3.Min(min[p], w);
                max[p] = Vector3.Max(max[p], w);
                counts[p]++;
            }
            for (int p = 0; p < n; p++)
            {
                var center = (min[p] + max[p]) * 0.5f;
                worldPivots[p] = counts[p] == 0 ? markers[p].transform.position : markers[p].pivot switch
                {
                    MovingPart.PivotMode.TransformOrigin => markers[p].transform.position,
                    MovingPart.PivotMode.CapturedTop => new Vector3(center.x, max[p].y, center.z),
                    MovingPart.PivotMode.CapturedBottom => new Vector3(center.x, min[p].y, center.z),
                    _ => center,
                };
                radii[p] = markers[p].rollingRadius;
            }
            for (int i = 0; i < vertices.Count; i++)
            {
                int p = partIndex[i];
                if (p < 0 || markers[p].rollingRadius > 0f) continue;
                radii[p] = Mathf.Max(radii[p], Vector3.Cross(transform.TransformPoint(vertices[i]) - worldPivots[p], markers[p].WorldAxis).magnitude);
            }

            var report = new System.Text.StringBuilder();
            var worldToRoot = transform.worldToLocalMatrix;
            for (int p = 0; p < n; p++)
            {
                var marker = markers[p];
                bool spin = marker.motion == MovingPart.Motion.Spin;
                if (counts[p] == 0)
                {
                    Debug.LogWarning($"[MovingParts] '{marker.name}' captured no geometry. Enlarge its capture region so the gizmo encloses the part center (Islands) or the whole part (Vertices).", marker);
                    continue;
                }
                if (spin && radii[p] <= 0f)
                {
                    Debug.LogWarning($"[MovingParts] '{marker.name}' has zero radius - set Rolling Radius manually.", marker);
                    continue;
                }
                if (!spin && marker.period <= 0f)
                {
                    Debug.LogWarning($"[MovingParts] '{marker.name}' needs a Period above zero.", marker);
                    continue;
                }
                pivots[p] = worldToRoot.MultiplyPoint3x4(worldPivots[p]);
                var axis = worldToRoot.MultiplyVector(marker.WorldAxis).normalized;
                yawPitches[p] = new Vector2(Mathf.Atan2(axis.x, axis.z), Mathf.Asin(Mathf.Clamp(axis.y, -1f, 1f)));
                float cycles = Mathf.Max(1f, Mathf.Round(DrivePeriod / (spin ? 2f * Mathf.PI * radii[p] : marker.period)));
                speeds[p] = ((int) marker.motion + 1) * BandMagic + cycles;
                if (marker.reverse) speeds[p] = -speeds[p];
                float amplitude = marker.motion == MovingPart.Motion.Swing ? marker.swingAngle / MaxSwingAngle : marker.bobDistance / MaxBobDistance;
                float phase = marker.randomPhase ? Hash01(worldPivots[p]) : marker.phase;
                motionColors[p] = new Color32((byte) Mathf.RoundToInt(Mathf.Clamp01(amplitude) * 255f), (byte) Mathf.RoundToInt(Mathf.Clamp01(phase) * 255f), 0, 255);

                Undo.RecordObject(marker, "Bake Moving Parts");
                marker.baked = true;
                marker.bakedLocalPivot = marker.transform.InverseTransformPoint(worldPivots[p]);
                marker.bakedRadius = radii[p];
                report.Append(spin
                    ? $"\n  {marker.name}: spin, {counts[p]} vertices, radius {radii[p]:F3} m{(marker.reverse ? ", reversed" : "")}"
                    : $"\n  {marker.name}: {marker.motion.ToString().ToLower()}, {counts[p]} vertices, period {DrivePeriod / cycles:F2}, phase {phase:F2}");
            }
            return report.ToString();
        }

        static float Hash01(Vector3 p)
        {
            uint h = 2166136261u;
            foreach (float f in new[] { p.x, p.y, p.z })
            {
                h ^= (uint) Mathf.RoundToInt(f * 1000f);
                h *= 16777619u;
            }
            return (h & 0xFFFFFF) / 16777216f;
        }

        static string Hash(List<Vector3> vertices, List<Vector2> a, List<Vector2> b, List<Vector2> c, List<Color32> colors)
        {
            uint h = 2166136261u;
            void Mix(float f)
            {
                h ^= (uint) Mathf.RoundToInt(f * 1000f);
                h *= 16777619u;
            }
            foreach (var v in vertices) { Mix(v.x); Mix(v.y); Mix(v.z); }
            foreach (var v in a) { Mix(v.x); Mix(v.y); }
            foreach (var v in b) { Mix(v.x); Mix(v.y); }
            foreach (var v in c) { Mix(v.x); Mix(v.y); }
            foreach (var v in colors) { Mix(v.r); Mix(v.g); }
            return h.ToString("x8");
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MovingPartsBaker))]
    public class MovingPartsBakerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var baker = (MovingPartsBaker) target;
            int parts = System.Array.FindAll(baker.GetComponentsInChildren<MovingPart>(true), p => !p.exclude).Length;

            EditorGUILayout.HelpBox(baker.IsBaked
                ? $"Baked into '{baker.bakedMesh.name}'. Unbake to edit the source objects again."
                : $"{parts} moving part(s) found. Add an empty child with a Moving Part per wheel, sign or float and size its capture region so the gizmo encloses it (or put the component on separate part objects); child markers with Exclude ticked remove axles and forks from their part. Then Bake: everything under this object becomes one mesh whose parts spin, swing or bob in the shader. Set Auto Speed on the material and enable Always Refresh in the Scene view to preview. Baked motion is visual only - collision stays where it was.",
                MessageType.Info);

            bool persistent = EditorUtility.IsPersistent(baker.gameObject);
            if (persistent)
                EditorGUILayout.HelpBox("Open the prefab to bake.", MessageType.Warning);
            else if (PrefabUtility.IsPartOfPrefabInstance(baker.gameObject) && PrefabStageUtility.GetPrefabStage(baker.gameObject) == null)
                EditorGUILayout.HelpBox("Prefab instance: the baked mesh is stored in this scene only - do not apply these overrides to the prefab asset. Bake inside the prefab instead to get a mesh asset.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(persistent))
            {
                if (!baker.IsBaked)
                {
                    if (GUILayout.Button("Bake")) baker.Bake();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Rebake")) baker.Bake();
                    if (GUILayout.Button("Unbake")) baker.Unbake();
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }
#endif

}
