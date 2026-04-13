namespace Game.Presentation
{
    /// <summary>
    /// 战斗动作观察快照。
    /// 统一封装当前动作、剩余时长和归一化进度，供感知层按同一数据形状读取玩家/分身状态。
    /// </summary>
    public readonly struct CombatActionObservation
    {
        public CombatActionObservation(GameAction action, float remainingTime, float normalizedProgress)
        {
            Action = action;
            RemainingTime = remainingTime;
            NormalizedProgress = normalizedProgress;
        }

        public GameAction Action { get; }
        public float RemainingTime { get; }
        public float NormalizedProgress { get; }

        public static CombatActionObservation From(ICombatActionReadable source)
        {
            return source != null
                ? new CombatActionObservation(source.CurrentActionId, source.StateRemainingTime, source.StateNormalizedProgress)
                : default;
        }
    }
}
