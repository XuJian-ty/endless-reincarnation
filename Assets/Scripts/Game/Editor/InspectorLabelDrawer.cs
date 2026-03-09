using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(InspectorLabelAttribute))]
    public class InspectorLabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var att = (InspectorLabelAttribute)attribute;
            EditorGUI.PropertyField(position, property, new GUIContent(att.Label), true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
