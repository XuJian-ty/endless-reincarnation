using Game.Data;
using UnityEngine;

namespace Game.Presentation
{
    internal readonly struct PlayerCloneBrainContext
    {
        public PlayerCloneBrainContext(
            PlayerCloneAIConfigSO config,
            CloneCombatMemory combatMemory,
            PlayerAttackMode currentAttackMode,
            float distanceToTarget,
            float currentAttackModeElapsed,
            bool hasTarget,
            bool hasMeleeWeapon,
            bool hasRangedWeapon,
            bool targetIsCastingSkill,
            bool targetIsHurt,
            bool targetIsInPostCastRecovery,
            bool targetThreatening,
            bool hasComboWindow,
            bool shouldRegroup,
            bool shouldInvestigate)
        {
            Config = config;
            CombatMemory = combatMemory;
            CurrentAttackMode = currentAttackMode;
            DistanceToTarget = distanceToTarget;
            CurrentAttackModeElapsed = currentAttackModeElapsed;
            HasTarget = hasTarget;
            HasMeleeWeapon = hasMeleeWeapon;
            HasRangedWeapon = hasRangedWeapon;
            TargetIsCastingSkill = targetIsCastingSkill;
            TargetIsHurt = targetIsHurt;
            TargetIsInPostCastRecovery = targetIsInPostCastRecovery;
            TargetThreatening = targetThreatening;
            HasComboWindow = hasComboWindow;
            ShouldRegroup = shouldRegroup;
            ShouldInvestigate = shouldInvestigate;
        }

        public PlayerCloneAIConfigSO Config { get; }
        public CloneCombatMemory CombatMemory { get; }
        public PlayerAttackMode CurrentAttackMode { get; }
        public float DistanceToTarget { get; }
        public float CurrentAttackModeElapsed { get; }
        public bool HasTarget { get; }
        public bool HasMeleeWeapon { get; }
        public bool HasRangedWeapon { get; }
        public bool TargetIsCastingSkill { get; }
        public bool TargetIsHurt { get; }
        public bool TargetIsInPostCastRecovery { get; }
        public bool TargetThreatening { get; }
        public bool HasComboWindow { get; }
        public bool ShouldRegroup { get; }
        public bool ShouldInvestigate { get; }
    }

    internal sealed class PlayerCloneBrain
    {
        public PlayerAttackMode ResolveAttackModeDecision(in PlayerCloneBrainContext context, out float score)
        {
            score = 0f;
            if (!context.HasRangedWeapon)
                return PlayerAttackMode.Melee;
            if (!context.HasMeleeWeapon)
                return PlayerAttackMode.Ranged;
            if (!context.HasTarget)
                return context.CurrentAttackMode;

            int maxContinuousWholeSeconds = context.Config != null
                ? Mathf.Max(1, Mathf.RoundToInt(context.Config.attackModeMaxContinuousSeconds))
                : 10;
            int elapsedWholeSeconds = Mathf.Max(0, Mathf.FloorToInt(context.CurrentAttackModeElapsed));
            int sampledWholeSeconds = Mathf.Min(elapsedWholeSeconds, maxContinuousWholeSeconds);
            if (sampledWholeSeconds >= maxContinuousWholeSeconds)
            {
                PlayerAttackMode forcedMode = context.CurrentAttackMode == PlayerAttackMode.Melee
                    ? PlayerAttackMode.Ranged
                    : PlayerAttackMode.Melee;
                score = 2.4f;
                return forcedMode;
            }

            float switchChance = sampledWholeSeconds / (float)maxContinuousWholeSeconds;
            if (switchChance <= 0f || Random.value > switchChance)
                return context.CurrentAttackMode;

            score = Mathf.Lerp(0.4f, 2.2f, switchChance);
            return context.CurrentAttackMode == PlayerAttackMode.Melee
                ? PlayerAttackMode.Ranged
                : PlayerAttackMode.Melee;
        }

