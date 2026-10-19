using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class LiftObjectData : ISerializable
    {

        public Vector3 endPosition = new(0, 0, 50);
        public Vector3[] mastPositions;

        public void Serialize(ISerializer serializer)
        {
            endPosition = serializer.SerializeVector3("end-position", endPosition);
            serializer.SerializeArray("masts", ref mastPositions, (id, v) => serializer.SerializeVector3(id, v));
        }

    }

    [ExecuteAlways]
    public class LiftSetup : MonoBehaviour
    {

        public Vector3 endPosition = new(0, 0, 50);
        public List<Vector3> mastPositions = new();
        public float autoMastInterval = 40f;

#if !JIBBERS_MAPTOOLS_INTERNAL
        [HideInInspector]
#endif
        public LiftStation baseStation;
#if !JIBBERS_MAPTOOLS_INTERNAL
        [HideInInspector]
#endif
        public LiftStation topStation;
#if !JIBBERS_MAPTOOLS_INTERNAL
        [HideInInspector]
#endif
        public LiftMast mastTemplate;
#if !JIBBERS_MAPTOOLS_INTERNAL
        [HideInInspector]
#endif
        public Material cableMaterial;
#if !JIBBERS_MAPTOOLS_INTERNAL
        [HideInInspector]
#endif
        public float cableRadius = 0.03f;
#if !JIBBERS_MAPTOOLS_INTERNAL
        [HideInInspector]
#endif
        public int cableSides = 8;

        public LiftObjectData ToData() => new()
        {
            endPosition = endPosition,
            mastPositions = mastPositions.ToArray(),
        };

        public void FromData(LiftObjectData data)
        {
            endPosition = data.endPosition;
            mastPositions = data.mastPositions != null ? new List<Vector3>(data.mastPositions) : new List<Vector3>();
#if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                Rebuild();
            }
#endif
        }

