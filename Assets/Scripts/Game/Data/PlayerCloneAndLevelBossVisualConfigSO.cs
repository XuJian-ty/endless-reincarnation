using UnityEngine;

namespace Game.Data
{
    [System.Serializable]
    public sealed class PlayerCloneVisualSettings
    {
        [InspectorLabel("染色颜色")]
        [Tooltip("分身整体幽灵化染色颜色。")]
        public Color tintColor = new Color(0.42f, 0.76f, 1f, 1f);

        [InspectorLabel("发光颜色")]
        [Tooltip("分身材质发光与点光源的主颜色。")]
        public Color emissionColor = new Color(0.16f, 0.58f, 1f, 1f);

        [InspectorLabel("最小透明度")]
        [Range(0f, 1f)]
        [Tooltip("分身最终显示时允许的最低透明度。")]
        public float minOpacity = 0.56f;

        [InspectorLabel("敏感部位透明系数")]
        [Range(0f, 1f)]
        [Tooltip("身体与内衣等敏感材质额外乘上的透明系数。")]
        public float sensitiveBodyOpacityScale = 0.58f;

        [InspectorLabel("常规染色强度")]
        [Range(0f, 1f)]
        [Tooltip("普通材质向分身染色颜色靠拢的强度。")]
        public float tintStrength = 0.62f;

        [InspectorLabel("敏感部位染色强度")]
        [Range(0f, 1f)]
        [Tooltip("身体与内衣等敏感材质向分身染色颜色靠拢的强度。")]
        public float sensitiveBodyTintStrength = 0.88f;

        [InspectorLabel("常规发光强度")]
        [Min(0f)]
        [Tooltip("普通材质的发光强度。")]
        public float emissionStrength = 1.25f;

        [InspectorLabel("敏感部位发光强度")]
        [Min(0f)]
        [Tooltip("身体与内衣等敏感材质的发光强度。")]
        public float sensitiveBodyEmissionStrength = 1.7f;

        [InspectorLabel("光照偏移")]
        [Tooltip("分身点光源相对根节点的位置偏移。")]
        public Vector3 lightOffset = new Vector3(0f, 1.35f, 0f);

        [InspectorLabel("最小亮度")]
        [Min(0f)]
        [Tooltip("分身蓝光脉冲的最小亮度。")]
        public float minLightIntensity = 0.85f;

        [InspectorLabel("最大亮度")]
        [Min(0f)]
        [Tooltip("分身蓝光脉冲的最大亮度。")]
        public float maxLightIntensity = 1.75f;

        [InspectorLabel("脉冲速度")]
        [Min(0.1f)]
        [Tooltip("分身蓝光脉冲速度。")]
        public float lightPulseSpeed = 2.2f;

        [InspectorLabel("光照范围")]
        [Min(0.1f)]
        [Tooltip("分身蓝光覆盖范围。")]
        public float lightRange = 4.8f;
    }

    [System.Serializable]
    public sealed class LevelBossVisualSettings
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

    /// <summary>
    /// 玩家分身和最终 Boss 视觉配置：统一控制分身透明发光表现与最终 Boss 视觉标记表现。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/玩家分身和最终Boss视觉配置", fileName = "玩家分身和最终Boss视觉配置")]
    public class PlayerCloneAndLevelBossVisualConfigSO : ScriptableObject
    {
        [Header("玩家分身")]
        public PlayerCloneVisualSettings playerClone = new PlayerCloneVisualSettings();

        [Header("最终Boss")]
        public LevelBossVisualSettings levelBoss = new LevelBossVisualSettings();
    }
}
