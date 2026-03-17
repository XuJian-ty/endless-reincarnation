using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(LevelEnemySpawner))]
    public class LevelEnemySpawnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty planLibrary = serializedObject.FindProperty("_planLibrary");
            SerializedProperty planIndex = serializedObject.FindProperty("_planIndex");

            EditorGUILayout.PropertyField(planLibrary, new GUIContent("全局方案库"));
            DrawPlanSelector(planIndex, planLibrary.objectReferenceValue as LevelEnemySpawnPlanSO);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawPlanSelector(SerializedProperty planIndexProperty, LevelEnemySpawnPlanSO library)
        {
            if (planIndexProperty == null)
                return;

            if (library == null || library.plans == null || library.plans.Count == 0)
            {
                EditorGUILayout.HelpBox("请先指定关卡全局生成方案库并至少创建一条全局生成方案。", MessageType.Info);
                EditorGUILayout.PropertyField(planIndexProperty, new GUIContent("全局方案列表项"));
                return;
            }

            List<string> labels = BuildPlanLabels(library);
            int selectedIndex = Mathf.Clamp(planIndexProperty.intValue, 0, labels.Count - 1);
            selectedIndex = EditorGUILayout.Popup("全局方案列表项", selectedIndex, labels.ToArray());
            planIndexProperty.intValue = selectedIndex;
        }

        private static List<string> BuildPlanLabels(LevelEnemySpawnPlanSO library)
        {
            var labels = new List<string>(library.plans.Count);
            for (int i = 0; i < library.plans.Count; i++)
            {
                LevelEnemyGlobalSpawnPlanDefinition plan = library.plans[i];
                if (plan == null)
                {
                    labels.Add($"[{i}] <空>");
                    continue;
                }

                int assignmentCount = plan.taskAssignments != null ? plan.taskAssignments.Count : 0;
                string modeText = plan.spawnMode == GlobalEnemySpawnMode.SpawnAllAtOnce ? "一次性" : "逐步";
                labels.Add($"[{i}] {modeText} | 任务分配{assignmentCount}项");
            }

            return labels;
        }
    }
}
