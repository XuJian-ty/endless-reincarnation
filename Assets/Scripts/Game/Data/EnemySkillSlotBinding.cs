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

        [Header("AI 战斗参数")]
        [InspectorLabel("冷却时间(秒)")]
        [Tooltip("该槽位再次可用前需要等待的时间。")]
        [Min(0f)] public float cooldown = 1f;

        [InspectorLabel("施法距离(米)")]
        [Tooltip("AI 认为可以施放该技能的理想距离。")]
        [Min(0.1f)] public float castRange = 3f;

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
        [Tooltip("发送给 Animator 的 Trigger 名。留空时默认使用 CastSkill{槽位索引}。")]
        public string animationTrigger = "";

        [Header("阶段限制")]
        [InspectorLabel("可用阶段")]
        [Tooltip("Always=全程可用，LowHpOnly=仅半血及以下，HighHpOnly=仅半血以上。")]
        public EnemySkillPhaseAvailability phaseAvailability = EnemySkillPhaseAvailability.Always;
    }
}
