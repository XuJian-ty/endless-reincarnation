using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Domain;

namespace Game.Data
{
    public enum EnemySkillPhaseAvailability
    {
        Always,
        LowHpOnly,
        HighHpOnly,
    }

    public enum EnemySkillEventTriggerMode
    {
        Once,
        Repeated,
    }

    public enum EnemySkillTargetMode
    {
        Self,
        CurrentTarget,
        DetectedTargets,
    }

    public enum EnemySkillEffectType
    {
        Damage,
        Heal,
        ApplyStatModifier,
        Knockback,
        Pull,
        DisplaceSelf,
    }

    public enum EnemySkillDirectionMode
    {
        Forward,
        TowardCurrentTarget,
        AwayFromCurrentTarget,
    }

    [Serializable]
    public class EnemySkillCueEntry
    {
        [InspectorLabel("特效/音效名")]
        [Tooltip("对应动画帧特效库中的 effectName")]
        public string cueName = "";
    }

    [Serializable]
    public class EnemySkillEffectEntry
    {
        [InspectorLabel("效果类型")]
        public EnemySkillEffectType effectType = EnemySkillEffectType.Damage;

        [InspectorLabel("目标模式")]
        public EnemySkillTargetMode targetMode = EnemySkillTargetMode.DetectedTargets;

        [InspectorLabel("效果数值")]
        [Tooltip("Damage=伤害倍率；Heal=数值；Knockback/Pull=位移距离；DisplaceSelf=自身位移距离")]
        public float magnitude = 1f;

        [InspectorLabel("持续时间")]
        [Tooltip("ApplyStatModifier=Buff时长；位移类=移动时长")]
        public float duration = 0f;

        [InspectorLabel("按最大生命百分比")]
        [Tooltip("仅对治疗生效：将效果数值解释为最大生命百分比，例如 0.2=20%")]
        public bool useMaxHpPercent;

        [InspectorLabel("方向模式")]
        [Tooltip("用于击退/拉拽/自身位移的方向来源")]
        public EnemySkillDirectionMode directionMode = EnemySkillDirectionMode.Forward;

        [InspectorLabel("伤害检测名覆盖")]
        [Tooltip("留空则使用事件中的 detectorDamageName")]
        public string damageNameOverride = "";

        [InspectorLabel("属性修正")]
        [Tooltip("仅当效果类型为 ApplyStatModifier 时生效")]
        public StatModifier statModifier = new StatModifier();
    }

    [Serializable]
    public class EnemySkillEventEntry
    {
        [InspectorLabel("事件ID")]
        public string eventId = "";

        [InspectorLabel("开始时间")]
        [Min(0f)] public float startTime = 0f;

        [InspectorLabel("触发模式")]
        public EnemySkillEventTriggerMode triggerMode = EnemySkillEventTriggerMode.Once;

        [InspectorLabel("持续时长")]
        [Tooltip("仅在 Repeated 模式下生效")]
        [Min(0f)] public float activeDuration = 0f;

        [InspectorLabel("重复间隔")]
        [Tooltip("仅在 Repeated 模式下生效")]
        [Min(0.01f)] public float repeatInterval = 0.1f;

        [InspectorLabel("伤害检测名")]
        [Tooltip("留空表示不走碰撞检测，效果将按 targetMode 直接生效")]
        public string detectorDamageName = "";

        [InspectorLabel("效果列表")]
        public List<EnemySkillEffectEntry> effects = new List<EnemySkillEffectEntry>();

        [InspectorLabel("特效提示列表")]
        public List<EnemySkillCueEntry> cues = new List<EnemySkillCueEntry>();

        public float GetEndTime()
        {
            if (triggerMode == EnemySkillEventTriggerMode.Repeated)
                return startTime + Mathf.Max(0f, activeDuration);
            return startTime;
        }
    }

    [Serializable]
    public class EnemySkillDefinition
    {
        [InspectorLabel("技能ID")]
        public string skillId = "";

        [InspectorLabel("显示名称")]
        public string displayName = "";

