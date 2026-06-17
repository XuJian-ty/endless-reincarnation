using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(EnemyStatsDatabaseSO))]
    public sealed class EnemyStatsDatabaseSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _groupsProperty;

        private void OnEnable()
        {
            _groupsProperty = serializedObject.FindProperty(nameof(EnemyStatsDatabaseSO.groups));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_groupsProperty == null)
            {
                EditorGUILayout.HelpBox("未找到敌人属性分组数据。", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField("分组数量", _groupsProperty.arraySize);

            EditorGUILayout.HelpBox("敌人属性分组由敌人行为资产同步维护，这里只编辑数值，不支持手工新增或删除分组/列表项。", MessageType.Info);
            EditorGUILayout.Space(4f);
            DrawGroups();

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed && target is EnemyStatsDatabaseSO database)
            {
                database.SyncFlatEntries();
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssetIfDirty(database);
            }
        }

        private void DrawGroups()
        {
            for (int groupIndex = 0; groupIndex < _groupsProperty.arraySize; groupIndex++)
            {
                SerializedProperty groupProperty = _groupsProperty.GetArrayElementAtIndex(groupIndex);
                if (groupProperty == null)
                    continue;

                SerializedProperty groupIdProperty = groupProperty.FindPropertyRelative(nameof(EnemyStatsGroupDefinition.groupId));
                SerializedProperty groupNameProperty = groupProperty.FindPropertyRelative(nameof(EnemyStatsGroupDefinition.groupName));
                SerializedProperty entriesProperty = groupProperty.FindPropertyRelative(nameof(EnemyStatsGroupDefinition.entries));

                string groupLabel = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                    ? groupNameProperty.stringValue.Trim()
                    : $"分组 {groupIndex + 1}";

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string foldoutLabel = entriesProperty != null
                        ? $"{groupLabel} ({entriesProperty.arraySize})"
                        : groupLabel;
                    groupProperty.isExpanded = EditorGUILayout.Foldout(groupProperty.isExpanded, foldoutLabel, true);
                    if (!groupProperty.isExpanded)
                        continue;

                    using (new EditorGUI.DisabledScope(true))
                    {
                        if (groupIdProperty != null)
                            EditorGUILayout.PropertyField(groupIdProperty);
                        if (groupNameProperty != null)
                            EditorGUILayout.PropertyField(groupNameProperty);
                        if (entriesProperty != null)
                            EditorGUILayout.IntField("条目数量", entriesProperty.arraySize);
                    }

                    if (entriesProperty == null)
                        continue;

                    EditorGUILayout.Space(2f);
                    for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
                    {
                        SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);
                        if (entryProperty == null)
                            continue;

                        DrawEntry(entryProperty, entryIndex);
                    }
                }
            }
        }

        private static void DrawEntry(SerializedProperty entryProperty, int entryIndex)
        {
            SerializedProperty displayNameProperty = entryProperty.FindPropertyRelative(nameof(EnemyStatsEntry.displayName));
            SerializedProperty enemyIdProperty = entryProperty.FindPropertyRelative(nameof(EnemyStatsEntry.enemyId));

            string entryLabel = !string.IsNullOrWhiteSpace(displayNameProperty?.stringValue)
                ? displayNameProperty.stringValue.Trim()
                : !string.IsNullOrWhiteSpace(enemyIdProperty?.stringValue)
                    ? enemyIdProperty.stringValue.Trim()
                    : $"敌人 {entryIndex + 1}";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(entryLabel, EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    DrawProperty(entryProperty, nameof(EnemyStatsEntry.enemyId));
                    DrawProperty(entryProperty, nameof(EnemyStatsEntry.displayName));
                    DrawProperty(entryProperty, nameof(EnemyStatsEntry.type));
                }

                DrawProperty(entryProperty, nameof(EnemyStatsEntry.baseHp));
                DrawProperty(entryProperty, nameof(EnemyStatsEntry.baseAttack));
                DrawProperty(entryProperty, nameof(EnemyStatsEntry.baseDefense));
                DrawProperty(entryProperty, nameof(EnemyStatsEntry.baseMoveSpeed));
                DrawProperty(entryProperty, nameof(EnemyStatsEntry.goldMin));
                DrawProperty(entryProperty, nameof(EnemyStatsEntry.goldMax));
                DrawProperty(entryProperty, nameof(EnemyStatsEntry.baseExp));
            }
        }

        private static void DrawProperty(SerializedProperty parent, string relativeName)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativeName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }
    }
}
