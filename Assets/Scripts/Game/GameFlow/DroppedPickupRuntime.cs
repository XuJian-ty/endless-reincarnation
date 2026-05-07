using UnityEngine;
using Game;
using Game.Data;
using Game.Domain;
using Game.Online;
using Game.Presentation;
using Game.Saving;
using Game.UI;
using Newtonsoft.Json;
using ProjectBase;
using UnityEngine.SceneManagement;

namespace Game.GameFlow
{
    /// <summary>
    /// 地面掉落物运行时对象：显示图标，玩家触碰或在附近按 E 后进入背包并销毁自身。
    /// </summary>
    public sealed class DroppedPickupRuntime : MonoBehaviour
    {
        private const float DefaultSpawnHeightOffset = 0.5f;
        private const float DefaultHoverAmplitude = 0.08f;
        private const float DefaultHoverFrequency = 2.4f;
        private const float DefaultPickupRadius = 0.7f;
        private const float DefaultInteractDistance = 1.25f;
        private const float DefaultIconScale = 0.2f;

        private static Sprite _fallbackSprite;
        private static readonly System.Collections.Generic.List<DroppedPickupRuntime> ActivePickups = new System.Collections.Generic.List<DroppedPickupRuntime>();

        private SpriteRenderer _iconRenderer;
        private Vector3 _basePosition;
        private float _spawnTime;
        private float _hoverAmplitude = DefaultHoverAmplitude;
        private float _hoverFrequency = DefaultHoverFrequency;
        private float _interactDistance = DefaultInteractDistance;
        private string _stackItemId;
        private int _stackCount;
        private WeaponInstance _weapon;
        private bool _pickedUp;
        private bool _initialized;
        private string _onlineDropId;
        private bool _requireFreshInteractPickup;
        private bool _waitForInteractRelease;
        private Transform _playerTransform;

        public static DroppedPickupRuntime SpawnStackable(string itemId, int count, Vector3 position, System.Random rng = null)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
                return null;

            var pickup = CreateRuntimeObject(position, rng);
            pickup._stackItemId = itemId.Trim();
            pickup._stackCount = Mathf.Max(1, count);
            pickup.RefreshVisual();
            pickup._initialized = true;
            return pickup;
        }

        public static DroppedPickupRuntime SpawnWeapon(WeaponInstance weapon, Vector3 position, System.Random rng = null)
        {
            if (weapon == null)
                return null;

            var pickup = CreateRuntimeObject(position, rng);
            pickup._weapon = weapon;
            pickup.RefreshVisual();
            pickup._initialized = true;
            return pickup;
        }

        public static DroppedPickupRuntime SpawnOnlineDrop(OnlineDungeonDropInfo drop)
        {
            if (drop == null)
            {
                Debug.Log("[OnlineLootDebug] Online drop spawn skipped because drop is null.");
                return null;
            }

            if (drop.pickedUp)
            {
                Debug.Log($"[OnlineLootDebug] Online drop spawn skipped because already picked. drop={drop.dropId} itemType={drop.itemType}");
                return null;
            }

            Vector3 position = new Vector3(drop.x, drop.y, drop.z);
            DroppedPickupRuntime pickup = null;
            if (string.Equals(drop.itemType, "weapon", System.StringComparison.OrdinalIgnoreCase))
            {
                WeaponInstance weapon = JsonConvert.DeserializeObject<WeaponInstance>(drop.payloadJson);
                if (weapon != null)
                    pickup = SpawnWeapon(weapon, position, null);
            }
            else
            {
                ItemStackSave stack = JsonConvert.DeserializeObject<ItemStackSave>(drop.payloadJson);
                string itemId = stack != null ? stack.itemId : drop.payloadJson;
                int count = stack != null ? Mathf.Max(1, stack.count) : Mathf.Max(1, drop.count);
                if (!string.IsNullOrWhiteSpace(itemId))
                    pickup = SpawnStackable(itemId, count, position, null);
            }

            if (pickup == null)
            {
                Debug.Log($"[OnlineLootDebug] Online drop spawn failed. drop={drop.dropId} itemType={drop.itemType} count={drop.count} payloadEmpty={string.IsNullOrWhiteSpace(drop.payloadJson)}");
                return null;
            }

            pickup._onlineDropId = drop.dropId?.Trim();
            pickup._basePosition = position;
            pickup.transform.position = position;
            Debug.Log($"[OnlineLootDebug] Online drop spawned. drop={pickup._onlineDropId} itemType={drop.itemType} count={drop.count} scene={pickup.gameObject.scene.name}");
            return pickup;
        }

