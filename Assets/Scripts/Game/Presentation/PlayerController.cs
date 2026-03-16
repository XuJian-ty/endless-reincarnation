using System;
using System.Collections.Generic;
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
    public class PlayerController : MonoBehaviour, IPlayerContext, ICombatHardControlReceiver
    {
        // ── IPlayerContext 组件引用 ────────────────────────────────────────
        public PlayerMover              Mover        { get; private set; }
        public PlayerAnimatorController Anim         { get; private set; }
        public PlayerStateMachine       StateMachine { get; private set; }
        public Transform                Transform    => transform;
        public PlayerModel              PlayerModel  { get; private set; }

        // ── IPlayerContext 移动参数（玩家唯一，直接写在脚本内）────────
        public float WalkSpeed   => Mathf.Max(0.1f, RunSpeed * 0.5f);
        public float RunSpeed    => Mathf.Max(0.1f, PlayerModel != null ? PlayerModel.Stats.MoveSpeed : 6f);
        public float JumpHeight  => 2f;
        public float DodgeSpeed  => 12f;
        public float RotateSpeed => 720f;

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
        private float              _temporarySuperArmorTimer;
        private float              _temporaryInvincibleTimer;
        private PlayerCloneManager _cloneManager;
        private readonly List<SkillTimelineRunner> _detachedTimelineRunners = new List<SkillTimelineRunner>();

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
            var skillConfig = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            PlayerModel?.RefreshUnlockedSkillEffects(skillConfig);
            StateMachine = new PlayerStateMachine(this);
            _deathSequenceStarted = false;
            _deathSequenceCompleted = false;
            _temporarySuperArmorTimer = 0f;
            _temporaryInvincibleTimer = 0f;
            StopDetachedTimelineRunners();
            _hitProtectionSystem = new HitProtectionSystem(_hitProtectionWindow, _hitProtectionThreshold, _hitProtectionDuration);
            ResolveCameraReference();
            _cloneManager = GetComponent<PlayerCloneManager>();
            if (_cloneManager == null)
                _cloneManager = gameObject.AddComponent<PlayerCloneManager>();
            _cloneManager.Initialize(this);

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

            TickTemporaryCombatFlags(Time.deltaTime);
            TickAttributeRegeneration(Time.deltaTime);
            _inputHandler.ManualUpdate();
            var input = _inputHandler.CurrentInput;
            StateMachine.Tick(Time.deltaTime, input);
            TickDetachedTimelineRunners(Time.deltaTime);
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
            if (_camera != null)
            {
                float yaw = _camera.GetMovementYaw();
                Quaternion planarRotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 direction = planarRotation * new Vector3(input.x, 0f, input.y);
                if (direction.sqrMagnitude > 1f)
                    direction.Normalize();
                return direction;
            }

            float fallbackYaw = transform.eulerAngles.y;
            return Quaternion.Euler(0f, fallbackYaw, 0f) * new Vector3(input.x, 0f, input.y);
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

        public void BindCamera(ThirdPersonCamera cameraRig)
        {
            _camera = cameraRig;
            if (_camera != null)
                _loggedMissingCameraWarning = false;
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
            if (_temporaryInvincibleTimer > 0f) return;

            // 检查是否处于霸体状态（Buff或连续受击保护）
            bool hasHitProtection = _hitProtectionSystem.IsProtected;
            bool hasSuperArmor = PlayerModel.HasBuff(Game.Data.BuffIds.SuperArmor) || _temporarySuperArmorTimer > 0f;

            PlayerModel.TakeDamage(damage);

            // 只有在非霸体状态下才累计受击次数
            if (!hasHitProtection && !hasSuperArmor)
            {
                _hitProtectionSystem.RecordHit();
            }

            // 判断是否进入硬直状态（霸体Buff或连续受击保护可以免疫硬直）
            if (!hasSuperArmor && !hasHitProtection)
                ApplyHardControl(stunDuration);

            if (PlayerModel.CurrentHp <= 0f)
                HandleDeath();
        }

        public void ApplyHardControl(float duration)
        {
            if (!_initialized || _deathSequenceStarted)
                return;

            float validDuration = Mathf.Max(0f, duration);
            if (validDuration <= 0f || StateMachine == null)
                return;

            StateMachine.ChangeState<HitStunState>(s => s.Duration = validDuration);
        }

        public void ApplyTemporarySuperArmor(float duration)
        {
            if (duration <= 0f)
                return;

            _temporarySuperArmorTimer = Mathf.Max(_temporarySuperArmorTimer, duration);
        }

        public void ApplyTemporaryInvincibility(float duration)
        {
            if (duration <= 0f)
                return;

            _temporaryInvincibleTimer = Mathf.Max(_temporaryInvincibleTimer, duration);
        }

        private void HandleDeath()
        {
            if (_deathSequenceStarted) return;

            Debug.Log("[PlayerController] Player died.");
            _deathSequenceStarted = true;
            StateMachine.ChangeState<PlayerDeathState>();
        }

        public void ContinueDetachedTimelineRunner(SkillTimelineRunner runner)
        {
            if (runner == null || runner.IsComplete || _detachedTimelineRunners.Contains(runner))
                return;

            _detachedTimelineRunners.Add(runner);
        }

        private void TickDetachedTimelineRunners(float deltaTime)
        {
            if (_detachedTimelineRunners.Count <= 0)
                return;

            for (int i = _detachedTimelineRunners.Count - 1; i >= 0; i--)
            {
                SkillTimelineRunner runner = _detachedTimelineRunners[i];
                if (runner == null || runner.IsComplete)
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
            {
                SkillTimelineRunner runner = _detachedTimelineRunners[i];
                runner?.Stop();
            }

            _detachedTimelineRunners.Clear();
        }

        public void CompleteDeathSequence()
        {
            if (_deathSequenceCompleted) return;

            _deathSequenceCompleted = true;
            _initialized = false;
            StopDetachedTimelineRunners();
            OnPlayerDied?.Invoke();
        }

        private void OnDisable()
        {
            StopDetachedTimelineRunners();
        }

        private void OnDestroy()
        {
            StopDetachedTimelineRunners();
        }

        private void TickTemporaryCombatFlags(float dt)
        {
            if (_temporarySuperArmorTimer > 0f)
                _temporarySuperArmorTimer = Mathf.Max(0f, _temporarySuperArmorTimer - dt);

            if (_temporaryInvincibleTimer > 0f)
                _temporaryInvincibleTimer = Mathf.Max(0f, _temporaryInvincibleTimer - dt);
        }

        private void TickAttributeRegeneration(float dt)
        {
            if (PlayerModel == null || dt <= 0f || _deathSequenceStarted)
                return;

            float hpRegen = Mathf.Max(0f, PlayerModel.Stats.HpRegen);
            if (hpRegen > 0f && PlayerModel.CurrentHp < PlayerModel.Stats.MaxHp)
                PlayerModel.Heal(hpRegen * dt);

            float mpRegen = Mathf.Max(0f, PlayerModel.Stats.MpRegen);
            if (mpRegen > 0f && PlayerModel.CurrentMp < PlayerModel.Stats.MaxMp)
                PlayerModel.CurrentMp = Mathf.Min(PlayerModel.Stats.MaxMp, PlayerModel.CurrentMp + mpRegen * dt);
        }
    }
}
