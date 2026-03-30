using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Domain;
using Game.GameFlow;

namespace Game.Presentation
{
    public class CombatMotionController : MonoBehaviour
    {
        private CharacterController _characterController;
        private NavMeshAgent _navMeshAgent;
        private Vector3 _velocity;
        private float _remainingTime;
        private bool _capturedAgentState;
        private bool _previousAgentStopped;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        public void ApplyDisplacement(Vector3 worldDelta, float duration)
        {
            if (worldDelta.sqrMagnitude <= 0.0001f)
                return;

            if (duration <= 0.01f)
            {
                ApplyImmediate(worldDelta);
                return;
            }

            _velocity = worldDelta / duration;
            _remainingTime = duration;

            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh && !_capturedAgentState)
            {
                _previousAgentStopped = _navMeshAgent.isStopped;
                _navMeshAgent.isStopped = true;
                _capturedAgentState = true;
            }
        }

        public void HoldStill(float duration)
        {
            if (duration <= 0.01f)
                return;

            _velocity = Vector3.zero;
            _remainingTime = duration;

            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh && !_capturedAgentState)
            {
                _previousAgentStopped = _navMeshAgent.isStopped;
                _navMeshAgent.isStopped = true;
                _capturedAgentState = true;
            }
        }

        private void LateUpdate()
        {
            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            if (_remainingTime <= 0f)
                return;

            float dt = Mathf.Min(Time.deltaTime, _remainingTime);
            var delta = _velocity * dt;
            ApplyImmediate(delta);

            _remainingTime -= dt;
            if (_remainingTime > 0f) return;

            _velocity = Vector3.zero;
            if (_navMeshAgent != null && _navMeshAgent.enabled && _capturedAgentState)
            {
                _navMeshAgent.isStopped = _previousAgentStopped;
                _capturedAgentState = false;
            }
        }

        private void ApplyImmediate(Vector3 delta)
        {
            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(delta);
                return;
            }

            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
            {
                Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);
                if (horizontalDelta.sqrMagnitude > 0.0001f)
                {
                    if (_navMeshAgent.updatePosition)
                        _navMeshAgent.Move(horizontalDelta);
                    else
                        transform.position += horizontalDelta;
                }

                if (Mathf.Abs(delta.y) > 0.0001f)
                {
                    Vector3 position = transform.position;
                    position.y += delta.y;
                    transform.position = position;
                }

                if (!_navMeshAgent.updatePosition || Mathf.Abs(delta.y) > 0.0001f)
                    _navMeshAgent.nextPosition = transform.position;
                return;
            }

            transform.position += delta;
        }
    }

    public class PlayerRuntimeStatModifierController : MonoBehaviour
    {
        [Serializable]
        private sealed class RuntimeModifier
        {
            public StatModifier modifier;
            public float endTime;
        }

        private readonly List<RuntimeModifier> _modifiers = new List<RuntimeModifier>();
        private readonly List<StatModifier> _stateScopedModifiers = new List<StatModifier>();
        private PlayerController _playerController;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            if (_modifiers.Count <= 0 || _playerController?.PlayerModel == null) return;

            float now = Time.time;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (now <= _modifiers[i].endTime) continue;
                _playerController.PlayerModel.RemoveStatModifierAndSyncVitals(_modifiers[i].modifier);
                _modifiers.RemoveAt(i);
            }
        }

        private void OnDisable()
        {
            if (_playerController?.PlayerModel == null) return;
            for (int i = 0; i < _modifiers.Count; i++)
                _playerController.PlayerModel.RemoveStatModifierAndSyncVitals(_modifiers[i].modifier);
            _modifiers.Clear();
            for (int i = 0; i < _stateScopedModifiers.Count; i++)
                _playerController.PlayerModel.RemoveStatModifierAndSyncVitals(_stateScopedModifiers[i]);
            _stateScopedModifiers.Clear();
        }

        public void ApplyModifier(StatModifier modifier, float duration)
        {
            if (_playerController?.PlayerModel == null || modifier == null)
                return;

            var clone = modifier.Clone();
            _playerController.PlayerModel.AddStatModifierAndSyncVitals(clone);
            _modifiers.Add(new RuntimeModifier
            {
                modifier = clone,
                endTime = Time.time + Mathf.Max(0.01f, duration)
            });
        }

        public void ApplyModifierUntilStateExit(StatModifier modifier, SkillCueRuntimeScope cueRuntime)
        {
            if (_playerController?.PlayerModel == null || modifier == null)
                return;

            StatModifier clone = modifier.Clone();
            _playerController.PlayerModel.AddStatModifierAndSyncVitals(clone);
            _stateScopedModifiers.Add(clone);
            cueRuntime?.RegisterStateExitCallback(() =>
            {
                if (this == null || _playerController?.PlayerModel == null)
                    return;

                if (_stateScopedModifiers.Remove(clone))
                    _playerController.PlayerModel.RemoveStatModifierAndSyncVitals(clone);
            });
        }
    }
}
