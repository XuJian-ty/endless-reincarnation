using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Game;
using Game.Domain;
using Game.Data;
using Game.GameFlow;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 背包面板：左侧为玩家模型区、12 项属性（一行 2 个）、当前装备武器格；右侧为 50 格滚动视图（每行 5 格，鼠标滚轮垂直滚动），支持 6 种排序。
    /// 约定：右侧 Slots 为 ScrollView/Viewport/Content，Content 下 50 个子物体依次为 Slot0..Slot49；每格下有 Image(图标)、Text(数量可选)。
    /// 左侧需有 PlayerModelArea（可选）、StatsRoot（12 个 Text 或子节点）、EquippedWeaponIcon（Image）；排序下拉或按钮命名为 SortDropdown / Btn_Sort*。
    /// </summary>
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
        private Text[] _statTexts = new Text[12];
        private int _contextSlotIndex = -1;
        private PlayerModel _player;
        private WeaponDatabaseSO _weaponDb;
        private ItemDisplayDatabaseSO _itemDisplayDb;
        private ShopPriceConfigSO _shopPrice;
        private SlotBackgroundConfigSO _slotBgConfig;
        private BackpackUIConfigSO _backpackUIConfig;
        private readonly BackpackPresenter _presenter = new BackpackPresenter();

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
            if (root != null && root.childCount >= SlotCount)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    _slotTransforms[i] = root.GetChild(i);
                    var images = _slotTransforms[i].GetComponentsInChildren<Image>(true);
                    if (images != null && images.Length >= 2)
                    {
                        _slotBackgrounds[i] = images[0];
                        _slotIcons[i] = images[1];
                    }
                    else if (images != null && images.Length == 1)
                    {
                        _slotBackgrounds[i] = null;
                        _slotIcons[i] = images[0];
                    }
                    else
                    {
                        _slotBackgrounds[i] = null;
                        _slotIcons[i] = _slotTransforms[i].GetComponentInChildren<Image>(true);
                    }

                    // 禁用 Background Icon 的 RaycastTarget，避免阻挡滚轮事件
                    if (_slotBackgrounds[i] != null)
                        _slotBackgrounds[i].raycastTarget = false;
                    if (_slotIcons[i] != null)
                        _slotIcons[i].raycastTarget = false;

                    _slotCountTexts[i] = _slotTransforms[i].GetComponentInChildren<Text>(true);
                    AddSlotListeners(_slotTransforms[i].gameObject, i);
                }
            }

            if (scrollRect == null && root != null)
                scrollRect = root.GetComponentInParent<ScrollRect>();

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
        }

        private void AddSlotListeners(GameObject slotGo, int displayIndex)
        {
            var trigger = slotGo.GetComponent<EventTrigger>();
            if (trigger == null) trigger = slotGo.AddComponent<EventTrigger>();

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
        }

        private void OnSortChanged(int value)
        {
            _presenter.SetSortMode(value);
            _presenter.RecomputeDisplayOrder();
            RefreshAllSlots();
        }

        private void OnEnable()
        {
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
            HideTooltip();
            HideContextMenu();
        }

        private void OnDisable()
        {
            var model = LevelUIModelLocator.Get();
            if (model != null)
                model.UnsubscribeInventoryChanged(OnInventoryChanged);
            else
            {
                var ec = EventCenter.GetInstance();
                if (ec != null) ec.RemoveEventListener(GameEvents.InventoryChanged, OnInventoryChanged);
            }
            HideContextMenu();
        }

        private void OnInventoryChanged()
        {
            _presenter.RecomputeDisplayOrder();
            RefreshAllSlots();
            RefreshLeftPanel();
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
                if (countTxt != null) countTxt.text = slot.stackCount > 1 ? slot.stackCount.ToString() : "";
            }
        }

        private void OnSlotPointerEnter(int displayIndex)
        {
            if (_player == null) return;
            int modelIndex = _presenter.GetModelIndexForDisplayIndex(displayIndex);
            var slot = _player.GetSlot(modelIndex);
            if (slot == null || slot.IsEmpty) { HideTooltip(); return; }

            string text = "";
            if (slot.IsWeapon && slot.weapon != null)
            {
                var w = slot.weapon;
                var entry = _weaponDb?.GetEntryByWeaponId(w.weaponId);
                text = (entry?.displayName ?? w.weaponId) + "\n品质: " + w.rarity;
                text += "\n生命 +" + w.rolledHp + " 攻击 +" + w.rolledAttack + " 防御 +" + w.rolledDefense;
                if (w.rolledHpRegen != 0) text += " 生命回复 +" + w.rolledHpRegen;
                if (w.rolledCritRate > 0) text += " 暴击率 +" + (w.rolledCritRate * 100) + "%";
            }
            else if (slot.IsStack)
            {
                var entry = _itemDisplayDb?.GetEntry(slot.stackItemId);
                text = (entry?.displayName ?? slot.stackItemId) + " x" + slot.stackCount;
                if (!string.IsNullOrEmpty(entry?.description)) text += "\n" + entry.description;
            }

            if (tooltipText != null) tooltipText.text = text;
            if (tooltipPanel != null) tooltipPanel.SetActive(true);
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

            _contextSlotIndex = modelIndex;
            if (contextMenuPanel != null) contextMenuPanel.SetActive(true);
            if (btnEquip != null) btnEquip.gameObject.SetActive(slot.IsWeapon);
            if (btnSell != null) btnSell.gameObject.SetActive(true);
            if (contextMenuPanel != null && contextMenuPanel.activeSelf)
            {
                var rt = contextMenuPanel.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = UnityEngine.Input.mousePosition;
            }
        }

        private void HideContextMenu()
        {
            _contextSlotIndex = -1;
            if (contextMenuPanel != null) contextMenuPanel.SetActive(false);
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
        }
    }
}
