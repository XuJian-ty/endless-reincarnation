using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public sealed class SkillTreeEdgeRouteOverride
    {
        [InspectorLabel("前驱节点ID")]
        public string predecessorNodeId = "";

        [InspectorLabel("拐点列表")]
        public List<Vector2> waypoints = new List<Vector2>();
    }

    [Serializable]
    public sealed class SkillTreeNodeDefinition
    {
        [InspectorLabel("节点ID")]
        public string nodeId = "";

        [InspectorLabel("技能ID")]
        public string skillId = "";

        [InspectorLabel("节点位置")]
        public Vector2 position = new Vector2(240f, 180f);

        [InspectorLabel("前驱节点ID列表")]
        public List<string> predecessorNodeIds = new List<string>();

        [InspectorLabel("连线拐点覆盖")]
        public List<SkillTreeEdgeRouteOverride> edgeRouteOverrides = new List<SkillTreeEdgeRouteOverride>();

        public bool TryGetRouteOverride(string predecessorNodeId, out SkillTreeEdgeRouteOverride routeOverride)
        {
            routeOverride = null;
            if (edgeRouteOverrides == null || string.IsNullOrWhiteSpace(predecessorNodeId))
                return false;

            string normalized = predecessorNodeId.Trim();
            for (int i = 0; i < edgeRouteOverrides.Count; i++)
            {
                SkillTreeEdgeRouteOverride entry = edgeRouteOverrides[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.predecessorNodeId))
                    continue;

                if (!string.Equals(entry.predecessorNodeId.Trim(), normalized, StringComparison.Ordinal))
                    continue;

                routeOverride = entry;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 技能树图配置：只负责节点布局与前驱关系，不存技能本体数据。
    /// 编辑器窗口和运行时技能树面板共用同一份图坐标。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/技能树图配置", fileName = "技能树图配置")]
    public sealed class SkillTreeGraphConfigSO : ScriptableObject
    {
        [InspectorLabel("图谱内容尺寸")]
        public Vector2 contentSize = new Vector2(2400f, 1400f);

        [InspectorLabel("技能节点列表")]
        public List<SkillTreeNodeDefinition> nodes = new List<SkillTreeNodeDefinition>();

        public SkillTreeNodeDefinition GetNodeById(string nodeId)
        {
            if (nodes == null || string.IsNullOrWhiteSpace(nodeId))
                return null;

            string normalized = nodeId.Trim();
            for (int i = 0; i < nodes.Count; i++)
            {
                SkillTreeNodeDefinition node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.nodeId))
                    continue;

                if (string.Equals(node.nodeId.Trim(), normalized, StringComparison.Ordinal))
                    return node;
            }

            return null;
        }

        public SkillTreeNodeDefinition GetNodeBySkillId(string skillId)
        {
            if (nodes == null || string.IsNullOrWhiteSpace(skillId))
                return null;

            string normalized = skillId.Trim();
            for (int i = 0; i < nodes.Count; i++)
            {
                SkillTreeNodeDefinition node = nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.skillId))
                    continue;

                if (string.Equals(node.skillId.Trim(), normalized, StringComparison.Ordinal))
                    return node;
            }

            return null;
        }

        public bool CanUnlock(string skillId, Func<string, bool> isSkillUnlocked)
        {
            SkillTreeNodeDefinition node = GetNodeBySkillId(skillId);
            if (node == null)
                return false;

            if (isSkillUnlocked == null)
                return node.predecessorNodeIds == null || node.predecessorNodeIds.Count == 0;

            if (node.predecessorNodeIds == null || node.predecessorNodeIds.Count == 0)
                return true;

            for (int i = 0; i < node.predecessorNodeIds.Count; i++)
            {
                string predecessorNodeId = node.predecessorNodeIds[i];
                if (string.IsNullOrWhiteSpace(predecessorNodeId))
                    continue;

                SkillTreeNodeDefinition predecessor = GetNodeById(predecessorNodeId);
                if (predecessor == null || string.IsNullOrWhiteSpace(predecessor.skillId))
                    continue;

                if (isSkillUnlocked(predecessor.skillId.Trim()))
                    return true;
            }

            return false;
        }

        public List<string> CollectValidationErrors(SkillConfigDatabaseSO skillConfig)
        {
            List<string> errors = new List<string>();
            Dictionary<string, SkillTreeNodeDefinition> nodeById = new Dictionary<string, SkillTreeNodeDefinition>(StringComparer.Ordinal);
            Dictionary<string, SkillTreeNodeDefinition> nodeBySkillId = new Dictionary<string, SkillTreeNodeDefinition>(StringComparer.Ordinal);

            if (nodes == null || nodes.Count == 0)
            {
                errors.Add("技能树图配置中没有任何节点。");
                return errors;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                SkillTreeNodeDefinition node = nodes[i];
                if (node == null)
                {
                    errors.Add($"第 {i + 1} 个技能树节点为空。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.nodeId))
                    errors.Add($"第 {i + 1} 个技能树节点缺少节点ID。");
                else
                {
                    string normalizedNodeId = node.nodeId.Trim();
                    if (!nodeById.TryAdd(normalizedNodeId, node))
                        errors.Add($"技能树节点ID重复：{normalizedNodeId}");
                }

                if (string.IsNullOrWhiteSpace(node.skillId))
                {
                    errors.Add($"技能树节点 {node.nodeId} 缺少技能ID。");
                }
                else
                {
                    string normalizedSkillId = node.skillId.Trim();
                    if (!nodeBySkillId.TryAdd(normalizedSkillId, node))
                        errors.Add($"技能树技能ID重复：{normalizedSkillId}");

                    if (skillConfig != null && skillConfig.GetEntry(normalizedSkillId) == null)
                        errors.Add($"技能树节点 {node.nodeId} 引用了不存在的技能ID：{normalizedSkillId}");
                }
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                SkillTreeNodeDefinition node = nodes[i];
                if (node == null || node.predecessorNodeIds == null)
                    continue;

                for (int j = 0; j < node.predecessorNodeIds.Count; j++)
                {
                    string predecessorNodeId = node.predecessorNodeIds[j];
                    if (string.IsNullOrWhiteSpace(predecessorNodeId))
                        continue;

                    string normalized = predecessorNodeId.Trim();
                    if (!nodeById.ContainsKey(normalized))
                        errors.Add($"技能树节点 {node.nodeId} 的前驱节点不存在：{normalized}");
                    else if (string.Equals(node.nodeId?.Trim(), normalized, StringComparison.Ordinal))
                        errors.Add($"技能树节点 {node.nodeId} 不能将自己设置为前驱节点。");
                }
            }

            if (HasCycle(nodeById))
                errors.Add("技能树图配置存在循环前驱关系，请改为无环结构。");

            return errors;
        }

        private static bool HasCycle(Dictionary<string, SkillTreeNodeDefinition> nodeById)
        {
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, SkillTreeNodeDefinition> pair in nodeById)
            {
                if (DetectCycle(pair.Key, nodeById, visiting, visited))
                    return true;
            }

            return false;
        }

        private static bool DetectCycle(
            string nodeId,
            Dictionary<string, SkillTreeNodeDefinition> nodeById,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (visited.Contains(nodeId))
                return false;

            if (!visiting.Add(nodeId))
                return true;

            if (nodeById.TryGetValue(nodeId, out SkillTreeNodeDefinition node) && node?.predecessorNodeIds != null)
            {
                for (int i = 0; i < node.predecessorNodeIds.Count; i++)
                {
                    string predecessorNodeId = node.predecessorNodeIds[i];
                    if (string.IsNullOrWhiteSpace(predecessorNodeId))
                        continue;

                    string normalized = predecessorNodeId.Trim();
                    if (!nodeById.ContainsKey(normalized))
                        continue;

                    if (DetectCycle(normalized, nodeById, visiting, visited))
                        return true;
                }
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
            return false;
        }
    }
}
