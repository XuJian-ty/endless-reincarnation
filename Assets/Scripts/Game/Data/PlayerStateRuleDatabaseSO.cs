using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Presentation;

namespace Game.Data
{
    public enum PlayerStateNaturalExitTarget
    {
        [InspectorName("无")]
        None,
        [InspectorName("IdleState")]
        IdleState,
        [InspectorName("ChargeLoopState")]
        ChargeLoopState,
        [InspectorName("FallState")]
        FallState,
        [InspectorName("FallAttackLoopState")]
        FallAttackLoopState,
    }

    [Serializable]
    public class PlayerStateActionPolicyRule
    {
        [InspectorLabel("动作")]
        public GameAction action = GameAction.None;

        [InspectorLabel("策略")]
        public TransitionPolicy policy = TransitionPolicy.Ignore;
    }

    [Serializable]
    public class PlayerStatePendingReleaseRule
    {
        [InspectorLabel("缓存动作")]
        public GameAction pendingAction = GameAction.None;

        [InspectorLabel("释放进度")]
        [Tooltip("当前状态动画播放到该进度时，若缓存动作匹配，则允许提前释放缓存。")]
        [Range(0f, 1f)]
        public float normalizedTime = 0.9f;
    }

    [Serializable]
    public class PlayerStateRuleEntry
    {
        [InspectorLabel("状态 ID")]
        [Tooltip("默认填写状态类名，例如 IdleState、Attack0State、Skill0State。")]
        public string stateId = "";

        [InspectorLabel("动作策略表")]
        public List<PlayerStateActionPolicyRule> actionPolicies = new List<PlayerStateActionPolicyRule>();

        [InspectorLabel("缓存释放规则")]
        public List<PlayerStatePendingReleaseRule> pendingReleaseRules = new List<PlayerStatePendingReleaseRule>();

        [InspectorLabel("覆盖自然退出进度")]
        public bool overrideNaturalExitNormalizedTime;

        [InspectorLabel("自然退出进度")]
        [Tooltip("仅对一次性动画自然结束类状态生效。")]
        [Range(0f, 1f)]
        public float naturalExitNormalizedTime = 0.9f;

        [InspectorLabel("自然退出默认目标")]
        [Tooltip("当前状态自然结束且没有预输入时，默认切到哪个状态。")]
        public PlayerStateNaturalExitTarget naturalExitTarget = PlayerStateNaturalExitTarget.None;

        public bool TryGetPolicy(GameAction action, out TransitionPolicy policy)
        {
            if (actionPolicies != null)
            {
                for (int i = 0; i < actionPolicies.Count; i++)
                {
                    PlayerStateActionPolicyRule rule = actionPolicies[i];
                    if (rule != null && rule.action == action)
                    {
                        policy = rule.policy;
                        return true;
                    }
                }
            }

            policy = TransitionPolicy.Ignore;
            return false;
        }

        public bool TryGetPendingReleaseThreshold(GameAction action, out float threshold)
        {
            if (pendingReleaseRules != null)
            {
                for (int i = 0; i < pendingReleaseRules.Count; i++)
                {
                    PlayerStatePendingReleaseRule rule = pendingReleaseRules[i];
                    if (rule != null && rule.pendingAction == action)
                    {
                        threshold = rule.normalizedTime;
                        return true;
                    }
                }
            }

            threshold = 0f;
            return false;
        }
    }

    [CreateAssetMenu(menuName = "游戏/配置/玩家状态规则", fileName = "玩家状态规则")]
    public class PlayerStateRuleDatabaseSO : ScriptableObject
    {
        [InspectorLabel("状态规则列表")]
        public List<PlayerStateRuleEntry> entries = new List<PlayerStateRuleEntry>();

        public PlayerStateRuleEntry GetEntry(string stateId)
        {
            if (entries == null || string.IsNullOrWhiteSpace(stateId))
                return null;

            string normalized = stateId.Trim();
            for (int i = 0; i < entries.Count; i++)
            {
                PlayerStateRuleEntry entry = entries[i];
                if (entry != null && string.Equals(entry.stateId, normalized, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        public bool TryGetPolicy(string stateId, GameAction action, out TransitionPolicy policy)
        {
            PlayerStateRuleEntry entry = GetEntry(stateId);
            if (entry != null)
                return entry.TryGetPolicy(action, out policy);

            policy = TransitionPolicy.Ignore;
            return false;
        }

        public bool TryGetPendingReleaseThreshold(string stateId, GameAction action, out float threshold)
        {
            PlayerStateRuleEntry entry = GetEntry(stateId);
            if (entry != null)
                return entry.TryGetPendingReleaseThreshold(action, out threshold);

            threshold = 0f;
            return false;
        }

        public bool TryGetNaturalExitThreshold(string stateId, out float threshold)
        {
            PlayerStateRuleEntry entry = GetEntry(stateId);
            if (entry != null && entry.overrideNaturalExitNormalizedTime)
            {
                threshold = entry.naturalExitNormalizedTime;
                return true;
            }

            threshold = 0f;
            return false;
        }

        public PlayerStateNaturalExitTarget GetNaturalExitTarget(string stateId)
        {
            PlayerStateRuleEntry entry = GetEntry(stateId);
            return entry != null ? entry.naturalExitTarget : PlayerStateNaturalExitTarget.None;
        }
    }
}
