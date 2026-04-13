using System;
using UnityEngine;
using UnityEngine.AI;
using Game.Data;
using Game.GameFlow;

namespace Game.Presentation
{
    /// <summary>
    /// Enemy perception based on sector detection, LOS/path probing, and target velocity estimation.
    /// </summary>
    public class EnemyPerception : MonoBehaviour
    {
        private const float CurrentTargetStickinessMultiplier = 0.82f;
        private const float SkillRangeTolerance = 0.25f;
        private const float DefaultLosProbeInterval = 0.1f;
        private const float DefaultPathProbeInterval = 0.3f;
        private const int DefaultObstacleMask = Physics.DefaultRaycastLayers;
        private const float DefaultPerceptionEyeHeight = 1.6f;

        private Transform _mainPlayer;
        private Transform _player;
        private PlayerController _playerController;
        private PlayerCloneActor _targetCloneActor;
        private EnemyController _controller;

        private bool _hasLineOfSight;
        private bool _hasReachablePath;
        private float _nextLosProbeTime;
        private float _nextPathProbeTime;
        private Vector3 _lastPlayerSamplePosition;
        private float _lastPlayerSampleTime;
        private bool _hasLastPlayerSample;
        private NavMeshPath _pathBuffer;

        public bool HasTarget { get; private set; }
        public Transform CurrentTarget => HasTarget ? _player : null;
        public Vector3? TargetPosition { get; private set; }
        public float DistanceToPlayer { get; private set; }
        public bool IsPlayerInSector { get; private set; }
        public bool IsPlayerBeyondChaseBreak { get; private set; }
        public bool HasLineOfSight => HasTarget && _hasLineOfSight;
        public bool HasReachablePath => HasTarget && _hasReachablePath;
        public Vector3 TargetVelocity { get; private set; }
        public Vector3 LastKnownTargetPosition { get; private set; }
        public float LastSeenTargetTime { get; private set; } = -1f;
        public float TimeSinceLastSeen => LastSeenTargetTime >= 0f ? Time.time - LastSeenTargetTime : float.MaxValue;
        public GameAction ObservedPlayerAction { get; private set; }
        public float ObservedPlayerStateRemainingTime { get; private set; }
        public float ObservedPlayerStateNormalizedProgress { get; private set; }
        public float ThreatScore { get; private set; }
        public bool HasImmediateThreat { get; private set; }
        public bool IsPlayerLikelyRecovering { get; private set; }
        public float PunishOpportunityScore { get; private set; }
        public bool HasPunishOpportunity => PunishOpportunityScore > 0.2f;

        public bool IsInSkillRange(int slot)
        {
            if (_controller == null)
                _controller = GetComponent<EnemyController>();
            if (_controller == null || !HasTarget) return false;

            _controller.EnsureInitialized();
            EnemyResolvedSkill skill = _controller.ResolveSkillSlot(slot);
            if (skill == null)
                return false;

            float range = skill.CastRange;
            float minRange = Mathf.Max(0f, skill.MinCastRange);
            return range > 0.01f &&
                   DistanceToPlayer + SkillRangeTolerance >= minRange &&
                   DistanceToPlayer <= range + SkillRangeTolerance;
        }

        public Vector3 PredictTargetPosition(float leadTime)
        {
            if (!TargetPosition.HasValue)
                return transform.position;

            Vector3 predicted = TargetPosition.Value + TargetVelocity * Mathf.Max(0f, leadTime);
            predicted.y = TargetPosition.Value.y;
            return predicted;
        }

        public bool HasLineOfSightFrom(Vector3 from, Vector3 to)
        {
            EnemyArchetypeSO archetype = _controller != null ? _controller.Archetype : null;
            return ProbeLineOfSight(from, to, archetype);
        }

        public bool HasRecentTargetMemory(float memoryDuration)
        {
            return LastSeenTargetTime >= 0f &&
                   Time.time - LastSeenTargetTime <= Mathf.Max(0f, memoryDuration);
        }

        private void Awake()
        {
            _pathBuffer = new NavMeshPath();
        }

        private void Start()
        {
            _controller = GetComponent<EnemyController>();
            _controller?.EnsureInitialized();
            RefreshPlayerRef();
        }

