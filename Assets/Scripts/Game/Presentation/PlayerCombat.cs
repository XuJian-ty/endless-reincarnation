using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 玩家攻击已统一由技能时间轴执行。
    /// 本组件保留为兼容占位，避免场景或预制体上已有引用时报缺失。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
    }
}
