using System.Collections.Generic;
using UnityEngine;
using Game.Domain;
using Game.Presentation;
using Game.Data;
using Game.Online;
using Game.Saving;
using Game.Social;
using Game.UI;
using ProjectBase;
using UnityEngine.SceneManagement;

namespace Game.GameFlow
{
    /// <summary>
    /// Level entry orchestration for scene startup.
    /// Handles direct-scene save startup, player placement, HUD setup, and buff selection.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LevelBootstrapper : MonoBehaviour
    {
        [Header("玩家")]
        [SerializeField] [Tooltip("场景中预放的玩家实例。可留空，留空时会自动查找或从玩家预制体生成。")]
        private PlayerController playerController;
        [SerializeField] [Tooltip("玩家预制体。场景中没有玩家实例时会优先使用它生成。")]
        private PlayerController playerPrefab;

        [Header("相机")]
        [SerializeField] [Tooltip("场景中的第三人称相机。可留空，留空时会自动查找。")]
        private ThirdPersonCamera cameraRig;

        [Header("出生点")]
        [SerializeField] [Tooltip("可选出生点。为空时使用 LevelBootstrapper 自身位置和朝向。")]
        private Transform spawnPoint;

        private UIDeathChoiceHandler _deathChoiceHandler;
        private string _lastOnlineGroundDropsSnapshotKey;

        private void Awake()
        {
            var gsm = GameStateMachine.GetInstance();
            if (gsm?.CurrentRun != null && gsm.Player != null)
                return;

            TryAdoptDirectSceneSaveContext(gsm, out _, out _);
        }

        private void Start()
        {
            PoolMgr.GetInstance().EnsureInitialized();
            LevelRuntimeHierarchy.EnsureSceneRoots();
            LevelRuntimeHierarchy.OrganizeSceneRuntimeObjects();

            var gsm = GameStateMachine.GetInstance();
            var run = gsm?.CurrentRun;
            var playerModel = gsm?.Player;

            if (run == null || playerModel == null)
            {
                if (!TryAdoptDirectSceneSaveContext(gsm, out run, out playerModel))
                {
                    Debug.LogError("[LevelBootstrapper] Failed to create or load the direct scene save context.");
                    return;
                }
            }

            BattleMemorySceneRuntime.TryAdoptPreviewContext(gsm, ref run, ref playerModel);
            FinalBossDuelSceneRuntime.TryAdoptPreviewContext(gsm, ref run, ref playerModel);

            if (SocialAidSessionCoordinator.GetInstance().IsInNetworkPlayMode)
            {
                Debug.Log("[LevelBootstrapper] 当前处于援助联机会话，关卡已进入联机兼容模式。");
            }

            ResolveSceneReferences();
            EnsurePlayerInstance(run);

            if (playerController == null)
            {
                Debug.LogError("[LevelBootstrapper] PlayerController is missing. Place a player in the scene or assign a player prefab.");
                return;
            }

            playerController.Init(playerModel);
            playerController.OnPlayerDied += HandlePlayerDied;
            gsm?.SetLevelPlayerTransform(playerController.transform);
            _deathChoiceHandler ??= new UIDeathChoiceHandler();
            gsm?.SetDeathChoiceHandler(_deathChoiceHandler);

            if (OnlineDungeonSessionCoordinator.GetInstance().TryConsumeSpawnOverride(out Vector3 onlineSpawnPosition, out Quaternion onlineSpawnRotation))
            {
                PlacePlayer(onlineSpawnPosition, onlineSpawnRotation);
            }
            else if (SocialAidSessionCoordinator.GetInstance().TryConsumeHelperSpawnOverride(out Vector3 aidSpawnPosition, out Quaternion aidSpawnRotation))
            {
                PlacePlayer(aidSpawnPosition, aidSpawnRotation);
            }
            else if (run.levelSnapshot != null)
            {
                var snap = run.levelSnapshot;
                var rotation = Quaternion.Euler(0f, snap.playerYaw, 0f);
                PlacePlayer(new Vector3(snap.playerX, snap.playerY, snap.playerZ), rotation);
            }
            else
            {
                var spawnTransform = GetSpawnTransform();
                var rotation = spawnTransform != null ? spawnTransform.rotation : Quaternion.identity;
                PlacePlayer(spawnTransform != null ? spawnTransform.position : Vector3.zero, rotation);
            }

            BindCameraToPlayer();

            Debug.Log($"[OpeningStoryDebug] Level entry buff check. level={run.levelIndex} pendingBuffSelection={run.pendingBuffSelection} scene={SceneManager.GetActiveScene().name}");
            if (run.pendingBuffSelection)
                StartCoroutine(ShowOpeningStoryComicOrBuffSelectionAfterLoading(run));

            LevelUIModelLocator.Set(new LevelUIModel(gsm));

            var ui = UIManager.GetInstance();
            if (ui != null)
            {
                ui.ShowPanel<PlayerInfoHUDPanel>(PanelNames.PlayerInfoHUD, PanelLayers.PlayerInfoHUD);
                ui.ShowPanel<FinalBossHealthBarOverlay>(PanelNames.FinalBossHealthBarOverlay, PanelLayers.FinalBossHealthBarOverlay);
                ui.ShowPanel<FloatingNumberOverlay>(PanelNames.FloatingNumberOverlay, PanelLayers.FloatingNumberOverlay);
            }

            DropdownScrollForwarder.EnsureExistsInScene();

            bool isBattleMemoryScene = BattleMemorySceneRuntime.TryPrepareScene(this);
            bool isFinalBossDuelScene = FinalBossDuelSceneRuntime.TryPrepareScene(this);
            bool skipLocalSnapshotRestore = OnlineDungeonSessionCoordinator.GetInstance().ShouldSkipLocalSnapshotRestore()
                                            || SocialAidSessionCoordinator.GetInstance().ShouldSkipLocalSnapshotRestore();
            if (!isBattleMemoryScene && !isFinalBossDuelScene && !skipLocalSnapshotRestore && run.levelSnapshot != null)
                StartCoroutine(RestoreLevelSnapshotState(run.levelSnapshot));

            RemoveOnlineFollowerLocalEnemies();

            Debug.Log($"[LevelBootstrapper] Level {run.levelIndex} initialized. Difficulty {run.difficulty}.");
        }

        private void OnDestroy()
        {
            GameStateMachine.GetInstance()?.SetLevelPlayerTransform(null);
            GameStateMachine.GetInstance()?.SetDeathChoiceHandler(null);
            LevelUIModelLocator.Set(null);

            if (playerController != null)
                playerController.OnPlayerDied -= HandlePlayerDied;

            var ui = UIManager.GetInstance();
            if (ui != null)
            {
                ui.HidePanel(PanelNames.FloatingNumberOverlay);
                ui.HidePanel(PanelNames.FinalBossHealthBarOverlay);
                ui.HidePanel(PanelNames.PlayerInfoHUD);
            }
        }

        private void HandlePlayerDied()
        {
            GameStateMachine.GetInstance()?.OnPlayerDied();
        }

        public void SaveAndQuit()
        {
            CaptureRuntimeSnapshot();
            var gsm = GameStateMachine.GetInstance();
            gsm?.SaveAndQuit();
        }

        public void CaptureRuntimeSnapshot()
        {
            var gsm = GameStateMachine.GetInstance();
            var run = gsm?.CurrentRun;
            if (run == null || playerController == null)
                return;

            var pos = playerController.transform.position;
            run.levelSnapshot ??= new LevelSnapshot();
            run.levelSnapshot.playerX = pos.x;
            run.levelSnapshot.playerY = pos.y;
            run.levelSnapshot.playerZ = pos.z;
            run.levelSnapshot.playerYaw = playerController.transform.eulerAngles.y;
            run.levelSnapshot.runtimeSnapshotVersion = 1;

            CaptureEnemySnapshots(run.levelSnapshot);
            CaptureChestSnapshots(run.levelSnapshot);
            CaptureShopSnapshots(run.levelSnapshot);
            CaptureGroundDropSnapshots(run.levelSnapshot);
            CaptureSpawnerSnapshots(run.levelSnapshot);
            CaptureLevelDirectorSnapshot(run.levelSnapshot);
        }

        public void ApplyOnlineWorldSnapshot(LevelSnapshot snapshot, System.Action onCompleted)
        {
            ApplyOnlineWorldSnapshotState(snapshot);
            onCompleted?.Invoke();
        }

        public void ApplyOnlineChestAuthorityState(OnlineDungeonChestAuthorityInfo chestState)
        {
            if (chestState == null || string.IsNullOrWhiteSpace(chestState.chestId))
                return;

            string chestId = chestState.chestId.Trim();
            if (!IsChestSnapshotForActiveScene(chestId))
                return;

            if (chestState.opened)
            {
                ChestInteractable[] chests = UnityEngine.Object.FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < chests.Length; i++)
                {
                    ChestInteractable chest = chests[i];
                    if (chest != null && string.Equals(chest.GetSnapshotId(), chestId, System.StringComparison.Ordinal))
                        chest.ApplyOnlineAuthorityOpened();
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(chestState.prefabId))
            {
                Debug.LogWarning($"[LevelBootstrapper] 联机宝箱权威状态缺少 prefabId：{chestId}");
                return;
            }

            UpsertChestSnapshot(new ChestSnapshotSave
            {
                snapshotId = chestId,
                prefabId = chestState.prefabId,
                x = chestState.x,
                y = chestState.y,
                z = chestState.z,
                yaw = chestState.yaw,
            });
        }

        private static bool IsChestSnapshotForActiveScene(string snapshotId)
        {
            if (string.IsNullOrWhiteSpace(snapshotId))
                return false;

            string sceneName = SceneManager.GetActiveScene().name;
            return snapshotId.StartsWith($"{sceneName}|", System.StringComparison.Ordinal);
        }

        private static void RemoveOnlineFollowerLocalEnemies()
        {
            OnlineDungeonSessionCoordinator coordinator = OnlineDungeonSessionCoordinator.GetInstance();
            if (!coordinator.HasActiveSession || coordinator.ShouldDriveOnlineWorldSimulation())
                return;

            EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null)
                    continue;

                enemy.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(enemy.gameObject);
            }
        }

        private bool TryAdoptDirectSceneSaveContext(GameStateMachine gsm, out RunData run, out PlayerModel playerModel)
        {
            run = null;
            playerModel = null;

            if (gsm == null)
                return false;

            SaveSystem saveSystem = SaveSystem.GetInstance();
            if (saveSystem == null)
                return false;

            int levelIndex = ResolveFallbackLevelIndex();
            SaveData data = saveSystem.GetOrCreateSaveForPlayerAndLevel("天依", levelIndex, out string saveId);
            if (data?.run == null)
                return false;

            run = data.run;
            playerModel = new PlayerModel(ConfigManager.GetInstance().GetLevelGrowth());
            gsm.AdoptRuntimeContext(run, playerModel, data.playerName, saveId, data.portraitId);
            Debug.Log($"[LevelBootstrapper] Adopted direct scene save '{data.playerName}' for level {levelIndex}.");
            return true;
        }

        private void PlacePlayer(Vector3 pos, Quaternion rotation)
        {
            if (playerController == null) return;

            var cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerController.transform.position = pos;
            playerController.transform.rotation = rotation;
            if (cc != null) cc.enabled = true;
        }

        private void ResolveSceneReferences()
        {
            if (!IsValidSceneObject(playerController))
            {
                playerController = FindFirstObjectByType<PlayerController>();
                if (playerController == null && playerPrefab == null && Resources.Load<PlayerController>("Prefabs/Player") == null)
                    Debug.LogWarning("[LevelBootstrapper] Scene PlayerController not found.");
            }

            if (!IsValidSceneObject(spawnPoint))
            {
                spawnPoint = FindSpawnPointInScene();
                if (spawnPoint == null)
                    Debug.LogWarning("[LevelBootstrapper] Spawn point not found. Fresh runs will fall back to LevelBootstrapper transform.");
            }

            if (!IsValidSceneObject(cameraRig))
                cameraRig = FindFirstObjectByType<ThirdPersonCamera>();
        }

        private void EnsurePlayerInstance(RunData run)
        {
            if (IsValidSceneObject(playerController))
                return;

            var prefab = playerPrefab != null ? playerPrefab : Resources.Load<PlayerController>("Prefabs/Player");
            if (prefab == null)
            {
                Debug.LogWarning("[LevelBootstrapper] Player prefab not found. Expected inspector reference or Resources/Prefabs/Player.");
                return;
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;
            if (run?.levelSnapshot != null)
            {
                spawnPosition = new Vector3(run.levelSnapshot.playerX, run.levelSnapshot.playerY, run.levelSnapshot.playerZ);
                spawnRotation = Quaternion.Euler(0f, run.levelSnapshot.playerYaw, 0f);
            }
            else
            {
                var spawnTransform = GetSpawnTransform();
                spawnPosition = spawnTransform != null ? spawnTransform.position : Vector3.zero;
                spawnRotation = spawnTransform != null ? spawnTransform.rotation : prefab.transform.rotation;
            }

            playerController = Instantiate(prefab, spawnPosition, spawnRotation);
            playerController.name = prefab.name;
        }

        private Transform GetSpawnTransform()
        {
            return spawnPoint != null ? spawnPoint : transform;
        }

        private void BindCameraToPlayer()
        {
            if (playerController == null)
                return;

            if (cameraRig == null)
                return;

            var inputHandler = playerController.GetComponent<PlayerInputHandler>();
            cameraRig.BindTarget(playerController.transform, inputHandler);
            playerController.BindCamera(cameraRig);
        }

        private static bool IsValidSceneObject(Component component)
        {
            return component != null &&
                   component.gameObject.scene.IsValid() &&
                   component.gameObject.scene.isLoaded;
        }

        private static Transform FindSpawnPointInScene()
        {
            var marker = GameObject.Find("\u73A9\u5BB6\u51FA\u751F\u70B9");
            if (marker != null)
                return marker.transform;

            marker = GameObject.Find("PlayerSpawnPoint");
            if (marker != null)
                return marker.transform;

            marker = GameObject.Find("SpawnPoint");
            if (marker != null)
                return marker.transform;

            return null;
        }

        private static int ResolveFallbackLevelIndex()
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(activeSceneName))
                return 1;

            LevelConfigDatabaseSO configDb = ConfigManager.GetInstance()?.GetLevelConfigDatabase();
            if (configDb?.levels != null)
            {
                for (int i = 0; i < configDb.levels.Count; i++)
                {
                    LevelConfigData config = configDb.levels[i];
                    if (config != null && string.Equals(config.sceneName, activeSceneName, System.StringComparison.OrdinalIgnoreCase))
                        return Mathf.Max(1, config.levelIndex);
                }
            }

            const string levelPrefix = "Level_";
            if (activeSceneName.StartsWith(levelPrefix, System.StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(activeSceneName.Substring(levelPrefix.Length), out int parsedLevel))
            {
                return Mathf.Max(1, parsedLevel);
            }

            return 1;
        }

        private void ShowBuffSelection()
        {
            var config = ConfigManager.GetInstance()?.GetBuffConfig();
            var allBuffs = config != null ? new List<string>(config.GetAllBuffIds()) : new List<string>();
            if (allBuffs.Count < 3)
            {
                Debug.LogWarning("[LevelBootstrapper] Buff config has fewer than 3 entries. Cannot open three-choice selection.");
                return;
            }

            var selectedBuffs = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                int randomIndex = Random.Range(0, allBuffs.Count);
                selectedBuffs.Add(allBuffs[randomIndex]);
                allBuffs.RemoveAt(randomIndex);
            }

            UIManager.GetInstance()?.ShowPanel<BuffSelectPanel>(
                PanelNames.BuffSelect,
                PanelLayers.BuffSelect,
                panel => panel.ShowWithBuffs(selectedBuffs, OnBuffSelected));
        }

        private void ShowOpeningStoryComicOrBuffSelection(RunData run)
        {
            if (run == null || run.levelIndex != 1)
            {
                Debug.Log($"[OpeningStoryDebug] Skip opening story and show buff directly. runNull={run == null} level={(run != null ? run.levelIndex : 0)} scene={SceneManager.GetActiveScene().name}");
                ShowBuffSelection();
                return;
            }

            UIManager ui = UIManager.GetInstance();
            if (ui == null)
            {
                Debug.LogWarning($"[OpeningStoryDebug] UIManager missing, show buff directly. level={run.levelIndex} scene={SceneManager.GetActiveScene().name}");
                ShowBuffSelection();
                return;
            }

            Debug.Log($"[OpeningStoryDebug] Request opening story panel before buff selection. level={run.levelIndex} scene={SceneManager.GetActiveScene().name}");
            ui.ShowPanel<OpeningStoryComicPanel>(
                PanelNames.OpeningStoryComic,
                PanelLayers.OpeningStoryComic,
                panel => panel.Play(ShowBuffSelection));
        }

        private System.Collections.IEnumerator ShowOpeningStoryComicOrBuffSelectionAfterLoading(RunData run)
        {
            UIManager ui = UIManager.GetInstance();
            while (ui != null && ui.GetPanel<LoadingPanel>(PanelNames.Loading) != null)
                yield return null;

            yield return null;
            ShowOpeningStoryComicOrBuffSelection(run);
        }

        private void OnBuffSelected(string buffId)
        {
            var gsm = GameStateMachine.GetInstance();
            var run = gsm?.CurrentRun;
            var playerModel = gsm?.Player;
            if (playerModel == null || run == null) return;

            var config = ConfigManager.GetInstance()?.GetBuffConfig();
            if (!string.IsNullOrWhiteSpace(run.currentLevelBuffId))
                playerModel.RemoveBuff(run.currentLevelBuffId);

            var modifier = config != null ? config.GetModifierForBuff(buffId) : new StatModifier();
            playerModel.AddBuff(buffId, modifier);
            run.pendingBuffSelection = false;
            run.currentLevelBuffId = buffId;
            playerModel.SaveTo(run);
            gsm.SaveCurrent();
        }

        private System.Collections.IEnumerator RestoreLevelSnapshotState(LevelSnapshot snapshot)
        {
            yield return null;

            if (snapshot == null)
                yield break;

            RestoreOpenedChests(snapshot);
            RestoreChestSnapshots(snapshot);
            RestoreEnemySnapshots(snapshot);
            UnityEngine.Object.FindFirstObjectByType<LevelDirector>()?.CompleteRuntimeSnapshotRestore();
            RestoreShopSnapshots(snapshot);
            RestoreGroundDrops(snapshot);
        }

        private static void CaptureEnemySnapshots(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            snapshot.enemies ??= new List<EnemySnapshot>();
            snapshot.enemies.Clear();

            EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy != null && enemy.TryBuildSnapshot(out EnemySnapshot enemySnapshot))
                    snapshot.enemies.Add(enemySnapshot);
            }
        }

        private static void CaptureChestSnapshots(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            snapshot.chests ??= new List<ChestSnapshotSave>();
            snapshot.chests.Clear();

            ChestInteractable[] chests = UnityEngine.Object.FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
            {
                ChestInteractable chest = chests[i];
                if (chest != null && chest.TryBuildSnapshot(out ChestSnapshotSave chestSnapshot))
                    snapshot.chests.Add(chestSnapshot);
            }
        }

        private static void CaptureShopSnapshots(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            snapshot.shops ??= new List<ShopSnapshotSave>();
            snapshot.shops.Clear();

            HutaoShopInteractable[] shops = UnityEngine.Object.FindObjectsByType<HutaoShopInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < shops.Length; i++)
            {
                HutaoShopInteractable shop = shops[i];
                if (shop != null && shop.TryBuildSnapshot(out ShopSnapshotSave shopSnapshot))
                    snapshot.shops.Add(shopSnapshot);
            }
        }

        private static void CaptureGroundDropSnapshots(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            snapshot.groundDrops ??= new List<GroundDropSave>();
            snapshot.groundDrops.Clear();

            DroppedPickupRuntime[] drops = UnityEngine.Object.FindObjectsByType<DroppedPickupRuntime>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < drops.Length; i++)
            {
                DroppedPickupRuntime drop = drops[i];
                if (drop != null && drop.TryBuildSave(out GroundDropSave groundDropSave))
                    snapshot.groundDrops.Add(groundDropSave);
            }
        }

        private static void CaptureSpawnerSnapshots(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            LevelEnemySpawner[] globalSpawners = UnityEngine.Object.FindObjectsByType<LevelEnemySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            LevelEnemySpawner globalSpawner = globalSpawners.Length > 0 ? globalSpawners[0] : null;
            if (globalSpawner != null && globalSpawner.TryBuildSnapshot(out GlobalSpawnerSnapshotSave globalSnapshot))
                snapshot.globalSpawner = globalSnapshot;
            else
                snapshot.globalSpawner = null;

            snapshot.localSpawners ??= new List<LocalSpawnerSnapshotSave>();
            snapshot.localSpawners.Clear();

            LevelLocalEnemySpawner[] localSpawners = UnityEngine.Object.FindObjectsByType<LevelLocalEnemySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < localSpawners.Length; i++)
            {
                LevelLocalEnemySpawner spawner = localSpawners[i];
                if (spawner != null && spawner.TryBuildSnapshot(out LocalSpawnerSnapshotSave localSnapshot))
                    snapshot.localSpawners.Add(localSnapshot);
            }
        }

        private static void CaptureLevelDirectorSnapshot(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            LevelDirector[] directors = UnityEngine.Object.FindObjectsByType<LevelDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            LevelDirector director = directors.Length > 0 ? directors[0] : null;
            if (director != null && director.TryBuildSnapshot(out LevelDirectorSnapshotSave directorSnapshot))
                snapshot.levelDirector = directorSnapshot;
            else
                snapshot.levelDirector = null;
        }

        private static void RestoreOpenedChests(LevelSnapshot snapshot)
        {
            if (snapshot?.openedChestIds == null || snapshot.openedChestIds.Count == 0)
                return;

            HashSet<string> openedChestIds = new HashSet<string>(snapshot.openedChestIds);
            ChestInteractable[] chests = UnityEngine.Object.FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
            {
                ChestInteractable chest = chests[i];
                if (chest != null && openedChestIds.Contains(chest.GetSnapshotId()))
                    chest.DestroyOnlineSnapshotOwner();
            }
        }

        private static void RestoreChestSnapshots(LevelSnapshot snapshot)
        {
            if (snapshot?.chests == null)
                return;

            HashSet<string> openedChestIds = snapshot.openedChestIds != null
                ? new HashSet<string>(snapshot.openedChestIds)
                : new HashSet<string>();
            Dictionary<string, ChestInteractable> currentBySnapshotId = new Dictionary<string, ChestInteractable>();
            ChestInteractable[] currentChests = UnityEngine.Object.FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < currentChests.Length; i++)
            {
                ChestInteractable chest = currentChests[i];
                if (chest == null)
                    continue;

                string snapshotId = chest.GetSnapshotId();
                if (!string.IsNullOrWhiteSpace(snapshotId))
                    currentBySnapshotId[snapshotId] = chest;
            }

            HashSet<string> snapshotIds = new HashSet<string>();
            for (int i = 0; i < snapshot.chests.Count; i++)
            {
                ChestSnapshotSave chestSnapshot = snapshot.chests[i];
                if (chestSnapshot == null || string.IsNullOrWhiteSpace(chestSnapshot.snapshotId) || openedChestIds.Contains(chestSnapshot.snapshotId))
                    continue;

                snapshotIds.Add(chestSnapshot.snapshotId);
                Vector3 position = new Vector3(chestSnapshot.x, chestSnapshot.y, chestSnapshot.z);
                Quaternion rotation = Quaternion.Euler(0f, chestSnapshot.yaw, 0f);
                if (currentBySnapshotId.TryGetValue(chestSnapshot.snapshotId, out ChestInteractable existingChest) && existingChest != null)
                {
                    existingChest.ApplyOnlineSnapshotPose(chestSnapshot.snapshotId, position, rotation);
                    OnlineDungeonSessionCoordinator.GetInstance()?.ApplyKnownOpenedOnlineChest(existingChest);
                    continue;
                }

                UpsertChestSnapshot(chestSnapshot);
            }

            for (int i = 0; i < currentChests.Length; i++)
            {
                ChestInteractable chest = currentChests[i];
                if (chest == null)
                    continue;

                string snapshotId = chest.GetSnapshotId();
                if (openedChestIds.Contains(snapshotId) || !snapshotIds.Contains(snapshotId))
                    chest.DestroyOnlineSnapshotOwner();
            }
        }

        private static void UpsertChestSnapshot(ChestSnapshotSave chestSnapshot)
        {
            if (chestSnapshot == null || string.IsNullOrWhiteSpace(chestSnapshot.snapshotId))
                return;

            Vector3 position = new Vector3(chestSnapshot.x, chestSnapshot.y, chestSnapshot.z);
            Quaternion rotation = Quaternion.Euler(0f, chestSnapshot.yaw, 0f);
            ChestInteractable[] currentChests = UnityEngine.Object.FindObjectsByType<ChestInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < currentChests.Length; i++)
            {
                ChestInteractable chest = currentChests[i];
                if (chest != null && string.Equals(chest.GetSnapshotId(), chestSnapshot.snapshotId, System.StringComparison.Ordinal))
                {
                    chest.ApplyOnlineSnapshotPose(chestSnapshot.snapshotId, position, rotation);
                    OnlineDungeonSessionCoordinator.GetInstance()?.ApplyKnownOpenedOnlineChest(chest);
                    return;
                }
            }

            SpawnChestFromSnapshot(chestSnapshot, position, rotation);
        }

        private static void SpawnChestFromSnapshot(ChestSnapshotSave chestSnapshot, Vector3 position, Quaternion rotation)
        {
            if (chestSnapshot == null || string.IsNullOrWhiteSpace(chestSnapshot.prefabId))
                return;

            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{chestSnapshot.prefabId.Trim()}");
            if (prefab == null)
            {
                Debug.LogWarning($"[LevelBootstrapper] 未找到宝箱快照预制体：{chestSnapshot.prefabId}");
                return;
            }

            Transform interactablesRoot = LevelRuntimeHierarchy.GetInteractablesRoot();
            GameObject instance = interactablesRoot != null
                ? UnityEngine.Object.Instantiate(prefab, position, rotation, interactablesRoot)
                : UnityEngine.Object.Instantiate(prefab, position, rotation);
            instance.name = prefab.name;
            ChestInteractable interactable = instance.GetComponentInChildren<ChestInteractable>(true);
            if (interactable == null)
                interactable = ChestInteractable.EnsureOn(instance);
            interactable?.ApplyOnlineSnapshotIdentity(chestSnapshot.snapshotId);
            OnlineDungeonSessionCoordinator.GetInstance()?.ApplyKnownOpenedOnlineChest(interactable);
        }

        private static void RestoreEnemySnapshots(LevelSnapshot snapshot)
        {
            if (snapshot == null || snapshot.runtimeSnapshotVersion <= 0)
                return;

            EnemyController[] currentEnemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < currentEnemies.Length; i++)
            {
                EnemyController enemy = currentEnemies[i];
                if (enemy == null)
                    continue;

                enemy.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(enemy.gameObject);
            }

            SanitizeEnemySnapshots(snapshot);
            EnemySpawnVariantCatalog catalog = EnemySpawnRuntime.BuildVariantCatalog();
            if (snapshot.enemies == null)
                return;

            for (int i = 0; i < snapshot.enemies.Count; i++)
                EnemySpawnRuntime.SpawnEnemyFromSnapshot(snapshot.enemies[i], catalog);
        }

        private void ApplyOnlineWorldSnapshotState(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            RestoreOpenedChests(snapshot);
            RestoreChestSnapshots(snapshot);
            ApplyOnlineEnemySnapshots(snapshot);
            RestoreSpawnerSnapshots(snapshot);
            RestoreLevelDirectorSnapshot(snapshot);
            RestoreShopSnapshots(snapshot);
        }

        private static void ApplyOnlineEnemySnapshots(LevelSnapshot snapshot)
        {
            if (snapshot == null || snapshot.runtimeSnapshotVersion <= 0)
                return;

            SanitizeEnemySnapshots(snapshot);
            Dictionary<string, EnemyController> currentByRuntimeId = new Dictionary<string, EnemyController>();
            EnemyController[] currentEnemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < currentEnemies.Length; i++)
            {
                EnemyController enemy = currentEnemies[i];
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.RuntimeId))
                    continue;

                currentByRuntimeId[enemy.RuntimeId] = enemy;
            }

            HashSet<string> snapshotRuntimeIds = new HashSet<string>();
            EnemySpawnVariantCatalog catalog = null;
            if (snapshot.enemies != null)
            {
                for (int i = 0; i < snapshot.enemies.Count; i++)
                {
                    EnemySnapshot enemySnapshot = snapshot.enemies[i];
                    if (enemySnapshot == null || string.IsNullOrWhiteSpace(enemySnapshot.runtimeId))
                        continue;

                    string runtimeId = enemySnapshot.runtimeId.Trim();
                    snapshotRuntimeIds.Add(runtimeId);
                    if (currentByRuntimeId.TryGetValue(runtimeId, out EnemyController existingEnemy) && existingEnemy != null)
                    {
                        existingEnemy.RestoreFromSnapshot(enemySnapshot);
                        existingEnemy.SetOnlineRemoteSimulationDisabled(true);
                        continue;
                    }

                    catalog ??= EnemySpawnRuntime.BuildVariantCatalog();
                    EnemyController spawnedEnemy = EnemySpawnRuntime.SpawnEnemyFromSnapshot(enemySnapshot, catalog);
                    spawnedEnemy?.SetOnlineRemoteSimulationDisabled(true);
                }
            }

            for (int i = 0; i < currentEnemies.Length; i++)
            {
                EnemyController enemy = currentEnemies[i];
                if (enemy == null || snapshotRuntimeIds.Contains(enemy.RuntimeId))
                    continue;

                enemy.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(enemy.gameObject);
            }
        }

        private static void RestoreSpawnerSnapshots(LevelSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            if (snapshot.globalSpawner != null)
            {
                LevelEnemySpawner[] globalSpawners = UnityEngine.Object.FindObjectsByType<LevelEnemySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < globalSpawners.Length; i++)
                    globalSpawners[i]?.RestoreSnapshot(snapshot.globalSpawner);
            }

            if (snapshot.localSpawners == null || snapshot.localSpawners.Count == 0)
                return;

            LevelLocalEnemySpawner[] localSpawners = UnityEngine.Object.FindObjectsByType<LevelLocalEnemySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < localSpawners.Length; i++)
            {
                LevelLocalEnemySpawner spawner = localSpawners[i];
                if (spawner == null)
                    continue;

                for (int j = 0; j < snapshot.localSpawners.Count; j++)
                    spawner.RestoreSnapshot(snapshot.localSpawners[j]);
            }
        }

        private static void RestoreLevelDirectorSnapshot(LevelSnapshot snapshot)
        {
            if (snapshot?.levelDirector == null)
                return;

            LevelDirector[] directors = UnityEngine.Object.FindObjectsByType<LevelDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < directors.Length; i++)
            {
                LevelDirector director = directors[i];
                if (director == null)
                    continue;

                director.RestoreSnapshot(snapshot.levelDirector);
                director.CompleteRuntimeSnapshotRestore();
            }
        }

        private static void SanitizeEnemySnapshots(LevelSnapshot snapshot)
        {
            if (snapshot?.enemies == null)
                return;

            bool isFinalBossDuelScene = FinalBossDuelSceneRuntime.IsFinalBossDuelScene();
            bool allowLevelBoss = isFinalBossDuelScene ||
                                  snapshot.levelDirector != null &&
                                  snapshot.levelDirector.bossSpawned &&
                                  !snapshot.levelDirector.bossDefeated;
            for (int i = 0; i < snapshot.enemies.Count; i++)
            {
                EnemySnapshot enemySnapshot = snapshot.enemies[i];
                if (enemySnapshot == null)
                    continue;

                if (isFinalBossDuelScene && enemySnapshot.enemyType == (int)EnemyType.Boss)
                {
                    enemySnapshot.countsAsLevelBoss = true;
                    continue;
                }

                if (enemySnapshot.enemyType != (int)EnemyType.Boss || !allowLevelBoss)
                    enemySnapshot.countsAsLevelBoss = false;
            }
        }

        private static void RestoreShopSnapshots(LevelSnapshot snapshot)
        {
            if (snapshot?.shops == null || snapshot.shops.Count == 0)
                return;

            Dictionary<string, ShopSnapshotSave> snapshotsById = new Dictionary<string, ShopSnapshotSave>();
            for (int i = 0; i < snapshot.shops.Count; i++)
            {
                ShopSnapshotSave shopSnapshot = snapshot.shops[i];
                if (shopSnapshot == null || string.IsNullOrWhiteSpace(shopSnapshot.shopId))
                    continue;

                snapshotsById[shopSnapshot.shopId] = shopSnapshot;
            }

            HutaoShopInteractable[] shops = UnityEngine.Object.FindObjectsByType<HutaoShopInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < shops.Length; i++)
            {
                HutaoShopInteractable shop = shops[i];
                if (shop == null)
                    continue;

                if (snapshotsById.TryGetValue(shop.GetSnapshotId(), out ShopSnapshotSave shopSnapshot))
                    shop.RestoreSnapshot(shopSnapshot);
            }
        }

        private static void RestoreGroundDrops(LevelSnapshot snapshot)
        {
            if (snapshot?.groundDrops == null || snapshot.groundDrops.Count == 0)
                return;

            for (int i = 0; i < snapshot.groundDrops.Count; i++)
                DroppedPickupRuntime.SpawnFromSave(snapshot.groundDrops[i]);
        }

        private static void RestoreGroundDropsForOnline(LevelSnapshot snapshot, ref string lastSnapshotKey)
        {
            string snapshotKey = BuildGroundDropsSnapshotKey(snapshot);
            if (lastSnapshotKey != null && string.Equals(snapshotKey, lastSnapshotKey, System.StringComparison.Ordinal))
                return;

            lastSnapshotKey = snapshotKey;
            DroppedPickupRuntime[] currentDrops = UnityEngine.Object.FindObjectsByType<DroppedPickupRuntime>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < currentDrops.Length; i++)
            {
                DroppedPickupRuntime drop = currentDrops[i];
                if (drop == null)
                    continue;

                drop.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(drop.gameObject);
            }

            RestoreGroundDrops(snapshot);
        }

        private static string BuildGroundDropsSnapshotKey(LevelSnapshot snapshot)
        {
            if (snapshot?.groundDrops == null || snapshot.groundDrops.Count == 0)
                return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < snapshot.groundDrops.Count; i++)
            {
                GroundDropSave drop = snapshot.groundDrops[i];
                if (drop == null)
                    continue;

                builder.Append(drop.itemType).Append('|')
                       .Append(drop.payload).Append('|')
                       .Append(drop.x).Append('|')
                       .Append(drop.y).Append('|')
                       .Append(drop.z).Append(';');
            }

            return builder.ToString();
        }
    }
}
