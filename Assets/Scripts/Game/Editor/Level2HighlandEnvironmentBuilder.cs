using System;
using System.Collections.Generic;
using System.IO;
using Game.GameFlow;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class Level2HighlandEnvironmentBuilder
    {
        private const string ScenePath = "Assets/Scenes/Level_2.unity";
        private const string AssetFolder = "Assets/Scenes/Level_2/Highland";
        private const string LocalSpawnerPrefabPath = "Assets/Resources/Prefabs/LevelLocalEnemySpawner.prefab";
        private const string MarkerPrefix = "_LEVEL2_HIGHLAND_20261027_";
        private const string MarkerName = MarkerPrefix + "V22";
        private const int EliteSpawnPlanIndex = 3;
        private const int MinionSpawnPlanIndex = 4;
        private const int ChestSpawnPlanIndex = 5;
        private const int Seed = 20261027;

        private static bool _isApplying;

        private sealed class SurfaceInfo
        {
            public Bounds bounds;
            public float topY;
        }

        private sealed class LevelLayout
        {
            public Vector3[] regionCenters;
            public Vector2[] regionExtents;
            public float minZ;
            public float maxZ;
            public float minY;
            public float maxY;

            public float SampleRouteX(float z)
            {
                return SampleRouteValue(z, center => center.x);
            }

            public float SampleRouteHeight(float z)
            {
                return SampleRouteValue(z, center => center.y);
            }

            public float SampleRouteHalfWidth(float z)
            {
                if (z <= regionCenters[0].z)
                    return regionExtents[0].x;
                for (int i = 0; i < regionCenters.Length - 1; i++)
                {
                    if (z > regionCenters[i + 1].z)
                        continue;
                    float t = Mathf.InverseLerp(regionCenters[i].z, regionCenters[i + 1].z, z);
                    return Mathf.Lerp(regionExtents[i].x, regionExtents[i + 1].x, t);
                }
                return regionExtents[regionExtents.Length - 1].x;
            }

            public float RiverX(float z)
            {
                float routeLeft = SampleRouteX(z) - SampleRouteHalfWidth(z) - 46f;
                float meander = -146f + Mathf.Sin(z * 0.015f) * 25f + Mathf.Sin(z * 0.043f + 1.3f) * 8f;
                return Mathf.Min(routeLeft, meander);
            }

            public float RiverY(float z)
            {
                return SampleRouteHeight(z) - 4.2f;
            }

            private float SampleRouteValue(float z, Func<Vector3, float> selector)
            {
                if (z <= regionCenters[0].z)
                    return selector(regionCenters[0]);
                for (int i = 0; i < regionCenters.Length - 1; i++)
                {
                    Vector3 from = regionCenters[i];
                    Vector3 to = regionCenters[i + 1];
                    if (z > to.z)
                        continue;
                    return Mathf.Lerp(selector(from), selector(to), Mathf.InverseLerp(from.z, to.z, z));
                }
                return selector(regionCenters[regionCenters.Length - 1]);
            }
        }

        static Level2HighlandEnvironmentBuilder()
        {
            EditorApplication.delayCall += TryApplyToSavedScene;
        }

        [MenuItem("Tools/Game/重建第二关高原环境")]
        public static void ApplyToSavedScene()
        {
            if (_isApplying)
                return;
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException($"找不到第二关场景：{ScenePath}");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedByBuilder = !scene.IsValid() || !scene.isLoaded;
            if (!openedByBuilder && scene.isDirty)
                throw new InvalidOperationException("第二关场景有未保存修改，无法重建高原环境。");

            _isApplying = true;
            try
            {
                if (openedByBuilder)
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

                Transform environmentRoot = FindEnvironmentRoot(scene);
                BuildIntoScene(scene, environmentRoot);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException($"无法保存第二关场景：{ScenePath}");

                AssetDatabase.SaveAssets();
                Debug.Log("[Level2HighlandEnvironmentBuilder] LEVEL2_HIGHLAND_BUILD_OK");
            }
            finally
            {
                if (openedByBuilder && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                _isApplying = false;
            }
        }

        public static void BuildIntoScene(Scene scene, Transform environmentRoot)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("第二关高原环境只能写入已加载场景。");
            if (environmentRoot == null || environmentRoot.gameObject.scene != scene)
                throw new InvalidOperationException("第二关环境根节点无效。");

            EnsureFolder(AssetFolder);
            Transform terrainRoot = RequireDirectChild(environmentRoot, "00_地形基底");
            Transform routeRoot = RequireDirectChild(environmentRoot, "01_主路线");
            Transform arenaRoot = RequireDirectChild(environmentRoot, "02_战斗区域");
            Transform branchRoot = RequireDirectChild(environmentRoot, "03_支线房间");
            Transform propRoot = RequireDirectChild(environmentRoot, "04_主题装饰");
            Transform lightRoot = RequireDirectChild(environmentRoot, "05_场景灯光");
            LevelLayout layout = ReadLayout(arenaRoot);

            for (int childIndex = environmentRoot.childCount - 1; childIndex >= 0; childIndex--)
            {
                Transform child = environmentRoot.GetChild(childIndex);
                if (child.name.StartsWith(MarkerPrefix, StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var highlandObject = new GameObject(MarkerName);
            SceneManager.MoveGameObjectToScene(highlandObject, scene);
            highlandObject.transform.SetParent(environmentRoot, false);
            Transform highlandRoot = highlandObject.transform;

            Material groundMaterial = CreateGroundMaterial();
            Material rockMaterial = CreateRockMaterial();
            Material mountainMaterial = CreateMountainMaterial();
            Material shoreMaterial = CreateShoreMaterial();
            Material grassMaterial = CreateGrassMaterial();
            Material fineGrassMaterial = CreateFineGrassMaterial();
            Material flowerMaterial = CreateFlowerMaterial();
            Material riverMaterial = CreateRiverMaterial();
            Material cloudMaterial = CreateCloudMaterial();

            ReworkExistingEnvironment(terrainRoot, routeRoot, arenaRoot, branchRoot, propRoot, groundMaterial, rockMaterial);
            StyleRegionGates(scene, rockMaterial);
            BuildLandscape(highlandRoot, layout, groundMaterial);
            BuildRiver(highlandRoot, layout, shoreMaterial, riverMaterial);
            BuildGrass(highlandRoot, layout, arenaRoot, branchRoot, grassMaterial, fineGrassMaterial);
            BuildWildflowers(highlandRoot, layout, flowerMaterial);
            BuildRocks(highlandRoot, layout, rockMaterial);
            BuildRockAssets(highlandRoot, layout, rockMaterial);
            BuildCliffAssets(highlandRoot, layout, rockMaterial);
            BuildAmbientEnemySpawners(highlandRoot, layout);
            BuildChestSpawners(highlandRoot, layout);
            BuildMountains(highlandRoot, layout, mountainMaterial);
            BuildClouds(highlandRoot, layout, cloudMaterial);
            BuildWind(highlandRoot);
            ConfigureLighting(scene, highlandRoot, lightRoot, layout);
            RenderPreview(scene, layout, "Level2HighlandScenic.png", true);
            RenderPreview(scene, layout, "Level2HighlandGameplay.png", false);
        }

        private static void TryApplyToSavedScene()
        {
            if (_isApplying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            if (!File.Exists(ScenePath) || File.ReadAllText(ScenePath).Contains(MarkerName))
                return;

            try
            {
                ApplyToSavedScene();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static Transform FindEnvironmentRoot(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                string objectName = roots[i].name;
                if (objectName.StartsWith("Level_2_", StringComparison.Ordinal) &&
                    objectName.EndsWith("_Environment", StringComparison.Ordinal))
                    return roots[i].transform;
            }
            throw new InvalidOperationException("第二关场景缺少环境根节点。");
        }

        private static Transform RequireDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
                throw new InvalidOperationException($"第二关场景缺少节点：{name}");
            return child;
        }

        private static LevelLayout ReadLayout(Transform arenaRoot)
        {
            var surfaces = new List<SurfaceInfo>();
            MeshRenderer[] renderers = arenaRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (!renderer.name.EndsWith("_地面", StringComparison.Ordinal))
                    continue;
                surfaces.Add(new SurfaceInfo { bounds = renderer.bounds, topY = renderer.bounds.max.y });
            }
            surfaces.Sort((left, right) => left.bounds.center.z.CompareTo(right.bounds.center.z));
            if (surfaces.Count != 6)
                throw new InvalidOperationException($"第二关战斗地面数量错误：{surfaces.Count}，预期=6。");

            var layout = new LevelLayout
            {
                regionCenters = new Vector3[surfaces.Count],
                regionExtents = new Vector2[surfaces.Count],
                minZ = surfaces[0].bounds.center.z,
                maxZ = surfaces[surfaces.Count - 1].bounds.center.z,
                minY = surfaces[0].topY,
                maxY = surfaces[0].topY,
            };
            for (int i = 0; i < surfaces.Count; i++)
            {
                Bounds bounds = surfaces[i].bounds;
                layout.regionCenters[i] = new Vector3(bounds.center.x, surfaces[i].topY, bounds.center.z);
                layout.regionExtents[i] = new Vector2(bounds.extents.x, bounds.extents.z);
                layout.minY = Mathf.Min(layout.minY, surfaces[i].topY);
                layout.maxY = Mathf.Max(layout.maxY, surfaces[i].topY);
            }
            return layout;
        }

        private static Material CreateGroundMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandGround.mat", "Game/Environment/HighlandGround");
            material.SetColor("_BaseColor", new Color(0.31f, 0.41f, 0.13f, 1f));
            material.SetColor("_SecondaryColor", new Color(0.48f, 0.46f, 0.2f, 1f));
            material.SetColor("_SlopeColor", new Color(0.42f, 0.4f, 0.34f, 1f));
            SetGroundTextures(material, "Assets/_Wyrms/_SeamlessTextures/SeamlessTextures/GroundMoss_diff 1.png");
            material.SetFloat("_GroundTiling", 0.11f);
            material.SetFloat("_RockTiling", 0.065f);
            material.SetFloat("_TextureStrength", 0.44f);
            material.SetFloat("_SnowAmount", 0f);
            material.SetFloat("_AmbientStrength", 0.82f);
            material.SetFloat("_MacroScale", 0.016f);
            material.SetFloat("_DetailScale", 0.38f);
            material.SetFloat("_SlopeStart", 0.31f);
            material.SetFloat("_SlopeBlend", 0.4f);
            material.SetFloat("_Smoothness", 0.16f);
            material.SetFloat("_NormalVariation", 0.2f);
            return Dirty(material);
        }

        private static Material CreateRockMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandRock.mat", "Game/Environment/HighlandGround");
            material.SetColor("_BaseColor", new Color(0.54f, 0.55f, 0.47f, 1f));
            material.SetColor("_SecondaryColor", new Color(0.72f, 0.68f, 0.54f, 1f));
            material.SetColor("_SlopeColor", new Color(0.43f, 0.45f, 0.42f, 1f));
            SetGroundTextures(material, "Assets/_Wyrms/_SeamlessTextures/SeamlessTextures/Rock_03_diff.png");
            material.SetFloat("_GroundTiling", 0.075f);
            material.SetFloat("_RockTiling", 0.075f);
            material.SetFloat("_TextureStrength", 0.68f);
            material.SetFloat("_SnowAmount", 0f);
            material.SetFloat("_AmbientStrength", 0.68f);
            material.SetFloat("_MacroScale", 0.026f);
            material.SetFloat("_DetailScale", 0.61f);
            material.SetFloat("_SlopeStart", 0.18f);
            material.SetFloat("_SlopeBlend", 0.22f);
            material.SetFloat("_Smoothness", 0.24f);
            material.SetFloat("_NormalVariation", 0.34f);
            return Dirty(material);
        }

        private static Material CreateMountainMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandMountain.mat", "Game/Environment/HighlandGround");
            material.SetColor("_BaseColor", new Color(0.32f, 0.34f, 0.33f, 1f));
            material.SetColor("_SecondaryColor", new Color(0.5f, 0.49f, 0.44f, 1f));
            material.SetColor("_SlopeColor", new Color(0.27f, 0.3f, 0.31f, 1f));
            SetGroundTextures(material, "Assets/_Wyrms/_SeamlessTextures/SeamlessTextures/Rock_03_diff.png");
            material.SetFloat("_GroundTiling", 0.034f);
            material.SetFloat("_RockTiling", 0.034f);
            material.SetFloat("_TextureStrength", 0.82f);
            material.SetFloat("_SnowAmount", 0.36f);
            material.SetFloat("_SnowLine", 152f);
            material.SetFloat("_SnowFade", 17f);
            material.SetFloat("_AmbientStrength", 0.52f);
            material.SetFloat("_MacroScale", 0.008f);
            material.SetFloat("_DetailScale", 0.24f);
            material.SetFloat("_SlopeStart", 0.12f);
            material.SetFloat("_SlopeBlend", 0.34f);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_NormalVariation", 0.64f);
            return Dirty(material);
        }

        private static Material CreateShoreMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandShore.mat", "Game/Environment/HighlandGround");
            material.SetColor("_BaseColor", new Color(0.42f, 0.4f, 0.3f, 1f));
            material.SetColor("_SecondaryColor", new Color(0.7f, 0.62f, 0.43f, 1f));
            material.SetColor("_SlopeColor", new Color(0.34f, 0.35f, 0.3f, 1f));
            SetGroundTextures(material, "Assets/_Wyrms/_SeamlessTextures/BeachSoil_diff.png");
            material.SetFloat("_GroundTiling", 0.18f);
            material.SetFloat("_RockTiling", 0.1f);
            material.SetFloat("_TextureStrength", 0.52f);
            material.SetFloat("_SnowAmount", 0f);
            material.SetFloat("_AmbientStrength", 0.72f);
            material.SetFloat("_MacroScale", 0.045f);
            material.SetFloat("_DetailScale", 0.82f);
            material.SetFloat("_SlopeStart", 0.28f);
            material.SetFloat("_SlopeBlend", 0.34f);
            material.SetFloat("_Smoothness", 0.31f);
            material.SetFloat("_NormalVariation", 0.3f);
            return Dirty(material);
        }

        private static Material CreateGrassMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandGrass.mat", "Game/Environment/HighlandGrass");
            material.SetTexture("_BladeTexture", RequireTexture("Assets/Eternal Temple/Textures/Atlas/Grass_01.psd"));
            material.SetFloat("_Cutoff", 0.22f);
            material.SetFloat("_TextureStrength", 0.46f);
            material.SetColor("_BaseColor", new Color(0.09f, 0.21f, 0.035f, 1f));
            material.SetColor("_TipColor", new Color(0.42f, 0.52f, 0.15f, 1f));
            material.SetColor("_DryColor", new Color(0.58f, 0.47f, 0.2f, 1f));
            material.SetVector("_WindDirection", new Vector4(0.82f, 0f, 0.57f, 0f));
            material.SetFloat("_WindStrength", 0.56f);
            material.SetFloat("_WindSpeed", 2.15f);
            material.SetFloat("_GustScale", 0.024f);
            material.SetFloat("_GustStrength", 0.74f);
            material.SetFloat("_Translucency", 0.45f);
            material.SetFloat("_AmbientStrength", 0.78f);
            material.enableInstancing = true;
            return Dirty(material);
        }

        private static Material CreateFlowerMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandFlower.mat", "Game/Environment/HighlandFlower");
            material.SetVector("_WindDirection", new Vector4(0.82f, 0f, 0.57f, 0f));
            material.SetFloat("_WindStrength", 0.16f);
            material.SetFloat("_WindSpeed", 2.15f);
            material.SetFloat("_AmbientStrength", 0.7f);
            return Dirty(material);
        }

        private static Material CreateFineGrassMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandFineGrass.mat", "Game/Environment/HighlandFineGrass");
            material.SetColor("_BaseColor", new Color(0.026f, 0.12f, 0.012f, 1f));
            material.SetColor("_TipColor", new Color(0.3f, 0.47f, 0.085f, 1f));
            material.SetColor("_DryColor", new Color(0.45f, 0.34f, 0.08f, 1f));
            material.SetVector("_WindDirection", new Vector4(0.82f, 0f, 0.57f, 0f));
            material.SetFloat("_WindStrength", 0.24f);
            material.SetFloat("_WindSpeed", 1.85f);
            material.SetFloat("_AmbientStrength", 0.54f);
            return Dirty(material);
        }

        private static Material CreateRiverMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandRiver.mat", "Game/Environment/HighlandRiver");
            material.SetColor("_ShallowColor", new Color(0.2f, 0.5f, 0.48f, 0.66f));
            material.SetColor("_DeepColor", new Color(0.025f, 0.15f, 0.22f, 0.86f));
            material.SetColor("_SunGlintColor", new Color(1f, 0.84f, 0.58f, 1f));
            material.SetColor("_FoamColor", new Color(0.91f, 0.94f, 0.88f, 1f));
            material.SetVector("_FlowDirection", new Vector4(0.16f, 0f, -1f, 0f));
            material.SetFloat("_FlowSpeed", 0.92f);
            material.SetFloat("_WaveScale", 0.34f);
            material.SetFloat("_WaveStrength", 0.24f);
            material.SetFloat("_DepthRange", 1.65f);
            material.SetFloat("_FoamWidth", 0.56f);
            material.SetFloat("_Smoothness", 0.9f);
            return Dirty(material);
        }

        private static Material CreateCloudMaterial()
        {
            Material material = LoadOrCreateMaterial("HighlandCloud.mat", "Game/Environment/HighlandCloud");
            material.SetColor("_LightColor", new Color(1f, 0.96f, 0.88f, 1f));
            material.SetColor("_ShadowColor", new Color(0.34f, 0.43f, 0.51f, 1f));
            material.SetFloat("_CloudScale", 4.8f);
            material.SetFloat("_Density", 0.48f);
            material.SetFloat("_Softness", 0.16f);
            material.SetFloat("_Speed", 0.017f);
            material.SetVector("_Direction", new Vector4(0.82f, 0f, 0.57f, 0f));
            material.SetFloat("_Opacity", 0.46f);
            return Dirty(material);
        }

        private static void SetGroundTextures(Material material, string groundTexturePath)
        {
            material.SetTexture("_GroundTex", RequireTexture(groundTexturePath));
            material.SetTexture("_RockTex", RequireTexture("Assets/_Wyrms/_SeamlessTextures/SeamlessTextures/Rock_03_diff.png"));
            material.SetTexture("_SnowTex", RequireTexture("Assets/_Wyrms/_SeamlessTextures/SeamlessTextures/GroundSnow_dif.png"));
        }

        private static Texture2D RequireTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new InvalidOperationException($"找不到第二关高原纹理：{path}");
            return texture;
        }

        private static Material LoadOrCreateMaterial(string fileName, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"找不到第二关高原 Shader：{shaderName}");
            string path = $"{AssetFolder}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(fileName) };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            return material;
        }

        private static Material Dirty(Material material)
        {
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ReworkExistingEnvironment(
            Transform terrainRoot,
            Transform routeRoot,
            Transform arenaRoot,
            Transform branchRoot,
            Transform propRoot,
            Material groundMaterial,
            Material rockMaterial)
        {
            MeshRenderer[] terrainRenderers = terrainRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < terrainRenderers.Length; i++)
            {
                string objectName = terrainRenderers[i].name;
                if (objectName == "世界基底" || objectName.StartsWith("远景山台_", StringComparison.Ordinal) ||
                    objectName.Contains("雾墙", StringComparison.Ordinal) || objectName.Contains("悬崖雾", StringComparison.Ordinal) ||
                    objectName.Contains("下层流雾", StringComparison.Ordinal))
                    terrainRenderers[i].enabled = false;
            }

            RemoveLegacyTorchesAndChests(propRoot);
            DisableLegacyCollision(terrainRoot);
            DisableLegacyCollision(routeRoot);
            DisableLegacyCollision(arenaRoot);
            DisableLegacyCollision(branchRoot);
            DisableLegacyCollision(propRoot);

            HideLegacySurfaces(routeRoot);
            HideLegacySurfaces(arenaRoot);
            HideLegacySurfaces(branchRoot);

            Transform[] propTransforms = propRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < propTransforms.Length; i++)
            {
                Transform current = propTransforms[i];
                if (current == propRoot)
                    continue;
                if (current.name.StartsWith("主题装饰_", StringComparison.Ordinal) || current.name.Contains("程序建模地标", StringComparison.Ordinal))
                    current.gameObject.SetActive(false);
            }

            MeshRenderer[] propRenderers = propRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < propRenderers.Length; i++)
                propRenderers[i].enabled = false;
        }

        private static void RemoveLegacyTorchesAndChests(Transform propRoot)
        {
            for (int childIndex = propRoot.childCount - 1; childIndex >= 0; childIndex--)
            {
                Transform child = propRoot.GetChild(childIndex);
                bool isTorch = child.name.Contains("_引导灯_", StringComparison.Ordinal);
                bool isChest = child.GetComponentInChildren<ChestInteractable>(true) != null;
                if (isTorch || isChest)
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void HideLegacySurfaces(Transform root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
        }

        private static void DisableLegacyCollision(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider.isTrigger || HasNamedAncestor(collider.transform, "区域封印门_"))
                    continue;
                collider.enabled = false;
            }
        }

        private static bool HasNamedAncestor(Transform current, string prefix)
        {
            while (current != null)
            {
                if (current.name.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static void ApplyNaturalMaterials(Transform root, Material groundMaterial, Material rockMaterial)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                string objectName = renderer.name;
                bool isGround = objectName.EndsWith("_地面", StringComparison.Ordinal) || objectName.Contains("路面台阶", StringComparison.Ordinal);
                if (!isGround)
                {
                    renderer.enabled = false;
                    continue;
                }
                renderer.enabled = true;
                renderer.sharedMaterial = groundMaterial;
            }
        }

        private static void StyleRegionGates(Scene scene, Material rockMaterial)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform gate = transforms[i];
                    if (!gate.name.StartsWith("区域封印门_", StringComparison.Ordinal))
                        continue;
                    Transform visualRoot = gate.Find("封印光影视觉");
                    MeshRenderer[] renderers = gate.GetComponentsInChildren<MeshRenderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        MeshRenderer renderer = renderers[rendererIndex];
                        if (visualRoot != null && renderer.transform.IsChildOf(visualRoot))
                            continue;
                        renderer.enabled = false;
                    }

                    if (visualRoot == null)
                        continue;

                    ParticleSystemRenderer[] particleRenderers = visualRoot.GetComponentsInChildren<ParticleSystemRenderer>(true);
                    for (int rendererIndex = 0; rendererIndex < particleRenderers.Length; rendererIndex++)
                        particleRenderers[rendererIndex].enabled = false;
                }
            }
        }

        private static void BuildAmbientEnemySpawners(Transform parent, LevelLayout layout)
        {
            GameObject spawnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalSpawnerPrefabPath);
            if (spawnerPrefab == null)
                throw new InvalidOperationException($"缺少高原游荡敌群生成器预制体：{LocalSpawnerPrefabPath}");

            var rootObject = new GameObject("高原游荡敌群");
            SceneManager.MoveGameObjectToScene(rootObject, parent.gameObject.scene);
            rootObject.transform.SetParent(parent, false);

            int spawnerIndex = 0;
            int ordinaryRegionCount = Mathf.Max(0, layout.regionCenters.Length - 1);
            for (int i = 0; i < ordinaryRegionCount; i++)
            {
                Vector3 center = layout.regionCenters[i];
                Vector3 previous = i > 0 ? layout.regionCenters[i - 1] : center - (layout.regionCenters[i + 1] - center);
                Vector3 next = layout.regionCenters[i + 1];
                Vector3 tangent = next - previous;
                tangent.y = 0f;
                tangent = tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward;
                Vector3 perpendicular = new Vector3(-tangent.z, 0f, tangent.x);

                CreateAmbientEnemySpawner(
                    spawnerPrefab,
                    rootObject.transform,
                    layout,
                    center + perpendicular * (58f + i % 2 * 8f) + tangent * 22f,
                    ++spawnerIndex,
                    MinionSpawnPlanIndex);
                CreateAmbientEnemySpawner(
                    spawnerPrefab,
                    rootObject.transform,
                    layout,
                    center - perpendicular * (62f + (i + 1) % 2 * 10f) - tangent * 18f,
                    ++spawnerIndex,
                    EliteSpawnPlanIndex);
            }

            for (int i = 0; i < layout.regionCenters.Length - 1; i++)
            {
                Vector3 from = layout.regionCenters[i];
                Vector3 to = layout.regionCenters[i + 1];
                Vector3 tangent = to - from;
                tangent.y = 0f;
                tangent = tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward;
                Vector3 perpendicular = new Vector3(-tangent.z, 0f, tangent.x);
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 position = Vector3.Lerp(from, to, 0.5f) + perpendicular * side * (44f + i % 3 * 9f);
                CreateAmbientEnemySpawner(
                    spawnerPrefab,
                    rootObject.transform,
                    layout,
                    position,
                    ++spawnerIndex,
                    MinionSpawnPlanIndex);
            }
        }

        private static void BuildChestSpawners(Transform parent, LevelLayout layout)
        {
            GameObject spawnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalSpawnerPrefabPath);
            if (spawnerPrefab == null)
                throw new InvalidOperationException($"缺少高原宝箱生成器预制体：{LocalSpawnerPrefabPath}");

            var rootObject = new GameObject("高原宝箱生成器");
            SceneManager.MoveGameObjectToScene(rootObject, parent.gameObject.scene);
            rootObject.transform.SetParent(parent, false);

            int[] regionIndices = { 0, 2, 4 };
            for (int i = 0; i < regionIndices.Length; i++)
            {
                int regionIndex = regionIndices[i];
                Vector3 center = layout.regionCenters[regionIndex];
                Vector3 previous = regionIndex > 0 ? layout.regionCenters[regionIndex - 1] : center - (layout.regionCenters[regionIndex + 1] - center);
                Vector3 next = layout.regionCenters[regionIndex + 1];
                Vector3 tangent = next - previous;
                tangent.y = 0f;
                tangent = tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward;
                Vector3 perpendicular = new Vector3(-tangent.z, 0f, tangent.x);
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 position = center
                                   + perpendicular * side * Mathf.Min(52f, layout.regionExtents[regionIndex].x * 0.48f)
                                   + tangent * Mathf.Min(26f, layout.regionExtents[regionIndex].y * 0.22f);

                CreateConfiguredSpawner(
                    spawnerPrefab,
                    rootObject.transform,
                    layout,
                    position,
                    $"高原宝箱生成器_{i + 1:00}",
                    ChestSpawnPlanIndex);
            }
        }

        private static void CreateAmbientEnemySpawner(
            GameObject spawnerPrefab,
            Transform parent,
            LevelLayout layout,
            Vector3 position,
            int index,
            int planIndex)
        {
            string groupType = planIndex == EliteSpawnPlanIndex ? "精英群" : "小怪群";
            CreateConfiguredSpawner(
                spawnerPrefab,
                parent,
                layout,
                position,
                $"高原游荡{groupType}_{index:00}",
                planIndex);
        }

        private static void CreateConfiguredSpawner(
            GameObject spawnerPrefab,
            Transform parent,
            LevelLayout layout,
            Vector3 position,
            string objectName,
            int planIndex)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(spawnerPrefab, parent.gameObject.scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"高原局部生成器创建失败：{objectName}");

            position.y = SampleTerrainHeight(layout, position.x, position.z) + 0.15f;
            instance.name = objectName;
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;

            LevelLocalEnemySpawner spawner = instance.GetComponent<LevelLocalEnemySpawner>();
            if (spawner == null)
                throw new InvalidOperationException($"高原局部生成器缺少组件：{objectName}");

            var serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("_planIndex").intValue = planIndex;
            serializedSpawner.FindProperty("_waitForActivation").boolValue = false;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            spawner.enabled = true;
            EditorUtility.SetDirty(spawner);
        }

        private static void BuildLandscape(Transform parent, LevelLayout layout, Material material)
        {
            const int xSegments = 128;
            const int zSegments = 192;
            float minX = -390f;
            float maxX = 390f;
            float minZ = layout.minZ - 220f;
            float maxZ = layout.maxZ + 320f;
            var vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[xSegments * zSegments * 6];

            int vertexIndex = 0;
            for (int zIndex = 0; zIndex <= zSegments; zIndex++)
            {
                float zT = zIndex / (float)zSegments;
                float z = Mathf.Lerp(minZ, maxZ, zT);
                for (int xIndex = 0; xIndex <= xSegments; xIndex++)
                {
                    float xT = xIndex / (float)xSegments;
                    float x = Mathf.Lerp(minX, maxX, xT);
                    vertices[vertexIndex] = new Vector3(x, SampleTerrainHeight(layout, x, z), z);
                    uvs[vertexIndex] = new Vector2(xT, zT);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            int row = xSegments + 1;
            for (int zIndex = 0; zIndex < zSegments; zIndex++)
            {
                for (int xIndex = 0; xIndex < xSegments; xIndex++)
                {
                    int a = zIndex * row + xIndex;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            Mesh mesh = new Mesh { name = "HighlandLandscape", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Mesh savedMesh = SaveOrUpdateMesh("HighlandLandscape.asset", mesh);
            MeshRenderer renderer = CreateMeshObject("高原连续地形", savedMesh, material, parent, ShadowCastingMode.On, true);
            MeshCollider collider = renderer.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = savedMesh;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static float SampleTerrainHeight(LevelLayout layout, float x, float z)
        {
            float routeHeight = layout.SampleRouteHeight(z);
            float routeX = layout.SampleRouteX(z);
            float lateralDistance = Mathf.Abs(x - routeX);
            float broadNoise = Mathf.PerlinNoise(x * 0.0042f + 17.4f, z * 0.0042f + 33.7f);
            float detailNoise = Mathf.PerlinNoise(x * 0.017f + 71.2f, z * 0.017f + 8.3f);
            float ridge = Mathf.Abs(Mathf.PerlinNoise(x * 0.007f + 4.6f, z * 0.007f + 91.8f) * 2f - 1f);
            float hillMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(138f, 360f, lateralDistance));
            float height = routeHeight + 0.035f;
            height += hillMask * (5f + broadNoise * 48f + ridge * 18f);
            height += (detailNoise - 0.5f) * Mathf.Lerp(0.32f, 6.5f, hillMask);

            float riverDistance = Mathf.Abs(x - layout.RiverX(z));
            if (riverDistance < 42f)
            {
                float channelT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 14.8f, riverDistance));
                float waterDepth = Mathf.Lerp(1.62f, 0.12f, channelT);
                float riverBed = layout.RiverY(z) - waterDepth + (detailNoise - 0.5f) * 0.14f;
                float bankBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14.8f, 42f, riverDistance));
                height = Mathf.Lerp(riverBed, height, bankBlend);
            }

            for (int i = 0; i < layout.regionCenters.Length; i++)
            {
                Vector3 center = layout.regionCenters[i];
                Vector2 extents = layout.regionExtents[i] + new Vector2(8f, 8f);
                float dx = Mathf.Abs(x - center.x) / Mathf.Max(1f, extents.x);
                float dz = Mathf.Abs(z - center.z) / Mathf.Max(1f, extents.y);
                float distance = Mathf.Max(dx, dz);
                if (distance < 1.18f)
                {
                    float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1.18f, distance));
                    height = Mathf.Lerp(center.y + 0.035f, height, blend);
                }
            }
            return height;
        }

        private static void BuildRiver(Transform parent, LevelLayout layout, Material shoreMaterial, Material riverMaterial)
        {
            const int segments = 180;
            float minZ = layout.minZ - 170f;
            float maxZ = layout.maxZ + 230f;
            Mesh shoreMesh = BuildRiverBankMesh("HighlandRiverShore", layout, minZ, maxZ, segments, 13.2f, 25f);
            Mesh riverMesh = BuildRibbonMesh("HighlandRiver", layout, minZ, maxZ, segments, 13.5f, 0f);
            CreateMeshObject("河岸湿地", SaveOrUpdateMesh("HighlandRiverShore.asset", shoreMesh), shoreMaterial, parent, ShadowCastingMode.Off, false);
            MeshRenderer riverRenderer = CreateMeshObject("高原河流", SaveOrUpdateMesh("HighlandRiver.asset", riverMesh), riverMaterial, parent, ShadowCastingMode.Off, false);
            var riverSamples = new Vector3[segments + 1];
            var halfWidths = new float[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float z = Mathf.Lerp(minZ, maxZ, i / (float)segments);
                riverSamples[i] = new Vector3(layout.RiverX(z), layout.RiverY(z), z);
                halfWidths[i] = 13.5f * (1f + Mathf.Sin(z * 0.031f) * 0.12f);
            }
            HighlandWaterInteraction waterInteraction = riverRenderer.gameObject.AddComponent<HighlandWaterInteraction>();
            waterInteraction.Configure(riverSamples, halfWidths, 0.06f, 0.55f, 2.5f);
        }

        private static Mesh BuildRibbonMesh(string name, LevelLayout layout, float minZ, float maxZ, int segments, float halfWidth, float yOffset)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float z = Mathf.Lerp(minZ, maxZ, t);
                float previousZ = Mathf.Lerp(minZ, maxZ, Mathf.Max(0f, t - 1f / segments));
                float nextZ = Mathf.Lerp(minZ, maxZ, Mathf.Min(1f, t + 1f / segments));
                Vector3 previous = new Vector3(layout.RiverX(previousZ), layout.RiverY(previousZ), previousZ);
                Vector3 next = new Vector3(layout.RiverX(nextZ), layout.RiverY(nextZ), nextZ);
                Vector3 direction = next - previous;
                direction.y = 0f;
                direction.Normalize();
                Vector3 right = new Vector3(direction.z, 0f, -direction.x);
                float widthVariation = 1f + Mathf.Sin(z * 0.031f) * 0.12f;
                Vector3 center = new Vector3(layout.RiverX(z), layout.RiverY(z) + yOffset, z);
                vertices[i * 2] = center - right * halfWidth * widthVariation;
                vertices[i * 2 + 1] = center + right * halfWidth * widthVariation;
                uvs[i * 2] = new Vector2(0f, t * 18f);
                uvs[i * 2 + 1] = new Vector2(1f, t * 18f);
            }
            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = d;
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRiverBankMesh(
            string name,
            LevelLayout layout,
            float minZ,
            float maxZ,
            int segments,
            float innerHalfWidth,
            float outerHalfWidth)
        {
            var vertices = new Vector3[(segments + 1) * 4];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 12];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float z = Mathf.Lerp(minZ, maxZ, t);
                float previousZ = Mathf.Lerp(minZ, maxZ, Mathf.Max(0f, t - 1f / segments));
                float nextZ = Mathf.Lerp(minZ, maxZ, Mathf.Min(1f, t + 1f / segments));
                Vector3 previous = new Vector3(layout.RiverX(previousZ), layout.RiverY(previousZ), previousZ);
                Vector3 next = new Vector3(layout.RiverX(nextZ), layout.RiverY(nextZ), nextZ);
                Vector3 direction = next - previous;
                direction.y = 0f;
                direction.Normalize();
                Vector3 right = new Vector3(direction.z, 0f, -direction.x);
                float widthVariation = 1f + Mathf.Sin(z * 0.031f) * 0.12f;
                Vector3 center = new Vector3(layout.RiverX(z), layout.RiverY(z), z);

                Vector3 leftOuter = center - right * outerHalfWidth * widthVariation;
                Vector3 leftInner = center - right * innerHalfWidth * widthVariation;
                Vector3 rightInner = center + right * innerHalfWidth * widthVariation;
                Vector3 rightOuter = center + right * outerHalfWidth * widthVariation;
                leftOuter.y = SampleTerrainHeight(layout, leftOuter.x, leftOuter.z) + 0.025f;
                leftInner.y = SampleTerrainHeight(layout, leftInner.x, leftInner.z) + 0.025f;
                rightInner.y = SampleTerrainHeight(layout, rightInner.x, rightInner.z) + 0.025f;
                rightOuter.y = SampleTerrainHeight(layout, rightOuter.x, rightOuter.z) + 0.025f;

                int vertex = i * 4;
                vertices[vertex] = leftOuter;
                vertices[vertex + 1] = leftInner;
                vertices[vertex + 2] = rightInner;
                vertices[vertex + 3] = rightOuter;
                uvs[vertex] = new Vector2(0f, t * 18f);
                uvs[vertex + 1] = new Vector2(1f, t * 18f);
                uvs[vertex + 2] = new Vector2(0f, t * 18f);
                uvs[vertex + 3] = new Vector2(1f, t * 18f);
            }

            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int current = i * 4;
                int next = current + 4;
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = next + 1;
                triangles[triangleIndex++] = current + 2;
                triangles[triangleIndex++] = next + 2;
                triangles[triangleIndex++] = current + 3;
                triangles[triangleIndex++] = current + 3;
                triangles[triangleIndex++] = next + 2;
                triangles[triangleIndex++] = next + 3;
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildGrass(
            Transform parent,
            LevelLayout layout,
            Transform arenaRoot,
            Transform branchRoot,
            Material material,
            Material fineGrassMaterial)
        {
            string[] paths =
            {
                "Assets/Eternal Temple/Source_FBX/Grass_01.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_02.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_03.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_04.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_05.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_06.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_07.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_08.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_09.fbx",
                "Assets/Eternal Temple/Source_FBX/Grass_010.fbx",
            };
            var prefabs = new GameObject[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefabs[i] == null)
                    throw new InvalidOperationException($"找不到第二关草簇模型：{paths[i]}");
            }

            var grassRoot = new GameObject("风动高原草甸");
            grassRoot.transform.SetParent(parent, false);
            var surfaces = new List<SurfaceInfo>();
            CollectFloorSurfaces(arenaRoot, surfaces);
            CollectFloorSurfaces(branchRoot, surfaces);
            var random = new System.Random(Seed);
            int surfaceBudget = 900;

            for (int surfaceIndex = 0; surfaceIndex < surfaces.Count && surfaceBudget > 0; surfaceIndex++)
            {
                SurfaceInfo surface = surfaces[surfaceIndex];
                float area = surface.bounds.size.x * surface.bounds.size.z;
                int requested = Mathf.Clamp(Mathf.RoundToInt(area / 13f), 80, 260);
                int count = Mathf.Min(requested, surfaceBudget);
                Vector3 center = surface.bounds.center;
                for (int i = 0; i < count; i++)
                {
                    float x = Mathf.Lerp(surface.bounds.min.x + 0.8f, surface.bounds.max.x - 0.8f, (float)random.NextDouble());
                    float z = Mathf.Lerp(surface.bounds.min.z + 0.8f, surface.bounds.max.z - 0.8f, (float)random.NextDouble());
                    float centerDistance = new Vector2(x - center.x, z - center.z).magnitude;
                    if (centerDistance < 6.5f && random.NextDouble() < 0.72)
                        continue;
                    SpawnGrass(prefabs[random.Next(prefabs.Length)], grassRoot.transform, material,
                        new Vector3(x, surface.topY + 0.02f, z), Mathf.Lerp(0.58f, 0.92f, (float)random.NextDouble()), random);
                }
                surfaceBudget -= count;
            }

            const int riverPatchCount = 900;
            for (int i = 0; i < riverPatchCount; i++)
            {
                float t = (float)random.NextDouble();
                float z = Mathf.Lerp(layout.minZ - 155f, layout.maxZ + 130f, t);
                float side = random.Next(0, 2) == 0 ? -1f : 1f;
                float x = layout.RiverX(z) + side * Mathf.Lerp(14.8f, 37f, Mathf.Pow((float)random.NextDouble(), 1.45f));
                SpawnGrass(prefabs[random.Next(prefabs.Length)], grassRoot.transform, material,
                    new Vector3(x, SampleTerrainHeight(layout, x, z) + 0.025f, z),
                    Mathf.Lerp(0.64f, 1.05f, (float)random.NextDouble()), random);
            }

            const int hillsidePatchCount = 500;
            for (int i = 0; i < hillsidePatchCount; i++)
            {
                float z = Mathf.Lerp(layout.minZ - 120f, layout.maxZ + 145f, (float)random.NextDouble());
                float routeX = layout.SampleRouteX(z);
                float side = random.Next(0, 2) == 0 ? -1f : 1f;
                float x = routeX + side * Mathf.Lerp(layout.SampleRouteHalfWidth(z) + 3f, 132f, (float)random.NextDouble());
                if (Mathf.Abs(x - layout.RiverX(z)) < 14.5f)
                    continue;
                SpawnGrass(prefabs[random.Next(prefabs.Length)], grassRoot.transform, material,
                    new Vector3(x, SampleTerrainHeight(layout, x, z) + 0.025f, z),
                    Mathf.Lerp(0.48f, 0.86f, (float)random.NextDouble()), random);
            }

            BuildFineGrass(parent, layout, fineGrassMaterial);
        }

        private static void BuildFineGrass(Transform parent, LevelLayout layout, Material material)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var random = new System.Random(Seed + 401);
            const int bladeCount = 28000;

            for (int i = 0; i < bladeCount; i++)
            {
                float z;
                float x;
                if (i < 9500)
                {
                    z = layout.minZ - 132f + Mathf.Lerp(0f, 205f, Mathf.Pow((float)random.NextDouble(), 1.4f));
                    float side = random.NextDouble() < 0.82 ? -1f : 1f;
                    x = layout.RiverX(z) + side * Mathf.Lerp(14.7f, 49f, Mathf.Pow((float)random.NextDouble(), 1.55f));
                }
                else
                {
                    z = Mathf.Lerp(layout.minZ - 155f, layout.maxZ + 140f, (float)random.NextDouble());
                    float side = random.Next(0, 2) == 0 ? -1f : 1f;
                    x = layout.RiverX(z) + side * Mathf.Lerp(14.7f, 63f, Mathf.Pow((float)random.NextDouble(), 1.42f));
                }

                float height = Mathf.Lerp(0.34f, 0.86f, (float)random.NextDouble());
                float width = Mathf.Lerp(0.024f, 0.055f, (float)random.NextDouble());
                float dry = (float)random.NextDouble();
                float dryMix = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, dry));
                Color variation = Color.Lerp(
                    new Color(0.018f, 0.105f, 0.008f, 1f),
                    new Color(0.15f, 0.105f, 0.018f, 1f),
                    dryMix);
                AddGrassBlade(
                    vertices,
                    normals,
                    uvs,
                    colors,
                    triangles,
                    new Vector3(x, SampleTerrainHeight(layout, x, z) + 0.025f, z),
                    height,
                    width,
                    (float)random.NextDouble() * 360f,
                    variation);
            }

            var mesh = new Mesh { name = "HighlandFineGrass", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            CreateMeshObject("细密风草", SaveOrUpdateMesh("HighlandFineGrassV2.asset", mesh), material, parent, ShadowCastingMode.Off, true);
        }

        private static void CollectFloorSurfaces(Transform root, List<SurfaceInfo> results)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].name.EndsWith("_地面", StringComparison.Ordinal))
                    continue;
                results.Add(new SurfaceInfo { bounds = renderers[i].bounds, topY = renderers[i].bounds.max.y });
            }
        }

        private static void SpawnGrass(GameObject prefab, Transform parent, Material material, Vector3 position, float scale, System.Random random)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"无法实例化第二关草簇：{prefab.name}");
            instance.name = "高原草簇";
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(
                Mathf.Lerp(-3f, 3f, (float)random.NextDouble()),
                (float)random.NextDouble() * 360f,
                Mathf.Lerp(-3f, 3f, (float)random.NextDouble()));
            float stretch = Mathf.Lerp(0.82f, 1.18f, (float)random.NextDouble());
            instance.transform.localScale = new Vector3(scale, scale * stretch, scale);
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                UnityEngine.Object.DestroyImmediate(colliders[i]);
            MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = material;
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                renderers[i].receiveShadows = true;
                renderers[i].lightProbeUsage = LightProbeUsage.BlendProbes;
                renderers[i].motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            }
        }

        private static void BuildWildflowers(Transform parent, LevelLayout layout, Material material)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var random = new System.Random(Seed + 211);
            const int flowerClusterCount = 960;

            for (int i = 0; i < flowerClusterCount; i++)
            {
                float z;
                float x;
                if (i < 260)
                {
                    z = layout.minZ - 126f + Mathf.Lerp(0f, 150f, Mathf.Pow((float)random.NextDouble(), 1.45f));
                    x = layout.RiverX(z) - Mathf.Lerp(15.5f, 43f, Mathf.Pow((float)random.NextDouble(), 1.25f));
                }
                else
                {
                    z = Mathf.Lerp(layout.minZ - 150f, layout.maxZ + 145f, (float)random.NextDouble());
                    float side = random.NextDouble() < 0.62 ? -1f : 1f;
                    x = layout.RiverX(z) + side * Mathf.Lerp(16.2f, 58f, Mathf.Pow((float)random.NextDouble(), 1.32f));
                }

                float height = Mathf.Lerp(0.32f, 0.68f, (float)random.NextDouble());
                float radius = Mathf.Lerp(0.045f, 0.105f, (float)random.NextDouble());
                float colorRoll = (float)random.NextDouble();
                Color petalColor = colorRoll < 0.57f
                    ? Color.Lerp(new Color(0.76f, 0.8f, 0.74f, 1f), new Color(1f, 0.97f, 0.86f, 1f), (float)random.NextDouble())
                    : colorRoll < 0.86f
                        ? Color.Lerp(new Color(0.92f, 0.57f, 0.06f, 1f), new Color(1f, 0.82f, 0.18f, 1f), (float)random.NextDouble())
                        : Color.Lerp(new Color(0.46f, 0.27f, 0.58f, 1f), new Color(0.72f, 0.5f, 0.78f, 1f), (float)random.NextDouble());

                int blossomCount = 2 + random.Next(3);
                for (int blossomIndex = 0; blossomIndex < blossomCount; blossomIndex++)
                {
                    float offsetAngle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float offsetDistance = Mathf.Lerp(0.08f, 0.72f, (float)random.NextDouble());
                    float flowerX = x + Mathf.Cos(offsetAngle) * offsetDistance;
                    float flowerZ = z + Mathf.Sin(offsetAngle) * offsetDistance;
                    float flowerY = SampleTerrainHeight(layout, flowerX, flowerZ) + 0.035f;
                    AddFlower(
                        vertices,
                        normals,
                        uvs,
                        colors,
                        triangles,
                        new Vector3(flowerX, flowerY, flowerZ),
                        height * Mathf.Lerp(0.82f, 1.16f, (float)random.NextDouble()),
                        radius * Mathf.Lerp(0.8f, 1.12f, (float)random.NextDouble()),
                        (float)random.NextDouble() * 360f,
                        petalColor);
                }
            }

            var mesh = new Mesh { name = "HighlandWildflowers", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            CreateMeshObject("高原野花", SaveOrUpdateMesh("HighlandWildflowers.asset", mesh), material, parent, ShadowCastingMode.Off, true);
        }

        private static void AddFlower(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles,
            Vector3 position,
            float height,
            float radius,
            float yaw,
            Color petalColor)
        {
            Color stemColor = new Color(0.08f, 0.2f, 0.035f, 1f);
            AddGrassCard(vertices, normals, uvs, colors, triangles, position, height, radius * 0.075f, yaw, stemColor);
            AddGrassCard(vertices, normals, uvs, colors, triangles, position, height * 0.98f, radius * 0.065f, yaw + 90f, stemColor);

            Vector3 center = position + Vector3.up * height;
            const int petalCount = 6;
            for (int petalIndex = 0; petalIndex < petalCount; petalIndex++)
            {
                float angle = (yaw + petalIndex * (360f / petalCount)) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0.08f, Mathf.Sin(angle)).normalized;
                Vector3 tangent = new Vector3(-direction.z, 0f, direction.x);
                int start = vertices.Count;
                vertices.Add(center + direction * radius * 0.12f);
                vertices.Add(center + direction * radius * 0.55f - tangent * radius * 0.31f);
                vertices.Add(center + direction * radius + Vector3.up * radius * 0.08f);
                vertices.Add(center + direction * radius * 0.55f + tangent * radius * 0.31f);
                for (int vertexIndex = 0; vertexIndex < 4; vertexIndex++)
                {
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(0.5f, 1f));
                    colors.Add(petalColor);
                }
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            Color centerColor = new Color(0.95f, 0.58f, 0.06f, 1f);
            int centerStart = vertices.Count;
            vertices.Add(center + Vector3.up * radius * 0.075f);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 1f));
            colors.Add(centerColor);
            for (int ringIndex = 0; ringIndex < petalCount; ringIndex++)
            {
                float angle = (yaw + ringIndex * (360f / petalCount)) * Mathf.Deg2Rad;
                vertices.Add(center + new Vector3(Mathf.Cos(angle), 0.08f, Mathf.Sin(angle)) * radius * 0.25f);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f, 1f));
                colors.Add(centerColor);
            }
            for (int ringIndex = 0; ringIndex < petalCount; ringIndex++)
            {
                triangles.Add(centerStart);
                triangles.Add(centerStart + 1 + ringIndex);
                triangles.Add(centerStart + 1 + (ringIndex + 1) % petalCount);
            }
        }

        private static void AddGrassBlade(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles,
            Vector3 position,
            float height,
            float width,
            float yaw,
            Color variation)
        {
            AddGrassCard(vertices, normals, uvs, colors, triangles, position, height, width, yaw, variation);
            AddGrassCard(vertices, normals, uvs, colors, triangles, position, height * 0.96f, width * 0.9f, yaw + 90f, variation);
        }

        private static void AddGrassCard(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles,
            Vector3 position,
            float height,
            float width,
            float yaw,
            Color variation)
        {
            float radians = yaw * Mathf.Deg2Rad;
            Vector3 right = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
            Vector3 normal = Vector3.Cross(Vector3.up, right).normalized;
            int start = vertices.Count;
            vertices.Add(position - right * width);
            vertices.Add(position + right * width);
            vertices.Add(position + Vector3.up * height - right * width * 0.16f);
            vertices.Add(position + Vector3.up * height + right * width * 0.16f);
            Color rootColor = new Color(variation.r * 0.42f, variation.g * 0.42f, variation.b * 0.42f, variation.a);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            colors.Add(rootColor);
            colors.Add(rootColor);
            colors.Add(variation);
            colors.Add(variation);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0.42f, 1f));
            uvs.Add(new Vector2(0.58f, 1f));
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void BuildRocks(Transform parent, LevelLayout layout, Material material)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var random = new System.Random(Seed + 31);
            for (int i = 0; i < 220; i++)
            {
                float t = (float)random.NextDouble();
                float z = Mathf.Lerp(layout.minZ - 100f, layout.maxZ + 150f, t);
                float side = random.Next(0, 2) == 0 ? -1f : 1f;
                float x = layout.RiverX(z) + side * Mathf.Lerp(17f, 35f, (float)random.NextDouble());
                float y = SampleTerrainHeight(layout, x, z) - 0.08f;
                Vector3 scale = new Vector3(
                    Mathf.Lerp(0.28f, 2.7f, (float)random.NextDouble()),
                    Mathf.Lerp(0.2f, 1.55f, (float)random.NextDouble()),
                    Mathf.Lerp(0.3f, 2.35f, (float)random.NextDouble()));
                Quaternion rotation = Quaternion.Euler(
                    Mathf.Lerp(-12f, 12f, (float)random.NextDouble()),
                    (float)random.NextDouble() * 360f,
                    Mathf.Lerp(-10f, 10f, (float)random.NextDouble()));
                AddRock(vertices, triangles, new Vector3(x, y, z), scale, rotation, random);
            }
            Mesh mesh = new Mesh { name = "HighlandRiverRocks", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            MeshRenderer rockRenderer = CreateMeshObject("河岸岩石群", SaveOrUpdateMesh("HighlandRiverRocks.asset", mesh), material, parent, ShadowCastingMode.On, true);
            EnsureStaticMeshColliders(rockRenderer.gameObject);
        }

        private static void AddRock(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 scale, Quaternion rotation, System.Random random)
        {
            const int segments = 8;
            int start = vertices.Count;
            for (int ring = 0; ring < 3; ring++)
            {
                float height = ring == 0 ? 0f : ring == 1 ? 0.56f : 0.9f;
                float radius = ring == 0 ? 0.76f : ring == 1 ? 1f : 0.48f;
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment / (float)segments * Mathf.PI * 2f;
                    float jitter = Mathf.Lerp(0.82f, 1.18f, (float)random.NextDouble());
                    Vector3 local = new Vector3(Mathf.Cos(angle) * radius * jitter, height, Mathf.Sin(angle) * radius * jitter);
                    vertices.Add(center + rotation * Vector3.Scale(local, scale));
                }
            }
            vertices.Add(center + rotation * Vector3.Scale(new Vector3(0f, 1.04f, 0f), scale));
            for (int ring = 0; ring < 2; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    int a = start + ring * segments + segment;
                    int b = start + ring * segments + next;
                    int c = start + (ring + 1) * segments + segment;
                    int d = start + (ring + 1) * segments + next;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
            int apex = start + segments * 3;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(start + segments * 2 + segment);
                triangles.Add(apex);
                triangles.Add(start + segments * 2 + next);
            }
        }

        private static void BuildRockAssets(Transform parent, LevelLayout layout, Material material)
        {
            string[] paths =
            {
                "Assets/_Wyrms/_Rocks/DarkRock_1.prefab",
                "Assets/_Wyrms/_Rocks/DarkRock_2.prefab",
                "Assets/_Wyrms/_Rocks/DarkRock_3.prefab",
                "Assets/_Wyrms/_Rocks/DarkRock_4.prefab",
            };
            var prefabs = new GameObject[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (prefabs[i] == null)
                    throw new InvalidOperationException($"找不到第二关岩石模型：{paths[i]}");
            }

            var rootObject = new GameObject("高原真实岩石");
            rootObject.transform.SetParent(parent, false);
            var random = new System.Random(Seed + 97);
            for (int i = 0; i < 94; i++)
            {
                float z = Mathf.Lerp(layout.minZ - 55f, layout.maxZ + 115f, (float)random.NextDouble());
                float side = random.Next(0, 2) == 0 ? -1f : 1f;
                float x = layout.RiverX(z) + side * Mathf.Lerp(15.5f, 28f, (float)random.NextDouble());
                float scale = Mathf.Lerp(0.08f, 0.26f, (float)random.NextDouble());
                SpawnRock(prefabs[random.Next(prefabs.Length)], rootObject.transform, material, new Vector3(x, SampleTerrainHeight(layout, x, z) - 0.3f, z), scale, random);
            }

            for (int i = 0; i < 72; i++)
            {
                float z = Mathf.Lerp(layout.minZ - 125f, layout.maxZ + 120f, (float)random.NextDouble());
                float x = layout.RiverX(z) + Mathf.Lerp(-10.8f, 10.8f, (float)random.NextDouble());
                float scale = Mathf.Lerp(0.045f, 0.15f, (float)random.NextDouble());
                SpawnRock(prefabs[random.Next(prefabs.Length)], rootObject.transform, material,
                    new Vector3(x, layout.RiverY(z) - Mathf.Lerp(0.34f, 0.72f, (float)random.NextDouble()), z), scale, random);
            }

            for (int regionIndex = 0; regionIndex < layout.regionCenters.Length; regionIndex++)
            {
                Vector3 center = layout.regionCenters[regionIndex];
                Vector2 extents = layout.regionExtents[regionIndex];
                for (int i = 0; i < 8; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    float x = center.x + side * (extents.x + Mathf.Lerp(5f, 15f, (float)random.NextDouble()));
                    float z = center.z + Mathf.Lerp(-extents.y, extents.y, (float)random.NextDouble());
                    float scale = Mathf.Lerp(0.2f, 0.58f, (float)random.NextDouble());
                    SpawnRock(prefabs[random.Next(prefabs.Length)], rootObject.transform, material, new Vector3(x, SampleTerrainHeight(layout, x, z) - 0.8f, z), scale, random);
                }
            }

            for (int i = 0; i < 9; i++)
            {
                float z = layout.minZ - 100f + i * 13.5f + Mathf.Lerp(-3f, 3f, (float)random.NextDouble());
                float x = layout.RiverX(z) - Mathf.Lerp(16.5f, 30f, (float)random.NextDouble());
                float scale = Mathf.Lerp(0.16f, 0.38f, (float)random.NextDouble());
                SpawnRock(
                    prefabs[random.Next(prefabs.Length)],
                    rootObject.transform,
                    material,
                    new Vector3(x, SampleTerrainHeight(layout, x, z) - 0.48f, z),
                    scale,
                    random);
            }
        }

        private static void SpawnRock(GameObject prefab, Transform parent, Material material, Vector3 position, float scale, System.Random random)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.name = "风化岩石";
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(
                Mathf.Lerp(-12f, 12f, (float)random.NextDouble()),
                (float)random.NextDouble() * 360f,
                Mathf.Lerp(-10f, 10f, (float)random.NextDouble()));
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, new Vector3(
                scale * Mathf.Lerp(0.78f, 1.24f, (float)random.NextDouble()),
                scale * Mathf.Lerp(0.72f, 1.18f, (float)random.NextDouble()),
                scale * Mathf.Lerp(0.8f, 1.26f, (float)random.NextDouble())));
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            if (scale < 0.2f)
            {
                for (int i = 0; i < colliders.Length; i++)
                    UnityEngine.Object.DestroyImmediate(colliders[i]);
            }
            else
            {
                EnsureStaticMeshColliders(instance);
            }
            MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = material;
        }

        private static void BuildCliffAssets(Transform parent, LevelLayout layout, Material material)
        {
            var prefabs = new GameObject[13];
            for (int i = 0; i < prefabs.Length; i++)
            {
                string path = $"Assets/Eternal Temple/Prefabs/Cliffs/Cliff_Formation_{i + 1:00}.prefab";
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabs[i] == null)
                    throw new InvalidOperationException($"找不到第二关悬崖模型：{path}");
            }

            var cliffRoot = new GameObject("高原风化断崖");
            cliffRoot.transform.SetParent(parent, false);
            var random = new System.Random(Seed + 173);
            for (int i = 0; i < 28; i++)
            {
                float z = Mathf.Lerp(layout.minZ - 55f, layout.maxZ + 185f, (float)random.NextDouble());
                float side = i % 2 == 0 ? -1f : 1f;
                float x = layout.SampleRouteX(z) + side * Mathf.Lerp(148f, 245f, (float)random.NextDouble());
                float y = SampleTerrainHeight(layout, x, z) - Mathf.Lerp(5f, 17f, (float)random.NextDouble());
                float scale = Mathf.Lerp(0.58f, 1.08f, (float)random.NextDouble());
                GameObject instance = UnityEngine.Object.Instantiate(prefabs[random.Next(prefabs.Length)], cliffRoot.transform);
                instance.name = "高原断崖";
                instance.transform.position = new Vector3(x, y, z);
                instance.transform.rotation = Quaternion.Euler(
                    Mathf.Lerp(-8f, 8f, (float)random.NextDouble()),
                    side < 0f ? Mathf.Lerp(40f, 140f, (float)random.NextDouble()) : Mathf.Lerp(220f, 320f, (float)random.NextDouble()),
                    Mathf.Lerp(-7f, 7f, (float)random.NextDouble()));
                instance.transform.localScale = Vector3.one * scale;
                EnsureStaticMeshColliders(instance);
                MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    renderers[rendererIndex].sharedMaterial = material;
                    renderers[rendererIndex].shadowCastingMode = ShadowCastingMode.On;
                    renderers[rendererIndex].receiveShadows = true;
                }
            }
        }

        private static void BuildMountains(Transform parent, LevelLayout layout, Material material)
        {
            Mesh distantRange = BuildMountainRangeMesh(
                "HighlandMountainRange",
                -720f,
                720f,
                layout.maxZ + 20f,
                layout.maxZ + 470f,
                layout.maxY - 24f,
                218f,
                Seed + 67);
            MeshRenderer distantRenderer = CreateMeshObject("雪线远山", SaveOrUpdateMesh("HighlandMountainRange.asset", distantRange), material, parent, ShadowCastingMode.On, true);
            EnsureStaticMeshColliders(distantRenderer.gameObject);

            Mesh middleRange = BuildMountainRangeMesh(
                "HighlandMiddleRange",
                -590f,
                590f,
                layout.maxZ - 85f,
                layout.maxZ + 275f,
                layout.maxY - 19f,
                126f,
                Seed + 131);
            MeshRenderer middleRenderer = CreateMeshObject("中景山脉", SaveOrUpdateMesh("HighlandMiddleRange.asset", middleRange), material, parent, ShadowCastingMode.On, true);
            EnsureStaticMeshColliders(middleRenderer.gameObject);
        }

        private static Mesh BuildMountainRangeMesh(
            string name,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float baseY,
            float maximumHeight,
            int seed)
        {
            const int xSegments = 240;
            const int zSegments = 84;
            var vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[xSegments * zSegments * 6];
            float seedOffset = Mathf.Abs(seed % 10000) * 0.000137f;
            float zRange = maxZ - minZ;

            int vertexIndex = 0;
            for (int zIndex = 0; zIndex <= zSegments; zIndex++)
            {
                float zT = zIndex / (float)zSegments;
                float z = Mathf.Lerp(minZ, maxZ, zT);
                for (int xIndex = 0; xIndex <= xSegments; xIndex++)
                {
                    float xT = xIndex / (float)xSegments;
                    float x = Mathf.Lerp(minX, maxX, xT);
                    float normalizedX = xT * 2f - 1f;
                    float broadEnvelope = Mathf.Max(
                        Mathf.Max(Gaussian(normalizedX, -0.78f, 0.18f) * 0.68f, Gaussian(normalizedX, -0.48f, 0.22f) * 0.82f),
                        Mathf.Max(Gaussian(normalizedX, -0.12f, 0.2f) * 0.74f,
                            Mathf.Max(Gaussian(normalizedX, 0.3f, 0.23f) * 0.88f, Gaussian(normalizedX, 0.72f, 0.21f) * 0.78f)));
                    float pointedEnvelope = Mathf.Max(
                        Mathf.Max(SharpPeak(normalizedX, -0.78f, 0.17f) * 0.7f, SharpPeak(normalizedX, -0.48f, 0.19f) * 0.92f),
                        Mathf.Max(SharpPeak(normalizedX, -0.12f, 0.17f) * 0.8f,
                            Mathf.Max(SharpPeak(normalizedX, 0.3f, 0.2f), SharpPeak(normalizedX, 0.72f, 0.18f) * 0.88f)));
                    float peakEnvelope = Mathf.Max(broadEnvelope * 0.76f, pointedEnvelope);
                    float crestNoise = FractalNoise(x * 0.0052f, seedOffset, 4);
                    float sharpNoise = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.021f + seedOffset * 17f, seedOffset * 29f) * 2f - 1f);
                    sharpNoise = Mathf.Pow(sharpNoise, 2.2f);
                    float crestHeight = maximumHeight * (0.1f + peakEnvelope * 0.79f + crestNoise * 0.065f + sharpNoise * 0.055f);
                    float crestDepth = minZ + zRange * Mathf.Lerp(0.42f, 0.63f,
                        Mathf.PerlinNoise(x * 0.0035f + seedOffset * 7f, seedOffset * 11f));
                    float depthDistance = z - crestDepth;
                    float depthWidth = depthDistance < 0f ? zRange * 0.29f : zRange * 0.38f;
                    float mountainProfile = Mathf.Exp(-Mathf.Pow(depthDistance / Mathf.Max(1f, depthWidth), 2f));
                    float largeCrags = FractalNoise(x * 0.009f + 17f, z * 0.007f + seedOffset, 4);
                    float ridgeNoise = 1f - Mathf.Abs(FractalNoise(x * 0.018f + seedOffset * 23f, z * 0.0048f + seedOffset * 41f, 3) * 2f - 1f);
                    ridgeNoise = Mathf.Pow(ridgeNoise, 3.4f);
                    float erosionChannel = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.026f + seedOffset * 31f, z * 0.0055f) * 2f - 1f);
                    erosionChannel = Mathf.Pow(erosionChannel, 5f);
                    float rockRelief = ((largeCrags - 0.48f) * 0.13f + (ridgeNoise - 0.32f) * 0.1f) * maximumHeight;
                    float erosion = erosionChannel * maximumHeight * 0.08f * mountainProfile;
                    float foothills = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(zT * Mathf.PI)), 1.4f) *
                        (4f + FractalNoise(x * 0.014f, z * 0.013f + seedOffset, 3) * maximumHeight * 0.09f);
                    float y = baseY + foothills + mountainProfile * (crestHeight + rockRelief) - erosion;
                    if (float.IsNaN(y) || float.IsInfinity(y))
                    {
                        throw new InvalidOperationException(
                            $"第二关山体高度无效：x={x}, z={z}, baseY={baseY}, peak={peakEnvelope}, crest={crestHeight}, profile={mountainProfile}, relief={rockRelief}, erosion={erosion}, foothills={foothills}");
                    }
                    vertices[vertexIndex] = new Vector3(x, y, z);
                    uvs[vertexIndex] = new Vector2(xT * 8f, zT * 4f);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            int row = xSegments + 1;
            for (int zIndex = 0; zIndex < zSegments; zIndex++)
            {
                for (int xIndex = 0; xIndex < xSegments; xIndex++)
                {
                    int a = zIndex * row + xIndex;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Gaussian(float value, float center, float width)
        {
            float distance = (value - center) / Mathf.Max(0.0001f, width);
            return Mathf.Exp(-distance * distance);
        }

        private static float SharpPeak(float value, float center, float width)
        {
            float distance = Mathf.Abs(value - center) / Mathf.Max(0.0001f, width);
            return Mathf.Pow(Mathf.Clamp01(1f - distance), 0.58f);
        }

        private static float FractalNoise(float x, float y, int octaves)
        {
            float value = 0f;
            float amplitude = 0.56f;
            float amplitudeSum = 0f;
            for (int octave = 0; octave < octaves; octave++)
            {
                float noise = Mathf.PerlinNoise(x, y);
                if (float.IsNaN(noise) || float.IsInfinity(noise))
                    noise = 0.5f;
                value += Mathf.Clamp01(noise) * amplitude;
                amplitudeSum += amplitude;
                x = x * 2.03f + 13.17f;
                y = y * 2.03f - 9.41f;
                amplitude *= 0.49f;
            }
            return value / Mathf.Max(0.0001f, amplitudeSum);
        }

        private static void BuildClouds(Transform parent, LevelLayout layout, Material material)
        {
            float centerZ = (layout.minZ + layout.maxZ) * 0.5f;
            const int horizontalSegments = 72;
            const int verticalSegments = 18;
            const float radius = 920f;
            var vertices = new Vector3[(horizontalSegments + 1) * (verticalSegments + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[horizontalSegments * verticalSegments * 6];
            Vector3 center = new Vector3(0f, layout.minY - 52f, centerZ);
            int vertexIndex = 0;
            for (int verticalIndex = 0; verticalIndex <= verticalSegments; verticalIndex++)
            {
                float verticalT = verticalIndex / (float)verticalSegments;
                float elevation = Mathf.Lerp(7f, 89f, verticalT) * Mathf.Deg2Rad;
                float ringRadius = Mathf.Cos(elevation) * radius;
                float y = center.y + Mathf.Sin(elevation) * radius;
                for (int horizontalIndex = 0; horizontalIndex <= horizontalSegments; horizontalIndex++)
                {
                    float horizontalT = horizontalIndex / (float)horizontalSegments;
                    float azimuth = horizontalT * Mathf.PI * 2f;
                    vertices[vertexIndex] = center + new Vector3(Mathf.Cos(azimuth) * ringRadius, y - center.y, Mathf.Sin(azimuth) * ringRadius);
                    uvs[vertexIndex] = new Vector2(horizontalT, verticalT);
                    vertexIndex++;
                }
            }
            int triangleIndex = 0;
            int row = horizontalSegments + 1;
            for (int verticalIndex = 0; verticalIndex < verticalSegments; verticalIndex++)
            {
                for (int horizontalIndex = 0; horizontalIndex < horizontalSegments; horizontalIndex++)
                {
                    int a = verticalIndex * row + horizontalIndex;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = d;
                    triangles[triangleIndex++] = c;
                }
            }
            var mesh = new Mesh { name = "HighlandCloudDome", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            CreateMeshObject("高空云穹", SaveOrUpdateMesh("HighlandCloudLayer.asset", mesh), material, parent, ShadowCastingMode.Off, false);
        }

        private static void BuildWind(Transform parent)
        {
            var windObject = new GameObject("高原风场");
            windObject.transform.SetParent(parent, false);
            windObject.transform.rotation = Quaternion.Euler(0f, 55f, 0f);
            WindZone wind = windObject.AddComponent<WindZone>();
            wind.mode = WindZoneMode.Directional;
            wind.windMain = 0.78f;
            wind.windTurbulence = 0.42f;
            wind.windPulseMagnitude = 0.48f;
            wind.windPulseFrequency = 0.21f;
        }

        private static void ConfigureLighting(Scene scene, Transform parent, Transform lightRoot, LevelLayout layout)
        {
            Light sun = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Light[] lights = roots[rootIndex].GetComponentsInChildren<Light>(true);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional && lights[i].name == "Directional Light")
                        sun = lights[i];
                }
            }
            if (sun == null)
                throw new InvalidOperationException("第二关场景缺少主平行光。");

            sun.transform.rotation = Quaternion.Euler(22f, 146f, 0f);
            sun.color = new Color(1f, 0.92f, 0.79f, 1f);
            sun.intensity = 2.32f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.shadowBias = 0.035f;
            sun.shadowNormalBias = 0.28f;
            RenderSettings.sun = sun;

            Light[] environmentLights = lightRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < environmentLights.Length; i++)
            {
                Light light = environmentLights[i];
                if (light.type == LightType.Directional)
                {
                    light.color = new Color(0.47f, 0.59f, 0.68f, 1f);
                    light.intensity = 0.18f;
                }
                else
                {
                    light.enabled = false;
                }
            }

            Material skybox = CreateSkyboxMaterial();
            RenderSettings.skybox = skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.7f, 0.76f, 0.77f, 1f);
            RenderSettings.fogDensity = 0.00072f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.75f, 0.82f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.54f, 0.58f, 0.49f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.31f, 0.22f, 1f);
            RenderSettings.ambientIntensity = 1.62f;
            RenderSettings.reflectionIntensity = 0.82f;

            CreateGlobalVolume(parent);
            ConfigureMainCamera(scene);
        }

        private static Material CreateSkyboxMaterial()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
                throw new InvalidOperationException("找不到 Unity Procedural Skybox Shader。");
            string path = $"{AssetFolder}/HighlandSkybox.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "HighlandSkybox" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            material.SetFloat("_SunDisk", 2f);
            material.SetFloat("_SunSize", 0.021f);
            material.SetFloat("_SunSizeConvergence", 9f);
            material.SetFloat("_AtmosphereThickness", 0.92f);
            material.SetColor("_SkyTint", new Color(0.4f, 0.63f, 0.87f, 1f));
            material.SetColor("_GroundColor", new Color(0.29f, 0.34f, 0.27f, 1f));
            material.SetFloat("_Exposure", 1.18f);
            return Dirty(material);
        }

        private static void CreateGlobalVolume(Transform parent)
        {
            string profilePath = $"{AssetFolder}/HighlandVolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile != null)
                AssetDatabase.DeleteAsset(profilePath);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "HighlandVolumeProfile";
            AssetDatabase.CreateAsset(profile, profilePath);
            if (profile == null)
            {
                throw new InvalidOperationException("无法创建第二关高原后期配置。");
            }

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.2f);
            bloom.scatter.Override(0.62f);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(1.04f);
            color.contrast.Override(2f);
            color.colorFilter.Override(new Color(1f, 1f, 1f, 1f));
            color.saturation.Override(-4f);

            WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.temperature.Override(3f);
            whiteBalance.tint.Override(-1f);

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.075f);
            vignette.smoothness.Override(0.34f);

            EditorUtility.SetDirty(profile);
            var volumeObject = new GameObject("高原全局后期");
            volumeObject.transform.SetParent(parent, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5f;
            volume.sharedProfile = profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                return component;
            component = ScriptableObject.CreateInstance<T>();
            component.name = typeof(T).Name;
            component.active = true;
            component.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            AssetDatabase.AddObjectToAsset(component, profile);
            profile.components.Add(component);
            return component;
        }

        private static void ConfigureMainCamera(Scene scene)
        {
            Camera camera = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length && camera == null; i++)
            {
                Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
                for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
                {
                    if (cameras[cameraIndex].CompareTag("MainCamera"))
                    {
                        camera = cameras[cameraIndex];
                        break;
                    }
                }
            }
            if (camera == null)
                throw new InvalidOperationException("第二关场景缺少主相机。");

            camera.farClipPlane = 1600f;
            camera.allowHDR = true;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
        }

        private static void RenderPreview(Scene scene, LevelLayout layout, string fileName, bool scenic)
        {
            var previewObject = new GameObject("第二关高原预览相机");
            SceneManager.MoveGameObjectToScene(previewObject, scene);
            Camera camera = previewObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = scenic ? 57f : 68f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1800f;
            camera.allowHDR = true;

            if (scenic)
            {
                float z = layout.minZ - 118f;
                float riverX = layout.RiverX(z);
                float cameraX = riverX - 14.8f;
                float cameraY = SampleTerrainHeight(layout, cameraX, z) + 1.58f;
                camera.transform.position = new Vector3(cameraX, cameraY, z);
                Vector3 target = new Vector3(layout.RiverX(z + 380f) + 2f, layout.SampleRouteHeight(z + 380f) + 17f, z + 380f);
                camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
            }
            else
            {
                Vector3 center = layout.regionCenters[0];
                camera.transform.position = center + new Vector3(-9f, 5.8f, -13f);
                camera.transform.rotation = Quaternion.LookRotation(center + new Vector3(0f, 2.4f, 18f) - camera.transform.position, Vector3.up);
            }

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.requiresDepthOption = CameraOverrideOption.On;

            const int width = 1672;
            const int height = 941;
            var targetTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf);
            var previewTexture = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = targetTexture;
                camera.Render();
                RenderTexture.active = targetTexture;
                previewTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                previewTexture.Apply(false, false);
                string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp", fileName));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, previewTexture.EncodeToPNG());
                Debug.Log($"[Level2HighlandEnvironmentBuilder] PREVIEW={outputPath}");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                targetTexture.Release();
                UnityEngine.Object.DestroyImmediate(targetTexture);
                UnityEngine.Object.DestroyImmediate(previewTexture);
                UnityEngine.Object.DestroyImmediate(previewObject);
            }
        }

        private static void EnsureStaticMeshColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = true;
                colliders[i].isTrigger = false;
            }

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.sharedMesh == null)
                    continue;
                GameObjectUtility.SetStaticEditorFlags(filter.gameObject,
                    StaticEditorFlags.NavigationStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                if (filter.GetComponent<Collider>() != null)
                    continue;
                MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
            }
        }

        private static Mesh SaveOrUpdateMesh(string fileName, Mesh generated)
        {
            string path = $"{AssetFolder}/{fileName}";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            existing.name = generated.name;
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static MeshRenderer CreateMeshObject(
            string name,
            Mesh mesh,
            Material material,
            Transform parent,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadowCastingMode;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return renderer;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || !AssetDatabase.IsValidFolder(parent))
                throw new DirectoryNotFoundException($"无法创建资源目录，父目录不存在：{path}");
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