        [InspectorLabel("动画触发器")]
        [Tooltip("例如：CastSkill0 / CastSkill1 / CastSkill2 / CastSkill3")]
        public string animationTrigger = "";

        [InspectorLabel("冷却时间")]
        [Min(0f)] public float cooldown = 1f;

        [InspectorLabel("施法时长")]
        [Tooltip("AI 执行该技能时的总锁定时长")]
        [Min(0.01f)] public float castDuration = 0.6f;

        [InspectorLabel("后摇Idle时长")]
        [Tooltip("仅在“释放后进入Idle”开启时生效")]
        [Min(0f)] public float postCastIdleDuration = 0f;

        [InspectorLabel("释放后进入Idle")]
        [Tooltip("开启后，技能结束进入后摇Idle，并执行后撤控距")]
        public bool enterIdleAfterCast;

        [InspectorLabel("默认施法距离")]
        [Min(0.1f)] public float castRange = 2f;

        [InspectorLabel("施法时朝向目标")]
        public bool rotateToTargetOnCast = true;

        [InspectorLabel("忽略动画伤害事件")]
        [Tooltip("开启后，动画 OnDealDamage 不再直接结算伤害")]
        public bool ignoreAnimationDamageEvents = true;

        [InspectorLabel("时间轴事件")]
        public List<EnemySkillEventEntry> events = new List<EnemySkillEventEntry>();

        public float GetTimelineDuration()
        {
            float maxTime = 0f;
            if (events != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    var evt = events[i];
                    if (evt == null) continue;
                    maxTime = Mathf.Max(maxTime, evt.GetEndTime());
                }
            }

            return Mathf.Max(castDuration, maxTime);
        }

        /// <summary>
        /// 将旧格式的 <see cref="EnemySkillDefinition"/> 转换为共享技能定义。
        /// 用于在共享技能库中未找到对应条目时的向下兼容回退。
        /// </summary>
        public SharedSkillDefinition ToShared()
        {
            var shared = new SharedSkillDefinition
            {
                skillId                     = skillId,
                displayName                 = displayName,
                ignoreAnimationDamageEvents = ignoreAnimationDamageEvents,
                events                      = new List<SkillTimelineEvent>(),
            };

            if (events != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    var srcEvt = events[i];
                    if (srcEvt == null) continue;
                    shared.events.Add(ConvertEvent(srcEvt));
                }
            }

