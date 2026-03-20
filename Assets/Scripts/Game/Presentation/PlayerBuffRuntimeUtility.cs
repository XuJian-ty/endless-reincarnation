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
            "FallAttackStart",
            "FallAttackLoop",
            "FallAttackLand"
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
            if (afterimageStackCount <= 0)
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

            ApplyAttackSpeed(ResolveCombatStats(caster), cloned, actionId);
            AppendAfterimageEvents(cloned.damageEvents, afterimageStackCount, delay, damageMultiplier);
            AppendAfterimageEvents(cloned.vfxEvents, afterimageStackCount, delay);
            AppendAfterimageEvents(cloned.sfxEvents, afterimageStackCount, delay);
            SortTimedEvents(cloned.damageEvents);
            SortTimedEvents(cloned.vfxEvents);
            SortTimedEvents(cloned.sfxEvents);
            return cloned;
        }

        public static float GetActionPlaybackSpeed(PlayerModel player, string actionId)
        {
            return GetActionPlaybackSpeed(player?.Stats, actionId);
        }

        public static float GetActionPlaybackSpeed(Stats stats, string actionId)
        {
            if (stats == null || string.IsNullOrWhiteSpace(actionId))
                return 1f;

            return AttackSpeedAffectedActions.Contains(actionId.Trim())
                ? Mathf.Max(0.1f, stats.AttackSpeed)
                : 1f;
        }

        private static void ApplyAttackSpeed(Stats stats, SharedSkillDefinition definition, string actionId)
        {
            if (stats == null || definition == null)
                return;

            float playbackSpeed = GetActionPlaybackSpeed(stats, actionId);
            if (Mathf.Approximately(playbackSpeed, 1f))
                return;

            float timeScale = 1f / playbackSpeed;
            ScaleTimedEvents(definition.damageEvents, timeScale);
            ScaleTimedEvents(definition.vfxEvents, timeScale);
            ScaleTimedEvents(definition.sfxEvents, timeScale);
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

        private static void ScaleTimedEvents<TEvent>(List<TEvent> events, float timeScale)
            where TEvent : SkillTimedEventBase
        {
            if (events == null || Mathf.Approximately(timeScale, 1f))
                return;

            for (int i = 0; i < events.Count; i++)
            {
                TEvent evt = events[i];
                if (evt == null)
                    continue;

                evt.startTime *= timeScale;
                evt.activeDuration *= timeScale;
                evt.repeatInterval = Mathf.Max(0.01f, evt.repeatInterval * timeScale);
            }
        }
    }
}
