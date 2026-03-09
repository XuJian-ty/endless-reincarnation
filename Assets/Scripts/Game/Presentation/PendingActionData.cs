namespace Game.Presentation
{
    /// <summary>
    /// 预输入缓存数据。存储一条待执行的 GameAction 及其附加参数。
    /// PlayerStateMachine 持有一个此实例，后来的覆盖先来的（最多缓存一条）。
    /// </summary>
    public readonly struct PendingActionData
    {
        public static readonly PendingActionData Empty = new PendingActionData(GameAction.None);

        public readonly GameAction Action;

        /// <summary>仅对 GameAction.Skill 有意义（0-3），其他动作忽略此字段</summary>
        public readonly int SkillIndex;

        public bool IsEmpty => Action == GameAction.None;

        public PendingActionData(GameAction action, int skillIndex = 0)
        {
            Action     = action;
            SkillIndex = skillIndex;
        }
    }
}
