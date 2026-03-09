using System;
using UnityEngine;
using Game.Domain;
using Game.Data;
using Game;
using Game.GameFlow;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Presentation
{
    /// <summary>
    /// 玩家编排层（薄壳，Facade 模式）。
    ///
    /// 职责：
    ///   1. 持有所有子系统引用，实现 IPlayerContext 接口供状态访问
    ///   2. 在 Update 中按固定顺序驱动：InputHandler → StateMachine → Mover
    ///   3. 接收 PlayerInput（Send Messages）回调，转发给 InputHandler
    ///   4. 暴露 Init / OnHit / OnPlayerDied 等外部接入点
    ///
    /// 本类不含任何游戏逻辑，所有动作决策均在各 PlayerStateBase 子类中。
    ///
    /// 挂载要求：同 GameObject 需有 PlayerMover / PlayerAnimatorController /
    ///           PlayerInputHandler / PlayerInput（Behavior = Send Messages）。
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    [RequireComponent(typeof(PlayerAnimatorController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour, IPlayerContext
    {
        // ── IPlayerContext 组件引用 ────────────────────────────────────────
        public PlayerMover              Mover        { get; private set; }
        public PlayerAnimatorController Anim         { get; private set; }
        public PlayerStateMachine       StateMachine { get; private set; }
        public Transform                Transform    => transform;
        public PlayerModel              PlayerModel  { get; private set; }

        // ── IPlayerContext 移动参数（玩家唯一，直接写在脚本内）────────
        public float WalkSpeed   => 3f;
        public float RunSpeed    => 6f;
        public float JumpHeight  => 2f;
        public float DodgeSpeed  => 12f;
        public float RotateSpeed => 720f;

        // ── 普攻连击：有普攻预输入时提前结束的动画进度（0～1）────────────
        [Header("普攻连击 - 普攻预输入（提前结束的动画进度 0～1）")]
        [SerializeField] [Range(0f, 1f)] [Tooltip("第 1～3 段：有普攻预输入时，动画播放到此比例即提前切下一段")]
        private float _comboEarlyExitThresholdSegment1To3 = 0.70f;
        [SerializeField] [Range(0f, 1f)] [Tooltip("第 4 段：有普攻预输入时，动画播放到此比例即提前结束")]
        private float _comboEarlyExitThresholdSegment4 = 0.60f;
        public float ComboEarlyExitThresholdSegment1To3 => _comboEarlyExitThresholdSegment1To3;
        public float ComboEarlyExitThresholdSegment4   => _comboEarlyExitThresholdSegment4;

        // ── 普攻连击：有移动预输入时提前结束的动画进度（0～1）────────────
        [Header("普攻连击 - 移动预输入（提前结束的动画进度 0～1）")]
        [SerializeField] [Range(0f, 1f)] [Tooltip("第 1～3 段：有移动预输入时，动画播放到此比例即提前结束")]
        private float _moveComboEarlyExitThresholdSegment1To3 = 0.80f;
        [SerializeField] [Range(0f, 1f)] [Tooltip("第 4 段：有移动预输入时，动画播放到此比例即提前结束")]
        private float _moveComboEarlyExitThresholdSegment4 = 0.70f;
        public float MoveComboEarlyExitThresholdSegment1To3 => _moveComboEarlyExitThresholdSegment1To3;
        public float MoveComboEarlyExitThresholdSegment4   => _moveComboEarlyExitThresholdSegment4;

        // ── 连续受击保护 ──────────────────────────────────────────────────
        [Header("连续受击保护")]
        [SerializeField] [Tooltip("触发保护的时间窗口（秒）")]
        private float _hitProtectionWindow = 2f;
        [SerializeField] [Tooltip("时间窗口内受击次数达到此值触发霸体")]
        private int _hitProtectionThreshold = 3;
        [SerializeField] [Tooltip("触发后获得的霸体持续时间（秒）")]
        private float _hitProtectionDuration = 1f;

        // ── 内部引用 ──────────────────────────────────────────────────────
        private PlayerInputHandler _inputHandler;
        private bool               _initialized;
        private ThirdPersonCamera  _camera;
        private bool               _loggedMissingCameraWarning;
        private bool               _deathSequenceStarted;
        private bool               _deathSequenceCompleted;

        // ── 连续受击保护状态 ──────────────────────────────────────────────
        private HitProtectionSystem _hitProtectionSystem;

        // ── 外部事件 ──────────────────────────────────────────────────────
        /// <summary>玩家死亡时触发（由 LevelBootstrapper 订阅，接入游戏流程）</summary>
        public event Action OnPlayerDied;
        public bool IsDead => _deathSequenceStarted;

        // ── Awake：获取组件引用 ───────────────────────────────────────────
        private void Awake()
        {
            Mover         = GetComponent<PlayerMover>();
            Anim          = GetComponent<PlayerAnimatorController>();
            _inputHandler = GetComponent<PlayerInputHandler>();
            ResolveCameraReference();
        }

        // ── 初始化（由 LevelBootstrapper 调用）────────────────────────────
        public void Init(PlayerModel playerModel)
        {
            PlayerModel  = playerModel;
            StateMachine = new PlayerStateMachine(this);
            _deathSequenceStarted = false;
            _deathSequenceCompleted = false;
            _hitProtectionSystem = new HitProtectionSystem(_hitProtectionWindow, _hitProtectionThreshold, _hitProtectionDuration);
            ResolveCameraReference();

#if ENABLE_INPUT_SYSTEM
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                _inputHandler.Init(playerInput);
#endif

            Cursor.lockState = CursorLockMode.Locked;
            StateMachine.ChangeState<IdleState>();
            _initialized = true;
        }

        // ── 主循环（InputHandler → StateMachine → Mover，顺序不可调换）────
        private void Update()
        {
            if (!_initialized || _inputHandler == null || StateMachine == null) return;
            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true) return;

            _inputHandler.ManualUpdate();
            var input = _inputHandler.CurrentInput;
            StateMachine.Tick(Time.deltaTime, input);
            Mover.Tick(Time.deltaTime);
        }

        // ── IPlayerContext 当前状态查询（HUD / 调试）──────────────────────
        public string CurrentStateName =>
            StateMachine?.CurrentState != null ? StateMachine.CurrentState.GetType().Name : "None";

        public GameAction CurrentActionId =>
            StateMachine?.CurrentState != null ? StateMachine.CurrentState.CurrentActionId : GameAction.None;

        public float StateRemainingTime
        {
            get
            {
                if (StateMachine?.CurrentState is IPlayerStateWithDuration d) return d.RemainingTime;
                return -1f;
            }
        }

        public float StateNormalizedProgress
        {
            get
            {
                if (StateMachine?.CurrentState is IPlayerStateWithDuration d) return d.NormalizedProgress;
                return -1f;
            }
        }

        // ── IPlayerContext 工具方法 ───────────────────────────────────────
        public Vector3 GetMoveDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.01f) return Vector3.zero;
            ResolveCameraReference();
            float yaw = _camera != null ? _camera.Yaw : transform.eulerAngles.y;
            return Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
        }

        private void ResolveCameraReference()
        {
            if (_camera != null) return;

            _camera = ThirdPersonCamera.Active;
            if (_camera == null)
                _camera = FindFirstObjectByType<ThirdPersonCamera>();

            if (_camera == null && !_loggedMissingCameraWarning)
            {
                Debug.LogWarning("[PlayerController] Scene ThirdPersonCamera not found. Movement will fall back to player yaw.");
                _loggedMissingCameraWarning = true;
            }
            else if (_camera != null)
            {
                _loggedMissingCameraWarning = false;
            }
        }

        // ── Send Messages 回调（PlayerInput → Behavior = Send Messages）──
#if ENABLE_INPUT_SYSTEM
        private void OnAttackTap(InputValue _)     => _inputHandler.RegisterAttackTap();

        private void OnChargeStart(InputValue value)
        {
            if (value.Get<float>() > 0.5f)
                _inputHandler.RegisterChargeStart();
        }

        private void OnChargeRelease(InputValue _) => _inputHandler.RegisterChargeRelease();
#endif

        // ── 受击入口 ───────────────────────────────────────────────────────
        /// <summary>
        /// 受击入口，由敌方攻击逻辑调用。伤害应由 CombatCalculator.CalculateDamageFromEnemy(敌人攻击力, PlayerModel.Stats.Defense) 计算（无暴击无增伤）。
        /// DodgeState 整段为无敌帧，自动豁免；全程霸体 Buff 或连续受击保护霸体下不进入硬直只扣血。
        /// </summary>
        public void OnHit(float damage, float stunDuration)
        {
            if (!_initialized) return;
            if (_deathSequenceStarted) return;
            if (StateMachine.CurrentState is DodgeState) return;

            // 检查是否处于霸体状态（Buff或连续受击保护）
            bool hasHitProtection = _hitProtectionSystem.IsProtected;
            bool hasSuperArmor = PlayerModel.HasBuff(Game.Data.BuffIds.SuperArmor);

            PlayerModel.TakeDamage(damage);

            // 只有在非霸体状态下才累计受击次数
            if (!hasHitProtection && !hasSuperArmor)
            {
                _hitProtectionSystem.RecordHit();
            }

            // 判断是否进入硬直状态（霸体Buff或连续受击保护可以免疫硬直）
            if (!hasSuperArmor && !hasHitProtection)
                StateMachine.ChangeState<HitStunState>(s => s.Duration = stunDuration);

            if (PlayerModel.CurrentHp <= 0f)
                HandleDeath();
        }

        private void HandleDeath()
        {
            if (_deathSequenceStarted) return;

            Debug.Log("[PlayerController] Player died.");
            _deathSequenceStarted = true;
            StateMachine.ChangeState<PlayerDeathState>();
        }

        public void CompleteDeathSequence()
        {
            if (_deathSequenceCompleted) return;

            _deathSequenceCompleted = true;
            _initialized = false;
            OnPlayerDied?.Invoke();
        }
    }
}
