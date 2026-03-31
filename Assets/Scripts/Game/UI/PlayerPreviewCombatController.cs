using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Presentation;

namespace Game.UI
{
    /// <summary>
    /// 背包预览专用的轻量战斗控制器。
    /// 只保留游戏内的 Idle + 四段普攻语义，输入仅响应普通攻击，
    /// 并且只执行技能库中的顶层 VFX / SFX。
    /// </summary>
    public sealed class PlayerPreviewCombatController
    {
        private const float PreviewComboWindowSeconds = 0.3f;
        private const float PreviewAttackFallbackDuration = 1.2f;
        private static readonly int PreviewSpeedHash = Animator.StringToHash("Speed");
        private static readonly int PreviewNormalLocomotionHash = Animator.StringToHash("NormalLocomotion");

        private readonly PreviewCueTimelineRunner _previewCueTimeline = new PreviewCueTimelineRunner();
        private Animator _animator;
        private Transform _previewRoot;
        private Game.Domain.PlayerModel _playerModel;
        private int _currentAttackIndex = -1;
        private bool _attackBuffered;
        private int _comboWindowNextIndex = -1;
        private float _comboWindowExpireAt;
        private float _attackFallbackExpireAt;
        private bool _attackEnteredOneShotState;
        private float _currentAttackStateAge;
        private string _lastAttackTriggerName;

        public void Bind(Animator animator, Transform previewRoot, Game.Domain.PlayerModel playerModel)
        {
            Unbind();

            _animator = animator;
            _previewRoot = previewRoot;
            _playerModel = playerModel;
            if (_animator == null)
                return;

            _animator.Rebind();
            _animator.Update(0f);
            TriggerLocomotion();
            _animator.Update(0f);
        }

        public void Unbind()
        {
            _previewCueTimeline.Stop();
            _animator = null;
            _previewRoot = null;
            _playerModel = null;
            _currentAttackIndex = -1;
            _attackBuffered = false;
            _comboWindowNextIndex = -1;
            _comboWindowExpireAt = 0f;
            _attackFallbackExpireAt = 0f;
            _attackEnteredOneShotState = false;
            _currentAttackStateAge = 0f;
            _lastAttackTriggerName = string.Empty;
        }

        public void Tick(float deltaTime)
        {
            _previewCueTimeline.Tick(deltaTime);

            if (_comboWindowNextIndex >= 0 && Time.unscaledTime > _comboWindowExpireAt)
            {
                _comboWindowNextIndex = -1;
                _comboWindowExpireAt = 0f;
            }

            if (_currentAttackIndex < 0 || _animator == null)
                return;

            _currentAttackStateAge += deltaTime;

            if (_animator.IsInTransition(0))
                return;

            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            float normalizedTime = Mathf.Max(0f, info.normalizedTime);
            float pendingThreshold = GetAttackPendingThreshold(_currentAttackIndex);
            bool isLoopingState = info.loop;
            bool shouldForceEnterAttack = !_attackEnteredOneShotState && Time.unscaledTime >= _attackFallbackExpireAt;

            if (!_attackEnteredOneShotState)
            {
                if (isLoopingState)
                {
                    if (!shouldForceEnterAttack)
                        return;
                }
                else
                {
                    _attackEnteredOneShotState = true;
                }
            }

            if (_currentAttackStateAge <= 0.1f)
                return;

            int bufferedFollowUpIndex = GetBufferedFollowUpAttackIndex();
            if (_attackBuffered && bufferedFollowUpIndex >= 0 && normalizedTime >= pendingThreshold)
            {
                StartAttack(bufferedFollowUpIndex);
                return;
            }

            float naturalExitThreshold = GetNaturalExitThreshold(_currentAttackIndex);
            if (!isLoopingState && normalizedTime < naturalExitThreshold)
                return;

            bool canOpenComboWindow = _currentAttackIndex < 3;
            if (_attackBuffered && bufferedFollowUpIndex >= 0)
            {
                StartAttack(bufferedFollowUpIndex);
                return;
            }

            EndAttackToIdle(canOpenComboWindow);
        }

        public void RequestNormalAttack()
        {
            if (_animator == null)
                return;

            if (_currentAttackIndex >= 0)
            {
                _attackBuffered = true;
                return;
            }

            float now = Time.unscaledTime;
            if (_comboWindowNextIndex >= 0 && now <= _comboWindowExpireAt)
            {
                StartAttack(_comboWindowNextIndex);
                return;
            }

            _comboWindowNextIndex = -1;
            _comboWindowExpireAt = 0f;
            StartAttack(0);
        }

