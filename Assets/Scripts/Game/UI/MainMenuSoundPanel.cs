using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 主菜单声音设置面板：三项功能——（1）复选框开关主界面 BGM（2）滑块调 BGM 音量（3）下拉切换 BGM 曲目。
    /// 子控件约定：Toggle_Bgm、Slider_BgMusic、Dropdown_BgmTrack、Btn_Close。数据存 PlayerPrefs（全局）。
    /// 曲目列表从 ScriptableObject 配置 MainMenuBgmTrackListSO（Resources/配置/主菜单BGM曲目列表）读取。
    /// </summary>
    public class MainMenuSoundPanel : BasePanel
    {
        private const string PrefsEnabled = "MenuBgmEnabled";
        private const string PrefsVolume = "MenuBgmVolume";
        private const string PrefsTrackIndex = "MenuBgmTrackIndex";

        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () =>
            {
                UIManager.GetInstance().HidePanel(PanelNames.MainMenuSound);
                UIManager.GetInstance().ShowPanel<MainMenuPanel>(PanelNames.MainMenu, PanelLayers.MainMenu);
            });

            var toggle = GetControl<Toggle>("Toggle_Bgm");
            var slider = GetControl<Slider>("Slider_BgMusic");
            var dropdownBgm = GetControl<Dropdown>("Dropdown_BgmTrack");

            if (toggle != null)
            {
                toggle.isOn = PlayerPrefs.GetInt(PrefsEnabled, 1) != 0;
                toggle.onValueChanged.AddListener(OnBgmToggleChanged);
            }
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = PlayerPrefs.GetFloat(PrefsVolume, 1f);
                slider.onValueChanged.AddListener(OnVolumeChanged);
            }
            if (dropdownBgm != null)
            {
                var displayNames = MainMenuBgmTrackListSO.GetDisplayNames();
                dropdownBgm.ClearOptions();
                dropdownBgm.AddOptions(displayNames);
                int count = displayNames.Count;
                dropdownBgm.value = count > 0 ? Mathf.Clamp(PlayerPrefs.GetInt(PrefsTrackIndex, 0), 0, count - 1) : 0;
                dropdownBgm.RefreshShownValue();
                dropdownBgm.onValueChanged.AddListener(OnTrackChanged);
            }
        }

        private void ApplyMainMenuBgmState()
        {
            bool enabled = PlayerPrefs.GetInt(PrefsEnabled, 1) != 0;
            float vol = PlayerPrefs.GetFloat(PrefsVolume, 1f);
            int index = PlayerPrefs.GetInt(PrefsTrackIndex, 0);
            MusicMgr.GetInstance().ChangeBKValue(enabled ? vol : 0f);
            int trackCount = MainMenuBgmTrackListSO.GetTrackCount();
            if (enabled && trackCount > 0)
            {
                index = Mathf.Clamp(index, 0, trackCount - 1);
                var clip = MainMenuBgmTrackListSO.GetClip(index);
                if (clip != null) MusicMgr.GetInstance().PlayBkMusic(clip);
            }
        }

        private void OnBgmToggleChanged(bool isOn)
        {
            PlayerPrefs.SetInt(PrefsEnabled, isOn ? 1 : 0);
            PlayerPrefs.Save();
            float vol = isOn ? PlayerPrefs.GetFloat(PrefsVolume, 1f) : 0f;
            MusicMgr.GetInstance().ChangeBKValue(vol);
            if (isOn)
            {
                int trackCount = MainMenuBgmTrackListSO.GetTrackCount();
                if (trackCount > 0)
                {
                    int index = Mathf.Clamp(PlayerPrefs.GetInt(PrefsTrackIndex, 0), 0, trackCount - 1);
                    var clip = MainMenuBgmTrackListSO.GetClip(index);
                    if (clip != null) MusicMgr.GetInstance().PlayBkMusic(clip);
                }
            }
        }

        private void OnVolumeChanged(float v)
        {
            PlayerPrefs.SetFloat(PrefsVolume, v);
            PlayerPrefs.Save();
            if (PlayerPrefs.GetInt(PrefsEnabled, 1) != 0)
                MusicMgr.GetInstance().ChangeBKValue(v);
        }

        private void OnTrackChanged(int index)
        {
            PlayerPrefs.SetInt(PrefsTrackIndex, index);
            PlayerPrefs.Save();
            if (PlayerPrefs.GetInt(PrefsEnabled, 1) != 0)
            {
                var clip = MainMenuBgmTrackListSO.GetClip(index);
                if (clip != null) MusicMgr.GetInstance().PlayBkMusic(clip);
            }
        }

        public override void ShowMe() => gameObject.SetActive(true);
        public override void HideMe() => gameObject.SetActive(false);
    }
}
