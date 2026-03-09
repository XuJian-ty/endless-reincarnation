using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 技能时间轴运行器：接受 <see cref="SharedSkillDefinition"/> 并逐帧推进，
    /// 在合适的时间点调用 <see cref="SkillEffectExecutor.ExecuteEvent"/>。
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

        private SharedSkillDefinition _definition;
        private ISkillExecutionContext _context;
        private float _elapsed;
        private float _castDuration;
        private EventState[] _eventStates;
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

            if (_definition?.events != null && _definition.events.Count > 0)
            {
                _eventStates = new EventState[_definition.events.Count];
                for (int i = 0; i < _eventStates.Length; i++)
                {
                    _eventStates[i] = new EventState
                    {
                        hasTriggered = false,
                        nextTriggerTime = _definition.events[i] != null
                            ? _definition.events[i].startTime
                            : float.MaxValue,
                    };
                }
            }
            else
            {
                _eventStates = Array.Empty<EventState>();
            }

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
        }

        // ── 内部评估 ──────────────────────────────────────────────────────────

        private void EvaluateEvents()
        {
            if (_definition?.events == null || _eventStates == null) return;

            for (int i = 0; i < _definition.events.Count; i++)
            {
                var evt = _definition.events[i];
                var state = _eventStates[i];
                if (evt == null || state == null) continue;

                if (evt.triggerMode == SkillEventTriggerMode.Once)
                {
                    if (!state.hasTriggered && _elapsed >= evt.startTime)
                    {
                        SkillEffectExecutor.ExecuteEvent(evt, _context);
                        state.hasTriggered = true;
                    }
                    continue;
                }

                // Repeated 模式
                float endTime = evt.startTime + UnityEngine.Mathf.Max(0f, evt.activeDuration);
                float interval = UnityEngine.Mathf.Max(0.01f, evt.repeatInterval);

                while (_elapsed >= state.nextTriggerTime
                       && state.nextTriggerTime <= endTime + 0.0001f)
                {
                    SkillEffectExecutor.ExecuteEvent(evt, _context);
                    state.hasTriggered = true;
                    state.nextTriggerTime += interval;
                }
            }
        }
    }
}
