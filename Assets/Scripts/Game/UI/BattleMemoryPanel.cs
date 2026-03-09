using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 战斗回忆面板（需求五、十九）：通关后从关卡内菜单进入，选择要重新挑战的 Boss，纯挑战、无奖励。
    /// 预制体放 Resources/UI/BattleMemoryPanel。当前为占位，仅有关闭；后续实现 Boss 列表与选关逻辑。
    /// </summary>
    public class BattleMemoryPanel : BasePanel
    {
        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () => UIManager.GetInstance().HidePanel(PanelNames.BattleMemory));
        }

        public override void ShowMe() => gameObject.SetActive(true);
        public override void HideMe() => gameObject.SetActive(false);
    }
}
