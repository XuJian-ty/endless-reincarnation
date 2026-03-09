namespace Game.Presentation
{
    /// <summary>
    /// 带有时长或进度的状态可实现此接口，供 HUD 显示无敌条、硬直条等。
    /// 非时长状态不实现，查询端用 StateRemainingTime &lt; 0 判断。
    /// </summary>
    public interface IPlayerStateWithDuration
    {
        /// <summary>剩余时间（秒），无明确时长时返回 -1</summary>
        float RemainingTime { get; }
        /// <summary>进度 0～1，无进度概念时返回 -1</summary>
        float NormalizedProgress { get; }
    }
}
