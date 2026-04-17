using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 场景 / Prefab 丢失脚本清理工具。
    /// </summary>
    public static class MissingScriptUtility
    {
        [MenuItem("游戏/场景/扫描当前场景丢失脚本", false, 2000)]
        private static void ScanActiveSceneMissingScripts()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[MissingScriptUtility] 当前没有已加载的有效场景。");
                return;
            }

            List<string> hits = new List<string>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                CollectMissingScripts(roots[i].transform, roots[i].name, hits);

            if (hits.Count == 0)
            {
                Debug.Log($"[MissingScriptUtility] 场景 {scene.name} 未发现 Missing Script。");
                return;
            }

            Debug.LogWarning(
                $"[MissingScriptUtility] 场景 {scene.name} 发现 {hits.Count} 个带 Missing Script 的对象：\n" +
                string.Join("\n", hits));
        }

        [MenuItem("游戏/场景/移除当前场景丢失脚本", false, 2001)]
        private static void RemoveActiveSceneMissingScripts()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[MissingScriptUtility] 当前没有已加载的有效场景。");
                return;
            }

            int removedCount = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                removedCount += RemoveMissingScriptsRecursive(roots[i].transform);

            if (removedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[MissingScriptUtility] 已从场景 {scene.name} 移除 {removedCount} 个 Missing Script 组件。");
            }
            else
            {
                Debug.Log($"[MissingScriptUtility] 场景 {scene.name} 没有可移除的 Missing Script。");
            }
        }

        [MenuItem("Assets/扫描选中 Prefab 丢失脚本", false, 2200)]
        private static void ScanSelectedPrefabMissingScripts()
        {
            string[] assetGuids = Selection.assetGUIDs;
            if (assetGuids == null || assetGuids.Length == 0)
            {
                Debug.LogWarning("[MissingScriptUtility] 请先在 Project 视图中选中一个或多个 Prefab。");
                return;
            }

            List<string> hits = new List<string>();
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                if (!assetPath.EndsWith(".prefab"))
                    continue;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    CollectMissingScripts(prefabRoot.transform, assetPath, hits);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            if (hits.Count == 0)
            {
                Debug.Log("[MissingScriptUtility] 选中的 Prefab 未发现 Missing Script。");
                return;
            }

            Debug.LogWarning("[MissingScriptUtility] 选中的 Prefab 发现 Missing Script：\n" + string.Join("\n", hits));
        }

        [MenuItem("Assets/移除选中 Prefab 丢失脚本", false, 2201)]
        private static void RemoveSelectedPrefabMissingScripts()
        {
            string[] assetGuids = Selection.assetGUIDs;
            if (assetGuids == null || assetGuids.Length == 0)
            {
                Debug.LogWarning("[MissingScriptUtility] 请先在 Project 视图中选中一个或多个 Prefab。");
                return;
            }

            int removedCount = 0;
            int prefabCount = 0;

            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                if (!assetPath.EndsWith(".prefab"))
                    continue;

                prefabCount++;
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    int removedInPrefab = RemoveMissingScriptsRecursive(prefabRoot.transform);
                    if (removedInPrefab > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                        removedCount += removedInPrefab;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            if (prefabCount == 0)
            {
                Debug.LogWarning("[MissingScriptUtility] 选中的资源里没有 Prefab。");
                return;
            }

            if (removedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[MissingScriptUtility] 已从选中的 Prefab 中移除 {removedCount} 个 Missing Script 组件。");
            }
            else
            {
                Debug.Log("[MissingScriptUtility] 选中的 Prefab 没有可移除的 Missing Script。");
            }
        }

        [MenuItem("Assets/扫描选中 Prefab 丢失脚本", true)]
        [MenuItem("Assets/移除选中 Prefab 丢失脚本", true)]
        private static bool ValidatePrefabSelection()
        {
            return Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
        }

        private static void CollectMissingScripts(Transform current, string path, List<string> hits)
        {
            if (current == null)
                return;

            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current.gameObject);
            if (missingCount > 0)
                hits.Add($"{path}/{current.name} -> {missingCount}");

            for (int i = 0; i < current.childCount; i++)
                CollectMissingScripts(current.GetChild(i), $"{path}/{current.name}", hits);
        }

        private static int RemoveMissingScriptsRecursive(Transform current)
        {
            if (current == null)
                return 0;

            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(current.gameObject);
            for (int i = 0; i < current.childCount; i++)
                removedCount += RemoveMissingScriptsRecursive(current.GetChild(i));

            return removedCount;
        }
    }
}
