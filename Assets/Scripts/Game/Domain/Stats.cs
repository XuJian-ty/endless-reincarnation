using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Domain
{
    [Serializable]
    public class StatModifier
    {
        public float hpAdd;
        public float mpAdd;
        public float attackAdd;
        public float defenseAdd;
        public float lifeStealAdd;
        public float critRateAdd;
        public float critDmgAdd;
        public float attackSpeedAdd;
        public float moveSpeedAdd;
        public float hpRegenAdd;
        public float mpRegenAdd;
        public float damageBonusAdd;

        public StatModifier Clone()
        {
            return new StatModifier
            {
                hpAdd = hpAdd,
                mpAdd = mpAdd,
                attackAdd = attackAdd,
                defenseAdd = defenseAdd,
                lifeStealAdd = lifeStealAdd,
                critRateAdd = critRateAdd,
                critDmgAdd = critDmgAdd,
                attackSpeedAdd = attackSpeedAdd,
                moveSpeedAdd = moveSpeedAdd,
                hpRegenAdd = hpRegenAdd,
                mpRegenAdd = mpRegenAdd,
                damageBonusAdd = damageBonusAdd
            };
        }
    }

    public class Stats
    {
        public float baseHp;
        public float baseMp;
        public float baseAttack;
        public float baseDefense;
        public float baseLifeSteal;
        public float baseCritRate;
        public float baseCritDmg;
        public float baseAttackSpeed;
        public float baseMoveSpeed;
        public float baseHpRegen;
        public float baseMpRegen;
        public float baseDamageBonus;

        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private bool _dirty = true;

        private float _maxHp;
        private float _maxMp;
        private float _attack;
        private float _defense;
        private float _lifeSteal;
        private float _critRate;
        private float _critDmg;
        private float _attackSpeed;
        private float _moveSpeed;
        private float _hpRegen;
        private float _mpRegen;
        private float _damageBonus;

        public void AddModifier(StatModifier mod)
        {
            if (mod == null) return;
            _modifiers.Add(mod);
            _dirty = true;
        }

        public void RemoveModifier(StatModifier mod)
        {
            if (mod == null) return;
            if (_modifiers.Remove(mod))
                _dirty = true;
        }

        public void InvalidateCache() => _dirty = true;

        public float MaxHp { get { Recalc(); return _maxHp; } }
        public float MaxMp { get { Recalc(); return _maxMp; } }
        public float Attack { get { Recalc(); return _attack; } }
        public float Defense { get { Recalc(); return _defense; } }
        public float LifeSteal { get { Recalc(); return _lifeSteal; } }
        public float CritRate { get { Recalc(); return _critRate; } }
        public float CritDmg { get { Recalc(); return _critDmg; } }
        public float AttackSpeed { get { Recalc(); return _attackSpeed; } }
        public float MoveSpeed { get { Recalc(); return _moveSpeed; } }
        public float HpRegen { get { Recalc(); return _hpRegen; } }
        public float MpRegen { get { Recalc(); return _mpRegen; } }
        public float DamageBonus { get { Recalc(); return _damageBonus; } }

        private void Recalc()
        {
            if (!_dirty) return;
            _dirty = false;

            float sumHp = 0f;
            float sumMp = 0f;
            float sumAtk = 0f;
            float sumDef = 0f;
            float sumLifeSteal = 0f;
            float sumCritRate = 0f;
            float sumCritDmg = 0f;
            float sumAtkSpd = 0f;
            float sumMoveSpd = 0f;
            float sumHpRegen = 0f;
            float sumMpRegen = 0f;
            float sumDmgBonus = 0f;

            foreach (var m in _modifiers)
            {
                sumHp += m.hpAdd;
                sumMp += m.mpAdd;
                sumAtk += m.attackAdd;
                sumDef += m.defenseAdd;
                sumLifeSteal += m.lifeStealAdd;
                sumCritRate += m.critRateAdd;
                sumCritDmg += m.critDmgAdd;
                sumAtkSpd += m.attackSpeedAdd;
                sumMoveSpd += m.moveSpeedAdd;
                sumHpRegen += m.hpRegenAdd;
                sumMpRegen += m.mpRegenAdd;
                sumDmgBonus += m.damageBonusAdd;
            }

            _maxHp = baseHp + sumHp;
            _maxMp = baseMp + sumMp;
            _attack = baseAttack + sumAtk;
            _defense = baseDefense + sumDef;
            _lifeSteal = Mathf.Clamp01(baseLifeSteal + sumLifeSteal);
            _critRate = Mathf.Clamp01(baseCritRate + sumCritRate);
            _critDmg = baseCritDmg + sumCritDmg;
            _attackSpeed = Mathf.Max(0.1f, baseAttackSpeed * (1f + sumAtkSpd));
            _moveSpeed = Mathf.Max(0.1f, baseMoveSpeed * (1f + sumMoveSpd));
            _hpRegen = Mathf.Max(0f, baseHpRegen + sumHpRegen);
            _mpRegen = Mathf.Max(0f, baseMpRegen + sumMpRegen);
            _damageBonus = baseDamageBonus + sumDmgBonus;
        }
    }

    public static class CombatCalculator
    {
        public static void CalculateDamage(
            Stats attacker,
            float skillMultiplier,
            float targetDefense,
            Func<float> nextFloat,
            out float finalDamage,
            out bool isCrit,
            out float lifeStealHeal)
        {
            if (nextFloat == null) nextFloat = () => (float)new System.Random().NextDouble();

            float atk = Mathf.Max(0f, attacker.Attack);
            float baseDmg = atk * Mathf.Max(0f, skillMultiplier);
            float defFactor = Mathf.Max(0f, 0.1f + 270f / (targetDefense + 300f));
            float dmgBonus = Mathf.Max(0f, 1f + attacker.DamageBonus);
            float raw = baseDmg * defFactor * dmgBonus;

            isCrit = nextFloat() < attacker.CritRate;
            if (isCrit) raw *= (1f + attacker.CritDmg);

            finalDamage = Mathf.Max(0f, raw);
            lifeStealHeal = finalDamage * Mathf.Clamp01(attacker.LifeSteal);
        }

        public static void CalculateDamage(
            Stats attacker,
            float skillMultiplier,
            float targetDefense,
            System.Random rng,
            out float finalDamage,
            out bool isCrit,
            out float lifeStealHeal)
        {
            Func<float> next = rng != null ? () => (float)rng.NextDouble() : null;
            CalculateDamage(attacker, skillMultiplier, targetDefense, next, out finalDamage, out isCrit, out lifeStealHeal);
        }

        public static float CalculateDamageFromEnemy(float attackerAttack, float targetDefense)
        {
            float atk = Mathf.Max(0f, attackerAttack);
            float defFactor = Mathf.Max(0f, 0.1f + 270f / (targetDefense + 300f));
            return Mathf.Max(0f, atk * defFactor);
        }
    }
}
