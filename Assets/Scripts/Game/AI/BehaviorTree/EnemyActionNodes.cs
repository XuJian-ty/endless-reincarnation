using UnityEngine;
using UnityEngine.AI;
using Game.Presentation;

namespace Game.AI
{
    /// <summary>Action: enter hurt intent.</summary>
    public class ActionSetIntentHurt : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Controller == null) return TaskStatus.Failure;
            ctx.Controller.CurrentIntent = new EnemyIntent { Type = EnemyIntentType.Hurt };
            return TaskStatus.Success;
        }
    }

    /// <summary>Action: chase current target.</summary>
    public class ActionSetIntentChase : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Controller == null || ctx.Perception == null || !ctx.Perception.TargetPosition.HasValue)
                return TaskStatus.Failure;

            ctx.Controller.CurrentIntent = new EnemyIntent
            {
                Type = EnemyIntentType.Chase,
                TargetPosition = ctx.Perception.TargetPosition,
            };
            return TaskStatus.Success;
        }
    }

    /// <summary>Action: cast the specified skill slot.</summary>
    public class ActionSetIntentCastSkill : IBehaviorNode
    {
        public int SkillSlot;

        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Controller == null)
                return TaskStatus.Failure;

            // TryStartSkillSlot already writes CurrentIntent, so no duplicate assignment is needed here.
            return ctx.Controller.TryStartSkillSlot(SkillSlot, ctx.Perception?.TargetPosition)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    /// <summary>
    /// Action: tactical combat controller.
    /// Priority: cast immediately when possible; otherwise optimize for shortest time-to-cast,
    /// and keep as much distance as possible within that constraint.
    /// </summary>
    public class ActionSetIntentTacticalCombat : IBehaviorNode
    {
        private static readonly float[] RepositionAngles = { 30f, -30f, 55f, -55f, 80f, -80f, 110f, -110f };

        public float HoldDuration = -1f;
        public float DistanceTolerance = -1f;

        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Controller == null || ctx.Perception == null || ctx.Archetype == null)
                return TaskStatus.Failure;
            if (!ctx.Perception.TargetPosition.HasValue)
                return TaskStatus.Failure;

            EnemyController controller = ctx.Controller;
            EnemyPerception perception = ctx.Perception;
            var archetype = ctx.Archetype;
            Vector3 targetPos = perception.TargetPosition.Value;

            if (!archetype.enableTacticalCombat)
            {
                return SetApproachIntent(controller, targetPos, EnemyIntentType.Chase)
                    ? TaskStatus.Success
                    : TaskStatus.Failure;
            }

            if (TryCastImmediately(controller, perception, archetype))
                return TaskStatus.Success;

            if (!TrySelectPlanningSkill(controller, perception, archetype, out var planSkill, out float planCooldownRemaining))
            {
                Vector3 predicted = perception.PredictTargetPosition(archetype.approachLeadTime);
                SetApproachIntent(controller, predicted, EnemyIntentType.Approach);
                return TaskStatus.Success;
            }

            float desiredDistance = archetype.GetDesiredCombatDistance(planSkill.CastRange);
            float tolerance = DistanceTolerance > 0f
                ? DistanceTolerance
                : Mathf.Max(0.05f, archetype.combatDistanceTolerance);
            float holdDuration = HoldDuration >= 0f
                ? HoldDuration
                : Mathf.Max(0.05f, archetype.tacticalHoldDuration);

            if (!perception.HasLineOfSight)
            {
                if (TryFindRepositionPoint(controller, perception, archetype, desiredDistance, out Vector3 repositionPoint))
                {
                    SetApproachIntent(controller, repositionPoint, EnemyIntentType.Reposition);
                    return TaskStatus.Success;
                }

                Vector3 predicted = perception.PredictTargetPosition(archetype.approachLeadTime);
                SetApproachIntent(controller, predicted, EnemyIntentType.Approach);
                return TaskStatus.Success;
            }

            float distance = perception.DistanceToPlayer;
            if (distance > desiredDistance + tolerance)
            {
                Vector3 predicted = perception.PredictTargetPosition(archetype.approachLeadTime);
                SetApproachIntent(controller, predicted, EnemyIntentType.Approach);
                return TaskStatus.Success;
            }

            if (distance < desiredDistance - tolerance)
            {
                if (TryFindRetreatPoint(controller, archetype, targetPos, desiredDistance, distance, out Vector3 retreatPoint))
                {
                    SetApproachIntent(controller, retreatPoint, EnemyIntentType.Retreat);
                    return TaskStatus.Success;
                }

                if (TryFindRepositionPoint(controller, perception, archetype, desiredDistance, out Vector3 fallbackReposition))
                {
                    SetApproachIntent(controller, fallbackReposition, EnemyIntentType.Reposition);
                    return TaskStatus.Success;
                }
            }

            if (!perception.HasReachablePath && TryFindRepositionPoint(controller, perception, archetype, desiredDistance, out Vector3 pathFixReposition))
            {
                SetApproachIntent(controller, pathFixReposition, EnemyIntentType.Reposition);
                return TaskStatus.Success;
            }

            float idleDuration = Mathf.Clamp(planCooldownRemaining, 0.05f, holdDuration);
            controller.EnterIdle(idleDuration, false);
            return TaskStatus.Success;
        }

        private static bool TryCastImmediately(EnemyController controller, EnemyPerception perception, Game.Data.EnemyArchetypeSO archetype)
        {
            int slotCount = archetype.GetSkillSlotCount();
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (!controller.CanCastSkill(slot))
                    continue;
                if (!perception.IsInSkillRange(slot))
                    continue;
                if (!perception.HasLineOfSight)
                    continue;

                if (controller.TryStartSkillSlot(slot, perception.TargetPosition))
                    return true;
            }

            return false;
        }

        private static bool TrySelectPlanningSkill(
            EnemyController controller,
            EnemyPerception perception,
            Game.Data.EnemyArchetypeSO archetype,
            out EnemyResolvedSkill bestSkill,
            out float bestCooldownRemaining)
        {
            bestSkill = null;
            bestCooldownRemaining = 0f;

            float bestTime = float.MaxValue;
            int bestSlot = int.MaxValue;
            int slotCount = archetype.GetSkillSlotCount();

            for (int slot = 0; slot < slotCount; slot++)
            {
                if (!archetype.IsSkillSlotAvailableInCurrentPhase(slot, controller.CurrentHpRatio))
                    continue;

                EnemyResolvedSkill skill = controller.ResolveSkillSlot(slot);
                if (skill == null)
                    continue;

                float cooldownRemaining = controller.GetSkillCooldownRemaining(slot);
                float approachDistance = Mathf.Max(0f, perception.DistanceToPlayer - skill.CastRange);
                float approachTime = approachDistance / Mathf.Max(0.1f, controller.MoveSpeed);
                float timeToCast = Mathf.Max(cooldownRemaining, approachTime);

                if (!perception.HasLineOfSight)
                    timeToCast += Mathf.Max(0f, archetype.losBlockedPenaltySeconds);

                if (timeToCast < bestTime - 0.01f ||
                    (Mathf.Abs(timeToCast - bestTime) <= 0.01f && slot < bestSlot))
                {
                    bestTime = timeToCast;
                    bestSlot = slot;
                    bestSkill = skill;
                    bestCooldownRemaining = cooldownRemaining;
                }
            }

            return bestSkill != null;
        }

        private static bool TryFindRetreatPoint(
            EnemyController controller,
            Game.Data.EnemyArchetypeSO archetype,
            Vector3 targetPos,
            float desiredDistance,
            float currentDistance,
            out Vector3 retreatPoint)
        {
            retreatPoint = Vector3.zero;

            Vector3 selfPos = controller.transform.position;
            Vector3 away = selfPos - targetPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = -controller.transform.forward;
            away.Normalize();

            float retreatDistance = Mathf.Max(archetype.retreatStepDistance, desiredDistance - currentDistance + archetype.combatDistanceTolerance);
            Vector3 rawCandidate = selfPos + away * retreatDistance;
            return TryGetReachablePoint(selfPos, rawCandidate, out retreatPoint);
        }

        private static bool TryFindRepositionPoint(
            EnemyController controller,
            EnemyPerception perception,
            Game.Data.EnemyArchetypeSO archetype,
            float desiredDistance,
            out Vector3 repositionPoint)
        {
            repositionPoint = Vector3.zero;

            if (!perception.TargetPosition.HasValue)
                return false;

            Vector3 targetPos = perception.TargetPosition.Value;
            Vector3 selfPos = controller.transform.position;
            Vector3 fromPlayerToSelf = selfPos - targetPos;
            fromPlayerToSelf.y = 0f;
            if (fromPlayerToSelf.sqrMagnitude < 0.0001f)
                fromPlayerToSelf = -controller.transform.forward;
            fromPlayerToSelf.Normalize();

            for (int i = 0; i < RepositionAngles.Length; i++)
            {
                Vector3 rotatedDir = Quaternion.Euler(0f, RepositionAngles[i], 0f) * fromPlayerToSelf;
                Vector3 rawCandidate = targetPos + rotatedDir * desiredDistance;
                if (!TryGetReachablePoint(selfPos, rawCandidate, out Vector3 candidate))
                    continue;
                if (!perception.HasLineOfSightFrom(candidate, targetPos))
                    continue;

                repositionPoint = candidate;
                return true;
            }

            return false;
        }

        private static bool TryGetReachablePoint(Vector3 from, Vector3 rawCandidate, out Vector3 sampled)
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

        private static bool SetApproachIntent(EnemyController controller, Vector3 targetPos, EnemyIntentType intentType)
        {
            var current = controller.CurrentIntent;
            if (current.Type == intentType && current.HasTargetPosition)
            {
                Vector3 delta = current.TargetPosition.Value - targetPos;
                delta.y = 0f;
                if (delta.sqrMagnitude <= 0.04f)
                    return true;
            }

            controller.CurrentIntent = new EnemyIntent
            {
                Type = intentType,
                TargetPosition = targetPos,
            };
            return true;
        }
    }

    /// <summary>Action: patrol, with an idle pause after reaching each patrol point.</summary>
    public class ActionSetIntentPatrol : IBehaviorNode
    {
        private const float ArrivalDistance = 0.6f;

        public float IntervalMin = 2f;
        public float IntervalMax = 5f;

        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Controller == null || ctx.Archetype == null)
                return TaskStatus.Failure;

            var controller = ctx.Controller;
            var currentIntent = controller.CurrentIntent;
            float now = Time.time;

            if (currentIntent.Type == EnemyIntentType.Patrol && currentIntent.HasTargetPosition)
            {
                Vector3 delta = currentIntent.TargetPosition.Value - controller.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > ArrivalDistance * ArrivalDistance)
                    return TaskStatus.Success;

                float idleDuration = Random.Range(IntervalMin, IntervalMax);
                controller.EnterIdle(idleDuration, false);
                ctx.NextPatrolTime = now + idleDuration;
                return TaskStatus.Success;
            }

            if (now < ctx.NextPatrolTime)
            {
                float remainingIdle = ctx.NextPatrolTime - now;
                if (currentIntent.Type != EnemyIntentType.Idle)
                    controller.EnterIdle(remainingIdle, false);
                return TaskStatus.Success;
            }

            float patrolRadius = Mathf.Max(0.1f, ctx.Archetype.patrolRadius);
            Vector2 circle = Random.insideUnitCircle * patrolRadius;
            Vector3 randomPoint = ctx.PatrolOrigin + new Vector3(circle.x, 0f, circle.y);
            controller.CurrentIntent = new EnemyIntent
            {
                Type = EnemyIntentType.Patrol,
                TargetPosition = randomPoint,
            };
            return TaskStatus.Success;
        }
    }
}
