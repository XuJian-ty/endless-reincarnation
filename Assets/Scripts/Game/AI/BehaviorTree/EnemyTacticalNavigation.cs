using Game.Data;
using Game.Presentation;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Shared navigation helpers for tactical enemy movement decisions.
    /// </summary>
    public static class EnemyTacticalNavigation
    {
        private static readonly float[] RepositionAnglesClockwiseFirst = { 55f, 30f, 80f, 110f, -30f, -55f, -80f, -110f };
        private static readonly float[] RepositionAnglesCounterClockwiseFirst = { -55f, -30f, -80f, -110f, 30f, 55f, 80f, 110f };

        public static bool TryFindRetreatPoint(
            EnemyController controller,
            EnemyArchetypeSO archetype,
            Vector3 targetPos,
            float desiredDistance,
            float currentDistance,
            out Vector3 retreatPoint)
        {
            retreatPoint = Vector3.zero;
            if (controller == null)
                return false;

            Vector3 selfPos = controller.transform.position;
            Vector3 away = selfPos - targetPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = -controller.transform.forward;
            away.Normalize();

            float stepDistance = archetype != null
                ? Mathf.Max(0.75f, archetype.retreatStepDistance)
                : 2.5f;
            float retreatDistance = Mathf.Max(stepDistance, desiredDistance - currentDistance);
            Vector3 rawCandidate = selfPos + away * retreatDistance;
            rawCandidate.y = targetPos.y;
            return TryResolveTacticalPoint(controller, archetype, selfPos, rawCandidate, out retreatPoint);
        }

        public static bool TryFindRepositionPoint(
            EnemyController controller,
            EnemyPerception perception,
            EnemyArchetypeSO archetype,
            float desiredDistance,
            int preferredStrafeSign,
            out Vector3 repositionPoint)
        {
            repositionPoint = Vector3.zero;
            if (controller == null || perception == null || !perception.TargetPosition.HasValue)
                return false;

            Vector3 targetPos = perception.TargetPosition.Value;
            Vector3 selfPos = controller.transform.position;
            Vector3 fromPlayerToSelf = selfPos - targetPos;
            fromPlayerToSelf.y = 0f;
            if (fromPlayerToSelf.sqrMagnitude < 0.0001f)
                fromPlayerToSelf = -controller.transform.forward;
            fromPlayerToSelf.Normalize();

            float[] angles = preferredStrafeSign >= 0 ? RepositionAnglesClockwiseFirst : RepositionAnglesCounterClockwiseFirst;
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 rotatedDir = Quaternion.Euler(0f, angles[i], 0f) * fromPlayerToSelf;
                Vector3 rawCandidate = targetPos + rotatedDir * desiredDistance;
                if (!TryResolveTacticalPoint(controller, archetype, selfPos, rawCandidate, out Vector3 candidate))
                    continue;
                if (!perception.HasLineOfSightFrom(candidate, targetPos))
                    continue;

                repositionPoint = candidate;
                return true;
            }

            return false;
        }

        public static bool TryFindStrafePoint(
            EnemyController controller,
            EnemyPerception perception,
            EnemyArchetypeSO archetype,
            float desiredDistance,
            int strafeSign,
            out Vector3 strafePoint)
        {
            strafePoint = Vector3.zero;
            if (controller == null || perception == null || !perception.TargetPosition.HasValue)
                return false;

            Vector3 targetPos = perception.TargetPosition.Value;
            Vector3 selfPos = controller.transform.position;
            Vector3 toSelf = selfPos - targetPos;
            toSelf.y = 0f;
            if (toSelf.sqrMagnitude < 0.0001f)
                toSelf = -controller.transform.forward;

            Vector3 radial = toSelf.normalized;
            Vector3 tangential = Quaternion.Euler(0f, 90f * Mathf.Sign(strafeSign == 0 ? 1 : strafeSign), 0f) * radial;
            float lateralStep = archetype != null
                ? Mathf.Max(1.3f, archetype.retreatStepDistance * 0.95f)
                : 2.1f;

            Vector3 rawCandidate = targetPos + radial * desiredDistance + tangential.normalized * lateralStep;
            if (!TryResolveTacticalPoint(controller, archetype, selfPos, rawCandidate, out Vector3 candidate))
                return false;
            if (!perception.HasLineOfSightFrom(candidate, targetPos))
                return false;

            strafePoint = candidate;
            return true;
        }

        public static bool TryFindDodgePoint(
            EnemyController controller,
            EnemyPerception perception,
            EnemyArchetypeSO archetype,
            out Vector3 dodgePoint)
        {
            dodgePoint = Vector3.zero;
            if (controller == null || perception == null || !perception.TargetPosition.HasValue)
                return false;

            int preferredSign = EnemySquadCoordinator.GetPreferredStrafeSign(controller);
            float desiredDistance = Mathf.Max(
                perception.DistanceToPlayer,
                archetype != null ? archetype.GetChaseInnerDistance() : 3f);

            if (TryFindStrafePoint(controller, perception, archetype, desiredDistance, preferredSign, out dodgePoint))
                return true;
            if (TryFindStrafePoint(controller, perception, archetype, desiredDistance, -preferredSign, out dodgePoint))
                return true;
            if (archetype != null &&
                TryFindRetreatPoint(controller, archetype, perception.TargetPosition.Value, desiredDistance, perception.DistanceToPlayer, out dodgePoint))
            {
                return true;
            }

            return false;
        }

        public static bool TryGetReachablePoint(Vector3 from, Vector3 rawCandidate, out Vector3 sampled)
        {
            sampled = Vector3.zero;
            if (!NavMesh.SamplePosition(rawCandidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return false;

            var path = new NavMeshPath();
            bool hasPath = NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path);
            if (!hasPath || path.status != NavMeshPathStatus.PathComplete)
                return false;

            sampled = hit.position;
            return true;
        }

        private static bool TryResolveTacticalPoint(
            EnemyController controller,
            EnemyArchetypeSO archetype,
            Vector3 from,
            Vector3 rawCandidate,
            out Vector3 sampled)
        {
            sampled = Vector3.zero;
            if (controller != null && archetype != null && archetype.UsesAerialMovement())
            {
                sampled = controller.ResolveFlightAnchorPosition(rawCandidate);
                return true;
            }

            return TryGetReachablePoint(from, rawCandidate, out sampled);
        }
    }
}
