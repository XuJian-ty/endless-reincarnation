using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(SkillPhysicsEffect))]
    public class SkillPhysicsEffectDrawer : PropertyDrawer
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

                SerializedProperty effectType = property.FindPropertyRelative("effectType");
                SerializedProperty targetMode = property.FindPropertyRelative("targetMode");
                SerializedProperty direction = property.FindPropertyRelative("direction");
                SerializedProperty magnitude = property.FindPropertyRelative("magnitude");
                SerializedProperty duration = property.FindPropertyRelative("duration");

                PhysicsEffectType effectTypeValue = (PhysicsEffectType)effectType.enumValueIndex;
                bool forceSelfTarget = effectTypeValue == PhysicsEffectType.DashSelf || effectTypeValue == PhysicsEffectType.Airborne;
                bool forceUpDirection = effectTypeValue == PhysicsEffectType.Airborne;

                if (forceSelfTarget && targetMode != null)
                    targetMode.enumValueIndex = (int)SkillTargetMode.Self;
                if (forceUpDirection && direction != null)
                    direction.enumValueIndex = (int)SkillEffectDirection.Up;

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawProperty(y, position, effectType);

                if (!forceSelfTarget)
                    y = DrawProperty(y, position, targetMode);

                if (!forceUpDirection)
                    y = DrawProperty(y, position, direction);

                y = DrawProperty(y, position, magnitude);
                DrawProperty(y, position, duration);

                EditorGUI.indentLevel = oldIndent;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property == null || !property.isExpanded)
                return height;

            SerializedProperty effectType = property.FindPropertyRelative("effectType");
            PhysicsEffectType effectTypeValue = effectType != null
                ? (PhysicsEffectType)effectType.enumValueIndex
                : PhysicsEffectType.Knockback;

            bool forceSelfTarget = effectTypeValue == PhysicsEffectType.DashSelf || effectTypeValue == PhysicsEffectType.Airborne;
            bool forceUpDirection = effectTypeValue == PhysicsEffectType.Airborne;

            height += EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(effectType);

            if (!forceSelfTarget)
                height += GetChildHeight(property.FindPropertyRelative("targetMode"));

            if (!forceUpDirection)
                height += GetChildHeight(property.FindPropertyRelative("direction"));

            height += GetChildHeight(property.FindPropertyRelative("magnitude"));
            height += GetChildHeight(property.FindPropertyRelative("duration"));
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
