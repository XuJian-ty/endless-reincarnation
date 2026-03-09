using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public enum SkillEventTriggerMode
    {
        [InspectorName("一次")]
        Once,
        [InspectorName("重复")]
        Repeated,
    }

    public enum SkillTargetMode
    {
        [InspectorName("自身")]
        Self,
        [InspectorName("当前目标")]
        CurrentTarget,
        [InspectorName("命中目标")]
        DetectedTargets,
    }

    public enum PhysicsEffectType
    {
        [InspectorName("击退")]
        Knockback,
        [InspectorName("拉拽")]
        Pull,
        [InspectorName("击飞")]
        Launch,
        [InspectorName("自身位移")]
        DashSelf,
        [InspectorName("眩晕")]
        Stun,
        [InspectorName("腾空")]
        Airborne,
    }

    public enum SkillEffectDirection
    {
        [InspectorName("前方")]
        Forward,
        [InspectorName("朝向目标")]
        TowardTarget,
        [InspectorName("远离目标")]
        AwayFromTarget,
        [InspectorName("向上")]
        Up,
    }

    public enum SkillStatField
    {
        [InspectorName("生命")]
        HP,
        [InspectorName("法力")]
        MP,
        [InspectorName("攻击")]
        Attack,
        [InspectorName("防御")]
        Defense,
        [InspectorName("移速")]
        MoveSpeed,
        [InspectorName("生命回复")]
        HPRegen,
        [InspectorName("法力回复")]
        MPRegen,
        [InspectorName("暴击率")]
        CritRate,
        [InspectorName("暴击伤害")]
        CritDamage,
        [InspectorName("攻速")]
        AttackSpeed,
        [InspectorName("增伤")]
        SkillDamage,
        [InspectorName("减伤")]
        DamageReduce,
        [InspectorName("吸血")]
        LifeSteal,
    }

    public enum CueAnchor
    {
        Caster,
        Target,
        ImpactPoint,
        World,
    }

    [Serializable]
    public class SkillCueEntry
    {
        [Header("特效")]
        [InspectorLabel("粒子特效 Prefab")]
        [Tooltip("直接拖入粒子特效预制体，事件触发时会在指定挂点生成。")]
        public GameObject particlePrefab;

        [Header("音效")]
        [InspectorLabel("音效片段")]
        [Tooltip("事件触发时播放一次，留空则不播放。")]
        public AudioClip audioClip;

        [Header("位置")]
        [InspectorLabel("挂点")]
        [Tooltip("Caster=施法者，Target=目标，ImpactPoint=命中点，World=世界位置。")]
        public CueAnchor anchor = CueAnchor.Caster;

        [InspectorLabel("位置偏移")]
        [Tooltip("相对挂点的局部偏移，单位：米。")]
        public Vector3 offset = Vector3.zero;

        [Header("生命周期")]
        [InspectorLabel("强制持续时长(秒)")]
        [Tooltip("0 表示使用粒子自身生命周期；大于 0 时会在指定时间后结束。")]
        [Min(0f)] public float duration = 0f;
    }

    [Serializable]
    public class SkillDamageEffect
    {
        [InspectorLabel("伤害检测名")]
        [Tooltip("填写“技能伤害数据配置库”中的 damageName。每条伤害效果都会单独执行一次命中检测。")]
        public string damageName = "";

        [InspectorLabel("伤害倍率")]
        [Tooltip("在伤害配置倍率基础上再乘一次。1 表示保持原倍率。")]
        [Min(0f)] public float damageMagnitude = 1f;
    }

    [Serializable]
    public class SkillPhysicsEffect
    {
        [InspectorLabel("效果类型")]
        [Tooltip("Knockback=击退，Pull=拉拽，Launch=对目标上抬并附带少量水平位移，DashSelf=自身位移，Stun=眩晕，Airborne=施法者自身纯向上腾空。")]
        public PhysicsEffectType effectType = PhysicsEffectType.Knockback;

        [InspectorLabel("作用目标")]
        [Tooltip("Self=自身，CurrentTarget=当前目标，DetectedTargets=本次伤害检测命中的所有目标。Airborne 和 DashSelf 会固定为 Self。")]
        public SkillTargetMode targetMode = SkillTargetMode.DetectedTargets;

        [InspectorLabel("作用方向")]
        [Tooltip("Forward=朝施法者前方，TowardTarget=朝目标，AwayFromTarget=远离目标，Up=竖直向上。Airborne 会固定为 Up；Launch 会在该方向基础上附带向上分量。")]
        public SkillEffectDirection direction = SkillEffectDirection.AwayFromTarget;

        [InspectorLabel("力度/距离")]
        [Tooltip("位移、推拉距离，单位：米。Stun 类型忽略该值。")]
        [Min(0f)] public float magnitude = 1f;

        [InspectorLabel("持续时长(秒)")]
        [Tooltip("位移完成或眩晕持续所需时间。")]
        [Min(0f)] public float duration = 0.2f;
    }

    [Serializable]
    public class SkillAttributeEffect
    {
        [InspectorLabel("属性字段")]
        [Tooltip("HP/MP 直接修改当前值；其他字段作为属性 Buff/Debuff 处理。")]
        public SkillStatField statField = SkillStatField.HP;

        [InspectorLabel("作用目标")]
        [Tooltip("Self=自身，CurrentTarget=当前目标，DetectedTargets=本次伤害检测命中的所有目标。")]
        public SkillTargetMode targetMode = SkillTargetMode.Self;

        [InspectorLabel("数值")]
        [Tooltip("正数表示增加或恢复，负数表示减少。")]
        public float magnitude = 0f;

        [InspectorLabel("按比例计算")]
        [Tooltip("勾选后按最大值或基础值比例计算，例如 0.2 表示 20%。")]
        public bool usePercent = false;

        [InspectorLabel("Buff 持续时长(秒)")]
        [Tooltip("仅对 HP/MP 以外的字段生效。0 表示本局常驻；大于 0 表示临时 Buff。")]
        [Min(0f)] public float duration = 0f;
    }

    [Serializable]
    public class SkillTimelineEvent
    {
        [Header("基础")]
        [InspectorLabel("事件标识 ID")]
        [Tooltip("可选，仅用于时间轴块命名、调试和日志定位，不参与逻辑和运行时效果判定。留空时编辑器会自动使用默认标签。")]
        public string eventId = "";

        [InspectorLabel("触发时间(秒)")]
        [Tooltip("从技能开始到本事件触发的延迟时间。")]
        [Min(0f)] public float startTime = 0f;

        [InspectorLabel("触发模式")]
        [Tooltip("Once=只触发一次，Repeated=在持续区间内重复触发。")]
        public SkillEventTriggerMode triggerMode = SkillEventTriggerMode.Once;

        [InspectorLabel("持续触发时长(秒)")]
        [Tooltip("仅 Repeated 模式使用。")]
        [Min(0f)] public float activeDuration = 0f;

        [InspectorLabel("重复触发间隔(秒)")]
        [Tooltip("仅 Repeated 模式使用。")]
        [Min(0.01f)] public float repeatInterval = 0.1f;

        [Header("附加效果")]
        [InspectorLabel("伤害效果")]
        [Tooltip("一条事件可以配置多条伤害效果，用于同一帧多段伤害。")]
        public List<SkillDamageEffect> damageEffects = new List<SkillDamageEffect>();

        [InspectorLabel("物理效果")]
        [Tooltip("击退、拉拽、击飞、冲刺、眩晕等效果，可叠加多条。")]
        public List<SkillPhysicsEffect> physicsEffects = new List<SkillPhysicsEffect>();

        [InspectorLabel("属性效果(回血/Buff等)")]
        [Tooltip("回血、回蓝、增减属性等效果，可叠加多条。")]
        public List<SkillAttributeEffect> attributeEffects = new List<SkillAttributeEffect>();

        [InspectorLabel("表现效果(VFX/SFX)")]
        [Tooltip("事件触发时播放的特效和音效。")]
        public List<SkillCueEntry> cues = new List<SkillCueEntry>();

        public float GetEndTime()
        {
            if (triggerMode == SkillEventTriggerMode.Repeated)
                return startTime + Mathf.Max(0f, activeDuration);
            return startTime;
        }
    }

    [Serializable]
    public class SharedSkillDefinition
    {
        [Header("标识")]
        [InspectorLabel("共享技能ID")]
        [Tooltip("玩家与敌人都通过该 ID 引用此共享技能定义。该值需要全局唯一。")]
        public string skillId = "";

        [InspectorLabel("显示名称")]
        [Tooltip("编辑器和 UI 中显示的名称。")]
        public string displayName = "";

        [Header("行为控制")]
        [InspectorLabel("忽略动画帧伤害事件")]
        [Tooltip("开启后仅使用时间轴事件结算技能效果。")]
        public bool ignoreAnimationDamageEvents = true;

        [Header("时间轴事件列表")]
        [InspectorLabel("事件列表")]
        [Tooltip("按时间顺序触发的事件节点。每个事件可包含伤害、物理、属性和表现效果。")]
        public List<SkillTimelineEvent> events = new List<SkillTimelineEvent>();

        public float GetTimelineDuration()
        {
            float maxTime = 0f;
            if (events != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    var evt = events[i];
                    if (evt != null)
                        maxTime = Mathf.Max(maxTime, evt.GetEndTime());
                }
            }
            return maxTime;
        }
    }
}
