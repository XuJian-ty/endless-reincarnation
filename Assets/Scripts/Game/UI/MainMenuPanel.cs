using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using Game.Social;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 主菜单面板（需求：一、开始界面）。
    /// 四个入口：新的游戏、加载存档、声音设置、退出游戏。
    /// 子控件约定：Btn_NewGame（新的游戏）、Btn_LoadGame（加载存档）、Btn_Settings（声音设置）、Btn_Quit（退出游戏）。
    /// 主菜单 BGM 曲目列表从 ScriptableObject 配置 MainMenuBgmTrackListSO（Resources/配置/主菜单BGM曲目列表）读取，主菜单 BGM 仍为全局（PlayerPrefs）。
    /// </summary>
    public class MainMenuPanel : BasePanel
    {
        private LevelGrowthSO _levelGrowth;

        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_NewGame", () =>
            {
                UIManager.GetInstance().HidePanel(PanelNames.MainMenu);
                UIManager.GetInstance().ShowPanel<NamePanel>(
                    PanelNames.NamePanel, PanelLayers.NamePanel,
                    panel => panel.Init(_levelGrowth));
            });
            RegisterClick("Btn_LoadGame", () =>
            {
                UIManager.GetInstance().HidePanel(PanelNames.MainMenu);
                UIManager.GetInstance().ShowPanel<SaveListPanel>(
                    PanelNames.SaveList, PanelLayers.SaveList,
                    panel => panel.Init(_levelGrowth));
            });
            RegisterClick("Btn_Settings", () =>
            {
                UIManager.GetInstance().HidePanel(PanelNames.MainMenu);
                UIManager.GetInstance().ShowPanel<MainMenuSoundPanel>(PanelNames.MainMenuSound, PanelLayers.MainMenuSound);
            });
            RegisterClick("Btn_Quit", () =>
            {
                UIManager.GetInstance().HidePanel(PanelNames.MainMenu);
                SocialSession.GetInstance().Logout();
            });
        }

        public void Init(LevelGrowthSO levelGrowth)
        {
            _levelGrowth = levelGrowth;
        }

        public override void ShowMe() => gameObject.SetActive(true);

        public override void HideMe() => gameObject.SetActive(false);
    }
}
