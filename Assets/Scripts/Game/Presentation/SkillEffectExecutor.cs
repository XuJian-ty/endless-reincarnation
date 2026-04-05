using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Domain;
using UnityObject = UnityEngine.Object;
using UnityRandom = UnityEngine.Random;

namespace Game.Presentation
{
    public readonly struct SkillDetectionMotionFrame
    {
        public SkillDetectionMotionFrame(Vector3 originPosition, Quaternion originRotation, float elapsed)
        {
            OriginPosition = originPosition;
            OriginRotation = originRotation;
            Elapsed = Mathf.Max(0f, elapsed);
        }

        public Vector3 OriginPosition { get; }
        public Quaternion OriginRotation { get; }
        public float Elapsed { get; }
    }

    public sealed class SkillCueRuntimeScope
    {
        private readonly List<GameObject> _stateExitInstances = new List<GameObject>();
        private readonly List<Action> _stateExitCallbacks = new List<Action>();
        private bool _stopped;

        public void RegisterStateExitInstance(GameObject instance)
        {
            if (instance == null)
                return;

            if (_stopped)
            {
                UnityObject.Destroy(instance);
                return;
            }

            _stateExitInstances.Add(instance);
        }

        public void RegisterStateExitCallback(Action callback)
        {
            if (callback == null)
                return;

            if (_stopped)
            {
                callback.Invoke();
                return;
            }

            _stateExitCallbacks.Add(callback);
        }

        public void Stop()
        {
            if (_stopped)
                return;

            _stopped = true;

            for (int i = _stateExitCallbacks.Count - 1; i >= 0; i--)
            {
                Action callback = _stateExitCallbacks[i];
                callback?.Invoke();
            }

            _stateExitCallbacks.Clear();

            for (int i = _stateExitInstances.Count - 1; i >= 0; i--)
            {
                GameObject instance = _stateExitInstances[i];
                if (instance != null)
                    UnityObject.Destroy(instance);
            }

            _stateExitInstances.Clear();
        }
    }

    public static class SkillEffectExecutor
    {
        private const float SkillSfxMinDistance = 6f;
        private const float SkillSfxMaxDistance = 30f;
        private const int TopLevelSfxPriority = 96;
        private const int OnHitSfxPriority = 128;
        private static readonly List<Transform> DetectedTargetsScratch = new List<Transform>(16);
        private static readonly HashSet<Transform> DetectedTargetSet = new HashSet<Transform>();
        private static readonly List<Transform> DamageTargetsScratch = new List<Transform>(16);
        private static readonly HashSet<Transform> DamageTargetSet = new HashSet<Transform>();
        private static HitStopRunner _hitStopRunner;
        private static DelayedEffectRunner _delayedEffectRunner;
        public static bool IsCameraLookBlockedByHitStop => _hitStopRunner != null && _hitStopRunner.IsCameraLookBlocked;

        private enum SkillSfxSourceKind
        {
            TopLevel,
            OnHit,
        }

        public static void ExecuteDamageEvent(SkillDamageEvent evt, ISkillExecutionContext ctx)
            => ExecuteDamageEvent(evt, ctx, null);

        public static void ExecuteDamageEvent(SkillDamageEvent evt, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (evt == null || ctx == null)
                return;

            if (evt.damageEffects != null)
            {
                for (int damageIndex = 0; damageIndex < evt.damageEffects.Count; damageIndex++)
                {
                    var damageEffect = evt.damageEffects[damageIndex];
                    if (damageEffect == null)
                        continue;

                    ExecuteDamageEffect(
                        damageEffect,
                        ctx,
                        null,
                        null,
                        null,
                        cueRuntime,
                        null);
                }
            }
        }

        public static bool ExecuteDamageEffect(
            SkillDamageEffect damageEffect,
            ISkillExecutionContext ctx,
            HashSet<Transform> alreadyHitTargets)
        {
            return ExecuteDamageEffect(damageEffect, ctx, alreadyHitTargets, null, null, null, null);
        }

        public static bool ExecuteDamageEffect(
            SkillDamageEffect damageEffect,
            ISkillExecutionContext ctx,
            HashSet<Transform> alreadyHitTargets,
            SkillCollisionHitbox collisionHitbox,
            object collisionToken)
        {
            return ExecuteDamageEffect(damageEffect, ctx, alreadyHitTargets, collisionHitbox, collisionToken, null, null);
        }

        public static bool ExecuteDamageEffect(
            SkillDamageEffect damageEffect,
            ISkillExecutionContext ctx,
            HashSet<Transform> alreadyHitTargets,
            SkillCollisionHitbox collisionHitbox,
            object collisionToken,
            SkillCueRuntimeScope cueRuntime,
            SkillDetectionMotionFrame? motionFrame = null)
        {
            if (damageEffect == null || ctx == null)
                return false;

            DetectedTargetsScratch.Clear();
            DetectedTargetSet.Clear();

            if (!DamageDetectionRunner.TryRunDetection(
                    damageEffect,
                    ctx.CasterTransform,
                    collisionHitbox,
                    collisionToken,
                    ctx.OverlapBuffer,
                    motionFrame,
                    out int hitCount))
            {
                return false;
            }

            DamageTargetsScratch.Clear();
            DamageTargetSet.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                var target = ExtractTargetTransform(ctx.OverlapBuffer[i]);
                if (target == null)
                    continue;

                if (alreadyHitTargets != null && alreadyHitTargets.Contains(target))
                    continue;

                if (DamageTargetSet.Add(target))
                    DamageTargetsScratch.Add(target);

                if (DetectedTargetSet.Add(target))
                    DetectedTargetsScratch.Add(target);
            }

            if (DamageTargetsScratch.Count <= 0)
                return false;

            if (alreadyHitTargets != null)
            {
                for (int i = 0; i < DamageTargetsScratch.Count; i++)
                    alreadyHitTargets.Add(DamageTargetsScratch[i]);
            }

            SkillHitStopEffect hitStopEffect = damageEffect.GetEffectiveHitStopEffect();
            if (SkillDamageEffect.HasConfiguredHitStopEffect(hitStopEffect))
            {
                RequestHitStop(
                    ctx,
                    hitStopEffect.hitStopDuration,
                    hitStopEffect.hitStopTimeScale,
                    hitStopEffect.pauseCameraLookDuringHitStop);
            }

