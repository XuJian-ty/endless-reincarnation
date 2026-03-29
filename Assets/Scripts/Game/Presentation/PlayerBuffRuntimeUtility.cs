using System.Collections.Generic;
using Game;
using Game.Data;
using Game.Domain;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 玩家 Buff 运行时辅助：
    /// - 统一读取可叠加 Buff 的运行时参数
    /// - 在技能开始时按 Buff 生成修正后的时间轴定义
    /// </summary>
    public static class PlayerBuffRuntimeUtility
    {
        private static readonly HashSet<string> AttackSpeedAffectedActions = new HashSet<string>
        {
            "Attack0",
            "Attack1",
            "Attack2",
            "Attack3",
            "AirAttack",
            "ChargeStart",
            "ChargeLoop",
            "ChargeRelease",
            "Shoot",
            "ShootCharge",
            "Shoot_Charge"
        };

        private static readonly HashSet<string> AfterimageAffectedActions = new HashSet<string>
        {
            "Attack0",
            "Attack1",
            "Attack2",
            "Attack3",
            "ChargeRelease",
            "FallAttackLand",
            "Shoot",
            "ShootCharge",
            "Shoot_Charge"
        };

        public static PlayerController ResolveOwningPlayer(Transform caster)
        {
            if (caster == null)
                return null;

            PlayerCloneActor cloneActor = ResolveOwningClone(caster);
            if (cloneActor != null && cloneActor.Owner != null)
                return cloneActor.Owner;

            return caster.GetComponentInParent<PlayerController>();
        }

        public static PlayerCloneActor ResolveOwningClone(Transform caster)
        {
            return caster != null ? caster.GetComponentInParent<PlayerCloneActor>() : null;
        }

        public static Stats ResolveCombatStats(Transform caster)
        {
            PlayerCloneActor cloneActor = ResolveOwningClone(caster);
            if (cloneActor != null)
                return cloneActor.CombatStats;

            PlayerController player = caster != null ? caster.GetComponentInParent<PlayerController>() : null;
            return player?.PlayerModel?.Stats;
        }

        public static int GetBuffStackCount(Transform caster, string buffId)
        {
            PlayerController player = ResolveOwningPlayer(caster);
            return player?.PlayerModel != null ? player.PlayerModel.GetBuffStackCount(buffId) : 0;
        }

        public static float GetDamageRangeScale(Transform caster)
        {
            PlayerController player = ResolveOwningPlayer(caster);
            if (player?.PlayerModel == null)
                return 1f;

            int stackCount = player.PlayerModel.GetBuffStackCount(BuffIds.DamageRangeExpand);
            if (stackCount <= 0)
                return 1f;

            BuffConfigSO buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            float singleScale = buffConfig != null
                ? buffConfig.GetDamageRangeScale(BuffIds.DamageRangeExpand)
                : 1f;

            return Mathf.Pow(Mathf.Max(1f, singleScale), stackCount);
        }

        public static SummonCloneBuffSettings GetSummonCloneSettings(Transform caster)
        {
            BuffConfigSO buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            if (buffConfig == null)
            {
                return new SummonCloneBuffSettings
                {
                    followRadius = 2f,
                    opacity = 0.45f
                };
            }

            return buffConfig.GetSummonCloneSettings(BuffIds.SummonClone);
        }

        public static SharedSkillDefinition BuildRuntimeSkillDefinition(Transform caster, SharedSkillDefinition source, string actionId = null)
        {
            if (caster == null || source == null)
                return source;

            PlayerController player = ResolveOwningPlayer(caster);
            if (player?.PlayerModel == null)
                return source;

            int afterimageStackCount = player.PlayerModel.GetBuffStackCount(BuffIds.Afterimage);
            bool shouldAppendAfterimage = afterimageStackCount > 0 && IsAfterimageAffectedAction(actionId);
            if (!shouldAppendAfterimage)
                return source;

            BuffConfigSO buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            float delay = buffConfig != null
                ? buffConfig.GetAfterimageDelaySeconds(BuffIds.Afterimage)
                : 0f;
            float damageMultiplier = buffConfig != null
                ? buffConfig.GetAfterimageDamageMultiplier(BuffIds.Afterimage)
                : 0.5f;
            if (delay <= 0f)
                return source;

            SharedSkillDefinition cloned = JsonUtility.FromJson<SharedSkillDefinition>(JsonUtility.ToJson(source));
            if (cloned == null)
                return source;

            AppendAfterimageEvents(cloned.damageEvents, 1, delay, damageMultiplier);
            AppendAfterimageEvents(cloned.vfxEvents, 1, delay);
            SortTimedEvents(cloned.damageEvents);
            SortTimedEvents(cloned.vfxEvents);
            return cloned;
        }

        public static float GetActionCastSpeedMultiplier(PlayerModel player, string actionId)
        {
            return GetActionCastSpeedMultiplier(player?.Stats, actionId);
        }

        public static float GetActionCastSpeedMultiplier(Stats stats, string actionId)
        {
            if (stats == null || string.IsNullOrWhiteSpace(actionId))
                return 1f;

            return IsAttackSpeedAffectedAction(actionId.Trim())
                ? Mathf.Max(0.1f, stats.AttackSpeed)
                : 1f;
        }

        public static float GetActionPlaybackSpeed(PlayerModel player, string actionId)
        {
            return GetActionCastSpeedMultiplier(player?.Stats, actionId);
        }

        public static float GetActionAnimatorPlaybackSpeed(PlayerModel player, string actionId)
        {
            return GetActionAnimatorPlaybackSpeed(player?.Stats, actionId);
        }

        public static float GetActionPlaybackSpeed(Stats stats, string actionId)
        {
            return GetActionCastSpeedMultiplier(stats, actionId);
        }

        public static float GetActionAnimatorPlaybackSpeed(Stats stats, string actionId)
        {
            return GetActionCastSpeedMultiplier(stats, actionId);
        }

        private static bool IsAttackSpeedAffectedAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            string normalizedActionId = actionId.Trim();
            if (AttackSpeedAffectedActions.Contains(normalizedActionId))
                return true;

            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillDb == null || !skillDb.TryGetFormActionSlot(normalizedActionId, out PlayerFormActionSlot actionSlot))
                return false;

            return actionSlot == PlayerFormActionSlot.Attack0
                   || actionSlot == PlayerFormActionSlot.Attack1
                   || actionSlot == PlayerFormActionSlot.Attack2
                   || actionSlot == PlayerFormActionSlot.Attack3
                   || actionSlot == PlayerFormActionSlot.AirAttack
                   || actionSlot == PlayerFormActionSlot.ChargeStart
                   || actionSlot == PlayerFormActionSlot.ChargeLoop
                   || actionSlot == PlayerFormActionSlot.ChargeRelease;
        }

        private static bool IsAfterimageAffectedAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            string normalizedActionId = actionId.Trim();
            if (AfterimageAffectedActions.Contains(normalizedActionId))
                return true;
            if (PlayerActionRouting.IsSkillSlotActionName(normalizedActionId))
                return true;

            SkillConfigDatabaseSO skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            SkillConfigEntry entry = skillDb != null ? skillDb.GetEntryByActionId(normalizedActionId) : null;
            return entry != null && entry.IsActiveSkill;
        }

        private static void AppendAfterimageEvents(List<SkillDamageEvent> events, int stackCount, float delay, float damageMultiplier)
        {
            if (events == null || events.Count == 0 || stackCount <= 0 || delay <= 0f)
                return;

            int originalCount = events.Count;
            for (int sourceIndex = 0; sourceIndex < originalCount; sourceIndex++)
            {
                SkillDamageEvent evt = events[sourceIndex];
                if (evt == null)
                    continue;

                for (int stackIndex = 0; stackIndex < stackCount; stackIndex++)
                {
                    SkillDamageEvent duplicated = JsonUtility.FromJson<SkillDamageEvent>(JsonUtility.ToJson(evt));
                    if (duplicated == null)
                        continue;

                    duplicated.startTime += delay * (stackIndex + 1);
                    if (duplicated.damageEffects != null)
                    {
                        for (int damageIndex = 0; damageIndex < duplicated.damageEffects.Count; damageIndex++)
                        {
                            SkillDamageEffect effect = duplicated.damageEffects[damageIndex];
                            if (effect == null)
                                continue;

                            List<SkillHitDamageEffect> hitDamageEffects = effect.GetEffectiveHitDamageEffects();
                            for (int hitDamageIndex = 0; hitDamageIndex < hitDamageEffects.Count; hitDamageIndex++)
                            {
                                SkillHitDamageEffect hitDamageEffect = hitDamageEffects[hitDamageIndex];
                                if (hitDamageEffect == null)
                                    continue;

                                hitDamageEffect.damageMagnitude *= Mathf.Max(0f, damageMultiplier);
                            }
                            duplicated.damageEffects[damageIndex] = effect;
                        }
                    }
                    events.Add(duplicated);
                }
            }
        }

        private static void AppendAfterimageEvents<TEvent>(List<TEvent> events, int stackCount, float delay)
            where TEvent : SkillTimedEventBase
        {
            if (events == null || events.Count == 0 || stackCount <= 0 || delay <= 0f)
                return;

            int originalCount = events.Count;
            for (int sourceIndex = 0; sourceIndex < originalCount; sourceIndex++)
            {
                TEvent evt = events[sourceIndex];
                if (evt == null)
                    continue;

                for (int stackIndex = 0; stackIndex < stackCount; stackIndex++)
                {
                    TEvent duplicated = JsonUtility.FromJson<TEvent>(JsonUtility.ToJson(evt));
                    if (duplicated == null)
                        continue;

                    duplicated.startTime += delay * (stackIndex + 1);
                    events.Add(duplicated);
                }
            }
        }

        private static void SortTimedEvents<TEvent>(List<TEvent> events) where TEvent : SkillTimedEventBase
        {
            if (events == null || events.Count <= 1)
                return;

            events.Sort((left, right) =>
            {
                if (left == null && right == null) return 0;
                if (left == null) return 1;
                if (right == null) return -1;
                return left.startTime.CompareTo(right.startTime);
            });
        }

    }
}
