using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Saving;

namespace Game.Domain
{
    /// <summary>
    /// 玩家数据模型：统一背包 50 格，每格可放一把武器或一类可堆叠物品；金币与天赋点单独存。
    /// 出售某格后后续格子前移；悬停显示信息、右键菜单（装备/出售）由 UI 根据格子内容调用本模型接口。
    /// </summary>
    public class PlayerModel
    {
        public const int SlotCount = 50;

        public static class ItemIds
        {
            public const string Gold        = "gold";
            public const string TalentPoint = "talent_point";
            public const string PotionHp    = "potion_hp";
            public const string PotionMp    = "potion_mp";
            public const string Nectar      = "nectar";
        }

        public const int MaxStackPerSlot = 999;

        public readonly Stats          Stats     = new Stats();
        public readonly ExpModel       Exp;
        public readonly EquipmentModel Equipment;

        private readonly InventorySlot[] _slots = new InventorySlot[SlotCount];
        /// <summary>仅存金币与天赋点；药剂、仙露等在 _slots 中</summary>
        private readonly Dictionary<string, int> _currency = new Dictionary<string, int>();

        public float CurrentHp { get; set; }
        public float CurrentMp { get; set; }

        public int Gold
        {
            get => GetCurrency(ItemIds.Gold);
            set => SetCurrency(ItemIds.Gold, Math.Max(0, value));
        }

        public int TalentPoints
        {
            get => GetCurrency(ItemIds.TalentPoint);
            set => SetCurrency(ItemIds.TalentPoint, Math.Max(0, value));
        }

        public HashSet<string> BuffIds         { get; private set; } = new HashSet<string>();
        public HashSet<string> UnlockedSkillIds { get; private set; } = new HashSet<string>();

        private readonly Dictionary<string, StatModifier> _buffModifiers = new Dictionary<string, StatModifier>();
        private readonly Dictionary<string, StatModifier> _passiveSkillModifiers = new Dictionary<string, StatModifier>();
        private LevelGrowthSO _levelGrowth;

        public PlayerModel(LevelGrowthSO levelGrowth)
        {
            _levelGrowth = levelGrowth;
            Exp = new ExpModel(1, 0, levelGrowth != null ? levelGrowth.GetExpToNextForLevel : null);
            Equipment = new EquipmentModel(Stats);
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = new InventorySlot();

            if (_levelGrowth != null)
                _levelGrowth.GetStatsForLevel(1, Stats);
            else
                ApplyDefaultLevel1Stats();

            CurrentHp = Stats.MaxHp;
            CurrentMp = Stats.MaxMp;
        }

        private void ApplyDefaultLevel1Stats()
        {
            Stats.baseHp          = 100f;
            Stats.baseMp          = 100f;
            Stats.baseAttack      = 20f;
            Stats.baseDefense     = 100f;
            Stats.baseHpRegen     = 1f;
            Stats.baseMpRegen     = 1f;
            Stats.baseLifeSteal   = 0f;
            Stats.baseCritRate    = 0.05f;
            Stats.baseCritDmg     = 1f;
            Stats.baseAttackSpeed = 1f;
            Stats.baseMoveSpeed   = 6f;
            Stats.baseDamageBonus = 0f;
            Stats.baseDamageReduce = 0f;
            Stats.InvalidateCache();
        }

        public bool HasBuff(string buffId) => !string.IsNullOrEmpty(buffId) && BuffIds.Contains(buffId);
        public bool HasUnlockedSkill(string skillId) => !string.IsNullOrEmpty(skillId) && UnlockedSkillIds.Contains(skillId);
        public bool IsSkillAvailable(SkillConfigEntry entry)
        {
            if (entry == null)
                return false;

            if (entry.IsBaseSkill)
                return true;

            if (entry.IsActiveSkill || entry.IsPassiveSkill)
                return HasUnlockedSkill(entry.skillId);

            return HasUnlockedSkill(entry.skillId);
        }

        // ── 统一背包 50 格 ───────────────────────────────────────────────────────────
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return null;
            return _slots[index];
        }

