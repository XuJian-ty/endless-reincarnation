using UnityEditor;
using UnityEngine;
using Game.Data;
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
        private const string LegacyResourcesConfigDir = "Assets/Resources/閰嶇疆";

        private static void EnsureConfigFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (AssetDatabase.IsValidFolder(ResourcesConfigDir))
                return;

            // Migrate old mojibake folder name created by earlier scripts.
            if (AssetDatabase.IsValidFolder(LegacyResourcesConfigDir))
            {
                string moveError = AssetDatabase.MoveAsset(LegacyResourcesConfigDir, ResourcesConfigDir);
                if (!string.IsNullOrEmpty(moveError))
                    Debug.LogWarning($"[配置] 迁移旧配置目录失败：{moveError}");
                if (AssetDatabase.IsValidFolder(ResourcesConfigDir))
                    return;
            }

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

        private static SharedSkillDatabaseSO CreateOrMergeSharedSkillDatabaseAsset(SharedSkillDatabaseSO source, string assetPath)
        {
            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            source.name = objectName;

            var existing = AssetDatabase.LoadAssetAtPath<SharedSkillDatabaseSO>(assetPath);
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

        private static EnemySkillSlotBinding BuildSkillSlot(
            int slotIndex,
            string skillId,
            float castRange = 3f,
            float cooldown = 1f,
            float castDuration = 0.6f,
            string animationTrigger = "",
            EnemySkillPhaseAvailability phaseAvailability = EnemySkillPhaseAvailability.Always,
            float postCastIdleDuration = 0f,
            bool enterIdleAfterCast = false,
            bool rotateToTargetOnCast = true)
        {
            return new EnemySkillSlotBinding
            {
                slotIndex            = slotIndex,
                skillId              = skillId,
                castRange            = castRange,
                cooldown             = cooldown,
                castDuration         = castDuration,
                animationTrigger     = animationTrigger,
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
            Game.AI.BehaviorTreeAsset defaultBehaviorTree,
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
            archetype.behaviorTreeAsset = defaultBehaviorTree;
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

            archetype.enableTacticalCombat = true;
            archetype.chaseInnerDistance = Mathf.Max(1.5f, Mathf.Min(maxRange, minRange + (isBossOrRanged ? 2.5f : 1.2f)));
            archetype.chaseOuterDistance = Mathf.Max(archetype.chaseInnerDistance + 2f, maxRange + (isBossOrRanged ? 3.5f : 2f));
            archetype.preferredSafetyDistanceOffset = isBossOrRanged ? 0.9f : 0.25f;
            archetype.combatDistanceTolerance = isBossOrRanged ? 1.1f : 0.75f;
            archetype.castRangeTolerance = 0.25f;
            archetype.retreatStepDistance = isBossOrRanged ? 4.2f : 2.6f;
            archetype.tacticalHoldDuration = 0.12f;
            archetype.approachLeadTime = isBossOrRanged ? 0.25f : 0.15f;
            archetype.losProbeInterval = 0.1f;
            archetype.pathProbeInterval = 0.3f;
            archetype.losBlockedPenaltySeconds = 0.35f;
            archetype.perceptionEyeHeight = 1.2f;
            archetype.obstacleMask = Physics.DefaultRaycastLayers;
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
            db.entries = new List<EnemyStatsEntry>
            {
                BuildEnemyStats("melee_minion", "近战小怪", EnemyType.MeleeMinion, 60f, 10f, 5f, 3.8f, 10, 10, 10),
                BuildEnemyStats("ranged_minion", "远程小怪", EnemyType.RangedMinion, 45f, 12f, 3f, 3.6f, 10, 10, 10),
                BuildEnemyStats("elite_1", "精英1", EnemyType.Elite, 160f, 18f, 8f, 4f, 30, 30, 30),
                BuildEnemyStats("elite_2", "精英2", EnemyType.Elite, 180f, 16f, 10f, 3.9f, 30, 30, 30),
                BuildEnemyStats("guardian_1", "守卫者1", EnemyType.Guardian, 260f, 22f, 10f, 4.2f, 0, 0, 0),
                BuildEnemyStats("guardian_2", "守卫者2", EnemyType.Guardian, 300f, 20f, 12f, 3.9f, 0, 0, 0),
                BuildEnemyStats("boss_1", "Boss1", EnemyType.Boss, 750f, 28f, 12f, 4.5f, 0, 0, 0),
                BuildEnemyStats("boss_2", "Boss2", EnemyType.Boss, 820f, 30f, 13f, 4.4f, 0, 0, 0),
                BuildEnemyStats("boss_3", "Boss3", EnemyType.Boss, 900f, 32f, 14f, 4.3f, 0, 0, 0),
                BuildEnemyStats("boss_4", "Boss4", EnemyType.Boss, 980f, 34f, 15f, 4.2f, 0, 0, 0),
                BuildEnemyStats("boss_5", "Boss5", EnemyType.Boss, 1100f, 36f, 16f, 4.1f, 0, 0, 0),
            };
            CreateOrUpdateAsset(db, $"{ResourcesConfigDir}/敌人属性库.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreateLevelConfigDatabase()
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<LevelConfigDatabaseSO>();
            db.levels = new List<LevelConfigData>
            {
                new LevelConfigData { levelIndex = 1, guardianCount = 3,  meleeMinCount = 0, meleeMaxCount = 4, rangedMinCount = 0, rangedMaxCount = 2, eliteMinCount = 0, eliteMaxCount = 1, chestMinCount = 0, chestMaxCount = 2, shopCount = 2, cellSize = 40f },
                new LevelConfigData { levelIndex = 2, guardianCount = 4,  meleeMinCount = 0, meleeMaxCount = 4, rangedMinCount = 0, rangedMaxCount = 2, eliteMinCount = 0, eliteMaxCount = 1, chestMinCount = 0, chestMaxCount = 2, shopCount = 2, cellSize = 40f },
                new LevelConfigData { levelIndex = 3, guardianCount = 6,  meleeMinCount = 0, meleeMaxCount = 4, rangedMinCount = 0, rangedMaxCount = 2, eliteMinCount = 0, eliteMaxCount = 1, chestMinCount = 0, chestMaxCount = 2, shopCount = 2, cellSize = 40f },
                new LevelConfigData { levelIndex = 4, guardianCount = 7,  meleeMinCount = 0, meleeMaxCount = 4, rangedMinCount = 0, rangedMaxCount = 2, eliteMinCount = 0, eliteMaxCount = 1, chestMinCount = 0, chestMaxCount = 2, shopCount = 2, cellSize = 40f },
                new LevelConfigData { levelIndex = 5, guardianCount = 10, meleeMinCount = 0, meleeMaxCount = 4, rangedMinCount = 0, rangedMaxCount = 2, eliteMinCount = 0, eliteMaxCount = 1, chestMinCount = 0, chestMaxCount = 2, shopCount = 2, cellSize = 40f },
            };
            CreateOrUpdateAsset(db, $"{ResourcesConfigDir}/关卡配置库.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreateDropTableDatabase()
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<DropTableDatabaseSO>();
            db.levelTables = new List<DropTableByLevel>
            {
                new DropTableByLevel { levelIndex = 1, entries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.3f }, new DropEntry { itemType = "weapon_rare", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
                new DropTableByLevel { levelIndex = 2, entries = new List<DropEntry> { new DropEntry { itemType = "weapon_common", weight = 0.15f }, new DropEntry { itemType = "weapon_rare", weight = 0.15f }, new DropEntry { itemType = "weapon_epic", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
                new DropTableByLevel { levelIndex = 3, entries = new List<DropEntry> { new DropEntry { itemType = "weapon_rare", weight = 0.2f }, new DropEntry { itemType = "weapon_epic", weight = 0.2f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
                new DropTableByLevel { levelIndex = 4, entries = new List<DropEntry> { new DropEntry { itemType = "weapon_epic", weight = 0.3f }, new DropEntry { itemType = "weapon_legendary", weight = 0.1f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
                new DropTableByLevel { levelIndex = 5, entries = new List<DropEntry> { new DropEntry { itemType = "weapon_legendary", weight = 0.4f }, new DropEntry { itemType = "potion_hp", weight = 0.25f }, new DropEntry { itemType = "potion_mp", weight = 0.25f }, new DropEntry { itemType = "nectar", weight = 0.1f } } },
            };
            CreateOrUpdateAsset(db, $"{ResourcesConfigDir}/掉落表库.asset");
            AssetDatabase.SaveAssets();
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
            var defaultBehaviorTree = CreateDefaultBehaviorTreeAsset.Create();
            CreateEnemyStatsDatabase();
            CreateSharedSkillDatabase();
            CreateEnemyArchetypeDatabase(defaultBehaviorTree);
            CreateLevelConfigDatabase();
            CreateDropTableDatabase();
            CreateWeaponDatabase();
            CreatePlayerGrowthDatabase();
            CreateOrUpdateAsset(
                ScriptableObject.CreateInstance<DifficultyScalingSO>(),
                $"{ResourcesConfigDir}/难度系数.asset");
            AssetDatabase.SaveAssets();
            CreateShopPriceConfig();
            CreatePotionConfig();
            CreateSkillConfigDatabase();
            CreateCharacterAnimationLibrary();
            DeleteLegacyAnimationFrameDamageDatabaseAsset();
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
                new KeyRebindEntry { actionName = "Skill0", displayName = "技能0" },
                new KeyRebindEntry { actionName = "Skill1", displayName = "技能1" },
                new KeyRebindEntry { actionName = "Skill2", displayName = "技能2" },
                new KeyRebindEntry { actionName = "Skill3", displayName = "技能3" },
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
                new BuffEntry { buffId = BuffIds.SuperArmor, type = BuffType.SuperArmor, displayName = "全程霸体", description = "全程霸体，不会被打断" },
                new BuffEntry { buffId = BuffIds.SummonClone, type = BuffType.SummonClone, displayName = "召唤分身", description = "召唤分身协助战斗" },
                new BuffEntry { buffId = BuffIds.Afterimage, type = BuffType.Afterimage, displayName = "攻击重影", description = "攻击附带重影，额外伤害" },
                new BuffEntry { buffId = BuffIds.MeleeRangeExpand, type = BuffType.MeleeRangeExpand, displayName = "近战范围扩大", description = "武器碰撞体、伤害检测与特效范围均扩大100%", meleeRangeScale = 2f },
                new BuffEntry { buffId = BuffIds.Multishot, type = BuffType.Multishot, displayName = "多重射击", description = "远程每次多发射一发子弹，间隔0.2秒", multishotDelaySeconds = 0.2f },
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

        private static void CreateSharedSkillDatabase()
        {
            EnsureConfigFolder();
            var db = SharedSkillDatabaseDefaults.CreateRuntimeDefault();
            CreateOrMergeSharedSkillDatabaseAsset(db, $"{ResourcesConfigDir}/技能库.asset");
            AssetDatabase.SaveAssets();
        }

        private static void CreateSkillConfigDatabase()
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<SkillConfigDatabaseSO>();
            db.entries = new List<SkillConfigEntry>();

            db.entries.Add(new SkillConfigEntry { skillId = "Attack0",      displayName = "普攻1",     isPassive = false, entryGroup = PlayerSkillEntryGroup.BaseSkill,   mpCost = 0, animationTrigger = "Attack0" });
            db.entries.Add(new SkillConfigEntry { skillId = "Attack1",      displayName = "普攻2",     isPassive = false, entryGroup = PlayerSkillEntryGroup.BaseSkill,   mpCost = 0, animationTrigger = "Attack1" });
            db.entries.Add(new SkillConfigEntry { skillId = "Attack2",      displayName = "普攻3",     isPassive = false, entryGroup = PlayerSkillEntryGroup.BaseSkill,   mpCost = 0, animationTrigger = "Attack2" });
            db.entries.Add(new SkillConfigEntry { skillId = "Attack3",      displayName = "普攻4",     isPassive = false, entryGroup = PlayerSkillEntryGroup.BaseSkill,   mpCost = 0, animationTrigger = "Attack3" });
            db.entries.Add(new SkillConfigEntry { skillId = "AirAttack",    displayName = "空中普攻",   isPassive = false, entryGroup = PlayerSkillEntryGroup.BaseSkill,   mpCost = 0, animationTrigger = "AirAttack" });
            db.entries.Add(new SkillConfigEntry { skillId = "FallAttack",   displayName = "下落攻击",   isPassive = false, entryGroup = PlayerSkillEntryGroup.BaseSkill,   mpCost = 0, animationTrigger = "FallAttackLand" });
            db.entries.Add(new SkillConfigEntry { skillId = "ChargeAttack", displayName = "蓄力攻击",   isPassive = false, entryGroup = PlayerSkillEntryGroup.BaseSkill,   mpCost = 0, animationTrigger = "ChargeRelease" });

            for (int i = 0; i < 4; i++)
                db.entries.Add(new SkillConfigEntry
                {
                    skillId            = $"Skill{i}",
                    displayName        = $"主动{i + 1}",
                    isPassive          = false,
                    entryGroup         = PlayerSkillEntryGroup.ActiveSkill,
                    talentCost         = 2,
                    mpCost             = 10,
                    animationTrigger   = $"Skill{i}",
                    activeSlotIndex    = i,
                });
            for (int i = 1; i <= 12; i++)
                db.entries.Add(new SkillConfigEntry
                {
                    skillId     = $"passive_{i}",
                    displayName = $"被动{i}",
                    isPassive   = true,
                    entryGroup  = PlayerSkillEntryGroup.PassiveSkill,
                    talentCost  = 1,
                    mpCost      = 0,
                });
            CreateOrUpdateAsset(db, $"{ResourcesConfigDir}/玩家技能配置库.asset");
            AssetDatabase.SaveAssets();
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

        private static void DeleteLegacyAnimationFrameDamageDatabaseAsset()
        {
            string[] assetPaths =
            {
                $"{ResourcesConfigDir}/技能伤害数据配置库.asset",
                $"{ResourcesConfigDir}/动画帧伤害数据配置库.asset",
            };

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static void CreateEnemyArchetypeDatabase(Game.AI.BehaviorTreeAsset defaultBehaviorTree)
        {
            EnsureConfigFolder();
            var archetypes = new List<EnemyArchetypeSO>
            {
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "melee_minion",
                        "近战小怪",
                        EnemyType.MeleeMinion,
                        defaultBehaviorTree,
                        120f,
                        12f,
                        20f,
                        10f,
                        BuildSkillSlot(0, "melee_minion_chop",   castRange: 2.2f,  cooldown: 0.65f, castDuration: 0.72f)),
                    $"{ResourcesConfigDir}/敌人行为_近战小怪.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "ranged_minion",
                        "远程小怪",
                        EnemyType.RangedMinion,
                        defaultBehaviorTree,
                        130f,
                        14f,
                        22f,
                        10f,
                        BuildSkillSlot(0, "ranged_minion_shot",  castRange: 12f,   cooldown: 1.2f,  castDuration: 0.85f)),
                    $"{ResourcesConfigDir}/敌人行为_远程小怪.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "elite_1",
                        "精英1",
                        EnemyType.Elite,
                        defaultBehaviorTree,
                        145f,
                        16f,
                        28f,
                        12f,
                        BuildSkillSlot(0, "elite_1_combo",       castRange: 2.6f,  cooldown: 0.95f, castDuration: 1f),
                        BuildSkillSlot(1, "elite_1_slam",        castRange: 3f,    cooldown: 3.2f,  castDuration: 1.1f,  enterIdleAfterCast: true, postCastIdleDuration: 0.3f)),
                    $"{ResourcesConfigDir}/敌人行为_精英1.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "elite_2",
                        "精英2",
                        EnemyType.Elite,
                        defaultBehaviorTree,
                        150f,
                        17f,
                        30f,
                        12f,
                        BuildSkillSlot(0, "elite_2_burst_shot",  castRange: 13f,   cooldown: 1.6f,  castDuration: 1.2f),
                        BuildSkillSlot(1, "elite_2_hook_pull",   castRange: 4f,    cooldown: 3.6f,  castDuration: 1.1f)),
                    $"{ResourcesConfigDir}/敌人行为_精英2.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "guardian_1",
                        "守卫者1",
                        EnemyType.Guardian,
                        defaultBehaviorTree,
                        145f,
                        18f,
                        30f,
                        14f,
                        BuildSkillSlot(0, "guardian_1_crush",    castRange: 2.8f,  cooldown: 3.5f,  castDuration: 1.15f, enterIdleAfterCast: true,  postCastIdleDuration: 0.35f),
                        BuildSkillSlot(1, "guardian_1_fortify",  castRange: 1f,    cooldown: 5f,    castDuration: 1f,    rotateToTargetOnCast: false)),
                    $"{ResourcesConfigDir}/敌人行为_守卫者1.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "guardian_2",
                        "守卫者2",
                        EnemyType.Guardian,
                        defaultBehaviorTree,
                        150f,
                        20f,
                        32f,
                        14f,
                        BuildSkillSlot(0, "guardian_2_bash",     castRange: 3.2f,  cooldown: 3.4f,  castDuration: 1f,    enterIdleAfterCast: true,  postCastIdleDuration: 0.3f),
                        BuildSkillSlot(1, "guardian_2_chain_pull", castRange: 5f,  cooldown: 4.2f,  castDuration: 1.1f)),
                    $"{ResourcesConfigDir}/敌人行为_守卫者2.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_1",
                        "Boss1",
                        EnemyType.Boss,
                        defaultBehaviorTree,
                        160f,
                        22f,
                        40f,
                        16f,
                        BuildSkillSlot(0, "boss_1_slash",        castRange: 3f,    cooldown: 2.2f,  castDuration: 0.9f),
                        BuildSkillSlot(1, "boss_1_shot",         castRange: 14f,   cooldown: 2.8f,  castDuration: 1f),
                        BuildSkillSlot(2, "boss_1_charge",       castRange: 5f,    cooldown: 4f,    castDuration: 1f,    enterIdleAfterCast: true,  postCastIdleDuration: 0.45f, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly),
                        BuildSkillSlot(3, "boss_1_roar",         castRange: 8f,    cooldown: 5f,    castDuration: 1.2f,  enterIdleAfterCast: true,  postCastIdleDuration: 0.55f, rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly)),
                    $"{ResourcesConfigDir}/敌人行为_Boss1.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_2",
                        "Boss2",
                        EnemyType.Boss,
                        defaultBehaviorTree,
                        165f,
                        23f,
                        42f,
                        16f,
                        BuildSkillSlot(0, "boss_2_cross_slash",  castRange: 3.2f,  cooldown: 2.1f,  castDuration: 1f),
                        BuildSkillSlot(1, "boss_2_volley",       castRange: 15f,   cooldown: 2.7f,  castDuration: 1.2f),
                        BuildSkillSlot(2, "boss_2_crash",        castRange: 5.5f,  cooldown: 3.8f,  castDuration: 1.05f, enterIdleAfterCast: true,  postCastIdleDuration: 0.5f,  phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly),
                        BuildSkillSlot(3, "boss_2_rage",         castRange: 8f,    cooldown: 4.8f,  castDuration: 1.15f, enterIdleAfterCast: true,  postCastIdleDuration: 0.55f, rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly)),
                    $"{ResourcesConfigDir}/敌人行为_Boss2.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_3",
                        "Boss3",
                        EnemyType.Boss,
                        defaultBehaviorTree,
                        170f,
                        24f,
                        44f,
                        17f,
                        BuildSkillSlot(0, "boss_3_pull_cleave",  castRange: 4f,    cooldown: 2.4f,  castDuration: 1.05f),
                        BuildSkillSlot(1, "boss_3_barrage",      castRange: 15f,   cooldown: 2.9f,  castDuration: 1.25f),
                        BuildSkillSlot(2, "boss_3_lunge",        castRange: 5.8f,  cooldown: 3.9f,  castDuration: 1f,    enterIdleAfterCast: true,  postCastIdleDuration: 0.45f, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly),
                        BuildSkillSlot(3, "boss_3_drain",        castRange: 8.5f,  cooldown: 5.2f,  castDuration: 1.25f, enterIdleAfterCast: true,  postCastIdleDuration: 0.55f, rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly)),
                    $"{ResourcesConfigDir}/敌人行为_Boss3.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_4",
                        "Boss4",
                        EnemyType.Boss,
                        defaultBehaviorTree,
                        175f,
                        25f,
                        46f,
                        17f,
                        BuildSkillSlot(0, "boss_4_spin",         castRange: 3.5f,  cooldown: 2f,    castDuration: 1.15f),
                        BuildSkillSlot(1, "boss_4_piercing_shot", castRange: 15.5f, cooldown: 2.5f,  castDuration: 1f),
                        BuildSkillSlot(2, "boss_4_pursuit",      castRange: 6.2f,  cooldown: 3.5f,  castDuration: 0.95f, enterIdleAfterCast: true,  postCastIdleDuration: 0.5f,  phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly),
                        BuildSkillSlot(3, "boss_4_battlecry",    castRange: 8.8f,  cooldown: 4.4f,  castDuration: 1.15f, enterIdleAfterCast: true,  postCastIdleDuration: 0.55f, rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly)),
                    $"{ResourcesConfigDir}/敌人行为_Boss4.asset"),
                CreateOrUpdateAsset(
                    BuildEnemyArchetype(
                        "boss_5",
                        "Boss5",
                        EnemyType.Boss,
                        defaultBehaviorTree,
                        180f,
                        26f,
                        48f,
                        18f,
                        BuildSkillSlot(0, "boss_5_cleave",       castRange: 3.6f,  cooldown: 1.9f,  castDuration: 1f),
                        BuildSkillSlot(1, "boss_5_storm",        castRange: 16f,   cooldown: 2.3f,  castDuration: 1.3f),
                        BuildSkillSlot(2, "boss_5_stampede",     castRange: 6.5f,  cooldown: 3.3f,  castDuration: 1.05f, enterIdleAfterCast: true,  postCastIdleDuration: 0.6f,  phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly),
                        BuildSkillSlot(3, "boss_5_regen_roar",   castRange: 9f,    cooldown: 4.2f,  castDuration: 1.25f, enterIdleAfterCast: true,  postCastIdleDuration: 0.6f,  rotateToTargetOnCast: false, phaseAvailability: EnemySkillPhaseAvailability.LowHpOnly)),
                    $"{ResourcesConfigDir}/敌人行为_Boss5.asset"),
            };

            NormalizeSkillSlotOverrides(archetypes);

            string legacyDatabasePath = $"{ResourcesConfigDir}/敌人行为配置库.asset";
            if (AssetDatabase.LoadMainAssetAtPath(legacyDatabasePath) != null)
                AssetDatabase.DeleteAsset(legacyDatabasePath);
            AssetDatabase.SaveAssets();
        }

        private static void CreateItemDisplayConfig()
        {
            EnsureConfigFolder();
            var db = ScriptableObject.CreateInstance<ItemDisplayDatabaseSO>();
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
