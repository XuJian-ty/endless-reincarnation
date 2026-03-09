using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    public class SharedSkillTimelineEditorWindow : EditorWindow
    {
        private const float LeftPanelWidth = 220f;
        private const float RightPanelWidth = 340f;
        private const float ToolbarHeight = 24f;
        private const float PreviewBarHeight = 28f;
        private const float ButtonBarHeight = 28f;
        private const float RulerHeight = 22f;
        private const float EventRowHeight = 28f;
        private const float ScrollbarHeight = 16f;
        private const float MinEventWidth = 10f;
        private const float ResizeHandleWidth = 6f;
        private const float PlayheadHitWidth = 6f;

        private static readonly Color TimelineBackground = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color RulerBackground = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color GridColor = new Color(0.34f, 0.34f, 0.34f, 1f);
        private static readonly Color DamageColor = new Color(0.90f, 0.28f, 0.28f, 0.88f);
        private static readonly Color PhysicsColor = new Color(0.28f, 0.50f, 0.92f, 0.88f);
        private static readonly Color AttributeColor = new Color(0.28f, 0.78f, 0.38f, 0.88f);
        private static readonly Color CueColor = new Color(0.92f, 0.80f, 0.18f, 0.88f);
        private static readonly Color DefaultColor = new Color(0.55f, 0.55f, 0.55f, 0.88f);
        private static readonly Color SelectedOverlay = new Color(1f, 1f, 1f, 0.30f);
        private static readonly Color PlayheadColor = new Color(1f, 0.25f, 0.25f, 1f);

        private SharedSkillDatabaseSO _database;
        private SerializedObject _serializedDb;

        private int _selectedSkillIndex = -1;
        private int _selectedEventIndex = -1;

        private Vector2 _skillScroll;
        private Vector2 _propertyScroll;
        private float _timelineScrollX;
        private float _zoom = 100f;

        private GameObject _previewTarget;
        private AnimationClip _previewClip;
        private float _previewTime;
        private bool _isPlaying;
        private double _prevEditorTime;

        private bool _isDraggingPlayhead;
        private bool _isDraggingEvent;
        private bool _isResizingEvent;
        private int _dragEventIndex = -1;
        private float _dragStartMouseX;
        private float _dragStartTime;
        private float _dragStartDuration;

        [MenuItem("游戏/技能时间轴编辑器")]
        public static void Open()
        {
            var window = GetWindow<SharedSkillTimelineEditorWindow>("技能时间轴编辑器");
            window.minSize = new Vector2(980f, 560f);
            window.TryAutoLoadDatabase();
        }

        private void OnEnable()
        {
            TryAutoLoadDatabase();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            StopPreview();
        }

        private void OnDestroy()
        {
            StopPreview();
        }

        private void OnEditorUpdate()
        {
            if (!_isPlaying)
                return;

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - _prevEditorTime);
            _prevEditorTime = now;

            _previewTime += deltaTime;
            float totalDuration = GetPreviewTotalDuration();
            if (_previewTime >= totalDuration)
            {
                _previewTime = totalDuration;
                _isPlaying = false;
            }

            SampleAnimationAtTime(_previewTime);
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnGUI()
        {
            EnsureSerializedDatabase();

            float totalWidth = position.width;
            float totalHeight = position.height;
            float centerWidth = Mathf.Max(200f, totalWidth - LeftPanelWidth - RightPanelWidth);

            DrawToolbar(new Rect(0f, 0f, totalWidth, ToolbarHeight));
            DrawPreviewBar(new Rect(0f, ToolbarHeight, totalWidth, PreviewBarHeight));

            float contentY = ToolbarHeight + PreviewBarHeight;
            float contentHeight = totalHeight - contentY;

            DrawLeftPanel(new Rect(0f, contentY, LeftPanelWidth, contentHeight));
            DrawTimelinePanel(new Rect(LeftPanelWidth, contentY, centerWidth, contentHeight));
            DrawRightPanel(new Rect(totalWidth - RightPanelWidth, contentY, RightPanelWidth, contentHeight));

            if (_serializedDb != null)
                _serializedDb.ApplyModifiedProperties();
        }

        private void DrawToolbar(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            GUILayout.BeginArea(rect);
            try
            {
                GUILayout.BeginHorizontal(EditorStyles.toolbar);
                try
                {
                    EditorGUI.BeginChangeCheck();
                    var newDb = (SharedSkillDatabaseSO)EditorGUILayout.ObjectField(_database, typeof(SharedSkillDatabaseSO), false, GUILayout.Width(220f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _database = newDb;
                        RebuildSerializedDb();
                        _selectedSkillIndex = -1;
                        _selectedEventIndex = -1;
                        _timelineScrollX = 0f;
                        StopPreview();
                    }

                    GUILayout.Space(8f);
                    if (GUILayout.Button("保存资产", EditorStyles.toolbarButton, GUILayout.Width(64f)) && _database != null)
                    {
                        EditorUtility.SetDirty(_database);
                        AssetDatabase.SaveAssets();
                    }

                    GUILayout.Space(8f);
                    GUILayout.Label("缩放", EditorStyles.miniLabel, GUILayout.Width(28f));
                    _zoom = GUILayout.HorizontalSlider(_zoom, 20f, 800f, GUILayout.Width(120f));
                    GUILayout.Label($"{_zoom:F0}px/s", EditorStyles.miniLabel, GUILayout.Width(56f));

                    if (GUILayout.Button("适应宽度", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                        FitZoomToWindow();

                    GUILayout.FlexibleSpace();
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawPreviewBar(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            GUILayout.BeginArea(rect);
            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Space(4f);
                    GUILayout.Label("预览", EditorStyles.miniLabel, GUILayout.Width(28f));

                    EditorGUI.BeginChangeCheck();
                    _previewTarget = (GameObject)EditorGUILayout.ObjectField(_previewTarget, typeof(GameObject), true, GUILayout.Width(150f));
                    if (EditorGUI.EndChangeCheck() && !_isPlaying)
                        SampleAnimationAtTime(_previewTime);

                    EditorGUI.BeginChangeCheck();
                    _previewClip = (AnimationClip)EditorGUILayout.ObjectField(_previewClip, typeof(AnimationClip), false, GUILayout.Width(150f));
                    if (EditorGUI.EndChangeCheck() && !_isPlaying)
                        SampleAnimationAtTime(_previewTime);

                    GUILayout.Space(8f);

                    if (GUILayout.Button("|<", EditorStyles.miniButtonLeft, GUILayout.Width(24f)))
                    {
                        StopPreview();
                        _previewTime = 0f;
                        SampleAnimationAtTime(_previewTime);
                    }

                    if (GUILayout.Button(_isPlaying ? "||" : ">", EditorStyles.miniButtonMid, GUILayout.Width(24f)))
                    {
                        if (_isPlaying)
                        {
                            _isPlaying = false;
                        }
                        else
                        {
                            StartPreviewPlayback();
                        }
                    }

                    if (GUILayout.Button("[]", EditorStyles.miniButtonRight, GUILayout.Width(24f)))
                    {
                        StopPreview();
                        _previewTime = 0f;
                        SampleAnimationAtTime(_previewTime);
                    }

                    GUILayout.Space(8f);
                    GUILayout.Label($"{_previewTime:F2}s / {GetPreviewTotalDuration():F2}s", EditorStyles.miniLabel, GUILayout.Width(110f));

                    if (_isPlaying)
                        GUILayout.Label("播放中", EditorStyles.miniLabel, GUILayout.Width(40f));
                    else if (AnimationMode.InAnimationMode())
                        GUILayout.Label("预览中", EditorStyles.miniLabel, GUILayout.Width(40f));

                    GUILayout.FlexibleSpace();
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }
        private void DrawLeftPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.24f, 0.24f, 0.24f, 1f));

            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 22f);
            EditorGUI.DrawRect(titleRect, new Color(0.20f, 0.20f, 0.20f, 1f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), "技能列表", EditorStyles.boldLabel);

            Rect addRect = new Rect(rect.x, rect.y + 22f, rect.width * 0.5f, 22f);
            Rect removeRect = new Rect(rect.x + rect.width * 0.5f, rect.y + 22f, rect.width * 0.5f, 22f);

            bool hasDatabase = _database != null;
            if (GUI.Button(addRect, "添加", EditorStyles.miniButtonLeft) && hasDatabase)
                AddSkill();

            EditorGUI.BeginDisabledGroup(!hasDatabase || !HasSelectedSkill());
            try
            {
                if (GUI.Button(removeRect, "删除", EditorStyles.miniButtonRight))
                    RemoveSelectedSkill();
            }
            finally
            {
                EditorGUI.EndDisabledGroup();
            }

            Rect scrollRect = new Rect(rect.x, rect.y + 44f, rect.width, rect.height - 44f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, hasDatabase ? Mathf.Max(24f, _database.entries.Count * 24f + 4f) : 24f);

            _skillScroll = GUI.BeginScrollView(scrollRect, _skillScroll, viewRect);
            try
            {
                if (!hasDatabase)
                {
                    GUI.Label(new Rect(4f, 4f, rect.width - 24f, 18f), "未选择共享技能库", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    EnsureEntryList();
                    for (int i = 0; i < _database.entries.Count; i++)
                    {
                        Rect rowRect = new Rect(0f, i * 24f + 2f, viewRect.width, 22f);
                        bool selected = i == _selectedSkillIndex;
                        if (selected)
                            EditorGUI.DrawRect(rowRect, new Color(0.26f, 0.47f, 0.76f, 0.8f));

                        var entry = _database.entries[i];
                        string label = entry == null || string.IsNullOrEmpty(entry.skillId)
                            ? $"[{i}] 未命名技能"
                            : $"[{i}] {entry.skillId}";
                        if (GUI.Button(rowRect, label, EditorStyles.label))
                        {
                            _selectedSkillIndex = i;
                            _selectedEventIndex = -1;
                            _timelineScrollX = 0f;
                            StopPreview();
                        }
                    }
                }
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private void DrawTimelinePanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, TimelineBackground);

            SharedSkillDefinition skill = GetSelectedSkill();
            Rect buttonBarRect = new Rect(rect.x, rect.y, rect.width, ButtonBarHeight);
            DrawTimelineButtonBar(buttonBarRect, skill);

            float timelineY = rect.y + ButtonBarHeight;

            if (skill == null)
            {
                GUI.Label(new Rect(rect.x + 8f, timelineY + 8f, rect.width - 16f, 18f), "请先在左侧选择一个技能", EditorStyles.centeredGreyMiniLabel);
                GUI.Label(new Rect(rect.x + 8f, timelineY + 28f, rect.width - 16f, 18f), "再在中间时间轴上添加和编辑事件", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var rows = AssignEventRows(skill.events);
            int rowCount = Mathf.Max(1, rows.Count);
            float eventsHeight = rowCount * EventRowHeight;

            Rect rulerRect = new Rect(rect.x, timelineY, rect.width, RulerHeight);
            Rect eventsRect = new Rect(rect.x, timelineY + RulerHeight, rect.width, eventsHeight);
            Rect scrollbarRect = new Rect(rect.x, eventsRect.yMax + 2f, rect.width, ScrollbarHeight);

            DrawRuler(rulerRect);
            HandlePlayheadDragInRuler(rulerRect);
            DrawPlayheadLine(rulerRect);

            EditorGUI.DrawRect(eventsRect, TimelineBackground);
            DrawGridLines(eventsRect);
            HandleTimelineMouse(Event.current, eventsRect, skill, rows);
            DrawEventBlocks(eventsRect, skill, rows);
            DrawPlayheadLine(eventsRect);

            float contentWidth = GetTimelineContentWidth(skill);
            float maxScroll = Mathf.Max(0f, contentWidth - rect.width);
            _timelineScrollX = GUI.HorizontalScrollbar(scrollbarRect, _timelineScrollX, rect.width, 0f, contentWidth);
            _timelineScrollX = Mathf.Clamp(_timelineScrollX, 0f, maxScroll);

            if (Event.current.type == EventType.ScrollWheel && rect.Contains(Event.current.mousePosition))
            {
                float pivotTime = XToTime(Event.current.mousePosition.x, rect.x);
                _zoom = Mathf.Clamp(_zoom - Event.current.delta.y * 5f, 20f, 800f);
                _timelineScrollX = Mathf.Max(0f, pivotTime * _zoom - (Event.current.mousePosition.x - rect.x));
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawTimelineButtonBar(Rect rect, SharedSkillDefinition skill)
        {
            EditorGUI.DrawRect(rect, new Color(0.20f, 0.20f, 0.20f, 1f));
            GUILayout.BeginArea(rect);
            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    bool hasSkill = skill != null;
                    bool hasEvent = hasSkill && HasSelectedEvent();

                    EditorGUI.BeginDisabledGroup(!hasSkill);
                    try
                    {
                        if (GUILayout.Button("添加事件", EditorStyles.miniButtonLeft, GUILayout.Width(80f)))
                            AddEvent(skill);
                    }
                    finally
                    {
                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUI.BeginDisabledGroup(!hasEvent);
                    try
                    {
                        if (GUILayout.Button("删除事件", EditorStyles.miniButtonMid, GUILayout.Width(80f)))
                            RemoveSelectedEvent(skill);
                        if (GUILayout.Button("按时间排序", EditorStyles.miniButtonRight, GUILayout.Width(80f)))
                            SortEvents(skill);
                    }
                    finally
                    {
                        EditorGUI.EndDisabledGroup();
                    }

                    GUILayout.FlexibleSpace();
                    if (hasSkill)
                    {
                        string label = string.IsNullOrEmpty(skill.displayName)
                            ? skill.skillId
                            : $"{skill.skillId} ({skill.displayName})";
                        GUILayout.Label(label, EditorStyles.miniLabel);
                    }

                    GUILayout.Space(4f);
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawRuler(Rect rect)
        {
            EditorGUI.DrawRect(rect, RulerBackground);

            float startTime = _timelineScrollX / _zoom;
            float endTime = (_timelineScrollX + rect.width) / _zoom;
            float minorStep = ChooseTickInterval(_zoom);
            float majorStep = minorStep * 5f;

            for (float time = Mathf.Floor(startTime / minorStep) * minorStep; time <= endTime + minorStep; time += minorStep)
            {
                float x = rect.x + TimeToX(time);
                bool isMajor = Mathf.Abs(Mathf.Repeat(time, majorStep)) < minorStep * 0.5f;
                float lineHeight = isMajor ? rect.height * 0.55f : rect.height * 0.30f;

                EditorGUI.DrawRect(new Rect(x, rect.yMax - lineHeight, 1f, lineHeight), Color.gray);
                if (isMajor && x >= rect.x && x <= rect.xMax)
                    GUI.Label(new Rect(x + 2f, rect.y + 2f, 40f, 14f), $"{time:F2}s", EditorStyles.miniLabel);
            }
        }

        private void DrawGridLines(Rect rect)
        {
            float startTime = _timelineScrollX / _zoom;
            float endTime = (_timelineScrollX + rect.width) / _zoom;
            float step = ChooseTickInterval(_zoom) * 5f;

            for (float time = Mathf.Floor(startTime / step) * step; time <= endTime + step; time += step)
            {
                float x = rect.x + TimeToX(time);
                if (x >= rect.x && x <= rect.xMax)
                    EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), GridColor);
            }
        }
        private void DrawEventBlocks(Rect eventsRect, SharedSkillDefinition skill, List<List<int>> rows)
        {
            if (skill == null || skill.events == null)
                return;

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                float rowY = eventsRect.y + rowIndex * EventRowHeight;
                foreach (int eventIndex in rows[rowIndex])
                {
                    if (eventIndex < 0 || eventIndex >= skill.events.Count)
                        continue;

                    SkillTimelineEvent evt = skill.events[eventIndex];
                    if (evt == null)
                        continue;

                    float x = eventsRect.x + TimeToX(evt.startTime);
                    float width = GetEventDisplayWidth(evt);
                    Rect blockRect = new Rect(x, rowY + 2f, width, EventRowHeight - 4f);
                    if (blockRect.xMax < eventsRect.x || blockRect.x > eventsRect.xMax)
                        continue;

                    EditorGUI.DrawRect(blockRect, GetEventColor(evt));
                    if (eventIndex == _selectedEventIndex)
                        EditorGUI.DrawRect(blockRect, SelectedOverlay);

                    if (blockRect.width > 18f)
                    {
                        GUI.Label(new Rect(blockRect.x + 4f, blockRect.y + 1f, blockRect.width - 8f, blockRect.height - 2f), GetEventLabel(evt), EditorStyles.miniLabel);
                    }

                    if (evt.triggerMode == SkillEventTriggerMode.Repeated && blockRect.width > ResizeHandleWidth * 2f)
                    {
                        Rect handleRect = new Rect(blockRect.xMax - ResizeHandleWidth, blockRect.y, ResizeHandleWidth, blockRect.height);
                        EditorGUI.DrawRect(handleRect, new Color(1f, 1f, 1f, 0.25f));
                        EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);
                    }
                }
            }
        }

        private void HandleTimelineMouse(Event evt, Rect eventsRect, SharedSkillDefinition skill, List<List<int>> rows)
        {
            if (skill == null || skill.events == null)
                return;

            if (evt.type == EventType.MouseDown && evt.button == 0 && eventsRect.Contains(evt.mousePosition))
            {
                bool hitEdge;
                int hitIndex = HitTestEvent(evt.mousePosition, eventsRect, skill, rows, out hitEdge);
                if (hitIndex >= 0)
                {
                    _selectedEventIndex = hitIndex;
                    _dragEventIndex = hitIndex;
                    _dragStartMouseX = evt.mousePosition.x;
                    _dragStartTime = skill.events[hitIndex].startTime;
                    _dragStartDuration = skill.events[hitIndex].activeDuration;
                    _isDraggingEvent = true;
                    _isResizingEvent = hitEdge;
                    GUI.FocusControl(null);
                    evt.Use();
                    Repaint();
                }
                else
                {
                    _selectedEventIndex = -1;
                    Repaint();
                }
            }
            else if (evt.type == EventType.MouseDrag && _isDraggingEvent && evt.button == 0)
            {
                if (_dragEventIndex < 0 || _dragEventIndex >= skill.events.Count)
                    return;

                SkillTimelineEvent draggedEvent = skill.events[_dragEventIndex];
                if (draggedEvent == null)
                    return;

                float deltaTime = (evt.mousePosition.x - _dragStartMouseX) / _zoom;
                Undo.RecordObject(_database, _isResizingEvent ? "Resize Skill Event" : "Move Skill Event");
                if (_isResizingEvent)
                    draggedEvent.activeDuration = Mathf.Max(0.01f, _dragStartDuration + deltaTime);
                else
                    draggedEvent.startTime = Mathf.Max(0f, _dragStartTime + deltaTime);

                EditorUtility.SetDirty(_database);
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseUp && _isDraggingEvent && evt.button == 0)
            {
                _isDraggingEvent = false;
                _isResizingEvent = false;
                _dragEventIndex = -1;
                evt.Use();
                Repaint();
            }
        }

        private int HitTestEvent(Vector2 mousePosition, Rect eventsRect, SharedSkillDefinition skill, List<List<int>> rows, out bool hitEdge)
        {
            hitEdge = false;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                float rowY = eventsRect.y + rowIndex * EventRowHeight;
                foreach (int eventIndex in rows[rowIndex])
                {
                    if (eventIndex < 0 || eventIndex >= skill.events.Count)
                        continue;

                    SkillTimelineEvent evt = skill.events[eventIndex];
                    if (evt == null)
                        continue;

                    float x = eventsRect.x + TimeToX(evt.startTime);
                    float width = GetEventDisplayWidth(evt);
                    Rect blockRect = new Rect(x, rowY + 2f, width, EventRowHeight - 4f);
                    if (!blockRect.Contains(mousePosition))
                        continue;

                    if (evt.triggerMode == SkillEventTriggerMode.Repeated && width > ResizeHandleWidth * 2f)
                    {
                        Rect handleRect = new Rect(blockRect.xMax - ResizeHandleWidth, blockRect.y, ResizeHandleWidth, blockRect.height);
                        if (handleRect.Contains(mousePosition))
                        {
                            hitEdge = true;
                            return eventIndex;
                        }
                    }

                    return eventIndex;
                }
            }

            return -1;
        }

        private void DrawPlayheadLine(Rect rect)
        {
            float x = rect.x + TimeToX(_previewTime);
            if (x < rect.x || x > rect.xMax)
                return;

            EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), PlayheadColor);
        }

        private void HandlePlayheadDragInRuler(Rect rulerRect)
        {
            Event evt = Event.current;
            float playheadX = rulerRect.x + TimeToX(_previewTime);
            bool nearPlayhead = Mathf.Abs(evt.mousePosition.x - playheadX) <= PlayheadHitWidth;

            if (evt.type == EventType.MouseDown && evt.button == 0 && rulerRect.Contains(evt.mousePosition))
            {
                _isDraggingPlayhead = true;
                SetPlayheadFromMouseX(evt.mousePosition.x, rulerRect.x);
                evt.Use();
                Repaint();
            }
            else if (_isDraggingPlayhead && evt.type == EventType.MouseDrag && evt.button == 0)
            {
                SetPlayheadFromMouseX(evt.mousePosition.x, rulerRect.x);
                evt.Use();
                Repaint();
            }
            else if (_isDraggingPlayhead && evt.type == EventType.MouseUp && evt.button == 0)
            {
                _isDraggingPlayhead = false;
                evt.Use();
            }
            else if (nearPlayhead && rulerRect.Contains(evt.mousePosition))
            {
                EditorGUIUtility.AddCursorRect(rulerRect, MouseCursor.ResizeHorizontal);
            }
        }

        private void SetPlayheadFromMouseX(float mouseX, float panelX)
        {
            _previewTime = Mathf.Max(0f, XToTime(mouseX, panelX));
            if (!_isPlaying)
            {
                StartAnimationMode();
                SampleAnimationAtTime(_previewTime);
                SceneView.RepaintAll();
            }
        }

        private void DrawRightPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.22f, 1f));
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 22f);
            EditorGUI.DrawRect(titleRect, new Color(0.18f, 0.18f, 0.18f, 1f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), "属性", EditorStyles.boldLabel);

            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
            {
                GUI.Label(new Rect(rect.x + 6f, rect.y + 28f, rect.width - 12f, 18f), "请选择一个技能", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            GUILayout.BeginArea(new Rect(rect.x, rect.y + 22f, rect.width, rect.height - 22f));
            try
            {
                _propertyScroll = EditorGUILayout.BeginScrollView(_propertyScroll);
                try
                {
                    DrawSkillInspector(skill);

                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("事件概览", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("事件数量", skill.events != null ? skill.events.Count.ToString() : "0");

                    if (HasSelectedEvent())
                    {
                        DrawEventInspector(skill.events[_selectedEventIndex]);
                    }
                    else
                    {
                        EditorGUILayout.Space(8f);
                        EditorGUILayout.HelpBox("在时间轴上选择一个事件后，这里会显示事件属性。", MessageType.Info);
                    }
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawSkillInspector(SharedSkillDefinition skill)
        {
            EditorGUILayout.LabelField("技能", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            string newSkillId = EditorGUILayout.TextField("技能 ID", skill.skillId ?? string.Empty);
            string newDisplayName = EditorGUILayout.TextField("显示名称", skill.displayName ?? string.Empty);
            bool newIgnoreAnimationDamageEvents = EditorGUILayout.Toggle("忽略动画帧伤害事件", skill.ignoreAnimationDamageEvents);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(_database, "Edit Skill");
            skill.skillId = newSkillId;
            skill.displayName = newDisplayName;
            skill.ignoreAnimationDamageEvents = newIgnoreAnimationDamageEvents;
            MarkDatabaseDirty();
        }

        private void DrawEventInspector(SkillTimelineEvent evt)
        {
            if (evt == null)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("选中事件", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            string newEventId = EditorGUILayout.TextField("事件标识 ID", evt.eventId ?? string.Empty);
            float newStartTime = Mathf.Max(0f, EditorGUILayout.FloatField("触发时间(秒)", evt.startTime));
            SkillEventTriggerMode newTriggerMode = (SkillEventTriggerMode)EditorGUILayout.EnumPopup("触发模式", evt.triggerMode);
            float newActiveDuration = evt.activeDuration;
            float newRepeatInterval = evt.repeatInterval;
            if (newTriggerMode == SkillEventTriggerMode.Repeated)
            {
                newActiveDuration = Mathf.Max(0f, EditorGUILayout.FloatField("持续触发时长(秒)", evt.activeDuration));
                newRepeatInterval = Mathf.Max(0.01f, EditorGUILayout.FloatField("重复触发间隔(秒)", evt.repeatInterval));
            }

            string newHitDetectionName = EditorGUILayout.TextField("伤害检测名", evt.hitDetectionName ?? string.Empty);
            float newDamageMagnitude = Mathf.Max(0f, EditorGUILayout.FloatField("伤害倍率", evt.damageMagnitude));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_database, "Edit Skill Event");
                evt.eventId = newEventId;
                evt.startTime = newStartTime;
                evt.triggerMode = newTriggerMode;
                evt.activeDuration = newTriggerMode == SkillEventTriggerMode.Repeated ? newActiveDuration : 0f;
                evt.repeatInterval = newTriggerMode == SkillEventTriggerMode.Repeated ? newRepeatInterval : 0.1f;
                evt.hitDetectionName = newHitDetectionName;
                evt.damageMagnitude = newDamageMagnitude;
                MarkDatabaseDirty();
            }

            DrawPhysicsEffectsEditor(evt);
            DrawAttributeEffectsEditor(evt);
            DrawCueEffectsEditor(evt);
        }

        private void DrawPhysicsEffectsEditor(SkillTimelineEvent evt)
        {
            EnsurePhysicsEffects(evt);

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("物理效果", EditorStyles.boldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Physics Effect");
                evt.physicsEffects.Add(new SkillPhysicsEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < evt.physicsEffects.Count; i++)
            {
                SkillPhysicsEffect effect = evt.physicsEffects[i] ?? (evt.physicsEffects[i] = new SkillPhysicsEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"物理效果 {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                PhysicsEffectType newEffectType = (PhysicsEffectType)EditorGUILayout.EnumPopup("效果类型", effect.effectType);
                SkillTargetMode newTargetMode = (SkillTargetMode)EditorGUILayout.EnumPopup("作用目标", effect.targetMode);
                SkillEffectDirection newDirection = (SkillEffectDirection)EditorGUILayout.EnumPopup("作用方向", effect.direction);
                float newMagnitude = Mathf.Max(0f, EditorGUILayout.FloatField("力度/距离", effect.magnitude));
                float newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("持续时长(秒)", effect.duration));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_database, "Edit Physics Effect");
                    effect.effectType = newEffectType;
                    effect.targetMode = newTargetMode;
                    effect.direction = newDirection;
                    effect.magnitude = newMagnitude;
                    effect.duration = newDuration;
                    MarkDatabaseDirty();
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Physics Effect");
                evt.physicsEffects.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAttributeEffectsEditor(SkillTimelineEvent evt)
        {
            EnsureAttributeEffects(evt);

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("属性效果", EditorStyles.boldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Attribute Effect");
                evt.attributeEffects.Add(new SkillAttributeEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < evt.attributeEffects.Count; i++)
            {
                SkillAttributeEffect effect = evt.attributeEffects[i] ?? (evt.attributeEffects[i] = new SkillAttributeEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"属性效果 {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                SkillStatField newStatField = (SkillStatField)EditorGUILayout.EnumPopup("属性字段", effect.statField);
                SkillTargetMode newTargetMode = (SkillTargetMode)EditorGUILayout.EnumPopup("作用目标", effect.targetMode);
                float newMagnitude = EditorGUILayout.FloatField("数值", effect.magnitude);
                bool newUsePercent = EditorGUILayout.Toggle("按比例计算", effect.usePercent);
                float newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("Buff 持续时长(秒)", effect.duration));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_database, "Edit Attribute Effect");
                    effect.statField = newStatField;
                    effect.targetMode = newTargetMode;
                    effect.magnitude = newMagnitude;
                    effect.usePercent = newUsePercent;
                    effect.duration = newDuration;
                    MarkDatabaseDirty();
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Attribute Effect");
                evt.attributeEffects.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCueEffectsEditor(SkillTimelineEvent evt)
        {
            EnsureCueEffects(evt);

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("表现效果", EditorStyles.boldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Cue Effect");
                evt.cues.Add(new SkillCueEntry());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < evt.cues.Count; i++)
            {
                SkillCueEntry cue = evt.cues[i] ?? (evt.cues[i] = new SkillCueEntry());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"表现效果 {i + 1}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                GameObject newParticlePrefab = (GameObject)EditorGUILayout.ObjectField("粒子特效 Prefab", cue.particlePrefab, typeof(GameObject), false);
                AudioClip newAudioClip = (AudioClip)EditorGUILayout.ObjectField("音效片段", cue.audioClip, typeof(AudioClip), false);
                CueAnchor newAnchor = (CueAnchor)EditorGUILayout.EnumPopup("挂点", cue.anchor);
                Vector3 newOffset = EditorGUILayout.Vector3Field("位置偏移", cue.offset);
                float newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("强制持续时长(秒)", cue.duration));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_database, "Edit Cue Effect");
                    cue.particlePrefab = newParticlePrefab;
                    cue.audioClip = newAudioClip;
                    cue.anchor = newAnchor;
                    cue.offset = newOffset;
                    cue.duration = newDuration;
                    MarkDatabaseDirty();
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Cue Effect");
                evt.cues.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private static void EnsurePhysicsEffects(SkillTimelineEvent evt)
        {
            if (evt.physicsEffects == null)
                evt.physicsEffects = new List<SkillPhysicsEffect>();
        }

        private static void EnsureAttributeEffects(SkillTimelineEvent evt)
        {
            if (evt.attributeEffects == null)
                evt.attributeEffects = new List<SkillAttributeEffect>();
        }

        private static void EnsureCueEffects(SkillTimelineEvent evt)
        {
            if (evt.cues == null)
                evt.cues = new List<SkillCueEntry>();
        }

        private void MarkDatabaseDirty()
        {
            if (_database == null)
                return;

            EditorUtility.SetDirty(_database);
            RebuildSerializedDb();
            Repaint();
        }
        private void AddSkill()
        {
            EnsureEntryList();
            Undo.RecordObject(_database, "Add Skill");
            _database.entries.Add(new SharedSkillDefinition
            {
                skillId = $"new_skill_{_database.entries.Count}",
                displayName = $"新技能{_database.entries.Count}",
                events = new List<SkillTimelineEvent>(),
            });
            _selectedSkillIndex = _database.entries.Count - 1;
            _selectedEventIndex = -1;
            EditorUtility.SetDirty(_database);
            RebuildSerializedDb();
            Repaint();
        }

        private void RemoveSelectedSkill()
        {
            if (!HasSelectedSkill())
                return;

            Undo.RecordObject(_database, "Delete Skill");
            _database.entries.RemoveAt(_selectedSkillIndex);
            _selectedSkillIndex = Mathf.Clamp(_selectedSkillIndex - 1, -1, _database.entries.Count - 1);
            _selectedEventIndex = -1;
            EditorUtility.SetDirty(_database);
            RebuildSerializedDb();
            Repaint();
        }

        private void AddEvent(SharedSkillDefinition skill)
        {
            if (skill == null)
                return;

            if (skill.events == null)
                skill.events = new List<SkillTimelineEvent>();

            Undo.RecordObject(_database, "Add Skill Event");
            float startTime = HasSelectedEvent() && _selectedEventIndex < skill.events.Count
                ? skill.events[_selectedEventIndex].startTime + 0.1f
                : 0f;

            skill.events.Add(new SkillTimelineEvent
            {
                eventId = $"evt_{skill.events.Count}",
                startTime = startTime,
                damageMagnitude = 1f,
            });
            _selectedEventIndex = skill.events.Count - 1;
            EditorUtility.SetDirty(_database);
            RebuildSerializedDb();
            Repaint();
        }

        private void RemoveSelectedEvent(SharedSkillDefinition skill)
        {
            if (skill == null || !HasSelectedEvent())
                return;

            Undo.RecordObject(_database, "Delete Skill Event");
            skill.events.RemoveAt(_selectedEventIndex);
            _selectedEventIndex = Mathf.Clamp(_selectedEventIndex - 1, -1, skill.events.Count - 1);
            EditorUtility.SetDirty(_database);
            RebuildSerializedDb();
            Repaint();
        }

        private void SortEvents(SharedSkillDefinition skill)
        {
            if (skill == null || skill.events == null)
                return;

            Undo.RecordObject(_database, "Sort Skill Events");
            skill.events.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return 1;
                if (right == null)
                    return -1;
                return left.startTime.CompareTo(right.startTime);
            });
            EditorUtility.SetDirty(_database);
            RebuildSerializedDb();
            Repaint();
        }

        private void StartPreviewPlayback()
        {
            if (_previewTime >= GetPreviewTotalDuration() - 0.01f)
                _previewTime = 0f;

            StartAnimationMode();
            _isPlaying = true;
            _prevEditorTime = EditorApplication.timeSinceStartup;
        }

        private void StartAnimationMode()
        {
            if (_previewTarget == null || _previewClip == null)
                return;

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
        }

        private void StopPreview()
        {
            _isPlaying = false;
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private void SampleAnimationAtTime(float time)
        {
            if (_previewTarget == null || _previewClip == null)
                return;

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_previewTarget, _previewClip, time);
            AnimationMode.EndSampling();
        }

        private float GetPreviewTotalDuration()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            float timelineDuration = skill != null ? skill.GetTimelineDuration() : 0f;
            float clipDuration = _previewClip != null ? _previewClip.length : 0f;
            return Mathf.Max(timelineDuration, clipDuration, 0.1f);
        }

        private SharedSkillDefinition GetSelectedSkill()
        {
            if (_database == null || _database.entries == null)
                return null;
            if (_selectedSkillIndex < 0 || _selectedSkillIndex >= _database.entries.Count)
                return null;
            return _database.entries[_selectedSkillIndex];
        }

        private bool HasSelectedSkill()
        {
            return GetSelectedSkill() != null;
        }

        private bool HasSelectedEvent()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            return skill != null && skill.events != null && _selectedEventIndex >= 0 && _selectedEventIndex < skill.events.Count;
        }

        private void EnsureSerializedDatabase()
        {
            if (_database == null)
                return;

            if (_serializedDb == null || _serializedDb.targetObject != _database)
                _serializedDb = new SerializedObject(_database);

            _serializedDb.Update();
        }

        private void RebuildSerializedDb()
        {
            _serializedDb = _database != null ? new SerializedObject(_database) : null;
        }

        private void EnsureEntryList()
        {
            if (_database != null && _database.entries == null)
                _database.entries = new List<SharedSkillDefinition>();
        }

        private void TryAutoLoadDatabase()
        {
            if (_database == null)
                _database = Resources.Load<SharedSkillDatabaseSO>("配置/共享技能库");

            if (_database != null && _serializedDb == null)
                _serializedDb = new SerializedObject(_database);
        }

        private float TimeToX(float time)
        {
            return time * _zoom - _timelineScrollX;
        }

        private float XToTime(float mouseX, float panelX)
        {
            return (mouseX - panelX + _timelineScrollX) / _zoom;
        }

        private float GetEventDisplayWidth(SkillTimelineEvent evt)
        {
            if (evt == null)
                return MinEventWidth;

            if (evt.triggerMode == SkillEventTriggerMode.Repeated)
                return Mathf.Max(MinEventWidth, evt.activeDuration * _zoom);

            return Mathf.Max(MinEventWidth, string.IsNullOrEmpty(evt.hitDetectionName) ? MinEventWidth : 12f);
        }

        private float GetTimelineContentWidth(SharedSkillDefinition skill)
        {
            float duration = Mathf.Max(skill != null ? skill.GetTimelineDuration() : 0f, GetPreviewTotalDuration(), 2f);
            return duration * _zoom + 40f;
        }

        private Color GetEventColor(SkillTimelineEvent evt)
        {
            if (evt == null)
                return DefaultColor;
            if (!string.IsNullOrEmpty(evt.hitDetectionName))
                return DamageColor;
            if (evt.physicsEffects != null && evt.physicsEffects.Count > 0)
                return PhysicsColor;
            if (evt.attributeEffects != null && evt.attributeEffects.Count > 0)
                return AttributeColor;
            if (evt.cues != null && evt.cues.Count > 0)
                return CueColor;
            return DefaultColor;
        }

        private string GetEventLabel(SkillTimelineEvent evt)
        {
            if (evt == null)
                return "evt";
            if (!string.IsNullOrEmpty(evt.eventId))
                return evt.eventId;
            if (!string.IsNullOrEmpty(evt.hitDetectionName))
                return $"伤害 {evt.hitDetectionName}";
            if (evt.physicsEffects != null && evt.physicsEffects.Count > 0)
                return "物理";
            if (evt.attributeEffects != null && evt.attributeEffects.Count > 0)
                return "属性";
            if (evt.cues != null && evt.cues.Count > 0)
                return "特效";
            return "evt";
        }
        private List<List<int>> AssignEventRows(List<SkillTimelineEvent> events)
        {
            var rows = new List<List<int>>();
            var rowEnds = new List<float>();

            if (events == null)
                return rows;

            for (int i = 0; i < events.Count; i++)
            {
                SkillTimelineEvent evt = events[i];
                if (evt == null)
                    continue;

                float endTime = evt.startTime + (evt.triggerMode == SkillEventTriggerMode.Repeated
                    ? Mathf.Max(0.01f, evt.activeDuration)
                    : MinEventWidth / _zoom);

                int assignedRow = -1;
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    if (rowEnds[rowIndex] <= evt.startTime + 0.005f)
                    {
                        assignedRow = rowIndex;
                        break;
                    }
                }

                if (assignedRow < 0)
                {
                    rows.Add(new List<int>());
                    rowEnds.Add(0f);
                    assignedRow = rows.Count - 1;
                }

                rows[assignedRow].Add(i);
                rowEnds[assignedRow] = Mathf.Max(rowEnds[assignedRow], endTime);
            }

            return rows;
        }

        private static float ChooseTickInterval(float zoom)
        {
            float raw = 40f / zoom;
            float[] steps = { 0.02f, 0.05f, 0.1f, 0.2f, 0.5f, 1f, 2f, 5f };
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] >= raw)
                    return steps[i];
            }
            return 5f;
        }

        private void FitZoomToWindow()
        {
            float width = Mathf.Max(100f, position.width - LeftPanelWidth - RightPanelWidth - 40f);
            float duration = Mathf.Max(GetPreviewTotalDuration(), 2f);
            _zoom = Mathf.Clamp(width / duration, 20f, 800f);
            _timelineScrollX = 0f;
            Repaint();
        }
    }
}

