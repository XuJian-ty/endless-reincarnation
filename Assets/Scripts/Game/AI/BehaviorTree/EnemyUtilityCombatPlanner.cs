using Game.Data;
using Game.Presentation;
using UnityEngine;

namespace Game.AI
{
    public enum EnemyCombatDecisionFocus
    {
        Standard,
        ThreatResponse,
        Punish,
        Search,
    }

    internal readonly struct EnemyUtilityDecision
    {
        public EnemyUtilityDecision(
            EnemyIntent intent,
            float commitDuration,
            float reactionDelay,
            bool registerPressure,
            bool releasePressure,
            int castSkillSlot)
        {
            Intent = intent;
            CommitDuration = commitDuration;
            ReactionDelay = reactionDelay;
            RegisterPressure = registerPressure;
            ReleasePressure = releasePressure;
            CastSkillSlot = castSkillSlot;
        }

        public EnemyIntent Intent { get; }
        public float CommitDuration { get; }
        public float ReactionDelay { get; }
        public bool RegisterPressure { get; }
        public bool ReleasePressure { get; }
        public int CastSkillSlot { get; }
        public bool IsValid => Intent.Type != EnemyIntentType.None || CastSkillSlot >= 0;
    }

    public static class EnemyUtilityCombatPlanner
    {
        private const float SearchArrivalDistance = 1f;

        private readonly struct Candidate
        {
            public Candidate(float score, EnemyUtilityDecision decision)
            {
                Score = score;
                Decision = decision;
            }

            public float Score { get; }
            public EnemyUtilityDecision Decision { get; }
            public bool IsValid => Decision.IsValid;
        }

        private struct CombatFrame
        {
            public EnemyController Controller;
            public EnemyPerception Perception;
            public EnemyArchetypeSO Archetype;
            public EnemyCombatMemory Memory;
            public Vector3 SelfPosition;
            public Vector3 TargetPosition;
            public float Distance;
            public float DesiredDistance;
            public float Tolerance;
            public bool CanPressure;
            public int PreferredStrafeSign;
            public EnemyResolvedSkill PlanningSkill;
            public float PlanningCooldownRemaining;
            public float ReactionDelay;
            public float CommitDuration;
        }

        public static bool TryExecute(EnemyAIContext ctx, EnemyCombatDecisionFocus focus)
        {
            if (ctx?.Controller == null || ctx.Perception == null || ctx.Archetype == null || ctx.Memory == null)
                return false;

            if (focus != EnemyCombatDecisionFocus.Search)
                ctx.Memory.ResetSearch();

            float now = ctx.TimeNow > 0f ? ctx.TimeNow : Time.time;
            EnemyUtilityDecision decision = focus switch
            {
                EnemyCombatDecisionFocus.ThreatResponse => BuildThreatResponseDecision(ctx, now),
                EnemyCombatDecisionFocus.Punish => BuildPunishDecision(ctx, now),
                EnemyCombatDecisionFocus.Search => BuildSearchDecision(ctx, now),
                _ => BuildStandardDecision(ctx, now),
            };

            if (!decision.IsValid)
                return false;

            return ApplyDecision(ctx, decision, now);
        }

        private static bool ApplyDecision(EnemyAIContext ctx, EnemyUtilityDecision decision, float now)
        {
            EnemyController controller = ctx.Controller;
            EnemyArchetypeSO archetype = ctx.Archetype;

            bool success;
            if (decision.CastSkillSlot >= 0)
            {
                success = controller.TryStartSkillSlot(decision.CastSkillSlot, ctx.Perception.TargetPosition);
                if (!success)
                    return false;
            }
            else
            {
                controller.CurrentIntent = decision.Intent;
                success = true;
            }

            if (decision.ReleasePressure)
                EnemySquadCoordinator.Release(controller);
            if (decision.RegisterPressure)
                EnemySquadCoordinator.RegisterPressure(controller, archetype, now, Mathf.Max(0.15f, decision.CommitDuration));

            EnemyIntent rememberedIntent = decision.CastSkillSlot >= 0 ? controller.CurrentIntent : decision.Intent;
            ctx.Memory.RecordDecision(rememberedIntent, now);
            ctx.Memory.ScheduleNextEvaluation(now, decision.ReactionDelay, decision.CommitDuration);
            return success;
        }

