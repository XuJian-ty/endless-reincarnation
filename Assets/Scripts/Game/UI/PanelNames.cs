using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 面板名常量，与 Resources/UI/ 下预制体名、UIManager.ShowPanel/HidePanel 参数一致。
    /// 集中管理避免魔法字符串与拼写错误。
    /// </summary>
    public static class PanelNames
    {
        public const string Backpack     = "BackpackPanel";
        public const string KeyConfig    = "KeyConfigPanel";
        public const string Menu         = "MenuPanel";
        public const string SkillTree    = "SkillTreePanel";
        public const string Settings     = "SettingsPanel";
        /// <summary>主菜单场景用，仅调节主界面 BGM 音量</summary>
        public const string MainMenuSound = "MainMenuSoundPanel";
        /// <summary>关卡内菜单用，仅调节关卡内 BGM 音量（按存档保存）</summary>
        public const string LevelSound    = "LevelSoundPanel";
        public const string BattleMemory = "BattleMemoryPanel";
        public const string NamePanel    = "NamePanel";
        public const string SaveList    = "SaveListPanel";
        public const string MainMenu    = "MainMenuPanel";
        /// <summary>主界面背景（主菜单场景底层全屏图，与 MainMenuPanel 同由 UIManager 加载，放在 Bot 层）</summary>
        public const string MainMenuBackground = "MainMenuBackground";
        public const string Loading      = "LoadingPanel";
        public const string BuffSelect   = "BuffSelectPanel";
        public const string PlayerInfoHUD = "PlayerInfoHUDPanel";

        /// <summary>
        /// 打开后应阻挡关卡内操作、显示光标、暂停游戏时间、阻止其它面板打开的面板。
        /// </summary>
        public static readonly string[] BlockingPanels =
        {
            Menu,
            KeyConfig,
            Backpack,
            SkillTree,
            BuffSelect,
            LevelSound,
            BattleMemory,
        };
    }

    /// <summary>
    /// 关卡内面板的默认显示层级，与 PanelNames 对应使用。
    /// </summary>
    public static class PanelLayers
    {
        public const E_UI_Layer Backpack     = E_UI_Layer.Mid;
        public const E_UI_Layer KeyConfig    = E_UI_Layer.Mid;
        public const E_UI_Layer Menu         = E_UI_Layer.Top;
        public const E_UI_Layer SkillTree    = E_UI_Layer.Mid;
        public const E_UI_Layer Settings     = E_UI_Layer.Top;
        public const E_UI_Layer MainMenuSound = E_UI_Layer.Top;
        public const E_UI_Layer LevelSound    = E_UI_Layer.Top;
        public const E_UI_Layer BattleMemory = E_UI_Layer.Mid;
        public const E_UI_Layer NamePanel    = E_UI_Layer.Mid;
        public const E_UI_Layer SaveList     = E_UI_Layer.Mid;
        public const E_UI_Layer MainMenu    = E_UI_Layer.Mid;
        public const E_UI_Layer MainMenuBackground = E_UI_Layer.Bot;
        public const E_UI_Layer Loading     = E_UI_Layer.System;
        public const E_UI_Layer BuffSelect  = E_UI_Layer.System;
        public const E_UI_Layer PlayerInfoHUD = E_UI_Layer.Top;
    }
}
