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

    [Serializable]
    public class PlayerFormActionMappingEntry
    {
        [InspectorLabel("动作槽位")]
        public PlayerFormActionSlot actionSlot = PlayerFormActionSlot.Attack0;

        [InspectorLabel("近战动作ID")]
        public string meleeActionId = "";

        [InspectorLabel("远程动作ID")]
        public string rangedActionId = "";

        public string ResolveActionId(PlayerAttackMode attackMode)
        {
            return attackMode == PlayerAttackMode.Ranged
                ? ResolveValue(rangedActionId)
                : ResolveValue(meleeActionId);
        }

        private static string ResolveValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    /// <summary>
    /// 玩家单条动作/技能配置。
    ///
    /// 被动技能：isPassive=true，仍需填写 actionId 作为身份标识；skillId 只负责映射被动技能效果定义。
    /// 主动/基础技能：按 actionId 绑定身份与状态逻辑，按“技能效果库引用 + skillId”绑定技能效果定义。
    ///
    /// 触发链（主动/基础技能）：
    ///   状态机根据输入和上下文得到动作ID → 先检查玩家动作及技能配置库中的条目是否可用/已解锁
    ///   → 向 Animator 发送 animationTrigger
    ///   → SkillTimelineRunner 按“技能效果库引用 + skillId”找到技能效果定义 → 逐帧触发事件
    ///   → SkillEffectExecutor 执行伤害/特效/属性效果
    /// </summary>
    [Serializable]
    public class SkillConfigEntry
    {
        [Header("─ 基础信息 ──────────────────────────")]
        [InspectorLabel("动作ID（技能身份标识）")]
        [Tooltip("所有基础动作/主动技能/被动技能统一使用的身份标识。\n默认使用玩家状态类名去掉 State 后缀的结果，例如 Jump、Dodge、Attack0、Skill0。")]
        public string actionId = "";

        [InspectorLabel("技能 ID（技能效果表现）")]
        [Tooltip("只用于映射所选技能效果库中的技能效果定义，不再承担玩家解锁、冷却、技能树身份等逻辑职责。")]
        public string skillId = "";

        [InspectorLabel("技能效果库引用")]
        [Tooltip("留空则使用默认的“技能效果库”。填写后会优先从该库中按 skillId 查找技能效果定义。")]
        public SkillEffectDatabaseSO skillEffectDatabase;

        [InspectorLabel("被动技能效果库引用")]
        [Tooltip("仅被动技能使用。留空则使用默认的“被动技能效果库”。填写后会优先从该库中按 skillId 查找被动属性效果定义。")]
        public PassiveSkillEffectDatabaseSO passiveSkillEffectDatabase;

        [InspectorLabel("显示名称")]
        [Tooltip("技能树 UI 中展示的名称，不影响逻辑。")]
        public string displayName = "";

        [InspectorLabel("技能介绍")]
        [Tooltip("技能树详情区中展示的说明文本。")]
        [TextArea(2, 5)]
        public string description = "";

        [InspectorLabel("技能图标")]
        [Tooltip("技能树 UI 中展示的技能图标，不影响运行时逻辑。")]
        public Sprite skillIcon;

        [InspectorLabel("被动技能")]
        [Tooltip("勾选 = 被动技能（仅增强属性，不消耗 MP，无动画）\n不勾选 = 主动技能（需填写下方主动技能参数）")]
        public bool isPassive = true;

        [InspectorLabel("技能分组")]
        [Tooltip("基础技能=走路跑步跳跃闪避普攻等基础动作；主动技能=技能树主动技能；被动技能=技能树被动技能。")]
        public PlayerSkillEntryGroup entryGroup = PlayerSkillEntryGroup.PassiveSkill;

        [InspectorLabel("支持形态")]
        [Tooltip("仅主动技能使用。用于限制该技能可在哪些形态下释放。")]
        public PlayerAttackModeMask supportedAttackModes = PlayerAttackModeMask.All;

        [InspectorLabel("天赋点消耗")]
        [Tooltip("在技能树中解锁此技能需要消耗的天赋点数。")]
        public int talentCost = 1;

        [Header("─ 主动技能参数（isPassive=false 时填写）─")]
        [InspectorLabel("耗蓝量（MP）")]
        [Tooltip("释放技能时消耗的 MP 量。若 MP 不足则无法进入技能状态。")]
        public int mpCost = 0;

        [InspectorLabel("动画 Trigger")]
        [Tooltip("播放该技能/动作时发送给 Animator 的 Trigger 名。留空时默认使用 actionId。")]
        public string animationTrigger = "";

        [InspectorLabel("冷却(秒)")]
        [Tooltip("仅主动技能使用。释放成功后进入冷却，冷却未结束时不能再次释放。")]
        [Min(0f)]
        public float cooldownSeconds = 0f;

        [Header("─ 被动技能参数（PassiveSkill 时填写）─")]
        [InspectorLabel("被动属性加成")]
        [Tooltip("兼容旧数据使用。若已配置“被动技能效果库引用 + 技能ID”，运行时会优先使用被动技能效果库中的属性加成。")]
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

            return string.Empty;
        }

        public string GetResolvedActionId()
        {
            if (!string.IsNullOrWhiteSpace(actionId))
                return actionId.Trim();

            return string.Empty;
        }

        public SkillEffectDatabaseSO GetResolvedSkillEffectDatabase()
        {
            return skillEffectDatabase != null
                ? skillEffectDatabase
                : global::Game.ConfigManager.GetInstance()?.GetSkillEffectDatabase();
        }

        public SharedSkillDefinition ResolveSkillEffectDefinition()
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return null;

            SkillEffectDatabaseSO database = GetResolvedSkillEffectDatabase();
            return database != null ? database.GetEntry(skillId.Trim()) : null;
        }

        public PassiveSkillEffectDatabaseSO GetResolvedPassiveSkillEffectDatabase()
        {
            return passiveSkillEffectDatabase != null
                ? passiveSkillEffectDatabase
                : global::Game.ConfigManager.GetInstance()?.GetPassiveSkillEffectDatabase();
        }

        public PassiveSkillEffectDefinition ResolvePassiveSkillEffectDefinition()
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return null;

            PassiveSkillEffectDatabaseSO database = GetResolvedPassiveSkillEffectDatabase();
            return database != null ? database.GetEntry(skillId.Trim()) : null;
        }

        public StatModifier ResolvePassiveStatModifier()
        {
            PassiveSkillEffectDefinition definition = ResolvePassiveSkillEffectDefinition();
            if (definition?.statModifier != null)
            {
                if (HasAnyStatModifierValue(definition.statModifier) || !HasAnyStatModifierValue(passiveStatModifier))
                    return definition.statModifier;
            }

            return passiveStatModifier;
        }

        private static bool HasAnyStatModifierValue(StatModifier modifier)
        {
            if (modifier == null)
                return false;

            if (!Mathf.Approximately(modifier.hpAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.mpAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.attackAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.defenseAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.lifeStealAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.critRateAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.critDmgAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.attackSpeedAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.moveSpeedAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.hpRegenAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.mpRegenAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.damageBonusAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.damageReduceAdd, 0f)) return true;

            return false;
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

        public bool SupportsAttackMode(PlayerAttackMode attackMode)
        {
            if (supportedAttackModes == PlayerAttackModeMask.None)
                return true;

            PlayerAttackModeMask currentMask = PlayerAttackModeUtility.ToMask(attackMode);
            return (supportedAttackModes & currentMask) != 0;
        }

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
        [Tooltip("顺序对应动作/技能的配置列表。基础动作、主动技能、被动技能统一通过动作ID标识；skillId 仅负责映射技能效果。")]
        public List<SkillConfigEntry> entries = new List<SkillConfigEntry>();

        [InspectorLabel("形态基础动作映射")]
        [Tooltip("同一语义动作在不同形态下解析到的具体 actionId。留空则回退到动作槽位默认名。")]
        public List<PlayerFormActionMappingEntry> formActionMappings = new List<PlayerFormActionMappingEntry>();

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
                if (e == null) continue;
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
            return GetEntryByActionId(PlayerActionRouting.BuildSkillSlotActionName(slotIndex));
        }

        public SkillConfigEntry GetBaseEntry(string actionId)
        {
            var entry = GetEntryByActionId(actionId);
            return entry != null && entry.IsBaseSkill ? entry : null;
        }

        public SkillConfigEntry GetActiveSkillEntryByActionId(string skillActionId)
        {
            SkillConfigEntry entry = GetEntryByActionId(skillActionId);
            return entry != null && entry.IsActiveSkill ? entry : null;
        }

        public string ResolveFormActionId(PlayerAttackMode attackMode, PlayerFormActionSlot actionSlot, string fallbackActionId = null)
        {
            if (formActionMappings != null)
            {
                for (int i = 0; i < formActionMappings.Count; i++)
                {
                    PlayerFormActionMappingEntry mapping = formActionMappings[i];
                    if (mapping == null || mapping.actionSlot != actionSlot)
                        continue;

                    string resolvedActionId = mapping.ResolveActionId(attackMode);
                    if (!string.IsNullOrWhiteSpace(resolvedActionId))
                        return resolvedActionId;
                }
            }

            return !string.IsNullOrWhiteSpace(fallbackActionId)
                ? fallbackActionId.Trim()
                : actionSlot.ToString();
        }

        public bool TryGetFormActionSlot(string actionId, out PlayerFormActionSlot actionSlot)
        {
            actionSlot = default;
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            string normalizedActionId = actionId.Trim();
            if (formActionMappings != null)
            {
                for (int i = 0; i < formActionMappings.Count; i++)
                {
                    PlayerFormActionMappingEntry mapping = formActionMappings[i];
                    if (mapping == null)
                        continue;

                    if (string.Equals(mapping.ResolveActionId(PlayerAttackMode.Melee), normalizedActionId, StringComparison.Ordinal)
                        || string.Equals(mapping.ResolveActionId(PlayerAttackMode.Ranged), normalizedActionId, StringComparison.Ordinal))
                    {
                        actionSlot = mapping.actionSlot;
                        return true;
                    }
                }
            }

            return Enum.TryParse(normalizedActionId, out actionSlot);
        }
    }
}
