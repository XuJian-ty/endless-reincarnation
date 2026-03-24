using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 让技能特效按固定速度持续移动。
    /// </summary>
    public sealed class SkillCueMover : MonoBehaviour
    {
        private Vector3 _velocity;
        private bool _useLocalSpace;
        private bool _useUnscaledTime;
        private bool _lockRotation;
        private Quaternion _lockedRotation = Quaternion.identity;

        public void Initialize(Vector3 velocity, bool useLocalSpace, bool useUnscaledTime, bool lockRotation, Quaternion lockedRotation)
        {
            _velocity = velocity;
            _useLocalSpace = useLocalSpace;
            _useUnscaledTime = useUnscaledTime;
            _lockRotation = lockRotation;
            _lockedRotation = lockedRotation;
        }

        private void Update()
        {
            float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime > 0f && _velocity.sqrMagnitude > 0.000001f)
            {
                if (_useLocalSpace)
                    transform.localPosition += _velocity * deltaTime;
                else
                    transform.position += _velocity * deltaTime;
            }

            if (_lockRotation)
                transform.rotation = _lockedRotation;
        }
    }
}
