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
        private const string TriggerLocomotion = "Locomotion";
        private const string TriggerHurt     = "Hurt";
        private const string TriggerDead     = "Dead";

        private Animator _anim;
        private EnemyController _controller;
        private EnemyPerception _perception;
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private readonly HashSet<string> _animParameterNames = new HashSet<string>();
        private readonly List<SkillTimelineRunner> _detachedTimelineRunners = new List<SkillTimelineRunner>();

        private EnemyResolvedSkill _runningSkill;
        private SkillTimelineRunner _timelineRunner = new SkillTimelineRunner();
        private readonly SkillTimelineRunner _loopStateTimelineRunner = new SkillTimelineRunner();
        private int _handledSkillSequence;
        private EnemyIntentType _lastIntentType = EnemyIntentType.None;
        private bool _wasHurtLastFrame;
        private bool _wasInLocomotionLoopState;
        private string _trackedNaturalExitAction;
        private float _trackedNaturalExitNormalizedTime = 0.9f;
        private EnemyAnimationNaturalExitTarget _trackedNaturalExitTarget = EnemyAnimationNaturalExitTarget.Locomotion;
        private bool _trackedNaturalExitTriggered;

        private void Awake()
        {
            _anim       = GetComponent<Animator>();
            _controller = GetComponent<EnemyController>();
            _perception = GetComponent<EnemyPerception>();
            CacheAnimatorParameters();
        }

        private void Update()
        {
            if (_controller == null) return;
            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true) return;

            UpdateAnimatorSpeed();
            TickDetachedTimelineRunners(Time.deltaTime);

            if (!_controller.IsAlive)
            {
                ClearRunningSkill();
                ClearTrackedNaturalExit();
                StopLoopStateTimeline();
                if (_controller.CurrentIntent.Type == EnemyIntentType.Dead && _lastIntentType != EnemyIntentType.Dead)
                    PlayAnimatorAction(TriggerDead);
                _lastIntentType   = _controller.CurrentIntent.Type;
                _wasHurtLastFrame = false;
                return;
            }

            TickLoopStateTimeline();

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
                {
                    PlayAnimatorAction(TriggerHurt);
                    TrackNaturalExit(
                        TriggerHurt,
                        ResolveHurtNaturalExitNormalizedTime(),
                        ResolveHurtNaturalExitTarget());
                }
            }

            TickNaturalExit();

            _lastIntentType   = _controller.CurrentIntent.Type;
            _wasHurtLastFrame = _controller.IsHurt;
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
            TrackNaturalExit(
                _runningSkill.AnimationTrigger,
                _runningSkill.NaturalExitNormalizedTime,
                _runningSkill.NaturalExitTarget);

            var ctx = new EnemySkillExecutionContext(
                _controller,
                _perception,
                _overlapBuffer);

            if (_timelineRunner == null)
                _timelineRunner = new SkillTimelineRunner();
            _timelineRunner.Begin(_runningSkill.Definition, ctx, _runningSkill.CastDuration);
        }

        private void TickRunningSkill()
        {
            if (_runningSkill == null) return;

            _timelineRunner?.Tick(Time.deltaTime);

            if (ShouldReleaseRunningSkill())
            {
                _controller.CompleteActiveSkill();
                DetachRunningSkillTimeline();
                _runningSkill = null;
            }
        }

        private void ClearRunningSkill()
        {
            _timelineRunner?.Stop();
            _runningSkill = null;
        }

        private bool ShouldReleaseRunningSkill()
        {
            if (_runningSkill == null)
                return false;

            if (_trackedNaturalExitTriggered)
                return true;

            if (IsCurrentStateNearEnd(_runningSkill.NaturalExitNormalizedTime))
                return true;

            if (_timelineRunner != null
                && _runningSkill.CastDuration > 0f
                && _timelineRunner.Elapsed >= _runningSkill.CastDuration)
            {
                return true;
            }

            return _timelineRunner != null && _timelineRunner.IsComplete;
        }

        private void DetachRunningSkillTimeline()
        {
            if (_timelineRunner == null)
                return;

            _timelineRunner.StopStateScopedCues();
            if (_timelineRunner.HasPendingWork)
                _detachedTimelineRunners.Add(_timelineRunner);
            else
                _timelineRunner.Stop();

            _timelineRunner = new SkillTimelineRunner();
        }

        private void TickDetachedTimelineRunners(float deltaTime)
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

                runner.Tick(deltaTime);
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

        private void TickLoopStateTimeline()
        {
            if (!ShouldRunLocomotionLoopState())
            {
                StopLoopStateTimeline();
                return;
            }

            string skillId = ResolveLocomotionLoopSkillId();
            if (!_wasInLocomotionLoopState)
            {
                _wasInLocomotionLoopState = true;
                BeginLoopStateTimeline(skillId);
            }

            _loopStateTimelineRunner.Tick(Time.deltaTime);
        }

        private void BeginLoopStateTimeline(string skillId)
        {
            _loopStateTimelineRunner.Stop();

            if (string.IsNullOrWhiteSpace(skillId))
                return;

            SharedSkillDefinition definition = ConfigManager.GetInstance()?.GetSkillDatabase()?.GetEntry(skillId);
            if (definition == null)
                return;

            var ctx = new EnemySkillExecutionContext(
                _controller,
                _perception,
                _overlapBuffer);

            _loopStateTimelineRunner.Begin(definition, ctx);
        }

        private void StopLoopStateTimeline()
        {
            _loopStateTimelineRunner.Stop();
            _wasInLocomotionLoopState = false;
        }

        private void ClearTrackedNaturalExit()
        {
            _trackedNaturalExitAction = null;
            _trackedNaturalExitNormalizedTime = 0.9f;
            _trackedNaturalExitTarget = EnemyAnimationNaturalExitTarget.Locomotion;
            _trackedNaturalExitTriggered = false;
        }

        private void TrackNaturalExit(string action, float normalizedTime, EnemyAnimationNaturalExitTarget target)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                ClearTrackedNaturalExit();
                return;
            }

            _trackedNaturalExitAction = action;
            _trackedNaturalExitNormalizedTime = Mathf.Clamp01(normalizedTime);
            _trackedNaturalExitTarget = target;
            _trackedNaturalExitTriggered = false;
        }

        private void TickNaturalExit()
        {
            if (_anim == null || _trackedNaturalExitTriggered || string.IsNullOrWhiteSpace(_trackedNaturalExitAction))
                return;

            string trigger = ResolveNaturalExitTrigger(_trackedNaturalExitTarget);
            if (string.IsNullOrEmpty(trigger))
            {
                _trackedNaturalExitTriggered = true;
                return;
            }

            if (!IsCurrentStateNearEnd(_trackedNaturalExitNormalizedTime))
                return;

            PlayAnimatorAction(trigger);
            _trackedNaturalExitTriggered = true;
        }

        private bool IsCurrentStateNearEnd(float threshold)
        {
            if (_anim == null || _anim.IsInTransition(0))
                return false;

            AnimatorStateInfo info = _anim.GetCurrentAnimatorStateInfo(0);
            if (info.loop)
                return false;

            return info.normalizedTime >= Mathf.Clamp01(threshold);
        }

        private float ResolveHurtNaturalExitNormalizedTime()
        {
            if (_controller != null && _controller.Archetype != null)
                return _controller.Archetype.hurtNaturalExitNormalizedTime;
            return 0.9f;
        }

        private EnemyAnimationNaturalExitTarget ResolveHurtNaturalExitTarget()
        {
            if (_controller != null && _controller.Archetype != null)
                return _controller.Archetype.hurtNaturalExitTarget;
            return EnemyAnimationNaturalExitTarget.Locomotion;
        }

        private bool ShouldRunLocomotionLoopState()
        {
            if (_controller == null || !_controller.IsAlive)
                return false;

            if (_controller.IsHurt || _controller.ActiveSkill != null)
                return false;

            if (_controller.CurrentIntent.Type == EnemyIntentType.Dead)
                return false;

            return Mathf.Abs(_controller.AnimatorMoveForward) > 0.01f
                || Mathf.Abs(_controller.AnimatorMoveStrafe) > 0.01f;
        }

        private string ResolveLocomotionLoopSkillId()
        {
            if (_controller != null && _controller.Archetype != null)
                return _controller.Archetype.locomotionSkillId;
            return "EnemyLocomotion";
        }

        private static string ResolveNaturalExitTrigger(EnemyAnimationNaturalExitTarget target) => target switch
        {
            EnemyAnimationNaturalExitTarget.Locomotion => TriggerLocomotion,
            _ => null,
        };

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

        private void OnDisable()
        {
            StopDetachedTimelineRunners();
        }

        private void OnDestroy()
        {
            StopDetachedTimelineRunners();
        }

        private void WarnMissingAnimatorParameter(string param)
        {
            string key = $"{name}:{param}";
            if (MissingAnimatorParameterWarnings.Add(key))
                Debug.LogWarning($"[EnemyCombat] {name}: Animator 缺少参数 '{param}'。请检查控制器参数表。", this);
        }
    }
}
