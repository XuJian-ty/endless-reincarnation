using Game.Data;
using UnityEditor;
using UnityEngine;

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
                y = DrawProperty(y, position, enemyType);
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
            height += GetChildHeight(property.FindPropertyRelative("enemyType"));
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

        private static float GetChildHeight(SerializedProperty property)
        {
            if (property == null)
                return 0f;

            return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
