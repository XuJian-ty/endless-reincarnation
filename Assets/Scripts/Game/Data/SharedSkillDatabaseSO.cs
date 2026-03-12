using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [System.Serializable]
    public class SkillGroupDefinition
    {
        [InspectorLabel("分组ID")]
        public string groupId = "default";

        [InspectorLabel("分组名称")]
        public string groupName = "默认分组";

        [InspectorLabel("技能列表")]
        public List<SharedSkillDefinition> entries = new List<SharedSkillDefinition>();
    }

    /// <summary>
    /// 玩家与敌人共用的技能库。技能本体时间轴统一定义在这里，
    /// 玩家与敌人分别通过各自的配置层引用 skillId。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/技能库", fileName = "技能库")]
    public class SharedSkillDatabaseSO : ScriptableObject
    {
        [InspectorLabel("技能分组")]
        public List<SkillGroupDefinition> groups = new List<SkillGroupDefinition>();

        [HideInInspector]
        public List<SharedSkillDefinition> entries = new List<SharedSkillDefinition>();

        private void OnEnable()
        {
            if (groups == null)
                groups = new List<SkillGroupDefinition>();

            if (groups.Count == 0 && entries != null && entries.Count > 0)
            {
                groups.Add(new SkillGroupDefinition
                {
                    groupId = "default",
                    groupName = "默认分组",
                    entries = entries,
                });
            }

            if (groups.Count > 0)
            {
                if (entries == null)
                    entries = new List<SharedSkillDefinition>();
                else
                    entries.Clear();

                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    var group = groups[groupIndex];
                    if (group?.entries == null)
                        continue;

                    for (int i = 0; i < group.entries.Count; i++)
                    {
                        var entry = group.entries[i];
                        if (entry != null)
                            entries.Add(entry);
                    }
                }
            }
        }

        public SharedSkillDefinition GetEntry(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
                return null;

            if (groups != null)
            {
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    var group = groups[groupIndex];
                    if (group?.entries == null)
                        continue;

                    for (int i = 0; i < group.entries.Count; i++)
                    {
                        var entry = group.entries[i];
                        if (entry != null && entry.skillId == skillId)
                            return entry;
                    }
                }
            }

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry != null && entry.skillId == skillId)
                        return entry;
                }
            }

            return null;
        }

        public bool Contains(string skillId)
        {
            return GetEntry(skillId) != null;
        }

        public List<SharedSkillDefinition> GetEntriesInGroup(int groupIndex)
        {
            if (groups != null && groupIndex >= 0 && groupIndex < groups.Count)
                return groups[groupIndex]?.entries;
            return entries;
        }
    }
}
