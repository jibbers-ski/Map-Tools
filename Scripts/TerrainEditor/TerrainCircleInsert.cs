using System;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomPropertyDrawer(typeof(TerrainCircleInsert))]
    public class TerrainCircleInsertDrawer : TerrainInsertDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!BeginInsertGUI(position, property, label)) return;

            DrawNameRow(property);

            var centerXProp = property.FindPropertyRelative("centerX");
            var centerYProp = property.FindPropertyRelative("centerY");

            DrawCoordinatePicker("Center", centerXProp, centerYProp);
            DrawHeightOverride(property.FindPropertyRelative("heightOverride"),
                "heightOverride", centerXProp, centerYProp);

            // radius + depth on one row
            {
                var row   = Row();
                float half = (row.width - 2) / 2f;
                float savedLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 40f;
                EditorGUI.Slider(new Rect(row.x,           row.y, half - 1, row.height),
                    property.FindPropertyRelative("radius"), 0f, 2048f, "radius");
                EditorGUI.Slider(new Rect(row.x + half + 1, row.y, half,   row.height),
                    property.FindPropertyRelative("depth"),  0f, 500f,  "depth");
                EditorGUIUtility.labelWidth = savedLW;
            }

            EditorGUI.CurveField(Row(10), property.FindPropertyRelative("radialCurve"),
                MoreColors.Mint, new Rect(0, -1, 1, 2), new GUIContent("radialCurve"));

            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("bakeResolution"),
                new GUIContent("bakeResolution"));

            DrawApplyButton(
                editor != null && editor.terrain != null
                    && listIdx >= 0
                    && editor.circleInserts != null && listIdx < editor.circleInserts.Count,
                () => editor.BuildCircle(editor.circleInserts[listIdx]));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            return line + (9 * (line + Spacing)) + 40f;
        }
    }
#endif

    [Serializable]
    public class TerrainCircleInsert : TerrainInsert
    {
        public TerrainCircleInsert() { name = "New Circle"; }

        public int centerX;
        public int centerY;

        public float radius         = 20f;
        public float heightOverride = -1f;
        public float depth          = 10f;

        public AnimationCurve radialCurve = AnimationCurve.Linear(0, 1, 1, 0);
    }

}
