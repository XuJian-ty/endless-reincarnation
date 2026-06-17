using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public class CharacterAnimationEntry
    {
        [InspectorLabel("动画ID")]
        public string animationId = "";

        [InspectorLabel("动画片段")]
        public AnimationClip clip;
    }

    [Serializable]
    public class CharacterAnimationVariantGroupDefinition
    {
        [HideInInspector]
        public string groupName = "";

        [InspectorLabel("动画列表")]
        public List<CharacterAnimationEntry> entries = new List<CharacterAnimationEntry>();

        public string GetResolvedGroupName()
        {
            if (!string.IsNullOrWhiteSpace(groupName))
                return groupName.Trim();

            if (entries == null)
                return string.Empty;

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterAnimationEntry entry = entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.animationId))
                    return entry.animationId.Trim();
            }

            return string.Empty;
        }
    }

    [Serializable]
    public class CharacterAnimationGroupDefinition
    {
        [InspectorLabel("分组ID")]
        public string groupId = "default";

        [InspectorLabel("分组名称")]
        public string groupName = "默认分组";

        [InspectorLabel("技能小分组")]
        public List<CharacterAnimationVariantGroupDefinition> skillGroups = new List<CharacterAnimationVariantGroupDefinition>();

        [HideInInspector]
        public List<CharacterAnimationEntry> entries = new List<CharacterAnimationEntry>();
    }

    [CreateAssetMenu(menuName = "游戏/配置/动画库", fileName = "动画库")]
    public class CharacterAnimationLibrarySO : ScriptableObject
    {
        [InspectorLabel("动画分组")]
        public List<CharacterAnimationGroupDefinition> groups = new List<CharacterAnimationGroupDefinition>();

        [HideInInspector]
        public List<CharacterAnimationEntry> entries = new List<CharacterAnimationEntry>();

        private void OnEnable()
        {
            Synchronize();
        }

        public CharacterAnimationEntry GetEntry(string animationId)
        {
            if (string.IsNullOrWhiteSpace(animationId))
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
                        if (entry != null && entry.animationId == animationId)
                            return entry;
                    }
                }
            }

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    CharacterAnimationEntry entry = entries[i];
                    if (entry != null && entry.animationId == animationId)
                        return entry;
                }
            }

            return null;
        }

        public List<CharacterAnimationVariantGroupDefinition> GetVariantGroupsInGroup(int groupIndex)
        {
            Synchronize();
            if (groups != null && groupIndex >= 0 && groupIndex < groups.Count)
                return groups[groupIndex]?.skillGroups;
            return null;
        }

        public List<CharacterAnimationEntry> GetEntriesInVariantGroup(int groupIndex, int variantGroupIndex)
        {
            List<CharacterAnimationVariantGroupDefinition> variantGroups = GetVariantGroupsInGroup(groupIndex);
            if (variantGroups != null && variantGroupIndex >= 0 && variantGroupIndex < variantGroups.Count)
                return variantGroups[variantGroupIndex]?.entries;
            return null;
        }

        public List<CharacterAnimationEntry> GetPrimaryEntriesInGroup(int groupIndex)
        {
            Synchronize();

            List<CharacterAnimationEntry> result = new List<CharacterAnimationEntry>();
            List<CharacterAnimationVariantGroupDefinition> variantGroups = GetVariantGroupsInGroup(groupIndex);
            if (variantGroups == null)
                return result;

            for (int i = 0; i < variantGroups.Count; i++)
            {
                CharacterAnimationEntry primaryEntry = GetPrimaryEntry(variantGroups[i]);
                if (primaryEntry != null)
                    result.Add(primaryEntry);
            }

            return result;
        }

        public List<CharacterAnimationEntry> GetEntriesInGroup(int groupIndex)
        {
            Synchronize();
            if (groups != null && groupIndex >= 0 && groupIndex < groups.Count)
                return groups[groupIndex]?.entries;
            return entries;
        }

        public void Synchronize()
        {
            if (groups == null)
                groups = new List<CharacterAnimationGroupDefinition>();

            if (groups.Count == 0 && entries != null && entries.Count > 0)
            {
                List<CharacterAnimationEntry> legacyEntries = new List<CharacterAnimationEntry>(entries);
                groups.Add(new CharacterAnimationGroupDefinition
                {
                    groupId = "default",
                    groupName = "默认分组",
                    entries = legacyEntries,
                    skillGroups = new List<CharacterAnimationVariantGroupDefinition>(),
                });
            }

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                CharacterAnimationGroupDefinition group = groups[groupIndex];
                if (group == null)
                {
                    groups[groupIndex] = new CharacterAnimationGroupDefinition();
                    group = groups[groupIndex];
                }

                NormalizeGroup(group);
            }

            RebuildFlatEntries();
        }

        public static CharacterAnimationVariantGroupDefinition FindVariantGroup(CharacterAnimationGroupDefinition group, string animationId)
        {
            if (group?.skillGroups == null || string.IsNullOrWhiteSpace(animationId))
                return null;

            string normalizedAnimationId = animationId.Trim();
            for (int groupIndex = 0; groupIndex < group.skillGroups.Count; groupIndex++)
            {
                CharacterAnimationVariantGroupDefinition variantGroup = group.skillGroups[groupIndex];
                if (variantGroup == null)
                    continue;

                if (string.Equals(GetVariantGroupName(variantGroup), normalizedAnimationId, StringComparison.Ordinal))
                    return variantGroup;

                if (variantGroup.entries == null)
                    continue;

                for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
                {
                    CharacterAnimationEntry entry = variantGroup.entries[entryIndex];
                    if (entry != null && string.Equals(entry.animationId, normalizedAnimationId, StringComparison.Ordinal))
                        return variantGroup;
                }
            }

            return null;
        }

        public static CharacterAnimationEntry GetPrimaryEntry(CharacterAnimationVariantGroupDefinition variantGroup)
        {
            if (variantGroup?.entries == null || variantGroup.entries.Count == 0)
                return null;
            return variantGroup.entries[0];
        }

        public static string GetVariantGroupName(CharacterAnimationVariantGroupDefinition variantGroup)
        {
            return variantGroup != null ? variantGroup.GetResolvedGroupName() : string.Empty;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Synchronize();
        }
