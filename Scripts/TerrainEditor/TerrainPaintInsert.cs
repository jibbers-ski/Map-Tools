using System;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomPropertyDrawer(typeof(TerrainPaintInsert))]
    public class TerrainPaintInsertDrawer : TerrainInsertDrawer
    {
        static readonly string[] markingColorNames = { "Red", "Orange", "Gold", "Yellow", "Yellow-Green", "Lime", "Light Green", "Green", "Teal", "Cyan", "Light Blue", "Blue", "Dark Blue", "Purple", "Pink", "Magenta" };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!BeginInsertGUI(position, property, label)) return;

            DrawNameRow(property);

            var startXProp = property.FindPropertyRelative("startX");
            var startYProp = property.FindPropertyRelative("startY");
            var endXProp   = property.FindPropertyRelative("endX");
            var endYProp   = property.FindPropertyRelative("endY");

            DrawCoordinatePicker("Start", startXProp, startYProp);
            DrawCoordinatePicker("End",   endXProp,   endYProp);

            var widthProp = property.FindPropertyRelative("width");
            widthProp.floatValue = EditorGUI.Slider(Row(10), "width", widthProp.floatValue, 0f, 2048f);

            var lengthCurveProp = property.FindPropertyRelative("lengthCurve");
            lengthCurveProp.animationCurveValue = EditorGUI.CurveField(
                Row(10), "lengthCurve", lengthCurveProp.animationCurveValue,
                MoreColors.Violet, new Rect(0, 0, 1, 1));

            var crossCurveProp = property.FindPropertyRelative("crossSectionCurve");
            crossCurveProp.animationCurveValue = EditorGUI.CurveField(
                Row(10), "crossSectionCurve", crossCurveProp.animationCurveValue,
                MoreColors.Forest, new Rect(0, 0, 1, 1));

            var repeatsProp = property.FindPropertyRelative("repeats");
            repeatsProp.intValue = Mathf.Max(1, EditorGUI.IntField(Row(10), "repeats", repeatsProp.intValue));

            var repeatScalingProp = property.FindPropertyRelative("repeatScaling");
            repeatScalingProp.floatValue = EditorGUI.FloatField(Row(), "repeatScaling", repeatScalingProp.floatValue);

            var targetProp = property.FindPropertyRelative("target");
            targetProp.enumValueIndex = (int)(PaintTarget) EditorGUI.EnumPopup(
                Row(10), "target", (PaintTarget) targetProp.enumValueIndex);

            if (targetProp.enumValueIndex == (int) PaintTarget.Marking)
            {
                var colorIdxProp = property.FindPropertyRelative("markingColorIdx");
                int idx = Mathf.Clamp(colorIdxProp.intValue, 0, markingColorNames.Length - 1);
                colorIdxProp.intValue = EditorGUI.Popup(Row(), "markingColor", idx, markingColorNames);
            }

            var eraseProp = property.FindPropertyRelative("erase");
            eraseProp.boolValue = EditorGUI.Toggle(Row(), "erase", eraseProp.boolValue);

            var coverageProp = property.FindPropertyRelative("coverage");
            coverageProp.floatValue = EditorGUI.Slider(Row(), "coverage", coverageProp.floatValue, 0f, 1f);

            DrawApplyButton(
                editor != null && editor.terrain != null
                    && listIdx >= 0
                    && editor.paintInserts != null && listIdx < editor.paintInserts.Count,
                () => editor.BuildPaint(editor.paintInserts[listIdx]));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            bool marking = property.FindPropertyRelative("target").enumValueIndex == (int) PaintTarget.Marking;
            int rows = marking ? 16 : 15;
            return line + (rows * (line + Spacing)) + 100f;
        }
    }
#endif

    public enum PaintTarget { Snow, Marking }

    [Serializable]
    public class TerrainPaintInsert : TerrainInsert
    {
        public TerrainPaintInsert() { name = "New Paint"; }

        public int startX;
        public int startY;
        public int endX;
        public int endY;

        public float          width             = 20f;
        public AnimationCurve lengthCurve       = AnimationCurve.Constant(0, 1, 1);
        public AnimationCurve crossSectionCurve = AnimationCurve.Constant(0, 1, 1);

        public int   repeats       = 1;
        public float repeatScaling = 1f;

        public PaintTarget target          = PaintTarget.Marking;
        public int         markingColorIdx = 0;
        public bool        erase           = false;
        public float       coverage        = 1f;
    }

}
