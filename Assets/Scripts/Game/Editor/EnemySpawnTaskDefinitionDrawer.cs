using Game.Data;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(EnemySpawnTaskDefinition))]
    public class EnemySpawnTaskDefinitionDrawer : PropertyDrawer
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

                SerializedProperty enemyType = property.FindPropertyRelative("enemyType");
                SerializedProperty specificEnemyId = property.FindPropertyRelative("specificEnemyId");
                SerializedProperty spawnRadius = property.FindPropertyRelative("spawnRadius");
                SerializedProperty countMode = property.FindPropertyRelative("countMode");
                SerializedProperty fixedCount = property.FindPropertyRelative("fixedCount");
                SerializedProperty randomMinCount = property.FindPropertyRelative("randomMinCount");
                SerializedProperty randomMaxCount = property.FindPropertyRelative("randomMaxCount");
                SerializedProperty enemyMinSpacing = property.FindPropertyRelative("enemyMinSpacing");

                EnemySpawnCountMode mode = countMode != null
                    ? (EnemySpawnCountMode)countMode.enumValueIndex
                    : EnemySpawnCountMode.Fixed;

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawEnemyTypePopup(y, position, enemyType);
                y = DrawSpecificEnemyPopup(y, position, enemyType, specificEnemyId);
                y = DrawProperty(y, position, spawnRadius);
                y = DrawCountModePopup(y, position, countMode);

                if (mode == EnemySpawnCountMode.Fixed)
                {
                    y = DrawProperty(y, position, fixedCount);
                }
                else
                {
                    y = DrawProperty(y, position, randomMinCount);
                    y = DrawProperty(y, position, randomMaxCount);
                }

                DrawProperty(y, position, enemyMinSpacing);
                EditorGUI.indentLevel = oldIndent;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property == null || !property.isExpanded)
                return height;

            SerializedProperty countMode = property.FindPropertyRelative("countMode");
            EnemySpawnCountMode mode = countMode != null
                ? (EnemySpawnCountMode)countMode.enumValueIndex
                : EnemySpawnCountMode.Fixed;

            height += EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property.FindPropertyRelative("spawnRadius"));
            height += GetChildHeight(countMode);

            if (mode == EnemySpawnCountMode.Fixed)
            {
                height += GetChildHeight(property.FindPropertyRelative("fixedCount"));
            }
            else
            {
                height += GetChildHeight(property.FindPropertyRelative("randomMinCount"));
                height += GetChildHeight(property.FindPropertyRelative("randomMaxCount"));
            }

            height += GetChildHeight(property.FindPropertyRelative("enemyMinSpacing"));
            return height;
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

        private static float DrawCountModePopup(float y, Rect totalRect, SerializedProperty property)
        {
            if (property == null)
                return y;

            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            int selected = Mathf.Clamp(property.enumValueIndex, 0, 1);
            selected = EditorGUI.Popup(rect, "数量模式", selected, new[] { "固定数量", "随机数量" });
            property.enumValueIndex = selected;
            return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float DrawEnemyTypePopup(float y, Rect totalRect, SerializedProperty property)
        {
            if (property == null)
                return y;

            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            int selected = Mathf.Clamp(property.enumValueIndex, 0, 4);
            selected = EditorGUI.Popup(rect, "敌人类型", GetCategoryPopupIndex(selected), new[] { "小怪", "精英怪", "守卫者", "Boss" });
            property.enumValueIndex = selected switch
            {
                0 => (int)EnemySpawnCategory.Minion,
                1 => (int)EnemySpawnCategory.Elite,
                2 => (int)EnemySpawnCategory.Guardian,
                3 => (int)EnemySpawnCategory.Boss,
                _ => (int)EnemySpawnCategory.Minion,
            };
            return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float DrawSpecificEnemyPopup(float y, Rect totalRect, SerializedProperty enemyTypeProperty, SerializedProperty specificEnemyIdProperty)
        {
            if (enemyTypeProperty == null || specificEnemyIdProperty == null)
                return y;

            EnemySpawnCategory category = (EnemySpawnCategory)enemyTypeProperty.enumValueIndex;
            List<string> optionLabels = new List<string> { "混合随机" };
            List<string> optionIds = new List<string> { string.Empty };

            EnemyStatsDatabaseSO statsDb = Resources.Load<EnemyStatsDatabaseSO>("配置/敌人属性库");
            if (statsDb?.entries != null)
            {
                for (int i = 0; i < statsDb.entries.Count; i++)
                {
                    EnemyStatsEntry entry = statsDb.entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId) || !MatchesCategory(category, entry.type))
                        continue;

                    optionIds.Add(entry.enemyId.Trim());
                    optionLabels.Add(string.IsNullOrWhiteSpace(entry.displayName) ? entry.enemyId.Trim() : entry.displayName.Trim());
                }
            }

            string currentId = string.IsNullOrWhiteSpace(specificEnemyIdProperty.stringValue)
                ? string.Empty
                : specificEnemyIdProperty.stringValue.Trim();
            int selectedIndex = 0;
            for (int i = 1; i < optionIds.Count; i++)
            {
                if (string.Equals(optionIds[i], currentId, System.StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }

            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            selectedIndex = EditorGUI.Popup(rect, "具体类型", selectedIndex, optionLabels.ToArray());
            specificEnemyIdProperty.stringValue = optionIds[selectedIndex];
            return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private static int GetCategoryPopupIndex(int enumValueIndex)
        {
            EnemySpawnCategory category = (EnemySpawnCategory)enumValueIndex;
            return category switch
            {
                EnemySpawnCategory.Elite => 1,
                EnemySpawnCategory.Guardian => 2,
                EnemySpawnCategory.Boss => 3,
                _ => 0,
            };
        }

        private static bool MatchesCategory(EnemySpawnCategory category, EnemyType type)
        {
            return category switch
            {
                EnemySpawnCategory.Minion => type == EnemyType.MeleeMinion || type == EnemyType.RangedMinion,
                EnemySpawnCategory.MinionLegacyRanged => type == EnemyType.MeleeMinion || type == EnemyType.RangedMinion,
                EnemySpawnCategory.Elite => type == EnemyType.Elite,
                EnemySpawnCategory.Guardian => type == EnemyType.Guardian,
                EnemySpawnCategory.Boss => type == EnemyType.Boss,
                _ => false,
            };
        }

        private static float GetChildHeight(SerializedProperty property)
        {
            if (property == null)
                return 0f;

            return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
