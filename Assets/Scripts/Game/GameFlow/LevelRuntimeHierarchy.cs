using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Presentation;

namespace Game.GameFlow
{
    internal static class LevelRuntimeHierarchy
    {
        private const string GameFlowRootName = "GameFlow";
        private const string RuntimeRootName = "RuntimeRoot";
        private const string EnemiesRootName = "Enemies";
        private const string InteractablesRootName = "Interactables";
        private const string PickupsRootName = "Pickups";
        private const string SystemsRootName = "Systems";
        private const string SummonsRootName = "Summons";
        private const string WorldVfxRootName = "VFX";

        public static void EnsureSceneRoots()
        {
            GetEnemiesRoot();
            GetInteractablesRoot();
            GetPickupsRoot();
            GetSystemsRoot();
            GetSummonsRoot();
            GetWorldVfxRoot();
        }

        public static void OrganizeSceneRuntimeObjects()
        {
            ReparentSceneRoots<EnemyController>(GetEnemiesRoot());
            ReparentSceneRoots<ChestInteractable>(GetInteractablesRoot());
            ReparentSceneRoots<HutaoShopInteractable>(GetInteractablesRoot());
            ReparentSceneRoots<DroppedPickupRuntime>(GetPickupsRoot());
            ReparentSceneRoots<LevelEnemySpawner>(GetSystemsRoot());
            ReparentSceneRoots<PlayerCloneActor>(GetSummonsRoot());
        }

        public static Transform GetEnemiesRoot()
        {
            return GetOrCreateChildRoot(EnemiesRootName);
        }

        public static Transform GetInteractablesRoot()
        {
            return GetOrCreateChildRoot(InteractablesRootName);
        }

        public static Transform GetPickupsRoot()
        {
            return GetOrCreateChildRoot(PickupsRootName);
        }

        public static Transform GetSystemsRoot()
        {
            return GetOrCreateChildRoot(SystemsRootName);
        }

        public static Transform GetSummonsRoot()
        {
            return GetOrCreateChildRoot(SummonsRootName);
        }

        public static Transform GetWorldVfxRoot()
        {
            return GetOrCreateChildRoot(WorldVfxRootName);
        }

        private static Transform GetOrCreateChildRoot(string childName)
        {
            Transform runtimeRoot = GetOrCreateRuntimeRoot();
            if (runtimeRoot == null)
                return null;

            Transform child = runtimeRoot.Find(childName);
            if (child != null)
                return child;

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(runtimeRoot, false);
            return childObject.transform;
        }

        private static Transform GetOrCreateRuntimeRoot()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return null;

            Transform runtimeRoot = FindRootObject(activeScene, RuntimeRootName)?.transform;
            if (runtimeRoot != null)
                return runtimeRoot;

            GameObject gameFlowRoot = FindRootObject(activeScene, GameFlowRootName);
            Transform nestedRuntimeRoot = gameFlowRoot != null ? gameFlowRoot.transform.Find(RuntimeRootName) : null;
            if (nestedRuntimeRoot != null)
            {
                nestedRuntimeRoot.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(nestedRuntimeRoot.gameObject, activeScene);
                return nestedRuntimeRoot;
            }

            GameObject runtimeRootObject = new GameObject(RuntimeRootName);
            SceneManager.MoveGameObjectToScene(runtimeRootObject, activeScene);
            return runtimeRootObject.transform;
        }

        private static void ReparentSceneRoots<T>(Transform targetRoot) where T : Component
        {
            if (targetRoot == null)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                    continue;

                Transform currentTransform = component.transform;
                if (currentTransform == null || currentTransform == targetRoot)
                    continue;

                if (currentTransform.parent != null)
                    continue;

                GameObject currentObject = currentTransform.gameObject;
                if (currentObject == null || currentObject.scene != activeScene)
                    continue;

                currentTransform.SetParent(targetRoot, true);
            }
        }

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                GameObject rootObject = rootObjects[i];
                if (rootObject != null && rootObject.name == objectName)
                    return rootObject;
            }

            return null;
        }
    }
}
