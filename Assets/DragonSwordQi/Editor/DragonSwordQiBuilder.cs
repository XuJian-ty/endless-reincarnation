using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DragonSwordQiEffect.Editor
{
    public static class DragonSwordQiBuilder
    {
        private const string RootFolder = "Assets/DragonSwordQi";
        private const string GeneratedFolder = RootFolder + "/Generated";
        private const string MaterialFolder = GeneratedFolder + "/Materials";
        private const string AudioFolder = GeneratedFolder + "/Audio";
        private const string PreviewFolder = RootFolder + "/Preview";
        private const string PrefabPath = RootFolder + "/DragonSwordQi.prefab";
        private const string DemoScenePath = RootFolder + "/DragonSwordQiDemo.unity";
        private const string VolumeProfilePath = GeneratedFolder + "/DragonSwordQiVolume.asset";
        private const string PreviewImagePath = PreviewFolder + "/DragonSwordQiPreview.png";

        public static void BuildAll()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Shader energyShader = RequireShader("DragonSwordQi/DragonEnergy");
            Shader trailShader = RequireShader("DragonSwordQi/DragonTrail");
            Shader particleShader = RequireShader("DragonSwordQi/DragonParticle");
            Shader shockwaveShader = RequireShader("DragonSwordQi/DragonShockwave");
            ValidateShader(energyShader);
            ValidateShader(trailShader);
            ValidateShader(particleShader);
            ValidateShader(shockwaveShader);

            Material bodyMaterial = CreateBodyMaterial(energyShader);
            Material coreMaterial = CreateCoreMaterial(energyShader);
            Material trailMaterial = CreateTrailMaterial(trailShader);
            Material particleMaterial = CreateParticleMaterial(particleShader);
            Material shockwaveMaterial = CreateShockwaveMaterial(shockwaveShader);
            Material groundMaterial = CreateGroundMaterial();
            Material obstacleMaterial = CreateObstacleMaterial();
            Material targetMaterial = CreateTargetMaterial();

            AudioClip chargeAudio = GenerateChargeAudio();
            AudioClip flightAudio = GenerateFlightAudio();
            AudioClip impactAudio = GenerateImpactAudio();
            VolumeProfile volumeProfile = CreateVolumeProfile();

            DragonSwordQiController prefab = CreatePrefab(
                bodyMaterial,
                coreMaterial,
                trailMaterial,
                particleMaterial,
                shockwaveMaterial,
                chargeAudio,
                flightAudio,
                impactAudio);

            Scene scene = CreateDemoScene(
                prefab,
                volumeProfile,
                groundMaterial,
                obstacleMaterial,
                targetMaterial,
                out Camera demoCamera,
                out Transform spawnPoint);
            EditorSceneManager.SaveScene(scene, DemoScenePath);

            CapturePreview(LoadPrefabController(), demoCamera, spawnPoint);
            EditorSceneManager.SaveScene(scene, DemoScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AuditAssetDependencies(PrefabPath);
            AuditAssetDependencies(DemoScenePath);

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log(
                "[DragonSwordQi] Build completed. "
                + $"Prefab: {PrefabPath}, Scene: {DemoScenePath}, Preview: {PreviewImagePath}");
        }

        public static void BuildAllBatch()
        {
            try
            {
                BuildAll();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        private static void EnsureFolders()
        {
            EnsureAssetFolder(RootFolder);
            EnsureAssetFolder(GeneratedFolder);
            EnsureAssetFolder(MaterialFolder);
            EnsureAssetFolder(AudioFolder);
            EnsureAssetFolder(PreviewFolder);
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static Shader RequireShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Required shader was not found: {shaderName}");
            return shader;
        }

        private static void ValidateShader(Shader shader)
        {
            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException($"Shader has compilation errors: {shader.name}");
        }

        private static Material CreateBodyMaterial(Shader shader)
        {
            Material material = CreateOrReplaceMaterial(MaterialFolder + "/DragonBody.mat", shader);
            material.SetColor("_BaseColor", new Color(0.012f, 0.28f, 0.88f, 0.34f));
            material.SetColor("_CoreColor", new Color(0.26f, 1.18f, 1.62f, 1f));
            material.SetFloat("_FlowSpeed", 1.72f);
            material.SetFloat("_NoiseScale", 3.8f);
            material.SetFloat("_StripeDensity", 3.2f);
            material.SetFloat("_FresnelPower", 1.72f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateCoreMaterial(Shader shader)
        {
            Material material = CreateOrReplaceMaterial(MaterialFolder + "/DragonCore.mat", shader);
            material.SetColor("_BaseColor", new Color(0.18f, 0.84f, 1.28f, 0.34f));
            material.SetColor("_CoreColor", new Color(0.82f, 1.52f, 1.78f, 1f));
            material.SetFloat("_FlowSpeed", 2.45f);
            material.SetFloat("_NoiseScale", 5.2f);
            material.SetFloat("_StripeDensity", 4.8f);
            material.SetFloat("_FresnelPower", 2.65f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTrailMaterial(Shader shader)
        {
            Material material = CreateOrReplaceMaterial(MaterialFolder + "/DragonTrail.mat", shader);
            material.SetColor("_BaseColor", new Color(0.012f, 0.34f, 1.02f, 0.56f));
            material.SetColor("_CoreColor", new Color(0.62f, 1.42f, 1.72f, 1f));
            material.SetFloat("_FlowSpeed", 2.6f);
            material.SetFloat("_FlowDensity", 8.8f);
            material.SetFloat("_EdgePower", 1.42f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateParticleMaterial(Shader shader)
        {
            Material material = CreateOrReplaceMaterial(MaterialFolder + "/DragonParticle.mat", shader);
            material.SetColor("_TintColor", new Color(0.22f, 1.25f, 1.8f, 1f));
            material.SetFloat("_Softness", 1.78f);
            material.SetFloat("_StarStrength", 0.58f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateShockwaveMaterial(Shader shader)
        {
            Material material = CreateOrReplaceMaterial(MaterialFolder + "/DragonShockwave.mat", shader);
            material.SetColor("_RingColor", new Color(0.03f, 0.72f, 1.7f, 0.82f));
            material.SetColor("_CoreColor", new Color(1.1f, 2.2f, 2.6f, 1f));
            material.SetFloat("_RingRadius", 0.64f);
            material.SetFloat("_RingWidth", 0.115f);
            material.SetFloat("_RayStrength", 0.7f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateGroundMaterial()
        {
            Shader shader = RequireShader("Universal Render Pipeline/Lit");
            Material material = CreateOrReplaceMaterial(MaterialFolder + "/DemoGround.mat", shader);
            material.SetColor("_BaseColor", new Color(0.12f, 0.38f, 0.52f, 1f));
            material.SetFloat("_Metallic", 0.22f);
            material.SetFloat("_Smoothness", 0.66f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateObstacleMaterial()
        {
            Shader shader = RequireShader("Universal Render Pipeline/Lit");
            Material material = CreateOrReplaceMaterial(MaterialFolder + "/DemoObstacle.mat", shader);
            material.SetColor("_BaseColor", new Color(0.075f, 0.25f, 0.34f, 1f));
            material.SetFloat("_Metallic", 0.08f);
            material.SetFloat("_Smoothness", 0.42f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTargetMaterial()
        {
            Shader shader = RequireShader("Universal Render Pipeline/Lit");
            Material material = CreateOrReplaceMaterial(MaterialFolder + "/DemoTarget.mat", shader);
            material.SetColor("_BaseColor", new Color(0.18f, 0.035f, 0.03f, 1f));
            material.SetColor("_EmissionColor", new Color(1.4f, 0.08f, 0.015f, 1f));
            material.EnableKeyword("_EMISSION");
            material.SetFloat("_Metallic", 0.12f);
            material.SetFloat("_Smoothness", 0.48f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrReplaceMaterial(string path, Shader shader)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = shader;
                existing.enableInstancing = true;
                return existing;
            }

            var material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path),
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static DragonSwordQiController CreatePrefab(
            Material body,
            Material core,
            Material trail,
            Material particle,
            Material shockwave,
            AudioClip charge,
            AudioClip flight,
            AudioClip impact)
        {
            var root = new GameObject("DragonSwordQi");
            try
            {
                DragonSwordQiController controller = root.AddComponent<DragonSwordQiController>();
                controller.ConfigureGeneratedAssets(
                    body,
                    core,
                    trail,
                    particle,
                    shockwave,
                    charge,
                    flight,
                    impact);
                controller.ConfigurePlayback(false, false, true, false);
                controller.ConfigurePath(
                    Vector3.zero,
                    new Vector3(-3.4f, 3.4f, 3.8f),
                    new Vector3(3.8f, 2.4f, 10.5f),
                    new Vector3(0f, 1.1f, 15.2f));

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException($"Failed to save prefab: {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            DragonSwordQiController prefabController =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<DragonSwordQiController>();
            if (prefabController == null)
                throw new InvalidOperationException("Generated prefab has no DragonSwordQiController.");
            return prefabController;
        }

        private static DragonSwordQiController LoadPrefabController()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            DragonSwordQiController controller = prefab != null
                ? prefab.GetComponent<DragonSwordQiController>()
                : null;
            if (controller == null)
                throw new InvalidOperationException($"Could not load generated prefab: {PrefabPath}");
            return controller;
        }

        private static VolumeProfile CreateVolumeProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath) != null)
                AssetDatabase.DeleteAsset(VolumeProfilePath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "DragonSwordQiVolume";
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.68f);
            bloom.intensity.Override(1.12f);
            bloom.scatter.Override(0.78f);
            bloom.highQualityFiltering.Override(true);

            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.38f);
            color.contrast.Override(13f);
            color.saturation.Override(18f);
            color.colorFilter.Override(new Color(0.88f, 0.96f, 1f, 1f));

            WhiteBalance whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(-8f);
            whiteBalance.tint.Override(-3f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.46f);
            vignette.rounded.Override(true);

            ChromaticAberration chromaticAberration = profile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.Override(0.055f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static Scene CreateDemoScene(
            DragonSwordQiController prefab,
            VolumeProfile volumeProfile,
            Material groundMaterial,
            Material obstacleMaterial,
            Material targetMaterial,
            out Camera demoCamera,
            out Transform spawnPoint)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DragonSwordQiDemo";

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.3f, 0.46f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.31f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.16f, 0.24f);
            RenderSettings.reflectionIntensity = 0.72f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.04f, 0.15f, 0.24f);
            RenderSettings.fogDensity = 0.008f;

            demoCamera = CreateCamera();
            CreateLighting();
            CreateVolume(volumeProfile);
            CreateDemoEnvironment(groundMaterial, obstacleMaterial, targetMaterial);

            var spawnObject = new GameObject("EffectSpawn");
            spawnPoint = spawnObject.transform;
            spawnPoint.position = new Vector3(-0.65f, 1.12f, -0.35f);
            spawnPoint.rotation = Quaternion.identity;

            var lookTargetObject = new GameObject("CameraLookTarget");
            lookTargetObject.transform.position = new Vector3(0f, 1.2f, 7.6f);

            var demoObject = new GameObject("DragonSwordQiDemoController");
            DragonSwordQiDemoController demoController =
                demoObject.AddComponent<DragonSwordQiDemoController>();
            demoController.Configure(prefab, spawnPoint, demoCamera, lookTargetObject.transform);

            return scene;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.19f, 0.3f, 1f);
            camera.fieldOfView = 47f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 180f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.transform.position = new Vector3(9.2f, 9.7f, -3.8f);
            camera.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 1.2f, 7.6f) - camera.transform.position,
                Vector3.up);

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            return camera;
        }

        private static void CreateLighting()
        {
            var keyObject = new GameObject("Moon Key Light");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.58f, 0.76f, 1f);
            key.intensity = 1.72f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

            var fillObject = new GameObject("Cyan Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.02f, 0.54f, 1f);
            fill.intensity = 11.5f;
            fill.range = 18f;
            fill.shadows = LightShadows.None;
            fill.transform.position = new Vector3(-2.5f, 4.5f, 6.5f);

            var rimObject = new GameObject("Target Rim Light");
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.color = new Color(1f, 0.16f, 0.04f);
            rim.intensity = 5.2f;
            rim.range = 10f;
            rim.shadows = LightShadows.None;
            rim.transform.position = new Vector3(0f, 2.4f, 15.2f);
        }

        private static void CreateVolume(VolumeProfile profile)
        {
            var volumeObject = new GameObject("Dragon Sword Qi Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        private static void CreateDemoEnvironment(
            Material groundMaterial,
            Material obstacleMaterial,
            Material targetMaterial)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Reflective Arena";
            ground.transform.position = new Vector3(0f, 0f, 7.2f);
            ground.transform.localScale = new Vector3(3.6f, 1f, 3.8f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float row = i / 2f;
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"ArenaPillar_{i + 1:00}";
                pillar.transform.position = new Vector3(
                    side * (5.4f + Mathf.Sin(i * 1.7f) * 0.8f),
                    0.75f + (i % 3) * 0.22f,
                    0.8f + row * 2.7f);
                pillar.transform.localScale = new Vector3(
                    0.48f + (i % 3) * 0.08f,
                    0.75f + (i % 4) * 0.28f,
                    0.48f + (i % 3) * 0.08f);
                pillar.transform.rotation = Quaternion.Euler(
                    Mathf.Sin(i) * 7f,
                    i * 23f,
                    Mathf.Cos(i * 1.4f) * 5f);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = obstacleMaterial;
            }

            for (int i = 0; i < 7; i++)
            {
                float angle = i / 7f * Mathf.PI * 2f;
                GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                target.name = $"Target_{i + 1:00}";
                target.transform.position = new Vector3(
                    Mathf.Sin(angle) * 2.1f,
                    1.15f,
                    14.8f + Mathf.Cos(angle) * 1.35f);
                target.transform.localScale = new Vector3(0.46f, 1.12f, 0.46f);
                target.GetComponent<MeshRenderer>().sharedMaterial = targetMaterial;
            }

            for (int i = 0; i < 16; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = $"GroundShard_{i + 1:00}";
                shard.transform.position = new Vector3(
                    Mathf.Sin(i * 2.17f) * (3.2f + i % 4),
                    0.08f,
                    1.2f + i * 0.82f);
                shard.transform.localScale = new Vector3(
                    0.22f + i % 3 * 0.12f,
                    0.12f + i % 2 * 0.08f,
                    0.58f + i % 5 * 0.14f);
                shard.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                shard.GetComponent<MeshRenderer>().sharedMaterial = obstacleMaterial;
            }
        }

        private static void CapturePreview(
            DragonSwordQiController prefab,
            Camera camera,
            Transform spawnPoint)
        {
            DragonSwordQiController preview = Object.Instantiate(
                prefab,
                spawnPoint.position,
                spawnPoint.rotation);
            preview.name = "DragonSwordQi_PreviewOnly";
            preview.ConfigurePlayback(false, false, false, false);
            preview.PreviewAtTime(1.05f);

            const int width = 1920;
            const int height = 1080;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
            {
                name = "DragonSwordQiPreviewRT",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                byte[] png = texture.EncodeToPNG();
                string absolutePath = Path.GetFullPath(PreviewImagePath);
                File.WriteAllBytes(absolutePath, png);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(preview.gameObject);
            }

            AssetDatabase.ImportAsset(PreviewImagePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(PreviewImagePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static AudioClip GenerateChargeAudio()
        {
            const int sampleRate = 44100;
            const float duration = 1.05f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            var random = new System.Random(24681);
            float phase = 0f;
            float filteredNoise = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                float frequency = Mathf.Lerp(82f, 720f, Mathf.Pow(t, 1.82f));
                phase += Mathf.PI * 2f * frequency / sampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filteredNoise += (noise - filteredNoise) * Mathf.Lerp(0.025f, 0.18f, t);
                float envelope = SmoothAttackRelease(t, 0.12f, 0.12f);
                float tonal = Mathf.Sin(phase) * 0.42f
                              + Mathf.Sin(phase * 2.01f + 0.6f) * 0.15f
                              + Mathf.Sin(phase * 0.503f) * 0.12f;
                float shimmer = Mathf.Sin(phase * 4.03f) * Mathf.Pow(t, 2f) * 0.08f;
                samples[i] = (tonal + shimmer + filteredNoise * 0.27f) * envelope * 0.82f;
            }

            return WriteWaveAndImport(AudioFolder + "/DragonCharge.wav", samples, sampleRate);
        }

        private static AudioClip GenerateFlightAudio()
        {
            const int sampleRate = 44100;
            const float duration = 1.42f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            var random = new System.Random(97531);
            float lowNoise = 0f;
            float slowPhase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float cutoff = 0.025f + Mathf.Sin(t * Mathf.PI) * 0.22f;
                lowNoise += (noise - lowNoise) * cutoff;
                float frequency = Mathf.Lerp(210f, 74f, t);
                slowPhase += Mathf.PI * 2f * frequency / sampleRate;
                float envelope = SmoothAttackRelease(t, 0.035f, 0.18f);
                float whoosh = lowNoise * 0.72f + noise * 0.12f;
                float body = Mathf.Sin(slowPhase) * 0.16f
                             + Mathf.Sin(slowPhase * 0.5f + 0.8f) * 0.11f;
                float pulse = 0.82f + Mathf.Sin(t * Mathf.PI * 13f) * 0.18f;
                samples[i] = (whoosh + body) * envelope * pulse * 0.92f;
            }

            return WriteWaveAndImport(AudioFolder + "/DragonFlight.wav", samples, sampleRate);
        }

        private static AudioClip GenerateImpactAudio()
        {
            const int sampleRate = 44100;
            const float duration = 0.92f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            var random = new System.Random(86420);
            float lowNoise = 0f;
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                lowNoise += (noise - lowNoise) * 0.075f;
                float frequency = Mathf.Lerp(92f, 38f, t);
                phase += Mathf.PI * 2f * frequency / sampleRate;
                float decay = Mathf.Exp(-t * 5.8f);
                float crack = noise * Mathf.Exp(-t * 24f) * 0.54f;
                float boom = Mathf.Sin(phase) * decay * 0.74f;
                float body = lowNoise * Mathf.Exp(-t * 3.6f) * 0.48f;
                float tail = Mathf.Sin(phase * 2.43f + 0.3f) * Mathf.Exp(-t * 8f) * 0.16f;
                samples[i] = Mathf.Clamp((crack + boom + body + tail) * 0.9f, -1f, 1f);
            }

            return WriteWaveAndImport(AudioFolder + "/DragonImpact.wav", samples, sampleRate);
        }

        private static AudioClip WriteWaveAndImport(string assetPath, float[] samples, int sampleRate)
        {
            string absolutePath = Path.GetFullPath(assetPath);
            using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                const short channels = 1;
                const short bitsPerSample = 16;
                int bytesPerSample = bitsPerSample / 8;
                int dataSize = samples.Length * bytesPerSample * channels;

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bytesPerSample);
                writer.Write((short)(channels * bytesPerSample));
                writer.Write(bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    short sample = (short)Mathf.RoundToInt(
                        Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                    writer.Write(sample);
                }
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer != null)
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat = AudioCompressionFormat.PCM,
                    quality = 1f,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                    preloadAudioData = true
                };
                importer.SaveAndReimport();
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
                throw new InvalidOperationException($"Failed to import generated audio: {assetPath}");
            return clip;
        }

        private static float SmoothAttackRelease(float normalizedTime, float attack, float release)
        {
            float attackValue = Mathf.Clamp01(normalizedTime / Mathf.Max(0.0001f, attack));
            float releaseValue = Mathf.Clamp01((1f - normalizedTime) / Mathf.Max(0.0001f, release));
            attackValue = attackValue * attackValue * (3f - 2f * attackValue);
            releaseValue = releaseValue * releaseValue * (3f - 2f * releaseValue);
            return attackValue * releaseValue;
        }

        private static void AuditAssetDependencies(string assetPath)
        {
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);
            List<string> invalidDependencies = dependencies
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)
                               && !path.StartsWith(RootFolder + "/", StringComparison.Ordinal)
                               && !string.Equals(path, RootFolder, StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            if (invalidDependencies.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Generated asset references project content outside {RootFolder}:\n"
                    + string.Join("\n", invalidDependencies));
            }

            Debug.Log(
                $"[DragonSwordQi] Dependency audit passed for {assetPath}. "
                + $"{dependencies.Length} dependencies, no existing project effect assets referenced.");
        }
    }
}
