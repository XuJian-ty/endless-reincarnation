using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Data;
using Game.Saving;
using Game.GameFlow;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 起名面板：新的游戏时弹出，输入玩家名字后确认则创建新存档并异步加载关卡一。
    /// 子控件约定：Input_PlayerName（InputField）、Btn_Confirm（确认）、Btn_Close（关闭）。
    /// </summary>
    public class NamePanel : BasePanel
    {
        private LevelGrowthSO _levelGrowth;

        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () =>
            {
                UIManager.GetInstance().HidePanel(PanelNames.NamePanel);
                UIManager.GetInstance().ShowPanel<MainMenuPanel>(
                    PanelNames.MainMenu, PanelLayers.MainMenu,
                    panel => panel.Init(_levelGrowth));
            });
            RegisterClick("Btn_Confirm", OnConfirm);
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;
            var k = Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
                OnConfirm();
        }

        public void Init(LevelGrowthSO levelGrowth)
        {
            _levelGrowth = levelGrowth;
            var input = GetControl<InputField>("Input_PlayerName");
            if (input != null) input.text = "";
        }

        private void OnConfirm()
        {
            var input = GetControl<InputField>("Input_PlayerName");
            string name = input != null ? input.text : "";
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.Log("[NamePanel] 请输入玩家名字");
                return;
            }

            string saveId = SaveSystem.GetInstance().CreateNewSave(name);
            UIManager.GetInstance().HidePanel(PanelNames.NamePanel);
            UIManager.GetInstance().HidePanel(PanelNames.MainMenu);
            // 不隐藏 MainMenuBackground，加载时保留主界面背景 + LoadingPanel
            UIManager.GetInstance().ShowPanel<LoadingPanel>(PanelNames.Loading, PanelLayers.Loading, _ =>
            {
                GameStateMachine.GetInstance().LoadGame(saveId, _levelGrowth, () =>
                {
                    UIManager.GetInstance().HidePanel(PanelNames.Loading);
                    UIManager.GetInstance().HidePanel(PanelNames.MainMenuBackground);
                });
            });
        }

        public override void ShowMe() => gameObject.SetActive(true);
        public override void HideMe() => gameObject.SetActive(false);
    }
}
