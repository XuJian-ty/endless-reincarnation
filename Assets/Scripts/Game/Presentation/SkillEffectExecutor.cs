using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Domain;

namespace Game.Presentation
{
    public static class SkillEffectExecutor
    {
        private static readonly List<Transform> DetectedTargetsScratch = new List<Transform>(16);
        private static readonly HashSet<Transform> DetectedTargetSet = new HashSet<Transform>();
        private static readonly List<Transform> DamageTargetsScratch = new List<Transform>(16);
        private static readonly HashSet<Transform> DamageTargetSet = new HashSet<Transform>();
        private static HitStopRunner _hitStopRunner;

        public static void ExecuteDamageEvent(SkillDamageEvent evt, ISkillExecutionContext ctx)
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
                    ExecuteDamageEffect(damageEffect, ctx, null);
                }
            }
        }

        public static void ExecuteDamageEffect(
            SkillDamageEffect damageEffect,
            ISkillExecutionContext ctx,
            HashSet<Transform> alreadyHitTargets)
        {
            ExecuteDamageEffect(damageEffect, ctx, alreadyHitTargets, null, null);
        }

        public static void ExecuteDamageEffect(
            SkillDamageEffect damageEffect,
            ISkillExecutionContext ctx,
            HashSet<Transform> alreadyHitTargets,
            SkillCollisionHitbox collisionHitbox,
            object collisionToken)
        {
            if (damageEffect == null || ctx == null)
                return;

            DetectedTargetsScratch.Clear();
            DetectedTargetSet.Clear();

            if (!DamageDetectionRunner.TryRunDetection(
                    damageEffect,
                    ctx.CasterTransform,
                    collisionHitbox,
                    collisionToken,
                    ctx.OverlapBuffer,
                    out int hitCount))
            {
                return;
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
                return;

            if (alreadyHitTargets != null)
            {
                for (int i = 0; i < DamageTargetsScratch.Count; i++)
                    alreadyHitTargets.Add(DamageTargetsScratch[i]);
            }

            float finalMultiplier = Mathf.Max(0f, damageEffect.damageMagnitude);
            float stunDuration = damageEffect.pushDuration > 0.01f
                ? damageEffect.pushDuration
                : 0.2f;

            if (finalMultiplier > 0f)
            {
                for (int i = 0; i < DamageTargetsScratch.Count; i++)
                    ApplyDamageToTarget(DamageTargetsScratch[i], ctx, finalMultiplier, stunDuration);
            }

            RequestHitStop(damageEffect.hitStopDuration, damageEffect.hitStopTimeScale);
            ApplyOnHitEffects(damageEffect, ctx, DamageTargetsScratch);
        }

        public static void ExecutePhysicsEvent(SkillPhysicsEvent evt, ISkillExecutionContext ctx)
        {
            if (evt?.physicsEffects == null || ctx == null)
                return;

            for (int i = 0; i < evt.physicsEffects.Count; i++)
                ApplyIndependentPhysicsEffect(evt.physicsEffects[i], ctx);
        }

        public static void ExecuteAttributeEvent(SkillAttributeEvent evt, ISkillExecutionContext ctx)
        {
            if (evt?.attributeEffects == null || ctx == null)
                return;

            for (int i = 0; i < evt.attributeEffects.Count; i++)
                ApplyIndependentAttributeEffect(evt.attributeEffects[i], ctx);
        }

        public static void ExecuteVfxEvent(SkillVfxEvent evt, ISkillExecutionContext ctx)
        {
            if (evt == null || ctx?.CasterTransform == null)
                return;

            PlayVfxEffects(evt.vfxEffects, ctx.CasterTransform, ctx.CasterTransform);
        }

        public static void ExecuteSfxEvent(SkillSfxEvent evt, ISkillExecutionContext ctx)
        {
            if (evt == null || ctx?.CasterTransform == null)
                return;

            PlaySfxEffects(evt.sfxEffects, ctx.CasterTransform, ctx.CasterTransform);
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
                float damage = CombatCalculator.CalculateDamageFromEnemy(
                    casterAttack,
                    enemy.Defense,
                    0f,
                    enemy.DamageReduce) * damageMultiplier;
                enemy.ApplyDamage(damage);
            }
        }

        private static void RequestHitStop(float duration, float timeScale)
        {
            if (duration <= 0f)
                return;

            float clampedScale = Mathf.Clamp01(timeScale);
            if (clampedScale >= 0.999f || Time.timeScale <= 0f)
                return;

            if (_hitStopRunner == null)
            {
                var go = new GameObject("[SkillHitStopRunner]");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                _hitStopRunner = go.AddComponent<HitStopRunner>();
            }

            _hitStopRunner.Apply(duration, clampedScale);
        }

        private static void ApplyOnHitEffects(
            SkillDamageEffect damageEffect,
            ISkillExecutionContext ctx,
            List<Transform> damageTargets)
        {
            if (damageEffect == null || ctx == null || damageTargets == null || damageTargets.Count == 0)
                return;

            if (damageEffect.onHitVfxEffects != null)
            {
                for (int targetIndex = 0; targetIndex < damageTargets.Count; targetIndex++)
                    PlayVfxEffects(damageEffect.onHitVfxEffects, ctx.CasterTransform, damageTargets[targetIndex]);
            }

            if (damageEffect.onHitSfxEffects != null)
            {
                for (int targetIndex = 0; targetIndex < damageTargets.Count; targetIndex++)
                    PlaySfxEffects(damageEffect.onHitSfxEffects, ctx.CasterTransform, damageTargets[targetIndex]);
            }

            if (damageEffect.onHitPhysicsEffects != null)
            {
                for (int i = 0; i < damageEffect.onHitPhysicsEffects.Count; i++)
                    ApplyPhysicsEffect(damageEffect.onHitPhysicsEffects[i], ctx, damageTargets, damageEffect);
            }

            if (damageEffect.onHitAttributeEffects != null)
            {
                for (int i = 0; i < damageEffect.onHitAttributeEffects.Count; i++)
                    ApplyAttributeEffect(damageEffect.onHitAttributeEffects[i], ctx, damageTargets);
            }
        }

        private static void ApplyIndependentPhysicsEffect(SkillPhysicsEffect effect, ISkillExecutionContext ctx)
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
                ApplySelfSuperArmor(effect, ctx);
                return;
            }

            if (effect.effectType == PhysicsEffectType.Invincible)
            {
                ApplySelfInvincibility(effect, ctx);
                return;
            }

        }

        private static void ApplyIndependentAttributeEffect(SkillAttributeEffect effect, ISkillExecutionContext ctx)
        {
            if (effect == null || ctx?.CasterTransform == null)
                return;

            ApplyAttributeToTarget(ctx.CasterTransform, effect);
        }

        private static void ApplyPhysicsEffect(
            SkillPhysicsEffect effect,
            ISkillExecutionContext ctx,
            List<Transform> detectedTargets,
            SkillDamageEffect damageEffect)
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

                ApplyTargetPhysicsEffect(target, effect, ctx, damageEffect);
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

            float duration = effect.duration > 0f ? effect.duration : 0.12f;
            var motion = ctx.CasterTransform.GetComponent<CombatMotionController>()
                         ?? ctx.CasterTransform.gameObject.AddComponent<CombatMotionController>();
            motion.ApplyDisplacement(Vector3.up * effect.height, duration);
        }

        private static void ApplySelfSuperArmor(SkillPhysicsEffect effect, ISkillExecutionContext ctx)
        {
            if (effect.duration <= 0f || ctx.CasterTransform == null)
                return;

            PlayerController player = ctx.CasterTransform.GetComponent<PlayerController>();
            player?.ApplyTemporarySuperArmor(effect.duration);
        }

        private static void ApplySelfInvincibility(SkillPhysicsEffect effect, ISkillExecutionContext ctx)
        {
            if (effect.duration <= 0f || ctx.CasterTransform == null)
                return;

            PlayerController player = ctx.CasterTransform.GetComponent<PlayerController>();
            player?.ApplyTemporaryInvincibility(effect.duration);
        }

        private static void ApplyTargetPhysicsEffect(
            Transform target,
            SkillPhysicsEffect effect,
            ISkillExecutionContext ctx,
            SkillDamageEffect damageEffect)
        {
            if (target == null)
                return;

            float duration = effect.duration > 0f
                ? effect.duration
                : (damageEffect != null && damageEffect.pushDuration > 0.01f
                    ? damageEffect.pushDuration
                    : 0.12f);

            if (effect.effectType == PhysicsEffectType.Stun)
            {
                ICombatHardControlReceiver hardControl =
                    target.GetComponentInParent<PlayerController>() as ICombatHardControlReceiver
                    ?? target.GetComponentInParent<EnemyController>() as ICombatHardControlReceiver;
                hardControl?.ApplyHardControl(duration);

                var stun = target.GetComponent<CombatMotionController>()
                           ?? target.gameObject.AddComponent<CombatMotionController>();
                stun.HoldStill(duration);
                return;
            }

            var motion = target.GetComponent<CombatMotionController>()
                         ?? target.gameObject.AddComponent<CombatMotionController>();
            Vector3 horizontal = ResolveHorizontalTargetDirection(ctx.CasterTransform, target, effect.effectType);
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
                ApplyAttributeToTarget(targets[i], effect);
        }

        private static void ApplyAttributeToTarget(Transform target, SkillAttributeEffect effect)
        {
            if (target == null)
                return;

            var player = target.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                ApplyStatFieldToPlayer(player, effect.statField, effect.magnitude, effect.duration, effect.usePercent);
                return;
            }

            var enemy = target.GetComponentInParent<EnemyController>();
            if (enemy != null)
                ApplyStatFieldToEnemy(enemy, effect.statField, effect.magnitude, effect.duration, effect.usePercent);
        }

        private static void ApplyStatFieldToPlayer(
            PlayerController player,
            SkillStatField field,
            float magnitude,
            float duration,
            bool usePercent)
        {
            var model = player.PlayerModel;
            if (model == null)
                return;

            if (field == SkillStatField.HP)
            {
                float amount = usePercent ? model.Stats.MaxHp * magnitude : magnitude;
                if (amount > 0f)
                    model.Heal(amount);
                return;
            }

            if (field == SkillStatField.MP)
            {
                float amount = usePercent ? model.Stats.MaxMp * magnitude : magnitude;
                if (amount > 0f)
                    model.CurrentMp = Mathf.Min(model.Stats.MaxMp, model.CurrentMp + amount);
                return;
            }

            var modifier = BuildModifierForField(field, magnitude, usePercent ? 1f : 0f);
            if (modifier == null)
                return;

            if (duration > 0f)
            {
                var runtime = player.GetComponent<PlayerRuntimeStatModifierController>()
                              ?? player.gameObject.AddComponent<PlayerRuntimeStatModifierController>();
                runtime.ApplyModifier(modifier, duration);
            }
            else
            {
                model.Stats.AddModifier(modifier);
            }
        }

        private static void ApplyStatFieldToEnemy(
            EnemyController enemy,
            SkillStatField field,
            float magnitude,
            float duration,
            bool usePercent)
        {
            if (field == SkillStatField.HP)
            {
                float amount = usePercent ? enemy.MaxHp * magnitude : magnitude;
                if (amount > 0f)
                    enemy.Heal(amount);
                return;
            }

            var modifier = BuildModifierForField(field, magnitude, usePercent ? 1f : 0f);
            if (modifier == null)
                return;

            if (duration > 0f)
                enemy.ApplyTimedStatModifier(modifier, duration);
            else
                enemy.ApplyTimedStatModifier(modifier, float.MaxValue);
        }

        private static void PlayVfxEffects(List<SkillVfxEffect> effects, Transform caster, Transform target)
        {
            if (effects == null || caster == null)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null || effect.particlePrefab == null)
                    continue;

                Transform anchor = ResolveAnchorTransform(effect.anchor, caster, target);
                Vector3 position = anchor != null ? anchor.TransformPoint(effect.offset) : caster.position + effect.offset;
                Quaternion rotation = anchor != null
                    ? anchor.rotation * Quaternion.Euler(effect.rotationEuler)
                    : caster.rotation * Quaternion.Euler(effect.rotationEuler);
                var instance = Object.Instantiate(effect.particlePrefab, position, rotation);
                instance.transform.localScale = Vector3.Scale(instance.transform.localScale, effect.scale);
            }
        }

        private static void PlaySfxEffects(List<SkillSfxEffect> effects, Transform caster, Transform target)
        {
            if (effects == null || caster == null)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null || effect.audioClip == null)
                    continue;

                Transform anchor = ResolveAnchorTransform(effect.anchor, caster, target);
                Vector3 position = anchor != null ? anchor.TransformPoint(effect.offset) : caster.position + effect.offset;
                AudioSource.PlayClipAtPoint(effect.audioClip, position);
            }
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
            PhysicsEffectType effectType)
        {
            if (caster == null || target == null)
                return Vector3.zero;

            Vector3 vector = target.position - caster.position;
            vector.y = 0f;
            if (vector.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 away = vector.normalized;
            if (effectType == PhysicsEffectType.Pull)
                return -away;

            return away;
        }

        private static Transform ResolveAnchorTransform(CueAnchor anchor, Transform caster, Transform target)
        {
            return anchor switch
            {
                CueAnchor.Target => target,
                _ => caster,
            };
        }

        private static Transform ExtractTargetTransform(Collider collider)
        {
            if (collider == null)
                return null;

            var player = collider.GetComponentInParent<PlayerController>();
            if (player != null)
                return player.transform;

            var enemy = collider.GetComponentInParent<EnemyController>();
            if (enemy != null)
                return enemy.transform;

            return collider.transform;
        }

        private static StatModifier BuildModifierForField(SkillStatField field, float magnitude, float percentBase)
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
            private float _restoreScale = 1f;
            private float _appliedScale = 1f;

            public void Apply(float duration, float timeScale)
            {
                if (duration <= 0f || Time.timeScale <= 0f)
                    return;

                float clampedScale = Mathf.Clamp01(timeScale);
                if (!_active)
                {
                    _restoreScale = Time.timeScale;
                    _remainingUnscaled = duration;
                    _appliedScale = clampedScale;
                    Time.timeScale = _appliedScale;
                    _active = true;
                    return;
                }

                _remainingUnscaled = Mathf.Max(_remainingUnscaled, duration);
                _appliedScale = Mathf.Min(_appliedScale, clampedScale);
                if (Time.timeScale > 0f)
                    Time.timeScale = _appliedScale;
            }

            private void Update()
            {
                if (!_active)
                    return;

                if (Mathf.Approximately(Time.timeScale, 0f))
                    return;

                if (!Mathf.Approximately(Time.timeScale, _appliedScale))
                {
                    _active = false;
                    return;
                }

                _remainingUnscaled -= Time.unscaledDeltaTime;
                if (_remainingUnscaled > 0f)
                    return;

                Time.timeScale = _restoreScale;
                _active = false;
            }
        }

    }
}
