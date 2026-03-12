using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 技能执行上下文接口：为 <see cref="SkillEffectExecutor"/> 提供施法者信息，
    /// 与具体角色类型（玩家/敌人）解耦。
    /// </summary>
    public interface ISkillExecutionContext
    {
        /// <summary>施法者 Transform。</summary>
        Transform CasterTransform { get; }

        /// <summary>施法者攻击力，用于伤害计算。</summary>
        float CasterAttack { get; }

        /// <summary>共享碰撞体缓冲区，避免频繁分配。</summary>
        Collider[] OverlapBuffer { get; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  EnemySkillExecutionContext
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>敌人技能执行上下文，持有 EnemyController 引用。</summary>
    public sealed class EnemySkillExecutionContext : ISkillExecutionContext
    {
        private readonly EnemyController _controller;
        private readonly EnemyPerception _perception;
        private readonly Collider[] _overlapBuffer;

        public EnemySkillExecutionContext(
            EnemyController controller,
            EnemyPerception perception,
            Collider[] overlapBuffer)
        {
            _controller = controller;
            _perception = perception;
            _overlapBuffer = overlapBuffer;
        }

        public Transform CasterTransform => _controller != null ? _controller.transform : null;

        public float CasterAttack => _controller != null ? _controller.Attack : 0f;

        public Collider[] OverlapBuffer => _overlapBuffer;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PlayerSkillExecutionContext
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>玩家技能执行上下文，持有 PlayerController 引用。</summary>
    public sealed class PlayerSkillExecutionContext : ISkillExecutionContext
    {
        private static readonly Collider[] SharedBuffer = new Collider[32];

        private readonly PlayerController _player;

        public PlayerSkillExecutionContext(PlayerController player)
        {
            _player = player;
        }

        public Transform CasterTransform => _player != null ? _player.transform : null;

        public float CasterAttack => _player != null ? _player.PlayerModel.Stats.Attack : 0f;

        public Collider[] OverlapBuffer => SharedBuffer;
    }
}
