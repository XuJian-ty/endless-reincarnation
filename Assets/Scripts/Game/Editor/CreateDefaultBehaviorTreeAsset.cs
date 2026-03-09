using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.AI;

namespace Game.Editor
{
    /// <summary>
    /// Create or update the default enemy behavior tree asset in Resources/配置.
    /// </summary>
    public static class CreateDefaultBehaviorTreeAsset
    {
        private const string ResourcesConfig = "Assets/Resources/配置";
        private const string TargetAssetPath = "Assets/Resources/配置/行为树_.asset";
        private const string LegacyAssetPath = "Assets/Resources/AI/DefaultEnemyBehaviorTree.asset";

        /// <summary>Called by "游戏/一键创建全部配置（需求书默认数据）".</summary>
        public static BehaviorTreeAsset Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesConfig))
                AssetDatabase.CreateFolder("Assets/Resources", "配置");

            if (AssetDatabase.LoadAssetAtPath<BehaviorTreeAsset>(TargetAssetPath) == null &&
                AssetDatabase.LoadAssetAtPath<BehaviorTreeAsset>(LegacyAssetPath) != null)
            {
                string error = AssetDatabase.MoveAsset(LegacyAssetPath, TargetAssetPath);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning($"[AI] Failed to migrate default behavior tree: {error}");
            }

            var asset = ScriptableObject.CreateInstance<BehaviorTreeAsset>();
            asset.nodes = BuildNodes();
            asset = CreateOrUpdateAsset(asset, TargetAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[AI] Default behavior tree updated: Assets/Resources/配置/行为树_.asset");
            return asset;
        }

        private static List<BehaviorTreeNodeData> BuildNodes()
        {
            return new List<BehaviorTreeNodeData>
            {
                new BehaviorTreeNodeData { nodeType = "Selector", parentIndex = -1, siblingOrder = 0 },

                new BehaviorTreeNodeData { nodeType = "Sequence", parentIndex = 0, siblingOrder = 0 },
                new BehaviorTreeNodeData { nodeType = "CondIsHurt", parentIndex = 1, siblingOrder = 0 },
                new BehaviorTreeNodeData { nodeType = "ActionSetIntentHurt", parentIndex = 1, siblingOrder = 1 },

                new BehaviorTreeNodeData { nodeType = "Sequence", parentIndex = 0, siblingOrder = 1 },
                new BehaviorTreeNodeData { nodeType = "CondHasTarget", parentIndex = 4, siblingOrder = 0 },
                new BehaviorTreeNodeData
                {
                    nodeType = "ActionSetIntentTacticalCombat",
                    parentIndex = 4,
                    siblingOrder = 1,
                    paramFloat1 = 0.12f,
                    paramFloat2 = 0.9f,
                },

                new BehaviorTreeNodeData
                {
                    nodeType = "ActionSetIntentPatrol",
                    parentIndex = 0,
                    siblingOrder = 2,
                    paramFloat1 = 2f,
                    paramFloat2 = 5f,
                },
            };
        }

        private static T CreateOrUpdateAsset<T>(T source, string assetPath) where T : ScriptableObject
        {
            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            source.name = objectName;

            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, assetPath);
                return source;
            }

            EditorUtility.CopySerialized(source, existing);
            existing.name = objectName;
            UnityEngine.Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
