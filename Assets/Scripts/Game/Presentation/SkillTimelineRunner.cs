using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 技能时间轴运行器：接受 <see cref="SharedSkillDefinition"/> 并逐帧推进，
    /// 在合适的时间点调用 <see cref="SkillEffectExecutor"/> 中对应的五类事件执行入口。
    /// 玩家状态机和敌人战斗组件均可持有此实例。
    /// </summary>
    public sealed class SkillTimelineRunner
    {
        // ── 运行时状态 ────────────────────────────────────────────────────────

        [Serializable]
        private sealed class EventState
        {
            public bool hasTriggered;
            public float nextTriggerTime;
        }

        [Serializable]
        private sealed class ActiveDamageWindow
        {
            public SkillDamageEffect effect;
            public float endTime;
            public SkillCollisionHitbox collisionHitbox;
            public readonly HashSet<UnityEngine.Transform> hitTargets = new HashSet<UnityEngine.Transform>();
        }

        private SharedSkillDefinition _definition;
        private ISkillExecutionContext _context;
        private float _elapsed;
        private float _castDuration;
        private EventState[] _damageStates;
        private EventState[] _physicsStates;
        private EventState[] _attributeStates;
        private EventState[] _vfxStates;
        private EventState[] _sfxStates;
        private readonly List<ActiveDamageWindow> _activeDamageWindows = new List<ActiveDamageWindow>();
        private bool _started;

        // ── 公开属性 ──────────────────────────────────────────────────────────

        public bool IsRunning => _started && !IsComplete;
        public bool IsComplete { get; private set; }

        /// <summary>当前已播放时长（秒）。</summary>
        public float Elapsed => _elapsed;

        /// <summary>
        /// 技能总时长（秒）。
        /// 若传入了 castDuration，取 castDuration 与时间轴事件结束时间的较大值；
        /// 否则仅返回时间轴事件的最大结束时间。
        /// </summary>
        public float Duration
        {
            get
            {
                float eventsDuration = _definition != null ? _definition.GetTimelineDuration() : 0f;
                if (_castDuration > 0f)
                    return UnityEngine.Mathf.Max(_castDuration, eventsDuration);
                return eventsDuration;
            }
        }

        // ── 公开方法 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 开始播放技能时间轴。可反复调用以重置并复用同一实例。
        /// </summary>
        /// <param name="castDuration">
        /// AI 总锁定时长（秒）。> 0 时，Duration = max(castDuration, 时间轴事件总时长)，
        /// 保证 AI 在动画未结束前不会过早切换状态。玩家状态机可传入 -1（忽略此参数）。
        /// </param>
        public void Begin(SharedSkillDefinition definition, ISkillExecutionContext context, float castDuration = -1f)
        {
            _definition   = definition;
            _context      = context;
            _castDuration = castDuration;
            _elapsed      = 0f;
            _started      = true;
            IsComplete    = false;

            _damageStates = BuildStates(_definition?.damageEvents);
            _physicsStates = BuildStates(_definition?.physicsEvents);
            _attributeStates = BuildStates(_definition?.attributeEvents);
            _vfxStates = BuildStates(_definition?.vfxEvents);
            _sfxStates = BuildStates(_definition?.sfxEvents);
            ClearActiveDamageWindows();

            // 立即评估 t=0 的事件
            EvaluateEvents();
        }

        /// <summary>
        /// 每帧推进时间轴并触发到期事件。
        /// 播放完毕后 <see cref="IsComplete"/> 变为 true，调用方可据此结束技能。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_started || IsComplete || _definition == null) return;

            _elapsed += deltaTime;
            EvaluateEvents();

            if (_elapsed >= Duration)
                IsComplete = true;
        }

        /// <summary>提前终止时间轴（不触发剩余事件）。</summary>
        public void Stop()
        {
            _started = false;
            IsComplete = true;
            ClearActiveDamageWindows();
        }

        // ── 内部评估 ──────────────────────────────────────────────────────────

        private void EvaluateEvents()
        {
            EvaluateDamageTrack();
            UpdateActiveDamageWindows();
            EvaluateTrack(_definition?.physicsEvents, _physicsStates, SkillEffectExecutor.ExecutePhysicsEvent);
            EvaluateTrack(_definition?.attributeEvents, _attributeStates, SkillEffectExecutor.ExecuteAttributeEvent);
            EvaluateTrack(_definition?.vfxEvents, _vfxStates, SkillEffectExecutor.ExecuteVfxEvent);
            EvaluateTrack(_definition?.sfxEvents, _sfxStates, SkillEffectExecutor.ExecuteSfxEvent);
        }

        private void EvaluateDamageTrack()
        {
            var events = _definition?.damageEvents;
            var states = _damageStates;
            if (events == null || states == null)
                return;

            int count = UnityEngine.Mathf.Min(events.Count, states.Length);
            for (int i = 0; i < count; i++)
            {
                SkillDamageEvent evt = events[i];
                EventState state = states[i];
                if (evt == null || state == null)
                    continue;

                if (evt.triggerMode == SkillEventTriggerMode.Once)
                {
                    if (!state.hasTriggered && _elapsed >= evt.startTime)
                    {
                        TriggerDamageEvent(evt);
                        state.hasTriggered = true;
                    }
                    continue;
                }

                float endTime = evt.GetEndTime();
                float interval = UnityEngine.Mathf.Max(0.01f, evt.repeatInterval);
                while (_elapsed >= state.nextTriggerTime
                       && state.nextTriggerTime <= endTime + 0.0001f)
                {
                    TriggerDamageEvent(evt);
                    state.hasTriggered = true;
                    state.nextTriggerTime += interval;
                }
            }
        }

        private void TriggerDamageEvent(SkillDamageEvent evt)
        {
            if (evt?.damageEffects == null || _context == null)
                return;

            for (int i = 0; i < evt.damageEffects.Count; i++)
            {
                SkillDamageEffect effect = evt.damageEffects[i];
                if (effect == null)
                    continue;

                if (effect.detectionType == DamageDetectionType.Collision)
                {
                    float detectionDuration = effect.detectionDuration > 0f
                        ? effect.detectionDuration
                        : UnityEngine.Mathf.Max(0.0001f, UnityEngine.Time.fixedDeltaTime);
                    var collisionWindow = new ActiveDamageWindow
                    {
                        effect = effect,
                        endTime = _elapsed + detectionDuration,
                    };
                    collisionWindow.collisionHitbox = DamageDetectionRunner.BeginCollisionWindow(_context.CasterTransform, effect, collisionWindow);
                    if (collisionWindow.collisionHitbox != null)
                        _activeDamageWindows.Add(collisionWindow);
                    continue;
                }

                if (effect.detectionDuration <= 0f)
                {
                    SkillEffectExecutor.ExecuteDamageEffect(effect, _context, null);
                    continue;
                }

                var detectionWindow = new ActiveDamageWindow
                {
                    effect = effect,
                    endTime = _elapsed + effect.detectionDuration,
                };
                _activeDamageWindows.Add(detectionWindow);
                SkillEffectExecutor.ExecuteDamageEffect(effect, _context, detectionWindow.hitTargets);
            }
        }

        private void UpdateActiveDamageWindows()
        {
            if (_activeDamageWindows.Count <= 0)
                return;

            for (int i = _activeDamageWindows.Count - 1; i >= 0; i--)
            {
                ActiveDamageWindow window = _activeDamageWindows[i];
                if (window?.effect == null)
                {
                    CloseCollisionWindow(window);
                    _activeDamageWindows.RemoveAt(i);
                    continue;
                }

                if (_elapsed > window.endTime + 0.0001f)
                {
                    CloseCollisionWindow(window);
                    _activeDamageWindows.RemoveAt(i);
                    continue;
                }

                SkillEffectExecutor.ExecuteDamageEffect(window.effect, _context, window.hitTargets, window.collisionHitbox, window);
            }
        }

        private void ClearActiveDamageWindows()
        {
            for (int i = _activeDamageWindows.Count - 1; i >= 0; i--)
                CloseCollisionWindow(_activeDamageWindows[i]);

            _activeDamageWindows.Clear();
        }

        private static void CloseCollisionWindow(ActiveDamageWindow window)
        {
            if (window?.collisionHitbox == null)
                return;

            DamageDetectionRunner.EndCollisionWindow(window.collisionHitbox, window);
            window.collisionHitbox = null;
        }

        private static EventState[] BuildStates<T>(List<T> events) where T : SkillTimedEventBase
        {
            if (events == null || events.Count <= 0)
                return Array.Empty<EventState>();

            var states = new EventState[events.Count];
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = new EventState
                {
                    hasTriggered = false,
                    nextTriggerTime = events[i] != null
                        ? events[i].startTime
                        : float.MaxValue,
                };
            }

            return states;
        }

        private void EvaluateTrack<T>(List<T> events, EventState[] states, Action<T, ISkillExecutionContext> executor)
            where T : SkillTimedEventBase
        {
            if (events == null || states == null || executor == null)
                return;

            int count = UnityEngine.Mathf.Min(events.Count, states.Length);
            for (int i = 0; i < count; i++)
            {
                T evt = events[i];
                EventState state = states[i];
                if (evt == null || state == null)
                    continue;

                if (evt.triggerMode == SkillEventTriggerMode.Once)
                {
                    if (!state.hasTriggered && _elapsed >= evt.startTime)
                    {
                        executor(evt, _context);
                        state.hasTriggered = true;
                    }
                    continue;
                }

                float endTime = evt.GetEndTime();
                float interval = UnityEngine.Mathf.Max(0.01f, evt.repeatInterval);
                while (_elapsed >= state.nextTriggerTime
                       && state.nextTriggerTime <= endTime + 0.0001f)
                {
                    executor(evt, _context);
                    state.hasTriggered = true;
                    state.nextTriggerTime += interval;
                }
            }
        }
    }
}
