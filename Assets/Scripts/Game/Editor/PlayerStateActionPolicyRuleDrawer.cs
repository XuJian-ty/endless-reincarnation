using Game.Data;
using Game.Presentation;
using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(PlayerStateActionPolicyRule))]
    public sealed class PlayerStateActionPolicyRuleDrawer : PropertyDrawer
    {
        private static readonly GameAction[] AllowedActions =
        {
            GameAction.Walk,
            GameAction.Jump,
            GameAction.Dodge,
            GameAction.NormalAttack,
            GameAction.AirAttack,
            GameAction.ChargeStart,
            GameAction.FallAttack,
            GameAction.Shoot,
            GameAction.ShootCharge,
            GameAction.Skill,
        };

        private static readonly string[] AllowedLabels =
        {
            "Move",
            "Jump",
            "Dodge",
            "NormalAttack",
            "AirAttack",
            "ChargeStart",
            "FallAttackStart",
            "Shoot",
            "ShootCharge",
            "Skill",
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

                SerializedProperty action = property.FindPropertyRelative(nameof(PlayerStateActionPolicyRule.action));
                SerializedProperty policy = property.FindPropertyRelative(nameof(PlayerStateActionPolicyRule.policy));

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawAction(y, position, action);
                DrawProperty(y, position, policy);

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
            height += GetChildHeight(property.FindPropertyRelative(nameof(PlayerStateActionPolicyRule.policy)));
            return height;
        }

        private static float DrawAction(float y, Rect totalRect, SerializedProperty property)
        {
            if (property == null)
                return y;

            GameAction current = (GameAction)property.enumValueIndex;
            int allowedIndex = FindAllowedIndex(current);
            bool isSupported = allowedIndex >= 0;
            string[] options = BuildOptions(current, isSupported);
            int selectedIndex = isSupported ? allowedIndex : 0;

            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            int newIndex = EditorGUI.Popup(rect, "动作", selectedIndex, options);
            GameAction resolved = ResolveSelectedAction(current, newIndex, isSupported);
            if (resolved != current)
                property.enumValueIndex = (int)resolved;

            return y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private static int FindAllowedIndex(GameAction action)
        {
            if (IsMoveAction(action))
                return 0;

            for (int i = 0; i < AllowedActions.Length; i++)
            {
                if (AllowedActions[i] == action)
                    return i;
            }

            return -1;
        }

        private static GameAction ResolveSelectedAction(GameAction current, int selectedIndex, bool currentIsSupported)
        {
            if (!currentIsSupported)
            {
                if (selectedIndex <= 0)
                    return current;

                selectedIndex--;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, AllowedActions.Length - 1);
            GameAction selected = AllowedActions[selectedIndex];
            if (selected == GameAction.Walk)
                return IsMoveAction(current) ? current : GameAction.Walk;

            return selected;
        }

        private static string[] BuildOptions(GameAction current, bool currentIsSupported)
        {
            if (currentIsSupported)
                return AllowedLabels;

            string[] options = new string[AllowedLabels.Length + 1];
            options[0] = $"Current ({current})";
            Array.Copy(AllowedLabels, 0, options, 1, AllowedLabels.Length);
            return options;
        }

        private static bool IsMoveAction(GameAction action)
        {
            return action == GameAction.Walk || action == GameAction.Run;
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
