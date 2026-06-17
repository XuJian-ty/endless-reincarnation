using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "游戏/配置/分身AI配置", fileName = "分身AI配置")]
    public class PlayerCloneAIConfigSO : ScriptableObject
    {
        [Header("属性倍率")]
        [InspectorLabel("生命上限倍率")]
        [Min(0f)] public float maxHpScale = 10f;

        [InspectorLabel("法力上限倍率")]
        [Min(0f)] public float maxMpScale = 1f;

        [InspectorLabel("攻击倍率")]
        [Min(0f)] public float attackScale = 0.5f;

        [InspectorLabel("防御倍率")]
        [Min(0f)] public float defenseScale = 1f;

        [InspectorLabel("吸血倍率")]
        [Min(0f)] public float lifeStealScale = 1f;

        [InspectorLabel("暴击率倍率")]
        [Min(0f)] public float critRateScale = 1f;

        [InspectorLabel("暴击伤害倍率")]
        [Min(0f)] public float critDmgScale = 1f;

        [InspectorLabel("攻速倍率")]
        [Min(0f)] public float attackSpeedScale = 1f;

        [InspectorLabel("移速倍率")]
        [Min(0f)] public float moveSpeedScale = 1f;

        [InspectorLabel("生命回复倍率")]
        [Min(0f)] public float hpRegenScale = 1f;

        [InspectorLabel("法力回复倍率")]
        [Min(0f)] public float mpRegenScale = 1f;

        [InspectorLabel("增伤倍率")]
        [Min(0f)] public float damageBonusScale = 1f;

        [InspectorLabel("减伤倍率")]
        [Min(0f)] public float damageReduceScale = 1f;

        [Header("决策")]
        [InspectorLabel("目标刷新间隔(秒)")]
        [Tooltip("重新搜索/校验目标的间隔。更小会更快换目标，但也更容易抖动。")]
        [Min(0.02f)] public float targetRefreshInterval = 0.12f;

        [InspectorLabel("重新决策间隔(秒)")]
        [Tooltip("分身重新评估动作的间隔，同时也会影响战斗站位点的刷新频率。")]
        [Min(0f)] public float decisionInterval = 0.06f;

        [InspectorLabel("玩家牵引半径(米)")]
        [Tooltip("敌人离玩家超过这个距离时，分身不会把它当成有效战斗目标。")]
        [Min(1f)] public float ownerLeashDistance = 22f;

        [InspectorLabel("强制回位距离(米)")]
        [Tooltip("分身离玩家超过这个距离时，会优先回到玩家身边，而不是继续缠斗。")]
        [Min(1f)] public float regroupDistance = 9f;

        [InspectorLabel("敌人感知半径(米)")]
        [Tooltip("分身从自身位置出发，最多会搜索多远的敌人。")]
        [Min(1f)] public float perceptionRadius = 12f;

        [InspectorLabel("当前目标保留权重")]
        [Tooltip("给当前目标一点额外留恋，避免频繁切换目标。值越大越不爱换目标。")]
        [Range(0f, 1f)] public float targetStickiness = 0.18f;

        [InspectorLabel("目标记忆时长(秒)")]
        [Tooltip("短暂丢失目标后，分身还会继续朝最后已知位置调查多久。")]
        [Min(0f)] public float targetMemoryDuration = 0.9f;

        [Header("控距")]
        [InspectorLabel("绕侧站位半径(米)")]
        [Tooltip("分身绕侧时，目标周围站位圈的半径。")]
        [Min(0.5f)] public float orbitDistance = 1.9f;

        [InspectorLabel("绕侧偏转角度(度)")]
        [Tooltip("分身绕到目标侧面的基础角度。值越大，站位越偏向目标侧后方。")]
        [Range(5f, 120f)] public float orbitAngle = 42f;

        [InspectorLabel("站位到位阈值(米)")]
        [Tooltip("离当前战斗站位点足够近时，就视为已经到位。")]
        [Min(0.05f)] public float orbitArrivalDistance = 0.2f;

        [InspectorLabel("跟随/巡逻移速倍率")]
        [Tooltip("没有明确战斗动作时，跟随玩家或巡逻调查时的移动速度倍率。")]
        [Min(0.1f)] public float patrolMoveSpeedRatio = 0.58f;

        [InspectorLabel("战斗走路移速倍率")]
        [Tooltip("战斗中小步调整站位、调查或慢速接近时的移动速度倍率。")]
        [Min(0.1f)] public float combatWalkSpeedRatio = 0.72f;

        [InspectorLabel("追击跑步速度倍率")]
        [Tooltip("需要明显拉近距离时的跑步追击速度倍率。")]
        [Min(0.1f)] public float combatRunSpeedRatio = 1f;

        [InspectorLabel("跑步追击阈值(米)")]
        [Tooltip("当与目标距离超过“理想站位距离 + 容差 + 该阈值”时，分身会按跑步追击处理。")]
        [Min(0.1f)] public float chaseDistanceThreshold = 1.6f;

        [Header("动作范围")]
        [InspectorLabel("近战作战距离(米)")]
        [Tooltip("分身近战时的核心距离参数：同时用于近战站位和普攻起手。站位容差、过近判定会由它自动推导。")]
        [Min(0.5f)] public float attackCastRange = 2.7f;

        [InspectorLabel("主动技能起手距离(米)")]
        [Tooltip("主动技能的统一距离门槛。超过这个距离时不会考虑主动技能。")]
        [Min(0.5f)] public float skillCastRange = 8f;

        [InspectorLabel("威胁判定距离(米)")]
        [Tooltip("敌人技能、压上和威胁判断会参考这个距离；它同时影响闪避与退避判断。")]
        [Min(0.5f)] public float dodgeThreatDistance = 4.2f;

        [InspectorLabel("远程贴身退避距离(米)")]
        [Tooltip("敌人贴得太近时，分身会优先后撤；持续远程动作在这个距离内也会结束。")]
        [Min(0.5f)] public float rangedExitDistance = 2.8f;

        [InspectorLabel("远程理想站位距离(米)")]
        [Tooltip("分身远程作战时想保持的输出距离。主要用于控位，不直接限制能不能起手。")]
        [Min(0.5f)] public float preferredRangedDistance = 6.5f;

        [InspectorLabel("远程最远起手距离(米)")]
        [Tooltip("远程普攻/远程蓄力最远能从多远开始或继续。超过这个距离就不会起手，持续动作也会结束。")]
        [Min(0.5f)] public float rangedAttackCastRange = 9f;

        [Header("动作节奏")]
        [InspectorLabel("蓄力保持最短时长(秒)")]
        [Tooltip("近战蓄力进入 Loop 后，至少保持多久再释放。")]
        [Min(0f)] public float chargeHoldMinSeconds = 0.18f;

        [InspectorLabel("蓄力保持最长时长(秒)")]
        [Tooltip("近战蓄力进入 Loop 后，最多保持多久就会释放。远程蓄力也会参考它来推导更长的爆发蓄力时间。")]
        [Min(0f)] public float chargeHoldMaxSeconds = 0.4f;

        [InspectorLabel("单形态最长连续作战时长(秒)")]
        [Tooltip("同一形态持续作战太久后，会提高切换概率；达到上限时会强制尝试切换。")]
        [Min(1f)] public float attackModeMaxContinuousSeconds = 10f;

        [Header("倾向")]
        [InspectorLabel("近战普攻倾向")]
        [Tooltip("主要影响近战普攻的出手积极度，不直接决定近战/远程形态切换。")]
        [Range(0f, 1f)] public float aggression = 0.82f;

        [InspectorLabel("闪避倾向")]
        [Tooltip("越高越容易在威胁出现时优先闪避。")]
        [Range(0f, 1f)] public float dodgeBias = 0.72f;

        [InspectorLabel("技能倾向")]
        [Tooltip("越高越愿意把主动技能放进当前动作候选。")]
        [Range(0f, 1f)] public float skillBias = 0.78f;

        [InspectorLabel("蓄力倾向")]
        [Tooltip("越高越愿意选择蓄力攻击。")]
        [Range(0f, 1f)] public float chargeBias = 0.68f;

        [InspectorLabel("远程动作倾向")]
        [Tooltip("影响远程普攻和远程蓄力的动作打分，不直接决定近战/远程形态切换。")]
        [Range(0f, 1f)] public float rangedBias = 0.22f;

        [Header("决策惩罚")]
        [InspectorLabel("重复闪避惩罚")]
        [Tooltip("连续多次选择闪避时，后续闪避评分会额外扣分。")]
        [Min(0f)] public float dodgeDecisionPenalty = 0.18f;

        [InspectorLabel("重复技能惩罚")]
        [Tooltip("连续多次选择主动技能时，后续技能评分会额外扣分。")]
        [Min(0f)] public float skillDecisionPenalty = 0.12f;

        [InspectorLabel("重复蓄力惩罚")]
        [Tooltip("连续多次选择蓄力时，后续蓄力评分会额外扣分。")]
        [Min(0f)] public float chargeDecisionPenalty = 0.15f;

        [InspectorLabel("重复普攻惩罚")]
        [Tooltip("连续多次选择普攻时，后续普攻评分会额外扣分。")]
        [Min(0f)] public float attackDecisionPenalty = 0.06f;
    }
}
