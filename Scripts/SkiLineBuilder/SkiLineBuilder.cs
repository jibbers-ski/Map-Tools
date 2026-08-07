using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditorInternal;

    [CustomEditor(typeof(SkiLineBuilder))]
    public class SkiLineBuilderEditor : Editor
    {
        SkiLineBuilder builder;
        ReorderableList lineList;

        int editLine = -1;
        int selectedNode = -1;
        bool addingNodes;

        void OnEnable()
        {
            builder = (SkiLineBuilder)target;
            lineList = new ReorderableList(serializedObject, serializedObject.FindProperty("lines"),
                draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);

            lineList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Ski Lines");

            lineList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = lineList.serializedProperty.GetArrayElementAtIndex(index);
                string nm = element.FindPropertyRelative("name").stringValue;
                string suffix = index == editLine ? "  (editing)" : "";
                EditorGUI.PropertyField(rect, element, new GUIContent($"[{index}]  {nm}{suffix}"), true);
            };

            lineList.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(lineList.serializedProperty.GetArrayElementAtIndex(index), true);

            lineList.onAddCallback = list =>
            {
                int newIdx = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                list.index = newIdx;
                SetLineDefaults(list.serializedProperty.GetArrayElementAtIndex(newIdx), newIdx);
            };

            lineList.onReorderCallback = list => { editLine = -1; selectedNode = -1; };

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            if (builder != null && builder.autoApply && builder.terrain != null)
                builder.ScheduleApply();
            SceneView.RepaintAll();
        }

        static void SetLineDefaults(SerializedProperty element, int index)
        {
            element.FindPropertyRelative("name").stringValue = "Line " + index;
            element.FindPropertyRelative("enabled").boolValue = true;
            element.FindPropertyRelative("drawPreview").boolValue = true;
            element.FindPropertyRelative("width").floatValue = 14f;
            element.FindPropertyRelative("crossSection").animationCurveValue = AnimationCurve.Constant(0, 1, 0);
            element.FindPropertyRelative("crossSectionDepth").floatValue = 0f;
            element.FindPropertyRelative("sideFlatten").floatValue = 0.15f;
            element.FindPropertyRelative("autoBank").floatValue = 0f;
            element.FindPropertyRelative("edgeBlend").floatValue = 0.25f;
            element.FindPropertyRelative("edgeFalloff").floatValue = 1f;
            element.FindPropertyRelative("endBlend").floatValue = 10f;
            element.FindPropertyRelative("bakeResolution").intValue = 1024;
            element.FindPropertyRelative("nodes").arraySize = 0;
            element.FindPropertyRelative("features").arraySize = 0;
            element.isExpanded = false;
        }

        public override void OnInspectorGUI()
        {
            builder = (SkiLineBuilder)target;

            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            lineList.DoLayoutList();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoApply"),
                new GUIContent("Auto Apply", "Re-apply the lines automatically whenever a line or feature changes."));
            serializedObject.ApplyModifiedProperties();
            bool changed = EditorGUI.EndChangeCheck();

            if (builder.lines == null || builder.lines.Count == 0)
                EditorGUILayout.HelpBox(
                    "Add a ski line, select it, press Edit Selected Line, then click the terrain to lay out the line. " +
                    "Add features (Kicker, Table, Roller, Gap) to shape jumps along it.",
                    MessageType.Info);

            int sel = lineList.index;
            bool hasSel = builder.lines != null && sel >= 0 && sel < builder.lines.Count;

            EditorGUILayout.Space(6);
            if (hasSel)
            {
                bool editing = editLine == sel;
                GUI.color = editing ? Color.yellow : Color.white;
                if (GUILayout.Button(editing ? "Stop Editing  (Esc)" : "Edit Selected Line"))
                {
                    editLine = editing ? -1 : sel;
                    addingNodes = !editing && (builder.lines[sel].nodes == null || builder.lines[sel].nodes.Count == 0);
                    selectedNode = -1;
                    if (!editing && SceneView.lastActiveSceneView != null)
                        SceneView.lastActiveSceneView.Focus();
                    SceneView.RepaintAll();
                }
                GUI.color = Color.white;

                if (editLine == sel)
                {
                    addingNodes = GUILayout.Toggle(addingNodes, "Add Nodes (click terrain to append)", "Button");
                    EditorGUILayout.HelpBox(
                        "Click sphere: select node  ·  Drag selected sphere: slide along terrain\n" +
                        "Arrow handles: exact XYZ  ·  X: delete selected  ·  Hold Ctrl: click node deletes it\n" +
                        "Click terrain: append node (Add Nodes mode)  ·  Ctrl+Click terrain: append node\n" +
                        "Shift+Click near line: insert node\n" +
                        "Feature cone: drag along line  ·  Feature cube: drag left/right  ·  Esc: stop",
                        MessageType.Info);

                    if (selectedNode >= 0 && GUILayout.Button("Delete Selected Node"))
                        DeleteSelectedNode();
                    if (GUILayout.Button(new GUIContent("Drape Nodes To Ground",
                        "Snap every node's height onto the pre-line terrain (the session snapshot, or the live terrain when no session is active).")))
                        DrapeLine(builder.lines[sel]);
                }
            }

            EditorGUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(builder.terrain == null || !builder.isActiveAndEnabled);
            if (GUILayout.Button(new GUIContent("Apply Lines",
                    "Carve all enabled lines and paint their stripes now, registering an undo step."), GUILayout.Height(28)))
                builder.Apply(true);
            EditorGUI.EndDisabledGroup();

            string maskProblem = builder.MaskProblem();
            if (maskProblem != null)
                EditorGUILayout.HelpBox("Paint stripes cannot render: " + maskProblem, MessageType.Warning);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
            bool session = builder.SessionActive;
            EditorGUILayout.HelpBox(session
                ? "Editing session active — the pre-line terrain is snapshotted into Library (temporary, not a project asset). " +
                  "Lines re-apply on top of that snapshot non-destructively while you edit.\n" +
                  "Bake makes the carved lines permanent ground and ends the session; Discard reverts the terrain and markings."
                : "No active session — the first line edit snapshots the terrain automatically and live editing begins.",
                session ? MessageType.None : MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!session);
            if (GUILayout.Button(new GUIContent("Bake Into Terrain",
                "End the session: the carved lines become permanent ground and the snapshot is deleted.")))
            {
                if (EditorUtility.DisplayDialog("Bake Ski Lines",
                    "Accepts the current terrain (with lines carved) as the permanent ground and ends the session.\n\n" +
                    "The pre-line snapshot is deleted — baking cannot be undone. Lines stay editable; the next edit " +
                    "starts a new session on top of the baked result.",
                    "Bake", "Cancel"))
                    builder.BakeSession();
            }
            if (GUILayout.Button(new GUIContent("Discard Session",
                "End the session: the terrain and markings revert to the snapshot and all lines are disabled (their data stays).")))
            {
                if (EditorUtility.DisplayDialog("Discard Ski Line Session",
                    "Restores the terrain and snow-mask markings to the session snapshot and disables all lines " +
                    "(their data is kept). The terrain restore is undoable; the discarded session is not.",
                    "Discard", "Cancel"))
                {
                    Undo.RecordObject(builder, "Discard Ski Line Session");
                    builder.DiscardSession();
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (changed && builder.autoApply && builder.terrain != null)
                builder.ScheduleApply();
        }

        void DeleteSelectedNode()
        {
            if (editLine < 0 || editLine >= builder.lines.Count) return;
            var line = builder.lines[editLine];
            if (line.nodes == null || selectedNode < 0 || selectedNode >= line.nodes.Count) return;
            Undo.RecordObject(builder, "Delete Ski Line Node");
            line.nodes.RemoveAt(selectedNode);
            selectedNode = -1;
            MarkChanged();
        }

        void DrapeLine(SkiLine line)
        {
            if (line.nodes == null) return;
            Undo.RecordObject(builder, "Drape Ski Line Nodes");
            foreach (var node in line.nodes)
                node.position.y = builder.SampleBaselineHeight(node.position.x, node.position.z);
            MarkChanged();
        }

        void MarkChanged()
        {
            EditorUtility.SetDirty(builder);
            if (builder.autoApply) builder.ScheduleApply();
            SceneView.RepaintAll();
        }

        void OnSceneGUI()
        {
            builder = (SkiLineBuilder)target;
            if (builder.terrain == null || builder.lines == null) return;

            DrawPreviews();

            if (editLine < 0 || editLine >= builder.lines.Count) return;
            var line = builder.lines[editLine];

            HandleEditInput(line);
            DrawNodeHandles(line);
            DrawFeatureHandles(line);
        }

        void HandleEditInput(SkiLine line)
        {
            int controlId = GUIUtility.GetControlID("SkiLineBuilder".GetHashCode(), FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            var evt = Event.current;

            if (evt.type == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    if (addingNodes) addingNodes = false;
                    else editLine = -1;
                    evt.Use();
                    Repaint();
                    return;
                }
                if (evt.keyCode == KeyCode.X && selectedNode >= 0)
                {
                    DeleteSelectedNode();
                    evt.Use();
                    return;
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt
                && HandleUtility.nearestControl == controlId)
            {
                bool append = addingNodes || evt.control;
                bool insert = evt.shift;
                if (!append && !insert)
                {
                    evt.Use();
                    return;
                }

                var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                var collider = builder.terrain.GetComponent<TerrainCollider>();
                if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
                {
                    Undo.RecordObject(builder, "Add Ski Line Node");
                    Vector3 local = builder.ClampLocal(hit.point - builder.terrain.transform.position);
                    if (line.nodes == null) line.nodes = new List<SkiLineNode>();

                    if (insert && line.nodes.Count >= 2)
                    {
                        var bake = builder.GetBake(line);
                        if (bake != null)
                        {
                            float d = SkiLineSpline.NearestDistance(bake, local, out _);
                            int idx = SkiLineSpline.IndexAtDistance(bake, d);
                            int seg = Mathf.Clamp((int)bake.nodeParam[idx], 0, line.nodes.Count - 2);
                            line.nodes.Insert(seg + 1, new SkiLineNode { position = local });
                            selectedNode = seg + 1;
                        }
                    }
                    else
                    {
                        line.nodes.Add(new SkiLineNode { position = local });
                        selectedNode = line.nodes.Count - 1;
                    }
                    MarkChanged();
                }
                evt.Use();
            }
        }

        void DrawNodeHandles(SkiLine line)
        {
            if (line.nodes == null) return;
            Vector3 tpos = builder.terrain.transform.position;
            var evt = Event.current;
            bool deleteMode = evt.control;

            Handles.color = MoreColors.Slate;
            for (int i = 1; i < line.nodes.Count; i++)
                Handles.DrawDottedLine(tpos + line.nodes[i - 1].position, tpos + line.nodes[i].position, 4f);

            for (int i = 0; i < line.nodes.Count; i++)
            {
                var node = line.nodes[i];
                Vector3 world = tpos + node.position;
                float size = HandleUtility.GetHandleSize(world) * 0.15f;
                bool isSelected = i == selectedNode;

                Handles.color = deleteMode ? MoreColors.Crimson : isSelected ? MoreColors.JibbersOrange : MoreColors.Azure;
                Handles.DrawLine(world, world + Vector3.up * size * 5f, 2f);

                if (deleteMode)
                {
                    if (Handles.Button(world, Quaternion.identity, size, size * 2f, Handles.SphereHandleCap))
                    {
                        Undo.RecordObject(builder, "Delete Ski Line Node");
                        line.nodes.RemoveAt(i);
                        if (selectedNode == i) selectedNode = -1;
                        else if (selectedNode > i) selectedNode--;
                        MarkChanged();
                        break;
                    }
                }
                else if (isSelected)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 np = Handles.FreeMoveHandle(world, size * 1.2f, Vector3.zero, Handles.SphereHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(builder, "Move Ski Line Node");
                        Vector3 target = np;
                        var collider = builder.terrain.GetComponent<TerrainCollider>();
                        var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                        if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
                            target = hit.point;
                        Vector3 newLocal = builder.ClampLocal(target - tpos);
                        float offset = node.position.y - builder.SampleBaselineHeight(node.position.x, node.position.z);
                        newLocal.y = builder.SampleBaselineHeight(newLocal.x, newLocal.z) + offset;
                        node.position = newLocal;
                        MarkChanged();
                    }

                    EditorGUI.BeginChangeCheck();
                    Vector3 hp = Handles.PositionHandle(world, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(builder, "Move Ski Line Node");
                        node.position = builder.ClampLocal(hp - tpos);
                        MarkChanged();
                    }
                }
                else
                {
                    if (Handles.Button(world, Quaternion.identity, size, size * 2.5f, Handles.SphereHandleCap))
                    {
                        selectedNode = i;
                        Repaint();
                    }
                }
                Handles.Label(world + Vector3.up * size * 6f, i.ToString(), EditorStyles.whiteBoldLabel);
            }
        }

        void DrawFeatureHandles(SkiLine line)
        {
            if (line.features == null || line.features.Count == 0) return;
            var bake = builder.GetBake(line);
            if (bake == null) return;
            Vector3 tpos = builder.terrain.transform.position;

            for (int i = 0; i < line.features.Count; i++)
            {
                var feature = line.features[i];
                if (feature == null) continue;

                float d = Mathf.Clamp(feature.start, 0f, bake.totalLength);
                int idx = SkiLineSpline.IndexAtDistance(bake, d);
                Vector3 right = SkiLineSpline.RightAt(bake, idx);
                float halfW = bake.halfWidth[Mathf.Clamp(idx, 0, bake.Count - 1)];
                float latOff = Mathf.Clamp(feature.lateralOffset, -halfW, halfW);

                Vector3 local = SkiLineSpline.PointAtDistance(bake, d) + right * latOff;
                local.y += SkiLineSpline.FeatureOffsetAt(line, d, latOff);
                Vector3 world = tpos + local + Vector3.up * 0.25f;
                Vector3 tangent = SkiLineSpline.TangentAt(bake, idx);

                Handles.color = feature.enabled ? MoreColors.Amber : MoreColors.Slate;
                float size = HandleUtility.GetHandleSize(world) * 0.18f;

                EditorGUI.BeginChangeCheck();
                Vector3 np = Handles.Slider(world, tangent, size, Handles.ConeHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    float nd = SkiLineSpline.NearestDistance(bake, np - tpos, out _);
                    Undo.RecordObject(builder, "Move Ski Line Feature");
                    feature.start = Mathf.Clamp(nd, 0f, bake.totalLength);
                    MarkChanged();
                }

                Vector3 cubePos = world + Vector3.up * size * 1.6f;
                EditorGUI.BeginChangeCheck();
                Vector3 np2 = Handles.Slider(cubePos, right, size * 0.7f, Handles.CubeHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    float delta = Vector3.Dot(np2 - cubePos, right);
                    Undo.RecordObject(builder, "Move Ski Line Feature");
                    feature.lateralOffset = Mathf.Clamp(feature.lateralOffset + delta, -halfW, halfW);
                    MarkChanged();
                }

                Handles.Label(world + Vector3.up * size * 3.2f, feature.name);
            }
        }

        void DrawPreviews()
        {
            for (int li = 0; li < builder.lines.Count; li++)
            {
                var line = builder.lines[li];
                if (line == null) continue;
                var bake = builder.GetBake(line);
                if (bake == null) continue;

                bool isEditing = li == editLine;
                var preview = GetPreview(line, bake);
                if (preview == null) continue;

                if (!line.drawPreview && !isEditing) continue;

                Handles.color = !line.enabled ? MoreColors.Slate : isEditing ? MoreColors.JibbersOrange : MoreColors.Mint;
                Handles.DrawAAPolyLine(3f, preview.center);

                Handles.color = MoreColors.Violet;
                Handles.DrawAAPolyLine(2.5f, preview.basePts);

                if (!isEditing && line.nodes != null && Event.current.type == EventType.Repaint)
                {
                    Handles.color = MoreColors.Azure;
                    foreach (var node in line.nodes)
                    {
                        if (node == null) continue;
                        Vector3 nw = builder.terrain.transform.position + node.position;
                        Handles.SphereHandleCap(0, nw, Quaternion.identity,
                            HandleUtility.GetHandleSize(nw) * 0.09f, EventType.Repaint);
                    }
                }

                Handles.color = MoreColors.Indigo;
                Handles.DrawAAPolyLine(2f, preview.left);
                Handles.DrawAAPolyLine(2f, preview.right);

                Handles.color = MoreColors.Forest;
                foreach (var rib in preview.ribs)
                    Handles.DrawAAPolyLine(2f, rib);

                Handles.color = MoreColors.Orange;
                for (int fi = 0; fi < preview.featureSpans.Length; fi++)
                {
                    if (preview.featureSpans[fi].Length >= 2)
                        Handles.DrawAAPolyLine(5f, preview.featureSpans[fi]);
                    Handles.Label(preview.featureLabelPos[fi] + Vector3.up, preview.featureNames[fi]);
                }

                if (!string.IsNullOrEmpty(line.name))
                    Handles.Label(preview.center[preview.center.Length / 2] + Vector3.up * 2f, line.name);
            }
        }

        SkiLinePreview GetPreview(SkiLine line, SkiLineBake bake)
        {
            if (line.preview != null) return line.preview;

            Vector3 tpos = builder.terrain.transform.position;
            int n = bake.Count;
            var preview = new SkiLinePreview
            {
                center = new Vector3[n],
                basePts = new Vector3[n],
                left = new Vector3[n],
                right = new Vector3[n],
            };

            for (int i = 0; i < n; i++)
            {
                preview.center[i] = tpos + SkiLineSpline.SurfacePoint(line, bake, i, 0.5f);
                preview.basePts[i] = tpos + bake.pos[i];
                preview.left[i] = tpos + SkiLineSpline.SurfacePoint(line, bake, i, 0f);
                preview.right[i] = tpos + SkiLineSpline.SurfacePoint(line, bake, i, 1f);
            }

            int ribCount = Mathf.Clamp(n / 24, 6, 24);
            const int ribRes = 16;
            preview.ribs = new Vector3[ribCount][];
            for (int r = 0; r < ribCount; r++)
            {
                int idx = Mathf.Clamp(r * (n - 1) / (ribCount - 1), 0, n - 1);
                var rib = new Vector3[ribRes + 1];
                for (int j = 0; j <= ribRes; j++)
                    rib[j] = tpos + SkiLineSpline.SurfacePoint(line, bake, idx, (float)j / ribRes);
                preview.ribs[r] = rib;
            }

            var spans = new List<Vector3[]>();
            var names = new List<string>();
            var labels = new List<Vector3>();
            if (line.features != null)
            {
                foreach (var feature in line.features)
                {
                    if (feature == null || !feature.enabled || feature.length <= 0.01f) continue;
                    int i0 = SkiLineSpline.IndexAtDistance(bake, feature.start);
                    int i1 = SkiLineSpline.IndexAtDistance(bake, feature.start + feature.length);
                    if (i1 <= i0) continue;
                    var span = new Vector3[i1 - i0 + 1];
                    for (int i = i0; i <= i1; i++)
                        span[i - i0] = preview.center[i] + Vector3.up * 0.05f;
                    spans.Add(span);
                    names.Add(feature.name);
                    labels.Add(preview.center[i0]);
                }
            }
            preview.featureSpans = spans.ToArray();
            preview.featureNames = names.ToArray();
            preview.featureLabelPos = labels.ToArray();

            line.preview = preview;
            return preview;
        }
    }