        private static EnemyUtilityDecision BuildThreatResponseDecision(EnemyAIContext ctx, float now)
        {
            if (!ctx.Perception.HasImmediateThreat || !ctx.Perception.TargetPosition.HasValue)
                return default;

            CombatFrame frame = BuildCombatFrame(ctx, now);
            Candidate best = default;

            TryTakeBetterCandidate(ref best, BuildDodgeCandidate(frame));
            TryTakeBetterCandidate(ref best, BuildRetreatCandidate(frame, EnemyCombatDecisionFocus.ThreatResponse));
            TryTakeBetterCandidate(ref best, BuildRepositionCandidate(frame, EnemyCombatDecisionFocus.ThreatResponse));
            TryTakeBetterCandidate(ref best, BuildHoldCandidate(frame, EnemyCombatDecisionFocus.ThreatResponse));

            return best.IsValid ? best.Decision : default;
        }

        private static EnemyUtilityDecision BuildPunishDecision(EnemyAIContext ctx, float now)
        {
            if (!ctx.Perception.HasPunishOpportunity || !ctx.Perception.TargetPosition.HasValue)
                return default;

            CombatFrame frame = BuildCombatFrame(ctx, now);
            Candidate best = default;

            TryTakeBetterCandidate(ref best, BuildCastSkillCandidate(frame, EnemyCombatDecisionFocus.Punish));
            TryTakeBetterCandidate(ref best, BuildPunishApproachCandidate(frame));
            TryTakeBetterCandidate(ref best, BuildStrafeCandidate(frame, frame.PreferredStrafeSign, EnemyCombatDecisionFocus.Punish));
            TryTakeBetterCandidate(ref best, BuildStrafeCandidate(frame, -frame.PreferredStrafeSign, EnemyCombatDecisionFocus.Punish));

            return best.IsValid ? best.Decision : default;
        }

        private static EnemyUtilityDecision BuildSearchDecision(EnemyAIContext ctx, float now)
        {
            if (!ctx.Perception.HasRecentTargetMemory(ctx.Archetype.searchMemoryDuration))
                return default;

            EnemyCombatMemory memory = ctx.Memory;
            Vector3 searchAnchor = ctx.Perception.LastKnownTargetPosition;
            memory.SyncSearchAnchor(searchAnchor);

            Vector3 selfPosition = ctx.Controller.transform.position;
            int preferredSign = EnemySquadCoordinator.GetPreferredStrafeSign(ctx.Controller);
            float sweepDistance = ResolveSearchSweepDistance(ctx.Archetype);
            Vector3 searchPos = ResolveSearchPoint(selfPosition, ctx.Controller.transform.forward, searchAnchor, preferredSign, memory.SearchStep, sweepDistance);
            Vector3 delta = searchPos - selfPosition;
            delta.y = 0f;

            if (memory.SearchStep < 3 && delta.sqrMagnitude <= SearchArrivalDistance * SearchArrivalDistance)
            {
                memory.AdvanceSearchStep();
                searchPos = ResolveSearchPoint(selfPosition, ctx.Controller.transform.forward, searchAnchor, preferredSign, memory.SearchStep, sweepDistance);
            }

            bool shouldHold = memory.SearchStep >= 3;

            EnemyIntent intent = new EnemyIntent
            {
                Type = shouldHold ? EnemyIntentType.Hold : EnemyIntentType.Search,
                TargetPosition = shouldHold ? searchAnchor : searchPos
            };

            return new EnemyUtilityDecision(
                intent,
                Mathf.Max(0.1f, ctx.Archetype.GetDecisionCommitDuration() * 0.8f),
                ctx.Archetype.GetRandomReactionDelay(),
                registerPressure: false,
                releasePressure: true,
                castSkillSlot: -1);
        }

        private static float ResolveSearchSweepDistance(EnemyArchetypeSO archetype)
        {
            if (archetype == null)
                return 1.5f;

            float innerDistance = archetype.GetChaseInnerDistance();
            return Mathf.Clamp(innerDistance * 0.6f, 1.2f, 2.5f);
        }

        private static Vector3 ResolveSearchPoint(
            Vector3 selfPosition,
            Vector3 forwardFallback,
            Vector3 anchor,
            int preferredSign,
            int searchStep,
            float sweepDistance)
        {
            if (searchStep <= 0)
                return anchor;

            Vector3 forward = anchor - selfPosition;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = forwardFallback;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
            float signedDistance = sweepDistance * (preferredSign >= 0 ? 1f : -1f);

            return searchStep switch
            {
                1 => anchor + lateral * signedDistance,
                2 => anchor - lateral * signedDistance,
                _ => anchor,
            };
        }

