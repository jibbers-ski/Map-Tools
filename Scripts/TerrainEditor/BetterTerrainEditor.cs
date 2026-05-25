using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditorInternal;

    [CustomEditor(typeof(BetterTerrainEditor))]
    public class BetterTerrainEditorEditor : Editor
    {
        int mirrorMode;
        BetterTerrainEditor editor;

        ReorderableList curveList;
        ReorderableList circleList;
        ReorderableList meshList;
        ReorderableList copyList;
        ReorderableList paintList;

        public static SerializedProperty pickTargetXProp;
        public static SerializedProperty pickTargetYProp;
        public static BetterTerrainEditor pickEditor;
        public static string pickLabel;

        static bool    painting;
        static int     paintMode;           // 0 = Snow (R), 1 = Markings (G)

        // Per-mode paint settings — persisted across sessions via EditorPrefs.
        static float _snowBrushSize  = 10f;
        static float _snowOpacity    = 1f;
        static float _snowHardness   = 1f;
        static float _markBrushSize  = 2f;
        static float _markOpacity    = 0.3f;
        static float _markHardness   = 0.1f;
        static int   _paintMarkingIdx;
        static bool  _lockDirection;
        static float _lockAngle;
        static bool  _stripeBrush;
        static float _stripeAngle;
        static int   _stripeCount = 3;
        static float _stripeSpacing = 20f;
        static bool  _paintPrefsLoaded;

        static Vector3 lockAnchorWorld;
        static bool    hasLockAnchor;

        const string PaintPrefPrefix = "BetterTerrainEditor.Paint.";

        static void EnsurePaintPrefsLoaded()
        {
            if (_paintPrefsLoaded) return;
            _paintPrefsLoaded = true;
            _snowBrushSize   = EditorPrefs.GetFloat(PaintPrefPrefix + "SnowBrush",    10f);
            _snowOpacity     = EditorPrefs.GetFloat(PaintPrefPrefix + "SnowOpacity",  1f);
            _snowHardness    = EditorPrefs.GetFloat(PaintPrefPrefix + "SnowHardness", 1f);
            _markBrushSize   = EditorPrefs.GetFloat(PaintPrefPrefix + "MarkBrush",    2f);
            _markOpacity     = EditorPrefs.GetFloat(PaintPrefPrefix + "MarkOpacity",  0.3f);
            _markHardness    = EditorPrefs.GetFloat(PaintPrefPrefix + "MarkHardness", 0.1f);
            _paintMarkingIdx = EditorPrefs.GetInt  (PaintPrefPrefix + "MarkingIdx",   0);
            _lockDirection   = EditorPrefs.GetBool (PaintPrefPrefix + "LockDirection", false);
            _lockAngle       = EditorPrefs.GetFloat(PaintPrefPrefix + "LockAngle",    0f);
            _stripeBrush     = EditorPrefs.GetBool (PaintPrefPrefix + "StripeBrush",   false);
            _stripeAngle     = EditorPrefs.GetFloat(PaintPrefPrefix + "StripeAngle",   0f);
            _stripeCount     = EditorPrefs.GetInt  (PaintPrefPrefix + "StripeCount",   3);
            _stripeSpacing   = EditorPrefs.GetFloat(PaintPrefPrefix + "StripeSpacing", 20f);
        }

        static void SavePaintPrefs()
        {
            EditorPrefs.SetFloat(PaintPrefPrefix + "SnowBrush",     _snowBrushSize);
            EditorPrefs.SetFloat(PaintPrefPrefix + "SnowOpacity",   _snowOpacity);
            EditorPrefs.SetFloat(PaintPrefPrefix + "SnowHardness",  _snowHardness);
            EditorPrefs.SetFloat(PaintPrefPrefix + "MarkBrush",     _markBrushSize);
            EditorPrefs.SetFloat(PaintPrefPrefix + "MarkOpacity",   _markOpacity);
            EditorPrefs.SetFloat(PaintPrefPrefix + "MarkHardness",  _markHardness);
            EditorPrefs.SetInt  (PaintPrefPrefix + "MarkingIdx",    _paintMarkingIdx);
            EditorPrefs.SetBool (PaintPrefPrefix + "LockDirection", _lockDirection);
            EditorPrefs.SetFloat(PaintPrefPrefix + "LockAngle",     _lockAngle);
            EditorPrefs.SetBool (PaintPrefPrefix + "StripeBrush",   _stripeBrush);
            EditorPrefs.SetFloat(PaintPrefPrefix + "StripeAngle",   _stripeAngle);
            EditorPrefs.SetInt  (PaintPrefPrefix + "StripeCount",   _stripeCount);
            EditorPrefs.SetFloat(PaintPrefPrefix + "StripeSpacing", _stripeSpacing);
        }

        static float paintBrushSize
        {
            get { EnsurePaintPrefsLoaded(); return paintMode == 0 ? _snowBrushSize : _markBrushSize; }
            set { if (paintMode == 0) _snowBrushSize = value; else _markBrushSize = value; SavePaintPrefs(); }
        }
        static float paintOpacity
        {
            get { EnsurePaintPrefsLoaded(); return paintMode == 0 ? _snowOpacity : _markOpacity; }
            set { if (paintMode == 0) _snowOpacity = value; else _markOpacity = value; SavePaintPrefs(); }
        }
        static float paintHardness
        {
            get { EnsurePaintPrefsLoaded(); return paintMode == 0 ? _snowHardness : _markHardness; }
            set { if (paintMode == 0) _snowHardness = value; else _markHardness = value; SavePaintPrefs(); }
        }
        static int paintMarkingIdx
        {
            get { EnsurePaintPrefsLoaded(); return _paintMarkingIdx; }
            set { _paintMarkingIdx = value; SavePaintPrefs(); }
        }
        static bool lockDirection
        {
            get { EnsurePaintPrefsLoaded(); return _lockDirection; }
            set { _lockDirection = value; SavePaintPrefs(); }
        }
        static float lockAngle
        {
            get { EnsurePaintPrefsLoaded(); return _lockAngle; }
            set { _lockAngle = value; SavePaintPrefs(); }
        }
        static bool stripeBrush
        {
            get { EnsurePaintPrefsLoaded(); return _stripeBrush; }
            set { _stripeBrush = value; SavePaintPrefs(); }
        }
        static float stripeAngle
        {
            get { EnsurePaintPrefsLoaded(); return _stripeAngle; }
            set { _stripeAngle = value; SavePaintPrefs(); }
        }
        static int stripeCount
        {
            get { EnsurePaintPrefsLoaded(); return _stripeCount; }
            set { _stripeCount = value; SavePaintPrefs(); }
        }
        static float stripeSpacing
        {
            get { EnsurePaintPrefsLoaded(); return _stripeSpacing; }
            set { _stripeSpacing = value; SavePaintPrefs(); }
        }

        static readonly string[] paintModeNames = { "Snow", "Markings" };
        static readonly string[] markingColorNames  = { "Red",  "Orange", "Gold", "Yellow", "Yellow-Green", "Lime", "Light Green", "Green", "Teal", "Cyan", "Light Blue", "Blue", "Dark Blue", "Purple", "Pink", "Magenta" };
        static readonly float[]  markingColorValues = { 0.05f,  0.10f,    0.15f,  0.20f,    0.25f,          0.30f,  0.35f,         0.40f,   0.45f,  0.50f,  0.55f,         0.60f,  0.65f,       0.70f,    0.75f,  0.80f     };
        static Texture2D paintTexture;
        static BetterTerrainEditor paintEditor;
        static float   lastPaintU, lastPaintV;
        static bool    hasLastPaint;

        void OnEnable()
        {
            SceneView.beforeSceneGui -= HandleEscape;
            SceneView.beforeSceneGui += HandleEscape;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            SetupLists();
        }

        void SetupLists()
        {
            curveList  = MakeList("curveInserts",  "Curve Inserts",  () => new TerrainCurveInsert());
            circleList = MakeList("circleInserts", "Circle Inserts", () => new TerrainCircleInsert());
            meshList   = MakeList("meshInserts",   "Mesh Inserts",   () => new TerrainMeshInsert());
            copyList   = MakeList("copyInserts",   "Copy Inserts",   () => new TerrainCopyInsert());
            paintList = MakeList("paintInserts", "Paint Inserts", () => new TerrainPaintInsert());
        }

        ReorderableList MakeList(string propName, string header, System.Func<object> createDefaults)
        {
            var list = new ReorderableList(serializedObject, serializedObject.FindProperty(propName),
                draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);

            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, header);

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
                EditorGUI.PropertyField(rect,
                    list.serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, true);

            list.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(list.serializedProperty.GetArrayElementAtIndex(index), true);

            list.onAddCallback = l =>
            {
                int newIdx = l.serializedProperty.arraySize;
                l.serializedProperty.arraySize++;
                l.index = newIdx;
                ApplyDefaults(l.serializedProperty.GetArrayElementAtIndex(newIdx), createDefaults());
            };

            return list;
        }

        static void ApplyDefaults(SerializedProperty prop, object instance)
        {
            foreach (var field in instance.GetType().GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var sp  = prop.FindPropertyRelative(field.Name);
                if (sp == null) continue;
                var val = field.GetValue(instance);
                if (val == null) continue;
                switch (sp.propertyType)
                {
                    case SerializedPropertyType.Boolean:        sp.boolValue           = (bool)val;           break;
                    case SerializedPropertyType.Integer:        sp.intValue            = (int)val;             break;
                    case SerializedPropertyType.Float:          sp.floatValue          = (float)val;           break;
                    case SerializedPropertyType.String:         sp.stringValue         = (string)val;          break;
                    case SerializedPropertyType.Vector2:        sp.vector2Value        = (Vector2)val;         break;
                    case SerializedPropertyType.AnimationCurve: sp.animationCurveValue = (AnimationCurve)val; break;
                    case SerializedPropertyType.Enum:           sp.enumValueIndex      = (int)val;             break;
                }
            }
        }

        void OnDisable()
        {
            SceneView.beforeSceneGui -= HandleEscape;
            SceneView.duringSceneGui -= OnSceneGUI;
            pickTargetXProp = null;
            pickTargetYProp = null;
            if (painting) StopPainting();
        }

        static void HandleEscape(SceneView sceneView)
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown || evt.keyCode != KeyCode.Escape) return;

            if (painting)
            {
                StopPainting();
                evt.Use();
            }
            else if (pickTargetXProp != null)
            {
                pickTargetXProp = null;
                pickTargetYProp = null;
                pickLabel = null;
                evt.Use();
            }
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (painting) HandlePaintSceneGUI(sceneView);
            else if (pickTargetXProp != null && pickEditor != null) HandlePickSceneGUI(sceneView);
        }

        static void HandlePickSceneGUI(SceneView sceneView)
        {
            int controlId = GUIUtility.GetControlID("TerrainPicker".GetHashCode(), FocusType.Keyboard);
            HandleUtility.AddDefaultControl(controlId);
            sceneView.Repaint();

            var evt = Event.current;

            Handles.BeginGUI();
            var mp = evt.mousePosition;
            GUI.Label(new Rect(mp.x + 15, mp.y - 10, 200, 20),
                $"Pick {pickLabel}", EditorStyles.whiteBoldLabel);
            Handles.EndGUI();

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 50000f))
                {
                    var coord = pickEditor.WorldToHeightmapCoord(hit.point);
                    pickTargetXProp.intValue = coord.x;
                    pickTargetYProp.intValue = coord.y;
                    pickTargetXProp.serializedObject.ApplyModifiedProperties();
                    pickTargetXProp = null;
                    pickTargetYProp = null;
                    pickLabel       = null;
                }
                evt.Use();
            }
        }

        static void HandlePaintSceneGUI(SceneView sceneView)
        {
            if (paintEditor == null || paintEditor.terrain == null || paintTexture == null)
            {
                StopPainting();
                return;
            }

            int controlId = GUIUtility.GetControlID("TerrainPainter".GetHashCode(), FocusType.Keyboard);
            HandleUtility.AddDefaultControl(controlId);
            GUIUtility.keyboardControl = controlId;
            sceneView.Repaint();

            var evt = Event.current;
            bool erase = evt.control;

            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            var col = paintEditor.terrain.GetComponent<TerrainCollider>();
            if (col != null && col.Raycast(ray, out RaycastHit hit, 50000f))
            {
                Vector3 cursorPos = hit.point;
                if (lockDirection && hasLockAnchor)
                    cursorPos = ProjectOntoLockAxis(hit.point);

                float worldRadius = paintBrushSize * paintEditor.terrain.terrainData.size.x
                    / paintTexture.width;
                if (erase)
                    Handles.color = new Color(1f, 0.3f, 0.3f, 0.6f);
                else if (paintMode == 1)
                {
                    Color hc = Color.HSVToRGB(markingColorValues[paintMarkingIdx], 1f, 1f);
                    Handles.color = new Color(hc.r, hc.g, hc.b, 0.6f);
                }
                else
                    Handles.color = new Color(1f, 1f, 1f, 0.6f);

                if (stripeBrush)
                {
                    var stripeOffsets = GetStripeOffsetsWorld();
                    foreach (var off in stripeOffsets)
                        Handles.DrawWireDisc(cursorPos + off, hit.normal, worldRadius);
                }
                else
                {
                    Handles.DrawWireDisc(cursorPos, hit.normal, worldRadius);
                }

                if (lockDirection)
                {
                    Vector3 dir = LockDirVector();
                    Vector3 axisAnchor = hasLockAnchor ? lockAnchorWorld : hit.point;
                    Handles.color = new Color(1f, 1f, 0f, 0.5f);
                    Handles.DrawLine(axisAnchor - dir * 1000f, axisAnchor + dir * 1000f, 1f);
                }

                if (evt.shift && hasLastPaint)
                {
                    Vector3 terrainPos = paintEditor.terrain.transform.position;
                    Vector3 terrainSize = paintEditor.terrain.terrainData.size;
                    Vector3 lastWorld = new Vector3(
                        terrainPos.x + lastPaintU * terrainSize.x,
                        0f,
                        terrainPos.z + lastPaintV * terrainSize.z);
                    lastWorld.y = paintEditor.terrain.SampleHeight(lastWorld) + terrainPos.y;
                    Handles.color = new Color(1f, 1f, 0f, 0.8f);
                    Handles.DrawLine(lastWorld, cursorPos);

                    Vector3 d = cursorPos - lastWorld;
                    float ang = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
                    if (ang < 0)     ang += 180f;
                    if (ang >= 180f) ang -= 180f;
                    var labelStyle = new GUIStyle(EditorStyles.boldLabel);
                    labelStyle.normal.textColor = Color.yellow;
                    labelStyle.fontSize = 13;
                    Handles.Label((lastWorld + cursorPos) * 0.5f + Vector3.up * 1f, $"{ang:F1}°", labelStyle);
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                Undo.RegisterCompleteObjectUndo(paintTexture, paintMode == 0 ? "Paint Snow" : "Paint Marking");
                if (lockDirection && col != null && col.Raycast(ray, out RaycastHit anchorHit, 50000f))
                {
                    lockAnchorWorld = anchorHit.point;
                    hasLockAnchor = true;
                }
            }
            if (evt.type == EventType.MouseUp && evt.button == 0)
                hasLockAnchor = false;

            bool shouldPaint = (evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag)
                && evt.button == 0;
            if (shouldPaint)
            {
                ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                if (col != null && col.Raycast(ray, out RaycastHit paintHit, 50000f))
                {
                    Vector3 worldPos = paintHit.point;
                    if (lockDirection && hasLockAnchor)
                        worldPos = ProjectOntoLockAxis(worldPos);

                    Vector3 size  = paintEditor.terrain.terrainData.size;
                    Vector3 terrainPos = paintEditor.terrain.transform.position;
                    bool isLine = evt.shift && hasLastPaint && evt.type == EventType.MouseDown;

                    Vector3[] offsets = stripeBrush ? GetStripeOffsetsWorld() : new[] { Vector3.zero };

                    Vector3 lastBaseWorld = Vector3.zero;
                    if (isLine)
                    {
                        lastBaseWorld = new Vector3(
                            terrainPos.x + lastPaintU * size.x,
                            0f,
                            terrainPos.z + lastPaintV * size.z);
                    }

                    foreach (var off in offsets)
                    {
                        Vector3 stripeWorld = worldPos + off;
                        Vector3 stripeLocal = stripeWorld - terrainPos;
                        float u = stripeLocal.x / size.x;
                        float v = stripeLocal.z / size.z;

                        if (isLine)
                        {
                            Vector3 lastStripeWorld = lastBaseWorld + off;
                            Vector3 lastStripeLocal = lastStripeWorld - terrainPos;
                            float lu = lastStripeLocal.x / size.x;
                            float lv = lastStripeLocal.z / size.z;
                            PaintLine(lu, lv, u, v, erase);
                        }
                        else
                        {
                            PaintAt(u, v, erase);
                        }
                    }

                    Vector3 baseLocal = worldPos - terrainPos;
                    lastPaintU   = baseLocal.x / size.x;
                    lastPaintV   = baseLocal.z / size.z;
                    hasLastPaint = true;
                }
                evt.Use();
            }
        }

        static Vector3[] GetStripeOffsetsWorld()
        {
            if (!stripeBrush || stripeCount <= 1)
                return new[] { Vector3.zero };

            float rad = stripeAngle * Mathf.Deg2Rad;
            Vector3 perp = new Vector3(-Mathf.Sin(rad), 0, Mathf.Cos(rad));
            float spacingWorld = stripeSpacing * paintEditor.terrain.terrainData.size.x / paintTexture.width;

            var result = new Vector3[stripeCount];
            for (int i = 0; i < stripeCount; i++)
                result[i] = perp * i * spacingWorld;
            return result;
        }

        static Vector3 LockDirVector()
        {
            float rad = lockAngle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));
        }

        static Vector3 ProjectOntoLockAxis(Vector3 worldPos)
        {
            Vector3 dir = LockDirVector();
            Vector3 toMouse = worldPos - lockAnchorWorld;
            toMouse.y = 0;
            float projDist = Vector3.Dot(toMouse, dir);
            Vector3 projected = lockAnchorWorld + dir * projDist;
            projected.y = worldPos.y;
            return projected;
        }

        static void PaintLine(float u0, float v0, float u1, float v1, bool erase)
        {
            int texW = paintTexture.width;
            int texH = paintTexture.height;
            float dx = (u1 - u0) * texW;
            float dy = (v1 - v0) * texH;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            int steps = Mathf.Max(Mathf.CeilToInt(dist / Mathf.Max(paintBrushSize * 0.3f, 1f)), 1);

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                PaintAt(Mathf.Lerp(u0, u1, t), Mathf.Lerp(v0, v1, t), erase);
            }
        }

        static void PaintAt(float u, float v, bool erase)
        {
            int texW = paintTexture.width;
            int texH = paintTexture.height;

            int cx = Mathf.RoundToInt(u * texW);
            int cy = Mathf.RoundToInt((1f - v) * texH);
            int r  = Mathf.CeilToInt(paintBrushSize);

            int x0 = Mathf.Max(cx - r, 0);
            int x1 = Mathf.Min(cx + r, texW - 1);
            int y0 = Mathf.Max(cy - r, 0);
            int y1 = Mathf.Min(cy + r, texH - 1);

            var pixels = paintTexture.GetPixels(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            int w = x1 - x0 + 1;

            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    float dx = px - cx;
                    float dy = py - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;

                    float t = dist / r;
                    float falloff = Mathf.Clamp01((1f - t) / (1f - paintHardness + 0.001f));
                    float strength = falloff * paintOpacity;

                    int idx = (py - y0) * w + (px - x0);
                    Color c = pixels[idx];

                    if (paintMode == 0) // Snow (R channel)
                    {
                        float target = erase ? 0f : 1f;
                        c.r = Mathf.Lerp(c.r, target, strength);
                    }
                    else // Markings (G = color bucket, B = smooth coverage)
                    {
                        if (erase)
                        {
                            c.b = Mathf.Lerp(c.b, 0f, strength);
                        }
                        else
                        {
                            float newColor = markingColorValues[paintMarkingIdx];
                            bool empty     = c.g < 0.05f;
                            bool sameColor = Mathf.Abs(c.g - newColor) < 0.05f;
                            if (empty || sameColor || falloff > 0.5f)
                                c.g = newColor;
                            c.b = Mathf.Lerp(c.b, 1f, strength);
                        }
                    }

                    pixels[idx] = c;
                }
            }

            paintTexture.SetPixels(x0, y0, w, y1 - y0 + 1, pixels);
            paintTexture.Apply();

            var mat = paintEditor.terrain.materialTemplate;
            mat.SetTexture("_SnowMask", paintTexture);
            paintEditor.terrain.materialTemplate = mat;
        }

        static Texture2D AcquireSnowMask(BetterTerrainEditor editor)
        {
            var mat = editor.terrain.materialTemplate;
            var tex = mat.GetTexture("_SnowMask") as Texture2D;

            if (tex == null)
            {
                Debug.LogError("[BetterTerrainEditor] No _SnowMask texture assigned to the terrain material.");
                return null;
            }

            if (!tex.isReadable)
            {
                Debug.LogError($"[BetterTerrainEditor] Cannot paint: texture '{tex.name}' is not readable. " +
                    "Enable Read/Write in the texture import settings, or use a runtime-created texture.");
                return null;
            }

            try
            {
                var test = tex.GetPixels(0, 0, 1, 1);
                tex.SetPixels(0, 0, 1, 1, test);
            }
            catch (Exception)
            {
                Debug.LogError($"[BetterTerrainEditor] Cannot paint: texture '{tex.name}' " +
                    $"uses unsupported format ({tex.format}). Use an uncompressed format like RGBA32.");
                return null;
            }

            return tex;
        }

        static void StopPainting()
        {
            painting     = false;
            paintEditor  = null;
            paintTexture = null;
            hasLastPaint = false;
        }

        static void CreateSnowMaskTexture(BetterTerrainEditor editor)
        {
            int resolution = editor.terrain.terrainData.heightmapResolution;
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.name = editor.terrain.name + "_SnowMask";

            var pixels = new Color[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(1f, 0f, 0f, 1f);
            tex.SetPixels(pixels);
            tex.Apply();

            string terrainPath = AssetDatabase.GetAssetPath(editor.terrain.terrainData);
            string dir = string.IsNullOrEmpty(terrainPath)
                ? "Assets"
                : System.IO.Path.GetDirectoryName(terrainPath);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + tex.name + ".asset");
            AssetDatabase.CreateAsset(tex, assetPath);
            AssetDatabase.SaveAssets();

            var mat = editor.terrain.materialTemplate;
            Undo.RecordObject(mat, "Assign SnowMask");
            mat.SetTexture("_SnowMask", tex);
            if (mat.HasProperty("_UseMasks"))
                mat.SetFloat("_UseMasks", 1f);
            if (mat.HasProperty("_SnowMask4Channel"))
                mat.SetFloat("_SnowMask4Channel", 1f);
            editor.terrain.materialTemplate = mat;
            EditorUtility.SetDirty(editor.terrain);

            var exporter = editor.GetComponentInParent<MapExporter>();
            if (exporter != null && exporter.chunks != null)
            {
                foreach (var chunk in exporter.chunks)
                {
                    if (chunk.terrain == editor.terrain)
                    {
                        Undo.RecordObject(exporter, "Assign SnowMask to Chunk");
                        chunk.snowMask = tex;
                        EditorUtility.SetDirty(exporter);
                        break;
                    }
                }
            }

            Debug.Log($"[BetterTerrainEditor] Created SnowMask at {assetPath}");
        }

        static bool IsMask4Channel(BetterTerrainEditor editor)
        {
            var mat = editor.terrain != null ? editor.terrain.materialTemplate : null;
            return mat != null && mat.HasProperty("_SnowMask4Channel") && mat.GetFloat("_SnowMask4Channel") > 0.5f;
        }

        static void ConvertSnowMaskTo4Channel(BetterTerrainEditor editor)
        {
            var mat = editor.terrain.materialTemplate;
            var tex = mat.GetTexture("_SnowMask") as Texture2D;
            if (tex == null) return;

            Undo.RegisterCompleteObjectUndo(tex, "Convert SnowMask to 4-Channel");

            var pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(pixels[i].r, 0f, 0f, 1f);
            tex.SetPixels(pixels);
            tex.Apply();

            if (mat.HasProperty("_SnowMask4Channel"))
                mat.SetFloat("_SnowMask4Channel", 1f);

            EditorUtility.SetDirty(tex);
            EditorUtility.SetDirty(editor.terrain);
        }

        static bool IsAssetlessTerrainData(BetterTerrainEditor editor) =>
            editor.terrain != null
            && editor.terrain.terrainData != null
            && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(editor.terrain.terrainData));

        static bool IsAssetlessSnowMask(BetterTerrainEditor editor)
        {
            if (editor.terrain == null) return false;
            var mat = editor.terrain.materialTemplate;
            if (mat == null || !mat.HasProperty("_SnowMask")) return false;
            var tex = mat.GetTexture("_SnowMask") as Texture2D;
            if (tex == null) return false;
            return string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex));
        }

        static string FindBestSaveDir(BetterTerrainEditor editor)
        {
            if (editor.terrain != null && editor.terrain.terrainData != null)
            {
                string p = AssetDatabase.GetAssetPath(editor.terrain.terrainData);
                if (!string.IsNullOrEmpty(p)) return System.IO.Path.GetDirectoryName(p);
            }
            var scene = editor.gameObject.scene;
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                return System.IO.Path.GetDirectoryName(scene.path);
            return "Assets";
        }

        static void SaveTerrainDataAsAsset(BetterTerrainEditor editor)
        {
            var data = editor.terrain.terrainData;
            string dir = FindBestSaveDir(editor);
            string baseName = string.IsNullOrEmpty(data.name) ? editor.terrain.name + "_TerrainData" : data.name;
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + baseName + ".asset");
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(editor.terrain);
            Debug.Log($"[BetterTerrainEditor] Saved TerrainData to {path}");
        }

        static void SaveSnowMaskAsAsset(BetterTerrainEditor editor)
        {
            var mat = editor.terrain.materialTemplate;
            var tex = mat.GetTexture("_SnowMask") as Texture2D;
            string dir = FindBestSaveDir(editor);
            string baseName = string.IsNullOrEmpty(tex.name) ? editor.terrain.name + "_SnowMask" : tex.name;
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + baseName + ".asset");
            AssetDatabase.CreateAsset(tex, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BetterTerrainEditor] Saved SnowMask to {path}");
        }

        static int AnimCurveHash(AnimationCurve curve)
        {
            if (curve == null) return 0;
            unchecked
            {
                int h = 17;
                foreach (var k in curve.keys)
                {
                    h = h * 31 + k.time.GetHashCode();
                    h = h * 31 + k.value.GetHashCode();
                    h = h * 31 + k.inTangent.GetHashCode();
                    h = h * 31 + k.outTangent.GetHashCode();
                }
                return h;
            }
        }

        static int ComputeCurveHash(TerrainCurveInsert ins, BetterTerrainEditor editor)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + ins.startX;
                h = h * 31 + ins.startY;
                h = h * 31 + ins.endX;
                h = h * 31 + ins.endY;
                h = h * 31 + ins.width.GetHashCode();
                h = h * 31 + ins.heightOverrides.GetHashCode();
                h = h * 31 + ins.repeats;
                h = h * 31 + ins.repeatScaling.GetHashCode();
                h = h * 31 + ins.repeatTransitionFade.GetHashCode();
                h = h * 31 + ins.crossSectionDepth.GetHashCode();
                h = h * 31 + ins.crossSectionSideFlatten.GetHashCode();
                h = h * 31 + ins.edgeBlend.GetHashCode();
                h = h * 31 + ins.edgeFalloff.GetHashCode();
                h = h * 31 + (int) ins.edgeBlendMode;
                h = h * 31 + ins.tiltDepth.GetHashCode();
                h = h * 31 + AnimCurveHash(ins.tiltCurve);
                h = h * 31 + AnimCurveHash(ins.curve);
                h = h * 31 + AnimCurveHash(ins.crossSection);
                if (editor.terrain != null)
                {
                    h = h * 31 + editor.terrain.transform.position.GetHashCode();
                    h = h * 31 + editor.terrain.terrainData.size.GetHashCode();
                }
                return h;
            }
        }

        static void BakeCurveCache(TerrainCurveInsert ins, BetterTerrainEditor editor,
            float idxToDist, Vector3 startWorld, Vector3 endWorld, int hash)
        {
            var a = startWorld;
            var b = endWorld;
            if (ins.heightOverrides.x != -1) a.y = editor.terrain.transform.position.y + ins.heightOverrides.x;
            if (ins.heightOverrides.y != -1) b.y = editor.terrain.transform.position.y + ins.heightOverrides.y;

            const int previewRes = 128;
            float[] baked = new float[previewRes];
            for (int i = 0; i < previewRes; i++)
                baked[i] = ins.curve.EvaluateRepeatedScaling((float) i / previewRes, ins.repeatScaling, ins.repeats);
            if (ins.repeatTransitionFade > 0 && ins.repeats > 1)
                BetterTerrainEditor.ApplyRepeatFade(baked, previewRes, ins.repeatScaling, ins.repeats, ins.repeatTransitionFade);

            var points = new Vector3[previewRes];
            for (int i = 0; i < previewRes; i++)
            {
                float t = (float) i / previewRes;
                var p = Vector3.Lerp(a, b, t);
                points[i] = new Vector3(p.x, Mathf.Lerp(a.y, b.y, 1 - baked[i]), p.z);
            }

            var fwd   = (b - a).normalized;
            var right = Vector3.Cross(Vector3.up, fwd);
            float halfW   = ins.width * 0.5f * idxToDist;
            float flatten = ins.crossSectionSideFlatten;

            Vector3[] leftEdge, rightEdge;
            if (flatten > 0)
            {
                leftEdge  = new[] { a - right * halfW, b - right * halfW };
                rightEdge = new[] { a + right * halfW, b + right * halfW };
            }
            else
            {
                float edgeCrossL = ins.crossSection != null ? ins.crossSection.Evaluate(0f) * ins.crossSectionDepth : 0f;
                float edgeCrossR = ins.crossSection != null ? ins.crossSection.Evaluate(1f) * ins.crossSectionDepth : 0f;
                leftEdge  = new Vector3[previewRes];
                rightEdge = new Vector3[previewRes];
                for (int i = 0; i < previewRes; i++)
                {
                    leftEdge[i]  = points[i] - right * halfW + Vector3.up * edgeCrossL;
                    rightEdge[i] = points[i] + right * halfW + Vector3.up * edgeCrossR;
                }
            }

            Vector3[][] crossSections = null;
            if (ins.crossSection != null)
            {
                const int crossPreviews = 8;
                const int crossRes      = 24;
                crossSections = new Vector3[crossPreviews + 1][];
                for (int ci = 0; ci <= crossPreviews; ci++)
                {
                    int idx = Mathf.Clamp(ci * previewRes / crossPreviews, 0, previewRes - 1);
                    float lt    = (float) idx / previewRes;
                    float flatY = Mathf.Lerp(a.y, b.y, lt);
                    float curveY = points[idx].y;
                    var baseXZ = new Vector3(points[idx].x, 0, points[idx].z);
                    var section = new Vector3[crossRes + 1];
                    for (int j = 0; j <= crossRes; j++)
                    {
                        float ct = (float) j / crossRes;
                        float sf;
                        if (flatten <= 0) sf = 1f;
                        else
                        {
                            float ed = Mathf.Min(ct, 1f - ct);
                            float f = Mathf.Clamp01(ed / flatten);
                            sf = f * f * (3f - 2f * f);
                        }
                        float crossH = ins.crossSection.Evaluate(ct) * ins.crossSectionDepth;
                        float tiltVal = ins.tiltCurve != null ? ins.tiltCurve.Evaluate(lt) : 0f;
                        float tiltBias = tiltVal * ins.tiltDepth * (ct - 0.5f);
                        float h = Mathf.Lerp(flatY, curveY + crossH + tiltBias, sf);
                        float lateralOffset = (ct - 0.5f) * ins.width * idxToDist;
                        section[j] = baseXZ + right * lateralOffset + Vector3.up * h;
                    }
                    crossSections[ci] = section;
                }
            }

            ins.cache = new TerrainCurveInsert.PreviewCache
            {
                hash          = hash,
                labelPos      = (a + b) * 0.5f,
                mainLine      = points,
                leftEdge      = leftEdge,
                rightEdge     = rightEdge,
                crossSections = crossSections,
            };
        }

        static void SetAllPreviews(BetterTerrainEditor editor, bool value)
        {
            Undo.RecordObject(editor, value ? "Show All Previews" : "Hide All Previews");
            if (editor.curveInserts  != null) foreach (var ins in editor.curveInserts)  if (ins != null) ins.drawPreview = value;
            if (editor.circleInserts != null) foreach (var ins in editor.circleInserts) if (ins != null) ins.drawPreview = value;
            if (editor.meshInserts   != null) foreach (var ins in editor.meshInserts)   if (ins != null) ins.drawPreview = value;
            if (editor.copyInserts   != null) foreach (var ins in editor.copyInserts)   if (ins != null) ins.drawPreview = value;
            if (editor.paintInserts  != null) foreach (var ins in editor.paintInserts)  if (ins != null) ins.drawPreview = value;
            EditorUtility.SetDirty(editor);
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            curveList.DoLayoutList();
            EditorGUILayout.Space(4);
            circleList.DoLayoutList();
            EditorGUILayout.Space(4);
            meshList.DoLayoutList();
            EditorGUILayout.Space(4);
            copyList.DoLayoutList();
            EditorGUILayout.Space(4);
            paintList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            editor = (BetterTerrainEditor)target;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show All Previews")) SetAllPreviews(editor, true);
            if (GUILayout.Button("Hide All Previews")) SetAllPreviews(editor, false);
            EditorGUILayout.EndHorizontal();

            if (editor.terrain != null && editor.terrain.materialTemplate != null
                && !editor.terrain.materialTemplate.name.Contains("(Instance)"))
            {
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Create Instanced Material"))
                {
                    var instance = new Material(editor.terrain.materialTemplate);
                    instance.name = editor.terrain.materialTemplate.name + " (Instance)";
                    Undo.RecordObject(editor.terrain, "Create Instanced Material");
                    editor.terrain.materialTemplate = instance;
                    EditorUtility.SetDirty(editor.terrain);
                }
            }

            if (editor.terrain != null && editor.terrain.materialTemplate != null
                && editor.terrain.materialTemplate.name.Contains("(Instance)")
                && editor.terrain.materialTemplate.HasProperty("_SnowMask"))
            {
                var maskTex = editor.terrain.materialTemplate.GetTexture("_SnowMask");
                if (maskTex == null)
                {
                    EditorGUILayout.Space(8);
                    if (GUILayout.Button("Create SnowMask Texture"))
                        CreateSnowMaskTexture(editor);
                }
                else if (!IsMask4Channel(editor))
                {
                    EditorGUILayout.Space(8);
                    if (GUILayout.Button("Convert SnowMask to 4-Channel"))
                        ConvertSnowMaskTo4Channel(editor);
                }
            }

            if (IsAssetlessTerrainData(editor))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("TerrainData is scene-embedded (saved inline with the scene file, not a separate asset). Extract it into an asset file for portability.", MessageType.Info);
                if (GUILayout.Button("Save TerrainData as Asset"))
                    SaveTerrainDataAsAsset(editor);
            }

            if (IsAssetlessSnowMask(editor))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("SnowMask is scene-embedded (saved inline with the scene file, not a separate asset). Extract it into an asset file for portability.", MessageType.Info);
                if (GUILayout.Button("Save SnowMask as Asset"))
                    SaveSnowMaskAsAsset(editor);
            }

            EditorGUILayout.Space(8);

            mirrorMode = EditorGUILayout.Popup("Mirror Mode", mirrorMode,
                new[] { "Horizontal", "Vertical", "Both" });
            if (GUILayout.Button("Mirror"))
                editor.Mirror(mirrorMode);

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Paint Terrain", EditorStyles.boldLabel);

            if (!painting)
            {
                bool hasMask = editor.terrain != null
                    && editor.terrain.materialTemplate != null
                    && editor.terrain.materialTemplate.HasProperty("_SnowMask");
                EditorGUI.BeginDisabledGroup(!hasMask);
                if (GUILayout.Button("Paint Terrain"))
                {
                    var tex = AcquireSnowMask(editor);
                    if (tex != null)
                    {
                        painting     = true;
                        paintEditor  = editor;
                        paintTexture = tex;
                        if (SceneView.lastActiveSceneView != null)
                            SceneView.lastActiveSceneView.Focus();
                    }
                }
                EditorGUI.EndDisabledGroup();
                if (!hasMask)
                    EditorGUILayout.HelpBox("Terrain material has no _SnowMask property.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Painting active — click/drag on terrain in Scene View.\nCtrl = erase, Shift+Click = straight line. Esc to stop.",
                    MessageType.Info);
                bool is4ch = IsMask4Channel(paintEditor);
                if (is4ch)
                    paintMode = EditorGUILayout.Popup("Paint Mode", paintMode, paintModeNames);
                else
                    paintMode = 0;
                paintBrushSize = EditorGUILayout.Slider("Brush Size", paintBrushSize, 1f, 500f);
                paintOpacity   = EditorGUILayout.Slider("Opacity",    paintOpacity,   0.01f, 1f);
                paintHardness  = EditorGUILayout.Slider("Hardness",   paintHardness,  0f, 1f);
                if (paintMode == 1)
                {
                    EditorGUILayout.Space(8);
                    paintMarkingIdx = EditorGUILayout.Popup("Marking Color", paintMarkingIdx, markingColorNames);
                }

                EditorGUILayout.Space(12);
                lockDirection = EditorGUILayout.Toggle("Lock Direction", lockDirection);
                if (lockDirection)
                    lockAngle = EditorGUILayout.Slider("Lock Angle (°)", lockAngle, 0f, 180f);

                EditorGUILayout.Space(12);
                stripeBrush = EditorGUILayout.Toggle("Stripe Brush", stripeBrush);
                if (stripeBrush)
                {
                    stripeAngle   = EditorGUILayout.Slider   ("Stripe Angle (°)", stripeAngle, 0f, 180f);
                    stripeCount   = EditorGUILayout.IntSlider("Stripe Count",     stripeCount, 1, 16);
                    stripeSpacing = EditorGUILayout.Slider   ("Stripe Spacing",   stripeSpacing, 1f, 500f);
                }

                EditorGUILayout.Space(12);
                if (GUILayout.Button("Stop Painting"))
                    StopPainting();
            }

            editor.gizmo = () =>
            {
                if (editor.terrain == null || editor.resX <= 1) return;

                float idxToDist = editor.terrain.terrainData.size.x / (editor.resX - 1);

                if (editor.curveInserts != null)
                {
                    foreach (var ins in editor.curveInserts)
                    {
                        if (ins.startX == ins.endX && ins.startY == ins.endY) continue;
                        if (ins.curve == null || ins.curve.length == 0) continue;

                        var startWorld = editor.HeightmapCoordToWorld(ins.startX, ins.startY);
                        var endWorld   = editor.HeightmapCoordToWorld(ins.endX,   ins.endY);

                        if (!ins.drawPreview)
                        {
                            if (!string.IsNullOrEmpty(ins.name))
                                Handles.Label((startWorld + endWorld) * 0.5f, ins.name);
                            Handles.color = MoreColors.Violet;
                            Handles.DrawLine(startWorld, endWorld, 2f);
                            continue;
                        }

                        int hash = ComputeCurveHash(ins, editor);
                        if (ins.cache == null || ins.cache.hash != hash)
                            BakeCurveCache(ins, editor, idxToDist, startWorld, endWorld, hash);

                        var cache = ins.cache;
                        if (!string.IsNullOrEmpty(ins.name))
                            Handles.Label(cache.labelPos, ins.name);

                        Handles.color = MoreColors.Mint;
                        if (cache.mainLine != null) Handles.DrawAAPolyLine(2f, cache.mainLine);

                        Handles.color = MoreColors.Indigo;
                        if (cache.leftEdge  != null) Handles.DrawAAPolyLine(2f, cache.leftEdge);
                        if (cache.rightEdge != null) Handles.DrawAAPolyLine(2f, cache.rightEdge);

                        if (cache.crossSections != null)
                        {
                            Handles.color = MoreColors.Forest;
                            foreach (var cs in cache.crossSections)
                                Handles.DrawAAPolyLine(2f, cs);
                        }
                    }
                }

                if (editor.circleInserts != null)
                {
                    const int ringRes = 48;
                    var ringPts = new List<Vector3>(ringRes + 1);

                    foreach (var ins in editor.circleInserts)
                    {
                        if (ins.radius <= 0) continue;

                        var   centerWorld = editor.HeightmapCoordToWorld(ins.centerX, ins.centerY);
                        float radiusWorld = ins.radius * idxToDist;

                        if (!string.IsNullOrEmpty(ins.name))
                            Handles.Label(centerWorld, ins.name);

                        ringPts.Clear();
                        for (int i = 0; i <= ringRes; i++)
                        {
                            float angle = (float)i / ringRes * Mathf.PI * 2f;
                            float rx = centerWorld.x + Mathf.Cos(angle) * radiusWorld;
                            float rz = centerWorld.z + Mathf.Sin(angle) * radiusWorld;
                            var   hc = editor.WorldToHeightmapCoord(new Vector3(rx, 0, rz));
                            float ry = editor.HeightmapCoordToWorld(hc.x, hc.y).y;
                            ringPts.Add(new Vector3(rx, ry, rz));
                        }
                        Gizmos.color = MoreColors.Violet;
                        Utility.DrawLineGizmo(ringPts, sphereRadius: 0.5f);

                        if (ins.drawPreview && ins.radialCurve != null && ins.radialCurve.length > 0)
                        {
                            float baseY = ins.heightOverride >= 0
                                ? editor.terrain.transform.position.y + ins.heightOverride
                                : centerWorld.y;

                            const int profRes = 48;
                            Gizmos.color = MoreColors.Mint;

                            for (int dir = 0; dir < 4; dir++)
                            {
                                float ax = dir == 0 ? 1 : dir == 1 ? -1 : 0;
                                float az = dir == 2 ? 1 : dir == 3 ? -1 : 0;
                                var profPts = new List<Vector3>(profRes);
                                for (int i = 0; i < profRes; i++)
                                {
                                    float t = (float)i / (profRes - 1);
                                    float r = t * radiusWorld;
                                    float h = baseY + ins.radialCurve.Evaluate(t) * ins.depth;
                                    profPts.Add(new Vector3(centerWorld.x + ax * r, h, centerWorld.z + az * r));
                                }
                                Utility.DrawLineGizmo(profPts, sphereRadius: 0.3f);
                            }
                        }
                    }
                }

                if (editor.meshInserts != null)
                {
                    foreach (var ins in editor.meshInserts)
                    {
                        if (ins.mesh == null) continue;

                        var centerWorld = editor.HeightmapCoordToWorld(ins.centerX, ins.centerY);
                        float baseY = ins.heightOffset >= 0
                            ? editor.terrain.transform.position.y + ins.heightOffset
                            : centerWorld.y;
                        var pos = new Vector3(centerWorld.x, baseY, centerWorld.z);
                        var rot = Quaternion.Euler(ins.rotation);
                        var scl = Vector3.one * ins.scale;

                        if (!string.IsNullOrEmpty(ins.name))
                            Handles.Label(pos, ins.name);

                        if (ins.drawPreview)
                        {
                            Gizmos.color = MoreColors.Mint;
                            Gizmos.DrawWireMesh(ins.mesh, pos, rot, scl);
                        }

                        Gizmos.color = MoreColors.Violet;
                        var bounds = ins.mesh.bounds;
                        Gizmos.matrix = Matrix4x4.TRS(pos, rot, scl);
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                        Gizmos.matrix = Matrix4x4.identity;
                    }
                }

                if (editor.copyInserts != null)
                {
                    foreach (var ins in editor.copyInserts)
                    {
                        if (ins.sourceTerrain == null) continue;

                        var src     = ins.sourceTerrain;
                        var srcData = src.terrainData;
                        int srcRes  = srcData.heightmapResolution;
                        int sx0     = Mathf.Clamp(ins.srcStartX, 0, srcRes - 1);
                        int sy0     = Mathf.Clamp(ins.srcStartY, 0, srcRes - 1);
                        int sw      = Mathf.Clamp(ins.srcSizeX,  1, srcRes - sx0);
                        int sh      = Mathf.Clamp(ins.srcSizeY,  1, srcRes - sy0);

                        Vector3 srcOrigin = src.transform.position;
                        float   srcPxX    = srcData.size.x / (srcRes - 1);
                        float   srcPxZ    = srcData.size.z / (srcRes - 1);

                        Vector3 sa = srcOrigin + new Vector3(sx0          * srcPxX, 0, sy0          * srcPxZ);
                        Vector3 sb = srcOrigin + new Vector3((sx0+sw-1)   * srcPxX, 0, sy0          * srcPxZ);
                        Vector3 sc = srcOrigin + new Vector3((sx0+sw-1)   * srcPxX, 0, (sy0+sh-1)   * srcPxZ);
                        Vector3 sd = srcOrigin + new Vector3(sx0          * srcPxX, 0, (sy0+sh-1)   * srcPxZ);
                        sa.y = srcOrigin.y + srcData.GetHeights(sx0,        sy0,        1, 1)[0,0] * srcData.size.y;
                        sb.y = srcOrigin.y + srcData.GetHeights(sx0+sw-1,   sy0,        1, 1)[0,0] * srcData.size.y;
                        sc.y = srcOrigin.y + srcData.GetHeights(sx0+sw-1,   sy0+sh-1,   1, 1)[0,0] * srcData.size.y;
                        sd.y = srcOrigin.y + srcData.GetHeights(sx0,        sy0+sh-1,   1, 1)[0,0] * srcData.size.y;

                        if (!string.IsNullOrEmpty(ins.name))
                            Handles.Label((sa + sc) * 0.5f, ins.name + " (source)");

                        Handles.color = MoreColors.Violet;
                        Handles.DrawLine(sa, sb, 2f);
                        Handles.DrawLine(sb, sc, 2f);
                        Handles.DrawLine(sc, sd, 2f);
                        Handles.DrawLine(sd, sa, 2f);

                        var dstData = editor.terrain.terrainData;
                        int dstRes  = dstData.heightmapResolution;

                        float worldW = (sw - 1) * srcPxX;
                        float worldD = (sh - 1) * srcPxZ;
                        float dstPxX = dstData.size.x / (dstRes - 1);
                        float dstPxZ = dstData.size.z / (dstRes - 1);
                        int dw = Mathf.Max(1, Mathf.RoundToInt(worldW / dstPxX) + 1);
                        int dh = Mathf.Max(1, Mathf.RoundToInt(worldD / dstPxZ) + 1);

                        int dx0 = Mathf.Clamp(ins.dstStartX, 0, dstRes - 1);
                        int dy0 = Mathf.Clamp(ins.dstStartY, 0, dstRes - 1);
                        dw = Mathf.Min(dw, dstRes - dx0);
                        dh = Mathf.Min(dh, dstRes - dy0);

                        Vector3 dstOrigin = editor.terrain.transform.position;
                        Vector3 da = dstOrigin + new Vector3(dx0          * dstPxX, 0, dy0          * dstPxZ);
                        Vector3 db = dstOrigin + new Vector3((dx0+dw-1)   * dstPxX, 0, dy0          * dstPxZ);
                        Vector3 dc = dstOrigin + new Vector3((dx0+dw-1)   * dstPxX, 0, (dy0+dh-1)   * dstPxZ);
                        Vector3 dd = dstOrigin + new Vector3(dx0          * dstPxX, 0, (dy0+dh-1)   * dstPxZ);
                        da.y = dstOrigin.y + dstData.GetHeights(dx0,        dy0,        1, 1)[0,0] * dstData.size.y;
                        db.y = dstOrigin.y + dstData.GetHeights(dx0+dw-1,   dy0,        1, 1)[0,0] * dstData.size.y;
                        dc.y = dstOrigin.y + dstData.GetHeights(dx0+dw-1,   dy0+dh-1,   1, 1)[0,0] * dstData.size.y;
                        dd.y = dstOrigin.y + dstData.GetHeights(dx0,        dy0+dh-1,   1, 1)[0,0] * dstData.size.y;

                        if (!string.IsNullOrEmpty(ins.name))
                            Handles.Label((da + dc) * 0.5f, ins.name + " (dest)");

                        Handles.color = MoreColors.Mint;
                        Handles.DrawLine(da, db, 2f);
                        Handles.DrawLine(db, dc, 2f);
                        Handles.DrawLine(dc, dd, 2f);
                        Handles.DrawLine(dd, da, 2f);
                    }
                }

                if (editor.paintInserts != null)
                {
                    foreach (var ins in editor.paintInserts)
                    {
                        if (ins.startX == ins.endX && ins.startY == ins.endY) continue;
                        if (ins.lengthCurve == null || ins.crossSectionCurve == null) continue;

                        Vector3 a = editor.HeightmapCoordToWorld(ins.startX, ins.startY);
                        Vector3 b = editor.HeightmapCoordToWorld(ins.endX,   ins.endY);
                        Vector3 baseDir = b - a;
                        baseDir.y = 0;
                        if (baseDir.sqrMagnitude < 1e-4f) continue;
                        baseDir.Normalize();
                        Vector3 perpDir = Vector3.Cross(Vector3.up, baseDir);

                        float halfWidthWorld = ins.width * 0.5f * idxToDist;

                        Color paintColor;
                        if (ins.target == PaintTarget.Marking)
                        {
                            int idx = Mathf.Clamp(ins.markingColorIdx, 0, 15);
                            float h = 0.05f + idx * 0.05f;
                            Color rgb = Color.HSVToRGB(h, 1f, 1f);
                            paintColor = new Color(rgb.r, rgb.g, rgb.b, 0.9f);
                        }
                        else
                        {
                            paintColor = new Color(1f, 1f, 1f, 0.9f);
                        }

                        if (!string.IsNullOrEmpty(ins.name))
                            Handles.Label((a + b) * 0.5f, ins.name);

                        Handles.color = paintColor;
                        Handles.DrawLine(a - perpDir * halfWidthWorld, b - perpDir * halfWidthWorld, 1.5f);
                        Handles.DrawLine(a + perpDir * halfWidthWorld, b + perpDir * halfWidthWorld, 1.5f);

                        if (ins.drawPreview)
                        {
                            const int sampleCount = 200;
                            bool wasAbove = false;
                            for (int i = 0; i < sampleCount; i++)
                            {
                                float t = (float) i / (sampleCount - 1);
                                float val = BetterTerrainEditor.EvalCurveRepeated(ins.lengthCurve, t, ins.repeatScaling, ins.repeats);
                                bool isAbove = val > 0.5f;
                                if (isAbove && !wasAbove)
                                {
                                    Vector3 center = Vector3.Lerp(a, b, t);
                                    Handles.DrawLine(center - perpDir * halfWidthWorld, center + perpDir * halfWidthWorld, 1f);
                                }
                                wasAbove = isAbove;
                            }
                        }
                    }
                }
            };
        }
    }