        public void RequireFreshInteractPickup()
        {
            _requireFreshInteractPickup = true;
#if ENABLE_INPUT_SYSTEM
            _waitForInteractRelease = LocalPlayerInteractionResolver.IsInteractPressed(ResolvePlayerTransform());
#else
            _waitForInteractRelease = false;
#endif
        }

        public static DroppedPickupRuntime SpawnFromSave(GroundDropSave save)
        {
            if (save == null)
                return null;

            Vector3 position = new Vector3(save.x, save.y, save.z);
            if (string.Equals(save.itemType, "weapon", System.StringComparison.OrdinalIgnoreCase))
            {
                WeaponInstance weapon = null;
                if (!string.IsNullOrWhiteSpace(save.payload))
                {
                    try
                    {
                        weapon = JsonConvert.DeserializeObject<WeaponInstance>(save.payload);
                    }
                    catch (JsonException)
                    {
                        return null;
                    }
                }

                if (weapon == null)
                    return null;

                DroppedPickupRuntime pickup = SpawnWeapon(weapon, position, null);
                if (pickup != null)
                {
                    pickup._basePosition = position;
                    pickup.transform.position = position;
                }

                return pickup;
            }

            ItemStackSave stack = null;
            if (!string.IsNullOrWhiteSpace(save.payload))
            {
                try
                {
                    stack = JsonConvert.DeserializeObject<ItemStackSave>(save.payload);
                }
                catch (JsonException)
                {
                    stack = null;
                }
            }

            string itemId = stack != null ? stack.itemId : save.payload;
            int count = stack != null ? Mathf.Max(1, stack.count) : 1;
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            DroppedPickupRuntime stackPickup = SpawnStackable(itemId, count, position, null);
            if (stackPickup != null)
            {
                stackPickup._basePosition = position;
                stackPickup.transform.position = position;
            }

            return stackPickup;
        }

        public bool TryBuildSave(out GroundDropSave save)
        {
            save = null;
            if (!_initialized || _pickedUp || !string.IsNullOrWhiteSpace(_onlineDropId))
                return false;

            save = new GroundDropSave
            {
                x = _basePosition.x,
                y = _basePosition.y,
                z = _basePosition.z,
            };

            if (_weapon != null)
            {
                save.itemType = "weapon";
                save.payload = JsonConvert.SerializeObject(_weapon);
                return true;
            }

            if (string.IsNullOrWhiteSpace(_stackItemId) || _stackCount <= 0)
                return false;

            save.itemType = "stackable";
            save.payload = JsonConvert.SerializeObject(new ItemStackSave
            {
                itemId = _stackItemId,
                count = _stackCount,
            });
            return true;
        }

        private static DroppedPickupRuntime CreateRuntimeObject(Vector3 position, System.Random rng)
        {
            var go = new GameObject("DroppedPickup");
            Transform pickupsRoot = LevelRuntimeHierarchy.GetPickupsRoot();
            if (pickupsRoot != null)
                go.transform.SetParent(pickupsRoot, false);
            var pickup = go.AddComponent<DroppedPickupRuntime>();
            pickup.ConfigureRuntimeComponents();
            pickup.ResetPosition(position, rng);
            return pickup;
        }

        private void ConfigureRuntimeComponents()
        {
            ItemDisplayDatabaseSO visualConfig = ConfigManager.GetInstance()?.GetItemDisplayDatabase();

            _iconRenderer = gameObject.AddComponent<SpriteRenderer>();
            _iconRenderer.sprite = GetFallbackSprite();
            _iconRenderer.color = Color.white;
            _iconRenderer.sortingOrder = 30;

            transform.localScale = Vector3.one * GetIconScale(visualConfig);

            var collider = gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = GetPickupRadius(visualConfig);

            var rigidbody = gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

            _hoverAmplitude = GetHoverAmplitude(visualConfig);
            _hoverFrequency = GetHoverFrequency(visualConfig);
            _interactDistance = Mathf.Max(GetPickupRadius(visualConfig), DefaultInteractDistance);
        }

