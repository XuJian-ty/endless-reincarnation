using UnityEngine;
using Game.Domain;
using Game.Data;
using ProjectBase;

namespace Game.GameFlow
{
    /// <summary>
    /// Handles enemy death rewards: exp, gold, and elite loot.
    /// </summary>
    public static class EnemyDeathRewardSystem
    {
        public sealed class RewardProposal
        {
            public int exp;
            public int gold;
            public int talentPoints;
        }

        public static void GrantRewards(PlayerModel player, EnemyRuntimeStats enemyStats, Vector3 deathPosition, System.Random rng)
        {
            if (player == null)
                return;

            if (BattleMemoryRuntimeContext.IsActive)
                return;

            if (rng == null)
                rng = new System.Random();

            LevelConfigData levelConfig = ResolveCurrentLevelConfig();

            switch (enemyStats.type)
            {
                case EnemyType.Guardian:
                    GrantGuardianRewards(player, enemyStats, deathPosition, levelConfig, rng);
                    break;

                case EnemyType.Boss:
                    GrantBossRewards(player, enemyStats, deathPosition, levelConfig, rng);
                    break;

                default:
                    GrantNormalEnemyRewards(player, enemyStats, deathPosition, rng, levelConfig);
                    break;
            }

            EventCenter.GetInstance().EventTrigger(GameEvents.InventoryChanged);
        }

        public static RewardProposal BuildRewardProposal(EnemyRuntimeStats enemyStats, Vector3 deathPosition, System.Random rng, string enemyRuntimeId)
        {
            if (rng == null)
                rng = new System.Random();

            LevelConfigData levelConfig = ResolveCurrentLevelConfig();
            RewardProposal proposal = new RewardProposal
            {
                exp = ResolveExpReward(enemyStats, levelConfig),
                gold = ResolveGoldReward(enemyStats, levelConfig, rng),
                talentPoints = enemyStats.type == EnemyType.Guardian
                    ? Mathf.Max(0, levelConfig != null ? levelConfig.GetTalentPointReward(enemyStats.type) : 1)
                    : 0,
            };

            return proposal;
        }

        public static void GrantFixedRewards(PlayerModel player, RewardProposal proposal)
        {
            if (player == null || proposal == null)
                return;

            int levelsGained = player.AddExp(Mathf.Max(0, proposal.exp));
            if (levelsGained > 0)
                EventCenter.GetInstance().EventTrigger(GameEvents.PlayerLevelUp, player.Exp.Level);

            player.AddItemCount(PlayerModel.ItemIds.Gold, Mathf.Max(0, proposal.gold));
            player.AddItemCount(PlayerModel.ItemIds.TalentPoint, Mathf.Max(0, proposal.talentPoints));
            EventCenter.GetInstance().EventTrigger(GameEvents.InventoryChanged);
        }

        private static void GrantGuardianRewards(PlayerModel player, EnemyRuntimeStats enemyStats, Vector3 deathPosition, LevelConfigData levelConfig, System.Random rng)
        {
            GrantFixedRewards(player, enemyStats, levelConfig, rng);
            int talentPoints = levelConfig != null
                ? levelConfig.GetTalentPointReward(enemyStats.type)
                : 1;
            player.AddItemCount(PlayerModel.ItemIds.TalentPoint, Mathf.Max(0, talentPoints));
            TryDropLoot(player, deathPosition, rng, EnemyType.Guardian);
        }

        private static void GrantBossRewards(PlayerModel player, EnemyRuntimeStats enemyStats, Vector3 deathPosition, LevelConfigData levelConfig, System.Random rng)
        {
            GrantFixedRewards(player, enemyStats, levelConfig, rng);
            TryDropLoot(player, deathPosition, rng, EnemyType.Boss);
        }

        private static void GrantNormalEnemyRewards(PlayerModel player, EnemyRuntimeStats enemyStats, Vector3 deathPosition, System.Random rng, LevelConfigData levelConfig)
        {
            GrantFixedRewards(player, enemyStats, levelConfig, rng);

            if (enemyStats.type == EnemyType.Elite)
                TryDropLoot(player, deathPosition, rng, EnemyType.Elite);
        }

        private static void GrantFixedRewards(PlayerModel player, EnemyRuntimeStats enemyStats, LevelConfigData levelConfig, System.Random rng)
        {
            int expReward = ResolveExpReward(enemyStats, levelConfig);
            int levelsGained = player.AddExp(expReward);
            if (levelsGained > 0)
                EventCenter.GetInstance().EventTrigger(GameEvents.PlayerLevelUp, player.Exp.Level);

            int gold = ResolveGoldReward(enemyStats, levelConfig, rng);
            player.AddItemCount(PlayerModel.ItemIds.Gold, gold);
        }

