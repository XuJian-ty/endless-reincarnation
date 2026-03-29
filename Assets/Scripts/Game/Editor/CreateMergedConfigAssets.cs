using UnityEditor;
using UnityEngine;
using Game.Data;
using Game.Presentation;
using System.Collections.Generic;
using System.IO;

namespace Game.Editor
{
    /// <summary>
    /// Creates config assets under Resources/配置 for one-click setup.
    /// Existing config assets are treated as the default source of truth; code defaults are only used when assets are missing.
    /// Menu: 游戏/一键创建全部配置（需求书默认数据）
    /// </summary>
    public static class CreateMergedConfigAssets
    {
        private const string ResourcesConfigDir = "Assets/Resources/配置";

        private static void EnsureConfigFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (AssetDatabase.IsValidFolder(ResourcesConfigDir))
                return;

            string parentDir = Path.GetDirectoryName(ResourcesConfigDir)?.Replace("\\", "/");
            string folderName = Path.GetFileName(ResourcesConfigDir);
            if (string.IsNullOrEmpty(parentDir) || string.IsNullOrEmpty(folderName))
            {
                Debug.LogError($"[配置] 无法解析配置目录路径：{ResourcesConfigDir}");
                return;
            }

            if (!AssetDatabase.IsValidFolder(parentDir))
            {
                Debug.LogError($"[配置] 配置目录父路径不存在：{parentDir}");
                return;
            }

            AssetDatabase.CreateFolder(parentDir, folderName);
        }

