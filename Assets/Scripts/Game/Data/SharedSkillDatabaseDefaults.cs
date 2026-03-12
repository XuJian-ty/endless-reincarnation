using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public static class SharedSkillDatabaseDefaults
    {
        private sealed class DefaultEventSpec
        {
            public string eventId = "";
            public float startTime = 0f;
            public SkillEventTriggerMode triggerMode = SkillEventTriggerMode.Once;
            public float activeDuration = 0f;
            public float repeatInterval = 0.1f;
            public SkillDamageEffect[] damageEffects;
            public SkillPhysicsEffect[] physicsEffects;
            public SkillAttributeEffect[] attributeEffects;
            public SkillVfxEffect[] vfxEffects;
            public SkillSfxEffect[] sfxEffects;
        }

        public static SharedSkillDatabaseSO CreateRuntimeDefault()
        {
            var db = ScriptableObject.CreateInstance<SharedSkillDatabaseSO>();
            db.groups = CreateDefaultGroups();
            db.entries = FlattenEntries(db.groups);
            return db;
        }

        public static List<SharedSkillDefinition> CreateDefaultEntries()
        {
            List<SkillGroupDefinition> clonedGroups = TryCloneExistingGroups();
            if (clonedGroups != null)
                return FlattenEntries(clonedGroups);

            return new List<SharedSkillDefinition>
            {
                PlayerSkill("Skill0", "主动技能0"),
                PlayerSkill("Skill1", "主动技能1"),
                PlayerSkill("Skill2", "主动技能2"),
                PlayerSkill("Skill3", "主动技能3"),
                CreatePlayerAttack0(),
                CreatePlayerAttack1(),
                CreatePlayerAttack2(),
                CreatePlayerAttack3(),
                CreatePlayerAirAttack(),
                CreatePlayerFallAttack(),
                CreatePlayerChargeAttack(),
                CreateMeleeMinionChop(),
                CreateRangedMinionShot(),
                CreateElite1Combo(),
                CreateElite1Slam(),
                CreateElite2BurstShot(),
                CreateElite2HookPull(),
                CreateGuardian1Crush(),
                CreateGuardian1Fortify(),
                CreateGuardian2Bash(),
                CreateGuardian2ChainPull(),
                CreateBoss1Slash(),
                CreateBoss1Shot(),
                CreateBoss1Charge(),
                CreateBoss1Roar(),
                CreateBoss2CrossSlash(),
                CreateBoss2Volley(),
                CreateBoss2Crash(),
                CreateBoss2Rage(),
                CreateBoss3PullCleave(),
                CreateBoss3Barrage(),
                CreateBoss3Lunge(),
                CreateBoss3Drain(),
                CreateBoss4Spin(),
                CreateBoss4PiercingShot(),
                CreateBoss4Pursuit(),
                CreateBoss4Battlecry(),
                CreateBoss5Cleave(),
                CreateBoss5Storm(),
                CreateBoss5Stampede(),
                CreateBoss5RegenRoar(),
            };
        }

        public static List<SkillGroupDefinition> CreateDefaultGroups()
        {
            List<SkillGroupDefinition> clonedGroups = TryCloneExistingGroups();
            if (clonedGroups != null)
                return clonedGroups;

            var groups = new List<SkillGroupDefinition>
            {
                CreateGroup("player", "玩家"),
                CreateGroup("melee_minion", "近战小怪"),
                CreateGroup("ranged_minion", "远程小怪"),
                CreateGroup("elite_1", "精英1"),
                CreateGroup("elite_2", "精英2"),
                CreateGroup("guardian_1", "守卫者1"),
                CreateGroup("guardian_2", "守卫者2"),
                CreateGroup("boss_1", "Boss1"),
                CreateGroup("boss_2", "Boss2"),
                CreateGroup("boss_3", "Boss3"),
                CreateGroup("boss_4", "Boss4"),
                CreateGroup("boss_5", "Boss5"),
            };

            var entries = CreateDefaultEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                var skill = entries[i];
                if (skill == null)
                    continue;

                SkillGroupDefinition group = groups.Find(item => item.groupId == ResolveGroupId(skill.skillId));
                if (group == null)
                    group = groups[0];

                group.entries.Add(skill);
            }

            return groups;
        }

        private static List<SkillGroupDefinition> TryCloneExistingGroups()
        {
            SharedSkillDatabaseSO existing = Resources.Load<SharedSkillDatabaseSO>("配置/技能库");
            if (existing == null)
                existing = Resources.Load<SharedSkillDatabaseSO>("配置/共享技能库");
            if (existing?.groups == null || existing.groups.Count == 0)
                return null;

            return CloneGroups(existing.groups);
        }

        private static List<SkillGroupDefinition> CloneGroups(List<SkillGroupDefinition> groups)
        {
            var result = new List<SkillGroupDefinition>();
            if (groups == null)
                return result;

            for (int i = 0; i < groups.Count; i++)
            {
                SkillGroupDefinition group = groups[i];
                if (group == null)
                    continue;

                var clonedGroup = new SkillGroupDefinition
                {
                    groupId = group.groupId,
                    groupName = group.groupName,
                    entries = new List<SharedSkillDefinition>(),
                };
                var seenSkillIds = new HashSet<string>();

                if (group.entries != null)
                {
                    for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
                    {
                        SharedSkillDefinition entry = group.entries[entryIndex];
                        if (entry == null)
                            continue;
                        if (!string.IsNullOrWhiteSpace(entry.skillId) && !seenSkillIds.Add(entry.skillId))
                            continue;

                        clonedGroup.entries.Add(Clone(entry));
                    }
                }

                result.Add(clonedGroup);
            }

            return result;
        }

        private static T Clone<T>(T source)
        {
            if (source == null)
                return default;

            return JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
        }

        private static SkillGroupDefinition CreateGroup(string groupId, string groupName)
        {
            return new SkillGroupDefinition
            {
                groupId = groupId,
                groupName = groupName,
                entries = new List<SharedSkillDefinition>(),
            };
        }

        private static string ResolveGroupId(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return "player";

            if (skillId.StartsWith("Skill", StringComparison.Ordinal) ||
                skillId.StartsWith("Attack", StringComparison.Ordinal) ||
                string.Equals(skillId, "AirAttack", StringComparison.Ordinal) ||
                string.Equals(skillId, "FallAttack", StringComparison.Ordinal) ||
                string.Equals(skillId, "ChargeAttack", StringComparison.Ordinal))
            {
                return "player";
            }
            if (skillId.StartsWith("melee_minion_"))
                return "melee_minion";
            if (skillId.StartsWith("ranged_minion_"))
                return "ranged_minion";
            if (skillId.StartsWith("elite_1_"))
                return "elite_1";
            if (skillId.StartsWith("elite_2_"))
                return "elite_2";
            if (skillId.StartsWith("guardian_1_"))
                return "guardian_1";
            if (skillId.StartsWith("guardian_2_"))
                return "guardian_2";
            if (skillId.StartsWith("boss_1_"))
                return "boss_1";
            if (skillId.StartsWith("boss_2_"))
                return "boss_2";
            if (skillId.StartsWith("boss_3_"))
                return "boss_3";
            if (skillId.StartsWith("boss_4_"))
                return "boss_4";
            if (skillId.StartsWith("boss_5_"))
                return "boss_5";
            return "player";
        }

        private static List<SharedSkillDefinition> FlattenEntries(List<SkillGroupDefinition> groups)
        {
            var result = new List<SharedSkillDefinition>();
            if (groups == null)
                return result;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group?.entries == null)
                    continue;

                for (int i = 0; i < group.entries.Count; i++)
                {
                    var entry = group.entries[i];
                    if (entry != null)
                        result.Add(entry);
                }
            }

            return result;
        }

        private static SharedSkillDefinition PlayerSkill(string skillId, string displayName)
        {
            return new SharedSkillDefinition
            {
                skillId = skillId,
                displayName = displayName,
                ignoreAnimationDamageEvents = false,
                damageEvents = new List<SkillDamageEvent>(),
                physicsEvents = new List<SkillPhysicsEvent>(),
                attributeEvents = new List<SkillAttributeEvent>(),
                vfxEvents = new List<SkillVfxEvent>(),
                sfxEvents = new List<SkillSfxEvent>(),
            };
        }

        private static SharedSkillDefinition Skill(string skillId, string displayName, params DefaultEventSpec[] events)
        {
            var definition = new SharedSkillDefinition
            {
                skillId = skillId,
                displayName = displayName,
                ignoreAnimationDamageEvents = true,
                damageEvents = new List<SkillDamageEvent>(),
                physicsEvents = new List<SkillPhysicsEvent>(),
                attributeEvents = new List<SkillAttributeEvent>(),
                vfxEvents = new List<SkillVfxEvent>(),
                sfxEvents = new List<SkillSfxEvent>(),
            };
            ApplySpecs(definition, events);
            return definition;
        }

        private static DefaultEventSpec Event(
            string eventId,
            float startTime,
            SkillDamageEffect[] damageEffects = null,
            SkillPhysicsEffect[] physicsEffects = null,
            SkillAttributeEffect[] attributeEffects = null,
            SkillVfxEffect[] vfxEffects = null,
            SkillSfxEffect[] sfxEffects = null)
        {
            return new DefaultEventSpec
            {
                eventId = eventId,
                startTime = startTime,
                damageEffects = damageEffects,
                physicsEffects = physicsEffects,
                attributeEffects = attributeEffects,
                vfxEffects = vfxEffects,
                sfxEffects = sfxEffects,
            };
        }

        private static SharedSkillDefinition PlayerAttackSkill(string skillId, string displayName, params DefaultEventSpec[] events)
        {
            var definition = new SharedSkillDefinition
            {
                skillId = skillId,
                displayName = displayName,
                ignoreAnimationDamageEvents = false,
                damageEvents = new List<SkillDamageEvent>(),
                physicsEvents = new List<SkillPhysicsEvent>(),
                attributeEvents = new List<SkillAttributeEvent>(),
                vfxEvents = new List<SkillVfxEvent>(),
                sfxEvents = new List<SkillSfxEvent>(),
            };
            ApplySpecs(definition, events);
            return definition;
        }

        private static DefaultEventSpec RepeatedEvent(
            string eventId,
            float startTime,
            float activeDuration,
            float repeatInterval,
            SkillDamageEffect[] damageEffects = null,
            SkillPhysicsEffect[] physicsEffects = null,
            SkillAttributeEffect[] attributeEffects = null,
            SkillVfxEffect[] vfxEffects = null,
            SkillSfxEffect[] sfxEffects = null)
        {
            return new DefaultEventSpec
            {
                eventId = eventId,
                startTime = startTime,
                triggerMode = SkillEventTriggerMode.Repeated,
                activeDuration = activeDuration,
                repeatInterval = repeatInterval,
                damageEffects = damageEffects,
                physicsEffects = physicsEffects,
                attributeEffects = attributeEffects,
                vfxEffects = vfxEffects,
                sfxEffects = sfxEffects,
            };
        }

        private static void ApplySpecs(SharedSkillDefinition definition, DefaultEventSpec[] events)
        {
            if (definition == null || events == null)
                return;

            for (int i = 0; i < events.Length; i++)
            {
                DefaultEventSpec spec = events[i];
                if (spec == null)
                    continue;

                if (spec.damageEffects != null && spec.damageEffects.Length > 0)
                {
                    definition.damageEvents.Add(new SkillDamageEvent
                    {
                        eventId = spec.eventId,
                        startTime = spec.startTime,
                        triggerMode = spec.triggerMode,
                        activeDuration = spec.activeDuration,
                        repeatInterval = spec.repeatInterval,
                        damageEffects = new List<SkillDamageEffect>(spec.damageEffects),
                    });
                }

                if (spec.physicsEffects != null && spec.physicsEffects.Length > 0)
                {
                    definition.physicsEvents.Add(new SkillPhysicsEvent
                    {
                        eventId = spec.eventId,
                        startTime = spec.startTime,
                        triggerMode = spec.triggerMode,
                        activeDuration = spec.activeDuration,
                        repeatInterval = spec.repeatInterval,
                        physicsEffects = new List<SkillPhysicsEffect>(spec.physicsEffects),
                    });
                }

                if (spec.attributeEffects != null && spec.attributeEffects.Length > 0)
                {
                    definition.attributeEvents.Add(new SkillAttributeEvent
                    {
                        eventId = spec.eventId,
                        startTime = spec.startTime,
                        triggerMode = spec.triggerMode,
                        activeDuration = spec.activeDuration,
                        repeatInterval = spec.repeatInterval,
                        attributeEffects = new List<SkillAttributeEffect>(spec.attributeEffects),
                    });
                }

                if (spec.vfxEffects != null && spec.vfxEffects.Length > 0)
                {
                    definition.vfxEvents.Add(new SkillVfxEvent
                    {
                        eventId = spec.eventId,
                        startTime = spec.startTime,
                        triggerMode = spec.triggerMode,
                        activeDuration = spec.activeDuration,
                        repeatInterval = spec.repeatInterval,
                        vfxEffects = new List<SkillVfxEffect>(spec.vfxEffects),
                    });
                }

                if (spec.sfxEffects != null && spec.sfxEffects.Length > 0)
                {
                    definition.sfxEvents.Add(new SkillSfxEvent
                    {
                        eventId = spec.eventId,
                        startTime = spec.startTime,
                        triggerMode = spec.triggerMode,
                        activeDuration = spec.activeDuration,
                        repeatInterval = spec.repeatInterval,
                        sfxEffects = new List<SkillSfxEffect>(spec.sfxEffects),
                    });
                }
            }
        }

        private static SkillDamageEffect Damage(string damageName, float magnitude, params SkillPhysicsEffect[] onHitPhysicsEffects)
        {
            SkillDamageEffect effect = damageName switch
            {
                "Attack0" => SphereDamage("Enemy", magnitude * 1f, 20f, 0.04f, 1.5f),
                "Attack1" => SphereDamage("Enemy", magnitude * 1f, 20f, 0.04f, 1.5f),
                "Attack2" => SphereDamage("Enemy", magnitude * 1f, 20f, 0.04f, 1.5f),
                "Attack3" => SphereDamage("Enemy", magnitude * 1.2f, 20f, 0.04f, 1.5f),
                "AirAttack" => SphereDamage("Enemy", magnitude * 1f, 15f, 0.04f, 1.5f),
                "FallAttack" => SphereDamage("Enemy", magnitude * 1.5f, 25f, 0.05f, 2f),
                "ChargeAttack" => SectorDamage("Enemy", magnitude * 2f, 30f, 0.06f, 2f, 180f),
                "EnemyMelee" => SphereDamage("Player", magnitude * 1f, 10f, 0.04f, 1.6f),
                "EnemyRanged" => SphereDamage("Player", magnitude * 1f, 8f, 0.04f, 1.6f),
                "EnemyMeleeLight" => SphereDamage("Player", magnitude * 0.95f, 8f, 0.05f, 1.45f),
                "EnemyMeleeArc" => SectorDamage("Player", magnitude * 1.05f, 10f, 0.06f, 2.4f, 110f),
                "EnemyMeleeHeavy" => BoxDamage("Player", magnitude * 1.25f, 18f, 0.1f, new Vector3(0f, 0f, 1.4f), new Vector3(2.2f, 1.8f, 3.2f)),
                "EnemyMeleeSpin" => SphereDamage("Player", magnitude * 0.8f, 10f, 0.05f, 2.6f),
                "EnemyPullWave" => SectorDamage("Player", magnitude * 1f, 0f, 0.05f, 4.2f, 80f),
                "EnemyChargeImpact" => BoxDamage("Player", magnitude * 1.4f, 18f, 0.12f, new Vector3(0f, 0f, 2f), new Vector3(2.4f, 1.8f, 4.4f)),
                "EnemyBossCleave" => SectorDamage("Player", magnitude * 1.6f, 16f, 0.08f, 3.4f, 140f),
                "EnemyBossRoar" => SphereDamage("Player", magnitude * 1f, 6f, 0.05f, 4.5f),
                "EnemyRangedShot" => RayDamage("Player", magnitude * 1f, 6f, 0.05f, 14f, new Vector3(0f, 1f, 0f)),
                "EnemyRangedBurst" => RayDamage("Player", magnitude * 0.85f, 4f, 0.04f, 16f, new Vector3(0f, 1f, 0f)),
                "EnemyRangedHeavy" => RayDamage("Player", magnitude * 1.35f, 10f, 0.07f, 20f, new Vector3(0f, 1f, 0f)),
                _ => SphereDamage("Enemy", magnitude, 20f, 0.04f, 1.5f),
            };

            if (onHitPhysicsEffects != null && onHitPhysicsEffects.Length > 0)
                effect.onHitPhysicsEffects = new List<SkillPhysicsEffect>(onHitPhysicsEffects);

            return effect;
        }

        private static SkillDamageEffect SphereDamage(string hitLayerName, float magnitude, float pushForce, float pushDuration, float sphereRadius)
        {
            return new SkillDamageEffect
            {
                damageMagnitude = magnitude,
                detectionType = DamageDetectionType.RangeOverlap,
                hitLayerName = hitLayerName,
                pushForce = pushForce,
                pushDuration = pushDuration,
                shape = AttackShapeType.Sphere,
                sphereRadius = sphereRadius,
            };
        }

        private static SkillDamageEffect SectorDamage(string hitLayerName, float magnitude, float pushForce, float pushDuration, float sphereRadius, float sectorAngle)
        {
            return new SkillDamageEffect
            {
                damageMagnitude = magnitude,
                detectionType = DamageDetectionType.RangeOverlap,
                hitLayerName = hitLayerName,
                pushForce = pushForce,
                pushDuration = pushDuration,
                shape = AttackShapeType.Sector,
                sphereRadius = sphereRadius,
                sectorAngle = sectorAngle,
            };
        }

        private static SkillDamageEffect BoxDamage(string hitLayerName, float magnitude, float pushForce, float pushDuration, Vector3 centerOffset, Vector3 boxSize)
        {
            return new SkillDamageEffect
            {
                damageMagnitude = magnitude,
                detectionType = DamageDetectionType.RangeOverlap,
                hitLayerName = hitLayerName,
                pushForce = pushForce,
                pushDuration = pushDuration,
                shape = AttackShapeType.Box,
                centerOffset = centerOffset,
                boxSize = boxSize,
            };
        }

        private static SkillDamageEffect RayDamage(string hitLayerName, float magnitude, float pushForce, float pushDuration, float rayMaxDistance, Vector3 rayOriginOffset)
        {
            return new SkillDamageEffect
            {
                damageMagnitude = magnitude,
                detectionType = DamageDetectionType.Raycast,
                hitLayerName = hitLayerName,
                pushForce = pushForce,
                pushDuration = pushDuration,
                rayOriginOffset = rayOriginOffset,
                rayMaxDistance = rayMaxDistance,
            };
        }

        private static SkillPhysicsEffect Knockback(float distance, float duration)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.Knockback,
                distance = distance,
                duration = duration,
            };
        }

        private static SkillPhysicsEffect Pull(float distance, float duration)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.Pull,
                distance = distance,
                duration = duration,
            };
        }

        private static SkillPhysicsEffect SelfDash(float distance, float duration)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.DashSelf,
                distance = distance,
                duration = duration,
            };
        }

        private static SkillPhysicsEffect Launch(float distance, float height, float duration)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.Launch,
                distance = distance,
                height = height,
                duration = duration,
            };
        }

        private static SkillPhysicsEffect Airborne(float height, float duration)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.Airborne,
                height = height,
                duration = duration,
            };
        }

        private static SkillPhysicsEffect Stun(float duration)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.Stun,
                duration = duration,
            };
        }

        private static SkillAttributeEffect HealSelf(float magnitude, bool usePercent = false)
        {
            return new SkillAttributeEffect
            {
                statField = SkillStatField.HP,
                targetMode = SkillTargetMode.Self,
                magnitude = magnitude,
                usePercent = usePercent,
            };
        }

        private static SkillAttributeEffect BuffSelf(SkillStatField statField, float magnitude, float duration)
        {
            return new SkillAttributeEffect
            {
                statField = statField,
                targetMode = SkillTargetMode.Self,
                magnitude = magnitude,
                duration = duration,
            };
        }

        private static SharedSkillDefinition CreatePlayerAttack0()
        {
            return PlayerAttackSkill(
                "Attack0",
                "玩家普攻1",
                Event("hit", 0f, damageEffects: new[] { Damage("Attack0", 1f) }));
        }

        private static SharedSkillDefinition CreatePlayerAttack1()
        {
            return PlayerAttackSkill(
                "Attack1",
                "玩家普攻2",
                Event("hit", 0f, damageEffects: new[] { Damage("Attack1", 1f) }));
        }

        private static SharedSkillDefinition CreatePlayerAttack2()
        {
            return PlayerAttackSkill(
                "Attack2",
                "玩家普攻3",
                Event("hit", 0f, damageEffects: new[] { Damage("Attack2", 1f) }));
        }

        private static SharedSkillDefinition CreatePlayerAttack3()
        {
            return PlayerAttackSkill(
                "Attack3",
                "玩家普攻4",
                Event("hit", 0f, damageEffects: new[] { Damage("Attack3", 1f) }));
        }

        private static SharedSkillDefinition CreatePlayerAirAttack()
        {
            return PlayerAttackSkill(
                "AirAttack",
                "玩家空中攻击",
                Event("hit", 0f, damageEffects: new[] { Damage("AirAttack", 1f) }));
        }

        private static SharedSkillDefinition CreatePlayerFallAttack()
        {
            return PlayerAttackSkill(
                "FallAttack",
                "玩家下落攻击",
                Event("hit", 0f, damageEffects: new[] { Damage("FallAttack", 1f) }));
        }

        private static SharedSkillDefinition CreatePlayerChargeAttack()
        {
            return PlayerAttackSkill(
                "ChargeAttack",
                "玩家蓄力攻击",
                Event("hit", 0f, damageEffects: new[] { Damage("ChargeAttack", 1f) }));
        }

        private static SharedSkillDefinition CreateMeleeMinionChop()
        {
            return Skill(
                "melee_minion_chop",
                "Melee Minion Chop",
                Event("hit", 0.24f,
                    damageEffects: new[] { Damage("EnemyMeleeLight", 1f, Knockback(0.8f, 0.08f)) }));
        }

        private static SharedSkillDefinition CreateRangedMinionShot()
        {
            return Skill(
                "ranged_minion_shot",
                "Ranged Minion Shot",
                Event("shot", 0.28f,
                    damageEffects: new[] { Damage("EnemyRangedShot", 1f) }));
        }

        private static SharedSkillDefinition CreateElite1Combo()
        {
            return Skill(
                "elite_1_combo",
                "Elite 1 Combo",
                Event("hit_a", 0.2f,
                    damageEffects: new[] { Damage("EnemyMeleeArc", 0.85f) }),
                Event("hit_b", 0.45f,
                    damageEffects: new[] { Damage("EnemyMeleeArc", 1f, Knockback(1f, 0.1f)) }));
        }

        private static SharedSkillDefinition CreateElite1Slam()
        {
            return Skill(
                "elite_1_slam",
                "Elite 1 Slam",
                Event("slam", 0.48f,
                    damageEffects: new[] { Damage("EnemyMeleeHeavy", 1.35f, Knockback(1.8f, 0.14f)) }));
        }

        private static SharedSkillDefinition CreateElite2BurstShot()
        {
            return Skill(
                "elite_2_burst_shot",
                "Elite 2 Burst Shot",
                RepeatedEvent("burst", 0.2f, 0.36f, 0.18f,
                    damageEffects: new[] { Damage("EnemyRangedBurst", 0.65f) }));
        }

        private static SharedSkillDefinition CreateElite2HookPull()
        {
            return Skill(
                "elite_2_hook_pull",
                "Elite 2 Hook Pull",
                Event("hook", 0.35f,
                    damageEffects: new[] { Damage("EnemyPullWave", 0.8f, Pull(2.2f, 0.18f)) }));
        }

        private static SharedSkillDefinition CreateGuardian1Crush()
        {
            return Skill(
                "guardian_1_crush",
                "Guardian 1 Crush",
                Event("crush", 0.5f,
                    damageEffects: new[] { Damage("EnemyMeleeHeavy", 1.5f, Knockback(2f, 0.18f)) }));
        }

        private static SharedSkillDefinition CreateGuardian1Fortify()
        {
            return Skill(
                "guardian_1_fortify",
                "Guardian 1 Fortify",
                Event("fortify", 0.15f,
                    attributeEffects: new[]
                    {
                        BuffSelf(SkillStatField.Attack, 4f, 4f),
                        BuffSelf(SkillStatField.Defense, 8f, 4f),
                    }));
        }

        private static SharedSkillDefinition CreateGuardian2Bash()
        {
            return Skill(
                "guardian_2_bash",
                "Guardian 2 Bash",
                Event("step", 0.05f,
                    physicsEffects: new[] { SelfDash(2.8f, 0.18f) }),
                Event("impact", 0.28f,
                    damageEffects: new[] { Damage("EnemyChargeImpact", 1.45f, Knockback(2.1f, 0.14f)) }));
        }

        private static SharedSkillDefinition CreateGuardian2ChainPull()
        {
            return Skill(
                "guardian_2_chain_pull",
                "Guardian 2 Chain Pull",
                Event("chain", 0.32f,
                    damageEffects: new[] { Damage("EnemyPullWave", 0.95f, Pull(2.8f, 0.2f)) }));
        }

        private static SharedSkillDefinition CreateBoss1Slash()
        {
            return Skill(
                "boss_1_slash",
                "Boss 1 Slash",
                Event("slash", 0.34f,
                    damageEffects: new[] { Damage("EnemyBossCleave", 1.45f, Knockback(1.2f, 0.1f)) }));
        }

        private static SharedSkillDefinition CreateBoss1Shot()
        {
            return Skill(
                "boss_1_shot",
                "Boss 1 Shot",
                Event("shot", 0.28f,
                    damageEffects: new[] { Damage("EnemyRangedHeavy", 1.15f) }));
        }

        private static SharedSkillDefinition CreateBoss1Charge()
        {
            return Skill(
                "boss_1_charge",
                "Boss 1 Charge",
                Event("dash", 0.05f,
                    physicsEffects: new[] { SelfDash(3.2f, 0.2f) }),
                Event("impact", 0.3f,
                    damageEffects: new[] { Damage("EnemyChargeImpact", 1.8f, Knockback(2.2f, 0.16f)) }));
        }

        private static SharedSkillDefinition CreateBoss1Roar()
        {
            return Skill(
                "boss_1_roar",
                "Boss 1 Roar",
                Event("buff", 0.2f,
                    attributeEffects: new[]
                    {
                        BuffSelf(SkillStatField.Attack, 6f, 3.5f),
                        BuffSelf(SkillStatField.Defense, 4f, 3.5f),
                        BuffSelf(SkillStatField.MoveSpeed, 0.2f, 3.5f),
                    }));
        }

        private static SharedSkillDefinition CreateBoss2CrossSlash()
        {
            return Skill(
                "boss_2_cross_slash",
                "Boss 2 Cross Slash",
                Event("cut_a", 0.22f,
                    damageEffects: new[] { Damage("EnemyMeleeArc", 0.9f) }),
                Event("cut_b", 0.48f,
                    damageEffects: new[] { Damage("EnemyBossCleave", 1.1f, Knockback(1.6f, 0.1f)) }));
        }

        private static SharedSkillDefinition CreateBoss2Volley()
        {
            return Skill(
                "boss_2_volley",
                "Boss 2 Volley",
                RepeatedEvent("volley", 0.22f, 0.45f, 0.15f,
                    damageEffects: new[] { Damage("EnemyRangedBurst", 0.72f) }));
        }

        private static SharedSkillDefinition CreateBoss2Crash()
        {
            return Skill(
                "boss_2_crash",
                "Boss 2 Crash",
                Event("dash", 0.05f,
                    physicsEffects: new[] { SelfDash(3.8f, 0.2f) }),
                Event("crash", 0.32f,
                    damageEffects: new[] { Damage("EnemyChargeImpact", 2f, Knockback(2.4f, 0.18f)) }));
        }

        private static SharedSkillDefinition CreateBoss2Rage()
        {
            return Skill(
                "boss_2_rage",
                "Boss 2 Rage",
                Event("rage", 0.18f,
                    attributeEffects: new[]
                    {
                        BuffSelf(SkillStatField.Attack, 8f, 4f),
                        BuffSelf(SkillStatField.MoveSpeed, 0.35f, 4f),
                    }));
        }

        private static SharedSkillDefinition CreateBoss3PullCleave()
        {
            return Skill(
                "boss_3_pull_cleave",
                "Boss 3 Pull Cleave",
                Event("pull", 0.14f,
                    damageEffects: new[] { Damage("EnemyPullWave", 0f, Pull(2.5f, 0.18f)) }),
                Event("cleave", 0.46f,
                    damageEffects: new[] { Damage("EnemyBossCleave", 1.35f, Knockback(1.4f, 0.1f)) }));
        }

        private static SharedSkillDefinition CreateBoss3Barrage()
        {
            return Skill(
                "boss_3_barrage",
                "Boss 3 Barrage",
                RepeatedEvent("barrage", 0.25f, 0.6f, 0.15f,
                    damageEffects: new[] { Damage("EnemyRangedBurst", 0.68f) }));
        }

        private static SharedSkillDefinition CreateBoss3Lunge()
        {
            return Skill(
                "boss_3_lunge",
                "Boss 3 Lunge",
                Event("lunge", 0.05f,
                    physicsEffects: new[] { SelfDash(4.1f, 0.22f) }),
                Event("impact", 0.3f,
                    damageEffects: new[] { Damage("EnemyChargeImpact", 1.75f, Knockback(2f, 0.16f)) }));
        }

        private static SharedSkillDefinition CreateBoss3Drain()
        {
            return Skill(
                "boss_3_drain",
                "Boss 3 Drain",
                Event("drain_hit", 0.28f,
                    damageEffects: new[] { Damage("EnemyBossRoar", 0.75f, Pull(1.6f, 0.12f)) }),
                Event("heal", 0.45f,
                    attributeEffects: new[]
                    {
                        HealSelf(0.08f, usePercent: true),
                        BuffSelf(SkillStatField.Defense, 5f, 3f),
                    }));
        }

        private static SharedSkillDefinition CreateBoss4Spin()
        {
            return Skill(
                "boss_4_spin",
                "Boss 4 Spin",
                RepeatedEvent("spin", 0.2f, 0.5f, 0.16f,
                    damageEffects: new[] { Damage("EnemyMeleeSpin", 0.7f, Knockback(0.7f, 0.08f)) }));
        }

        private static SharedSkillDefinition CreateBoss4PiercingShot()
        {
            return Skill(
                "boss_4_piercing_shot",
                "Boss 4 Piercing Shot",
                Event("pierce", 0.28f,
                    damageEffects: new[] { Damage("EnemyRangedHeavy", 1.4f, Knockback(1.8f, 0.12f)) }));
        }

        private static SharedSkillDefinition CreateBoss4Pursuit()
        {
            return Skill(
                "boss_4_pursuit",
                "Boss 4 Pursuit",
                Event("rush", 0.04f,
                    physicsEffects: new[] { SelfDash(4.6f, 0.22f) }),
                Event("finish", 0.26f,
                    damageEffects: new[] { Damage("EnemyChargeImpact", 1.95f, Knockback(2.3f, 0.18f)) }));
        }

        private static SharedSkillDefinition CreateBoss4Battlecry()
        {
            return Skill(
                "boss_4_battlecry",
                "Boss 4 Battlecry",
                Event("battlecry", 0.18f,
                    attributeEffects: new[]
                    {
                        BuffSelf(SkillStatField.Attack, 6f, 4f),
                        BuffSelf(SkillStatField.MoveSpeed, 0.45f, 4f),
                        BuffSelf(SkillStatField.HPRegen, 1.2f, 4f),
                    }));
        }

        private static SharedSkillDefinition CreateBoss5Cleave()
        {
            return Skill(
                "boss_5_cleave",
                "Boss 5 Cleave",
                Event("cleave", 0.38f,
                    damageEffects: new[] { Damage("EnemyBossCleave", 1.7f, Knockback(2f, 0.12f)) }));
        }

        private static SharedSkillDefinition CreateBoss5Storm()
        {
            return Skill(
                "boss_5_storm",
                "Boss 5 Storm",
                RepeatedEvent("storm", 0.2f, 0.75f, 0.15f,
                    damageEffects: new[] { Damage("EnemyRangedBurst", 0.75f) }));
        }

        private static SharedSkillDefinition CreateBoss5Stampede()
        {
            return Skill(
                "boss_5_stampede",
                "Boss 5 Stampede",
                Event("charge", 0.04f,
                    physicsEffects: new[] { SelfDash(5f, 0.24f) }),
                Event("stomp", 0.3f,
                    damageEffects: new[] { Damage("EnemyChargeImpact", 2.1f, Knockback(2.6f, 0.2f)) }));
        }

        private static SharedSkillDefinition CreateBoss5RegenRoar()
        {
            return Skill(
                "boss_5_regen_roar",
                "Boss 5 Regen Roar",
                Event("roar_hit", 0.25f,
                    damageEffects: new[] { Damage("EnemyBossRoar", 0.85f, Pull(1.2f, 0.1f)) }),
                Event("regen", 0.42f,
                    attributeEffects: new[]
                    {
                        HealSelf(0.12f, usePercent: true),
                        BuffSelf(SkillStatField.Attack, 5f, 4.5f),
                        BuffSelf(SkillStatField.Defense, 6f, 4.5f),
                    }));
        }
    }
}
