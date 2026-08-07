using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomEditor(typeof(TerrainHeightChanger))]
    [CanEditMultipleObjects]
    public class TerrainHeightChangerEditor : Editor
    {
        bool armed;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("newHeight"),
                new GUIContent("New Height Range", "The terrain's new size Y in metres."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("addRoom"),
                new GUIContent("Add Room", "Where the extra range goes when growing (and where it is taken from when shrinking). " +
                    "Above keeps the terrain floor fixed and adds headroom for building up. " +
                    "Below keeps the ceiling fixed and moves the Terrain object down, adding room to dig. " +
                    "Both splits it equally. World geometry never moves either way."));
            serializedObject.ApplyModifiedProperties();

            var changer = (TerrainHeightChanger)target;
            var terrain = changer.GetComponent<Terrain>();
            if (targets.Length == 1 && terrain != null && terrain.terrainData != null)
            {
                EditorGUILayout.LabelField("Current Height Range", terrain.terrainData.size.y.ToString("0.##") + " m");
                if (armed)
                {
                    EditorGUILayout.LabelField("Highest Point", changer.HighestPoint().ToString("0.##") + " m above floor");
                    EditorGUILayout.LabelField("Lowest Point", changer.LowestPoint().ToString("0.##") + " m above floor");
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Changes the terrain's height range (size Y) without moving any world-space geometry: the whole " +
                "heightmap is renormalized so every point keeps its exact world height (tiny float rounding aside).\n" +
                "Add Room picks where the new range goes — Above for more headroom, Below to allow digging deeper " +
                "(moves the Terrain object's Y; child objects are kept at their world positions). Formation/Ski-Line " +
                "baselines and Better-Terrain-Editor insert heights are adjusted along with it; painted terrain trees " +
                "are snapped back onto the surface.",
                MessageType.Info);

            string label = targets.Length > 1 ? $"Change {targets.Length} Terrains" : "Change Height Range";
            if (!armed)
            {
                if (GUILayout.Button(label))
                    armed = true;
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This rewrites the whole heightmap. Editor undo of terrain heightmaps is unreliable — use the Revert button below to go back exactly.",
                    MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm"))
                {
                    foreach (var t in targets)
                        ((TerrainHeightChanger)t).ChangeHeight();
                    armed = false;
                }
                if (GUILayout.Button("Cancel"))
                    armed = false;
                EditorGUILayout.EndHorizontal();
            }

            bool canRevert = false;
            foreach (var t in targets)
            {
                var c = (TerrainHeightChanger)t;
                var ter = c.GetComponent<Terrain>();
                if (c.PreviousHeight > 0f && ter != null && ter.terrainData != null
                    && !Mathf.Approximately(c.PreviousHeight, ter.terrainData.size.y))
                {
                    canRevert = true;
                    break;
                }
            }
            EditorGUI.BeginDisabledGroup(!canRevert);
            string revertLabel = canRevert && targets.Length == 1
                ? $"Revert to {((TerrainHeightChanger)target).PreviousHeight:0.##} m"
                : "Revert Last Change";
            if (GUILayout.Button(new GUIContent(revertLabel,
                "Runs the exact inverse renormalization back to the previous height range. Works even when editor undo doesn't cover the heightmap, and survives editor restarts.")))
            {
                foreach (var t in targets)
                    ((TerrainHeightChanger)t).Revert();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button(new GUIContent("Snap Trees To Ground",
                "Snaps every terrain tree onto the terrain surface at its XZ position (undoable). Use this to repair floating or buried trees.")))
            {
                foreach (var t in targets)
                    ((TerrainHeightChanger)t).SnapTreesToSurface();
            }
        }
    }
#endif

    public enum TerrainHeightRoom
    {
        [InspectorName("Above (keep floor)")] Above,
        [InspectorName("Below (keep ceiling)")] Below,
        [InspectorName("Both (split equally)")] Split,
    }

    public class TerrainHeightChanger : MonoBehaviour
    {
        public float newHeight = 128f;
        public TerrainHeightRoom addRoom = TerrainHeightRoom.Above;
        [SerializeField, HideInInspector] float previousHeight = -1f;
        [SerializeField, HideInInspector] float previousPosY = float.NaN;

        public float PreviousHeight => previousHeight;

        public float HighestPoint()
        {
            var terrain = GetComponent<Terrain>();
            var data = terrain != null ? terrain.terrainData : null;
            if (data == null) return 0f;
            int hres = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, hres, hres);
            float maxN = 0f;
            for (int y = 0; y < hres; y++)
                for (int x = 0; x < hres; x++)
                    if (heights[y, x] > maxN) maxN = heights[y, x];
            return maxN * data.size.y;
        }

        public float LowestPoint()
        {
            var terrain = GetComponent<Terrain>();
            var data = terrain != null ? terrain.terrainData : null;
            if (data == null) return 0f;
            int hres = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, hres, hres);
            float minN = 1f;
            for (int y = 0; y < hres; y++)
                for (int x = 0; x < hres; x++)
                    if (heights[y, x] < minN) minN = heights[y, x];
            return minN * data.size.y;
        }

        public void ChangeHeight()
        {
#if UNITY_EDITOR
            var terrain = GetComponent<Terrain>();
            var data = terrain != null ? terrain.terrainData : null;
            if (data == null)
            {
                Debug.LogError("[TerrainHeightChanger] Terrain has no TerrainData.");
                return;
            }
            float delta = newHeight - data.size.y;
            float bottom = addRoom == TerrainHeightRoom.Above ? 0f
                         : addRoom == TerrainHeightRoom.Below ? delta
                         : delta * 0.5f;
            ApplyRange(newHeight, terrain.transform.position.y - bottom);
#endif
        }

        public void Revert()
        {
#if UNITY_EDITOR
            if (previousHeight <= 0f) return;
            var terrain = GetComponent<Terrain>();
            if (terrain == null) return;
            ApplyRange(previousHeight, float.IsNaN(previousPosY) ? terrain.transform.position.y : previousPosY);
#endif
        }

        public void SnapTreesToSurface()
        {
#if UNITY_EDITOR
            var terrain = GetComponent<Terrain>();
            var data = terrain != null ? terrain.terrainData : null;
            if (data == null) return;
            if (data.treeInstances.Length == 0)
            {
                Debug.LogWarning("[TerrainHeightChanger] This terrain has 0 painted tree instances — nothing to snap. Trees that are GameObjects are not affected by this button; move them (or their parent) instead.");
                return;
            }
            UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Snap Trees To Surface");
            data.SetTreeInstances(data.treeInstances, true);
            UnityEditor.EditorUtility.SetDirty(data);
            Debug.Log($"[TerrainHeightChanger] Snapped {data.treeInstances.Length} trees onto the terrain surface.");
#endif
        }

