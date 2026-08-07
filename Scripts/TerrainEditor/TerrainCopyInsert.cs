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
            var dstTerrain = editor != null ? editor.terrain : null;
            {
                var row = Row();
                if (srcTerrain != null && dstTerrain != null && srcTerrain == dstTerrain)
                {
                    var warn = new GUIStyle(EditorStyles.miniBoldLabel);
                    warn.normal.textColor = new Color(1f, 0.45f, 0.15f);
                    EditorGUI.LabelField(row, "source = target terrain (copies onto itself!)", warn);
                }
                else
                {
                    EditorGUI.LabelField(row,
                        "pastes into: " + (dstTerrain != null ? dstTerrain.name : "?") + " (this component's terrain)",
                        EditorStyles.miniLabel);
                }
            }

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
                HalfIntSlider(row, row.x,           half, srcSizeXProp, 1, srcMax + 1, "w");
                HalfIntSlider(row, row.x + half + 2, half, srcSizeYProp, 1, srcMax + 1, "h");
                EditorGUIUtility.labelWidth = savedLW;
            }

            EditorGUI.LabelField(Row(10), "Destination", EditorStyles.boldLabel);
            DrawCoordinatePicker("Start",
                property.FindPropertyRelative("dstStartX"),
                property.FindPropertyRelative("dstStartY"));

            {
                var row = Row(10);
                float third = row.width / 3f;
                var mirrorXProp = property.FindPropertyRelative("mirrorX");
                var mirrorZProp = property.FindPropertyRelative("mirrorZ");
                EditorGUI.LabelField(new Rect(row.x, row.y, third, row.height), "Mirror", EditorStyles.boldLabel);
                mirrorXProp.boolValue = EditorGUI.ToggleLeft(
                    new Rect(row.x + third, row.y, third, row.height), "X", mirrorXProp.boolValue);
                mirrorZProp.boolValue = EditorGUI.ToggleLeft(
                    new Rect(row.x + third * 2f, row.y, third, row.height), "Z", mirrorZProp.boolValue);
            }

            EditorGUI.Slider(Row(10), property.FindPropertyRelative("blendFalloff"),
                0f, 1f, "blendFalloff");
            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("heightOffset"),
                new GUIContent("heightOffset"));
            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("copySnowmask"),
                new GUIContent("copy snowmask"));

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
            return line + (14 * (line + Spacing)) + 70f;
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
        public bool mirrorX;
        public bool mirrorZ;

        [Space(20)]
        [Range(0, 1)]
        public float blendFalloff = 0.2f;
        public float heightOffset = 0;

        public bool copySnowmask;
    }

}
