using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditorInternal;

    [CustomEditor(typeof(FormationBuilder))]
    public class FormationBuilderEditor : Editor
    {
        FormationBuilder builder;
        ReorderableList formationList;

        int editFormation = -1;
        int selectedPoint = -1;
        bool addingPoints;

        void OnEnable()
        {
            builder = (FormationBuilder)target;
            formationList = new ReorderableList(serializedObject, serializedObject.FindProperty("formations"),
                draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);

            formationList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Formations");

            formationList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = formationList.serializedProperty.GetArrayElementAtIndex(index);
                string nm = element.FindPropertyRelative("name").stringValue;
                string suffix = index == editFormation ? "  (editing)" : "";
                EditorGUI.PropertyField(rect, element, new GUIContent($"[{index}]  {nm}{suffix}"), true);
            };

            formationList.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(formationList.serializedProperty.GetArrayElementAtIndex(index), true);

            formationList.onAddCallback = list =>
            {
                int newIdx = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                list.index = newIdx;
                var el = list.serializedProperty.GetArrayElementAtIndex(newIdx);
                el.FindPropertyRelative("name").stringValue = "Formation " + newIdx;
                el.FindPropertyRelative("enabled").boolValue = true;
                el.FindPropertyRelative("drawPreview").boolValue = true;
                el.FindPropertyRelative("area").arraySize = 0;
                el.FindPropertyRelative("presetAsset").objectReferenceValue = null;
                el.FindPropertyRelative("fitted").boolValue = false;
                el.FindPropertyRelative("uiShape").boolValue = true;
                el.FindPropertyRelative("uiShapeAdv").boolValue = false;
                el.FindPropertyRelative("uiThermal").boolValue = false;
                el.FindPropertyRelative("uiHydraulic").boolValue = false;
                el.FindPropertyRelative("uiSnow").boolValue = false;
                el.FindPropertyRelative("uiSnowAdv").boolValue = false;
                var tmp = new Formation();
                Formation.Presets[0].apply(tmp);
                FormationDrawer.WriteShape(el, tmp, setName: false);
                el.isExpanded = true;
            };

            formationList.onReorderCallback = list => { editFormation = -1; selectedPoint = -1; };

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            if (builder != null) builder.NotifyUndoRedo();
            if (builder != null && builder.autoApply && builder.terrain != null)
                builder.ScheduleApply(builder.liveErosion);
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            builder = (FormationBuilder)target;

            EditorGUILayout.HelpBox(
                "Formations work against a BASELINE: a snapshot of the terrain taken when the first formation is " +
                "applied. While formations are applied, every change re-renders the terrain from that snapshot — " +
                "edits made with other tools (painting, sculpting, inserts) conflict with it and can be LOST. " +
                "Bake formations into the terrain as soon as they are final, and resolve any conflict warning " +
                "below before editing further.",
                MessageType.Warning);

            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            formationList.DoLayoutList();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoApply"),
                new GUIContent("Auto Apply", "Re-apply formations automatically whenever a formation or its area changes."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("liveErosion"),
                new GUIContent("Live Erosion", "Run the full erosion/snow simulation on every edit. Turn off for snappier editing on large areas — the Apply button always runs the full simulation."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("visualizeBaseline"),
                new GUIContent("Visualize Baseline", "Scene-view debug overlay: violet grid = the baseline ground hidden under each formation, mint = areas the tool rewrites, slate = last written area, amber = rock-painted mask area, crimson = unresolved external edits."));
            serializedObject.ApplyModifiedProperties();
            bool changed = EditorGUI.EndChangeCheck();

            if (builder.formations == null || builder.formations.Count == 0)
                EditorGUILayout.HelpBox(
                    "Add a formation, pick a preset (Basic / Mountain / Hill / Rocks / Crater), press Edit Selected " +
                    "Formation, then click the terrain to outline the area. The shape is generated inside that outline, " +
                    "then eroded and snow-covered; rock shows through the snow mask where snow can't hold.",
                    MessageType.Info);

            int sel = formationList.index;
            bool hasSel = builder.formations != null && sel >= 0 && sel < builder.formations.Count;

            EditorGUILayout.Space(6);
            if (hasSel)
            {
                bool editing = editFormation == sel;
                GUI.color = editing ? Color.yellow : Color.white;
                if (GUILayout.Button(editing ? "Stop Editing  (Esc)" : "Edit Selected Formation"))
                {
                    editFormation = editing ? -1 : sel;
                    addingPoints = !editing && (builder.formations[sel].area == null || builder.formations[sel].area.Count < 3);
                    selectedPoint = -1;
                    if (!editing && SceneView.lastActiveSceneView != null)
                        SceneView.lastActiveSceneView.Focus();
                    SceneView.RepaintAll();
                }
                GUI.color = Color.white;

                if (editFormation == sel)
                {
                    addingPoints = GUILayout.Toggle(addingPoints, "Add Points (click terrain to append)", "Button");
                    EditorGUILayout.HelpBox(
                        "Click terrain: append boundary point (Add Points mode) · Ctrl+Click terrain: append\n" +
                        "Click sphere: select · Drag selected sphere: slide along terrain · Arrow handles: exact XZ\n" +
                        "Shift+Click near edge: insert point · X or Ctrl+Click sphere: delete · Esc: stop\n" +
                        "While Add Points is on, the shape auto-scales to the outlined area.",
                        MessageType.Info);

                    if (selectedPoint >= 0 && GUILayout.Button("Delete Selected Point"))
                        DeleteSelectedPoint();
                    if (GUILayout.Button("Clear Area"))
                    {
                        Undo.RecordObject(builder, "Clear Formation Area");
                        builder.formations[sel].area.Clear();
                        selectedPoint = -1;
                        MarkChanged();
                    }
                }
            }

            EditorGUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(builder.terrain == null || !builder.isActiveAndEnabled);
            if (GUILayout.Button(new GUIContent("Apply Formations (full simulation)",
                    "Rebuild all enabled formations with full erosion and snow, registering an undo step."), GUILayout.Height(28)))
                builder.Apply(true, true);
            EditorGUI.EndDisabledGroup();

            string maskProblem = builder.MaskProblem();
            if (maskProblem != null)
                EditorGUILayout.HelpBox("Rock cannot render (heightmap still generates): " + maskProblem,
                    MessageType.Warning);

            if (builder.HasBlockingExternalEdits())
            {
                EditorGUILayout.HelpBox(
                    "The terrain was edited by other tools inside formation areas. Auto-apply is paused so those edits are not overwritten.\n" +
                    "Adopt folds them into the baseline (they become part of the ground under the formations); Overwrite discards them (undoable).",
                    MessageType.Warning);
                bool canAdopt = builder.CanAdoptExternalEdits();
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(!canAdopt);
                if (GUILayout.Button("Adopt & Apply"))
                {
                    builder.AdoptExternalEdits();
                    builder.Apply(false, builder.liveErosion);
                }
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("Overwrite (undoable)"))
                {
                    builder.OverwriteExternalEdits();
                    builder.Apply(false, builder.liveErosion);
                }
                EditorGUILayout.EndHorizontal();
                if (!canAdopt)
                    EditorGUILayout.LabelField(
                        "Adopt unavailable — the write cache was lost to a script reload. Overwrite, or Restore Baseline → redo the edit → Capture Baseline.",
                        EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Baseline", EditorStyles.boldLabel);
            string status = builder.baseline == null
                ? "No baseline captured yet — the first Apply snapshots the terrain automatically."
                : builder.baseline.width != builder.res
                    ? $"Baseline resolution {builder.baseline.width} does not match heightmap {builder.res} — recapture."
                    : $"Baseline {builder.baseline.width}×{builder.baseline.height} captured — formations re-apply on top of it non-destructively.";
            EditorGUILayout.HelpBox(status +
                "\nTo edit the base terrain with other tools: Restore Baseline → edit → Capture Baseline → Apply.",
                builder.baseline == null ? MessageType.Info : MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Capture Baseline",
                "Snapshot the current terrain as the ground that formations build on. The previous baseline is backed up first.")))
            {
                if (!builder.formationsApplied || EditorUtility.DisplayDialog("Capture Baseline",
                    "The terrain currently includes applied formations. Capturing now bakes them into the baseline permanently.\n\n" +
                    "Consider Restore Baseline first, then edit, then capture.",
                    "Capture Anyway", "Cancel"))
                    builder.CaptureBaseline();
            }
            EditorGUI.BeginDisabledGroup(builder.baseline == null);
            if (GUILayout.Button(new GUIContent("Restore Baseline",
                "Write the pre-formation ground back to the terrain and clear the painted rock (undoable).")))
                builder.RestoreBaseline();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(builder.baseline == null || !builder.HasBaselineBackup());
            if (GUILayout.Button("Restore Baseline From Backup"))
            {
                if (EditorUtility.DisplayDialog("Restore Baseline From Backup",
                    $"Overwrites the baseline with the most recent automatic backup ({builder.LatestBackupDescription()}) " +
                    "and re-applies the formations. Undoable.\n\nBackups are written before every capture, adoption and " +
                    "height-range change (Library/JibbersMapToolsBackups, last 8 kept).",
                    "Restore", "Cancel"))
                {
                    builder.RestoreBaselineFromBackup();
                    builder.Apply(false, true);
                }
            }
            EditorGUI.EndDisabledGroup();

            if (changed && builder.autoApply && builder.terrain != null)
                builder.ScheduleApply(builder.liveErosion);
        }

        void DeleteSelectedPoint()
        {
            if (editFormation < 0 || editFormation >= builder.formations.Count) return;
            var f = builder.formations[editFormation];
            if (f.area == null || selectedPoint < 0 || selectedPoint >= f.area.Count) return;
            Undo.RecordObject(builder, "Delete Formation Point");
            f.area.RemoveAt(selectedPoint);
            selectedPoint = -1;
            RefitAfterAreaEdit(f);
            MarkChanged();
        }

        void RefitAfterAreaEdit(Formation f)
        {
            if (f.area != null && f.area.Count >= 3 && (addingPoints || !f.fitted))
                builder.FitObject(f);
        }

        void MarkChanged()
        {
            EditorUtility.SetDirty(builder);
            if (builder.autoApply) builder.ScheduleApply(builder.liveErosion);
            SceneView.RepaintAll();
        }

        void OnSceneGUI()
        {
            builder = (FormationBuilder)target;
            if (builder.terrain == null || builder.formations == null) return;

            DrawPreviews();
            if (builder.visualizeBaseline) DrawBaselineOverlay();

            if (editFormation < 0 || editFormation >= builder.formations.Count) return;
            var f = builder.formations[editFormation];

            HandleEditInput(f);
            DrawPointHandles(f);
        }

        int vizHash;
        readonly List<Vector3[]> vizGrid = new List<Vector3[]>();

        void DrawBaselineOverlay()
        {
            var data = builder.terrain.terrainData;
            if (data == null) return;

            if (builder.baseline != null && builder.baseline.width == builder.res)
            {
                int hash = builder.ContentHash() ^ (builder.baselineVersion * 397);
                if (hash != vizHash)
                {
                    vizHash = hash;
                    RebuildBaselineGrid(data);
                }
                Handles.color = new Color(MoreColors.Violet.r, MoreColors.Violet.g, MoreColors.Violet.b, 0.6f);
                foreach (var line in vizGrid)
                    Handles.DrawAAPolyLine(1.5f, line);
            }

            foreach (var f in builder.formations)
                if (f != null && f.enabled && builder.TryGetRegionRect(f, out var fr))
                    DrawHeightmapRect(data, fr, MoreColors.Mint, 0.15f);
            if (builder.TryGetLastWrittenRect(out var lastRect))
                DrawHeightmapRect(data, lastRect, MoreColors.Slate, 0.3f);
            if (builder.TryGetExternalDirtyRect(out var dirtyRect))
                DrawHeightmapRect(data, dirtyRect, MoreColors.Crimson, 0.45f);
            if (builder.TryGetPaintedMaskLocalRect(out var mMin, out var mMax))
                DrawLocalRect(data, mMin, mMax, MoreColors.Amber, 0.2f);
        }

        void RebuildBaselineGrid(TerrainData data)
        {
            vizGrid.Clear();
            var flat = builder.baseline.GetPixelData<float>(0);
            int res = builder.res;
            Vector3 tpos = builder.terrain.transform.position;
            float sx = data.size.x, sy = data.size.y, sz = data.size.z;

            foreach (var f in builder.formations)
            {
                if (f == null || !f.enabled || !builder.TryGetRegionRect(f, out var r)) continue;
                int step = Mathf.Max(1, Mathf.Max(r.width, r.height) / 20);
                var pts = new List<Vector3>();
                for (int gy = r.yMin; gy < r.yMax; gy += step)
                {
                    pts.Clear();
                    for (int gx = r.xMin; gx < r.xMax; gx += step)
                        pts.Add(tpos + new Vector3(gx / (float)(res - 1) * sx,
                            flat[gy * res + gx] * sy + 0.1f, gy / (float)(res - 1) * sz));
                    if (pts.Count >= 2) vizGrid.Add(pts.ToArray());
                }
                for (int gx = r.xMin; gx < r.xMax; gx += step)
                {
                    pts.Clear();
                    for (int gy = r.yMin; gy < r.yMax; gy += step)
                        pts.Add(tpos + new Vector3(gx / (float)(res - 1) * sx,
                            flat[gy * res + gx] * sy + 0.1f, gy / (float)(res - 1) * sz));
                    if (pts.Count >= 2) vizGrid.Add(pts.ToArray());
                }
            }
        }

        void DrawHeightmapRect(TerrainData data, RectInt r, Color c, float lift)
        {
            int res = builder.res;
            if (res < 2 || r.width <= 0 || r.height <= 0) return;
            var min = new Vector2(r.xMin / (float)(res - 1) * data.size.x, r.yMin / (float)(res - 1) * data.size.z);
            var max = new Vector2((r.xMax - 1) / (float)(res - 1) * data.size.x, (r.yMax - 1) / (float)(res - 1) * data.size.z);
            DrawLocalRect(data, min, max, c, lift);
        }

        void DrawLocalRect(TerrainData data, Vector2 min, Vector2 max, Color c, float lift)
        {
            Vector3 tpos = builder.terrain.transform.position;
            const int seg = 24;
            var pts = new Vector3[seg * 4 + 1];
            for (int i = 0; i < seg * 4; i++)
            {
                float t = (i % seg) / (float)seg;
                float x, z;
                switch (i / seg)
                {
                    case 0: x = Mathf.Lerp(min.x, max.x, t); z = min.y; break;
                    case 1: x = max.x; z = Mathf.Lerp(min.y, max.y, t); break;
                    case 2: x = Mathf.Lerp(max.x, min.x, t); z = max.y; break;
                    default: x = min.x; z = Mathf.Lerp(max.y, min.y, t); break;
                }
                float h = data.GetInterpolatedHeight(
                    Mathf.Clamp01(x / data.size.x), Mathf.Clamp01(z / data.size.z));
                pts[i] = tpos + new Vector3(x, h + lift, z);
            }
            pts[seg * 4] = pts[0];
            Handles.color = c;
            Handles.DrawAAPolyLine(3f, pts);
        }

        void HandleEditInput(Formation f)
        {
            int controlId = GUIUtility.GetControlID("FormationBuilder".GetHashCode(), FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            var evt = Event.current;

            if (evt.type == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    if (addingPoints) addingPoints = false;
                    else editFormation = -1;
                    evt.Use();
                    Repaint();
                    return;
                }
                if (evt.keyCode == KeyCode.X && selectedPoint >= 0)
                {
                    DeleteSelectedPoint();
                    evt.Use();
                    return;
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt
                && HandleUtility.nearestControl == controlId)
            {
                bool append = addingPoints || evt.control;
                bool insert = evt.shift;
                if (!append && !insert)
                {
                    if (selectedPoint >= 0)
                    {
                        selectedPoint = -1;
                        Repaint();
                    }
                    evt.Use();
                    return;
                }

                var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                var collider = builder.terrain.GetComponent<TerrainCollider>();
                if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
                {
                    Undo.RecordObject(builder, "Add Formation Point");
                    if (f.area == null) f.area = new List<Vector2>();
                    Vector2 local = builder.ClampAreaPoint(hit.point - builder.terrain.transform.position);

                    if (insert && f.area.Count >= 3)
                    {
                        int segIdx = NearestEdge(f.area, local);
                        f.area.Insert(segIdx + 1, local);
                        selectedPoint = segIdx + 1;
                    }
                    else
                    {
                        f.area.Add(local);
                        selectedPoint = f.area.Count - 1;
                    }
                    RefitAfterAreaEdit(f);
                    MarkChanged();
                    Repaint();
                }
                evt.Use();
            }
        }

        static int NearestEdge(List<Vector2> area, Vector2 p)
        {
            int n = area.Count;
            float best = float.MaxValue;
            int bestSeg = n - 1;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = area[i];
                Vector2 b = area[(i + 1) % n];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(Vector2.Dot(ab, ab), 1e-6f));
                float d2 = (p - (a + ab * t)).sqrMagnitude;
                if (d2 < best) { best = d2; bestSeg = i; }
            }
            return bestSeg;
        }

        void DrawPointHandles(Formation f)
        {
            if (f.area == null) return;
            Vector3 tpos = builder.terrain.transform.position;
            var evt = Event.current;
            bool deleteMode = evt.control;
            int n = f.area.Count;

            Handles.color = MoreColors.Slate;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = PointWorld(f.area[i]);
                Vector3 b = PointWorld(f.area[(i + 1) % n]);
                if (n >= 2) Handles.DrawDottedLine(a, b, 4f);
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 pt = f.area[i];
                Vector3 world = PointWorld(pt);
                float size = HandleUtility.GetHandleSize(world) * 0.14f;
                bool isSelected = i == selectedPoint;

                Handles.color = deleteMode ? MoreColors.Crimson : isSelected ? MoreColors.JibbersOrange : MoreColors.Azure;

                if (deleteMode)
                {
                    if (Handles.Button(world, Quaternion.identity, size, size * 2f, Handles.SphereHandleCap))
                    {
                        Undo.RecordObject(builder, "Delete Formation Point");
                        f.area.RemoveAt(i);
                        if (selectedPoint == i) selectedPoint = -1;
                        else if (selectedPoint > i) selectedPoint--;
                        RefitAfterAreaEdit(f);
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
                        Undo.RecordObject(builder, "Move Formation Point");
                        Vector3 target = np;
                        var collider = builder.terrain.GetComponent<TerrainCollider>();
                        var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                        if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
                            target = hit.point;
                        f.area[i] = builder.ClampAreaPoint(target - tpos);
                        MarkChanged();
                    }

                    EditorGUI.BeginChangeCheck();
                    Vector3 hp = Handles.PositionHandle(world, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(builder, "Move Formation Point");
                        f.area[i] = builder.ClampAreaPoint(hp - tpos);
                        MarkChanged();
                    }
                }
                else
                {
                    if (Handles.Button(world, Quaternion.identity, size, size * 2.5f, Handles.SphereHandleCap))
                    {
                        selectedPoint = i;
                        Repaint();
                    }
                }
                Handles.Label(world + Vector3.up * size * 5f, i.ToString(), EditorStyles.whiteBoldLabel);
            }
        }

        Vector3 PointWorld(Vector2 local)
        {
            Vector3 tpos = builder.terrain.transform.position;
            return tpos + new Vector3(local.x, builder.SampleBaselineHeight(local.x, local.y), local.y);
        }

        void DrawPreviews()
        {
            for (int fi = 0; fi < builder.formations.Count; fi++)
            {
                var f = builder.formations[fi];
                if (f == null) continue;
                bool isEditing = fi == editFormation;
                if (!f.drawPreview && !isEditing) continue;

                var preview = builder.GetPreview(f);
                if (preview == null || preview.outline == null) continue;

                Handles.color = !f.enabled ? MoreColors.Slate : isEditing ? MoreColors.JibbersOrange : MoreColors.Mint;
                Handles.DrawAAPolyLine(isEditing ? 3f : 2f, preview.outline);

                if (preview.hasPeak)
                {
                    Handles.color = f.enabled ? MoreColors.Violet : MoreColors.Slate;
                    Handles.DrawLine(preview.peakBase, preview.peakTop, 2f);
                    Handles.color = MoreColors.Amber;
                    float s = HandleUtility.GetHandleSize(preview.peakTop) * 0.05f;
                    Handles.SphereHandleCap(0, preview.peakTop, Quaternion.identity, s, EventType.Repaint);
                }

                if (!string.IsNullOrEmpty(f.name))
                    Handles.Label(preview.labelPos + Vector3.up * 1.5f, f.name);
            }
        }
    }
