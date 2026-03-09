using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Game;
using Game.Data;

namespace Game.UI
{
    public class BuffCardUI : MonoBehaviour
    {
        [Header("Selected Effect")]
        [Tooltip("Scale multiplier when selected")]
        public float selectedScale = 1.1f;

        [Tooltip("Scale animation duration")]
        public float scaleDuration = 0.2f;

        public string BuffId { get; private set; }

        private int _index;
        private System.Action<int> _onSelected;
        private Toggle _toggle;
        private Text _titleText;
        private Text _descText;
        private Vector3 _originalScale;
        private Button _cardButton;

        public void Setup(int index, string buffId, System.Action<int> onSelected)
        {
            _index = index;
            BuffId = buffId;
            _onSelected = onSelected;

            if (_originalScale == Vector3.zero)
                _originalScale = transform.localScale;
            else
                transform.localScale = _originalScale;

            _toggle = GetComponentInChildren<Toggle>(true);
            var texts = GetComponentsInChildren<Text>(true);
            if (texts.Length >= 2)
            {
                _titleText = texts[0];
                _descText = texts[1];
            }

            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnToggleChanged);
                _toggle.onValueChanged.AddListener(OnToggleChanged);
            }

            var background = transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = true;
                _cardButton = GetComponent<Button>();
                if (_cardButton == null)
                    _cardButton = gameObject.AddComponent<Button>();

                _cardButton.targetGraphic = background;
                _cardButton.transition = Selectable.Transition.None;
                _cardButton.onClick.RemoveListener(OnCardAreaClicked);
                _cardButton.onClick.AddListener(OnCardAreaClicked);
            }

            LoadBuffInfo(buffId);
        }

        private void LoadBuffInfo(string buffId)
        {
            var config = ConfigManager.GetInstance()?.GetBuffConfig();
            if (_titleText != null)
                _titleText.text = config != null ? config.GetDisplayName(buffId) : buffId;
            if (_descText != null)
                _descText.text = config != null ? config.GetDescription(buffId) : string.Empty;
        }

        public void SetSelected(bool selected)
        {
            if (_toggle != null)
                _toggle.SetIsOnWithoutNotify(selected);

            ApplySelectionVisual(selected);
        }

        private void OnCardAreaClicked()
        {
            if (_toggle != null && !_toggle.isOn)
                _toggle.isOn = true;
        }

        private void OnToggleChanged(bool isOn)
        {
            if (isOn)
                _onSelected?.Invoke(_index);

            ApplySelectionVisual(isOn);
        }

        private void ApplySelectionVisual(bool selected)
        {
            Vector3 targetScale = selected ? _originalScale * selectedScale : _originalScale;
            StopAllCoroutines();

            if (!gameObject.activeInHierarchy || !isActiveAndEnabled)
            {
                transform.localScale = targetScale;
                return;
            }

            StartCoroutine(ScaleAnimation(targetScale));
        }

        private IEnumerator ScaleAnimation(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, scaleDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
        }
    }
}
