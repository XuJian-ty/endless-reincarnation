using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(SkillDamageEffect))]
    public class SkillDamageEffectDrawer : PropertyDrawer
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

                var detectionDuration = property.FindPropertyRelative("detectionDuration");
                var detectionType = property.FindPropertyRelative("detectionType");
                var hitLayerName = property.FindPropertyRelative("hitLayerName");
                var shape = property.FindPropertyRelative("shape");
                var centerOffset = property.FindPropertyRelative("centerOffset");
                var rotationEuler = property.FindPropertyRelative("rotationEuler");
                var sphereRadius = property.FindPropertyRelative("sphereRadius");
                var sectorAngle = property.FindPropertyRelative("sectorAngle");
                var boxSize = property.FindPropertyRelative("boxSize");
                var colliderNodeName = property.FindPropertyRelative("colliderNodeName");
                var rayOriginOffset = property.FindPropertyRelative("rayOriginOffset");
                var rayMaxDistance = property.FindPropertyRelative("rayMaxDistance");
                var rayRadius = property.FindPropertyRelative("rayRadius");
                var motion = property.FindPropertyRelative("motion");
                var companionVfxEffects = property.FindPropertyRelative("companionVfxEffects");
                var onHitDamageEffects = property.FindPropertyRelative("onHitDamageEffects");
                var onHitStopEffect = property.FindPropertyRelative("onHitStopEffect");
                var onHitPhysicsEffects = property.FindPropertyRelative("onHitPhysicsEffects");
                var onHitAttributeEffects = property.FindPropertyRelative("onHitAttributeEffects");
                var onHitVfxEffects = property.FindPropertyRelative("onHitVfxEffects");
                var onHitSfxEffects = property.FindPropertyRelative("onHitSfxEffects");

                DamageDetectionType detection = detectionType != null
                    ? (DamageDetectionType)detectionType.enumValueIndex
                    : DamageDetectionType.RangeOverlap;
                AttackShapeType shapeValue = shape != null
                    ? (AttackShapeType)shape.enumValueIndex
                    : AttackShapeType.Sphere;

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                y = DrawProperty(y, position, detectionDuration);
                y = DrawProperty(y, position, detectionType);
                y = DrawProperty(y, position, hitLayerName);

                if (detection == DamageDetectionType.RangeOverlap)
                {
                    y = DrawProperty(y, position, shape);
                    y = DrawProperty(y, position, centerOffset);

                    if (shapeValue == AttackShapeType.Sector || shapeValue == AttackShapeType.Box)
                        y = DrawProperty(y, position, rotationEuler);

                    if (shapeValue == AttackShapeType.Sphere || shapeValue == AttackShapeType.Sector)
                        y = DrawProperty(y, position, sphereRadius);

                    if (shapeValue == AttackShapeType.Sector)
                        y = DrawProperty(y, position, sectorAngle);

                    if (shapeValue == AttackShapeType.Box)
                        y = DrawProperty(y, position, boxSize);
                }
                else if (detection == DamageDetectionType.Collision)
                {
                    y = DrawProperty(y, position, colliderNodeName);
                }
                else if (detection == DamageDetectionType.Raycast)
                {
                    y = DrawProperty(y, position, rayOriginOffset);
                    y = DrawProperty(y, position, rotationEuler);
                    y = DrawProperty(y, position, rayMaxDistance);
                    y = DrawProperty(y, position, rayRadius);
                }

                if (detection != DamageDetectionType.Collision)
                {
                    y = DrawProperty(y, position, motion);
                    y = DrawProperty(y, position, companionVfxEffects);
                }
                y = DrawProperty(y, position, onHitStopEffect);
                y = DrawProperty(y, position, onHitDamageEffects);
                y = DrawProperty(y, position, onHitPhysicsEffects);
                y = DrawProperty(y, position, onHitAttributeEffects);
                y = DrawProperty(y, position, onHitVfxEffects);
                DrawProperty(y, position, onHitSfxEffects);

                EditorGUI.indentLevel = oldIndent;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property == null || !property.isExpanded)
                return height;

            SerializedProperty detectionType = property.FindPropertyRelative("detectionType");
            SerializedProperty shape = property.FindPropertyRelative("shape");
            DamageDetectionType detection = detectionType != null
                ? (DamageDetectionType)detectionType.enumValueIndex
                : DamageDetectionType.RangeOverlap;
            AttackShapeType shapeValue = shape != null
                ? (AttackShapeType)shape.enumValueIndex
                : AttackShapeType.Sphere;

            height += EditorGUIUtility.standardVerticalSpacing;
            height += GetChildHeight(property.FindPropertyRelative("detectionDuration"));
            height += GetChildHeight(detectionType);
            height += GetChildHeight(property.FindPropertyRelative("hitLayerName"));

            if (detection == DamageDetectionType.RangeOverlap)
            {
                height += GetChildHeight(shape);
                height += GetChildHeight(property.FindPropertyRelative("centerOffset"));

                if (shapeValue == AttackShapeType.Sector || shapeValue == AttackShapeType.Box)
                    height += GetChildHeight(property.FindPropertyRelative("rotationEuler"));

                if (shapeValue == AttackShapeType.Sphere || shapeValue == AttackShapeType.Sector)
                    height += GetChildHeight(property.FindPropertyRelative("sphereRadius"));

                if (shapeValue == AttackShapeType.Sector)
                    height += GetChildHeight(property.FindPropertyRelative("sectorAngle"));

                if (shapeValue == AttackShapeType.Box)
                    height += GetChildHeight(property.FindPropertyRelative("boxSize"));
            }
            else if (detection == DamageDetectionType.Collision)
            {
                height += GetChildHeight(property.FindPropertyRelative("colliderNodeName"));
            }
            else if (detection == DamageDetectionType.Raycast)
            {
                height += GetChildHeight(property.FindPropertyRelative("rayOriginOffset"));
                height += GetChildHeight(property.FindPropertyRelative("rotationEuler"));
                height += GetChildHeight(property.FindPropertyRelative("rayMaxDistance"));
                height += GetChildHeight(property.FindPropertyRelative("rayRadius"));
            }

            if (detection != DamageDetectionType.Collision)
            {
                height += GetChildHeight(property.FindPropertyRelative("motion"));
                height += GetChildHeight(property.FindPropertyRelative("companionVfxEffects"));
            }
            height += GetChildHeight(property.FindPropertyRelative("onHitStopEffect"));
            height += GetChildHeight(property.FindPropertyRelative("onHitDamageEffects"));
            height += GetChildHeight(property.FindPropertyRelative("onHitPhysicsEffects"));
            height += GetChildHeight(property.FindPropertyRelative("onHitAttributeEffects"));
            height += GetChildHeight(property.FindPropertyRelative("onHitVfxEffects"));
            height += GetChildHeight(property.FindPropertyRelative("onHitSfxEffects"));
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
