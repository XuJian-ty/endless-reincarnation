using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Game.Data
{
    [Serializable]
    public sealed class BossVictoryCutsceneEntry
    {
        [InspectorLabel("Boss ID")]
        [Tooltip("与敌人配置中的 bossId 对应，例如 boss_1。")]
        public string bossId;

        [InspectorLabel("胜利过场视频")]
        [Tooltip("击败该 Boss 后播放的视频。未配置时会直接显示 Boss 结算面板。")]
        public VideoClip cutsceneVideo;
    }

    [CreateAssetMenu(menuName = "游戏/配置/Boss胜利过场动画配置", fileName = "Boss胜利过场动画配置")]
    public class BossVictoryCutsceneConfigSO : ScriptableObject
    {
        [InspectorLabel("Boss 过场配置")]
        public List<BossVictoryCutsceneEntry> entries = new List<BossVictoryCutsceneEntry>();

        public VideoClip GetCutsceneVideo(string bossId)
        {
            string normalizedBossId = string.IsNullOrWhiteSpace(bossId) ? string.Empty : bossId.Trim();
            if (string.IsNullOrWhiteSpace(normalizedBossId) || entries == null)
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                BossVictoryCutsceneEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.bossId))
                    continue;

                if (string.Equals(entry.bossId.Trim(), normalizedBossId, StringComparison.OrdinalIgnoreCase))
                    return entry.cutsceneVideo;
            }

            return null;
        }
    }
}
