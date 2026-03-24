using Game.Data;

namespace Game.Domain
{
    /// <summary>
    /// 装备槽：按形态分别保存武器，且仅当前形态的武器会对 Stats 挂/摘 StatModifier。
    /// </summary>
    public class EquipmentModel
    {
        private readonly Stats _stats;
        private WeaponInstance _meleeWeapon;
        private WeaponInstance _rangedWeapon;
        private PlayerAttackMode _activeAttackMode = PlayerAttackMode.Melee;
        private StatModifier   _currentModifier;

        public WeaponInstance EquippedWeapon => GetEquippedWeapon(_activeAttackMode);

        public EquipmentModel(Stats stats)
        {
            _stats = stats;
        }

        public WeaponInstance GetEquippedWeapon(PlayerAttackMode attackMode)
        {
            return attackMode == PlayerAttackMode.Ranged ? _rangedWeapon : _meleeWeapon;
        }

        public void SetActiveAttackMode(PlayerAttackMode attackMode)
        {
            if (_activeAttackMode == attackMode)
                return;

            RemoveCurrentModifier();
            _activeAttackMode = attackMode;
            ApplyCurrentModifier();
        }

        /// <summary>装备当前形态的武器；若已有装备则直接替换。</summary>
        public void Equip(WeaponInstance weapon)
        {
            Equip(_activeAttackMode, weapon);
        }

        public void Equip(PlayerAttackMode attackMode, WeaponInstance weapon)
        {
            AssignEquippedWeapon(attackMode, weapon);
            if (_activeAttackMode == attackMode)
            {
                RemoveCurrentModifier();
                ApplyCurrentModifier();
            }
        }

        /// <summary>卸下当前形态武器并返回该武器。</summary>
        public WeaponInstance Unequip()
        {
            return Unequip(_activeAttackMode);
        }

        public WeaponInstance Unequip(PlayerAttackMode attackMode)
        {
            WeaponInstance removed = GetEquippedWeapon(attackMode);
            if (removed == null)
                return null;

            if (_activeAttackMode == attackMode)
                RemoveCurrentModifier();

            AssignEquippedWeapon(attackMode, null);

            if (_activeAttackMode == attackMode)
                ApplyCurrentModifier();

            return removed;
        }

        public void LoadFrom(PlayerAttackMode activeAttackMode, WeaponInstance meleeWeapon, WeaponInstance rangedWeapon)
        {
            RemoveCurrentModifier();
            _meleeWeapon = meleeWeapon;
            _rangedWeapon = rangedWeapon;
            _activeAttackMode = activeAttackMode;
            ApplyCurrentModifier();
        }

        private void AssignEquippedWeapon(PlayerAttackMode attackMode, WeaponInstance weapon)
        {
            if (attackMode == PlayerAttackMode.Ranged)
                _rangedWeapon = weapon;
            else
                _meleeWeapon = weapon;
        }

        private void ApplyCurrentModifier()
        {
            WeaponInstance currentWeapon = EquippedWeapon;
            if (currentWeapon == null)
                return;

            _currentModifier = currentWeapon.ToModifier();
            _stats.AddModifier(_currentModifier);
        }

        private void RemoveCurrentModifier()
        {
            if (_currentModifier != null)
                _stats.RemoveModifier(_currentModifier);

            _currentModifier = null;
        }
    }
}
