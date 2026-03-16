using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(WeaponStatRanges))]
    public sealed class WeaponStatRangesDrawer : PropertyDrawer
    {
        private const float RowHeight = 18f;
        private const float RowSpacing = 2f;
        private const float LabelWidth = 64f;
        private const float ValueGap = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property == null || !property.isExpanded)
                return RowHeight;

            int fieldCount = GetVisibleFieldCount(property);
            return RowHeight + RowSpacing + fieldCount * (RowHeight + RowSpacing);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect row = new Rect(position.x, position.y, position.width, RowHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.hp)), "生命");
            DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.mp)), "法力");
            DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.attack)), "攻击");
            DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.defense)), "防御");

            if (AllowsRareFields(property))
            {
                DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.hpRegen)), "生命回复");
                DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.mpRegen)), "法力回复");
            }

            if (AllowsEpicFields(property))
            {
                DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.critRate)), "暴击率");
                DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.critDmg)), "暴击伤害");
            }

            if (AllowsLegendaryFields(property))
            {
                DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.attackSpeed)), "攻速加成");
                DrawRangeField(ref row, property.FindPropertyRelative(nameof(WeaponStatRanges.moveSpeed)), "移速加成");
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static int GetVisibleFieldCount(SerializedProperty property)
        {
            if (AllowsLegendaryFields(property))
                return 10;
            if (AllowsEpicFields(property))
                return 8;
            if (AllowsRareFields(property))
                return 6;
            return 4;
        }

        private static bool AllowsRareFields(SerializedProperty property)
        {
            string rarityName = property.name;
            return rarityName == nameof(WeaponEntryData.rare)
                || rarityName == nameof(WeaponEntryData.epic)
                || rarityName == nameof(WeaponEntryData.legendary);
        }

        private static bool AllowsEpicFields(SerializedProperty property)
        {
            string rarityName = property.name;
            return rarityName == nameof(WeaponEntryData.epic)
                || rarityName == nameof(WeaponEntryData.legendary);
        }

        private static bool AllowsLegendaryFields(SerializedProperty property)
        {
            return property.name == nameof(WeaponEntryData.legendary);
        }

        private static void DrawRangeField(ref Rect row, SerializedProperty rangeProperty, string label)
        {
            row.y += RowHeight + RowSpacing;

            SerializedProperty minProperty = rangeProperty.FindPropertyRelative(nameof(FloatRange.min));
            SerializedProperty maxProperty = rangeProperty.FindPropertyRelative(nameof(FloatRange.max));

            Rect contentRect = EditorGUI.IndentedRect(row);
            Rect labelRect = new Rect(contentRect.x, contentRect.y, LabelWidth, RowHeight);
            Rect minRect = new Rect(labelRect.xMax + ValueGap, contentRect.y, (contentRect.width - LabelWidth - ValueGap * 2f) * 0.5f, RowHeight);
            Rect maxRect = new Rect(minRect.xMax + ValueGap, contentRect.y, minRect.width, RowHeight);

            EditorGUI.LabelField(labelRect, label);
            EditorGUI.PropertyField(minRect, minProperty, GUIContent.none);
            EditorGUI.PropertyField(maxRect, maxProperty, GUIContent.none);
        }
    }
}
