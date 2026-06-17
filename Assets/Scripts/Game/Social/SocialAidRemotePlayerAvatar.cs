using UnityEngine;
using Game;
using Game.Data;
using Game.Presentation;

namespace Game.Social
{
    public sealed class SocialAidRemotePlayerAvatar : MonoBehaviour
    {
        public enum RemoteAction : byte
        {
            Idle = 0,
            Move = 1,
            Aim = 2,
            Jump = 3,
            AirJump = 4,
            Fall = 5,
            Land = 6,
            Dodge = 7,
            Attack0 = 8,
            Attack1 = 9,
            Attack2 = 10,
            Attack3 = 11,
            AirAttack = 12,
            FallAttackStart = 13,
            FallAttackLoop = 14,
            FallAttackLand = 15,
            ChargeStart = 16,
            ChargeLoop = 17,
            ChargeRelease = 18,
            Shoot = 19,
            ShootCharge = 20,
            Skill = 21,
            HitStun = 22,
            Dead = 23,
        }

        private const float TurnLerpSpeed = 18f;
        private const float AimHeadTrackingBlendSpeed = 10f;
        private const float MaxAimUpAngle = 30f;
        private const float MaxAimDownAngle = 22f;
        private const float UpperChestAimWeight = 0.2f;
        private const float ChestAimWeight = 0.25f;
        private const float NeckAimWeight = 0.35f;
        private const float HeadAimWeight = 0.65f;
        private const float SnapshotInterpolationDelaySeconds = 0.2f;
        private const float MaxSnapshotExtrapolationSeconds = 0.08f;
        private const float AnimatorFloatBlendSpeed = 14f;
        private const float SnapshotTimeEpsilon = 0.0001f;
        private const int MaxBufferedSnapshots = 12;
        private const int MaxBufferedActionPresentations = 8;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int UpperBodyPlaybackSpeedHash = Animator.StringToHash("UpperBodyPlaybackSpeed");

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Animator _animator;
        private RemoteAction _lastAction = (RemoteAction)byte.MaxValue;
        private int _lastActionSequence;
        private string _lastLocomotionTrigger = string.Empty;
        private SkillTimelineRunner _visualTimelineRunner;
        private float _lastPresentedMoveSpeed;
        private float _displayedMoveSpeed;
        private float _displayedMoveX;
        private float _displayedMoveY;
        private bool _hasDisplayedMoveParameters;
        private PlayerAttackMode _lastPresentedAttackMode = PlayerAttackMode.Melee;
        private bool _aimActive;
        private bool _aimLayerActive;
        private float _targetAimPitch;
        private Vector3 _targetAimDirection = Vector3.forward;
        private Vector3 _currentAimDirection = Vector3.forward;
        private int _aimLayerIndex = -1;
        private float _currentAimHeadTrackingWeight;
        private Transform _spineBone;
        private Transform _upperChestBone;
        private Transform _chestBone;
        private Transform _neckBone;
        private Transform _headBone;
        private System.Collections.Generic.Dictionary<string, AnimatorControllerParameterType> _animatorParameterTypes;
        private readonly System.Collections.Generic.List<RemotePoseSnapshot> _poseBuffer = new System.Collections.Generic.List<RemotePoseSnapshot>();
        private readonly System.Collections.Generic.List<RemoteActionPresentation> _pendingActionPresentations = new System.Collections.Generic.List<RemoteActionPresentation>();

        public float LastPoseTime { get; private set; }

        private void Awake()
        {
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            _animator = GetComponentInChildren<Animator>(true);
            _aimLayerIndex = _animator != null ? _animator.GetLayerIndex("Aim Layer") : -1;
            ResolveAimBones();
            LastPoseTime = Time.unscaledTime;
        }