        private static int ResolveGoldReward(EnemyRuntimeStats enemyStats, LevelConfigData levelConfig, System.Random rng)
        {
            if (levelConfig != null)
                return levelConfig.GetGoldReward(enemyStats.type);

            int minGold = Mathf.Min(enemyStats.goldMin, enemyStats.goldMax);
            int maxGold = Mathf.Max(enemyStats.goldMin, enemyStats.goldMax);
            return rng.Next(minGold, maxGold + 1);
        }

        private static int ResolveExpReward(EnemyRuntimeStats enemyStats, LevelConfigData levelConfig)
        {
            return levelConfig != null
                ? levelConfig.GetExpReward(enemyStats.type)
                : Mathf.Max(0, enemyStats.expReward);
        }

        private static void TryDropLoot(PlayerModel player, Vector3 position, System.Random rng, EnemyType enemyType)
        {
            var cfg = ConfigManager.GetInstance();
            var levelConfigDb = cfg?.GetLevelConfigDatabase();
            var weaponDb = cfg?.GetWeaponDatabase();
            if (levelConfigDb == null || weaponDb == null)
                return;

            int level = GetCurrentLevel();
            LevelConfigData levelConfig = levelConfigDb.GetConfigForLevel(level);
            if (levelConfig == null)
                return;

            string dropId = ResolveDropId(levelConfig, rng, enemyType);
            if (string.IsNullOrEmpty(dropId))
                return;

            if (TryGetWeaponRarity(dropId, out WeaponRarity rarity))
            {
                var instance = weaponDb.RollRandomWeapon(rarity, rng);
                if (instance == null)
                    return;

                DroppedPickupRuntime pickup = DroppedPickupRuntime.SpawnWeapon(instance, position, rng);
                if (pickup == null)
                    return;

                var entry = weaponDb.GetEntryByWeaponId(instance.weaponId);
                string weaponName = entry != null ? entry.displayName : instance.weaponId;
                Debug.Log($"[EnemyDeathRewardSystem] {GetEnemyTypeLabel(enemyType)}掉落武器: {weaponName} (品质: {instance.rarity})");
                return;
            }

            if (IsStackableDrop(dropId))
            {
                DroppedPickupRuntime pickup = DroppedPickupRuntime.SpawnStackable(dropId, 1, position, rng);
                if (pickup == null)
                    return;

                Debug.Log($"[EnemyDeathRewardSystem] {GetEnemyTypeLabel(enemyType)}掉落物品: {dropId}");
                return;
            }

            Debug.LogWarning($"[EnemyDeathRewardSystem] 未识别掉落配置，跳过{GetEnemyTypeLabel(enemyType)}掉落: {dropId} @ {position}");
        }

        private static int GetCurrentLevel()
        {
            var gsm = GameStateMachine.GetInstance();
            return gsm?.CurrentRun?.levelIndex ?? 1;
        }

        private static LevelConfigData ResolveCurrentLevelConfig()
        {
            var cfg = ConfigManager.GetInstance();
            var levelConfigDb = cfg?.GetLevelConfigDatabase();
            return levelConfigDb?.GetConfigForLevel(GetCurrentLevel());
        }

        private static bool TryGetWeaponRarity(string dropId, out WeaponRarity rarity)
        {
            switch (dropId)
            {
                case "weapon_common":
                    rarity = WeaponRarity.Common;
                    return true;
                case "weapon_rare":
                    rarity = WeaponRarity.Rare;
                    return true;
                case "weapon_epic":
                    rarity = WeaponRarity.Epic;
                    return true;
                case "weapon_legendary":
                    rarity = WeaponRarity.Legendary;
                    return true;
                default:
                    rarity = WeaponRarity.Common;
                    return false;
            }
        }

        private static bool IsStackableDrop(string dropId)
        {
            return !string.IsNullOrWhiteSpace(dropId)
                   && ConfigManager.GetInstance()?.GetItemDisplayDatabase()?.GetEntry(dropId.Trim()) != null;
        }

        private static string ResolveDropId(LevelConfigData levelConfig, System.Random rng, EnemyType enemyType)
        {
            if (levelConfig == null)
                return null;

            switch (enemyType)
            {
                case EnemyType.Guardian:
                    return levelConfig.RollGuardianDrop(rng);
                case EnemyType.Boss:
                    return levelConfig.RollBossDrop(rng);
                default:
                    return levelConfig.RollEliteDrop(rng);
            }
        }

        private static string GetEnemyTypeLabel(EnemyType enemyType)
        {
            switch (enemyType)
            {
                case EnemyType.Guardian:
                    return "守卫者";
                case EnemyType.Boss:
                    return "Boss";
                default:
                    return "精英";
            }
        }
    }
}
