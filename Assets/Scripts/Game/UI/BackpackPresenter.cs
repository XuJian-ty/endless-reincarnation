using System.Collections.Generic;
using Game.Domain;
using Game.Data;

namespace Game.UI
{
    /// <summary>
    /// 背包面板业务逻辑：排序与显示顺序。面板只负责视图绑定与用户输入转发。
    /// </summary>
    public class BackpackPresenter
    {
        public enum SortMode
        {
            AllQualityLowToHigh = 0,
            AllQualityHighToLow = 1,
            OthersFirstQualityLowToHigh = 2,
            OthersFirstQualityHighToLow = 3,
            WeaponsFirstQualityLowToHigh = 4,
            WeaponsFirstQualityHighToLow = 5,
        }

        private PlayerModel _player;
        private SortMode _sortMode = SortMode.AllQualityLowToHigh;
        private readonly int[] _displayOrder = new int[PlayerModel.SlotCount];
        private ItemDisplayDatabaseSO _itemDisplayDatabase;

        public void Init(PlayerModel player)
        {
            _player = player;
            for (int i = 0; i < PlayerModel.SlotCount; i++) _displayOrder[i] = i;
        }

        public void SetSortMode(SortMode mode) => _sortMode = mode;
        public void SetSortMode(int value) => _sortMode = (SortMode)System.Math.Clamp(value, 0, 5);
        /// <summary>当前排序模式对应的下拉索引 0～5，用于同步 Dropdown.value。</summary>
        public int GetSortModeIndex() => (int)_sortMode;

        /// <summary>显示顺序中 displayIndex 位置对应的背包格子在 PlayerModel 中的索引。</summary>
        public int GetModelIndexForDisplayIndex(int displayIndex)
        {
            if (displayIndex < 0 || displayIndex >= PlayerModel.SlotCount) return displayIndex;
            return _displayOrder[displayIndex];
        }

        public void RecomputeDisplayOrder()
        {
            if (_player == null)
            {
                for (int i = 0; i < PlayerModel.SlotCount; i++) _displayOrder[i] = i;
                return;
            }

            if (_itemDisplayDatabase == null)
                _itemDisplayDatabase = global::Game.ConfigManager.GetInstance()?.GetItemDisplayDatabase();

            var list = new List<int>();
            for (int i = 0; i < PlayerModel.SlotCount; i++) list.Add(i);

            int WeaponRarityOrder(WeaponRarity r) => (int)r;
            float GetSortKey(int slotIndex)
            {
                var slot = _player.GetSlot(slotIndex);
                if (slot == null || slot.IsEmpty) return 999f;
                if (slot.IsWeapon && slot.weapon != null) return WeaponRarityOrder(slot.weapon.rarity);
                if (slot.IsStack && !string.IsNullOrEmpty(slot.stackItemId))
                {
                    var entry = _itemDisplayDatabase?.GetEntry(slot.stackItemId);
                    if (entry != null) return WeaponRarityOrder(entry.rarity);
                }
                return 0f;
            }

            bool IsWeapon(int slotIndex)
            {
                var slot = _player.GetSlot(slotIndex);
                return slot != null && slot.IsWeapon;
            }

            list.Sort((a, b) =>
            {
                bool emptyA = _player.GetSlot(a) == null || _player.GetSlot(a).IsEmpty;
                bool emptyB = _player.GetSlot(b) == null || _player.GetSlot(b).IsEmpty;
                if (emptyA && emptyB) return a.CompareTo(b);
                if (emptyA) return 1;
                if (emptyB) return -1;

                bool weaponA = IsWeapon(a);
                bool weaponB = IsWeapon(b);
                float keyA = GetSortKey(a);
                float keyB = GetSortKey(b);

                switch (_sortMode)
                {
                    case SortMode.AllQualityLowToHigh:
                        return keyA.CompareTo(keyB) != 0 ? keyA.CompareTo(keyB) : a.CompareTo(b);
                    case SortMode.AllQualityHighToLow:
                        return keyB.CompareTo(keyA) != 0 ? keyB.CompareTo(keyA) : a.CompareTo(b);
                    case SortMode.OthersFirstQualityLowToHigh:
                        if (weaponA != weaponB) return weaponA.CompareTo(weaponB);
                        return keyA.CompareTo(keyB) != 0 ? keyA.CompareTo(keyB) : a.CompareTo(b);
                    case SortMode.OthersFirstQualityHighToLow:
                        if (weaponA != weaponB) return weaponA.CompareTo(weaponB);
                        return keyB.CompareTo(keyA) != 0 ? keyB.CompareTo(keyA) : a.CompareTo(b);
                    case SortMode.WeaponsFirstQualityLowToHigh:
                        if (weaponA != weaponB) return weaponB.CompareTo(weaponA);
                        return keyA.CompareTo(keyB) != 0 ? keyA.CompareTo(keyB) : a.CompareTo(b);
                    case SortMode.WeaponsFirstQualityHighToLow:
                        if (weaponA != weaponB) return weaponB.CompareTo(weaponA);
                        return keyB.CompareTo(keyA) != 0 ? keyB.CompareTo(keyA) : a.CompareTo(b);
                    default:
                        return a.CompareTo(b);
                }
            });

            for (int i = 0; i < PlayerModel.SlotCount; i++) _displayOrder[i] = list[i];
        }
    }
}
