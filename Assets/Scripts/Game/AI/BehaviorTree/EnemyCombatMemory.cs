using Game.Presentation;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Persisted enemy combat memory used to throttle decisions and penalize repetition.
    /// </summary>
    public sealed class EnemyCombatMemory
    {
        public EnemyIntentType LastIntentType { get; private set; } = EnemyIntentType.None;
        public int LastSkillSlot { get; private set; } = -1;
        public int RepeatedDecisionCount { get; private set; }
        public float LastDecisionTime { get; private set; } = -1f;
        public float NextDecisionTime { get; private set; }
        public bool HasSearchAnchor { get; private set; }
        public Vector3 SearchAnchor { get; private set; }
        public int SearchStep { get; private set; }

        public bool CanEvaluate(float now)
        {
            return now >= NextDecisionTime;
        }

        public void ScheduleNextEvaluation(float now, float reactionDelay, float commitDuration)
        {
            float delay = reactionDelay > commitDuration ? reactionDelay : commitDuration;
            NextDecisionTime = now + (delay > 0f ? delay : 0f);
        }

        public void RecordDecision(EnemyIntent intent, float now)
        {
            int skillSlot = intent.Type == EnemyIntentType.CastSkill ? intent.SkillSlot : -1;
            if (intent.Type == LastIntentType && skillSlot == LastSkillSlot)
                RepeatedDecisionCount++;
            else
                RepeatedDecisionCount = 0;

            LastIntentType = intent.Type;
            LastSkillSlot = skillSlot;
            LastDecisionTime = now;
        }

        public float GetRepeatPenalty(float basePenalty)
        {
            if (RepeatedDecisionCount <= 0 || basePenalty <= 0f)
                return 0f;

            return basePenalty * RepeatedDecisionCount;
        }

        public void SyncSearchAnchor(Vector3 anchor)
        {
            if (!HasSearchAnchor || (anchor - SearchAnchor).sqrMagnitude > 1f)
            {
                HasSearchAnchor = true;
                SearchAnchor = anchor;
                SearchStep = 0;
                return;
            }

            SearchAnchor = anchor;
        }

        public void AdvanceSearchStep()
        {
            if (!HasSearchAnchor)
                return;

            if (SearchStep < 3)
                SearchStep++;
        }

        public void ResetSearch()
        {
            HasSearchAnchor = false;
            SearchAnchor = Vector3.zero;
            SearchStep = 0;
        }

        public void Clear()
        {
            LastIntentType = EnemyIntentType.None;
            LastSkillSlot = -1;
            RepeatedDecisionCount = 0;
            LastDecisionTime = -1f;
            NextDecisionTime = 0f;
            ResetSearch();
        }
    }
}
