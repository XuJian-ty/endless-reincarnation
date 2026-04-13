using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 统一封装 Animator 当前状态的归一化时间判定，供玩家分身/敌人的执行层复用。
    /// </summary>
    public static class AnimatorStateTimingUtility
    {
        public static bool TryGetCurrentStateInfo(Animator animator, int layerIndex, out AnimatorStateInfo info)
        {
            info = default;
            if (animator == null || animator.IsInTransition(layerIndex))
                return false;

            info = animator.GetCurrentAnimatorStateInfo(layerIndex);
            return true;
        }

        public static bool IsCurrentStatePastNormalizedTime(Animator animator, int layerIndex, float threshold)
        {
            if (!TryGetCurrentStateInfo(animator, layerIndex, out AnimatorStateInfo info) || info.loop)
                return false;

            return info.normalizedTime >= Mathf.Clamp01(threshold);
        }

        public static bool TryGetCurrentNonLoopNormalizedTime(Animator animator, int layerIndex, out float normalizedTime)
        {
            normalizedTime = -1f;
            if (!TryGetCurrentStateInfo(animator, layerIndex, out AnimatorStateInfo info) || info.loop)
                return false;

            normalizedTime = Mathf.Clamp01(info.normalizedTime);
            return true;
        }

        public static bool IsCurrentStateLooping(Animator animator, int layerIndex)
        {
            return TryGetCurrentStateInfo(animator, layerIndex, out AnimatorStateInfo info) && info.loop;
        }
    }
}