#endif

    [ExecuteInEditMode]
    public class BetterTerrainEditor : MonoBehaviour
    {

        public List<TerrainCurveInsert>  curveInserts;
        public List<TerrainCircleInsert> circleInserts;
        public List<TerrainMeshInsert>   meshInserts;
        public List<TerrainCopyInsert>   copyInserts;
        public List<TerrainPaintInsert>  paintInserts;

        [HideInInspector] public Terrain terrain;
        TerrainData data;

        [HideInInspector] public int resX;
        [HideInInspector] public int resY;

        public Vector3 Size => data.size;

        public Action gizmo;

        ComputeShader terrainEditShader;
        int curveKernel;
        int mirrorKernel;
        int circleKernel;
        int meshStampKernel;

        RenderTexture heightRT;

        ComputeBuffer curveBakeBuffer;
        ComputeBuffer crossSectionBakeBuffer;
        ComputeBuffer tiltBakeBuffer;

        void OnEnable()
        {
            if (Application.isPlaying)
            {
                Destroy(this);
                return;
            }

#if UNITY_EDITOR
            if (terrainEditShader == null)
            {
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("TerrainEdit t:ComputeShader"))
                {
                    var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null) { terrainEditShader = candidate; break; }
                }
            }
#endif
            if (terrainEditShader == null)
            {
                Debug.LogError("[BetterTerrainEditor] TerrainEdit compute shader not found.");
                enabled = false;
                return;
            }

            curveKernel     = terrainEditShader.FindKernel("Curve");
            mirrorKernel    = terrainEditShader.FindKernel("Mirror");
            circleKernel    = terrainEditShader.FindKernel("Circle");
            meshStampKernel = terrainEditShader.FindKernel("MeshStamp");

            terrain = GetComponent<Terrain>();
            data    = terrain.terrainData;
            resX    = data.heightmapResolution;
            resY    = data.heightmapResolution;

            heightRT = new RenderTexture(resX, resY, 0, RenderTextureFormat.RFloat);
            heightRT.enableRandomWrite = true;
            heightRT.Create();

            Reload();
        }

        void OnDisable()
        {
            if (heightRT != null) { heightRT.Release(); heightRT = null; }
            if (curveBakeBuffer != null) { curveBakeBuffer.Release(); curveBakeBuffer = null; }
            if (crossSectionBakeBuffer != null) { crossSectionBakeBuffer.Release(); crossSectionBakeBuffer = null; }
            if (tiltBakeBuffer != null) { tiltBakeBuffer.Release(); tiltBakeBuffer = null; }
        }

        public void BakeCurve(AnimationCurve curve, int resolution, float repeatScaling, int repeats, float transitionFade = 0f)
        {
            float[] d = new float[resolution];
            for (int i = 0; i < resolution; i++)
                d[i] = curve.EvaluateRepeatedScaling((float)i / resolution, repeatScaling, repeats);

            if (transitionFade > 0 && repeats > 1)
                ApplyRepeatFade(d, resolution, repeatScaling, repeats, transitionFade);

            curveBakeBuffer?.Release();
            curveBakeBuffer = new ComputeBuffer(resolution, sizeof(float));
            curveBakeBuffer.SetData(d);
            terrainEditShader.SetInt("bakedCurveRes", resolution);
        }

        public static void ApplyRepeatFade(float[] d, int resolution, float scalePerRepeat, int repeats, float fade)
        {
            float total = 0, current = 1;
            float[] scales = new float[repeats];
            for (int i = 0; i < repeats; i++) { total += current; scales[i] = current; current *= scalePerRepeat; }
            for (int i = 0; i < repeats; i++) scales[i] /= total;

            float[] boundaries = new float[repeats + 1];
            boundaries[0] = 0;
            for (int i = 0; i < repeats; i++)
                boundaries[i + 1] = boundaries[i] + scales[i];

            float[] original = (float[])d.Clone();

            for (int b = 1; b < repeats; b++)
            {
                int fadePixelsPrev = Mathf.Max(Mathf.RoundToInt(scales[b - 1] * fade * resolution), 1);
                int fadePixelsNext = Mathf.Max(Mathf.RoundToInt(scales[b] * fade * resolution), 1);
                int boundaryIdx   = Mathf.RoundToInt(boundaries[b] * resolution);

                int fadeStart = Mathf.Max(boundaryIdx - fadePixelsPrev, 0);
                int fadeEnd   = Mathf.Min(boundaryIdx + fadePixelsNext, resolution - 1);
                if (fadeStart >= fadeEnd) continue;

                float valStart = original[fadeStart];
                float valEnd   = original[fadeEnd];

                for (int i = fadeStart; i <= fadeEnd; i++)
                {
                    float t = (float)(i - fadeStart) / (fadeEnd - fadeStart);
                    float smooth = t * t * (3f - 2f * t);
                    d[i] = Mathf.Lerp(valStart, valEnd, smooth);
                }
            }
        }

        public void BakeCrossSection(AnimationCurve crossSection, int resolution)
        {
            float[] d = new float[resolution];
            for (int i = 0; i < resolution; i++)
                d[i] = crossSection.Evaluate((float)i / resolution);

            crossSectionBakeBuffer?.Release();
            crossSectionBakeBuffer = new ComputeBuffer(resolution, sizeof(float));
            crossSectionBakeBuffer.SetData(d);
            terrainEditShader.SetInt("bakedCrossSectionRes", resolution);
        }

        public void BakeTilt(AnimationCurve tilt, int resolution)
        {
            float[] d = new float[resolution];
            for (int i = 0; i < resolution; i++)
                d[i] = tilt.Evaluate((float)i / resolution);

            tiltBakeBuffer?.Release();
            tiltBakeBuffer = new ComputeBuffer(resolution, sizeof(float));
            tiltBakeBuffer.SetData(d);
            terrainEditShader.SetInt("bakedTiltRes", resolution);
        }

        public void Reload()
        {
            var rawData = data.GetHeights(0, 0, resX, resY);
            Texture2D temp = new Texture2D(resX, resY, TextureFormat.RFloat, false);
            for (int y = 0; y < resY; y++)
                for (int x = 0; x < resX; x++)
                    temp.SetPixel(x, y, new Color(rawData[y, x], 0, 0, 0));
            temp.Apply();
            Graphics.Blit(temp, heightRT);
            DestroyImmediate(temp);
        }

        public Vector2Int WorldToHeightmapCoord(Vector3 worldPos)
        {
            Vector3 local = worldPos - terrain.transform.position;
            float nx = local.x / data.size.x;
            float nz = local.z / data.size.z;
            int x = Mathf.Clamp(Mathf.RoundToInt(nx * (resX - 1)), 0, resX - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(nz * (resY - 1)), 0, resY - 1);
            return new Vector2Int(x, y);
        }

        public Vector3 HeightmapCoordToWorld(int x, int y)
        {
            float nx = (float)x / (resX - 1);
            float nz = (float)y / (resY - 1);
            float wx = terrain.transform.position.x + nx * data.size.x;
            float wz = terrain.transform.position.z + nz * data.size.z;
            float wy = terrain.transform.position.y + data.GetHeights(x, y, 1, 1)[0, 0] * data.size.y;
            return new Vector3(wx, wy, wz);
        }

        public float GetHeightAtWorldPos(Vector3 worldPos)
        {
            var uv = WorldToHeightmapCoord(worldPos);
            return data.GetHeights(uv.x, uv.y, 1, 1)[0, 0] * data.size.y;
        }

        public float GetHeight(int x, int y)
        {
            return data.GetHeights(x, y, 1, 1)[0, 0] * data.size.y;
        }

        public void SetHeight(int x, int y, float worldHeight)
        {
            float[,] h = new float[1, 1] { { worldHeight / data.size.y } };
            data.SetHeights(x, y, h);
        }

        void OnDrawGizmosSelected()
        {
            gizmo?.Invoke();
        }

        public void Mirror(int mode)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Mirror Terrain");