#endif

    [ExecuteInEditMode]
    [RequireComponent(typeof(Terrain))]
    [AddComponentMenu("Jibbers/Ski Line Builder")]
    public class SkiLineBuilder : MonoBehaviour
    {
        public List<SkiLine> lines = new List<SkiLine>();
        public bool autoApply = true;

        [System.NonSerialized] public Texture2D baseline;
        [System.NonSerialized] public Texture2D maskBaseline;
        [SerializeField, HideInInspector] string sessionId;
        [HideInInspector] public bool linesApplied;
        [SerializeField, HideInInspector] bool maskPainted;
        [SerializeField, HideInInspector] int paintPx0 = -1, paintPy0 = -1, paintPx1 = -1, paintPy1 = -1;
        [SerializeField, HideInInspector] RectInt lastHeightRegion;
        [SerializeField, HideInInspector] bool hasLastHeightRegion;
        [System.NonSerialized] int appliedHash;
        [System.NonSerialized] bool hasAppliedHash;
        [System.NonSerialized] int trackPx0, trackPy0, trackPx1, trackPy1;

        static readonly float[] MarkingColorValues = { 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f, 0.45f, 0.50f, 0.55f, 0.60f, 0.65f, 0.70f, 0.75f, 0.80f };

        [HideInInspector] public Terrain terrain;
        TerrainData data;
        [HideInInspector] public int res;

        ComputeShader carveShader;
        int carveKernel;
        RenderTexture heightRT;
        readonly HashSet<ComputeBuffer> liveBuffers = new HashSet<ComputeBuffer>();

        void OnEnable()
        {
            if (Application.isPlaying)
            {
                Destroy(this);
                return;
            }

#if UNITY_EDITOR
            if (carveShader == null)
            {
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("SkiLineEdit t:ComputeShader"))
                {
                    var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null) { carveShader = candidate; break; }
                }
            }
