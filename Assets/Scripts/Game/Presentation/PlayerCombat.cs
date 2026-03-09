using System.Collections.Generic;
using UnityEngine;
using Game.Domain;
using Game.Data;
using Game.Presentation;
using Game;

namespace Game.Presentation
{
    /// <summary>
    /// 玩家攻击命中判定：订阅单一动画事件 OnDealDamage(damageName)，用配置库做检测并结算伤害与吸血。
    /// 动画里在命中帧添加 AnimEvent_DealDamage，String 参数填配置中的伤害名即可。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("无配置时的回退检测")]
        [Tooltip("回退时检测球心相对角色位置的向前偏移（米）")]
        [SerializeField] private float _fallbackOriginForward = 0.5f;
        [Tooltip("回退时检测球半径（米）")]
        [SerializeField] private float _fallbackRadius = 1.5f;
        [Tooltip("回退时检测的层级名")]
        [SerializeField] private string _fallbackEnemyLayerName = "Enemy";

        private IPlayerContext _ctx;
        private PlayerAnimatorController _anim;
        private int _fallbackLayerMask;
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private readonly HashSet<EnemyController> _hitSet = new HashSet<EnemyController>();
        private System.Random _rng;

        private void Awake()
        {
            var controller = GetComponent<PlayerController>();
            _ctx = controller;
            _anim = GetComponent<PlayerAnimatorController>();
            _fallbackLayerMask = LayerMask.GetMask(_fallbackEnemyLayerName);
            _rng = new System.Random();
            if (_fallbackLayerMask == 0)
                Debug.LogWarning($"[PlayerCombat] Layer '{_fallbackEnemyLayerName}' 不存在，回退检测将无命中。");
        }

        private void OnEnable()
        {
            if (_anim != null)
                _anim.OnDealDamage += OnDealDamage;
        }

        private void OnDisable()
        {
            if (_anim != null)
                _anim.OnDealDamage -= OnDealDamage;
        }

        private void OnDealDamage(string damageName)
        {
            if (_ctx?.PlayerModel == null) return;
            var database = ConfigManager.GetInstance().GetAnimationFrameDamageDatabase();

            if (!string.IsNullOrEmpty(damageName) && database != null &&
                DamageDetectionRunner.TryRunDetection(damageName, transform, database, _overlapBuffer, out var entry, out int hitCount) &&
                entry != null && hitCount > 0)
            {
                ApplyDamageToHitColliders(hitCount, entry.damageMultiplier);
                return;
            }

            // 无配置或 Collision 类型：回退到固定球体检测、倍率 1
            int fallbackCount = RunFallbackOverlap();
            ApplyDamageToHitColliders(fallbackCount, 1f);
        }

        private void ApplyDamageToHitColliders(int bufferCount, float skillMultiplier)
        {
            _hitSet.Clear();
            if (bufferCount > 0)
            {
                for (int i = 0; i < bufferCount; i++)
                {
                    var enemy = _overlapBuffer[i].GetComponentInParent<EnemyController>();
                    if (enemy != null)
                        _hitSet.Add(enemy);
                }
            }

            foreach (var enemy in _hitSet)
            {
                CombatCalculator.CalculateDamage(
                    _ctx.PlayerModel.Stats,
                    skillMultiplier,
                    enemy.Defense,
                    _rng,
                    out float finalDamage,
                    out _,
                    out float lifeStealHeal);

                bool killed = enemy.ApplyDamage(finalDamage);
                _ctx.PlayerModel.Heal(lifeStealHeal);
            }
        }

        private int RunFallbackOverlap()
        {
            if (_fallbackLayerMask == 0) return 0;
            Vector3 origin = transform.position + transform.forward * _fallbackOriginForward;
            return Physics.OverlapSphereNonAlloc(origin, _fallbackRadius, _overlapBuffer, _fallbackLayerMask);
        }
    }
}
