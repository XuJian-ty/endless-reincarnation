using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 敌人当前意图，由行为树写入、Mover/Combat 读取执行。
    /// </summary>
    public struct EnemyIntent
    {
        public static readonly EnemyIntent None = new EnemyIntent { Type = EnemyIntentType.None };

        public EnemyIntentType Type;
        /// <summary>移动/追击目标位置；CastSkill 时可为 null</summary>
        public Vector3? TargetPosition;
        /// <summary>CastSkill 时的技能槽位 0～3</summary>
        public int SkillSlot;

        public bool HasTargetPosition => TargetPosition.HasValue;
    }

    /// <summary>
    /// 敌人意图类型，与设计文档一致。
    /// </summary>
    public enum EnemyIntentType
    {
        None,
        Patrol,
        Chase,
        Approach,
        Retreat,
        Reposition,
        CastSkill,
        Hurt,
        Idle,
        Dead,
    }
}
