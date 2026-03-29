using System;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomPropertyDrawer(typeof(TerrainCurveInsert))]
    public class TerrainCurveInsertDrawer : TerrainInsertDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!BeginInsertGUI(position, property, label)) return;

            DrawNameRow(property);

            var startXProp = property.FindPropertyRelative("startX");
            var startYProp = property.FindPropertyRelative("startY");
            var endXProp   = property.FindPropertyRelative("endX");
            var endYProp   = property.FindPropertyRelative("endY");

            DrawCoordinatePicker("Start", startXProp, startYProp);
            DrawCoordinatePicker("End", endXProp, endYProp);

            EditorGUI.Slider(Row(10), property.FindPropertyRelative("width"), 0f, 2048f, "width");

            var hoProp = property.FindPropertyRelative("heightOverrides");
            DrawHeightOverrideVector2(hoProp, "Start Height", 0, startXProp, startYProp);
            DrawHeightOverrideVector2(hoProp, "End Height",   1, endXProp,   endYProp);

            EditorGUI.CurveField(Row(10), property.FindPropertyRelative("curve"),
                MoreColors.Violet, new Rect(0, 0, 1, 1), new GUIContent("curve"));

            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("bakeResolution"),
                new GUIContent("bakeResolution"));

            EditorGUI.CurveField(Row(10), property.FindPropertyRelative("crossSection"),
                MoreColors.Forest, new Rect(0, -1, 1, 2), new GUIContent("crossSection"));

            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("crossSectionDepth"),
                new GUIContent("crossSectionDepth"));
            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("crossSectionBakeRes"),
                new GUIContent("crossSectionBakeRes"));

            // repeats + repeatScaling on one row
            {
                var row   = Row(10);
                float half = (row.width - 2) / 2f;
                float savedLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 70f;
                EditorGUI.PropertyField(new Rect(row.x,           row.y, half - 1, row.height),
                    property.FindPropertyRelative("repeats"),       new GUIContent("repeats"));
                EditorGUI.PropertyField(new Rect(row.x + half + 1, row.y, half,   row.height),
                    property.FindPropertyRelative("repeatScaling"), new GUIContent("repeatScaling"));
                EditorGUIUtility.labelWidth = savedLW;
            }

            DrawApplyButton(
                editor != null && editor.terrain != null
                    && listIdx >= 0
                    && editor.curveInserts != null && listIdx < editor.curveInserts.Count,
                () => editor.BuildCurve(editor.curveInserts[listIdx]));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            return line + (15 * (line + Spacing)) + 70f;
        }
    }
#endif

    [Serializable]
    public class TerrainCurveInsert : TerrainInsert
    {
        public TerrainCurveInsert() { name = "New Curve"; }

        public int startX;
        public int startY;

        public int endX;
        public int endY;

        [Space(20)]
        public float   width           = 20;
        public Vector2 heightOverrides = new Vector2(-1, -1);

        [Space(20)]
        public AnimationCurve curve         = AnimationCurve.Linear(0, 1, 1, 0);
        public int            repeats        = 1;
        public float          repeatScaling  = 1;

        [Space(20)]
        public AnimationCurve crossSection        = AnimationCurve.Constant(0, 1, 0);
        public float          crossSectionDepth   = 0;
        public int            crossSectionBakeRes = 512;
    }

}
