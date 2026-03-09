using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Game.Input;
using Game.GameFlow;

namespace Game.UI
{
    /// <summary>
    /// Cursor visibility policy for in-level UI.
    /// </summary>
    public class CursorVisibilityController : MonoBehaviour
    {
        private static bool IsGameplayCursorContext()
        {
            var gsm = GameStateMachine.GetInstance();
            if (gsm == null)
                return false;

            return gsm.CurrentState is GameStateMachine.State.InLevel or GameStateMachine.State.Paused;
        }

        private void OnEnable()
        {
            GameplayInputEvents.ToggleCursorRequested += OnToggleCursorRequested;
        }

        private void OnDisable()
        {
            GameplayInputEvents.ToggleCursorRequested -= OnToggleCursorRequested;
        }

        private void Update()
        {
            // Main menu and non-level states should always keep cursor visible.
            if (!IsGameplayCursorContext())
            {
                ShowCursor();
                return;
            }

            bool hasGameplayPanelOpen = GameplayUIInputBridge.IsAnyGameplayPanelOpen();
            ApplyPanelBlockState(hasGameplayPanelOpen);
            if (hasGameplayPanelOpen)
                return;

            if (!Cursor.visible)
                return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            LockCursor();
        }

        private void OnToggleCursorRequested()
        {
            if (!IsGameplayCursorContext())
                return;

            if (GameplayUIInputBridge.IsAnyGameplayPanelOpen())
                return;

            if (Cursor.visible)
                LockCursor();
            else
                ShowCursor();
        }

        public static void ApplyPanelBlockState(bool hasBlockingPanelOpen)
        {
            if (!IsGameplayCursorContext())
            {
                ShowCursor();
                return;
            }

            if (hasBlockingPanelOpen)
                ShowCursor();
        }

        public static void ShowCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private static void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
