using Game.Data;
using Game.Domain;
using UnityEngine;

namespace Game.Presentation
{
    public static class PlayerWeaponVisualUtility
    {
        public const string WeaponMountName = "手持武器挂点";
        public const string BackWeaponMountName = "背上武器挂点";
        public const string MeleeWeaponPointName = "剑挂点";
        public const string RangedWeaponPointName = "枪挂点";
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
            if (!TryResolveDualMountWeaponObjects(
                    root,
                    out Transform handMeleePoint,
                    out Transform handRangedPoint,
                    out Transform backMeleePoint,
                    out Transform backRangedPoint,
                    out Transform meleeWeapon,
                    out Transform rangedWeapon))
                return;

            ApplyDualMountLayout(
                currentAttackMode,
                hasMeleeWeapon,
                hasRangedWeapon,
                handMeleePoint,
                handRangedPoint,
                backMeleePoint,
                backRangedPoint,
                meleeWeapon,
                rangedWeapon);
        }

        private static void ApplyDualMountLayout(
            PlayerAttackMode currentAttackMode,
            bool hasMeleeWeapon,
            bool hasRangedWeapon,
            Transform handMeleePoint,
            Transform handRangedPoint,
            Transform backMeleePoint,
            Transform backRangedPoint,
            Transform meleeWeapon,
            Transform rangedWeapon)
        {
            if (meleeWeapon != null)
            {
                bool shouldShowMelee = hasMeleeWeapon;
                if (shouldShowMelee)
                {
                    Transform meleeTargetPoint = currentAttackMode == PlayerAttackMode.Melee
                        ? handMeleePoint
                        : backMeleePoint;
                    TryMoveWeaponToPoint(meleeWeapon, meleeTargetPoint);
                }
                meleeWeapon.gameObject.SetActive(shouldShowMelee);
            }

            if (rangedWeapon != null)
            {
                bool shouldShowRanged = hasRangedWeapon;
                if (shouldShowRanged)
                {
                    Transform rangedTargetPoint = currentAttackMode == PlayerAttackMode.Ranged
                        ? handRangedPoint
                        : backRangedPoint;
                    TryMoveWeaponToPoint(rangedWeapon, rangedTargetPoint);
                }
                rangedWeapon.gameObject.SetActive(shouldShowRanged);
            }
        }

        private static void TryMoveWeaponToPoint(Transform weapon, Transform targetPoint)
        {
            if (weapon == null || targetPoint == null)
                return;

            if (weapon.parent == targetPoint)
                return;

            weapon.SetParent(targetPoint, false);
        }

        private static bool TryResolveDualMountWeaponObjects(
            Transform root,
            out Transform handMeleePoint,
            out Transform handRangedPoint,
            out Transform backMeleePoint,
            out Transform backRangedPoint,
            out Transform meleeWeapon,
            out Transform rangedWeapon)
        {
            handMeleePoint = null;
            handRangedPoint = null;
            backMeleePoint = null;
            backRangedPoint = null;
            meleeWeapon = null;
            rangedWeapon = null;

            if (root == null)
                return false;

            Transform handMount = FindDescendant(root, WeaponMountName);
            Transform backMount = FindDescendant(root, BackWeaponMountName);
            if (handMount == null || backMount == null)
                return false;

            handMeleePoint = FindDescendant(handMount, MeleeWeaponPointName);
            handRangedPoint = FindDescendant(handMount, RangedWeaponPointName);
            backMeleePoint = FindDescendant(backMount, MeleeWeaponPointName);
            backRangedPoint = FindDescendant(backMount, RangedWeaponPointName);

            if (handMeleePoint == null || handRangedPoint == null || backMeleePoint == null || backRangedPoint == null)
                return false;

            meleeWeapon = ResolveWeaponTransform(root, handMeleePoint, backMeleePoint, MeleeWeaponName);
            rangedWeapon = ResolveWeaponTransform(root, handRangedPoint, backRangedPoint, RangedWeaponName);
            return meleeWeapon != null || rangedWeapon != null;
        }

        private static Transform ResolveWeaponTransform(
            Transform root,
            Transform handPoint,
            Transform backPoint,
            string weaponName)
        {
            Transform weapon = FindDescendant(root, weaponName);
            if (weapon != null)
                return weapon;

            weapon = TryGetSingleChild(handPoint);
            if (weapon != null)
                return weapon;

            return TryGetSingleChild(backPoint);
        }

        private static Transform TryGetSingleChild(Transform point)
        {
            if (point == null || point.childCount == 0)
                return null;

            return point.GetChild(0);
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
