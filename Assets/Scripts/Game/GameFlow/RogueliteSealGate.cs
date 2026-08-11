using UnityEngine;

namespace Game.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class RogueliteSealGate : MonoBehaviour
    {
        [SerializeField] private Collider _blocker;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Renderer[] _energyRenderers = new Renderer[0];
        [SerializeField] private Light[] _sealLights = new Light[0];
        [SerializeField] private ParticleSystem[] _fogParticles = new ParticleSystem[0];
        [SerializeField] private Color _sealColor = new Color(0.34f, 0.7f, 1f, 0.72f);
        [SerializeField] [Min(0f)] private float _pulseSpeed = 1.5f;
        [SerializeField] [Range(0f, 1f)] private float _pulseAmount = 0.12f;
        [SerializeField] private bool _sealed = true;

        private MaterialPropertyBlock _propertyBlock;
        private float[] _baseLightIntensities = new float[0];

        public bool IsSealed => _sealed;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            CacheLightIntensities();
            ApplySealedState();
        }

        private void Update()
        {
            if (!_sealed)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();

            float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmount;
            Color pulsedColor = _sealColor * pulse;
            pulsedColor.a = _sealColor.a;

            for (int i = 0; i < _energyRenderers.Length; i++)
            {
                Renderer energyRenderer = _energyRenderers[i];
                if (energyRenderer == null)
                    continue;

                energyRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", _sealColor);
                _propertyBlock.SetColor("_Color", pulsedColor);
                _propertyBlock.SetColor("_EmissionColor", _sealColor * 1.45f);
                _propertyBlock.SetFloat("_Pulse", pulse);
                energyRenderer.SetPropertyBlock(_propertyBlock);
            }

            for (int i = 0; i < _sealLights.Length; i++)
            {
                if (_sealLights[i] != null)
                    _sealLights[i].intensity = _baseLightIntensities[i] * pulse;
            }
        }

        public void SetSealed(bool sealedState)
        {
            if (_sealed == sealedState)
                return;

            _sealed = sealedState;
            ApplySealedState();
        }

        public void Configure(
            Collider blocker,
            GameObject visualRoot,
            Renderer[] energyRenderers,
            Light[] sealLights,
            ParticleSystem[] fogParticles,
            Color sealColor)
        {
            _blocker = blocker;
            _visualRoot = visualRoot;
            _energyRenderers = energyRenderers ?? new Renderer[0];
            _sealLights = sealLights ?? new Light[0];
            _fogParticles = fogParticles ?? new ParticleSystem[0];
            _sealColor = sealColor;
            CacheLightIntensities();
            ApplySealedState();
        }

        private void ApplySealedState()
        {
            if (_blocker != null)
                _blocker.enabled = _sealed;
            if (_visualRoot != null)
                _visualRoot.SetActive(_sealed);

            for (int i = 0; i < _fogParticles.Length; i++)
            {
                ParticleSystem particles = _fogParticles[i];
                if (particles == null)
                    continue;

                if (_sealed)
                    particles.Play(true);
                else
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void CacheLightIntensities()
        {
            _baseLightIntensities = new float[_sealLights.Length];
            for (int i = 0; i < _sealLights.Length; i++)
                _baseLightIntensities[i] = _sealLights[i] != null ? Mathf.Max(0f, _sealLights[i].intensity) : 0f;
        }
    }
}
