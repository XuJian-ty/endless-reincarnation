using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 武器类型；与 WeaponDatabaseSO 配合使用，旧版 WeaponSO 已合并入 Database。
    /// </summary>
    public enum WeaponType
    {
        MeleeSword,
        RangedGun
    }

    public enum WeaponRarity
    {
        Common    = 0,
        Rare      = 1,
        Epic      = 2,
        Legendary = 3
    }

    /// <summary>
    /// 单个品质下所有可能的属性范围（全 10 项 [min, max]）。
    /// 设计者只需填写该品质实际开放的项，未使用的保持 (0, 0) 即可。
    /// </summary>
    [Serializable]
    public struct WeaponStatRanges
    {
        [Header("基础 4 项（所有品质）")]
        [InspectorLabel("生命")] public FloatRange hp;
        [InspectorLabel("法力")] public FloatRange mp;
        [InspectorLabel("攻击")] public FloatRange attack;
        [InspectorLabel("防御")] public FloatRange defense;

        [Header("精良+ 2 项")]
        [InspectorLabel("生命回复")] public FloatRange hpRegen;
        [InspectorLabel("法力回复")] public FloatRange mpRegen;

        [Header("史诗+ 2 项")]
        [InspectorLabel("暴击率")]
        [Tooltip("暴击率小数，0.4～0.6 表示 40%～60%")]
        public FloatRange critRate;
        [InspectorLabel("暴击伤害")]
        [Tooltip("暴击伤害小数，0.8～1.2 表示 80%～120%")]
        public FloatRange critDmg;

        [Header("传说 2 项")]
        [InspectorLabel("攻速加成")]
        [Tooltip("攻速加成比例，0.2 表示 +20%")]
        public FloatRange attackSpeed;
        [InspectorLabel("移速加成")]
        [Tooltip("移速加成比例，0.2 表示 +20%")]
        public FloatRange moveSpeed;
    }
}
