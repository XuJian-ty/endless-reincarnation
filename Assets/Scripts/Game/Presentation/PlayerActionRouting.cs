using System;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 玩家动作路由辅助：
    /// 基础动作ID（Jump / Dodge / Attack0...）与技能槽位动作名（Skill0 / Skill1...）在这里统一判定与转换。
    /// </summary>
    public static class PlayerActionRouting
    {
        public const string SkillSlotActionPrefix = "Skill";

        public static string BuildSkillSlotActionName(int slotIndex)
        {
            return slotIndex >= 0 ? $"{SkillSlotActionPrefix}{slotIndex}" : string.Empty;
        }

        public static bool IsSkillSlotActionName(string actionId)
        {
            return TryParseSkillSlotIndex(actionId, out _);
        }

        public static bool TryParseSkillSlotIndex(string actionId, out int slotIndex)
        {
            slotIndex = -1;
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            string normalized = actionId.Trim();
            if (!normalized.StartsWith(SkillSlotActionPrefix, StringComparison.Ordinal))
                return false;

            string suffix = normalized.Substring(SkillSlotActionPrefix.Length);
            return int.TryParse(suffix, out slotIndex) && slotIndex >= 0;
        }

        public static bool IsBaseActionId(string actionId, SkillConfigDatabaseSO config)
        {
            SkillConfigEntry entry = config != null ? config.GetEntryByActionId(actionId) : null;
            return entry != null && entry.IsBaseSkill;
        }

        public static bool IsActiveSkillActionId(string actionId, SkillConfigDatabaseSO config)
        {
            SkillConfigEntry entry = config != null ? config.GetEntryByActionId(actionId) : null;
            return entry != null && entry.IsActiveSkill;
        }
    }
}
