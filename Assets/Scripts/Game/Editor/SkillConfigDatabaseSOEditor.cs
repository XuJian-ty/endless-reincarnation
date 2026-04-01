using System.Collections.Generic;
using Game.Data;
using Game.Presentation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(SkillConfigDatabaseSO))]
    public sealed class SkillConfigDatabaseSOEditor : UnityEditor.Editor
    {
        private static readonly string[] BaseActionIds =
        {
            "NormalIdle", "NormalWalk", "NormalRun",
            "AimIdle", "AimWalk", "AimRun",
            "Jump", "Dodge", "Fall", "Land",
            "Attack0", "Attack1", "Attack2", "Attack3",
            "AirAttack", "ChargeStart", "ChargeLoop", "ChargeRelease",
            "FallAttackStart", "FallAttackLoop", "FallAttackLand",
            "HitStun", "PlayerDeath", "Shoot", "ShootCharge", "Aim"
        };

        private SerializedProperty _entriesProp;
        private SerializedProperty _formActionMappingsProp;
        private bool _pendingExitGui;

        private void OnEnable()
        {
            _entriesProp = serializedObject.FindProperty(nameof(SkillConfigDatabaseSO.entries));
            _formActionMappingsProp = serializedObject.FindProperty(nameof(SkillConfigDatabaseSO.formActionMappings));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            _pendingExitGui = false;

            if (_entriesProp == null)
            {
                EditorGUILayout.HelpBox("未找到技能配置列表。", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            if (_formActionMappingsProp != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("形态基础动作映射", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_formActionMappingsProp, true);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(6f);
            }

            DrawGroupSection("基础动作", PlayerSkillEntryGroup.BaseSkill, DrawBaseSkillFields);
            if (_pendingExitGui)
            {
                GUIUtility.ExitGUI();
                return;
            }

            EditorGUILayout.Space(6f);
            DrawGroupSection("主动技能", PlayerSkillEntryGroup.ActiveSkill, DrawActiveSkillFields);
            if (_pendingExitGui)
            {
                GUIUtility.ExitGUI();
                return;
            }

            EditorGUILayout.Space(6f);
            DrawGroupSection("被动技能", PlayerSkillEntryGroup.PassiveSkill, DrawPassiveSkillFields);
            if (_pendingExitGui)
            {
                GUIUtility.ExitGUI();
                return;
            }

            serializedObject.ApplyModifiedProperties();

            if (_pendingExitGui)
                GUIUtility.ExitGUI();
        }

        private void DrawGroupSection(string title, PlayerSkillEntryGroup group, System.Action<SerializedProperty> drawFields)
        {
            List<int> indices = CollectGroupIndices(group);
            bool deleted = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            for (int orderIndex = 0; orderIndex < indices.Count; orderIndex++)
            {
                int entryIndex = indices[orderIndex];
                SerializedProperty entryProp = _entriesProp.GetArrayElementAtIndex(entryIndex);
                SyncEntryGroup(entryProp, group, orderIndex);
                if (DrawEntryHeader(entryProp, group, orderIndex, entryIndex))
                {
                    deleted = true;
                    break;
                }
                if (entryProp.isExpanded)
                    drawFields?.Invoke(entryProp);
                EditorGUILayout.Space(2f);
            }

            if (!deleted && group != PlayerSkillEntryGroup.BaseSkill && GUILayout.Button($"添加{title}"))
            {
                if (group == PlayerSkillEntryGroup.ActiveSkill)
                    AddActiveSkillEntry();
                else
                    AddEntry(group);
            }

            EditorGUILayout.EndVertical();

            if (!deleted)
                return;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            _pendingExitGui = true;
        }

        private bool DrawEntryHeader(SerializedProperty entryProp, PlayerSkillEntryGroup group, int orderIndex, int entryIndex)
        {
            string title = ResolveEntryTitle(entryProp, group, orderIndex);
            EditorGUILayout.BeginHorizontal();
            entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, title, true);
            bool canDelete = group != PlayerSkillEntryGroup.BaseSkill;
            if (canDelete && GUILayout.Button("删除", GUILayout.Width(56f)))
            {
                if (group == PlayerSkillEntryGroup.ActiveSkill)
                    DeleteActiveSkillEntry(entryProp);
                else
                {
                    Undo.RecordObject(target, "删除动作或技能配置");
                    DeleteEntry(entryIndex);
                }
                EditorGUILayout.EndHorizontal();
                return true;
            }
            EditorGUILayout.EndHorizontal();
            return false;
        }

        private void DrawBaseSkillFields(SerializedProperty entryProp)
        {
            DrawReadOnlyProperty(entryProp, nameof(SkillConfigEntry.actionId));
            DrawProperty(entryProp, nameof(SkillConfigEntry.skillEffectDatabase));
            DrawProperty(entryProp, nameof(SkillConfigEntry.skillId));
            DrawProperty(entryProp, nameof(SkillConfigEntry.displayName));
            DrawProperty(entryProp, nameof(SkillConfigEntry.description));
            EditorGUILayout.HelpBox("动画 Trigger 统一在“技能效果库”对应 skillId 的默认技能效果项中维护。", MessageType.Info);
            DrawProperty(entryProp, nameof(SkillConfigEntry.supportedAttackModes));
            DrawActionRuleFields(entryProp);
        }

        private void DrawActiveSkillFields(SerializedProperty entryProp)
        {
            DrawReadOnlyProperty(entryProp, nameof(SkillConfigEntry.actionId));
            DrawProperty(entryProp, nameof(SkillConfigEntry.skillEffectDatabase));
            DrawProperty(entryProp, nameof(SkillConfigEntry.skillId));
            DrawProperty(entryProp, nameof(SkillConfigEntry.displayName));
            DrawProperty(entryProp, nameof(SkillConfigEntry.description));
            DrawProperty(entryProp, nameof(SkillConfigEntry.skillIcon));
            EditorGUILayout.HelpBox("动画 Trigger 统一在“技能效果库”对应 skillId 的默认技能效果项中维护。", MessageType.Info);
            DrawProperty(entryProp, nameof(SkillConfigEntry.supportedAttackModes));
            DrawProperty(entryProp, nameof(SkillConfigEntry.talentCost));
            DrawProperty(entryProp, nameof(SkillConfigEntry.mpCost));
            DrawProperty(entryProp, nameof(SkillConfigEntry.cooldownSeconds));
            DrawActionRuleFields(entryProp);
        }

        private void DrawPassiveSkillFields(SerializedProperty entryProp)
        {
            DrawReadOnlyProperty(entryProp, nameof(SkillConfigEntry.actionId));
            DrawProperty(entryProp, nameof(SkillConfigEntry.passiveSkillEffectDatabase));
            DrawProperty(entryProp, nameof(SkillConfigEntry.skillId));
            DrawProperty(entryProp, nameof(SkillConfigEntry.displayName));
            DrawProperty(entryProp, nameof(SkillConfigEntry.description));
            DrawProperty(entryProp, nameof(SkillConfigEntry.skillIcon));
            DrawProperty(entryProp, nameof(SkillConfigEntry.talentCost));
            EditorGUILayout.HelpBox("被动属性加成统一在“被动技能效果库”中配置；当前条目只保留技能ID映射。", MessageType.Info);
        }

        private void DrawProperty(SerializedProperty entryProp, string relativeName)
        {
            SerializedProperty prop = entryProp.FindPropertyRelative(relativeName);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, true);
        }

        private void DrawProperty(SerializedProperty entryProp, string relativeName, string label)
        {
            SerializedProperty prop = entryProp.FindPropertyRelative(relativeName);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
        }

        private void DrawReadOnlyProperty(SerializedProperty entryProp, string relativeName)
        {
            SerializedProperty prop = entryProp.FindPropertyRelative(relativeName);
            if (prop == null)
                return;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(prop, true);
        }

        private void DrawActionRuleFields(SerializedProperty entryProp)
        {
            DrawProperty(entryProp, nameof(SkillConfigEntry.actionPolicies), "动作策略表");
            DrawProperty(entryProp, nameof(SkillConfigEntry.pendingReleaseRules), "缓存释放规则");
            DrawProperty(entryProp, nameof(SkillConfigEntry.overrideNaturalExitNormalizedTime));
            if (entryProp.FindPropertyRelative(nameof(SkillConfigEntry.overrideNaturalExitNormalizedTime))?.boolValue == true)
                DrawProperty(entryProp, nameof(SkillConfigEntry.naturalExitNormalizedTime));
            DrawProperty(entryProp, nameof(SkillConfigEntry.naturalExitTarget));
        }

        private List<int> CollectGroupIndices(PlayerSkillEntryGroup group)
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                SerializedProperty entryProp = _entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty groupProp = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.entryGroup));
                if (groupProp != null && groupProp.enumValueIndex == (int)group)
                    indices.Add(i);
            }

            return indices;
        }

        private void SyncEntryGroup(SerializedProperty entryProp, PlayerSkillEntryGroup group, int orderIndex)
        {
            SerializedProperty groupProp = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.entryGroup));
            if (groupProp != null)
                groupProp.enumValueIndex = (int)group;

            SerializedProperty passiveProp = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.isPassive));
            if (passiveProp != null)
                passiveProp.boolValue = group == PlayerSkillEntryGroup.PassiveSkill;

            SerializedProperty actionIdProp = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.actionId));
            if (actionIdProp == null)
                return;

            if (group == PlayerSkillEntryGroup.BaseSkill)
            {
                actionIdProp.stringValue = orderIndex >= 0 && orderIndex < BaseActionIds.Length
                    ? BaseActionIds[orderIndex]
                    : $"BaseAction{orderIndex + 1}";
                return;
            }

            if (group == PlayerSkillEntryGroup.ActiveSkill)
            {
                if (string.IsNullOrWhiteSpace(actionIdProp.stringValue))
                    actionIdProp.stringValue = $"Skill{orderIndex}";
                return;
            }

            if (string.IsNullOrWhiteSpace(actionIdProp.stringValue))
                actionIdProp.stringValue = $"passive_{orderIndex + 1}";
        }

        private string ResolveEntryTitle(SerializedProperty entryProp, PlayerSkillEntryGroup group, int orderIndex)
        {
            SerializedProperty displayName = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.displayName));
            SerializedProperty actionId = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.actionId));
            SerializedProperty skillId = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.skillId));

            string resolvedName = displayName != null && !string.IsNullOrWhiteSpace(displayName.stringValue)
                ? displayName.stringValue.Trim()
                : (actionId != null && !string.IsNullOrWhiteSpace(actionId.stringValue)
                    ? actionId.stringValue.Trim()
                    : (skillId != null ? skillId.stringValue.Trim() : string.Empty));

            if (string.IsNullOrEmpty(resolvedName))
            {
                resolvedName = group switch
                {
                    PlayerSkillEntryGroup.BaseSkill => $"基础动作 {orderIndex + 1}",
                    PlayerSkillEntryGroup.ActiveSkill => $"主动{orderIndex}",
                    _ => $"被动{orderIndex + 1}"
                };
            }

            return resolvedName;
        }

        private void AddEntry(PlayerSkillEntryGroup group)
        {
            int groupOrderIndex = CollectGroupIndices(group).Count;
            int insertIndex = _entriesProp.arraySize;
            _entriesProp.InsertArrayElementAtIndex(insertIndex);
            SerializedProperty entryProp = _entriesProp.GetArrayElementAtIndex(insertIndex);
            ResetEntry(entryProp, group, groupOrderIndex);

            if (group != PlayerSkillEntryGroup.PassiveSkill)
                return;

            serializedObject.ApplyModifiedProperties();
            PassiveSkillEffectDatabaseSOEditor.EnsurePassiveSkillGroups(target as SkillConfigDatabaseSO);
            serializedObject.Update();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }

        private void AddActiveSkillEntry()
        {
            serializedObject.ApplyModifiedProperties();

            if (!ActiveSkillAuthoringUtility.TryAppendActiveSkill(target as SkillConfigDatabaseSO, out _, out string errorMessage))
            {
                Debug.LogError($"[SkillConfigDatabaseSOEditor] 添加主动技能失败：{errorMessage}");
                _pendingExitGui = true;
                return;
            }

            serializedObject.Update();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            _pendingExitGui = true;
        }

        private void DeleteEntry(int entryIndex)
        {
            int oldSize = _entriesProp.arraySize;
            _entriesProp.DeleteArrayElementAtIndex(entryIndex);
            if (_entriesProp.arraySize == oldSize)
                _entriesProp.DeleteArrayElementAtIndex(entryIndex);
        }

        private void DeleteActiveSkillEntry(SerializedProperty entryProp)
        {
            string actionId = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.actionId))?.stringValue;
            serializedObject.ApplyModifiedProperties();

            if (!ActiveSkillAuthoringUtility.TryRemoveActiveSkill(target as SkillConfigDatabaseSO, actionId, out string errorMessage))
            {
                Debug.LogError($"[SkillConfigDatabaseSOEditor] 删除主动技能失败：{errorMessage}");
                _pendingExitGui = true;
                return;
            }

            serializedObject.Update();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            _pendingExitGui = true;
        }
        private static void ResetEntry(SerializedProperty entryProp, PlayerSkillEntryGroup group, int orderIndex)
        {
            entryProp.isExpanded = true;

            SerializedProperty skillId = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.skillId));
            string defaultActionId = group switch
            {
                PlayerSkillEntryGroup.ActiveSkill => $"Skill{orderIndex}",
                PlayerSkillEntryGroup.PassiveSkill => $"passive_{orderIndex + 1}",
                _ => string.Empty
            };
            if (skillId != null)
            {
                skillId.stringValue = group switch
                {
                    PlayerSkillEntryGroup.ActiveSkill => defaultActionId,
                    PlayerSkillEntryGroup.PassiveSkill => defaultActionId,
                    _ => string.Empty
                };
            }

            SerializedProperty actionId = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.actionId));
            if (actionId != null)
                actionId.stringValue = group == PlayerSkillEntryGroup.BaseSkill ? string.Empty : defaultActionId;

            SerializedProperty skillEffectDatabase = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.skillEffectDatabase));
            if (skillEffectDatabase != null)
                skillEffectDatabase.objectReferenceValue = null;

            SerializedProperty passiveSkillEffectDatabase = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.passiveSkillEffectDatabase));
            if (passiveSkillEffectDatabase != null)
                passiveSkillEffectDatabase.objectReferenceValue = null;

            SerializedProperty displayName = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.displayName));
            if (displayName != null)
            {
                displayName.stringValue = group switch
                {
                    PlayerSkillEntryGroup.ActiveSkill => $"主动{orderIndex}",
                    PlayerSkillEntryGroup.PassiveSkill => $"被动{orderIndex + 1}",
                    _ => string.Empty
                };
            }

            SerializedProperty isPassive = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.isPassive));
            if (isPassive != null)
                isPassive.boolValue = group == PlayerSkillEntryGroup.PassiveSkill;

            SerializedProperty entryGroup = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.entryGroup));
            if (entryGroup != null)
                entryGroup.enumValueIndex = (int)group;

            SerializedProperty talentCost = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.talentCost));
            if (talentCost != null)
                talentCost.intValue = group == PlayerSkillEntryGroup.ActiveSkill ? 2 : 1;

            SerializedProperty mpCost = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.mpCost));
            if (mpCost != null)
                mpCost.intValue = group == PlayerSkillEntryGroup.ActiveSkill ? 10 : 0;

            SerializedProperty cooldown = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.cooldownSeconds));
            if (cooldown != null)
                cooldown.floatValue = group == PlayerSkillEntryGroup.ActiveSkill ? 3f : 0f;

            SerializedProperty supportedAttackModes = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.supportedAttackModes));
            if (supportedAttackModes != null)
                supportedAttackModes.intValue = (int)PlayerAttackModeMask.All;

            SerializedProperty actionPolicies = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.actionPolicies));
            if (actionPolicies != null)
            {
                actionPolicies.ClearArray();
                if (group == PlayerSkillEntryGroup.ActiveSkill)
                {
                    AddActionPolicy(actionPolicies, GameAction.Walk, TransitionPolicy.Buffer);
                    AddActionPolicy(actionPolicies, GameAction.Jump, TransitionPolicy.Buffer);
                    AddActionPolicy(actionPolicies, GameAction.Dodge, TransitionPolicy.Interrupt);
                    AddActionPolicy(actionPolicies, GameAction.NormalAttack, TransitionPolicy.Ignore);
                    AddActionPolicy(actionPolicies, GameAction.AirAttack, TransitionPolicy.Buffer);
                    AddActionPolicy(actionPolicies, GameAction.ChargeStart, TransitionPolicy.Interrupt);
                    AddActionPolicy(actionPolicies, GameAction.FallAttack, TransitionPolicy.Buffer);
                    AddActionPolicy(actionPolicies, GameAction.Shoot, TransitionPolicy.Ignore);
                    AddActionPolicy(actionPolicies, GameAction.ShootCharge, TransitionPolicy.Interrupt);
                    AddActionPolicy(actionPolicies, GameAction.Skill, TransitionPolicy.Interrupt);
                }
            }

            SerializedProperty pendingReleaseRules = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.pendingReleaseRules));
            if (pendingReleaseRules != null)
                pendingReleaseRules.ClearArray();

            SerializedProperty overrideNaturalExit = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.overrideNaturalExitNormalizedTime));
            if (overrideNaturalExit != null)
                overrideNaturalExit.boolValue = group == PlayerSkillEntryGroup.ActiveSkill;

            SerializedProperty naturalExitNormalizedTime = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.naturalExitNormalizedTime));
            if (naturalExitNormalizedTime != null)
                naturalExitNormalizedTime.floatValue = 0.9f;

            SerializedProperty naturalExitTarget = entryProp.FindPropertyRelative(nameof(SkillConfigEntry.naturalExitTarget));
            if (naturalExitTarget != null)
                naturalExitTarget.enumValueIndex = 0;
        }

        private static void AddActionPolicy(SerializedProperty actionPolicies, int actionValue, int policyValue)
        {
            int insertIndex = actionPolicies.arraySize;
            actionPolicies.InsertArrayElementAtIndex(insertIndex);
            SerializedProperty rule = actionPolicies.GetArrayElementAtIndex(insertIndex);
            SerializedProperty action = rule.FindPropertyRelative("action");
            if (action != null)
                action.enumValueIndex = actionValue;

            SerializedProperty policy = rule.FindPropertyRelative("policy");
            if (policy != null)
                policy.enumValueIndex = policyValue;
        }

        private static void AddActionPolicy(SerializedProperty actionPolicies, GameAction actionValue, TransitionPolicy policyValue)
        {
            AddActionPolicy(actionPolicies, (int)actionValue, (int)policyValue);
        }

    }
}
