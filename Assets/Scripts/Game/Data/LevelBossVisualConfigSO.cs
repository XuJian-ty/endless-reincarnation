using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 最终 Boss 视觉配置：控制真正决定关卡胜负的 Boss 体型与光照标记表现。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/最终Boss视觉配置", fileName = "最终Boss视觉配置")]
    public class LevelBossVisualConfigSO : ScriptableObject
    {
        [InspectorLabel("体型倍率")]
        [Min(0.1f)]
        [Tooltip("最终 Boss 相对普通 Boss 的整体缩放倍率。")]
        public float scaleMultiplier = 3f;

        [InspectorLabel("光照颜色")]
        [Tooltip("最终 Boss 光照标记颜色。")]
        public Color markerColor = new Color(1f, 0.18f, 0.18f, 1f);

        [InspectorLabel("最小亮度")]
        [Min(0f)]
        [Tooltip("光照脉冲的最小亮度。")]
        public float minIntensity = 2.6f;

        [InspectorLabel("最大亮度")]
        [Min(0f)]
        [Tooltip("光照脉冲的最大亮度。")]
        public float maxIntensity = 5.4f;

        [InspectorLabel("脉冲速度")]
        [Min(0.1f)]
        [Tooltip("光照脉冲速度。")]
        public float pulseSpeed = 2f;

        [InspectorLabel("光照范围")]
        [Min(0.1f)]
        [Tooltip("光照覆盖范围。")]
        public float range = 11f;

        [InspectorLabel("光照偏移")]
        [Tooltip("光照相对最终 Boss 根节点的位置偏移。")]
        public Vector3 offset = new Vector3(0f, 4.2f, 0f);
    }
}
