using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 地面掉落物显示配置：控制图标缩放、悬浮效果和拾取碰撞范围。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/掉落物显示配置", fileName = "掉落物显示配置")]
    public class WorldPickupVisualConfigSO : ScriptableObject
    {
        [InspectorLabel("图标缩放")]
        [Min(0.01f)]
        public float iconScale = 0.2f;

        [InspectorLabel("生成高度")]
        public float spawnHeightOffset = 0.5f;

        [InspectorLabel("悬浮幅度")]
        [Min(0f)]
        public float hoverAmplitude = 0.08f;

        [InspectorLabel("悬浮频率")]
        [Min(0f)]
        public float hoverFrequency = 2.4f;

        [InspectorLabel("拾取半径")]
        [Min(0.01f)]
        public float pickupRadius = 0.7f;
    }
}
