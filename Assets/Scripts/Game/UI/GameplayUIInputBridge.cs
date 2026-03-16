using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Input;
using Game.GameFlow;
using ProjectBase;

namespace Game.UI
{
    public class GameplayUIInputBridge : MonoBehaviour
    {
        private static readonly string[] ToggleablePanelNames =
        {
            PanelNames.Menu,
            PanelNames.KeyConfig,
            PanelNames.Backpack,
            PanelNames.SkillTree,
        };

        private static readonly string[] BlockingPanelNames = PanelNames.BlockingPanels;

        private static GameplayUIInputBridge _instance;

        private readonly HashSet<string> _pendingOpens = new HashSet<string>();
        private readonly HashSet<string> _cancelledOpens = new HashSet<string>();

        private GameplayInputEvents _gameplayInputEvents;
        private string _lastClosedPanelName;
        private int _lastClosedFrame;
        private bool _panelOpening;

        private static bool IsGameplayUiContext()
        {
            var gsm = GameStateMachine.GetInstance();
            if (gsm == null)
                return false;

            return gsm.CurrentState is GameStateMachine.State.InLevel or GameStateMachine.State.Paused;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
                Debug.LogWarning("[GameplayUIInputBridge] Multiple instances detected. Latest instance will drive UI input state.");

            _instance = this;
            _gameplayInputEvents = Object.FindFirstObjectByType<GameplayInputEvents>();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
                _instance = null;
        }

        private void OnEnable()
        {
            GameplayInputEvents.ToggleBackpackRequested += OnToggleBackpackRequested;
            GameplayInputEvents.ToggleSkillTreeRequested += OnToggleSkillTreeRequested;
            GameplayInputEvents.ToggleKeyConfigRequested += OnToggleKeyConfigRequested;
            GameplayInputEvents.ToggleMenuRequested += OnToggleMenuRequested;
            RefreshBlockingUiState();
        }

        private void OnDisable()
        {
            GameplayInputEvents.ToggleBackpackRequested -= OnToggleBackpackRequested;
            GameplayInputEvents.ToggleSkillTreeRequested -= OnToggleSkillTreeRequested;
            GameplayInputEvents.ToggleKeyConfigRequested -= OnToggleKeyConfigRequested;
            GameplayInputEvents.ToggleMenuRequested -= OnToggleMenuRequested;
            RefreshBlockingUiState();
        }

        private void Update()
        {
            if (!IsGameplayUiContext())
            {
                RefreshBlockingUiState();
                return;
            }

            var ui = UIManager.GetInstance();
            var openPanel = GetOpenToggleablePanelName(ui);
            var keyboard = Keyboard.current;

            if (openPanel != null && keyboard != null)
            {
                if (openPanel == PanelNames.Menu && keyboard.escapeKey.wasPressedThisFrame) ClosePanelAndMark(PanelNames.Menu);
                else if (openPanel == PanelNames.KeyConfig && keyboard.xKey.wasPressedThisFrame) ClosePanelAndMark(PanelNames.KeyConfig);
                else if (openPanel == PanelNames.Backpack && keyboard.cKey.wasPressedThisFrame) ClosePanelAndMark(PanelNames.Backpack);
                else if (openPanel == PanelNames.SkillTree && keyboard.vKey.wasPressedThisFrame) ClosePanelAndMark(PanelNames.SkillTree);
            }

            RefreshBlockingUiState();
        }

        private void OnToggleBackpackRequested() => TogglePanel<BackpackPanel>(PanelNames.Backpack, PanelLayers.Backpack);
        private void OnToggleSkillTreeRequested() => TogglePanel<SkillTreePanel>(PanelNames.SkillTree, PanelLayers.SkillTree);
        private void OnToggleKeyConfigRequested() => TogglePanel<KeyConfigPanel>(PanelNames.KeyConfig, PanelLayers.KeyConfig);
        private void OnToggleMenuRequested() => TogglePanel<MenuPanel>(PanelNames.Menu, PanelLayers.Menu);

        private void ClosePanelAndMark(string panelName)
        {
            _lastClosedPanelName = panelName;
            _lastClosedFrame = Time.frameCount;
            UIManager.GetInstance().HidePanel(panelName);
            RefreshBlockingUiState();
        }

