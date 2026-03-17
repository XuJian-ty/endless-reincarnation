using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.Editor
{
    public static class CreateBgmTrackListAssets
    {
        private const string ResourcesRoot = "Assets/Resources";
        private const string ConfigFolder = "Assets/Resources/配置";

        private const string MainMenuAssetPath = "Assets/Resources/配置/主菜单BGM曲目列表.asset";
        private const string LevelAssetPath = "Assets/Resources/配置/关卡BGM曲目列表.asset";

        public static void Create()
        {
            EnsureFolders();

            CreateIfMissing(
                MainMenuAssetPath,
                () => ScriptableObject.CreateInstance<MainMenuBgmTrackListSO>());

            CreateIfMissing(
                LevelAssetPath,
                () => ScriptableObject.CreateInstance<LevelBgmTrackListSO>());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CreateBgmTrackListAssets] 已确保 Resources/配置/主菜单BGM曲目列表.asset 与 关卡BGM曲目列表.asset 存在。");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder(ResourcesRoot, "配置");
        }

        private static void CreateIfMissing<T>(string assetPath, System.Func<T> factory)
            where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(assetPath) != null)
                return;

            var asset = factory();
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
        }
    }
}
