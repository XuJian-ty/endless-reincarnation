namespace Game.GameFlow
{
    /// <summary>
    /// Boss 进度服务接口：击败 Boss 后写 Checkpoint、保存、弹选关面板等。
    /// 可由 LevelDirector 或独立 MonoBehaviour 实现，便于测试与替换。
    /// </summary>
    public interface IBossProgressService
    {
        /// <summary>Boss 被击败时调用；写 checkpoint、SaveCurrent、可选弹本关/下一关面板</summary>
        void OnBossDefeated(string bossId);
    }
}
