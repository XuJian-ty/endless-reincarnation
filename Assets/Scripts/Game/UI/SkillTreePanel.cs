using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 技能树面板（占位）。预制体放 Resources/UI/SkillTreePanel，子控件与逻辑由你后续实现。
    /// </summary>
    public class SkillTreePanel : BasePanel
    {
        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () => UIManager.GetInstance().HidePanel(PanelNames.SkillTree));
        }

        public override void ShowMe() => gameObject.SetActive(true);
        public override void HideMe() => gameObject.SetActive(false);
    }
}
