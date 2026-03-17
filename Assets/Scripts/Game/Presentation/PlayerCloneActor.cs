using StringComparer = System.StringComparer;
using System.Collections.Generic;
using Game;
using Game.Data;
using Game.Domain;
using Game.GameFlow;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    public sealed class PlayerCloneActor : MonoBehaviour, ICombatHardControlReceiver
    {
        private static readonly List<PlayerCloneActor> ActiveCloneActors = new List<PlayerCloneActor>();
        private enum CloneDecisionType
        {
            None,
            Dodge,
            Skill,
            Charge,
            Attack,
        }

        private enum CloneBufferedAction
        {
            None,
            Move,
            Attack,
        }

        private const float TargetRefreshInterval = 0.12f;
        private const float MaxTargetOwnerDistance = 22f;
        private const float PreferredAttackDistance = 2.25f;
        private const float AttackDistanceTolerance = 0.35f;
        private const float AttackCastRange = 2.7f;
        private const float SkillCastRange = 8f;
        private const float TooCloseDistance = 1.35f;
        private const float OrbitOffsetDistance = 1.9f;
        private const float OrbitArrivalDistance = 0.2f;
        private const float CombatMoveSpeedRatio = 0.96f;
        private const float NavMeshSampleRadius = 2.5f;
        private const float FollowPatrolArrivalDistance = 0.18f;
        private const float FollowPatrolAnchorTolerance = 1.6f;
        private const float WalkLocomotionSpeed = 0.5f;
        private const float RunLocomotionSpeed = 1f;
        private const float FinalBossAssistRadius = 48f;
        private const float MinActionExitValidationDelay = 0.05f;
        private const float AttackComboWindowDuration = PlayerStateMachine.ComboWindowDuration;
        private static readonly Color CloneTintColor = new Color(0.42f, 0.76f, 1f, 1f);
        private static readonly Color CloneEmissionColor = new Color(0.16f, 0.58f, 1f, 1f);
        private static readonly Color CloneHeadHealthBarColor = new Color(0.26f, 0.72f, 1f, 0.95f);
        private static readonly Vector3 CloneGlowOffset = new Vector3(0f, 1.35f, 0f);
        private const float CloneGlowMinIntensity = 0.85f;
        private const float CloneGlowMaxIntensity = 1.75f;
        private const float CloneGlowPulseSpeed = 2.2f;
        private const float CloneGlowRange = 4.8f;
        private const string CloneHudRootName = "PlayerCloneHUD";

        private sealed class CloneSkillExecutionContext : ISkillExecutionContext
        {
            private static readonly Collider[] SharedBuffer = new Collider[32];
            private readonly PlayerCloneActor _actor;

            public CloneSkillExecutionContext(PlayerCloneActor actor)
            {
                _actor = actor;
            }

            public Transform CasterTransform => _actor != null ? _actor.transform : null;
            public float CasterAttack => _actor != null ? _actor.CombatStats.Attack : 0f;
            public Collider[] OverlapBuffer => SharedBuffer;
        }

        private sealed class RuntimeModifier
        {
            public StatModifier modifier;
            public float endTime;
        }

        private readonly List<Material> _instancedMaterials = new List<Material>();
        private readonly Dictionary<int, float> _activeSkillCooldowns = new Dictionary<int, float>(4);
        private readonly List<int> _cooldownUpdateSlots = new List<int>(4);
        private readonly List<float> _cooldownUpdateValues = new List<float>(4);
        private readonly List<int> _cooldownExpiredSlots = new List<int>(4);
        private readonly List<RuntimeModifier> _runtimeModifiers = new List<RuntimeModifier>();
        private readonly List<SkillTimelineRunner> _detachedTimelineRunners = new List<SkillTimelineRunner>();
        private readonly CloneCombatMemory _combatMemory = new CloneCombatMemory();
        private NavMeshPath _navMeshPath;
        private readonly Stats _combatStats = new Stats();
        private Stats _ownerSharedStats;
        private EnemyController _target;
        private PlayerController _owner;
        private SummonCloneBuffSettings _settings;
        private Animator _animator;
        private PlayerAnimatorController _animatorController;
        private SkillTimelineRunner _timelineRunner;
        private PlayerCloneAIConfigSO _aiConfig;
        private NavMeshAgent _navMeshAgent;
        private Light _cloneGlowLight;
        private ActorHeadHealthBar _headHealthBar;
        private int _formationIndex;
        private int _formationCount = 1;
        private int _orbitSign = 1;
        private float _targetRefreshTimer;
        private float _actionLockTimer;
        private float _decisionTimer;
        private float _dodgeCooldownTimer;
        private float _chargeCooldownTimer;
        private float _dodgeMotionTimer;
        private float _hardControlTimer;
        private float _temporarySuperArmorTimer;
        private float _temporaryInvincibleTimer;
        private float _currentHp;
        private float _currentMp;
        private int _comboStage;
        private int _comboNextIndex = -1;
        private int _ownerSharedBuildSignature = int.MinValue;
        private string _activeActionId;
        private Vector3 _dodgeVelocity;
        private Vector3 _followPatrolTarget;
        private Vector3 _combatAnchorTarget;
        private Vector3 _combatAnchorReferenceTargetPosition;
        private float _followPatrolTimer;
        private float _combatAnchorRefreshTimer;
        private bool _hasFollowPatrolTarget;
        private bool _isInFollowLocomotion;
        private bool _hasCombatAnchorTarget;
        private bool _isInCombatLocomotion;
        private bool _combatAnchorThreatening;
        private CloneBufferedAction _bufferedAction;
        private int _bufferedAttackComboIndex = -1;
        private float _activeActionElapsed;
        private float _comboWindowTimer;

        public static IReadOnlyList<PlayerCloneActor> ActiveClones => ActiveCloneActors;
        public PlayerController Owner => _owner;
        public Stats CombatStats => _combatStats;
        public float MaxHp => _combatStats.MaxHp;
        public float CurrentHp => _currentHp;
        public float HeadHealthBarHeightOffset => _aiConfig != null ? Mathf.Max(0f, _aiConfig.headHealthBarHeightOffset) : 0f;
        public float CurrentHpRatio => MaxHp > 0f ? Mathf.Clamp01(_currentHp / MaxHp) : 0f;
        public float MaxMp => _combatStats.MaxMp;
        public float CurrentMp => _currentMp;
        public float Defense => _combatStats.Defense;
        public float DamageReduce => _combatStats.DamageReduce;
        public bool IsAlive => _owner != null && _currentHp > 0f;
        public GameAction CurrentActionId => ResolveCurrentGameAction();
        public float StateRemainingTime => ResolveStateRemainingTime();
        public float StateNormalizedProgress => ResolveStateNormalizedProgress();

        private void Awake()
        {
            _navMeshPath = new NavMeshPath();
            EnsureHeadHealthBar();
        }

        public void Initialize(PlayerController owner, SummonCloneBuffSettings settings, int formationIndex, int formationCount)
        {
            _owner = owner;
            _settings = settings;
            _aiConfig = ConfigManager.GetInstance()?.GetPlayerCloneAIConfig();
            _formationIndex = formationIndex;
            _formationCount = Mathf.Max(1, formationCount);
            _orbitSign = ResolveOrbitSign(formationIndex);
            _targetRefreshTimer = 0f;
            _decisionTimer = 0f;
            _dodgeCooldownTimer = 0f;
            _chargeCooldownTimer = 0f;
            _dodgeMotionTimer = 0f;
            _hardControlTimer = 0f;
            _temporarySuperArmorTimer = 0f;
            _temporaryInvincibleTimer = 0f;
            _actionLockTimer = 0f;
            _comboStage = 0;
            _comboNextIndex = -1;
            _activeActionId = string.Empty;
            _dodgeVelocity = Vector3.zero;
            _ownerSharedStats = null;
            _ownerSharedBuildSignature = int.MinValue;
            _followPatrolTarget = Vector3.zero;
            _followPatrolTimer = 0f;
            _combatAnchorTarget = Vector3.zero;
            _combatAnchorReferenceTargetPosition = Vector3.zero;
            _combatAnchorRefreshTimer = 0f;
            _hasFollowPatrolTarget = false;
            _isInFollowLocomotion = false;
            _hasCombatAnchorTarget = false;
            _isInCombatLocomotion = false;
            _combatAnchorThreatening = false;
            _bufferedAction = CloneBufferedAction.None;
            _bufferedAttackComboIndex = -1;
            _activeActionElapsed = 0f;
            _comboWindowTimer = 0f;
            _combatMemory.Clear();
            AssignCombatLayer();
            ConfigureNavigationSupport();
            RefreshDerivedStats(true);
            SnapToGround(owner != null ? owner.transform.position : transform.position);
            RebuildVisual();
            RefreshHeadHealthBarImmediate();
        }

        public void ApplySettings(SummonCloneBuffSettings settings)
        {
            if (Mathf.Approximately(_settings.followRadius, settings.followRadius)
                && Mathf.Approximately(_settings.opacity, settings.opacity))
            {
                return;
            }

            _settings = settings;
            RebuildVisual();
            RefreshHeadHealthBarImmediate();
        }

        public void SetFormation(int formationIndex, int formationCount)
        {
            _formationIndex = formationIndex;
            _formationCount = Mathf.Max(1, formationCount);
            _orbitSign = ResolveOrbitSign(formationIndex);
        }

        private void Update()
        {
            if (_owner == null || _owner.IsDead || !IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            float dt = Time.deltaTime;
            UpdateHeadHealthBar();
            if (!string.IsNullOrWhiteSpace(_activeActionId))
                _activeActionElapsed += dt;
            TickCloneGlowLight();
            TickComboWindow(dt);
            TickRuntimeModifiers();
            RefreshDerivedStats();
            TickAttributeRegeneration(dt);
            TickSkillCooldowns(dt);
            TickLocalCooldowns(dt);
            TickTemporaryCombatFlags(dt);
            TickDetachedTimelineRunners(dt);
            if (_actionLockTimer > 0f)
                _actionLockTimer = Mathf.Max(0f, _actionLockTimer - dt);

            UpdateTarget(dt);

            TickTimeline(dt);
            if (TickDodgeMotion(dt))
                return;

            if (_timelineRunner != null && !_timelineRunner.IsComplete)
            {
                MaintainActionFacing(dt);
                return;
            }

            if (_target == null)
            {
                if (ShouldInvestigateRecentTarget())
                {
                    TickInvestigate(dt);
                    return;
                }

                TickFollow(dt);
                return;
            }

            if (ShouldRegroupToOwner())
            {
                TickFollow(dt);
                return;
            }

            if (_hardControlTimer > 0f)
            {
                _animatorController?.SetGrounded(true);
                _animatorController?.SetLocomotionSpeed(0f);
                return;
            }

            if (_actionLockTimer > 0f || _decisionTimer > 0f)
            {
                TickCombatMove(dt);
                return;
            }

            ResetDecisionTimer();
            if (EvaluateAndExecuteCombatDecision())
                return;

            TickCombatMove(dt);
        }

        private void TickFollow(float dt)
        {
            if (_owner == null)
                return;

            ClearCombatAnchorTarget();
            ExitCombatLocomotion();
            EnterFollowLocomotion();
            Vector3 center = _owner.transform.position;
            float angleStep = 360f / Mathf.Max(1, _formationCount);
            float angle = angleStep * _formationIndex;
            Vector3 anchorOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.right * _settings.followRadius;
            Vector3 followAnchor = center + anchorOffset;
            followAnchor.y = center.y;

            Vector3 targetPosition = ResolveFollowPatrolTarget(followAnchor, dt);
            Vector3 moveTarget = ResolveSteeringTarget(targetPosition, true);
            Vector3 delta = moveTarget - transform.position;
            delta.y = 0f;
            bool shouldMove = delta.sqrMagnitude > FollowPatrolArrivalDistance * FollowPatrolArrivalDistance;
            float patrolMoveSpeedRatio = _aiConfig != null ? Mathf.Max(0.1f, _aiConfig.patrolMoveSpeedRatio) : 0.58f;
            float moveSpeed = Mathf.Max(1f, _combatStats.MoveSpeed) * patrolMoveSpeedRatio;
            if (shouldMove)
                transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * dt);
            SyncNavigationSupport();

            if (shouldMove)
            {
                Vector3 planar = delta;
                planar.y = 0f;
                if (planar.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(planar.normalized), 720f * dt);
            }

            if (_animatorController != null)
            {
                _animatorController.SetPlaybackSpeed(1f);
                _animatorController.SetGrounded(true);
                _animatorController.SetLocomotionSpeed(shouldMove ? WalkLocomotionSpeed : 0f);
            }
        }

        private void TickInvestigate(float dt)
        {
            if (_owner == null)
            {
                TickFollow(dt);
                return;
            }

            ExitFollowLocomotion();
            EnterCombatLocomotion();
            ClearCombatAnchorTarget();

            Vector3 moveTarget = ResolveSteeringTarget(_combatMemory.LastKnownTargetPosition, true);
            Vector3 delta = moveTarget - transform.position;
            delta.y = 0f;

            float arrivalDistance = Mathf.Max(FollowPatrolArrivalDistance, 0.32f);
            bool shouldMove = delta.sqrMagnitude > arrivalDistance * arrivalDistance;
            float investigateSpeedRatio = _aiConfig != null ? Mathf.Max(0.1f, _aiConfig.combatWalkSpeedRatio) : 0.72f;
            float moveSpeed = Mathf.Max(1f, _combatStats.MoveSpeed) * investigateSpeedRatio;
            if (shouldMove)
                transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * dt);
            SyncNavigationSupport();

            if (delta.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta.normalized), 780f * dt);

            if (_animatorController != null)
            {
                _animatorController.SetPlaybackSpeed(1f);
                _animatorController.SetGrounded(true);
                _animatorController.SetLocomotionSpeed(shouldMove ? WalkLocomotionSpeed : 0f);
            }
        }

        private void TickCombatMove(float dt)
        {
            ExitFollowLocomotion();
            if (_target == null || !_target.IsAlive || _owner == null)
            {
                TickFollow(dt);
                return;
            }

            EnterCombatLocomotion();
            Vector3 targetPosition = ResolveTargetCenterPosition(_target);
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float distanceToTarget = ResolvePlanarSurfaceDistance(transform.position, _target);

            Vector3 desiredPoint = ResolveStableCombatAnchor(targetPosition, distanceToTarget, dt);
            Vector3 moveTarget = ResolveSteeringTarget(desiredPoint, true);
            Vector3 moveDelta = moveTarget - transform.position;
            moveDelta.y = 0f;

            float orbitArrivalDistance = _aiConfig != null ? Mathf.Max(0.05f, _aiConfig.orbitArrivalDistance) : OrbitArrivalDistance;
            bool shouldMove = moveDelta.sqrMagnitude > orbitArrivalDistance * orbitArrivalDistance;
            float preferredAttackDistance = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.preferredMeleeDistance) : PreferredAttackDistance;
            float attackDistanceTolerance = _aiConfig != null ? Mathf.Max(0.05f, _aiConfig.meleeDistanceTolerance) : AttackDistanceTolerance;
            float chaseDistanceThreshold = _aiConfig != null ? Mathf.Max(0.1f, _aiConfig.chaseDistanceThreshold) : 1.6f;
            bool isChasing = distanceToTarget > preferredAttackDistance + attackDistanceTolerance + chaseDistanceThreshold;
            bool useRun = isChasing || moveDelta.sqrMagnitude > 1.4f * 1.4f;
            float combatWalkSpeedRatio = _aiConfig != null ? Mathf.Max(0.1f, _aiConfig.combatWalkSpeedRatio) : 0.72f;
            float combatRunSpeedRatio = _aiConfig != null ? Mathf.Max(0.1f, _aiConfig.combatRunSpeedRatio) : CombatMoveSpeedRatio;
            float moveSpeed = Mathf.Max(1f, _combatStats.MoveSpeed) * (useRun ? combatRunSpeedRatio : combatWalkSpeedRatio);
            if (shouldMove)
                transform.position = Vector3.MoveTowards(transform.position, moveTarget, moveSpeed * dt);
            SyncNavigationSupport();

            Vector3 facing = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : _owner.transform.forward;
            if (shouldMove && moveDelta.sqrMagnitude > 0.0001f)
            {
                Vector3 moveFacing = moveDelta.normalized;
                float alignment = toTarget.sqrMagnitude > 0.0001f ? Vector3.Dot(moveFacing, toTarget.normalized) : 1f;
                if (isChasing || alignment < 0.35f)
                    facing = moveFacing;
            }
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 900f * dt);

            if (_animatorController != null)
            {
                _animatorController.SetPlaybackSpeed(1f);
                _animatorController.SetGrounded(true);
                _animatorController.SetLocomotionSpeed(!shouldMove ? 0f : (useRun ? RunLocomotionSpeed : WalkLocomotionSpeed));
            }
        }

        private void TickTimeline(float dt)
        {
            if (_timelineRunner == null)
                return;

            _timelineRunner.Tick(dt);
            if (!_timelineRunner.IsComplete)
            {
                TryResolveActionTimelineExit();
                return;
            }

            CompleteCurrentAction(detachTimeline: false);
        }

        private bool TryResolveActionTimelineExit()
        {
            if (_timelineRunner == null || string.IsNullOrWhiteSpace(_activeActionId))
                return false;

            SkillConfigEntry entry = ResolveActionConfigEntry(_activeActionId);
            if (entry == null)
                return false;

            if (IsAttackComboAction(_activeActionId))
                return TryResolveAttackTimelineExit(entry);

            if (string.Equals(_activeActionId, "Dodge", System.StringComparison.Ordinal))
                return TryResolveDodgeTimelineExit(entry);

            if (ShouldKeepTimelineBoundToAction(_activeActionId))
                return false;

            float naturalExitThreshold = ResolveNaturalExitThreshold(entry, 0.9f);
            if (!IsCurrentActionPastThreshold(naturalExitThreshold))
                return false;

            CompleteCurrentAction(detachTimeline: true);
            return true;
        }

        private bool TryResolveAttackTimelineExit(SkillConfigEntry entry)
        {
            int currentStage = ParseAttackComboIndex(_activeActionId);
            int nextStage = currentStage + 1;
            float comboThreshold = ResolvePendingThreshold(entry, GameAction.NormalAttack, currentStage <= 2 ? 0.7f : 0.6f);
            float moveThreshold = ResolvePendingThreshold(entry, GameAction.Walk, currentStage <= 2 ? 0.8f : 0.7f);
            BufferAction(EvaluateAttackBufferedAction(nextStage), nextStage);

            if (nextStage <= 3
                && _bufferedAction == CloneBufferedAction.Attack
                && !ShouldContinueAttackCombo(nextStage))
            {
                ClearBufferedAction();
                BufferMoveAction();
            }

            if (_bufferedAction == CloneBufferedAction.Attack
                && !IsCurrentActionPastThreshold(comboThreshold))
            {
                return false;
            }

            if (_bufferedAction == CloneBufferedAction.Attack)
            {
                OpenComboWindow(nextStage);
                CompleteCurrentAction(detachTimeline: true);
                return true;
            }

            if (_bufferedAction == CloneBufferedAction.Move
                && IsCurrentActionPastThreshold(moveThreshold))
            {
                OpenComboWindow(nextStage);
                CompleteCurrentAction(detachTimeline: true);
                return true;
            }

            float naturalExitThreshold = ResolveNaturalExitThreshold(entry, 0.9f);
            if (!IsCurrentActionPastThreshold(naturalExitThreshold))
                return false;

            OpenComboWindow(nextStage);
            CompleteCurrentAction(detachTimeline: true);
            return true;
        }

        private bool TryResolveDodgeTimelineExit(SkillConfigEntry entry)
        {
            float threshold = ResolvePendingThreshold(entry, GameAction.Walk, 0.8f);
            BufferAction(EvaluateDodgeBufferedAction(), 0);
            if (!IsCurrentActionPastThreshold(threshold))
                return false;

            EndDodgeMotion();
            CompleteCurrentAction(detachTimeline: true);
            return true;
        }

        private void CompleteCurrentAction(bool detachTimeline)
        {
            if (detachTimeline)
                DetachCurrentTimelineRunner();
            else
                _timelineRunner = null;

            _animatorController?.SetPlaybackSpeed(1f);
            _animatorController?.TriggerLocomotion();
            if (!detachTimeline && TryContinueActionSequence())
                return;

            if (TryConsumeBufferedAction())
                return;

            EnterCombatLocomotion();
            _actionLockTimer = 0f;
            _decisionTimer = 0f;
            _activeActionId = string.Empty;
            ClearBufferedAction();
            _activeActionElapsed = 0f;
        }

        private void DetachCurrentTimelineRunner()
        {
            if (_timelineRunner == null)
                return;

            _timelineRunner.StopStateScopedCues();
            if (_timelineRunner.HasPendingWork)
                _detachedTimelineRunners.Add(_timelineRunner);
            else
                _timelineRunner.Stop();
            _timelineRunner = null;
        }

        private void TickComboWindow(float dt)
        {
            if (_comboWindowTimer <= 0f)
                return;

            _comboWindowTimer = Mathf.Max(0f, _comboWindowTimer - dt);
            if (_comboWindowTimer <= 0f)
                ClearComboWindow();
        }

        private void TickDetachedTimelineRunners(float dt)
        {
            if (_detachedTimelineRunners.Count <= 0)
                return;

            for (int i = _detachedTimelineRunners.Count - 1; i >= 0; i--)
            {
                SkillTimelineRunner runner = _detachedTimelineRunners[i];
                if (runner == null)
                {
                    _detachedTimelineRunners.RemoveAt(i);
                    continue;
                }

                runner.Tick(dt);
                if (runner.IsComplete)
                    _detachedTimelineRunners.RemoveAt(i);
            }
        }

        private void StopDetachedTimelineRunners()
        {
            if (_detachedTimelineRunners.Count <= 0)
                return;

            for (int i = _detachedTimelineRunners.Count - 1; i >= 0; i--)
                _detachedTimelineRunners[i]?.Stop();

            _detachedTimelineRunners.Clear();
        }

        private SkillConfigEntry ResolveActionConfigEntry(string actionId)
        {
            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            return skillDb != null ? skillDb.GetEntryByActionId(actionId) : null;
        }

        private static float ResolvePendingThreshold(SkillConfigEntry entry, GameAction action, float fallback)
        {
            if (entry != null && entry.TryGetPendingReleaseThreshold(action, out float configured))
                return Mathf.Clamp01(configured);

            return Mathf.Clamp01(fallback);
        }

        private static float ResolveNaturalExitThreshold(SkillConfigEntry entry, float fallback)
        {
            if (entry != null && entry.overrideNaturalExitNormalizedTime)
                return Mathf.Clamp01(entry.naturalExitNormalizedTime);

            return Mathf.Clamp01(fallback);
        }

        private bool IsCurrentActionPastThreshold(float threshold)
        {
            if (_animator == null)
                return false;

            threshold = Mathf.Clamp01(threshold);
            if (_activeActionElapsed < MinActionExitValidationDelay)
                return false;

            if (_animator.IsInTransition(0))
                return false;

            AnimatorStateInfo currentInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (currentInfo.loop)
                return false;

            return currentInfo.normalizedTime >= threshold;
        }

        private static bool IsAttackComboAction(string actionId)
        {
            return !string.IsNullOrWhiteSpace(actionId)
                   && actionId.StartsWith("Attack", System.StringComparison.Ordinal);
        }

        private static bool ShouldKeepTimelineBoundToAction(string actionId)
        {
            return string.Equals(actionId, "ChargeStart", System.StringComparison.Ordinal)
                   || string.Equals(actionId, "ChargeLoop", System.StringComparison.Ordinal)
                   || string.Equals(actionId, "ChargeRelease", System.StringComparison.Ordinal);
        }

        private CloneBufferedAction EvaluateAttackBufferedAction(int nextStage)
        {
            if (_target == null || !_target.IsAlive)
                return CloneBufferedAction.Move;

            return nextStage <= 3 && ShouldContinueAttackCombo(nextStage)
                ? CloneBufferedAction.Attack
                : CloneBufferedAction.Move;
        }

        private CloneBufferedAction EvaluateDodgeBufferedAction()
        {
            if (_target == null || !_target.IsAlive)
                return CloneBufferedAction.Move;

            float distanceToTarget = ResolvePlanarSurfaceDistance(transform.position, _target);
            if (IsTargetThreatening(distanceToTarget))
                return CloneBufferedAction.Move;

            float attackScore = EvaluateAttackScore(distanceToTarget);
            return attackScore > 0f ? CloneBufferedAction.Attack : CloneBufferedAction.Move;
        }

        private void BufferAction(CloneBufferedAction action, int attackComboIndex)
        {
            if (action == CloneBufferedAction.Attack)
            {
                BufferAttackAction(attackComboIndex);
                return;
            }

            if (action == CloneBufferedAction.Move)
            {
                BufferMoveAction();
                return;
            }

            ClearBufferedAction();
        }

        private void BufferAttackAction(int attackComboIndex)
        {
            _bufferedAction = CloneBufferedAction.Attack;
            _bufferedAttackComboIndex = Mathf.Clamp(attackComboIndex, 0, 3);
        }

        private void BufferMoveAction()
        {
            _bufferedAction = CloneBufferedAction.Move;
            _bufferedAttackComboIndex = -1;
        }

        private void ClearBufferedAction()
        {
            _bufferedAction = CloneBufferedAction.None;
            _bufferedAttackComboIndex = -1;
        }

        private bool TryConsumeBufferedAction()
        {
            CloneBufferedAction bufferedAction = _bufferedAction;
            int bufferedAttackComboIndex = _bufferedAttackComboIndex;
            ClearBufferedAction();

            return bufferedAction switch
            {
                CloneBufferedAction.Attack => TryExecuteBufferedAttack(bufferedAttackComboIndex),
                _ => false,
            };
        }

        private bool TryExecuteBufferedAttack(int attackComboIndex)
        {
            int comboIndex = ResolveBufferedAttackComboIndex(attackComboIndex);
            return TryCastAttack(comboIndex);
        }

        private void EndDodgeMotion()
        {
            _dodgeMotionTimer = 0f;
            _dodgeVelocity = Vector3.zero;
        }

        private void UpdateTarget(float dt)
        {
            _targetRefreshTimer -= dt;

            bool invalidTarget = _target == null || !_target.IsAlive;
            float ownerLeashDistance = _aiConfig != null ? Mathf.Max(8f, _aiConfig.ownerLeashDistance) : MaxTargetOwnerDistance;
            if (!invalidTarget && _owner != null)
            {
                float ownerDistance = Vector3.Distance(_target.transform.position, _owner.transform.position);
                invalidTarget = ownerDistance > ownerLeashDistance;
            }

            if (!invalidTarget && !CanKeepTargetWhileRegrouping(_target))
                invalidTarget = true;

            if (!invalidTarget && _targetRefreshTimer > 0f)
                return;

            EnemyController previousTarget = _target;
            _target = FindBestTarget();
            if (_target != null)
            {
                _combatMemory.RecordTarget(_target, ResolveTargetCenterPosition(_target), Time.time);
            }
            else if (previousTarget != null)
            {
                _combatMemory.RecordTarget(previousTarget, previousTarget.transform.position, Time.time);
                _combatMemory.ClearTarget();
            }

            if (_target != previousTarget)
            {
                ClearCombatAnchorTarget();
                ClearComboWindow();
            }
            _targetRefreshTimer = _aiConfig != null ? Mathf.Max(0.02f, _aiConfig.targetRefreshInterval) : TargetRefreshInterval;
        }

        private EnemyController FindBestTarget()
        {
            if (_owner == null)
                return null;

            EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            EnemyController best = null;
            float bestScore = float.MaxValue;
            Vector3 origin = transform.position;
            float perceptionRadius = _aiConfig != null ? Mathf.Max(1f, _aiConfig.perceptionRadius) : 12f;
            float targetStickiness = _aiConfig != null ? Mathf.Clamp01(_aiConfig.targetStickiness) : 0.18f;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                bool isFinalBoss = IsFinalBossTarget(enemy);
                float distanceToClone = ResolvePlanarSurfaceDistance(origin, enemy);
                float effectivePerceptionRadius = isFinalBoss ? Mathf.Max(perceptionRadius, FinalBossAssistRadius) : perceptionRadius;
                if (distanceToClone > effectivePerceptionRadius)
                    continue;

                float distanceToOwner = ResolvePlanarSurfaceDistance(_owner.transform.position, enemy);
                float ownerLeashDistance = _aiConfig != null ? Mathf.Max(8f, _aiConfig.ownerLeashDistance) : MaxTargetOwnerDistance;
                float effectiveOwnerLeashDistance = isFinalBoss ? Mathf.Max(ownerLeashDistance, FinalBossAssistRadius) : ownerLeashDistance;
                if (distanceToOwner > effectiveOwnerLeashDistance)
                    continue;

                if (!CanKeepTargetWhileRegrouping(enemy, distanceToClone, distanceToOwner))
                    continue;

                float score = distanceToClone + distanceToOwner * 0.3f + enemy.CurrentHpRatio * 1.2f;
                if (enemy.IsHurt)
                    score -= 1.8f;
                if (enemy.IsInPostCastRecovery)
                    score -= 2.6f;
                if (enemy.IsCastingSkill)
                    score -= 0.7f;
                if (enemy == _target)
                    score -= targetStickiness * 4f;
                if (enemy == _combatMemory.LastTarget && _combatMemory.HasRecentTargetMemory(GetTargetMemoryDuration()))
                    score -= 1.1f;
                if (isFinalBoss)
                    score -= 5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }

            return best;
        }

        private bool TryCastAttack()
        {
            int comboIndex = HasComboWindow() ? _comboNextIndex : 0;
            return TryCastAttack(comboIndex);
        }

        private bool TryCastAttack(int requestedComboIndex)
        {
            if (_target == null || !_target.IsAlive)
                return false;

            float distanceToTarget = ResolvePlanarSurfaceDistance(transform.position, _target);
            float attackCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.attackCastRange) : AttackCastRange;
            if (distanceToTarget > attackCastRange)
                return false;

            int comboIndex = Mathf.Clamp(requestedComboIndex, 0, 3);
            string actionId = $"Attack{comboIndex}";
            if (!BeginConfiguredAction(actionId))
                return false;

            _comboStage = comboIndex;
            if (comboIndex == 0 || comboIndex == _comboNextIndex)
                ClearComboWindow();
            return true;
        }

        private bool TryCastSharedCooldownSkill()
        {
            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillDb?.entries == null || _owner?.PlayerModel == null || _target == null || !_target.IsAlive)
                return false;

            List<SkillConfigEntry> candidates = new List<SkillConfigEntry>();
            for (int i = 0; i < skillDb.entries.Count; i++)
            {
                SkillConfigEntry entry = skillDb.entries[i];
                if (entry == null || !entry.IsActiveSkill)
                    continue;
                if (!_owner.PlayerModel.IsSkillAvailable(entry))
                    continue;
                if (!CanCastActiveSkill(entry))
                    continue;
                candidates.Add(entry);
            }

            if (candidates.Count <= 0)
                return false;

            float skillCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.skillCastRange) : SkillCastRange;
            float distanceToTarget = ResolvePlanarSurfaceDistance(transform.position, _target);
            if (distanceToTarget > skillCastRange)
                return false;

            SkillConfigEntry selected = SelectBestActiveSkill(candidates, distanceToTarget);
            if (selected == null)
                return false;

            if (!TryCommitActiveSkill(selected))
                return false;
            return BeginConfiguredAction(selected.GetResolvedActionId());
        }

        private bool BeginConfiguredAction(string actionId, float overrideDuration = -1f)
        {
            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            SkillConfigEntry entry = skillDb != null ? skillDb.GetEntryByActionId(actionId) : null;
            if (entry == null)
                return false;

            if (!IsAttackComboAction(actionId))
                ClearComboWindow();

            ExitFollowLocomotion();
            ExitCombatLocomotion();
            string triggerName = entry.GetResolvedAnimationTrigger();
            _animatorController?.SetPlaybackSpeed(PlayerBuffRuntimeUtility.GetActionPlaybackSpeed(_combatStats, actionId));
            _animatorController?.TriggerAction(triggerName);
            _activeActionId = actionId ?? string.Empty;
            _activeActionElapsed = 0f;

            SharedSkillDefinition definition = ConfigManager.GetInstance()?.GetSkillDatabase()?.GetEntry(entry.skillId);
            if (definition != null)
                BeginTimeline(definition, actionId, overrideDuration);
            else
                _actionLockTimer = Mathf.Max(0.12f, overrideDuration > 0f ? overrideDuration : 0.25f);

            return true;
        }

        private void BeginTimeline(SharedSkillDefinition definition, string actionId, float overrideDuration = -1f)
        {
            if (definition == null)
                return;

            _timelineRunner?.Stop();
            _timelineRunner = new SkillTimelineRunner();
            SharedSkillDefinition runtimeDefinition = PlayerBuffRuntimeUtility.BuildRuntimeSkillDefinition(transform, definition, actionId);
            float playbackSpeed = PlayerBuffRuntimeUtility.GetActionPlaybackSpeed(_combatStats, actionId);
            float timelineDuration = runtimeDefinition.GetTimelineDuration();
            float effectiveDuration = overrideDuration > 0f
                ? overrideDuration
                : timelineDuration / Mathf.Max(0.1f, playbackSpeed);
            float actionCommitDuration = Mathf.Max(0.12f, effectiveDuration * 0.7f);
            _actionLockTimer = Mathf.Clamp(actionCommitDuration, 0.12f, 0.9f);
            _timelineRunner.Begin(runtimeDefinition, new CloneSkillExecutionContext(this), overrideDuration);
        }

        private Vector3 ResolveCombatAnchor(Vector3 targetPosition, float distanceToTarget)
        {
            float preferredAttackDistance = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.preferredMeleeDistance) : PreferredAttackDistance;
            float attackDistanceTolerance = _aiConfig != null ? Mathf.Max(0.05f, _aiConfig.meleeDistanceTolerance) : AttackDistanceTolerance;
            float tooCloseDistance = _aiConfig != null ? Mathf.Max(0.3f, _aiConfig.tooCloseDistance) : TooCloseDistance;
            float orbitOffsetDistance = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.orbitDistance) : OrbitOffsetDistance;
            float orbitAngle = _aiConfig != null ? _aiConfig.orbitAngle : 42f;
            float targetRadius = ResolveTargetPlanarRadius(_target);

            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            Vector3 facingToTarget = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
            facingToTarget.y = 0f;
            if (facingToTarget.sqrMagnitude <= 0.0001f)
                facingToTarget = Vector3.forward;

            Vector3 awayFromTarget = -facingToTarget;
            Vector3 sideStep = Quaternion.Euler(0f, 90f * _orbitSign, 0f) * awayFromTarget;
            bool targetThreatening = IsTargetThreatening(distanceToTarget);
            if (targetThreatening)
            {
                float retreatDistance = targetRadius + Mathf.Max(preferredAttackDistance + 1.05f, tooCloseDistance + 1f);
                Vector3 retreatDirection = (sideStep * 0.62f + awayFromTarget * 0.38f).normalized;
                if (retreatDirection.sqrMagnitude <= 0.0001f)
                    retreatDirection = awayFromTarget;
                return targetPosition + retreatDirection * retreatDistance;
            }

            if (distanceToTarget < tooCloseDistance)
                return targetPosition - facingToTarget * (targetRadius + tooCloseDistance + 0.4f);

            if (_target != null && (_target.IsHurt || _target.IsInPostCastRecovery))
                preferredAttackDistance = Mathf.Max(1.6f, preferredAttackDistance - 0.45f);

            if (distanceToTarget > preferredAttackDistance + attackDistanceTolerance)
                return targetPosition - facingToTarget * (targetRadius + preferredAttackDistance);

            Vector3 toOwner = _owner.transform.position - targetPosition;
            toOwner.y = 0f;
            Vector3 baseDir = toOwner.sqrMagnitude > 0.0001f ? -toOwner.normalized : -facingToTarget;
            if (baseDir.sqrMagnitude <= 0.0001f)
                baseDir = -facingToTarget;

            float orbitWeight = _aiConfig != null ? Mathf.Clamp01(_aiConfig.orbitBias) : 0.85f;
            Vector3 orbitDir = Quaternion.Euler(0f, orbitAngle * _orbitSign * Mathf.Lerp(0.75f, 1.15f, orbitWeight), 0f) * baseDir;
            return targetPosition + orbitDir.normalized * (targetRadius + orbitOffsetDistance);
        }

        private bool EvaluateAndExecuteCombatDecision()
        {
            if (_target == null || !_target.IsAlive)
                return false;

            float distanceToTarget = ResolvePlanarSurfaceDistance(transform.position, _target);
            SkillConfigEntry bestSkillEntry = ResolveBestActiveSkill(distanceToTarget, out float skillScore);
            float dodgeScore = EvaluateDodgeScore(distanceToTarget);
            float chargeScore = EvaluateChargeScore(distanceToTarget);
            float attackScore = EvaluateAttackScore(distanceToTarget);
            ApplyDecisionMemoryPenalty(CloneCombatDecisionKind.Dodge, ref dodgeScore);
            ApplyDecisionMemoryPenalty(CloneCombatDecisionKind.Skill, ref skillScore);
            ApplyDecisionMemoryPenalty(CloneCombatDecisionKind.Charge, ref chargeScore);
            ApplyDecisionMemoryPenalty(CloneCombatDecisionKind.Attack, ref attackScore);

            CloneDecisionType decision = CloneDecisionType.None;
            float bestScore = 0f;

            ConsiderDecision(CloneDecisionType.Dodge, dodgeScore, ref decision, ref bestScore);
            ConsiderDecision(CloneDecisionType.Skill, skillScore, ref decision, ref bestScore);
            ConsiderDecision(CloneDecisionType.Charge, chargeScore, ref decision, ref bestScore);
            ConsiderDecision(CloneDecisionType.Attack, attackScore, ref decision, ref bestScore);

            bool success = decision switch
            {
                CloneDecisionType.Dodge => TryStartDodge(),
                CloneDecisionType.Skill => bestSkillEntry != null && ExecuteActiveSkill(bestSkillEntry),
                CloneDecisionType.Charge => TryStartChargeAttack(),
                CloneDecisionType.Attack => TryCastAttack(),
                _ => false,
            };

            if (success)
                _combatMemory.RecordDecision(ResolveDecisionKind(decision), Time.time);

            return success;
        }

        private SkillConfigEntry SelectBestActiveSkill(List<SkillConfigEntry> candidates, float distanceToTarget)
        {
            SkillConfigEntry best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                SkillConfigEntry candidate = candidates[i];
                if (candidate == null)
                    continue;

                float score = 1f + candidate.cooldownSeconds * 0.12f;
                if (_target != null && (_target.IsHurt || _target.IsInPostCastRecovery))
                    score += 0.55f;
                if (distanceToTarget > ((_aiConfig != null ? _aiConfig.attackCastRange : AttackCastRange) + 0.25f))
                    score += 0.3f;
                score += Random.Range(0f, 0.18f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private SkillConfigEntry ResolveBestActiveSkill(float distanceToTarget, out float score)
        {
            score = 0f;
            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillDb?.entries == null || _owner?.PlayerModel == null || _target == null || !_target.IsAlive)
                return null;

            float skillCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.skillCastRange) : SkillCastRange;
            if (distanceToTarget > skillCastRange)
                return null;

            SkillConfigEntry best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < skillDb.entries.Count; i++)
            {
                SkillConfigEntry entry = skillDb.entries[i];
                if (entry == null || !entry.IsActiveSkill)
                    continue;
                if (!_owner.PlayerModel.IsSkillAvailable(entry))
                    continue;
                if (!CanCastActiveSkill(entry))
                    continue;

                float nextScore = 0.45f + entry.cooldownSeconds * 0.08f;
                nextScore += (_aiConfig != null ? _aiConfig.skillBias : 0.78f) * 0.65f;
                if (_target.IsHurt || _target.IsInPostCastRecovery)
                    nextScore += 0.55f;
                if (distanceToTarget > ((_aiConfig != null ? _aiConfig.preferredMeleeDistance : PreferredAttackDistance) + 0.5f))
                    nextScore += 0.2f;
                nextScore += Random.Range(0f, 0.15f);
                if (nextScore > bestScore)
                {
                    bestScore = nextScore;
                    best = entry;
                }
            }

            if (best != null)
                score = bestScore;
            return best;
        }

        private float EvaluateDodgeScore(float distanceToTarget)
        {
            if (_target == null || !_target.IsAlive || _dodgeCooldownTimer > 0f)
                return 0f;

            float threatDistance = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.dodgeThreatDistance) : 4.2f;
            if (distanceToTarget > threatDistance)
                return 0f;

            bool targetThreatening = IsTargetThreatening(distanceToTarget);
            if (!targetThreatening)
                return 0f;

            float score = 0.25f + (_aiConfig != null ? _aiConfig.dodgeBias : 0.72f) * 0.85f;
            if (_target.IsCastingSkill)
                score += 0.35f;
            if (!_target.IsCastingSkill && !_target.IsHurt && !_target.IsInPostCastRecovery)
                score += 0.18f;
            if (_target.ActiveSkillTargetPosition.HasValue)
            {
                float targetedDistance = Vector3.Distance(_target.ActiveSkillTargetPosition.Value, transform.position);
                if (targetedDistance <= threatDistance)
                    score += 0.25f;
            }

            return score;
        }

        private float EvaluateChargeScore(float distanceToTarget)
        {
            if (_target == null || !_target.IsAlive || _chargeCooldownTimer > 0f)
                return 0f;

            if (IsTargetThreatening(distanceToTarget))
                return 0f;

            float chargeEnterRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.chargeEnterRange) : 4.8f;
            if (distanceToTarget > chargeEnterRange)
                return 0f;

            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillDb == null || skillDb.GetEntryByActionId("ChargeStart") == null || skillDb.GetEntryByActionId("ChargeRelease") == null)
                return 0f;

            float score = 0.3f + (_aiConfig != null ? _aiConfig.chargeBias : 0.68f) * 0.8f;
            if (_target.IsHurt || _target.IsInPostCastRecovery)
                score += 0.45f;
            if (distanceToTarget > ((_aiConfig != null ? _aiConfig.preferredMeleeDistance : PreferredAttackDistance) + 0.45f))
                score += 0.12f;
            return score;
        }

        private float EvaluateAttackScore(float distanceToTarget)
        {
            float attackCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.attackCastRange) : AttackCastRange;
            if (_target == null || !_target.IsAlive || distanceToTarget > attackCastRange)
                return 0f;

            if (IsTargetThreatening(distanceToTarget))
                return 0f;

            float score = 0.5f + (_aiConfig != null ? _aiConfig.aggression : 0.82f) * 0.7f;
            if (_target.IsHurt || _target.IsInPostCastRecovery)
                score += 0.22f;
            return score;
        }

        private static void ConsiderDecision(CloneDecisionType candidate, float candidateScore, ref CloneDecisionType current, ref float bestScore)
        {
            if (candidateScore <= bestScore)
                return;

            current = candidate;
            bestScore = candidateScore;
        }

        private void ApplyDecisionMemoryPenalty(CloneCombatDecisionKind decisionKind, ref float score)
        {
            if (score <= 0f)
                return;

            float basePenalty = decisionKind switch
            {
                CloneCombatDecisionKind.Dodge => _aiConfig != null ? Mathf.Max(0f, _aiConfig.dodgeDecisionPenalty) : 0.18f,
                CloneCombatDecisionKind.Skill => _aiConfig != null ? Mathf.Max(0f, _aiConfig.skillDecisionPenalty) : 0.12f,
                CloneCombatDecisionKind.Charge => _aiConfig != null ? Mathf.Max(0f, _aiConfig.chargeDecisionPenalty) : 0.15f,
                CloneCombatDecisionKind.Attack => HasComboWindow() ? 0f : (_aiConfig != null ? Mathf.Max(0f, _aiConfig.attackDecisionPenalty) : 0.06f),
                _ => 0f,
            };

            if (basePenalty <= 0f)
                return;

            score = Mathf.Max(0f, score - _combatMemory.GetRepeatPenalty(decisionKind, basePenalty));
        }

        private static CloneCombatDecisionKind ResolveDecisionKind(CloneDecisionType decision)
        {
            return decision switch
            {
                CloneDecisionType.Dodge => CloneCombatDecisionKind.Dodge,
                CloneDecisionType.Skill => CloneCombatDecisionKind.Skill,
                CloneDecisionType.Charge => CloneCombatDecisionKind.Charge,
                CloneDecisionType.Attack => CloneCombatDecisionKind.Attack,
                _ => CloneCombatDecisionKind.None,
            };
        }

        private bool ExecuteActiveSkill(SkillConfigEntry entry)
        {
            if (entry == null)
                return false;

            float distanceToTarget = _target != null ? ResolvePlanarSurfaceDistance(transform.position, _target) : float.MaxValue;
            if (IsTargetThreatening(distanceToTarget))
                return false;

            if (!TryCommitActiveSkill(entry))
                return false;
            return BeginConfiguredAction(entry.GetResolvedActionId());
        }

        private bool TryStartChargeAttack()
        {
            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillDb == null || skillDb.GetEntryByActionId("ChargeStart") == null || skillDb.GetEntryByActionId("ChargeRelease") == null)
                return false;

            _chargeCooldownTimer = _aiConfig != null ? Mathf.Max(0f, _aiConfig.chargeCooldownSeconds) : 3f;
            return BeginConfiguredAction("ChargeStart");
        }

        private bool TryStartDodge()
        {
            if (_target == null)
                return false;

            Vector3 dodgeDirection = ResolveDodgeDirection();
            if (dodgeDirection.sqrMagnitude <= 0.0001f)
                return false;

            float dodgeDuration = _aiConfig != null ? Mathf.Max(0.05f, _aiConfig.dodgeDuration) : 0.22f;
            float dodgeDistance = _aiConfig != null ? Mathf.Max(0.2f, _aiConfig.dodgeDistance) : 3.5f;
            _dodgeVelocity = dodgeDirection.normalized * (dodgeDistance / dodgeDuration);
            _dodgeMotionTimer = dodgeDuration;
            _dodgeCooldownTimer = _aiConfig != null ? Mathf.Max(0f, _aiConfig.dodgeCooldownSeconds) : 1.2f;
            return BeginConfiguredAction("Dodge", dodgeDuration);
        }

        private Vector3 ResolveDodgeDirection()
        {
            if (_target == null)
                return Vector3.zero;

            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                toTarget = transform.forward;

            Vector3 away = -toTarget.normalized;
            Vector3 side = Quaternion.Euler(0f, 90f * _orbitSign, 0f) * away;
            Vector3 desired = (_target.IsCastingSkill ? side * 0.8f + away * 0.2f : side * 0.65f + away * 0.35f).normalized;
            return desired.sqrMagnitude > 0.0001f ? desired : away;
        }

        private void TickLocalCooldowns(float dt)
        {
            if (dt <= 0f)
                return;

            _decisionTimer = Mathf.Max(0f, _decisionTimer - dt);
            if (_dodgeCooldownTimer > 0f)
                _dodgeCooldownTimer = Mathf.Max(0f, _dodgeCooldownTimer - dt);
            if (_chargeCooldownTimer > 0f)
                _chargeCooldownTimer = Mathf.Max(0f, _chargeCooldownTimer - dt);
        }

        private bool TickDodgeMotion(float dt)
        {
            if (_dodgeMotionTimer <= 0f)
                return false;

            ExitFollowLocomotion();
            _dodgeMotionTimer = Mathf.Max(0f, _dodgeMotionTimer - dt);
            Vector3 dodgeTarget = transform.position + _dodgeVelocity * dt;
            transform.position = ResolveSteeringTarget(dodgeTarget, false);
            SyncNavigationSupport();
            Vector3 facing = _dodgeVelocity;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing.normalized), 1080f * dt);

            if (_animatorController != null)
            {
                _animatorController.SetGrounded(true);
                _animatorController.SetLocomotionSpeed(0f);
            }

            return true;
        }

        private void MaintainActionFacing(float dt)
        {
            if (_target == null || !_target.IsAlive || _activeActionId == "Dodge")
                return;

            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget.normalized), 960f * dt);
        }

        private bool TryContinueActionSequence()
        {
            switch (_activeActionId)
            {
                case "ChargeStart":
                {
                    float holdMin = _aiConfig != null ? Mathf.Max(0f, _aiConfig.chargeHoldMinSeconds) : 0.18f;
                    float holdMax = _aiConfig != null ? Mathf.Max(holdMin, _aiConfig.chargeHoldMaxSeconds) : 0.4f;
                    return BeginConfiguredAction("ChargeLoop", Random.Range(holdMin, holdMax));
                }
                case "ChargeLoop":
                    return BeginConfiguredAction("ChargeRelease");
                default:
                    return false;
            }
        }

        private bool ShouldContinueAttackCombo(int nextStage)
        {
            if (_target == null || !_target.IsAlive)
                return false;

            float attackCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.attackCastRange) : AttackCastRange;
            float distanceToTarget = ResolvePlanarSurfaceDistance(transform.position, _target);
            if (distanceToTarget > attackCastRange + 0.45f)
                return false;

            if (IsTargetThreatening(distanceToTarget))
                return false;

            if (_target.IsHurt || _target.IsInPostCastRecovery)
                return true;

            if (nextStage <= 2)
                return true;

            float continueChance = 0.6f + (_aiConfig != null ? _aiConfig.comboBias : 0.72f) * 0.25f;
            continueChance += (_aiConfig != null ? _aiConfig.aggression : 0.82f) * 0.1f;
            return Random.value <= Mathf.Clamp01(continueChance);
        }

        private static int ParseAttackComboIndex(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId) || !actionId.StartsWith("Attack", System.StringComparison.Ordinal))
                return 0;

            string suffix = actionId.Substring("Attack".Length);
            return int.TryParse(suffix, out int index) ? index : 0;
        }

        private void OpenComboWindow(int nextAttackIndex)
        {
            if (nextAttackIndex <= 0 || nextAttackIndex > 3)
            {
                ClearComboWindow();
                return;
            }

            _comboNextIndex = nextAttackIndex;
            _comboWindowTimer = AttackComboWindowDuration;
        }

        private void ClearComboWindow()
        {
            _comboNextIndex = -1;
            _comboWindowTimer = 0f;
        }

        private bool HasComboWindow()
        {
            return _comboNextIndex > 0 && _comboWindowTimer > 0f;
        }

        private int ResolveBufferedAttackComboIndex(int requestedComboIndex)
        {
            if (HasComboWindow())
                return _comboNextIndex;

            return Mathf.Clamp(requestedComboIndex, 0, 3);
        }

        private bool ShouldRegroupToOwner()
        {
            if (_owner == null)
                return false;

            float regroupDistance = _aiConfig != null ? Mathf.Max(_settings.followRadius + 1f, _aiConfig.regroupDistance) : _settings.followRadius + 7f;
            return Vector3.Distance(transform.position, _owner.transform.position) > regroupDistance;
        }

        private bool ShouldInvestigateRecentTarget()
        {
            if (_owner == null || _target != null)
                return false;

            if (ShouldRegroupToOwner())
                return false;

            if (!_combatMemory.HasRecentTargetMemory(GetTargetMemoryDuration()))
                return false;

            float ownerLeashDistance = _aiConfig != null ? Mathf.Max(8f, _aiConfig.ownerLeashDistance) : MaxTargetOwnerDistance;
            float memoryDistanceToOwner = Vector3.Distance(_combatMemory.LastKnownTargetPosition, _owner.transform.position);
            return memoryDistanceToOwner <= ownerLeashDistance + 1f;
        }

        private float GetTargetMemoryDuration()
        {
            return _aiConfig != null ? Mathf.Max(0f, _aiConfig.targetMemoryDuration) : 0.9f;
        }

        private GameAction ResolveCurrentGameAction()
        {
            if (string.IsNullOrWhiteSpace(_activeActionId))
                return GameAction.None;

            if (IsAttackComboAction(_activeActionId))
                return GameAction.NormalAttack;

            return _activeActionId switch
            {
                "Dodge" => GameAction.Dodge,
                "ChargeStart" => GameAction.ChargeStart,
                "ChargeLoop" => GameAction.ChargeStart,
                "ChargeRelease" => GameAction.ChargeRelease,
                _ when _activeActionId.StartsWith("Skill", System.StringComparison.Ordinal) => GameAction.Skill,
                _ => GameAction.None,
            };
        }

        private float ResolveStateRemainingTime()
        {
            if (string.IsNullOrWhiteSpace(_activeActionId))
                return -1f;

            if (_timelineRunner != null && !_timelineRunner.IsComplete)
            {
                float duration = _timelineRunner.Duration;
                if (!float.IsNaN(duration) && !float.IsInfinity(duration))
                    return Mathf.Max(0f, duration - _timelineRunner.Elapsed);
            }

            return _actionLockTimer > 0f ? _actionLockTimer : -1f;
        }

        private float ResolveStateNormalizedProgress()
        {
            if (string.IsNullOrWhiteSpace(_activeActionId))
                return -1f;

            if (_animator != null && !_animator.IsInTransition(0))
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
                if (!info.loop)
                    return Mathf.Clamp01(info.normalizedTime);
            }

            if (_timelineRunner != null && !_timelineRunner.IsComplete)
            {
                float duration = _timelineRunner.Duration;
                if (duration > 0f && !float.IsInfinity(duration))
                    return Mathf.Clamp01(_timelineRunner.Elapsed / duration);
            }

            return -1f;
        }

        private bool CanKeepTargetWhileRegrouping(EnemyController enemy)
        {
            if (enemy == null || _owner == null)
                return false;

            float distanceToClone = ResolvePlanarSurfaceDistance(transform.position, enemy);
            float distanceToOwner = ResolvePlanarSurfaceDistance(_owner.transform.position, enemy);
            return CanKeepTargetWhileRegrouping(enemy, distanceToClone, distanceToOwner);
        }

        private bool CanKeepTargetWhileRegrouping(EnemyController enemy, float distanceToClone, float distanceToOwner)
        {
            if (enemy == null || _owner == null)
                return false;

            if (!ShouldRegroupToOwner())
                return true;

            float attackCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.attackCastRange) : AttackCastRange;
            float closeEngageDistance = attackCastRange + 0.45f;
            if (distanceToClone <= closeEngageDistance)
                return true;

            float cloneDistanceToOwner = Vector3.Distance(transform.position, _owner.transform.position);
            return distanceToOwner <= cloneDistanceToOwner + 0.35f;
        }

        private Vector3 ResolveStableCombatAnchor(Vector3 targetPosition, float distanceToTarget, float dt)
        {
            _combatAnchorRefreshTimer = Mathf.Max(0f, _combatAnchorRefreshTimer - dt);
            bool targetThreatening = IsTargetThreatening(distanceToTarget);
            bool targetMoved = (_combatAnchorReferenceTargetPosition - targetPosition).sqrMagnitude > 0.81f;
            bool threatChanged = _combatAnchorThreatening != targetThreatening;
            if (!_hasCombatAnchorTarget || _combatAnchorRefreshTimer <= 0f || targetMoved || threatChanged)
            {
                _combatAnchorReferenceTargetPosition = targetPosition;
                _combatAnchorTarget = ResolveCombatAnchor(targetPosition, distanceToTarget);
                _combatAnchorThreatening = targetThreatening;
                float refreshInterval = _aiConfig != null ? Mathf.Max(0.08f, _aiConfig.decisionInterval * 1.6f) : 0.1f;
                _combatAnchorRefreshTimer = refreshInterval;
                _hasCombatAnchorTarget = true;
            }

            return _combatAnchorTarget;
        }

        private void ClearCombatAnchorTarget()
        {
            _combatAnchorTarget = Vector3.zero;
            _combatAnchorReferenceTargetPosition = Vector3.zero;
            _combatAnchorRefreshTimer = 0f;
            _hasCombatAnchorTarget = false;
            _combatAnchorThreatening = false;
        }

        private bool IsTargetThreatening(float distanceToTarget)
        {
            if (_target == null || !_target.IsAlive)
                return false;

            float threatDistance = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.dodgeThreatDistance) : 4.2f;
            if (_target.IsCastingSkill)
                return true;

            if (_target.ActiveSkillTargetPosition.HasValue)
            {
                float targetedDistance = Vector3.Distance(_target.ActiveSkillTargetPosition.Value, transform.position);
                if (targetedDistance <= threatDistance)
                    return true;
            }

            if (_target.IsHurt || _target.IsInPostCastRecovery)
                return false;

            return _target.CurrentIntent.Type switch
            {
                EnemyIntentType.CastSkill => true,
                EnemyIntentType.Punish => distanceToTarget <= threatDistance,
                _ => false,
            };
        }

        private static bool IsFinalBossTarget(EnemyController enemy)
        {
            return enemy != null
                && enemy.EnemyCategory == EnemyType.Boss
                && enemy.GetComponent<LevelBossVisualMarker>() != null;
        }

        private static Vector3 ResolveTargetCenterPosition(EnemyController enemy)
        {
            if (enemy == null)
                return Vector3.zero;

            Collider collider = enemy.GetComponent<Collider>();
            if (collider != null)
            {
                Vector3 center = collider.bounds.center;
                center.y = enemy.transform.position.y;
                return center;
            }

            return enemy.transform.position;
        }

        private static float ResolveTargetPlanarRadius(EnemyController enemy)
        {
            if (enemy == null)
                return 0f;

            Collider collider = enemy.GetComponent<Collider>();
            if (collider == null)
                return 0f;

            Vector3 extents = collider.bounds.extents;
            return Mathf.Max(0f, Mathf.Max(extents.x, extents.z));
        }

        private static float ResolvePlanarSurfaceDistance(Vector3 origin, EnemyController enemy)
        {
            if (enemy == null)
                return float.MaxValue;

            Vector3 targetCenter = ResolveTargetCenterPosition(enemy);
            Vector3 planarOffset = targetCenter - origin;
            planarOffset.y = 0f;
            float centerDistance = planarOffset.magnitude;
            float targetRadius = ResolveTargetPlanarRadius(enemy);
            return Mathf.Max(0f, centerDistance - targetRadius);
        }

        private void ResetDecisionTimer()
        {
            _decisionTimer = _aiConfig != null ? Mathf.Max(0f, _aiConfig.decisionInterval) : 0.06f;
        }

        private void RefreshDerivedStats(bool resetVitals = false)
        {
            if (_owner?.PlayerModel == null)
                return;

            RefreshOwnerSharedStats();
            Stats ownerStats = _ownerSharedStats;
            if (ownerStats == null)
                return;

            float maxHpScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.maxHpScale) : 10f;
            float maxMpScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.maxMpScale) : 1f;
            float attackScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.attackScale) : 0.5f;
            float defenseScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.defenseScale) : 1f;
            float lifeStealScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.lifeStealScale) : 1f;
            float critRateScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.critRateScale) : 1f;
            float critDmgScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.critDmgScale) : 1f;
            float attackSpeedScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.attackSpeedScale) : 1f;
            float moveSpeedScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.moveSpeedScale) : 1f;
            float hpRegenScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.hpRegenScale) : 1f;
            float mpRegenScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.mpRegenScale) : 1f;
            float damageBonusScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.damageBonusScale) : 1f;
            float damageReduceScale = _aiConfig != null ? Mathf.Max(0f, _aiConfig.damageReduceScale) : 1f;

            float newBaseHp = Mathf.Max(1f, ownerStats.MaxHp * maxHpScale);
            float newBaseMp = Mathf.Max(0f, ownerStats.MaxMp * maxMpScale);
            float newBaseAttack = Mathf.Max(0f, ownerStats.Attack * attackScale);
            float newBaseDefense = Mathf.Max(0f, ownerStats.Defense * defenseScale);
            float newBaseLifeSteal = ownerStats.LifeSteal * lifeStealScale;
            float newBaseCritRate = ownerStats.CritRate * critRateScale;
            float newBaseCritDmg = ownerStats.CritDmg * critDmgScale;
            float newBaseAttackSpeed = Mathf.Max(0.1f, ownerStats.AttackSpeed * attackSpeedScale);
            float newBaseMoveSpeed = Mathf.Max(0.1f, ownerStats.MoveSpeed * moveSpeedScale);
            float newBaseHpRegen = Mathf.Max(0f, ownerStats.HpRegen * hpRegenScale);
            float newBaseMpRegen = Mathf.Max(0f, ownerStats.MpRegen * mpRegenScale);
            float newBaseDamageBonus = ownerStats.DamageBonus * damageBonusScale;
            float newBaseDamageReduce = ownerStats.DamageReduce * damageReduceScale;

            bool baseChanged =
                !Mathf.Approximately(_combatStats.baseHp, newBaseHp) ||
                !Mathf.Approximately(_combatStats.baseMp, newBaseMp) ||
                !Mathf.Approximately(_combatStats.baseAttack, newBaseAttack) ||
                !Mathf.Approximately(_combatStats.baseDefense, newBaseDefense) ||
                !Mathf.Approximately(_combatStats.baseLifeSteal, newBaseLifeSteal) ||
                !Mathf.Approximately(_combatStats.baseCritRate, newBaseCritRate) ||
                !Mathf.Approximately(_combatStats.baseCritDmg, newBaseCritDmg) ||
                !Mathf.Approximately(_combatStats.baseAttackSpeed, newBaseAttackSpeed) ||
                !Mathf.Approximately(_combatStats.baseMoveSpeed, newBaseMoveSpeed) ||
                !Mathf.Approximately(_combatStats.baseHpRegen, newBaseHpRegen) ||
                !Mathf.Approximately(_combatStats.baseMpRegen, newBaseMpRegen) ||
                !Mathf.Approximately(_combatStats.baseDamageBonus, newBaseDamageBonus) ||
                !Mathf.Approximately(_combatStats.baseDamageReduce, newBaseDamageReduce);

            if (!baseChanged)
            {
                ClampVitals();
                return;
            }

            float previousMaxHp = Mathf.Max(0f, _combatStats.MaxHp);
            float previousMaxMp = Mathf.Max(0f, _combatStats.MaxMp);

            _combatStats.baseHp = newBaseHp;
            _combatStats.baseMp = newBaseMp;
            _combatStats.baseAttack = newBaseAttack;
            _combatStats.baseDefense = newBaseDefense;
            _combatStats.baseLifeSteal = newBaseLifeSteal;
            _combatStats.baseCritRate = newBaseCritRate;
            _combatStats.baseCritDmg = newBaseCritDmg;
            _combatStats.baseAttackSpeed = newBaseAttackSpeed;
            _combatStats.baseMoveSpeed = newBaseMoveSpeed;
            _combatStats.baseHpRegen = newBaseHpRegen;
            _combatStats.baseMpRegen = newBaseMpRegen;
            _combatStats.baseDamageBonus = newBaseDamageBonus;
            _combatStats.baseDamageReduce = newBaseDamageReduce;
            _combatStats.InvalidateCache();

            ApplyVitalCaps(previousMaxHp, previousMaxMp, resetVitals);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || MaxHp <= 0f)
                return;

            _currentHp = Mathf.Min(MaxHp, _currentHp + amount);
        }

        public void OnHit(float damage, float stunDuration)
        {
            if (!IsAlive || damage <= 0f || _temporaryInvincibleTimer > 0f || _dodgeMotionTimer > 0f || _activeActionId == "Dodge")
                return;

            _currentHp = Mathf.Max(0f, _currentHp - damage);
            if (_currentHp <= 0f)
            {
                RefreshHeadHealthBarOnDeath();
                Destroy(gameObject);
                return;
            }

            if (_temporarySuperArmorTimer <= 0f)
                ApplyHardControl(stunDuration);
        }

        public void RestoreMp(float amount)
        {
            if (amount <= 0f || MaxMp <= 0f)
                return;

            _currentMp = Mathf.Min(MaxMp, _currentMp + amount);
        }

        public bool CanSpendMp(float amount)
        {
            return _currentMp + 0.001f >= Mathf.Max(0f, amount);
        }

        public void SpendMp(float amount)
        {
            if (amount <= 0f)
                return;

            _currentMp = Mathf.Max(0f, _currentMp - amount);
        }

        public void ApplyHardControl(float duration)
        {
            float validDuration = Mathf.Max(0f, duration);
            if (!IsAlive || validDuration <= 0f)
                return;

            DetachCurrentTimelineRunner();
            _activeActionId = string.Empty;
            ClearBufferedAction();
            ClearComboWindow();
            _activeActionElapsed = 0f;
            ExitCombatLocomotion();
            _actionLockTimer = Mathf.Max(_actionLockTimer, validDuration);
            _decisionTimer = Mathf.Max(_decisionTimer, validDuration);
            _hardControlTimer = Mathf.Max(_hardControlTimer, validDuration);
            EndDodgeMotion();
            _animatorController?.TriggerLocomotion();
        }

        public void ApplyTemporarySuperArmor(float duration)
        {
            if (duration <= 0f)
                return;

            _temporarySuperArmorTimer = Mathf.Max(_temporarySuperArmorTimer, duration);
        }

        public void ApplyTemporaryInvincibility(float duration)
        {
            if (duration <= 0f)
                return;

            _temporaryInvincibleTimer = Mathf.Max(_temporaryInvincibleTimer, duration);
        }

        public void ApplyStatModifier(StatModifier modifier, float duration)
        {
            if (modifier == null)
                return;

            float previousMaxHp = Mathf.Max(0f, MaxHp);
            float previousMaxMp = Mathf.Max(0f, MaxMp);
            StatModifier clonedModifier = modifier.Clone();
            _combatStats.AddModifier(clonedModifier);
            if (duration > 0f)
            {
                _runtimeModifiers.Add(new RuntimeModifier
                {
                    modifier = clonedModifier,
                    endTime = Time.time + Mathf.Max(0.01f, duration)
                });
            }

            ApplyVitalCaps(previousMaxHp, previousMaxMp, false);
        }

        private static int ResolveActiveSkillSlotIndex(SkillConfigEntry entry)
        {
            if (entry == null)
                return -1;

            string actionId = entry.GetResolvedActionId();
            if (string.IsNullOrWhiteSpace(actionId) || !actionId.StartsWith("Skill", System.StringComparison.Ordinal))
                return -1;

            string suffix = actionId.Substring("Skill".Length);
            return int.TryParse(suffix, out int slotIndex) ? slotIndex : -1;
        }

        private bool CanCastActiveSkill(SkillConfigEntry entry)
        {
            if (entry == null || !CanSpendMp(entry.mpCost))
                return false;

            int slotIndex = ResolveActiveSkillSlotIndex(entry);
            return slotIndex >= 0 && GetActiveSkillCooldownRemaining(slotIndex) <= 0f;
        }

        private bool TryCommitActiveSkill(SkillConfigEntry entry)
        {
            if (!CanCastActiveSkill(entry))
                return false;

            SpendMp(entry.mpCost);
            StartActiveSkillCooldown(entry);
            return true;
        }

        private void StartActiveSkillCooldown(SkillConfigEntry entry)
        {
            int slotIndex = ResolveActiveSkillSlotIndex(entry);
            if (slotIndex < 0 || entry == null)
                return;

            float cooldown = Mathf.Max(0f, entry.cooldownSeconds);
            if (cooldown <= 0f)
                return;

            _activeSkillCooldowns[slotIndex] = cooldown;
        }

        private float GetActiveSkillCooldownRemaining(int slotIndex)
        {
            return _activeSkillCooldowns.TryGetValue(slotIndex, out float remaining)
                ? Mathf.Max(0f, remaining)
                : 0f;
        }

        private void TickSkillCooldowns(float dt)
        {
            if (_activeSkillCooldowns.Count <= 0 || dt <= 0f)
                return;

            _cooldownUpdateSlots.Clear();
            _cooldownUpdateValues.Clear();
            _cooldownExpiredSlots.Clear();

            foreach (KeyValuePair<int, float> pair in _activeSkillCooldowns)
            {
                float remaining = Mathf.Max(0f, pair.Value - dt);
                if (remaining <= 0f)
                {
                    _cooldownExpiredSlots.Add(pair.Key);
                }
                else
                {
                    _cooldownUpdateSlots.Add(pair.Key);
                    _cooldownUpdateValues.Add(remaining);
                }
            }

            for (int i = 0; i < _cooldownUpdateSlots.Count; i++)
                _activeSkillCooldowns[_cooldownUpdateSlots[i]] = _cooldownUpdateValues[i];

            for (int i = 0; i < _cooldownExpiredSlots.Count; i++)
                _activeSkillCooldowns.Remove(_cooldownExpiredSlots[i]);
        }

        private void TickRuntimeModifiers()
        {
            if (_runtimeModifiers.Count <= 0)
                return;

            float now = Time.time;
            for (int i = _runtimeModifiers.Count - 1; i >= 0; i--)
            {
                RuntimeModifier runtimeModifier = _runtimeModifiers[i];
                if (runtimeModifier == null || runtimeModifier.modifier == null || now <= runtimeModifier.endTime)
                    continue;

                float previousMaxHp = Mathf.Max(0f, MaxHp);
                float previousMaxMp = Mathf.Max(0f, MaxMp);
                _combatStats.RemoveModifier(runtimeModifier.modifier);
                _runtimeModifiers.RemoveAt(i);
                ApplyVitalCaps(previousMaxHp, previousMaxMp, false);
            }
        }

        private void TickAttributeRegeneration(float dt)
        {
            if (dt <= 0f)
                return;

            float hpRegen = Mathf.Max(0f, _combatStats.HpRegen);
            if (hpRegen > 0f && _currentHp < MaxHp)
                Heal(hpRegen * dt);

            float mpRegen = Mathf.Max(0f, _combatStats.MpRegen);
            if (mpRegen > 0f && _currentMp < MaxMp)
                RestoreMp(mpRegen * dt);
        }

        private void TickTemporaryCombatFlags(float dt)
        {
            if (dt <= 0f)
                return;

            if (_hardControlTimer > 0f)
                _hardControlTimer = Mathf.Max(0f, _hardControlTimer - dt);
            if (_temporarySuperArmorTimer > 0f)
                _temporarySuperArmorTimer = Mathf.Max(0f, _temporarySuperArmorTimer - dt);
            if (_temporaryInvincibleTimer > 0f)
                _temporaryInvincibleTimer = Mathf.Max(0f, _temporaryInvincibleTimer - dt);
        }

        private void ApplyVitalCaps(float previousMaxHp, float previousMaxMp, bool resetVitals)
        {
            float currentMaxHp = Mathf.Max(1f, _combatStats.MaxHp);
            float currentMaxMp = Mathf.Max(0f, _combatStats.MaxMp);
            float missingHp = resetVitals || previousMaxHp <= 0f ? 0f : Mathf.Max(0f, previousMaxHp - _currentHp);
            float missingMp = resetVitals || previousMaxMp <= 0f ? 0f : Mathf.Max(0f, previousMaxMp - _currentMp);
            _currentHp = resetVitals ? currentMaxHp : Mathf.Clamp(currentMaxHp - missingHp, 0f, currentMaxHp);
            _currentMp = resetVitals ? currentMaxMp : Mathf.Clamp(currentMaxMp - missingMp, 0f, currentMaxMp);
        }

        private void ClampVitals()
        {
            float currentMaxHp = Mathf.Max(1f, MaxHp);
            float currentMaxMp = Mathf.Max(0f, MaxMp);
            _currentHp = Mathf.Clamp(_currentHp, 0f, currentMaxHp);
            _currentMp = Mathf.Clamp(_currentMp, 0f, currentMaxMp);
        }

        private void RefreshOwnerSharedStats()
        {
            if (_owner?.PlayerModel == null)
                return;

            int buildSignature = ResolveOwnerSharedBuildSignature();
            if (_ownerSharedStats != null && _ownerSharedBuildSignature == buildSignature)
                return;

            SkillConfigDatabaseSO skillConfig = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            BuffConfigSO buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            _ownerSharedStats = _owner.PlayerModel.BuildCloneSharedStatsSnapshot(
                skillConfig,
                buffConfig != null ? buffConfig.GetModifierForBuff : null);
            _ownerSharedBuildSignature = buildSignature;
        }

        private int ResolveOwnerSharedBuildSignature()
        {
            if (_owner?.PlayerModel == null)
                return 0;

            unchecked
            {
                int hash = 17;
                PlayerModel ownerModel = _owner.PlayerModel;
                hash = hash * 31 + ownerModel.Exp.Level;

                WeaponInstance weapon = ownerModel.Equipment.EquippedWeapon;
                if (weapon != null)
                {
                    hash = hash * 31 + (weapon.weaponId != null ? StringComparer.Ordinal.GetHashCode(weapon.weaponId) : 0);
                    hash = hash * 31 + (int)weapon.type;
                    hash = hash * 31 + (int)weapon.rarity;
                    hash = hash * 31 + weapon.rolledHp.GetHashCode();
                    hash = hash * 31 + weapon.rolledMp.GetHashCode();
                    hash = hash * 31 + weapon.rolledAttack.GetHashCode();
                    hash = hash * 31 + weapon.rolledDefense.GetHashCode();
                    hash = hash * 31 + weapon.rolledHpRegen.GetHashCode();
                    hash = hash * 31 + weapon.rolledMpRegen.GetHashCode();
                    hash = hash * 31 + weapon.rolledCritRate.GetHashCode();
                    hash = hash * 31 + weapon.rolledCritDmg.GetHashCode();
                    hash = hash * 31 + weapon.rolledAttackSpeed.GetHashCode();
                    hash = hash * 31 + weapon.rolledMoveSpeed.GetHashCode();
                }

                if (ownerModel.BuffIds != null)
                {
                    for (int i = 0; i < ownerModel.BuffIds.Count; i++)
                    {
                        string buffId = ownerModel.BuffIds[i];
                        if (string.IsNullOrWhiteSpace(buffId)
                            || string.Equals(buffId, BuffIds.SummonClone, System.StringComparison.Ordinal))
                        {
                            continue;
                        }

                        hash = hash * 31 + StringComparer.Ordinal.GetHashCode(buffId);
                    }
                }

                if (ownerModel.UnlockedSkillIds != null)
                {
                    int skillHash = 0;
                    foreach (string skillId in ownerModel.UnlockedSkillIds)
                    {
                        if (string.IsNullOrWhiteSpace(skillId))
                            continue;

                        skillHash ^= StringComparer.Ordinal.GetHashCode(skillId);
                    }

                    hash = hash * 31 + skillHash;
                }

                return hash;
            }
        }

        private void RebuildVisual()
        {
            DestroyInstancedMaterials();

            _animator = gameObject.GetComponent<Animator>();
            if (_animator == null)
                _animator = gameObject.AddComponent<Animator>();

            Transform visualRoot = transform.Find("Root");
            if (visualRoot == null)
            {
                if (_owner == null)
                    return;

                Animator sourceAnimator = _owner.GetComponentInChildren<Animator>(true);
                if (sourceAnimator == null)
                    return;

                Transform sourceVisualRoot = sourceAnimator.transform.Find("Root");
                if (sourceVisualRoot == null && sourceAnimator.transform.childCount > 0)
                    sourceVisualRoot = sourceAnimator.transform.GetChild(0);
                if (sourceVisualRoot == null)
                    return;

                ClearVisualChildren();
                GameObject visualClone = Instantiate(sourceVisualRoot.gameObject, transform);
                visualClone.name = sourceVisualRoot.name;
                RebindExternalSkinnedMeshBones(visualClone.transform);
                DisableCloneBehaviours(visualClone);
                visualRoot = visualClone.transform;
                _animator.avatar = sourceAnimator.avatar;
                _animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
                _animator.cullingMode = sourceAnimator.cullingMode;
            }

            if ((_animator.avatar == null || _animator.runtimeAnimatorController == null) && _owner != null)
            {
                Animator sourceAnimator = _owner.GetComponentInChildren<Animator>(true);
                if (sourceAnimator != null)
                {
                    _animator.avatar = sourceAnimator.avatar;
                    _animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
                    _animator.cullingMode = sourceAnimator.cullingMode;
                }
            }

            _animator.updateMode = AnimatorUpdateMode.Normal;
            _animator.applyRootMotion = false;

            _animatorController = gameObject.GetComponent<PlayerAnimatorController>();
            if (_animatorController == null)
                _animatorController = gameObject.AddComponent<PlayerAnimatorController>();

            ApplyOpacity();
            EnsureCloneGlowLight();
            _animator.Rebind();
            _animator.Update(0f);
            _animatorController.TriggerLocomotion();
        }

        private void EnterFollowLocomotion()
        {
            if (_isInFollowLocomotion)
                return;

            _isInFollowLocomotion = true;
            if (_timelineRunner == null)
            {
                _activeActionId = string.Empty;
                _actionLockTimer = 0f;
            }

            if (_animatorController != null)
            {
                _animatorController.SetPlaybackSpeed(1f);
                _animatorController.SetGrounded(true);
                _animatorController.TriggerLocomotion();
            }
        }

        private void ExitFollowLocomotion()
        {
            _isInFollowLocomotion = false;
        }

        private void EnterCombatLocomotion()
        {
            if (_isInCombatLocomotion)
                return;

            _isInCombatLocomotion = true;
            if (_animatorController != null)
            {
                _animatorController.SetPlaybackSpeed(1f);
                _animatorController.SetGrounded(true);
                _animatorController.TriggerLocomotion();
            }
        }

        private void ExitCombatLocomotion()
        {
            _isInCombatLocomotion = false;
        }

        private Vector3 ResolveFollowPatrolTarget(Vector3 followAnchor, float dt)
        {
            Vector3 ownerPosition = _owner != null ? _owner.transform.position : followAnchor;
            float distanceToOwner = Vector3.Distance(transform.position, ownerPosition);
            float regroupDistance = _aiConfig != null ? Mathf.Max(_settings.followRadius + 1f, _aiConfig.regroupDistance) : _settings.followRadius + 7f;
            if (distanceToOwner > regroupDistance * 0.75f)
            {
                _followPatrolTarget = followAnchor;
                _followPatrolTimer = 0.2f;
                _hasFollowPatrolTarget = true;
                return followAnchor;
            }

            _followPatrolTimer = Mathf.Max(0f, _followPatrolTimer - dt);
            bool reachedPatrolTarget = _hasFollowPatrolTarget
                && Vector3.Distance(transform.position, _followPatrolTarget) <= FollowPatrolArrivalDistance;
            bool anchorDrifted = _hasFollowPatrolTarget
                && Vector3.Distance(_followPatrolTarget, followAnchor) > FollowPatrolAnchorTolerance;
            if (!_hasFollowPatrolTarget || _followPatrolTimer <= 0f || reachedPatrolTarget || anchorDrifted)
                _followPatrolTarget = PickFollowPatrolTarget(followAnchor);

            return _followPatrolTarget;
        }

        private Vector3 PickFollowPatrolTarget(Vector3 followAnchor)
        {
            _followPatrolTimer = Random.Range(0.8f, 1.6f);
            _hasFollowPatrolTarget = true;

            if (Random.value <= 0.35f)
                return followAnchor;

            float patrolRadius = Mathf.Clamp(_settings.followRadius * 0.45f, 0.3f, 1.25f);
            Vector2 offset2D = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = followAnchor + new Vector3(offset2D.x, 0f, offset2D.y);
            return TrySampleNavMeshPosition(candidate, out Vector3 sampledPosition)
                ? sampledPosition
                : candidate;
        }

        private void ClearVisualChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name == CloneHudRootName)
                    continue;

                Destroy(child.gameObject);
            }
        }

        private void DisableCloneBehaviours(GameObject root)
        {
            if (root == null)
                return;

            Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (behaviour is Animator)
                {
                    behaviour.enabled = false;
                    continue;
                }

                behaviour.enabled = false;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
                rigidbodies[i].isKinematic = true;
        }

        private void ApplyOpacity()
        {
            PlayerCloneVisualSettings visualConfig = GetPlayerCloneVisualSettings();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Material[] materials = renderer.sharedMaterials;
                Material[] instanced = new Material[materials.Length];
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    if (source == null)
                        continue;

                    Material clone = new Material(source);
                    ApplyMaterialOpacity(clone, _settings.opacity, visualConfig);
                    _instancedMaterials.Add(clone);
                    instanced[materialIndex] = clone;
                }

                renderer.materials = instanced;
            }
        }

        private static void ApplyMaterialOpacity(Material material, float opacity, PlayerCloneVisualSettings visualConfig)
        {
            if (material == null)
                return;

            ConfigureTransparentMaterial(material);
            float alpha = ResolveCloneMaterialAlpha(material, opacity, visualConfig);

            if (material.HasProperty("_Color"))
            {
                Color color = material.color;
                color = BlendCloneTint(color, alpha, ResolveCloneTintStrength(material, visualConfig), ResolveCloneTintColor(visualConfig));
                color.a = alpha;
                material.color = color;
            }

            if (material.HasProperty("_BaseColor"))
            {
                Color color = material.GetColor("_BaseColor");
                color = BlendCloneTint(color, alpha, ResolveCloneTintStrength(material, visualConfig), ResolveCloneTintColor(visualConfig));
                color.a = alpha;
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                float emissionStrength = ResolveCloneEmissionStrength(material, visualConfig);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", ResolveCloneEmissionColor(visualConfig) * emissionStrength);
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SurfaceType"))
                material.SetFloat("_SurfaceType", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha"))
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha"))
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);
            if (material.HasProperty("_ZWriteControl"))
                material.SetFloat("_ZWriteControl", 1f);
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void EnsureCloneGlowLight()
        {
            PlayerCloneVisualSettings visualConfig = GetPlayerCloneVisualSettings();
            if (_cloneGlowLight == null)
            {
                Transform existing = transform.Find("CloneGlowLight");
                if (existing != null)
                    _cloneGlowLight = existing.GetComponent<Light>();
            }

            if (_cloneGlowLight == null)
            {
                GameObject lightObject = new GameObject("CloneGlowLight");
                lightObject.transform.SetParent(transform, false);
                _cloneGlowLight = lightObject.AddComponent<Light>();
            }

            if (_cloneGlowLight == null)
                return;

            _cloneGlowLight.type = LightType.Point;
            _cloneGlowLight.color = ResolveCloneEmissionColor(visualConfig);
            _cloneGlowLight.range = ResolveCloneGlowRange(visualConfig);
            _cloneGlowLight.intensity = ResolveCloneGlowMaxIntensity(visualConfig);
            _cloneGlowLight.shadows = LightShadows.None;
            _cloneGlowLight.renderMode = LightRenderMode.ForcePixel;
            _cloneGlowLight.transform.localPosition = ResolveCloneGlowOffset(visualConfig);
        }

        private void TickCloneGlowLight()
        {
            if (_cloneGlowLight == null)
                return;

            PlayerCloneVisualSettings visualConfig = GetPlayerCloneVisualSettings();
            _cloneGlowLight.transform.position = transform.position + ResolveCloneGlowOffset(visualConfig);
            float pulse = (Mathf.Sin(Time.time * ResolveCloneGlowPulseSpeed(visualConfig)) + 1f) * 0.5f;
            _cloneGlowLight.intensity = Mathf.Lerp(
                ResolveCloneGlowMinIntensity(visualConfig),
                ResolveCloneGlowMaxIntensity(visualConfig),
                pulse);
        }

        private static float ResolveCloneMaterialAlpha(Material material, float opacity, PlayerCloneVisualSettings visualConfig)
        {
            float baseAlpha = Mathf.Clamp01(Mathf.Max(opacity, ResolveCloneMinOpacity(visualConfig)));
            if (material == null)
                return baseAlpha;

            return IsSensitiveBodyMaterial(material)
                ? Mathf.Clamp01(baseAlpha * ResolveCloneSensitiveOpacityScale(visualConfig))
                : baseAlpha;
        }

        private static float ResolveCloneTintStrength(Material material, PlayerCloneVisualSettings visualConfig)
        {
            return IsSensitiveBodyMaterial(material)
                ? ResolveCloneSensitiveTintStrength(visualConfig)
                : ResolveCloneTintStrengthDefault(visualConfig);
        }

        private static float ResolveCloneEmissionStrength(Material material, PlayerCloneVisualSettings visualConfig)
        {
            return IsSensitiveBodyMaterial(material)
                ? ResolveCloneSensitiveEmissionStrength(visualConfig)
                : ResolveCloneEmissionStrengthDefault(visualConfig);
        }

        private static Color BlendCloneTint(Color source, float alpha, float tintStrength, Color tintColor)
        {
            Color sourceOpaque = new Color(source.r, source.g, source.b, 1f);
            Color tinted = Color.Lerp(sourceOpaque, tintColor, Mathf.Clamp01(tintStrength));
            tinted.a = alpha;
            return tinted;
        }

        private static Color ResolveCloneTintColor(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? visualConfig.tintColor : CloneTintColor;
        }

        private static Color ResolveCloneEmissionColor(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? visualConfig.emissionColor : CloneEmissionColor;
        }

        private static float ResolveCloneMinOpacity(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Clamp01(visualConfig.minOpacity) : 0.56f;
        }

        private static float ResolveCloneSensitiveOpacityScale(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Clamp01(visualConfig.sensitiveBodyOpacityScale) : 0.58f;
        }

        private static float ResolveCloneTintStrengthDefault(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Clamp01(visualConfig.tintStrength) : 0.62f;
        }

        private static float ResolveCloneSensitiveTintStrength(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Clamp01(visualConfig.sensitiveBodyTintStrength) : 0.88f;
        }

        private static float ResolveCloneEmissionStrengthDefault(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Max(0f, visualConfig.emissionStrength) : 1.25f;
        }

        private static float ResolveCloneSensitiveEmissionStrength(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Max(0f, visualConfig.sensitiveBodyEmissionStrength) : 1.7f;
        }

        private static Vector3 ResolveCloneGlowOffset(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? visualConfig.lightOffset : CloneGlowOffset;
        }

        private static float ResolveCloneGlowMinIntensity(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Max(0f, visualConfig.minLightIntensity) : CloneGlowMinIntensity;
        }

        private static float ResolveCloneGlowMaxIntensity(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Max(0f, visualConfig.maxLightIntensity) : CloneGlowMaxIntensity;
        }

        private static float ResolveCloneGlowPulseSpeed(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Max(0.1f, visualConfig.lightPulseSpeed) : CloneGlowPulseSpeed;
        }

        private static float ResolveCloneGlowRange(PlayerCloneVisualSettings visualConfig)
        {
            return visualConfig != null ? Mathf.Max(0.1f, visualConfig.lightRange) : CloneGlowRange;
        }

        private static bool IsSensitiveBodyMaterial(Material material)
        {
            if (material == null)
                return false;

            string name = material.name;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return ContainsNameToken(name, "body")
                || ContainsNameToken(name, "skin")
                || ContainsNameToken(name, "face")
                || ContainsNameToken(name, "head")
                || ContainsNameToken(name, "underwear")
                || ContainsNameToken(name, "panty")
                || ContainsNameToken(name, "bra")
                || ContainsNameToken(name, "nude")
                || name.Contains("身体", System.StringComparison.Ordinal)
                || name.Contains("皮肤", System.StringComparison.Ordinal)
                || name.Contains("脸", System.StringComparison.Ordinal)
                || name.Contains("头", System.StringComparison.Ordinal)
                || name.Contains("胸", System.StringComparison.Ordinal)
                || name.Contains("内裤", System.StringComparison.Ordinal);
        }

        private static bool ContainsNameToken(string source, string token)
        {
            return source?.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static PlayerCloneVisualSettings GetPlayerCloneVisualSettings()
        {
            return ConfigManager.GetInstance()?.GetPlayerCloneAndLevelBossVisualConfig()?.playerClone;
        }

        private Vector3 ResolveSteeringTarget(Vector3 desiredPosition, bool allowPathing)
        {
            Vector3 sampledDesired = TrySampleNavMeshPosition(desiredPosition, out Vector3 desiredHit)
                ? desiredHit
                : desiredPosition;

            if (!allowPathing)
                return sampledDesired;

            Vector3 sampledCurrent = TrySampleNavMeshPosition(transform.position, out Vector3 currentHit)
                ? currentHit
                : transform.position;

            Vector3 toDesired = sampledDesired - sampledCurrent;
            toDesired.y = 0f;
            if (toDesired.sqrMagnitude <= 0.01f)
                return sampledDesired;

            if (_navMeshPath == null)
                _navMeshPath = new NavMeshPath();

            if (!NavMesh.CalculatePath(sampledCurrent, sampledDesired, NavMesh.AllAreas, _navMeshPath))
                return sampledDesired;

            if (_navMeshPath.status == NavMeshPathStatus.PathInvalid)
                return sampledDesired;

            Vector3[] corners = _navMeshPath.corners;
            if (corners != null && corners.Length > 1)
            {
                Vector3 nextCorner = corners[1];
                return TrySampleNavMeshPosition(nextCorner, out Vector3 cornerHit)
                    ? cornerHit
                    : nextCorner;
            }

            return sampledDesired;
        }

        private static bool TrySampleNavMeshPosition(Vector3 source, out Vector3 sampledPosition)
        {
            if (NavMesh.SamplePosition(source, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas))
            {
                sampledPosition = hit.position;
                return true;
            }

            sampledPosition = source;
            return false;
        }

        private void ConfigureNavigationSupport()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            if (_navMeshAgent == null)
                return;

            _navMeshAgent.updatePosition = false;
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.isStopped = true;
            SyncNavigationSupport(true);
        }

        private void SyncNavigationSupport(bool allowWarp = false)
        {
            if (_navMeshAgent == null || !_navMeshAgent.enabled)
                return;

            if (_navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.nextPosition = transform.position;
                return;
            }

            if (!allowWarp)
                return;

            if (TrySampleNavMeshPosition(transform.position, out Vector3 sampledPosition))
                _navMeshAgent.Warp(sampledPosition);
        }

        private void SnapToGround(Vector3 referencePosition)
        {
            if (TrySampleNavMeshPosition(referencePosition, out Vector3 sampledPosition))
            {
                transform.position = sampledPosition;
                SyncNavigationSupport(true);
                return;
            }

            if (TrySampleNavMeshPosition(transform.position, out sampledPosition))
                transform.position = sampledPosition;
            SyncNavigationSupport(true);
        }

        private static void RebindExternalSkinnedMeshBones(Transform clonedRoot)
        {
            if (clonedRoot == null)
                return;

            Transform[] transforms = clonedRoot.GetComponentsInChildren<Transform>(true);
            Dictionary<string, Transform> localTransformsByName = new Dictionary<string, Transform>(transforms.Length);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null || localTransformsByName.ContainsKey(current.name))
                    continue;

                localTransformsByName.Add(current.name, current);
            }

            SkinnedMeshRenderer[] skinnedMeshRenderers = clonedRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedMeshRenderers[i];
                if (renderer == null)
                    continue;

                Transform[] bones = renderer.bones;
                bool bonesChanged = false;
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    Transform bone = bones[boneIndex];
                    if (bone == null || IsWithinHierarchy(clonedRoot, bone))
                        continue;

                    if (!localTransformsByName.TryGetValue(bone.name, out Transform replacementBone))
                        continue;

                    bones[boneIndex] = replacementBone;
                    bonesChanged = true;
                }

                if (bonesChanged)
                    renderer.bones = bones;

                Transform rootBone = renderer.rootBone;
                if (rootBone != null
                    && !IsWithinHierarchy(clonedRoot, rootBone)
                    && localTransformsByName.TryGetValue(rootBone.name, out Transform replacementRootBone))
                {
                    renderer.rootBone = replacementRootBone;
                }
            }
        }

        private static bool IsWithinHierarchy(Transform root, Transform candidate)
        {
            return candidate == root || (candidate != null && candidate.IsChildOf(root));
        }

        private void DestroyInstancedMaterials()
        {
            for (int i = 0; i < _instancedMaterials.Count; i++)
            {
                Material material = _instancedMaterials[i];
                if (material != null)
                    Destroy(material);
            }

            _instancedMaterials.Clear();
        }

        private void AssignCombatLayer()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                gameObject.layer = playerLayer;
        }

        private void EnsureHeadHealthBar()
        {
            if (_headHealthBar == null)
                _headHealthBar = GetComponent<ActorHeadHealthBar>();
            if (_headHealthBar == null)
                _headHealthBar = gameObject.AddComponent<ActorHeadHealthBar>();

            _headHealthBar.Configure(CloneHeadHealthBarColor);
        }

        private void UpdateHeadHealthBar()
        {
            EnsureHeadHealthBar();
            if (_headHealthBar == null)
                return;

            _headHealthBar.SetNormalized(CurrentHpRatio);
            _headHealthBar.SetVisible(IsAlive);
        }

        private void RefreshHeadHealthBarOnDeath()
        {
            if (_headHealthBar == null)
                _headHealthBar = GetComponent<ActorHeadHealthBar>();
            if (_headHealthBar == null)
                return;

            _headHealthBar.SetNormalized(0f);
        }

        private void RefreshHeadHealthBarImmediate()
        {
            EnsureHeadHealthBar();
            if (_headHealthBar == null)
                return;

            _headHealthBar.RefreshHeightOffset();
            _headHealthBar.SetNormalized(CurrentHpRatio);
            _headHealthBar.SetVisible(IsAlive);
        }

        private void OnEnable()
        {
            if (!ActiveCloneActors.Contains(this))
                ActiveCloneActors.Add(this);
        }

        private void OnDisable()
        {
            StopDetachedTimelineRunners();
            ActiveCloneActors.Remove(this);
        }

        private void OnDestroy()
        {
            _timelineRunner?.Stop();
            StopDetachedTimelineRunners();
            ActiveCloneActors.Remove(this);
            DestroyInstancedMaterials();
        }

        private static int ResolveOrbitSign(int formationIndex)
        {
            return (formationIndex & 1) == 0 ? 1 : -1;
        }
    }
}
