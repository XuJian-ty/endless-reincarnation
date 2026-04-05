using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(SkillAttributeEffect))]
    public class SkillAttributeEffectDrawer : PropertyDrawer
    {
        private static readonly string[] TargetLabels =
        {
            "自身",
            "命中目标",
        };

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
                var targetMode = property.FindPropertyRelative("targetMode");
                bool isOnHit = IsOnHitContext(property);
                y = DrawProperty(y, position, statField);
                y = DrawTargetMode(y, position, targetMode, isOnHit);
                if (isOnHit)
                    y = DrawProperty(y, position, property.FindPropertyRelative("onHitTriggerDelay"));
                y = DrawProperty(y, position, property.FindPropertyRelative("magnitude"));
                y = DrawProperty(y, position, property.FindPropertyRelative("usePercent"));

                bool showDuration = ShouldShowDuration(statField);
                if (showDuration)
                {
                    var durationMode = property.FindPropertyRelative("durationMode");
                    if (!isOnHit)
                        y = DrawProperty(y, position, durationMode);
                    else if (durationMode != null && durationMode.enumValueIndex != (int)SkillEffectDurationMode.FixedTime)
                        durationMode.enumValueIndex = (int)SkillEffectDurationMode.FixedTime;

                    if (durationMode == null || durationMode.enumValueIndex == (int)SkillEffectDurationMode.FixedTime)
                        DrawProperty(y, position, property.FindPropertyRelative("duration"));
                }
                else
                {
                    var durationMode = property.FindPropertyRelative("durationMode");
                    if (durationMode != null && durationMode.enumValueIndex != (int)SkillEffectDurationMode.FixedTime)
                        durationMode.enumValueIndex = (int)SkillEffectDurationMode.FixedTime;
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
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            if (IsOnHitContext(property))
                height += GetChildHeight(property.FindPropertyRelative("onHitTriggerDelay"));
            height += GetChildHeight(property.FindPropertyRelative("magnitude"));
            height += GetChildHeight(property.FindPropertyRelative("usePercent"));

            if (ShouldShowDuration(statField))
            {
                if (!IsOnHitContext(property))
                    height += GetChildHeight(property.FindPropertyRelative("durationMode"));

                SerializedProperty durationMode = property.FindPropertyRelative("durationMode");
                if (IsOnHitContext(property)
                    || durationMode == null
                    || durationMode.enumValueIndex == (int)SkillEffectDurationMode.FixedTime)
                {
                    height += GetChildHeight(property.FindPropertyRelative("duration"));
                }
            }

            return height;
        }

        private static bool ShouldShowDuration(SerializedProperty statField)
        {
            if (statField == null)
                return true;

            int value = statField.enumValueIndex;
            return value != (int)SkillStatField.HP && value != (int)SkillStatField.MP;
        }

        private static float DrawTargetMode(float y, Rect totalRect, SerializedProperty property, bool isOnHit)
        {
            if (property == null)
                return y;

            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            if (!isOnHit)
            {
                property.enumValueIndex = (int)SkillTargetMode.Self;
                EditorGUI.LabelField(rect, "作用目标", "自身");
            }
            else
            {
                int selected = property.enumValueIndex == (int)SkillTargetMode.DetectedTargets ? 1 : 0;
                selected = EditorGUI.Popup(rect, "作用目标", selected, TargetLabels);
                property.enumValueIndex = selected == 1 ? (int)SkillTargetMode.DetectedTargets : (int)SkillTargetMode.Self;
            }

            return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
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

        private static bool IsOnHitContext(SerializedProperty property)
        {
            return property.propertyPath.Contains("onHitAttributeEffects");
        }
    }
}
