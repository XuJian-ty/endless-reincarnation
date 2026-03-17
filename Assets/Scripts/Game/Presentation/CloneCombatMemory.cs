using UnityEngine;

namespace Game.Presentation
{
    public enum CloneCombatDecisionKind
    {
        None,
        Dodge,
        Skill,
        Charge,
        Attack,
    }

    /// <summary>
    /// 轻量分身战斗记忆：用于保留短暂目标记忆，并抑制重复决策抖动。
    /// </summary>
    public sealed class CloneCombatMemory
    {
        public CloneCombatDecisionKind LastDecisionKind { get; private set; } = CloneCombatDecisionKind.None;
        public int RepeatedDecisionCount { get; private set; }
        public float LastDecisionTime { get; private set; } = -1f;
        public EnemyController LastTarget { get; private set; }
        public Vector3 LastKnownTargetPosition { get; private set; }
        public float LastSeenTargetTime { get; private set; } = -1f;

        public bool HasRecentTargetMemory(float duration)
        {
            return LastSeenTargetTime >= 0f && Time.time - LastSeenTargetTime <= Mathf.Max(0f, duration);
        }

        public void RecordDecision(CloneCombatDecisionKind kind, float now)
        {
            if (kind == CloneCombatDecisionKind.None)
                return;

            if (kind == LastDecisionKind)
                RepeatedDecisionCount++;
            else
                RepeatedDecisionCount = 0;

            LastDecisionKind = kind;
            LastDecisionTime = now;
        }

        public float GetRepeatPenalty(CloneCombatDecisionKind kind, float basePenalty)
        {
            if (basePenalty <= 0f || kind == CloneCombatDecisionKind.None)
                return 0f;

            if (kind != LastDecisionKind || RepeatedDecisionCount <= 0)
                return 0f;

            return basePenalty * RepeatedDecisionCount;
        }

        public void RecordTarget(EnemyController target, Vector3 position, float now)
        {
            LastTarget = target;
            LastKnownTargetPosition = position;
            LastSeenTargetTime = now;
        }

        public void ClearTarget()
        {
            LastTarget = null;
        }

        public void Clear()
        {
            LastDecisionKind = CloneCombatDecisionKind.None;
            RepeatedDecisionCount = 0;
            LastDecisionTime = -1f;
            LastTarget = null;
            LastKnownTargetPosition = Vector3.zero;
            LastSeenTargetTime = -1f;
        }
    }
}
