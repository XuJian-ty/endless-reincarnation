using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using Game.GameFlow;
using Game.Saving;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 关卡内声音设置面板：五项功能——（1）复选框开关关卡 BGM（2）滑块调 BGM 音量（3）下拉切换 BGM 曲目（4）复选框开关音效（5）滑块调音效音量。按当前存档保存（JSON）。
    /// 子控件约定：Toggle_Bgm、Slider_BgMusic、Dropdown_BgmTrack、Toggle_SoundEffects、Slider_SoundEffects、Btn_Close。
    /// 曲目列表从 ScriptableObject 配置 LevelBgmTrackListSO（Resources/Config/LevelBgmTracks）读取。
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
            var run = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
            if (run == null) return;

            var toggleBgm = GetControl<Toggle>("Toggle_Bgm");
            var sliderBgm = GetControl<Slider>("Slider_BgMusic");
            var dropdownBgm = GetControl<Dropdown>("Dropdown_BgmTrack");
            var toggleSfx = GetControl<Toggle>("Toggle_SoundEffects");
            var sliderSfx = GetControl<Slider>("Slider_SoundEffects");

            if (toggleBgm != null) toggleBgm.isOn = run.levelBgmEnabled;
            if (sliderBgm != null) sliderBgm.value = Mathf.Clamp01(run.levelBgmVolume);
            if (dropdownBgm != null)
            {
                var displayNames = LevelBgmTrackListSO.GetDisplayNames();
                dropdownBgm.ClearOptions();
                dropdownBgm.AddOptions(displayNames);
                int n = displayNames.Count;
                dropdownBgm.SetValueWithoutNotify(n > 0 ? Mathf.Clamp(run.levelBgmTrackIndex, 0, n - 1) : 0);
                dropdownBgm.RefreshShownValue();
            }
            if (toggleSfx != null) toggleSfx.isOn = run.soundEffectsEnabled;
            if (sliderSfx != null) sliderSfx.value = Mathf.Clamp01(run.soundEffectsVolume);
        }

        private void ApplyToRunAndSave(System.Action<RunData> set)
        {
            var gsm = GameStateMachine.GetInstance();
            if (gsm?.CurrentRun == null || string.IsNullOrEmpty(gsm.CurrentSaveId)) return;
            set(gsm.CurrentRun);
            gsm.SaveCurrent();
        }

        private void OnBgmToggleChanged(bool isOn)
        {
            ApplyToRunAndSave(run => run.levelBgmEnabled = isOn);
            var run = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
            float vol = isOn ? (run?.levelBgmVolume ?? 1f) : 0f;
            MusicMgr.GetInstance().ChangeBKValue(Mathf.Clamp01(vol));
            if (isOn)
            {
                int trackCount = LevelBgmTrackListSO.GetTrackCount();
                if (trackCount > 0)
                {
                    var r = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
                    int idx = Mathf.Clamp(r?.levelBgmTrackIndex ?? 0, 0, trackCount - 1);
                    var clip = LevelBgmTrackListSO.GetClip(idx);
                    if (clip != null) MusicMgr.GetInstance().PlayBkMusic(clip);
                }
            }
        }

        private void OnBgmVolumeChanged(float v)
        {
            ApplyToRunAndSave(run => run.levelBgmVolume = Mathf.Clamp01(v));
            var run = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
            if (run?.levelBgmEnabled == true)
                MusicMgr.GetInstance().ChangeBKValue(v);
        }

        private void OnBgmTrackChanged(int index)
        {
            ApplyToRunAndSave(run => run.levelBgmTrackIndex = Mathf.Clamp(index, 0, 999));
            var run = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
            if (run?.levelBgmEnabled == true)
            {
                var clip = LevelBgmTrackListSO.GetClip(index);
                if (clip != null) MusicMgr.GetInstance().PlayBkMusic(clip);
            }
        }

        private void OnSoundEffectsToggleChanged(bool isOn)
        {
            ApplyToRunAndSave(run => run.soundEffectsEnabled = isOn);
            var run = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
            float vol = isOn ? (run?.soundEffectsVolume ?? 1f) : 0f;
            MusicMgr.GetInstance().ChangeSoundValue(Mathf.Clamp01(vol));
        }

        private void OnSoundEffectsVolumeChanged(float v)
        {
            ApplyToRunAndSave(run => run.soundEffectsVolume = Mathf.Clamp01(v));
            var run = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
            if (run?.soundEffectsEnabled == true)
                MusicMgr.GetInstance().ChangeSoundValue(v);
        }
    }
}
