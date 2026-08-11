using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonSwordQiEffect.WaterDragonQi
{
    [DisallowMultipleComponent]
    public sealed class WaterDragonQiDragonController : MonoBehaviour
    {
        private static readonly int BodyColorId = Shader.PropertyToID("_BodyColor");
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int LayerAlphaId = Shader.PropertyToID("_LayerAlpha");
        private static readonly int RevealFrontId = Shader.PropertyToID("_RevealFront");
        private static readonly int RevealWidthId = Shader.PropertyToID("_RevealWidth");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int FlowScaleId = Shader.PropertyToID("_FlowScale");
        private static readonly int FlowPhaseId = Shader.PropertyToID("_FlowPhase");
        private static readonly int WaveAmplitudeId = Shader.PropertyToID("_WaveAmplitude");
        private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");
        private static readonly int TwistAmountId = Shader.PropertyToID("_TwistAmount");
        private static readonly int FresnelPowerId = Shader.PropertyToID("_FresnelPower");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

        [Header("Generated Assets")]
        [SerializeField] private Mesh dragonMesh;
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material coreMaterial;
        [SerializeField] private Material shellMaterial;

        [Header("Playback")]
        [SerializeField, Min(0.05f)] private float chargeDuration = 0.34f;
        [SerializeField, Min(0.1f)] private float flightDuration = 1.16f;
        [SerializeField, Min(0.05f)] private float lingerDuration = 0.46f;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool loop;
        [SerializeField] private bool destroyOnComplete = true;
        [SerializeField, Min(0f)] private float loopDelay = 0.5f;

        [Header("Path")]
        [SerializeField] private Vector3 pathStart = new Vector3(-0.28f, -0.06f, 0f);
        [SerializeField] private Vector3 pathControlA = new Vector3(0.15f, 0.46f, 1.18f);
        [SerializeField] private Vector3 pathControlB = new Vector3(1.42f, 0.34f, 3.05f);
        [SerializeField] private Vector3 pathEnd = new Vector3(4.28f, 0.18f, 5.85f);

        [Header("Motion")]
        [SerializeField, Min(0.05f)] private float startScale = 0.38f;
        [SerializeField, Min(0.05f)] private float peakScale = 1.06f;
        [SerializeField, Min(0.05f)] private float endScale = 0.94f;
        [SerializeField, Min(0f)] private float pathBobAmplitude = 0.11f;
        [SerializeField, Min(0f)] private float pathBobFrequency = 3.8f;
        [SerializeField, Min(0f)] private float bodyWaveAmplitude = 0.14f;
        [SerializeField, Min(0f)] private float bodyWaveFrequency = 10.4f;
        [SerializeField, Min(0f)] private float bodyTwistAmplitude = 12.5f;
        [SerializeField, Min(0f)] private float bodyTwistFrequency = 1.25f;
        [SerializeField, Min(0f)] private float ghostTimeOffset = 0.08f;
        [SerializeField, Min(0f)] private float ghostScaleA = 0.96f;
        [SerializeField, Min(0f)] private float ghostScaleB = 0.90f;
        [SerializeField, Min(0f)] private float ghostAlphaA = 0.36f;
        [SerializeField, Min(0f)] private float ghostAlphaB = 0.18f;

        private readonly List<DragonLayer> _layers = new List<DragonLayer>(3);
        private MaterialPropertyBlock _propertyBlock;
        private Transform _visualRoot;
        private Vector3 _originLocalPosition;
        private Quaternion _originLocalRotation;
        private Vector3 _originLocalScale;
        private bool _initialized;
        private bool _playing;
        private float _playbackTime;
        private float _loopWait;

        public float TotalDuration => chargeDuration + flightDuration + lingerDuration;

        public bool IsPlaying => _playing;

        public void ConfigureGeneratedAssets(Mesh mesh, Material body, Material core, Material shell)
        {
            dragonMesh = mesh;
            bodyMaterial = body;
            coreMaterial = core;
            shellMaterial = shell;
            if (_initialized)
                RebuildVisuals();
        }

        public void ConfigureGeneratedAssets(Material body, Material core, Material shell)
        {
            bodyMaterial = body;
            coreMaterial = core;
            shellMaterial = shell;
            if (_initialized)
                RebuildVisuals();
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

        private void Awake()
        {
            if (!EnsureInitialized())
                return;

            if (playOnAwake)
                PlayEffect();
            else
                PreviewAtTime(0f);
        }

        private void Update()
        {
            if (!_initialized || !_playing)
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

            if (!ValidateAssets())
                return false;

            _propertyBlock = new MaterialPropertyBlock();
            _originLocalPosition = transform.localPosition;
            _originLocalRotation = transform.localRotation;
            _originLocalScale = transform.localScale;

            RebuildVisuals();
            _initialized = true;
            return true;
        }

        private bool ValidateAssets()
        {
            if (dragonMesh != null
                && bodyMaterial != null
                && coreMaterial != null
                && shellMaterial != null)
            {
                return true;
            }

            Debug.LogError(
                "WaterDragonQiDragonController requires the baked dragon mesh and all generated materials. "
                + "Run Tools/Dragon Sword Qi/Build Water Dragon Qi first.",
                this);
            return false;
        }

        private void RebuildVisuals()
        {
            for (int i = 0; i < _layers.Count; i++)
                DestroyLayer(_layers[i]);
            _layers.Clear();

            if (_visualRoot != null)
                DestroyChild(_visualRoot);

            _visualRoot = CreateChild("VisualRoot", transform);

            _layers.Add(CreateLayer(
                "DragonLayer_Main",
                0f,
                1f,
                1f,
                Vector3.zero,
                0f,
                0f,
                0));

            _layers.Add(CreateLayer(
                "DragonLayer_GhostA",
                -ghostTimeOffset,
                ghostAlphaA,
                ghostScaleA,
                new Vector3(-0.12f, 0.02f, 0f),
                -0.08f,
                1.6f,
                -3));

            _layers.Add(CreateLayer(
                "DragonLayer_GhostB",
                -ghostTimeOffset * 2f,
                ghostAlphaB,
                ghostScaleB,
                new Vector3(-0.24f, -0.03f, 0f),
                -0.16f,
                3.1f,
                -6));
        }

        private DragonLayer CreateLayer(
            string name,
            float timeOffset,
            float alphaMultiplier,
            float scaleMultiplier,
            Vector3 localOffset,
            float revealOffset,
            float flowSeed,
            int sortingBase)
        {
            Transform layerRoot = CreateChild(name, _visualRoot);
            layerRoot.localPosition = localOffset;
            layerRoot.localRotation = Quaternion.identity;
            layerRoot.localScale = Vector3.one;

            CreateMeshRenderer(
                "Body",
                layerRoot,
                dragonMesh,
                bodyMaterial,
                out Transform bodyTransform,
                out MeshRenderer bodyRenderer);
            CreateMeshRenderer(
                "Core",
                layerRoot,
                dragonMesh,
                coreMaterial,
                out Transform coreTransform,
                out MeshRenderer coreRenderer);
            CreateMeshRenderer(
                "Shell",
                layerRoot,
                dragonMesh,
                shellMaterial,
                out Transform shellTransform,
                out MeshRenderer shellRenderer);

            bodyTransform.localScale = Vector3.one;
            coreTransform.localScale = Vector3.one * 0.92f;
            shellTransform.localScale = Vector3.one * 1.06f;

            shellRenderer.sortingOrder = sortingBase;
            bodyRenderer.sortingOrder = sortingBase + 1;
            coreRenderer.sortingOrder = sortingBase + 2;

            ConfigureRenderer(bodyRenderer);
            ConfigureRenderer(coreRenderer);
            ConfigureRenderer(shellRenderer);

            return new DragonLayer
            {
                Root = layerRoot,
                BodyRenderer = bodyRenderer,
                CoreRenderer = coreRenderer,
                ShellRenderer = shellRenderer,
                TimeOffset = timeOffset,
                AlphaMultiplier = alphaMultiplier,
                ScaleMultiplier = scaleMultiplier,
                LocalOffset = localOffset,
                RevealOffset = revealOffset,
                FlowSeed = flowSeed
            };
        }

        private void EvaluateVisual(float time)
        {
            DragonState state = EvaluateState(time);
            transform.localPosition = _originLocalPosition + state.Position;
            transform.localRotation = _originLocalRotation * state.Rotation;
            transform.localScale = _originLocalScale * state.Scale;

            for (int i = 0; i < _layers.Count; i++)
            {
                DragonLayer layer = _layers[i];
                DragonState layerState = EvaluateState(Mathf.Max(0f, time + layer.TimeOffset));
                ApplyLayerState(layer, layerState);
            }
        }

        private void ApplyLayerState(DragonLayer layer, DragonState state)
        {
            if (layer.Root == null)
                return;

            layer.Root.localPosition = layer.LocalOffset;
            layer.Root.localRotation = Quaternion.identity;
            layer.Root.localScale = Vector3.one * layer.ScaleMultiplier;

            float alpha = state.Alpha * layer.AlphaMultiplier;
            float revealFront = Mathf.Clamp01(state.RevealFront + layer.RevealOffset);
            float flowPhase = state.FlowPhase + layer.FlowSeed;

            ApplyRendererState(layer.BodyRenderer, alpha, revealFront, state, flowPhase);
            ApplyRendererState(layer.CoreRenderer, alpha, revealFront, state, flowPhase);
            ApplyRendererState(layer.ShellRenderer, alpha, revealFront, state, flowPhase);
        }

        private void ApplyRendererState(
            Renderer renderer,
            float alpha,
            float revealFront,
            DragonState state,
            float flowPhase)
        {
            if (renderer == null)
                return;

            _propertyBlock.Clear();
            _propertyBlock.SetFloat(LayerAlphaId, alpha);
            _propertyBlock.SetFloat(RevealFrontId, revealFront);
            _propertyBlock.SetFloat(RevealWidthId, state.RevealWidth);
            _propertyBlock.SetFloat(FlowSpeedId, state.FlowSpeed);
            _propertyBlock.SetFloat(FlowScaleId, state.FlowScale);
            _propertyBlock.SetFloat(FlowPhaseId, flowPhase);
            _propertyBlock.SetFloat(WaveAmplitudeId, state.WaveAmplitude);
            _propertyBlock.SetFloat(WaveFrequencyId, state.WaveFrequency);
            _propertyBlock.SetFloat(TwistAmountId, state.TwistAmount);
            _propertyBlock.SetFloat(FresnelPowerId, state.FresnelPower);
            _propertyBlock.SetFloat(NoiseStrengthId, state.NoiseStrength);
            _propertyBlock.SetFloat(EmissionStrengthId, state.EmissionStrength);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private DragonState EvaluateState(float time)
        {
            float totalDuration = Mathf.Max(0.001f, TotalDuration);
            float normalized = Mathf.Clamp01(time / totalDuration);

            float chargeT = Ease01(Mathf.Clamp01(time / Mathf.Max(0.001f, chargeDuration)));
            float flightT = Ease01(Mathf.Clamp01((time - chargeDuration) / Mathf.Max(0.001f, flightDuration)));
            float lingerT = Ease01(Mathf.Clamp01((time - chargeDuration - flightDuration) / Mathf.Max(0.001f, lingerDuration)));

            Vector3 position = Bezier(pathStart, pathControlA, pathControlB, pathEnd, normalized);
            position.y += Mathf.Sin(time * pathBobFrequency) * pathBobAmplitude;
            position.x += Mathf.Sin(time * pathBobFrequency * 0.67f) * pathBobAmplitude * 0.28f;
            position.z += Mathf.Cos(time * pathBobFrequency * 0.91f) * pathBobAmplitude * 0.18f;

            Vector3 tangent = BezierTangent(pathStart, pathControlA, pathControlB, pathEnd, normalized);
            Vector3 upReference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.92f
                ? Vector3.right
                : Vector3.up;
            Vector3 side = Vector3.Cross(upReference, tangent).normalized;
            Vector3 up = Vector3.Cross(tangent, side).normalized;

            float roll = Mathf.Sin(time * bodyTwistFrequency) * bodyTwistAmplitude * 0.16f
                         + Mathf.Sin(time * bodyTwistFrequency * 0.73f + normalized * 5.3f) * bodyTwistAmplitude * 0.05f;

            Quaternion rotation = Quaternion.LookRotation(tangent, up)
                                  * Quaternion.Euler(
                                      Mathf.Sin(time * bodyTwistFrequency * 0.88f) * bodyTwistAmplitude * 0.18f,
                                      Mathf.Sin(time * bodyTwistFrequency * 1.12f) * bodyTwistAmplitude * 0.56f,
                                      roll);

            float scale = Mathf.Lerp(startScale, peakScale, chargeT);
            scale = Mathf.Lerp(scale, endScale, Mathf.Max(flightT, lingerT * 0.5f));
            scale *= 1f + Mathf.Sin(time * 5.3f) * 0.025f;

            float revealFront = Mathf.Lerp(0.035f, 0.18f, chargeT);
            revealFront = Mathf.Lerp(revealFront, 1.06f, flightT);

            float revealWidth = Mathf.Lerp(0.06f, 0.11f, Mathf.Max(chargeT, flightT));
            float flowSpeed = Mathf.Lerp(1.7f, 3.45f, flightT);
            float flowScale = Mathf.Lerp(2.4f, 4.8f, flightT);
            float waveAmplitude = Mathf.Lerp(0.06f, bodyWaveAmplitude, flightT);
            float waveFrequency = bodyWaveFrequency;
            float twistAmount = Mathf.Lerp(4.6f, bodyTwistAmplitude, flightT);
            float fresnelPower = Mathf.Lerp(2.3f, 4.4f, chargeT);
            float noiseStrength = Mathf.Lerp(0.11f, 0.24f, flightT);
            float emissionStrength = Mathf.Lerp(1.05f, 1.68f, flightT);
            float alpha = Mathf.Lerp(0.14f, 1f, Mathf.Max(chargeT, flightT));
            alpha *= 1f - lingerT * 0.84f;

            return new DragonState
            {
                Position = position,
                Rotation = rotation,
                Scale = scale,
                RevealFront = revealFront,
                RevealWidth = revealWidth,
                FlowPhase = time * flowSpeed,
                FlowSpeed = flowSpeed,
                FlowScale = flowScale,
                WaveAmplitude = waveAmplitude,
                WaveFrequency = waveFrequency,
                TwistAmount = twistAmount,
                FresnelPower = fresnelPower,
                NoiseStrength = noiseStrength,
                EmissionStrength = emissionStrength,
                Alpha = alpha
            };
        }

        private void ConfigureRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name);
            Transform childTransform = child.transform;
            childTransform.SetParent(parent, false);
            childTransform.localPosition = Vector3.zero;
            childTransform.localRotation = Quaternion.identity;
            childTransform.localScale = Vector3.one;
            return childTransform;
        }

        private static void DestroyChild(Transform child)
        {
            if (child != null)
                DestroyGameObject(child.gameObject);
        }

        private static void DestroyLayer(DragonLayer layer)
        {
            if (layer.Root != null)
                DestroyGameObject(layer.Root.gameObject);
        }

        private static void DestroyGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(gameObject);
            else
                Object.DestroyImmediate(gameObject);
        }

        private static void CreateMeshRenderer(
            string name,
            Transform parent,
            Mesh mesh,
            Material material,
            out Transform childTransform,
            out MeshRenderer meshRenderer)
        {
            childTransform = CreateChild(name, parent);
            var meshFilter = childTransform.gameObject.AddComponent<MeshFilter>();
            meshRenderer = childTransform.gameObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
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

        private static Vector3 BezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            Vector3 tangent = 3f * u * u * (p1 - p0)
                              + 6f * u * t * (p2 - p1)
                              + 3f * t * t * (p3 - p2);
            return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector3.forward;
        }

        private static float Ease01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private struct DragonState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public float RevealFront;
            public float RevealWidth;
            public float FlowPhase;
            public float FlowSpeed;
            public float FlowScale;
            public float WaveAmplitude;
            public float WaveFrequency;
            public float TwistAmount;
            public float FresnelPower;
            public float NoiseStrength;
            public float EmissionStrength;
            public float Alpha;
        }

        private struct DragonLayer
        {
            public Transform Root;
            public MeshRenderer BodyRenderer;
            public MeshRenderer CoreRenderer;
            public MeshRenderer ShellRenderer;
            public float TimeOffset;
            public float AlphaMultiplier;
            public float ScaleMultiplier;
            public Vector3 LocalOffset;
            public float RevealOffset;
            public float FlowSeed;
        }
    }
}
