using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(BuffEntry))]
    public sealed class BuffEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null)
                return;

            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string title = ResolveFoldoutLabel(property);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                int oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                SerializedProperty buffId = property.FindPropertyRelative(nameof(BuffEntry.buffId));
                SerializedProperty displayName = property.FindPropertyRelative(nameof(BuffEntry.displayName));
                SerializedProperty description = property.FindPropertyRelative(nameof(BuffEntry.description));
                SerializedProperty type = property.FindPropertyRelative(nameof(BuffEntry.type));

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawProperty(y, position, buffId);
                y = DrawProperty(y, position, displayName);
                y = DrawProperty(y, position, description);
                y = DrawTypePopup(y, position, type);

                BuffType buffType = type != null
                    ? (BuffType)type.enumValueIndex
                    : BuffType.StatOnly;

                switch (buffType)
                {
                    case BuffType.StatOnly:
                        y = DrawStatFields(y, position, property);
                        break;

                    case BuffType.Afterimage:
                        y = DrawProperty(y, position, property.FindPropertyRelative(nameof(BuffEntry.afterimageDelaySeconds)));
                        y = DrawProperty(y, position, property.FindPropertyRelative(nameof(BuffEntry.afterimageDamageMultiplier)));
                        break;

                    case BuffType.SuperArmorDamageReduce:
                        y = DrawProperty(y, position, property.FindPropertyRelative(nameof(BuffEntry.damageReduceAdd)));
                        break;

                    case BuffType.DamageRangeExpand:
                        y = DrawProperty(y, position, property.FindPropertyRelative(nameof(BuffEntry.damageRangeScale)));
                        break;

                    case BuffType.SummonClone:
                        y = DrawProperty(y, position, property.FindPropertyRelative(nameof(BuffEntry.cloneFollowRadius)));
                        y = DrawProperty(y, position, property.FindPropertyRelative(nameof(BuffEntry.cloneOpacity)));
                        break;
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

            height += EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.buffId)));
            height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.displayName)));
            height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.description)));
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty type = property.FindPropertyRelative(nameof(BuffEntry.type));
            BuffType buffType = type != null
                ? (BuffType)type.enumValueIndex
                : BuffType.StatOnly;

            switch (buffType)
            {
                case BuffType.StatOnly:
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.hpAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.mpAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.attackAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.defenseAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.lifeStealAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.critRateAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.critDmgAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.attackSpeedAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.moveSpeedAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.hpRegenAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.mpRegenAdd)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.damageBonusAdd)));
                    break;

                case BuffType.Afterimage:
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.afterimageDelaySeconds)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.afterimageDamageMultiplier)));
                    break;

                case BuffType.SuperArmorDamageReduce:
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.damageReduceAdd)));
                    break;

                case BuffType.DamageRangeExpand:
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.damageRangeScale)));
                    break;

                case BuffType.SummonClone:
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.cloneFollowRadius)));
                    height += GetChildHeight(property.FindPropertyRelative(nameof(BuffEntry.cloneOpacity)));
                    break;
            }

            return height;
        }

        private static string ResolveFoldoutLabel(SerializedProperty property)
        {
            SerializedProperty displayName = property.FindPropertyRelative(nameof(BuffEntry.displayName));
            if (displayName != null && !string.IsNullOrWhiteSpace(displayName.stringValue))
                return displayName.stringValue.Trim();

            SerializedProperty buffId = property.FindPropertyRelative(nameof(BuffEntry.buffId));
            if (buffId != null && !string.IsNullOrWhiteSpace(buffId.stringValue))
                return buffId.stringValue.Trim();

            return "Buff 条目";
        }

        private static float DrawStatFields(float y, Rect totalRect, SerializedProperty property)
        {
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.hpAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.mpAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.attackAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.defenseAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.lifeStealAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.critRateAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.critDmgAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.attackSpeedAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.moveSpeedAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.hpRegenAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.mpRegenAdd)));
            y = DrawProperty(y, totalRect, property.FindPropertyRelative(nameof(BuffEntry.damageBonusAdd)));
            return y;
        }

        private static float DrawTypePopup(float y, Rect totalRect, SerializedProperty property)
        {
            if (property == null)
                return y;

            Rect rect = new Rect(totalRect.x, y, totalRect.width, EditorGUIUtility.singleLineHeight);
            int selected = Mathf.Clamp(property.enumValueIndex, 0, 4);
            string[] options = { "属性加成", "霸体加减伤", "召唤分身", "攻击附带重影", "伤害范围扩大" };
            selected = EditorGUI.Popup(rect, "类型", selected, options);
            property.enumValueIndex = selected switch
            {
                0 => (int)BuffType.StatOnly,
                1 => (int)BuffType.SuperArmorDamageReduce,
                2 => (int)BuffType.SummonClone,
                3 => (int)BuffType.Afterimage,
                4 => (int)BuffType.DamageRangeExpand,
                _ => (int)BuffType.StatOnly,
            };
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
