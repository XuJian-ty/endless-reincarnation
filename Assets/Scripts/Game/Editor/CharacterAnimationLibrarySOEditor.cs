using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(CharacterAnimationLibrarySO))]
    public sealed class CharacterAnimationLibrarySOEditor : UnityEditor.Editor
    {
        private SerializedProperty _groupsProperty;

        private void OnEnable()
        {
            _groupsProperty = serializedObject.FindProperty(nameof(CharacterAnimationLibrarySO.groups));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

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
                SerializedProperty entriesProperty = groupProperty.FindPropertyRelative(nameof(CharacterAnimationGroupDefinition.entries));
                string groupLabel = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                    ? groupNameProperty.stringValue.Trim()
                    : $"分组{i + 1}";

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
                        EditorGUILayout.PropertyField(groupIdProperty);
                        EditorGUILayout.PropertyField(groupNameProperty);
                        if (entriesProperty != null)
                            EditorGUILayout.IntField("动画数量", entriesProperty.arraySize);
                    }

                    if (entriesProperty == null)
                        continue;

                    for (int entryIndex = 0; entryIndex < entriesProperty.arraySize; entryIndex++)
                    {
                        SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(entryIndex);
                        if (entryProperty == null)
                            continue;

                        SerializedProperty animationIdProperty = entryProperty.FindPropertyRelative(nameof(CharacterAnimationEntry.animationId));
                        string animationLabel = !string.IsNullOrWhiteSpace(animationIdProperty?.stringValue)
                            ? animationIdProperty.stringValue.Trim()
                            : $"动画 {entryIndex + 1}";

                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            entryProperty.isExpanded = EditorGUILayout.Foldout(entryProperty.isExpanded, animationLabel, true);
                            if (!entryProperty.isExpanded)
                                continue;

                            using (new EditorGUI.DisabledScope(true))
                                EditorGUILayout.PropertyField(animationIdProperty);
                            EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative(nameof(CharacterAnimationEntry.clip)));
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