#endif
            Reload();

            RenderTexture temp = new RenderTexture(resX, resY, 0, RenderTextureFormat.RFloat);
            temp.enableRandomWrite = true;
            temp.Create();

            Graphics.Blit(heightRT, temp);

            terrainEditShader.SetTexture(mirrorKernel, "Heightmap", heightRT);
            terrainEditShader.SetTexture(mirrorKernel, "Src", temp);
            terrainEditShader.SetInt("res", resX);
            terrainEditShader.SetInt("mode", mode);

            int groups = Mathf.CeilToInt(resX / 8f);
            terrainEditShader.Dispatch(mirrorKernel, groups, groups, 1);

            ReadBackRTData();
            temp.Release();
        }

        public void BuildCurve(TerrainCurveInsert insert)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Build Curve: " + insert.name);
#endif
            Reload();
            BakeCurve(insert.curve, insert.bakeResolution, insert.repeatScaling, insert.repeats, insert.repeatTransitionFade);
            BakeCrossSection(insert.crossSection, insert.crossSectionBakeRes);
            BakeTilt(insert.tiltCurve, insert.bakeResolution);

            terrainEditShader.SetTexture(curveKernel, "Heightmap", heightRT);
            terrainEditShader.SetBuffer(curveKernel, "BakedCurve", curveBakeBuffer);
            terrainEditShader.SetBuffer(curveKernel, "BakedCrossSection", crossSectionBakeBuffer);
            terrainEditShader.SetBuffer(curveKernel, "BakedTilt", tiltBakeBuffer);
            terrainEditShader.SetInts("start",  insert.startX, insert.startY);
            terrainEditShader.SetInts("end",    insert.endX,   insert.endY);
            terrainEditShader.SetFloat("topOverride",        insert.heightOverrides.x / data.size.y);
            terrainEditShader.SetFloat("bottomOverride",     insert.heightOverrides.y / data.size.y);
            terrainEditShader.SetFloat("curveWidth",        insert.width);
            terrainEditShader.SetFloat("crossSectionDepth", insert.crossSectionDepth / data.size.y);
            terrainEditShader.SetFloat("crossSectionSideFlatten", insert.crossSectionSideFlatten);
            terrainEditShader.SetFloat("edgeBlend",         insert.edgeBlend);
            terrainEditShader.SetFloat("edgeFalloff",       insert.edgeFalloff);
            terrainEditShader.SetInt("edgeBlendMode",       (int) insert.edgeBlendMode);
            terrainEditShader.SetFloat("tiltDepth",         insert.tiltDepth / data.size.y);
            terrainEditShader.SetInt("res", resX);

            int groups = Mathf.CeilToInt(resX / 8f);
            terrainEditShader.Dispatch(curveKernel, groups, groups, 1);

            ReadBackRTData();
        }

        public void BuildCircle(TerrainCircleInsert insert)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Build Circle: " + insert.name);