            return shared;
        }

        private static SkillTimelineEvent ConvertEvent(EnemySkillEventEntry src)
        {
            var dst = new SkillTimelineEvent
            {
                eventId          = src.eventId,
                startTime        = src.startTime,
                triggerMode      = src.triggerMode == EnemySkillEventTriggerMode.Repeated
                                       ? SkillEventTriggerMode.Repeated
                                       : SkillEventTriggerMode.Once,
                activeDuration   = src.activeDuration,
                repeatInterval   = src.repeatInterval,
                hitDetectionName = src.detectorDamageName,
                damageMagnitude  = 1f,
                physicsEffects   = new List<SkillPhysicsEffect>(),
                attributeEffects = new List<SkillAttributeEffect>(),
                cues             = new List<SkillCueEntry>(),
            };

            // 转换旧效果列表
            if (src.effects != null)
            {
                for (int i = 0; i < src.effects.Count; i++)
                {
                    var eff = src.effects[i];
                    if (eff == null) continue;
                    ConvertEffect(eff, src, dst);
                }
            }

            // 旧格式 cueName 已废弃，新系统改用 particlePrefab 直接引用，转换时跳过。

            return dst;
        }

        private static void ConvertEffect(EnemySkillEffectEntry eff, EnemySkillEventEntry srcEvt, SkillTimelineEvent dst)
        {
            var targetMode = eff.targetMode switch
            {
                EnemySkillTargetMode.Self           => SkillTargetMode.Self,
                EnemySkillTargetMode.CurrentTarget  => SkillTargetMode.CurrentTarget,
                _                                   => SkillTargetMode.DetectedTargets,
            };

            var direction = eff.directionMode switch
            {
                EnemySkillDirectionMode.TowardCurrentTarget  => SkillEffectDirection.TowardTarget,
                EnemySkillDirectionMode.AwayFromCurrentTarget => SkillEffectDirection.AwayFromTarget,
                _                                             => SkillEffectDirection.Forward,
            };

            switch (eff.effectType)
            {
                case EnemySkillEffectType.Damage:
                    dst.hitDetectionName = !string.IsNullOrEmpty(eff.damageNameOverride)
                        ? eff.damageNameOverride
                        : srcEvt.detectorDamageName;
                    dst.damageMagnitude = eff.magnitude > 0f ? eff.magnitude : 1f;
                    break;

                case EnemySkillEffectType.Heal:
                    dst.attributeEffects.Add(new SkillAttributeEffect
                    {
                        statField  = SkillStatField.HP,
                        targetMode = targetMode,
                        magnitude  = eff.magnitude,
                        usePercent = eff.useMaxHpPercent,
                    });
                    break;

                case EnemySkillEffectType.ApplyStatModifier:
                    // 旧格式的多字段修正在新系统中已不支持，转换时跳过。
                    // 如需转换，请在共享技能库中为每个属性字段添加独立的 SkillAttributeEffect 条目。
                    break;

                case EnemySkillEffectType.Knockback:
                    dst.physicsEffects.Add(new SkillPhysicsEffect
                    {
                        effectType = PhysicsEffectType.Knockback,
                        targetMode = targetMode,
                        direction  = direction,
                        magnitude  = eff.magnitude,
                        duration   = eff.duration > 0f ? eff.duration : 0.2f,
                    });
                    break;

                case EnemySkillEffectType.Pull:
                    dst.physicsEffects.Add(new SkillPhysicsEffect
                    {
                        effectType = PhysicsEffectType.Pull,
                        targetMode = targetMode,
                        direction  = direction,
                        magnitude  = eff.magnitude,
                        duration   = eff.duration > 0f ? eff.duration : 0.2f,
                    });
                    break;

                case EnemySkillEffectType.DisplaceSelf:
                    dst.physicsEffects.Add(new SkillPhysicsEffect
                    {
                        effectType = PhysicsEffectType.DashSelf,
                        targetMode = SkillTargetMode.Self,
                        direction  = direction,
                        magnitude  = eff.magnitude,
                        duration   = eff.duration > 0f ? eff.duration : 0.12f,
                    });
                    break;
            }
        }
    }

    [Serializable]
    public class EnemySkillSlotBinding
    {
        [Header("─ 槽位标识 ──────────────────────────────────────")]
        [InspectorLabel("槽位索引")]
        [Tooltip("从 0 开始的槽位编号。行为树通过此索引决策使用哪个技能。\n同一敌人下槽位索引应唯一。")]
        [Min(0)] public int slotIndex;

        [InspectorLabel("技能 ID")]
        [Tooltip("对应「共享技能库（SharedSkillDatabaseSO）」中某条定义的 skillId。\n系统按此 ID 查找时间轴定义；找不到时会输出警告日志。")]
        public string skillId = "";

        [Header("─ AI 战斗参数 ──────────────────────────────────")]
        [InspectorLabel("冷却时间（秒）")]
        [Tooltip("使用该技能后，等待多久才能再次选择此槽位。\n单位：秒。")]
        [Min(0f)] public float cooldown = 1f;

        [InspectorLabel("施法距离（米）")]
        [Tooltip("AI 在目标距离 ≤ 此值时才会选择该技能。\n同时影响战术 AI 的「理想作战距离」计算。")]
        [Min(0.1f)] public float castRange = 3f;

        [InspectorLabel("施法锁定时长（秒）")]
        [Tooltip("AI 进入技能状态后保持「正在施法」的最短时长。\n应略长于或等于动画实际时长，防止 AI 过早结束技能。\n若时间轴事件总时长更长，以时间轴为准。")]
        [Min(0.01f)] public float castDuration = 0.6f;

        [InspectorLabel("施法后进入后摇 Idle")]
        [Tooltip("勾选后，技能动画结束会进入「战术 Idle」状态（小幅后撤 + 等待），模拟攻击后的喘息。\n不勾选则立即回到普通状态机流程。")]
        public bool enterIdleAfterCast = false;

        [InspectorLabel("后摇 Idle 持续时长（秒）")]
        [Tooltip("「施法后进入后摇 Idle」开启时，Idle 持续多久。\n0 = 仅做一步后撤不等待。仅在上方选项勾选时生效。")]
        [Min(0f)] public float postCastIdleDuration = 0f;

        [InspectorLabel("施法时朝向目标")]
        [Tooltip("技能开始瞬间自动旋转面朝当前目标。\n关闭后 AI 不主动转身（适合需要预判位置的技能）。")]
        public bool rotateToTargetOnCast = true;

        [Header("─ 动画 ──────────────────────────────────────────")]
        [InspectorLabel("动画触发器名称")]
        [Tooltip("施法时发送给 Animator 的 Trigger 参数名。\n留空则自动使用 \"CastSkill{槽位索引}\"（如槽位0 = CastSkill0）。\n需要与 Animator Controller 中的参数名完全一致。")]
        public string animationTrigger = "";

        [Header("─ 阶段限制 ──────────────────────────────────────")]
        [InspectorLabel("可用阶段")]
        [Tooltip("Always   = 全程可用（普通技能）\nLowHpOnly  = 仅在 HP ≤ 50% 时可选（狂暴技能）\nHighHpOnly = 仅在 HP > 50% 时可选（保护性技能）")]
        public EnemySkillPhaseAvailability phaseAvailability = EnemySkillPhaseAvailability.Always;
    }

    [CreateAssetMenu(menuName = "游戏/配置/敌人技能库", fileName = "敌人技能库")]
    public class EnemySkillDatabaseSO : ScriptableObject
    {
        [InspectorLabel("技能列表")]
        public List<EnemySkillDefinition> entries = new List<EnemySkillDefinition>();

        public EnemySkillDefinition GetEntry(string skillId)
        {
            if (entries == null || string.IsNullOrEmpty(skillId)) return null;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.skillId == skillId)
                    return entry;
            }

            return null;
        }
    }
    public static class EnemySkillDatabaseDefaults
    {
        public static EnemySkillDatabaseSO CreateRuntimeDefault()
        {
            var db = ScriptableObject.CreateInstance<EnemySkillDatabaseSO>();
            db.entries = new List<EnemySkillDefinition>
            {
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
            return db;
        }

        private static EnemySkillDefinition Skill(
            string skillId,
            string displayName,
            string animationTrigger,
            float cooldown,
            float castDuration,
            float castRange,
            params EnemySkillEventEntry[] events)
        {
            return Skill(
                skillId,
                displayName,
                animationTrigger,
                cooldown,
                castDuration,
                castRange,
                true,
                events);
        }

        private static EnemySkillDefinition Skill(
            string skillId,
            string displayName,
            string animationTrigger,
            float cooldown,
            float castDuration,
            float castRange,
            bool rotateToTargetOnCast,
            params EnemySkillEventEntry[] events)
        {
            return new EnemySkillDefinition
            {
                skillId = skillId,
                displayName = displayName,
                animationTrigger = animationTrigger,
                cooldown = cooldown,
                castDuration = castDuration,
                castRange = castRange,
                rotateToTargetOnCast = rotateToTargetOnCast,
                ignoreAnimationDamageEvents = true,
                events = new List<EnemySkillEventEntry>(events),
            };
        }

        private static EnemySkillDefinition WithPostCastRecovery(EnemySkillDefinition definition, float duration)
        {
            if (definition == null)
                return null;

            float clampedDuration = Mathf.Max(0f, duration);
            definition.enterIdleAfterCast = clampedDuration > 0f;
            definition.postCastIdleDuration = clampedDuration;
            return definition;
        }

        private static EnemySkillEventEntry Event(
            string eventId,
            float startTime,
            string detectorDamageName,
            params EnemySkillEffectEntry[] effects)
        {
            return new EnemySkillEventEntry
            {
                eventId = eventId,
                startTime = startTime,
                detectorDamageName = detectorDamageName,
                effects = new List<EnemySkillEffectEntry>(effects),
            };
        }

        private static EnemySkillEventEntry RepeatedEvent(
            string eventId,
            float startTime,
            float activeDuration,
            float repeatInterval,
            string detectorDamageName,
            params EnemySkillEffectEntry[] effects)
        {
            return new EnemySkillEventEntry
            {
                eventId = eventId,
                startTime = startTime,
                triggerMode = EnemySkillEventTriggerMode.Repeated,
                activeDuration = activeDuration,
                repeatInterval = repeatInterval,
                detectorDamageName = detectorDamageName,
                effects = new List<EnemySkillEffectEntry>(effects),
            };
        }

        private static EnemySkillEffectEntry Damage(float magnitude, string damageNameOverride = "")
        {
            return new EnemySkillEffectEntry
            {
                effectType = EnemySkillEffectType.Damage,
                targetMode = EnemySkillTargetMode.DetectedTargets,
                magnitude = magnitude,
                damageNameOverride = damageNameOverride,
            };
        }

        private static EnemySkillEffectEntry HealSelf(float magnitude, bool useMaxHpPercent = false)
        {
            return new EnemySkillEffectEntry
            {
                effectType = EnemySkillEffectType.Heal,
                targetMode = EnemySkillTargetMode.Self,
                magnitude = magnitude,
                useMaxHpPercent = useMaxHpPercent,
            };
        }

        private static EnemySkillEffectEntry Knockback(float distance, float duration, EnemySkillDirectionMode directionMode = EnemySkillDirectionMode.Forward)
        {
            return new EnemySkillEffectEntry
            {
                effectType = EnemySkillEffectType.Knockback,
                targetMode = EnemySkillTargetMode.DetectedTargets,
                magnitude = distance,
                duration = duration,
                directionMode = directionMode,
            };
        }

        private static EnemySkillEffectEntry Pull(float distance, float duration, EnemySkillDirectionMode directionMode = EnemySkillDirectionMode.Forward)
        {
            return new EnemySkillEffectEntry
            {
                effectType = EnemySkillEffectType.Pull,
                targetMode = EnemySkillTargetMode.DetectedTargets,
                magnitude = distance,
                duration = duration,
                directionMode = directionMode,
            };
        }

        private static EnemySkillEffectEntry SelfDash(float distance, float duration, EnemySkillDirectionMode directionMode = EnemySkillDirectionMode.TowardCurrentTarget)
        {
            return new EnemySkillEffectEntry
            {
                effectType = EnemySkillEffectType.DisplaceSelf,
                targetMode = EnemySkillTargetMode.Self,
                magnitude = distance,
                duration = duration,
                directionMode = directionMode,
            };
        }

        private static EnemySkillEffectEntry SelfBuff(float duration, float attackAdd = 0f, float defenseAdd = 0f, float moveSpeedAdd = 0f, float hpRegenAdd = 0f)
        {
            return new EnemySkillEffectEntry
            {
                effectType = EnemySkillEffectType.ApplyStatModifier,
                targetMode = EnemySkillTargetMode.Self,
                duration = duration,
                statModifier = new StatModifier
                {
                    attackAdd = attackAdd,
                    defenseAdd = defenseAdd,
                    moveSpeedAdd = moveSpeedAdd,
                    hpRegenAdd = hpRegenAdd,
                }
            };
        }

        private static EnemySkillDefinition CreateMeleeMinionChop()
        {
            return Skill(
                "melee_minion_chop",
                "Melee Minion Chop",
                "CastSkill0",
                0.65f,
                0.72f,
                2.2f,
                Event("hit", 0.24f, "EnemyMeleeLight", Damage(1f), Knockback(0.8f, 0.08f)));
        }

        private static EnemySkillDefinition CreateRangedMinionShot()
        {
            return Skill(
                "ranged_minion_shot",
                "Ranged Minion Shot",
                "CastSkill0",
                1.2f,
                0.85f,
                12f,
                Event("shot", 0.28f, "EnemyRangedShot", Damage(1f)));
        }

        private static EnemySkillDefinition CreateElite1Combo()
        {
            return Skill(
                "elite_1_combo",
                "Elite 1 Combo",
                "CastSkill0",
                0.95f,
                1f,
                2.6f,
                Event("hit_a", 0.2f, "EnemyMeleeArc", Damage(0.85f)),
                Event("hit_b", 0.45f, "EnemyMeleeArc", Damage(1f), Knockback(1f, 0.1f)));
        }

        private static EnemySkillDefinition CreateElite1Slam()
        {
            return WithPostCastRecovery(
                Skill(
                    "elite_1_slam",
                    "Elite 1 Slam",
                    "CastSkill1",
                    3.2f,
                    1.1f,
                    3f,
                    Event("slam", 0.48f, "EnemyMeleeHeavy", Damage(1.35f), Knockback(1.8f, 0.14f))),
                0.3f);
        }

        private static EnemySkillDefinition CreateElite2BurstShot()
        {
            return Skill(
                "elite_2_burst_shot",
                "Elite 2 Burst Shot",
                "CastSkill0",
                1.6f,
                1.2f,
                13f,
                RepeatedEvent("burst", 0.2f, 0.36f, 0.18f, "EnemyRangedBurst", Damage(0.65f)));
        }

        private static EnemySkillDefinition CreateElite2HookPull()
        {
            return Skill(
                "elite_2_hook_pull",
                "Elite 2 Hook Pull",
                "CastSkill1",
                3.6f,
                1.1f,
                4f,
                Event("hook", 0.35f, "EnemyPullWave", Damage(0.8f), Pull(2.2f, 0.18f, EnemySkillDirectionMode.TowardCurrentTarget)));
        }

        private static EnemySkillDefinition CreateGuardian1Crush()
        {
            return WithPostCastRecovery(
                Skill(
                    "guardian_1_crush",
                    "Guardian 1 Crush",
                    "CastSkill0",
                    3.5f,
                    1.15f,
                    2.8f,
                    Event("crush", 0.5f, "EnemyMeleeHeavy", Damage(1.5f), Knockback(2f, 0.18f))),
                0.35f);
        }

        private static EnemySkillDefinition CreateGuardian1Fortify()
        {
            return Skill(
                "guardian_1_fortify",
                "Guardian 1 Fortify",
                "CastSkill1",
                5f,
                1f,
                1f,
                false,
                Event("fortify", 0.15f, string.Empty, SelfBuff(4f, attackAdd: 4f, defenseAdd: 8f)));
        }

        private static EnemySkillDefinition CreateGuardian2Bash()
        {
            return WithPostCastRecovery(
                Skill(
                    "guardian_2_bash",
                    "Guardian 2 Bash",
                    "CastSkill0",
                    3.4f,
                    1f,
                    3.2f,
                    Event("step", 0.05f, string.Empty, SelfDash(2.8f, 0.18f)),
                    Event("impact", 0.28f, "EnemyChargeImpact", Damage(1.45f), Knockback(2.1f, 0.14f))),
                0.3f);
        }

        private static EnemySkillDefinition CreateGuardian2ChainPull()
        {
            return Skill(
                "guardian_2_chain_pull",
                "Guardian 2 Chain Pull",
                "CastSkill1",
                4.2f,
                1.1f,
                5f,
                Event("chain", 0.32f, "EnemyPullWave", Damage(0.95f), Pull(2.8f, 0.2f, EnemySkillDirectionMode.TowardCurrentTarget)));
        }

        private static EnemySkillDefinition CreateBoss1Slash()
        {
            return Skill(
                "boss_1_slash",
                "Boss 1 Slash",
                "CastSkill0",
                2.2f,
                0.9f,
                3f,
                Event("slash", 0.34f, "EnemyBossCleave", Damage(1.45f), Knockback(1.2f, 0.1f)));
        }

        private static EnemySkillDefinition CreateBoss1Shot()
        {
            return Skill(
                "boss_1_shot",
                "Boss 1 Shot",
                "CastSkill1",
                2.8f,
                1f,
                14f,
                Event("shot", 0.28f, "EnemyRangedHeavy", Damage(1.15f)));
        }

        private static EnemySkillDefinition CreateBoss1Charge()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_1_charge",
                    "Boss 1 Charge",
                    "CastSkill2",
                    4f,
                    1f,
                    5f,
                    Event("dash", 0.05f, string.Empty, SelfDash(3.2f, 0.2f)),
                    Event("impact", 0.3f, "EnemyChargeImpact", Damage(1.8f), Knockback(2.2f, 0.16f))),
                0.45f);
        }

        private static EnemySkillDefinition CreateBoss1Roar()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_1_roar",
                    "Boss 1 Roar",
                    "CastSkill3",
                    5f,
                    1.2f,
                    8f,
                    false,
                    Event("buff", 0.2f, string.Empty, SelfBuff(3.5f, attackAdd: 6f, defenseAdd: 4f, moveSpeedAdd: 0.2f))),
                0.55f);
        }

        private static EnemySkillDefinition CreateBoss2CrossSlash()
        {
            return Skill(
                "boss_2_cross_slash",
                "Boss 2 Cross Slash",
                "CastSkill0",
                2.1f,
                1f,
                3.2f,
                Event("cut_a", 0.22f, "EnemyMeleeArc", Damage(0.9f)),
                Event("cut_b", 0.48f, "EnemyBossCleave", Damage(1.1f), Knockback(1.6f, 0.1f)));
        }

        private static EnemySkillDefinition CreateBoss2Volley()
        {
            return Skill(
                "boss_2_volley",
                "Boss 2 Volley",
                "CastSkill1",
                2.7f,
                1.2f,
                15f,
                RepeatedEvent("volley", 0.22f, 0.45f, 0.15f, "EnemyRangedBurst", Damage(0.72f)));
        }

        private static EnemySkillDefinition CreateBoss2Crash()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_2_crash",
                    "Boss 2 Crash",
                    "CastSkill2",
                    3.8f,
                    1.05f,
                    5.5f,
                    Event("dash", 0.05f, string.Empty, SelfDash(3.8f, 0.2f)),
                    Event("crash", 0.32f, "EnemyChargeImpact", Damage(2f), Knockback(2.4f, 0.18f))),
                0.5f);
        }

        private static EnemySkillDefinition CreateBoss2Rage()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_2_rage",
                    "Boss 2 Rage",
                    "CastSkill3",
                    4.8f,
                    1.15f,
                    8f,
                    false,
                    Event("rage", 0.18f, string.Empty, SelfBuff(4f, attackAdd: 8f, moveSpeedAdd: 0.35f))),
                0.55f);
        }

        private static EnemySkillDefinition CreateBoss3PullCleave()
        {
            return Skill(
                "boss_3_pull_cleave",
                "Boss 3 Pull Cleave",
                "CastSkill0",
                2.4f,
                1.05f,
                4f,
                Event("pull", 0.14f, "EnemyPullWave", Pull(2.5f, 0.18f, EnemySkillDirectionMode.TowardCurrentTarget)),
                Event("cleave", 0.46f, "EnemyBossCleave", Damage(1.35f), Knockback(1.4f, 0.1f)));
        }

        private static EnemySkillDefinition CreateBoss3Barrage()
        {
            return Skill(
                "boss_3_barrage",
                "Boss 3 Barrage",
                "CastSkill1",
                2.9f,
                1.25f,
                15f,
                RepeatedEvent("barrage", 0.25f, 0.6f, 0.15f, "EnemyRangedBurst", Damage(0.68f)));
        }

        private static EnemySkillDefinition CreateBoss3Lunge()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_3_lunge",
                    "Boss 3 Lunge",
                    "CastSkill2",
                    3.9f,
                    1f,
                    5.8f,
                    Event("lunge", 0.05f, string.Empty, SelfDash(4.1f, 0.22f)),
                    Event("impact", 0.3f, "EnemyChargeImpact", Damage(1.75f), Knockback(2f, 0.16f))),
                0.45f);
        }

        private static EnemySkillDefinition CreateBoss3Drain()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_3_drain",
                    "Boss 3 Drain",
                    "CastSkill3",
                    5.2f,
                    1.25f,
                    8.5f,
                    false,
                    Event("drain_hit", 0.28f, "EnemyBossRoar", Damage(0.75f), Pull(1.6f, 0.12f)),
                    Event("heal", 0.45f, string.Empty, HealSelf(0.08f, useMaxHpPercent: true), SelfBuff(3f, defenseAdd: 5f))),
                0.55f);
        }

        private static EnemySkillDefinition CreateBoss4Spin()
        {
            return Skill(
                "boss_4_spin",
                "Boss 4 Spin",
                "CastSkill0",
                2f,
                1.15f,
                3.5f,
                RepeatedEvent("spin", 0.2f, 0.5f, 0.16f, "EnemyMeleeSpin", Damage(0.7f), Knockback(0.7f, 0.08f)));
        }

        private static EnemySkillDefinition CreateBoss4PiercingShot()
        {
            return Skill(
                "boss_4_piercing_shot",
                "Boss 4 Piercing Shot",
                "CastSkill1",
                2.5f,
                1f,
                15.5f,
                Event("pierce", 0.28f, "EnemyRangedHeavy", Damage(1.4f), Knockback(1.8f, 0.12f, EnemySkillDirectionMode.Forward)));
        }

        private static EnemySkillDefinition CreateBoss4Pursuit()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_4_pursuit",
                    "Boss 4 Pursuit",
                    "CastSkill2",
                    3.5f,
                    0.95f,
                    6.2f,
                    Event("rush", 0.04f, string.Empty, SelfDash(4.6f, 0.22f)),
                    Event("finish", 0.26f, "EnemyChargeImpact", Damage(1.95f), Knockback(2.3f, 0.18f))),
                0.5f);
        }

        private static EnemySkillDefinition CreateBoss4Battlecry()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_4_battlecry",
                    "Boss 4 Battlecry",
                    "CastSkill3",
                    4.4f,
                    1.15f,
                    8.8f,
                    false,
                    Event("battlecry", 0.18f, string.Empty, SelfBuff(4f, attackAdd: 6f, moveSpeedAdd: 0.45f, hpRegenAdd: 1.2f))),
                0.55f);
        }

        private static EnemySkillDefinition CreateBoss5Cleave()
        {
            return Skill(
                "boss_5_cleave",
                "Boss 5 Cleave",
                "CastSkill0",
                1.9f,
                1f,
                3.6f,
                Event("cleave", 0.38f, "EnemyBossCleave", Damage(1.7f), Knockback(2f, 0.12f)));
        }

        private static EnemySkillDefinition CreateBoss5Storm()
        {
            return Skill(
                "boss_5_storm",
                "Boss 5 Storm",
                "CastSkill1",
                2.3f,
                1.3f,
                16f,
                RepeatedEvent("storm", 0.2f, 0.75f, 0.15f, "EnemyRangedBurst", Damage(0.75f)));
        }

        private static EnemySkillDefinition CreateBoss5Stampede()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_5_stampede",
                    "Boss 5 Stampede",
                    "CastSkill2",
                    3.3f,
                    1.05f,
                    6.5f,
                    Event("charge", 0.04f, string.Empty, SelfDash(5f, 0.24f)),
                    Event("stomp", 0.3f, "EnemyChargeImpact", Damage(2.1f), Knockback(2.6f, 0.2f))),
                0.6f);
        }

        private static EnemySkillDefinition CreateBoss5RegenRoar()
        {
            return WithPostCastRecovery(
                Skill(
                    "boss_5_regen_roar",
                    "Boss 5 Regen Roar",
                    "CastSkill3",
                    4.2f,
                    1.25f,
                    9f,
                    false,
                    Event("roar_hit", 0.25f, "EnemyBossRoar", Damage(0.85f), Pull(1.2f, 0.1f)),
                    Event("regen", 0.42f, string.Empty, HealSelf(0.12f, useMaxHpPercent: true), SelfBuff(4.5f, attackAdd: 5f, defenseAdd: 6f))),
                0.6f);
        }
    }
}
