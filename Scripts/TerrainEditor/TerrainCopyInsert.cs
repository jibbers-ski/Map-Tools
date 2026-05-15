using System;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomPropertyDrawer(typeof(TerrainCopyInsert))]
    public class TerrainCopyInsertDrawer : TerrainInsertDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!BeginInsertGUI(position, property, label)) return;

            DrawNameRow(property);

            EditorGUI.PropertyField(Row(10), property.FindPropertyRelative("sourceTerrain"),
                new GUIContent("source terrain"));

            var srcTerrainProp = property.FindPropertyRelative("sourceTerrain");
            var srcTerrain = srcTerrainProp.objectReferenceValue as Terrain;
            int srcMax = srcTerrain != null
                ? srcTerrain.terrainData.heightmapResolution - 1
                : 2048;

            var srcStartXProp = property.FindPropertyRelative("srcStartX");
            var srcStartYProp = property.FindPropertyRelative("srcStartY");
            var srcSizeXProp  = property.FindPropertyRelative("srcSizeX");
            var srcSizeYProp  = property.FindPropertyRelative("srcSizeY");

            EditorGUI.LabelField(Row(10), "Source Region", EditorStyles.boldLabel);
            {
                var row = Row();
                float half = (row.width - 2) / 2f;
                float savedLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 16f;
                HalfIntSlider(row, row.x,           half, srcStartXProp, 0, srcMax, "x");
                HalfIntSlider(row, row.x + half + 2, half, srcStartYProp, 0, srcMax, "y");
                EditorGUIUtility.labelWidth = savedLW;
            }
            {
                var row = Row();
                float half = (row.width - 2) / 2f;
                float savedLW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 30f;
                HalfIntSlider(row, row.x,           half, srcSizeXProp, 1, srcMax, "w");
                HalfIntSlider(row, row.x + half + 2, half, srcSizeYProp, 1, srcMax, "h");
                EditorGUIUtility.labelWidth = savedLW;
            }

            EditorGUI.LabelField(Row(10), "Destination", EditorStyles.boldLabel);
            DrawCoordinatePicker("Start",
                property.FindPropertyRelative("dstStartX"),
                property.FindPropertyRelative("dstStartY"));

            EditorGUI.Slider(Row(10), property.FindPropertyRelative("blendFalloff"),
                0f, 1f, "blendFalloff");
            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("heightOffset"),
                new GUIContent("heightOffset"));

            DrawApplyButton(
                editor != null && editor.terrain != null
                    && listIdx >= 0
                    && editor.copyInserts != null && listIdx < editor.copyInserts.Count
                    && editor.copyInserts[listIdx].sourceTerrain != null,
                () => editor.BuildCopy(editor.copyInserts[listIdx]));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            return line + (11 * (line + Spacing)) + 60f;
        }
    }
#endif

    [Serializable]
    public class TerrainCopyInsert : TerrainInsert
    {
        public TerrainCopyInsert() { name = "New Copy"; }

        public Terrain sourceTerrain;

        [Space(20)]
        public int srcStartX;
        public int srcStartY;
        public int srcSizeX = 100;
        public int srcSizeY = 100;

        [Space(20)]
        public int dstStartX;
        public int dstStartY;

        [Space(20)]
        [Range(0, 1)]
        public float blendFalloff = 0.2f;
        public float heightOffset = 0;
    }

}
