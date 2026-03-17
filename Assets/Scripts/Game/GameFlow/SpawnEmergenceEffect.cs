using System.Collections.Generic;
using Game.Presentation;
using UnityEngine;
using UnityEngine.AI;

namespace Game.GameFlow
{
    /// <summary>
    /// 统一生成表现：让对象在短时间内从地下升起，并在完成前冻结移动与碰撞。
    /// </summary>
    public sealed class SpawnEmergenceEffect : MonoBehaviour
    {
        private const float DefaultDuration = 0.75f;
        private const float DefaultRiseDistance = 1.5f;

        private sealed class RigidbodyState
        {
            public Rigidbody rigidbody;
            public bool wasKinematic;
            public bool hadGravity;
            public Vector3 velocity;
            public Vector3 angularVelocity;
        }

        private readonly List<Behaviour> _disabledBehaviours = new List<Behaviour>();
        private readonly List<Collider> _disabledColliders = new List<Collider>();
        private readonly List<RigidbodyState> _rigidbodyStates = new List<RigidbodyState>();

        private NavMeshAgent _navMeshAgent;
        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private float _duration;
        private float _elapsed;
        private bool _initialized;
        private bool _completed;

        public void Initialize(float duration = DefaultDuration)
        {
            if (_initialized)
                return;

            _initialized = true;
            _duration = Mathf.Max(0.01f, duration);
            _targetPosition = transform.position;
            _startPosition = _targetPosition - Vector3.up * ResolveRiseDistance();

            FreezeForEmergence();
            transform.position = _startPosition;
        }

        private void Update()
        {
            if (!_initialized || _completed)
                return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.LerpUnclamped(_startPosition, _targetPosition, eased);

            if (t >= 1f)
                CompleteEmergence();
        }

        private float ResolveRiseDistance()
        {
            bool hasBounds = false;
            Bounds combinedBounds = default;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                Collider[] colliders = GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider collider = colliders[i];
                    if (collider == null)
                        continue;

                    if (!hasBounds)
                    {
                        combinedBounds = collider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(collider.bounds);
                    }
                }
            }

            return hasBounds
                ? Mathf.Max(DefaultRiseDistance, combinedBounds.size.y)
                : DefaultRiseDistance;
        }

        private void FreezeForEmergence()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
            if (_navMeshAgent != null && _navMeshAgent.enabled)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.enabled = false;
            }

            DisableBehaviourIfEnabled<EnemyAI>();
            DisableBehaviourIfEnabled<EnemyMover>();
            DisableBehaviourIfEnabled<EnemyCombat>();

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                _disabledColliders.Add(collider);
            }

            Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rigidbody = rigidbodies[i];
                if (rigidbody == null)
                    continue;

                _rigidbodyStates.Add(new RigidbodyState
                {
                    rigidbody = rigidbody,
                    wasKinematic = rigidbody.isKinematic,
                    hadGravity = rigidbody.useGravity,
                    velocity = rigidbody.velocity,
                    angularVelocity = rigidbody.angularVelocity,
                });
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                rigidbody.velocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void CompleteEmergence()
        {
            _completed = true;
            transform.position = _targetPosition;

            for (int i = 0; i < _rigidbodyStates.Count; i++)
            {
                RigidbodyState state = _rigidbodyStates[i];
                if (state?.rigidbody == null)
                    continue;

                state.rigidbody.isKinematic = state.wasKinematic;
                state.rigidbody.useGravity = state.hadGravity;
                if (!state.rigidbody.isKinematic)
                {
                    state.rigidbody.velocity = state.velocity;
                    state.rigidbody.angularVelocity = state.angularVelocity;
                }
            }

            for (int i = 0; i < _disabledColliders.Count; i++)
            {
                Collider collider = _disabledColliders[i];
                if (collider != null)
                    collider.enabled = true;
            }

            if (_navMeshAgent != null)
            {
                _navMeshAgent.enabled = true;
                if (_navMeshAgent.isOnNavMesh)
                    _navMeshAgent.Warp(_targetPosition);
            }

            for (int i = 0; i < _disabledBehaviours.Count; i++)
            {
                Behaviour behaviour = _disabledBehaviours[i];
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            Destroy(this);
        }

        private void DisableBehaviourIfEnabled<T>() where T : Behaviour
        {
            T behaviour = GetComponent<T>();
            if (behaviour == null || !behaviour.enabled)
                return;

            behaviour.enabled = false;
            _disabledBehaviours.Add(behaviour);
        }
    }
}