#endif

        private static void NormalizeGroup(CharacterAnimationGroupDefinition group)
        {
            if (group == null)
                return;

            group.skillGroups ??= new List<CharacterAnimationVariantGroupDefinition>();
            group.entries ??= new List<CharacterAnimationEntry>();

            if (group.skillGroups.Count == 0 && group.entries.Count > 0)
            {
                for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
                {
                    CharacterAnimationEntry entry = group.entries[entryIndex];
                    if (entry == null)
                        continue;

                    group.skillGroups.Add(new CharacterAnimationVariantGroupDefinition
                    {
                        groupName = ResolveVariantGroupName(entry, entryIndex),
                        entries = new List<CharacterAnimationEntry> { entry },
                    });
                }
            }

            for (int variantGroupIndex = group.skillGroups.Count - 1; variantGroupIndex >= 0; variantGroupIndex--)
            {
                CharacterAnimationVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
                if (variantGroup == null)
                {
                    group.skillGroups[variantGroupIndex] = new CharacterAnimationVariantGroupDefinition();
                    variantGroup = group.skillGroups[variantGroupIndex];
                }

                NormalizeVariantGroup(variantGroup, variantGroupIndex);
            }
        }

        private void RebuildFlatEntries()
        {
            entries ??= new List<CharacterAnimationEntry>();
            entries.Clear();

            if (groups == null)
                return;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                CharacterAnimationGroupDefinition group = groups[groupIndex];
                if (group == null)
                    continue;

                group.entries ??= new List<CharacterAnimationEntry>();
                group.entries.Clear();
                if (group.skillGroups == null)
                    continue;

                for (int variantGroupIndex = 0; variantGroupIndex < group.skillGroups.Count; variantGroupIndex++)
                {
                    CharacterAnimationVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
                    if (variantGroup?.entries == null)
                        continue;

                    for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
                    {
                        CharacterAnimationEntry entry = variantGroup.entries[entryIndex];
                        if (entry == null)
                            continue;

                        group.entries.Add(entry);
                        entries.Add(entry);
                    }
                }
            }
        }

        private static void NormalizeVariantGroup(CharacterAnimationVariantGroupDefinition variantGroup, int variantGroupIndex)
        {
            variantGroup.entries ??= new List<CharacterAnimationEntry>();
            for (int entryIndex = variantGroup.entries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                if (variantGroup.entries[entryIndex] == null)
                    variantGroup.entries.RemoveAt(entryIndex);
            }

            string groupName = variantGroup.GetResolvedGroupName();
            if (string.IsNullOrWhiteSpace(groupName))
                groupName = $"animation_group_{variantGroupIndex}";

            if (variantGroup.entries.Count == 0)
                variantGroup.entries.Add(CreateDefaultEntry(groupName));

            CharacterAnimationEntry primaryEntry = variantGroup.entries[0];
            if (primaryEntry == null)
            {
                primaryEntry = CreateDefaultEntry(groupName);
                variantGroup.entries[0] = primaryEntry;
            }

            if (string.IsNullOrWhiteSpace(primaryEntry.animationId))
                primaryEntry.animationId = groupName;

            groupName = primaryEntry.animationId.Trim();
            variantGroup.groupName = groupName;
            primaryEntry.animationId = groupName;

            for (int entryIndex = 1; entryIndex < variantGroup.entries.Count; entryIndex++)
            {
                CharacterAnimationEntry entry = variantGroup.entries[entryIndex];
                if (entry == null)
                {
                    variantGroup.entries[entryIndex] = CreateDefaultEntry($"{groupName}_Variant{entryIndex}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.animationId))
                    entry.animationId = $"{groupName}_Variant{entryIndex}";
            }
        }

        private static string ResolveVariantGroupName(CharacterAnimationEntry entry, int entryIndex)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.animationId))
                return entry.animationId.Trim();
            return $"animation_group_{entryIndex}";
        }

        private static CharacterAnimationEntry CreateDefaultEntry(string animationId)
        {
            return new CharacterAnimationEntry
            {
                animationId = animationId,
                clip = null,
            };
        }
    }
}
