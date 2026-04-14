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
        private const float HurtNaturalExitNormalizedTime = 0.9f;
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
        private readonly TimelineRunnerCollection _detachedTimelines = new TimelineRunnerCollection();

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
        private float _localCastSpeedOverrideTimer;
        private float _localCastSpeedOverrideMultiplier = 1f;

        public float CurrentMovementSpeedMultiplier
        {
            get
            {
                float multiplier = 1f;
                if (_timelineRunner != null && _timelineRunner.IsRunning)
                    multiplier *= Mathf.Max(0f, _timelineRunner.CurrentMovementSpeedMultiplier);

                if (_loopStateTimelineRunner != null && _loopStateTimelineRunner.IsRunning)
                    multiplier *= Mathf.Max(0f, _loopStateTimelineRunner.CurrentMovementSpeedMultiplier);

                return multiplier;
            }
        }

        public float LocalCastSpeedMultiplier => _localCastSpeedOverrideTimer > 0f
            ? Mathf.Clamp(_localCastSpeedOverrideMultiplier, 0f, 1f)
            : 1f;

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

            TickLocalHitStop(Time.deltaTime);
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
                UpdateAnimatorPlaybackSpeed();
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
            UpdateAnimatorPlaybackSpeed();

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
                EnemyAnimationNaturalExitTarget.Locomotion);

            var ctx = new EnemySkillExecutionContext(
                _controller,
                _perception,
                _overlapBuffer);

            if (_timelineRunner == null)
                _timelineRunner = new SkillTimelineRunner();
            _timelineRunner.Begin(
                _runningSkill.Definition,
                ctx,
                -1f,
                1f,
                () => LocalCastSpeedMultiplier);
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

            return _timelineRunner != null && _timelineRunner.IsComplete;
        }

        private void DetachRunningSkillTimeline()
        {
            if (_timelineRunner == null)
                return;

            _detachedTimelines.DetachOrStop(_timelineRunner);

            _timelineRunner = new SkillTimelineRunner();
        }

        private void TickDetachedTimelineRunners(float deltaTime)
        {
            _detachedTimelines.Tick(deltaTime);
        }

        private void StopDetachedTimelineRunners()
        {
            _detachedTimelines.StopAll();
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

            SharedSkillDefinition definition = ConfigManager.GetInstance()?.GetSkillEffectDatabase()?.GetEntry(skillId);
            if (definition == null)
                return;

            var ctx = new EnemySkillExecutionContext(
                _controller,
                _perception,
                _overlapBuffer);

            _loopStateTimelineRunner.Begin(
                definition,
                ctx,
                -1f,
                1f,
                () => LocalCastSpeedMultiplier);
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
            return AnimatorStateTimingUtility.IsCurrentStatePastNormalizedTime(_anim, 0, threshold);
        }

        private float ResolveHurtNaturalExitNormalizedTime()
        {
            return HurtNaturalExitNormalizedTime;
        }

        private EnemyAnimationNaturalExitTarget ResolveHurtNaturalExitTarget()
        {
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
            return string.Empty;
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

        private void UpdateAnimatorPlaybackSpeed()
        {
            if (_anim == null)
                return;

            float playbackSpeed = 1f;
            if (_runningSkill != null && _timelineRunner != null)
                playbackSpeed = Mathf.Max(0.01f, _timelineRunner.CurrentCastSpeedMultiplier);

            _anim.speed = playbackSpeed;
        }

        public void ApplyLocalHitStop(float duration, float castSpeedMultiplier)
        {
            if (duration <= 0f)
                return;

            _localCastSpeedOverrideTimer = Mathf.Max(_localCastSpeedOverrideTimer, duration);
            _localCastSpeedOverrideMultiplier = Mathf.Clamp01(
                Mathf.Min(_localCastSpeedOverrideMultiplier, castSpeedMultiplier));
        }

        private void TickLocalHitStop(float deltaTime)
        {
            if (_localCastSpeedOverrideTimer <= 0f)
                return;

            _localCastSpeedOverrideTimer = Mathf.Max(0f, _localCastSpeedOverrideTimer - deltaTime);
            if (_localCastSpeedOverrideTimer <= 0f)
                _localCastSpeedOverrideMultiplier = 1f;
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
