using UnityEngine;
using Game.Data;
using Game.AI;
using Game.GameFlow;

namespace Game.Presentation
{
    /// <summary>
    /// Runs perception first, then behavior tree evaluation.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyAI : MonoBehaviour
    {
        private EnemyController _controller;
        private EnemyPerception _perception;
        private IBehaviorNode _root;
        private EnemyAIContext _ctx;
        private Vector3 _patrolOrigin;
        private float _nextPatrolTime;

        private void Awake()
        {
            _controller = GetComponent<EnemyController>();
            _controller?.EnsureInitialized();

            _perception = GetComponent<EnemyPerception>();
            if (_perception == null)
                _perception = gameObject.AddComponent<EnemyPerception>();
        }

        private void Start()
        {
            _patrolOrigin = transform.position;
            _nextPatrolTime = 0f;
            _ctx = new EnemyAIContext();
            _ctx.Memory = new EnemyCombatMemory();
            EnsureBehaviorTree();
        }

        private void OnEnable()
        {
            _patrolOrigin = transform.position;
            _nextPatrolTime = 0f;

            if (_ctx == null)
            {
                _ctx = new EnemyAIContext();
                _ctx.Memory = new EnemyCombatMemory();
            }
            else
            {
                _ctx.Memory ??= new EnemyCombatMemory();
                _ctx.Memory.Clear();
            }

            EnsureBehaviorTree();
        }

        private void Update()
        {
            _controller?.EnsureInitialized();
            if (GameStateMachine.GetInstance()?.IsGameplayPaused == true)
                return;

            _perception?.Tick();
            if (_controller == null || !_controller.IsAlive) return;
            if (_controller.IsCastingSkill) return;
            if (_controller.IsDecisionLocked) return;

            if (_root == null)
                EnsureBehaviorTree();
            if (_root == null || _ctx == null) return;

            float now = Time.time;
            _ctx.Controller = _controller;
            _ctx.Perception = _perception;
            _ctx.Archetype = _controller.Archetype;
            _ctx.TimeNow = now;
            _ctx.PatrolOrigin = _patrolOrigin;
            _ctx.NextPatrolTime = _nextPatrolTime;

            if (!_perception.HasImmediateThreat &&
                _ctx.Memory != null &&
                !_ctx.Memory.CanEvaluate(now) &&
                !ShouldForceImmediateReevaluation())
                return;

            _root.Tick(_ctx);
            _nextPatrolTime = _ctx.NextPatrolTime;
        }

        private void EnsureBehaviorTree()
        {
            _controller?.EnsureInitialized();

            _root = _controller != null && _controller.Archetype != null
                ? EnemyBehaviorTreeBuilder.BuildDefaultTree()
                : null;
        }

        private bool ShouldForceImmediateReevaluation()
        {
            if (_controller == null)
                return false;

            EnemyIntent intent = _controller.CurrentIntent;
            if (intent.Type == EnemyIntentType.None)
                return true;

            if (intent.Type == EnemyIntentType.Hold || intent.Type == EnemyIntentType.Idle)
                return true;

            if (!intent.HasTargetPosition)
                return false;

            if (!IsMoveIntent(intent.Type))
                return false;

            Vector3 delta = intent.TargetPosition.Value - _controller.transform.position;
            if (!_controller.UsesAerialMovement)
                delta.y = 0f;
            return delta.sqrMagnitude <= 0.75f * 0.75f;
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
    }
}