        private void TogglePanel<T>(string panelName, E_UI_Layer layer) where T : BasePanel
        {
            if (!IsGameplayUiContext())
                return;

            if (_lastClosedPanelName == panelName && _lastClosedFrame == Time.frameCount)
                return;

            var ui = UIManager.GetInstance();
            var panel = ui.GetPanel<T>(panelName);
            if (panel != null && panel.gameObject.activeSelf)
            {
                ClosePanelAndMark(panelName);
                return;
            }

            if (_cancelledOpens.Remove(panelName))
                return;

            if (IsAnyBlockingPanelOpen(ui))
                return;

            if (_pendingOpens.Contains(panelName))
            {
                _cancelledOpens.Add(panelName);
                _pendingOpens.Remove(panelName);
                return;
            }

            _pendingOpens.Add(panelName);
            _panelOpening = true;
            RefreshBlockingUiState();

            ui.ShowPanel<T>(panelName, layer, _ =>
            {
                _pendingOpens.Remove(panelName);
                if (_cancelledOpens.Remove(panelName))
                    ui.HidePanel(panelName);

                RefreshBlockingUiState();
            });
        }

        public static bool IsAnyGameplayPanelOpen()
        {
            return IsAnyBlockingPanelOpen(UIManager.GetInstance());
        }

        public static void RequestStateRefresh()
        {
            if (_instance != null)
            {
                _instance.RefreshBlockingUiState();
                return;
            }

            var ui = UIManager.GetInstance();
            bool hasBlockingPanelOpen = IsAnyBlockingPanelOpen(ui);
            bool shouldBlockGameplay = hasBlockingPanelOpen;
            var gameplayInputEvents = Object.FindFirstObjectByType<GameplayInputEvents>();
            if (gameplayInputEvents != null)
                gameplayInputEvents.SetGameplayMapEnabled(!shouldBlockGameplay);

            var gsm = GameStateMachine.GetInstance();
            if (gsm != null)
            {
                if (shouldBlockGameplay)
                    gsm.Pause();
                else if (gsm.CurrentState == GameStateMachine.State.Paused)
                    gsm.Resume();
            }

            CursorVisibilityController.ApplyPanelBlockState(shouldBlockGameplay);
        }

        private static string GetOpenToggleablePanelName(UIManager ui)
        {
            if (ui == null)
                return null;

            foreach (var name in ToggleablePanelNames)
            {
                var panel = ui.GetPanel<BasePanel>(name);
                if (panel != null && panel.gameObject.activeSelf)
                    return name;
            }

            return null;
        }

        private static bool IsAnyBlockingPanelOpen(UIManager ui)
        {
            if (ui == null)
                return false;

            foreach (var name in BlockingPanelNames)
            {
                var panel = ui.GetPanel<BasePanel>(name);
                if (panel != null && panel.gameObject.activeSelf)
                    return true;
            }

            return false;
        }

        private void RefreshBlockingUiState()
        {
            var ui = UIManager.GetInstance();
            bool hasBlockingPanelOpen = IsAnyBlockingPanelOpen(ui);

            if (_pendingOpens.Count == 0)
                _panelOpening = false;

            bool shouldBlockGameplay = hasBlockingPanelOpen || _panelOpening;

            if (_gameplayInputEvents == null)
                _gameplayInputEvents = Object.FindFirstObjectByType<GameplayInputEvents>();

            if (!IsGameplayUiContext())
            {
                if (_gameplayInputEvents != null)
                    _gameplayInputEvents.SetGameplayMapEnabled(true);

                CursorVisibilityController.ApplyPanelBlockState(false);
                return;
            }

            if (_gameplayInputEvents != null)
                _gameplayInputEvents.SetGameplayMapEnabled(!shouldBlockGameplay);

            var gsm = GameStateMachine.GetInstance();
            if (gsm != null)
            {
                if (shouldBlockGameplay)
                    gsm.Pause();
                else if (gsm.CurrentState == GameStateMachine.State.Paused)
                    gsm.Resume();
            }

            CursorVisibilityController.ApplyPanelBlockState(shouldBlockGameplay);
        }
    }
}
