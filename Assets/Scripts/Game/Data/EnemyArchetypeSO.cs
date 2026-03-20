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

        [InspectorLabel("巡逻回血百分比/秒")]
        [Tooltip("敌人处于巡逻状态时，每秒按最大生命百分比回复。0.05 代表每秒回复 5% 最大生命。")]
        [Min(0f)] public float patrolHealPercentPerSecond = 0.05f;

        [Header("战术战斗")]
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

        [InspectorLabel("接近预测提前量(秒)")]
        [Tooltip("玩家移动时，接近目标点的预测时间")]
        [Min(0f)] public float approachLeadTime = 0.2f;

        [InspectorLabel("视线检测间隔(秒)")]
        [Min(0.01f)] public float losProbeInterval = 0.1f;

        [InspectorLabel("路径检测间隔(秒)")]
        [Min(0.02f)] public float pathProbeInterval = 0.3f;

        [InspectorLabel("遮挡层级")]
        [Tooltip("用于阻挡视线检测的 Layer")]
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        [InspectorLabel("感知视点高度(米)")]
        [Min(0f)] public float perceptionEyeHeight = 1.2f;

        [Header("战斗人格")]
        [InspectorLabel("反应最短延迟(秒)")]
        [Tooltip("敌人在重新思考前至少要等待多久。更小会更灵敏，但也更像读输入。")]
        [Min(0f)] public float reactionMinSeconds = 0.08f;

        [InspectorLabel("反应最长延迟(秒)")]
        [Tooltip("敌人在安全情况下重新决策的最长延迟。建议只比最短延迟略高。")]
        [Min(0f)] public float reactionMaxSeconds = 0.18f;

        [InspectorLabel("最短承诺时长(秒)")]
        [Tooltip("一旦决定接近、绕步、闪避或压制，至少坚持这么久再重新评估，避免每帧来回改主意。")]
        [Min(0f)] public float decisionCommitSeconds = 0.22f;

        [InspectorLabel("目标记忆时长(秒)")]
        [Tooltip("短暂丢失视野后，敌人还会继续搜索玩家多久。")]
        [Min(0f)] public float searchMemoryDuration = 1.6f;

        [InspectorLabel("进攻倾向")]
        [Tooltip("越高越喜欢主动压上、抢回合和把距离压进来。")]
        [Range(0f, 1f)] public float aggression = 0.55f;

        [InspectorLabel("谨慎倾向")]
        [Tooltip("越高越在意风险、会更频繁持距、后撤和等待。")]
        [Range(0f, 1f)] public float caution = 0.45f;

        [InspectorLabel("闪避倾向")]
        [Tooltip("越高越容易在感知到威胁时优先选择 Dodge/Reposition，而不是硬顶。")]
        [Range(0f, 1f)] public float dodgeBias = 0.5f;

        [InspectorLabel("抓后摇倾向")]
        [Tooltip("越高越会主动读玩家空档，抢惩罚窗口。")]
        [Range(0f, 1f)] public float punishBias = 0.55f;

        [InspectorLabel("绕步倾向")]
        [Tooltip("越高越爱左右绕步，不会总站桩或只会直线追。")]
        [Range(0f, 1f)] public float strafeBias = 0.45f;

        [Header("受击与协同")]
        [InspectorLabel("最大压制人数")]
        [Tooltip("同时允许多少个同阵营敌人进入正面压制/抢回合状态，其他敌人会更倾向绕步、持距或等待。")]
        [Min(1)] public int maxPressureAllies = 2;

        [InspectorLabel("最大韧性")]
        [Tooltip("韧性归零才会触发明显破韧；值越高越不容易被连续小招打成木桩。")]
        [Min(0f)] public float poiseMax = 24f;

        [InspectorLabel("韧性恢复/秒")]
        [Tooltip("脱离连续受击后，每秒恢复多少韧性。")]
        [Min(0f)] public float poiseRecoveryPerSecond = 10f;

        [InspectorLabel("破韧硬直时长(秒)")]
        [Tooltip("韧性被打空时，敌人会进入多长时间的硬直。")]
        [Min(0f)] public float poiseBreakStunDuration = 0.35f;

        [InspectorLabel("允许强击轻硬直")]
        [Tooltip("开启后，伤害较大的单次攻击即使没有打空韧性，也可能造成一次短硬直。")]
        public bool allowLightHitFlinch = true;

        [InspectorLabel("施法时霸体")]
        [Tooltip("开启后，敌人在施法过程中不会被普通受击直接打断，但韧性依然会被消耗。")]
        public bool superArmorWhileCasting;

        [Header("动画自然退出")]
        [InspectorLabel("受击自然退出进度")]
        [Tooltip("一次性受击动画播放到该进度后，立即触发自然退出默认目标。动画仅负责表现，不影响受击逻辑。")]
        [Range(0f, 1f)] public float hurtNaturalExitNormalizedTime = 0.9f;

        [InspectorLabel("受击自然退出默认目标")]
        [Tooltip("受击动画自然退出时要切回的循环状态。当前通常使用 Locomotion。")]
        public EnemyAnimationNaturalExitTarget hurtNaturalExitTarget = EnemyAnimationNaturalExitTarget.Locomotion;

        [Header("循环状态技能")]
        [InspectorLabel("Locomotion技能ID")]
        [Tooltip("敌人进入 Locomotion 循环状态时，自动从技能库启动一次该技能时间轴。留空则不触发。")]
        public string locomotionSkillId = "EnemyLocomotion";

        [Header("技能作者化")]
        [InspectorLabel("专属动画控制器")]
        [Tooltip("敌人技能一键添加/删除时要同步更新的 AnimatorController。建议每个敌人使用独立控制器。")]
        public RuntimeAnimatorController dedicatedAnimatorController;

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

        public float GetRandomReactionDelay()
        {
            float min = Mathf.Max(0f, reactionMinSeconds);
            float max = Mathf.Max(min, reactionMaxSeconds);
            return Random.Range(min, max);
        }

        public float GetDecisionCommitDuration()
        {
            return Mathf.Max(0f, decisionCommitSeconds);
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
