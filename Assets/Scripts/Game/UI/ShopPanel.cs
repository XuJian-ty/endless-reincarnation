using System;
using System.Collections.Generic;
using Game.GameFlow;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 通用商店面板：显示商品格子、玩家金币与关闭按钮。
    /// 面板不在运行时创建任何 UI，只绑定手动搭建好的预制体控件。
    /// </summary>
    public sealed class ShopPanel : BasePanel
    {
        private const string ShopSlotResourcePath = "UI/ShopSlot";
        private const int DefaultSlotCount = 6;

        [Serializable]
        private sealed class ShopSlotBinding
        {
            public GameObject root;
            public Button button;
            public Image icon;
            public Text nameText;
            public Text priceText;
            public Text countText;
            public GameObject soldOutMark;
        }

        [Header("面板控件")]
        [SerializeField] private Text _goldText;
        [SerializeField] private Transform _goodsRoot;
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private List<ShopSlotBinding> _slotBindings = new List<ShopSlotBinding>();

        private HutaoShopInteractable _shopSource;
        private bool _loggedMissingBindings;

        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () => UIManager.GetInstance().HidePanel(PanelNames.Shop));
            ResolveReferences();
            EnsureSlotBindings(0);
            BindSlotButtons();
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            OpenShop();
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        public void OpenShop()
        {
            ResolveReferences();
            EnsureSlotBindings(GetRequiredSlotCount());
            BindSlotButtons();
            if (!HasRequiredBindings())
            {
                UIManager.GetInstance()?.HidePanel(PanelNames.Shop);
                return;
            }

            RefreshView();
        }

        public void BindShop(HutaoShopInteractable source)
        {
            _shopSource = source;
            ResolveReferences();
            EnsureSlotBindings(GetRequiredSlotCount());
            BindSlotButtons();
        }

        private void ResolveReferences()
        {
            if (_goldText == null)
                _goldText = FindChildByName("GoldText")?.GetComponent<Text>();
            if (_goodsRoot == null)
                _goodsRoot = FindChildByName("GoodsRoot");
            if (_slotPrefab == null)
                _slotPrefab = Resources.Load<GameObject>(ShopSlotResourcePath);
        }

        private void EnsureSlotBindings(int requiredCount)
        {
            if (_goodsRoot == null)
                return;

            int targetCount = Mathf.Max(requiredCount, _goodsRoot.childCount);
            if (targetCount <= 0 && _goodsRoot.childCount == 0)
                targetCount = DefaultSlotCount;

            if (_goodsRoot.childCount < targetCount)
                CreateSlots(targetCount - _goodsRoot.childCount);

            if (_slotBindings == null)
                _slotBindings = new List<ShopSlotBinding>();
            else
                _slotBindings.Clear();

            for (int i = 0; i < _goodsRoot.childCount; i++)
            {
                Transform child = _goodsRoot.GetChild(i);
                if (child == null)
                    continue;

                _slotBindings.Add(CreateSlotBinding(child));
            }
        }

        private void BindSlotButtons()
        {
            if (_slotBindings == null)
                return;

            for (int i = 0; i < _slotBindings.Count; i++)
            {
                ShopSlotBinding slot = _slotBindings[i];
                if (slot?.button == null)
                    continue;

                slot.button.onClick.RemoveAllListeners();
                int capturedIndex = i;
                slot.button.onClick.AddListener(() => OnSlotClicked(capturedIndex));
            }
        }

        private bool HasRequiredBindings()
        {
            if (_goodsRoot != null && _slotBindings != null && _slotBindings.Count > 0)
                return true;

            if (_goodsRoot == null)
                LogMissingBindings("商店面板缺少 GoodsRoot，无法显示商品列表。");
            else if (_slotPrefab == null)
                LogMissingBindings("未找到 UI/ShopSlot 预制体，无法生成商品格子。");
            else
                LogMissingBindings("商店面板缺少可用的商品格子绑定。");
            return false;
        }

        private void RefreshView()
        {
            RefreshGoldText();

            if (_slotBindings == null)
                return;

            int offerCount = _shopSource != null ? _shopSource.GetShopOfferCount() : 0;
            for (int i = 0; i < _slotBindings.Count; i++)
            {
                ShopSlotBinding slot = _slotBindings[i];
                if (slot == null)
                    continue;

                bool hasOffer = i < offerCount;
                if (slot.root != null)
                    slot.root.SetActive(hasOffer);

                if (!hasOffer)
                    continue;

                HutaoShopInteractable.ShopOfferViewData offer = _shopSource.GetShopOfferViewData(i);
                if (slot.icon != null)
                    slot.icon.sprite = offer.icon;
                if (slot.nameText != null)
                    slot.nameText.text = offer.displayName;
                if (slot.priceText != null)
                    slot.priceText.text = $"价格：{Mathf.Max(0, offer.price)}";
                if (slot.countText != null)
                    slot.countText.text = $"剩余数量：{Mathf.Max(0, offer.remainingCount)}";
                if (slot.soldOutMark != null)
                    slot.soldOutMark.SetActive(offer.soldOut);

                if (slot.button != null)
                    slot.button.interactable = !offer.soldOut;
            }
        }

        private bool HasValidSlotBindings()
        {
            if (_slotBindings == null || _slotBindings.Count == 0)
                return false;

            for (int i = 0; i < _slotBindings.Count; i++)
            {
                if (_slotBindings[i]?.root == null)
                    return false;
            }

            return true;
        }

        private int GetRequiredSlotCount()
        {
            return _shopSource != null ? _shopSource.GetShopOfferCount() : 0;
        }

        private void CreateSlots(int count)
        {
            if (_goodsRoot == null || _slotPrefab == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                GameObject slotObject = Instantiate(_slotPrefab, _goodsRoot);
                slotObject.name = $"{_slotPrefab.name}_{_goodsRoot.childCount}";
                slotObject.SetActive(true);
            }
        }

        private static ShopSlotBinding CreateSlotBinding(Transform slotRoot)
        {
            return new ShopSlotBinding
            {
                root = slotRoot.gameObject,
                button = FindNestedChildByName(slotRoot, "Btn_Buy")?.GetComponent<Button>() ??
                         slotRoot.GetComponent<Button>() ??
                         slotRoot.GetComponentInChildren<Button>(true),
                icon = FindNestedChildByName(slotRoot, "Icon")?.GetComponent<Image>(),
                nameText = FindNestedChildByName(slotRoot, "NameText")?.GetComponent<Text>(),
                priceText = FindNestedChildByName(slotRoot, "PriceText")?.GetComponent<Text>(),
                countText = FindNestedChildByName(slotRoot, "CountText")?.GetComponent<Text>(),
                soldOutMark = FindNestedChildByName(slotRoot, "SoldOut")?.gameObject,
            };
        }

        private void RefreshGoldText()
        {
            if (_goldText == null)
                return;

            Game.Domain.PlayerModel player = GameStateMachine.GetInstance()?.Player;
            int gold = player != null ? player.Gold : 0;
            _goldText.text = $"金币数量：{gold}";
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (_shopSource == null)
                return;

            if (_shopSource.TryBuyShopOffer(slotIndex))
                RefreshView();
        }

        private Transform FindChildByName(string targetName)
        {
            return FindChildByName(transform, targetName);
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName)
                    return child;

                Transform nested = FindChildByName(child, targetName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static Transform FindNestedChildByName(Transform root, string targetName)
        {
            return FindChildByName(root, targetName);
        }

        private void LogMissingBindings(string message)
        {
            if (_loggedMissingBindings)
                return;

            _loggedMissingBindings = true;
            Debug.LogWarning($"[ShopPanel:{name}] {message}", this);
        }
    }
}
