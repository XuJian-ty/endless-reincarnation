using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 连续受击保护系统：管理玩家连续受击时的霸体保护机制。
    /// 在短时间内受击多次时自动触发霸体保护，避免被连续控制。
    /// </summary>
    public class HitProtectionSystem
    {
        private readonly Queue<float> _recentHitTimes = new Queue<float>();
        private float _protectionEndTime;

        private readonly float _timeWindow;
        private readonly int _hitThreshold;
        private readonly float _protectionDuration;

        /// <summary>
        /// 是否处于霸体保护状态
        /// </summary>
        public bool IsProtected => Time.time < _protectionEndTime;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="timeWindow">触发保护的时间窗口（秒）</param>
        /// <param name="hitThreshold">时间窗口内受击次数达到此值触发霸体</param>
        /// <param name="protectionDuration">触发后获得的霸体持续时间（秒）</param>
        public HitProtectionSystem(float timeWindow, int hitThreshold, float protectionDuration)
        {
            _timeWindow = timeWindow;
            _hitThreshold = hitThreshold;
            _protectionDuration = protectionDuration;
        }

        /// <summary>
        /// 记录一次受击，如果满足条件则触发保护
        /// </summary>
        /// <returns>是否触发了保护</returns>
        public bool RecordHit()
        {
            float currentTime = Time.time;

            // 记录受击时间
            _recentHitTimes.Enqueue(currentTime);

            // 移除超出时间窗口的旧记录
            while (_recentHitTimes.Count > 0 && currentTime - _recentHitTimes.Peek() > _timeWindow)
            {
                _recentHitTimes.Dequeue();
            }

            // 检查是否触发连续受击保护
            if (_recentHitTimes.Count >= _hitThreshold)
            {
                _protectionEndTime = currentTime + _protectionDuration;
                _recentHitTimes.Clear();
                Debug.Log($"[HitProtectionSystem] 连续受击保护触发，获得 {_protectionDuration} 秒霸体");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 重置保护系统状态
        /// </summary>
        public void Reset()
        {
            _recentHitTimes.Clear();
            _protectionEndTime = 0f;
        }

        /// <summary>
        /// 获取剩余保护时间（秒）
        /// </summary>
        public float GetRemainingProtectionTime()
        {
            if (!IsProtected) return 0f;
            return Mathf.Max(0f, _protectionEndTime - Time.time);
        }
    }
}
