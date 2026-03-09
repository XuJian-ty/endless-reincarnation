using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public static class SharedSkillDatabaseDefaults
    {
        public static SharedSkillDatabaseSO CreateRuntimeDefault()
        {
            var db = ScriptableObject.CreateInstance<SharedSkillDatabaseSO>();
            db.entries = CreateDefaultEntries();
            return db;
        }

        public static List<SharedSkillDefinition> CreateDefaultEntries()
        {
            return new List<SharedSkillDefinition>
            {
                PlayerSkill("player_skill_0", "主动技能0"),
                PlayerSkill("player_skill_1", "主动技能1"),
                PlayerSkill("player_skill_2", "主动技能2"),
                PlayerSkill("player_skill_3", "主动技能3"),
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

        private static SharedSkillDefinition PlayerSkill(string skillId, string displayName)
        {
            return new SharedSkillDefinition
            {
                skillId = skillId,
                displayName = displayName,
                ignoreAnimationDamageEvents = false,
                events = new List<SkillTimelineEvent>(),
            };
        }

        private static SharedSkillDefinition Skill(string skillId, string displayName, params SkillTimelineEvent[] events)
        {
            return new SharedSkillDefinition
            {
                skillId = skillId,
                displayName = displayName,
                ignoreAnimationDamageEvents = true,
                events = new List<SkillTimelineEvent>(events),
            };
        }

        private static SkillTimelineEvent Event(
            string eventId,
            float startTime,
            SkillDamageEffect[] damageEffects = null,
            SkillPhysicsEffect[] physicsEffects = null,
            SkillAttributeEffect[] attributeEffects = null,
            SkillCueEntry[] cues = null)
        {
            return new SkillTimelineEvent
            {
                eventId = eventId,
                startTime = startTime,
                damageEffects = damageEffects != null ? new List<SkillDamageEffect>(damageEffects) : new List<SkillDamageEffect>(),
                physicsEffects = physicsEffects != null ? new List<SkillPhysicsEffect>(physicsEffects) : new List<SkillPhysicsEffect>(),
                attributeEffects = attributeEffects != null ? new List<SkillAttributeEffect>(attributeEffects) : new List<SkillAttributeEffect>(),
                cues = cues != null ? new List<SkillCueEntry>(cues) : new List<SkillCueEntry>(),
            };
        }

        private static SkillTimelineEvent RepeatedEvent(
            string eventId,
            float startTime,
            float activeDuration,
            float repeatInterval,
            SkillDamageEffect[] damageEffects = null,
            SkillPhysicsEffect[] physicsEffects = null,
            SkillAttributeEffect[] attributeEffects = null,
            SkillCueEntry[] cues = null)
        {
            return new SkillTimelineEvent
            {
                eventId = eventId,
                startTime = startTime,
                triggerMode = SkillEventTriggerMode.Repeated,
                activeDuration = activeDuration,
                repeatInterval = repeatInterval,
                damageEffects = damageEffects != null ? new List<SkillDamageEffect>(damageEffects) : new List<SkillDamageEffect>(),
                physicsEffects = physicsEffects != null ? new List<SkillPhysicsEffect>(physicsEffects) : new List<SkillPhysicsEffect>(),
                attributeEffects = attributeEffects != null ? new List<SkillAttributeEffect>(attributeEffects) : new List<SkillAttributeEffect>(),
                cues = cues != null ? new List<SkillCueEntry>(cues) : new List<SkillCueEntry>(),
            };
        }

        private static SkillDamageEffect Damage(string damageName, float magnitude)
        {
            return new SkillDamageEffect
            {
                damageName = damageName,
                damageMagnitude = magnitude,
            };
        }

        private static SkillPhysicsEffect Knockback(float distance, float duration, SkillEffectDirection direction = SkillEffectDirection.Forward)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.Knockback,
                targetMode = SkillTargetMode.DetectedTargets,
                direction = direction,
                magnitude = distance,
                duration = duration,
            };
        }

        private static SkillPhysicsEffect Pull(float distance, float duration, SkillEffectDirection direction = SkillEffectDirection.TowardTarget)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.Pull,
                targetMode = SkillTargetMode.DetectedTargets,
                direction = direction,
                magnitude = distance,
                duration = duration,
            };
        }

        private static SkillPhysicsEffect SelfDash(float distance, float duration, SkillEffectDirection direction = SkillEffectDirection.TowardTarget)
        {
            return new SkillPhysicsEffect
            {
                effectType = PhysicsEffectType.DashSelf,
                targetMode = SkillTargetMode.Self,
                direction = direction,
                magnitude = distance,
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

        private static SharedSkillDefinition CreateMeleeMinionChop()
        {
            return Skill(
                "melee_minion_chop",
                "Melee Minion Chop",
                Event("hit", 0.24f,
                    damageEffects: new[] { Damage("EnemyMeleeLight", 1f) },
                    physicsEffects: new[] { Knockback(0.8f, 0.08f) }));
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
                    damageEffects: new[] { Damage("EnemyMeleeArc", 1f) },
                    physicsEffects: new[] { Knockback(1f, 0.1f) }));
        }

        private static SharedSkillDefinition CreateElite1Slam()
        {
            return Skill(
                "elite_1_slam",
                "Elite 1 Slam",
                Event("slam", 0.48f,
                    damageEffects: new[] { Damage("EnemyMeleeHeavy", 1.35f) },
                    physicsEffects: new[] { Knockback(1.8f, 0.14f) }));
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
                    damageEffects: new[] { Damage("EnemyPullWave", 0.8f) },
                    physicsEffects: new[] { Pull(2.2f, 0.18f) }));
        }

        private static SharedSkillDefinition CreateGuardian1Crush()
        {
            return Skill(
                "guardian_1_crush",
                "Guardian 1 Crush",
                Event("crush", 0.5f,
                    damageEffects: new[] { Damage("EnemyMeleeHeavy", 1.5f) },
                    physicsEffects: new[] { Knockback(2f, 0.18f) }));
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
                    damageEffects: new[] { Damage("EnemyChargeImpact", 1.45f) },
                    physicsEffects: new[] { Knockback(2.1f, 0.14f) }));
        }

        private static SharedSkillDefinition CreateGuardian2ChainPull()
        {
            return Skill(
                "guardian_2_chain_pull",
                "Guardian 2 Chain Pull",
                Event("chain", 0.32f,
                    damageEffects: new[] { Damage("EnemyPullWave", 0.95f) },
                    physicsEffects: new[] { Pull(2.8f, 0.2f) }));
        }

        private static SharedSkillDefinition CreateBoss1Slash()
        {
            return Skill(
                "boss_1_slash",
                "Boss 1 Slash",
                Event("slash", 0.34f,
                    damageEffects: new[] { Damage("EnemyBossCleave", 1.45f) },
                    physicsEffects: new[] { Knockback(1.2f, 0.1f) }));
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
                    damageEffects: new[] { Damage("EnemyChargeImpact", 1.8f) },
                    physicsEffects: new[] { Knockback(2.2f, 0.16f) }));
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
                    damageEffects: new[] { Damage("EnemyBossCleave", 1.1f) },
                    physicsEffects: new[] { Knockback(1.6f, 0.1f) }));
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
                    damageEffects: new[] { Damage("EnemyChargeImpact", 2f) },
                    physicsEffects: new[] { Knockback(2.4f, 0.18f) }));
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
                    physicsEffects: new[] { Pull(2.5f, 0.18f) }),
                Event("cleave", 0.46f,
                    damageEffects: new[] { Damage("EnemyBossCleave", 1.35f) },
                    physicsEffects: new[] { Knockback(1.4f, 0.1f) }));
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
                    damageEffects: new[] { Damage("EnemyChargeImpact", 1.75f) },
                    physicsEffects: new[] { Knockback(2f, 0.16f) }));
        }

        private static SharedSkillDefinition CreateBoss3Drain()
        {
            return Skill(
                "boss_3_drain",
                "Boss 3 Drain",
                Event("drain_hit", 0.28f,
                    damageEffects: new[] { Damage("EnemyBossRoar", 0.75f) },
                    physicsEffects: new[] { Pull(1.6f, 0.12f, SkillEffectDirection.Forward) }),
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
                    damageEffects: new[] { Damage("EnemyMeleeSpin", 0.7f) },
                    physicsEffects: new[] { Knockback(0.7f, 0.08f) }));
        }

        private static SharedSkillDefinition CreateBoss4PiercingShot()
        {
            return Skill(
                "boss_4_piercing_shot",
                "Boss 4 Piercing Shot",
                Event("pierce", 0.28f,
                    damageEffects: new[] { Damage("EnemyRangedHeavy", 1.4f) },
                    physicsEffects: new[] { Knockback(1.8f, 0.12f, SkillEffectDirection.Forward) }));
        }

        private static SharedSkillDefinition CreateBoss4Pursuit()
        {
            return Skill(
                "boss_4_pursuit",
                "Boss 4 Pursuit",
                Event("rush", 0.04f,
                    physicsEffects: new[] { SelfDash(4.6f, 0.22f) }),
                Event("finish", 0.26f,
                    damageEffects: new[] { Damage("EnemyChargeImpact", 1.95f) },
                    physicsEffects: new[] { Knockback(2.3f, 0.18f) }));
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
                    damageEffects: new[] { Damage("EnemyBossCleave", 1.7f) },
                    physicsEffects: new[] { Knockback(2f, 0.12f) }));
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
                    damageEffects: new[] { Damage("EnemyChargeImpact", 2.1f) },
                    physicsEffects: new[] { Knockback(2.6f, 0.2f) }));
        }

        private static SharedSkillDefinition CreateBoss5RegenRoar()
        {
            return Skill(
                "boss_5_regen_roar",
                "Boss 5 Regen Roar",
                Event("roar_hit", 0.25f,
                    damageEffects: new[] { Damage("EnemyBossRoar", 0.85f) },
                    physicsEffects: new[] { Pull(1.2f, 0.1f, SkillEffectDirection.Forward) }),
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
