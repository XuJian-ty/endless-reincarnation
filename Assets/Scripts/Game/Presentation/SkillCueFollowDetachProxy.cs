using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 让技能特效在世界根节点下持续跟随锚点，
    /// 并可在指定跟随时长结束后停止跟随、保留当前世界位置与旋转。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillCueFollowDetachProxy : MonoBehaviour
    {
        private Transform _followTarget;
        private Vector3 _followLocalOffset;
        private Quaternion _followLocalRotation = Quaternion.identity;
        private float _remainingSeconds;
        private bool _useFollowDuration;
        private bool _isConfigured;

        public void Configure(Transform followTarget, Vector3 localOffset, Quaternion localRotation, bool useFollowDuration, float followDuration)
        {
            _followTarget = followTarget;
            _followLocalOffset = localOffset;
            _followLocalRotation = localRotation;
            _useFollowDuration = useFollowDuration;
            _remainingSeconds = Mathf.Max(0f, followDuration);
            _isConfigured = true;
            enabled = true;

            ApplyFollowTransform();
            if (_useFollowDuration && _remainingSeconds <= 0f)
                StopFollowing();
        }

        private void LateUpdate()
        {
            if (!_isConfigured)
                return;

            ApplyFollowTransform();

            if (!_useFollowDuration)
                return;

            if (_remainingSeconds > 0f)
            {
                _remainingSeconds -= Time.deltaTime;
                if (_remainingSeconds > 0f)
                    return;
            }

            StopFollowing();
        }

        public void StopFollowing()
        {
            if (!_isConfigured)
                return;

            _isConfigured = false;
            _followTarget = null;

            enabled = false;
        }

        private void ApplyFollowTransform()
        {
            if (_followTarget == null)
            {
                StopFollowing();
                return;
            }

            transform.position = _followTarget.TransformPoint(_followLocalOffset);
            transform.rotation = _followTarget.rotation * _followLocalRotation;
        }
    }
}
