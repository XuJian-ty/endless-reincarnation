using UnityEngine;

namespace Game.Data
{
    [System.Serializable]
    public class SlotBackgroundGroup
    {
        [Header("空格子")]
        [InspectorLabel("默认背景")]
        [Tooltip("未放置物品时使用的默认格子背景")]
        public Sprite defaultBackground;

        [Header("按品质")]
        [InspectorLabel("普通背景")] public Sprite commonBackground;
        [InspectorLabel("精良背景")] public Sprite rareBackground;
        [InspectorLabel("史诗背景")] public Sprite epicBackground;
        [InspectorLabel("传说背景")] public Sprite legendaryBackground;

        /// <summary>根据品质取背景图；null 表示空格子用默认。</summary>
        public Sprite GetSprite(WeaponRarity? rarity)
        {
            if (rarity == null) return defaultBackground;
            switch (rarity.Value)
            {
                case WeaponRarity.Common:    return commonBackground != null ? commonBackground : defaultBackground;
                case WeaponRarity.Rare:      return rareBackground != null ? rareBackground : defaultBackground;
                case WeaponRarity.Epic:      return epicBackground != null ? epicBackground : defaultBackground;
                case WeaponRarity.Legendary: return legendaryBackground != null ? legendaryBackground : defaultBackground;
                default:                     return defaultBackground;
            }
        }
    }

    /// <summary>
    /// 格子背景图配置：背包与商店各一组，每组包含空格子默认 + 四种品质背景图，使用时由 ConfigManager 按需加载。
    /// 放在 Resources/配置/ 下，命名为「格子背景配置」。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/格子背景配置", fileName = "格子背景配置")]
    public class SlotBackgroundConfigSO : ScriptableObject
    {
        [Header("背包格子")]
        [InspectorLabel("背包格子背景")]
        public SlotBackgroundGroup backpack = new SlotBackgroundGroup();

        [Header("商店格子")]
        [InspectorLabel("商店格子背景")]
        public SlotBackgroundGroup shop = new SlotBackgroundGroup();

        /// <summary>兼容旧调用：默认返回背包格子背景。</summary>
        public Sprite GetSprite(WeaponRarity? rarity)
        {
            return GetBackpackSprite(rarity);
        }

        public Sprite GetBackpackSprite(WeaponRarity? rarity)
        {
            return backpack != null ? backpack.GetSprite(rarity) : null;
        }

        public Sprite GetShopSprite(WeaponRarity? rarity)
        {
            if (shop == null)
                return GetBackpackSprite(rarity);

            Sprite sprite = shop.GetSprite(rarity);
            return sprite != null ? sprite : GetBackpackSprite(rarity);
        }
    }
}
