using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 关卡内声音设置面板：五项功能——（1）复选框开关关卡 BGM（2）滑块调 BGM 音量（3）下拉切换 BGM 曲目（4）复选框开关音效（5）滑块调音效音量。按当前设备本地保存。
    /// 子控件约定：Toggle_Bgm、Slider_BgMusic、Dropdown_BgmTrack、Toggle_SoundEffects、Slider_SoundEffects、Btn_Close。
    /// 曲目列表从 ScriptableObject 配置 LevelBgmTrackListSO（Resources/配置/关卡BGM曲目列表）读取。
    /// </summary>
    public class LevelSoundPanel : BasePanel
    {
        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () => UIManager.GetInstance().HidePanel(PanelNames.LevelSound));

            var toggleBgm = GetControl<Toggle>("Toggle_Bgm");
            var sliderBgm = GetControl<Slider>("Slider_BgMusic");
            var dropdownBgm = GetControl<Dropdown>("Dropdown_BgmTrack");
            var toggleSfx = GetControl<Toggle>("Toggle_SoundEffects");
            var sliderSfx = GetControl<Slider>("Slider_SoundEffects");

            if (toggleBgm != null) toggleBgm.onValueChanged.AddListener(OnBgmToggleChanged);
            if (sliderBgm != null)
            {
                sliderBgm.minValue = 0f;
                sliderBgm.maxValue = 1f;
                sliderBgm.onValueChanged.AddListener(OnBgmVolumeChanged);
            }
            if (dropdownBgm != null)
            {
                var displayNames = LevelBgmTrackListSO.GetDisplayNames();
                dropdownBgm.ClearOptions();
                dropdownBgm.AddOptions(displayNames);
                dropdownBgm.onValueChanged.AddListener(OnBgmTrackChanged);
            }
            if (toggleSfx != null) toggleSfx.onValueChanged.AddListener(OnSoundEffectsToggleChanged);
            if (sliderSfx != null)
            {
                sliderSfx.minValue = 0f;
                sliderSfx.maxValue = 1f;
                sliderSfx.onValueChanged.AddListener(OnSoundEffectsVolumeChanged);
            }
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            var toggleBgm = GetControl<Toggle>("Toggle_Bgm");
            var sliderBgm = GetControl<Slider>("Slider_BgMusic");
            var dropdownBgm = GetControl<Dropdown>("Dropdown_BgmTrack");
            var toggleSfx = GetControl<Toggle>("Toggle_SoundEffects");
            var sliderSfx = GetControl<Slider>("Slider_SoundEffects");

            if (toggleBgm != null) toggleBgm.isOn = LevelAudioSettings.BgmEnabled;
            if (sliderBgm != null) sliderBgm.value = Mathf.Clamp01(LevelAudioSettings.BgmVolume);
            if (dropdownBgm != null)
            {
                var displayNames = LevelBgmTrackListSO.GetDisplayNames();
                dropdownBgm.ClearOptions();
                dropdownBgm.AddOptions(displayNames);
                int n = displayNames.Count;
                dropdownBgm.SetValueWithoutNotify(n > 0 ? Mathf.Clamp(LevelAudioSettings.BgmTrackIndex, 0, n - 1) : 0);
                dropdownBgm.RefreshShownValue();
            }
            if (toggleSfx != null) toggleSfx.isOn = LevelAudioSettings.SoundEffectsEnabled;
            if (sliderSfx != null) sliderSfx.value = Mathf.Clamp01(LevelAudioSettings.SoundEffectsVolume);
        }

        private void OnBgmToggleChanged(bool isOn)
        {
            LevelAudioSettings.BgmEnabled = isOn;
            float vol = isOn ? LevelAudioSettings.BgmVolume : 0f;
            MusicMgr.GetInstance().ChangeBKValue(Mathf.Clamp01(vol));
            if (isOn)
            {
                int trackCount = LevelBgmTrackListSO.GetTrackCount();
                if (trackCount > 0)
                {
                    int idx = Mathf.Clamp(LevelAudioSettings.BgmTrackIndex, 0, trackCount - 1);
                    var clip = LevelBgmTrackListSO.GetClip(idx);
                    if (clip != null) MusicMgr.GetInstance().PlayBkMusic(clip);
                }
            }
        }

        private void OnBgmVolumeChanged(float v)
        {
            LevelAudioSettings.BgmVolume = Mathf.Clamp01(v);
            if (LevelAudioSettings.BgmEnabled)
                MusicMgr.GetInstance().ChangeBKValue(v);
        }

        private void OnBgmTrackChanged(int index)
        {
            LevelAudioSettings.BgmTrackIndex = Mathf.Clamp(index, 0, 999);
            if (LevelAudioSettings.BgmEnabled)
            {
                var clip = LevelBgmTrackListSO.GetClip(index);
                if (clip != null) MusicMgr.GetInstance().PlayBkMusic(clip);
            }
        }

        private void OnSoundEffectsToggleChanged(bool isOn)
        {
            LevelAudioSettings.SoundEffectsEnabled = isOn;
            float vol = isOn ? LevelAudioSettings.SoundEffectsVolume : 0f;
            MusicMgr.GetInstance().ChangeSoundValue(Mathf.Clamp01(vol));
        }

        private void OnSoundEffectsVolumeChanged(float v)
        {
            LevelAudioSettings.SoundEffectsVolume = Mathf.Clamp01(v);
            if (LevelAudioSettings.SoundEffectsEnabled)
                MusicMgr.GetInstance().ChangeSoundValue(v);
        }
    }
}
