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
        public static void GrantRewards(PlayerModel player, EnemyRuntimeStats enemyStats, Vector3 deathPosition, System.Random rng)
        {
            if (player == null)
                return;

            if (rng == null)
                rng = new System.Random();

            switch (enemyStats.type)
            {
                case EnemyType.Guardian:
                    GrantGuardianRewards(player, deathPosition, rng);
                    break;

                case EnemyType.Boss:
                    GrantBossRewards(player, deathPosition, rng);
                    break;

                default:
                    GrantNormalEnemyRewards(player, enemyStats, deathPosition, rng);
                    break;
            }
        }

        private static void GrantGuardianRewards(PlayerModel player, Vector3 deathPosition, System.Random rng)
        {
            player.AddItemCount(PlayerModel.ItemIds.TalentPoint, 1);
            TryDropLoot(player, deathPosition, rng, EnemyType.Guardian);
        }

        private static void GrantBossRewards(PlayerModel player, Vector3 deathPosition, System.Random rng)
        {
            TryDropLoot(player, deathPosition, rng, EnemyType.Boss);
        }

        private static void GrantNormalEnemyRewards(PlayerModel player, EnemyRuntimeStats enemyStats, Vector3 deathPosition, System.Random rng)
        {
            int levelsGained = player.AddExp(enemyStats.expReward);
            if (levelsGained > 0)
                EventCenter.GetInstance().EventTrigger(GameEvents.PlayerLevelUp, player.Exp.Level);

            int minGold = Mathf.Min(enemyStats.goldMin, enemyStats.goldMax);
            int maxGold = Mathf.Max(enemyStats.goldMin, enemyStats.goldMax);
            int gold = rng.Next(minGold, maxGold + 1);
            player.AddItemCount(PlayerModel.ItemIds.Gold, gold);

            if (enemyStats.type == EnemyType.Elite)
                TryDropLoot(player, deathPosition, rng, EnemyType.Elite);
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
            return dropId == PlayerModel.ItemIds.PotionHp
                   || dropId == PlayerModel.ItemIds.PotionMp
                   || dropId == PlayerModel.ItemIds.Nectar;
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
