namespace Game.Presentation
{
    /// <summary>
    /// 玩家状态统一暴露剩余时长与归一化进度，供 HUD、调试和敌人观察层读取。
    /// 无明确时长或进度概念时返回 -1。
    /// </summary>
    public interface IPlayerStateWithDuration
    {
        /// <summary>剩余时间（秒），无明确时长时返回 -1</summary>
        float RemainingTime { get; }
        /// <summary>进度 0～1，无进度概念时返回 -1</summary>
        float NormalizedProgress { get; }
    }
}
