using System;
using System.Collections.Generic;
using System.IO;
using Game.Data;
using Game.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    internal static class EnemySkillAuthoringUtility
    {
        private struct SkillAnimatorTemplateContext
        {
            public AnimatorStateMachine stateMachine;
            public ChildAnimatorState childState;
        }

        private struct TransitionConditionSnapshot
        {
            public AnimatorConditionMode mode;
            public float threshold;
            public string parameter;
        }

        private const string EnemyStatsDatabasePath = "Assets/Resources/配置/敌人属性库.asset";
        private const string SkillEffectDatabasePath = "Assets/Resources/配置/技能效果库.asset";
        private const string AnimationLibraryPath = "Assets/Resources/配置/动画库.asset";
        private const string EnemyAnimatorTemplatePath = "Assets/外部导入/Animator/EnemyAnimator.controller";
        private const string EnemyPrefabFolder = "Assets/Resources/Prefabs";
        private const string EnemyConfigFolder = "Assets/Resources/配置";

        public static bool TryAppendEnemySkill(EnemyArchetypeSO archetype, out int createdSlotIndex, out string errorMessage)
        {
            createdSlotIndex = -1;
            errorMessage = null;

            if (!TryLoadDependencies(
                    archetype,
                    createDedicatedControllerIfMissing: true,
                    out EnemyStatsDatabaseSO enemyStatsDatabase,
                    out SkillEffectDatabaseSO sharedSkillDatabase,
                    out CharacterAnimationLibrarySO animationLibrary,
                    out AnimatorController animatorController,
                    out string dependencyError))
            {
                errorMessage = dependencyError;
                return false;
            }

            List<UnityEngine.Object> dirtyAssets = BuildDirtyAssetList(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            Undo.RecordObjects(dirtyAssets.ToArray(), "添加敌人技能");

            NormalizeEnemySkillAuthoring(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);

            int nextSlotIndex = ResolveNextSlotIndex(archetype.skillSlots);
            EnemySkillSlotBinding templateSlot = FindHighestSlotBinding(archetype.skillSlots);
            EnemySkillSlotBinding createdSlot = CloneSlotBinding(templateSlot);
            ApplyDefaultSlotMetadata(archetype, createdSlot, nextSlotIndex);

            archetype.skillSlots ??= new List<EnemySkillSlotBinding>();
            archetype.skillSlots.Add(createdSlot);
            SortSkillSlots(archetype.skillSlots);

            SharedSkillDefinition templateDefinition = templateSlot != null
                ? sharedSkillDatabase.GetEntry(templateSlot.skillId)
                : null;
            EnsureSharedSkillDefinition(sharedSkillDatabase, archetype, createdSlot, templateDefinition);

            CharacterAnimationEntry templateAnimationEntry = templateSlot != null
                ? FindAnimationEntry(FindOrCreateAnimationGroup(animationLibrary, archetype), ResolveExpectedAnimationTrigger(templateSlot.slotIndex))
                : null;
            CharacterAnimationEntry animationEntry = EnsureAnimationEntry(animationLibrary, archetype, createdSlot.animationTrigger, templateAnimationEntry);

            EnsureAnimatorSkillState(animatorController, createdSlot.animationTrigger, animationEntry, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage))
                return false;

            FinalizeChanges(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            createdSlotIndex = createdSlot.slotIndex;
            return true;
        }

        public static bool TryRemoveEnemySkill(EnemyArchetypeSO archetype, int slotIndex, out string errorMessage)
        {
            errorMessage = null;
            if (archetype == null)
            {
                errorMessage = "未找到敌人行为资产。";
                return false;
            }

            EnemySkillSlotBinding slot = archetype.GetSkillSlot(slotIndex);
            if (slot == null)
            {
                errorMessage = $"未找到槽位 {slotIndex}。";
                return false;
            }

            if (!TryLoadDependencies(
                    archetype,
                    createDedicatedControllerIfMissing: false,
                    out EnemyStatsDatabaseSO enemyStatsDatabase,
                    out SkillEffectDatabaseSO sharedSkillDatabase,
                    out CharacterAnimationLibrarySO animationLibrary,
                    out AnimatorController animatorController,
                    out string dependencyError))
            {
                errorMessage = dependencyError;
                return false;
            }

            List<UnityEngine.Object> dirtyAssets = BuildDirtyAssetList(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            Undo.RecordObjects(dirtyAssets.ToArray(), "删除敌人技能");

            NormalizeEnemySkillAuthoring(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);

            slot = archetype.GetSkillSlot(slotIndex);
            if (slot == null)
            {
                errorMessage = $"未找到槽位 {slotIndex}。";
                return false;
            }

            RemoveSkillSlot(archetype, slotIndex);
            RemoveSharedSkillDefinition(sharedSkillDatabase, archetype, BuildEnemySkillId(archetype, slotIndex));
            RemoveAnimationEntry(animationLibrary, archetype, ResolveExpectedAnimationTrigger(slotIndex));
            RemoveAnimatorSkillArtifacts(animatorController, ResolveExpectedAnimationTrigger(slotIndex));

            FinalizeChanges(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            return true;
        }

        public static bool TryProvisionDedicatedAnimatorController(EnemyArchetypeSO archetype, out string errorMessage)
        {
            errorMessage = null;
            if (!TryLoadDependencies(
                    archetype,
                    createDedicatedControllerIfMissing: true,
                    out EnemyStatsDatabaseSO enemyStatsDatabase,
                    out SkillEffectDatabaseSO sharedSkillDatabase,
                    out CharacterAnimationLibrarySO animationLibrary,
                    out AnimatorController animatorController,
                    out string dependencyError))
            {
                errorMessage = dependencyError;
                return false;
            }

            List<UnityEngine.Object> dirtyAssets = BuildDirtyAssetList(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            Undo.RecordObjects(dirtyAssets.ToArray(), "创建敌人专属动画控制器");
            NormalizeEnemySkillAuthoring(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            FinalizeChanges(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            return true;
        }

        public static bool TryNormalizeEnemySkillAuthoring(EnemyArchetypeSO archetype, out string errorMessage)
        {
            errorMessage = null;
            if (!TryLoadDependencies(
                    archetype,
                    createDedicatedControllerIfMissing: true,
                    out EnemyStatsDatabaseSO enemyStatsDatabase,
                    out SkillEffectDatabaseSO sharedSkillDatabase,
                    out CharacterAnimationLibrarySO animationLibrary,
                    out AnimatorController animatorController,
                    out string dependencyError))
            {
                errorMessage = dependencyError;
                return false;
            }

            List<UnityEngine.Object> dirtyAssets = BuildDirtyAssetList(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            Undo.RecordObjects(dirtyAssets.ToArray(), "规范化敌人技能资源");
            NormalizeEnemySkillAuthoring(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            FinalizeChanges(archetype, enemyStatsDatabase, sharedSkillDatabase, animationLibrary, animatorController);
            return true;
        }

        public static bool TryCleanupDeletedEnemyArchetypeArtifacts(EnemyArchetypeSO archetype, out string errorMessage)
        {
            errorMessage = null;
            if (archetype == null)
            {
                errorMessage = "未找到被删除的敌人行为资产。";
                return false;
            }

            string enemyId = ResolveEnemyId(archetype);
            if (string.IsNullOrWhiteSpace(enemyId))
                return true;

            EnemyStatsDatabaseSO enemyStatsDatabase = AssetDatabase.LoadAssetAtPath<EnemyStatsDatabaseSO>(EnemyStatsDatabasePath);
            SkillEffectDatabaseSO sharedSkillDatabase = AssetDatabase.LoadAssetAtPath<SkillEffectDatabaseSO>(SkillEffectDatabasePath);
            CharacterAnimationLibrarySO animationLibrary = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(AnimationLibraryPath);
            AnimatorController dedicatedAnimatorController = archetype.dedicatedAnimatorController as AnimatorController;
            EnemySpawnTaskDatabaseSO[] taskDatabases = LoadAssetsOfType<EnemySpawnTaskDatabaseSO>();
            LevelEnemySpawnPlanSO[] globalPlans = LoadAssetsOfType<LevelEnemySpawnPlanSO>();
            LevelLocalEnemySpawnPlanSO[] localPlans = LoadAssetsOfType<LevelLocalEnemySpawnPlanSO>();

            RemoveEnemyStatsGroup(enemyStatsDatabase, enemyId);
            RemoveEnemySharedSkillGroup(sharedSkillDatabase, enemyId);
            RemoveEnemyAnimationGroup(animationLibrary, enemyId);
            CleanupSpawnTaskReferences(enemyId, taskDatabases, globalPlans, localPlans);
            CleanupEnemyPrefabReferences(archetype, dedicatedAnimatorController);

            List<UnityEngine.Object> dirtyAssets = new List<UnityEngine.Object>();
            AddDirtyAsset(dirtyAssets, enemyStatsDatabase);
            AddDirtyAsset(dirtyAssets, sharedSkillDatabase);
            AddDirtyAsset(dirtyAssets, animationLibrary);
            AddDirtyAsset(dirtyAssets, dedicatedAnimatorController);
            AddDirtyAssets(dirtyAssets, taskDatabases);
            AddDirtyAssets(dirtyAssets, globalPlans);
            AddDirtyAssets(dirtyAssets, localPlans);
            FinalizeDirtyAssets(dirtyAssets);

            DeleteDedicatedAnimatorControllerIfOwned(archetype, dedicatedAnimatorController);
            return true;
        }

        private static bool TryLoadDependencies(
            EnemyArchetypeSO archetype,
            bool createDedicatedControllerIfMissing,
            out EnemyStatsDatabaseSO enemyStatsDatabase,
            out SkillEffectDatabaseSO sharedSkillDatabase,
            out CharacterAnimationLibrarySO animationLibrary,
            out AnimatorController animatorController,
            out string errorMessage)
        {
            errorMessage = null;
            enemyStatsDatabase = AssetDatabase.LoadAssetAtPath<EnemyStatsDatabaseSO>(EnemyStatsDatabasePath);
            sharedSkillDatabase = AssetDatabase.LoadAssetAtPath<SkillEffectDatabaseSO>(SkillEffectDatabasePath);
            animationLibrary = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(AnimationLibraryPath);
            animatorController = null;

            if (archetype == null)
            {
                errorMessage = "未找到敌人行为资产。";
                return false;
            }

            if (enemyStatsDatabase == null)
            {
                errorMessage = $"未找到敌人属性库：{EnemyStatsDatabasePath}";
                return false;
            }

            if (sharedSkillDatabase == null)
            {
                errorMessage = $"未找到技能效果库：{SkillEffectDatabasePath}";
                return false;
            }

            if (animationLibrary == null)
            {
                errorMessage = $"未找到动画库：{AnimationLibraryPath}";
                return false;
            }

            return TryGetOrCreateDedicatedAnimatorController(
                archetype,
                createDedicatedControllerIfMissing,
                out animatorController,
                out errorMessage);
        }

        private static bool TryGetOrCreateDedicatedAnimatorController(
            EnemyArchetypeSO archetype,
            bool createIfMissing,
            out AnimatorController animatorController,
            out string errorMessage)
        {
            animatorController = null;
            errorMessage = null;

            if (archetype == null)
            {
                errorMessage = "未找到敌人行为资产。";
                return false;
            }

            if (archetype.dedicatedAnimatorController != null)
            {
                animatorController = archetype.dedicatedAnimatorController as AnimatorController;
                if (animatorController == null)
                {
                    errorMessage = "敌人行为资产引用的专属动画控制器不是 AnimatorController。";
                    return false;
                }

                if (IsTemplateAnimatorController(animatorController))
                {
                    if (!createIfMissing)
                        return true;

                    animatorController = DuplicateTemplateAnimatorController(archetype, out errorMessage);
                    if (animatorController == null)
                        return false;

                    archetype.dedicatedAnimatorController = animatorController;
                }

                return true;
            }

            if (!createIfMissing)
                return true;

            animatorController = LoadDedicatedAnimatorControllerByConvention(archetype);
            if (animatorController == null)
            {
                animatorController = DuplicateTemplateAnimatorController(archetype, out errorMessage);
                if (animatorController == null)
                    return false;
            }

            archetype.dedicatedAnimatorController = animatorController;
            return true;
        }

        private static AnimatorController LoadDedicatedAnimatorControllerByConvention(EnemyArchetypeSO archetype)
        {
            string targetPath = ResolveDedicatedAnimatorControllerPath(archetype);
            if (string.IsNullOrWhiteSpace(targetPath))
                return null;
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
        }

        private static AnimatorController DuplicateTemplateAnimatorController(EnemyArchetypeSO archetype, out string errorMessage)
        {
            errorMessage = null;
            AnimatorController existingController = LoadDedicatedAnimatorControllerByConvention(archetype);
            if (existingController != null)
                return existingController;

            AnimatorController templateController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyAnimatorTemplatePath);
            if (templateController == null)
            {
                errorMessage = $"未找到敌人 AnimatorController 模板：{EnemyAnimatorTemplatePath}";
                return null;
            }

            string targetPath = ResolveDedicatedAnimatorControllerPath(archetype);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                errorMessage = "无法为敌人生成专属动画控制器路径。";
                return null;
            }

            string directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                errorMessage = $"目标动画控制器目录不存在：{directory}";
                return null;
            }

            if (!AssetDatabase.CopyAsset(EnemyAnimatorTemplatePath, targetPath))
            {
                errorMessage = $"复制敌人 AnimatorController 模板失败：{targetPath}";
                return null;
            }

            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AnimatorController duplicatedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
            if (duplicatedController == null)
            {
                errorMessage = $"复制后的敌人 AnimatorController 加载失败：{targetPath}";
                return null;
            }

            duplicatedController.name = Path.GetFileNameWithoutExtension(targetPath);
            return duplicatedController;
        }

        private static bool IsTemplateAnimatorController(AnimatorController animatorController)
        {
            if (animatorController == null)
                return false;

            string assetPath = AssetDatabase.GetAssetPath(animatorController);
            return string.Equals(assetPath, EnemyAnimatorTemplatePath, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDedicatedAnimatorControllerPath(EnemyArchetypeSO archetype)
        {
            string enemyId = ResolveEnemyId(archetype);
            if (string.IsNullOrWhiteSpace(enemyId))
                return null;

            string sanitizedEnemyId = SanitizeFileName(enemyId);
            return $"Assets/外部导入/Animator/EnemyAnimator_{sanitizedEnemyId}.controller";
        }

        private static string ResolveEnemyId(EnemyArchetypeSO archetype)
        {
            if (archetype == null)
                return string.Empty;

            string enemyId = archetype.GetResolvedEnemyId();
            return string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId.Trim();
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Enemy";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] buffer = value.Trim().ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (Array.IndexOf(invalidChars, buffer[i]) >= 0)
                    buffer[i] = '_';
            }

            return new string(buffer);
        }

        private static void NormalizeEnemySkillAuthoring(
            EnemyArchetypeSO archetype,
            EnemyStatsDatabaseSO enemyStatsDatabase,
            SkillEffectDatabaseSO sharedSkillDatabase,
            CharacterAnimationLibrarySO animationLibrary,
            AnimatorController animatorController)
        {
            if (archetype == null)
                return;

            archetype.skillSlots ??= new List<EnemySkillSlotBinding>();
            SortSkillSlots(archetype.skillSlots);

            NormalizeEnemyAnimatorController(animatorController);
            EnsureEnemyStatsEntry(enemyStatsDatabase, archetype);
            SkillGroupDefinition skillGroup = FindOrCreateSharedSkillGroup(sharedSkillDatabase, archetype);
            CharacterAnimationGroupDefinition animationGroup = FindOrCreateAnimationGroup(animationLibrary, archetype);

            for (int i = 0; i < archetype.skillSlots.Count; i++)
            {
                EnemySkillSlotBinding slot = archetype.skillSlots[i];
                if (slot == null)
                    continue;

                int slotIndex = Mathf.Max(0, slot.slotIndex);
                string expectedSkillId = BuildEnemySkillId(archetype, slotIndex);
                string expectedTrigger = ResolveExpectedAnimationTrigger(slotIndex);

                MoveOrCreateSharedSkillDefinition(sharedSkillDatabase, skillGroup, slot.skillId, expectedSkillId, ResolveExpectedDisplayName(slot, slotIndex));
                MoveOrCreateAnimationEntry(animationGroup, slot.animationTrigger, expectedTrigger);

                slot.slotIndex = slotIndex;
                slot.skillId = expectedSkillId;
                slot.displayName = ResolveExpectedDisplayName(slot, slotIndex);
                slot.animationTrigger = expectedTrigger;
            }

            NormalizeSharedSkillGroupOrder(skillGroup);
            NormalizeAnimationGroupOrder(animationGroup);
            enemyStatsDatabase?.SyncFlatEntries();
            RebuildSharedSkillFlatEntries(sharedSkillDatabase);
            SyncEnemyPrefabAnimatorControllers(archetype, animatorController);
        }

        private static void NormalizeEnemyAnimatorController(AnimatorController animatorController)
        {
            if (animatorController == null)
                return;

            EnsureNormalizedSkillParameters(animatorController);
            AnimatorStateMachine rootStateMachine = animatorController.layers != null && animatorController.layers.Length > 0
                ? animatorController.layers[0].stateMachine
                : null;
            NormalizeEnemyAnimatorStateMachine(rootStateMachine);
            RemoveLegacyCastSkillParameters(animatorController);
        }

        private static void EnsureNormalizedSkillParameters(AnimatorController animatorController)
        {
            if (animatorController?.parameters == null)
                return;

            HashSet<int> requiredIndices = new HashSet<int>();
            for (int i = 0; i < animatorController.parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = animatorController.parameters[i];
                if (parameter != null && TryParseEnemySkillName(parameter.name, out int skillIndex))
                    requiredIndices.Add(skillIndex);
            }

            foreach (int skillIndex in requiredIndices)
            {
                string triggerName = ResolveExpectedAnimationTrigger(skillIndex);
                if (!HasAnimatorParameter(animatorController, triggerName))
                    animatorController.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);
            }
        }

        private static void RemoveLegacyCastSkillParameters(AnimatorController animatorController)
        {
            if (animatorController?.parameters == null)
                return;

            List<string> toRemove = new List<string>();
            for (int i = 0; i < animatorController.parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = animatorController.parameters[i];
                if (parameter == null)
                    continue;

                if (TryParseLegacyEnemySkillName(parameter.name, out _))
                    toRemove.Add(parameter.name);
            }

            for (int i = 0; i < toRemove.Count; i++)
                RemoveAnimatorParameter(animatorController, toRemove[i]);
        }

        private static void NormalizeEnemyAnimatorStateMachine(AnimatorStateMachine stateMachine)
        {
            if (stateMachine == null)
                return;

            if (stateMachine.states != null)
            {
                for (int i = 0; i < stateMachine.states.Length; i++)
                {
                    AnimatorState state = stateMachine.states[i].state;
                    if (state != null && TryParseEnemySkillStateName(state.name, out int skillIndex))
                        state.name = ResolveExpectedAnimationTrigger(skillIndex);
                }
            }

            NormalizeAnyStateTransitions(stateMachine);

            if (stateMachine.stateMachines == null)
                return;

            for (int i = 0; i < stateMachine.stateMachines.Length; i++)
                NormalizeEnemyAnimatorStateMachine(stateMachine.stateMachines[i].stateMachine);
        }

        private static void NormalizeAnyStateTransitions(AnimatorStateMachine stateMachine)
        {
            if (stateMachine?.anyStateTransitions == null)
                return;

            for (int i = 0; i < stateMachine.anyStateTransitions.Length; i++)
            {
                AnimatorStateTransition transition = stateMachine.anyStateTransitions[i];
                if (transition == null || transition.conditions == null)
                    continue;

                List<TransitionConditionSnapshot> conditions = new List<TransitionConditionSnapshot>(transition.conditions.Length);
                for (int conditionIndex = 0; conditionIndex < transition.conditions.Length; conditionIndex++)
                {
                    AnimatorCondition existingCondition = transition.conditions[conditionIndex];
                    conditions.Add(new TransitionConditionSnapshot
                    {
                        mode = existingCondition.mode,
                        threshold = existingCondition.threshold,
                        parameter = existingCondition.parameter,
                    });
                }

                bool changed = false;
                for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
                {
                    TransitionConditionSnapshot condition = conditions[conditionIndex];
                    if (!TryParseEnemySkillName(condition.parameter, out int skillIndex))
                        continue;

                    string normalizedParameter = ResolveExpectedAnimationTrigger(skillIndex);
                    if (string.Equals(condition.parameter, normalizedParameter, StringComparison.Ordinal))
                        continue;

                    condition.parameter = normalizedParameter;
                    conditions[conditionIndex] = condition;
                    changed = true;
                }

                if (!changed)
                    continue;

                AnimatorCondition[] existingConditions = transition.conditions;
                for (int conditionIndex = existingConditions.Length - 1; conditionIndex >= 0; conditionIndex--)
                    transition.RemoveCondition(existingConditions[conditionIndex]);

                for (int conditionIndex = 0; conditionIndex < conditions.Count; conditionIndex++)
                {
                    TransitionConditionSnapshot condition = conditions[conditionIndex];
                    transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
                }
            }
        }

        private static void EnsureEnemyStatsEntry(EnemyStatsDatabaseSO enemyStatsDatabase, EnemyArchetypeSO archetype)
        {
            if (enemyStatsDatabase == null || archetype == null)
                return;

            enemyStatsDatabase.groups ??= new List<EnemyStatsGroupDefinition>();
            string enemyId = ResolveEnemyId(archetype);
            string displayName = ResolveEnemyGroupName(archetype);
            EnemyStatsGroupDefinition group = FindOrCreateEnemyStatsGroup(enemyStatsDatabase, enemyId, displayName);
            EnemyStatsEntry entry = FindEnemyStatsEntry(group, enemyId);
            if (entry == null)
            {
                entry = CreateDefaultEnemyStatsEntry(enemyStatsDatabase, archetype);
                group.entries ??= new List<EnemyStatsEntry>();
                group.entries.Add(entry);
            }

            entry.enemyId = enemyId;
            entry.displayName = displayName;
            entry.type = archetype.enemyType;
            group.groupId = enemyId;
            group.groupName = displayName;

            RemoveDuplicateEnemyStatsEntries(enemyStatsDatabase, group, enemyId, entry);
            enemyStatsDatabase.SyncFlatEntries();
        }

        private static EnemyStatsGroupDefinition FindOrCreateEnemyStatsGroup(
            EnemyStatsDatabaseSO enemyStatsDatabase,
            string groupId,
            string groupName)
        {
            for (int i = 0; i < enemyStatsDatabase.groups.Count; i++)
            {
                EnemyStatsGroupDefinition group = enemyStatsDatabase.groups[i];
                if (group == null)
                    continue;

                if (string.Equals(NormalizeIdentifier(group.groupId), NormalizeIdentifier(groupId), StringComparison.Ordinal))
                {
                    group.groupId = groupId;
                    group.groupName = groupName;
                    group.entries ??= new List<EnemyStatsEntry>();
                    return group;
                }
            }

            EnemyStatsEntry orphanEntry = enemyStatsDatabase.GetEntry(groupId);
            EnemyStatsGroupDefinition created = new EnemyStatsGroupDefinition
            {
                groupId = groupId,
                groupName = groupName,
                entries = new List<EnemyStatsEntry>(),
            };
            if (orphanEntry != null)
                created.entries.Add(orphanEntry);

            enemyStatsDatabase.groups.Add(created);
            return created;
        }

        private static EnemyStatsEntry FindEnemyStatsEntry(EnemyStatsGroupDefinition group, string enemyId)
        {
            if (group?.entries == null)
                return null;

            for (int i = 0; i < group.entries.Count; i++)
            {
                EnemyStatsEntry entry = group.entries[i];
                if (entry != null &&
                    string.Equals(NormalizeIdentifier(entry.enemyId), NormalizeIdentifier(enemyId), StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private static EnemyStatsEntry CreateDefaultEnemyStatsEntry(EnemyStatsDatabaseSO enemyStatsDatabase, EnemyArchetypeSO archetype)
        {
            EnemyStatsEntry template = enemyStatsDatabase.GetEntry(archetype.enemyType);
            if (template == null)
                template = new EnemyStatsEntry();

            return new EnemyStatsEntry
            {
                enemyId = ResolveEnemyId(archetype),
                displayName = ResolveEnemyGroupName(archetype),
                type = archetype.enemyType,
                baseHp = template.baseHp > 0f ? template.baseHp : 60f,
                baseAttack = template.baseAttack > 0f ? template.baseAttack : 10f,
                baseDefense = template.baseDefense,
                baseMoveSpeed = template.baseMoveSpeed > 0f ? template.baseMoveSpeed : 3.8f,
                goldMin = template.goldMin,
                goldMax = template.goldMax,
                baseExp = template.baseExp,
            };
        }

        private static void RemoveDuplicateEnemyStatsEntries(
            EnemyStatsDatabaseSO enemyStatsDatabase,
            EnemyStatsGroupDefinition ownerGroup,
            string enemyId,
            EnemyStatsEntry keepEntry)
        {
            if (enemyStatsDatabase?.groups == null)
                return;

            string normalizedEnemyId = NormalizeIdentifier(enemyId);
            for (int groupIndex = enemyStatsDatabase.groups.Count - 1; groupIndex >= 0; groupIndex--)
            {
                EnemyStatsGroupDefinition group = enemyStatsDatabase.groups[groupIndex];
                if (group?.entries == null)
                    continue;

                for (int entryIndex = group.entries.Count - 1; entryIndex >= 0; entryIndex--)
                {
                    EnemyStatsEntry entry = group.entries[entryIndex];
                    if (entry == null || ReferenceEquals(entry, keepEntry))
                        continue;

                    if (!string.Equals(NormalizeIdentifier(entry.enemyId), normalizedEnemyId, StringComparison.Ordinal))
                        continue;

                    group.entries.RemoveAt(entryIndex);
                }

                if (!ReferenceEquals(group, ownerGroup) && group.entries.Count == 0)
                    enemyStatsDatabase.groups.RemoveAt(groupIndex);
            }
        }

        private static void RemoveEnemyStatsGroup(EnemyStatsDatabaseSO enemyStatsDatabase, string enemyId)
        {
            if (enemyStatsDatabase?.groups == null || string.IsNullOrWhiteSpace(enemyId))
                return;

            string normalizedEnemyId = NormalizeIdentifier(enemyId);
            for (int groupIndex = enemyStatsDatabase.groups.Count - 1; groupIndex >= 0; groupIndex--)
            {
                EnemyStatsGroupDefinition group = enemyStatsDatabase.groups[groupIndex];
                if (group == null)
                    continue;

                if (string.Equals(NormalizeIdentifier(group.groupId), normalizedEnemyId, StringComparison.Ordinal))
                {
                    enemyStatsDatabase.groups.RemoveAt(groupIndex);
                    continue;
                }

                if (group.entries == null)
                    continue;

                for (int entryIndex = group.entries.Count - 1; entryIndex >= 0; entryIndex--)
                {
                    EnemyStatsEntry entry = group.entries[entryIndex];
                    if (entry != null &&
                        string.Equals(NormalizeIdentifier(entry.enemyId), normalizedEnemyId, StringComparison.Ordinal))
                    {
                        group.entries.RemoveAt(entryIndex);
                    }
                }

                if (group.entries.Count == 0)
                    enemyStatsDatabase.groups.RemoveAt(groupIndex);
            }

            enemyStatsDatabase.SyncFlatEntries();
        }

        private static void SyncEnemyPrefabAnimatorControllers(EnemyArchetypeSO archetype, AnimatorController animatorController)
        {
            if (archetype == null || animatorController == null)
                return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
            string enemyId = ResolveEnemyId(archetype);
            if (string.IsNullOrWhiteSpace(enemyId))
                return;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabRoot == null)
                    continue;

                EnemyController enemyController = prefabRoot.GetComponentInChildren<EnemyController>(true);
                Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
                if (enemyController == null || animator == null)
                    continue;

                SerializedObject enemySerializedObject = new SerializedObject(enemyController);
                SerializedProperty archetypeOverrideProperty = enemySerializedObject.FindProperty("_archetypeOverride");
                SerializedProperty enemyIdProperty = enemySerializedObject.FindProperty("_enemyId");

                bool matchesArchetype = archetypeOverrideProperty != null && archetypeOverrideProperty.objectReferenceValue == archetype;
                bool matchesEnemyId = enemyIdProperty != null &&
                                     string.Equals(NormalizeIdentifier(enemyIdProperty.stringValue), NormalizeIdentifier(enemyId), StringComparison.Ordinal);
                if (!matchesArchetype && !matchesEnemyId)
                    continue;

                if (animator.runtimeAnimatorController == animatorController)
                    continue;

                Undo.RecordObject(animator, "绑定敌人专属动画控制器");
                animator.runtimeAnimatorController = animatorController;
                EditorUtility.SetDirty(animator);
                PrefabUtility.SavePrefabAsset(prefabRoot);
            }
        }

        private static void CleanupEnemyPrefabReferences(EnemyArchetypeSO archetype, AnimatorController dedicatedAnimatorController)
        {
            if (archetype == null)
                return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
            AnimatorController templateAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyAnimatorTemplatePath);
            string enemyId = ResolveEnemyId(archetype);

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabRoot == null)
                    continue;

                EnemyController enemyController = prefabRoot.GetComponentInChildren<EnemyController>(true);
                Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
                if (enemyController == null)
                    continue;

                bool changed = false;
                SerializedObject enemySerializedObject = new SerializedObject(enemyController);
                SerializedProperty archetypeOverrideProperty = enemySerializedObject.FindProperty("_archetypeOverride");
                SerializedProperty enemyIdProperty = enemySerializedObject.FindProperty("_enemyId");
                bool matchesArchetype = archetypeOverrideProperty != null && archetypeOverrideProperty.objectReferenceValue == archetype;
                bool matchesEnemyId = enemyIdProperty != null &&
                                     string.Equals(NormalizeIdentifier(enemyIdProperty.stringValue), NormalizeIdentifier(enemyId), StringComparison.Ordinal);
                if (!matchesArchetype && !matchesEnemyId)
                    continue;

                if (archetypeOverrideProperty != null && archetypeOverrideProperty.objectReferenceValue == archetype)
                {
                    archetypeOverrideProperty.objectReferenceValue = null;
                    enemySerializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(enemyController);
                    changed = true;
                }

                if (animator != null && dedicatedAnimatorController != null && animator.runtimeAnimatorController == dedicatedAnimatorController)
                {
                    animator.runtimeAnimatorController = templateAnimatorController;
                    EditorUtility.SetDirty(animator);
                    changed = true;
                }

                if (changed)
                    PrefabUtility.SavePrefabAsset(prefabRoot);
            }
        }

        private static string NormalizeIdentifier(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static List<UnityEngine.Object> BuildDirtyAssetList(
            EnemyArchetypeSO archetype,
            EnemyStatsDatabaseSO enemyStatsDatabase,
            SkillEffectDatabaseSO sharedSkillDatabase,
            CharacterAnimationLibrarySO animationLibrary,
            AnimatorController animatorController)
        {
            List<UnityEngine.Object> dirtyAssets = new List<UnityEngine.Object>();
            AddDirtyAsset(dirtyAssets, archetype);
            AddDirtyAsset(dirtyAssets, enemyStatsDatabase);
            AddDirtyAsset(dirtyAssets, sharedSkillDatabase);
            AddDirtyAsset(dirtyAssets, animationLibrary);
            AddDirtyAsset(dirtyAssets, animatorController);
            return dirtyAssets;
        }

        private static void AddDirtyAsset(List<UnityEngine.Object> dirtyAssets, UnityEngine.Object asset)
        {
            if (asset != null && !dirtyAssets.Contains(asset))
                dirtyAssets.Add(asset);
        }

        private static void AddDirtyAssets<T>(List<UnityEngine.Object> dirtyAssets, T[] assets) where T : UnityEngine.Object
        {
            if (assets == null)
                return;

            for (int i = 0; i < assets.Length; i++)
                AddDirtyAsset(dirtyAssets, assets[i]);
        }

        private static T[] LoadAssetsOfType<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { EnemyConfigFolder });
            List<T> assets = new List<T>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets.ToArray();
        }

        private static EnemySkillSlotBinding CloneSlotBinding(EnemySkillSlotBinding template)
        {
            if (template == null)
            {
                return new EnemySkillSlotBinding
                {
                    cooldown = 1f,
                    castRange = 3f,
                    castDuration = 0.6f,
                    rotateToTargetOnCast = true,
                    naturalExitNormalizedTime = 0.9f,
                    naturalExitTarget = EnemyAnimationNaturalExitTarget.Locomotion,
                    phaseAvailability = EnemySkillPhaseAvailability.Always,
                };
            }

            return new EnemySkillSlotBinding
            {
                slotIndex = template.slotIndex,
                skillId = template.skillId,
                displayName = template.displayName,
                cooldown = template.cooldown,
                castRange = template.castRange,
                minCastRange = template.minCastRange,
                idealCastRange = template.idealCastRange,
                skillRole = template.skillRole,
                riskWeight = template.riskWeight,
                punishWeight = template.punishWeight,
                repeatPenalty = template.repeatPenalty,
                canUseUnderThreat = template.canUseUnderThreat,
                castDuration = template.castDuration,
                enterIdleAfterCast = template.enterIdleAfterCast,
                postCastIdleDuration = template.postCastIdleDuration,
                rotateToTargetOnCast = template.rotateToTargetOnCast,
                animationTrigger = template.animationTrigger,
                naturalExitNormalizedTime = template.naturalExitNormalizedTime,
                naturalExitTarget = template.naturalExitTarget,
                phaseAvailability = template.phaseAvailability,
            };
        }

        private static void ApplyDefaultSlotMetadata(EnemyArchetypeSO archetype, EnemySkillSlotBinding slot, int slotIndex)
        {
            if (slot == null)
                return;

            slot.slotIndex = slotIndex;
            slot.skillId = BuildEnemySkillId(archetype, slotIndex);
            slot.displayName = $"技能{slotIndex}";
            slot.animationTrigger = ResolveExpectedAnimationTrigger(slotIndex);
        }

        private static int ResolveNextSlotIndex(List<EnemySkillSlotBinding> skillSlots)
        {
            if (skillSlots == null || skillSlots.Count == 0)
                return 0;

            int maxSlotIndex = -1;
            for (int i = 0; i < skillSlots.Count; i++)
            {
                EnemySkillSlotBinding slot = skillSlots[i];
                if (slot != null)
                    maxSlotIndex = Mathf.Max(maxSlotIndex, slot.slotIndex);
            }

            return maxSlotIndex + 1;
        }

        private static EnemySkillSlotBinding FindHighestSlotBinding(List<EnemySkillSlotBinding> skillSlots)
        {
            EnemySkillSlotBinding result = null;
            int maxSlotIndex = -1;
            if (skillSlots == null)
                return null;

            for (int i = 0; i < skillSlots.Count; i++)
            {
                EnemySkillSlotBinding slot = skillSlots[i];
                if (slot == null || slot.slotIndex < maxSlotIndex)
                    continue;

                maxSlotIndex = slot.slotIndex;
                result = slot;
            }

            return result;
        }

        private static string BuildEnemySkillId(EnemyArchetypeSO archetype, int slotIndex)
        {
            return $"{ResolveEnemyId(archetype)}Skill{slotIndex}";
        }

        private static string ResolveExpectedAnimationTrigger(int slotIndex)
        {
            return $"Skill{slotIndex}";
        }

        private static string ResolveExpectedDisplayName(EnemySkillSlotBinding slot, int slotIndex)
        {
            if (slot != null && !string.IsNullOrWhiteSpace(slot.displayName))
                return slot.displayName.Trim();
            return $"技能{slotIndex}";
        }

        private static void EnsureSharedSkillDefinition(
            SkillEffectDatabaseSO sharedSkillDatabase,
            EnemyArchetypeSO archetype,
            EnemySkillSlotBinding slot,
            SharedSkillDefinition templateDefinition)
        {
            if (sharedSkillDatabase == null || archetype == null || slot == null)
                return;

            SkillGroupDefinition group = FindOrCreateSharedSkillGroup(sharedSkillDatabase, archetype);
            SkillEffectVariantGroupDefinition existingGroup = FindSharedSkillVariantGroup(group, slot.skillId);
            SharedSkillDefinition existing = SkillEffectDatabaseSO.GetPrimaryEntry(existingGroup);
            if (existing != null)
            {
                existing.skillId = slot.skillId;
                existing.displayName = slot.displayName;
                return;
            }

            SharedSkillDefinition created = CloneSharedSkillDefinition(templateDefinition);
            created.skillId = slot.skillId;
            created.displayName = slot.displayName;
            group.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();
            group.skillGroups.Add(new SkillEffectVariantGroupDefinition
            {
                groupName = slot.skillId,
                entries = new List<SharedSkillDefinition> { created },
            });
            NormalizeSharedSkillGroupOrder(group);
            RebuildSharedSkillFlatEntries(sharedSkillDatabase);
        }

        private static void MoveOrCreateSharedSkillDefinition(
            SkillEffectDatabaseSO sharedSkillDatabase,
            SkillGroupDefinition targetGroup,
            string currentSkillId,
            string expectedSkillId,
            string displayName)
        {
            if (sharedSkillDatabase == null || targetGroup == null || string.IsNullOrWhiteSpace(expectedSkillId))
                return;

            SkillGroupDefinition ownerGroup = null;
            SkillEffectVariantGroupDefinition variantGroup = FindSharedSkillVariantGroup(sharedSkillDatabase, expectedSkillId, out ownerGroup);
            if (variantGroup == null && !string.IsNullOrWhiteSpace(currentSkillId))
                variantGroup = FindSharedSkillVariantGroup(sharedSkillDatabase, currentSkillId, out ownerGroup);

            SharedSkillDefinition entry = SkillEffectDatabaseSO.GetPrimaryEntry(variantGroup);
            if (entry == null)
            {
                SharedSkillDefinition template = GetLastSharedSkillDefinition(targetGroup);
                entry = CloneSharedSkillDefinition(template);
                variantGroup = new SkillEffectVariantGroupDefinition
                {
                    groupName = expectedSkillId,
                    entries = new List<SharedSkillDefinition> { entry },
                };
                targetGroup.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();
                targetGroup.skillGroups.Add(variantGroup);
            }
            else
            {
                if (!ReferenceEquals(ownerGroup, targetGroup))
                    MoveSharedSkillDefinitionToGroup(sharedSkillDatabase, targetGroup, variantGroup);
            }

            entry.skillId = expectedSkillId;
            entry.displayName = displayName;
            if (variantGroup != null)
                variantGroup.groupName = expectedSkillId;
        }

        private static SharedSkillDefinition CloneSharedSkillDefinition(SharedSkillDefinition template)
        {
            if (template == null)
                return new SharedSkillDefinition();

            SharedSkillDefinition clone = new SharedSkillDefinition();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(template), clone);
            return clone;
        }

        private static SharedSkillDefinition GetLastSharedSkillDefinition(SkillGroupDefinition group)
        {
            if (group?.skillGroups == null || group.skillGroups.Count == 0)
                return null;

            for (int i = group.skillGroups.Count - 1; i >= 0; i--)
            {
                SharedSkillDefinition entry = SkillEffectDatabaseSO.GetPrimaryEntry(group.skillGroups[i]);
                if (entry != null)
                    return entry;
            }

            return null;
        }

        private static void MoveSharedSkillDefinitionToGroup(
            SkillEffectDatabaseSO sharedSkillDatabase,
            SkillGroupDefinition targetGroup,
            SkillEffectVariantGroupDefinition variantGroup)
        {
            if (sharedSkillDatabase?.groups == null || targetGroup == null || variantGroup == null)
                return;

            for (int groupIndex = 0; groupIndex < sharedSkillDatabase.groups.Count; groupIndex++)
            {
                SkillGroupDefinition group = sharedSkillDatabase.groups[groupIndex];
                if (group?.skillGroups == null)
                    continue;

                if (!group.skillGroups.Remove(variantGroup))
                    continue;

                targetGroup.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();
                if (!targetGroup.skillGroups.Contains(variantGroup))
                    targetGroup.skillGroups.Add(variantGroup);
                return;
            }
        }

        private static void RemoveSharedSkillDefinition(SkillEffectDatabaseSO sharedSkillDatabase, EnemyArchetypeSO archetype, string skillId)
        {
            if (sharedSkillDatabase?.groups == null || string.IsNullOrWhiteSpace(skillId))
                return;

            SkillGroupDefinition group = FindOrCreateSharedSkillGroup(sharedSkillDatabase, archetype);
            if (group?.skillGroups != null)
            {
                for (int i = group.skillGroups.Count - 1; i >= 0; i--)
                {
                    SkillEffectVariantGroupDefinition variantGroup = group.skillGroups[i];
                    if (variantGroup != null && string.Equals(SkillEffectDatabaseSO.GetVariantGroupName(variantGroup), skillId, StringComparison.Ordinal))
                        group.skillGroups.RemoveAt(i);
                }
            }

            RebuildSharedSkillFlatEntries(sharedSkillDatabase);
        }

        private static void RemoveEnemySharedSkillGroup(SkillEffectDatabaseSO sharedSkillDatabase, string enemyId)
        {
            if (sharedSkillDatabase?.groups == null || string.IsNullOrWhiteSpace(enemyId))
                return;

            string normalizedEnemyId = NormalizeIdentifier(enemyId);
            for (int groupIndex = sharedSkillDatabase.groups.Count - 1; groupIndex >= 0; groupIndex--)
            {
                SkillGroupDefinition group = sharedSkillDatabase.groups[groupIndex];
                if (group != null &&
                    string.Equals(NormalizeIdentifier(group.groupId), normalizedEnemyId, StringComparison.Ordinal))
                {
                    sharedSkillDatabase.groups.RemoveAt(groupIndex);
                }
            }

            RebuildSharedSkillFlatEntries(sharedSkillDatabase);
        }

        private static SkillGroupDefinition FindOrCreateSharedSkillGroup(SkillEffectDatabaseSO sharedSkillDatabase, EnemyArchetypeSO archetype)
        {
            sharedSkillDatabase.Synchronize();
            sharedSkillDatabase.groups ??= new List<SkillGroupDefinition>();

            string enemyId = ResolveEnemyId(archetype);
            for (int i = 0; i < sharedSkillDatabase.groups.Count; i++)
            {
                SkillGroupDefinition group = sharedSkillDatabase.groups[i];
                if (group != null && string.Equals(group.groupId, enemyId, StringComparison.Ordinal))
                {
                    group.groupName = ResolveEnemyGroupName(archetype);
                    group.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();
                    return group;
                }
            }

            SkillGroupDefinition created = new SkillGroupDefinition
            {
                groupId = enemyId,
                groupName = ResolveEnemyGroupName(archetype),
                skillGroups = new List<SkillEffectVariantGroupDefinition>(),
                entries = new List<SharedSkillDefinition>(),
            };
            sharedSkillDatabase.groups.Add(created);
            return created;
        }

        private static string ResolveEnemyGroupName(EnemyArchetypeSO archetype)
        {
            if (archetype == null)
                return "敌人";
            return !string.IsNullOrWhiteSpace(archetype.displayName)
                ? archetype.displayName.Trim()
                : ResolveEnemyId(archetype);
        }

        private static SharedSkillDefinition FindSharedSkillDefinition(SkillEffectDatabaseSO sharedSkillDatabase, string skillId)
        {
            return sharedSkillDatabase != null ? sharedSkillDatabase.GetEntry(skillId) : null;
        }

        private static SkillEffectVariantGroupDefinition FindSharedSkillVariantGroup(SkillEffectDatabaseSO sharedSkillDatabase, string skillId, out SkillGroupDefinition ownerGroup)
        {
            ownerGroup = null;
            if (sharedSkillDatabase?.groups == null || string.IsNullOrWhiteSpace(skillId))
                return null;

            for (int groupIndex = 0; groupIndex < sharedSkillDatabase.groups.Count; groupIndex++)
            {
                SkillGroupDefinition group = sharedSkillDatabase.groups[groupIndex];
                SkillEffectVariantGroupDefinition variantGroup = FindSharedSkillVariantGroup(group, skillId);
                if (variantGroup == null)
                    continue;

                ownerGroup = group;
                return variantGroup;
            }

            return null;
        }

        private static SkillEffectVariantGroupDefinition FindSharedSkillVariantGroup(SkillGroupDefinition group, string skillId)
        {
            return SkillEffectDatabaseSO.FindVariantGroup(group, skillId);
        }

        private static SharedSkillDefinition FindSharedSkillDefinition(SkillGroupDefinition group, string skillId)
        {
            SkillEffectVariantGroupDefinition variantGroup = FindSharedSkillVariantGroup(group, skillId);
            return SkillEffectDatabaseSO.GetPrimaryEntry(variantGroup);
        }

        private static CharacterAnimationEntry EnsureAnimationEntry(
            CharacterAnimationLibrarySO animationLibrary,
            EnemyArchetypeSO archetype,
            string animationId,
            CharacterAnimationEntry templateEntry)
        {
            CharacterAnimationGroupDefinition group = FindOrCreateAnimationGroup(animationLibrary, archetype);
            CharacterAnimationEntry existing = FindAnimationEntry(group, animationId);
            if (existing != null)
                return existing;

            CharacterAnimationEntry created = CloneAnimationEntry(templateEntry);
            created.animationId = animationId;
            group.entries ??= new List<CharacterAnimationEntry>();
            group.entries.Add(created);
            NormalizeAnimationGroupOrder(group);
            return created;
        }

        private static void MoveOrCreateAnimationEntry(CharacterAnimationGroupDefinition group, string currentAnimationId, string expectedAnimationId)
        {
            if (group == null || string.IsNullOrWhiteSpace(expectedAnimationId))
                return;

            CharacterAnimationEntry entry = FindAnimationEntry(group, expectedAnimationId);
            if (entry == null && !string.IsNullOrWhiteSpace(currentAnimationId))
                entry = FindAnimationEntry(group, currentAnimationId);

            if (entry == null)
            {
                CharacterAnimationEntry template = GetLastAnimationEntry(group);
                entry = CloneAnimationEntry(template);
                group.entries ??= new List<CharacterAnimationEntry>();
                group.entries.Add(entry);
            }

            entry.animationId = expectedAnimationId;
        }

        private static CharacterAnimationEntry CloneAnimationEntry(CharacterAnimationEntry template)
        {
            return new CharacterAnimationEntry
            {
                animationId = template != null ? template.animationId : string.Empty,
                clip = template != null ? template.clip : null,
            };
        }

        private static CharacterAnimationEntry GetLastAnimationEntry(CharacterAnimationGroupDefinition group)
        {
            if (group?.entries == null || group.entries.Count == 0)
                return null;
            return group.entries[group.entries.Count - 1];
        }

        private static CharacterAnimationGroupDefinition FindOrCreateAnimationGroup(CharacterAnimationLibrarySO animationLibrary, EnemyArchetypeSO archetype)
        {
            animationLibrary.groups ??= new List<CharacterAnimationGroupDefinition>();
            string enemyId = ResolveEnemyId(archetype);

            for (int i = 0; i < animationLibrary.groups.Count; i++)
            {
                CharacterAnimationGroupDefinition group = animationLibrary.groups[i];
                if (group != null && string.Equals(group.groupId, enemyId, StringComparison.Ordinal))
                {
                    group.groupName = ResolveEnemyGroupName(archetype);
                    group.entries ??= new List<CharacterAnimationEntry>();
                    return group;
                }
            }

            CharacterAnimationGroupDefinition created = new CharacterAnimationGroupDefinition
            {
                groupId = enemyId,
                groupName = ResolveEnemyGroupName(archetype),
                entries = new List<CharacterAnimationEntry>(),
            };
            animationLibrary.groups.Add(created);
            return created;
        }

        private static CharacterAnimationEntry FindAnimationEntry(CharacterAnimationGroupDefinition group, string animationId)
        {
            if (group?.entries == null || string.IsNullOrWhiteSpace(animationId))
                return null;

            for (int i = 0; i < group.entries.Count; i++)
            {
                CharacterAnimationEntry entry = group.entries[i];
                if (entry != null && string.Equals(entry.animationId, animationId, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static void RemoveAnimationEntry(CharacterAnimationLibrarySO animationLibrary, EnemyArchetypeSO archetype, string animationId)
        {
            CharacterAnimationGroupDefinition group = FindOrCreateAnimationGroup(animationLibrary, archetype);
            if (group?.entries == null || string.IsNullOrWhiteSpace(animationId))
                return;

            for (int i = group.entries.Count - 1; i >= 0; i--)
            {
                CharacterAnimationEntry entry = group.entries[i];
                if (entry != null && string.Equals(entry.animationId, animationId, StringComparison.Ordinal))
                    group.entries.RemoveAt(i);
            }
        }

        private static void RemoveEnemyAnimationGroup(CharacterAnimationLibrarySO animationLibrary, string enemyId)
        {
            if (animationLibrary?.groups == null || string.IsNullOrWhiteSpace(enemyId))
                return;

            string normalizedEnemyId = NormalizeIdentifier(enemyId);
            for (int groupIndex = animationLibrary.groups.Count - 1; groupIndex >= 0; groupIndex--)
            {
                CharacterAnimationGroupDefinition group = animationLibrary.groups[groupIndex];
                if (group != null &&
                    string.Equals(NormalizeIdentifier(group.groupId), normalizedEnemyId, StringComparison.Ordinal))
                {
                    animationLibrary.groups.RemoveAt(groupIndex);
                }
            }
        }

        private static void CleanupSpawnTaskReferences(
            string enemyId,
            EnemySpawnTaskDatabaseSO[] taskDatabases,
            LevelEnemySpawnPlanSO[] globalPlans,
            LevelLocalEnemySpawnPlanSO[] localPlans)
        {
            if (string.IsNullOrWhiteSpace(enemyId) || taskDatabases == null)
                return;

            string normalizedEnemyId = NormalizeIdentifier(enemyId);
            for (int databaseIndex = 0; databaseIndex < taskDatabases.Length; databaseIndex++)
            {
                EnemySpawnTaskDatabaseSO taskDatabase = taskDatabases[databaseIndex];
                if (taskDatabase?.tasks == null || taskDatabase.tasks.Count == 0)
                    continue;

                List<int> removedIndices = new List<int>();
                for (int taskIndex = taskDatabase.tasks.Count - 1; taskIndex >= 0; taskIndex--)
                {
                    EnemySpawnTaskDefinition task = taskDatabase.tasks[taskIndex];
                    if (task == null || string.IsNullOrWhiteSpace(task.specificSpawnId))
                        continue;

                    if (!string.Equals(NormalizeIdentifier(task.specificSpawnId), normalizedEnemyId, StringComparison.Ordinal))
                        continue;

                    taskDatabase.tasks.RemoveAt(taskIndex);
                    removedIndices.Add(taskIndex);
                }

                if (removedIndices.Count == 0)
                    continue;

                removedIndices.Sort();
                AdjustGlobalSpawnPlans(taskDatabase, globalPlans, removedIndices);
                AdjustLocalSpawnPlans(taskDatabase, localPlans, removedIndices);
            }
        }

        private static void AdjustGlobalSpawnPlans(
            EnemySpawnTaskDatabaseSO taskDatabase,
            LevelEnemySpawnPlanSO[] globalPlans,
            List<int> removedIndices)
        {
            if (globalPlans == null || removedIndices == null || removedIndices.Count == 0)
                return;

            for (int planIndex = 0; planIndex < globalPlans.Length; planIndex++)
            {
                LevelEnemySpawnPlanSO planAsset = globalPlans[planIndex];
                if (planAsset == null || planAsset.taskDatabase != taskDatabase || planAsset.plans == null)
                    continue;

                for (int definitionIndex = 0; definitionIndex < planAsset.plans.Count; definitionIndex++)
                {
                    LevelEnemyGlobalSpawnPlanDefinition definition = planAsset.plans[definitionIndex];
                    if (definition?.taskAssignments == null)
                        continue;

                    for (int assignmentIndex = definition.taskAssignments.Count - 1; assignmentIndex >= 0; assignmentIndex--)
                    {
                        GlobalEnemySpawnTaskAssignment assignment = definition.taskAssignments[assignmentIndex];
                        if (assignment == null)
                            continue;

                        if (removedIndices.Contains(assignment.taskIndex))
                        {
                            definition.taskAssignments.RemoveAt(assignmentIndex);
                            continue;
                        }

                        assignment.taskIndex -= CountRemovedIndicesBefore(removedIndices, assignment.taskIndex);
                    }
                }
            }
        }

        private static void AdjustLocalSpawnPlans(
            EnemySpawnTaskDatabaseSO taskDatabase,
            LevelLocalEnemySpawnPlanSO[] localPlans,
            List<int> removedIndices)
        {
            if (localPlans == null || removedIndices == null || removedIndices.Count == 0)
                return;

            for (int planIndex = 0; planIndex < localPlans.Length; planIndex++)
            {
                LevelLocalEnemySpawnPlanSO planAsset = localPlans[planIndex];
                if (planAsset == null || planAsset.taskDatabase != taskDatabase || planAsset.plans == null)
                    continue;

                for (int definitionIndex = planAsset.plans.Count - 1; definitionIndex >= 0; definitionIndex--)
                {
                    LevelLocalEnemySpawnPlanDefinition definition = planAsset.plans[definitionIndex];
                    if (definition == null)
                        continue;

                    if (removedIndices.Contains(definition.taskIndex))
                    {
                        planAsset.plans.RemoveAt(definitionIndex);
                        continue;
                    }

                    definition.taskIndex -= CountRemovedIndicesBefore(removedIndices, definition.taskIndex);
                }
            }
        }

        private static int CountRemovedIndicesBefore(List<int> removedIndices, int taskIndex)
        {
            int removedBefore = 0;
            for (int i = 0; i < removedIndices.Count; i++)
            {
                if (removedIndices[i] < taskIndex)
                    removedBefore++;
            }

            return removedBefore;
        }

        private static void EnsureAnimatorSkillState(
            AnimatorController animatorController,
            string triggerName,
            CharacterAnimationEntry animationEntry,
            out string errorMessage)
        {
            errorMessage = null;
            if (animatorController == null || animatorController.layers == null || animatorController.layers.Length == 0)
            {
                errorMessage = "敌人 AnimatorController 缺少基础层。";
                return;
            }

            AnimatorStateMachine stateMachine = animatorController.layers[0].stateMachine;
            if (stateMachine == null)
            {
                errorMessage = "敌人 AnimatorController 缺少状态机。";
                return;
            }

            NormalizeEnemyAnimatorController(animatorController);

            if (!HasAnimatorParameter(animatorController, triggerName))
                animatorController.AddParameter(triggerName, AnimatorControllerParameterType.Trigger);

            AnimatorState state = FindAnimatorState(stateMachine, triggerName);
            if (state == null)
            {
                if (!TryFindHighestSkillAnimatorState(stateMachine, out SkillAnimatorTemplateContext templateContext))
                {
                    errorMessage = "敌人 AnimatorController 中未找到可复制的技能状态模板。";
                    return;
                }

                Vector3 position = ResolveNextSkillStatePosition(templateContext.stateMachine, templateContext.childState.position);
                state = templateContext.stateMachine.AddState(triggerName, position);
                CopyAnimatorStateSettings(templateContext.childState.state, state);
            }

            state.name = triggerName;
            if (animationEntry?.clip != null)
                state.motion = animationEntry.clip;

            EnsureAnyStateTransition(stateMachine, state, triggerName);
        }

        private static void RemoveAnimatorSkillArtifacts(AnimatorController animatorController, string triggerName)
        {
            if (animatorController == null || string.IsNullOrWhiteSpace(triggerName))
                return;

            AnimatorStateMachine stateMachine = animatorController.layers != null && animatorController.layers.Length > 0
                ? animatorController.layers[0].stateMachine
                : null;
            if (stateMachine == null)
                return;

            RemoveAnimatorTransitionsRecursive(stateMachine, triggerName);
            RemoveAnimatorStatesRecursive(stateMachine, triggerName);
            RemoveAnimatorParameter(animatorController, triggerName);
        }

        private static void RemoveAnimatorTransitionsRecursive(AnimatorStateMachine stateMachine, string triggerName)
        {
            if (stateMachine == null)
                return;

            if (stateMachine.anyStateTransitions != null)
            {
                for (int i = stateMachine.anyStateTransitions.Length - 1; i >= 0; i--)
                {
                    AnimatorStateTransition transition = stateMachine.anyStateTransitions[i];
                    if (ShouldRemoveAnimatorTransition(transition, triggerName))
                        stateMachine.RemoveAnyStateTransition(transition);
                }
            }

            if (stateMachine.stateMachines == null)
                return;

            for (int i = 0; i < stateMachine.stateMachines.Length; i++)
                RemoveAnimatorTransitionsRecursive(stateMachine.stateMachines[i].stateMachine, triggerName);
        }

        private static bool ShouldRemoveAnimatorTransition(AnimatorStateTransition transition, string triggerName)
        {
            if (transition == null)
                return false;

            if (transition.destinationState != null &&
                string.Equals(transition.destinationState.name, triggerName, StringComparison.Ordinal))
            {
                return true;
            }

            if (transition.conditions == null)
                return false;

            for (int i = 0; i < transition.conditions.Length; i++)
            {
                string parameter = transition.conditions[i].parameter;
                if (string.Equals(parameter, triggerName, StringComparison.Ordinal))
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
                    if (!TryParseEnemySkillName(condition.parameter, out int skillIndex))
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

        private static void ReplaceTransitionConditions(AnimatorStateTransition transition, string triggerName)
        {
            if (transition == null)
                return;

            AnimatorCondition[] conditions = transition.conditions;
            for (int i = conditions.Length - 1; i >= 0; i--)
                transition.RemoveCondition(conditions[i]);

            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
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

        private static bool HasAnimatorParameter(AnimatorController animatorController, string parameterName)
        {
            if (animatorController?.parameters == null || string.IsNullOrWhiteSpace(parameterName))
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

            if (stateMachine.states != null)
            {
                for (int i = 0; i < stateMachine.states.Length; i++)
                {
                    AnimatorState state = stateMachine.states[i].state;
                    if (state != null && string.Equals(state.name, stateName, StringComparison.Ordinal))
                        return state;
                }
            }

            if (stateMachine.stateMachines == null)
                return null;

            for (int i = 0; i < stateMachine.stateMachines.Length; i++)
            {
                AnimatorState childState = FindAnimatorState(stateMachine.stateMachines[i].stateMachine, stateName);
                if (childState != null)
                    return childState;
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
                    if (!TryParseEnemySkillStateName(childState.state != null ? childState.state.name : string.Empty, out int skillIndex))
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
                    if (!TryParseEnemySkillStateName(childState.state != null ? childState.state.name : string.Empty, out _))
                        continue;

                    x = childState.position.x;
                    z = childState.position.z;
                    maxY = Mathf.Max(maxY, childState.position.y);
                }
            }

            return new Vector3(x, maxY + 70f, z);
        }

        private static bool TryParseEnemySkillName(string value, out int skillIndex)
        {
            skillIndex = -1;
            if (TryParseSkillNameWithPrefix(value, "Skill", out skillIndex))
                return true;
            return TryParseLegacyEnemySkillName(value, out skillIndex);
        }

        private static bool TryParseLegacyEnemySkillName(string value, out int skillIndex)
        {
            return TryParseSkillNameWithPrefix(value, "CastSkill", out skillIndex);
        }

        private static bool TryParseEnemySkillStateName(string value, out int skillIndex)
        {
            skillIndex = -1;
            if (TryParseEnemySkillName(value, out skillIndex))
                return true;

            if (string.IsNullOrWhiteSpace(value) || !value.EndsWith("State", StringComparison.Ordinal))
                return false;

            string actionId = value.Substring(0, value.Length - "State".Length);
            return TryParseEnemySkillName(actionId, out skillIndex);
        }

        private static bool TryParseSkillNameWithPrefix(string value, string prefix, out int skillIndex)
        {
            skillIndex = -1;
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string suffix = value.Substring(prefix.Length);
            return int.TryParse(suffix, out skillIndex) && skillIndex >= 0;
        }

        private static void NormalizeSharedSkillGroupOrder(SkillGroupDefinition group)
        {
            if (group?.skillGroups == null)
                return;

            group.skillGroups.Sort((left, right) => CompareEnemySkillNames(
                SkillEffectDatabaseSO.GetPrimaryEntry(left) != null ? SkillEffectDatabaseSO.GetPrimaryEntry(left).skillId : string.Empty,
                SkillEffectDatabaseSO.GetPrimaryEntry(right) != null ? SkillEffectDatabaseSO.GetPrimaryEntry(right).skillId : string.Empty));
        }

        private static void NormalizeAnimationGroupOrder(CharacterAnimationGroupDefinition group)
        {
            if (group?.entries == null)
                return;

            group.entries.Sort((left, right) => CompareEnemySkillNames(left != null ? left.animationId : string.Empty, right != null ? right.animationId : string.Empty));
        }

        private static int CompareEnemySkillNames(string left, string right)
        {
            bool leftIsSkill = TryParseEnemySkillOrderToken(left, out int leftIndex);
            bool rightIsSkill = TryParseEnemySkillOrderToken(right, out int rightIndex);

            if (!leftIsSkill && !rightIsSkill)
                return string.Compare(left, right, StringComparison.Ordinal);
            if (!leftIsSkill)
                return -1;
            if (!rightIsSkill)
                return 1;
            return leftIndex.CompareTo(rightIndex);
        }

        private static bool TryParseEnemySkillOrderToken(string value, out int skillIndex)
        {
            skillIndex = -1;
            if (TryParseEnemySkillName(value, out skillIndex))
                return true;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            int markerIndex = value.LastIndexOf("Skill", StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;

            string suffix = value.Substring(markerIndex + "Skill".Length);
            return int.TryParse(suffix, out skillIndex) && skillIndex >= 0;
        }

        private static void SortSkillSlots(List<EnemySkillSlotBinding> skillSlots)
        {
            if (skillSlots == null)
                return;

            skillSlots.Sort((left, right) =>
            {
                if (left == null && right == null) return 0;
                if (left == null) return 1;
                if (right == null) return -1;
                return left.slotIndex.CompareTo(right.slotIndex);
            });
        }

        private static void RemoveSkillSlot(EnemyArchetypeSO archetype, int slotIndex)
        {
            if (archetype?.skillSlots == null)
                return;

            for (int i = archetype.skillSlots.Count - 1; i >= 0; i--)
            {
                EnemySkillSlotBinding slot = archetype.skillSlots[i];
                if (slot != null && slot.slotIndex == slotIndex)
                {
                    archetype.skillSlots.RemoveAt(i);
                    return;
                }
            }
        }

        private static void RebuildSharedSkillFlatEntries(SkillEffectDatabaseSO sharedSkillDatabase)
        {
            if (sharedSkillDatabase == null)
                return;

            sharedSkillDatabase.Synchronize();
        }

        private static void FinalizeChanges(
            EnemyArchetypeSO archetype,
            EnemyStatsDatabaseSO enemyStatsDatabase,
            SkillEffectDatabaseSO sharedSkillDatabase,
            CharacterAnimationLibrarySO animationLibrary,
            AnimatorController animatorController)
        {
            EditorUtility.SetDirty(archetype);
            if (enemyStatsDatabase != null)
                EditorUtility.SetDirty(enemyStatsDatabase);
            if (sharedSkillDatabase != null)
                EditorUtility.SetDirty(sharedSkillDatabase);
            if (animationLibrary != null)
                EditorUtility.SetDirty(animationLibrary);
            if (animatorController != null)
                EditorUtility.SetDirty(animatorController);

            AssetDatabase.SaveAssetIfDirty(archetype);
            if (enemyStatsDatabase != null)
                AssetDatabase.SaveAssetIfDirty(enemyStatsDatabase);
            if (sharedSkillDatabase != null)
                AssetDatabase.SaveAssetIfDirty(sharedSkillDatabase);
            if (animationLibrary != null)
                AssetDatabase.SaveAssetIfDirty(animationLibrary);
            if (animatorController != null)
                AssetDatabase.SaveAssetIfDirty(animatorController);
        }

        private static void FinalizeDirtyAssets(List<UnityEngine.Object> dirtyAssets)
        {
            if (dirtyAssets == null)
                return;

            for (int i = 0; i < dirtyAssets.Count; i++)
            {
                UnityEngine.Object asset = dirtyAssets[i];
                if (asset == null)
                    continue;

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        private static void DeleteDedicatedAnimatorControllerIfOwned(EnemyArchetypeSO archetype, AnimatorController dedicatedAnimatorController)
        {
            if (archetype == null || dedicatedAnimatorController == null || IsTemplateAnimatorController(dedicatedAnimatorController))
                return;

            if (IsAnimatorControllerReferencedByOtherArchetypes(archetype, dedicatedAnimatorController))
                return;

            string controllerPath = AssetDatabase.GetAssetPath(dedicatedAnimatorController);
            if (string.IsNullOrWhiteSpace(controllerPath))
                return;

            AssetDatabase.DeleteAsset(controllerPath);
        }

        private static bool IsAnimatorControllerReferencedByOtherArchetypes(EnemyArchetypeSO deletingArchetype, AnimatorController dedicatedAnimatorController)
        {
            string[] archetypeGuids = AssetDatabase.FindAssets("t:EnemyArchetypeSO", new[] { EnemyConfigFolder });
            for (int i = 0; i < archetypeGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(archetypeGuids[i]);
                EnemyArchetypeSO archetype = AssetDatabase.LoadAssetAtPath<EnemyArchetypeSO>(assetPath);
                if (archetype == null || archetype == deletingArchetype)
                    continue;

                if (archetype.dedicatedAnimatorController == dedicatedAnimatorController)
                    return true;
            }

            return false;
        }
    }
}
