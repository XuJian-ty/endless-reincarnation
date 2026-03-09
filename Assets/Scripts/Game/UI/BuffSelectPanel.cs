using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectBase;

namespace Game.UI
{
    public class BuffSelectPanel : BasePanel
    {
        [Header("Animation")]
        [Tooltip("Expand animation duration in seconds")]
        public float expandDuration = 2f;

        [Header("References")]
        [Tooltip("Root body to animate")]
        public RectTransform panelBody;
        [Tooltip("Container holding the three buff cards")]
        public Transform cardsContainer;

        [Header("Cards")]
        public BuffCardUI card1;
        public BuffCardUI card2;
        public BuffCardUI card3;

        private readonly List<BuffCardUI> _cards = new List<BuffCardUI>();
        private int _selectedIndex = -1;
        private System.Action<string> _onConfirm;
        private Vector2 _expandedSize;
        private Button _confirmButton;

        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Confirm", OnConfirmClicked);
            _confirmButton = GetControl<Button>("Btn_Confirm");
            if (panelBody != null)
                _expandedSize = panelBody.sizeDelta;
            ApplyCollapsedVisualState();
        }

        public void ShowWithBuffs(List<string> buffOptions, System.Action<string> onConfirm)
        {
            if (buffOptions == null || buffOptions.Count != 3)
            {
                Debug.LogError("[BuffSelectPanel] Exactly 3 buff options are required.");
                return;
            }

            _onConfirm = onConfirm;
            _selectedIndex = -1;
            StopAllCoroutines();

            _cards.Clear();
            if (card1 != null) _cards.Add(card1);
            if (card2 != null) _cards.Add(card2);
            if (card3 != null) _cards.Add(card3);

            for (int i = 0; i < _cards.Count && i < buffOptions.Count; i++)
            {
                _cards[i].Setup(i, buffOptions[i], OnCardSelected);
                _cards[i].SetSelected(false);
            }

            ApplyCollapsedVisualState();
            gameObject.SetActive(true);
            GameplayUIInputBridge.RequestStateRefresh();

            StartCoroutine(PlayExpandAnimation());
        }

        public override void ShowMe()
        {
            // Buff panel should only be shown through ShowWithBuffs,
            // where options are injected and expand animation starts.
        }

        public override void HideMe()
        {
            StopAllCoroutines();
            ApplyCollapsedVisualState();
            gameObject.SetActive(false);
            GameplayUIInputBridge.RequestStateRefresh();
        }

        private void OnCardSelected(int index)
        {
            _selectedIndex = index;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i != index)
                    _cards[i].SetSelected(false);
            }
        }

        private void OnConfirmClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _cards.Count)
            {
                Debug.LogWarning("[BuffSelectPanel] Select a buff before confirming.");
                return;
            }

            string selectedBuffId = _cards[_selectedIndex].BuffId;
            UIManager.GetInstance().HidePanel(PanelNames.BuffSelect);
            _onConfirm?.Invoke(selectedBuffId);
        }

        private IEnumerator PlayExpandAnimation()
        {
            if (panelBody == null)
            {
                Debug.LogError("[BuffSelectPanel] panelBody is not assigned.");
                yield break;
            }

            yield return null;

            float originalWidth = _expandedSize.x > 0f ? _expandedSize.x : panelBody.sizeDelta.x;
            float originalHeight = _expandedSize.y > 0f ? _expandedSize.y : panelBody.sizeDelta.y;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, expandDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = EaseOutCubic(t);
                float currentWidth = Mathf.Lerp(0f, originalWidth, easedT);
                panelBody.sizeDelta = new Vector2(currentWidth, originalHeight);
                yield return null;
            }

            panelBody.sizeDelta = new Vector2(originalWidth, originalHeight);
            SetContentVisible(true);
        }

        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private void ApplyCollapsedVisualState()
        {
            if (panelBody != null)
            {
                float height = _expandedSize.y > 0f ? _expandedSize.y : panelBody.sizeDelta.y;
                panelBody.sizeDelta = new Vector2(0f, height);
            }

            SetContentVisible(false);
        }

        private void SetContentVisible(bool visible)
        {
            if (cardsContainer != null)
                cardsContainer.gameObject.SetActive(visible);

            if (_confirmButton != null)
                _confirmButton.gameObject.SetActive(visible);
        }
    }
}