#endif
            Reload();
            BakeCurve(insert.radialCurve, insert.bakeResolution, 1f, 1);

            if (crossSectionBakeBuffer == null)
            {
                crossSectionBakeBuffer = new ComputeBuffer(1, sizeof(float));
                crossSectionBakeBuffer.SetData(new float[] { 0f });
                terrainEditShader.SetInt("bakedCrossSectionRes", 1);
            }
            terrainEditShader.SetBuffer(circleKernel, "BakedCrossSection", crossSectionBakeBuffer);

            if (tiltBakeBuffer == null)
            {
                tiltBakeBuffer = new ComputeBuffer(1, sizeof(float));
                tiltBakeBuffer.SetData(new float[] { 0f });
                terrainEditShader.SetInt("bakedTiltRes", 1);
            }
            terrainEditShader.SetBuffer(circleKernel, "BakedTilt", tiltBakeBuffer);

            float normalizedOverride = insert.heightOverride < 0
                ? -1f
                : insert.heightOverride / data.size.y;

            terrainEditShader.SetTexture(circleKernel, "Heightmap", heightRT);
            terrainEditShader.SetBuffer(circleKernel, "BakedCurve", curveBakeBuffer);
            terrainEditShader.SetInts("circleCenter", insert.centerX, insert.centerY);
            terrainEditShader.SetFloat("circleRadius",           insert.radius);
            terrainEditShader.SetFloat("circleHeightOverride",   normalizedOverride);
            terrainEditShader.SetFloat("circleDepth", insert.depth / data.size.y);
            terrainEditShader.SetInt("res", resX);

            int groups = Mathf.CeilToInt(resX / 8f);
            terrainEditShader.Dispatch(circleKernel, groups, groups, 1);

            ReadBackRTData();
        }

        public void BuildMesh(TerrainMeshInsert insert)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Build Mesh: " + insert.name);
