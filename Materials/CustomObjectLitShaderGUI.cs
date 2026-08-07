#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class CustomObjectLitShaderGUI : ShaderGUI
    {
        static readonly GUIContent[] modeOptions =
        {
            new GUIContent("Opaque", "Solid surface without transparency."),
            new GUIContent("Alpha Clip", "Pixels below the Alpha Cutoff are cut out completely (foliage, fences, decals)."),
            new GUIContent("Transparent", "Alpha-blended see-through surface. Does not write depth."),
        };

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

            int mode = Mathf.Clamp((int) modeProp.floatValue, 0, modeOptions.Length - 1);

            EditorGUI.BeginChangeCheck();
            mode = EditorGUILayout.Popup(new GUIContent("Render Mode", "How the surface is drawn: solid, cutout, or alpha-blended."), mode, modeOptions);
            if (EditorGUI.EndChangeCheck())
            {
                modeProp.floatValue = mode;
                foreach (var obj in modeProp.targets)
                    ApplyRenderMode((Material) obj, (CustomObjectRenderMode) mode);
            }

            EditorGUILayout.Space(6);
            editor.TexturePropertySingleLine(new GUIContent("Base Map", "Albedo color texture, tinted by the color field."), baseMap, baseColor);
            DrawReadWriteWarning(baseMap);
            editor.TexturePropertySingleLine(new GUIContent("Roughness", "Roughness texture (white = rough, black = shiny). The slider adds extra smoothness on top of the texture."), roughnessMap, smoothnessProp);
            DrawReadWriteWarning(roughnessMap);
            editor.TexturePropertySingleLine(new GUIContent("Metallic", "Metallic texture (white = metal). The slider adds extra metalness on top of the texture."),  metallicMap, metallicProp);
            DrawReadWriteWarning(metallicMap);
            editor.TexturePropertySingleLine(new GUIContent("Normal", "Tangent-space normal map for surface detail."),    normalMap);
            DrawReadWriteWarning(normalMap);
            editor.TexturePropertySingleLine(new GUIContent("Emission", "Self-illumination: texture multiplied by the HDR color. Black color = no emission."),  emissionMap, emissionColor);
            DrawReadWriteWarning(emissionMap);
            editor.TextureScaleOffsetProperty(baseMap);

            EditorGUILayout.Space(6);
            if ((CustomObjectRenderMode) mode == CustomObjectRenderMode.AlphaClip)
                editor.ShaderProperty(cutoffProp, new GUIContent("Alpha Cutoff", "Pixels with base map alpha below this value are cut out."));
            editor.ShaderProperty(cullProp, new GUIContent("Cull Mode", "Which faces are drawn: Back = normal, Front = inside-out, Off = double-sided."));

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (BeginFeature(props, "Overlay", "Directional accumulation layer (snow, dust, moss) applied where the surface faces the overlay direction.", "_UseOverlay", "_OVERLAY"))
                DrawOverlay(editor, props);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (BeginFeature(props, "Liquid", "Animated rippling liquid surface. Uses the base map for color.", "_UseLiquid", "_LIQUID"))
                DrawLiquid(editor, props);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (BeginFeature(props, "Wind Sway", "Vertex animation that sways the mesh in the wind. Use the masks to keep bases and trunks planted.", "_UseWindSway", "_WIND_SWAY"))
                DrawWindSway(editor, props);
            EditorGUILayout.EndVertical();
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
            EditorGUILayout.LabelField("Ripples", EditorStyles.miniBoldLabel);
            editor.ShaderProperty(liquidTiling,       new GUIContent("Tiling", "Scale of the ripple pattern. Higher = smaller, denser ripples."));
            editor.ShaderProperty(liquidScroll1,      new GUIContent("Scroll 1 (XY)", "Drift direction and speed of the first ripple layer."));
            editor.ShaderProperty(liquidScroll2,      new GUIContent("Scroll 2 (XY)", "Drift direction and speed of the second ripple layer. Different directions make the surface shimmer."));
            editor.ShaderProperty(liquidWaveHeight,   new GUIContent("Wave Height", "How strongly the ripples bend the surface lighting."));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Surface", EditorStyles.miniBoldLabel);
            editor.ShaderProperty(liquidSmoothness,   new GUIContent("Smoothness", "Glossiness of the liquid surface. High values give sharp reflections."));
            editor.ShaderProperty(liquidFresnelPower, new GUIContent("Fresnel Power", "Falloff of the rim glow. Higher = glow hugs grazing angles only."));
            editor.ShaderProperty(liquidFresnelStr,   new GUIContent("Fresnel Strength", "Brightness of the rim glow at grazing angles."));
            EditorGUI.indentLevel--;
        }

        static void DrawWindSway(MaterialEditor editor, MaterialProperty[] props)
        {
            var windDirection       = FindProperty("_WindDirection", props);
            var windStrength        = FindProperty("_WindStrength", props);
            var windSpeed           = FindProperty("_WindSpeed", props);
            var windVariation       = FindProperty("_WindVariation", props);
            var windCrossSway       = FindProperty("_WindCrossSway", props);
            var windSwayAxis        = FindProperty("_WindSwayAxis", props);
            var windSwayMin         = FindProperty("_WindSwayMin", props);
            var windSwayMax         = FindProperty("_WindSwayMax", props);
            var windMaskExponent    = FindProperty("_WindMaskExponent", props);
            var windRadialInfluence = FindProperty("_WindRadialInfluence", props);
            var windRadialMin       = FindProperty("_WindRadialMin", props);
            var windRadialMax       = FindProperty("_WindRadialMax", props);
            var windRadialExponent  = FindProperty("_WindRadialExponent", props);

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Motion", EditorStyles.miniBoldLabel);
            editor.ShaderProperty(windDirection,       new GUIContent("Direction (World)", "World-space direction the wind blows in. The mesh sways back and forth along this."));
            editor.ShaderProperty(windStrength,        new GUIContent("Strength", "Maximum sway distance in meters."));
            editor.ShaderProperty(windSpeed,           new GUIContent("Speed", "How fast the sway oscillates."));
            editor.ShaderProperty(windVariation,       new GUIContent("Variation", "Adds a faster secondary wobble that differs across the object, breaking up uniform motion."));
            editor.ShaderProperty(windCrossSway,       new GUIContent("Cross Sway", "Adds sideways sway perpendicular to the wind direction, turning the flat back-and-forth into elliptical motion. Great for tree canopies."));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Height Mask", EditorStyles.miniBoldLabel);
            editor.ShaderProperty(windSwayAxis,        new GUIContent("Axis (Object)", "Object-space axis the mask ramps along, usually (0,1,0) so the base stays planted. Also serves as the trunk line for the radial ramp."));
            editor.ShaderProperty(windSwayMin,         new GUIContent("Min", "Distance along the axis where sway begins. Everything below stays still."));
            editor.ShaderProperty(windSwayMax,         new GUIContent("Max", "Distance along the axis where sway reaches full strength."));
            editor.ShaderProperty(windMaskExponent,    new GUIContent("Exponent", "Curve of the ramp between Min and Max. Higher keeps the lower part stiffer and concentrates motion at the top."));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Radial Ramp", EditorStyles.miniBoldLabel);
            editor.ShaderProperty(windRadialInfluence, new GUIContent("Influence", "How much distance from the trunk line scales the sway. 0 = off. At 1, geometry near the trunk stays connected while leaves far out sway fully."));
            editor.ShaderProperty(windRadialMin,       new GUIContent("Min", "Distance from the trunk line where sway begins, roughly the trunk radius."));
            editor.ShaderProperty(windRadialMax,       new GUIContent("Max", "Distance from the trunk line where sway reaches full strength, roughly the canopy radius."));
            editor.ShaderProperty(windRadialExponent,  new GUIContent("Exponent", "Curve of the radial ramp. Higher keeps the inner canopy stiffer."));
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
            EditorGUILayout.LabelField("Look", EditorStyles.miniBoldLabel);
            editor.TexturePropertySingleLine(new GUIContent("Map", "Overlay texture, tinted by the color. Shown where the surface faces the overlay direction."), overlayMap, overlayColor);
            DrawReadWriteWarning(overlayMap);
            editor.ShaderProperty(overlayTiling,      new GUIContent("Tiling", "Tiling multiplier for the overlay texture."));
            editor.ShaderProperty(overlaySmoothness,  new GUIContent("Smoothness", "Glossiness of the overlaid areas."));
            editor.ShaderProperty(overlayNormalBlend, new GUIContent("Normal Blend", "Bends the normals of overlaid areas toward the overlay direction, flattening their lighting like a smooth snow layer."));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Coverage", EditorStyles.miniBoldLabel);
            editor.ShaderProperty(overlayDirection,   new GUIContent("Direction", "World direction the overlay accumulates from, e.g. (0,1,0) for snow settling from above."));
            editor.ShaderProperty(overlayAmount,      new GUIContent("Coverage", "How far around the object the overlay reaches. Lower values cover more: -1 covers everything, 1 covers nothing."));
            editor.ShaderProperty(overlaySharpness,   new GUIContent("Edge Sharpness", "Width of the transition at the coverage edge. Small = crisp line, large = soft gradual blend."));
            editor.ShaderProperty(overlayIntensity,   new GUIContent("Intensity", "Overall blend strength of the overlay."));

            EditorGUILayout.Space(4);
            if (BeginFeature(props, "Fade", "Fades the overlay in along an axis, e.g. only above a certain height.", "_UseOverlayFade", "_OVERLAY_FADE"))
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
            editor.ShaderProperty(fadeAxis,        new GUIContent("Axis", "Direction the fade runs along, e.g. (0,1,0) to fade in with height."));
            editor.ShaderProperty(fadeMin,         new GUIContent("Min", "Position along the axis where the overlay is fully faded out."));
            editor.ShaderProperty(fadeMax,         new GUIContent("Max", "Position along the axis where the overlay is fully visible."));
            editor.ShaderProperty(fadeObjectSpace, new GUIContent("Object Space", "On: positions are measured in object space and move with the object. Off: measured in world space."));
            EditorGUI.indentLevel--;
        }

        static bool BeginFeature(MaterialProperty[] props, string label, string tooltip, string toggleName, string keyword)
        {
            var toggle = FindProperty(toggleName, props);
            EditorGUI.BeginChangeCheck();
            bool enabled = toggle.floatValue > 0.5f;
            enabled = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), enabled, EditorStyles.boldLabel);
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
            ApplyKeyword(material, "_WIND_SWAY", material.GetFloat("_UseWindSway") > 0.5f);
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