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
        Flexible,
        GapClose,
        Pressure,
        Punish,
        Escape,
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

        [InspectorLabel("施法距离(米)")]
        [Tooltip("AI 认为可以施放该技能的理想距离。")]
        [Min(0.1f)] public float castRange = 3f;

        [InspectorLabel("最小施法距离(米)")]
        [Tooltip("低于该距离时，AI 会倾向于不用这个技能。填 0 表示没有最小距离限制。")]
        [Min(0f)] public float minCastRange = 0f;

        [InspectorLabel("理想施法距离(米)")]
        [Tooltip("Utility 评分会优先把敌人站到这个距离附近；填 0 时回退到“施法距离”。")]
        [Min(0f)] public float idealCastRange = 0f;

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

        [InspectorLabel("施法锁定时长(秒)")]
        [Tooltip("敌人进入施法状态后，至少保持多久不切出。")]
        [Min(0.01f)] public float castDuration = 0.6f;

        [InspectorLabel("施法后进入后摇 Idle")]
        [Tooltip("开启后，技能结束会进入战术 Idle/后撤恢复。")]
        public bool enterIdleAfterCast;

        [InspectorLabel("后摇 Idle 持续时长(秒)")]
        [Tooltip("仅在“施法后进入后摇 Idle”开启时生效。")]
        [Min(0f)] public float postCastIdleDuration = 0f;

        [InspectorLabel("施法时朝向目标")]
        [Tooltip("开启后，施法开始时会先朝向当前目标。")]
        public bool rotateToTargetOnCast = true;

        [Header("动画")]
        [InspectorLabel("动画 Trigger")]
        [Tooltip("发送给 Animator 的 Trigger 名。留空时默认使用 Skill{槽位索引}。")]
        public string animationTrigger = "";

        [InspectorLabel("自然退出进度")]
        [Tooltip("一次性施法动画播放到该进度后，立即触发自然退出默认目标。动画仅负责表现，不等待技能时间轴结束。")]
        [Range(0f, 1f)] public float naturalExitNormalizedTime = 0.9f;

        [InspectorLabel("自然退出默认目标")]
        [Tooltip("一次性施法动画自然退出时要切回的循环状态。当前通常使用 Locomotion。")]
        public EnemyAnimationNaturalExitTarget naturalExitTarget = EnemyAnimationNaturalExitTarget.Locomotion;

        [Header("阶段限制")]
        [InspectorLabel("可用阶段")]
        [Tooltip("Always=全程可用，LowHpOnly=仅半血及以下，HighHpOnly=仅半血以上。")]
        public EnemySkillPhaseAvailability phaseAvailability = EnemySkillPhaseAvailability.Always;
    }
}
