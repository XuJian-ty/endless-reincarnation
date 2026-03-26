using System;
using System.Collections.Generic;
using Game.Data;
using Game.Domain;
using Game.GameFlow;
using ProjectBase;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    internal sealed class SkillTreePanelRuntime
    {
        private const string NodePrefabResourcePath = "UI/SkillTreeNode";
        private const string SlotPrefabResourcePath = "UI/SkillTreeSlot";
        private const string MutationOptionPrefabResourcePath = "UI/SkillTreeMutationOption";
        private const float GraphNodeDiameter = 92f;
        private const float EdgeThickness = 6f;
        private const float GraphMinZoomScale = 0.75f;
        private const float GraphMaxZoomScale = 2.4f;
        private const float GraphZoomStep = 0.15f;

        private sealed class MutationOptionData
        {
            public string skillId;
            public string label;
            public string description;
            public int mutationTalentCost;
            public bool isCurrent;
            public bool isSelected;
        }

        private readonly SkillTreePanel _panel;
        private readonly Dictionary<string, SkillTreeNodeView> _nodeViews = new Dictionary<string, SkillTreeNodeView>(StringComparer.Ordinal);
        private readonly List<SkillTreeSlotView> _slotViews = new List<SkillTreeSlotView>(PlayerModel.SkillSlotCount);
        private readonly List<Graphic> _edgeGraphics = new List<Graphic>();

        private RectTransform _windowRoot;
        private RectTransform _graphViewport;
        private RectTransform _graphContent;
        private RectTransform _edgesRoot;
        private RectTransform _nodesRoot;
        private RectTransform _slotsContent;
        private RectTransform _dragGhostRoot;
        private ScrollRect _graphScrollRect;
        private ScrollRect _slotScrollRect;
        private SkillTreeGraphZoomHandler _graphZoomHandler;
        private Text _talentPointsText;
        private Text _statusText;
        private Text _skillTypeText;
        private Text _skillCostText;
        private Text _skillDescText;
        private Button _unlockButton;
        private Text _unlockButtonText;
        private Button _mutateButton;
        private Text _mutateButtonText;
        private RectTransform _mutationPanelRoot;
        private ScrollRect _mutationOptionsScrollRect;
        private RectTransform _mutationOptionsRoot;
        private Text _mutationTitleText;
        private Text _mutationCurrentText;
        private Text _mutationHintText;
        private Button _confirmMutationButton;
        private Text _confirmMutationButtonText;
        private Image _selectedSkillIcon;
        private Image _dragGhostIcon;
        private SkillConfigDatabaseSO _skillConfig;
        private SkillTreeGraphConfigSO _graphConfig;
        private SkillTreeNodeView _selectedNodeView;
        private SkillTreeNodeView _draggingNodeView;
        private string _selectedNodeId = string.Empty;
        private string _pendingMutationActionId = string.Empty;
        private string _pendingMutationSkillId = string.Empty;
        private GameObject _nodePrefab;
        private GameObject _slotPrefab;
        private GameObject _mutationOptionPrefab;

        public SkillTreePanelRuntime(SkillTreePanel panel)
        {
            _panel = panel;
        }

        public void Initialize()
        {
            EnsureConfigsLoaded();
            EnsureRuntimeUi();
        }

        public void Show()
        {
            _panel.gameObject.SetActive(true);
            EnsureConfigsLoaded();
            EnsureRuntimeUi();
            HideMutationPanel();
            RebuildGraph();
            RebuildSlots();
            SelectFirstNodeIfNeeded();
            ResetGraphView();
            if (_slotScrollRect != null)
                _slotScrollRect.horizontalNormalizedPosition = 0f;
            RefreshAll();
            SetStatus("左侧选择技能或基础动作节点；右侧可解锁技能并切换变异，已解锁主动技能可拖拽到下方技能槽位。", false);
        }

        public void Hide()
        {
            CancelDragVisual();
            HideMutationPanel();
            _panel.gameObject.SetActive(false);
        }

        public void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ILevelUIModel model = LevelUIModelLocator.Get();
            if (model != null)
                model.SubscribeInventoryChanged(OnInventoryChanged);
            else
                EventCenter.GetInstance().AddEventListener(GameEvents.InventoryChanged, OnInventoryChanged);
        }

        public void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            ILevelUIModel model = LevelUIModelLocator.Get();
            if (model != null)
                model.UnsubscribeInventoryChanged(OnInventoryChanged);
            else
            {
                var ec = EventCenter.GetInstance();
                if (ec != null) ec.RemoveEventListener(GameEvents.InventoryChanged, OnInventoryChanged);
            }

            CancelDragVisual();
        }

        public void HandleNodeSelected(SkillTreeNodeView nodeView)
        {
            if (nodeView == null)
                return;

            _selectedNodeView = nodeView;
            _selectedNodeId = nodeView.NodeId ?? string.Empty;
            HideMutationPanel();
            RefreshAll();
        }

        public void HandleNodeBeginDrag(SkillTreeNodeView nodeView, PointerEventData eventData)
        {
            if (nodeView?.Entry == null || eventData == null || eventData.button != PointerEventData.InputButton.Left)
                return;

            _selectedNodeView = nodeView;
            _selectedNodeId = nodeView.NodeId ?? string.Empty;

            PlayerModel player = ResolvePlayerModel();
            SkillConfigEntry entry = nodeView.Entry;
            if (player == null || !entry.IsActiveSkill || !player.HasUnlockedEntry(entry))
            {
                RefreshAll();
                return;
            }

            _draggingNodeView = nodeView;
            if (_dragGhostIcon != null)
            {
                _dragGhostIcon.sprite = entry.skillIcon;
                _dragGhostIcon.enabled = entry.skillIcon != null;
            }

            if (_dragGhostRoot != null)
            {
                _dragGhostRoot.gameObject.SetActive(true);
                _dragGhostRoot.SetAsLastSibling();
                UpdateDragGhostPosition(eventData);
            }

            RefreshAll();
        }

        public void HandleNodeDrag(SkillTreeNodeView nodeView, PointerEventData eventData)
        {
            if (_draggingNodeView == null || eventData == null)
                return;

            UpdateDragGhostPosition(eventData);
        }

        public void HandleNodeEndDrag(SkillTreeNodeView nodeView, PointerEventData eventData)
        {
            CancelDragVisual();
        }

        public void HandleSlotDrop(SkillTreeSlotView slotView, PointerEventData eventData)
        {
            if (_draggingNodeView?.Entry == null || slotView == null)
                return;

            PlayerModel player = ResolvePlayerModel();
            if (player == null || _skillConfig == null)
            {
                SetStatus("当前没有可用的玩家或技能配置数据。", true);
                return;
            }

            SkillConfigEntry entry = _draggingNodeView.Entry;
            string actionId = entry.GetResolvedActionId();
            if (!player.AssignSkillActionToSlot(slotView.SlotIndex, actionId, _skillConfig))
            {
                SetStatus($"无法将 {GetDisplayName(entry)} 装备到槽位 {slotView.SlotIndex + 1}。", true);
                return;
            }

            PersistPlayerChanges(false);
            RefreshAll();
            SetStatus($"已将 {GetDisplayName(entry)} 装备到槽位 {slotView.SlotIndex + 1}。", false);
        }

        public void HandleSlotPointerClick(SkillTreeSlotView slotView, PointerEventData eventData)
        {
            if (slotView == null || eventData == null || eventData.button != PointerEventData.InputButton.Right)
                return;

            PlayerModel player = ResolvePlayerModel();
            if (player == null)
            {
                SetStatus("当前没有可用的玩家数据。", true);
                return;
            }

            if (!player.ClearSkillSlot(slotView.SlotIndex))
            {
                SetStatus($"槽位 {slotView.SlotIndex + 1} 当前没有已装备技能。", true);
                return;
            }

            PersistPlayerChanges(false);
            RefreshAll();
            SetStatus($"已清空槽位 {slotView.SlotIndex + 1}。", false);
        }

        private void EnsureConfigsLoaded()
        {
            ConfigManager cfg = ConfigManager.GetInstance();
            _skillConfig ??= cfg?.GetSkillConfigDatabase();
            _graphConfig ??= cfg?.GetSkillTreeGraphConfig();
            _nodePrefab ??= Resources.Load<GameObject>(NodePrefabResourcePath);
            _slotPrefab ??= Resources.Load<GameObject>(SlotPrefabResourcePath);
            _mutationOptionPrefab ??= Resources.Load<GameObject>(MutationOptionPrefabResourcePath);
        }

        private void PersistPlayerChanges(bool inventoryChanged)
        {
            GameStateMachine.GetInstance()?.SaveCurrent(false);
            if (inventoryChanged)
                EventCenter.GetInstance().EventTrigger(GameEvents.InventoryChanged);
        }

        private void OnInventoryChanged()
        {
            if (!_panel.isActiveAndEnabled)
                return;

            EnsureConfigsLoaded();
            RefreshAll();
        }

        private void EnsureRuntimeUi()
        {
            if (_windowRoot != null)
                return;

            _windowRoot = FindRect("Window");
            _talentPointsText = FindComponent<Text>("Window/Txt_TalentPoints");
            _statusText = FindComponent<Text>("Window/Txt_Status");
            _graphScrollRect = FindComponent<ScrollRect>("Window/SkillLibrarySection/SkillCardsRoot/GraphSection/GraphScroll");
            _graphViewport = _graphScrollRect != null ? _graphScrollRect.viewport : FindRect("Window/SkillLibrarySection/SkillCardsRoot/GraphSection/GraphScroll/Viewport");
            _graphContent = _graphScrollRect != null ? _graphScrollRect.content : FindRect("Window/SkillLibrarySection/SkillCardsRoot/GraphSection/GraphScroll/Viewport/Content");
            _edgesRoot = FindRect("Window/SkillLibrarySection/SkillCardsRoot/GraphSection/GraphScroll/Viewport/Content/EdgesRoot");
            _nodesRoot = FindRect("Window/SkillLibrarySection/SkillCardsRoot/GraphSection/GraphScroll/Viewport/Content/NodesRoot");

            _skillTypeText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/Txt_SkillType");
            _skillCostText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/Txt_SkillCost");
            _skillDescText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/Txt_SkillDesc");
            _selectedSkillIcon = FindComponent<Image>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/SelectedSkillIconRoot/SelectedSkillCircle/SelectedSkillIcon");
            _unlockButton = FindComponent<Button>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/Btn_Unlock");
            _unlockButtonText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/Btn_Unlock/Text");
            _mutateButton = FindComponent<Button>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/Btn_Mutate");
            _mutateButtonText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/Btn_Mutate/Text");
            _mutationPanelRoot = FindRect("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/MutationPanelRoot");
            _mutationOptionsScrollRect = FindComponent<ScrollRect>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/MutationPanelRoot/MutationOptionsScroll");
            _mutationOptionsRoot = FindRect("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/MutationPanelRoot/MutationOptionsScroll/Viewport/Content");
            _mutationTitleText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/MutationPanelRoot/Txt_MutationTitle");
            _mutationCurrentText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/MutationPanelRoot/Txt_MutationCurrent");
            _mutationHintText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/MutationPanelRoot/Txt_MutationHint");
            _confirmMutationButton = FindComponent<Button>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/MutationPanelRoot/Btn_ConfirmMutation");
            _confirmMutationButtonText = FindComponent<Text>("Window/SkillLibrarySection/SkillCardsRoot/InfoSection/MutationPanelRoot/Btn_ConfirmMutation/Text");

            _slotScrollRect = FindComponent<ScrollRect>("Window/SkillSlotsSection/SlotScroll");
            _slotsContent = _slotScrollRect != null ? _slotScrollRect.content : FindRect("Window/SkillSlotsSection/SlotScroll/Viewport/SkillSlotsRoot");

            _dragGhostRoot = FindRect("DragGhost");
            _dragGhostIcon = FindComponent<Image>("DragGhost/GhostCircle/GhostIcon");
            ConfigureGraphZoomHandler();

            Button closeButton = FindComponent<Button>("Window/Btn_Close");
            if (closeButton != null)
                closeButton.onClick.AddListener(() => UIManager.GetInstance()?.HidePanel(PanelNames.SkillTree));

            if (_unlockButton != null)
                _unlockButton.onClick.AddListener(TryUnlockSelectedNode);
            if (_mutateButton != null)
                _mutateButton.onClick.AddListener(ToggleMutationPanel);
            if (_confirmMutationButton != null)
                _confirmMutationButton.onClick.AddListener(ConfirmSelectedMutation);

            if (_dragGhostIcon != null)
                _dragGhostIcon.enabled = false;
            if (_dragGhostRoot != null)
                _dragGhostRoot.gameObject.SetActive(false);
            HideMutationPanel();
        }

        private void ConfigureGraphZoomHandler()
        {
            if (_graphScrollRect == null || _graphContent == null)
                return;

            _graphScrollRect.scrollSensitivity = 0f;
            _graphZoomHandler = _graphScrollRect.GetComponent<SkillTreeGraphZoomHandler>();
            if (_graphZoomHandler == null)
                _graphZoomHandler = _graphScrollRect.gameObject.AddComponent<SkillTreeGraphZoomHandler>();

            _graphZoomHandler.Initialize(_graphViewport, _graphContent, _graphScrollRect, GraphMinZoomScale, GraphMaxZoomScale, GraphZoomStep);
        }

        private void ResetGraphView()
        {
            _graphZoomHandler?.ResetZoom();
            if (_graphScrollRect != null)
                _graphScrollRect.normalizedPosition = new Vector2(0f, 1f);
        }

        private void RefreshAll()
        {
            PlayerModel player = ResolvePlayerModel();
            if (_talentPointsText != null)
                _talentPointsText.text = player != null ? $"天赋点：{player.TalentPoints}" : "天赋点：-";

            RefreshNodeVisuals(player);
            RefreshEdges(player);
            RefreshInfoPanel(player);
            RefreshSlots(player);
        }

        private void RefreshNodeVisuals(PlayerModel player)
        {
            foreach (KeyValuePair<string, SkillTreeNodeView> pair in _nodeViews)
            {
                SkillTreeNodeView view = pair.Value;
                SkillConfigEntry entry = view.Entry;
                if (entry == null)
                    continue;

                bool unlocked = IsEntryUnlockedInTree(entry, player);
                bool canUnlock = !unlocked && player != null && CanUnlockEntry(entry, player);
                bool selected = ReferenceEquals(view, _selectedNodeView);
                view.RefreshVisual(selected, unlocked, canUnlock);
            }
        }

        private void RefreshInfoPanel(PlayerModel player)
        {
            SkillConfigEntry entry = _selectedNodeView != null ? _selectedNodeView.Entry : null;
            if (entry == null)
            {
                if (_skillTypeText != null) _skillTypeText.text = "-";
                if (_skillCostText != null) _skillCostText.text = "";
                if (_skillDescText != null) _skillDescText.text = "请在左侧选择一个技能节点。";
                if (_selectedSkillIcon != null) _selectedSkillIcon.enabled = false;
                if (_unlockButton != null) _unlockButton.interactable = false;
                if (_unlockButtonText != null) _unlockButtonText.text = "解锁";
                if (_mutateButton != null) _mutateButton.interactable = false;
                if (_mutateButtonText != null) _mutateButtonText.text = "变异";
                HideMutationPanel();
                return;
            }

            bool unlocked = IsEntryUnlockedInTree(entry, player);
            bool canUnlock = !unlocked && player != null && CanUnlockEntry(entry, player);

            if (_skillTypeText != null)
                _skillTypeText.text = entry.IsBaseSkill
                    ? "基础动作"
                    : entry.IsActiveSkill ? "主动技能" : "被动技能";
            if (_skillCostText != null)
            {
                _skillCostText.text = entry.IsBaseSkill
                    ? "基础动作  |  无需解锁"
                    : entry.IsActiveSkill
                    ? $"解锁天赋点 {Mathf.Max(0, entry.talentCost)}  |  MP {entry.mpCost}  |  冷却 {entry.cooldownSeconds:0.#} 秒"
                    : $"解锁天赋点 {Mathf.Max(0, entry.talentCost)}  |  被动常驻生效";
            }
            if (_skillDescText != null)
                _skillDescText.text = GetDescription(entry, player);
            if (_selectedSkillIcon != null)
            {
                _selectedSkillIcon.sprite = entry.skillIcon;
                _selectedSkillIcon.enabled = entry.skillIcon != null;
                _selectedSkillIcon.preserveAspect = true;
            }
            if (_unlockButton != null)
                _unlockButton.interactable = entry.IsBaseSkill ? false : !unlocked && canUnlock;
            if (_unlockButtonText != null)
                _unlockButtonText.text = entry.IsBaseSkill ? "基础动作" : unlocked ? "已解锁" : canUnlock ? "解锁" : "未满足条件";
            RefreshMutationPanel(entry, player, unlocked);
        }

        private void RebuildGraph()
        {
            string selectedNodeId = _selectedNodeId;
            _nodeViews.Clear();
            ClearChildren(_nodesRoot);
            ClearChildren(_edgesRoot);
            _edgeGraphics.Clear();
            _selectedNodeView = null;

            if (_graphContent != null && _graphConfig != null)
                _graphContent.sizeDelta = _graphConfig.contentSize;

            if (_graphConfig?.nodes == null || _skillConfig == null)
                return;

            for (int i = 0; i < _graphConfig.nodes.Count; i++)
            {
                SkillTreeNodeDefinition node = _graphConfig.nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.actionId))
                    continue;

                SkillConfigEntry entry = ResolveGraphEntry(node.actionId.Trim());
                if (entry == null)
                    continue;

                SkillTreeNodeView view = CreateNodeView(_nodesRoot);
                if (view == null)
                    continue;

                RectTransform rect = view.RectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(GraphNodeDiameter, GraphNodeDiameter);
                rect.anchoredPosition = new Vector2(node.position.x, -node.position.y);
                view.Bind(_panel, node.nodeId, entry);
                _nodeViews[node.nodeId] = view;
            }

            if (!string.IsNullOrWhiteSpace(selectedNodeId) && _nodeViews.TryGetValue(selectedNodeId, out SkillTreeNodeView selectedView))
            {
                _selectedNodeView = selectedView;
                _selectedNodeId = selectedNodeId;
                return;
            }

            _selectedNodeId = string.Empty;
        }

        private void RebuildSlots()
        {
            _slotViews.Clear();
            ClearChildren(_slotsContent);
            if (_slotsContent == null)
                return;

            HorizontalLayoutGroup layout = _slotsContent.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = _slotsContent.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(8, 8, 6, 6);
                layout.spacing = 16f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlHeight = false;
                layout.childControlWidth = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            ContentSizeFitter fitter = _slotsContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = _slotsContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            for (int i = 0; i < PlayerModel.SkillSlotCount; i++)
            {
                SkillTreeSlotView slotView = CreateSlotView(_slotsContent);
                if (slotView == null)
                    continue;

                slotView.Bind(_panel, i);
                _slotViews.Add(slotView);
            }
        }

        private void SelectFirstNodeIfNeeded()
        {
            if (_selectedNodeView != null)
                return;

            foreach (KeyValuePair<string, SkillTreeNodeView> pair in _nodeViews)
            {
                _selectedNodeView = pair.Value;
                _selectedNodeId = pair.Key;
                return;
            }

            _selectedNodeId = string.Empty;
        }

        private void RefreshEdges(PlayerModel player)
        {
            ClearChildren(_edgesRoot);
            _edgeGraphics.Clear();

            if (_graphConfig?.nodes == null)
                return;

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

                    if (!_nodeViews.TryGetValue(node.nodeId, out SkillTreeNodeView currentView))
                        continue;
                    if (!_nodeViews.TryGetValue(predecessorNodeId.Trim(), out SkillTreeNodeView predecessorView))
                        continue;

                    bool predecessorUnlocked = IsEntryUnlockedInTree(predecessorView.Entry, player);
                    Color color = predecessorUnlocked
                        ? new Color(0.30f, 0.83f, 0.70f, 0.92f)
                        : new Color(0.34f, 0.38f, 0.39f, 0.90f);

                    List<Vector2> points = BuildEdgePoints(node, predecessorNodeId.Trim(), predecessorView.RectTransform.anchoredPosition, currentView.RectTransform.anchoredPosition);
                    for (int k = 0; k < points.Count - 1; k++)
                        CreateEdgeSegment(points[k], points[k + 1], color);
                }
            }
        }

        private List<Vector2> BuildEdgePoints(SkillTreeNodeDefinition node, string predecessorNodeId, Vector2 predecessorAnchoredPosition, Vector2 currentAnchoredPosition)
        {
            Vector2 start = predecessorAnchoredPosition + new Vector2(GraphNodeDiameter * 0.5f, 0f);
            Vector2 end = currentAnchoredPosition + new Vector2(-GraphNodeDiameter * 0.5f, 0f);

            List<Vector2> points = new List<Vector2>(4) { start };
            if (node.TryGetRouteOverride(predecessorNodeId, out SkillTreeEdgeRouteOverride routeOverride) && routeOverride.waypoints != null && routeOverride.waypoints.Count > 0)
            {
                for (int i = 0; i < routeOverride.waypoints.Count; i++)
                    points.Add(new Vector2(routeOverride.waypoints[i].x, -routeOverride.waypoints[i].y));
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

        private void CreateEdgeSegment(Vector2 start, Vector2 end, Color color)
        {
            RectTransform segment = CreateRect(_edgesRoot, "EdgeSegment");
            segment.anchorMin = new Vector2(0f, 1f);
            segment.anchorMax = new Vector2(0f, 1f);
            segment.pivot = new Vector2(0.5f, 0.5f);
            segment.gameObject.AddComponent<CanvasRenderer>();
            Image image = segment.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            Vector2 delta = end - start;
            float length = Mathf.Max(1f, delta.magnitude);
            segment.sizeDelta = new Vector2(length, EdgeThickness);
            segment.anchoredPosition = (start + end) * 0.5f;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            segment.localRotation = Quaternion.Euler(0f, 0f, angle);
            _edgeGraphics.Add(image);
        }

        private void RefreshSlots(PlayerModel player)
        {
            if (_skillConfig == null)
                return;

            for (int i = 0; i < _slotViews.Count; i++)
            {
                SkillTreeSlotView slotView = _slotViews[i];
                string actionId = player != null ? player.GetEquippedSkillActionId(slotView.SlotIndex) : string.Empty;
                SkillConfigEntry entry = !string.IsNullOrWhiteSpace(actionId) ? _skillConfig.GetEntryByActionId(actionId) : null;
                bool unlocked = entry != null && player != null && player.HasUnlockedEntry(entry);
                bool available = entry != null && player != null && player.IsSkillAvailable(entry);
                slotView.RefreshVisual(entry, unlocked, available);
            }
        }

        private void TryUnlockSelectedNode()
        {
            SkillConfigEntry entry = _selectedNodeView != null ? _selectedNodeView.Entry : null;
            PlayerModel player = ResolvePlayerModel();
            if (entry == null || player == null || _skillConfig == null)
            {
                SetStatus("当前没有可用的技能或玩家数据。", true);
                return;
            }

            if (entry.IsBaseSkill)
            {
                SetStatus($"{GetDisplayName(entry)} 为基础动作，无需解锁。", false);
                return;
            }

            if (IsEntryUnlockedInTree(entry, player))
            {
                SetStatus($"{GetDisplayName(entry)} 已解锁。", false);
                return;
            }

            if (!CanUnlockEntry(entry, player))
            {
                SetStatus($"{GetDisplayName(entry)} 还没有满足前驱解锁条件。", true);
                return;
            }

            int talentCost = Mathf.Max(0, entry.talentCost);
            if (talentCost > 0 && !player.TryConsumeItem(PlayerModel.ItemIds.TalentPoint, talentCost))
            {
                SetStatus($"解锁天赋点不足，解锁 {GetDisplayName(entry)} 需要 {talentCost} 点。", true);
                return;
            }

            if (!player.UnlockAction(entry.GetResolvedActionId(), _skillConfig))
            {
                if (talentCost > 0)
                    player.AddItemCount(PlayerModel.ItemIds.TalentPoint, talentCost);

                SetStatus($"解锁 {GetDisplayName(entry)} 失败。", true);
                return;
            }

            PersistPlayerChanges(true);
            RefreshAll();
            SetStatus($"已解锁 {GetDisplayName(entry)}。", false);
        }

        private bool CanUnlockEntry(SkillConfigEntry entry, PlayerModel player)
        {
            if (entry == null || player == null)
                return false;

            if (entry.IsBaseSkill)
                return false;

            if (_graphConfig == null)
                return true;

            return _graphConfig.CanUnlock(entry.GetResolvedActionId(), player.HasUnlockedAction);
        }

        private void ToggleMutationPanel()
        {
            if (_mutationPanelRoot == null)
                return;

            if (_mutationPanelRoot.gameObject.activeSelf)
            {
                HideMutationPanel();
                return;
            }

            SkillConfigEntry entry = _selectedNodeView != null ? _selectedNodeView.Entry : null;
            PlayerModel player = ResolvePlayerModel();
            if (!CanMutateEntry(entry, player))
            {
                SetStatus("当前技能暂时没有可用的变异方向。", true);
                return;
            }

            RebuildMutationPanel(entry, player);
            _mutationPanelRoot.gameObject.SetActive(true);
        }

        private void RefreshMutationPanel(SkillConfigEntry entry, PlayerModel player, bool unlocked)
        {
            if (_mutateButton != null)
                _mutateButton.interactable = entry != null
                                             && player != null
                                             && (entry.IsBaseSkill || unlocked)
                                             && CanMutateEntry(entry, player);

            if (_mutateButtonText != null)
                _mutateButtonText.text = "变异";

            if (_mutationPanelRoot == null || !_mutationPanelRoot.gameObject.activeSelf)
                return;

            if (!CanMutateEntry(entry, player))
            {
                HideMutationPanel();
                return;
            }

            RebuildMutationPanel(entry, player);
        }

        private void HideMutationPanel()
        {
            if (_mutationPanelRoot != null)
                _mutationPanelRoot.gameObject.SetActive(false);

            ClearPendingMutationSelection();
        }

        private bool CanMutateEntry(SkillConfigEntry entry, PlayerModel player)
        {
            if (entry == null || player == null)
                return false;

            if (!entry.IsBaseSkill && !player.HasUnlockedEntry(entry))
                return false;

            List<MutationOptionData> options = CollectMutationOptions(entry, player);
            return options.Count > 0;
        }

        private void RebuildMutationPanel(SkillConfigEntry entry, PlayerModel player)
        {
            if (_mutationPanelRoot == null)
                return;

            EnsureMutationOptionsLayout();
            ClearChildren(_mutationOptionsRoot);

            if (_mutationTitleText != null)
                _mutationTitleText.text = $"{GetDisplayName(entry)} 变异";

            MutationOptionData currentOption = ResolveCurrentMutationOption(entry, player);
            List<MutationOptionData> options = CollectMutationOptions(entry, player);
            string actionId = entry != null ? entry.GetResolvedActionId() : string.Empty;
            if (!string.Equals(_pendingMutationActionId, actionId, StringComparison.Ordinal))
            {
                _pendingMutationActionId = actionId;
                _pendingMutationSkillId = string.Empty;
            }

            MutationOptionData selectedOption = FindMutationOption(options, _pendingMutationSkillId);
            if (selectedOption == null)
                _pendingMutationSkillId = string.Empty;

            selectedOption = FindMutationOption(options, _pendingMutationSkillId);
            for (int i = 0; i < options.Count; i++)
                options[i].isSelected = selectedOption != null && string.Equals(options[i].skillId, selectedOption.skillId, StringComparison.Ordinal);

            if (_mutationCurrentText != null)
                _mutationCurrentText.text = BuildCurrentMutationText(entry, player, currentOption);

            if (_mutationHintText != null)
            {
                _mutationHintText.text = options.Count > 0
                    ? selectedOption != null
                        ? "已选择变异方向，点击下方“确定变异”后才会生效。"
                        : "选择一个变异方向，然后点击下方“确定变异”。"
                    : "当前技能没有可用的变异方向。";
            }

            if (_confirmMutationButton != null)
                _confirmMutationButton.interactable = selectedOption != null;

            if (_confirmMutationButtonText != null)
                _confirmMutationButtonText.text = "确定变异";

            for (int i = 0; i < options.Count; i++)
                CreateMutationOptionButton(entry, options[i]);

            if (_mutationOptionsScrollRect != null)
                _mutationOptionsScrollRect.verticalNormalizedPosition = 1f;
        }

        private List<MutationOptionData> CollectMutationOptions(SkillConfigEntry entry, PlayerModel player)
        {
            List<MutationOptionData> allOptions = CollectAllMutationOptions(entry, player);
            var result = new List<MutationOptionData>(allOptions.Count);
            for (int i = 0; i < allOptions.Count; i++)
            {
                MutationOptionData option = allOptions[i];
                if (option == null || option.isCurrent)
                    continue;

                result.Add(option);
            }

            return result;
        }

        private MutationOptionData ResolveCurrentMutationOption(SkillConfigEntry entry, PlayerModel player)
        {
            List<MutationOptionData> allOptions = CollectAllMutationOptions(entry, player);
            for (int i = 0; i < allOptions.Count; i++)
            {
                MutationOptionData option = allOptions[i];
                if (option != null && option.isCurrent)
                    return option;
            }

            string currentSkillId = player != null ? player.ResolveSkillEffectId(entry) : entry != null ? entry.GetResolvedSkillId() : string.Empty;
            if (string.IsNullOrWhiteSpace(currentSkillId))
                return null;

            return new MutationOptionData
            {
                skillId = currentSkillId.Trim(),
                label = ResolveMutationOptionLabel(entry, currentSkillId.Trim(), string.Empty),
                description = ResolveMutationOptionDescription(entry, entry != null ? entry.GetResolvedSkillId() : string.Empty, currentSkillId.Trim(), string.Empty),
                isCurrent = true,
            };
        }

        private List<MutationOptionData> CollectAllMutationOptions(SkillConfigEntry entry, PlayerModel player)
        {
            var result = new List<MutationOptionData>();
            if (entry == null)
                return result;

            string currentSkillId = player != null ? player.ResolveSkillEffectId(entry) : entry.GetResolvedSkillId();
            string defaultSkillId = entry.GetResolvedSkillId();

            if (entry.IsPassiveSkill)
            {
                PassiveSkillEffectVariantGroupDefinition variantGroup = entry.ResolvePassiveSkillEffectVariantGroup();
                if (variantGroup?.entries != null)
                {
                    for (int i = 0; i < variantGroup.entries.Count; i++)
                    {
                        PassiveSkillEffectDefinition option = variantGroup.entries[i];
                        string skillId = option != null && !string.IsNullOrWhiteSpace(option.skillId) ? option.skillId.Trim() : string.Empty;
                        if (string.IsNullOrWhiteSpace(skillId))
                            continue;

                        result.Add(new MutationOptionData
                        {
                            skillId = skillId,
                            label = ResolveMutationOptionLabel(entry, skillId, option.displayName),
                            description = ResolveMutationOptionDescription(entry, defaultSkillId, skillId, option.effectDescription),
                            mutationTalentCost = Mathf.Max(0, option.mutationTalentCost),
                            isCurrent = string.Equals(currentSkillId, skillId, StringComparison.Ordinal)
                        });
                    }
                }

                return result;
            }

            SkillEffectVariantGroupDefinition sharedVariantGroup = entry.ResolveSkillEffectVariantGroup();
            if (sharedVariantGroup?.entries != null)
            {
                for (int i = 0; i < sharedVariantGroup.entries.Count; i++)
                {
                    SharedSkillDefinition option = sharedVariantGroup.entries[i];
                    string skillId = option != null && !string.IsNullOrWhiteSpace(option.skillId) ? option.skillId.Trim() : string.Empty;
                    if (string.IsNullOrWhiteSpace(skillId))
                        continue;

                    string label = ResolveMutationOptionLabel(entry, skillId, option.displayName);
                    result.Add(new MutationOptionData
                    {
                        skillId = skillId,
                        label = label,
                        description = ResolveMutationOptionDescription(entry, defaultSkillId, skillId, option.effectDescription),
                        mutationTalentCost = Mathf.Max(0, option.mutationTalentCost),
                        isCurrent = string.Equals(currentSkillId, skillId, StringComparison.Ordinal)
                    });
                }
            }

            return result;
        }

        private string ResolveMutationOptionDescription(SkillConfigEntry entry, string defaultSkillId, string skillId, string explicitDescription)
        {
            if (!string.IsNullOrWhiteSpace(explicitDescription))
                return explicitDescription.Trim();

            if (!string.IsNullOrWhiteSpace(entry?.description)
                && string.Equals(defaultSkillId, skillId, StringComparison.Ordinal))
            {
                return entry.description.Trim();
            }

            return string.Empty;
        }

        private string ResolveMutationOptionLabel(SkillConfigEntry entry, string skillId, string explicitDisplayName)
        {
            if (!string.IsNullOrWhiteSpace(explicitDisplayName))
                return explicitDisplayName.Trim();

            if (entry != null && string.Equals(entry.GetResolvedSkillId(), skillId, StringComparison.Ordinal))
            {
                string entryDisplayName = GetDisplayName(entry);
                if (!string.IsNullOrWhiteSpace(entryDisplayName))
                    return entryDisplayName;
            }

            return string.IsNullOrWhiteSpace(skillId) ? string.Empty : skillId.Trim();
        }

        private void EnsureMutationOptionsLayout()
        {
            if (_mutationOptionsRoot == null)
                return;

            VerticalLayoutGroup layout = _mutationOptionsRoot.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = _mutationOptionsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlHeight = false;
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            ContentSizeFitter fitter = _mutationOptionsRoot.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = _mutationOptionsRoot.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private void CreateMutationOptionButton(SkillConfigEntry entry, MutationOptionData option)
        {
            if (_mutationOptionsRoot == null || option == null)
                return;

            GameObject buttonObject = CreateMutationOptionObject();
            if (buttonObject == null)
                return;

            buttonObject.name = $"Mutation_{option.skillId}";
            buttonObject.transform.SetParent(_mutationOptionsRoot, false);

            Image background = buttonObject.GetComponent<Image>();
            if (background != null)
            {
                background.color = option.isSelected
                    ? new Color(0.24f, 0.42f, 0.30f, 0.98f)
                    : new Color(0.20f, 0.24f, 0.28f, 0.96f);
            }

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            if (layout != null)
                layout.preferredHeight = 88f;

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
            string selectedSkillId = option.skillId;
            button.onClick.AddListener(() => SelectMutationOption(entry, selectedSkillId));

            Text text = buttonObject.transform.Find("Text")?.GetComponent<Text>();
            if (text == null)
                text = buttonObject.GetComponentInChildren<Text>(true);

            if (text == null)
                return;

            text.font = ResolveMutationOptionFont();
            text.fontSize = 16;
            text.lineSpacing = 1.1f;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = option.isSelected
                ? new Color(0.98f, 0.96f, 0.88f, 1f)
                : new Color(0.92f, 0.94f, 0.96f, 1f);
            text.text = BuildMutationOptionButtonText(option);
        }

        private GameObject CreateMutationOptionObject()
        {
            if (_mutationOptionPrefab != null)
                return UnityEngine.Object.Instantiate(_mutationOptionPrefab);

            GameObject buttonObject = new GameObject("SkillTreeMutationOption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 88f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);
            return buttonObject;
        }

        private Font ResolveMutationOptionFont()
        {
            if (_mutationHintText != null && _mutationHintText.font != null)
                return _mutationHintText.font;

            if (_skillDescText != null && _skillDescText.font != null)
                return _skillDescText.font;

            if (_skillTypeText != null && _skillTypeText.font != null)
                return _skillTypeText.font;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void SelectMutationOption(SkillConfigEntry entry, string skillId)
        {
            if (entry == null || string.IsNullOrWhiteSpace(skillId))
                return;

            _pendingMutationActionId = entry.GetResolvedActionId();
            _pendingMutationSkillId = skillId.Trim();
            RebuildMutationPanel(entry, ResolvePlayerModel());
        }

        private void ConfirmSelectedMutation()
        {
            SkillConfigEntry entry = _selectedNodeView != null ? _selectedNodeView.Entry : null;
            PlayerModel player = ResolvePlayerModel();
            if (entry == null || player == null || _skillConfig == null)
            {
                SetStatus("当前无法切换技能变异。", true);
                return;
            }

            List<MutationOptionData> options = CollectMutationOptions(entry, player);
            MutationOptionData selectedOption = FindMutationOption(options, _pendingMutationSkillId);
            if (selectedOption == null)
            {
                SetStatus("请先选择一个变异方向。", true);
                return;
            }

            string currentSkillId = player.ResolveSkillEffectId(entry);
            if (string.Equals(currentSkillId, selectedOption.skillId, StringComparison.Ordinal))
            {
                SetStatus($"{GetDisplayName(entry)} 当前已是该变异方向。", false);
                HideMutationPanel();
                return;
            }

            int mutationTalentCost = Mathf.Max(0, selectedOption.mutationTalentCost);
            if (mutationTalentCost > 0 && !player.TryConsumeItem(PlayerModel.ItemIds.TalentPoint, mutationTalentCost))
            {
                SetStatus($"变异天赋点不足，切换 {GetDisplayName(entry)} 需要 {mutationTalentCost} 点。", true);
                return;
            }

            if (!player.TrySetSelectedSkillVariant(entry.GetResolvedActionId(), selectedOption.skillId, _skillConfig))
            {
                if (mutationTalentCost > 0)
                    player.AddItemCount(PlayerModel.ItemIds.TalentPoint, mutationTalentCost);

                SetStatus($"{GetDisplayName(entry)} 的变异切换失败。", true);
                return;
            }

            PersistPlayerChanges(true);
            HideMutationPanel();
            RefreshAll();
            SetStatus($"已将 {GetDisplayName(entry)} 变异为 {selectedOption.label}。", false);
        }

        private static MutationOptionData FindMutationOption(List<MutationOptionData> options, string skillId)
        {
            if (options == null || string.IsNullOrWhiteSpace(skillId))
                return null;

            string normalizedSkillId = skillId.Trim();
            for (int i = 0; i < options.Count; i++)
            {
                MutationOptionData option = options[i];
                if (option != null && string.Equals(option.skillId, normalizedSkillId, StringComparison.Ordinal))
                    return option;
            }

            return null;
        }

        private string BuildCurrentMutationText(SkillConfigEntry entry, PlayerModel player, MutationOptionData currentOption)
        {
            string currentSkillId = player != null ? player.ResolveSkillEffectId(entry) : entry != null ? entry.GetResolvedSkillId() : string.Empty;
            string label = currentOption != null && !string.IsNullOrWhiteSpace(currentOption.label)
                ? currentOption.label
                : (string.IsNullOrWhiteSpace(currentSkillId) ? "-" : currentSkillId.Trim());

            if (currentOption == null || string.IsNullOrWhiteSpace(currentOption.description))
                return $"当前方向：{label}";

            return $"当前方向：{label}\n{currentOption.description}";
        }

        private static string BuildMutationOptionButtonText(MutationOptionData option)
        {
            if (option == null)
                return string.Empty;

            string costText = $"变异天赋点消耗：{Mathf.Max(0, option.mutationTalentCost)}";
            if (string.IsNullOrWhiteSpace(option.description))
                return $"{option.label}\n{costText}";

            return $"{option.label}\n{option.description}\n{costText}";
        }

        private void ClearPendingMutationSelection()
        {
            _pendingMutationActionId = string.Empty;
            _pendingMutationSkillId = string.Empty;
        }

        private static bool IsEntryUnlockedInTree(SkillConfigEntry entry, PlayerModel player)
        {
            if (entry == null)
                return false;

            return entry.IsBaseSkill || (player != null && player.HasUnlockedEntry(entry));
        }

        private void UpdateDragGhostPosition(PointerEventData eventData)
        {
            if (_dragGhostRoot == null || eventData == null)
                return;

            RectTransform canvasRect = _panel.transform as RectTransform;
            if (canvasRect == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                _dragGhostRoot.anchoredPosition = localPoint;
        }

        private void CancelDragVisual()
        {
            _draggingNodeView = null;
            if (_dragGhostRoot != null)
                _dragGhostRoot.gameObject.SetActive(false);
        }

        private PlayerModel ResolvePlayerModel()
        {
            ILevelUIModel levelUiModel = LevelUIModelLocator.Get();
            return levelUiModel?.Player ?? GameStateMachine.GetInstance()?.Player;
        }

        private SkillConfigEntry ResolveGraphEntry(string actionId)
        {
            return _skillConfig != null ? _skillConfig.GetEntryByActionId(actionId) : null;
        }

        private void SetStatus(string message, bool isError)
        {
            if (_statusText == null)
                return;

            _statusText.text = message ?? string.Empty;
            _statusText.color = isError
                ? new Color(0.98f, 0.56f, 0.50f, 1f)
                : new Color(0.98f, 0.90f, 0.58f, 1f);
        }

        private SkillTreeNodeView CreateNodeView(Transform parent)
        {
            if (_nodePrefab == null)
            {
                Debug.LogError("SkillTreeNode prefab 丢失，无法构建技能树节点。");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(_nodePrefab, parent, false);
            SkillTreeNodeView prefabView = instance.GetComponent<SkillTreeNodeView>();
            if (prefabView == null)
            {
                Debug.LogError("SkillTreeNode prefab 缺少 SkillTreeNodeView 组件，无法构建技能树节点。");
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            prefabView.EnsureReferencesBound();
            return prefabView;
        }

        private SkillTreeSlotView CreateSlotView(Transform parent)
        {
            if (_slotPrefab == null)
            {
                Debug.LogError("SkillTreeSlot prefab 丢失，无法构建技能树槽位。");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(_slotPrefab, parent, false);
            SkillTreeSlotView prefabView = instance.GetComponent<SkillTreeSlotView>();
            if (prefabView == null)
            {
                Debug.LogError("SkillTreeSlot prefab 缺少 SkillTreeSlotView 组件，无法构建技能树槽位。");
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            prefabView.EnsureReferencesBound();
            return prefabView;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                    continue;

                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static string GetDisplayName(SkillConfigEntry entry)
        {
            if (entry == null)
                return "技能";

            if (!string.IsNullOrWhiteSpace(entry.displayName))
                return entry.displayName.Trim();

            string actionId = entry.GetResolvedActionId();
            return string.IsNullOrWhiteSpace(actionId) ? "技能" : actionId;
        }

        private static string GetDescription(SkillConfigEntry entry, PlayerModel player)
        {
            if (entry == null)
                return string.Empty;

            string baseDescription = !string.IsNullOrWhiteSpace(entry.description)
                ? entry.description.Trim()
                : GetDefaultEntryDescription(entry);
            string effectDescription = player != null ? player.ResolveSkillEffectDescription(entry) : string.Empty;
            if (string.IsNullOrWhiteSpace(effectDescription) || string.Equals(effectDescription, baseDescription, StringComparison.Ordinal))
                return baseDescription;

            if (string.IsNullOrWhiteSpace(baseDescription))
                return effectDescription;

            return $"{baseDescription}\n\n当前效果：{effectDescription}";
        }

        private static string GetDefaultEntryDescription(SkillConfigEntry entry)
        {
            if (entry == null)
                return string.Empty;

            if (entry.IsBaseSkill)
                return "基础动作，可通过“变异”切换不同技能效果。";

            return entry.IsActiveSkill
                ? "主动技能，解锁后可拖拽到下方技能槽位中释放。"
                : "被动技能，解锁后会立即生效。";
        }

        private RectTransform FindRect(string path)
        {
            return _panel.transform.Find(path) as RectTransform;
        }

        private T FindComponent<T>(string path) where T : Component
        {
            Transform target = _panel.transform.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }
    }
}

namespace Game.UI
{
    internal sealed class SkillTreeGraphZoomHandler : MonoBehaviour, IScrollHandler
    {
        private RectTransform _viewport;
        private RectTransform _content;
        private ScrollRect _scrollRect;
        private float _minScale = 0.75f;
        private float _maxScale = 2.4f;
        private float _zoomStep = 0.15f;
        private float _currentScale = 1f;

        public void Initialize(RectTransform viewport, RectTransform content, ScrollRect scrollRect, float minScale, float maxScale, float zoomStep)
        {
            _viewport = viewport;
            _content = content;
            _scrollRect = scrollRect;
            _minScale = Mathf.Max(0.1f, minScale);
            _maxScale = Mathf.Max(_minScale, maxScale);
            _zoomStep = Mathf.Max(0.01f, zoomStep);

            if (_content != null)
            {
                Vector3 scale = _content.localScale;
                _currentScale = Mathf.Clamp(scale.x, _minScale, _maxScale);
                _content.localScale = new Vector3(_currentScale, _currentScale, 1f);
            }
        }

        public void ResetZoom()
        {
            ApplyZoom(1f, Vector2.zero, null, false);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_content == null || eventData == null)
                return;

            float delta = eventData.scrollDelta.y;
            if (Mathf.Abs(delta) < 0.01f)
                return;

            float targetScale = delta > 0f ? _currentScale + _zoomStep : _currentScale - _zoomStep;
            Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
            ApplyZoom(targetScale, eventData.position, eventCamera, true);
            eventData.Use();
        }

        private void ApplyZoom(float targetScale, Vector2 screenPoint, Camera eventCamera, bool keepPointerFocus)
        {
            if (_content == null)
                return;

            float clampedScale = Mathf.Clamp(targetScale, _minScale, _maxScale);
            if (Mathf.Approximately(clampedScale, _currentScale))
                return;

            if (keepPointerFocus && _viewport != null &&
                RectTransformUtility.ScreenPointToWorldPointInRectangle(_viewport, screenPoint, eventCamera, out Vector3 worldPoint))
            {
                Vector3 localPointBefore = _content.InverseTransformPoint(worldPoint);
                _content.localScale = new Vector3(clampedScale, clampedScale, 1f);
                Vector3 worldPointAfter = _content.TransformPoint(localPointBefore);
                _content.position += worldPoint - worldPointAfter;
            }
            else
            {
                _content.localScale = new Vector3(clampedScale, clampedScale, 1f);
            }

            _currentScale = clampedScale;
            if (_scrollRect != null)
                _scrollRect.StopMovement();
        }
    }
}
