using Game.Data;

namespace Game.AI
{
    /// <summary>
    /// Default enemy behavior tree: hurt > tactical combat > patrol.
    /// </summary>
    public static class EnemyBehaviorTreeBuilder
    {
        public static IBehaviorNode BuildDefaultTree(EnemyArchetypeSO archetype = null)
        {
            var hurtBranch = new SequenceNode(
                new CondIsHurt(),
                new ActionSetIntentHurt());

            var combatBranch = new SequenceNode(
                new CondHasTarget(),
                new ActionSetIntentTacticalCombat());

            var patrolAction = new ActionSetIntentPatrol { IntervalMin = 2f, IntervalMax = 5f };
            return new SelectorNode(hurtBranch, combatBranch, patrolAction);
        }
    }
}
