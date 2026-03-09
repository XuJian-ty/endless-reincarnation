using System.Collections.Generic;

namespace Game.AI
{
    /// <summary>
    /// 选择节点：从左到右执行子节点，任一子节点 Success 则返回 Success；全部 Failure 则返回 Failure。
    /// </summary>
    public class SelectorNode : IBehaviorNode
    {
        private readonly IBehaviorNode[] _children;

        public SelectorNode(params IBehaviorNode[] children)
        {
            _children = children ?? new IBehaviorNode[0];
        }

        public SelectorNode(IList<IBehaviorNode> children)
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
                if (status == TaskStatus.Success || status == TaskStatus.Running)
                    return status;
            }
            return TaskStatus.Failure;
        }
    }
}
