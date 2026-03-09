using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 运行时已解析的技能：持有共享技能定义与槽位绑定，所有战斗参数从绑定读取。
    /// </summary>
    public sealed class EnemyResolvedSkill
    {
        public EnemyResolvedSkill(
            int slotIndex,
            EnemySkillSlotBinding binding,
            SharedSkillDefinition definition,
            string animationTrigger)
        {
            SlotIndex        = slotIndex;
            Binding          = binding;
            Definition       = definition;
            AnimationTrigger = animationTrigger;
        }

        public int                   SlotIndex        { get; }
        public EnemySkillSlotBinding Binding          { get; }
        public SharedSkillDefinition Definition       { get; }
        public string                AnimationTrigger { get; }

        public float Cooldown    => Binding != null ? Binding.cooldown    : 1f;
        public float CastRange   => Binding != null ? Binding.castRange   : 3f;
        public float CastDuration => Binding != null ? Binding.castDuration : 0.6f;

        public bool IgnoreAnimationDamageEvents =>
            Definition != null && Definition.ignoreAnimationDamageEvents;

        public bool RotateToTargetOnCast =>
            Binding != null && Binding.rotateToTargetOnCast;

        public float PostCastIdleDuration =>
            Binding != null ? Binding.postCastIdleDuration : 0f;

        public bool EnterIdleAfterCast =>
            Binding != null && Binding.enterIdleAfterCast;

        public string SkillId =>
            Definition != null ? Definition.skillId :
            Binding    != null ? Binding.skillId    : string.Empty;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EnemySkillResolver
    // ═══════════════════════════════════════════════════════════════════════════

    public static class EnemySkillResolver
    {
        private static readonly HashSet<string> MissingSkillWarnings = new HashSet<string>();

        /// <summary>
        /// 解析槽位技能：优先从共享技能库查找，回退到旧敌人技能库（自动转换为共享格式）。
        /// </summary>
        public static EnemyResolvedSkill Resolve(
            EnemyArchetypeSO archetype,
            EnemySkillDatabaseSO legacyDatabase,
            int slot)
        {
            if (archetype == null) return null;

            var binding = archetype.GetSkillSlot(slot);
            if (binding == null || string.IsNullOrWhiteSpace(binding.skillId))
                return null;

            // 1. 优先查共享技能库
            SharedSkillDefinition sharedDef = null;
            var sharedDb = ConfigManager.GetInstance()?.GetSharedSkillDatabase();
            if (sharedDb != null)
                sharedDef = sharedDb.GetEntry(binding.skillId);

            // 2. 回退到旧敌人技能库，自动转换为共享格式
            if (sharedDef == null && legacyDatabase != null)
            {
                var legacyDef = legacyDatabase.GetEntry(binding.skillId);
                if (legacyDef != null)
                    sharedDef = legacyDef.ToShared();
            }

            if (sharedDef == null)
            {
                if (MissingSkillWarnings.Add(binding.skillId))
                    Debug.LogWarning($"[EnemySkill] 未在共享技能库或敌人技能库中找到技能定义: {binding.skillId}");
                return null;
            }

            // 3. 解析动画触发器（binding 中的 animationTrigger 优先，否则默认 CastSkill{slot}）
            string animationTrigger = !string.IsNullOrEmpty(binding.animationTrigger)
                ? binding.animationTrigger
                : $"CastSkill{slot}";

            return new EnemyResolvedSkill(slot, binding, sharedDef, animationTrigger);
        }
    }
}
