using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 背包面板 UI 文案配置：12 项属性显示名、6 种排序模式下拉选项。与 StatsRoot 子物体顺序、SortMode 枚举顺序一致。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/背包UI配置", fileName = "背包UI配置")]
    public class BackpackUIConfigSO : ScriptableObject
    {
        [InspectorLabel("12 项属性显示名")] [Tooltip("顺序：生命、法力、攻击、防御、生命回复、法力回复、暴击率、暴击伤害、攻速、移速、吸血、增伤")]
        public List<string> statDisplayNames = new List<string>
        {
            "生命", "法力", "攻击", "防御", "生命回复", "法力回复",
            "暴击率", "暴击伤害", "攻速", "移速", "吸血", "增伤"
        };
        [InspectorLabel("6 种排序模式下拉选项")] [Tooltip("顺序与 BackpackPresenter.SortMode 枚举一致")]
        public List<string> sortModeLabels = new List<string>
        {
            "品质低→高", "品质高→低",
            "其余优先 品质低→高", "其余优先 品质高→低",
            "武器优先 品质低→高", "武器优先 品质高→低"
        };
    }
}