#endif
            if (carveShader == null)
            {
                Debug.LogError("[SkiLineBuilder] SkiLineEdit compute shader not found.");
                enabled = false;
                return;
            }

            carveKernel = carveShader.FindKernel("CarveLine");

            terrain = GetComponent<Terrain>();
            data = terrain != null ? terrain.terrainData : null;
            if (data == null)
            {
                Debug.LogError("[SkiLineBuilder] Terrain has no TerrainData.");
                enabled = false;
                return;
            }

            res = data.heightmapResolution;
            CreateRT();

#if UNITY_EDITOR
            if (baseline == null)
                TryLoadSessionFile();
#endif
            appliedHash = ContentHash();
            hasAppliedHash = true;
        }

        void OnDisable()
        {
            if (heightRT != null) { heightRT.Release(); heightRT = null; }
            foreach (var buffer in liveBuffers) buffer?.Release();
            liveBuffers.Clear();
            DestroySessionTextures();
        }

        void DestroySessionTextures()
        {
            if (baseline != null) { DestroyImmediate(baseline); baseline = null; }
            if (maskBaseline != null) { DestroyImmediate(maskBaseline); maskBaseline = null; }
        }

        static Texture2D NewSessionTex(int w, int h, TextureFormat fmt)
        {
            return new Texture2D(w, h, fmt, false, true) { hideFlags = HideFlags.HideAndDontSave };
        }

        public bool SessionActive
        {
            get
            {
                if (baseline != null) return true;
#if UNITY_EDITOR
                string path = SessionPath;
                return path != null && System.IO.File.Exists(path);
#else
                return false;
#endif
            }
        }

        public int ContentHash()
        {
            unchecked
            {
                int h = 19;
                if (lines != null && data != null)
                    foreach (var line in lines)
                        if (line != null)
                        {
                            h = h * 31 + (line.enabled ? 1 : 0);
                            h = h * 31 + SkiLineSpline.ComputeHash(line, transform.position, data.size, res);
                        }
                return h;
            }
        }

        public void NotifyHeightRangeChanged(float scale, float offsetN, float raiseNodes)
        {
#if UNITY_EDITOR
            if (baseline == null) TryLoadSessionFile();
#endif
            if (baseline != null)
            {
                var flat = baseline.GetPixelData<float>(0);
                for (int i = 0; i < flat.Length; i++) flat[i] = Mathf.Clamp01(flat[i] * scale + offsetN);
                baseline.Apply(false, false);
#if UNITY_EDITOR
                SaveSessionFile();
#endif
            }
            if (Mathf.Abs(raiseNodes) > 0.0001f && lines != null)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(this, "Change Terrain Height Range");
#endif
                foreach (var line in lines)
                {
                    if (line == null || line.nodes == null) continue;
                    foreach (var node in line.nodes)
                        if (node != null) node.position.y += raiseNodes;
                }
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
#if UNITY_EDITOR
            if (autoApply) ScheduleApply();
#endif
        }

