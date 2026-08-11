using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DragonSwordQiEffect.WaterDragonQi.Editor
{
    public static class WaterDragonQiBuilder
    {
        private const string RootFolder = "Assets/DragonSwordQi/WaterDragonQi";
        private const string GeneratedFolder = RootFolder + "/Generated";
        private const string ModelFolder = GeneratedFolder + "/Models";
        private const string MaterialFolder = GeneratedFolder + "/Materials";
        private const string PrefabFolder = GeneratedFolder + "/Prefabs";
        private const string PreviewFolder = RootFolder + "/Preview";
        private const string ShaderPath = RootFolder + "/Shaders/WaterDragonQiEnergy.shader";
        private const string PrefabPath = PrefabFolder + "/WaterDragonQi.prefab";
        private const string PreviewImagePath = PreviewFolder + "/WaterDragonQiPreview.png";
        private const string ImportedModelPath = ModelFolder + "/WaterDragonEnergy.fbx";
        private const string BakedMeshPath = ModelFolder + "/WaterDragonQiBakedMesh.asset";
        private const string SourceModelPath = @"C:\Users\xujia\Desktop\水龙.fbx";

        public static void BuildAll()
        {
            EnsureFolders();
            CopySourceModel();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new InvalidOperationException($"Missing shader asset: {ShaderPath}");

            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedModelPath);
            if (sourcePrefab == null)
                throw new InvalidOperationException($"Imported model could not be loaded: {ImportedModelPath}");

            Mesh bakedMesh = WaterDragonQiMeshBaker.BakeModelMesh(sourcePrefab, BakedMeshPath);
            if (bakedMesh == null)
                throw new InvalidOperationException($"Failed to bake mesh from model: {ImportedModelPath}");

            Material bodyMaterial = CreateWaterDragonMaterial(
                MaterialFolder + "/WaterDragonBody.mat",
                shader,
                new Color(0.10f, 0.68f, 1.00f, 0.34f),
                new Color(0.78f, 0.96f, 1.00f, 1.00f),
                new Color(0.62f, 0.90f, 1.00f, 1.00f),
                0.40f,
                1.10f,
                0.92f,
                0.72f,
                0);

            Material coreMaterial = CreateWaterDragonMaterial(
                MaterialFolder + "/WaterDragonCore.mat",
                shader,
                new Color(0.28f, 0.84f, 1.00f, 0.28f),
                new Color(0.96f, 1.00f, 1.00f, 1.00f),
                new Color(0.84f, 0.98f, 1.00f, 1.00f),
                0.28f,
                1.95f,
                1.35f,
                0.45f,
                0);

            Material shellMaterial = CreateWaterDragonMaterial(
                MaterialFolder + "/WaterDragonShell.mat",
                shader,
                new Color(0.70f, 0.90f, 1.00f, 0.18f),
                new Color(0.98f, 1.00f, 1.00f, 1.00f),
                new Color(0.92f, 0.99f, 1.00f, 1.00f),
                0.18f,
                0.76f,
                0.72f,
                1.35f,
                0);

            GameObject prefabRoot = CreateDragonPrefabRoot(bakedMesh, bodyMaterial, coreMaterial, shellMaterial);
            try
            {
                GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                if (prefabAsset == null)
                    throw new InvalidOperationException($"Failed to save prefab: {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log(
                "[WaterDragonQi] Build completed. "
                + $"Prefab: {PrefabPath}, Mesh: {BakedMeshPath}");
        }

        private static void EnsureFolders()
        {
            EnsureAssetFolder(RootFolder);
            EnsureAssetFolder(GeneratedFolder);
            EnsureAssetFolder(ModelFolder);
            EnsureAssetFolder(MaterialFolder);
            EnsureAssetFolder(PrefabFolder);
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

        private static void CopySourceModel()
        {
            if (!File.Exists(SourceModelPath))
                throw new FileNotFoundException($"Source model not found: {SourceModelPath}");

            string destination = Path.GetFullPath(ImportedModelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("Missing destination folder."));
            File.Copy(SourceModelPath, destination, true);
        }

        private static Material CreateWaterDragonMaterial(
            string path,
            Shader shader,
            Color bodyColor,
            Color coreColor,
            Color glowColor,
            float opacity,
            float emissionStrength,
            float coreBias,
            float shellBias,
            int cull)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    enableInstancing = true,
                    renderQueue = 3000
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BodyColor", bodyColor);
            material.SetColor("_CoreColor", coreColor);
            material.SetColor("_GlowColor", glowColor);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_LayerAlpha", 1f);
            material.SetFloat("_FlowSpeed", 2.2f);
            material.SetFloat("_FlowScale", 3.2f);
            material.SetFloat("_WaveAmplitude", 0.12f);
            material.SetFloat("_WaveFrequency", 10.4f);
            material.SetFloat("_TwistAmount", 12.5f);
            material.SetFloat("_FresnelPower", 3.1f);
            material.SetFloat("_NoiseStrength", 0.22f);
            material.SetFloat("_EmissionStrength", emissionStrength);
            material.SetFloat("_CoreBias", coreBias);
            material.SetFloat("_ShellBias", shellBias);
            material.SetFloat("_Cull", cull);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateDragonPrefabRoot(
            Mesh dragonMesh,
            Material bodyMaterial,
            Material coreMaterial,
            Material shellMaterial)
        {
            GameObject root = new GameObject("WaterDragonQi");
            var controller = root.AddComponent<DragonSwordQiEffect.WaterDragonQi.WaterDragonQiDragonController>();
            controller.ConfigureGeneratedAssets(dragonMesh, bodyMaterial, coreMaterial, shellMaterial);
            controller.ConfigurePlayback(false, false, true);
            controller.ConfigurePath(
                new Vector3(-0.26f, -0.05f, 0f),
                new Vector3(0.10f, 0.44f, 1.06f),
                new Vector3(1.64f, 0.31f, 3.12f),
                new Vector3(4.32f, 0.16f, 5.72f));
            return root;
        }

        private static Material CreateMaterial(
            string path,
            Shader shader,
            Color baseColor,
            Color coreColor,
            Color glowColor,
            float opacity,
            float flowSpeed,
            float flowScale,
            float fresnelPower,
            float noiseStrength,
            float coreMix,
            float emissionStrength,
            int cull)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    enableInstancing = true,
                    renderQueue = 3000
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_CoreColor", coreColor);
            material.SetColor("_GlowColor", glowColor);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_FlowSpeed", flowSpeed);
            material.SetFloat("_FlowScale", flowScale);
            material.SetFloat("_FresnelPower", fresnelPower);
            material.SetFloat("_NoiseStrength", noiseStrength);
            material.SetFloat("_CoreMix", coreMix);
            material.SetFloat("_EmissionStrength", emissionStrength);
            material.SetFloat("_Cull", cull);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrefabRoot(Material bodyMaterial, Material coreMaterial, Material shellMaterial)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedModelPath);
            if (sourcePrefab == null)
                throw new InvalidOperationException($"Imported model could not be loaded: {ImportedModelPath}");

            float instanceScale = DetermineModelScale(sourcePrefab, 60f);

            GameObject root = new GameObject("WaterDragonQi");
            var controller = root.AddComponent<DragonSwordQiEffect.WaterDragonQi.WaterDragonQiController>();
            controller.ConfigureGeneratedAssets(bodyMaterial, coreMaterial, shellMaterial);
            controller.ConfigurePlayback(false, false, true);
            controller.ConfigurePath(
                new Vector3(-0.22f, -0.02f, 0f),
                new Vector3(0.12f, 0.32f, 1.12f),
                new Vector3(1.58f, 0.22f, 3.02f),
                new Vector3(4.18f, 0.08f, 5.55f));

            GameObject body = InstantiateModelInstance(sourcePrefab, "WaterDragonBody", root.transform, Vector3.one * instanceScale, Quaternion.identity);
            GameObject core = InstantiateModelInstance(sourcePrefab, "WaterDragonCore", root.transform, Vector3.one * instanceScale * 0.94f, Quaternion.identity);
            GameObject shell = InstantiateModelInstance(sourcePrefab, "WaterDragonShell", root.transform, Vector3.one * instanceScale * 1.05f, Quaternion.identity);

            AssignMaterials(body, bodyMaterial);
            AssignMaterials(core, coreMaterial);
            AssignMaterials(shell, shellMaterial);

            root.transform.localScale = Vector3.one;
            return root;
        }

        private static GameObject InstantiateModelInstance(GameObject sourcePrefab, string name, Transform parent, Vector3 scale, Quaternion rotation)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Failed to instantiate model prefab: {sourcePrefab.name}");

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = rotation;
            instance.transform.localScale = scale;
            return instance;
        }

        private static void AssignMaterials(GameObject instance, Material material)
        {
            if (instance == null || material == null)
                return;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.sharedMaterial = material;
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

        private static void CapturePreview(Material bodyMaterial, Material coreMaterial, Material shellMaterial)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedModelPath);
            if (sourcePrefab == null)
                throw new InvalidOperationException($"Imported model could not be loaded: {ImportedModelPath}");

            float instanceScale = DetermineModelScale(sourcePrefab, 60f);

            Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.01f, 0.02f, 0.03f, 1f);
                RenderSettings.fog = false;

                var root = new GameObject("PreviewRoot");
                GameObject body = InstantiateModelInstance(sourcePrefab, "PreviewBody", root.transform, Vector3.one * instanceScale, Quaternion.identity);
                GameObject core = InstantiateModelInstance(sourcePrefab, "PreviewCore", root.transform, Vector3.one * instanceScale * 0.94f, Quaternion.identity);
                GameObject shell = InstantiateModelInstance(sourcePrefab, "PreviewShell", root.transform, Vector3.one * instanceScale * 1.05f, Quaternion.identity);
                AssignMaterials(body, bodyMaterial);
                AssignMaterials(core, coreMaterial);
                AssignMaterials(shell, shellMaterial);

                var controller = root.AddComponent<DragonSwordQiEffect.WaterDragonQi.WaterDragonQiController>();
                controller.ConfigureGeneratedAssets(bodyMaterial, coreMaterial, shellMaterial);
                controller.ConfigurePlayback(false, false, false);
                controller.ConfigurePath(
                    new Vector3(-0.22f, -0.02f, 0f),
                    new Vector3(0.12f, 0.32f, 1.12f),
                    new Vector3(1.58f, 0.22f, 3.02f),
                    new Vector3(4.18f, 0.08f, 5.55f));
                controller.PreviewAtTime(0.82f);

                GameObject cameraObject = new GameObject("PreviewCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.fieldOfView = 24f;

                Bounds bounds = CalculateCombinedBounds(root);
                FrameCamera(camera, bounds);

                var lightKey = new GameObject("KeyLight").AddComponent<Light>();
                lightKey.type = LightType.Directional;
                lightKey.color = new Color(0.68f, 0.84f, 1.0f, 1f);
                lightKey.intensity = 1.5f;
                lightKey.transform.rotation = Quaternion.Euler(44f, -34f, 0f);

                var lightFill = new GameObject("FillLight").AddComponent<Light>();
                lightFill.type = LightType.Point;
                lightFill.color = new Color(0.12f, 0.74f, 1.0f, 1f);
                lightFill.intensity = 24f;
                lightFill.range = 18f;
                lightFill.transform.position = new Vector3(0.5f, 1.1f, -1.8f);

                const int width = 1400;
                const int height = 1400;
                var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
                {
                    name = "WaterDragonQiPreviewRT",
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
                    File.WriteAllBytes(Path.GetFullPath(PreviewImagePath), png);
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                    Object.DestroyImmediate(texture);
                    Object.DestroyImmediate(root);
                    Object.DestroyImmediate(cameraObject);
                    Object.DestroyImmediate(lightKey.gameObject);
                    Object.DestroyImmediate(lightFill.gameObject);
                }

                AssetDatabase.ImportAsset(PreviewImagePath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static Bounds CalculateCombinedBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.one * 0.5f);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
                bounds = new Bounds(root.transform.position, Vector3.one);

            return bounds;
        }

        private static float DetermineModelScale(GameObject sourcePrefab, float targetMaxExtent)
        {
            GameObject probe = Object.Instantiate(sourcePrefab);
            try
            {
                Bounds bounds = CalculateCombinedBounds(probe);
                float sourceMaxExtent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                if (sourceMaxExtent <= 0.0001f)
                    return 1f;

                float scale = targetMaxExtent / sourceMaxExtent;
                return scale;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static void FrameCamera(Camera camera, Bounds bounds)
        {
            Vector3 extents = bounds.extents;
            float maxExtent = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            float distance = Mathf.Max(2.5f, maxExtent * 3.2f);
            Vector3 direction = new Vector3(0.95f, 0.40f, -1.0f).normalized;

            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center + new Vector3(0f, extents.y * 0.08f, 0f));
            camera.nearClipPlane = Mathf.Max(0.03f, distance * 0.02f);
            camera.farClipPlane = Mathf.Max(50f, distance * 10f);
        }
    }
}
