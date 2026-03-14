using System;
using UnityEngine;
using UnityEngine.UI;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 死亡二选一面板：仙露复活 / 结束本局。
    /// 若缺少预制体子节点，会自动构建最小可用运行时 UI。
    /// </summary>
    public class DeathChoicePanel : BasePanel
    {
        private Action _onRevive;
        private Action _onGiveUp;
        private Text _titleText;
        private Text _messageText;
        private Text _detailText;
        private Button _reviveButton;
        private Text _reviveButtonText;
        private Button _giveUpButton;
        private string _detailPrefix;

        protected override void Awake()
        {
            EnsureRuntimeUi();
            base.Awake();

            RegisterClick("Btn_Revive", OnReviveClicked);
            RegisterClick("Btn_GiveUp", OnGiveUpClicked);

            _titleText = GetControl<Text>("Txt_Title");
            _messageText = GetControl<Text>("Txt_Message");
            _detailText = GetControl<Text>("Txt_Detail");
            _reviveButton = GetControl<Button>("Btn_Revive");
            _giveUpButton = GetControl<Button>("Btn_GiveUp");
            _reviveButtonText = _reviveButton != null ? _reviveButton.GetComponentInChildren<Text>(true) : null;
            _detailPrefix = _detailText != null ? _detailText.text : string.Empty;
        }

        public void ShowChoice(bool canRevive, int nectarCount, Action onRevive, Action onGiveUp)
        {
            _onRevive = onRevive;
            _onGiveUp = onGiveUp;

            if (_detailText != null)
                _detailText.text = $"{_detailPrefix}{Mathf.Max(0, nectarCount)}";

            if (_reviveButton != null)
                _reviveButton.interactable = canRevive;

            gameObject.SetActive(true);
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        private void OnReviveClicked()
        {
            if (_reviveButton != null && !_reviveButton.interactable)
                return;

            UIManager.GetInstance().HidePanel(PanelNames.DeathChoice);
            _onRevive?.Invoke();
        }

        private void OnGiveUpClicked()
        {
            UIManager.GetInstance().HidePanel(PanelNames.DeathChoice);
            _onGiveUp?.Invoke();
        }

        private void EnsureRuntimeUi()
        {
            if (transform.Find("Window") != null)
                return;

            RectTransform rootRect = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(rootRect);
            RuntimeOverlayPanelBuilder.EnsureOverlayBackground(gameObject, new Color(0f, 0f, 0f, 0.75f));

            RectTransform window = RuntimeOverlayPanelBuilder.CreateWindow(
                transform,
                "Window",
                new Vector2(620f, 340f),
                new Color(0.12f, 0.11f, 0.09f, 0.96f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Title",
                "你已倒下",
                34,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 108f),
                new Vector2(520f, 48f),
                Color.white,
                FontStyle.Bold);

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Message",
                "可消耗 1 个仙露回滚到检查点并继续本局。",
                22,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 36f),
                new Vector2(520f, 80f),
                new Color(0.95f, 0.92f, 0.84f, 1f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Detail",
                "当前仙露数量：0",
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -18f),
                new Vector2(460f, 32f),
                new Color(0.82f, 0.77f, 0.62f, 1f));

            RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Revive",
                "仙露复活",
                new Vector2(-130f, -110f),
                new Vector2(210f, 58f),
                new Color(0.28f, 0.46f, 0.32f, 1f),
                Color.white);

            RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_GiveUp",
                "结束本局",
                new Vector2(130f, -110f),
                new Vector2(210f, 58f),
                new Color(0.46f, 0.22f, 0.2f, 1f),
                Color.white);
        }
    }
}