#endif

    [ExecuteInEditMode]
    [RequireComponent(typeof(Terrain))]
    [AddComponentMenu("Jibbers/Formation Builder")]
    public class FormationBuilder : MonoBehaviour
    {
        public List<Formation> formations = new List<Formation>();
        public bool autoApply = true;
        public bool liveErosion = true;
        public bool visualizeBaseline;

        [HideInInspector] public Texture2D baseline;
        [HideInInspector] public Texture2D maskBaseline;
        [HideInInspector] public bool formationsApplied;
        [SerializeField, HideInInspector] bool maskPainted;
        [SerializeField, HideInInspector] int paintedPx0 = -1, paintedPy0 = -1, paintedPx1 = -1, paintedPy1 = -1;

        [HideInInspector] public Terrain terrain;
        TerrainData data;
        [HideInInspector] public int res;

        const int DomeProfileRes = 256;
        const int RegionPad = 8;

        ComputeShader formationShader;
        int kClearRock, kGenerate, kSmooth, kThermal, kHeightToInt, kDroplet, kIntToHeight, kSnowAccumulate, kSnowSettle, kComposite;

        RenderTexture heightRT, rockRT;
        RenderTexture hA, hB, snowA, snowB, simBase, simMask;
        ComputeBuffer heightIntBuffer;
        int simRegW, simRegH;

        readonly HashSet<ComputeBuffer> liveBuffers = new HashSet<ComputeBuffer>();

        [SerializeField, HideInInspector] RectInt lastHeightRegion;
        [SerializeField, HideInInspector] bool hasLastHeightRegion;
        [System.NonSerialized] float[] lastWritten;
        [System.NonSerialized] RectInt externalDirty;
        [System.NonSerialized] bool hasExternalDirty;
        [System.NonSerialized] bool selfWrite;
        [System.NonSerialized] bool warnedExternal;
        [System.NonSerialized] public int baselineVersion;
        [System.NonSerialized] int appliedHash;
        [System.NonSerialized] bool appliedSimulated;
        [System.NonSerialized] bool appliedInteractive;
        [System.NonSerialized] bool hasAppliedHash;

        void OnEnable()
        {
            if (Application.isPlaying)
            {
                Destroy(this);
                return;
            }

#if UNITY_EDITOR
            if (formationShader == null)
            {
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("FormationEdit t:ComputeShader"))
                {
                    var candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null) { formationShader = candidate; break; }
                }
            }
