using Game.Data;

namespace Game.AI
{
    /// <summary>
    /// Default enemy behavior tree: hurt > tactical combat > patrol.
    /// </summary>
    public static class EnemyBehaviorTreeBuilder
    {
        public static IBehaviorNode BuildDefaultTree()
        {
            var hurtBranch = new SequenceNode(
                new CondIsHurt(),
                new ActionSetIntentHurt());

            var evadeBranch = new SequenceNode(
                new CondHasImmediateThreat(),
                new ActionSetIntentEvadeThreat());

            var punishBranch = new SequenceNode(
                new CondHasPunishWindow(),
                new ActionSetIntentPunish());

            var combatBranch = new SequenceNode(
                new CondHasTarget(),
                new ActionSetIntentTacticalCombat());

            var searchBranch = new SequenceNode(
                new CondHasTargetMemory(),
                new ActionSetIntentSearch());

            var patrolAction = new ActionSetIntentPatrol { IntervalMin = 2f, IntervalMax = 5f };
            return new SelectorNode(hurtBranch, evadeBranch, punishBranch, combatBranch, searchBranch, patrolAction);
        }
    }
}
