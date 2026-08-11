using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Domain;
using Game.Data;
using Game.AI;
using Game.GameFlow;
using Game.Saving;
using Game;
using ProjectBase;

namespace Game.Presentation
{
    /// <summary>
    /// Enemy presentation facade: stats, damage/death, runtime modifiers, and skill-slot casting state.
    /// </summary>
    public class EnemyController : MonoBehaviour, ICombatHardControlReceiver
    {
        private static readonly List<EnemyController> ActiveEnemyControllers = new List<EnemyController>();

        [Serializable]
        private sealed class RuntimeStatModifier
        {
            public StatModifier modifier;
            public float endTime;
        }

        [Header("敌人标识（优先按 enemyId 查配置）")]
        [SerializeField] private string _enemyId = "";
        [SerializeField] private EnemyType _enemyType = EnemyType.MeleeMinion;

        [Header("行为配置（可选，不挂则按 enemyId 或类型从 ConfigManager 获取）")]
        [SerializeField] private EnemyArchetypeSO _archetypeOverride;

        [Header("节奏控制")]
        [SerializeField, Min(0f)] private float _deathDisableDelay = 1.2f;
        [SerializeField] private bool _countsAsLevelBoss = false;

        private Vector3 _baseVisualScale = Vector3.one;
        private bool _baseVisualScaleInitialized;
        private EnemyRuntimeStats _stats;
        private readonly List<RuntimeStatModifier> _runtimeModifiers = new List<RuntimeStatModifier>();
        private readonly Dictionary<int, float> _lastSkillCastTimeBySlot = new Dictionary<int, float>();
        private ActorHeadHealthBar _headHealthBar;

        private System.Random _rng;
        private bool _dead;
        private float _hurtRemainingTime;
        private bool _modifierCacheDirty = true;
        private float _bonusAttack;
        private float _bonusDefense;
        private float _bonusMoveSpeedRatio;
        private float _bonusHpRegen;
        private float _bonusLifeSteal;
        private float _bonusDamageBonus;
        private float _bonusDamageReduce;
        private EnemyResolvedSkill _activeSkill;
        private Vector3? _activeSkillTargetPosition;
        private int _activeSkillSequence;
        private readonly Collider[] _onlineRemoteVisualOverlapBuffer = new Collider[32];
        private SkillTimelineRunner _onlineRemoteVisualTimelineRunner;
        private int _onlineRemoteVisualSkillSequence;
        private const float OnlineAuthorityIntervalSeconds = 1.5f;
        private float _nextOnlineRemoteMovementLogTime;
        private float _nextOnlineRemoteHeartbeatLogTime;
        private float _lastOnlineRemoteMovementApplyTime;
        private float _lastOnlineRemoteSkillApplyTime;
        private int _lastLoggedMissingRemoteSkillSequence;
        private float _idleUntilTime;
        private float _decisionLockUntilTime;
        private bool _isInPostCastRecovery;
        private bool _hasPostCastRetreatDestination;
        private Vector3 _postCastRetreatDestination;
        private float _deathCleanupTime;
        private bool _deathFinalized;
        private float _animatorMoveBlend;
        private float _animatorMoveSigned;
        private float _animatorMoveStrafe;
        private bool _runtimeStateInitialized;
        private bool _loggedAmbiguousVariantError;
        private bool _loggedMissingStatsError;
        private bool _loggedMissingArchetypeError;
        private GameObject _poolSourcePrefab;
        private bool _onlineRemoteSimulationDisabled;
        private bool _onlineAuthorityCombatVisible;
        private float _currentPoise;
        private bool _poiseInitialized;
        private string _runtimeId;
        private float _temporarySuperArmorTimer;
        private float _temporaryInvincibleTimer;
        private int _stateScopedSuperArmorCount;
        private int _stateScopedInvincibleCount;

        private void Reset()
        {
            if (GetComponent<Collider>() == null)
                gameObject.AddComponent<CapsuleCollider>();
        }

        public float Defense
        {
            get
            {
                EnsureInitialized();
                RecalculateModifierCacheIfNeeded();
                return Mathf.Max(0f, _stats.defense + _bonusDefense);
            }
        }

        public float CurrentHp
        {
            get
            {
                EnsureInitialized();
                return _stats.currentHp;
            }
        }

        public float MaxHp
        {
            get
            {
                EnsureInitialized();
                return _stats.maxHp;
            }
        }

        public float Attack
        {
            get
            {
                EnsureInitialized();
                RecalculateModifierCacheIfNeeded();
                return Mathf.Max(0f, _stats.attack + _bonusAttack);
            }
        }

        public float MoveSpeed
        {
            get
            {
                EnsureInitialized();
                RecalculateModifierCacheIfNeeded();
                return Mathf.Max(0.1f, _stats.moveSpeed * (1f + _bonusMoveSpeedRatio));
            }
        }

        public float LifeSteal
        {
            get
            {
                EnsureInitialized();
                RecalculateModifierCacheIfNeeded();
                return Mathf.Clamp01(_bonusLifeSteal);
            }
        }

        public float DamageBonus
        {
            get
            {
                EnsureInitialized();
                RecalculateModifierCacheIfNeeded();
                return _bonusDamageBonus;
            }
        }

        public float DamageReduce
        {
            get
            {
                EnsureInitialized();
                RecalculateModifierCacheIfNeeded();
                return Mathf.Clamp01(_bonusDamageReduce);
            }
        }

        public EnemyType EnemyCategory
        {
            get
            {
                EnsureInitialized();
                return _stats.type;
            }
        }

        public string EnemyId
        {
            get
            {
                EnsureInitialized();
                if (!string.IsNullOrEmpty(_stats.enemyId))
                    return _stats.enemyId;
                if (!string.IsNullOrEmpty(_enemyId))
                    return _enemyId;
                return Archetype != null ? Archetype.GetResolvedEnemyId() : _enemyType.ToString();
            }
        }

        public bool IsAlive => !_dead;
        public static IReadOnlyList<EnemyController> ActiveEnemies => ActiveEnemyControllers;
        public string RuntimeId
        {
            get
            {
                EnsureRuntimeId();
                return _runtimeId;
            }
        }
        public void AssignPlacedRuntimeId(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                Debug.LogError($"[Enemy] {name} 的场景直摆运行时 ID 为空。", this);
                return;
            }

