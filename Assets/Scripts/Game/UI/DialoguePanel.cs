using System;
using System.Collections.Generic;
using Game.Data;
using Game.GameFlow;
using Game.Input;
using ProjectBase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 通用对话面板：显示一轮 NPC 台词，按 E 后进入玩家答复选择；确认答复后进入下一轮或结束。
    /// </summary>
    public sealed class DialoguePanel : BasePanel
    {
        private const string ReplyOptionResourcePath = "UI/DialogueReplyOption";
        private const float NavigateRepeatInterval = 0.18f;
        private const float NavigateTriggerThreshold = 0.5f;

        private enum DialogueState
        {
            None,
            ShowingNpcLine,
            SelectingReply,
        }

        [Header("面板控件")]
        [FormerlySerializedAs("_speakerText")]
        [SerializeField] private Text _speakerNameText;
        [SerializeField] private Text _contentText;
        [SerializeField] private Text _hintText;
        [SerializeField] private RectTransform _optionsRoot;
        [SerializeField] private ToggleGroup _optionsGroup;
        [SerializeField] private GameObject _replyOptionPrefab;

        private readonly UiGameObjectPool _replyOptionPool = new UiGameObjectPool();
        private readonly List<GameObject> _activeOptionObjects = new List<GameObject>();
        private readonly List<Toggle> _spawnedOptionToggles = new List<Toggle>();
        private readonly List<Text> _spawnedOptionTexts = new List<Text>();

        private HutaoShopDialogueConfigSO _config;
        private Action<bool> _onConversationFinished;
        private DialogueState _state;
        private int _roundIndex;
        private float _lastNavigateInputY;
        private float _nextNavigateAllowedTime;
        private bool _sessionActive;
        private bool _loggedMissingBindings;

        public void BeginSession(HutaoShopDialogueConfigSO config, Action<bool> onConversationFinished)
        {
            ResolveReferences();
            _config = config;
            _onConversationFinished = onConversationFinished;
            _roundIndex = 0;
            _sessionActive = _config != null && _config.HasRounds;

            if (!_sessionActive || !HasRequiredBindings())
            {
                FinishConversation(false);
                return;
            }

            ShowRoundLine();
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
        }

        private void Update()
        {
            if (!_sessionActive || !gameObject.activeInHierarchy)
                return;

            if (_state == DialogueState.SelectingReply)
                HandleReplyNavigation();

            if (!WasInteractPressedThisFrame())
                return;

            if (_state == DialogueState.ShowingNpcLine)
            {
                ShowReplyOptions();
                return;
            }

            if (_state == DialogueState.SelectingReply)
                ConfirmSelectedReply();
        }

        private void ShowRoundLine()
        {
            HutaoDialogueRoundData round = _config?.GetRound(_roundIndex);
            if (round == null)
            {
                FinishConversation(false);
                return;
            }

            _state = DialogueState.ShowingNpcLine;
            SetText(_speakerNameText, string.IsNullOrWhiteSpace(_config.speakerName) ? "胡桃" : _config.speakerName);
            SetText(_contentText, round.hutaoLine);
            SetText(_hintText, "按 E 继续");
            SetGraphicVisible(_speakerNameText, true);
            SetGraphicVisible(_contentText, true);

            if (_optionsRoot != null)
                _optionsRoot.gameObject.SetActive(false);

            ClearReplyOptions();
        }

        private void ShowReplyOptions()
        {
            HutaoDialogueRoundData round = _config?.GetRound(_roundIndex);
            if (round == null)
            {
                FinishConversation(false);
                return;
            }

            List<HutaoDialogueOptionData> options = round.options;
            if (options == null || options.Count == 0)
            {
                FinishConversation(false);
                return;
            }

            ResolveReferences();
            if (_replyOptionPrefab == null)
            {
                LogMissingBindings("未绑定玩家答复选项预制体，无法显示对话答复。");
                FinishConversation(false);
                return;
            }

            ClearReplyOptions();
            SetText(_speakerNameText, ResolvePlayerSpeakerName());
            SetGraphicVisible(_speakerNameText, true);
            SetGraphicVisible(_contentText, false);

            Toggle firstToggle = null;
            for (int i = 0; i < options.Count; i++)
            {
                GameObject optionObject = _replyOptionPool.Acquire(_replyOptionPrefab, _optionsRoot, $"ReplyOption_{i + 1}");
                if (optionObject == null)
                    continue;

                Toggle toggle = optionObject.GetComponent<Toggle>();
                Text text = FindText(optionObject.transform, "ReplyText") ?? optionObject.GetComponentInChildren<Text>(true);
                if (toggle != null)
                {
                    toggle.SetIsOnWithoutNotify(false);
                    if (_optionsGroup != null)
                        toggle.group = _optionsGroup;
                    if (firstToggle == null)
                        firstToggle = toggle;
                }

                SetText(text, options[i]?.replyText);
                _activeOptionObjects.Add(optionObject);
                _spawnedOptionToggles.Add(toggle);
                _spawnedOptionTexts.Add(text);
            }

            if (firstToggle != null)
                firstToggle.isOn = true;

            _state = DialogueState.SelectingReply;
            SetText(_hintText, "点击选项后按 E 确认");

            if (_optionsRoot != null)
                _optionsRoot.gameObject.SetActive(true);
        }

        private void ConfirmSelectedReply()
        {
            HutaoDialogueRoundData round = _config?.GetRound(_roundIndex);
            List<HutaoDialogueOptionData> options = round?.options;
            if (options == null || options.Count == 0)
            {
                FinishConversation(false);
                return;
            }

            int optionIndex = Mathf.Clamp(GetSelectedReplyIndex(), 0, options.Count - 1);
            HutaoDialogueOptionData option = options[optionIndex];
            bool isLastRound = _config == null || _roundIndex >= _config.rounds.Count - 1;
            bool shouldEnd = option != null && option.endConversation || isLastRound;
            bool openShop = option != null && option.openShopPanelOnEnd;

            if (shouldEnd)
            {
                FinishConversation(openShop);
                return;
            }

            _roundIndex++;
            ShowRoundLine();
        }

        private void HandleReplyNavigation()
        {
            InputAction action = ResolveNavigateAction();
            if (action == null)
                return;

            float vertical = action.ReadValue<Vector2>().y;
            bool movedUp = vertical >= NavigateTriggerThreshold;
            bool movedDown = vertical <= -NavigateTriggerThreshold;
            bool wasNeutral = Mathf.Abs(_lastNavigateInputY) < NavigateTriggerThreshold;
            float now = Time.unscaledTime;

            if (movedUp && (wasNeutral || now >= _nextNavigateAllowedTime))
            {
                SelectRelativeReply(-1);
                _nextNavigateAllowedTime = now + NavigateRepeatInterval;
            }
            else if (movedDown && (wasNeutral || now >= _nextNavigateAllowedTime))
            {
                SelectRelativeReply(1);
                _nextNavigateAllowedTime = now + NavigateRepeatInterval;
            }
            else if (!movedUp && !movedDown)
            {
                _nextNavigateAllowedTime = now;
            }

            _lastNavigateInputY = vertical;
        }

        private void FinishConversation(bool openShop)
        {
            _sessionActive = false;
            _state = DialogueState.None;
            ClearReplyOptions();
            SetGraphicVisible(_speakerNameText, true);
            SetGraphicVisible(_contentText, true);

            if (_optionsRoot != null)
                _optionsRoot.gameObject.SetActive(false);

            Action<bool> callback = _onConversationFinished;
            _onConversationFinished = null;

            UIManager.GetInstance()?.HidePanel(PanelNames.Dialogue);
            if (!openShop)
                GameplayUIInputBridge.RequestStateRefresh();
            callback?.Invoke(openShop);
        }

        private void ResolveReferences()
        {
            Transform dialogueRoot = transform.Find("DialogueRoot");
            if (_speakerNameText == null && dialogueRoot != null)
                _speakerNameText = FindText(dialogueRoot, "SpeakerName") ?? FindText(dialogueRoot, "SpeakerText");
            if (_contentText == null && dialogueRoot != null)
                _contentText = FindText(dialogueRoot, "DialogueText") ?? FindText(dialogueRoot, "ContentText");
            if (_hintText == null && dialogueRoot != null)
                _hintText = FindText(dialogueRoot, "HintText");
            if (_optionsRoot == null && dialogueRoot != null)
                _optionsRoot = dialogueRoot.Find("OptionsRoot") as RectTransform;
            if (_optionsGroup == null && _optionsRoot != null)
                _optionsGroup = _optionsRoot.GetComponent<ToggleGroup>();
            if (_replyOptionPrefab == null)
                _replyOptionPrefab = Resources.Load<GameObject>(ReplyOptionResourcePath);
        }

        private bool HasRequiredBindings()
        {
            if (_contentText != null && _hintText != null && _optionsRoot != null && _optionsGroup != null && _replyOptionPrefab != null)
                return true;

            LogMissingBindings("对话面板缺少必要控件，请先搭建 DialoguePanel 预制体。");
            return false;
        }

        private int GetSelectedReplyIndex()
        {
            for (int i = 0; i < _spawnedOptionToggles.Count; i++)
            {
                Toggle toggle = _spawnedOptionToggles[i];
                if (toggle != null && toggle.gameObject.activeSelf && toggle.isOn)
                    return i;
            }

            return 0;
        }

        private void ClearReplyOptions()
        {
            for (int i = 0; i < _activeOptionObjects.Count; i++)
                _replyOptionPool.Release(_activeOptionObjects[i], _optionsRoot);

            _activeOptionObjects.Clear();
            _spawnedOptionToggles.Clear();
            _spawnedOptionTexts.Clear();
            _lastNavigateInputY = 0f;
            _nextNavigateAllowedTime = 0f;
        }

        private void OnDestroy()
        {
            _replyOptionPool.Clear();
        }

        private bool WasInteractPressedThisFrame()
        {
            InputAction action = ResolveInteractAction();
            return action != null && action.WasPressedThisFrame();
        }

        private InputAction ResolveNavigateAction()
        {
            GameplayInputEvents events = FindFirstObjectByType<GameplayInputEvents>();
            InputAction action = events != null ? events.FindAction("Navigate") : null;
            if (action != null)
                return action;

            PlayerInput playerInput = FindFirstObjectByType<PlayerInput>();
            return playerInput?.actions?.FindAction("Navigate");
        }

        private static InputAction ResolveInteractAction()
        {
            GameplayInputEvents events = FindFirstObjectByType<GameplayInputEvents>();
            InputAction action = events != null ? events.FindAction("Interact") : null;
            if (action != null)
                return action;

            PlayerInput playerInput = FindFirstObjectByType<PlayerInput>();
            return playerInput?.actions?.FindAction("Interact");
        }

        private void SelectRelativeReply(int delta)
        {
            int optionCount = _spawnedOptionToggles.Count;
            if (optionCount <= 0)
                return;

            int selectedIndex = Mathf.Clamp(GetSelectedReplyIndex(), 0, optionCount - 1);
            int nextIndex = (selectedIndex + delta + optionCount) % optionCount;
            Toggle nextToggle = _spawnedOptionToggles[nextIndex];
            if (nextToggle == null)
                return;

            nextToggle.isOn = true;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static string ResolvePlayerSpeakerName()
        {
            string playerName = GameStateMachine.GetInstance()?.CurrentPlayerName;
            return string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName;
        }

        private static Text FindText(Transform parent, string name)
        {
            return parent != null ? parent.Find(name)?.GetComponent<Text>() : null;
        }

        private static void SetGraphicVisible(Graphic graphic, bool visible)
        {
            if (graphic != null)
                graphic.gameObject.SetActive(visible);
        }

        private void LogMissingBindings(string message)
        {
            if (_loggedMissingBindings)
                return;

            _loggedMissingBindings = true;
            Debug.LogWarning($"[DialoguePanel:{name}] {message}", this);
        }
    }
}
