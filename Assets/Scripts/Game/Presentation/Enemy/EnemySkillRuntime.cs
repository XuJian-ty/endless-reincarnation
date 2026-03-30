using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 运行时已解析的敌人技能：持有共享技能定义与槽位绑定，战斗参数从槽位读取。
    /// </summary>
    public sealed class EnemyResolvedSkill
    {
        public EnemyResolvedSkill(
            int slotIndex,
            EnemySkillSlotBinding binding,
            SharedSkillDefinition definition,
            string animationTrigger)
        {
            SlotIndex = slotIndex;
            Binding = binding;
            Definition = definition;
            AnimationTrigger = animationTrigger;
        }

        public int SlotIndex { get; }
        public EnemySkillSlotBinding Binding { get; }
        public SharedSkillDefinition Definition { get; }
        public string AnimationTrigger { get; }

        public float Cooldown => Binding != null ? Binding.cooldown : 1f;
        public float CastRange => Binding != null ? Binding.castRange : 3f;
        public float MinCastRange => Binding != null ? Binding.minCastRange : 0f;
        public float IdealCastRange => Binding != null && Binding.idealCastRange > 0.01f ? Binding.idealCastRange : CastRange;
        public float CastDuration => Binding != null ? Binding.castDuration : 0.6f;
        public float NaturalExitNormalizedTime => Binding != null ? Binding.naturalExitNormalizedTime : 0.9f;
        public EnemySkillRole SkillRole => Binding != null ? Binding.skillRole : EnemySkillRole.Flexible;
        public EnemyAnimationNaturalExitTarget NaturalExitTarget => Binding != null ? Binding.naturalExitTarget : EnemyAnimationNaturalExitTarget.Locomotion;
        public float RiskWeight => Binding != null ? Binding.riskWeight : 0.35f;
        public float PunishWeight => Binding != null ? Binding.punishWeight : 0.5f;
        public float RepeatPenalty => Binding != null ? Binding.repeatPenalty : 0.2f;
        public bool CanUseUnderThreat => Binding != null && Binding.canUseUnderThreat;
        public bool IgnoreAnimationDamageEvents => Definition != null && Definition.ignoreAnimationDamageEvents;
        public bool RotateToTargetOnCast => Binding != null && Binding.rotateToTargetOnCast;
        public float PostCastIdleDuration => Binding != null ? Binding.postCastIdleDuration : 0f;
        public bool EnterIdleAfterCast => Binding != null && Binding.enterIdleAfterCast;
        public string SkillId => Definition != null ? Definition.skillId : Binding != null ? Binding.skillId : string.Empty;
    }

    public static class EnemySkillResolver
    {
        private static readonly HashSet<string> MissingSkillWarnings = new HashSet<string>();
        private static readonly HashSet<string> MissingAnimationTriggerWarnings = new HashSet<string>();

        /// <summary>
        /// 解析敌人技能槽位，只从技能库读取技能定义。
        /// </summary>
        public static EnemyResolvedSkill Resolve(EnemyArchetypeSO archetype, int slot)
        {
            if (archetype == null)
                return null;

            var binding = archetype.GetSkillSlot(slot);
            if (binding == null || string.IsNullOrWhiteSpace(binding.skillId))
                return null;

            var sharedDb = ConfigManager.GetInstance()?.GetSkillEffectDatabase();
            var sharedDef = sharedDb != null ? sharedDb.GetEntry(binding.skillId) : null;
            if (sharedDef == null)
            {
                if (MissingSkillWarnings.Add(binding.skillId))
                    Debug.LogWarning($"[EnemySkill] 未在技能库中找到技能定义: {binding.skillId}");
                return null;
            }

            string animationTrigger = sharedDef.GetAnimationTriggerOrEmpty();
            if (string.IsNullOrWhiteSpace(animationTrigger))
            {
                if (MissingAnimationTriggerWarnings.Add(binding.skillId))
                    Debug.LogWarning($"[EnemySkill] 技能未填写动画 Trigger: {binding.skillId}");
                animationTrigger = string.Empty;
            }

            return new EnemyResolvedSkill(slot, binding, sharedDef, animationTrigger);
        }
    }
}
