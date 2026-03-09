using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(SkillTimelineEvent))]
    public class SkillTimelineEventDrawer : PropertyDrawer
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

                y = DrawProperty(y, position, property.FindPropertyRelative("eventId"));
                y = DrawProperty(y, position, property.FindPropertyRelative("startTime"));

                var triggerMode = property.FindPropertyRelative("triggerMode");
                y = DrawProperty(y, position, triggerMode);

                bool isRepeated = triggerMode != null && triggerMode.enumValueIndex == (int)SkillEventTriggerMode.Repeated;
                if (isRepeated)
                {
                    y = DrawProperty(y, position, property.FindPropertyRelative("activeDuration"));
                    y = DrawProperty(y, position, property.FindPropertyRelative("repeatInterval"));
                }

                y = DrawProperty(y, position, property.FindPropertyRelative("damageEffects"));
                y = DrawProperty(y, position, property.FindPropertyRelative("physicsEffects"));
                y = DrawProperty(y, position, property.FindPropertyRelative("attributeEffects"));
                DrawProperty(y, position, property.FindPropertyRelative("cues"));

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
            height += GetChildHeight(property.FindPropertyRelative("eventId"));
            height += GetChildHeight(property.FindPropertyRelative("startTime"));

            var triggerMode = property.FindPropertyRelative("triggerMode");
            height += GetChildHeight(triggerMode);

            bool isRepeated = triggerMode != null && triggerMode.enumValueIndex == (int)SkillEventTriggerMode.Repeated;
            if (isRepeated)
            {
                height += GetChildHeight(property.FindPropertyRelative("activeDuration"));
                height += GetChildHeight(property.FindPropertyRelative("repeatInterval"));
            }

            height += GetChildHeight(property.FindPropertyRelative("damageEffects"));
            height += GetChildHeight(property.FindPropertyRelative("physicsEffects"));
            height += GetChildHeight(property.FindPropertyRelative("attributeEffects"));
            height += GetChildHeight(property.FindPropertyRelative("cues"));

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

        private static float GetChildHeight(SerializedProperty property)
        {
            if (property == null)
                return 0f;

            return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