#if UNITY_EDITOR
        const string PreviewName = "[LiftPreview]";

        static Material fallbackCableMaterial;

        Transform previewRoot;
        readonly List<Transform> mastPreviews = new();
        Mesh cableMesh;

        void OnEnable()
        {
            if(!Application.isPlaying)
                ScheduleRebuild();
        }

        void OnDisable()
        {
            if(!Application.isPlaying)
                ClearPreviews();
        }

        void OnValidate()
        {
            if(!Application.isPlaying)
                ScheduleRebuild();
        }

        public void ScheduleRebuild()
        {
            UnityEditor.EditorApplication.delayCall -= DelayedRebuild;
            UnityEditor.EditorApplication.delayCall += DelayedRebuild;
        }

        void DelayedRebuild()
        {
            if(this)
                Rebuild();
        }

        public void Rebuild()
        {
            if(Application.isPlaying || !isActiveAndEnabled)
                return;
            if(UnityEditor.EditorUtility.IsPersistent(gameObject))
                return;
            if(UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null)
                return;

            ClearPreviews();

            var parameters = GetComponent<MapObject>()?.parameters;
            float slack = LiftGeometry.GetFloat(parameters, "slack", LiftGeometry.DefaultSlack);

            previewRoot = new GameObject(PreviewName).transform;
            previewRoot.SetParent(transform, false);
            previewRoot.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;

            if(baseStation)
            {
                baseStation.gameObject.SetActive(true);
                baseStation.transform.position = transform.position;
            }
            if(topStation)
            {
                topStation.gameObject.SetActive(true);
                topStation.transform.position = transform.TransformPoint(endPosition);
            }

            mastPreviews.Clear();
            var masts = new List<LiftMast>();
            if(mastTemplate)
                foreach(var pos in mastPositions)
                {
                    var mast = SpawnMastPreview(transform.TransformPoint(pos));
                    mastPreviews.Add(mast.transform);
                    masts.Add(mast);
                }

            LiftGeometry.OrientPieces(baseStation ? baseStation.transform : null, topStation ? topStation.transform : null, mastPreviews);

            var path = LiftGeometry.BuildLoop(baseStation, topStation, masts, slack);
            if(path == null)
                return;

            var cableGO = new GameObject("Cable");
            cableGO.transform.SetParent(previewRoot, false);
            cableGO.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            cableMesh = LiftGeometry.BuildTube(path, cableRadius, cableSides, cableGO.transform);
            if(cableMesh == null)
                return;
            cableMesh.hideFlags = HideFlags.DontSave;
            cableGO.AddComponent<MeshFilter>().sharedMesh = cableMesh;
            cableGO.AddComponent<MeshRenderer>().sharedMaterial = cableMaterial ? cableMaterial : FallbackCableMaterial();
            UnityEditor.SceneVisibilityManager.instance.DisablePicking(cableGO, false);
        }

        void ClearPreviews()
        {
            for(int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if(child.name == PreviewName)
                    DestroyImmediate(child.gameObject);
            }
            if(cableMesh)
                DestroyImmediate(cableMesh);
            previewRoot = null;
            mastPreviews.Clear();
        }

        LiftMast SpawnMastPreview(Vector3 position)
        {
            var clone = Instantiate(mastTemplate, previewRoot);
            clone.gameObject.SetActive(true);
            clone.transform.position = position;
            clone.transform.rotation = Quaternion.identity;
            foreach(var collider in clone.GetComponentsInChildren<Collider>(true))
                DestroyImmediate(collider);
            foreach(var t in clone.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            UnityEditor.SceneVisibilityManager.instance.DisablePicking(clone.gameObject, true);
            return clone;
        }

        static Material FallbackCableMaterial()
        {
            if(fallbackCableMaterial)
                return fallbackCableMaterial;
            var shader = Shader.Find("Custom/CustomObjectLit");
            if(shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            fallbackCableMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            if(fallbackCableMaterial.HasProperty("_BaseColor"))
                fallbackCableMaterial.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f));
            return fallbackCableMaterial;
        }

        public int InsertIndexFor(Vector3 localPosition)
        {
            float T(Vector3 p)
            {
                var line = endPosition;
                line.y = 0;
                p.y = 0;
                return line.sqrMagnitude > 1e-6f ? Vector3.Dot(p, line) / line.sqrMagnitude : 0f;
            }

            float t = T(localPosition);
            for(int i = 0; i < mastPositions.Count; i++)
                if(T(mastPositions[i]) > t)
                    return i;
            return mastPositions.Count;
        }

        public void AutoPlaceMasts()
        {
            mastPositions.Clear();
            var start = transform.position;
            var end = transform.TransformPoint(endPosition);
            float distance = Vector3.Distance(start, end);
            int count = Mathf.FloorToInt(distance / Mathf.Max(autoMastInterval, 5f));
            for(int i = 1; i <= count; i++)
            {
                var p = Vector3.Lerp(start, end, (float) i / (count + 1));
                if(Physics.Raycast(p + Vector3.up * 500f, Vector3.down, out var hit, 10000f))
                    p.y = hit.point.y;
                mastPositions.Add(transform.InverseTransformPoint(p));
            }
            Rebuild();
        }