            ApplyOnHitEffects(damageEffect, ctx, DamageTargetsScratch, cueRuntime, motionFrame);
            return true;
        }

        public static void ExecutePhysicsEvent(SkillPhysicsEvent evt, ISkillExecutionContext ctx)
            => ExecutePhysicsEvent(evt, ctx, null);

        public static void ExecutePhysicsEvent(SkillPhysicsEvent evt, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (evt?.physicsEffects == null || ctx == null)
                return;

            for (int i = 0; i < evt.physicsEffects.Count; i++)
                ApplyIndependentPhysicsEffect(evt.physicsEffects[i], ctx, cueRuntime);
        }

        public static void ExecuteAttributeEvent(SkillAttributeEvent evt, ISkillExecutionContext ctx)
            => ExecuteAttributeEvent(evt, ctx, null);

        public static void ExecuteAttributeEvent(SkillAttributeEvent evt, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (evt?.attributeEffects == null || ctx == null)
                return;

            for (int i = 0; i < evt.attributeEffects.Count; i++)
                ApplyIndependentAttributeEffect(evt.attributeEffects[i], ctx, cueRuntime);
        }

        public static void ExecuteVfxEvent(SkillVfxEvent evt, ISkillExecutionContext ctx)
            => ExecuteVfxEvent(evt, ctx, null);

        public static void ExecuteVfxEvent(SkillVfxEvent evt, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (evt == null || ctx?.CasterTransform == null)
                return;

            PlayVfxEffects(evt.vfxEffects, ctx.CasterTransform, ctx.CasterTransform, cueRuntime);
        }

        public static void ExecuteSfxEvent(SkillSfxEvent evt, ISkillExecutionContext ctx)
            => ExecuteSfxEvent(evt, ctx, null);

        public static void ExecuteSfxEvent(SkillSfxEvent evt, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (evt == null || ctx?.CasterTransform == null)
                return;

            PlaySfxEffects(evt.sfxEffects, ctx.CasterTransform, ctx.CasterTransform, cueRuntime, SkillSfxSourceKind.TopLevel);
        }

        private static void ApplyDamageToTarget(
            Transform target,
            ISkillExecutionContext ctx,
            float damageMultiplier,
            float stunDuration)
        {
            if (target == null || ctx == null)
                return;

            float casterAttack = ctx.CasterAttack;
            EnemyController casterEnemy = ctx.CasterTransform != null
                ? ctx.CasterTransform.GetComponent<EnemyController>()
                : null;

            var clone = target.GetComponentInParent<PlayerCloneActor>();
            if (clone != null)
            {
                float damage = CombatCalculator.CalculateDamageFromEnemy(
                    casterAttack,
                    clone.Defense,
                    casterEnemy != null ? casterEnemy.DamageBonus : 0f,
                    clone.DamageReduce) * damageMultiplier;
                clone.OnHit(damage, stunDuration);
                if (casterEnemy != null && damage > 0f)
                    casterEnemy.Heal(damage * casterEnemy.LifeSteal);
                return;
            }

            var player = target.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                float damage = CombatCalculator.CalculateDamageFromEnemy(
                    casterAttack,
                    player.PlayerModel.Stats.Defense,
                    casterEnemy != null ? casterEnemy.DamageBonus : 0f,
                    player.PlayerModel.Stats.DamageReduce) * damageMultiplier;
                player.OnHit(damage, stunDuration);
                if (casterEnemy != null && damage > 0f)
                    casterEnemy.Heal(damage * casterEnemy.LifeSteal);
                return;
            }

