using System;
using System.Collections.Generic;
using Game.Social;
using UnityEngine;

namespace Game.Presentation
{
    public sealed class HighlandWaterInteraction : MonoBehaviour
    {
        [SerializeField] private Vector3[] _riverSamples = Array.Empty<Vector3>();
        [SerializeField] private float[] _halfWidths = Array.Empty<float>();
        [SerializeField] private float _minimumWaterDepth = 0.06f;
        [SerializeField] private float _rippleSpeedThreshold = 0.55f;
        [SerializeField] private float _splashSpeedThreshold = 2.5f;

        private readonly Dictionary<Transform, ActorState> _actors = new Dictionary<Transform, ActorState>();
        private readonly HashSet<Transform> _discoveredActors = new HashSet<Transform>();
        private ParticleSystem _rippleParticles;
        private ParticleSystem _wakeParticles;
        private ParticleSystem _splashParticles;
        private float _nextActorRefreshTime;

        private sealed class ActorState
        {
            public Vector3 lastPosition;
            public float nextRippleTime;
            public float nextWakeTime;
            public float nextSplashTime;
            public bool wasInWater;
        }

        public void Configure(
            Vector3[] riverSamples,
            float[] halfWidths,
            float minimumWaterDepth,
            float rippleSpeedThreshold,
            float splashSpeedThreshold)
        {
            if (riverSamples == null || halfWidths == null || riverSamples.Length < 2 || riverSamples.Length != halfWidths.Length)
                throw new ArgumentException("高原河流水体采样数据无效。");
            if (minimumWaterDepth < 0f || rippleSpeedThreshold < 0f || splashSpeedThreshold < 0f)
                throw new ArgumentOutOfRangeException(nameof(minimumWaterDepth), "高原河流水体交互阈值不能为负数。");

            _riverSamples = riverSamples;
            _halfWidths = halfWidths;
            _minimumWaterDepth = minimumWaterDepth;
            _rippleSpeedThreshold = rippleSpeedThreshold;
            _splashSpeedThreshold = splashSpeedThreshold;
        }

        private void Awake()
        {
            CreateParticleSystems();
            RefreshActors();
        }

        private void OnDestroy()
        {
            DestroyRuntimeMaterial(_rippleParticles);
            DestroyRuntimeMaterial(_wakeParticles);
            DestroyRuntimeMaterial(_splashParticles);
        }

        private void Update()
        {
            if (_riverSamples == null || _riverSamples.Length < 2)
                return;

            if (Time.unscaledTime >= _nextActorRefreshTime)
                RefreshActors();

            float deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            foreach (KeyValuePair<Transform, ActorState> pair in _actors)
            {
                Transform actor = pair.Key;
                if (actor == null || !actor.gameObject.activeInHierarchy)
                    continue;

                ActorState state = pair.Value;
                Vector3 position = actor.position;
                Vector3 planarVelocity = Vector3.ProjectOnPlane(position - state.lastPosition, Vector3.up) / deltaTime;
                float speed = planarVelocity.magnitude;
                bool isInWater = TryGetWaterSurface(position, out Vector3 waterPosition, out float depth);

                if (isInWater)
                {
                    if (!state.wasInWater)
                    {
                        EmitRipple(waterPosition, 0.68f);
                        EmitSplash(waterPosition, planarVelocity, 4);
                        state.nextRippleTime = Time.time + 0.34f;
                        state.nextWakeTime = Time.time + 0.08f;
                        state.nextSplashTime = Time.time + 0.28f;
                    }

                    if (speed >= _rippleSpeedThreshold && Time.time >= state.nextWakeTime)
                    {
                        float speed01 = Mathf.Clamp01(speed / 7f);
                        Vector3 wakePosition = waterPosition - planarVelocity.normalized * 0.03f;
                        EmitWake(wakePosition, planarVelocity, Mathf.Lerp(1.05f, 1.42f, speed01));
                        state.nextWakeTime = Time.time + Mathf.Lerp(0.105f, 0.065f, speed01);
                    }

                    if (speed >= _rippleSpeedThreshold && Time.time >= state.nextRippleTime)
                    {
                        float speed01 = Mathf.Clamp01(speed / 7f);
                        Vector3 wakePosition = waterPosition - planarVelocity.normalized * 0.12f;
                        EmitRipple(wakePosition, Mathf.Lerp(0.66f, 0.98f, speed01));
                        state.nextRippleTime = Time.time + Mathf.Lerp(0.48f, 0.24f, speed01);
                    }

                    if (speed >= _splashSpeedThreshold && depth >= 0.16f && Time.time >= state.nextSplashTime)
                    {
                        int count = Mathf.RoundToInt(Mathf.Lerp(4f, 7f, Mathf.Clamp01(speed / 8f)));
                        EmitSplash(waterPosition, planarVelocity, count);
                        state.nextSplashTime = Time.time + Mathf.Lerp(0.38f, 0.18f, Mathf.Clamp01(speed / 8f));
                    }
                }

                state.wasInWater = isInWater;
                state.lastPosition = position;
            }
        }

