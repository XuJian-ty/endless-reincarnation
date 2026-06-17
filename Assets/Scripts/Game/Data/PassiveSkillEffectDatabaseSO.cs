using System;
using System.Collections.Generic;
using Game.Domain;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public class PassiveSkillEffectDefinition
    {
        [InspectorLabel("技能ID")]
        public string skillId = "";

        [InspectorLabel("显示名称")]
        [Tooltip("用于技能树变异面板展示该被动效果的名称。留空时回退为技能ID。")]
        public string displayName = "";

        [InspectorLabel("技能效果描述")]
        [TextArea(2, 5)]
        public string effectDescription = "";

        [InspectorLabel("变异天赋点消耗")]
        [Min(0)]
        public int mutationTalentCost = 0;

        [InspectorLabel("属性加成")]
        public StatModifier statModifier = new StatModifier();

        public string GetResolvedDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            return !string.IsNullOrWhiteSpace(skillId) ? skillId.Trim() : string.Empty;
        }
    }

    [Serializable]
    public class PassiveSkillEffectVariantGroupDefinition
    {
        [HideInInspector]
        public string groupName = "";

        [InspectorLabel("被动效果列表")]
        public List<PassiveSkillEffectDefinition> entries = new List<PassiveSkillEffectDefinition>();

        public string GetResolvedGroupName()
        {
            if (!string.IsNullOrWhiteSpace(groupName))
                return groupName.Trim();

            if (entries == null)
                return string.Empty;

            for (int i = 0; i < entries.Count; i++)
            {
                PassiveSkillEffectDefinition entry = entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.skillId))
                    return entry.skillId.Trim();
            }

            return string.Empty;
        }
    }

    [Serializable]
    public class PassiveSkillEffectGroupDefinition
    {
        [InspectorLabel("分组ID")]
        public string groupId = "default";

        [InspectorLabel("分组名称")]
        public string groupName = "默认分组";

        [InspectorLabel("技能小分组")]
        public List<PassiveSkillEffectVariantGroupDefinition> skillGroups = new List<PassiveSkillEffectVariantGroupDefinition>();

        [NonSerialized, HideInInspector]
        public List<PassiveSkillEffectDefinition> entries = new List<PassiveSkillEffectDefinition>();
    }

    /// <summary>
    /// 玩家被动技能效果库。按 skillId 映射属性加成，并支持在同一小分组下维护多个技能ID变体。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/被动技能效果库", fileName = "被动技能效果库")]
    public class PassiveSkillEffectDatabaseSO : ScriptableObject
    {
        [InspectorLabel("被动技能分组")]
        public List<PassiveSkillEffectGroupDefinition> groups = new List<PassiveSkillEffectGroupDefinition>();

        [NonSerialized, HideInInspector]
        public List<PassiveSkillEffectDefinition> entries = new List<PassiveSkillEffectDefinition>();

        private void OnEnable()
        {
            Synchronize();
        }

        public PassiveSkillEffectDefinition GetEntry(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return null;

            Synchronize();

            string normalizedSkillId = skillId.Trim();
            if (groups != null)
            {
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    PassiveSkillEffectGroupDefinition group = groups[groupIndex];
                    if (group?.entries == null)
                        continue;

                    for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
                    {
                        PassiveSkillEffectDefinition entry = group.entries[entryIndex];
                        if (entry != null && string.Equals(entry.skillId, normalizedSkillId, StringComparison.Ordinal))
                            return entry;
                    }
                }
            }

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    PassiveSkillEffectDefinition entry = entries[i];
                    if (entry != null && string.Equals(entry.skillId, normalizedSkillId, StringComparison.Ordinal))
                        return entry;
                }
            }

            return null;
        }

        public bool Contains(string skillId)
        {
            return GetEntry(skillId) != null;
        }

        public List<PassiveSkillEffectVariantGroupDefinition> GetVariantGroupsInGroup(int groupIndex)
        {
            Synchronize();
            if (groups != null && groupIndex >= 0 && groupIndex < groups.Count)
                return groups[groupIndex]?.skillGroups;
            return null;
        }

        public void Synchronize()
        {
            if (groups == null)
                groups = new List<PassiveSkillEffectGroupDefinition>();

            if (groups.Count == 0 && entries != null && entries.Count > 0)
            {
                groups.Add(new PassiveSkillEffectGroupDefinition
                {
                    groupId = "default",
                    groupName = "默认分组",
                    entries = entries,
                    skillGroups = new List<PassiveSkillEffectVariantGroupDefinition>(),
                });
            }

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                PassiveSkillEffectGroupDefinition group = groups[groupIndex];
                if (group == null)
                {
                    groups[groupIndex] = new PassiveSkillEffectGroupDefinition();
                    group = groups[groupIndex];
                }

                NormalizeGroup(group);
            }

            RebuildFlatEntries();
        }

        public static PassiveSkillEffectVariantGroupDefinition FindVariantGroup(PassiveSkillEffectGroupDefinition group, string skillId)
        {
            if (group?.skillGroups == null || string.IsNullOrWhiteSpace(skillId))
                return null;

            string normalizedSkillId = skillId.Trim();
            for (int groupIndex = 0; groupIndex < group.skillGroups.Count; groupIndex++)
            {
                PassiveSkillEffectVariantGroupDefinition variantGroup = group.skillGroups[groupIndex];
                if (variantGroup == null)
                    continue;

                if (string.Equals(GetVariantGroupName(variantGroup), normalizedSkillId, StringComparison.Ordinal))
                    return variantGroup;

                if (variantGroup.entries == null)
                    continue;

                for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
                {
                    PassiveSkillEffectDefinition entry = variantGroup.entries[entryIndex];
                    if (entry != null && string.Equals(entry.skillId, normalizedSkillId, StringComparison.Ordinal))
                        return variantGroup;
                }
            }

            return null;
        }

        public static PassiveSkillEffectDefinition GetPrimaryEntry(PassiveSkillEffectVariantGroupDefinition variantGroup)
        {
            if (variantGroup?.entries == null || variantGroup.entries.Count == 0)
                return null;
            return variantGroup.entries[0];
        }

        public static string GetVariantGroupName(PassiveSkillEffectVariantGroupDefinition variantGroup)
        {
            return variantGroup != null ? variantGroup.GetResolvedGroupName() : string.Empty;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Synchronize();
        }
