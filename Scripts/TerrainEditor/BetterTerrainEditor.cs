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

        // terrain picking state — static so the SceneView callback can access it
        public static SerializedProperty pickTargetXProp;
        public static SerializedProperty pickTargetYProp;
        public static BetterTerrainEditor pickEditor;
        public static string pickLabel;

        // paint state
        static bool    painting;
        static float   paintBrushSize = 50f;
        static float   paintOpacity   = 1f;
        static float   paintHardness  = 0.5f;
        static Texture2D paintTexture;
        static BetterTerrainEditor paintEditor;

        void OnEnable()
        {
            SceneView.beforeSceneGui -= HandlePickSceneGUI;
            SceneView.beforeSceneGui += HandlePickSceneGUI;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            SetupLists();
        }

        void SetupLists()
        {
            curveList  = MakeList("curveInserts",  "Curve Inserts",  () => new TerrainCurveInsert());
            circleList = MakeList("circleInserts", "Circle Inserts", () => new TerrainCircleInsert());
            meshList   = MakeList("meshInserts",   "Mesh Inserts",   () => new TerrainMeshInsert());
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

        // Reads public instance fields from a freshly-constructed default instance and
        // writes them into the matching SerializedProperty children.
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
                }
            }
        }

        void OnDisable()
        {
            SceneView.beforeSceneGui -= HandlePickSceneGUI;
            SceneView.duringSceneGui -= OnSceneGUI;
            pickTargetXProp = null;
            pickTargetYProp = null;
            if (painting) StopPainting();
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (painting) HandlePaintSceneGUI(sceneView);
            else          DrawPickLabel(sceneView);
        }

        // Runs before Editor.OnSceneGUI — claims clicks before the Terrain editor can
        static void HandlePickSceneGUI(SceneView sceneView)
        {
            if (pickTargetXProp == null || pickEditor == null) return;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
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
                Event.current.Use();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                pickTargetXProp = null;
                pickTargetYProp = null;
                pickLabel       = null;
                Event.current.Use();
            }
        }

        // Runs during normal scene GUI — draws the pick label
        static void DrawPickLabel(SceneView sceneView)
        {
            if (pickTargetXProp == null || pickEditor == null) return;

            int controlId = GUIUtility.GetControlID("TerrainPicker".GetHashCode(), FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);
            sceneView.Repaint();

            Handles.BeginGUI();
            var mousePos = Event.current.mousePosition;
            GUI.Label(new Rect(mousePos.x + 15, mousePos.y - 10, 200, 20),
                $"Pick {pickLabel} (click terrain)", EditorStyles.whiteBoldLabel);
            Handles.EndGUI();
        }

        // ── Paint Scene View ──────────────────────────────────────────────────
        static void HandlePaintSceneGUI(SceneView sceneView)
        {
            if (paintEditor == null || paintEditor.terrain == null || paintTexture == null)
            {
                StopPainting();
                return;
            }

            int controlId = GUIUtility.GetControlID("TerrainPainter".GetHashCode(), FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);
            sceneView.Repaint();

            var evt = Event.current;
            bool erase = evt.shift;

            // Draw brush cursor
            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            var col = paintEditor.terrain.GetComponent<TerrainCollider>();
            if (col != null && col.Raycast(ray, out RaycastHit hit, 50000f))
            {
                float worldRadius = paintBrushSize * paintEditor.terrain.terrainData.size.x
                    / paintTexture.width;
                Handles.color = erase ? new Color(1f, 0.3f, 0.3f, 0.6f) : new Color(1f, 1f, 1f, 0.6f);
                Handles.DrawWireDisc(hit.point, hit.normal, worldRadius);
            }

            Handles.BeginGUI();
            var mp = evt.mousePosition;
            string label = erase ? "Erase (release Shift to paint)" : "Paint (hold Shift to erase)";
            GUI.Label(new Rect(mp.x + 15, mp.y - 10, 300, 20), label, EditorStyles.whiteBoldLabel);
            Handles.EndGUI();

            // Register undo at stroke start
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                Undo.RegisterCompleteObjectUndo(paintTexture, "Paint Snow Mask");
            }

            // Paint on drag or click
            bool shouldPaint = (evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag)
                && evt.button == 0;
            if (shouldPaint)
            {
                ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                if (col != null && col.Raycast(ray, out RaycastHit paintHit, 50000f))
                {
                    Vector3 local = paintHit.point - paintEditor.terrain.transform.position;
                    Vector3 size  = paintEditor.terrain.terrainData.size;
                    float u = local.x / size.x;
                    float v = local.z / size.z;
                    PaintAt(u, v, erase);
                }
                evt.Use();
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                StopPainting();
                evt.Use();
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
                    float current = pixels[idx].r;
                    float target = erase ? 0f : 1f;
                    pixels[idx] = new Color(Mathf.Lerp(current, target, strength), 0, 0, 1);
                }
            }

            paintTexture.SetPixels(x0, y0, w, y1 - y0 + 1, pixels);
            paintTexture.Apply();

            // Force terrain to pick up the changed texture
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

            // Verify Get/SetPixels will work (fails on compressed formats)
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
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw the two lists with proper add-callbacks for default values
            curveList.DoLayoutList();
            EditorGUILayout.Space(4);
            circleList.DoLayoutList();
            EditorGUILayout.Space(4);
            meshList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();

            editor = (BetterTerrainEditor)target;

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

            EditorGUILayout.Space(8);

            mirrorMode = EditorGUILayout.Popup("Mirror Mode", mirrorMode,
                new[] { "Horizontal", "Vertical", "Both" });
            if (GUILayout.Button("Mirror"))
                editor.Mirror(mirrorMode);

            // ── Paint Terrain ─────────────────────────────────────────────────────
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
                    }
                }
                EditorGUI.EndDisabledGroup();
                if (!hasMask)
                    EditorGUILayout.HelpBox("Terrain material has no _SnowMask property.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Painting active — click/drag on terrain in Scene View.\nHold Shift to erase. Esc to stop.",
                    MessageType.Info);
                paintBrushSize = EditorGUILayout.Slider("Brush Size", paintBrushSize, 1f, 500f);
                paintOpacity   = EditorGUILayout.Slider("Opacity",    paintOpacity,   0.01f, 1f);
                paintHardness  = EditorGUILayout.Slider("Hardness",   paintHardness,  0f, 1f);
                if (GUILayout.Button("Stop Painting"))
                    StopPainting();
            }

            editor.gizmo = () =>
            {
                if (editor.terrain == null || editor.resX <= 1) return;

                float idxToDist = editor.terrain.terrainData.size.x / (editor.resX - 1);

                // ── Curve inserts ────────────────────────────────────────────────
                if (editor.curveInserts != null)
                {
                    foreach (var ins in editor.curveInserts)
                    {
                        if (!ins.enabled) continue;
                        if (ins.startX == ins.endX && ins.startY == ins.endY) continue;
                        if (ins.curve == null || ins.curve.length == 0) continue;

                        var startWorld = editor.HeightmapCoordToWorld(ins.startX, ins.startY);
                        var endWorld   = editor.HeightmapCoordToWorld(ins.endX,   ins.endY);

                        if (!string.IsNullOrEmpty(ins.name))
                            Handles.Label((startWorld + endWorld) * 0.5f, ins.name);

                        if (ins.drawPreview)
                        {
                            var a = startWorld;
                            var b = endWorld;
                            if (ins.heightOverrides.x != -1) a.y = editor.terrain.transform.position.y + ins.heightOverrides.x;
                            if (ins.heightOverrides.y != -1) b.y = editor.terrain.transform.position.y + ins.heightOverrides.y;

                            int res = Mathf.Max(ins.bakeResolution, 1);
                            var points = new List<Vector3>(res);
                            for (int i = 0; i < res; ++i)
                            {
                                float t = (float)i / res;
                                var   p = Vector3.Lerp(a, b, t);
                                points.Add(new Vector3(p.x,
                                    Mathf.Lerp(a.y, b.y, 1 - ins.curve.EvaluateRepeatedScaling(t, ins.repeatScaling, ins.repeats)),
                                    p.z));
                            }

                            var fwd   = (b - a).normalized;
                            var right = Vector3.Cross(fwd, Vector3.up);
                            float halfW = ins.width * 0.5f * idxToDist;

                            Gizmos.color = MoreColors.Mint;
                            Utility.DrawLineGizmo(points, sphereRadius: 0.5f);

                            float leftCrossH  = ins.crossSection != null ? ins.crossSection.Evaluate(0f) * ins.crossSectionDepth : 0f;
                            float rightCrossH = ins.crossSection != null ? ins.crossSection.Evaluate(1f) * ins.crossSectionDepth : 0f;

                            var leftPts  = new List<Vector3>(points.Count);
                            var rightPts = new List<Vector3>(points.Count);
                            foreach (var p in points)
                            {
                                leftPts.Add(p  + (-right * halfW) + Vector3.up * leftCrossH);
                                rightPts.Add(p + ( right * halfW) + Vector3.up * rightCrossH);
                            }

                            Gizmos.color = MoreColors.Indigo;
                            Utility.DrawLineGizmo(leftPts,  sphereRadius: 0.5f);
                            Utility.DrawLineGizmo(rightPts, sphereRadius: 0.5f);

                            if (ins.crossSectionDepth != 0 && ins.crossSection != null)
                            {
                                Gizmos.color = MoreColors.Forest;
                                const int crossPreviews = 8;
                                const int crossRes      = 16;
                                var crossPts = new List<Vector3>(crossRes + 1);
                                for (int ci = 0; ci <= crossPreviews; ci++)
                                {
                                    int idx = Mathf.Clamp(ci * points.Count / crossPreviews, 0, points.Count - 1);
                                    var center = points[idx];
                                    crossPts.Clear();
                                    for (int j = 0; j <= crossRes; j++)
                                    {
                                        float ct = (float)j / crossRes;
                                        float crossH = ins.crossSection.Evaluate(ct) * ins.crossSectionDepth;
                                        float lateralOffset = (ct - 0.5f) * ins.width * idxToDist;
                                        crossPts.Add(center + right * lateralOffset + Vector3.up * crossH);
                                    }
                                    Utility.DrawLineGizmo(crossPts, sphereRadius: 0.2f);
                                }
                            }
                        }
                        else
                        {
                            Gizmos.color = MoreColors.Violet;
                            Utility.DrawLineGizmo(new List<Vector3> { startWorld, endWorld }, sphereRadius: 0.5f);
                        }
                    }
                }

                // ── Circle inserts ────────────────────────────────────────────────
                if (editor.circleInserts != null)
                {
                    const int ringRes = 48;
                    var ringPts = new List<Vector3>(ringRes + 1);

                    foreach (var ins in editor.circleInserts)
                    {
                        if (!ins.enabled) continue;
                        if (ins.radius <= 0) continue;

                        var   centerWorld = editor.HeightmapCoordToWorld(ins.centerX, ins.centerY);
                        float radiusWorld = ins.radius * idxToDist;

                        if (!string.IsNullOrEmpty(ins.name))
                            Handles.Label(centerWorld, ins.name);

                        // Ring at edge radius — sample terrain height so it follows the surface
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

                            // Cross-section profile in 4 directions (east/west/north/south)
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

                // ── Mesh inserts ─────────────────────────────────────────────────
                if (editor.meshInserts != null)
                {
                    foreach (var ins in editor.meshInserts)
                    {
                        if (!ins.enabled || ins.mesh == null) continue;

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

                        // Bounding box outline
                        Gizmos.color = MoreColors.Violet;
                        var bounds = ins.mesh.bounds;
                        Gizmos.matrix = Matrix4x4.TRS(pos, rot, scl);
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                        Gizmos.matrix = Matrix4x4.identity;
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
        }

        public void BakeCurve(AnimationCurve curve, int resolution, float repeatScaling, int repeats)
        {
            float[] d = new float[resolution];
            for (int i = 0; i < resolution; i++)
                d[i] = curve.EvaluateRepeatedScaling((float)i / resolution, repeatScaling, repeats);

            curveBakeBuffer?.Release();
            curveBakeBuffer = new ComputeBuffer(resolution, sizeof(float));
            curveBakeBuffer.SetData(d);
            terrainEditShader.SetInt("bakedCurveRes", resolution);
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
            BakeCurve(insert.curve, insert.bakeResolution, insert.repeatScaling, insert.repeats);
            BakeCrossSection(insert.crossSection, insert.crossSectionBakeRes);

            terrainEditShader.SetTexture(curveKernel, "Heightmap", heightRT);
            terrainEditShader.SetBuffer(curveKernel, "BakedCurve", curveBakeBuffer);
            terrainEditShader.SetBuffer(curveKernel, "BakedCrossSection", crossSectionBakeBuffer);
            terrainEditShader.SetInts("start",  insert.startX, insert.startY);
            terrainEditShader.SetInts("end",    insert.endX,   insert.endY);
            terrainEditShader.SetFloat("topOverride",        insert.heightOverrides.x / data.size.y);
            terrainEditShader.SetFloat("bottomOverride",     insert.heightOverrides.y / data.size.y);
            terrainEditShader.SetFloat("curveWidth",        insert.width);
            terrainEditShader.SetFloat("crossSectionDepth", insert.crossSectionDepth / data.size.y);
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

            // Ensure BakedCrossSection is bound — Circle kernel doesn't use it, but
            // Unity's validation requires all globally-declared buffers to be bound.
            if (crossSectionBakeBuffer == null)
            {
                crossSectionBakeBuffer = new ComputeBuffer(1, sizeof(float));
                crossSectionBakeBuffer.SetData(new float[] { 0f });
                terrainEditShader.SetInt("bakedCrossSectionRes", 1);
            }
            terrainEditShader.SetBuffer(circleKernel, "BakedCrossSection", crossSectionBakeBuffer);

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

            // Position the mesh exactly as the gizmo shows it
            Vector3 centerWorld = HeightmapCoordToWorld(insert.centerX, insert.centerY);
            float baseY = insert.heightOffset >= 0
                ? terrain.transform.position.y + insert.heightOffset
                : centerWorld.y;
            Vector3 meshPos = new Vector3(centerWorld.x, baseY, centerWorld.z);

            // Create temp MeshCollider at the exact gizmo position
            var tempGO = new GameObject("_MeshStampTemp") { hideFlags = HideFlags.HideAndDontSave };
            tempGO.transform.position = meshPos;
            tempGO.transform.rotation = rot;
            tempGO.transform.localScale = Vector3.one * meshScale;
            var mf = tempGO.AddComponent<MeshFilter>();
            mf.sharedMesh = insert.mesh;
            var mc = tempGO.AddComponent<MeshCollider>();
            mc.sharedMesh = insert.mesh;

            // Conservative AABB for the rotated/scaled mesh
            Vector3 scaledExtents = meshBounds.extents * meshScale;
            float maxExtent = Mathf.Max(scaledExtents.x, Mathf.Max(scaledExtents.y, scaledExtents.z));
            float halfSize = maxExtent;

            float rayHeight = meshPos.y + maxExtent * 2 + 100f;
            float maxDist   = maxExtent * 4 + 200f;
            float terrainBaseY = terrain.transform.position.y;
            float terrainSizeY = data.size.y;

            // Raycast from above — record normalised terrain height where mesh is hit
            var pixels = new Color[bakeRes * bakeRes];
            for (int py = 0; py < bakeRes; py++)
            {
                for (int px = 0; px < bakeRes; px++)
                {
                    float u = ((float)px / (bakeRes - 1)) * 2f - 1f;
                    float v = ((float)py / (bakeRes - 1)) * 2f - 1f;
                    Vector3 origin = new Vector3(
                        meshPos.x + u * halfSize,
                        rayHeight,
                        meshPos.z + v * halfSize);

                    float h = 0f; // 0 = no hit
                    if (mc.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, maxDist))
                    {
                        // Convert world Y to normalised terrain height (what the heightmap stores)
                        h = (hit.point.y - terrainBaseY) / terrainSizeY;
                        h = Mathf.Max(h, 0.0001f); // keep above 0 so kernel knows there was a hit
                    }
                    pixels[py * bakeRes + px] = new Color(h, 0, 0, 0);
                }
            }

            DestroyImmediate(tempGO);

            depthTex.SetPixels(pixels);
            depthTex.Apply();

            // ── Compute the stamp footprint in heightmap coords ───────────────
            float idxPerWorld = (resX - 1) / data.size.x;
            int stampPixels = Mathf.CeilToInt(halfSize * 2f * idxPerWorld);
            int hmCX = insert.centerX;
            int hmCY = insert.centerY;
            int minX = Mathf.Max(hmCX - stampPixels / 2, 0);
            int minY = Mathf.Max(hmCY - stampPixels / 2, 0);
            int sizeX = Mathf.Min(stampPixels, resX - minX);
            int sizeY = Mathf.Min(stampPixels, resY - minY);

            // ── Dispatch compute ──────────────────────────────────────────────
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
            terrainEditShader.SetBuffer(meshStampKernel, "BakedCurve", curveBakeBuffer);
            terrainEditShader.SetBuffer(meshStampKernel, "BakedCrossSection", crossSectionBakeBuffer);

            terrainEditShader.SetTexture(meshStampKernel, "Heightmap", heightRT);
            terrainEditShader.SetTexture(meshStampKernel, "MeshDepthTex", depthTex);
            terrainEditShader.SetInt("meshDepthRes", bakeRes);
            terrainEditShader.SetInts("stampMin",  minX, minY);
            terrainEditShader.SetInts("stampSize", sizeX, sizeY);
            terrainEditShader.SetFloat("stampFalloff", insert.blendFalloff);
            terrainEditShader.SetInt("res", resX);

            int groups = Mathf.CeilToInt(resX / 8f);
            terrainEditShader.Dispatch(meshStampKernel, groups, groups, 1);

            DestroyImmediate(depthTex);
            ReadBackRTData();
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