        private void ResetPosition(Vector3 position, System.Random rng)
        {
            ItemDisplayDatabaseSO visualConfig = ConfigManager.GetInstance()?.GetItemDisplayDatabase();
            Vector3 offset = Vector3.zero;
            if (rng != null)
            {
                offset.x = Lerp(rng, -0.35f, 0.35f);
                offset.z = Lerp(rng, -0.35f, 0.35f);
            }

            _basePosition = position + offset + Vector3.up * GetSpawnHeightOffset(visualConfig);
            transform.position = _basePosition;
            _spawnTime = Time.time;
        }

        private void RefreshVisual()
        {
            if (_iconRenderer == null)
                return;

            var config = ConfigManager.GetInstance();
            Sprite icon = null;
            string displayName = "DroppedPickup";

            if (_weapon != null)
            {
                var weaponEntry = config?.GetWeaponDatabase()?.GetEntryByWeaponId(_weapon.weaponId);
                icon = weaponEntry?.icon;
                displayName = weaponEntry != null ? weaponEntry.displayName : _weapon.weaponId;
            }
            else
            {
                var itemEntry = config?.GetItemDisplayDatabase()?.GetEntry(_stackItemId);
                icon = itemEntry?.icon;
                displayName = itemEntry != null ? itemEntry.displayName : _stackItemId;
            }

            _iconRenderer.sprite = icon != null ? icon : GetFallbackSprite();
            gameObject.name = $"Drop_{displayName}";
        }

        private void Update()
        {
            if (!_initialized || _pickedUp)
                return;

            float hoverOffset = Mathf.Sin((Time.time - _spawnTime) * _hoverFrequency) * _hoverAmplitude;
            transform.position = _basePosition + Vector3.up * hoverOffset;
            FaceCamera();
#if ENABLE_INPUT_SYSTEM
            if (_waitForInteractRelease && !LocalPlayerInteractionResolver.IsInteractPressed(ResolvePlayerTransform()))
                _waitForInteractRelease = false;
#endif
            TryPickupWithInteract();
        }

        private void OnEnable()
        {
            if (!ActivePickups.Contains(this))
                ActivePickups.Add(this);
        }

        private void OnDisable()
        {
            ActivePickups.Remove(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPickup(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryPickup(other);
        }

        private void TryPickup(Collider other)
        {
            if (_pickedUp || _requireFreshInteractPickup || !IsPlayerCollider(other))
                return;

            PlayerController playerController = other.GetComponentInParent<PlayerController>();
            if (playerController == null)
                return;

            Transform localPlayerTransform = ResolvePlayerTransform();
            if (localPlayerTransform == null || playerController.transform != localPlayerTransform)
                return;

            TryPickup(playerController);
        }

        private void TryPickup(PlayerController playerController)
        {
            if (_pickedUp || playerController == null)
                return;

            var player = GameStateMachine.GetInstance()?.Player;
            if (player == null)
                return;

            if (!string.IsNullOrWhiteSpace(_onlineDropId))
            {
                if (_weapon != null)
                {
                    if (!player.CanAddWeapon())
                    {
                        Debug.Log($"[OnlineLootDebug] Online drop pickup blocked by weapon capacity. drop={_onlineDropId} scene={gameObject.scene.name}");
                        return;
                    }
                }
                else if (!player.CanAddStackable(_stackItemId, _stackCount))
                {
                    Debug.Log($"[OnlineLootDebug] Online drop pickup blocked by stack capacity. drop={_onlineDropId} item={_stackItemId} count={_stackCount} scene={gameObject.scene.name}");
                    return;
                }

                bool requested = OnlineDungeonSessionCoordinator.GetInstance().TryRequestPickupOnlineDrop(_onlineDropId);
                Debug.Log($"[OnlineLootDebug] Online drop pickup request result. drop={_onlineDropId} requested={requested} item={(_weapon != null ? "weapon" : _stackItemId)} count={_stackCount} scene={gameObject.scene.name}");

                return;
            }

            if (_weapon != null)
            {
                if (!player.CanAddWeapon() || !player.AddWeapon(_weapon))
                    return;

                EventCenter.GetInstance().EventTrigger(GameEvents.ItemPickedUp, _weapon);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_stackItemId) || _stackCount <= 0)
                    return;

                if (!player.CanAddStackable(_stackItemId, _stackCount))
                    return;

                player.AddItemCount(_stackItemId, _stackCount);
                EventCenter.GetInstance().EventTrigger(GameEvents.ItemPickedUp, _stackItemId);
            }

            _pickedUp = true;
            EventCenter.GetInstance().EventTrigger(GameEvents.InventoryChanged);
            Destroy(gameObject);
        }

