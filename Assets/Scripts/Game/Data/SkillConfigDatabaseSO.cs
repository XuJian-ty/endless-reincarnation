using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain;

namespace Game.Data
{
    public enum PlayerSkillEntryGroup
    {
        [InspectorName("基础技能")]
        BaseSkill,
        [InspectorName("主动技能")]
        ActiveSkill,
        [InspectorName("被动技能")]
        PassiveSkill,
    }

    /// <summary>
    /// 玩家单条技能配置。
    ///
    /// 被动技能：isPassive=true，只需 skillId / displayName / talentCost，运行时由被动系统读取。
    /// 主动/基础技能：按 animationTrigger 与 skillId 绑定动画与技能逻辑。
    ///
    /// 触发链（主动/基础技能）：
    ///   玩家按键 → 先检查玩家技能配置库中的条目是否可用/已解锁 → 向 Animator 发送 animationTrigger
    ///   → SkillTimelineRunner 按 skillId 找到技能库中的定义 → 逐帧触发事件
    ///   → SkillEffectExecutor 执行伤害/特效/属性效果
    /// </summary>
    [Serializable]
    public class SkillConfigEntry
    {
        [Header("─ 基础信息 ──────────────────────────")]
        [InspectorLabel("技能 ID（全局唯一）")]
        [Tooltip("技能的唯一标识符，供技能树系统、存档和技能库引用。\n主动技能请直接填写技能库中的 skillId；被动技能填写自己的被动 skillId。")]
        public string skillId = "";

        [InspectorLabel("显示名称")]
        [Tooltip("技能树 UI 中展示的名称，不影响逻辑。")]
        public string displayName = "";

        [InspectorLabel("被动技能")]
        [Tooltip("勾选 = 被动技能（仅增强属性，不消耗 MP，无动画）\n不勾选 = 主动技能（需填写下方主动技能参数）")]
        public bool isPassive = true;

        [InspectorLabel("技能分组")]
        [Tooltip("基础技能=走路跑步跳跃闪避普攻等基础动作；主动技能=技能树主动技能；被动技能=技能树被动技能。")]
        public PlayerSkillEntryGroup entryGroup = PlayerSkillEntryGroup.PassiveSkill;

        [InspectorLabel("天赋点消耗")]
        [Tooltip("在技能树中解锁此技能需要消耗的天赋点数。")]
        public int talentCost = 1;

        [Header("─ 主动技能参数（isPassive=false 时填写）─")]
        [InspectorLabel("耗蓝量（MP）")]
        [Tooltip("释放技能时消耗的 MP 量。若 MP 不足则无法进入技能状态。")]
        public int mpCost = 0;

        [InspectorLabel("动画 Trigger")]
        [Tooltip("播放该技能/动作时发送给 Animator 的 Trigger 名。留空时默认使用 skillId。")]
        public string animationTrigger = "";

        [InspectorLabel("主动技能槽位")]
        [Tooltip("仅主动技能使用。0~3 对应四个主动技能按键；-1 表示不参与主动技能按键触发。")]
        public int activeSlotIndex = -1;

        [Header("─ 被动技能参数（PassiveSkill 时填写）─")]
        [InspectorLabel("被动属性加成")]
        [Tooltip("被动技能解锁后永久附加到玩家基础属性上的加成。")]
        public StatModifier passiveStatModifier = new StatModifier();

        public string GetResolvedAnimationTrigger()
        {
            return string.IsNullOrWhiteSpace(animationTrigger) ? skillId : animationTrigger.Trim();
        }

        public bool IsPassiveSkill =>
            isPassive || entryGroup == PlayerSkillEntryGroup.PassiveSkill;

        public bool IsActiveSkill
        {
            get
            {
                if (IsPassiveSkill)
                    return false;

                if (entryGroup == PlayerSkillEntryGroup.ActiveSkill || activeSlotIndex >= 0)
                    return true;

                return !string.IsNullOrEmpty(skillId) &&
                       skillId.StartsWith("Skill", StringComparison.Ordinal);
            }
        }

        public bool IsBaseSkill => !IsPassiveSkill && !IsActiveSkill;
    }

    /// <summary>
    /// 玩家技能配置库。
    /// 存储所有可解锁技能的配置，顺序与技能树 UI 一致。
    /// 建议布局：前 12 条被动（各 1 天赋点）+ 后 4 条主动（各 2 天赋点）。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/玩家技能配置库", fileName = "玩家技能配置库")]
    public class SkillConfigDatabaseSO : ScriptableObject
    {
        [InspectorLabel("技能配置列表")]
        [Tooltip("顺序对应技能树的解锁顺序。\n建议：前 12 条被动（天赋点各1）+ 后 4 条主动（天赋点各2，填写耗蓝和动画索引）。")]
        public List<SkillConfigEntry> entries = new List<SkillConfigEntry>();

        public SkillConfigEntry GetEntry(string skillId)
        {
            if (entries == null || string.IsNullOrEmpty(skillId)) return null;
            foreach (var e in entries)
                if (e.skillId == skillId) return e;
            return null;
        }

        /// <summary>按索引取条目（0～11 被动，12～15 主动）</summary>
        public SkillConfigEntry GetEntryByIndex(int index)
        {
            if (entries == null || index < 0 || index >= entries.Count) return null;
            return entries[index];
        }

        public SkillConfigEntry GetEntryByTrigger(string triggerName)
        {
            if (entries == null || string.IsNullOrWhiteSpace(triggerName)) return null;
            string normalized = triggerName.Trim();
            foreach (var e in entries)
            {
                if (e == null || e.IsPassiveSkill) continue;
                if (string.Equals(e.GetResolvedAnimationTrigger(), normalized, StringComparison.Ordinal))
                    return e;
            }
            return null;
        }

        public SkillConfigEntry GetActiveEntryBySlot(int slotIndex)
        {
            if (entries == null || slotIndex < 0) return null;
            foreach (var e in entries)
            {
                if (e == null || !e.IsActiveSkill) continue;
                if (e.activeSlotIndex == slotIndex)
                    return e;
            }

            string fallbackSkillId = $"Skill{slotIndex}";
            return GetEntry(fallbackSkillId);
        }

        public SkillConfigEntry GetBaseEntry(string skillId)
        {
            var entry = GetEntry(skillId);
            return entry != null && entry.IsBaseSkill ? entry : null;
        }
    }
}
