using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 让技能特效按运动配置持续移动。
    /// </summary>
    public sealed class SkillCueMover : MonoBehaviour
    {
        private SkillMotionSettings _motion;
        private Vector3 _originPosition;
        private Quaternion _originRotation = Quaternion.identity;
        private bool _useUnscaledTime;
        private bool _lockRotation;
        private Quaternion _lockedRotation = Quaternion.identity;
        private float _elapsed;

        public void Initialize(Vector3 originPosition, Quaternion originRotation, SkillMotionSettings motion, bool useUnscaledTime, bool lockRotation, Quaternion lockedRotation)
        {
            _originPosition = originPosition;
            _originRotation = originRotation;
            _motion = motion;
            _useUnscaledTime = useUnscaledTime;
            _lockRotation = lockRotation;
            _lockedRotation = lockedRotation;
            _elapsed = 0f;
        }

        private void Update()
        {
            float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime > 0f && _motion != null && _motion.IsActive)
            {
                _elapsed += deltaTime;
                transform.position = _originPosition + _motion.EvaluateWorldDisplacement(_originRotation, _elapsed);
            }

            if (_lockRotation)
                transform.rotation = _lockedRotation;
        }
    }
}