        private static EnemyUtilityDecision BuildStandardDecision(EnemyAIContext ctx, float now)
        {
            if (!ctx.Perception.TargetPosition.HasValue)
                return default;

            CombatFrame frame = BuildCombatFrame(ctx, now);
            Candidate best = default;

            TryTakeBetterCandidate(ref best, BuildCastSkillCandidate(frame, EnemyCombatDecisionFocus.Standard));
            TryTakeBetterCandidate(ref best, BuildApproachCandidate(frame));
            TryTakeBetterCandidate(ref best, BuildPunishApproachCandidate(frame));
            TryTakeBetterCandidate(ref best, BuildStrafeCandidate(frame, frame.PreferredStrafeSign, EnemyCombatDecisionFocus.Standard));
            TryTakeBetterCandidate(ref best, BuildStrafeCandidate(frame, -frame.PreferredStrafeSign, EnemyCombatDecisionFocus.Standard));
            TryTakeBetterCandidate(ref best, BuildRetreatCandidate(frame, EnemyCombatDecisionFocus.Standard));
            TryTakeBetterCandidate(ref best, BuildRepositionCandidate(frame, EnemyCombatDecisionFocus.Standard));
            TryTakeBetterCandidate(ref best, BuildHoldCandidate(frame, EnemyCombatDecisionFocus.Standard));

            return best.IsValid ? best.Decision : default;
        }

        private static CombatFrame BuildCombatFrame(EnemyAIContext ctx, float now)
        {
            EnemyResolvedSkill planningSkill = TrySelectPlanningSkill(ctx.Controller, ctx.Perception, ctx.Archetype, out float cooldownRemaining);
            float desiredDistance = planningSkill != null
                ? ctx.Archetype.GetDesiredCombatDistance(planningSkill.IdealCastRange)
                : ctx.Controller.GetPreferredCombatRange();

            CombatFrame frame;
            frame.Controller = ctx.Controller;
            frame.Perception = ctx.Perception;
            frame.Archetype = ctx.Archetype;
            frame.Memory = ctx.Memory;
            frame.SelfPosition = ctx.Controller.transform.position;
            frame.TargetPosition = ctx.Perception.TargetPosition ?? ctx.Controller.transform.position;
            frame.Distance = ctx.Perception.DistanceToPlayer;
            frame.DesiredDistance = desiredDistance;
            frame.Tolerance = Mathf.Max(0.1f, ctx.Archetype.combatDistanceTolerance);
            frame.CanPressure = EnemySquadCoordinator.CanPressure(ctx.Controller, ctx.Archetype, now);
            frame.PreferredStrafeSign = EnemySquadCoordinator.GetPreferredStrafeSign(ctx.Controller);
            frame.PlanningSkill = planningSkill;
            frame.PlanningCooldownRemaining = cooldownRemaining;
            frame.ReactionDelay = ctx.Archetype.GetRandomReactionDelay();
            frame.CommitDuration = ctx.Archetype.GetDecisionCommitDuration();
            return frame;
        }

        private static Candidate BuildCastSkillCandidate(CombatFrame frame, EnemyCombatDecisionFocus focus)
        {
            float bestScore = float.MinValue;
            int bestSlot = -1;

            int slotCount = frame.Archetype.GetSkillSlotCount();
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (!frame.Controller.CanCastSkill(slot))
                    continue;

                EnemyResolvedSkill skill = frame.Controller.ResolveSkillSlot(slot);
                if (skill == null)
                    continue;
                if (!IsSkillInRange(frame.Perception, frame.Archetype, skill))
                    continue;
                if (!frame.Perception.HasLineOfSight)
                    continue;
                if (frame.Perception.HasImmediateThreat && !skill.CanUseUnderThreat)
                    continue;

                float score = 46f;
                float distanceDelta = Mathf.Abs(frame.Distance - skill.IdealCastRange);
                float rangeScale = Mathf.Max(1f, skill.IdealCastRange);
                score += (1f - Mathf.Clamp01(distanceDelta / rangeScale)) * 20f;
                score += frame.Archetype.aggression * 16f;
                score += frame.Perception.PunishOpportunityScore * skill.PunishWeight * 24f;
                score += GetRoleBonus(skill, frame, focus);
                score -= skill.RiskWeight * frame.Archetype.caution * 15f;
                score -= frame.Memory.GetRepeatPenalty(skill.RepeatPenalty * 14f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                }
            }

