using Game.Data;

namespace Game.Presentation
{
    internal enum PlayerCloneTacticalMode
    {
        FollowOwner,
        Investigate,
        MeleePressure,
        RangedHold,
        RangedRetreat,
    }

    internal enum PlayerCloneAttackModeDirective
    {
        None,
        Switch,
    }

    internal readonly struct PlayerCloneCombatPlan
    {
        public PlayerCloneCombatPlan(
            PlayerCloneTacticalMode tacticalMode,
            GameAction requestedAction,
            PlayerAttackMode desiredAttackMode,
            PlayerCloneAttackModeDirective attackModeDirective = PlayerCloneAttackModeDirective.None,
            int skillSlotIndex = -1)
        {
            TacticalMode = tacticalMode;
            RequestedAction = requestedAction;
            DesiredAttackMode = desiredAttackMode;
            AttackModeDirective = attackModeDirective;
            SkillSlotIndex = skillSlotIndex;
        }

        public PlayerCloneTacticalMode TacticalMode { get; }
        public GameAction RequestedAction { get; }
        public PlayerAttackMode DesiredAttackMode { get; }
        public PlayerCloneAttackModeDirective AttackModeDirective { get; }
        public int SkillSlotIndex { get; }
    }
}
