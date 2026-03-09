using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.AI
{
    /// <summary>
    /// 行为树资产：扁平节点列表，在 Inspector 中编辑；parentIndex=-1 的节点为根（仅一个）。
    /// 不挂则 EnemyAI 使用代码默认树。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/AI/行为树资产", fileName = "行为树_")]
    public class BehaviorTreeAsset : ScriptableObject
    {
        [InspectorLabel("节点列表")]
        [Tooltip("扁平列表。parentIndex=-1 的项为根节点；通过 parentIndex、siblingOrder 表示树结构")]
        public List<BehaviorTreeNodeData> nodes = new List<BehaviorTreeNodeData>();
    }
}
