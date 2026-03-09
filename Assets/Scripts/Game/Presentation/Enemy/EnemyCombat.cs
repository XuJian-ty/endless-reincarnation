using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Domain;
using Game.GameFlow;
using Game;

namespace Game.Presentation
{
    /// <summary>
    /// Enemy combat execution: triggers skill animations, runs the shared skill timeline, and optionally processes animation damage events.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    [RequireComponent(typeof(Animator))]
    public class EnemyCombat : MonoBehaviour
    {
        private static readonly HashSet<string> MissingAnimationDamageWarnings  = new HashSet<string>();
        private static readonly HashSet<string> MissingAnimatorParameterWarnings = new HashSet<string>();
        private static readonly HashSet<string> MissingAnimatorActionWarnings    = new HashSet<string>();
        private const string ParamMoveX      = "MoveX";
        private const string ParamMoveY      = "MoveY";
        private const string TriggerHurt     = "Hurt";
        private const string TriggerDead     = "Dead";

        [Header("Detection Config")]
        [Tooltip("If empty, loads from ConfigManager at Start")]
        [SerializeField] private AnimationFrameDamageDatabaseSO _damageDatabase;

        private Animator _anim;
        private EnemyController _controller;
        private EnemyPerception _perception;
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private readonly HashSet<string> _animParameterNames = new HashSet<string>();

        private EnemyResolvedSkill _runningSkill;
        private readonly SkillTimelineRunner _timelineRunner = new SkillTimelineRunner();
        private int _handledSkillSequence;
        private EnemyIntentType _lastIntentType = EnemyIntentType.None;
        private bool _wasHurtLastFrame;

        private void Awake()
        {
            _anim       = GetComponent<Animator>();
            _controller = GetComponent<EnemyController>();
            _perception = GetComponent<EnemyPerception>();
            CacheAnimatorParameters();
        }

        private void Start()
        {
            if (_damageDatabase == null)
                _damageDatabase = ConfigManager.GetInstance()?.GetAnimationFrameDamageDatabase();
        }

        private void Update()
        {
            if (_controller == null) return;
            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true) return;

            UpdateAnimatorSpeed();

            if (!_controller.IsAlive)
            {
                ClearRunningSkill();
                if (_controller.CurrentIntent.Type == EnemyIntentType.Dead && _lastIntentType != EnemyIntentType.Dead)
                    PlayAnimatorAction(TriggerDead);
                _lastIntentType   = _controller.CurrentIntent.Type;
                _wasHurtLastFrame = false;
                return;
            }

            if (_controller.ActiveSkill != null)
            {
                if (_controller.ActiveSkillSequence != _handledSkillSequence)
                    BeginSkillCast(_controller.ActiveSkill, _controller.ActiveSkillTargetPosition);

                TickRunningSkill();
            }
            else
            {
                ClearRunningSkill();
                if (_controller.IsHurt && !_wasHurtLastFrame)
                    PlayAnimatorAction(TriggerHurt);
            }

            _lastIntentType   = _controller.CurrentIntent.Type;
            _wasHurtLastFrame = _controller.IsHurt;
        }

        /// <summary>动画事件伤害：仅当当前技能未声明忽略动画事件时生效。</summary>
        public void OnDealDamage(string damageName)
        {
            if (_controller == null || !_controller.IsAlive) return;
            if (_runningSkill != null && _runningSkill.IgnoreAnimationDamageEvents) return;

            var db = _damageDatabase ?? ConfigManager.GetInstance()?.GetAnimationFrameDamageDatabase();
            if (db == null) return;

            string resolvedName = !string.IsNullOrEmpty(damageName) ? damageName
                : _runningSkill?.Binding?.skillId;
            if (string.IsNullOrEmpty(resolvedName)) return;

            if (db.GetEntry(resolvedName) == null)
            {
                string warnKey = $"{name}:{resolvedName}";
                if (MissingAnimationDamageWarnings.Add(warnKey))
                {
                    Debug.LogWarning(
                        $"[EnemyCombat] {name}: 动画伤害事件未找到检测配置 '{resolvedName}'。" +
                        "若技能完全由时间轴驱动，请设 ignoreAnimationDamageEvents = true。", this);
                }
                return;
            }

            if (!DamageDetectionRunner.TryRunDetection(resolvedName, transform, db, _overlapBuffer, out var entry, out int hitCount))
                return;
            if (entry == null || hitCount <= 0) return;

            ApplyDamageToPlayerTargets(hitCount, entry.damageMultiplier, entry.pushDuration);
        }

