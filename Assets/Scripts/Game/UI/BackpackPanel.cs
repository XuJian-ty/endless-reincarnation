using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Game;
using Game.Domain;
using Game.Data;
using Game.GameFlow;
using Game.Presentation;
using ProjectBase;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI
{
    /// <summary>
    /// 背包面板：左侧为玩家模型区、12 项属性（一行 2 个）、当前装备武器格；右侧为 50 格滚动视图（每行 5 格，鼠标滚轮垂直滚动），支持 6 种排序。
    /// 约定：右侧 Slots 为 ScrollView/Viewport/Content，Content 下 50 个子物体依次为 Slot0..Slot49；每格下有 Image(图标)、Text(数量可选)。
    /// 左侧需有 PlayerModelArea（可选）、StatsRoot（12 个 Text 或子节点）、EquippedWeaponIcon（Image）；排序下拉或按钮命名为 SortDropdown / Btn_Sort*。
    /// </summary>
    [ExecuteAlways]
    public class BackpackPanel : BasePanel
    {
        private static readonly int SlotCount = PlayerModel.SlotCount;

        [Header("右侧背包")]
        [Tooltip("ScrollView 下的 Content（带 GridLayoutGroup，5 列），其下 50 个子物体为 Slot0..Slot49")]
        [SerializeField] private Transform slotsRoot;
        [SerializeField] private ScrollRect scrollRect;

        [Header("左侧")]
        [Tooltip("玩家模型展示区（可选，后续可挂 3D 模型渲染）")]
        [SerializeField] private RectTransform playerModelArea;
        [Tooltip("12 项属性容器，其下 12 个 Text 按顺序：HP/MP/ATK/DEF/生命偷取/暴击率/暴击伤害/攻速/移速/生命回复/法力回复/增伤")]
        [SerializeField] private Transform statsRoot;
        [Tooltip("当前装备武器图标")]
        [SerializeField] private Image equippedWeaponIcon;

        [Header("模型预览")]
        [Tooltip("玩家模型在预览中的旋转（欧拉角）")]
        [SerializeField] private Vector3 previewModelRotation = new Vector3(0f, 180f, 0f);
        [Tooltip("预览镜头对焦点相对模型中心的偏移")]
        [SerializeField] private Vector3 previewFocusOffset = new Vector3(0f, 0.1f, 0f);
        [Tooltip("预览缩放倍率，越大显示越大")]
        [SerializeField] private float previewZoom = 1.5f;
        [Tooltip("预览镜头距离倍率")]
        [SerializeField] private float previewDistanceMultiplier = 3f;
        [Tooltip("预览镜头最小距离")]
        [SerializeField] private float previewMinDistance = 2.2f;
        [Tooltip("鼠标左右拖拽时的模型旋转速度")]
        [SerializeField] private float previewDragRotateSpeed = 0.25f;

        [Header("排序")]
        [SerializeField] private Dropdown sortDropdown;

        [Header("悬停与右键")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private Text tooltipText;
        [SerializeField] private GameObject contextMenuPanel;
        [SerializeField] private Button btnEquip;
        [SerializeField] private Button btnSell;

        private Transform[] _slotTransforms = new Transform[SlotCount];
        private Image[] _slotBackgrounds = new Image[SlotCount];
        private Image[] _slotIcons = new Image[SlotCount];
        private Text[] _slotCountTexts = new Text[SlotCount];
        private GameObject[] _slotCountVisuals = new GameObject[SlotCount];
        private Text[] _statTexts = new Text[12];
        private int _contextSlotIndex = -1;
        private PlayerModel _player;
        private WeaponDatabaseSO _weaponDb;
        private ItemDisplayDatabaseSO _itemDisplayDb;
        private ShopPriceConfigSO _shopPrice;
        private SlotBackgroundConfigSO _slotBgConfig;
        private BackpackUIConfigSO _backpackUIConfig;
        private readonly BackpackPresenter _presenter = new BackpackPresenter();
        private const int PreviewLayer = 31;
        private const string SlotPrefabResourcePath = "UI/BackpackSlot";
        private RawImage _playerModelPreviewImage;
        private RenderTexture _playerModelPreviewTexture;
        private Camera _playerModelPreviewCamera;
        private GameObject _playerModelPreviewRoot;
        private Transform _playerModelPreviewTransform;
        private Animator _playerModelPreviewAnimator;
        private float _playerModelPreviewYaw = float.NaN;
        private readonly PlayerPreviewCombatController _previewCombat = new PlayerPreviewCombatController();
        private bool _previewPointerTracking;
        private bool _previewPointerDragging;
        private Vector2 _previewPointerDownPosition;
        private float _previewPointerDownYaw;
        private readonly Vector3[] _popupWorldCorners = new Vector3[4];
        private Text _tooltipRightText;

        private const float PreviewDragThresholdPixels = 10f;
        private static readonly Vector2 PopupOffset = new Vector2(12f, -12f);
        private const float TooltipPanelWidth = 300f;
        private const float TooltipMinHeight = 48f;
        private const float TooltipPadding = 12f;
        private const float TooltipWeaponLeftRightInset = 150f;
        private const float TooltipWeaponRightLeftInset = 156f;

        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () => UIManager.GetInstance().HidePanel(PanelNames.Backpack));
            RegisterClick("Btn_Equip", OnEquipClicked);
            RegisterClick("Btn_Sell", OnSellClicked);
            ResolveReferences();
            if (sortDropdown != null)
                sortDropdown.onValueChanged.AddListener(OnSortChanged);
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
        }

        public override void HideMe()
        {
            HideTooltip();
            HideContextMenu();
            gameObject.SetActive(false);
        }

        private void ResolveReferences()
        {
            var root = slotsRoot != null ? slotsRoot : transform.Find("ScrollView/Viewport/Content");
            if (root == null) root = transform.Find("Slots");
            if (slotsRoot == null) slotsRoot = root;
            ClearSlotReferences();
            EnsureSlotObjects(root);
            if (root != null && root.childCount >= SlotCount)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    _slotTransforms[i] = root.GetChild(i);
                    _slotBackgrounds[i] = _slotTransforms[i].Find("Background")?.GetComponent<Image>();
                    _slotIcons[i] = _slotTransforms[i].Find("Icon")?.GetComponent<Image>();
                    _slotCountTexts[i] = FindNamedDescendant(_slotTransforms[i], "Count")?.GetComponent<Text>();
                    _slotCountVisuals[i] = ResolveCountVisualRoot(_slotTransforms[i], _slotCountTexts[i]);

                    if (_slotBackgrounds[i] == null || _slotIcons[i] == null)
                    {
                        var images = _slotTransforms[i].GetComponentsInChildren<Image>(true);
                        if (_slotBackgrounds[i] == null)
                            _slotBackgrounds[i] = System.Array.Find(images, img => img != null && img.transform != _slotTransforms[i] && img.name == "Background");
                        if (_slotIcons[i] == null)
                            _slotIcons[i] = System.Array.Find(images, img => img != null && img.transform != _slotTransforms[i] && img.name == "Icon");
                        if (_slotIcons[i] == null && images != null && images.Length > 0)
                            _slotIcons[i] = images[images.Length - 1];
                    }
                    if (_slotCountTexts[i] == null)
                        _slotCountTexts[i] = _slotTransforms[i].GetComponentInChildren<Text>(true);

                    ConfigureIconImage(_slotIcons[i]);
                    ConfigureSlotRaycastTargets(i);
                    AddSlotListeners(_slotTransforms[i].gameObject, i);
                }
            }

            if (scrollRect == null && root != null)
                scrollRect = root.GetComponentInParent<ScrollRect>();

            if (playerModelArea == null)
                playerModelArea = FindChildByName("PlayerModelArea") as RectTransform;
            if (statsRoot == null)
                statsRoot = FindChildByName("StatsRoot");
            if (equippedWeaponIcon == null)
                equippedWeaponIcon = FindChildByName("EquippedWeaponIcon")?.GetComponent<Image>();
            ConfigureIconImage(equippedWeaponIcon);

            if (statsRoot != null)
            {
                for (int i = 0; i < 12 && i < statsRoot.childCount; i++)
                    _statTexts[i] = statsRoot.GetChild(i).GetComponentInChildren<Text>(true);
            }

            if (tooltipPanel == null) tooltipPanel = transform.Find("TooltipPanel")?.gameObject;
            if (tooltipText == null && tooltipPanel != null) tooltipText = tooltipPanel.GetComponentInChildren<Text>(true);
            if (contextMenuPanel == null) contextMenuPanel = transform.Find("ContextMenuPanel")?.gameObject;
            if (btnEquip == null && contextMenuPanel != null) btnEquip = contextMenuPanel.transform.Find("Btn_Equip")?.GetComponent<Button>();
            if (btnSell == null && contextMenuPanel != null) btnSell = contextMenuPanel.transform.Find("Btn_Sell")?.GetComponent<Button>();
            EnsureWeaponTooltipColumns();
            ConfigureTooltipRaycastTargets();
            BindContextMenuButtons();
        }

        private static void ConfigureIconImage(Image image)
        {
            if (image == null)
                return;

            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        private void ClearSlotReferences()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _slotTransforms[i] = null;
                _slotBackgrounds[i] = null;
                _slotIcons[i] = null;
                _slotCountTexts[i] = null;
                _slotCountVisuals[i] = null;
            }
        }

        private void EnsureSlotObjects(Transform root)
        {
            if (root == null || root.childCount >= SlotCount || !Application.isPlaying)
                return;

            GameObject prefab = Resources.Load<GameObject>(SlotPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[BackpackPanel] 未找到背包格子预制体 Resources/UI/BackpackSlot。");
                return;
            }

            for (int i = root.childCount; i < SlotCount; i++)
            {
                var slotGo = Instantiate(prefab, root);
                if (slotGo == null)
                    break;

                slotGo.name = $"Slot{i}";
                slotGo.SetActive(true);
            }
        }

        private void AddSlotListeners(GameObject slotGo, int displayIndex)
        {
            var trigger = slotGo.GetComponent<EventTrigger>();
            if (trigger == null) trigger = slotGo.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
            else trigger.triggers.Clear();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => OnSlotPointerEnter(displayIndex));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => OnSlotPointerExit());
            trigger.triggers.Add(exit);

            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(data =>
            {
                var ped = (PointerEventData)data;
                if (ped.button == PointerEventData.InputButton.Right)
                    OnSlotRightClick(displayIndex);
            });
            trigger.triggers.Add(click);

            var scroll = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            scroll.callback.AddListener(data => ForwardSlotScroll(data as PointerEventData));
            trigger.triggers.Add(scroll);
        }

        private void ForwardSlotScroll(PointerEventData eventData)
        {
            if (eventData == null || scrollRect == null)
                return;

            scrollRect.OnScroll(eventData);
        }

        private void BindContextMenuButtons()
        {
            if (btnEquip != null)
            {
                btnEquip.onClick.RemoveListener(OnEquipClicked);
                btnEquip.onClick.AddListener(OnEquipClicked);
            }

            if (btnSell != null)
            {
                btnSell.onClick.RemoveListener(OnSellClicked);
                btnSell.onClick.AddListener(OnSellClicked);
            }
        }

        private void ConfigureTooltipRaycastTargets()
        {
            if (tooltipPanel == null)
                return;

            var graphics = tooltipPanel.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }
        }

        private void EnsureWeaponTooltipColumns()
        {
            if (!Application.isPlaying || tooltipPanel == null || tooltipText == null || _tooltipRightText != null)
                return;

            var rightTextGo = new GameObject("TooltipRightText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            rightTextGo.layer = tooltipText.gameObject.layer;
            rightTextGo.transform.SetParent(tooltipPanel.transform, false);

            _tooltipRightText = rightTextGo.GetComponent<Text>();
            CopyTooltipTextStyle(tooltipText, _tooltipRightText);
            _tooltipRightText.alignment = TextAnchor.UpperLeft;
            _tooltipRightText.raycastTarget = false;
            _tooltipRightText.gameObject.SetActive(false);
        }

        private static void CopyTooltipTextStyle(Text source, Text target)
        {
            if (source == null || target == null)
                return;

            target.font = source.font;
            target.fontSize = source.fontSize;
            target.fontStyle = source.fontStyle;
            target.lineSpacing = source.lineSpacing;
            target.supportRichText = source.supportRichText;
            target.resizeTextForBestFit = source.resizeTextForBestFit;
            target.resizeTextMinSize = source.resizeTextMinSize;
            target.resizeTextMaxSize = source.resizeTextMaxSize;
            target.horizontalOverflow = source.horizontalOverflow;
            target.verticalOverflow = source.verticalOverflow;
            target.alignByGeometry = source.alignByGeometry;
            target.color = source.color;
        }

        private void ConfigureGenericTooltipLayout()
        {
            SetTooltipPanelWidth(TooltipPanelWidth);
            ConfigureTooltipTextRect(tooltipText, TooltipPadding, TooltipPadding, TooltipPadding, TooltipPadding);
            if (_tooltipRightText != null)
            {
                _tooltipRightText.text = string.Empty;
                _tooltipRightText.gameObject.SetActive(false);
            }
        }

        private void ConfigureWeaponTooltipLayout()
        {
            SetTooltipPanelWidth(TooltipPanelWidth);
            ConfigureTooltipTextRect(tooltipText, TooltipPadding, TooltipPadding, TooltipWeaponLeftRightInset, TooltipPadding);
            ConfigureTooltipTextRect(_tooltipRightText, TooltipWeaponRightLeftInset, TooltipPadding, TooltipPadding, TooltipPadding);
            if (_tooltipRightText != null)
                _tooltipRightText.gameObject.SetActive(true);
        }

        private static void ConfigureTooltipTextRect(Text text, float left, float top, float right, float bottom)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private void SetTooltipPanelWidth(float width)
        {
            if (tooltipPanel == null)
                return;

            var rect = tooltipPanel.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private void RefreshTooltipPanelHeight()
        {
            if (tooltipPanel == null)
                return;

            var panelRect = tooltipPanel.GetComponent<RectTransform>();
            if (panelRect == null)
                return;

            Canvas.ForceUpdateCanvases();

            float contentHeight = 0f;
            if (tooltipText != null)
                contentHeight = Mathf.Max(contentHeight, tooltipText.preferredHeight);
            if (_tooltipRightText != null && _tooltipRightText.gameObject.activeSelf)
                contentHeight = Mathf.Max(contentHeight, _tooltipRightText.preferredHeight);

            float height = Mathf.Max(TooltipMinHeight, contentHeight + TooltipPadding * 2f);
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void ConfigureSlotRaycastTargets(int displayIndex)
        {
            if (_slotTransforms == null || displayIndex < 0 || displayIndex >= _slotTransforms.Length)
                return;

            var slotTransform = _slotTransforms[displayIndex];
            if (slotTransform == null)
                return;

            var graphics = slotTransform.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }

            if (_slotBackgrounds[displayIndex] != null)
                _slotBackgrounds[displayIndex].raycastTarget = true;
            else if (_slotIcons[displayIndex] != null)
                _slotIcons[displayIndex].raycastTarget = true;
        }

        private void OnSortChanged(int value)
        {
            _presenter.SetSortMode(value);
            _presenter.RecomputeDisplayOrder();
            RefreshAllSlots();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                ResolveReferences();
                RebuildPlayerModelPreview();
                return;
            }

            var model = LevelUIModelLocator.Get();
            _player = model?.Player ?? GameStateMachine.GetInstance()?.Player;
            _presenter.Init(_player);

            _weaponDb = ConfigManager.GetInstance()?.GetWeaponDatabase();
            _itemDisplayDb = ConfigManager.GetInstance()?.GetItemDisplayDatabase();
            _shopPrice = ConfigManager.GetInstance()?.GetShopPriceConfig();
            _slotBgConfig = ConfigManager.GetInstance()?.GetSlotBackgroundConfig();
            _backpackUIConfig = ConfigManager.GetInstance()?.GetBackpackUIConfig();

            if (model != null)
                model.SubscribeInventoryChanged(OnInventoryChanged);
            else
                EventCenter.GetInstance().AddEventListener(GameEvents.InventoryChanged, OnInventoryChanged);

            if (sortDropdown != null)
            {
                sortDropdown.ClearOptions();
                if (_backpackUIConfig?.sortModeLabels != null && _backpackUIConfig.sortModeLabels.Count >= 6)
                    sortDropdown.AddOptions(_backpackUIConfig.sortModeLabels);
                else
                    sortDropdown.AddOptions(new List<string> { "品质低→高", "品质高→低", "其余优先 品质低→高", "其余优先 品质高→低", "武器优先 品质低→高", "武器优先 品质高→低" });
                sortDropdown.SetValueWithoutNotify(_presenter.GetSortModeIndex());
            }

            _presenter.RecomputeDisplayOrder();
            RefreshAllSlots();
            RefreshLeftPanel();
            RebuildPlayerModelPreview();
            HideTooltip();
            HideContextMenu();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                DestroyPlayerModelPreview();
                return;
            }

            var model = LevelUIModelLocator.Get();
            if (model != null)
                model.UnsubscribeInventoryChanged(OnInventoryChanged);
            else
            {
                var ec = EventCenter.GetInstance();
                if (ec != null) ec.RemoveEventListener(GameEvents.InventoryChanged, OnInventoryChanged);
            }
            DestroyPlayerModelPreview();
            HideContextMenu();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            EditorApplication.delayCall += DelayedEditorPreviewRefresh;
        }

        private void DelayedEditorPreviewRefresh()
        {
            if (this == null || Application.isPlaying || !isActiveAndEnabled)
                return;

            ResolveReferences();
            RebuildPlayerModelPreview();
        }
