using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.GameFlow;
using Game.UI;

namespace Game.Input
{
    /// <summary>
    /// 关卡内全局输入事件：仅负责从新输入系统读取“非玩家角色”的按键（背包、技能树、按键配置、菜单等），
    /// 通过 C# 事件向外广播，不依赖 UI、玩家等模块，保持解耦。
    /// 挂载在场景内独立物体（如 GameplayInput）上，Inspector 中绑定 PlayerInputActions 资源。
    /// </summary>
    public class GameplayInputEvents : MonoBehaviour
    {
        [Header("输入资源")]
        [SerializeField] private InputActionAsset _inputActionAsset;

        private InputActionMap _gameplayMap;
        private InputActionMap _uiMap;
        private InputAction _toggleBackpackAction;
        private InputAction _toggleSkillTreeAction;
        private InputAction _toggleKeyConfigAction;
        private InputAction _toggleMenuAction;

        /// <summary>按 C 切换背包时触发。</summary>
        public static event Action ToggleBackpackRequested
        {
            add => _toggleBackpackRequested += value;
            remove => _toggleBackpackRequested -= value;
        }
        private static event Action _toggleBackpackRequested;

        /// <summary>按 V 切换技能树面板时触发。</summary>
        public static event Action ToggleSkillTreeRequested
        {
            add => _toggleSkillTreeRequested += value;
            remove => _toggleSkillTreeRequested -= value;
        }
        private static event Action _toggleSkillTreeRequested;

        /// <summary>按 X 切换按键配置面板时触发。</summary>
        public static event Action ToggleKeyConfigRequested
        {
            add => _toggleKeyConfigRequested += value;
            remove => _toggleKeyConfigRequested -= value;
        }
        private static event Action _toggleKeyConfigRequested;

        /// <summary>按 Esc 切换菜单面板时触发。</summary>
        public static event Action ToggleMenuRequested
        {
            add => _toggleMenuRequested += value;
            remove => _toggleMenuRequested -= value;
        }
        private static event Action _toggleMenuRequested;

        /// <summary>按 Z 切换光标显示/隐藏时触发。</summary>
        public static event Action ToggleCursorRequested
        {
            add => _toggleCursorRequested += value;
            remove => _toggleCursorRequested -= value;
        }
        private static event Action _toggleCursorRequested;

        private static bool _hasLoggedNoAsset;

        private InputAction _toggleCursorAction;