            if (bestSlot < 0)
                return default;

            EnemyIntent intent = new EnemyIntent
            {
                Type = EnemyIntentType.CastSkill,
                SkillSlot = bestSlot,
                TargetPosition = frame.Perception.TargetPosition
            };

            return new Candidate(bestScore, new EnemyUtilityDecision(
                intent,
                Mathf.Max(frame.CommitDuration, 0.12f),
                frame.ReactionDelay,
                registerPressure: true,
                releasePressure: false,
                castSkillSlot: bestSlot));
        }

        private static Candidate BuildApproachCandidate(CombatFrame frame)
        {
            if (frame.Distance <= frame.DesiredDistance + frame.Tolerance || !frame.Perception.TargetPosition.HasValue)
                return default;

            Vector3 predicted = frame.Perception.PredictTargetPosition(frame.Archetype.approachLeadTime);
            float distanceOvershoot = frame.Distance - frame.DesiredDistance;
            float score = 24f + frame.Archetype.aggression * 22f + Mathf.Min(distanceOvershoot, 6f) * 4.5f;
            if (!frame.CanPressure)
                score -= 10f;

            EnemyIntent intent = new EnemyIntent
            {
                Type = EnemyIntentType.Approach,
                TargetPosition = predicted
            };

            return new Candidate(score, new EnemyUtilityDecision(
                intent,
                frame.CommitDuration,
                frame.ReactionDelay,
                registerPressure: frame.CanPressure,
                releasePressure: !frame.CanPressure,
                castSkillSlot: -1));
        }

        private static Candidate BuildPunishApproachCandidate(CombatFrame frame)
        {
            if (!frame.Perception.HasPunishOpportunity || !frame.Perception.TargetPosition.HasValue)
                return default;
            if (frame.Distance <= frame.DesiredDistance + frame.Tolerance * 0.5f && frame.CanPressure)
                return default;

            Vector3 predicted = frame.Perception.PredictTargetPosition(frame.Archetype.approachLeadTime * 0.6f);
            float score = 34f + frame.Archetype.punishBias * 24f + frame.Perception.PunishOpportunityScore * 24f;
            if (!frame.CanPressure)
                score -= 6f;

            EnemyIntent intent = new EnemyIntent
            {
                Type = EnemyIntentType.Punish,
                TargetPosition = predicted
            };

            return new Candidate(score, new EnemyUtilityDecision(
                intent,
                Mathf.Max(0.12f, frame.CommitDuration),
                Mathf.Max(0.03f, frame.ReactionDelay * 0.7f),
                registerPressure: frame.CanPressure,
                releasePressure: !frame.CanPressure,
                castSkillSlot: -1));
        }

        private static Candidate BuildStrafeCandidate(CombatFrame frame, int sign, EnemyCombatDecisionFocus focus)
        {
            if (!frame.Perception.HasLineOfSight || !frame.Perception.TargetPosition.HasValue)
                return default;

            bool inBand = Mathf.Abs(frame.Distance - frame.DesiredDistance) <= frame.Tolerance * 1.5f;
            if (!inBand && focus == EnemyCombatDecisionFocus.Standard)
                return default;

            if (!EnemyTacticalNavigation.TryFindStrafePoint(
                    frame.Controller,
                    frame.Perception,
                    frame.Archetype,
                    frame.DesiredDistance,
                    sign,
                    out Vector3 strafePoint))
            {
                return default;
            }

            float score = 20f + frame.Archetype.strafeBias * 20f + (frame.CanPressure ? 5f : 10f);
            if (focus == EnemyCombatDecisionFocus.Punish)
                score += frame.Archetype.punishBias * 8f;
            if (sign != frame.PreferredStrafeSign)
                score -= 1.5f;

            EnemyIntent intent = new EnemyIntent
            {
                Type = sign >= 0 ? EnemyIntentType.StrafeRight : EnemyIntentType.StrafeLeft,
                TargetPosition = strafePoint
            };

            return new Candidate(score, new EnemyUtilityDecision(
                intent,
                Mathf.Max(0.1f, frame.CommitDuration * 0.9f),
                frame.ReactionDelay,
                registerPressure: false,
                releasePressure: true,
                castSkillSlot: -1));
        }

