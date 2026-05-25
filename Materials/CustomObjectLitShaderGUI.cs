#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class CustomObjectLitShaderGUI : ShaderGUI
    {
        static readonly string[] modeNames = { "Opaque", "Alpha Clip", "Transparent" };

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
        {
            var modeProp    = FindProperty("_RenderMode", props);
            var cutoffProp  = FindProperty("_Cutoff", props);
            var cullProp    = FindProperty("_Cull", props);
            var baseMap     = FindProperty("_BaseMap", props);
            var baseColor   = FindProperty("_BaseColor", props);
            var roughness   = FindProperty("_RoughnessMap", props);
            var metallic    = FindProperty("_MetallicMap", props);
            var normalMap   = FindProperty("_NormalMap", props);

            int mode = Mathf.Clamp((int) modeProp.floatValue, 0, modeNames.Length - 1);

            EditorGUI.BeginChangeCheck();
            mode = EditorGUILayout.Popup("Render Mode", mode, modeNames);
            if (EditorGUI.EndChangeCheck())
            {
                modeProp.floatValue = mode;
                foreach (var obj in modeProp.targets)
                    ApplyRenderMode((Material) obj, (CustomObjectRenderMode) mode);
            }

            EditorGUILayout.Space(6);
            editor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
            editor.TexturePropertySingleLine(new GUIContent("Roughness"), roughness);
            editor.TexturePropertySingleLine(new GUIContent("Metallic"),  metallic);
            editor.TexturePropertySingleLine(new GUIContent("Normal"),    normalMap);
            editor.TextureScaleOffsetProperty(baseMap);

            EditorGUILayout.Space(6);
            if ((CustomObjectRenderMode) mode == CustomObjectRenderMode.AlphaClip)
                editor.ShaderProperty(cutoffProp, "Alpha Cutoff");
            editor.ShaderProperty(cullProp, "Cull Mode");
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            ApplyRenderMode(material, (CustomObjectRenderMode) material.GetFloat("_RenderMode"));
        }

        public static void ApplyRenderMode(Material mat, CustomObjectRenderMode mode)
        {
            switch (mode)
            {
                case CustomObjectRenderMode.Opaque:
                    mat.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1f);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Geometry;
                    break;
                case CustomObjectRenderMode.AlphaClip:
                    mat.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.AlphaTest;
                    break;
                case CustomObjectRenderMode.Transparent:
                    mat.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }
        }
    }

}
#endif
