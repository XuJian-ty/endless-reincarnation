using UnityEngine;
using UnityEngine.UI;
using Game.GameFlow;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 关卡内菜单面板（按 Esc 打开，需求十九）。预制体放 Resources/UI/MenuPanel。
    /// 子控件约定：Btn_Resume（继续游戏）、Btn_Quit（退出游戏，保存并返回主菜单）、Btn_Settings（声音设置）、Btn_BattleMemory（战斗回忆，仅通关后显示）。
    /// </summary>
    public class MenuPanel : BasePanel
    {
        protected override void Awake()
        {
            base.Awake();
            void CloseMenu()
            {
                UIManager.GetInstance().HidePanel(PanelNames.Menu);
            }
            RegisterClick("Btn_Close", CloseMenu);
            RegisterClick("Btn_Resume", CloseMenu);
            RegisterClick("Btn_Settings", () => UIManager.GetInstance().ShowPanel<LevelSoundPanel>(PanelNames.LevelSound, PanelLayers.LevelSound));
            RegisterClick("Btn_Quit", () =>
            {
                var boot = FindFirstObjectByType<LevelBootstrapper>();
                if (boot != null)
                    boot.SaveAndQuit();
                else
                    GameStateMachine.GetInstance().SaveAndQuit();
                UIManager.GetInstance().HidePanel(PanelNames.Menu);
            });
            RegisterClick("Btn_BattleMemory", () => UIManager.GetInstance().ShowPanel<BattleMemoryPanel>(PanelNames.BattleMemory, PanelLayers.BattleMemory));
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            base.ShowMe();
            RefreshBattleMemoryButton();
        }

        public override void HideMe() => gameObject.SetActive(false);

        private void RefreshBattleMemoryButton()
        {
            var btn = GetControl<Button>("Btn_BattleMemory");
            if (btn == null) return;
            var run = LevelUIModelLocator.Get()?.CurrentRun ?? GameStateMachine.GetInstance()?.CurrentRun;
            btn.gameObject.SetActive(run != null && run.isGameCleared);
        }
    }
}
