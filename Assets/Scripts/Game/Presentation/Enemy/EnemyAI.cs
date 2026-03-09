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
        [Header("��Ϊ��������������")]
        [SerializeField] private BehaviorTreeAsset _behaviorTreeAsset;

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

            _ctx.Controller = _controller;
            _ctx.Perception = _perception;
            _ctx.Archetype = _controller.Archetype;
            _ctx.PatrolOrigin = _patrolOrigin;
            _ctx.NextPatrolTime = _nextPatrolTime;

            _root.Tick(_ctx);
            _nextPatrolTime = _ctx.NextPatrolTime;
        }

        private void EnsureBehaviorTree()
        {
            _controller?.EnsureInitialized();

            var asset = _behaviorTreeAsset;
            if (asset == null)
                asset = _controller != null ? _controller.Archetype?.behaviorTreeAsset : null;

            if (asset != null && asset.nodes != null && asset.nodes.Count > 0)
            {
                _root = BehaviorTreeFromDataBuilder.Build(asset);
                return;
            }

            _root = _controller != null && _controller.Archetype != null
                ? EnemyBehaviorTreeBuilder.BuildDefaultTree(_controller.Archetype)
                : null;
        }
    }
}

