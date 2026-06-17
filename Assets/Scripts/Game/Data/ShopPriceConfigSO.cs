using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 商店价格配置：各品质武器、药剂、仙露的售价（需求书示例，可调）。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/商店价格", fileName = "商店价格")]
    public class ShopPriceConfigSO : ScriptableObject
    {
        [Header("武器（按品质）")]
        [InspectorLabel("普通武器")] public int weaponCommonPrice   = 100;
        [InspectorLabel("精良武器")] public int weaponRarePrice     = 200;
        [InspectorLabel("史诗武器")] public int weaponEpicPrice     = 350;
        [InspectorLabel("传说武器")] public int weaponLegendaryPrice = 500;

        [Header("药剂与仙露")]
        [InspectorLabel("回血药剂")] public int potionHpPrice = 100;
        [InspectorLabel("回蓝药剂")] public int potionMpPrice = 100;
        [InspectorLabel("仙露")] public int nectarPrice = 500;

        /// <summary>按品质返回武器售价；未匹配则返回普通价</summary>
        public int GetWeaponPrice(WeaponRarity rarity)
        {
            switch (rarity)
            {
                case WeaponRarity.Rare:      return weaponRarePrice;
                case WeaponRarity.Epic:      return weaponEpicPrice;
                case WeaponRarity.Legendary: return weaponLegendaryPrice;
                default:                     return weaponCommonPrice;
            }
        }

        /// <summary>按 itemId 返回出售单价（用于背包右键出售）；未配置返回 0</summary>
        public int GetItemSellPrice(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            if (itemId == "potion_hp") return potionHpPrice;
            if (itemId == "potion_mp") return potionMpPrice;
            if (itemId == "nectar") return nectarPrice;
            return 0;
        }
    }
}
