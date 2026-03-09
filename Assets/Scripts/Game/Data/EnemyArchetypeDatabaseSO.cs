using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Enemy archetype database. All enemy behavior configs are stored in a single list.
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/敌人行为配置库", fileName = "敌人行为配置库")]
    public class EnemyArchetypeDatabaseSO : ScriptableObject
    {
        [InspectorLabel("敌人行为列表")]
        public List<EnemyArchetypeSO> entries = new List<EnemyArchetypeSO>();

        public EnemyArchetypeSO Get(EnemyType type)
        {
            if (entries == null) return null;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.enemyType == type)
                    return entry;
            }

            return null;
        }

        public int CountByType(EnemyType type)
        {
            if (entries == null) return 0;

            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.enemyType == type)
                    count++;
            }

            return count;
        }

        public EnemyArchetypeSO Get(string enemyId)
        {
            if (entries == null || string.IsNullOrWhiteSpace(enemyId))
                return null;

            string normalizedId = NormalizeId(enemyId);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && NormalizeId(entry.GetResolvedEnemyId()) == normalizedId)
                    return entry;
            }

            return null;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}
