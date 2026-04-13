using System;
using System.Collections.Generic;
using Game.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(EnemyArchetypeSO))]
    public sealed class EnemyArchetypeSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _enemyIdProperty;
        private SerializedProperty _skillSlotsProperty;
        private bool _pendingExitGui;

        private void OnEnable()
        {
            _enemyIdProperty = serializedObject.FindProperty(nameof(EnemyArchetypeSO.enemyId));
            _skillSlotsProperty = serializedObject.FindProperty(nameof(EnemyArchetypeSO.skillSlots));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            _pendingExitGui = false;

            DrawIdentitySection();
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                nameof(EnemyArchetypeSO.enemyId),
                nameof(EnemyArchetypeSO.dedicatedAnimatorController),
                nameof(EnemyArchetypeSO.skillSlots));
            EditorGUILayout.Space(8f);
            DrawAuthoringToolbar();
            if (_pendingExitGui)
            {
                GUIUtility.ExitGUI();
                return;
            }
            EditorGUILayout.Space(8f);
            DrawSkillSlotsSection();
            if (_pendingExitGui)
            {
                GUIUtility.ExitGUI();
                return;
            }

            serializedObject.ApplyModifiedProperties();

            if (_pendingExitGui)
                GUIUtility.ExitGUI();
        }

        private void DrawIdentitySection()
        {
            EnemyArchetypeSO archetype = target as EnemyArchetypeSO;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("基础标识", EditorStyles.boldLabel);

                string currentEnemyId = _enemyIdProperty != null ? _enemyIdProperty.stringValue : string.Empty;
                string nextEnemyId = EditorGUILayout.DelayedTextField(
                    new GUIContent("敌人ID", "主键，必须唯一，例如：melee_minion / boss_1"),
                    currentEnemyId);

                if (_enemyIdProperty != null && !string.Equals(nextEnemyId, currentEnemyId, StringComparison.Ordinal))
                    ApplyEnemyIdChange(archetype, nextEnemyId);

                if (_enemyIdProperty != null && string.IsNullOrWhiteSpace(_enemyIdProperty.stringValue))
                    EditorGUILayout.HelpBox("敌人ID建议保持非空且唯一。修改后会同步更新属性库、技能效果库、动画库以及相关引用。", MessageType.Info);
            }
        }

        private void DrawAuthoringToolbar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("资源与引用", EditorStyles.boldLabel);

                EnemyArchetypeSO archetype = target as EnemyArchetypeSO;
                AnimatorController dedicatedController = archetype != null
                    ? archetype.dedicatedAnimatorController as AnimatorController
                    : null;

                if (dedicatedController == null)
                    EditorGUILayout.HelpBox("当前还没有绑定敌人专属 AnimatorController。点击下方按钮会自动从共享敌人控制器复制一份，并回绑到对应 prefab。", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("创建/同步敌人所需资源和引用"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        if (!EnemySkillAuthoringUtility.TryProvisionDedicatedAnimatorController(archetype, out string errorMessage))
                            Debug.LogError($"[EnemyArchetypeSOEditor] 创建专属动画控制器失败：{errorMessage}");

                        serializedObject.Update();
                        EditorUtility.SetDirty(target);
                        AssetDatabase.SaveAssetIfDirty(target);
                        _pendingExitGui = true;
                    }
                }
            }
        }

        private void DrawSkillSlotsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("技能槽位", EditorStyles.boldLabel);

                if (_skillSlotsProperty == null)
                {
                    EditorGUILayout.HelpBox("未找到技能槽位列表。", MessageType.Error);
                    return;
                }

                List<int> slotIndices = CollectSlotIndices();
                for (int i = 0; i < slotIndices.Count; i++)
                {
                    SerializedProperty slotProperty = _skillSlotsProperty.GetArrayElementAtIndex(slotIndices[i]);
                    if (slotProperty == null)
                        continue;

                    if (DrawSlotHeader(slotProperty))
                        return;

                    if (slotProperty.isExpanded)
                        DrawSlotFields(slotProperty);

                    EditorGUILayout.Space(4f);
                }

                if (GUILayout.Button("添加技能"))
                {
                    serializedObject.ApplyModifiedProperties();
                    if (!EnemySkillAuthoringUtility.TryAppendEnemySkill(target as EnemyArchetypeSO, out _, out string errorMessage))
                        Debug.LogError($"[EnemyArchetypeSOEditor] 添加敌人技能失败：{errorMessage}");

                    serializedObject.Update();
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssetIfDirty(target);
                    _pendingExitGui = true;
                }
            }
        }

        private bool DrawSlotHeader(SerializedProperty slotProperty)
        {
            string title = ResolveSlotTitle(slotProperty);

            using (new EditorGUILayout.HorizontalScope())
            {
                slotProperty.isExpanded = EditorGUILayout.Foldout(slotProperty.isExpanded, title, true);
                if (GUILayout.Button("删除", GUILayout.Width(56f)))
                {
                    int slotIndex = slotProperty.FindPropertyRelative(nameof(EnemySkillSlotBinding.slotIndex))?.intValue ?? -1;
                    serializedObject.ApplyModifiedProperties();
                    if (!EnemySkillAuthoringUtility.TryRemoveEnemySkill(target as EnemyArchetypeSO, slotIndex, out string errorMessage))
                        Debug.LogError($"[EnemyArchetypeSOEditor] 删除敌人技能失败：{errorMessage}");

                    serializedObject.Update();
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssetIfDirty(target);
                    _pendingExitGui = true;
                    return true;
                }
            }

            return false;
        }

        private void ApplyEnemyIdChange(EnemyArchetypeSO archetype, string newEnemyId)
        {
            if (archetype == null || _enemyIdProperty == null)
                return;

            string previousExplicitEnemyId = archetype.enemyId;
            string previousEnemyId = archetype.GetResolvedEnemyId();
            string trimmedNewEnemyId = string.IsNullOrWhiteSpace(newEnemyId) ? string.Empty : newEnemyId.Trim();
            if (string.Equals(previousEnemyId, trimmedNewEnemyId, StringComparison.Ordinal))
            {
                _enemyIdProperty.stringValue = trimmedNewEnemyId;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                return;
            }

            if (!EnemySkillAuthoringUtility.TryValidateEnemyIdChange(archetype, trimmedNewEnemyId, out string errorMessage))
            {
                EditorUtility.DisplayDialog("敌人ID无效", errorMessage, "确定");
                _enemyIdProperty.stringValue = archetype.enemyId;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                return;
            }

            _enemyIdProperty.stringValue = trimmedNewEnemyId;
            serializedObject.ApplyModifiedProperties();

            if (!EnemySkillAuthoringUtility.TryRenameEnemyId(archetype, previousEnemyId, trimmedNewEnemyId, out errorMessage))
            {
                EditorUtility.DisplayDialog("敌人ID同步失败", errorMessage, "确定");
                archetype.enemyId = previousExplicitEnemyId;
                EditorUtility.SetDirty(archetype);
                AssetDatabase.SaveAssetIfDirty(archetype);
            }

            serializedObject.Update();
        }

        private void DrawSlotFields(SerializedProperty slotProperty)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
                DrawReadOnlyProperty(slotProperty, nameof(EnemySkillSlotBinding.slotIndex));
                DrawReadOnlyProperty(slotProperty, nameof(EnemySkillSlotBinding.skillId));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.displayName));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.naturalExitNormalizedTime));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.phaseAvailability));
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("AI战斗参数", EditorStyles.boldLabel);
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.cooldown));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.castRange));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.minCastRange));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.idealCastRange));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.skillRole));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.riskWeight));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.punishWeight));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.repeatPenalty));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.canUseUnderThreat));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.postCastIdleDuration));
                DrawProperty(slotProperty, nameof(EnemySkillSlotBinding.rotateToTargetOnCast));
            }
        }

        private List<int> CollectSlotIndices()
        {
            List<int> indices = new List<int>();
            if (_skillSlotsProperty == null)
                return indices;

            for (int i = 0; i < _skillSlotsProperty.arraySize; i++)
                indices.Add(i);

            indices.Sort((left, right) =>
            {
                SerializedProperty leftProp = _skillSlotsProperty.GetArrayElementAtIndex(left);
                SerializedProperty rightProp = _skillSlotsProperty.GetArrayElementAtIndex(right);
                int leftIndex = leftProp?.FindPropertyRelative(nameof(EnemySkillSlotBinding.slotIndex))?.intValue ?? int.MaxValue;
                int rightIndex = rightProp?.FindPropertyRelative(nameof(EnemySkillSlotBinding.slotIndex))?.intValue ?? int.MaxValue;
                return leftIndex.CompareTo(rightIndex);
            });
            return indices;
        }

        private static string ResolveSlotTitle(SerializedProperty slotProperty)
        {
            SerializedProperty slotIndex = slotProperty.FindPropertyRelative(nameof(EnemySkillSlotBinding.slotIndex));
            SerializedProperty displayName = slotProperty.FindPropertyRelative(nameof(EnemySkillSlotBinding.displayName));
            SerializedProperty skillId = slotProperty.FindPropertyRelative(nameof(EnemySkillSlotBinding.skillId));

            if (displayName != null && !string.IsNullOrWhiteSpace(displayName.stringValue))
                return $"{displayName.stringValue.Trim()} (槽位 {slotIndex?.intValue ?? -1})";

            if (skillId != null && !string.IsNullOrWhiteSpace(skillId.stringValue))
                return $"{skillId.stringValue.Trim()} (槽位 {slotIndex?.intValue ?? -1})";

            return $"技能槽 {slotIndex?.intValue ?? -1}";
        }

        private static void DrawProperty(SerializedProperty slotProperty, string relativeName)
        {
            SerializedProperty property = slotProperty.FindPropertyRelative(relativeName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        private static void DrawReadOnlyProperty(SerializedProperty slotProperty, string relativeName)
        {
            SerializedProperty property = slotProperty.FindPropertyRelative(relativeName);
            if (property == null)
                return;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(property, true);
        }
    }
}
