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

        public static void ExecuteEvent(SkillTimelineEvent evt, ISkillExecutionContext ctx)
        {
            if (evt == null || ctx == null)
                return;

            PlayCues(evt.cues, ctx.CasterTransform, null);

            AnimationFrameDamageEntry primaryDetectorEntry = null;
            DetectedTargetsScratch.Clear();
            DetectedTargetSet.Clear();

            if (!string.IsNullOrEmpty(evt.hitDetectionName) && ctx.DamageDatabase != null)
            {
                if (DamageDetectionRunner.TryRunDetection(
                        evt.hitDetectionName,
                        ctx.CasterTransform,
                        ctx.DamageDatabase,
                        ctx.OverlapBuffer,
                        out var detectorEntry,
                        out int hitCount))
                {
                    primaryDetectorEntry = detectorEntry;
                    for (int i = 0; i < hitCount; i++)
                    {
                        var target = ExtractTargetTransform(ctx.OverlapBuffer[i]);
                        if (target == null)
                            continue;

                        if (DetectedTargetSet.Add(target))
                            DetectedTargetsScratch.Add(target);
                    }

                    float detectorMultiplier = detectorEntry != null ? detectorEntry.damageMultiplier : 1f;
                    float finalMultiplier = evt.damageMagnitude > 0f
                        ? evt.damageMagnitude * detectorMultiplier
                        : detectorMultiplier;
                    float stunDuration = detectorEntry != null && detectorEntry.pushDuration > 0.01f
                        ? detectorEntry.pushDuration
                        : 0.2f;

                    for (int i = 0; i < DetectedTargetsScratch.Count; i++)
                        ApplyDamageToTarget(DetectedTargetsScratch[i], ctx.CasterAttack, finalMultiplier, stunDuration);
                }
            }

            if (evt.physicsEffects != null)
            {
                for (int i = 0; i < evt.physicsEffects.Count; i++)
                    ApplyPhysicsEffect(evt.physicsEffects[i], ctx, DetectedTargetsScratch, primaryDetectorEntry);
            }

            if (evt.attributeEffects != null)
            {
                for (int i = 0; i < evt.attributeEffects.Count; i++)
                    ApplyAttributeEffect(evt.attributeEffects[i], ctx, DetectedTargetsScratch);
            }
        }

        private static void ApplyDamageToTarget(
            Transform target,
            float casterAttack,
            float damageMultiplier,
            float stunDuration)
        {
            if (target == null)
                return;

            var player = target.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                float damage = CombatCalculator.CalculateDamageFromEnemy(
                    casterAttack,
                    player.PlayerModel.Stats.Defense) * damageMultiplier;
                player.OnHit(damage, stunDuration);
                return;
            }

            var enemy = target.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                float damage = CombatCalculator.CalculateDamageFromEnemy(
                    casterAttack,
                    enemy.Defense) * damageMultiplier;
                enemy.ApplyDamage(damage);
            }
        }

        private static void ApplyPhysicsEffect(
            SkillPhysicsEffect effect,
            ISkillExecutionContext ctx,
            List<Transform> detectedTargets,
            AnimationFrameDamageEntry detectorEntry)
        {
            if (effect == null)
                return;

            if (effect.effectType == PhysicsEffectType.DashSelf)
            {
                ApplySelfDisplacement(effect, ctx);
                return;
            }

            var targets = ResolveTargets(effect.targetMode, ctx, detectedTargets);
            for (int i = 0; i < targets.Count; i++)
                ApplyTargetPhysicsEffect(targets[i], effect, ctx, detectorEntry);
        }

        private static void ApplySelfDisplacement(SkillPhysicsEffect effect, ISkillExecutionContext ctx)
        {
            if (effect.magnitude <= 0f || ctx.CasterTransform == null)
                return;

            float duration = effect.duration > 0f ? effect.duration : 0.12f;
            Vector3 direction = ResolveDirection(ctx.CasterTransform, null, effect.direction, ctx);
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            var motion = ctx.CasterTransform.GetComponent<CombatMotionController>()
                         ?? ctx.CasterTransform.gameObject.AddComponent<CombatMotionController>();
            motion.ApplyDisplacement(direction.normalized * effect.magnitude, duration);
        }

        private static void ApplyTargetPhysicsEffect(
            Transform target,
            SkillPhysicsEffect effect,
            ISkillExecutionContext ctx,
            AnimationFrameDamageEntry detectorEntry)
        {
            if (target == null || effect.magnitude <= 0f)
                return;

            float duration = effect.duration > 0f
                ? effect.duration
                : (detectorEntry != null && detectorEntry.pushDuration > 0.01f
                    ? detectorEntry.pushDuration
                    : 0.12f);

            Vector3 direction = ResolveDirection(ctx.CasterTransform, target, effect.direction, ctx);

            if (effect.effectType == PhysicsEffectType.Pull)
            {
                direction = -direction;
            }
            else if (effect.effectType == PhysicsEffectType.Launch)
            {
                direction = Vector3.up;
            }
            else if (effect.effectType == PhysicsEffectType.Stun)
            {
                var stun = target.GetComponent<CombatMotionController>()
                           ?? target.gameObject.AddComponent<CombatMotionController>();
                stun.ApplyDisplacement(Vector3.zero, duration);
                return;
            }

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            var motion = target.GetComponent<CombatMotionController>()
                         ?? target.gameObject.AddComponent<CombatMotionController>();
            motion.ApplyDisplacement(direction.normalized * effect.magnitude, duration);
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

        private static void PlayCues(List<SkillCueEntry> cues, Transform caster, Transform target)
        {
            if (cues == null || caster == null)
                return;

            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (cue == null)
                    continue;

                if (cue.particlePrefab != null)
                {
                    Transform anchor = ResolveAnchorTransform(cue.anchor, caster, target);
                    Vector3 position = (anchor != null ? anchor.position : caster.position) + cue.offset;
                    Object.Instantiate(cue.particlePrefab, position, caster.rotation);
                }

                if (cue.audioClip != null)
                    AudioSource.PlayClipAtPoint(cue.audioClip, caster.position);
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
                case SkillTargetMode.CurrentTarget:
                    if (ctx.CurrentTarget != null)
                        result.Add(ctx.CurrentTarget);
                    break;
                default:
                    result.AddRange(detectedTargets);
                    break;
            }

            return result;
        }

        private static Vector3 ResolveDirection(
            Transform caster,
            Transform target,
            SkillEffectDirection direction,
            ISkillExecutionContext ctx)
        {
            if (direction == SkillEffectDirection.TowardTarget)
            {
                var resolvedTarget = target ?? ctx.CurrentTarget;
                if (resolvedTarget != null && caster != null)
                {
                    var vector = resolvedTarget.position - caster.position;
                    vector.y = 0f;
                    if (vector.sqrMagnitude > 0.0001f)
                        return vector.normalized;
                }
            }
            else if (direction == SkillEffectDirection.AwayFromTarget)
            {
                var resolvedTarget = target ?? ctx.CurrentTarget;
                if (resolvedTarget != null && caster != null)
                {
                    var vector = caster.position - resolvedTarget.position;
                    vector.y = 0f;
                    if (vector.sqrMagnitude > 0.0001f)
                        return vector.normalized;
                }
            }
            else if (direction == SkillEffectDirection.Up)
            {
                return Vector3.up;
            }

            return caster != null ? caster.forward : Vector3.forward;
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
                default:
                    return null;
            }

            return modifier;
        }
    }
}