        private void RefreshActors()
        {
            _nextActorRefreshTime = Time.unscaledTime + 0.75f;
            _discoveredActors.Clear();

            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].enabled)
                    _discoveredActors.Add(players[i].transform);
            }

            SocialAidRemotePlayerAvatar[] remotePlayers = FindObjectsByType<SocialAidRemotePlayerAvatar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < remotePlayers.Length; i++)
            {
                if (remotePlayers[i] != null && remotePlayers[i].enabled)
                    _discoveredActors.Add(remotePlayers[i].transform);
            }

            _actors.RemoveWhere(pair => pair.Key == null || !_discoveredActors.Contains(pair.Key));
            foreach (Transform actor in _discoveredActors)
            {
                if (_actors.ContainsKey(actor))
                    continue;
                _actors.Add(actor, new ActorState { lastPosition = actor.position });
            }
        }

        private bool TryGetWaterSurface(Vector3 actorPosition, out Vector3 waterPosition, out float depth)
        {
            waterPosition = default;
            depth = 0f;
            float closestSqrDistance = float.PositiveInfinity;

            Vector2 actorXZ = new Vector2(actorPosition.x, actorPosition.z);
            for (int i = 0; i < _riverSamples.Length - 1; i++)
            {
                Vector3 from = _riverSamples[i];
                Vector3 to = _riverSamples[i + 1];
                Vector2 fromXZ = new Vector2(from.x, from.z);
                Vector2 segment = new Vector2(to.x - from.x, to.z - from.z);
                float segmentLengthSqr = segment.sqrMagnitude;
                if (segmentLengthSqr <= 0.0001f)
                    continue;

                float t = Mathf.Clamp01(Vector2.Dot(actorXZ - fromXZ, segment) / segmentLengthSqr);
                Vector2 closestXZ = fromXZ + segment * t;
                float sqrDistance = (actorXZ - closestXZ).sqrMagnitude;
                float halfWidth = Mathf.Lerp(_halfWidths[i], _halfWidths[i + 1], t);
                if (sqrDistance > halfWidth * halfWidth || sqrDistance >= closestSqrDistance)
                    continue;

                closestSqrDistance = sqrDistance;
                float surfaceY = Mathf.Lerp(from.y, to.y, t);
                waterPosition = new Vector3(actorPosition.x, surfaceY + 0.055f, actorPosition.z);
                depth = surfaceY - actorPosition.y;
            }

            return closestSqrDistance < float.PositiveInfinity && depth >= _minimumWaterDepth;
        }

        private void CreateParticleSystems()
        {
            Shader particleShader = Shader.Find("Game/Environment/HighlandWaterInteraction");
            if (particleShader == null)
                throw new InvalidOperationException("找不到高原水体交互 Shader。");

            Material rippleMaterial = CreateParticleMaterial(
                particleShader,
                "HighlandRippleRuntime",
                new Color(0.78f, 0.9f, 0.88f, 1f),
                0f,
                0.86f,
                0.38f);
            Material wakeMaterial = CreateParticleMaterial(
                particleShader,
                "HighlandWakeRuntime",
                new Color(0.76f, 0.9f, 0.88f, 1f),
                2f,
                0.78f,
                0.42f);
            Material splashMaterial = CreateParticleMaterial(
                particleShader,
                "HighlandSplashRuntime",
                new Color(0.8f, 0.92f, 0.91f, 1f),
                1f,
                0.82f,
                0.3f);
            _rippleParticles = CreateRippleSystem(rippleMaterial);
            _wakeParticles = CreateWakeSystem(wakeMaterial);
            _splashParticles = CreateSplashSystem(splashMaterial);
        }

        private ParticleSystem CreateRippleSystem(Material material)
        {
            var effectObject = new GameObject("玩家涉水波纹");
            effectObject.transform.SetParent(transform, false);
            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 36;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.72f, 0.98f);
            main.startSpeed = 0f;
            main.startSize = 0.58f;
            main.startColor = Color.white;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.68f),
                new Keyframe(0.45f, 1.16f),
                new Keyframe(1f, 1.5f)));
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.78f, 0.9f, 0.9f), 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.76f, 0.12f),
                    new GradientAlphaKey(0.44f, 0.62f),
                    new GradientAlphaKey(0f, 1f),
                },
            });

            ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = CreateRingMesh();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private ParticleSystem CreateWakeSystem(Material material)
        {
            var effectObject = new GameObject("玩家涉水尾流");
            effectObject.transform.SetParent(transform, false);
            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.76f);
            main.startSpeed = 0f;
            main.startSize = 1.12f;
            main.startRotation3D = true;
            main.startColor = Color.white;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.78f),
                new Keyframe(0.5f, 1.12f),
                new Keyframe(1f, 1.36f)));
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.68f, 0.86f, 0.86f), 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.68f, 0.12f),
                    new GradientAlphaKey(0.42f, 0.66f),
                    new GradientAlphaKey(0f, 1f),
                },
            });

            ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = CreateWakeMesh();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private ParticleSystem CreateSplashSystem(Material material)
        {
            var effectObject = new GameObject("玩家涉水水花");
            effectObject.transform.SetParent(transform, false);
            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 72;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.46f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.048f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.82f, 0.94f, 0.94f, 0.72f), Color.white);
            main.gravityModifier = 1.25f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.58f, 0.82f, 0.86f), 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.5f, 0.58f),
                    new GradientAlphaKey(0f, 1f),
                },
            });

            ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 0.82f;
            renderer.cameraVelocityScale = 0f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private void EmitRipple(Vector3 position, float size)
        {
            if (_rippleParticles == null)
                return;
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                startSize = size,
                startColor = new Color(0.84f, 0.94f, 0.92f, 0.76f),
            };
            _rippleParticles.Emit(emit, 1);
        }

        private void EmitSplash(Vector3 position, Vector3 planarVelocity, int count)
        {
            if (_splashParticles == null)
                return;

            float speed = planarVelocity.magnitude;
            Vector3 moveDirection = speed > 0.05f ? planarVelocity / speed : Vector3.forward;
            Vector3 sideDirection = Vector3.Cross(Vector3.up, moveDirection);
            for (int i = 0; i < count; i++)
            {
                float speedFactor = Mathf.Lerp(0.72f, 1.15f, Mathf.Clamp01(speed / 8f));
                float sideSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                float sideSpeed = sideSign * UnityEngine.Random.Range(0.52f, 1.24f) * speedFactor;
                float wakeSpeed = UnityEngine.Random.Range(-0.78f, -0.18f) * speedFactor;
                var emit = new ParticleSystem.EmitParams
                {
                    position = position + sideDirection * sideSign * UnityEngine.Random.Range(0.1f, 0.28f)
                                        - moveDirection * UnityEngine.Random.Range(0.02f, 0.18f)
                                        + Vector3.up * UnityEngine.Random.Range(0.015f, 0.1f),
                    velocity = sideDirection * sideSpeed + moveDirection * wakeSpeed + Vector3.up * UnityEngine.Random.Range(0.9f, 1.9f),
                    startSize = UnityEngine.Random.Range(0.018f, 0.048f),
                    startLifetime = UnityEngine.Random.Range(0.24f, 0.46f),
                    startColor = new Color(0.86f, 0.95f, 0.94f, UnityEngine.Random.Range(0.64f, 0.82f)),
                };
                _splashParticles.Emit(emit, 1);
            }
        }

        private void EmitWake(Vector3 position, Vector3 planarVelocity, float size)
        {
            if (_wakeParticles == null || planarVelocity.sqrMagnitude <= 0.0025f)
                return;

            Vector3 direction = planarVelocity.normalized;
            var emit = new ParticleSystem.EmitParams
            {
                position = position - direction * 0.18f,
                rotation3D = new Vector3(0f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f),
                startSize = size,
                startColor = new Color(0.84f, 0.95f, 0.92f, 0.74f),
            };
            _wakeParticles.Emit(emit, 1);
        }

        private static Material CreateParticleMaterial(
            Shader shader,
            string name,
            Color color,
            float effectMode,
            float opacity,
            float softness)
        {
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            material.SetColor("_TintColor", color);
            material.SetFloat("_EffectMode", effectMode);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_Softness", softness);
            return material;
        }

        private static Mesh CreateRingMesh()
        {
            const int segments = 64;
            var vertices = new Vector3[segments * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float irregularity = Mathf.Sin(angle * 3f + 0.7f) * 0.006f + Mathf.Sin(angle * 7f - 1.9f) * 0.003f;
                vertices[i * 2] = direction * (0.458f + irregularity);
                vertices[i * 2 + 1] = direction * (0.5f + irregularity);
                uvs[i * 2] = new Vector2(0f, i / (float)segments);
                uvs[i * 2 + 1] = new Vector2(1f, i / (float)segments);
                int next = (i + 1) % segments;
                int triangle = i * 6;
                triangles[triangle] = i * 2;
                triangles[triangle + 1] = next * 2;
                triangles[triangle + 2] = i * 2 + 1;
                triangles[triangle + 3] = i * 2 + 1;
                triangles[triangle + 4] = next * 2;
                triangles[triangle + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = "HighlandRippleRing", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateWakeMesh()
        {
            const int pointsPerArm = 6;
            var vertices = new Vector3[pointsPerArm * 4];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(pointsPerArm - 1) * 12];
            int triangleIndex = 0;
            for (int arm = 0; arm < 2; arm++)
            {
                float side = arm == 0 ? -1f : 1f;
                int vertexOffset = arm * pointsPerArm * 2;
                for (int i = 0; i < pointsPerArm; i++)
                {
                    float t = i / (float)(pointsPerArm - 1);
                    Vector3 point = new Vector3(side * (0.08f + 0.42f * t + 0.18f * t * t), 0f, 0.08f - 1.12f * t);
                    Vector3 tangent = new Vector3(side * (0.42f + 0.36f * t), 0f, -1.12f).normalized;
                    Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);
                    float halfThickness = Mathf.Lerp(0.035f, 0.012f, t);
                    int vertex = vertexOffset + i * 2;
                    vertices[vertex] = point - normal * halfThickness;
                    vertices[vertex + 1] = point + normal * halfThickness;
                    uvs[vertex] = new Vector2(0f, t);
                    uvs[vertex + 1] = new Vector2(1f, t);

                    if (i >= pointsPerArm - 1)
                        continue;
                    int next = vertex + 2;
                    triangles[triangleIndex++] = vertex;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = vertex + 1;
                    triangles[triangleIndex++] = vertex + 1;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = next + 1;
                }
            }

            var mesh = new Mesh { name = "HighlandWaterWake", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyRuntimeMaterial(ParticleSystem particles)
        {
            if (particles == null)
                return;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                Destroy(renderer.sharedMaterial);
        }
    }

    internal static class HighlandWaterDictionaryExtensions
    {
        public static void RemoveWhere<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, Func<KeyValuePair<TKey, TValue>, bool> predicate)
        {
            var keys = new List<TKey>();
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            {
                if (predicate(pair))
                    keys.Add(pair.Key);
            }
            for (int i = 0; i < keys.Count; i++)
                dictionary.Remove(keys[i]);
        }
    }
}
