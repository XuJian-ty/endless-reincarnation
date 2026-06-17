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
        public const string AttackComboActionPrefix = "Attack";

        public static string BuildSkillSlotActionName(int slotIndex)
        {
            return slotIndex >= 0 ? $"{SkillSlotActionPrefix}{slotIndex}" : string.Empty;
        }

        public static bool IsSkillSlotActionName(string actionId)
        {
            return TryParseSkillSlotIndex(actionId, out _);
        }

        public static bool IsAttackComboActionName(string actionId)
        {
            return !string.IsNullOrWhiteSpace(actionId)
                   && actionId.StartsWith(AttackComboActionPrefix, StringComparison.Ordinal);
        }

        public static bool IsDodgeActionName(string actionId)
        {
            return string.Equals(actionId, "Dodge", StringComparison.Ordinal);
        }

        public static bool IsChargeStartActionName(string actionId)
        {
            return string.Equals(actionId, "ChargeStart", StringComparison.Ordinal);
        }

        public static bool IsChargeLoopActionName(string actionId)
        {
            return string.Equals(actionId, "ChargeLoop", StringComparison.Ordinal);
        }

        public static bool IsRangedChargeActionName(string actionId)
        {
            return string.Equals(actionId, "ShootCharge", StringComparison.Ordinal)
                   || string.Equals(actionId, "Shoot_Charge", StringComparison.Ordinal);
        }

        public static bool IsRangedActionName(string actionId)
        {
            return string.Equals(actionId, "Shoot", StringComparison.Ordinal)
                   || IsRangedChargeActionName(actionId);
        }

        public static GameAction ResolveGameAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return GameAction.None;

            string normalizedActionId = actionId.Trim();
            if (IsAttackComboActionName(normalizedActionId))
                return GameAction.NormalAttack;
            if (IsDodgeActionName(normalizedActionId))
                return GameAction.Dodge;
            if (IsChargeStartActionName(normalizedActionId))
                return GameAction.ChargeStart;
            if (IsChargeLoopActionName(normalizedActionId))
                return GameAction.ChargeStart;
            if (IsRangedChargeActionName(normalizedActionId))
                return GameAction.ShootCharge;

            return normalizedActionId switch
            {
                "ChargeRelease" => GameAction.ChargeRelease,
                "Shoot" => GameAction.Shoot,
                _ when IsSkillSlotActionName(normalizedActionId) => GameAction.Skill,
                _ => GameAction.None,
            };
        }

        public static bool TryParseAttackComboIndex(string actionId, out int index)
        {
            index = 0;
            if (!IsAttackComboActionName(actionId))
                return false;

            string normalizedActionId = actionId.Trim();
            string suffix = normalizedActionId.Substring(AttackComboActionPrefix.Length);
            index = int.TryParse(suffix, out int parsedIndex) ? parsedIndex : 0;
            return true;
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
