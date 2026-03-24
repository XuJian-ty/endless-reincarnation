using Game.Data;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(PlayerStatePendingReleaseRule))]
    public sealed class PlayerStatePendingReleaseRuleDrawer : PropertyDrawer
    {
        private static readonly GameAction[] AllowedActions =
        {
            GameAction.None,
            GameAction.Walk,
            GameAction.Run,
            GameAction.Jump,
            GameAction.NormalAttack,
            GameAction.ChargeStart,
            GameAction.AirAttack,
            GameAction.FallAttack,
            GameAction.Dodge,
            GameAction.Skill,
            GameAction.Shoot,
            GameAction.ShootCharge,
        };

        private static readonly string[] AllowedLabels =
        {
            "None",
            "Walk",
            "Run",
            "Jump",
            "NormalAttack",
            "ChargeStart",
            "AirAttack",
            "FallAttackStart",
            "Dodge",
            "Skill",
            "Shoot",
            "ShootCharge",
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

                SerializedProperty pendingAction = property.FindPropertyRelative(nameof(PlayerStatePendingReleaseRule.pendingAction));
                SerializedProperty normalizedTime = property.FindPropertyRelative(nameof(PlayerStatePendingReleaseRule.normalizedTime));

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawPendingAction(y, position, pendingAction);
                DrawProperty(y, position, normalizedTime);

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
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property.FindPropertyRelative(nameof(PlayerStatePendingReleaseRule.normalizedTime)));
            return height;
        }

        private static float DrawPendingAction(float y, Rect totalRect, SerializedProperty property)
        {
            if (property == null)
                return y;

            int selectedIndex = 0;
            GameAction current = (GameAction)property.enumValueIndex;
            for (int i = 0; i < AllowedActions.Length; i++)
            {
                if (AllowedActions[i] == current)
                {
                    selectedIndex = i;
                    break;
                }
            }

            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            int newIndex = EditorGUI.Popup(rect, "缓存动作", selectedIndex, AllowedLabels);
            property.enumValueIndex = (int)AllowedActions[Mathf.Clamp(newIndex, 0, AllowedActions.Length - 1)];
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
    }
}
