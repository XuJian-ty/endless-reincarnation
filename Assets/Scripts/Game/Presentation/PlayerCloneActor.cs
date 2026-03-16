using System.Collections.Generic;
using Game;
using Game.Data;
using Game.Domain;
using Game.GameFlow;
using UnityEngine;

namespace Game.Presentation
{
    public sealed class PlayerCloneActor : MonoBehaviour
    {
        private enum CloneDecisionType
        {
            None,
            Dodge,
            Skill,
            Charge,
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

        private sealed class CloneSkillExecutionContext : ISkillExecutionContext
        {
            private static readonly Collider[] SharedBuffer = new Collider[32];
            private readonly PlayerCloneActor _actor;

            public CloneSkillExecutionContext(PlayerCloneActor actor)
            {
                _actor = actor;
            }

            public Transform CasterTransform => _actor != null ? _actor.transform : null;
            public float CasterAttack => _actor != null && _actor.Owner != null ? _actor.Owner.PlayerModel.Stats.Attack * 0.5f : 0f;
            public Collider[] OverlapBuffer => SharedBuffer;
        }

        private readonly List<Material> _instancedMaterials = new List<Material>();
        private readonly Dictionary<int, float> _activeSkillCooldowns = new Dictionary<int, float>(4);
        private readonly List<int> _cooldownUpdateSlots = new List<int>(4);
        private readonly List<float> _cooldownUpdateValues = new List<float>(4);
        private readonly List<int> _cooldownExpiredSlots = new List<int>(4);
        private EnemyController _target;
        private PlayerController _owner;
        private SummonCloneBuffSettings _settings;
        private Animator _animator;
        private PlayerAnimatorController _animatorController;
        private SkillTimelineRunner _timelineRunner;
        private PlayerCloneAIConfigSO _aiConfig;
        private int _formationIndex;
        private int _formationCount = 1;
        private int _orbitSign = 1;
        private float _targetRefreshTimer;
        private float _actionLockTimer;
        private float _decisionTimer;
        private float _dodgeCooldownTimer;
        private float _chargeCooldownTimer;
        private float _dodgeMotionTimer;
        private float _currentHp;
        private float _maxHp;
        private int _comboStage;
        private string _activeActionId;
        private Vector3 _dodgeVelocity;

        public PlayerController Owner => _owner;
        public float MaxHp => _maxHp;
        public float CurrentHp => _currentHp;

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
            _actionLockTimer = 0f;
            _comboStage = 0;
            _activeActionId = string.Empty;
            _dodgeVelocity = Vector3.zero;
            RefreshDerivedStats();
            RebuildVisual();
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
        }

        public void SetFormation(int formationIndex, int formationCount)
        {
            _formationIndex = formationIndex;
            _formationCount = Mathf.Max(1, formationCount);
            _orbitSign = ResolveOrbitSign(formationIndex);
        }

        private void Update()
        {
            if (_owner == null || _owner.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            float dt = Time.deltaTime;
            RefreshDerivedStats();
            TickSkillCooldowns(dt);
            TickLocalCooldowns(dt);
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
                TickFollow(dt);
                return;
            }

            if (ShouldRegroupToOwner())
            {
                TickFollow(dt);
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

            Vector3 center = _owner.transform.position;
            float angleStep = 360f / Mathf.Max(1, _formationCount);
            float angle = angleStep * _formationIndex;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.right * _settings.followRadius;
            Vector3 targetPosition = center + offset;
            targetPosition.y = center.y;

            Vector3 delta = targetPosition - transform.position;
            float moveSpeed = Mathf.Max(1f, _owner.PlayerModel.Stats.MoveSpeed);
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * dt);

            if (delta.sqrMagnitude > 0.0001f)
            {
                Vector3 planar = delta;
                planar.y = 0f;
                if (planar.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(planar.normalized), 720f * dt);
            }
            else
            {
                Vector3 ownerForward = _owner.transform.forward;
                ownerForward.y = 0f;
                if (ownerForward.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(ownerForward.normalized), 720f * dt);
            }

            if (_animatorController != null)
            {
                _animatorController.SetPlaybackSpeed(1f);
                _animatorController.SetGrounded(true);
                _animatorController.SetLocomotionSpeed(delta.sqrMagnitude > 0.01f ? 1f : 0f);
            }
        }