            var enemy = target.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                float damage = CalculatePlayerSideDamage(ctx, damageMultiplier, enemy, out bool isCrit);
                enemy.ApplyDamage(damage);
                CombatNumberDispatcher.PublishDamage(enemy.transform, damage, isCrit);
            }
        }

        private static float CalculatePlayerSideDamage(ISkillExecutionContext ctx, float damageMultiplier, EnemyController enemy, out bool isCrit)
        {
            isCrit = false;
            if (ctx == null || enemy == null)
                return 0f;

            PlayerController owningPlayer = PlayerBuffRuntimeUtility.ResolveOwningPlayer(ctx.CasterTransform);
            PlayerCloneActor cloneActor = PlayerBuffRuntimeUtility.ResolveOwningClone(ctx.CasterTransform);
            Stats attackerStats = cloneActor != null
                ? cloneActor.CombatStats
                : owningPlayer?.PlayerModel?.Stats;

            if (attackerStats == null)
            {
                return CombatCalculator.CalculateDamageFromEnemy(
                    ctx.CasterAttack,
                    enemy.Defense,
                    0f,
                    enemy.DamageReduce) * damageMultiplier;
            }

            CombatCalculator.CalculateDamage(
                attackerStats,
                damageMultiplier,
                enemy.Defense,
                () => UnityRandom.value,
                enemy.DamageReduce,
                out float finalDamage,
                out isCrit,
                out float lifeStealHeal);

            if (lifeStealHeal > 0f)
            {
                if (cloneActor != null)
                    ApplyHealToClone(cloneActor, lifeStealHeal, true);
                else if (owningPlayer != null)
                    ApplyHealToPlayer(owningPlayer, lifeStealHeal, true);
            }

            return finalDamage;
        }

        private static void RequestHitStop(ISkillExecutionContext ctx, float duration, float timeScale, bool pauseCameraLookDuringHitStop)
        {
            if (duration <= 0f)
                return;

            float clampedScale = Mathf.Clamp01(timeScale);
            if (clampedScale >= 0.999f)
                return;

            PlayerController player = ctx?.CasterTransform != null
                ? ctx.CasterTransform.GetComponent<PlayerController>()
                : null;
            if (player == null)
                return;

            player.ApplyLocalHitStop(duration, clampedScale);

            if (_hitStopRunner == null)
            {
                var go = new GameObject("[SkillHitStopRunner]");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityObject.DontDestroyOnLoad(go);
                _hitStopRunner = go.AddComponent<HitStopRunner>();
            }

            _hitStopRunner.Apply(duration, pauseCameraLookDuringHitStop);
        }

        private static void ApplyOnHitEffects(
            SkillDamageEffect damageEffect,
            ISkillExecutionContext ctx,
            List<Transform> damageTargets,
            SkillCueRuntimeScope cueRuntime,
            SkillDetectionMotionFrame? motionFrame)
        {
            if (damageEffect == null || ctx == null || damageTargets == null || damageTargets.Count == 0)
                return;

            List<Transform> targetSnapshot = CopyDamageTargets(damageTargets);
            if (targetSnapshot.Count == 0)
                return;

            float stunDuration = 0.2f;
            List<SkillHitDamageEffect> hitDamageEffects = damageEffect.GetEffectiveHitDamageEffects();
            for (int damageIndex = 0; damageIndex < hitDamageEffects.Count; damageIndex++)
            {
                SkillHitDamageEffect hitDamageEffect = hitDamageEffects[damageIndex];
                if (hitDamageEffect == null)
                    continue;

                float finalMultiplier = Mathf.Max(0f, hitDamageEffect.damageMagnitude);
                if (finalMultiplier <= 0f)
                    continue;

                float delay = Mathf.Max(0f, hitDamageEffect.onHitTriggerDelay);
                ScheduleOnHitEffect(delay, () =>
                {
                    for (int i = 0; i < targetSnapshot.Count; i++)
                        ApplyDamageToTarget(targetSnapshot[i], ctx, finalMultiplier, stunDuration);
                });
            }

            if (damageEffect.onHitVfxEffects != null)
            {
                for (int targetIndex = 0; targetIndex < targetSnapshot.Count; targetIndex++)
                {
                    Transform target = targetSnapshot[targetIndex];
                    for (int effectIndex = 0; effectIndex < damageEffect.onHitVfxEffects.Count; effectIndex++)
                    {
                        SkillVfxEffect effect = damageEffect.onHitVfxEffects[effectIndex];
                        if (effect == null || effect.particlePrefab == null)
                            continue;

                        float delay = Mathf.Max(0f, effect.onHitTriggerDelay);
                        ScheduleOnHitEffect(delay, () => PlayVfxEffect(effect, ctx.CasterTransform, target, cueRuntime));
                    }
                }
            }

            if (damageEffect.onHitSfxEffects != null)
            {
                for (int targetIndex = 0; targetIndex < targetSnapshot.Count; targetIndex++)
                {
                    Transform target = targetSnapshot[targetIndex];
                    for (int effectIndex = 0; effectIndex < damageEffect.onHitSfxEffects.Count; effectIndex++)
                    {
                        SkillSfxEffect effect = damageEffect.onHitSfxEffects[effectIndex];
                        if (effect == null || effect.audioClip == null)
                            continue;

                        float delay = Mathf.Max(0f, effect.onHitTriggerDelay);
                        ScheduleOnHitEffect(delay, () => PlaySfxEffect(effect, ctx.CasterTransform, target, cueRuntime, SkillSfxSourceKind.OnHit));
                    }
                }
            }

            if (damageEffect.onHitPhysicsEffects != null)
            {
                TryResolveDamageEffectOrigin(damageEffect, ctx.CasterTransform, motionFrame, out Vector3 effectOrigin);
                for (int i = 0; i < damageEffect.onHitPhysicsEffects.Count; i++)
                {
                    SkillPhysicsEffect effect = damageEffect.onHitPhysicsEffects[i];
                    if (effect == null)
                        continue;

                    float delay = Mathf.Max(0f, effect.onHitTriggerDelay);
                    ScheduleOnHitEffect(delay, () => ApplyPhysicsEffect(effect, ctx, targetSnapshot, damageEffect, effectOrigin));
                }
            }

            if (damageEffect.onHitAttributeEffects != null)
            {
                for (int i = 0; i < damageEffect.onHitAttributeEffects.Count; i++)
                {
                    SkillAttributeEffect effect = damageEffect.onHitAttributeEffects[i];
                    if (effect == null)
                        continue;

                    float delay = Mathf.Max(0f, effect.onHitTriggerDelay);
                    ScheduleOnHitEffect(delay, () => ApplyAttributeEffect(effect, ctx, targetSnapshot));
                }
            }
        }

        private static List<Transform> CopyDamageTargets(List<Transform> damageTargets)
        {
            var targets = new List<Transform>(damageTargets.Count);
            for (int i = 0; i < damageTargets.Count; i++)
            {
                Transform target = damageTargets[i];
                if (target != null)
                    targets.Add(target);
            }

            return targets;
        }

        private static void ScheduleOnHitEffect(float delay, Action action)
        {
            if (action == null)
                return;

            if (delay <= 0f)
            {
                action.Invoke();
                return;
            }

            if (_delayedEffectRunner == null)
            {
                var go = new GameObject("[SkillDelayedEffectRunner]");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityObject.DontDestroyOnLoad(go);
                _delayedEffectRunner = go.AddComponent<DelayedEffectRunner>();
            }

            _delayedEffectRunner.Schedule(delay, action);
        }

        private static void ApplyIndependentPhysicsEffect(SkillPhysicsEffect effect, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (effect == null || ctx?.CasterTransform == null)
                return;

            if (effect.effectType == PhysicsEffectType.DashSelf)
            {
                ApplySelfDisplacement(effect, ctx);
                return;
            }

            if (effect.effectType == PhysicsEffectType.Airborne)
            {
                ApplySelfAirborne(effect, ctx);
                return;
            }

            if (effect.effectType == PhysicsEffectType.SuperArmor)
            {
                ApplySelfSuperArmor(effect, ctx, cueRuntime);
                return;
            }

            if (effect.effectType == PhysicsEffectType.Invincible)
            {
                ApplySelfInvincibility(effect, ctx, cueRuntime);
                return;
            }

        }

        private static void ApplyIndependentAttributeEffect(SkillAttributeEffect effect, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (effect == null || ctx?.CasterTransform == null)
                return;

            ApplyAttributeToTarget(ctx.CasterTransform, effect, cueRuntime);
        }

        private static void ApplyPhysicsEffect(
            SkillPhysicsEffect effect,
            ISkillExecutionContext ctx,
            List<Transform> detectedTargets,
            SkillDamageEffect damageEffect,
            Vector3 effectOrigin)
        {
            if (effect == null)
                return;

            if (effect.effectType == PhysicsEffectType.DashSelf || effect.effectType == PhysicsEffectType.Airborne)
                return;

            for (int i = 0; i < detectedTargets.Count; i++)
            {
                Transform target = detectedTargets[i];
                if (target == null)
                    continue;

                ApplyTargetPhysicsEffect(target, effect, ctx, damageEffect, effectOrigin);
            }
        }

        private static void ApplySelfDisplacement(SkillPhysicsEffect effect, ISkillExecutionContext ctx)
        {
            if (effect.distance <= 0f || ctx.CasterTransform == null)
                return;

            float duration = effect.duration > 0f ? effect.duration : 0.12f;
            Vector3 direction = ctx.CasterTransform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            var motion = ctx.CasterTransform.GetComponent<CombatMotionController>()
                         ?? ctx.CasterTransform.gameObject.AddComponent<CombatMotionController>();
            motion.ApplyDisplacement(direction.normalized * effect.distance, duration);
        }

        private static void ApplySelfAirborne(SkillPhysicsEffect effect, ISkillExecutionContext ctx)
        {
            if (effect.height <= 0f || ctx.CasterTransform == null)
                return;

            PlayerMover playerMover = ctx.CasterTransform.GetComponent<PlayerMover>();
            if (playerMover != null)
            {
                playerMover.Jump(effect.height);
                return;
            }

            float duration = effect.duration > 0f ? effect.duration : 0.12f;
            var motion = ctx.CasterTransform.GetComponent<CombatMotionController>()
                         ?? ctx.CasterTransform.gameObject.AddComponent<CombatMotionController>();
            motion.ApplyDisplacement(Vector3.up * effect.height, duration);
        }

        private static void ApplySelfSuperArmor(SkillPhysicsEffect effect, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (ctx.CasterTransform == null)
                return;

            PlayerController player = ctx.CasterTransform.GetComponent<PlayerController>();
            if (effect.durationMode == SkillEffectDurationMode.UntilStateExit && effect.SupportsUntilStateExitDuration && cueRuntime != null)
            {
                if (player != null)
                {
                    player.AddStateScopedSuperArmor();
                    cueRuntime.RegisterStateExitCallback(() =>
                    {
                        if (player != null)
                            player.RemoveStateScopedSuperArmor();
                    });
                    return;
                }

                PlayerCloneActor stateScopedClone = ctx.CasterTransform.GetComponent<PlayerCloneActor>();
                if (stateScopedClone != null)
                {
                    stateScopedClone.AddStateScopedSuperArmor();
                    cueRuntime.RegisterStateExitCallback(() =>
                    {
                        if (stateScopedClone != null)
                            stateScopedClone.RemoveStateScopedSuperArmor();
                    });
                }
                return;
            }

            if (effect.duration <= 0f)
                return;

            if (player != null)
            {
                player.ApplyTemporarySuperArmor(effect.duration);
                return;
            }

            PlayerCloneActor clone = ctx.CasterTransform.GetComponent<PlayerCloneActor>();
            clone?.ApplyTemporarySuperArmor(effect.duration);
        }

        private static void ApplySelfInvincibility(SkillPhysicsEffect effect, ISkillExecutionContext ctx, SkillCueRuntimeScope cueRuntime)
        {
            if (ctx.CasterTransform == null)
                return;

            PlayerController player = ctx.CasterTransform.GetComponent<PlayerController>();
            if (effect.durationMode == SkillEffectDurationMode.UntilStateExit && effect.SupportsUntilStateExitDuration && cueRuntime != null)
            {
                if (player != null)
                {
                    player.AddStateScopedInvincibility();
                    cueRuntime.RegisterStateExitCallback(() =>
                    {
                        if (player != null)
                            player.RemoveStateScopedInvincibility();
                    });
                    return;
                }

                PlayerCloneActor stateScopedClone = ctx.CasterTransform.GetComponent<PlayerCloneActor>();
                if (stateScopedClone != null)
                {
                    stateScopedClone.AddStateScopedInvincibility();
                    cueRuntime.RegisterStateExitCallback(() =>
                    {
                        if (stateScopedClone != null)
                            stateScopedClone.RemoveStateScopedInvincibility();
                    });
                }
                return;
            }

            if (effect.duration <= 0f)
                return;

            if (player != null)
            {
                player.ApplyTemporaryInvincibility(effect.duration);
                return;
            }

            PlayerCloneActor clone = ctx.CasterTransform.GetComponent<PlayerCloneActor>();
            clone?.ApplyTemporaryInvincibility(effect.duration);
        }

        private static void ApplyTargetPhysicsEffect(
            Transform target,
            SkillPhysicsEffect effect,
            ISkillExecutionContext ctx,
            SkillDamageEffect damageEffect,
            Vector3 effectOrigin)
        {
            if (target == null)
                return;

            float duration = effect.duration > 0f
                ? effect.duration
                : 0.12f;

            if (effect.effectType == PhysicsEffectType.Stun)
            {
                ICombatHardControlReceiver hardControl =
                    target.GetComponentInParent<PlayerController>() as ICombatHardControlReceiver
                    ?? target.GetComponentInParent<PlayerCloneActor>() as ICombatHardControlReceiver
                    ?? target.GetComponentInParent<EnemyController>() as ICombatHardControlReceiver;
                hardControl?.ApplyHardControl(duration);

                var stun = target.GetComponent<CombatMotionController>()
                           ?? target.gameObject.AddComponent<CombatMotionController>();
                stun.HoldStill(duration);
                return;
            }

            var motion = target.GetComponent<CombatMotionController>()
                         ?? target.gameObject.AddComponent<CombatMotionController>();
            Vector3 horizontal = ResolveHorizontalTargetDirection(ctx.CasterTransform, target, effect.effectType, effectOrigin);
            Vector3 displacement = Vector3.zero;

            if (effect.effectType == PhysicsEffectType.Knockback)
                displacement = horizontal * effect.distance;
            else if (effect.effectType == PhysicsEffectType.Pull)
                displacement = horizontal * effect.distance;
            else if (effect.effectType == PhysicsEffectType.Launch)
                displacement = horizontal * effect.distance + Vector3.up * effect.height;

            if (displacement.sqrMagnitude <= 0.0001f)
                return;

            motion.ApplyDisplacement(displacement, duration);
        }

        private static void ApplyAttributeEffect(
            SkillAttributeEffect effect,
            ISkillExecutionContext ctx,
            List<Transform> detectedTargets)
        {
            if (effect == null)
                return;

            var targets = ResolveTargets(effect.targetMode, ctx, detectedTargets);
            for (int i = 0; i < targets.Count; i++)
                ApplyAttributeToTarget(targets[i], effect, null);
        }

        private static void ApplyAttributeToTarget(Transform target, SkillAttributeEffect effect, SkillCueRuntimeScope cueRuntime)
        {
            if (target == null)
                return;

            var clone = target.GetComponentInParent<PlayerCloneActor>();
            if (clone != null)
            {
                ApplyStatFieldToClone(clone, effect.statField, effect.magnitude, effect.durationMode, effect.duration, effect.usePercent, cueRuntime);
                return;
            }

            var player = target.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                ApplyStatFieldToPlayer(player, effect.statField, effect.magnitude, effect.durationMode, effect.duration, effect.usePercent, cueRuntime);
                return;
            }

            var enemy = target.GetComponentInParent<EnemyController>();
            if (enemy != null)
                ApplyStatFieldToEnemy(enemy, effect.statField, effect.magnitude, effect.durationMode, effect.duration, effect.usePercent, cueRuntime);
        }

        private static void ApplyStatFieldToPlayer(
            PlayerController player,
            SkillStatField field,
            float magnitude,
            SkillEffectDurationMode durationMode,
            float duration,
            bool usePercent,
            SkillCueRuntimeScope cueRuntime)
        {
            var model = player.PlayerModel;
            if (model == null)
                return;

            float resolvedMagnitude = ResolvePlayerStatMagnitude(model, field, magnitude, usePercent);
            if (field == SkillStatField.HP)
            {
                if (resolvedMagnitude > 0f)
                    ApplyHealToPlayer(player, resolvedMagnitude, true);
                return;
            }

            if (field == SkillStatField.MP)
            {
                if (resolvedMagnitude > 0f)
                    ApplyManaToPlayer(player, resolvedMagnitude, true);
                return;
            }

            var modifier = BuildModifierForField(field, resolvedMagnitude);
            if (modifier == null)
                return;

            var runtime = player.GetComponent<PlayerRuntimeStatModifierController>()
                          ?? player.gameObject.AddComponent<PlayerRuntimeStatModifierController>();

            if (durationMode == SkillEffectDurationMode.UntilStateExit && cueRuntime != null)
            {
                runtime.ApplyModifierUntilStateExit(modifier, cueRuntime);
            }
            else if (duration > 0f)
            {
                runtime.ApplyModifier(modifier, duration);
            }
            else
            {
                model.AddStatModifierAndSyncVitals(modifier);
            }
        }

        private static void ApplyStatFieldToClone(
            PlayerCloneActor clone,
            SkillStatField field,
            float magnitude,
            SkillEffectDurationMode durationMode,
            float duration,
            bool usePercent,
            SkillCueRuntimeScope cueRuntime)
        {
            if (clone == null)
                return;

            if (field == SkillStatField.HP)
            {
                float amount = usePercent ? clone.MaxHp * magnitude : magnitude;
                if (amount > 0f)
                    ApplyHealToClone(clone, amount, true);
                return;
            }

            if (field == SkillStatField.MP)
            {
                float amount = usePercent ? clone.MaxMp * magnitude : magnitude;
                if (amount > 0f)
                    ApplyManaToClone(clone, amount, false);
                return;
            }

            var modifier = BuildModifierForField(field, magnitude);
            if (modifier == null)
                return;

            if (durationMode == SkillEffectDurationMode.UntilStateExit && cueRuntime != null)
                clone.ApplyStatModifierUntilStateExit(modifier, cueRuntime);
            else
                clone.ApplyStatModifier(modifier, duration);
        }

        private static void ApplyStatFieldToEnemy(
            EnemyController enemy,
            SkillStatField field,
            float magnitude,
            SkillEffectDurationMode durationMode,
            float duration,
            bool usePercent,
            SkillCueRuntimeScope cueRuntime)
        {
            if (field == SkillStatField.HP)
            {
                float amount = usePercent ? enemy.MaxHp * magnitude : magnitude;
                if (amount > 0f)
                    enemy.Heal(amount);
                return;
            }

            var modifier = BuildModifierForField(field, magnitude);
            if (modifier == null)
                return;

            if (durationMode == SkillEffectDurationMode.UntilStateExit && cueRuntime != null)
                enemy.ApplyStatModifierUntilStateExit(modifier, cueRuntime);
            else if (duration > 0f)
                enemy.ApplyTimedStatModifier(modifier, duration);
            else
                enemy.ApplyTimedStatModifier(modifier, float.MaxValue);
        }

        private static void ApplyHealToPlayer(PlayerController player, float amount, bool showNumber)
        {
            if (player?.PlayerModel == null || amount <= 0f)
                return;

            float before = player.PlayerModel.CurrentHp;
            player.PlayerModel.Heal(amount);
            float applied = player.PlayerModel.CurrentHp - before;
            if (showNumber && applied > 0f)
                CombatNumberDispatcher.PublishHeal(player.transform, applied);
        }

        private static void ApplyManaToPlayer(PlayerController player, float amount, bool showNumber)
        {
            if (player?.PlayerModel == null || amount <= 0f)
                return;

            float before = player.PlayerModel.CurrentMp;
            player.PlayerModel.CurrentMp = Mathf.Min(player.PlayerModel.Stats.MaxMp, player.PlayerModel.CurrentMp + amount);
            float applied = player.PlayerModel.CurrentMp - before;
            if (showNumber && applied > 0f)
                CombatNumberDispatcher.PublishMana(player.transform, applied);
        }

        private static void ApplyHealToClone(PlayerCloneActor clone, float amount, bool showNumber)
        {
            if (clone == null || amount <= 0f)
                return;

            float before = clone.CurrentHp;
            clone.Heal(amount);
            float applied = clone.CurrentHp - before;
            if (showNumber && applied > 0f)
                CombatNumberDispatcher.PublishHeal(clone.transform, applied);
        }

        private static void ApplyManaToClone(PlayerCloneActor clone, float amount, bool showNumber)
        {
            if (clone == null || amount <= 0f)
                return;

            float before = clone.CurrentMp;
            clone.RestoreMp(amount);
            float applied = clone.CurrentMp - before;
            if (showNumber && applied > 0f)
                CombatNumberDispatcher.PublishMana(clone.transform, applied);
        }

        private static void PlayVfxEffects(List<SkillVfxEffect> effects, Transform caster, Transform target, SkillCueRuntimeScope cueRuntime)
        {
            if (effects == null || caster == null)
                return;

            for (int i = 0; i < effects.Count; i++)
                PlayVfxEffect(effects[i], caster, target, cueRuntime);
        }

        private static void PlayVfxEffect(SkillVfxEffect effect, Transform caster, Transform target, SkillCueRuntimeScope cueRuntime)
        {
            if (effect == null || effect.particlePrefab == null || caster == null)
                return;

            Transform anchor = ResolveAnchorTransform(effect.anchor, caster, target);
            GameObject instance = CreateVfxInstance(effect, caster, anchor);
            RegisterCueLifetime(instance, effect.destroyMode, effect.duration, cueRuntime);
        }

        private static void PlaySfxEffects(
            List<SkillSfxEffect> effects,
            Transform caster,
            Transform target,
            SkillCueRuntimeScope cueRuntime,
            SkillSfxSourceKind sourceKind)
        {
            if (effects == null || caster == null)
                return;

            for (int i = 0; i < effects.Count; i++)
                PlaySfxEffect(effects[i], caster, target, cueRuntime, sourceKind);
        }

        private static void PlaySfxEffect(
            SkillSfxEffect effect,
            Transform caster,
            Transform target,
            SkillCueRuntimeScope cueRuntime,
            SkillSfxSourceKind sourceKind)
        {
            if (effect == null || effect.audioClip == null || caster == null)
                return;

            Transform anchor = ResolveAnchorTransform(effect.anchor, caster, target);
            CreateSfxInstance(effect, caster, anchor, cueRuntime, sourceKind);
        }

        private static GameObject CreateVfxInstance(SkillVfxEffect effect, Transform caster, Transform anchor)
        {
            if (effect == null || effect.particlePrefab == null)
                return null;

            float rangeScale = PlayerBuffRuntimeUtility.GetDamageRangeScale(caster);
            bool useWorldMotion = ShouldUseWorldCueMotion(effect);
            bool followAnchor = effect.anchor != CueAnchor.World && anchor != null;
            GameObject instance;
            if (followAnchor)
            {
                instance = UnityObject.Instantiate(effect.particlePrefab, anchor);
                instance.transform.localPosition = effect.offset;
                instance.transform.localRotation = Quaternion.Euler(effect.rotationEuler);
            }
            else
            {
                Vector3 position = ResolveCueSpawnPosition(effect, caster, anchor);
                Quaternion rotation = ResolveCueSpawnRotation(effect, caster, anchor);
                instance = UnityObject.Instantiate(effect.particlePrefab, position, rotation);
            }

            SanitizeSpawnedVfxInstance(instance);
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, effect.scale * rangeScale);
            ConfigureCueFollowDuration(instance, effect, followAnchor);
            ConfigureVfxMotion(instance, effect.motion, useWorldMotion);
            ApplyCueMotion(instance, effect, caster, useWorldMotion);
            EnsureCuePauseProxy(instance);
            return instance;
        }

        private static void ConfigureCueFollowDuration(GameObject instance, SkillVfxEffect effect, bool followAnchor)
        {
            if (instance == null || effect == null || !followAnchor || !effect.useFollowDuration)
                return;

            SkillCueFollowDetachProxy detachProxy = instance.GetComponent<SkillCueFollowDetachProxy>();
            if (detachProxy == null)
                detachProxy = instance.AddComponent<SkillCueFollowDetachProxy>();

            detachProxy.Configure(effect.followDuration);
        }

        private static void SanitizeSpawnedVfxInstance(GameObject instance)
        {
            if (instance == null)
                return;

            ProjectileMover[] projectileMovers = instance.GetComponentsInChildren<ProjectileMover>(true);
            for (int i = 0; i < projectileMovers.Length; i++)
            {
                ProjectileMover projectileMover = projectileMovers[i];
                if (projectileMover != null)
                    projectileMover.enabled = false;
            }

            Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rigidbody = rigidbodies[i];
                if (rigidbody == null)
                    continue;

                rigidbody.velocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = false;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null)
                    collider.enabled = false;
            }
        }

        private static void CreateSfxInstance(
            SkillSfxEffect effect,
            Transform caster,
            Transform anchor,
            SkillCueRuntimeScope cueRuntime,
            SkillSfxSourceKind sourceKind)
        {
            if (effect == null || effect.audioClip == null)
                return;

            var go = new GameObject($"[SkillSfx]{effect.audioClip.name}");
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = SkillSfxMinDistance;
            audioSource.maxDistance = SkillSfxMaxDistance;
            audioSource.clip = effect.audioClip;
            audioSource.loop = effect.loop;
            audioSource.priority = ResolveSfxPriority(sourceKind);

            if (anchor != null)
            {
                go.transform.SetParent(anchor, false);
                go.transform.localPosition = effect.offset;
            }
            else
            {
                go.transform.position = anchor != null ? anchor.TransformPoint(effect.offset) : caster.position + effect.offset;
            }

            audioSource.Play();
            EnsureCuePauseProxy(go);
            RegisterAudioLifetime(go, audioSource, effect, cueRuntime);
        }

        private static int ResolveSfxPriority(SkillSfxSourceKind sourceKind)
        {
            return sourceKind == SkillSfxSourceKind.OnHit
                ? OnHitSfxPriority
                : TopLevelSfxPriority;
        }

        private static void RegisterCueLifetime(GameObject instance, SkillCueDestroyMode destroyMode, float duration, SkillCueRuntimeScope cueRuntime)
        {
            if (instance == null)
                return;

            switch (destroyMode)
            {
                case SkillCueDestroyMode.Timed:
                    if (duration > 0f)
                        UnityObject.Destroy(instance, duration);
                    break;
                case SkillCueDestroyMode.OnStateExit:
                    cueRuntime?.RegisterStateExitInstance(instance);
                    break;
            }
        }

        private static void RegisterAudioLifetime(GameObject instance, AudioSource audioSource, SkillSfxEffect effect, SkillCueRuntimeScope cueRuntime)
        {
            if (instance == null || audioSource == null || effect == null)
                return;

            SkillCueDestroyMode destroyMode = effect.destroyMode;
            if (effect.loop && destroyMode == SkillCueDestroyMode.NaturalDestroy)
                destroyMode = SkillCueDestroyMode.OnStateExit;

            switch (destroyMode)
            {
                case SkillCueDestroyMode.OnStateExit:
                    if (cueRuntime != null)
                    {
                        cueRuntime.RegisterStateExitInstance(instance);
                        return;
                    }
                    break;
                case SkillCueDestroyMode.Timed:
                    if (effect.duration > 0f)
                    {
                        UnityObject.Destroy(instance, effect.duration);
                        return;
                    }
                    break;
            }

            float clipDuration = effect.audioClip != null ? effect.audioClip.length : 0f;
            if (clipDuration > 0f && !effect.loop)
                UnityObject.Destroy(instance, clipDuration);
            else if (!effect.loop)
                UnityObject.Destroy(instance, 0.1f);
        }

        private static void ApplyCueMotion(GameObject instance, SkillVfxEffect effect, Transform caster, bool useWorldMotion)
        {
            SkillMotionSettings motion = effect != null ? effect.motion : null;
            if (instance == null || caster == null || motion == null || !useWorldMotion || !motion.IsActive)
                return;

            Vector3 originPosition = instance.transform.position;
            Quaternion originRotation = caster.rotation;
            Quaternion lockedRotation = instance.transform.rotation;

            SkillCueMover mover = instance.GetComponent<SkillCueMover>();
            if (mover == null)
                mover = instance.AddComponent<SkillCueMover>();
            mover.Initialize(originPosition, originRotation, motion, false, true, lockedRotation, caster, effect != null ? effect.offset : Vector3.zero);
        }

        private static void EnsureCuePauseProxy(GameObject instance)
        {
            if (instance == null || instance.GetComponent<SkillCuePauseProxy>() != null)
                return;

            instance.AddComponent<SkillCuePauseProxy>();
        }

        private static void ConfigureVfxMotion(GameObject instance, SkillMotionSettings motion, bool useWorldMotion)
        {
            if (instance == null || motion == null || !useWorldMotion || !motion.IsActive)
                return;

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                var main = particleSystem.main;
                if (main.simulationSpace == ParticleSystemSimulationSpace.World)
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        private static bool ShouldUseWorldCueMotion(SkillVfxEffect effect)
        {
            return effect != null
                   && effect.anchor == CueAnchor.World
                   && effect.motion != null
                   && effect.motion.IsActive;
        }

        private static Vector3 ResolveCueSpawnPosition(SkillVfxEffect effect, Transform caster, Transform anchor)
        {
            Transform basis = anchor != null ? anchor : caster;
            if (basis != null)
                return basis.TransformPoint(effect.offset);

            return effect.offset;
        }

        private static Quaternion ResolveCueSpawnRotation(SkillVfxEffect effect, Transform caster, Transform anchor)
        {
            Transform basis = anchor != null ? anchor : caster;
            Quaternion basisRotation = basis != null ? basis.rotation : Quaternion.identity;
            return basisRotation * Quaternion.Euler(effect.rotationEuler);
        }

        private static List<Transform> ResolveTargets(
            SkillTargetMode mode,
            ISkillExecutionContext ctx,
            List<Transform> detectedTargets)
        {
            var result = new List<Transform>(4);

            switch (mode)
            {
                case SkillTargetMode.Self:
                    if (ctx.CasterTransform != null)
                        result.Add(ctx.CasterTransform);
                    break;
                default:
                    result.AddRange(detectedTargets);
                    break;
            }

            return result;
        }

        private static Vector3 ResolveHorizontalTargetDirection(
            Transform caster,
            Transform target,
            PhysicsEffectType effectType,
            Vector3 effectOrigin)
        {
            if (target == null)
                return Vector3.zero;

            Vector3 sourcePosition = effectType == PhysicsEffectType.Pull
                ? effectOrigin
                : caster != null ? caster.position : effectOrigin;

            Vector3 vector = target.position - sourcePosition;
            vector.y = 0f;
            if (vector.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 away = vector.normalized;
            if (effectType == PhysicsEffectType.Pull)
                return -away;

            return away;
        }

        private static bool TryResolveDamageEffectOrigin(
            SkillDamageEffect damageEffect,
            Transform caster,
            SkillDetectionMotionFrame? motionFrame,
            out Vector3 origin)
        {
            origin = Vector3.zero;
            if (damageEffect == null)
                return false;

            Vector3 basePosition = caster != null ? caster.position : Vector3.zero;
            Quaternion baseRotation = caster != null ? caster.rotation : Quaternion.identity;
            Vector3 motionOffset = Vector3.zero;

            if (damageEffect.anchor == SkillDamageAnchor.World && motionFrame.HasValue)
            {
                Vector3 localOriginOffset = damageEffect.detectionType switch
                {
                    DamageDetectionType.Raycast => damageEffect.rayOriginOffset,
                    DamageDetectionType.RangeOverlap => damageEffect.centerOffset,
                    _ => Vector3.zero,
                };
                Vector3 originWorld = motionFrame.Value.OriginPosition + motionFrame.Value.OriginRotation * localOriginOffset;
                Vector3 retargetWorld = caster != null
                    ? caster.TransformPoint(localOriginOffset)
                    : originWorld;
                if (damageEffect.motion != null
                    && damageEffect.motion.IsActive
                    && damageEffect.motion.TryEvaluateRetargetedWorldPosition(originWorld, motionFrame.Value.OriginRotation, retargetWorld, motionFrame.Value.Elapsed, out origin))
                {
                    return true;
                }

                basePosition = motionFrame.Value.OriginPosition;
                baseRotation = motionFrame.Value.OriginRotation;
                if (damageEffect.motion != null && damageEffect.motion.IsActive)
                    motionOffset = damageEffect.motion.EvaluateLocalDisplacement(motionFrame.Value.Elapsed);
            }

            switch (damageEffect.detectionType)
            {
                case DamageDetectionType.RangeOverlap:
                    origin = basePosition + baseRotation * (damageEffect.centerOffset + motionOffset);
                    return true;
                case DamageDetectionType.Raycast:
                    origin = basePosition + baseRotation * (damageEffect.rayOriginOffset + motionOffset);
                    return true;
                default:
                    origin = basePosition;
                    return caster != null;
            }
        }

        private static Transform ResolveAnchorTransform(CueAnchor anchor, Transform caster, Transform target)
        {
            return anchor switch
            {
                CueAnchor.Target => target,
                CueAnchor.World => null,
                _ => caster,
            };
        }

        private static Transform ExtractTargetTransform(Collider collider)
        {
            if (collider == null)
                return null;

            var clone = collider.GetComponentInParent<PlayerCloneActor>();
            if (clone != null)
                return clone.transform;

            var player = collider.GetComponentInParent<PlayerController>();
            if (player != null)
                return player.transform;

            var enemy = collider.GetComponentInParent<EnemyController>();
            if (enemy != null)
                return enemy.transform;

            return collider.transform;
        }

        private static float ResolvePlayerStatMagnitude(PlayerModel model, SkillStatField field, float magnitude, bool usePercent)
        {
            if (!usePercent || model == null)
                return magnitude;

            return GetPlayerStatPercentBase(model, field) * magnitude;
        }

        private static float GetPlayerStatPercentBase(PlayerModel model, SkillStatField field)
        {
            Stats stats = model?.Stats;
            if (stats == null)
                return 0f;

            switch (field)
            {
                case SkillStatField.HP:
                    return Mathf.Max(0f, stats.baseHp);
                case SkillStatField.MP:
                    return Mathf.Max(0f, stats.baseMp);
                case SkillStatField.Attack:
                    return Mathf.Max(0f, stats.baseAttack);
                case SkillStatField.Defense:
                    return Mathf.Max(0f, stats.baseDefense);
                case SkillStatField.HPRegen:
                    return Mathf.Max(0f, stats.baseHpRegen);
                case SkillStatField.MPRegen:
                    return Mathf.Max(0f, stats.baseMpRegen);
                case SkillStatField.CritRate:
                    return Mathf.Max(0f, stats.baseCritRate);
                case SkillStatField.CritDamage:
                    return Mathf.Max(0f, stats.baseCritDmg);
                case SkillStatField.MoveSpeed:
                case SkillStatField.AttackSpeed:
                case SkillStatField.SkillDamage:
                case SkillStatField.DamageReduce:
                case SkillStatField.LifeSteal:
                    return 1f;
                default:
                    return 0f;
            }
        }

        private static StatModifier BuildModifierForField(SkillStatField field, float magnitude)
        {
            var modifier = new StatModifier();
            switch (field)
            {
                case SkillStatField.Attack:
                    modifier.attackAdd = magnitude;
                    break;
                case SkillStatField.Defense:
                    modifier.defenseAdd = magnitude;
                    break;
                case SkillStatField.MoveSpeed:
                    modifier.moveSpeedAdd = magnitude;
                    break;
                case SkillStatField.HPRegen:
                    modifier.hpRegenAdd = magnitude;
                    break;
                case SkillStatField.MPRegen:
                    modifier.mpRegenAdd = magnitude;
                    break;
                case SkillStatField.CritRate:
                    modifier.critRateAdd = magnitude;
                    break;
                case SkillStatField.CritDamage:
                    modifier.critDmgAdd = magnitude;
                    break;
                case SkillStatField.AttackSpeed:
                    modifier.attackSpeedAdd = magnitude;
                    break;
                case SkillStatField.SkillDamage:
                    modifier.damageBonusAdd = magnitude;
                    break;
                case SkillStatField.DamageReduce:
                    modifier.damageReduceAdd = magnitude;
                    break;
                case SkillStatField.LifeSteal:
                    modifier.lifeStealAdd = magnitude;
                    break;
                default:
                    return null;
            }

            return modifier;
        }

        private sealed class HitStopRunner : MonoBehaviour
        {
            private bool _active;
            private float _remainingUnscaled;
            private bool _pauseCameraLookDuringHitStop;

            public bool IsActive => _active;
            public bool IsCameraLookBlocked => _active && _pauseCameraLookDuringHitStop;

            public void Apply(float duration, bool pauseCameraLookDuringHitStop)
            {
                if (duration <= 0f)
                    return;

                if (!_active)
                {
                    _remainingUnscaled = duration;
                    _pauseCameraLookDuringHitStop = pauseCameraLookDuringHitStop;
                    _active = true;
                    return;
                }

                _remainingUnscaled = Mathf.Max(_remainingUnscaled, duration);
                _pauseCameraLookDuringHitStop |= pauseCameraLookDuringHitStop;
            }

            private void Update()
            {
                if (!_active)
                    return;

                _remainingUnscaled -= Time.unscaledDeltaTime;
                if (_remainingUnscaled > 0f)
                    return;

                _active = false;
                _pauseCameraLookDuringHitStop = false;
            }
        }

        private sealed class DelayedEffectRunner : MonoBehaviour
        {
            public void Schedule(float delay, Action action)
            {
                if (action == null)
                    return;

                StartCoroutine(Run(delay, action));
            }

            private static IEnumerator Run(float delay, Action action)
            {
                if (delay > 0f)
                    yield return new WaitForSeconds(delay);

                action?.Invoke();
            }
        }

    }
}
