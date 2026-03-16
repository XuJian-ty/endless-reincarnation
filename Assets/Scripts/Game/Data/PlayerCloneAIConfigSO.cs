using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "游戏/配置/分身AI配置", fileName = "分身AI配置")]
    public class PlayerCloneAIConfigSO : ScriptableObject
    {
        [Header("决策")]
        [InspectorLabel("目标刷新间隔(秒)")]
        [Min(0.02f)] public float targetRefreshInterval = 0.12f;

        [InspectorLabel("决策间隔(秒)")]
        [Min(0f)] public float decisionInterval = 0.06f;

        [InspectorLabel("玩家牵引半径(米)")]
        [Min(1f)] public float ownerLeashDistance = 22f;

        [InspectorLabel("强制回位距离(米)")]
        [Min(1f)] public float regroupDistance = 9f;

        [Header("控距")]
        [InspectorLabel("理想近战距离(米)")]
        [Min(0.5f)] public float preferredMeleeDistance = 2.25f;

        [InspectorLabel("近战距离容差(米)")]
        [Min(0.05f)] public float meleeDistanceTolerance = 0.35f;

        [InspectorLabel("过近距离(米)")]
        [Min(0.3f)] public float tooCloseDistance = 1.35f;

        [InspectorLabel("绕侧半径(米)")]
        [Min(0.5f)] public float orbitDistance = 1.9f;

        [InspectorLabel("绕侧角度(度)")]
        [Range(5f, 120f)] public float orbitAngle = 42f;

        [InspectorLabel("到位阈值(米)")]
        [Min(0.05f)] public float orbitArrivalDistance = 0.2f;

        [InspectorLabel("战斗移动速度倍率")]
        [Min(0.1f)] public float combatMoveSpeedRatio = 0.96f;

        [Header("动作范围")]
        [InspectorLabel("普攻触发距离(米)")]
        [Min(0.5f)] public float attackCastRange = 2.7f;

        [InspectorLabel("主动技能触发距离(米)")]
        [Min(0.5f)] public float skillCastRange = 8f;

        [InspectorLabel("蓄力起手距离(米)")]
        [Min(0.5f)] public float chargeEnterRange = 4.8f;

        [InspectorLabel("闪避威胁距离(米)")]
        [Min(0.5f)] public float dodgeThreatDistance = 4.2f;

        [Header("动作节奏")]
        [InspectorLabel("闪避冷却(秒)")]
        [Min(0f)] public float dodgeCooldownSeconds = 1.2f;

        [InspectorLabel("闪避位移(米)")]
        [Min(0.2f)] public float dodgeDistance = 3.5f;

        [InspectorLabel("闪避持续(秒)")]
        [Min(0.05f)] public float dodgeDuration = 0.22f;

        [InspectorLabel("蓄力本地冷却(秒)")]
        [Min(0f)] public float chargeCooldownSeconds = 3f;

        [InspectorLabel("蓄力保持最短时长(秒)")]
        [Min(0f)] public float chargeHoldMinSeconds = 0.18f;

        [InspectorLabel("蓄力保持最长时长(秒)")]
        [Min(0f)] public float chargeHoldMaxSeconds = 0.4f;

        [Header("倾向")]
        [InspectorLabel("进攻倾向")]
        [Range(0f, 1f)] public float aggression = 0.82f;

        [InspectorLabel("闪避倾向")]
        [Range(0f, 1f)] public float dodgeBias = 0.72f;

        [InspectorLabel("技能倾向")]
        [Range(0f, 1f)] public float skillBias = 0.78f;

        [InspectorLabel("蓄力倾向")]
        [Range(0f, 1f)] public float chargeBias = 0.68f;

        [InspectorLabel("连段倾向")]
        [Range(0f, 1f)] public float comboBias = 0.72f;

        [InspectorLabel("绕侧倾向")]
        [Range(0f, 1f)] public float orbitBias = 0.85f;
    }
}
