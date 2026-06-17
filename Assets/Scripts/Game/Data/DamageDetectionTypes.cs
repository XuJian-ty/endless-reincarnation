using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 伤害检测方式：范围 Overlap、碰撞体事件、射线/SphereCast。
    /// 共用字段（倍率、层级、击退）一致，仅检测逻辑与专用参数不同。
    /// </summary>
    public enum DamageDetectionType
    {
        [InspectorName("范围检测")]
        [Tooltip("范围检测：球体/扇形/盒体 Overlap。在检测时长内每帧重做一次检测，同一目标在同一检测窗口内只会命中第一次。")]
        RangeOverlap,

        [InspectorName("碰撞检测")]
        [Tooltip("碰撞检测：在检测时长内启用某子物体上的 Trigger Collider，由该武器命中盒组件通过 OnTriggerEnter 上报首次进入的目标。")]
        Collision,

        [InspectorName("射线检测")]
        [Tooltip("射线检测：在检测时长内每帧从起点沿前方重新 Raycast，同一目标在同一检测窗口内只会命中第一次。")]
        Raycast,
    }

    /// <summary>
    /// 范围检测时的形状，仅在 RangeOverlap 时使用。
    /// </summary>
    public enum AttackShapeType
    {
        [InspectorName("扇形")]
        Sector,
        [InspectorName("球形")]
        Sphere,
        [InspectorName("盒体")]
        Box,
    }
}
