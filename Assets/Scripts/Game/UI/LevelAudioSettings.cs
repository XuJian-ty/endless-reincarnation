using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 关卡内音频设置：按当前设备本地保存，不参与存档同步。
    /// 这样联机时每个玩家都保留自己的本机音量与曲目偏好。
    /// </summary>
    public static class LevelAudioSettings
    {
        private const string BgmEnabledKey = "LevelBgmEnabled";
        private const string BgmVolumeKey = "LevelBgmVolume";
        private const string BgmTrackIndexKey = "LevelBgmTrackIndex";
        private const string SoundEffectsEnabledKey = "LevelSoundEffectsEnabled";
        private const string SoundEffectsVolumeKey = "LevelSoundEffectsVolume";

        public static bool BgmEnabled
        {
            get => PlayerPrefs.GetInt(BgmEnabledKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(BgmEnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float BgmVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
            set
            {
                PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static int BgmTrackIndex
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(BgmTrackIndexKey, 0));
            set
            {
                PlayerPrefs.SetInt(BgmTrackIndexKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static bool SoundEffectsEnabled
        {
            get => PlayerPrefs.GetInt(SoundEffectsEnabledKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(SoundEffectsEnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float SoundEffectsVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SoundEffectsVolumeKey, 1f));
            set
            {
                PlayerPrefs.SetFloat(SoundEffectsVolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }
    }
}
