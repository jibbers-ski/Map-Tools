#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class DuplicateXTimesWindow : EditorWindow
    {
        static int     count  = 5;
        static Vector3 offset = new Vector3(1, 0, 0);

        GameObject[] sources;

        [MenuItem("GameObject/Jibbers/Duplicate X Times", false, 10)]
        static void Open()
        {
            var window = CreateInstance<DuplicateXTimesWindow>();
            window.titleContent = new GUIContent("Duplicate X Times");
            window.sources = Selection.gameObjects;
            window.minSize = new Vector2(340, 160);
            window.maxSize = new Vector2(600, 160);
            window.ShowUtility();
        }

        [MenuItem("GameObject/Jibbers/Duplicate X Times", true)]
        static bool Validate() => Selection.activeGameObject != null;

        void OnGUI()
        {
            if (sources == null || sources.Length == 0)
            {
                EditorGUILayout.LabelField("No source objects.");
                if (GUILayout.Button("Close")) Close();
                return;
            }

            string label = sources.Length == 1 ? sources[0].name : $"{sources.Length} objects";
            EditorGUILayout.LabelField("Source", label);

            EditorGUILayout.Space(4);
            count  = Mathf.Max(1, EditorGUILayout.IntField("Count", count));
            offset = EditorGUILayout.Vector3Field("Offset per copy", offset);

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                $"Creates {count} additional cop{(count == 1 ? "y" : "ies")} of {label}. " +
                $"Copy i sits at source + offset × i (so the first copy is at source + offset).",
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            bool applyClicked  = GUILayout.Button("Apply");
            bool cancelClicked = GUILayout.Button("Cancel");
            EditorGUILayout.EndHorizontal();

            if (!applyClicked && !cancelClicked) return;

            var capturedSources = sources;
            var capturedCount   = count;
            var capturedOffset  = offset;
            bool doApply        = applyClicked;

            Close();

            if (doApply)
                EditorApplication.delayCall += () => StaticApply(capturedSources, capturedCount, capturedOffset);

            GUIUtility.ExitGUI();
        }

        static void StaticApply(GameObject[] sources, int count, Vector3 offset)
        {
            if (sources == null || sources.Length == 0 || count <= 0) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Duplicate X Times");
            int group = Undo.GetCurrentGroup();

            var prevSelection = Selection.objects;

            foreach (var src in sources)
            {
                if (src == null) continue;
                var basePos = src.transform.position;

                for (int i = 1; i <= count; i++)
                {
                    Selection.activeGameObject = src;
                    EditorApplication.ExecuteMenuItem("Edit/Duplicate");
                    var copy = Selection.activeGameObject;
                    if (copy != null && copy != src)
                        copy.transform.position = basePos + offset * i;
                }
            }

            Selection.objects = prevSelection;
            Undo.CollapseUndoOperations(group);
        }
    }

}
#endif
