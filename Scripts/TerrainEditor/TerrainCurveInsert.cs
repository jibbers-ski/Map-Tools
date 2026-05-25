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
            EditorGUI.Slider(Row(), property.FindPropertyRelative("crossSectionSideFlatten"),
                0f, 0.5f, "crossSectionSideFlatten");

            EditorGUI.CurveField(Row(10), property.FindPropertyRelative("tiltCurve"),
                MoreColors.Orange, new Rect(0, -1, 1, 2), new GUIContent("tiltCurve"));
            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("tiltDepth"),
                new GUIContent("tiltDepth"));

            EditorGUI.Slider(Row(10), property.FindPropertyRelative("edgeBlend"),
                0f, 0.5f, "edgeBlend");
            EditorGUI.Slider(Row(), property.FindPropertyRelative("edgeFalloff"),
                0.25f, 4f, "edgeFalloff");
            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("edgeBlendMode"),
                new GUIContent("edgeBlendMode"));

            EditorGUI.PropertyField(Row(10), property.FindPropertyRelative("repeats"),
                new GUIContent("repeats"));
            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("repeatScaling"),
                new GUIContent("repeatScaling"));
            EditorGUI.Slider(Row(), property.FindPropertyRelative("repeatTransitionFade"),
                0f, 0.5f, "repeatTransitionFade");

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
            return line + (23 * (line + Spacing)) + 90f;
        }
    }
#endif

    public enum EdgeBlendMode { All, Sides, Ends }

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
        public AnimationCurve crossSection            = AnimationCurve.Constant(0, 1, 0);
        public float          crossSectionDepth       = 0;
        public int            crossSectionBakeRes     = 512;
        [Range(0, 0.5f)]
        public float          crossSectionSideFlatten = 0;

        [Space(20)]
        public AnimationCurve tiltCurve = AnimationCurve.Constant(0, 1, 0);
        public float          tiltDepth = 0;

        [Range(0, 0.5f)]
        public float          repeatTransitionFade    = 0;

        [Range(0, 0.5f)]
        public float          edgeBlend               = 0;

        [Range(0.25f, 4f)]
        public float          edgeFalloff             = 1;

        public EdgeBlendMode  edgeBlendMode           = EdgeBlendMode.All;

        [NonSerialized] public PreviewCache cache;

        public class PreviewCache
        {
            public int hash;
            public Vector3 labelPos;
            public Vector3[] mainLine;
            public Vector3[] leftEdge;
            public Vector3[] rightEdge;
            public Vector3[][] crossSections;
        }
    }

}