        private void TickCombatMove(float dt)
        {
            if (_target == null || !_target.IsAlive || _owner == null)
            {
                TickFollow(dt);
                return;
            }

            Vector3 targetPosition = _target.transform.position;
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float distanceToTarget = toTarget.magnitude;

            Vector3 desiredPoint = ResolveCombatAnchor(targetPosition, distanceToTarget);
            Vector3 moveDelta = desiredPoint - transform.position;
            moveDelta.y = 0f;

            float combatMoveSpeedRatio = _aiConfig != null ? Mathf.Max(0.1f, _aiConfig.combatMoveSpeedRatio) : CombatMoveSpeedRatio;
            float orbitArrivalDistance = _aiConfig != null ? Mathf.Max(0.05f, _aiConfig.orbitArrivalDistance) : OrbitArrivalDistance;
            float moveSpeed = Mathf.Max(1f, _owner.PlayerModel.Stats.MoveSpeed) * combatMoveSpeedRatio;
            bool shouldMove = moveDelta.sqrMagnitude > orbitArrivalDistance * orbitArrivalDistance;
            if (shouldMove)
                transform.position = Vector3.MoveTowards(transform.position, desiredPoint, moveSpeed * dt);

            Vector3 facing = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : _owner.transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 900f * dt);

            if (_animatorController != null)
            {
                _animatorController.SetPlaybackSpeed(1f);
                _animatorController.SetGrounded(true);
                _animatorController.SetLocomotionSpeed(shouldMove ? 1f : 0f);
            }
        }

        private void TickTimeline(float dt)
        {
            if (_timelineRunner == null)
                return;

            _timelineRunner.Tick(dt);
            if (!_timelineRunner.IsComplete)
                return;

            _timelineRunner = null;
            _animatorController?.SetPlaybackSpeed(1f);
            _animatorController?.TriggerLocomotion();
            if (TryContinueActionSequence())
                return;

            _activeActionId = string.Empty;
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

            if (!invalidTarget && _targetRefreshTimer > 0f)
                return;

            _target = FindBestTarget();
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

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                float distanceToClone = Vector3.Distance(origin, enemy.transform.position);
                float distanceToOwner = Vector3.Distance(_owner.transform.position, enemy.transform.position);
                float ownerLeashDistance = _aiConfig != null ? Mathf.Max(8f, _aiConfig.ownerLeashDistance) : MaxTargetOwnerDistance;
                if (distanceToOwner > ownerLeashDistance)
                    continue;

                float score = distanceToClone + distanceToOwner * 0.3f + enemy.CurrentHpRatio * 1.2f;
                if (enemy.IsHurt)
                    score -= 1.8f;
                if (enemy.IsInPostCastRecovery)
                    score -= 2.6f;
                if (enemy.IsCastingSkill)
                    score -= 0.7f;
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
            if (_target == null || !_target.IsAlive)
                return false;

            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            float attackCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.attackCastRange) : AttackCastRange;
            if (toTarget.sqrMagnitude > attackCastRange * attackCastRange)
                return false;

