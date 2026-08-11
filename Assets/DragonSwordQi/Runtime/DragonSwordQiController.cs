using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DragonSwordQiEffect
{
    [DisallowMultipleComponent]
    public sealed class DragonSwordQiController : MonoBehaviour
    {
        private static readonly int EffectAlphaId = Shader.PropertyToID("_EffectAlpha");

        [Header("Generated Assets")]
        [SerializeField] private Material dragonBodyMaterial;
        [SerializeField] private Material dragonCoreMaterial;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private Material particleMaterial;
        [SerializeField] private Material shockwaveMaterial;
        [SerializeField] private AudioClip chargeAudio;
        [SerializeField] private AudioClip flightAudio;
        [SerializeField] private AudioClip impactAudio;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float chargeDuration = 0.42f;
        [SerializeField, Min(0.1f)] private float flightDuration = 1.28f;
        [SerializeField, Min(0.05f)] private float lingerDuration = 0.62f;
        [SerializeField, Min(0f)] private float loopDelay = 0.55f;

        [Header("Shape")]
        [SerializeField, Min(16)] private int bodySegments = 72;
        [SerializeField, Range(5, 16)] private int radialSides = 10;
        [SerializeField, Min(0.05f)] private float bodyRadius = 0.56f;
        [SerializeField, Range(0.12f, 0.75f)] private float bodyPathSpan = 0.62f;
        [SerializeField, Min(0f)] private float bodyWaveAmplitude = 0.82f;
        [SerializeField, Min(0f)] private float bodyWaveFrequency = 9f;
        [SerializeField] private Vector3 pathStart = Vector3.zero;
        [SerializeField] private Vector3 pathControlA = new Vector3(-1.8f, 2.3f, 4.5f);
        [SerializeField] private Vector3 pathControlB = new Vector3(2.3f, 1.2f, 9.8f);
        [SerializeField] private Vector3 pathEnd = new Vector3(0f, 0.65f, 15.5f);

        [Header("Playback")]
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool loop;
        [SerializeField] private bool destroyOnComplete = true;
        [SerializeField] private bool enableCameraShake;
        [SerializeField, Range(0f, 0.5f)] private float cameraShakeStrength = 0.16f;

        private readonly List<Mesh> _runtimeMeshes = new List<Mesh>();
        private readonly List<Renderer> _dragonRenderers = new List<Renderer>();
        private readonly List<ParticleSystem> _particleSystems = new List<ParticleSystem>();

        private Transform _visualRoot;
        private Transform _headAnchor;
        private Transform[] _trailAnchors;
        private TrailRenderer[] _trails;
        private LineRenderer _spineLine;
        private LineRenderer[] _whiskers;
        private MeshRenderer _crescentRenderer;
        private Transform _crescentTransform;
        private MeshRenderer _chargeRingRenderer;
        private Transform _chargeRingTransform;
        private MeshRenderer[] _impactRingRenderers;
        private Transform[] _impactRingTransforms;
        private ParticleSystem _flightSparks;
        private ParticleSystem _flightMist;
        private ParticleSystem _impactBurst;
        private ParticleSystem _impactMist;
        private AudioSource _audioSource;

        private DragonSwordQiProceduralMesh.TubeMesh _bodyTube;
        private DragonSwordQiProceduralMesh.TubeMesh _coreTube;
        private Vector3[] _bodyCenters;
        private Vector3[] _bodyTangents;
        private Vector3[] _bodyUpAxes;
        private float[] _bodyRadii;
        private float[] _coreRadii;
        private MaterialPropertyBlock _propertyBlock;
        private Coroutine _cameraShakeRoutine;

        private float _playbackTime;
        private float _loopWait;
        private bool _initialized;
        private bool _playing;
        private bool _flightAudioTriggered;
        private bool _impactTriggered;
        private bool _isPreviewEvaluation;

        public event Action Impacted;

        public float TotalDuration => chargeDuration + flightDuration + lingerDuration;

        public bool IsPlaying => _playing;

        private void Awake()
        {
            if (!EnsureInitialized())
                return;

            if (playOnAwake)
                PlayEffect();
            else
                EvaluateVisual(0f, false);
        }

        private void Update()
        {
            if (!_playing || !_initialized)
                return;

            _playbackTime += Time.deltaTime;
            EvaluateVisual(_playbackTime, true);

            if (_playbackTime < TotalDuration)
                return;

            if (loop)
            {
                _loopWait += Time.deltaTime;
                if (_loopWait >= loopDelay)
                    PlayEffect();
                return;
            }

            _playing = false;
            SetFlightEmission(false);
            SetTrailEmission(false);
            if (destroyOnComplete)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_cameraShakeRoutine != null)
                StopCoroutine(_cameraShakeRoutine);

            _bodyTube?.Dispose();
            _coreTube?.Dispose();
            _bodyTube = null;
            _coreTube = null;

            for (int i = 0; i < _runtimeMeshes.Count; i++)
                DestroyRuntimeObject(_runtimeMeshes[i]);
            _runtimeMeshes.Clear();
        }

        public void ConfigureGeneratedAssets(
            Material body,
            Material core,
            Material trail,
            Material particle,
            Material shockwave,
            AudioClip charge,
            AudioClip flight,
            AudioClip impact)
        {
            dragonBodyMaterial = body;
            dragonCoreMaterial = core;
            trailMaterial = trail;
            particleMaterial = particle;
            shockwaveMaterial = shockwave;
            chargeAudio = charge;
            flightAudio = flight;
            impactAudio = impact;
        }

        public void ConfigurePlayback(bool autoPlay, bool shouldLoop, bool shouldDestroy, bool cameraShake)
        {
            playOnAwake = autoPlay;
            loop = shouldLoop;
            destroyOnComplete = shouldDestroy;
            enableCameraShake = cameraShake;
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

            _playbackTime = 0f;
            _loopWait = 0f;
            _playing = true;
            _flightAudioTriggered = false;
            _impactTriggered = false;
            ResetRuntimeState();
            EvaluateVisual(0f, true);

            if (chargeAudio != null)
                _audioSource.PlayOneShot(chargeAudio, 0.82f);
        }

        public void StopEffect()
        {
            _playing = false;
            SetFlightEmission(false);
            SetTrailEmission(false);
            StopAndClearParticles();
        }

        public void PreviewAtTime(float time)
        {
            if (!EnsureInitialized())
                return;

            _isPreviewEvaluation = true;
            _playing = false;
            _flightAudioTriggered = false;
            _impactTriggered = false;
            ResetRuntimeState();

            float targetTime = Mathf.Clamp(time, 0f, TotalDuration);
            const float previewStep = 1f / 45f;
            float simulatedTime = 0f;
            while (simulatedTime < targetTime)
            {
                float step = Mathf.Min(previewStep, targetTime - simulatedTime);
                simulatedTime += step;
                EvaluateVisual(simulatedTime, false);
                SimulateParticles(step);
            }

            if (targetTime <= 0f)
                EvaluateVisual(0f, false);
            else
                EvaluateVisual(targetTime, false);

            SetTrailEmission(false);
            for (int i = 0; i < _trails.Length; i++)
                _trails[i].Clear();

            _playbackTime = targetTime;
            _isPreviewEvaluation = false;
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
                return true;

            if (!ValidateGeneratedAssets())
                return false;

            bodySegments = Mathf.Max(16, bodySegments);
            radialSides = Mathf.Clamp(radialSides, 5, 16);

            _propertyBlock = new MaterialPropertyBlock();
            _bodyCenters = new Vector3[bodySegments + 1];
            _bodyTangents = new Vector3[bodySegments + 1];
            _bodyUpAxes = new Vector3[bodySegments + 1];
            _bodyRadii = new float[bodySegments + 1];
            _coreRadii = new float[bodySegments + 1];

            CreateVisualHierarchy();
            CreateParticleSystems();
            CreateAudioSource();

            _initialized = true;
            ResetRuntimeState();
            return true;
        }

        private bool ValidateGeneratedAssets()
        {
            if (dragonBodyMaterial != null
                && dragonCoreMaterial != null
                && trailMaterial != null
                && particleMaterial != null
                && shockwaveMaterial != null)
            {
                return true;
            }

            Debug.LogError(
                "DragonSwordQiController requires all five generated materials. "
                + "Run Tools/Dragon Sword Qi/Build All before using the effect.",
                this);
            return false;
        }

        private void CreateVisualHierarchy()
        {
            _visualRoot = CreateChild("VisualRoot", transform);

            _bodyTube = new DragonSwordQiProceduralMesh.TubeMesh(
                "DragonSwordQi_Body_Runtime",
                bodySegments,
                radialSides);
            _coreTube = new DragonSwordQiProceduralMesh.TubeMesh(
                "DragonSwordQi_Core_Runtime",
                bodySegments,
                Mathf.Max(5, radialSides - 2));

            CreateMeshRenderer(
                "EnergyBody",
                _visualRoot,
                _bodyTube.Mesh,
                dragonBodyMaterial,
                out _,
                out MeshRenderer bodyRenderer);
            _dragonRenderers.Add(bodyRenderer);

            CreateMeshRenderer(
                "EnergyCore",
                _visualRoot,
                _coreTube.Mesh,
                dragonCoreMaterial,
                out _,
                out MeshRenderer coreRenderer);
            _dragonRenderers.Add(coreRenderer);

            CreateHeadHierarchy();
            CreateCrescent();
            CreateSpineLine();
            CreateTrailRenderers();
            CreateChargeRing();
            CreateImpactRings();
        }

        private void CreateHeadHierarchy()
        {
            _headAnchor = CreateChild("DragonHead", _visualRoot);
            Mesh headMesh = RegisterRuntimeMesh(DragonSwordQiProceduralMesh.CreateDragonHeadMesh());
            CreateMeshRenderer(
                "HeadMesh",
                _headAnchor,
                headMesh,
                dragonBodyMaterial,
                out Transform headMeshTransform,
                out MeshRenderer headRenderer);
            headMeshTransform.localScale = new Vector3(1f, 1f, 1.08f);
            _dragonRenderers.Add(headRenderer);

            Mesh eyeMesh = RegisterRuntimeMesh(DragonSwordQiProceduralMesh.CreateUvSphereMesh());
            CreateEye("Eye_Left", eyeMesh, new Vector3(-0.43f, 0.19f, 0.49f));
            CreateEye("Eye_Right", eyeMesh, new Vector3(0.43f, 0.19f, 0.49f));

            Mesh hornMesh = RegisterRuntimeMesh(DragonSwordQiProceduralMesh.CreateHornMesh());
            CreateHorn("Horn_Left", hornMesh, new Vector3(-0.36f, 0.38f, -0.25f), -17f);
            CreateHorn("Horn_Right", hornMesh, new Vector3(0.36f, 0.38f, -0.25f), 17f);

            _whiskers = new[]
            {
                CreateWhisker("Whisker_Left", -1f),
                CreateWhisker("Whisker_Right", 1f)
            };
        }

        private void CreateEye(string name, Mesh mesh, Vector3 localPosition)
        {
            CreateMeshRenderer(
                name,
                _headAnchor,
                mesh,
                dragonCoreMaterial,
                out Transform eyeTransform,
                out MeshRenderer eyeRenderer);
            eyeTransform.localPosition = localPosition;
            eyeTransform.localScale = new Vector3(0.105f, 0.13f, 0.08f);
            _dragonRenderers.Add(eyeRenderer);
        }

        private void CreateHorn(string name, Mesh mesh, Vector3 localPosition, float zRotation)
        {
            CreateMeshRenderer(
                name,
                _headAnchor,
                mesh,
                dragonCoreMaterial,
                out Transform hornTransform,
                out MeshRenderer hornRenderer);
            hornTransform.localPosition = localPosition;
            hornTransform.localRotation = Quaternion.Euler(-21f, 0f, zRotation);
            hornTransform.localScale = Vector3.one * 0.78f;
            _dragonRenderers.Add(hornRenderer);
        }

        private LineRenderer CreateWhisker(string name, float side)
        {
            Transform whiskerTransform = CreateChild(name, _headAnchor);
            var line = whiskerTransform.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.positionCount = 14;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.09f),
                new Keyframe(0.32f, 0.055f),
                new Keyframe(1f, 0f));
            line.material = trailMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 8;

            for (int i = 0; i < line.positionCount; i++)
            {
                float t = (float)i / (line.positionCount - 1);
                Vector3 point = new Vector3(
                    side * (0.36f + t * 2.3f),
                    -0.10f + Mathf.Sin(t * Mathf.PI * 1.2f) * 0.34f - t * 0.12f,
                    0.76f - t * 1.52f);
                line.SetPosition(i, point);
            }

            _dragonRenderers.Add(line);
            return line;
        }

        private void CreateCrescent()
        {
            Mesh crescentMesh = RegisterRuntimeMesh(DragonSwordQiProceduralMesh.CreateCrescentMesh());
            CreateMeshRenderer(
                "SwordQiCrescent",
                _visualRoot,
                crescentMesh,
                trailMaterial,
                out _crescentTransform,
                out _crescentRenderer);
            _crescentRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _crescentRenderer.receiveShadows = false;
            _crescentRenderer.sortingOrder = 3;
        }

        private void CreateSpineLine()
        {
            Transform spineTransform = CreateChild("LuminousSpine", _visualRoot);
            _spineLine = spineTransform.gameObject.AddComponent<LineRenderer>();
            _spineLine.useWorldSpace = false;
            _spineLine.loop = false;
            _spineLine.alignment = LineAlignment.View;
            _spineLine.textureMode = LineTextureMode.Tile;
            _spineLine.positionCount = bodySegments + 1;
            _spineLine.numCornerVertices = 2;
            _spineLine.numCapVertices = 2;
            _spineLine.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.42f, 0.13f),
                new Keyframe(0.86f, 0.23f),
                new Keyframe(1f, 0.07f));
            _spineLine.material = dragonCoreMaterial;
            _spineLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _spineLine.receiveShadows = false;
            _spineLine.sortingOrder = 5;
            _dragonRenderers.Add(_spineLine);
        }

        private void CreateTrailRenderers()
        {
            _trailAnchors = new Transform[3];
            _trails = new TrailRenderer[3];
            float[] widths = { 0.72f, 0.26f, 0.26f };
            float[] times = { 0.42f, 0.31f, 0.31f };

            for (int i = 0; i < _trails.Length; i++)
            {
                Transform trailTransform = CreateChild($"WakeTrail_{i + 1}", _visualRoot);
                var trail = trailTransform.gameObject.AddComponent<TrailRenderer>();
                trail.time = times[i];
                trail.minVertexDistance = 0.045f;
                trail.widthMultiplier = widths[i];
                trail.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.28f, 0.78f),
                    new Keyframe(1f, 0f));
                trail.material = trailMaterial;
                trail.textureMode = LineTextureMode.Tile;
                trail.alignment = LineAlignment.View;
                trail.numCornerVertices = 4;
                trail.numCapVertices = 4;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.autodestruct = false;
                trail.emitting = false;
                trail.sortingOrder = i == 0 ? 1 : 2;

                _trailAnchors[i] = trailTransform;
                _trails[i] = trail;
                _dragonRenderers.Add(trail);
            }
        }

        private void CreateChargeRing()
        {
            Mesh quadMesh = RegisterRuntimeMesh(DragonSwordQiProceduralMesh.CreateQuadMesh("DragonSwordQi_ChargeQuad"));
            CreateMeshRenderer(
                "ChargeVortex",
                _visualRoot,
                quadMesh,
                shockwaveMaterial,
                out _chargeRingTransform,
                out _chargeRingRenderer);
            _chargeRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _chargeRingRenderer.receiveShadows = false;
            _chargeRingRenderer.sortingOrder = 0;
        }

        private void CreateImpactRings()
        {
            Mesh quadMesh = RegisterRuntimeMesh(DragonSwordQiProceduralMesh.CreateQuadMesh("DragonSwordQi_ImpactQuad"));
            _impactRingTransforms = new Transform[3];
            _impactRingRenderers = new MeshRenderer[3];
            for (int i = 0; i < _impactRingTransforms.Length; i++)
            {
                CreateMeshRenderer(
                    $"ImpactRing_{i + 1}",
                    _visualRoot,
                    quadMesh,
                    shockwaveMaterial,
                    out _impactRingTransforms[i],
                    out _impactRingRenderers[i]);
                _impactRingRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _impactRingRenderers[i].receiveShadows = false;
                _impactRingRenderers[i].sortingOrder = 6 + i;
            }
        }

        private void CreateParticleSystems()
        {
            _flightSparks = CreateFlightSparks();
            _flightMist = CreateFlightMist();
            _impactBurst = CreateImpactBurst();
            _impactMist = CreateImpactMist();

            _particleSystems.Add(_flightSparks);
            _particleSystems.Add(_flightMist);
            _particleSystems.Add(_impactBurst);
            _particleSystems.Add(_impactMist);
        }

        private ParticleSystem CreateFlightSparks()
        {
            ParticleSystem system = CreateParticleSystem("FlightSparks", 1037u);
            var main = system.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.28f, 0.92f, 1f, 0.82f),
                new Color(0.88f, 1f, 1f, 1f));
            main.maxParticles = 650;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 145f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.34f;

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 1.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);

            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient(
                new Color(0.72f, 1f, 1f, 0f),
                new Color(0.16f, 0.82f, 1f, 0.95f),
                new Color(0.12f, 0.48f, 1f, 0f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.2f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0f)));

            return system;
        }

        private ParticleSystem CreateFlightMist()
        {
            ParticleSystem system = CreateParticleSystem("FlightMist", 2089u);
            var main = system.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.82f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.86f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.16f, 0.72f, 1f, 0.16f),
                new Color(0.78f, 1f, 1f, 0.34f));
            main.maxParticles = 260;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 42f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.52f;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.72f;
            noise.frequency = 0.62f;
            noise.scrollSpeed = 0.84f;

            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient(
                new Color(0.55f, 0.95f, 1f, 0f),
                new Color(0.24f, 0.78f, 1f, 0.32f),
                new Color(0.16f, 0.38f, 0.82f, 0f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.18f),
                    new Keyframe(0.42f, 1f),
                    new Keyframe(1f, 1.45f)));

            return system;
        }

        private ParticleSystem CreateImpactBurst()
        {
            ParticleSystem system = CreateParticleSystem("ImpactBurst", 4093u);
            var main = system.main;
            main.loop = false;
            main.duration = 0.65f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.72f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 10.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.2f, 0.78f, 1f, 0.9f),
                Color.white);
            main.maxParticles = 220;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)72),
                new ParticleSystem.Burst(0.08f, (short)36)
            });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.24f;

            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient(
                new Color(0.92f, 1f, 1f, 0f),
                Color.white,
                new Color(0.08f, 0.48f, 1f, 0f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.12f, 1f),
                    new Keyframe(1f, 0f)));

            return system;
        }

        private ParticleSystem CreateImpactMist()
        {
            ParticleSystem system = CreateParticleSystem("ImpactMist", 6151u);
            var main = system.main;
            main.loop = false;
            main.duration = 0.9f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.48f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 3.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.1f, 0.62f, 1f, 0.16f),
                new Color(0.72f, 1f, 1f, 0.34f));
            main.maxParticles = 120;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)28),
                new ParticleSystem.Burst(0.12f, (short)18)
            });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.42f;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 1.15f;
            noise.frequency = 0.48f;
            noise.scrollSpeed = 0.52f;

            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient(
                new Color(0.72f, 1f, 1f, 0f),
                new Color(0.28f, 0.82f, 1f, 0.28f),
                new Color(0.08f, 0.28f, 0.65f, 0f));

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.2f),
                    new Keyframe(0.38f, 0.85f),
                    new Keyframe(1f, 1.65f)));

            return system;
        }

        private ParticleSystem CreateParticleSystem(string name, uint randomSeed)
        {
            Transform systemTransform = CreateChild(name, _visualRoot);
            var system = systemTransform.gameObject.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;

            system.useAutoRandomSeed = false;
            system.randomSeed = randomSeed;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = particleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 10;

            return system;
        }

        private void CreateAudioSource()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0.72f;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.minDistance = 4f;
            _audioSource.maxDistance = 38f;
            _audioSource.dopplerLevel = 0.2f;
        }

        private void ResetRuntimeState()
        {
            StopAndClearParticles();
            for (int i = 0; i < _trails.Length; i++)
            {
                _trails[i].emitting = false;
                _trails[i].Clear();
            }

            _flightSparks.Play(false);
            _flightMist.Play(false);
            SetFlightEmission(false);
            SetRendererAlpha(_chargeRingRenderer, 0f);
            SetRendererAlpha(_crescentRenderer, 0f);
            for (int i = 0; i < _impactRingRenderers.Length; i++)
                SetRendererAlpha(_impactRingRenderers[i], 0f);

            EvaluateBody(0f, 0f, 0f);
            SetTrailEmission(false);
        }

        private void StopAndClearParticles()
        {
            for (int i = 0; i < _particleSystems.Count; i++)
            {
                ParticleSystem system = _particleSystems[i];
                if (system == null)
                    continue;

                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Clear(false);
            }
        }

        private void EvaluateVisual(float time, bool allowFeedback)
        {
            float chargeNormalized = Mathf.Clamp01(time / chargeDuration);
            float flightNormalized = Mathf.Clamp01((time - chargeDuration) / flightDuration);
            float lingerNormalized = Mathf.Clamp01((time - chargeDuration - flightDuration) / lingerDuration);
            float chargeEase = Smooth01(chargeNormalized);
            float flightEase = 1f - Mathf.Pow(1f - flightNormalized, 3.2f);
            float fade = 1f - Smooth01(lingerNormalized);
            float headProgress = time < chargeDuration
                ? Mathf.Lerp(0.018f, 0.115f, chargeEase)
                : flightEase;

            float emergence = Mathf.Clamp01(time / Mathf.Max(0.08f, chargeDuration * 0.76f));
            float bodyAlpha = Smooth01(emergence) * fade;
            EvaluateBody(headProgress, time, bodyAlpha);

            bool flightActive = time >= chargeDuration
                                && time <= chargeDuration + flightDuration
                                && fade > 0.01f;
            UpdateTrailAnchors(flightNormalized, bodyAlpha);
            SetTrailEmission(flightActive);
            SetFlightEmission(time < chargeDuration || flightActive);

            _flightSparks.transform.localPosition = _headAnchor.localPosition;
            _flightMist.transform.localPosition = _headAnchor.localPosition;

            EvaluateChargeRing(chargeNormalized, time);
            EvaluateCrescent(flightNormalized, flightActive, fade, time);
            EvaluateImpactRings(time);

            if (!_flightAudioTriggered && time >= chargeDuration)
            {
                _flightAudioTriggered = true;
                if (allowFeedback && flightAudio != null)
                    _audioSource.PlayOneShot(flightAudio, 0.76f);
            }

            float impactMoment = chargeDuration + flightDuration * 0.86f;
            if (!_impactTriggered && time >= impactMoment)
                TriggerImpact(allowFeedback);
        }

        private void EvaluateBody(float headProgress, float time, float alpha)
        {
            int sectionCount = _bodyCenters.Length;
            for (int section = 0; section < sectionCount; section++)
            {
                float bodyU = (float)section / (sectionCount - 1);
                float pathT = headProgress - bodyPathSpan * (1f - bodyU);
                float visibility = SmoothEdge(pathT, 0f, 0.045f)
                                   * (1f - SmoothEdge(pathT, 1.015f, 1.06f));
                float clampedPathT = Mathf.Clamp01(pathT);

                Vector3 tangent = DragonSwordQiProceduralMesh.EvaluateCubicBezierTangent(
                    pathStart,
                    pathControlA,
                    pathControlB,
                    pathEnd,
                    clampedPathT);
                Vector3 basePosition = DragonSwordQiProceduralMesh.EvaluateCubicBezier(
                    pathStart,
                    pathControlA,
                    pathControlB,
                    pathEnd,
                    clampedPathT);

                Vector3 frameReference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.92f
                    ? Vector3.right
                    : Vector3.up;
                Vector3 sideAxis = Vector3.Cross(frameReference, tangent).normalized;
                Vector3 upAxis = Vector3.Cross(tangent, sideAxis).normalized;
                float bodyEnvelope = Mathf.Pow(
                    Mathf.Clamp01(Mathf.Sin(bodyU * Mathf.PI)),
                    0.58f);
                float phase = clampedPathT * bodyWaveFrequency - time * 6.8f;
                float waveStrength = bodyWaveAmplitude * bodyEnvelope;
                Vector3 waveOffset = sideAxis * Mathf.Sin(phase) * waveStrength
                                     + upAxis * Mathf.Cos(phase * 0.72f + 0.4f) * waveStrength * 0.46f;
                float pathLift = Mathf.Sin(clampedPathT * Mathf.PI) * 0.32f;

                _bodyCenters[section] = basePosition + waveOffset + Vector3.up * pathLift;
                _bodyTangents[section] = tangent;
                _bodyUpAxes[section] = upAxis;

                float tailTaper = Mathf.Pow(Mathf.Clamp01(bodyU), 0.48f);
                float neckTaper = Mathf.Lerp(1f, 0.74f, SmoothEdge(bodyU, 0.86f, 1f));
                float pulse = 1f + Mathf.Sin(time * 12f - bodyU * 9f) * 0.055f;
                _bodyRadii[section] = bodyRadius * tailTaper * neckTaper * visibility * pulse;
                _coreRadii[section] = _bodyRadii[section] * Mathf.Lerp(0.16f, 0.27f, bodyU);
            }

            _bodyTube.Update(
                _bodyCenters,
                _bodyTangents,
                _bodyRadii,
                Color.white,
                alpha,
                6.2f);
            _coreTube.Update(
                _bodyCenters,
                _bodyTangents,
                _coreRadii,
                Color.white,
                alpha,
                8.4f);

            UpdateHead(alpha, time);
            UpdateSpine(alpha);
            ApplyDragonAlpha(alpha);
        }

        private void UpdateHead(float alpha, float time)
        {
            int headIndex = _bodyCenters.Length - 1;
            Vector3 headPosition = _bodyCenters[headIndex];
            Vector3 headTangent = _bodyTangents[headIndex];
            Vector3 headUp = _bodyUpAxes[headIndex];

            _headAnchor.localPosition = headPosition;
            _headAnchor.localRotation = Quaternion.LookRotation(headTangent, headUp)
                                        * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 7.2f) * 5f);
            float headScale = Mathf.Lerp(0.58f, 1.08f, Smooth01(alpha));
            _headAnchor.localScale = Vector3.one * headScale;
        }

        private void UpdateSpine(float alpha)
        {
            for (int section = 0; section < _bodyCenters.Length; section++)
            {
                Vector3 point = _bodyCenters[section] + _bodyUpAxes[section] * _bodyRadii[section] * 0.78f;
                _spineLine.SetPosition(section, point);
            }

            SetRendererAlpha(_spineLine, alpha * 0.92f);
        }

        private void ApplyDragonAlpha(float alpha)
        {
            for (int i = 0; i < _dragonRenderers.Count; i++)
                SetRendererAlpha(_dragonRenderers[i], alpha);
        }

        private void UpdateTrailAnchors(float flightNormalized, float alpha)
        {
            int headIndex = _bodyCenters.Length - 1;
            Vector3 headPosition = _bodyCenters[headIndex];
            Vector3 sideAxis = Vector3.Cross(_bodyUpAxes[headIndex], _bodyTangents[headIndex]).normalized;
            Vector3 upAxis = _bodyUpAxes[headIndex];

            _trailAnchors[0].localPosition = headPosition - _bodyTangents[headIndex] * 0.35f;
            _trailAnchors[1].localPosition = headPosition + sideAxis * 0.62f - upAxis * 0.18f;
            _trailAnchors[2].localPosition = headPosition - sideAxis * 0.62f - upAxis * 0.18f;

            float trailAlpha = Mathf.Sin(Mathf.Clamp01(flightNormalized) * Mathf.PI) * alpha;
            for (int i = 0; i < _trails.Length; i++)
                SetRendererAlpha(_trails[i], Mathf.Lerp(0.55f, 1f, i == 0 ? 1f : 0f) * trailAlpha);
        }

        private void EvaluateChargeRing(float chargeNormalized, float time)
        {
            float ringAlpha = Mathf.Sin(chargeNormalized * Mathf.PI) * 0.92f;
            _chargeRingTransform.localPosition = pathStart + Vector3.up * 0.24f;
            Vector3 initialTangent = DragonSwordQiProceduralMesh.EvaluateCubicBezierTangent(
                pathStart,
                pathControlA,
                pathControlB,
                pathEnd,
                0f);
            _chargeRingTransform.localRotation = Quaternion.LookRotation(initialTangent, Vector3.up)
                                                 * Quaternion.Euler(0f, 0f, time * 210f);
            float scale = Mathf.Lerp(0.8f, 5.6f, Smooth01(chargeNormalized));
            _chargeRingTransform.localScale = new Vector3(scale, scale, scale);
            SetRendererAlpha(_chargeRingRenderer, ringAlpha);
        }

        private void EvaluateCrescent(float flightNormalized, bool flightActive, float fade, float time)
        {
            float appear = SmoothEdge(flightNormalized, 0.01f, 0.12f);
            float disappear = 1f - SmoothEdge(flightNormalized, 0.56f, 0.9f);
            float alpha = flightActive ? appear * disappear * fade * 0.58f : 0f;
            int headIndex = _bodyCenters.Length - 1;

            _crescentTransform.localPosition = _bodyCenters[headIndex]
                                               - _bodyTangents[headIndex] * 0.52f;
            _crescentTransform.localRotation = Quaternion.LookRotation(
                                                   _bodyTangents[headIndex],
                                                   _bodyUpAxes[headIndex])
                                               * Quaternion.Euler(0f, 0f, -16f + time * 46f);
            float scale = Mathf.Lerp(0.42f, 0.86f, Smooth01(flightNormalized));
            _crescentTransform.localScale = new Vector3(scale, scale * 0.72f, scale);
            SetRendererAlpha(_crescentRenderer, alpha);
        }

        private void EvaluateImpactRings(float time)
        {
            float impactMoment = chargeDuration + flightDuration * 0.86f;
            Vector3 impactPosition = pathEnd + Vector3.up * 0.18f;
            Vector3 impactTangent = DragonSwordQiProceduralMesh.EvaluateCubicBezierTangent(
                pathStart,
                pathControlA,
                pathControlB,
                pathEnd,
                1f);

            for (int i = 0; i < _impactRingTransforms.Length; i++)
            {
                float delay = i * 0.075f;
                float duration = 0.54f + i * 0.10f;
                float normalized = Mathf.Clamp01((time - impactMoment - delay) / duration);
                bool active = time >= impactMoment + delay && normalized < 1f;
                float alpha = active ? Mathf.Pow(1f - normalized, 1.65f) : 0f;
                float scale = Mathf.Lerp(0.5f + i * 0.35f, 7.8f + i * 2.2f, Smooth01(normalized));

                _impactRingTransforms[i].localPosition = impactPosition
                                                         - impactTangent * (0.06f + i * 0.12f);
                _impactRingTransforms[i].localRotation = i == 2
                    ? Quaternion.Euler(90f, 0f, 0f)
                    : Quaternion.LookRotation(impactTangent, Vector3.up)
                      * Quaternion.Euler(0f, 0f, i * 33f);
                _impactRingTransforms[i].localScale = Vector3.one * scale;
                SetRendererAlpha(_impactRingRenderers[i], alpha);
            }
        }

        private void TriggerImpact(bool allowFeedback)
        {
            _impactTriggered = true;
            Vector3 position = pathEnd + Vector3.up * 0.15f;
            _impactBurst.transform.localPosition = position;
            _impactMist.transform.localPosition = position;
            _impactBurst.Play(false);
            _impactMist.Play(false);

            if (allowFeedback)
            {
                if (impactAudio != null)
                    _audioSource.PlayOneShot(impactAudio, 0.95f);

                if (enableCameraShake && !_isPreviewEvaluation && isActiveAndEnabled)
                {
                    if (_cameraShakeRoutine != null)
                        StopCoroutine(_cameraShakeRoutine);
                    _cameraShakeRoutine = StartCoroutine(ShakeCamera());
                }

                Impacted?.Invoke();
            }
        }

        private IEnumerator ShakeCamera()
        {
            Camera targetCamera = Camera.main;
            if (targetCamera == null)
                yield break;

            Transform cameraTransform = targetCamera.transform;
            Vector3 initialLocalPosition = cameraTransform.localPosition;
            const float duration = 0.16f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float envelope = 1f - Mathf.Clamp01(elapsed / duration);
                Vector2 shake = UnityEngine.Random.insideUnitCircle * cameraShakeStrength * envelope;
                cameraTransform.localPosition = initialLocalPosition + new Vector3(shake.x, shake.y, 0f);
                yield return null;
            }

            cameraTransform.localPosition = initialLocalPosition;
            _cameraShakeRoutine = null;
        }

        private void SetFlightEmission(bool enabled)
        {
            if (_flightSparks != null)
            {
                var emission = _flightSparks.emission;
                emission.enabled = enabled;
            }

            if (_flightMist != null)
            {
                var emission = _flightMist.emission;
                emission.enabled = enabled;
            }
        }

        private void SetTrailEmission(bool enabled)
        {
            if (_trails == null)
                return;

            for (int i = 0; i < _trails.Length; i++)
            {
                if (_trails[i] != null)
                    _trails[i].emitting = enabled;
            }
        }

        private void SimulateParticles(float deltaTime)
        {
            for (int i = 0; i < _particleSystems.Count; i++)
            {
                ParticleSystem system = _particleSystems[i];
                if (system != null && system.isPlaying)
                    system.Simulate(deltaTime, false, false, false);
            }
        }

        private void SetRendererAlpha(Renderer targetRenderer, float alpha)
        {
            if (targetRenderer == null)
                return;

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(EffectAlphaId, Mathf.Clamp01(alpha));
            targetRenderer.SetPropertyBlock(_propertyBlock);
            _propertyBlock.Clear();
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
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private Mesh RegisterRuntimeMesh(Mesh mesh)
        {
            _runtimeMeshes.Add(mesh);
            return mesh;
        }

        private static ParticleSystem.MinMaxGradient CreateFadeGradient(
            Color start,
            Color middle,
            Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(middle, 0.28f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(middle.a, 0.24f),
                    new GradientAlphaKey(end.a, 1f)
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float SmoothEdge(float value, float edgeStart, float edgeEnd)
        {
            if (Mathf.Approximately(edgeStart, edgeEnd))
                return value >= edgeEnd ? 1f : 0f;
            return Smooth01((value - edgeStart) / (edgeEnd - edgeStart));
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