        private static Candidate BuildRetreatCandidate(CombatFrame frame, EnemyCombatDecisionFocus focus)
        {
            bool tooClose = frame.Distance < frame.DesiredDistance - frame.Tolerance;
            if (!tooClose && focus != EnemyCombatDecisionFocus.ThreatResponse)
                return default;

            if (!EnemyTacticalNavigation.TryFindRetreatPoint(
                    frame.Controller,
                    frame.Archetype,
                    frame.TargetPosition,
                    frame.DesiredDistance,
                    frame.Distance,
                    out Vector3 retreatPoint))
            {
                return default;
            }

            float score = 14f + frame.Archetype.caution * 16f + frame.Perception.ThreatScore * 12f;
            if (focus == EnemyCombatDecisionFocus.ThreatResponse)
                score += frame.Archetype.dodgeBias * 12f;
            if (frame.CanPressure && !frame.Perception.HasImmediateThreat)
                score -= 8f;

            EnemyIntent intent = new EnemyIntent
            {
                Type = EnemyIntentType.Retreat,
                TargetPosition = retreatPoint
            };

            return new Candidate(score, new EnemyUtilityDecision(
                intent,
                Mathf.Max(0.1f, frame.CommitDuration * 0.9f),
                Mathf.Max(0.03f, frame.ReactionDelay * 0.8f),
                registerPressure: false,
                releasePressure: true,
                castSkillSlot: -1));
        }

        private static Candidate BuildRepositionCandidate(CombatFrame frame, EnemyCombatDecisionFocus focus)
        {
            bool needFix = !frame.Perception.HasLineOfSight || !frame.Perception.HasReachablePath;
            if (!needFix && focus != EnemyCombatDecisionFocus.ThreatResponse)
                return default;

            if (!EnemyTacticalNavigation.TryFindRepositionPoint(
                    frame.Controller,
                    frame.Perception,
                    frame.Archetype,
                    frame.DesiredDistance,
                    out Vector3 repositionPoint))
            {
                return default;
            }

            float score = 17f + frame.Archetype.caution * 10f;
            if (needFix)
                score += 10f;
            if (focus == EnemyCombatDecisionFocus.ThreatResponse)
                score += frame.Archetype.dodgeBias * 10f;

            EnemyIntent intent = new EnemyIntent
            {
                Type = EnemyIntentType.Reposition,
                TargetPosition = repositionPoint
            };

            return new Candidate(score, new EnemyUtilityDecision(
                intent,
                Mathf.Max(0.1f, frame.CommitDuration),
                frame.ReactionDelay,
                registerPressure: false,
                releasePressure: true,
                castSkillSlot: -1));
        }

        private static Candidate BuildHoldCandidate(CombatFrame frame, EnemyCombatDecisionFocus focus)
        {
            bool inBand = Mathf.Abs(frame.Distance - frame.DesiredDistance) <= frame.Tolerance * 1.35f;
            if (!inBand && focus == EnemyCombatDecisionFocus.Standard)
                return default;

            bool waitingForSkillWindow = frame.PlanningSkill != null && frame.PlanningCooldownRemaining <= 0.35f;
            bool defensiveHold = !frame.CanPressure || frame.Perception.HasImmediateThreat;
            if (!waitingForSkillWindow && !defensiveHold)
                return default;

            float score = 2.5f + frame.Archetype.caution * 7f;
            if (!frame.CanPressure)
                score += 5f;
            if (focus == EnemyCombatDecisionFocus.ThreatResponse)
                score += 4f;
            if (frame.PlanningSkill != null)
                score += Mathf.Clamp(frame.PlanningCooldownRemaining, 0f, 0.35f) * 8f;

            EnemyIntent intent = new EnemyIntent
            {
                Type = EnemyIntentType.Hold,
                TargetPosition = frame.Perception.TargetPosition
            };

            return new Candidate(score, new EnemyUtilityDecision(
                intent,
                Mathf.Max(0.08f, frame.CommitDuration * 0.75f),
                frame.ReactionDelay,
                registerPressure: false,
                releasePressure: true,
                castSkillSlot: -1));
        }

