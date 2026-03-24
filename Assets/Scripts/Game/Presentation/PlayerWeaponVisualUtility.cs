using Game.Data;
using Game.Domain;
using UnityEngine;

namespace Game.Presentation
{
    public static class PlayerWeaponVisualUtility
    {
        public const string WeaponMountName = "手持武器挂点";
        public const string MeleeWeaponName = "剑";
        public const string RangedWeaponName = "枪";

        public static void ApplyCurrentLoadout(Transform root, PlayerModel playerModel)
        {
            if (playerModel == null)
            {
                ApplyWeaponVisibility(root, PlayerAttackMode.Melee, false, false);
                return;
            }

            ApplyWeaponVisibility(
                root,
                playerModel.CurrentAttackMode,
                playerModel.GetEquippedWeapon(PlayerAttackMode.Melee) != null,
                playerModel.GetEquippedWeapon(PlayerAttackMode.Ranged) != null);
        }

        public static void ApplyWeaponVisibility(
            Transform root,
            PlayerAttackMode currentAttackMode,
            bool hasMeleeWeapon,
            bool hasRangedWeapon)
        {
            if (!TryResolveWeaponObjects(root, out Transform meleeWeapon, out Transform rangedWeapon))
                return;

            bool showMelee = currentAttackMode == PlayerAttackMode.Melee && hasMeleeWeapon;
            bool showRanged = currentAttackMode == PlayerAttackMode.Ranged && hasRangedWeapon;

            if (meleeWeapon != null)
                meleeWeapon.gameObject.SetActive(showMelee);

            if (rangedWeapon != null)
                rangedWeapon.gameObject.SetActive(showRanged);
        }

        private static bool TryResolveWeaponObjects(Transform root, out Transform meleeWeapon, out Transform rangedWeapon)
        {
            meleeWeapon = null;
            rangedWeapon = null;
            if (root == null)
                return false;

            Transform weaponMount = FindDescendant(root, WeaponMountName);
            if (weaponMount == null)
                return false;

            meleeWeapon = FindDescendant(weaponMount, MeleeWeaponName);
            rangedWeapon = FindDescendant(weaponMount, RangedWeaponName);
            return meleeWeapon != null || rangedWeapon != null;
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null && transform.name == targetName)
                    return transform;
            }

            return null;
        }
    }
}
