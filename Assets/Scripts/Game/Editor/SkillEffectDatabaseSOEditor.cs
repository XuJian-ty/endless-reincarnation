using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomEditor(typeof(SkillEffectDatabaseSO))]
    public class SkillEffectDatabaseSOEditor : UnityEditor.Editor
    {
        private const string SkillConfigDatabasePath = "Assets/Resources/配置/玩家动作及技能配置库.asset";
        private SerializedProperty _groupsProperty;
        private SkillConfigDatabaseSO _skillConfigDatabase;

        private void OnEnable()
        {
            SkillEffectAnimationLibrarySyncUtility.TryEnsurePlayerAuthoringAssets(saveAssets: true);
            _groupsProperty = serializedObject.FindProperty("groups");
            _skillConfigDatabase = AssetDatabase.LoadAssetAtPath<SkillConfigDatabaseSO>(SkillConfigDatabasePath);
        }

        public override void OnInspectorGUI()
        {
            SkillEffectDatabaseSO database = (SkillEffectDatabaseSO)target;
            serializedObject.Update();

            DrawToolbar();
            EditorGUILayout.Space(6f);

            if (_groupsProperty == null)
            {
                EditorGUILayout.HelpBox("未找到技能分组数据。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("技能分组由玩家/敌人作者化流程同步维护。这里支持在每个技能小分组下增加多个技能效果变体；每个小分组的第一个技能效果为默认项，不可删除、不可改名。", MessageType.Info);
                DrawGroups(database, _groupsProperty, _skillConfigDatabase);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                database?.Synchronize();
                if (database != null)
                    EditorUtility.SetDirty(database);
            }
        }

        private static void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("打开技能编辑器", GUILayout.Width(120f)))
                    SharedSkillTimelineEditorWindow.Open();
            }
        }

        private static void DrawGroups(
            SkillEffectDatabaseSO database,
            SerializedProperty groupsProperty,
            SkillConfigDatabaseSO skillConfigDatabase)
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
                    : $"分组 {groupIndex}";

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    string foldoutLabel = skillGroupsProperty != null
                        ? $"{groupLabel} ({skillGroupsProperty.arraySize})"
                        : groupLabel;
                    groupProperty.isExpanded = EditorGUILayout.Foldout(groupProperty.isExpanded, foldoutLabel, true);
                    if (!groupProperty.isExpanded)
                        continue;

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(groupIdProperty);
                        EditorGUILayout.PropertyField(groupNameProperty);
                        if (skillGroupsProperty != null)
                            EditorGUILayout.IntField("技能小分组数量", skillGroupsProperty.arraySize);
                    }
                    EditorGUILayout.Space(4f);

                    if (IsPlayerGroup(groupIdProperty))
                        DrawConfiguredPlayerVariantGroups(database, groupIndex, skillGroupsProperty, skillConfigDatabase);
                    else
                        DrawVariantGroups(database, groupIndex, skillGroupsProperty);
                }
            }
        }

        private static void DrawVariantGroups(SkillEffectDatabaseSO database, int groupIndex, SerializedProperty skillGroupsProperty)
        {
            if (skillGroupsProperty == null)
                return;

            for (int variantGroupIndex = 0; variantGroupIndex < skillGroupsProperty.arraySize; variantGroupIndex++)
            {
                SerializedProperty variantGroupProperty = skillGroupsProperty.GetArrayElementAtIndex(variantGroupIndex);
                if (variantGroupProperty == null)
                    continue;

                DrawVariantGroup(database, groupIndex, variantGroupIndex, variantGroupProperty);
            }
        }

        private static void DrawConfiguredPlayerVariantGroups(
            SkillEffectDatabaseSO database,
            int groupIndex,
            SerializedProperty skillGroupsProperty,
            SkillConfigDatabaseSO skillConfigDatabase)
        {
            if (skillGroupsProperty == null || skillConfigDatabase?.entries == null)
            {
                DrawVariantGroups(database, groupIndex, skillGroupsProperty);
                return;
            }

            HashSet<int> drawnVariantIndices = new HashSet<int>();
            PlayerSkillEntryGroup? currentSection = null;

            for (int i = 0; i < skillConfigDatabase.entries.Count; i++)
            {
                SkillConfigEntry configEntry = skillConfigDatabase.entries[i];
                if (configEntry == null || configEntry.IsPassiveSkill)
                    continue;

                int variantGroupIndex = FindVariantGroupIndex(skillGroupsProperty, configEntry.GetResolvedSkillId());
                if (variantGroupIndex < 0 || drawnVariantIndices.Contains(variantGroupIndex))
                    continue;

                if (currentSection != configEntry.entryGroup)
                {
                    currentSection = configEntry.entryGroup;
                    DrawSectionHeader(GetEntryGroupLabel(configEntry.entryGroup));
                }

                SerializedProperty variantGroupProperty = skillGroupsProperty.GetArrayElementAtIndex(variantGroupIndex);
                if (variantGroupProperty == null)
                    continue;

                DrawVariantGroup(database, groupIndex, variantGroupIndex, variantGroupProperty);
                drawnVariantIndices.Add(variantGroupIndex);
            }

            bool hasOrphans = false;
            for (int variantGroupIndex = 0; variantGroupIndex < skillGroupsProperty.arraySize; variantGroupIndex++)
            {
                if (drawnVariantIndices.Contains(variantGroupIndex))
                    continue;

                if (!hasOrphans)
                {
                    DrawSectionHeader("未绑定技能效果");
                    hasOrphans = true;
                }

                SerializedProperty variantGroupProperty = skillGroupsProperty.GetArrayElementAtIndex(variantGroupIndex);
                if (variantGroupProperty == null)
                    continue;

                DrawVariantGroup(database, groupIndex, variantGroupIndex, variantGroupProperty);
            }
        }

        private static void DrawVariantGroup(
            SkillEffectDatabaseSO database,
            int groupIndex,
            int variantGroupIndex,
            SerializedProperty variantGroupProperty)
        {
            SerializedProperty groupNameProperty = variantGroupProperty.FindPropertyRelative("groupName");
            SerializedProperty entriesProperty = variantGroupProperty.FindPropertyRelative("entries");
            string groupLabel = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                ? groupNameProperty.stringValue
                : $"技能组 {variantGroupIndex}";

            using (new EditorGUILayout.VerticalScope("box"))
            {
                string foldoutLabel = entriesProperty != null
                    ? $"{groupLabel} ({entriesProperty.arraySize})"
                    : groupLabel;
                variantGroupProperty.isExpanded = EditorGUILayout.Foldout(variantGroupProperty.isExpanded, foldoutLabel, true);
                if (!variantGroupProperty.isExpanded)
                    return;

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("组名", groupLabel);

                EditorGUILayout.HelpBox("首个技能效果为默认项，不可删除、不可改名。新增的技能效果变体可单独填写技能ID。", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("添加技能效果", GUILayout.Width(110f)))
                    {
                        AddVariantEntry(database, groupIndex, variantGroupIndex);
                        GUIUtility.ExitGUI();
                    }
                }

                DrawVariantEntries(database, groupIndex, variantGroupIndex, entriesProperty);
            }
        }

        private static void DrawVariantEntries(SkillEffectDatabaseSO database, int groupIndex, int variantGroupIndex, SerializedProperty entriesProperty)
        {
            if (entriesProperty == null)
                return;

            for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
            {
                SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);
                if (entryProperty == null)
                    continue;

                SerializedProperty skillIdProperty = entryProperty.FindPropertyRelative("skillId");
                string skillLabel = !string.IsNullOrWhiteSpace(skillIdProperty?.stringValue)
                    ? skillIdProperty.stringValue
                    : $"技能效果 {entryIndex}";
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
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("animationTrigger"), new GUIContent("动画 Trigger"));
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("effectDescription"), new GUIContent("技能效果描述"));
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("mutationTalentCost"));
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("ignoreAnimationDamageEvents"));
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("damageEvents"), new GUIContent("命中事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("physicsEvents"), new GUIContent("物理事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("attributeEvents"), new GUIContent("属性事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("vfxEvents"), new GUIContent("特效事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("sfxEvents"), new GUIContent("音效事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("speedEvents"), new GUIContent("速度区间列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("cameraEvents"), new GUIContent("镜头区间列表"), true);
                }
            }
        }

        private static void AddVariantEntry(SkillEffectDatabaseSO database, int groupIndex, int variantGroupIndex)
        {
            if (database?.groups == null || groupIndex < 0 || groupIndex >= database.groups.Count)
                return;

            SkillGroupDefinition group = database.groups[groupIndex];
            if (group?.skillGroups == null || variantGroupIndex < 0 || variantGroupIndex >= group.skillGroups.Count)
                return;

            SkillEffectVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
            if (variantGroup == null)
                return;

            Undo.RecordObject(database, "添加技能效果");

            SharedSkillDefinition template = SkillEffectDatabaseSO.GetPrimaryEntry(variantGroup);
            SharedSkillDefinition created = CloneSkillDefinition(template);
            created.skillId = BuildNewVariantSkillId(database, variantGroup);
            if (string.IsNullOrWhiteSpace(created.displayName))
                created.displayName = created.skillId;

            EnsureSkillLists(created);

            variantGroup.entries ??= new List<SharedSkillDefinition>();
            variantGroup.entries.Add(created);
            database.Synchronize();
            SkillEffectAnimationLibrarySyncUtility.TrySyncVariantEntriesFromDatabase(database, groupIndex, variantGroupIndex);
            EditorUtility.SetDirty(database);
        }

        private static void RemoveVariantEntry(SkillEffectDatabaseSO database, int groupIndex, int variantGroupIndex, int entryIndex)
        {
            if (database?.groups == null || entryIndex <= 0 || groupIndex < 0 || groupIndex >= database.groups.Count)
                return;

            SkillGroupDefinition group = database.groups[groupIndex];
            if (group?.skillGroups == null || variantGroupIndex < 0 || variantGroupIndex >= group.skillGroups.Count)
                return;

            SkillEffectVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
            if (variantGroup?.entries == null || entryIndex >= variantGroup.entries.Count)
                return;

            Undo.RecordObject(database, "删除技能效果");
            variantGroup.entries.RemoveAt(entryIndex);
            database.Synchronize();
            SkillEffectAnimationLibrarySyncUtility.TrySyncVariantEntriesFromDatabase(database, groupIndex, variantGroupIndex);
            EditorUtility.SetDirty(database);
        }

        private static SharedSkillDefinition CloneSkillDefinition(SharedSkillDefinition source)
        {
            SharedSkillDefinition clone = source != null
                ? JsonUtility.FromJson<SharedSkillDefinition>(JsonUtility.ToJson(source))
                : new SharedSkillDefinition();

            EnsureSkillLists(clone);
            return clone;
        }

        private static void EnsureSkillLists(SharedSkillDefinition definition)
        {
            if (definition == null)
                return;

            definition.damageEvents ??= new List<SkillDamageEvent>();
            definition.physicsEvents ??= new List<SkillPhysicsEvent>();
            definition.attributeEvents ??= new List<SkillAttributeEvent>();
            definition.vfxEvents ??= new List<SkillVfxEvent>();
            definition.sfxEvents ??= new List<SkillSfxEvent>();
            definition.speedEvents ??= new List<SkillSpeedEvent>();
            definition.cameraEvents ??= new List<SkillCameraEvent>();
        }

        private static string BuildNewVariantSkillId(SkillEffectDatabaseSO database, SkillEffectVariantGroupDefinition variantGroup)
        {
            string baseName = SkillEffectDatabaseSO.GetVariantGroupName(variantGroup);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "new_skill";

            int suffix = variantGroup?.entries != null ? variantGroup.entries.Count : 1;
            string candidate = $"{baseName}_Variant{suffix}";
            while (database != null && database.Contains(candidate))
            {
                suffix++;
                candidate = $"{baseName}_Variant{suffix}";
            }

            return candidate;
        }

        private static bool IsPlayerGroup(SerializedProperty groupIdProperty)
        {
            return groupIdProperty != null
                   && string.Equals(groupIdProperty.stringValue?.Trim(), "player", System.StringComparison.Ordinal);
        }

        private static int FindVariantGroupIndex(SerializedProperty skillGroupsProperty, string skillId)
        {
            if (skillGroupsProperty == null || string.IsNullOrWhiteSpace(skillId))
                return -1;

            string normalizedSkillId = skillId.Trim();
            for (int i = 0; i < skillGroupsProperty.arraySize; i++)
            {
                SerializedProperty variantGroupProperty = skillGroupsProperty.GetArrayElementAtIndex(i);
                if (variantGroupProperty == null)
                    continue;

                SerializedProperty groupNameProperty = variantGroupProperty.FindPropertyRelative("groupName");
                if (groupNameProperty != null
                    && string.Equals(groupNameProperty.stringValue?.Trim(), normalizedSkillId, System.StringComparison.Ordinal))
                {
                    return i;
                }

                SerializedProperty entriesProperty = variantGroupProperty.FindPropertyRelative("entries");
                if (entriesProperty == null || entriesProperty.arraySize == 0)
                    continue;

                SerializedProperty firstEntryProperty = entriesProperty.GetArrayElementAtIndex(0);
                SerializedProperty firstSkillIdProperty = firstEntryProperty?.FindPropertyRelative("skillId");
                if (firstSkillIdProperty != null
                    && string.Equals(firstSkillIdProperty.stringValue?.Trim(), normalizedSkillId, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void DrawSectionHeader(string label)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private static string GetEntryGroupLabel(PlayerSkillEntryGroup entryGroup)
        {
            switch (entryGroup)
            {
                case PlayerSkillEntryGroup.ActiveSkill:
                    return "主动技能";
                case PlayerSkillEntryGroup.BaseSkill:
                default:
                    return "基础动作";
            }
        }
    }
}
