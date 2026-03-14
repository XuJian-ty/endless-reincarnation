using System.Collections.Generic;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(LevelEnemySpawnPlanSO))]
    public class LevelEnemySpawnPlanSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty taskDatabase = serializedObject.FindProperty("taskDatabase");
            SerializedProperty plans = serializedObject.FindProperty("plans");

            EditorGUILayout.PropertyField(taskDatabase, new GUIContent("任务库"));
            DrawPlans(plans, taskDatabase.objectReferenceValue as EnemySpawnTaskDatabaseSO);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawPlans(SerializedProperty plansProperty, EnemySpawnTaskDatabaseSO database)
        {
            if (plansProperty == null)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("全局生成方案列表", EditorStyles.boldLabel);

            if (database == null || database.tasks == null || database.tasks.Count == 0)
            {
                EditorGUILayout.HelpBox("请先指定任务库并至少创建一条具体类型敌人生成数据。", MessageType.Info);
                EditorGUILayout.PropertyField(plansProperty, true);
                return;
            }

            List<string> labels = BuildTaskLabels(database);
            for (int i = 0; i < plansProperty.arraySize; i++)
            {
                SerializedProperty plan = plansProperty.GetArrayElementAtIndex(i);
                if (plan == null)
                    continue;

                SerializedProperty assignments = plan.FindPropertyRelative("taskAssignments");
                SerializedProperty spawnMode = plan.FindPropertyRelative("spawnMode");
                SerializedProperty taskCenterMinSpacing = plan.FindPropertyRelative("taskCenterMinSpacing");
                SerializedProperty initialTaskCount = plan.FindPropertyRelative("initialTaskCount");
                SerializedProperty taskInterval = plan.FindPropertyRelative("taskInterval");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"全局生成方案 {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("删除", GUILayout.Width(64f)))
                {
                    plansProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                DrawTaskAssignments(assignments, labels);
                DrawSpawnModePopup(spawnMode);

                GlobalEnemySpawnMode mode = (GlobalEnemySpawnMode)spawnMode.enumValueIndex;
                if (mode == GlobalEnemySpawnMode.SpawnAllAtOnce)
                {
                    EditorGUILayout.PropertyField(taskCenterMinSpacing);
                }
                else
                {
                    EditorGUILayout.PropertyField(initialTaskCount, new GUIContent("初始任务数量"));
                    EditorGUILayout.PropertyField(taskInterval, new GUIContent("任务间隔(秒)"));
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("添加全局生成方案"))
            {
                int index = plansProperty.arraySize;
                plansProperty.InsertArrayElementAtIndex(index);
                SerializedProperty newPlan = plansProperty.GetArrayElementAtIndex(index);
                if (newPlan != null)
                {
                    SerializedProperty assignments = newPlan.FindPropertyRelative("taskAssignments");
                    SerializedProperty spawnMode = newPlan.FindPropertyRelative("spawnMode");
                    SerializedProperty taskCenterMinSpacing = newPlan.FindPropertyRelative("taskCenterMinSpacing");
                    SerializedProperty initialTaskCount = newPlan.FindPropertyRelative("initialTaskCount");
                    SerializedProperty taskInterval = newPlan.FindPropertyRelative("taskInterval");
                    if (assignments != null)
                        assignments.ClearArray();
                    if (spawnMode != null)
                        spawnMode.enumValueIndex = (int)GlobalEnemySpawnMode.SpawnAllAtOnce;
                    if (taskCenterMinSpacing != null)
                        taskCenterMinSpacing.floatValue = 16f;
                    if (initialTaskCount != null)
                        initialTaskCount.intValue = 3;
                    if (taskInterval != null)
                        taskInterval.floatValue = 8f;
                }
            }
        }

        private static void DrawTaskAssignments(SerializedProperty assignmentsProperty, List<string> labels)
        {
            if (assignmentsProperty == null)
                return;

            EditorGUILayout.LabelField("任务分配列表", EditorStyles.boldLabel);
            for (int i = 0; i < assignmentsProperty.arraySize; i++)
            {
                SerializedProperty item = assignmentsProperty.GetArrayElementAtIndex(i);
                if (item == null)
                    continue;

                SerializedProperty taskIndex = item.FindPropertyRelative("taskIndex");
                SerializedProperty taskCount = item.FindPropertyRelative("taskCount");

                EditorGUILayout.BeginHorizontal();
                int selectedIndex = Mathf.Clamp(taskIndex.intValue, 0, labels.Count - 1);
                selectedIndex = EditorGUILayout.Popup("列表项", selectedIndex, labels.ToArray());
                taskIndex.intValue = selectedIndex;
                if (GUILayout.Button("删除", GUILayout.Width(64f)))
                {
                    assignmentsProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(taskCount, new GUIContent("生成任务数量"));
            }

            if (GUILayout.Button("添加任务分配"))
            {
                int index = assignmentsProperty.arraySize;
                assignmentsProperty.InsertArrayElementAtIndex(index);
                SerializedProperty newItem = assignmentsProperty.GetArrayElementAtIndex(index);
                if (newItem != null)
                {
                    SerializedProperty taskIndex = newItem.FindPropertyRelative("taskIndex");
                    SerializedProperty taskCount = newItem.FindPropertyRelative("taskCount");
                    if (taskIndex != null)
                        taskIndex.intValue = 0;
                    if (taskCount != null)
                        taskCount.intValue = 1;
                }
            }
        }

        private static List<string> BuildTaskLabels(EnemySpawnTaskDatabaseSO database)
        {
            var labels = new List<string>(database.tasks.Count);
            for (int i = 0; i < database.tasks.Count; i++)
            {
                EnemySpawnTaskDefinition task = database.tasks[i];
                if (task == null)
                {
                    labels.Add($"[{i}] <空>");
                    continue;
                }

                string countText = task.countMode == EnemySpawnCountMode.Fixed
                    ? $"固定{Mathf.Max(0, task.fixedCount)}"
                    : $"随机{Mathf.Max(0, task.randomMinCount)}~{Mathf.Max(task.randomMinCount, task.randomMaxCount)}";
                labels.Add($"[{i}] {task.enemyType} | 半径{task.spawnRadius:0.##} | {countText}");
            }

            return labels;
        }

        private static void DrawSpawnModePopup(SerializedProperty property)
        {
            if (property == null)
                return;

            int selected = Mathf.Clamp(property.enumValueIndex, 0, 1);
            selected = EditorGUILayout.Popup("生成模式", selected, new[] { "一次性生成所有敌人", "随时间逐渐生成敌人" });
            property.enumValueIndex = selected;
        }
    }
}
