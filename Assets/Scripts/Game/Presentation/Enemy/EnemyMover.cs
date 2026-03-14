using UnityEngine;
using UnityEngine.AI;
using Game.GameFlow;

namespace Game.Presentation
{
    /// <summary>
    /// Enemy movement execution layer driven by CurrentIntent.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyMover : MonoBehaviour
    {
        private const float WalkSpeedRatio = 0.55f;
        private const float WalkBlendValue = 0.5f;
        private const float RunBlendValue = 1f;
        private const float RepositionSpeedRatio = 0.85f;
        private const float DodgeSpeedRatio = 1.15f;
        private const float RetreatArrivalDistance = 0.2f;

        private NavMeshAgent _agent;
        private EnemyController _controller;
        private EnemyPerception _perception;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _controller = GetComponent<EnemyController>();
            _perception = GetComponent<EnemyPerception>();

            if (_agent != null)
                _agent.updateRotation = false;
        }

        private void Update()
        {
            if (_controller == null)
                return;

            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
            {
                StopMoving();
                return;
            }

            if (!_controller.IsAlive)
            {
                StopMoving();
                return;
            }

            if (_controller.IsInPostCastRecovery)
            {
                if (ExecutePostCastRecoveryRetreat())
                    return;

                StopMoving();
                return;
            }

            EnemyIntent intent = _controller.CurrentIntent;
            if (_controller.IsCastingSkill || _controller.IsHurt ||
                intent.Type == EnemyIntentType.Idle || intent.Type == EnemyIntentType.Dead)
            {
                StopMoving();
                return;
            }

            if (intent.Type == EnemyIntentType.Hold)
            {
                ExecuteHoldIntent(intent);
                return;
            }

            if (IsMoveIntent(intent.Type))
            {
                ExecuteMoveIntent(intent);
                return;
            }

            StopMoving();
        }

        private void ExecuteMoveIntent(EnemyIntent intent)
        {
            if (!intent.HasTargetPosition)
            {
                StopMoving();
                return;
            }

            float moveSpeed = _controller.MoveSpeed;
            float moveBlend = RunBlendValue;

            switch (intent.Type)
            {
                case EnemyIntentType.Patrol:
                case EnemyIntentType.Search:
                    moveSpeed *= WalkSpeedRatio;
                    moveBlend = WalkBlendValue;
                    break;
                case EnemyIntentType.Chase:
                case EnemyIntentType.Approach:
                case EnemyIntentType.Punish:
                    ResolveChaseSpeed(intent.TargetPosition.Value, ref moveSpeed, ref moveBlend);
                    break;
                case EnemyIntentType.Retreat:
                    moveSpeed *= WalkSpeedRatio;
                    moveBlend = WalkBlendValue;
                    break;
                case EnemyIntentType.Dodge:
                    moveSpeed *= DodgeSpeedRatio;
                    moveBlend = RunBlendValue;
                    break;
                case EnemyIntentType.StrafeLeft:
                case EnemyIntentType.StrafeRight:
                case EnemyIntentType.Reposition:
                    moveSpeed *= RepositionSpeedRatio;
                    moveBlend = WalkBlendValue;
                    break;
            }

            _agent.isStopped = false;
            _agent.speed = moveSpeed;
            _agent.SetDestination(intent.TargetPosition.Value);

            Vector3 facingPoint = ResolveFacingPoint(intent);
            RotateToward(facingPoint);

            ApplyDirectionalAnimatorMotion(intent, moveBlend, facingPoint);
        }

        private Vector3 ResolveFacingPoint(EnemyIntent intent)
        {
            if ((intent.Type == EnemyIntentType.Retreat || intent.Type == EnemyIntentType.Reposition) &&
                _perception != null &&
                _perception.TargetPosition.HasValue)
            {
                return _perception.TargetPosition.Value;
            }

            if ((intent.Type == EnemyIntentType.StrafeLeft ||
                 intent.Type == EnemyIntentType.StrafeRight ||
                 intent.Type == EnemyIntentType.Dodge ||
                 intent.Type == EnemyIntentType.Punish ||
                 intent.Type == EnemyIntentType.Hold) &&
                _perception != null &&
                _perception.TargetPosition.HasValue)
            {
                return _perception.TargetPosition.Value;
            }

            return intent.TargetPosition.Value;
        }

        private void ApplyDirectionalAnimatorMotion(EnemyIntent intent, float moveBlend, Vector3 facingPoint)
        {
            Vector3 moveDir = GetPlanarDirection(transform.position, intent.TargetPosition.Value);
            if (moveDir.sqrMagnitude < 0.0001f)
            {
                moveDir = _agent.desiredVelocity;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.0001f)
                    moveDir.Normalize();
            }