#endif
            Reload();

            int bakeRes  = Mathf.Max(insert.bakeResolution, 16);
            var depthTex = new Texture2D(bakeRes, bakeRes, TextureFormat.RFloat, false);

            Quaternion rot = Quaternion.Euler(insert.rotation);
            float meshScale = insert.scale;
            Bounds meshBounds = insert.mesh.bounds;

            Vector3 centerWorld = HeightmapCoordToWorld(insert.centerX, insert.centerY);
            float baseY = insert.heightOffset >= 0
                ? terrain.transform.position.y + insert.heightOffset
                : centerWorld.y;
            Vector3 meshPos = new Vector3(centerWorld.x, baseY, centerWorld.z);

            var tempGO = new GameObject("_MeshStampTemp") { hideFlags = HideFlags.HideAndDontSave };
            tempGO.transform.position = meshPos;
            tempGO.transform.rotation = rot;
            tempGO.transform.localScale = Vector3.one * meshScale;
            var mf = tempGO.AddComponent<MeshFilter>();
            mf.sharedMesh = insert.mesh;
            var mc = tempGO.AddComponent<MeshCollider>();
            mc.sharedMesh = insert.mesh;

            Bounds worldAABB = new Bounds(meshPos + rot * (meshBounds.center * meshScale), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((i & 1) != 0 ? 1f : -1f,
                                (i & 2) != 0 ? 1f : -1f,
                                (i & 4) != 0 ? 1f : -1f));
                worldAABB.Encapsulate(meshPos + rot * (corner * meshScale));
            }

            Vector3 aabbCenter = worldAABB.center;
            float halfSizeX = worldAABB.extents.x;
            float halfSizeZ = worldAABB.extents.z;

            float rayHeight = worldAABB.max.y + 100f;
            float maxDist   = worldAABB.size.y + 200f;
            float terrainBaseY = terrain.transform.position.y;
            float terrainSizeY = data.size.y;

            var pixels = new Color[bakeRes * bakeRes];
            for (int py = 0; py < bakeRes; py++)
            {
                for (int px = 0; px < bakeRes; px++)
                {
                    float u = ((float)px / (bakeRes - 1)) * 2f - 1f;
                    float v = ((float)py / (bakeRes - 1)) * 2f - 1f;
                    Vector3 origin = new Vector3(
                        aabbCenter.x + u * halfSizeX,
                        rayHeight,
                        aabbCenter.z + v * halfSizeZ);

                    float h = 0f;
                    if (mc.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, maxDist))
                    {
                        h = (hit.point.y - terrainBaseY) / terrainSizeY;
                        h = Mathf.Max(h, 0.0001f);
                    }
                    pixels[py * bakeRes + px] = new Color(h, 0, 0, 0);
                }
            }

            DestroyImmediate(tempGO);

            depthTex.SetPixels(pixels);
            depthTex.Apply();

            float idxPerWorldX = (resX - 1) / data.size.x;
            float idxPerWorldZ = (resY - 1) / data.size.z;
            int stampPixelsX = Mathf.CeilToInt(halfSizeX * 2f * idxPerWorldX);
            int stampPixelsZ = Mathf.CeilToInt(halfSizeZ * 2f * idxPerWorldZ);
            var hmCenter = WorldToHeightmapCoord(aabbCenter);
            int hmCX = hmCenter.x;
            int hmCY = hmCenter.y;
            int minX = Mathf.Max(hmCX - stampPixelsX / 2, 0);
            int minY = Mathf.Max(hmCY - stampPixelsZ / 2, 0);
            int sizeX = Mathf.Min(stampPixelsX, resX - minX);
            int sizeY = Mathf.Min(stampPixelsZ, resY - minY);

            if (curveBakeBuffer == null)
            {
                curveBakeBuffer = new ComputeBuffer(1, sizeof(float));
                curveBakeBuffer.SetData(new float[] { 0f });
                terrainEditShader.SetInt("bakedCurveRes", 1);
            }
            if (crossSectionBakeBuffer == null)
            {
                crossSectionBakeBuffer = new ComputeBuffer(1, sizeof(float));
                crossSectionBakeBuffer.SetData(new float[] { 0f });
                terrainEditShader.SetInt("bakedCrossSectionRes", 1);
            }
            if (tiltBakeBuffer == null)
            {
                tiltBakeBuffer = new ComputeBuffer(1, sizeof(float));
                tiltBakeBuffer.SetData(new float[] { 0f });
                terrainEditShader.SetInt("bakedTiltRes", 1);
            }
            terrainEditShader.SetBuffer(meshStampKernel, "BakedCurve", curveBakeBuffer);
            terrainEditShader.SetBuffer(meshStampKernel, "BakedCrossSection", crossSectionBakeBuffer);
            terrainEditShader.SetBuffer(meshStampKernel, "BakedTilt", tiltBakeBuffer);

            terrainEditShader.SetTexture(meshStampKernel, "Heightmap", heightRT);
            terrainEditShader.SetTexture(meshStampKernel, "MeshDepthTex", depthTex);
            terrainEditShader.SetInts("stampMin",  minX, minY);
            terrainEditShader.SetInts("stampSize", sizeX, sizeY);
            terrainEditShader.SetFloat("stampFalloff", insert.blendFalloff);
            terrainEditShader.SetInt("res", resX);

            int groups = Mathf.CeilToInt(resX / 8f);
            terrainEditShader.Dispatch(meshStampKernel, groups, groups, 1);

            DestroyImmediate(depthTex);
            ReadBackRTData();
        }

        public void BuildPaint(TerrainPaintInsert insert)
        {
#if UNITY_EDITOR
            var mat = terrain.materialTemplate;
            if (mat == null || !mat.HasProperty("_SnowMask"))
            {
                Debug.LogError("[BetterTerrainEditor] Terrain material has no _SnowMask property.");
                return;
            }
            var tex = mat.GetTexture("_SnowMask") as Texture2D;
            if (tex == null)
            {
                Debug.LogError("[BetterTerrainEditor] Terrain has no snow mask texture assigned.");
                return;
            }
            if (!tex.isReadable)
            {
                Debug.LogError($"[BetterTerrainEditor] Snow mask '{tex.name}' must be readable.");
                return;
            }
            if (insert.lengthCurve == null || insert.crossSectionCurve == null) return;

            UnityEditor.Undo.RegisterCompleteObjectUndo(tex, "Paint Insert: " + insert.name);

            int maskW = tex.width;
            int maskH = tex.height;
            var pixels = tex.GetPixels();

            Vector2 a = HeightmapToMaskPixel(insert.startX, insert.startY, maskW, maskH);
            Vector2 b = HeightmapToMaskPixel(insert.endX,   insert.endY,   maskW, maskH);

            Vector2 dirVec = b - a;
            if (dirVec.sqrMagnitude < 1e-4f) return;
            float lenMask = dirVec.magnitude;
            Vector2 dir = dirVec / lenMask;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            float hToMask = (float) maskW / (resX - 1);
            float widthMask = insert.width * hToMask;
            float halfWidthMask = widthMask * 0.5f;

            float[] bucketValues = { 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f, 0.45f, 0.50f, 0.55f, 0.60f, 0.65f, 0.70f, 0.75f, 0.80f };
            int colorIdx = Mathf.Clamp(insert.markingColorIdx, 0, bucketValues.Length - 1);
            float markColor = bucketValues[colorIdx];

            float minX = Mathf.Min(a.x, b.x) - halfWidthMask;
            float maxX = Mathf.Max(a.x, b.x) + halfWidthMask;
            float minY = Mathf.Min(a.y, b.y) - halfWidthMask;
            float maxY = Mathf.Max(a.y, b.y) + halfWidthMask;

            int px0 = Mathf.Max(0,         Mathf.FloorToInt(minX));
            int px1 = Mathf.Min(maskW - 1, Mathf.CeilToInt(maxX));
            int py0 = Mathf.Max(0,         Mathf.FloorToInt(minY));
            int py1 = Mathf.Min(maskH - 1, Mathf.CeilToInt(maxY));

            for (int py = py0; py <= py1; py++)
            {
                for (int px = px0; px <= px1; px++)
                {
                    Vector2 p = new Vector2(px + 0.5f, py + 0.5f);
                    Vector2 toP = p - a;
                    float along = Vector2.Dot(toP, dir) / lenMask;
                    if (along < 0f || along > 1f) continue;

                    float across = Vector2.Dot(toP, perp);
                    if (Mathf.Abs(across) > halfWidthMask) continue;
                    float ct = (across + halfWidthMask) / widthMask;

                    float lengthVal = EvalCurveRepeated(insert.lengthCurve, along, insert.repeatScaling, insert.repeats);
                    float crossVal  = insert.crossSectionCurve.Evaluate(ct);
                    float cov = Mathf.Clamp01(lengthVal * crossVal * insert.coverage);
                    if (cov <= 0f) continue;

                    int idx = py * maskW + px;
                    var c = pixels[idx];

                    if (insert.target == PaintTarget.Snow)
                    {
                        float t = insert.erase ? 0f : 1f;
                        c.r = Mathf.Lerp(c.r, t, cov);
                    }
                    else
                    {
                        if (insert.erase)
                        {
                            c.b = Mathf.Lerp(c.b, 0f, cov);
                        }
                        else
                        {
                            c.g = markColor;
                            c.b = Mathf.Lerp(c.b, 1f, cov);
                        }
                    }

                    pixels[idx] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            UnityEditor.EditorUtility.SetDirty(tex);
#endif
        }

        Vector2 HeightmapToMaskPixel(int hx, int hy, int maskW, int maskH)
        {
            float u = (float) hx / (resX - 1);
            float v = (float) hy / (resY - 1);
            return new Vector2(u * maskW, (1f - v) * maskH);
        }

        public static float EvalCurveRepeated(AnimationCurve curve, float t, float scalePerRepeat, int repeats)
        {
            if (curve == null) return 0f;
            if (repeats <= 1) return curve.Evaluate(t);

            float[] scales = new float[repeats];
            float total = 0f;
            float current = 1f;
            for (int i = 0; i < repeats; i++)
            {
                scales[i] = current;
                total += current;
                current *= scalePerRepeat;
            }
            for (int i = 0; i < repeats; i++) scales[i] /= total;

            float cumStart = 0f;
            int section = repeats - 1;
            for (int i = 0; i < repeats; i++)
            {
                float cumEnd = cumStart + scales[i];
                if (t < cumEnd) { section = i; break; }
                cumStart = cumEnd;
            }
            float sectionEnd = cumStart + scales[section];
            float sectionT = sectionEnd > cumStart ? Mathf.InverseLerp(cumStart, sectionEnd, t) : 0f;
            return curve.Evaluate(sectionT);
        }

        public void BuildCopy(TerrainCopyInsert insert)
        {
            if (insert.sourceTerrain == null) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Build Copy: " + insert.name);
#endif

            var srcData = insert.sourceTerrain.terrainData;
            var dstData = terrain.terrainData;

            int srcRes = srcData.heightmapResolution;
            int dstRes = dstData.heightmapResolution;

            int sx0 = Mathf.Clamp(insert.srcStartX, 0, srcRes - 1);
            int sy0 = Mathf.Clamp(insert.srcStartY, 0, srcRes - 1);
            int sw  = Mathf.Clamp(insert.srcSizeX,  1, srcRes - sx0);
            int sh  = Mathf.Clamp(insert.srcSizeY,  1, srcRes - sy0);

            float srcPxX = srcData.size.x / (srcRes - 1);
            float srcPxZ = srcData.size.z / (srcRes - 1);
            float worldW = (sw - 1) * srcPxX;
            float worldD = (sh - 1) * srcPxZ;

            float dstPxX = dstData.size.x / (dstRes - 1);
            float dstPxZ = dstData.size.z / (dstRes - 1);
            int dw = Mathf.Max(1, Mathf.RoundToInt(worldW / dstPxX) + 1);
            int dh = Mathf.Max(1, Mathf.RoundToInt(worldD / dstPxZ) + 1);

            int dx0 = Mathf.Clamp(insert.dstStartX, 0, dstRes - 1);
            int dy0 = Mathf.Clamp(insert.dstStartY, 0, dstRes - 1);
            dw = Mathf.Min(dw, dstRes - dx0);
            dh = Mathf.Min(dh, dstRes - dy0);
            if (dw < 1 || dh < 1) return;

            float[,] srcHeights = srcData.GetHeights(sx0, sy0, sw, sh);
            float[,] dstHeights = dstData.GetHeights(dx0, dy0, dw, dh);

            float srcSizeY = srcData.size.y;
            float dstSizeY = dstData.size.y;
            float ho   = insert.heightOffset;
            float fall = insert.blendFalloff;

            for (int y = 0; y < dh; y++)
            {
                float sy = (dh <= 1) ? 0 : (float) y / (dh - 1) * (sh - 1);
                int sy0i = Mathf.FloorToInt(sy);
                int sy1i = Mathf.Min(sy0i + 1, sh - 1);
                float ty = sy - sy0i;

                for (int x = 0; x < dw; x++)
                {
                    float sx = (dw <= 1) ? 0 : (float) x / (dw - 1) * (sw - 1);
                    int sx0i = Mathf.FloorToInt(sx);
                    int sx1i = Mathf.Min(sx0i + 1, sw - 1);
                    float tx = sx - sx0i;

                    float h00 = srcHeights[sy0i, sx0i];
                    float h01 = srcHeights[sy0i, sx1i];
                    float h10 = srcHeights[sy1i, sx0i];
                    float h11 = srcHeights[sy1i, sx1i];
                    float hSrc = Mathf.Lerp(Mathf.Lerp(h00, h01, tx), Mathf.Lerp(h10, h11, tx), ty);

                    float worldY = hSrc * srcSizeY + ho;
                    float hDst   = worldY / dstSizeY;

                    float uX = (dw <= 1) ? 0.5f : (float) x / (dw - 1);
                    float uY = (dh <= 1) ? 0.5f : (float) y / (dh - 1);
                    float edge = Mathf.Min(Mathf.Min(uX, 1f - uX), Mathf.Min(uY, 1f - uY));
                    float w    = fall > 0 ? Mathf.SmoothStep(0, fall, edge) : 1f;

                    dstHeights[y, x] = Mathf.Lerp(dstHeights[y, x], hDst, w);
                }
            }

            dstData.SetHeights(dx0, dy0, dstHeights);
        }

        void ReadBackRTData()
        {
            AsyncGPUReadback.Request(heightRT, 0, request =>
            {
                if (request.hasError) return;

                var raw = request.GetData<float>();
                float[,] heights = new float[resX, resY];
                for (int y = 0; y < resY; y++)
                    for (int x = 0; x < resX; x++)
                        heights[y, x] = raw[y * resX + x];

                data.SetHeightsDelayLOD(0, 0, heights);
                terrain.terrainData.SyncHeightmap();
            });
        }

    }

}
