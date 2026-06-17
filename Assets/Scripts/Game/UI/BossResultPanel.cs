using System;
using UnityEngine;
using UnityEngine.UI;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// Boss 结算面板：支持重打本关，以及进入下一关或完成游戏。
    /// 若缺少预制体子节点，会自动构建最小可用运行时 UI。
    /// </summary>
    public class BossResultPanel : BasePanel
    {
        private Action _onRetry;
        private Action _onAdvanceOrFinish;
        private Text _titleText;
        private Text _messageText;
        private Text _detailText;
        private Text _advanceButtonText;
        private string _detailPrefix;
        private string _messagePrefix;

        protected override void Awake()
        {
            EnsureRuntimeUi();
            base.Awake();

            RegisterClick("Btn_Retry", OnRetryClicked);
            RegisterClick("Btn_Advance", OnAdvanceClicked);

            _titleText = GetControl<Text>("Txt_Title");
            _messageText = GetControl<Text>("Txt_Message");
            _detailText = GetControl<Text>("Txt_Detail");
            var advanceButton = GetControl<Button>("Btn_Advance");
            _advanceButtonText = advanceButton != null ? advanceButton.GetComponentInChildren<Text>(true) : null;
            _detailPrefix = _detailText != null ? _detailText.text : string.Empty;
            _messagePrefix = _messageText != null ? _messageText.text : string.Empty;
        }

        public void ShowResult(int clearedLevel, string bossId, bool hasNextLevel, Action onRetry, Action onAdvanceOrFinish)
        {
            _onRetry = onRetry;
            _onAdvanceOrFinish = onAdvanceOrFinish;

            if (_messageText != null && !string.IsNullOrWhiteSpace(_messagePrefix))
                _messageText.text = _messagePrefix;

            if (_detailText != null)
            {
                string detail = hasNextLevel
                    ? "可选择重打本关，或继续前往下一关。"
                    : "可选择重打本关，或留在当前关卡继续探索。";
                _detailText.text = string.IsNullOrWhiteSpace(_detailPrefix)
                    ? detail
                    : $"{_detailPrefix}{clearedLevel}";
            }

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

        private void OnRetryClicked()
        {
            UIManager.GetInstance().HidePanel(PanelNames.BossResult);
            _onRetry?.Invoke();
        }

        private void OnAdvanceClicked()
        {
            UIManager.GetInstance().HidePanel(PanelNames.BossResult);
            _onAdvanceOrFinish?.Invoke();
        }

        private void EnsureRuntimeUi()
        {
            if (transform.Find("Window") != null)
                return;

            RectTransform rootRect = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(rootRect);
            RuntimeOverlayPanelBuilder.EnsureOverlayBackground(gameObject, new Color(0f, 0f, 0f, 0.72f));

            RectTransform window = RuntimeOverlayPanelBuilder.CreateWindow(
                transform,
                "Window",
                new Vector2(660f, 360f),
                new Color(0.12f, 0.11f, 0.09f, 0.96f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Title",
                "Boss 已击败",
                34,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 114f),
                new Vector2(560f, 48f),
                Color.white,
                FontStyle.Bold);

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Message",
                "本关 Boss 已被击败。",
                22,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 38f),
                new Vector2(560f, 88f),
                new Color(0.95f, 0.92f, 0.84f, 1f));

            RuntimeOverlayPanelBuilder.CreateText(
                window,
                "Txt_Detail",
                "可选择重打本关，或继续前往下一关。",
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -20f),
                new Vector2(520f, 34f),
                new Color(0.82f, 0.77f, 0.62f, 1f));

            RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Retry",
                "重打本关",
                new Vector2(-140f, -118f),
                new Vector2(220f, 58f),
                new Color(0.31f, 0.33f, 0.42f, 1f),
                Color.white);

            RuntimeOverlayPanelBuilder.CreateButton(
                window,
                "Btn_Advance",
                "继续探索",
                new Vector2(140f, -118f),
                new Vector2(220f, 58f),
                new Color(0.25f, 0.42f, 0.3f, 1f),
                Color.white);
        }
    }
}
