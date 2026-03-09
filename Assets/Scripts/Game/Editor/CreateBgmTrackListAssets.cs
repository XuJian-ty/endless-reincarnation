using System.Collections.Generic;
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

        private const string LegacyMainMenuAssetPath = "Assets/Resources/Config/MainMenuBgmTracks.asset";
        private const string LegacyLevelAssetPath = "Assets/Resources/Config/LevelBgmTracks.asset";

        public static void Create()
        {
            EnsureFolders();

            CreateIfMissingFromLegacy(
                MainMenuAssetPath,
                LegacyMainMenuAssetPath,
                () => ScriptableObject.CreateInstance<MainMenuBgmTrackListSO>());

            CreateIfMissingFromLegacy(
                LevelAssetPath,
                LegacyLevelAssetPath,
                () => ScriptableObject.CreateInstance<LevelBgmTrackListSO>());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CreateBgmTrackListAssets] 已确保 Resources/配置/主菜单BGM曲目列表.asset 与 关卡BGM曲目列表.asset 存在。若检测到旧版英文路径资产，会自动迁移其曲目列表。");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder(ResourcesRoot, "配置");
        }

        private static void CreateIfMissingFromLegacy<T>(string assetPath, string legacyAssetPath, System.Func<T> factory)
            where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(assetPath) != null)
                return;

            var asset = factory();
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            CopyTracksFromLegacy(asset, AssetDatabase.LoadAssetAtPath<T>(legacyAssetPath));
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        private static void CopyTracksFromLegacy(MainMenuBgmTrackListSO target, MainMenuBgmTrackListSO legacy)
        {
            target.tracks = CloneTracks(legacy != null ? legacy.tracks : null);
        }

        private static void CopyTracksFromLegacy(LevelBgmTrackListSO target, LevelBgmTrackListSO legacy)
        {
            target.tracks = CloneTracks(legacy != null ? legacy.tracks : null);
        }

        private static void CopyTracksFromLegacy<T>(T target, T legacy) where T : ScriptableObject
        {
            switch (target)
            {
                case MainMenuBgmTrackListSO mainMenuTarget:
                    CopyTracksFromLegacy(mainMenuTarget, legacy as MainMenuBgmTrackListSO);
                    break;
                case LevelBgmTrackListSO levelTarget:
                    CopyTracksFromLegacy(levelTarget, legacy as LevelBgmTrackListSO);
                    break;
            }
        }

        private static List<BgmTrackEntry> CloneTracks(List<BgmTrackEntry> source)
        {
            var result = new List<BgmTrackEntry>();
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null)
                    continue;

                result.Add(new BgmTrackEntry
                {
                    displayName = entry.displayName,
                    clip = entry.clip
                });
            }

            return result;
        }
    }
}
