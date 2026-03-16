using System.Collections.Generic;
using UnityEngine;
using Game.Domain;
using Game.Presentation;
using Game.Data;
using Game.Saving;
using Game.UI;
using ProjectBase;
using UnityEngine.SceneManagement;

namespace Game.GameFlow
{
    /// <summary>
    /// Level entry orchestration for scene startup.
    /// Handles fallback debug startup, player placement, HUD setup, and buff selection.
    /// </summary>
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

        private void Start()
        {
            var gsm = GameStateMachine.GetInstance();
            var run = gsm?.CurrentRun;
            var playerModel = gsm?.Player;

            if (run == null || playerModel == null)
            {
                run = CreateFallbackRun();
                playerModel = new PlayerModel(ConfigManager.GetInstance().GetLevelGrowth());
                gsm?.AdoptRuntimeContext(run, playerModel);
                Debug.LogWarning(
                    "[LevelBootstrapper] No valid RunData was found. Created fallback debug data. " +
                    "Use the main menu flow for normal gameplay.");
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

            if (run.levelSnapshot != null)
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

            if (run.pendingBuffSelection)
                ShowBuffSelection();

            LevelUIModelLocator.Set(new LevelUIModel(gsm));

            var ui = UIManager.GetInstance();
            if (ui != null)
                ui.ShowPanel<PlayerInfoHUDPanel>(PanelNames.PlayerInfoHUD, PanelLayers.PlayerInfoHUD);

            DropdownScrollForwarder.EnsureExistsInScene();

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
                ui.HidePanel(PanelNames.PlayerInfoHUD);
        }

        private void HandlePlayerDied()
        {
            GameStateMachine.GetInstance()?.OnPlayerDied();
        }

        public void SaveAndQuit()
        {
            var gsm = GameStateMachine.GetInstance();
            var run = gsm?.CurrentRun;
            if (run != null && playerController != null)
            {
                var pos = playerController.transform.position;
                run.levelSnapshot ??= new LevelSnapshot();
                run.levelSnapshot.playerX = pos.x;
                run.levelSnapshot.playerY = pos.y;
                run.levelSnapshot.playerZ = pos.z;
                run.levelSnapshot.playerYaw = playerController.transform.eulerAngles.y;
            }

            gsm?.SaveAndQuit();
        }

        private RunData CreateFallbackRun()
        {
            int levelIndex = ResolveFallbackLevelIndex();
            var configDb = ConfigManager.GetInstance().GetLevelConfigDatabase();
            if (configDb != null)
            {
                var config = configDb.GetConfigForLevel(levelIndex);
                if (config != null)
                    levelIndex = config.levelIndex;
            }

            return SaveSystem.NewDebugRun(levelIndex, 1).run;
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
    }
}