#endif

        private static void NormalizeGroup(PassiveSkillEffectGroupDefinition group)
        {
            if (group == null)
                return;

            group.skillGroups ??= new List<PassiveSkillEffectVariantGroupDefinition>();
            group.entries ??= new List<PassiveSkillEffectDefinition>();

            for (int entryIndex = group.entries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                PassiveSkillEffectDefinition entry = group.entries[entryIndex];
                if (entry == null)
                    group.entries.RemoveAt(entryIndex);
            }

            if (group.skillGroups.Count == 0 && group.entries.Count > 0)
            {
                for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
                {
                    PassiveSkillEffectDefinition entry = group.entries[entryIndex];
                    if (entry == null)
                        continue;

                    group.skillGroups.Add(new PassiveSkillEffectVariantGroupDefinition
                    {
                        groupName = ResolveVariantGroupName(entry, entryIndex),
                        entries = new List<PassiveSkillEffectDefinition> { entry },
                    });
                }
            }

            for (int variantGroupIndex = group.skillGroups.Count - 1; variantGroupIndex >= 0; variantGroupIndex--)
            {
                PassiveSkillEffectVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
                if (variantGroup == null)
                {
                    group.skillGroups[variantGroupIndex] = new PassiveSkillEffectVariantGroupDefinition();
                    variantGroup = group.skillGroups[variantGroupIndex];
                }

                NormalizeVariantGroup(variantGroup, variantGroupIndex);
            }
        }

        private void RebuildFlatEntries()
        {
            entries ??= new List<PassiveSkillEffectDefinition>();
            entries.Clear();

            if (groups == null)
                return;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                PassiveSkillEffectGroupDefinition group = groups[groupIndex];
                if (group == null)
                    continue;

                group.entries ??= new List<PassiveSkillEffectDefinition>();
                group.entries.Clear();
                if (group.skillGroups == null)
                    continue;

                for (int variantGroupIndex = 0; variantGroupIndex < group.skillGroups.Count; variantGroupIndex++)
                {
                    PassiveSkillEffectVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
                    if (variantGroup?.entries == null)
                        continue;

                    for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
                    {
                        PassiveSkillEffectDefinition entry = variantGroup.entries[entryIndex];
                        if (entry == null)
                            continue;

                        group.entries.Add(entry);
                        entries.Add(entry);
                    }
                }
            }
        }

        private static void NormalizeVariantGroup(PassiveSkillEffectVariantGroupDefinition variantGroup, int variantGroupIndex)
        {
            variantGroup.entries ??= new List<PassiveSkillEffectDefinition>();
            for (int entryIndex = variantGroup.entries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                if (variantGroup.entries[entryIndex] == null)
                    variantGroup.entries.RemoveAt(entryIndex);
            }

            string groupName = variantGroup.GetResolvedGroupName();
            if (string.IsNullOrWhiteSpace(groupName))
                groupName = $"passive_skill_group_{variantGroupIndex}";

            if (variantGroup.entries.Count == 0)
                variantGroup.entries.Add(CreateDefaultEntry(groupName));

            PassiveSkillEffectDefinition primaryEntry = variantGroup.entries[0];
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
                PassiveSkillEffectDefinition entry = variantGroup.entries[entryIndex];
                if (entry == null)
                {
                    variantGroup.entries[entryIndex] = CreateDefaultEntry($"{groupName}_Variant{entryIndex}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.skillId))
                    entry.skillId = $"{groupName}_Variant{entryIndex}";
            }
        }

        private static string ResolveVariantGroupName(PassiveSkillEffectDefinition entry, int entryIndex)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.skillId))
                return entry.skillId.Trim();
            return $"passive_skill_group_{entryIndex}";
        }

        private static PassiveSkillEffectDefinition CreateDefaultEntry(string skillId)
        {
            return new PassiveSkillEffectDefinition
            {
                skillId = skillId,
                statModifier = new StatModifier(),
            };
        }
    }
}
