using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public class PlayerInitialItemEntry
    {
        [InspectorLabel("物品ID")]
        [Tooltip("填写物品显示配置和道具逻辑使用的 itemId。金币使用 gold，天赋点使用 talent_point。")]
        public string itemId = "";

        [InspectorLabel("初始数量")]
        [Min(0)]
        public int count;
    }

    /// <summary>
    /// 创建新角色时写入存档的初始道具数量配置。只在新存档创建时读取。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/角色初始道具配置", fileName = "角色初始道具配置")]
    public class PlayerInitialItemConfigSO : ScriptableObject
    {
        [InspectorLabel("初始道具列表")]
        public List<PlayerInitialItemEntry> initialItems = new List<PlayerInitialItemEntry>();
    }
}
