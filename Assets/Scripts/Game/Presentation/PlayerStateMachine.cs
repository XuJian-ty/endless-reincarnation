using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 玩家状态机（State Machine + Flyweight + Chain of Responsibility）。
    ///
    /// 核心职责：
    ///   1. Flyweight：以 Type 为键缓存状态实例，ChangeState 时复用（零 GC）
    ///   2. 输入→动作映射：将 PlayerInputData 按优先级转为 GameAction 候选列表
    ///   3. 策略应用：调用当前状态的 GetPolicyFor() → Interrupt / Buffer / Ignore
    ///   4. 预输入管理：_pending 最多缓存一条，后来的覆盖先来的
    ///   5. 连击窗口：攻击结束后立即回到 Idle/Move，在窗口期内再按攻击则路由到下一段
    /// </summary>
    public class PlayerStateMachine
    {
        public const float ComboWindowDuration = 0.3f;

        // ── Flyweight 缓存 ────────────────────────────────────────────────
        private readonly Dictionary<Type, PlayerStateBase> _states =
            new Dictionary<Type, PlayerStateBase>();

        public PlayerStateBase CurrentState  { get; private set; }
        public PlayerStateBase PreviousState { get; private set; }

        private readonly IPlayerContext _ctx;

        // ── 预输入 ────────────────────────────────────────────────────────
        private PendingActionData _pending = PendingActionData.Empty;

        // ── 连击窗口（攻击结束后仍可衔接下一段的时间窗）─────────────────
        private int   _comboNextIndex = -1;
        private float _comboTimer;

        // ── 候选动作缓冲区（避免每帧 GC）────────────────────────────────
        private readonly List<PendingActionData> _candidates = new List<PendingActionData>(12);

        public PlayerStateMachine(IPlayerContext ctx) => _ctx = ctx;

        // ── ChangeState ───────────────────────────────────────────────────
        public void ChangeState<T>() where T : PlayerStateBase, new()
            => TransitionTo(GetOrCreate<T>());

        public void ChangeState<T>(Action<T> configure) where T : PlayerStateBase, new()
        {
            var next = GetOrCreate<T>();
            configure?.Invoke(next);
            TransitionTo(next);
        }

        // ── 每帧驱动 ──────────────────────────────────────────────────────
        public void Tick(float dt, in PlayerInputData input)
        {
            if (CurrentState == null) return;
            if (_comboTimer > 0f)
            {
                _comboTimer -= dt;
                if (_comboTimer <= 0f) ClearComboWindow();
            }
            ProcessActions(in input);
            CurrentState?.Tick(dt, in input);
        }

        // ── 动作执行（公开，供 CompleteWithPending 调用）─────────────────
        public void ExecuteAction(PendingActionData action)
        {
            if (action.Action is not (GameAction.NormalAttack or GameAction.Walk or GameAction.Run))
                ClearComboWindow();

            switch (action.Action)
            {
                case GameAction.Dodge:         ChangeState<DodgeState>();           break;
                case GameAction.Skill:         ExecuteSkill(action.SkillIndex);     break;
                case GameAction.ChargeStart:   ChangeState<ChargeStartState>();     break;
                case GameAction.ChargeRelease: ChangeState<ChargeReleaseState>();   break;
                case GameAction.NormalAttack:  ExecuteNormalAttack();               break;
                case GameAction.AirAttack:     ChangeState<AirAttackState>();       break;
                case GameAction.FallAttack:    ChangeState<FallAttackStartState>(); break;
                case GameAction.Jump:          ChangeState<JumpState>();            break;
                case GameAction.Walk:          ChangeState<MoveState>(s => s.InitialIsRunning = false); break;
                case GameAction.Run:           ChangeState<MoveState>(s => s.InitialIsRunning = true);  break;
            }
        }

        // ── Pending API ───────────────────────────────────────────────────
        public PendingActionData ConsumePending()
        {
            var p = _pending;
            _pending = PendingActionData.Empty;
            return p;
        }

        public PendingActionData PeekPending() => _pending;

        public void ClearPending() => _pending = PendingActionData.Empty;

        // ── 连击窗口 ────────────────────────────────────────────────────
        /// <summary>攻击结束时调用，设置连击窗口：在窗口期内按攻击将路由到 nextAttackIndex 对应的攻击段。</summary>
        public void OpenComboWindow(int nextAttackIndex)
        {
            _comboNextIndex = nextAttackIndex;
            _comboTimer     = ComboWindowDuration;
        }

        private void ClearComboWindow()
        {
            _comboNextIndex = -1;
            _comboTimer     = 0f;
        }

        private void ExecuteNormalAttack()
        {
            int idx = _comboNextIndex;
            ClearComboWindow();
            switch (idx)
            {
                case 1:  ChangeState<Attack2State>(); break;
                case 2:  ChangeState<Attack3State>(); break;
                case 3:  ChangeState<Attack4State>(); break;
                default: ChangeState<Attack1State>(); break;
            }
        }

        // ── 私有：输入→动作→策略 ─────────────────────────────────────────
        private void ProcessActions(in PlayerInputData input)
        {
            CollectCandidateActions(in input);

            foreach (var candidate in _candidates)
            {
                if (CurrentState == null) return;
                switch (CurrentState.GetPolicyFor(candidate.Action))
                {
                    case TransitionPolicy.Interrupt:
                        ExecuteAction(candidate);
                        return;
                    case TransitionPolicy.Buffer:
                        // 只保留优先级最高（最先遇到）的缓存动作，
                        // 防止低优先级的 Walk/Run 覆盖已缓存的 NormalAttack。
                        if (_pending.IsEmpty)
                            _pending = candidate;
                        break;
                }
            }
        }

        private void CollectCandidateActions(in PlayerInputData input)
        {
            _candidates.Clear();

            if (input.DodgePressed)         _candidates.Add(new PendingActionData(GameAction.Dodge));
            if (input.Skill0Pressed)        _candidates.Add(new PendingActionData(GameAction.Skill, 0));
            if (input.Skill1Pressed)        _candidates.Add(new PendingActionData(GameAction.Skill, 1));
            if (input.Skill2Pressed)        _candidates.Add(new PendingActionData(GameAction.Skill, 2));
            if (input.Skill3Pressed)        _candidates.Add(new PendingActionData(GameAction.Skill, 3));
            if (input.ChargeReleasePressed) _candidates.Add(new PendingActionData(GameAction.ChargeRelease));
            if (input.AltAttackPressed)     _candidates.Add(new PendingActionData(GameAction.FallAttack));
            if (input.ChargeStartPressed)   _candidates.Add(new PendingActionData(GameAction.ChargeStart));

            if (input.AttackTapPressed)
            {
                bool inAir = IsAirState(CurrentState);
                _candidates.Add(new PendingActionData(inAir ? GameAction.AirAttack : GameAction.NormalAttack));
            }

            if (input.JumpPressed) _candidates.Add(new PendingActionData(GameAction.Jump));

            if (input.MoveInput.sqrMagnitude > 0.01f)
                _candidates.Add(new PendingActionData(
                    input.IsRunRequested ? GameAction.Run : GameAction.Walk));
        }

        private static bool IsAirState(PlayerStateBase state) =>
            state is JumpState
            or FallState
            or AirAttackState
            or FallAttackStartState
            or FallAttackLoopState;

        private void ExecuteSkill(int skillIndex)
        {
            switch (skillIndex)
            {
                case 0: ChangeState<Skill0State>(); break;
                case 1: ChangeState<Skill1State>(); break;
                case 2: ChangeState<Skill2State>(); break;
                case 3: ChangeState<Skill3State>(); break;
            }
        }

        private void TransitionTo(PlayerStateBase next)
        {
            if (next == CurrentState) return;

            PreviousState = CurrentState;
            CurrentState?.Exit();
            CurrentState = next;
            CurrentState.Enter(_ctx);
        }

        private T GetOrCreate<T>() where T : PlayerStateBase, new()
        {
            var key = typeof(T);
            if (!_states.TryGetValue(key, out var state))
                _states[key] = state = new T();
            return (T)state;
        }
    }
}