        public PlayerCloneTacticalMode ResolveTacticalMode(
            in PlayerCloneBrainContext context,
            PlayerAttackMode desiredAttackMode)
        {
            if (context.ShouldRegroup)
                return PlayerCloneTacticalMode.FollowOwner;

            if (!context.HasTarget)
                return context.ShouldInvestigate
                    ? PlayerCloneTacticalMode.Investigate
                    : PlayerCloneTacticalMode.FollowOwner;

            if (desiredAttackMode == PlayerAttackMode.Ranged && context.HasRangedWeapon)
            {
                float rangedExitDistance = context.Config != null
                    ? Mathf.Max(0.5f, context.Config.rangedExitDistance)
                    : 2.8f;
                float preferredRangedDistance = context.Config != null
                    ? Mathf.Max(rangedExitDistance + 0.1f, context.Config.preferredRangedDistance)
                    : 6.5f;
                bool shouldRetreat =
                    context.DistanceToTarget <= rangedExitDistance + 0.35f
                    || context.DistanceToTarget < preferredRangedDistance - 0.75f
                    || context.TargetThreatening;
                return shouldRetreat
                    ? PlayerCloneTacticalMode.RangedRetreat
                    : PlayerCloneTacticalMode.RangedHold;
            }

            return PlayerCloneTacticalMode.MeleePressure;
        }

        public GameAction SelectCombatAction(
            in PlayerCloneBrainContext context,
            float dodgeScore,
            float skillScore,
            float chargeScore,
            float attackScore)
        {
            ApplyDecisionMemoryPenalty(context, CloneCombatDecisionKind.Dodge, ref dodgeScore);
            ApplyDecisionMemoryPenalty(context, CloneCombatDecisionKind.Skill, ref skillScore);
            ApplyDecisionMemoryPenalty(context, CloneCombatDecisionKind.Charge, ref chargeScore);
            ApplyDecisionMemoryPenalty(context, CloneCombatDecisionKind.Attack, ref attackScore);

            GameAction decision = GameAction.None;
            float bestScore = 0f;

            ConsiderDecision(GameAction.Dodge, dodgeScore, ref decision, ref bestScore);
            ConsiderDecision(GameAction.Skill, skillScore, ref decision, ref bestScore);
            ConsiderDecision(GameAction.ChargeStart, chargeScore, ref decision, ref bestScore);
            ConsiderDecision(GameAction.NormalAttack, attackScore, ref decision, ref bestScore);
            return decision;
        }

        private static void ApplyDecisionMemoryPenalty(
            in PlayerCloneBrainContext context,
            CloneCombatDecisionKind decisionKind,
            ref float score)
        {
            if (score <= 0f)
                return;

            float basePenalty = decisionKind switch
            {
                CloneCombatDecisionKind.Dodge => context.Config != null ? Mathf.Max(0f, context.Config.dodgeDecisionPenalty) : 0.18f,
                CloneCombatDecisionKind.Skill => context.Config != null ? Mathf.Max(0f, context.Config.skillDecisionPenalty) : 0.12f,
                CloneCombatDecisionKind.Charge => context.Config != null ? Mathf.Max(0f, context.Config.chargeDecisionPenalty) : 0.15f,
                CloneCombatDecisionKind.Attack => context.HasComboWindow ? 0f : (context.Config != null ? Mathf.Max(0f, context.Config.attackDecisionPenalty) : 0.06f),
                _ => 0f,
            };

            if (basePenalty <= 0f || context.CombatMemory == null)
                return;

            score = Mathf.Max(0f, score - context.CombatMemory.GetRepeatPenalty(decisionKind, basePenalty));
        }

        private static void ConsiderDecision(
            GameAction candidate,
            float candidateScore,
            ref GameAction current,
            ref float bestScore)
        {
            if (candidateScore <= bestScore)
                return;

            current = candidate;
            bestScore = candidateScore;
        }
    }
}
