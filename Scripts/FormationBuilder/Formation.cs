using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;

    [CustomPropertyDrawer(typeof(Formation))]
    public class FormationDrawer : PropertyDrawer
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

            var builder = property.serializedObject.targetObject as FormationBuilder;
            int index = ElementIndex(property);

            GUIContent C(string l, string t) => new GUIContent(l, t);

            Rect Row()
            {
                var r = new Rect(pos.x, y, pos.width, line);
                y += line + 2;
                return r;
            }

            void Field(string prop, GUIContent gc, float lw)
            {
                EditorGUIUtility.labelWidth = lw;
                EditorGUI.PropertyField(Row(), property.FindPropertyRelative(prop), gc);
                EditorGUIUtility.labelWidth = savedLW;
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

            bool Section(string title, string uiProp, string tip)
            {
                var sp = property.FindPropertyRelative(uiProp);
                sp.boolValue = EditorGUI.Foldout(Row(), sp.boolValue, C(title, tip), true, EditorStyles.foldout);
                return sp.boolValue;
            }

            bool ToggleSection(string title, string uiProp, string enableProp, string tip)
            {
                var row = Row();
                var enP = property.FindPropertyRelative(enableProp);
                enP.boolValue = EditorGUI.Toggle(new Rect(row.x, row.y, 16f, line), enP.boolValue);
                var sp = property.FindPropertyRelative(uiProp);
                sp.boolValue = EditorGUI.Foldout(new Rect(row.x + 16f, row.y, row.width - 16f, line),
                    sp.boolValue, C(title, tip), true, EditorStyles.foldout);
                return sp.boolValue;
            }

            {
                var row = Row();
                const float tW = 46f, pW = 60f;
                string nm = property.FindPropertyRelative("name").stringValue;
                string head = index >= 0
                    ? $"[{index}]  {(string.IsNullOrEmpty(nm) ? "Formation" : nm)}"
                    : (string.IsNullOrEmpty(nm) ? "Formation" : nm);
                property.isExpanded = EditorGUI.Foldout(
                    new Rect(row.x, row.y, row.width - tW - pW - 4, line), property.isExpanded, head, true);
                EditorGUIUtility.labelWidth = 38f;
                EditorGUI.PropertyField(new Rect(row.xMax - tW - pW - 2, row.y, pW, line),
                    property.FindPropertyRelative("drawPreview"), C("show", "Draw this formation's outline in the Scene view."));
                EditorGUIUtility.labelWidth = 20f;
                EditorGUI.PropertyField(new Rect(row.xMax - tW, row.y, tW, line),
                    property.FindPropertyRelative("enabled"), C("on", "Include this formation when applying."));
                EditorGUIUtility.labelWidth = savedLW;
            }

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.PropertyField(Row(), property.FindPropertyRelative("name"), new GUIContent("Name"));

            {
                var row = Row();
                var presets = Formation.Presets;
                float w = (row.width - (presets.Length - 1) * 2) / presets.Length;
                for (int c = 0; c < presets.Length; c++)
                {
                    if (GUI.Button(new Rect(row.x + c * (w + 2), row.y, w, line),
                        new GUIContent(presets[c].name, $"Replace this formation's settings with the {presets[c].name} preset and fit it to the marked area.")))
                    {
                        var tmp = new Formation();
                        presets[c].apply(tmp);
                        tmp.name = presets[c].name;
                        WriteShape(property, tmp, setName: true);
                        FitSerialized(property, builder, index);
                    }
                }
            }

            {
                var row = Row();
                const float btnW = 46f;
                var presetProp = property.FindPropertyRelative("presetAsset");
                EditorGUI.PropertyField(new Rect(row.x, row.y, row.width - btnW * 2 - 4, line),
                    presetProp, GUIContent.none);
                var asset = presetProp.objectReferenceValue as FormationPreset;
                EditorGUI.BeginDisabledGroup(asset == null || asset.formation == null);
                if (GUI.Button(new Rect(row.xMax - btnW * 2 - 2, row.y, btnW, line),
                        new GUIContent("Load", "Load the assigned preset asset into this formation (keeps the marked area)."))
                    && asset != null && asset.formation != null)
                    WriteShape(property, asset.formation, setName: true);
                EditorGUI.EndDisabledGroup();
                if (GUI.Button(new Rect(row.xMax - btnW, row.y, btnW, line),
                        new GUIContent("Save", "Save this formation's settings to the assigned preset asset, or create a new preset asset.")))
                {
                    var shape = ReadShape(property);
                    var b = builder; int idx = index; var existing = asset;
                    EditorApplication.delayCall += () => SaveDeferred(shape, existing, b, idx);
                }
            }

            {
                var row = Row();
                var areaProp = property.FindPropertyRelative("area");
                int pts = areaProp.arraySize;
                EditorGUI.LabelField(new Rect(row.x, row.y, row.width - 80f, line),
                    $"Area: {pts} point{(pts == 1 ? "" : "s")}" + (pts < 3 ? "  (draw ≥3 on terrain)" : ""),
                    pts < 3 ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(pts == 0);
                if (GUI.Button(new Rect(row.xMax - 78f, row.y, 78f, line), "Clear Area"))
                    areaProp.arraySize = 0;
                EditorGUI.EndDisabledGroup();
            }

            if (Section("Shape", "uiShape", "The base heightmap shape: a dome plus procedural noise."))
            {
                Half("blendMode", C("Mode", "Add = offset on top of the existing ground. Raise = keep whichever is higher, so the formation grows out of the terrain. Carve = keep whichever is lower (craters/canyons — use negative Height). Set = replace the ground with the formation surface. Raise/Carve/Set build from the marked area's rim height plus Base Y."),
                     "baseHeight", C("Base Y", "Moves the whole formation up or down (m). In Add mode an extra offset; in Raise/Carve/Set the offset from the area's rim height."), 60f);
                Half("noiseType", C("Style", "Noise pattern. Smooth = rolling hills, Ridged = sharp mountain crests, Billow = lumpy blobs."),
                     "height", C("Height", "Peak height added above the existing terrain, in metres."), 44f);
                Half("noiseScale", C("Feature Size", "Size of the largest noise features, in metres. Bigger = broader bumps."),
                     "noiseHeight", C("Roughness", "Amount of noise detail added on top of the dome, in metres."), 78f);
                Half("octaves", C("Detail", "Noise octaves. Higher = finer detail, auto-limited by heightmap resolution to prevent spikes."),
                     "edgeFalloff", C("Edge Blend", "Distance (m) over which the formation fades into the surrounding terrain at the boundary."), 52f);
                Half("smooth", C("Smoothing", "Blurs the shape to round off hard edges. 0 = off."),
                     "smoothIterations", C("Smooth Steps", "Number of smoothing passes."), 66f);
                EditorGUI.BeginDisabledGroup(builder == null || property.FindPropertyRelative("area").arraySize < 3);
                if (GUI.Button(Row(), new GUIContent("Fit to Marked Area", "Rescale Height, Feature Size, Peak Width and Edge Blend to match the size of the drawn area.")))
                    FitSerialized(property, builder, index);
                EditorGUI.EndDisabledGroup();
                if (Section("Advanced", "uiShapeAdv", "Fine noise and blend controls."))
                {
                    EditorGUI.CurveField(Row(), property.FindPropertyRelative("domeProfile"),
                        MoreColors.JibbersOrange, new Rect(0, 0, 1, 1),
                        new GUIContent("Profile", "Dome cross-section from edge (left) to peak (right)."));
                    Field("domeReach", C("Peak Width", "Distance (m) from the area edge to where the dome reaches full height. Fit-to-Area sets this to the area radius."), 80f);
                    Half("lacunarity", C("Lacunarity", "Frequency multiplier between octaves. 2 is standard."),
                         "gain", C("Gain", "Amplitude falloff between octaves. Lower = smoother."), 70f);
                    Half("warp", C("Warp", "Domain warp. Distorts the noise for more natural, flowing shapes."),
                         "noiseFollowsDome", C("On Peak", "Concentrates noise toward the peak (1) vs spreading it evenly (0)."), 52f);
                    Field("seed", C("Seed", "Random seed. Change for a different noise pattern."), 80f);
                }
            }

            if (ToggleSection("Thermal Erosion", "uiThermal", "thermalEnabled",
                "Slumps steep slopes down to the angle of repose — rounds peaks and builds scree slopes."))
            {
                Field("thermalStrength", C("Amount", "How strongly material slumps down slopes each step."), 80f);
                Field("thermalIterations", C("Iterations", "Number of thermal-erosion steps. More = more rounding."), 80f);
                Field("thermalRepose", C("Slope Angle", "Angle of repose (°). Slopes steeper than this slump until they reach it."), 80f);
            }

            if (ToggleSection("Hydraulic Erosion", "uiHydraulic", "hydraulicEnabled",
                "Simulates rainfall and water flow — carves drainage valleys and sharpens ridges."))
            {
                Half("rain", C("Droplets", "Density of water droplets simulated (per cell). More = finer drainage detail, slower."),
                     "hydraulicIterations", C("Lifetime", "How many cells each droplet travels before drying up. Should be in the order of the area size in cells; auto-fit scales it. Longer = longer valleys."), 78f);
                Half("dropletInertia", C("Inertia", "How much a droplet keeps its direction vs following the slope. Low = hugs the terrain."),
                     "sedimentCapacity", C("Capacity", "How much sediment a droplet can carry. Higher = deeper channels."), 66f);
                Half("erosionRate", C("Erode", "How fast droplets cut into the terrain."),
                     "depositionRate", C("Deposit", "How fast droplets drop sediment where they slow down."), 66f);
                Half("evaporation", C("Evaporate", "How fast droplets lose water per step. Higher = shorter channels."),
                     "erosionRadius", C("Radius", "Erosion brush radius in heightmap cells. Bigger = wider, smoother valleys; 1 = single-cell (sharp, noisy)."), 66f);
            }

            if (ToggleSection("Snowfall", "uiSnow", "snowEnabled",
                "Accumulates snow by height, slope and terrain shape; rock shows through the snow mask where snow can't hold."))
            {
                Half("snowSlopeStart", C("Rock Starts", "Slopes steeper than this angle (°) begin to show rock."),
                     "snowSlopeFull", C("Full Rock", "Slopes steeper than this angle (°) are entirely bare rock."), 78f);
                Half("rockStrength", C("Rock Amount", "How strongly bare rock shows through the snow mask on steep slopes. 0 = never paint rock."),
                     "snowCrevice", C("Crevices", "Hollows and gullies hold extra snow while ridge crests and cliff brows shed it. Higher = more contrast between crags and gullies."), 84f);
                Half("snowLineLow", C("Snow Line Lo", "World Y below which no snow accumulates. -1 = disabled (snow at all heights)."),
                     "snowLineHigh", C("Snow Line Hi", "World Y above which snow fully accumulates. -1 = disabled."), 84f);
                if (Section("Advanced", "uiSnowAdv", "Fine snow controls."))
                {
                    Half("snowAmount", C("Snow Depth", "Snow depth (m) added where covered — scaled by Adds Height."),
                         "snowAddsHeight", C("Adds Height", "How much the snow deforms the terrain (0-1): adds Snow Depth and drifts snow into hollows, burying fine detail under cover."), 84f);
                    Field("snowSettleIterations", C("Soften", "Blur passes that soften the snow/rock boundary."), 84f);
                }
            }

            {
                var row = Row();
                bool canBake = builder != null && index >= 0
                    && property.FindPropertyRelative("enabled").boolValue
                    && property.FindPropertyRelative("area").arraySize >= 3;
                EditorGUI.BeginDisabledGroup(!canBake);
                if (GUI.Button(row, C("Bake Into Terrain",
                    "Permanently folds this formation into the terrain and the baseline, then removes it from the list. Asks for confirmation; a baseline backup is written first.")))
                {
                    var b = builder;
                    var target = b != null && index >= 0 && b.formations != null && index < b.formations.Count
                        ? b.formations[index] : null;
                    EditorApplication.delayCall += () => BakeDeferred(b, target);
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUI.EndProperty();
        }

        static void BakeDeferred(FormationBuilder b, Formation f)
        {
            if (b == null || f == null) return;
            string nm = string.IsNullOrEmpty(f.name) ? "Formation" : f.name;
            if (!EditorUtility.DisplayDialog("Bake Formation",
                $"Permanently fold '{nm}' into the terrain and its baseline, then remove it from the formation list?\n\n" +
                "A baseline backup is written first and the bake is undoable this session. Formations overlapping this " +
                "one may shift slightly when they re-apply on the new ground.",
                "Bake", "Cancel")) return;
            b.BakeFormation(f);
        }

        static void FitSerialized(SerializedProperty p, FormationBuilder builder, int index)
        {
            if (builder == null || index < 0 || builder.formations == null || index >= builder.formations.Count) return;
            float radius = builder.AreaRadius(builder.formations[index]);
            if (radius < 1f) return;
            var drP = p.FindPropertyRelative("domeReach");
            float factor = radius / Mathf.Max(drP.floatValue, 0.01f);
            var hP = p.FindPropertyRelative("height");
            float newH = hP.floatValue * factor;
            float maxH = builder.TerrainHeight() * 0.9f;
            hP.floatValue = Mathf.Sign(newH) * Mathf.Min(Mathf.Abs(newH), maxH);
            drP.floatValue = radius;
            var ns = p.FindPropertyRelative("noiseScale"); ns.floatValue *= factor;
            var nh = p.FindPropertyRelative("noiseHeight"); nh.floatValue *= factor;
            var ef = p.FindPropertyRelative("edgeFalloff"); ef.floatValue *= factor;
            var bh = p.FindPropertyRelative("baseHeight"); bh.floatValue *= factor;
            var hi = p.FindPropertyRelative("hydraulicIterations");
            hi.intValue = Mathf.Clamp(Mathf.RoundToInt(hi.intValue * factor), 8, 4000);
            p.FindPropertyRelative("fitted").boolValue = true;
        }

        internal static void WriteShape(SerializedProperty p, Formation src, bool setName)
        {
            if (setName) p.FindPropertyRelative("name").stringValue = src.name;
            p.FindPropertyRelative("blendMode").enumValueIndex = (int)src.blendMode;
            p.FindPropertyRelative("height").floatValue = src.height;
            p.FindPropertyRelative("baseHeight").floatValue = src.baseHeight;
            p.FindPropertyRelative("domeReach").floatValue = src.domeReach;
            p.FindPropertyRelative("edgeFalloff").floatValue = src.edgeFalloff;
            p.FindPropertyRelative("domeProfile").animationCurveValue =
                src.domeProfile != null ? new AnimationCurve(src.domeProfile.keys) : AnimationCurve.EaseInOut(0, 0, 1, 1);
            p.FindPropertyRelative("noiseType").enumValueIndex = (int)src.noiseType;
            p.FindPropertyRelative("noiseHeight").floatValue = src.noiseHeight;
            p.FindPropertyRelative("noiseScale").floatValue = src.noiseScale;
            p.FindPropertyRelative("octaves").intValue = src.octaves;
            p.FindPropertyRelative("lacunarity").floatValue = src.lacunarity;
            p.FindPropertyRelative("gain").floatValue = src.gain;
            p.FindPropertyRelative("warp").floatValue = src.warp;
            p.FindPropertyRelative("seed").intValue = src.seed;
            p.FindPropertyRelative("noiseFollowsDome").floatValue = src.noiseFollowsDome;
            p.FindPropertyRelative("smooth").floatValue = src.smooth;
            p.FindPropertyRelative("smoothIterations").intValue = src.smoothIterations;
            p.FindPropertyRelative("dropletInertia").floatValue = src.dropletInertia;
            p.FindPropertyRelative("thermalEnabled").boolValue = src.thermalEnabled;
            p.FindPropertyRelative("thermalIterations").intValue = src.thermalIterations;
            p.FindPropertyRelative("thermalRepose").floatValue = src.thermalRepose;
            p.FindPropertyRelative("thermalStrength").floatValue = src.thermalStrength;
            p.FindPropertyRelative("hydraulicEnabled").boolValue = src.hydraulicEnabled;
            p.FindPropertyRelative("hydraulicIterations").intValue = src.hydraulicIterations;
            p.FindPropertyRelative("rain").floatValue = src.rain;
            p.FindPropertyRelative("evaporation").floatValue = src.evaporation;
            p.FindPropertyRelative("sedimentCapacity").floatValue = src.sedimentCapacity;
            p.FindPropertyRelative("erosionRate").floatValue = src.erosionRate;
            p.FindPropertyRelative("depositionRate").floatValue = src.depositionRate;
            p.FindPropertyRelative("erosionRadius").intValue = src.erosionRadius;
            p.FindPropertyRelative("snowEnabled").boolValue = src.snowEnabled;
            p.FindPropertyRelative("snowAmount").floatValue = src.snowAmount;
            p.FindPropertyRelative("snowLineLow").floatValue = src.snowLineLow;
            p.FindPropertyRelative("snowLineHigh").floatValue = src.snowLineHigh;
            p.FindPropertyRelative("snowSlopeStart").floatValue = src.snowSlopeStart;
            p.FindPropertyRelative("snowSlopeFull").floatValue = src.snowSlopeFull;
            p.FindPropertyRelative("snowCrevice").floatValue = src.snowCrevice;
            p.FindPropertyRelative("snowSettleIterations").intValue = src.snowSettleIterations;
            p.FindPropertyRelative("snowAddsHeight").floatValue = src.snowAddsHeight;
            p.FindPropertyRelative("rockStrength").floatValue = src.rockStrength;
        }

        static Formation ReadShape(SerializedProperty p)
        {
            return new Formation
            {
                name = p.FindPropertyRelative("name").stringValue,
                blendMode = (FormationBlend)p.FindPropertyRelative("blendMode").enumValueIndex,
                height = p.FindPropertyRelative("height").floatValue,
                baseHeight = p.FindPropertyRelative("baseHeight").floatValue,
                domeReach = p.FindPropertyRelative("domeReach").floatValue,
                edgeFalloff = p.FindPropertyRelative("edgeFalloff").floatValue,
                domeProfile = p.FindPropertyRelative("domeProfile").animationCurveValue,
                noiseType = (FormationNoise)p.FindPropertyRelative("noiseType").enumValueIndex,
                noiseHeight = p.FindPropertyRelative("noiseHeight").floatValue,
                noiseScale = p.FindPropertyRelative("noiseScale").floatValue,
                octaves = p.FindPropertyRelative("octaves").intValue,
                lacunarity = p.FindPropertyRelative("lacunarity").floatValue,
                gain = p.FindPropertyRelative("gain").floatValue,
                warp = p.FindPropertyRelative("warp").floatValue,
                seed = p.FindPropertyRelative("seed").intValue,
                noiseFollowsDome = p.FindPropertyRelative("noiseFollowsDome").floatValue,
                smooth = p.FindPropertyRelative("smooth").floatValue,
                smoothIterations = p.FindPropertyRelative("smoothIterations").intValue,
                dropletInertia = p.FindPropertyRelative("dropletInertia").floatValue,
                thermalEnabled = p.FindPropertyRelative("thermalEnabled").boolValue,
                thermalIterations = p.FindPropertyRelative("thermalIterations").intValue,
                thermalRepose = p.FindPropertyRelative("thermalRepose").floatValue,
                thermalStrength = p.FindPropertyRelative("thermalStrength").floatValue,
                hydraulicEnabled = p.FindPropertyRelative("hydraulicEnabled").boolValue,
                hydraulicIterations = p.FindPropertyRelative("hydraulicIterations").intValue,
                rain = p.FindPropertyRelative("rain").floatValue,
                evaporation = p.FindPropertyRelative("evaporation").floatValue,
                sedimentCapacity = p.FindPropertyRelative("sedimentCapacity").floatValue,
                erosionRate = p.FindPropertyRelative("erosionRate").floatValue,
                depositionRate = p.FindPropertyRelative("depositionRate").floatValue,
                erosionRadius = p.FindPropertyRelative("erosionRadius").intValue,
                snowEnabled = p.FindPropertyRelative("snowEnabled").boolValue,
                snowAmount = p.FindPropertyRelative("snowAmount").floatValue,
                snowLineLow = p.FindPropertyRelative("snowLineLow").floatValue,
                snowLineHigh = p.FindPropertyRelative("snowLineHigh").floatValue,
                snowSlopeStart = p.FindPropertyRelative("snowSlopeStart").floatValue,
                snowSlopeFull = p.FindPropertyRelative("snowSlopeFull").floatValue,
                snowCrevice = p.FindPropertyRelative("snowCrevice").floatValue,
                snowSettleIterations = p.FindPropertyRelative("snowSettleIterations").intValue,
                snowAddsHeight = p.FindPropertyRelative("snowAddsHeight").floatValue,
                rockStrength = p.FindPropertyRelative("rockStrength").floatValue,
                area = new List<Vector2>(),
            };
        }

        static void SaveDeferred(Formation shape, FormationPreset existing, FormationBuilder b, int idx)
        {
            if (existing != null)
            {
                Undo.RecordObject(existing, "Update Formation Preset");
                existing.formation = shape;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject("Save Formation Preset",
                (string.IsNullOrEmpty(shape.name) ? "Formation" : shape.name) + "Preset",
                "asset", "Save this formation's settings as a reusable preset asset.");
            if (string.IsNullOrEmpty(path)) return;
            var a = ScriptableObject.CreateInstance<FormationPreset>();
            a.formation = shape;
            AssetDatabase.CreateAsset(a, path);
            AssetDatabase.SaveAssets();
            if (b != null && b.formations != null && idx >= 0 && idx < b.formations.Count)
            {
                Undo.RecordObject(b, "Assign Formation Preset");
                b.formations[idx].presetAsset = a;
                EditorUtility.SetDirty(b);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + 2;
            if (!property.isExpanded) return line;

            int rows = 1 + 5;
            rows += 1;
            if (property.FindPropertyRelative("uiShape").boolValue)
            {
                rows += 7;
                if (property.FindPropertyRelative("uiShapeAdv").boolValue) rows += 5;
            }
            rows += 1;
            if (property.FindPropertyRelative("uiThermal").boolValue) rows += 3;
            rows += 1;
            if (property.FindPropertyRelative("uiHydraulic").boolValue) rows += 4;
            rows += 1;
            if (property.FindPropertyRelative("uiSnow").boolValue)
            {
                rows += 4;
                if (property.FindPropertyRelative("uiSnowAdv").boolValue) rows += 2;
            }
            return line * rows + 4;
        }
    }
#endif

    public enum FormationBlend { Add, Raise, Carve, Set }
    public enum FormationNoise { Smooth, Ridged, Billow }

    [Serializable]
    public class Formation
    {
        public string name = "Formation";
        public bool enabled = true;
        public bool drawPreview = true;
        public bool fitted = false;

        public List<Vector2> area = new List<Vector2>();

        public FormationPreset presetAsset;

        public bool uiShape = true;
        public bool uiShapeAdv = false;
        public bool uiThermal = false;
        public bool uiHydraulic = false;
        public bool uiSnow = false;
        public bool uiSnowAdv = false;

        public FormationBlend blendMode = FormationBlend.Add;
        public float height = 55f;
        public float baseHeight = 0f;
        public float domeReach = 100f;
        public float edgeFalloff = 14f;
        public AnimationCurve domeProfile = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public FormationNoise noiseType = FormationNoise.Smooth;
        public float noiseHeight = 12f;
        public float noiseScale = 60f;
        [Range(1, 9)] public int octaves = 4;
        [Range(1.2f, 4f)] public float lacunarity = 2f;
        [Range(0.1f, 0.9f)] public float gain = 0.5f;
        [Range(0f, 2f)] public float warp = 0.25f;
        public int seed = 0;
        [Range(0, 1)] public float noiseFollowsDome = 0.4f;

        [Range(0, 1)] public float smooth = 0.2f;
        public int smoothIterations = 2;

        public bool thermalEnabled = false;
        public int thermalIterations = 50;
        [Range(5f, 80f)] public float thermalRepose = 36f;
        [Range(0, 1)] public float thermalStrength = 0.5f;

        public bool hydraulicEnabled = false;
        public float rain = 0.5f;
        public int hydraulicIterations = 250;
        [Range(0, 1)] public float dropletInertia = 0.05f;
        public float sedimentCapacity = 4f;
        [Range(0, 1)] public float erosionRate = 0.3f;
        [Range(0, 1)] public float depositionRate = 0.3f;
        [Range(0, 1)] public float evaporation = 0.01f;
        [Range(1, 4)] public int erosionRadius = 3;

        public bool snowEnabled = false;
        public float snowAmount = 1.2f;
        public float snowLineLow = -1f;
        public float snowLineHigh = -1f;
        [Range(0f, 90f)] public float snowSlopeStart = 30f;
        [Range(0f, 90f)] public float snowSlopeFull = 54f;
        [Range(0, 1)] public float snowCrevice = 0.5f;
        public int snowSettleIterations = 2;
        [Range(0, 1)] public float snowAddsHeight = 0.4f;

        [Range(0, 1)] public float rockStrength = 1f;

        [NonSerialized] public FormationPreview preview;
        [NonSerialized] public int previewHash;

        public struct Preset
        {
            public string name;
            public Action<Formation> apply;
        }

        static void Common(Formation f)
        {
            f.domeProfile = AnimationCurve.EaseInOut(0, 0, 1, 1);
            f.baseHeight = 0f;
            f.domeReach = 100f;
            f.lacunarity = 2f;
            f.gain = 0.5f;
            f.noiseFollowsDome = 0.35f;
            f.snowLineLow = -1f;
            f.snowLineHigh = -1f;
        }

        public static readonly Preset[] Presets =
        {
            new Preset { name = "Basic", apply = f =>
            {
                Common(f);
                f.blendMode = FormationBlend.Add; f.height = 55f; f.edgeFalloff = 14f;
                f.noiseType = FormationNoise.Smooth; f.noiseHeight = 12f; f.noiseScale = 60f;
                f.octaves = 4; f.warp = 0.25f; f.noiseFollowsDome = 0.4f;
                f.smooth = 0.2f; f.smoothIterations = 2;
                f.thermalEnabled = false; f.hydraulicEnabled = false; f.snowEnabled = false;
            }},
            new Preset { name = "Mountain", apply = f =>
            {
                Common(f);
                f.blendMode = FormationBlend.Add; f.height = 70f; f.edgeFalloff = 14f;
                f.noiseType = FormationNoise.Ridged; f.noiseHeight = 26f; f.noiseScale = 55f;
                f.octaves = 6; f.warp = 0.5f; f.noiseFollowsDome = 0.35f;
                f.smooth = 0.15f; f.smoothIterations = 1;
                f.thermalEnabled = true; f.thermalIterations = 50; f.thermalRepose = 36f; f.thermalStrength = 0.5f;
                f.hydraulicEnabled = true; f.hydraulicIterations = 350; f.dropletInertia = 0.05f;
                f.rain = 0.7f; f.evaporation = 0.01f; f.sedimentCapacity = 4f; f.erosionRate = 0.3f; f.depositionRate = 0.3f;
                f.erosionRadius = 3;
                f.snowEnabled = true; f.snowAmount = 1.2f; f.snowSlopeStart = 30f; f.snowSlopeFull = 54f;
                f.snowCrevice = 0.6f; f.snowSettleIterations = 2; f.snowAddsHeight = 0.4f;
                f.rockStrength = 1f;
            }},
            new Preset { name = "Hill", apply = f =>
            {
                Common(f);
                f.blendMode = FormationBlend.Add; f.height = 35f; f.edgeFalloff = 20f;
                f.noiseType = FormationNoise.Smooth; f.noiseHeight = 6f; f.noiseScale = 80f;
                f.octaves = 4; f.warp = 0.3f; f.noiseFollowsDome = 0.5f;
                f.smooth = 0.5f; f.smoothIterations = 4;
                f.thermalEnabled = true; f.thermalIterations = 25; f.thermalRepose = 42f; f.thermalStrength = 0.4f;
                f.hydraulicEnabled = false;
                f.snowEnabled = true; f.snowAmount = 1.6f; f.snowSlopeStart = 34f; f.snowSlopeFull = 60f;
                f.snowCrevice = 0.4f; f.snowSettleIterations = 4; f.snowAddsHeight = 0.5f;
                f.rockStrength = 0f;
            }},
            new Preset { name = "Rocks", apply = f =>
            {
                Common(f);
                f.blendMode = FormationBlend.Add; f.height = 22f; f.edgeFalloff = 6f;
                f.noiseType = FormationNoise.Ridged; f.noiseHeight = 14f; f.noiseScale = 25f;
                f.octaves = 6; f.warp = 0.8f; f.noiseFollowsDome = 0.2f;
                f.smooth = 0.05f; f.smoothIterations = 1;
                f.thermalEnabled = true; f.thermalIterations = 20; f.thermalRepose = 46f; f.thermalStrength = 0.4f;
                f.hydraulicEnabled = false;
                f.snowEnabled = true; f.snowAmount = 0.25f; f.snowSlopeStart = 20f; f.snowSlopeFull = 42f;
                f.snowCrevice = 0.7f; f.snowSettleIterations = 1; f.snowAddsHeight = 0f;
                f.rockStrength = 1f;
            }},
            new Preset { name = "Crater", apply = f =>
            {
                Common(f);
                f.blendMode = FormationBlend.Carve; f.height = -30f; f.edgeFalloff = 16f;
                f.noiseType = FormationNoise.Smooth; f.noiseHeight = 6f; f.noiseScale = 50f;
                f.octaves = 4; f.warp = 0.3f; f.noiseFollowsDome = 0.4f;
                f.smooth = 0.3f; f.smoothIterations = 3;
                f.thermalEnabled = true; f.thermalIterations = 30; f.thermalRepose = 34f; f.thermalStrength = 0.5f;
                f.hydraulicEnabled = false;
                f.snowEnabled = true; f.snowAmount = 0.9f; f.snowSlopeStart = 26f; f.snowSlopeFull = 50f;
                f.snowCrevice = 0.5f; f.snowSettleIterations = 2; f.snowAddsHeight = 0.3f;
                f.rockStrength = 1f;
            }},
        };
    }

    public class FormationPreview
    {
        public Vector3[] outline;
        public Vector3 peakBase;
        public Vector3 peakTop;
        public Vector3 labelPos;
        public bool hasPeak;
    }

}
