using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [System.Serializable]
    public class SkillEffectVariantGroupDefinition
    {
        [HideInInspector]
        public string groupName = "";

        [InspectorLabel("技能效果列表")]
        public List<SharedSkillDefinition> entries = new List<SharedSkillDefinition>();

        public string GetResolvedGroupName()
        {
            if (!string.IsNullOrWhiteSpace(groupName))
                return groupName.Trim();

            if (entries == null)
                return string.Empty;

            for (int i = 0; i < entries.Count; i++)
            {
                SharedSkillDefinition entry = entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.skillId))
                    return entry.skillId.Trim();
            }

            return string.Empty;
        }
    }

    [System.Serializable]
    public class SkillGroupDefinition
    {
        [InspectorLabel("分组ID")]
        public string groupId = "default";

        [InspectorLabel("分组名称")]
        public string groupName = "默认分组";

        [InspectorLabel("技能小分组")]
        public List<SkillEffectVariantGroupDefinition> skillGroups = new List<SkillEffectVariantGroupDefinition>();

        [HideInInspector]
        public List<SharedSkillDefinition> entries = new List<SharedSkillDefinition>();
    }

    /// <summary>
    /// 玩家与敌人共用的技能效果库。技能本体时间轴统一定义在这里，
    /// 玩家与敌人分别通过各自的配置层引用 skillId。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/技能效果库", fileName = "技能效果库")]
    public class SkillEffectDatabaseSO : ScriptableObject
    {
        [InspectorLabel("技能分组")]
        public List<SkillGroupDefinition> groups = new List<SkillGroupDefinition>();

        [HideInInspector]
        public List<SharedSkillDefinition> entries = new List<SharedSkillDefinition>();

        private void OnEnable()
        {
            Synchronize();
        }

        public SharedSkillDefinition GetEntry(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
                return null;

            Synchronize();

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

        public List<SkillEffectVariantGroupDefinition> GetVariantGroupsInGroup(int groupIndex)
        {
            Synchronize();
            if (groups != null && groupIndex >= 0 && groupIndex < groups.Count)
                return groups[groupIndex]?.skillGroups;
            return null;
        }

        public List<SharedSkillDefinition> GetEntriesInVariantGroup(int groupIndex, int variantGroupIndex)
        {
            List<SkillEffectVariantGroupDefinition> variantGroups = GetVariantGroupsInGroup(groupIndex);
            if (variantGroups != null && variantGroupIndex >= 0 && variantGroupIndex < variantGroups.Count)
                return variantGroups[variantGroupIndex]?.entries;
            return null;
        }

        public List<SharedSkillDefinition> GetPrimaryEntriesInGroup(int groupIndex)
        {
            Synchronize();

            var result = new List<SharedSkillDefinition>();
            List<SkillEffectVariantGroupDefinition> variantGroups = GetVariantGroupsInGroup(groupIndex);
            if (variantGroups == null)
                return result;

            for (int i = 0; i < variantGroups.Count; i++)
            {
                SharedSkillDefinition primaryEntry = GetPrimaryEntry(variantGroups[i]);
                if (primaryEntry != null)
                    result.Add(primaryEntry);
            }

            return result;
        }

        public List<SharedSkillDefinition> GetEntriesInGroup(int groupIndex)
        {
            Synchronize();
            if (groups != null && groupIndex >= 0 && groupIndex < groups.Count)
                return groups[groupIndex]?.entries;
            return entries;
        }

        public void Synchronize()
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
                    skillGroups = new List<SkillEffectVariantGroupDefinition>(),
                });
            }

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                SkillGroupDefinition group = groups[groupIndex];
                if (group == null)
                {
                    groups[groupIndex] = new SkillGroupDefinition();
                    group = groups[groupIndex];
                }

                NormalizeGroup(group);
            }

            RebuildFlatEntries();
        }

        public static SkillEffectVariantGroupDefinition FindVariantGroup(SkillGroupDefinition group, string skillId)
        {
            if (group?.skillGroups == null || string.IsNullOrWhiteSpace(skillId))
                return null;

            string normalizedSkillId = skillId.Trim();
            for (int groupIndex = 0; groupIndex < group.skillGroups.Count; groupIndex++)
            {
                SkillEffectVariantGroupDefinition variantGroup = group.skillGroups[groupIndex];
                if (variantGroup == null)
                    continue;

                if (string.Equals(GetVariantGroupName(variantGroup), normalizedSkillId, StringComparison.Ordinal))
                    return variantGroup;

                if (variantGroup.entries == null)
                    continue;

                for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
                {
                    SharedSkillDefinition entry = variantGroup.entries[entryIndex];
                    if (entry != null && string.Equals(entry.skillId, normalizedSkillId, StringComparison.Ordinal))
                        return variantGroup;
                }
            }

            return null;
        }

        public static SharedSkillDefinition GetPrimaryEntry(SkillEffectVariantGroupDefinition variantGroup)
        {
            if (variantGroup?.entries == null || variantGroup.entries.Count == 0)
                return null;
            return variantGroup.entries[0];
        }

        public static string GetVariantGroupName(SkillEffectVariantGroupDefinition variantGroup)
        {
            return variantGroup != null ? variantGroup.GetResolvedGroupName() : string.Empty;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Synchronize();
        }
