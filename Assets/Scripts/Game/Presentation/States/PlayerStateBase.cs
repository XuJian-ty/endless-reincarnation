using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 所有玩家状态的抽象基类（State Pattern + Template Method Pattern）。
    ///
    /// 设计原则：
    ///   - 状态只负责「切换决策」，不包含复杂计时/逻辑
    ///   - GetPolicyFor() 是每个状态的「中断策略表」，PlayerStateMachine 据此
    ///     决定新输入是立即打断、缓存还是忽略
    ///   - CompleteWithPending() 供自然结束的状态调用，优先消费预输入，无则走默认
    ///   - Flyweight：每种状态仅实例化一次，被复用，OnEnter/OnExit 替代构造/析构
    ///
    /// 默认策略（适用于有自然结束点的动作状态，如攻击、技能、蓄力）：
    ///   Dodge / Skill / ChargeStart → Interrupt
    ///   NormalAttack / Jump / Walk / Run → Buffer
    ///   其余 → Ignore
    ///
    /// ⚠ 没有自然结束点的状态（不调用 CompleteWithPending）必须覆写 Walk / Run / Jump
    ///   为 Interrupt，否则这些输入会被静默丢弃。当前此类状态：IdleState、MoveState。
    /// </summary>
    public abstract class PlayerStateBase
    {
        protected IPlayerContext Ctx { get; private set; }
        protected virtual string ActionId => TrimStateSuffix(GetType().Name);

        private float _stateAge;
        private SkillTimelineRunner _timelineRunner;

        /// <summary>当前状态已持续运行的时间（秒）。子类可用于时序/首帧保护。</summary>
        protected float StateAge => _stateAge;

        // ── 生命周期（由状态机调用，不可 override）──────────────────────
        internal void Enter(IPlayerContext ctx)
        {
            Ctx       = ctx;
            _stateAge = 0f;
            ApplyActionPlaybackSpeed();
            OnEnter();
        }

        internal void Exit()
        {
            StopTimelineSkill();
            ResetActionPlaybackSpeed();
            OnExit();
            Ctx = null;
        }

        internal void Tick(float dt, in PlayerInputData input)
        {
            _stateAge += dt;
            TickTimeline(dt);
            OnTick(dt, in input);
        }

        // ── 子类实现点（Template Method）────────────────────────────────
        protected abstract void OnEnter();
        protected virtual  void OnExit() { }
        protected abstract void OnTick(float dt, in PlayerInputData input);

        /// <summary>当前状态对应的动作 ID，供 HUD/调试用；默认 None。</summary>
        public virtual GameAction CurrentActionId => GameAction.None;

        // ── 策略表（Strategy Pattern）────────────────────────────────────
        public virtual TransitionPolicy GetPolicyFor(GameAction action)
            => ResolveConfiguredPolicy(action, action switch
            {
                GameAction.Dodge        => TransitionPolicy.Interrupt,
                GameAction.Skill        => TransitionPolicy.Interrupt,
                GameAction.ChargeStart  => TransitionPolicy.Interrupt,
                GameAction.NormalAttack => TransitionPolicy.Buffer,
                GameAction.Jump         => TransitionPolicy.Buffer,
                GameAction.Walk         => TransitionPolicy.Buffer,
                GameAction.Run          => TransitionPolicy.Buffer,
                _                       => TransitionPolicy.Ignore,
            });

        // ── 过渡辅助 ─────────────────────────────────────────────────────
        /// <summary>
        /// 动画播放完成检测：当前状态为一次性动画且进度 >= threshold 时为 true；带 0.1s 保护期。
        /// </summary>
        protected bool AnimNearEnd(float threshold = 0.9f)
            => _stateAge > 0.1f && Ctx.Anim.IsCurrentStateNearEnd(threshold);

        protected bool AnimNearConfiguredEnd(float fallbackThreshold = 0.9f)
            => AnimNearEnd(GetConfiguredNaturalExitThreshold(fallbackThreshold));

        /// <summary>状态自然结束时调用。优先消费预输入并执行；若无预输入，执行 fallback。</summary>
        protected void CompleteWithPending(System.Action fallback)
        {
            var pending = Ctx.StateMachine.ConsumePending();
            if (!pending.IsEmpty)
                Ctx.StateMachine.ExecuteAction(pending);
            else
                fallback?.Invoke();
        }

        protected void GoTo<T>() where T : PlayerStateBase, new()
            => Ctx.StateMachine.ChangeState<T>();

        protected void GoTo<T>(System.Action<T> configure) where T : PlayerStateBase, new()
            => Ctx.StateMachine.ChangeState(configure);

        protected bool IsGrounded => Ctx.Mover.IsGrounded;

        protected SkillConfigEntry ResolvePlayerSkillEntry(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return null;

            var skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            return skillDb != null ? skillDb.GetEntry(skillId) : null;
        }

        protected SkillConfigEntry ResolvePlayerActionEntry(string actionId = null)
        {
            string resolvedActionId = string.IsNullOrWhiteSpace(actionId) ? ActionId : actionId.Trim();
            if (string.IsNullOrWhiteSpace(resolvedActionId))
                return null;

            var skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            return skillDb != null ? skillDb.GetEntryByActionId(resolvedActionId) : null;
        }

        protected SkillConfigEntry ResolveCurrentActionEntry()
        {
            return ResolvePlayerActionEntry(ActionId);
        }

        protected bool IsPlayerSkillAvailable(string skillId)
        {
            SkillConfigEntry entry = ResolvePlayerSkillEntry(skillId);
            if (entry == null)
                return true;
            return Ctx.PlayerModel != null && Ctx.PlayerModel.IsSkillAvailable(entry);
        }

        protected bool TriggerConfiguredAction(string skillId, string fallbackTrigger = null)
        {
            SkillConfigEntry entry = ResolvePlayerSkillEntry(skillId);
            string triggerName = entry != null ? entry.GetResolvedAnimationTrigger() : fallbackTrigger;
            if (string.IsNullOrWhiteSpace(triggerName))
                triggerName = skillId;
            return Ctx.Anim.TriggerAction(triggerName);
        }

        protected bool TriggerConfiguredActionByActionId(string actionId = null, string fallbackTrigger = null)
        {
            string resolvedActionId = string.IsNullOrWhiteSpace(actionId) ? ActionId : actionId.Trim();
            SkillConfigEntry entry = ResolvePlayerActionEntry(resolvedActionId);
            string triggerName = entry != null ? entry.GetResolvedAnimationTrigger() : fallbackTrigger;
            if (string.IsNullOrWhiteSpace(triggerName))
                triggerName = resolvedActionId;
            return Ctx.Anim.TriggerAction(triggerName);
        }

        protected void StartTimelineSkill(string skillId, float overrideDuration = -1f)
        {
            StopTimelineSkill();

            if (string.IsNullOrWhiteSpace(skillId))
                return;

            var sharedDb = ConfigManager.GetInstance()?.GetSkillDatabase();
            if (sharedDb == null)
                return;

            var def = sharedDb.GetEntry(skillId);
            if (def == null)
                return;

            var player = Ctx?.Transform != null ? Ctx.Transform.GetComponent<PlayerController>() : null;
            if (player == null)
                return;

            def = PlayerBuffRuntimeUtility.BuildRuntimeSkillDefinition(player.transform, def, ActionId);
            var ctx = new PlayerSkillExecutionContext(player);
            _timelineRunner = new SkillTimelineRunner();
            _timelineRunner.Begin(def, ctx, overrideDuration);
        }

        protected void StartConfiguredTimelineByActionId(string actionId = null, float overrideDuration = -1f)
        {
            SkillConfigEntry entry = ResolvePlayerActionEntry(actionId);
            string skillId = entry != null ? entry.skillId : string.Empty;
            if (string.IsNullOrWhiteSpace(skillId))
                return;

            StartTimelineSkill(skillId, overrideDuration, string.IsNullOrWhiteSpace(actionId) ? ActionId : actionId.Trim());
        }

        protected void StartTimelineSkill(string skillId, float overrideDuration, string actionId)
        {
            StopTimelineSkill();

            if (string.IsNullOrWhiteSpace(skillId))
                return;

            var sharedDb = ConfigManager.GetInstance()?.GetSkillDatabase();
            if (sharedDb == null)
                return;

            var def = sharedDb.GetEntry(skillId);
            if (def == null)
                return;

            var player = Ctx?.Transform != null ? Ctx.Transform.GetComponent<PlayerController>() : null;
            if (player == null)
                return;

            def = PlayerBuffRuntimeUtility.BuildRuntimeSkillDefinition(player.transform, def, actionId);
            var ctx = new PlayerSkillExecutionContext(player);
            _timelineRunner = new SkillTimelineRunner();
            _timelineRunner.Begin(def, ctx, overrideDuration);
        }

        protected void StopTimelineSkill()
        {
            if (_timelineRunner == null)
                return;

            _timelineRunner.StopStateScopedCues();
            if (_timelineRunner.HasPendingWork)
            {
                var player = Ctx?.Transform != null ? Ctx.Transform.GetComponent<PlayerController>() : null;
                if (player != null)
                    player.ContinueDetachedTimelineRunner(_timelineRunner);
                else
                    _timelineRunner.Stop();
            }
            else
            {
                _timelineRunner.Stop();
            }

            _timelineRunner = null;
        }

        protected TransitionPolicy ResolveConfiguredPolicy(GameAction action, TransitionPolicy fallback)
        {
            SkillConfigEntry entry = ResolveCurrentActionEntry();
            if (entry != null && entry.TryGetPolicy(action, out TransitionPolicy configured))
                return configured;

            return fallback;
        }

        protected float GetConfiguredPendingReleaseThreshold(GameAction pendingAction, float fallbackThreshold)
        {
            SkillConfigEntry entry = ResolveCurrentActionEntry();
            if (entry != null && entry.TryGetPendingReleaseThreshold(pendingAction, out float configured))
                return configured;

            return fallbackThreshold;
        }

        protected float GetConfiguredNaturalExitThreshold(float fallbackThreshold)
        {
            SkillConfigEntry entry = ResolveCurrentActionEntry();
            if (entry != null && entry.overrideNaturalExitNormalizedTime)
                return entry.naturalExitNormalizedTime;

            return fallbackThreshold;
        }

        protected PlayerStateNaturalExitTarget GetConfiguredNaturalExitTarget(PlayerStateNaturalExitTarget fallbackTarget)
        {
            SkillConfigEntry entry = ResolveCurrentActionEntry();
            if (entry == null)
                return fallbackTarget;

            PlayerStateNaturalExitTarget configured = entry.naturalExitTarget;
            return configured != PlayerStateNaturalExitTarget.None ? configured : fallbackTarget;
        }

        protected void GoToConfiguredNaturalExit(PlayerStateNaturalExitTarget fallbackTarget)
        {
            switch (GetConfiguredNaturalExitTarget(fallbackTarget))
            {
                case PlayerStateNaturalExitTarget.IdleState:
                    GoTo<IdleState>();
                    break;
                case PlayerStateNaturalExitTarget.ChargeLoopState:
                    GoTo<ChargeLoopState>();
                    break;
                case PlayerStateNaturalExitTarget.FallState:
                    GoTo<FallState>();
                    break;
                case PlayerStateNaturalExitTarget.FallAttackLoopState:
                    GoTo<FallAttackLoopState>();
                    break;
            }
        }

        private void TickTimeline(float dt)
        {
            _timelineRunner?.Tick(dt);
        }

        private static string TrimStateSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim();
            return normalized.EndsWith("State")
                ? normalized.Substring(0, normalized.Length - "State".Length)
                : normalized;
        }

        private void ApplyActionPlaybackSpeed()
        {
            if (Ctx?.PlayerModel == null || Ctx?.Anim == null)
                return;

            Ctx.Anim.SetPlaybackSpeed(PlayerBuffRuntimeUtility.GetActionPlaybackSpeed(Ctx.PlayerModel, ActionId));
        }

        private void ResetActionPlaybackSpeed()
        {
            Ctx?.Anim?.SetPlaybackSpeed(1f);
        }
    }
}