            Vector3 faceDir = GetPlanarDirection(transform.position, facingPoint);
            if (faceDir.sqrMagnitude < 0.0001f)
            {
                faceDir = transform.forward;
                faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.0001f)
                    faceDir.Normalize();
            }

            if (moveDir.sqrMagnitude < 0.0001f || faceDir.sqrMagnitude < 0.0001f)
            {
                _controller.SetAnimatorMove(0f, 0f, 0f);
                return;
            }

            Vector3 right = Vector3.Cross(Vector3.up, faceDir).normalized;
            float forward = Vector3.Dot(moveDir, faceDir);
            float strafe = Vector3.Dot(moveDir, right);

            float norm = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(forward), Mathf.Abs(strafe)));
            forward /= norm;
            strafe /= norm;

            float forwardSigned = Mathf.Clamp(forward * moveBlend, -1f, 1f);
            float strafeSigned = Mathf.Clamp(strafe * moveBlend, -1f, 1f);

            if (intent.Type == EnemyIntentType.Retreat && forwardSigned > 0f)
                forwardSigned = -forwardSigned;

            _controller.SetAnimatorMove(moveBlend, forwardSigned, strafeSigned);
        }

        private bool ExecutePostCastRecoveryRetreat()
        {
            if (!_controller.TryGetPostCastRetreatDestination(out Vector3 retreatDestination))
                return false;

            float planarDistance = GetPlanarDistance(transform.position, retreatDestination);
            if (planarDistance <= RetreatArrivalDistance)
            {
                StopMoving();
                return true;
            }

            _agent.isStopped = false;
            _agent.speed = _controller.MoveSpeed * WalkSpeedRatio;
            _agent.SetDestination(retreatDestination);

            Vector3 facingPoint = ResolveCurrentThreatPosition();
            RotateToward(facingPoint);

            var retreatIntent = new EnemyIntent
            {
                Type = EnemyIntentType.Retreat,
                TargetPosition = retreatDestination,
            };
            ApplyDirectionalAnimatorMotion(retreatIntent, WalkBlendValue, facingPoint);
            return true;
        }

        private void ExecuteHoldIntent(EnemyIntent intent)
        {
            StopMoving();

            Vector3 facingPoint = ResolveFacingPoint(intent);
            RotateToward(facingPoint);
        }

        private Vector3 ResolveCurrentThreatPosition()
        {
            if (_perception != null && _perception.TargetPosition.HasValue)
                return _perception.TargetPosition.Value;

            var gsm = GameStateMachine.GetInstance();
            if (gsm?.LevelPlayerTransform != null)
                return gsm.LevelPlayerTransform.position;

            return transform.position + transform.forward;
        }

        private void ResolveChaseSpeed(Vector3 targetPosition, ref float moveSpeed, ref float moveBlend)
        {
            float targetDistance = GetPlanarDistance(transform.position, targetPosition);
            float innerRing = 4f;
            float outerRing = 9f;

            if (_controller.Archetype != null)
            {
                innerRing = _controller.Archetype.GetChaseInnerDistance();
                outerRing = _controller.Archetype.GetChaseOuterDistance();
            }

            if (targetDistance > outerRing)
            {
                moveBlend = RunBlendValue;
                return;
            }

            if (targetDistance <= innerRing)
            {
                moveSpeed *= WalkSpeedRatio;
                moveBlend = WalkBlendValue;
                return;
            }

            moveSpeed *= WalkSpeedRatio;
            moveBlend = WalkBlendValue;
        }

        private void RotateToward(Vector3 worldPosition)
        {
            Vector3 dir = worldPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                return;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }

        public void SetStopped(bool stopped)
        {
            if (_agent != null)
                _agent.isStopped = stopped;
        }

        private void StopMoving()
        {
            if (_agent != null)
                _agent.isStopped = true;
            _controller.SetAnimatorMove(0f, 0f, 0f);
        }

        private static bool IsMoveIntent(EnemyIntentType type)
        {
            return type == EnemyIntentType.Patrol ||
                   type == EnemyIntentType.Search ||
                   type == EnemyIntentType.Chase ||
                   type == EnemyIntentType.Approach ||
                   type == EnemyIntentType.Punish ||
                   type == EnemyIntentType.StrafeLeft ||
                   type == EnemyIntentType.StrafeRight ||
                   type == EnemyIntentType.Dodge ||
                   type == EnemyIntentType.Retreat ||
                   type == EnemyIntentType.Reposition;
        }

        private static float GetPlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Vector3 GetPlanarDirection(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f)
                return Vector3.zero;
            return dir.normalized;
        }
    }
}
