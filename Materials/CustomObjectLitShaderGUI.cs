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
            var modeProp        = FindProperty("_RenderMode", props);
            var cutoffProp      = FindProperty("_Cutoff", props);
            var cullProp        = FindProperty("_Cull", props);
            var baseMap         = FindProperty("_BaseMap", props);
            var baseColor       = FindProperty("_BaseColor", props);
            var roughnessMap    = FindProperty("_RoughnessMap", props);
            var smoothnessProp  = FindProperty("_Smoothness", props);
            var metallicMap     = FindProperty("_MetallicMap", props);
            var metallicProp    = FindProperty("_Metallic", props);
            var normalMap       = FindProperty("_NormalMap", props);
            var emissionMap     = FindProperty("_EmissionMap", props);
            var emissionColor   = FindProperty("_EmissionColor", props);

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
            DrawReadWriteWarning(baseMap);
            editor.TexturePropertySingleLine(new GUIContent("Roughness"), roughnessMap, smoothnessProp);
            DrawReadWriteWarning(roughnessMap);
            editor.TexturePropertySingleLine(new GUIContent("Metallic"),  metallicMap, metallicProp);
            DrawReadWriteWarning(metallicMap);
            editor.TexturePropertySingleLine(new GUIContent("Normal"),    normalMap);
            DrawReadWriteWarning(normalMap);
            editor.TexturePropertySingleLine(new GUIContent("Emission"),  emissionMap, emissionColor);
            DrawReadWriteWarning(emissionMap);
            editor.TextureScaleOffsetProperty(baseMap);

            EditorGUILayout.Space(6);
            if ((CustomObjectRenderMode) mode == CustomObjectRenderMode.AlphaClip)
                editor.ShaderProperty(cutoffProp, "Alpha Cutoff");
            editor.ShaderProperty(cullProp, "Cull Mode");

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);
            if (BeginFeature(props, "Overlay", "_UseOverlay", "_OVERLAY"))
                DrawOverlay(editor, props);
            if (BeginFeature(props, "Liquid", "_UseLiquid", "_LIQUID"))
                DrawLiquid(editor, props);
        }

        static void DrawLiquid(MaterialEditor editor, MaterialProperty[] props)
        {
            var liquidTiling       = FindProperty("_LiquidTiling", props);
            var liquidScroll1      = FindProperty("_LiquidScroll1", props);
            var liquidScroll2      = FindProperty("_LiquidScroll2", props);
            var liquidWaveHeight   = FindProperty("_LiquidWaveHeight", props);
            var liquidSmoothness   = FindProperty("_LiquidSmoothness", props);
            var liquidFresnelPower = FindProperty("_LiquidFresnelPower", props);
            var liquidFresnelStr   = FindProperty("_LiquidFresnelStrength", props);

            EditorGUI.indentLevel++;
            editor.ShaderProperty(liquidTiling,       "Tiling");
            editor.ShaderProperty(liquidScroll1,      "Scroll 1 (XY)");
            editor.ShaderProperty(liquidScroll2,      "Scroll 2 (XY)");
            editor.ShaderProperty(liquidWaveHeight,   "Wave Height");
            editor.ShaderProperty(liquidSmoothness,   "Smoothness");
            editor.ShaderProperty(liquidFresnelPower, "Fresnel Power");
            editor.ShaderProperty(liquidFresnelStr,   "Fresnel Strength");
            EditorGUI.indentLevel--;
        }

        static void DrawOverlay(MaterialEditor editor, MaterialProperty[] props)
        {
            var overlayMap         = FindProperty("_OverlayMap", props);
            var overlayColor       = FindProperty("_OverlayColor", props);
            var overlayDirection   = FindProperty("_OverlayDirection", props);
            var overlayTiling      = FindProperty("_OverlayTiling", props);
            var overlayIntensity   = FindProperty("_OverlayIntensity", props);
            var overlayAmount      = FindProperty("_OverlayAmount", props);
            var overlaySharpness   = FindProperty("_OverlaySharpness", props);
            var overlaySmoothness  = FindProperty("_OverlaySmoothness", props);
            var overlayNormalBlend = FindProperty("_OverlayNormalBlend", props);

            EditorGUI.indentLevel++;
            editor.TexturePropertySingleLine(new GUIContent("Map"), overlayMap, overlayColor);
            DrawReadWriteWarning(overlayMap);
            editor.ShaderProperty(overlayDirection,   "Direction");
            editor.ShaderProperty(overlayTiling,      "Tiling");
            editor.ShaderProperty(overlayIntensity,   "Intensity");
            editor.ShaderProperty(overlayAmount,      "Coverage");
            editor.ShaderProperty(overlaySharpness,   "Edge Sharpness");
            editor.ShaderProperty(overlaySmoothness,  "Smoothness");
            editor.ShaderProperty(overlayNormalBlend, "Normal Blend");

            EditorGUILayout.Space(4);
            if (BeginFeature(props, "Fade", "_UseOverlayFade", "_OVERLAY_FADE"))
                DrawOverlayFade(editor, props);
            EditorGUI.indentLevel--;
        }

        static void DrawOverlayFade(MaterialEditor editor, MaterialProperty[] props)
        {
            var fadeAxis        = FindProperty("_OverlayFadeAxis", props);
            var fadeMin         = FindProperty("_OverlayFadeMin", props);
            var fadeMax         = FindProperty("_OverlayFadeMax", props);
            var fadeObjectSpace = FindProperty("_OverlayFadeObjectSpace", props);

            EditorGUI.indentLevel++;
            editor.ShaderProperty(fadeAxis,        "Axis");
            editor.ShaderProperty(fadeMin,         "Min");
            editor.ShaderProperty(fadeMax,         "Max");
            editor.ShaderProperty(fadeObjectSpace, "Object Space");
            EditorGUI.indentLevel--;
        }

        static bool BeginFeature(MaterialProperty[] props, string label, string toggleName, string keyword)
        {
            var toggle = FindProperty(toggleName, props);
            EditorGUI.BeginChangeCheck();
            bool enabled = toggle.floatValue > 0.5f;
            enabled = EditorGUILayout.Toggle(label, enabled);
            if (EditorGUI.EndChangeCheck())
            {
                toggle.floatValue = enabled ? 1f : 0f;
                foreach (var obj in toggle.targets)
                    ApplyKeyword((Material) obj, keyword, enabled);
            }
            return enabled;
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            ApplyRenderMode(material, (CustomObjectRenderMode) material.GetFloat("_RenderMode"));
            ApplyKeyword(material, "_OVERLAY", material.GetFloat("_UseOverlay") > 0.5f);
            ApplyKeyword(material, "_OVERLAY_FADE", material.GetFloat("_UseOverlayFade") > 0.5f);
            ApplyKeyword(material, "_LIQUID", material.GetFloat("_UseLiquid") > 0.5f);
        }

        public static void DrawReadWriteWarning(MaterialProperty texProp)
        {
            var tex = texProp?.textureValue as Texture2D;
            if (tex == null) return;

            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.isReadable) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"'{tex.name}' has Read/Write disabled and will be exported as a blank texture.", MessageType.Warning);
            if (GUILayout.Button("Fix", GUILayout.ExpandHeight(true), GUILayout.Width(50)))
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void ApplyKeyword(Material mat, string keyword, bool enabled)
        {
            if (enabled) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
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
