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
                    GrantGuardianRewards(player);
                    break;

                case EnemyType.Boss:
                    // Boss reward flow is handled elsewhere.
                    break;

                default:
                    GrantNormalEnemyRewards(player, enemyStats, deathPosition, rng);
                    break;
            }
        }

        private static void GrantGuardianRewards(PlayerModel player)
        {
            player.AddItemCount(PlayerModel.ItemIds.TalentPoint, 1);
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
                TryDropLoot(player, deathPosition, rng);
        }

        private static void TryDropLoot(PlayerModel player, Vector3 position, System.Random rng)
        {
            var cfg = ConfigManager.GetInstance();
            var dropDb = cfg?.GetDropTableDatabase();
            var weaponDb = cfg?.GetWeaponDatabase();
            if (dropDb == null || weaponDb == null)
                return;

            int level = GetCurrentLevel();
            string dropId = dropDb.Roll(level, rng);
            if (string.IsNullOrEmpty(dropId))
                return;

            if (TryGetWeaponRarity(dropId, out WeaponRarity rarity))
            {
                if (!player.CanAddWeapon())
                    return;

                var instance = weaponDb.RollRandomWeapon(rarity, rng);
                if (instance == null)
                    return;

                if (!player.AddWeapon(instance))
                    return;

                var entry = weaponDb.GetEntryByWeaponId(instance.weaponId);
                string weaponName = entry != null ? entry.displayName : instance.weaponId;
                Debug.Log($"[EnemyDeathRewardSystem] 掉落武器: {weaponName} (品质: {instance.rarity})");
                return;
            }

            if (IsStackableDrop(dropId) && player.CanAddStackable(dropId, 1))
            {
                player.AddItemCount(dropId, 1);
                Debug.Log($"[EnemyDeathRewardSystem] 掉落物品: {dropId}");
                return;
            }

            Debug.LogWarning($"[EnemyDeathRewardSystem] 未识别或背包已满，跳过掉落: {dropId} @ {position}");
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
    }
}
