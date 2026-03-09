using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// 根据扁平节点列表构建运行时 IBehaviorNode 树，避免递归序列化深度限制。
    /// </summary>
    public static class BehaviorTreeFromDataBuilder
    {
        /// <summary>
        /// 从资产的扁平节点列表构建树；根为 parentIndex==-1 的节点（取第一个）。若无有效根则返回 null。
        /// </summary>
        public static IBehaviorNode Build(BehaviorTreeAsset asset)
        {
            if (asset == null || asset.nodes == null || asset.nodes.Count == 0) return null;
            var nodes = asset.nodes;
            int rootIndex = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].parentIndex == -1)
                {
                    rootIndex = i;
                    break;
                }
            }
            if (rootIndex < 0) return null;
            return BuildNode(nodes, rootIndex, BuildChildMap(nodes));
        }

        private static Dictionary<int, List<int>> BuildChildMap(List<BehaviorTreeNodeData> nodes)
        {
            var map = new Dictionary<int, List<int>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null) continue;
                int p = nodes[i].parentIndex;
                if (!map.ContainsKey(p)) map[p] = new List<int>();
                map[p].Add(i);
            }
            foreach (var list in map.Values)
                list.Sort((a, b) => nodes[a].siblingOrder.CompareTo(nodes[b].siblingOrder));
            return map;
        }

        private static IBehaviorNode BuildNode(List<BehaviorTreeNodeData> nodes, int index, Dictionary<int, List<int>> childMap)
        {
            var data = nodes[index];
            if (data == null || string.IsNullOrEmpty(data.nodeType)) return null;

            string type = data.nodeType.Trim();
            if (type == "Selector" || type == "Sequence")
            {
                if (!childMap.TryGetValue(index, out var childIndices) || childIndices.Count == 0) return null;
                var childNodes = new List<IBehaviorNode>();
                foreach (int ci in childIndices)
                {
                    var cn = BuildNode(nodes, ci, childMap);
                    if (cn != null) childNodes.Add(cn);
                }
                if (childNodes.Count == 0) return null;
                return type == "Selector" ? (IBehaviorNode)new SelectorNode(childNodes) : new SequenceNode(childNodes);
            }

            return CreateLeafNode(data);
        }

        private static IBehaviorNode CreateLeafNode(BehaviorTreeNodeData data)
        {
            string type = data.nodeType.Trim();
            switch (type)
            {
                case "CondHasTarget": return new CondHasTarget();
                case "CondCanCastSkill":
                    return new CondCanCastSkill { SkillSlot = data.paramInt1 };
                case "CondIsInSkillRange":
                    return new CondIsInSkillRange { SkillSlot = data.paramInt1 };
                case "CondHasLineOfSight":
                    return new CondHasLineOfSight();
                case "CondIsHurt": return new CondIsHurt();
                case "ActionSetIntentHurt": return new ActionSetIntentHurt();
                case "ActionSetIntentChase": return new ActionSetIntentChase();
                case "ActionSetIntentCastSkill":
                    return new ActionSetIntentCastSkill { SkillSlot = data.paramInt1 };
                case "ActionSetIntentTacticalCombat":
                    return new ActionSetIntentTacticalCombat
                    {
                        HoldDuration = data.paramFloat1 > 0f ? data.paramFloat1 : -1f,
                        DistanceTolerance = data.paramFloat2 > 0f ? data.paramFloat2 : -1f,
                    };
                case "ActionSetIntentPatrol":
                    var patrol = new ActionSetIntentPatrol();
                    patrol.IntervalMin = data.paramFloat1 > 0.001f ? data.paramFloat1 : 2f;
                    patrol.IntervalMax = data.paramFloat2 > 0.001f ? data.paramFloat2 : 5f;
                    return patrol;
                default:
                    Debug.LogWarning($"[BehaviorTree] 未知节点类型: {type}");
                    return null;
            }
        }
    }
}
