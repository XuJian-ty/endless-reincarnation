using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 难度放大系数配置：每提升 1 点难度，怪物各属性按此比例递增。
    /// 默认：HP +30%、攻击 +30%、防御 +20%、移速 +10%。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/难度系数", fileName = "难度系数")]
    public class DifficultyScalingSO : ScriptableObject
    {
        [InspectorLabel("生命放大比例")]
        [Tooltip("每升 1 难度 HP 放大比例，0.3 = +30%")]
        public float hpScale        = 0.3f;
        [InspectorLabel("攻击放大比例")]
        [Tooltip("每升 1 难度攻击力放大比例")]
        public float attackScale    = 0.3f;
        [InspectorLabel("防御放大比例")]
        [Tooltip("每升 1 难度防御力放大比例")]
        public float defenseScale   = 0.2f;
        [InspectorLabel("移速放大比例")]
        [Tooltip("每升 1 难度移速放大比例")]
        public float moveSpeedScale = 0.1f;
    }
}
