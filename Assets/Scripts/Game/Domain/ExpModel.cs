using System;
using UnityEngine;

namespace Game.Domain
{
    /// <summary>
    /// 经验与等级：1～10 级，升级所需经验可从玩家成长属性库配置，溢出经验保留到下一级。
    /// </summary>
    public class ExpModel
    {
        public const int MaxLevel = 10;

        private int _level = 1;
        private int _exp;
        private int _expToNext;
        private readonly Func<int, int> _getExpToNextForLevel;

        public int   Level      => _level;
        public int   Exp        => _exp;
        public int   ExpToNext  => _expToNext;

        /// <summary>升到下一级还需多少经验（用于 UI 进度条）</summary>
        public int   ExpNeedForNext => Mathf.Max(0, _expToNext - _exp);

        /// <summary>经验条进度 0～1</summary>
        public float ExpProgress    => _expToNext <= 0 ? 1f : Mathf.Clamp01((float)_exp / _expToNext);

        /// <summary>仅用公式：80+level*40，用于无配置时。</summary>
        public static int GetExpRequiredForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            return 80 + level * 40;
        }

        public ExpModel(int level = 1, int exp = 0) : this(level, exp, null) { }

        /// <param name="getExpToNextForLevel">按等级取「升到下一级所需经验」；为 null 时用公式。</param>
        public ExpModel(int level, int exp, Func<int, int> getExpToNextForLevel)
        {
            _getExpToNextForLevel = getExpToNextForLevel;
            _level = Mathf.Clamp(level, 1, MaxLevel);
            _exp   = Mathf.Max(0, exp);
            RecalcExpToNext();
        }

        private void RecalcExpToNext()
        {
            if (_level >= MaxLevel) { _expToNext = 0; _exp = 0; return; }
            _expToNext = _getExpToNextForLevel != null
                ? _getExpToNextForLevel(_level)
                : GetExpRequiredForLevel(_level);
        }

        /// <summary>增加经验；溢出保留并可能连续升级，返回实际升了几级</summary>
        public int AddExp(int amount)
        {
            if (amount <= 0 || _level >= MaxLevel) return 0;
            int gained = 0;
            _exp += amount;
            while (_level < MaxLevel && _exp >= _expToNext)
            {
                _exp -= _expToNext;
                _level++;
                gained++;
                RecalcExpToNext();
            }
            if (_level >= MaxLevel) _exp = 0;
            return gained;
        }

        public void Load(int level, int exp)
        {
            _level = Mathf.Clamp(level, 1, MaxLevel);
            _exp   = Mathf.Max(0, exp);
            RecalcExpToNext();
            if (_level >= MaxLevel) _exp = 0;
        }
    }
}
