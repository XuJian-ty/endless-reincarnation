using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 让挂在技能挂点下的特效在经过指定跟随时长后脱离父节点，
    /// 并保留当前世界位置、旋转与缩放。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillCueFollowDetachProxy : MonoBehaviour
    {
        private float _remainingSeconds;
        private bool _isConfigured;

        public void Configure(float followDuration)
        {
            _remainingSeconds = Mathf.Max(0f, followDuration);
            _isConfigured = true;
            enabled = true;

            if (_remainingSeconds <= 0f)
                DetachNow();
        }

        private void LateUpdate()
        {
            if (!_isConfigured)
                return;

            if (_remainingSeconds > 0f)
            {
                _remainingSeconds -= Time.deltaTime;
                if (_remainingSeconds > 0f)
                    return;
            }

            DetachNow();
        }

        private void DetachNow()
        {
            if (!_isConfigured)
                return;

            _isConfigured = false;

            Transform current = transform;
            if (current.parent != null)
                current.SetParent(null, true);

            enabled = false;
        }
    }
}
