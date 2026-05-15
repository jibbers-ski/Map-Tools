using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Jibbers.MapTools
{

    public class MapSpawnPoint : MonoBehaviour
    {

        public bool forwardVelocity;
        public float speedKmh = 36f;
        public Vector3 velocity;

        public Vector3 GetVelocity()
        {
            return forwardVelocity ? transform.forward * (speedKmh / 3.6f) : velocity;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.red;
            Gizmos.DrawCube(new Vector3(0.11f, -0.9f, 0.1f), new Vector3(0.15f, 0.1f, 1.8f));
            Gizmos.DrawCube(new Vector3(-0.11f, -0.9f, 0.1f), new Vector3(0.15f, 0.1f, 1.8f));

            Gizmos.color = Color.black;
            Gizmos.DrawCube(new Vector3(0, 0.75f, 0.25f), new Vector3(0.3f, 0.2f, 0.1f));

            Gizmos.color = Color.green;
            Gizmos.DrawCube(Vector3.zero, new Vector3(0.5f, 2, 0.5f));
            Gizmos.DrawCube(new Vector3(0, 0.6f, 0), new Vector3(1.7f, 0.15f, 0.15f));

            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, GetVelocity().normalized * 2);
        }

    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MapSpawnPoint))]
    public class MapSpawnPointEditor : Editor
    {
        static MapSpawnPoint placingTarget;
        static Vector3 placeHitPoint;
        static bool placeDragging;

        void OnEnable()
        {
            SceneView.beforeSceneGui -= HandlePlaceEscape;
            SceneView.beforeSceneGui += HandlePlaceEscape;
            SceneView.duringSceneGui -= OnPlaceSceneGUI;
            SceneView.duringSceneGui += OnPlaceSceneGUI;
        }

        void OnDisable()
        {
            SceneView.beforeSceneGui -= HandlePlaceEscape;
            SceneView.duringSceneGui -= OnPlaceSceneGUI;
            if (placingTarget == (MapSpawnPoint)target)
                placingTarget = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var sp = (MapSpawnPoint) target;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("forwardVelocity"));

            if (sp.forwardVelocity)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("speedKmh"), new GUIContent("Speed (kph)"));
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("velocity"));
                EditorGUILayout.LabelField("Speed", $"{sp.velocity.magnitude * 3.6f:F1} kph");
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Snap to Ground"))
            {
                if (Physics.Raycast(sp.transform.position, Vector3.down, out RaycastHit hit, 10000f))
                {
                    Undo.RecordObject(sp.transform, "Snap to Ground");
                    sp.transform.position = hit.point + hit.normal;
                    Vector3 forward = Vector3.ProjectOnPlane(sp.transform.forward, hit.normal).normalized;
                    if (forward.sqrMagnitude < 0.001f)
                        forward = Vector3.ProjectOnPlane(Vector3.forward, hit.normal).normalized;
                    sp.transform.rotation = Quaternion.LookRotation(forward, hit.normal);
                }
            }

            bool isPlacing = placingTarget == sp;
            GUI.color = isPlacing ? Color.yellow : Color.white;
            if (GUILayout.Button(isPlacing ? "Placing..." : "Place"))
            {
                placingTarget = isPlacing ? null : sp;
                if (placingTarget != null && SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.Focus();
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        static void HandlePlaceEscape(SceneView sceneView)
        {
            if (placingTarget == null) return;
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                placingTarget = null;
                placeDragging = false;
                evt.Use();
            }
        }

        static void OnPlaceSceneGUI(SceneView sceneView)
        {
            if (placingTarget == null) return;

            int controlId = GUIUtility.GetControlID("SpawnPointPlace".GetHashCode(), FocusType.Keyboard);
            HandleUtility.AddDefaultControl(controlId);
            sceneView.Repaint();

            var evt = Event.current;
            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 50000f))
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(hit.point, hit.normal, 0.5f);

                if (placeDragging)
                {
                    Vector3 dragDir = Vector3.ProjectOnPlane(hit.point - placeHitPoint, Vector3.up).normalized;
                    if (dragDir.sqrMagnitude > 0.001f)
                    {
                        Handles.color = Color.cyan;
                        Handles.DrawLine(placeHitPoint + Vector3.up, placeHitPoint + Vector3.up + dragDir * 2f);
                    }
                }
            }

            Handles.BeginGUI();
            var mp = evt.mousePosition;
            GUI.Label(new Rect(mp.x + 15, mp.y - 10, 300, 20),
                "Click to place, drag to set direction", EditorStyles.whiteBoldLabel);
            Handles.EndGUI();

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (Physics.Raycast(ray, out RaycastHit downHit, 50000f))
                {
                    Undo.RecordObject(placingTarget.transform, "Place Spawn Point");
                    placeHitPoint = downHit.point;
                    placingTarget.transform.position = downHit.point + downHit.normal;
                    Vector3 fwd = Vector3.ProjectOnPlane(placingTarget.transform.forward, downHit.normal).normalized;
                    if (fwd.sqrMagnitude < 0.001f)
                        fwd = Vector3.ProjectOnPlane(Vector3.forward, downHit.normal).normalized;
                    placingTarget.transform.rotation = Quaternion.LookRotation(fwd, downHit.normal);
                    placeDragging = true;
                }
                evt.Use();
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && placeDragging)
            {
                if (Physics.Raycast(ray, out RaycastHit dragHit, 50000f))
                {
                    Vector3 dragDir = Vector3.ProjectOnPlane(dragHit.point - placeHitPoint, Vector3.up).normalized;
                    if (dragDir.sqrMagnitude > 0.001f)
                    {
                        Vector3 up = placingTarget.transform.up;
                        Vector3 projected = Vector3.ProjectOnPlane(dragDir, up).normalized;
                        if (projected.sqrMagnitude > 0.001f)
                            placingTarget.transform.rotation = Quaternion.LookRotation(projected, up);
                    }
                }
                evt.Use();
            }

            if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                placeDragging = false;
                placingTarget = null;
                evt.Use();
            }
        }
    }
#endif

    public class SpawnPointData : ISerializable
    {

        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 velocity;

        public SpawnPointData() {}
        public SpawnPointData(MapSpawnPoint spawnPoint)
        {
            name = spawnPoint.name;
            position = spawnPoint.transform.position;
            rotation = Quaternion.LookRotation(spawnPoint.transform.forward).eulerAngles;
            velocity = spawnPoint.GetVelocity();
        }

        public void Serialize(ISerializer serializer)
        {
            name = serializer.SerializeString("name", name);
            position = serializer.SerializeVector3("position", position);
            rotation = serializer.SerializeVector3("rotation", rotation);
            velocity = serializer.SerializeVector3("velocity", velocity);
        }
    }

}
