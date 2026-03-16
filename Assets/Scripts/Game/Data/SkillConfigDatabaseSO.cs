using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain;
using Game.Presentation;

namespace Game.Data
{
    public enum PlayerSkillEntryGroup
    {
        [InspectorName("基础动作")]
        BaseSkill,
        [InspectorName("主动技能")]
        ActiveSkill,
        [InspectorName("被动技能")]
        PassiveSkill,
    }

    /// <summary>
    /// 玩家单条动作/技能配置。
    ///
    /// 被动技能：isPassive=true，只需 skillId / displayName / talentCost，运行时由被动系统读取。
    /// 主动/基础技能：按 animationTrigger 与 skillId 绑定动画与技能逻辑。
    ///
    /// 触发链（主动/基础技能）：
    ///   状态机根据输入和上下文得到动作ID → 先检查玩家动作及技能配置库中的条目是否可用/已解锁
    ///   → 向 Animator 发送 animationTrigger
    ///   → SkillTimelineRunner 按 skillId 找到技能库中的定义 → 逐帧触发事件
    ///   → SkillEffectExecutor 执行伤害/特效/属性效果
    /// </summary>
    [Serializable]
    public class SkillConfigEntry
    {
        [Header("─ 基础信息 ──────────────────────────")]
        [InspectorLabel("动作ID（只读）")]
        [Tooltip("基础动作/主动技能从状态机进入配置库时使用的映射标记。\n默认使用玩家状态类名去掉 State 后缀的结果，例如 Jump、Dodge、Attack0、Skill0。")]
        public string actionId = "";

        [InspectorLabel("技能 ID（全局唯一）")]
        [Tooltip("基础动作/主动技能用于映射技能库中的技能效果定义。\n被动技能填写自己的被动 skillId。")]
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

        [InspectorLabel("冷却(秒)")]
        [Tooltip("仅主动技能使用。释放成功后进入冷却，冷却未结束时不能再次释放。")]
        [Min(0f)]
        public float cooldownSeconds = 0f;

        [Header("─ 被动技能参数（PassiveSkill 时填写）─")]
        [InspectorLabel("被动属性加成")]
        [Tooltip("被动技能解锁后永久附加到玩家基础属性上的加成。")]
        public StatModifier passiveStatModifier = new StatModifier();

        [Header("─ 状态规则（基础动作/主动技能）─")]
        [InspectorLabel("动作策略表")]
        public List<PlayerStateActionPolicyRule> actionPolicies = new List<PlayerStateActionPolicyRule>();

        [InspectorLabel("缓存释放规则")]
        public List<PlayerStatePendingReleaseRule> pendingReleaseRules = new List<PlayerStatePendingReleaseRule>();

        [InspectorLabel("覆盖自然退出进度")]
        public bool overrideNaturalExitNormalizedTime;

        [InspectorLabel("自然退出进度")]
        [Range(0f, 1f)]
        public float naturalExitNormalizedTime = 0.9f;

        [InspectorLabel("自然退出默认目标")]
        public PlayerStateNaturalExitTarget naturalExitTarget = PlayerStateNaturalExitTarget.None;

        public string GetResolvedAnimationTrigger()
        {
            if (!string.IsNullOrWhiteSpace(animationTrigger))
                return animationTrigger.Trim();

            if (!string.IsNullOrWhiteSpace(actionId))
                return actionId.Trim();

            return skillId;
        }

        public string GetResolvedActionId()
        {
            return string.IsNullOrWhiteSpace(actionId) ? string.Empty : actionId.Trim();
        }

        public bool IsPassiveSkill =>
            isPassive || entryGroup == PlayerSkillEntryGroup.PassiveSkill;

        public bool IsActiveSkill
        {
            get
            {
                if (IsPassiveSkill)
                    return false;

                return entryGroup == PlayerSkillEntryGroup.ActiveSkill;
            }
        }

        public bool IsBaseSkill => !IsPassiveSkill && !IsActiveSkill;

        public bool TryGetPolicy(GameAction action, out TransitionPolicy policy)
        {
            if (actionPolicies != null)
            {
                for (int i = 0; i < actionPolicies.Count; i++)
                {
                    PlayerStateActionPolicyRule rule = actionPolicies[i];
                    if (rule != null && rule.action == action)
                    {
                        policy = rule.policy;
                        return true;
                    }
                }
            }

            policy = TransitionPolicy.Ignore;
            return false;
        }

        public bool TryGetPendingReleaseThreshold(GameAction action, out float threshold)
        {
            if (pendingReleaseRules != null)
            {
                for (int i = 0; i < pendingReleaseRules.Count; i++)
                {
                    PlayerStatePendingReleaseRule rule = pendingReleaseRules[i];
                    if (rule != null && rule.pendingAction == action)
                    {
                        threshold = rule.normalizedTime;
                        return true;
                    }
                }
            }

            threshold = 0f;
            return false;
        }
    }

    /// <summary>
    /// 玩家动作及技能配置库。
    /// 存储基础动作、主动技能、被动技能的统一配置。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/玩家动作及技能配置库", fileName = "玩家动作及技能配置库")]
    public class SkillConfigDatabaseSO : ScriptableObject
    {
        [InspectorLabel("技能配置列表")]
        [Tooltip("顺序对应动作/技能的配置列表。基础动作和主动技能通过动作ID映射；被动技能只用于技能树和属性增强。")]
        public List<SkillConfigEntry> entries = new List<SkillConfigEntry>();

        public SkillConfigEntry GetEntry(string skillId)
        {
            if (entries == null || string.IsNullOrEmpty(skillId)) return null;
            foreach (var e in entries)
                if (e.skillId == skillId) return e;
            return null;
        }

        public SkillConfigEntry GetEntryByActionId(string actionId)
        {
            if (entries == null || string.IsNullOrWhiteSpace(actionId)) return null;
            string normalized = actionId.Trim();
            foreach (var e in entries)
            {
                if (e == null || e.IsPassiveSkill) continue;
                if (string.Equals(e.GetResolvedActionId(), normalized, StringComparison.Ordinal))
                    return e;
            }
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
            if (slotIndex < 0) return null;
            return GetEntryByActionId($"Skill{slotIndex}");
        }

        public SkillConfigEntry GetBaseEntry(string actionId)
        {
            var entry = GetEntryByActionId(actionId);
            return entry != null && entry.IsBaseSkill ? entry : null;
        }
    }
}
