using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonSwordQiEffect.WaterDragonQi
{
    [DisallowMultipleComponent]
    public sealed class WaterDragonQiController : MonoBehaviour
    {
        [Header("Generated Assets")]
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material coreMaterial;
        [SerializeField] private Material shellMaterial;

        [Header("Playback")]
        [SerializeField, Min(0.05f)] private float chargeDuration = 0.36f;
        [SerializeField, Min(0.1f)] private float flightDuration = 1.12f;
        [SerializeField, Min(0.05f)] private float lingerDuration = 0.44f;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool loop;
        [SerializeField] private bool destroyOnComplete = true;
        [SerializeField, Min(0f)] private float loopDelay = 0.5f;

        [Header("Path")]
        [SerializeField] private Vector3 pathStart = new Vector3(-0.35f, -0.02f, 0f);
        [SerializeField] private Vector3 pathControlA = new Vector3(-0.05f, 0.34f, 1.1f);
        [SerializeField] private Vector3 pathControlB = new Vector3(1.45f, 0.26f, 2.85f);
        [SerializeField] private Vector3 pathEnd = new Vector3(3.95f, 0.10f, 5.4f);

        [Header("Pose")]
        [SerializeField, Min(0.05f)] private float startScale = 0.34f;
        [SerializeField, Min(0.05f)] private float peakScale = 1.0f;
        [SerializeField, Min(0.05f)] private float endScale = 0.88f;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.14f;
        [SerializeField, Min(0f)] private float bobFrequency = 4.2f;
        [SerializeField, Min(0f)] private float twistAmplitude = 10.5f;
        [SerializeField, Min(0f)] private float twistFrequency = 1.55f;

        private readonly List<Renderer> _renderers = new List<Renderer>(8);
        private readonly List<Renderer> _assignedRenderers = new List<Renderer>(3);
        private MaterialPropertyBlock _propertyBlock;

        private Renderer _bodyRenderer;
        private Renderer _coreRenderer;
        private Renderer _shellRenderer;
        private Vector3 _originLocalPosition;
        private Quaternion _originLocalRotation;
        private Vector3 _originLocalScale;
        private bool _initialized;
        private bool _playing;
        private float _playbackTime;
        private float _loopWait;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int FlowScaleId = Shader.PropertyToID("_FlowScale");
        private static readonly int FresnelPowerId = Shader.PropertyToID("_FresnelPower");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int CoreMixId = Shader.PropertyToID("_CoreMix");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

        public float TotalDuration => chargeDuration + flightDuration + lingerDuration;
        public bool IsPlaying => _playing;

        private void Awake()
        {
            EnsureInitialized();
            if (playOnAwake)
                PlayEffect();
            else
                PreviewAtTime(0f);
        }

        private void Update()
        {
            if (!_playing || !_initialized)
                return;

            _playbackTime += Time.deltaTime;
            float totalDuration = TotalDuration;
            if (_playbackTime <= totalDuration)
            {
                EvaluateVisual(_playbackTime);
                return;
            }

            if (loop)
            {
                _loopWait += Time.deltaTime;
                if (_loopWait < loopDelay)
                    return;

                PlayEffect();
                return;
            }

            _playing = false;
            EvaluateVisual(totalDuration);
            if (destroyOnComplete)
                Destroy(gameObject);
        }

        public void ConfigureGeneratedAssets(Material body, Material core, Material shell)
        {
            bodyMaterial = body;
            coreMaterial = core;
            shellMaterial = shell;
            if (_initialized)
                ApplyMaterials();
        }

        public void ConfigurePlayback(bool autoPlay, bool shouldLoop, bool shouldDestroy)
        {
            playOnAwake = autoPlay;
            loop = shouldLoop;
            destroyOnComplete = shouldDestroy;
        }

        public void ConfigurePath(Vector3 start, Vector3 controlA, Vector3 controlB, Vector3 end)
        {
            pathStart = start;
            pathControlA = controlA;
            pathControlB = controlB;
            pathEnd = end;
        }

        public void PlayEffect()
        {
            if (!EnsureInitialized())
                return;

            _playing = true;
            _loopWait = 0f;
            _playbackTime = 0f;
            EvaluateVisual(0f);
        }

        public void StopEffect()
        {
            _playing = false;
            _loopWait = 0f;
        }

        public void PreviewAtTime(float time)
        {
            if (!EnsureInitialized())
                return;

            _playing = false;
            _loopWait = 0f;
            _playbackTime = Mathf.Max(0f, time);
            EvaluateVisual(_playbackTime);
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
                return true;

            _originLocalPosition = transform.localPosition;
            _originLocalRotation = transform.localRotation;
            _originLocalScale = transform.localScale;
            _propertyBlock = new MaterialPropertyBlock();

            CollectRenderers();
            ApplyMaterials();

            _initialized = true;
            return true;
        }

        private void CollectRenderers()
        {
            _renderers.Clear();
            _assignedRenderers.Clear();
            _bodyRenderer = null;
            _coreRenderer = null;
            _shellRenderer = null;

            Renderer[] children = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Renderer renderer = children[i];
                if (renderer == null)
                    continue;

                _renderers.Add(renderer);
                string lowerName = renderer.gameObject.name.ToLowerInvariant();
                if (_bodyRenderer == null && !lowerName.Contains("core") && !lowerName.Contains("shell"))
                    _bodyRenderer = renderer;
                else if (_coreRenderer == null && lowerName.Contains("core"))
                    _coreRenderer = renderer;
                else if (_shellRenderer == null && lowerName.Contains("shell"))
                    _shellRenderer = renderer;
            }

            if (_bodyRenderer == null && _renderers.Count > 0)
                _bodyRenderer = _renderers[0];
            if (_coreRenderer == null && _renderers.Count > 1)
                _coreRenderer = _renderers[1];
            if (_shellRenderer == null && _renderers.Count > 2)
                _shellRenderer = _renderers[2];
        }

        private void ApplyMaterials()
        {
            ApplyMaterial(_bodyRenderer, bodyMaterial);
            ApplyMaterial(_coreRenderer, coreMaterial);
            ApplyMaterial(_shellRenderer, shellMaterial);

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    skinnedMeshRenderer.updateWhenOffscreen = true;
                    skinnedMeshRenderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 10f);
                }
            }
        }

        private void ApplyMaterial(Renderer renderer, Material material)
        {
            if (renderer == null || material == null)
                return;

            renderer.sharedMaterial = material;
            if (!_assignedRenderers.Contains(renderer))
                _assignedRenderers.Add(renderer);
        }

        private void EvaluateVisual(float time)
        {
            float totalDuration = Mathf.Max(0.001f, TotalDuration);
            float normalized = Mathf.Clamp01(time / totalDuration);

            float chargeT = Ease01(Mathf.Clamp01(time / Mathf.Max(0.001f, chargeDuration)));
            float flightT = Ease01(Mathf.Clamp01((time - chargeDuration) / Mathf.Max(0.001f, flightDuration)));
            float lingerT = Ease01(Mathf.Clamp01((time - chargeDuration - flightDuration) / Mathf.Max(0.001f, lingerDuration)));

            Vector3 motion = Bezier(pathStart, pathControlA, pathControlB, pathEnd, normalized);
            motion.y += Mathf.Sin(time * bobFrequency) * bobAmplitude;
            motion.x += Mathf.Sin(time * bobFrequency * 0.71f) * bobAmplitude * 0.25f;
            motion.z += Mathf.Cos(time * bobFrequency * 0.58f) * bobAmplitude * 0.18f;

            transform.localPosition = _originLocalPosition + motion;
            transform.localRotation = _originLocalRotation * Quaternion.Euler(
                Mathf.Sin(time * twistFrequency) * twistAmplitude * 0.22f,
                Mathf.Sin(time * twistFrequency * 0.83f) * twistAmplitude,
                Mathf.Sin(time * twistFrequency * 1.17f) * twistAmplitude * 0.28f);

            float scaleFactor = Mathf.Lerp(startScale, peakScale, chargeT);
            scaleFactor = Mathf.Lerp(scaleFactor, endScale, Mathf.Max(flightT, lingerT * 0.5f));
            scaleFactor *= 1f + Mathf.Sin(time * 5.5f) * 0.03f;
            transform.localScale = _originLocalScale * scaleFactor;

            UpdateRendererTint(_bodyRenderer, new Color(0.10f, 0.62f, 1.00f, Mathf.Lerp(0.18f, 0.42f, chargeT) * (1f - lingerT * 0.32f)), 1.4f + flightT * 0.7f);
            UpdateRendererTint(_coreRenderer, new Color(0.76f, 0.96f, 1.00f, Mathf.Lerp(0.28f, 0.88f, flightT) * (1f - lingerT * 0.20f)), 2.2f + chargeT * 1.1f);
            UpdateRendererTint(_shellRenderer, new Color(0.58f, 0.90f, 1.00f, Mathf.Lerp(0.10f, 0.26f, flightT) * (1f - lingerT * 0.26f)), 0.9f + chargeT * 0.5f);
        }

        private void UpdateRendererTint(Renderer renderer, Color color, float emissionMultiplier)
        {
            if (renderer == null)
                return;

            _propertyBlock.Clear();
            Color baseColor = color;
            Color coreColor = Color.Lerp(color, Color.white, 0.78f);
            Color glowColor = Color.Lerp(color, new Color(0.52f, 0.90f, 1.00f, 1f), 0.72f);

            _propertyBlock.SetColor(BaseColorId, baseColor);
            _propertyBlock.SetColor(CoreColorId, coreColor);
            _propertyBlock.SetColor(GlowColorId, glowColor);
            _propertyBlock.SetFloat(OpacityId, Mathf.Clamp(color.a * 3.6f, 0f, 2f));
            _propertyBlock.SetFloat(FlowSpeedId, emissionMultiplier * 1.35f);
            _propertyBlock.SetFloat(FlowScaleId, 2.4f + emissionMultiplier * 0.66f);
            _propertyBlock.SetFloat(FresnelPowerId, 2.6f + emissionMultiplier * 0.72f);
            _propertyBlock.SetFloat(NoiseStrengthId, Mathf.Clamp01(0.12f + color.a * 1.65f));
            _propertyBlock.SetFloat(CoreMixId, Mathf.Clamp01(0.28f + color.a * 1.9f));
            _propertyBlock.SetFloat(EmissionStrengthId, emissionMultiplier);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0
                   + 3f * uu * t * p1
                   + 3f * u * tt * p2
                   + ttt * p3;
        }

        private static float Ease01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