        private void StartAttack(int comboIndex)
        {
            if (_animator == null)
                return;

            comboIndex = Mathf.Clamp(comboIndex, 0, 3);
            _currentAttackIndex = comboIndex;
            _attackBuffered = false;
            _comboWindowNextIndex = -1;
            _comboWindowExpireAt = 0f;
            _attackFallbackExpireAt = Time.unscaledTime + PreviewAttackFallbackDuration;
            _attackEnteredOneShotState = false;
            _currentAttackStateAge = 0f;

            TriggerAttack(comboIndex);
            BeginAttackCue(comboIndex);
        }

        private void EndAttackToIdle(bool openComboWindow)
        {
            if (openComboWindow && _currentAttackIndex >= 0 && _currentAttackIndex < 3)
            {
                _comboWindowNextIndex = _currentAttackIndex + 1;
                _comboWindowExpireAt = Time.unscaledTime + PreviewComboWindowSeconds;
            }
            else
            {
                _comboWindowNextIndex = -1;
                _comboWindowExpireAt = 0f;
            }

            _currentAttackIndex = -1;
            _attackBuffered = false;
            _attackFallbackExpireAt = 0f;
            _attackEnteredOneShotState = false;
            _currentAttackStateAge = 0f;
            TriggerLocomotion();
        }

        private void TriggerLocomotion()
        {
            if (_animator == null)
                return;

            _animator.SetFloat(PreviewSpeedHash, 0f);
            if (!string.IsNullOrWhiteSpace(_lastAttackTriggerName))
                _animator.ResetTrigger(_lastAttackTriggerName);
            ResetLocomotionTrigger();
            if (HasParameter("NormalLocomotion"))
                _animator.SetTrigger(PreviewNormalLocomotionHash);
        }

        private void TriggerAttack(int comboIndex)
        {
            if (_animator == null || comboIndex < 0 || comboIndex > 3)
                return;

            string triggerName = ResolveAttackTriggerName(comboIndex);
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            _animator.SetFloat(PreviewSpeedHash, 0f);
            ResetLocomotionTrigger();
            if (!string.IsNullOrWhiteSpace(_lastAttackTriggerName))
                _animator.ResetTrigger(_lastAttackTriggerName);
            _animator.SetTrigger(triggerName);
            _lastAttackTriggerName = triggerName;
        }

        private void ResetLocomotionTrigger()
        {
            if (_animator == null)
                return;

            if (HasParameter("NormalLocomotion"))
                _animator.ResetTrigger(PreviewNormalLocomotionHash);
        }

