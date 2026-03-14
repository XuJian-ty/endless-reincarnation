using System.Collections.Generic;
using Game.Data;
using Game.Presentation;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Lightweight squad coordination so nearby enemies do not all pressure the player at once.
    /// </summary>
    public static class EnemySquadCoordinator
    {
        private sealed class PressureClaim
        {
            public EnemyController controller;
            public int enemyId;
            public float expiresAt;
        }

        private static readonly List<PressureClaim> ActiveClaims = new List<PressureClaim>();

        public static bool CanPressure(EnemyController controller, EnemyArchetypeSO archetype, float now)
        {
            if (controller == null || archetype == null)
                return true;

            Prune(now);

            int selfId = controller.GetInstanceID();
            Vector3 selfPosition = controller.transform.position;
            float coordinationRadius = ResolveCoordinationRadius(archetype);
            float coordinationRadiusSqr = coordinationRadius * coordinationRadius;
            int activeCount = 0;
            for (int i = 0; i < ActiveClaims.Count; i++)
            {
                PressureClaim claim = ActiveClaims[i];
                if (claim.enemyId == selfId)
                    continue;

                if (claim.controller == null)
                    continue;

                Vector3 delta = claim.controller.transform.position - selfPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude > coordinationRadiusSqr)
                    continue;

                activeCount++;
            }

            return activeCount < ResolvePressureLimit(archetype);
        }

        public static void RegisterPressure(EnemyController controller, EnemyArchetypeSO archetype, float now, float duration)
        {
            if (controller == null || archetype == null)
                return;

            Prune(now);

            float validDuration = Mathf.Max(0.1f, duration);
            int selfId = controller.GetInstanceID();
            for (int i = 0; i < ActiveClaims.Count; i++)
            {
                if (ActiveClaims[i].enemyId != selfId)
                    continue;

                ActiveClaims[i].controller = controller;
                ActiveClaims[i].expiresAt = now + validDuration;
                return;
            }

            ActiveClaims.Add(new PressureClaim
            {
                controller = controller,
                enemyId = selfId,
                expiresAt = now + validDuration
            });
        }

        public static int GetPreferredStrafeSign(EnemyController controller)
        {
            if (controller == null)
                return 1;

            return (controller.GetInstanceID() & 1) == 0 ? 1 : -1;
        }

        public static void Release(EnemyController controller)
        {
            if (controller == null)
                return;

            int selfId = controller.GetInstanceID();
            for (int i = ActiveClaims.Count - 1; i >= 0; i--)
            {
                if (ActiveClaims[i].enemyId == selfId)
                    ActiveClaims.RemoveAt(i);
            }
        }

        private static void Prune(float now)
        {
            for (int i = ActiveClaims.Count - 1; i >= 0; i--)
            {
                PressureClaim claim = ActiveClaims[i];
                if (claim == null || claim.controller == null || claim.expiresAt <= now)
                    ActiveClaims.RemoveAt(i);
            }
        }

        private static int ResolvePressureLimit(EnemyArchetypeSO archetype)
        {
            if (archetype == null)
                return 1;

            if (archetype.maxPressureAllies > 0)
                return archetype.maxPressureAllies;

            return archetype.enemyType switch
            {
                EnemyType.MeleeMinion => 1,
                EnemyType.RangedMinion => 2,
                EnemyType.Elite => 2,
                EnemyType.Guardian => 1,
                EnemyType.Boss => 3,
                _ => 1,
            };
        }

        private static float ResolveCoordinationRadius(EnemyArchetypeSO archetype)
        {
            if (archetype == null)
                return 12f;

            float chaseOuter = archetype.GetChaseOuterDistance();
            return Mathf.Clamp(chaseOuter + 4f, 8f, 18f);
        }
    }
}
