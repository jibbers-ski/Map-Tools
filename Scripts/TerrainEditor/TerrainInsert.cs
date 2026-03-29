using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    public abstract class TerrainInsertDrawer : PropertyDrawer
    {
        protected const float Spacing = 1f;
        static readonly Dictionary<string, double> appliedTimestamps = new Dictionary<string, double>();

        protected BetterTerrainEditor editor;
        protected int maxX, maxY;
        protected int listIdx;
        protected string insertName;
        protected float y;
        protected Rect position;
        protected float line;

        protected static int GetListIndex(SerializedProperty property)
        {
            var path  = property.propertyPath;
            int open  = path.LastIndexOf('[');
            int close = path.LastIndexOf(']');
            if (open >= 0 && close > open
                && int.TryParse(path.Substring(open + 1, close - open - 1), out int idx))
                return idx;
            return -1;
        }

        protected static void HalfIntSlider(Rect row, float xOff, float halfW,
            SerializedProperty prop, int min, int max, string lbl)
        {
            const float arrowW = 20f;
            float sliderW = halfW - arrowW * 2 - 4;
            EditorGUI.IntSlider(new Rect(xOff,                   row.y, sliderW, row.height), prop, min, max, lbl);
            if (GUI.Button(new Rect(xOff + sliderW + 2,          row.y, arrowW,  row.height), "<"))
                prop.intValue = Mathf.Clamp(prop.intValue - 1, min, max);
            if (GUI.Button(new Rect(xOff + sliderW + 2 + arrowW, row.y, arrowW,  row.height), ">"))
                prop.intValue = Mathf.Clamp(prop.intValue + 1, min, max);
        }

        protected Rect Row(float margin = 0)
        {
            y += margin;
            var r = new Rect(position.x, y, position.width, line);
            y += line + Spacing;
            return r;
        }

        // Returns false if collapsed — caller should return early
        protected bool BeginInsertGUI(Rect pos, SerializedProperty property, GUIContent label)
        {
            position = pos;
            EditorGUI.BeginProperty(position, label, property);
            line = EditorGUIUtility.singleLineHeight;

            listIdx    = GetListIndex(property);
            insertName = property.FindPropertyRelative("name").stringValue;
            string foldLabel = listIdx >= 0 ? $"[{listIdx}]  {insertName}" : insertName;

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, line),
                property.isExpanded, foldLabel, true);

            if (!property.isExpanded) { EditorGUI.EndProperty(); return false; }

            editor = property.serializedObject.targetObject as BetterTerrainEditor;
            maxX   = editor != null && editor.resX > 0 ? editor.resX : 2048;
            maxY   = editor != null && editor.resY > 0 ? editor.resY : 2048;

            y = position.y + line + Spacing;
            return true;
        }

        protected void DrawNameRow(SerializedProperty property)
        {
            var row    = Row();
            const float checkW = 70f;
            float nameW  = row.width - checkW * 2 - 4;
            float savedLW = EditorGUIUtility.labelWidth;
            EditorGUI.PropertyField(new Rect(row.x, row.y, nameW, row.height),
                property.FindPropertyRelative("name"), GUIContent.none);
            EditorGUIUtility.labelWidth = 52f;
            EditorGUI.PropertyField(new Rect(row.x + nameW + 2,          row.y, checkW, row.height),
                property.FindPropertyRelative("enabled"),     new GUIContent("enabled"));
            EditorGUI.PropertyField(new Rect(row.x + nameW + 2 + checkW, row.y, checkW, row.height),
                property.FindPropertyRelative("drawPreview"), new GUIContent("preview"));
            EditorGUIUtility.labelWidth = savedLW;
        }

        protected void DrawCoordinatePicker(string headerLabel, SerializedProperty xProp, SerializedProperty yProp)
        {
            // Header with height display and Pick button
            {
                var row = Row(10);
                const float pickW = 50f;
                float labelW = headerLabel.Length * 8f + 4f;
                var labelR  = new Rect(row.x,            row.y, labelW,                             row.height);
                var heightR = new Rect(row.x + labelW + 2, row.y, row.width - labelW - pickW - 8f, row.height);
                var pickR   = new Rect(row.xMax - pickW, row.y, pickW,                              row.height);
                EditorGUI.LabelField(labelR, headerLabel, EditorStyles.boldLabel);
                if (editor != null && editor.terrain != null)
                    EditorGUI.LabelField(heightR, $"H: {editor.GetHeight(xProp.intValue, yProp.intValue):F1}", EditorStyles.miniLabel);
                bool isPicking = BetterTerrainEditorEditor.pickTargetXProp?.propertyPath == xProp.propertyPath;
                GUI.color = isPicking ? Color.yellow : Color.white;
                if (GUI.Button(pickR, isPicking ? "..." : "Pick"))
                {
                    BetterTerrainEditorEditor.pickTargetXProp = xProp;
                    BetterTerrainEditorEditor.pickTargetYProp = yProp;
                    BetterTerrainEditorEditor.pickEditor      = editor;
                    BetterTerrainEditorEditor.pickLabel       = insertName + " " + headerLabel;
                }
                GUI.color = Color.white;
            }
            // XY sliders
            {
                var row   = Row();
                float half = (row.width - 2) / 2f;
                float savedLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 16f;
                HalfIntSlider(row, row.x,           half, xProp, 0, maxX, "x");
                HalfIntSlider(row, row.x + half + 2, half, yProp, 0, maxY, "y");
                EditorGUIUtility.labelWidth = savedLW;
            }
        }

        protected void DrawHeightOverride(SerializedProperty hoProp, string label,
            SerializedProperty xProp, SerializedProperty yProp)
        {
            var row    = Row(10);
            const float btnW = 70f;
            EditorGUI.PropertyField(new Rect(row.x, row.y, row.width - btnW * 2 - 4, row.height),
                hoProp, new GUIContent(label));
            bool canSet = editor != null && editor.terrain != null;
            EditorGUI.BeginDisabledGroup(!canSet);
            if (GUI.Button(new Rect(row.xMax - btnW * 2 - 2, row.y, btnW, row.height), "Auto"))
            {
                hoProp.floatValue = -1f;
                hoProp.serializedObject.ApplyModifiedProperties();
            }
            if (GUI.Button(new Rect(row.xMax - btnW, row.y, btnW, row.height), "Current"))
            {
                hoProp.floatValue = editor.GetHeight(xProp.intValue, yProp.intValue);
                hoProp.serializedObject.ApplyModifiedProperties();
            }
            EditorGUI.EndDisabledGroup();
        }

        protected void DrawHeightOverrideVector2(SerializedProperty hoProp, string label,
            int component, SerializedProperty xProp, SerializedProperty yProp)
        {
            var row    = Row();
            const float btnW = 70f;
            float fieldW = row.width - btnW * 2 - 4;
            var v = hoProp.vector2Value;
            float savedLW = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 80f;
            EditorGUI.BeginChangeCheck();
            float val = EditorGUI.FloatField(new Rect(row.x, row.y, fieldW, row.height),
                new GUIContent(label), component == 0 ? v.x : v.y);
            if (EditorGUI.EndChangeCheck())
                hoProp.vector2Value = component == 0 ? new Vector2(val, v.y) : new Vector2(v.x, val);
            EditorGUIUtility.labelWidth = savedLW;
            bool canSet = editor != null && editor.terrain != null;
            EditorGUI.BeginDisabledGroup(!canSet);
            if (GUI.Button(new Rect(row.xMax - btnW * 2 - 2, row.y, btnW, row.height), "Auto"))
            {
                hoProp.vector2Value = component == 0
                    ? new Vector2(-1, hoProp.vector2Value.y)
                    : new Vector2(hoProp.vector2Value.x, -1);
                hoProp.serializedObject.ApplyModifiedProperties();
            }
            if (GUI.Button(new Rect(row.xMax - btnW, row.y, btnW, row.height), "Current"))
            {
                float h = editor.GetHeight(xProp.intValue, yProp.intValue);
                hoProp.vector2Value = component == 0
                    ? new Vector2(h, hoProp.vector2Value.y)
                    : new Vector2(hoProp.vector2Value.x, h);
                hoProp.serializedObject.ApplyModifiedProperties();
            }
            EditorGUI.EndDisabledGroup();
        }

        protected void DrawApplyButton(bool canApply, Action onApply)
        {
            var row = Row(10);
            string key = insertName + listIdx;
            bool showFeedback = appliedTimestamps.TryGetValue(key, out double t)
                && EditorApplication.timeSinceStartup - t < 1;
            EditorGUI.BeginDisabledGroup(!canApply);
            if (GUI.Button(row, showFeedback ? "Applied!" : "Apply"))
            {
                onApply();
                appliedTimestamps[key] = EditorApplication.timeSinceStartup;
            }
            EditorGUI.EndDisabledGroup();
        }
    }
#endif

    [Serializable]
    public abstract class TerrainInsert
    {
        public string name;

        public bool enabled     = true;
        public bool drawPreview = true;

        public int bakeResolution = 512;
    }

}
