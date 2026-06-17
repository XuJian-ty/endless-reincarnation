using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 状态对某个 GameAction 请求的响应策略。
    /// 由 PlayerStateBase.GetPolicyFor() 返回，PlayerStateMachine 据此决定行为。
    /// </summary>
    public enum TransitionPolicy
    {
        /// <summary>立即打断当前状态，切换到对应目标状态</summary>
        [InspectorName("打断")]
        Interrupt,

        /// <summary>缓存为预输入（最多一条，后来的覆盖先来的），在当前状态自然结束时执行</summary>
        [InspectorName("缓存")]
        Buffer,

        /// <summary>丢弃，不做任何处理</summary>
        [InspectorName("忽略")]
        Ignore,
    }
}
