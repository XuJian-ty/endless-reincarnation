using System.Collections.Generic;
using UnityEngine;
using Game.AI;
using Game;

namespace Game.Data
{
    /// <summary>
    /// 敌人行为配置：身份、感知、巡逻与战术技能槽。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/敌人行为", fileName = "敌人行为_")]
    public class EnemyArchetypeSO : ScriptableObject
    {
        [Header("基础标识")]
        [InspectorLabel("敌人ID")]
        [Tooltip("主键，必须唯一，例如：melee_minion / boss_1")]
        public string enemyId = "";

        [InspectorLabel("显示名称")]
        public string displayName = "";

        [InspectorLabel("敌人类型")]
        [Tooltip("用于类型回退与奖励分组")]
        public EnemyType enemyType = EnemyType.MeleeMinion;

        [InspectorLabel("行为树")]
        [Tooltip("为空时由 EnemyAI 在运行时回退到默认行为树")]
        public BehaviorTreeAsset behaviorTreeAsset;

        [Header("感知（前方扇形发现 + 圆形追击丢失）")]
        [InspectorLabel("扇形视角(度)")]
        [Range(1f, 360f)] public float sectorAngle = 120f;

        [InspectorLabel("扇形感知距离(米)")]
        [Min(0.1f)] public float sectorRange = 15f;

        [InspectorLabel("追击丢失距离(米)")]
        [Min(0.1f)] public float chaseBreakDistance = 25f;

        [Header("巡逻")]
        [InspectorLabel("巡逻半径(米)")]
        [Min(0.1f)] public float patrolRadius = 10f;

        [Header("战术战斗")]
        [InspectorLabel("启用战术战斗")]
        [Tooltip("启用精明决策：靠近/等待/后撤，而不是纯追击")]
        public bool enableTacticalCombat = true;

        [InspectorLabel("追击内圈(米)")]
        [Min(0.1f)] public float chaseInnerDistance = 6f;

        [InspectorLabel("追击外圈(米)")]
        [Min(0.1f)] public float chaseOuterDistance = 12f;

        [InspectorLabel("安全偏移(米)")]
        [Tooltip("在技能施法距离基础上额外保持的安全距离")]
        [Min(0f)] public float preferredSafetyDistanceOffset = 0.75f;

        [InspectorLabel("距离容差(米)")]
        [Min(0.05f)] public float combatDistanceTolerance = 0.9f;

        [InspectorLabel("施法距离容差(米)")]
        [Min(0f)] public float castRangeTolerance = 0.25f;

        [InspectorLabel("后撤步长(米)")]
        [Min(0.1f)] public float retreatStepDistance = 3.5f;

        [InspectorLabel("战术等待时长(秒)")]
        [Tooltip("当距离合适但技能在冷却时，每次决策进入 Idle 的等待时间")]
        [Min(0f)] public float tacticalHoldDuration = 0.12f;

        [InspectorLabel("接近预测提前量(秒)")]
        [Tooltip("玩家移动时，接近目标点的预测时间")]
        [Min(0f)] public float approachLeadTime = 0.2f;

        [InspectorLabel("视线检测间隔(秒)")]
        [Min(0.01f)] public float losProbeInterval = 0.1f;

        [InspectorLabel("路径检测间隔(秒)")]
        [Min(0.02f)] public float pathProbeInterval = 0.3f;

        [InspectorLabel("遮挡惩罚时间(秒)")]
        [Tooltip("目标被障碍遮挡时，为决策时间增加的惩罚项")]
        [Min(0f)] public float losBlockedPenaltySeconds = 0.35f;

        [InspectorLabel("遮挡层级")]
        [Tooltip("用于阻挡视线检测的 Layer")]
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        [InspectorLabel("感知视点高度(米)")]
        [Min(0f)] public float perceptionEyeHeight = 1.2f;

        [Header("技能槽位")]
        [InspectorLabel("技能槽列表")]
        [Tooltip("该敌人可用技能槽位集合")]
        public List<EnemySkillSlotBinding> skillSlots = new List<EnemySkillSlotBinding>();

