using System;
using UnityEngine;

namespace Game.Data
{
    public enum EnemySkillPhaseAvailability
    {
        Always,
        LowHpOnly,
        HighHpOnly,
    }

    public enum EnemySkillRole
    {
        [InspectorName("通用")]
        Flexible,
        [InspectorName("贴近")]
        GapClose,
        [InspectorName("压制")]
        Pressure,
        [InspectorName("抓后摇")]
        Punish,
        [InspectorName("脱压")]
        Escape,
        [InspectorName("区域压制")]
        AreaControl,
    }

    public enum EnemyAnimationNaturalExitTarget
    {
        [InspectorName("Locomotion")]
        Locomotion,
        [InspectorName("无")]
        None,
    }

    [Serializable]
    public class EnemySkillSlotBinding
    {
        [Header("槽位标识")]
        [InspectorLabel("槽位索引")]
        [Tooltip("从 0 开始的槽位编号。同一敌人的槽位索引必须唯一。")]
        [Min(0)] public int slotIndex;

        [InspectorLabel("技能ID")]
        [Tooltip("引用技能库中的 skillId。敌人通过这个 ID 绑定技能时间轴定义。")]
        public string skillId = "";

        [InspectorLabel("显示名称")]
        [Tooltip("仅用于编辑器中区分该技能槽的用途说明。")]
        public string displayName = "";

        [Header("AI 战斗参数")]
        [InspectorLabel("冷却时间(秒)")]
        [Tooltip("该槽位再次可用前需要等待的时间。")]
        [Min(0f)] public float cooldown = 1f;

        [InspectorLabel("技能最远释放距离(米)")]
        [Tooltip("AI 最远能从多远开始或继续释放这招。它不是技能真实命中范围；真实攻击范围仍由技能时间轴里的伤害检测决定。")]
        [Min(0.1f)] public float castRange = 3f;

        [InspectorLabel("技能最近释放距离(米)")]
        [Tooltip("AI 低于这个距离时不会释放这招。填 0 表示没有最近释放距离限制。")]
        [Min(0f)] public float minCastRange = 0f;

        [InspectorLabel("技能理想释放距离(米)")]
        [Tooltip("AI 更偏好在这个距离附近释放这招；越接近这里，这招在决策打分时越容易被选中。填 0 时回退到“技能最远释放距离”。")]
        [Min(0f)] public float idealCastRange = 0f;

        [InspectorLabel("技能理想释放距离容差(米)")]
        [Tooltip("围绕“技能理想释放距离”允许偏离的范围。越大越容易接受当前站位，不会频繁前后修距离。")]
        [Min(0.05f)] public float idealCastDistanceTolerance = 0.9f;

        [InspectorLabel("技能定位")]
        [Tooltip("Flexible=通用；GapClose=贴近；Pressure=持续压制；Punish=抓后摇；Escape=脱离压力；AreaControl=区域压制。")]
        public EnemySkillRole skillRole = EnemySkillRole.Flexible;

        [InspectorLabel("风险权重")]
        [Tooltip("越高表示这招后摇/站桩/送脸风险越大，谨慎型敌人会更少使用。")]
        [Range(0f, 1f)] public float riskWeight = 0.35f;

        [InspectorLabel("抓后摇权重")]
        [Tooltip("玩家出现后摇或空档时，这招会额外加多少分。")]
        [Range(0f, 1f)] public float punishWeight = 0.5f;

        [InspectorLabel("重复惩罚")]
        [Tooltip("连续重复使用同一槽位时的降分强度，越高越不容易反复刷同一招。")]
        [Range(0f, 1f)] public float repeatPenalty = 0.2f;

        [InspectorLabel("压力下允许释放")]
        [Tooltip("开启后，即使敌人判断自己正面临较高威胁，也允许把这招纳入候选。适合带位移、反打或霸体的技能。")]
        public bool canUseUnderThreat;

        [InspectorLabel("后摇恢复时长(秒)")]
        [Tooltip("技能结束后进入战术恢复/后撤的持续时长。填 0 表示没有额外后摇恢复。")]
        [Min(0f)] public float postCastIdleDuration = 0f;

        [InspectorLabel("施法时朝向目标")]
        [Tooltip("开启后，施法开始时会先朝向当前目标。")]
        public bool rotateToTargetOnCast = true;

        [Header("动画")]
        [InspectorLabel("动画自然退出进度")]
        [Tooltip("一次性施法动画播放到该进度后，立即触发自然退出并切回 Locomotion。动画仅负责表现，不等待技能时间轴结束。")]
        [Range(0f, 1f)] public float naturalExitNormalizedTime = 0.9f;

        [Header("阶段限制")]
        [InspectorLabel("可用阶段")]
        [Tooltip("Always=全程可用，LowHpOnly=仅半血及以下，HighHpOnly=仅半血以上。")]
        public EnemySkillPhaseAvailability phaseAvailability = EnemySkillPhaseAvailability.Always;
    }
}