        public void ApplyPose(
            Vector3 position,
            float yaw,
            bool aimActive,
            bool aimLayerActive,
            float aimPitch,
            Vector3 aimDirection,
            float moveSpeed,
            float moveX,
            float moveY,
            string actionId,
            RemoteAction action,
            PlayerAttackMode attackMode,
            bool hasMeleeWeapon,
            bool hasRangedWeapon,
            float sampleTime)
        {
            LastPoseTime = Time.unscaledTime;
            InsertPoseSnapshot(new RemotePoseSnapshot(
                sampleTime,
                position,
                Quaternion.Euler(0f, yaw, 0f),
                aimActive && aimDirection.sqrMagnitude > 0.0001f,
                aimLayerActive,
                aimPitch,
                aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward,
                moveSpeed,
                moveX,
                moveY,
                actionId,
                action,
                attackMode,
                hasMeleeWeapon,
                hasRangedWeapon));
        }

        public void ApplyActionPresentation(string actionId, string skillEffectId, int actionSequence, float actionElapsedSeconds, float actionNormalizedProgress, float sampleTime)
        {
            if (actionSequence <= 0 || actionSequence == _lastActionSequence)
                return;

            _lastActionSequence = actionSequence;
            if (actionNormalizedProgress >= 0.85f)
                return;

            _pendingActionPresentations.Add(new RemoteActionPresentation(sampleTime, actionId, skillEffectId, actionElapsedSeconds));
            if (_pendingActionPresentations.Count > MaxBufferedActionPresentations)
                _pendingActionPresentations.RemoveAt(0);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;
            EvaluateBufferedPose();
            transform.position = _targetPosition;
            transform.rotation = _targetRotation;
            _currentAimDirection = Vector3.Slerp(_currentAimDirection, _targetAimDirection, Mathf.Clamp01(TurnLerpSpeed * deltaTime));
            ApplyAimHeadTracking(deltaTime);
            EvaluatePendingActionPresentations(Time.unscaledTime - SnapshotInterpolationDelaySeconds);
            TickVisualTimeline(deltaTime);
        }

        private void InsertPoseSnapshot(RemotePoseSnapshot snapshot)
        {
            bool wasEmpty = _poseBuffer.Count == 0;
            int insertIndex = _poseBuffer.Count;
            for (int i = _poseBuffer.Count - 1; i >= 0; i--)
            {
                float timeDelta = snapshot.receivedTime - _poseBuffer[i].receivedTime;
                if (Mathf.Abs(timeDelta) <= SnapshotTimeEpsilon)
                {
                    _poseBuffer[i] = snapshot;
                    insertIndex = -1;
                    break;
                }

                if (timeDelta > 0f)
                    break;

                insertIndex = i;
            }

            if (insertIndex >= 0)
                _poseBuffer.Insert(insertIndex, snapshot);

            if (_poseBuffer.Count > MaxBufferedSnapshots)
                _poseBuffer.RemoveAt(0);

            if (wasEmpty && _poseBuffer.Count == 1)
            {
                _targetPosition = snapshot.position;
                _targetRotation = snapshot.rotation;
                _aimActive = snapshot.aimActive;
                _aimLayerActive = snapshot.aimLayerActive;
                _targetAimPitch = snapshot.aimPitch;
                _targetAimDirection = snapshot.aimDirection;
                ApplySnapshotPresentation(snapshot);
            }
        }

        private void EvaluateBufferedPose()
        {
            if (_poseBuffer.Count <= 0)
                return;

            float renderTime = Time.unscaledTime - SnapshotInterpolationDelaySeconds;
            while (_poseBuffer.Count >= 3 && _poseBuffer[1].receivedTime <= renderTime)
                _poseBuffer.RemoveAt(0);

            if (_poseBuffer.Count >= 2)
            {
                RemotePoseSnapshot from = _poseBuffer[0];
                RemotePoseSnapshot to = _poseBuffer[1];
                float duration = Mathf.Max(0.0001f, to.receivedTime - from.receivedTime);
                float rawT = (renderTime - from.receivedTime) / duration;
                float extrapolationT = 1f + MaxSnapshotExtrapolationSeconds / duration;
                float positionT = Mathf.Clamp(rawT, 0f, extrapolationT);
                float poseT = Mathf.Clamp01(rawT);
                _targetPosition = Vector3.LerpUnclamped(from.position, to.position, positionT);
                _targetRotation = Quaternion.Slerp(from.rotation, to.rotation, poseT);
                _aimActive = poseT < 0.5f ? from.aimActive : to.aimActive;
                _aimLayerActive = poseT < 0.5f ? from.aimLayerActive : to.aimLayerActive;
                _targetAimPitch = Mathf.Lerp(from.aimPitch, to.aimPitch, poseT);
                _targetAimDirection = Vector3.Slerp(from.aimDirection, to.aimDirection, poseT).normalized;
                ApplySnapshotPresentation(poseT < 0.5f ? from : to, Mathf.Lerp(from.moveSpeed, to.moveSpeed, poseT), Mathf.Lerp(from.moveX, to.moveX, poseT), Mathf.Lerp(from.moveY, to.moveY, poseT));
                return;
            }

            RemotePoseSnapshot latest = _poseBuffer[0];
            _targetPosition = latest.position;
            _targetRotation = latest.rotation;
            _aimActive = latest.aimActive;
            _aimLayerActive = latest.aimLayerActive;
            _targetAimPitch = latest.aimPitch;
            _targetAimDirection = latest.aimDirection;
            ApplySnapshotPresentation(latest);
        }