        public float GetChaseInnerDistance()
        {
            if (chaseInnerDistance > 0.1f)
                return chaseInnerDistance;

            EstimateSkillRangeBounds(out float minRange, out float maxRange);
            bool isBossOrRanged = enemyType == EnemyType.Boss || enemyType == EnemyType.RangedMinion;
            return Mathf.Max(1.5f, Mathf.Min(maxRange, minRange + (isBossOrRanged ? 2.5f : 1.2f)));
        }

        public float GetChaseOuterDistance()
        {
            float inner = GetChaseInnerDistance();
            if (chaseOuterDistance > 0.1f)
                return Mathf.Max(inner + 0.1f, chaseOuterDistance);

            EstimateSkillRangeBounds(out _, out float maxRange);
            bool isBossOrRanged = enemyType == EnemyType.Boss || enemyType == EnemyType.RangedMinion;
            float fallbackOuter = maxRange + (isBossOrRanged ? 3.5f : 2f);
            return Mathf.Max(inner + 0.1f, fallbackOuter);
        }

        public float GetDesiredCombatDistance(float skillRange)
        {
            float targetRange = Mathf.Max(0.1f, skillRange);
            bool isBossOrRanged = enemyType == EnemyType.Boss || enemyType == EnemyType.RangedMinion;
            float safetyOffset = preferredSafetyDistanceOffset > 0f
                ? preferredSafetyDistanceOffset
                : (isBossOrRanged ? 0.9f : 0.25f);
            float desired = targetRange + safetyOffset;
            return Mathf.Clamp(desired, GetChaseInnerDistance(), GetChaseOuterDistance());
        }

        private void EstimateSkillRangeBounds(out float minRange, out float maxRange)
        {
            minRange = float.MaxValue;
            maxRange = 0f;

            if (skillSlots != null)
            {
                for (int i = 0; i < skillSlots.Count; i++)
                {
                    var slot = skillSlots[i];
                    if (slot == null)
                        continue;

                    float range = ResolveSlotRange(slot);
                    minRange = Mathf.Min(minRange, range);
                    maxRange = Mathf.Max(maxRange, range);
                }
            }

            if (minRange == float.MaxValue)
            {
                minRange = 2.5f;
                maxRange = 6f;
            }
        }

        private static float ResolveSlotRange(EnemySkillSlotBinding slot)
        {
            if (slot == null)
                return 3f;

            return slot.castRange > 0.01f ? slot.castRange : 3f;
        }

        public string GetResolvedEnemyId()
        {
            if (!string.IsNullOrWhiteSpace(enemyId))
                return enemyId;
            return enemyType.ToString();
        }

        public int GetSkillSlotCount()
        {
            if (skillSlots == null || skillSlots.Count == 0)
                return 0;

            int maxSlot = -1;
            for (int i = 0; i < skillSlots.Count; i++)
            {
                var slot = skillSlots[i];
                if (slot != null)
                    maxSlot = Mathf.Max(maxSlot, slot.slotIndex);
            }

            return maxSlot + 1;
        }

        public EnemySkillSlotBinding GetSkillSlot(int slot)
        {
            if (slot < 0 || skillSlots == null)
                return null;

            for (int i = 0; i < skillSlots.Count; i++)
            {
                var binding = skillSlots[i];
                if (binding != null && binding.slotIndex == slot)
                    return binding;
            }

            return null;
        }

        public bool IsSkillSlotAvailableInCurrentPhase(int slot, float currentHpRatio)
        {
            var binding = GetSkillSlot(slot);
            if (binding == null)
                return false;

            float hpRatio = Mathf.Clamp01(currentHpRatio);
            return binding.phaseAvailability switch
            {
                EnemySkillPhaseAvailability.Always => true,
                EnemySkillPhaseAvailability.LowHpOnly => hpRatio <= 0.5f,
                EnemySkillPhaseAvailability.HighHpOnly => hpRatio > 0.5f,
                _ => true,
            };
        }
    }
}
