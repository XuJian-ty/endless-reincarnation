using System;
using Game.Data;

namespace Game.Domain
{
    /// <summary>
    /// 统一背包一格：空、一把武器、或一类可堆叠物品（itemId + 数量，单格最多 999）。
    /// 每格只能是一种内容，武器与堆叠物互斥。
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public const int MaxStack = 999;

        public WeaponInstance weapon;
        public string stackItemId;
        public int stackCount;

        public bool IsEmpty => weapon == null && (string.IsNullOrEmpty(stackItemId) || stackCount <= 0);
        public bool IsWeapon => weapon != null;
        public bool IsStack => !string.IsNullOrEmpty(stackItemId) && stackCount > 0;

        public void Clear()
        {
            weapon = null;
            stackItemId = null;
            stackCount = 0;
        }

        public void CopyFrom(InventorySlot other)
        {
            if (other == null) { Clear(); return; }
            weapon = other.weapon;
            stackItemId = other.stackItemId;
            stackCount = other.stackCount;
        }
    }
}
