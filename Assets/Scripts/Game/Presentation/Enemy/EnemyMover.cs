using UnityEngine;
using UnityEngine.AI;
using Game.Data;
using Game.GameFlow;

namespace Game.Presentation
{
    /// <summary>
    /// Enemy movement execution layer driven by CurrentIntent.
    /// </summary>
    public class EnemyMover : MonoBehaviour
    {
        private const float WalkSpeedRatio = 0.55f;
        private const float WalkBlendValue = 0.5f;
        private const float RunBlendValue = 1f;
        private const float RepositionSpeedRatio = 0.85f;
        private const float DodgeSpeedRatio = 1.15f;
        private const float RetreatArrivalDistance = 0.2f;
        private const float AerialArrivalDistance = 0.3f;

        private NavMeshAgent _agent;
        private EnemyController _controller;
        private EnemyCombat _combat;
        private EnemyPerception _perception;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _controller = GetComponent<EnemyController>();
            _combat = GetComponent<EnemyCombat>();
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
                if (ExecutePostCastRecoveryMovement())
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
            float timelineMoveSpeedMultiplier = _combat != null
                ? Mathf.Max(0f, _combat.CurrentMovementSpeedMultiplier)
                : 1f;

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

            moveSpeed *= timelineMoveSpeedMultiplier;

            if (_controller.UsesAerialMovement)
            {
                ExecuteAerialMoveIntent(intent, moveSpeed, moveBlend);
                return;
            }

            if (_agent == null)
            {
                StopMoving();
                return;
            }

            _agent.isStopped = false;
            _agent.speed = moveSpeed;
            _agent.SetDestination(intent.TargetPosition.Value);

            Vector3 facingPoint = ResolveFacingPoint(intent);
            RotateToward(facingPoint);

            ApplyDirectionalAnimatorMotion(intent, moveBlend, facingPoint);
        }

