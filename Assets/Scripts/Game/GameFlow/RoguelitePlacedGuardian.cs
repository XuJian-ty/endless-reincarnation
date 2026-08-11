using Game.Presentation;
using Game.Saving;
using UnityEngine;

namespace Game.GameFlow
{
    /// <summary>
    /// 标记场景中直接摆放的区域守卫者，并为存档提供稳定身份。
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class RoguelitePlacedGuardian : MonoBehaviour
    {
        [SerializeField] private string placementId;
        [SerializeField] private string regionId;
        [SerializeField] private EnemyController enemyController;

        public string PlacementId => placementId;
        public string RegionId => regionId;
        public bool IsAlive => gameObject.activeInHierarchy && enemyController != null && enemyController.IsAlive;

        private void Awake()
        {
            ResolveController();
            BindRuntimeId();
        }

        public void Configure(string stablePlacementId, string owningRegionId)
        {
            placementId = string.IsNullOrWhiteSpace(stablePlacementId) ? string.Empty : stablePlacementId.Trim();
            regionId = string.IsNullOrWhiteSpace(owningRegionId) ? string.Empty : owningRegionId.Trim();
            ResolveController();
            BindRuntimeId();
        }

        public void ActivateForRegion()
        {
            ResolveController();
            BindRuntimeId();
            gameObject.SetActive(true);
        }

        public void DeactivateForSnapshotRestore()
        {
            gameObject.SetActive(false);
        }

        public void RestoreFromSnapshot(EnemySnapshot snapshot)
        {
            if (snapshot == null)
                return;

            ResolveController();
            if (enemyController == null)
            {
                Debug.LogError($"[RoguelitePlacedGuardian] {name} 缺少 EnemyController。", this);
                return;
            }

            BindRuntimeId();
            enemyController.transform.SetPositionAndRotation(
                new Vector3(snapshot.x, snapshot.y, snapshot.z),
                Quaternion.Euler(0f, snapshot.yaw, 0f));
            gameObject.SetActive(true);
            enemyController.RestoreFromSnapshot(snapshot);
        }

        private void ResolveController()
        {
            if (enemyController == null)
                enemyController = GetComponentInChildren<EnemyController>(true);
        }

        private void BindRuntimeId()
        {
            if (enemyController == null)
                return;
            if (string.IsNullOrWhiteSpace(placementId))
            {
                Debug.LogError($"[RoguelitePlacedGuardian] {name} 未配置稳定摆放 ID。", this);
                return;
            }

            enemyController.AssignPlacedRuntimeId(placementId);
        }
    }
}
