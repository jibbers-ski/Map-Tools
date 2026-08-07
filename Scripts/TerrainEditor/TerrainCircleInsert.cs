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

            {
                var normalProp = property.FindPropertyRelative("normal");
                var row = Row(10);
                const float btnW = 70f;
                const float labelW = 50f;
                float fieldW = (row.width - labelW - btnW * 2 - 8) / 2f;
                EditorGUI.LabelField(new Rect(row.x, row.y, labelW, row.height), "Normal", EditorStyles.boldLabel);
                float savedLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 16f;
                var v = normalProp.vector2Value;
                EditorGUI.BeginChangeCheck();
                float nx = EditorGUI.FloatField(new Rect(row.x + labelW, row.y, fieldW, row.height), "x", v.x);
                float ny = EditorGUI.FloatField(new Rect(row.x + labelW + fieldW + 2, row.y, fieldW, row.height), "y", v.y);
                if (EditorGUI.EndChangeCheck())
                    normalProp.vector2Value = new Vector2(Mathf.Clamp(nx, -1f, 1f), Mathf.Clamp(ny, -1f, 1f));
                EditorGUIUtility.labelWidth = savedLW;
                if (GUI.Button(new Rect(row.xMax - btnW * 2 - 2, row.y, btnW, row.height), "Flatten"))
                    normalProp.vector2Value = Vector2.zero;
                EditorGUI.BeginDisabledGroup(editor == null || editor.terrain == null);
                if (GUI.Button(new Rect(row.xMax - btnW, row.y, btnW, row.height), "Current"))
                    normalProp.vector2Value = editor.GetTerrainNormalXZ(centerXProp.intValue, centerYProp.intValue);
                EditorGUI.EndDisabledGroup();
            }

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

            EditorGUI.Slider(Row(10), property.FindPropertyRelative("edgeBlend"),
                0f, 0.5f, "edgeBlend");
            EditorGUI.Slider(Row(), property.FindPropertyRelative("edgeFalloff"),
                0.25f, 4f, "edgeFalloff");

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
            return line + (12 * (line + Spacing)) + 60f;
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

        public Vector2 normal = Vector2.zero;

        [Range(0, 0.5f)]
        public float edgeBlend = 0;

        [Range(0.25f, 4f)]
        public float edgeFalloff = 1;

        public AnimationCurve radialCurve = AnimationCurve.Linear(0, 1, 1, 0);

        public Vector3 UnitNormal
        {
            get
            {
                var h = normal;
                if (h.sqrMagnitude > 0.98f) h *= Mathf.Sqrt(0.98f / h.sqrMagnitude);
                return new Vector3(h.x, Mathf.Sqrt(1f - h.sqrMagnitude), h.y);
            }
        }

        public Vector2 TiltSlope
        {
            get { var n = UnitNormal; return new Vector2(n.x, n.z) / n.y; }
        }
    }

}
