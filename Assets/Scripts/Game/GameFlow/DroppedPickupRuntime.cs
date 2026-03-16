using UnityEngine;
using Game;
using Game.Data;
using Game.Domain;
using Game.Presentation;
using ProjectBase;

namespace Game.GameFlow
{
    /// <summary>
    /// 地面掉落物运行时对象：显示图标，玩家触碰后进入背包并销毁自身。
    /// </summary>
    public sealed class DroppedPickupRuntime : MonoBehaviour
    {
        private const float DefaultSpawnHeightOffset = 0.5f;
        private const float DefaultHoverAmplitude = 0.08f;
        private const float DefaultHoverFrequency = 2.4f;
        private const float DefaultPickupRadius = 0.7f;
        private const float DefaultIconScale = 0.2f;

        private static Sprite _fallbackSprite;

        private SpriteRenderer _iconRenderer;
        private Vector3 _basePosition;
        private float _spawnTime;
        private float _hoverAmplitude = DefaultHoverAmplitude;
        private float _hoverFrequency = DefaultHoverFrequency;
        private string _stackItemId;
        private int _stackCount;
        private WeaponInstance _weapon;
        private bool _pickedUp;
        private bool _initialized;

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

        private static DroppedPickupRuntime CreateRuntimeObject(Vector3 position, System.Random rng)
        {
            var go = new GameObject("DroppedPickup");
            var pickup = go.AddComponent<DroppedPickupRuntime>();
            pickup.ConfigureRuntimeComponents();
            pickup.ResetPosition(position, rng);
            return pickup;
        }

        private void ConfigureRuntimeComponents()
        {
            WorldPickupVisualConfigSO visualConfig = ConfigManager.GetInstance()?.GetWorldPickupVisualConfig();

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
        }

        private void ResetPosition(Vector3 position, System.Random rng)
        {
            WorldPickupVisualConfigSO visualConfig = ConfigManager.GetInstance()?.GetWorldPickupVisualConfig();
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
            if (_pickedUp || !IsPlayerCollider(other))
                return;

            var player = GameStateMachine.GetInstance()?.Player;
            if (player == null)
                return;

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

        private static float GetIconScale(WorldPickupVisualConfigSO config)
        {
            return config != null ? Mathf.Max(0.01f, config.iconScale) : DefaultIconScale;
        }

        private static float GetSpawnHeightOffset(WorldPickupVisualConfigSO config)
        {
            return config != null ? config.spawnHeightOffset : DefaultSpawnHeightOffset;
        }

        private static float GetHoverAmplitude(WorldPickupVisualConfigSO config)
        {
            return config != null ? Mathf.Max(0f, config.hoverAmplitude) : DefaultHoverAmplitude;
        }

        private static float GetHoverFrequency(WorldPickupVisualConfigSO config)
        {
            return config != null ? Mathf.Max(0f, config.hoverFrequency) : DefaultHoverFrequency;
        }

        private static float GetPickupRadius(WorldPickupVisualConfigSO config)
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
