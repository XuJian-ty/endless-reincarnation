using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomEditor(typeof(SharedSkillDatabaseSO))]
    public class SharedSkillDatabaseSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _groupsProperty;

        private void OnEnable()
        {
            _groupsProperty = serializedObject.FindProperty("groups");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawToolbar();
            EditorGUILayout.Space(6f);

            if (_groupsProperty == null)
            {
                EditorGUILayout.HelpBox("未找到技能分组数据。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("技能分组由玩家/敌人作者化流程同步维护，这里只编辑技能内容，不支持手工新增或删除分组/列表项。", MessageType.Info);
                DrawGroups(_groupsProperty);
            }

            serializedObject.ApplyModifiedProperties();
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

        private static void DrawGroups(SerializedProperty groupsProperty)
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
                SerializedProperty entriesProperty = groupProperty.FindPropertyRelative("entries");

                string groupLabel = !string.IsNullOrWhiteSpace(groupNameProperty?.stringValue)
                    ? groupNameProperty.stringValue
                    : $"分组 {groupIndex}";

                using (new EditorGUILayout.VerticalScope("box"))
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
                            EditorGUILayout.IntField("技能数量", entriesProperty.arraySize);
                    }
                    EditorGUILayout.Space(4f);

                    DrawEntries(entriesProperty);
                }
            }
        }

        private static void DrawEntries(SerializedProperty entriesProperty)
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
                    : $"技能 {entryIndex}";

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    entryProperty.isExpanded = EditorGUILayout.Foldout(entryProperty.isExpanded, skillLabel, true);
                    if (!entryProperty.isExpanded)
                        continue;

                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(skillIdProperty, new GUIContent("技能ID"));
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("ignoreAnimationDamageEvents"));
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("damageEvents"), new GUIContent("命中事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("physicsEvents"), new GUIContent("物理事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("attributeEvents"), new GUIContent("属性事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("vfxEvents"), new GUIContent("特效事件列表"), true);
                    EditorGUILayout.PropertyField(entryProperty.FindPropertyRelative("sfxEvents"), new GUIContent("音效事件列表"), true);
                }
            }
        }
    }
}
