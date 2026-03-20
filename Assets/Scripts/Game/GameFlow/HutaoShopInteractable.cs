using System.Collections.Generic;
using Game.Data;
using Game.Domain;
using Game.Presentation;
using Game.Saving;
using Game.UI;
using ProjectBase;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.GameFlow
{
    /// <summary>
    /// 胡桃商店交互：玩家靠近后显示交互提示，按 E 打开对话，最终根据选项决定是否打开商店面板。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HutaoShopInteractable : MonoBehaviour
    {
        public readonly struct ShopOfferViewData
        {
            public readonly string displayName;
            public readonly Sprite icon;
            public readonly int price;
            public readonly int remainingCount;
            public readonly bool soldOut;

            public ShopOfferViewData(string displayName, Sprite icon, int price, int remainingCount)
            {
                this.displayName = displayName ?? string.Empty;
                this.icon = icon;
                this.price = Mathf.Max(0, price);
                this.remainingCount = Mathf.Max(0, remainingCount);
                soldOut = this.remainingCount <= 0;
            }
        }

        private sealed class ShopOfferRuntime
        {
            public bool isWeapon;
            public string displayName;
            public Sprite icon;
            public int price;
            public string stackItemId;
            public string weaponId;
            public WeaponRarity weaponRarity;
            public int remainingCount;
        }

        private const float DefaultInteractDistance = 3f;
        private const float DefaultPromptHeightOffset = 1.6f;
        private const string PromptCanvasResourcePath = "UI/WorldInteractionCanvas";
        private const string PromptVisualResourcePath = "UI/InteractionPromptButton";

        [Header("交互")]
        [SerializeField] [Min(0.2f)] private float _interactDistance = DefaultInteractDistance;
        [SerializeField] [Min(0f)] private float _promptHeightOffset = DefaultPromptHeightOffset;

        [Header("对话配置")]
        [SerializeField] private HutaoShopDialogueConfigSO _dialogueConfig;

        private Transform _playerTransform;
        private Transform _promptRoot;
        private Vector3 _promptBaseLocalScale = Vector3.one;
        private bool _promptVisible;
        private bool _dialogueActive;
        private bool _loggedMissingPromptConfig;
        private bool _shopInventoryGenerated;

        private readonly List<ShopOfferRuntime> _shopOffers = new List<ShopOfferRuntime>();

        private ItemDisplayDatabaseSO _itemDisplayDb;
        private ShopPriceConfigSO _shopPriceConfig;
        private WeaponDatabaseSO _weaponDb;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
        private InputAction _interactAction;
        private bool _loggedInteractActionMissing;
#endif

        public static HutaoShopInteractable EnsureOn(GameObject target)
        {
            if (target == null)
                return null;

            HutaoShopInteractable interactable = target.GetComponent<HutaoShopInteractable>();
            return interactable != null ? interactable : target.AddComponent<HutaoShopInteractable>();
        }

        private void Awake()
        {
            ApplySnapshotIfAvailable();
            EnsurePromptInstance();
            SetPromptVisible(false);
        }

        private void Update()
        {
            if (_dialogueActive && UIManager.GetInstance()?.GetPanel<DialoguePanel>(PanelNames.Dialogue) == null)
                _dialogueActive = false;

            if (_dialogueActive)
            {
                SetPromptVisible(false);
                return;
            }

            if (GameplayUIInputBridge.IsAnyGameplayPanelOpen())
            {
                SetPromptVisible(false);
                return;
            }

            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
            {
                SetPromptVisible(false);
                return;
            }

            if (GetComponentInParent<SpawnEmergenceEffect>() != null)
            {
                SetPromptVisible(false);
                return;
            }

            Transform player = ResolvePlayerTransform();
            if (player == null)
            {
                SetPromptVisible(false);
                return;
            }

            float maxDistance = Mathf.Max(0.2f, _interactDistance);
            bool inRange = (player.position - transform.position).sqrMagnitude <= maxDistance * maxDistance;
            SetPromptVisible(inRange);

            if (inRange && WasInteractPressedThisFrame())
                OpenDialogue();
        }

        private void LateUpdate()
        {
            if (_promptVisible)
                RefreshPromptTransform();
        }

        private void OpenDialogue()
        {
            if (_dialogueActive || _dialogueConfig == null || !_dialogueConfig.HasRounds)
            {
                if (_dialogueConfig == null)
                    Debug.LogWarning($"[HutaoShopInteractable:{name}] 未分配对话配置，无法打开胡桃对话。", this);
                else if (!_dialogueConfig.HasRounds)
                    Debug.LogWarning($"[HutaoShopInteractable:{name}] 对话配置 rounds 为空，无法打开胡桃对话。", this);
                return;
            }

            _dialogueActive = true;
            SetPromptVisible(false);

            UIManager.GetInstance()?.ShowPanel<DialoguePanel>(
                PanelNames.Dialogue,
                PanelLayers.Dialogue,
                panel =>
                {
                    if (panel == null)
                    {
                        Debug.LogWarning($"[HutaoShopInteractable:{name}] DialoguePanel 打开失败。", this);
                        _dialogueActive = false;
                        return;
                    }

                    panel.BeginSession(_dialogueConfig, OnDialogueFinished);
                    GameplayUIInputBridge.RequestStateRefresh();
                });
        }

        private void OnDialogueFinished(bool openShop)
        {
            _dialogueActive = false;

            if (!openShop)
            {
                GameplayUIInputBridge.RequestStateRefresh();
                return;
            }

            UIManager.GetInstance()?.ShowPanel<ShopPanel>(
                PanelNames.Shop,
                PanelLayers.Shop,
                panel =>
                {
                    panel?.BindShop(this);
                    GameplayUIInputBridge.RequestStateRefresh();
                });
        }

        public int GetShopOfferCount()
        {
            EnsureShopInventoryGenerated();
            return _shopOffers.Count;
        }

        public string GetSnapshotId()
        {
            return BuildSnapshotId();
        }

        public bool TryBuildSnapshot(out ShopSnapshotSave snapshot)
        {
            snapshot = null;
            if (!_shopInventoryGenerated)
                return false;

            snapshot = new ShopSnapshotSave
            {
                shopId = BuildSnapshotId(),
                offers = new List<ShopOfferSave>(_shopOffers.Count),
            };

            for (int i = 0; i < _shopOffers.Count; i++)
            {
                ShopOfferRuntime offer = _shopOffers[i];
                if (offer == null)
                    continue;

                snapshot.offers.Add(new ShopOfferSave
                {
                    isWeapon = offer.isWeapon,
                    stackItemId = offer.stackItemId,
                    weaponId = offer.weaponId,
                    weaponRarity = offer.weaponRarity,
                    remainingCount = Mathf.Max(0, offer.remainingCount),
                });
            }

            return true;
        }

        public void RestoreSnapshot(ShopSnapshotSave snapshot)
        {
            if (snapshot == null || !string.Equals(snapshot.shopId, BuildSnapshotId(), System.StringComparison.Ordinal))
                return;

            CacheShopConfigs();
            _shopOffers.Clear();
            _shopInventoryGenerated = true;

            if (snapshot.offers == null)
                return;

            for (int i = 0; i < snapshot.offers.Count; i++)
            {
                ShopOfferRuntime offer = BuildOfferFromSave(snapshot.offers[i]);
                if (offer != null)
                    _shopOffers.Add(offer);
            }
        }

        private void ApplySnapshotIfAvailable()
        {
            LevelSnapshot snapshot = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot;
            if (snapshot?.shops == null || snapshot.shops.Count == 0)
                return;

            string snapshotId = BuildSnapshotId();
            for (int i = 0; i < snapshot.shops.Count; i++)
            {
                ShopSnapshotSave shopSnapshot = snapshot.shops[i];
                if (shopSnapshot != null && string.Equals(shopSnapshot.shopId, snapshotId, System.StringComparison.Ordinal))
                {
                    RestoreSnapshot(shopSnapshot);
                    return;
                }
            }
        }

        public ShopOfferViewData GetShopOfferViewData(int index)
        {
            EnsureShopInventoryGenerated();
            if (index < 0 || index >= _shopOffers.Count)
                return default;

            ShopOfferRuntime offer = _shopOffers[index];
            return new ShopOfferViewData(offer.displayName, offer.icon, offer.price, offer.remainingCount);
        }

        public bool TryBuyShopOffer(int index)
        {
            EnsureShopInventoryGenerated();
            if (index < 0 || index >= _shopOffers.Count)
                return false;

            PlayerModel player = GameStateMachine.GetInstance()?.Player;
            if (player == null)
                return false;

            ShopOfferRuntime offer = _shopOffers[index];
            if (offer == null || offer.remainingCount <= 0)
                return false;

            int price = Mathf.Max(0, offer.price);
            if (player.Gold < price)
                return false;

            if (offer.isWeapon)
            {
                if (!player.CanAddWeapon())
                    return false;

                WeaponInstance weapon = CreateWeaponInstance(offer);
                if (weapon == null)
                    return false;

                if (!player.TryConsumeItem(PlayerModel.ItemIds.Gold, price))
                    return false;

                if (!player.AddWeapon(weapon))
                    return false;
            }
            else
            {
                if (!player.CanAddStackable(offer.stackItemId, 1))
                    return false;

                if (!player.TryConsumeItem(PlayerModel.ItemIds.Gold, price))
                    return false;

                if (!player.AddStackable(offer.stackItemId, 1))
                    return false;
            }

            offer.remainingCount = Mathf.Max(0, offer.remainingCount - 1);
            EventCenter.GetInstance().EventTrigger(GameEvents.InventoryChanged);
            return true;
        }

        public void EnsureShopInventoryGenerated()
        {
            if (_shopInventoryGenerated)
                return;

            _shopInventoryGenerated = true;
            _shopOffers.Clear();
            CacheShopConfigs();

            LevelConfigData levelConfig = ResolveCurrentLevelConfig();
            List<ShopGoodsEntry> shopGoodsEntries = levelConfig?.shopGoodsEntries;
            if (shopGoodsEntries == null || shopGoodsEntries.Count == 0)
                return;

            System.Random rng = CreateShopRandom();
            List<ShopGoodsEntry> validEntries = new List<ShopGoodsEntry>();
            for (int i = 0; i < shopGoodsEntries.Count; i++)
            {
                ShopGoodsEntry entry = shopGoodsEntries[i];
                if (!IsValidShopGoodsEntry(entry))
                    continue;

                validEntries.Add(entry);
                if (rng.NextDouble() > Mathf.Clamp01(entry.spawnChance))
                    continue;

                ShopOfferRuntime offer = BuildShopOffer(entry, rng);
                if (offer != null)
                    _shopOffers.Add(offer);
            }

            if (_shopOffers.Count == 0 && validEntries.Count > 0)
            {
                ShopOfferRuntime fallbackOffer = BuildShopOffer(validEntries[rng.Next(validEntries.Count)], rng);
                if (fallbackOffer != null)
                    _shopOffers.Add(fallbackOffer);
            }
        }

        private void CacheShopConfigs()
        {
            _itemDisplayDb ??= ConfigManager.GetInstance()?.GetItemDisplayDatabase();
            _shopPriceConfig ??= ConfigManager.GetInstance()?.GetShopPriceConfig();
            _weaponDb ??= ConfigManager.GetInstance()?.GetWeaponDatabase();
        }

        private LevelConfigData ResolveCurrentLevelConfig()
        {
            LevelConfigDatabaseSO levelConfigDb = ConfigManager.GetInstance()?.GetLevelConfigDatabase();
            int levelIndex = GameStateMachine.GetInstance()?.CurrentRun?.levelIndex ?? 1;
            return levelConfigDb?.GetConfigForLevel(levelIndex);
        }

        private System.Random CreateShopRandom()
        {
            int seed = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot?.seed ?? System.Environment.TickCount;
            return new System.Random(unchecked(seed ^ ComputeStableHash(BuildSnapshotId())));
        }

        private bool IsValidShopGoodsEntry(ShopGoodsEntry entry)
        {
            if (entry == null)
                return false;

            return entry.goodsType == ShopGoodsType.Weapon
                ? _weaponDb?.GetEntryByWeaponId(entry.weaponId) != null
                : !string.IsNullOrWhiteSpace(entry.itemId);
        }

        private ShopOfferRuntime BuildShopOffer(ShopGoodsEntry entry, System.Random rng)
        {
            if (entry == null)
                return null;

            int remainingCount = entry.RollCount(rng);
            if (remainingCount <= 0)
                return null;

            if (entry.goodsType == ShopGoodsType.Weapon)
            {
                WeaponEntryData weaponEntry = _weaponDb?.GetEntryByWeaponId(entry.weaponId);
                if (weaponEntry == null)
                    return null;

                return new ShopOfferRuntime
                {
                    isWeapon = true,
                    weaponId = entry.weaponId,
                    weaponRarity = entry.weaponRarity,
                    displayName = $"{weaponEntry.displayName} {GetRarityText(entry.weaponRarity)}",
                    icon = weaponEntry.icon,
                    price = _shopPriceConfig != null ? _shopPriceConfig.GetWeaponPrice(entry.weaponRarity) : 0,
                    remainingCount = remainingCount,
                };
            }

            ItemDisplayEntry itemEntry = _itemDisplayDb?.GetEntry(entry.itemId);
            return new ShopOfferRuntime
            {
                isWeapon = false,
                stackItemId = entry.itemId,
                displayName = itemEntry != null ? itemEntry.displayName : entry.itemId,
                icon = itemEntry?.icon,
                price = _shopPriceConfig != null ? _shopPriceConfig.GetItemSellPrice(entry.itemId) : 0,
                remainingCount = remainingCount,
            };
        }

        private ShopOfferRuntime BuildOfferFromSave(ShopOfferSave save)
        {
            if (save == null)
                return null;

            int remainingCount = Mathf.Max(0, save.remainingCount);
            if (save.isWeapon)
            {
                WeaponEntryData weaponEntry = _weaponDb?.GetEntryByWeaponId(save.weaponId);
                if (weaponEntry == null)
                    return null;

                return new ShopOfferRuntime
                {
                    isWeapon = true,
                    weaponId = save.weaponId,
                    weaponRarity = save.weaponRarity,
                    displayName = $"{weaponEntry.displayName} {GetRarityText(save.weaponRarity)}",
                    icon = weaponEntry.icon,
                    price = _shopPriceConfig != null ? _shopPriceConfig.GetWeaponPrice(save.weaponRarity) : 0,
                    remainingCount = remainingCount,
                };
            }

            if (string.IsNullOrWhiteSpace(save.stackItemId))
                return null;

            ItemDisplayEntry itemEntry = _itemDisplayDb?.GetEntry(save.stackItemId);
            return new ShopOfferRuntime
            {
                isWeapon = false,
                stackItemId = save.stackItemId,
                displayName = itemEntry != null ? itemEntry.displayName : save.stackItemId,
                icon = itemEntry?.icon,
                price = _shopPriceConfig != null ? _shopPriceConfig.GetItemSellPrice(save.stackItemId) : 0,
                remainingCount = remainingCount,
            };
        }

        private WeaponInstance CreateWeaponInstance(ShopOfferRuntime offer)
        {
            if (offer == null || string.IsNullOrWhiteSpace(offer.weaponId))
                return null;

            WeaponEntryData weaponEntry = _weaponDb?.GetEntryByWeaponId(offer.weaponId);
            if (weaponEntry == null)
                return null;

            System.Random rng = CreateShopRandom();
            return _weaponDb.Roll(weaponEntry.type, offer.weaponRarity, rng);
        }

        private string BuildSnapshotId()
        {
            Vector3 position = transform.position;
            string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : string.Empty;
            return $"{sceneName}|{gameObject.name}|{position.x:F3}|{position.y:F3}|{position.z:F3}";
        }

        private static int ComputeStableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * 16777619;
                return hash;
            }
        }

        private static string GetRarityText(WeaponRarity rarity)
        {
            return rarity switch
            {
                WeaponRarity.Rare => "·精良",
                WeaponRarity.Epic => "·史诗",
                WeaponRarity.Legendary => "·传说",
                _ => "·普通",
            };
        }

        private void EnsurePromptInstance()
        {
            if (_promptRoot != null)
                return;

            GameObject promptCanvasAsset = LoadPromptAsset(PromptCanvasResourcePath);
            if (promptCanvasAsset == null)
            {
                if (!_loggedMissingPromptConfig)
                {
                    _loggedMissingPromptConfig = true;
                    Debug.LogWarning($"[HutaoShopInteractable:{name}] 未找到提示资源，无法显示交互提示：canvasPath={PromptCanvasResourcePath}, promptPath={PromptVisualResourcePath}", this);
                }
                return;
            }

            if (!TryInstantiatePromptAsset(promptCanvasAsset, transform, out _promptRoot))
                return;
            _promptRoot.name = ResolvePromptInstanceName(promptCanvasAsset, _promptRoot, "WorldInteractionCanvas");
            _promptBaseLocalScale = _promptRoot.localScale;

            Transform promptVisual = null;
            GameObject promptVisualAsset = LoadPromptAsset(PromptVisualResourcePath);
            if (promptVisualAsset != null && TryInstantiatePromptAsset(promptVisualAsset, _promptRoot, out promptVisual))
                promptVisual.name = ResolvePromptInstanceName(promptVisualAsset, promptVisual, "InteractionPromptButton");

            if (promptVisual == null)
                return;

            ResetPromptVisualTransform(promptVisual);
            ApplyPromptText(promptVisual);
            RefreshPromptTransform();
        }

        private bool TryInstantiatePromptAsset(GameObject prefabAsset, Transform parent, out Transform instanceTransform)
        {
            instanceTransform = null;
            if (prefabAsset == null)
                return false;

            GameObject instanceObject;
            try
            {
                instanceObject = Instantiate(prefabAsset);
                if (parent != null)
                    instanceObject.transform.SetParent(parent, false);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HutaoShopInteractable:{name}] 实例化提示资源失败：asset={prefabAsset.name}, error={ex.GetType().Name}: {ex.Message}", this);
                return false;
            }

            instanceTransform = ResolveInstanceTransform(instanceObject);
            if (instanceTransform == null)
            {
                string instanceType = instanceObject != null ? instanceObject.GetType().FullName : "null";
                Debug.LogWarning($"[HutaoShopInteractable:{name}] 实例化后的提示资源无法转为 Transform：asset={prefabAsset.name}, instanceType={instanceType}", this);
                return false;
            }

            return true;
        }

        private static GameObject LoadPromptAsset(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            return Resources.Load<GameObject>(resourcePath);
        }

        private static string ResolvePromptInstanceName(GameObject prefabAsset, Transform instanceTransform, string defaultName)
        {
            if (prefabAsset != null && !string.IsNullOrWhiteSpace(prefabAsset.name))
                return prefabAsset.name;

            if (instanceTransform != null && !string.IsNullOrWhiteSpace(instanceTransform.name))
                return instanceTransform.name;

            return defaultName;
        }

        private static Transform ResolveInstanceTransform(UnityEngine.Object instanceObject)
        {
            if (instanceObject is GameObject gameObject)
                return gameObject.transform;

            if (instanceObject is Component component)
                return component.transform;

            return null;
        }

        private void ApplyPromptText(Transform promptVisual)
        {
            if (promptVisual == null)
                return;

            UnityEngine.UI.Text[] texts = promptVisual.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                UnityEngine.UI.Text text = texts[i];
                if (text == null)
                    continue;

                if (text.name == "KeyText")
                    text.text = "E";
            }
        }

        private static void ResetPromptVisualTransform(Transform promptVisual)
        {
            if (promptVisual == null)
                return;

            promptVisual.localPosition = Vector3.zero;
            promptVisual.localRotation = Quaternion.identity;
            promptVisual.localScale = Vector3.one;

            if (promptVisual is RectTransform rect)
                rect.anchoredPosition = Vector2.zero;
        }

        private void RefreshPromptTransform()
        {
            if (_promptRoot == null)
                return;

            _promptRoot.localPosition = new Vector3(0f, ResolvePromptHeight(), 0f);
            ApplyPromptScale();

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera != null)
                _promptRoot.rotation = worldCamera.transform.rotation;
        }

        private void ApplyPromptScale()
        {
            if (_promptRoot == null)
                return;

            Vector3 lossyScale = transform.lossyScale;
            float scaleX = Mathf.Abs(lossyScale.x) > 0.0001f ? _promptBaseLocalScale.x / Mathf.Abs(lossyScale.x) : _promptBaseLocalScale.x;
            float scaleY = Mathf.Abs(lossyScale.y) > 0.0001f ? _promptBaseLocalScale.y / Mathf.Abs(lossyScale.y) : _promptBaseLocalScale.y;
            float scaleZ = Mathf.Abs(lossyScale.z) > 0.0001f ? _promptBaseLocalScale.z / Mathf.Abs(lossyScale.z) : _promptBaseLocalScale.z;
            _promptRoot.localScale = new Vector3(scaleX, scaleY, scaleZ);
        }

        private float ResolvePromptHeight()
        {
            float height = Mathf.Max(0f, _promptHeightOffset);
            if (TryGetWorldBounds(out Bounds bounds))
                height = Mathf.Max(height, bounds.max.y - transform.position.y + 0.2f);
            return height;
        }

        private bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return false;

            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
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

            return hasBounds;
        }

        private void SetPromptVisible(bool visible)
        {
            EnsurePromptInstance();
            _promptVisible = visible && !_dialogueActive;

            if (_promptRoot != null && _promptRoot.gameObject.activeSelf != _promptVisible)
                _promptRoot.gameObject.SetActive(_promptVisible);
        }

        private Transform ResolvePlayerTransform()
        {
            if (IsUsablePlayerTransform(_playerTransform))
                return _playerTransform;

            Transform levelPlayerTransform = GameStateMachine.GetInstance()?.LevelPlayerTransform;
            if (IsUsablePlayerTransform(levelPlayerTransform))
            {
                _playerTransform = levelPlayerTransform;
                return _playerTransform;
            }

            PlayerController controller = FindFirstObjectByType<PlayerController>();
            Transform scenePlayerTransform = controller != null ? controller.transform : null;
            if (IsUsablePlayerTransform(scenePlayerTransform))
            {
                _playerTransform = scenePlayerTransform;
                return _playerTransform;
            }

            _playerTransform = null;
            return null;
        }

        private bool IsUsablePlayerTransform(Transform player)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                return false;

            Scene playerScene = player.gameObject.scene;
            if (!playerScene.IsValid() || !playerScene.isLoaded)
                return false;

            Scene currentScene = gameObject.scene;
            if (currentScene.IsValid() && currentScene.isLoaded && playerScene != currentScene)
                return false;

            return true;
        }

        private static Camera ResolveWorldCamera()
        {
            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;

            ThirdPersonCamera thirdPersonCamera = ThirdPersonCamera.Active;
            if (thirdPersonCamera != null)
            {
                Camera cameraComponent = thirdPersonCamera.GetComponent<Camera>();
                if (cameraComponent != null && cameraComponent.isActiveAndEnabled)
                    return cameraComponent;
            }

            return FindFirstObjectByType<Camera>();
        }

        private bool WasInteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            InputAction interactAction = ResolveInteractAction();
            return interactAction != null && interactAction.WasPressedThisFrame();
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private InputAction ResolveInteractAction()
        {
            if (_interactAction != null)
                return _interactAction;

            Transform player = ResolvePlayerTransform();
            if (player == null)
                return null;

            PlayerInput playerInput = player.GetComponent<PlayerInput>();
            if (playerInput == null)
                playerInput = player.GetComponentInParent<PlayerInput>();
            if (playerInput == null)
            {
                if (!_loggedInteractActionMissing)
                {
                    _loggedInteractActionMissing = true;
                    Debug.LogWarning($"[HutaoShopInteractable:{name}] 无法读取 Interact：未找到 PlayerInput", this);
                }
                return null;
            }

            if (_playerInput != playerInput)
            {
                _playerInput = playerInput;
                _interactAction = null;
            }

            _interactAction = _playerInput.actions?.FindAction("Interact");
            if (_interactAction == null)
            {
                if (!_loggedInteractActionMissing)
                {
                    _loggedInteractActionMissing = true;
                    Debug.LogWarning($"[HutaoShopInteractable:{name}] 无法读取 Interact：PlayerInput 中未找到 Interact Action", this);
                }
            }
            return _interactAction;
        }
#endif
    }
}