        private void TryPickupWithInteract()
        {
#if ENABLE_INPUT_SYSTEM
            if (GameplayUIInputBridge.IsAnyGameplayPanelOpen())
                return;

            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            Transform playerTransform = ResolvePlayerTransform();
            if (playerTransform == null || !IsWithinInteractRange(playerTransform))
                return;

            if (!IsNearestInteractablePickup(playerTransform))
                return;

            if (!LocalPlayerInteractionResolver.IsInteractPressedThisFrame(playerTransform))
                return;

            if (_waitForInteractRelease)
                return;

            PlayerController playerController = playerTransform.GetComponent<PlayerController>();
            if (playerController == null)
                playerController = playerTransform.GetComponentInParent<PlayerController>();
            if (playerController == null)
                return;

            TryPickup(playerController);
#endif
        }

        private bool IsWithinInteractRange(Transform playerTransform)
        {
            if (playerTransform == null)
                return false;

            Vector3 offset = playerTransform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= _interactDistance * _interactDistance;
        }

        private bool IsNearestInteractablePickup(Transform playerTransform)
        {
            DroppedPickupRuntime nearestPickup = null;
            float nearestDistanceSqr = float.MaxValue;

            for (int i = ActivePickups.Count - 1; i >= 0; i--)
            {
                DroppedPickupRuntime pickup = ActivePickups[i];
                if (pickup == null)
                {
                    ActivePickups.RemoveAt(i);
                    continue;
                }

                if (!pickup.CanBeInteractPicked())
                    continue;

                Vector3 offset = playerTransform.position - pickup.transform.position;
                offset.y = 0f;
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr > pickup._interactDistance * pickup._interactDistance)
                    continue;

                if (nearestPickup == null ||
                    distanceSqr < nearestDistanceSqr ||
                    (Mathf.Approximately(distanceSqr, nearestDistanceSqr) && pickup.GetInstanceID() < nearestPickup.GetInstanceID()))
                {
                    nearestPickup = pickup;
                    nearestDistanceSqr = distanceSqr;
                }
            }

            return nearestPickup == this;
        }

        private bool CanBeInteractPicked()
        {
            return !_pickedUp && _initialized && isActiveAndEnabled;
        }

        private Transform ResolvePlayerTransform()
        {
            _playerTransform = LocalPlayerInteractionResolver.ResolvePlayerTransform(gameObject.scene, _playerTransform);
            return _playerTransform;
        }

        private static bool IsPlayerCollider(Collider other)
        {
            if (other == null)
                return false;

            if (other.CompareTag("Player"))
                return true;

            return other.GetComponentInParent<PlayerController>() != null;
        }

        private void FaceCamera()
        {
            Camera cam = null;
            if (ThirdPersonCamera.Active != null)
                cam = ThirdPersonCamera.Active.GetComponent<Camera>();
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return;

            transform.rotation = cam.transform.rotation;
        }

        private static float Lerp(System.Random rng, float min, float max)
        {
            if (rng == null)
                return min;

            return min + (float)rng.NextDouble() * (max - min);
        }

        private static float GetIconScale(ItemDisplayDatabaseSO config)
        {
            return config != null ? Mathf.Max(0.01f, config.iconScale) : DefaultIconScale;
        }

        private static float GetSpawnHeightOffset(ItemDisplayDatabaseSO config)
        {
            return config != null ? config.spawnHeightOffset : DefaultSpawnHeightOffset;
        }

        private static float GetHoverAmplitude(ItemDisplayDatabaseSO config)
        {
            return config != null ? Mathf.Max(0f, config.hoverAmplitude) : DefaultHoverAmplitude;
        }

        private static float GetHoverFrequency(ItemDisplayDatabaseSO config)
        {
            return config != null ? Mathf.Max(0f, config.hoverFrequency) : DefaultHoverFrequency;
        }

        private static float GetPickupRadius(ItemDisplayDatabaseSO config)
        {
            return config != null ? Mathf.Max(0.01f, config.pickupRadius) : DefaultPickupRadius;
        }

        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite != null)
                return _fallbackSprite;

            _fallbackSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f));

            return _fallbackSprite;
        }
    }
}
