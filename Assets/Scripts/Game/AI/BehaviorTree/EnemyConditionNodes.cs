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

    /// <summary>Condition: skill slot can be cast (phase + cooldown + runtime state).</summary>
    public class CondCanCastSkill : IBehaviorNode
    {
        public int SkillSlot;

        public TaskStatus Tick(EnemyAIContext ctx)
        {
            return (ctx?.Controller != null && ctx.Controller.CanCastSkill(SkillSlot)) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    /// <summary>Condition: target is in range and visible for this slot.</summary>
    public class CondIsInSkillRange : IBehaviorNode
    {
        public int SkillSlot;

        public TaskStatus Tick(EnemyAIContext ctx)
        {
            if (ctx?.Perception == null)
                return TaskStatus.Failure;

            return (ctx.Perception.IsInSkillRange(SkillSlot) && ctx.Perception.HasLineOfSight)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }

    /// <summary>Condition: current target is visible (line of sight clear).</summary>
    public class CondHasLineOfSight : IBehaviorNode
    {
        public TaskStatus Tick(EnemyAIContext ctx)
        {
            return (ctx?.Perception != null && ctx.Perception.HasLineOfSight)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }
}
