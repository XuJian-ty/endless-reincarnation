using UnityEngine;

namespace DragonSwordQiEffect
{
    [DisallowMultipleComponent]
    public sealed class DragonSwordQiDemoController : MonoBehaviour
    {
        [SerializeField] private DragonSwordQiController effectPrefab;
        [SerializeField, Min(0f)] private float replayDelay = 0.85f;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private bool autoReplay = true;
        [SerializeField] private Camera demoCamera;
        [SerializeField] private Transform cameraLookTarget;
        [SerializeField, Min(0f)] private float cameraOrbitSpeed = 2.4f;

        private DragonSwordQiController _activeEffect;
        private float _replayTimer;

        private void Start()
        {
            SpawnEffect();
        }

        private void LateUpdate()
        {
            UpdateCamera();

            if (!autoReplay)
                return;

            if (_activeEffect != null)
                return;

            _replayTimer += Time.deltaTime;
            if (_replayTimer >= replayDelay)
                SpawnEffect();
        }

        private void OnGUI()
        {
            const float panelWidth = 360f;
            var panelRect = new Rect(24f, 24f, panelWidth, 112f);
            GUI.Box(panelRect, GUIContent.none);
            GUI.Label(
                new Rect(42f, 38f, panelWidth - 36f, 26f),
                "Dragon Sword Qi — fully procedural");
            GUI.Label(
                new Rect(42f, 64f, panelWidth - 36f, 22f),
                "Mesh / shader / particles / audio generated from zero");

            if (GUI.Button(new Rect(42f, 91f, 128f, 28f), "Replay"))
                SpawnEffect();

            bool nextAutoReplay = GUI.Toggle(
                new Rect(188f, 95f, 150f, 24f),
                autoReplay,
                "Auto replay");
            if (nextAutoReplay != autoReplay)
            {
                autoReplay = nextAutoReplay;
                _replayTimer = 0f;
            }
        }

        public void Configure(
            DragonSwordQiController prefab,
            Transform effectSpawnPoint,
            Camera targetCamera,
            Transform lookTarget)
        {
            effectPrefab = prefab;
            spawnPoint = effectSpawnPoint;
            demoCamera = targetCamera;
            cameraLookTarget = lookTarget;
        }

        public void SpawnEffect()
        {
            if (effectPrefab == null)
            {
                Debug.LogError("DragonSwordQiDemoController has no effect prefab assigned.", this);
                return;
            }

            if (_activeEffect != null)
                Destroy(_activeEffect.gameObject);

            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            _activeEffect = Instantiate(effectPrefab, position, rotation);
            _activeEffect.name = "DragonSwordQi_Runtime";
            _activeEffect.ConfigurePlayback(true, false, true, true);
            _activeEffect.PlayEffect();
            _replayTimer = 0f;
        }

        private void UpdateCamera()
        {
            if (demoCamera == null || cameraLookTarget == null)
                return;

            Transform cameraTransform = demoCamera.transform;
            Vector3 targetPosition = cameraLookTarget.position;
            Vector3 offset = cameraTransform.position - targetPosition;
            if (cameraOrbitSpeed > 0f)
                offset = Quaternion.AngleAxis(cameraOrbitSpeed * Time.deltaTime, Vector3.up) * offset;

            cameraTransform.position = targetPosition + offset;
            Vector3 lookDirection = targetPosition - cameraTransform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
                cameraTransform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }
    }
}
