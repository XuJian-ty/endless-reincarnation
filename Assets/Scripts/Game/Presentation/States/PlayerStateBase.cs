using UnityEngine;

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

        private float _stateAge;

        /// <summary>当前状态已持续运行的时间（秒）。子类可用于时序/首帧保护。</summary>
        protected float StateAge => _stateAge;

        // ── 生命周期（由状态机调用，不可 override）──────────────────────
        internal void Enter(IPlayerContext ctx)
        {
            Ctx       = ctx;
            _stateAge = 0f;
            OnEnter();
        }

        internal void Exit()
        {
            OnExit();
            Ctx = null;
        }

        internal void Tick(float dt, in PlayerInputData input)
        {
            _stateAge += dt;
            OnTick(dt, in input);
        }

        // ── 子类实现点（Template Method）────────────────────────────────
        protected abstract void OnEnter();
        protected virtual  void OnExit() { }
        protected abstract void OnTick(float dt, in PlayerInputData input);

        /// <summary>当前状态对应的动作 ID，供 HUD/调试用；默认 None。</summary>
        public virtual GameAction CurrentActionId => GameAction.None;

        // ── 策略表（Strategy Pattern）────────────────────────────────────
        public virtual TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Dodge        => TransitionPolicy.Interrupt,
            GameAction.Skill        => TransitionPolicy.Interrupt,
            GameAction.ChargeStart  => TransitionPolicy.Interrupt,
            GameAction.NormalAttack => TransitionPolicy.Buffer,
            GameAction.Jump         => TransitionPolicy.Buffer,
            GameAction.Walk         => TransitionPolicy.Buffer,
            GameAction.Run          => TransitionPolicy.Buffer,
            _                       => TransitionPolicy.Ignore,
        };

        // ── 过渡辅助 ─────────────────────────────────────────────────────
        /// <summary>
        /// 动画播放完成检测：当前状态为一次性动画且进度 >= threshold 时为 true；带 0.1s 保护期。
        /// </summary>
        protected bool AnimNearEnd(float threshold = 0.9f)
            => _stateAge > 0.1f && Ctx.Anim.IsCurrentStateNearEnd(threshold);

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
    }
}
