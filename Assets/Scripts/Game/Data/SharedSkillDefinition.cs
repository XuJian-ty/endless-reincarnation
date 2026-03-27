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
        World,
    }

    public enum SkillCueDestroyMode
    {
        [InspectorName("等待自然销毁")]
        NaturalDestroy,
        [InspectorName("指定秒数销毁")]
        Timed,
        [InspectorName("状态退出销毁")]
        OnStateExit,
    }

    public enum SkillEventActiveDurationMode
    {
        [InspectorName("指定时长")]
        FixedTime,
        [InspectorName("状态退出")]
        UntilStateExit,
    }

    public enum SkillEffectDurationMode
    {
        [InspectorName("指定时长")]
        FixedTime,
        [InspectorName("状态退出")]
        UntilStateExit,
    }

    [Serializable]
    public class SkillMotionSettings
    {
        [InspectorLabel("随时间移动")]
        [Tooltip("开启后，会在生成后按“移动速度”和“移动方向”持续移动。关闭则保持原本位置/跟随行为。")]
        public bool enabled = false;

        [InspectorLabel("移动速度(米/秒)")]
        [Tooltip("仅“随时间移动”开启时使用。")]
        [Min(0f)] public float speed = 0f;

        [InspectorLabel("移动方向")]
        [Tooltip("相对施法者朝向的本地方向。(0,0,1) 表示朝前方移动。")]
        public Vector3 direction = Vector3.forward;

        public bool IsActive => enabled && speed > 0.0001f && direction.sqrMagnitude > 0.0001f;
    }

    [Serializable]
    public class SkillVfxEffect
    {
        [InspectorLabel("粒子特效 Prefab")]
        [Tooltip("直接拖入粒子特效预制体，事件触发时会在指定挂点生成。")]
        public GameObject particlePrefab;
        [InspectorLabel("挂点")]
        [Tooltip("Caster=施法者，Target=目标，World=世界位置。")]
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

        [InspectorLabel("移动设置")]
        [Tooltip("用于实现剑气、前进中的扇形特效等。")]
        public SkillMotionSettings motion = new SkillMotionSettings();

        [InspectorLabel("销毁方式")]
        [Tooltip("等待自然销毁=交给特效自身；指定秒数销毁=到时强制销毁；状态退出销毁=当前状态退出时销毁。")]
        public SkillCueDestroyMode destroyMode = SkillCueDestroyMode.NaturalDestroy;

        [InspectorLabel("销毁时间(秒)")]
        [Tooltip("仅“指定秒数销毁”使用。到时会强制销毁该特效实例。")]
        [Min(0f)] public float duration = 0f;
    }

    [Serializable]
    public class SkillSfxEffect
    {
        [InspectorLabel("音效片段")]
        [Tooltip("事件触发时播放一次，留空则不播放。")]
        public AudioClip audioClip;

        [InspectorLabel("挂点")]
        [Tooltip("Caster=施法者，Target=目标，World=世界位置。")]
        public CueAnchor anchor = CueAnchor.Caster;

        [InspectorLabel("位置偏移")]
        [Tooltip("相对挂点的局部偏移，单位：米。")]
        public Vector3 offset = Vector3.zero;

        [InspectorLabel("循环播放")]
        [Tooltip("开启后使用循环 AudioSource，通常应搭配“指定秒数销毁”或“状态退出销毁”。")]
        public bool loop = false;

        [InspectorLabel("销毁方式")]
        [Tooltip("等待自然销毁=播完音频自然结束；指定秒数销毁=到时强制停止并销毁；状态退出销毁=当前状态退出时停止并销毁。")]
        public SkillCueDestroyMode destroyMode = SkillCueDestroyMode.NaturalDestroy;

        [InspectorLabel("销毁时间(秒)")]
        [Tooltip("仅“指定秒数销毁”使用。到时会强制停止并销毁该音效实例。")]
        [Min(0f)] public float duration = 0f;
    }

    [Serializable]
    public class SkillHitDamageEffect
    {
        [InspectorLabel("伤害倍率")]
        [Tooltip("当前命中伤害效果的最终伤害倍率。")]
        [Min(0f)] public float damageMagnitude = 1f;
    }

    [Serializable]
    public class SkillHitStopEffect
    {
        [InspectorLabel("命中停顿时长(秒)")]
        [Tooltip("命中成功后附加的全局命中停顿时长。0 表示不触发命中停顿。")]
        [Min(0f)] public float hitStopDuration = 0f;

        [InspectorLabel("命中停顿速度")]
        [Tooltip("命中停顿期间的全局时间缩放。0 表示完全停住，1 表示不减速。")]
        [Range(0f, 1f)] public float hitStopTimeScale = 0f;

        [InspectorLabel("同时停顿镜头转向")]
        [Tooltip("勾选后，命中停顿期间会同时暂停玩家镜头转向输入。")]
        public bool pauseCameraLookDuringHitStop = false;
    }

    [Serializable]
    public class SkillDamageEffect
    {
        [InspectorLabel("检测时长(秒)")]
        [Tooltip("每次伤害触发后，这条伤害判定会持续存在的时间。0 表示仅在触发瞬间检测一次；大于 0 表示在该时间窗口内持续检测，但同一目标只会在第一次进入时命中一次。对碰撞检测来说，这个时长就是武器碰撞器的开启时长。")]
        [Min(0f)] public float detectionDuration = 0f;

        [InspectorLabel("检测方式")]
        [Tooltip("范围=在检测窗口内每帧重做 Overlap；碰撞=在检测窗口内启用指定挂点上的 Trigger Collider；射线=在检测窗口内每帧重做 Raycast。")]
        public DamageDetectionType detectionType = DamageDetectionType.RangeOverlap;

        [InspectorLabel("命中层级名")]
        [Tooltip("例如 Enemy 或 Player。")]
        public string hitLayerName = "Enemy";

        [InspectorLabel("范围形状")]
        [Tooltip("仅 RangeOverlap 检测方式使用。")]
        public AttackShapeType shape = AttackShapeType.Sphere;

        [InspectorLabel("中心偏移")]
        [Tooltip("仅 RangeOverlap 检测方式使用，相对施法者的本地偏移。")]
        public Vector3 centerOffset = Vector3.zero;

        [InspectorLabel("旋转偏移")]
        [Tooltip("用于旋转扇形、盒体或射线方向，例如把扇形判定竖起来。")]
        public Vector3 rotationEuler = Vector3.zero;

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

        [InspectorLabel("射线半径")]
        [Tooltip("仅 Raycast 检测方式使用。0 表示细射线；大于 0 时会改为球形射线，相当于可调粗细。")]
        [Min(0f)] public float rayRadius = 0f;

        [InspectorLabel("移动设置")]
        [Tooltip("用于实现向前推进的扇形、剑气判定等。碰撞检测模式暂不使用这组设置。")]
        public SkillMotionSettings motion = new SkillMotionSettings();

        [HideInInspector]
        [FormerlySerializedAs("companionVfxEffect")]
        public SkillVfxEffect legacyCompanionVfxEffect;

        [InspectorLabel("伴随特效效果(VFX)")]
        [Tooltip("仅非碰撞检测使用。检测窗口创建时生成，检测窗口销毁时销毁，并随检测体一起移动。")]
        public List<SkillVfxEffect> companionVfxEffects = new List<SkillVfxEffect>();

        [InspectorLabel("命中伤害效果")]
        [Tooltip("命中成功后触发的伤害子效果。")]
        public List<SkillHitDamageEffect> onHitDamageEffects = new List<SkillHitDamageEffect>();

        [InspectorLabel("命中停顿效果")]
        [Tooltip("命中成功后触发的命中停顿子效果。")]
        public SkillHitStopEffect onHitStopEffect = new SkillHitStopEffect();

        [HideInInspector]
        [FormerlySerializedAs("onHitStopEffects")]
        public List<SkillHitStopEffect> legacyOnHitStopEffects = new List<SkillHitStopEffect>();

        [HideInInspector] public float hitStopDuration = 0f;
        [HideInInspector] public float hitStopTimeScale = 0f;
        [HideInInspector] public bool pauseCameraLookDuringHitStop = false;
        [HideInInspector] public float damageMagnitude = 0f;
        [HideInInspector] public bool nestedSubEffectsOwnedByLists = false;

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

        public bool TryMigrateLegacySubEffects()
        {
            bool migrated = false;
            companionVfxEffects ??= new List<SkillVfxEffect>();
            onHitDamageEffects ??= new List<SkillHitDamageEffect>();
            onHitStopEffect ??= new SkillHitStopEffect();
            legacyOnHitStopEffects ??= new List<SkillHitStopEffect>();
            bool listOwnedByCurrentFields = nestedSubEffectsOwnedByLists;
            bool hasLegacyHitDamage = !listOwnedByCurrentFields && damageMagnitude > 0f;
            bool hasLegacyHitStop =
                hitStopDuration > 0f
                || hitStopTimeScale > 0f
                || pauseCameraLookDuringHitStop;

            if (legacyCompanionVfxEffect != null)
            {
                if (!listOwnedByCurrentFields && companionVfxEffects.Count == 0)
                    companionVfxEffects.Add(legacyCompanionVfxEffect);

                legacyCompanionVfxEffect = null;
                migrated = true;
            }

            if (hasLegacyHitDamage && onHitDamageEffects.Count == 0)
            {
                onHitDamageEffects.Add(new SkillHitDamageEffect
                {
                    damageMagnitude = Mathf.Max(0f, damageMagnitude)
                });
                migrated = true;
            }
            if (legacyOnHitStopEffects.Count > 0)
            {
                SkillHitStopEffect primaryLegacyHitStopEffect = null;
                for (int i = 0; i < legacyOnHitStopEffects.Count; i++)
                {
                    SkillHitStopEffect effect = legacyOnHitStopEffects[i];
                    if (effect != null)
                    {
                        primaryLegacyHitStopEffect = effect;
                        break;
                    }
                }

                if (primaryLegacyHitStopEffect != null
                    && !HasConfiguredHitStopEffect(onHitStopEffect))
                {
                    onHitStopEffect = new SkillHitStopEffect
                    {
                        hitStopDuration = Mathf.Max(0f, primaryLegacyHitStopEffect.hitStopDuration),
                        hitStopTimeScale = Mathf.Clamp01(primaryLegacyHitStopEffect.hitStopTimeScale),
                        pauseCameraLookDuringHitStop = primaryLegacyHitStopEffect.pauseCameraLookDuringHitStop
                    };
                    migrated = true;
                }
            }

            if (hasLegacyHitStop && !HasConfiguredHitStopEffect(onHitStopEffect))
            {
                onHitStopEffect = new SkillHitStopEffect
                {
                    hitStopDuration = Mathf.Max(0f, hitStopDuration),
                    hitStopTimeScale = Mathf.Clamp01(hitStopTimeScale),
                    pauseCameraLookDuringHitStop = pauseCameraLookDuringHitStop
                };
                migrated = true;
            }

            if (!migrated && listOwnedByCurrentFields)
                return false;

            damageMagnitude = 0f;
            hitStopDuration = 0f;
            hitStopTimeScale = 0f;
            pauseCameraLookDuringHitStop = false;
            legacyOnHitStopEffects.Clear();
            nestedSubEffectsOwnedByLists = true;
            return true;
        }

        public List<SkillHitDamageEffect> GetEffectiveHitDamageEffects()
        {
            TryMigrateLegacySubEffects();
            onHitDamageEffects ??= new List<SkillHitDamageEffect>();
            return onHitDamageEffects;
        }

        public SkillHitStopEffect GetEffectiveHitStopEffect()
        {
            TryMigrateLegacySubEffects();
            onHitStopEffect ??= new SkillHitStopEffect();
            return onHitStopEffect;
        }

        public SkillHitDamageEffect GetPrimaryHitDamageEffect()
        {
            List<SkillHitDamageEffect> effects = GetEffectiveHitDamageEffects();
            for (int i = 0; i < effects.Count; i++)
            {
                SkillHitDamageEffect effect = effects[i];
                if (effect != null)
                    return effect;
            }

            return null;
        }

        public SkillHitStopEffect GetPrimaryHitStopEffect()
        {
            return GetEffectiveHitStopEffect();
        }

        public static bool HasConfiguredHitStopEffect(SkillHitStopEffect effect)
        {
            return effect != null
                && (effect.hitStopDuration > 0f
                    || effect.hitStopTimeScale > 0f
                    || effect.pauseCameraLookDuringHitStop);
        }
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

        [InspectorLabel("持续方式")]
        [Tooltip("指定时长=持续固定秒数；状态退出=持续到当前状态退出。仅顶层霸体/无敌效果建议使用“状态退出”。")]
        public SkillEffectDurationMode durationMode = SkillEffectDurationMode.FixedTime;

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

        public bool SupportsUntilStateExitDuration =>
            effectType == PhysicsEffectType.SuperArmor
            || effectType == PhysicsEffectType.Invincible;
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

        [InspectorLabel("持续方式")]
        [Tooltip("指定时长=持续固定秒数；状态退出=持续到当前状态退出。仅对 HP/MP 以外的字段生效。")]
        public SkillEffectDurationMode durationMode = SkillEffectDurationMode.FixedTime;

        [InspectorLabel("Buff 持续时长(秒)")]
        [Tooltip("仅对 HP/MP 以外的字段生效。0 表示本局常驻；大于 0 表示临时 Buff。")]
        [Min(0f)] public float duration = 0f;

        public bool UsesDuration =>
            statField != SkillStatField.HP && statField != SkillStatField.MP;
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
        [Tooltip("仅“指定时长”模式使用。")]
        [Min(0f)] public float activeDuration = 0f;

        [InspectorLabel("持续方式")]
        [Tooltip("指定时长=重复触发一段固定时长；状态退出=只要当前状态未退出就持续重复触发。")]
        public SkillEventActiveDurationMode activeDurationMode = SkillEventActiveDurationMode.FixedTime;

        [InspectorLabel("重复触发间隔(秒)")]
        [Tooltip("仅 Repeated 模式使用。")]
        [Min(0.01f)] public float repeatInterval = 0.1f;

        public float GetEndTime()
        {
            return triggerMode == SkillEventTriggerMode.Repeated
                ? activeDurationMode == SkillEventActiveDurationMode.UntilStateExit
                    ? startTime
                    : startTime + Mathf.Max(0f, activeDuration)
                : startTime;
        }

        public bool RepeatsUntilStateExit =>
            triggerMode == SkillEventTriggerMode.Repeated &&
            activeDurationMode == SkillEventActiveDurationMode.UntilStateExit;
    }

    [Serializable]
    public class SkillDamageEvent : SkillTimedEventBase
    {
        [HideInInspector] public float hitStopDuration = 0f;
        [HideInInspector] public float hitStopTimeScale = 0f;
        [HideInInspector] public bool pauseCameraLookDuringHitStop = false;
        [HideInInspector] public bool hitStopSettingsOwnedByEvent = false;

        [InspectorLabel("命中效果")]
        [Tooltip("一条命中事件可以配置多条命中效果，用于同一帧多段命中。")]
        public List<SkillDamageEffect> damageEffects = new List<SkillDamageEffect>();

        public float GetLifecycleEndTime()
        {
            float maxTime = GetEndTime();
            if (damageEffects == null)
                return maxTime;

            float lastTriggerTime = SharedSkillDefinition.GetLastPossibleTriggerTime(this);

            for (int i = 0; i < damageEffects.Count; i++)
            {
                SkillDamageEffect effect = damageEffects[i];
                if (effect == null)
                    continue;

                maxTime = Mathf.Max(maxTime, lastTriggerTime + SharedSkillDefinition.GetDamageEffectLifetime(effect));
            }

            return maxTime;
        }

        public float GetDetectionEndTime() => GetLifecycleEndTime();

        public SkillDamageEffect GetPrimaryDamageEffect()
        {
            SkillDamageEffect primaryEffect = FindPrimaryDamageEffect();
            primaryEffect?.TryMigrateLegacySubEffects();
            return primaryEffect;
        }

        private SkillDamageEffect FindPrimaryDamageEffect()
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

        public bool TryMigrateLegacyHitStopSettings()
        {
            if (!hitStopSettingsOwnedByEvent || damageEffects == null || damageEffects.Count == 0)
                return false;

            SkillDamageEffect primaryEffect = FindPrimaryDamageEffect();
            if (primaryEffect == null)
                return false;

            primaryEffect.TryMigrateLegacySubEffects();
            SkillHitStopEffect hitStopEffect = primaryEffect.GetEffectiveHitStopEffect();
            if (!SkillDamageEffect.HasConfiguredHitStopEffect(hitStopEffect))
            {
                primaryEffect.onHitStopEffect = new SkillHitStopEffect
                {
                    hitStopDuration = Mathf.Max(0f, hitStopDuration),
                    hitStopTimeScale = Mathf.Clamp01(hitStopTimeScale),
                    pauseCameraLookDuringHitStop = pauseCameraLookDuringHitStop
                };
            }

            hitStopDuration = 0f;
            hitStopTimeScale = 0f;
            pauseCameraLookDuringHitStop = false;
            hitStopSettingsOwnedByEvent = false;
            return true;
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

        [InspectorLabel("技能效果描述")]
        [Tooltip("用于技能树变异面板展示该技能效果的说明。")]
        [TextArea(2, 5)]
        public string effectDescription = "";

        [InspectorLabel("变异天赋点消耗")]
        [Tooltip("在技能树中切换到该变异方向时需要消耗的天赋点数。")]
        [Min(0)]
        public int mutationTalentCost = 0;

        [InspectorLabel("显示名称")]
        [Tooltip("用于技能树变异面板展示该技能效果的名称。留空时回退为技能ID。")]
        [FormerlySerializedAs("displayName")]
        public string displayName = "";

        [InspectorLabel("动画 Trigger")]
        [Tooltip("该技能效果对应发送给 Animator 的 Trigger 名。留空时回退到外部配置或技能ID。")]
        public string animationTrigger = "";

        [Header("行为控制")]
        [InspectorLabel("忽略动画帧伤害事件")]
        [Tooltip("开启后仅使用时间轴事件结算技能效果。")]
        public bool ignoreAnimationDamageEvents = true;

        [Header("时间轴事件列表")]
        [InspectorLabel("命中事件列表")]
        [Tooltip("按时间顺序触发的命中事件节点。")]
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
            maxTime = Mathf.Max(maxTime, GetMaxPhysicsEndTime(physicsEvents));
            maxTime = Mathf.Max(maxTime, GetMaxAttributeEndTime(attributeEvents));
            maxTime = Mathf.Max(maxTime, GetMaxVfxEndTime(vfxEvents));
            maxTime = Mathf.Max(maxTime, GetMaxSfxEndTime(sfxEvents));
            return maxTime;
        }

        public string GetAnimationTriggerOrEmpty()
        {
            return string.IsNullOrWhiteSpace(animationTrigger)
                ? string.Empty
                : animationTrigger.Trim();
        }

        public string GetResolvedAnimationTrigger(string fallbackTrigger = null)
        {
            string resolvedTrigger = GetAnimationTriggerOrEmpty();
            if (!string.IsNullOrWhiteSpace(resolvedTrigger))
                return resolvedTrigger;

            if (!string.IsNullOrWhiteSpace(fallbackTrigger))
                return fallbackTrigger.Trim();

            if (!string.IsNullOrWhiteSpace(skillId))
                return skillId.Trim();

            return string.Empty;
        }

        public bool HasUntilStateExitEvents()
        {
            return HasUntilStateExitEvent(damageEvents)
                || HasUntilStateExitEvent(physicsEvents)
                || HasUntilStateExitEvent(attributeEvents)
                || HasUntilStateExitTopLevelEffects(physicsEvents)
                || HasUntilStateExitTopLevelEffects(attributeEvents)
                || HasUntilStateExitEvent(vfxEvents)
                || HasUntilStateExitEvent(sfxEvents);
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

        private static float GetMaxPhysicsEndTime(List<SkillPhysicsEvent> list)
        {
            return GetMaxTimedEventEndTime(list, GetPhysicsEventTailDuration);
        }

        private static float GetMaxAttributeEndTime(List<SkillAttributeEvent> list)
        {
            return GetMaxTimedEventEndTime(list, GetAttributeEventTailDuration);
        }

        private static float GetMaxVfxEndTime(List<SkillVfxEvent> list)
        {
            return GetMaxTimedEventEndTime(list, GetVfxEventTailDuration);
        }

        private static float GetMaxSfxEndTime(List<SkillSfxEvent> list)
        {
            return GetMaxTimedEventEndTime(list, GetSfxEventTailDuration);
        }

        private static float GetMaxTimedEventEndTime<T>(List<T> list, Func<T, float> getTailDuration)
            where T : SkillTimedEventBase
        {
            float maxTime = 0f;
            if (list == null)
                return maxTime;

            for (int i = 0; i < list.Count; i++)
            {
                T evt = list[i];
                if (evt == null)
                    continue;

                maxTime = Mathf.Max(maxTime, GetLastPossibleTriggerTime(evt) + Mathf.Max(0f, getTailDuration(evt)));
            }

            return maxTime;
        }

        private static bool HasUntilStateExitEvent<T>(List<T> list) where T : SkillTimedEventBase
        {
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                T evt = list[i];
                if (evt != null && evt.RepeatsUntilStateExit)
                    return true;
            }

            return false;
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
                    maxTime = Mathf.Max(maxTime, evt.GetLifecycleEndTime());
            }

            return maxTime;
        }

        public static float GetLastPossibleTriggerTime(SkillTimedEventBase evt)
        {
            if (evt == null)
                return 0f;

            if (evt.triggerMode != SkillEventTriggerMode.Repeated)
                return evt.startTime;

            if (evt.RepeatsUntilStateExit)
                return evt.startTime;

            float interval = Mathf.Max(0.01f, evt.repeatInterval);
            float activeDuration = Mathf.Max(0f, evt.activeDuration);
            int repeatCount = Mathf.FloorToInt(activeDuration / interval);
            return evt.startTime + repeatCount * interval;
        }

        public static float GetDamageEventTailDuration(SkillDamageEvent evt)
        {
            if (evt?.damageEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.damageEffects.Count; i++)
            {
                SkillDamageEffect effect = evt.damageEffects[i];
                if (effect != null)
                    duration = Mathf.Max(duration, GetDamageEffectLifetime(effect));
            }

            return duration;
        }

        public static float GetDamageEffectLifetime(SkillDamageEffect effect)
        {
            if (effect == null)
                return 0f;

            return Mathf.Max(0f, effect.detectionDuration) + GetDamageOnHitTailDuration(effect);
        }

        public static float GetDamageOnHitTailDuration(SkillDamageEffect effect)
        {
            if (effect == null)
                return 0f;

            float duration = 0f;
            if (effect.onHitPhysicsEffects != null)
            {
                for (int i = 0; i < effect.onHitPhysicsEffects.Count; i++)
                    duration = Mathf.Max(duration, GetPhysicsEffectLifetime(effect.onHitPhysicsEffects[i]));
            }

            if (effect.onHitAttributeEffects != null)
            {
                for (int i = 0; i < effect.onHitAttributeEffects.Count; i++)
                    duration = Mathf.Max(duration, GetAttributeEffectLifetime(effect.onHitAttributeEffects[i]));
            }

            if (effect.onHitVfxEffects != null)
            {
                for (int i = 0; i < effect.onHitVfxEffects.Count; i++)
                    duration = Mathf.Max(duration, GetVfxEffectLifetime(effect.onHitVfxEffects[i]));
            }

            if (effect.onHitSfxEffects != null)
            {
                for (int i = 0; i < effect.onHitSfxEffects.Count; i++)
                    duration = Mathf.Max(duration, GetSfxEffectLifetime(effect.onHitSfxEffects[i]));
            }

            return duration;
        }

        public static float GetPhysicsEventTailDuration(SkillPhysicsEvent evt)
        {
            if (evt?.physicsEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.physicsEffects.Count; i++)
                duration = Mathf.Max(duration, GetPhysicsEffectLifetime(evt.physicsEffects[i]));

            return duration;
        }

        public static float GetAttributeEventTailDuration(SkillAttributeEvent evt)
        {
            if (evt?.attributeEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.attributeEffects.Count; i++)
                duration = Mathf.Max(duration, GetAttributeEffectLifetime(evt.attributeEffects[i]));

            return duration;
        }

        public static float GetVfxEventTailDuration(SkillVfxEvent evt)
        {
            if (evt?.vfxEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.vfxEffects.Count; i++)
                duration = Mathf.Max(duration, GetVfxEffectLifetime(evt.vfxEffects[i]));

            return duration;
        }

        public static float GetSfxEventTailDuration(SkillSfxEvent evt)
        {
            if (evt?.sfxEffects == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < evt.sfxEffects.Count; i++)
                duration = Mathf.Max(duration, GetSfxEffectLifetime(evt.sfxEffects[i]));

            return duration;
        }

        public static float GetPhysicsEffectLifetime(SkillPhysicsEffect effect)
        {
            if (effect == null)
                return 0f;

            return effect.durationMode == SkillEffectDurationMode.UntilStateExit
                ? 0f
                : Mathf.Max(0f, effect.duration);
        }

        public static float GetAttributeEffectLifetime(SkillAttributeEffect effect)
        {
            if (effect == null)
                return 0f;

            return effect.statField == SkillStatField.HP || effect.statField == SkillStatField.MP
                ? 0f
                : effect.durationMode == SkillEffectDurationMode.UntilStateExit
                    ? 0f
                    : Mathf.Max(0f, effect.duration);
        }

        private static bool HasUntilStateExitTopLevelEffects(List<SkillPhysicsEvent> list)
        {
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                SkillPhysicsEvent evt = list[i];
                if (evt?.physicsEffects == null)
                    continue;

                for (int effectIndex = 0; effectIndex < evt.physicsEffects.Count; effectIndex++)
                {
                    SkillPhysicsEffect effect = evt.physicsEffects[effectIndex];
                    if (effect != null
                        && effect.SupportsUntilStateExitDuration
                        && effect.durationMode == SkillEffectDurationMode.UntilStateExit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasUntilStateExitTopLevelEffects(List<SkillAttributeEvent> list)
        {
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                SkillAttributeEvent evt = list[i];
                if (evt?.attributeEffects == null)
                    continue;

                for (int effectIndex = 0; effectIndex < evt.attributeEffects.Count; effectIndex++)
                {
                    SkillAttributeEffect effect = evt.attributeEffects[effectIndex];
                    if (effect != null
                        && effect.UsesDuration
                        && effect.durationMode == SkillEffectDurationMode.UntilStateExit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static float GetVfxEffectLifetime(SkillVfxEffect effect)
        {
            if (effect == null)
                return 0f;

            if (effect.destroyMode == SkillCueDestroyMode.OnStateExit)
                return 0f;

            if (effect.destroyMode == SkillCueDestroyMode.Timed && effect.duration > 0f)
                return effect.duration;

            return EstimateParticlePrefabDuration(effect.particlePrefab);
        }

        public static float GetSfxEffectLifetime(SkillSfxEffect effect)
        {
            if (effect == null)
                return 0f;

            if (effect.destroyMode == SkillCueDestroyMode.OnStateExit)
                return 0f;

            if (effect.loop && effect.destroyMode == SkillCueDestroyMode.NaturalDestroy)
                return 0f;

            if (effect.destroyMode == SkillCueDestroyMode.Timed && effect.duration > 0f)
                return effect.duration;

            return effect.audioClip != null ? effect.audioClip.length : 0f;
        }

        private static float EstimateParticlePrefabDuration(GameObject prefab)
        {
            if (prefab == null)
                return 0f;

            float duration = 0f;
            ParticleSystem[] particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                duration = Mathf.Max(duration, main.duration + GetMaxCurveValue(main.startLifetime));
            }

            return duration;
        }

        private static float GetMaxCurveValue(ParticleSystem.MinMaxCurve curve)
        {
            return curve.mode switch
            {
                ParticleSystemCurveMode.Constant => curve.constant,
                ParticleSystemCurveMode.TwoConstants => Mathf.Max(curve.constantMin, curve.constantMax),
                ParticleSystemCurveMode.Curve => curve.curve != null && curve.curve.length > 0 ? curve.curve.keys[curve.curve.length - 1].value : 0f,
                ParticleSystemCurveMode.TwoCurves => Mathf.Max(
                    curve.curveMin != null && curve.curveMin.length > 0 ? curve.curveMin.keys[curve.curveMin.length - 1].value : 0f,
                    curve.curveMax != null && curve.curveMax.length > 0 ? curve.curveMax.keys[curve.curveMax.length - 1].value : 0f),
                _ => 0f
            };
        }
    }
}