        // ── 内部 ─────────────────────────────────────────────────────────────

        private void BeginSkillCast(EnemyResolvedSkill skill, Vector3? targetPosition)
        {
            _runningSkill         = skill;
            _handledSkillSequence = _controller.ActiveSkillSequence;

            if (_runningSkill == null) return;

            if (_runningSkill.RotateToTargetOnCast && targetPosition.HasValue)
                RotateToward(targetPosition.Value);

            PlayAnimatorAction(_runningSkill.AnimationTrigger);

            var ctx = new EnemySkillExecutionContext(
                _controller,
                _perception,
                _damageDatabase ?? ConfigManager.GetInstance()?.GetAnimationFrameDamageDatabase(),
                _overlapBuffer);

            _timelineRunner.Begin(_runningSkill.Definition, ctx, _runningSkill.CastDuration);
        }

        private void TickRunningSkill()
        {
            if (_runningSkill == null) return;

            _timelineRunner.Tick(Time.deltaTime);

            if (_timelineRunner.IsComplete)
            {
                _controller.CompleteActiveSkill();
                ClearRunningSkill();
            }
        }

        private void ClearRunningSkill() => _runningSkill = null;

        private void ApplyDamageToPlayerTargets(int hitCount, float damageMultiplier, float stunDuration)
        {
            for (int i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;
                var player = col.GetComponentInParent<PlayerController>();
                if (player == null) continue;
                float dmg = CombatCalculator.CalculateDamageFromEnemy(
                    _controller.Attack,
                    player.PlayerModel.Stats.Defense,
                    _controller.DamageBonus,
                    player.PlayerModel.Stats.DamageReduce) * damageMultiplier;
                player.OnHit(dmg, stunDuration > 0.01f ? stunDuration : 0.2f);
                if (dmg > 0f && _controller.LifeSteal > 0f)
                    _controller.Heal(dmg * _controller.LifeSteal);
                break;
            }
        }

        private void UpdateAnimatorSpeed()
        {
            if (_anim == null) return;

            if (HasAnimatorParameter(ParamMoveX))      _anim.SetFloat(ParamMoveX,      _controller.AnimatorMoveStrafe);
            if (HasAnimatorParameter(ParamMoveY))      _anim.SetFloat(ParamMoveY,      _controller.AnimatorMoveForward);
        }

        private void RotateToward(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        private void PlayAnimatorAction(string action)
        {
            if (_anim == null || string.IsNullOrEmpty(action)) return;

            if (HasAnimatorParameter(action))
            {
                _anim.ResetTrigger(action);
                _anim.SetTrigger(action);
                return;
            }

            string key = $"{name}:{action}";
            if (MissingAnimatorActionWarnings.Add(key))
                Debug.LogWarning($"[EnemyCombat] {name}: Animator 缺少 Trigger 参数 '{action}'。请在控制器参数表中补齐。", this);
        }

        private void CacheAnimatorParameters()
        {
            _animParameterNames.Clear();
            if (_anim == null) return;
            foreach (var p in _anim.parameters)
                _animParameterNames.Add(p.name);
        }

        private bool HasAnimatorParameter(string paramName)
        {
            if (string.IsNullOrEmpty(paramName)) return false;
            if (_animParameterNames.Count == 0) CacheAnimatorParameters();
            return _animParameterNames.Contains(paramName);
        }

        private void WarnMissingAnimatorParameter(string param)
        {
            string key = $"{name}:{param}";
            if (MissingAnimatorParameterWarnings.Add(key))
                Debug.LogWarning($"[EnemyCombat] {name}: Animator 缺少参数 '{param}'。请检查控制器参数表。", this);
        }
    }
}
