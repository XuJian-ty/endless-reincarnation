using System;
using UnityEngine;
using Game.Data;

namespace Game.AI
{
    /// <summary>
    /// 可序列化的行为树节点描述（扁平列表项），用于数据驱动，避免递归嵌套超过 Unity 序列化深度限制。
    /// 通过 parentIndex、siblingOrder 表达父子与顺序，运行时由 BehaviorTreeFromDataBuilder 从节点列表构建 IBehaviorNode 树。
    /// </summary>
    [Serializable]
    public class BehaviorTreeNodeData
    {
        [InspectorLabel("节点类型")]
        [Tooltip("Selector / Sequence / CondHasTarget / CondCanCastSkill / CondIsInSkillRange / CondHasLineOfSight / CondIsHurt / ActionSetIntentHurt / ActionSetIntentChase / ActionSetIntentCastSkill / ActionSetIntentTacticalCombat / ActionSetIntentPatrol")]
        public string nodeType = "Selector";

        [InspectorLabel("父节点下标")]
        [Tooltip("父节点在 nodes 列表中的下标；-1 表示根节点（仅允许一个）")]
        public int parentIndex = -1;
        [InspectorLabel("兄弟顺序")]
        [Tooltip("在同父节点下的兄弟顺序，0 表示第一个")]
        public int siblingOrder;

        [Header("可选参数（部分动作节点使用）")]
        [InspectorLabel("参数1(如巡逻间隔最小值)")]
        [Tooltip("ActionSetIntentPatrol：巡逻间隔最小值（秒）。若为 0 则用默认 2")]
        public float paramFloat1 = 2f;
        [InspectorLabel("参数2(如巡逻间隔最大值)")]
        [Tooltip("ActionSetIntentPatrol：巡逻间隔最大值（秒）。若为 0 则用默认 5")]
        public float paramFloat2 = 5f;
        [InspectorLabel("参数整数(如技能槽)")]
        [Tooltip("预留，如技能槽位等")]
        public int paramInt1;
    }
}
