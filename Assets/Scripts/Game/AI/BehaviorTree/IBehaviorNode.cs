namespace Game.AI
{
    /// <summary>
    /// 行为树节点接口，每帧由 Runner 调用 Tick。
    /// </summary>
    public interface IBehaviorNode
    {
        TaskStatus Tick(EnemyAIContext ctx);
    }
}