        private void ApplySnapshotPresentation(RemotePoseSnapshot snapshot)
        {
            ApplySnapshotPresentation(snapshot, snapshot.moveSpeed, snapshot.moveX, snapshot.moveY);
        }

        private void ApplySnapshotPresentation(RemotePoseSnapshot snapshot, float moveSpeed, float moveX, float moveY)
        {
            _lastPresentedMoveSpeed = moveSpeed;
            _lastPresentedAttackMode = snapshot.attackMode;
            ApplyWeaponState(snapshot.attackMode, snapshot.hasMeleeWeapon, snapshot.hasRangedWeapon);
            ApplyAnimationState(moveSpeed, moveX, moveY, snapshot.action, snapshot.actionId, snapshot.attackMode);
            ApplyAimLayerState(snapshot.attackMode);
        }

        private void ApplyWeaponState(PlayerAttackMode attackMode, bool hasMeleeWeapon, bool hasRangedWeapon)
        {
            bool showMeleeWeapon = attackMode == PlayerAttackMode.Melee && hasMeleeWeapon;
            bool showRangedWeapon = attackMode == PlayerAttackMode.Ranged && hasRangedWeapon;
            PlayerWeaponVisualUtility.ApplyWeaponVisibility(transform, attackMode, showMeleeWeapon, showRangedWeapon);
        }

        private void ApplyAimLayerState(PlayerAttackMode attackMode)
        {
            if (_animator == null || _aimLayerIndex < 0)
                return;

            bool active = attackMode == PlayerAttackMode.Ranged && _aimLayerActive;
            _animator.SetLayerWeight(_aimLayerIndex, active ? 1f : 0f);
        }

        private void ApplyAnimationState(float moveSpeed, float moveX, float moveY, RemoteAction action, string actionId, PlayerAttackMode attackMode)
        {
            if (_animator == null)
                return;

            RemoteAction presentationAction = action;
            float normalizedMoveSpeed = Mathf.Clamp01(moveSpeed);
            float targetMoveX = Mathf.Clamp(moveX, -1f, 1f);
            float targetMoveY = Mathf.Clamp(moveY, -1f, 1f);
            SmoothAnimatorMoveParameters(normalizedMoveSpeed, targetMoveX, targetMoveY);
            SetFloatIfExists("Speed", SpeedHash, _displayedMoveSpeed);
            SetFloatIfExists("MoveX", MoveXHash, _displayedMoveX);
            SetFloatIfExists("MoveY", MoveYHash, _displayedMoveY);
            SetFloatIfExists("UpperBodyPlaybackSpeed", UpperBodyPlaybackSpeedHash, 1f);
            SetBoolIfExists("IsGrounded", IsGroundedHash, IsGroundedAction(presentationAction));
            EnsureRangedLocomotionState(presentationAction, actionId);

            if (presentationAction == _lastAction)
            {
                EnsureLocomotionState(presentationAction);
                return;
            }

            _lastAction = presentationAction;
            if (!IsLocomotionAction(presentationAction))
                _lastLocomotionTrigger = string.Empty;
            TriggerAction(presentationAction, actionId);
        }

