using UnityEngine;
using Game.Data;
using Game.Social;
using ProjectBase;
using Game;

namespace Game.UI
{
    /// <summary>
    /// 主界面背景面板。进入游戏时只显示本面板（背景 +「点击任意键继续...」提示），
    /// 用户按下任意键后隐藏提示并显示主菜单面板。挂在 Resources/UI/MainMenuBackground 预制体根物体上。
    /// 主菜单 BGM 在 ShowMe 时即应用，进入游戏后第一时间播放，无需等点击。
    /// </summary>
    public class MainMenuBackgroundPanel : BasePanel
    {
        [SerializeField] private GameObject promptClickToContinue;

        private static bool _useLoadingTransitionForNextShow;
        private bool _mainMenuShown;
        private bool _sessionSubscribed;
        private int _promptAvailableFrame = -1;

        /// <summary>
        /// 将下一次 ShowMe 设为“加载过渡模式”：
        /// 不显示「点击任意键继续...」，也不播放主菜单 BGM。
        /// </summary>
        public static void RequestLoadingTransitionShow()
        {
            _useLoadingTransitionForNextShow = true;
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            SetSessionSubscription(true);

            bool isLoadingTransition = _useLoadingTransitionForNextShow;
            _useLoadingTransitionForNextShow = false;

            if (!isLoadingTransition)
                ApplyMainMenuBgm();

            if (isLoadingTransition)
            {
                if (promptClickToContinue != null)
                    promptClickToContinue.SetActive(false);
                _mainMenuShown = true;
                return;
            }

            _mainMenuShown = false;
            RefreshEntryFlow();
        }

        public override void HideMe()
        {
            SetSessionSubscription(false);
            gameObject.SetActive(false);
        }

        private static void ApplyMainMenuBgm()
        {
            bool enabled = PlayerPrefs.GetInt("MenuBgmEnabled", 1) != 0;
            float vol = PlayerPrefs.GetFloat("MenuBgmVolume", 1f);
            MusicMgr.GetInstance().ChangeBKValue(enabled ? vol : 0f);
            int trackCount = MainMenuBgmTrackListSO.GetTrackCount();
            if (enabled && trackCount > 0)
            {
                int idx = Mathf.Clamp(PlayerPrefs.GetInt("MenuBgmTrackIndex", 0), 0, trackCount - 1);
                var clip = MainMenuBgmTrackListSO.GetClip(idx);
                if (clip != null)
                    MusicMgr.GetInstance().PlayBkMusic(clip);
            }
        }

        private void Update()
        {
            if (_mainMenuShown) return;
            if (promptClickToContinue != null && !promptClickToContinue.activeSelf) return;
            if (Time.frameCount <= _promptAvailableFrame) return;
            if (!UnityEngine.Input.anyKeyDown && !UnityEngine.Input.GetMouseButtonDown(0)) return;

            _mainMenuShown = true;
            if (promptClickToContinue != null)
                promptClickToContinue.SetActive(false);

            var levelGrowth = ConfigManager.GetInstance().GetLevelGrowth();
            UIManager.GetInstance().ShowPanel<MainMenuPanel>(
                PanelNames.MainMenu,
                PanelLayers.MainMenu,
                panel => panel.Init(levelGrowth));
        }

        private void RefreshEntryFlow()
        {
            if (SocialSession.GetInstance().IsLoggedIn)
            {
                UIManager.GetInstance().HidePanel(PanelNames.AccountLogin);
                if (promptClickToContinue != null)
                {
                    promptClickToContinue.SetActive(!_mainMenuShown);
                    if (!_mainMenuShown)
                        _promptAvailableFrame = Time.frameCount;
                }
                return;
            }

            _mainMenuShown = false;
            if (promptClickToContinue != null)
                promptClickToContinue.SetActive(false);

            UIManager.GetInstance().HidePanel(PanelNames.MainMenu);
            UIManager.GetInstance().HidePanel(PanelNames.NamePanel);
            UIManager.GetInstance().HidePanel(PanelNames.SaveList);
            UIManager.GetInstance().HidePanel(PanelNames.SocialHub);
            UIManager.GetInstance().ShowPanel<AccountLoginPanel>(
                PanelNames.AccountLogin,
                PanelLayers.AccountLogin);
        }

        private void HandleSessionChanged()
        {
            if (!gameObject.activeInHierarchy)
                return;

            RefreshEntryFlow();
        }

        private void SetSessionSubscription(bool subscribe)
        {
            if (subscribe)
            {
                if (_sessionSubscribed)
                    return;

                SocialSession.GetInstance().SessionChanged += HandleSessionChanged;
                _sessionSubscribed = true;
                return;
            }

            if (!_sessionSubscribed)
                return;

            SocialSession.GetInstance().SessionChanged -= HandleSessionChanged;
            _sessionSubscribed = false;
        }

        private void OnDestroy()
        {
            SetSessionSubscription(false);
        }
    }
}