            _runtimeId = runtimeId.Trim();
        }
        public bool CountsAsLevelBoss => _countsAsLevelBoss;
        public EnemyIntent CurrentIntent { get; set; }
        public float HurtRemainingTime => _hurtRemainingTime;
        public bool IsHurt => _hurtRemainingTime > 0f;
        public EnemyArchetypeSO Archetype { get; private set; }
        public bool UsesAerialMovement => Archetype != null && Archetype.UsesAerialMovement();
        public EnemyResolvedSkill ActiveSkill => _activeSkill;
        public Vector3? ActiveSkillTargetPosition => _activeSkillTargetPosition;
        public int ActiveSkillSequence => _activeSkillSequence;
        public bool IsCastingSkill => _activeSkill != null;
        public bool IsDecisionLocked => !_dead && Time.time < _decisionLockUntilTime;
        public bool IsIdling => !_dead && CurrentIntent.Type == EnemyIntentType.Idle && Time.time < _idleUntilTime;
        public bool IsInPostCastRecovery => !_dead && _isInPostCastRecovery && Time.time < _idleUntilTime;
        public bool IsInCombatState
        {
            get
            {
                if (_dead)
                    return false;

                return CurrentIntent.Type switch
                {
                    EnemyIntentType.Search => true,
                    EnemyIntentType.Chase => true,
                    EnemyIntentType.Approach => true,
                    EnemyIntentType.Punish => true,
                    EnemyIntentType.Hold => true,
                    EnemyIntentType.StrafeLeft => true,
                    EnemyIntentType.StrafeRight => true,
                    EnemyIntentType.Dodge => true,
                    EnemyIntentType.Retreat => true,
                    EnemyIntentType.Reposition => true,
                    EnemyIntentType.CastSkill => true,
                    EnemyIntentType.Hurt => true,
                    EnemyIntentType.Idle => IsInPostCastRecovery,
                    _ => false,
                };
            }
        }
        public bool ShouldShowCombatHealthBar => IsAlive && (IsInCombatState || _onlineAuthorityCombatVisible);
        public bool IsInPatrolState => !_dead && !IsInCombatState;
        public bool HasSuperArmor => _temporarySuperArmorTimer > 0f || _stateScopedSuperArmorCount > 0;
        public bool HasInvincibility => _temporaryInvincibleTimer > 0f || _stateScopedInvincibleCount > 0;
        public float AnimatorMoveBlend => _animatorMoveBlend;
        public float AnimatorMoveSigned => _animatorMoveSigned;
        public float AnimatorMoveForward => _animatorMoveSigned;
        public float AnimatorMoveStrafe => _animatorMoveStrafe;
        public float CurrentPoise => _currentPoise;
        public float CurrentHpRatio
        {
            get
            {
                EnsureInitialized();
                return _stats.maxHp > 0.01f ? _stats.currentHp / _stats.maxHp : 0f;
            }
        }

        public void SetOnlineRemoteSimulationDisabled(bool disabled)
        {
            if (_onlineRemoteSimulationDisabled != disabled)
                Debug.Log($"[OnlineAuthority] Remote simulation flag changed. runtime={RuntimeId} enemyId={EnemyId} disabled={disabled} scene={gameObject.scene.name}");

            _onlineRemoteSimulationDisabled = disabled;
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null)
                ai.enabled = !disabled;

            EnemyMover mover = GetComponent<EnemyMover>();
            if (mover != null)
                mover.enabled = !disabled;

