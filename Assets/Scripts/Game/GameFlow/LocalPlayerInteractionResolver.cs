using Game.Input;
using Game.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.GameFlow
{
    /// <summary>
    /// 场景交互统一入口：只解析本机玩家和本机交互输入，避免联机远端头像影响宝箱、掉落物和商店。
    /// </summary>
    public static class LocalPlayerInteractionResolver
    {
        public static Transform ResolvePlayerTransform(Scene interactionScene, Transform cachedPlayerTransform)
        {
            if (IsUsablePlayerTransform(cachedPlayerTransform, interactionScene))
                return cachedPlayerTransform;

            Transform levelPlayerTransform = GameStateMachine.GetInstance()?.LevelPlayerTransform;
            if (IsUsablePlayerTransform(levelPlayerTransform, interactionScene))
                return levelPlayerTransform;

            PlayerController[] controllers = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                PlayerController controller = controllers[i];
                if (controller != null && controller.PlayerModel != null && IsUsablePlayerTransform(controller.transform, interactionScene))
                    return controller.transform;
            }

            return null;
        }

        public static bool IsInteractPressedThisFrame(Transform localPlayerTransform)
        {
#if ENABLE_INPUT_SYSTEM
            InputAction interactAction = ResolveInteractAction(localPlayerTransform);
            return interactAction != null && interactAction.WasPressedThisFrame();
#else
            return false;
#endif
        }

        public static bool IsInteractPressed(Transform localPlayerTransform)
        {
#if ENABLE_INPUT_SYSTEM
            InputAction interactAction = ResolveInteractAction(localPlayerTransform);
            return interactAction != null && interactAction.IsPressed();
#else
            return false;
#endif
        }

        private static bool IsUsablePlayerTransform(Transform playerTransform, Scene interactionScene)
        {
            if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
                return false;

            Scene playerScene = playerTransform.gameObject.scene;
            if (!playerScene.IsValid() || !playerScene.isLoaded)
                return false;

            if (interactionScene.IsValid() && interactionScene.isLoaded && playerScene != interactionScene)
                return false;

            PlayerController controller = playerTransform.GetComponent<PlayerController>();
            if (controller == null)
                controller = playerTransform.GetComponentInParent<PlayerController>();

            return controller != null && controller.PlayerModel != null;
        }

#if ENABLE_INPUT_SYSTEM
        private static InputAction ResolveInteractAction(Transform localPlayerTransform)
        {
            PlayerInput playerInput = localPlayerTransform != null ? localPlayerTransform.GetComponent<PlayerInput>() : null;
            if (playerInput == null && localPlayerTransform != null)
                playerInput = localPlayerTransform.GetComponentInParent<PlayerInput>();

            InputAction action = playerInput?.actions?.FindAction("Interact");
            if (IsUsableAction(action))
                return action;

            GameplayInputEvents events = Object.FindFirstObjectByType<GameplayInputEvents>();
            action = events != null ? events.FindAction("Interact") : null;
            return IsUsableAction(action) ? action : null;
        }

        private static bool IsUsableAction(InputAction action)
        {
            return action != null && action.enabled;
        }
#endif
    }
}
