using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public class KeyRebindEntry
    {
        [InspectorLabel("Action 名")] public string actionName = "";
        [InspectorLabel("复合绑定子名")] [Tooltip("如 Move 的 up/down/left/right，无则留空")]
        public string bindingPart = "";
        [InspectorLabel("显示名")] public string displayName = "";
    }

    /// <summary>
    /// 按键配置面板的动态行数据：Action 名、复合绑定子名（可选）、显示名。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/按键重绑定配置", fileName = "按键重绑定配置")]
    public class KeyRebindConfigSO : ScriptableObject
    {
        [InspectorLabel("按键行列表")]
        public List<KeyRebindEntry> entries = new List<KeyRebindEntry>
        {
            new KeyRebindEntry { actionName = "Skill0", displayName = "技能1" },
            new KeyRebindEntry { actionName = "Skill1", displayName = "技能2" },
            new KeyRebindEntry { actionName = "Skill2", displayName = "技能3" },
            new KeyRebindEntry { actionName = "Skill3", displayName = "技能4" },
            new KeyRebindEntry { actionName = "UseHealthPotion", displayName = "回血药剂" },
            new KeyRebindEntry { actionName = "UseManaPotion", displayName = "回蓝药剂" },
            new KeyRebindEntry { actionName = "ToggleKeyConfig", displayName = "按键配置" },
            new KeyRebindEntry { actionName = "ToggleBackpack", displayName = "背包" },
            new KeyRebindEntry { actionName = "ToggleSkillTree", displayName = "技能树" },
            new KeyRebindEntry { actionName = "ToggleMenu", displayName = "菜单" },
            new KeyRebindEntry { actionName = "Dodge", displayName = "闪避" },
            new KeyRebindEntry { actionName = "Jump", displayName = "跳跃" },
            new KeyRebindEntry { actionName = "Move", bindingPart = "up", displayName = "移动-上" },
            new KeyRebindEntry { actionName = "Move", bindingPart = "down", displayName = "移动-下" },
            new KeyRebindEntry { actionName = "Move", bindingPart = "left", displayName = "移动-左" },
            new KeyRebindEntry { actionName = "Move", bindingPart = "right", displayName = "移动-右" },
        };
    }
}