        private void SmoothAnimatorMoveParameters(float targetMoveSpeed, float targetMoveX, float targetMoveY)
        {
            if (!_hasDisplayedMoveParameters)
            {
                _displayedMoveSpeed = targetMoveSpeed;
                _displayedMoveX = targetMoveX;
                _displayedMoveY = targetMoveY;
                _hasDisplayedMoveParameters = true;
                return;
            }

            float maxDelta = AnimatorFloatBlendSpeed * Time.unscaledDeltaTime;
            _displayedMoveSpeed = Mathf.MoveTowards(_displayedMoveSpeed, targetMoveSpeed, maxDelta);
            _displayedMoveX = Mathf.MoveTowards(_displayedMoveX, targetMoveX, maxDelta);
            _displayedMoveY = Mathf.MoveTowards(_displayedMoveY, targetMoveY, maxDelta);
        }

        private void PlayVisualTimeline(string actionId, string skillEffectId, float actionElapsedSeconds)
        {
            StopVisualTimeline();

            if (string.IsNullOrWhiteSpace(skillEffectId))
                return;

            SharedSkillDefinition definition = null;
            SkillConfigEntry entry = null;
            var skillConfig = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillConfig != null && !string.IsNullOrWhiteSpace(actionId))
                entry = skillConfig.GetEntryByActionId(actionId.Trim());

            if (entry != null)
                definition = entry.ResolveSkillEffectDefinition(skillEffectId.Trim());

            if (definition == null)
                definition = ConfigManager.GetInstance()?.GetSkillEffectDatabase()?.GetEntry(skillEffectId.Trim());

            if (definition == null)
                return;

            _visualTimelineRunner = new SkillTimelineRunner();
            _visualTimelineRunner.Begin(definition, new RemoteVisualSkillExecutionContext(this), visualOnly: true);
            if (actionElapsedSeconds > 0f)
                _visualTimelineRunner.Tick(actionElapsedSeconds);
        }

        private void EvaluatePendingActionPresentations(float renderTime)
        {
            while (_pendingActionPresentations.Count > 0 && _pendingActionPresentations[0].receivedTime <= renderTime)
            {
                RemoteActionPresentation presentation = _pendingActionPresentations[0];
                _pendingActionPresentations.RemoveAt(0);
                PlayActionAnimation(presentation.actionId, presentation.skillEffectId);
                PlayVisualTimeline(presentation.actionId, presentation.skillEffectId, presentation.actionElapsedSeconds);
            }
        }

        private void PlayActionAnimation(string actionId, string skillEffectId)
        {
            if (IsBaseMotionActionId(actionId))
                return;
            if (IsRangedShootActionId(actionId))
                EnsureLocomotionTrigger("AimLocomotion");

            string triggerName = ResolvePresentationTrigger(actionId, skillEffectId);
            if (!string.IsNullOrWhiteSpace(triggerName))
                SetTriggerIfExists(triggerName);
        }

        private static bool IsRangedShootActionId(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            string normalized = actionId.Trim();
            return string.Equals(normalized, "Shoot", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "ShootCharge", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "Shoot_Charge", System.StringComparison.Ordinal);
        }

        private static bool IsBaseMotionActionId(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            string normalized = actionId.Trim();
            return string.Equals(normalized, "Idle", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "Move", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "Aim", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "Jump", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "AirJump", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "Fall", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "Land", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "NormalLocomotion", System.StringComparison.Ordinal) ||
                   string.Equals(normalized, "AimLocomotion", System.StringComparison.Ordinal);
        }

        private static string ResolvePresentationTrigger(string actionId, string skillEffectId)
        {
            SkillConfigEntry entry = null;
            var skillConfig = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillConfig != null && !string.IsNullOrWhiteSpace(actionId))
                entry = skillConfig.GetEntryByActionId(actionId.Trim());

