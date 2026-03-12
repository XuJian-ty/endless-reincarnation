using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        [InspectorName("位移")]
        DashSelf,
        [InspectorName("眩晕")]
        Stun,
        [InspectorName("腾空")]
        Airborne,
        [InspectorName("霸体")]
        SuperArmor,
        [InspectorName("无敌")]
        Invincible,
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
    public class SkillVfxEffect
    {
        [InspectorLabel("粒子特效 Prefab")]
        [Tooltip("直接拖入粒子特效预制体，事件触发时会在指定挂点生成。")]
        public GameObject particlePrefab;
        [InspectorLabel("挂点")]
        [Tooltip("Caster=施法者，Target=目标，ImpactPoint=命中点，World=世界位置。")]
        public CueAnchor anchor = CueAnchor.Caster;

        [InspectorLabel("位置偏移")]
        [Tooltip("相对挂点的局部偏移，单位：米。")]
        public Vector3 offset = Vector3.zero;

        [InspectorLabel("旋转偏移")]
        [Tooltip("相对挂点的局部旋转欧拉角，单位：度。")]
        public Vector3 rotationEuler = Vector3.zero;

        [InspectorLabel("缩放")]
        [Tooltip("生成特效时附加的局部缩放。")]
        public Vector3 scale = Vector3.one;

        [InspectorLabel("强制持续时长(秒)")]
        [Tooltip("0 表示使用粒子自身生命周期；大于 0 时会在指定时间后结束。")]
        [Min(0f)] public float duration = 0f;
    }

    [Serializable]
    public class SkillSfxEffect
    {
        [InspectorLabel("音效片段")]
        [Tooltip("事件触发时播放一次，留空则不播放。")]
        public AudioClip audioClip;

        [InspectorLabel("挂点")]
        [Tooltip("Caster=施法者，Target=目标，ImpactPoint=命中点，World=世界位置。")]
        public CueAnchor anchor = CueAnchor.Caster;

        [InspectorLabel("位置偏移")]
        [Tooltip("相对挂点的局部偏移，单位：米。")]
        public Vector3 offset = Vector3.zero;

        [InspectorLabel("强制持续时长(秒)")]
        [Tooltip("预留字段，当前音效为一次播放。")]
        [Min(0f)] public float duration = 0f;
    }

    [Serializable]
    public class SkillCueEntry
    {
        [HideInInspector] public GameObject particlePrefab;
        [HideInInspector] public AudioClip audioClip;
        [HideInInspector] public CueAnchor anchor = CueAnchor.Caster;
        [HideInInspector] public Vector3 offset = Vector3.zero;
        [HideInInspector] public Vector3 rotationEuler = Vector3.zero;
        [HideInInspector] public Vector3 scale = Vector3.one;
        [HideInInspector] [Min(0f)] public float duration = 0f;
    }

    [Serializable]
    public class SkillDamageEffect
    {
        [InspectorLabel("伤害倍率")]
        [Tooltip("当前伤害效果的最终伤害倍率。")]
        [Min(0f)] public float damageMagnitude = 1f;

        [InspectorLabel("检测时长(秒)")]
        [Tooltip("每次伤害触发后，这条伤害判定会持续存在的时间。0 表示仅在触发瞬间检测一次；大于 0 表示在该时间窗口内持续检测，但同一目标只会在第一次进入时命中一次。对碰撞检测来说，这个时长就是武器碰撞器的开启时长。")]
        [Min(0f)] public float detectionDuration = 0f;

        [InspectorLabel("检测方式")]
        [Tooltip("范围=在检测窗口内每帧重做 Overlap；碰撞=在检测窗口内启用指定挂点上的 Trigger Collider；射线=在检测窗口内每帧重做 Raycast。")]
        public DamageDetectionType detectionType = DamageDetectionType.RangeOverlap;

        [InspectorLabel("命中层级名")]
        [Tooltip("例如 Enemy 或 Player。")]
        public string hitLayerName = "Enemy";

        [InspectorLabel("击退力")]
        [Tooltip("命中后附带的击退力度。")]
        [Min(0f)] public float pushForce = 20f;

        [InspectorLabel("击退时长")]
        [Tooltip("命中后附带的击退持续时间。")]
        [Min(0f)] public float pushDuration = 0.04f;

        [InspectorLabel("范围形状")]
        [Tooltip("仅 RangeOverlap 检测方式使用。")]
        public AttackShapeType shape = AttackShapeType.Sphere;

        [InspectorLabel("中心偏移")]
        [Tooltip("仅 RangeOverlap 检测方式使用，相对施法者的本地偏移。")]
        public Vector3 centerOffset = Vector3.zero;

        [InspectorLabel("球体半径")]
        [Tooltip("仅 Sphere/Sector 形状使用。")]
        [Min(0.01f)] public float sphereRadius = 1.5f;

        [InspectorLabel("扇形角度")]
        [Tooltip("仅 Sector 形状使用。")]
        [Range(1f, 360f)] public float sectorAngle = 180f;

        [InspectorLabel("盒体尺寸")]
        [Tooltip("仅 Box 形状使用。")]
        public Vector3 boxSize = Vector3.one;

        [InspectorLabel("武器命中盒对象名")]
        [Tooltip("仅 Collision 检测方式使用。运行时会在施法者身上按名称查找子物体，并启用该物体上的 Trigger Collider 作为武器命中盒。")]
        public string colliderNodeName = "";

        [InspectorLabel("射线起点偏移")]
        [Tooltip("仅 Raycast 检测方式使用，相对施法者的本地偏移。")]
        public Vector3 rayOriginOffset = Vector3.zero;

        [InspectorLabel("射线最大距离")]
        [Tooltip("仅 Raycast 检测方式使用。")]
        [Min(0.01f)] public float rayMaxDistance = 50f;

        [InspectorLabel("命中停顿时长(秒)")]
        [Tooltip("命中成功后附加的全局命中停顿时长。0 表示不触发命中停顿。")]
        [Min(0f)] public float hitStopDuration = 0f;

        [InspectorLabel("命中停顿速度")]
        [Tooltip("命中停顿期间的全局时间缩放。0 表示完全停住，1 表示不减速。")]
        [Range(0f, 1f)] public float hitStopTimeScale = 0f;

        [InspectorLabel("命中物理效果")]
        [Tooltip("仅在这条伤害效果命中目标后触发。")]
        public List<SkillPhysicsEffect> onHitPhysicsEffects = new List<SkillPhysicsEffect>();

        [InspectorLabel("命中属性效果")]
        [Tooltip("仅在这条伤害效果命中目标后触发。")]
        public List<SkillAttributeEffect> onHitAttributeEffects = new List<SkillAttributeEffect>();

        [InspectorLabel("命中特效效果(VFX)")]
        [Tooltip("仅在这条伤害效果命中目标后触发。")]
        public List<SkillVfxEffect> onHitVfxEffects = new List<SkillVfxEffect>();

        [InspectorLabel("命中音效效果(SFX)")]
        [Tooltip("仅在这条伤害效果命中目标后触发。")]
        public List<SkillSfxEffect> onHitSfxEffects = new List<SkillSfxEffect>();
    }

    [Serializable]
    public class SkillPhysicsEffect
    {
        [InspectorLabel("效果类型")]
        [Tooltip("击退/拉拽/击飞/眩晕 只用于命中物理效果。位移/腾空/霸体/无敌 只用于顶层物理效果。方向由代码按效果类型自动计算。")]
        public PhysicsEffectType effectType = PhysicsEffectType.Knockback;

        [InspectorLabel("距离")]
        [Tooltip("击退/拉拽/位移 使用该距离；击飞表示水平距离。")]
        [Min(0f)] public float distance = 1f;

        [InspectorLabel("高度")]
        [Tooltip("击飞/腾空 使用该高度。")]
        [Min(0f)] public float height = 1f;

        [InspectorLabel("持续时长(秒)")]
        [Tooltip("击退、拉拽、击飞、位移、腾空、眩晕、霸体、无敌 的持续时间。")]
        [Min(0f)] public float duration = 0.2f;

        public bool IsTopLevelOnly =>
            effectType == PhysicsEffectType.DashSelf
            || effectType == PhysicsEffectType.Airborne
            || effectType == PhysicsEffectType.SuperArmor
            || effectType == PhysicsEffectType.Invincible;

        public bool IsOnHitOnly =>
            effectType == PhysicsEffectType.Knockback
            || effectType == PhysicsEffectType.Pull
            || effectType == PhysicsEffectType.Launch
            || effectType == PhysicsEffectType.Stun;

        public bool UsesDistance =>
            effectType == PhysicsEffectType.Knockback
            || effectType == PhysicsEffectType.Pull
            || effectType == PhysicsEffectType.Launch
            || effectType == PhysicsEffectType.DashSelf;

        public bool UsesHeight =>
            effectType == PhysicsEffectType.Launch
            || effectType == PhysicsEffectType.Airborne;
    }

    [Serializable]
    public class SkillAttributeEffect
    {
        [InspectorLabel("属性字段")]
        [Tooltip("HP/MP 直接修改当前值；其他字段作为属性 Buff/Debuff 处理。")]
        public SkillStatField statField = SkillStatField.HP;

        [InspectorLabel("作用目标")]
        [Tooltip("Self=自身，DetectedTargets=本次伤害检测命中的所有目标。")]
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

    [System.Serializable]
    public abstract class SkillTimedEventBase
    {
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

        public float GetEndTime()
        {
            return triggerMode == SkillEventTriggerMode.Repeated
                ? startTime + Mathf.Max(0f, activeDuration)
                : startTime;
        }
    }

    [Serializable]
    public class SkillDamageEvent : SkillTimedEventBase
    {
        [InspectorLabel("伤害效果")]
        [Tooltip("一条事件可以配置多条伤害效果，用于同一帧多段伤害。")]
        public List<SkillDamageEffect> damageEffects = new List<SkillDamageEffect>();

        public float GetDetectionEndTime()
        {
            float maxTime = GetEndTime();
            if (damageEffects == null)
                return maxTime;

            float lastTriggerTime = startTime;
            if (triggerMode == SkillEventTriggerMode.Repeated)
                lastTriggerTime = startTime + Mathf.Max(0f, activeDuration);

            for (int i = 0; i < damageEffects.Count; i++)
            {
                SkillDamageEffect effect = damageEffects[i];
                if (effect == null)
                    continue;

                maxTime = Mathf.Max(maxTime, lastTriggerTime + Mathf.Max(0f, effect.detectionDuration));
            }

            return maxTime;
        }

        public SkillDamageEffect GetPrimaryDamageEffect()
        {
            if (damageEffects == null)
                return null;

            for (int i = 0; i < damageEffects.Count; i++)
            {
                SkillDamageEffect effect = damageEffects[i];
                if (effect != null)
                    return effect;
            }

            return null;
        }
    }

    [Serializable]
    public class SkillPhysicsEvent : SkillTimedEventBase
    {
        [InspectorLabel("物理效果")]
        [Tooltip("顶层物理效果，仅用于位移、腾空、霸体与无敌。")]
        public List<SkillPhysicsEffect> physicsEffects = new List<SkillPhysicsEffect>();
    }

    [Serializable]
    public class SkillAttributeEvent : SkillTimedEventBase
    {
        [InspectorLabel("属性效果(回血/Buff等)")]
        [Tooltip("回血、回蓝、增减属性等效果，可叠加多条。")]
        public List<SkillAttributeEffect> attributeEffects = new List<SkillAttributeEffect>();
    }

    [Serializable]
    public class SkillVfxEvent : SkillTimedEventBase
    {
        [InspectorLabel("特效效果(VFX)")]
        [Tooltip("事件触发时播放的特效。")]
        public List<SkillVfxEffect> vfxEffects = new List<SkillVfxEffect>();
    }

    [Serializable]
    public class SkillSfxEvent : SkillTimedEventBase
    {
        [InspectorLabel("音效效果(SFX)")]
        [Tooltip("事件触发时播放的音效。")]
        public List<SkillSfxEffect> sfxEffects = new List<SkillSfxEffect>();
    }

    [Serializable]
    public class SharedSkillDefinition
    {
        [Header("标识")]
        [InspectorLabel("技能ID")]
        [Tooltip("玩家与敌人都通过该 ID 引用此技能定义。该值需要全局唯一。")]
        public string skillId = "";

        [FormerlySerializedAs("displayName")]
        [HideInInspector]
        public string displayName = "";

        [Header("行为控制")]
        [InspectorLabel("忽略动画帧伤害事件")]
        [Tooltip("开启后仅使用时间轴事件结算技能效果。")]
        public bool ignoreAnimationDamageEvents = true;

        [Header("时间轴事件列表")]
        [InspectorLabel("伤害事件列表")]
        [Tooltip("按时间顺序触发的伤害事件节点。")]
        public List<SkillDamageEvent> damageEvents = new List<SkillDamageEvent>();

        [InspectorLabel("物理事件列表")]
        [Tooltip("按时间顺序触发的顶层物理事件节点。")]
        public List<SkillPhysicsEvent> physicsEvents = new List<SkillPhysicsEvent>();

        [InspectorLabel("属性事件列表")]
        [Tooltip("按时间顺序触发的顶层属性事件节点。")]
        public List<SkillAttributeEvent> attributeEvents = new List<SkillAttributeEvent>();

        [InspectorLabel("特效事件列表")]
        [Tooltip("按时间顺序触发的顶层特效事件节点。")]
        public List<SkillVfxEvent> vfxEvents = new List<SkillVfxEvent>();

        [InspectorLabel("音效事件列表")]
        [Tooltip("按时间顺序触发的顶层音效事件节点。")]
        public List<SkillSfxEvent> sfxEvents = new List<SkillSfxEvent>();

        public float GetTimelineDuration()
        {
            float maxTime = 0f;
            maxTime = Mathf.Max(maxTime, GetMaxDamageEndTime(damageEvents));
            maxTime = Mathf.Max(maxTime, GetMaxEndTime(physicsEvents));
            maxTime = Mathf.Max(maxTime, GetMaxEndTime(attributeEvents));
            maxTime = Mathf.Max(maxTime, GetMaxEndTime(vfxEvents));
            maxTime = Mathf.Max(maxTime, GetMaxEndTime(sfxEvents));
            return maxTime;
        }

        public SkillDamageEffect GetPrimaryDamageEffect()
        {
            if (damageEvents != null)
            {
                for (int i = 0; i < damageEvents.Count; i++)
                {
                    SkillDamageEvent evt = damageEvents[i];
                    if (evt == null)
                        continue;

                    SkillDamageEffect effect = evt.GetPrimaryDamageEffect();
                    if (effect != null)
                        return effect;
                }
            }
            return null;
        }

        private static float GetMaxEndTime<T>(List<T> list) where T : SkillTimedEventBase
        {
            float maxTime = 0f;
            if (list == null)
                return maxTime;

            for (int i = 0; i < list.Count; i++)
            {
                T evt = list[i];
                if (evt != null)
                    maxTime = Mathf.Max(maxTime, evt.GetEndTime());
            }

            return maxTime;
        }

        private static float GetMaxDamageEndTime(List<SkillDamageEvent> list)
        {
            float maxTime = 0f;
            if (list == null)
                return maxTime;

            for (int i = 0; i < list.Count; i++)
            {
                SkillDamageEvent evt = list[i];
                if (evt != null)
                    maxTime = Mathf.Max(maxTime, evt.GetDetectionEndTime());
            }

            return maxTime;
        }
    }
}