#if UNITY_EDITOR
        void ApplyRange(float targetHeight, float targetPosY)
        {
            var terrain = GetComponent<Terrain>();
            var data = terrain != null ? terrain.terrainData : null;
            if (data == null)
            {
                Debug.LogError("[TerrainHeightChanger] Terrain has no TerrainData.");
                return;
            }

            float oldHeight = data.size.y;
            float oldPosY = terrain.transform.position.y;
            if (targetHeight < 0.01f)
            {
                Debug.LogError("[TerrainHeightChanger] New height range must be positive.");
                return;
            }
            if (Mathf.Approximately(targetHeight, oldHeight) && Mathf.Approximately(targetPosY, oldPosY))
            {
                Debug.Log("[TerrainHeightChanger] Height range unchanged — nothing to do.");
                return;
            }

            int hres = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, hres, hres);
            float minN = 1f, maxN = 0f;
            for (int y = 0; y < hres; y++)
                for (int x = 0; x < hres; x++)
                {
                    float h = heights[y, x];
                    if (h > maxN) maxN = h;
                    if (h < minN) minN = h;
                }

            float worldMin = oldPosY + minN * oldHeight;
            float worldMax = oldPosY + maxN * oldHeight;
            if (worldMax > targetPosY + targetHeight + 0.0001f)
            {
                Debug.LogError($"[TerrainHeightChanger] The terrain's highest point sits {worldMax - targetPosY:0.##} m above the new floor — a range of {targetHeight:0.##} m would clip it. Use at least {Mathf.Ceil(worldMax - targetPosY)} m, or add the room Above.");
                return;
            }
            if (worldMin < targetPosY - 0.0001f)
            {
                Debug.LogError($"[TerrainHeightChanger] Taking room from below would clip the terrain's lowest point ({worldMin - oldPosY:0.##} m above the current floor). Take the room from Above instead.");
                return;
            }

            float bottom = oldPosY - targetPosY;
            float scale = oldHeight / targetHeight;
            float offsetN = bottom / targetHeight;
            for (int y = 0; y < hres; y++)
                for (int x = 0; x < hres; x++)
                    heights[y, x] = Mathf.Clamp01(heights[y, x] * scale + offsetN);

            UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Change Terrain Height Range");
            UnityEditor.Undo.RecordObject(this, "Change Terrain Height Range");
            UnityEditor.Undo.RecordObject(terrain.transform, "Change Terrain Height Range");
            data.size = new Vector3(data.size.x, targetHeight, data.size.z);
            data.SetHeights(0, 0, heights);

            int childCount = 0;
            float childLift = oldPosY - targetPosY;
            var pos = terrain.transform.position;
            pos.y = targetPosY;
            terrain.transform.position = pos;
            if (Mathf.Abs(childLift) > 0.0001f)
            {
                var tf = terrain.transform;
                childCount = tf.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    var child = tf.GetChild(i);
                    UnityEditor.Undo.RecordObject(child, "Change Terrain Height Range");
                    child.position += Vector3.up * childLift;
                }
            }

            if (data.treeInstances.Length > 0)
                data.SetTreeInstances(data.treeInstances, true);

            previousHeight = oldHeight;
            previousPosY = oldPosY;
            UnityEditor.EditorUtility.SetDirty(data);
            UnityEditor.EditorUtility.SetDirty(this);

            var formation = GetComponent<FormationBuilder>();
            if (formation != null) formation.NotifyHeightRangeChanged(scale, offsetN);
            var skiLine = GetComponent<SkiLineBuilder>();
            if (skiLine != null) skiLine.NotifyHeightRangeChanged(scale, offsetN, bottom);
            var bte = GetComponent<BetterTerrainEditor>();
            if (bte != null && Mathf.Abs(bottom) > 0.0001f) bte.NotifyFloorMoved(bottom);

            string floorNote = bottom > 0.001f ? $", floor lowered {bottom:0.##} m"
                             : bottom < -0.001f ? $", floor raised {-bottom:0.##} m"
                             : ", room added above";
            string childNote = childCount > 0 ? $"; {childCount} child object(s) kept at their world positions" : "";
            Debug.Log($"[TerrainHeightChanger] '{terrain.name}' height range {oldHeight:0.##} m → {targetHeight:0.##} m{floorNote}; world geometry unchanged{childNote}. Revert restores the previous state exactly.");
        }
#endif
    }

}
