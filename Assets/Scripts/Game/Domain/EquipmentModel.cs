using Game.Data;

namespace Game.Domain
{
    /// <summary>
    /// 装备槽：当前装备的武器，装备/卸下时对 Stats 挂/摘 StatModifier。
    /// </summary>
    public class EquipmentModel
    {
        private readonly Stats _stats;
        private WeaponInstance _equippedWeapon;
        private StatModifier   _currentModifier;

        public WeaponInstance EquippedWeapon => _equippedWeapon;

        public EquipmentModel(Stats stats)
        {
            _stats = stats;
        }

        /// <summary>装备武器；若已有装备则先卸下再装备新的</summary>
        public void Equip(WeaponInstance weapon)
        {
            Unequip();
            if (weapon == null) return;
            _equippedWeapon  = weapon;
            _currentModifier = weapon.ToModifier();
            _stats.AddModifier(_currentModifier);
        }

        /// <summary>卸下当前武器</summary>
        public void Unequip()
        {
            if (_currentModifier != null)
                _stats.RemoveModifier(_currentModifier);
            _currentModifier = null;
            _equippedWeapon  = null;
        }

        public void LoadFrom(WeaponInstance saved)
        {
            Unequip();
            if (saved != null) Equip(saved);
        }
    }
}
