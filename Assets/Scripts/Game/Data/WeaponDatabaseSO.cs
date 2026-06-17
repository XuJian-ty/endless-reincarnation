using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 单条武器配置（与 WeaponSO 字段一致，内联在 Database 中，无需单独 Sword/Gun.asset）。
    /// </summary>
    [Serializable]
    public class WeaponEntryData
    {
        [InspectorLabel("武器ID")] public string     weaponId     = "";
        [InspectorLabel("显示名")] public string     displayName  = "";
        [InspectorLabel("图标")] public Sprite     icon;
        [InspectorLabel("类型")] public WeaponType type         = WeaponType.MeleeSword;
        [InspectorLabel("普通")] public WeaponStatRanges common;
        [InspectorLabel("精良")] public WeaponStatRanges rare;
        [InspectorLabel("史诗")] public WeaponStatRanges epic;
        [InspectorLabel("传说")] public WeaponStatRanges legendary;

        public WeaponStatRanges GetRangesFor(WeaponRarity rarity)
        {
            switch (rarity)
            {
                case WeaponRarity.Rare:      return rare;
                case WeaponRarity.Epic:      return epic;
                case WeaponRarity.Legendary: return legendary;
                default:                     return common;
            }
        }
    }

    /// <summary>
    /// 所有武器类型合并在一个 SO 中，用列表按 WeaponType 区分；数据内联，可删除原 Sword/Gun.asset。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/武器库", fileName = "武器库")]
    public class WeaponDatabaseSO : ScriptableObject
    {
        [InspectorLabel("武器列表")]
        [Tooltip("每种武器类型一条（剑、枪等），数据内联，无需再拖 Sword/Gun.asset")]
        public List<WeaponEntryData> entries = new List<WeaponEntryData>();

        public WeaponEntryData GetEntry(WeaponType type)
        {
            if (entries == null) return null;
            foreach (var e in entries)
                if (e.type == type) return e;
            return null;
        }

        /// <summary>按 weaponId 查条目，背包/工具提示用（图标、显示名）</summary>
        public WeaponEntryData GetEntryByWeaponId(string weaponId)
        {
            if (entries == null || string.IsNullOrEmpty(weaponId)) return null;
            foreach (var e in entries)
                if (e.weaponId == weaponId) return e;
            return null;
        }

        /// <summary>按类型与品质 Roll 一条武器实例；无对应条目返回 null</summary>
        public WeaponInstance Roll(WeaponType type, WeaponRarity rarity, System.Random rng)
        {
            var e = GetEntry(type);
            if (e == null) return null;
            var r = e.GetRangesFor(rarity);
            return new WeaponInstance
            {
                weaponId          = e.weaponId,
                type              = e.type,
                rarity            = rarity,
                rolledHp          = r.hp.RandomValue(rng),
                rolledMp          = r.mp.RandomValue(rng),
                rolledAttack      = r.attack.RandomValue(rng),
                rolledDefense     = r.defense.RandomValue(rng),
                rolledHpRegen     = rarity >= WeaponRarity.Rare      ? r.hpRegen.RandomValue(rng)    : 0f,
                rolledMpRegen     = rarity >= WeaponRarity.Rare      ? r.mpRegen.RandomValue(rng)    : 0f,
                rolledCritRate    = rarity >= WeaponRarity.Epic      ? r.critRate.RandomValue(rng)   : 0f,
                rolledCritDmg     = rarity >= WeaponRarity.Epic      ? r.critDmg.RandomValue(rng)    : 0f,
                rolledAttackSpeed = rarity >= WeaponRarity.Legendary ? r.attackSpeed.RandomValue(rng) : 0f,
                rolledMoveSpeed   = rarity >= WeaponRarity.Legendary ? r.moveSpeed.RandomValue(rng)   : 0f,
            };
        }

        /// <summary>按类型与品质生成一条“所有属性取范围下限”的武器实例，用于新存档默认装备/道具。</summary>
        public WeaponInstance CreateMinimumRoll(WeaponType type, WeaponRarity rarity)
        {
            var e = GetEntry(type);
            if (e == null) return null;
            var r = e.GetRangesFor(rarity);
            return new WeaponInstance
            {
                weaponId          = e.weaponId,
                type              = e.type,
                rarity            = rarity,
                rolledHp          = r.hp.min,
                rolledMp          = r.mp.min,
                rolledAttack      = r.attack.min,
                rolledDefense     = r.defense.min,
                rolledHpRegen     = rarity >= WeaponRarity.Rare      ? r.hpRegen.min      : 0f,
                rolledMpRegen     = rarity >= WeaponRarity.Rare      ? r.mpRegen.min      : 0f,
                rolledCritRate    = rarity >= WeaponRarity.Epic      ? r.critRate.min     : 0f,
                rolledCritDmg     = rarity >= WeaponRarity.Epic      ? r.critDmg.min      : 0f,
                rolledAttackSpeed = rarity >= WeaponRarity.Legendary ? r.attackSpeed.min  : 0f,
                rolledMoveSpeed   = rarity >= WeaponRarity.Legendary ? r.moveSpeed.min    : 0f,
            };
        }

        /// <summary>在已有武器中随机选一个类型再 Roll（需求：剑/枪各半）</summary>
        public WeaponInstance RollRandomWeapon(WeaponRarity rarity, System.Random rng)
        {
            if (entries == null || entries.Count == 0) return null;
            var e = entries[rng.Next(entries.Count)];
            return Roll(e.type, rarity, rng);
        }
    }
}
