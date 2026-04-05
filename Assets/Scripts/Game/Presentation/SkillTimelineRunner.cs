using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 技能时间轴运行器：接受 <see cref="SharedSkillDefinition"/> 并逐帧推进，
    /// 在合适的时间点调用 <see cref="SkillEffectExecutor"/> 中对应的五类事件执行入口。
    /// 玩家状态机和敌人战斗组件均可持有此实例。
    ///
    /// 这里的“时间轴”本质上是技能/动作效果时间线，不是动画状态机本身。
    /// 它负责伤害检测、物理效果、属性效果、特效和音效这些运行时效果，
    /// 允许在动作或技能动画已经自然退出后继续独立推进。
    /// 因此调用方不应默认用它来决定动画是否切回 Locomotion，也不应默认用它来阻塞状态切换。
    /// </summary>
    public sealed class SkillTimelineRunner
    {
        // ── 运行时状态 ────────────────────────────────────────────────────────

        [Serializable]
        private sealed class EventState
        {
            public bool hasTriggered;
            public float nextTriggerTime;
            public bool hasFixedDamageAnchor;
            public UnityEngine.Vector3 fixedDamageAnchorPosition;
            public UnityEngine.Quaternion fixedDamageAnchorRotation = UnityEngine.Quaternion.identity;
        }

        [Serializable]
        private sealed class ActiveDamageWindow
        {
            public SkillDamageEffect effect;
            public float endTime;
            public SkillCollisionHitbox collisionHitbox;
            public UnityEngine.GameObject companionVfxRoot;
            public float startTime;
            public UnityEngine.Vector3 originPosition;
            public UnityEngine.Quaternion originRotation;
            public UnityEngine.Transform casterTransform;
            public bool hasExecutedOneShotDetection;
            public bool wasDetectionActive;
            public int lastActivationCycleIndex = -1;
            public readonly HashSet<UnityEngine.Transform> hitTargets = new HashSet<UnityEngine.Transform>();
        }

        [Serializable]
        private sealed class CameraEventState
        {
            public bool wasActive;
            public bool snapshotCaptured;
            public UnityEngine.Vector3 snapshotPosition;
            public UnityEngine.Quaternion snapshotRotation = UnityEngine.Quaternion.identity;
        }

        public struct CameraOverrideRequest
        {
            public UnityEngine.Vector3 DesiredPosition;
            public UnityEngine.Vector3 LookAtPosition;
            public float BlendWeight;
            public bool LockLookInput;

            public CameraOverrideRequest(
                UnityEngine.Vector3 desiredPosition,
                UnityEngine.Vector3 lookAtPosition,
                float blendWeight,
                bool lockLookInput)
            {
                DesiredPosition = desiredPosition;
                LookAtPosition = lookAtPosition;
                BlendWeight = blendWeight;
                LockLookInput = lockLookInput;
            }

            public bool IsActive => BlendWeight > 0.0001f;
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
        private CameraEventState[] _cameraStates;
        private readonly List<ActiveDamageWindow> _activeDamageWindows = new List<ActiveDamageWindow>();
        private SkillCueRuntimeScope _cueRuntime = new SkillCueRuntimeScope();
        private bool _started;
        private bool _stateScopeEnded;
        private float _dynamicCompletionTime;
        private float _baseCastSpeedMultiplier = 1f;
        private Func<float> _externalCastSpeedMultiplierProvider;

        // ── 公开属性 ──────────────────────────────────────────────────────────

        public bool IsRunning => _started && !IsComplete;
        public bool IsComplete { get; private set; }
        public bool HasActiveDamageWindows => _activeDamageWindows.Count > 0;
        public bool HasPendingWork => _started && !IsComplete;
        public float CurrentCastSpeedMultiplier => ResolveEffectiveCastSpeedMultiplier(_elapsed);
        public float CurrentMovementSpeedMultiplier => ResolveMovementSpeedMultiplier(_elapsed);

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
                eventsDuration = UnityEngine.Mathf.Max(eventsDuration, _dynamicCompletionTime);
                bool hasUntilStateExitEvents =
                    !_stateScopeEnded
                    && _definition != null
                    && _definition.HasUntilStateExitEvents();
                if (hasUntilStateExitEvents && _castDuration <= 0f)
                    return float.PositiveInfinity;
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
        /// AI/状态层可选的“动作占用时长”提示。> 0 时，Duration = max(castDuration, 时间轴事件总时长)；
        /// 但这只是时间轴自身的持续时间参考，不代表动画或状态必须等到 Duration 结束后才能退出。
        /// 玩家状态机可传入 -1（忽略此参数）。
        /// </param>
        public void Begin(
            SharedSkillDefinition definition,
            ISkillExecutionContext context,
            float castDuration = -1f,
            float baseCastSpeedMultiplier = 1f,
            Func<float> externalCastSpeedMultiplierProvider = null)
        {
            _definition   = definition;
            _context      = context;
            _castDuration = castDuration;
            _baseCastSpeedMultiplier = UnityEngine.Mathf.Max(0.01f, baseCastSpeedMultiplier);
            _externalCastSpeedMultiplierProvider = externalCastSpeedMultiplierProvider;
            _elapsed      = 0f;
            _started      = true;
            _stateScopeEnded = false;
            _dynamicCompletionTime = 0f;
            IsComplete    = false;
            _cueRuntime.Stop();
            _cueRuntime = new SkillCueRuntimeScope();

            _damageStates = BuildStates(_definition?.damageEvents);
            _physicsStates = BuildStates(_definition?.physicsEvents);
            _attributeStates = BuildStates(_definition?.attributeEvents);
            _vfxStates = BuildStates(_definition?.vfxEvents);
            _sfxStates = BuildStates(_definition?.sfxEvents);
            _cameraStates = BuildCameraStates(_definition?.cameraEvents);
            ClearActiveDamageWindows();

            // 立即评估 t=0 的事件
            EvaluateEvents();
            UpdateCameraStates();
        }

        /// <summary>
        /// 每帧推进时间轴并触发到期事件。
        /// 播放完毕后 <see cref="IsComplete"/> 变为 true，调用方可据此结束技能。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_started || IsComplete || _definition == null) return;

            _elapsed += UnityEngine.Mathf.Max(0f, deltaTime) * ResolveEffectiveCastSpeedMultiplier(_elapsed);
            UpdateCameraStates();
            EvaluateEvents();
            RefreshCompletion();
        }

        /// <summary>提前终止时间轴（不触发剩余事件）。</summary>
        public void Stop()
        {
            _started = false;
            IsComplete = true;
            ClearActiveDamageWindows();
            _cueRuntime.Stop();
            _externalCastSpeedMultiplierProvider = null;
            _cameraStates = null;
        }

        /// <summary>
        /// 停止与当前动作状态强绑定的 Cue，但保留其余效果时间线继续运行。
        /// 典型场景是：动作动画已经自然退出，状态机已切回 Locomotion，
        /// 但拖尾伤害、残留特效或持续检测窗口仍需继续结算。
        /// </summary>
        public void StopStateScopedCues()
        {
            _stateScopeEnded = true;
            _cueRuntime.Stop();
            UpdateCameraStates();
            RefreshCompletion();
        }

        public bool TryGetCurrentCameraOverride(float lookTargetHeight, out CameraOverrideRequest request)
        {
            request = default;
            if (!_started || _definition?.cameraEvents == null || _cameraStates == null)
                return false;

            UpdateCameraStates();

            int count = UnityEngine.Mathf.Min(_definition.cameraEvents.Count, _cameraStates.Length);
            for (int i = count - 1; i >= 0; i--)
            {
                SkillCameraEvent evt = _definition.cameraEvents[i];
                CameraEventState state = _cameraStates[i];
                if (evt == null || state == null || !state.snapshotCaptured || !evt.IsActive(_elapsed, _stateScopeEnded))
                    continue;

                float blendWeight = EvaluateCameraBlendWeight(evt, _elapsed);
                if (blendWeight <= 0.0001f)
                    continue;

                request = new CameraOverrideRequest(
                    EvaluateCameraWorldPosition(evt, state, _elapsed),
                    ResolveCameraLookAtPosition(lookTargetHeight, state),
                    blendWeight,
                    evt.lockLookInput);
                return request.IsActive;
            }

            return false;
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
                        TriggerDamageEvent(evt, state);
                        state.hasTriggered = true;
                    }
                    continue;
                }

                float interval = UnityEngine.Mathf.Max(0.01f, evt.repeatInterval);
                bool repeatsUntilStateExit = evt.RepeatsUntilStateExit && !_stateScopeEnded;
                float endTime = evt.GetEndTime();
                while (_elapsed >= state.nextTriggerTime
                       && (repeatsUntilStateExit || state.nextTriggerTime <= endTime + 0.0001f))
                {
                    TriggerDamageEvent(evt, state);
                    state.hasTriggered = true;
                    state.nextTriggerTime += interval;
                }
            }
        }

        private void TriggerDamageEvent(SkillDamageEvent evt, EventState state)
        {
            if (evt?.damageEffects == null || _context == null)
                return;

            for (int i = 0; i < evt.damageEffects.Count; i++)
            {
                SkillDamageEffect effect = evt.damageEffects[i];
                if (effect == null)
                    continue;

                ResolveDamageAnchorSnapshot(evt, state, effect, out UnityEngine.Vector3 anchorPosition, out UnityEngine.Quaternion anchorRotation);

                if (effect.detectionType == DamageDetectionType.Collision)
                {
                    float detectionDuration = effect.detectionDuration > 0f
                        ? effect.detectionDuration
                        : UnityEngine.Mathf.Max(0.0001f, UnityEngine.Time.fixedDeltaTime);
                    UpdateDynamicCompletionTime(_elapsed + detectionDuration + SharedSkillDefinition.GetDamageOnHitTailDuration(effect));
                    var collisionWindow = new ActiveDamageWindow
                    {
                        effect = effect,
                        endTime = _elapsed + detectionDuration,
                        startTime = _elapsed,
                        originPosition = anchorPosition,
                        originRotation = anchorRotation,
                        casterTransform = _context.CasterTransform,
                        wasDetectionActive = true,
                        lastActivationCycleIndex = 0,
                    };
                    collisionWindow.collisionHitbox = DamageDetectionRunner.BeginCollisionWindow(_context.CasterTransform, effect, collisionWindow);
                    if (collisionWindow.collisionHitbox != null)
                        _activeDamageWindows.Add(collisionWindow);
                    continue;
                }

                float detectionLifetime = SharedSkillDefinition.GetDamageDetectionWindowLifetime(effect);
                UpdateDynamicCompletionTime(_elapsed + SharedSkillDefinition.GetDamageEffectLifetime(effect));
                if (effect.detectionDuration <= 0f && detectionLifetime <= 0f)
                {
                    SkillEffectExecutor.ExecuteDamageEffect(
                        effect,
                        _context,
                        null,
                        null,
                        null,
                        _cueRuntime,
                        CreateMotionFrame(anchorPosition, anchorRotation, 0f));
                    continue;
                }

                bool initialDetectionActive = IsDamageWindowDetectionActive(effect, 0f);
                var detectionWindow = new ActiveDamageWindow
                {
                    effect = effect,
                    endTime = _elapsed + detectionLifetime,
                    startTime = _elapsed,
                    originPosition = anchorPosition,
                    originRotation = anchorRotation,
                    casterTransform = _context.CasterTransform,
                    wasDetectionActive = initialDetectionActive,
                    lastActivationCycleIndex = ResolveDamageActivationCycleIndex(effect, 0f, initialDetectionActive),
                };
                CreateDamageWindowCompanionVfx(detectionWindow);
                _activeDamageWindows.Add(detectionWindow);
                if (ShouldExecuteDamageWindow(detectionWindow, 0f, detectionWindow.wasDetectionActive))
                {
                    SkillEffectExecutor.ExecuteDamageEffect(
                        effect,
                        _context,
                        detectionWindow.hitTargets,
                        null,
                        null,
                        _cueRuntime,
                        CreateMotionFrame(detectionWindow, 0f));

                    if (IsOneShotDamageWindow(detectionWindow))
                    {
                        detectionWindow.hasExecutedOneShotDetection = true;
                        DestroyDamageWindowCompanionVfx(detectionWindow);
                        _activeDamageWindows.Remove(detectionWindow);
                    }
                }
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
                    DestroyDamageWindowCompanionVfx(window);
                    _activeDamageWindows.RemoveAt(i);
                    continue;
                }

                float windowElapsed = _elapsed - window.startTime;
                UpdateDamageWindowCompanionVfx(window, windowElapsed);
                bool isDetectionActive = IsDamageWindowDetectionActive(window.effect, windowElapsed);
                ApplyDamageWindowHitDeduplication(window, windowElapsed, isDetectionActive);
                window.wasDetectionActive = isDetectionActive;
                if (ShouldExecuteDamageWindow(window, windowElapsed, isDetectionActive))
                {
                    SkillEffectExecutor.ExecuteDamageEffect(
                        window.effect,
                        _context,
                        window.hitTargets,
                        window.collisionHitbox,
                        window,
                        _cueRuntime,
                        CreateMotionFrame(window, windowElapsed));

                    if (IsOneShotDamageWindow(window))
                    {
                        window.hasExecutedOneShotDetection = true;
                        CloseCollisionWindow(window);
                        DestroyDamageWindowCompanionVfx(window);
                        _activeDamageWindows.RemoveAt(i);
                        continue;
                    }
                }

                if (_elapsed > window.endTime + 0.0001f)
                {
                    CloseCollisionWindow(window);
                    DestroyDamageWindowCompanionVfx(window);
                    _activeDamageWindows.RemoveAt(i);
                }
            }
        }

        private void ClearActiveDamageWindows()
        {
            for (int i = _activeDamageWindows.Count - 1; i >= 0; i--)
            {
                CloseCollisionWindow(_activeDamageWindows[i]);
                DestroyDamageWindowCompanionVfx(_activeDamageWindows[i]);
            }

            _activeDamageWindows.Clear();
        }

        private static CameraEventState[] BuildCameraStates(List<SkillCameraEvent> events)
        {
            if (events == null || events.Count == 0)
                return null;

            var states = new CameraEventState[events.Count];
            for (int i = 0; i < events.Count; i++)
                states[i] = new CameraEventState();
            return states;
        }

        private void UpdateCameraStates()
        {
            if (_definition?.cameraEvents == null || _cameraStates == null)
                return;

            int count = UnityEngine.Mathf.Min(_definition.cameraEvents.Count, _cameraStates.Length);
            for (int i = 0; i < count; i++)
            {
                SkillCameraEvent evt = _definition.cameraEvents[i];
                CameraEventState state = _cameraStates[i];
                if (evt == null || state == null)
                    continue;

                bool isActive = evt.IsActive(_elapsed, _stateScopeEnded);
                if (isActive && !state.wasActive)
                    CaptureCameraSnapshot(state);

                state.wasActive = isActive;
            }
        }

        private void CaptureCameraSnapshot(CameraEventState state)
        {
            if (state == null)
                return;

            UnityEngine.Transform caster = _context?.CasterTransform;
            if (caster == null)
            {
                state.snapshotPosition = UnityEngine.Vector3.zero;
                state.snapshotRotation = UnityEngine.Quaternion.identity;
                state.snapshotCaptured = true;
                return;
            }

            state.snapshotPosition = caster.position;
            state.snapshotRotation = UnityEngine.Quaternion.Euler(0f, caster.eulerAngles.y, 0f);
            state.snapshotCaptured = true;
        }

        private static float EvaluateCameraBlendWeight(SkillCameraEvent evt, float timelineTime)
        {
            if (evt == null)
                return 0f;

            float weight = 1f;
            if (evt.blendInDuration > 0f)
                weight = UnityEngine.Mathf.Min(weight, UnityEngine.Mathf.Clamp01((timelineTime - evt.startTime) / evt.blendInDuration));

            if (evt.durationMode == SkillEffectDurationMode.FixedTime && evt.blendOutDuration > 0f)
            {
                float endTime = evt.startTime + UnityEngine.Mathf.Max(0f, evt.duration);
                weight = UnityEngine.Mathf.Min(weight, UnityEngine.Mathf.Clamp01((endTime - timelineTime) / evt.blendOutDuration));
            }

            return UnityEngine.Mathf.Clamp01(weight);
        }

        private UnityEngine.Vector3 EvaluateCameraWorldPosition(SkillCameraEvent evt, CameraEventState state, float timelineTime)
        {
            UnityEngine.Vector3 localPosition = evt.positionMode == SkillCameraPositionMode.BezierPath
                ? EvaluateCameraBezierLocalPosition(evt, timelineTime)
                : evt.holdLocalOffset;

            return state.snapshotPosition + state.snapshotRotation * localPosition;
        }

        private static UnityEngine.Vector3 EvaluateCameraBezierLocalPosition(SkillCameraEvent evt, float timelineTime)
        {
            if (evt == null)
                return UnityEngine.Vector3.zero;

            float normalizedTime = evt.durationMode == SkillEffectDurationMode.FixedTime && evt.duration > 0f
                ? UnityEngine.Mathf.Clamp01((timelineTime - evt.startTime) / evt.duration)
                : 0f;

            float progress = EvaluateProgressCurve(evt.pathProgressCurve, normalizedTime);
            return EvaluateCubicBezier(
                evt.pathStartLocalOffset,
                evt.pathControlPointA,
                evt.pathControlPointB,
                evt.pathEndLocalOffset,
                progress);
        }

        private UnityEngine.Vector3 ResolveCameraLookAtPosition(float lookTargetHeight, CameraEventState state)
        {
            UnityEngine.Transform caster = _context?.CasterTransform;
            UnityEngine.Vector3 basePosition = caster != null ? caster.position : state.snapshotPosition;
            return basePosition + UnityEngine.Vector3.up * lookTargetHeight;
        }

        private static float EvaluateProgressCurve(UnityEngine.AnimationCurve curve, float normalizedTime)
        {
            if (curve == null || curve.length == 0)
                return normalizedTime;

            return UnityEngine.Mathf.Clamp01(curve.Evaluate(normalizedTime));
        }

        private static UnityEngine.Vector3 EvaluateCubicBezier(
            UnityEngine.Vector3 p0,
            UnityEngine.Vector3 p1,
            UnityEngine.Vector3 p2,
            UnityEngine.Vector3 p3,
            float t)
        {
            float clampedT = UnityEngine.Mathf.Clamp01(t);
            float oneMinusT = 1f - clampedT;
            return oneMinusT * oneMinusT * oneMinusT * p0
                   + 3f * oneMinusT * oneMinusT * clampedT * p1
                   + 3f * oneMinusT * clampedT * clampedT * p2
                   + clampedT * clampedT * clampedT * p3;
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

        private static SkillDetectionMotionFrame CreateMotionFrame(UnityEngine.Vector3 originPosition, UnityEngine.Quaternion originRotation, float elapsed)
        {
            return new SkillDetectionMotionFrame(originPosition, originRotation, elapsed);
        }

        private static SkillDetectionMotionFrame? CreateMotionFrame(UnityEngine.Transform caster, float elapsed)
        {
            if (caster == null)
                return null;

            return new SkillDetectionMotionFrame(caster.position, caster.rotation, elapsed);
        }

        private static SkillDetectionMotionFrame CreateMotionFrame(ActiveDamageWindow window, float elapsed)
        {
            if (window?.effect != null
                && window.effect.anchor == SkillDamageAnchor.Self
                && window.casterTransform != null)
            {
                return CreateMotionFrame(window.casterTransform.position, window.casterTransform.rotation, elapsed);
            }

            return new SkillDetectionMotionFrame(window.originPosition, window.originRotation, elapsed);
        }

        private static void ApplyDamageWindowHitDeduplication(ActiveDamageWindow window, float elapsed, bool isDetectionActive)
        {
            if (window?.effect == null || !isDetectionActive)
                return;

            switch (window.effect.hitDeduplicationScope)
            {
                case SkillDamageHitDeduplicationScope.PerActivation:
                    int currentCycleIndex = ResolveDamageActivationCycleIndex(window.effect, elapsed, isDetectionActive);
                    if (currentCycleIndex != window.lastActivationCycleIndex)
                    {
                        window.hitTargets.Clear();
                        window.lastActivationCycleIndex = currentCycleIndex;
                    }
                    break;
                case SkillDamageHitDeduplicationScope.PerDetection:
                    window.hitTargets.Clear();
                    break;
            }
        }

        private static int ResolveDamageActivationCycleIndex(SkillDamageEffect effect, float elapsed, bool isDetectionActive)
        {
            if (effect == null || !isDetectionActive)
                return -1;

            if (effect.detectionType == DamageDetectionType.Collision || !SharedSkillDefinition.UsesDamageActivationScheduling(effect))
                return 0;

            float resolvedElapsed = UnityEngine.Mathf.Max(0f, elapsed);
            float activationTime = UnityEngine.Mathf.Max(0f, effect.activationTime);
            if (resolvedElapsed < activationTime)
                return -1;

            if (effect.activationMode == SkillDamageActivationMode.Continuous)
                return 0;

            float activeDuration = UnityEngine.Mathf.Max(0.01f, effect.intermittentActiveDuration);
            float intervalDuration = UnityEngine.Mathf.Max(0f, effect.intermittentIntervalDuration);
            float cycleDuration = activeDuration + intervalDuration;
            if (cycleDuration <= 0.0001f)
                return 0;

            float cycleElapsed = resolvedElapsed - activationTime;
            return UnityEngine.Mathf.Max(0, UnityEngine.Mathf.FloorToInt(cycleElapsed / cycleDuration));
        }

        private static bool IsDamageWindowDetectionActive(SkillDamageEffect effect, float elapsed)
        {
            if (effect == null)
                return false;

            if (effect.detectionType == DamageDetectionType.Collision)
                return true;

            if (!SharedSkillDefinition.UsesDamageActivationScheduling(effect))
                return true;

            return SharedSkillDefinition.IsDamageDetectionActiveAt(effect, elapsed);
        }

        private static bool ShouldExecuteDamageWindow(ActiveDamageWindow window, float elapsed, bool isDetectionActive)
        {
            if (window?.effect == null)
                return false;

            if (!isDetectionActive)
                return false;

            if (IsOneShotDamageWindow(window) && window.hasExecutedOneShotDetection)
                return false;

            return true;
        }

        private static bool IsOneShotDamageWindow(ActiveDamageWindow window)
        {
            return window?.effect != null
                && SharedSkillDefinition.UsesDamageActivationScheduling(window.effect)
                && window.effect.detectionDuration <= 0f;
        }

        private void CreateDamageWindowCompanionVfx(ActiveDamageWindow window)
        {
            if (!ShouldCreateDamageWindowCompanionVfx(window))
                return;

            List<SkillVfxEffect> companionVfxEffects = window.effect.companionVfxEffects;
            string rootName = "[SkillDamageCompanion]";
            for (int i = 0; i < companionVfxEffects.Count; i++)
            {
                SkillVfxEffect effect = companionVfxEffects[i];
                if (effect?.particlePrefab != null)
                {
                    rootName = $"[SkillDamageCompanion]{effect.particlePrefab.name}";
                    break;
                }
            }

            var root = new UnityEngine.GameObject(rootName);
            root.AddComponent<SkillCuePauseProxy>();

            float rangeScale = PlayerBuffRuntimeUtility.GetDamageRangeScale(_context.CasterTransform);
            for (int i = 0; i < companionVfxEffects.Count; i++)
            {
                SkillVfxEffect companionVfx = companionVfxEffects[i];
                if (companionVfx?.particlePrefab == null)
                    continue;

                UnityEngine.GameObject instance = UnityEngine.Object.Instantiate(companionVfx.particlePrefab, root.transform);
                instance.transform.localPosition = companionVfx.offset;
                instance.transform.localRotation = UnityEngine.Quaternion.Euler(companionVfx.rotationEuler);
                instance.transform.localScale = UnityEngine.Vector3.Scale(instance.transform.localScale, companionVfx.scale * rangeScale);
                ConfigureDamageWindowCompanionParticles(instance);
            }

            window.companionVfxRoot = root;
            UpdateDamageWindowCompanionVfx(window, 0f);
        }

        private void UpdateDamageWindowCompanionVfx(ActiveDamageWindow window, float elapsed)
        {
            if (window?.companionVfxRoot == null)
                return;

            if (!TryEvaluateDamageWindowTransform(window, elapsed, out UnityEngine.Vector3 position, out UnityEngine.Quaternion rotation))
                return;

            window.companionVfxRoot.transform.SetPositionAndRotation(position, rotation);
        }

        private static void DestroyDamageWindowCompanionVfx(ActiveDamageWindow window)
        {
            if (window?.companionVfxRoot == null)
                return;

            UnityEngine.Object.Destroy(window.companionVfxRoot);
            window.companionVfxRoot = null;
        }

        private bool ShouldCreateDamageWindowCompanionVfx(ActiveDamageWindow window)
        {
            if (window?.effect == null
                || window.effect.detectionType == DamageDetectionType.Collision
                || _context?.CasterTransform == null)
            {
                return false;
            }

            List<SkillVfxEffect> companionVfxEffects = window.effect.companionVfxEffects;
            if (companionVfxEffects == null)
                return false;

            for (int i = 0; i < companionVfxEffects.Count; i++)
            {
                if (companionVfxEffects[i]?.particlePrefab != null)
                    return true;
            }

            return false;
        }

        private static void ConfigureDamageWindowCompanionParticles(UnityEngine.GameObject instance)
        {
            if (instance == null)
                return;

            UnityEngine.ParticleSystem[] particleSystems = instance.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                UnityEngine.ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                UnityEngine.ParticleSystem.MainModule main = particleSystem.main;
                if (main.simulationSpace == UnityEngine.ParticleSystemSimulationSpace.World)
                    main.simulationSpace = UnityEngine.ParticleSystemSimulationSpace.Local;
            }
        }

        private static bool TryEvaluateDamageWindowTransform(
            ActiveDamageWindow window,
            float elapsed,
            out UnityEngine.Vector3 position,
            out UnityEngine.Quaternion rotation)
        {
            position = UnityEngine.Vector3.zero;
            rotation = UnityEngine.Quaternion.identity;
            if (window?.effect == null)
                return false;

            SkillDamageEffect effect = window.effect;
            SkillDetectionMotionFrame motionFrame = CreateMotionFrame(window, elapsed);
            bool useWorldMotion = effect.anchor == SkillDamageAnchor.World
                && effect.motion != null
                && effect.motion.IsActive;
            UnityEngine.Vector3 motionOffset = useWorldMotion
                ? effect.motion.EvaluateLocalDisplacement(motionFrame.Elapsed)
                : UnityEngine.Vector3.zero;

            switch (effect.detectionType)
            {
                case DamageDetectionType.RangeOverlap:
                    position = motionFrame.OriginPosition + motionFrame.OriginRotation * (effect.centerOffset + motionOffset);
                    rotation = motionFrame.OriginRotation * UnityEngine.Quaternion.Euler(effect.rotationEuler);
                    return true;
                case DamageDetectionType.Raycast:
                    position = motionFrame.OriginPosition + motionFrame.OriginRotation * (effect.rayOriginOffset + motionOffset);
                    rotation = motionFrame.OriginRotation * UnityEngine.Quaternion.Euler(effect.rotationEuler);
                    return true;
                default:
                    return false;
            }
        }

        private void ResolveDamageAnchorSnapshot(
            SkillDamageEvent evt,
            EventState state,
            SkillDamageEffect effect,
            out UnityEngine.Vector3 anchorPosition,
            out UnityEngine.Quaternion anchorRotation)
        {
            UnityEngine.Transform caster = _context?.CasterTransform;
            UnityEngine.Vector3 currentPosition = caster != null ? caster.position : UnityEngine.Vector3.zero;
            UnityEngine.Quaternion currentRotation = caster != null ? caster.rotation : UnityEngine.Quaternion.identity;

            if (effect == null || effect.anchor != SkillDamageAnchor.World)
            {
                anchorPosition = currentPosition;
                anchorRotation = currentRotation;
                return;
            }

            if (evt != null
                && evt.triggerMode == SkillEventTriggerMode.Repeated
                && evt.repeatedAnchorMode == SkillDamageRepeatedAnchorMode.Fixed)
            {
                if (state != null && !state.hasFixedDamageAnchor)
                {
                    state.fixedDamageAnchorPosition = currentPosition;
                    state.fixedDamageAnchorRotation = currentRotation;
                    state.hasFixedDamageAnchor = true;
                }

                if (state != null && state.hasFixedDamageAnchor)
                {
                    anchorPosition = state.fixedDamageAnchorPosition;
                    anchorRotation = state.fixedDamageAnchorRotation;
                    return;
                }
            }

            anchorPosition = currentPosition;
            anchorRotation = currentRotation;
        }

        private void EvaluateTrack<T>(List<T> events, EventState[] states, Action<T, ISkillExecutionContext, SkillCueRuntimeScope> executor)
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
                        executor(evt, _context, _cueRuntime);
                        state.hasTriggered = true;
                    }
                    continue;
                }

                float interval = UnityEngine.Mathf.Max(0.01f, evt.repeatInterval);
                bool repeatsUntilStateExit = evt.RepeatsUntilStateExit && !_stateScopeEnded;
                float endTime = evt.GetEndTime();
                while (_elapsed >= state.nextTriggerTime
                       && (repeatsUntilStateExit || state.nextTriggerTime <= endTime + 0.0001f))
                {
                    UpdateDynamicCompletionTime(state.nextTriggerTime + GetEventOccurrenceLifetime(evt));
                    executor(evt, _context, _cueRuntime);
                    state.hasTriggered = true;
                    state.nextTriggerTime += interval;
                }
            }
        }

        private void RefreshCompletion()
        {
            if (!_started || _definition == null)
            {
                IsComplete = true;
                return;
            }

            if (float.IsPositiveInfinity(Duration))
            {
                IsComplete = false;
                return;
            }

            IsComplete = _elapsed >= Duration && _activeDamageWindows.Count <= 0;
        }

        private void UpdateDynamicCompletionTime(float endTime)
        {
            _dynamicCompletionTime = UnityEngine.Mathf.Max(_dynamicCompletionTime, endTime);
        }

        private float ResolveEffectiveCastSpeedMultiplier(float timelineTime)
        {
            float definitionMultiplier = _definition != null
                ? _definition.EvaluateCastSpeedMultiplier(timelineTime, _stateScopeEnded)
                : 1f;
            float externalMultiplier = _externalCastSpeedMultiplierProvider != null
                ? UnityEngine.Mathf.Max(0f, _externalCastSpeedMultiplierProvider.Invoke())
                : 1f;
            return UnityEngine.Mathf.Max(0f, definitionMultiplier * _baseCastSpeedMultiplier * externalMultiplier);
        }

        private float ResolveMovementSpeedMultiplier(float timelineTime)
        {
            return _definition != null
                ? UnityEngine.Mathf.Max(0f, _definition.EvaluateMovementSpeedMultiplier(timelineTime, _stateScopeEnded))
                : 1f;
        }

        private static float GetEventOccurrenceLifetime<T>(T evt) where T : SkillTimedEventBase
        {
            return evt switch
            {
                SkillPhysicsEvent physicsEvent => SharedSkillDefinition.GetPhysicsEventTailDuration(physicsEvent),
                SkillAttributeEvent attributeEvent => SharedSkillDefinition.GetAttributeEventTailDuration(attributeEvent),
                SkillVfxEvent vfxEvent => SharedSkillDefinition.GetVfxEventTailDuration(vfxEvent),
                SkillSfxEvent sfxEvent => SharedSkillDefinition.GetSfxEventTailDuration(sfxEvent),
                _ => 0f,
            };
        }
    }
}
