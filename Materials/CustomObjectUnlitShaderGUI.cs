#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class CustomObjectUnlitShaderGUI : ShaderGUI
    {
        static readonly string[] modeNames = { "Opaque", "Alpha Clip", "Transparent" };

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
        {
            var modeProp   = FindProperty("_RenderMode", props);
            var cutoffProp = FindProperty("_Cutoff", props);
            var cullProp   = FindProperty("_Cull", props);
            var baseMap    = FindProperty("_BaseMap", props);
            var baseColor  = FindProperty("_BaseColor", props);

            int mode = Mathf.Clamp((int) modeProp.floatValue, 0, modeNames.Length - 1);

            EditorGUI.BeginChangeCheck();
            mode = EditorGUILayout.Popup("Render Mode", mode, modeNames);
            if (EditorGUI.EndChangeCheck())
            {
                modeProp.floatValue = mode;
                foreach (var obj in modeProp.targets)
                    CustomObjectLitShaderGUI.ApplyRenderMode((Material) obj, (CustomObjectRenderMode) mode);
            }

            EditorGUILayout.Space(6);
            editor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
            editor.TextureScaleOffsetProperty(baseMap);

            EditorGUILayout.Space(6);
            if ((CustomObjectRenderMode) mode == CustomObjectRenderMode.AlphaClip)
                editor.ShaderProperty(cutoffProp, "Alpha Cutoff");
            editor.ShaderProperty(cullProp, "Cull Mode");
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            CustomObjectLitShaderGUI.ApplyRenderMode(material, (CustomObjectRenderMode) material.GetFloat("_RenderMode"));
        }
    }

}
#endif
