using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DragonSwordQiEffect.WaterDragonQi.Editor
{
    public static class WaterDragonQiTestSceneBuilder
    {
        private const string RootFolder = "Assets/DragonSwordQi/WaterDragonQi";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string ScenePath = SceneFolder + "/WaterDragonQi_Test.unity";
        private const string PrefabPath = RootFolder + "/Generated/Prefabs/WaterDragonQi.prefab";

        public static void CreateTestScene()
        {
            EnsureFolder(SceneFolder);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Missing prefab asset: {PrefabPath}");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;

            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.02f, 0.03f, 0.04f, 1f);
                RenderSettings.fog = false;

                root = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (root == null)
                    throw new InvalidOperationException("Failed to instantiate WaterDragonQi prefab.");

                root.name = "WaterDragonQi";
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;

                ConfigureController(root);

                Bounds bounds = CalculateCombinedBounds(root);

                cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.fieldOfView = 24f;
                FrameCamera(camera, bounds);

                lightObject = new GameObject("Directional Light");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(0.76f, 0.88f, 1.00f, 1f);
                light.intensity = 1.15f;
                light.transform.rotation = Quaternion.Euler(34f, -28f, 0f);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                Debug.Log($"[WaterDragonQi] Test scene created: {ScenePath}");
            }
            finally
            {
                if (lightObject != null)
                    UnityEngine.Object.DestroyImmediate(lightObject);
                if (cameraObject != null)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureController(GameObject root)
        {
            WaterDragonQiDragonController controller = root.GetComponentInChildren<WaterDragonQiDragonController>(true);
            if (controller == null)
                throw new InvalidOperationException("WaterDragonQiDragonController is missing from prefab instance.");

            SerializedObject serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("playOnAwake").boolValue = true;
            serializedObject.FindProperty("loop").boolValue = true;
            serializedObject.FindProperty("destroyOnComplete").boolValue = false;
            serializedObject.FindProperty("loopDelay").floatValue = 0.25f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            controller.PreviewAtTime(1.05f);
        }

        private static Bounds CalculateCombinedBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.one);

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

            return hasBounds ? bounds : new Bounds(root.transform.position, Vector3.one);
        }

        private static void FrameCamera(Camera camera, Bounds bounds)
        {
            Vector3 extents = bounds.extents;
            float maxExtent = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            float distance = Mathf.Max(3.5f, maxExtent * 3.0f);
            Vector3 direction = new Vector3(0.9f, 0.42f, -1.0f).normalized;

            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center + new Vector3(0f, extents.y * 0.1f, 0f));
            camera.nearClipPlane = Mathf.Max(0.03f, distance * 0.02f);
            camera.farClipPlane = Mathf.Max(50f, distance * 10f);
        }

        private static void EnsureFolder(string path)
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
    }
}
