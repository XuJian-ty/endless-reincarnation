using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomEditor(typeof(PassiveSkillEffectDatabaseSO))]
    public sealed class PassiveSkillEffectDatabaseSOEditor : UnityEditor.Editor
    {
        private const string DefaultPassiveSkillEffectDatabasePath = "Assets/Resources/配置/被动技能效果库.asset";
        private const string DefaultSkillConfigDatabasePath = "Assets/Resources/配置/玩家动作及技能配置库.asset";
        private const string PlayerPassiveGroupId = "player_passive";
        private const string PlayerPassiveGroupName = "玩家被动技能";

        private SerializedProperty _groupsProperty;

        private void OnEnable()
        {
            PassiveSkillEffectDatabaseSO database = target as PassiveSkillEffectDatabaseSO;
            database?.Synchronize();
            EnsurePassiveSkillGroups(LoadDefaultSkillConfigDatabase(), database, false);
            _groupsProperty = serializedObject.FindProperty("groups");
        }

        public override void OnInspectorGUI()
        {
            PassiveSkillEffectDatabaseSO database = (PassiveSkillEffectDatabaseSO)target;
            serializedObject.Update();

            if (_groupsProperty == null)
            {
                EditorGUILayout.HelpBox("未找到被动技能分组数据。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("被动技能小分组由“玩家动作及技能配置库”新增被动技能时自动同步创建。这里仅维护每个小分组下的被动技能效果变体。", MessageType.Info);
                DrawGroups(database, _groupsProperty);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                database?.Synchronize();
                if (database != null)
                    EditorUtility.SetDirty(database);
            }
        }

        public static void EnsurePassiveSkillGroups(SkillConfigDatabaseSO skillConfigDatabase)
        {
            EnsurePassiveSkillGroups(skillConfigDatabase, LoadDefaultPassiveSkillEffectDatabase(), true);
        }

        public static void EnsurePassiveSkillGroups(SkillConfigDatabaseSO skillConfigDatabase, PassiveSkillEffectDatabaseSO database)
        {
            EnsurePassiveSkillGroups(skillConfigDatabase, database, false);
        }

        private static void EnsurePassiveSkillGroups(SkillConfigDatabaseSO skillConfigDatabase, PassiveSkillEffectDatabaseSO database, bool saveAsset)
        {
            if (skillConfigDatabase == null || database == null)
                return;

            database.Synchronize();
            bool changed = false;
            PassiveSkillEffectGroupDefinition group = FindOrCreatePlayerPassiveGroup(database, ref changed);
            List<string> passiveSkillIds = CollectPassiveSkillIds(skillConfigDatabase);
            for (int i = 0; i < passiveSkillIds.Count; i++)
            {
                changed |= EnsureVariantGroup(group, passiveSkillIds[i]);
            }

            changed |= ReorderVariantGroups(group, passiveSkillIds);

            if (!changed)
                return;

            database.Synchronize();
            EditorUtility.SetDirty(database);
            if (saveAsset)
                AssetDatabase.SaveAssetIfDirty(database);
        }

        private static void DrawGroups(PassiveSkillEffectDatabaseSO database, SerializedProperty groupsProperty)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("分组数量", groupsProperty.arraySize);

            for (int groupIndex = 0; groupIndex < groupsProperty.arraySize; groupIndex++)
            {
                SerializedProperty groupProperty = groupsProperty.GetArrayElementAtIndex(groupIndex);
                if (groupProperty == null)
                    continue;

                SerializedProperty groupIdProperty = groupProperty.FindPropertyRelative("groupId");
                SerializedProperty groupNameProperty = groupProperty.FindPropertyRelative("groupName");
                SerializedProperty skillGroupsProperty = groupProperty.FindPropertyRelative("skillGroups");

                string groupLabel = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                    ? groupNameProperty.stringValue
                    : $"分组 {groupIndex + 1}";

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    groupProperty.isExpanded = EditorGUILayout.Foldout(groupProperty.isExpanded, groupLabel, true);
                    if (!groupProperty.isExpanded)
                        continue;

                    EditorGUILayout.PropertyField(groupIdProperty);
                    EditorGUILayout.PropertyField(groupNameProperty);
                    if (skillGroupsProperty != null)
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.IntField("技能小分组数量", skillGroupsProperty.arraySize);
                    }

                    EditorGUILayout.Space(4f);
                    DrawVariantGroups(database, groupIndex, skillGroupsProperty);
                }
            }
        }

        private static void DrawVariantGroups(PassiveSkillEffectDatabaseSO database, int groupIndex, SerializedProperty skillGroupsProperty)
        {
            if (skillGroupsProperty == null)
                return;

            for (int variantGroupIndex = 0; variantGroupIndex < skillGroupsProperty.arraySize; variantGroupIndex++)
            {
                SerializedProperty variantGroupProperty = skillGroupsProperty.GetArrayElementAtIndex(variantGroupIndex);
                if (variantGroupProperty == null)
                    continue;

                SerializedProperty groupNameProperty = variantGroupProperty.FindPropertyRelative("groupName");
                SerializedProperty entriesProperty = variantGroupProperty.FindPropertyRelative("entries");
                string groupLabel = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                    ? groupNameProperty.stringValue
                    : $"技能组 {variantGroupIndex + 1}";

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    string foldoutLabel = entriesProperty != null
                        ? $"{groupLabel} ({entriesProperty.arraySize})"
                        : groupLabel;
                    variantGroupProperty.isExpanded = EditorGUILayout.Foldout(variantGroupProperty.isExpanded, foldoutLabel, true);
                    if (!variantGroupProperty.isExpanded)
                        continue;

                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField("组名", groupLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("添加被动技能效果", GUILayout.Width(120f)))
                        {
                            AddVariantEntry(database, groupIndex, variantGroupIndex);
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUILayout.Space(2f);
                    DrawVariantEntries(database, groupIndex, variantGroupIndex, entriesProperty);
                }
            }
        }

        private static void DrawVariantEntries(PassiveSkillEffectDatabaseSO database, int groupIndex, int variantGroupIndex, SerializedProperty entriesProperty)
        {
            if (entriesProperty == null)
                return;

            for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
            {
                SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);
                if (entryProperty == null)
                    continue;

                SerializedProperty skillIdProperty = entryProperty.FindPropertyRelative("skillId");
                SerializedProperty statModifierProperty = entryProperty.FindPropertyRelative("statModifier");
                string skillLabel = !string.IsNullOrWhiteSpace(skillIdProperty?.stringValue)
                    ? skillIdProperty.stringValue
                    : $"技能ID {entryIndex + 1}";
                bool isDefaultEntry = entryIndex == 0;

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    string foldoutLabel = isDefaultEntry ? $"{skillLabel} [默认]" : skillLabel;
                    entryProperty.isExpanded = EditorGUILayout.Foldout(entryProperty.isExpanded, foldoutLabel, true);
                    if (!entryProperty.isExpanded)
                        continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(isDefaultEntry))
                            EditorGUILayout.PropertyField(skillIdProperty, new GUIContent("技能ID"));

                        using (new EditorGUI.DisabledScope(isDefaultEntry))
                        {
                            if (GUILayout.Button("删除", GUILayout.Width(52f)))
                            {
                                RemoveVariantEntry(database, groupIndex, variantGroupIndex, entryIndex);
                                GUIUtility.ExitGUI();
                            }
                        }
                    }

                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("displayName"), new GUIContent("显示名称"));
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("effectDescription"), new GUIContent("技能效果描述"));
                    using (new EditorGUI.DisabledScope(isDefaultEntry))
                        EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("mutationUnlockItemId"));
                    EditorGUILayout.PropertyField(statModifierProperty, true);
                }
            }
        }

        private static void AddGroup(PassiveSkillEffectDatabaseSO database)
        {
            if (database == null)
                return;

            Undo.RecordObject(database, "添加被动技能效果分组");

            database.groups ??= new List<PassiveSkillEffectGroupDefinition>();
            PassiveSkillEffectGroupDefinition group = new PassiveSkillEffectGroupDefinition
            {
                groupId = BuildNewGroupId(database),
                groupName = $"分组{database.groups.Count + 1}",
                skillGroups = new List<PassiveSkillEffectVariantGroupDefinition>(),
                entries = new List<PassiveSkillEffectDefinition>(),
            };
            database.groups.Add(group);
            AddVariantGroup(database, database.groups.Count - 1, recordUndo: false);
            database.Synchronize();
            EditorUtility.SetDirty(database);
        }

        private static SkillConfigDatabaseSO LoadDefaultSkillConfigDatabase()
        {
            return AssetDatabase.LoadAssetAtPath<SkillConfigDatabaseSO>(DefaultSkillConfigDatabasePath);
        }

        private static PassiveSkillEffectDatabaseSO LoadDefaultPassiveSkillEffectDatabase()
        {
            return AssetDatabase.LoadAssetAtPath<PassiveSkillEffectDatabaseSO>(DefaultPassiveSkillEffectDatabasePath);
        }

        private static PassiveSkillEffectGroupDefinition FindOrCreatePlayerPassiveGroup(PassiveSkillEffectDatabaseSO database, ref bool changed)
        {
            database.groups ??= new List<PassiveSkillEffectGroupDefinition>();
            for (int i = 0; i < database.groups.Count; i++)
            {
                PassiveSkillEffectGroupDefinition group = database.groups[i];
                if (group == null)
                    continue;

                if (string.Equals(group.groupId, PlayerPassiveGroupId, System.StringComparison.Ordinal)
                    || string.Equals(group.groupName, PlayerPassiveGroupName, System.StringComparison.Ordinal))
                {
                    group.skillGroups ??= new List<PassiveSkillEffectVariantGroupDefinition>();
                    group.entries ??= new List<PassiveSkillEffectDefinition>();
                    return group;
                }
            }

            var createdGroup = new PassiveSkillEffectGroupDefinition
            {
                groupId = PlayerPassiveGroupId,
                groupName = PlayerPassiveGroupName,
                skillGroups = new List<PassiveSkillEffectVariantGroupDefinition>(),
                entries = new List<PassiveSkillEffectDefinition>(),
            };
            database.groups.Add(createdGroup);
            changed = true;
            return createdGroup;
        }

        private static bool EnsureVariantGroup(PassiveSkillEffectGroupDefinition group, string skillId)
        {
            if (group == null || string.IsNullOrWhiteSpace(skillId))
                return false;

            group.skillGroups ??= new List<PassiveSkillEffectVariantGroupDefinition>();
            if (PassiveSkillEffectDatabaseSO.FindVariantGroup(group, skillId) != null)
                return false;

            group.skillGroups.Add(new PassiveSkillEffectVariantGroupDefinition
            {
                groupName = skillId,
                entries = new List<PassiveSkillEffectDefinition>
                {
                    new PassiveSkillEffectDefinition
                    {
                        skillId = skillId,
                        statModifier = new Game.Domain.StatModifier(),
                    }
                }
            });
            return true;
        }

        private static List<string> CollectPassiveSkillIds(SkillConfigDatabaseSO skillConfigDatabase)
        {
            var result = new List<string>();
            if (skillConfigDatabase?.entries == null)
                return result;

            for (int i = 0; i < skillConfigDatabase.entries.Count; i++)
            {
                SkillConfigEntry entry = skillConfigDatabase.entries[i];
                if (entry == null || !entry.IsPassiveSkill || string.IsNullOrWhiteSpace(entry.skillId))
                    continue;

                string skillId = entry.skillId.Trim();
                bool exists = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if (string.Equals(result[j], skillId, System.StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    result.Add(skillId);
            }

            return result;
        }

        private static bool ReorderVariantGroups(PassiveSkillEffectGroupDefinition group, List<string> orderedSkillIds)
        {
            if (group?.skillGroups == null || group.skillGroups.Count <= 1 || orderedSkillIds == null || orderedSkillIds.Count == 0)
                return false;

            var remaining = new List<PassiveSkillEffectVariantGroupDefinition>(group.skillGroups);
            var reordered = new List<PassiveSkillEffectVariantGroupDefinition>(group.skillGroups.Count);

            for (int i = 0; i < orderedSkillIds.Count; i++)
            {
                PassiveSkillEffectVariantGroupDefinition variantGroup = FindMatchingVariantGroup(remaining, orderedSkillIds[i]);
                if (variantGroup == null)
                    continue;

                reordered.Add(variantGroup);
                remaining.Remove(variantGroup);
            }

            for (int i = 0; i < remaining.Count; i++)
            {
                if (remaining[i] != null)
                    reordered.Add(remaining[i]);
            }

            if (reordered.Count != group.skillGroups.Count)
                return false;

            bool changed = false;
            for (int i = 0; i < reordered.Count; i++)
            {
                if (!ReferenceEquals(group.skillGroups[i], reordered[i]))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
                return false;

            group.skillGroups = reordered;
            return true;
        }

        private static PassiveSkillEffectVariantGroupDefinition FindMatchingVariantGroup(
            List<PassiveSkillEffectVariantGroupDefinition> variantGroups,
            string skillId)
        {
            if (variantGroups == null || string.IsNullOrWhiteSpace(skillId))
                return null;

            for (int i = 0; i < variantGroups.Count; i++)
            {
                PassiveSkillEffectVariantGroupDefinition variantGroup = variantGroups[i];
                if (variantGroup == null)
                    continue;

                if (string.Equals(PassiveSkillEffectDatabaseSO.GetVariantGroupName(variantGroup), skillId, System.StringComparison.Ordinal))
                    return variantGroup;

                if (variantGroup.entries == null)
                    continue;

                for (int j = 0; j < variantGroup.entries.Count; j++)
                {
                    PassiveSkillEffectDefinition entry = variantGroup.entries[j];
                    if (entry != null && string.Equals(entry.skillId, skillId, System.StringComparison.Ordinal))
                        return variantGroup;
                }
            }

            return null;
        }

        private static void RemoveGroup(PassiveSkillEffectDatabaseSO database, int groupIndex)
        {
            if (database?.groups == null || groupIndex < 0 || groupIndex >= database.groups.Count)
                return;

            Undo.RecordObject(database, "删除被动技能效果分组");
            database.groups.RemoveAt(groupIndex);
            database.Synchronize();
            EditorUtility.SetDirty(database);
        }

        private static void AddVariantGroup(PassiveSkillEffectDatabaseSO database, int groupIndex, bool recordUndo = true)
        {
            if (database?.groups == null || groupIndex < 0 || groupIndex >= database.groups.Count)
                return;

            PassiveSkillEffectGroupDefinition group = database.groups[groupIndex];
            if (group == null)
                return;

            if (recordUndo)
                Undo.RecordObject(database, "添加被动技能小分组");

            group.skillGroups ??= new List<PassiveSkillEffectVariantGroupDefinition>();
            string skillId = BuildNewPrimarySkillId(database);
            group.skillGroups.Add(new PassiveSkillEffectVariantGroupDefinition
            {
                groupName = skillId,
                entries = new List<PassiveSkillEffectDefinition>
                {
                    new PassiveSkillEffectDefinition
                    {
                        skillId = skillId,
                        statModifier = new Game.Domain.StatModifier(),
                    }
                }
            });

            database.Synchronize();
            EditorUtility.SetDirty(database);
        }

        private static void RemoveVariantGroup(PassiveSkillEffectDatabaseSO database, int groupIndex, int variantGroupIndex)
        {
            if (database?.groups == null || groupIndex < 0 || groupIndex >= database.groups.Count)
                return;

            PassiveSkillEffectGroupDefinition group = database.groups[groupIndex];
            if (group?.skillGroups == null || variantGroupIndex < 0 || variantGroupIndex >= group.skillGroups.Count)
                return;

            Undo.RecordObject(database, "删除被动技能小分组");
            group.skillGroups.RemoveAt(variantGroupIndex);
            database.Synchronize();
            EditorUtility.SetDirty(database);
        }

        private static void AddVariantEntry(PassiveSkillEffectDatabaseSO database, int groupIndex, int variantGroupIndex)
        {
            if (database?.groups == null || groupIndex < 0 || groupIndex >= database.groups.Count)
                return;

            PassiveSkillEffectGroupDefinition group = database.groups[groupIndex];
            if (group?.skillGroups == null || variantGroupIndex < 0 || variantGroupIndex >= group.skillGroups.Count)
                return;

            PassiveSkillEffectVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
            if (variantGroup == null)
                return;

            Undo.RecordObject(database, "添加被动技能效果");

            PassiveSkillEffectDefinition template = PassiveSkillEffectDatabaseSO.GetPrimaryEntry(variantGroup);
            PassiveSkillEffectDefinition created = CloneDefinition(template);
            created.skillId = BuildNewVariantSkillId(database, variantGroup);
            EnsureDefinition(created);

            variantGroup.entries ??= new List<PassiveSkillEffectDefinition>();
            variantGroup.entries.Add(created);
            database.Synchronize();
            EditorUtility.SetDirty(database);
        }

        private static void RemoveVariantEntry(PassiveSkillEffectDatabaseSO database, int groupIndex, int variantGroupIndex, int entryIndex)
        {
            if (database?.groups == null || entryIndex <= 0 || groupIndex < 0 || groupIndex >= database.groups.Count)
                return;

            PassiveSkillEffectGroupDefinition group = database.groups[groupIndex];
            if (group?.skillGroups == null || variantGroupIndex < 0 || variantGroupIndex >= group.skillGroups.Count)
                return;

            PassiveSkillEffectVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
            if (variantGroup?.entries == null || entryIndex >= variantGroup.entries.Count)
                return;

            Undo.RecordObject(database, "删除被动技能效果");
            variantGroup.entries.RemoveAt(entryIndex);
            database.Synchronize();
            EditorUtility.SetDirty(database);
        }

        private static PassiveSkillEffectDefinition CloneDefinition(PassiveSkillEffectDefinition source)
        {
            PassiveSkillEffectDefinition clone = source != null
                ? JsonUtility.FromJson<PassiveSkillEffectDefinition>(JsonUtility.ToJson(source))
                : new PassiveSkillEffectDefinition();

            EnsureDefinition(clone);
            return clone;
        }

        private static void EnsureDefinition(PassiveSkillEffectDefinition definition)
        {
            if (definition == null)
                return;

            definition.statModifier ??= new Game.Domain.StatModifier();
        }

        private static string BuildNewGroupId(PassiveSkillEffectDatabaseSO database)
        {
            int suffix = database?.groups != null ? database.groups.Count + 1 : 1;
            string candidate = $"group_{suffix}";

            while (database?.groups != null && ContainsGroupId(database, candidate))
            {
                suffix++;
                candidate = $"group_{suffix}";
            }

            return candidate;
        }

        private static bool ContainsGroupId(PassiveSkillEffectDatabaseSO database, string groupId)
        {
            if (database?.groups == null || string.IsNullOrWhiteSpace(groupId))
                return false;

            for (int i = 0; i < database.groups.Count; i++)
            {
                PassiveSkillEffectGroupDefinition group = database.groups[i];
                if (group != null && string.Equals(group.groupId, groupId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string BuildNewPrimarySkillId(PassiveSkillEffectDatabaseSO database)
        {
            int suffix = 1;
            string candidate = $"passive_{suffix}";
            while (database != null && database.Contains(candidate))
            {
                suffix++;
                candidate = $"passive_{suffix}";
            }

            return candidate;
        }

        private static string BuildNewVariantSkillId(PassiveSkillEffectDatabaseSO database, PassiveSkillEffectVariantGroupDefinition variantGroup)
        {
            string baseName = PassiveSkillEffectDatabaseSO.GetVariantGroupName(variantGroup);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "passive_skill";

            int suffix = variantGroup?.entries != null ? variantGroup.entries.Count : 1;
            string candidate = $"{baseName}_Variant{suffix}";
            while (database != null && database.Contains(candidate))
            {
                suffix++;
                candidate = $"{baseName}_Variant{suffix}";
            }

            return candidate;
        }
    }
}
