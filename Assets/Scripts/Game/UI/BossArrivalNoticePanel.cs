using System.Collections;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Boss 降临顶部提示。默认在屏幕最上方显示一行红字，到时自动隐藏。
    /// </summary>
    public sealed class BossArrivalNoticePanel : BasePanel
    {
        private const string MessageTextName = "Txt_Message";

        private Coroutine _hideRoutine;
        private Text _messageText;

        protected override void Awake()
        {
            EnsureRuntimeUi();
            base.Awake();
            _messageText = GetControl<Text>(MessageTextName);
            HideMe();
        }

        public void ShowNotice(string message, float duration)
        {
            if (_messageText == null)
                return;

            _messageText.text = message;
            ShowMe();

            if (_hideRoutine != null)
                StopCoroutine(_hideRoutine);

            if (duration > 0f)
                _hideRoutine = StartCoroutine(HideAfterDelay(duration));
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
        }

        public override void HideMe()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator HideAfterDelay(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            _hideRoutine = null;
            UIManager.GetInstance()?.HidePanel(PanelNames.BossArrivalNotice);
        }

        private void EnsureRuntimeUi()
        {
            if (transform.Find(MessageTextName) != null)
                return;

            RectTransform rootRect = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(rootRect);

            GameObject messageObject = new GameObject(
                MessageTextName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            messageObject.transform.SetParent(transform, false);

            RectTransform rect = messageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(960f, 72f);
            rect.anchoredPosition = new Vector2(0f, -32f);

            Text text = messageObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = "最终Boss即将降临！";
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.18f, 0.18f, 1f);
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Outline outline = messageObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }
}
