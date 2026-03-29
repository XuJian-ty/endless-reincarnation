using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 封装 CharacterController 的物理层：重力、移动、跳跃、闪避速度。
    /// 不含任何游戏逻辑，由 PlayerController.Update 在状态机 Tick 之后调用 Tick(dt)。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : MonoBehaviour
    {
        [SerializeField] private float _gravity = -20f;

        private CharacterController _cc;
        private Vector3 _velocity;
        private bool    _isDodging;
        private Vector3 _dodgeVelocity;
        private float _motionSpeedMultiplier = 1f;

        public bool    IsGrounded       => _cc.isGrounded;
        public Vector3 Velocity         => _velocity;
        public float   VerticalVelocity => _velocity.y;

        private void Awake() => _cc = GetComponent<CharacterController>();

        public void Tick(float dt)
        {
            if (_cc.isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;
            else
                _velocity.y += _gravity * dt;

            Vector3 moveVelocity = _velocity;
            if (_isDodging)
            {
                _velocity.x = _dodgeVelocity.x;
                _velocity.z = _dodgeVelocity.z;
                moveVelocity.x = _dodgeVelocity.x;
                moveVelocity.z = _dodgeVelocity.z;
            }

            _cc.Move(moveVelocity * dt * Mathf.Max(0f, _motionSpeedMultiplier));
        }

        public void SetHorizontalVelocity(Vector3 horizontal)
        {
            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
        }

        public void RotateToward(Vector3 direction, float rotateSpeed)
        {
            if (direction.sqrMagnitude < 0.01f) return;
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotateSpeed * Time.deltaTime);
        }

        public void Jump(float jumpHeight)
            => _velocity.y = Mathf.Sqrt(-2f * _gravity * jumpHeight);

        public void StartDodge(Vector3 direction, float speed)
        {
            _isDodging     = true;
            _dodgeVelocity = direction.normalized * speed;
        }

        public void StopDodge()
        {
            _isDodging     = false;
            _dodgeVelocity = Vector3.zero;
            _velocity.x    = 0f;
            _velocity.z    = 0f;
        }

        public void SetVerticalVelocity(float vy) => _velocity.y = vy;
        public void SetMotionSpeedMultiplier(float multiplier) => _motionSpeedMultiplier = Mathf.Max(0f, multiplier);

        /// <summary>
        /// 检测角色是否在距地面 threshold 米以内。
        /// 仅在下落阶段（垂直速度 ≤ 0）时返回 true，上升阶段始终返回 false。
        /// </summary>
        public bool IsNearGround(float threshold)
        {
            if (IsGrounded) return true;
            if (_velocity.y > 0f) return false;

            Vector3 origin   = transform.position + Vector3.up * (_cc.radius + _cc.skinWidth);
            float   castDist = threshold + _cc.radius + _cc.skinWidth;

            return Physics.SphereCast(
                origin, _cc.radius * 0.9f, Vector3.down,
                out _, castDist,
                ~LayerMask.GetMask("Player"),
                QueryTriggerInteraction.Ignore);
        }
    }
}
