using UnityEngine;
using Game.Data;

namespace Game
{
    /// <summary>
    /// 敌人死亡事件参数，供 EventCenter.EventTrigger(GameEvents.EnemyDied, args) 使用。
    /// </summary>
    public class EnemyDiedArgs
    {
        public string    EnemyId;
        public Vector3   Position;
        public EnemyType Type;
    }
}
