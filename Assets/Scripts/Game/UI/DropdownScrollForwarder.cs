using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 将滚轮事件转发给当前打开的下拉框列表内的 ScrollRect，解决 Unity Dropdown 的 Blocker 挡住滚轮导致列表无法滚动的问题。
    /// 用法：挂到场景里常驻的 Canvas 或 EventSystem 上（主菜单、关卡内各挂一份，或挂到 DontDestroyOnLoad 的 UI 根节点上）。
    /// </summary>
    public class DropdownScrollForwarder : MonoBehaviour
    {
        [Tooltip("滚轮灵敏度，越大滚动越快")]
        [SerializeField] private float scrollSensitivity = 0.1f;

        private void Update()
        {
            float delta = GetScrollDelta();
            if (delta == 0f) return;

            var scrollRect = FindOpenDropdownScrollRect();
            if (scrollRect == null) return;

            float next = scrollRect.verticalNormalizedPosition + delta * scrollSensitivity;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(next);
        }

        private static float GetScrollDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        /// <summary>
        /// 在场景中确保存在一个 DropdownScrollForwarder（挂到 Canvas 或 EventSystem 上）。
        /// 由 GameEntry / LevelBootstrapper 在 Start 时调用即可。
        /// </summary>
        public static void EnsureExistsInScene()
        {
            if (FindFirstObjectByType<DropdownScrollForwarder>() != null) return;
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                canvas.gameObject.AddComponent<DropdownScrollForwarder>();
                return;
            }
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es != null)
                es.gameObject.AddComponent<DropdownScrollForwarder>();
        }

        /// <summary>
        /// 查找当前任意一个“已展开”的 Dropdown 其列表内的 ScrollRect（仅取第一个）。
        /// Unity 展开列表时会把 template 实例挂到 dropdown 下，该实例里带有 ScrollRect。
        /// </summary>
        private static ScrollRect FindOpenDropdownScrollRect()
        {
            var dropdowns = FindObjectsByType<Dropdown>(FindObjectsSortMode.None);
            foreach (var d in dropdowns)
            {
                if (d == null || !d.gameObject.activeInHierarchy) continue;
                for (int i = 0; i < d.transform.childCount; i++)
                {
                    var child = d.transform.GetChild(i);
                    if (!child.gameObject.activeSelf) continue;
                    var sr = child.GetComponentInChildren<ScrollRect>();
                    if (sr != null && sr.vertical) return sr;
                }
            }
            return null;
        }
    }
}
