using System;
using System.Collections.Generic;
using Game.Data;
using Game.Domain;
using Game.Online;
using Game.Presentation;
using Game.Saving;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Game.GameFlow
{
    /// <summary>
    /// 宝箱交互：玩家靠近后显示按键提示，按 E 开启并掉落物品，随后渐隐销毁。
    /// 当前读取本关宝箱掉落表。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChestInteractable : MonoBehaviour
    {
        private const float DefaultInteractDistance = 2.5f;
        private const float DefaultPromptHeightOffset = 1.4f;
        private const float DefaultFadeDuration = 1f;
        private const int DefaultDropCount = 1;
        private const string PromptCanvasResourcePath = "UI/WorldInteractionCanvas";
        private const string PromptVisualResourcePath = "UI/InteractionPromptButton";

        [Header("联机快照")]
        [SerializeField] [Tooltip("联机权威快照用于重建宝箱的 Resources/Prefabs 路径 ID。")]
        private string _snapshotPrefabId = string.Empty;

        [Header("交互")]
        [SerializeField] [Min(0.2f)] [Tooltip("玩家进入该距离后显示提示，并可按 E 开启宝箱。")]
        private float _interactDistance = DefaultInteractDistance;
        [SerializeField] [Min(0f)] [Tooltip("提示按钮的最小抬高高度。")]
        private float _promptHeightOffset = DefaultPromptHeightOffset;

        [Header("掉落")]
        [SerializeField] [Min(1)] [Tooltip("每次开箱掉落的物品数量。默认 1 个。")]
        private int _dropCount = DefaultDropCount;

        [Header("消失")]
        [SerializeField] [Min(0.01f)] [Tooltip("开箱后渐隐销毁所需时间。")]
        private float _fadeDuration = DefaultFadeDuration;
        private readonly List<MaterialFadeState> _fadeMaterials = new List<MaterialFadeState>();

        private Transform _playerTransform;
        private Transform _promptRoot;
        private Vector3 _promptBaseLocalScale = Vector3.one;
        private bool _promptVisible;
        private bool _opened;
        private bool _pendingOnlineOpen;
        private float _fadeElapsed;
        private string _onlineSnapshotIdOverride = string.Empty;
        private bool _loggedMissingPromptConfig;

        private sealed class MaterialFadeState
        {
            public Material material;
            public bool hasColor;
            public Color color;
            public bool hasBaseColor;
            public Color baseColor;
        }

        public static ChestInteractable EnsureOn(GameObject target)
        {
            if (target == null)
                return null;

            ChestInteractable interactable = target.GetComponent<ChestInteractable>();
            return interactable != null ? interactable : target.AddComponent<ChestInteractable>();
        }

        public string GetSnapshotId()
        {
            if (!string.IsNullOrWhiteSpace(_onlineSnapshotIdOverride))
                return _onlineSnapshotIdOverride;

            Transform owner = ResolveSnapshotOwnerTransform();
            Vector3 position = owner.position;
            string sceneName = owner.gameObject.scene.IsValid() ? owner.gameObject.scene.name : string.Empty;
            return $"{sceneName}|{owner.gameObject.name}|{position.x:F3}|{position.y:F3}|{position.z:F3}";
        }

        public void ApplyOnlineSnapshotIdentity(string snapshotId)
        {
            _onlineSnapshotIdOverride = string.IsNullOrWhiteSpace(snapshotId) ? string.Empty : snapshotId.Trim();
        }

        public void ApplyOnlineSnapshotPose(string snapshotId, Vector3 position, Quaternion rotation)
        {
            ApplyOnlineSnapshotIdentity(snapshotId);
            ResolveSnapshotOwnerTransform().SetPositionAndRotation(position, rotation);
        }

        public bool TryBuildSnapshot(out ChestSnapshotSave snapshot)
        {
            snapshot = null;
            if (_opened)
                return false;

            string snapshotId = GetSnapshotId();
            string prefabId = ResolveSnapshotPrefabId();
            if (string.IsNullOrWhiteSpace(snapshotId) || string.IsNullOrWhiteSpace(prefabId))
                return false;

            Transform owner = ResolveSnapshotOwnerTransform();
            Vector3 position = owner.position;
            snapshot = new ChestSnapshotSave
            {
                snapshotId = snapshotId,
                prefabId = prefabId,
                x = position.x,
                y = position.y,
                z = position.z,
                yaw = owner.eulerAngles.y,
            };
            return true;
        }

        private string ResolveSnapshotPrefabId()
        {
            string prefabId = _snapshotPrefabId;
            if (string.IsNullOrWhiteSpace(prefabId))
            {
                Debug.LogWarning($"[ChestInteractable:{name}] 未配置联机快照预制体 ID，宝箱不会进入联机权威快照。", this);
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(prefabId) ? string.Empty : prefabId.Trim();
        }

        private void Awake()
        {
            if (ShouldDestroyFromSnapshot())
            {
                Destroy(ResolveSnapshotOwnerTransform().gameObject);
                return;
            }

            ValidateBlockingSetup();
            EnsurePromptInstance();
            SetPromptVisible(false);
        }

        private void Update()
        {
            if (_opened)
            {
                TickFade();
                return;
            }

            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
            {
                SetPromptVisible(false);
                return;
            }

            if (GetComponent<SpawnEmergenceEffect>() != null)
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
            {
                OpenChest();
            }
        }

        private void LateUpdate()
        {
            if (_promptVisible)
                RefreshPromptTransform();
        }

        private void OpenChest()
        {
            if (_opened)
                return;

            OnlineDungeonSessionCoordinator onlineCoordinator = OnlineDungeonSessionCoordinator.GetInstance();
            if (onlineCoordinator.HasActiveSession)
            {
                bool requested = !_pendingOnlineOpen && onlineCoordinator.TryRequestOpenOnlineChest(this, _dropCount);
                if (requested)
                {
                    _pendingOnlineOpen = true;
                    SetPromptVisible(false);
                }

                return;
            }

            _opened = true;
            RecordOpenedSnapshot();
            SetPromptVisible(false);
            DisableAllColliders();
            SpawnRewards();
            CacheFadeMaterials();
        }

        public void ApplyOnlineAuthorityOpened()
        {
            if (_opened)
                return;

            _opened = true;
            _pendingOnlineOpen = false;
            RecordOpenedSnapshot();
            SetPromptVisible(false);
            DisableAllColliders();
            CacheFadeMaterials();
        }

        public void CancelPendingOnlineOpen()
        {
            if (_opened)
                return;

            _pendingOnlineOpen = false;
        }

        private void RecordOpenedSnapshot()
        {
            var run = GameStateMachine.GetInstance()?.CurrentRun;
            if (run == null)
                return;

            run.levelSnapshot ??= new Saving.LevelSnapshot();
            run.levelSnapshot.openedChestIds ??= new List<string>();

            string snapshotId = GetSnapshotId();
            if (!string.IsNullOrWhiteSpace(snapshotId) && !run.levelSnapshot.openedChestIds.Contains(snapshotId))
                run.levelSnapshot.openedChestIds.Add(snapshotId);
        }

        private bool ShouldDestroyFromSnapshot()
        {
            var snapshot = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot;
            if (snapshot?.openedChestIds == null || snapshot.openedChestIds.Count == 0)
                return false;

            return snapshot.openedChestIds.Contains(GetSnapshotId());
        }

        private void TickFade()
        {
            _fadeElapsed += Time.deltaTime;
            float duration = Mathf.Max(0.01f, _fadeDuration);
            float normalized = Mathf.Clamp01(_fadeElapsed / duration);
            ApplyAlpha(1f - normalized);

            if (normalized >= 1f)
                Destroy(ResolveSnapshotOwnerTransform().gameObject);
        }

        private void SpawnRewards()
        {
            System.Random rng = CreateChestRandom();
            Vector3 dropPosition = ResolveDropPosition();
            int rewardCount = Mathf.Max(1, _dropCount);

            for (int i = 0; i < rewardCount; i++)
            {
                string dropId = ResolveDropId(rng);
                if (string.IsNullOrWhiteSpace(dropId))
                    continue;

                SpawnDrop(dropId, dropPosition, rng);
            }
        }

        private System.Random CreateChestRandom()
        {
            int seed = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot?.seed ?? Environment.TickCount;
            return new System.Random(unchecked(seed ^ ComputeStableHash(GetSnapshotId())));
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

        private string ResolveDropId(System.Random rng)
        {
            LevelConfigDatabaseSO levelConfigDb = ConfigManager.GetInstance()?.GetLevelConfigDatabase();
            int levelIndex = GameStateMachine.GetInstance()?.CurrentRun?.levelIndex ?? 1;
            LevelConfigData levelConfig = levelConfigDb?.GetConfigForLevel(levelIndex);
            if (levelConfig == null)
                return null;

            return levelConfig.RollChestDrop(rng);
        }

        private void SpawnDrop(string dropId, Vector3 position, System.Random rng)
        {
            if (string.IsNullOrWhiteSpace(dropId))
                return;

            WeaponDatabaseSO weaponDb = ConfigManager.GetInstance()?.GetWeaponDatabase();
            if (TryGetWeaponRarity(dropId, out WeaponRarity rarity))
            {
                WeaponInstance instance = weaponDb?.RollRandomWeapon(rarity, rng);
                if (instance != null)
                {
                    DroppedPickupRuntime pickup = DroppedPickupRuntime.SpawnWeapon(instance, position, rng);
                    pickup?.RequireFreshInteractPickup();
                }
                return;
            }

            if (IsStackableDrop(dropId))
            {
                DroppedPickupRuntime pickup = DroppedPickupRuntime.SpawnStackable(dropId, 1, position, rng);
                pickup?.RequireFreshInteractPickup();
            }
        }

        private void CacheFadeMaterials()
        {
            if (_fadeMaterials.Count > 0)
                return;

            Renderer[] renderers = ResolveSnapshotOwnerTransform().GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                        continue;

                    ConfigureMaterialForFade(material);

                    var state = new MaterialFadeState
                    {
                        material = material,
                        hasColor = material.HasProperty("_Color"),
                        hasBaseColor = material.HasProperty("_BaseColor"),
                    };

                    if (state.hasColor)
                        state.color = material.GetColor("_Color");
                    if (state.hasBaseColor)
                        state.baseColor = material.GetColor("_BaseColor");

                    _fadeMaterials.Add(state);
                }
            }
        }

        private void ApplyAlpha(float alpha)
        {
            float clampedAlpha = Mathf.Clamp01(alpha);
            for (int i = 0; i < _fadeMaterials.Count; i++)
            {
                MaterialFadeState state = _fadeMaterials[i];
                if (state?.material == null)
                    continue;

                if (state.hasColor)
                {
                    Color color = state.color;
                    color.a = state.color.a * clampedAlpha;
                    state.material.SetColor("_Color", color);
                }

                if (state.hasBaseColor)
                {
                    Color baseColor = state.baseColor;
                    baseColor.a = state.baseColor.a * clampedAlpha;
                    state.material.SetColor("_BaseColor", baseColor);
                }
            }
        }

        private static void ConfigureMaterialForFade(Material material)
        {
            if (material == null)
                return;

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
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
                    Debug.LogWarning($"[ChestInteractable:{name}] 未找到提示资源，无法显示交互提示：canvasPath={PromptCanvasResourcePath}, promptPath={PromptVisualResourcePath}", this);
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
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChestInteractable:{name}] 实例化提示资源失败：asset={prefabAsset.name}, error={ex.GetType().Name}: {ex.Message}", this);
                return false;
            }

            instanceTransform = ResolveInstanceTransform(instanceObject);
            if (instanceTransform == null)
            {
                string instanceType = instanceObject != null ? instanceObject.GetType().FullName : "null";
                Debug.LogWarning($"[ChestInteractable:{name}] 实例化后的提示资源无法转为 Transform：asset={prefabAsset.name}, instanceType={instanceType}", this);
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

            _promptRoot.position = ResolvePromptWorldPosition();
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

        private Vector3 ResolvePromptWorldPosition()
        {
            Vector3 basePosition = transform.position;
            float minHeight = Mathf.Max(0f, _promptHeightOffset);
            if (TryGetWorldBounds(out Bounds bounds))
            {
                float y = Mathf.Max(basePosition.y + minHeight, bounds.max.y + 0.25f);
                return new Vector3(bounds.center.x, y, bounds.center.z);
            }

            return basePosition + Vector3.up * minHeight;
        }

        private Vector3 ResolveDropPosition()
        {
            if (TryGetWorldBounds(out Bounds bounds))
                return bounds.center;

            return ResolveSnapshotOwnerTransform().position;
        }

        private bool TryGetWorldBounds(out Bounds bounds)
        {
            Transform owner = ResolveSnapshotOwnerTransform();
            Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
            if (TryEncapsulateBounds(renderers, out bounds))
                return true;

            Collider[] colliders = owner.GetComponentsInChildren<Collider>(true);
            return TryEncapsulateBounds(colliders, out bounds);
        }

        private static bool TryEncapsulateBounds<T>(T[] components, out Bounds bounds) where T : Component
        {
            bounds = default;
            bool hasBounds = false;
            if (components == null)
                return false;

            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                    continue;

                Bounds componentBounds;
                if (component is Renderer renderer)
                    componentBounds = renderer.bounds;
                else if (component is Collider collider)
                    componentBounds = collider.bounds;
                else
                    continue;

                if (!hasBounds)
                {
                    bounds = componentBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(componentBounds);
                }
            }

            return hasBounds;
        }

        private void SetPromptVisible(bool visible)
        {
            EnsurePromptInstance();
            _promptVisible = visible && !_opened;

            if (_promptRoot != null && _promptRoot.gameObject.activeSelf != _promptVisible)
                _promptRoot.gameObject.SetActive(_promptVisible);
        }

        private void DisableAllColliders()
        {
            Transform owner = ResolveSnapshotOwnerTransform();
            Collider[] colliders = owner.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }

            NavMeshObstacle[] obstacles = owner.GetComponentsInChildren<NavMeshObstacle>(true);
            for (int i = 0; i < obstacles.Length; i++)
            {
                if (obstacles[i] != null)
                    obstacles[i].enabled = false;
            }
        }

        private void ValidateBlockingSetup()
        {
            Transform owner = ResolveSnapshotOwnerTransform();
            Collider[] colliders = owner.GetComponentsInChildren<Collider>(true);
            bool hasBlockingCollider = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && !collider.isTrigger)
                {
                    hasBlockingCollider = true;
                    break;
                }
            }

            if (!hasBlockingCollider)
                Debug.LogWarning($"[ChestInteractable:{name}] 缺少实体 Collider，玩家将无法被宝箱阻挡。请直接在宝箱 prefab 上配置阻挡碰撞体。", this);

            NavMeshObstacle[] obstacles = owner.GetComponentsInChildren<NavMeshObstacle>(true);
            bool hasNavMeshObstacle = false;
            for (int i = 0; i < obstacles.Length; i++)
            {
                if (obstacles[i] != null)
                {
                    hasNavMeshObstacle = true;
                    break;
                }
            }

            if (!hasNavMeshObstacle)
                Debug.LogWarning($"[ChestInteractable:{name}] 缺少 NavMeshObstacle，敌人可能不会绕开宝箱。请直接在宝箱 prefab 上配置导航障碍。", this);
        }

        private Transform ResolveSnapshotOwnerTransform()
        {
            Transform parent = transform.parent;
            bool parentLooksLikeChestOwner = parent != null &&
                                             parent.GetComponent<ChestInteractable>() == null &&
                                             (parent.GetComponent<Collider>() != null ||
                                              parent.GetComponent<NavMeshObstacle>() != null);
            return parentLooksLikeChestOwner
                ? transform.parent
                : transform;
        }

        private Transform ResolvePlayerTransform()
        {
            _playerTransform = LocalPlayerInteractionResolver.ResolvePlayerTransform(gameObject.scene, _playerTransform);
            return _playerTransform;
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
            return LocalPlayerInteractionResolver.IsInteractPressedThisFrame(ResolvePlayerTransform());
        }

        private static bool TryGetWeaponRarity(string dropId, out WeaponRarity rarity)
        {
            switch (dropId)
            {
                case "weapon_common":
                    rarity = WeaponRarity.Common;
                    return true;
                case "weapon_rare":
                    rarity = WeaponRarity.Rare;
                    return true;
                case "weapon_epic":
                    rarity = WeaponRarity.Epic;
                    return true;
                case "weapon_legendary":
                    rarity = WeaponRarity.Legendary;
                    return true;
                default:
                    rarity = WeaponRarity.Common;
                    return false;
            }
        }

        private static bool IsStackableDrop(string dropId)
        {
            return dropId == PlayerModel.ItemIds.PotionHp
                   || dropId == PlayerModel.ItemIds.PotionMp
                   || dropId == PlayerModel.ItemIds.Nectar;
        }
    }
}
