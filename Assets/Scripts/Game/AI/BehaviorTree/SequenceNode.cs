using System.Collections.Generic;

namespace Game.AI
{
    /// <summary>
    /// 顺序节点：从左到右执行子节点，任一子节点 Failure 则返回 Failure；全部 Success 则返回 Success。
    /// </summary>
    public class SequenceNode : IBehaviorNode
    {
        private readonly IBehaviorNode[] _children;

        public SequenceNode(params IBehaviorNode[] children)
        {
            _children = children ?? new IBehaviorNode[0];
        }

        public SequenceNode(IList<IBehaviorNode> children)
        {
            _children = children != null ? new IBehaviorNode[children.Count] : new IBehaviorNode[0];
            if (children != null)
                for (int i = 0; i < children.Count; i++)
                    _children[i] = children[i];
        }

        public TaskStatus Tick(EnemyAIContext ctx)
        {
            foreach (var child in _children)
            {
                var status = child.Tick(ctx);
                if (status == TaskStatus.Failure || status == TaskStatus.Running)
                    return status;
            }
            return TaskStatus.Success;
        }
    }
}
