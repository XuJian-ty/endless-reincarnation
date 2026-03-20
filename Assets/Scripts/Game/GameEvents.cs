namespace Game
{
    /// <summary>
    /// 游戏内事件名常量，替代魔法字符串，便于重构与避免拼写错误。
    /// 使用方式：EventCenter.GetInstance().EventTrigger(GameEvents.EnemyDied, data);
    /// </summary>
    public static class GameEvents
    {
        // ── 战斗与关卡 ────────────────────────────────────────────────────
        /// <summary>怪物死亡；参数可传 (string enemyId, Vector3 position, int enemyType) 或自定义 DTO</summary>
        public const string EnemyDied = "EnemyDied";

        /// <summary>守卫者死亡；用于 Boss 降临计数</summary>
        public const string GuardianDied = "GuardianDied";

        /// <summary>Boss 被击败；参数传 bossId，流程层写 Checkpoint 并弹选关</summary>
        public const string BossDefeated = "BossDefeated";

        // ── 玩家与成长 ───────────────────────────────────────────────────
        /// <summary>玩家升级；参数可传 (int newLevel)</summary>
        public const string PlayerLevelUp = "PlayerLevelUp";

        /// <summary>玩家死亡；流程层已通过 PlayerController.OnPlayerDied 订阅，此处供 UI 等复用</summary>
        public const string PlayerDied = "PlayerDied";

        /// <summary>战斗数字显示请求；参数传 CombatNumberRequest。</summary>
        public const string CombatNumberRequested = "CombatNumberRequested";

        // ── 拾取与背包 ───────────────────────────────────────────────────
        /// <summary>拾取物品；参数可传 itemId 或 WeaponInstance 等</summary>
        public const string ItemPickedUp = "ItemPickedUp";

        /// <summary>背包变更；HUD 等可订阅刷新</summary>
        public const string InventoryChanged = "InventoryChanged";

        // ── 场景加载 ────────────────────────────────────────────────────
        /// <summary>场景异步加载进度；参数 float 0～1，由 ScenesMgr 在 LoadSceneAsyn 中触发</summary>
        public const string SceneLoadProgress = "进度条更新";
    }
}
