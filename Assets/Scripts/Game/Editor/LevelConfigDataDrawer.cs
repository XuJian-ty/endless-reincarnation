using System.Collections.Generic;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(LevelConfigData))]
    public class LevelConfigDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null)
                return;

            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                int oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                SerializedProperty levelIndex = property.FindPropertyRelative("levelIndex");
                SerializedProperty library = property.FindPropertyRelative("enemyGlobalSpawnPlanLibrary");
                SerializedProperty planIndex = property.FindPropertyRelative("enemyGlobalSpawnPlanIndex");
                SerializedProperty chestMinCount = property.FindPropertyRelative("chestMinCount");
                SerializedProperty chestMaxCount = property.FindPropertyRelative("chestMaxCount");
                SerializedProperty shopCount = property.FindPropertyRelative("shopCount");
                SerializedProperty cellSize = property.FindPropertyRelative("cellSize");

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawProperty(y, position, levelIndex);
                y = DrawProperty(y, position, library);
                y = DrawPlanSelector(y, position, planIndex, library.objectReferenceValue as LevelEnemySpawnPlanSO);
                y = DrawProperty(y, position, chestMinCount);
                y = DrawProperty(y, position, chestMaxCount);
                y = DrawProperty(y, position, shopCount);
                DrawProperty(y, position, cellSize);

                EditorGUI.indentLevel = oldIndent;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property == null || !property.isExpanded)
                return height;

            height += EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property.FindPropertyRelative("levelIndex"));
            height += GetChildHeight(property.FindPropertyRelative("enemyGlobalSpawnPlanLibrary"));
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property.FindPropertyRelative("chestMinCount"));
            height += GetChildHeight(property.FindPropertyRelative("chestMaxCount"));
            height += GetChildHeight(property.FindPropertyRelative("shopCount"));
            height += GetChildHeight(property.FindPropertyRelative("cellSize"));
            return height;
        }

        private static float DrawPlanSelector(float y, Rect totalRect, SerializedProperty planIndexProperty, LevelEnemySpawnPlanSO library)
        {
            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            if (planIndexProperty == null)
                return y;

            if (library == null || library.plans == null || library.plans.Count == 0)
            {
                EditorGUI.PropertyField(rect, planIndexProperty, new GUIContent("全局方案列表项"));
                return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            List<string> labels = BuildPlanLabels(library);
            int selectedIndex = Mathf.Clamp(planIndexProperty.intValue, 0, labels.Count - 1);
            selectedIndex = EditorGUI.Popup(rect, "全局方案列表项", selectedIndex, labels.ToArray());
            planIndexProperty.intValue = selectedIndex;
            return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
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

        private static float DrawProperty(float y, Rect totalRect, SerializedProperty property)
        {
            if (property == null)
                return y;

            float height = EditorGUI.GetPropertyHeight(property, true);
            Rect rect = new Rect(totalRect.x, y, totalRect.width, height);
            EditorGUI.PropertyField(rect, property, true);
            return y + height + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float GetChildHeight(SerializedProperty property)
        {
            if (property == null)
                return 0f;

            return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
