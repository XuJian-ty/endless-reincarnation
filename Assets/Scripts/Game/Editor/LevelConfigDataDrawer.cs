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
            int levelSlotIndex = ResolveLevelSlotIndex(property);
            int displayLevelIndex = Mathf.Clamp(levelSlotIndex + 1, 1, 999);

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, new GUIContent($"关卡 {displayLevelIndex}"), true);

            if (property.isExpanded)
            {
                int oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                SerializedProperty levelIndex = property.FindPropertyRelative("levelIndex");
                SerializedProperty sceneName = property.FindPropertyRelative("sceneName");
                SerializedProperty sceneGuid = property.FindPropertyRelative("sceneGuid");
                SerializedProperty library = property.FindPropertyRelative("globalSpawnPlanLibrary");
                SerializedProperty planIndex = property.FindPropertyRelative("globalSpawnPlanIndex");
                SerializedProperty chestMinCount = property.FindPropertyRelative("chestMinCount");
                SerializedProperty chestMaxCount = property.FindPropertyRelative("chestMaxCount");
                SerializedProperty shopCount = property.FindPropertyRelative("shopCount");
                SerializedProperty cellSize = property.FindPropertyRelative("cellSize");
                SerializedProperty eliteDropEntries = property.FindPropertyRelative("eliteDropEntries");
                SerializedProperty guardianDropEntries = property.FindPropertyRelative("guardianDropEntries");
                SerializedProperty bossDropEntries = property.FindPropertyRelative("bossDropEntries");

                if (levelIndex != null && levelIndex.intValue != displayLevelIndex)
                    levelIndex.intValue = displayLevelIndex;

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawReadOnlyLevelIndex(y, position, displayLevelIndex);
                y = DrawSceneSelector(y, position, sceneName, sceneGuid, displayLevelIndex);
                y = DrawProperty(y, position, library);
                y = DrawPlanSelector(y, position, planIndex, library.objectReferenceValue as LevelEnemySpawnPlanSO);
                y = DrawProperty(y, position, chestMinCount);
                y = DrawProperty(y, position, chestMaxCount);
                y = DrawProperty(y, position, shopCount);
                y = DrawProperty(y, position, cellSize);
                y = DrawProperty(y, position, eliteDropEntries);
                y = DrawProperty(y, position, guardianDropEntries);
                DrawProperty(y, position, bossDropEntries);

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
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property.FindPropertyRelative("globalSpawnPlanLibrary"));
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property.FindPropertyRelative("chestMinCount"));
            height += GetChildHeight(property.FindPropertyRelative("chestMaxCount"));
            height += GetChildHeight(property.FindPropertyRelative("shopCount"));
            height += GetChildHeight(property.FindPropertyRelative("cellSize"));
            height += GetChildHeight(property.FindPropertyRelative("eliteDropEntries"));
            height += GetChildHeight(property.FindPropertyRelative("guardianDropEntries"));
            height += GetChildHeight(property.FindPropertyRelative("bossDropEntries"));
            return height;
        }

        private static int ResolveLevelSlotIndex(SerializedProperty property)
        {
            if (property == null || string.IsNullOrWhiteSpace(property.propertyPath))
                return 0;

            string path = property.propertyPath;
            int dataIndex = path.IndexOf(".Array.data[", System.StringComparison.Ordinal);
            if (dataIndex < 0)
                dataIndex = path.IndexOf("levels.Array.data[", System.StringComparison.Ordinal);
            if (dataIndex < 0)
                return 0;

            int bracketStart = path.IndexOf('[', dataIndex);
            int bracketEnd = path.IndexOf(']', bracketStart + 1);
            if (bracketStart < 0 || bracketEnd <= bracketStart)
                return 0;

            string rawIndex = path.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            return int.TryParse(rawIndex, out int index) ? Mathf.Max(0, index) : 0;
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
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUI.Popup(rect, "全局方案列表项", selectedIndex, labels.ToArray());
            if (EditorGUI.EndChangeCheck())
                planIndexProperty.intValue = nextIndex;
            return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float DrawSceneSelector(
            float y,
            Rect totalRect,
            SerializedProperty sceneNameProperty,
            SerializedProperty sceneGuidProperty,
            int levelIndex)
        {
            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            SceneAsset currentScene = ResolveSceneAsset(sceneGuidProperty, sceneNameProperty);

            EditorGUI.BeginChangeCheck();
            SceneAsset nextScene = (SceneAsset)EditorGUI.ObjectField(rect, "关卡场景", currentScene, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (nextScene == null)
                {
                    sceneGuidProperty.stringValue = string.Empty;
                    sceneNameProperty.stringValue = string.Empty;
                }
                else
                {
                    string scenePath = AssetDatabase.GetAssetPath(nextScene);
                    sceneGuidProperty.stringValue = AssetDatabase.AssetPathToGUID(scenePath);
                    sceneNameProperty.stringValue = nextScene.name;
                }
            }

            if (nextScene == null && string.IsNullOrWhiteSpace(sceneNameProperty.stringValue))
                sceneNameProperty.stringValue = $"Level_{levelIndex}";

            return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private static SceneAsset ResolveSceneAsset(SerializedProperty sceneGuidProperty, SerializedProperty sceneNameProperty)
        {
            if (sceneGuidProperty != null && !string.IsNullOrWhiteSpace(sceneGuidProperty.stringValue))
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuidProperty.stringValue);
                if (!string.IsNullOrWhiteSpace(scenePath))
                {
                    SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                    if (scene != null)
                        return scene;
                }
            }

            if (sceneNameProperty != null && !string.IsNullOrWhiteSpace(sceneNameProperty.stringValue))
            {
                string[] sceneGuids = AssetDatabase.FindAssets($"{sceneNameProperty.stringValue} t:Scene");
                for (int i = 0; i < sceneGuids.Length; i++)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                    if (scene != null && string.Equals(scene.name, sceneNameProperty.stringValue, System.StringComparison.OrdinalIgnoreCase))
                        return scene;
                }
            }

            return null;
        }

        private static float DrawReadOnlyLevelIndex(float y, Rect totalRect, int levelIndex)
        {
            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.IntField(rect, "关卡编号", levelIndex);
            }

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
