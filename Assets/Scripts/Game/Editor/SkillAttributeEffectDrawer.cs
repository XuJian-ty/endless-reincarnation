using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(SkillAttributeEffect))]
    public class SkillAttributeEffectDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                int oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var statField = property.FindPropertyRelative("statField");
                y = DrawProperty(y, position, statField);
                y = DrawProperty(y, position, property.FindPropertyRelative("targetMode"));
                y = DrawProperty(y, position, property.FindPropertyRelative("magnitude"));
                y = DrawProperty(y, position, property.FindPropertyRelative("usePercent"));

                bool showDuration = ShouldShowDuration(statField);
                if (showDuration)
                    DrawProperty(y, position, property.FindPropertyRelative("duration"));
                else
                {
                    var duration = property.FindPropertyRelative("duration");
                    if (duration != null && duration.floatValue != 0f)
                        duration.floatValue = 0f;
                }

                EditorGUI.indentLevel = oldIndent;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return height;

            height += EditorGUIUtility.standardVerticalSpacing;

            var statField = property.FindPropertyRelative("statField");
            height += GetChildHeight(statField);
            height += GetChildHeight(property.FindPropertyRelative("targetMode"));
            height += GetChildHeight(property.FindPropertyRelative("magnitude"));
            height += GetChildHeight(property.FindPropertyRelative("usePercent"));

            if (ShouldShowDuration(statField))
                height += GetChildHeight(property.FindPropertyRelative("duration"));

            return height;
        }

        private static bool ShouldShowDuration(SerializedProperty statField)
        {
            if (statField == null)
                return true;

            int value = statField.enumValueIndex;
            return value != (int)SkillStatField.HP && value != (int)SkillStatField.MP;
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