        private static Candidate BuildDodgeCandidate(CombatFrame frame)
        {
            if (!frame.Perception.HasImmediateThreat)
                return default;

            if (!EnemyTacticalNavigation.TryFindDodgePoint(frame.Controller, frame.Perception, frame.Archetype, out Vector3 dodgePoint))
                return default;

            float score = 36f + frame.Archetype.dodgeBias * 24f + frame.Perception.ThreatScore * 26f;
            EnemyIntent intent = new EnemyIntent
            {
                Type = EnemyIntentType.Dodge,
                TargetPosition = dodgePoint
            };

            return new Candidate(score, new EnemyUtilityDecision(
                intent,
                Mathf.Max(0.12f, frame.CommitDuration),
                Mathf.Max(0.02f, frame.ReactionDelay * 0.5f),
                registerPressure: false,
                releasePressure: true,
                castSkillSlot: -1));
        }

        private static EnemyResolvedSkill TrySelectPlanningSkill(
            EnemyController controller,
            EnemyPerception perception,
            EnemyArchetypeSO archetype,
            out float bestCooldownRemaining)
        {
            bestCooldownRemaining = 0f;
            if (controller == null || perception == null || archetype == null)
                return null;

            EnemyResolvedSkill bestSkill = null;
            float bestScore = float.MinValue;
            int slotCount = archetype.GetSkillSlotCount();

            for (int slot = 0; slot < slotCount; slot++)
            {
                if (!archetype.IsSkillSlotAvailableInCurrentPhase(slot, controller.CurrentHpRatio))
                    continue;

                EnemyResolvedSkill skill = controller.ResolveSkillSlot(slot);
                if (skill == null)
                    continue;

                float cooldownRemaining = controller.GetSkillCooldownRemaining(slot);
                float approachDistance = Mathf.Max(0f, perception.DistanceToPlayer - skill.IdealCastRange);
                float approachTime = approachDistance / Mathf.Max(0.1f, controller.MoveSpeed);
                float score = -(Mathf.Max(cooldownRemaining, approachTime) + skill.RiskWeight * 0.4f);
                score += GetRolePlanningBonus(skill, perception);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSkill = skill;
                    bestCooldownRemaining = cooldownRemaining;
                }
            }

            return bestSkill;
        }

        private static bool IsSkillInRange(EnemyPerception perception, EnemyArchetypeSO archetype, EnemyResolvedSkill skill)
        {
            if (perception == null || skill == null)
                return false;

            float tolerance = archetype != null ? Mathf.Max(0f, archetype.castRangeTolerance) : 0f;
            float minRange = Mathf.Max(0f, skill.MinCastRange);
            return perception.DistanceToPlayer + tolerance >= minRange &&
                   perception.DistanceToPlayer <= skill.CastRange + tolerance;
        }

        private static float GetRoleBonus(EnemyResolvedSkill skill, CombatFrame frame, EnemyCombatDecisionFocus focus)
        {
            if (skill == null)
                return 0f;

            float bonus = skill.SkillRole switch
            {
                EnemySkillRole.GapClose when frame.Distance > frame.DesiredDistance + frame.Tolerance => 12f,
                EnemySkillRole.Pressure when frame.CanPressure => 10f,
                EnemySkillRole.Punish when frame.Perception.HasPunishOpportunity => 18f,
                EnemySkillRole.Escape when frame.Perception.HasImmediateThreat => 14f,
                EnemySkillRole.AreaControl when !frame.CanPressure => 8f,
                _ => 0f,
            };

            if (focus == EnemyCombatDecisionFocus.Punish)
                bonus += skill.PunishWeight * 14f;
            else if (focus == EnemyCombatDecisionFocus.ThreatResponse && skill.SkillRole == EnemySkillRole.Escape)
                bonus += 10f;

            return bonus;
        }

        private static float GetRolePlanningBonus(EnemyResolvedSkill skill, EnemyPerception perception)
        {
            if (skill == null || perception == null)
                return 0f;

            return skill.SkillRole switch
            {
                EnemySkillRole.Punish when perception.HasPunishOpportunity => 0.45f,
                EnemySkillRole.Escape when perception.HasImmediateThreat => 0.4f,
                EnemySkillRole.GapClose when perception.DistanceToPlayer > skill.IdealCastRange => 0.3f,
                EnemySkillRole.Pressure => 0.2f,
                _ => 0f,
            };
        }

        private static void TryTakeBetterCandidate(ref Candidate best, Candidate candidate)
        {
            if (!candidate.IsValid)
                return;
            if (!best.IsValid || candidate.Score > best.Score)
                best = candidate;
        }
    }
}