            if (entry != null)
            {
                SharedSkillDefinition entryDefinition = !string.IsNullOrWhiteSpace(skillEffectId)
                    ? entry.ResolveSkillEffectDefinition(skillEffectId.Trim())
                    : entry.ResolveSkillEffectDefinition();
                string entryTrigger = entryDefinition != null ? entryDefinition.GetResolvedAnimationTrigger() : entry.GetResolvedAnimationTrigger();
                if (!string.IsNullOrWhiteSpace(entryTrigger))
                    return entryTrigger;
            }

            if (!string.IsNullOrWhiteSpace(skillEffectId))
            {
                SharedSkillDefinition definition = ConfigManager.GetInstance()?.GetSkillEffectDatabase()?.GetEntry(skillEffectId.Trim());
                string definitionTrigger = definition != null ? definition.GetResolvedAnimationTrigger() : string.Empty;
                if (!string.IsNullOrWhiteSpace(definitionTrigger))
                    return definitionTrigger;
            }

            return string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
        }

        private void TickVisualTimeline(float deltaTime)
        {
            if (_visualTimelineRunner == null)
                return;

            _visualTimelineRunner.Tick(deltaTime);
            if (_visualTimelineRunner.IsComplete)
                _visualTimelineRunner = null;
        }

        private void StopVisualTimeline()
        {
            if (_visualTimelineRunner == null)
                return;

            _visualTimelineRunner.Stop();
            _visualTimelineRunner = null;
        }

        private void TriggerAction(RemoteAction action, string actionId = null)
        {
            string triggerName = GetTriggerName(action);
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            EnsureRangedLocomotionState(action, actionId);
            SetTriggerIfExists(triggerName);
            if (IsLocomotionAction(action))
                _lastLocomotionTrigger = triggerName;
        }

        private void EnsureLocomotionState(RemoteAction action)
        {
            if (!IsLocomotionAction(action))
                return;

            string triggerName = GetTriggerName(action);
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            if (string.Equals(_lastLocomotionTrigger, triggerName, System.StringComparison.Ordinal) && action == _lastAction)
                return;

            SetTriggerIfExists(triggerName);
            _lastLocomotionTrigger = triggerName;
        }

        private void EnsureRangedLocomotionState(RemoteAction action, string actionId)
        {
            if (!IsRangedShootAction(action, actionId))
                return;

            EnsureLocomotionTrigger("AimLocomotion");
        }

        private void EnsureLocomotionTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            if (string.Equals(_lastLocomotionTrigger, triggerName, System.StringComparison.Ordinal))
                return;

            SetTriggerIfExists(triggerName);
            _lastLocomotionTrigger = triggerName;
        }

        private static bool IsRangedShootAction(RemoteAction action, string actionId)
        {
            return action == RemoteAction.Shoot ||
                   action == RemoteAction.ShootCharge ||
                   IsRangedShootActionId(actionId);
        }

        private static bool IsLocomotionAction(RemoteAction action)
        {
            return action == RemoteAction.Idle ||
                   action == RemoteAction.Move ||
                   action == RemoteAction.Aim;
        }

        private static string GetTriggerName(RemoteAction action)
        {
            return action switch
            {
                RemoteAction.Idle => "NormalLocomotion",
                RemoteAction.Move => "NormalLocomotion",
                RemoteAction.Aim => "AimLocomotion",
                RemoteAction.Jump => "Jump",
                RemoteAction.AirJump => "AirJump",
                RemoteAction.Fall => "Fall",
                RemoteAction.Land => "Land",
                RemoteAction.Dodge => "Dodge",
                RemoteAction.Attack0 => "Attack0",
                RemoteAction.Attack1 => "Attack1",
                RemoteAction.Attack2 => "Attack2",
                RemoteAction.Attack3 => "Attack3",
                RemoteAction.AirAttack => "AirAttack",
                RemoteAction.FallAttackStart => "FallAttackStart",
                RemoteAction.FallAttackLoop => "FallAttackLoop",
                RemoteAction.FallAttackLand => "FallAttackLand",
                RemoteAction.ChargeStart => "ChargeStart",
                RemoteAction.ChargeLoop => "ChargeLoop",
                RemoteAction.ChargeRelease => "ChargeRelease",
                RemoteAction.Shoot => "Shoot",
                RemoteAction.ShootCharge => "Shoot_Charge",
                RemoteAction.Skill => "Skill0",
                RemoteAction.HitStun => "HitStun",
                RemoteAction.Dead => "Dead",
                _ => string.Empty,
            };
        }

        private static bool IsGroundedAction(RemoteAction action)
        {
            return action != RemoteAction.Jump &&
                   action != RemoteAction.AirJump &&
                   action != RemoteAction.Fall &&
                   action != RemoteAction.AirAttack &&
                   action != RemoteAction.FallAttackStart &&
                   action != RemoteAction.FallAttackLoop;
        }

        private void SetTriggerIfExists(string triggerName)
        {
            if (!HasParameter(triggerName, AnimatorControllerParameterType.Trigger))
                return;

            if (ShouldClearLocomotionTrigger(triggerName))
                ResetLocomotionTriggers();

            _animator.ResetTrigger(triggerName);
            _animator.SetTrigger(triggerName);
        }

        private void ResetLocomotionTriggers()
        {
            ResetTriggerIfExists("NormalLocomotion");
            ResetTriggerIfExists("AimLocomotion");
        }

        private void ResetTriggerIfExists(string triggerName)
        {
            if (HasParameter(triggerName, AnimatorControllerParameterType.Trigger))
                _animator.ResetTrigger(triggerName);
        }

        private static bool ShouldClearLocomotionTrigger(string triggerName)
        {
            return !string.IsNullOrWhiteSpace(triggerName)
                   && !string.Equals(triggerName, "NormalLocomotion", System.StringComparison.Ordinal)
                   && !string.Equals(triggerName, "AimLocomotion", System.StringComparison.Ordinal)
                   && !string.Equals(triggerName, "Aim", System.StringComparison.Ordinal)
                   && !PlayerActionRouting.IsRangedActionName(triggerName);
        }

        private void SetFloatIfExists(string parameterName, int parameterHash, float value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Float))
                _animator.SetFloat(parameterHash, value);
        }

        private void SetBoolIfExists(string parameterName, int parameterHash, bool value)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
                _animator.SetBool(parameterHash, value);
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            if (_animatorParameterTypes == null)
            {
                _animatorParameterTypes = new System.Collections.Generic.Dictionary<string, AnimatorControllerParameterType>();
                AnimatorControllerParameter[] parameters = _animator.parameters;
                for (int i = 0; i < parameters.Length; i++)
                {
                    AnimatorControllerParameter parameter = parameters[i];
                    _animatorParameterTypes[parameter.name] = parameter.type;
                }
            }

            return _animatorParameterTypes.TryGetValue(parameterName, out AnimatorControllerParameterType actualType) &&
                   actualType == parameterType;
        }

        public bool TryGetCurrentAimPose(out SkillAimPose aimPose)
        {
            aimPose = default;
            if (!_aimActive || _currentAimDirection.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 origin = ResolveAimOrigin();
            Vector3 direction = _currentAimDirection.normalized;
            aimPose = new SkillAimPose(
                origin,
                origin + direction * 100f,
                direction,
                Quaternion.LookRotation(direction, Vector3.up));
            return true;
        }

        private Vector3 ResolveAimOrigin()
        {
            Transform rangedWeapon = FindChildTransformByName(transform, PlayerWeaponVisualUtility.RangedWeaponName);
            if (rangedWeapon != null)
                return rangedWeapon.position;

            return transform.position + Vector3.up * 1.2f + transform.forward * 0.2f;
        }

        private void ResolveAimBones()
        {
            if (_animator == null || !_animator.isHuman)
                return;

            _spineBone = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _upperChestBone = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
            _chestBone = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _neckBone = _animator.GetBoneTransform(HumanBodyBones.Neck);
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
        }

        private void ApplyAimHeadTracking(float deltaTime)
        {
            if (_animator == null || !_animator.isHuman)
                return;

            float targetWeight = _aimActive ? 1f : 0f;
            _currentAimHeadTrackingWeight = Mathf.MoveTowards(
                _currentAimHeadTrackingWeight,
                targetWeight,
                AimHeadTrackingBlendSpeed * deltaTime);

            if (_currentAimHeadTrackingWeight <= 0.001f)
                return;

            float clampedPitch = _targetAimPitch;
            ApplyAimPitchToBone(_spineBone, clampedPitch, UpperChestAimWeight * 0.35f);
            ApplyAimPitchToBone(_upperChestBone, clampedPitch, UpperChestAimWeight);
            ApplyAimPitchToBone(_chestBone, clampedPitch, ChestAimWeight);
            ApplyAimPitchToBone(_neckBone, clampedPitch, NeckAimWeight);
            ApplyAimPitchToBone(_headBone, clampedPitch, HeadAimWeight);
        }

        private void ApplyAimPitchToBone(Transform bone, float clampedPitch, float weight)
        {
            if (bone == null)
                return;

            float finalPitch = clampedPitch * Mathf.Clamp01(weight) * _currentAimHeadTrackingWeight;
            if (Mathf.Abs(finalPitch) <= 0.001f)
                return;

            bone.rotation = Quaternion.AngleAxis(-finalPitch, transform.right) * bone.rotation;
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

        private sealed class RemoteVisualSkillExecutionContext : ISkillExecutionContext, ISkillAimPoseProvider
        {
            private readonly SocialAidRemotePlayerAvatar _avatar;
            private readonly Collider[] _overlapBuffer = new Collider[1];

            public RemoteVisualSkillExecutionContext(SocialAidRemotePlayerAvatar avatar)
            {
                _avatar = avatar;
            }

            public Transform CasterTransform => _avatar != null ? _avatar.transform : null;

            public float CasterAttack => 0f;

            public Collider[] OverlapBuffer => _overlapBuffer;

            public bool IsAimModeActive => _avatar != null && _avatar._aimActive;

            public bool TryGetCurrentAimPose(out SkillAimPose aimPose)
            {
                aimPose = default;
                return _avatar != null && _avatar.TryGetCurrentAimPose(out aimPose);
            }
        }

        private readonly struct RemotePoseSnapshot
        {
            public readonly float receivedTime;
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public readonly bool aimActive;
            public readonly bool aimLayerActive;
            public readonly float aimPitch;
            public readonly Vector3 aimDirection;
            public readonly float moveSpeed;
            public readonly float moveX;
            public readonly float moveY;
            public readonly string actionId;
            public readonly RemoteAction action;
            public readonly PlayerAttackMode attackMode;
            public readonly bool hasMeleeWeapon;
            public readonly bool hasRangedWeapon;

            public RemotePoseSnapshot(
                float receivedTime,
                Vector3 position,
                Quaternion rotation,
                bool aimActive,
                bool aimLayerActive,
                float aimPitch,
                Vector3 aimDirection,
                float moveSpeed,
                float moveX,
                float moveY,
                string actionId,
                RemoteAction action,
                PlayerAttackMode attackMode,
                bool hasMeleeWeapon,
                bool hasRangedWeapon)
            {
                this.receivedTime = receivedTime;
                this.position = position;
                this.rotation = rotation;
                this.aimActive = aimActive;
                this.aimLayerActive = aimLayerActive;
                this.aimPitch = Mathf.Clamp(aimPitch, -MaxAimDownAngle, MaxAimUpAngle);
                this.aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : rotation * Vector3.forward;
                this.moveSpeed = moveSpeed;
                this.moveX = moveX;
                this.moveY = moveY;
                this.actionId = string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
                this.action = action;
                this.attackMode = attackMode;
                this.hasMeleeWeapon = hasMeleeWeapon;
                this.hasRangedWeapon = hasRangedWeapon;
            }
        }

        private readonly struct RemoteActionPresentation
        {
            public readonly float receivedTime;
            public readonly string actionId;
            public readonly string skillEffectId;
            public readonly float actionElapsedSeconds;

            public RemoteActionPresentation(float receivedTime, string actionId, string skillEffectId, float actionElapsedSeconds)
            {
                this.receivedTime = receivedTime;
                this.actionId = actionId;
                this.skillEffectId = skillEffectId;
                this.actionElapsedSeconds = Mathf.Max(0f, actionElapsedSeconds);
            }
        }
    }
}