        private void OnEnable()
        {
            if (_inputActionAsset == null)
            {
                if (!_hasLoggedNoAsset)
                {
                    _hasLoggedNoAsset = true;
                    Debug.LogWarning("[GameplayInputEvents] Input Action Asset 未分配，Esc/X/C/V 等按键无效。请在关卡场景中选中挂载本组件的物体，将 PlayerInputActions 拖到 Inspector 的 Input Action Asset。");
                }
                return;
            }

            _inputActionAsset.Enable();
            _gameplayMap = _inputActionAsset.FindActionMap("Gameplay");
            _uiMap = _inputActionAsset.FindActionMap("UI");
            var gsm = GameStateMachine.GetInstance();
            if (gsm != null && !string.IsNullOrEmpty(gsm.GetKeyConfigOverrides()))
                KeyConfigPanel.ApplyOverridesTo(_inputActionAsset, gsm.GetKeyConfigOverrides());
            // 四个面板键可能在 Gameplay 或 UI 地图中，两处都查一遍
            _toggleBackpackAction = _gameplayMap?.FindAction("ToggleBackpack") ?? _uiMap?.FindAction("ToggleBackpack");
            _toggleSkillTreeAction = _gameplayMap?.FindAction("ToggleSkillTree") ?? _uiMap?.FindAction("ToggleSkillTree");
            _toggleKeyConfigAction = _gameplayMap?.FindAction("ToggleKeyConfig") ?? _uiMap?.FindAction("ToggleKeyConfig");
            _toggleMenuAction = _gameplayMap?.FindAction("ToggleMenu") ?? _uiMap?.FindAction("ToggleMenu");
            _toggleCursorAction = _gameplayMap?.FindAction("ToggleCursor") ?? _uiMap?.FindAction("ToggleCursor");

            if (_toggleBackpackAction == null) Debug.LogWarning("[GameplayInputEvents] 未找到 ToggleBackpack，C 键无法打开背包。请在 PlayerInputActions 的 Gameplay 或 UI 地图中添加该 Action 并绑定 C。");
            if (_toggleSkillTreeAction == null) Debug.LogWarning("[GameplayInputEvents] 未找到 ToggleSkillTree，V 键无效。");
            if (_toggleKeyConfigAction == null) Debug.LogWarning("[GameplayInputEvents] 未找到 ToggleKeyConfig，X 键无法打开按键配置。请在 PlayerInputActions 的 Gameplay 或 UI 地图中添加该 Action 并绑定 X。");
            if (_toggleMenuAction == null) Debug.LogWarning("[GameplayInputEvents] 未找到 ToggleMenu，Esc 无法打开菜单。请在 PlayerInputActions 的 Gameplay 或 UI 地图中添加该 Action 并绑定 Escape。");

            if (_toggleBackpackAction != null) _toggleBackpackAction.performed += OnToggleBackpackPerformed;
            if (_toggleSkillTreeAction != null) _toggleSkillTreeAction.performed += OnToggleSkillTreePerformed;
            if (_toggleKeyConfigAction != null) _toggleKeyConfigAction.performed += OnToggleKeyConfigPerformed;
            if (_toggleMenuAction != null) _toggleMenuAction.performed += OnToggleMenuPerformed;
            if (_toggleCursorAction != null) _toggleCursorAction.performed += OnToggleCursorPerformed;
        }

        private void OnDisable()
        {
            if (_toggleBackpackAction != null) _toggleBackpackAction.performed -= OnToggleBackpackPerformed;
            if (_toggleSkillTreeAction != null) _toggleSkillTreeAction.performed -= OnToggleSkillTreePerformed;
            if (_toggleKeyConfigAction != null) _toggleKeyConfigAction.performed -= OnToggleKeyConfigPerformed;
            if (_toggleMenuAction != null) _toggleMenuAction.performed -= OnToggleMenuPerformed;
            if (_toggleCursorAction != null) _toggleCursorAction.performed -= OnToggleCursorPerformed;
            _toggleBackpackAction = _toggleSkillTreeAction = _toggleKeyConfigAction = _toggleMenuAction = _toggleCursorAction = null;
            _gameplayMap = null;
            _uiMap = null;
            if (_inputActionAsset != null)
                _inputActionAsset.Disable();
        }

        /// <summary>打开 UI 面板时由 GameplayUIInputBridge 设为 false，仅响应 UI 地图；关闭全部面板后设为 true。</summary>
        public void SetGameplayMapEnabled(bool enabled)
        {
            if (_gameplayMap != null)
            {
                if (enabled) _gameplayMap.Enable();
                else _gameplayMap.Disable();
            }
        }

        public InputAction FindAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return null;

            return _uiMap?.FindAction(actionName) ?? _gameplayMap?.FindAction(actionName);
        }

        private void OnToggleBackpackPerformed(InputAction.CallbackContext _) => _toggleBackpackRequested?.Invoke();
        private void OnToggleSkillTreePerformed(InputAction.CallbackContext _) => _toggleSkillTreeRequested?.Invoke();
        private void OnToggleKeyConfigPerformed(InputAction.CallbackContext _) => _toggleKeyConfigRequested?.Invoke();
        private void OnToggleMenuPerformed(InputAction.CallbackContext _) => _toggleMenuRequested?.Invoke();
        private void OnToggleCursorPerformed(InputAction.CallbackContext _) => _toggleCursorRequested?.Invoke();
    }
}
