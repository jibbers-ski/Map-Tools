using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomPropertyDrawer(typeof(SkiLine))]
    public class SkiLineDrawer : PropertyDrawer
    {
        static int ElementIndex(SerializedProperty property)
        {
            var path = property.propertyPath;
            int open = path.LastIndexOf('[');
            int close = path.LastIndexOf(']');
            if (open >= 0 && close > open
                && int.TryParse(path.Substring(open + 1, close - open - 1), out int idx))
                return idx;
            return -1;
        }

        public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(pos, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float y = pos.y;
            float savedLW = EditorGUIUtility.labelWidth;

            GUIContent C(string l, string t) => new GUIContent(l, t);

            Rect Row()
            {
                var r = new Rect(pos.x, y, pos.width, line);
                y += line + 2;
                return r;
            }

            void Half(string prop, GUIContent gc, string prop2, GUIContent gc2, float lw)
            {
                var row = Row();
                float half = (row.width - 2) / 2f;
                EditorGUIUtility.labelWidth = lw;
                EditorGUI.PropertyField(new Rect(row.x, row.y, half, line),
                    property.FindPropertyRelative(prop), gc);
                EditorGUI.PropertyField(new Rect(row.x + half + 2, row.y, half, line),
                    property.FindPropertyRelative(prop2), gc2);
                EditorGUIUtility.labelWidth = savedLW;
            }

            {
                var row = Row();
                const float tW = 46f, pW = 60f;
                string nm = property.FindPropertyRelative("name").stringValue;
                int index = ElementIndex(property);
                string head = index >= 0
                    ? $"[{index}]  {(string.IsNullOrEmpty(nm) ? "Line" : nm)}"
                    : (string.IsNullOrEmpty(nm) ? "Line" : nm);
                property.isExpanded = EditorGUI.Foldout(
                    new Rect(row.x, row.y, row.width - tW - pW - 4, line), property.isExpanded, head, true);
                EditorGUIUtility.labelWidth = 38f;
                EditorGUI.PropertyField(new Rect(row.xMax - tW - pW - 2, row.y, pW, line),
                    property.FindPropertyRelative("drawPreview"),
                    C("show", "Draw this line's full preview (surface edges, ribs, features) in the Scene view."));
                EditorGUIUtility.labelWidth = 20f;
                EditorGUI.PropertyField(new Rect(row.xMax - tW, row.y, tW, line),
                    property.FindPropertyRelative("enabled"),
                    C("on", "Include this line when applying."));
                EditorGUIUtility.labelWidth = savedLW;
            }

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("name"), new GUIContent("Name"));

            EditorGUI.LabelField(Row(), "Shape", EditorStyles.boldLabel);
            Half("width", C("Width", "Width of the line surface in metres. Individual nodes can override it."),
                 "crossSectionDepth", C("Depth", "How deep the Cross-Section curve shapes the surface (m). 0 = flat."), 52f);
            EditorGUI.CurveField(Row(), property.FindPropertyRelative("crossSection"),
                MoreColors.JibbersOrange, new Rect(0, 0, 1, 1),
                C("Cross-Section", "Surface profile across the line, from one edge (left) to the other (right), scaled by Depth."));
            Half("sideFlatten", C("Flatten", "Fraction of each side over which cross-section and roll flatten out toward the edges — keeps the surface ridable."),
                 "autoBank", C("Auto Bank", "Banks the line through turns automatically: degrees of banking in a 20 m-radius turn, proportionally more in tighter turns, none on straights. Per-node roll is added on top."), 62f);

            EditorGUI.LabelField(Row(), "Blending", EditorStyles.boldLabel);
            Half("edgeBlend", C("Edge Blend", "Fraction of each side blended into the surrounding terrain."),
                 "edgeFalloff", C("Edge Curve", "Falloff exponent of the edge blend. Higher = sharper shoulder."), 68f);
            Half("endBlend", C("End Blend", "Distance (m) over which the line fades in and out at its ends."),
                 "bakeResolution", C("Detail", "Spline samples along the line. Raise for very long or twisty lines."), 68f);

            var nodesProp = property.FindPropertyRelative("nodes");
            float nodesH = EditorGUI.GetPropertyHeight(nodesProp, true);
            EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, nodesH), nodesProp,
                C("Nodes", "The spline points. Usually laid out in the Scene view via Edit Selected Line."), true);
            y += nodesH + 2;

            var featuresProp = property.FindPropertyRelative("features");
            float featuresH = EditorGUI.GetPropertyHeight(featuresProp, true);
            EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, featuresH), featuresProp,
                C("Features", "Kickers, tables, rollers and gaps placed along the line — additive bumps on top of the line surface."), true);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + 2;
            if (!property.isExpanded) return line;
            float h = line * 9;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("nodes"), true) + 2;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("features"), true) + 2;
            return h + 4;
        }
    }

    [CustomPropertyDrawer(typeof(SkiLineNode))]
    public class SkiLineNodeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;

            EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, line),
                property.FindPropertyRelative("position"), GUIContent.none);

            var row = new Rect(position.x, position.y + line + 1, position.width, line);
            float half = (row.width - 2) / 2f;
            float savedLW = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 32f;
            EditorGUI.Slider(new Rect(row.x, row.y, half, line),
                property.FindPropertyRelative("roll"), -60f, 60f,
                new GUIContent("roll", "Manual bank of the surface at this node (°). Added on top of the line's Auto Bank."));
            EditorGUIUtility.labelWidth = 44f;
            EditorGUI.PropertyField(new Rect(row.x + half + 2, row.y, half, line),
                property.FindPropertyRelative("widthOverride"),
                new GUIContent("width", "Overrides the line width at this node (m). -1 = use the line's Width."));
            EditorGUIUtility.labelWidth = savedLW;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight * 2 + 4;
    }

    [CustomPropertyDrawer(typeof(SkiLinePaintStripe))]
    public class SkiLinePaintStripeDrawer : PropertyDrawer
    {
        static readonly GUIContent[] orientationNames =
        {
            new GUIContent("Along Line", "The stripe runs along the feature's length."),
            new GUIContent("Across Line", "The stripe runs across the line at one point of the feature."),
        };
        public static readonly string[] ColorNames = { "Red", "Orange", "Gold", "Yellow", "Yellow-Green", "Lime", "Light Green", "Green", "Teal", "Cyan", "Light Blue", "Blue", "Dark Blue", "Purple", "Pink", "Magenta" };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float half = (position.width - 2) / 2f;
            float savedLW = EditorGUIUtility.labelWidth;

            var acrossProp = property.FindPropertyRelative("acrossLine");
            int orient = acrossProp.boolValue ? 1 : 0;
            int newOrient = EditorGUI.Popup(new Rect(position.x, position.y, half, line), orient, orientationNames);
            if (newOrient != orient) acrossProp.boolValue = newOrient == 1;

            EditorGUIUtility.labelWidth = 28f;
            EditorGUI.Slider(new Rect(position.x + half + 2, position.y, half, line),
                property.FindPropertyRelative("position"), 0f, 1f,
                new GUIContent("pos", "Along stripes: sideways position across the width. Across stripes: position along the feature (0 = start, 1 = end)."));

            float y2 = position.y + line + 1;
            EditorGUIUtility.labelWidth = 40f;
            EditorGUI.PropertyField(new Rect(position.x, y2, half, line),
                property.FindPropertyRelative("stripeWidth"),
                new GUIContent("width", "Stripe width in metres."));
            EditorGUIUtility.labelWidth = 32f;
            EditorGUI.Slider(new Rect(position.x + half + 2, y2, half, line),
                property.FindPropertyRelative("softness"), 0f, 1f,
                new GUIContent("soft", "Softness of the paint edge. 0 = hard line."));

            float y3 = y2 + line + 1;
            EditorGUIUtility.labelWidth = 52f;
            EditorGUI.Slider(new Rect(position.x, y3, half, line),
                property.FindPropertyRelative("opacity"), 0f, 1f,
                new GUIContent("opacity", "How strongly the marking is painted into the snow mask."));
            EditorGUIUtility.labelWidth = 38f;
            EditorGUI.Slider(new Rect(position.x + half + 2, y3, half, line),
                property.FindPropertyRelative("inset"), 0f, 0.45f,
                new GUIContent("inset", "Pulls across-stripes inward from the line edges (fraction of the width)."));

            float y4 = y3 + line + 1;
            var colorProp = property.FindPropertyRelative("colorIdx");
            colorProp.intValue = EditorGUI.Popup(new Rect(position.x, y4, position.width, line),
                Mathf.Clamp(colorProp.intValue, 0, ColorNames.Length - 1), ColorNames);

            EditorGUIUtility.labelWidth = savedLW;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight * 4 + 8;
    }

    [CustomPropertyDrawer(typeof(SkiLineFeature))]
    public class SkiLineFeatureDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float y = position.y;
            Rect Row() { var r = new Rect(position.x, y, position.width, line); y += line + 1; return r; }
            float savedLW = EditorGUIUtility.labelWidth;

            void HalfRow(SerializedProperty left, GUIContent leftLabel, SerializedProperty right, GUIContent rightLabel)
            {
                var row = Row();
                float half = (row.width - 2) / 2f;
                EditorGUIUtility.labelWidth = 52f;
                EditorGUI.PropertyField(new Rect(row.x, row.y, half, line), left, leftLabel);
                EditorGUI.PropertyField(new Rect(row.x + half + 2, row.y, half, line), right, rightLabel);
                EditorGUIUtility.labelWidth = savedLW;
            }

            {
                var row = Row();
                const float toggleW = 70f;
                string nm = property.FindPropertyRelative("name").stringValue;
                property.isExpanded = EditorGUI.Foldout(
                    new Rect(row.x, row.y, row.width - toggleW - 2, line),
                    property.isExpanded, string.IsNullOrEmpty(nm) ? "Feature" : nm, true);
                EditorGUIUtility.labelWidth = 52f;
                EditorGUI.PropertyField(new Rect(row.xMax - toggleW, row.y, toggleW, line),
                    property.FindPropertyRelative("enabled"), new GUIContent("enabled", "Include this feature when carving and painting."));
                EditorGUIUtility.labelWidth = savedLW;
            }

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("name"), new GUIContent("name"));

            var presets = SkiLineFeature.Presets;
            {
                var row = Row();
                float w = (row.width - (presets.Length - 1) * 2) / presets.Length;
                for (int c = 0; c < presets.Length; c++)
                {
                    var preset = presets[c];
                    if (GUI.Button(new Rect(row.x + c * (w + 2), row.y, w, line),
                        new GUIContent(preset.name, $"Replace this feature's shape and stripes with the built-in {preset.name} preset (keeps its position on the line).")))
                    {
                        var tmp = new SkiLineFeature
                        {
                            name = preset.name,
                            length = preset.length,
                            height = preset.height,
                            profile = preset.profile(),
                            paintStripes = new List<SkiLinePaintStripe>(preset.stripes()),
                        };
                        WriteFeature(tmp, property, includeStart: false);
                    }
                }
            }

            {
                var row = Row();
                const float btnW = 46f;
                var presetProp = property.FindPropertyRelative("preset");
                EditorGUI.PropertyField(new Rect(row.x, row.y, row.width - btnW * 2 - 4, line),
                    presetProp, GUIContent.none);
                var asset = presetProp.objectReferenceValue as SkiLineFeaturePreset;
                EditorGUI.BeginDisabledGroup(asset == null);
                if (GUI.Button(new Rect(row.xMax - btnW * 2 - 2, row.y, btnW, line),
                        new GUIContent("Load", "Load the assigned preset asset into this feature (keeps its position on the line)."))
                    && asset != null && asset.feature != null)
                    WriteFeature(asset.feature, property, includeStart: false);
                EditorGUI.EndDisabledGroup();
                if (GUI.Button(new Rect(row.xMax - btnW, row.y, btnW, line),
                        new GUIContent("Save", "Save this feature to the assigned preset asset, or create a new preset asset.")))
                    SaveToPresetAsset(property, asset);
            }

            HalfRow(property.FindPropertyRelative("start"),
                    new GUIContent("start", "Distance along the line where the feature begins (m). Drag the cone handle in the Scene view."),
                    property.FindPropertyRelative("lateralOffset"),
                    new GUIContent("lateral", "Sideways offset from the line centre (m). Drag the cube handle in the Scene view."));
            HalfRow(property.FindPropertyRelative("length"),
                    new GUIContent("length", "Length of the feature along the line (m)."),
                    property.FindPropertyRelative("width"),
                    new GUIContent("width", "Feature width (m). 0 = spans the full line width."));
            HalfRow(property.FindPropertyRelative("height"),
                    new GUIContent("height", "Peak height of the profile (m). Negative digs into the line (gaps)."),
                    property.FindPropertyRelative("sideBlend"),
                    new GUIContent("blend", "Side blend when the feature is narrower than the line (0-1)."));

            EditorGUI.CurveField(Row(), property.FindPropertyRelative("profile"),
                MoreColors.JibbersOrange, new Rect(0, 0, 1, 1),
                new GUIContent("profile", "Height profile from the feature's start (left) to its end (right), scaled by height."));

            var stripesProp = property.FindPropertyRelative("paintStripes");
            float stripesH = EditorGUI.GetPropertyHeight(stripesProp, true);
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, stripesH),
                stripesProp, new GUIContent("paint stripes"), true);

            EditorGUI.EndProperty();
        }

        static void WriteFeature(SkiLineFeature src, SerializedProperty property, bool includeStart)
        {
            property.FindPropertyRelative("name").stringValue = src.name;
            property.FindPropertyRelative("enabled").boolValue = true;
            if (includeStart) property.FindPropertyRelative("start").floatValue = src.start;
            property.FindPropertyRelative("length").floatValue = src.length;
            property.FindPropertyRelative("height").floatValue = src.height;
            property.FindPropertyRelative("lateralOffset").floatValue = src.lateralOffset;
            property.FindPropertyRelative("width").floatValue = src.width;
            property.FindPropertyRelative("sideBlend").floatValue = src.sideBlend;
            property.FindPropertyRelative("profile").animationCurveValue =
                src.profile != null ? new AnimationCurve(src.profile.keys) : new AnimationCurve();

            var stripesProp = property.FindPropertyRelative("paintStripes");
            int count = src.paintStripes != null ? src.paintStripes.Count : 0;
            stripesProp.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                var s = src.paintStripes[i];
                var el = stripesProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("acrossLine").boolValue = s.acrossLine;
                el.FindPropertyRelative("position").floatValue = s.position;
                el.FindPropertyRelative("stripeWidth").floatValue = s.stripeWidth;
                el.FindPropertyRelative("softness").floatValue = s.softness;
                el.FindPropertyRelative("opacity").floatValue = s.opacity;
                el.FindPropertyRelative("inset").floatValue = s.inset;
                el.FindPropertyRelative("colorIdx").intValue = s.colorIdx;
            }
        }

        static SkiLineFeature ReadFeature(SerializedProperty property)
        {
            var f = new SkiLineFeature
            {
                name = property.FindPropertyRelative("name").stringValue,
                enabled = true,
                start = property.FindPropertyRelative("start").floatValue,
                length = property.FindPropertyRelative("length").floatValue,
                height = property.FindPropertyRelative("height").floatValue,
                lateralOffset = property.FindPropertyRelative("lateralOffset").floatValue,
                width = property.FindPropertyRelative("width").floatValue,
                sideBlend = property.FindPropertyRelative("sideBlend").floatValue,
                profile = property.FindPropertyRelative("profile").animationCurveValue,
                preset = null,
                paintStripes = new List<SkiLinePaintStripe>(),
            };
            var stripesProp = property.FindPropertyRelative("paintStripes");
            for (int i = 0; i < stripesProp.arraySize; i++)
            {
                var el = stripesProp.GetArrayElementAtIndex(i);
                f.paintStripes.Add(new SkiLinePaintStripe
                {
                    acrossLine = el.FindPropertyRelative("acrossLine").boolValue,
                    position = el.FindPropertyRelative("position").floatValue,
                    stripeWidth = el.FindPropertyRelative("stripeWidth").floatValue,
                    softness = el.FindPropertyRelative("softness").floatValue,
                    opacity = el.FindPropertyRelative("opacity").floatValue,
                    inset = el.FindPropertyRelative("inset").floatValue,
                    colorIdx = el.FindPropertyRelative("colorIdx").intValue,
                });
            }
            return f;
        }

        static void SaveToPresetAsset(SerializedProperty property, SkiLineFeaturePreset asset)
        {
            // Read synchronously while the SerializedProperty is valid; defer the
            // modal dialog + AssetDatabase work so it never runs inside OnGUI
            // (a modal dialog mid-IMGUI corrupts the property-scope stack).
            var feature = ReadFeature(property);
            var so = property.serializedObject;
            string propPath = property.propertyPath;

            EditorApplication.delayCall += () =>
            {
                if (asset != null)
                {
                    Undo.RecordObject(asset, "Update Feature Preset");
                    asset.feature = feature;
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                    return;
                }

                string path = EditorUtility.SaveFilePanelInProject("Save Feature Preset",
                    feature.name + "Preset", "asset", "Save this feature as a reusable preset asset.");
                if (string.IsNullOrEmpty(path)) return;

                var newAsset = ScriptableObject.CreateInstance<SkiLineFeaturePreset>();
                newAsset.feature = feature;
                AssetDatabase.CreateAsset(newAsset, path);
                AssetDatabase.SaveAssets();

                if (so != null && so.targetObject != null)
                {
                    so.Update();
                    var p = so.FindProperty(propPath);
                    var presetProp = p != null ? p.FindPropertyRelative("preset") : null;
                    if (presetProp != null)
                    {
                        presetProp.objectReferenceValue = newAsset;
                        so.ApplyModifiedProperties();
                    }
                }
                EditorGUIUtility.PingObject(newAsset);
            };
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + 1;
            if (!property.isExpanded) return line + 2;
            float h = line * 8 + 4;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("paintStripes"), true) + 4;
            return h;
        }
    }
