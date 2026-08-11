using System;
using System.Collections.Generic;
using Game.Data;
using Game.Domain;
using Game.Online;
using Game.Presentation;
using Game.Saving;
using UnityEngine;

namespace Game.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class RogueliteRegionFlowController : MonoBehaviour
    {
        [Serializable]
        public sealed class RegionDefinition
        {
            public string regionId = "";
            public string displayName = "";
            public Vector3 guardianCenter;
            [Min(1f)] public float guardianRadius = 55f;
            public Vector3 entryCenter;
            [Min(0.5f)] public float entryRadius = 12f;
            public List<LevelLocalEnemySpawner> spawners = new List<LevelLocalEnemySpawner>();
            public List<RoguelitePlacedGuardian> placedGuardians = new List<RoguelitePlacedGuardian>();
            public RogueliteSealGate exitGate;
            public bool isBossRegion;
            public Vector3 bossSpawnPoint;
        }

        [SerializeField] private string _flowId = "";
        [SerializeField] private List<RegionDefinition> _regions = new List<RegionDefinition>();
        [SerializeField] [Min(0.05f)] private float _guardianCheckInterval = 0.25f;

        private readonly HashSet<string> _enteredRegionIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _clearedRegionIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _buffClaimedRegionIds = new HashSet<string>(StringComparer.Ordinal);
        private string _currentRegionId = string.Empty;
        private string _pendingBuffRegionId = string.Empty;
        private Transform _localPlayerTransform;
        private float _guardianCheckTimer;
        private float _buffRetryTime;
        private bool _buffSelectionOpen;
        private string _armedExitRegionId = string.Empty;

        public bool AllRegionsCleared => _regions.Count > 0 && _clearedRegionIds.Count >= _regions.Count;

        public bool IsBossRegionReady
        {
            get
            {
                RegionDefinition currentRegion = FindRegion(_currentRegionId);
                return currentRegion != null
                    && currentRegion.isBossRegion
                    && _enteredRegionIds.Contains(currentRegion.regionId)
                    && _buffClaimedRegionIds.Contains(currentRegion.regionId);
            }
        }

        public bool TryGetBossSpawnPoint(out Vector3 bossSpawnPoint)
        {
            RegionDefinition currentRegion = FindRegion(_currentRegionId);
            if (currentRegion != null
                && currentRegion.isBossRegion
                && _enteredRegionIds.Contains(currentRegion.regionId)
                && _buffClaimedRegionIds.Contains(currentRegion.regionId))
            {
                bossSpawnPoint = currentRegion.bossSpawnPoint;
                return true;
            }

            bossSpawnPoint = default;
            return false;
        }

        private void Start()
        {
            RogueliteRegionFlowSnapshotSave snapshot = GameStateMachine.GetInstance()?.CurrentRun?.levelSnapshot?.regionFlow;
            if (snapshot != null && string.Equals(snapshot.flowId, GetSnapshotId(), StringComparison.Ordinal))
                RestoreSnapshot(snapshot);
            else
                StartFreshFlow();
        }

        private void Update()
        {
            if (_regions == null || _regions.Count == 0)
                return;

            RegionDefinition currentRegion = FindRegion(_currentRegionId);
            if (currentRegion == null)
                return;

            if (_enteredRegionIds.Contains(currentRegion.regionId)
                && !_buffClaimedRegionIds.Contains(currentRegion.regionId))
            {
                TryShowRegionBuff(currentRegion);
                return;
            }

            if (!OnlineDungeonSessionCoordinator.GetInstance().ShouldDriveOnlineWorldSimulation())
                return;

            if (currentRegion.isBossRegion)
                return;

            if (!_clearedRegionIds.Contains(currentRegion.regionId))
            {
                _guardianCheckTimer -= Time.deltaTime;
                if (_guardianCheckTimer <= 0f)
                {
                    _guardianCheckTimer = Mathf.Max(0.05f, _guardianCheckInterval);
                    if (IsRegionGuardianWaveCleared(currentRegion))
                        CompleteRegion(currentRegion);
                }
                return;
            }

            int currentIndex = GetRegionIndex(currentRegion.regionId);
            if (currentIndex < 0 || currentIndex >= _regions.Count - 1)
                return;

            RegionDefinition nextRegion = _regions[currentIndex + 1];
            Transform playerTransform = ResolveLocalPlayerTransform();
            if (playerTransform == null)
                return;

            TryEnterRegionAfterCrossingGate(currentRegion, nextRegion, playerTransform.position);
        }

        public void Configure(string flowId, List<RegionDefinition> regions)
        {
            _flowId = string.IsNullOrWhiteSpace(flowId) ? string.Empty : flowId.Trim();
            _regions = regions ?? new List<RegionDefinition>();
        }

        public string GetSnapshotId()
        {
            return string.IsNullOrWhiteSpace(_flowId) ? string.Empty : _flowId.Trim();
        }

        public bool TryBuildSnapshot(out RogueliteRegionFlowSnapshotSave snapshot)
        {
            snapshot = null;
            string flowId = GetSnapshotId();
            if (string.IsNullOrWhiteSpace(flowId))
                return false;

            snapshot = new RogueliteRegionFlowSnapshotSave
            {
                flowId = flowId,
                currentRegionId = _currentRegionId,
                enteredRegionIds = BuildOrderedRegionIdList(_enteredRegionIds),
                clearedRegionIds = BuildOrderedRegionIdList(_clearedRegionIds),
                buffClaimedRegionIds = BuildOrderedRegionIdList(_buffClaimedRegionIds),
            };
            return true;
        }

        public void RestoreSnapshot(RogueliteRegionFlowSnapshotSave snapshot)
        {
            if (snapshot == null || !string.Equals(snapshot.flowId, GetSnapshotId(), StringComparison.Ordinal))
                return;

            ReplaceRegionIdSet(_enteredRegionIds, snapshot.enteredRegionIds);
            ReplaceRegionIdSet(_clearedRegionIds, snapshot.clearedRegionIds);
            ReplaceRegionIdSet(_buffClaimedRegionIds, snapshot.buffClaimedRegionIds);
            _currentRegionId = FindRegion(snapshot.currentRegionId) != null
                ? snapshot.currentRegionId
                : string.Empty;

            ApplyRegionWorldState();
            RegionDefinition currentRegion = FindRegion(_currentRegionId);
            if (currentRegion != null
                && _enteredRegionIds.Contains(currentRegion.regionId)
                && !_buffClaimedRegionIds.Contains(currentRegion.regionId))
            {
                _buffRetryTime = 0f;
            }
        }

        private void StartFreshFlow()
        {
            _enteredRegionIds.Clear();
            _clearedRegionIds.Clear();
            _buffClaimedRegionIds.Clear();
            _currentRegionId = string.Empty;
            if (_regions == null || _regions.Count == 0)
                return;

            RegionDefinition firstRegion = _regions[0];
            _currentRegionId = firstRegion.regionId;
            _enteredRegionIds.Add(firstRegion.regionId);
            ApplyRegionWorldState();
            PersistProgress();
            TryShowRegionBuff(firstRegion);
        }

        private void ApplyRegionWorldState()
        {
            for (int i = 0; i < _regions.Count; i++)
            {
                RegionDefinition region = _regions[i];
                if (region == null || string.IsNullOrWhiteSpace(region.regionId))
                    continue;

                if (region.exitGate != null)
                {
                    bool nextRegionEntered = i + 1 < _regions.Count
                        && _regions[i + 1] != null
                        && _enteredRegionIds.Contains(_regions[i + 1].regionId);
                    region.exitGate.SetSealed(
                        !_clearedRegionIds.Contains(region.regionId) || nextRegionEntered);
                }

                bool shouldActivate = _buffClaimedRegionIds.Contains(region.regionId);
                if (shouldActivate)
                    ActivateRegionSpawners(region);
            }
        }

        private bool IsRegionGuardianWaveCleared(RegionDefinition region)
        {
            if (region?.placedGuardians != null)
            {
                for (int i = 0; i < region.placedGuardians.Count; i++)
                {
                    RoguelitePlacedGuardian guardian = region.placedGuardians[i];
                    if (guardian != null && guardian.IsAlive)
                        return false;
                }
            }

            if (region?.spawners != null)
            {
                for (int i = 0; i < region.spawners.Count; i++)
                {
                    LevelLocalEnemySpawner spawner = region.spawners[i];
                    if (spawner == null)
                        continue;

                    int plannedGuardians = spawner.GetPlannedGuardianCount();
                    if (plannedGuardians > 0 && (!spawner.IsActivated || !spawner.HasStartedInitialTask))
                        return false;

                    int pendingGuardians = spawner.GetPendingGuardianCount();
                    if (pendingGuardians != 0)
                        return false;
                }
            }

            float guardianRadius = Mathf.Max(1f, region.guardianRadius);
            float guardianRadiusSqr = guardianRadius * guardianRadius;
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy != null
                    && enemy.IsAlive
                    && enemy.EnemyCategory == EnemyType.Guardian
                    && (enemy.transform.position - region.guardianCenter).sqrMagnitude <= guardianRadiusSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private void CompleteRegion(RegionDefinition region)
        {
            if (region == null || !_clearedRegionIds.Add(region.regionId))
                return;

            region.exitGate?.SetSealed(false);
            Debug.Log($"[RogueliteRegionFlow] 区域守卫已清空：{region.displayName}", this);
            PersistProgress();
        }

        private void EnterRegion(RegionDefinition region)
        {
            if (region == null || string.IsNullOrWhiteSpace(region.regionId))
                return;

            _currentRegionId = region.regionId;
            _armedExitRegionId = string.Empty;
            _enteredRegionIds.Add(region.regionId);
            ApplyRegionWorldState();
            PersistProgress();
            TryShowRegionBuff(region);
        }

        private void TryEnterRegionAfterCrossingGate(
            RegionDefinition currentRegion,
            RegionDefinition nextRegion,
            Vector3 playerPosition)
        {
            if (currentRegion?.exitGate == null || nextRegion == null)
                return;

            Transform gateTransform = currentRegion.exitGate.transform;
            Vector3 passageDirection = nextRegion.entryCenter - gateTransform.position;
            passageDirection.y = 0f;
            if (passageDirection.sqrMagnitude <= 0.01f)
                return;

            passageDirection.Normalize();
            Vector3 gateToPlayer = playerPosition - gateTransform.position;
            gateToPlayer.y = 0f;
            float signedDistance = Vector3.Dot(gateToPlayer, passageDirection);

            if (!string.Equals(_armedExitRegionId, currentRegion.regionId, StringComparison.Ordinal))
            {
                if (signedDistance <= -0.4f)
                    _armedExitRegionId = currentRegion.regionId;
                return;
            }

            if (signedDistance < 2.25f)
                return;

            currentRegion.exitGate.SetSealed(true);
            EnterRegion(nextRegion);
        }

        private void TryShowRegionBuff(RegionDefinition region)
        {
            if (region == null
                || _buffClaimedRegionIds.Contains(region.regionId)
                || _buffSelectionOpen
                || Time.unscaledTime < _buffRetryTime)
            {
                return;
            }

            _pendingBuffRegionId = region.regionId;
            _buffSelectionOpen = true;
            if (!BuffSelectionService.ShowThreeChoices(OnRegionBuffSelected))
            {
                _buffSelectionOpen = false;
                _pendingBuffRegionId = string.Empty;
                _buffRetryTime = Time.unscaledTime + 1f;
            }
        }

        private void OnRegionBuffSelected(string buffId)
        {
            RegionDefinition region = FindRegion(_pendingBuffRegionId);
            _buffSelectionOpen = false;
            _pendingBuffRegionId = string.Empty;
            if (region == null || string.IsNullOrWhiteSpace(buffId))
                return;

            GameStateMachine gameStateMachine = GameStateMachine.GetInstance();
            PlayerModel player = gameStateMachine?.Player;
            RunData run = gameStateMachine?.CurrentRun;
            if (player == null || run == null)
                return;

            var buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            StatModifier modifier = buffConfig?.GetModifierForBuff(buffId);
            player.AddBuff(buffId, modifier);
            _buffClaimedRegionIds.Add(region.regionId);
            ActivateRegionCombat(region);
            player.SaveTo(run);
            PersistProgress();
            Debug.Log($"[RogueliteRegionFlow] 进入 {region.displayName}，选择 Buff：{buffId}", this);
        }

        private void ActivateRegionSpawners(RegionDefinition region)
        {
            if (region?.spawners == null)
                return;

            for (int i = 0; i < region.spawners.Count; i++)
                region.spawners[i]?.ActivateSpawner();
        }

        private void ActivateRegionCombat(RegionDefinition region)
        {
            ActivateRegionSpawners(region);
            if (region?.placedGuardians == null)
                return;

            for (int i = 0; i < region.placedGuardians.Count; i++)
                region.placedGuardians[i]?.ActivateForRegion();
        }

        private void PersistProgress()
        {
            GameStateMachine gameStateMachine = GameStateMachine.GetInstance();
            RunData run = gameStateMachine?.CurrentRun;
            if (run == null)
                return;

            run.levelSnapshot ??= new LevelSnapshot();
            if (TryBuildSnapshot(out RogueliteRegionFlowSnapshotSave snapshot))
                run.levelSnapshot.regionFlow = snapshot;
            gameStateMachine.SaveCurrent();
        }

        private Transform ResolveLocalPlayerTransform()
        {
            _localPlayerTransform = LocalPlayerInteractionResolver.ResolvePlayerTransform(gameObject.scene, _localPlayerTransform);
            return _localPlayerTransform;
        }

        private RegionDefinition FindRegion(string regionId)
        {
            if (_regions == null || string.IsNullOrWhiteSpace(regionId))
                return null;

            for (int i = 0; i < _regions.Count; i++)
            {
                RegionDefinition region = _regions[i];
                if (region != null && string.Equals(region.regionId, regionId, StringComparison.Ordinal))
                    return region;
            }

            return null;
        }

        private int GetRegionIndex(string regionId)
        {
            if (_regions == null || string.IsNullOrWhiteSpace(regionId))
                return -1;

            for (int i = 0; i < _regions.Count; i++)
            {
                if (_regions[i] != null && string.Equals(_regions[i].regionId, regionId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private List<string> BuildOrderedRegionIdList(HashSet<string> source)
        {
            var result = new List<string>();
            for (int i = 0; i < _regions.Count; i++)
            {
                string regionId = _regions[i]?.regionId;
                if (!string.IsNullOrWhiteSpace(regionId) && source.Contains(regionId))
                    result.Add(regionId);
            }
            return result;
        }

        private void ReplaceRegionIdSet(HashSet<string> target, List<string> source)
        {
            target.Clear();
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                string regionId = source[i];
                if (FindRegion(regionId) != null)
                    target.Add(regionId);
            }
        }
    }
}
