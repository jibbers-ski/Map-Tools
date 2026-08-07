using UnityEditor;
using UnityEngine;

namespace Jibbers.MapTools
{
    public class BasicSnowTerrainShaderGUI : ShaderGUI
    {
        static bool systemFoldout;

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
        {
            EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
            Prop(editor, props, "_BaseColor");
            Prop(editor, props, "_RockColor");
            Prop(editor, props, "_PowderMinColor");
            Prop(editor, props, "_PowderMaxColor");
            Prop(editor, props, "_MarkingTint");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Grooming Grooves", EditorStyles.boldLabel);
            Prop(editor, props, "_PisteTiling");
            Prop(editor, props, "_GrooveStrength");

            EditorGUILayout.Space(12);
            systemFoldout = EditorGUILayout.Foldout(systemFoldout, "System (managed by the terrain tools)", true);
            if (!systemFoldout)
                return;

            EditorGUILayout.HelpBox(
                "Assigned by the terrain editor and the map importer. Changing these by hand can break snow coverage, markings, powder and flow on this terrain.",
                MessageType.Warning);
            Tex(editor, props, "_SnowMask");
            Tex(editor, props, "_SnowMask2");
            Prop(editor, props, "_SnowMask4Channel");
            Prop(editor, props, "_ThirdFromAlpha");
            Prop(editor, props, "_FlowFromMask2");
            Prop(editor, props, "_PowderMaxHeight");
            Prop(editor, props, "_ThirdCoverageDepth");
            EditorGUILayout.Space(4);
            Prop(editor, props, "_DebugMode");
            Prop(editor, props, "_PaintView");
        }

        static void Prop(MaterialEditor editor, MaterialProperty[] props, string name)
        {
            var p = FindProperty(name, props, false);
            if (p != null)
                editor.ShaderProperty(p, p.displayName);
        }

        static void Tex(MaterialEditor editor, MaterialProperty[] props, string name)
        {
            var p = FindProperty(name, props, false);
            if (p != null)
                editor.TexturePropertySingleLine(new GUIContent(p.displayName), p);
        }
    }
}