            EnemyCombat combat = GetComponent<EnemyCombat>();
            if (combat != null)
                combat.enabled = !disabled;
        }

        public Vector3 ResolveFlightAnchorPosition(Vector3 baseTargetPosition)
        {
            EnsureInitialized();
            if (!UsesAerialMovement || Archetype == null)
                return baseTargetPosition;

            baseTargetPosition.y += Archetype.flightHeightOffset;
            return baseTargetPosition;
        }

        public Vector3 ResolveFlightMoveDestination(Vector3 anchorPosition)
        {
            EnsureInitialized();
            if (!UsesAerialMovement || Archetype == null)
                return anchorPosition;

            float amplitude = Mathf.Max(0f, Archetype.flightHeightBobAmplitude);
            float frequency = Mathf.Max(0f, Archetype.flightHeightBobFrequency);
            if (amplitude <= 0f || frequency <= 0f)
                return anchorPosition;

            float phase = (GetInstanceID() & 255) * 0.173f;
            anchorPosition.y += Mathf.Sin(Time.time * frequency + phase) * amplitude;
            return anchorPosition;
        }

        private void Awake()
        {
            _baseVisualScale = transform.localScale;
            _baseVisualScaleInitialized = true;
            EnsureRuntimeId();
            RegisterActiveEnemy();
            _rng = new System.Random();
            InitializeRuntimeState();
            _countsAsLevelBoss = GetComponent<LevelBossVisualMarker>() != null;
            EnsureInitialized();
            EnsureHeadHealthBar();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public bool TryBuildSnapshot(out EnemySnapshot snapshot)
        {
            snapshot = null;
            EnsureInitialized();
            if (_dead || string.IsNullOrWhiteSpace(EnemyId))
                return false;

            Vector3 position = transform.position;
            Vector3? targetPosition = CurrentIntent.TargetPosition;
            snapshot = new EnemySnapshot
            {
                id = EnemyId,
                runtimeId = RuntimeId,
                enemyType = (int)EnemyCategory,
                x = position.x,
                y = position.y,
                z = position.z,
                yaw = transform.eulerAngles.y,
                currentHp = Mathf.Max(0f, CurrentHp),
                currentPoise = Mathf.Max(0f, _currentPoise),
                hurtRemainingTime = Mathf.Max(0f, _hurtRemainingTime),
                idleRemainingTime = Mathf.Max(0f, _idleUntilTime - Time.time),
                decisionLockRemainingTime = Mathf.Max(0f, _decisionLockUntilTime - Time.time),
                countsAsLevelBoss = IsFinalBoss(),
                intentType = (int)CurrentIntent.Type,
                hasTarget = targetPosition.HasValue,
                lastTargetX = targetPosition.HasValue ? targetPosition.Value.x : 0f,
                lastTargetZ = targetPosition.HasValue ? targetPosition.Value.z : 0f,
                skillCooldowns = new List<EnemySkillCooldownSave>(),
            };

            AppendSkillCooldowns(snapshot.skillCooldowns);
            return true;
        }

        public void RestoreFromSnapshot(EnemySnapshot snapshot)
        {
            if (snapshot == null)
                return;

            EnsureInitialized();
            if (!string.IsNullOrWhiteSpace(snapshot.runtimeId))
                _runtimeId = snapshot.runtimeId.Trim();

            transform.position = new Vector3(snapshot.x, snapshot.y, snapshot.z);
            transform.rotation = Quaternion.Euler(0f, snapshot.yaw, 0f);
            SetCountsAsLevelBoss(snapshot.countsAsLevelBoss);
            _dead = false;
            _deathFinalized = false;
            _deathCleanupTime = 0f;
            _stats.currentHp = Mathf.Clamp(snapshot.currentHp, 0f, Mathf.Max(1f, _stats.maxHp));
            EnsurePoiseInitialized();
            _currentPoise = Archetype != null
                ? Mathf.Clamp(snapshot.currentPoise, 0f, Mathf.Max(0f, Archetype.poiseMax))
                : Mathf.Max(0f, snapshot.currentPoise);

            _hurtRemainingTime = Mathf.Max(0f, snapshot.hurtRemainingTime);
            _idleUntilTime = Time.time + Mathf.Max(0f, snapshot.idleRemainingTime);
            _decisionLockUntilTime = Time.time + Mathf.Max(0f, snapshot.decisionLockRemainingTime);
            _activeSkill = null;
            _activeSkillTargetPosition = null;
            _activeSkillSequence = 0;
            ClearPostCastRecoveryState();
            RestoreSkillCooldowns(snapshot.skillCooldowns);
            CurrentIntent = BuildIntentFromSnapshot(snapshot);
            EnsureHeadHealthBar();
            UpdateHeadHealthBar();
        }

        private void Update()
        {
            EnsureInitialized();
            if (!_dead && GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            if (_onlineRemoteSimulationDisabled)
            {
                TickOnlineRemoteVisualTimeline(Time.deltaTime);
                LogOnlineRemoteHeartbeat();
                UpdateHeadHealthBar();
                return;
            }

            if (_dead)
            {
                if (!_deathFinalized && Time.time >= _deathCleanupTime)
                    FinalizeDeath();
                return;
            }

            if (_hurtRemainingTime > 0f)
                _hurtRemainingTime = Mathf.Max(0f, _hurtRemainingTime - Time.deltaTime);

            if (_temporarySuperArmorTimer > 0f)
                _temporarySuperArmorTimer = Mathf.Max(0f, _temporarySuperArmorTimer - Time.deltaTime);

            if (_temporaryInvincibleTimer > 0f)
                _temporaryInvincibleTimer = Mathf.Max(0f, _temporaryInvincibleTimer - Time.deltaTime);

            if (_isInPostCastRecovery && Time.time >= _idleUntilTime)
                ClearPostCastRecoveryState();

            if (_runtimeModifiers.Count > 0)
            {
                float now = Time.time;
                for (int i = _runtimeModifiers.Count - 1; i >= 0; i--)
                {
                    if (now <= _runtimeModifiers[i].endTime) continue;
                    _runtimeModifiers.RemoveAt(i);
                    _modifierCacheDirty = true;
                }
            }

            RecalculateModifierCacheIfNeeded();
            TickPoiseRecovery(Time.deltaTime);
            if (!_dead && _bonusHpRegen > 0f)
                Heal(_bonusHpRegen * Time.deltaTime);
            if (!_dead && IsInPatrolState && Archetype != null && Archetype.patrolHealPercentPerSecond > 0f)
                Heal(_stats.maxHp * Archetype.patrolHealPercentPerSecond * Time.deltaTime);
            UpdateHeadHealthBar();

            if (CurrentIntent.Type == EnemyIntentType.Idle &&
                !IsHurt &&
                Time.time >= _idleUntilTime &&
                !IsDecisionLocked)
            {
                CurrentIntent = EnemyIntent.None;
            }

            if (CurrentIntent.Type == EnemyIntentType.Hurt &&
                !IsHurt &&
                !IsDecisionLocked)
            {
                CurrentIntent = EnemyIntent.None;
            }
        }

        public void EnsureInitialized()
        {
            InitializeRuntimeState();

            var cfg = ConfigManager.GetInstance();
            if (cfg == null)
                return;

            string resolvedEnemyId = ResolveConfiguredEnemyId();
            bool canFallbackStatsToType = CanFallbackStatsToType(cfg, resolvedEnemyId);
            bool canFallbackArchetypeToType = CanFallbackArchetypeToType(cfg, resolvedEnemyId);

            int difficulty = 1;
            var gsm = GameStateMachine.GetInstance();
            if (gsm?.CurrentRun != null)
                difficulty = gsm.CurrentRun.difficulty;

            var statsDb = cfg.GetEnemyStatsDatabase();
            var scaling = cfg.GetDifficultyScaling();
            if (string.IsNullOrEmpty(_stats.enemyId) && statsDb != null && scaling != null)
            {
                if (!string.IsNullOrEmpty(resolvedEnemyId))
                    _stats = statsDb.GetScaled(resolvedEnemyId, difficulty, scaling);
                else if (canFallbackStatsToType)
                    _stats = statsDb.GetScaled(_enemyType, difficulty, scaling);

                if (string.IsNullOrEmpty(_stats.enemyId) && !_loggedMissingStatsError)
                {
                    Debug.LogError($"[Enemy] {name} 未找到敌人数值配置。enemyId={resolvedEnemyId}, enemyType={_enemyType}", this);
                    _loggedMissingStatsError = true;
                }
            }

            if (Archetype == null)
            {
                Archetype = _archetypeOverride;
                if (Archetype == null)
                {
                    if (!string.IsNullOrEmpty(resolvedEnemyId))
                        Archetype = cfg.GetEnemyArchetype(resolvedEnemyId);
                    else if (canFallbackArchetypeToType)
                        Archetype = cfg.GetEnemyArchetype(_enemyType);
                }

                if (Archetype == null && !_loggedMissingArchetypeError)
                {
                    Debug.LogError($"[Enemy] {name} 未找到敌人行为配置。enemyId={resolvedEnemyId}, enemyType={_enemyType}", this);
                    _loggedMissingArchetypeError = true;
                }
            }

            EnsurePoiseInitialized();
        }

        public EnemyResolvedSkill ResolveSkillSlot(int slot)
        {
            EnsureInitialized();
            if (Archetype == null) return null;
            return EnemySkillResolver.Resolve(Archetype, slot);
        }

        public float GetSkillRange(int slot)
        {
            var skill = ResolveSkillSlot(slot);
            return skill != null ? skill.CastRange : 0f;
        }

        public float GetSkillCooldownRemaining(int slot)
        {
            EnsureInitialized();
            var skill = ResolveSkillSlot(slot);
            if (skill == null)
                return 0f;

            if (!_lastSkillCastTimeBySlot.TryGetValue(slot, out float lastCastTime))
                return 0f;

            float elapsed = Time.time - lastCastTime;
            return Mathf.Max(0f, skill.Cooldown - elapsed);
        }

        public float GetPreferredCombatRange()
        {
            EnsureInitialized();
            if (Archetype?.skillSlots == null || Archetype.skillSlots.Count == 0)
                return 2.5f;

            float bestRange = float.MaxValue;
            for (int i = 0; i < Archetype.skillSlots.Count; i++)
            {
                var binding = Archetype.skillSlots[i];
                if (binding == null)
                    continue;

                if (!Archetype.IsSkillSlotAvailableInCurrentPhase(binding.slotIndex, CurrentHpRatio))
                    continue;

                float range = GetSkillRange(binding.slotIndex);
                if (range > 0.01f)
                    bestRange = Mathf.Min(bestRange, range);
            }

            if (bestRange < float.MaxValue)
                return bestRange;

            for (int i = 0; i < Archetype.skillSlots.Count; i++)
            {
                var binding = Archetype.skillSlots[i];
                if (binding == null)
                    continue;

                float range = GetSkillRange(binding.slotIndex);
                if (range > 0.01f)
                    bestRange = Mathf.Min(bestRange, range);
            }

            return bestRange < float.MaxValue ? bestRange : 2.5f;
        }

        public void SetAnimatorMoveBlend(float value)
        {
            SetAnimatorMove(value, value, 0f);
        }

        public void SetAnimatorMove(float blendAbs, float blendSigned)
        {
            SetAnimatorMove(blendAbs, blendSigned, 0f);
        }

        public void SetAnimatorMove(float blendAbs, float blendForwardSigned, float blendStrafeSigned)
        {
            _animatorMoveBlend = Mathf.Clamp01(blendAbs);
            _animatorMoveSigned = Mathf.Clamp(blendForwardSigned, -1f, 1f);
            _animatorMoveStrafe = Mathf.Clamp(blendStrafeSigned, -1f, 1f);
        }

        public void ApplyOnlineRemoteMovementPresentation(float moveBlend, float moveForward, float moveStrafe)
        {
            if (!_onlineRemoteSimulationDisabled)
            {
                if (Time.unscaledTime >= _nextOnlineRemoteMovementLogTime)
                {
                    _nextOnlineRemoteMovementLogTime = Time.unscaledTime + OnlineAuthorityIntervalSeconds;
                    Debug.Log($"[OnlineAuthority] Remote movement ignored because simulation is enabled. runtime={RuntimeId} enemyId={EnemyId} move=({moveBlend:F3},{moveForward:F3},{moveStrafe:F3}) scene={gameObject.scene.name}");
                }

                return;
            }

            SetAnimatorMove(moveBlend, moveForward, moveStrafe);
            _lastOnlineRemoteMovementApplyTime = Time.unscaledTime;
            Animator animator = GetComponent<Animator>();
            if (animator == null)
            {
                if (Time.unscaledTime >= _nextOnlineRemoteMovementLogTime)
                {
                    _nextOnlineRemoteMovementLogTime = Time.unscaledTime + OnlineAuthorityIntervalSeconds;
                    Debug.Log($"[OnlineAuthority] Remote movement has no animator. runtime={RuntimeId} enemyId={EnemyId} move=({moveBlend:F3},{moveForward:F3},{moveStrafe:F3}) scene={gameObject.scene.name}");
                }

                return;
            }

            bool hasMoveX = HasAnimatorParameter(animator, "MoveX", AnimatorControllerParameterType.Float);
            bool hasMoveY = HasAnimatorParameter(animator, "MoveY", AnimatorControllerParameterType.Float);
            if (hasMoveX)
                animator.SetFloat("MoveX", _animatorMoveStrafe);
            if (hasMoveY)
                animator.SetFloat("MoveY", _animatorMoveSigned);

            if (Time.unscaledTime >= _nextOnlineRemoteMovementLogTime && (moveBlend > 0.05f || moveForward > 0.05f || moveStrafe > 0.05f || !hasMoveX || !hasMoveY))
            {
                _nextOnlineRemoteMovementLogTime = Time.unscaledTime + OnlineAuthorityIntervalSeconds;
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"[OnlineAuthority] Remote movement applied. runtime={RuntimeId} enemyId={EnemyId} move=({moveBlend:F3},{moveForward:F3},{moveStrafe:F3}) hasMoveX={hasMoveX} hasMoveY={hasMoveY} animatorSpeed={animator.speed:F2} stateHash={stateInfo.shortNameHash} normalized={stateInfo.normalizedTime:F2} scene={gameObject.scene.name}");
            }
        }

        public bool CanCastSkill(int slot)
        {
            EnsureInitialized();
            return TryGetCastableSkill(slot, out _);
        }

        public bool TryStartSkillSlot(int slot, Vector3? targetPosition)
        {
            EnsureInitialized();
            if (!TryGetCastableSkill(slot, out var skill))
                return false;

            ClearPostCastRecoveryState();

            _activeSkill = skill;
            _activeSkillTargetPosition = targetPosition;
            _activeSkillSequence++;
            _lastSkillCastTimeBySlot[slot] = Time.time;

            CurrentIntent = new EnemyIntent
            {
                Type = EnemyIntentType.CastSkill,
                SkillSlot = slot,
                TargetPosition = targetPosition
            };
            return true;
        }

        public void SetCountsAsLevelBoss(bool value)
        {
            EnsureBaseVisualScale();
            if (!value)
            {
                _countsAsLevelBoss = false;
                transform.localScale = _baseVisualScale;
                LevelBossVisualMarker marker = GetComponent<LevelBossVisualMarker>();
                if (marker != null)
                    Destroy(marker);
                return;
            }

            _countsAsLevelBoss = true;
            float scaleMultiplier = ConfigManager.GetInstance()?.GetPlayerCloneAndLevelBossVisualConfig()?.levelBoss.scaleMultiplier ?? 3f;
            transform.localScale = _baseVisualScale * scaleMultiplier;
            if (GetComponent<LevelBossVisualMarker>() == null)
                gameObject.AddComponent<LevelBossVisualMarker>();
        }

        private void EnsureBaseVisualScale()
        {
            if (_baseVisualScaleInitialized)
                return;

            _baseVisualScale = transform.localScale;
            _baseVisualScaleInitialized = true;
        }

        public void PrepareForSpawn(GameObject poolSourcePrefab)
        {
            _poolSourcePrefab = poolSourcePrefab;
            _runtimeId = Guid.NewGuid().ToString("N");
            _runtimeStateInitialized = false;
            _poiseInitialized = false;
            _loggedAmbiguousVariantError = false;
            _loggedMissingStatsError = false;
            _loggedMissingArchetypeError = false;
            _onlineAuthorityCombatVisible = false;
            _countsAsLevelBoss = false;
            _baseVisualScale = transform.localScale;
            _baseVisualScaleInitialized = true;
            Archetype = null;
            _stats = default;

            InitializeRuntimeState();
            EnsureInitialized();
            _stats.currentHp = Mathf.Max(1f, _stats.maxHp);
            EnsurePoiseInitialized();
            if (Archetype != null)
                _currentPoise = Mathf.Max(0f, Archetype.poiseMax);

            CurrentIntent = EnemyIntent.None;
            EnsureHeadHealthBar();
            UpdateHeadHealthBar();
        }

        public void CompleteActiveSkill()
        {
            float postCastIdleDuration = _activeSkill != null
                ? Mathf.Max(0f, _activeSkill.PostCastIdleDuration)
                : 0f;
            Vector3? targetPosition = _activeSkillTargetPosition;

            _activeSkill = null;
            _activeSkillTargetPosition = null;
            if (CurrentIntent.Type == EnemyIntentType.CastSkill)
            {
                if (postCastIdleDuration > 0f)
                    EnterPostCastRecovery(postCastIdleDuration, targetPosition);
                else
                {
                    ClearPostCastRecoveryState();
                    CurrentIntent = EnemyIntent.None;
                }
            }
        }

        public void CancelActiveSkill()
        {
            if (_activeSkill == null) return;
            _activeSkill = null;
            _activeSkillTargetPosition = null;
            if (CurrentIntent.Type == EnemyIntentType.CastSkill)
                CurrentIntent = EnemyIntent.None;
        }

        public void SetHurt(float duration)
        {
            if (_dead) return;
            float validDuration = Mathf.Max(0f, duration);
            if (validDuration <= 0f)
                return;

            EnemySquadCoordinator.Release(this);
            ClearPostCastRecoveryState();

            if (validDuration > _hurtRemainingTime)
                _hurtRemainingTime = validDuration;

            _decisionLockUntilTime = Mathf.Max(_decisionLockUntilTime, Time.time + _hurtRemainingTime);

            if (CurrentIntent.Type != EnemyIntentType.Dead)
                CurrentIntent = new EnemyIntent { Type = EnemyIntentType.Hurt };
        }

        public void ApplyHardControl(float duration)
        {
            if (_dead)
                return;

            float validDuration = Mathf.Max(0f, duration);
            if (validDuration <= 0f)
                return;

            CancelActiveSkill();
            ClearPostCastRecoveryState();
            SetHurt(validDuration);
        }

        public void ApplyTemporarySuperArmor(float duration)
        {
            if (duration <= 0f)
                return;

            _temporarySuperArmorTimer = Mathf.Max(_temporarySuperArmorTimer, duration);
        }

        public void AddStateScopedSuperArmor()
        {
            _stateScopedSuperArmorCount++;
        }

        public void RemoveStateScopedSuperArmor()
        {
            _stateScopedSuperArmorCount = Mathf.Max(0, _stateScopedSuperArmorCount - 1);
        }

        public void ApplyTemporaryInvincibility(float duration)
        {
            if (duration <= 0f)
                return;

            _temporaryInvincibleTimer = Mathf.Max(_temporaryInvincibleTimer, duration);
        }

        public void AddStateScopedInvincibility()
        {
            _stateScopedInvincibleCount++;
        }

        public void RemoveStateScopedInvincibility()
        {
            _stateScopedInvincibleCount = Mathf.Max(0, _stateScopedInvincibleCount - 1);
        }

        public void EnterIdle(float duration, bool lockDecision)
        {
            EnsureInitialized();
            if (_dead) return;

            float validDuration = Mathf.Max(0f, duration);
            if (validDuration <= 0f)
            {
                if (CurrentIntent.Type == EnemyIntentType.Idle)
                    CurrentIntent = EnemyIntent.None;
                return;
            }

            ClearPostCastRecoveryState();

            _idleUntilTime = Time.time + validDuration;
            if (lockDecision)
                _decisionLockUntilTime = Mathf.Max(_decisionLockUntilTime, _idleUntilTime);

            CurrentIntent = new EnemyIntent { Type = EnemyIntentType.Idle };
        }

        public bool TryGetPostCastRetreatDestination(out Vector3 destination)
        {
            if (IsInPostCastRecovery && _hasPostCastRetreatDestination)
            {
                destination = _postCastRetreatDestination;
                return true;
            }

            destination = Vector3.zero;
            return false;
        }

        public void OnDeathAnimationFinished()
        {
            if (!_dead || _deathFinalized)
                return;

            _deathCleanupTime = Mathf.Min(_deathCleanupTime, Time.time);
        }

        public void ApplyTimedStatModifier(StatModifier modifier, float duration)
        {
            EnsureInitialized();
            if (modifier == null) return;

            float validDuration = Mathf.Max(0.01f, duration);
            _runtimeModifiers.Add(new RuntimeStatModifier
            {
                modifier = modifier.Clone(),
                endTime = Time.time + validDuration
            });
            _modifierCacheDirty = true;
        }

        public void ApplyStatModifierUntilStateExit(StatModifier modifier, SkillCueRuntimeScope cueRuntime)
        {
            EnsureInitialized();
            if (modifier == null)
                return;

            StatModifier clonedModifier = modifier.Clone();
            _runtimeModifiers.Add(new RuntimeStatModifier
            {
                modifier = clonedModifier,
                endTime = float.PositiveInfinity
            });
            _modifierCacheDirty = true;
            cueRuntime?.RegisterStateExitCallback(() =>
            {
                if (this == null)
                    return;

                for (int i = _runtimeModifiers.Count - 1; i >= 0; i--)
                {
                    RuntimeStatModifier runtimeModifier = _runtimeModifiers[i];
                    if (runtimeModifier == null || runtimeModifier.modifier != clonedModifier)
                        continue;

                    _runtimeModifiers.RemoveAt(i);
                    _modifierCacheDirty = true;
                    break;
                }
            });
        }

        public void Heal(float amount)
        {
            EnsureInitialized();
            if (_dead || amount <= 0f) return;
            _stats.currentHp = Mathf.Min(_stats.maxHp, _stats.currentHp + amount);
        }

        public bool ApplyDamage(float amount)
        {
            EnsureInitialized();
            if (_dead || HasInvincibility) return false;

            _stats = _stats.WithDamageApplied(amount);
            if (_stats.IsDead)
            {
                RefreshHeadHealthBarOnDeath();
                Die();
                return true;
            }

            ProcessHitReaction(amount);
            return false;
        }

        public bool ApplyOnlineDamage(float amount)
        {
            return ApplyDamage(amount);
        }

        public void ApplyOnlineAuthorityState(float currentHp, bool isDead)
        {
            ApplyOnlineAuthorityState(currentHp, isDead, _onlineAuthorityCombatVisible);
        }

        public void ApplyOnlineAuthorityState(float currentHp, bool isDead, bool showCombatHealthBar)
        {
            EnsureInitialized();
            _stats.currentHp = Mathf.Clamp(currentHp, 0f, Mathf.Max(1f, _stats.maxHp));
            _onlineAuthorityCombatVisible = _onlineRemoteSimulationDisabled && !isDead && showCombatHealthBar;
            if (isDead || _stats.currentHp <= 0f)
            {
                _onlineAuthorityCombatVisible = false;
                if (!_dead)
                {
                    RefreshHeadHealthBarOnDeath();
                    Die();
                }

                return;
            }

            _dead = false;
            _deathFinalized = false;
            _deathCleanupTime = 0f;
            UpdateHeadHealthBar();
        }

        public void ApplyOnlineRemoteSkillPresentation(
            int activeSkillSequence,
            int activeSkillSlot,
            string activeSkillId,
            string activeSkillAnimationTrigger,
            bool hasActiveSkillTarget,
            Vector3 activeSkillTarget)
        {
            if (!_onlineRemoteSimulationDisabled || activeSkillSequence <= 0 || activeSkillSequence == _onlineRemoteVisualSkillSequence)
            {
                if (!_onlineRemoteSimulationDisabled && activeSkillSequence > 0 && activeSkillSequence != _lastLoggedMissingRemoteSkillSequence)
                {
                    _lastLoggedMissingRemoteSkillSequence = activeSkillSequence;
                    Debug.Log($"[OnlineAuthority] Remote skill ignored because simulation is enabled. runtime={RuntimeId} enemyId={EnemyId} sequence={activeSkillSequence} slot={activeSkillSlot} skill={activeSkillId} scene={gameObject.scene.name}");
                }

                return;
            }

            EnsureInitialized();
            EnemyResolvedSkill resolvedSkill = activeSkillSlot >= 0 ? ResolveSkillSlot(activeSkillSlot) : null;
            SharedSkillDefinition definition = resolvedSkill != null ? resolvedSkill.Definition : null;
            if (definition == null && !string.IsNullOrWhiteSpace(activeSkillId))
                definition = ConfigManager.GetInstance()?.GetSkillEffectDatabase()?.GetEntry(activeSkillId.Trim());
            if (definition == null)
            {
                if (activeSkillSequence != _lastLoggedMissingRemoteSkillSequence)
                {
                    _lastLoggedMissingRemoteSkillSequence = activeSkillSequence;
                    Debug.Log($"[OnlineAuthority] Remote skill missing definition. runtime={RuntimeId} enemyId={EnemyId} sequence={activeSkillSequence} slot={activeSkillSlot} skill={activeSkillId} trigger={activeSkillAnimationTrigger} scene={gameObject.scene.name}");
                }

                return;
            }

            _onlineRemoteVisualSkillSequence = activeSkillSequence;
            if (hasActiveSkillTarget)
                RotateOnlineRemoteVisualToward(activeSkillTarget);

            string triggerName = !string.IsNullOrWhiteSpace(activeSkillAnimationTrigger)
                ? activeSkillAnimationTrigger.Trim()
                : resolvedSkill != null ? resolvedSkill.AnimationTrigger : definition.GetAnimationTriggerOrEmpty();
            SetOnlineRemoteVisualTrigger(triggerName);
            _lastOnlineRemoteSkillApplyTime = Time.unscaledTime;
            Debug.Log($"[OnlineAuthority] Remote skill applied. runtime={RuntimeId} enemyId={EnemyId} sequence={activeSkillSequence} slot={activeSkillSlot} skill={activeSkillId} trigger={triggerName} hasTarget={hasActiveSkillTarget} scene={gameObject.scene.name}");

            _onlineRemoteVisualTimelineRunner?.Stop();
            _onlineRemoteVisualTimelineRunner = new SkillTimelineRunner();
            _onlineRemoteVisualTimelineRunner.Begin(
                definition,
                new EnemySkillExecutionContext(this, GetComponent<EnemyPerception>(), _onlineRemoteVisualOverlapBuffer),
                -1f,
                1f,
                null,
                true);
        }

        private void TickOnlineRemoteVisualTimeline(float deltaTime)
        {
            if (_onlineRemoteVisualTimelineRunner == null)
                return;

            _onlineRemoteVisualTimelineRunner.Tick(deltaTime);
            if (_onlineRemoteVisualTimelineRunner.IsComplete)
                _onlineRemoteVisualTimelineRunner = null;
        }

        private void LogOnlineRemoteHeartbeat()
        {
            if (Time.unscaledTime < _nextOnlineRemoteHeartbeatLogTime)
                return;

            _nextOnlineRemoteHeartbeatLogTime = Time.unscaledTime + OnlineAuthorityIntervalSeconds;
            Animator animator = GetComponent<Animator>();
            bool hasAnimator = animator != null;
            float animatorSpeed = hasAnimator ? animator.speed : 0f;
            int stateHash = 0;
            float normalizedTime = 0f;
            if (hasAnimator)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                stateHash = stateInfo.shortNameHash;
                normalizedTime = stateInfo.normalizedTime;
            }

            Debug.Log($"[OnlineAuthority] Remote enemy heartbeat. runtime={RuntimeId} enemyId={EnemyId} hp={CurrentHp:F1} dead={_dead} move=({_animatorMoveBlend:F3},{_animatorMoveSigned:F3},{_animatorMoveStrafe:F3}) hasAnimator={hasAnimator} animatorSpeed={animatorSpeed:F2} stateHash={stateHash} normalized={normalizedTime:F2} timelineActive={_onlineRemoteVisualTimelineRunner != null} visualSkillSeq={_onlineRemoteVisualSkillSequence} secondsSinceMove={(Time.unscaledTime - _lastOnlineRemoteMovementApplyTime):F2} secondsSinceSkill={(Time.unscaledTime - _lastOnlineRemoteSkillApplyTime):F2} scene={gameObject.scene.name}");
        }

        private void RotateOnlineRemoteVisualToward(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void SetOnlineRemoteVisualTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            Animator animator = GetComponent<Animator>();
            if (animator == null || !HasAnimatorParameter(animator, triggerName, AnimatorControllerParameterType.Trigger))
                return;

            animator.ResetTrigger(triggerName);
            animator.SetTrigger(triggerName);
        }

        private static bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == parameterType && string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void InitializeRuntimeState()
        {
            if (_runtimeStateInitialized)
                return;

            _runtimeStateInitialized = true;
            _dead = false;
            _hurtRemainingTime = 0f;
            _runtimeModifiers.Clear();
            _lastSkillCastTimeBySlot.Clear();
            _activeSkill = null;
            _activeSkillTargetPosition = null;
            _activeSkillSequence = 0;
            _onlineAuthorityCombatVisible = false;
            _idleUntilTime = 0f;
            _decisionLockUntilTime = 0f;
            _isInPostCastRecovery = false;
            _hasPostCastRetreatDestination = false;
            _postCastRetreatDestination = Vector3.zero;
            _deathCleanupTime = 0f;
            _deathFinalized = false;
            _animatorMoveBlend = 0f;
            _animatorMoveSigned = 0f;
            _animatorMoveStrafe = 0f;
            _currentPoise = 0f;
            _poiseInitialized = false;
            _temporarySuperArmorTimer = 0f;
            _temporaryInvincibleTimer = 0f;
            _stateScopedSuperArmorCount = 0;
            _stateScopedInvincibleCount = 0;
            _modifierCacheDirty = true;
            CurrentIntent = EnemyIntent.None;
        }

        private bool TryGetCastableSkill(int slot, out EnemyResolvedSkill skill)
        {
            EnsureInitialized();
            skill = null;
            if (_dead || Archetype == null || IsHurt || IsCastingSkill)
                return false;

            if (!Archetype.IsSkillSlotAvailableInCurrentPhase(slot, CurrentHpRatio))
                return false;

            skill = ResolveSkillSlot(slot);
            if (skill == null)
                return false;

            if (_lastSkillCastTimeBySlot.TryGetValue(slot, out float lastCastTime) && Time.time - lastCastTime < skill.Cooldown)
                return false;

            return true;
        }

        private void RecalculateModifierCacheIfNeeded()
        {
            if (!_modifierCacheDirty) return;
            _modifierCacheDirty = false;

            _bonusAttack = 0f;
            _bonusDefense = 0f;
            _bonusMoveSpeedRatio = 0f;
            _bonusHpRegen = 0f;
            _bonusLifeSteal = 0f;
            _bonusDamageBonus = 0f;
            _bonusDamageReduce = 0f;

            for (int i = 0; i < _runtimeModifiers.Count; i++)
            {
                var modifier = _runtimeModifiers[i].modifier;
                if (modifier == null) continue;
                _bonusAttack += modifier.attackAdd;
                _bonusDefense += modifier.defenseAdd;
                _bonusMoveSpeedRatio += modifier.moveSpeedAdd;
                _bonusHpRegen += modifier.hpRegenAdd;
                _bonusLifeSteal += modifier.lifeStealAdd;
                _bonusDamageBonus += modifier.damageBonusAdd;
                _bonusDamageReduce += modifier.damageReduceAdd;
            }
        }

        private void AppendSkillCooldowns(List<EnemySkillCooldownSave> cooldowns)
        {
            if (cooldowns == null || Archetype?.skillSlots == null)
                return;

            var recordedSlots = new HashSet<int>();
            for (int i = 0; i < Archetype.skillSlots.Count; i++)
            {
                EnemySkillSlotBinding binding = Archetype.skillSlots[i];
                if (binding == null || !recordedSlots.Add(binding.slotIndex))
                    continue;

                float remaining = GetSkillCooldownRemaining(binding.slotIndex);
                if (remaining <= 0f)
                    continue;

                cooldowns.Add(new EnemySkillCooldownSave
                {
                    slot = binding.slotIndex,
                    remainingTime = remaining,
                });
            }
        }

        private void RestoreSkillCooldowns(List<EnemySkillCooldownSave> cooldowns)
        {
            _lastSkillCastTimeBySlot.Clear();
            if (cooldowns == null)
                return;

            for (int i = 0; i < cooldowns.Count; i++)
            {
                EnemySkillCooldownSave cooldown = cooldowns[i];
                if (cooldown == null)
                    continue;

                EnemyResolvedSkill skill = ResolveSkillSlot(cooldown.slot);
                if (skill == null)
                    continue;

                float remaining = Mathf.Clamp(cooldown.remainingTime, 0f, skill.Cooldown);
                if (remaining <= 0f)
                    continue;

                _lastSkillCastTimeBySlot[cooldown.slot] = Time.time - Mathf.Max(0f, skill.Cooldown - remaining);
            }
        }

        private EnemyIntent BuildIntentFromSnapshot(EnemySnapshot snapshot)
        {
            EnemyIntentType type = Enum.IsDefined(typeof(EnemyIntentType), snapshot.intentType)
                ? (EnemyIntentType)snapshot.intentType
                : EnemyIntentType.None;

            if (type == EnemyIntentType.CastSkill || type == EnemyIntentType.Dead)
                type = EnemyIntentType.None;

            Vector3? targetPosition = snapshot.hasTarget
                ? new Vector3(snapshot.lastTargetX, transform.position.y, snapshot.lastTargetZ)
                : (Vector3?)null;

            return new EnemyIntent
            {
                Type = type,
                TargetPosition = targetPosition,
            };
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;
            UnregisterActiveEnemy();
            EnemySquadCoordinator.Release(this);
            CancelActiveSkill();
            ClearPostCastRecoveryState();
            _idleUntilTime = 0f;
            _decisionLockUntilTime = 0f;
            _deathCleanupTime = Time.time + Mathf.Max(0f, _deathDisableDelay);
            CurrentIntent = new EnemyIntent { Type = EnemyIntentType.Dead };

            Vector3 position = transform.position;
            EventCenter.GetInstance().EventTrigger(GameEvents.EnemyDied, new EnemyDiedArgs
            {
                EnemyId = _stats.enemyId,
                Position = position,
                Type = _stats.type,
            });

            if (_stats.type == EnemyType.Guardian)
                EventCenter.GetInstance().EventTrigger(GameEvents.GuardianDied);
            else if (_stats.type == EnemyType.Boss && GetComponent<LevelBossVisualMarker>() != null)
                EventCenter.GetInstance().EventTrigger(GameEvents.BossDefeated, EnemyId);

            // 使用奖励系统处理玩家奖励，解耦 EnemyController 和 PlayerModel
            var player = GameStateMachine.GetInstance()?.Player;
            if (player != null && !Game.Online.OnlineDungeonSessionCoordinator.GetInstance().TrySubmitEnemyKillReward(this, _stats, position, _rng))
                Game.GameFlow.EnemyDeathRewardSystem.GrantRewards(player, _stats, position, _rng);
        }

        private void RefreshHeadHealthBarOnDeath()
        {
            if (IsFinalBoss())
                return;

            if (_headHealthBar == null)
                _headHealthBar = GetComponent<ActorHeadHealthBar>();
            if (_headHealthBar == null)
                return;

            _headHealthBar.SetNormalized(0f);
        }

        private void FinalizeDeath()
        {
            if (_deathFinalized)
                return;

            _deathFinalized = true;
            if (_poolSourcePrefab != null)
            {
                PoolMgr.GetInstance().PushObj(_poolSourcePrefab, gameObject);
                return;
            }

            Destroy(gameObject);
        }

        private void OnEnable()
        {
            RegisterActiveEnemy();
        }

        private void OnDisable()
        {
            UnregisterActiveEnemy();
        }

        private void OnDestroy()
        {
            UnregisterActiveEnemy();
        }

        private string ResolveConfiguredEnemyId()
        {
            if (!string.IsNullOrWhiteSpace(_enemyId))
                return NormalizeEnemyId(_enemyId);

            if (_archetypeOverride != null && !string.IsNullOrWhiteSpace(_archetypeOverride.enemyId))
                return NormalizeEnemyId(_archetypeOverride.enemyId);

            return string.Empty;
        }

        private static string NormalizeEnemyId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private void EnsureRuntimeId()
        {
            if (string.IsNullOrWhiteSpace(_runtimeId))
                _runtimeId = Guid.NewGuid().ToString("N");
        }

        private bool CanFallbackStatsToType(ConfigManager cfg, string resolvedEnemyId)
        {
            if (!string.IsNullOrEmpty(resolvedEnemyId))
                return true;

            int statVariantCount = cfg?.GetEnemyStatsDatabase()?.CountByType(_enemyType) ?? 0;
            if (statVariantCount <= 1)
                return true;

            if (!_loggedAmbiguousVariantError)
            {
                Debug.LogError(
                    $"[Enemy] {name} 未设置 enemyId，且 EnemyType {_enemyType} 存在多个数值变体，无法按类型回退。请填写 enemyId。",
                    this);
                _loggedAmbiguousVariantError = true;
            }

            return false;
        }

        private bool CanFallbackArchetypeToType(ConfigManager cfg, string resolvedEnemyId)
        {
            if (!string.IsNullOrEmpty(resolvedEnemyId) || _archetypeOverride != null)
                return true;

            int archetypeVariantCount = cfg != null ? cfg.CountEnemyArchetypesByType(_enemyType) : 0;
            if (archetypeVariantCount <= 1)
                return true;

            if (!_loggedAmbiguousVariantError)
            {
                Debug.LogError(
                    $"[Enemy] {name} 未设置 enemyId，且 EnemyType {_enemyType} 存在多个行为变体，无法按类型回退。请填写 enemyId 或指定 ArchetypeOverride。",
                    this);
                _loggedAmbiguousVariantError = true;
            }

            return false;
        }

        private void EnterPostCastRecovery(float duration, Vector3? fallbackTargetPosition)
        {
            float validDuration = Mathf.Max(0f, duration);
            if (validDuration <= 0f)
            {
                ClearPostCastRecoveryState();
                CurrentIntent = EnemyIntent.None;
                return;
            }

            _idleUntilTime = Time.time + validDuration;
            _decisionLockUntilTime = Mathf.Max(_decisionLockUntilTime, _idleUntilTime);
            _isInPostCastRecovery = true;
            _hasPostCastRetreatDestination = TryResolvePostCastRetreatDestination(fallbackTargetPosition, out _postCastRetreatDestination);

            CurrentIntent = new EnemyIntent
            {
                Type = EnemyIntentType.Idle,
                TargetPosition = _hasPostCastRetreatDestination ? _postCastRetreatDestination : (Vector3?)null,
            };
        }

        private bool TryResolvePostCastRetreatDestination(Vector3? fallbackTargetPosition, out Vector3 destination)
        {
            destination = Vector3.zero;

            Vector3 selfPos = transform.position;
            Vector3 threatPos = selfPos;
            bool hasThreatPos = false;

            if (fallbackTargetPosition.HasValue)
            {
                threatPos = fallbackTargetPosition.Value;
                hasThreatPos = true;
            }
            else
            {
                Transform player = GameStateMachine.GetInstance()?.LevelPlayerTransform;
                if (player != null)
                {
                    threatPos = player.position;
                    hasThreatPos = true;
                }
            }

            if (!hasThreatPos)
                return false;

            float stepDistance = Archetype != null
                ? Mathf.Max(0.5f, Archetype.retreatStepDistance)
                : 2f;

            float currentDistance = UsesAerialMovement
                ? Vector3.Distance(selfPos, threatPos)
                : Vector3.Distance(
                    new Vector3(selfPos.x, 0f, selfPos.z),
                    new Vector3(threatPos.x, 0f, threatPos.z));

            float desiredDistance = currentDistance + stepDistance;
            return EnemyTacticalNavigation.TryFindRetreatPoint(
                this,
                Archetype,
                threatPos,
                desiredDistance,
                currentDistance,
                out destination);
        }

        private void ClearPostCastRecoveryState()
        {
            _isInPostCastRecovery = false;
            _hasPostCastRetreatDestination = false;
            _postCastRetreatDestination = Vector3.zero;
        }

        private void EnsurePoiseInitialized()
        {
            if (_poiseInitialized && (Archetype == null || _currentPoise > 0f || Archetype.poiseMax <= 0f))
                return;

            _poiseInitialized = true;
            _currentPoise = Archetype != null ? Mathf.Max(0f, Archetype.poiseMax) : 0f;
        }

        private void TickPoiseRecovery(float dt)
        {
            if (_dead || Archetype == null)
                return;

            EnsurePoiseInitialized();

            float maxPoise = Mathf.Max(0f, Archetype.poiseMax);
            if (maxPoise <= 0f || _currentPoise >= maxPoise)
                return;

            _currentPoise = Mathf.Min(maxPoise, _currentPoise + Mathf.Max(0f, Archetype.poiseRecoveryPerSecond) * dt);
        }

        private void ProcessHitReaction(float damage)
        {
            if (Archetype == null)
            {
                ApplyHardControl(0.3f);
                return;
            }

            EnsurePoiseInitialized();

            float maxPoise = Mathf.Max(0f, Archetype.poiseMax);
            float breakStunDuration = Mathf.Max(0.05f, Archetype.poiseBreakStunDuration);
            if (maxPoise <= 0f)
            {
                if (!HasSuperArmor)
                    ApplyHardControl(breakStunDuration);
                return;
            }

            _currentPoise = Mathf.Max(0f, _currentPoise - Mathf.Max(0.5f, damage));
            if (_currentPoise <= 0.01f)
            {
                _currentPoise = maxPoise;
                if (!HasSuperArmor)
                    ApplyHardControl(breakStunDuration);
                return;
            }

            if (!Archetype.allowLightHitFlinch || HasSuperArmor)
                return;

            float heavyHitThreshold = Mathf.Max(4f, maxPoise * 0.45f);
            if (damage >= heavyHitThreshold)
                ApplyHardControl(Mathf.Max(0.05f, breakStunDuration * 0.55f));
        }

        private void EnsureHeadHealthBar()
        {
            if (_headHealthBar == null)
                _headHealthBar = GetComponent<ActorHeadHealthBar>();
            if (_headHealthBar == null)
                _headHealthBar = gameObject.AddComponent<ActorHeadHealthBar>();

            _headHealthBar.Configure();
        }

        private void UpdateHeadHealthBar()
        {
            EnsureHeadHealthBar();
            if (_headHealthBar == null)
                return;

            if (IsFinalBoss())
            {
                _headHealthBar.SetVisible(false);
                return;
            }

            _headHealthBar.SetNormalized(CurrentHpRatio);
            _headHealthBar.SetVisible(ShouldShowCombatHealthBar);
        }

        private bool IsFinalBoss()
        {
            return EnemyCategory == EnemyType.Boss && GetComponent<LevelBossVisualMarker>() != null;
        }

        private void RegisterActiveEnemy()
        {
            if (!ActiveEnemyControllers.Contains(this))
                ActiveEnemyControllers.Add(this);
        }

        private void UnregisterActiveEnemy()
        {
            ActiveEnemyControllers.Remove(this);
        }

        public static EnemyController FindByRuntimeId(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
                return null;

            string normalizedRuntimeId = runtimeId.Trim();
            for (int i = 0; i < ActiveEnemyControllers.Count; i++)
            {
                EnemyController enemy = ActiveEnemyControllers[i];
                if (enemy != null && string.Equals(enemy.RuntimeId, normalizedRuntimeId, StringComparison.Ordinal))
                    return enemy;
            }

            return null;
        }

    }
}
