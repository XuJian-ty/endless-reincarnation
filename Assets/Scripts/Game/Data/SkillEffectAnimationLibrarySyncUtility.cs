#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Data
{
    public static class SkillEffectAnimationLibrarySyncUtility
    {
        private const string SkillConfigDatabasePath = "Assets/Resources/配置/玩家动作及技能配置库.asset";
        private const string SkillEffectDatabasePath = "Assets/Resources/配置/技能效果库.asset";
        private const string AnimationLibraryPath = "Assets/Resources/配置/动画库.asset";
        private const string PlayerGroupId = "player";
        private const string PlayerGroupName = "玩家";
        private const string VariantSuffix = "_Variant";

        private static bool s_isSyncing;

        public static bool TrySyncAll(bool saveAssets = false)
        {
            SkillConfigDatabaseSO skillConfig = AssetDatabase.LoadAssetAtPath<SkillConfigDatabaseSO>(SkillConfigDatabasePath);
            SkillEffectDatabaseSO database = AssetDatabase.LoadAssetAtPath<SkillEffectDatabaseSO>(SkillEffectDatabasePath);
            CharacterAnimationLibrarySO animationLibrary = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(AnimationLibraryPath);
            return TrySync(skillConfig, database, animationLibrary, saveAssets);
        }

        public static bool TrySyncFromDatabase(SkillEffectDatabaseSO database, bool saveAssets = false)
        {
            SkillConfigDatabaseSO skillConfig = AssetDatabase.LoadAssetAtPath<SkillConfigDatabaseSO>(SkillConfigDatabasePath);
            CharacterAnimationLibrarySO animationLibrary = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(AnimationLibraryPath);
            return TrySync(skillConfig, database, animationLibrary, saveAssets);
        }

        public static bool TrySyncFromSkillConfig(SkillConfigDatabaseSO skillConfig, bool saveAssets = false)
        {
            SkillEffectDatabaseSO database = AssetDatabase.LoadAssetAtPath<SkillEffectDatabaseSO>(SkillEffectDatabasePath);
            CharacterAnimationLibrarySO animationLibrary = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(AnimationLibraryPath);
            return TrySync(skillConfig, database, animationLibrary, saveAssets);
        }

        public static bool TrySyncVariantEntriesFromDatabase(
            SkillEffectDatabaseSO database,
            int groupIndex,
            int variantGroupIndex,
            bool saveAssets = false)
        {
            if (database?.groups == null || groupIndex < 0 || groupIndex >= database.groups.Count)
                return false;

            CharacterAnimationLibrarySO animationLibrary = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(AnimationLibraryPath);
            if (animationLibrary == null)
                return false;

            database.Synchronize();
            animationLibrary.Synchronize();

            SkillGroupDefinition skillGroup = database.groups[groupIndex];
            if (skillGroup?.skillGroups == null || variantGroupIndex < 0 || variantGroupIndex >= skillGroup.skillGroups.Count)
                return false;

            SkillEffectVariantGroupDefinition skillVariantGroup = skillGroup.skillGroups[variantGroupIndex];
            if (skillVariantGroup == null)
                return false;

            bool changed = SyncAnimationVariantGroupEntries(animationLibrary, skillGroup, skillVariantGroup, variantGroupIndex);
            if (!changed)
                return false;

            animationLibrary.Synchronize();
            EditorUtility.SetDirty(animationLibrary);
            if (saveAssets)
                AssetDatabase.SaveAssetIfDirty(animationLibrary);

            return true;
        }

        public static bool TrySync(
            SkillConfigDatabaseSO skillConfig,
            SkillEffectDatabaseSO database,
            CharacterAnimationLibrarySO animationLibrary,
            bool saveAssets = false)
        {
            if (s_isSyncing || skillConfig == null || database == null || animationLibrary == null)
                return false;

            s_isSyncing = true;
            try
            {
                bool databaseChanged = SyncPlayerSkillEffectGroup(skillConfig, database);
                database.Synchronize();
                animationLibrary.Synchronize();

                List<CharacterAnimationGroupDefinition> syncedGroups = BuildSyncedAnimationGroups(skillConfig, database, animationLibrary);
                bool animationChanged = !AreGroupsEquivalent(animationLibrary.groups, syncedGroups);
                if (animationChanged)
                {
                    animationLibrary.groups = syncedGroups;
                    animationLibrary.Synchronize();
                    EditorUtility.SetDirty(animationLibrary);
                }

                if (databaseChanged)
                    EditorUtility.SetDirty(database);

                if (saveAssets)
                {
                    if (databaseChanged)
                        AssetDatabase.SaveAssetIfDirty(database);
                    if (animationChanged)
                        AssetDatabase.SaveAssetIfDirty(animationLibrary);
                }

                return databaseChanged || animationChanged;
            }
            finally
            {
                s_isSyncing = false;
            }
        }

        private static bool SyncPlayerSkillEffectGroup(SkillConfigDatabaseSO skillConfig, SkillEffectDatabaseSO database)
        {
            database.Synchronize();

            SkillGroupDefinition playerGroup = FindOrCreatePlayerSkillGroup(database);
            bool changed = EnsurePlayerGroupIdentity(playerGroup);
            List<SkillEffectVariantGroupDefinition> syncedVariantGroups =
                BuildSyncedPlayerVariantGroups(skillConfig, playerGroup, ref changed);

            if (!AreVariantGroupsEquivalent(playerGroup.skillGroups, syncedVariantGroups))
            {
                playerGroup.skillGroups = syncedVariantGroups;
                changed = true;
            }

            if (changed)
                database.Synchronize();

            return changed;
        }

        private static List<SkillEffectVariantGroupDefinition> BuildSyncedPlayerVariantGroups(
            SkillConfigDatabaseSO skillConfig,
            SkillGroupDefinition playerGroup,
            ref bool changed)
        {
            List<SkillEffectVariantGroupDefinition> currentVariantGroups =
                playerGroup.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();

            List<SkillEffectVariantGroupDefinition> syncedVariantGroups = new List<SkillEffectVariantGroupDefinition>();
            HashSet<SkillEffectVariantGroupDefinition> usedVariantGroups = new HashSet<SkillEffectVariantGroupDefinition>();
            List<SkillConfigEntry> configuredEntries = CollectConfiguredPlayerEntries(skillConfig);

            for (int i = 0; i < configuredEntries.Count; i++)
            {
                SkillConfigEntry configEntry = configuredEntries[i];
                string skillId = NormalizeKey(configEntry?.GetResolvedSkillId());
                if (string.IsNullOrWhiteSpace(skillId))
                    continue;

                SkillEffectVariantGroupDefinition variantGroup = SkillEffectDatabaseSO.FindVariantGroup(playerGroup, skillId);
                if (variantGroup == null)
                {
                    variantGroup = CreatePlayerVariantGroup(configEntry);
                    changed = true;
                }

                if (EnsurePlayerVariantGroupIdentity(variantGroup, configEntry))
                    changed = true;

                syncedVariantGroups.Add(variantGroup);
                usedVariantGroups.Add(variantGroup);
            }

            for (int i = 0; i < currentVariantGroups.Count; i++)
            {
                SkillEffectVariantGroupDefinition variantGroup = currentVariantGroups[i];
                if (variantGroup == null || usedVariantGroups.Contains(variantGroup))
                    continue;

                syncedVariantGroups.Add(variantGroup);
            }

            return syncedVariantGroups;
        }

        private static bool EnsurePlayerVariantGroupIdentity(
            SkillEffectVariantGroupDefinition variantGroup,
            SkillConfigEntry configEntry)
        {
            if (variantGroup == null || configEntry == null)
                return false;

            bool changed = false;
            string skillId = NormalizeKey(configEntry.GetResolvedSkillId());
            string actionId = NormalizeKey(configEntry.GetResolvedActionId());

            variantGroup.entries ??= new List<SharedSkillDefinition>();
            if (variantGroup.entries.Count == 0)
            {
                variantGroup.entries.Add(CreateDefaultSkillDefinition(skillId, actionId, configEntry.displayName));
                changed = true;
            }

            SharedSkillDefinition primaryEntry = variantGroup.entries[0];
            if (primaryEntry == null)
            {
                primaryEntry = CreateDefaultSkillDefinition(skillId, actionId, configEntry.displayName);
                variantGroup.entries[0] = primaryEntry;
                changed = true;
            }

            if (!string.Equals(NormalizeKey(primaryEntry.skillId), skillId, StringComparison.Ordinal))
            {
                primaryEntry.skillId = skillId;
                changed = true;
            }

            if (!string.Equals(NormalizeKey(variantGroup.groupName), skillId, StringComparison.Ordinal))
            {
                variantGroup.groupName = skillId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(primaryEntry.displayName) && !string.IsNullOrWhiteSpace(configEntry.displayName))
            {
                primaryEntry.displayName = configEntry.displayName.Trim();
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(primaryEntry.animationTrigger) && !string.IsNullOrWhiteSpace(actionId))
            {
                primaryEntry.animationTrigger = actionId;
                changed = true;
            }

            EnsureSkillDefinitionLists(primaryEntry);
            return changed;
        }

        private static SkillEffectVariantGroupDefinition CreatePlayerVariantGroup(SkillConfigEntry configEntry)
        {
            string skillId = NormalizeKey(configEntry?.GetResolvedSkillId());
            string actionId = NormalizeKey(configEntry?.GetResolvedActionId());
            return new SkillEffectVariantGroupDefinition
            {
                groupName = skillId,
                entries = new List<SharedSkillDefinition>
                {
                    CreateDefaultSkillDefinition(skillId, actionId, configEntry != null ? configEntry.displayName : string.Empty)
                },
            };
        }

        private static SharedSkillDefinition CreateDefaultSkillDefinition(string skillId, string actionId, string displayName)
        {
            return new SharedSkillDefinition
            {
                skillId = skillId,
                displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim(),
                animationTrigger = actionId,
                damageEvents = new List<SkillDamageEvent>(),
                physicsEvents = new List<SkillPhysicsEvent>(),
                attributeEvents = new List<SkillAttributeEvent>(),
                vfxEvents = new List<SkillVfxEvent>(),
                sfxEvents = new List<SkillSfxEvent>(),
                speedEvents = new List<SkillSpeedEvent>(),
                cameraEvents = new List<SkillCameraEvent>(),
            };
        }

        private static void EnsureSkillDefinitionLists(SharedSkillDefinition definition)
        {
            if (definition == null)
                return;

            definition.damageEvents ??= new List<SkillDamageEvent>();
            definition.physicsEvents ??= new List<SkillPhysicsEvent>();
            definition.attributeEvents ??= new List<SkillAttributeEvent>();
            definition.vfxEvents ??= new List<SkillVfxEvent>();
            definition.sfxEvents ??= new List<SkillSfxEvent>();
            definition.speedEvents ??= new List<SkillSpeedEvent>();
            definition.cameraEvents ??= new List<SkillCameraEvent>();
        }
        private static List<CharacterAnimationGroupDefinition> BuildSyncedAnimationGroups(
            SkillConfigDatabaseSO skillConfig,
            SkillEffectDatabaseSO database,
            CharacterAnimationLibrarySO animationLibrary)
        {
            Dictionary<string, AnimationClip> existingClipByEntryId = BuildClipLookup(animationLibrary?.groups);
            Dictionary<string, AnimationClip> clipSearchCache = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            List<CharacterAnimationGroupDefinition> result = new List<CharacterAnimationGroupDefinition>();

            if (database?.groups == null)
                return result;

            for (int i = 0; i < database.groups.Count; i++)
            {
                SkillGroupDefinition skillGroup = database.groups[i];
                if (skillGroup == null)
                    continue;

                CharacterAnimationGroupDefinition syncedGroup = IsPlayerGroup(skillGroup)
                    ? BuildSyncedPlayerAnimationGroup(skillConfig, database, existingClipByEntryId, clipSearchCache)
                    : BuildSyncedNonPlayerAnimationGroup(skillGroup, existingClipByEntryId, clipSearchCache);

                if (syncedGroup != null)
                    result.Add(syncedGroup);
            }

            return result;
        }

        private static CharacterAnimationGroupDefinition BuildSyncedPlayerAnimationGroup(
            SkillConfigDatabaseSO skillConfig,
            SkillEffectDatabaseSO database,
            Dictionary<string, AnimationClip> existingClipByEntryId,
            Dictionary<string, AnimationClip> clipSearchCache)
        {
            SkillGroupDefinition playerSkillGroup = FindPlayerSkillGroup(database);
            if (playerSkillGroup == null)
                return null;

            List<CharacterAnimationVariantGroupDefinition> syncedVariantGroups = new List<CharacterAnimationVariantGroupDefinition>();
            List<SkillConfigEntry> configuredEntries = CollectConfiguredPlayerEntries(skillConfig);

            for (int i = 0; i < configuredEntries.Count; i++)
            {
                string skillId = NormalizeKey(configuredEntries[i]?.GetResolvedSkillId());
                if (string.IsNullOrWhiteSpace(skillId))
                    continue;

                SkillEffectVariantGroupDefinition variantGroup = SkillEffectDatabaseSO.FindVariantGroup(playerSkillGroup, skillId);
                CharacterAnimationVariantGroupDefinition syncedVariantGroup =
                    BuildSyncedPlayerAnimationVariantGroup(variantGroup, existingClipByEntryId, clipSearchCache);
                if (syncedVariantGroup != null)
                    syncedVariantGroups.Add(syncedVariantGroup);
            }

            return new CharacterAnimationGroupDefinition
            {
                groupId = PlayerGroupId,
                groupName = PlayerGroupName,
                skillGroups = syncedVariantGroups,
                entries = new List<CharacterAnimationEntry>(),
            };
        }

        private static CharacterAnimationGroupDefinition BuildSyncedNonPlayerAnimationGroup(
            SkillGroupDefinition skillGroup,
            Dictionary<string, AnimationClip> existingClipByEntryId,
            Dictionary<string, AnimationClip> clipSearchCache)
        {
            if (skillGroup == null)
                return null;

            List<CharacterAnimationVariantGroupDefinition> syncedVariantGroups = new List<CharacterAnimationVariantGroupDefinition>();

            if (skillGroup.skillGroups != null)
            {
                for (int variantGroupIndex = 0; variantGroupIndex < skillGroup.skillGroups.Count; variantGroupIndex++)
                {
                    SkillEffectVariantGroupDefinition variantGroup = skillGroup.skillGroups[variantGroupIndex];
                    CharacterAnimationVariantGroupDefinition syncedVariantGroup =
                        BuildSyncedNonPlayerAnimationVariantGroup(variantGroup, existingClipByEntryId, clipSearchCache);
                    if (syncedVariantGroup != null)
                        syncedVariantGroups.Add(syncedVariantGroup);
                }
            }

            return new CharacterAnimationGroupDefinition
            {
                groupId = NormalizeKey(skillGroup.groupId),
                groupName = NormalizeName(skillGroup.groupName, skillGroup.groupId, "敌人"),
                skillGroups = syncedVariantGroups,
                entries = new List<CharacterAnimationEntry>(),
            };
        }

        private static CharacterAnimationVariantGroupDefinition BuildSyncedPlayerAnimationVariantGroup(
            SkillEffectVariantGroupDefinition variantGroup,
            Dictionary<string, AnimationClip> existingClipByEntryId,
            Dictionary<string, AnimationClip> clipSearchCache)
        {
            if (variantGroup?.entries == null)
                return null;

            List<CharacterAnimationEntry> syncedEntries = new List<CharacterAnimationEntry>();
            for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
            {
                SharedSkillDefinition skill = variantGroup.entries[entryIndex];
                string animationId = NormalizeKey(skill?.skillId);
                if (skill == null || string.IsNullOrWhiteSpace(animationId))
                    continue;

                syncedEntries.Add(new CharacterAnimationEntry
                {
                    animationId = animationId,
                    clip = ResolveClip(skill, existingClipByEntryId, clipSearchCache),
                });
            }

            if (syncedEntries.Count == 0)
                return null;

            return new CharacterAnimationVariantGroupDefinition
            {
                groupName = NormalizeKey(SkillEffectDatabaseSO.GetVariantGroupName(variantGroup), syncedEntries[0].animationId),
                entries = syncedEntries,
            };
        }

        private static CharacterAnimationVariantGroupDefinition BuildSyncedNonPlayerAnimationVariantGroup(
            SkillEffectVariantGroupDefinition variantGroup,
            Dictionary<string, AnimationClip> existingClipByEntryId,
            Dictionary<string, AnimationClip> clipSearchCache)
        {
            if (variantGroup?.entries == null)
                return null;

            string primaryAnimationId = ResolveNonPlayerPrimaryAnimationId(SkillEffectDatabaseSO.GetPrimaryEntry(variantGroup));
            List<CharacterAnimationEntry> syncedEntries = new List<CharacterAnimationEntry>();
            for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
            {
                SharedSkillDefinition skill = variantGroup.entries[entryIndex];
                if (skill == null)
                    continue;

                string animationId = entryIndex == 0
                    ? primaryAnimationId
                    : NormalizeKey(skill.skillId);
                if (string.IsNullOrWhiteSpace(animationId))
                    continue;

                syncedEntries.Add(new CharacterAnimationEntry
                {
                    animationId = animationId,
                    clip = ResolveNonPlayerClip(skill, primaryAnimationId, existingClipByEntryId, clipSearchCache),
                });
            }

            if (syncedEntries.Count == 0)
                return null;

            return new CharacterAnimationVariantGroupDefinition
            {
                groupName = NormalizeKey(SkillEffectDatabaseSO.GetVariantGroupName(variantGroup), syncedEntries[0].animationId),
                entries = syncedEntries,
            };
        }

        private static AnimationClip ResolveClip(
            SharedSkillDefinition skill,
            Dictionary<string, AnimationClip> existingClipByEntryId,
            Dictionary<string, AnimationClip> clipSearchCache)
        {
            List<string> candidates = BuildPlayerClipLookupCandidates(skill);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (existingClipByEntryId.TryGetValue(candidates[i], out AnimationClip existingClip) && existingClip != null)
                    return existingClip;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                AnimationClip assetClip = FindClipAssetByName(candidates[i], clipSearchCache);
                if (assetClip != null)
                    return assetClip;
            }

            return null;
        }

        private static AnimationClip ResolveNonPlayerClip(
            SharedSkillDefinition skill,
            string primaryAnimationId,
            Dictionary<string, AnimationClip> existingClipByEntryId,
            Dictionary<string, AnimationClip> clipSearchCache)
        {
            List<string> candidates = BuildNonPlayerClipLookupCandidates(skill, primaryAnimationId);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (existingClipByEntryId.TryGetValue(candidates[i], out AnimationClip existingClip) && existingClip != null)
                    return existingClip;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                AnimationClip assetClip = FindClipAssetByName(candidates[i], clipSearchCache);
                if (assetClip != null)
                    return assetClip;
            }

            return null;
        }

        private static List<string> BuildPlayerClipLookupCandidates(SharedSkillDefinition skill)
        {
            List<string> result = new List<string>();

            string skillId = NormalizeKey(skill?.skillId);
            string resolvedTrigger = skill != null ? NormalizeKey(skill.GetResolvedAnimationTrigger()) : string.Empty;
            string primarySkillId = GetPrimaryVariantId(skillId);
            string primaryTrigger = GetPrimaryVariantId(resolvedTrigger);
            AddCandidate(result, skillId);
            AddCandidate(result, resolvedTrigger);
            AddCandidate(result, primarySkillId);
            AddCandidate(result, primaryTrigger);
            AddCandidate(result, ExtractTrailingSkillSlotToken(skillId));
            AddCandidate(result, ExtractTrailingSkillSlotToken(resolvedTrigger));
            AddCandidate(result, RemoveUnderscores(skillId));
            AddCandidate(result, RemoveUnderscores(resolvedTrigger));

            if (string.Equals(skillId, "PlayerDeath", StringComparison.Ordinal)
                || string.Equals(resolvedTrigger, "Dead", StringComparison.Ordinal))
            {
                AddCandidate(result, "Dead");
            }

            AppendPlayerAliasCandidates(result, skillId, resolvedTrigger);
            if (!string.Equals(primarySkillId, skillId, StringComparison.Ordinal)
                || !string.Equals(primaryTrigger, resolvedTrigger, StringComparison.Ordinal))
            {
                AppendPlayerAliasCandidates(result, primarySkillId, primaryTrigger);
            }

            return result;
        }

        private static List<string> BuildNonPlayerClipLookupCandidates(SharedSkillDefinition skill, string primaryAnimationId)
        {
            List<string> result = new List<string>();
            string skillId = NormalizeKey(skill != null ? skill.skillId : string.Empty);
            string primarySkillId = GetPrimaryVariantId(skillId);

            AddCandidate(result, skillId);
            AddCandidate(result, primarySkillId);
            AddCandidate(result, primaryAnimationId);
            AddCandidate(result, ExtractTrailingSkillSlotToken(skillId));
            AddCandidate(result, ExtractTrailingSkillSlotToken(primarySkillId));
            return result;
        }

        private static void AppendPlayerAliasCandidates(List<string> candidates, string skillId, string resolvedTrigger)
        {
            switch (skillId)
            {
                case "NormalIdle": AddCandidate(candidates, "Idle"); break;
                case "NormalWalk": AddCandidate(candidates, "Walk"); AddCandidate(candidates, "WalkForward"); break;
                case "NormalRun": AddCandidate(candidates, "Run"); break;
                case "AimIdle": AddCandidate(candidates, "Aim"); break;
                case "AimWalk": AddCandidate(candidates, "Rifle_WalkFwdLoop"); AddCandidate(candidates, "walk_Shoot"); break;
                case "AimRun": AddCandidate(candidates, "Rifle_RunFwdLoop"); break;
                case "Jump": AddCandidate(candidates, "JumpLoop"); AddCandidate(candidates, "JumoLoop"); AddCandidate(candidates, "JumpStart"); break;
                case "Dodge": AddCandidate(candidates, "DodgeForward"); AddCandidate(candidates, "LongDodge_Forward"); break;
                case "Land": AddCandidate(candidates, "JumpEnd"); break;
                case "ChargeLoop": AddCandidate(candidates, "ChargeLoop"); break;
                case "ChargeRelease": AddCandidate(candidates, "ChargeIaieleaseEnd"); AddCandidate(candidates, "ChargeIaielease"); AddCandidate(candidates, "ChargeThrustEnd"); break;
                case "FallAttackStart": AddCandidate(candidates, "FallAttackStart"); break;
                case "FallAttackLoop": AddCandidate(candidates, "FallAttackLoop"); break;
                case "FallAttackLand": AddCandidate(candidates, "FallAttackEnd"); break;
                case "Fall": AddCandidate(candidates, "JumpLoop"); AddCandidate(candidates, "JumoLoop"); break;
                case "HitStun": AddCandidate(candidates, "HitStun"); break;
            }

            if (string.Equals(resolvedTrigger, "NormalLocomotion", StringComparison.Ordinal))
            {
                AddCandidate(candidates, "Idle");
                AddCandidate(candidates, "Walk");
                AddCandidate(candidates, "Run");
            }
            else if (string.Equals(resolvedTrigger, "AimLocomotion", StringComparison.Ordinal))
            {
                AddCandidate(candidates, "Aim");
                AddCandidate(candidates, "Rifle_WalkFwdLoop");
                AddCandidate(candidates, "Rifle_RunFwdLoop");
            }
        }

        private static Dictionary<string, AnimationClip> BuildClipLookup(List<CharacterAnimationGroupDefinition> groups)
        {
            Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            if (groups == null)
                return result;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                CharacterAnimationGroupDefinition group = groups[groupIndex];
                if (group?.skillGroups == null)
                    continue;

                for (int variantGroupIndex = 0; variantGroupIndex < group.skillGroups.Count; variantGroupIndex++)
                {
                    CharacterAnimationVariantGroupDefinition variantGroup = group.skillGroups[variantGroupIndex];
                    if (variantGroup?.entries == null)
                        continue;

                    for (int entryIndex = 0; entryIndex < variantGroup.entries.Count; entryIndex++)
                    {
                        CharacterAnimationEntry entry = variantGroup.entries[entryIndex];
                        string key = NormalizeKey(entry?.animationId);
                        if (string.IsNullOrWhiteSpace(key) || entry == null || entry.clip == null || result.ContainsKey(key))
                            continue;

                        result.Add(key, entry.clip);
                    }
                }
            }

            return result;
        }

        private static AnimationClip FindClipAssetByName(string clipName, Dictionary<string, AnimationClip> clipSearchCache)
        {
            string normalizedClipName = NormalizeKey(clipName);
            if (string.IsNullOrWhiteSpace(normalizedClipName))
                return null;

            if (clipSearchCache.TryGetValue(normalizedClipName, out AnimationClip cachedClip))
                return cachedClip;

            string[] guids = AssetDatabase.FindAssets($"{normalizedClipName} t:AnimationClip", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : Path.GetFileNameWithoutExtension(assetPath);
                if (!string.Equals(fileName, normalizedClipName, StringComparison.OrdinalIgnoreCase))
                    continue;

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                clipSearchCache[normalizedClipName] = clip;
                return clip;
            }

            clipSearchCache[normalizedClipName] = null;
            return null;
        }
        private static bool AreGroupsEquivalent(
            List<CharacterAnimationGroupDefinition> currentGroups,
            List<CharacterAnimationGroupDefinition> newGroups)
        {
            int currentCount = currentGroups != null ? currentGroups.Count : 0;
            int newCount = newGroups != null ? newGroups.Count : 0;
            if (currentCount != newCount)
                return false;

            for (int groupIndex = 0; groupIndex < newCount; groupIndex++)
            {
                if (!AreGroupsEquivalent(currentGroups[groupIndex], newGroups[groupIndex]))
                    return false;
            }

            return true;
        }

        private static bool AreGroupsEquivalent(CharacterAnimationGroupDefinition currentGroup, CharacterAnimationGroupDefinition newGroup)
        {
            if (currentGroup == null || newGroup == null)
                return currentGroup == newGroup;

            if (!string.Equals(NormalizeKey(currentGroup.groupId), NormalizeKey(newGroup.groupId), StringComparison.Ordinal))
                return false;
            if (!string.Equals(NormalizeName(currentGroup.groupName), NormalizeName(newGroup.groupName), StringComparison.Ordinal))
                return false;

            int currentVariantGroupCount = currentGroup.skillGroups != null ? currentGroup.skillGroups.Count : 0;
            int newVariantGroupCount = newGroup.skillGroups != null ? newGroup.skillGroups.Count : 0;
            if (currentVariantGroupCount != newVariantGroupCount)
                return false;

            for (int variantGroupIndex = 0; variantGroupIndex < newVariantGroupCount; variantGroupIndex++)
            {
                if (!AreAnimationVariantGroupsEquivalent(currentGroup.skillGroups[variantGroupIndex], newGroup.skillGroups[variantGroupIndex]))
                    return false;
            }

            return true;
        }

        private static bool AreAnimationVariantGroupsEquivalent(
            CharacterAnimationVariantGroupDefinition currentGroup,
            CharacterAnimationVariantGroupDefinition newGroup)
        {
            if (currentGroup == null || newGroup == null)
                return currentGroup == newGroup;

            if (!string.Equals(
                    NormalizeKey(CharacterAnimationLibrarySO.GetVariantGroupName(currentGroup)),
                    NormalizeKey(CharacterAnimationLibrarySO.GetVariantGroupName(newGroup)),
                    StringComparison.Ordinal))
            {
                return false;
            }

            int currentEntryCount = currentGroup.entries != null ? currentGroup.entries.Count : 0;
            int newEntryCount = newGroup.entries != null ? newGroup.entries.Count : 0;
            if (currentEntryCount != newEntryCount)
                return false;

            for (int entryIndex = 0; entryIndex < newEntryCount; entryIndex++)
            {
                if (!AreEntriesEquivalent(currentGroup.entries[entryIndex], newGroup.entries[entryIndex]))
                    return false;
            }

            return true;
        }

        private static bool AreEntriesEquivalent(CharacterAnimationEntry currentEntry, CharacterAnimationEntry newEntry)
        {
            if (currentEntry == null || newEntry == null)
                return currentEntry == newEntry;

            return string.Equals(NormalizeKey(currentEntry.animationId), NormalizeKey(newEntry.animationId), StringComparison.Ordinal)
                   && currentEntry.clip == newEntry.clip;
        }

        private static bool AreVariantGroupsEquivalent(
            List<SkillEffectVariantGroupDefinition> currentGroups,
            List<SkillEffectVariantGroupDefinition> newGroups)
        {
            int currentCount = currentGroups != null ? currentGroups.Count : 0;
            int newCount = newGroups != null ? newGroups.Count : 0;
            if (currentCount != newCount)
                return false;

            for (int groupIndex = 0; groupIndex < newCount; groupIndex++)
            {
                if (!AreVariantGroupsEquivalent(currentGroups[groupIndex], newGroups[groupIndex]))
                    return false;
            }

            return true;
        }

        private static bool AreVariantGroupsEquivalent(
            SkillEffectVariantGroupDefinition currentGroup,
            SkillEffectVariantGroupDefinition newGroup)
        {
            if (currentGroup == null || newGroup == null)
                return currentGroup == newGroup;

            if (!string.Equals(
                    NormalizeKey(SkillEffectDatabaseSO.GetVariantGroupName(currentGroup)),
                    NormalizeKey(SkillEffectDatabaseSO.GetVariantGroupName(newGroup)),
                    StringComparison.Ordinal))
            {
                return false;
            }

            int currentEntryCount = currentGroup.entries != null ? currentGroup.entries.Count : 0;
            int newEntryCount = newGroup.entries != null ? newGroup.entries.Count : 0;
            if (currentEntryCount != newEntryCount)
                return false;

            for (int entryIndex = 0; entryIndex < newEntryCount; entryIndex++)
            {
                string currentSkillId = NormalizeKey(currentGroup.entries[entryIndex] != null ? currentGroup.entries[entryIndex].skillId : string.Empty);
                string newSkillId = NormalizeKey(newGroup.entries[entryIndex] != null ? newGroup.entries[entryIndex].skillId : string.Empty);
                if (!string.Equals(currentSkillId, newSkillId, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static List<SkillConfigEntry> CollectConfiguredPlayerEntries(SkillConfigDatabaseSO skillConfig)
        {
            List<SkillConfigEntry> result = new List<SkillConfigEntry>();
            if (skillConfig?.entries == null)
                return result;

            for (int i = 0; i < skillConfig.entries.Count; i++)
            {
                SkillConfigEntry entry = skillConfig.entries[i];
                if (entry == null || entry.IsPassiveSkill || string.IsNullOrWhiteSpace(entry.GetResolvedSkillId()))
                    continue;

                result.Add(entry);
            }

            return result;
        }

        private static SkillGroupDefinition FindOrCreatePlayerSkillGroup(SkillEffectDatabaseSO database)
        {
            database.groups ??= new List<SkillGroupDefinition>();
            SkillGroupDefinition existing = FindPlayerSkillGroup(database);
            if (existing != null)
                return existing;

            SkillGroupDefinition created = new SkillGroupDefinition
            {
                groupId = PlayerGroupId,
                groupName = PlayerGroupName,
                skillGroups = new List<SkillEffectVariantGroupDefinition>(),
                entries = new List<SharedSkillDefinition>(),
            };
            database.groups.Insert(0, created);
            return created;
        }

        private static SkillGroupDefinition FindPlayerSkillGroup(SkillEffectDatabaseSO database)
        {
            if (database?.groups == null)
                return null;

            for (int i = 0; i < database.groups.Count; i++)
            {
                if (IsPlayerGroup(database.groups[i]))
                    return database.groups[i];
            }

            return null;
        }

        private static bool EnsurePlayerGroupIdentity(SkillGroupDefinition group)
        {
            if (group == null)
                return false;

            bool changed = false;
            if (!string.Equals(NormalizeKey(group.groupId), PlayerGroupId, StringComparison.Ordinal))
            {
                group.groupId = PlayerGroupId;
                changed = true;
            }

            if (!string.Equals(NormalizeName(group.groupName), PlayerGroupName, StringComparison.Ordinal))
            {
                group.groupName = PlayerGroupName;
                changed = true;
            }

            group.skillGroups ??= new List<SkillEffectVariantGroupDefinition>();
            group.entries ??= new List<SharedSkillDefinition>();
            return changed;
        }

        private static bool IsPlayerGroup(SkillGroupDefinition group)
        {
            return group != null && string.Equals(NormalizeKey(group.groupId), PlayerGroupId, StringComparison.Ordinal);
        }

        private static bool IsPlayerGroup(CharacterAnimationGroupDefinition group)
        {
            return group != null && string.Equals(NormalizeKey(group.groupId), PlayerGroupId, StringComparison.Ordinal);
        }

        private static string ResolveNonPlayerPrimaryAnimationId(SharedSkillDefinition skill)
        {
            return NormalizeKey(skill != null ? skill.skillId : string.Empty);
        }

        private static bool SyncAnimationVariantGroupEntries(
            CharacterAnimationLibrarySO animationLibrary,
            SkillGroupDefinition skillGroup,
            SkillEffectVariantGroupDefinition skillVariantGroup,
            int variantGroupIndex)
        {
            if (animationLibrary == null || skillGroup == null || skillVariantGroup == null)
                return false;

            CharacterAnimationGroupDefinition animationGroup = FindOrCreateAnimationGroup(animationLibrary, skillGroup);
            bool changed = EnsureAnimationGroupIdentity(animationGroup, skillGroup);

            CharacterAnimationVariantGroupDefinition animationVariantGroup = FindAnimationVariantGroup(animationGroup, skillVariantGroup);
            if (animationVariantGroup == null)
            {
                animationVariantGroup = new CharacterAnimationVariantGroupDefinition
                {
                    groupName = NormalizeKey(SkillEffectDatabaseSO.GetVariantGroupName(skillVariantGroup)),
                    entries = new List<CharacterAnimationEntry>(),
                };

                animationGroup.skillGroups ??= new List<CharacterAnimationVariantGroupDefinition>();
                if (variantGroupIndex >= 0 && variantGroupIndex <= animationGroup.skillGroups.Count)
                    animationGroup.skillGroups.Insert(variantGroupIndex, animationVariantGroup);
                else
                    animationGroup.skillGroups.Add(animationVariantGroup);
                changed = true;
            }

            string expectedGroupName = NormalizeKey(SkillEffectDatabaseSO.GetVariantGroupName(skillVariantGroup));
            if (!string.Equals(NormalizeKey(animationVariantGroup.groupName), expectedGroupName, StringComparison.Ordinal))
            {
                animationVariantGroup.groupName = expectedGroupName;
                changed = true;
            }

            List<CharacterAnimationEntry> syncedEntries = BuildStructuralAnimationEntries(skillVariantGroup, animationVariantGroup);
            if (!AreAnimationEntriesEquivalent(animationVariantGroup.entries, syncedEntries))
            {
                animationVariantGroup.entries = syncedEntries;
                changed = true;
            }

            return changed;
        }

        private static CharacterAnimationGroupDefinition FindOrCreateAnimationGroup(
            CharacterAnimationLibrarySO animationLibrary,
            SkillGroupDefinition skillGroup)
        {
            animationLibrary.groups ??= new List<CharacterAnimationGroupDefinition>();

            string targetGroupId = NormalizeKey(skillGroup != null ? skillGroup.groupId : string.Empty);
            for (int i = 0; i < animationLibrary.groups.Count; i++)
            {
                CharacterAnimationGroupDefinition group = animationLibrary.groups[i];
                if (group == null)
                    continue;

                if (string.Equals(NormalizeKey(group.groupId), targetGroupId, StringComparison.Ordinal))
                    return group;
            }

            CharacterAnimationGroupDefinition created = new CharacterAnimationGroupDefinition
            {
                groupId = targetGroupId,
                groupName = NormalizeName(skillGroup != null ? skillGroup.groupName : string.Empty, targetGroupId, "敌人"),
                skillGroups = new List<CharacterAnimationVariantGroupDefinition>(),
                entries = new List<CharacterAnimationEntry>(),
            };
            animationLibrary.groups.Add(created);
            return created;
        }

        private static bool EnsureAnimationGroupIdentity(CharacterAnimationGroupDefinition animationGroup, SkillGroupDefinition skillGroup)
        {
            if (animationGroup == null || skillGroup == null)
                return false;

            bool changed = false;
            string expectedGroupId = NormalizeKey(skillGroup.groupId);
            string expectedGroupName = NormalizeName(skillGroup.groupName, skillGroup.groupId, "敌人");

            if (!string.Equals(NormalizeKey(animationGroup.groupId), expectedGroupId, StringComparison.Ordinal))
            {
                animationGroup.groupId = expectedGroupId;
                changed = true;
            }

            if (!string.Equals(NormalizeName(animationGroup.groupName), expectedGroupName, StringComparison.Ordinal))
            {
                animationGroup.groupName = expectedGroupName;
                changed = true;
            }

            animationGroup.skillGroups ??= new List<CharacterAnimationVariantGroupDefinition>();
            animationGroup.entries ??= new List<CharacterAnimationEntry>();
            return changed;
        }

        private static CharacterAnimationVariantGroupDefinition FindAnimationVariantGroup(
            CharacterAnimationGroupDefinition animationGroup,
            SkillEffectVariantGroupDefinition skillVariantGroup)
        {
            if (animationGroup == null || skillVariantGroup == null)
                return null;

            string groupName = NormalizeKey(SkillEffectDatabaseSO.GetVariantGroupName(skillVariantGroup));
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                CharacterAnimationVariantGroupDefinition byGroupName = CharacterAnimationLibrarySO.FindVariantGroup(animationGroup, groupName);
                if (byGroupName != null)
                    return byGroupName;
            }

            SharedSkillDefinition primarySkill = SkillEffectDatabaseSO.GetPrimaryEntry(skillVariantGroup);
            string primarySkillId = NormalizeKey(primarySkill != null ? primarySkill.skillId : string.Empty);
            return string.IsNullOrWhiteSpace(primarySkillId)
                ? null
                : CharacterAnimationLibrarySO.FindVariantGroup(animationGroup, primarySkillId);
        }

        private static List<CharacterAnimationEntry> BuildStructuralAnimationEntries(
            SkillEffectVariantGroupDefinition skillVariantGroup,
            CharacterAnimationVariantGroupDefinition animationVariantGroup)
        {
            List<CharacterAnimationEntry> currentEntries = animationVariantGroup?.entries ?? new List<CharacterAnimationEntry>();
            List<CharacterAnimationEntry> result = new List<CharacterAnimationEntry>();
            HashSet<CharacterAnimationEntry> usedEntries = new HashSet<CharacterAnimationEntry>();

            CharacterAnimationEntry currentPrimary = currentEntries.Count > 0 ? currentEntries[0] : null;
            string primaryAnimationId = ResolveStructuralPrimaryAnimationId(skillVariantGroup, currentPrimary);
            CharacterAnimationEntry primaryEntry = CloneAnimationEntry(currentPrimary);
            primaryEntry.animationId = primaryAnimationId;
            result.Add(primaryEntry);
            if (currentPrimary != null)
                usedEntries.Add(currentPrimary);

            List<SharedSkillDefinition> skillEntries = skillVariantGroup?.entries ?? new List<SharedSkillDefinition>();
            for (int entryIndex = 1; entryIndex < skillEntries.Count; entryIndex++)
            {
                SharedSkillDefinition skill = skillEntries[entryIndex];
                string expectedAnimationId = NormalizeKey(
                    skill != null ? skill.skillId : string.Empty,
                    $"{primaryAnimationId}_Variant{entryIndex}");

                CharacterAnimationEntry existing = FindReusableAnimationEntry(currentEntries, usedEntries, expectedAnimationId, entryIndex);
                CharacterAnimationEntry syncedEntry = existing != null
                    ? CloneAnimationEntry(existing)
                    : CloneAnimationEntry(primaryEntry);
                syncedEntry.animationId = expectedAnimationId;
                result.Add(syncedEntry);
                if (existing != null)
                    usedEntries.Add(existing);
            }

            return result;
        }

        private static string ResolveStructuralPrimaryAnimationId(
            SkillEffectVariantGroupDefinition skillVariantGroup,
            CharacterAnimationEntry currentPrimary)
        {
            string existingAnimationId = NormalizeKey(currentPrimary != null ? currentPrimary.animationId : string.Empty);
            if (!string.IsNullOrWhiteSpace(existingAnimationId))
                return existingAnimationId;

            SharedSkillDefinition primarySkill = SkillEffectDatabaseSO.GetPrimaryEntry(skillVariantGroup);
            string primarySkillId = NormalizeKey(primarySkill != null ? primarySkill.skillId : string.Empty);
            if (!string.IsNullOrWhiteSpace(primarySkillId))
                return primarySkillId;

            return NormalizeKey(SkillEffectDatabaseSO.GetVariantGroupName(skillVariantGroup), "Animation");
        }

        private static CharacterAnimationEntry FindReusableAnimationEntry(
            List<CharacterAnimationEntry> currentEntries,
            HashSet<CharacterAnimationEntry> usedEntries,
            string expectedAnimationId,
            int expectedIndex)
        {
            if (currentEntries != null)
            {
                for (int i = 0; i < currentEntries.Count; i++)
                {
                    CharacterAnimationEntry candidate = currentEntries[i];
                    if (candidate == null || usedEntries.Contains(candidate))
                        continue;

                    if (string.Equals(NormalizeKey(candidate.animationId), expectedAnimationId, StringComparison.Ordinal))
                        return candidate;
                }

                if (expectedIndex >= 0 && expectedIndex < currentEntries.Count)
                {
                    CharacterAnimationEntry indexedCandidate = currentEntries[expectedIndex];
                    if (indexedCandidate != null && !usedEntries.Contains(indexedCandidate))
                        return indexedCandidate;
                }
            }

            return null;
        }

        private static CharacterAnimationEntry CloneAnimationEntry(CharacterAnimationEntry template)
        {
            return new CharacterAnimationEntry
            {
                animationId = template != null ? template.animationId : string.Empty,
                clip = template != null ? template.clip : null,
            };
        }

        private static bool AreAnimationEntriesEquivalent(
            List<CharacterAnimationEntry> currentEntries,
            List<CharacterAnimationEntry> newEntries)
        {
            int currentCount = currentEntries != null ? currentEntries.Count : 0;
            int newCount = newEntries != null ? newEntries.Count : 0;
            if (currentCount != newCount)
                return false;

            for (int i = 0; i < newCount; i++)
            {
                if (!AreEntriesEquivalent(currentEntries[i], newEntries[i]))
                    return false;
            }

            return true;
        }

        private static string ExtractTrailingSkillSlotToken(string value)
        {
            string normalizedValue = NormalizeKey(value);
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return string.Empty;

            int markerIndex = normalizedValue.LastIndexOf("Skill", StringComparison.Ordinal);
            if (markerIndex < 0)
                return string.Empty;

            string suffix = normalizedValue.Substring(markerIndex + "Skill".Length);
            return int.TryParse(suffix, out _) ? normalizedValue.Substring(markerIndex) : string.Empty;
        }

        private static string GetPrimaryVariantId(string value)
        {
            string normalizedValue = NormalizeKey(value);
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return string.Empty;

            int variantIndex = normalizedValue.IndexOf(VariantSuffix, StringComparison.Ordinal);
            return variantIndex > 0 ? normalizedValue.Substring(0, variantIndex) : normalizedValue;
        }

        private static string RemoveUnderscores(string value)
        {
            string normalizedValue = NormalizeKey(value);
            return string.IsNullOrWhiteSpace(normalizedValue)
                ? string.Empty
                : normalizedValue.Replace("_", string.Empty);
        }

        private static void AddCandidate(List<string> candidates, string value)
        {
            string normalizedValue = NormalizeKey(value);
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i], normalizedValue, StringComparison.Ordinal))
                    return;
            }

            candidates.Add(normalizedValue);
        }

        private static string NormalizeKey(string value, string fallback = "")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string NormalizeName(string value, string fallbackValue = "", string defaultValue = "")
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
            if (!string.IsNullOrWhiteSpace(fallbackValue))
                return fallbackValue.Trim();
            return defaultValue;
        }
    }
}
#endif
