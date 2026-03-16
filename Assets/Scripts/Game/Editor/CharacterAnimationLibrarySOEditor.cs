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

            EditorGUILayout.PropertyField(_groupsProperty, new GUIContent("动画分组"), false);
            if (_groupsProperty.isExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < _groupsProperty.arraySize; i++)
                {
                    SerializedProperty groupProperty = _groupsProperty.GetArrayElementAtIndex(i);
                    if (groupProperty == null)
                        continue;

                    SerializedProperty groupNameProperty = groupProperty.FindPropertyRelative(nameof(CharacterAnimationGroupDefinition.groupName));
                    string groupLabel = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                        ? groupNameProperty.stringValue.Trim()
                        : $"分组{i + 1}";

                    groupProperty.isExpanded = EditorGUILayout.Foldout(groupProperty.isExpanded, groupLabel, true);
                    if (!groupProperty.isExpanded)
                        continue;

                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(groupProperty.FindPropertyRelative(nameof(CharacterAnimationGroupDefinition.groupId)));
                    EditorGUILayout.PropertyField(groupNameProperty);
                    EditorGUILayout.PropertyField(groupProperty.FindPropertyRelative(nameof(CharacterAnimationGroupDefinition.entries)), true);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(2f);
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
