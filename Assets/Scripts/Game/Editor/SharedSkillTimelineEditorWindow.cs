using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Data;
using Game.Domain;
using Game.Presentation;

namespace Game.Editor
{
    public class SharedSkillTimelineEditorWindow : EditorWindow
    {
        private const float DefaultLeftPanelWidth = 220f;
        private const float DefaultRightPanelWidth = 340f;
        private const float MinLeftPanelWidth = 140f;
        private const float MinCenterPanelWidth = 180f;
        private const float MinRightPanelWidth = 240f;
        private const float PanelDividerWidth = 4f;
        private const float ToolbarHeight = 24f;
        private const float PreviewBarHeight = 0f;
        private const float SelectionBarHeight = 54f;
        private const float ButtonBarHeight = 58f;
        private const float RulerHeight = 22f;
        private const float TrackHeaderHeight = 18f;
        private const float TrackSpacing = 4f;
        private const float EventRowHeight = 28f;
        private const float ScrollbarHeight = 16f;
        private const float MinEventWidth = 10f;
        private const float ResizeHandleWidth = 6f;
        private const float PlayheadHitWidth = 6f;
        private const bool ShowScenePreviewText = false;
        private const float CompactSkillObjectWidth = 142f;
        private const float CompactSkillGroupWidth = 70f;
        private const float CompactSkillMenuWidth = 70f;
        private const float CompactAnimationObjectWidth = 136f;
        private const float CompactAnimationGroupWidth = 70f;
        private const float CompactAnimationEntryWidth = 70f;
        private const float CompactStatusLabelWidth = 40f;
        private const float CompactPreviewControlGapWidth = 0f;

        private static readonly Color TimelineBackground = new Color(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color RulerBackground = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color GridColor = new Color(0.34f, 0.34f, 0.34f, 1f);
        private static readonly Color DamageColor = new Color(0.90f, 0.28f, 0.28f, 0.88f);
        private static readonly Color PhysicsColor = new Color(0.28f, 0.50f, 0.92f, 0.88f);
        private static readonly Color AttributeColor = new Color(0.28f, 0.78f, 0.38f, 0.88f);
        private static readonly Color VfxColor = new Color(0.92f, 0.80f, 0.18f, 0.88f);
        private static readonly Color SfxColor = new Color(0.92f, 0.55f, 0.18f, 0.88f);
        private static readonly Color DefaultColor = new Color(0.55f, 0.55f, 0.55f, 0.88f);
        private static readonly Color SelectedOverlay = new Color(1f, 1f, 1f, 0.30f);
        private static readonly Color PlayheadColor = new Color(1f, 0.25f, 0.25f, 1f);
        private static readonly CueAnchor[] TopLevelCueAnchors = { CueAnchor.Caster, CueAnchor.World };
        private static readonly string[] TopLevelCueAnchorLabels = { "自身", "世界" };
        private static readonly CueAnchor[] OnHitCueAnchors = { CueAnchor.Caster, CueAnchor.Target, CueAnchor.World };
        private static readonly string[] OnHitCueAnchorLabels = { "自身", "目标", "世界" };

        private SkillEffectDatabaseSO _database;
        private SerializedObject _serializedDb;

        private int _selectedGroupIndex = -1;
        private int _selectedSkillIndex = -1;
        private int _selectedSkillVariantIndex = -1;
        private int _selectedEventIndex = -1;
        private TimelineTrackType _selectedEventTrackType = TimelineTrackType.Damage;

        private Vector2 _skillScroll;
        private Vector2 _propertyScroll;
        private float _timelineScrollX;
        private float _zoom = 100f;
        private float _leftPanelWidth = DefaultLeftPanelWidth;
        private float _rightPanelWidth = DefaultRightPanelWidth;
        private float _lastTimelinePanelWidth = 200f;

        private GameObject _previewTarget;
        private AnimationClip _previewClip;
        private float _previewTime;
        private bool _isPlaying;
        private double _prevEditorTime;

        private bool _isDraggingPlayhead;
        private bool _isDraggingEvent;
        private bool _isResizingEvent;
        private int _dragEventIndex = -1;
        private TimelineTrackType _dragTrackType = TimelineTrackType.Damage;
        private float _dragStartMouseX;
        private float _dragStartTime;
        private float _dragStartDuration;
        private SplitterHandle _activeSplitter;
        private TimelineTimeDisplayMode _timeDisplayMode = TimelineTimeDisplayMode.SecondsAndFrames;
        private TimelineSnapMode _snapMode = TimelineSnapMode.Frame;
        private int _frameRate = 30;
        private bool _showDamageEvents = true;
        private bool _showPhysicsEvents = true;
        private bool _showAttributeEvents = true;
        private bool _showVfxEvents = true;
        private bool _showSfxEvents = true;
        private bool _filterDamageWithOnHitPhysics;
        private bool _filterDamageWithOnHitAttribute;
        private bool _filterDamageWithOnHitVfx;
        private bool _filterDamageWithOnHitSfx;
        private TimelineTrackType _newEventTrackType = TimelineTrackType.Damage;
        private bool _sceneHandlesEnabled = true;
        private int _activeSceneDamageEventIndex = -1;
        private int _activeSceneDamageIndex = -1;
        private int _activeSceneCueEventIndex = -1;
        private int _activeSceneCueIndex = -1;
        private int _activeSceneHitVfxEventIndex = -1;
        private int _activeSceneHitVfxDamageIndex = -1;
        private int _activeSceneHitVfxIndex = -1;
        private int _activeSceneCompanionVfxEventIndex = -1;
        private int _activeSceneCompanionVfxDamageIndex = -1;
        private int _activeSceneCompanionVfxIndex = -1;
        private readonly HashSet<string> _collapsedEffectKeys = new HashSet<string>();
        private readonly HashSet<string> _hiddenPreviewCueKeys = new HashSet<string>();
        private CharacterAnimationLibrarySO _animationLibrary;
        private int _selectedAnimationGroupIndex = -1;
        private int _selectedAnimationEntryIndex = -1;
        private GameObject _scenePreviewCueInstance;
        private GameObject _scenePreviewCuePrefab;
        private int _scenePreviewCueEventIndex = -1;
        private int _scenePreviewCueListIndex = -1;
        private int _scenePreviewCueDamageIndex = -1;
        private TimelineTrackType _scenePreviewCueTrackType = TimelineTrackType.Vfx;
        private bool _scenePreviewCueFollowsDamageWindow;
        private bool _scenePreviewCueHasWorldOrigin;
        private float _scenePreviewCueWorldOriginTriggerTime = -1f;
        private Vector3 _scenePreviewCueWorldOriginPosition;
        private Quaternion _scenePreviewCueWorldOriginRotation = Quaternion.identity;
        private PreviewTargetFilter _previewTargetFilter = PreviewTargetFilter.All;
        private GameObject _previewVictimTarget;
        private PreviewTargetFilter _previewVictimFilter = PreviewTargetFilter.Enemy;
        private readonly List<TimelinePreviewVfxInstance> _timelinePreviewVfxInstances = new List<TimelinePreviewVfxInstance>();
        private readonly HashSet<string> _timelinePreviewHitSfxKeys = new HashSet<string>();
        private readonly Dictionary<Transform, Vector3> _previewPhysicsOriginalPositions = new Dictionary<Transform, Vector3>();
        private LevelGrowthSO _previewLevelGrowth;
        private EnemyStatsDatabaseSO _previewEnemyStatsDb;

        private static readonly System.Type AudioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly MethodInfo AudioUtilPlayPreviewClipMethod = ResolveAudioUtilMethod("PlayPreviewClip", typeof(AudioClip), typeof(int), typeof(bool));
        private static readonly MethodInfo AudioUtilStopAllPreviewClipsMethod = ResolveAudioUtilMethod("StopAllPreviewClips");

        private static string s_eventClipboardJson;
        private static TimelineTrackType s_eventClipboardTrackType = TimelineTrackType.Damage;

        private enum SplitterHandle
        {
            None,
            Left,
            Right,
        }

        private enum TimelineTimeDisplayMode
        {
            Seconds,
            Frames,
            SecondsAndFrames,
        }

        private enum TimelineSnapMode
        {
            None,
            Frame,
            Grid,
        }

        private enum PreviewTargetFilter
        {
            [InspectorName("全部")]
            All,
            [InspectorName("玩家")]
            Player,
            [InspectorName("敌人")]
            Enemy,
        }

        private enum TimelineTrackType
        {
            [InspectorName("命中")]
            Damage,
            [InspectorName("物理")]
            Physics,
            [InspectorName("属性")]
            Attribute,
            [InspectorName("特效")]
            Vfx,
            [InspectorName("音效")]
            Sfx,
        }

        private sealed class TimelineTrackLayout
        {
            public TimelineTrackType trackType;
            public List<List<TimelineEventHandle>> rows;
            public Rect headerRect;
            public Rect bodyRect;
        }

        private sealed class TimelineEventHandle
        {
            public TimelineTrackType trackType;
            public int eventIndex;
            public SkillTimedEventBase timedEvent;
        }

        private sealed class TimelinePreviewVfxInstance
        {
            public string key;
            public TimelineTrackType trackType;
            public int eventIndex;
            public int effectIndex;
            public int triggerIndex;
            public float triggerTime;
            public GameObject prefab;
            public GameObject instance;
            public SkillVfxEffect effect;
            public SkillDamageEffect damageEffect;
            public Transform explicitTarget;
            public Vector3 baseScale;
            public Vector3 originPosition;
            public Quaternion originRotation;
            public bool hasWorldOrigin;
            public bool followsDamageWindow;
        }

        private sealed class PreviewOverlayLine
        {
            public string text;
            public Color color;
        }

        private sealed class PreviewDamageHitResult
        {
            public int eventIndex;
            public int effectIndex;
            public int triggerIndex;
            public float triggerTime;
            public float hitTime;
            public SkillDamageEffect effect;
            public List<Transform> hitTargets = new List<Transform>();
        }

        [MenuItem("游戏/技能时间轴编辑器")]
        public static void Open()
        {
            var window = GetWindow<SharedSkillTimelineEditorWindow>("技能时间轴编辑器");
            window.minSize = new Vector2(MinLeftPanelWidth + MinCenterPanelWidth + MinRightPanelWidth + PanelDividerWidth * 2f, 420f);
            window.TryAutoLoadDatabase();
        }

        private void OnEnable()
        {
            TryAutoLoadDatabase();
            TryAutoLoadAnimationLibrary();
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyScenePreviewCueInstance();
            StopPreview();
        }

        private void OnDestroy()
        {
            DestroyScenePreviewCueInstance();
            StopPreview();
        }

        private void OnEditorUpdate()
        {
            if (!_isPlaying)
                return;

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - _prevEditorTime);
            _prevEditorTime = now;

            float previousTime = _previewTime;
            _previewTime += deltaTime;
            float totalDuration = GetPreviewTotalDuration();
            if (_previewTime >= totalDuration)
            {
                _previewTime = totalDuration;
                _isPlaying = false;
            }

            SampleAnimationAtTime(_previewTime);
            TriggerTimelinePreviewSfx(previousTime, _previewTime);
            TriggerTimelinePreviewHitSfx();
            UpdateScenePreviewCueInstance();
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_previewTarget == null)
                return;

            DrawTimelinePreviewStateOverlay();

            if (!_sceneHandlesEnabled)
                return;

            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return;

            UpdateScenePreviewCueInstance();

            if (GetTrackEvent(skill, TimelineTrackType.Vfx, _activeSceneCueEventIndex) is SkillVfxEvent activeCueEvent
                && _activeSceneCueIndex >= 0
                && activeCueEvent.vfxEffects != null
                && _activeSceneCueIndex < activeCueEvent.vfxEffects.Count
                && TryGetTopLevelSceneCuePlaybackContext(activeCueEvent, out float topLevelTriggerTime, out Transform topLevelExplicitTarget))
            {
                DrawCueSceneHandle(activeCueEvent.vfxEffects[_activeSceneCueIndex], topLevelTriggerTime, topLevelExplicitTarget, "特效效果");
            }

            if (GetTrackEvent(skill, TimelineTrackType.Damage, _activeSceneHitVfxEventIndex) is SkillDamageEvent activeHitVfxDamageEvent
                && _activeSceneHitVfxDamageIndex >= 0
                && activeHitVfxDamageEvent.damageEffects != null
                && _activeSceneHitVfxDamageIndex < activeHitVfxDamageEvent.damageEffects.Count)
            {
                SkillDamageEffect damageEffect = activeHitVfxDamageEvent.damageEffects[_activeSceneHitVfxDamageIndex];
                if (damageEffect?.onHitVfxEffects != null
                    && _activeSceneHitVfxIndex >= 0
                    && _activeSceneHitVfxIndex < damageEffect.onHitVfxEffects.Count
                    && TryGetOnHitSceneCuePlaybackContext(out float onHitTriggerTime, out Transform onHitExplicitTarget))
                {
                    DrawCueSceneHandle(damageEffect.onHitVfxEffects[_activeSceneHitVfxIndex], onHitTriggerTime, onHitExplicitTarget, "命中特效效果");
                }
            }

            if (GetTrackEvent(skill, TimelineTrackType.Damage, _activeSceneCompanionVfxEventIndex) is SkillDamageEvent activeCompanionVfxDamageEvent
                && _activeSceneCompanionVfxDamageIndex >= 0
                && activeCompanionVfxDamageEvent.damageEffects != null
                && _activeSceneCompanionVfxDamageIndex < activeCompanionVfxDamageEvent.damageEffects.Count)
            {
                SkillDamageEffect damageEffect = activeCompanionVfxDamageEvent.damageEffects[_activeSceneCompanionVfxDamageIndex];
                damageEffect?.TryMigrateLegacySubEffects();
                if (damageEffect?.companionVfxEffects != null
                    && _activeSceneCompanionVfxIndex >= 0
                    && _activeSceneCompanionVfxIndex < damageEffect.companionVfxEffects.Count
                    && TryGetSceneCompanionCuePlaybackContext(out float companionTriggerTime))
                {
                    DrawDamageCompanionCueSceneHandle(damageEffect, damageEffect.companionVfxEffects[_activeSceneCompanionVfxIndex], companionTriggerTime, "伴随特效效果");
                }
            }

            if (GetTrackEvent(skill, TimelineTrackType.Damage, _activeSceneDamageEventIndex) is SkillDamageEvent activeDamageEvent
                && _activeSceneDamageIndex >= 0
                && activeDamageEvent.damageEffects != null
                && _activeSceneDamageIndex < activeDamageEvent.damageEffects.Count)
            {
                SkillDamageEffect damageEffect = activeDamageEvent.damageEffects[_activeSceneDamageIndex];
                if (TryGetActiveSceneDamageTriggerTime(out float activeDamageTriggerTime))
                    DrawDamageSceneHandle(damageEffect, activeDamageTriggerTime);
                else
                    DrawDamageSceneHandle(damageEffect);
            }
        }

        private void DrawTimelinePreviewVfxBounds()
        {
            if (_timelinePreviewVfxInstances == null || _timelinePreviewVfxInstances.Count == 0)
                return;

            Color oldColor = Handles.color;
            for (int i = 0; i < _timelinePreviewVfxInstances.Count; i++)
            {
                TimelinePreviewVfxInstance playback = _timelinePreviewVfxInstances[i];
                if (playback?.instance == null)
                    continue;

                Renderer[] renderers = playback.instance.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                    continue;

                bool hasBounds = false;
                Bounds bounds = default;
                for (int j = 0; j < renderers.Length; j++)
                {
                    Renderer renderer = renderers[j];
                    if (renderer == null)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                if (!hasBounds)
                    continue;

                Handles.color = playback.trackType == TimelineTrackType.Damage
                    ? new Color(1f, 0.85f, 0.22f, 0.85f)
                    : new Color(0.18f, 0.9f, 1f, 0.85f);
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            Handles.color = oldColor;
        }

        private void OnGUI()
        {
            EnsureSerializedDatabase();

            float totalWidth = position.width;
            float totalHeight = position.height;

            DrawToolbar(new Rect(0f, 0f, totalWidth, ToolbarHeight));
            DrawPreviewBar(new Rect(0f, ToolbarHeight, totalWidth, PreviewBarHeight));
            DrawSelectionBar(new Rect(0f, ToolbarHeight + PreviewBarHeight, totalWidth, SelectionBarHeight));

            float contentY = ToolbarHeight + PreviewBarHeight + SelectionBarHeight;
            float contentHeight = totalHeight - contentY;
            ClampPanelWidths(totalWidth);

            Rect leftRect = new Rect(0f, contentY, _leftPanelWidth, contentHeight);
            Rect leftDividerRect = new Rect(leftRect.xMax, contentY, PanelDividerWidth, contentHeight);
            Rect rightDividerRect = new Rect(totalWidth - _rightPanelWidth - PanelDividerWidth, contentY, PanelDividerWidth, contentHeight);
            Rect centerRect = new Rect(leftDividerRect.xMax, contentY, Mathf.Max(MinCenterPanelWidth, rightDividerRect.x - leftDividerRect.xMax), contentHeight);
            Rect rightRect = new Rect(rightDividerRect.xMax, contentY, _rightPanelWidth, contentHeight);

            HandlePanelResizing(leftDividerRect, rightDividerRect, totalWidth);

            ClampPanelWidths(totalWidth);
            leftRect = new Rect(0f, contentY, _leftPanelWidth, contentHeight);
            leftDividerRect = new Rect(leftRect.xMax, contentY, PanelDividerWidth, contentHeight);
            rightDividerRect = new Rect(totalWidth - _rightPanelWidth - PanelDividerWidth, contentY, PanelDividerWidth, contentHeight);
            centerRect = new Rect(leftDividerRect.xMax, contentY, Mathf.Max(MinCenterPanelWidth, rightDividerRect.x - leftDividerRect.xMax), contentHeight);
            rightRect = new Rect(rightDividerRect.xMax, contentY, _rightPanelWidth, contentHeight);
            _lastTimelinePanelWidth = centerRect.width;

            DrawLeftPanel(leftRect);
            DrawPanelDivider(leftDividerRect, _activeSplitter == SplitterHandle.Left);
            DrawTimelinePanel(centerRect);
            DrawPanelDivider(rightDividerRect, _activeSplitter == SplitterHandle.Right);
            DrawRightPanel(rightRect);

            if (_serializedDb != null)
                _serializedDb.ApplyModifiedProperties();
        }

        private void ClampPanelWidths(float totalWidth)
        {
            float maxLeft = Mathf.Max(MinLeftPanelWidth, totalWidth - MinCenterPanelWidth - MinRightPanelWidth - PanelDividerWidth * 2f);
            _leftPanelWidth = Mathf.Clamp(_leftPanelWidth, MinLeftPanelWidth, maxLeft);

            float maxRight = Mathf.Max(MinRightPanelWidth, totalWidth - _leftPanelWidth - MinCenterPanelWidth - PanelDividerWidth * 2f);
            _rightPanelWidth = Mathf.Clamp(_rightPanelWidth, MinRightPanelWidth, maxRight);

            maxLeft = Mathf.Max(MinLeftPanelWidth, totalWidth - _rightPanelWidth - MinCenterPanelWidth - PanelDividerWidth * 2f);
            _leftPanelWidth = Mathf.Clamp(_leftPanelWidth, MinLeftPanelWidth, maxLeft);
        }

        private void HandlePanelResizing(Rect leftDividerRect, Rect rightDividerRect, float totalWidth)
        {
            Event evt = Event.current;
            EditorGUIUtility.AddCursorRect(leftDividerRect, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(rightDividerRect, MouseCursor.ResizeHorizontal);

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (leftDividerRect.Contains(evt.mousePosition))
                {
                    _activeSplitter = SplitterHandle.Left;
                    evt.Use();
                }
                else if (rightDividerRect.Contains(evt.mousePosition))
                {
                    _activeSplitter = SplitterHandle.Right;
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                if (_activeSplitter == SplitterHandle.Left)
                {
                    float maxLeft = Mathf.Max(MinLeftPanelWidth, totalWidth - _rightPanelWidth - MinCenterPanelWidth - PanelDividerWidth * 2f);
                    _leftPanelWidth = Mathf.Clamp(evt.mousePosition.x, MinLeftPanelWidth, maxLeft);
                    evt.Use();
                    Repaint();
                }
                else if (_activeSplitter == SplitterHandle.Right)
                {
                    float maxRight = Mathf.Max(MinRightPanelWidth, totalWidth - _leftPanelWidth - MinCenterPanelWidth - PanelDividerWidth * 2f);
                    _rightPanelWidth = Mathf.Clamp(totalWidth - evt.mousePosition.x - PanelDividerWidth, MinRightPanelWidth, maxRight);
                    evt.Use();
                    Repaint();
                }
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0 && _activeSplitter != SplitterHandle.None)
            {
                _activeSplitter = SplitterHandle.None;
                evt.Use();
            }
        }

        private static void DrawPanelDivider(Rect rect, bool active)
        {
            Color color = active
                ? new Color(0.38f, 0.62f, 0.95f, 1f)
                : new Color(0.12f, 0.12f, 0.12f, 1f);
            EditorGUI.DrawRect(rect, color);
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

                    GUILayout.Space(8f);
                    GUILayout.Label("显示", EditorStyles.miniLabel, GUILayout.Width(28f));
                    _timeDisplayMode = (TimelineTimeDisplayMode)EditorGUILayout.EnumPopup(_timeDisplayMode, EditorStyles.toolbarPopup, GUILayout.Width(92f));

                    GUILayout.Space(6f);
                    GUILayout.Label("吸附", EditorStyles.miniLabel, GUILayout.Width(28f));
                    _snapMode = (TimelineSnapMode)EditorGUILayout.EnumPopup(_snapMode, EditorStyles.toolbarPopup, GUILayout.Width(72f));

                    GUILayout.Space(6f);
                    GUILayout.Label("FPS", EditorStyles.miniLabel, GUILayout.Width(24f));
                    _frameRate = Mathf.Max(1, EditorGUILayout.IntField(_frameRate, EditorStyles.toolbarTextField, GUILayout.Width(40f)));

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
            // Preview controls are drawn in the selection bar.
        }
        private void DrawLeftPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.24f, 0.24f, 0.24f, 1f));

            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 22f);
            EditorGUI.DrawRect(titleRect, new Color(0.20f, 0.20f, 0.20f, 1f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), "预览属性", EditorStyles.boldLabel);

            GUILayout.BeginArea(new Rect(rect.x + 4f, rect.y + 24f, rect.width - 8f, rect.height - 28f));
            try
            {
                _skillScroll = EditorGUILayout.BeginScrollView(_skillScroll);
                try
                {
                    DrawLeftPreviewInspector();
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

        private void DrawSelectionBar(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.17f, 0.17f, 0.17f, 1f));
            GUILayout.BeginArea(rect);
            try
            {
                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUILayout.FlexibleSpace();
                    DrawSkillLibrarySelectorCompact();
                    GUILayout.Space(CompactPreviewControlGapWidth);
                    DrawPreviewControlsCompact();
                    GUILayout.Space(CompactPreviewControlGapWidth);
                    DrawAnimationLibrarySelectorCompact();
                    GUILayout.FlexibleSpace();
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }

                string previewStateLabel = _isPlaying
                    ? "播放中"
                    : (AnimationMode.InAnimationMode() ? "预览中" : string.Empty);
                if (!string.IsNullOrEmpty(previewStateLabel))
                {
                    GUI.Label(
                        new Rect(rect.width - CompactStatusLabelWidth - 4f, 2f, CompactStatusLabelWidth, 18f),
                        previewStateLabel,
                        EditorStyles.miniLabel);
                }

                GUILayout.Space(4f);

                EditorGUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("预览对象", EditorStyles.miniLabel, GUILayout.Width(48f));
                    GUILayout.Label("标签", EditorStyles.miniLabel, GUILayout.Width(24f));

                    EditorGUI.BeginChangeCheck();
                    _previewTargetFilter = (PreviewTargetFilter)EditorGUILayout.EnumPopup(_previewTargetFilter, EditorStyles.toolbarPopup, GUILayout.Width(56f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        EnsurePreviewTargetIsValid();
                        if (!_isPlaying)
                            SampleAnimationAtTime(_previewTime);
                    }

                    DrawPreviewTargetSelector();

                    GUILayout.Space(8f);
                    GUILayout.Label("目标", EditorStyles.miniLabel, GUILayout.Width(24f));
                    GUILayout.Label("标签", EditorStyles.miniLabel, GUILayout.Width(24f));

                    EditorGUI.BeginChangeCheck();
                    _previewVictimFilter = (PreviewTargetFilter)EditorGUILayout.EnumPopup(_previewVictimFilter, EditorStyles.toolbarPopup, GUILayout.Width(56f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        EnsurePreviewVictimIsValid();
                        if (!_isPlaying)
                            SampleAnimationAtTime(_previewTime);
                    }

                    DrawPreviewVictimSelector();
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
            finally
            {
                GUILayout.EndArea();
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

            Rect rulerRect = new Rect(rect.x, timelineY, rect.width, RulerHeight);
            List<TimelineTrackLayout> trackLayouts = BuildTrackLayouts(skill, rect.x, timelineY + RulerHeight, rect.width);
            float eventsHeight = GetTrackLayoutsHeight(trackLayouts);
            Rect eventsRect = new Rect(rect.x, timelineY + RulerHeight, rect.width, eventsHeight);
            Rect scrollbarRect = new Rect(rect.x, eventsRect.yMax + 2f, rect.width, ScrollbarHeight);

            DrawRuler(rulerRect);
            HandlePlayheadDragInRuler(rulerRect);
            DrawPlayheadLine(rulerRect);

            EditorGUI.DrawRect(eventsRect, TimelineBackground);
            DrawGridLines(eventsRect);
            DrawTrackHeaders(trackLayouts);
            HandleTimelineMouse(Event.current, skill, trackLayouts);
            DrawEventBlocks(skill, trackLayouts);
            DrawPlayheadLine(eventsRect);

            float contentWidth = GetTimelineContentWidth(skill);
            float maxScroll = Mathf.Max(0f, contentWidth - rect.width);
            DrawTimelineScrollbar(scrollbarRect, contentWidth, maxScroll);

            if (Event.current.type == EventType.ScrollWheel && rect.Contains(Event.current.mousePosition))
            {
                float pivotTime = XToTime(Event.current.mousePosition.x, rect.x);
                _zoom = Mathf.Clamp(_zoom - Event.current.delta.y * 5f, 20f, 800f);
                _timelineScrollX = Mathf.Max(0f, pivotTime * _zoom - (Event.current.mousePosition.x - rect.x));
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawTimelineScrollbar(Rect rect, float contentWidth, float maxScroll)
        {
            if (contentWidth <= rect.width + 0.01f)
            {
                _timelineScrollX = 0f;
                EditorGUI.DrawRect(rect, new Color(0.19f, 0.19f, 0.19f, 1f));
                Rect fillRect = new Rect(rect.x + 1f, rect.y + 2f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 4f));
                EditorGUI.DrawRect(fillRect, new Color(0.42f, 0.42f, 0.42f, 1f));
                return;
            }

            GUI.BeginGroup(rect);
            try
            {
                float visibleSize = rect.width;
                Rect localRect = new Rect(0f, 0f, rect.width, rect.height);
                _timelineScrollX = GUI.HorizontalScrollbar(localRect, _timelineScrollX, visibleSize, 0f, contentWidth);
                _timelineScrollX = Mathf.Clamp(_timelineScrollX, 0f, maxScroll);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        private void DrawTimelineButtonBar(Rect rect, SharedSkillDefinition skill)
        {
            EditorGUI.DrawRect(rect, new Color(0.20f, 0.20f, 0.20f, 1f));
            GUILayout.BeginArea(rect);
            try
            {
                GUIStyle compactPopupStyle = new GUIStyle(EditorStyles.toolbarPopup) { fontSize = 10 };
                GUIStyle compactLeftButtonStyle = new GUIStyle(EditorStyles.miniButtonLeft) { fontSize = 10 };
                GUIStyle compactMidButtonStyle = new GUIStyle(EditorStyles.miniButtonMid) { fontSize = 10 };
                GUIStyle compactRightButtonStyle = new GUIStyle(EditorStyles.miniButtonRight) { fontSize = 10 };
                GUIStyle compactLabelStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10 };
                GUILayout.BeginVertical();
                try
                {
                    bool hasSkill = skill != null;
                    bool hasEvent = hasSkill && HasSelectedEvent();
                    bool hasClipboard = !string.IsNullOrEmpty(s_eventClipboardJson);

                    GUILayout.BeginHorizontal();
                    try
                    {
                        EditorGUI.BeginDisabledGroup(!hasSkill);
                        try
                        {
                            _newEventTrackType = (TimelineTrackType)EditorGUILayout.EnumPopup(_newEventTrackType, compactPopupStyle, GUILayout.Width(58f));
                            if (GUILayout.Button("添加事件", compactLeftButtonStyle, GUILayout.Width(66f)))
                                AddEvent(skill, _newEventTrackType);
                        }
                        finally
                        {
                            EditorGUI.EndDisabledGroup();
                        }

                        EditorGUI.BeginDisabledGroup(!hasEvent);
                        try
                        {
                            if (GUILayout.Button("删除事件", compactMidButtonStyle, GUILayout.Width(66f)))
                                RemoveSelectedEvent(skill);
                            if (GUILayout.Button("复制事件", compactMidButtonStyle, GUILayout.Width(66f)))
                                CopySelectedEvent(skill);
                        }
                        finally
                        {
                            EditorGUI.EndDisabledGroup();
                        }

                        EditorGUI.BeginDisabledGroup(!hasSkill || !hasClipboard);
                        try
                        {
                            if (GUILayout.Button("粘贴事件", compactMidButtonStyle, GUILayout.Width(66f)))
                                PasteClipboardEvent(skill);
                        }
                        finally
                        {
                            EditorGUI.EndDisabledGroup();
                        }

                        EditorGUI.BeginDisabledGroup(!hasSkill);
                        try
                        {
                            if (GUILayout.Button("清空事件", compactRightButtonStyle, GUILayout.Width(66f)))
                                ConfirmAndClearAllEvents(skill);
                        }
                        finally
                        {
                            EditorGUI.EndDisabledGroup();
                        }

                        GUILayout.FlexibleSpace();
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    try
                    {
                        GUILayout.Label("筛选", compactLabelStyle, GUILayout.Width(26f));
                        if (GUILayout.Button("全部", compactLeftButtonStyle, GUILayout.Width(38f)))
                        {
                            _showDamageEvents = true;
                            _showPhysicsEvents = true;
                            _showAttributeEvents = true;
                            _showVfxEvents = true;
                            _showSfxEvents = true;
                            _filterDamageWithOnHitPhysics = false;
                            _filterDamageWithOnHitAttribute = false;
                            _filterDamageWithOnHitVfx = false;
                            _filterDamageWithOnHitSfx = false;
                            Repaint();
                        }

                        _showDamageEvents = GUILayout.Toggle(_showDamageEvents, "命中", compactMidButtonStyle, GUILayout.Width(38f));
                        _showPhysicsEvents = GUILayout.Toggle(_showPhysicsEvents, "物理", compactMidButtonStyle, GUILayout.Width(38f));
                        _showAttributeEvents = GUILayout.Toggle(_showAttributeEvents, "属性", compactMidButtonStyle, GUILayout.Width(38f));
                        _showVfxEvents = GUILayout.Toggle(_showVfxEvents, "特效", compactMidButtonStyle, GUILayout.Width(38f));
                        _showSfxEvents = GUILayout.Toggle(_showSfxEvents, "音效", compactRightButtonStyle, GUILayout.Width(38f));

                        GUILayout.Space(6f);
                        GUILayout.Label($"吸附: {GetSnapModeLabel()}", compactLabelStyle, GUILayout.Width(64f));
                        GUILayout.Label($"步长: {FormatTimelineTime(GetActiveSnapStep())}", compactLabelStyle, GUILayout.Width(96f));

                        GUILayout.FlexibleSpace();
                        if (hasSkill)
                            GUILayout.Label(skill.skillId, compactLabelStyle);
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    try
                    {
                        GUILayout.Label("命中筛选", compactLabelStyle, GUILayout.Width(48f));
                        _filterDamageWithOnHitPhysics = GUILayout.Toggle(_filterDamageWithOnHitPhysics, "有物理", compactLeftButtonStyle, GUILayout.Width(48f));
                        _filterDamageWithOnHitAttribute = GUILayout.Toggle(_filterDamageWithOnHitAttribute, "有属性", compactMidButtonStyle, GUILayout.Width(48f));
                        _filterDamageWithOnHitVfx = GUILayout.Toggle(_filterDamageWithOnHitVfx, "有特效", compactMidButtonStyle, GUILayout.Width(48f));
                        _filterDamageWithOnHitSfx = GUILayout.Toggle(_filterDamageWithOnHitSfx, "有音效", compactRightButtonStyle, GUILayout.Width(48f));
                        GUILayout.FlexibleSpace();
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }
                }
                finally
                {
                    GUILayout.EndVertical();
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
                    GUI.Label(new Rect(x + 2f, rect.y + 2f, 84f, 14f), FormatRulerTime(time), EditorStyles.miniLabel);
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
        private void DrawEventBlocks(SharedSkillDefinition skill, List<TimelineTrackLayout> trackLayouts)
        {
            if (skill == null)
                return;

            for (int trackIndex = 0; trackIndex < trackLayouts.Count; trackIndex++)
            {
                TimelineTrackLayout layout = trackLayouts[trackIndex];
                for (int rowIndex = 0; rowIndex < layout.rows.Count; rowIndex++)
                {
                    float rowY = layout.bodyRect.y + rowIndex * EventRowHeight;
                    foreach (TimelineEventHandle handle in layout.rows[rowIndex])
                    {
                        SkillTimedEventBase evt = handle?.timedEvent;
                        if (evt == null)
                            continue;

                        float x = layout.bodyRect.x + TimeToX(evt.startTime);
                        float width = GetEventDisplayWidth(evt, layout.trackType);
                        Rect blockRect = new Rect(x, rowY + 2f, width, EventRowHeight - 4f);
                        if (blockRect.xMax < layout.bodyRect.x || blockRect.x > layout.bodyRect.xMax)
                            continue;

                        EditorGUI.DrawRect(blockRect, GetTrackColor(layout.trackType));
                        if (handle.trackType == _selectedEventTrackType && handle.eventIndex == _selectedEventIndex)
                            EditorGUI.DrawRect(blockRect, SelectedOverlay);
                        if (IsCurrentPreviewEvent(evt, layout.trackType))
                            DrawCurrentEventOutline(blockRect);

                        if (blockRect.width > 18f)
                        {
                            GUI.Label(
                                new Rect(blockRect.x + 4f, blockRect.y + 1f, blockRect.width - 8f, blockRect.height - 2f),
                                GetTrackEventLabel(evt, layout.trackType),
                                EditorStyles.miniLabel);
                        }

                        if (GetTrackDisplayDuration(evt, layout.trackType) > 0f && blockRect.width > ResizeHandleWidth * 2f)
                        {
                            Rect handleRect = new Rect(blockRect.xMax - ResizeHandleWidth, blockRect.y, ResizeHandleWidth, blockRect.height);
                            EditorGUI.DrawRect(handleRect, new Color(1f, 1f, 1f, 0.25f));
                            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);
                        }
                    }
                }
            }
        }

        private void HandleTimelineMouse(Event evt, SharedSkillDefinition skill, List<TimelineTrackLayout> trackLayouts)
        {
            if (skill == null)
                return;

            if (evt.type == EventType.MouseDown && evt.button == 0 && IsMouseInAnyTrackBody(evt.mousePosition, trackLayouts))
            {
                bool hitEdge;
                TimelineEventHandle hitHandle = HitTestEvent(evt.mousePosition, skill, trackLayouts, out hitEdge);
                if (hitHandle != null)
                {
                    SelectEvent(hitHandle.trackType, hitHandle.eventIndex);
                    _dragEventIndex = hitHandle.eventIndex;
                    _dragTrackType = hitHandle.trackType;
                    _dragStartMouseX = evt.mousePosition.x;
                    _dragStartTime = hitHandle.timedEvent.startTime;
                    _dragStartDuration = GetTrackDisplayDuration(hitHandle.timedEvent, hitHandle.trackType);
                    _isDraggingEvent = true;
                    _isResizingEvent = hitEdge;
                    GUI.FocusControl(null);
                    evt.Use();
                    Repaint();
                }
                else
                {
                    ClearSelectedEvent();
                    Repaint();
                }
            }
            else if (evt.type == EventType.MouseDrag && _isDraggingEvent && evt.button == 0)
            {
                SkillTimedEventBase draggedEvent = GetTrackEvent(skill, _dragTrackType, _dragEventIndex);
                if (draggedEvent == null)
                    return;

                float deltaTime = (evt.mousePosition.x - _dragStartMouseX) / _zoom;
                Undo.RecordObject(_database, _isResizingEvent ? "Resize Skill Event" : "Move Skill Event");
                if (_isResizingEvent)
                    SetTrackDisplayDuration(draggedEvent, _dragTrackType, SnapDuration(Mathf.Max(0.01f, _dragStartDuration + deltaTime)));
                else
                    draggedEvent.startTime = SnapTime(Mathf.Max(0f, _dragStartTime + deltaTime));

                MarkDatabaseDirty();
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseUp && _isDraggingEvent && evt.button == 0)
            {
                _isDraggingEvent = false;
                _isResizingEvent = false;
                _dragEventIndex = -1;
                _dragTrackType = TimelineTrackType.Damage;
                evt.Use();
                Repaint();
            }
        }

        private TimelineEventHandle HitTestEvent(Vector2 mousePosition, SharedSkillDefinition skill, List<TimelineTrackLayout> trackLayouts, out bool hitEdge)
        {
            hitEdge = false;
            for (int trackIndex = 0; trackIndex < trackLayouts.Count; trackIndex++)
            {
                TimelineTrackLayout layout = trackLayouts[trackIndex];
                for (int rowIndex = 0; rowIndex < layout.rows.Count; rowIndex++)
                {
                    float rowY = layout.bodyRect.y + rowIndex * EventRowHeight;
                    foreach (TimelineEventHandle handle in layout.rows[rowIndex])
                    {
                        SkillTimedEventBase evt = handle?.timedEvent;
                        if (evt == null)
                            continue;

                        float x = layout.bodyRect.x + TimeToX(evt.startTime);
                        float width = GetEventDisplayWidth(evt, layout.trackType);
                        Rect blockRect = new Rect(x, rowY + 2f, width, EventRowHeight - 4f);
                        if (!blockRect.Contains(mousePosition))
                            continue;

                        if (GetTrackDisplayDuration(evt, layout.trackType) > 0f && width > ResizeHandleWidth * 2f)
                        {
                            Rect handleRect = new Rect(blockRect.xMax - ResizeHandleWidth, blockRect.y, ResizeHandleWidth, blockRect.height);
                            if (handleRect.Contains(mousePosition))
                            {
                                hitEdge = true;
                                return handle;
                            }
                        }

                        return handle;
                    }
                }
            }

            return null;
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
            _previewTime = SnapTime(Mathf.Max(0f, XToTime(mouseX, panelX)));
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
                float oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = Mathf.Clamp(rect.width * 0.34f, 88f, 150f);
                _propertyScroll = EditorGUILayout.BeginScrollView(_propertyScroll);
                try
                {
                    DrawSkillInspector(skill);
                    DrawSkillValidationWarnings(skill);

                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("事件概览", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("事件数量", GetTotalEventCount(skill).ToString());

                    if (HasSelectedEvent())
                    {
                        DrawSelectedEventInspector();
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
                    EditorGUIUtility.labelWidth = oldLabelWidth;
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
            SkillEffectVariantGroupDefinition skillGroup = GetSelectedSkillVariantGroup();
            bool isDefaultVariant = _selectedSkillVariantIndex == 0;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("技能组", SkillEffectDatabaseSO.GetVariantGroupName(skillGroup));

            if (isDefaultVariant)
                EditorGUILayout.HelpBox("默认技能效果不可改名；如需替换效果，请在当前技能组下新增技能效果变体。", MessageType.None);

            EditorGUI.BeginChangeCheck();
            string newSkillId;
            using (new EditorGUI.DisabledScope(isDefaultVariant))
                newSkillId = EditorGUILayout.TextField("技能ID", skill.skillId ?? string.Empty);
            string newAnimationTrigger = EditorGUILayout.TextField("动画 Trigger", skill.animationTrigger ?? string.Empty);
            bool newIgnoreAnimationDamageEvents = EditorGUILayout.Toggle("忽略动画帧伤害事件", skill.ignoreAnimationDamageEvents);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(_database, "Edit Skill");
            if (!isDefaultVariant)
                skill.skillId = newSkillId;
            skill.animationTrigger = newAnimationTrigger;
            skill.ignoreAnimationDamageEvents = newIgnoreAnimationDamageEvents;
            MarkDatabaseDirty();
        }

        private void DrawLeftPreviewInspector()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (_database == null)
            {
                EditorGUILayout.HelpBox("未选择技能库。", MessageType.Info);
                return;
            }

            if (skill == null)
            {
                EditorGUILayout.HelpBox("当前分组下未选择技能。", MessageType.Info);
                return;
            }

            if (_previewTarget == null)
            {
                EditorGUILayout.HelpBox("未选择预览对象。", MessageType.Info);
            }

            DrawPreviewStatsPanel(skill);
        }

        private void DrawPreviewStatsPanel(SharedSkillDefinition skill)
        {
            if (_previewTarget == null)
                return;

            List<PreviewDamageHitResult> damageHits = CollectPreviewDamageHitHistory(skill);

            EditorGUILayout.LabelField("自身", EditorStyles.boldLabel);
            DrawPreviewStatBlock(skill, _previewTarget.transform, damageHits, "属性");

            Transform explicitVictim = ResolveRootPreviewTarget(_previewVictimTarget)?.transform;
            Transform hitTarget = explicitVictim ?? ResolvePrimaryPreviewHitTarget();
            if (hitTarget == null)
                return;

            if (explicitVictim == null && hitTarget == _previewTarget.transform)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("目标", EditorStyles.boldLabel);
            DrawPreviewStatBlock(skill, hitTarget, damageHits, "属性");
        }

        private void DrawPreviewStatBlock(SharedSkillDefinition skill, Transform target, List<PreviewDamageHitResult> damageHits, string title)
        {
            List<KeyValuePair<string, string>> items = BuildDisplayStatItems(skill, target, damageHits);
            if (items == null || items.Count == 0)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            try
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                DrawPreviewStatGrid(items);
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private List<KeyValuePair<string, string>> BuildDisplayStatItems(SharedSkillDefinition skill, Transform target, List<PreviewDamageHitResult> damageHits)
        {
            if (target == null)
                return null;

            target = ResolvePreviewStatTarget(target);
            PlayerController player = target.GetComponentInChildren<PlayerController>();
            if (player != null)
            {
                return new List<KeyValuePair<string, string>>
                {
                    BuildPreviewResourceStatItem(skill, target, damageHits, "生命", SkillStatField.HP),
                    BuildPreviewResourceStatItem(skill, target, damageHits, "法力", SkillStatField.MP),
                    BuildPreviewStatItem(skill, target, damageHits, "攻击", SkillStatField.Attack, false),
                    BuildPreviewStatItem(skill, target, damageHits, "防御", SkillStatField.Defense, false),
                    BuildPreviewStatItem(skill, target, damageHits, "生命回复", SkillStatField.HPRegen, false),
                    BuildPreviewStatItem(skill, target, damageHits, "法力回复", SkillStatField.MPRegen, false),
                    BuildPreviewStatItem(skill, target, damageHits, "暴击率", SkillStatField.CritRate, true),
                    BuildPreviewStatItem(skill, target, damageHits, "暴击伤害", SkillStatField.CritDamage, true),
                    BuildPreviewStatItem(skill, target, damageHits, "攻速", SkillStatField.AttackSpeed, false),
                    BuildPreviewStatItem(skill, target, damageHits, "移速", SkillStatField.MoveSpeed, false),
                    BuildPreviewStatItem(skill, target, damageHits, "吸血", SkillStatField.LifeSteal, true),
                    BuildPreviewStatItem(skill, target, damageHits, "增伤", SkillStatField.SkillDamage, true),
                };
            }

            EnemyController enemy = target.GetComponentInChildren<EnemyController>();
            if (enemy != null)
            {
                return new List<KeyValuePair<string, string>>
                {
                    BuildPreviewResourceStatItem(skill, target, damageHits, "生命", SkillStatField.HP),
                    BuildPreviewStatItem(skill, target, damageHits, "攻击", SkillStatField.Attack, false),
                    BuildPreviewStatItem(skill, target, damageHits, "防御", SkillStatField.Defense, false),
                    BuildPreviewStatItem(skill, target, damageHits, "移速", SkillStatField.MoveSpeed, false),
                };
            }

            return null;
        }

        private KeyValuePair<string, string> BuildPreviewResourceStatItem(SharedSkillDefinition skill, Transform target, List<PreviewDamageHitResult> damageHits, string label, SkillStatField field)
        {
            if (!TryGetEffectivePreviewResourceValue(skill, target, damageHits, field, out float currentValue, out float maxValue))
                return new KeyValuePair<string, string>(label, "--");

            return new KeyValuePair<string, string>(label, $"{FormatValue(currentValue)}/{FormatValue(maxValue)}");
        }

        private KeyValuePair<string, string> BuildPreviewStatItem(SharedSkillDefinition skill, Transform target, List<PreviewDamageHitResult> damageHits, string label, SkillStatField field, bool treatAsPercent)
        {
            float value = GetEffectivePreviewStatValue(skill, target, damageHits, field, out bool found);
            if (!found)
                return new KeyValuePair<string, string>(label, "--");

            return new KeyValuePair<string, string>(label, treatAsPercent ? FormatPercent(value) : FormatValue(value));
        }

        private void DrawPreviewStatGrid(List<KeyValuePair<string, string>> items)
        {
            for (int i = 0; i < items.Count; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                try
                {
                    DrawPreviewStatCell(items[i]);
                    GUILayout.Space(6f);
                    if (i + 1 < items.Count)
                        DrawPreviewStatCell(items[i + 1]);
                    else
                        GUILayout.FlexibleSpace();
                }
                finally
                {
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void DrawPreviewStatCell(KeyValuePair<string, string> item)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 18f, GUILayout.ExpandWidth(true));
            GUI.Label(rect, $"{item.Key}: {item.Value}", EditorStyles.label);
        }

        private Transform GetPrimaryPreviewHitTarget(SharedSkillDefinition skill)
        {
            return ResolvePrimaryPreviewHitTarget();
        }

        private List<KeyValuePair<string, string>> BuildCurrentStatItems(Transform target)
        {
            if (target == null)
                return null;

            target = ResolvePreviewStatTarget(target);
            PlayerController player = target.GetComponentInChildren<PlayerController>();
            if (player?.PlayerModel != null)
            {
                Stats stats = player.PlayerModel.Stats;
                return new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("生命", FormatValue(stats.MaxHp)),
                    new KeyValuePair<string, string>("法力", FormatValue(stats.MaxMp)),
                    new KeyValuePair<string, string>("攻击", FormatValue(stats.Attack)),
                    new KeyValuePair<string, string>("防御", FormatValue(stats.Defense)),
                    new KeyValuePair<string, string>("生命回复", FormatValue(stats.HpRegen)),
                    new KeyValuePair<string, string>("法力回复", FormatValue(stats.MpRegen)),
                    new KeyValuePair<string, string>("暴击率", FormatPercent(stats.CritRate)),
                    new KeyValuePair<string, string>("暴击伤害", FormatPercent(stats.CritDmg)),
                    new KeyValuePair<string, string>("攻速", FormatValue(stats.AttackSpeed)),
                    new KeyValuePair<string, string>("移速", FormatValue(stats.MoveSpeed)),
                    new KeyValuePair<string, string>("吸血", FormatPercent(stats.LifeSteal)),
                    new KeyValuePair<string, string>("增伤", FormatPercent(stats.DamageBonus)),
                };
            }

            EnemyController enemy = target.GetComponentInChildren<EnemyController>();
            if (enemy != null)
            {
                return new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("生命", FormatValue(enemy.MaxHp)),
                    new KeyValuePair<string, string>("攻击", FormatValue(enemy.Attack)),
                    new KeyValuePair<string, string>("防御", FormatValue(enemy.Defense)),
                    new KeyValuePair<string, string>("移速", FormatValue(enemy.MoveSpeed)),
                };
            }

            return null;
        }

        private List<KeyValuePair<string, string>> BuildBaseStatItems(Transform target)
        {
            if (target == null)
                return null;

            target = ResolvePreviewStatTarget(target);
            PlayerController player = target.GetComponentInChildren<PlayerController>();
            if (player != null)
            {
                EnsurePreviewReferenceDatabases();
                var stats = new Stats();
                _previewLevelGrowth?.GetStatsForLevel(1, stats);
                return new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("生命", FormatValue(stats.MaxHp)),
                    new KeyValuePair<string, string>("法力", FormatValue(stats.MaxMp)),
                    new KeyValuePair<string, string>("攻击", FormatValue(stats.Attack)),
                    new KeyValuePair<string, string>("防御", FormatValue(stats.Defense)),
                    new KeyValuePair<string, string>("生命回复", FormatValue(stats.HpRegen)),
                    new KeyValuePair<string, string>("法力回复", FormatValue(stats.MpRegen)),
                    new KeyValuePair<string, string>("暴击率", FormatPercent(stats.CritRate)),
                    new KeyValuePair<string, string>("暴击伤害", FormatPercent(stats.CritDmg)),
                    new KeyValuePair<string, string>("攻速", FormatValue(stats.AttackSpeed)),
                    new KeyValuePair<string, string>("移速", FormatValue(stats.MoveSpeed)),
                    new KeyValuePair<string, string>("吸血", FormatPercent(stats.LifeSteal)),
                    new KeyValuePair<string, string>("增伤", FormatPercent(stats.DamageBonus)),
                };
            }

            EnsurePreviewReferenceDatabases();
            EnemyController enemy = target.GetComponentInChildren<EnemyController>();
            if (enemy != null && _previewEnemyStatsDb != null)
            {
                EnemyStatsEntry entry = _previewEnemyStatsDb.GetEntry(enemy.EnemyId) ?? _previewEnemyStatsDb.GetEntry(enemy.EnemyCategory);
                if (entry != null)
                {
                    return new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("生命", FormatValue(entry.baseHp)),
                        new KeyValuePair<string, string>("攻击", FormatValue(entry.baseAttack)),
                        new KeyValuePair<string, string>("防御", FormatValue(entry.baseDefense)),
                        new KeyValuePair<string, string>("移速", FormatValue(entry.baseMoveSpeed)),
                    };
                }
            }

            return null;
        }

        private bool TryGetEffectivePreviewResourceValue(SharedSkillDefinition skill, Transform target, List<PreviewDamageHitResult> damageHits, SkillStatField field, out float currentValue, out float maxValue)
        {
            currentValue = 0f;
            maxValue = 0f;
            if (!TryGetPreviewBaselineStatValue(target, field, out currentValue, out maxValue))
                return false;

            ApplyPreviewAttributeEffects(ref currentValue, skill, target, damageHits, field, maxValue, resourceMode: true);
            currentValue = Mathf.Clamp(currentValue, 0f, maxValue);
            return true;
        }

        private float GetEffectivePreviewStatValue(SharedSkillDefinition skill, Transform target, List<PreviewDamageHitResult> damageHits, SkillStatField field, out bool found)
        {
            target = ResolvePreviewStatTarget(target);
            float currentValue;
            float referenceValue;
            found = TryGetPreviewBaselineStatValue(target, field, out currentValue, out referenceValue);
            if (!found)
                return 0f;

            float value = currentValue;
            ApplyPreviewAttributeEffects(ref value, skill, target, damageHits, field, referenceValue, resourceMode: false);
            return value;
        }

        private bool TryGetPreviewBaselineStatValue(Transform target, SkillStatField field, out float currentValue, out float referenceValue)
        {
            currentValue = 0f;
            referenceValue = 0f;
            target = ResolvePreviewStatTarget(target);
            bool preferBaseStats = !Application.isPlaying;
            bool hasPlayer = target != null && target.GetComponentInChildren<PlayerController>() != null;
            bool hasEnemy = target != null && target.GetComponentInChildren<EnemyController>() != null;

            bool found;
            if (preferBaseStats && (hasPlayer || hasEnemy))
                found = TryGetPreviewBaseStatValue(target, field, out currentValue, out referenceValue);
            else
                found = TryGetPreviewStatValue(target, field, out currentValue, out referenceValue);

            if (!found)
                found = TryGetPreviewStatValue(target, field, out currentValue, out referenceValue);
            if (!found)
                found = TryGetPreviewBaseStatValue(target, field, out currentValue, out referenceValue);
            return found;
        }

        private void ApplyPreviewAttributeEffects(ref float value, SharedSkillDefinition skill, Transform target, List<PreviewDamageHitResult> damageHits, SkillStatField field, float referenceValue, bool resourceMode)
        {
            if (skill == null || target == null)
                return;

            List<SkillAttributeEvent> attributeEvents = GetAttributeEventList(skill);
            if (attributeEvents != null)
            {
                for (int eventIndex = 0; eventIndex < attributeEvents.Count; eventIndex++)
                {
                    SkillAttributeEvent evt = attributeEvents[eventIndex];
                    if (evt?.attributeEffects == null)
                        continue;

                    if (evt.triggerMode != SkillEventTriggerMode.Repeated)
                    {
                        float triggerTime = evt.startTime;
                        if (triggerTime > _previewTime + 0.0001f)
                            continue;

                        for (int effectIndex = 0; effectIndex < evt.attributeEffects.Count; effectIndex++)
                        {
                            SkillAttributeEffect effect = evt.attributeEffects[effectIndex];
                            if (!DoesAttributeEffectApplyToTarget(effect, target, isOnHitEffect: false))
                                continue;

                            ApplyPreviewAttributeEffect(ref value, effect, field, referenceValue, triggerTime, resourceMode);
                        }
                    }
                    else
                    {
                        float interval = Mathf.Max(0.01f, evt.repeatInterval);
                        float endTime = GetPreviewRepeatedEndTime(evt);
                        for (float triggerTime = evt.startTime; triggerTime <= endTime + 0.0001f && triggerTime <= _previewTime + 0.0001f; triggerTime += interval)
                        {
                            for (int effectIndex = 0; effectIndex < evt.attributeEffects.Count; effectIndex++)
                            {
                                SkillAttributeEffect effect = evt.attributeEffects[effectIndex];
                                if (!DoesAttributeEffectApplyToTarget(effect, target, isOnHitEffect: false))
                                    continue;

                                ApplyPreviewAttributeEffect(ref value, effect, field, referenceValue, triggerTime, resourceMode);
                            }
                        }
                    }
                }
            }

            if (damageHits == null)
                return;

            for (int hitIndex = 0; hitIndex < damageHits.Count; hitIndex++)
            {
                PreviewDamageHitResult hit = damageHits[hitIndex];
                if (hit?.effect?.onHitAttributeEffects == null || hit.hitTargets == null || hit.hitTargets.Count <= 0)
                    continue;

                for (int effectIndex = 0; effectIndex < hit.effect.onHitAttributeEffects.Count; effectIndex++)
                {
                    SkillAttributeEffect effect = hit.effect.onHitAttributeEffects[effectIndex];
                    if (!DoesOnHitAttributeEffectApplyToTarget(effect, target, hit.hitTargets))
                        continue;

                    ApplyPreviewAttributeEffect(ref value, effect, field, referenceValue, hit.hitTime, resourceMode);
                }
            }
        }

        private void DrawPreviewControlsCompact()
        {
            GUILayout.Label("预览", EditorStyles.miniLabel, GUILayout.Width(28f));

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
                    StopAllTimelinePreviewAudio();
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

            GUILayout.Space(0f);
            GUILayout.Label($"{FormatTimelineTime(_previewTime)} / {FormatTimelineTime(GetPreviewTotalDuration())}", EditorStyles.miniLabel, GUILayout.Width(124f));
        }

        private void IteratePreviewTriggerTimes(SkillTimedEventBase evt, System.Action<float> action)
        {
            if (evt == null || action == null)
                return;

            if (evt.triggerMode != SkillEventTriggerMode.Repeated)
            {
                if (evt.startTime <= _previewTime + 0.0001f)
                    action(evt.startTime);
                return;
            }

            float interval = Mathf.Max(0.01f, evt.repeatInterval);
            float endTime = GetPreviewRepeatedEndTime(evt);
            for (float triggerTime = evt.startTime; triggerTime <= endTime + 0.0001f && triggerTime <= _previewTime + 0.0001f; triggerTime += interval)
                action(triggerTime);
        }

        private float GetPreviewRepeatedEndTime(SkillTimedEventBase evt)
        {
            if (evt == null)
                return 0f;

            if (evt.RepeatsUntilStateExit)
                return Mathf.Max(evt.startTime, GetPreviewStateExitTime());

            return evt.startTime + Mathf.Max(0f, evt.activeDuration);
        }

        private void ApplyPreviewAttributeEffect(ref float value, SkillAttributeEffect effect, SkillStatField field, float referenceValue, float triggerTime, bool resourceMode)
        {
            if (effect == null || effect.statField != field)
                return;

            if (triggerTime > _previewTime + 0.0001f)
                return;

            if (resourceMode)
            {
                value += GetPreviewAttributeDelta(effect, field, referenceValue);
                return;
            }

            if (effect.UsesDuration)
            {
                float effectLifetime = GetPreviewAttributeEffectLifetime(effect, triggerTime);
                if (effectLifetime > 0f && _previewTime > triggerTime + effectLifetime + 0.0001f)
                    return;
            }

            value += GetPreviewAttributeDelta(effect, field, referenceValue);
        }

        private bool TryGetPreviewBaseStatValue(Transform target, SkillStatField statField, out float currentValue, out float referenceValue)
        {
            currentValue = 0f;
            referenceValue = 0f;
            if (target == null)
                return false;

            target = ResolvePreviewStatTarget(target);
            EnsurePreviewReferenceDatabases();

            PlayerController player = target.GetComponentInChildren<PlayerController>();
            if (player != null)
            {
                var stats = new Stats();
                _previewLevelGrowth?.GetStatsForLevel(1, stats);
                switch (statField)
                {
                    case SkillStatField.HP:
                        currentValue = stats.MaxHp;
                        referenceValue = stats.MaxHp;
                        return true;
                    case SkillStatField.MP:
                        currentValue = stats.MaxMp;
                        referenceValue = stats.MaxMp;
                        return true;
                    case SkillStatField.Attack:
                        currentValue = stats.Attack;
                        referenceValue = stats.Attack;
                        return true;
                    case SkillStatField.Defense:
                        currentValue = stats.Defense;
                        referenceValue = stats.Defense;
                        return true;
                    case SkillStatField.MoveSpeed:
                        currentValue = stats.MoveSpeed;
                        referenceValue = stats.MoveSpeed;
                        return true;
                    case SkillStatField.HPRegen:
                        currentValue = stats.HpRegen;
                        referenceValue = stats.HpRegen;
                        return true;
                    case SkillStatField.MPRegen:
                        currentValue = stats.MpRegen;
                        referenceValue = stats.MpRegen;
                        return true;
                    case SkillStatField.CritRate:
                        currentValue = stats.CritRate;
                        referenceValue = stats.CritRate;
                        return true;
                    case SkillStatField.CritDamage:
                        currentValue = stats.CritDmg;
                        referenceValue = stats.CritDmg;
                        return true;
                    case SkillStatField.AttackSpeed:
                        currentValue = stats.AttackSpeed;
                        referenceValue = stats.AttackSpeed;
                        return true;
                    case SkillStatField.SkillDamage:
                        currentValue = stats.DamageBonus;
                        referenceValue = stats.DamageBonus;
                        return true;
                    case SkillStatField.DamageReduce:
                        currentValue = stats.DamageReduce;
                        referenceValue = stats.DamageReduce;
                        return true;
                    case SkillStatField.LifeSteal:
                        currentValue = stats.LifeSteal;
                        referenceValue = stats.LifeSteal;
                        return true;
                }
            }

            EnemyController enemy = target.GetComponentInChildren<EnemyController>();
            if (enemy != null && _previewEnemyStatsDb != null)
            {
                EnemyStatsEntry entry = _previewEnemyStatsDb.GetEntry(enemy.EnemyId) ?? _previewEnemyStatsDb.GetEntry(enemy.EnemyCategory);
                if (entry == null)
                    return false;

                switch (statField)
                {
                    case SkillStatField.HP:
                        currentValue = entry.baseHp;
                        referenceValue = entry.baseHp;
                        return true;
                    case SkillStatField.Attack:
                        currentValue = entry.baseAttack;
                        referenceValue = entry.baseAttack;
                        return true;
                    case SkillStatField.Defense:
                        currentValue = entry.baseDefense;
                        referenceValue = entry.baseDefense;
                        return true;
                    case SkillStatField.MoveSpeed:
                        currentValue = entry.baseMoveSpeed;
                        referenceValue = entry.baseMoveSpeed;
                        return true;
                }
            }

            return false;
        }

        private bool DoesAttributeEffectApplyToTarget(SkillAttributeEffect effect, Transform target, bool isOnHitEffect)
        {
            if (effect == null || target == null)
                return false;

            Transform selfTarget = _previewTarget != null ? ResolveRootPreviewTarget(_previewTarget)?.transform : null;
            if (effect.targetMode == SkillTargetMode.Self)
                return TargetsMatch(target, selfTarget);

            if (!isOnHitEffect)
                return false;

            Transform explicitVictim = ResolveRootPreviewTarget(_previewVictimTarget)?.transform;
            Transform hitTarget = explicitVictim ?? ResolvePrimaryPreviewHitTarget();
            return effect.targetMode == SkillTargetMode.DetectedTargets && TargetsMatch(target, hitTarget);
        }

        private bool DoesOnHitAttributeEffectApplyToTarget(SkillAttributeEffect effect, Transform target, List<Transform> hitTargets)
        {
            if (effect == null || target == null)
                return false;

            if (effect.targetMode == SkillTargetMode.Self)
                return TargetsMatch(target, _previewTarget != null ? ResolveRootPreviewTarget(_previewTarget)?.transform : null);

            if (effect.targetMode != SkillTargetMode.DetectedTargets || hitTargets == null)
                return false;

            for (int i = 0; i < hitTargets.Count; i++)
            {
                if (TargetsMatch(target, hitTargets[i]))
                    return true;
            }

            return false;
        }

        private static bool TargetsMatch(Transform left, Transform right)
        {
            if (left == null || right == null)
                return false;

            return left == right || left.root == right.root;
        }

        private static void DrawSceneTextLabel(Vector3 position, string text, GUIStyle style = null)
        {
            if (!ShowScenePreviewText || string.IsNullOrWhiteSpace(text))
                return;

            if (style != null)
                Handles.Label(position, text, style);
            else
                Handles.Label(position, text);
        }

        private static float GetPreviewAttributeDelta(SkillAttributeEffect effect, SkillStatField field, float referenceValue)
        {
            if (effect == null || effect.statField != field)
                return 0f;

            return effect.usePercent ? referenceValue * effect.magnitude : effect.magnitude;
        }

        private static string FormatValue(float value)
        {
            return value.ToString("0.##");
        }

        private static string FormatPercent(float value)
        {
            return (value * 100f).ToString("0.##") + "%";
        }

        private string BuildCurrentStatSummary(Transform target)
        {
            if (target == null)
                return string.Empty;

            PlayerController player = target.GetComponentInChildren<PlayerController>();
            if (player?.PlayerModel != null)
            {
                Stats stats = player.PlayerModel.Stats;
                return $"HP {player.PlayerModel.CurrentHp:0.#}/{stats.MaxHp:0.#}\nMP {player.PlayerModel.CurrentMp:0.#}/{stats.MaxMp:0.#}\n攻击 {stats.Attack:0.#}  防御 {stats.Defense:0.#}\n移速 {stats.MoveSpeed:0.##}";
            }

            EnemyController enemy = target.GetComponentInChildren<EnemyController>();
            if (enemy != null)
                return $"HP {enemy.CurrentHp:0.#}/{enemy.MaxHp:0.#}\n攻击 {enemy.Attack:0.#}  防御 {enemy.Defense:0.#}\n移速 {enemy.MoveSpeed:0.##}";

            return string.Empty;
        }

        private void DrawSkillValidationWarnings(SharedSkillDefinition skill)
        {
            List<string> warnings = CollectSkillWarnings(skill);
            if (warnings.Count == 0)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("技能校验", EditorStyles.boldLabel);
            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }

        private void DrawPreviewStateInspector(SharedSkillDefinition skill)
        {
            if (skill == null)
                return;

            List<PreviewOverlayLine> lines = BuildActivePreviewOverlayLines(skill);
            AppendPreviewTargetStatLines(lines);
            AppendPreviewBaseStatLines(lines, skill);
            if (lines.Count == 0)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("当前预览反馈", EditorStyles.boldLabel);
            for (int i = 0; i < lines.Count; i++)
                EditorGUILayout.HelpBox(lines[i].text, MessageType.None);
        }

        private void DrawSelectedEventInspector()
        {
            SkillTimedEventBase timedEvent = GetSelectedTimedEvent();
            if (timedEvent == null)
                return;

            SkillTimelineEvent view = CreateLegacyEventView(timedEvent, _selectedEventTrackType);
            if (view == null)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("选中事件", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("事件摘要", GetEventSummary(view), EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            string newEventId = EditorGUILayout.TextField("事件标识 ID", timedEvent.eventId ?? string.Empty);
            float newStartTime = Mathf.Max(0f, EditorGUILayout.FloatField("触发时间(秒)", timedEvent.startTime));
            EditorGUILayout.LabelField("触发时间(帧)", FormatFrameOnly(newStartTime), EditorStyles.miniLabel);
            SkillEventTriggerMode newTriggerMode = (SkillEventTriggerMode)EditorGUILayout.EnumPopup("触发模式", timedEvent.triggerMode);
            SkillEventActiveDurationMode newActiveDurationMode = timedEvent.activeDurationMode;
            float newActiveDuration = timedEvent.activeDuration;
            float newRepeatInterval = timedEvent.repeatInterval;
            if (newTriggerMode == SkillEventTriggerMode.Repeated)
            {
                newActiveDurationMode = (SkillEventActiveDurationMode)EditorGUILayout.EnumPopup("持续方式", timedEvent.activeDurationMode);
                if (newActiveDurationMode == SkillEventActiveDurationMode.FixedTime)
                {
                    newActiveDuration = Mathf.Max(0f, EditorGUILayout.FloatField("持续触发时长(秒)", timedEvent.activeDuration));
                    EditorGUILayout.LabelField("持续触发时长(帧)", FormatFrameOnly(newActiveDuration), EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox("将持续重复触发，直到当前状态退出。", MessageType.None);
                }
                newRepeatInterval = Mathf.Max(0.01f, EditorGUILayout.FloatField("重复触发间隔(秒)", timedEvent.repeatInterval));
                EditorGUILayout.LabelField("重复触发间隔(帧)", FormatFrameOnly(newRepeatInterval), EditorStyles.miniLabel);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_database, "Edit Skill Event");
                timedEvent.eventId = newEventId;
                timedEvent.startTime = SnapTime(newStartTime);
                timedEvent.triggerMode = newTriggerMode;
                timedEvent.activeDurationMode = newTriggerMode == SkillEventTriggerMode.Repeated ? newActiveDurationMode : SkillEventActiveDurationMode.FixedTime;
                timedEvent.activeDuration = newTriggerMode == SkillEventTriggerMode.Repeated && newActiveDurationMode == SkillEventActiveDurationMode.FixedTime
                    ? SnapDuration(newActiveDuration)
                    : 0f;
                timedEvent.repeatInterval = newTriggerMode == SkillEventTriggerMode.Repeated ? SnapDuration(newRepeatInterval) : 0.1f;
                MarkDatabaseDirty();
                view = CreateLegacyEventView(timedEvent, _selectedEventTrackType);
            }

            DrawEventValidationWarnings(view, timedEvent);

            if (_selectedEventTrackType == TimelineTrackType.Damage)
                DrawDamageEffectsEditor(view, timedEvent as SkillDamageEvent);

            if (_selectedEventTrackType == TimelineTrackType.Physics)
                DrawPhysicsEffectsEditor(view);

            if (_selectedEventTrackType == TimelineTrackType.Attribute)
                DrawAttributeEffectsEditor(view);

            if (_selectedEventTrackType == TimelineTrackType.Vfx)
                DrawVfxEffectsEditor(view);

            if (_selectedEventTrackType == TimelineTrackType.Sfx)
                DrawSfxEffectsEditor(view);
        }

        private void DrawDamageEffectsEditor(SkillTimelineEvent evt, SkillDamageEvent damageEvent)
        {
            EnsureDamageEffects(evt);

            if (damageEvent != null && damageEvent.hitStopSettingsOwnedByEvent)
            {
                Undo.RecordObject(_database, "Migrate Damage Event Hit Stop");
                if (damageEvent.TryMigrateLegacyHitStopSettings())
                    MarkDatabaseDirty();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("命中效果", EditorStyles.boldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Hit Effect");
                SkillDamageEffect newEffect = new SkillDamageEffect();
                newEffect.TryMigrateLegacySubEffects();
                evt.damageEffects.Add(newEffect);
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < evt.damageEffects.Count; i++)
            {
                SkillDamageEffect effect = evt.damageEffects[i] ?? (evt.damageEffects[i] = new SkillDamageEffect());
                effect.TryMigrateLegacySubEffects();
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                string effectSummary = GetDamageDetectionDisplayName(effect);
                string companionSummary = JoinVfxEffectNames(effect.companionVfxEffects);
                if (!string.IsNullOrWhiteSpace(companionSummary))
                    effectSummary = $"{effectSummary} / 伴随特效:{companionSummary}";
                string onHitSummary = BuildOnHitEffectSummary(effect);
                if (!string.IsNullOrWhiteSpace(onHitSummary))
                    effectSummary = $"{effectSummary} / {onHitSummary}";
                bool isExpanded = DrawEffectFoldout(
                    GetDamageEffectFoldoutKey(_selectedEventIndex, i),
                    BuildIndexedFoldoutLabel("命中效果", i + 1, effectSummary));
                bool isEditingDamage = _selectedEventTrackType == TimelineTrackType.Damage
                    && _activeSceneDamageEventIndex == _selectedEventIndex
                    && _activeSceneDamageIndex == i;
                if (GUILayout.Button(isEditingDamage ? "编辑中" : "场景编辑", GUILayout.Width(64f)))
                {
                    bool same = isEditingDamage;
                    _activeSceneDamageEventIndex = same ? -1 : _selectedEventIndex;
                    _activeSceneDamageIndex = same ? -1 : i;
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    float newDetectionDuration = Mathf.Max(0f, EditorGUILayout.FloatField("检测时长(秒)", effect.detectionDuration));
                    EditorGUILayout.LabelField("检测时长(帧)", FormatFrameOnly(newDetectionDuration), EditorStyles.miniLabel);
                    DamageDetectionType newDetectionType = (DamageDetectionType)EditorGUILayout.EnumPopup("检测方式", effect.detectionType);
                    string newHitLayerName = EditorGUILayout.TextField("命中层级名", effect.hitLayerName ?? string.Empty);
                    AttackShapeType newShape = effect.shape;
                    Vector3 newCenterOffset = effect.centerOffset;
                    Vector3 newRotationEuler = effect.rotationEuler;
                    float newSphereRadius = effect.sphereRadius;
                    float newSectorAngle = effect.sectorAngle;
                    Vector3 newBoxSize = effect.boxSize;
                    string newColliderNodeName = effect.colliderNodeName ?? string.Empty;
                    Vector3 newRayOriginOffset = effect.rayOriginOffset;
                    float newRayMaxDistance = effect.rayMaxDistance;
                    float newRayRadius = effect.rayRadius;
                    SkillMotionSettings newMotion = CopyMotionSettings(effect.motion);

                    switch (newDetectionType)
                    {
                        case DamageDetectionType.RangeOverlap:
                            newShape = (AttackShapeType)EditorGUILayout.EnumPopup("范围形状", effect.shape);
                            newCenterOffset = EditorGUILayout.Vector3Field("中心偏移", effect.centerOffset);
                            if (newShape == AttackShapeType.Sector || newShape == AttackShapeType.Box)
                                newRotationEuler = EditorGUILayout.Vector3Field("旋转偏移", effect.rotationEuler);
                            if (newShape == AttackShapeType.Sphere || newShape == AttackShapeType.Sector)
                                newSphereRadius = Mathf.Max(0.01f, EditorGUILayout.FloatField("球体半径", effect.sphereRadius));
                            if (newShape == AttackShapeType.Sector)
                                newSectorAngle = Mathf.Clamp(EditorGUILayout.FloatField("扇形角度", effect.sectorAngle), 1f, 360f);
                            if (newShape == AttackShapeType.Box)
                                newBoxSize = ClampVector3(EditorGUILayout.Vector3Field("盒体尺寸", effect.boxSize), 0.01f);
                            break;

                        case DamageDetectionType.Collision:
                            newColliderNodeName = EditorGUILayout.TextField("武器命中盒对象名", effect.colliderNodeName ?? string.Empty);
                            break;

                        case DamageDetectionType.Raycast:
                            newRayOriginOffset = EditorGUILayout.Vector3Field("射线起点偏移", effect.rayOriginOffset);
                            newRotationEuler = EditorGUILayout.Vector3Field("旋转偏移", effect.rotationEuler);
                            newRayMaxDistance = Mathf.Max(0.01f, EditorGUILayout.FloatField("射线最大距离", effect.rayMaxDistance));
                            newRayRadius = Mathf.Max(0f, EditorGUILayout.FloatField("射线半径", effect.rayRadius));
                            break;
                    }

                    if (newDetectionType != DamageDetectionType.Collision)
                        DrawMotionSettingsEditor("移动设置", newMotion);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Hit Effect");
                        effect.detectionDuration = SnapDuration(newDetectionDuration);
                        effect.detectionType = newDetectionType;
                        effect.hitLayerName = newHitLayerName;
                        effect.shape = newShape;
                        effect.centerOffset = newCenterOffset;
                        effect.rotationEuler = newRotationEuler;
                        effect.sphereRadius = Mathf.Max(0.01f, newSphereRadius);
                        effect.sectorAngle = Mathf.Clamp(newSectorAngle, 1f, 360f);
                        effect.boxSize = ClampVector3(newBoxSize, 0.01f);
                        effect.colliderNodeName = newColliderNodeName;
                        effect.rayOriginOffset = newRayOriginOffset;
                        effect.rayMaxDistance = Mathf.Max(0.01f, newRayMaxDistance);
                        effect.rayRadius = Mathf.Max(0f, newRayRadius);
                        effect.motion = newMotion;
                        MarkDatabaseDirty();
                    }

                    DrawHitStopEffectEditor(effect, i);
                    DrawDamageCompanionVfxEditor(effect, i);
                    DrawNestedHitDamageEffectsEditor(effect, i);
                    DrawNestedVfxEffectsEditor(effect, i);
                    DrawNestedSfxEffectsEditor(effect, i);
                    DrawNestedPhysicsEffectsEditor(effect, i);
                    DrawNestedAttributeEffectsEditor(effect, i);
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Damage Effect");
                evt.damageEffects.RemoveAt(removeIndex);
                if (_selectedEventTrackType == TimelineTrackType.Damage
                    && _activeSceneDamageEventIndex == _selectedEventIndex)
                {
                    if (_activeSceneDamageIndex == removeIndex)
                    {
                        _activeSceneDamageEventIndex = -1;
                        _activeSceneDamageIndex = -1;
                    }
                    else if (_activeSceneDamageIndex > removeIndex)
                    {
                        _activeSceneDamageIndex--;
                    }
                }
                if (_selectedEventTrackType == TimelineTrackType.Damage
                    && _activeSceneHitVfxEventIndex == _selectedEventIndex)
                {
                    if (_activeSceneHitVfxDamageIndex == removeIndex)
                    {
                        _activeSceneHitVfxEventIndex = -1;
                        _activeSceneHitVfxDamageIndex = -1;
                        _activeSceneHitVfxIndex = -1;
                        DestroyScenePreviewCueInstance();
                    }
                    else if (_activeSceneHitVfxDamageIndex > removeIndex)
                    {
                        _activeSceneHitVfxDamageIndex--;
                    }
                }
                if (_selectedEventTrackType == TimelineTrackType.Damage
                    && _activeSceneCompanionVfxEventIndex == _selectedEventIndex)
                {
                    if (_activeSceneCompanionVfxDamageIndex == removeIndex)
                    {
                        _activeSceneCompanionVfxEventIndex = -1;
                        _activeSceneCompanionVfxDamageIndex = -1;
                        _activeSceneCompanionVfxIndex = -1;
                        DestroyScenePreviewCueInstance();
                    }
                    else if (_activeSceneCompanionVfxDamageIndex > removeIndex)
                    {
                        _activeSceneCompanionVfxDamageIndex--;
                    }
                }
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
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
                evt.physicsEffects.Add(new SkillPhysicsEffect { effectType = PhysicsEffectType.DashSelf });
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < evt.physicsEffects.Count; i++)
            {
                SkillPhysicsEffect effect = evt.physicsEffects[i] ?? (evt.physicsEffects[i] = new SkillPhysicsEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetTopLevelPhysicsFoldoutKey(_selectedEventIndex, i),
                    BuildIndexedFoldoutLabel("物理效果", i + 1, GetPhysicsEffectDisplayName(effect.effectType)));
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    PhysicsEffectType newEffectType = DrawPhysicsEffectTypePopup(effect.effectType, false);
                    float newDistance = effect.distance;
                    float newHeight = effect.height;
                    if (UsesPhysicsDistance(newEffectType))
                        newDistance = Mathf.Max(0f, EditorGUILayout.FloatField("距离", effect.distance));
                    if (UsesPhysicsHeight(newEffectType))
                        newHeight = Mathf.Max(0f, EditorGUILayout.FloatField("高度", effect.height));
                    SkillEffectDurationMode newDurationMode = SupportsTopLevelUntilStateExit(newEffectType)
                        ? (SkillEffectDurationMode)EditorGUILayout.EnumPopup("持续方式", effect.durationMode)
                        : SkillEffectDurationMode.FixedTime;
                    float newDuration = effect.duration;
                    if (!SupportsTopLevelUntilStateExit(newEffectType) || newDurationMode == SkillEffectDurationMode.FixedTime)
                        newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("持续时长(秒)", effect.duration));
                    else
                        EditorGUILayout.HelpBox("该效果会持续到当前状态退出。", MessageType.None);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Physics Effect");
                        effect.effectType = newEffectType;
                        effect.distance = newDistance;
                        effect.height = newHeight;
                        effect.durationMode = newDurationMode;
                        effect.duration = newDuration;
                        MarkDatabaseDirty();
                    }
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
                bool isExpanded = DrawEffectFoldout(
                    GetTopLevelAttributeFoldoutKey(_selectedEventIndex, i),
                    BuildIndexedFoldoutLabel("属性效果", i + 1, GetAttributeEffectSummary(effect)));
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    SkillStatField newStatField = (SkillStatField)EditorGUILayout.EnumPopup("属性字段", effect.statField);
                    SkillTargetMode newTargetMode = SkillTargetMode.Self;
                    float newMagnitude = EditorGUILayout.FloatField("数值", effect.magnitude);
                    bool newUsePercent = EditorGUILayout.Toggle("按比例计算", effect.usePercent);
                    float newDuration = effect.duration;
                    bool showDuration = newStatField != SkillStatField.HP && newStatField != SkillStatField.MP;
                    SkillEffectDurationMode newDurationMode = showDuration
                        ? (SkillEffectDurationMode)EditorGUILayout.EnumPopup("持续方式", effect.durationMode)
                        : SkillEffectDurationMode.FixedTime;
                    if (showDuration)
                    {
                        if (newDurationMode == SkillEffectDurationMode.FixedTime)
                            newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("Buff 持续时长(秒)", effect.duration));
                        else
                            EditorGUILayout.HelpBox("该 Buff 会持续到当前状态退出。", MessageType.None);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Attribute Effect");
                        effect.statField = newStatField;
                        effect.targetMode = newTargetMode;
                        effect.magnitude = newMagnitude;
                        effect.usePercent = newUsePercent;
                        effect.durationMode = newDurationMode;
                        effect.duration = showDuration ? newDuration : 0f;
                        MarkDatabaseDirty();
                    }
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

        private void DrawVfxEffectsEditor(SkillTimelineEvent evt)
        {
            EnsureVfxEffects(evt);

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("特效效果", EditorStyles.boldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add VFX Effect");
                evt.vfxEffects.Add(new SkillVfxEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < evt.vfxEffects.Count; i++)
            {
                SkillVfxEffect cue = evt.vfxEffects[i] ?? (evt.vfxEffects[i] = new SkillVfxEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetTopLevelVfxFoldoutKey(_selectedEventIndex, i),
                    BuildIndexedFoldoutLabel("特效效果", i + 1, GetVfxEffectSummary(cue)));
                string hiddenKey = GetEditorCueVisibilityKey(_selectedEventIndex, i);
                bool visible = !_hiddenPreviewCueKeys.Contains(hiddenKey);
                bool newVisible = EditorGUILayout.ToggleLeft("显示", visible, GUILayout.Width(50f));
                if (newVisible != visible)
                {
                    if (newVisible)
                        _hiddenPreviewCueKeys.Remove(hiddenKey);
                    else
                        _hiddenPreviewCueKeys.Add(hiddenKey);

                    if (!newVisible
                        && _activeSceneCueEventIndex == _selectedEventIndex
                        && _activeSceneCueIndex == i)
                    {
                        _activeSceneCueEventIndex = -1;
                        _activeSceneCueIndex = -1;
                        DestroyScenePreviewCueInstance();
                    }

                    Repaint();
                    SceneView.RepaintAll();
                }
                bool isEditingTopLevelCue = _activeSceneCueEventIndex == _selectedEventIndex
                    && _activeSceneCueIndex == i;
                if (GUILayout.Button(isEditingTopLevelCue ? "编辑中" : "场景编辑", GUILayout.Width(64f)))
                {
                    _hiddenPreviewCueKeys.Remove(hiddenKey);
                    bool same = isEditingTopLevelCue;
                    _activeSceneCueEventIndex = same ? -1 : _selectedEventIndex;
                    _activeSceneCueIndex = same ? -1 : i;
                    if (!same)
                        UpdateScenePreviewCueInstance();
                    else
                        DestroyScenePreviewCueInstance();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    GameObject newParticlePrefab = (GameObject)EditorGUILayout.ObjectField("粒子特效 Prefab", cue.particlePrefab, typeof(GameObject), false);
                    CueAnchor newAnchor = DrawCueAnchorPopup("挂点", cue.anchor, TopLevelCueAnchors, TopLevelCueAnchorLabels);
                    Vector3 newOffset = EditorGUILayout.Vector3Field("位置偏移", cue.offset);
                    Vector3 newRotationEuler = EditorGUILayout.Vector3Field("旋转偏移", cue.rotationEuler);
                    Vector3 newScale = EditorGUILayout.Vector3Field("缩放", cue.scale);
                    SkillMotionSettings newMotion = CopyMotionSettings(cue.motion);
                    if (ShouldShowCueMotionSettings(newAnchor))
                        DrawMotionSettingsEditor("移动设置", newMotion);
                    SkillCueDestroyMode newDestroyMode = (SkillCueDestroyMode)EditorGUILayout.EnumPopup("销毁方式", cue.destroyMode);
                    float newDuration = cue.duration;
                    if (newDestroyMode == SkillCueDestroyMode.Timed)
                        newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("销毁时间(秒)", cue.duration));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit VFX Effect");
                        cue.particlePrefab = newParticlePrefab;
                        cue.anchor = newAnchor;
                        cue.offset = newOffset;
                        cue.rotationEuler = newRotationEuler;
                        cue.scale = new Vector3(
                            Mathf.Max(0.01f, newScale.x),
                            Mathf.Max(0.01f, newScale.y),
                            Mathf.Max(0.01f, newScale.z));
                        cue.motion = ShouldShowCueMotionSettings(newAnchor) ? newMotion : new SkillMotionSettings();
                        cue.destroyMode = newDestroyMode;
                        cue.duration = newDestroyMode == SkillCueDestroyMode.Timed ? newDuration : 0f;
                        MarkDatabaseDirty();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove VFX Effect");
                evt.vfxEffects.RemoveAt(removeIndex);
                if (_selectedEventTrackType == TimelineTrackType.Vfx
                    && _activeSceneCueEventIndex == _selectedEventIndex
                    && _activeSceneCueIndex == removeIndex)
                {
                    _activeSceneCueEventIndex = -1;
                    _activeSceneCueIndex = -1;
                    DestroyScenePreviewCueInstance();
                }
                else if (_selectedEventTrackType == TimelineTrackType.Vfx
                    && _activeSceneCueEventIndex == _selectedEventIndex
                    && _activeSceneCueIndex > removeIndex)
                {
                    _activeSceneCueIndex--;
                }
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSfxEffectsEditor(SkillTimelineEvent evt)
        {
            EnsureSfxEffects(evt);

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("音效效果", EditorStyles.boldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add SFX Effect");
                evt.sfxEffects.Add(new SkillSfxEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < evt.sfxEffects.Count; i++)
            {
                SkillSfxEffect effect = evt.sfxEffects[i] ?? (evt.sfxEffects[i] = new SkillSfxEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetTopLevelSfxFoldoutKey(_selectedEventIndex, i),
                    BuildIndexedFoldoutLabel("音效效果", i + 1, GetSfxEffectSummary(effect)));
                string hiddenKey = GetEditorSfxVisibilityKey(_selectedEventIndex, i);
                bool visible = !_hiddenPreviewCueKeys.Contains(hiddenKey);
                bool newVisible = EditorGUILayout.ToggleLeft("显示", visible, GUILayout.Width(50f));
                if (newVisible != visible)
                {
                    if (newVisible)
                        _hiddenPreviewCueKeys.Remove(hiddenKey);
                    else
                        _hiddenPreviewCueKeys.Add(hiddenKey);

                    Repaint();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    AudioClip newAudioClip = (AudioClip)EditorGUILayout.ObjectField("音效片段", effect.audioClip, typeof(AudioClip), false);
                    CueAnchor newAnchor = DrawCueAnchorPopup("挂点", effect.anchor, TopLevelCueAnchors, TopLevelCueAnchorLabels);
                    Vector3 newOffset = EditorGUILayout.Vector3Field("位置偏移", effect.offset);
                    bool newLoop = EditorGUILayout.Toggle("循环播放", effect.loop);
                    SkillCueDestroyMode newDestroyMode = (SkillCueDestroyMode)EditorGUILayout.EnumPopup("销毁方式", effect.destroyMode);
                    float newDuration = effect.duration;
                    if (newDestroyMode == SkillCueDestroyMode.Timed)
                        newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("销毁时间(秒)", effect.duration));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit SFX Effect");
                        effect.audioClip = newAudioClip;
                        effect.anchor = newAnchor;
                        effect.offset = newOffset;
                        effect.loop = newLoop;
                        effect.destroyMode = newDestroyMode;
                        effect.duration = newDestroyMode == SkillCueDestroyMode.Timed ? newDuration : 0f;
                        MarkDatabaseDirty();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove SFX Effect");
                evt.sfxEffects.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private static void EnsureDamageEffects(SkillTimelineEvent evt)
        {
            if (evt.damageEffects == null)
                evt.damageEffects = new List<SkillDamageEffect>();
        }

        private static void EnsurePhysicsEffects(SkillTimelineEvent evt)
        {
            if (evt.physicsEffects == null)
                evt.physicsEffects = new List<SkillPhysicsEffect>();
        }

        private static void EnsureHitDamageEffects(SkillDamageEffect effect)
        {
            effect?.TryMigrateLegacySubEffects();
            if (effect != null && effect.onHitDamageEffects == null)
                effect.onHitDamageEffects = new List<SkillHitDamageEffect>();
        }

        private static void EnsureHitStopEffect(SkillDamageEffect effect)
        {
            effect?.TryMigrateLegacySubEffects();
            if (effect != null && effect.onHitStopEffect == null)
                effect.onHitStopEffect = new SkillHitStopEffect();
        }

        private static void EnsurePhysicsEffects(SkillDamageEffect effect)
        {
            if (effect.onHitPhysicsEffects == null)
                effect.onHitPhysicsEffects = new List<SkillPhysicsEffect>();
        }

        private static void EnsureAttributeEffects(SkillTimelineEvent evt)
        {
            if (evt.attributeEffects == null)
                evt.attributeEffects = new List<SkillAttributeEffect>();
        }

        private static void EnsureAttributeEffects(SkillDamageEffect effect)
        {
            if (effect.onHitAttributeEffects == null)
                effect.onHitAttributeEffects = new List<SkillAttributeEffect>();
        }

        private static void EnsureVfxEffects(SkillTimelineEvent evt)
        {
            if (evt.vfxEffects == null)
                evt.vfxEffects = new List<SkillVfxEffect>();
        }

        private static void EnsureVfxEffects(SkillDamageEffect effect)
        {
            if (effect.onHitVfxEffects == null)
                effect.onHitVfxEffects = new List<SkillVfxEffect>();
        }

        private static void EnsureSfxEffects(SkillTimelineEvent evt)
        {
            if (evt.sfxEffects == null)
                evt.sfxEffects = new List<SkillSfxEffect>();
        }

        private static void EnsureSfxEffects(SkillDamageEffect effect)
        {
            if (effect.onHitSfxEffects == null)
                effect.onHitSfxEffects = new List<SkillSfxEffect>();
        }

        private void DrawNestedHitDamageEffectsEditor(SkillDamageEffect damageEffect, int damageIndex)
        {
            EnsureHitDamageEffects(damageEffect);
            DrawNestedHitDamageEffectsList("命中伤害效果", damageEffect.onHitDamageEffects, damageIndex);
        }

        private void DrawNestedHitDamageEffectsList(string title, List<SkillHitDamageEffect> effects, int damageIndex)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Nested Hit Damage Effect");
                effects.Add(new SkillHitDamageEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillHitDamageEffect effect = effects[i] ?? (effects[i] = new SkillHitDamageEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetNestedHitDamageFoldoutKey(_selectedEventIndex, damageIndex, i),
                    BuildIndexedFoldoutLabel("命中伤害效果", i + 1, GetHitDamageEffectSummary(effect)));
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    float newDamageMagnitude = Mathf.Max(0f, EditorGUILayout.FloatField("伤害倍率", effect.damageMagnitude));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Nested Hit Damage Effect");
                        effect.damageMagnitude = newDamageMagnitude;
                        MarkDatabaseDirty();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Nested Hit Damage Effect");
                effects.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHitStopEffectEditor(SkillDamageEffect damageEffect, int damageIndex)
        {
            EnsureHitStopEffect(damageEffect);
            SkillHitStopEffect effect = damageEffect?.onHitStopEffect;
            if (effect == null)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("命中停顿效果", EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();
            float newHitStopDuration = Mathf.Max(0f, EditorGUILayout.FloatField("命中停顿时长(秒)", effect.hitStopDuration));
            float newHitStopTimeScale = Mathf.Clamp01(EditorGUILayout.Slider("命中停顿速度", effect.hitStopTimeScale, 0f, 1f));
            bool newPauseCameraLookDuringHitStop = EditorGUILayout.Toggle("同时停顿镜头转向", effect.pauseCameraLookDuringHitStop);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_database, "Edit Hit Stop Effect");
                effect.hitStopDuration = newHitStopDuration;
                effect.hitStopTimeScale = newHitStopTimeScale;
                effect.pauseCameraLookDuringHitStop = newPauseCameraLookDuringHitStop;
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNestedPhysicsEffectsEditor(SkillDamageEffect damageEffect, int damageIndex)
        {
            EnsurePhysicsEffects(damageEffect);
            DrawNestedPhysicsEffectsList("命中物理效果", damageEffect.onHitPhysicsEffects, damageIndex);
        }

        private void DrawNestedPhysicsEffectsList(string title, List<SkillPhysicsEffect> effects, int damageIndex)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Nested Physics Effect");
                effects.Add(new SkillPhysicsEffect { effectType = PhysicsEffectType.Knockback });
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillPhysicsEffect effect = effects[i] ?? (effects[i] = new SkillPhysicsEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetNestedPhysicsFoldoutKey(_selectedEventIndex, damageIndex, i),
                    BuildIndexedFoldoutLabel("命中物理效果", i + 1, GetPhysicsEffectDisplayName(effect.effectType)));
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    PhysicsEffectType newEffectType = DrawPhysicsEffectTypePopup(effect.effectType, true);
                    float newDistance = effect.distance;
                    float newHeight = effect.height;
                    if (UsesPhysicsDistance(newEffectType))
                        newDistance = Mathf.Max(0f, EditorGUILayout.FloatField("距离", effect.distance));
                    if (UsesPhysicsHeight(newEffectType))
                        newHeight = Mathf.Max(0f, EditorGUILayout.FloatField("高度", effect.height));
                    float newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("持续时长(秒)", effect.duration));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Nested Physics Effect");
                        effect.effectType = newEffectType;
                        effect.distance = newDistance;
                        effect.height = newHeight;
                        effect.duration = newDuration;
                        MarkDatabaseDirty();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Nested Physics Effect");
                effects.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private static PhysicsEffectType DrawPhysicsEffectTypePopup(PhysicsEffectType currentType, bool isOnHit)
        {
            PhysicsEffectType[] allowedTypes = isOnHit
                ? new[] { PhysicsEffectType.Knockback, PhysicsEffectType.Pull, PhysicsEffectType.Launch, PhysicsEffectType.Stun }
                : new[] { PhysicsEffectType.DashSelf, PhysicsEffectType.Airborne, PhysicsEffectType.SuperArmor, PhysicsEffectType.Invincible };
            string[] labels = isOnHit
                ? new[] { "击退", "拉拽", "击飞", "眩晕" }
                : new[] { "位移", "腾空", "霸体", "无敌" };

            int selectedIndex = 0;
            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (allowedTypes[i] == currentType)
                {
                    selectedIndex = i;
                    break;
                }
            }

            selectedIndex = EditorGUILayout.Popup("效果类型", selectedIndex, labels);
            return allowedTypes[Mathf.Clamp(selectedIndex, 0, allowedTypes.Length - 1)];
        }

        private static bool UsesPhysicsDistance(PhysicsEffectType effectType)
        {
            return effectType == PhysicsEffectType.Knockback
                || effectType == PhysicsEffectType.Pull
                || effectType == PhysicsEffectType.Launch
                || effectType == PhysicsEffectType.DashSelf;
        }

        private static bool UsesPhysicsHeight(PhysicsEffectType effectType)
        {
            return effectType == PhysicsEffectType.Launch
                || effectType == PhysicsEffectType.Airborne;
        }

        private static bool SupportsTopLevelUntilStateExit(PhysicsEffectType effectType)
        {
            return effectType == PhysicsEffectType.SuperArmor
                || effectType == PhysicsEffectType.Invincible;
        }

        private static SkillMotionSettings CopyMotionSettings(SkillMotionSettings source)
        {
            if (source == null)
                return new SkillMotionSettings();

            return new SkillMotionSettings
            {
                enabled = source.enabled,
                speed = source.speed,
                direction = source.direction,
            };
        }

        private static void DrawMotionSettingsEditor(string title, SkillMotionSettings motion)
        {
            if (motion == null)
                return;

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            motion.enabled = EditorGUILayout.Toggle("随时间移动", motion.enabled);
            if (motion.enabled)
            {
                motion.speed = Mathf.Max(0f, EditorGUILayout.FloatField("移动速度(米/秒)", motion.speed));
                motion.direction = EditorGUILayout.Vector3Field("移动方向", motion.direction);
            }
            EditorGUILayout.EndVertical();
        }

        private static bool ShouldShowCueMotionSettings(CueAnchor anchor)
        {
            return anchor == CueAnchor.World;
        }

        private void DrawNestedAttributeEffectsEditor(SkillDamageEffect damageEffect, int damageIndex)
        {
            EnsureAttributeEffects(damageEffect);
            DrawNestedAttributeEffectsList("命中属性效果", damageEffect.onHitAttributeEffects, damageIndex);
        }

        private void DrawNestedAttributeEffectsList(string title, List<SkillAttributeEffect> effects, int damageIndex)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Nested Attribute Effect");
                effects.Add(new SkillAttributeEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillAttributeEffect effect = effects[i] ?? (effects[i] = new SkillAttributeEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetNestedAttributeFoldoutKey(_selectedEventIndex, damageIndex, i),
                    BuildIndexedFoldoutLabel("命中属性效果", i + 1, GetAttributeEffectSummary(effect)));
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    SkillStatField newStatField = (SkillStatField)EditorGUILayout.EnumPopup("属性字段", effect.statField);
                    int targetModeIndex = effect.targetMode == SkillTargetMode.DetectedTargets ? 1 : 0;
                    targetModeIndex = EditorGUILayout.Popup("作用目标", targetModeIndex, new[] { "自身", "命中目标" });
                    SkillTargetMode newTargetMode = targetModeIndex == 1 ? SkillTargetMode.DetectedTargets : SkillTargetMode.Self;
                    float newMagnitude = EditorGUILayout.FloatField("数值", effect.magnitude);
                    bool newUsePercent = EditorGUILayout.Toggle("按比例计算", effect.usePercent);
                    float newDuration = effect.duration;
                    bool showDuration = newStatField != SkillStatField.HP && newStatField != SkillStatField.MP;
                    if (showDuration)
                        newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("Buff 持续时长(秒)", effect.duration));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Nested Attribute Effect");
                        effect.statField = newStatField;
                        effect.targetMode = newTargetMode;
                        effect.magnitude = newMagnitude;
                        effect.usePercent = newUsePercent;
                        effect.duration = showDuration ? newDuration : 0f;
                        MarkDatabaseDirty();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Nested Attribute Effect");
                effects.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNestedVfxEffectsEditor(SkillDamageEffect damageEffect, int damageIndex)
        {
            EnsureVfxEffects(damageEffect);
            DrawNestedVfxEffectsList("命中特效效果", damageEffect.onHitVfxEffects, damageIndex);
        }

        private void DrawDamageCompanionVfxEditor(SkillDamageEffect damageEffect, int damageIndex)
        {
            if (damageEffect == null || damageEffect.detectionType == DamageDetectionType.Collision)
                return;

            EnsureCompanionVfxEffects(damageEffect);

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("伴随特效效果", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Damage Companion VFX");
                damageEffect.companionVfxEffects.Add(new SkillVfxEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < damageEffect.companionVfxEffects.Count; i++)
            {
                SkillVfxEffect effect = damageEffect.companionVfxEffects[i] ?? (damageEffect.companionVfxEffects[i] = new SkillVfxEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetCompanionVfxFoldoutKey(_selectedEventIndex, damageIndex, i),
                    BuildIndexedFoldoutLabel("伴随特效效果", i + 1, GetVfxEffectSummary(effect)));
                string hiddenKey = GetEditorCompanionCueVisibilityKey(_selectedEventIndex, damageIndex, i);
                bool visible = !_hiddenPreviewCueKeys.Contains(hiddenKey);
                bool newVisible = EditorGUILayout.ToggleLeft("显示", visible, GUILayout.Width(50f));
                if (newVisible != visible)
                {
                    if (newVisible)
                        _hiddenPreviewCueKeys.Remove(hiddenKey);
                    else
                        _hiddenPreviewCueKeys.Add(hiddenKey);

                    bool isCurrentHidden = _activeSceneCompanionVfxEventIndex == _selectedEventIndex
                        && _activeSceneCompanionVfxDamageIndex == damageIndex
                        && _activeSceneCompanionVfxIndex == i;
                    if (!newVisible && isCurrentHidden)
                    {
                        _activeSceneCompanionVfxEventIndex = -1;
                        _activeSceneCompanionVfxDamageIndex = -1;
                        _activeSceneCompanionVfxIndex = -1;
                        DestroyScenePreviewCueInstance();
                    }

                    Repaint();
                    SceneView.RepaintAll();
                }

                bool isEditingCompanionVfx = _selectedEventTrackType == TimelineTrackType.Damage
                    && _activeSceneCompanionVfxEventIndex == _selectedEventIndex
                    && _activeSceneCompanionVfxDamageIndex == damageIndex
                    && _activeSceneCompanionVfxIndex == i;
                if (GUILayout.Button(isEditingCompanionVfx ? "编辑中" : "场景编辑", GUILayout.Width(64f)))
                {
                    _hiddenPreviewCueKeys.Remove(hiddenKey);
                    bool same = isEditingCompanionVfx;
                    _activeSceneCompanionVfxEventIndex = same ? -1 : _selectedEventIndex;
                    _activeSceneCompanionVfxDamageIndex = same ? -1 : damageIndex;
                    _activeSceneCompanionVfxIndex = same ? -1 : i;
                    if (!same)
                        UpdateScenePreviewCueInstance();
                    else
                        DestroyScenePreviewCueInstance();
                    Repaint();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    GameObject newParticlePrefab = (GameObject)EditorGUILayout.ObjectField("粒子特效 Prefab", effect.particlePrefab, typeof(GameObject), false);
                    Vector3 newOffset = EditorGUILayout.Vector3Field("位置偏移", effect.offset);
                    Vector3 newRotationEuler = EditorGUILayout.Vector3Field("旋转偏移", effect.rotationEuler);
                    Vector3 newScale = EditorGUILayout.Vector3Field("缩放", effect.scale);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Damage Companion VFX");
                        effect.particlePrefab = newParticlePrefab;
                        effect.anchor = CueAnchor.World;
                        effect.offset = newOffset;
                        effect.rotationEuler = newRotationEuler;
                        effect.scale = ClampVector3(newScale, 0.01f);
                        effect.motion = new SkillMotionSettings();
                        effect.destroyMode = SkillCueDestroyMode.NaturalDestroy;
                        effect.duration = 0f;
                        MarkDatabaseDirty();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Damage Companion VFX");
                damageEffect.companionVfxEffects.RemoveAt(removeIndex);
                if (_activeSceneCompanionVfxEventIndex == _selectedEventIndex
                    && _activeSceneCompanionVfxDamageIndex == damageIndex)
                {
                    if (_activeSceneCompanionVfxIndex == removeIndex)
                    {
                        _activeSceneCompanionVfxEventIndex = -1;
                        _activeSceneCompanionVfxDamageIndex = -1;
                        _activeSceneCompanionVfxIndex = -1;
                        DestroyScenePreviewCueInstance();
                    }
                    else if (_activeSceneCompanionVfxIndex > removeIndex)
                    {
                        _activeSceneCompanionVfxIndex--;
                    }
                }
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private static void EnsureCompanionVfxEffects(SkillDamageEffect damageEffect)
        {
            if (damageEffect == null)
                return;

            damageEffect.TryMigrateLegacySubEffects();
            if (damageEffect.companionVfxEffects == null)
                damageEffect.companionVfxEffects = new List<SkillVfxEffect>();
        }

        private void DrawNestedVfxEffectsList(string title, List<SkillVfxEffect> effects, int damageIndex)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Nested VFX Effect");
                effects.Add(new SkillVfxEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillVfxEffect effect = effects[i] ?? (effects[i] = new SkillVfxEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetNestedVfxFoldoutKey(_selectedEventIndex, damageIndex, i),
                    BuildIndexedFoldoutLabel("命中特效效果", i + 1, GetVfxEffectSummary(effect)));
                string hiddenKey = GetEditorOnHitCueVisibilityKey(_selectedEventIndex, damageIndex, i);
                bool visible = !_hiddenPreviewCueKeys.Contains(hiddenKey);
                bool newVisible = EditorGUILayout.ToggleLeft("显示", visible, GUILayout.Width(50f));
                if (newVisible != visible)
                {
                    if (newVisible)
                        _hiddenPreviewCueKeys.Remove(hiddenKey);
                    else
                        _hiddenPreviewCueKeys.Add(hiddenKey);

                    bool isCurrentHidden = _activeSceneHitVfxEventIndex == _selectedEventIndex
                        && _activeSceneHitVfxDamageIndex == damageIndex
                        && _activeSceneHitVfxIndex == i;
                    if (!newVisible && isCurrentHidden)
                    {
                        _activeSceneHitVfxEventIndex = -1;
                        _activeSceneHitVfxDamageIndex = -1;
                        _activeSceneHitVfxIndex = -1;
                        DestroyScenePreviewCueInstance();
                    }

                    Repaint();
                    SceneView.RepaintAll();
                }
                bool isEditingHitVfx = _selectedEventTrackType == TimelineTrackType.Damage
                    && _activeSceneHitVfxEventIndex == _selectedEventIndex
                    && _activeSceneHitVfxDamageIndex == damageIndex
                    && _activeSceneHitVfxIndex == i;
                if (GUILayout.Button(isEditingHitVfx ? "编辑中" : "场景编辑", GUILayout.Width(64f)))
                {
                    _hiddenPreviewCueKeys.Remove(hiddenKey);
                    bool same = isEditingHitVfx;
                    _activeSceneHitVfxEventIndex = same ? -1 : _selectedEventIndex;
                    _activeSceneHitVfxDamageIndex = same ? -1 : damageIndex;
                    _activeSceneHitVfxIndex = same ? -1 : i;
                    if (!same)
                        UpdateScenePreviewCueInstance();
                    else
                        DestroyScenePreviewCueInstance();
                    Repaint();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    GameObject newParticlePrefab = (GameObject)EditorGUILayout.ObjectField("粒子特效 Prefab", effect.particlePrefab, typeof(GameObject), false);
                    CueAnchor newAnchor = DrawCueAnchorPopup("挂点", effect.anchor, OnHitCueAnchors, OnHitCueAnchorLabels);
                    Vector3 newOffset = EditorGUILayout.Vector3Field("位置偏移", effect.offset);
                    Vector3 newRotationEuler = EditorGUILayout.Vector3Field("旋转偏移", effect.rotationEuler);
                    Vector3 newScale = EditorGUILayout.Vector3Field("缩放", effect.scale);
                    SkillMotionSettings newMotion = CopyMotionSettings(effect.motion);
                    if (ShouldShowCueMotionSettings(newAnchor))
                        DrawMotionSettingsEditor("移动设置", newMotion);
                    SkillCueDestroyMode newDestroyMode = (SkillCueDestroyMode)EditorGUILayout.EnumPopup("销毁方式", effect.destroyMode);
                    float newDuration = effect.duration;
                    if (newDestroyMode == SkillCueDestroyMode.Timed)
                        newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("销毁时间(秒)", effect.duration));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Nested VFX Effect");
                        effect.particlePrefab = newParticlePrefab;
                        effect.anchor = newAnchor;
                        effect.offset = newOffset;
                        effect.rotationEuler = newRotationEuler;
                        effect.scale = new Vector3(
                            Mathf.Max(0.01f, newScale.x),
                            Mathf.Max(0.01f, newScale.y),
                            Mathf.Max(0.01f, newScale.z));
                        effect.motion = ShouldShowCueMotionSettings(newAnchor) ? newMotion : new SkillMotionSettings();
                        effect.destroyMode = newDestroyMode;
                        effect.duration = newDestroyMode == SkillCueDestroyMode.Timed ? newDuration : 0f;
                        MarkDatabaseDirty();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Nested VFX Effect");
                effects.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNestedSfxEffectsEditor(SkillDamageEffect damageEffect, int damageIndex)
        {
            EnsureSfxEffects(damageEffect);
            DrawNestedSfxEffectsList("命中音效效果", damageEffect.onHitSfxEffects, damageIndex);
        }

        private void DrawNestedSfxEffectsList(string title, List<SkillSfxEffect> effects, int damageIndex)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            if (GUILayout.Button("添加", GUILayout.Width(48f)))
            {
                Undo.RecordObject(_database, "Add Nested SFX Effect");
                effects.Add(new SkillSfxEffect());
                MarkDatabaseDirty();
            }
            GUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < effects.Count; i++)
            {
                SkillSfxEffect effect = effects[i] ?? (effects[i] = new SkillSfxEffect());
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                bool isExpanded = DrawEffectFoldout(
                    GetNestedSfxFoldoutKey(_selectedEventIndex, damageIndex, i),
                    BuildIndexedFoldoutLabel("命中音效效果", i + 1, GetSfxEffectSummary(effect)));
                string hiddenKey = GetEditorOnHitSfxVisibilityKey(_selectedEventIndex, damageIndex, i);
                bool visible = !_hiddenPreviewCueKeys.Contains(hiddenKey);
                bool newVisible = EditorGUILayout.ToggleLeft("显示", visible, GUILayout.Width(50f));
                if (newVisible != visible)
                {
                    if (newVisible)
                        _hiddenPreviewCueKeys.Remove(hiddenKey);
                    else
                        _hiddenPreviewCueKeys.Add(hiddenKey);

                    Repaint();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    removeIndex = i;
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    EditorGUI.BeginChangeCheck();
                    AudioClip newAudioClip = (AudioClip)EditorGUILayout.ObjectField("音效片段", effect.audioClip, typeof(AudioClip), false);
                    CueAnchor newAnchor = DrawCueAnchorPopup("挂点", effect.anchor, OnHitCueAnchors, OnHitCueAnchorLabels);
                    Vector3 newOffset = EditorGUILayout.Vector3Field("位置偏移", effect.offset);
                    bool newLoop = EditorGUILayout.Toggle("循环播放", effect.loop);
                    SkillCueDestroyMode newDestroyMode = (SkillCueDestroyMode)EditorGUILayout.EnumPopup("销毁方式", effect.destroyMode);
                    float newDuration = effect.duration;
                    if (newDestroyMode == SkillCueDestroyMode.Timed)
                        newDuration = Mathf.Max(0f, EditorGUILayout.FloatField("销毁时间(秒)", effect.duration));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Nested SFX Effect");
                        effect.audioClip = newAudioClip;
                        effect.anchor = newAnchor;
                        effect.offset = newOffset;
                        effect.loop = newLoop;
                        effect.destroyMode = newDestroyMode;
                        effect.duration = newDestroyMode == SkillCueDestroyMode.Timed ? newDuration : 0f;
                        MarkDatabaseDirty();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_database, "Remove Nested SFX Effect");
                effects.RemoveAt(removeIndex);
                MarkDatabaseDirty();
            }

            EditorGUILayout.EndVertical();
        }

        private void MarkDatabaseDirty()
        {
            if (_database == null)
                return;

            _database.Synchronize();
            EditorUtility.SetDirty(_database);
            RebuildSerializedDb();
            Repaint();
        }

        private void AddEvent(SharedSkillDefinition skill, TimelineTrackType trackType, float? explicitStartTime = null)
        {
            if (skill == null)
                return;

            Undo.RecordObject(_database, "Add Skill Event");
            SkillTimedEventBase selectedEvent = GetSelectedTimedEvent();
            float startTime = explicitStartTime ?? (selectedEvent != null
                ? selectedEvent.startTime + Mathf.Max(0.1f, GetActiveSnapStep())
                : _previewTime);
            startTime = SnapTime(startTime);

            int newIndex = -1;
            string eventId = $"evt_{GetTotalEventCount(skill)}";
            switch (trackType)
            {
                case TimelineTrackType.Damage:
                    var hitEffect = new SkillDamageEffect();
                    hitEffect.TryMigrateLegacySubEffects();
                    var damageEvent = new SkillDamageEvent
                    {
                        eventId = eventId,
                        startTime = startTime,
                        damageEffects = new List<SkillDamageEffect> { hitEffect },
                    };
                    GetDamageEventList(skill).Add(damageEvent);
                    newIndex = GetDamageEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Physics:
                    var physicsEvent = new SkillPhysicsEvent
                    {
                        eventId = eventId,
                        startTime = startTime,
                        physicsEffects = new List<SkillPhysicsEffect> { new SkillPhysicsEffect { effectType = PhysicsEffectType.DashSelf } },
                    };
                    GetPhysicsEventList(skill).Add(physicsEvent);
                    newIndex = GetPhysicsEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Attribute:
                    var attributeEvent = new SkillAttributeEvent
                    {
                        eventId = eventId,
                        startTime = startTime,
                        attributeEffects = new List<SkillAttributeEffect> { new SkillAttributeEffect { targetMode = SkillTargetMode.Self } },
                    };
                    GetAttributeEventList(skill).Add(attributeEvent);
                    newIndex = GetAttributeEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Vfx:
                    var vfxEvent = new SkillVfxEvent
                    {
                        eventId = eventId,
                        startTime = startTime,
                        vfxEffects = new List<SkillVfxEffect> { new SkillVfxEffect { anchor = CueAnchor.Caster, duration = 0f } },
                    };
                    GetVfxEventList(skill).Add(vfxEvent);
                    newIndex = GetVfxEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Sfx:
                    var sfxEvent = new SkillSfxEvent
                    {
                        eventId = eventId,
                        startTime = startTime,
                        sfxEffects = new List<SkillSfxEffect> { new SkillSfxEffect { anchor = CueAnchor.Caster, duration = 0f } },
                    };
                    GetSfxEventList(skill).Add(sfxEvent);
                    newIndex = GetSfxEventList(skill).Count - 1;
                    break;
            }

            if (newIndex >= 0)
                SelectEvent(trackType, newIndex);

            MarkDatabaseDirty();
        }

        private void CopySelectedEvent(SharedSkillDefinition skill)
        {
            if (skill == null || !HasSelectedEvent())
                return;

            SkillTimedEventBase evt = GetSelectedTimedEvent();
            if (evt == null)
                return;

            s_eventClipboardJson = JsonUtility.ToJson(evt);
            s_eventClipboardTrackType = _selectedEventTrackType;
        }

        private void PasteClipboardEvent(SharedSkillDefinition skill)
        {
            if (skill == null || string.IsNullOrEmpty(s_eventClipboardJson))
                return;

            Undo.RecordObject(_database, "Paste Skill Event");
            int newIndex = -1;
            switch (s_eventClipboardTrackType)
            {
                case TimelineTrackType.Damage:
                    SkillDamageEvent damageEvent = JsonUtility.FromJson<SkillDamageEvent>(s_eventClipboardJson);
                    if (damageEvent == null)
                        return;
                    damageEvent.startTime = SnapTime(_previewTime);
                    GetDamageEventList(skill).Add(damageEvent);
                    newIndex = GetDamageEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Physics:
                    SkillPhysicsEvent physicsEvent = JsonUtility.FromJson<SkillPhysicsEvent>(s_eventClipboardJson);
                    if (physicsEvent == null)
                        return;
                    physicsEvent.startTime = SnapTime(_previewTime);
                    GetPhysicsEventList(skill).Add(physicsEvent);
                    newIndex = GetPhysicsEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Attribute:
                    SkillAttributeEvent attributeEvent = JsonUtility.FromJson<SkillAttributeEvent>(s_eventClipboardJson);
                    if (attributeEvent == null)
                        return;
                    attributeEvent.startTime = SnapTime(_previewTime);
                    GetAttributeEventList(skill).Add(attributeEvent);
                    newIndex = GetAttributeEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Vfx:
                    SkillVfxEvent vfxEvent = JsonUtility.FromJson<SkillVfxEvent>(s_eventClipboardJson);
                    if (vfxEvent == null)
                        return;
                    vfxEvent.startTime = SnapTime(_previewTime);
                    GetVfxEventList(skill).Add(vfxEvent);
                    newIndex = GetVfxEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Sfx:
                    SkillSfxEvent sfxEvent = JsonUtility.FromJson<SkillSfxEvent>(s_eventClipboardJson);
                    if (sfxEvent == null)
                        return;
                    sfxEvent.startTime = SnapTime(_previewTime);
                    GetSfxEventList(skill).Add(sfxEvent);
                    newIndex = GetSfxEventList(skill).Count - 1;
                    break;
            }

            if (newIndex >= 0)
                SelectEvent(s_eventClipboardTrackType, newIndex);
            MarkDatabaseDirty();
        }

        private void DuplicateSelectedEvent(SharedSkillDefinition skill)
        {
            if (skill == null || !HasSelectedEvent())
                return;

            SkillTimedEventBase source = GetSelectedTimedEvent();
            if (source == null)
                return;

            float offset = Mathf.Max(GetTrackDisplayDuration(source, _selectedEventTrackType), Mathf.Max(0.1f, GetActiveSnapStep()));

            Undo.RecordObject(_database, "Duplicate Skill Event");
            int newIndex = -1;
            switch (_selectedEventTrackType)
            {
                case TimelineTrackType.Damage:
                    SkillDamageEvent clonedDamage = JsonUtility.FromJson<SkillDamageEvent>(JsonUtility.ToJson((SkillDamageEvent)source));
                    if (clonedDamage == null)
                        return;
                    clonedDamage.startTime = SnapTime(source.startTime + offset);
                    GetDamageEventList(skill).Add(clonedDamage);
                    newIndex = GetDamageEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Physics:
                    SkillPhysicsEvent clonedPhysics = JsonUtility.FromJson<SkillPhysicsEvent>(JsonUtility.ToJson((SkillPhysicsEvent)source));
                    if (clonedPhysics == null)
                        return;
                    clonedPhysics.startTime = SnapTime(source.startTime + offset);
                    GetPhysicsEventList(skill).Add(clonedPhysics);
                    newIndex = GetPhysicsEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Attribute:
                    SkillAttributeEvent clonedAttribute = JsonUtility.FromJson<SkillAttributeEvent>(JsonUtility.ToJson((SkillAttributeEvent)source));
                    if (clonedAttribute == null)
                        return;
                    clonedAttribute.startTime = SnapTime(source.startTime + offset);
                    GetAttributeEventList(skill).Add(clonedAttribute);
                    newIndex = GetAttributeEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Vfx:
                    SkillVfxEvent clonedVfx = JsonUtility.FromJson<SkillVfxEvent>(JsonUtility.ToJson((SkillVfxEvent)source));
                    if (clonedVfx == null)
                        return;
                    clonedVfx.startTime = SnapTime(source.startTime + offset);
                    GetVfxEventList(skill).Add(clonedVfx);
                    newIndex = GetVfxEventList(skill).Count - 1;
                    break;

                case TimelineTrackType.Sfx:
                    SkillSfxEvent clonedSfx = JsonUtility.FromJson<SkillSfxEvent>(JsonUtility.ToJson((SkillSfxEvent)source));
                    if (clonedSfx == null)
                        return;
                    clonedSfx.startTime = SnapTime(source.startTime + offset);
                    GetSfxEventList(skill).Add(clonedSfx);
                    newIndex = GetSfxEventList(skill).Count - 1;
                    break;
            }

            if (newIndex >= 0)
                SelectEvent(_selectedEventTrackType, newIndex);
            MarkDatabaseDirty();
        }

        private void RemoveSelectedEvent(SharedSkillDefinition skill)
        {
            if (skill == null || !HasSelectedEvent())
                return;

            Undo.RecordObject(_database, "Delete Skill Event");
            switch (_selectedEventTrackType)
            {
                case TimelineTrackType.Damage:
                    GetDamageEventList(skill).RemoveAt(_selectedEventIndex);
                    if (_activeSceneDamageEventIndex == _selectedEventIndex)
                    {
                        _activeSceneDamageEventIndex = -1;
                        _activeSceneDamageIndex = -1;
                    }
                    else if (_activeSceneDamageEventIndex > _selectedEventIndex)
                    {
                        _activeSceneDamageEventIndex--;
                    }

                    if (_activeSceneHitVfxEventIndex == _selectedEventIndex)
                    {
                        _activeSceneHitVfxEventIndex = -1;
                        _activeSceneHitVfxDamageIndex = -1;
                        _activeSceneHitVfxIndex = -1;
                    }
                    else if (_activeSceneHitVfxEventIndex > _selectedEventIndex)
                    {
                        _activeSceneHitVfxEventIndex--;
                    }

                    if (_activeSceneCompanionVfxEventIndex == _selectedEventIndex)
                    {
                        _activeSceneCompanionVfxEventIndex = -1;
                        _activeSceneCompanionVfxDamageIndex = -1;
                        _activeSceneCompanionVfxIndex = -1;
                    }
                    else if (_activeSceneCompanionVfxEventIndex > _selectedEventIndex)
                    {
                        _activeSceneCompanionVfxEventIndex--;
                    }
                    _selectedEventIndex = Mathf.Clamp(_selectedEventIndex - 1, -1, GetDamageEventList(skill).Count - 1);
                    break;
                case TimelineTrackType.Physics:
                    GetPhysicsEventList(skill).RemoveAt(_selectedEventIndex);
                    _selectedEventIndex = Mathf.Clamp(_selectedEventIndex - 1, -1, GetPhysicsEventList(skill).Count - 1);
                    break;
                case TimelineTrackType.Attribute:
                    GetAttributeEventList(skill).RemoveAt(_selectedEventIndex);
                    _selectedEventIndex = Mathf.Clamp(_selectedEventIndex - 1, -1, GetAttributeEventList(skill).Count - 1);
                    break;
                case TimelineTrackType.Vfx:
                    GetVfxEventList(skill).RemoveAt(_selectedEventIndex);
                    if (_activeSceneCueEventIndex == _selectedEventIndex)
                    {
                        _activeSceneCueEventIndex = -1;
                        _activeSceneCueIndex = -1;
                    }
                    else if (_activeSceneCueEventIndex > _selectedEventIndex)
                    {
                        _activeSceneCueEventIndex--;
                    }
                    _selectedEventIndex = Mathf.Clamp(_selectedEventIndex - 1, -1, GetVfxEventList(skill).Count - 1);
                    break;
                case TimelineTrackType.Sfx:
                    GetSfxEventList(skill).RemoveAt(_selectedEventIndex);
                    _selectedEventIndex = Mathf.Clamp(_selectedEventIndex - 1, -1, GetSfxEventList(skill).Count - 1);
                    break;
            }
            DestroyScenePreviewCueInstance();
            MarkDatabaseDirty();
        }

        private void ClearAllEvents(SharedSkillDefinition skill)
        {
            if (skill == null)
                return;

            Undo.RecordObject(_database, "Clear Skill Events");
            GetDamageEventList(skill).Clear();
            GetPhysicsEventList(skill).Clear();
            GetAttributeEventList(skill).Clear();
            GetVfxEventList(skill).Clear();
            GetSfxEventList(skill).Clear();

            _selectedEventIndex = -1;
            _activeSceneDamageEventIndex = -1;
            _activeSceneDamageIndex = -1;
            _activeSceneCueEventIndex = -1;
            _activeSceneCueIndex = -1;
            _activeSceneHitVfxEventIndex = -1;
            _activeSceneHitVfxDamageIndex = -1;
            _activeSceneHitVfxIndex = -1;
            _activeSceneCompanionVfxEventIndex = -1;
            _activeSceneCompanionVfxDamageIndex = -1;
            _activeSceneCompanionVfxIndex = -1;
            DestroyScenePreviewCueInstance();
            MarkDatabaseDirty();
        }

        private void ConfirmAndClearAllEvents(SharedSkillDefinition skill)
        {
            if (skill == null)
                return;

            if (!EditorUtility.DisplayDialog("确认清空事件", "确定要清空当前技能的所有事件吗？此操作不可撤销。", "确定", "取消"))
                return;

            ClearAllEvents(skill);
        }

        private void SortEvents(SharedSkillDefinition skill)
        {
            if (skill == null)
                return;

            Undo.RecordObject(_database, "Sort Skill Events");
            SkillTimedEventBase selectedEvent = GetSelectedTimedEvent();
            SortTimedEventList(GetDamageEventList(skill));
            SortTimedEventList(GetPhysicsEventList(skill));
            SortTimedEventList(GetAttributeEventList(skill));
            SortTimedEventList(GetVfxEventList(skill));
            SortTimedEventList(GetSfxEventList(skill));

            if (selectedEvent != null)
            {
                int newIndex = FindTrackEventIndex(skill, _selectedEventTrackType, selectedEvent);
                if (newIndex >= 0)
                    _selectedEventIndex = newIndex;
            }
            MarkDatabaseDirty();
        }

        private static void SortTimedEventList<T>(List<T> events) where T : SkillTimedEventBase
        {
            if (events == null)
                return;

            events.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return 1;
                if (right == null)
                    return -1;
                return left.startTime.CompareTo(right.startTime);
            });
        }

        private int FindTrackEventIndex(SharedSkillDefinition skill, TimelineTrackType trackType, SkillTimedEventBase target)
        {
            if (skill == null || target == null)
                return -1;

            int count = GetTrackEventCount(skill, trackType);
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(GetTrackEvent(skill, trackType, i), target))
                    return i;
            }

            return -1;
        }

        private void StartPreviewPlayback()
        {
            bool restartFromBeginning = _previewTime <= 0.0001f || _previewTime >= GetPreviewTotalDuration() - 0.01f;
            if (_previewTime >= GetPreviewTotalDuration() - 0.01f)
                _previewTime = 0f;

            StopAllTimelinePreviewAudio();
            DestroyTimelinePreviewVfxInstances();
            StartAnimationMode();
            SampleAnimationAtTime(_previewTime);
            if (restartFromBeginning)
            {
                _timelinePreviewHitSfxKeys.Clear();
                TriggerTimelinePreviewSfx(_previewTime - 0.0001f, _previewTime);
                TriggerTimelinePreviewHitSfx();
            }
            else
            {
                RebuildTimelinePreviewHitSfxKeys();
            }
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
            StopAllTimelinePreviewAudio();
            DestroyTimelinePreviewVfxInstances();
            _timelinePreviewHitSfxKeys.Clear();
            RestorePreviewPhysicsTransforms();
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private void SampleAnimationAtTime(float time)
        {
            RestorePreviewPhysicsTransforms();
            if (_previewTarget == null)
            {
                DestroyTimelinePreviewVfxInstances();
                _timelinePreviewHitSfxKeys.Clear();
                return;
            }

            if (_previewClip != null)
            {
                if (!AnimationMode.InAnimationMode())
                    AnimationMode.StartAnimationMode();

                float sampleTime = Mathf.Clamp(time, 0f, _previewClip.length);
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(_previewTarget, _previewClip, sampleTime);
                AnimationMode.EndSampling();
            }

            UpdateTimelinePreviewVfxInstances();
            ApplyPreviewPhysicsTransforms();
            UpdateScenePreviewCueInstance();
        }

        private float GetPreviewTotalDuration()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            float timelineDuration = skill != null ? GetSkillPreviewDuration(skill) : 0f;
            float clipDuration = _previewClip != null ? _previewClip.length : 0f;
            float stateExitDuration = skill != null && skill.HasUntilStateExitEvents()
                ? GetPreviewStateExitTime()
                : 0f;
            return Mathf.Max(timelineDuration, clipDuration, stateExitDuration, 0.1f);
        }

        private float GetPreviewStateExitTime()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            float clipDuration = _previewClip != null ? _previewClip.length : 0f;
            float timelineDuration = skill != null ? skill.GetTimelineDuration() : 0f;
            return Mathf.Max(clipDuration, timelineDuration, 2f);
        }

        private float GetSkillPreviewDuration(SharedSkillDefinition skill)
        {
            if (skill == null)
                return 0f;

            float duration = skill.GetTimelineDuration();
            duration = Mathf.Max(duration, GetTrackPreviewDuration(GetDamageEventList(skill), TimelineTrackType.Damage));
            duration = Mathf.Max(duration, GetTrackPreviewDuration(GetPhysicsEventList(skill), TimelineTrackType.Physics));
            duration = Mathf.Max(duration, GetTrackPreviewDuration(GetAttributeEventList(skill), TimelineTrackType.Attribute));
            duration = Mathf.Max(duration, GetTrackPreviewDuration(GetVfxEventList(skill), TimelineTrackType.Vfx));
            duration = Mathf.Max(duration, GetTrackPreviewDuration(GetSfxEventList(skill), TimelineTrackType.Sfx));
            return duration;
        }

        private float GetTrackPreviewDuration<T>(List<T> events, TimelineTrackType trackType) where T : SkillTimedEventBase
        {
            float maxTime = 0f;
            if (events == null)
                return maxTime;

            for (int i = 0; i < events.Count; i++)
            {
                T evt = events[i];
                if (evt == null)
                    continue;

                maxTime = Mathf.Max(maxTime, evt.startTime + GetTrackDisplayDuration(evt, trackType));
            }

            return maxTime;
        }

        private float GetPreviewPlaybackDuration(SkillTimedEventBase evt, TimelineTrackType trackType)
        {
            float lastTriggerTime = evt != null && evt.triggerMode == SkillEventTriggerMode.Repeated
                ? GetPreviewRepeatedEndTime(evt)
                : evt != null ? evt.startTime : 0f;
            float duration = Mathf.Max(0f, GetTrackDisplayDuration(evt, trackType));
            switch (trackType)
            {
                case TimelineTrackType.Vfx:
                    if (evt is SkillVfxEvent vfxEvent && vfxEvent.vfxEffects != null)
                    {
                        for (int i = 0; i < vfxEvent.vfxEffects.Count; i++)
                            duration = Mathf.Max(duration, GetVfxPlaybackDuration(vfxEvent.vfxEffects[i], lastTriggerTime));
                    }
                    break;
                case TimelineTrackType.Sfx:
                    if (evt is SkillSfxEvent sfxEvent && sfxEvent.sfxEffects != null)
                    {
                        for (int i = 0; i < sfxEvent.sfxEffects.Count; i++)
                            duration = Mathf.Max(duration, GetSfxPlaybackDuration(sfxEvent.sfxEffects[i], lastTriggerTime));
                    }
                    break;
            }

            return duration;
        }

        private float GetTrackEffectTailDuration(SkillTimedEventBase evt, TimelineTrackType trackType, float lastTriggerTime)
        {
            if (evt == null)
                return 0f;

            float duration = 0f;
            switch (trackType)
            {
                case TimelineTrackType.Damage:
                    if (evt is SkillDamageEvent damageEvent)
                        duration = Mathf.Max(duration, GetPreviewDamageEventTailDuration(damageEvent, lastTriggerTime));
                    break;
                case TimelineTrackType.Physics:
                    if (evt is SkillPhysicsEvent physicsEvent)
                        duration = Mathf.Max(duration, GetPreviewPhysicsEventTailDuration(physicsEvent, lastTriggerTime));
                    break;
                case TimelineTrackType.Attribute:
                    if (evt is SkillAttributeEvent attributeEvent)
                        duration = Mathf.Max(duration, GetPreviewAttributeEventTailDuration(attributeEvent, lastTriggerTime));
                    break;
                case TimelineTrackType.Vfx:
                    if (evt is SkillVfxEvent vfxEvent && vfxEvent.vfxEffects != null)
                    {
                        for (int i = 0; i < vfxEvent.vfxEffects.Count; i++)
                        {
                            SkillVfxEffect effect = vfxEvent.vfxEffects[i];
                            if (effect != null)
                                duration = Mathf.Max(duration, GetVfxPlaybackDuration(effect, lastTriggerTime));
                        }
                    }
                    break;
                case TimelineTrackType.Sfx:
                    if (evt is SkillSfxEvent sfxEvent && sfxEvent.sfxEffects != null)
                    {
                        for (int i = 0; i < sfxEvent.sfxEffects.Count; i++)
                        {
                            SkillSfxEffect effect = sfxEvent.sfxEffects[i];
                            if (effect != null)
                                duration = Mathf.Max(duration, GetSfxPlaybackDuration(effect, lastTriggerTime));
                        }
                    }
                    break;
            }

            return Mathf.Max(0f, duration);
        }

        private float GetPreviewPhysicsEventTailDuration(SkillPhysicsEvent evt, float lastTriggerTime)
        {
            if (evt?.physicsEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.physicsEffects.Count; i++)
                duration = Mathf.Max(duration, GetPreviewPhysicsEffectLifetime(evt.physicsEffects[i], lastTriggerTime));

            return duration;
        }

        private float GetPreviewAttributeEventTailDuration(SkillAttributeEvent evt, float lastTriggerTime)
        {
            if (evt?.attributeEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.attributeEffects.Count; i++)
                duration = Mathf.Max(duration, GetPreviewAttributeEffectLifetime(evt.attributeEffects[i], lastTriggerTime));

            return duration;
        }

        private static float GetTrackEffectTailDurationForAuthoring(SkillTimedEventBase evt, TimelineTrackType trackType)
        {
            if (evt == null)
                return 0f;

            return trackType switch
            {
                TimelineTrackType.Damage => evt is SkillDamageEvent damageEvent ? SharedSkillDefinition.GetDamageEventTailDuration(damageEvent) : 0f,
                TimelineTrackType.Physics => evt is SkillPhysicsEvent physicsEvent ? SharedSkillDefinition.GetPhysicsEventTailDuration(physicsEvent) : 0f,
                TimelineTrackType.Attribute => evt is SkillAttributeEvent attributeEvent ? SharedSkillDefinition.GetAttributeEventTailDuration(attributeEvent) : 0f,
                TimelineTrackType.Vfx => evt is SkillVfxEvent vfxEvent ? SharedSkillDefinition.GetVfxEventTailDuration(vfxEvent) : 0f,
                TimelineTrackType.Sfx => evt is SkillSfxEvent sfxEvent ? SharedSkillDefinition.GetSfxEventTailDuration(sfxEvent) : 0f,
                _ => 0f,
            };
        }

        private float GetPreviewPhysicsEffectLifetime(SkillPhysicsEffect effect, float triggerTime)
        {
            if (effect == null)
                return 0f;

            if (effect.durationMode == SkillEffectDurationMode.UntilStateExit && effect.SupportsUntilStateExitDuration)
                return Mathf.Max(0f, GetPreviewStateExitTime() - triggerTime);

            return SharedSkillDefinition.GetPhysicsEffectLifetime(effect);
        }

        private float GetPreviewAttributeEffectLifetime(SkillAttributeEffect effect, float triggerTime)
        {
            if (effect == null || !effect.UsesDuration)
                return 0f;

            if (effect.durationMode == SkillEffectDurationMode.UntilStateExit)
                return Mathf.Max(0f, GetPreviewStateExitTime() - triggerTime);

            return SharedSkillDefinition.GetAttributeEffectLifetime(effect);
        }

        private float GetPreviewDamageEventTailDuration(SkillDamageEvent evt, float lastTriggerTime)
        {
            if (evt?.damageEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.damageEffects.Count; i++)
            {
                SkillDamageEffect effect = evt.damageEffects[i];
                if (effect == null)
                    continue;

                float detectionDuration = Mathf.Max(0f, effect.detectionDuration);
                float hitTime = lastTriggerTime + detectionDuration;
                float onHitTail = Mathf.Max(
                    GetPreviewOnHitPhysicsTailDuration(effect),
                    GetPreviewOnHitAttributeTailDuration(effect));

                if (effect.onHitVfxEffects != null)
                {
                    for (int vfxIndex = 0; vfxIndex < effect.onHitVfxEffects.Count; vfxIndex++)
                        onHitTail = Mathf.Max(onHitTail, GetVfxPlaybackDuration(effect.onHitVfxEffects[vfxIndex], hitTime));
                }

                if (effect.onHitSfxEffects != null)
                {
                    for (int sfxIndex = 0; sfxIndex < effect.onHitSfxEffects.Count; sfxIndex++)
                        onHitTail = Mathf.Max(onHitTail, GetSfxPlaybackDuration(effect.onHitSfxEffects[sfxIndex], hitTime));
                }

                duration = Mathf.Max(duration, detectionDuration + onHitTail);
            }

            return duration;
        }

        private static float GetPreviewOnHitPhysicsTailDuration(SkillDamageEffect effect)
        {
            if (effect?.onHitPhysicsEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < effect.onHitPhysicsEffects.Count; i++)
                duration = Mathf.Max(duration, SharedSkillDefinition.GetPhysicsEffectLifetime(effect.onHitPhysicsEffects[i]));

            return duration;
        }

        private static float GetPreviewOnHitAttributeTailDuration(SkillDamageEffect effect)
        {
            if (effect?.onHitAttributeEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < effect.onHitAttributeEffects.Count; i++)
                duration = Mathf.Max(duration, SharedSkillDefinition.GetAttributeEffectLifetime(effect.onHitAttributeEffects[i]));

            return duration;
        }

        private float GetVfxPlaybackDuration(SkillVfxEffect effect, float triggerTime)
        {
            if (effect == null)
                return 0f;

            if (effect.destroyMode == SkillCueDestroyMode.OnStateExit)
                return Mathf.Max(0f, GetPreviewStateExitTime() - triggerTime);

            if (effect.destroyMode == SkillCueDestroyMode.Timed && effect.duration > 0f)
                return effect.duration;

            return SharedSkillDefinition.GetVfxEffectLifetime(effect);
        }

        private float GetSfxPlaybackDuration(SkillSfxEffect effect, float triggerTime)
        {
            if (effect == null)
                return 0f;

            if (effect.destroyMode == SkillCueDestroyMode.OnStateExit
                || (effect.loop && effect.destroyMode == SkillCueDestroyMode.NaturalDestroy))
            {
                return Mathf.Max(0f, GetPreviewStateExitTime() - triggerTime);
            }

            if (effect.destroyMode == SkillCueDestroyMode.Timed && effect.duration > 0f)
                return effect.duration;

            return SharedSkillDefinition.GetSfxEffectLifetime(effect);
        }

        private static MethodInfo ResolveAudioUtilMethod(string methodName, params System.Type[] parameterTypes)
        {
            if (AudioUtilType == null)
                return null;

            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            if (parameterTypes != null && parameterTypes.Length > 0)
            {
                MethodInfo exact = AudioUtilType.GetMethod(methodName, flags, null, parameterTypes, null);
                if (exact != null)
                    return exact;
            }

            MethodInfo[] methods = AudioUtilType.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method != null && method.Name == methodName)
                    return method;
            }

            return null;
        }

        private void TriggerTimelinePreviewSfx(float previousTime, float currentTime)
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return;

            List<SkillSfxEvent> events = GetSfxEventList(skill);
            if (events == null)
                return;

            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                SkillSfxEvent evt = events[eventIndex];
                if (evt?.sfxEffects == null)
                    continue;

                int triggerCount = GetTriggeredOccurrenceCount(evt, previousTime, currentTime);
                for (int triggerIndex = 0; triggerIndex < triggerCount; triggerIndex++)
                {
                    for (int effectIndex = 0; effectIndex < evt.sfxEffects.Count; effectIndex++)
                    {
                        SkillSfxEffect effect = evt.sfxEffects[effectIndex];
                        if (effect?.audioClip == null)
                            continue;
                        if (!IsTopLevelSfxVisible(eventIndex, effectIndex))
                            continue;

                        PlayPreviewAudio(effect.audioClip);
                    }
                }
            }
        }

        private int GetTriggeredOccurrenceCount(SkillTimedEventBase evt, float previousTime, float currentTime)
        {
            if (evt == null || currentTime + 0.0001f < evt.startTime)
                return 0;

            if (evt.triggerMode == SkillEventTriggerMode.Once)
                return previousTime < evt.startTime && currentTime + 0.0001f >= evt.startTime ? 1 : 0;

            float endTime = evt.RepeatsUntilStateExit ? currentTime : evt.GetEndTime();
            if (previousTime > endTime + 0.0001f)
                return 0;

            float interval = Mathf.Max(0.01f, evt.repeatInterval);
            int count = 0;
            for (float triggerTime = evt.startTime; triggerTime <= endTime + 0.0001f; triggerTime += interval)
            {
                if (triggerTime > previousTime && triggerTime <= currentTime + 0.0001f)
                    count++;
            }

            return count;
        }

        private void UpdateTimelinePreviewVfxInstances()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null || _previewTarget == null)
            {
                DestroyTimelinePreviewVfxInstances();
                return;
            }

            var activeKeys = new HashSet<string>();
            List<SkillVfxEvent> events = GetVfxEventList(skill);
            if (events != null)
            {
                for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    SkillVfxEvent evt = events[eventIndex];
                    if (evt?.vfxEffects == null)
                        continue;

                    float endTime = evt.triggerMode == SkillEventTriggerMode.Repeated ? GetPreviewRepeatedEndTime(evt) : evt.startTime;
                    float interval = Mathf.Max(0.01f, evt.repeatInterval);
                    int triggerIndex = 0;
                    for (float triggerTime = evt.startTime; triggerTime <= endTime + 0.0001f; triggerTime += evt.triggerMode == SkillEventTriggerMode.Repeated ? interval : endTime + 1f)
                    {
                        for (int effectIndex = 0; effectIndex < evt.vfxEffects.Count; effectIndex++)
                        {
                            SkillVfxEffect effect = evt.vfxEffects[effectIndex];
                            float duration = GetVfxPlaybackDuration(effect, triggerTime);
                            if (effect?.particlePrefab == null || duration <= 0f)
                                continue;

                            if (!IsTopLevelCueVisible(eventIndex, effectIndex))
                                continue;

                            if (!_isPlaying && IsSceneEditingTopLevelCue(eventIndex, effectIndex))
                                continue;

                            if (_previewTime + 0.0001f < triggerTime || _previewTime > triggerTime + duration + 0.0001f)
                                continue;

                            string key = GetTimelinePreviewVfxKey(eventIndex, effectIndex, triggerIndex);
                            activeKeys.Add(key);
                            TimelinePreviewVfxInstance playback = EnsureTimelinePreviewVfxInstance(key, effect, TimelineTrackType.Vfx, eventIndex, effectIndex, triggerIndex, triggerTime, null);
                            if (playback != null)
                                SimulateTimelinePreviewVfx(playback, Mathf.Max(0f, _previewTime - triggerTime));
                        }

                        if (evt.triggerMode != SkillEventTriggerMode.Repeated)
                            break;
                        triggerIndex++;
                    }
                }
            }

            List<PreviewDamageHitResult> damageHits = CollectPreviewDamageHitHistory(skill);
            List<SkillDamageEvent> damageEvents = GetDamageEventList(skill);
            if (damageEvents != null)
            {
                for (int eventIndex = 0; eventIndex < damageEvents.Count; eventIndex++)
                {
                    SkillDamageEvent evt = damageEvents[eventIndex];
                    if (evt?.damageEffects == null)
                        continue;

                    float endTime = evt.triggerMode == SkillEventTriggerMode.Repeated ? GetPreviewRepeatedEndTime(evt) : evt.startTime;
                    float interval = Mathf.Max(0.01f, evt.repeatInterval);
                    int triggerIndex = 0;
                    for (float triggerTime = evt.startTime; triggerTime <= endTime + 0.0001f; triggerTime += evt.triggerMode == SkillEventTriggerMode.Repeated ? interval : endTime + 1f)
                    {
                        for (int damageEffectIndex = 0; damageEffectIndex < evt.damageEffects.Count; damageEffectIndex++)
                        {
                            SkillDamageEffect damageEffect = evt.damageEffects[damageEffectIndex];
                            damageEffect?.TryMigrateLegacySubEffects();
                            if (damageEffect == null
                                || damageEffect.detectionType == DamageDetectionType.Collision
                                || damageEffect.detectionDuration <= 0f
                                || damageEffect.companionVfxEffects == null)
                            {
                                continue;
                            }

                            float companionDuration = Mathf.Max(0f, damageEffect.detectionDuration);
                            if (_previewTime + 0.0001f < triggerTime || _previewTime > triggerTime + companionDuration + 0.0001f)
                                continue;

                            for (int effectIndex = 0; effectIndex < damageEffect.companionVfxEffects.Count; effectIndex++)
                            {
                                SkillVfxEffect companionVfx = damageEffect.companionVfxEffects[effectIndex];
                                if (companionVfx?.particlePrefab == null)
                                    continue;

                                if (!IsCompanionCueVisible(eventIndex, damageEffectIndex, effectIndex))
                                    continue;

                                if (!_isPlaying && IsSceneEditingCompanionCue(eventIndex, damageEffectIndex, effectIndex))
                                    continue;

                                string key = GetTimelinePreviewDamageCompanionVfxKey(eventIndex, triggerIndex, damageEffectIndex, effectIndex);
                                activeKeys.Add(key);
                                TimelinePreviewVfxInstance playback = EnsureTimelinePreviewVfxInstance(
                                    key,
                                    companionVfx,
                                    TimelineTrackType.Damage,
                                    eventIndex,
                                    effectIndex,
                                    triggerIndex,
                                    triggerTime,
                                    null,
                                    damageEffect,
                                    true);
                                if (playback != null)
                                    SimulateTimelinePreviewVfx(playback, Mathf.Max(0f, _previewTime - triggerTime));
                            }
                        }

                        if (evt.triggerMode != SkillEventTriggerMode.Repeated)
                            break;
                        triggerIndex++;
                    }
                }
            }

            for (int i = 0; i < damageHits.Count; i++)
            {
                PreviewDamageHitResult hit = damageHits[i];
                if (hit?.effect?.onHitVfxEffects == null || hit.hitTargets == null || hit.hitTargets.Count == 0)
                    continue;

                for (int targetIndex = 0; targetIndex < hit.hitTargets.Count; targetIndex++)
                {
                    Transform target = hit.hitTargets[targetIndex];
                    if (target == null)
                        continue;

                    for (int effectIndex = 0; effectIndex < hit.effect.onHitVfxEffects.Count; effectIndex++)
                    {
                        SkillVfxEffect effect = hit.effect.onHitVfxEffects[effectIndex];
                        float duration = GetVfxPlaybackDuration(effect, hit.hitTime);
                        if (effect?.particlePrefab == null || duration <= 0f)
                            continue;

                        if (!IsOnHitCueVisible(hit.eventIndex, hit.effectIndex, effectIndex))
                            continue;

                        if (!_isPlaying && IsSceneEditingOnHitCue(hit.eventIndex, hit.effectIndex, effectIndex))
                            continue;

                        if (_previewTime + 0.0001f < hit.hitTime || _previewTime > hit.hitTime + duration + 0.0001f)
                            continue;

                        string key = GetTimelinePreviewOnHitVfxKey(hit.eventIndex, hit.triggerIndex, hit.effectIndex, effectIndex, target);
                        activeKeys.Add(key);
                        TimelinePreviewVfxInstance playback = EnsureTimelinePreviewVfxInstance(key, effect, TimelineTrackType.Damage, hit.eventIndex, effectIndex, hit.triggerIndex, hit.hitTime, target);
                        if (playback != null)
                            SimulateTimelinePreviewVfx(playback, Mathf.Max(0f, _previewTime - playback.triggerTime));
                    }
                }
            }

            for (int i = _timelinePreviewVfxInstances.Count - 1; i >= 0; i--)
            {
                TimelinePreviewVfxInstance playback = _timelinePreviewVfxInstances[i];
                if (playback == null)
                {
                    _timelinePreviewVfxInstances.RemoveAt(i);
                    continue;
                }

                if (activeKeys.Contains(playback.key))
                    continue;

                DestroyTimelinePreviewVfxInstance(playback);
                _timelinePreviewVfxInstances.RemoveAt(i);
            }
        }

        private TimelinePreviewVfxInstance EnsureTimelinePreviewVfxInstance(
            string key,
            SkillVfxEffect effect,
            TimelineTrackType trackType,
            int eventIndex,
            int effectIndex,
            int triggerIndex,
            float triggerTime,
            Transform explicitTarget,
            SkillDamageEffect damageEffect = null,
            bool followsDamageWindow = false)
        {
            for (int i = 0; i < _timelinePreviewVfxInstances.Count; i++)
            {
                TimelinePreviewVfxInstance playback = _timelinePreviewVfxInstances[i];
                if (playback == null)
                    continue;

                if (playback.key == key)
                {
                    playback.effect = effect;
                    playback.triggerTime = triggerTime;
                    playback.explicitTarget = explicitTarget;
                    playback.damageEffect = damageEffect;
                    playback.followsDamageWindow = followsDamageWindow;
                    if (followsDamageWindow || effect == null || effect.anchor != CueAnchor.World)
                        playback.hasWorldOrigin = false;
                    EnsureTimelinePreviewCueWorldOrigin(playback);
                    UpdateTimelinePreviewVfxTransform(playback);
                    return playback;
                }
            }

            GameObject instance = Object.Instantiate(effect.particlePrefab);
            if (instance == null)
                return null;

            ApplyHideFlagsRecursively(instance, HideFlags.HideAndDontSave);
            instance.name = $"[TimelinePreview]{effect.particlePrefab.name}";

            TimelinePreviewVfxInstance created = new TimelinePreviewVfxInstance
            {
                key = key,
                trackType = trackType,
                eventIndex = eventIndex,
                effectIndex = effectIndex,
                triggerIndex = triggerIndex,
                triggerTime = triggerTime,
                prefab = effect.particlePrefab,
                instance = instance,
                effect = effect,
                damageEffect = damageEffect,
                explicitTarget = explicitTarget,
                baseScale = instance.transform.localScale,
                followsDamageWindow = followsDamageWindow,
            };
            if (followsDamageWindow)
                ConfigureDamageCompanionPreviewParticles(instance);
            EnsureTimelinePreviewCueWorldOrigin(created);
            _timelinePreviewVfxInstances.Add(created);
            UpdateTimelinePreviewVfxTransform(created);
            return created;
        }

        private static string GetTimelinePreviewVfxKey(int eventIndex, int effectIndex, int triggerIndex)
        {
            return $"{eventIndex}:{effectIndex}:{triggerIndex}";
        }

        private static string GetTimelinePreviewOnHitVfxKey(int eventIndex, int triggerIndex, int damageEffectIndex, int effectIndex, Transform target)
        {
            int targetId = target != null ? target.GetInstanceID() : 0;
            return $"hit:{eventIndex}:{triggerIndex}:{damageEffectIndex}:{effectIndex}:{targetId}";
        }

        private static string GetTimelinePreviewDamageCompanionVfxKey(int eventIndex, int triggerIndex, int damageEffectIndex, int companionEffectIndex)
        {
            return $"body:{eventIndex}:{triggerIndex}:{damageEffectIndex}:{companionEffectIndex}";
        }

        private void UpdateTimelinePreviewVfxTransform(TimelinePreviewVfxInstance playback)
        {
            if (playback?.instance == null || playback.effect == null)
                return;

            Vector3 position;
            Quaternion rotation;
            if (playback.followsDamageWindow)
            {
                if (!TryEvaluatePreviewDamageCompanionTransform(playback, out position, out rotation))
                    return;
            }
            else if (playback.effect.anchor == CueAnchor.World)
            {
                EnsureTimelinePreviewCueWorldOrigin(playback);
                if (!playback.hasWorldOrigin)
                    return;

                EvaluateWorldCueTransform(
                    playback.effect,
                    playback.originPosition,
                    playback.originRotation,
                    Mathf.Max(0f, _previewTime - playback.triggerTime),
                    out position,
                    out rotation);
            }
            else if (!TryEvaluatePreviewCueTransform(playback.effect, playback.explicitTarget, playback.triggerTime, out position, out rotation))
            {
                return;
            }

            playback.instance.transform.position = position;
            playback.instance.transform.rotation = rotation;
            playback.instance.transform.localScale = Vector3.Scale(playback.baseScale, playback.effect.scale);
        }

        private bool TryEvaluatePreviewDamageCompanionTransform(TimelinePreviewVfxInstance playback, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (playback?.damageEffect == null || playback.effect == null)
                return false;

            if (!TryEvaluatePreviewDamageCompanionTransform(playback.damageEffect, playback.effect, playback.triggerTime, _previewTime, out position, out rotation))
                return false;

            return true;
        }

        private bool TryEvaluatePreviewDamageCompanionTransform(SkillDamageEffect damageEffect, SkillVfxEffect cue, float triggerTime, float sampleTime, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (damageEffect == null || cue == null)
                return false;

            if (!TryEvaluatePreviewDamageWindowTransform(damageEffect, triggerTime, sampleTime, out Vector3 bodyPosition, out Quaternion bodyRotation))
                return false;

            position = bodyPosition + bodyRotation * cue.offset;
            rotation = bodyRotation * Quaternion.Euler(cue.rotationEuler);
            return true;
        }

        private bool TryEvaluatePreviewDamageWindowTransform(SkillDamageEffect damageEffect, float triggerTime, float sampleTime, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (_previewTarget == null || damageEffect == null)
                return false;

            Transform preview = _previewTarget.transform;
            if (preview == null)
                return false;

            float elapsed = Mathf.Max(0f, sampleTime - triggerTime);
            Vector3 motionOffset = Vector3.zero;
            Vector3 basisPosition = preview.position;
            Quaternion basisRotation = preview.rotation;
            if (damageEffect.motion != null && damageEffect.motion.IsActive)
            {
                Vector3 motionDirection = damageEffect.motion.direction.sqrMagnitude > 0.0001f
                    ? damageEffect.motion.direction.normalized
                    : Vector3.forward;
                motionOffset = basisRotation * motionDirection * (damageEffect.motion.speed * elapsed);
            }

            switch (damageEffect.detectionType)
            {
                case DamageDetectionType.RangeOverlap:
                    position = basisPosition + basisRotation * damageEffect.centerOffset + motionOffset;
                    rotation = basisRotation * Quaternion.Euler(damageEffect.rotationEuler);
                    return true;
                case DamageDetectionType.Raycast:
                    position = basisPosition + basisRotation * damageEffect.rayOriginOffset + motionOffset;
                    rotation = basisRotation * Quaternion.Euler(damageEffect.rotationEuler);
                    return true;
                default:
                    return false;
            }
        }

        private static void SimulateTimelinePreviewVfx(TimelinePreviewVfxInstance playback, float localTime)
        {
            if (playback?.instance == null)
                return;

            ParticleSystem[] particleSystems = playback.instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Simulate(localTime, false, true, true);
            }
        }

        private static void ConfigureDamageCompanionPreviewParticles(GameObject instance)
        {
            if (instance == null)
                return;

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                var main = particleSystem.main;
                if (main.simulationSpace == ParticleSystemSimulationSpace.World)
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        private void DestroyTimelinePreviewVfxInstances()
        {
            for (int i = _timelinePreviewVfxInstances.Count - 1; i >= 0; i--)
            {
                DestroyTimelinePreviewVfxInstance(_timelinePreviewVfxInstances[i]);
            }
            _timelinePreviewVfxInstances.Clear();
        }

        private static void DestroyTimelinePreviewVfxInstance(TimelinePreviewVfxInstance playback)
        {
            if (playback?.instance != null)
                Object.DestroyImmediate(playback.instance);
        }

        private static void PlayPreviewAudio(AudioClip clip)
        {
            if (clip == null || AudioUtilPlayPreviewClipMethod == null)
                return;

            ParameterInfo[] parameters = AudioUtilPlayPreviewClipMethod.GetParameters();
            if (parameters.Length == 3)
                AudioUtilPlayPreviewClipMethod.Invoke(null, new object[] { clip, 0, false });
            else if (parameters.Length == 2)
                AudioUtilPlayPreviewClipMethod.Invoke(null, new object[] { clip, 0 });
            else if (parameters.Length == 1)
                AudioUtilPlayPreviewClipMethod.Invoke(null, new object[] { clip });
        }

        private static void StopAllTimelinePreviewAudio()
        {
            AudioUtilStopAllPreviewClipsMethod?.Invoke(null, null);
        }

        private void TriggerTimelinePreviewHitSfx()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return;

            List<PreviewDamageHitResult> damageHits = CollectPreviewDamageHitHistory(skill);
            for (int i = 0; i < damageHits.Count; i++)
            {
                PreviewDamageHitResult hit = damageHits[i];
                if (hit?.effect?.onHitSfxEffects == null || hit.hitTargets == null || hit.hitTargets.Count == 0)
                    continue;

                for (int targetIndex = 0; targetIndex < hit.hitTargets.Count; targetIndex++)
                {
                    Transform target = hit.hitTargets[targetIndex];
                    if (target == null)
                        continue;

                    for (int effectIndex = 0; effectIndex < hit.effect.onHitSfxEffects.Count; effectIndex++)
                    {
                        SkillSfxEffect effect = hit.effect.onHitSfxEffects[effectIndex];
                        if (effect?.audioClip == null)
                            continue;
                        if (!IsOnHitSfxVisible(hit.eventIndex, hit.effectIndex, effectIndex))
                            continue;

                        string key = $"{hit.eventIndex}:{hit.triggerIndex}:{hit.effectIndex}:{effectIndex}:{target.GetInstanceID()}";
                        if (_timelinePreviewHitSfxKeys.Add(key))
                            PlayPreviewAudio(effect.audioClip);
                    }
                }
            }
        }

        private void RebuildTimelinePreviewHitSfxKeys()
        {
            _timelinePreviewHitSfxKeys.Clear();

            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return;

            List<PreviewDamageHitResult> damageHits = CollectPreviewDamageHitHistory(skill);
            for (int i = 0; i < damageHits.Count; i++)
            {
                PreviewDamageHitResult hit = damageHits[i];
                if (hit?.effect?.onHitSfxEffects == null || hit.hitTargets == null || hit.hitTargets.Count == 0)
                    continue;

                for (int targetIndex = 0; targetIndex < hit.hitTargets.Count; targetIndex++)
                {
                    Transform target = hit.hitTargets[targetIndex];
                    if (target == null)
                        continue;

                    for (int effectIndex = 0; effectIndex < hit.effect.onHitSfxEffects.Count; effectIndex++)
                    {
                        SkillSfxEffect effect = hit.effect.onHitSfxEffects[effectIndex];
                        if (effect?.audioClip == null)
                            continue;

                        string key = $"{hit.eventIndex}:{hit.triggerIndex}:{hit.effectIndex}:{effectIndex}:{target.GetInstanceID()}";
                        _timelinePreviewHitSfxKeys.Add(key);
                    }
                }
            }
        }

        private void DrawTimelinePreviewDamageOverlays()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null || _previewTarget == null)
                return;

            List<SkillDamageEvent> damageEvents = GetDamageEventList(skill);
            if (damageEvents == null)
                return;

            for (int eventIndex = 0; eventIndex < damageEvents.Count; eventIndex++)
            {
                SkillDamageEvent evt = damageEvents[eventIndex];
                if (evt?.damageEffects == null)
                    continue;

                IteratePreviewTriggerTimes(evt, triggerTime =>
                {
                    float occurrenceEndTime = triggerTime + GetMaxDamageDetectionDuration(evt);
                    if (_previewTime + 0.0001f < triggerTime || _previewTime > occurrenceEndTime + 0.0001f)
                        return;

                    for (int effectIndex = 0; effectIndex < evt.damageEffects.Count; effectIndex++)
                    {
                        SkillDamageEffect effect = evt.damageEffects[effectIndex];
                        if (effect == null)
                            continue;

                        DrawDamageSceneHandle(effect, triggerTime);
                    }
                });
            }
        }

        private void DrawTimelinePreviewStateOverlay()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null || _previewTarget == null)
                return;

            List<PreviewOverlayLine> lines = BuildActivePreviewOverlayLines(skill);
            List<PreviewDamageHitResult> damageHits = CollectCurrentPreviewDamageHits(skill);
            if (lines.Count == 0)
            {
                DrawPreviewPhysicsOverlays(skill, damageHits);
                return;
            }

            Vector3 basePosition = _previewTarget.transform.position + Vector3.up * 2.4f;
            float handleSize = HandleUtility.GetHandleSize(basePosition);
            float lineOffset = handleSize * 0.12f;
            Color oldColor = Handles.color;
            GUIStyle style = new GUIStyle(EditorStyles.whiteMiniLabel)
            {
                fontStyle = FontStyle.Bold
            };

            for (int i = 0; i < lines.Count; i++)
            {
                PreviewOverlayLine line = lines[i];
                Handles.color = line.color;
                style.normal.textColor = line.color;
                DrawSceneTextLabel(basePosition + Vector3.up * (lineOffset * (lines.Count - i)), line.text, style);
            }

            Handles.color = oldColor;
            DrawPreviewPhysicsOverlays(skill, damageHits);
        }

        private void DrawPreviewPhysicsOverlays(SharedSkillDefinition skill, List<PreviewDamageHitResult> damageHits)
        {
            if (skill == null || _previewTarget == null)
                return;

            DrawTopLevelPhysicsPreviewOverlays(skill);
            DrawTopLevelAttributePreviewOverlays(skill);
            DrawDamageHitPreviewOverlays(damageHits);
        }

        private void ApplyPreviewPhysicsTransforms()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null || _previewTarget == null)
                return;

            List<SkillPhysicsEvent> physicsEvents = GetPhysicsEventList(skill);
            if (physicsEvents != null)
            {
                for (int eventIndex = 0; eventIndex < physicsEvents.Count; eventIndex++)
                {
                    SkillPhysicsEvent evt = physicsEvents[eventIndex];
                    if (evt?.physicsEffects == null)
                        continue;

                    int occurrenceIndex = 0;
                    float eventEndTime = evt.GetEndTime();
                    float interval = Mathf.Max(0.01f, evt.repeatInterval);
                    for (float triggerTime = evt.startTime; triggerTime <= eventEndTime + 0.0001f; triggerTime += evt.triggerMode == SkillEventTriggerMode.Repeated ? interval : eventEndTime + 1f)
                    {
                        for (int effectIndex = 0; effectIndex < evt.physicsEffects.Count; effectIndex++)
                        {
                            SkillPhysicsEffect effect = evt.physicsEffects[effectIndex];
                            if (effect == null)
                                continue;

                            ApplyPreviewPhysicsEffectToTarget(_previewTarget.transform, _previewTarget.transform.position, effect, triggerTime);
                        }

                        if (evt.triggerMode != SkillEventTriggerMode.Repeated)
                            break;
                        occurrenceIndex++;
                    }
                }
            }

            List<PreviewDamageHitResult> damageHits = CollectPreviewDamageHitHistory(skill);
            for (int i = 0; i < damageHits.Count; i++)
            {
                PreviewDamageHitResult hit = damageHits[i];
                if (hit?.effect?.onHitPhysicsEffects == null || hit.hitTargets == null)
                    continue;

                for (int targetIndex = 0; targetIndex < hit.hitTargets.Count; targetIndex++)
                {
                    Transform target = hit.hitTargets[targetIndex];
                    if (target == null)
                        continue;

                    for (int effectIndex = 0; effectIndex < hit.effect.onHitPhysicsEffects.Count; effectIndex++)
                    {
                        SkillPhysicsEffect effect = hit.effect.onHitPhysicsEffects[effectIndex];
                        if (effect == null)
                            continue;

                        ApplyPreviewPhysicsEffectToTarget(target, _previewTarget.transform.position, effect, hit.hitTime);
                    }
                }
            }
        }

        private void ApplyPreviewPhysicsEffectToTarget(Transform target, Vector3 attackerPosition, SkillPhysicsEffect effect, float triggerTime)
        {
            if (target == null || effect == null)
                return;

            float progress = GetPreviewPhysicsProgress(triggerTime, GetPreviewPhysicsEffectLifetime(effect, triggerTime));
            if (progress < 0f)
                return;

            CachePreviewPhysicsBasePosition(target);
            Vector3 offset = GetPreviewPhysicsOffset(target, attackerPosition, effect, progress);
            if (offset == Vector3.zero)
                return;

            target.position += offset;
        }

        private float GetPreviewPhysicsProgress(float triggerTime, float duration)
        {
            if (duration > 0f)
            {
                if (_previewTime < triggerTime || _previewTime > triggerTime + duration)
                    return -1f;
                return Mathf.Clamp01((_previewTime - triggerTime) / duration);
            }

            float tolerance = Mathf.Max(0.02f, 0.5f / Mathf.Max(1, _frameRate));
            return Mathf.Abs(_previewTime - triggerTime) <= tolerance ? 1f : -1f;
        }

        private Vector3 GetPreviewPhysicsOffset(Transform target, Vector3 attackerPosition, SkillPhysicsEffect effect, float progress)
        {
            Vector3 origin = _previewPhysicsOriginalPositions.TryGetValue(target, out Vector3 basePos) ? basePos : target.position;
            Vector3 away = (origin - attackerPosition);
            if (away.sqrMagnitude <= 0.0001f)
                away = _previewTarget != null ? _previewTarget.transform.forward : Vector3.forward;
            away.Normalize();

            return effect.effectType switch
            {
                PhysicsEffectType.DashSelf => target.forward * effect.distance * progress,
                PhysicsEffectType.Airborne => Vector3.up * effect.height * progress,
                PhysicsEffectType.Knockback => away * effect.distance * progress,
                PhysicsEffectType.Pull => -away * effect.distance * progress,
                PhysicsEffectType.Launch => away * effect.distance * progress + Vector3.up * effect.height * progress,
                _ => Vector3.zero,
            };
        }

        private void CachePreviewPhysicsBasePosition(Transform target)
        {
            if (target == null || _previewPhysicsOriginalPositions.ContainsKey(target))
                return;

            _previewPhysicsOriginalPositions[target] = target.position;
        }

        private void RestorePreviewPhysicsTransforms()
        {
            if (_previewPhysicsOriginalPositions.Count == 0)
                return;

            foreach (var pair in _previewPhysicsOriginalPositions)
            {
                if (pair.Key != null)
                    pair.Key.position = pair.Value;
            }

            _previewPhysicsOriginalPositions.Clear();
        }

        private void DrawTopLevelPhysicsPreviewOverlays(SharedSkillDefinition skill)
        {
            List<SkillPhysicsEvent> events = GetPhysicsEventList(skill);
            if (events == null)
                return;

            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                SkillPhysicsEvent evt = events[eventIndex];
                if (evt?.physicsEffects == null || !IsCurrentPreviewEvent(evt, TimelineTrackType.Physics))
                    continue;

                for (int effectIndex = 0; effectIndex < evt.physicsEffects.Count; effectIndex++)
                {
                    SkillPhysicsEffect effect = evt.physicsEffects[effectIndex];
                    if (effect == null)
                        continue;

                    DrawPhysicsPreviewVector(_previewTarget.transform.position, _previewTarget.transform.forward, effect, GetTrackColor(TimelineTrackType.Physics), $"顶层物理:{GetPhysicsEffectDisplayName(effect.effectType)}");
                }
            }
        }

        private void DrawDamageHitPreviewOverlays(List<PreviewDamageHitResult> damageHits)
        {
            if (damageHits == null)
                return;

            for (int i = 0; i < damageHits.Count; i++)
            {
                PreviewDamageHitResult hit = damageHits[i];
                if (hit?.effect == null || hit.hitTargets == null)
                    continue;

                for (int targetIndex = 0; targetIndex < hit.hitTargets.Count; targetIndex++)
                {
                    Transform target = hit.hitTargets[targetIndex];
                    if (target == null)
                        continue;

                    Vector3 targetPos = target.position;
                    Color oldColor = Handles.color;
                    Handles.color = GetTrackColor(TimelineTrackType.Damage);
                    Handles.DrawDottedLine(_previewTarget.transform.position, targetPos, 4f);
                    DrawSceneTextLabel(targetPos + Vector3.up * HandleUtility.GetHandleSize(targetPos) * 0.14f, $"命中:{target.name}");
                    Handles.color = oldColor;

                    if (hit.effect.onHitPhysicsEffects != null)
                    {
                        for (int j = 0; j < hit.effect.onHitPhysicsEffects.Count; j++)
                        {
                            SkillPhysicsEffect physics = hit.effect.onHitPhysicsEffects[j];
                            if (physics == null)
                                continue;
                            DrawPhysicsPreviewVector(targetPos, (_previewTarget.transform.position - targetPos).normalized, physics, GetTrackColor(TimelineTrackType.Physics), $"命中物理:{GetPhysicsEffectDisplayName(physics.effectType)}");
                        }
                    }

                    string attrSummary = BuildAttributePreviewSummary(target, hit.effect.onHitAttributeEffects);
                    if (!string.IsNullOrWhiteSpace(attrSummary))
                    {
                        Color oldAttrColor = Handles.color;
                        Handles.color = GetTrackColor(TimelineTrackType.Attribute);
                        DrawSceneTextLabel(targetPos + Vector3.up * HandleUtility.GetHandleSize(targetPos) * 0.28f, $"命中属性:{attrSummary}");
                        Handles.color = oldAttrColor;
                    }

                    string vfxSummary = JoinVfxEffectNames(hit.effect.onHitVfxEffects);
                    if (!string.IsNullOrWhiteSpace(vfxSummary))
                    {
                        Color oldVfxColor = Handles.color;
                        Handles.color = GetTrackColor(TimelineTrackType.Vfx);
                        DrawSceneTextLabel(targetPos + Vector3.up * HandleUtility.GetHandleSize(targetPos) * 0.42f, $"命中特效:{vfxSummary}");
                        Handles.color = oldVfxColor;
                    }

                    string sfxSummary = JoinSfxEffectNames(hit.effect.onHitSfxEffects);
                    if (!string.IsNullOrWhiteSpace(sfxSummary))
                    {
                        Color oldSfxColor = Handles.color;
                        Handles.color = GetTrackColor(TimelineTrackType.Sfx);
                        DrawSceneTextLabel(targetPos + Vector3.up * HandleUtility.GetHandleSize(targetPos) * 0.56f, $"命中音效:{sfxSummary}");
                        Handles.color = oldSfxColor;
                    }
                }
            }
        }

        private void DrawTopLevelAttributePreviewOverlays(SharedSkillDefinition skill)
        {
            List<SkillAttributeEvent> events = GetAttributeEventList(skill);
            if (events == null || _previewTarget == null)
                return;

            Vector3 labelPos = _previewTarget.transform.position + Vector3.up * HandleUtility.GetHandleSize(_previewTarget.transform.position) * 0.42f;
            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                SkillAttributeEvent evt = events[eventIndex];
                if (evt?.attributeEffects == null || !IsCurrentPreviewEvent(evt, TimelineTrackType.Attribute))
                    continue;

                string summary = BuildAttributePreviewSummary(_previewTarget.transform, evt.attributeEffects);
                if (string.IsNullOrWhiteSpace(summary))
                    continue;

                Color oldColor = Handles.color;
                Handles.color = GetTrackColor(TimelineTrackType.Attribute);
                DrawSceneTextLabel(labelPos, $"顶层属性:{summary}");
                Handles.color = oldColor;
                labelPos += Vector3.up * HandleUtility.GetHandleSize(labelPos) * 0.14f;
            }
        }

        private void DrawPhysicsPreviewVector(Vector3 origin, Vector3 referenceDirection, SkillPhysicsEffect effect, Color color, string label)
        {
            if (effect == null)
                return;

            Vector3 direction = Vector3.zero;
            switch (effect.effectType)
            {
                case PhysicsEffectType.DashSelf:
                    direction = _previewTarget != null ? _previewTarget.transform.forward : referenceDirection;
                    break;
                case PhysicsEffectType.Airborne:
                    direction = Vector3.up;
                    break;
                case PhysicsEffectType.Knockback:
                    direction = (origin - _previewTarget.transform.position).normalized;
                    break;
                case PhysicsEffectType.Pull:
                    direction = (_previewTarget.transform.position - origin).normalized;
                    break;
                case PhysicsEffectType.Launch:
                    direction = ((origin - _previewTarget.transform.position).normalized + Vector3.up * Mathf.Max(0.2f, effect.height)).normalized;
                    break;
                case PhysicsEffectType.Stun:
                case PhysicsEffectType.SuperArmor:
                case PhysicsEffectType.Invincible:
                    direction = Vector3.zero;
                    break;
            }

            Color oldColor = Handles.color;
            Handles.color = color;
            float handleSize = HandleUtility.GetHandleSize(origin);

            if (effect.effectType == PhysicsEffectType.Stun
                || effect.effectType == PhysicsEffectType.SuperArmor
                || effect.effectType == PhysicsEffectType.Invincible)
            {
                DrawSceneTextLabel(origin + Vector3.up * handleSize * 0.18f, $"{label} {GetPhysicsEffectDurationText(effect)}");
                Handles.color = oldColor;
                return;
            }

            float length = effect.effectType switch
            {
                PhysicsEffectType.Airborne => Mathf.Max(0.1f, effect.height),
                PhysicsEffectType.Launch => Mathf.Max(0.1f, effect.distance + effect.height),
                _ => Mathf.Max(0.1f, effect.distance),
            };

            Vector3 end = origin + direction.normalized * length;
            Handles.DrawAAPolyLine(3f, origin, end);
            Handles.ConeHandleCap(0, end, Quaternion.LookRotation(direction.normalized == Vector3.zero ? Vector3.forward : direction.normalized), handleSize * 0.08f, EventType.Repaint);
            DrawSceneTextLabel(end + Vector3.up * handleSize * 0.08f, label);
            Handles.color = oldColor;
        }

        private List<PreviewOverlayLine> BuildActivePreviewOverlayLines(SharedSkillDefinition skill)
        {
            var lines = new List<PreviewOverlayLine>();
            if (skill == null)
                return lines;

            List<PreviewDamageHitResult> damageHits = CollectCurrentPreviewDamageHits(skill);
            AppendActivePreviewDamageLines(lines, damageHits);
            AppendActivePreviewLines(lines, GetPhysicsEventList(skill), TimelineTrackType.Physics);
            AppendActivePreviewLines(lines, GetAttributeEventList(skill), TimelineTrackType.Attribute);
            AppendActivePreviewLines(lines, GetVfxEventList(skill), TimelineTrackType.Vfx);
            AppendActivePreviewLines(lines, GetSfxEventList(skill), TimelineTrackType.Sfx);
            AppendPreviewAttributeDeltaLines(lines, skill, damageHits);
            return lines;
        }

        private void AppendPreviewTargetStatLines(List<PreviewOverlayLine> lines)
        {
            if (_previewTarget == null)
                return;

            PlayerController player = _previewTarget.GetComponentInChildren<PlayerController>();
            if (player?.PlayerModel != null)
            {
                Stats stats = player.PlayerModel.Stats;
                lines.Insert(0, new PreviewOverlayLine
                {
                    text = $"[自身属性] HP {player.PlayerModel.CurrentHp:0.#}/{stats.MaxHp:0.#}  MP {player.PlayerModel.CurrentMp:0.#}/{stats.MaxMp:0.#}  攻击 {stats.Attack:0.#}  防御 {stats.Defense:0.#}  移速 {stats.MoveSpeed:0.##}",
                    color = new Color(0.85f, 0.95f, 1f, 1f)
                });
                return;
            }

            EnemyController enemy = _previewTarget.GetComponentInChildren<EnemyController>();
            if (enemy != null)
            {
                lines.Insert(0, new PreviewOverlayLine
                {
                    text = $"[自身属性] HP {enemy.CurrentHp:0.#}/{enemy.MaxHp:0.#}  攻击 {enemy.Attack:0.#}  防御 {enemy.Defense:0.#}  移速 {enemy.MoveSpeed:0.##}",
                    color = new Color(1f, 0.92f, 0.82f, 1f)
                });
            }
        }

        private void AppendPreviewBaseStatLines(List<PreviewOverlayLine> lines, SharedSkillDefinition skill)
        {
            if (lines == null || _previewTarget == null)
                return;

            EnsurePreviewReferenceDatabases();

            string selfBase = BuildBaseStatSummary(_previewTarget.transform);
            if (!string.IsNullOrWhiteSpace(selfBase))
            {
                lines.Insert(Mathf.Min(1, lines.Count), new PreviewOverlayLine
                {
                    text = $"[基础属性(玩家1级/敌人难度1)] {selfBase}",
                    color = new Color(0.92f, 0.92f, 0.92f, 1f)
                });
            }

            List<PreviewDamageHitResult> damageHits = CollectCurrentPreviewDamageHits(skill);
            var seen = new HashSet<Transform>();
            for (int i = 0; i < damageHits.Count; i++)
            {
                PreviewDamageHitResult hit = damageHits[i];
                if (hit?.hitTargets == null)
                    continue;

                for (int targetIndex = 0; targetIndex < hit.hitTargets.Count; targetIndex++)
                {
                    Transform target = hit.hitTargets[targetIndex];
                    if (target == null || !seen.Add(target))
                        continue;

                    string summary = BuildBaseStatSummary(target);
                    if (string.IsNullOrWhiteSpace(summary))
                        continue;

                    lines.Add(new PreviewOverlayLine
                    {
                        text = $"[命中目标基础属性] {target.name} -> {summary}",
                        color = new Color(0.92f, 0.92f, 0.92f, 1f)
                    });
                }
            }
        }

        private void EnsurePreviewReferenceDatabases()
        {
            if (_previewLevelGrowth == null)
                _previewLevelGrowth = Resources.Load<LevelGrowthSO>("配置/玩家成长属性库");
            if (_previewEnemyStatsDb == null)
                _previewEnemyStatsDb = Resources.Load<EnemyStatsDatabaseSO>("配置/敌人属性库");
        }

        private string BuildBaseStatSummary(Transform target)
        {
            if (target == null)
                return string.Empty;

            PlayerController player = target.GetComponentInChildren<PlayerController>();
            if (player != null)
            {
                EnsurePreviewReferenceDatabases();
                var stats = new Stats();
                _previewLevelGrowth?.GetStatsForLevel(1, stats);
                return $"HP {stats.MaxHp:0.#}  MP {stats.MaxMp:0.#}  攻击 {stats.Attack:0.#}  防御 {stats.Defense:0.#}  移速 {stats.MoveSpeed:0.##}";
            }

            EnemyController enemy = target.GetComponentInChildren<EnemyController>();
            if (enemy != null && _previewEnemyStatsDb != null)
            {
                EnemyStatsEntry entry = _previewEnemyStatsDb.GetEntry(enemy.EnemyId) ?? _previewEnemyStatsDb.GetEntry(enemy.EnemyCategory);
                if (entry != null)
                    return $"HP {entry.baseHp:0.#}  攻击 {entry.baseAttack:0.#}  防御 {entry.baseDefense:0.#}  移速 {entry.baseMoveSpeed:0.##}";
            }

            return string.Empty;
        }

        private void AppendPreviewAttributeDeltaLines(List<PreviewOverlayLine> lines, SharedSkillDefinition skill, List<PreviewDamageHitResult> damageHits)
        {
            if (_previewTarget == null || skill == null)
                return;

            List<SkillAttributeEvent> attributeEvents = GetAttributeEventList(skill);
            if (attributeEvents != null)
            {
                for (int eventIndex = 0; eventIndex < attributeEvents.Count; eventIndex++)
                {
                    SkillAttributeEvent evt = attributeEvents[eventIndex];
                    if (evt?.attributeEffects == null || !IsCurrentPreviewEvent(evt, TimelineTrackType.Attribute))
                        continue;

                    string summary = BuildAttributePreviewSummary(_previewTarget.transform, evt.attributeEffects);
                    if (string.IsNullOrWhiteSpace(summary))
                        continue;

                    lines.Add(new PreviewOverlayLine
                    {
                        text = $"[属性变化] 自身 -> {summary}",
                        color = GetTrackColor(TimelineTrackType.Attribute)
                    });
                }
            }

            if (damageHits == null)
                return;

            for (int i = 0; i < damageHits.Count; i++)
            {
                PreviewDamageHitResult hit = damageHits[i];
                if (hit?.effect?.onHitAttributeEffects == null || hit.hitTargets == null)
                    continue;

                for (int targetIndex = 0; targetIndex < hit.hitTargets.Count; targetIndex++)
                {
                    Transform target = hit.hitTargets[targetIndex];
                    if (target == null)
                        continue;

                    string summary = BuildAttributePreviewSummary(target, hit.effect.onHitAttributeEffects);
                    if (string.IsNullOrWhiteSpace(summary))
                        continue;

                    lines.Add(new PreviewOverlayLine
                    {
                        text = $"[命中属性变化] {target.name} -> {summary}",
                        color = GetTrackColor(TimelineTrackType.Attribute)
                    });
                }
            }
        }

        private void AppendActivePreviewDamageLines(List<PreviewOverlayLine> lines, List<PreviewDamageHitResult> damageHits)
        {
            if (damageHits == null || damageHits.Count == 0)
                return;

            for (int i = 0; i < damageHits.Count; i++)
            {
                PreviewDamageHitResult hit = damageHits[i];
                if (hit == null || hit.effect == null)
                    continue;

                string targetNames = hit.hitTargets.Count > 0
                    ? string.Join("、", hit.hitTargets.ConvertAll(t => t != null ? t.name : "未知目标"))
                    : "未命中";

                string subSummary = BuildOnHitEffectSummary(hit.effect);
                string text = $"[命中轨] {GetDamageDetectionDisplayName(hit.effect)} -> {targetNames}";
                if (!string.IsNullOrWhiteSpace(subSummary))
                    text += $" / {subSummary}";

                lines.Add(new PreviewOverlayLine
                {
                    text = text,
                    color = GetTrackColor(TimelineTrackType.Damage)
                });
            }
        }

        private void AppendActivePreviewLines<T>(List<PreviewOverlayLine> lines, List<T> events, TimelineTrackType trackType) where T : SkillTimedEventBase
        {
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                T evt = events[i];
                if (evt == null || !IsCurrentPreviewEvent(evt, trackType))
                    continue;

                string summary = BuildPreviewTrackSummary(evt, trackType);
                if (string.IsNullOrWhiteSpace(summary))
                    summary = GetTrackHeaderLabel(trackType);

                lines.Add(new PreviewOverlayLine
                {
                    text = $"[{GetTrackHeaderLabel(trackType)}] {summary}",
                    color = GetTrackColor(trackType)
                });
            }
        }

        private List<PreviewDamageHitResult> CollectCurrentPreviewDamageHits(SharedSkillDefinition skill)
        {
            var results = new List<PreviewDamageHitResult>();
            if (skill == null || _previewTarget == null)
                return results;

            List<SkillDamageEvent> damageEvents = GetDamageEventList(skill);
            if (damageEvents == null)
                return results;

            for (int eventIndex = 0; eventIndex < damageEvents.Count; eventIndex++)
            {
                SkillDamageEvent evt = damageEvents[eventIndex];
                if (evt?.damageEffects == null)
                    continue;

                int occurrenceIndex = 0;
                float eventEndTime = evt.GetEndTime();
                float interval = Mathf.Max(0.01f, evt.repeatInterval);
                for (float triggerTime = evt.startTime; triggerTime <= eventEndTime + 0.0001f; triggerTime += evt.triggerMode == SkillEventTriggerMode.Repeated ? interval : eventEndTime + 1f)
                {
                    if (!IsCurrentDamageOccurrence(evt, triggerTime))
                    {
                        if (evt.triggerMode != SkillEventTriggerMode.Repeated)
                            break;
                        occurrenceIndex++;
                        continue;
                    }

                    for (int effectIndex = 0; effectIndex < evt.damageEffects.Count; effectIndex++)
                    {
                        SkillDamageEffect effect = evt.damageEffects[effectIndex];
                        if (effect == null)
                            continue;

                        results.AddRange(CollectPreviewDamageHitsForOccurrence(
                            eventIndex,
                            effectIndex,
                            occurrenceIndex,
                            triggerTime,
                            effect,
                            currentOnly: true));
                    }

                    if (evt.triggerMode != SkillEventTriggerMode.Repeated)
                        break;
                    occurrenceIndex++;
                }
            }

            return results;
        }

        private List<PreviewDamageHitResult> CollectPreviewDamageHitHistory(SharedSkillDefinition skill)
        {
            var results = new List<PreviewDamageHitResult>();
            if (skill == null || _previewTarget == null)
                return results;

            List<SkillDamageEvent> damageEvents = GetDamageEventList(skill);
            if (damageEvents == null)
                return results;

            for (int eventIndex = 0; eventIndex < damageEvents.Count; eventIndex++)
            {
                SkillDamageEvent evt = damageEvents[eventIndex];
                if (evt?.damageEffects == null)
                    continue;

                int occurrenceIndex = 0;
                IteratePreviewTriggerTimes(evt, triggerTime =>
                {
                    for (int effectIndex = 0; effectIndex < evt.damageEffects.Count; effectIndex++)
                    {
                        SkillDamageEffect effect = evt.damageEffects[effectIndex];
                        if (effect == null)
                            continue;

                        results.AddRange(CollectPreviewDamageHitsForOccurrence(
                            eventIndex,
                            effectIndex,
                            occurrenceIndex,
                            triggerTime,
                            effect,
                            currentOnly: false));
                    }

                    occurrenceIndex++;
                });
            }

            return results;
        }

        private List<PreviewDamageHitResult> CollectPreviewDamageHitsForOccurrence(
            int eventIndex,
            int effectIndex,
            int triggerIndex,
            float triggerTime,
            SkillDamageEffect effect,
            bool currentOnly)
        {
            var results = new List<PreviewDamageHitResult>();
            if (effect == null || _previewTarget == null || _previewTime + 0.0001f < triggerTime)
                return results;

            Transform explicitVictim = ResolveRootPreviewTarget(_previewVictimTarget)?.transform;
            float tolerance = Mathf.Max(0.02f, 0.5f / Mathf.Max(1, _frameRate));
            float sampleStep = 1f / Mathf.Max(1, _frameRate);
            float sampleEndTime = effect.detectionDuration > 0f
                ? Mathf.Min(_previewTime, triggerTime + effect.detectionDuration)
                : triggerTime;
            float currentWindowStart = Mathf.Max(triggerTime, _previewTime - sampleStep - tolerance);
            var firstHitTimes = new Dictionary<Transform, float>();

            for (float sampleTime = triggerTime; sampleTime <= sampleEndTime + 0.0001f; sampleTime += effect.detectionDuration > 0f ? sampleStep : sampleEndTime + 1f)
            {
                List<Transform> hitTargets = RunEditorPreviewDetection(effect, triggerTime, sampleTime, explicitVictim);
                for (int targetIndex = 0; targetIndex < hitTargets.Count; targetIndex++)
                {
                    Transform target = hitTargets[targetIndex];
                    if (target == null || firstHitTimes.ContainsKey(target))
                        continue;

                    firstHitTimes.Add(target, sampleTime);
                }

                if (effect.detectionDuration <= 0f)
                    break;
            }

            foreach (var pair in firstHitTimes)
            {
                bool include = !currentOnly || (pair.Value >= currentWindowStart - 0.0001f && pair.Value <= _previewTime + 0.0001f);
                if (!include)
                    continue;

                results.Add(new PreviewDamageHitResult
                {
                    eventIndex = eventIndex,
                    effectIndex = effectIndex,
                    triggerIndex = triggerIndex,
                    triggerTime = triggerTime,
                    hitTime = pair.Value,
                    effect = effect,
                    hitTargets = new List<Transform> { pair.Key }
                });
            }

            return results;
        }

        private bool IsCurrentDamageOccurrence(SkillDamageEvent evt, float triggerTime)
        {
            if (evt == null)
                return false;

            float endTime = triggerTime + GetMaxDamageDetectionDuration(evt);
            if (endTime > triggerTime)
                return _previewTime >= triggerTime && _previewTime <= endTime;

            float tolerance = Mathf.Max(0.02f, 0.5f / Mathf.Max(1, _frameRate));
            return Mathf.Abs(_previewTime - triggerTime) <= tolerance;
        }

        private List<Transform> RunEditorPreviewDetection(SkillDamageEffect effect, float triggerTime, float sampleTime, Transform explicitVictim)
        {
            var targets = new List<Transform>();
            if (_previewTarget == null || effect == null)
                return targets;

            // Scene 视图里直接拖动物体后，先同步物理世界，避免预览命中仍读取旧碰撞体位置。
            Physics.SyncTransforms();

            var buffer = new Collider[32];
            int hitCount = 0;
            bool ranDetection = false;

            if (effect.detectionType == DamageDetectionType.Collision)
            {
                hitCount = RunEditorPreviewCollisionDetection(effect, buffer);
                ranDetection = true;
            }
            else
            {
                SkillDetectionMotionFrame? motionFrame = CreateEditorPreviewMotionFrame(effect, triggerTime, sampleTime);
                ranDetection = DamageDetectionRunner.TryRunDetection(effect, _previewTarget.transform, null, null, buffer, motionFrame, out hitCount);
            }

            if (!ranDetection || hitCount <= 0)
                return targets;

            Transform previewRoot = _previewTarget.transform.root;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = buffer[i];
                if (hit == null)
                    continue;

                Transform root = ResolvePreviewHitTargetRoot(hit.transform);
                if (root == null || root == previewRoot || targets.Contains(root))
                    continue;
                if (explicitVictim != null && root != explicitVictim)
                    continue;

                targets.Add(root);
            }

            return targets;
        }

        private SkillDetectionMotionFrame? CreateEditorPreviewMotionFrame(SkillDamageEffect effect, float triggerTime, float sampleTime)
        {
            if (_previewTarget == null || effect?.motion == null || !effect.motion.IsActive)
                return null;

            float elapsed = Mathf.Max(0f, sampleTime - triggerTime);
            return new SkillDetectionMotionFrame(_previewTarget.transform.position, _previewTarget.transform.rotation, elapsed);
        }

        private bool TryGetCurrentSelectedDamageTriggerTime(out float triggerTime)
        {
            triggerTime = 0f;
            if (_selectedEventTrackType != TimelineTrackType.Damage)
                return false;

            if (GetSelectedTimedEvent() is not SkillDamageEvent damageEvent)
                return false;

            return TryGetDamageEventTriggerTime(damageEvent, out triggerTime);
        }

        private bool TryGetActiveSceneDamageTriggerTime(out float triggerTime)
        {
            triggerTime = 0f;
            return TryGetSceneDamageTriggerTime(_activeSceneDamageEventIndex, out triggerTime);
        }

        private bool TryGetSceneDamageTriggerTime(int damageEventIndex, out float triggerTime)
        {
            triggerTime = 0f;
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return false;

            if (GetTrackEvent(skill, TimelineTrackType.Damage, damageEventIndex) is not SkillDamageEvent damageEvent)
                return false;

            return TryGetDamageEventTriggerTime(damageEvent, out triggerTime);
        }

        private bool TryGetDamageEventTriggerTime(SkillDamageEvent damageEvent, out float triggerTime)
        {
            triggerTime = 0f;
            if (damageEvent == null)
                return false;

            bool found = false;
            float resolvedTriggerTime = 0f;
            IteratePreviewTriggerTimes(damageEvent, time =>
            {
                if (IsCurrentDamageOccurrence(damageEvent, time))
                {
                    resolvedTriggerTime = time;
                    found = true;
                }
            });
            if (found)
                triggerTime = resolvedTriggerTime;
            return found;
        }

        private int RunEditorPreviewCollisionDetection(SkillDamageEffect effect, Collider[] outBuffer)
        {
            if (_previewTarget == null || effect == null || outBuffer == null || string.IsNullOrWhiteSpace(effect.colliderNodeName))
                return 0;

            Transform node = FindChildRecursive(_previewTarget.transform, effect.colliderNodeName);
            if (node == null)
                return 0;

            Collider collider = node.GetComponent<Collider>();
            if (collider == null)
                collider = node.GetComponentInChildren<Collider>(true);
            if (collider == null)
                return 0;

            int layerMask = LayerMask.GetMask(effect.hitLayerName);
            if (layerMask == 0)
                return 0;

            if (collider is BoxCollider box)
            {
                Vector3 lossyScale = collider.transform.lossyScale;
                Vector3 size = Vector3.Scale(box.size, new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
                return Physics.OverlapBoxNonAlloc(collider.transform.TransformPoint(box.center), size * 0.5f, outBuffer, collider.transform.rotation, layerMask, QueryTriggerInteraction.Collide);
            }

            if (collider is SphereCollider sphere)
            {
                float radius = sphere.radius * Mathf.Max(Mathf.Abs(collider.transform.lossyScale.x), Mathf.Abs(collider.transform.lossyScale.y), Mathf.Abs(collider.transform.lossyScale.z));
                return Physics.OverlapSphereNonAlloc(collider.transform.TransformPoint(sphere.center), radius, outBuffer, layerMask, QueryTriggerInteraction.Collide);
            }

            Bounds bounds = collider.bounds;
            return Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, outBuffer, collider.transform.rotation, layerMask, QueryTriggerInteraction.Collide);
        }

        private static Transform ResolvePreviewHitTargetRoot(Transform target)
        {
            if (target == null)
                return null;

            PlayerController player = target.GetComponentInParent<PlayerController>();
            if (player != null)
                return player.transform;

            EnemyController enemy = target.GetComponentInParent<EnemyController>();
            if (enemy != null)
                return enemy.transform;

            return target.root;
        }

        private string BuildPreviewTrackSummary(SkillTimedEventBase evt, TimelineTrackType trackType)
        {
            switch (trackType)
            {
                case TimelineTrackType.Damage:
                    if (evt is SkillDamageEvent damageEvent)
                    {
                        var parts = new List<string>();
                        if (damageEvent.damageEffects != null)
                        {
                            for (int i = 0; i < damageEvent.damageEffects.Count; i++)
                            {
                                SkillDamageEffect effect = damageEvent.damageEffects[i];
                                if (effect == null)
                                    continue;

                                effect.TryMigrateLegacySubEffects();

                                string detectionLabel = effect.detectionType switch
                                {
                                    DamageDetectionType.RangeOverlap => effect.shape switch
                                    {
                                        AttackShapeType.Sphere => "球体检测",
                                        AttackShapeType.Sector => "扇形检测",
                                        AttackShapeType.Box => "盒体检测",
                                        _ => "范围检测",
                                    },
                                    DamageDetectionType.Raycast => "射线检测",
                                    DamageDetectionType.Collision => $"碰撞命中盒:{effect.colliderNodeName}",
                                    _ => "命中检测",
                                };

                                int onHitCount = (effect.onHitPhysicsEffects?.Count ?? 0)
                                    + (effect.onHitAttributeEffects?.Count ?? 0)
                                    + (effect.onHitVfxEffects?.Count ?? 0)
                                    + (effect.onHitSfxEffects?.Count ?? 0);
                                if (onHitCount > 0)
                                    detectionLabel += $" / 命中附加{onHitCount}";

                                string companionVfxName = JoinVfxEffectNames(effect.companionVfxEffects);
                                if (effect.detectionType != DamageDetectionType.Collision && !string.IsNullOrWhiteSpace(companionVfxName))
                                    detectionLabel += $" / 伴随特效效果:{companionVfxName}";

                                parts.Add(detectionLabel);
                            }
                        }

                        return string.Join("；", parts);
                    }
                    break;

                case TimelineTrackType.Physics:
                    if (evt is SkillPhysicsEvent physicsEvent)
                        return JoinPhysicsEffectNames(physicsEvent.physicsEffects);
                    break;

                case TimelineTrackType.Attribute:
                    if (evt is SkillAttributeEvent attributeEvent)
                        return JoinAttributeEffectNames(attributeEvent.attributeEffects);
                    break;

                case TimelineTrackType.Vfx:
                    if (evt is SkillVfxEvent vfxEvent)
                        return JoinVfxEffectNames(vfxEvent.vfxEffects);
                    break;

                case TimelineTrackType.Sfx:
                    if (evt is SkillSfxEvent sfxEvent)
                        return JoinSfxEffectNames(sfxEvent.sfxEffects);
                    break;
            }

            return string.Empty;
        }

        private static string JoinPhysicsEffectNames(List<SkillPhysicsEffect> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var names = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillPhysicsEffect effect = effects[i];
                if (effect != null)
                    names.Add(GetPhysicsEffectDisplayName(effect.effectType));
            }

            return string.Join("、", names);
        }

        private static string JoinAttributeEffectNames(List<SkillAttributeEffect> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var names = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillAttributeEffect effect = effects[i];
                if (effect != null)
                {
                    string valueText = effect.usePercent
                        ? $"{effect.magnitude * 100f:+0.##;-0.##;0}%"
                        : $"{effect.magnitude:+0.##;-0.##;0}";
                    string text = $"{GetStatFieldDisplayName(effect.statField)} {valueText}";
                    string durationSuffix = GetAttributeEffectDurationSuffix(effect);
                    if (!string.IsNullOrEmpty(durationSuffix))
                        text += durationSuffix;
                    names.Add(text);
                }
            }

            return string.Join("、", names);
        }

        private string BuildAttributePreviewSummary(Transform target, List<SkillAttributeEffect> effects)
        {
            if (target == null || effects == null || effects.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillAttributeEffect effect = effects[i];
                if (effect == null)
                    continue;

                string part = BuildSingleAttributePreviewText(target, effect);
                if (!string.IsNullOrWhiteSpace(part))
                    parts.Add(part);
            }

            return string.Join("、", parts);
        }

        private string BuildSingleAttributePreviewText(Transform target, SkillAttributeEffect effect)
        {
            if (effect == null)
                return string.Empty;

            string fieldName = GetStatFieldDisplayName(effect.statField);
            string durationSuffix = GetAttributeEffectDurationSuffix(effect);

            if (TryGetPreviewStatValue(target, effect.statField, out float currentValue, out float referenceValue))
            {
                float delta = effect.usePercent ? referenceValue * effect.magnitude : effect.magnitude;
                float after = currentValue + delta;
                string valueText = effect.usePercent
                    ? $"{effect.magnitude * 100f:+0.##;-0.##;0}%"
                    : $"{delta:+0.##;-0.##;0}";
                return $"{fieldName} {valueText} ({currentValue:0.##}->{after:0.##}){durationSuffix}";
            }

            string fallback = effect.usePercent
                ? $"{effect.magnitude * 100f:+0.##;-0.##;0}%"
                : $"{effect.magnitude:+0.##;-0.##;0}";
            return $"{fieldName} {fallback}{durationSuffix}";
        }

        private bool TryGetPreviewStatValue(Transform target, SkillStatField statField, out float currentValue, out float referenceValue)
        {
            currentValue = 0f;
            referenceValue = 0f;
            if (target == null)
                return false;

            target = ResolvePreviewStatTarget(target);
            PlayerController player = target.GetComponentInChildren<PlayerController>();
            if (player?.PlayerModel != null)
            {
                Stats stats = player.PlayerModel.Stats;
                switch (statField)
                {
                    case SkillStatField.HP:
                        currentValue = player.PlayerModel.CurrentHp;
                        referenceValue = stats.MaxHp;
                        return true;
                    case SkillStatField.MP:
                        currentValue = player.PlayerModel.CurrentMp;
                        referenceValue = stats.MaxMp;
                        return true;
                    case SkillStatField.Attack:
                        currentValue = stats.Attack;
                        referenceValue = stats.Attack;
                        return true;
                    case SkillStatField.Defense:
                        currentValue = stats.Defense;
                        referenceValue = stats.Defense;
                        return true;
                    case SkillStatField.MoveSpeed:
                        currentValue = stats.MoveSpeed;
                        referenceValue = stats.MoveSpeed;
                        return true;
                    case SkillStatField.HPRegen:
                        currentValue = stats.HpRegen;
                        referenceValue = stats.HpRegen;
                        return true;
                    case SkillStatField.MPRegen:
                        currentValue = stats.MpRegen;
                        referenceValue = stats.MpRegen;
                        return true;
                    case SkillStatField.CritRate:
                        currentValue = stats.CritRate;
                        referenceValue = stats.CritRate;
                        return true;
                    case SkillStatField.CritDamage:
                        currentValue = stats.CritDmg;
                        referenceValue = stats.CritDmg;
                        return true;
                    case SkillStatField.AttackSpeed:
                        currentValue = stats.AttackSpeed;
                        referenceValue = stats.AttackSpeed;
                        return true;
                    case SkillStatField.SkillDamage:
                        currentValue = stats.DamageBonus;
                        referenceValue = stats.DamageBonus;
                        return true;
                    case SkillStatField.DamageReduce:
                        currentValue = stats.DamageReduce;
                        referenceValue = stats.DamageReduce;
                        return true;
                    case SkillStatField.LifeSteal:
                        currentValue = stats.LifeSteal;
                        referenceValue = stats.LifeSteal;
                        return true;
                }
            }

            EnemyController enemy = target.GetComponentInChildren<EnemyController>();
            if (enemy != null)
            {
                switch (statField)
                {
                    case SkillStatField.HP:
                        currentValue = enemy.CurrentHp;
                        referenceValue = enemy.MaxHp;
                        return true;
                    case SkillStatField.Attack:
                        currentValue = enemy.Attack;
                        referenceValue = enemy.Attack;
                        return true;
                    case SkillStatField.Defense:
                        currentValue = enemy.Defense;
                        referenceValue = enemy.Defense;
                        return true;
                    case SkillStatField.MoveSpeed:
                        currentValue = enemy.MoveSpeed;
                        referenceValue = enemy.MoveSpeed;
                        return true;
                }
            }

            return false;
        }

        private static Transform ResolvePreviewStatTarget(Transform target)
        {
            if (target == null)
                return null;

            Transform root = target.root != null ? target.root : target;
            if (root.GetComponentInChildren<PlayerController>() != null || root.GetComponentInChildren<EnemyController>() != null)
                return root;

            return target;
        }

        private static string GetPhysicsEffectDisplayName(PhysicsEffectType effectType)
        {
            return effectType switch
            {
                PhysicsEffectType.Knockback => "击退",
                PhysicsEffectType.Pull => "拉拽",
                PhysicsEffectType.Launch => "击飞",
                PhysicsEffectType.DashSelf => "位移",
                PhysicsEffectType.Stun => "眩晕",
                PhysicsEffectType.Airborne => "腾空",
                PhysicsEffectType.SuperArmor => "霸体",
                PhysicsEffectType.Invincible => "无敌",
                _ => effectType.ToString(),
            };
        }

        private static string GetPhysicsEffectDurationText(SkillPhysicsEffect effect)
        {
            if (effect == null)
                return string.Empty;

            if (effect.durationMode == SkillEffectDurationMode.UntilStateExit && effect.SupportsUntilStateExitDuration)
                return "状态退出";

            return $"{effect.duration:0.##}s";
        }

        private static string GetAttributeEffectDurationSuffix(SkillAttributeEffect effect)
        {
            if (effect == null || !effect.UsesDuration)
                return string.Empty;

            if (effect.durationMode == SkillEffectDurationMode.UntilStateExit)
                return " 状态退出";

            return effect.duration > 0f ? $" {effect.duration:0.##}s" : string.Empty;
        }

        private static string GetStatFieldDisplayName(SkillStatField statField)
        {
            return statField switch
            {
                SkillStatField.HP => "生命",
                SkillStatField.MP => "法力",
                SkillStatField.Attack => "攻击",
                SkillStatField.Defense => "防御",
                SkillStatField.MoveSpeed => "移速",
                SkillStatField.HPRegen => "生命回复",
                SkillStatField.MPRegen => "法力回复",
                SkillStatField.CritRate => "暴击率",
                SkillStatField.CritDamage => "暴击伤害",
                SkillStatField.AttackSpeed => "攻速",
                SkillStatField.SkillDamage => "增伤",
                SkillStatField.DamageReduce => "减伤",
                SkillStatField.LifeSteal => "吸血",
                _ => statField.ToString(),
            };
        }

        private static string GetDamageDetectionDisplayName(SkillDamageEffect effect)
        {
            if (effect == null)
                return "命中检测";

            return effect.detectionType switch
            {
                DamageDetectionType.RangeOverlap => effect.shape switch
                {
                    AttackShapeType.Sphere => "球体检测",
                    AttackShapeType.Sector => "扇形检测",
                    AttackShapeType.Box => "盒体检测",
                    _ => "范围检测",
                },
                DamageDetectionType.Raycast => "射线检测",
                DamageDetectionType.Collision => string.IsNullOrWhiteSpace(effect.colliderNodeName)
                    ? "碰撞命中盒"
                    : $"碰撞命中盒:{effect.colliderNodeName}",
                _ => "命中检测",
            };
        }

        private static string BuildOnHitEffectSummary(SkillDamageEffect effect)
        {
            if (effect == null)
                return string.Empty;

            effect.TryMigrateLegacySubEffects();
            var parts = new List<string>();
            if (effect.onHitDamageEffects != null && effect.onHitDamageEffects.Count > 0)
                parts.Add($"命中伤害:{JoinHitDamageEffectValues(effect.onHitDamageEffects)}");
            if (effect.onHitVfxEffects != null && effect.onHitVfxEffects.Count > 0)
                parts.Add($"命中特效:{JoinVfxEffectNames(effect.onHitVfxEffects)}");
            if (effect.onHitSfxEffects != null && effect.onHitSfxEffects.Count > 0)
                parts.Add($"命中音效:{JoinSfxEffectNames(effect.onHitSfxEffects)}");
            if (effect.onHitPhysicsEffects != null && effect.onHitPhysicsEffects.Count > 0)
                parts.Add($"命中物理:{JoinPhysicsEffectNames(effect.onHitPhysicsEffects)}");
            if (effect.onHitAttributeEffects != null && effect.onHitAttributeEffects.Count > 0)
                parts.Add($"命中属性:{JoinAttributeEffectNames(effect.onHitAttributeEffects)}");
            if (SkillDamageEffect.HasConfiguredHitStopEffect(effect.onHitStopEffect))
                parts.Add($"命中停顿:{FormatHitStopEffectName(effect.onHitStopEffect)}");
            return string.Join(" / ", parts);
        }

        private static string JoinHitDamageEffectValues(List<SkillHitDamageEffect> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var values = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillHitDamageEffect effect = effects[i];
                if (effect != null)
                    values.Add(effect.damageMagnitude.ToString("0.##"));
            }

            return string.Join("、", values);
        }

        private static string FormatHitStopEffectName(SkillHitStopEffect effect)
        {
            if (!SkillDamageEffect.HasConfiguredHitStopEffect(effect))
                return string.Empty;

            return $"{effect.hitStopDuration:0.##}s@{effect.hitStopTimeScale:0.##}";
        }

        private static string JoinVfxEffectNames(List<SkillVfxEffect> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var names = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillVfxEffect effect = effects[i];
                if (effect?.particlePrefab != null)
                    names.Add(effect.particlePrefab.name);
            }

            return string.Join("、", names);
        }

        private static string JoinSfxEffectNames(List<SkillSfxEffect> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var names = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillSfxEffect effect = effects[i];
                if (effect?.audioClip != null)
                    names.Add(effect.audioClip.name);
            }

            return string.Join("、", names);
        }
        private SharedSkillDefinition GetSelectedSkill()
        {
            List<SharedSkillDefinition> visibleSkills = GetVisibleSkillList();
            if (visibleSkills == null)
                return null;
            if (_selectedSkillVariantIndex < 0 || _selectedSkillVariantIndex >= visibleSkills.Count)
                return null;
            return visibleSkills[_selectedSkillVariantIndex];
        }

        private void SelectEvent(TimelineTrackType trackType, int eventIndex)
        {
            _selectedEventTrackType = trackType;
            _selectedEventIndex = eventIndex;
        }

        private void ClearSelectedEvent()
        {
            _selectedEventTrackType = TimelineTrackType.Damage;
            _selectedEventIndex = -1;
            _activeSceneDamageEventIndex = -1;
            _activeSceneDamageIndex = -1;
            _activeSceneCueEventIndex = -1;
            _activeSceneCueIndex = -1;
            _activeSceneHitVfxEventIndex = -1;
            _activeSceneHitVfxDamageIndex = -1;
            _activeSceneHitVfxIndex = -1;
            _activeSceneCompanionVfxEventIndex = -1;
            _activeSceneCompanionVfxDamageIndex = -1;
            _activeSceneCompanionVfxIndex = -1;
            DestroyScenePreviewCueInstance();
        }

        private List<SkillDamageEvent> GetDamageEventList(SharedSkillDefinition skill)
        {
            if (skill == null)
                return null;
            if (skill.damageEvents == null)
                skill.damageEvents = new List<SkillDamageEvent>();
            return skill.damageEvents;
        }

        private List<SkillPhysicsEvent> GetPhysicsEventList(SharedSkillDefinition skill)
        {
            if (skill == null)
                return null;
            if (skill.physicsEvents == null)
                skill.physicsEvents = new List<SkillPhysicsEvent>();
            return skill.physicsEvents;
        }

        private List<SkillAttributeEvent> GetAttributeEventList(SharedSkillDefinition skill)
        {
            if (skill == null)
                return null;
            if (skill.attributeEvents == null)
                skill.attributeEvents = new List<SkillAttributeEvent>();
            return skill.attributeEvents;
        }

        private List<SkillVfxEvent> GetVfxEventList(SharedSkillDefinition skill)
        {
            if (skill == null)
                return null;
            if (skill.vfxEvents == null)
                skill.vfxEvents = new List<SkillVfxEvent>();
            return skill.vfxEvents;
        }

        private List<SkillSfxEvent> GetSfxEventList(SharedSkillDefinition skill)
        {
            if (skill == null)
                return null;
            if (skill.sfxEvents == null)
                skill.sfxEvents = new List<SkillSfxEvent>();
            return skill.sfxEvents;
        }

        private int GetTrackEventCount(SharedSkillDefinition skill, TimelineTrackType trackType)
        {
            return trackType switch
            {
                TimelineTrackType.Damage => GetDamageEventList(skill)?.Count ?? 0,
                TimelineTrackType.Physics => GetPhysicsEventList(skill)?.Count ?? 0,
                TimelineTrackType.Attribute => GetAttributeEventList(skill)?.Count ?? 0,
                TimelineTrackType.Vfx => GetVfxEventList(skill)?.Count ?? 0,
                TimelineTrackType.Sfx => GetSfxEventList(skill)?.Count ?? 0,
                _ => 0,
            };
        }

        private int GetTotalEventCount(SharedSkillDefinition skill)
        {
            if (skill == null)
                return 0;

            return GetTrackEventCount(skill, TimelineTrackType.Damage)
                + GetTrackEventCount(skill, TimelineTrackType.Physics)
                + GetTrackEventCount(skill, TimelineTrackType.Attribute)
                + GetTrackEventCount(skill, TimelineTrackType.Vfx)
                + GetTrackEventCount(skill, TimelineTrackType.Sfx);
        }

        private SkillTimedEventBase GetTrackEvent(SharedSkillDefinition skill, TimelineTrackType trackType, int eventIndex)
        {
            if (skill == null || eventIndex < 0)
                return null;

            return trackType switch
            {
                TimelineTrackType.Damage => eventIndex < GetDamageEventList(skill).Count ? GetDamageEventList(skill)[eventIndex] : null,
                TimelineTrackType.Physics => eventIndex < GetPhysicsEventList(skill).Count ? GetPhysicsEventList(skill)[eventIndex] : null,
                TimelineTrackType.Attribute => eventIndex < GetAttributeEventList(skill).Count ? GetAttributeEventList(skill)[eventIndex] : null,
                TimelineTrackType.Vfx => eventIndex < GetVfxEventList(skill).Count ? GetVfxEventList(skill)[eventIndex] : null,
                TimelineTrackType.Sfx => eventIndex < GetSfxEventList(skill).Count ? GetSfxEventList(skill)[eventIndex] : null,
                _ => null,
            };
        }

        private SkillTimedEventBase GetSelectedTimedEvent()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return null;
            return GetTrackEvent(skill, _selectedEventTrackType, _selectedEventIndex);
        }

        private List<TimelineEventHandle> GetTrackEventHandles(SharedSkillDefinition skill, TimelineTrackType trackType)
        {
            var handles = new List<TimelineEventHandle>();
            if (skill == null)
                return handles;

            int count = GetTrackEventCount(skill, trackType);
            for (int i = 0; i < count; i++)
            {
                SkillTimedEventBase timedEvent = GetTrackEvent(skill, trackType, i);
                if (timedEvent == null)
                    continue;
                if (trackType == TimelineTrackType.Damage && timedEvent is SkillDamageEvent damageEvent && !PassesDamageSubFilters(damageEvent))
                    continue;

                handles.Add(new TimelineEventHandle
                {
                    trackType = trackType,
                    eventIndex = i,
                    timedEvent = timedEvent,
                });
            }

            return handles;
        }

        private bool PassesDamageSubFilters(SkillDamageEvent evt)
        {
            if (evt == null)
                return false;

            bool needsFilter = _filterDamageWithOnHitPhysics
                || _filterDamageWithOnHitAttribute
                || _filterDamageWithOnHitVfx
                || _filterDamageWithOnHitSfx;
            if (!needsFilter)
                return true;

            bool hasPhysics = false;
            bool hasAttribute = false;
            bool hasVfx = false;
            bool hasSfx = false;
            if (evt.damageEffects != null)
            {
                for (int i = 0; i < evt.damageEffects.Count; i++)
                {
                    SkillDamageEffect effect = evt.damageEffects[i];
                    if (effect == null)
                        continue;

                    hasPhysics |= effect.onHitPhysicsEffects != null && effect.onHitPhysicsEffects.Count > 0;
                    hasAttribute |= effect.onHitAttributeEffects != null && effect.onHitAttributeEffects.Count > 0;
                    hasVfx |= effect.onHitVfxEffects != null && effect.onHitVfxEffects.Count > 0;
                    hasSfx |= effect.onHitSfxEffects != null && effect.onHitSfxEffects.Count > 0;
                }
            }

            if (_filterDamageWithOnHitPhysics && !hasPhysics)
                return false;
            if (_filterDamageWithOnHitAttribute && !hasAttribute)
                return false;
            if (_filterDamageWithOnHitVfx && !hasVfx)
                return false;
            if (_filterDamageWithOnHitSfx && !hasSfx)
                return false;
            return true;
        }

        private SkillTimelineEvent CreateLegacyEventView(SkillTimedEventBase timedEvent, TimelineTrackType trackType)
        {
            if (timedEvent == null)
                return null;

            var view = new SkillTimelineEvent
            {
                eventId = timedEvent.eventId,
                startTime = timedEvent.startTime,
                triggerMode = timedEvent.triggerMode,
                activeDuration = timedEvent.activeDuration,
                activeDurationMode = timedEvent.activeDurationMode,
                repeatInterval = timedEvent.repeatInterval,
                damageEffects = new List<SkillDamageEffect>(),
                physicsEffects = new List<SkillPhysicsEffect>(),
                attributeEffects = new List<SkillAttributeEffect>(),
                vfxEffects = new List<SkillVfxEffect>(),
                sfxEffects = new List<SkillSfxEffect>(),
            };

            switch (trackType)
            {
                case TimelineTrackType.Damage:
                    SkillDamageEvent damageEvent = (SkillDamageEvent)timedEvent;
                    if (damageEvent.damageEffects == null)
                        damageEvent.damageEffects = new List<SkillDamageEffect>();
                    view.damageEffects = damageEvent.damageEffects;
                    break;
                case TimelineTrackType.Physics:
                    SkillPhysicsEvent physicsEvent = (SkillPhysicsEvent)timedEvent;
                    if (physicsEvent.physicsEffects == null)
                        physicsEvent.physicsEffects = new List<SkillPhysicsEffect>();
                    view.physicsEffects = physicsEvent.physicsEffects;
                    break;
                case TimelineTrackType.Attribute:
                    SkillAttributeEvent attributeEvent = (SkillAttributeEvent)timedEvent;
                    if (attributeEvent.attributeEffects == null)
                        attributeEvent.attributeEffects = new List<SkillAttributeEffect>();
                    view.attributeEffects = attributeEvent.attributeEffects;
                    break;
                case TimelineTrackType.Vfx:
                    SkillVfxEvent vfxEvent = (SkillVfxEvent)timedEvent;
                    if (vfxEvent.vfxEffects == null)
                        vfxEvent.vfxEffects = new List<SkillVfxEffect>();
                    view.vfxEffects = vfxEvent.vfxEffects;
                    break;
                case TimelineTrackType.Sfx:
                    SkillSfxEvent sfxEvent = (SkillSfxEvent)timedEvent;
                    if (sfxEvent.sfxEffects == null)
                        sfxEvent.sfxEffects = new List<SkillSfxEffect>();
                    view.sfxEffects = sfxEvent.sfxEffects;
                    break;
            }

            return view;
        }

        private bool HasSelectedSkill()
        {
            return GetSelectedSkill() != null;
        }

        private bool HasSelectedEvent()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            return skill != null
                && _selectedEventIndex >= 0
                && _selectedEventIndex < GetTrackEventCount(skill, _selectedEventTrackType);
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

        private void EnsureGroupList()
        {
            if (_database == null)
                return;

            _database.Synchronize();
            if (_database.groups == null)
                _database.groups = new List<SkillGroupDefinition>();

            if (_database.groups.Count == 0)
            {
                _database.groups.Add(new SkillGroupDefinition
                {
                    groupId = "default",
                    groupName = "默认分组",
                    skillGroups = new List<SkillEffectVariantGroupDefinition>(),
                    entries = new List<SharedSkillDefinition>(),
                });
                _selectedGroupIndex = 0;
                return;
            }

            for (int i = 0; i < _database.groups.Count; i++)
            {
                if (_database.groups[i] == null)
                    _database.groups[i] = new SkillGroupDefinition();
                else
                {
                    _database.groups[i].skillGroups ??= new List<SkillEffectVariantGroupDefinition>();
                    _database.groups[i].entries ??= new List<SharedSkillDefinition>();
                }
            }

            if (_selectedGroupIndex < 0 || _selectedGroupIndex >= _database.groups.Count)
                _selectedGroupIndex = 0;
        }

        private void DrawGroupSelector(Rect rect, bool hasDatabase)
        {
            EditorGUI.BeginDisabledGroup(!hasDatabase);
            try
            {
                if (!hasDatabase)
                {
                    EditorGUI.Popup(rect, 0, new[] { "未选择技能库" });
                    return;
                }

                EnsureGroupList();
                string[] options = GetGroupDisplayOptions();
                int newGroupIndex = EditorGUI.Popup(rect, Mathf.Clamp(_selectedGroupIndex, 0, Mathf.Max(0, options.Length - 1)), options);
                if (newGroupIndex != _selectedGroupIndex)
                {
                    _selectedGroupIndex = newGroupIndex;
                    _selectedSkillIndex = 0;
                    _selectedSkillVariantIndex = 0;
                    EnsureSkillSelectionIsValid();
                    ClearSelectedEvent();
                    _timelineScrollX = 0f;
                    StopPreview();
                }
            }
            finally
            {
                EditorGUI.EndDisabledGroup();
            }
        }

        private string[] GetGroupDisplayOptions()
        {
            EnsureGroupList();
            if (_database?.groups == null || _database.groups.Count == 0)
                return new[] { "默认分组" };

            var labels = new string[_database.groups.Count];
            for (int i = 0; i < _database.groups.Count; i++)
            {
                var group = _database.groups[i];
                labels[i] = group == null
                    ? $"分组{i}"
                    : (!string.IsNullOrWhiteSpace(group.groupName) ? group.groupName : group.groupId);
            }
            return labels;
        }

        private string GetSelectedSkillMenuLabel()
        {
            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return "未选择技能";

            string skillId = !string.IsNullOrWhiteSpace(skill.skillId)
                ? skill.skillId
                : "未命名技能";
            string groupName = SkillEffectDatabaseSO.GetVariantGroupName(GetSelectedSkillVariantGroup());
            if (string.IsNullOrWhiteSpace(groupName) || groupName == skillId)
                return skillId;

            return $"{groupName}/{skillId}";
        }

        private void DrawSkillSelectionDropdown(GUIStyle style, params GUILayoutOption[] options)
        {
            EnsureSkillSelectionIsValid();

            GUIContent buttonContent = new GUIContent(GetSelectedSkillMenuLabel());
            Rect buttonRect = GUILayoutUtility.GetRect(buttonContent, style, options);

            EditorGUI.BeginDisabledGroup(!HasSelectableSkillVariants());
            try
            {
                if (EditorGUI.DropdownButton(buttonRect, buttonContent, FocusType.Passive, style))
                    ShowSkillSelectionMenu(buttonRect);
            }
            finally
            {
                EditorGUI.EndDisabledGroup();
            }
        }

        private bool HasSelectableSkillVariants()
        {
            List<SkillEffectVariantGroupDefinition> visibleSkillGroups = GetVisibleSkillGroups();
            if (visibleSkillGroups == null)
                return false;

            for (int groupIndex = 0; groupIndex < visibleSkillGroups.Count; groupIndex++)
            {
                SkillEffectVariantGroupDefinition skillGroup = visibleSkillGroups[groupIndex];
                if (skillGroup?.entries != null && skillGroup.entries.Count > 0)
                    return true;
            }

            return false;
        }

        private void ShowSkillSelectionMenu(Rect buttonRect)
        {
            GenericMenu menu = new GenericMenu();
            List<SkillEffectVariantGroupDefinition> visibleSkillGroups = GetVisibleSkillGroups();
            bool hasEntries = false;

            if (visibleSkillGroups != null)
            {
                for (int groupIndex = 0; groupIndex < visibleSkillGroups.Count; groupIndex++)
                {
                    SkillEffectVariantGroupDefinition skillGroup = visibleSkillGroups[groupIndex];
                    List<SharedSkillDefinition> entries = skillGroup?.entries;
                    if (entries == null || entries.Count == 0)
                        continue;

                    string groupName = SkillEffectDatabaseSO.GetVariantGroupName(skillGroup);
                    if (string.IsNullOrWhiteSpace(groupName))
                        groupName = $"技能组{groupIndex + 1}";

                    for (int variantIndex = 0; variantIndex < entries.Count; variantIndex++)
                    {
                        SharedSkillDefinition entry = entries[variantIndex];
                        string skillLabel = entry != null && !string.IsNullOrWhiteSpace(entry.skillId)
                            ? entry.skillId
                            : $"技能效果{variantIndex + 1}";
                        int capturedGroupIndex = groupIndex;
                        int capturedVariantIndex = variantIndex;
                        bool isSelected = capturedGroupIndex == _selectedSkillIndex
                            && capturedVariantIndex == _selectedSkillVariantIndex;

                        menu.AddItem(
                            new GUIContent($"{groupName}/{skillLabel}"),
                            isSelected,
                            () => SelectSkillVariant(capturedGroupIndex, capturedVariantIndex));
                        hasEntries = true;
                    }
                }
            }

            if (!hasEntries)
                menu.AddDisabledItem(new GUIContent("未选择技能"));

            menu.DropDown(buttonRect);
        }

        private void SelectSkillVariant(int skillIndex, int skillVariantIndex)
        {
            _selectedSkillIndex = skillIndex;
            _selectedSkillVariantIndex = skillVariantIndex;
            EnsureSkillSelectionIsValid();
            ClearSelectedEvent();
            _timelineScrollX = 0f;
            StopPreview();
            Repaint();
        }

        private List<SkillEffectVariantGroupDefinition> GetVisibleSkillGroups()
        {
            EnsureGroupList();
            return _database?.GetVariantGroupsInGroup(_selectedGroupIndex) ?? new List<SkillEffectVariantGroupDefinition>();
        }

        private List<SharedSkillDefinition> GetVisibleSkillList()
        {
            EnsureGroupList();
            return _database?.GetEntriesInVariantGroup(_selectedGroupIndex, _selectedSkillIndex) ?? new List<SharedSkillDefinition>();
        }

        private SkillEffectVariantGroupDefinition GetSelectedSkillVariantGroup()
        {
            List<SkillEffectVariantGroupDefinition> visibleSkillGroups = GetVisibleSkillGroups();
            if (visibleSkillGroups == null)
                return null;
            if (_selectedSkillIndex < 0 || _selectedSkillIndex >= visibleSkillGroups.Count)
                return null;
            return visibleSkillGroups[_selectedSkillIndex];
        }

        private void EnsureSkillSelectionIsValid()
        {
            EnsureGroupList();
            List<SkillEffectVariantGroupDefinition> visibleSkillGroups = GetVisibleSkillGroups();
            if (visibleSkillGroups == null || visibleSkillGroups.Count == 0)
            {
                _selectedSkillIndex = -1;
                _selectedSkillVariantIndex = -1;
                return;
            }

            _selectedSkillIndex = Mathf.Clamp(_selectedSkillIndex, 0, visibleSkillGroups.Count - 1);

            List<SharedSkillDefinition> visibleSkills = GetVisibleSkillList();
            if (visibleSkills == null || visibleSkills.Count == 0)
            {
                _selectedSkillVariantIndex = -1;
                return;
            }

            _selectedSkillVariantIndex = Mathf.Clamp(_selectedSkillVariantIndex, 0, visibleSkills.Count - 1);
        }

        private void DrawPreviewTargetSelector()
        {
            List<GameObject> candidates = GetPreviewRootCandidates(_previewTargetFilter);
            GameObject currentRoot = ResolveRootPreviewTarget(_previewTarget);
            int selectedIndex = 0;
            string[] options = new string[candidates.Count + 1];
            options[0] = "未选择对象";
            for (int i = 0; i < candidates.Count; i++)
            {
                options[i + 1] = candidates[i] != null ? candidates[i].name : "空对象";
                if (candidates[i] == currentRoot)
                    selectedIndex = i + 1;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(selectedIndex, options, EditorStyles.toolbarPopup, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                _previewTarget = newIndex <= 0 ? null : candidates[newIndex - 1];
                if (!_isPlaying)
                    SampleAnimationAtTime(_previewTime);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void DrawPreviewVictimSelector()
        {
            List<GameObject> candidates = GetPreviewRootCandidates(_previewVictimFilter);
            GameObject currentRoot = ResolveRootPreviewTarget(_previewVictimTarget);
            int selectedIndex = 0;
            string[] options = new string[candidates.Count + 1];
            options[0] = "未选择对象";
            for (int i = 0; i < candidates.Count; i++)
            {
                options[i + 1] = candidates[i] != null ? candidates[i].name : "空对象";
                if (candidates[i] == currentRoot)
                    selectedIndex = i + 1;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(selectedIndex, options, EditorStyles.toolbarPopup, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                _previewVictimTarget = newIndex <= 0 ? null : candidates[newIndex - 1];
                if (!_isPlaying)
                    SampleAnimationAtTime(_previewTime);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void DrawSkillLibrarySelectorCompact()
        {
            EditorGUI.BeginChangeCheck();
            SkillEffectDatabaseSO newDatabase = (SkillEffectDatabaseSO)EditorGUILayout.ObjectField(
                _database,
                typeof(SkillEffectDatabaseSO),
                false,
                GUILayout.Width(CompactSkillObjectWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _database = newDatabase;
                RebuildSerializedDb();
                _selectedGroupIndex = 0;
                _selectedSkillIndex = 0;
                _selectedSkillVariantIndex = 0;
                EnsureSkillSelectionIsValid();
                ClearSelectedEvent();
                _timelineScrollX = 0f;
                StopPreview();
            }

            EnsureSkillSelectionIsValid();

            string[] groupOptions = GetGroupDisplayOptions();
            EditorGUI.BeginDisabledGroup(groupOptions.Length <= 1);
            EditorGUI.BeginChangeCheck();
            int newGroupIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedGroupIndex), groupOptions, EditorStyles.toolbarPopup, GUILayout.Width(CompactSkillGroupWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedGroupIndex = newGroupIndex;
                _selectedSkillIndex = 0;
                _selectedSkillVariantIndex = 0;
                EnsureSkillSelectionIsValid();
                ClearSelectedEvent();
                _timelineScrollX = 0f;
                StopPreview();
            }
            EditorGUI.EndDisabledGroup();

            DrawSkillSelectionDropdown(EditorStyles.toolbarPopup, GUILayout.Width(CompactSkillMenuWidth));
        }

        private void DrawAnimationLibrarySelectorCompact()
        {
            EditorGUI.BeginChangeCheck();
            CharacterAnimationLibrarySO newLibrary = (CharacterAnimationLibrarySO)EditorGUILayout.ObjectField(
                _animationLibrary,
                typeof(CharacterAnimationLibrarySO),
                false,
                GUILayout.Width(CompactAnimationObjectWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _animationLibrary = newLibrary;
                _selectedAnimationGroupIndex = 0;
                _selectedAnimationEntryIndex = 0;
                EnsureAnimationSelectionIsValid();
                ApplySelectedPreviewClip();
            }

            EnsureAnimationSelectionIsValid();

            string[] groupOptions = GetAnimationGroupOptions();
            EditorGUI.BeginDisabledGroup(groupOptions.Length <= 1);
            EditorGUI.BeginChangeCheck();
            int newGroupIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedAnimationGroupIndex), groupOptions, EditorStyles.toolbarPopup, GUILayout.Width(CompactAnimationGroupWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedAnimationGroupIndex = newGroupIndex;
                _selectedAnimationEntryIndex = 0;
                EnsureAnimationSelectionIsValid();
                ApplySelectedPreviewClip();
            }
            EditorGUI.EndDisabledGroup();

            string[] entryOptions = GetAnimationEntryOptions();
            EditorGUI.BeginDisabledGroup(entryOptions.Length <= 1);
            EditorGUI.BeginChangeCheck();
            int newEntryIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedAnimationEntryIndex), entryOptions, EditorStyles.toolbarPopup, GUILayout.Width(CompactAnimationEntryWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedAnimationEntryIndex = newEntryIndex;
                EnsureAnimationSelectionIsValid();
                ApplySelectedPreviewClip();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawSkillLibrarySelectorPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            try
            {
                EditorGUILayout.LabelField("技能库", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                SkillEffectDatabaseSO newDatabase = (SkillEffectDatabaseSO)EditorGUILayout.ObjectField(_database, typeof(SkillEffectDatabaseSO), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _database = newDatabase;
                    RebuildSerializedDb();
                    _selectedGroupIndex = 0;
                    _selectedSkillIndex = 0;
                    _selectedSkillVariantIndex = 0;
                    EnsureSkillSelectionIsValid();
                    ClearSelectedEvent();
                    _timelineScrollX = 0f;
                    StopPreview();
                }

                EnsureSkillSelectionIsValid();

                string[] groupOptions = GetGroupDisplayOptions();
                EditorGUI.BeginDisabledGroup(groupOptions.Length <= 1);
                EditorGUI.BeginChangeCheck();
                int newGroupIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedGroupIndex), groupOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedGroupIndex = newGroupIndex;
                    _selectedSkillIndex = 0;
                    _selectedSkillVariantIndex = 0;
                    EnsureSkillSelectionIsValid();
                    ClearSelectedEvent();
                    _timelineScrollX = 0f;
                    StopPreview();
                }
                EditorGUI.EndDisabledGroup();

                DrawSkillSelectionDropdown(EditorStyles.popup);
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawAnimationLibrarySelectorPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            try
            {
                EditorGUILayout.LabelField("动画库", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                CharacterAnimationLibrarySO newLibrary = (CharacterAnimationLibrarySO)EditorGUILayout.ObjectField(_animationLibrary, typeof(CharacterAnimationLibrarySO), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _animationLibrary = newLibrary;
                    _selectedAnimationGroupIndex = 0;
                    _selectedAnimationEntryIndex = 0;
                    EnsureAnimationSelectionIsValid();
                    ApplySelectedPreviewClip();
                }

                EnsureAnimationSelectionIsValid();

                string[] groupOptions = GetAnimationGroupOptions();
                EditorGUI.BeginDisabledGroup(groupOptions.Length <= 1);
                EditorGUI.BeginChangeCheck();
                int newGroupIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedAnimationGroupIndex), groupOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedAnimationGroupIndex = newGroupIndex;
                    _selectedAnimationEntryIndex = 0;
                    EnsureAnimationSelectionIsValid();
                    ApplySelectedPreviewClip();
                }
                EditorGUI.EndDisabledGroup();

                string[] entryOptions = GetAnimationEntryOptions();
                EditorGUI.BeginDisabledGroup(entryOptions.Length <= 1);
                EditorGUI.BeginChangeCheck();
                int newEntryIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedAnimationEntryIndex), entryOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedAnimationEntryIndex = newEntryIndex;
                    EnsureAnimationSelectionIsValid();
                    ApplySelectedPreviewClip();
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(30f);
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawSkillLibrarySelector()
        {
            EditorGUI.BeginChangeCheck();
            SkillEffectDatabaseSO newDatabase = (SkillEffectDatabaseSO)EditorGUILayout.ObjectField(
                _database,
                typeof(SkillEffectDatabaseSO),
                false,
                GUILayout.Width(CompactSkillObjectWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _database = newDatabase;
                RebuildSerializedDb();
                _selectedGroupIndex = 0;
                _selectedSkillIndex = 0;
                _selectedSkillVariantIndex = 0;
                EnsureSkillSelectionIsValid();
                ClearSelectedEvent();
                _timelineScrollX = 0f;
                StopPreview();
            }

            EnsureSkillSelectionIsValid();

            string[] groupOptions = GetGroupDisplayOptions();
            EditorGUI.BeginDisabledGroup(groupOptions.Length <= 1);
            EditorGUI.BeginChangeCheck();
            int newGroupIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedGroupIndex), groupOptions, EditorStyles.toolbarPopup, GUILayout.Width(CompactSkillGroupWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedGroupIndex = newGroupIndex;
                _selectedSkillIndex = 0;
                _selectedSkillVariantIndex = 0;
                EnsureSkillSelectionIsValid();
                ClearSelectedEvent();
                _timelineScrollX = 0f;
                StopPreview();
            }
            EditorGUI.EndDisabledGroup();

            DrawSkillSelectionDropdown(EditorStyles.toolbarPopup, GUILayout.Width(CompactSkillMenuWidth));
        }

        private void DrawAnimationLibrarySelector()
        {
            EditorGUI.BeginChangeCheck();
            CharacterAnimationLibrarySO newLibrary = (CharacterAnimationLibrarySO)EditorGUILayout.ObjectField(
                _animationLibrary,
                typeof(CharacterAnimationLibrarySO),
                false,
                GUILayout.Width(CompactAnimationObjectWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _animationLibrary = newLibrary;
                _selectedAnimationGroupIndex = 0;
                _selectedAnimationEntryIndex = 0;
                EnsureAnimationSelectionIsValid();
                ApplySelectedPreviewClip();
            }

            EnsureAnimationSelectionIsValid();

            string[] groupOptions = GetAnimationGroupOptions();
            EditorGUI.BeginDisabledGroup(groupOptions.Length <= 1);
            EditorGUI.BeginChangeCheck();
            int newGroupIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedAnimationGroupIndex), groupOptions, EditorStyles.toolbarPopup, GUILayout.Width(CompactAnimationGroupWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedAnimationGroupIndex = newGroupIndex;
                _selectedAnimationEntryIndex = 0;
                EnsureAnimationSelectionIsValid();
                ApplySelectedPreviewClip();
            }
            EditorGUI.EndDisabledGroup();

            string[] entryOptions = GetAnimationEntryOptions();
            EditorGUI.BeginDisabledGroup(entryOptions.Length <= 1);
            EditorGUI.BeginChangeCheck();
            int newEntryIndex = EditorGUILayout.Popup(Mathf.Max(0, _selectedAnimationEntryIndex), entryOptions, EditorStyles.toolbarPopup, GUILayout.Width(CompactAnimationEntryWidth));
            if (EditorGUI.EndChangeCheck())
            {
                _selectedAnimationEntryIndex = newEntryIndex;
                EnsureAnimationSelectionIsValid();
                ApplySelectedPreviewClip();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void EnsurePreviewTargetIsValid()
        {
            GameObject root = ResolveRootPreviewTarget(_previewTarget);
            if (root != null && MatchesPreviewTargetFilter(root, _previewTargetFilter))
            {
                _previewTarget = root;
                return;
            }

            List<GameObject> candidates = GetPreviewRootCandidates(_previewTargetFilter);
            _previewTarget = candidates.Count > 0 ? candidates[0] : null;
        }

        private void EnsurePreviewVictimIsValid()
        {
            GameObject root = ResolveRootPreviewTarget(_previewVictimTarget);
            if (root != null && MatchesPreviewTargetFilter(root, _previewVictimFilter))
            {
                _previewVictimTarget = root;
                return;
            }

            _previewVictimTarget = null;
        }

        private List<GameObject> GetPreviewRootCandidates()
        {
            return GetPreviewRootCandidates(_previewTargetFilter);
        }

        private List<GameObject> GetPreviewRootCandidates(PreviewTargetFilter filter)
        {
            var result = new List<GameObject>();
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
                return result;

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && MatchesPreviewTargetFilter(root, filter))
                    result.Add(root);
            }

            return result;
        }

        private bool MatchesPreviewTargetFilter(GameObject root)
        {
            return MatchesPreviewTargetFilter(root, _previewTargetFilter);
        }

        private bool MatchesPreviewTargetFilter(GameObject root, PreviewTargetFilter filter)
        {
            if (root == null)
                return false;

            return filter switch
            {
                PreviewTargetFilter.Player => root.tag == "Player",
                PreviewTargetFilter.Enemy => root.tag == "Enemy",
                _ => true,
            };
        }

        private static GameObject ResolveRootPreviewTarget(GameObject target)
        {
            if (target == null)
                return null;
            return target.transform.root != null ? target.transform.root.gameObject : target;
        }

        private void TryAutoLoadAnimationLibrary()
        {
            if (_animationLibrary == null)
                _animationLibrary = Resources.Load<CharacterAnimationLibrarySO>("配置/动画库");

            EnsureAnimationSelectionIsValid();
            if (_previewClip == null)
                ApplySelectedPreviewClip();
        }

        private void EnsureAnimationSelectionIsValid()
        {
            if (_animationLibrary == null || _animationLibrary.groups == null || _animationLibrary.groups.Count == 0)
            {
                _selectedAnimationGroupIndex = -1;
                _selectedAnimationEntryIndex = -1;
                if (_animationLibrary == null)
                    _previewClip = null;
                return;
            }

            _selectedAnimationGroupIndex = Mathf.Clamp(_selectedAnimationGroupIndex < 0 ? 0 : _selectedAnimationGroupIndex, 0, _animationLibrary.groups.Count - 1);
            var group = _animationLibrary.groups[_selectedAnimationGroupIndex];
            int entryCount = group?.entries != null ? group.entries.Count : 0;
            if (entryCount <= 0)
            {
                _selectedAnimationEntryIndex = -1;
                _previewClip = null;
                return;
            }

            _selectedAnimationEntryIndex = Mathf.Clamp(_selectedAnimationEntryIndex < 0 ? 0 : _selectedAnimationEntryIndex, 0, entryCount - 1);
        }

        private string[] GetAnimationGroupOptions()
        {
            if (_animationLibrary == null || _animationLibrary.groups == null || _animationLibrary.groups.Count == 0)
                return new[] { "未选择动画库" };

            var labels = new string[_animationLibrary.groups.Count];
            for (int i = 0; i < _animationLibrary.groups.Count; i++)
            {
                var group = _animationLibrary.groups[i];
                labels[i] = group != null && !string.IsNullOrWhiteSpace(group.groupName) ? group.groupName : $"分组{i + 1}";
            }
            return labels;
        }

        private string[] GetAnimationEntryOptions()
        {
            if (_animationLibrary == null || _animationLibrary.groups == null || _selectedAnimationGroupIndex < 0 || _selectedAnimationGroupIndex >= _animationLibrary.groups.Count)
                return new[] { "未选择动画" };

            var group = _animationLibrary.groups[_selectedAnimationGroupIndex];
            if (group?.entries == null || group.entries.Count == 0)
                return new[] { "分组无动画" };

            var labels = new string[group.entries.Count];
            for (int i = 0; i < group.entries.Count; i++)
            {
                var entry = group.entries[i];
                labels[i] = entry != null && !string.IsNullOrWhiteSpace(entry.animationId) ? entry.animationId : $"动画{i + 1}";
            }
            return labels;
        }

        private CharacterAnimationEntry GetSelectedAnimationEntry()
        {
            if (_animationLibrary == null || _animationLibrary.groups == null || _selectedAnimationGroupIndex < 0 || _selectedAnimationGroupIndex >= _animationLibrary.groups.Count)
                return null;

            var group = _animationLibrary.groups[_selectedAnimationGroupIndex];
            if (group?.entries == null || _selectedAnimationEntryIndex < 0 || _selectedAnimationEntryIndex >= group.entries.Count)
                return null;

            return group.entries[_selectedAnimationEntryIndex];
        }

        private void ApplySelectedPreviewClip()
        {
            CharacterAnimationEntry entry = GetSelectedAnimationEntry();
            _previewClip = entry != null ? entry.clip : null;
            if (!_isPlaying)
                SampleAnimationAtTime(_previewTime);
        }

        private void TryAutoLoadDatabase()
        {
            if (_database == null)
            {
                _database = Resources.Load<SkillEffectDatabaseSO>("配置/技能效果库");
            }

            if (_database != null && _serializedDb == null)
            {
                _serializedDb = new SerializedObject(_database);
                EnsureGroupList();
            }
        }

        private float TimeToX(float time)
        {
            return time * _zoom - _timelineScrollX;
        }

        private float XToTime(float mouseX, float panelX)
        {
            return (mouseX - panelX + _timelineScrollX) / _zoom;
        }

        private float GetEventDisplayWidth(SkillTimedEventBase evt, TimelineTrackType trackType)
        {
            if (evt == null)
                return MinEventWidth;

            float duration = GetTrackDisplayDuration(evt, trackType);
            if (duration > 0f)
                return Mathf.Max(MinEventWidth, duration * _zoom);

            return Mathf.Max(MinEventWidth, 12f);
        }

        private float GetTrackDisplayDuration(SkillTimedEventBase evt, TimelineTrackType trackType)
        {
            if (evt == null)
                return 0f;

            float lastTriggerTime = evt.triggerMode == SkillEventTriggerMode.Repeated
                ? GetPreviewRepeatedEndTime(evt)
                : evt.startTime;
            float tailDuration = GetTrackEffectTailDuration(evt, trackType, lastTriggerTime);
            if (evt.triggerMode == SkillEventTriggerMode.Repeated)
            {
                float repeatedDuration = evt.RepeatsUntilStateExit
                    ? Mathf.Max(0f, GetPreviewStateExitTime() - evt.startTime)
                    : evt.activeDuration;
                return Mathf.Max(0f, repeatedDuration + tailDuration);
            }
            return tailDuration;
        }

        private static void SetTrackDisplayDuration(SkillTimedEventBase evt, TimelineTrackType trackType, float duration)
        {
            if (evt == null)
                return;

            float clampedDuration = Mathf.Max(0.01f, duration);

            switch (trackType)
            {
                case TimelineTrackType.Damage:
                    if (evt is not SkillDamageEvent damageEvent || damageEvent.damageEffects == null)
                        return;

                    float maxDetectionDuration = GetMaxDamageDetectionDuration(damageEvent);
                    if (damageEvent.triggerMode == SkillEventTriggerMode.Repeated)
                    {
                        damageEvent.activeDurationMode = SkillEventActiveDurationMode.FixedTime;
                        damageEvent.activeDuration = Mathf.Max(0f, clampedDuration - maxDetectionDuration);
                        if (damageEvent.activeDuration > 0f)
                            damageEvent.repeatInterval = Mathf.Clamp(damageEvent.repeatInterval, 0.01f, damageEvent.activeDuration);
                    }
                    else
                    {
                        for (int i = 0; i < damageEvent.damageEffects.Count; i++)
                        {
                            SkillDamageEffect effect = damageEvent.damageEffects[i];
                            if (effect != null)
                                effect.detectionDuration = clampedDuration;
                        }
                    }
                    break;

                case TimelineTrackType.Physics:
                    if (evt is not SkillPhysicsEvent physicsEvent || physicsEvent.physicsEffects == null)
                        return;
                    if (physicsEvent.triggerMode == SkillEventTriggerMode.Repeated)
                    {
                        physicsEvent.activeDurationMode = SkillEventActiveDurationMode.FixedTime;
                        float tailDuration = GetTrackEffectTailDurationForAuthoring(physicsEvent, trackType);
                        physicsEvent.activeDuration = Mathf.Max(0f, clampedDuration - tailDuration);
                        if (physicsEvent.activeDuration > 0f)
                            physicsEvent.repeatInterval = Mathf.Clamp(physicsEvent.repeatInterval, 0.01f, physicsEvent.activeDuration);
                        return;
                    }
                    for (int i = 0; i < physicsEvent.physicsEffects.Count; i++)
                    {
                        SkillPhysicsEffect effect = physicsEvent.physicsEffects[i];
                        if (effect != null)
                        {
                            effect.durationMode = SkillEffectDurationMode.FixedTime;
                            effect.duration = clampedDuration;
                        }
                    }
                    break;

                case TimelineTrackType.Attribute:
                    if (evt is not SkillAttributeEvent attributeEvent || attributeEvent.attributeEffects == null)
                        return;
                    if (attributeEvent.triggerMode == SkillEventTriggerMode.Repeated)
                    {
                        attributeEvent.activeDurationMode = SkillEventActiveDurationMode.FixedTime;
                        float tailDuration = GetTrackEffectTailDurationForAuthoring(attributeEvent, trackType);
                        attributeEvent.activeDuration = Mathf.Max(0f, clampedDuration - tailDuration);
                        if (attributeEvent.activeDuration > 0f)
                            attributeEvent.repeatInterval = Mathf.Clamp(attributeEvent.repeatInterval, 0.01f, attributeEvent.activeDuration);
                        return;
                    }
                    for (int i = 0; i < attributeEvent.attributeEffects.Count; i++)
                    {
                        SkillAttributeEffect effect = attributeEvent.attributeEffects[i];
                        if (effect != null && effect.statField != SkillStatField.HP && effect.statField != SkillStatField.MP)
                        {
                            effect.durationMode = SkillEffectDurationMode.FixedTime;
                            effect.duration = clampedDuration;
                        }
                    }
                    break;

                case TimelineTrackType.Vfx:
                    if (evt is not SkillVfxEvent vfxEvent || vfxEvent.vfxEffects == null)
                        return;
                    if (vfxEvent.triggerMode == SkillEventTriggerMode.Repeated)
                    {
                        vfxEvent.activeDurationMode = SkillEventActiveDurationMode.FixedTime;
                        float tailDuration = GetTrackEffectTailDurationForAuthoring(vfxEvent, trackType);
                        vfxEvent.activeDuration = Mathf.Max(0f, clampedDuration - tailDuration);
                        if (vfxEvent.activeDuration > 0f)
                            vfxEvent.repeatInterval = Mathf.Clamp(vfxEvent.repeatInterval, 0.01f, vfxEvent.activeDuration);
                        return;
                    }
                    for (int i = 0; i < vfxEvent.vfxEffects.Count; i++)
                    {
                        SkillVfxEffect effect = vfxEvent.vfxEffects[i];
                        if (effect != null)
                            effect.duration = clampedDuration;
                    }
                    break;

                case TimelineTrackType.Sfx:
                    if (evt is not SkillSfxEvent sfxEvent || sfxEvent.sfxEffects == null)
                        return;
                    if (sfxEvent.triggerMode == SkillEventTriggerMode.Repeated)
                    {
                        sfxEvent.activeDurationMode = SkillEventActiveDurationMode.FixedTime;
                        float tailDuration = GetTrackEffectTailDurationForAuthoring(sfxEvent, trackType);
                        sfxEvent.activeDuration = Mathf.Max(0f, clampedDuration - tailDuration);
                        if (sfxEvent.activeDuration > 0f)
                            sfxEvent.repeatInterval = Mathf.Clamp(sfxEvent.repeatInterval, 0.01f, sfxEvent.activeDuration);
                        return;
                    }
                    for (int i = 0; i < sfxEvent.sfxEffects.Count; i++)
                    {
                        SkillSfxEffect effect = sfxEvent.sfxEffects[i];
                        if (effect != null)
                            effect.duration = clampedDuration;
                    }
                    break;
            }
        }

        private static float GetMaxDamageDetectionDuration(SkillDamageEvent evt)
        {
            if (evt?.damageEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.damageEffects.Count; i++)
            {
                SkillDamageEffect effect = evt.damageEffects[i];
                if (effect != null)
                    duration = Mathf.Max(duration, effect.detectionDuration);
            }

            return duration;
        }

        private float GetTimelineContentWidth(SharedSkillDefinition skill)
        {
            float duration = Mathf.Max(skill != null ? skill.GetTimelineDuration() : 0f, GetPreviewTotalDuration(), 2f);
            return duration * _zoom + 40f;
        }

        private Color GetTrackColor(TimelineTrackType trackType)
        {
            return trackType switch
            {
                TimelineTrackType.Damage => DamageColor,
                TimelineTrackType.Physics => PhysicsColor,
                TimelineTrackType.Attribute => AttributeColor,
                TimelineTrackType.Vfx => VfxColor,
                TimelineTrackType.Sfx => SfxColor,
                _ => DefaultColor,
            };
        }

        private string GetTrackHeaderLabel(TimelineTrackType trackType)
        {
            return trackType switch
            {
                TimelineTrackType.Damage => "命中轨",
                TimelineTrackType.Physics => "物理轨",
                TimelineTrackType.Attribute => "属性轨",
                TimelineTrackType.Vfx => "特效轨",
                TimelineTrackType.Sfx => "音效轨",
                _ => "轨道",
            };
        }

        private string GetTrackEventLabel(SkillTimedEventBase evt, TimelineTrackType trackType)
        {
            if (evt == null)
                return "evt";

            SkillTimelineEvent view = CreateLegacyEventView(evt, trackType);
            if (view == null)
                return "evt";

            string prefix = !string.IsNullOrEmpty(view.eventId) ? view.eventId : trackType switch
            {
                TimelineTrackType.Damage => "命中",
                TimelineTrackType.Physics => "物理",
                TimelineTrackType.Attribute => "属性",
                TimelineTrackType.Vfx => "特效",
                TimelineTrackType.Sfx => "音效",
                _ => "事件",
            };

            string detail = trackType switch
            {
                TimelineTrackType.Damage => GetFirstDamageEffectName(view),
                TimelineTrackType.Physics => GetFirstPhysicsEffectName(view),
                TimelineTrackType.Attribute => GetFirstAttributeEffectName(view),
                TimelineTrackType.Vfx => GetFirstVfxEffectName(view),
                TimelineTrackType.Sfx => GetFirstSfxEffectName(view),
                _ => string.Empty,
            };

            return string.IsNullOrEmpty(detail) ? prefix : $"{prefix} {detail}";
        }

        private static string GetEventSummary(SkillTimelineEvent evt)
        {
            if (evt == null)
                return string.Empty;

            int damageCount = GetDamageEffectCount(evt);
            int physicsCount = evt.physicsEffects != null ? evt.physicsEffects.Count : 0;
            int attributeCount = evt.attributeEffects != null ? evt.attributeEffects.Count : 0;
            int vfxCount = evt.vfxEffects != null ? evt.vfxEffects.Count : 0;
            int sfxCount = evt.sfxEffects != null ? evt.sfxEffects.Count : 0;
            return $"[D{damageCount}/P{physicsCount}/A{attributeCount}/V{vfxCount}/S{sfxCount}]";
        }

        private static int GetDamageEffectCount(SkillTimelineEvent evt)
        {
            if (evt == null)
                return 0;

            if (evt.damageEffects == null || evt.damageEffects.Count == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < evt.damageEffects.Count; i++)
            {
                if (evt.damageEffects[i] != null)
                    count++;
            }

            return count;
        }

        private static string GetFirstDamageEffectName(SkillTimelineEvent evt)
        {
            if (evt == null)
                return "命中";

            if (evt.damageEffects != null)
            {
                for (int i = 0; i < evt.damageEffects.Count; i++)
                {
                    var effect = evt.damageEffects[i];
                    if (effect == null)
                        continue;
                    return GetDamageEffectLabel(effect);
                }
            }

            return "命中";
        }

        private static string GetDamageEffectLabel(SkillDamageEffect effect)
        {
            if (effect == null)
                return "命中";

            return effect.detectionType switch
            {
                DamageDetectionType.Raycast => "射线",
                DamageDetectionType.Collision => "碰撞",
                _ => effect.shape switch
                {
                    AttackShapeType.Sector => "扇形",
                    AttackShapeType.Box => "盒体",
                    _ => "球形",
                },
            };
        }

        private static string GetFirstPhysicsEffectName(SkillTimelineEvent evt)
        {
            if (evt?.physicsEffects == null)
                return string.Empty;

            for (int i = 0; i < evt.physicsEffects.Count; i++)
            {
                var effect = evt.physicsEffects[i];
                if (effect != null)
                {
                    return effect.effectType switch
                    {
                        PhysicsEffectType.Knockback => "击退",
                        PhysicsEffectType.Pull => "拉拽",
                        PhysicsEffectType.Launch => "击飞",
                        PhysicsEffectType.DashSelf => "位移",
                        PhysicsEffectType.Stun => "眩晕",
                        PhysicsEffectType.Airborne => "腾空",
                        PhysicsEffectType.SuperArmor => "霸体",
                        PhysicsEffectType.Invincible => "无敌",
                        _ => "物理",
                    };
                }
            }

            return string.Empty;
        }

        private static string GetFirstAttributeEffectName(SkillTimelineEvent evt)
        {
            if (evt?.attributeEffects == null)
                return string.Empty;

            for (int i = 0; i < evt.attributeEffects.Count; i++)
            {
                var effect = evt.attributeEffects[i];
                if (effect != null)
                {
                    return effect.statField switch
                    {
                        SkillStatField.HP => "生命",
                        SkillStatField.MP => "法力",
                        SkillStatField.Attack => "攻击",
                        SkillStatField.Defense => "防御",
                        SkillStatField.MoveSpeed => "移速",
                        SkillStatField.HPRegen => "生命回复",
                        SkillStatField.MPRegen => "法力回复",
                        SkillStatField.CritRate => "暴击率",
                        SkillStatField.CritDamage => "暴击伤害",
                        SkillStatField.AttackSpeed => "攻速",
                        SkillStatField.SkillDamage => "增伤",
                        SkillStatField.DamageReduce => "减伤",
                        SkillStatField.LifeSteal => "吸血",
                        _ => "属性",
                    };
                }
            }

            return string.Empty;
        }

        private static string GetFirstVfxEffectName(SkillTimelineEvent evt)
        {
            if (evt?.vfxEffects == null)
                return string.Empty;

            for (int i = 0; i < evt.vfxEffects.Count; i++)
            {
                var effect = evt.vfxEffects[i];
                if (effect?.particlePrefab != null)
                    return effect.particlePrefab.name;
            }

            return string.Empty;
        }

        private static string GetFirstSfxEffectName(SkillTimelineEvent evt)
        {
            if (evt?.sfxEffects == null)
                return string.Empty;

            for (int i = 0; i < evt.sfxEffects.Count; i++)
            {
                var effect = evt.sfxEffects[i];
                if (effect?.audioClip != null)
                    return effect.audioClip.name;
            }

            return string.Empty;
        }

        private void DrawEventValidationWarnings(SkillTimelineEvent evt, SkillTimedEventBase timedEvent)
        {
            List<string> warnings = CollectEventWarnings(evt, timedEvent);
            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }

        private List<string> CollectEventWarnings(SkillTimelineEvent evt, SkillTimedEventBase timedEvent)
        {
            var warnings = new List<string>();
            if (evt == null)
                return warnings;

            if (GetDamageEffectCount(evt) == 0
                && (evt.physicsEffects == null || evt.physicsEffects.Count == 0)
                && (evt.attributeEffects == null || evt.attributeEffects.Count == 0)
                && (evt.vfxEffects == null || evt.vfxEffects.Count == 0)
                && (evt.sfxEffects == null || evt.sfxEffects.Count == 0))
            {
                warnings.Add("这个事件当前没有任何效果，运行时不会产生实际结果。");
            }

            int topLevelTypeCount = 0;
            if (GetDamageEffectCount(evt) > 0) topLevelTypeCount++;
            if (evt.physicsEffects != null && evt.physicsEffects.Count > 0) topLevelTypeCount++;
            if (evt.attributeEffects != null && evt.attributeEffects.Count > 0) topLevelTypeCount++;
            if (evt.vfxEffects != null && evt.vfxEffects.Count > 0) topLevelTypeCount++;
            if (evt.sfxEffects != null && evt.sfxEffects.Count > 0) topLevelTypeCount++;
            if (topLevelTypeCount > 1)
                warnings.Add("当前事件混合了多种顶层类型。按新工作流，建议一条事件只负责一种顶层类型；命中后的附加效果请配在命中效果内部。");

            if (evt.triggerMode == SkillEventTriggerMode.Repeated)
            {
                if (evt.activeDurationMode == SkillEventActiveDurationMode.FixedTime)
                {
                    float repeatedWindow = Mathf.Max(0f, evt.activeDuration);
                    if (repeatedWindow <= 0f)
                        warnings.Add("重复触发模式的持续触发时长为 0，事件不会重复执行。");
                    if (evt.repeatInterval > repeatedWindow && repeatedWindow > 0f)
                        warnings.Add("重复触发间隔大于持续触发时长，通常只会触发 1 次。");
                }
                else
                {
                    warnings.Add("当前事件会持续重复触发，直到状态退出。请确认它只用于你期望的循环状态。");
                }
            }

            if (_previewClip != null && evt.startTime > _previewClip.length + 0.0001f)
                warnings.Add("事件触发时间超过了当前预览动画长度，预览时可能看不到效果。");

            if (evt.damageEffects != null)
            {
                for (int i = 0; i < evt.damageEffects.Count; i++)
                {
                    var effect = evt.damageEffects[i];
                    if (effect == null)
                    {
                        warnings.Add($"命中效果 {i + 1} 为空引用。");
                        continue;
                    }

                    effect.TryMigrateLegacySubEffects();

                    if (string.IsNullOrWhiteSpace(effect.hitLayerName))
                        warnings.Add($"命中效果 {i + 1} 没有填写命中层级名。");

                    if (effect.detectionType == DamageDetectionType.RangeOverlap)
                    {
                        if ((effect.shape == AttackShapeType.Sphere || effect.shape == AttackShapeType.Sector) && effect.sphereRadius <= 0f)
                            warnings.Add($"命中效果 {i + 1} 的球体半径必须大于 0。");

                        if (effect.shape == AttackShapeType.Sector && effect.sectorAngle <= 0f)
                            warnings.Add($"命中效果 {i + 1} 的扇形角度必须大于 0。");

                        if (effect.shape == AttackShapeType.Box &&
                            (effect.boxSize.x <= 0f || effect.boxSize.y <= 0f || effect.boxSize.z <= 0f))
                        {
                            warnings.Add($"命中效果 {i + 1} 的盒体尺寸必须全部大于 0。");
                        }
                    }
                    else if (effect.detectionType == DamageDetectionType.Collision)
                    {
                        if (string.IsNullOrWhiteSpace(effect.colliderNodeName))
                            warnings.Add($"命中效果 {i + 1} 使用碰撞检测时必须填写武器命中盒对象名。");
                    }
                    else if (effect.detectionType == DamageDetectionType.Raycast)
                    {
                        if (effect.rayMaxDistance <= 0f)
                            warnings.Add($"命中效果 {i + 1} 的射线最大距离必须大于 0。");
                        if (effect.rayRadius < 0f)
                            warnings.Add($"命中效果 {i + 1} 的射线半径不能小于 0。");
                    }

                    if (effect.detectionType != DamageDetectionType.Collision
                        && effect.companionVfxEffects != null)
                    {
                        for (int j = 0; j < effect.companionVfxEffects.Count; j++)
                        {
                            SkillVfxEffect companionVfx = effect.companionVfxEffects[j];
                            if (companionVfx != null && companionVfx.particlePrefab == null)
                                warnings.Add($"命中效果 {i + 1} 的伴随特效效果 {j + 1} 没有粒子特效 Prefab。");
                        }
                    }

                    SkillHitStopEffect hitStopEffect = effect.GetEffectiveHitStopEffect();
                    if (SkillDamageEffect.HasConfiguredHitStopEffect(hitStopEffect)
                        && hitStopEffect.hitStopDuration > 0f
                        && hitStopEffect.hitStopTimeScale >= 0.999f)
                    {
                        warnings.Add($"命中效果 {i + 1} 的命中停顿效果配了命中停顿时长，但命中停顿速度为 1，等于没有停顿效果。");
                    }

                    if (effect.onHitPhysicsEffects != null)
                    {
                        for (int j = 0; j < effect.onHitPhysicsEffects.Count; j++)
                        {
                            SkillPhysicsEffect physics = effect.onHitPhysicsEffects[j];
                            if (physics != null && physics.IsTopLevelOnly)
                                warnings.Add($"命中效果 {i + 1} 的命中物理效果 {j + 1} 使用了仅限顶层的 {physics.effectType}。");
                        }
                    }
                }
            }

            if (evt.physicsEffects != null)
            {
                for (int i = 0; i < evt.physicsEffects.Count; i++)
                {
                    SkillPhysicsEffect effect = evt.physicsEffects[i];
                    if (effect == null)
                        continue;

                    if (effect.IsOnHitOnly)
                        warnings.Add($"物理效果 {i + 1} 使用了仅限命中物理的 {effect.effectType}。");

                    if (effect.durationMode == SkillEffectDurationMode.FixedTime && effect.duration <= 0f)
                        warnings.Add($"物理效果 {i + 1} 的持续时长必须大于 0。");
                    else if (!effect.SupportsUntilStateExitDuration && effect.durationMode == SkillEffectDurationMode.UntilStateExit)
                        warnings.Add($"物理效果 {i + 1} 的 {effect.effectType} 不支持持续到状态退出。");
                }
            }

            if (evt.attributeEffects != null)
            {
                for (int i = 0; i < evt.attributeEffects.Count; i++)
                {
                    SkillAttributeEffect effect = evt.attributeEffects[i];
                    if (effect == null)
                        continue;

                    if (effect.targetMode != SkillTargetMode.Self)
                        warnings.Add($"属性效果 {i + 1} 作为顶层属性事件时，作用目标应为自身。");

                    if ((effect.statField == SkillStatField.HP || effect.statField == SkillStatField.MP)
                        && effect.durationMode == SkillEffectDurationMode.UntilStateExit)
                    {
                        warnings.Add($"属性效果 {i + 1} 的 {effect.statField} 不支持持续到状态退出，当前设置会被忽略。");
                    }

                    if ((effect.statField == SkillStatField.HP || effect.statField == SkillStatField.MP) && effect.duration > 0f)
                        warnings.Add($"属性效果 {i + 1} 的 {effect.statField} 不使用持续时长，当前持续时长会被忽略。");
                }
            }

            if (evt.vfxEffects != null)
            {
                for (int i = 0; i < evt.vfxEffects.Count; i++)
                {
                    var cue = evt.vfxEffects[i];
                    if (cue == null)
                    {
                        warnings.Add($"特效效果 {i + 1} 为空引用。");
                        continue;
                    }

                    if (cue.particlePrefab == null)
                        warnings.Add($"特效效果 {i + 1} 没有粒子特效 Prefab。");
                    if (cue.destroyMode == SkillCueDestroyMode.Timed && cue.duration <= 0f)
                        warnings.Add($"特效效果 {i + 1} 选择了“指定秒数销毁”，但销毁时间未大于 0。");
                }
            }

            if (evt.sfxEffects != null)
            {
                for (int i = 0; i < evt.sfxEffects.Count; i++)
                {
                    var cue = evt.sfxEffects[i];
                    if (cue == null)
                    {
                        warnings.Add($"音效效果 {i + 1} 为空引用。");
                        continue;
                    }

                    if (cue.audioClip == null)
                        warnings.Add($"音效效果 {i + 1} 没有音效片段。");
                    if (cue.destroyMode == SkillCueDestroyMode.Timed && cue.duration <= 0f)
                        warnings.Add($"音效效果 {i + 1} 选择了“指定秒数销毁”，但销毁时间未大于 0。");
                    if (cue.loop && cue.destroyMode == SkillCueDestroyMode.NaturalDestroy)
                        warnings.Add($"音效效果 {i + 1} 开启了循环播放，但销毁方式仍是“等待自然销毁”，运行时会回退为状态退出销毁。");
                }
            }

            return warnings;
        }

        private List<string> CollectSkillWarnings(SharedSkillDefinition skill)
        {
            var warnings = new List<string>();
            if (skill == null)
                return warnings;

            if (string.IsNullOrWhiteSpace(skill.skillId))
                warnings.Add("当前技能没有填写技能ID。");

            if (GetTotalEventCount(skill) == 0)
                warnings.Add("当前技能还没有任何事件。运行时不会产生任何效果。");

            var eventIds = new Dictionary<string, List<string>>();
            CollectEventIdWarnings(eventIds, GetDamageEventList(skill), TimelineTrackType.Damage);
            CollectEventIdWarnings(eventIds, GetPhysicsEventList(skill), TimelineTrackType.Physics);
            CollectEventIdWarnings(eventIds, GetAttributeEventList(skill), TimelineTrackType.Attribute);
            CollectEventIdWarnings(eventIds, GetVfxEventList(skill), TimelineTrackType.Vfx);
            CollectEventIdWarnings(eventIds, GetSfxEventList(skill), TimelineTrackType.Sfx);

            foreach (var pair in eventIds)
            {
                if (pair.Value.Count > 1)
                    warnings.Add($"事件标识 ID “{pair.Key}” 在多个事件中重复：{string.Join("、", pair.Value)}。");
            }

            if (_sceneHandlesEnabled && _previewTarget == null)
                warnings.Add("已开启场景编辑，但当前没有预览对象。场景可视化编辑与部分预览反馈不会显示。");

            if (_animationLibrary != null && _previewClip == null)
                warnings.Add("已指定动画库，但当前没有选中预览动画。部分时间预览与时长校验会不准确。");

            if (_previewTarget != null)
                CollectPreviewTargetWarnings(warnings, skill);

            return warnings;
        }

        private void CollectEventIdWarnings<T>(Dictionary<string, List<string>> map, List<T> events, TimelineTrackType trackType) where T : SkillTimedEventBase
        {
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                T evt = events[i];
                if (evt == null || string.IsNullOrWhiteSpace(evt.eventId))
                    continue;

                if (!map.TryGetValue(evt.eventId, out var uses))
                {
                    uses = new List<string>();
                    map.Add(evt.eventId, uses);
                }

                uses.Add($"{GetTrackHeaderLabel(trackType)}#{i + 1}");
            }
        }

        private void CollectPreviewTargetWarnings(List<string> warnings, SharedSkillDefinition skill)
        {
            List<SkillDamageEvent> damageEvents = GetDamageEventList(skill);
            if (damageEvents == null)
                return;

            for (int eventIndex = 0; eventIndex < damageEvents.Count; eventIndex++)
            {
                SkillDamageEvent evt = damageEvents[eventIndex];
                if (evt?.damageEffects == null)
                    continue;

                for (int effectIndex = 0; effectIndex < evt.damageEffects.Count; effectIndex++)
                {
                    SkillDamageEffect effect = evt.damageEffects[effectIndex];
                    if (effect == null || effect.detectionType != DamageDetectionType.Collision || string.IsNullOrWhiteSpace(effect.colliderNodeName))
                        continue;

                    Transform hitboxNode = FindChildRecursive(_previewTarget.transform, effect.colliderNodeName);
                    if (hitboxNode == null)
                    {
                        warnings.Add($"命中轨事件#{eventIndex + 1} 的命中盒对象 “{effect.colliderNodeName}” 在当前预览对象中不存在。");
                        continue;
                    }

                    Collider hitboxCollider = hitboxNode.GetComponent<Collider>();
                    if (hitboxCollider == null)
                    {
                        warnings.Add($"命中轨事件#{eventIndex + 1} 的命中盒对象 “{effect.colliderNodeName}” 没有 Collider。");
                        continue;
                    }

                    if (!hitboxCollider.isTrigger)
                        warnings.Add($"命中轨事件#{eventIndex + 1} 的命中盒对象 “{effect.colliderNodeName}” 当前未勾选 Is Trigger。运行时会自动改为 Trigger，但编辑时建议直接按 Trigger 配置。");
                }
            }
        }

        private bool IsCurrentPreviewEvent(SkillTimedEventBase evt, TimelineTrackType trackType)
        {
            if (evt == null)
                return false;

            float previewEndTime = evt.startTime + Mathf.Max(0f, GetTrackDisplayDuration(evt, trackType));
            if (previewEndTime <= evt.startTime)
                previewEndTime = evt.GetEndTime();
            if (previewEndTime > evt.startTime)
                return _previewTime >= evt.startTime && _previewTime <= previewEndTime;

            float tolerance = Mathf.Max(0.02f, 0.5f / Mathf.Max(1, _frameRate));
            return Mathf.Abs(_previewTime - evt.startTime) <= tolerance;
        }

        private static void DrawCurrentEventOutline(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.92f, 0.35f, 1f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private List<TimelineTrackLayout> BuildTrackLayouts(SharedSkillDefinition skill, float x, float y, float width)
        {
            var layouts = new List<TimelineTrackLayout>();
            if (skill == null)
                return layouts;

            TimelineTrackType[] trackOrder =
            {
                TimelineTrackType.Damage,
                TimelineTrackType.Physics,
                TimelineTrackType.Attribute,
                TimelineTrackType.Vfx,
                TimelineTrackType.Sfx,
            };

            float currentY = y;
            for (int i = 0; i < trackOrder.Length; i++)
            {
                TimelineTrackType trackType = trackOrder[i];
                if (!IsTrackVisible(trackType))
                    continue;

                List<List<TimelineEventHandle>> rows = AssignEventRows(GetTrackEventHandles(skill, trackType), trackType);
                int rowCount = Mathf.Max(1, rows.Count);
                float bodyHeight = rowCount * EventRowHeight;
                var layout = new TimelineTrackLayout
                {
                    trackType = trackType,
                    rows = rows,
                    headerRect = new Rect(x, currentY, width, TrackHeaderHeight),
                    bodyRect = new Rect(x, currentY + TrackHeaderHeight, width, bodyHeight),
                };
                layouts.Add(layout);
                currentY += TrackHeaderHeight + bodyHeight + TrackSpacing;
            }

            return layouts;
        }

        private static float GetTrackLayoutsHeight(List<TimelineTrackLayout> trackLayouts)
        {
            if (trackLayouts == null || trackLayouts.Count == 0)
                return TrackHeaderHeight + EventRowHeight;

            TimelineTrackLayout last = trackLayouts[trackLayouts.Count - 1];
            return Mathf.Max(TrackHeaderHeight + EventRowHeight, last.bodyRect.yMax - trackLayouts[0].headerRect.y);
        }

        private void DrawTrackHeaders(List<TimelineTrackLayout> trackLayouts)
        {
            for (int i = 0; i < trackLayouts.Count; i++)
            {
                TimelineTrackLayout layout = trackLayouts[i];
                Color headerColor = GetTrackColor(layout.trackType) * 0.65f;
                headerColor.a = 1f;
                EditorGUI.DrawRect(layout.headerRect, headerColor);
                GUI.Label(
                    new Rect(layout.headerRect.x + 6f, layout.headerRect.y + 1f, layout.headerRect.width - 12f, layout.headerRect.height - 2f),
                    GetTrackHeaderLabel(layout.trackType),
                    EditorStyles.miniBoldLabel);
            }
        }

        private static bool IsMouseInAnyTrackBody(Vector2 mousePosition, List<TimelineTrackLayout> trackLayouts)
        {
            for (int i = 0; i < trackLayouts.Count; i++)
            {
                if (trackLayouts[i].bodyRect.Contains(mousePosition))
                    return true;
            }

            return false;
        }

        private List<List<TimelineEventHandle>> AssignEventRows(List<TimelineEventHandle> handles, TimelineTrackType trackType)
        {
            var rows = new List<List<TimelineEventHandle>>();
            var rowEnds = new List<float>();

            if (handles == null)
                return rows;

            for (int i = 0; i < handles.Count; i++)
            {
                TimelineEventHandle handle = handles[i];
                SkillTimedEventBase evt = handle?.timedEvent;
                if (evt == null)
                    continue;

                float displayDuration = GetTrackDisplayDuration(evt, trackType);
                float endTime = evt.startTime + (displayDuration > 0f
                    ? Mathf.Max(0.01f, displayDuration)
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
                    rows.Add(new List<TimelineEventHandle>());
                    rowEnds.Add(0f);
                    assignedRow = rows.Count - 1;
                }

                rows[assignedRow].Add(handle);
                rowEnds[assignedRow] = Mathf.Max(rowEnds[assignedRow], endTime);
            }

            return rows;
        }

        private bool IsTrackVisible(TimelineTrackType trackType)
        {
            return trackType switch
            {
                TimelineTrackType.Damage => _showDamageEvents,
                TimelineTrackType.Physics => _showPhysicsEvents,
                TimelineTrackType.Attribute => _showAttributeEvents,
                TimelineTrackType.Vfx => _showVfxEvents,
                TimelineTrackType.Sfx => _showSfxEvents,
                _ => true,
            };
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

        private SkillTimelineEvent GetSelectedSceneEvent()
        {
            SkillTimedEventBase timedEvent = GetSelectedTimedEvent();
            return timedEvent != null ? CreateLegacyEventView(timedEvent, _selectedEventTrackType) : null;
        }

        private void DrawCueSceneHandle(SkillVfxEffect cue, float triggerTime, Transform explicitTarget, string label)
        {
            if (cue == null || _previewTarget == null)
                return;

            Transform anchor = ResolveCueEditBasisTransform(cue.anchor, explicitTarget);
            if (anchor == null)
                anchor = _previewTarget.transform;

            if (!TryEvaluatePreviewCueTransform(cue, explicitTarget, triggerTime, out Vector3 worldPosition, out Quaternion worldRotation))
                return;

            Vector3 motionOffset = Vector3.zero;
            if (CueUsesWorldMotion(cue))
            {
                float elapsed = Mathf.Max(0f, _previewTime - triggerTime);
                Quaternion motionBasisRotation = cue.anchor == CueAnchor.World && _scenePreviewCueHasWorldOrigin
                    ? _scenePreviewCueWorldOriginRotation
                    : anchor.rotation;
                Vector3 direction = cue.motion.direction.sqrMagnitude > 0.0001f
                    ? cue.motion.direction.normalized
                    : Vector3.forward;
                motionOffset = motionBasisRotation * direction * (cue.motion.speed * elapsed);
            }

            Vector3 scale = cue.scale;
            float handleSize = HandleUtility.GetHandleSize(worldPosition);

            Color oldColor = Handles.color;
            Handles.color = new Color(0.18f, 0.9f, 1f, 1f);
            DrawSceneTextLabel(worldPosition + Vector3.up * handleSize * 0.15f, label);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPosition = Handles.PositionHandle(worldPosition, worldRotation);
            Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, newWorldPosition);
            Vector3 newScale = Handles.ScaleHandle(scale, newWorldPosition, newWorldRotation, handleSize * 0.75f);
            if (EditorGUI.EndChangeCheck())
            {
                Quaternion basisRotation = cue.anchor == CueAnchor.World && _scenePreviewCueHasWorldOrigin
                    ? _scenePreviewCueWorldOriginRotation
                    : anchor.rotation;
                Vector3 basisPosition = cue.anchor == CueAnchor.World && _scenePreviewCueHasWorldOrigin
                    ? _scenePreviewCueWorldOriginPosition - basisRotation * cue.offset
                    : anchor.position;
                Undo.RecordObject(_database, "Edit VFX Scene Handle");
                cue.offset = Quaternion.Inverse(basisRotation) * ((newWorldPosition - motionOffset) - basisPosition);
                cue.rotationEuler = (Quaternion.Inverse(basisRotation) * newWorldRotation).eulerAngles;
                cue.scale = ClampVector3(newScale, 0.01f);
                MarkDatabaseDirty();
                InvalidateScenePreviewCueWorldOrigin();
                DestroyScenePreviewCueInstance();
                if (!_isPlaying)
                    SampleAnimationAtTime(_previewTime);
                else
                    UpdateScenePreviewCueInstance();
                Repaint();
                SceneView.RepaintAll();
            }

            DrawSceneCueBounds(cue, worldPosition, worldRotation);

            Handles.color = oldColor;
        }

        private void DrawDamageCompanionCueSceneHandle(SkillDamageEffect damageEffect, SkillVfxEffect cue, float triggerTime, string label)
        {
            if (damageEffect == null || cue == null || _previewTarget == null)
                return;

            if (!TryEvaluatePreviewDamageCompanionTransform(damageEffect, cue, triggerTime, _previewTime, out Vector3 worldPosition, out Quaternion worldRotation))
                return;

            Vector3 scale = cue.scale;
            float handleSize = HandleUtility.GetHandleSize(worldPosition);

            Color oldColor = Handles.color;
            Handles.color = new Color(0.18f, 0.9f, 1f, 1f);
            DrawSceneTextLabel(worldPosition + Vector3.up * handleSize * 0.15f, label);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPosition = Handles.PositionHandle(worldPosition, worldRotation);
            Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, newWorldPosition);
            Vector3 newScale = Handles.ScaleHandle(scale, newWorldPosition, newWorldRotation, handleSize * 0.75f);
            if (EditorGUI.EndChangeCheck())
            {
                if (!TryEvaluatePreviewDamageWindowTransform(damageEffect, triggerTime, _previewTime, out Vector3 bodyPosition, out Quaternion bodyRotation))
                    return;

                Undo.RecordObject(_database, "Edit Damage Companion VFX Scene Handle");
                cue.offset = Quaternion.Inverse(bodyRotation) * (newWorldPosition - bodyPosition);
                cue.rotationEuler = (Quaternion.Inverse(bodyRotation) * newWorldRotation).eulerAngles;
                cue.scale = ClampVector3(newScale, 0.01f);
                MarkDatabaseDirty();
                InvalidateScenePreviewCueWorldOrigin();
                DestroyScenePreviewCueInstance();
                UpdateScenePreviewCueInstance();
                Repaint();
                SceneView.RepaintAll();
            }

            DrawSceneCueBounds(cue, worldPosition, worldRotation);

            Handles.color = oldColor;
        }

        private void DrawDamageSceneHandle(SkillDamageEffect effect, float? triggerTime = null)
        {
            if (effect == null || _previewTarget == null)
                return;

            switch (effect.detectionType)
            {
                case DamageDetectionType.RangeOverlap:
                    DrawRangeDamageSceneHandle(effect, triggerTime);
                    break;
                case DamageDetectionType.Raycast:
                    DrawRaycastDamageSceneHandle(effect, triggerTime);
                    break;
                case DamageDetectionType.Collision:
                    DrawCollisionDamageSceneHandle(effect);
                    break;
            }
        }

        private void DrawRangeDamageSceneHandle(SkillDamageEffect effect, float? triggerTime = null)
        {
            Transform preview = _previewTarget.transform;
            Vector3 basisPosition = preview.position;
            Quaternion basisRotation = preview.rotation;
            Vector3 motionOffset = Vector3.zero;
            float resolvedTriggerTime;
            bool hasTriggerTime = triggerTime.HasValue;
            if (hasTriggerTime)
            {
                resolvedTriggerTime = triggerTime.Value;
            }
            else
            {
                hasTriggerTime = TryGetCurrentSelectedDamageTriggerTime(out resolvedTriggerTime);
            }

            if (hasTriggerTime)
            {
                SkillDetectionMotionFrame? motionFrame = CreateEditorPreviewMotionFrame(effect, resolvedTriggerTime, _previewTime);
                if (motionFrame.HasValue)
                {
                    basisPosition = motionFrame.Value.OriginPosition;
                    basisRotation = motionFrame.Value.OriginRotation;
                    Vector3 motionDirection = effect.motion.direction.sqrMagnitude > 0.0001f
                        ? effect.motion.direction.normalized
                        : Vector3.forward;
                    motionOffset = basisRotation * motionDirection * (effect.motion.speed * motionFrame.Value.Elapsed);
                }
            }

            Vector3 origin = basisPosition + basisRotation * effect.centerOffset + motionOffset;
            Quaternion rotation = basisRotation * Quaternion.Euler(effect.rotationEuler);
            float handleSize = HandleUtility.GetHandleSize(origin);
            bool changed = false;

            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 0.25f, 0.25f, 1f);
            DrawSceneTextLabel(origin + Vector3.up * handleSize * 0.15f, "命中范围");

            EditorGUI.BeginChangeCheck();
            Vector3 newOrigin = Handles.PositionHandle(origin, rotation);
            Quaternion newRotation = rotation;
            if (effect.shape == AttackShapeType.Sector || effect.shape == AttackShapeType.Box)
                newRotation = Handles.RotationHandle(rotation, newOrigin);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_database, "Edit Damage Scene Handle");
                effect.centerOffset = Quaternion.Inverse(basisRotation) * ((newOrigin - motionOffset) - basisPosition);
                if (effect.shape == AttackShapeType.Sector || effect.shape == AttackShapeType.Box)
                    effect.rotationEuler = (Quaternion.Inverse(basisRotation) * newRotation).eulerAngles;
                origin = newOrigin;
                rotation = newRotation;
                changed = true;
            }

            switch (effect.shape)
            {
                case AttackShapeType.Sphere:
                    DrawWireSphere(origin, Mathf.Max(0.01f, effect.sphereRadius), preview);
                    EditorGUI.BeginChangeCheck();
                    float newRadius = Handles.RadiusHandle(Quaternion.identity, origin, Mathf.Max(0.01f, effect.sphereRadius));
                    if (EditorGUI.EndChangeCheck())
                    {
                        effect.sphereRadius = Mathf.Max(0.01f, newRadius);
                        changed = true;
                    }
                    break;

                case AttackShapeType.Sector:
                    Vector3 sectorForward = rotation * Vector3.forward;
                    Vector3 sectorUp = rotation * Vector3.up;
                    DrawWireSector(origin, sectorForward, sectorUp, Mathf.Max(0.01f, effect.sphereRadius), effect.sectorAngle);
                    EditorGUI.BeginChangeCheck();
                    Vector3 radiusHandle = origin + sectorForward.normalized * Mathf.Max(0.01f, effect.sphereRadius);
                    Vector3 newRadiusHandle = Handles.Slider(radiusHandle, sectorForward, handleSize * 0.1f, Handles.ConeHandleCap, 0f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Damage Scene Handle");
                        effect.sphereRadius = Mathf.Max(0.01f, Vector3.Dot(newRadiusHandle - origin, sectorForward.normalized));
                        changed = true;
                    }
                    break;

                case AttackShapeType.Box:
                    DrawWireBox(origin, rotation, effect.boxSize);
                    EditorGUI.BeginChangeCheck();
                    Vector3 newSize = Handles.ScaleHandle(effect.boxSize, origin, rotation, handleSize * 0.8f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_database, "Edit Damage Scene Handle");
                        effect.boxSize = ClampVector3(newSize, 0.01f);
                        changed = true;
                    }
                    break;
            }

            if (changed)
                MarkDatabaseDirty();

            Handles.color = oldColor;
        }

        private void DrawRaycastDamageSceneHandle(SkillDamageEffect effect, float? triggerTime = null)
        {
            Transform preview = _previewTarget.transform;
            Vector3 basisPosition = preview.position;
            Quaternion basisRotation = preview.rotation;
            Vector3 motionOffset = Vector3.zero;
            float resolvedTriggerTime;
            bool hasTriggerTime = triggerTime.HasValue;
            if (hasTriggerTime)
            {
                resolvedTriggerTime = triggerTime.Value;
            }
            else
            {
                hasTriggerTime = TryGetCurrentSelectedDamageTriggerTime(out resolvedTriggerTime);
            }

            if (hasTriggerTime)
            {
                SkillDetectionMotionFrame? motionFrame = CreateEditorPreviewMotionFrame(effect, resolvedTriggerTime, _previewTime);
                if (motionFrame.HasValue)
                {
                    basisPosition = motionFrame.Value.OriginPosition;
                    basisRotation = motionFrame.Value.OriginRotation;
                    Vector3 motionDirection = effect.motion.direction.sqrMagnitude > 0.0001f
                        ? effect.motion.direction.normalized
                        : Vector3.forward;
                    motionOffset = basisRotation * motionDirection * (effect.motion.speed * motionFrame.Value.Elapsed);
                }
            }

            Vector3 origin = basisPosition + basisRotation * effect.rayOriginOffset + motionOffset;
            Quaternion rotation = basisRotation * Quaternion.Euler(effect.rotationEuler);
            Vector3 direction = (rotation * Vector3.forward).normalized;
            float handleSize = HandleUtility.GetHandleSize(origin);
            bool changed = false;

            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 0.25f, 0.25f, 1f);
            DrawSceneTextLabel(origin + Vector3.up * handleSize * 0.15f, "射线范围");

            EditorGUI.BeginChangeCheck();
            Vector3 newOrigin = Handles.PositionHandle(origin, rotation);
            Quaternion newRotation = Handles.RotationHandle(rotation, newOrigin);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_database, "Edit Damage Scene Handle");
                effect.rayOriginOffset = Quaternion.Inverse(basisRotation) * ((newOrigin - motionOffset) - basisPosition);
                effect.rotationEuler = (Quaternion.Inverse(basisRotation) * newRotation).eulerAngles;
                origin = newOrigin;
                rotation = newRotation;
                direction = (rotation * Vector3.forward).normalized;
                changed = true;
            }

            Vector3 end = origin + direction * Mathf.Max(0.01f, effect.rayMaxDistance);
            float rayRadius = Mathf.Max(0f, effect.rayRadius);
            Handles.DrawLine(origin, end);
            if (rayRadius > 0.0001f)
            {
                Handles.DrawWireDisc(origin, direction, rayRadius);
                Handles.DrawWireDisc(end, direction, rayRadius);

                Quaternion ringRotation = Quaternion.LookRotation(direction);
                Vector3 side = ringRotation * Vector3.right * rayRadius;
                Vector3 up = ringRotation * Vector3.up * rayRadius;
                Handles.DrawLine(origin + side, end + side);
                Handles.DrawLine(origin - side, end - side);
                Handles.DrawLine(origin + up, end + up);
                Handles.DrawLine(origin - up, end - up);
            }
            EditorGUI.BeginChangeCheck();
            Vector3 newEnd = Handles.Slider(end, direction, handleSize * 0.1f, Handles.ConeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_database, "Edit Damage Scene Handle");
                effect.rayMaxDistance = Mathf.Max(0.01f, Vector3.Dot(newEnd - origin, direction));
                changed = true;
            }

            EditorGUI.BeginChangeCheck();
            float newRadius = Handles.RadiusHandle(rotation, origin, Mathf.Max(0.01f, rayRadius <= 0f ? 0.2f : rayRadius));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_database, "Edit Damage Scene Handle");
                effect.rayRadius = Mathf.Max(0f, newRadius);
                changed = true;
            }

            if (changed)
                MarkDatabaseDirty();

            Handles.color = oldColor;
        }

        private void DrawCollisionDamageSceneHandle(SkillDamageEffect effect)
        {
            if (_previewTarget == null)
                return;

            if (string.IsNullOrWhiteSpace(effect.colliderNodeName))
            {
                DrawSceneTextLabel(_previewTarget.transform.position + Vector3.up * 2f, "碰撞检测: 未填写武器命中盒对象名");
                return;
            }

            Transform hitboxNode = FindChildRecursive(_previewTarget.transform, effect.colliderNodeName);
            if (hitboxNode == null)
            {
                DrawSceneTextLabel(_previewTarget.transform.position + Vector3.up * 2f, $"碰撞检测: 找不到 {effect.colliderNodeName}");
                return;
            }

            Collider hitboxCollider = hitboxNode.GetComponent<Collider>();
            if (hitboxCollider == null)
            {
                DrawSceneTextLabel(hitboxNode.position + Vector3.up * 0.2f, $"碰撞检测: {effect.colliderNodeName} 没有 Collider");
                return;
            }

            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 0.25f, 0.25f, 1f);
            DrawSceneTextLabel(hitboxCollider.bounds.center + Vector3.up * HandleUtility.GetHandleSize(hitboxCollider.bounds.center) * 0.12f, $"命中盒: {effect.colliderNodeName}");

            if (hitboxCollider is BoxCollider box)
            {
                Matrix4x4 oldMatrix = Handles.matrix;
                Vector3 lossyScale = hitboxCollider.transform.lossyScale;
                Handles.matrix = Matrix4x4.TRS(hitboxCollider.transform.TransformPoint(box.center), hitboxCollider.transform.rotation, new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
                Handles.DrawWireCube(Vector3.zero, box.size);
                Handles.matrix = oldMatrix;
            }
            else if (hitboxCollider is SphereCollider sphere)
            {
                float radius = sphere.radius * Mathf.Max(Mathf.Abs(hitboxCollider.transform.lossyScale.x), Mathf.Abs(hitboxCollider.transform.lossyScale.y), Mathf.Abs(hitboxCollider.transform.lossyScale.z));
                DrawWireSphere(hitboxCollider.transform.TransformPoint(sphere.center), radius, hitboxCollider.transform);
            }
            else
            {
                Bounds bounds = hitboxCollider.bounds;
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            Handles.color = oldColor;
        }

        private void UpdateScenePreviewCueInstance()
        {
            if (_isPlaying || !_sceneHandlesEnabled || _previewTarget == null)
            {
                DestroyScenePreviewCueInstance();
                return;
            }

            SkillTimelineEvent evt = GetSelectedSceneEvent();
            if (TryGetActiveSceneCompanionCue(evt, out SkillDamageEffect companionDamageEffect, out SkillVfxEffect companionCue, out int companionCueEventIndex, out int companionCueListIndex))
            {
                if (companionCue == null || companionCue.particlePrefab == null)
                {
                    DestroyScenePreviewCueInstance();
                    return;
                }

                bool needRecreateCompanion = _scenePreviewCueInstance == null
                    || _scenePreviewCuePrefab != companionCue.particlePrefab
                    || _scenePreviewCueTrackType != TimelineTrackType.Damage
                    || _scenePreviewCueEventIndex != companionCueEventIndex
                    || _scenePreviewCueDamageIndex != _activeSceneCompanionVfxDamageIndex
                    || _scenePreviewCueListIndex != companionCueListIndex
                    || !_scenePreviewCueFollowsDamageWindow;
                if (needRecreateCompanion)
                    RecreateScenePreviewCueInstance(companionCue, TimelineTrackType.Damage, companionCueEventIndex, _activeSceneCompanionVfxDamageIndex, companionCueListIndex, true);

                if (_scenePreviewCueInstance == null)
                    return;

                if (!TryGetSceneCompanionCuePlaybackContext(out float companionTriggerTime))
                {
                    _scenePreviewCueInstance.SetActive(false);
                    return;
                }

                UpdateScenePreviewCompanionCueTransform(companionDamageEffect, companionCue, companionTriggerTime);
                bool shouldShowCompanion = _previewTime + 0.0001f >= companionTriggerTime;
                _scenePreviewCueInstance.SetActive(shouldShowCompanion);
                if (!shouldShowCompanion)
                    return;

                float companionLocalTime = Mathf.Max(0f, _previewTime - companionTriggerTime);
                SimulateScenePreviewParticles(companionLocalTime);
                return;
            }

            if (!TryGetActiveSceneCue(evt, out SkillVfxEffect cue, out int cueEventIndex, out int cueListIndex, out TimelineTrackType cueTrackType))
            {
                DestroyScenePreviewCueInstance();
                return;
            }
            if (cue == null || cue.particlePrefab == null)
            {
                DestroyScenePreviewCueInstance();
                return;
            }

            bool needRecreate = _scenePreviewCueInstance == null
                || _scenePreviewCuePrefab != cue.particlePrefab
                || _scenePreviewCueTrackType != cueTrackType
                || _scenePreviewCueEventIndex != cueEventIndex
                || _scenePreviewCueDamageIndex != -1
                || _scenePreviewCueListIndex != cueListIndex
                || _scenePreviewCueFollowsDamageWindow;

            if (needRecreate)
                RecreateScenePreviewCueInstance(cue, cueTrackType, cueEventIndex, -1, cueListIndex, false);

            if (_scenePreviewCueInstance == null)
                return;

            if (!TryGetCurrentSceneCuePlaybackContext(out float triggerTime, out Transform explicitTarget))
            {
                _scenePreviewCueInstance.SetActive(false);
                return;
            }

            UpdateScenePreviewCueTransform(cue);

            bool shouldShow = _previewTime + 0.0001f >= triggerTime;
            _scenePreviewCueInstance.SetActive(shouldShow);
            if (!shouldShow)
                return;

            float localTime = Mathf.Max(0f, _previewTime - triggerTime);
            SimulateScenePreviewParticles(localTime);
        }

        private bool TryGetActiveSceneCue(SkillTimelineEvent evt, out SkillVfxEffect cue, out int cueEventIndex, out int cueListIndex, out TimelineTrackType cueTrackType)
        {
            cue = null;
            cueEventIndex = -1;
            cueListIndex = -1;
            cueTrackType = TimelineTrackType.Vfx;

            if (evt == null)
                return false;

            if (_selectedEventTrackType == TimelineTrackType.Damage
                && _activeSceneHitVfxEventIndex == _selectedEventIndex
                && _activeSceneHitVfxDamageIndex >= 0
                && evt.damageEffects != null
                && _activeSceneHitVfxDamageIndex < evt.damageEffects.Count)
            {
                SkillDamageEffect damageEffect = evt.damageEffects[_activeSceneHitVfxDamageIndex];
                if (damageEffect?.onHitVfxEffects != null
                    && _activeSceneHitVfxIndex >= 0
                    && _activeSceneHitVfxIndex < damageEffect.onHitVfxEffects.Count)
                {
                    if (!IsOnHitCueVisible(_selectedEventIndex, _activeSceneHitVfxDamageIndex, _activeSceneHitVfxIndex))
                        return false;

                    cue = damageEffect.onHitVfxEffects[_activeSceneHitVfxIndex];
                    cueEventIndex = _selectedEventIndex;
                    cueListIndex = _activeSceneHitVfxIndex;
                    cueTrackType = TimelineTrackType.Damage;
                    return cue != null;
                }
            }

            if (evt.vfxEffects != null && _activeSceneCueIndex >= 0 && _activeSceneCueIndex < evt.vfxEffects.Count)
            {
                if (!IsTopLevelCueVisible(_selectedEventIndex, _activeSceneCueIndex))
                    return false;

                cue = evt.vfxEffects[_activeSceneCueIndex];
                cueEventIndex = _selectedEventIndex;
                cueListIndex = _activeSceneCueIndex;
                cueTrackType = _selectedEventTrackType;
                return cue != null;
            }

            return false;
        }

        private bool TryGetActiveSceneCompanionCue(SkillTimelineEvent evt, out SkillDamageEffect damageEffect, out SkillVfxEffect cue, out int cueEventIndex, out int cueListIndex)
        {
            damageEffect = null;
            cue = null;
            cueEventIndex = -1;
            cueListIndex = -1;

            if (evt == null
                || _selectedEventTrackType != TimelineTrackType.Damage
                || _activeSceneCompanionVfxEventIndex != _selectedEventIndex
                || _activeSceneCompanionVfxDamageIndex < 0
                || _activeSceneCompanionVfxIndex < 0
                || evt.damageEffects == null
                || _activeSceneCompanionVfxDamageIndex >= evt.damageEffects.Count)
            {
                return false;
            }

            damageEffect = evt.damageEffects[_activeSceneCompanionVfxDamageIndex];
            damageEffect?.TryMigrateLegacySubEffects();
            if (damageEffect?.companionVfxEffects == null
                || _activeSceneCompanionVfxIndex >= damageEffect.companionVfxEffects.Count)
            {
                return false;
            }

            if (!IsCompanionCueVisible(_selectedEventIndex, _activeSceneCompanionVfxDamageIndex, _activeSceneCompanionVfxIndex))
                return false;

            cue = damageEffect.companionVfxEffects[_activeSceneCompanionVfxIndex];
            cueEventIndex = _selectedEventIndex;
            cueListIndex = _activeSceneCompanionVfxIndex;
            return cue != null;
        }

        private void RecreateScenePreviewCueInstance(SkillVfxEffect cue, TimelineTrackType cueTrackType, int cueEventIndex, int cueDamageIndex, int cueListIndex, bool followsDamageWindow)
        {
            DestroyScenePreviewCueInstance();
            if (cue?.particlePrefab == null)
                return;

            _scenePreviewCueInstance = Object.Instantiate(cue.particlePrefab);
            if (_scenePreviewCueInstance == null)
                return;

            ApplyHideFlagsRecursively(_scenePreviewCueInstance, HideFlags.HideAndDontSave);
            _scenePreviewCueInstance.name = $"[SkillPreview]{cue.particlePrefab.name}";
            _scenePreviewCuePrefab = cue.particlePrefab;
            _scenePreviewCueTrackType = cueTrackType;
            _scenePreviewCueEventIndex = cueEventIndex;
            _scenePreviewCueDamageIndex = cueDamageIndex;
            _scenePreviewCueListIndex = cueListIndex;
            _scenePreviewCueFollowsDamageWindow = followsDamageWindow;
            _scenePreviewCueHasWorldOrigin = false;
            _scenePreviewCueWorldOriginTriggerTime = -1f;
            if (followsDamageWindow)
                ConfigureDamageCompanionPreviewParticles(_scenePreviewCueInstance);
        }

        private void UpdateScenePreviewCueTransform(SkillVfxEffect cue)
        {
            if (_scenePreviewCueInstance == null || cue == null)
                return;

            if (!TryGetCurrentSceneCuePlaybackContext(out float triggerTime, out Transform explicitTarget))
            {
                _scenePreviewCueInstance.SetActive(false);
                _scenePreviewCueHasWorldOrigin = false;
                _scenePreviewCueWorldOriginTriggerTime = -1f;
                return;
            }

            Vector3 position;
            Quaternion rotation;
            if (cue.anchor == CueAnchor.World)
            {
                EnsureScenePreviewCueWorldOrigin(cue, triggerTime);
                if (!_scenePreviewCueHasWorldOrigin)
                    return;

                EvaluateWorldCueTransform(
                    cue,
                    _scenePreviewCueWorldOriginPosition,
                    _scenePreviewCueWorldOriginRotation,
                    Mathf.Max(0f, _previewTime - triggerTime),
                    out position,
                    out rotation);
            }
            else if (!TryEvaluatePreviewCueTransform(cue, explicitTarget, triggerTime, out position, out rotation))
            {
                return;
            }

            _scenePreviewCueInstance.transform.position = position;
            _scenePreviewCueInstance.transform.rotation = rotation;
            _scenePreviewCueInstance.transform.localScale = cue.scale;
        }

        private void UpdateScenePreviewCompanionCueTransform(SkillDamageEffect damageEffect, SkillVfxEffect cue, float triggerTime)
        {
            if (_scenePreviewCueInstance == null || damageEffect == null || cue == null)
                return;

            if (!TryEvaluatePreviewDamageCompanionTransform(damageEffect, cue, triggerTime, _previewTime, out Vector3 position, out Quaternion rotation))
            {
                _scenePreviewCueInstance.SetActive(false);
                return;
            }

            _scenePreviewCueInstance.transform.position = position;
            _scenePreviewCueInstance.transform.rotation = rotation;
            _scenePreviewCueInstance.transform.localScale = cue.scale;
        }

        private bool IsSceneEditingTopLevelCue(int eventIndex, int effectIndex)
        {
            return _activeSceneCueEventIndex >= 0
                && _activeSceneCueEventIndex == eventIndex
                && _activeSceneCueIndex == effectIndex;
        }

        private bool IsSceneEditingOnHitCue(int eventIndex, int damageEffectIndex, int onHitEffectIndex)
        {
            return _activeSceneHitVfxEventIndex >= 0
                && _activeSceneHitVfxDamageIndex >= 0
                && _activeSceneHitVfxIndex >= 0
                && _activeSceneHitVfxEventIndex == eventIndex
                && _activeSceneHitVfxDamageIndex == damageEffectIndex
                && _activeSceneHitVfxIndex == onHitEffectIndex;
        }

        private bool IsSceneEditingCompanionCue(int eventIndex, int damageEffectIndex, int companionEffectIndex)
        {
            return _activeSceneCompanionVfxEventIndex >= 0
                && _activeSceneCompanionVfxDamageIndex >= 0
                && _activeSceneCompanionVfxIndex >= 0
                && _activeSceneCompanionVfxEventIndex == eventIndex
                && _activeSceneCompanionVfxDamageIndex == damageEffectIndex
                && _activeSceneCompanionVfxIndex == companionEffectIndex;
        }

        private bool DrawEffectFoldout(string key, string label)
        {
            bool expanded = !_collapsedEffectKeys.Contains(key);
            bool newExpanded = EditorGUILayout.Foldout(expanded, label, true);
            if (newExpanded != expanded)
            {
                if (newExpanded)
                    _collapsedEffectKeys.Remove(key);
                else
                    _collapsedEffectKeys.Add(key);
            }

            return newExpanded;
        }

        private static string BuildFoldoutLabel(string title, string summary = null)
        {
            return title;
        }

        private static string BuildIndexedFoldoutLabel(string title, int index, string summary = null)
        {
            return BuildFoldoutLabel($"{title} {index}", summary);
        }

        private static string GetVfxEffectSummary(SkillVfxEffect effect)
        {
            return effect?.particlePrefab != null ? effect.particlePrefab.name : string.Empty;
        }

        private static string GetSfxEffectSummary(SkillSfxEffect effect)
        {
            return effect?.audioClip != null ? effect.audioClip.name : string.Empty;
        }

        private static string GetHitDamageEffectSummary(SkillHitDamageEffect effect)
        {
            return effect != null ? $"倍率 {effect.damageMagnitude:0.##}" : string.Empty;
        }

        private static string GetAttributeEffectSummary(SkillAttributeEffect effect)
        {
            if (effect == null)
                return string.Empty;

            string targetText = effect.targetMode == SkillTargetMode.DetectedTargets ? "命中目标" : "自身";
            string valueText = effect.usePercent ? $"{effect.magnitude:0.##}%" : effect.magnitude.ToString("0.##");
            return $"{targetText} {GetStatFieldDisplayName(effect.statField)} {valueText}{GetAttributeEffectDurationSuffix(effect)}";
        }

        private string GetDamageEffectFoldoutKey(int eventIndex, int damageEffectIndex)
        {
            return $"damagefold:{eventIndex}:{damageEffectIndex}";
        }

        private string GetHitStopFoldoutKey(int eventIndex, int damageEffectIndex)
        {
            return $"hitstopfold:{eventIndex}:{damageEffectIndex}";
        }

        private string GetTopLevelPhysicsFoldoutKey(int eventIndex, int effectIndex)
        {
            return $"physicsfold:{eventIndex}:{effectIndex}";
        }

        private string GetTopLevelAttributeFoldoutKey(int eventIndex, int effectIndex)
        {
            return $"attrfold:{eventIndex}:{effectIndex}";
        }

        private string GetTopLevelVfxFoldoutKey(int eventIndex, int effectIndex)
        {
            return $"vfxfold:{eventIndex}:{effectIndex}";
        }

        private string GetTopLevelSfxFoldoutKey(int eventIndex, int effectIndex)
        {
            return $"sfxfold:{eventIndex}:{effectIndex}";
        }

        private string GetNestedHitDamageFoldoutKey(int eventIndex, int damageEffectIndex, int effectIndex)
        {
            return $"hitdamagefold:{eventIndex}:{damageEffectIndex}:{effectIndex}";
        }

        private string GetNestedPhysicsFoldoutKey(int eventIndex, int damageEffectIndex, int effectIndex)
        {
            return $"hitphysicsfold:{eventIndex}:{damageEffectIndex}:{effectIndex}";
        }

        private string GetNestedAttributeFoldoutKey(int eventIndex, int damageEffectIndex, int effectIndex)
        {
            return $"hitattrfold:{eventIndex}:{damageEffectIndex}:{effectIndex}";
        }

        private string GetNestedVfxFoldoutKey(int eventIndex, int damageEffectIndex, int effectIndex)
        {
            return $"hitvfxfold:{eventIndex}:{damageEffectIndex}:{effectIndex}";
        }

        private string GetCompanionVfxFoldoutKey(int eventIndex, int damageEffectIndex, int effectIndex)
        {
            return $"companionvfxfold:{eventIndex}:{damageEffectIndex}:{effectIndex}";
        }

        private string GetNestedSfxFoldoutKey(int eventIndex, int damageEffectIndex, int effectIndex)
        {
            return $"hitsfxfold:{eventIndex}:{damageEffectIndex}:{effectIndex}";
        }

        private string GetEditorCueVisibilityKey(int eventIndex, int effectIndex)
        {
            return $"cue:{eventIndex}:{effectIndex}";
        }

        private string GetEditorOnHitCueVisibilityKey(int eventIndex, int damageEffectIndex, int onHitEffectIndex)
        {
            return $"hitcue:{eventIndex}:{damageEffectIndex}:{onHitEffectIndex}";
        }

        private string GetEditorCompanionCueVisibilityKey(int eventIndex, int damageEffectIndex, int companionEffectIndex)
        {
            return $"bodycue:{eventIndex}:{damageEffectIndex}:{companionEffectIndex}";
        }

        private string GetEditorSfxVisibilityKey(int eventIndex, int effectIndex)
        {
            return $"sfx:{eventIndex}:{effectIndex}";
        }

        private string GetEditorOnHitSfxVisibilityKey(int eventIndex, int damageEffectIndex, int onHitEffectIndex)
        {
            return $"hitsfx:{eventIndex}:{damageEffectIndex}:{onHitEffectIndex}";
        }

        private bool IsTopLevelCueVisible(int eventIndex, int effectIndex)
        {
            return !_hiddenPreviewCueKeys.Contains(GetEditorCueVisibilityKey(eventIndex, effectIndex));
        }

        private bool IsOnHitCueVisible(int eventIndex, int damageEffectIndex, int onHitEffectIndex)
        {
            return !_hiddenPreviewCueKeys.Contains(GetEditorOnHitCueVisibilityKey(eventIndex, damageEffectIndex, onHitEffectIndex));
        }

        private bool IsCompanionCueVisible(int eventIndex, int damageEffectIndex, int companionEffectIndex)
        {
            return !_hiddenPreviewCueKeys.Contains(GetEditorCompanionCueVisibilityKey(eventIndex, damageEffectIndex, companionEffectIndex));
        }

        private bool IsTopLevelSfxVisible(int eventIndex, int effectIndex)
        {
            return !_hiddenPreviewCueKeys.Contains(GetEditorSfxVisibilityKey(eventIndex, effectIndex));
        }

        private bool IsOnHitSfxVisible(int eventIndex, int damageEffectIndex, int onHitEffectIndex)
        {
            return !_hiddenPreviewCueKeys.Contains(GetEditorOnHitSfxVisibilityKey(eventIndex, damageEffectIndex, onHitEffectIndex));
        }

        private bool TryGetCurrentSceneCuePlaybackContext(out float triggerTime, out Transform explicitTarget)
        {
            triggerTime = 0f;
            explicitTarget = null;

            SkillTimelineEvent evt = GetSelectedSceneEvent();
            if (evt == null)
                return false;

            if (_selectedEventTrackType == TimelineTrackType.Damage
                && _activeSceneHitVfxDamageIndex >= 0
                && _activeSceneHitVfxIndex >= 0)
            {
                SharedSkillDefinition skill = GetSelectedSkill();
                if (skill == null)
                    return false;

                List<PreviewDamageHitResult> hits = CollectPreviewDamageHitHistory(skill);
                PreviewDamageHitResult bestHit = null;
                for (int i = 0; i < hits.Count; i++)
                {
                    PreviewDamageHitResult hit = hits[i];
                    if (hit == null
                        || hit.eventIndex != _selectedEventIndex
                        || hit.effectIndex != _activeSceneHitVfxDamageIndex
                        || hit.hitTargets == null
                        || hit.hitTargets.Count == 0
                        || hit.hitTime > _previewTime + 0.0001f)
                        continue;

                    if (bestHit == null || hit.hitTime > bestHit.hitTime)
                        bestHit = hit;
                }

                if (bestHit == null)
                    return false;

                triggerTime = bestHit.hitTime;
                explicitTarget = bestHit.hitTargets[0];
                return true;
            }

            if (_activeSceneCueIndex < 0 || _previewTime + 0.0001f < evt.startTime)
                return false;

            triggerTime = evt.startTime;
            if (evt.triggerMode == SkillEventTriggerMode.Repeated)
            {
                float interval = Mathf.Max(0.01f, evt.repeatInterval);
                float repeatedEndTime = evt.activeDurationMode == SkillEventActiveDurationMode.UntilStateExit
                    ? Mathf.Max(evt.startTime, GetPreviewStateExitTime())
                    : evt.startTime + Mathf.Max(0f, evt.activeDuration);
                float endTime = Mathf.Min(repeatedEndTime, _previewTime);
                for (float time = evt.startTime; time <= endTime + 0.0001f; time += interval)
                    triggerTime = time;
            }

            return true;
        }

        private bool TryGetTopLevelSceneCuePlaybackContext(SkillVfxEvent evt, out float triggerTime, out Transform explicitTarget)
        {
            triggerTime = 0f;
            explicitTarget = null;

            if (evt == null || _activeSceneCueIndex < 0 || _previewTime + 0.0001f < evt.startTime)
                return false;

            triggerTime = evt.startTime;
            if (evt.triggerMode == SkillEventTriggerMode.Repeated)
            {
                float interval = Mathf.Max(0.01f, evt.repeatInterval);
                float repeatedEndTime = evt.activeDurationMode == SkillEventActiveDurationMode.UntilStateExit
                    ? Mathf.Max(evt.startTime, GetPreviewStateExitTime())
                    : evt.startTime + Mathf.Max(0f, evt.activeDuration);
                float endTime = Mathf.Min(repeatedEndTime, _previewTime);
                for (float time = evt.startTime; time <= endTime + 0.0001f; time += interval)
                    triggerTime = time;
            }

            return true;
        }

        private bool TryGetOnHitSceneCuePlaybackContext(out float triggerTime, out Transform explicitTarget)
        {
            triggerTime = 0f;
            explicitTarget = null;

            GameObject explicitVictim = ResolveRootPreviewTarget(_previewVictimTarget);
            if (explicitVictim == null
                || _activeSceneHitVfxEventIndex < 0
                || _activeSceneHitVfxDamageIndex < 0
                || _activeSceneHitVfxIndex < 0)
            {
                return false;
            }

            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return false;

            List<PreviewDamageHitResult> hits = CollectPreviewDamageHitHistory(skill);
            PreviewDamageHitResult bestHit = null;
            for (int i = 0; i < hits.Count; i++)
            {
                PreviewDamageHitResult hit = hits[i];
                if (hit == null
                    || hit.eventIndex != _activeSceneHitVfxEventIndex
                    || hit.effectIndex != _activeSceneHitVfxDamageIndex
                    || hit.hitTargets == null
                    || hit.hitTargets.Count == 0
                    || hit.hitTime > _previewTime + 0.0001f)
                {
                    continue;
                }

                if (bestHit == null || hit.hitTime > bestHit.hitTime)
                    bestHit = hit;
            }

            if (bestHit == null)
            {
                if (!TryGetSceneDamageTriggerTime(_activeSceneHitVfxEventIndex, out triggerTime))
                    return false;

                explicitTarget = explicitVictim.transform;
                return true;
            }

            triggerTime = bestHit.hitTime;
            explicitTarget = bestHit.hitTargets[0];
            return true;
        }

        private bool TryGetSceneCompanionCuePlaybackContext(out float triggerTime)
        {
            triggerTime = 0f;
            if (_activeSceneCompanionVfxEventIndex < 0
                || _activeSceneCompanionVfxDamageIndex < 0
                || _activeSceneCompanionVfxIndex < 0)
            {
                return false;
            }

            return TryGetSceneDamageTriggerTime(_activeSceneCompanionVfxEventIndex, out triggerTime);
        }

        private bool TryEvaluatePreviewCueTransform(SkillVfxEffect cue, Transform explicitTarget, float triggerTime, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (cue == null || _previewTarget == null)
                return false;

            if (cue.anchor == CueAnchor.World)
            {
                EnsureScenePreviewCueWorldOrigin(cue, triggerTime);
                if (!_scenePreviewCueHasWorldOrigin)
                    return false;

                EvaluateWorldCueTransform(
                    cue,
                    _scenePreviewCueWorldOriginPosition,
                    _scenePreviewCueWorldOriginRotation,
                    Mathf.Max(0f, _previewTime - triggerTime),
                    out position,
                    out rotation);
                return true;
            }

            Transform anchor = ResolveCueAnchorTransform(cue.anchor, cue, explicitTarget);
            if (anchor == null)
                anchor = _previewTarget.transform;
            if (anchor == null)
                return false;

            position = anchor.TransformPoint(cue.offset);
            rotation = anchor.rotation * Quaternion.Euler(cue.rotationEuler);
            return true;
        }

        private void EnsureTimelinePreviewCueWorldOrigin(TimelinePreviewVfxInstance playback)
        {
            if (playback == null || playback.effect == null || playback.effect.anchor != CueAnchor.World || playback.hasWorldOrigin)
                return;

            if (!TryCapturePreviewCueWorldOrigin(playback.effect, out playback.originPosition, out playback.originRotation))
                return;

            playback.hasWorldOrigin = true;
        }

        private void EnsureScenePreviewCueWorldOrigin(SkillVfxEffect cue, float triggerTime)
        {
            if (cue == null || cue.anchor != CueAnchor.World)
            {
                _scenePreviewCueHasWorldOrigin = false;
                _scenePreviewCueWorldOriginTriggerTime = -1f;
                return;
            }

            if (_scenePreviewCueHasWorldOrigin && Mathf.Abs(_scenePreviewCueWorldOriginTriggerTime - triggerTime) <= 0.0001f)
                return;

            if (!TryCapturePreviewCueWorldOrigin(cue, out _scenePreviewCueWorldOriginPosition, out _scenePreviewCueWorldOriginRotation))
                return;

            _scenePreviewCueHasWorldOrigin = true;
            _scenePreviewCueWorldOriginTriggerTime = triggerTime;
        }

        private void InvalidateScenePreviewCueWorldOrigin()
        {
            _scenePreviewCueHasWorldOrigin = false;
            _scenePreviewCueWorldOriginTriggerTime = -1f;
        }

        private bool TryCapturePreviewCueWorldOrigin(SkillVfxEffect cue, out Vector3 originPosition, out Quaternion originRotation)
        {
            originPosition = Vector3.zero;
            originRotation = Quaternion.identity;
            if (cue == null || _previewTarget == null)
                return false;

            Transform basis = _previewTarget.transform;
            if (basis == null)
                return false;

            originPosition = basis.TransformPoint(cue.offset);
            originRotation = basis.rotation;
            return true;
        }

        private static void EvaluateWorldCueTransform(SkillVfxEffect cue, Vector3 originPosition, Quaternion originRotation, float elapsed, out Vector3 position, out Quaternion rotation)
        {
            position = originPosition;
            rotation = originRotation * Quaternion.Euler(cue.rotationEuler);

            if (!CueUsesWorldMotion(cue))
                return;

            Vector3 direction = cue.motion.direction.sqrMagnitude > 0.0001f
                ? cue.motion.direction.normalized
                : Vector3.forward;
            position += originRotation * direction * (cue.motion.speed * Mathf.Max(0f, elapsed));
        }

        private static bool CueUsesWorldMotion(SkillVfxEffect cue)
        {
            return cue != null
                && cue.anchor == CueAnchor.World
                && cue.motion != null
                && cue.motion.IsActive;
        }

        private void DrawSceneCueBounds(SkillVfxEffect cue, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (cue?.particlePrefab == null)
                return;

            if (!TryGetCuePrefabLocalBounds(cue.particlePrefab, out Bounds localBounds))
                return;

            Color oldColor = Handles.color;
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.color = new Color(0.18f, 0.9f, 1f, 0.9f);
            Handles.matrix = Matrix4x4.TRS(worldPosition, worldRotation, cue.scale);
            Handles.DrawWireCube(localBounds.center, localBounds.size);
            Handles.matrix = oldMatrix;
            Handles.color = oldColor;
        }

        private static bool TryGetCuePrefabLocalBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            if (prefab == null)
                return false;

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return false;

            Transform root = prefab.transform;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Bounds rendererBounds = TransformLocalBoundsToRoot(root, renderer.transform, renderer.localBounds);
                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds;
        }

        private static Bounds TransformLocalBoundsToRoot(Transform root, Transform source, Bounds localBounds)
        {
            Matrix4x4 matrix = root.worldToLocalMatrix * source.localToWorldMatrix;
            Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
            Vector3 extents = localBounds.extents;

            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));

            Vector3 transformedExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

            return new Bounds(center, transformedExtents * 2f);
        }

        private void SimulateScenePreviewParticles(float localTime)
        {
            if (_scenePreviewCueInstance == null)
                return;

            ParticleSystem[] particleSystems = _scenePreviewCueInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Simulate(localTime, false, true, true);
            }
        }

        private void DestroyScenePreviewCueInstance()
        {
            if (_scenePreviewCueInstance != null)
                Object.DestroyImmediate(_scenePreviewCueInstance);

            _scenePreviewCueInstance = null;
            _scenePreviewCuePrefab = null;
            _scenePreviewCueTrackType = TimelineTrackType.Vfx;
            _scenePreviewCueEventIndex = -1;
            _scenePreviewCueDamageIndex = -1;
            _scenePreviewCueListIndex = -1;
            _scenePreviewCueFollowsDamageWindow = false;
            _scenePreviewCueHasWorldOrigin = false;
            _scenePreviewCueWorldOriginTriggerTime = -1f;
            _scenePreviewCueWorldOriginPosition = Vector3.zero;
            _scenePreviewCueWorldOriginRotation = Quaternion.identity;
        }

        private static void DrawWireSphere(Vector3 center, float radius, Transform preview)
        {
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, preview.right, radius);
            Handles.DrawWireDisc(center, preview.forward, radius);
        }

        private static void DrawWireSector(Vector3 center, Vector3 forward, Vector3 up, float radius, float angle)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(forward, up).normalized;
            if (planarForward.sqrMagnitude <= 0.0001f)
                planarForward = Vector3.forward;

            Vector3 leftDir = Quaternion.AngleAxis(-angle * 0.5f, up) * planarForward;
            Vector3 rightDir = Quaternion.AngleAxis(angle * 0.5f, up) * planarForward;
            Handles.DrawWireArc(center, up, leftDir, angle, radius);
            Handles.DrawLine(center, center + leftDir * radius);
            Handles.DrawLine(center, center + rightDir * radius);
        }

        private static void DrawWireBox(Vector3 center, Quaternion rotation, Vector3 size)
        {
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, size);
            Handles.matrix = oldMatrix;
        }

        private static void ApplyHideFlagsRecursively(GameObject root, HideFlags flags)
        {
            if (root == null)
                return;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null)
                    continue;

                transform.gameObject.hideFlags = flags;
                Component[] components = transform.GetComponents<Component>();
                for (int j = 0; j < components.Length; j++)
                {
                    Component component = components[j];
                    if (component != null)
                        component.hideFlags = flags;
                }
            }
        }

        private Transform ResolveSceneCueAnchor(CueAnchor anchor, SkillVfxEffect cue = null)
        {
            return ResolveCueAnchorTransform(anchor, cue, null);
        }

        private Transform ResolveCueEditBasisTransform(CueAnchor anchor, Transform explicitTarget)
        {
            return anchor == CueAnchor.Target
                ? ResolveCueAnchorTransform(anchor, null, explicitTarget)
                : _previewTarget != null ? _previewTarget.transform : null;
        }

        private CueAnchor DrawCueAnchorPopup(string label, CueAnchor current, CueAnchor[] values, string[] labels)
        {
            int currentIndex = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != current)
                    continue;

                currentIndex = i;
                break;
            }

            int selectedIndex = EditorGUILayout.Popup(label, currentIndex, labels);
            return values[Mathf.Clamp(selectedIndex, 0, values.Length - 1)];
        }

        private Transform ResolveCueAnchorTransform(CueAnchor anchor, SkillVfxEffect cue, Transform explicitTarget)
        {
            if (_previewTarget == null)
                return null;

            switch (anchor)
            {
                case CueAnchor.Target:
                    return explicitTarget ?? ResolvePrimaryPreviewHitTarget() ?? _previewTarget.transform;
                default:
                    return _previewTarget.transform;
            }
        }

        private Transform ResolvePrimaryPreviewHitTarget()
        {
            GameObject explicitVictim = ResolveRootPreviewTarget(_previewVictimTarget);
            if (explicitVictim != null)
                return explicitVictim.transform;

            SharedSkillDefinition skill = GetSelectedSkill();
            if (skill == null)
                return null;

            List<PreviewDamageHitResult> hits = CollectCurrentPreviewDamageHits(skill);
            for (int i = 0; i < hits.Count; i++)
            {
                PreviewDamageHitResult hit = hits[i];
                if (hit?.hitTargets == null || hit.hitTargets.Count == 0)
                    continue;

                if (_selectedEventTrackType == TimelineTrackType.Damage
                    && _selectedEventIndex == hit.eventIndex
                    && (_activeSceneDamageIndex < 0 || _activeSceneDamageIndex == hit.effectIndex))
                {
                    return hit.hitTargets[0];
                }
            }

            for (int i = 0; i < hits.Count; i++)
            {
                PreviewDamageHitResult hit = hits[i];
                if (hit?.hitTargets != null && hit.hitTargets.Count > 0)
                    return hit.hitTargets[0];
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void FitZoomToWindow()
        {
            float width = Mathf.Max(100f, _lastTimelinePanelWidth - 40f);
            float duration = Mathf.Max(GetPreviewTotalDuration(), 2f);
            _zoom = Mathf.Clamp(width / duration, 20f, 800f);
            _timelineScrollX = 0f;
            Repaint();
        }

        private float GetActiveSnapStep()
        {
            switch (_snapMode)
            {
                case TimelineSnapMode.Frame:
                    return 1f / Mathf.Max(1, _frameRate);
                case TimelineSnapMode.Grid:
                    return ChooseTickInterval(_zoom);
                default:
                    return 0f;
            }
        }

        private float SnapTime(float time)
        {
            float step = GetActiveSnapStep();
            if (step <= 0f)
                return time;

            return Mathf.Max(0f, Mathf.Round(time / step) * step);
        }

        private float SnapDuration(float time)
        {
            float step = GetActiveSnapStep();
            if (step <= 0f)
                return time;

            return Mathf.Max(step, Mathf.Round(time / step) * step);
        }

        private string GetSnapModeLabel()
        {
            switch (_snapMode)
            {
                case TimelineSnapMode.Frame:
                    return "帧";
                case TimelineSnapMode.Grid:
                    return "网格";
                default:
                    return "关闭";
            }
        }

        private string FormatRulerTime(float time)
        {
            switch (_timeDisplayMode)
            {
                case TimelineTimeDisplayMode.Frames:
                    return FormatFrameOnly(time);
                case TimelineTimeDisplayMode.SecondsAndFrames:
                    return $"{time:F2}s/{TimeToFrame(time)}f";
                default:
                    return $"{time:F2}s";
            }
        }

        private string FormatTimelineTime(float time)
        {
            switch (_timeDisplayMode)
            {
                case TimelineTimeDisplayMode.Frames:
                    return FormatFrameOnly(time);
                case TimelineTimeDisplayMode.SecondsAndFrames:
                    return $"{time:F2}s ({TimeToFrame(time)}f)";
                default:
                    return $"{time:F2}s";
            }
        }

        private string FormatFrameOnly(float time)
        {
            return $"{TimeToFrame(time)}f @{Mathf.Max(1, _frameRate)}fps";
        }

        private int TimeToFrame(float time)
        {
            return Mathf.RoundToInt(time * Mathf.Max(1, _frameRate));
        }

        private static Vector3 ClampVector3(Vector3 value, float minValue)
        {
            return new Vector3(
                Mathf.Max(minValue, value.x),
                Mathf.Max(minValue, value.y),
                Mathf.Max(minValue, value.z));
        }
    }
}

