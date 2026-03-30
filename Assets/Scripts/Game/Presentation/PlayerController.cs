using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain;
using Game.Data;
using Game;
using Game.GameFlow;
using Game.UI;
using ProjectBase;

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
        private bool               _loggedMissingWeaponVisualWarning;
        private bool               _deathSequenceStarted;
        private bool               _deathSequenceCompleted;
        private Transform          _aimGuideOrigin;
        private float              _temporarySuperArmorTimer;
        private float              _temporaryInvincibleTimer;
        private int                _stateScopedSuperArmorCount;
        private int                _stateScopedInvincibleCount;
        private float              _localCastSpeedOverrideTimer;
        private float              _localCastSpeedOverrideMultiplier = 1f;
        private float              _healthPotionRemainingTime;
        private float              _manaPotionRemainingTime;
        private float              _hpRegenTickAccumulator;
        private float              _mpRegenTickAccumulator;
        private float              _healthPotionTickAccumulator;
        private float              _manaPotionTickAccumulator;
        private PlayerCloneManager _cloneManager;
        private bool               _isAimModeActive;
        private readonly List<SkillTimelineRunner> _detachedTimelineRunners = new List<SkillTimelineRunner>();

        // ── 连续受击保护状态 ──────────────────────────────────────────────
        private HitProtectionSystem _hitProtectionSystem;

        // ── 外部事件 ──────────────────────────────────────────────────────
        /// <summary>玩家死亡时触发（由 LevelBootstrapper 订阅，接入游戏流程）</summary>
        public event Action OnPlayerDied;
        public bool IsDead => _deathSequenceStarted;
        public Vector2 CurrentMoveInput => _inputHandler != null ? _inputHandler.CurrentInput.MoveInput : Vector2.zero;
        public float LocalCastSpeedMultiplier => _localCastSpeedOverrideTimer > 0f
            ? Mathf.Clamp(_localCastSpeedOverrideMultiplier, 0f, 1f)
            : 1f;
        public bool IsAimModeActive => _isAimModeActive
                                       && PlayerModel != null
                                       && PlayerModel.CurrentAttackMode == PlayerAttackMode.Ranged;

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
            if (PlayerModel != null)
                PlayerModel.LoadoutChanged -= OnPlayerLoadoutChanged;

            PlayerModel  = playerModel;
            if (PlayerModel != null)
                PlayerModel.LoadoutChanged += OnPlayerLoadoutChanged;

            var skillConfig = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            PlayerModel?.RefreshUnlockedSkillEffects(skillConfig);
            StateMachine = new PlayerStateMachine(this);
            _deathSequenceStarted = false;
            _deathSequenceCompleted = false;
            _temporarySuperArmorTimer = 0f;
            _temporaryInvincibleTimer = 0f;
            _stateScopedSuperArmorCount = 0;
            _stateScopedInvincibleCount = 0;
            _localCastSpeedOverrideTimer = 0f;
            _localCastSpeedOverrideMultiplier = 1f;
            _healthPotionRemainingTime = 0f;
            _manaPotionRemainingTime = 0f;
            _hpRegenTickAccumulator = 0f;
            _mpRegenTickAccumulator = 0f;
            _healthPotionTickAccumulator = 0f;
            _manaPotionTickAccumulator = 0f;
            _isAimModeActive = false;
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
            RefreshWeaponVisuals();
            RefreshAimPresentation();
            _initialized = true;
        }

        // ── 主循环（InputHandler → StateMachine → Mover，顺序不可调换）────
        private void Update()
        {
            if (!_initialized || _inputHandler == null || StateMachine == null) return;
            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
            {
                RefreshAimPresentation();
                return;
            }

            TickTemporaryCombatFlags(Time.deltaTime);
            TickAttributeRegeneration(Time.deltaTime);
            _inputHandler.ManualUpdate();
            var input = _inputHandler.CurrentInput;
            StateMachine.Tick(Time.deltaTime, input);
            TickDetachedTimelineRunners(Time.deltaTime);
            Mover.Tick(Time.deltaTime);
            RefreshAimPresentation();
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

        private void OnUseHealthPotion(InputValue value)
        {
            if (value.Get<float>() > 0.5f)
                TryUsePotion(PlayerModel.ItemIds.PotionHp);
        }

        private void OnUseManaPotion(InputValue value)
        {
            if (value.Get<float>() > 0.5f)
                TryUsePotion(PlayerModel.ItemIds.PotionMp);
        }

        private void OnToggleAttackMode(InputValue value)
        {
            if (value.Get<float>() > 0.5f)
                ToggleAttackMode();
        }

        private void OnAim(InputValue value)
        {
            if (value.Get<float>() > 0.5f)
                ToggleAimMode();
        }
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
            if (_temporaryInvincibleTimer > 0f || _stateScopedInvincibleCount > 0) return;

            // 检查是否处于霸体状态（Buff或连续受击保护）
            bool hasHitProtection = _hitProtectionSystem.IsProtected;
            bool hasSuperArmor = PlayerModel.HasBuff(Game.Data.BuffIds.SuperArmor)
                                 || _temporarySuperArmorTimer > 0f
                                 || _stateScopedSuperArmorCount > 0;

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

        public void ApplyLocalHitStop(float duration, float castSpeedMultiplier)
        {
            if (duration <= 0f)
                return;

            _localCastSpeedOverrideTimer = Mathf.Max(_localCastSpeedOverrideTimer, duration);
            _localCastSpeedOverrideMultiplier = Mathf.Clamp01(
                Mathf.Min(_localCastSpeedOverrideMultiplier, castSpeedMultiplier));
        }

        public void AddStateScopedSuperArmor()
        {
            _stateScopedSuperArmorCount++;
        }

        public void RemoveStateScopedSuperArmor()
        {
            _stateScopedSuperArmorCount = Mathf.Max(0, _stateScopedSuperArmorCount - 1);
        }

        public void AddStateScopedInvincibility()
        {
            _stateScopedInvincibleCount++;
        }

        public void RemoveStateScopedInvincibility()
        {
            _stateScopedInvincibleCount = Mathf.Max(0, _stateScopedInvincibleCount - 1);
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

        public bool TryGetDetachedCameraOverride(float lookTargetHeight, out SkillTimelineRunner.CameraOverrideRequest request)
        {
            request = default;
            if (_detachedTimelineRunners.Count <= 0)
                return false;

            for (int i = _detachedTimelineRunners.Count - 1; i >= 0; i--)
            {
                SkillTimelineRunner runner = _detachedTimelineRunners[i];
                if (runner != null && !runner.IsComplete && runner.TryGetCurrentCameraOverride(lookTargetHeight, out request))
                    return request.IsActive;
            }

            return false;
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
            PlayerAimCrosshairRuntime.SetVisible(false);
            PlayerAimCrosshairRuntime.SetAimGuide(Vector3.zero, Vector3.zero, false);
        }

        private void OnDestroy()
        {
            if (PlayerModel != null)
                PlayerModel.LoadoutChanged -= OnPlayerLoadoutChanged;

            StopDetachedTimelineRunners();
            PlayerAimCrosshairRuntime.SetVisible(false);
            PlayerAimCrosshairRuntime.SetAimGuide(Vector3.zero, Vector3.zero, false);
        }

        private void TickTemporaryCombatFlags(float dt)
        {
            if (_temporarySuperArmorTimer > 0f)
                _temporarySuperArmorTimer = Mathf.Max(0f, _temporarySuperArmorTimer - dt);

            if (_temporaryInvincibleTimer > 0f)
                _temporaryInvincibleTimer = Mathf.Max(0f, _temporaryInvincibleTimer - dt);

            if (_localCastSpeedOverrideTimer > 0f)
            {
                _localCastSpeedOverrideTimer = Mathf.Max(0f, _localCastSpeedOverrideTimer - dt);
                if (_localCastSpeedOverrideTimer <= 0f)
                    _localCastSpeedOverrideMultiplier = 1f;
            }
        }

        private void TickAttributeRegeneration(float dt)
        {
            if (PlayerModel == null || dt <= 0f || _deathSequenceStarted)
                return;

            _hpRegenTickAccumulator += dt;
            while (_hpRegenTickAccumulator >= 1f)
            {
                _hpRegenTickAccumulator -= 1f;
                float hpRegen = Mathf.Max(0f, PlayerModel.Stats.HpRegen);
                if (hpRegen > 0f && PlayerModel.CurrentHp < PlayerModel.Stats.MaxHp)
                    ApplyHealWithCombatNumber(hpRegen);
            }

            _mpRegenTickAccumulator += dt;
            while (_mpRegenTickAccumulator >= 1f)
            {
                _mpRegenTickAccumulator -= 1f;
                float mpRegen = Mathf.Max(0f, PlayerModel.Stats.MpRegen);
                if (mpRegen > 0f && PlayerModel.CurrentMp < PlayerModel.Stats.MaxMp)
                    ApplyManaWithCombatNumber(mpRegen);
            }

            TickPotionEffects(dt);
        }

        private void TickPotionEffects(float dt)
        {
            PotionConfigSO potionConfig = ConfigManager.GetInstance()?.GetPotionConfig();
            if (potionConfig == null)
                return;

            if (_healthPotionRemainingTime > 0f)
            {
                float activeDelta = Mathf.Min(dt, _healthPotionRemainingTime);
                _healthPotionRemainingTime = Mathf.Max(0f, _healthPotionRemainingTime - dt);
                _healthPotionTickAccumulator += activeDelta;
                while (_healthPotionTickAccumulator >= 1f)
                {
                    _healthPotionTickAccumulator -= 1f;
                    float deltaHp = PlayerModel.Stats.MaxHp * Mathf.Max(0f, potionConfig.hpPercentPerSecond);
                    if (deltaHp > 0f && PlayerModel.CurrentHp < PlayerModel.Stats.MaxHp)
                        ApplyHealWithCombatNumber(deltaHp);
                }
            }
            else
            {
                _healthPotionTickAccumulator = 0f;
            }

            if (_manaPotionRemainingTime > 0f)
            {
                float activeDelta = Mathf.Min(dt, _manaPotionRemainingTime);
                _manaPotionRemainingTime = Mathf.Max(0f, _manaPotionRemainingTime - dt);
                _manaPotionTickAccumulator += activeDelta;
                while (_manaPotionTickAccumulator >= 1f)
                {
                    _manaPotionTickAccumulator -= 1f;
                    float deltaMp = PlayerModel.Stats.MaxMp * Mathf.Max(0f, potionConfig.mpPercentPerSecond);
                    if (deltaMp > 0f && PlayerModel.CurrentMp < PlayerModel.Stats.MaxMp)
                        ApplyManaWithCombatNumber(deltaMp);
                }
            }
            else
            {
                _manaPotionTickAccumulator = 0f;
            }
        }

        public bool ToggleAttackMode()
        {
            return ToggleAttackMode(out _);
        }

        public bool ToggleAttackMode(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!_initialized || _deathSequenceStarted || PlayerModel == null)
                return false;

            if (IsAimModeActive)
                SetAimModeInternal(false, true);

            if (!CanToggleAttackModeInCurrentState())
            {
                errorMessage = "当前状态下无法切换形态";
                return false;
            }

            bool changed = PlayerModel.ToggleAttackMode();
            if (!changed)
                return false;

            EventCenter.GetInstance()?.EventTrigger(GameEvents.InventoryChanged);
            return true;
        }

        private bool CanToggleAttackModeInCurrentState()
        {
            PlayerStateBase currentState = StateMachine?.CurrentState;
            return currentState == null
                   || currentState is IdleState
                   || currentState is MoveState
                   || currentState is AimState
                   || currentState is JumpState
                   || currentState is FallState
                   || currentState is LandState;
        }

        public bool ToggleAimMode()
        {
            return SetAimModeInternal(!IsAimModeActive, false);
        }

        private bool SetAimModeInternal(bool active, bool forceStateRefresh)
        {
            if (!_initialized || _deathSequenceStarted || PlayerModel == null || StateMachine == null)
                return false;
            if (active && GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return false;

            bool canActivate = active
                               && PlayerModel.CurrentAttackMode == PlayerAttackMode.Ranged
                               && CanToggleAimModeInCurrentState();

            bool targetActive = canActivate;
            bool changed = _isAimModeActive != targetActive;
            _isAimModeActive = targetActive;

            if (_isAimModeActive)
            {
                if (StateMachine.CurrentState is not AimState)
                    StateMachine.ChangeState<AimState>();
            }
            else if (forceStateRefresh || changed)
            {
                if (StateMachine.CurrentState is AimState)
                    ExitAimStateToLocomotion();
            }

            RefreshAimPresentation();
            return changed || forceStateRefresh;
        }

        private bool CanToggleAimModeInCurrentState()
        {
            PlayerStateBase currentState = StateMachine?.CurrentState;
            return currentState == null
                   || currentState is IdleState
                   || currentState is MoveState
                   || currentState is AimState;
        }

        private void ExitAimStateToLocomotion()
        {
            if (StateMachine == null)
                return;

            if (Mover != null && !Mover.IsGrounded)
            {
                StateMachine.ChangeState<FallState>();
                return;
            }

            Vector2 moveInput = _inputHandler != null ? _inputHandler.CurrentInput.MoveInput : Vector2.zero;
            bool isRunning = _inputHandler != null && _inputHandler.CurrentInput.IsRunRequested;
            if (moveInput.sqrMagnitude > 0.01f)
            {
                StateMachine.ChangeState<MoveState>(state => state.InitialIsRunning = isRunning);
                return;
            }

            StateMachine.ChangeState<IdleState>();
        }

        private void OnPlayerLoadoutChanged()
        {
            RefreshWeaponVisuals();
            RefreshAimPresentation();
        }

        private void RefreshWeaponVisuals()
        {
            if (PlayerModel == null)
                return;

            if (!HasWeaponVisualObjects())
                return;

            PlayerWeaponVisualUtility.ApplyCurrentLoadout(transform, PlayerModel);
            _aimGuideOrigin = null;
        }

        private bool HasWeaponVisualObjects()
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            bool hasMount = false;
            bool hasWeapon = false;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null)
                    continue;

                if (current.name == PlayerWeaponVisualUtility.WeaponMountName)
                    hasMount = true;
                else if (current.name == PlayerWeaponVisualUtility.MeleeWeaponName
                         || current.name == PlayerWeaponVisualUtility.RangedWeaponName)
                    hasWeapon = true;

                if (hasMount && hasWeapon)
                {
                    _loggedMissingWeaponVisualWarning = false;
                    return true;
                }
            }

            if (!_loggedMissingWeaponVisualWarning)
            {
                Debug.LogWarning("[PlayerController] 未找到手持武器挂点或剑/枪模型，无法根据形态刷新武器显示。", this);
                _loggedMissingWeaponVisualWarning = true;
            }

            return false;
        }

        private void TryUsePotion(string itemId)
        {
            if (!_initialized || _deathSequenceStarted || PlayerModel == null || string.IsNullOrWhiteSpace(itemId))
                return;

            PotionConfigSO potionConfig = ConfigManager.GetInstance()?.GetPotionConfig();
            if (potionConfig == null)
                return;

            if (!PlayerModel.TryConsumeItem(itemId, 1))
                return;

            float duration = Mathf.Max(0.1f, potionConfig.durationSeconds);
            if (string.Equals(itemId, PlayerModel.ItemIds.PotionHp, StringComparison.Ordinal))
            {
                _healthPotionRemainingTime += duration;
                return;
            }

            if (string.Equals(itemId, PlayerModel.ItemIds.PotionMp, StringComparison.Ordinal))
                _manaPotionRemainingTime += duration;
        }

        private void ApplyHealWithCombatNumber(float amount)
        {
            if (PlayerModel == null || amount <= 0f)
                return;

            float before = PlayerModel.CurrentHp;
            PlayerModel.Heal(amount);
            float applied = PlayerModel.CurrentHp - before;
            if (applied > 0f)
                CombatNumberDispatcher.PublishHeal(transform, applied);
        }

        private void ApplyManaWithCombatNumber(float amount)
        {
            if (PlayerModel == null || amount <= 0f)
                return;

            float before = PlayerModel.CurrentMp;
            PlayerModel.CurrentMp = Mathf.Min(PlayerModel.Stats.MaxMp, PlayerModel.CurrentMp + amount);
            float applied = PlayerModel.CurrentMp - before;
            if (applied > 0f)
                CombatNumberDispatcher.PublishMana(transform, applied);
        }

        private void RefreshAimPresentation()
        {
            PlayerStateBase currentState = StateMachine?.CurrentState;
            bool isAimVisualState = (IsAimModeActive
                                     && (currentState is AimState
                                         || currentState is IdleState
                                         || currentState is MoveState
                                         || currentState is AttackStateBase))
                                    || (PlayerModel != null
                                        && PlayerModel.CurrentAttackMode == PlayerAttackMode.Ranged
                                        && currentState is ChargeLoopState);
            bool shouldShowCrosshair = isAimVisualState
                                       && GameStateMachine.GetInstance()?.IsGameplayPaused != true;
            PlayerAimCrosshairRuntime.SetVisible(shouldShowCrosshair);
            RefreshAimGuide(shouldShowCrosshair);
        }

        private void RefreshAimGuide(bool visible)
        {
            if (!visible || !TryGetAimGuideSegment(out Vector3 start, out Vector3 end))
            {
                PlayerAimCrosshairRuntime.SetAimGuide(Vector3.zero, Vector3.zero, false);
                return;
            }

            PlayerAimCrosshairRuntime.SetAimGuide(start, end, true);
        }

        private bool TryGetAimGuideSegment(out Vector3 start, out Vector3 end)
        {
            start = Vector3.zero;
            end = Vector3.zero;

            Camera gameplayCamera = ResolveGameplayCamera();
            if (gameplayCamera == null)
                return false;

            start = ResolveAimGuideOrigin();
            Ray aimRay = gameplayCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 targetPoint = aimRay.origin + aimRay.direction * 100f;

            RaycastHit[] hits = Physics.RaycastAll(aimRay, 100f, ~0, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider hitCollider = hits[i].collider;
                    if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                        continue;

                    targetPoint = hits[i].point;
                    break;
                }
            }

            if ((targetPoint - start).sqrMagnitude <= 0.0001f)
                return false;

            end = targetPoint;
            return true;
        }

        private Vector3 ResolveAimGuideOrigin()
        {
            Transform origin = ResolveAimGuideOriginTransform();
            if (origin != null)
                return origin.position;

            return transform.position + Vector3.up * 1.2f + transform.forward * 0.2f;
        }

        private Transform ResolveAimGuideOriginTransform()
        {
            if (_aimGuideOrigin != null)
                return _aimGuideOrigin;

            Transform weaponMount = FindChildTransformByName(transform, PlayerWeaponVisualUtility.WeaponMountName);
            if (weaponMount == null)
                return null;

            Transform rangedWeapon = FindChildTransformByName(weaponMount, PlayerWeaponVisualUtility.RangedWeaponName);
            _aimGuideOrigin = rangedWeapon != null ? rangedWeapon : weaponMount;
            return _aimGuideOrigin;
        }

        private Camera ResolveGameplayCamera()
        {
            ResolveCameraReference();

            Camera gameplayCamera = _camera != null ? _camera.GetComponent<Camera>() : null;
            if (gameplayCamera != null && gameplayCamera.isActiveAndEnabled)
                return gameplayCamera;

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;

            return null;
        }

        private static Transform FindChildTransformByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current != null && current.name == targetName)
                    return current;
            }

            return null;
        }
    }
}
