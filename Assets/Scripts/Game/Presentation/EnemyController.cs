using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Domain;
using Game.Data;
using Game.AI;
using Game.GameFlow;
using Game;
using ProjectBase;

namespace Game.Presentation
{
    /// <summary>
    /// Enemy presentation facade: stats, damage/death, runtime modifiers, and skill-slot casting state.
    /// </summary>
    [RequireComponent(typeof(Collider))]
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
        [SerializeField] private bool _countsAsLevelBoss = true;

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
        private float _currentPoise;
        private bool _poiseInitialized;
        private static readonly Color HeadHealthBarColor = new Color(0.86f, 0.22f, 0.22f, 0.95f);

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
        public bool CountsAsLevelBoss => _countsAsLevelBoss;
        public EnemyIntent CurrentIntent { get; set; }
        public float HurtRemainingTime => _hurtRemainingTime;
        public bool IsHurt => _hurtRemainingTime > 0f;
        public EnemyArchetypeSO Archetype { get; private set; }
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
        public bool IsInPatrolState => !_dead && !IsInCombatState;
        public bool HasCastingSuperArmor => IsCastingSkill && Archetype != null && Archetype.superArmorWhileCasting;
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

        private void Awake()
        {
            RegisterActiveEnemy();
            _rng = new System.Random();
            InitializeRuntimeState();
            EnsureInitialized();
            EnsureHeadHealthBar();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            EnsureInitialized();
            if (!_dead && GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            if (_dead)
            {
                if (!_deathFinalized && Time.time >= _deathCleanupTime)
                    FinalizeDeath();
                return;
            }

            if (_hurtRemainingTime > 0f)
                _hurtRemainingTime = Mathf.Max(0f, _hurtRemainingTime - Time.deltaTime);

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
            _countsAsLevelBoss = value;
        }

        public void CompleteActiveSkill()
        {
            bool enterIdleAfterCast = _activeSkill != null && _activeSkill.EnterIdleAfterCast;
            float postCastIdleDuration = (_activeSkill != null && enterIdleAfterCast)
                ? Mathf.Max(0f, _activeSkill.PostCastIdleDuration)
                : 0f;
            Vector3? targetPosition = _activeSkillTargetPosition;

            _activeSkill = null;
            _activeSkillTargetPosition = null;
            if (CurrentIntent.Type == EnemyIntentType.CastSkill)
            {
                if (enterIdleAfterCast && postCastIdleDuration > 0f)
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

        public void Heal(float amount)
        {
            EnsureInitialized();
            if (_dead || amount <= 0f) return;
            _stats.currentHp = Mathf.Min(_stats.maxHp, _stats.currentHp + amount);
        }

        public bool ApplyDamage(float amount)
        {
            EnsureInitialized();
            if (_dead) return false;

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
            if (player != null)
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

            Vector3 away = selfPos - threatPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = -transform.forward;
            away.Normalize();

            float stepDistance = Archetype != null
                ? Mathf.Max(0.5f, Archetype.retreatStepDistance)
                : 2f;

            Vector3 rawDestination = selfPos + away * stepDistance;
            if (!NavMesh.SamplePosition(rawDestination, out NavMeshHit sampled, 2f, NavMesh.AllAreas))
                return false;

            var path = new NavMeshPath();
            bool hasPath = NavMesh.CalculatePath(selfPos, sampled.position, NavMesh.AllAreas, path);
            if (!hasPath || path.status != NavMeshPathStatus.PathComplete)
                return false;

            destination = sampled.position;
            return true;
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
                if (!HasCastingSuperArmor)
                    ApplyHardControl(breakStunDuration);
                return;
            }

            _currentPoise = Mathf.Max(0f, _currentPoise - Mathf.Max(0.5f, damage));
            if (_currentPoise <= 0.01f)
            {
                _currentPoise = maxPoise;
                if (!HasCastingSuperArmor)
                    ApplyHardControl(breakStunDuration);
                return;
            }

            if (!Archetype.allowLightHitFlinch || HasCastingSuperArmor)
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

            _headHealthBar.Configure(HeadHealthBarColor);
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
            _headHealthBar.SetVisible(IsAlive && IsInCombatState);
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

    }
}
