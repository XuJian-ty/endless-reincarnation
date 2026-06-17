using System;
using System.Collections.Generic;
using System.IO;
using Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    public sealed class SkillTreeGraphEditorWindow : EditorWindow
    {
        private const string GraphAssetPath = "Assets/Resources/配置/技能树图配置.asset";
        private const string SkillConfigAssetPath = "Assets/Resources/配置/玩家动作及技能配置库.asset";
        private const string SkillTreePanelPrefabPath = "Assets/Resources/UI/SkillTreePanel.prefab";
        private static readonly Vector2 PreviewCanvasReferenceResolution = new Vector2(1920f, 1080f);
        private const float NodeDiameter = 92f;
        private const float EdgeThickness = 6f;
        private const float GridStep = 120f;
        private const float ToolbarHeight = 50f;
        private const float MinCanvasZoom = 0.75f;
        private const float MaxCanvasZoom = 2.4f;

        private SkillTreeGraphConfigSO _graphConfig;
        private SkillConfigDatabaseSO _skillConfig;
        private Vector2 _canvasScroll;
        private float _canvasZoom = 1f;
        private string _selectedNodeId;
        private string _draggingNodeId;
        private bool _isCanvasPointerCaptured;
        private bool _isPanningCanvas;
        private Vector2 _lastCanvasMousePosition;
        private bool _shouldCenterCanvasView = true;
        private Vector2 _runtimeViewportReferenceSize = new Vector2(1440f, 720f);

        [MenuItem("游戏/技能树图编辑器")]
        private static void OpenWindow()
        {
            SkillTreeGraphEditorWindow window = GetWindow<SkillTreeGraphEditorWindow>("技能树图编辑器");
            window.minSize = new Vector2(1200f, 760f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            _graphConfig = AssetDatabase.LoadAssetAtPath<SkillTreeGraphConfigSO>(GraphAssetPath);
            _skillConfig = AssetDatabase.LoadAssetAtPath<SkillConfigDatabaseSO>(SkillConfigAssetPath);
            _shouldCenterCanvasView = true;
            RefreshRuntimeViewportMetrics();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawCanvasPanel();
        }

        private void DrawToolbar()
        {
            Rect toolbarRect = new Rect(0f, 0f, position.width, ToolbarHeight);
            GUILayout.BeginArea(toolbarRect, EditorStyles.toolbar);
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            _graphConfig = (SkillTreeGraphConfigSO)EditorGUILayout.ObjectField(_graphConfig, typeof(SkillTreeGraphConfigSO), false, GUILayout.Width(260f));
            _skillConfig = (SkillConfigDatabaseSO)EditorGUILayout.ObjectField(_skillConfig, typeof(SkillConfigDatabaseSO), false, GUILayout.Width(260f));

            if (_graphConfig == null && GUILayout.Button("创建图配置", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                CreateGraphConfigAsset();

            using (new EditorGUI.DisabledScope(_graphConfig == null || _skillConfig == null))
            {
                if (GUILayout.Button("同步技能节点", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                    SyncNodesFromSkillConfig();

                if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                    ValidateCurrentGraph(true);

                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                    SaveGraphAsset();

                if (GUILayout.Button("刷新预览", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    _shouldCenterCanvasView = true;
                    RefreshRuntimeViewportMetrics();
                }

                if (GUILayout.Button("居中视图", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    _shouldCenterCanvasView = true;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawToolbarOverlay();

            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawCanvasPanel()
        {
            Rect panelRect = new Rect(4f, ToolbarHeight + 2f, Mathf.Max(0f, position.width - 8f), Mathf.Max(0f, position.height - ToolbarHeight - 6f));
            GUI.Box(panelRect, GUIContent.none, EditorStyles.helpBox);

            if (_graphConfig == null)
            {
                EditorGUI.LabelField(panelRect, "未找到技能树图配置，请先创建或指定配置资产。", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Rect availableRect = new Rect(panelRect.x + 8f, panelRect.y + 8f, panelRect.width - 16f, panelRect.height - 16f);
            Rect viewportRect = GetGraphPreviewRect(availableRect);
            Vector2 logicalContentSize = new Vector2(Mathf.Max(800f, _graphConfig.contentSize.x), Mathf.Max(600f, _graphConfig.contentSize.y));

            EnsureCanvasViewCenteredIfNeeded(viewportRect, logicalContentSize);
            HandleCanvasEvents(viewportRect, logicalContentSize);
            _canvasScroll = ClampCanvasScroll(_canvasScroll, viewportRect.size, GetDisplayedContentSize(logicalContentSize, viewportRect));

            EditorGUI.DrawRect(viewportRect, new Color(0.08f, 0.10f, 0.09f, 1f));
            GUI.BeginClip(viewportRect);
            float renderScale = GetCanvasRenderScale(viewportRect);
            DrawCanvasBackground(viewportRect.size, logicalContentSize, renderScale);
            DrawEdges(renderScale);
            DrawNodes(renderScale);
            GUI.EndClip();
        }

        private void DrawToolbarOverlay()
        {
            if (_graphConfig == null)
            {
                EditorGUILayout.LabelField("请先指定技能树图配置。", EditorStyles.miniLabel);
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("内容尺寸", GUILayout.Width(52f));
            Vector2 newContentSize = EditorGUILayout.Vector2Field(GUIContent.none, _graphConfig.contentSize, GUILayout.Width(180f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_graphConfig, "修改技能树图谱尺寸");
                _graphConfig.contentSize = new Vector2(Mathf.Max(800f, newContentSize.x), Mathf.Max(600f, newContentSize.y));
                EditorUtility.SetDirty(_graphConfig);
                _shouldCenterCanvasView = true;
            }

            SkillTreeNodeDefinition selectedNode = _graphConfig.GetNodeById(_selectedNodeId);
            if (selectedNode == null)
            {
                GUILayout.Space(8f);
                EditorGUILayout.LabelField("左键拖拽空白处平移画布，滚轮缩放，右键节点编辑前驱。", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                DrawValidationStatusLabel();
                EditorGUILayout.EndHorizontal();
                return;
            }

            SkillConfigEntry entry = _skillConfig != null ? _skillConfig.GetEntryByActionId(selectedNode.actionId) : null;
            string displayName = entry != null && !string.IsNullOrWhiteSpace(entry.displayName)
                ? entry.displayName.Trim()
                : selectedNode.actionId;

            GUILayout.Space(12f);
            EditorGUILayout.LabelField($"当前节点：{displayName}", GUILayout.Width(220f));
            EditorGUILayout.LabelField("坐标", GUILayout.Width(28f));
            EditorGUI.BeginChangeCheck();
            float newPositionX = EditorGUILayout.FloatField(selectedNode.position.x, GUILayout.Width(72f));
            float newPositionY = EditorGUILayout.FloatField(selectedNode.position.y, GUILayout.Width(72f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_graphConfig, "修改技能树节点位置");
                selectedNode.position = ClampToContent(new Vector2(newPositionX, newPositionY));
                EditorUtility.SetDirty(_graphConfig);
                Repaint();
            }

            GUILayout.Space(12f);
            EditorGUILayout.LabelField($"节点ID：{selectedNode.nodeId}", EditorStyles.miniLabel, GUILayout.Width(180f));
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("右键节点可编辑前驱。", EditorStyles.miniLabel, GUILayout.Width(120f));
            GUILayout.FlexibleSpace();
            DrawValidationStatusLabel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawValidationStatusLabel()
        {
            List<string> errors = _graphConfig.CollectValidationErrors(_skillConfig);
            if (errors.Count == 0)
            {
                EditorGUILayout.LabelField("校验通过", EditorStyles.miniLabel, GUILayout.Width(60f));
                return;
            }

            EditorGUILayout.LabelField($"校验异常 {errors.Count} 条", EditorStyles.miniLabel, GUILayout.Width(96f));
        }

        private void DrawPredecessorSelector(SkillTreeNodeDefinition selectedNode)
        {
            if (_graphConfig.nodes == null)
                return;

            for (int i = 0; i < _graphConfig.nodes.Count; i++)
            {
                SkillTreeNodeDefinition candidate = _graphConfig.nodes[i];
                if (candidate == null || candidate == selectedNode)
                    continue;

                SkillConfigEntry candidateEntry = _skillConfig != null ? _skillConfig.GetEntryByActionId(candidate.actionId) : null;
                string label = candidateEntry != null && !string.IsNullOrWhiteSpace(candidateEntry.displayName)
                    ? candidateEntry.displayName.Trim()
                    : candidate.actionId;

                bool contains = ContainsPredecessor(selectedNode, candidate.nodeId);
                EditorGUI.BeginChangeCheck();
                bool toggled = EditorGUILayout.ToggleLeft($"{label} ({candidate.nodeId})", contains);
                if (!EditorGUI.EndChangeCheck())
                    continue;

                Undo.RecordObject(_graphConfig, "修改技能树前驱关系");
                if (toggled)
                    AddPredecessor(selectedNode, candidate.nodeId);
                else
                    RemovePredecessor(selectedNode, candidate.nodeId);
                EditorUtility.SetDirty(_graphConfig);
            }
        }

        private string GetNodeLabel(SkillTreeNodeDefinition node)
        {
            if (node == null)
                return "节点";

            SkillConfigEntry entry = _skillConfig != null ? _skillConfig.GetEntryByActionId(node.actionId) : null;
            if (entry != null && !string.IsNullOrWhiteSpace(entry.displayName))
                return $"{entry.displayName.Trim()} ({node.nodeId})";

            return $"{node.actionId} ({node.nodeId})";
        }

        private void DrawCanvasBackground(Vector2 viewportSize, Vector2 logicalContentSize, float renderScale)
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, viewportSize.x, viewportSize.y), new Color(0.08f, 0.10f, 0.09f, 1f));
            Rect contentRect = new Rect(-_canvasScroll.x, -_canvasScroll.y, logicalContentSize.x * renderScale, logicalContentSize.y * renderScale);
            EditorGUI.DrawRect(contentRect, new Color(0.12f, 0.14f, 0.13f, 1f));
            Handles.BeginGUI();
            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.05f);

            for (float x = 0f; x <= logicalContentSize.x; x += GridStep)
            {
                float drawX = x * renderScale - _canvasScroll.x;
                Handles.DrawLine(new Vector3(drawX, contentRect.yMin), new Vector3(drawX, contentRect.yMax));
            }

            for (float y = 0f; y <= logicalContentSize.y; y += GridStep)
            {
                float drawY = y * renderScale - _canvasScroll.y;
                Handles.DrawLine(new Vector3(contentRect.xMin, drawY), new Vector3(contentRect.xMax, drawY));
            }

            Handles.color = oldColor;
            Handles.EndGUI();
        }

        private void DrawEdges(float renderScale)
        {
            if (_graphConfig.nodes == null)
                return;

            Handles.BeginGUI();
            Color oldColor = Handles.color;
            for (int i = 0; i < _graphConfig.nodes.Count; i++)
            {
                SkillTreeNodeDefinition node = _graphConfig.nodes[i];
                if (node?.predecessorNodeIds == null)
                    continue;

                for (int j = 0; j < node.predecessorNodeIds.Count; j++)
                {
                    string predecessorNodeId = node.predecessorNodeIds[j];
                    if (string.IsNullOrWhiteSpace(predecessorNodeId))
                        continue;

                    SkillTreeNodeDefinition predecessor = _graphConfig.GetNodeById(predecessorNodeId);
                    if (predecessor == null)
                        continue;

                    List<Vector2> edgePoints = BuildEdgePoints(node, predecessorNodeId.Trim(), predecessor.position, node.position);
                    Vector3[] drawPoints = new Vector3[edgePoints.Count];
                    for (int k = 0; k < edgePoints.Count; k++)
                        drawPoints[k] = CanvasToViewportLocalPoint(edgePoints[k], renderScale);

                    Handles.color = new Color(0.50f, 0.77f, 0.70f, 0.92f);
                    Handles.DrawAAPolyLine(Mathf.Max(1f, EdgeThickness * renderScale), drawPoints);
                }
            }
            Handles.color = oldColor;
            Handles.EndGUI();
        }

        private void DrawNodes(float renderScale)
        {
            if (_graphConfig.nodes == null)
                return;

            for (int i = 0; i < _graphConfig.nodes.Count; i++)
            {
                SkillTreeNodeDefinition node = _graphConfig.nodes[i];
                if (node == null)
                    continue;

                Rect nodeRect = GetScaledNodeRect(node, renderScale);
                SkillConfigEntry entry = _skillConfig != null ? _skillConfig.GetEntryByActionId(node.actionId) : null;
                bool isSelected = string.Equals(_selectedNodeId, node.nodeId, StringComparison.Ordinal);

                EditorGUI.DrawRect(nodeRect, isSelected
                    ? new Color(0.24f, 0.58f, 0.50f, 1f)
                    : new Color(0.24f, 0.27f, 0.29f, 1f));

                if (entry != null && entry.skillIcon != null)
                {
                    float padding = Mathf.Max(6f, 12f * renderScale);
                    GUI.DrawTexture(new Rect(nodeRect.x + padding, nodeRect.y + padding, nodeRect.width - padding * 2f, nodeRect.height - padding * 2f), entry.skillIcon.texture, ScaleMode.ScaleToFit, true);
                }
            }
        }

        private void HandleCanvasEvents(Rect viewportRect, Vector2 logicalContentSize)
        {
            Event current = Event.current;
            if (current == null)
                return;

            EventType eventType = current.type;
            if (eventType == EventType.Ignore || eventType == EventType.Used)
                eventType = current.rawType;
            Vector2 mousePosition = current.mousePosition;
            bool isDraggingCanvasInteraction = (eventType == EventType.MouseDrag || eventType == EventType.MouseUp)
                && (_isCanvasPointerCaptured || _isPanningCanvas || !string.IsNullOrWhiteSpace(_draggingNodeId));
            if (eventType == EventType.MouseUp)
            {
                bool hadCanvasInteraction = _isCanvasPointerCaptured || _isPanningCanvas || !string.IsNullOrWhiteSpace(_draggingNodeId);
                _isCanvasPointerCaptured = false;
                _draggingNodeId = null;
                _isPanningCanvas = false;
                if (hadCanvasInteraction)
                    current.Use();
                return;
            }

            if (!viewportRect.Contains(mousePosition) && !isDraggingCanvasInteraction)
                return;

            EditorGUIUtility.AddCursorRect(viewportRect, MouseCursor.Pan);
            Vector2 canvasPoint = ViewportToCanvasPoint(mousePosition, viewportRect);
            switch (eventType)
            {
                case EventType.ScrollWheel:
                    HandleCanvasZoom(mousePosition, viewportRect, logicalContentSize, current.delta.y);
                    current.Use();
                    break;

                case EventType.MouseDown:
                    if (current.button == 1)
                    {
                        HandleRightClick(canvasPoint);
                        current.Use();
                        break;
                    }

                    if (current.button != 0)
                        return;
                    _isCanvasPointerCaptured = true;
                    _shouldCenterCanvasView = false;
                    _lastCanvasMousePosition = mousePosition;
                    GUI.FocusControl(null);
                    HandleMouseDown(canvasPoint);
                    current.Use();
                    break;

                case EventType.MouseDrag:
                    if (!_isCanvasPointerCaptured && !_isPanningCanvas && string.IsNullOrWhiteSpace(_draggingNodeId))
                        return;
                    Vector2 mouseDelta = mousePosition - _lastCanvasMousePosition;
                    _lastCanvasMousePosition = mousePosition;
                    HandleMouseDrag(mouseDelta, viewportRect, logicalContentSize);
                    current.Use();
                    break;
            }
        }

        private void HandleMouseDown(Vector2 canvasPoint)
        {
            SkillTreeNodeDefinition clickedNode = FindNodeAt(canvasPoint);
            if (clickedNode == null)
            {
                _draggingNodeId = null;
                _isPanningCanvas = true;
                Repaint();
                return;
            }

            _selectedNodeId = clickedNode.nodeId;
            _draggingNodeId = clickedNode.nodeId;
            _isPanningCanvas = false;
            Repaint();
        }

        private void HandleRightClick(Vector2 canvasPoint)
        {
            SkillTreeNodeDefinition clickedNode = FindNodeAt(canvasPoint);
            if (clickedNode == null)
                return;

            _selectedNodeId = clickedNode.nodeId;
            ShowNodeContextMenu(clickedNode);
            Repaint();
        }

        private void ShowNodeContextMenu(SkillTreeNodeDefinition node)
        {
            if (_graphConfig?.nodes == null || node == null)
                return;

            GenericMenu menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent(GetNodeLabel(node)));
            menu.AddSeparator("");

            bool hasCandidate = false;
            for (int i = 0; i < _graphConfig.nodes.Count; i++)
            {
                SkillTreeNodeDefinition candidate = _graphConfig.nodes[i];
                if (candidate == null || candidate == node || string.IsNullOrWhiteSpace(candidate.nodeId))
                    continue;

                hasCandidate = true;
                string predecessorNodeId = candidate.nodeId.Trim();
                string label = GetNodeLabel(candidate);
                bool contains = ContainsPredecessor(node, predecessorNodeId);
                menu.AddItem(new GUIContent($"前驱/{label}"), contains, () => TogglePredecessor(node, predecessorNodeId));
            }

            if (!hasCandidate)
                menu.AddDisabledItem(new GUIContent("前驱/无可选节点"));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("清空所有前驱"), false, () => ClearAllPredecessors(node));
            menu.ShowAsContext();
        }

        private void TogglePredecessor(SkillTreeNodeDefinition node, string predecessorNodeId)
        {
            if (_graphConfig == null || node == null || string.IsNullOrWhiteSpace(predecessorNodeId))
                return;

            Undo.RecordObject(_graphConfig, "修改技能树前驱关系");
            if (ContainsPredecessor(node, predecessorNodeId))
                RemovePredecessor(node, predecessorNodeId);
            else
                AddPredecessor(node, predecessorNodeId);
            EditorUtility.SetDirty(_graphConfig);
            Repaint();
        }

        private void ClearAllPredecessors(SkillTreeNodeDefinition node)
        {
            if (_graphConfig == null || node == null)
                return;

            Undo.RecordObject(_graphConfig, "清空技能树前驱关系");
            if (node.predecessorNodeIds != null)
                node.predecessorNodeIds.Clear();
            EditorUtility.SetDirty(_graphConfig);
            Repaint();
        }

        private void HandleMouseDrag(Vector2 mouseDelta, Rect viewportRect, Vector2 logicalContentSize)
        {
            if (!string.IsNullOrWhiteSpace(_draggingNodeId) && _graphConfig != null)
            {
                SkillTreeNodeDefinition node = _graphConfig.GetNodeById(_draggingNodeId);
                if (node == null)
                    return;

                float renderScale = GetCanvasRenderScale(viewportRect);
                if (renderScale <= 0.0001f)
                    return;

                _shouldCenterCanvasView = false;
                Undo.RecordObject(_graphConfig, "拖拽技能树节点");
                node.position = ClampToContent(node.position + mouseDelta / renderScale);
                EditorUtility.SetDirty(_graphConfig);
                Repaint();
                return;
            }

            if (!_isPanningCanvas)
                return;

            _shouldCenterCanvasView = false;
            _canvasScroll = ClampCanvasScroll(_canvasScroll - mouseDelta, viewportRect.size, GetDisplayedContentSize(logicalContentSize, viewportRect));
            Repaint();
        }

        private SkillTreeNodeDefinition FindNodeAt(Vector2 canvasPoint)
        {
            if (_graphConfig?.nodes == null)
                return null;

            for (int i = _graphConfig.nodes.Count - 1; i >= 0; i--)
            {
                SkillTreeNodeDefinition node = _graphConfig.nodes[i];
                if (node == null)
                    continue;

                if (GetNodeRect(node).Contains(canvasPoint))
                    return node;
            }

            return null;
        }

        private Rect GetNodeRect(SkillTreeNodeDefinition node)
        {
            return new Rect(node.position.x - NodeDiameter * 0.5f, node.position.y - NodeDiameter * 0.5f, NodeDiameter, NodeDiameter);
        }

        private Vector2 ClampToContent(Vector2 positionValue)
        {
            float half = NodeDiameter * 0.5f;
            return new Vector2(
                Mathf.Clamp(positionValue.x, half, Mathf.Max(half, _graphConfig.contentSize.x - half)),
                Mathf.Clamp(positionValue.y, half, Mathf.Max(half, _graphConfig.contentSize.y - half)));
        }

        private List<Vector2> BuildEdgePoints(SkillTreeNodeDefinition node, string predecessorNodeId, Vector2 predecessorPosition, Vector2 currentPosition)
        {
            Vector2 start = predecessorPosition + new Vector2(NodeDiameter * 0.5f, 0f);
            Vector2 end = currentPosition + new Vector2(-NodeDiameter * 0.5f, 0f);

            List<Vector2> points = new List<Vector2>(4) { start };
            if (node.TryGetRouteOverride(predecessorNodeId, out SkillTreeEdgeRouteOverride routeOverride) && routeOverride.waypoints != null && routeOverride.waypoints.Count > 0)
            {
                for (int i = 0; i < routeOverride.waypoints.Count; i++)
                    points.Add(routeOverride.waypoints[i]);
            }
            else
            {
                float middleX = (start.x + end.x) * 0.5f;
                points.Add(new Vector2(middleX, start.y));
                points.Add(new Vector2(middleX, end.y));
            }

            points.Add(end);
            return points;
        }

        private void HandleCanvasZoom(Vector2 mousePosition, Rect viewportRect, Vector2 logicalContentSize, float wheelDelta)
        {
            if (Mathf.Abs(wheelDelta) < 0.01f)
                return;

            float zoomFactor = wheelDelta > 0f ? 0.9f : 1.1f;
            float newZoom = Mathf.Clamp(_canvasZoom * zoomFactor, MinCanvasZoom, MaxCanvasZoom);
            if (Mathf.Approximately(newZoom, _canvasZoom))
                return;

            Vector2 viewPosition = mousePosition - viewportRect.position;
            float beforeScale = GetCanvasRenderScale(viewportRect);
            if (beforeScale <= 0.0001f)
                return;

            _shouldCenterCanvasView = false;
            Vector2 canvasPoint = (_canvasScroll + viewPosition) / beforeScale;
            _canvasZoom = newZoom;
            float afterScale = GetCanvasRenderScale(viewportRect);
            _canvasScroll = ClampCanvasScroll(canvasPoint * afterScale - viewPosition, viewportRect.size, GetDisplayedContentSize(logicalContentSize, afterScale));
            Repaint();
        }

        private void EnsureCanvasViewCenteredIfNeeded(Rect viewportRect, Vector2 logicalContentSize)
        {
            if (!_shouldCenterCanvasView)
                return;

            Vector2 displayedContentSize = GetDisplayedContentSize(logicalContentSize, viewportRect);
            _canvasScroll = ClampCanvasScroll(
                new Vector2(
                    (displayedContentSize.x - viewportRect.width) * 0.5f,
                    (displayedContentSize.y - viewportRect.height) * 0.5f),
                viewportRect.size,
                displayedContentSize);
            _shouldCenterCanvasView = false;
        }

        private Vector2 ViewportToCanvasPoint(Vector2 mousePosition, Rect viewportRect)
        {
            Vector2 viewPosition = mousePosition - viewportRect.position;
            float renderScale = GetCanvasRenderScale(viewportRect);
            if (renderScale <= 0.0001f)
                return Vector2.zero;

            return (_canvasScroll + viewPosition) / renderScale;
        }

        private Rect GetGraphPreviewRect(Rect availableRect)
        {
            return availableRect;
        }

        private float GetPreviewScale(Rect viewportRect)
        {
            float widthScale = viewportRect.width / Mathf.Max(1f, _runtimeViewportReferenceSize.x);
            float heightScale = viewportRect.height / Mathf.Max(1f, _runtimeViewportReferenceSize.y);
            return Mathf.Max(widthScale, heightScale);
        }

        private float GetCanvasRenderScale(Rect viewportRect)
        {
            return GetPreviewScale(viewportRect) * _canvasZoom;
        }

        private Vector2 CanvasToViewportLocalPoint(Vector2 canvasPoint, float renderScale)
        {
            return canvasPoint * renderScale - _canvasScroll;
        }

        private Rect GetScaledNodeRect(SkillTreeNodeDefinition node, float renderScale)
        {
            Vector2 center = CanvasToViewportLocalPoint(node.position, renderScale);
            float scaledDiameter = NodeDiameter * renderScale;
            return new Rect(
                center.x - scaledDiameter * 0.5f,
                center.y - scaledDiameter * 0.5f,
                scaledDiameter,
                scaledDiameter);
        }

        private Vector2 GetDisplayedContentSize(Vector2 logicalContentSize, Rect viewportRect)
        {
            return GetDisplayedContentSize(logicalContentSize, GetCanvasRenderScale(viewportRect));
        }

        private static Vector2 GetDisplayedContentSize(Vector2 logicalContentSize, float renderScale)
        {
            return new Vector2(logicalContentSize.x * renderScale, logicalContentSize.y * renderScale);
        }

        private static Vector2 ClampCanvasScroll(Vector2 scrollValue, Vector2 viewportSize, Vector2 contentSize)
        {
            Vector2 padding = viewportSize * 0.5f;
            return new Vector2(
                Mathf.Clamp(scrollValue.x, -padding.x, Mathf.Max(-padding.x, contentSize.x - viewportSize.x + padding.x)),
                Mathf.Clamp(scrollValue.y, -padding.y, Mathf.Max(-padding.y, contentSize.y - viewportSize.y + padding.y)));
        }

        private void RefreshRuntimeViewportMetrics()
        {
            _runtimeViewportReferenceSize = new Vector2(1440f, 720f);

            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillTreePanelPrefabPath);
            if (panelPrefab == null)
                return;

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject canvasRoot = null;
            try
            {
                canvasRoot = new GameObject("SkillTreePreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(canvasRoot, previewScene);

                RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
                canvasRect.anchoredPosition = Vector2.zero;
                canvasRect.sizeDelta = PreviewCanvasReferenceResolution;

                Canvas canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = PreviewCanvasReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                GameObject panelInstance = PrefabUtility.InstantiatePrefab(panelPrefab, previewScene) as GameObject;
                if (panelInstance == null)
                    return;

                RectTransform panelRect = panelInstance.transform as RectTransform;
                panelRect.SetParent(canvasRect, false);
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                panelRect.localScale = Vector3.one;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

                RectTransform viewport = panelInstance.transform.Find("Window/SkillLibrarySection/SkillCardsRoot/GraphSection/GraphScroll/Viewport") as RectTransform;
                if (viewport != null && viewport.rect.width > 1f && viewport.rect.height > 1f)
                    _runtimeViewportReferenceSize = viewport.rect.size;
            }
            finally
            {
                if (canvasRoot != null)
                    UnityEngine.Object.DestroyImmediate(canvasRoot);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private void CreateGraphConfigAsset()
        {
            if (!Directory.Exists(Path.GetDirectoryName(GraphAssetPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(GraphAssetPath));

            SkillTreeGraphConfigSO asset = CreateInstance<SkillTreeGraphConfigSO>();
            AssetDatabase.CreateAsset(asset, GraphAssetPath);
            AssetDatabase.SaveAssets();
            _graphConfig = asset;
            Selection.activeObject = asset;
        }

        private void SyncNodesFromSkillConfig()
        {
            if (_graphConfig == null || _skillConfig == null)
                return;

            Undo.RecordObject(_graphConfig, "同步技能树节点");
            if (_graphConfig.nodes == null)
                _graphConfig.nodes = new List<SkillTreeNodeDefinition>();

            int createdCount = 0;
            int skillIndex = 0;
            for (int i = 0; i < _skillConfig.entries.Count; i++)
            {
                SkillConfigEntry entry = _skillConfig.entries[i];
                if (entry == null || (!entry.IsActiveSkill && !entry.IsPassiveSkill))
                    continue;

                string entryId = entry.GetResolvedActionId();
                if (string.IsNullOrWhiteSpace(entryId))
                    continue;

                SkillTreeNodeDefinition existingNode = _graphConfig.GetNodeByActionId(entryId);
                if (existingNode != null)
                {
                    if (string.IsNullOrWhiteSpace(existingNode.nodeId))
                        existingNode.nodeId = entryId;
                    if (string.IsNullOrWhiteSpace(existingNode.actionId))
                        existingNode.actionId = entryId;
                    continue;
                }

                int column = skillIndex % 4;
                int row = skillIndex / 4;
                skillIndex++;
                createdCount++;

                _graphConfig.nodes.Add(new SkillTreeNodeDefinition
                {
                    nodeId = entryId,
                    actionId = entryId,
                    position = new Vector2(180f + column * 280f, 160f + row * 220f),
                });
            }

            EditorUtility.SetDirty(_graphConfig);
            Repaint();
            if (createdCount > 0)
                Debug.Log($"[SkillTreeGraphEditorWindow] 已同步新增 {createdCount} 个技能树节点。");
        }

        private void SaveGraphAsset()
        {
            if (_graphConfig == null)
                return;

            EditorUtility.SetDirty(_graphConfig);
            AssetDatabase.SaveAssetIfDirty(_graphConfig);
            AssetDatabase.SaveAssets();
        }

        private void ValidateCurrentGraph(bool pingConsole)
        {
            if (_graphConfig == null)
                return;

            List<string> errors = _graphConfig.CollectValidationErrors(_skillConfig);
            if (!pingConsole)
                return;

            if (errors.Count == 0)
                Debug.Log("[SkillTreeGraphEditorWindow] 技能树图配置校验通过。");
            else
                Debug.LogWarning("[SkillTreeGraphEditorWindow] 技能树图配置校验结果：\n" + string.Join("\n", errors));
        }

        private static bool ContainsPredecessor(SkillTreeNodeDefinition node, string predecessorNodeId)
        {
            if (node?.predecessorNodeIds == null || string.IsNullOrWhiteSpace(predecessorNodeId))
                return false;

            string normalized = predecessorNodeId.Trim();
            for (int i = 0; i < node.predecessorNodeIds.Count; i++)
            {
                string current = node.predecessorNodeIds[i];
                if (string.IsNullOrWhiteSpace(current))
                    continue;

                if (string.Equals(current.Trim(), normalized, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void AddPredecessor(SkillTreeNodeDefinition node, string predecessorNodeId)
        {
            if (node == null || string.IsNullOrWhiteSpace(predecessorNodeId))
                return;

            if (node.predecessorNodeIds == null)
                node.predecessorNodeIds = new List<string>();

            if (!ContainsPredecessor(node, predecessorNodeId))
                node.predecessorNodeIds.Add(predecessorNodeId.Trim());
        }

        private static void RemovePredecessor(SkillTreeNodeDefinition node, string predecessorNodeId)
        {
            if (node?.predecessorNodeIds == null || string.IsNullOrWhiteSpace(predecessorNodeId))
                return;

            string normalized = predecessorNodeId.Trim();
            for (int i = node.predecessorNodeIds.Count - 1; i >= 0; i--)
            {
                string current = node.predecessorNodeIds[i];
                if (string.IsNullOrWhiteSpace(current))
                    continue;

                if (string.Equals(current.Trim(), normalized, StringComparison.Ordinal))
                    node.predecessorNodeIds.RemoveAt(i);
            }
        }
    }
}