        private void RefreshPlayerRef()
        {
            var gsm = GameStateMachine.GetInstance();
            if (gsm != null && gsm.LevelPlayerTransform != null)
                _mainPlayer = gsm.LevelPlayerTransform;
            if (_mainPlayer == null)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    _mainPlayer = player.transform;
                    _playerController = player;
                }
            }

            if (_playerController == null && _mainPlayer != null)
                _playerController = _mainPlayer.GetComponent<PlayerController>();
        }

        public void Tick()
        {
            if (_controller == null)
                _controller = GetComponent<EnemyController>();
            if (_controller == null || !_controller.IsAlive)
            {
                ClearTargetState();
                return;
            }

            _controller.EnsureInitialized();
            EnemyArchetypeSO archetype = _controller.Archetype;
            if (archetype == null)
            {
                ClearTargetState();
                return;
            }

            if (!TryResolveBestTarget(out Transform targetTransform, out PlayerController targetPlayerController, out PlayerCloneActor targetCloneActor))
            {
                ClearTargetState();
                return;
            }

            bool targetChanged = targetTransform != _player;
            _player = targetTransform;
            _playerController = targetPlayerController;
            _targetCloneActor = targetCloneActor;
            if (targetChanged)
            {
                _hasLastPlayerSample = false;
                _nextLosProbeTime = 0f;
                _nextPathProbeTime = 0f;
            }

            Vector3 playerPos = _player.position;
            Vector3 toPlayer = playerPos - transform.position;
            toPlayer.y = 0f;
            DistanceToPlayer = toPlayer.magnitude;

            float sectorRange = archetype.sectorRange;
            float chaseBreakDistance = archetype.chaseBreakDistance;
            float sectorAngle = archetype.sectorAngle;

            bool inRange = DistanceToPlayer <= sectorRange;
            Vector3 forward = transform.forward;
            if (toPlayer.sqrMagnitude < 0.0001f)
                toPlayer = forward;
            Vector3 toPlayerDir = toPlayer.normalized;
            float minDot = Mathf.Cos(sectorAngle * 0.5f * Mathf.Deg2Rad);
            bool inSector = inRange && Vector3.Dot(forward, toPlayerDir) >= minDot;

            IsPlayerInSector = inSector;
            IsPlayerBeyondChaseBreak = DistanceToPlayer > chaseBreakDistance;

            if (HasTarget)
            {
                if (IsPlayerBeyondChaseBreak)
                {
                    ClearTargetState();
                    return;
                }

                TargetPosition = playerPos;
            }
            else if (inSector)
            {
                HasTarget = true;
                TargetPosition = playerPos;
            }

            UpdateTargetVelocity(playerPos);

            float now = Time.time;
            bool shouldProbeTarget = HasTarget || inSector || HasRecentTargetMemory(archetype.searchMemoryDuration);
            if (shouldProbeTarget && now >= _nextLosProbeTime)
            {
                _hasLineOfSight = ProbeLineOfSight(transform.position, playerPos, archetype);
                _nextLosProbeTime = now + DefaultLosProbeInterval;
            }
            else if (!shouldProbeTarget)
            {
                _hasLineOfSight = false;
            }

            if (shouldProbeTarget && now >= _nextPathProbeTime)
            {
                _hasReachablePath = ProbePathToTarget(playerPos);
                _nextPathProbeTime = now + DefaultPathProbeInterval;
            }
            else if (!shouldProbeTarget)
            {
                _hasReachablePath = false;
            }

            UpdateTargetAwareness(playerPos, inSector, archetype, now);
            UpdatePlayerCombatObservation(archetype);
        }

        private void UpdateTargetVelocity(Vector3 playerPos)
        {
            if (_hasLastPlayerSample)
            {
                float dt = Time.time - _lastPlayerSampleTime;
                if (dt > 0.0001f)
                {
                    Vector3 velocity = (playerPos - _lastPlayerSamplePosition) / dt;
                    velocity.y = 0f;
                    TargetVelocity = velocity;
                }
                else
                {
                    TargetVelocity = Vector3.zero;
                }
            }
            else
            {
                TargetVelocity = Vector3.zero;
                _hasLastPlayerSample = true;
            }

            _lastPlayerSamplePosition = playerPos;
            _lastPlayerSampleTime = Time.time;
        }

        private void UpdateTargetAwareness(Vector3 playerPos, bool inSector, EnemyArchetypeSO archetype, float now)
        {
            bool canSeePlayer = inSector || _hasLineOfSight;
            if (canSeePlayer)
            {
                HasTarget = true;
                TargetPosition = playerPos;
                LastKnownTargetPosition = playerPos;
                LastSeenTargetTime = now;
                return;
            }

            float loseTrackDelay = Mathf.Clamp(archetype.searchMemoryDuration * 0.35f, 0.25f, 0.6f);
            if (HasTarget && TimeSinceLastSeen <= loseTrackDelay)
            {
                TargetPosition = LastKnownTargetPosition;
                return;
            }

            HasTarget = false;
            TargetPosition = null;
        }

        private void UpdatePlayerCombatObservation(EnemyArchetypeSO archetype)
        {
            if (_targetCloneActor == null && _playerController == null && _player != null)
                _playerController = _player.GetComponent<PlayerController>();

            if (TryApplyObservedCombatObservation(_targetCloneActor, archetype))
                return;

            if (TryApplyObservedCombatObservation(_playerController, archetype))
                return;

            ClearPlayerCombatObservation();
        }

        private bool TryApplyObservedCombatObservation(ICombatActionReadable source, EnemyArchetypeSO archetype)
        {
            if (source == null)
                return false;

            ApplyObservedCombatObservation(CombatActionObservation.From(source), archetype);
            return true;
        }

        private void ApplyObservedCombatObservation(CombatActionObservation observation, EnemyArchetypeSO archetype)
        {
            ObservedPlayerAction = observation.Action;
            ObservedPlayerStateRemainingTime = Mathf.Max(0f, observation.RemainingTime);
            ObservedPlayerStateNormalizedProgress = observation.NormalizedProgress >= 0f
                ? Mathf.Clamp01(observation.NormalizedProgress)
                : 0f;

            float threatWeight = ResolveActionThreatWeight(ObservedPlayerAction);
            float progressWeight = ResolveActionThreatProgress(
                ObservedPlayerAction,
                ObservedPlayerStateNormalizedProgress,
                ObservedPlayerStateRemainingTime);
            float threatRange = Mathf.Max(2.25f, archetype.GetChaseInnerDistance() + 1.25f);
            float rangeWeight = 1f - Mathf.Clamp01(DistanceToPlayer / threatRange);

            ThreatScore = threatWeight * progressWeight * rangeWeight;
            HasImmediateThreat = HasTarget && ThreatScore >= 0.22f;

            bool isAttackLike = threatWeight > 0.01f;
            IsPlayerLikelyRecovering = isAttackLike &&
                                       ObservedPlayerStateNormalizedProgress >= 0.58f &&
                                       ObservedPlayerStateRemainingTime <= 0.6f;

            if (HasTarget && HasLineOfSight && IsPlayerLikelyRecovering)
                PunishOpportunityScore = (0.35f + ObservedPlayerStateNormalizedProgress * 0.65f) * rangeWeight;
            else
                PunishOpportunityScore = 0f;
        }

        private void ClearPlayerCombatObservation()
        {
            ObservedPlayerAction = GameAction.None;
            ObservedPlayerStateRemainingTime = 0f;
            ObservedPlayerStateNormalizedProgress = 0f;
            ThreatScore = 0f;
            HasImmediateThreat = false;
            IsPlayerLikelyRecovering = false;
            PunishOpportunityScore = 0f;
        }

        private static float ResolveActionThreatWeight(GameAction action)
        {
            return action switch
            {
                GameAction.NormalAttack => 0.7f,
                GameAction.Shoot => 0.7f,
                GameAction.Skill => 1f,
                GameAction.AirAttack => 0.8f,
                GameAction.FallAttack => 0.95f,
                GameAction.ChargeRelease => 0.9f,
                GameAction.ChargeStart => 0.45f,
                GameAction.ShootCharge => 0.9f,
                _ => 0f,
            };
        }

        private static float ResolveActionThreatProgress(GameAction action, float progress, float remainingTime)
        {
            if (action == GameAction.None)
                return 0f;

            if (progress <= 0f && remainingTime <= 0f)
                return 0f;

            if (progress < 0.12f)
                return 0.35f;
            if (progress < 0.68f)
                return 1f;
            if (remainingTime > 0.55f)
                return 0.55f;
            return 0.35f;
        }

        private bool ProbeLineOfSight(Vector3 from, Vector3 to, EnemyArchetypeSO archetype)
        {
            Vector3 origin = WithEyeHeight(from, archetype);
            Vector3 destination = WithEyeHeight(to, archetype);

            Vector3 dir = destination - origin;
            float distance = dir.magnitude;
            if (distance <= 0.05f)
                return true;

            int obstacleMask = DefaultObstacleMask;
            if (obstacleMask == 0)
                obstacleMask = DefaultObstacleMask;

            RaycastHit[] hits = Physics.RaycastAll(origin, dir / distance, distance, obstacleMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return true;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == null)
                    continue;
                if (hitTransform.IsChildOf(transform))
                    continue;
                if (_player != null && hitTransform.IsChildOf(_player))
                    return true;

                return false;
            }

            return true;
        }

        private bool ProbePathToTarget(Vector3 targetPos)
        {
            if (_pathBuffer == null)
                _pathBuffer = new NavMeshPath();

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 2f, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(targetPos, out NavMeshHit endHit, 2f, NavMesh.AllAreas))
                return false;

            bool hasPath = NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, _pathBuffer);
            return hasPath && _pathBuffer.status == NavMeshPathStatus.PathComplete;
        }

        private static Vector3 WithEyeHeight(Vector3 position, EnemyArchetypeSO archetype)
        {
            float height = archetype != null
                ? Mathf.Max(0f, archetype.perceptionEyeHeight)
                : DefaultPerceptionEyeHeight;
            return position + Vector3.up * height;
        }

        private void ClearTargetState()
        {
            HasTarget = false;
            _player = null;
            _playerController = null;
            _targetCloneActor = null;
            TargetPosition = null;
            DistanceToPlayer = float.MaxValue;
            IsPlayerInSector = false;
            IsPlayerBeyondChaseBreak = true;
            _hasLineOfSight = false;
            _hasReachablePath = false;
            TargetVelocity = Vector3.zero;
            _hasLastPlayerSample = false;
            _nextLosProbeTime = 0f;
            _nextPathProbeTime = 0f;
            LastKnownTargetPosition = Vector3.zero;
            LastSeenTargetTime = -1f;
            ClearPlayerCombatObservation();
        }

        private bool TryResolveBestTarget(out Transform targetTransform, out PlayerController targetPlayerController, out PlayerCloneActor targetCloneActor)
        {
            targetTransform = null;
            targetPlayerController = null;
            targetCloneActor = null;

            float bestScore = float.MaxValue;
            RefreshPlayerRef();
            if (_playerController != null && _playerController.gameObject.activeInHierarchy && !_playerController.IsDead)
            {
                ConsiderTarget(
                    _playerController.transform,
                    _playerController,
                    null,
                    ref targetTransform,
                    ref targetPlayerController,
                    ref targetCloneActor,
                    ref bestScore);
            }

            var clones = PlayerCloneActor.ActiveClones;
            for (int i = 0; i < clones.Count; i++)
            {
                PlayerCloneActor clone = clones[i];
                if (clone == null || !clone.gameObject.activeInHierarchy || !clone.IsAlive)
                    continue;

                ConsiderTarget(
                    clone.transform,
                    null,
                    clone,
                    ref targetTransform,
                    ref targetPlayerController,
                    ref targetCloneActor,
                    ref bestScore);
            }

            return targetTransform != null;
        }

        private void ConsiderTarget(
            Transform candidateTransform,
            PlayerController candidatePlayerController,
            PlayerCloneActor candidateCloneActor,
            ref Transform targetTransform,
            ref PlayerController targetPlayerController,
            ref PlayerCloneActor targetCloneActor,
            ref float bestScore)
        {
            if (candidateTransform == null)
                return;

            float score = (candidateTransform.position - transform.position).sqrMagnitude;
            if (candidateTransform == _player)
                score *= CurrentTargetStickinessMultiplier;

            if (score >= bestScore)
                return;

            bestScore = score;
            targetTransform = candidateTransform;
            targetPlayerController = candidatePlayerController;
            targetCloneActor = candidateCloneActor;
        }
    }
}
