using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 伤害检测方式：范围 Overlap、碰撞体事件、射线/SphereCast。
    /// 共用字段（倍率、层级、击退）一致，仅检测逻辑与专用参数不同。
    /// </summary>
    public enum DamageDetectionType
    {
        [Tooltip("范围检测：球体/扇形/盒体 Overlap，单帧或帧区间内执行一次")]
        RangeOverlap,

        [Tooltip("碰撞检测：在指定帧区间内启用某子物体上的 Collider，由 OnTriggerEnter 等回调命中后按伤害名结算")]
        Collision,

        [Tooltip("射线检测：子弹等，从起点沿方向 Raycast/SphereCast")]
        Raycast,
    }

    /// <summary>
    /// 范围检测时的形状，仅在 RangeOverlap 时使用。
    /// </summary>
    public enum AttackShapeType
    {
        Sector,
        Sphere,
        Box,
    }

    /// <summary>
    /// 单次伤害检测配置。
    ///
    /// 使用方式：
    /// 1. 时间轴技能事件在 damageEffects 列表中填写 damageName，运行时按名称查这份表。
    /// 2. 动画事件 AnimEvent_DealDamage 传入名称，运行时按名称查这份表。
    ///
    /// 真正的最终伤害仍由战斗执行层决定：
    /// CombatCalculator(攻击力, 防御力) × damageMultiplier × 事件伤害倍率。
    /// </summary>
    [Serializable]
    public class AnimationFrameDamageEntry
    {
        [Header("标识")]
        [InspectorLabel("伤害名")]
        [Tooltip("全局唯一字符串，必须与技能事件 damageEffects 中的 damageName 或动画事件参数完全一致。")]
        public string damageName = "A1";

        [Header("检测方式")]
        [InspectorLabel("检测方式")]
        [Tooltip("范围=Overlap 单帧检测；碰撞=帧区间内启用 Collider；射线=Raycast 或 SphereCast。")]
        public DamageDetectionType detectionType = DamageDetectionType.RangeOverlap;

        [Header("通用参数")]
        [InspectorLabel("伤害倍率")]
        [Tooltip("乘以 CombatCalculator 基础伤害的系数，最终还会再乘以技能事件中对应伤害效果的 damageMagnitude。")]
        public float damageMultiplier = 1f;

        [InspectorLabel("命中层级名")]
        [Tooltip("例如 Enemy 或 Player。")]
        public string hitLayerName = "Enemy";

        [InspectorLabel("击退力")]
        public float pushForce = 20f;

        [InspectorLabel("击退时长")]
        public float pushDuration = 0.04f;

        [Header("范围检测参数")]
        [InspectorLabel("形状")]
        public AttackShapeType shape = AttackShapeType.Sphere;

        [InspectorLabel("中心偏移 X")]
        public float centerOffsetX;

        [InspectorLabel("中心偏移 Y")]
        public float centerOffsetY;

        [InspectorLabel("中心偏移 Z")]
        public float centerOffsetZ;

        [InspectorLabel("球体半径")]
        public float sphereRadius = 1.5f;

        [InspectorLabel("扇形角度")]
        [Range(1f, 360f)] public float sectorAngle = 180f;

        [InspectorLabel("盒体 X")]
        public float boxSizeX = 1f;

        [InspectorLabel("盒体 Y")]
        public float boxSizeY = 1f;

        [InspectorLabel("盒体 Z")]
        public float boxSizeZ = 1f;

        [Header("碰撞检测参数")]
        [InspectorLabel("碰撞体挂点名")]
        [Tooltip("攻击者子物体的名称，运行时会通过 transform.Find(挂点名) 查找。")]
        public string colliderNodeName = "";

        [Header("射线检测参数")]
        [InspectorLabel("起点偏移 X")]
        public float rayOriginOffsetX;

        [InspectorLabel("起点偏移 Y")]
        public float rayOriginOffsetY;

        [InspectorLabel("起点偏移 Z")]
        public float rayOriginOffsetZ;

        [InspectorLabel("最大距离")]
        [Tooltip("射线最大检测距离；方向由调用方提供。")]
        public float rayMaxDistance = 50f;

        public Vector3 CenterOffset => new Vector3(centerOffsetX, centerOffsetY, centerOffsetZ);
        public Vector3 BoxSize => new Vector3(boxSizeX, boxSizeY, boxSizeZ);
        public Vector3 RayOriginOffset => new Vector3(rayOriginOffsetX, rayOriginOffsetY, rayOriginOffsetZ);
    }

    /// <summary>
    /// 技能伤害数据配置库：按伤害名取一条；支持范围、碰撞、射线三种检测方式。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/技能伤害数据配置库", fileName = "技能伤害数据配置库")]
    public class AnimationFrameDamageDatabaseSO : ScriptableObject
    {
        [InspectorLabel("伤害数据列表")]
        [Tooltip("每条对应一个伤害名；检测方式可选范围、碰撞或射线。")]
        public List<AnimationFrameDamageEntry> entries = new List<AnimationFrameDamageEntry>();

        public AnimationFrameDamageEntry GetEntry(string damageName)
        {
            if (entries == null || string.IsNullOrEmpty(damageName))
                return null;

            foreach (var entry in entries)
            {
                if (entry != null && entry.damageName == damageName)
                    return entry;
            }

            return null;
        }
    }
}
