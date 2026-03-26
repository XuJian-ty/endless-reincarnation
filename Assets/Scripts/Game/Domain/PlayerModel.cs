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
        public const int SkillSlotCount = 10;

        private sealed class AppliedBuffModifier
        {
            public string buffId;
            public StatModifier modifier;
        }

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
        public event Action LoadoutChanged;

        private readonly InventorySlot[] _slots = new InventorySlot[SlotCount];
        private readonly string[] _equippedSkillActionIds = new string[SkillSlotCount];
        private readonly Dictionary<string, string> _selectedSkillVariantIds = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>仅存金币与天赋点；药剂、仙露等在 _slots 中</summary>
        private readonly Dictionary<string, int> _currency = new Dictionary<string, int>();
        private PlayerAttackMode _currentAttackMode = PlayerAttackMode.Melee;

        public float CurrentHp { get; set; }
        public float CurrentMp { get; set; }
        public PlayerAttackMode CurrentAttackMode => _currentAttackMode;

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

        public List<string> BuffIds             { get; private set; } = new List<string>();
        public HashSet<string> UnlockedActionIds { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

        private readonly List<AppliedBuffModifier> _buffModifiers = new List<AppliedBuffModifier>();
        private readonly Dictionary<string, StatModifier> _passiveSkillModifiers = new Dictionary<string, StatModifier>();
        private LevelGrowthSO _levelGrowth;

        public PlayerModel(LevelGrowthSO levelGrowth)
        {
            _levelGrowth = levelGrowth;
            Exp = new ExpModel(1, 0, levelGrowth != null ? levelGrowth.GetExpToNextForLevel : null);
            Equipment = new EquipmentModel(Stats);
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = new InventorySlot();
            ApplyDefaultSkillSlotActions();

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
        public int GetBuffStackCount(string buffId)
        {
            if (string.IsNullOrEmpty(buffId) || BuffIds == null || BuffIds.Count == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < BuffIds.Count; i++)
            {
                if (BuffIds[i] == buffId)
                    count++;
            }

            return count;
        }
        public bool HasUnlockedAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            string normalizedActionId = actionId.Trim();
            if (UnlockedActionIds.Contains(normalizedActionId))
                return true;

            normalizedActionId = ResolveUnlockedActionId(normalizedActionId);
            return !string.IsNullOrEmpty(normalizedActionId) && UnlockedActionIds.Contains(normalizedActionId);
        }

        public bool HasUnlockedEntry(SkillConfigEntry entry)
        {
            return entry != null && HasUnlockedAction(entry.GetResolvedActionId());
        }

        public bool IsSkillAvailable(SkillConfigEntry entry)
        {
            if (entry == null)
                return false;

            if (entry.IsBaseSkill)
                return entry.SupportsAttackMode(_currentAttackMode);

            if (entry.IsActiveSkill || entry.IsPassiveSkill)
                return HasUnlockedEntry(entry)
                       && (!entry.IsActiveSkill || entry.SupportsAttackMode(_currentAttackMode));

            return HasUnlockedEntry(entry);
        }

        public WeaponInstance GetEquippedWeapon(PlayerAttackMode attackMode)
        {
            return Equipment != null ? Equipment.GetEquippedWeapon(attackMode) : null;
        }

        public bool SetAttackMode(PlayerAttackMode attackMode)
        {
            if (_currentAttackMode == attackMode)
                return false;

            float previousMaxHp = Mathf.Max(0f, Stats.MaxHp);
            float previousMaxMp = Mathf.Max(0f, Stats.MaxMp);
            _currentAttackMode = attackMode;
            Equipment?.SetActiveAttackMode(attackMode);
            ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
            NotifyLoadoutChanged();
            return true;
        }

        public bool ToggleAttackMode()
        {
            return SetAttackMode(PlayerAttackModeUtility.GetOpposite(_currentAttackMode));
        }

        public bool CanEquipWeaponInCurrentAttackMode(WeaponInstance weapon)
        {
            if (weapon == null)
                return false;

            return PlayerAttackModeUtility.GetAttackModeForWeaponType(weapon.type) == _currentAttackMode;
        }

        public string ResolveCurrentFormActionId(PlayerFormActionSlot actionSlot, SkillConfigDatabaseSO config, string fallbackActionId = null)
        {
            return config != null
                ? config.ResolveFormActionId(_currentAttackMode, actionSlot, fallbackActionId)
                : (string.IsNullOrWhiteSpace(fallbackActionId) ? actionSlot.ToString() : fallbackActionId.Trim());
        }

        public string GetEquippedSkillActionId(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SkillSlotCount)
                return string.Empty;

            return _equippedSkillActionIds[slotIndex] ?? string.Empty;
        }

        public int FindEquippedSkillSlotIndex(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return -1;

            string normalizedActionId = actionId.Trim();
            for (int i = 0; i < _equippedSkillActionIds.Length; i++)
            {
                if (string.Equals(_equippedSkillActionIds[i], normalizedActionId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public SkillConfigEntry GetEquippedActiveSkillEntry(int slotIndex, SkillConfigDatabaseSO config)
        {
            if (config == null)
                return null;

            string actionId = GetEquippedSkillActionId(slotIndex);
            if (string.IsNullOrWhiteSpace(actionId))
                return null;

            return config.GetActiveSkillEntryByActionId(actionId);
        }

        public string ResolveSkillEffectId(SkillConfigEntry entry)
        {
            if (entry == null)
                return string.Empty;

            string defaultSkillId = entry.GetResolvedSkillId();
            string actionId = entry.GetResolvedActionId();
            if (string.IsNullOrWhiteSpace(actionId))
                return defaultSkillId;

            if (_selectedSkillVariantIds.TryGetValue(actionId, out string selectedSkillId)
                && entry.ContainsSkillVariant(selectedSkillId))
            {
                return selectedSkillId.Trim();
            }

            return defaultSkillId;
        }

        public SharedSkillDefinition ResolveSkillEffectDefinition(SkillConfigEntry entry)
        {
            if (entry == null || entry.IsPassiveSkill)
                return null;

            string resolvedSkillId = ResolveSkillEffectId(entry);
            SharedSkillDefinition definition = entry.ResolveSkillEffectDefinition(resolvedSkillId);
            return definition ?? entry.ResolveSkillEffectDefinition();
        }

        public PassiveSkillEffectDefinition ResolvePassiveSkillEffectDefinition(SkillConfigEntry entry)
        {
            if (entry == null || !entry.IsPassiveSkill)
                return null;

            string resolvedSkillId = ResolveSkillEffectId(entry);
            PassiveSkillEffectDefinition definition = entry.ResolvePassiveSkillEffectDefinition(resolvedSkillId);
            return definition ?? entry.ResolvePassiveSkillEffectDefinition();
        }

        public StatModifier ResolvePassiveStatModifier(SkillConfigEntry entry)
        {
            if (entry == null)
                return null;

            PassiveSkillEffectDefinition definition = ResolvePassiveSkillEffectDefinition(entry);
            if (definition?.statModifier != null)
            {
                if (HasAnyStatModifierValue(definition.statModifier) || !HasAnyStatModifierValue(entry.passiveStatModifier))
                    return definition.statModifier;
            }

            return entry.passiveStatModifier;
        }

        public string ResolveSkillEffectDescription(SkillConfigEntry entry)
        {
            if (entry == null)
                return string.Empty;

            if (entry.IsPassiveSkill)
            {
                PassiveSkillEffectDefinition passiveDefinition = ResolvePassiveSkillEffectDefinition(entry);
                return !string.IsNullOrWhiteSpace(passiveDefinition?.effectDescription)
                    ? passiveDefinition.effectDescription.Trim()
                    : string.Empty;
            }

            SharedSkillDefinition definition = ResolveSkillEffectDefinition(entry);
            return !string.IsNullOrWhiteSpace(definition?.effectDescription)
                ? definition.effectDescription.Trim()
                : string.Empty;
        }

        public bool TrySetSelectedSkillVariant(string actionId, string skillId, SkillConfigDatabaseSO config)
        {
            SkillConfigDatabaseSO resolvedConfig = config ?? ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            string normalizedActionId = ResolveUnlockedActionId(actionId, resolvedConfig);
            string normalizedSkillId = NormalizeId(skillId);
            if (string.IsNullOrWhiteSpace(normalizedActionId) || string.IsNullOrWhiteSpace(normalizedSkillId) || resolvedConfig == null)
                return false;

            SkillConfigEntry entry = resolvedConfig.GetEntryByActionId(normalizedActionId);
            if (entry == null || !entry.ContainsSkillVariant(normalizedSkillId))
                return false;

            string defaultSkillId = entry.GetResolvedSkillId();
            bool changed;
            if (string.Equals(defaultSkillId, normalizedSkillId, StringComparison.Ordinal))
            {
                changed = _selectedSkillVariantIds.Remove(normalizedActionId);
            }
            else if (_selectedSkillVariantIds.TryGetValue(normalizedActionId, out string existingSkillId)
                     && string.Equals(existingSkillId, normalizedSkillId, StringComparison.Ordinal))
            {
                changed = false;
            }
            else
            {
                _selectedSkillVariantIds[normalizedActionId] = normalizedSkillId;
                changed = true;
            }

            if (!changed)
                return false;

            if (entry.IsPassiveSkill && HasUnlockedEntry(entry))
                RefreshUnlockedSkillEffects(resolvedConfig);

            NotifyLoadoutChanged();
            return true;
        }

        public bool AssignSkillActionToSlot(int slotIndex, string actionId, SkillConfigDatabaseSO config)
        {
            if (slotIndex < 0 || slotIndex >= SkillSlotCount)
                return false;

            if (string.IsNullOrWhiteSpace(actionId))
                return ClearSkillSlot(slotIndex);

            string normalizedActionId = actionId.Trim();
            SkillConfigEntry entry = config != null ? config.GetActiveSkillEntryByActionId(normalizedActionId) : null;
            if (entry == null || !HasUnlockedEntry(entry))
                return false;

            int previousSlotIndex = FindEquippedSkillSlotIndex(normalizedActionId);
            if (previousSlotIndex >= 0 && previousSlotIndex != slotIndex)
                _equippedSkillActionIds[previousSlotIndex] = string.Empty;

            _equippedSkillActionIds[slotIndex] = normalizedActionId;
            return true;
        }

        public bool ClearSkillSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SkillSlotCount)
                return false;

            bool hadValue = !string.IsNullOrWhiteSpace(_equippedSkillActionIds[slotIndex]);
            _equippedSkillActionIds[slotIndex] = string.Empty;
            return hadValue;
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
        public bool EquipWeaponAt(int index)
        {
            return EquipWeaponAt(index, out _);
        }

        public bool EquipWeaponAt(int index, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (index < 0 || index >= SlotCount) return false;
            var slot = _slots[index];
            if (slot?.weapon == null) return false;

            WeaponInstance selectedWeapon = slot.weapon;
            if (!CanEquipWeaponInCurrentAttackMode(selectedWeapon))
            {
                errorMessage = "请切换到对应形态下装备该武器";
                return false;
            }

            WeaponInstance previouslyEquipped = Equipment.GetEquippedWeapon(_currentAttackMode);

            if (ReferenceEquals(selectedWeapon, previouslyEquipped))
            {
                RemoveSlotAt(index);
                NotifyLoadoutChanged();
                return true;
            }

            float previousMaxHp = Mathf.Max(0f, Stats.MaxHp);
            float previousMaxMp = Mathf.Max(0f, Stats.MaxMp);
            Equipment.Equip(_currentAttackMode, selectedWeapon);
            ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);

            if (previouslyEquipped != null)
            {
                slot.weapon = previouslyEquipped;
                slot.stackItemId = null;
                slot.stackCount = 0;
            }
            else
            {
                RemoveSlotAt(index);
            }

            NotifyLoadoutChanged();
            return true;
        }

        /// <summary>卸下当前武器并放回背包；若背包已满则失败</summary>
        public bool UnequipWeapon()
        {
            WeaponInstance equippedWeapon = Equipment.GetEquippedWeapon(_currentAttackMode);
            if (equippedWeapon == null)
                return false;

            if (!CanAddWeapon())
                return false;

            float previousMaxHp = Mathf.Max(0f, Stats.MaxHp);
            float previousMaxMp = Mathf.Max(0f, Stats.MaxMp);
            WeaponInstance removedWeapon = Equipment.Unequip(_currentAttackMode);
            ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
            bool added = AddWeapon(removedWeapon);
            if (added)
                NotifyLoadoutChanged();
            return added;
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
            {
                float previousMaxHp = Mathf.Max(0f, Stats.MaxHp);
                float previousMaxMp = Mathf.Max(0f, Stats.MaxMp);
                _levelGrowth.GetStatsForLevel(Exp.Level, Stats);
                ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
            }
            return levels;
        }

        public void SetLevelGrowth(LevelGrowthSO so) => _levelGrowth = so;

        public void AddBuff(string buffId, StatModifier modifier)
        {
            if (string.IsNullOrEmpty(buffId)) return;
            BuffIds.Add(buffId);
            if (modifier != null)
            {
                var clonedModifier = modifier.Clone();
                _buffModifiers.Add(new AppliedBuffModifier
                {
                    buffId = buffId,
                    modifier = clonedModifier
                });
                AddStatModifierAndSyncVitals(clonedModifier);
            }
        }

        public void RemoveBuff(string buffId)
        {
            if (string.IsNullOrEmpty(buffId)) return;
            BuffIds.Remove(buffId);
            for (int i = 0; i < _buffModifiers.Count; i++)
            {
                AppliedBuffModifier entry = _buffModifiers[i];
                if (entry == null || entry.buffId != buffId)
                    continue;

                RemoveStatModifierAndSyncVitals(entry.modifier);
                _buffModifiers.RemoveAt(i);
                break;
            }
        }

        public void ClearBuffModifiers()
        {
            ClearBuffModifiers(true);
        }

        private void ClearBuffModifiers(bool syncVitals)
        {
            float previousMaxHp = syncVitals ? Mathf.Max(0f, Stats.MaxHp) : 0f;
            float previousMaxMp = syncVitals ? Mathf.Max(0f, Stats.MaxMp) : 0f;
            for (int i = 0; i < _buffModifiers.Count; i++)
            {
                AppliedBuffModifier entry = _buffModifiers[i];
                if (entry?.modifier != null)
                    Stats.RemoveModifier(entry.modifier);
            }
            _buffModifiers.Clear();
            if (syncVitals)
                ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
        }

        public void ReapplyBuffModifiers(Func<string, StatModifier> getModifier)
        {
            if (getModifier == null) return;
            float previousMaxHp = Mathf.Max(0f, Stats.MaxHp);
            float previousMaxMp = Mathf.Max(0f, Stats.MaxMp);
            ClearBuffModifiers(false);
            foreach (string id in BuffIds)
            {
                var mod = getModifier(id);
                if (mod != null)
                {
                    var clonedModifier = mod.Clone();
                    _buffModifiers.Add(new AppliedBuffModifier
                    {
                        buffId = id,
                        modifier = clonedModifier
                    });
                    Stats.AddModifier(clonedModifier);
                }
            }
            ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
        }

        public bool UnlockAction(string actionId, SkillConfigDatabaseSO config)
        {
            string normalizedActionId = ResolveUnlockedActionId(actionId, config);
            if (string.IsNullOrWhiteSpace(normalizedActionId))
                return false;

            if (!UnlockedActionIds.Add(normalizedActionId))
                return false;

            ApplyPassiveSkillModifier(normalizedActionId, config);
            return true;
        }

        public Stats BuildCloneSharedStatsSnapshot(
            SkillConfigDatabaseSO config,
            Func<string, StatModifier> getModifierForBuff,
            PlayerAttackMode attackMode = PlayerAttackMode.Melee)
        {
            Stats snapshot = new Stats();
            if (_levelGrowth != null)
                _levelGrowth.GetStatsForLevel(Exp.Level, snapshot);
            else
                ApplyDefaultLevel1Stats(snapshot);

            WeaponInstance equippedWeapon = GetEquippedWeapon(attackMode);
            if (equippedWeapon != null)
                snapshot.AddModifier(equippedWeapon.ToModifier());

            if (config != null && UnlockedActionIds != null)
            {
                foreach (string actionId in UnlockedActionIds)
                {
                    if (string.IsNullOrWhiteSpace(actionId))
                        continue;

                    SkillConfigEntry entry = config.GetEntryByActionId(actionId);
                    if (entry == null || !entry.IsPassiveSkill)
                        continue;

                    StatModifier passiveModifier = ResolvePassiveStatModifier(entry);
                    if (passiveModifier != null)
                        snapshot.AddModifier(passiveModifier.Clone());
                }
            }

            if (getModifierForBuff != null && BuffIds != null)
            {
                for (int i = 0; i < BuffIds.Count; i++)
                {
                    string buffId = BuffIds[i];
                    if (string.IsNullOrWhiteSpace(buffId)
                        || string.Equals(buffId, Game.Data.BuffIds.SummonClone, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    StatModifier modifier = getModifierForBuff(buffId);
                    if (modifier != null)
                        snapshot.AddModifier(modifier.Clone());
                }
            }

            return snapshot;
        }

        public void RefreshUnlockedSkillEffects(SkillConfigDatabaseSO config)
        {
            float previousMaxHp = Mathf.Max(0f, Stats.MaxHp);
            float previousMaxMp = Mathf.Max(0f, Stats.MaxMp);
            ClearPassiveSkillModifiers(false);
            if (config?.entries == null || UnlockedActionIds == null || UnlockedActionIds.Count == 0)
            {
                ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
                return;
            }

            foreach (string actionId in UnlockedActionIds)
                ApplyPassiveSkillModifier(actionId, config, false);

            ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
        }

        private void ApplyPassiveSkillModifier(string actionId, SkillConfigDatabaseSO config)
        {
            ApplyPassiveSkillModifier(actionId, config, true);
        }

        private void ApplyPassiveSkillModifier(string actionId, SkillConfigDatabaseSO config, bool syncVitals)
        {
            string normalizedActionId = ResolveUnlockedActionId(actionId, config);
            if (config == null || string.IsNullOrWhiteSpace(normalizedActionId))
                return;

            var entry = config.GetEntryByActionId(normalizedActionId);
            if (entry == null || !entry.IsPassiveSkill)
                return;

            if (_passiveSkillModifiers.ContainsKey(normalizedActionId))
                return;

            StatModifier sourceModifier = ResolvePassiveStatModifier(entry);
            if (sourceModifier == null)
                return;

            var modifier = sourceModifier.Clone();
            _passiveSkillModifiers[normalizedActionId] = modifier;
            if (syncVitals)
                AddStatModifierAndSyncVitals(modifier);
            else
                Stats.AddModifier(modifier);
        }

        private void ClearPassiveSkillModifiers()
        {
            ClearPassiveSkillModifiers(true);
        }

        private void ClearPassiveSkillModifiers(bool syncVitals)
        {
            float previousMaxHp = syncVitals ? Mathf.Max(0f, Stats.MaxHp) : 0f;
            float previousMaxMp = syncVitals ? Mathf.Max(0f, Stats.MaxMp) : 0f;
            foreach (var modifier in _passiveSkillModifiers.Values)
                Stats.RemoveModifier(modifier);
            _passiveSkillModifiers.Clear();
            if (syncVitals)
                ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
        }

        public void LoadFrom(RunData run)
        {
            if (run == null) return;
            for (int i = 0; i < SlotCount; i++) _slots[i].Clear();
            for (int i = 0; i < SkillSlotCount; i++) _equippedSkillActionIds[i] = string.Empty;
            _selectedSkillVariantIds.Clear();
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

            WeaponInstance meleeWeapon = run.meleeEquippedWeapon;
            WeaponInstance rangedWeapon = run.rangedEquippedWeapon;
            if (meleeWeapon == null && rangedWeapon == null && run.equippedWeapon != null)
            {
                PlayerAttackMode legacyAttackMode = PlayerAttackModeUtility.GetAttackModeForWeaponType(run.equippedWeapon.type);
                if (legacyAttackMode == PlayerAttackMode.Ranged)
                    rangedWeapon = run.equippedWeapon;
                else
                    meleeWeapon = run.equippedWeapon;
            }

            _currentAttackMode = Enum.IsDefined(typeof(PlayerAttackMode), run.currentAttackMode)
                ? (PlayerAttackMode)run.currentAttackMode
                : PlayerAttackMode.Melee;
            Equipment.LoadFrom(_currentAttackMode, meleeWeapon, rangedWeapon);

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

            BuffIds = run.buffIds != null ? new List<string>(run.buffIds) : new List<string>();
            UnlockedActionIds = new HashSet<string>(StringComparer.Ordinal);
            if (run.unlockedSkillIds != null)
            {
                for (int i = 0; i < run.unlockedSkillIds.Count; i++)
                {
                    string normalizedActionId = ResolveUnlockedActionId(run.unlockedSkillIds[i]);
                    if (!string.IsNullOrWhiteSpace(normalizedActionId))
                        UnlockedActionIds.Add(normalizedActionId);
                }
            }
            LoadSelectedSkillVariants(run);
            LoadEquippedSkillActions(run);
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

            if (run.itemCounts == null) run.itemCounts = new ItemCountSaveData();
            run.itemCounts.gold = Gold;
            run.itemCounts.talentPoints = TalentPoints;
            run.itemCounts.items?.Clear();
            if (run.itemCounts.items == null) run.itemCounts.items = new List<ItemStackSave>();

            run.equippedWeapon   = Equipment.EquippedWeapon;
            run.meleeEquippedWeapon = Equipment.GetEquippedWeapon(PlayerAttackMode.Melee);
            run.rangedEquippedWeapon = Equipment.GetEquippedWeapon(PlayerAttackMode.Ranged);
            run.currentAttackMode = (int)_currentAttackMode;
            run.buffIds          = new List<string>(BuffIds);
            run.unlockedSkillIds = new List<string>(UnlockedActionIds);
            run.equippedSkillActionIds = new List<string>(SkillSlotCount);
            for (int i = 0; i < SkillSlotCount; i++)
                run.equippedSkillActionIds.Add(GetEquippedSkillActionId(i));
            run.selectedSkillMutations = new List<SkillMutationSelectionSave>();
            if (_selectedSkillVariantIds.Count > 0)
            {
                List<string> actionIds = new List<string>(_selectedSkillVariantIds.Keys);
                actionIds.Sort(StringComparer.Ordinal);
                for (int i = 0; i < actionIds.Count; i++)
                {
                    string actionId = actionIds[i];
                    if (!_selectedSkillVariantIds.TryGetValue(actionId, out string selectedSkillId)
                        || string.IsNullOrWhiteSpace(actionId)
                        || string.IsNullOrWhiteSpace(selectedSkillId))
                    {
                        continue;
                    }

                    run.selectedSkillMutations.Add(new SkillMutationSelectionSave
                    {
                        actionId = actionId,
                        skillId = selectedSkillId
                    });
                }
            }
        }

        public void TakeDamage(float damage) => CurrentHp = Mathf.Max(0f, CurrentHp - damage);
        public void Heal(float amount) => CurrentHp = Mathf.Min(Stats.MaxHp, CurrentHp + amount);
        public void SpendMp(float amount) => CurrentMp = Mathf.Max(0f, CurrentMp - amount);
        public bool CanSpendMp(float amount) => CurrentMp >= amount;

        public void AddStatModifierAndSyncVitals(StatModifier modifier)
        {
            if (modifier == null)
                return;

            float previousMaxHp = Mathf.Max(0f, Stats.MaxHp);
            float previousMaxMp = Mathf.Max(0f, Stats.MaxMp);
            Stats.AddModifier(modifier);
            ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
        }

        public void RemoveStatModifierAndSyncVitals(StatModifier modifier)
        {
            if (modifier == null)
                return;

            float previousMaxHp = Mathf.Max(0f, Stats.MaxHp);
            float previousMaxMp = Mathf.Max(0f, Stats.MaxMp);
            Stats.RemoveModifier(modifier);
            ApplyVitalCapsAfterStatChange(previousMaxHp, previousMaxMp);
        }

        private static void ApplyDefaultLevel1Stats(Stats stats)
        {
            if (stats == null)
                return;

            stats.baseHp = 100f;
            stats.baseMp = 100f;
            stats.baseAttack = 20f;
            stats.baseDefense = 100f;
            stats.baseHpRegen = 1f;
            stats.baseMpRegen = 1f;
            stats.baseLifeSteal = 0f;
            stats.baseCritRate = 0.05f;
            stats.baseCritDmg = 1f;
            stats.baseAttackSpeed = 1f;
            stats.baseMoveSpeed = 6f;
            stats.baseDamageBonus = 0f;
            stats.baseDamageReduce = 0f;
            stats.InvalidateCache();
        }

        private void ApplyDefaultSkillSlotActions()
        {
            for (int i = 0; i < SkillSlotCount; i++)
                _equippedSkillActionIds[i] = string.Empty;
        }

        private void LoadEquippedSkillActions(RunData run)
        {
            if (run?.equippedSkillActionIds == null || run.equippedSkillActionIds.Count == 0)
            {
                ApplyDefaultSkillSlotActions();
                return;
            }

            for (int i = 0; i < SkillSlotCount; i++)
            {
                if (i < run.equippedSkillActionIds.Count && !string.IsNullOrWhiteSpace(run.equippedSkillActionIds[i]))
                    _equippedSkillActionIds[i] = run.equippedSkillActionIds[i].Trim();
                else
                    _equippedSkillActionIds[i] = string.Empty;
            }
        }

        private void LoadSelectedSkillVariants(RunData run)
        {
            _selectedSkillVariantIds.Clear();
            if (run?.selectedSkillMutations == null || run.selectedSkillMutations.Count == 0)
                return;

            SkillConfigDatabaseSO config = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            for (int i = 0; i < run.selectedSkillMutations.Count; i++)
            {
                SkillMutationSelectionSave selection = run.selectedSkillMutations[i];
                string actionId = ResolveUnlockedActionId(selection?.actionId, config);
                string selectedSkillId = NormalizeId(selection?.skillId);
                if (string.IsNullOrWhiteSpace(actionId) || string.IsNullOrWhiteSpace(selectedSkillId))
                    continue;

                if (config == null)
                {
                    _selectedSkillVariantIds[actionId] = selectedSkillId;
                    continue;
                }

                SkillConfigEntry entry = config.GetEntryByActionId(actionId);
                if (entry == null || !entry.ContainsSkillVariant(selectedSkillId))
                    continue;

                if (string.Equals(entry.GetResolvedSkillId(), selectedSkillId, StringComparison.Ordinal))
                    continue;

                _selectedSkillVariantIds[actionId] = selectedSkillId;
            }
        }

        private void NotifyLoadoutChanged()
        {
            LoadoutChanged?.Invoke();
        }

        private static bool HasAnyStatModifierValue(StatModifier modifier)
        {
            if (modifier == null)
                return false;

            if (!Mathf.Approximately(modifier.hpAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.mpAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.attackAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.defenseAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.lifeStealAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.critRateAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.critDmgAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.attackSpeedAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.moveSpeedAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.hpRegenAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.mpRegenAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.damageBonusAdd, 0f)) return true;
            if (!Mathf.Approximately(modifier.damageReduceAdd, 0f)) return true;

            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private string ResolveUnlockedActionId(string entryId, SkillConfigDatabaseSO config = null)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return string.Empty;

            string normalizedEntryId = entryId.Trim();
            SkillConfigDatabaseSO resolvedConfig = config ?? ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            SkillConfigEntry entry = resolvedConfig != null ? resolvedConfig.GetEntryByActionId(normalizedEntryId) : null;
            entry ??= resolvedConfig != null ? resolvedConfig.GetEntry(normalizedEntryId) : null;
            if (entry != null)
            {
                string resolvedActionId = entry.GetResolvedActionId();
                if (!string.IsNullOrWhiteSpace(resolvedActionId))
                    return resolvedActionId;
            }

            return normalizedEntryId;
        }

        private void ApplyVitalCapsAfterStatChange(float previousMaxHp, float previousMaxMp)
        {
            float currentMaxHp = Mathf.Max(0f, Stats.MaxHp);
            if (currentMaxHp > previousMaxHp)
                CurrentHp += currentMaxHp - previousMaxHp;

            float currentMaxMp = Mathf.Max(0f, Stats.MaxMp);
            if (currentMaxMp > previousMaxMp)
                CurrentMp += currentMaxMp - previousMaxMp;

            CurrentHp = Mathf.Clamp(CurrentHp, 0f, currentMaxHp);
            CurrentMp = Mathf.Clamp(CurrentMp, 0f, currentMaxMp);
        }
    }
}
