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
        private const float DefaultDecisionCommitSeconds = 0.24f;

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
        [InspectorLabel("步行追击阈值(米)")]
        [Tooltip("与目标距离小于等于该值时，追击移动使用走路；大于该值时使用跑步。")]
        [Min(0.1f)] public float chaseInnerDistance = 2.5f;

        [InspectorLabel("战术位移步长(米)")]
        [Tooltip("战术移动时的一步长度。它会同时影响后撤、绕步横移，以及施法后恢复时拉开的距离。")]
        [Min(0.1f)] public float retreatStepDistance = 3.5f;

        [InspectorLabel("接近预测提前量(秒)")]
        [Tooltip("玩家移动时，接近目标点的预测时间")]
        [Min(0f)] public float approachLeadTime = 0.2f;

        [InspectorLabel("感知视点高度(米)")]
        [Tooltip("视线检测和感知射线使用的视点高度。体型更高的敌人可以适当调大。")]
        [Min(0f)] public float perceptionEyeHeight = 1.6f;

        [Header("战斗人格")]
        [InspectorLabel("反应延迟(秒)")]
        [Tooltip("敌人每次重新评估决策前的等待时间。更小会更灵敏，更大则更迟缓。")]
        [Min(0f)] public float reactionDelaySeconds = 0.12f;

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
            return Mathf.Max(0.1f, chaseInnerDistance);
        }

        public float GetDesiredCombatDistance(float skillRange)
        {
            float targetRange = Mathf.Max(0.1f, skillRange);
            float maxCombatDistance = Mathf.Max(0.2f, chaseBreakDistance - 0.1f);
            return Mathf.Clamp(targetRange, 0.1f, maxCombatDistance);
        }

        public float GetRandomReactionDelay()
        {
            return Mathf.Max(0f, reactionDelaySeconds);
        }

        public float GetDecisionCommitDuration()
        {
            return DefaultDecisionCommitSeconds;
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