#if UNITY_EDITOR
        static string SessionDir => System.IO.Path.Combine("Library", "JibbersMapToolsSessions");
        string SessionPath => string.IsNullOrEmpty(sessionId) ? null : System.IO.Path.Combine(SessionDir, sessionId + ".bin");
        const int SessionMagic = 0x4A534B31;

        void SaveSessionFile()
        {
            if (baseline == null || string.IsNullOrEmpty(sessionId)) return;
            System.IO.Directory.CreateDirectory(SessionDir);
            using (var fs = new System.IO.FileStream(SessionPath, System.IO.FileMode.Create))
            using (var w = new System.IO.BinaryWriter(fs))
            {
                w.Write(SessionMagic);
                w.Write(baseline.width);
                var flat = baseline.GetPixelData<float>(0);
                var bytes = new byte[flat.Length * 4];
                System.Buffer.BlockCopy(flat.ToArray(), 0, bytes, 0, bytes.Length);
                w.Write(bytes);
                if (maskBaseline != null)
                {
                    w.Write(maskBaseline.width);
                    w.Write(maskBaseline.height);
                    w.Write(maskBaseline.GetRawTextureData());
                }
                else { w.Write(0); w.Write(0); }
            }
        }

        bool TryLoadSessionFile()
        {
            string path = SessionPath;
            if (path == null || !System.IO.File.Exists(path)) return false;
            try
            {
                using (var fs = System.IO.File.OpenRead(path))
                using (var r = new System.IO.BinaryReader(fs))
                {
                    if (r.ReadInt32() != SessionMagic) return false;
                    int bres = r.ReadInt32();
                    var floats = new float[bres * bres];
                    var bytes = r.ReadBytes(floats.Length * 4);
                    if (bytes.Length != floats.Length * 4) return false;
                    System.Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
                    DestroySessionTextures();
                    baseline = NewSessionTex(bres, bres, TextureFormat.RFloat);
                    baseline.SetPixelData(floats, 0);
                    baseline.Apply(false, false);
                    int mw = r.ReadInt32(), mh = r.ReadInt32();
                    if (mw > 0 && mh > 0)
                    {
                        maskBaseline = NewSessionTex(mw, mh, TextureFormat.RGBA32);
                        maskBaseline.LoadRawTextureData(r.ReadBytes(mw * mh * 4));
                        maskBaseline.Apply(false, false);
                    }
                }
                return baseline != null;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SkiLineBuilder] Failed to load the session snapshot: " + e.Message);
                DestroySessionTextures();
                return false;
            }
        }

        void DeleteSessionFile()
        {
            string path = SessionPath;
            if (path != null && System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

#endif

        void CreateRT()
        {
            if (heightRT != null) heightRT.Release();
            heightRT = new RenderTexture(res, res, 0, RenderTextureFormat.RFloat);
            heightRT.enableRandomWrite = true;
            heightRT.Create();
        }

        public Vector3 ClampLocal(Vector3 local)
        {
            local.x = Mathf.Clamp(local.x, 0f, data.size.x);
            local.z = Mathf.Clamp(local.z, 0f, data.size.z);
            return local;
        }

        public SkiLineBake GetBake(SkiLine line)
        {
            if (line == null || data == null) return null;
            int hash = SkiLineSpline.ComputeHash(line, transform.position, data.size, res);
            if (line.bakeHash == hash) return line.bake;
            line.bake = SkiLineSpline.Bake(line, line.bakeResolution);
            line.bakeHash = hash;
            line.preview = null;
            return line.bake;
        }

        public float SampleBaselineHeight(float localX, float localZ)
        {
            if (data == null) return 0f;
            if (baseline == null || baseline.width != res)
                return data.GetInterpolatedHeight(localX / data.size.x, localZ / data.size.z);

            var flat = baseline.GetPixelData<float>(0);
            float fx = Mathf.Clamp01(localX / data.size.x) * (res - 1);
            float fy = Mathf.Clamp01(localZ / data.size.z) * (res - 1);
            int x0 = (int)fx, y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, res - 1);
            int y1 = Mathf.Min(y0 + 1, res - 1);
            float tx = fx - x0, ty = fy - y0;
            float h = Mathf.Lerp(
                Mathf.Lerp(flat[y0 * res + x0], flat[y0 * res + x1], tx),
                Mathf.Lerp(flat[y1 * res + x0], flat[y1 * res + x1], tx), ty);
            return h * data.size.y;
        }

        static RectInt UnionRect(RectInt a, RectInt b)
        {
            if (a.width <= 0 || a.height <= 0) return b;
            if (b.width <= 0 || b.height <= 0) return a;
            int x0 = Mathf.Min(a.xMin, b.xMin);
            int y0 = Mathf.Min(a.yMin, b.yMin);
            int x1 = Mathf.Max(a.xMax, b.xMax);
            int y1 = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        public void Apply(bool registerUndo = true)
        {
            if (terrain == null || data == null || carveShader == null || !isActiveAndEnabled) return;
            if (data.heightmapResolution != res) res = data.heightmapResolution;
            if (heightRT == null || !heightRT.IsCreated() || heightRT.width != res) CreateRT();

            bool anyRun = false;
            if (lines != null)
                foreach (var line in lines)
                    if (line != null && line.enabled && GetBake(line) != null) { anyRun = true; break; }

            if (!anyRun && !linesApplied)
            {
                appliedHash = ContentHash();
                hasAppliedHash = true;
                return;
            }

            EnsureBaseline();
            if (baseline == null || baseline.width != res || baseline.height != res) return;

#if UNITY_EDITOR
            if (registerUndo)
                UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Apply Ski Lines");
#endif
            Graphics.Blit(baseline, heightRT);

            var batch = new List<ComputeBuffer>();
            var currentRect = new RectInt(0, 0, 0, 0);
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (line == null || !line.enabled) continue;
                    var bake = GetBake(line);
                    if (bake == null) continue;
                    currentRect = UnionRect(currentRect, Carve(line, bake, batch));
                }
            }

            var readRect = hasLastHeightRegion
                ? UnionRect(currentRect, lastHeightRegion)
                : new RectInt(0, 0, res, res);
            if (readRect.width <= 0 || readRect.height <= 0) readRect = new RectInt(0, 0, res, res);
            lastHeightRegion = currentRect;
            hasLastHeightRegion = true;

            ReadBack(batch, readRect);
            PaintFeatures(registerUndo);
            linesApplied = anyRun;
            appliedHash = ContentHash();
            hasAppliedHash = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        Texture2D AcquireMask(out string problem)
        {
            problem = null;
            if (terrain == null || terrain.materialTemplate == null)
            {
                problem = "Terrain has no material.";
                return null;
            }
            var mat = terrain.materialTemplate;
            if (!mat.HasProperty("_SnowMask"))
            {
                problem = "Terrain material has no _SnowMask property.";
                return null;
            }
            var tex = mat.GetTexture("_SnowMask") as Texture2D;
            if (tex == null)
            {
                problem = "No _SnowMask texture assigned to the terrain material.";
                return null;
            }
            if (!tex.isReadable)
            {
                problem = $"Snow mask '{tex.name}' is not readable — enable Read/Write in its import settings.";
                return null;
            }
            if (tex.format != TextureFormat.RGBA32 && tex.format != TextureFormat.ARGB32
                && tex.format != TextureFormat.BGRA32 && tex.format != TextureFormat.RGB24)
            {
                problem = $"Snow mask '{tex.name}' uses format {tex.format} — needs an uncompressed format like RGBA32.";
                return null;
            }
            return tex;
        }

        Texture2D AcquireMask(bool logProblems)
        {
            var tex = AcquireMask(out string problem);
            if (tex == null && logProblems && problem != null)
                Debug.LogWarning("[SkiLineBuilder] " + problem);
            return tex;
        }

        public string MaskProblem()
        {
            if (!AnyPaintStripes()) return null;
            AcquireMask(out string problem);
            return problem;
        }

        bool AnyPaintStripes()
        {
            if (lines == null) return false;
            foreach (var line in lines)
            {
                if (line == null || !line.enabled || line.features == null) continue;
                foreach (var f in line.features)
                    if (f != null && f.enabled && f.length > 0.01f && f.paintStripes != null && f.paintStripes.Count > 0)
                        return true;
            }
            return false;
        }

        void PaintFeatures(bool explicitApply)
        {
            bool anyStripes = AnyPaintStripes();
            if (!anyStripes && !maskPainted) return;

            var mask = AcquireMask(explicitApply && anyStripes);
            if (mask == null) return;

            if (maskBaseline == null || maskBaseline.width != mask.width || maskBaseline.height != mask.height)
                CaptureMaskBaseline(mask);
            if (maskBaseline == null || maskBaseline.width != mask.width || maskBaseline.height != mask.height) return;

#if UNITY_EDITOR
            if (explicitApply)
                UnityEditor.Undo.RegisterCompleteObjectUndo(mask, "Apply Ski Lines");
#endif

            var pixels = mask.GetPixels32();
            var basePixels = maskBaseline.GetPixels32();
            int maskW = mask.width;
            int maskH = mask.height;

            if (maskPainted && paintPx0 >= 0)
            {
                int bx0 = Mathf.Clamp(paintPx0, 0, maskW - 1), by0 = Mathf.Clamp(paintPy0, 0, maskH - 1);
                int bx1 = Mathf.Clamp(paintPx1, 0, maskW - 1), by1 = Mathf.Clamp(paintPy1, 0, maskH - 1);
                for (int py = by0; py <= by1; py++)
                    for (int px = bx0; px <= bx1; px++)
                    {
                        int i = py * maskW + px;
                        pixels[i].g = basePixels[i].g;
                        pixels[i].b = basePixels[i].b;
                    }
            }

            trackPx0 = maskW; trackPy0 = maskH; trackPx1 = -1; trackPy1 = -1;
            if (anyStripes)
            {
                foreach (var line in lines)
                {
                    if (line == null || !line.enabled || line.features == null) continue;
                    var bake = GetBake(line);
                    if (bake == null) continue;
                    foreach (var feature in line.features)
                    {
                        if (feature == null || !feature.enabled || feature.length <= 0.01f || feature.paintStripes == null) continue;
                        foreach (var stripe in feature.paintStripes)
                            if (stripe != null)
                                RasterizeStripe(pixels, maskW, maskH, bake, feature, stripe);
                    }
                }
            }

            mask.SetPixels32(pixels);
            mask.Apply(mask.mipmapCount > 1, false);
            bool haveNew = trackPx1 >= trackPx0 && trackPy1 >= trackPy0;
            maskPainted = haveNew;
            if (haveNew) { paintPx0 = trackPx0; paintPy0 = trackPy0; paintPx1 = trackPx1; paintPy1 = trackPy1; }
            else { paintPx0 = paintPy0 = paintPx1 = paintPy1 = -1; }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mask);
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        void RasterizeStripe(Color32[] pixels, int maskW, int maskH, SkiLineBake bake, SkiLineFeature feature, SkiLinePaintStripe stripe)
        {
            float d0 = Mathf.Clamp(feature.start, 0f, bake.totalLength);
            float d1 = Mathf.Clamp(feature.start + feature.length, 0f, bake.totalLength);
            if (d1 - d0 < 0.01f) return;

            byte colorByte = (byte)Mathf.RoundToInt(MarkingColorValues[Mathf.Clamp(stripe.colorIdx, 0, MarkingColorValues.Length - 1)] * 255f);
            float radiusM = Mathf.Max(stripe.stripeWidth * 0.5f, 0.05f);
            float softness = Mathf.Clamp01(stripe.softness);
            float opacity = stripe.opacity <= 0f ? 1f : Mathf.Clamp01(stripe.opacity);
            bool windowed = feature.width > 0.01f;

            if (stripe.acrossLine)
            {
                float d = Mathf.Lerp(d0, d1, Mathf.Clamp01(stripe.position));
                int idx = SkiLineSpline.IndexAtDistance(bake, d);
                Vector3 center = SkiLineSpline.PointAtDistance(bake, d);
                Vector3 right = SkiLineSpline.RightAt(bake, idx);
                float halfW = windowed ? feature.width * 0.5f : bake.halfWidth[Mathf.Clamp(idx, 0, bake.Count - 1)];
                float mid = windowed ? feature.lateralOffset : 0f;
                float inset = Mathf.Clamp(stripe.inset, 0f, 0.45f) * halfW * 2f;
                Vector3 a = center + right * (mid - halfW + inset);
                Vector3 b = center + right * (mid + halfW - inset);
                RasterizeSegment(pixels, maskW, maskH, a, b, radiusM, softness, opacity, colorByte);
            }
            else
            {
                int i0 = SkiLineSpline.IndexAtDistance(bake, d0);
                int i1 = SkiLineSpline.IndexAtDistance(bake, d1);
                if (i1 <= i0) return;
                Vector3 prev = default;
                bool hasPrev = false;
                for (int i = i0; i <= i1; i++)
                {
                    Vector3 right = SkiLineSpline.RightAt(bake, i);
                    float halfW = windowed ? feature.width * 0.5f : bake.halfWidth[i];
                    float mid = windowed ? feature.lateralOffset : 0f;
                    float lateral = mid + (Mathf.Clamp01(stripe.position) - 0.5f) * 2f * halfW;
                    Vector3 pt = bake.pos[i] + right * lateral;
                    if (hasPrev) RasterizeSegment(pixels, maskW, maskH, prev, pt, radiusM, softness, opacity, colorByte);
                    prev = pt;
                    hasPrev = true;
                }
            }
        }

        void RasterizeSegment(Color32[] pixels, int maskW, int maskH, Vector3 a, Vector3 b, float radiusM, float softness, float opacity, byte colorByte)
        {
            float pxSizeM = data.size.x / maskW;
            float reachM = Mathf.Max(radiusM * (1f + softness * 2f), pxSizeM * (0.75f + softness * 2.5f));

            float ax = a.x, az = a.z, bx = b.x, bz = b.z;
            float minX = Mathf.Min(ax, bx) - reachM, maxX = Mathf.Max(ax, bx) + reachM;
            float minZ = Mathf.Min(az, bz) - reachM, maxZ = Mathf.Max(az, bz) + reachM;

            int px0 = Mathf.Max(0, Mathf.FloorToInt(minX / data.size.x * maskW));
            int px1 = Mathf.Min(maskW - 1, Mathf.CeilToInt(maxX / data.size.x * maskW));
            int py0 = Mathf.Max(0, Mathf.FloorToInt((1f - maxZ / data.size.z) * maskH));
            int py1 = Mathf.Min(maskH - 1, Mathf.CeilToInt((1f - minZ / data.size.z) * maskH));
            if (px1 < px0 || py1 < py0) return;

            trackPx0 = Mathf.Min(trackPx0, px0); trackPx1 = Mathf.Max(trackPx1, px1);
            trackPy0 = Mathf.Min(trackPy0, py0); trackPy1 = Mathf.Max(trackPy1, py1);

            float abx = bx - ax, abz = bz - az;
            float abLen2 = Mathf.Max(abx * abx + abz * abz, 1e-8f);

            for (int py = py0; py <= py1; py++)
            {
                float mz = (1f - (py + 0.5f) / maskH) * data.size.z;
                for (int px = px0; px <= px1; px++)
                {
                    float mx = (px + 0.5f) / maskW * data.size.x;
                    float t = Mathf.Clamp01(((mx - ax) * abx + (mz - az) * abz) / abLen2);
                    float dx = mx - (ax + abx * t);
                    float dz = mz - (az + abz * t);
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    float falloff = Mathf.Clamp01((1f - dist / reachM) / (softness + 0.001f));
                    byte covByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(falloff * opacity) * 255f);
                    if (covByte == 0) continue;
                    int i = py * maskW + px;
                    var c = pixels[i];
                    c.g = colorByte;
                    c.b = (byte)Mathf.Max(c.b, covByte);
                    pixels[i] = c;
                }
            }
        }

        void CaptureMaskBaseline(Texture2D mask)
        {
            if (mask == null) return;
            if (maskBaseline == null || maskBaseline.width != mask.width || maskBaseline.height != mask.height)
            {
                if (maskBaseline != null) DestroyImmediate(maskBaseline);
                maskBaseline = NewSessionTex(mask.width, mask.height, TextureFormat.RGBA32);
            }
            maskBaseline.SetPixels32(mask.GetPixels32());
            maskBaseline.Apply(false, false);
#if UNITY_EDITOR
            SaveSessionFile();
#endif
        }

        RectInt Carve(SkiLine line, SkiLineBake bake, List<ComputeBuffer> batch)
        {
            int n = bake.Count;
            float pxPerMeter = (res - 1) / data.size.x;

            var arr = new float[n * 8];
            float minPx = float.MaxValue, minPy = float.MaxValue;
            float maxPx = float.MinValue, maxPy = float.MinValue;
            float maxHalfW = 0f;

            for (int i = 0; i < n; i++)
            {
                float px = bake.pos[i].x / data.size.x * (res - 1);
                float py = bake.pos[i].z / data.size.z * (res - 1);
                float halfWpx = bake.halfWidth[i] * pxPerMeter;

                arr[i * 8 + 0] = px;
                arr[i * 8 + 1] = py;
                arr[i * 8 + 2] = bake.pos[i].y / data.size.y;
                arr[i * 8 + 3] = halfWpx;
                arr[i * 8 + 4] = bake.halfWidth[i] * 2f * Mathf.Tan(bake.roll[i] * Mathf.Deg2Rad) / data.size.y;
                arr[i * 8 + 5] = bake.cumDist[i] * pxPerMeter;

                if (px < minPx) minPx = px;
                if (px > maxPx) maxPx = px;
                if (py < minPy) minPy = py;
                if (py > maxPy) maxPy = py;
                if (halfWpx > maxHalfW) maxHalfW = halfWpx;
            }

            int margin = Mathf.CeilToInt(maxHalfW) + 2;
            int rx0 = Mathf.Clamp(Mathf.FloorToInt(minPx) - margin, 0, res - 1);
            int ry0 = Mathf.Clamp(Mathf.FloorToInt(minPy) - margin, 0, res - 1);
            int rx1 = Mathf.Clamp(Mathf.CeilToInt(maxPx) + margin, 0, res - 1);
            int ry1 = Mathf.Clamp(Mathf.CeilToInt(maxPy) + margin, 0, res - 1);
            int sizeX = rx1 - rx0 + 1;
            int sizeY = ry1 - ry0 + 1;
            if (sizeX < 1 || sizeY < 1) return new RectInt(0, 0, 0, 0);

            var sampleBuffer = new ComputeBuffer(n, 32);
            sampleBuffer.SetData(arr);
            batch.Add(sampleBuffer);
            liveBuffers.Add(sampleBuffer);

            const int crossRes = 256;
            var cross = new float[crossRes];
            if (line.crossSection != null && line.crossSection.length > 0)
                for (int i = 0; i < crossRes; i++)
                    cross[i] = line.crossSection.Evaluate((float)i / (crossRes - 1));
            var crossBuffer = new ComputeBuffer(crossRes, sizeof(float));
            crossBuffer.SetData(cross);
            batch.Add(crossBuffer);
            liveBuffers.Add(crossBuffer);

            const int latRes = 32;
            var strip = new float[n * latRes];
            for (int i = 0; i < n; i++)
            {
                float d = bake.cumDist[i];
                float halfWm = bake.halfWidth[i];
                for (int b = 0; b < latRes; b++)
                {
                    float crossT = (float)b / (latRes - 1);
                    float lat = (crossT - 0.5f) * 2f * halfWm;
                    strip[i * latRes + b] = SkiLineSpline.FeatureOffsetAt(line, d, lat) / data.size.y;
                }
            }
            var stripBuffer = new ComputeBuffer(n * latRes, sizeof(float));
            stripBuffer.SetData(strip);
            batch.Add(stripBuffer);
            liveBuffers.Add(stripBuffer);

            carveShader.SetTexture(carveKernel, "Heightmap", heightRT);
            carveShader.SetBuffer(carveKernel, "Samples", sampleBuffer);
            carveShader.SetBuffer(carveKernel, "CrossSection", crossBuffer);
            carveShader.SetBuffer(carveKernel, "FeatureStrip", stripBuffer);
            carveShader.SetInt("sampleCount", n);
            carveShader.SetInt("res", res);
            carveShader.SetInt("crossSectionRes", crossRes);
            carveShader.SetInt("featureLatRes", latRes);
            carveShader.SetInts("regionMin", rx0, ry0);
            carveShader.SetInts("regionSize", sizeX, sizeY);
            carveShader.SetFloat("totalLength", bake.totalLength * pxPerMeter);
            carveShader.SetFloat("edgeBlend", line.edgeBlend);
            carveShader.SetFloat("edgeFalloff", line.edgeFalloff);
            carveShader.SetFloat("endBlend", line.endBlend * pxPerMeter);
            carveShader.SetFloat("crossSectionDepth", line.crossSectionDepth / data.size.y);
            carveShader.SetFloat("sideFlatten", line.sideFlatten);

            carveShader.Dispatch(carveKernel,
                Mathf.Max(1, Mathf.CeilToInt(sizeX / 8f)),
                Mathf.Max(1, Mathf.CeilToInt(sizeY / 8f)), 1);
            return new RectInt(rx0, ry0, sizeX, sizeY);
        }

        void ReadBack(List<ComputeBuffer> batch, RectInt rect)
        {
            int requestRes = res;
            int rx = Mathf.Clamp(rect.x, 0, requestRes - 1);
            int ry = Mathf.Clamp(rect.y, 0, requestRes - 1);
            int rw = Mathf.Clamp(rect.width, 1, requestRes - rx);
            int rh = Mathf.Clamp(rect.height, 1, requestRes - ry);
            AsyncGPUReadback.Request(heightRT, 0, rx, rw, ry, rh, 0, 1, request =>
            {
                foreach (var buffer in batch)
                    if (buffer != null && liveBuffers.Remove(buffer))
                        buffer.Release();

                if (request.hasError) return;
                if (this == null || data == null) return;
                if (requestRes != res || data.heightmapResolution != requestRes) return;

                var raw = request.GetData<float>();
                if (raw.Length < rw * rh) return;
                var heights = new float[rh, rw];
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < rw; x++)
                        heights[y, x] = raw[y * rw + x];

                data.SetHeightsDelayLOD(rx, ry, heights);
                data.SyncHeightmap();
            });
        }

        void EnsureBaseline()
        {
            if (baseline != null && baseline.width == res && baseline.height == res) return;
#if UNITY_EDITOR
            if (baseline == null && TryLoadSessionFile() && baseline != null && baseline.width == res && baseline.height == res)
                return;
            if (baseline != null && (baseline.width != res || baseline.height != res))
            {
                Debug.LogError("[SkiLineBuilder] The session snapshot does not match the heightmap resolution — bake or discard the session before changing the resolution.");
                return;
            }
            if (linesApplied)
            {
                Debug.LogError("[SkiLineBuilder] Ski lines are applied but the session snapshot is missing — refusing to re-snapshot (it would bake the lines into the ground silently). If the current terrain is fine as-is, press Bake Into Terrain to accept it.");
                return;
            }
            CaptureSession();
#endif
        }

