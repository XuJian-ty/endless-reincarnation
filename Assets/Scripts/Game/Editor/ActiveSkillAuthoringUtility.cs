using System;
using System.Collections.Generic;
using System.IO;
using Game.Data;
using Game.Domain;
using Game.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Editor
{
    internal static class ActiveSkillAuthoringUtility
    {
        private struct SkillAnimatorTemplateContext
        {
            public AnimatorStateMachine stateMachine;
            public ChildAnimatorState childState;
        }

        private const string SkillEffectDatabasePath = "Assets/Resources/配置/技能效果库.asset";
        private const string AnimationLibraryPath = "Assets/Resources/配置/动画库.asset";
        private const string InputActionsPath = "Assets/Input/PlayerInputActions.inputactions";
        private const string KeyRebindConfigPath = "Assets/Resources/配置/按键重绑定配置.asset";
        private const string PlayerAnimatorControllerPath = "Assets/外部导入/Animator/PlayerAnimator.controller";
        private const string GameplayActionMapName = "Gameplay";
        private const string PlayerGroupId = "player";
        private const string PlayerGroupName = "玩家";

        public static bool TryAppendActiveSkill(SkillConfigDatabaseSO skillConfig, out int createdSkillIndex, out string errorMessage)
        {
            createdSkillIndex = -1;
            errorMessage = null;

            if (skillConfig == null)
            {
                errorMessage = "未找到玩家动作及技能配置库。";
                return false;
            }

            if (!TryLoadDependencies(
                    out SkillEffectDatabaseSO sharedSkillDatabase,
                    out CharacterAnimationLibrarySO animationLibrary,
                    out AnimatorController animatorController,
                    out InputActionAsset inputActions,
                    out KeyRebindConfigSO keyRebindConfig,
                    out errorMessage))
            {
                return false;
            }

            if (!ValidateAuthoringPrerequisites(animatorController, inputActions, out errorMessage))
                return false;

            int nextSkillIndex = ResolveNextSkillIndex(
                skillConfig,
                sharedSkillDatabase,
                animationLibrary,
                animatorController,
                inputActions,
                keyRebindConfig);

            string actionId = $"Skill{nextSkillIndex}";
            string displayName = $"主动{nextSkillIndex}";

            List<UnityEngine.Object> dirtyAssets = new List<UnityEngine.Object>
            {
                skillConfig,
                sharedSkillDatabase,
                animationLibrary,
                animatorController,
                inputActions,
                keyRebindConfig,
            };

            Undo.RecordObjects(dirtyAssets.ToArray(), "添加主动技能");

            AppendSkillConfigEntry(skillConfig, sharedSkillDatabase, actionId, displayName);
            AppendSharedSkillDefinition(sharedSkillDatabase, actionId, displayName);
            CharacterAnimationEntry animationEntry = AppendAnimationEntry(animationLibrary, actionId);
            EnsureAnimatorSkillState(animatorController, actionId, animationEntry, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage))
                return false;

            EnsureInputAction(inputActions, nextSkillIndex);
            EnsurePotionBindings(inputActions);
            EnsureKeyRebindEntries(keyRebindConfig, nextSkillIndex);

            for (int i = 0; i < dirtyAssets.Count; i++)
            {
                UnityEngine.Object asset = dirtyAssets[i];
                if (asset != null)
                    EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssetIfDirty(skillConfig);
            AssetDatabase.SaveAssetIfDirty(sharedSkillDatabase);
            AssetDatabase.SaveAssetIfDirty(animationLibrary);
            AssetDatabase.SaveAssetIfDirty(animatorController);
            SaveInputActionsAsset(inputActions);
            AssetDatabase.SaveAssetIfDirty(keyRebindConfig);

            createdSkillIndex = nextSkillIndex;
            return true;
        }

        public static bool TryRemoveActiveSkill(SkillConfigDatabaseSO skillConfig, string actionId, out string errorMessage)
        {
            errorMessage = null;

            if (skillConfig == null)
            {
                errorMessage = "未找到玩家动作及技能配置库。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(actionId))
            {
                errorMessage = "未提供要删除的主动技能 ActionId。";
                return false;
            }

            string normalizedActionId = actionId.Trim();
            if (!TryParseSkillName(normalizedActionId, out _))
            {
                errorMessage = $"要删除的条目不是主动技能：{normalizedActionId}";
                return false;
            }

            if (!TryLoadDependencies(
                    out SkillEffectDatabaseSO sharedSkillDatabase,
                    out CharacterAnimationLibrarySO animationLibrary,
                    out AnimatorController animatorController,
                    out InputActionAsset inputActions,
                    out KeyRebindConfigSO keyRebindConfig,
                    out errorMessage))
            {
                return false;
            }

            List<UnityEngine.Object> dirtyAssets = new List<UnityEngine.Object>
            {
                skillConfig,
                sharedSkillDatabase,
                animationLibrary,
                animatorController,
                inputActions,
                keyRebindConfig,
            };

            Undo.RecordObjects(dirtyAssets.ToArray(), "删除主动技能");

            if (!RemoveSkillConfigEntry(skillConfig, normalizedActionId))
            {
                errorMessage = $"未在玩家动作及技能配置库中找到主动技能：{normalizedActionId}";
                return false;
            }

            RemoveSharedSkillDefinition(sharedSkillDatabase, normalizedActionId);
            RemoveAnimationEntry(animationLibrary, normalizedActionId);
            RemoveAnimatorSkillArtifacts(animatorController, normalizedActionId);
            RemoveInputAction(inputActions, normalizedActionId);
            RemoveKeyRebindEntry(keyRebindConfig, normalizedActionId);

            for (int i = 0; i < dirtyAssets.Count; i++)
            {
                UnityEngine.Object asset = dirtyAssets[i];
                if (asset != null)
                    EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssetIfDirty(skillConfig);
            AssetDatabase.SaveAssetIfDirty(sharedSkillDatabase);
            AssetDatabase.SaveAssetIfDirty(animationLibrary);
            AssetDatabase.SaveAssetIfDirty(animatorController);
            SaveInputActionsAsset(inputActions);
            AssetDatabase.SaveAssetIfDirty(keyRebindConfig);

            return true;
        }

        private static bool ValidateAuthoringPrerequisites(
            AnimatorController animatorController,
            InputActionAsset inputActions,
            out string errorMessage)
        {
            errorMessage = null;

            if (animatorController == null || animatorController.layers == null || animatorController.layers.Length == 0)
            {
                errorMessage = "玩家 AnimatorController 缺少基础层。";
                return false;
            }

            AnimatorStateMachine stateMachine = animatorController.layers[0].stateMachine;
            if (stateMachine == null || !TryFindHighestSkillAnimatorState(stateMachine, out _))
            {
                errorMessage = "玩家 AnimatorController 中未找到可复制的主动技能状态模板。";
                return false;
            }

            if (inputActions?.FindActionMap(GameplayActionMapName, false) == null)
            {
                errorMessage = "输入动作资产中未找到 Gameplay ActionMap。";
                return false;
            }

            return true;
        }

        private static bool TryLoadDependencies(
            out SkillEffectDatabaseSO sharedSkillDatabase,
            out CharacterAnimationLibrarySO animationLibrary,
            out AnimatorController animatorController,
            out InputActionAsset inputActions,
            out KeyRebindConfigSO keyRebindConfig,
            out string errorMessage)
        {
            errorMessage = null;
            sharedSkillDatabase = AssetDatabase.LoadAssetAtPath<SkillEffectDatabaseSO>(SkillEffectDatabasePath);
            if (sharedSkillDatabase == null)
            {
                errorMessage = $"未找到技能效果库：{SkillEffectDatabasePath}";
                animationLibrary = null;
                animatorController = null;
                inputActions = null;
                keyRebindConfig = null;
                return false;
            }

            animationLibrary = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(AnimationLibraryPath);
            if (animationLibrary == null)
            {
                errorMessage = $"未找到动画库：{AnimationLibraryPath}";
                animatorController = null;
                inputActions = null;
                keyRebindConfig = null;
                return false;
            }

            animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorControllerPath);
            if (animatorController == null)
            {
                errorMessage = $"未找到玩家 AnimatorController：{PlayerAnimatorControllerPath}";
                inputActions = null;
                keyRebindConfig = null;
                return false;
            }

            inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                errorMessage = $"未找到输入动作资产：{InputActionsPath}";
                keyRebindConfig = null;
                return false;
            }

            keyRebindConfig = AssetDatabase.LoadAssetAtPath<KeyRebindConfigSO>(KeyRebindConfigPath);
            if (keyRebindConfig == null)
            {
                errorMessage = $"未找到按键重绑定配置：{KeyRebindConfigPath}";
                return false;
            }

            return true;
        }

        private static int ResolveNextSkillIndex(
            SkillConfigDatabaseSO skillConfig,
            SkillEffectDatabaseSO sharedSkillDatabase,
            CharacterAnimationLibrarySO animationLibrary,
            AnimatorController animatorController,
            InputActionAsset inputActions,
            KeyRebindConfigSO keyRebindConfig)
        {
            int maxSkillIndex = -1;

            maxSkillIndex = Mathf.Max(maxSkillIndex, GetMaxSkillIndex(skillConfig?.entries, entry => entry != null ? entry.GetResolvedActionId() : string.Empty));

            SkillGroupDefinition sharedPlayerGroup = FindOrCreateSharedSkillGroup(sharedSkillDatabase);
            maxSkillIndex = Mathf.Max(maxSkillIndex, GetMaxSkillIndex(GetPrimarySharedSkillDefinitions(sharedPlayerGroup), entry => entry != null ? entry.skillId : string.Empty));

            CharacterAnimationGroupDefinition animationPlayerGroup = FindOrCreateAnimationGroup(animationLibrary);
            maxSkillIndex = Mathf.Max(maxSkillIndex, GetMaxSkillIndex(animationPlayerGroup?.entries, entry => entry != null ? entry.animationId : string.Empty));

            maxSkillIndex = Mathf.Max(maxSkillIndex, GetMaxAnimatorSkillIndex(animatorController));
            maxSkillIndex = Mathf.Max(maxSkillIndex, GetMaxInputSkillIndex(inputActions));
            maxSkillIndex = Mathf.Max(maxSkillIndex, GetMaxSkillIndex(keyRebindConfig?.entries, entry => entry != null ? entry.actionName : string.Empty));

            return maxSkillIndex + 1;
        }

        private static int GetMaxAnimatorSkillIndex(AnimatorController animatorController)
        {
            if (animatorController == null || animatorController.layers == null || animatorController.layers.Length == 0)
                return -1;

            AnimatorStateMachine stateMachine = animatorController.layers[0].stateMachine;
            int maxSkillIndex = -1;

            if (animatorController.parameters != null)
                maxSkillIndex = Mathf.Max(maxSkillIndex, GetMaxSkillIndex(animatorController.parameters, parameter => parameter != null ? parameter.name : string.Empty));

            if (stateMachine?.states != null)
            {
                for (int i = 0; i < stateMachine.states.Length; i++)
                {
                    AnimatorState state = stateMachine.states[i].state;
                    if (TryParseSkillStateName(state != null ? state.name : string.Empty, out int skillIndex))
                        maxSkillIndex = Mathf.Max(maxSkillIndex, skillIndex);
                }

                AnimatorStateTransition[] anyStateTransitions = stateMachine.anyStateTransitions;
                if (anyStateTransitions != null)
                {
                    for (int i = 0; i < anyStateTransitions.Length; i++)
                    {
                        AnimatorStateTransition transition = anyStateTransitions[i];
                        if (transition?.conditions == null)
                            continue;

                        for (int conditionIndex = 0; conditionIndex < transition.conditions.Length; conditionIndex++)
                        {
                            if (TryParseSkillName(transition.conditions[conditionIndex].parameter, out int skillIndex))
                                maxSkillIndex = Mathf.Max(maxSkillIndex, skillIndex);
                        }
                    }
                }
            }

            return maxSkillIndex;
        }

        private static int GetMaxInputSkillIndex(InputActionAsset inputActions)
        {
            InputActionMap gameplayMap = inputActions?.FindActionMap(GameplayActionMapName, false);
            if (gameplayMap == null)
                return -1;

            return GetMaxSkillIndex(gameplayMap.actions, action => action != null ? action.name : string.Empty);
        }

        private static int GetMaxSkillIndex<T>(IEnumerable<T> source, Func<T, string> selector)
        {
            int maxSkillIndex = -1;
            if (source == null || selector == null)
                return maxSkillIndex;

            foreach (T item in source)
            {
                if (TryParseSkillName(selector(item), out int skillIndex))
                    maxSkillIndex = Mathf.Max(maxSkillIndex, skillIndex);
            }

            return maxSkillIndex;
        }

        private static void AppendSkillConfigEntry(SkillConfigDatabaseSO skillConfig, SkillEffectDatabaseSO sharedSkillDatabase, string actionId, string displayName)
        {
            skillConfig.entries ??= new List<SkillConfigEntry>();

            SkillConfigEntry template = FindHighestSkillConfigEntry(skillConfig.entries);
            SkillConfigEntry created = template != null
                ? CloneManaged(template)
                : CreateDefaultSkillConfigEntry();

            created.actionId = actionId;
            created.skillId = actionId;
            created.skillEffectDatabase = sharedSkillDatabase;
            created.displayName = displayName;
            created.animationTrigger = actionId;
            created.isPassive = false;
            created.entryGroup = PlayerSkillEntryGroup.ActiveSkill;

            skillConfig.entries.Add(created);
        }

        private static bool RemoveSkillConfigEntry(SkillConfigDatabaseSO skillConfig, string actionId)
        {
            if (skillConfig?.entries == null || string.IsNullOrWhiteSpace(actionId))
                return false;

            bool removed = false;
            for (int i = skillConfig.entries.Count - 1; i >= 0; i--)
            {
                SkillConfigEntry entry = skillConfig.entries[i];
                if (entry == null || !entry.IsActiveSkill)
                    continue;

                bool matchesAction = string.Equals(entry.GetResolvedActionId(), actionId, StringComparison.Ordinal);
                bool matchesSkill = string.Equals(entry.skillId, actionId, StringComparison.Ordinal);
                if (!matchesAction && !matchesSkill)
                    continue;

                skillConfig.entries.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        private static void AppendSharedSkillDefinition(SkillEffectDatabaseSO sharedSkillDatabase, string actionId, string displayName)
        {
            SkillGroupDefinition group = FindOrCreateSharedSkillGroup(sharedSkillDatabase);
            group.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();

            SkillEffectVariantGroupDefinition existingVariantGroup = FindSharedSkillVariantGroup(group, actionId);
            SharedSkillDefinition existing = SkillEffectDatabaseSO.GetPrimaryEntry(existingVariantGroup);
            if (existing != null)
            {
                existing.skillId = actionId;
                existing.displayName = displayName;
                existingVariantGroup.groupName = actionId;
                NormalizePlayerSkillDefinitions(group);
                RebuildSharedSkillFlatEntries(sharedSkillDatabase);
                return;
            }

            SharedSkillDefinition template = FindHighestSharedSkillDefinition(GetPrimarySharedSkillDefinitions(group));
            SharedSkillDefinition created = template != null
                ? CloneManaged(template)
                : CreateDefaultSharedSkillDefinition();

            created.skillId = actionId;
            created.displayName = displayName;
            group.skillGroups.Add(new SkillEffectVariantGroupDefinition
            {
                groupName = actionId,
                entries = new List<SharedSkillDefinition> { created },
            });

            NormalizePlayerSkillDefinitions(group);
            RebuildSharedSkillFlatEntries(sharedSkillDatabase);
        }

        private static CharacterAnimationEntry AppendAnimationEntry(CharacterAnimationLibrarySO animationLibrary, string actionId)
        {
            CharacterAnimationGroupDefinition group = FindOrCreateAnimationGroup(animationLibrary);
            group.entries ??= new List<CharacterAnimationEntry>();

            CharacterAnimationEntry existing = FindAnimationEntry(group.entries, actionId);
            if (existing != null)
            {
                NormalizePlayerAnimationEntries(group);
                return existing;
            }

            CharacterAnimationEntry template = FindHighestAnimationEntry(group.entries);
            CharacterAnimationEntry created = template != null
                ? CloneManaged(template)
                : new CharacterAnimationEntry();

            created.animationId = actionId;
            group.entries.Add(created);

            NormalizePlayerAnimationEntries(group);
            return FindAnimationEntry(group.entries, actionId);
        }

        private static SkillConfigEntry FindHighestSkillConfigEntry(List<SkillConfigEntry> entries)
        {
            SkillConfigEntry result = null;
            int highestSkillIndex = -1;
            if (entries == null)
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                SkillConfigEntry entry = entries[i];
                if (entry == null || !entry.IsActiveSkill)
                    continue;

                if (!TryParseSkillName(entry.GetResolvedActionId(), out int skillIndex))
                    continue;

                if (skillIndex > highestSkillIndex)
                {
                    highestSkillIndex = skillIndex;
                    result = entry;
                }
            }

            return result;
        }

        private static SharedSkillDefinition FindHighestSharedSkillDefinition(List<SharedSkillDefinition> entries)
        {
            SharedSkillDefinition result = null;
            int highestSkillIndex = -1;
            if (entries == null)
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                SharedSkillDefinition entry = entries[i];
                if (entry == null || !TryParseSkillName(entry.skillId, out int skillIndex))
                    continue;

                if (skillIndex > highestSkillIndex)
                {
                    highestSkillIndex = skillIndex;
                    result = entry;
                }
            }

            return result;
        }

        private static CharacterAnimationEntry FindHighestAnimationEntry(List<CharacterAnimationEntry> entries)
        {
            CharacterAnimationEntry result = null;
            int highestSkillIndex = -1;
            if (entries == null)
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterAnimationEntry entry = entries[i];
                if (entry == null || !TryParseSkillName(entry.animationId, out int skillIndex))
                    continue;

                if (skillIndex > highestSkillIndex)
                {
                    highestSkillIndex = skillIndex;
                    result = entry;
                }
            }

            return result;
        }

        private static SkillConfigEntry CreateDefaultSkillConfigEntry()
        {
            return new SkillConfigEntry
            {
                isPassive = false,
                entryGroup = PlayerSkillEntryGroup.ActiveSkill,
                talentCost = 2,
                mpCost = 10,
                cooldownSeconds = 3f,
                passiveStatModifier = new StatModifier(),
                actionPolicies = new List<PlayerStateActionPolicyRule>
                {
                    new PlayerStateActionPolicyRule { action = GameAction.Dodge, policy = TransitionPolicy.Interrupt },
                    new PlayerStateActionPolicyRule { action = GameAction.Skill, policy = TransitionPolicy.Interrupt },
                    new PlayerStateActionPolicyRule { action = GameAction.ChargeStart, policy = TransitionPolicy.Interrupt },
                    new PlayerStateActionPolicyRule { action = GameAction.ShootCharge, policy = TransitionPolicy.Interrupt },
                    new PlayerStateActionPolicyRule { action = GameAction.NormalAttack, policy = TransitionPolicy.Buffer },
                    new PlayerStateActionPolicyRule { action = GameAction.Shoot, policy = TransitionPolicy.Buffer },
                    new PlayerStateActionPolicyRule { action = GameAction.ChargeRelease, policy = TransitionPolicy.Buffer },
                },
                pendingReleaseRules = new List<PlayerStatePendingReleaseRule>(),
                overrideNaturalExitNormalizedTime = true,
                naturalExitNormalizedTime = 0.9f,
                naturalExitTarget = PlayerStateNaturalExitTarget.IdleState,
            };
        }

        private static SharedSkillDefinition CreateDefaultSharedSkillDefinition()
        {
            return new SharedSkillDefinition
            {
                ignoreAnimationDamageEvents = false,
                damageEvents = new List<SkillDamageEvent>(),
                physicsEvents = new List<SkillPhysicsEvent>(),
                attributeEvents = new List<SkillAttributeEvent>(),
                vfxEvents = new List<SkillVfxEvent>(),
                sfxEvents = new List<SkillSfxEvent>(),
            };
        }

        private static T CloneManaged<T>(T source) where T : new()
        {
            T clone = new T();
            if (source != null)
                EditorUtility.CopySerializedManagedFieldsOnly(source, clone);

            return clone;
        }

        private static SkillGroupDefinition FindOrCreateSharedSkillGroup(SkillEffectDatabaseSO sharedSkillDatabase)
        {
            sharedSkillDatabase.Synchronize();
            sharedSkillDatabase.groups ??= new List<SkillGroupDefinition>();

            for (int i = 0; i < sharedSkillDatabase.groups.Count; i++)
            {
                SkillGroupDefinition group = sharedSkillDatabase.groups[i];
                if (group != null && (group.groupId == PlayerGroupId || group.groupName == PlayerGroupName))
                {
                    group.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();
                    return group;
                }
            }

            SkillGroupDefinition created = new SkillGroupDefinition
            {
                groupId = PlayerGroupId,
                groupName = PlayerGroupName,
                skillGroups = new List<SkillEffectVariantGroupDefinition>(),
                entries = new List<SharedSkillDefinition>(),
            };
            sharedSkillDatabase.groups.Add(created);
            return created;
        }

        private static CharacterAnimationGroupDefinition FindOrCreateAnimationGroup(CharacterAnimationLibrarySO animationLibrary)
        {
            animationLibrary.groups ??= new List<CharacterAnimationGroupDefinition>();

            for (int i = 0; i < animationLibrary.groups.Count; i++)
            {
                CharacterAnimationGroupDefinition group = animationLibrary.groups[i];
                if (group != null && (group.groupId == PlayerGroupId || group.groupName == PlayerGroupName))
                    return group;
            }

            CharacterAnimationGroupDefinition created = new CharacterAnimationGroupDefinition
            {
                groupId = PlayerGroupId,
                groupName = PlayerGroupName,
                entries = new List<CharacterAnimationEntry>(),
            };
            animationLibrary.groups.Add(created);
            return created;
        }

        private static SkillGroupDefinition FindSharedSkillGroup(SkillEffectDatabaseSO sharedSkillDatabase)
        {
            if (sharedSkillDatabase?.groups == null)
                return null;

            for (int i = 0; i < sharedSkillDatabase.groups.Count; i++)
            {
                SkillGroupDefinition group = sharedSkillDatabase.groups[i];
                if (group != null && (group.groupId == PlayerGroupId || group.groupName == PlayerGroupName))
                    return group;
            }

            return null;
        }

        private static CharacterAnimationGroupDefinition FindAnimationGroup(CharacterAnimationLibrarySO animationLibrary)
        {
            if (animationLibrary?.groups == null)
                return null;

            for (int i = 0; i < animationLibrary.groups.Count; i++)
            {
                CharacterAnimationGroupDefinition group = animationLibrary.groups[i];
                if (group != null && (group.groupId == PlayerGroupId || group.groupName == PlayerGroupName))
                    return group;
            }

            return null;
        }

        private static SkillEffectVariantGroupDefinition FindSharedSkillVariantGroup(SkillGroupDefinition group, string skillId)
        {
            return SkillEffectDatabaseSO.FindVariantGroup(group, skillId);
        }

        private static List<SharedSkillDefinition> GetPrimarySharedSkillDefinitions(SkillGroupDefinition group)
        {
            List<SharedSkillDefinition> result = new List<SharedSkillDefinition>();
            if (group?.skillGroups == null)
                return result;

            for (int i = 0; i < group.skillGroups.Count; i++)
            {
                SharedSkillDefinition primaryEntry = SkillEffectDatabaseSO.GetPrimaryEntry(group.skillGroups[i]);
                if (primaryEntry != null)
                    result.Add(primaryEntry);
            }

            return result;
        }

        private static SharedSkillDefinition FindSharedSkillDefinition(List<SharedSkillDefinition> entries, string skillId)
        {
            if (entries == null || string.IsNullOrWhiteSpace(skillId))
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                SharedSkillDefinition entry = entries[i];
                if (entry != null && string.Equals(entry.skillId, skillId, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static CharacterAnimationEntry FindAnimationEntry(List<CharacterAnimationEntry> entries, string animationId)
        {
            if (entries == null || string.IsNullOrWhiteSpace(animationId))
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterAnimationEntry entry = entries[i];
                if (entry != null && string.Equals(entry.animationId, animationId, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static void NormalizePlayerSkillDefinitions(SkillGroupDefinition group)
        {
            if (group?.skillGroups == null)
                return;

            List<SkillEffectVariantGroupDefinition> normalEntries = new List<SkillEffectVariantGroupDefinition>();
            List<SkillEffectVariantGroupDefinition> activeSkillEntries = new List<SkillEffectVariantGroupDefinition>();
            for (int i = 0; i < group.skillGroups.Count; i++)
            {
                SkillEffectVariantGroupDefinition entryGroup = group.skillGroups[i];
                SharedSkillDefinition entry = SkillEffectDatabaseSO.GetPrimaryEntry(entryGroup);
                if (entryGroup == null || entry == null)
                    continue;

                if (TryParseSkillName(entry.skillId, out _))
                    activeSkillEntries.Add(entryGroup);
                else
                    normalEntries.Add(entryGroup);
            }

            activeSkillEntries.Sort((left, right) => CompareSkillNames(
                SkillEffectDatabaseSO.GetPrimaryEntry(left)?.skillId,
                SkillEffectDatabaseSO.GetPrimaryEntry(right)?.skillId));

            group.skillGroups.Clear();
            group.skillGroups.AddRange(normalEntries);
            group.skillGroups.AddRange(activeSkillEntries);
        }

        private static void NormalizePlayerAnimationEntries(CharacterAnimationGroupDefinition group)
        {
            if (group?.entries == null)
                return;

            List<CharacterAnimationEntry> normalEntries = new List<CharacterAnimationEntry>();
            List<CharacterAnimationEntry> activeSkillEntries = new List<CharacterAnimationEntry>();
            for (int i = 0; i < group.entries.Count; i++)
            {
                CharacterAnimationEntry entry = group.entries[i];
                if (entry == null)
                    continue;

                if (TryParseSkillName(entry.animationId, out _))
                    activeSkillEntries.Add(entry);
                else
                    normalEntries.Add(entry);
            }

            activeSkillEntries.Sort((left, right) => CompareSkillNames(left?.animationId, right?.animationId));

            group.entries.Clear();
            group.entries.AddRange(normalEntries);
            group.entries.AddRange(activeSkillEntries);
        }

        private static int CompareSkillNames(string left, string right)
        {
            bool leftIsSkill = TryParseSkillName(left, out int leftIndex);
            bool rightIsSkill = TryParseSkillName(right, out int rightIndex);
            if (!leftIsSkill && !rightIsSkill)
                return 0;
            if (!leftIsSkill)
                return -1;
            if (!rightIsSkill)
                return 1;
            return leftIndex.CompareTo(rightIndex);
        }

        private static void RebuildSharedSkillFlatEntries(SkillEffectDatabaseSO sharedSkillDatabase)
        {
            sharedSkillDatabase.Synchronize();
        }

        private static void EnsureAnimatorSkillState(
            AnimatorController animatorController,
            string actionId,
            CharacterAnimationEntry animationEntry,
            out string errorMessage)
        {
            errorMessage = null;
            if (animatorController == null || animatorController.layers == null || animatorController.layers.Length == 0)
            {
                errorMessage = "玩家 AnimatorController 缺少基础层。";
                return;
            }

            AnimatorStateMachine stateMachine = animatorController.layers[0].stateMachine;
            if (stateMachine == null)
            {
                errorMessage = "玩家 AnimatorController 缺少状态机。";
                return;
            }

            if (!HasAnimatorParameter(animatorController, actionId))
                animatorController.AddParameter(actionId, AnimatorControllerParameterType.Trigger);

            string stateName = $"{actionId}State";
            AnimatorState state = FindAnimatorState(stateMachine, stateName);
            if (state == null)
            {
                if (!TryFindHighestSkillAnimatorState(stateMachine, out SkillAnimatorTemplateContext templateContext))
                {
                    errorMessage = "玩家 AnimatorController 中未找到可复制的主动技能状态模板。";
                    return;
                }

                Vector3 position = ResolveNextSkillStatePosition(templateContext.stateMachine, templateContext.childState.position);
                state = templateContext.stateMachine.AddState(stateName, position);
                CopyAnimatorStateSettings(templateContext.childState.state, state);
            }

            state.name = stateName;
            if (animationEntry?.clip != null)
                state.motion = animationEntry.clip;

            EnsureAnyStateTransition(stateMachine, state, actionId);
        }

        private static void RemoveSharedSkillDefinition(SkillEffectDatabaseSO sharedSkillDatabase, string actionId)
        {
            SkillGroupDefinition group = FindSharedSkillGroup(sharedSkillDatabase);
            if (group?.skillGroups == null || string.IsNullOrWhiteSpace(actionId))
                return;

            for (int i = group.skillGroups.Count - 1; i >= 0; i--)
            {
                SkillEffectVariantGroupDefinition variantGroup = group.skillGroups[i];
                if (variantGroup != null && string.Equals(SkillEffectDatabaseSO.GetVariantGroupName(variantGroup), actionId, StringComparison.Ordinal))
                    group.skillGroups.RemoveAt(i);
            }

            NormalizePlayerSkillDefinitions(group);
            RebuildSharedSkillFlatEntries(sharedSkillDatabase);
        }

        private static void RemoveAnimationEntry(CharacterAnimationLibrarySO animationLibrary, string actionId)
        {
            CharacterAnimationGroupDefinition group = FindAnimationGroup(animationLibrary);
            if (group?.entries == null || string.IsNullOrWhiteSpace(actionId))
                return;

            for (int i = group.entries.Count - 1; i >= 0; i--)
            {
                CharacterAnimationEntry entry = group.entries[i];
                if (entry != null && string.Equals(entry.animationId, actionId, StringComparison.Ordinal))
                    group.entries.RemoveAt(i);
            }

            NormalizePlayerAnimationEntries(group);
        }

        private static void RemoveAnimatorSkillArtifacts(AnimatorController animatorController, string actionId)
        {
            if (animatorController == null || string.IsNullOrWhiteSpace(actionId) ||
                animatorController.layers == null || animatorController.layers.Length == 0)
            {
                return;
            }

            AnimatorStateMachine stateMachine = animatorController.layers[0].stateMachine;
            if (stateMachine == null)
                return;

            string stateName = $"{actionId}State";
            RemoveAnimatorTransitionsRecursive(stateMachine, actionId, stateName);
            RemoveAnimatorStatesRecursive(stateMachine, stateName);
            RemoveAnimatorParameter(animatorController, actionId);
        }

        private static void RemoveAnimatorTransitionsRecursive(AnimatorStateMachine stateMachine, string actionId, string stateName)
        {
            if (stateMachine == null)
                return;

            if (stateMachine.anyStateTransitions != null)
            {
                for (int i = stateMachine.anyStateTransitions.Length - 1; i >= 0; i--)
                {
                    AnimatorStateTransition transition = stateMachine.anyStateTransitions[i];
                    if (ShouldRemoveAnimatorTransition(transition, actionId, stateName))
                        stateMachine.RemoveAnyStateTransition(transition);
                }
            }

            if (stateMachine.stateMachines == null)
                return;

            for (int i = 0; i < stateMachine.stateMachines.Length; i++)
                RemoveAnimatorTransitionsRecursive(stateMachine.stateMachines[i].stateMachine, actionId, stateName);
        }

        private static bool ShouldRemoveAnimatorTransition(AnimatorStateTransition transition, string actionId, string stateName)
        {
            if (transition == null)
                return false;

            if (transition.destinationState != null &&
                string.Equals(transition.destinationState.name, stateName, StringComparison.Ordinal))
            {
                return true;
            }

            if (transition.conditions == null)
                return false;

            for (int i = 0; i < transition.conditions.Length; i++)
            {
                if (string.Equals(transition.conditions[i].parameter, actionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void RemoveAnimatorStatesRecursive(AnimatorStateMachine stateMachine, string stateName)
        {
            if (stateMachine == null || string.IsNullOrWhiteSpace(stateName))
                return;

            if (stateMachine.stateMachines != null)
            {
                for (int i = 0; i < stateMachine.stateMachines.Length; i++)
                    RemoveAnimatorStatesRecursive(stateMachine.stateMachines[i].stateMachine, stateName);
            }

            if (stateMachine.states == null)
                return;

            for (int i = stateMachine.states.Length - 1; i >= 0; i--)
            {
                ChildAnimatorState childState = stateMachine.states[i];
                if (childState.state != null && string.Equals(childState.state.name, stateName, StringComparison.Ordinal))
                    stateMachine.RemoveState(childState.state);
            }
        }

        private static void RemoveAnimatorParameter(AnimatorController animatorController, string parameterName)
        {
            if (animatorController?.parameters == null || string.IsNullOrWhiteSpace(parameterName))
                return;

            for (int i = animatorController.parameters.Length - 1; i >= 0; i--)
            {
                AnimatorControllerParameter parameter = animatorController.parameters[i];
                if (parameter != null && string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                    animatorController.RemoveParameter(parameter);
            }
        }

        private static void EnsureAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState state, string triggerName)
        {
            AnimatorStateTransition existing = FindAnyStateTransition(stateMachine, state);
            AnimatorStateTransition template = FindSkillTemplateTransition(stateMachine);

            AnimatorStateTransition transition = existing ?? stateMachine.AddAnyStateTransition(state);
            if (template != null)
                CopyTransitionSettings(template, transition);

            ReplaceTransitionConditions(transition, triggerName);
        }

        private static void EnsureInputAction(InputActionAsset inputActions, int skillIndex)
        {
            InputActionMap gameplayMap = inputActions?.FindActionMap(GameplayActionMapName, true);
            if (gameplayMap == null)
                return;

            string actionName = $"Skill{skillIndex}";
            InputAction action = gameplayMap.FindAction(actionName, false);
            if (action == null)
            {
                action = gameplayMap.AddAction(actionName, InputActionType.Button);
                action.expectedControlType = "Button";
            }

            string bindingPath = ResolveDefaultSkillBindingPath(skillIndex);
            if (string.IsNullOrEmpty(bindingPath))
                return;

            if (FindNonCompositeBindingIndex(action) >= 0)
                return;

            action.AddBinding(bindingPath);
        }

        private static void EnsurePotionBindings(InputActionAsset inputActions)
        {
            InputActionMap gameplayMap = inputActions?.FindActionMap(GameplayActionMapName, true);
            if (gameplayMap == null)
                return;

            SetPrimaryBindingPath(gameplayMap, "UseHealthPotion", "<Keyboard>/r");
            SetPrimaryBindingPath(gameplayMap, "UseManaPotion", "<Keyboard>/t");
        }

        private static void SaveInputActionsAsset(InputActionAsset inputActions)
        {
            if (inputActions == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(inputActions);
            if (string.IsNullOrWhiteSpace(assetPath))
                assetPath = InputActionsPath;
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            string assetJson = inputActions.ToJson();
            string existingJson = File.Exists(assetPath) ? File.ReadAllText(assetPath) : string.Empty;
            if (string.Equals(assetJson, existingJson, StringComparison.Ordinal))
                return;

            File.WriteAllText(assetPath, assetJson);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        private static void RemoveInputAction(InputActionAsset inputActions, string actionName)
        {
            InputActionMap gameplayMap = inputActions?.FindActionMap(GameplayActionMapName, true);
            if (gameplayMap == null || string.IsNullOrWhiteSpace(actionName))
                return;

            InputAction action = gameplayMap.FindAction(actionName, false);
            if (action != null)
                action.RemoveAction();
        }

        private static void EnsureKeyRebindEntries(KeyRebindConfigSO keyRebindConfig, int skillIndex)
        {
            keyRebindConfig.entries ??= new List<KeyRebindEntry>();

            string actionName = $"Skill{skillIndex}";
            KeyRebindEntry existingSkillEntry = FindKeyRebindEntry(keyRebindConfig.entries, actionName);
            if (existingSkillEntry == null)
            {
                int insertIndex = ResolveSkillEntryInsertIndex(keyRebindConfig.entries);
                keyRebindConfig.entries.Insert(insertIndex, new KeyRebindEntry
                {
                    actionName = actionName,
                    displayName = $"技能{skillIndex + 1}",
                });
            }
            else
            {
                existingSkillEntry.displayName = $"技能{skillIndex + 1}";
            }

            EnsureKeyRebindDisplayName(keyRebindConfig.entries, "UseHealthPotion", "回血药剂");
            EnsureKeyRebindDisplayName(keyRebindConfig.entries, "UseManaPotion", "回蓝药剂");
        }

        private static void RemoveKeyRebindEntry(KeyRebindConfigSO keyRebindConfig, string actionName)
        {
            if (keyRebindConfig?.entries == null || string.IsNullOrWhiteSpace(actionName))
                return;

            for (int i = keyRebindConfig.entries.Count - 1; i >= 0; i--)
            {
                KeyRebindEntry entry = keyRebindConfig.entries[i];
                if (entry != null &&
                    string.Equals(entry.actionName, actionName, StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(entry.bindingPart))
                {
                    keyRebindConfig.entries.RemoveAt(i);
                }
            }

            EnsureKeyRebindDisplayName(keyRebindConfig.entries, "UseHealthPotion", "回血药剂");
            EnsureKeyRebindDisplayName(keyRebindConfig.entries, "UseManaPotion", "回蓝药剂");
        }

        private static bool HasAnimatorParameter(AnimatorController animatorController, string parameterName)
        {
            if (animatorController?.parameters == null)
                return false;

            for (int i = 0; i < animatorController.parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = animatorController.parameters[i];
                if (parameter != null && string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static AnimatorState FindAnimatorState(AnimatorStateMachine stateMachine, string stateName)
        {
            if (stateMachine == null || string.IsNullOrWhiteSpace(stateName))
                return null;

            AnimatorState localState = FindAnimatorStateInCurrentStateMachine(stateMachine, stateName);
            if (localState != null)
                return localState;

            if (stateMachine.stateMachines == null)
                return null;

            for (int i = 0; i < stateMachine.stateMachines.Length; i++)
            {
                AnimatorStateMachine childStateMachine = stateMachine.stateMachines[i].stateMachine;
                AnimatorState childState = FindAnimatorState(childStateMachine, stateName);
                if (childState != null)
                    return childState;
            }

            return null;
        }

        private static AnimatorState FindAnimatorStateInCurrentStateMachine(AnimatorStateMachine stateMachine, string stateName)
        {
            if (stateMachine?.states == null || string.IsNullOrWhiteSpace(stateName))
                return null;

            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                AnimatorState state = stateMachine.states[i].state;
                if (state != null && string.Equals(state.name, stateName, StringComparison.Ordinal))
                    return state;
            }

            return null;
        }

        private static bool TryFindHighestSkillAnimatorState(AnimatorStateMachine stateMachine, out SkillAnimatorTemplateContext result)
        {
            result = default;
            int highestSkillIndex = -1;
            TryFindHighestSkillAnimatorStateRecursive(stateMachine, ref highestSkillIndex, ref result);
            return highestSkillIndex >= 0 && result.stateMachine != null && result.childState.state != null;
        }

        private static void TryFindHighestSkillAnimatorStateRecursive(
            AnimatorStateMachine stateMachine,
            ref int highestSkillIndex,
            ref SkillAnimatorTemplateContext result)
        {
            if (stateMachine == null)
                return;

            if (stateMachine.states != null)
            {
                for (int i = 0; i < stateMachine.states.Length; i++)
                {
                    ChildAnimatorState childState = stateMachine.states[i];
                    if (!TryParseSkillStateName(childState.state != null ? childState.state.name : string.Empty, out int skillIndex))
                        continue;

                    if (skillIndex > highestSkillIndex)
                    {
                        highestSkillIndex = skillIndex;
                        result = new SkillAnimatorTemplateContext
                        {
                            stateMachine = stateMachine,
                            childState = childState,
                        };
                    }
                }
            }

            if (stateMachine.stateMachines == null)
                return;

            for (int i = 0; i < stateMachine.stateMachines.Length; i++)
                TryFindHighestSkillAnimatorStateRecursive(stateMachine.stateMachines[i].stateMachine, ref highestSkillIndex, ref result);
        }

        private static Vector3 ResolveNextSkillStatePosition(AnimatorStateMachine stateMachine, Vector3 fallbackPosition)
        {
            float maxY = fallbackPosition.y;
            float x = fallbackPosition.x;
            float z = fallbackPosition.z;

            if (stateMachine?.states != null)
            {
                for (int i = 0; i < stateMachine.states.Length; i++)
                {
                    ChildAnimatorState childState = stateMachine.states[i];
                    if (!TryParseSkillStateName(childState.state != null ? childState.state.name : string.Empty, out _))
                        continue;

                    x = childState.position.x;
                    z = childState.position.z;
                    maxY = Mathf.Max(maxY, childState.position.y);
                }
            }

            return new Vector3(x, maxY + 70f, z);
        }

        private static void CopyAnimatorStateSettings(AnimatorState template, AnimatorState destination)
        {
            if (template == null || destination == null)
                return;

            destination.motion = template.motion;
            destination.speed = template.speed;
            destination.cycleOffset = template.cycleOffset;
            destination.mirror = template.mirror;
            destination.iKOnFeet = template.iKOnFeet;
            destination.writeDefaultValues = template.writeDefaultValues;
            destination.tag = template.tag;
            destination.speedParameterActive = template.speedParameterActive;
            destination.speedParameter = template.speedParameter;
            destination.mirrorParameterActive = template.mirrorParameterActive;
            destination.mirrorParameter = template.mirrorParameter;
            destination.cycleOffsetParameterActive = template.cycleOffsetParameterActive;
            destination.cycleOffsetParameter = template.cycleOffsetParameter;
            destination.timeParameterActive = template.timeParameterActive;
            destination.timeParameter = template.timeParameter;
        }

        private static AnimatorStateTransition FindAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destinationState)
        {
            if (stateMachine?.anyStateTransitions == null || destinationState == null)
                return null;

            for (int i = 0; i < stateMachine.anyStateTransitions.Length; i++)
            {
                AnimatorStateTransition transition = stateMachine.anyStateTransitions[i];
                if (transition != null && transition.destinationState == destinationState)
                    return transition;
            }

            return null;
        }

        private static AnimatorStateTransition FindSkillTemplateTransition(AnimatorStateMachine stateMachine)
        {
            if (stateMachine?.anyStateTransitions == null)
                return null;

            AnimatorStateTransition result = null;
            int highestSkillIndex = -1;
            for (int i = 0; i < stateMachine.anyStateTransitions.Length; i++)
            {
                AnimatorStateTransition transition = stateMachine.anyStateTransitions[i];
                if (transition?.conditions == null)
                    continue;

                for (int conditionIndex = 0; conditionIndex < transition.conditions.Length; conditionIndex++)
                {
                    AnimatorCondition condition = transition.conditions[conditionIndex];
                    if (!TryParseSkillName(condition.parameter, out int skillIndex))
                        continue;

                    if (skillIndex > highestSkillIndex)
                    {
                        highestSkillIndex = skillIndex;
                        result = transition;
                    }
                }
            }

            return result;
        }

        private static void CopyTransitionSettings(AnimatorStateTransition template, AnimatorStateTransition destination)
        {
            if (template == null || destination == null)
                return;

            destination.duration = template.duration;
            destination.offset = template.offset;
            destination.exitTime = template.exitTime;
            destination.hasExitTime = template.hasExitTime;
            destination.hasFixedDuration = template.hasFixedDuration;
            destination.interruptionSource = template.interruptionSource;
            destination.orderedInterruption = template.orderedInterruption;
            destination.canTransitionToSelf = template.canTransitionToSelf;
            destination.mute = template.mute;
            destination.solo = template.solo;
        }

        private static void ReplaceTransitionConditions(AnimatorStateTransition transition, string triggerName)
        {
            if (transition == null)
                return;

            AnimatorCondition[] conditions = transition.conditions;
            for (int i = conditions.Length - 1; i >= 0; i--)
                transition.RemoveCondition(conditions[i]);

            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        }

        private static void SetPrimaryBindingPath(InputActionMap actionMap, string actionName, string path)
        {
            if (actionMap == null || string.IsNullOrWhiteSpace(actionName) || string.IsNullOrWhiteSpace(path))
                return;

            InputAction action = actionMap.FindAction(actionName, false);
            if (action == null)
                return;

            int bindingIndex = FindNonCompositeBindingIndex(action);
            if (bindingIndex >= 0)
            {
                action.ChangeBinding(bindingIndex).WithPath(path);
                return;
            }

            action.AddBinding(path);
        }

        private static int FindNonCompositeBindingIndex(InputAction action)
        {
            if (action == null || action.bindings.Count == 0)
                return -1;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (!binding.isComposite && !binding.isPartOfComposite)
                    return i;
            }

            return -1;
        }

        private static string ResolveDefaultSkillBindingPath(int skillIndex)
        {
            int keyNumber = skillIndex + 1;
            if (keyNumber >= 1 && keyNumber <= 9)
                return $"<Keyboard>/{keyNumber}";

            if (keyNumber == 10)
                return "<Keyboard>/0";

            return string.Empty;
        }

        private static KeyRebindEntry FindKeyRebindEntry(List<KeyRebindEntry> entries, string actionName)
        {
            if (entries == null || string.IsNullOrWhiteSpace(actionName))
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                KeyRebindEntry entry = entries[i];
                if (entry != null &&
                    string.Equals(entry.actionName, actionName, StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(entry.bindingPart))
                {
                    return entry;
                }
            }

            return null;
        }

        private static int ResolveSkillEntryInsertIndex(List<KeyRebindEntry> entries)
        {
            int insertIndex = 0;
            if (entries == null)
                return insertIndex;

            for (int i = 0; i < entries.Count; i++)
            {
                KeyRebindEntry entry = entries[i];
                if (entry != null && TryParseSkillName(entry.actionName, out _))
                    insertIndex = i + 1;
            }

            return insertIndex;
        }

        private static void EnsureKeyRebindDisplayName(List<KeyRebindEntry> entries, string actionName, string displayName)
        {
            KeyRebindEntry entry = FindKeyRebindEntry(entries, actionName);
            if (entry != null)
                entry.displayName = displayName;
        }

        private static bool TryParseSkillName(string value, out int skillIndex)
        {
            skillIndex = -1;
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("Skill", StringComparison.Ordinal))
                return false;

            string suffix = value.Substring("Skill".Length);
            return int.TryParse(suffix, out skillIndex) && skillIndex >= 0;
        }

        private static bool TryParseSkillStateName(string value, out int skillIndex)
        {
            skillIndex = -1;
            if (string.IsNullOrWhiteSpace(value) || !value.EndsWith("State", StringComparison.Ordinal))
                return false;

            string actionId = value.Substring(0, value.Length - "State".Length);
            return TryParseSkillName(actionId, out skillIndex);
        }
    }
}
