using UnityEngine;
using Game.Presentation;

namespace Game.AI
{
    /// <summary>Action: enter hurt intent.</summary>
    public class ActionSetIntentHurt : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Controller == null) return TaskStatus.Failure;
            EnemySquadCoordinator.Release(ctx.Controller);
            ctx.Controller.CurrentIntent = new EnemyIntent { Type = EnemyIntentType.Hurt };
            return TaskStatus.Success;
        }
    }

    /// <summary>Action: evade an imminent player threat.</summary>
    public class ActionSetIntentEvadeThreat : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            return EnemyUtilityCombatPlanner.TryExecute(ctx, EnemyCombatDecisionFocus.ThreatResponse)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    /// <summary>Action: exploit a punish opportunity.</summary>
    public class ActionSetIntentPunish : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            return EnemyUtilityCombatPlanner.TryExecute(ctx, EnemyCombatDecisionFocus.Punish)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    /// <summary>Action: search the last known player position.</summary>
    public class ActionSetIntentSearch : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            return EnemyUtilityCombatPlanner.TryExecute(ctx, EnemyCombatDecisionFocus.Search)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    /// <summary>
    /// Action: tactical combat controller.
    /// </summary>
    public class ActionSetIntentTacticalCombat : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Controller == null || ctx.Perception == null || ctx.Archetype == null)
                return TaskStatus.Failure;
            if (!ctx.Perception.TargetPosition.HasValue)
                return TaskStatus.Failure;

            return EnemyUtilityCombatPlanner.TryExecute(ctx, EnemyCombatDecisionFocus.Standard)
                ? TaskStatus.Success
                : TaskStatus.Failure;
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

            EnemySquadCoordinator.Release(ctx.Controller);

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
                TargetPosition = controller.ResolveFlightAnchorPosition(randomPoint),
            };
            return TaskStatus.Success;
        }
    }
}
