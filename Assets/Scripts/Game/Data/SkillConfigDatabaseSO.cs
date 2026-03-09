using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 玩家单条技能配置。
    ///
    /// 被动技能：isPassive=true，只需 skillId / displayName / talentCost，运行时由被动系统读取。
    /// 主动技能：isPassive=false，还需填写 mpCost / animationStateIndex / sharedSkillId。
    ///
    /// 触发链（主动技能）：
    ///   玩家按技能键 → Skill[N]State 进入 → TriggerSkill(animationStateIndex) → 播动画
    ///   → SkillTimelineRunner 按 sharedSkillId 找到 SharedSkillDefinition → 逐帧触发事件
    ///   → SkillEffectExecutor 执行伤害/特效/属性效果
    /// </summary>
    [Serializable]
    public class SkillConfigEntry
    {
        [Header("─ 基础信息 ──────────────────────────")]
        [InspectorLabel("技能 ID（全局唯一）")]
        [Tooltip("技能的唯一标识符，供技能树系统和存档引用。\n建议规则：passive_atk_1、active_skill_0 等。")]
        public string skillId = "";

        [InspectorLabel("显示名称")]
        [Tooltip("技能树 UI 中展示的名称，不影响逻辑。")]
        public string displayName = "";

        [InspectorLabel("被动技能")]
        [Tooltip("勾选 = 被动技能（仅增强属性，不消耗 MP，无动画）\n不勾选 = 主动技能（需填写下方主动技能参数）")]
        public bool isPassive = true;

        [InspectorLabel("天赋点消耗")]
        [Tooltip("在技能树中解锁此技能需要消耗的天赋点数。")]
        public int talentCost = 1;

        [Header("─ 主动技能参数（isPassive=false 时填写）─")]
        [InspectorLabel("耗蓝量（MP）")]
        [Tooltip("释放技能时消耗的 MP 量。若 MP 不足则无法进入技能状态。")]
        public int mpCost = 0;

        [InspectorLabel("动画状态索引")]
        [Tooltip("决定释放哪个技能动画：\n0 = Skill0State（PlayerAnimatorController.TriggerSkill(0)）\n1 = Skill1State\n2 = Skill2State\n3 = Skill3State\n与动画控制器中的 SkillIndex 参数对应。")]
        public int animationStateIndex = 0;

        [InspectorLabel("关联共享技能 ID")]
        [Tooltip("填写「共享技能库」中某条 SharedSkillDefinition 的 skillId。\n系统会在释放此技能时加载对应的时间轴定义，按事件列表执行伤害/特效/Buff。\n留空 = 只依靠动画帧的 AnimEvent_DealDamage 结算伤害（无时间轴控制）。")]
        public string sharedSkillId = "";
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
    }
}
