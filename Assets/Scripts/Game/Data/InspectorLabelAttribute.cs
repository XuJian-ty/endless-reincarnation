using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 仅用于在 Unity Inspector 中显示中文（或自定义）标签，不改变代码中的变量名。
    /// 用法：[InspectorLabel("生命上限")] public float baseHp = 100f;
    /// </summary>
    public class InspectorLabelAttribute : PropertyAttribute
    {
        public string Label { get; }

        public InspectorLabelAttribute(string label)
        {
            Label = label;
        }
    }
}
