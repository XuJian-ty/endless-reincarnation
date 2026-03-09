using UnityEngine;
using Game.Data;
using Game.Presentation;

namespace Game.AI
{
    /// <summary>
    /// 行为树每帧 Tick 时使用的上下文，由 EnemyAI 填充。
    /// </summary>
    public class EnemyAIContext
    {
        public EnemyController Controller;
        public EnemyPerception Perception;
        public EnemyArchetypeSO Archetype;
        /// <summary>巡逻起点（通常为出生点）</summary>
        public Vector3 PatrolOrigin;
        /// <summary>下次可执行巡逻的时间戳，由 Patrol 动作节点写入</summary>
        public float NextPatrolTime;
    }
}
