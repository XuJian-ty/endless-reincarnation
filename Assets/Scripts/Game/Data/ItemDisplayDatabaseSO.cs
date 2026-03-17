using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 单条物品显示配置：背包格子图标与悬停提示用。
    /// </summary>
    [Serializable]
    public class ItemDisplayEntry
    {
        [InspectorLabel("物品ID")] public string itemId = "";
        [InspectorLabel("显示名")] public string displayName = "";
        [InspectorLabel("图标")] public Sprite icon;
        [InspectorLabel("说明")] [TextArea(1, 3)] public string description = "";
        [InspectorLabel("品质")] [Tooltip("用于背包格子背景：仙露=传说，回血/回蓝药剂=精良")] public WeaponRarity rarity = WeaponRarity.Common;
    }

    /// <summary>
    /// 仅数量类物品（金币、天赋点、药剂、仙露等）的图标与说明配置；背包格子和悬停提示按 itemId 查表。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/物品显示配置", fileName = "物品显示配置")]
    public class ItemDisplayDatabaseSO : ScriptableObject
    {
        [Header("掉落物显示")]
        [InspectorLabel("图标缩放")]
        [Min(0.01f)]
        public float iconScale = 0.2f;

        [InspectorLabel("生成高度")]
        public float spawnHeightOffset = 0.5f;

        [InspectorLabel("悬浮幅度")]
        [Min(0f)]
        public float hoverAmplitude = 0.08f;

        [InspectorLabel("悬浮频率")]
        [Min(0f)]
        public float hoverFrequency = 2.4f;

        [InspectorLabel("拾取半径")]
        [Min(0.01f)]
        public float pickupRadius = 0.7f;

        [Header("物品显示")]
        [InspectorLabel("物品列表")] public List<ItemDisplayEntry> entries = new List<ItemDisplayEntry>();

        public ItemDisplayEntry GetEntry(string itemId)
        {
            if (entries == null || string.IsNullOrEmpty(itemId)) return null;
            foreach (var e in entries)
                if (e.itemId == itemId) return e;
            return null;
        }
    }
}
