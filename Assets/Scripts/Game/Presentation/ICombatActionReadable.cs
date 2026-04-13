namespace Game.Presentation
{
    /// <summary>
    /// 统一暴露战斗动作观察所需的最小只读状态。
    /// 用于感知层读取玩家/分身当前动作、剩余时长和进度，而不关心其内部实现是状态机还是专用执行器。
    /// </summary>
    public interface ICombatActionReadable
    {
        GameAction CurrentActionId { get; }
        float StateRemainingTime { get; }
        float StateNormalizedProgress { get; }
    }
}