#endif

    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(LiftSetup))]
    public class LiftSetupEditor : UnityEditor.Editor
    {
        static LiftSetup editTarget;
        int selectedMast = -1;

        void OnEnable()
        {
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
            if(editTarget == target as LiftSetup)
                editTarget = null;
        }

        void OnUndoRedo()
        {
            if(target is LiftSetup setup && setup)
                setup.ScheduleRebuild();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var setup = (LiftSetup) target;

            if(!setup.GetComponent<MapObject>())
                UnityEditor.EditorGUILayout.HelpBox("No MapObject on this object — lift settings (speed, slack, reversed) come from its parameters.", UnityEditor.MessageType.Warning);

            UnityEditor.EditorGUILayout.Space(8);

            bool editing = editTarget == setup;
            GUI.color = editing ? Color.yellow : Color.white;
            if(GUILayout.Button(editing ? "Editing Lift (Esc to stop)" : "Edit Lift"))
            {
                editTarget = editing ? null : setup;
                if(editTarget != null && UnityEditor.SceneView.lastActiveSceneView != null)
                    UnityEditor.SceneView.lastActiveSceneView.Focus();
                UnityEditor.SceneView.RepaintAll();
            }
            GUI.color = Color.white;

            if(setup.mastTemplate)
            {
                UnityEditor.EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("Auto-Place Masts"))
                {
                    UnityEditor.Undo.RecordObject(setup, "Auto-Place Masts");
                    setup.AutoPlaceMasts();
                }
                if(GUILayout.Button("Clear Masts"))
                {
                    UnityEditor.Undo.RecordObject(setup, "Clear Masts");
                    setup.mastPositions.Clear();
                    setup.Rebuild();
                }
                UnityEditor.EditorGUILayout.EndHorizontal();
            }

            UnityEditor.EditorGUILayout.HelpBox(setup.mastTemplate
                ? "Edit Lift: drag the station spheres, click the ground to add a mast, Ctrl-click a mast to delete it, X deletes the selected mast."
                : "Edit Lift: drag the station spheres.", UnityEditor.MessageType.None);
        }

        void OnSceneGUI()
        {
            var setup = (LiftSetup) target;
            if(!setup || !setup.isActiveAndEnabled)
                return;

            if(setup.transform.hasChanged)
            {
                setup.transform.hasChanged = false;
                setup.ScheduleRebuild();
            }

            if(editTarget != setup)
                return;

            int controlId = GUIUtility.GetControlID("LiftSetup".GetHashCode(), FocusType.Passive);
            UnityEditor.HandleUtility.AddDefaultControl(controlId);
            var evt = Event.current;

            if(evt.type == EventType.KeyDown)
            {
                if(evt.keyCode == KeyCode.Escape)
                {
                    editTarget = null;
                    evt.Use();
                    Repaint();
                    return;
                }
                if(evt.keyCode == KeyCode.X && selectedMast >= 0 && selectedMast < setup.mastPositions.Count)
                {
                    UnityEditor.Undo.RecordObject(setup, "Delete Lift Mast");
                    setup.mastPositions.RemoveAt(selectedMast);
                    selectedMast = -1;
                    setup.Rebuild();
                    evt.Use();
                    return;
                }
            }

            var root = setup.transform;
            var baseWorld = root.position;
            var topWorld = root.TransformPoint(setup.endPosition);

            UnityEditor.Handles.color = MoreColors.Slate;
            UnityEditor.Handles.DrawDottedLine(baseWorld, topWorld, 4f);

            DrawStationHandle(setup, root, baseWorld, true, evt);
            DrawStationHandle(setup, root, topWorld, false, evt);
            if(setup.mastTemplate)
            {
                DrawMastHandles(setup, root, evt);
                HandleGroundClick(setup, root, controlId, evt);
            }
        }

        void DrawStationHandle(LiftSetup setup, Transform root, Vector3 world, bool isBase, Event evt)
        {
            float size = UnityEditor.HandleUtility.GetHandleSize(world) * 0.2f;
            UnityEditor.Handles.color = isBase ? MoreColors.Mint : MoreColors.JibbersOrange;
            UnityEditor.Handles.DrawLine(world, world + Vector3.up * size * 5f, 2f);
            UnityEditor.Handles.Label(world + Vector3.up * size * 6f, isBase ? "Base" : "Top", UnityEditor.EditorStyles.whiteBoldLabel);

            UnityEditor.EditorGUI.BeginChangeCheck();
            var moved = UnityEditor.Handles.FreeMoveHandle(world, size, Vector3.zero, UnityEditor.Handles.SphereHandleCap);
            if(UnityEditor.EditorGUI.EndChangeCheck())
            {
                moved = CursorGroundPoint(evt, moved);
                if(isBase)
                {
                    var topWorld = root.TransformPoint(setup.endPosition);
                    var mastWorlds = new Vector3[setup.mastPositions.Count];
                    for(int i = 0; i < mastWorlds.Length; i++)
                        mastWorlds[i] = root.TransformPoint(setup.mastPositions[i]);

                    UnityEditor.Undo.RecordObject(root, "Move Lift Base");
                    UnityEditor.Undo.RecordObject(setup, "Move Lift Base");
                    root.position = moved;
                    root.hasChanged = false;
                    setup.endPosition = root.InverseTransformPoint(topWorld);
                    for(int i = 0; i < mastWorlds.Length; i++)
                        setup.mastPositions[i] = root.InverseTransformPoint(mastWorlds[i]);
                }
                else
                {
                    UnityEditor.Undo.RecordObject(setup, "Move Lift Top");
                    setup.endPosition = root.InverseTransformPoint(moved);
                }
                setup.Rebuild();
            }
        }

        void DrawMastHandles(LiftSetup setup, Transform root, Event evt)
        {
            bool deleteMode = evt.control;

            for(int i = 0; i < setup.mastPositions.Count; i++)
            {
                var world = root.TransformPoint(setup.mastPositions[i]);
                float size = UnityEditor.HandleUtility.GetHandleSize(world) * 0.15f;
                UnityEditor.Handles.color = deleteMode ? MoreColors.Crimson : i == selectedMast ? MoreColors.JibbersOrange : MoreColors.Azure;
                UnityEditor.Handles.DrawLine(world, world + Vector3.up * size * 4f, 2f);

                if(deleteMode)
                {
                    if(UnityEditor.Handles.Button(world, Quaternion.identity, size, size * 2f, UnityEditor.Handles.SphereHandleCap))
                    {
                        UnityEditor.Undo.RecordObject(setup, "Delete Lift Mast");
                        setup.mastPositions.RemoveAt(i);
                        if(selectedMast == i)
                            selectedMast = -1;
                        else if(selectedMast > i)
                            selectedMast--;
                        setup.Rebuild();
                        break;
                    }
                }
                else if(i == selectedMast)
                {
                    UnityEditor.EditorGUI.BeginChangeCheck();
                    var moved = UnityEditor.Handles.FreeMoveHandle(world, size * 1.2f, Vector3.zero, UnityEditor.Handles.SphereHandleCap);
                    if(UnityEditor.EditorGUI.EndChangeCheck())
                    {
                        UnityEditor.Undo.RecordObject(setup, "Move Lift Mast");
                        setup.mastPositions[i] = root.InverseTransformPoint(CursorGroundPoint(evt, moved));
                        setup.Rebuild();
                    }
                }
                else if(UnityEditor.Handles.Button(world, Quaternion.identity, size, size * 2.5f, UnityEditor.Handles.SphereHandleCap))
                {
                    selectedMast = i;
                    Repaint();
                }
            }
        }

        void HandleGroundClick(LiftSetup setup, Transform root, int controlId, Event evt)
        {
            if(evt.type != EventType.MouseDown || evt.button != 0 || evt.alt || evt.control)
                return;
            if(UnityEditor.HandleUtility.nearestControl != controlId)
                return;

            var ray = UnityEditor.HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if(Physics.Raycast(ray, out var hit, 100000f))
            {
                UnityEditor.Undo.RecordObject(setup, "Add Lift Mast");
                var local = root.InverseTransformPoint(hit.point);
                int index = setup.InsertIndexFor(local);
                setup.mastPositions.Insert(index, local);
                selectedMast = index;
                setup.Rebuild();
            }
            evt.Use();
        }

        static Vector3 CursorGroundPoint(Event evt, Vector3 fallback)
        {
            var ray = UnityEditor.HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if(Physics.Raycast(ray, out var hit, 100000f))
                return hit.point;
            if(Physics.Raycast(fallback + Vector3.up * 500f, Vector3.down, out hit, 10000f))
                fallback.y = hit.point.y;
            return fallback;
        }
    }
#endif

}
