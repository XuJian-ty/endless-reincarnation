using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// 按键配置面板中的一行：显示动作名、当前键位，点击「重绑」后等待键盘按键并写回，点击「恢复默认」清除覆盖。
    /// 由 KeyConfigPanel 在运行时创建并绑定到 InputAction + bindingIndex。
    /// </summary>
    public class KeyConfigRow : MonoBehaviour
    {
        [SerializeField] private Text labelText;
        [SerializeField] private Text keyText;
        [SerializeField] private Button rebindButton;
        [SerializeField] private Button resetButton;

        private InputAction _action;
        private int _bindingIndex;
        private System.Action<InputAction, int, KeyConfigRow> _onRebindClicked;
        private System.Action<InputAction, int> _onResetClicked;
        private System.Action _onCustomPrimaryClicked;
        private System.Action _onCustomSecondaryClicked;

        /// <summary>动态创建行时注入控件引用（无预制体时由 KeyConfigPanel 调用）。</summary>
        public void SetLabelAndKey(Text label, Text key, Button rebindBtn, Button resetBtn = null)
        {
            labelText = label;
            keyText = key;
            rebindButton = rebindBtn;
            resetButton = resetBtn;
        }

        public void Set(string displayName, InputAction action, int bindingIndex,
            System.Action<InputAction, int, KeyConfigRow> onRebindClicked,
            System.Action<InputAction, int> onResetClicked = null)
        {
            _action = action;
            _bindingIndex = bindingIndex;
            _onRebindClicked = onRebindClicked;
            _onResetClicked = onResetClicked;
            _onCustomPrimaryClicked = null;
            _onCustomSecondaryClicked = null;

            if (labelText != null) labelText.text = displayName;
            RefreshKeyText();

            if (rebindButton != null)
            {
                rebindButton.onClick.RemoveAllListeners();
                rebindButton.onClick.AddListener(OnRebindClick);
            }
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnResetClick);
            }
        }

        public void SetCustom(
            string displayName,
            string valueText,
            string primaryButtonText,
            System.Action onPrimaryClicked,
            string secondaryButtonText,
            System.Action onSecondaryClicked)
        {
            _action = null;
            _onRebindClicked = null;
            _onResetClicked = null;
            _onCustomPrimaryClicked = onPrimaryClicked;
            _onCustomSecondaryClicked = onSecondaryClicked;

            if (labelText != null) labelText.text = displayName;
            if (keyText != null) keyText.text = valueText;
            SetButtonText(rebindButton, primaryButtonText);
            SetButtonText(resetButton, secondaryButtonText);

            if (rebindButton != null)
            {
                rebindButton.onClick.RemoveAllListeners();
                rebindButton.onClick.AddListener(OnRebindClick);
            }
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnResetClick);
            }
        }

        public void RefreshKeyText()
        {
            if (keyText == null || _action == null) return;
            keyText.text = _action.GetBindingDisplayString(_bindingIndex);
        }

        /// <summary>重绑等待时显示提示（如「按任意键...」），由 KeyConfigPanel 调用。</summary>
        public void SetKeyTextPrompt(string prompt)
        {
            if (keyText != null) keyText.text = prompt;
        }

        /// <summary>重绑过程中禁用/恢复本行「重绑」「恢复默认」两个按钮，由 KeyConfigPanel 调用。</summary>
        public void SetButtonsInteractable(bool interactable)
        {
            if (rebindButton != null) rebindButton.interactable = interactable;
            if (resetButton != null) resetButton.interactable = interactable;
        }

        private void OnRebindClick()
        {
            if (_onCustomPrimaryClicked != null)
            {
                _onCustomPrimaryClicked.Invoke();
                return;
            }

            _onRebindClicked?.Invoke(_action, _bindingIndex, this);
        }

        private void OnResetClick()
        {
            if (_onCustomSecondaryClicked != null)
            {
                _onCustomSecondaryClicked.Invoke();
                return;
            }

            _onResetClicked?.Invoke(_action, _bindingIndex);
        }

        private static void SetButtonText(Button button, string content)
        {
            Text text = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (text != null)
                text.text = content;
        }
    }
}
