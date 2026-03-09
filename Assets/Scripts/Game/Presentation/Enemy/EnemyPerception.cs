using System;
using UnityEngine;
using UnityEngine.AI;
using Game.Data;
using Game.GameFlow;

namespace Game.Presentation
{
    /// <summary>
    /// Enemy perception based on sector detection, LOS/path probing, and target velocity estimation.
    /// </summary>
    public class EnemyPerception : MonoBehaviour
    {
        private Transform _player;
        private EnemyController _controller;

        private bool _hasLineOfSight;
        private bool _hasReachablePath;
        private float _nextLosProbeTime;
        private float _nextPathProbeTime;
        private Vector3 _lastPlayerSamplePosition;
        private float _lastPlayerSampleTime;
        private bool _hasLastPlayerSample;
        private NavMeshPath _pathBuffer;

        public bool HasTarget { get; private set; }
        public Transform CurrentTarget => HasTarget ? _player : null;
        public Vector3? TargetPosition { get; private set; }
        public float DistanceToPlayer { get; private set; }
        public bool IsPlayerInSector { get; private set; }
        public bool IsPlayerBeyondChaseBreak { get; private set; }
        public bool HasLineOfSight => HasTarget && _hasLineOfSight;
        public bool HasReachablePath => HasTarget && _hasReachablePath;
        public Vector3 TargetVelocity { get; private set; }

        public bool IsInSkillRange(int slot)
        {
            if (_controller == null)
                _controller = GetComponent<EnemyController>();
            if (_controller == null || !HasTarget) return false;

            _controller.EnsureInitialized();
            float range = _controller.GetSkillRange(slot);
            float tolerance = _controller.Archetype != null ? Mathf.Max(0f, _controller.Archetype.castRangeTolerance) : 0f;
            return range > 0.01f && DistanceToPlayer <= range + tolerance;
        }

        public Vector3 PredictTargetPosition(float leadTime)
        {
            if (!TargetPosition.HasValue)
                return transform.position;

            Vector3 predicted = TargetPosition.Value + TargetVelocity * Mathf.Max(0f, leadTime);
            predicted.y = TargetPosition.Value.y;
            return predicted;
        }

        public bool HasLineOfSightFrom(Vector3 from, Vector3 to)
        {
            EnemyArchetypeSO archetype = _controller != null ? _controller.Archetype : null;
            return ProbeLineOfSight(from, to, archetype);
        }

        private void Awake()
        {
            _pathBuffer = new NavMeshPath();
        }

        private void Start()
        {
            _controller = GetComponent<EnemyController>();
            _controller?.EnsureInitialized();
            RefreshPlayerRef();
        }

        private void RefreshPlayerRef()
        {
            var gsm = GameStateMachine.GetInstance();
            if (gsm != null && gsm.LevelPlayerTransform != null)
                _player = gsm.LevelPlayerTransform;
            if (_player == null)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (player != null)
                    _player = player.transform;
            }
        }

