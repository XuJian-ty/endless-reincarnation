using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 主菜单 BGM 曲目列表配置（ScriptableObject）。每项含曲目名称（下拉显示）+ AudioClip。主菜单 BGM 仍为全局设置，存 PlayerPrefs。
    /// 按中文文件名加载，须放在 Resources/配置/主菜单BGM曲目列表。默认背景音乐为配置里第一项。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/主菜单BGM曲目列表", fileName = "主菜单BGM曲目列表")]
    public class MainMenuBgmTrackListSO : ScriptableObject
    {
        [InspectorLabel("曲目列表")]
        [Tooltip("每项配置「曲目名称」（下拉框显示）和「音频」；第一项为默认背景音乐")]
        public List<BgmTrackEntry> tracks = new List<BgmTrackEntry>();

        private const string ResourcesPath = "配置/主菜单BGM曲目列表";
        private static MainMenuBgmTrackListSO Load()
        {
            return Resources.Load<MainMenuBgmTrackListSO>(ResourcesPath);
        }

        /// <summary>曲目数量。</summary>
        public static int GetTrackCount()
        {
            var so = Load();
            return so != null && so.tracks != null ? so.tracks.Count : 0;
        }

        /// <summary>根据索引取显示名；越界或空返回「曲目N」。</summary>
        public static string GetDisplayName(int index)
        {
            var so = Load();
            if (so == null || so.tracks == null || index < 0 || index >= so.tracks.Count)
                return "曲目" + (index + 1);
            var entry = so.tracks[index];
            return !string.IsNullOrWhiteSpace(entry.displayName) ? entry.displayName : "曲目" + (index + 1);
        }

        /// <summary>下拉框选项文案列表（与配置顺序一致）。</summary>
        public static List<string> GetDisplayNames()
        {
            var list = new List<string>();
            int n = GetTrackCount();
            for (int i = 0; i < n; i++)
                list.Add(GetDisplayName(i));
            return list;
        }

        /// <summary>根据索引取 AudioClip；越界或为空返回 null。</summary>
        public static AudioClip GetClip(int index)
        {
            var so = Load();
            if (so == null || so.tracks == null || index < 0 || index >= so.tracks.Count)
                return null;
            var entry = so.tracks[index];
            return entry != null ? entry.clip : null;
        }
    }
}
