using UnityEngine.UI;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 声音设置面板。
    /// 子控件约定：Slider_BgMusic、Slider_Sound、Btn_Close。
    /// </summary>
    public class SettingsPanel : BasePanel
    {
        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () => UIManager.GetInstance().HidePanel(PanelNames.Settings));

            var bgSlider = GetControl<Slider>("Slider_BgMusic");
            if (bgSlider != null)
            {
                bgSlider.value = 1f;
                bgSlider.onValueChanged.AddListener(v => MusicMgr.GetInstance().ChangeBKValue(v));
            }

            var soundSlider = GetControl<Slider>("Slider_Sound");
            if (soundSlider != null)
            {
                soundSlider.value = 1f;
                soundSlider.onValueChanged.AddListener(v => MusicMgr.GetInstance().ChangeSoundValue(v));
            }
        }

        public override void ShowMe() => gameObject.SetActive(true);
        public override void HideMe() => gameObject.SetActive(false);
    }
}
