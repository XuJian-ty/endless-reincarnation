using System.Collections.Generic;
using Game.Domain;
using Game.Presentation;
using UnityEngine;

namespace Game.Online
{
    public sealed class OnlineDungeonRemotePlayerTarget : MonoBehaviour, ICombatHardControlReceiver
    {
        private static readonly List<OnlineDungeonRemotePlayerTarget> ActiveTargets = new List<OnlineDungeonRemotePlayerTarget>();

        [SerializeField] private string _userId;
        private float _defense;
        private float _damageReduce;
        private float _currentHp = 1f;
        private float _maxHp = 1f;
        private bool _isDead;

        public string UserId => _userId;
        public static IReadOnlyList<OnlineDungeonRemotePlayerTarget> ActiveRemoteTargets => ActiveTargets;

        public void Initialize(string userId)
        {
            _userId = string.IsNullOrWhiteSpace(userId) ? string.Empty : userId.Trim();
        }

        public void ApplyStats(float defense, float damageReduce, float currentHp, float maxHp, bool isDead)
        {
            _defense = Mathf.Max(0f, defense);
            _damageReduce = Mathf.Clamp01(damageReduce);
            _maxHp = Mathf.Max(1f, maxHp);
            _currentHp = Mathf.Clamp(currentHp, 0f, _maxHp);
            _isDead = isDead || _currentHp <= 0f;
        }

        public void ApplyHardControl(float duration)
        {
        }

        public void ReceiveDamage(float casterAttack, float casterDamageBonus, float damageMultiplier, float stunDuration)
        {
            if (_isDead)
                return;

            float damage = CombatCalculator.CalculateDamageFromEnemy(
                casterAttack,
                _defense,
                casterDamageBonus,
                _damageReduce) * Mathf.Max(0f, damageMultiplier);
            OnlineDungeonSessionCoordinator.GetInstance().TryReportRemotePlayerDamage(_userId, damage, transform.position);
        }

        private void OnEnable()
        {
            if (!ActiveTargets.Contains(this))
                ActiveTargets.Add(this);
        }

        private void OnDisable()
        {
            ActiveTargets.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveTargets.Remove(this);
        }
    }
}
