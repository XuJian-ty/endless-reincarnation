using Game.Presentation;
using UnityEngine;

namespace Game.GameFlow
{
    /// <summary>
    /// 负责关卡地图下方的坠落死亡判定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelFallDeathController : MonoBehaviour
    {
        [SerializeField] [Tooltip("玩家低于该世界高度时立即死亡。")]
        private float _deathHeight = -20f;

        private Transform _playerTransform;
        private bool _deathTriggered;

        public void Configure(float deathHeight)
        {
            _deathHeight = deathHeight;
        }

        private void Update()
        {
            if (_deathTriggered)
                return;

            _playerTransform = LocalPlayerInteractionResolver.ResolvePlayerTransform(gameObject.scene, _playerTransform);
            if (_playerTransform == null || _playerTransform.position.y > _deathHeight)
                return;

            PlayerController playerController = _playerTransform.GetComponentInParent<PlayerController>(true);
            if (playerController == null)
                return;

            _deathTriggered = true;
            playerController.ForceEnvironmentalDeath();
        }
    }
}