#endif

        private static void NormalizeGroup(SkillGroupDefinition group)
        {
            if (group == null)
                return;

            group.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();
            group.entries ??= new List<SharedSkillDefinition>();

            if (group.skillGroups.Count == 0 && group.entries.Count > 0)
            {
                for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
                {
                    SharedSkillDefinition entry = group.entries[entryIndex];
                    if (entry == null)
                        continue;

                    group.skillGroups.Add(new SkillEffectVariantGroupDefinition
                    {
                        groupName = ResolveVariantGroupName(entry, entryIndex),
                        entries = new List<SharedSkillDefinition> { entry },
                    });
                }
            }

            for (int variantGroupIndex = group.skillGroups.Count - 1; variantGroupIndex >= 0; variantGroupIndex--)
            {
                SkillEffectVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
                if (variantGroup == null)
                {
                    group.skillGroups[variantGroupIndex] = new SkillEffectVariantGroupDefinition();
                    variantGroup = group.skillGroups[variantGroupIndex];
                }

                NormalizeVariantGroup(variantGroup, variantGroupIndex);
            }
        }

        private void RebuildFlatEntries()
        {
            entries ??= new List<SharedSkillDefinition>();
            entries.Clear();

            if (groups == null)
                return;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                SkillGroupDefinition group = groups[groupIndex];
                if (group == null)
                    continue;

                group.entries ??= new List<SharedSkillDefinition>();
                group.entries.Clear();
                if (group.skillGroups == null)
                    continue;

                for (int variantGroupIndex = 0; variantGroupIndex < group.skillGroups.Count; variantGroupIndex++)
                {
                    SkillEffectVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
                    if (variantGroup?.entries == null)
                        continue;

                    for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
                    {
                        SharedSkillDefinition entry = variantGroup.entries[entryIndex];
                        if (entry == null)
                            continue;

                        group.entries.Add(entry);
                        entries.Add(entry);
                    }
                }
            }
        }

        private static void NormalizeVariantGroup(SkillEffectVariantGroupDefinition variantGroup, int variantGroupIndex)
        {
            variantGroup.entries ??= new List<SharedSkillDefinition>();
            for (int entryIndex = variantGroup.entries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                if (variantGroup.entries[entryIndex] == null)
                    variantGroup.entries.RemoveAt(entryIndex);
            }

            string groupName = variantGroup.GetResolvedGroupName();
            if (string.IsNullOrWhiteSpace(groupName))
                groupName = $"skill_group_{variantGroupIndex}";

            if (variantGroup.entries.Count == 0)
                variantGroup.entries.Add(CreateDefaultEntry(groupName));

            SharedSkillDefinition primaryEntry = variantGroup.entries[0];
            if (primaryEntry == null)
            {
                primaryEntry = CreateDefaultEntry(groupName);
                variantGroup.entries[0] = primaryEntry;
            }

            if (string.IsNullOrWhiteSpace(primaryEntry.skillId))
                primaryEntry.skillId = groupName;

            groupName = primaryEntry.skillId.Trim();
            variantGroup.groupName = groupName;
            primaryEntry.skillId = groupName;

            for (int entryIndex = 1; entryIndex < variantGroup.entries.Count; entryIndex++)
            {
                SharedSkillDefinition entry = variantGroup.entries[entryIndex];
                if (entry == null)
                {
                    variantGroup.entries[entryIndex] = CreateDefaultEntry($"{groupName}_Variant{entryIndex}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.skillId))
                    entry.skillId = $"{groupName}_Variant{entryIndex}";
            }
        }

        private static string ResolveVariantGroupName(SharedSkillDefinition entry, int entryIndex)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.skillId))
                return entry.skillId.Trim();
            return $"skill_group_{entryIndex}";
        }

        private static SharedSkillDefinition CreateDefaultEntry(string skillId)
        {
            return new SharedSkillDefinition
            {
                skillId = skillId,
                damageEvents = new List<SkillDamageEvent>(),
                physicsEvents = new List<SkillPhysicsEvent>(),
                attributeEvents = new List<SkillAttributeEvent>(),
                vfxEvents = new List<SkillVfxEvent>(),
                sfxEvents = new List<SkillSfxEvent>(),
            };
        }
    }
}