            _comboStage = 0;
            return BeginConfiguredAction("Attack0");
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
                int slotIndex = ResolveActiveSkillSlotIndex(entry);
                if (slotIndex < 0 || GetActiveSkillCooldownRemaining(slotIndex) > 0f)
                    continue;
                candidates.Add(entry);
            }

            if (candidates.Count <= 0)
                return false;

            float skillCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.skillCastRange) : SkillCastRange;
            float distanceToTarget = Vector3.Distance(transform.position, _target.transform.position);
            if (distanceToTarget > skillCastRange)
                return false;

            SkillConfigEntry selected = SelectBestActiveSkill(candidates, distanceToTarget);
            if (selected == null)
                return false;

            StartActiveSkillCooldown(selected);
            return BeginConfiguredAction(selected.GetResolvedActionId());
        }

        private bool BeginConfiguredAction(string actionId, float overrideDuration = -1f)
        {
            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            SkillConfigEntry entry = skillDb != null ? skillDb.GetEntryByActionId(actionId) : null;
            if (entry == null)
                return false;

            string triggerName = entry.GetResolvedAnimationTrigger();
            _animatorController?.SetPlaybackSpeed(PlayerBuffRuntimeUtility.GetActionPlaybackSpeed(_owner.PlayerModel, actionId));
            _animatorController?.TriggerAction(triggerName);
            _activeActionId = actionId ?? string.Empty;

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
            float playbackSpeed = PlayerBuffRuntimeUtility.GetActionPlaybackSpeed(_owner.PlayerModel, actionId);
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

            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            Vector3 facingToTarget = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
            facingToTarget.y = 0f;
            if (facingToTarget.sqrMagnitude <= 0.0001f)
                facingToTarget = Vector3.forward;

            if (distanceToTarget < tooCloseDistance)
                return transform.position - facingToTarget * (tooCloseDistance - distanceToTarget + 0.4f);

            if (_target != null && (_target.IsHurt || _target.IsInPostCastRecovery))
                preferredAttackDistance = Mathf.Max(1.6f, preferredAttackDistance - 0.45f);

            if (distanceToTarget > preferredAttackDistance + attackDistanceTolerance)
                return targetPosition - facingToTarget * preferredAttackDistance;

            Vector3 toOwner = _owner.transform.position - targetPosition;
            toOwner.y = 0f;
            Vector3 baseDir = toOwner.sqrMagnitude > 0.0001f ? -toOwner.normalized : -facingToTarget;
            if (baseDir.sqrMagnitude <= 0.0001f)
                baseDir = -facingToTarget;

            float orbitWeight = _aiConfig != null ? Mathf.Clamp01(_aiConfig.orbitBias) : 0.85f;
            Vector3 orbitDir = Quaternion.Euler(0f, orbitAngle * _orbitSign * Mathf.Lerp(0.75f, 1.15f, orbitWeight), 0f) * baseDir;
            return targetPosition + orbitDir.normalized * orbitOffsetDistance;
        }

        private bool EvaluateAndExecuteCombatDecision()
        {
            if (_target == null || !_target.IsAlive)
                return false;

            float distanceToTarget = Vector3.Distance(transform.position, _target.transform.position);
            SkillConfigEntry bestSkillEntry = ResolveBestActiveSkill(distanceToTarget, out float skillScore);
            float dodgeScore = EvaluateDodgeScore(distanceToTarget);
            float chargeScore = EvaluateChargeScore(distanceToTarget);
            float attackScore = EvaluateAttackScore(distanceToTarget);

            CloneDecisionType decision = CloneDecisionType.None;
            float bestScore = 0f;

            ConsiderDecision(CloneDecisionType.Dodge, dodgeScore, ref decision, ref bestScore);
            ConsiderDecision(CloneDecisionType.Skill, skillScore, ref decision, ref bestScore);
            ConsiderDecision(CloneDecisionType.Charge, chargeScore, ref decision, ref bestScore);
            ConsiderDecision(CloneDecisionType.Attack, attackScore, ref decision, ref bestScore);

            return decision switch
            {
                CloneDecisionType.Dodge => TryStartDodge(),
                CloneDecisionType.Skill => bestSkillEntry != null && ExecuteActiveSkill(bestSkillEntry),
                CloneDecisionType.Charge => TryStartChargeAttack(),
                CloneDecisionType.Attack => TryCastAttack(),
                _ => false,
            };
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

                int slotIndex = ResolveActiveSkillSlotIndex(entry);
                if (slotIndex < 0 || GetActiveSkillCooldownRemaining(slotIndex) > 0f)
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

            if (!_target.IsCastingSkill && !_target.IsHurt)
                return 0f;

            float score = 0.25f + (_aiConfig != null ? _aiConfig.dodgeBias : 0.72f) * 0.85f;
            if (_target.IsCastingSkill)
                score += 0.35f;
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

        private bool ExecuteActiveSkill(SkillConfigEntry entry)
        {
            if (entry == null)
                return false;

            StartActiveSkillCooldown(entry);
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

            _dodgeMotionTimer = Mathf.Max(0f, _dodgeMotionTimer - dt);
            transform.position += _dodgeVelocity * dt;
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
                case "Attack0":
                case "Attack1":
                case "Attack2":
                {
                    int currentStage = ParseAttackComboIndex(_activeActionId);
                    int nextStage = currentStage + 1;
                    if (nextStage <= 3 && ShouldContinueAttackCombo(nextStage))
                    {
                        _comboStage = nextStage;
                        return BeginConfiguredAction($"Attack{nextStage}");
                    }

                    _comboStage = 0;
                    return false;
                }
                case "Attack3":
                    _comboStage = 0;
                    return false;
                default:
                    return false;
            }
        }

        private bool ShouldContinueAttackCombo(int nextStage)
        {
            if (_target == null || !_target.IsAlive)
                return false;

            float attackCastRange = _aiConfig != null ? Mathf.Max(0.5f, _aiConfig.attackCastRange) : AttackCastRange;
            float distanceToTarget = Vector3.Distance(transform.position, _target.transform.position);
            if (distanceToTarget > attackCastRange + 0.45f)
                return false;

            float continueChance = 0.35f + (_aiConfig != null ? _aiConfig.comboBias : 0.72f) * 0.45f;
            continueChance += (_aiConfig != null ? _aiConfig.aggression : 0.82f) * 0.15f;
            if (_target.IsHurt || _target.IsInPostCastRecovery)
                continueChance += 0.2f;

            return Random.value <= Mathf.Clamp01(continueChance);
        }

        private static int ParseAttackComboIndex(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId) || !actionId.StartsWith("Attack", System.StringComparison.Ordinal))
                return 0;

            string suffix = actionId.Substring("Attack".Length);
            return int.TryParse(suffix, out int index) ? index : 0;
        }

        private bool ShouldRegroupToOwner()
        {
            if (_owner == null)
                return false;

            float regroupDistance = _aiConfig != null ? Mathf.Max(_settings.followRadius + 1f, _aiConfig.regroupDistance) : _settings.followRadius + 7f;
            return Vector3.Distance(transform.position, _owner.transform.position) > regroupDistance;
        }

        private void ResetDecisionTimer()
        {
            _decisionTimer = _aiConfig != null ? Mathf.Max(0f, _aiConfig.decisionInterval) : 0.06f;
        }

        private void RefreshDerivedStats()
        {
            if (_owner?.PlayerModel == null)
                return;

            float newMaxHp = Mathf.Max(1f, _owner.PlayerModel.Stats.MaxHp * 10f);
            if (_maxHp <= 0f)
            {
                _maxHp = newMaxHp;
                _currentHp = newMaxHp;
                return;
            }

            float hpRatio = Mathf.Clamp01(_currentHp / _maxHp);
            _maxHp = newMaxHp;
            _currentHp = _maxHp * hpRatio;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || _maxHp <= 0f)
                return;

            _currentHp = Mathf.Min(_maxHp, _currentHp + amount);
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

        private void RebuildVisual()
        {
            ClearVisualChildren();
            DestroyInstancedMaterials();

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

            GameObject visualClone = Instantiate(sourceVisualRoot.gameObject, transform);
            visualClone.name = sourceVisualRoot.name;

            _animator = gameObject.GetComponent<Animator>();
            if (_animator == null)
                _animator = gameObject.AddComponent<Animator>();
            _animator.avatar = sourceAnimator.avatar;
            _animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            _animator.cullingMode = sourceAnimator.cullingMode;
            _animator.updateMode = AnimatorUpdateMode.Normal;
            _animator.applyRootMotion = false;

            _animatorController = gameObject.GetComponent<PlayerAnimatorController>();
            if (_animatorController == null)
                _animatorController = gameObject.AddComponent<PlayerAnimatorController>();

            DisableCloneBehaviours(visualClone);
            ApplyOpacity();
            _animatorController.TriggerLocomotion();
        }

        private void ClearVisualChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
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
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                Material[] instanced = new Material[materials.Length];
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    if (source == null)
                        continue;

                    Material clone = new Material(source);
                    ApplyMaterialOpacity(clone, _settings.opacity);
                    _instancedMaterials.Add(clone);
                    instanced[materialIndex] = clone;
                }

                renderer.materials = instanced;
            }
        }

        private static void ApplyMaterialOpacity(Material material, float opacity)
        {
            if (material == null)
                return;

            if (material.HasProperty("_Color"))
            {
                Color color = material.color;
                color.a *= opacity;
                material.color = color;
            }

            if (material.HasProperty("_BaseColor"))
            {
                Color color = material.GetColor("_BaseColor");
                color.a *= opacity;
                material.SetColor("_BaseColor", color);
            }
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

        private void OnDestroy()
        {
            _timelineRunner?.Stop();
            DestroyInstancedMaterials();
        }

        private static int ResolveOrbitSign(int formationIndex)
        {
            return (formationIndex & 1) == 0 ? 1 : -1;
        }
    }
}
