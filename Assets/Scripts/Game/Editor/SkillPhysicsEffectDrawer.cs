using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(SkillPhysicsEffect))]
    public class SkillPhysicsEffectDrawer : PropertyDrawer
    {
        private static readonly PhysicsEffectType[] TopLevelTypes =
        {
            PhysicsEffectType.DashSelf,
            PhysicsEffectType.Airborne,
            PhysicsEffectType.SuperArmor,
            PhysicsEffectType.Invincible,
        };

        private static readonly string[] TopLevelLabels =
        {
            "位移",
            "腾空",
            "霸体",
            "无敌",
        };

        private static readonly PhysicsEffectType[] OnHitTypes =
        {
            PhysicsEffectType.Knockback,
            PhysicsEffectType.Pull,
            PhysicsEffectType.Launch,
            PhysicsEffectType.Stun,
        };

        private static readonly string[] OnHitLabels =
        {
            "击退",
            "拉拽",
            "击飞",
            "眩晕",
        };

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
                SerializedProperty distance = property.FindPropertyRelative("distance");
                SerializedProperty heightValue = property.FindPropertyRelative("height");
                SerializedProperty durationMode = property.FindPropertyRelative("durationMode");
                SerializedProperty duration = property.FindPropertyRelative("duration");
                bool isOnHit = IsOnHitContext(property);
                PhysicsEffectType[] allowedTypes = isOnHit ? OnHitTypes : TopLevelTypes;
                string[] allowedLabels = isOnHit ? OnHitLabels : TopLevelLabels;
                PhysicsEffectType effectTypeValue = CoerceEffectType(effectType, allowedTypes[0], allowedTypes);

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawEffectTypePopup(y, position, effectType, effectTypeValue, allowedTypes, allowedLabels);

                if (isOnHit)
                    y = DrawProperty(y, position, property.FindPropertyRelative("onHitTriggerDelay"));

                if (UsesDistance(effectTypeValue))
                    y = DrawProperty(y, position, distance);

                if (UsesHeight(effectTypeValue))
                    y = DrawProperty(y, position, heightValue);

                bool showDurationMode = !isOnHit && SupportsTopLevelUntilStateExit(effectTypeValue);
                if (showDurationMode)
                    y = DrawProperty(y, position, durationMode);
                else if (durationMode != null && durationMode.enumValueIndex != (int)SkillEffectDurationMode.FixedTime)
                    durationMode.enumValueIndex = (int)SkillEffectDurationMode.FixedTime;

                if (!showDurationMode
                    || durationMode == null
                    || durationMode.enumValueIndex == (int)SkillEffectDurationMode.FixedTime)
                {
                    DrawProperty(y, position, duration);
                }

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
            bool isOnHit = IsOnHitContext(property);
            PhysicsEffectType[] allowedTypes = isOnHit ? OnHitTypes : TopLevelTypes;
            PhysicsEffectType effectTypeValue = effectType != null
                ? CoerceEffectType(effectType, allowedTypes[0], allowedTypes)
                : allowedTypes[0];

            height += EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(effectType);

            if (isOnHit)
                height += GetChildHeight(property.FindPropertyRelative("onHitTriggerDelay"));

            if (UsesDistance(effectTypeValue))
                height += GetChildHeight(property.FindPropertyRelative("distance"));

            if (UsesHeight(effectTypeValue))
                height += GetChildHeight(property.FindPropertyRelative("height"));

            bool showDurationMode = !isOnHit && SupportsTopLevelUntilStateExit(effectTypeValue);
            if (showDurationMode)
                height += GetChildHeight(property.FindPropertyRelative("durationMode"));

            SerializedProperty durationMode = property.FindPropertyRelative("durationMode");
            if (!showDurationMode
                || durationMode == null
                || durationMode.enumValueIndex == (int)SkillEffectDurationMode.FixedTime)
            {
                height += GetChildHeight(property.FindPropertyRelative("duration"));
            }
            return height;
        }

        private static float DrawEffectTypePopup(
            float y,
            Rect totalRect,
            SerializedProperty property,
            PhysicsEffectType currentType,
            PhysicsEffectType[] allowedTypes,
            string[] allowedLabels)
        {
            int currentIndex = 0;
            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (allowedTypes[i] == currentType)
                {
                    currentIndex = i;
                    break;
                }
            }

            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            int selectedIndex = EditorGUI.Popup(rect, "效果类型", currentIndex, allowedLabels);
            property.enumValueIndex = (int)allowedTypes[Mathf.Clamp(selectedIndex, 0, allowedTypes.Length - 1)];
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
            return property.propertyPath.Contains("onHitPhysicsEffects");
        }

        private static PhysicsEffectType CoerceEffectType(
            SerializedProperty property,
            PhysicsEffectType fallback,
            PhysicsEffectType[] allowedTypes)
        {
            PhysicsEffectType current = (PhysicsEffectType)property.enumValueIndex;
            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (allowedTypes[i] == current)
                    return current;
            }

            property.enumValueIndex = (int)fallback;
            return fallback;
        }

        private static bool UsesDistance(PhysicsEffectType effectType)
        {
            return effectType == PhysicsEffectType.Knockback
                || effectType == PhysicsEffectType.Pull
                || effectType == PhysicsEffectType.Launch
                || effectType == PhysicsEffectType.DashSelf;
        }

        private static bool UsesHeight(PhysicsEffectType effectType)
        {
            return effectType == PhysicsEffectType.Launch
                || effectType == PhysicsEffectType.Airborne;
        }

        private static bool SupportsTopLevelUntilStateExit(PhysicsEffectType effectType)
        {
            return effectType == PhysicsEffectType.SuperArmor
                || effectType == PhysicsEffectType.Invincible;
        }
    }
}
