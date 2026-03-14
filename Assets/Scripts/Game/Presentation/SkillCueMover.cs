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

        public void Initialize(Vector3 velocity, bool useLocalSpace, bool useUnscaledTime)
        {
            _velocity = velocity;
            _useLocalSpace = useLocalSpace;
            _useUnscaledTime = useUnscaledTime;
        }

        private void Update()
        {
            float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f || _velocity.sqrMagnitude <= 0.000001f)
                return;

            if (_useLocalSpace)
                transform.localPosition += _velocity * deltaTime;
            else
                transform.position += _velocity * deltaTime;
        }
    }
}
