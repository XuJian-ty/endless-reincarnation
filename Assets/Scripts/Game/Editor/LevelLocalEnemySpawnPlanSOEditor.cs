using System.Collections.Generic;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(LevelLocalEnemySpawnPlanSO))]
    public class LevelLocalEnemySpawnPlanSOEditor : UnityEditor.Editor
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
            EditorGUILayout.LabelField("局部生成方案列表", EditorStyles.boldLabel);

            if (database == null || database.tasks == null || database.tasks.Count == 0)
            {
                EditorGUILayout.HelpBox("请先指定任务库并至少创建一条具体类型生成数据。", MessageType.Info);
                EditorGUILayout.PropertyField(plansProperty, true);
                return;
            }

            List<string> labels = BuildTaskLabels(database);
            for (int i = 0; i < plansProperty.arraySize; i++)
            {
                SerializedProperty plan = plansProperty.GetArrayElementAtIndex(i);
                if (plan == null)
                    continue;

                SerializedProperty taskIndex = plan.FindPropertyRelative("taskIndex");
                SerializedProperty spawnMode = plan.FindPropertyRelative("spawnMode");
                SerializedProperty taskTotalMode = plan.FindPropertyRelative("taskTotalMode");
                SerializedProperty totalTaskCount = plan.FindPropertyRelative("totalTaskCount");
                SerializedProperty taskInterval = plan.FindPropertyRelative("taskInterval");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"局部生成方案 {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("删除", GUILayout.Width(64f)))
                {
                    plansProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                int selectedIndex = Mathf.Clamp(taskIndex.intValue, 0, labels.Count - 1);
                selectedIndex = EditorGUILayout.Popup("列表项", selectedIndex, labels.ToArray());
                taskIndex.intValue = selectedIndex;

                DrawSpawnModePopup(spawnMode);
                LocalEnemySpawnMode mode = (LocalEnemySpawnMode)spawnMode.enumValueIndex;
                if (mode == LocalEnemySpawnMode.SpawnRepeatedly)
                {
                    DrawTaskTotalModePopup(taskTotalMode);
                    if ((LocalEnemySpawnTaskTotalMode)taskTotalMode.enumValueIndex == LocalEnemySpawnTaskTotalMode.FixedCount)
                        EditorGUILayout.PropertyField(totalTaskCount, new GUIContent("生成任务总数"));

                    EditorGUILayout.PropertyField(taskInterval, new GUIContent("任务间隔(秒)"));
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("添加局部生成方案"))
            {
                int index = plansProperty.arraySize;
                plansProperty.InsertArrayElementAtIndex(index);
                SerializedProperty newPlan = plansProperty.GetArrayElementAtIndex(index);
                if (newPlan != null)
                {
                    SerializedProperty taskIndex = newPlan.FindPropertyRelative("taskIndex");
                    SerializedProperty spawnMode = newPlan.FindPropertyRelative("spawnMode");
                    SerializedProperty taskTotalMode = newPlan.FindPropertyRelative("taskTotalMode");
                    SerializedProperty totalTaskCount = newPlan.FindPropertyRelative("totalTaskCount");
                    SerializedProperty taskInterval = newPlan.FindPropertyRelative("taskInterval");
                    if (taskIndex != null)
                        taskIndex.intValue = 0;
                    if (spawnMode != null)
                        spawnMode.enumValueIndex = (int)LocalEnemySpawnMode.SpawnOnce;
                    if (taskTotalMode != null)
                        taskTotalMode.enumValueIndex = (int)LocalEnemySpawnTaskTotalMode.FixedCount;
                    if (totalTaskCount != null)
                        totalTaskCount.intValue = 1;
                    if (taskInterval != null)
                        taskInterval.floatValue = 8f;
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
                string variantText = ResolveVariantText(task);
                labels.Add($"[{i}] {GetSpawnTypeText(task.spawnType)} | {variantText} | 半径{task.spawnRadius:0.##} | {countText}");
            }

            return labels;
        }

        private static string GetSpawnTypeText(EnemySpawnCategory type)
        {
            return type switch
            {
                EnemySpawnCategory.Minion => "小怪",
                EnemySpawnCategory.MinionLegacyRanged => "小怪",
                EnemySpawnCategory.Elite => "精英怪",
                EnemySpawnCategory.Guardian => "守卫者",
                EnemySpawnCategory.Boss => "Boss",
                EnemySpawnCategory.Chest => "宝箱",
                EnemySpawnCategory.Shop => "商店",
                _ => type.ToString(),
            };
        }

        private static string ResolveVariantText(EnemySpawnTaskDefinition task)
        {
            if (task == null)
                return "混合随机";

            if (!task.IsEnemyCategory)
                return task.UseMixedVariants ? "默认" : task.specificSpawnId.Trim();

            if (task.UseMixedVariants)
                return "混合随机";

            EnemyStatsDatabaseSO statsDb = Resources.Load<EnemyStatsDatabaseSO>("配置/敌人属性库");
            if (statsDb?.entries != null)
            {
                for (int i = 0; i < statsDb.entries.Count; i++)
                {
                    EnemyStatsEntry entry = statsDb.entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId))
                        continue;

                    if (string.Equals(entry.enemyId.Trim(), task.specificSpawnId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                        return string.IsNullOrWhiteSpace(entry.displayName) ? entry.enemyId.Trim() : entry.displayName.Trim();
                }
            }

            return task.specificSpawnId.Trim();
        }

        private static void DrawSpawnModePopup(SerializedProperty property)
        {
            if (property == null)
                return;

            int selected = Mathf.Clamp(property.enumValueIndex, 0, 1);
            selected = EditorGUILayout.Popup("生成模式", selected, new[] { "只生成一次对象", "随时间不断生成对象" });
            property.enumValueIndex = selected;
        }

        private static void DrawTaskTotalModePopup(SerializedProperty property)
        {
            if (property == null)
                return;

            int selected = Mathf.Clamp(property.enumValueIndex, 0, 1);
            selected = EditorGUILayout.Popup("任务总数模式", selected, new[] { "固定数量", "无限" });
            property.enumValueIndex = selected;
        }
    }
}
