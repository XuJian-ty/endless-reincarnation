using UnityEngine;
using Game.Data;

namespace Game.GameFlow
{
    /// <summary>
    /// 最终 Boss 视觉标记：为真正决定关卡胜负的 Boss 添加脉冲光照标记。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelBossVisualMarker : MonoBehaviour
    {
        [Header("光照参数")]
        [SerializeField] [Tooltip("光照颜色。")]
        private Color _markerColor = new Color(1f, 0.18f, 0.18f, 1f);
        [SerializeField] [Min(0f)] [Tooltip("最小光照强度。")]
        private float _minIntensity = 2.6f;
        [SerializeField] [Min(0f)] [Tooltip("最大光照强度。")]
        private float _maxIntensity = 5.4f;
        [SerializeField] [Min(0.1f)] [Tooltip("脉冲速度。")]
        private float _pulseSpeed = 2f;
        [SerializeField] [Min(0.1f)] [Tooltip("光照范围。")]
        private float _range = 11f;
        [SerializeField] [Tooltip("光照高度偏移。")]
        private Vector3 _offset = new Vector3(0f, 4.2f, 0f);

        private Light _markerLight;

        private void Awake()
        {
            ApplyConfig();
            EnsureMarkerLight();
        }

        private void LateUpdate()
        {
            if (_markerLight == null)
                EnsureMarkerLight();

            if (_markerLight == null)
                return;

            Transform lightTransform = _markerLight.transform;
            lightTransform.position = transform.position + _offset;
            float pulse = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
            _markerLight.intensity = Mathf.Lerp(_minIntensity, _maxIntensity, pulse);
        }

        private void EnsureMarkerLight()
        {
            if (_markerLight != null)
                return;

            Transform existing = transform.Find("FinalBossMarkerLight");
            if (existing != null)
                _markerLight = existing.GetComponent<Light>();

            if (_markerLight == null)
            {
                var lightObject = new GameObject("FinalBossMarkerLight");
                lightObject.transform.SetParent(transform, false);
                _markerLight = lightObject.AddComponent<Light>();
            }

            if (_markerLight == null)
                return;

            _markerLight.type = LightType.Point;
            _markerLight.color = _markerColor;
            _markerLight.range = _range;
            _markerLight.intensity = _maxIntensity;
            _markerLight.shadows = LightShadows.None;
            _markerLight.renderMode = LightRenderMode.ForcePixel;
            _markerLight.transform.localPosition = _offset;
        }

        private void ApplyConfig()
        {
            PlayerCloneAndLevelBossVisualConfigSO config = ConfigManager.GetInstance()?.GetPlayerCloneAndLevelBossVisualConfig();
            if (config == null)
                return;

            _markerColor = config.levelBoss.markerColor;
            _minIntensity = config.levelBoss.minIntensity;
            _maxIntensity = config.levelBoss.maxIntensity;
            _pulseSpeed = config.levelBoss.pulseSpeed;
            _range = config.levelBoss.range;
            _offset = config.levelBoss.offset;
        }
    }
}
