using System;
using System.Collections.Generic;
using System.IO;
using Game.GameFlow;
using Game.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class CampaignRogueliteSceneBuilder
    {
        private const string LevelOneBuildMarkerName = "_CAMPAIGN_BUILD_20260811_V6";
        private const string BuildMarkerName = "_CAMPAIGN_BUILD_20260811_V11";
        private const string GameFlowPrefabPath = "Assets/Resources/Prefabs/GameFlow.prefab";
        private const string MainCameraPrefabPath = "Assets/Resources/Prefabs/Main Camera.prefab";
        private const string LocalSpawnerPrefabPath = "Assets/Resources/Prefabs/LevelLocalEnemySpawner.prefab";
        private const string ChestPrefabPath = "Assets/Resources/Prefabs/宝箱.prefab";
        private const string ShopPrefabPath = "Assets/Resources/Prefabs/商店.prefab";
        private const string GuardianOnePrefabPath = "Assets/Resources/Prefabs/guardian_1.prefab";
        private const string GuardianTwoPrefabPath = "Assets/Resources/Prefabs/guardian_2.prefab";
        private const string SealMaterialPath = "Assets/Scenes/Level_1/RegionSeal.mat";

        private static readonly Vector2[] RegionSizes =
        {
            new Vector2(82f, 66f),
            new Vector2(94f, 74f),
            new Vector2(102f, 80f),
            new Vector2(110f, 86f),
            new Vector2(120f, 94f),
            new Vector2(150f, 126f),
        };

        private static bool _isBuilding;
        private static Scene _targetScene;
        private static Material _floorMaterial;
        private static Material _wallMaterial;
        private static Material _accentMaterial;
        private static Material _hazardMaterial;
        private static Material _sealMaterial;
        private static Material _voidFogMaterial;
        private static Mesh _spireMesh;
        private static Mesh _boulderMesh;
        private static Mesh _torusMesh;

        private enum SceneStyle
        {
            VerdantCliffs,
            FrozenMonastery,
            VolcanicForge,
            AstralCitadel,
        }

        private sealed class SceneSpec
        {
            public int levelIndex;
            public string themeName;
            public string flowId;
            public SceneStyle style;
            public Vector3 spawnPoint;
            public Vector3[] regionCenters;
            public string[] regionNames;
            public int[] guardianPlanIndices;
            public Color floorColor;
            public Color wallColor;
            public Color accentColor;
            public Color hazardColor;
            public Color fogColor;
            public Color ambientColor;
            public Color sunlightColor;
        }

        static CampaignRogueliteSceneBuilder()
        {
            EditorApplication.delayCall += TryRunPendingBuild;
        }

        public static void RebuildForProjectUpdate()
        {
            if (_isBuilding)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
                throw new InvalidOperationException("当前场景有未保存修改，无法自动重建战役关卡。");

            string originalScenePath = activeScene.IsValid() ? activeScene.path : string.Empty;
            _isBuilding = true;
            try
            {
                string levelOnePath = GetScenePath(1);
                if (!File.Exists(levelOnePath) || !File.ReadAllText(levelOnePath).Contains(LevelOneBuildMarkerName))
                    Level1RogueliteSceneBuilder.RebuildForProjectUpdate();
                SceneSpec[] specs = CreateSceneSpecs();
                for (int i = 0; i < specs.Length; i++)
                    BuildScene(specs[i]);

                RogueliteContentConfigurator.ApplyAll();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[CampaignRogueliteSceneBuilder] CAMPAIGN_BUILD_OK | Levels=5 | RegionsPerLevel=6 | GatesPerLevel=5");
            }
            finally
            {
                _isBuilding = false;
                if (!string.IsNullOrWhiteSpace(originalScenePath) && File.Exists(originalScenePath))
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }
        }

        private static void TryRunPendingBuild()
        {
            if (_isBuilding || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            if (AllScenesContainBuildMarker())
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                Debug.LogWarning("[CampaignRogueliteSceneBuilder] 检测到当前场景有未保存修改，已暂停自动重建。保存后重新加载脚本即可继续。");
                return;
            }

            try
            {
                RebuildForProjectUpdate();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static bool AllScenesContainBuildMarker()
        {
            for (int levelIndex = 1; levelIndex <= 5; levelIndex++)
            {
                string scenePath = GetScenePath(levelIndex);
                string marker = levelIndex == 1 ? LevelOneBuildMarkerName : BuildMarkerName;
                if (!File.Exists(scenePath) || !File.ReadAllText(scenePath).Contains(marker))
                    return false;
            }

            return true;
        }

        private static SceneSpec[] CreateSceneSpecs()
        {
            return new[]
            {
                new SceneSpec
                {
                    levelIndex = 2,
                    themeName = "翠雾悬谷",
                    flowId = "level_2_verdant_cliffs_region_flow",
                    style = SceneStyle.VerdantCliffs,
                    spawnPoint = new Vector3(0f, 0f, 62f),
                    regionCenters = new[]
                    {
                        new Vector3(0f, 0f, 82f),
                        new Vector3(72f, 4f, 195f),
                        new Vector3(-58f, 10f, 315f),
                        new Vector3(68f, 16f, 438f),
                        new Vector3(-44f, 23f, 568f),
                        new Vector3(18f, 31f, 724f),
                    },
                    regionNames = new[] { "苔痕前哨", "古木栈桥", "雾瀑祭坛", "藤冠高庭", "悬谷神殿", "苍翠兽巢" },
                    guardianPlanIndices = new[] { 0, 0, 1, 1, 2 },
                    floorColor = new Color(0.18f, 0.28f, 0.16f),
                    wallColor = new Color(0.24f, 0.19f, 0.12f),
                    accentColor = new Color(0.3f, 0.92f, 0.58f),
                    hazardColor = new Color(0.035f, 0.15f, 0.13f),
                    fogColor = new Color(0.12f, 0.24f, 0.18f),
                    ambientColor = new Color(0.22f, 0.34f, 0.25f),
                    sunlightColor = new Color(0.72f, 0.9f, 0.68f),
                },
                new SceneSpec
                {
                    levelIndex = 3,
                    themeName = "寒晶修道院",
                    flowId = "level_3_frozen_monastery_region_flow",
                    style = SceneStyle.FrozenMonastery,
                    spawnPoint = new Vector3(0f, 0f, 66f),
                    regionCenters = new[]
                    {
                        new Vector3(0f, 0f, 86f),
                        new Vector3(-82f, 3f, 202f),
                        new Vector3(82f, 7f, 320f),
                        new Vector3(0f, 12f, 442f),
                        new Vector3(0f, 18f, 574f),
                        new Vector3(0f, 27f, 730f),
                    },
                    regionNames = new[] { "霜钟庭院", "镜湖回廊", "双塔冰桥", "寒祷圣所", "极昼阶殿", "永冻主祭坛" },
                    guardianPlanIndices = new[] { 0, 1, 1, 2, 2 },
                    floorColor = new Color(0.42f, 0.58f, 0.68f),
                    wallColor = new Color(0.23f, 0.34f, 0.48f),
                    accentColor = new Color(0.38f, 0.92f, 1f),
                    hazardColor = new Color(0.03f, 0.12f, 0.28f),
                    fogColor = new Color(0.32f, 0.48f, 0.62f),
                    ambientColor = new Color(0.34f, 0.46f, 0.58f),
                    sunlightColor = new Color(0.72f, 0.9f, 1f),
                },
                new SceneSpec
                {
                    levelIndex = 4,
                    themeName = "熔炉断城",
                    flowId = "level_4_volcanic_forge_region_flow",
                    style = SceneStyle.VolcanicForge,
                    spawnPoint = new Vector3(0f, 0f, 70f),
                    regionCenters = new[]
                    {
                        new Vector3(0f, 0f, 90f),
                        new Vector3(92f, 5f, 208f),
                        new Vector3(24f, 11f, 334f),
                        new Vector3(-92f, 17f, 454f),
                        new Vector3(0f, 24f, 584f),
                        new Vector3(0f, 34f, 742f),
                    },
                    regionNames = new[] { "灰烬关口", "赤炉工坊", "断链熔桥", "黑铁兵站", "焚风核心", "破城铸王台" },
                    guardianPlanIndices = new[] { 1, 1, 2, 2, 2 },
                    floorColor = new Color(0.12f, 0.105f, 0.1f),
                    wallColor = new Color(0.2f, 0.12f, 0.09f),
                    accentColor = new Color(1f, 0.34f, 0.07f),
                    hazardColor = new Color(0.72f, 0.055f, 0.015f),
                    fogColor = new Color(0.23f, 0.08f, 0.045f),
                    ambientColor = new Color(0.25f, 0.12f, 0.08f),
                    sunlightColor = new Color(1f, 0.55f, 0.3f),
                },
                new SceneSpec
                {
                    levelIndex = 5,
                    themeName = "星渊天宫",
                    flowId = "level_5_astral_citadel_region_flow",
                    style = SceneStyle.AstralCitadel,
                    spawnPoint = new Vector3(0f, 4f, 72f),
                    regionCenters = new[]
                    {
                        new Vector3(0f, 4f, 92f),
                        new Vector3(-92f, 11f, 214f),
                        new Vector3(84f, 19f, 340f),
                        new Vector3(-64f, 29f, 474f),
                        new Vector3(72f, 41f, 614f),
                        new Vector3(0f, 56f, 780f),
                    },
                    regionNames = new[] { "星尘门庭", "月环浮台", "彗光长阶", "虚空观测站", "天穹议庭", "轮回星座" },
                    guardianPlanIndices = new[] { 1, 2, 2, 2, 2 },
                    floorColor = new Color(0.09f, 0.105f, 0.22f),
                    wallColor = new Color(0.18f, 0.12f, 0.3f),
                    accentColor = new Color(0.42f, 0.82f, 1f),
                    hazardColor = new Color(0.015f, 0.008f, 0.055f),
                    fogColor = new Color(0.055f, 0.035f, 0.13f),
                    ambientColor = new Color(0.13f, 0.11f, 0.3f),
                    sunlightColor = new Color(0.68f, 0.72f, 1f),
                },
            };
        }

        private static void BuildScene(SceneSpec spec)
        {
            string scenePath = GetScenePath(spec.levelIndex);
            string sceneFolder = $"Assets/Scenes/Level_{spec.levelIndex}";
            EnsureFolder(sceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException($"无法覆盖保存场景：{scenePath}");

            _targetScene = scene;
            LoadStyleMaterials(spec, sceneFolder);
            ConfigureRenderSettings(spec);
            CreateMainCamera(scene);
            Transform spawnerRoot = CreateSceneRoot(scene, "SpawnAreas");

            GameObject environmentRoot = new GameObject($"Level_{spec.levelIndex}_{spec.themeName}_Environment");
            SceneManager.MoveGameObjectToScene(environmentRoot, scene);
            CreateGroup(environmentRoot.transform, BuildMarkerName);
            Transform terrainRoot = CreateGroup(environmentRoot.transform, "00_地形基底");
            Transform routeRoot = CreateGroup(environmentRoot.transform, "01_主路线");
            Transform arenaRoot = CreateGroup(environmentRoot.transform, "02_战斗区域");
            Transform branchRoot = CreateGroup(environmentRoot.transform, "03_支线房间");
            Transform propRoot = CreateGroup(environmentRoot.transform, "04_主题装饰");
            Transform lightRoot = CreateGroup(environmentRoot.transform, "05_场景灯光");
            Transform flowRoot = CreateGroup(environmentRoot.transform, "06_区域流程");

            BuildBackdrop(spec, terrainRoot);
            BuildRouteAndArenas(spec, routeRoot, arenaRoot, branchRoot, propRoot, out List<Vector3> branchCenters);
            BuildThemeDecorations(spec, propRoot);
            PlaceCampaignShops(spec, propRoot, branchCenters);
            BuildLighting(spec, lightRoot, scene);
            BuildNavigation(spec, scene, sceneFolder);
            ValidateNavigation(spec, branchCenters);
            BuildPrefabEnvironment(spec, propRoot);
            CreateGameFlow(spec, scene);
            CreateFallDeathController(spec, scene);
            ConfigureRegionGameplay(spec, flowRoot, spawnerRoot, branchCenters);
            ValidateSceneComposition(spec, scene, environmentRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            SaveSceneAsText(scene);
            AssetDatabase.SaveAssets();
            RenderScenePreviews(spec);
            Debug.Log($"[CampaignRogueliteSceneBuilder] LEVEL_BUILD_OK | Level={spec.levelIndex} | Theme={spec.themeName} | Regions=6 | Gates=5");
        }

        private static void LoadStyleMaterials(SceneSpec spec, string sceneFolder)
        {
            Color floorColor = Color.Lerp(spec.floorColor, Color.white, spec.style == SceneStyle.VolcanicForge ? 0.22f : 0.14f);
            Color wallColor = Color.Lerp(spec.wallColor, Color.white, spec.style == SceneStyle.AstralCitadel ? 0.2f : 0.12f);
            Texture2D surfaceTexture = CreateOrUpdateProceduralTexture(
                spec,
                $"{sceneFolder}/CampaignSurface.png",
                false);
            Texture2D detailTexture = CreateOrUpdateProceduralTexture(
                spec,
                $"{sceneFolder}/CampaignDetail.png",
                true);
            _floorMaterial = LoadOrCreateMaterial(
                $"{sceneFolder}/CampaignFloor.mat",
                floorColor,
                Color.Lerp(floorColor, wallColor, 0.42f),
                Color.black,
                0.08f,
                spec.style == SceneStyle.FrozenMonastery ? 0.82f : 0.34f,
                surfaceTexture,
                detailTexture,
                spec.style == SceneStyle.FrozenMonastery ? 4.2f : 3.2f,
                0.34f);
            _wallMaterial = LoadOrCreateMaterial(
                $"{sceneFolder}/CampaignArchitecture.mat",
                wallColor,
                Color.Lerp(wallColor, spec.floorColor, 0.34f),
                Color.black,
                spec.style == SceneStyle.VolcanicForge ? 0.72f : 0.24f,
                0.3f,
                surfaceTexture,
                detailTexture,
                4.4f,
                0.38f);
            _accentMaterial = LoadOrCreateMaterial(
                $"{sceneFolder}/CampaignAccent.mat",
                spec.accentColor,
                Color.Lerp(spec.accentColor, Color.white, 0.24f),
                spec.accentColor * (spec.style == SceneStyle.VolcanicForge ? 0.78f : 0.52f),
                0.35f,
                0.72f,
                surfaceTexture,
                detailTexture,
                5.4f,
                0.22f);
            _hazardMaterial = LoadOrCreateMaterial(
                $"{sceneFolder}/CampaignHazard.mat",
                spec.hazardColor,
                Color.Lerp(spec.hazardColor, spec.accentColor, 0.18f),
                spec.hazardColor * (spec.style == SceneStyle.VolcanicForge ? 1.8f : 0.45f),
                0.15f,
                0.48f,
                surfaceTexture,
                detailTexture,
                5.8f,
                0.42f);
            _sealMaterial = AssetDatabase.LoadAssetAtPath<Material>(SealMaterialPath);
            if (_sealMaterial == null)
                throw new InvalidOperationException($"缺少光门材质：{SealMaterialPath}");

            _voidFogMaterial = LoadOrCreateVoidFogMaterial(spec, $"{sceneFolder}/CampaignVoidFog.mat");
            _spireMesh = LoadOrCreateThemeMesh($"{sceneFolder}/CampaignSpire.asset", spec.style, false);
            _boulderMesh = LoadOrCreateThemeMesh($"{sceneFolder}/CampaignBoulder.asset", spec.style, true);
            _torusMesh = LoadOrCreateTorusMesh($"{sceneFolder}/CampaignRing.asset");
        }

        private static Material LoadOrCreateMaterial(
            string path,
            Color baseColor,
            Color secondaryColor,
            Color emissionColor,
            float metallic,
            float smoothness,
            Texture2D baseTexture,
            Texture2D detailTexture,
            float detailScale,
            float patternStrength)
        {
            Shader shader = Shader.Find("Game/Environment/RogueliteSurface");
            if (shader == null)
                throw new InvalidOperationException("找不到场景表面 Shader：Game/Environment/RogueliteSurface");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", baseTexture);
            material.SetTexture("_DetailMap", detailTexture);
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_SecondaryColor", secondaryColor);
            material.SetColor("_EmissionColor", emissionColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_DetailScale", detailScale);
            material.SetFloat("_PatternStrength", patternStrength);
            material.SetFloat("_NormalStrength", Mathf.Lerp(0.62f, 0.24f, smoothness));
            material.SetFloat("_RoughnessVariation", Mathf.Lerp(0.36f, 0.18f, smoothness));
            material.SetFloat("_MacroScale", 0.12f);
            material.SetFloat("_OcclusionStrength", Mathf.Lerp(0.46f, 0.2f, metallic));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateOrUpdateProceduralTexture(SceneSpec spec, string path, bool detailOnly)
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = Path.GetFileNameWithoutExtension(path),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[size * size];
            int styleIndex = (int)spec.style;
            Color dark = Color.Lerp(spec.wallColor, spec.floorColor, 0.34f) * 0.82f;
            Color light = Color.Lerp(spec.floorColor, Color.white, 0.18f);
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float broadVariation = SamplePeriodicFractalNoise(u, v, 3, styleIndex * 97 + 11);
                    float secondaryNoise = SamplePeriodicFractalNoise(u, v, 5, styleIndex * 131 + 47);
                    float fineGrain = SamplePeriodicValueNoise(u, v, 64, styleIndex * 173 + 89);
                    float motif = spec.style switch
                    {
                        SceneStyle.VerdantCliffs => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.82f, secondaryNoise)),
                        SceneStyle.FrozenMonastery => Mathf.Pow(Mathf.Abs(broadVariation - secondaryNoise), 0.62f),
                        SceneStyle.VolcanicForge => 1f - Mathf.SmoothStep(0.035f, 0.19f, Mathf.Abs(secondaryNoise - 0.5f)),
                        SceneStyle.AstralCitadel => Mathf.SmoothStep(0.58f, 0.88f, Mathf.Max(broadVariation, secondaryNoise)),
                        _ => broadVariation,
                    };

                    if (detailOnly)
                    {
                        float value = Mathf.Clamp01(0.18f + broadVariation * 0.46f + secondaryNoise * 0.18f + fineGrain * 0.12f + motif * 0.12f);
                        pixels[y * size + x] = new Color(value, value, value, 1f);
                    }
                    else
                    {
                        float surfaceTone = Mathf.Clamp01(0.12f + broadVariation * 0.56f + secondaryNoise * 0.18f + fineGrain * 0.06f + motif * 0.08f);
                        Color color = Color.Lerp(dark, light, surfaceTone);
                        Color geologicalTint = spec.style switch
                        {
                            SceneStyle.VerdantCliffs => new Color(0.16f, 0.3f, 0.13f, 1f),
                            SceneStyle.FrozenMonastery => new Color(0.58f, 0.75f, 0.84f, 1f),
                            SceneStyle.VolcanicForge => new Color(0.3f, 0.12f, 0.055f, 1f),
                            SceneStyle.AstralCitadel => new Color(0.24f, 0.18f, 0.44f, 1f),
                            _ => light,
                        };
                        color = Color.Lerp(color, geologicalTint, motif * 0.24f);
                        pixels[y * size + x] = new Color(color.r, color.g, color.b, 1f);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = !detailOnly;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }

            Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (importedTexture == null)
                throw new InvalidOperationException($"程序纹理生成失败：{path}");
            return importedTexture;
        }

        private static float SamplePeriodicFractalNoise(float u, float v, int baseFrequency, int seed)
        {
            float value = 0f;
            float amplitude = 0.54f;
            float amplitudeSum = 0f;
            int frequency = Mathf.Max(1, baseFrequency);
            for (int octave = 0; octave < 5; octave++)
            {
                value += SamplePeriodicValueNoise(u, v, frequency, seed + octave * 43) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.52f;
                frequency *= 2;
            }

            return amplitudeSum > 0f ? value / amplitudeSum : 0f;
        }

        private static float SamplePeriodicValueNoise(float u, float v, int frequency, int seed)
        {
            float sampleX = Mathf.Repeat(u, 1f) * frequency;
            float sampleY = Mathf.Repeat(v, 1f) * frequency;
            int x0 = Mathf.FloorToInt(sampleX);
            int y0 = Mathf.FloorToInt(sampleY);
            int x1 = (x0 + 1) % frequency;
            int y1 = (y0 + 1) % frequency;
            x0 %= frequency;
            y0 %= frequency;
            float blendX = Mathf.SmoothStep(0f, 1f, sampleX - Mathf.Floor(sampleX));
            float blendY = Mathf.SmoothStep(0f, 1f, sampleY - Mathf.Floor(sampleY));
            float bottom = Mathf.Lerp(HashNoise(x0, y0, seed), HashNoise(x1, y0, seed), blendX);
            float top = Mathf.Lerp(HashNoise(x0, y1, seed), HashNoise(x1, y1, seed), blendX);
            return Mathf.Lerp(bottom, top, blendY);
        }

        private static float HashNoise(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static Material LoadOrCreateVoidFogMaterial(SceneSpec spec, string path)
        {
            Shader shader = Shader.Find("Game/VFX/CliffVoidFog");
            if (shader == null)
                throw new InvalidOperationException("找不到悬崖雾幕 Shader：Game/VFX/CliffVoidFog");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "CampaignVoidFog" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Color fogColor = Color.Lerp(spec.hazardColor, spec.fogColor, 0.62f);
            fogColor.a = spec.style == SceneStyle.AstralCitadel ? 0.8f : 0.7f;
            material.SetColor("_BaseColor", fogColor);
            material.SetColor("_GlowColor", spec.accentColor * 0.72f);
            material.SetFloat("_Density", spec.style == SceneStyle.VolcanicForge ? 0.68f : 0.94f);
            material.SetFloat("_FlowSpeed", spec.style == SceneStyle.FrozenMonastery ? 0.08f : 0.18f);
            material.SetFloat("_NoiseScale", spec.style == SceneStyle.AstralCitadel ? 5.4f : 3.6f);
            material.SetFloat("_EdgeFade", 0.1f);
            material.SetFloat("_DepthSoftness", 2.8f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh LoadOrCreateThemeMesh(string path, SceneStyle style, bool broad)
        {
            Mesh generated = BuildFacetedThemeMesh(Path.GetFileNameWithoutExtension(path), style, broad);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = generated;
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(generated, mesh);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        private static Mesh BuildFacetedThemeMesh(string name, SceneStyle style, bool broad)
        {
            int sides = style == SceneStyle.FrozenMonastery ? 6 : 8;
            float[] heights = { 0f, 0.18f, 0.68f, 1f };
            float[] radii = broad
                ? new[] { 0.54f, 1f, 0.78f, 0.16f }
                : new[] { 0.34f, 0.72f, 0.48f, 0.045f };
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            int styleIndex = (int)style + 1;
            for (int ring = 0; ring < heights.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides + ring * 0.11f * styleIndex;
                    float irregularity = 1f + Mathf.Sin(side * 2.31f + ring * 1.47f + styleIndex) * (broad ? 0.16f : 0.08f);
                    float radius = radii[ring] * irregularity;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, heights[ring], Mathf.Sin(angle) * radius));
                    uvs.Add(new Vector2(side / (float)sides, heights[ring]));
                }
            }

            for (int ring = 0; ring < heights.Length - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int nextSide = (side + 1) % sides;
                    int a = ring * sides + side;
                    int b = ring * sides + nextSide;
                    int c = (ring + 1) * sides + side;
                    int d = (ring + 1) * sides + nextSide;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh LoadOrCreateTorusMesh(string path)
        {
            Mesh generated = BuildTorusMesh(Path.GetFileNameWithoutExtension(path), 28, 8, 1f, 0.12f);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = generated;
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(generated, mesh);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        private static Mesh BuildTorusMesh(string name, int ringSegments, int tubeSegments, float radius, float tubeRadius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring <= ringSegments; ring++)
            {
                float ringT = ring / (float)ringSegments;
                float ringAngle = ringT * Mathf.PI * 2f;
                Vector3 ringDirection = new Vector3(Mathf.Cos(ringAngle), 0f, Mathf.Sin(ringAngle));
                for (int tube = 0; tube <= tubeSegments; tube++)
                {
                    float tubeT = tube / (float)tubeSegments;
                    float tubeAngle = tubeT * Mathf.PI * 2f;
                    Vector3 normal = ringDirection * Mathf.Cos(tubeAngle) + Vector3.up * Mathf.Sin(tubeAngle);
                    vertices.Add(ringDirection * radius + normal * tubeRadius);
                    normals.Add(normal);
                    uvs.Add(new Vector2(ringT, tubeT));
                }
            }

            int stride = tubeSegments + 1;
            for (int ring = 0; ring < ringSegments; ring++)
            {
                for (int tube = 0; tube < tubeSegments; tube++)
                {
                    int a = ring * stride + tube;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ConfigureRenderSettings(SceneSpec spec)
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(spec.fogColor, spec.ambientColor, 0.58f);
            RenderSettings.fogDensity = spec.style == SceneStyle.AstralCitadel ? 0.0018f : 0.00145f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(spec.ambientColor, Color.white, 0.58f);
            RenderSettings.ambientEquatorColor = Color.Lerp(spec.ambientColor, Color.white, 0.26f);
            RenderSettings.ambientGroundColor = Color.Lerp(spec.wallColor, spec.ambientColor, 0.44f);
            RenderSettings.ambientIntensity = 1.48f;
            RenderSettings.reflectionIntensity = 0.8f;
        }

        private static void BuildBackdrop(SceneSpec spec, Transform parent)
        {
            float backdropY = spec.style == SceneStyle.AstralCitadel ? -20f : -4f;
            CreateBox(
                "世界基底",
                new Vector3(0f, backdropY, 390f),
                new Vector3(520f, 2f, 980f),
                _hazardMaterial,
                parent,
                false,
                false);

            if (spec.style == SceneStyle.VolcanicForge)
            {
                for (int i = 0; i < 9; i++)
                {
                    float x = i % 2 == 0 ? -135f : 135f;
                    float z = 60f + i * 82f;
                    CreateBox($"熔岩河_{i + 1:00}", new Vector3(x, -1.8f + i * 3.1f, z), new Vector3(95f, 0.7f, 62f), _hazardMaterial, parent, false, false, Quaternion.Euler(0f, i % 2 == 0 ? -18f : 18f, 0f));
                }
            }
            else if (spec.style == SceneStyle.VerdantCliffs)
            {
                for (int i = 0; i < 10; i++)
                {
                    float x = i % 2 == 0 ? -155f : 155f;
                    float z = 30f + i * 78f;
                    CreateCylinder($"远景山台_{i + 1:00}", new Vector3(x, -5f + i * 2.8f, z), 44f + i % 3 * 12f, 18f + i % 4 * 7f, _wallMaterial, parent, false);
                }
            }
            else if (spec.style == SceneStyle.FrozenMonastery)
            {
                CreateBox("冻结湖面", new Vector3(0f, -1.7f, 385f), new Vector3(430f, 0.4f, 900f), _hazardMaterial, parent, false, false);
            }

            BuildVoidFog(spec, parent);
        }

        private static void BuildVoidFog(SceneSpec spec, Transform parent)
        {
            float fogY = spec.style == SceneStyle.AstralCitadel ? -7.5f : -2.7f;
            CreateFogQuad(
                "悬崖下层流雾",
                new Vector3(0f, fogY, 390f),
                new Vector2(520f, 980f),
                Quaternion.Euler(90f, 0f, 0f),
                parent);

            CreateFogQuad("西侧远景雾墙", new Vector3(-245f, fogY + 22f, 390f), new Vector2(980f, 48f), Quaternion.Euler(0f, 90f, 0f), parent);
            CreateFogQuad("东侧远景雾墙", new Vector3(245f, fogY + 22f, 390f), new Vector2(980f, 48f), Quaternion.Euler(0f, -90f, 0f), parent);
            CreateFogQuad("南侧远景雾墙", new Vector3(0f, fogY + 22f, -90f), new Vector2(520f, 48f), Quaternion.identity, parent);
            CreateFogQuad("北侧远景雾墙", new Vector3(0f, fogY + 22f, 870f), new Vector2(520f, 48f), Quaternion.Euler(0f, 180f, 0f), parent);

            for (int i = 0; i < spec.regionCenters.Length; i++)
            {
                Vector3 center = spec.regionCenters[i] + Vector3.down * 1.35f;
                Vector2 size = RegionSizes[i];
                float stripDepth = 24f;
                CreateFogQuad($"{spec.regionNames[i]}_北侧悬崖雾", center + Vector3.forward * (size.y * 0.5f + stripDepth * 0.48f), new Vector2(size.x + 36f, stripDepth), Quaternion.Euler(90f, 0f, 0f), parent);
                CreateFogQuad($"{spec.regionNames[i]}_南侧悬崖雾", center + Vector3.back * (size.y * 0.5f + stripDepth * 0.48f), new Vector2(size.x + 36f, stripDepth), Quaternion.Euler(90f, 0f, 0f), parent);
                CreateFogQuad($"{spec.regionNames[i]}_西侧悬崖雾", center + Vector3.left * (size.x * 0.5f + stripDepth * 0.48f), new Vector2(size.y + 34f, stripDepth), Quaternion.Euler(90f, 90f, 0f), parent);
                CreateFogQuad($"{spec.regionNames[i]}_东侧悬崖雾", center + Vector3.right * (size.x * 0.5f + stripDepth * 0.48f), new Vector2(size.y + 34f, stripDepth), Quaternion.Euler(90f, 90f, 0f), parent);
            }
        }

        private static void BuildRouteAndArenas(
            SceneSpec spec,
            Transform routeRoot,
            Transform arenaRoot,
            Transform branchRoot,
            Transform propRoot,
            out List<Vector3> branchCenters)
        {
            branchCenters = new List<Vector3>();

            for (int i = 0; i < spec.regionCenters.Length; i++)
            {
                bool isBossRegion = i == spec.regionCenters.Length - 1;
                var arenaOpenings = new List<Vector3>();
                BuildArena(arenaRoot, spec.regionNames[i], spec.regionCenters[i], RegionSizes[i], isBossRegion);
                Vector3 mainEntry = spec.spawnPoint;
                Vector3 mainExit = spec.regionCenters[i];

                if (i > 0)
                {
                    Vector3 incomingDirection = HorizontalDirection(spec.regionCenters[i], spec.regionCenters[i - 1]);
                    mainEntry = GetArenaBoundaryPoint(spec.regionCenters[i], RegionSizes[i], incomingDirection);
                    arenaOpenings.Add(mainEntry);
                }

                if (i < spec.regionCenters.Length - 1)
                {
                    Vector3 routeDirection = HorizontalDirection(spec.regionCenters[i], spec.regionCenters[i + 1]);
                    Vector3 routeStart = GetArenaBoundaryPoint(spec.regionCenters[i], RegionSizes[i], routeDirection);
                    Vector3 routeEnd = GetArenaBoundaryPoint(spec.regionCenters[i + 1], RegionSizes[i + 1], -routeDirection);
                    mainExit = routeStart;
                    arenaOpenings.Add(routeStart);
                    BuildRampRoute(routeRoot, $"主路线_{i + 1:00}_{i + 2:00}", routeStart, routeEnd, 22f + i * 0.8f);
                }

                BuildArenaGuidance(arenaRoot, spec.regionNames[i] + "_主路线引导", mainEntry, spec.regionCenters[i], mainExit);

                Vector3 previous = i == 0 ? spec.spawnPoint : spec.regionCenters[i - 1];
                Vector3 next = i < spec.regionCenters.Length - 1 ? spec.regionCenters[i + 1] : spec.regionCenters[i] + Vector3.forward * 40f;
                Vector3 tangent = HorizontalDirection(previous, next);
                Vector3 perpendicular = new Vector3(-tangent.z, 0f, tangent.x);

                if (!isBossRegion)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    Vector3 branchCenter = spec.regionCenters[i]
                        + perpendicular * side * (RegionSizes[i].x * 0.5f + 42f)
                        + Vector3.up * (i % 2 == 0 ? 2.5f : 4.5f);
                    Vector3 branchDirection = HorizontalDirection(spec.regionCenters[i], branchCenter);
                    Vector3 branchRouteStart = GetArenaBoundaryPoint(spec.regionCenters[i], RegionSizes[i], branchDirection);
                    Vector3 branchRouteEnd = GetArenaBoundaryPoint(branchCenter, new Vector2(44f, 38f), -branchDirection);
                    arenaOpenings.Add(branchRouteStart);
                    BuildRampRoute(branchRoot, $"{spec.regionNames[i]}_支线路径", branchRouteStart, branchRouteEnd, 12f);
                    BuildArena(branchRoot, $"{spec.regionNames[i]}_支线房间", branchCenter, new Vector2(44f, 38f), false, true);
                    BuildArenaEnclosure(
                        branchRoot,
                        $"{spec.regionNames[i]}_支线高墙",
                        branchCenter,
                        new Vector2(44f, 38f),
                        new[] { branchRouteEnd },
                        8f);
                    PlaceChest(propRoot, $"{spec.regionNames[i]}_支线宝箱", branchCenter + tangent * 4f);
                    branchCenters.Add(branchCenter);

                    if (i == 1 || i == 3)
                        BuildUpperDeck(branchRoot, spec, i, perpendicular * -side, tangent);
                }
                else
                {
                    BuildBossLandmark(propRoot, spec, spec.regionCenters[i]);
                }

                BuildArenaEnclosure(
                    arenaRoot,
                    $"{spec.regionNames[i]}_区域高墙",
                    spec.regionCenters[i],
                    RegionSizes[i],
                    arenaOpenings,
                    isBossRegion ? 14f : 11f);
            }
        }

        private static void BuildArena(
            Transform parent,
            string name,
            Vector3 center,
            Vector2 size,
            bool isBossRegion,
            bool isBranchRoom = false)
        {
            Transform arena = CreateGroup(parent, name);
            CreateBox(name + "_地面", center + Vector3.down * 0.5f, new Vector3(size.x, 1f, size.y), _floorMaterial, arena, true, true);

            float x = size.x * 0.42f;
            float z = size.y * 0.4f;
            float pillarHeight = isBossRegion ? 14f : isBranchRoom ? 5f : 8f;
            CreateCylinder("界柱_西南", center + new Vector3(-x, pillarHeight * 0.5f, -z), 2.6f, pillarHeight, _wallMaterial, arena, true);
            CreateCylinder("界柱_东南", center + new Vector3(x, pillarHeight * 0.5f, -z), 2.6f, pillarHeight, _wallMaterial, arena, true);
            CreateCylinder("界柱_西北", center + new Vector3(-x, pillarHeight * 0.5f, z), 2.6f, pillarHeight, _wallMaterial, arena, true);
            CreateCylinder("界柱_东北", center + new Vector3(x, pillarHeight * 0.5f, z), 2.6f, pillarHeight, _wallMaterial, arena, true);

            if (isBossRegion)
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * size.x * 0.34f, 0f, Mathf.Sin(angle) * size.y * 0.34f);
                    CreateCylinder($"王座环柱_{i + 1:00}", center + offset + Vector3.up * 7f, 3.6f, 14f, i % 2 == 0 ? _wallMaterial : _accentMaterial, arena, true);
                }
                return;
            }

            CreateBox("掩体_左", center + new Vector3(-size.x * 0.2f, 1.2f, -size.y * 0.13f), new Vector3(8f, 2.4f, 4.5f), _wallMaterial, arena, true, true, Quaternion.Euler(0f, 22f, 0f));
            CreateBox("掩体_右", center + new Vector3(size.x * 0.21f, 1.2f, size.y * 0.15f), new Vector3(8f, 2.4f, 4.5f), _wallMaterial, arena, true, true, Quaternion.Euler(0f, -22f, 0f));
        }

        private static void BuildArenaGuidance(Transform parent, string name, params Vector3[] points)
        {
            Transform guidanceRoot = CreateGroup(parent, name);
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 start = points[i];
                Vector3 end = points[i + 1];
                Vector3 delta = end - start;
                delta.y = 0f;
                float length = delta.magnitude;
                if (length < 1f)
                    continue;

                float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 midpoint = (start + end) * 0.5f + Vector3.up * 0.055f;
                CreateBox(
                    $"{name}_{i + 1:00}_光带",
                    midpoint,
                    new Vector3(2.2f, 0.09f, length),
                    _accentMaterial,
                    guidanceRoot,
                    false,
                    false,
                    rotation);

                int markerCount = Mathf.FloorToInt(length / 9f);
                for (int markerIndex = 1; markerIndex <= markerCount; markerIndex++)
                {
                    float t = markerIndex / (float)(markerCount + 1);
                    Vector3 markerPosition = Vector3.Lerp(start, end, t) + Vector3.up * 0.07f;
                    CreateBox(
                        $"{name}_{i + 1:00}_方向刻度_{markerIndex:00}",
                        markerPosition,
                        new Vector3(5.2f, 0.08f, 0.3f),
                        _accentMaterial,
                        guidanceRoot,
                        false,
                        false,
                        rotation);
                }
            }
        }

        private static void BuildArenaEnclosure(
            Transform parent,
            string name,
            Vector3 center,
            Vector2 size,
            IReadOnlyList<Vector3> openings,
            float wallHeight)
        {
            Transform enclosure = CreateGroup(parent, name);
            BuildEnclosureSide(enclosure, "北墙", center, size, openings, wallHeight, 0);
            BuildEnclosureSide(enclosure, "南墙", center, size, openings, wallHeight, 1);
            BuildEnclosureSide(enclosure, "东墙", center, size, openings, wallHeight, 2);
            BuildEnclosureSide(enclosure, "西墙", center, size, openings, wallHeight, 3);
        }

        private static void BuildEnclosureSide(
            Transform parent,
            string name,
            Vector3 center,
            Vector2 size,
            IReadOnlyList<Vector3> openings,
            float wallHeight,
            int side)
        {
            bool horizontal = side <= 1;
            float halfLength = horizontal ? size.x * 0.5f : size.y * 0.5f;
            var gaps = new List<Vector2>();
            for (int i = 0; i < openings.Count; i++)
            {
                Vector3 local = openings[i] - center;
                float[] distances =
                {
                    Mathf.Abs(local.z - size.y * 0.5f),
                    Mathf.Abs(local.z + size.y * 0.5f),
                    Mathf.Abs(local.x - size.x * 0.5f),
                    Mathf.Abs(local.x + size.x * 0.5f),
                };
                int closestSide = 0;
                for (int candidate = 1; candidate < distances.Length; candidate++)
                {
                    if (distances[candidate] < distances[closestSide])
                        closestSide = candidate;
                }

                if (closestSide != side)
                    continue;

                float offset = horizontal ? local.x : local.z;
                const float halfOpening = 16f;
                gaps.Add(new Vector2(
                    Mathf.Clamp(offset - halfOpening, -halfLength, halfLength),
                    Mathf.Clamp(offset + halfOpening, -halfLength, halfLength)));
                BuildOpeningFrame(parent, $"{name}_门框_{gaps.Count:00}", center, size, openings[i], wallHeight, side);
            }

            gaps.Sort((left, right) => left.x.CompareTo(right.x));
            float cursor = -halfLength;
            for (int i = 0; i < gaps.Count; i++)
            {
                if (gaps[i].x > cursor)
                    BuildEnclosureWallSegment(parent, $"{name}_{i + 1:00}", center, size, cursor, gaps[i].x, wallHeight, side);
                cursor = Mathf.Max(cursor, gaps[i].y);
            }

            if (cursor < halfLength)
                BuildEnclosureWallSegment(parent, $"{name}_末段", center, size, cursor, halfLength, wallHeight, side);
        }

        private static void BuildEnclosureWallSegment(
            Transform parent,
            string name,
            Vector3 center,
            Vector2 size,
            float start,
            float end,
            float wallHeight,
            int side)
        {
            float length = end - start;
            if (length < 0.8f)
                return;

            bool horizontal = side <= 1;
            float along = (start + end) * 0.5f;
            Vector3 position = horizontal
                ? center + new Vector3(along, wallHeight * 0.5f, (side == 0 ? 1f : -1f) * size.y * 0.5f)
                : center + new Vector3((side == 2 ? 1f : -1f) * size.x * 0.5f, wallHeight * 0.5f, along);
            Vector3 wallScale = horizontal
                ? new Vector3(length, wallHeight, 2.6f)
                : new Vector3(2.6f, wallHeight, length);
            Vector3 capScale = horizontal
                ? new Vector3(length + 0.25f, 0.42f, 3.1f)
                : new Vector3(3.1f, 0.42f, length + 0.25f);
            CreateBox(name + "_墙体", position, wallScale, _wallMaterial, parent, true, true);
            CreateBox(name + "_顶部辉光压条", position + Vector3.up * (wallHeight * 0.5f + 0.16f), capScale, _accentMaterial, parent, false, false);
        }

        private static void BuildOpeningFrame(
            Transform parent,
            string name,
            Vector3 center,
            Vector2 size,
            Vector3 opening,
            float wallHeight,
            int side)
        {
            Vector3 tangent = side <= 1 ? Vector3.right : Vector3.forward;
            Vector3 boundary = opening;
            boundary.y = center.y;
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 towerPosition = boundary + tangent * (i * 17.4f) + Vector3.up * ((wallHeight + 2f) * 0.5f);
                CreateCylinder($"{name}_{(i < 0 ? "左" : "右")}", towerPosition, 2.3f, wallHeight + 2f, _wallMaterial, parent, true);
                CreateMeshDecoration(
                    $"{name}_{(i < 0 ? "左" : "右")}_冠饰",
                    boundary + tangent * (i * 17.4f) + Vector3.up * (wallHeight + 2.6f),
                    new Vector3(2.8f, 3.8f, 2.8f),
                    Quaternion.identity,
                    _spireMesh,
                    _accentMaterial,
                    parent);
            }
        }

        private static void BuildRampRoute(Transform parent, string name, Vector3 start, Vector3 end, float width, bool highWalls = true)
        {
            Transform route = CreateGroup(parent, name);
            Vector3 delta = end - start;
            float horizontalLength = new Vector2(delta.x, delta.z).magnitude;
            float length = delta.magnitude;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(delta.y, Mathf.Max(0.01f, horizontalLength)) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 midpoint = (start + end) * 0.5f - Vector3.up * 0.5f;
            Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);
            int stepCount = Mathf.Max(
                2,
                Mathf.CeilToInt(horizontalLength / 8f),
                Mathf.CeilToInt(Mathf.Abs(delta.y) / 0.22f) + 1);
            float stepLength = horizontalLength / stepCount;
            for (int i = 0; i < stepCount; i++)
            {
                float positionT = (i + 0.5f) / stepCount;
                float heightT = stepCount == 1 ? 0f : (float)i / (stepCount - 1);
                Vector3 stepPosition = Vector3.Lerp(start, end, positionT);
                stepPosition.y = Mathf.Lerp(start.y, end.y, heightT) - 0.5f;
                CreateBox(
                    $"{name}_路面台阶_{i + 1:00}",
                    stepPosition,
                    new Vector3(width, 1f, stepLength + 1.2f),
                    _floorMaterial,
                    route,
                    true,
                    true,
                    flatRotation);
            }
            CreateBox(name + "_导向光带", midpoint + Vector3.up * 0.54f, new Vector3(Mathf.Max(1.2f, width * 0.09f), 0.08f, length * 0.92f), _accentMaterial, route, false, false, rotation);

            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float wallHeight = highWalls ? 9f : 2.2f;
            float wallLength = length + 2.5f;
            Vector3 wallScale = new Vector3(1.5f, wallHeight, wallLength);
            Vector3 wallLift = Vector3.up * (wallHeight * 0.5f + 0.5f);
            Vector3 leftWallPosition = midpoint - right * (width * 0.5f + 0.75f) + wallLift;
            Vector3 rightWallPosition = midpoint + right * (width * 0.5f + 0.75f) + wallLift;
            CreateBox(name + "_左侧连续高墙", leftWallPosition, wallScale, _wallMaterial, route, true, true, rotation);
            CreateBox(name + "_右侧连续高墙", rightWallPosition, wallScale, _wallMaterial, route, true, true, rotation);
            Vector3 trimScale = new Vector3(1.9f, 0.32f, wallLength);
            CreateBox(name + "_左墙顶饰", leftWallPosition + Vector3.up * (wallHeight * 0.5f + 0.14f), trimScale, _accentMaterial, route, false, false, rotation);
            CreateBox(name + "_右墙顶饰", rightWallPosition + Vector3.up * (wallHeight * 0.5f + 0.14f), trimScale, _accentMaterial, route, false, false, rotation);
        }

        private static void BuildUpperDeck(Transform parent, SceneSpec spec, int regionIndex, Vector3 sideDirection, Vector3 tangent)
        {
            Vector3 center = spec.regionCenters[regionIndex];
            Vector3 deckCenter = center + sideDirection.normalized * 22f + tangent * 3f + Vector3.up * 6f;
            BuildRampRoute(parent, $"{spec.regionNames[regionIndex]}_二层坡道", center + sideDirection.normalized * 5f, deckCenter - sideDirection.normalized * 12f, 10f, false);
            BuildArena(parent, $"{spec.regionNames[regionIndex]}_二层平台", deckCenter, new Vector2(32f, 26f), false, true);
        }

        private static void BuildThemeDecorations(SceneSpec spec, Transform parent)
        {
            var random = new System.Random(spec.levelIndex * 7919);
            for (int regionIndex = 0; regionIndex < spec.regionCenters.Length; regionIndex++)
            {
                Vector3 center = spec.regionCenters[regionIndex];
                for (int i = 0; i < 12; i++)
                {
                    float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                    float radius = 58f + (float)random.NextDouble() * 52f;
                    Vector3 position = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    BuildThemeProp(spec, parent, $"主题装饰_{regionIndex + 1:00}_{i + 1:00}", position, random);
                }

                BuildRegionSetPiece(spec, parent, regionIndex);
            }
        }

        private static void BuildThemeProp(SceneSpec spec, Transform parent, string name, Vector3 position, System.Random random)
        {
            float scale = 0.75f + (float)random.NextDouble() * 1.25f;
            switch (spec.style)
            {
                case SceneStyle.VerdantCliffs:
                    CreateMeshDecoration(name + "_盘根树干", position, new Vector3(2.3f, 13f, 2.3f) * scale, iRotation(random, 5f), _spireMesh, _wallMaterial, parent);
                    CreateMeshDecoration(name + "_树冠_A", position + new Vector3(-1.8f, 10.5f, 0f) * scale, new Vector3(5.4f, 5.8f, 5.4f) * scale, iRotation(random, 18f), _boulderMesh, _floorMaterial, parent);
                    CreateMeshDecoration(name + "_树冠_B", position + new Vector3(2.1f, 12.4f, 0.8f) * scale, new Vector3(4.4f, 4.8f, 4.4f) * scale, iRotation(random, 18f), _boulderMesh, _floorMaterial, parent);
                    break;
                case SceneStyle.FrozenMonastery:
                    CreateMeshDecoration(name + "_寒晶主簇", position, new Vector3(2.8f, 13f, 2.8f) * scale, iRotation(random, 12f), _spireMesh, _accentMaterial, parent);
                    CreateMeshDecoration(name + "_寒晶侧簇_A", position + new Vector3(-2.4f, 0f, 1.2f) * scale, new Vector3(1.8f, 8f, 1.8f) * scale, Quaternion.Euler(-12f, random.Next(0, 360), 16f), _spireMesh, _wallMaterial, parent);
                    CreateMeshDecoration(name + "_寒晶侧簇_B", position + new Vector3(2.2f, 0f, -0.8f) * scale, new Vector3(1.5f, 6.5f, 1.5f) * scale, Quaternion.Euler(14f, random.Next(0, 360), -18f), _spireMesh, _accentMaterial, parent);
                    break;
                case SceneStyle.VolcanicForge:
                    CreateMeshDecoration(name + "_炉基", position, new Vector3(5.5f, 4.5f, 5.5f) * scale, Quaternion.identity, _boulderMesh, _wallMaterial, parent);
                    CreateCylinder(name + "_锻炉塔", position + Vector3.up * 7f * scale, 4.2f * scale, 13f * scale, _wallMaterial, parent, false);
                    CreateMeshDecoration(name + "_炉环", position + Vector3.up * 8f * scale, new Vector3(3.4f, 3.4f, 3.4f) * scale, Quaternion.Euler(90f, 0f, 0f), _torusMesh, _accentMaterial, parent);
                    CreateBox(name + "_炉口", position + Vector3.up * 7f * scale, new Vector3(2.8f, 4f, 2.8f) * scale, _accentMaterial, parent, false, false);
                    break;
                case SceneStyle.AstralCitadel:
                    CreateMeshDecoration(name + "_星界尖塔", position, new Vector3(2.4f, 18f, 2.4f) * scale, iRotation(random, 14f), _spireMesh, random.Next(0, 2) == 0 ? _accentMaterial : _wallMaterial, parent);
                    CreateMeshDecoration(name + "_星轨_A", position + Vector3.up * 9f * scale, new Vector3(5.4f, 5.4f, 5.4f) * scale, Quaternion.Euler(68f, random.Next(0, 360), 18f), _torusMesh, _accentMaterial, parent);
                    CreateMeshDecoration(name + "_星轨_B", position + Vector3.up * 9f * scale, new Vector3(4f, 4f, 4f) * scale, Quaternion.Euler(18f, random.Next(0, 360), 72f), _torusMesh, _wallMaterial, parent);
                    break;
            }
        }

        private static void BuildRegionSetPiece(SceneSpec spec, Transform parent, int regionIndex)
        {
            Vector3 center = spec.regionCenters[regionIndex];
            Vector3 previous = regionIndex == 0 ? spec.spawnPoint : spec.regionCenters[regionIndex - 1];
            Vector3 next = regionIndex < spec.regionCenters.Length - 1 ? spec.regionCenters[regionIndex + 1] : center + Vector3.forward * 50f;
            Vector3 tangent = HorizontalDirection(previous, next);
            Vector3 perpendicular = new Vector3(-tangent.z, 0f, tangent.x);
            Vector3 landmarkCenter = center + perpendicular * (RegionSizes[regionIndex].x * 0.34f);
            float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            Transform landmark = CreateGroup(parent, $"{spec.regionNames[regionIndex]}_程序建模地标");

            switch (spec.style)
            {
                case SceneStyle.VerdantCliffs:
                    for (int i = -2; i <= 2; i++)
                    {
                        Vector3 rootPosition = landmarkCenter + tangent * i * 5f;
                        CreateMeshDecoration($"盘根石_{i + 3:00}", rootPosition, new Vector3(7f, 3f + Mathf.Abs(i), 6f), Quaternion.Euler(0f, yaw + i * 17f, 0f), _boulderMesh, _wallMaterial, landmark);
                        CreateMeshDecoration($"古木柱_{i + 3:00}", rootPosition + Vector3.up * 1.2f, new Vector3(1.5f, 10f + (2 - Mathf.Abs(i)) * 2f, 1.5f), Quaternion.Euler(i * 3f, yaw, -i * 4f), _spireMesh, _floorMaterial, landmark);
                    }
                    break;
                case SceneStyle.FrozenMonastery:
                    for (int i = -3; i <= 3; i++)
                    {
                        float height = 9f + (3 - Mathf.Abs(i)) * 3f;
                        CreateMeshDecoration($"冰祷晶柱_{i + 4:00}", landmarkCenter + tangent * i * 4f, new Vector3(2.2f, height, 2.2f), Quaternion.Euler(i * 2f, yaw + i * 9f, -i * 5f), _spireMesh, i % 2 == 0 ? _accentMaterial : _wallMaterial, landmark);
                    }
                    CreateMeshDecoration("冰祷环", landmarkCenter + Vector3.up * 10f, new Vector3(10f, 10f, 10f), Quaternion.Euler(90f, yaw, 0f), _torusMesh, _accentMaterial, landmark);
                    break;
                case SceneStyle.VolcanicForge:
                    CreateCylinder("断城主炉", landmarkCenter + Vector3.up * 7f, 11f, 14f, _wallMaterial, landmark, false);
                    CreateMeshDecoration("断城炉冠", landmarkCenter + Vector3.up * 12f, new Vector3(7f, 7f, 7f), Quaternion.Euler(90f, yaw, 0f), _torusMesh, _accentMaterial, landmark);
                    for (int i = -2; i <= 2; i++)
                    {
                        Vector3 pipePosition = landmarkCenter + tangent * i * 6f + perpendicular * 5f;
                        CreateCylinder($"导热管_{i + 3:00}", pipePosition + Vector3.up * 4f, 1.4f, 8f + Mathf.Abs(i) * 2f, i == 0 ? _accentMaterial : _wallMaterial, landmark, false);
                    }
                    break;
                case SceneStyle.AstralCitadel:
                    CreateMeshDecoration("天宫星核塔", landmarkCenter, new Vector3(5f, 24f, 5f), Quaternion.Euler(0f, yaw, 0f), _spireMesh, _accentMaterial, landmark);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateMeshDecoration($"星仪轨道_{i + 1:00}", landmarkCenter + Vector3.up * (11f + i * 2f), Vector3.one * (10f + i * 3f), Quaternion.Euler(25f + i * 38f, yaw + i * 51f, i * 24f), _torusMesh, i == 1 ? _wallMaterial : _accentMaterial, landmark);
                    }
                    break;
            }
        }

        private static Quaternion iRotation(System.Random random, float tilt)
        {
            return Quaternion.Euler(
                Mathf.Lerp(-tilt, tilt, (float)random.NextDouble()),
                (float)random.NextDouble() * 360f,
                Mathf.Lerp(-tilt, tilt, (float)random.NextDouble()));
        }

        private static void BuildBossLandmark(Transform parent, SceneSpec spec, Vector3 center)
        {
            CreateCylinder("Boss祭坛", center + Vector3.down * 0.15f, 18f, 0.6f, _accentMaterial, parent, false);
            CreateBox("Boss王座背墙", center + new Vector3(0f, 9f, RegionSizes[5].y * 0.34f), new Vector3(38f, 18f, 3f), _wallMaterial, parent, true, true);
            CreateBox("Boss王座核心", center + new Vector3(0f, 8f, RegionSizes[5].y * 0.33f - 1.8f), new Vector3(9f, 13f, 1f), _accentMaterial, parent, false, false);
        }

        private static void BuildPrefabEnvironment(SceneSpec spec, Transform parent)
        {
            const string torchPath = "Assets/Eternal Temple/Prefabs/Lanterns/Torch_01.prefab";
            string accentPrefabPath = spec.style switch
            {
                SceneStyle.VerdantCliffs => "Assets/Eternal Temple/Prefabs/Flora/Tree_01.prefab",
                SceneStyle.FrozenMonastery => "Assets/_DLNK/Alien Terrain Pack/[Prefabs]/Details/Minerals/AMineral04.prefab",
                SceneStyle.VolcanicForge => "Assets/_DLNK/Alien Terrain Pack/[Prefabs]/Details/Rocks/RockGroup00.prefab",
                SceneStyle.AstralCitadel => "Assets/_DLNK/Alien Terrain Pack/[Prefabs]/Terrain/Floating/FloatingRockCrystals00.prefab",
                _ => throw new ArgumentOutOfRangeException(),
            };

            for (int regionIndex = 0; regionIndex < spec.regionCenters.Length; regionIndex++)
            {
                Vector3 center = spec.regionCenters[regionIndex];
                Vector3 previous = regionIndex == 0 ? spec.spawnPoint : spec.regionCenters[regionIndex - 1];
                Vector3 next = regionIndex < spec.regionCenters.Length - 1
                    ? spec.regionCenters[regionIndex + 1]
                    : center + HorizontalDirection(previous, center) * 60f;
                Vector3 tangent = HorizontalDirection(previous, next);
                Vector3 perpendicular = new Vector3(-tangent.z, 0f, tangent.x);
                PlaceDecorativePrefab(
                    accentPrefabPath,
                    $"{spec.regionNames[regionIndex]}_资源点缀",
                    center + perpendicular * (RegionSizes[regionIndex].x * 0.5f + 18f) + tangent * 9f,
                    -perpendicular,
                    spec.style == SceneStyle.AstralCitadel ? 22f : 15f,
                    null,
                    parent,
                    false);

                Vector3 torchOffset = perpendicular * Mathf.Min(25f, RegionSizes[regionIndex].x * 0.28f)
                                      - tangent * Mathf.Min(20f, RegionSizes[regionIndex].y * 0.24f);
                PlaceDecorativePrefab(
                    torchPath,
                    $"{spec.regionNames[regionIndex]}_引导灯_左",
                    center - torchOffset,
                    tangent,
                    4f,
                    null,
                    parent,
                    true);
                PlaceDecorativePrefab(
                    torchPath,
                    $"{spec.regionNames[regionIndex]}_引导灯_右",
                    center + torchOffset,
                    tangent,
                    4f,
                    null,
                    parent,
                    true);
            }
        }

        private static GameObject PlaceDecorativePrefab(
            string prefabPath,
            string name,
            Vector3 groundPosition,
            Vector3 forward,
            float targetFootprint,
            Material overrideMaterial,
            Transform parent,
            bool keepLights)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"缺少场景装饰预制体：{prefabPath}");

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, _targetScene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"场景装饰预制体创建失败：{prefabPath}");

            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(
                groundPosition,
                Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward, Vector3.up));

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
            NavMeshObstacle[] obstacles = instance.GetComponentsInChildren<NavMeshObstacle>(true);
            for (int i = 0; i < obstacles.Length; i++)
                obstacles[i].enabled = false;
            if (!keepLights)
            {
                Light[] lights = instance.GetComponentsInChildren<Light>(true);
                for (int i = 0; i < lights.Length; i++)
                    lights[i].enabled = false;
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (TryCalculateRendererBounds(renderers, out Bounds initialBounds))
            {
                float footprint = Mathf.Max(initialBounds.size.x, initialBounds.size.z);
                if (footprint > 0.01f)
                {
                    float scale = Mathf.Clamp(targetFootprint / footprint, 0.15f, 4f);
                    instance.transform.localScale *= scale;
                }
            }

            if (overrideMaterial != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null || renderers[i] is ParticleSystemRenderer)
                        continue;
                    Material[] materials = new Material[renderers[i].sharedMaterials.Length];
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        materials[materialIndex] = overrideMaterial;
                    renderers[i].sharedMaterials = materials;
                }
            }

            if (TryCalculateRendererBounds(renderers, out Bounds scaledBounds))
                instance.transform.position += Vector3.up * (groundPosition.y - scaledBounds.min.y);
            return instance;
        }

        private static bool TryCalculateRendererBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer)
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
            return hasBounds;
        }

        private static void BuildLighting(SceneSpec spec, Transform parent, Scene scene)
        {
            GameObject sunObject = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(sunObject, scene);
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f + spec.levelIndex * 11f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = spec.sunlightColor;
            sun.intensity = spec.style == SceneStyle.VolcanicForge ? 1.65f : 1.78f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.68f;

            GameObject fillObject = new GameObject("环境补光");
            fillObject.transform.SetParent(parent, false);
            fillObject.transform.rotation = Quaternion.Euler(58f, 148f - spec.levelIndex * 7f, 0f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = Color.Lerp(spec.ambientColor, Color.white, 0.34f);
            fill.intensity = 0.56f;
            fill.shadows = LightShadows.None;

            for (int i = 0; i < spec.regionCenters.Length; i++)
            {
                GameObject lightObject = new GameObject($"区域辉光_{i + 1:00}");
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.position = spec.regionCenters[i] + Vector3.up * 10f;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = spec.accentColor;
                light.intensity = i == spec.regionCenters.Length - 1 ? 6.4f : 4.1f;
                light.range = i == spec.regionCenters.Length - 1 ? 66f : 50f;
                light.shadows = LightShadows.None;
            }
        }

        private static void CreateMainCamera(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainCameraPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"缺少主相机预制体：{MainCameraPrefabPath}");

            GameObject mainCamera = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (mainCamera == null)
                throw new InvalidOperationException("主相机预制体创建失败。");
            mainCamera.name = "Main Camera";
            UniversalAdditionalCameraData cameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                throw new InvalidOperationException("主相机预制体缺少 UniversalAdditionalCameraData。");
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            EditorUtility.SetDirty(cameraData);
        }

        private static void CreateGameFlow(SceneSpec spec, Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameFlowPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"缺少 GameFlow 预制体：{GameFlowPrefabPath}");

            GameObject gameFlow = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (gameFlow == null)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 无法创建 GameFlow。");
            gameFlow.name = "GameFlow";

            Transform spawnPoint = FindChildByName(gameFlow.transform, "SpawnPoint");
            if (spawnPoint == null)
                throw new InvalidOperationException("GameFlow 预制体缺少 SpawnPoint。");
            spawnPoint.position = spec.spawnPoint;
            spawnPoint.rotation = Quaternion.LookRotation(HorizontalDirection(spec.spawnPoint, spec.regionCenters[0]), Vector3.up);
        }

        private static void CreateFallDeathController(SceneSpec spec, Scene scene)
        {
            GameObject fallDeathObject = new GameObject("FallDeathController");
            SceneManager.MoveGameObjectToScene(fallDeathObject, scene);
            LevelFallDeathController fallDeathController = fallDeathObject.AddComponent<LevelFallDeathController>();
            fallDeathController.Configure(spec.style == SceneStyle.AstralCitadel ? -26f : -16f);
            EditorUtility.SetDirty(fallDeathController);
        }

        private static void ConfigureRegionGameplay(
            SceneSpec spec,
            Transform flowRoot,
            Transform spawnerRoot,
            List<Vector3> branchCenters)
        {
            GameObject spawnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalSpawnerPrefabPath);
            if (spawnerPrefab == null)
                throw new InvalidOperationException($"缺少局部敌人生成器预制体：{LocalSpawnerPrefabPath}");
            GameObject guardianOnePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardianOnePrefabPath);
            GameObject guardianTwoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardianTwoPrefabPath);
            if (guardianOnePrefab == null || guardianTwoPrefab == null)
                throw new InvalidOperationException("缺少场景直摆守卫者预制体。");
            Transform guardianRoot = CreateGroup(flowRoot, "直摆守卫者");

            var gates = new List<RogueliteSealGate>();
            var entryCenters = new List<Vector3> { spec.spawnPoint };
            for (int i = 0; i < spec.regionCenters.Length - 1; i++)
            {
                Vector3 direction = HorizontalDirection(spec.regionCenters[i], spec.regionCenters[i + 1]);
                Vector3 gatePosition = Vector3.Lerp(spec.regionCenters[i], spec.regionCenters[i + 1], 0.4f);
                gatePosition.y = Mathf.Lerp(spec.regionCenters[i].y, spec.regionCenters[i + 1].y, 0.4f);
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                gates.Add(CreateSealGate(flowRoot, $"区域封印门_{i + 1:00}", gatePosition, yaw));
                entryCenters.Add(gatePosition + direction * 12f + Vector3.up * 0.25f);
            }

            var regions = new List<RogueliteRegionFlowController.RegionDefinition>();
            for (int i = 0; i < spec.regionCenters.Length; i++)
            {
                bool isBossRegion = i == spec.regionCenters.Length - 1;
                var spawners = new List<LevelLocalEnemySpawner>();
                var placedGuardians = new List<RoguelitePlacedGuardian>();
                if (!isBossRegion)
                {
                    Vector3 previous = i == 0 ? spec.spawnPoint : spec.regionCenters[i - 1];
                    Vector3 next = spec.regionCenters[i + 1];
                    Vector3 tangent = HorizontalDirection(previous, next);
                    Vector3 perpendicular = new Vector3(-tangent.z, 0f, tangent.x);
                    Vector3 combatAnchor = i == 0 ? spec.regionCenters[i] - tangent * 12f : spec.regionCenters[i];
                    spawners.Add(CreateRegionSpawner(spawnerPrefab, spawnerRoot, $"区域{i + 1:00}_精英", combatAnchor + perpendicular * 14f + tangent * 7f, 3));
                    spawners.Add(CreateRegionSpawner(spawnerPrefab, spawnerRoot, $"区域{i + 1:00}_小怪_A", combatAnchor - perpendicular * 15f - tangent * 5f, 4));
                    spawners.Add(CreateRegionSpawner(spawnerPrefab, spawnerRoot, $"区域{i + 1:00}_支线小怪", branchCenters[i] - tangent * 9f, 4));
                    placedGuardians = CreatePlacedGuardians(
                        spec,
                        guardianRoot,
                        guardianOnePrefab,
                        guardianTwoPrefab,
                        i,
                        branchCenters[i],
                        tangent,
                        perpendicular);
                }

                regions.Add(new RogueliteRegionFlowController.RegionDefinition
                {
                    regionId = $"region_{i + 1:00}",
                    displayName = spec.regionNames[i],
                    guardianCenter = spec.regionCenters[i],
                    guardianRadius = isBossRegion ? 78f : 66f,
                    entryCenter = entryCenters[i],
                    entryRadius = 14f,
                    spawners = spawners,
                    placedGuardians = placedGuardians,
                    exitGate = i < gates.Count ? gates[i] : null,
                    isBossRegion = isBossRegion,
                    bossSpawnPoint = spec.regionCenters[i],
                });
            }

            RogueliteRegionFlowController flowController = flowRoot.gameObject.AddComponent<RogueliteRegionFlowController>();
            flowController.Configure(spec.flowId, regions);
            EditorUtility.SetDirty(flowController);

            RogueliteMinimapController minimapController = flowRoot.gameObject.AddComponent<RogueliteMinimapController>();
            EditorUtility.SetDirty(minimapController);
        }

        private static LevelLocalEnemySpawner CreateRegionSpawner(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 position,
            int planIndex)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, _targetScene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"局部生成器创建失败：{name}");

            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            LevelLocalEnemySpawner spawner = instance.GetComponent<LevelLocalEnemySpawner>();
            if (spawner == null)
                throw new InvalidOperationException($"局部生成器预制体缺少组件：{name}");

            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("_planIndex").intValue = planIndex;
            serializedSpawner.FindProperty("_waitForActivation").boolValue = true;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            spawner.enabled = true;
            EditorUtility.SetDirty(spawner);
            return spawner;
        }

        private static List<RoguelitePlacedGuardian> CreatePlacedGuardians(
            SceneSpec spec,
            Transform parent,
            GameObject guardianOnePrefab,
            GameObject guardianTwoPrefab,
            int regionIndex,
            Vector3 branchCenter,
            Vector3 tangent,
            Vector3 perpendicular)
        {
            var result = new List<RoguelitePlacedGuardian>();
            Vector3[] offsets =
            {
                perpendicular * -11f + tangent * 5f,
                perpendicular * 10f - tangent * 4f,
            };
            GameObject[] prefabs = { guardianOnePrefab, guardianTwoPrefab };
            string regionId = $"region_{regionIndex + 1:00}";
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 requestedPosition = branchCenter + offsets[i] + Vector3.up * 2f;
                if (!NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                    throw new InvalidOperationException($"Level_{spec.levelIndex} 区域 {regionIndex + 1} 的直摆守卫者位置不在导航地面。");

                GameObject guardianObject = PrefabUtility.InstantiatePrefab(prefabs[i], _targetScene) as GameObject;
                if (guardianObject == null)
                    throw new InvalidOperationException($"Level_{spec.levelIndex} 区域 {regionIndex + 1} 的直摆守卫者创建失败。");

                guardianObject.name = $"区域{regionIndex + 1:00}_支线守卫者_{i + 1:00}";
                guardianObject.transform.SetParent(parent, true);
                guardianObject.transform.SetPositionAndRotation(
                    hit.position,
                    Quaternion.LookRotation(HorizontalDirection(hit.position, branchCenter), Vector3.up));
                RoguelitePlacedGuardian marker = guardianObject.GetComponent<RoguelitePlacedGuardian>();
                if (marker == null)
                    marker = guardianObject.AddComponent<RoguelitePlacedGuardian>();
                marker.Configure($"level_{spec.levelIndex}_{regionId}_guardian_{i + 1:00}", regionId);
                EditorUtility.SetDirty(marker);
                guardianObject.SetActive(false);
                result.Add(marker);
            }

            return result;
        }

        private static RogueliteSealGate CreateSealGate(Transform parent, string name, Vector3 position, float yaw)
        {
            GameObject gateObject = new GameObject(name);
            gateObject.transform.SetParent(parent, false);
            gateObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            BoxCollider blocker = gateObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, 4f, 0f);
            blocker.size = new Vector3(30f, 8f, 2.2f);
            NavMeshModifier modifier = gateObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;

            Transform visualRoot = CreateGroup(gateObject.transform, "封印光影视觉");
            var renderers = new List<Renderer>
            {
                CreateEnergyQuad(visualRoot, "浅蓝光幕_A", new Vector3(0f, 4f, -0.08f), new Vector2(29f, 8f), 0f),
                CreateEnergyQuad(visualRoot, "浅蓝光幕_B", new Vector3(0f, 4f, 0.08f), new Vector2(28f, 7.5f), 180f),
            };

            Color sealColor = new Color(0.3f, 0.82f, 1f, 0.2f);
            var lights = new List<Light>();
            for (int i = -1; i <= 1; i++)
            {
                GameObject lightObject = new GameObject($"光门辉光_{i + 2:00}");
                lightObject.transform.SetParent(visualRoot, false);
                lightObject.transform.localPosition = new Vector3(i * 11f, 4f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = sealColor;
                light.intensity = i == 0 ? 1.4f : 1.9f;
                light.range = 12f;
                light.shadows = LightShadows.None;
                lights.Add(light);
            }

            var particles = new[]
            {
                CreateSealParticles(visualRoot, "光门薄雾", sealColor, false),
                CreateSealParticles(visualRoot, "光门星屑", sealColor, true),
            };

            CreateLocalBox("光门左柱", new Vector3(-16f, 4f, 0f), new Vector3(2.4f, 9f, 3f), _wallMaterial, gateObject.transform, true);
            CreateLocalBox("光门右柱", new Vector3(16f, 4f, 0f), new Vector3(2.4f, 9f, 3f), _wallMaterial, gateObject.transform, true);
            CreateLocalBox("光门横梁", new Vector3(0f, 8.7f, 0f), new Vector3(34.4f, 1.4f, 3f), _wallMaterial, gateObject.transform, true);

            RogueliteSealGate gate = gateObject.AddComponent<RogueliteSealGate>();
            gate.Configure(blocker, visualRoot.gameObject, renderers.ToArray(), lights.ToArray(), particles, sealColor);
            EditorUtility.SetDirty(gate);
            return gate;
        }

        private static Renderer CreateEnergyQuad(Transform parent, string name, Vector3 localPosition, Vector2 size, float roll)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = _sealMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static ParticleSystem CreateSealParticles(Transform parent, string name, Color color, bool sparks)
        {
            GameObject particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, 4f, 0f);
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.duration = 2.4f;
            main.startLifetime = sparks ? new ParticleSystem.MinMaxCurve(0.7f, 1.3f) : new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = sparks ? new ParticleSystem.MinMaxCurve(0.3f, 1f) : new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            main.startSize = sparks ? new ParticleSystem.MinMaxCurve(0.05f, 0.13f) : new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            Color particleColor = color;
            particleColor.a = sparks ? 0.58f : 0.14f;
            main.startColor = particleColor;
            main.maxParticles = sparks ? 150 : 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = sparks ? 20f : 11f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(27f, 7f, 0.9f);
            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _sealMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static void PlaceChest(Transform parent, string name, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
            if (prefab == null)
                return;
            GameObject chest = PrefabUtility.InstantiatePrefab(prefab, _targetScene) as GameObject;
            if (chest == null)
                return;
            chest.name = name;
            chest.transform.SetParent(parent, true);
            chest.transform.position = position + Vector3.up * 0.15f;
            ChestInteractable.EnsureOn(chest);
        }

        private static void PlaceCampaignShops(SceneSpec spec, Transform parent, List<Vector3> branchCenters)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"缺少商店预制体：{ShopPrefabPath}");

            int regionIndex = spec.regionCenters.Length - 2;
            Vector3 previous = spec.regionCenters[regionIndex - 1];
            Vector3 next = spec.regionCenters[regionIndex + 1];
            Vector3 tangent = HorizontalDirection(previous, next);
            Vector3 position = branchCenters[regionIndex] + tangent * 11f + Vector3.up * 0.15f;

            GameObject shop = PrefabUtility.InstantiatePrefab(prefab, _targetScene) as GameObject;
            if (shop == null)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 商店创建失败。");

            shop.name = "Boss战前补给商店";
            shop.transform.SetParent(parent, true);
            Quaternion importedAxisCorrection = prefab.transform.localRotation;
            shop.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(-tangent, Vector3.up) * importedAxisCorrection);
            if (shop.GetComponentInChildren<HutaoShopInteractable>(true) == null)
                HutaoShopInteractable.EnsureOn(shop);
        }

        private static void BuildNavigation(SceneSpec spec, Scene scene, string sceneFolder)
        {
            GameObject navigation = new GameObject("Navigation");
            SceneManager.MoveGameObjectToScene(navigation, scene);
            NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.overrideTileSize = true;
            surface.tileSize = 256;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.22f;
            surface.minRegionArea = 2f;

            string navMeshPath = $"{sceneFolder}/NavMesh-Navigation.asset";
            NavMeshData persistedData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navMeshPath);
            surface.BuildNavMesh();
            NavMeshData builtData = surface.navMeshData;
            if (builtData == null)
                throw new InvalidOperationException($"Level_{spec.levelIndex} NavMesh 烘焙失败。");

            if (persistedData == null)
            {
                AssetDatabase.CreateAsset(builtData, navMeshPath);
            }
            else if (builtData != persistedData)
            {
                surface.RemoveData();
                EditorUtility.CopySerialized(builtData, persistedData);
                persistedData.name = surface.name;
                EditorUtility.SetDirty(persistedData);
                surface.navMeshData = persistedData;
                if (surface.isActiveAndEnabled)
                    surface.AddData();
                UnityEngine.Object.DestroyImmediate(builtData);
            }

            EditorUtility.SetDirty(surface);
        }

        private static void ValidateNavigation(SceneSpec spec, List<Vector3> branchCenters)
        {
            var points = new List<Vector3> { spec.spawnPoint };
            points.AddRange(spec.regionCenters);
            points.AddRange(branchCenters);
            var sampled = new List<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                if (!NavMesh.SamplePosition(points[i], out NavMeshHit hit, 7f, NavMesh.AllAreas))
                    throw new InvalidOperationException($"Level_{spec.levelIndex} 导航验证失败：点位 {i} 没有 NavMesh。");
                sampled.Add(hit.position);
            }

            Vector2 spawnOffset = new Vector2(sampled[0].x - spec.spawnPoint.x, sampled[0].z - spec.spawnPoint.z);
            if (spawnOffset.magnitude > 1.5f || Mathf.Abs(sampled[0].y - spec.spawnPoint.y) > 0.75f)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 出生点未贴合首区地面：配置={spec.spawnPoint.ToString("F2")}，导航={sampled[0].ToString("F2")}。");

            for (int i = 1; i <= spec.regionCenters.Length; i++)
            {
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(sampled[i - 1], sampled[i], NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    var probeResults = new List<string>();
                    for (int probeIndex = 1; probeIndex <= 12; probeIndex++)
                    {
                        float t = probeIndex / 12f;
                        Vector3 probePoint = Vector3.Lerp(points[i - 1], points[i], t);
                        if (!NavMesh.SamplePosition(probePoint, out NavMeshHit probeHit, 4f, NavMesh.AllAreas))
                        {
                            probeResults.Add($"{probeIndex}:无网格");
                            continue;
                        }

                        var probePath = new NavMeshPath();
                        NavMesh.CalculatePath(sampled[i - 1], probeHit.position, NavMesh.AllAreas, probePath);
                        probeResults.Add($"{probeIndex}:{probeHit.position.ToString("F1")}/{probePath.status}");
                    }

                    throw new InvalidOperationException(
                        $"Level_{spec.levelIndex} 导航验证失败：主路线 {i - 1} → {i} 不可达。" +
                        $"起点={sampled[i - 1].ToString("F2")}，终点={sampled[i].ToString("F2")}，" +
                        $"探针={string.Join(" | ", probeResults)}");
                }
            }


            int branchStartIndex = 1 + spec.regionCenters.Length;
            for (int i = 0; i < branchCenters.Count; i++)
            {
                var branchPath = new NavMeshPath();
                Vector3 regionPoint = sampled[i + 1];
                Vector3 branchPoint = sampled[branchStartIndex + i];
                if (!NavMesh.CalculatePath(regionPoint, branchPoint, NavMesh.AllAreas, branchPath) || branchPath.status != NavMeshPathStatus.PathComplete)
                {
                    throw new InvalidOperationException(
                        $"Level_{spec.levelIndex} 导航验证失败：区域 {i + 1} 无法进入支线房间。" +
                        $"起点={regionPoint.ToString("F2")}，终点={branchPoint.ToString("F2")}。");
                }
            }
        }

        private static void ValidateSceneComposition(SceneSpec spec, Scene scene, GameObject environmentRoot)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            bool hasMainCamera = false;
            bool hasGameFlow = false;
            bool hasSpawnAreas = false;
            bool hasNavigation = false;
            for (int i = 0; i < roots.Length; i++)
            {
                hasMainCamera |= roots[i].name == "Main Camera";
                hasGameFlow |= roots[i].name == "GameFlow";
                hasSpawnAreas |= roots[i].name == "SpawnAreas";
                hasNavigation |= roots[i].name == "Navigation";
            }

            if (!hasMainCamera || !hasGameFlow || !hasSpawnAreas || !hasNavigation)
            {
                throw new InvalidOperationException(
                    $"Level_{spec.levelIndex} 场景骨架不完整：" +
                    $"MainCamera={hasMainCamera}，GameFlow={hasGameFlow}，SpawnAreas={hasSpawnAreas}，Navigation={hasNavigation}。");
            }

            UniversalAdditionalCameraData[] cameraData = UnityEngine.Object.FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cameraData.Length != 1 || cameraData[0].requiresDepthOption != CameraOverrideOption.On)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 主相机深度纹理配置不正确。");

            RogueliteSealGate[] gates = UnityEngine.Object.FindObjectsByType<RogueliteSealGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (gates.Length != spec.regionCenters.Length - 1)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 光门数量错误：{gates.Length}。");

            RogueliteRegionFlowController[] flows = UnityEngine.Object.FindObjectsByType<RogueliteRegionFlowController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RogueliteMinimapController[] minimaps = UnityEngine.Object.FindObjectsByType<RogueliteMinimapController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            LevelFallDeathController[] fallControllers = UnityEngine.Object.FindObjectsByType<LevelFallDeathController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (flows.Length != 1 || minimaps.Length != 1 || fallControllers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Level_{spec.levelIndex} 关卡系统数量错误：区域流程={flows.Length}，小地图={minimaps.Length}，坠落死亡={fallControllers.Length}。");
            }

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int directionalLightCount = 0;
            int pointLightCount = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                    directionalLightCount++;
                else if (lights[i].type == LightType.Point)
                    pointLightCount++;
            }
            if (directionalLightCount < 2 || pointLightCount < spec.regionCenters.Length)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 灯光配置不足：平行光={directionalLightCount}，点光源={pointLightCount}。");

            LevelLocalEnemySpawner[] spawners = UnityEngine.Object.FindObjectsByType<LevelLocalEnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int expectedSpawnerCount = (spec.regionCenters.Length - 1) * 3;
            if (spawners.Length != expectedSpawnerCount)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 敌人生成器数量错误：{spawners.Length}，预期={expectedSpawnerCount}。");
            for (int i = 0; i < spawners.Length; i++)
            {
                if (!NavMesh.SamplePosition(spawners[i].transform.position, out _, 6f, NavMesh.AllAreas))
                    throw new InvalidOperationException($"Level_{spec.levelIndex} 敌人生成点未落在导航地面：{spawners[i].name}。");
            }

            RoguelitePlacedGuardian[] placedGuardians = UnityEngine.Object.FindObjectsByType<RoguelitePlacedGuardian>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int expectedGuardianCount = (spec.regionCenters.Length - 1) * 2;
            if (placedGuardians.Length != expectedGuardianCount)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 直摆守卫者数量错误：{placedGuardians.Length}，预期={expectedGuardianCount}。");
            var guardianIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < placedGuardians.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(placedGuardians[i].PlacementId) || !guardianIds.Add(placedGuardians[i].PlacementId))
                    throw new InvalidOperationException($"Level_{spec.levelIndex} 直摆守卫者 ID 为空或重复：{placedGuardians[i].name}。");
                if (placedGuardians[i].gameObject.activeSelf)
                    throw new InvalidOperationException($"Level_{spec.levelIndex} 直摆守卫者必须等待区域激活：{placedGuardians[i].name}。");
                if (!NavMesh.SamplePosition(placedGuardians[i].transform.position, out _, 3f, NavMesh.AllAreas))
                    throw new InvalidOperationException($"Level_{spec.levelIndex} 直摆守卫者未落在导航地面：{placedGuardians[i].name}。");
            }

            HutaoShopInteractable[] shops = UnityEngine.Object.FindObjectsByType<HutaoShopInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (shops.Length != 1)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 商店数量错误：{shops.Length}，预期=1。");

            Transform[] environmentTransforms = environmentRoot.GetComponentsInChildren<Transform>(true);
            int arenaWallGroupCount = 0;
            int corridorWallCount = 0;
            for (int i = 0; i < environmentTransforms.Length; i++)
            {
                if (environmentTransforms[i].name.Contains("_区域高墙", StringComparison.Ordinal))
                    arenaWallGroupCount++;
                if (environmentTransforms[i].name.Contains("连续高墙", StringComparison.Ordinal))
                    corridorWallCount++;
            }
            if (arenaWallGroupCount != spec.regionCenters.Length || corridorWallCount < 20)
                throw new InvalidOperationException($"Level_{spec.levelIndex} 区域隔离不完整：区域高墙={arenaWallGroupCount}，通道高墙={corridorWallCount}。");

            Renderer[] renderers = environmentRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer)
                    continue;
                Material[] materials = renderers[i].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (materials[materialIndex] == null)
                        throw new InvalidOperationException($"Level_{spec.levelIndex} 存在未配置材质的场景物体：{renderers[i].name}。");
                }
            }
        }

        private static GameObject CreateBox(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent,
            bool keepCollider,
            bool navigationStatic,
            Quaternion? rotation = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
                UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            SetStaticFlags(box, navigationStatic);
            return box;
        }

        private static GameObject CreateLocalBox(
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Transform parent,
            bool keepCollider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = localScale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
                UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            SetStaticFlags(box, keepCollider);
            return box;
        }

        private static GameObject CreateCylinder(
            string name,
            Vector3 position,
            float diameter,
            float height,
            Material material,
            Transform parent,
            bool keepCollider)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.position = position;
            cylinder.transform.localScale = new Vector3(diameter * 0.5f, height * 0.5f, diameter * 0.5f);
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
                UnityEngine.Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            SetStaticFlags(cylinder, keepCollider);
            return cylinder;
        }

        private static GameObject CreateDecorativePrimitive(
            PrimitiveType primitiveType,
            string name,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Material material,
            Transform parent)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.SetPositionAndRotation(position, rotation);
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            SetStaticFlags(gameObject, false);
            return gameObject;
        }

        private static GameObject CreateMeshDecoration(
            string name,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Mesh mesh,
            Material material,
            Transform parent)
        {
            if (mesh == null)
                throw new InvalidOperationException($"程序模型缺失：{name}");

            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.SetPositionAndRotation(position, rotation);
            gameObject.transform.localScale = scale;
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            SetStaticFlags(gameObject, false);
            return gameObject;
        }

        private static GameObject CreateFogQuad(
            string name,
            Vector3 position,
            Vector2 size,
            Quaternion rotation,
            Transform parent)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(position, rotation);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = _voidFogMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetStaticFlags(quad, false);
            return quad;
        }

        private static void SetStaticFlags(GameObject gameObject, bool navigationStatic)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
            if (navigationStatic)
                flags |= StaticEditorFlags.NavigationStatic;
            GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
        }

        private static Transform CreateSceneRoot(Scene scene, string name)
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (string.Equals(children[i].name, childName, StringComparison.Ordinal))
                    return children[i];
            }
            return null;
        }

        private static Vector3 HorizontalDirection(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        private static Vector3 GetArenaBoundaryPoint(Vector3 center, Vector2 size, Vector3 outwardDirection)
        {
            Vector3 direction = outwardDirection;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;

            float xDistance = Mathf.Abs(direction.x) > 0.001f
                ? size.x * 0.5f / Mathf.Abs(direction.x)
                : float.PositiveInfinity;
            float zDistance = Mathf.Abs(direction.z) > 0.001f
                ? size.y * 0.5f / Mathf.Abs(direction.z)
                : float.PositiveInfinity;
            float boundaryDistance = Mathf.Max(0f, Mathf.Min(xDistance, zDistance) - 1.5f);
            return center + direction * boundaryDistance;
        }

        private static string GetScenePath(int levelIndex)
        {
            return $"Assets/Scenes/Level_{levelIndex}.unity";
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;
            string parentPath = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parentPath) || !AssetDatabase.IsValidFolder(parentPath))
                throw new InvalidOperationException($"无法创建场景资源目录：{folderPath}");
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static void RenderScenePreviews(SceneSpec spec)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return;

            string previewFolder = Path.Combine(projectRoot, "Temp", "CampaignScenePreviews");
            Directory.CreateDirectory(previewFolder);
            RenderScenePreview(
                Path.Combine(previewFolder, $"Level_{spec.levelIndex}_Layout.png"),
                new Vector3(0f, 1050f, 420f),
                Quaternion.Euler(90f, 0f, 0f),
                true,
                475f,
                768,
                1280,
                false);

            Vector3 entranceCameraPosition = spec.spawnPoint + new Vector3(0f, 7f, -8f);
            Vector3 entranceTarget = spec.spawnPoint + HorizontalDirection(spec.spawnPoint, spec.regionCenters[0]) * 20f + Vector3.up * 3f;
            RenderScenePreview(
                Path.Combine(previewFolder, $"Level_{spec.levelIndex}_Entrance.png"),
                entranceCameraPosition,
                Quaternion.LookRotation(entranceTarget - entranceCameraPosition, Vector3.up),
                false,
                60f,
                1280,
                720,
                true);
        }

        private static void RenderScenePreview(
            string path,
            Vector3 position,
            Quaternion rotation,
            bool orthographic,
            float sizeOrFieldOfView,
            int width,
            int height,
            bool keepSceneFog)
        {
            GameObject cameraObject = new GameObject("CampaignScene_PreviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            Camera camera = cameraObject.AddComponent<Camera>();
            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.orthographic = orthographic;
            if (orthographic)
                camera.orthographicSize = sizeOrFieldOfView;
            else
                camera.fieldOfView = sizeOrFieldOfView;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.Lerp(RenderSettings.fogColor, Color.black, 0.22f);

            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            bool previousFog = RenderSettings.fog;
            try
            {
                if (!keepSceneFog)
                    RenderSettings.fog = false;
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                RenderSettings.fog = previousFog;
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void SaveSceneAsText(Scene scene)
        {
            SerializationMode previousMode = EditorSettings.serializationMode;
            try
            {
                EditorSettings.serializationMode = SerializationMode.ForceText;
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSettings.serializationMode = previousMode;
            }
        }
    }
}