        private void ExecuteAerialMoveIntent(EnemyIntent intent, float moveSpeed, float moveBlend)
        {
            Vector3 destination = _controller.ResolveFlightMoveDestination(intent.TargetPosition.Value);
            MoveAerialToDestination(destination, moveSpeed);

            Vector3 facingPoint = ResolveFacingPoint(intent);
            RotateToward(facingPoint);
            ApplyAerialAnimatorMotion(destination, moveBlend);
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
                if (_agent != null)
                {
                    moveDir = _agent.desiredVelocity;
                    moveDir.y = 0f;
                    if (moveDir.sqrMagnitude > 0.0001f)
                        moveDir.Normalize();
                }
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

        private bool ExecutePostCastRecoveryMovement()
        {
            if (!_controller.TryGetPostCastRetreatDestination(out Vector3 retreatDestination))
            {
                if (!TryResolveDynamicRecoveryDestination(out retreatDestination))
                    return false;
            }

            if (_controller.UsesAerialMovement)
                return ExecuteAerialPostCastRecoveryMovement(retreatDestination);

            float planarDistance = GetPlanarDistance(transform.position, retreatDestination);
            if (planarDistance <= RetreatArrivalDistance)
            {
                StopMoving();
                return true;
            }

            if (_agent == null)
            {
                StopMoving();
                return false;
            }

            _agent.isStopped = false;
            float timelineMoveSpeedMultiplier = _combat != null
                ? Mathf.Max(0f, _combat.CurrentMovementSpeedMultiplier)
                : 1f;
            _agent.speed = _controller.MoveSpeed * WalkSpeedRatio * timelineMoveSpeedMultiplier;
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

        private bool ExecuteAerialPostCastRecoveryMovement(Vector3 retreatDestination)
        {
            float distance = Vector3.Distance(transform.position, _controller.ResolveFlightMoveDestination(retreatDestination));
            if (distance <= AerialArrivalDistance)
            {
                StopMoving();
                return true;
            }

            float timelineMoveSpeedMultiplier = _combat != null
                ? Mathf.Max(0f, _combat.CurrentMovementSpeedMultiplier)
                : 1f;
            float moveSpeed = _controller.MoveSpeed * WalkSpeedRatio * timelineMoveSpeedMultiplier;
            Vector3 destination = _controller.ResolveFlightMoveDestination(retreatDestination);
            MoveAerialToDestination(destination, moveSpeed);

            Vector3 facingPoint = ResolveCurrentThreatPosition();
            RotateToward(facingPoint);
            ApplyAerialAnimatorMotion(destination, WalkBlendValue);
            return true;
        }

        private bool TryResolveDynamicRecoveryDestination(out Vector3 destination)
        {
            destination = Vector3.zero;
            if (_controller == null || _perception == null || !_perception.TargetPosition.HasValue)
                return false;

            EnemyArchetypeSO archetype = _controller.Archetype;
            float desiredDistance = _controller.GetPreferredCombatRange();
            int preferredSign = Game.AI.EnemySquadCoordinator.GetPreferredStrafeSign(_controller);

            if (Game.AI.EnemyTacticalNavigation.TryFindStrafePoint(
                    _controller,
                    _perception,
                    archetype,
                    desiredDistance,
                    preferredSign,
                    out destination))
            {
                return true;
            }

            if (Game.AI.EnemyTacticalNavigation.TryFindStrafePoint(
                    _controller,
                    _perception,
                    archetype,
                    desiredDistance,
                    -preferredSign,
                    out destination))
            {
                return true;
            }

            if (Game.AI.EnemyTacticalNavigation.TryFindRepositionPoint(
                    _controller,
                    _perception,
                    archetype,
                    desiredDistance,
                    preferredSign,
                    out destination))
            {
                return true;
            }

            return false;
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
            float targetDistance = _controller != null && _controller.UsesAerialMovement
                ? Vector3.Distance(transform.position, targetPosition)
                : GetPlanarDistance(transform.position, targetPosition);
            float walkThreshold = 2.5f;

            if (_controller.Archetype != null)
                walkThreshold = _controller.Archetype.GetChaseInnerDistance();

            if (targetDistance <= walkThreshold)
            {
                moveSpeed *= WalkSpeedRatio;
                moveBlend = WalkBlendValue;
                return;
            }

            moveBlend = RunBlendValue;
        }

        private void RotateToward(Vector3 worldPosition)
        {
            Vector3 dir = worldPosition - transform.position;
            if (_controller == null || !_controller.UsesAerialMovement)
                dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                return;

            float turnSpeed = _controller != null && _controller.UsesAerialMovement && _controller.Archetype != null
                ? Mathf.Max(0.1f, _controller.Archetype.flightTurnSpeed)
                : 10f;
            Vector3 upAxis = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, upAxis);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
        }

        public void SetStopped(bool stopped)
        {
            if (_agent != null)
                _agent.isStopped = stopped;
        }

        private void StopMoving()
        {
            if (_agent != null)
            {
                _agent.isStopped = true;
                if (_controller != null && _controller.UsesAerialMovement && _agent.enabled && _agent.isOnNavMesh)
                    _agent.nextPosition = transform.position;
            }
            _controller.SetAnimatorMove(0f, 0f, 0f);
        }

        private void MoveAerialToDestination(Vector3 destination, float moveSpeed)
        {
            if (_agent != null && _agent.enabled)
            {
                _agent.isStopped = true;
                if (_agent.isOnNavMesh)
                    _agent.nextPosition = transform.position;
            }

            float verticalSpeed = _controller != null && _controller.Archetype != null
                ? Mathf.Max(0.1f, _controller.Archetype.flightVerticalSpeed)
                : moveSpeed;

            Vector3 delta = destination - transform.position;
            Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);

            float horizontalStep = moveSpeed * Time.deltaTime;
            float verticalStep = verticalSpeed * Time.deltaTime;

            Vector3 nextPosition = transform.position;
            if (horizontalDelta.sqrMagnitude > 0.0001f)
            {
                Vector3 horizontalMove = horizontalDelta.normalized * Mathf.Min(horizontalStep, horizontalDelta.magnitude);
                nextPosition += new Vector3(horizontalMove.x, 0f, horizontalMove.z);
            }

            nextPosition.y = Mathf.MoveTowards(transform.position.y, destination.y, verticalStep);
            transform.position = nextPosition;
        }

        private void ApplyAerialAnimatorMotion(Vector3 destination, float moveBlend)
        {
            Vector3 delta = destination - transform.position;
            if (delta.sqrMagnitude <= AerialArrivalDistance * AerialArrivalDistance)
            {
                _controller.SetAnimatorMove(0f, 0f, 0f);
                return;
            }

            _controller.SetAnimatorMove(moveBlend, moveBlend, 0f);
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