        private static T CreateOrUpdateAsset<T>(T source, string assetPath) where T : ScriptableObject
        {
            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            source.name = objectName;

            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, assetPath);
                return source;
            }

            UnityEngine.Object.DestroyImmediate(source);
            return existing;
        }

        private static WeaponDatabaseSO CreateOrMergeWeaponDatabaseAsset(WeaponDatabaseSO source, string assetPath)
        {
            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            source.name = objectName;

            var existing = AssetDatabase.LoadAssetAtPath<WeaponDatabaseSO>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, assetPath);
                return source;
            }
            UnityEngine.Object.DestroyImmediate(source);
            return existing;
        }

        private static ItemDisplayDatabaseSO CreateOrMergeItemDisplayDatabaseAsset(ItemDisplayDatabaseSO source, string assetPath)
        {
            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            source.name = objectName;

            var existing = AssetDatabase.LoadAssetAtPath<ItemDisplayDatabaseSO>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, assetPath);
                return source;
            }
            UnityEngine.Object.DestroyImmediate(source);
            return existing;
        }

        private static SkillEffectDatabaseSO CreateOrMergeSkillEffectDatabaseAsset(SkillEffectDatabaseSO source, string assetPath)
        {
            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            source.name = objectName;

            var existing = AssetDatabase.LoadAssetAtPath<SkillEffectDatabaseSO>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, assetPath);
                return source;
            }
            UnityEngine.Object.DestroyImmediate(source);
            return existing;
        }

        private static CharacterAnimationLibrarySO CreateOrMergeCharacterAnimationLibraryAsset(CharacterAnimationLibrarySO source, string assetPath)
        {
            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            source.name = objectName;

            var existing = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, assetPath);
                return source;
            }
            UnityEngine.Object.DestroyImmediate(source);
            return existing;
        }

        private static void NormalizeConfigAssetNames()
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Resources/配置" });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath) as ScriptableObject;
                if (asset == null)
                    continue;

                string expectedName = Path.GetFileNameWithoutExtension(assetPath);
                if (asset.name == expectedName)
                    continue;

                asset.name = expectedName;
                EditorUtility.SetDirty(asset);
            }
        }

        private static EnemyStatsEntry BuildEnemyStats(
            string enemyId,
            string displayName,
            EnemyType type,
            float baseHp,
            float baseAttack,
            float baseDefense,
            float baseMoveSpeed,
            int goldMin,
            int goldMax,
            int baseExp)
        {
            return new EnemyStatsEntry
            {
                enemyId = enemyId,
                displayName = displayName,
                type = type,
                baseHp = baseHp,
                baseAttack = baseAttack,
                baseDefense = baseDefense,
                baseMoveSpeed = baseMoveSpeed,
                goldMin = goldMin,
                goldMax = goldMax,
                baseExp = baseExp,
            };
        }

        private static EnemyStatsGroupDefinition BuildEnemyStatsGroup(EnemyStatsEntry entry)
        {
            return new EnemyStatsGroupDefinition
            {
                groupId = entry.enemyId,
                groupName = entry.displayName,
                entries = new List<EnemyStatsEntry> { entry },
            };
        }

        private static EnemySkillSlotBinding BuildSkillSlot(
            string enemyId,
            int slotIndex,
            string displayName,
            float castRange = 3f,
            float cooldown = 1f,
            float castDuration = 0.6f,
            string animationTrigger = "",
            EnemySkillPhaseAvailability phaseAvailability = EnemySkillPhaseAvailability.Always,
            float postCastIdleDuration = 0f,
            bool enterIdleAfterCast = false,
            bool rotateToTargetOnCast = true,
            float minCastRange = 0f,
            float idealCastRange = 0f,
            EnemySkillRole skillRole = EnemySkillRole.Flexible,
            float riskWeight = 0.35f,
            float punishWeight = 0.5f,
            float repeatPenalty = 0.2f,
            bool canUseUnderThreat = false)
        {
            return new EnemySkillSlotBinding
            {
                slotIndex            = slotIndex,
                skillId              = $"{enemyId}Skill{slotIndex}",
                displayName          = displayName,
                castRange            = castRange,
                minCastRange         = minCastRange,
                idealCastRange       = idealCastRange > 0.01f ? idealCastRange : castRange,
                skillRole            = skillRole,
                riskWeight           = riskWeight,
                punishWeight         = punishWeight,
                repeatPenalty        = repeatPenalty,
                canUseUnderThreat    = canUseUnderThreat,
                cooldown             = cooldown,
                castDuration         = castDuration,
                animationTrigger     = string.IsNullOrWhiteSpace(animationTrigger) ? $"Skill{slotIndex}" : animationTrigger,
                phaseAvailability    = phaseAvailability,
                postCastIdleDuration = postCastIdleDuration,
                enterIdleAfterCast   = enterIdleAfterCast,
                rotateToTargetOnCast = rotateToTargetOnCast,
            };
        }

        private static EnemyArchetypeSO BuildEnemyArchetype(
            string enemyId,
            string displayName,
            EnemyType enemyType,
            float sectorAngle,
            float sectorRange,
            float chaseBreakDistance,
            float patrolRadius,
            params EnemySkillSlotBinding[] skillSlots)
        {
            var archetype = ScriptableObject.CreateInstance<EnemyArchetypeSO>();
            archetype.enemyId = enemyId;
            archetype.displayName = displayName;
            archetype.enemyType = enemyType;
            archetype.sectorAngle = sectorAngle;
            archetype.sectorRange = sectorRange;
            archetype.chaseBreakDistance = chaseBreakDistance;
            archetype.patrolRadius = patrolRadius;
            archetype.skillSlots = new List<EnemySkillSlotBinding>(skillSlots);
            ApplyTacticalDefaults(archetype, enemyType, skillSlots);
            return archetype;
        }

        private static void ApplyTacticalDefaults(
            EnemyArchetypeSO archetype,
            EnemyType enemyType,
            EnemySkillSlotBinding[] skillSlots)
        {
            float minRange = float.MaxValue;
            float maxRange = 0f;

            if (skillSlots != null)
            {
                for (int i = 0; i < skillSlots.Length; i++)
                {
                    var slot = skillSlots[i];
                    if (slot == null)
                        continue;

                    float castRange = ResolveSkillSlotRange(slot);
                    minRange = Mathf.Min(minRange, castRange);
                    maxRange = Mathf.Max(maxRange, castRange);
                }
            }

            if (minRange == float.MaxValue)
            {
                minRange = 2.5f;
                maxRange = 6f;
            }

            bool isBossOrRanged = enemyType == EnemyType.Boss || enemyType == EnemyType.RangedMinion;

            archetype.chaseInnerDistance = Mathf.Max(1.5f, Mathf.Min(maxRange, minRange + (isBossOrRanged ? 2.5f : 1.2f)));
            archetype.chaseOuterDistance = Mathf.Max(archetype.chaseInnerDistance + 2f, maxRange + (isBossOrRanged ? 3.5f : 2f));
            archetype.preferredSafetyDistanceOffset = isBossOrRanged ? 0.9f : 0.25f;
            archetype.combatDistanceTolerance = isBossOrRanged ? 1.1f : 0.75f;
            archetype.castRangeTolerance = 0.25f;
            archetype.retreatStepDistance = isBossOrRanged ? 4.2f : 2.6f;
            archetype.approachLeadTime = isBossOrRanged ? 0.25f : 0.15f;
            archetype.losProbeInterval = 0.1f;
            archetype.pathProbeInterval = 0.3f;
            archetype.perceptionEyeHeight = 1.2f;
            archetype.obstacleMask = Physics.DefaultRaycastLayers;
            archetype.reactionMinSeconds = isBossOrRanged ? 0.07f : 0.09f;
            archetype.reactionMaxSeconds = isBossOrRanged ? 0.16f : 0.2f;
            archetype.decisionCommitSeconds = isBossOrRanged ? 0.22f : 0.26f;
            archetype.searchMemoryDuration = isBossOrRanged ? 1.9f : 1.4f;
            archetype.aggression = isBossOrRanged ? 0.62f : 0.55f;
            archetype.caution = isBossOrRanged ? 0.45f : 0.38f;
            archetype.dodgeBias = isBossOrRanged ? 0.58f : 0.42f;
            archetype.punishBias = isBossOrRanged ? 0.62f : 0.5f;
            archetype.strafeBias = isBossOrRanged ? 0.52f : 0.38f;
            archetype.maxPressureAllies = enemyType == EnemyType.MeleeMinion ? 1 : enemyType == EnemyType.Boss ? 3 : 2;
            archetype.poiseMax = enemyType switch
            {
                EnemyType.MeleeMinion => 10f,
                EnemyType.RangedMinion => 8f,
                EnemyType.Elite => 18f,
                EnemyType.Guardian => 26f,
                EnemyType.Boss => 34f,
                _ => 12f,
            };
            archetype.poiseRecoveryPerSecond = enemyType switch
            {
                EnemyType.MeleeMinion => 7f,
                EnemyType.RangedMinion => 6f,
                EnemyType.Elite => 9f,
                EnemyType.Guardian => 11f,
                EnemyType.Boss => 13f,
                _ => 8f,
            };
            archetype.poiseBreakStunDuration = enemyType switch
            {
                EnemyType.MeleeMinion => 0.28f,
                EnemyType.RangedMinion => 0.25f,
                EnemyType.Elite => 0.34f,
                EnemyType.Guardian => 0.4f,
                EnemyType.Boss => 0.45f,
                _ => 0.3f,
            };
            archetype.allowLightHitFlinch = enemyType == EnemyType.MeleeMinion || enemyType == EnemyType.RangedMinion;
            archetype.superArmorWhileCasting = enemyType == EnemyType.Guardian || enemyType == EnemyType.Boss;
        }

        private static float ResolveSkillSlotRange(EnemySkillSlotBinding slot)
        {
            if (slot == null)
                return 3f;

            return slot.castRange > 0.01f ? slot.castRange : 3f;
        }

        private static void NormalizeSkillSlotOverrides(List<EnemyArchetypeSO> archetypes)
        {
            // 新系统中槽位绑定字段为直接值（无覆盖语义），此方法保留签名以免调用处报错。
        }

        private static void CreateEnemyStatsDatabase()
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<EnemyStatsDatabaseSO>();
            db.groups = new List<EnemyStatsGroupDefinition>
            {
                BuildEnemyStatsGroup(BuildEnemyStats("melee_minion", "近战小怪", EnemyType.MeleeMinion, 60f, 10f, 5f, 3.8f, 10, 10, 10)),
                BuildEnemyStatsGroup(BuildEnemyStats("ranged_minion", "远程小怪", EnemyType.RangedMinion, 45f, 12f, 3f, 3.6f, 10, 10, 10)),
                BuildEnemyStatsGroup(BuildEnemyStats("elite_1", "精英1", EnemyType.Elite, 160f, 18f, 8f, 4f, 30, 30, 30)),
                BuildEnemyStatsGroup(BuildEnemyStats("elite_2", "精英2", EnemyType.Elite, 180f, 16f, 10f, 3.9f, 30, 30, 30)),
                BuildEnemyStatsGroup(BuildEnemyStats("guardian_1", "守卫者1", EnemyType.Guardian, 260f, 22f, 10f, 4.2f, 0, 0, 0)),
                BuildEnemyStatsGroup(BuildEnemyStats("guardian_2", "守卫者2", EnemyType.Guardian, 300f, 20f, 12f, 3.9f, 0, 0, 0)),
                BuildEnemyStatsGroup(BuildEnemyStats("boss_1", "Boss1", EnemyType.Boss, 750f, 28f, 12f, 4.5f, 0, 0, 0)),
                BuildEnemyStatsGroup(BuildEnemyStats("boss_2", "Boss2", EnemyType.Boss, 820f, 30f, 13f, 4.4f, 0, 0, 0)),
                BuildEnemyStatsGroup(BuildEnemyStats("boss_3", "Boss3", EnemyType.Boss, 900f, 32f, 14f, 4.3f, 0, 0, 0)),
                BuildEnemyStatsGroup(BuildEnemyStats("boss_4", "Boss4", EnemyType.Boss, 980f, 34f, 15f, 4.2f, 0, 0, 0)),
                BuildEnemyStatsGroup(BuildEnemyStats("boss_5", "Boss5", EnemyType.Boss, 1100f, 36f, 16f, 4.1f, 0, 0, 0)),
            };
            db.SyncFlatEntries();
            CreateOrUpdateAsset(db, $"{ResourcesConfigDir}/敌人属性库.asset");
            AssetDatabase.SaveAssets();
        }

        private static EnemySpawnTaskDatabaseSO CreateEnemySpawnTaskDatabase()
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<EnemySpawnTaskDatabaseSO>();
            db.tasks = new List<EnemySpawnTaskDefinition>
            {
                new EnemySpawnTaskDefinition
                {
                    spawnType = EnemySpawnCategory.Minion,
                    specificSpawnId = "melee_minion",
                    spawnRadius = 12f,
                    countMode = EnemySpawnCountMode.RandomRange,
                    randomMinCount = 2,
                    randomMaxCount = 4,
                    spawnMinSpacing = 3f,
                },
                new EnemySpawnTaskDefinition
                {
                    spawnType = EnemySpawnCategory.Minion,
                    specificSpawnId = "ranged_minion",
                    spawnRadius = 14f,
                    countMode = EnemySpawnCountMode.RandomRange,
                    randomMinCount = 1,
                    randomMaxCount = 3,
                    spawnMinSpacing = 3.5f,
                },
                new EnemySpawnTaskDefinition
                {
                    spawnType = EnemySpawnCategory.Elite,
                    spawnRadius = 10f,
                    countMode = EnemySpawnCountMode.Fixed,
                    fixedCount = 1,
                    spawnMinSpacing = 4f,
                },
                new EnemySpawnTaskDefinition
                {
                    spawnType = EnemySpawnCategory.Guardian,
                    spawnRadius = 8f,
                    countMode = EnemySpawnCountMode.Fixed,
                    fixedCount = 1,
                    spawnMinSpacing = 5f,
                },
            };

            var asset = CreateOrUpdateAsset(db, $"{ResourcesConfigDir}/生成任务库.asset");
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static LevelEnemySpawnPlanSO CreateEnemySpawnPlanLibrary(EnemySpawnTaskDatabaseSO taskDatabase)
        {
            EnsureConfigFolder();
            var so = ScriptableObject.CreateInstance<LevelEnemySpawnPlanSO>();
            so.taskDatabase = taskDatabase;
            so.plans = new List<LevelEnemyGlobalSpawnPlanDefinition>
            {
                new LevelEnemyGlobalSpawnPlanDefinition
                {
                    taskAssignments = new List<GlobalEnemySpawnTaskAssignment>
                    {
                        new GlobalEnemySpawnTaskAssignment { taskIndex = 0, taskCount = 4 },
                        new GlobalEnemySpawnTaskAssignment { taskIndex = 1, taskCount = 3 },
                        new GlobalEnemySpawnTaskAssignment { taskIndex = 2, taskCount = 2 },
                        new GlobalEnemySpawnTaskAssignment { taskIndex = 3, taskCount = 2 },
                    },
                    spawnMode = GlobalEnemySpawnMode.SpawnAllAtOnce,
                    taskCenterMinSpacing = 18f,
                    initialTaskCount = 3,
                    taskInterval = 8f,
                }
            };

            var asset = CreateOrUpdateAsset(so, $"{ResourcesConfigDir}/关卡全局生成方案库.asset");
            if (asset != null)
            {
                bool changed = false;
                if (asset.taskDatabase == null && taskDatabase != null)
                {
                    asset.taskDatabase = taskDatabase;
                    changed = true;
                }

                if (asset.plans == null || asset.plans.Count == 0)
                {
                    asset.plans = new List<LevelEnemyGlobalSpawnPlanDefinition>
                    {
                        new LevelEnemyGlobalSpawnPlanDefinition
                        {
                            taskAssignments = new List<GlobalEnemySpawnTaskAssignment>
                            {
                                new GlobalEnemySpawnTaskAssignment { taskIndex = 0, taskCount = 4 },
                                new GlobalEnemySpawnTaskAssignment { taskIndex = 1, taskCount = 3 },
                                new GlobalEnemySpawnTaskAssignment { taskIndex = 2, taskCount = 2 },
                                new GlobalEnemySpawnTaskAssignment { taskIndex = 3, taskCount = 2 },
                            },
                            spawnMode = GlobalEnemySpawnMode.SpawnAllAtOnce,
                            taskCenterMinSpacing = 18f,
                            initialTaskCount = 3,
                            taskInterval = 8f,
                        }
                    };
                    changed = true;
                }

                if (changed)
                    EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            return asset;
        }

        private static LevelLocalEnemySpawnPlanSO CreateLocalEnemySpawnPlanLibrary(EnemySpawnTaskDatabaseSO taskDatabase)
        {
            EnsureConfigFolder();
            var so = ScriptableObject.CreateInstance<LevelLocalEnemySpawnPlanSO>();
            so.taskDatabase = taskDatabase;
            so.plans = new List<LevelLocalEnemySpawnPlanDefinition>
            {
                new LevelLocalEnemySpawnPlanDefinition
                {
                    taskIndex = 0,
                    spawnMode = LocalEnemySpawnMode.SpawnOnce,
                    taskTotalMode = LocalEnemySpawnTaskTotalMode.FixedCount,
                    totalTaskCount = 1,
                    taskInterval = 8f,
                },
                new LevelLocalEnemySpawnPlanDefinition
                {
                    taskIndex = 3,
                    spawnMode = LocalEnemySpawnMode.SpawnRepeatedly,
                    taskTotalMode = LocalEnemySpawnTaskTotalMode.FixedCount,
                    totalTaskCount = 3,
                    taskInterval = 10f,
                },
            };

            var asset = CreateOrUpdateAsset(so, $"{ResourcesConfigDir}/关卡局部生成方案库.asset");
            if (asset != null)
            {
                bool changed = false;
                if (asset.taskDatabase == null && taskDatabase != null)
                {
                    asset.taskDatabase = taskDatabase;
                    changed = true;
                }

                if (asset.plans == null || asset.plans.Count == 0)
                {
                    asset.plans = new List<LevelLocalEnemySpawnPlanDefinition>
                    {
                        new LevelLocalEnemySpawnPlanDefinition
                        {
                            taskIndex = 0,
                            spawnMode = LocalEnemySpawnMode.SpawnOnce,
                            taskTotalMode = LocalEnemySpawnTaskTotalMode.FixedCount,
                            totalTaskCount = 1,
                            taskInterval = 8f,
                        },
                        new LevelLocalEnemySpawnPlanDefinition
                        {
                            taskIndex = 3,
                            spawnMode = LocalEnemySpawnMode.SpawnRepeatedly,
                            taskTotalMode = LocalEnemySpawnTaskTotalMode.FixedCount,
                            totalTaskCount = 3,
                            taskInterval = 10f,
                        },
                    };
                    changed = true;
                }

                if (changed)
                    EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void CreateLevelConfigDatabase(LevelEnemySpawnPlanSO defaultSpawnPlanLibrary)
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<LevelConfigDatabaseSO>();
            db.levels = new List<LevelConfigData>
            {
                new LevelConfigData { levelIndex = 1, sceneName = "Level_1", globalSpawnPlanLibrary = defaultSpawnPlanLibrary, globalSpawnPlanIndex = 0, minionGoldReward = 10, minionExpReward = 10, chestDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.45f }, new DropEntry { itemType = "weapon_rare", weight = 0.2f }, new DropEntry { itemType = "potion_hp", weight = 0.15f }, new DropEntry { itemType = "potion_mp", weight = 0.15f }, new DropEntry { itemType = "nectar", weight = 0.05f } }, eliteGoldReward = 30, eliteExpReward = 30, eliteDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.3f }, new DropEntry { itemType = "weapon_rare", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, guardianGoldReward = 0, guardianExpReward = 0, guardianTalentReward = 1, guardianDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.3f }, new DropEntry { itemType = "weapon_rare", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, bossGoldReward = 0, bossExpReward = 0, bossDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.3f }, new DropEntry { itemType = "weapon_rare", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
                new LevelConfigData { levelIndex = 2, sceneName = "Level_2", globalSpawnPlanLibrary = defaultSpawnPlanLibrary, globalSpawnPlanIndex = 0, minionGoldReward = 10, minionExpReward = 10, chestDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.25f }, new DropEntry { itemType = "weapon_rare", weight = 0.3f }, new DropEntry { itemType = "weapon_epic", weight = 0.15f }, new DropEntry { itemType = "potion_hp", weight = 0.1f }, new DropEntry { itemType = "potion_mp", weight = 0.1f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, eliteGoldReward = 30, eliteExpReward = 30, eliteDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.15f }, new DropEntry { itemType = "weapon_rare", weight = 0.15f }, new DropEntry { itemType = "weapon_epic", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, guardianGoldReward = 0, guardianExpReward = 0, guardianTalentReward = 1, guardianDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.15f }, new DropEntry { itemType = "weapon_rare", weight = 0.15f }, new DropEntry { itemType = "weapon_epic", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, bossGoldReward = 0, bossExpReward = 0, bossDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.15f }, new DropEntry { itemType = "weapon_rare", weight = 0.15f }, new DropEntry { itemType = "weapon_epic", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
                new LevelConfigData { levelIndex = 3, sceneName = "Level_3", globalSpawnPlanLibrary = defaultSpawnPlanLibrary, globalSpawnPlanIndex = 0, minionGoldReward = 10, minionExpReward = 10, chestDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_rare", weight = 0.35f }, new DropEntry { itemType = "weapon_epic", weight = 0.25f }, new DropEntry { itemType = "potion_hp", weight = 0.15f }, new DropEntry { itemType = "potion_mp", weight = 0.15f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, eliteGoldReward = 30, eliteExpReward = 30, eliteDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_rare", weight = 0.2f }, new DropEntry { itemType = "weapon_epic", weight = 0.2f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, guardianGoldReward = 0, guardianExpReward = 0, guardianTalentReward = 1, guardianDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_rare", weight = 0.2f }, new DropEntry { itemType = "weapon_epic", weight = 0.2f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, bossGoldReward = 0, bossExpReward = 0, bossDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_rare", weight = 0.2f }, new DropEntry { itemType = "weapon_epic", weight = 0.2f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
                new LevelConfigData { levelIndex = 4, sceneName = "Level_4", globalSpawnPlanLibrary = defaultSpawnPlanLibrary, globalSpawnPlanIndex = 0, minionGoldReward = 10, minionExpReward = 10, chestDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_epic", weight = 0.5f }, new DropEntry { itemType = "weapon_legendary", weight = 0.2f }, new DropEntry { itemType = "potion_hp", weight = 0.1f }, new DropEntry { itemType = "potion_mp", weight = 0.1f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, eliteGoldReward = 30, eliteExpReward = 30, eliteDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_epic", weight = 0.3f }, new DropEntry { itemType = "weapon_legendary", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, guardianGoldReward = 0, guardianExpReward = 0, guardianTalentReward = 1, guardianDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_epic", weight = 0.3f }, new DropEntry { itemType = "weapon_legendary", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, bossGoldReward = 0, bossExpReward = 0, bossDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_epic", weight = 0.3f }, new DropEntry { itemType = "weapon_legendary", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
                new LevelConfigData { levelIndex = 5, sceneName = "Level_5", globalSpawnPlanLibrary = defaultSpawnPlanLibrary, globalSpawnPlanIndex = 0, minionGoldReward = 10, minionExpReward = 10, chestDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_legendary", weight = 0.6f }, new DropEntry { itemType = "potion_hp", weight = 0.15f }, new DropEntry { itemType = "potion_mp", weight = 0.15f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, eliteGoldReward = 30, eliteExpReward = 30, eliteDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_legendary", weight = 0.4f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, guardianGoldReward = 0, guardianExpReward = 0, guardianTalentReward = 1, guardianDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_legendary", weight = 0.4f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } }, bossGoldReward = 0, bossExpReward = 0, bossDropEntries = new List<DropEntry> { new DropEntry { itemType = "weapon_legendary", weight = 0.4f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
            };
            var asset = CreateOrUpdateAsset(db, $"{ResourcesConfigDir}/关卡配置库.asset");
            if (asset != null && asset.levels != null && defaultSpawnPlanLibrary != null)
            {
                bool changed = false;
                for (int i = 0; i < asset.levels.Count; i++)
                {
                    LevelConfigData level = asset.levels[i];
                    if (level == null)
                        continue;

                    if (level.globalSpawnPlanLibrary == null)
                    {
                        level.globalSpawnPlanLibrary = defaultSpawnPlanLibrary;
                        level.globalSpawnPlanIndex = 0;
                        changed = true;
                    }

                    if (level.guardianDropEntries == null || level.guardianDropEntries.Count == 0)
                    {
                        level.guardianDropEntries = CloneDropEntries(level.eliteDropEntries);
                        changed = true;
                    }

                    if (level.bossDropEntries == null || level.bossDropEntries.Count == 0)
                    {
                        level.bossDropEntries = CloneDropEntries(level.eliteDropEntries);
                        changed = true;
                    }

                    if (level.shopGoodsEntries == null || level.shopGoodsEntries.Count == 0)
                    {
                        level.shopGoodsEntries = CreateDefaultShopGoodsEntries(level.levelIndex);
                        changed = true;
                    }
                }

                if (changed)
                    EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }

        private static List<ShopGoodsEntry> CreateDefaultShopGoodsEntries(int levelIndex)
        {
            return levelIndex switch
            {
                <= 1 => new List<ShopGoodsEntry>
                {
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "sword", weaponRarity = WeaponRarity.Common, spawnChance = 0.65f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "gun", weaponRarity = WeaponRarity.Common, spawnChance = 0.45f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_hp", spawnChance = 0.8f, minCount = 1, maxCount = 3 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_mp", spawnChance = 0.8f, minCount = 1, maxCount = 3 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "nectar", spawnChance = 0.25f, minCount = 1, maxCount = 1 },
                },
                2 => new List<ShopGoodsEntry>
                {
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "sword", weaponRarity = WeaponRarity.Rare, spawnChance = 0.55f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "gun", weaponRarity = WeaponRarity.Rare, spawnChance = 0.55f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_hp", spawnChance = 0.85f, minCount = 2, maxCount = 4 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_mp", spawnChance = 0.85f, minCount = 2, maxCount = 4 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "nectar", spawnChance = 0.3f, minCount = 1, maxCount = 1 },
                },
                3 => new List<ShopGoodsEntry>
                {
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "sword", weaponRarity = WeaponRarity.Rare, spawnChance = 0.45f, minCount = 1, maxCount = 2 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "gun", weaponRarity = WeaponRarity.Epic, spawnChance = 0.45f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_hp", spawnChance = 0.9f, minCount = 2, maxCount = 5 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_mp", spawnChance = 0.9f, minCount = 2, maxCount = 5 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "nectar", spawnChance = 0.35f, minCount = 1, maxCount = 2 },
                },
                4 => new List<ShopGoodsEntry>
                {
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "sword", weaponRarity = WeaponRarity.Epic, spawnChance = 0.55f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "gun", weaponRarity = WeaponRarity.Epic, spawnChance = 0.55f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_hp", spawnChance = 0.9f, minCount = 3, maxCount = 6 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_mp", spawnChance = 0.9f, minCount = 3, maxCount = 6 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "nectar", spawnChance = 0.45f, minCount = 1, maxCount = 2 },
                },
                _ => new List<ShopGoodsEntry>
                {
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "sword", weaponRarity = WeaponRarity.Legendary, spawnChance = 0.6f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.Weapon, weaponId = "gun", weaponRarity = WeaponRarity.Legendary, spawnChance = 0.6f, minCount = 1, maxCount = 1 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_hp", spawnChance = 0.95f, minCount = 4, maxCount = 7 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "potion_mp", spawnChance = 0.95f, minCount = 4, maxCount = 7 },
                    new ShopGoodsEntry { goodsType = ShopGoodsType.StackableItem, itemId = "nectar", spawnChance = 0.55f, minCount = 1, maxCount = 3 },
                },
            };
        }

        private static List<DropEntry> CloneDropEntries(List<DropEntry> source)
        {
            var clonedEntries = new List<DropEntry>();
            if (source == null)
                return clonedEntries;

            for (int i = 0; i < source.Count; i++)
            {
                DropEntry entry = source[i];
                if (entry == null)
                    continue;

                clonedEntries.Add(new DropEntry
                {
                    itemType = entry.itemType,
                    weight = entry.weight
                });
            }

            return clonedEntries;
        }

        private static void CreateWeaponDatabase()
        {
            EnsureConfigFolder();
            var def = new WeaponStatRanges
            {
                hp = new FloatRange(10f, 30f), mp = new FloatRange(10f, 30f), attack = new FloatRange(5f, 15f), defense = new FloatRange(2f, 8f),
                hpRegen = new FloatRange(0.5f, 1.5f), mpRegen = new FloatRange(0.5f, 1.5f),
                critRate = new FloatRange(0.4f, 0.6f), critDmg = new FloatRange(0.8f, 1.2f),
                attackSpeed = new FloatRange(0.1f, 0.3f), moveSpeed = new FloatRange(0.1f, 0.3f),
            };
            var db = ScriptableObject.CreateInstance<WeaponDatabaseSO>();
            db.entries = new List<WeaponEntryData>
            {
                new WeaponEntryData { weaponId = "sword", displayName = "剑", type = WeaponType.MeleeSword, common = def, rare = def, epic = def, legendary = def },
                new WeaponEntryData { weaponId = "gun", displayName = "枪", type = WeaponType.RangedGun, common = def, rare = def, epic = def, legendary = def },
            };
            CreateOrMergeWeaponDatabaseAsset(db, $"{ResourcesConfigDir}/武器库.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreatePlayerGrowthDatabase()
        {
            EnsureConfigFolder();
            var so = ScriptableObject.CreateInstance<LevelGrowthSO>();
            so.levels = new List<LevelGrowthEntry>();
            float bHp = 100f, bMp = 100f, bAtk = 20f, bDef = 100f, bHpR = 1f, bMpR = 1f;
            for (int level = 1; level <= 10; level++)
            {
                int steps = level - 1;
                float mul = Mathf.Pow(1.1f, steps);
                so.levels.Add(new LevelGrowthEntry
                {
                    level = level,
                    baseHp = bHp * mul,
                    baseMp = bMp * mul,
                    baseAttack = bAtk * mul,
                    baseDefense = bDef * mul,
                    baseHpRegen = bHpR * mul,
                    baseMpRegen = bMpR * mul,
                });
            }
            CreateOrUpdateAsset(so, $"{ResourcesConfigDir}/玩家成长属性库.asset");
            AssetDatabase.SaveAssets();
        }
        [MenuItem("游戏/一键创建全部配置（需求书默认数据）", false, 0)]
        public static void CreateAll()
        {
            EnsureConfigFolder();
            CreateEnemyStatsDatabase();
            CreateSkillEffectDatabase();
            CreatePassiveSkillEffectDatabase();
            CreateEnemyArchetypeDatabase();
            var enemySpawnTaskDatabase = CreateEnemySpawnTaskDatabase();
            var enemySpawnPlanLibrary = CreateEnemySpawnPlanLibrary(enemySpawnTaskDatabase);
            CreateLocalEnemySpawnPlanLibrary(enemySpawnTaskDatabase);
            CreateLevelConfigDatabase(enemySpawnPlanLibrary);
            CreateWeaponDatabase();
            CreatePlayerGrowthDatabase();
            CreateOrUpdateAsset(
                ScriptableObject.CreateInstance<DifficultyScalingSO>(),
                $"{ResourcesConfigDir}/难度系数.asset");
            CreateOrUpdateAsset(
                ScriptableObject.CreateInstance<PlayerCloneAndLevelBossVisualConfigSO>(),
                $"{ResourcesConfigDir}/玩家分身和最终Boss视觉配置.asset");
            CreateOrUpdateAsset(
                ScriptableObject.CreateInstance<PlayerCloneAIConfigSO>(),
                $"{ResourcesConfigDir}/分身AI配置.asset");
            AssetDatabase.SaveAssets();
            CreateShopPriceConfig();
            CreatePotionConfig();
            CreateSkillConfigDatabase();
            CreateCharacterAnimationLibrary();
            CreateItemDisplayConfig();
            CreateKeyRebindConfig();
            CreateBackpackUIConfig();
            CreateBuffConfig();
            CreateBgmTrackListAssets.Create();
            NormalizeConfigAssetNames();
            AssetDatabase.SaveAssets();
            Debug.Log("[配置] 已完成一键创建。已有配置文件优先保留；仅在缺失时回退到代码默认值。");
        }

        private static void CreateKeyRebindConfig()
        {
            EnsureConfigFolder();
            var so = ScriptableObject.CreateInstance<KeyRebindConfigSO>();
            so.entries = new List<KeyRebindEntry>
            {
                new KeyRebindEntry { actionName = "Skill0", displayName = "技能1" },
                new KeyRebindEntry { actionName = "Skill1", displayName = "技能2" },
                new KeyRebindEntry { actionName = "Skill2", displayName = "技能3" },
                new KeyRebindEntry { actionName = "Skill3", displayName = "技能4" },
                new KeyRebindEntry { actionName = "UseHealthPotion", displayName = "回血药剂" },
                new KeyRebindEntry { actionName = "UseManaPotion", displayName = "回蓝药剂" },
                new KeyRebindEntry { actionName = "ToggleKeyConfig", displayName = "按键配置" },
                new KeyRebindEntry { actionName = "ToggleBackpack", displayName = "背包" },
                new KeyRebindEntry { actionName = "ToggleSkillTree", displayName = "技能树" },
                new KeyRebindEntry { actionName = "ToggleMenu", displayName = "菜单" },
                new KeyRebindEntry { actionName = "Dodge", displayName = "闪避" },
                new KeyRebindEntry { actionName = "Jump", displayName = "跳跃" },
                new KeyRebindEntry { actionName = "Move", bindingPart = "up", displayName = "移动-上" },
                new KeyRebindEntry { actionName = "Move", bindingPart = "down", displayName = "移动-下" },
                new KeyRebindEntry { actionName = "Move", bindingPart = "left", displayName = "移动-左" },
                new KeyRebindEntry { actionName = "Move", bindingPart = "right", displayName = "移动-右" },
                new KeyRebindEntry { actionName = "ToggleCursor", displayName = "光标显隐" },
            };
            CreateOrUpdateAsset(so, $"{ResourcesConfigDir}/按键重绑定配置.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreateBackpackUIConfig()
        {
            EnsureConfigFolder();
            var so = ScriptableObject.CreateInstance<BackpackUIConfigSO>();
            so.statDisplayNames = new List<string> { "生命", "法力", "攻击", "防御", "生命回复", "法力回复", "暴击率", "暴击伤害", "攻速", "移速", "吸血", "增伤" };
            so.sortModeLabels = new List<string> { "品质低到高", "品质高到低", "其余优先 品质低到高", "其余优先 品质高到低", "武器优先 品质低到高", "武器优先 品质高到低" };
            CreateOrUpdateAsset(so, $"{ResourcesConfigDir}/背包UI配置.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreateBuffConfig()
        {
            EnsureConfigFolder();
            var so = ScriptableObject.CreateInstance<BuffConfigSO>();
            so.entries = new List<BuffEntry>
            {
                new BuffEntry { buffId = BuffIds.Lifesteal, type = BuffType.StatOnly, displayName = "吸血提升", description = "吸血比例提升30%，攻击回复生命", lifeStealAdd = 0.3f },
                new BuffEntry { buffId = BuffIds.CritRate, type = BuffType.StatOnly, displayName = "暴击率提升", description = "暴击率提升50%，更容易打出暴击", critRateAdd = 0.5f },
                new BuffEntry { buffId = BuffIds.CritDmg, type = BuffType.StatOnly, displayName = "暴击伤害提升", description = "暴击伤害提升100%，暴击更痛", critDmgAdd = 1f },
                new BuffEntry { buffId = BuffIds.AtkSpeed, type = BuffType.StatOnly, displayName = "攻速提升", description = "攻击速度提升50%，攻击更快", attackSpeedAdd = 0.5f },
                new BuffEntry { buffId = BuffIds.MoveSpeed, type = BuffType.StatOnly, displayName = "移速提升", description = "移动速度提升50%，跑得更快", moveSpeedAdd = 0.5f },
                new BuffEntry { buffId = BuffIds.HpRegen, type = BuffType.StatOnly, displayName = "生命回复提升", description = "生命回复速度+300%，快速回血", hpRegenAdd = 3f },
                new BuffEntry { buffId = BuffIds.MpRegen, type = BuffType.StatOnly, displayName = "法力回复提升", description = "法力回复速度+300%，快速回蓝", mpRegenAdd = 3f },
                new BuffEntry { buffId = BuffIds.Damage, type = BuffType.StatOnly, displayName = "伤害提升", description = "伤害提升50%，打得更痛", damageBonusAdd = 0.5f },
                new BuffEntry { buffId = BuffIds.SuperArmor, type = BuffType.SuperArmorDamageReduce, displayName = "霸体加减伤", description = "全程霸体，且提供减伤", damageReduceAdd = 0.3f },
                new BuffEntry { buffId = BuffIds.SummonClone, type = BuffType.SummonClone, displayName = "召唤分身", description = "召唤一个半透明分身跟随玩家，并由分身AI协助战斗", cloneFollowRadius = 2f, cloneOpacity = 0.45f },
                new BuffEntry { buffId = BuffIds.Afterimage, type = BuffType.Afterimage, displayName = "攻击附带重影", description = "玩家攻击会延迟再次触发一份同源的伤害、特效与音效事件", afterimageDelaySeconds = 0.2f, afterimageDamageMultiplier = 0.5f },
                new BuffEntry { buffId = BuffIds.DamageRangeExpand, type = BuffType.DamageRangeExpand, displayName = "伤害范围扩大", description = "特效、命中特效、伤害范围、碰撞体与射线距离均按倍数扩大", damageRangeScale = 2f },
            };
            CreateOrUpdateAsset(so, $"{ResourcesConfigDir}/Buff配置.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreateShopPriceConfig()
        {
            EnsureConfigFolder();
            var so = ScriptableObject.CreateInstance<ShopPriceConfigSO>();
            so.weaponCommonPrice = 100;
            so.weaponRarePrice = 200;
            so.weaponEpicPrice = 350;
            so.weaponLegendaryPrice = 500;
            so.potionHpPrice = 100;
            so.potionMpPrice = 100;
            so.nectarPrice = 500;
            CreateOrUpdateAsset(so, $"{ResourcesConfigDir}/商店价格.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreatePotionConfig()
        {
            EnsureConfigFolder();
            var so = ScriptableObject.CreateInstance<PotionConfigSO>();
            so.hpPercentPerSecond = 0.05f;
            so.mpPercentPerSecond = 0.05f;
            so.durationSeconds = 10f;
            CreateOrUpdateAsset(so, $"{ResourcesConfigDir}/药水效果.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreateSkillEffectDatabase()
        {
            EnsureConfigFolder();
            var db = SkillEffectDatabaseDefaults.CreateRuntimeDefault();
            CreateOrMergeSkillEffectDatabaseAsset(db, $"{ResourcesConfigDir}/技能效果库.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreatePassiveSkillEffectDatabase()
        {
            EnsureConfigFolder();
            var db = BuildDefaultPassiveSkillEffectDatabase();
            CreateOrUpdateAsset(db, $"{ResourcesConfigDir}/被动技能效果库.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreateSkillConfigDatabase()
        {
            EnsureConfigFolder();
            var db = BuildDefaultSkillConfigDatabase();
            CreateOrMergeSkillConfigDatabaseAsset(db, $"{ResourcesConfigDir}/玩家动作及技能配置库.asset");
            AssetDatabase.SaveAssets();
        }

        private static SkillConfigDatabaseSO BuildDefaultSkillConfigDatabase()
        {
            var db = ScriptableObject.CreateInstance<SkillConfigDatabaseSO>();
            db.entries = new List<SkillConfigEntry>
            {
                CreateBaseActionEntry("NormalIdle", "正常待机"),
                CreateBaseActionEntry("NormalWalk", "正常走路"),
                CreateBaseActionEntry("NormalRun", "正常跑步"),
                CreateBaseActionEntry("AimIdle", "射击待机", supportedAttackModes: PlayerAttackModeMask.Ranged),
                CreateBaseActionEntry("AimWalk", "射击走路", supportedAttackModes: PlayerAttackModeMask.Ranged),
                CreateBaseActionEntry("AimRun", "射击跑步", supportedAttackModes: PlayerAttackModeMask.Ranged),
                CreateBaseActionEntry("Jump", "跳跃"),
                CreateBaseActionEntry("Dodge", "闪避"),
                CreateBaseActionEntry("Fall", "坠落"),
                CreateBaseActionEntry("Land", "着陆"),
                CreateBaseActionEntry("Attack0", "普攻1"),
                CreateBaseActionEntry("Attack1", "普攻2"),
                CreateBaseActionEntry("Attack2", "普攻3"),
                CreateBaseActionEntry("Attack3", "普攻4"),
                CreateBaseActionEntry("AirAttack", "空中普攻"),
                CreateBaseActionEntry("ChargeStart", "蓄力开始"),
                CreateBaseActionEntry("ChargeLoop", "蓄力循环"),
                CreateBaseActionEntry("ChargeRelease", "蓄力释放"),
                CreateBaseActionEntry("FallAttackStart", "下落攻击开始"),
                CreateBaseActionEntry("FallAttackLoop", "下落攻击循环"),
                CreateBaseActionEntry("FallAttackLand", "下落攻击着陆"),
                CreateBaseActionEntry("HitStun", "受击"),
                CreateBaseActionEntry("PlayerDeath", "死亡"),
                CreateBaseActionEntry("Shoot", "射击", supportedAttackModes: PlayerAttackModeMask.Ranged),
                CreateBaseActionEntry("ShootCharge", "持续射击", supportedAttackModes: PlayerAttackModeMask.Ranged),
                CreateBaseActionEntry("Aim", "瞄准", supportedAttackModes: PlayerAttackModeMask.Ranged),
            };

            db.formActionMappings = new List<PlayerFormActionMappingEntry>
            {
                CreateFormActionMapping(PlayerFormActionSlot.Attack0, "Attack0", "Shoot"),
                CreateFormActionMapping(PlayerFormActionSlot.Attack1, "Attack1", "Shoot"),
                CreateFormActionMapping(PlayerFormActionSlot.Attack2, "Attack2", "Shoot"),
                CreateFormActionMapping(PlayerFormActionSlot.Attack3, "Attack3", "Shoot"),
                CreateFormActionMapping(PlayerFormActionSlot.AirAttack, "AirAttack", "Shoot"),
                CreateFormActionMapping(PlayerFormActionSlot.ChargeStart, "ChargeStart", "ShootCharge"),
                CreateFormActionMapping(PlayerFormActionSlot.ChargeLoop, "ChargeLoop", "ShootCharge"),
            };

            for (int i = 0; i < 4; i++)
                db.entries.Add(CreateActiveSkillEntry($"Skill{i}", $"主动{i + 1}"));

            for (int i = 1; i <= 12; i++)
            {
                db.entries.Add(new SkillConfigEntry
                {
                    actionId = $"passive_{i}",
                    skillId = $"passive_{i}",
                    displayName = $"被动{i}",
                    description = $"被动{i}，解锁后会立即生效。",
                    isPassive = true,
                    entryGroup = PlayerSkillEntryGroup.PassiveSkill,
                    talentCost = 1,
                    mpCost = 0,
                });
            }

            return db;
        }

        private static PassiveSkillEffectDatabaseSO BuildDefaultPassiveSkillEffectDatabase()
        {
            var db = ScriptableObject.CreateInstance<PassiveSkillEffectDatabaseSO>();
            db.groups = new List<PassiveSkillEffectGroupDefinition>
            {
                new PassiveSkillEffectGroupDefinition
                {
                    groupId = "player_passive",
                    groupName = "玩家被动技能",
                    skillGroups = new List<PassiveSkillEffectVariantGroupDefinition>(),
                    entries = new List<PassiveSkillEffectDefinition>(),
                }
            };

            List<PassiveSkillEffectVariantGroupDefinition> skillGroups = db.groups[0].skillGroups;
            for (int i = 1; i <= 12; i++)
            {
                skillGroups.Add(new PassiveSkillEffectVariantGroupDefinition
                {
                    groupName = $"passive_{i}",
                    entries = new List<PassiveSkillEffectDefinition>
                    {
                        new PassiveSkillEffectDefinition
                        {
                            skillId = $"passive_{i}",
                            statModifier = new Game.Domain.StatModifier(),
                        }
                    }
                });
            }

            db.Synchronize();
            return db;
        }

        private static SkillConfigDatabaseSO CreateOrMergeSkillConfigDatabaseAsset(SkillConfigDatabaseSO source, string assetPath)
        {
            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            source.name = objectName;

            var existing = AssetDatabase.LoadAssetAtPath<SkillConfigDatabaseSO>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, assetPath);
                return source;
            }

            MergeSkillConfigDatabase(existing, source);
            UnityEngine.Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void MergeSkillConfigDatabase(SkillConfigDatabaseSO existing, SkillConfigDatabaseSO defaults)
        {
            if (existing == null || defaults == null)
                return;

            if (existing.entries == null)
                existing.entries = new List<SkillConfigEntry>();
            if (existing.formActionMappings == null)
                existing.formActionMappings = new List<PlayerFormActionMappingEntry>();

            for (int i = 0; i < defaults.entries.Count; i++)
            {
                SkillConfigEntry defaultEntry = defaults.entries[i];
                if (defaultEntry == null)
                    continue;

                SkillConfigEntry existingEntry = FindSkillConfigEntry(existing.entries, defaultEntry);
                if (existingEntry == null)
                {
                    existing.entries.Add(CloneSkillConfigEntry(defaultEntry));
                    continue;
                }

                MergeSkillConfigEntry(existingEntry, defaultEntry);
            }

            MergeFormActionMappings(existing.formActionMappings, defaults.formActionMappings);
        }

        private static SkillConfigEntry FindSkillConfigEntry(List<SkillConfigEntry> entries, SkillConfigEntry target)
        {
            if (entries == null || target == null)
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                SkillConfigEntry entry = entries[i];
                if (entry == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(target.actionId)
                    && string.Equals(entry.GetResolvedActionId(), target.actionId, System.StringComparison.Ordinal))
                    return entry;

                if (!string.IsNullOrWhiteSpace(target.skillId)
                    && string.Equals(entry.skillId, target.skillId, System.StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static void MergeSkillConfigEntry(SkillConfigEntry existing, SkillConfigEntry defaults)
        {
            if (existing == null || defaults == null)
                return;

            if (string.IsNullOrWhiteSpace(existing.actionId))
                existing.actionId = defaults.actionId;
            if (string.IsNullOrWhiteSpace(existing.skillId))
                existing.skillId = defaults.skillId;
            if (string.IsNullOrWhiteSpace(existing.displayName))
                existing.displayName = defaults.displayName;
            if (string.IsNullOrWhiteSpace(existing.description))
                existing.description = defaults.description;
            existing.isPassive = defaults.isPassive;
            existing.entryGroup = defaults.entryGroup;

            if (existing.entryGroup == PlayerSkillEntryGroup.ActiveSkill)
            {
                if (existing.talentCost <= 0)
                    existing.talentCost = defaults.talentCost;
                if (existing.cooldownSeconds <= 0f)
                    existing.cooldownSeconds = defaults.cooldownSeconds;
            }

            if (existing.actionPolicies == null || existing.actionPolicies.Count == 0)
                existing.actionPolicies = CloneActionPolicies(defaults.actionPolicies);

            if (existing.pendingReleaseRules == null || existing.pendingReleaseRules.Count == 0)
                existing.pendingReleaseRules = ClonePendingReleaseRules(defaults.pendingReleaseRules);

            if (existing.supportedAttackModes == PlayerAttackModeMask.None
                && defaults.supportedAttackModes != PlayerAttackModeMask.None)
                existing.supportedAttackModes = defaults.supportedAttackModes;

            if (!existing.overrideNaturalExitNormalizedTime && defaults.overrideNaturalExitNormalizedTime)
            {
                existing.overrideNaturalExitNormalizedTime = true;
                existing.naturalExitNormalizedTime = defaults.naturalExitNormalizedTime;
            }

            if (existing.naturalExitTarget == PlayerStateNaturalExitTarget.None
                && defaults.naturalExitTarget != PlayerStateNaturalExitTarget.None)
                existing.naturalExitTarget = defaults.naturalExitTarget;
        }

        private static SkillConfigEntry CloneSkillConfigEntry(SkillConfigEntry source)
        {
            return new SkillConfigEntry
            {
                actionId = source.actionId,
                skillId = source.skillId,
                passiveSkillEffectDatabase = source.passiveSkillEffectDatabase,
                displayName = source.displayName,
                description = source.description,
                isPassive = source.isPassive,
                entryGroup = source.entryGroup,
                supportedAttackModes = source.supportedAttackModes,
                talentCost = source.talentCost,
                mpCost = source.mpCost,
                cooldownSeconds = source.cooldownSeconds,
                actionPolicies = CloneActionPolicies(source.actionPolicies),
                pendingReleaseRules = ClonePendingReleaseRules(source.pendingReleaseRules),
                overrideNaturalExitNormalizedTime = source.overrideNaturalExitNormalizedTime,
                naturalExitNormalizedTime = source.naturalExitNormalizedTime,
                naturalExitTarget = source.naturalExitTarget,
            };
        }

        private static void MergeFormActionMappings(List<PlayerFormActionMappingEntry> existing, List<PlayerFormActionMappingEntry> defaults)
        {
            if (existing == null || defaults == null)
                return;

            for (int i = 0; i < defaults.Count; i++)
            {
                PlayerFormActionMappingEntry defaultMapping = defaults[i];
                if (defaultMapping == null)
                    continue;

                PlayerFormActionMappingEntry existingMapping = FindFormActionMapping(existing, defaultMapping.actionSlot);
                if (existingMapping == null)
                {
                    existing.Add(CloneFormActionMapping(defaultMapping));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(existingMapping.meleeActionId))
                    existingMapping.meleeActionId = defaultMapping.meleeActionId;
                if (string.IsNullOrWhiteSpace(existingMapping.rangedActionId))
                    existingMapping.rangedActionId = defaultMapping.rangedActionId;
            }
        }

        private static PlayerFormActionMappingEntry FindFormActionMapping(List<PlayerFormActionMappingEntry> mappings, PlayerFormActionSlot actionSlot)
        {
            if (mappings == null)
                return null;

            for (int i = 0; i < mappings.Count; i++)
            {
                PlayerFormActionMappingEntry mapping = mappings[i];
                if (mapping != null && mapping.actionSlot == actionSlot)
                    return mapping;
            }

            return null;
        }

        private static PlayerFormActionMappingEntry CloneFormActionMapping(PlayerFormActionMappingEntry source)
        {
            if (source == null)
                return null;

            return new PlayerFormActionMappingEntry
            {
                actionSlot = source.actionSlot,
                meleeActionId = source.meleeActionId,
                rangedActionId = source.rangedActionId,
            };
        }

        private static List<PlayerStateActionPolicyRule> CloneActionPolicies(List<PlayerStateActionPolicyRule> source)
        {
            List<PlayerStateActionPolicyRule> result = new List<PlayerStateActionPolicyRule>();
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                PlayerStateActionPolicyRule rule = source[i];
                if (rule == null)
                    continue;

                result.Add(new PlayerStateActionPolicyRule
                {
                    action = rule.action,
                    policy = rule.policy,
                });
            }

            return result;
        }

        private static List<PlayerStatePendingReleaseRule> ClonePendingReleaseRules(List<PlayerStatePendingReleaseRule> source)
        {
            List<PlayerStatePendingReleaseRule> result = new List<PlayerStatePendingReleaseRule>();
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                PlayerStatePendingReleaseRule rule = source[i];
                if (rule == null)
                    continue;

                result.Add(new PlayerStatePendingReleaseRule
                {
                    pendingAction = rule.pendingAction,
                    normalizedTime = rule.normalizedTime,
                });
            }

            return result;
        }

        private static SkillConfigEntry CreateBaseActionEntry(
            string actionId,
            string displayName,
            PlayerAttackModeMask supportedAttackModes = PlayerAttackModeMask.All)
        {
            SkillConfigEntry entry = new SkillConfigEntry
            {
                actionId = actionId,
                skillId = actionId,
                displayName = displayName,
                description = $"{displayName}，用于玩家基础动作表现。",
                isPassive = false,
                entryGroup = PlayerSkillEntryGroup.BaseSkill,
                supportedAttackModes = supportedAttackModes,
                talentCost = 0,
                mpCost = 0,
                cooldownSeconds = 0f,
            };

            ApplyDefaultActionRules(entry);
            return entry;
        }

        private static SkillConfigEntry CreateActiveSkillEntry(string actionId, string displayName)
        {
            SkillConfigEntry entry = new SkillConfigEntry
            {
                actionId = actionId,
                skillId = actionId,
                displayName = displayName,
                description = $"{displayName}，解锁后可拖拽到下方技能槽位中释放。",
                isPassive = false,
                entryGroup = PlayerSkillEntryGroup.ActiveSkill,
                talentCost = 2,
                mpCost = 10,
                cooldownSeconds = 3f,
            };

            ApplyDefaultActionRules(entry);
            return entry;
        }

        private static void ApplyDefaultActionRules(SkillConfigEntry entry)
        {
            if (entry == null || entry.IsPassiveSkill || string.IsNullOrWhiteSpace(entry.actionId))
                return;

            switch (entry.actionId)
            {
                case "NormalIdle":
                case "AimIdle":
                    AddPolicy(entry, GameAction.Walk, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Run, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Jump, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Shoot, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ShootCharge, TransitionPolicy.Interrupt);
                    break;
                case "NormalWalk":
                case "NormalRun":
                case "AimWalk":
                case "AimRun":
                    AddPolicy(entry, GameAction.Jump, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Shoot, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ShootCharge, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Walk, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Run, TransitionPolicy.Ignore);
                    break;
                case "Jump":
                case "Fall":
                    AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Skill, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.AirAttack, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.FallAttack, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Jump, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ChargeStart, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ChargeRelease, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Shoot, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ShootCharge, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Walk, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Run, TransitionPolicy.Ignore);
                    break;
                case "Dodge":
                    AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Skill, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ChargeRelease, TransitionPolicy.Ignore);
                    AddPending(entry, GameAction.Walk, 0.80f);
                    AddPending(entry, GameAction.Run, 0.80f);
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    entry.naturalExitTarget = PlayerStateNaturalExitTarget.IdleState;
                    break;
                case "Land":
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    entry.naturalExitTarget = PlayerStateNaturalExitTarget.IdleState;
                    break;
                case "Attack0":
                case "Attack1":
                case "Attack2":
                case "Attack3":
                    AddPolicy(entry, GameAction.Jump, TransitionPolicy.Interrupt);
                    AddPending(entry, GameAction.NormalAttack, entry.actionId == "Attack3" ? 0.60f : 0.70f);
                    AddPending(entry, GameAction.Walk, entry.actionId == "Attack3" ? 0.70f : 0.80f);
                    AddPending(entry, GameAction.Run, entry.actionId == "Attack3" ? 0.70f : 0.80f);
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    entry.naturalExitTarget = PlayerStateNaturalExitTarget.IdleState;
                    break;
                case "Shoot":
                    AddPolicy(entry, GameAction.Jump, TransitionPolicy.Interrupt);
                    AddPending(entry, GameAction.Shoot, 0.70f);
                    AddPending(entry, GameAction.Walk, 0.80f);
                    AddPending(entry, GameAction.Run, 0.80f);
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    entry.naturalExitTarget = PlayerStateNaturalExitTarget.IdleState;
                    break;
                case "AirAttack":
                    AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Skill, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.FallAttack, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.AirAttack, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ChargeStart, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ChargeRelease, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Jump, TransitionPolicy.Ignore);
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    break;
                case "ChargeStart":
                    AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Skill, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ChargeRelease, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ChargeStart, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ShootCharge, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Shoot, TransitionPolicy.Ignore);
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    entry.naturalExitTarget = PlayerStateNaturalExitTarget.ChargeLoopState;
                    break;
                case "ChargeLoop":
                    AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Skill, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ChargeRelease, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ChargeStart, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ShootCharge, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Shoot, TransitionPolicy.Ignore);
                    break;
                case "ShootCharge":
                    AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Skill, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ChargeRelease, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.ShootCharge, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.Shoot, TransitionPolicy.Ignore);
                    break;
                case "ChargeRelease":
                    AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Skill, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ChargeStart, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.ShootCharge, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.ChargeRelease, TransitionPolicy.Ignore);
                    AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.Shoot, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.Jump, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.Walk, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.Run, TransitionPolicy.Buffer);
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    entry.naturalExitTarget = PlayerStateNaturalExitTarget.IdleState;
                    break;
                case "FallAttackStart":
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    entry.naturalExitTarget = PlayerStateNaturalExitTarget.FallAttackLoopState;
                    break;
                case "FallAttackLand":
                    AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.Skill, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.ChargeStart, TransitionPolicy.Interrupt);
                    AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.Jump, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.Walk, TransitionPolicy.Buffer);
                    AddPolicy(entry, GameAction.Run, TransitionPolicy.Buffer);
                    entry.overrideNaturalExitNormalizedTime = true;
                    entry.naturalExitNormalizedTime = 0.90f;
                    entry.naturalExitTarget = PlayerStateNaturalExitTarget.IdleState;
                    break;
                default:
                    if (entry.entryGroup == PlayerSkillEntryGroup.ActiveSkill)
                    {
                        AddPolicy(entry, GameAction.Dodge, TransitionPolicy.Interrupt);
                        AddPolicy(entry, GameAction.Skill, TransitionPolicy.Interrupt);
                        AddPolicy(entry, GameAction.ChargeStart, TransitionPolicy.Interrupt);
                        AddPolicy(entry, GameAction.ShootCharge, TransitionPolicy.Interrupt);
                        AddPolicy(entry, GameAction.NormalAttack, TransitionPolicy.Ignore);
                        AddPolicy(entry, GameAction.Shoot, TransitionPolicy.Ignore);
                        AddPolicy(entry, GameAction.ChargeRelease, TransitionPolicy.Ignore);
                        entry.overrideNaturalExitNormalizedTime = true;
                        entry.naturalExitNormalizedTime = 0.90f;
                        entry.naturalExitTarget = PlayerStateNaturalExitTarget.IdleState;
                    }
                    break;
            }
        }

        private static void AddPolicy(SkillConfigEntry entry, GameAction action, TransitionPolicy policy)
        {
            if (entry == null)
                return;

            if (entry.actionPolicies == null)
                entry.actionPolicies = new List<PlayerStateActionPolicyRule>();

            for (int i = 0; i < entry.actionPolicies.Count; i++)
            {
                PlayerStateActionPolicyRule rule = entry.actionPolicies[i];
                if (rule != null && rule.action == action)
                {
                    rule.policy = policy;
                    return;
                }
            }

            entry.actionPolicies.Add(new PlayerStateActionPolicyRule { action = action, policy = policy });
        }

        private static void AddPending(SkillConfigEntry entry, GameAction action, float normalizedTime)
        {
            if (entry == null)
                return;

            if (entry.pendingReleaseRules == null)
                entry.pendingReleaseRules = new List<PlayerStatePendingReleaseRule>();

            for (int i = 0; i < entry.pendingReleaseRules.Count; i++)
            {
                PlayerStatePendingReleaseRule rule = entry.pendingReleaseRules[i];
                if (rule != null && rule.pendingAction == action)
                {
                    rule.normalizedTime = normalizedTime;
                    return;
                }
            }

            entry.pendingReleaseRules.Add(new PlayerStatePendingReleaseRule { pendingAction = action, normalizedTime = normalizedTime });
        }

        private static void CreateCharacterAnimationLibrary()
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<CharacterAnimationLibrarySO>();
            db.groups = CloneExistingAnimationGroupsOrDefault();
            CreateOrMergeCharacterAnimationLibraryAsset(db, $"{ResourcesConfigDir}/动画库.asset");
            AssetDatabase.SaveAssets();
        }

        private static List<CharacterAnimationGroupDefinition> CloneExistingAnimationGroupsOrDefault()
        {
            string assetPath = $"{ResourcesConfigDir}/动画库.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CharacterAnimationLibrarySO>(assetPath);
            if (existing != null && existing.groups != null && existing.groups.Count > 0)
                return CloneAnimationGroups(existing.groups);

            return new List<CharacterAnimationGroupDefinition>
            {
                CreateAnimationGroup("player", "玩家", CreatePlayerAnimationEntries()),
                CreateAnimationGroup("melee_minion", "近战小怪"),
                CreateAnimationGroup("ranged_minion", "远程小怪"),
                CreateAnimationGroup("elite_1", "精英1"),
                CreateAnimationGroup("elite_2", "精英2"),
                CreateAnimationGroup("guardian_1", "守卫者1"),
                CreateAnimationGroup("guardian_2", "守卫者2"),
                CreateAnimationGroup("boss_1", "Boss1"),
                CreateAnimationGroup("boss_2", "Boss2"),
                CreateAnimationGroup("boss_3", "Boss3"),
                CreateAnimationGroup("boss_4", "Boss4"),
                CreateAnimationGroup("boss_5", "Boss5"),
            };
        }

        private static List<CharacterAnimationGroupDefinition> CloneAnimationGroups(List<CharacterAnimationGroupDefinition> groups)
        {
            var result = new List<CharacterAnimationGroupDefinition>();
            if (groups == null)
                return result;

            for (int i = 0; i < groups.Count; i++)
            {
                CharacterAnimationGroupDefinition group = groups[i];
                if (group == null)
                    continue;

                var clonedGroup = new CharacterAnimationGroupDefinition
                {
                    groupId = group.groupId,
                    groupName = group.groupName,
                    entries = new List<CharacterAnimationEntry>(),
                };

                if (group.entries != null)
                {
                    for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
                    {
                        CharacterAnimationEntry entry = group.entries[entryIndex];
                        if (entry == null)
                            continue;

                        clonedGroup.entries.Add(new CharacterAnimationEntry
                        {
                            animationId = entry.animationId,
                            clip = entry.clip,
                        });
                    }
                }

                result.Add(clonedGroup);
            }

            return result;
        }

        private static CharacterAnimationGroupDefinition CreateAnimationGroup(
            string groupId,
            string groupName,
            List<CharacterAnimationEntry> entries = null)
        {
            return new CharacterAnimationGroupDefinition
            {
                groupId = groupId,
                groupName = groupName,
                entries = entries ?? new List<CharacterAnimationEntry>(),
            };
        }

        private static List<CharacterAnimationEntry> CreatePlayerAnimationEntries()
        {
            return new List<CharacterAnimationEntry>
            {
                CreateAnimationEntry("Attack0"),
                CreateAnimationEntry("Attack1"),
                CreateAnimationEntry("Attack2"),
                CreateAnimationEntry("Attack3"),
                CreateAnimationEntry("Skill0"),
                CreateAnimationEntry("Skill1"),
                CreateAnimationEntry("Skill2"),
                CreateAnimationEntry("Skill3"),
                CreateAnimationEntry("AirAttack"),
                CreateAnimationEntry("FallAttack"),
                CreateAnimationEntry("ChargeRelease"),
                CreateAnimationEntry("Aim", "Assets/外部导入/人物动画/射击相关/AimAndShoot_Charge.anim"),
                CreateAnimationEntry("Shoot", "Assets/外部导入/人物动画/射击相关/ShootOnce.anim"),
                CreateAnimationEntry("ShootCharge", "Assets/外部导入/人物动画/射击相关/AimAndShoot_Charge.anim"),
                CreateAnimationEntry("Rifle_WalkFwdLoop", "Assets/外部导入/人物动画/射击相关/Rifle_WalkFwdLoop.anim"),
                CreateAnimationEntry("Rifle_WalkBwdLoop", "Assets/外部导入/人物动画/射击相关/Rifle_WalkBwdLoop.anim"),
                CreateAnimationEntry("Rifle_StrafeWalkLeftLoop", "Assets/外部导入/人物动画/射击相关/Rifle_StrafeWalkLeftLoop.anim"),
                CreateAnimationEntry("Rifle_StrafeWalkRightLoop", "Assets/外部导入/人物动画/射击相关/Rifle_StrafeWalkRightLoop.anim"),
                CreateAnimationEntry("Rifle_RunFwdLoop", "Assets/外部导入/人物动画/射击相关/Rifle_RunFwdLoop.anim"),
                CreateAnimationEntry("Rifle_RunBwdLoop", "Assets/外部导入/人物动画/射击相关/Rifle_RunBwdLoop.anim"),
                CreateAnimationEntry("Rifle_StrafeRunLeftLoop", "Assets/外部导入/人物动画/射击相关/Rifle_StrafeRunLeftLoop.anim"),
                CreateAnimationEntry("Rifle_StrafeRunRightLoop", "Assets/外部导入/人物动画/射击相关/Rifle_StrafeRunRightLoop.anim"),
            };
        }

        private static PlayerFormActionMappingEntry CreateFormActionMapping(PlayerFormActionSlot actionSlot, string meleeActionId, string rangedActionId)
        {
            return new PlayerFormActionMappingEntry
            {
                actionSlot = actionSlot,
                meleeActionId = meleeActionId,
                rangedActionId = rangedActionId,
            };
        }

        private static CharacterAnimationEntry CreateAnimationEntry(string animationId)
        {
            return new CharacterAnimationEntry
            {
                animationId = animationId,
                clip = null,
            };
        }

        private static CharacterAnimationEntry CreateAnimationEntry(string animationId, string clipAssetPath)
        {
            return new CharacterAnimationEntry
            {
                animationId = animationId,
                clip = string.IsNullOrWhiteSpace(clipAssetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<AnimationClip>(clipAssetPath),
            };
        }

        private static void CreateEnemyArchetypeDatabase()
        {
            EnsureConfigFolder();
            var archetypes = new List<EnemyArchetypeSO>
            {
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "melee_minion",
                        "近战小怪",
                        EnemyType.MeleeMinion,
                        120f,
                        12f,
                        20f,
                        10f,
                        BuildSkillSlot("melee_minion", 0, "劈砍", castRange: 2.2f,  cooldown: 0.65f, castDuration: 0.72f, idealCastRange: 1.9f, skillRole: EnemySkillRole.Pressure, riskWeight: 0.22f, punishWeight: 0.3f, repeatPenalty: 0.22f)),
                    $"{ResourcesConfigDir}/敌人行为_近战小怪.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "ranged_minion",
                        "远程小怪",
                        EnemyType.RangedMinion,
                        130f,
                        14f,
                        22f,
                        10f,
                        BuildSkillSlot("ranged_minion", 0, "射击", castRange: 12f,   cooldown: 1.2f,  castDuration: 0.85f, idealCastRange: 10.5f, skillRole: EnemySkillRole.AreaControl, riskWeight: 0.18f, punishWeight: 0.2f, repeatPenalty: 0.18f)),
                    $"{ResourcesConfigDir}/敌人行为_远程小怪.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "elite_1",
                        "精英1",
                        EnemyType.Elite,
                        145f,
                        16f,
                        28f,
                        12f,
                        BuildSkillSlot("elite_1", 0, "连斩",     castRange: 2.6f,  cooldown: 0.95f, castDuration: 1f,    idealCastRange: 2.2f, skillRole: EnemySkillRole.Pressure, riskWeight: 0.28f, punishWeight: 0.35f, repeatPenalty: 0.25f),
                        BuildSkillSlot("elite_1", 1, "重砸",     castRange: 3f,    cooldown: 3.2f,  castDuration: 1.1f,  enterIdleAfterCast: true, postCastIdleDuration: 0.3f, idealCastRange: 2.7f, skillRole: EnemySkillRole.Punish, riskWeight: 0.48f, punishWeight: 0.82f, repeatPenalty: 0.34f)),
                    $"{ResourcesConfigDir}/敌人行为_精英1.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "elite_2",
                        "精英2",
                        EnemyType.Elite,
                        150f,
                        17f,
                        30f,
                        12f,
                        BuildSkillSlot("elite_2", 0, "爆裂射击", castRange: 13f,   cooldown: 1.6f,  castDuration: 1.2f,  idealCastRange: 11.5f, skillRole: EnemySkillRole.AreaControl, riskWeight: 0.28f, punishWeight: 0.3f, repeatPenalty: 0.22f),
                        BuildSkillSlot("elite_2", 1, "钩索拉扯", castRange: 4f,    cooldown: 3.6f,  castDuration: 1.1f,  minCastRange: 1.5f, idealCastRange: 3.4f, skillRole: EnemySkillRole.Punish, riskWeight: 0.4f, punishWeight: 0.72f, repeatPenalty: 0.28f, canUseUnderThreat: true)),
                    $"{ResourcesConfigDir}/敌人行为_精英2.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "guardian_1",
                        "守卫者1",
                        EnemyType.Guardian,
                        145f,
                        18f,
                        30f,
                        14f,
                        BuildSkillSlot("guardian_1", 0, "粉碎重击", castRange: 2.8f,  cooldown: 3.5f,  castDuration: 1.15f, enterIdleAfterCast: true,  postCastIdleDuration: 0.35f, idealCastRange: 2.4f, skillRole: EnemySkillRole.Pressure, riskWeight: 0.5f, punishWeight: 0.46f, repeatPenalty: 0.32f),
                        BuildSkillSlot("guardian_1", 1, "强固姿态", castRange: 1f,    cooldown: 5f,    castDuration: 1f,    rotateToTargetOnCast: false, idealCastRange: 1f, skillRole: EnemySkillRole.Escape, riskWeight: 0.08f, punishWeight: 0f, repeatPenalty: 0.15f, canUseUnderThreat: true)),
                    $"{ResourcesConfigDir}/敌人行为_守卫者1.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "guardian_2",
                        "守卫者2",
                        EnemyType.Guardian,
                        150f,
                        20f,
                        32f,
                        14f,
                        BuildSkillSlot("guardian_2", 0, "盾击",   castRange: 3.2f,  cooldown: 3.4f,  castDuration: 1f,    enterIdleAfterCast: true,  postCastIdleDuration: 0.3f, idealCastRange: 2.8f, skillRole: EnemySkillRole.Pressure, riskWeight: 0.42f, punishWeight: 0.4f, repeatPenalty: 0.28f),
                        BuildSkillSlot("guardian_2", 1, "锁链拖拽", castRange: 5f,  cooldown: 4.2f,  castDuration: 1.1f,  minCastRange: 2f, idealCastRange: 4.3f, skillRole: EnemySkillRole.Punish, riskWeight: 0.38f, punishWeight: 0.75f, repeatPenalty: 0.3f, canUseUnderThreat: true)),
                    $"{ResourcesConfigDir}/敌人行为_守卫者2.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_1",
                        "Boss1",
                        EnemyType.Boss,
                        160f,
                        22f,
                        40f,
                        16f,
                        BuildSkillSlot("boss_1", 0, "斩击",      castRange: 3f,    cooldown: 2.2f,  castDuration: 0.9f,  idealCastRange: 2.6f, skillRole: EnemySkillRole.Pressure, riskWeight: 0.28f, punishWeight: 0.35f, repeatPenalty: 0.2f),
                        BuildSkillSlot("boss_1", 1, "射击",      castRange: 14f,   cooldown: 2.8f,  castDuration: 1f,    idealCastRange: 12.5f, skillRole: EnemySkillRole.AreaControl, riskWeight: 0.22f, punishWeight: 0.25f, repeatPenalty: 0.18f),
                        BuildSkillSlot("boss_1", 2, "冲锋",      castRange: 5f,    cooldown: 4f,    castDuration: 1f,    enterIdleAfterCast: true,  postCastIdleDuration: 0.45f, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, minCastRange: 2f, idealCastRange: 4.2f, skillRole: EnemySkillRole.GapClose, riskWeight: 0.48f, punishWeight: 0.58f, repeatPenalty: 0.3f, canUseUnderThreat: true),
                        BuildSkillSlot("boss_1", 3, "战吼",      castRange: 8f,    cooldown: 5f,    castDuration: 1.2f,  enterIdleAfterCast: true,  postCastIdleDuration: 0.55f, rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, idealCastRange: 7f, skillRole: EnemySkillRole.Escape, riskWeight: 0.18f, punishWeight: 0.18f, repeatPenalty: 0.25f, canUseUnderThreat: true)),
                    $"{ResourcesConfigDir}/敌人行为_Boss1.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_2",
                        "Boss2",
                        EnemyType.Boss,
                        165f,
                        23f,
                        42f,
                        16f,
                        BuildSkillSlot("boss_2", 0, "十字斩",    castRange: 3.2f,  cooldown: 2.1f,  castDuration: 1f,    idealCastRange: 2.8f, skillRole: EnemySkillRole.Pressure, riskWeight: 0.28f, punishWeight: 0.36f, repeatPenalty: 0.2f),
                        BuildSkillSlot("boss_2", 1, "连射",      castRange: 15f,   cooldown: 2.7f,  castDuration: 1.2f,  idealCastRange: 13.2f, skillRole: EnemySkillRole.AreaControl, riskWeight: 0.24f, punishWeight: 0.22f, repeatPenalty: 0.18f),
                        BuildSkillSlot("boss_2", 2, "崩击",      castRange: 5.5f,  cooldown: 3.8f,  castDuration: 1.05f, enterIdleAfterCast: true,  postCastIdleDuration: 0.5f,  phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, minCastRange: 2.2f, idealCastRange: 4.7f, skillRole: EnemySkillRole.GapClose, riskWeight: 0.45f, punishWeight: 0.62f, repeatPenalty: 0.3f, canUseUnderThreat: true),
                        BuildSkillSlot("boss_2", 3, "狂怒",      castRange: 8f,    cooldown: 4.8f,  castDuration: 1.15f, enterIdleAfterCast: true,  postCastIdleDuration: 0.55f, rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, idealCastRange: 7.2f, skillRole: EnemySkillRole.Escape, riskWeight: 0.16f, punishWeight: 0.16f, repeatPenalty: 0.24f, canUseUnderThreat: true)),
                    $"{ResourcesConfigDir}/敌人行为_Boss2.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_3",
                        "Boss3",
                        EnemyType.Boss,
                        170f,
                        24f,
                        44f,
                        17f,
                        BuildSkillSlot("boss_3", 0, "勾拽劈斩",  castRange: 4f,    cooldown: 2.4f,  castDuration: 1.05f, idealCastRange: 3.5f, skillRole: EnemySkillRole.Punish, riskWeight: 0.32f, punishWeight: 0.68f, repeatPenalty: 0.24f),
                        BuildSkillSlot("boss_3", 1, "弹幕",      castRange: 15f,   cooldown: 2.9f,  castDuration: 1.25f, idealCastRange: 13.5f, skillRole: EnemySkillRole.AreaControl, riskWeight: 0.26f, punishWeight: 0.2f, repeatPenalty: 0.18f),
                        BuildSkillSlot("boss_3", 2, "突刺",      castRange: 5.8f,  cooldown: 3.9f,  castDuration: 1f,    enterIdleAfterCast: true,  postCastIdleDuration: 0.45f, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, minCastRange: 2.4f, idealCastRange: 4.9f, skillRole: EnemySkillRole.GapClose, riskWeight: 0.44f, punishWeight: 0.6f, repeatPenalty: 0.3f, canUseUnderThreat: true),
                        BuildSkillSlot("boss_3", 3, "吸取",      castRange: 8.5f,  cooldown: 5.2f,  castDuration: 1.25f, enterIdleAfterCast: true,  postCastIdleDuration: 0.55f, rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, idealCastRange: 7.6f, skillRole: EnemySkillRole.Escape, riskWeight: 0.18f, punishWeight: 0.18f, repeatPenalty: 0.24f, canUseUnderThreat: true)),
                    $"{ResourcesConfigDir}/敌人行为_Boss3.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_4",
                        "Boss4",
                        EnemyType.Boss,
                        175f,
                        25f,
                        46f,
                        17f,
                        BuildSkillSlot("boss_4", 0, "旋斩",      castRange: 3.5f,  cooldown: 2f,    castDuration: 1.15f, idealCastRange: 3f, skillRole: EnemySkillRole.Pressure, riskWeight: 0.34f, punishWeight: 0.34f, repeatPenalty: 0.2f),
                        BuildSkillSlot("boss_4", 1, "穿刺射击",  castRange: 15.5f, cooldown: 2.5f,  castDuration: 1f,    idealCastRange: 13.8f, skillRole: EnemySkillRole.AreaControl, riskWeight: 0.22f, punishWeight: 0.2f, repeatPenalty: 0.18f),
                        BuildSkillSlot("boss_4", 2, "追猎突进",  castRange: 6.2f,  cooldown: 3.5f,  castDuration: 0.95f, enterIdleAfterCast: true,  postCastIdleDuration: 0.5f,  phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, minCastRange: 2.6f, idealCastRange: 5.2f, skillRole: EnemySkillRole.GapClose, riskWeight: 0.42f, punishWeight: 0.56f, repeatPenalty: 0.28f, canUseUnderThreat: true),
                        BuildSkillSlot("boss_4", 3, "战吼",      castRange: 8.8f,  cooldown: 4.4f,  castDuration: 1.15f, enterIdleAfterCast: true,  postCastIdleDuration: 0.55f, rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, idealCastRange: 7.8f, skillRole: EnemySkillRole.Escape, riskWeight: 0.14f, punishWeight: 0.16f, repeatPenalty: 0.22f, canUseUnderThreat: true)),
                    $"{ResourcesConfigDir}/敌人行为_Boss4.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_5",
                        "Boss5",
                        EnemyType.Boss,
                        180f,
                        26f,
                        48f,
                        18f,
                        BuildSkillSlot("boss_5", 0, "横斩",      castRange: 3.6f,  cooldown: 1.9f,  castDuration: 1f,    idealCastRange: 3.1f, skillRole: EnemySkillRole.Pressure, riskWeight: 0.3f, punishWeight: 0.32f, repeatPenalty: 0.18f),
                        BuildSkillSlot("boss_5", 1, "风暴射击",  castRange: 16f,   cooldown: 2.3f,  castDuration: 1.3f,  idealCastRange: 14.2f, skillRole: EnemySkillRole.AreaControl, riskWeight: 0.24f, punishWeight: 0.2f, repeatPenalty: 0.18f),
                        BuildSkillSlot("boss_5", 2, "践踏冲锋",  castRange: 6.5f,  cooldown: 3.3f,  castDuration: 1.05f, enterIdleAfterCast: true,  postCastIdleDuration: 0.6f,  phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, minCastRange: 2.8f, idealCastRange: 5.4f, skillRole: EnemySkillRole.GapClose, riskWeight: 0.44f, punishWeight: 0.58f, repeatPenalty: 0.28f, canUseUnderThreat: true),
                        BuildSkillSlot("boss_5", 3, "回复战吼",  castRange: 9f,    cooldown: 4.2f,  castDuration: 1.25f, enterIdleAfterCast: true,  postCastIdleDuration: 0.6f,  rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly, idealCastRange: 8f, skillRole: EnemySkillRole.Escape, riskWeight: 0.14f, punishWeight: 0.16f, repeatPenalty: 0.22f, canUseUnderThreat: true)),
                    $"{ResourcesConfigDir}/敌人行为_Boss5.asset"),
            };

            NormalizeSkillSlotOverrides(archetypes);
            AssetDatabase.SaveAssets();
        }

        private static void CreateItemDisplayConfig()
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<ItemDisplayDatabaseSO>();
            db.iconScale = 0.2f;
            db.spawnHeightOffset = 0.5f;
            db.hoverAmplitude = 0.08f;
            db.hoverFrequency = 2.4f;
            db.pickupRadius = 0.7f;
            db.entries = new List<ItemDisplayEntry>
            {
                new ItemDisplayEntry { itemId = "potion_hp",  displayName = "回血药剂",  description = "使用后每秒回复5%最大生命值，持续10秒。", rarity = WeaponRarity.Epic },
                new ItemDisplayEntry { itemId = "potion_mp",  displayName = "回蓝药剂",  description = "使用后每秒回复5%最大法力值，持续10秒。", rarity = WeaponRarity.Epic },
                new ItemDisplayEntry { itemId = "nectar",     displayName = "仙露",      description = "死亡时可消耗1个仙露复活，并回滚至检查点且难度-1。", rarity = WeaponRarity.Legendary },
            };
            CreateOrMergeItemDisplayDatabaseAsset(db, $"{ResourcesConfigDir}/物品显示配置.asset");
            AssetDatabase.SaveAssets();
        }
    }
}
