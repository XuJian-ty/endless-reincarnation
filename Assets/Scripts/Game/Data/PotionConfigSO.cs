using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 药水效果配置：使用后每秒回复最大生命/法力百分比，持续时长。
    /// 需求书：每秒 5% 最大生命或法力，持续 10 秒（总回复约 50%）。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/药水效果", fileName = "药水效果")]
    public class PotionConfigSO : ScriptableObject
    {
        [Header("回复比例（每秒）")]
        [InspectorLabel("生命回复比例/秒")]
        [Tooltip("如 0.05 表示每秒回复 5% 最大生命")]
        [Range(0.01f, 0.5f)]
        public float hpPercentPerSecond = 0.05f;

        [InspectorLabel("法力回复比例/秒")]
        [Tooltip("如 0.05 表示每秒回复 5% 最大法力")]
        [Range(0.01f, 0.5f)]
        public float mpPercentPerSecond = 0.05f;

        [Header("持续时间")]
        [InspectorLabel("持续时间（秒）")]
        [Min(0.1f)]
        public float durationSeconds = 10f;
    }
}