#endif

    [Serializable]
    public class SkiLineNode
    {
        public Vector3 position;
        public float widthOverride = -1f;
        public float roll;
    }

    [Serializable]
    public class SkiLinePaintStripe
    {
        public bool acrossLine;
        public float position = 0.5f;
        public float stripeWidth = 0.6f;
        public float softness = 0.4f;
        public float opacity = 0.7f;
        public float inset = 0f;
        public int colorIdx = 11;
    }

    [Serializable]
    public class SkiLineFeature
    {
        public string name = "Feature";
        public bool enabled = true;
        public SkiLineFeaturePreset preset;
        public float start = 20f;
        public float length = 12f;
        public float height = 3f;
        public float lateralOffset = 0f;
        public float width = 0f;
        [Range(0, 1)] public float sideBlend = 0.5f;
        public AnimationCurve profile = KickerProfile();
        public List<SkiLinePaintStripe> paintStripes = new List<SkiLinePaintStripe>();

        public class Preset
        {
            public string name;
            public float length;
            public float height;
            public Func<AnimationCurve> profile;
            public Func<SkiLinePaintStripe[]> stripes;
        }

        public static readonly Preset[] Presets =
        {
            new Preset { name = "Kicker", length = 12f, height = 3f, profile = KickerProfile, stripes = () => new[]
                {
                    new SkiLinePaintStripe { acrossLine = false, position = 0.06f, stripeWidth = 0.5f, softness = 0.4f, opacity = 0.7f, colorIdx = 11 },
                    new SkiLinePaintStripe { acrossLine = false, position = 0.94f, stripeWidth = 0.5f, softness = 0.4f, opacity = 0.7f, colorIdx = 11 },
                    new SkiLinePaintStripe { acrossLine = true,  position = 1f, inset = 0.06f, stripeWidth = 0.5f, softness = 0.4f, opacity = 0.85f, colorIdx = 0 },
                } },
            new Preset { name = "Table", length = 18f, height = 2.5f, profile = TableProfile, stripes = () => new[]
                {
                    new SkiLinePaintStripe { acrossLine = false, position = 0.06f, stripeWidth = 0.5f, softness = 0.4f, opacity = 0.7f, colorIdx = 11 },
                    new SkiLinePaintStripe { acrossLine = false, position = 0.94f, stripeWidth = 0.5f, softness = 0.4f, opacity = 0.7f, colorIdx = 11 },
                } },
            new Preset { name = "Roller", length = 10f, height = 1.2f, profile = RollerProfile, stripes = () => new SkiLinePaintStripe[0] },
            new Preset { name = "Gap", length = 8f, height = -3f, profile = RollerProfile, stripes = () => new[]
                {
                    new SkiLinePaintStripe { acrossLine = true, position = 1f, stripeWidth = 0.6f, softness = 0.4f, opacity = 0.85f, colorIdx = 0 },
                } },
            new Preset { name = "Landing", length = 14f, height = 0f, profile = FlatProfile, stripes = () => new[]
                {
                    new SkiLinePaintStripe { acrossLine = true, position = 0.15f, inset = 0.08f, stripeWidth = 0.6f, softness = 0.4f, opacity = 0.85f, colorIdx = 0 },
                    new SkiLinePaintStripe { acrossLine = true, position = 0.5f,  inset = 0.08f, stripeWidth = 0.6f, softness = 0.4f, opacity = 0.85f, colorIdx = 0 },
                    new SkiLinePaintStripe { acrossLine = true, position = 0.85f, inset = 0.08f, stripeWidth = 0.6f, softness = 0.4f, opacity = 0.85f, colorIdx = 0 },
                } },
        };

        public static AnimationCurve KickerProfile() => new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.5f, 0.3f, 1.1f, 1.1f),
            new Keyframe(1f, 1f, 2.4f, 0f));

        public static AnimationCurve TableProfile() => new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3f),
            new Keyframe(0.25f, 1f, 0f, 0f),
            new Keyframe(0.75f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -3f, 0f));

        public static AnimationCurve RollerProfile() => new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.5f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, 0f, 0f));

        public static AnimationCurve FlatProfile() => AnimationCurve.Constant(0, 1, 0);
    }

    [Serializable]
    public class SkiLine
    {
        public string name = "New Line";
        public bool enabled = true;
        public bool drawPreview = true;

        public float width = 14f;
        public AnimationCurve crossSection = AnimationCurve.Constant(0, 1, 0);
        public float crossSectionDepth = 0f;
        [Range(0, 0.5f)] public float sideFlatten = 0.15f;
        [Range(0f, 60f)] public float autoBank = 0f;

        [Range(0, 0.5f)] public float edgeBlend = 0.25f;
        [Range(0.25f, 4f)] public float edgeFalloff = 1f;
        public float endBlend = 10f;

        public int bakeResolution = 1024;

        public List<SkiLineNode> nodes = new List<SkiLineNode>();
        public List<SkiLineFeature> features = new List<SkiLineFeature>();

        [NonSerialized] public SkiLineBake bake;
        [NonSerialized] public int bakeHash;
        [NonSerialized] public SkiLinePreview preview;
    }

    public class SkiLinePreview
    {
        public Vector3[] center;
        public Vector3[] basePts;
        public Vector3[] left;
        public Vector3[] right;
        public Vector3[][] ribs;
        public Vector3[][] featureSpans;
        public string[] featureNames;
        public Vector3[] featureLabelPos;
    }

}