#endif

        private void OnInventoryChanged()
        {
            _presenter.RecomputeDisplayOrder();
            RefreshAllSlots();
            RefreshLeftPanel();
            RebuildPlayerModelPreview();
        }

        private void RefreshLeftPanel()
        {
            if (_player == null) return;
            var stats = _player.Stats;
            var statNames = _backpackUIConfig?.statDisplayNames;
            if (stats != null)
            {
                float[] values = new[]
                {
                    stats.MaxHp, stats.MaxMp, stats.Attack, stats.Defense,
                    stats.HpRegen, stats.MpRegen,
                    stats.CritRate * 100f, stats.CritDmg * 100f,
                    stats.AttackSpeed, stats.MoveSpeed,
                    stats.LifeSteal * 100f, stats.DamageBonus * 100f
                };
                bool[] asPercent = { false, false, false, false, false, false, true, true, false, false, true, true };
                for (int i = 0; i < 12 && i < _statTexts.Length; i++)
                {
                    if (_statTexts[i] != null)
                    {
                        string label = (statNames != null && i < statNames.Count) ? statNames[i] : $"属性{i}";
                        string num = (i <= 3 || i == 8 || i == 9) ? values[i].ToString("F0") : values[i].ToString("F1");
                        _statTexts[i].text = label + ": " + num + (asPercent[i] ? "%" : "");
                    }
                }
            }
            if (equippedWeaponIcon != null)
            {
                var w = _player.Equipment?.EquippedWeapon;
                if (w != null && _weaponDb != null)
                {
                    var entry = _weaponDb.GetEntryByWeaponId(w.weaponId);
                    if (entry?.icon != null) { equippedWeaponIcon.sprite = entry.icon; equippedWeaponIcon.enabled = true; }
                    else equippedWeaponIcon.enabled = false;
                }
                else
                    equippedWeaponIcon.enabled = false;
            }
        }

        private void RefreshAllSlots()
        {
            if (_player == null) return;
            for (int i = 0; i < SlotCount; i++)
                RefreshSlotAtDisplayIndex(i);
        }

        private Sprite GetSlotBackgroundForRarity(WeaponRarity? rarity)
        {
            return _slotBgConfig != null ? _slotBgConfig.GetSprite(rarity) : null;
        }

        private void RefreshSlotAtDisplayIndex(int displayIndex)
        {
            int modelIndex = _presenter.GetModelIndexForDisplayIndex(displayIndex);
            var slot = _player?.GetSlot(modelIndex);
            if (_slotIcons == null || displayIndex >= _slotIcons.Length) return;

            var bg = displayIndex < _slotBackgrounds.Length ? _slotBackgrounds[displayIndex] : null;
            var icon = _slotIcons[displayIndex];
            var countTxt = displayIndex < _slotCountTexts.Length ? _slotCountTexts[displayIndex] : null;
            var countVisual = displayIndex < _slotCountVisuals.Length ? _slotCountVisuals[displayIndex] : null;

            WeaponRarity? rarity = null;
            if (slot != null && !slot.IsEmpty)
            {
                if (slot.IsWeapon && slot.weapon != null)
                    rarity = slot.weapon.rarity;
                else if (slot.IsStack)
                {
                    var entry = _itemDisplayDb?.GetEntry(slot.stackItemId);
                    if (entry != null) rarity = entry.rarity;
                }
            }
            Sprite bgSprite = GetSlotBackgroundForRarity(rarity);
            if (bg != null && bgSprite != null) { bg.sprite = bgSprite; bg.enabled = true; bg.color = Color.white; }

            if (icon != null) icon.enabled = false;
            if (countTxt != null) countTxt.text = "";
            if (countVisual != null) countVisual.SetActive(false);

            if (slot == null || slot.IsEmpty)
                return;

            if (slot.IsWeapon && slot.weapon != null)
            {
                var entry = _weaponDb?.GetEntryByWeaponId(slot.weapon.weaponId);
                if (entry?.icon != null && icon != null) { icon.sprite = entry.icon; icon.enabled = true; }
                if (countTxt != null) countTxt.text = "";
                return;
            }

            if (slot.IsStack)
            {
                var entry = _itemDisplayDb?.GetEntry(slot.stackItemId);
                if (entry?.icon != null && icon != null) { icon.sprite = entry.icon; icon.enabled = true; }
                if (countTxt != null) countTxt.text = slot.stackCount > 0 ? slot.stackCount.ToString() : "";
                if (countVisual != null) countVisual.SetActive(slot.stackCount > 0);
            }
        }

        private void OnSlotPointerEnter(int displayIndex)
        {
            if (_player == null) return;
            int modelIndex = _presenter.GetModelIndexForDisplayIndex(displayIndex);
            var slot = _player.GetSlot(modelIndex);
            if (slot == null || slot.IsEmpty) { HideTooltip(); return; }

            if (slot.IsWeapon && slot.weapon != null)
            {
                SetWeaponTooltip(slot.weapon);
            }
            else if (slot.IsStack)
            {
                var entry = _itemDisplayDb?.GetEntry(slot.stackItemId);
                string text = (entry?.displayName ?? slot.stackItemId) + " x" + slot.stackCount;
                if (!string.IsNullOrEmpty(entry?.description)) text += "\n" + entry.description;
                SetGenericTooltip(text);
            }
            else
            {
                SetGenericTooltip(string.Empty);
            }

            ShowPopupAtSlot(tooltipPanel, displayIndex);
        }

        private void OnSlotPointerExit()
        {
            HideTooltip();
        }

        private void HideTooltip()
        {
            if (tooltipPanel != null) tooltipPanel.SetActive(false);
        }

        private void OnSlotRightClick(int displayIndex)
        {
            if (_player == null) return;
            int modelIndex = _presenter.GetModelIndexForDisplayIndex(displayIndex);
            var slot = _player.GetSlot(modelIndex);
            if (slot == null || slot.IsEmpty) return;

            HideTooltip();
            _contextSlotIndex = modelIndex;
            if (contextMenuPanel != null) contextMenuPanel.SetActive(true);
            if (btnEquip != null) btnEquip.gameObject.SetActive(slot.IsWeapon);
            if (btnSell != null) btnSell.gameObject.SetActive(true);
            ShowPopupAtSlot(contextMenuPanel, displayIndex);
        }

        private void HideContextMenu()
        {
            _contextSlotIndex = -1;
            if (contextMenuPanel != null) contextMenuPanel.SetActive(false);
        }

        private void SetGenericTooltip(string text)
        {
            ConfigureGenericTooltipLayout();
            if (tooltipText != null)
                tooltipText.text = text;
            RefreshTooltipPanelHeight();
        }

        private void SetWeaponTooltip(WeaponInstance weapon)
        {
            if (weapon == null)
            {
                SetGenericTooltip(string.Empty);
                return;
            }

            EnsureWeaponTooltipColumns();
            if (_tooltipRightText == null)
            {
                SetGenericTooltip(BuildWeaponTooltipFallbackText(weapon));
                return;
            }

            var entry = _weaponDb?.GetEntryByWeaponId(weapon.weaponId);
            var leftSb = new StringBuilder();
            var rightSb = new StringBuilder();

            leftSb.Append(entry?.displayName ?? weapon.weaponId);
            leftSb.Append("\n品质: ").Append(GetWeaponRarityDisplayName(weapon.rarity));
            rightSb.Append('\n');

            AppendWeaponStatLine(leftSb, rightSb, "生命", FormatSignedValue(weapon.rolledHp, 0), "法力", FormatSignedValue(weapon.rolledMp, 0));
            AppendWeaponStatLine(leftSb, rightSb, "攻击", FormatSignedValue(weapon.rolledAttack, 0), "防御", FormatSignedValue(weapon.rolledDefense, 0));
            AppendWeaponStatLine(leftSb, rightSb, "回血", FormatSignedValue(weapon.rolledHpRegen, 1), "回蓝", FormatSignedValue(weapon.rolledMpRegen, 1));
            AppendWeaponStatLine(leftSb, rightSb, "暴击", FormatSignedPercent(weapon.rolledCritRate), "爆伤", FormatSignedPercent(weapon.rolledCritDmg));
            AppendWeaponStatLine(leftSb, rightSb, "攻速", FormatSignedPercent(weapon.rolledAttackSpeed), "移速", FormatSignedPercent(weapon.rolledMoveSpeed));

            ConfigureWeaponTooltipLayout();
            tooltipText.text = leftSb.ToString();
            _tooltipRightText.text = rightSb.ToString();
            RefreshTooltipPanelHeight();
        }

        private static string BuildWeaponTooltipFallbackText(WeaponInstance weapon)
        {
            var sb = new StringBuilder();
            sb.Append(weapon.weaponId);
            sb.Append("\n品质: ").Append(GetWeaponRarityDisplayName(weapon.rarity));
            AppendWeaponFallbackLine(sb, "生命", FormatSignedValue(weapon.rolledHp, 0), "法力", FormatSignedValue(weapon.rolledMp, 0));
            AppendWeaponFallbackLine(sb, "攻击", FormatSignedValue(weapon.rolledAttack, 0), "防御", FormatSignedValue(weapon.rolledDefense, 0));
            AppendWeaponFallbackLine(sb, "回血", FormatSignedValue(weapon.rolledHpRegen, 1), "回蓝", FormatSignedValue(weapon.rolledMpRegen, 1));
            AppendWeaponFallbackLine(sb, "暴击", FormatSignedPercent(weapon.rolledCritRate), "爆伤", FormatSignedPercent(weapon.rolledCritDmg));
            AppendWeaponFallbackLine(sb, "攻速", FormatSignedPercent(weapon.rolledAttackSpeed), "移速", FormatSignedPercent(weapon.rolledMoveSpeed));
            return sb.ToString();
        }

        private static void AppendWeaponStatLine(StringBuilder leftSb, StringBuilder rightSb, string leftLabel, string leftValue, string rightLabel, string rightValue)
        {
            leftSb.Append('\n').Append(FormatWeaponStatCell(leftLabel, leftValue));
            rightSb.Append('\n').Append(FormatWeaponStatCell(rightLabel, rightValue));
        }

        private static void AppendWeaponFallbackLine(StringBuilder sb, string leftLabel, string leftValue, string rightLabel, string rightValue)
        {
            sb.Append('\n')
              .Append(FormatWeaponStatCell(leftLabel, leftValue))
              .Append("  ")
              .Append(FormatWeaponStatCell(rightLabel, rightValue));
        }

        private static string FormatWeaponStatCell(string label, string value)
        {
            string safeLabel = string.IsNullOrEmpty(label) ? "--" : label;
            string safeValue = string.IsNullOrEmpty(value) ? "--" : value;
            return safeLabel + ": " + safeValue;
        }

        private static string FormatSignedValue(float value, int decimals)
        {
            string format = decimals <= 0 ? "F0" : "F" + decimals;
            return value >= 0f ? "+" + value.ToString(format) : value.ToString(format);
        }

        private static string FormatSignedPercent(float value)
        {
            float percent = value * 100f;
            return percent >= 0f ? "+" + percent.ToString("F1") + "%" : percent.ToString("F1") + "%";
        }

        private static string GetWeaponRarityDisplayName(WeaponRarity rarity)
        {
            switch (rarity)
            {
                case WeaponRarity.Common:
                    return "普通";
                case WeaponRarity.Rare:
                    return "精良";
                case WeaponRarity.Epic:
                    return "史诗";
                case WeaponRarity.Legendary:
                    return "传说";
                default:
                    return rarity.ToString();
            }
        }

        private void OnEquipClicked()
        {
            if (_contextSlotIndex < 0 || _player == null) return;
            _player.EquipWeaponAt(_contextSlotIndex);
            EventCenter.GetInstance().EventTrigger(GameEvents.InventoryChanged);
            HideContextMenu();
        }

        private void OnSellClicked()
        {
            if (_contextSlotIndex < 0 || _player == null) return;
            var slot = _player.GetSlot(_contextSlotIndex);
            if (slot == null || slot.IsEmpty) { HideContextMenu(); return; }

            int gold = 0;
            if (slot.IsWeapon && slot.weapon != null && _shopPrice != null)
                gold = _shopPrice.GetWeaponPrice(slot.weapon.rarity);
            else if (slot.IsStack && _shopPrice != null)
                gold = _shopPrice.GetItemSellPrice(slot.stackItemId) * slot.stackCount;

            _player.SellSlotAt(_contextSlotIndex, gold);
            EventCenter.GetInstance().EventTrigger(GameEvents.InventoryChanged);
            HideContextMenu();
        }

        private void Update()
        {
            if (contextMenuPanel != null && contextMenuPanel.activeSelf && UnityEngine.Input.GetMouseButtonDown(0))
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(contextMenuPanel.GetComponent<RectTransform>(), UnityEngine.Input.mousePosition))
                    HideContextMenu();
            }

            if (!Application.isPlaying)
                return;

            _previewCombat.Tick(Time.unscaledDeltaTime);
            HandlePreviewPointerInput();
        }

        private Transform FindChildByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }

            return null;
        }

        private static Transform FindNamedDescendant(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != root && all[i].name == name)
                    return all[i];
            }

            return null;
        }

        private static GameObject ResolveCountVisualRoot(Transform slotTransform, Text countText)
        {
            if (slotTransform == null || countText == null)
                return null;

            Transform current = countText.transform;
            while (current.parent != null && current.parent != slotTransform)
                current = current.parent;

            return current.gameObject;
        }

        private void ShowPopupAtSlot(GameObject popup, int displayIndex)
        {
            if (popup == null)
                return;

            popup.SetActive(true);

            if (displayIndex < 0 || displayIndex >= _slotTransforms.Length)
                return;

            var slotRect = _slotTransforms[displayIndex] as RectTransform;
            var popupRect = popup.GetComponent<RectTransform>();
            if (slotRect == null || popupRect == null)
                return;

            Canvas.ForceUpdateCanvases();
            slotRect.GetWorldCorners(_popupWorldCorners);
            Vector3 slotCenter = (_popupWorldCorners[0] + _popupWorldCorners[2]) * 0.5f;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(GetUiCamera(), slotCenter);
            PositionPopupAtScreenPoint(popupRect, screenPoint, PopupOffset);
        }

        private void PositionPopupAtScreenPoint(RectTransform popupRect, Vector2 screenPoint, Vector2 offset)
        {
            var parentRect = popupRect.parent as RectTransform;
            if (parentRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, GetUiCamera(), out var localPoint))
                return;

            popupRect.pivot = new Vector2(0f, 1f);
            var anchored = localPoint + offset;
            var size = popupRect.rect.size;
            var bounds = parentRect.rect;
            anchored.x = Mathf.Clamp(anchored.x, bounds.xMin, bounds.xMax - size.x);
            anchored.y = Mathf.Clamp(anchored.y, bounds.yMin + size.y, bounds.yMax);
            popupRect.anchoredPosition = anchored;
        }

        private Camera GetUiCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;

            var rootCanvas = canvas.rootCanvas;
            return rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
        }

        private void RebuildPlayerModelPreview()
        {
            if (_playerModelPreviewTransform != null)
                _playerModelPreviewYaw = _playerModelPreviewTransform.eulerAngles.y;
            else if (float.IsNaN(_playerModelPreviewYaw))
                _playerModelPreviewYaw = previewModelRotation.y;

            DestroyPlayerModelPreview();
            if (playerModelArea == null || !isActiveAndEnabled)
                return;

            if (!TryResolvePreviewSource(out var sourceAnimator, out var sourceVisualRoot))
                return;

            _playerModelPreviewRoot = new GameObject("BackpackPlayerPreviewRoot");
            _playerModelPreviewRoot.hideFlags = HideFlags.HideAndDontSave;
            _playerModelPreviewRoot.transform.position = new Vector3(10000f, 10000f, 10000f);

            var clone = new GameObject("PlayerPreviewClone");
            clone.transform.SetParent(_playerModelPreviewRoot.transform, false);
            clone.transform.position = _playerModelPreviewRoot.transform.position;
            clone.transform.rotation = Quaternion.Euler(previewModelRotation.x, _playerModelPreviewYaw, previewModelRotation.z);

            var visualClone = Instantiate(sourceVisualRoot, clone.transform);
            visualClone.name = sourceVisualRoot.name;

            _playerModelPreviewAnimator = clone.AddComponent<Animator>();
            CopyPreviewAnimatorSettings(sourceAnimator, _playerModelPreviewAnimator);

            PreparePreviewClone(clone);
            SetLayerRecursively(clone.transform, PreviewLayer);
            _playerModelPreviewTransform = clone.transform;
            InitializePreviewAnimator();

            var bounds = CalculatePreviewBounds(clone.transform);
            if (bounds.size.sqrMagnitude <= 0f)
            {
                DestroyPlayerModelPreview();
                return;
            }

            int width = Mathf.Max(256, Mathf.RoundToInt(Mathf.Max(1f, playerModelArea.rect.width) * 2f));
            int height = Mathf.Max(256, Mathf.RoundToInt(Mathf.Max(1f, playerModelArea.rect.height) * 2f));

            _playerModelPreviewTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            _playerModelPreviewTexture.name = "BackpackPlayerPreviewRT";
            _playerModelPreviewTexture.Create();

            var cameraGo = new GameObject("BackpackPlayerPreviewCamera");
            cameraGo.hideFlags = HideFlags.HideAndDontSave;
            _playerModelPreviewCamera = cameraGo.AddComponent<Camera>();
            _playerModelPreviewCamera.cullingMask = 1 << PreviewLayer;
            _playerModelPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
            _playerModelPreviewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _playerModelPreviewCamera.orthographic = true;
            _playerModelPreviewCamera.nearClipPlane = 0.01f;
            _playerModelPreviewCamera.farClipPlane = 50f;
            _playerModelPreviewCamera.targetTexture = _playerModelPreviewTexture;

            var focus = bounds.center + previewFocusOffset;
            float distance = Mathf.Max(bounds.extents.z * previewDistanceMultiplier, previewMinDistance);
            _playerModelPreviewCamera.transform.position = focus + new Vector3(0f, 0f, -distance);
            _playerModelPreviewCamera.transform.LookAt(focus);
            float zoom = Mathf.Max(0.1f, previewZoom);
            _playerModelPreviewCamera.orthographicSize = Mathf.Max(bounds.extents.y * 1.15f, bounds.extents.x * 1.2f, 0.8f) / zoom;

            _playerModelPreviewImage = playerModelArea.GetComponentInChildren<RawImage>(true);
            if (_playerModelPreviewImage == null)
            {
                var previewGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                previewGo.transform.SetParent(playerModelArea, false);
                _playerModelPreviewImage = previewGo.GetComponent<RawImage>();
                var rt = previewGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            _playerModelPreviewImage.texture = _playerModelPreviewTexture;
            _playerModelPreviewImage.color = Color.white;
            _playerModelPreviewImage.raycastTarget = false;
        }

        private PlayerController ResolvePreviewSourcePlayer()
        {
            Transform levelPlayer = GameStateMachine.GetInstance()?.LevelPlayerTransform;
            if (levelPlayer != null)
            {
                var controller = levelPlayer.GetComponent<PlayerController>();
                if (controller != null)
                    return controller;
            }

            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController candidate = players[i];
                if (candidate == null)
                    continue;
                if (candidate.gameObject.hideFlags != HideFlags.None)
                    continue;
                if (_playerModelPreviewRoot != null && candidate.transform.IsChildOf(_playerModelPreviewRoot.transform))
                    continue;
                return candidate;
            }

            return null;
        }

        private bool TryResolvePreviewSource(out Animator sourceAnimator, out GameObject sourceVisualRoot)
        {
            sourceAnimator = null;
            sourceVisualRoot = null;

            var scenePlayer = ResolvePreviewSourcePlayer();
            if (scenePlayer != null)
            {
                sourceAnimator = scenePlayer.GetComponentInChildren<Animator>(true);
                sourceVisualRoot = ResolvePreviewVisualRoot(sourceAnimator);
            }

            if (sourceAnimator != null && sourceVisualRoot != null)
                return true;

            var prefab = Resources.Load<PlayerController>("Prefabs/Player");
            if (prefab == null)
                return false;

            sourceAnimator = prefab.GetComponentInChildren<Animator>(true);
            sourceVisualRoot = ResolvePreviewVisualRoot(sourceAnimator);
            return sourceAnimator != null && sourceVisualRoot != null;
        }

        private GameObject ResolvePreviewVisualRoot(Animator sourceAnimator)
        {
            if (sourceAnimator == null)
                return null;

            Transform visualRoot = sourceAnimator.transform.Find("Root");
            if (visualRoot == null && sourceAnimator.transform.childCount > 0)
                visualRoot = sourceAnimator.transform.GetChild(0);

            return visualRoot != null ? visualRoot.gameObject : null;
        }

        private void CopyPreviewAnimatorSettings(Animator sourceAnimator, Animator previewAnimator)
        {
            if (sourceAnimator == null || previewAnimator == null)
                return;

            previewAnimator.avatar = sourceAnimator.avatar;
            previewAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            previewAnimator.cullingMode = sourceAnimator.cullingMode;
            previewAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            previewAnimator.applyRootMotion = false;
        }

        private void PreparePreviewClone(GameObject clone)
        {
            if (clone == null)
                return;

            var behaviours = clone.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (behaviour is Animator animator)
                {
                    animator.enabled = true;
                    continue;
                }

                if (behaviour is Canvas)
                {
                    behaviour.enabled = false;
                    continue;
                }

                behaviour.enabled = false;
            }

            var colliders = clone.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            var rigidbodies = clone.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
                rigidbodies[i].isKinematic = true;
        }

        private Bounds CalculatePreviewBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(root.position, Vector3.one);

            Renderer primary = renderers[0];
            float bestScore = GetRendererBoundsScore(primary.bounds, primary is SkinnedMeshRenderer);
            for (int i = 1; i < renderers.Length; i++)
            {
                float score = GetRendererBoundsScore(renderers[i].bounds, renderers[i] is SkinnedMeshRenderer);
                if (score > bestScore)
                {
                    primary = renderers[i];
                    bestScore = score;
                }
            }

            Bounds bounds = primary.bounds;
            float inclusionRadius = Mathf.Max(bounds.extents.magnitude * 2f, 0.75f);
            float inclusionRadiusSqr = inclusionRadius * inclusionRadius;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == primary)
                    continue;

                Bounds candidate = renderers[i].bounds;
                if ((candidate.center - bounds.center).sqrMagnitude <= inclusionRadiusSqr)
                    bounds.Encapsulate(candidate);
            }

            return bounds;
        }

        private float GetRendererBoundsScore(Bounds bounds, bool preferSkinned)
        {
            Vector3 size = bounds.size;
            float score = Mathf.Max(0.0001f, size.x * size.y * size.z);
            if (preferSkinned)
                score *= 2f;
            return score;
        }

        private void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        private void DestroyPlayerModelPreview()
        {
            _previewCombat.Unbind();
            _previewPointerTracking = false;
            _previewPointerDragging = false;
            _playerModelPreviewAnimator = null;
            _playerModelPreviewTransform = null;

            if (_playerModelPreviewImage != null)
                _playerModelPreviewImage.texture = null;

            if (_playerModelPreviewCamera != null)
                DestroyObjectImmediateSafe(_playerModelPreviewCamera.gameObject);
            _playerModelPreviewCamera = null;

            if (_playerModelPreviewRoot != null)
                DestroyObjectImmediateSafe(_playerModelPreviewRoot);
            _playerModelPreviewRoot = null;

            if (_playerModelPreviewTexture != null)
            {
                _playerModelPreviewTexture.Release();
                DestroyObjectImmediateSafe(_playerModelPreviewTexture);
            }
            _playerModelPreviewTexture = null;
        }

        private void DestroyObjectImmediateSafe(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        private void InitializePreviewAnimator()
        {
            _previewCombat.Bind(_playerModelPreviewAnimator, _playerModelPreviewTransform);
        }

        private void HandlePreviewPointerInput()
        {
            if (_playerModelPreviewTransform == null || playerModelArea == null)
                return;

            Vector2 mousePosition = UnityEngine.Input.mousePosition;
            bool insidePreview = RectTransformUtility.RectangleContainsScreenPoint(
                playerModelArea,
                mousePosition,
                GetUiEventCamera());

            if (UnityEngine.Input.GetMouseButtonDown(0) && insidePreview)
            {
                _previewPointerTracking = true;
                _previewPointerDragging = false;
                _previewPointerDownPosition = mousePosition;
                _previewPointerDownYaw = _playerModelPreviewYaw;
            }

            if (_previewPointerTracking && UnityEngine.Input.GetMouseButton(0))
            {
                float deltaX = mousePosition.x - _previewPointerDownPosition.x;
                if (!_previewPointerDragging && Mathf.Abs(deltaX) >= PreviewDragThresholdPixels)
                    _previewPointerDragging = true;

                if (_previewPointerDragging)
                {
                    _playerModelPreviewYaw = _previewPointerDownYaw - deltaX * previewDragRotateSpeed;
                    _playerModelPreviewTransform.rotation = Quaternion.Euler(
                        previewModelRotation.x,
                        _playerModelPreviewYaw,
                        previewModelRotation.z);
                }
            }

            if (_previewPointerTracking && UnityEngine.Input.GetMouseButtonUp(0))
            {
                bool shouldAttack = insidePreview && !_previewPointerDragging;
                _previewPointerTracking = false;
                bool wasDragging = _previewPointerDragging;
                _previewPointerDragging = false;

                if (!wasDragging && shouldAttack)
                    _previewCombat.RequestNormalAttack();
            }
        }

        private Camera GetUiEventCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            return canvas.worldCamera;
        }
    }
}