        private bool HasParameter(string parameterName)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter != null && parameter.name == parameterName)
                    return true;
            }

            return false;
        }

        private void BeginAttackCue(int comboIndex)
        {
            if (_previewRoot == null)
                return;

            SkillConfigEntry entry = ResolveAttackEntry(comboIndex);
            SharedSkillDefinition definition = GetSharedSkillDefinition(entry, comboIndex);
            _previewCueTimeline.Begin(definition, _previewRoot);
        }

        private SharedSkillDefinition GetSharedSkillDefinition(SkillConfigEntry entry, int comboIndex)
        {
            if (entry != null)
            {
                SharedSkillDefinition resolvedDefinition = _playerModel != null
                    ? _playerModel.ResolveSkillEffectDefinition(entry)
                    : entry.ResolveSkillEffectDefinition();
                if (resolvedDefinition != null)
                    return resolvedDefinition;
            }

            string skillId = entry != null
                ? (_playerModel != null ? _playerModel.ResolveSkillEffectId(entry) : entry.GetResolvedSkillId())
                : ResolveAttackActionId(comboIndex);
            if (string.IsNullOrWhiteSpace(skillId))
                return null;

            var db = ConfigManager.GetInstance()?.GetSkillEffectDatabase();
            if (db == null)
                db = Resources.Load<SkillEffectDatabaseSO>("配置/技能效果库");
            return db != null ? db.GetEntry(skillId) : null;
        }

        private float GetAttackPendingThreshold(int comboIndex)
        {
            SkillConfigEntry entry = ResolveAttackEntry(comboIndex);
            if (entry != null && entry.TryGetPendingReleaseThreshold(GameAction.NormalAttack, out float configured))
                return configured;

            return comboIndex < 3 ? 0.7f : 0.6f;
        }

        private float GetNaturalExitThreshold(int comboIndex)
        {
            SkillConfigEntry entry = ResolveAttackEntry(comboIndex);
            if (entry != null && entry.overrideNaturalExitNormalizedTime)
                return entry.naturalExitNormalizedTime;

            return 0.9f;
        }

        private SkillConfigEntry ResolveAttackEntry(int comboIndex)
        {
            SkillConfigDatabaseSO skillDb = GetSkillConfigDatabase();
            string actionId = ResolveAttackActionId(comboIndex);
            return skillDb != null && !string.IsNullOrWhiteSpace(actionId)
                ? skillDb.GetEntryByActionId(actionId)
                : null;
        }

        private string ResolveAttackActionId(int comboIndex)
        {
            string fallbackActionId = $"Attack{comboIndex}";
            SkillConfigDatabaseSO skillDb = GetSkillConfigDatabase();
            return _playerModel != null && skillDb != null
                ? _playerModel.ResolveCurrentFormActionId((PlayerFormActionSlot)comboIndex, skillDb, fallbackActionId)
                : fallbackActionId;
        }

        private string ResolveAttackTriggerName(int comboIndex)
        {
            SkillConfigEntry entry = ResolveAttackEntry(comboIndex);
            if (entry != null)
            {
                SharedSkillDefinition definition = GetSharedSkillDefinition(entry, comboIndex);
                if (definition != null)
                {
                    string definitionTrigger = definition.GetResolvedAnimationTrigger();
                    if (!string.IsNullOrWhiteSpace(definitionTrigger))
                        return definitionTrigger;
                }

                return entry.GetResolvedAnimationTrigger();
            }

            return ResolveAttackActionId(comboIndex);
        }

        private static SkillConfigDatabaseSO GetSkillConfigDatabase()
        {
            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillDb == null)
                skillDb = Resources.Load<SkillConfigDatabaseSO>("配置/玩家动作及技能配置库");
            return skillDb;
        }

        private int GetBufferedFollowUpAttackIndex()
        {
            if (_currentAttackIndex < 0)
                return -1;

            return _currentAttackIndex < 3 ? _currentAttackIndex + 1 : 0;
        }

        private sealed class PreviewSkillExecutionContext : ISkillExecutionContext
        {
            private static readonly Collider[] SharedBuffer = new Collider[1];
            private Transform _caster;

            public void Bind(Transform caster)
            {
                _caster = caster;
            }

            public Transform CasterTransform => _caster;
            public float CasterAttack => 0f;
            public Collider[] OverlapBuffer => SharedBuffer;
        }

        private sealed class PreviewCueTimelineRunner
        {
            private sealed class EventState
            {
                public bool hasTriggered;
                public float nextTriggerTime;
            }

            private readonly PreviewSkillExecutionContext _context = new PreviewSkillExecutionContext();
            private readonly SkillCueRuntimeScope _cueRuntime = new SkillCueRuntimeScope();
            private readonly List<GameObject> _audioInstances = new List<GameObject>();
            private SharedSkillDefinition _definition;
            private float _elapsed;
            private float _duration;
            private bool _running;
            private EventState[] _vfxStates;
            private EventState[] _sfxStates;
            private int _previewLayer;

            public void Begin(SharedSkillDefinition definition, Transform caster)
            {
                Stop();
                if (definition == null || caster == null)
                    return;

                _definition = definition;
                _elapsed = 0f;
                _running = true;
                _duration = Mathf.Max(GetMaxEndTime(definition.vfxEvents), GetMaxEndTime(definition.sfxEvents));
                _context.Bind(caster);
                _previewLayer = caster.gameObject.layer;
                _vfxStates = BuildStates(definition.vfxEvents);
                _sfxStates = BuildStates(definition.sfxEvents);
                Evaluate();
            }

            public void Tick(float deltaTime)
            {
                if (!_running || _definition == null)
                    return;

                _elapsed += deltaTime;
                Evaluate();
                if (_elapsed >= _duration)
                    _running = false;
            }

            public void Stop()
            {
                _running = false;
                _definition = null;
                _elapsed = 0f;
                _duration = 0f;
                _vfxStates = null;
                _sfxStates = null;
                _context.Bind(null);
                _previewLayer = 0;
                _cueRuntime.Stop();

                for (int i = _audioInstances.Count - 1; i >= 0; i--)
                {
                    GameObject instance = _audioInstances[i];
                    if (instance != null)
                        UnityEngine.Object.Destroy(instance);
                }

                _audioInstances.Clear();
            }

            private void Evaluate()
            {
                EvaluateVfxTrack();
                EvaluateSfxTrack();
            }

            private void EvaluateVfxTrack()
            {
                var events = _definition?.vfxEvents;
                if (events == null || _vfxStates == null)
                    return;

                for (int i = 0; i < Mathf.Min(events.Count, _vfxStates.Length); i++)
                    EvaluateEvent(events[i], _vfxStates[i], PlayVfxEvent);
            }

            private void EvaluateSfxTrack()
            {
                var events = _definition?.sfxEvents;
                if (events == null || _sfxStates == null)
                    return;

                for (int i = 0; i < Mathf.Min(events.Count, _sfxStates.Length); i++)
                    EvaluateEvent(events[i], _sfxStates[i], PlaySfxEvent);
            }

            private void EvaluateEvent<T>(T evt, EventState state, Action<T> trigger) where T : SkillTimedEventBase
            {
                if (evt == null || state == null || trigger == null)
                    return;

                if (evt.triggerMode == SkillEventTriggerMode.Once)
                {
                    if (!state.hasTriggered && _elapsed >= evt.startTime)
                    {
                        trigger(evt);
                        state.hasTriggered = true;
                    }
                    return;
                }

                float interval = Mathf.Max(0.01f, evt.repeatInterval);
                float endTime = evt.GetEndTime();
                while (_elapsed >= state.nextTriggerTime && (evt.RepeatsUntilStateExit || state.nextTriggerTime <= endTime + 0.0001f))
                {
                    trigger(evt);
                    state.hasTriggered = true;
                    state.nextTriggerTime += interval;
                }
            }

            private void PlaySfxEvent(SkillSfxEvent evt)
            {
                if (evt?.sfxEffects == null || _context.CasterTransform == null)
                    return;

                for (int i = 0; i < evt.sfxEffects.Count; i++)
                {
                    SkillSfxEffect effect = evt.sfxEffects[i];
                    if (effect == null || effect.audioClip == null)
                        continue;

                    GameObject instance = new GameObject($"[PreviewSfx]{effect.audioClip.name}");
                    instance.hideFlags = HideFlags.HideAndDontSave;
                    AudioSource audioSource = instance.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                    audioSource.clip = effect.audioClip;
                    audioSource.loop = effect.loop;

                    Transform anchor = ResolveAnchor(effect.anchor, _context.CasterTransform);
                    if (anchor != null)
                    {
                        instance.transform.SetParent(anchor, false);
                        instance.transform.localPosition = effect.offset;
                    }
                    else
                    {
                        instance.transform.position = anchor != null
                            ? anchor.TransformPoint(effect.offset)
                            : _context.CasterTransform.position + effect.offset;
                    }

                    audioSource.Play();
                    RegisterAudioLifetime(instance, effect);
                }
            }

            private void PlayVfxEvent(SkillVfxEvent evt)
            {
                if (evt?.vfxEffects == null || _context.CasterTransform == null)
                    return;

                for (int i = 0; i < evt.vfxEffects.Count; i++)
                {
                    SkillVfxEffect effect = evt.vfxEffects[i];
                    if (effect == null || effect.particlePrefab == null)
                        continue;

                    Transform anchor = ResolveAnchor(effect.anchor, _context.CasterTransform);
                    GameObject instance = CreateVfxInstance(effect, _context.CasterTransform, anchor);
                    RegisterCueLifetime(instance, effect.destroyMode, effect.duration);
                }
            }

            private void RegisterAudioLifetime(GameObject instance, SkillSfxEffect effect)
            {
                if (instance == null || effect == null)
                    return;

                SkillCueDestroyMode destroyMode = effect.destroyMode;
                if (effect.loop && destroyMode == SkillCueDestroyMode.NaturalDestroy)
                    destroyMode = SkillCueDestroyMode.OnStateExit;

                switch (destroyMode)
                {
                    case SkillCueDestroyMode.OnStateExit:
                        _audioInstances.Add(instance);
                        return;
                    case SkillCueDestroyMode.Timed:
                        if (effect.duration > 0f)
                        {
                            UnityEngine.Object.Destroy(instance, effect.duration);
                            return;
                        }
                        break;
                }

                float clipDuration = effect.audioClip != null ? effect.audioClip.length : 0f;
                if (clipDuration > 0f && !effect.loop)
                    UnityEngine.Object.Destroy(instance, clipDuration);
                else if (!effect.loop)
                    UnityEngine.Object.Destroy(instance, 0.1f);
                else
                    _audioInstances.Add(instance);
            }

            private void RegisterCueLifetime(GameObject instance, SkillCueDestroyMode destroyMode, float duration)
            {
                if (instance == null)
                    return;

                switch (destroyMode)
                {
                    case SkillCueDestroyMode.Timed:
                        if (duration > 0f)
                            UnityEngine.Object.Destroy(instance, duration);
                        break;
                    case SkillCueDestroyMode.OnStateExit:
                        _cueRuntime.RegisterStateExitInstance(instance);
                        break;
                }
            }

            private GameObject CreateVfxInstance(SkillVfxEffect effect, Transform caster, Transform anchor)
            {
                if (effect == null || effect.particlePrefab == null)
                    return null;

                bool useWorldMotion = effect.anchor == CueAnchor.World
                    && effect.motion != null
                    && effect.motion.IsActive;
                bool detachFromAnchor = useWorldMotion;
                GameObject instance;
                if (anchor != null && !detachFromAnchor)
                {
                    instance = UnityEngine.Object.Instantiate(effect.particlePrefab, anchor);
                    instance.transform.localPosition = effect.offset;
                    instance.transform.localRotation = Quaternion.Euler(effect.rotationEuler);
                }
                else
                {
                    Vector3 position = anchor != null ? anchor.TransformPoint(effect.offset) : caster.position + effect.offset;
                    Quaternion rotation = anchor != null
                        ? anchor.rotation * Quaternion.Euler(effect.rotationEuler)
                        : caster.rotation * Quaternion.Euler(effect.rotationEuler);
                    instance = UnityEngine.Object.Instantiate(effect.particlePrefab, position, rotation);
                }

                instance.transform.localScale = Vector3.Scale(instance.transform.localScale, effect.scale);
                SetLayerRecursively(instance.transform, _previewLayer);
                ConfigurePreviewParticleSystems(instance, effect.motion, useWorldMotion);
                ApplyCueMotion(instance, effect, caster, useWorldMotion);
                return instance;
            }

            private static Transform ResolveAnchor(CueAnchor anchor, Transform caster)
            {
                return anchor switch
                {
                    CueAnchor.World => null,
                    _ => caster,
                };
            }

            private static void SetLayerRecursively(Transform root, int layer)
            {
                if (root == null)
                    return;

                root.gameObject.layer = layer;
                for (int i = 0; i < root.childCount; i++)
                    SetLayerRecursively(root.GetChild(i), layer);
            }

            private static void ConfigurePreviewParticleSystems(GameObject instance, SkillMotionSettings motion, bool useWorldMotion)
            {
                if (instance == null)
                    return;

                bool useMotion = motion != null && motion.IsActive && useWorldMotion;
                ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    ParticleSystem particleSystem = particleSystems[i];
                    if (particleSystem == null)
                        continue;

                    var main = particleSystem.main;
                    if (useMotion && main.simulationSpace == ParticleSystemSimulationSpace.World)
                        main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.useUnscaledTime = true;
                    particleSystem.Play(true);
                }
            }

            private static void ApplyCueMotion(GameObject instance, SkillVfxEffect effect, Transform caster, bool useWorldMotion)
            {
                SkillMotionSettings motion = effect != null ? effect.motion : null;
                if (instance == null || caster == null || motion == null || !motion.IsActive || !useWorldMotion)
                    return;

                Vector3 originPosition = instance.transform.position;
                Quaternion originRotation = caster.rotation;
                Quaternion lockedRotation = instance.transform.rotation;
                SkillCueMover mover = instance.GetComponent<SkillCueMover>();
                if (mover == null)
                    mover = instance.AddComponent<SkillCueMover>();
                mover.Initialize(originPosition, originRotation, motion, true, true, lockedRotation, caster, effect != null ? effect.offset : Vector3.zero);
            }

            private static EventState[] BuildStates<T>(List<T> events) where T : SkillTimedEventBase
            {
                if (events == null || events.Count == 0)
                    return null;

                EventState[] states = new EventState[events.Count];
                for (int i = 0; i < events.Count; i++)
                {
                    T evt = events[i];
                    states[i] = new EventState
                    {
                        hasTriggered = false,
                        nextTriggerTime = evt != null ? evt.startTime : 0f,
                    };
                }

                return states;
            }

            private static float GetMaxEndTime<T>(List<T> events) where T : SkillTimedEventBase
            {
                float max = 0f;
                if (events == null)
                    return max;

                for (int i = 0; i < events.Count; i++)
                {
                    T evt = events[i];
                    if (evt != null)
                        max = Mathf.Max(max, evt.GetEndTime());
                }

                return max;
            }
        }
    }
}