        public bool CanAddWeapon()
        {
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i].IsEmpty) return true;
            return false;
        }

        public bool AddWeapon(WeaponInstance weapon)
        {
            if (weapon == null || !CanAddWeapon()) return false;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].IsEmpty) continue;
                _slots[i].weapon     = weapon;
                _slots[i].stackItemId = null;
                _slots[i].stackCount  = 0;
                return true;
            }
            return false;
        }

        public bool CanAddStackable(string itemId, int count = 1)
        {
            if (count <= 0) return true;
            int remaining = count;
            for (int i = 0; i < SlotCount && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s.weapon != null) continue;
                if (string.IsNullOrEmpty(s.stackItemId)) return true;
                if (s.stackItemId == itemId) remaining -= (InventorySlot.MaxStack - s.stackCount);
                if (remaining <= 0) return true;
            }
            return remaining <= 0;
        }

        /// <summary>添加可堆叠物品到背包格（先填同种再占空格），返回是否成功</summary>
        public bool AddStackable(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return true;
            int remaining = count;
            for (int i = 0; i < SlotCount && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s.weapon != null) continue;
                if (string.IsNullOrEmpty(s.stackItemId))
                {
                    int add = Math.Min(remaining, InventorySlot.MaxStack);
                    s.stackItemId = itemId;
                    s.stackCount  = add;
                    remaining -= add;
                    continue;
                }
                if (s.stackItemId != itemId) continue;
                int room = InventorySlot.MaxStack - s.stackCount;
                int add2 = Math.Min(remaining, room);
                s.stackCount += add2;
                remaining -= add2;
            }
            return remaining <= 0;
        }

        /// <summary>移除第 index 格内容并让后续格子前移（出售/丢弃时调用）</summary>
        public void RemoveSlotAt(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            for (int i = index; i < SlotCount - 1; i++)
                _slots[i].CopyFrom(_slots[i + 1]);
            _slots[SlotCount - 1].Clear();
        }

        /// <summary>出售第 index 格：移除并前移，增加金币。由 UI 根据配置算好 goldEarned 传入。</summary>
        public void SellSlotAt(int index, int goldEarned)
        {
            if (index < 0 || index >= SlotCount) return;
            RemoveSlotAt(index);
            if (goldEarned > 0) Gold += goldEarned;
        }

        /// <summary>装备第 index 格的武器；若非武器格或空格则无效果</summary>
        public void EquipWeaponAt(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            var slot = _slots[index];
            if (slot?.weapon == null) return;
            Equipment.Equip(slot.weapon);
        }

        /// <summary>兼容：按格子取武器（若该格是武器格则返回实例）</summary>
        public WeaponInstance GetWeaponAt(int index)
        {
            var slot = GetSlot(index);
            return slot?.weapon;
        }

        // ── 物品数量：金币/天赋点从 _currency；其余从格子汇总 ─────────────────────────
        public int GetItemCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            if (itemId == ItemIds.Gold) return Gold;
            if (itemId == ItemIds.TalentPoint) return TalentPoints;
            int total = 0;
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i].stackItemId == itemId)
                    total += _slots[i].stackCount;
            return total;
        }

        public void AddItemCount(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return;
            if (itemId == ItemIds.Gold) { Gold += count; return; }
            if (itemId == ItemIds.TalentPoint) { TalentPoints += count; return; }
            AddStackable(itemId, count);
        }

        /// <summary>尝试消耗指定数量（先扣背包格中的该物品，不足再扣金币/天赋点）；成功返回 true</summary>
        public bool TryConsumeItem(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (itemId == ItemIds.Gold) { if (Gold < count) return false; Gold -= count; return true; }
            if (itemId == ItemIds.TalentPoint) { if (TalentPoints < count) return false; TalentPoints -= count; return true; }
            if (GetItemCount(itemId) < count) return false;
            int remaining = count;
            for (int i = 0; i < SlotCount && remaining > 0; i++)
            {
                if (_slots[i].stackItemId != itemId || _slots[i].stackCount <= 0) continue;
                int take = Math.Min(remaining, _slots[i].stackCount);
                _slots[i].stackCount -= take;
                if (_slots[i].stackCount <= 0) _slots[i].Clear();
                remaining -= take;
            }
            CompactSlots();
            return true;
        }

        /// <summary>去掉中间空位，让所有有内容的格子连续排在前面</summary>
        public void CompactSlots()
        {
            int write = 0;
            for (int read = 0; read < SlotCount; read++)
            {
                if (_slots[read].IsEmpty) continue;
                if (write != read) _slots[write].CopyFrom(_slots[read]);
                write++;
            }
            for (int i = write; i < SlotCount; i++)
                _slots[i].Clear();
        }

        private int GetCurrency(string key)
        {
            return _currency.TryGetValue(key, out int c) ? c : 0;
        }

        private void SetCurrency(string key, int value)
        {
            value = Math.Max(0, value);
            if (value == 0) _currency.Remove(key);
            else _currency[key] = value;
        }

        // ── Buff / 经验等 ───────────────────────────────────────────────────────────
        public int AddExp(int amount)
        {
            int levels = Exp.AddExp(amount);
            if (levels > 0 && _levelGrowth != null)
                _levelGrowth.GetStatsForLevel(Exp.Level, Stats);
            return levels;
        }

        public void SetLevelGrowth(LevelGrowthSO so) => _levelGrowth = so;

        public void AddBuff(string buffId, StatModifier modifier)
        {
            if (string.IsNullOrEmpty(buffId)) return;
            if (_buffModifiers.ContainsKey(buffId)) return;
            BuffIds.Add(buffId);
            if (modifier != null)
            {
                _buffModifiers[buffId] = modifier;
                Stats.AddModifier(modifier);
            }
        }

        public void RemoveBuff(string buffId)
        {
            if (string.IsNullOrEmpty(buffId)) return;
            BuffIds.Remove(buffId);
            if (_buffModifiers.TryGetValue(buffId, out var mod))
            {
                Stats.RemoveModifier(mod);
                _buffModifiers.Remove(buffId);
            }
        }

        public void ClearBuffModifiers()
        {
            foreach (var mod in _buffModifiers.Values)
                Stats.RemoveModifier(mod);
            _buffModifiers.Clear();
        }

        public void ReapplyBuffModifiers(Func<string, StatModifier> getModifier)
        {
            if (getModifier == null) return;
            ClearBuffModifiers();
            foreach (string id in BuffIds)
            {
                var mod = getModifier(id);
                if (mod != null)
                {
                    _buffModifiers[id] = mod;
                    Stats.AddModifier(mod);
                }
            }
        }

        public bool UnlockSkill(string skillId, SkillConfigDatabaseSO config)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return false;

            string normalizedSkillId = skillId.Trim();
            if (!UnlockedSkillIds.Add(normalizedSkillId))
                return false;

            ApplyPassiveSkillModifier(normalizedSkillId, config);
            return true;
        }

        public void RefreshUnlockedSkillEffects(SkillConfigDatabaseSO config)
        {
            ClearPassiveSkillModifiers();
            if (config?.entries == null || UnlockedSkillIds == null || UnlockedSkillIds.Count == 0)
                return;

            foreach (string skillId in UnlockedSkillIds)
                ApplyPassiveSkillModifier(skillId, config);
        }

        private void ApplyPassiveSkillModifier(string skillId, SkillConfigDatabaseSO config)
        {
            if (config == null || string.IsNullOrWhiteSpace(skillId))
                return;

            var entry = config.GetEntry(skillId);
            if (entry == null || !entry.IsPassiveSkill || entry.passiveStatModifier == null)
                return;

            if (_passiveSkillModifiers.ContainsKey(entry.skillId))
                return;

            var modifier = entry.passiveStatModifier.Clone();
            _passiveSkillModifiers[entry.skillId] = modifier;
            Stats.AddModifier(modifier);
        }

        private void ClearPassiveSkillModifiers()
        {
            foreach (var modifier in _passiveSkillModifiers.Values)
                Stats.RemoveModifier(modifier);
            _passiveSkillModifiers.Clear();
        }

        public void LoadFrom(RunData run)
        {
            if (run == null) return;
            for (int i = 0; i < SlotCount; i++) _slots[i].Clear();
            _currency.Clear();

            if (run.player != null)
            {
                Exp.Load(run.player.level, run.player.exp);
                CurrentHp = run.player.currentHp;
                CurrentMp = run.player.currentMp;
            }
            if (_levelGrowth != null)
                _levelGrowth.GetStatsForLevel(Exp.Level, Stats);

            bool loadedSlots = false;
            if (run.inventory?.slots != null && run.inventory.slots.Count >= SlotCount)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    var save = run.inventory.slots[i];
                    if (save?.weapon != null)
                    {
                        _slots[i].weapon = save.weapon;
                        _slots[i].stackItemId = null;
                        _slots[i].stackCount = 0;
                    }
                    else if (!string.IsNullOrEmpty(save?.itemId) && save.count > 0)
                    {
                        _slots[i].weapon = null;
                        _slots[i].stackItemId = save.itemId;
                        _slots[i].stackCount = Math.Min(save.count, InventorySlot.MaxStack);
                    }
                }
                loadedSlots = true;
            }

            if (!loadedSlots && run.inventory != null)
            {
                int idx = 0;
                if (run.inventory.weapons != null)
                    foreach (var w in run.inventory.weapons)
                    {
                        if (idx >= SlotCount) break;
                        _slots[idx].weapon = w;
                        idx++;
                    }
                if (run.inventory.stacks != null)
                    foreach (var s in run.inventory.stacks)
                    {
                        if (idx >= SlotCount) break;
                        if (string.IsNullOrEmpty(s.itemId) || s.count <= 0) continue;
                        _slots[idx].stackItemId = s.itemId;
                        _slots[idx].stackCount = Math.Min(s.count, InventorySlot.MaxStack);
                        idx++;
                    }
            }

            Equipment.LoadFrom(run.equippedWeapon);

            if (run.itemCounts != null)
            {
                Gold = run.itemCounts.gold;
                TalentPoints = run.itemCounts.talentPoints;
                if (!loadedSlots && run.itemCounts.items != null)
                    foreach (var s in run.itemCounts.items)
                        if (!string.IsNullOrEmpty(s.itemId) && s.count > 0
                            && s.itemId != ItemIds.Gold && s.itemId != ItemIds.TalentPoint)
                            AddStackable(s.itemId, s.count);
            }
            else
            {
                Gold = run.player?.gold ?? 0;
                TalentPoints = run.talentPoints;
            }

            BuffIds          = run.buffIds         != null ? new HashSet<string>(run.buffIds)         : new HashSet<string>();
            UnlockedSkillIds = run.unlockedSkillIds != null ? new HashSet<string>(run.unlockedSkillIds) : new HashSet<string>();
            ClearBuffModifiers();
            CurrentHp = Mathf.Clamp(CurrentHp, 0f, Stats.MaxHp);
            CurrentMp = Mathf.Clamp(CurrentMp, 0f, Stats.MaxMp);
        }

        public void SaveTo(RunData run)
        {
            if (run == null) return;
            if (run.player == null) run.player = new PlayerSaveData();
            run.player.level     = Exp.Level;
            run.player.exp       = Exp.Exp;
            run.player.currentHp = CurrentHp;
            run.player.currentMp = CurrentMp;
            run.player.gold      = Gold;
            run.talentPoints     = TalentPoints;

            if (run.inventory == null) run.inventory = new InventorySaveData();
            run.inventory.slots.Clear();
            for (int i = 0; i < SlotCount; i++)
            {
                var s = _slots[i];
                run.inventory.slots.Add(new InventorySlotSave
                {
                    weapon = s.weapon,
                    itemId = s.stackItemId,
                    count  = s.stackCount
                });
            }
            run.inventory.weapons?.Clear();
            run.inventory.stacks?.Clear();

            if (run.itemCounts == null) run.itemCounts = new ItemCountSaveData();
            run.itemCounts.gold = Gold;
            run.itemCounts.talentPoints = TalentPoints;
            run.itemCounts.items?.Clear();
            if (run.itemCounts.items == null) run.itemCounts.items = new List<ItemStackSave>();

            run.equippedWeapon   = Equipment.EquippedWeapon;
            run.buffIds          = new List<string>(BuffIds);
            run.unlockedSkillIds = new List<string>(UnlockedSkillIds);
        }

        public void TakeDamage(float damage) => CurrentHp = Mathf.Max(0f, CurrentHp - damage);
        public void Heal(float amount) => CurrentHp = Mathf.Min(Stats.MaxHp, CurrentHp + amount);
        public void SpendMp(float amount) => CurrentMp = Mathf.Max(0f, CurrentMp - amount);
        public bool CanSpendMp(float amount) => CurrentMp >= amount;
    }
}
