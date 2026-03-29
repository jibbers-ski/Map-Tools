using System;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomPropertyDrawer(typeof(TerrainMeshInsert))]
    public class TerrainMeshInsertDrawer : TerrainInsertDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!BeginInsertGUI(position, property, label)) return;

            DrawNameRow(property);

            EditorGUI.PropertyField(Row(10), property.FindPropertyRelative("mesh"), new GUIContent("mesh"));

            var centerXProp = property.FindPropertyRelative("centerX");
            var centerYProp = property.FindPropertyRelative("centerY");

            DrawCoordinatePicker("Center", centerXProp, centerYProp);

            EditorGUI.PropertyField(Row(10), property.FindPropertyRelative("rotation"),
                new GUIContent("rotation"));

            EditorGUI.Slider(Row(), property.FindPropertyRelative("scale"),
                0.01f, 10000f, "scale");

            DrawHeightOverride(property.FindPropertyRelative("heightOffset"),
                "heightOffset", centerXProp, centerYProp);

            EditorGUI.Slider(Row(), property.FindPropertyRelative("blendFalloff"),
                0f, 1f, "blendFalloff");

            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("bakeResolution"),
                new GUIContent("bakeResolution"));

            DrawApplyButton(
                editor != null && editor.terrain != null
                    && listIdx >= 0
                    && editor.meshInserts != null && listIdx < editor.meshInserts.Count
                    && editor.meshInserts[listIdx].mesh != null,
                () => editor.BuildMesh(editor.meshInserts[listIdx]));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            return line + (12 * (line + Spacing)) + 50f;
        }
    }
#endif

    [Serializable]
    public class TerrainMeshInsert : TerrainInsert
    {
        public TerrainMeshInsert() { name = "New Mesh"; }

        public Mesh mesh;

        public int centerX;
        public int centerY;

        public Vector3 rotation  = Vector3.zero;
        public float   scale     = 1f;
        public float heightOffset = -1f;
        public float blendFalloff = 0.1f;
    }

}
