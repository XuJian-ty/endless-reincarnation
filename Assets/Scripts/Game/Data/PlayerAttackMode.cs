using System;

namespace Game.Data
{
    public enum PlayerAttackMode
    {
        Melee = 0,
        Ranged = 1
    }

    [Flags]
    public enum PlayerAttackModeMask
    {
        None = 0,
        Melee = 1 << 0,
        Ranged = 1 << 1,
        All = Melee | Ranged
    }

    public enum PlayerFormActionSlot
    {
        Attack0 = 0,
        Attack1 = 1,
        Attack2 = 2,
        Attack3 = 3,
        AirAttack = 4,
        ChargeStart = 5,
        ChargeLoop = 6,
        ChargeRelease = 7,
    }

    public static class PlayerAttackModeUtility
    {
        public static PlayerAttackMode GetOpposite(PlayerAttackMode attackMode)
        {
            return attackMode == PlayerAttackMode.Melee
                ? PlayerAttackMode.Ranged
                : PlayerAttackMode.Melee;
        }

        public static PlayerAttackModeMask ToMask(PlayerAttackMode attackMode)
        {
            return attackMode == PlayerAttackMode.Melee
                ? PlayerAttackModeMask.Melee
                : PlayerAttackModeMask.Ranged;
        }

        public static PlayerAttackMode GetAttackModeForWeaponType(WeaponType weaponType)
        {
            return weaponType == WeaponType.RangedGun
                ? PlayerAttackMode.Ranged
                : PlayerAttackMode.Melee;
        }

        public static string GetDisplayName(PlayerAttackMode attackMode)
        {
            return attackMode == PlayerAttackMode.Melee ? "近战形态" : "远程形态";
        }
    }
}
