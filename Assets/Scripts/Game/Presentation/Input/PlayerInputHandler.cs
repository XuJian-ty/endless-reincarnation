using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation
{
    /// <summary>
    /// 负责采集和预处理原始输入，每帧构建一次 PlayerInputData 快照。
    ///
    /// 职责划分：
    ///   - 持续输入（Move、Look）：直接从 InputAction 读取当前值
    ///   - 双击奔跑检测：内部维护时间戳，识别 WASD 双击
    ///   - LMB 三段式事件（AttackTap / ChargeStart / ChargeRelease）：
    ///       由 PlayerController 的 Send Messages 回调写入，
    ///       此类仅暴露 Register 方法，在 ManualUpdate 中打包后归零
    ///   - 所有单帧标记在 ManualUpdate 读取后立即归零（消费语义）
    ///
    /// 不含任何游戏逻辑，是纯粹的输入层组件。
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        // ── 双击奔跑配置 ──────────────────────────────────────────────────
        [SerializeField] private float _doubleTapWindow = 0.25f;

        // ── 双击检测状态 ──────────────────────────────────────────────────
        private float  _lastMoveStartTime  = -1f;
        private bool   _wasMoving          = false;
        private bool   _isRunRequested     = false;

        // ── LMB Interaction 回调标记（由 PlayerController 写入）─────────
        private bool _attackTapReceived;
        private bool _chargeStartReceived;
        private bool _chargeReleaseReceived;

        // ── LMB 实时按压状态（由 PlayerController 的 Hold performed/canceled 维护）
        private bool _isLmbHeld;
        public  bool IsLmbHeld => _isLmbHeld;

        // ── 其他单帧标记 ──────────────────────────────────────────────────
        private bool _jumpReceived;
        private bool _dodgeReceived;
        private bool _altAttackReceived;

        // ── InputAction 引用（在 Inspector 绑定或由 PlayerInput 注入）────
#if ENABLE_INPUT_SYSTEM
        private sealed class SkillInputBinding
        {
            public int slotIndex;
            public InputAction action;
            public Action<InputAction.CallbackContext> callback;
            public bool received;
        }

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _dodgeAction;
        private InputAction _fallAttackAction;
        private InputAction _chargeStartAction;
        private readonly List<SkillInputBinding> _skillBindings = new List<SkillInputBinding>(4);
        private readonly List<int> _pressedSkillIndices = new List<int>(4);

        // ── 具名委托存储：订阅与取消订阅必须使用同一个引用 ──────────────
        // ⚠ Lambda 每次创建不同实例，用 -= 取消时根本不是原来那个！
        //   必须将委托存为字段，才能正确取消订阅，避免内存泄漏和野引用回调。
        private System.Action<InputAction.CallbackContext> _onJump;
        private System.Action<InputAction.CallbackContext> _onDodge;
        private System.Action<InputAction.CallbackContext> _onFallAttack;
#endif

        // ── 当前帧快照（ManualUpdate 后可读）────────────────────────────
        public PlayerInputData CurrentInput { get; private set; }

        // ── 初始化 ────────────────────────────────────────────────────────
#if ENABLE_INPUT_SYSTEM
        public void Init(PlayerInput playerInput)
        {
            _moveAction       = playerInput.actions["Move"];
            _lookAction       = playerInput.actions["Look"];
            _jumpAction       = playerInput.actions["Jump"];
            _dodgeAction      = playerInput.actions["Dodge"];
            _fallAttackAction = playerInput.actions["FallAttack"];
            _chargeStartAction = playerInput.actions["ChargeStart"];

            SubscribeDirectActions();
            SubscribeSkillActions(playerInput);
        }

        private void SubscribeDirectActions()
        {
            _onJump       = _ => _jumpReceived      = true;
            _onDodge      = _ => _dodgeReceived     = true;
            _onFallAttack = _ => _altAttackReceived = true;

            _jumpAction.performed       += _onJump;
            _dodgeAction.performed      += _onDodge;
            _fallAttackAction.performed += _onFallAttack;
        }

        private void SubscribeSkillActions(PlayerInput playerInput)
        {
            ClearSkillSubscriptions();

            InputActionMap gameplayMap = playerInput?.actions?.FindActionMap("Gameplay", false);
            if (gameplayMap == null)
                return;

            foreach (InputAction action in gameplayMap.actions)
            {
                if (!PlayerActionRouting.TryParseSkillSlotIndex(action?.name, out int slotIndex))
                    continue;

                SkillInputBinding binding = new SkillInputBinding
                {
                    slotIndex = slotIndex,
                    action = action,
                };

                binding.callback = _ => binding.received = true;
                action.performed += binding.callback;
                _skillBindings.Add(binding);
            }

            _skillBindings.Sort((left, right) => left.slotIndex.CompareTo(right.slotIndex));
        }

        private void OnDestroy()
        {
            if (_jumpAction == null) return;

            _jumpAction.performed       -= _onJump;
            _dodgeAction.performed      -= _onDodge;
            _fallAttackAction.performed -= _onFallAttack;
            ClearSkillSubscriptions();
        }
#endif

        // ── 公开注册接口（由 PlayerController Send Messages 回调调用）────
        public void RegisterAttackTap()     => _attackTapReceived    = true;
        public void RegisterChargeStart()   => _chargeStartReceived  = true;
        public void RegisterChargeRelease() => _chargeReleaseReceived = true;

        // ── 每帧更新（由 PlayerController.Update 调用）──────────────────
        public void ManualUpdate()
        {
#if ENABLE_INPUT_SYSTEM
            var moveRaw = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            var lookRaw = _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
            UpdateRunDetection(moveRaw);

            // 实时同步 LMB 按压状态：Hold Interaction 的 canceled 不走 Send Messages，
            // 所以用 action phase 判断——只有 Started 或 Performed 才算按住
            if (_chargeStartAction != null)
            {
                var phase = _chargeStartAction.phase;
                _isLmbHeld = phase == InputActionPhase.Started || phase == InputActionPhase.Performed;
            }

            _pressedSkillIndices.Clear();
            CollectPressedSkillIndices(_pressedSkillIndices);

            CurrentInput = new PlayerInputData(
                moveInput:            moveRaw,
                lookDelta:            lookRaw,
                isRunRequested:       _isRunRequested,
                isLmbHeld:            _isLmbHeld,
                jumpPressed:          Consume(ref _jumpReceived),
                dodgePressed:         Consume(ref _dodgeReceived),
                attackTapPressed:     Consume(ref _attackTapReceived),
                chargeStartPressed:   Consume(ref _chargeStartReceived),
                chargeReleasePressed: Consume(ref _chargeReleaseReceived),
                altAttackPressed:     Consume(ref _altAttackReceived),
                pressedSkillIndices:  _pressedSkillIndices);
#else
            CurrentInput = default;
#endif
        }

        // ── 双击奔跑检测 ──────────────────────────────────────────────────
        private void UpdateRunDetection(Vector2 moveRaw)
        {
            bool isMovingNow = moveRaw.sqrMagnitude > 0.01f;

            if (isMovingNow && !_wasMoving)
            {
                float now = Time.unscaledTime;
                if (now - _lastMoveStartTime <= _doubleTapWindow)
                    _isRunRequested = true;
                _lastMoveStartTime = now;
            }
            else if (!isMovingNow)
            {
                _isRunRequested = false;
            }

            _wasMoving = isMovingNow;
        }

        // ── 工具方法 ──────────────────────────────────────────────────────
        private static bool Consume(ref bool flag)
        {
            if (!flag) return false;
            flag = false;
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        private void CollectPressedSkillIndices(List<int> pressedSkillIndices)
        {
            if (pressedSkillIndices == null)
                return;

            for (int i = 0; i < _skillBindings.Count; i++)
            {
                SkillInputBinding binding = _skillBindings[i];
                if (binding == null || !Consume(ref binding.received))
                    continue;

                pressedSkillIndices.Add(binding.slotIndex);
            }
        }

        private void ClearSkillSubscriptions()
        {
            for (int i = 0; i < _skillBindings.Count; i++)
            {
                SkillInputBinding binding = _skillBindings[i];
                if (binding?.action == null || binding.callback == null)
                    continue;

                binding.action.performed -= binding.callback;
            }

            _skillBindings.Clear();
        }

        private static bool TryParseSkillActionName(string actionName, out int slotIndex)
        {
            return PlayerActionRouting.TryParseSkillSlotIndex(actionName, out slotIndex);
        }
#endif
    }
}
