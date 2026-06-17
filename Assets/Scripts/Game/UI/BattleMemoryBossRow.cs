using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 战斗回忆列表项：展示 Boss 名称，并提供进入回忆按钮。
    /// </summary>
    public sealed class BattleMemoryBossRow : MonoBehaviour
    {
        private const string BossNameTextName = "Txt_BossName";
        private const string EnterButtonName = "Btn_Enter";
        private const string EnterButtonTextName = "Txt_Enter";

        private Text _bossNameText;
        private Button _enterButton;
        private Text _enterButtonText;
        private string _bossId;
        private Action<string> _onEnter;

        private void Awake()
        {
            _bossNameText = transform.Find(BossNameTextName)?.GetComponent<Text>();
            _enterButton = transform.Find(EnterButtonName)?.GetComponent<Button>();
            _enterButtonText = transform.Find($"{EnterButtonName}/{EnterButtonTextName}")?.GetComponent<Text>();

            if (_enterButton != null)
            {
                _enterButton.onClick.RemoveListener(HandleEnterClicked);
                _enterButton.onClick.AddListener(HandleEnterClicked);
            }
        }

        public void Bind(string bossId, string bossDisplayName, Action<string> onEnter)
        {
            _bossId = string.IsNullOrWhiteSpace(bossId) ? string.Empty : bossId.Trim();
            _onEnter = onEnter;

            if (_bossNameText != null)
                _bossNameText.text = string.IsNullOrWhiteSpace(bossDisplayName) ? _bossId : bossDisplayName;

            if (_enterButtonText != null)
                _enterButtonText.text = "进入回忆";
        }

        private void HandleEnterClicked()
        {
            if (string.IsNullOrWhiteSpace(_bossId))
                return;

            _onEnter?.Invoke(_bossId);
        }
    }
}
