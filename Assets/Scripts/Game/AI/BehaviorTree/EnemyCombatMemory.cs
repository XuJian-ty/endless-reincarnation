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
        public int RepeatedMobilityDecisionCount { get; private set; }
        public int LastStrafeSign { get; private set; }
        public int RepeatedSameStrafeSignCount { get; private set; }
        public float LastDecisionTime { get; private set; } = -1f;
        public float NextDecisionTime { get; private set; }
        public bool HasSearchAnchor { get; private set; }
        public Vector3 SearchAnchor { get; private set; }
        public int SearchStep { get; private set; }
        private int _preferredStrafeSign;
        private float _nextStrafePreferenceRefreshTime;

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
            bool isMobilityIntent = IsMobilityIntent(intent.Type);
            int currentStrafeSign = ResolveStrafeSign(intent.Type);
            if (isMobilityIntent && IsMobilityIntent(LastIntentType))
                RepeatedMobilityDecisionCount++;
            else
                RepeatedMobilityDecisionCount = 0;

            if (currentStrafeSign != 0)
            {
                if (currentStrafeSign == LastStrafeSign)
                    RepeatedSameStrafeSignCount++;
                else
                    RepeatedSameStrafeSignCount = 0;

                LastStrafeSign = currentStrafeSign;
            }
            else if (!isMobilityIntent)
            {
                RepeatedSameStrafeSignCount = 0;
            }

            if (intent.Type == LastIntentType && skillSlot == LastSkillSlot)
                RepeatedDecisionCount++;
            else
                RepeatedDecisionCount = 0;

            LastIntentType = intent.Type;
            LastSkillSlot = skillSlot;
            LastDecisionTime = now;

            if (isMobilityIntent)
                ScheduleNextStrafePreferenceRefresh(now);
        }

        public float GetRepeatPenalty(float basePenalty)
        {
            if (RepeatedDecisionCount <= 0 || basePenalty <= 0f)
                return 0f;

            return basePenalty * RepeatedDecisionCount;
        }

        public float GetIntentRepeatPenalty(EnemyIntentType intentType, float basePenalty)
        {
            if (intentType == EnemyIntentType.None || intentType != LastIntentType)
                return 0f;

            return GetRepeatPenalty(basePenalty);
        }

        public int ResolvePreferredStrafeSign(int fallbackSign, float now)
        {
            int normalizedFallback = fallbackSign < 0 ? -1 : 1;
            if (_preferredStrafeSign == 0)
            {
                _preferredStrafeSign = Random.value < 0.5f ? normalizedFallback : -normalizedFallback;
                ScheduleNextStrafePreferenceRefresh(now);
                return _preferredStrafeSign;
            }

            if (now >= _nextStrafePreferenceRefreshTime)
            {
                if (LastStrafeSign != 0)
                {
                    bool forceFlip = RepeatedSameStrafeSignCount >= 1 || RepeatedMobilityDecisionCount >= 2;
                    bool shouldFlip = forceFlip || Random.value < 0.7f;
                    _preferredStrafeSign = shouldFlip ? -LastStrafeSign : LastStrafeSign;
                }
                else
                {
                    bool shouldFlip = RepeatedMobilityDecisionCount >= 2 || Random.value < 0.4f;
                    _preferredStrafeSign = shouldFlip ? -_preferredStrafeSign : normalizedFallback;
                }

                ScheduleNextStrafePreferenceRefresh(now);
            }

            return _preferredStrafeSign;
        }

        public float GetMobilityVariationPressure()
        {
            if (RepeatedMobilityDecisionCount <= 0)
                return 0f;

            return Mathf.Clamp01(RepeatedMobilityDecisionCount / 3f);
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
            RepeatedMobilityDecisionCount = 0;
            LastStrafeSign = 0;
            RepeatedSameStrafeSignCount = 0;
            LastDecisionTime = -1f;
            NextDecisionTime = 0f;
            _preferredStrafeSign = 0;
            _nextStrafePreferenceRefreshTime = 0f;
            ResetSearch();
        }

        private void ScheduleNextStrafePreferenceRefresh(float now)
        {
            _nextStrafePreferenceRefreshTime = now + Random.Range(0.55f, 1.35f);
        }

        private static bool IsMobilityIntent(EnemyIntentType intentType)
        {
            return intentType == EnemyIntentType.StrafeLeft ||
                   intentType == EnemyIntentType.StrafeRight ||
                   intentType == EnemyIntentType.Retreat ||
                   intentType == EnemyIntentType.Reposition;
        }

        private static int ResolveStrafeSign(EnemyIntentType intentType)
        {
            if (intentType == EnemyIntentType.StrafeRight)
                return 1;
            if (intentType == EnemyIntentType.StrafeLeft)
                return -1;
            return 0;
        }
    }
}
