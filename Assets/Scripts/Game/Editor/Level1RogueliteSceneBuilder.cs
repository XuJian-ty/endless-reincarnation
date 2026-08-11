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
    public static class Level1RogueliteSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Level_1.unity";
        private const string NavMeshAssetPath = "Assets/Scenes/Level_1/NavMesh-Navigation.asset";
        private const string SealMaterialPath = "Assets/Scenes/Level_1/RegionSeal.mat";
        private const string StoneMaterialPath = "Assets/Scenes/Level_1/RogueliteStoneFloor.mat";
        private const string ArchitectureMaterialPath = "Assets/Scenes/Level_1/RogueliteTempleWall.mat";
        private const string RouteMaterialPath = "Assets/Scenes/Level_1/RogueliteRouteAccent.mat";
        private const string VoidFogMaterialPath = "Assets/Scenes/Level_1/CliffVoidFog.mat";
        private const string EnvironmentRootName = "Level_1_Environment";
        private const string BuildVersionMarkerName = "_CAMPAIGN_BUILD_20260811_V6";

        private const string SoilMaterialPath = "Assets/Eternal Temple/Source_FBX/Materials/Soil_Material_01.mat";
        private const string SkyboxMaterialPath = "Assets/Eternal Temple/Source_FBX/Materials/ET_SkyBox_01.mat";
        private const string FloorTexturePath = "Assets/Eternal Temple/Textures/Temple_Floor_Indoor_01_diffuse.png";
        private const string FloorNormalTexturePath = "Assets/Eternal Temple/Textures/Temple_Floor_Indoor_01_normal.png";
        private const string WallTexturePath = "Assets/Eternal Temple/Textures/Atlas/Atlas_Architecture_01_Diffuse.png";
        private const string WallNormalTexturePath = "Assets/Eternal Temple/Textures/Atlas/Atlas_Architecture_01_Normal.png";

        private const string ArchPrefabPath = "Assets/Eternal Temple/Prefabs/Bases/Arch_Wide_01.prefab";
        private const string ColumnPrefabPath = "Assets/Eternal Temple/Prefabs/Columns/Column_05.prefab";
        private const string BrokenColumnPrefabPath = "Assets/Eternal Temple/Prefabs/Damaged/Column_Broken_02.prefab";
        private const string TorchPrefabPath = "Assets/Eternal Temple/Prefabs/Lanterns/Torch_01.prefab";
        private const string TreePrefabPath = "Assets/Eternal Temple/Prefabs/Flora/Tree_01.prefab";
        private const string AltarPrefabPath = "Assets/Eternal Temple/Prefabs/Props/Furniture/Stone_Altar_03.prefab";
        private const string ChestPrefabPath = "Assets/Eternal Temple/Prefabs/Props/Chest_01.prefab";
        private const string PortalPrefabPath = "Assets/Eternal Temple/Prefabs/Bases/Base_Portal_01.prefab";
        private const string ShopPrefabPath = "Assets/Resources/Prefabs/商店.prefab";
        private const string GuardianOnePrefabPath = "Assets/Resources/Prefabs/guardian_1.prefab";
        private const string GuardianTwoPrefabPath = "Assets/Resources/Prefabs/guardian_2.prefab";

        private static Material _stoneMaterial;
        private static Material _architectureMaterial;
        private static Material _soilMaterial;
        private static Material _routeMaterial;
        private static Material _sealMaterial;
        private static Material _voidFogMaterial;
        private static Scene _targetScene;

        [Flags]
        private enum ArenaGates
        {
            None = 0,
            North = 1 << 0,
            South = 1 << 1,
            East = 1 << 2,
            West = 1 << 3,
        }

        private static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildLevel1(true);
        }

        public static void RebuildForProjectUpdate()
        {
            BuildLevel1(true);
        }

        private static void BuildLevel1(bool forceRebuild)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject existingRoot = FindRoot(scene, EnvironmentRootName);
            if (existingRoot != null && !forceRebuild)
            {
                if (existingRoot.transform.Find(BuildVersionMarkerName) == null)
                {
                    forceRebuild = true;
                }
                else
                {
                    DisableDecorationLights(existingRoot);
                    BuildNavigation(scene);
                    ValidateNavigation();
                    SaveSceneAsText(scene);
                    AssetDatabase.SaveAssets();
                    string existingPreviewPath = RenderPreview(existingRoot);
                    LogBuildSummary(existingRoot, existingPreviewPath);
                    return;
                }
            }

            _targetScene = scene;
            LoadSharedAssets();

            if (existingRoot != null)
                UnityEngine.Object.DestroyImmediate(existingRoot);

            RemovePlaceholderPlane(scene);
            ConfigureEnvironment(scene);
            ConfigureMainCameraDepth(scene);

            GameObject environmentRoot = new GameObject(EnvironmentRootName);
            SceneManager.MoveGameObjectToScene(environmentRoot, scene);
            CreateGroup(environmentRoot.transform, BuildVersionMarkerName);

            Transform groundRoot = CreateGroup(environmentRoot.transform, "00_地形");
            Transform routeRoot = CreateGroup(environmentRoot.transform, "01_路线");
            Transform arenaRoot = CreateGroup(environmentRoot.transform, "02_战斗区域");
            Transform ruinRoot = CreateGroup(environmentRoot.transform, "03_圣殿遗迹");
            Transform propRoot = CreateGroup(environmentRoot.transform, "04_环境装饰");
            Transform lightRoot = CreateGroup(environmentRoot.transform, "05_灯光");
            Transform landmarkRoot = CreateGroup(environmentRoot.transform, "06_地标");
            Transform flowRoot = CreateGroup(environmentRoot.transform, "07_区域流程");

            BuildBackdrop(groundRoot);
            BuildCliffFog(groundRoot);
            BuildPlayableLayout(arenaRoot, routeRoot);
            BuildTempleRuins(ruinRoot, landmarkRoot);
            BuildEnvironmentProps(propRoot);
            BuildLighting(lightRoot);
            DisableDecorationLights(environmentRoot);
            ConfigureRegionGameplay(flowRoot);
            ConfigureSpawnPoint(scene);

            BuildNavigation(scene);
            ValidateNavigation();
            EditorSceneManager.MarkSceneDirty(scene);
            SaveSceneAsText(scene);
            AssetDatabase.SaveAssets();

            string previewPath = RenderPreview(environmentRoot);
            LogBuildSummary(environmentRoot, previewPath);
            Selection.activeGameObject = environmentRoot;
            EditorGUIUtility.PingObject(environmentRoot);
        }

        private static void LoadSharedAssets()
        {
            _stoneMaterial = LoadOrCreateEnvironmentMaterial(
                StoneMaterialPath,
                FloorTexturePath,
                FloorNormalTexturePath,
                new Color(0.46f, 0.49f, 0.52f, 1f),
                new Vector2(6f, 6f),
                Color.black);
            _architectureMaterial = LoadOrCreateEnvironmentMaterial(
                ArchitectureMaterialPath,
                WallTexturePath,
                WallNormalTexturePath,
                new Color(0.32f, 0.36f, 0.4f, 1f),
                new Vector2(2f, 2f),
                Color.black);
            _soilMaterial = AssetDatabase.LoadAssetAtPath<Material>(SoilMaterialPath);
            _routeMaterial = LoadOrCreateEnvironmentMaterial(
                RouteMaterialPath,
                FloorTexturePath,
                FloorNormalTexturePath,
                new Color(0.42f, 0.3f, 0.12f, 1f),
                new Vector2(3f, 10f),
                new Color(0.18f, 0.09f, 0.015f, 1f));
            _sealMaterial = LoadOrCreateSealMaterial();
            _voidFogMaterial = LoadOrCreateVoidFogMaterial();

            if (_stoneMaterial == null || _architectureMaterial == null || _soilMaterial == null || _routeMaterial == null || _sealMaterial == null || _voidFogMaterial == null)
                throw new InvalidOperationException("Level_1 搭建所需的 Eternal Temple 材质缺失。");
        }

        private static Material LoadOrCreateEnvironmentMaterial(
            string materialPath,
            string baseTexturePath,
            string normalTexturePath,
            Color baseColor,
            Vector2 textureScale,
            Color emissionColor)
        {
            Shader shader = Shader.Find("Game/Environment/RogueliteSurface");
            if (shader == null)
                throw new InvalidOperationException("未找到 Level_1 场景表面 Shader：Game/Environment/RogueliteSurface");

            Texture2D baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexturePath);
            Texture2D normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexturePath);
            if (baseTexture == null || normalTexture == null)
                throw new InvalidOperationException($"Level_1 场景材质贴图缺失：{baseTexturePath}");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool createAsset = material == null;
            if (createAsset)
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(materialPath) };
            else
                material.shader = shader;

            material.SetTexture("_BaseMap", baseTexture);
            material.SetTexture("_DetailMap", normalTexture);
            material.SetTextureScale("_BaseMap", textureScale);
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_SecondaryColor", Color.Lerp(baseColor, new Color(0.09f, 0.11f, 0.13f, 1f), 0.48f));
            material.SetFloat("_DetailScale", Mathf.Max(textureScale.x, textureScale.y));
            material.SetFloat("_PatternStrength", 0.46f);
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_NormalStrength", 0.38f);
            material.SetFloat("_RoughnessVariation", 0.34f);
            material.SetFloat("_MacroScale", 0.1f);
            material.SetFloat("_OcclusionStrength", 0.42f);
            material.SetColor("_EmissionColor", emissionColor);
            material.enableInstancing = true;
            material.renderQueue = -1;

            if (createAsset)
                AssetDatabase.CreateAsset(material, materialPath);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateSealMaterial()
        {
            Shader shader = Shader.Find("Game/VFX/RogueliteSealGate");
            if (shader == null)
                throw new InvalidOperationException("未找到区域封印门 Shader：Game/VFX/RogueliteSealGate");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(SealMaterialPath);
            bool createAsset = material == null;
            if (createAsset)
            {
                material = new Material(shader) { name = "RegionSeal" };
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", new Color(0.32f, 0.82f, 1f, 0.18f));
            material.SetColor("_EmissionColor", new Color(0.42f, 0.94f, 1.12f, 1f));
            material.SetFloat("_Intensity", 0.7f);
            material.SetFloat("_FlowSpeed", 0.58f);
            material.SetFloat("_NoiseScale", 4.8f);
            material.SetFloat("_EdgeSoftness", 0.2f);
            material.SetFloat("_DepthSoftness", 1.25f);
            material.SetFloat("_DistanceFadeStart", 58f);
            material.SetFloat("_DistanceFadeEnd", 175f);
            material.renderQueue = (int)RenderQueue.Transparent + 20;

            if (createAsset)
                AssetDatabase.CreateAsset(material, SealMaterialPath);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateVoidFogMaterial()
        {
            Shader shader = Shader.Find("Game/VFX/CliffVoidFog");
            if (shader == null)
                throw new InvalidOperationException("未找到悬崖雾幕 Shader：Game/VFX/CliffVoidFog");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(VoidFogMaterialPath);
            bool createAsset = material == null;
            if (createAsset)
                material = new Material(shader) { name = "CliffVoidFog" };
            else
                material.shader = shader;

            material.SetColor("_BaseColor", new Color(0.035f, 0.075f, 0.11f, 0.76f));
            material.SetColor("_GlowColor", new Color(0.16f, 0.44f, 0.58f, 1f));
            material.SetFloat("_Density", 0.96f);
            material.SetFloat("_FlowSpeed", 0.14f);
            material.SetFloat("_NoiseScale", 3.8f);
            material.SetFloat("_EdgeFade", 0.1f);
            material.SetFloat("_DepthSoftness", 2.8f);

            if (createAsset)
                AssetDatabase.CreateAsset(material, VoidFogMaterialPath);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureEnvironment(Scene scene)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.075f, 0.105f, 0.14f, 1f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 980f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.18f, 0.24f, 0.32f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.13f, 0.16f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.04f, 0.05f, 1f);
            RenderSettings.ambientIntensity = 0.85f;
            RenderSettings.reflectionIntensity = 0.7f;

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (skybox != null)
                RenderSettings.skybox = skybox;

            GameObject directionalLightObject = FindRoot(scene, "Directional Light");
            if (directionalLightObject == null)
                return;

            directionalLightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            Light directionalLight = directionalLightObject.GetComponent<Light>();
            if (directionalLight != null)
            {
                directionalLight.color = new Color(0.82f, 0.88f, 1f, 1f);
                directionalLight.intensity = 1.15f;
                directionalLight.shadows = LightShadows.Soft;
            }
        }

        private static void ConfigureSpawnPoint(Scene scene)
        {
            GameObject gameFlow = FindRoot(scene, "GameFlow");
            if (gameFlow == null)
                throw new InvalidOperationException("Level_1 中缺少 GameFlow 根对象。");

            Transform spawnPoint = null;
            Transform[] transforms = gameFlow.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, "SpawnPoint", StringComparison.Ordinal))
                {
                    spawnPoint = transforms[i];
                    break;
                }
            }

            if (spawnPoint == null)
                throw new InvalidOperationException("Level_1 的 GameFlow 中缺少 SpawnPoint。");

            spawnPoint.SetPositionAndRotation(new Vector3(0f, 0f, 70f), Quaternion.LookRotation(Vector3.forward, Vector3.up));
            EditorUtility.SetDirty(spawnPoint);
        }

        private static void ConfigureMainCameraDepth(Scene scene)
        {
            GameObject mainCameraObject = FindRoot(scene, "Main Camera");
            if (mainCameraObject == null)
                throw new InvalidOperationException("Level_1 中缺少 Main Camera 根对象。");

            UniversalAdditionalCameraData cameraData = mainCameraObject.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                throw new InvalidOperationException("Level_1 主相机缺少 UniversalAdditionalCameraData。");

            cameraData.requiresDepthOption = CameraOverrideOption.On;
            EditorUtility.SetDirty(cameraData);
        }

        private static void RemovePlaceholderPlane(Scene scene)
        {
            GameObject plane = FindRoot(scene, "Plane");
            if (plane != null)
                UnityEngine.Object.DestroyImmediate(plane);
        }

        private static void BuildBackdrop(Transform parent)
        {
            GameObject lowerGround = CreateBox(
                "遗迹深谷底面",
                new Vector3(0f, -14f, 390f),
                new Vector3(340f, 8f, 930f),
                _soilMaterial,
                parent,
                false,
                false);
            lowerGround.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            CreateBox("远景北侧台地", new Vector3(0f, -5f, 855f), new Vector3(320f, 10f, 80f), _soilMaterial, parent, false, false);
            CreateBox("远景西侧台地", new Vector3(-170f, -7f, 390f), new Vector3(80f, 14f, 930f), _soilMaterial, parent, false, false);
            CreateBox("远景东侧台地", new Vector3(170f, -7f, 390f), new Vector3(80f, 14f, 930f), _soilMaterial, parent, false, false);
        }

        private static void BuildCliffFog(Transform parent)
        {
            CreateFogQuad("深谷流动雾海", new Vector3(0f, -2.8f, 390f), new Vector2(340f, 930f), Quaternion.Euler(90f, 0f, 0f), parent);
            CreateFogQuad("西侧深谷雾墙", new Vector3(-150f, 18f, 390f), new Vector2(930f, 46f), Quaternion.Euler(0f, 90f, 0f), parent);
            CreateFogQuad("东侧深谷雾墙", new Vector3(150f, 18f, 390f), new Vector2(930f, 46f), Quaternion.Euler(0f, -90f, 0f), parent);
            CreateFogQuad("南侧深谷雾墙", new Vector3(0f, 18f, -55f), new Vector2(340f, 46f), Quaternion.identity, parent);
            CreateFogQuad("北侧深谷雾墙", new Vector3(0f, 22f, 840f), new Vector2(340f, 52f), Quaternion.Euler(0f, 180f, 0f), parent);

            Vector3[] centers =
            {
                new Vector3(0f, 0f, 86f),
                new Vector3(0f, 0f, 245f),
                new Vector3(0f, 4f, 350f),
                new Vector3(0f, 4f, 500f),
                new Vector3(0f, 4f, 630f),
                new Vector3(0f, 7f, 770f),
            };
            Vector2[] sizes =
            {
                new Vector2(84f, 64f),
                new Vector2(100f, 80f),
                new Vector2(120f, 92f),
                new Vector2(110f, 84f),
                new Vector2(106f, 84f),
                new Vector2(136f, 120f),
            };
            for (int i = 0; i < centers.Length; i++)
            {
                Vector3 center = centers[i] + Vector3.down * 1.35f;
                const float stripDepth = 22f;
                CreateFogQuad($"区域{i + 1:00}_北侧断崖雾", center + Vector3.forward * (sizes[i].y * 0.5f + stripDepth * 0.48f), new Vector2(sizes[i].x + 32f, stripDepth), Quaternion.Euler(90f, 0f, 0f), parent);
                CreateFogQuad($"区域{i + 1:00}_南侧断崖雾", center + Vector3.back * (sizes[i].y * 0.5f + stripDepth * 0.48f), new Vector2(sizes[i].x + 32f, stripDepth), Quaternion.Euler(90f, 0f, 0f), parent);
                CreateFogQuad($"区域{i + 1:00}_西侧断崖雾", center + Vector3.left * (sizes[i].x * 0.5f + stripDepth * 0.48f), new Vector2(sizes[i].y + 30f, stripDepth), Quaternion.Euler(90f, 90f, 0f), parent);
                CreateFogQuad($"区域{i + 1:00}_东侧断崖雾", center + Vector3.right * (sizes[i].x * 0.5f + stripDepth * 0.48f), new Vector2(sizes[i].y + 30f, stripDepth), Quaternion.Euler(90f, 90f, 0f), parent);
            }
        }

        private static void BuildPlayableLayout(Transform arenaParent, Transform routeParent)
        {
            BuildArena(
                arenaParent,
                "出生庭院",
                new Vector3(0f, 0f, 0f),
                new Vector2(54f, 48f),
                ArenaGates.North,
                6f,
                false);

            BuildCauseway(routeParent, "风蚀长廊", new Vector3(0f, 0f, 24f), new Vector3(0f, 0f, 54f), 16f, true);

            BuildArena(
                arenaParent,
                "首战广场",
                new Vector3(0f, 0f, 86f),
                new Vector2(84f, 64f),
                ArenaGates.South | ArenaGates.North | ArenaGates.East | ArenaGates.West,
                8f,
                true);
            GameObject rearClosure = CreateBox("出生区后向封闭墙", new Vector3(0f, 4f, 53.8f), new Vector3(21.5f, 8f, 2.8f), _architectureMaterial, arenaParent, true, true);
            NavMeshModifier rearClosureModifier = rearClosure.AddComponent<NavMeshModifier>();
            rearClosureModifier.ignoreFromBuild = true;
            CreateBox("出生区封闭墙纹章", new Vector3(0f, 4.2f, 55.24f), new Vector3(10f, 4.8f, 0.12f), _routeMaterial, arenaParent, false, false);
            BuildBranchRoomPair(arenaParent, routeParent, "首战", new Vector3(0f, 0f, 86f), 42f, 78f, new Vector2(44f, 38f));

            BuildCauseway(routeParent, "分岔前桥", new Vector3(0f, 0f, 118f), new Vector3(0f, 0f, 140f), 18f, true);
            CreateBox("分岔平台", new Vector3(0f, -0.5f, 142f), new Vector3(24f, 1f, 24f), _stoneMaterial, routeParent, true, true);
            BuildCauseway(routeParent, "金色中轴主路线", new Vector3(0f, 0f, 142f), new Vector3(0f, 0f, 205f), 20f, true);

            BuildCauseway(routeParent, "西侧试炼路", new Vector3(-8f, 0f, 144f), new Vector3(-41f, 0f, 165f), 14f, true);
            BuildCauseway(routeParent, "东侧探索路", new Vector3(8f, 0f, 144f), new Vector3(41f, 0f, 165f), 14f, true);

            BuildArena(
                arenaParent,
                "西侧高压战斗区",
                new Vector3(-72f, 0f, 165f),
                new Vector2(62f, 60f),
                ArenaGates.East | ArenaGates.North,
                7f,
                true);

            BuildArena(
                arenaParent,
                "东侧探索奖励区",
                new Vector3(72f, 0f, 165f),
                new Vector2(62f, 60f),
                ArenaGates.West | ArenaGates.North,
                6f,
                false);

            BuildCauseway(routeParent, "西侧汇流路", new Vector3(-72f, 0f, 195f), new Vector3(-6f, 0f, 207f), 16f, false);
            BuildCauseway(routeParent, "东侧汇流路", new Vector3(72f, 0f, 195f), new Vector3(6f, 0f, 207f), 16f, false);

            BuildArena(
                arenaParent,
                "精英回廊",
                new Vector3(0f, 0f, 245f),
                new Vector2(100f, 80f),
                ArenaGates.South | ArenaGates.North | ArenaGates.East | ArenaGates.West,
                9f,
                true);
            BuildBranchRoomPair(arenaParent, routeParent, "回廊", new Vector3(0f, 0f, 245f), 50f, 88f, new Vector2(46f, 42f));

            BuildCauseway(routeParent, "终局升阶道", new Vector3(0f, 0f, 285f), new Vector3(0f, 4f, 304f), 22f, true);

            BuildArena(
                arenaParent,
                "星门中庭",
                new Vector3(0f, 4f, 350f),
                new Vector2(120f, 92f),
                ArenaGates.South | ArenaGates.North | ArenaGates.East | ArenaGates.West,
                11f,
                true);
            BuildBranchRoomPair(arenaParent, routeParent, "星门", new Vector3(0f, 4f, 350f), 60f, 100f, new Vector2(48f, 44f));

            BuildCauseway(routeParent, "星门北上道", new Vector3(0f, 4f, 396f), new Vector3(0f, 4f, 458f), 22f, true);
            BuildArena(
                arenaParent,
                "沉眠武库",
                new Vector3(0f, 4f, 500f),
                new Vector2(110f, 84f),
                ArenaGates.South | ArenaGates.North | ArenaGates.East | ArenaGates.West,
                10f,
                true);
            BuildBranchRoomPair(arenaParent, routeParent, "武库", new Vector3(0f, 4f, 500f), 55f, 92f, new Vector2(44f, 40f));

            BuildCauseway(routeParent, "武库巡礼道", new Vector3(0f, 4f, 542f), new Vector3(0f, 4f, 588f), 22f, true);
            BuildArena(
                arenaParent,
                "月蚀回廊",
                new Vector3(0f, 4f, 630f),
                new Vector2(106f, 84f),
                ArenaGates.South | ArenaGates.North | ArenaGates.East | ArenaGates.West,
                10f,
                true);
            BuildBranchRoomPair(arenaParent, routeParent, "月蚀", new Vector3(0f, 4f, 630f), 53f, 90f, new Vector2(44f, 40f));

            BuildCauseway(routeParent, "王座朝圣道", new Vector3(0f, 4f, 672f), new Vector3(0f, 7f, 710f), 24f, true);
            BuildArena(
                arenaParent,
                "轮回王座",
                new Vector3(0f, 7f, 770f),
                new Vector2(136f, 120f),
                ArenaGates.South | ArenaGates.East | ArenaGates.West,
                13f,
                true);
            BuildBranchRoomPair(arenaParent, routeParent, "王座", new Vector3(0f, 7f, 770f), 68f, 110f, new Vector2(48f, 46f));
        }

        private static void BuildBranchRoomPair(
            Transform arenaParent,
            Transform routeParent,
            string prefix,
            Vector3 mainCenter,
            float mainHalfWidth,
            float roomCenterX,
            Vector2 roomSize)
        {
            float innerRoomEdge = roomCenterX - roomSize.x * 0.5f;
            BuildCauseway(routeParent, prefix + "西支线", mainCenter + Vector3.left * mainHalfWidth, mainCenter + Vector3.left * innerRoomEdge, 10f, false);
            BuildCauseway(routeParent, prefix + "东支线", mainCenter + Vector3.right * mainHalfWidth, mainCenter + Vector3.right * innerRoomEdge, 10f, false);
            BuildArena(arenaParent, prefix + "西侧小房间", mainCenter + Vector3.left * roomCenterX, roomSize, ArenaGates.East, 6f, false);
            BuildArena(arenaParent, prefix + "东侧小房间", mainCenter + Vector3.right * roomCenterX, roomSize, ArenaGates.West, 6f, false);
        }

        private static void BuildArena(
            Transform parent,
            string name,
            Vector3 center,
            Vector2 size,
            ArenaGates gates,
            float wallHeight,
            bool addCombatCover)
        {
            Transform arena = CreateGroup(parent, name);
            CreateBox(
                name + "_地面",
                center + Vector3.down * 0.5f,
                new Vector3(size.x, 1f, size.y),
                _stoneMaterial,
                arena,
                true,
                true);

            CreateBox(
                name + "_中央纹理层",
                center + Vector3.up * 0.025f,
                new Vector3(size.x * 0.58f, 0.05f, size.y * 0.58f),
                _soilMaterial,
                arena,
                false,
                false);
            CreateBox(
                name + "_主轴纹章",
                center + Vector3.up * 0.055f,
                new Vector3(1.15f, 0.045f, size.y * 0.58f),
                _routeMaterial,
                arena,
                false,
                false);

            BuildArenaWall(arena, center, size, wallHeight, ArenaGates.North, (gates & ArenaGates.North) != 0);
            BuildArenaWall(arena, center, size, wallHeight, ArenaGates.South, (gates & ArenaGates.South) != 0);
            BuildArenaWall(arena, center, size, wallHeight, ArenaGates.East, (gates & ArenaGates.East) != 0);
            BuildArenaWall(arena, center, size, wallHeight, ArenaGates.West, (gates & ArenaGates.West) != 0);

            float x = size.x * 0.38f;
            float z = size.y * 0.38f;
            CreateCylinder("角塔_西南", center + new Vector3(-x, wallHeight * 0.5f, -z), 4.2f, wallHeight + 2f, _architectureMaterial, arena, true);
            CreateCylinder("角塔_东南", center + new Vector3(x, wallHeight * 0.5f, -z), 4.2f, wallHeight + 2f, _architectureMaterial, arena, true);
            CreateCylinder("角塔_西北", center + new Vector3(-x, wallHeight * 0.5f, z), 4.2f, wallHeight + 2f, _architectureMaterial, arena, true);
            CreateCylinder("角塔_东北", center + new Vector3(x, wallHeight * 0.5f, z), 4.2f, wallHeight + 2f, _architectureMaterial, arena, true);

            if (!addCombatCover)
                return;

            float coverY = center.y + 1.1f;
            CreateBox("掩体_西南", new Vector3(center.x - size.x * 0.2f, coverY, center.z - size.y * 0.18f), new Vector3(7f, 2.2f, 4f), _architectureMaterial, arena, true, true, Quaternion.Euler(0f, 18f, 0f));
            CreateBox("掩体_东北", new Vector3(center.x + size.x * 0.21f, coverY, center.z + size.y * 0.17f), new Vector3(8f, 2.2f, 4f), _architectureMaterial, arena, true, true, Quaternion.Euler(0f, -22f, 0f));
            CreateCylinder("中央断柱", center + new Vector3(0f, 1.5f, 0f), 3.2f, 3f, _architectureMaterial, arena, true);
        }

        private static void BuildArenaWall(
            Transform parent,
            Vector3 center,
            Vector2 size,
            float height,
            ArenaGates side,
            bool hasGate)
        {
            const float thickness = 2.5f;
            const float gateWidth = 22f;
            bool horizontal = side == ArenaGates.North || side == ArenaGates.South;
            float totalLength = horizontal ? size.x : size.y;
            float sideOffset = horizontal ? size.y * 0.5f : size.x * 0.5f;

            Vector3 axis = horizontal ? Vector3.right : Vector3.forward;
            Vector3 outward = side switch
            {
                ArenaGates.North => Vector3.forward,
                ArenaGates.South => Vector3.back,
                ArenaGates.East => Vector3.right,
                ArenaGates.West => Vector3.left,
                _ => Vector3.zero,
            };

            string sideName = side switch
            {
                ArenaGates.North => "北",
                ArenaGates.South => "南",
                ArenaGates.East => "东",
                ArenaGates.West => "西",
                _ => string.Empty,
            };

            if (!hasGate)
            {
                Vector3 scale = horizontal
                    ? new Vector3(totalLength, height, thickness)
                    : new Vector3(thickness, height, totalLength);
                CreateBox(sideName + "侧围墙", center + outward * sideOffset + Vector3.up * (height * 0.5f), scale, _architectureMaterial, parent, true, true);
                return;
            }

            float segmentLength = Mathf.Max(2f, (totalLength - gateWidth) * 0.5f);
            float segmentOffset = gateWidth * 0.5f + segmentLength * 0.5f;
            Vector3 segmentScale = horizontal
                ? new Vector3(segmentLength, height, thickness)
                : new Vector3(thickness, height, segmentLength);

            Vector3 basePosition = center + outward * sideOffset + Vector3.up * (height * 0.5f);
            CreateBox(sideName + "侧围墙_A", basePosition - axis * segmentOffset, segmentScale, _architectureMaterial, parent, true, true);
            CreateBox(sideName + "侧围墙_B", basePosition + axis * segmentOffset, segmentScale, _architectureMaterial, parent, true, true);
        }

        private static void BuildCauseway(Transform parent, string name, Vector3 start, Vector3 end, float width, bool addRails)
        {
            Transform causeway = CreateGroup(parent, name);
            Vector3 delta = end - start;
            float horizontalLength = new Vector2(delta.x, delta.z).magnitude;
            float length = delta.magnitude;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(delta.y, Mathf.Max(0.01f, horizontalLength)) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 midpoint = (start + end) * 0.5f - Vector3.up * 0.5f;

            CreateBox(name + "_路面", midpoint, new Vector3(width, 1f, length + 1f), _stoneMaterial, causeway, true, true, rotation);

            if (!addRails)
                return;

            CreateBox(
                name + "_主路线纹章带",
                midpoint + Vector3.up * 0.53f,
                new Vector3(Mathf.Max(1.1f, width * 0.12f), 0.055f, length * 0.92f),
                _routeMaterial,
                causeway,
                false,
                false,
                rotation);

            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 railScale = new Vector3(1.3f, 2.4f, length + 1f);
            CreateBox(name + "_左护墙", midpoint - right * (width * 0.5f + 0.65f) + Vector3.up * 1.1f, railScale, _architectureMaterial, causeway, true, true, rotation);
            CreateBox(name + "_右护墙", midpoint + right * (width * 0.5f + 0.65f) + Vector3.up * 1.1f, railScale, _architectureMaterial, causeway, true, true, rotation);
        }

        private static void BuildTempleRuins(Transform parent, Transform landmarkParent)
        {
            PlaceArch(parent, "出生门", new Vector3(0f, 0f, 24f), 0f, 1.35f);
            PlaceArch(parent, "首战南门", new Vector3(0f, 0f, 54f), 0f, 1.35f);
            PlaceArch(parent, "首战北门", new Vector3(0f, 0f, 118f), 180f, 1.35f);
            PlaceArch(parent, "西侧试炼门", new Vector3(-41f, 0f, 165f), 90f, 1.25f);
            PlaceArch(parent, "东侧探索门", new Vector3(41f, 0f, 165f), -90f, 1.25f);
            PlaceArch(parent, "精英入口", new Vector3(0f, 0f, 205f), 0f, 1.55f);
            PlaceArch(parent, "星门入口", new Vector3(0f, 4f, 304f), 0f, 1.75f);
            PlaceArch(parent, "武库入口", new Vector3(0f, 4f, 458f), 0f, 1.65f);
            PlaceArch(parent, "月蚀入口", new Vector3(0f, 4f, 588f), 0f, 1.65f);
            PlaceArch(parent, "王座入口", new Vector3(0f, 7f, 710f), 0f, 1.9f);

            PlacePrefab(ColumnPrefabPath, parent, "首战柱_西", new Vector3(-25f, 0f, 86f), Quaternion.Euler(0f, 12f, 0f), Vector3.one * 1.4f);
            PlacePrefab(BrokenColumnPrefabPath, parent, "首战断柱_东", new Vector3(25f, 0f, 84f), Quaternion.Euler(0f, -18f, 0f), Vector3.one * 1.5f);
            PlacePrefab(ColumnPrefabPath, parent, "精英柱_西", new Vector3(-34f, 0f, 245f), Quaternion.identity, Vector3.one * 1.75f);
            PlacePrefab(ColumnPrefabPath, parent, "精英柱_东", new Vector3(34f, 0f, 245f), Quaternion.identity, Vector3.one * 1.75f);

            PlacePrefab(AltarPrefabPath, landmarkParent, "探索祭坛", new Vector3(72f, 0f, 174f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 1.4f);
            PlaceGameplayChest(landmarkParent, "探索宝箱地标", new Vector3(72f, 0f, 159f), 180f);
            PlacePrefab(PortalPrefabPath, landmarkParent, "中段星门", new Vector3(0f, 4f, 376f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 1.7f);
            PlacePrefab(PortalPrefabPath, landmarkParent, "终局传送门", new Vector3(0f, 7f, 818f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 2f);

            Vector3[] branchChestPositions =
            {
                new Vector3(-78f, 0f, 86f), new Vector3(78f, 0f, 86f),
                new Vector3(-88f, 0f, 245f), new Vector3(88f, 0f, 245f),
                new Vector3(-100f, 4f, 350f), new Vector3(100f, 4f, 350f),
                new Vector3(-92f, 4f, 500f), new Vector3(92f, 4f, 500f),
                new Vector3(-90f, 4f, 630f), new Vector3(90f, 4f, 630f),
                new Vector3(-110f, 7f, 770f), new Vector3(110f, 7f, 770f),
            };
            for (int i = 0; i < branchChestPositions.Length; i++)
                PlaceGameplayChest(landmarkParent, "支线宝箱_" + i.ToString("00"), branchChestPositions[i], i % 2 == 0 ? 90f : -90f);

            PlaceGameplayShop(landmarkParent, "首个区域补给商店", new Vector3(78f, 0f, 102f), -90f);
            PlaceGameplayShop(landmarkParent, "Boss战前补给商店", new Vector3(90f, 4f, 646f), -90f);

            PlaceTorchPair(parent, new Vector3(0f, 0f, 24f), 9f, 0f);
            PlaceTorchPair(parent, new Vector3(0f, 0f, 118f), 10f, 0f);
            PlaceTorchPair(parent, new Vector3(0f, 0f, 205f), 13f, 0f);
            PlaceTorchPair(parent, new Vector3(0f, 4f, 304f), 15f, 0f);
            PlaceTorchPair(parent, new Vector3(0f, 4f, 458f), 14f, 0f);
            PlaceTorchPair(parent, new Vector3(0f, 4f, 588f), 14f, 0f);
            PlaceTorchPair(parent, new Vector3(0f, 7f, 710f), 16f, 0f);
        }

        private static void BuildEnvironmentProps(Transform parent)
        {
            string[] cliffPrefabs =
            {
                "Assets/Eternal Temple/Prefabs/Cliffs/Cliff_Formation_02.prefab",
                "Assets/Eternal Temple/Prefabs/Cliffs/Cliff_Formation_05.prefab",
                "Assets/Eternal Temple/Prefabs/Cliffs/Cliff_Formation_08.prefab",
                "Assets/Eternal Temple/Prefabs/Cliffs/Cliff_Formation_11.prefab",
            };

            for (int i = 0; i < 26; i++)
            {
                float z = -10f + i * 34f;
                float side = i % 2 == 0 ? -1f : 1f;
                string path = cliffPrefabs[i % cliffPrefabs.Length];
                Vector3 position = new Vector3(side * (125f + (i % 3) * 12f), -3f, z);
                Quaternion rotation = Quaternion.Euler(0f, side < 0f ? 70f + i * 11f : -70f - i * 9f, 0f);
                float scale = 1.25f + (i % 4) * 0.16f;
                PlacePrefab(path, parent, "外围峭壁_" + i.ToString("00"), position, rotation, Vector3.one * scale);
            }

            string[] buildingPrefabs =
            {
                "Assets/Eternal Temple/Prefabs/Buildings/Building_Preset_02.prefab",
                "Assets/Eternal Temple/Prefabs/Buildings/Building_Preset_05.prefab",
                "Assets/Eternal Temple/Prefabs/Buildings/Building_Preset_08.prefab",
            };

            Vector3[] buildingPositions =
            {
                new Vector3(-105f, -2f, 72f),
                new Vector3(108f, -2f, 92f),
                new Vector3(-118f, -2f, 250f),
                new Vector3(120f, -2f, 280f),
                new Vector3(-94f, 0f, 360f),
                new Vector3(96f, 0f, 366f),
                new Vector3(-128f, 1f, 470f),
                new Vector3(132f, 1f, 520f),
                new Vector3(-126f, 1f, 620f),
                new Vector3(130f, 1f, 670f),
                new Vector3(-142f, 4f, 760f),
                new Vector3(144f, 4f, 790f),
            };

            for (int i = 0; i < buildingPositions.Length; i++)
            {
                float yaw = i % 2 == 0 ? 22f : -28f;
                PlacePrefab(buildingPrefabs[i % buildingPrefabs.Length], parent, "远景遗迹_" + i.ToString("00"), buildingPositions[i], Quaternion.Euler(0f, yaw, 0f), Vector3.one * 1.15f);
            }

            Vector3[] treePositions =
            {
                new Vector3(-33f, 0f, 12f),
                new Vector3(34f, 0f, 4f),
                new Vector3(-102f, 0f, 150f),
                new Vector3(-98f, 0f, 184f),
                new Vector3(101f, 0f, 145f),
                new Vector3(103f, 0f, 188f),
                new Vector3(-60f, 0f, 260f),
                new Vector3(62f, 0f, 265f),
                new Vector3(-70f, 4f, 350f),
                new Vector3(72f, 4f, 356f),
                new Vector3(-72f, 4f, 470f),
                new Vector3(74f, 4f, 528f),
                new Vector3(-69f, 4f, 610f),
                new Vector3(71f, 4f, 650f),
                new Vector3(-82f, 7f, 738f),
                new Vector3(84f, 7f, 802f),
            };

            for (int i = 0; i < treePositions.Length; i++)
            {
                float scale = 1.1f + (i % 3) * 0.18f;
                PlacePrefab(TreePrefabPath, parent, "枯树_" + i.ToString("00"), treePositions[i], Quaternion.Euler(0f, i * 37f, 0f), Vector3.one * scale);
            }
        }

        private static void BuildLighting(Transform parent)
        {
            CreatePointLight(parent, "出生冷光", new Vector3(0f, 10f, 0f), new Color(0.28f, 0.5f, 1f), 6f, 42f);
            CreatePointLight(parent, "首战火光", new Vector3(0f, 11f, 86f), new Color(1f, 0.42f, 0.18f), 8f, 52f);
            CreatePointLight(parent, "西侧猩红光", new Vector3(-72f, 10f, 165f), new Color(1f, 0.18f, 0.12f), 7f, 44f);
            CreatePointLight(parent, "东侧祭坛光", new Vector3(72f, 10f, 165f), new Color(0.18f, 0.85f, 0.75f), 7f, 44f);
            CreatePointLight(parent, "精英金光", new Vector3(0f, 13f, 245f), new Color(1f, 0.62f, 0.22f), 9f, 60f);
            CreatePointLight(parent, "星门幽光", new Vector3(0f, 18f, 350f), new Color(0.46f, 0.3f, 1f), 11f, 72f);
            CreatePointLight(parent, "武库青光", new Vector3(0f, 17f, 500f), new Color(0.18f, 0.72f, 0.86f), 9f, 62f);
            CreatePointLight(parent, "月蚀紫光", new Vector3(0f, 17f, 630f), new Color(0.65f, 0.2f, 1f), 10f, 64f);
            CreatePointLight(parent, "王座轮回光", new Vector3(0f, 24f, 770f), new Color(1f, 0.32f, 0.16f), 13f, 86f);
        }

        private static void DisableDecorationLights(GameObject environmentRoot)
        {
            Transform decorationRoot = environmentRoot.transform.Find("04_环境装饰");
            if (decorationRoot == null)
                return;

            foreach (Light light in decorationRoot.GetComponentsInChildren<Light>(true))
                light.enabled = false;
        }

        private static void ConfigureRegionGameplay(Transform flowRoot)
        {
            Vector3[] regionCenters =
            {
                new Vector3(0f, 0.25f, 86f),
                new Vector3(0f, 0.25f, 245f),
                new Vector3(0f, 4.25f, 350f),
                new Vector3(0f, 4.25f, 500f),
                new Vector3(0f, 4.25f, 630f),
                new Vector3(0f, 7.25f, 770f),
            };
            Vector3[] entryCenters =
            {
                new Vector3(0f, 0f, 70f),
                new Vector3(0f, 0f, 212f),
                new Vector3(0f, 4f, 312f),
                new Vector3(0f, 4f, 462f),
                new Vector3(0f, 4f, 592f),
                new Vector3(0f, 7f, 718f),
            };
            Vector3[] gatePositions =
            {
                new Vector3(0f, 0f, 118f),
                new Vector3(0f, 0f, 285f),
                new Vector3(0f, 4f, 396f),
                new Vector3(0f, 4f, 542f),
                new Vector3(0f, 4f, 672f),
            };
            string[] regionNames = { "风蚀前庭", "精英回廊", "星门中庭", "沉眠武库", "月蚀回廊", "轮回王座" };
            Vector3[] guardianBranchCenters =
            {
                new Vector3(-78f, 0f, 86f),
                new Vector3(88f, 0f, 245f),
                new Vector3(-100f, 4f, 350f),
                new Vector3(92f, 4f, 500f),
                new Vector3(-90f, 4f, 630f),
            };

            var gates = new List<RogueliteSealGate>();
            for (int i = 0; i < gatePositions.Length; i++)
                gates.Add(CreateSealGate(flowRoot, $"区域封印门_{i + 1:00}", gatePositions[i]));

            LevelLocalEnemySpawner[] oldSpawners = UnityEngine.Object.FindObjectsByType<LevelLocalEnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (oldSpawners.Length == 0)
                throw new InvalidOperationException("Level_1 中没有可复制的局部生成器模板。");

            LevelLocalEnemySpawner templateSpawner = oldSpawners[0];
            GameObject templateObject = templateSpawner.gameObject;
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(templateObject);
            Transform spawnerParent = templateObject.transform.parent;
            GameObject guardianOnePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardianOnePrefabPath);
            GameObject guardianTwoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardianTwoPrefabPath);
            if (guardianOnePrefab == null || guardianTwoPrefab == null)
                throw new InvalidOperationException("Level_1 缺少场景直摆守卫者预制体。");
            Transform guardianParent = CreateGroup(flowRoot, "直摆守卫者");
            var createdRegionSpawners = new List<List<LevelLocalEnemySpawner>>();
            var createdRegionGuardians = new List<List<RoguelitePlacedGuardian>>();
            int combatRegionCount = regionCenters.Length - 1;
            for (int i = 0; i < combatRegionCount; i++)
            {
                Vector3 combatAnchor = i == 0 ? regionCenters[i] + Vector3.back * 4f : regionCenters[i];
                var regionSpawners = new List<LevelLocalEnemySpawner>
                {
                    CreateRegionSpawner(prefabAsset, templateObject, spawnerParent, $"区域{i + 1:00}_精英", combatAnchor + new Vector3(8f, 0f, -7f), 3),
                    CreateRegionSpawner(prefabAsset, templateObject, spawnerParent, $"区域{i + 1:00}_小怪_A", combatAnchor + new Vector3(-8f, 0f, 7f), 4),
                    CreateRegionSpawner(prefabAsset, templateObject, spawnerParent, $"区域{i + 1:00}_支线小怪", new Vector3(-guardianBranchCenters[i].x, guardianBranchCenters[i].y, guardianBranchCenters[i].z), 4),
                };
                createdRegionSpawners.Add(regionSpawners);
                createdRegionGuardians.Add(CreatePlacedGuardians(
                    guardianParent,
                    guardianOnePrefab,
                    guardianTwoPrefab,
                    i,
                    guardianBranchCenters[i],
                    regionCenters[i]));
            }
            createdRegionSpawners.Add(new List<LevelLocalEnemySpawner>());
            createdRegionGuardians.Add(new List<RoguelitePlacedGuardian>());

            var oldSpawnerObjects = new HashSet<GameObject>();
            for (int i = 0; i < oldSpawners.Length; i++)
                oldSpawnerObjects.Add(oldSpawners[i].gameObject);
            foreach (GameObject oldSpawnerObject in oldSpawnerObjects)
                UnityEngine.Object.DestroyImmediate(oldSpawnerObject);

            LevelEnemySpawner[] globalSpawners = UnityEngine.Object.FindObjectsByType<LevelEnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < globalSpawners.Length; i++)
                UnityEngine.Object.DestroyImmediate(globalSpawners[i].gameObject);

            var regions = new List<RogueliteRegionFlowController.RegionDefinition>();
            for (int i = 0; i < regionCenters.Length; i++)
            {
                regions.Add(new RogueliteRegionFlowController.RegionDefinition
                {
                    regionId = $"region_{i + 1:00}",
                    displayName = regionNames[i],
                    guardianCenter = regionCenters[i],
                    guardianRadius = i == regionCenters.Length - 1 ? 72f : 62f,
                    entryCenter = entryCenters[i],
                    entryRadius = 13f,
                    spawners = createdRegionSpawners[i],
                    placedGuardians = createdRegionGuardians[i],
                    exitGate = i < gates.Count ? gates[i] : null,
                    isBossRegion = i == regionCenters.Length - 1,
                    bossSpawnPoint = regionCenters[i],
                });
            }

            RogueliteRegionFlowController flowController = flowRoot.gameObject.AddComponent<RogueliteRegionFlowController>();
            flowController.Configure("level_1_lost_temple_region_flow", regions);
            EditorUtility.SetDirty(flowController);

            RogueliteMinimapController minimapController = flowRoot.gameObject.AddComponent<RogueliteMinimapController>();
            EditorUtility.SetDirty(minimapController);

            LevelFallDeathController fallDeathController = flowRoot.gameObject.AddComponent<LevelFallDeathController>();
            fallDeathController.Configure(-18f);
            EditorUtility.SetDirty(fallDeathController);
        }

        private static LevelLocalEnemySpawner CreateRegionSpawner(
            GameObject prefabAsset,
            GameObject templateObject,
            Transform parent,
            string name,
            Vector3 position,
            int planIndex)
        {
            GameObject instance = prefabAsset != null
                ? PrefabUtility.InstantiatePrefab(prefabAsset, _targetScene) as GameObject
                : UnityEngine.Object.Instantiate(templateObject, parent);
            if (instance == null)
                throw new InvalidOperationException($"局部生成器创建失败：{name}");

            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            LevelLocalEnemySpawner spawner = instance.GetComponent<LevelLocalEnemySpawner>();
            if (spawner == null)
                spawner = instance.GetComponentInChildren<LevelLocalEnemySpawner>(true);
            if (spawner == null)
                throw new InvalidOperationException($"局部生成器模板缺少 LevelLocalEnemySpawner：{name}");

            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("_planIndex").intValue = planIndex;
            serializedSpawner.FindProperty("_waitForActivation").boolValue = true;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            spawner.enabled = true;
            EditorUtility.SetDirty(spawner);
            return spawner;
        }

        private static List<RoguelitePlacedGuardian> CreatePlacedGuardians(
            Transform parent,
            GameObject guardianOnePrefab,
            GameObject guardianTwoPrefab,
            int regionIndex,
            Vector3 branchCenter,
            Vector3 regionCenter)
        {
            var result = new List<RoguelitePlacedGuardian>();
            GameObject[] prefabs = { guardianOnePrefab, guardianTwoPrefab };
            Vector3[] offsets = { new Vector3(0f, 0f, -9f), new Vector3(0f, 0f, 9f) };
            string regionId = $"region_{regionIndex + 1:00}";
            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject guardianObject = PrefabUtility.InstantiatePrefab(prefabs[i], _targetScene) as GameObject;
                if (guardianObject == null)
                    throw new InvalidOperationException($"Level_1 区域 {regionIndex + 1} 的直摆守卫者创建失败。");

                Vector3 position = branchCenter + offsets[i];
                Vector3 lookDirection = regionCenter - position;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude <= 0.001f)
                    lookDirection = Vector3.forward;
                guardianObject.name = $"区域{regionIndex + 1:00}_支线守卫者_{i + 1:00}";
                guardianObject.transform.SetParent(parent, true);
                guardianObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookDirection.normalized, Vector3.up));
                RoguelitePlacedGuardian marker = guardianObject.GetComponent<RoguelitePlacedGuardian>();
                if (marker == null)
                    marker = guardianObject.AddComponent<RoguelitePlacedGuardian>();
                marker.Configure($"level_1_{regionId}_guardian_{i + 1:00}", regionId);
                EditorUtility.SetDirty(marker);
                guardianObject.SetActive(false);
                result.Add(marker);
            }

            return result;
        }

        private static RogueliteSealGate CreateSealGate(Transform parent, string name, Vector3 position)
        {
            GameObject gateObject = new GameObject(name);
            gateObject.transform.SetParent(parent, false);
            gateObject.transform.position = position;
            BoxCollider blocker = gateObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, 4f, 0f);
            blocker.size = new Vector3(21f, 8f, 2.4f);
            NavMeshModifier navMeshModifier = gateObject.AddComponent<NavMeshModifier>();
            navMeshModifier.ignoreFromBuild = true;

            Transform visualRoot = CreateGroup(gateObject.transform, "封印光影视觉");
            var energyRenderers = new List<Renderer>();
            for (int i = 0; i < 2; i++)
            {
                float layerScale = 1f - i * 0.055f;
                energyRenderers.Add(CreateEnergyQuad(
                    visualRoot,
                    $"流动封印光幕_{i + 1:00}",
                    position + new Vector3(0f, 4f, i == 0 ? -0.06f : 0.08f),
                    new Vector2(20.8f * layerScale, 8.2f * layerScale),
                    i % 2 == 0 ? 0f : 180f));
            }

            Color sealColor = new Color(0.3f, 0.8f, 1f, 0.22f);
            var sealLights = new List<Light>();
            for (int i = -1; i <= 1; i++)
            {
                GameObject lightObject = new GameObject(i < 0 ? "封印左光源" : i > 0 ? "封印右光源" : "封印核心光源");
                lightObject.transform.SetParent(visualRoot, false);
                lightObject.transform.position = position + new Vector3(i * 8f, 4f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = sealColor;
                light.intensity = i == 0 ? 1.6f : 2.2f;
                light.range = i == 0 ? 11f : 13f;
                light.shadows = LightShadows.None;
                sealLights.Add(light);
            }

            var fogParticles = new List<ParticleSystem>
            {
                CreateSealParticleLayer(visualRoot, "封印流光雾", position + Vector3.up * 4f, sealColor, false),
                CreateSealParticleLayer(visualRoot, "封印星屑", position + Vector3.up * 4f, sealColor, true),
            };

            CreateBox("封印门左柱", position + new Vector3(-11.5f, 4f, 0f), new Vector3(2.2f, 9f, 3f), _architectureMaterial, parent, true, true);
            CreateBox("封印门右柱", position + new Vector3(11.5f, 4f, 0f), new Vector3(2.2f, 9f, 3f), _architectureMaterial, parent, true, true);
            CreateBox("封印门横梁", position + new Vector3(0f, 8.65f, 0f), new Vector3(25.2f, 1.3f, 3f), _architectureMaterial, parent, true, true);

            energyRenderers.Add(CreateEnergyQuad(visualRoot, "封印顶部辉光", position + new Vector3(0f, 8.45f, -0.3f), new Vector2(23.5f, 0.55f), 0f));
            energyRenderers.Add(CreateEnergyQuad(visualRoot, "封印底部辉光", position + new Vector3(0f, 0.25f, -0.3f), new Vector2(21f, 0.35f), 0f));

            RogueliteSealGate gate = gateObject.AddComponent<RogueliteSealGate>();
            gate.Configure(blocker, visualRoot.gameObject, energyRenderers.ToArray(), sealLights.ToArray(), fogParticles.ToArray(), sealColor);
            EditorUtility.SetDirty(gate);
            return gate;
        }

        private static Renderer CreateEnergyQuad(
            Transform parent,
            string name,
            Vector3 position,
            Vector2 size,
            float roll)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, roll));
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = _sealMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static ParticleSystem CreateSealParticleLayer(
            Transform parent,
            string name,
            Vector3 position,
            Color color,
            bool sparks)
        {
            GameObject particlesObject = new GameObject(name);
            particlesObject.transform.SetParent(parent, false);
            particlesObject.transform.position = position;
            ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.duration = 2.5f;
            main.startLifetime = sparks
                ? new ParticleSystem.MinMaxCurve(0.7f, 1.35f)
                : new ParticleSystem.MinMaxCurve(1.6f, 2.7f);
            main.startSpeed = sparks
                ? new ParticleSystem.MinMaxCurve(0.35f, 1.1f)
                : new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
            main.startSize = sparks
                ? new ParticleSystem.MinMaxCurve(0.06f, 0.14f)
                : new ParticleSystem.MinMaxCurve(0.35f, 1.15f);
            Color particleColor = color;
            particleColor.a = sparks ? 0.58f : 0.16f;
            main.startColor = particleColor;
            main.maxParticles = sparks ? 180 : 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = sparks ? 24f : 14f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = sparks ? new Vector3(19.5f, 7.4f, 1.2f) : new Vector3(18.5f, 6.8f, 0.8f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(sparks ? 1.2f : 0.18f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = sparks ? 0.24f : 0.42f;
            noise.frequency = sparks ? 0.8f : 0.35f;
            noise.scrollSpeed = sparks ? 0.4f : 0.2f;

            ParticleSystemRenderer particleRenderer = particlesObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = _sealMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            return particles;
        }

        private static void BuildNavigation(Scene scene)
        {
            NavMeshSurface surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>(FindObjectsInactive.Include);
            if (surface == null || surface.gameObject.scene != scene)
                throw new InvalidOperationException("Level_1 中未找到 Navigation/NavMeshSurface。");

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.overrideTileSize = true;
            surface.tileSize = 256;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.2f;
            surface.minRegionArea = 2f;

            NavMeshData persistedData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
            surface.BuildNavMesh();
            NavMeshData builtData = surface.navMeshData;
            if (builtData == null)
                throw new InvalidOperationException("Level_1 NavMesh 烘焙失败，未生成导航数据。");

            if (persistedData == null)
            {
                AssetDatabase.CreateAsset(builtData, NavMeshAssetPath);
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

        private static void ValidateNavigation()
        {
            var checkpoints = new Dictionary<string, Vector3>
            {
                { "出生点", new Vector3(0f, 0f, 70f) },
                { "首战广场", new Vector3(0f, 0f, 86f) },
                { "西侧高压战斗区", new Vector3(-72f, 0f, 165f) },
                { "东侧探索奖励区", new Vector3(72f, 0f, 165f) },
                { "精英回廊", new Vector3(0f, 0f, 245f) },
                { "星门中庭", new Vector3(0f, 4f, 350f) },
                { "沉眠武库", new Vector3(0f, 4f, 500f) },
                { "月蚀回廊", new Vector3(0f, 4f, 630f) },
                { "轮回王座", new Vector3(0f, 7f, 770f) },
                { "首战西支线", new Vector3(-78f, 0f, 86f) },
                { "星门东支线", new Vector3(100f, 4f, 350f) },
                { "武库西支线", new Vector3(-92f, 4f, 500f) },
                { "月蚀东支线", new Vector3(90f, 4f, 630f) },
                { "王座东支线", new Vector3(110f, 7f, 770f) },
            };

            var sampledPositions = new Dictionary<string, Vector3>();
            foreach (KeyValuePair<string, Vector3> checkpoint in checkpoints)
            {
                if (!NavMesh.SamplePosition(checkpoint.Value, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    throw new InvalidOperationException($"Level_1 导航验证失败：{checkpoint.Key} 未找到 NavMesh。");
                sampledPositions.Add(checkpoint.Key, hit.position);
            }

            (string from, string to)[] requiredPaths =
            {
                ("出生点", "首战广场"),
                ("首战广场", "精英回廊"),
                ("精英回廊", "星门中庭"),
                ("星门中庭", "沉眠武库"),
                ("沉眠武库", "月蚀回廊"),
                ("月蚀回廊", "轮回王座"),
                ("首战广场", "首战西支线"),
                ("星门中庭", "星门东支线"),
                ("沉眠武库", "武库西支线"),
                ("月蚀回廊", "月蚀东支线"),
                ("轮回王座", "王座东支线"),
            };

            foreach ((string from, string to) in requiredPaths)
            {
                var path = new NavMeshPath();
                bool found = NavMesh.CalculatePath(sampledPositions[from], sampledPositions[to], NavMesh.AllAreas, path);
                if (!found || path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException(
                        $"Level_1 导航验证失败：{from} 无法完整到达 {to}。" +
                        $"起点={sampledPositions[from].ToString("F2")}，终点={sampledPositions[to].ToString("F2")}，" +
                        $"Found={found}，Status={path.status}，" +
                        $"Corners={string.Join(" -> ", Array.ConvertAll(path.corners, point => point.ToString("F2")))}");
            }

            LevelLocalEnemySpawner[] spawners = UnityEngine.Object.FindObjectsByType<LevelLocalEnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (spawners.Length != 15)
                throw new InvalidOperationException($"Level_1 敌人生成器数量错误：{spawners.Length}，预期=15。");

            RoguelitePlacedGuardian[] placedGuardians = UnityEngine.Object.FindObjectsByType<RoguelitePlacedGuardian>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (placedGuardians.Length != 10)
                throw new InvalidOperationException($"Level_1 直摆守卫者数量错误：{placedGuardians.Length}，预期=10。");
            for (int i = 0; i < placedGuardians.Length; i++)
            {
                if (placedGuardians[i].gameObject.activeSelf)
                    throw new InvalidOperationException($"Level_1 直摆守卫者必须等待区域激活：{placedGuardians[i].name}。");
                if (!NavMesh.SamplePosition(placedGuardians[i].transform.position, out _, 3f, NavMesh.AllAreas))
                    throw new InvalidOperationException($"Level_1 直摆守卫者未落在导航地面：{placedGuardians[i].name}。");
            }

            HutaoShopInteractable[] shops = UnityEngine.Object.FindObjectsByType<HutaoShopInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (shops.Length != 2)
                throw new InvalidOperationException($"Level_1 商店数量错误：{shops.Length}，预期=2。");

            GameObject environmentRoot = FindRoot(_targetScene, EnvironmentRootName);
            Renderer[] environmentRenderers = environmentRoot != null
                ? environmentRoot.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            for (int rendererIndex = 0; rendererIndex < environmentRenderers.Length; rendererIndex++)
            {
                Renderer renderer = environmentRenderers[rendererIndex];
                if (renderer is ParticleSystemRenderer
                    || renderer is TrailRenderer
                    || renderer.GetComponentInParent<RoguelitePlacedGuardian>() != null
                    || renderer.GetComponentInParent<HutaoShopInteractable>() != null
                    || renderer.GetComponentInParent<ChestInteractable>() != null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (NeedsEnvironmentMaterialRepair(materials[materialIndex]))
                        throw new InvalidOperationException($"Level_1 仍存在白模或无效材质：{renderer.name}。");
                }
            }

            Debug.Log($"[Level1RogueliteSceneBuilder] NAVIGATION_OK | Checkpoints={checkpoints.Count} | RequiredPaths={requiredPaths.Length}");
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
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

            Renderer renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            if (!keepCollider)
            {
                Collider collider = box.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            SetStaticFlags(box, navigationStatic);
            return box;
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
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = _voidFogMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetStaticFlags(quad, false);
            return quad;
        }

        private static GameObject CreateCylinder(
            string name,
            Vector3 position,
            float diameter,
            float height,
            Material material,
            Transform parent,
            bool navigationStatic)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.position = position;
            cylinder.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            SetStaticFlags(cylinder, navigationStatic);
            return cylinder;
        }

        private static void SetStaticFlags(GameObject gameObject, bool navigationStatic)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
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

        private static GameObject PlacePrefab(
            string prefabPath,
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Level1RogueliteSceneBuilder] 未找到装饰预制体：{prefabPath}");
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, _targetScene) as GameObject;
            if (instance == null)
                return null;

            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            RepairPlacedPrefabMaterials(instance, prefabPath);
            return instance;
        }

        private static void RepairPlacedPrefabMaterials(GameObject instance, string prefabPath)
        {
            if (instance == null)
                return;

            bool useSoilMaterial = prefabPath.Contains("/Cliffs/", StringComparison.Ordinal)
                || prefabPath.Contains("/Flora/", StringComparison.Ordinal);
            Material replacement = useSoilMaterial ? _soilMaterial : _architectureMaterial;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (!NeedsEnvironmentMaterialRepair(materials[materialIndex]))
                        continue;

                    materials[materialIndex] = replacement;
                    changed = true;
                }

                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        private static bool NeedsEnvironmentMaterialRepair(Material material)
        {
            if (material == null || material.shader == null || !material.shader.isSupported)
                return true;
            if (material.name.Contains("Default-Material", StringComparison.OrdinalIgnoreCase))
                return true;

            bool hasTexture = (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                || (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null);
            if (hasTexture)
                return false;

            Color color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
            return color.r >= 0.86f
                && color.g >= 0.86f
                && color.b >= 0.86f
                && emission.maxColorComponent <= 0.05f;
        }

        private static void PlaceGameplayChest(Transform parent, string name, Vector3 position, float yaw)
        {
            GameObject chest = PlacePrefab(
                ChestPrefabPath,
                parent,
                name,
                position,
                Quaternion.Euler(0f, yaw, 0f),
                Vector3.one * 1.25f);
            if (chest != null)
            {
                ChestInteractable interactable = ChestInteractable.EnsureOn(chest);
                NavMeshObstacle obstacle = chest.GetComponent<NavMeshObstacle>();
                if (obstacle == null)
                    obstacle = chest.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = new Vector3(0f, 0.8f, 0f);
                obstacle.size = new Vector3(2f, 1.6f, 1.5f);
                obstacle.carving = true;
                SerializedObject serializedChest = new SerializedObject(interactable);
                serializedChest.FindProperty("_snapshotPrefabId").stringValue = "宝箱";
                serializedChest.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(interactable);
            }
        }

        private static void PlaceGameplayShop(Transform parent, string name, Vector3 position, float yaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"缺少商店预制体：{ShopPrefabPath}");

            GameObject shop = PrefabUtility.InstantiatePrefab(prefab, _targetScene) as GameObject;
            if (shop == null)
                throw new InvalidOperationException($"Level_1 商店创建失败：{name}");

            shop.name = name;
            shop.transform.SetParent(parent, true);
            Quaternion importedAxisCorrection = prefab.transform.localRotation;
            shop.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * importedAxisCorrection);
            if (shop.GetComponentInChildren<HutaoShopInteractable>(true) == null)
                HutaoShopInteractable.EnsureOn(shop);
        }

        private static void PlaceArch(Transform parent, string name, Vector3 position, float yaw, float scale)
        {
            PlacePrefab(ArchPrefabPath, parent, name, position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
        }

        private static void PlaceTorchPair(Transform parent, Vector3 center, float halfSpacing, float yaw)
        {
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            PlacePrefab(TorchPrefabPath, parent, "火把_左", center - right * halfSpacing + Vector3.up * 2f, Quaternion.Euler(0f, yaw, 0f), Vector3.one * 1.2f);
            PlacePrefab(TorchPrefabPath, parent, "火把_右", center + right * halfSpacing + Vector3.up * 2f, Quaternion.Euler(0f, yaw + 180f, 0f), Vector3.one * 1.2f);
        }

        private static void CreatePointLight(
            Transform parent,
            string name,
            Vector3 position,
            Color color,
            float intensity,
            float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.65f;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (string.Equals(root.name, name, StringComparison.Ordinal))
                    return root;
            }

            return null;
        }

        private static string RenderPreview(GameObject environmentRoot)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return string.Empty;

            string previewPath = Path.Combine(projectRoot, "Temp", "Level1RogueliteScenePreview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(previewPath) ?? projectRoot);

            GameObject cameraObject = new GameObject("Level1Roguelite_PreviewCamera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 950f, 390f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 475f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.09f, 1f);

            GameObject lightObject = new GameObject("Level1Roguelite_PreviewLight")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            Light previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.color = new Color(0.9f, 0.94f, 1f, 1f);
            previewLight.intensity = 1.8f;
            previewLight.shadows = LightShadows.None;
            lightObject.transform.rotation = Quaternion.Euler(65f, -25f, 0f);

            AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            Color previousAmbientLight = RenderSettings.ambientLight;
            float previousAmbientIntensity = RenderSettings.ambientIntensity;
            bool previousFog = RenderSettings.fog;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.7f, 1f);
            RenderSettings.ambientIntensity = 1.25f;
            RenderSettings.fog = false;

            RenderTexture renderTexture = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(previewPath, texture.EncodeToPNG());

            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;
            RenderSettings.ambientIntensity = previousAmbientIntensity;
            RenderSettings.fog = previousFog;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(lightObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            return previewPath;
        }

        private static void LogBuildSummary(GameObject environmentRoot, string previewPath)
        {
            int rendererCount = environmentRoot.GetComponentsInChildren<Renderer>(true).Length;
            int colliderCount = environmentRoot.GetComponentsInChildren<Collider>(true).Length;
            Light[] lights = environmentRoot.GetComponentsInChildren<Light>(true);
            int enabledLightCount = 0;
            foreach (Light light in lights)
            {
                if (light.enabled && light.gameObject.activeInHierarchy)
                    enabledLightCount++;
            }
            Debug.Log($"[Level1RogueliteSceneBuilder] COMPLETE | Renderers={rendererCount} | Colliders={colliderCount} | EnabledLights={enabledLightCount}/{lights.Length} | Preview={previewPath}");
        }
    }
}
