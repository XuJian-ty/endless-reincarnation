using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 玩家与敌人共享技能库。存储 <see cref="SharedSkillDefinition"/> 条目，
    /// 敌人通过 <see cref="EnemySkillSlotBinding.skillId"/> 引用，
    /// 玩家通过 <see cref="SkillConfigEntry.sharedSkillId"/> 引用。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/共享技能库", fileName = "共享技能库")]
    public class SharedSkillDatabaseSO : ScriptableObject
    {
        [InspectorLabel("技能列表")]
        public List<SharedSkillDefinition> entries = new List<SharedSkillDefinition>();

        public SharedSkillDefinition GetEntry(string skillId)
        {
            if (entries == null || string.IsNullOrEmpty(skillId)) return null;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.skillId == skillId)
                    return entry;
            }

            return null;
        }

        public bool Contains(string skillId) => GetEntry(skillId) != null;
    }
}
