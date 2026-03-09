using System.Collections.Generic;
using UnityEngine;
using Game.Domain;
using Game.Presentation;
using Game.Data;
using Game.Saving;
using Game.UI;
using ProjectBase;

namespace Game.GameFlow
{
    /// <summary>
    /// Level entry orchestration for scene startup.
    /// Handles fallback debug startup, player placement, HUD setup, and buff selection.
    /// </summary>
    public class LevelBootstrapper : MonoBehaviour
    {
        [HideInInspector]
        public PlayerController playerController;

        [HideInInspector]
        public PlayerController playerPrefab;

        [HideInInspector]
        public Transform spawnPoint;

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

            if (run.levelSnapshot != null)
            {
                var snap = run.levelSnapshot;
                var rotation = Quaternion.Euler(0f, snap.playerYaw, 0f);
                PlacePlayer(new Vector3(snap.playerX, snap.playerY, snap.playerZ), rotation);
            }
            else
            {
                var rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
                PlacePlayer(spawnPoint != null ? spawnPoint.position : Vector3.zero, rotation);
            }

            if (run.levelSnapshot == null)
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
            int levelIndex = 1;
            var configDb = ConfigManager.GetInstance().GetLevelConfigDatabase();
            if (configDb != null)
            {
                var config = configDb.GetConfigForLevel(1);
                if (config != null)
                    levelIndex = config.levelIndex;
            }

            return SaveSystem.NewGameRun(levelIndex, 1).run;
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
                if (playerController == null)
                    Debug.LogWarning("[LevelBootstrapper] Scene PlayerController not found.");
            }

            if (!IsValidSceneObject(spawnPoint))
            {
                spawnPoint = FindSpawnPointInScene();
                if (spawnPoint == null)
                    Debug.LogWarning("[LevelBootstrapper] Spawn point not found. Fresh runs will fall back to Vector3.zero.");
            }
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
                spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
                spawnRotation = spawnPoint != null ? spawnPoint.rotation : prefab.transform.rotation;
            }

            playerController = Instantiate(prefab, spawnPosition, spawnRotation);
            playerController.name = prefab.name;
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
            var playerModel = gsm?.Player;
            if (playerModel == null) return;

            var config = ConfigManager.GetInstance()?.GetBuffConfig();
            var modifier = config != null ? config.GetModifierForBuff(buffId) : new StatModifier();
            playerModel.AddBuff(buffId, modifier);
        }
    }
}
