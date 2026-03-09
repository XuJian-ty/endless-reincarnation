using UnityEngine;
using Game.Data;

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

        /// <summary>当前目标 Transform（无目标时为 null）。</summary>
        Transform CurrentTarget { get; }

        /// <summary>施法者攻击力，用于伤害计算。</summary>
        float CasterAttack { get; }

        /// <summary>伤害检测数据库。</summary>
        AnimationFrameDamageDatabaseSO DamageDatabase { get; }

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
        private readonly AnimationFrameDamageDatabaseSO _damageDatabase;
        private readonly Collider[] _overlapBuffer;

        public EnemySkillExecutionContext(
            EnemyController controller,
            EnemyPerception perception,
            AnimationFrameDamageDatabaseSO damageDatabase,
            Collider[] overlapBuffer)
        {
            _controller = controller;
            _perception = perception;
            _damageDatabase = damageDatabase;
            _overlapBuffer = overlapBuffer;
        }

        public Transform CasterTransform => _controller != null ? _controller.transform : null;

        public Transform CurrentTarget
        {
            get
            {
                if (_perception != null && _perception.CurrentTarget != null)
                    return _perception.CurrentTarget;
                return GameFlow.GameStateMachine.GetInstance()?.LevelPlayerTransform;
            }
        }

        public float CasterAttack => _controller != null ? _controller.Attack : 0f;

        public AnimationFrameDamageDatabaseSO DamageDatabase =>
            _damageDatabase ?? ConfigManager.GetInstance()?.GetAnimationFrameDamageDatabase();

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
        private readonly AnimationFrameDamageDatabaseSO _damageDatabase;

        public PlayerSkillExecutionContext(
            PlayerController player,
            AnimationFrameDamageDatabaseSO damageDatabase = null)
        {
            _player = player;
            _damageDatabase = damageDatabase;
        }

        public Transform CasterTransform => _player != null ? _player.transform : null;

        public Transform CurrentTarget
        {
            get
            {
                // 返回场景中最近的存活敌人（简单策略，可扩展为锁定目标）
                var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
                Transform nearest = null;
                float minDist = float.MaxValue;
                Vector3 pos = CasterTransform != null ? CasterTransform.position : Vector3.zero;
                for (int i = 0; i < enemies.Length; i++)
                {
                    if (enemies[i] == null || !enemies[i].IsAlive) continue;
                    float d = (enemies[i].transform.position - pos).sqrMagnitude;
                    if (d < minDist)
                    {
                        minDist = d;
                        nearest = enemies[i].transform;
                    }
                }
                return nearest;
            }
        }

        public float CasterAttack => _player != null ? _player.PlayerModel.Stats.Attack : 0f;

        public AnimationFrameDamageDatabaseSO DamageDatabase =>
            _damageDatabase ?? ConfigManager.GetInstance()?.GetAnimationFrameDamageDatabase();

        public Collider[] OverlapBuffer => SharedBuffer;
    }
}