        public void Tick()
        {
            if (_controller == null)
                _controller = GetComponent<EnemyController>();
            if (_controller == null || !_controller.IsAlive)
            {
                ClearTargetState();
                return;
            }

            _controller.EnsureInitialized();
            EnemyArchetypeSO archetype = _controller.Archetype;
            if (archetype == null)
            {
                ClearTargetState();
                return;
            }

            if (_player == null || !_player.gameObject.activeInHierarchy)
            {
                RefreshPlayerRef();
                if (_player == null)
                {
                    ClearTargetState();
                    return;
                }
            }

            Vector3 playerPos = _player.position;
            Vector3 toPlayer = playerPos - transform.position;
            toPlayer.y = 0f;
            DistanceToPlayer = toPlayer.magnitude;

            float sectorRange = archetype.sectorRange;
            float chaseBreakDistance = archetype.chaseBreakDistance;
            float sectorAngle = archetype.sectorAngle;

            bool inRange = DistanceToPlayer <= sectorRange;
            Vector3 forward = transform.forward;
            if (toPlayer.sqrMagnitude < 0.0001f)
                toPlayer = forward;
            Vector3 toPlayerDir = toPlayer.normalized;
            float minDot = Mathf.Cos(sectorAngle * 0.5f * Mathf.Deg2Rad);
            bool inSector = inRange && Vector3.Dot(forward, toPlayerDir) >= minDot;

            IsPlayerInSector = inSector;
            IsPlayerBeyondChaseBreak = DistanceToPlayer > chaseBreakDistance;

            if (HasTarget)
            {
                if (IsPlayerBeyondChaseBreak)
                {
                    ClearTargetState();
                    return;
                }

                TargetPosition = playerPos;
            }
            else if (inSector)
            {
                HasTarget = true;
                TargetPosition = playerPos;
            }
            else
            {
                TargetPosition = null;
            }

            if (!HasTarget || !TargetPosition.HasValue)
            {
                _hasLineOfSight = false;
                _hasReachablePath = false;
                TargetVelocity = Vector3.zero;
                return;
            }

            UpdateTargetVelocity(playerPos);

            float now = Time.time;
            if (now >= _nextLosProbeTime)
            {
                _hasLineOfSight = ProbeLineOfSight(transform.position, playerPos, archetype);
                _nextLosProbeTime = now + Mathf.Max(0.01f, archetype.losProbeInterval);
            }

            if (now >= _nextPathProbeTime)
            {
                _hasReachablePath = ProbePathToTarget(playerPos);
                _nextPathProbeTime = now + Mathf.Max(0.02f, archetype.pathProbeInterval);
            }
        }

        private void UpdateTargetVelocity(Vector3 playerPos)
        {
            if (_hasLastPlayerSample)
            {
                float dt = Time.time - _lastPlayerSampleTime;
                if (dt > 0.0001f)
                {
                    Vector3 velocity = (playerPos - _lastPlayerSamplePosition) / dt;
                    velocity.y = 0f;
                    TargetVelocity = velocity;
                }
                else
                {
                    TargetVelocity = Vector3.zero;
                }
            }
            else
            {
                TargetVelocity = Vector3.zero;
                _hasLastPlayerSample = true;
            }

            _lastPlayerSamplePosition = playerPos;
            _lastPlayerSampleTime = Time.time;
        }

        private bool ProbeLineOfSight(Vector3 from, Vector3 to, EnemyArchetypeSO archetype)
        {
            Vector3 origin = WithEyeHeight(from, archetype);
            Vector3 destination = WithEyeHeight(to, archetype);

            Vector3 dir = destination - origin;
            float distance = dir.magnitude;
            if (distance <= 0.05f)
                return true;

            int obstacleMask = archetype != null ? archetype.obstacleMask.value : Physics.DefaultRaycastLayers;
            if (obstacleMask == 0)
                obstacleMask = Physics.DefaultRaycastLayers;

            RaycastHit[] hits = Physics.RaycastAll(origin, dir / distance, distance, obstacleMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return true;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == null)
                    continue;
                if (hitTransform.IsChildOf(transform))
                    continue;
                if (_player != null && hitTransform.IsChildOf(_player))
                    return true;

                return false;
            }

            return true;
        }

        private bool ProbePathToTarget(Vector3 targetPos)
        {
            if (_pathBuffer == null)
                _pathBuffer = new NavMeshPath();

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, 2f, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(targetPos, out NavMeshHit endHit, 2f, NavMesh.AllAreas))
                return false;

            bool hasPath = NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, _pathBuffer);
            return hasPath && _pathBuffer.status == NavMeshPathStatus.PathComplete;
        }

        private static Vector3 WithEyeHeight(Vector3 position, EnemyArchetypeSO archetype)
        {
            float height = archetype != null ? Mathf.Max(0f, archetype.perceptionEyeHeight) : 1.2f;
            return position + Vector3.up * height;
        }

        private void ClearTargetState()
        {
            HasTarget = false;
            TargetPosition = null;
            DistanceToPlayer = float.MaxValue;
            IsPlayerInSector = false;
            IsPlayerBeyondChaseBreak = true;
            _hasLineOfSight = false;
            _hasReachablePath = false;
            TargetVelocity = Vector3.zero;
            _hasLastPlayerSample = false;
            _nextLosProbeTime = 0f;
            _nextPathProbeTime = 0f;
        }
    }
}
