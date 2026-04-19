using Game.Data;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(CharacterAnimationLibrarySO))]
    public sealed class CharacterAnimationLibrarySOEditor : UnityEditor.Editor
    {
        private const string SkillConfigDatabasePath = "Assets/Resources/配置/玩家动作及技能配置库.asset";
        private SerializedProperty _groupsProperty;
        private SkillConfigDatabaseSO _skillConfigDatabase;
        private readonly Dictionary<string, bool> _playerSkillGroupFoldouts = new Dictionary<string, bool>();
        private bool _pendingSaveAsset;

        private void OnEnable()
        {
            SkillEffectAnimationLibrarySyncUtility.TryEnsurePlayerAuthoringAssets(saveAssets: true);
            _groupsProperty = serializedObject.FindProperty(nameof(CharacterAnimationLibrarySO.groups));
            _skillConfigDatabase = AssetDatabase.LoadAssetAtPath<SkillConfigDatabaseSO>(SkillConfigDatabasePath);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            _pendingSaveAsset = false;

            if (_groupsProperty == null)
            {
                EditorGUILayout.HelpBox("未找到动画分组数据。", MessageType.Warning);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("分组数量", _groupsProperty.arraySize);

            EditorGUILayout.HelpBox("动画分组由玩家/敌人作者化流程同步维护，这里只编辑动画片段，不支持手工新增或删除分组/列表项。", MessageType.Info);
            EditorGUILayout.Space(4f);

            for (int i = 0; i < _groupsProperty.arraySize; i++)
            {
                SerializedProperty groupProperty = _groupsProperty.GetArrayElementAtIndex(i);
                if (groupProperty == null)
                    continue;

                SerializedProperty groupIdProperty = groupProperty.FindPropertyRelative(nameof(CharacterAnimationGroupDefinition.groupId));
                SerializedProperty groupNameProperty = groupProperty.FindPropertyRelative(nameof(CharacterAnimationGroupDefinition.groupName));
                SerializedProperty skillGroupsProperty = groupProperty.FindPropertyRelative(nameof(CharacterAnimationGroupDefinition.skillGroups));
                string groupLabel = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                    ? groupNameProperty.stringValue.Trim()
                    : $"分组{i + 1}";

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
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

                    if (skillGroupsProperty == null)
                        continue;

                    if (IsPlayerGroup(groupIdProperty))
                        DrawConfiguredPlayerEntries(skillGroupsProperty);
                    else
                        DrawVariantGroups(skillGroupsProperty);
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (_pendingSaveAsset && target != null)
                AssetDatabase.SaveAssetIfDirty(target);
        }

        private void DrawConfiguredPlayerEntries(SerializedProperty skillGroupsProperty)
        {
            if (skillGroupsProperty == null)
            {
                DrawVariantGroups(skillGroupsProperty);
                return;
            }

            EditorGUILayout.HelpBox("玩家动画直接按动画库里的技能小分组显示；排序由玩家动作及技能配置库同步维护。", MessageType.None);

            PlayerSkillEntryGroup? currentSection = null;
            for (int variantGroupIndex = 0; variantGroupIndex < skillGroupsProperty.arraySize; variantGroupIndex++)
            {
                SerializedProperty variantGroupProperty = skillGroupsProperty.GetArrayElementAtIndex(variantGroupIndex);
                if (variantGroupProperty == null)
                    continue;

                if (TryGetPlayerEntryGroup(variantGroupProperty, out PlayerSkillEntryGroup entryGroup) && currentSection != entryGroup)
                {
                    currentSection = entryGroup;
                    DrawSectionHeader(GetEntryGroupLabel(entryGroup));
                }

                DrawVariantGroup(variantGroupProperty, variantGroupIndex);
            }
        }

        private void DrawVariantGroups(SerializedProperty skillGroupsProperty)
        {
            if (skillGroupsProperty == null)
                return;

            for (int variantGroupIndex = 0; variantGroupIndex < skillGroupsProperty.arraySize; variantGroupIndex++)
            {
                SerializedProperty variantGroupProperty = skillGroupsProperty.GetArrayElementAtIndex(variantGroupIndex);
                DrawVariantGroup(variantGroupProperty, variantGroupIndex);
            }
        }

        private void DrawVariantGroup(SerializedProperty variantGroupProperty, int variantGroupIndex)
        {
            if (variantGroupProperty == null)
                return;

            SerializedProperty groupNameProperty = variantGroupProperty.FindPropertyRelative(nameof(CharacterAnimationVariantGroupDefinition.groupName));
            SerializedProperty entriesProperty = variantGroupProperty.FindPropertyRelative(nameof(CharacterAnimationVariantGroupDefinition.entries));
            string groupName = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                ? groupNameProperty.stringValue.Trim()
                : $"技能组{variantGroupIndex + 1}";
            int variantCount = entriesProperty != null ? entriesProperty.arraySize : 0;
            bool isExpanded = GetFoldoutState(groupName, true);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                isExpanded = EditorGUILayout.Foldout(isExpanded, $"{groupName} ({variantCount})", true);
                SetFoldoutState(groupName, isExpanded);
                if (!isExpanded)
                    return;

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(groupNameProperty);

                if (entriesProperty == null)
                    return;

                for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
                {
                    SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);
                    DrawAnimationEntry(entryProperty, entryIndex, entryIndex == 0);
                }
            }
        }

        private void DrawAnimationEntry(SerializedProperty entryProperty, int entryIndex, bool isDefaultEntry = false)
        {
            if (entryProperty == null)
                return;

            SerializedProperty animationIdProperty = entryProperty.FindPropertyRelative(nameof(CharacterAnimationEntry.animationId));
            string animationLabel = !string.IsNullOrWhiteSpace(animationIdProperty?.stringValue)
                ? animationIdProperty.stringValue.Trim()
                : $"动画 {entryIndex + 1}";

            if (isDefaultEntry)
                animationLabel += " [默认]";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                entryProperty.isExpanded = EditorGUILayout.Foldout(entryProperty.isExpanded, animationLabel, true);
                if (!entryProperty.isExpanded)
                    return;

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(animationIdProperty);
                DrawClipField(entryProperty, animationIdProperty);
            }
        }

        private void DrawClipField(SerializedProperty entryProperty, SerializedProperty animationIdProperty)
        {
            if (entryProperty == null)
                return;

            string animationId = animationIdProperty != null ? animationIdProperty.stringValue?.Trim() : string.Empty;
            CharacterAnimationEntry entry = FindAnimationEntry(animationId);
            AnimationClip currentClip = entry != null ? entry.clip : null;

            EditorGUI.BeginChangeCheck();
            AnimationClip newClip = (AnimationClip)EditorGUILayout.ObjectField("动画片段", currentClip, typeof(AnimationClip), false);
            if (!EditorGUI.EndChangeCheck())
                return;

            CharacterAnimationLibrarySO library = target as CharacterAnimationLibrarySO;
            if (entry == null || library == null)
                return;

            if (library != null)
                Undo.RecordObject(library, "修改动画片段引用");

            entry.clip = newClip;
            EditorUtility.SetDirty(library);
            _pendingSaveAsset = true;

            GUI.changed = true;
        }

        private CharacterAnimationEntry FindAnimationEntry(string animationId)
        {
            CharacterAnimationLibrarySO library = target as CharacterAnimationLibrarySO;
            if (library?.groups == null || string.IsNullOrWhiteSpace(animationId))
                return null;

            for (int groupIndex = 0; groupIndex < library.groups.Count; groupIndex++)
            {
                CharacterAnimationGroupDefinition group = library.groups[groupIndex];
                if (group?.skillGroups == null)
                    continue;

                for (int variantGroupIndex = 0; variantGroupIndex < group.skillGroups.Count; variantGroupIndex++)
                {
                    CharacterAnimationVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
                    if (variantGroup?.entries == null)
                        continue;

                    for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
                    {
                        CharacterAnimationEntry entry = variantGroup.entries[entryIndex];
                        if (entry != null
                            && string.Equals(entry.animationId?.Trim(), animationId, System.StringComparison.Ordinal))
                        {
                            return entry;
                        }
                    }
                }
            }

            return null;
        }

        private bool TryGetPlayerEntryGroup(SerializedProperty variantGroupProperty, out PlayerSkillEntryGroup entryGroup)
        {
            entryGroup = PlayerSkillEntryGroup.BaseSkill;
            if (_skillConfigDatabase?.entries == null || variantGroupProperty == null)
                return false;

            SerializedProperty groupNameProperty = variantGroupProperty.FindPropertyRelative(nameof(CharacterAnimationVariantGroupDefinition.groupName));
            string groupName = groupNameProperty != null ? groupNameProperty.stringValue?.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(groupName))
                return false;

            for (int i = 0; i < _skillConfigDatabase.entries.Count; i++)
            {
                SkillConfigEntry configEntry = _skillConfigDatabase.entries[i];
                if (configEntry == null || configEntry.IsPassiveSkill)
                    continue;

                if (string.Equals(configEntry.GetResolvedSkillId()?.Trim(), groupName, System.StringComparison.Ordinal)
                    || string.Equals(configEntry.GetResolvedActionId()?.Trim(), groupName, System.StringComparison.Ordinal))
                {
                    entryGroup = configEntry.entryGroup;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPlayerGroup(SerializedProperty groupIdProperty)
        {
            return groupIdProperty != null
                   && string.Equals(groupIdProperty.stringValue?.Trim(), "player", System.StringComparison.Ordinal);
        }

        private bool GetFoldoutState(string key, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue;

            return _playerSkillGroupFoldouts.TryGetValue(key, out bool value) ? value : defaultValue;
        }

        private void SetFoldoutState(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            _playerSkillGroupFoldouts[key] = value;
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