#endif
            if (formationShader == null)
            {
                Debug.LogError("[FormationBuilder] FormationEdit compute shader not found.");
                enabled = false;
                return;
            }

            kClearRock = formationShader.FindKernel("ClearRock");
            kGenerate = formationShader.FindKernel("Generate");
            kSmooth = formationShader.FindKernel("Smooth");
            kThermal = formationShader.FindKernel("Thermal");
            kHeightToInt = formationShader.FindKernel("HeightToInt");
            kDroplet = formationShader.FindKernel("Droplet");
            kIntToHeight = formationShader.FindKernel("IntToHeight");
            kSnowAccumulate = formationShader.FindKernel("SnowAccumulate");
            kSnowSettle = formationShader.FindKernel("SnowSettle");
            kComposite = formationShader.FindKernel("Composite");

            terrain = GetComponent<Terrain>();
            data = terrain != null ? terrain.terrainData : null;
            if (data == null)
            {
                Debug.LogError("[FormationBuilder] Terrain has no TerrainData.");
                enabled = false;
                return;
            }

            res = data.heightmapResolution;
            CreateHeightRTs();

            appliedHash = ContentHash();
            appliedSimulated = true;
            hasAppliedHash = true;

            TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
        }

        void OnDisable()
        {
            TerrainCallbacks.heightmapChanged -= OnHeightmapChanged;
            ReleaseRT(ref heightRT); ReleaseRT(ref rockRT);
            ReleaseSimRTs();
            foreach (var buffer in liveBuffers) buffer?.Release();
            liveBuffers.Clear();
        }

        void OnHeightmapChanged(Terrain t, RectInt region, bool synched)
        {
            if (t != terrain || selfWrite) return;
            externalDirty = hasExternalDirty ? UnionRect(externalDirty, region) : region;
            hasExternalDirty = true;
#if UNITY_EDITOR
            if (!warnedExternal && !UnityEditor.Undo.isProcessing && HasBlockingExternalEdits())
            {
                warnedExternal = true;
                Debug.LogWarning("[FormationBuilder] The terrain was edited inside formation areas. Auto-apply is paused so the edit is not overwritten — resolve it in the Formation Builder inspector (Adopt or Overwrite). The baseline is never changed automatically.");
            }
#endif
        }

        RectInt CurrentRegionsRect()
        {
            var rect = new RectInt(0, 0, 0, 0);
            if (formations != null && data != null)
                foreach (var f in formations)
                {
                    if (f == null || !f.enabled || f.area == null || f.area.Count < 3) continue;
                    var r = ComputeRegion(f);
                    if (r.valid) rect = UnionRect(rect, new RectInt(r.x0, r.y0, r.w, r.h));
                }
            return rect;
        }

        RectInt PendingWriteRect()
        {
            var current = CurrentRegionsRect();
            return hasLastHeightRegion ? UnionRect(current, lastHeightRegion) : current;
        }

        static RectInt Intersect(RectInt a, RectInt b)
        {
            int x0 = Mathf.Max(a.xMin, b.xMin);
            int y0 = Mathf.Max(a.yMin, b.yMin);
            int x1 = Mathf.Min(a.xMax, b.xMax);
            int y1 = Mathf.Min(a.yMax, b.yMax);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        RectInt ClampToRes(RectInt r)
        {
            int x0 = Mathf.Clamp(r.xMin, 0, res);
            int y0 = Mathf.Clamp(r.yMin, 0, res);
            int x1 = Mathf.Clamp(r.xMax, 0, res);
            int y1 = Mathf.Clamp(r.yMax, 0, res);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        public bool HasBlockingExternalEdits()
        {
            if (!hasExternalDirty || data == null || baseline == null) return false;
            var inter = Intersect(ClampToRes(externalDirty), PendingWriteRect());
            return inter.width > 0 && inter.height > 0;
        }

        public bool CanAdoptExternalEdits()
        {
            if (!HasBlockingExternalEdits()) return true;
            var inter = ClampToRes(Intersect(externalDirty, PendingWriteRect()));
            bool haveCache = lastWritten != null && lastWritten.Length == res * res;
            for (int y = inter.yMin; y < inter.yMax; y++)
                for (int x = inter.xMin; x < inter.xMax; x++)
                {
                    if (haveCache && !float.IsNaN(lastWritten[y * res + x])) continue;
                    if (hasLastHeightRegion && !lastHeightRegion.Contains(new Vector2Int(x, y))) continue;
                    return false;
                }
            return true;
        }

        public void AdoptExternalEdits()
        {
            if (!hasExternalDirty || data == null) return;
            if (baseline == null || baseline.width != res || baseline.height != res) return;
            var rect = ClampToRes(externalDirty);
            if (rect.width <= 0 || rect.height <= 0) { ClearExternalDirty(); return; }

#if UNITY_EDITOR
            BackupBaseline("before adopting external edits");
            UnityEditor.Undo.RegisterCompleteObjectUndo(baseline, "Adopt External Terrain Edits");
#endif
            var cur = data.GetHeights(rect.xMin, rect.yMin, rect.width, rect.height);
            var flat = baseline.GetPixelData<float>(0);
            bool haveCache = lastWritten != null && lastWritten.Length == res * res;
            bool changed = false;

            for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                {
                    int gx = rect.xMin + x, gy = rect.yMin + y;
                    int gi = gy * res + gx;
                    float nv;
                    if (haveCache && !float.IsNaN(lastWritten[gi]))
                        nv = Mathf.Clamp01(flat[gi] + (cur[y, x] - lastWritten[gi]));
                    else if (hasLastHeightRegion && !lastHeightRegion.Contains(new Vector2Int(gx, gy)))
                        nv = cur[y, x];
                    else
                        continue;
                    if (flat[gi] != nv) { flat[gi] = nv; changed = true; }
                }

            if (changed)
            {
                baseline.Apply(false, false);
                baselineVersion++;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(baseline);
#endif
            }
            ClearExternalDirty();
        }

        public void OverwriteExternalEdits()
        {
#if UNITY_EDITOR
            if (data != null)
                UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Apply Formations Over External Edits");
#endif
            ClearExternalDirty();
        }

        void ClearExternalDirty()
        {
            hasExternalDirty = false;
            warnedExternal = false;
        }

        public void NotifyUndoRedo()
        {
            ClearExternalDirty();
            lastWritten = null;
            baselineVersion++;
        }

        public bool TryGetRegionRect(Formation f, out RectInt rect)
        {
            rect = default;
            if (f == null || data == null || f.area == null || f.area.Count < 3) return false;
            var r = ComputeRegion(f);
            if (!r.valid) return false;
            rect = new RectInt(r.x0, r.y0, r.w, r.h);
            return true;
        }

        public bool TryGetLastWrittenRect(out RectInt rect)
        {
            rect = lastHeightRegion;
            return hasLastHeightRegion && rect.width > 0 && rect.height > 0;
        }

        public bool TryGetExternalDirtyRect(out RectInt rect)
        {
            rect = ClampToRes(externalDirty);
            return hasExternalDirty && rect.width > 0 && rect.height > 0;
        }

        public bool TryGetPaintedMaskLocalRect(out Vector2 min, out Vector2 max)
        {
            min = max = default;
            if (!maskPainted || paintedPx0 < 0 || data == null) return false;
            var mask = AcquireMask(false);
            if (mask == null) return false;
            float mw = mask.width, mh = mask.height;
            min = new Vector2(paintedPx0 / mw * data.size.x, (1f - (paintedPy1 + 1) / mh) * data.size.z);
            max = new Vector2((paintedPx1 + 1) / mw * data.size.x, (1f - paintedPy0 / mh) * data.size.z);
            return true;
        }

        public void NotifyHeightRangeChanged(float scale, float offsetN)
        {
            if (baseline != null && baseline.width == res && baseline.height == res)
            {
#if UNITY_EDITOR
                BackupBaseline("before height-range change");
                UnityEditor.Undo.RegisterCompleteObjectUndo(baseline, "Change Terrain Height Range");
#endif
                var flat = baseline.GetPixelData<float>(0);
                for (int i = 0; i < flat.Length; i++) flat[i] = Mathf.Clamp01(flat[i] * scale + offsetN);
                baseline.Apply(false, false);
                baselineVersion++;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(baseline);
#endif
            }
            lastWritten = null;
            ClearExternalDirty();
#if UNITY_EDITOR
            if (autoApply) ScheduleApply(liveErosion);
#endif
        }

        static void ReleaseRT(ref RenderTexture rt)
        {
            if (rt != null) { rt.Release(); rt = null; }
        }

        void CreateHeightRTs()
        {
            ReleaseRT(ref heightRT);
            ReleaseRT(ref rockRT);
            heightRT = NewRT(res, res, RenderTextureFormat.RFloat);
            rockRT = NewRT(res, res, RenderTextureFormat.RFloat);
        }

        static RenderTexture NewRT(int w, int h, RenderTextureFormat fmt)
        {
            var rt = new RenderTexture(w, h, 0, fmt) { enableRandomWrite = true };
            rt.Create();
            return rt;
        }

        void ReleaseSimRTs()
        {
            ReleaseRT(ref hA); ReleaseRT(ref hB);
            ReleaseRT(ref snowA); ReleaseRT(ref snowB);
            ReleaseRT(ref simBase); ReleaseRT(ref simMask);
            if (heightIntBuffer != null) { heightIntBuffer.Release(); heightIntBuffer = null; }
            simRegW = simRegH = 0;
        }

        void EnsureSimRTs(int w, int h)
        {
            if (hA != null && simRegW >= w && simRegH >= h) return;
            int nw = Mathf.Max(w, simRegW);
            int nh = Mathf.Max(h, simRegH);
            ReleaseSimRTs();
            hA = NewRT(nw, nh, RenderTextureFormat.RFloat);
            hB = NewRT(nw, nh, RenderTextureFormat.RFloat);
            snowA = NewRT(nw, nh, RenderTextureFormat.RFloat);
            snowB = NewRT(nw, nh, RenderTextureFormat.RFloat);
            simBase = NewRT(nw, nh, RenderTextureFormat.RFloat);
            simMask = NewRT(nw, nh, RenderTextureFormat.RFloat);
            heightIntBuffer = new ComputeBuffer(nw * nh, sizeof(int));
            simRegW = nw; simRegH = nh;
        }

        public Vector2 ClampAreaPoint(Vector3 local)
        {
            if (data == null) return new Vector2(local.x, local.z);
            return new Vector2(
                Mathf.Clamp(local.x, 0f, data.size.x),
                Mathf.Clamp(local.z, 0f, data.size.z));
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

#if UNITY_EDITOR
        public FormationPreview GetPreview(Formation f)
        {
            if (f == null || f.area == null || f.area.Count < 2 || data == null) return null;
            int hash = FormationArea.ComputeHash(f, transform.position, data.size, res);
            if (f.preview != null && f.previewHash == hash) return f.preview;

            Vector3 tpos = transform.position;
            int n = f.area.Count;
            var outline = new Vector3[n + 1];
            for (int i = 0; i < n; i++)
            {
                var a = f.area[i];
                outline[i] = tpos + new Vector3(a.x, SampleBaselineHeight(a.x, a.y), a.y);
            }
            outline[n] = outline[0];

            var pv = new FormationPreview { outline = outline, labelPos = outline[0] };
            if (n >= 3)
            {
                Vector2 peak = FormationArea.PeakPoint(f.area, out float maxD);
                float by = SampleBaselineHeight(peak.x, peak.y);
                pv.peakBase = tpos + new Vector3(peak.x, by, peak.y);
                float domeT = f.domeReach > 0f ? Mathf.Clamp01(maxD / f.domeReach) : 1f;
                float ph = (f.domeProfile != null ? f.domeProfile.Evaluate(domeT) : domeT) * f.height + f.baseHeight;
                pv.peakTop = pv.peakBase + Vector3.up * ph;
                pv.labelPos = pv.peakBase;
                pv.hasPeak = Mathf.Abs(ph) > 0.01f;
            }
            f.preview = pv;
            f.previewHash = hash;
            return pv;
        }
#endif

        struct Region { public int x0, y0, w, h; public bool valid; }

        Region ComputeRegion(Formation f)
        {
            var r = new Region();
            if (!FormationArea.Bounds(f.area, out Vector2 min, out Vector2 max)) return r;
            float pxPerX = (res - 1) / data.size.x;
            float pxPerZ = (res - 1) / data.size.z;
            int rx0 = Mathf.Clamp(Mathf.FloorToInt(min.x * pxPerX) - RegionPad, 0, res - 1);
            int ry0 = Mathf.Clamp(Mathf.FloorToInt(min.y * pxPerZ) - RegionPad, 0, res - 1);
            int rx1 = Mathf.Clamp(Mathf.CeilToInt(max.x * pxPerX) + RegionPad, 0, res - 1);
            int ry1 = Mathf.Clamp(Mathf.CeilToInt(max.y * pxPerZ) + RegionPad, 0, res - 1);
            r.x0 = rx0; r.y0 = ry0; r.w = rx1 - rx0 + 1; r.h = ry1 - ry0 + 1;
            r.valid = r.w >= 1 && r.h >= 1;
            return r;
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

        int EffectiveOctaves(Formation f)
        {
            int oct = Mathf.Clamp(f.octaves, 1, 9);
            if (data == null || res < 2) return oct;
            float cell = data.size.x / (res - 1);
            float finest = Mathf.Max(cell * 3f, 0.001f);
            if (f.noiseScale <= finest || f.lacunarity <= 1.001f) return 1;
            int allowed = 1 + Mathf.FloorToInt(Mathf.Log(f.noiseScale / finest) / Mathf.Log(f.lacunarity));
            return Mathf.Clamp(Mathf.Min(oct, allowed), 1, 9);
        }

        public float TerrainHeight() => data != null ? data.size.y : 100f;

        float RimLevelN(Formation f)
        {
            if (f.area == null || f.area.Count == 0 || data == null) return 0f;
            float sum = 0f;
            foreach (var p in f.area) sum += SampleBaselineHeight(p.x, p.y);
            return sum / (f.area.Count * data.size.y);
        }

        public float AreaRadius(Formation f)
        {
            if (f != null && f.area != null && f.area.Count >= 3)
            {
                FormationArea.PeakPoint(f.area, out float maxD);
                if (maxD > 0.5f) return maxD;
            }
            return data != null ? 0.12f * Mathf.Min(data.size.x, data.size.z) : 50f;
        }

        public void FitObject(Formation f)
        {
            if (f == null) return;
            float radius = AreaRadius(f);
            if (radius < 1f) return;
            float factor = radius / Mathf.Max(f.domeReach, 0.01f);
            f.height *= factor;
            f.domeReach = radius;
            f.noiseScale *= factor;
            f.noiseHeight *= factor;
            f.edgeFalloff *= factor;
            f.baseHeight *= factor;
            f.hydraulicIterations = Mathf.Clamp(Mathf.RoundToInt(f.hydraulicIterations * factor), 8, 4000);
            float maxH = TerrainHeight() * 0.9f;
            f.height = Mathf.Sign(f.height) * Mathf.Min(Mathf.Abs(f.height), maxH);
            f.fitted = true;
            f.preview = null;
        }

        public void Apply(bool registerUndo = true, bool simulate = true, bool interactive = false)
        {
            if (terrain == null || data == null || formationShader == null || !isActiveAndEnabled) return;
            if (data.heightmapResolution != res) { res = data.heightmapResolution; CreateHeightRTs(); }
            if (heightRT == null || !heightRT.IsCreated() || heightRT.width != res) CreateHeightRTs();

            int maxW = 0, maxH = 0;
            var currentRect = new RectInt(0, 0, 0, 0);
            var regions = new List<Region>();
            if (formations != null)
                foreach (var f in formations)
                {
                    if (f == null || !f.enabled || f.area == null || f.area.Count < 3) { regions.Add(default(Region)); continue; }
                    var r = ComputeRegion(f);
                    regions.Add(r);
                    if (r.valid)
                    {
                        maxW = Mathf.Max(maxW, r.w); maxH = Mathf.Max(maxH, r.h);
                        currentRect = UnionRect(currentRect, new RectInt(r.x0, r.y0, r.w, r.h));
                    }
                }
            bool anyRun = maxW > 0;

            if (!anyRun && !formationsApplied)
            {
                appliedHash = ContentHash();
                appliedSimulated = true;
                appliedInteractive = false;
                hasAppliedHash = true;
                return;
            }

            EnsureBaseline();
            if (baseline == null || baseline.width != res || baseline.height != res) return;

            if (HasBlockingExternalEdits())
            {
#if UNITY_EDITOR
                if (!registerUndo || interactive) return;
                if (CanAdoptExternalEdits())
                {
                    int choice = UnityEditor.EditorUtility.DisplayDialogComplex("External Terrain Edits",
                        "The terrain was edited outside Formation Builder inside formation areas. Applying will overwrite those edits.",
                        "Adopt Into Baseline", "Cancel", "Overwrite");
                    if (choice == 1) return;
                    if (choice == 0) AdoptExternalEdits();
                    else hasExternalDirty = false;
                }
                else
                {
                    if (!UnityEditor.EditorUtility.DisplayDialog("External Terrain Edits",
                        "The terrain was edited outside Formation Builder inside formation areas, and the pre-edit state is no longer cached (script reload). Applying will overwrite those edits (undoable).",
                        "Overwrite", "Cancel")) return;
                    hasExternalDirty = false;
                }
#else
                return;
#endif
            }

#if UNITY_EDITOR
            if (registerUndo)
                UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Apply Formations");
#endif
            Graphics.Blit(baseline, heightRT);

            int clearGroups = Mathf.Max(1, Mathf.CeilToInt(res / 8f));
            formationShader.SetInt("res", res);
            formationShader.SetTexture(kClearRock, "RockMask", rockRT);
            formationShader.Dispatch(kClearRock, clearGroups, clearGroups, 1);

            if (anyRun) EnsureSimRTs(maxW, maxH);

            var batch = new List<ComputeBuffer>();
            bool anyRock = false;
            if (formations != null)
                for (int i = 0; i < formations.Count; i++)
                {
                    var f = formations[i];
                    if (f == null || !f.enabled || f.area == null || f.area.Count < 3) continue;
                    var r = regions[i];
                    if (!r.valid) continue;
                    RunFormation(f, r, simulate, batch);
                    if (f.snowEnabled && f.rockStrength > 0f) anyRock = true;
                }

            var readRect = hasLastHeightRegion
                ? UnionRect(currentRect, lastHeightRegion)
                : currentRect;
            if (readRect.width <= 0 || readRect.height <= 0) readRect = new RectInt(0, 0, res, res);
            lastHeightRegion = currentRect;
            hasLastHeightRegion = true;

            ReadBackHeight(batch, readRect);
            if (!interactive) PaintRock(registerUndo, anyRock);
            formationsApplied = anyRun;
            appliedHash = ContentHash();
            appliedSimulated = simulate;
            appliedInteractive = interactive;
            hasAppliedHash = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            InvalidatePreviews();
        }

        void SetCommonSim(Region r)
        {
            formationShader.SetInt("res", res);
            formationShader.SetInts("regionMin", r.x0, r.y0);
            formationShader.SetInts("regionSize", r.w, r.h);
            formationShader.SetFloat("sizeX", data.size.x);
            formationShader.SetFloat("sizeZ", data.size.z);
            formationShader.SetFloat("sizeY", data.size.y);
            formationShader.SetFloat("cellX", data.size.x / (res - 1));
            formationShader.SetFloat("cellZ", data.size.z / (res - 1));
            formationShader.SetFloat("terrainPosY", transform.position.y);
            formationShader.SetInt("snowStencil", Mathf.Clamp(Mathf.RoundToInt(0.5f / (data.size.x / (res - 1))), 1, 8));
        }

        void Dispatch(int kernel, Region r)
        {
            formationShader.Dispatch(kernel,
                Mathf.Max(1, Mathf.CeilToInt(r.w / 8f)),
                Mathf.Max(1, Mathf.CeilToInt(r.h / 8f)), 1);
        }

        void RunFormation(Formation f, Region r, bool simulate, List<ComputeBuffer> batch)
        {
            int n = f.area.Count;
            var poly = new Vector2[n];
            for (int i = 0; i < n; i++) poly[i] = f.area[i];
            var polyBuffer = new ComputeBuffer(n, sizeof(float) * 2);
            polyBuffer.SetData(poly);
            batch.Add(polyBuffer);
            liveBuffers.Add(polyBuffer);

            var dome = new float[DomeProfileRes];
            if (f.domeProfile != null && f.domeProfile.length > 0)
                for (int i = 0; i < DomeProfileRes; i++)
                    dome[i] = f.domeProfile.Evaluate((float)i / (DomeProfileRes - 1));
            else
                for (int i = 0; i < DomeProfileRes; i++)
                    dome[i] = (float)i / (DomeProfileRes - 1);
            var domeBuffer = new ComputeBuffer(DomeProfileRes, sizeof(float));
            domeBuffer.SetData(dome);
            batch.Add(domeBuffer);
            liveBuffers.Add(domeBuffer);

            SetCommonSim(r);

            RenderTexture hCur = hA, hAlt = hB, snCur = snowA, snAlt = snowB;

            formationShader.SetBuffer(kGenerate, "Poly", polyBuffer);
            formationShader.SetInt("polyCount", n);
            formationShader.SetBuffer(kGenerate, "DomeProfile", domeBuffer);
            formationShader.SetInt("domeProfileRes", DomeProfileRes);
            formationShader.SetFloat("edgeFalloff", f.edgeFalloff);
            formationShader.SetFloat("domeReach", f.domeReach);
            formationShader.SetFloat("peakHeight", f.height);
            formationShader.SetInt("blendMode", (int)f.blendMode);
            formationShader.SetFloat("baseHeight", f.baseHeight / data.size.y);
            formationShader.SetFloat("baseLevelN", RimLevelN(f));
            formationShader.SetInt("noiseType", (int)f.noiseType);
            formationShader.SetFloat("noiseHeight", f.noiseHeight);
            formationShader.SetFloat("noiseScale", f.noiseScale);
            formationShader.SetInt("octaves", EffectiveOctaves(f));
            formationShader.SetFloat("lacunarity", f.lacunarity);
            formationShader.SetFloat("gain", f.gain);
            formationShader.SetFloat("warp", f.warp);
            formationShader.SetInt("seed", unchecked(f.seed * 1013904223 + 1));
            formationShader.SetFloat("noiseFollowsDome", f.noiseFollowsDome);
            formationShader.SetTexture(kGenerate, "HeightMap", heightRT);
            formationShader.SetTexture(kGenerate, "HOut", hCur);
            formationShader.SetTexture(kGenerate, "SimBase", simBase);
            formationShader.SetTexture(kGenerate, "SimMask", simMask);
            Dispatch(kGenerate, r);

            if (f.smooth > 0f && f.smoothIterations > 0)
            {
                int smoothIters = Mathf.Min(f.smoothIterations, 64);
                formationShader.SetFloat("smoothStrength", f.smooth);
                formationShader.SetTexture(kSmooth, "SimMask", simMask);
                for (int i = 0; i < smoothIters; i++)
                {
                    formationShader.SetTexture(kSmooth, "HIn", hCur);
                    formationShader.SetTexture(kSmooth, "HOut", hAlt);
                    Dispatch(kSmooth, r);
                    Swap(ref hCur, ref hAlt);
                }
            }

            if (simulate)
            {
                if (f.thermalEnabled && f.thermalIterations > 0 && f.thermalStrength > 0f)
                {
                    int thermalIters = Mathf.Min(f.thermalIterations, 512);
                    float talus = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(f.thermalRepose, 1f, 85f)) * (data.size.x / (res - 1)) / data.size.y;
                    formationShader.SetFloat("talusThreshold", talus);
                    formationShader.SetFloat("thermalStrength", f.thermalStrength);
                    for (int i = 0; i < thermalIters; i++)
                    {
                        formationShader.SetTexture(kThermal, "HIn", hCur);
                        formationShader.SetTexture(kThermal, "HOut", hAlt);
                        Dispatch(kThermal, r);
                        Swap(ref hCur, ref hAlt);
                    }
                }

                if (f.hydraulicEnabled)
                {
                    float cellX = data.size.x / (res - 1);
                    float cellArea = cellX * (data.size.z / (res - 1));
                    float polyCells = FormationArea.Area(f.area) / Mathf.Max(cellArea, 1e-6f);
                    int nDroplets = Mathf.Clamp(Mathf.RoundToInt(polyCells * Mathf.Max(f.rain, 0.02f)), 256, 400000);
                    formationShader.SetInt("maxLifetime", Mathf.Clamp(f.hydraulicIterations, 8, 4000));
                    formationShader.SetFloat("inertia", Mathf.Clamp01(f.dropletInertia));
                    formationShader.SetFloat("capacity", Mathf.Max(f.sedimentCapacity, 0.01f));
                    formationShader.SetFloat("deposit", Mathf.Clamp01(f.depositionRate));
                    formationShader.SetFloat("erode", Mathf.Clamp01(f.erosionRate));
                    formationShader.SetFloat("evaporate", Mathf.Clamp01(f.evaporation));
                    formationShader.SetInt("erosionRadius", Mathf.Clamp(f.erosionRadius, 1, 4));
                    formationShader.SetInt("dropletSeed", unchecked(f.seed * 747796405 + 1));
                    formationShader.SetFloat("maxErodeStep", Mathf.Max(2f * cellX, 0.5f) / data.size.y);

                    formationShader.SetBuffer(kHeightToInt, "HeightInt", heightIntBuffer);
                    formationShader.SetTexture(kHeightToInt, "HIn", hCur);
                    Dispatch(kHeightToInt, r);

                    formationShader.SetBuffer(kDroplet, "HeightInt", heightIntBuffer);
                    formationShader.SetTexture(kDroplet, "SimMask", simMask);
                    int waves = Mathf.Clamp(Mathf.CeilToInt(nDroplets / 8192f), 1, 64);
                    int waveSize = Mathf.CeilToInt(nDroplets / (float)waves);
                    for (int w = 0; w < waves; w++)
                    {
                        int count = Mathf.Min(waveSize, nDroplets - w * waveSize);
                        if (count <= 0) break;
                        formationShader.SetInt("numDroplets", count);
                        formationShader.SetInt("dropletOffset", w * waveSize);
                        formationShader.Dispatch(kDroplet, Mathf.Max(1, Mathf.CeilToInt(count / 64f)), 1, 1);
                    }

                    formationShader.SetBuffer(kIntToHeight, "HeightInt", heightIntBuffer);
                    formationShader.SetTexture(kIntToHeight, "HOut", hCur);
                    Dispatch(kIntToHeight, r);

                    formationShader.SetFloat("talusThreshold", Mathf.Tan(Mathf.Deg2Rad * 70f) * cellX / data.size.y);
                    formationShader.SetFloat("thermalStrength", 0.5f);
                    for (int i = 0; i < 3; i++)
                    {
                        formationShader.SetTexture(kThermal, "HIn", hCur);
                        formationShader.SetTexture(kThermal, "HOut", hAlt);
                        Dispatch(kThermal, r);
                        Swap(ref hCur, ref hAlt);
                    }
                }
            }

            if (f.snowEnabled)
            {
                formationShader.SetFloat("snowStrength", 1f);
                formationShader.SetTexture(kSnowSettle, "SnowIn", hCur);
                formationShader.SetTexture(kSnowSettle, "SnowOut", snCur);
                Dispatch(kSnowSettle, r);
                formationShader.SetTexture(kSnowSettle, "SnowIn", snCur);
                formationShader.SetTexture(kSnowSettle, "SnowOut", hAlt);
                Dispatch(kSnowSettle, r);

                formationShader.SetFloat("snowLineLow", f.snowLineLow);
                formationShader.SetFloat("snowLineHigh", f.snowLineHigh);
                formationShader.SetFloat("snowSlopeStart", f.snowSlopeStart);
                formationShader.SetFloat("snowSlopeFull", f.snowSlopeFull);
                formationShader.SetFloat("snowCrevice", f.snowCrevice);
                formationShader.SetTexture(kSnowAccumulate, "HIn", hCur);
                formationShader.SetTexture(kSnowAccumulate, "HStruct", hAlt);
                formationShader.SetTexture(kSnowAccumulate, "SnowOut", snCur);
                Dispatch(kSnowAccumulate, r);

                if (simulate && f.snowSettleIterations > 0)
                {
                    int settleIters = Mathf.Min(f.snowSettleIterations, 64);
                    formationShader.SetFloat("snowStrength", 0.5f);
                    for (int i = 0; i < settleIters; i++)
                    {
                        formationShader.SetTexture(kSnowSettle, "SnowIn", snCur);
                        formationShader.SetTexture(kSnowSettle, "SnowOut", snAlt);
                        Dispatch(kSnowSettle, r);
                        Swap(ref snCur, ref snAlt);
                    }
                }
            }

            formationShader.SetFloat("snowAmount", f.snowEnabled ? f.snowAmount / data.size.y : 0f);
            formationShader.SetFloat("snowAddsHeight", f.snowEnabled ? f.snowAddsHeight : 0f);
            formationShader.SetFloat("rockStrength", f.snowEnabled ? f.rockStrength : 0f);
            formationShader.SetTexture(kComposite, "HeightMap", heightRT);
            formationShader.SetTexture(kComposite, "RockMask", rockRT);
            formationShader.SetTexture(kComposite, "HIn", hCur);
            formationShader.SetTexture(kComposite, "SnowIn", snCur);
            formationShader.SetTexture(kComposite, "SimBase", simBase);
            formationShader.SetTexture(kComposite, "SimMask", simMask);
            Dispatch(kComposite, r);
        }

        static void Swap(ref RenderTexture a, ref RenderTexture b)
        {
            var t = a; a = b; b = t;
        }

        void ReadBackHeight(List<ComputeBuffer> batch, RectInt rect)
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

                if (lastWritten == null || lastWritten.Length != requestRes * requestRes)
                {
                    lastWritten = new float[requestRes * requestRes];
                    for (int i = 0; i < lastWritten.Length; i++) lastWritten[i] = float.NaN;
                }
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < rw; x++)
                        lastWritten[(ry + y) * requestRes + rx + x] = heights[y, x];

                selfWrite = true;
                try
                {
                    data.SetHeightsDelayLOD(rx, ry, heights);
                    data.SyncHeightmap();
                }
                finally { selfWrite = false; }
            });
        }

        void PaintRock(bool registerUndo, bool anyRock)
        {
            if (!anyRock && !maskPainted) return;

            var mask = AcquireMask(registerUndo && anyRock);
            if (mask == null) return;

            if (maskBaseline == null || maskBaseline.width != mask.width || maskBaseline.height != mask.height)
                CaptureMaskBaseline(mask);

#if UNITY_EDITOR
            if (registerUndo)
                UnityEditor.Undo.RegisterCompleteObjectUndo(mask, "Apply Formations");
#endif

            int requestRes = res;
            bool willPaint = anyRock;
            AsyncGPUReadback.Request(rockRT, 0, request =>
            {
                if (request.hasError) return;
                if (this == null || data == null || mask == null || maskBaseline == null) return;
                if (requestRes != res) return;
                if (maskBaseline.width != mask.width || maskBaseline.height != mask.height) return;

                var raw = request.GetData<float>();
                if (raw.Length < requestRes * requestRes) return;

                int mw = mask.width, mh = mask.height;
                int nx0 = 0, ny0 = 0, nx1 = -1, ny1 = -1;
                bool haveNew = willPaint && ComputeMaskBounds(mw, mh, out nx0, out ny0, out nx1, out ny1);
                bool haveOld = maskPainted && paintedPx0 >= 0 && paintedPx1 >= paintedPx0 && paintedPy1 >= paintedPy0;
                if (!haveNew && !haveOld) return;

                int ox0 = haveOld ? Mathf.Clamp(paintedPx0, 0, mw - 1) : nx0;
                int oy0 = haveOld ? Mathf.Clamp(paintedPy0, 0, mh - 1) : ny0;
                int ox1 = haveOld ? Mathf.Clamp(paintedPx1, 0, mw - 1) : nx1;
                int oy1 = haveOld ? Mathf.Clamp(paintedPy1, 0, mh - 1) : ny1;
                int ux0 = haveNew ? Mathf.Min(nx0, ox0) : ox0;
                int uy0 = haveNew ? Mathf.Min(ny0, oy0) : oy0;
                int ux1 = haveNew ? Mathf.Max(nx1, ox1) : ox1;
                int uy1 = haveNew ? Mathf.Max(ny1, oy1) : oy1;

                var basePixels = maskBaseline.GetPixels32();
                var pixels = mask.GetPixels32();

                for (int py = uy0; py <= uy1; py++)
                    for (int px = ux0; px <= ux1; px++)
                    {
                        int i = py * mw + px;
                        pixels[i].r = basePixels[i].r;
                    }

                if (haveNew)
                {
                    for (int py = ny0; py <= ny1; py++)
                    {
                        float mzN = 1f - (py + 0.5f) / mh;
                        for (int px = nx0; px <= nx1; px++)
                        {
                            float mxN = (px + 0.5f) / mw;
                            float cov = SampleRock(raw, requestRes, mxN * (requestRes - 1), mzN * (requestRes - 1));
                            if (cov <= 0f) continue;
                            int i = py * mw + px;
                            pixels[i].r = (byte)Mathf.RoundToInt(Mathf.Lerp(basePixels[i].r, 0f, Mathf.Clamp01(cov)));
                        }
                    }
                }

                mask.SetPixels32(pixels);
                mask.Apply(mask.mipmapCount > 1, false);
                maskPainted = haveNew;
                if (haveNew) { paintedPx0 = nx0; paintedPy0 = ny0; paintedPx1 = nx1; paintedPy1 = ny1; }
                else { paintedPx0 = paintedPy0 = paintedPx1 = paintedPy1 = -1; }
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(mask);
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            });
        }

        bool ComputeMaskBounds(int mw, int mh, out int px0, out int py0, out int px1, out int py1)
        {
            px0 = mw; py0 = mh; px1 = -1; py1 = -1;
            bool any = false;
            foreach (var f in formations)
            {
                if (f == null || !f.enabled || f.area == null || f.area.Count < 3 || !f.snowEnabled || f.rockStrength <= 0f) continue;
                if (!FormationArea.Bounds(f.area, out Vector2 min, out Vector2 max)) continue;
                any = true;
                int a0 = Mathf.FloorToInt(min.x / data.size.x * mw) - 2;
                int a1 = Mathf.CeilToInt(max.x / data.size.x * mw) + 2;
                int b0 = Mathf.FloorToInt((1f - max.y / data.size.z) * mh) - 2;
                int b1 = Mathf.CeilToInt((1f - min.y / data.size.z) * mh) + 2;
                px0 = Mathf.Min(px0, a0); px1 = Mathf.Max(px1, a1);
                py0 = Mathf.Min(py0, b0); py1 = Mathf.Max(py1, b1);
            }
            if (!any) return false;
            px0 = Mathf.Clamp(px0, 0, mw - 1);
            px1 = Mathf.Clamp(px1, 0, mw - 1);
            py0 = Mathf.Clamp(py0, 0, mh - 1);
            py1 = Mathf.Clamp(py1, 0, mh - 1);
            return px1 >= px0 && py1 >= py0;
        }

        static float SampleRock(Unity.Collections.NativeArray<float> raw, int res, float xf, float zf)
        {
            xf = Mathf.Clamp(xf, 0f, res - 1);
            zf = Mathf.Clamp(zf, 0f, res - 1);
            int x0 = (int)xf, z0 = (int)zf;
            int x1 = Mathf.Min(x0 + 1, res - 1);
            int z1 = Mathf.Min(z0 + 1, res - 1);
            float tx = xf - x0, tz = zf - z0;
            float a = raw[z0 * res + x0];
            float b = raw[z0 * res + x1];
            float c = raw[z1 * res + x0];
            float d = raw[z1 * res + x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
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
                problem = "No _SnowMask texture assigned — create one with Better Terrain Editor.";
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
                Debug.LogWarning("[FormationBuilder] " + problem);
            return tex;
        }

        public string MaskProblem()
        {
            if (!AnyRock()) return null;
            AcquireMask(out string problem);
            return problem;
        }

        bool AnyRock()
        {
            if (formations == null) return false;
            foreach (var f in formations)
                if (f != null && f.enabled && f.area != null && f.area.Count >= 3 && f.snowEnabled && f.rockStrength > 0f)
                    return true;
            return false;
        }

        void CaptureMaskBaseline(Texture2D mask)
        {
            if (mask == null) return;
            bool fresh = maskBaseline == null || maskBaseline.width != mask.width || maskBaseline.height != mask.height;
            if (fresh)
            {
                maskBaseline = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false);
                maskBaseline.name = (terrain != null ? terrain.name : "Terrain") + "_FormationMaskBaseline";
            }
            maskBaseline.SetPixels32(mask.GetPixels32());
            maskBaseline.Apply(false, false);

#if UNITY_EDITOR
            if (fresh)
            {
                string dir = FindBestSaveDir();
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(dir + "/" + maskBaseline.name + ".asset");
                UnityEditor.AssetDatabase.CreateAsset(maskBaseline, path);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[FormationBuilder] Captured snow mask baseline to {path}");
            }
            else
            {
                UnityEditor.EditorUtility.SetDirty(maskBaseline);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(maskBaseline);
            }
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        void EnsureBaseline()
        {
            if (baseline != null && baseline.width == res && baseline.height == res) return;
            if (formationsApplied)
            {
                Debug.LogError("[FormationBuilder] The baseline is missing or does not match the heightmap while formations are applied — refusing to auto-capture, it would bake the formations in permanently. Use Capture Baseline deliberately if the current terrain is the intended ground.");
                return;
            }
            CaptureBaseline();
        }

        public void CaptureBaseline()
        {
            if (data == null) return;
            res = data.heightmapResolution;

            var heights = data.GetHeights(0, 0, res, res);
            var flat = new float[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    flat[y * res + x] = heights[y, x];

            bool fresh = baseline == null || baseline.width != res || baseline.height != res;
            if (fresh)
            {
                baseline = new Texture2D(res, res, TextureFormat.RFloat, false, true);
                baseline.name = (terrain != null ? terrain.name : "Terrain") + "_FormationBaseline";
            }
#if UNITY_EDITOR
            else
            {
                BackupBaseline("before capture");
                UnityEditor.Undo.RegisterCompleteObjectUndo(baseline, "Capture Formation Baseline");
            }
#endif
            baseline.SetPixelData(flat, 0);
            baseline.Apply(false, false);

#if UNITY_EDITOR
            if (fresh)
            {
                string dir = FindBestSaveDir();
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(dir + "/" + baseline.name + ".asset");
                UnityEditor.AssetDatabase.CreateAsset(baseline, path);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[FormationBuilder] Captured baseline to {path}");
            }
            else
            {
                UnityEditor.EditorUtility.SetDirty(baseline);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(baseline);
            }
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            if (maskBaseline != null || AnyRock())
            {
                var mask = AcquireMask(false);
                if (mask != null) CaptureMaskBaseline(mask);
            }
            maskPainted = false;
            paintedPx0 = paintedPy0 = paintedPx1 = paintedPy1 = -1;
            formationsApplied = false;
            hasLastHeightRegion = false;
            lastWritten = null;
            hasExternalDirty = false;
            baselineVersion++;
            InvalidatePreviews();
        }

        public void RestoreBaseline()
        {
            if (baseline == null || data == null) return;
            res = data.heightmapResolution;
            if (baseline.width != res || baseline.height != res)
            {
                Debug.LogError("[FormationBuilder] Baseline resolution does not match the heightmap — recapture the baseline.");
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Restore Formation Baseline");
#endif
            var flat = baseline.GetPixelData<float>(0);
            var heights = new float[res, res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    heights[y, x] = flat[y * res + x];
            selfWrite = true;
            try { data.SetHeights(0, 0, heights); }
            finally { selfWrite = false; }

            var mask = AcquireMask(false);
            if (mask != null && maskBaseline != null
                && maskBaseline.width == mask.width && maskBaseline.height == mask.height)
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCompleteObjectUndo(mask, "Restore Formation Baseline");
#endif
                var basePixels = maskBaseline.GetPixels32();
                var pixels = mask.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i].r = basePixels[i].r;
                mask.SetPixels32(pixels);
                mask.Apply(mask.mipmapCount > 1, false);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(mask);
#endif
            }

            maskPainted = false;
            paintedPx0 = paintedPy0 = paintedPx1 = paintedPy1 = -1;
            formationsApplied = false;
            hasLastHeightRegion = false;
            lastWritten = null;
            hasExternalDirty = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        void InvalidatePreviews()
        {
            if (formations == null) return;
            foreach (var f in formations)
                if (f != null) f.preview = null;
        }

        public int ContentHash()
        {
            unchecked
            {
                int h = 19;
                if (formations != null && data != null)
                    foreach (var f in formations)
                        if (f != null)
                            h = h * 31 + FormationArea.ComputeHash(f, transform.position, data.size, res);
                return h;
            }
        }

        bool NeedsApply(bool simulate)
        {
            if (!hasAppliedHash) return true;
            return ContentHash() != appliedHash || (simulate && !appliedSimulated);
        }

#if UNITY_EDITOR
        [System.NonSerialized] bool applyQueued;
        [System.NonSerialized] bool pendingSimulate = true;

        public void ScheduleApply(bool simulate = true)
        {
            if (applyQueued)
            {
                pendingSimulate = pendingSimulate || simulate;
                return;
            }
            if (!NeedsApply(simulate)) return;
            pendingSimulate = simulate;
            applyQueued = true;
            UnityEditor.EditorApplication.delayCall += ProcessScheduledApply;
        }

        void ProcessScheduledApply()
        {
            if (this == null || !isActiveAndEnabled) { applyQueued = false; return; }
            if (GUIUtility.hotControl != 0)
            {
                if (NeedsApply(false)) Apply(false, false, interactive: true);
                UnityEditor.EditorApplication.delayCall += ProcessScheduledApply;
                return;
            }
            applyQueued = false;
            if (NeedsApply(pendingSimulate) || appliedInteractive) Apply(false, pendingSimulate);
            pendingSimulate = true;
        }

        string FindBestSaveDir()
        {
            if (data != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(data);
                if (!string.IsNullOrEmpty(path)) return System.IO.Path.GetDirectoryName(path);
            }
            var scene = gameObject.scene;
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                return System.IO.Path.GetDirectoryName(scene.path);
            return "Assets";
        }

        static string BackupDir => System.IO.Path.Combine("Library", "JibbersMapToolsBackups");
        string BackupPrefix => (terrain != null ? terrain.name : "Terrain") + "_FormationBaseline_";
        const int BackupMagic = 0x4A464231;
        const int BackupKeep = 8;

        void BackupBaseline(string reason)
        {
            if (baseline == null) return;
            try
            {
                System.IO.Directory.CreateDirectory(BackupDir);
                string path = System.IO.Path.Combine(BackupDir,
                    BackupPrefix + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".bin");
                using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
                using (var w = new System.IO.BinaryWriter(fs))
                {
                    w.Write(BackupMagic);
                    w.Write(baseline.width);
                    var flat = baseline.GetPixelData<float>(0);
                    var bytes = new byte[flat.Length * 4];
                    System.Buffer.BlockCopy(flat.ToArray(), 0, bytes, 0, bytes.Length);
                    w.Write(bytes);
                }
                var files = System.IO.Directory.GetFiles(BackupDir, BackupPrefix + "*.bin");
                System.Array.Sort(files);
                for (int i = 0; i < files.Length - BackupKeep; i++)
                    System.IO.File.Delete(files[i]);
                Debug.Log($"[FormationBuilder] Baseline backup ({reason}): {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[FormationBuilder] Baseline backup failed: " + e.Message);
            }
        }

        string LatestBackupPath()
        {
            if (!System.IO.Directory.Exists(BackupDir)) return null;
            var files = System.IO.Directory.GetFiles(BackupDir, BackupPrefix + "*.bin");
            if (files.Length == 0) return null;
            System.Array.Sort(files);
            return files[files.Length - 1];
        }

        public bool HasBaselineBackup() => LatestBackupPath() != null;

        public string LatestBackupDescription()
        {
            string path = LatestBackupPath();
            return path == null ? "none" : System.IO.File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm");
        }

        public void RestoreBaselineFromBackup()
        {
            string path = LatestBackupPath();
            if (path == null || data == null) return;
            if (baseline == null || baseline.width != res || baseline.height != res)
            {
                Debug.LogError("[FormationBuilder] No matching baseline texture to restore into — the backup can only replace an existing baseline of the same resolution.");
                return;
            }
            try
            {
                using (var fs = System.IO.File.OpenRead(path))
                using (var r = new System.IO.BinaryReader(fs))
                {
                    if (r.ReadInt32() != BackupMagic)
                    {
                        Debug.LogError("[FormationBuilder] Backup file is invalid.");
                        return;
                    }
                    int bres = r.ReadInt32();
                    if (bres != res)
                    {
                        Debug.LogError($"[FormationBuilder] Backup resolution {bres} does not match the heightmap {res}.");
                        return;
                    }
                    var floats = new float[bres * bres];
                    var bytes = r.ReadBytes(floats.Length * 4);
                    if (bytes.Length != floats.Length * 4)
                    {
                        Debug.LogError("[FormationBuilder] Backup file is truncated.");
                        return;
                    }
                    System.Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
                    UnityEditor.Undo.RegisterCompleteObjectUndo(baseline, "Restore Baseline From Backup");
                    baseline.SetPixelData(floats, 0);
                    baseline.Apply(false, false);
                    UnityEditor.EditorUtility.SetDirty(baseline);
                }
                lastWritten = null;
                ClearExternalDirty();
                baselineVersion++;
                Debug.Log($"[FormationBuilder] Baseline restored from backup: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[FormationBuilder] Failed to restore the baseline backup: " + e.Message);
            }
        }

        public bool BakeFormation(Formation f)
        {
            if (f == null || terrain == null || data == null || formationShader == null || !isActiveAndEnabled) return false;
            int index = formations != null ? formations.IndexOf(f) : -1;
            if (index < 0) return false;
            if (!f.enabled || f.area == null || f.area.Count < 3)
            {
                Debug.LogError("[FormationBuilder] Only enabled formations with a marked area (3+ points) can be baked.");
                return false;
            }
            if (data.heightmapResolution != res) { res = data.heightmapResolution; CreateHeightRTs(); }
            if (heightRT == null || !heightRT.IsCreated() || heightRT.width != res) CreateHeightRTs();
            EnsureBaseline();
            if (baseline == null || baseline.width != res || baseline.height != res) return false;

            var rect = ClampToRes(CurrentRegionsRect());
            if (rect.width <= 0 || rect.height <= 0) return false;

            var batchAll = RenderComposite(-1);
            var hAll = SyncReadRegion(rect);
            ReleaseBatch(batchAll);
            if (hAll == null) return false;

            var batchWithout = RenderComposite(index);
            var hWithout = SyncReadRegion(rect);
            ReleaseBatch(batchWithout);
            if (hWithout == null) return false;

            BackupBaseline($"before baking '{f.name}'");
            UnityEditor.Undo.RegisterCompleteObjectUndo(baseline, "Bake Formation");
            UnityEditor.Undo.RecordObject(this, "Bake Formation");

            var flat = baseline.GetPixelData<float>(0);
            for (int y = 0; y < rect.height; y++)
                for (int x = 0; x < rect.width; x++)
                {
                    int gi = (rect.yMin + y) * res + rect.xMin + x;
                    int ri = y * rect.width + x;
                    flat[gi] = Mathf.Clamp01(flat[gi] + (hAll[ri] - hWithout[ri]));
                }
            baseline.Apply(false, false);
            UnityEditor.EditorUtility.SetDirty(baseline);

            BakeRockIntoMaskBaseline(f);

            formations.RemoveAt(index);
            baselineVersion++;
            InvalidatePreviews();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[FormationBuilder] Baked '{f.name}' into the terrain baseline (backup written). {formations.Count} formation(s) remain live.");
            ScheduleApply(true);
            return true;
        }

        List<ComputeBuffer> RenderComposite(int skipIndex)
        {
            Graphics.Blit(baseline, heightRT);
            int clearGroups = Mathf.Max(1, Mathf.CeilToInt(res / 8f));
            formationShader.SetInt("res", res);
            formationShader.SetTexture(kClearRock, "RockMask", rockRT);
            formationShader.Dispatch(kClearRock, clearGroups, clearGroups, 1);

            int maxW = 0, maxH = 0;
            var regions = new List<Region>();
            for (int i = 0; i < formations.Count; i++)
            {
                var f = formations[i];
                if (f == null || !f.enabled || f.area == null || f.area.Count < 3 || i == skipIndex)
                {
                    regions.Add(default(Region));
                    continue;
                }
                var r = ComputeRegion(f);
                regions.Add(r);
                if (r.valid) { maxW = Mathf.Max(maxW, r.w); maxH = Mathf.Max(maxH, r.h); }
            }
            var batch = new List<ComputeBuffer>();
            if (maxW <= 0) return batch;
            EnsureSimRTs(maxW, maxH);
            for (int i = 0; i < formations.Count; i++)
                if (regions[i].valid)
                    RunFormation(formations[i], regions[i], true, batch);
            return batch;
        }

        float[] SyncReadRegion(RectInt rect)
        {
            var req = AsyncGPUReadback.Request(heightRT, 0, rect.xMin, rect.width, rect.yMin, rect.height, 0, 1);
            req.WaitForCompletion();
            if (req.hasError)
            {
                Debug.LogError("[FormationBuilder] GPU readback failed during bake.");
                return null;
            }
            var raw = req.GetData<float>();
            if (raw.Length < rect.width * rect.height) return null;
            var arr = new float[rect.width * rect.height];
            for (int i = 0; i < arr.Length; i++) arr[i] = raw[i];
            return arr;
        }

        void ReleaseBatch(List<ComputeBuffer> batch)
        {
            foreach (var buffer in batch)
                if (buffer != null && liveBuffers.Remove(buffer))
                    buffer.Release();
        }

        void BakeRockIntoMaskBaseline(Formation f)
        {
            if (!f.snowEnabled || f.rockStrength <= 0f || !maskPainted) return;
            var mask = AcquireMask(false);
            if (mask == null || maskBaseline == null
                || maskBaseline.width != mask.width || maskBaseline.height != mask.height) return;
            if (!FormationArea.Bounds(f.area, out Vector2 min, out Vector2 max)) return;
            int mw = mask.width, mh = mask.height;
            int a0 = Mathf.Clamp(Mathf.FloorToInt(min.x / data.size.x * mw) - 2, 0, mw - 1);
            int a1 = Mathf.Clamp(Mathf.CeilToInt(max.x / data.size.x * mw) + 2, 0, mw - 1);
            int b0 = Mathf.Clamp(Mathf.FloorToInt((1f - max.y / data.size.z) * mh) - 2, 0, mh - 1);
            int b1 = Mathf.Clamp(Mathf.CeilToInt((1f - min.y / data.size.z) * mh) + 2, 0, mh - 1);
            if (a1 < a0 || b1 < b0) return;

            UnityEditor.Undo.RegisterCompleteObjectUndo(maskBaseline, "Bake Formation");
            var basePixels = maskBaseline.GetPixels32();
            var cur = mask.GetPixels32();
            for (int py = b0; py <= b1; py++)
                for (int px = a0; px <= a1; px++)
                {
                    int i = py * mw + px;
                    basePixels[i].r = cur[i].r;
                }
            maskBaseline.SetPixels32(basePixels);
            maskBaseline.Apply(false, false);
            UnityEditor.EditorUtility.SetDirty(maskBaseline);
        }
#endif
    }

}