#if UNITY_EDITOR
        void CaptureSession()
        {
            if (data == null) return;
            res = data.heightmapResolution;

            var heights = data.GetHeights(0, 0, res, res);
            var flat = new float[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    flat[y * res + x] = heights[y, x];

            DestroySessionTextures();
            baseline = NewSessionTex(res, res, TextureFormat.RFloat);
            baseline.SetPixelData(flat, 0);
            baseline.Apply(false, false);

            var mask = AcquireMask(false);
            if (mask != null) CaptureMaskBaseline(mask);

            sessionId = System.Guid.NewGuid().ToString("N");
            SaveSessionFile();

            maskPainted = false;
            paintPx0 = paintPy0 = paintPx1 = paintPy1 = -1;
            linesApplied = false;
            hasLastHeightRegion = false;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void BakeSession()
        {
            DeleteSessionFile();
            DestroySessionTextures();
            sessionId = null;
            linesApplied = false;
            maskPainted = false;
            paintPx0 = paintPy0 = paintPx1 = paintPy1 = -1;
            hasLastHeightRegion = false;
            appliedHash = ContentHash();
            hasAppliedHash = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void DiscardSession()
        {
            RestoreBaseline();
            DeleteSessionFile();
            DestroySessionTextures();
            sessionId = null;
            if (lines != null)
                foreach (var line in lines)
                    if (line != null) line.enabled = false;
            maskPainted = false;
            paintPx0 = paintPy0 = paintPx1 = paintPy1 = -1;
            linesApplied = false;
            hasLastHeightRegion = false;
            appliedHash = ContentHash();
            hasAppliedHash = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public void RestoreBaseline()
        {
#if UNITY_EDITOR
            if (baseline == null) TryLoadSessionFile();
#endif
            if (baseline == null || data == null) return;
            res = data.heightmapResolution;
            if (baseline.width != res || baseline.height != res)
            {
                Debug.LogError("[SkiLineBuilder] The session snapshot does not match the heightmap resolution — cannot restore.");
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Restore Ski Line Session");
#endif
            var flat = baseline.GetPixelData<float>(0);
            var heights = new float[res, res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    heights[y, x] = flat[y * res + x];
            data.SetHeights(0, 0, heights);

            var mask = AcquireMask(false);
            if (mask != null && maskBaseline != null
                && maskBaseline.width == mask.width && maskBaseline.height == mask.height)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCompleteObjectUndo(mask, "Restore Ski Line Session");
#endif
                var basePixels = maskBaseline.GetPixels32();
                var pixels = mask.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i].g = basePixels[i].g;
                    pixels[i].b = basePixels[i].b;
                }
                mask.SetPixels32(pixels);
                mask.Apply(mask.mipmapCount > 1, false);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(mask);
#endif
            }

            maskPainted = false;
            paintPx0 = paintPy0 = paintPx1 = paintPy1 = -1;
            linesApplied = false;
            hasLastHeightRegion = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        [System.NonSerialized] bool applyQueued;

        public void ScheduleApply()
        {
            if (applyQueued) return;
            if (hasAppliedHash && ContentHash() == appliedHash) return;
            applyQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                applyQueued = false;
                if (this == null || !isActiveAndEnabled) return;
                if (hasAppliedHash && ContentHash() == appliedHash) return;
                Apply(false);
            };
        }
#endif
    }

}
