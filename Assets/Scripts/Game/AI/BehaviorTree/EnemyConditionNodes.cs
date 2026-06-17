namespace Game.AI
{
    /// <summary>Condition: has a valid target.</summary>
    public class CondHasTarget : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Perception == null) return TaskStatus.Failure;
            return (ctx.Perception.HasTarget && ctx.Perception.TargetPosition.HasValue) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    /// <summary>Condition: currently in hurt state.</summary>
    public class CondIsHurt : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            return (ctx?.Controller != null && ctx.Controller.IsHurt) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    /// <summary>Condition: perceived player action is dangerous enough to warrant an evasive response.</summary>
    public class CondHasImmediateThreat : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            return (ctx?.Perception != null && ctx.Perception.HasImmediateThreat)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    /// <summary>Condition: player seems to be in recovery, creating a punish opportunity.</summary>
    public class CondHasPunishWindow : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            return (ctx?.Perception != null && ctx.Perception.HasPunishOpportunity)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    /// <summary>Condition: enemy recently saw the target and should search the last known position.</summary>
    public class CondHasTargetMemory : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Perception == null || ctx.Archetype == null)
                return TaskStatus.Failure;

            return (!ctx.Perception.HasTarget && ctx.Perception.HasRecentTargetMemory(ctx.Archetype.searchMemoryDuration))
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }
}
