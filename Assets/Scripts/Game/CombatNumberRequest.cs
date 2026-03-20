using UnityEngine;
using ProjectBase;

namespace Game
{
    public enum CombatNumberKind
    {
        Damage,
        Heal,
        Mana,
    }

    public sealed class CombatNumberRequest
    {
        public Transform anchor;
        public Vector3 worldPosition;
        public CombatNumberKind kind;
        public float amount;
        public bool isCritical;
    }

    public static class CombatNumberDispatcher
    {
        private const string NumberAnchorName = "NumberAnchor";

        public static void PublishDamage(Transform anchor, float amount, bool isCritical)
        {
            Publish(anchor, CombatNumberKind.Damage, amount, isCritical);
        }

        public static void PublishHeal(Transform anchor, float amount)
        {
            Publish(anchor, CombatNumberKind.Heal, amount, false);
        }

        public static void PublishMana(Transform anchor, float amount)
        {
            Publish(anchor, CombatNumberKind.Mana, amount, false);
        }

        private static void Publish(Transform anchor, CombatNumberKind kind, float amount, bool isCritical)
        {
            if (anchor == null || amount <= 0f)
                return;

            Transform resolvedAnchor = ResolveNumberAnchor(anchor);
            if (resolvedAnchor == null)
            {
                Debug.LogWarning($"[CombatNumberDispatcher] 目标 {anchor.name} 缺少 NumberAnchor，已跳过飘字请求。");
                return;
            }

            EventCenter.GetInstance().EventTrigger(GameEvents.CombatNumberRequested, new CombatNumberRequest
            {
                anchor = resolvedAnchor,
                worldPosition = resolvedAnchor.position,
                kind = kind,
                amount = amount,
                isCritical = isCritical,
            });
        }

        private static Transform ResolveNumberAnchor(Transform root)
        {
            if (root == null)
                return null;

            if (root.name == NumberAnchorName)
                return root;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == NumberAnchorName)
                    return child;
            }

            return null;
        }
    }
}
