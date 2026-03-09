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
        public float CastDuration => Binding != null ? Binding.castDuration : 0.6f;
        public bool IgnoreAnimationDamageEvents => Definition != null && Definition.ignoreAnimationDamageEvents;
        public bool RotateToTargetOnCast => Binding != null && Binding.rotateToTargetOnCast;
        public float PostCastIdleDuration => Binding != null ? Binding.postCastIdleDuration : 0f;
        public bool EnterIdleAfterCast => Binding != null && Binding.enterIdleAfterCast;
        public string SkillId => Definition != null ? Definition.skillId : Binding != null ? Binding.skillId : string.Empty;
    }

    public static class EnemySkillResolver
    {
        private static readonly HashSet<string> MissingSkillWarnings = new HashSet<string>();

        /// <summary>
        /// 解析敌人技能槽位，只从共享技能库读取技能定义。
        /// </summary>
        public static EnemyResolvedSkill Resolve(EnemyArchetypeSO archetype, int slot)
        {
            if (archetype == null)
                return null;

            var binding = archetype.GetSkillSlot(slot);
            if (binding == null || string.IsNullOrWhiteSpace(binding.skillId))
                return null;

            var sharedDb = ConfigManager.GetInstance()?.GetSharedSkillDatabase();
            var sharedDef = sharedDb != null ? sharedDb.GetEntry(binding.skillId) : null;
            if (sharedDef == null)
            {
                if (MissingSkillWarnings.Add(binding.skillId))
                    Debug.LogWarning($"[EnemySkill] 未在共享技能库中找到技能定义: {binding.skillId}");
                return null;
            }

            string animationTrigger = !string.IsNullOrEmpty(binding.animationTrigger)
                ? binding.animationTrigger
                : $"CastSkill{slot}";

            return new EnemyResolvedSkill(slot, binding, sharedDef, animationTrigger);
        }
    }
}
