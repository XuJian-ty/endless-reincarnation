using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal sealed class EnemyArchetypeAssetModificationProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            EnemyArchetypeSO archetype = AssetDatabase.LoadAssetAtPath<EnemyArchetypeSO>(assetPath);
            if (archetype == null)
                return AssetDeleteResult.DidNotDelete;

            if (!EnemySkillAuthoringUtility.TryCleanupDeletedEnemyArchetypeArtifacts(archetype, out string errorMessage))
                Debug.LogError($"[EnemyArchetypeAssetModificationProcessor] 删除敌人行为资产时同步清理失败：{assetPath}\n{errorMessage}");

            AssetDatabase.SaveAssets();
            return AssetDeleteResult.DidNotDelete;
        }
    }
}
