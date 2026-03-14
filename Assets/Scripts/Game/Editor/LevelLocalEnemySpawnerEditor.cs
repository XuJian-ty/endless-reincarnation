using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(LevelLocalEnemySpawner))]
    public class LevelLocalEnemySpawnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty planLibrary = serializedObject.FindProperty("_planLibrary");
            SerializedProperty planIndex = serializedObject.FindProperty("_planIndex");

            EditorGUILayout.PropertyField(planLibrary, new GUIContent("局部方案库"));
            DrawPlanSelector(planIndex, planLibrary.objectReferenceValue as LevelLocalEnemySpawnPlanSO);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawPlanSelector(SerializedProperty planIndexProperty, LevelLocalEnemySpawnPlanSO library)
        {
            if (planIndexProperty == null)
                return;

            if (library == null || library.plans == null || library.plans.Count == 0)
            {
                EditorGUILayout.HelpBox("请先指定局部敌人生成方案库并至少创建一条局部生成方案。", MessageType.Info);
                EditorGUILayout.PropertyField(planIndexProperty, new GUIContent("局部方案列表项"));
                return;
            }

            List<string> labels = BuildPlanLabels(library);
            int selectedIndex = Mathf.Clamp(planIndexProperty.intValue, 0, labels.Count - 1);
            selectedIndex = EditorGUILayout.Popup("局部方案列表项", selectedIndex, labels.ToArray());
            planIndexProperty.intValue = selectedIndex;
        }

        private static List<string> BuildPlanLabels(LevelLocalEnemySpawnPlanSO library)
        {
            var labels = new List<string>(library.plans.Count);
            for (int i = 0; i < library.plans.Count; i++)
            {
                LevelLocalEnemySpawnPlanDefinition plan = library.plans[i];
                if (plan == null)
                {
                    labels.Add($"[{i}] <空>");
                    continue;
                }

                string modeText = plan.spawnMode == LocalEnemySpawnMode.SpawnOnce ? "一次" : "重复";
                string countText = plan.spawnMode == LocalEnemySpawnMode.SpawnOnce
                    ? "任务1次"
                    : (plan.taskTotalMode == LocalEnemySpawnTaskTotalMode.Infinite ? "无限" : $"任务{Mathf.Max(1, plan.totalTaskCount)}次");
                labels.Add($"[{i}] 列表项{plan.taskIndex} | {modeText} | {countText}");
            }

            return labels;
        }
    }
}
