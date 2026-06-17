using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 掉落条目：物品类型 + 权重（归属由所在关卡列表项决定）。
    /// itemType 约定：weapon_common / weapon_rare / weapon_epic / weapon_legendary，potion_hp / potion_mp，nectar。
    /// </summary>
    [Serializable]
    public class DropEntry
    {
        [InspectorLabel("物品类型")]
        public string itemType;
        [InspectorLabel("权重")]
        [Tooltip("权重，数字越大越容易出")]
        public float weight;
    }

}
