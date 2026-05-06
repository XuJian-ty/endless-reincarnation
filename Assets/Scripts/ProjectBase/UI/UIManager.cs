using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.GameFlow;
using Game.UI;

namespace ProjectBase
{
/// <summary>
/// UI 层级枚举
/// </summary>
public enum E_UI_Layer
{
    Bot,
    Mid,
    Top,
    System,
}

/// <summary>
/// UI 管理器：管理所有显示的面板，提供显示/隐藏/获取接口。
/// Canvas 和 EventSystem 跨场景持久存在（DontDestroyOnLoad）。
/// </summary>
public class UIManager : BaseManager<UIManager>
{
    // ── 面板缓存（私有，通过 GetPanel<T> 访问）──────────────────────────
    private readonly Dictionary<string, BasePanel> _panelDic =
        new Dictionary<string, BasePanel>();

    // ── 层级 Transform ────────────────────────────────────────────────────
    private Transform _bot;
    private Transform _mid;
    private Transform _top;
    private Transform _system;

    /// <summary>Canvas 的 RectTransform（只读，外部仅用于位置计算）</summary>
    public RectTransform Canvas { get; private set; }

    public UIManager()
    {
        var obj = ResMgr.GetInstance().Load<GameObject>("UI/Canvas");
        Canvas = obj.transform as RectTransform;
        GameObject.DontDestroyOnLoad(obj);

        _bot    = Canvas.Find("Bot");
        _mid    = Canvas.Find("Mid");
        _top    = Canvas.Find("Top");
        _system = Canvas.Find("System");

        var existingEventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (existingEventSystem != null)
            GameObject.DontDestroyOnLoad(existingEventSystem.gameObject);
        else
        {
            obj = ResMgr.GetInstance().Load<GameObject>("UI/EventSystem");
            GameObject.DontDestroyOnLoad(obj);
        }
    }

    /// <summary>通过层级枚举得到对应层级的父对象</summary>
    public Transform GetLayerFather(E_UI_Layer layer) => layer switch
    {
        E_UI_Layer.Bot    => _bot,
        E_UI_Layer.Mid    => _mid,
        E_UI_Layer.Top    => _top,
        E_UI_Layer.System => _system,
        _                 => _bot
    };

    /// <summary>
    /// 显示面板。若已缓存则直接显示；否则异步加载后再显示。
    /// </summary>
    /// <typeparam name="T">面板脚本类型（继承自 BasePanel）</typeparam>
    /// <param name="panelName">与 Resources/UI/ 下预制体同名的字符串</param>
    /// <param name="layer">显示在哪一层</param>
    /// <param name="callBack">面板创建成功后的回调</param>
    public void ShowPanel<T>(string panelName, E_UI_Layer layer = E_UI_Layer.Mid,
                             UnityAction<T> callBack = null) where T : BasePanel
    {
        if (_panelDic.TryGetValue(panelName, out var cached))
        {
            cached.ShowMe();
            callBack?.Invoke(cached as T);
            TryPauseGameplayForPanel(panelName);
            return;
        }

        T existing = null;
        var allPanels = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allPanels.Length; i++)
        {
            if (allPanels[i].gameObject.name == panelName)
            {
                existing = allPanels[i];
                break;
            }
        }
        if (existing != null)
        {
            var father = GetLayerFather(layer);
            existing.transform.SetParent(father);
            existing.transform.localPosition = Vector3.zero;
            existing.transform.localScale = Vector3.one;
            var rect = existing.transform as RectTransform;
            if (rect != null)
            {
                rect.offsetMax = Vector2.zero;
                rect.offsetMin = Vector2.zero;
            }
            _panelDic[panelName] = existing;
            existing.ShowMe();
            callBack?.Invoke(existing);
            TryPauseGameplayForPanel(panelName);
            return;
        }

        ResMgr.GetInstance().LoadAsync<GameObject>("UI/" + panelName, obj =>
        {
            if (_panelDic.TryGetValue(panelName, out var loadedCached))
            {
                if (obj != null)
                    GameObject.Destroy(obj);

                loadedCached.ShowMe();
                callBack?.Invoke(loadedCached as T);
                TryPauseGameplayForPanel(panelName);
                return;
            }

            if (obj == null)
            {
                Debug.LogWarning($"[UIManager] 未找到面板预制体 '{panelName}'，将创建运行时回退面板。");
                obj = CreateRuntimeFallbackPanel<T>(panelName);
            }

            if (obj == null)
            {
                Debug.LogError($"[UIManager] 无法创建面板 '{panelName}'。");
                return;
            }

            var father = GetLayerFather(layer);
            obj.transform.SetParent(father);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localScale    = Vector3.one;

            var rect = obj.transform as RectTransform;
            if (rect != null)
            {
                rect.offsetMax = Vector2.zero;
                rect.offsetMin = Vector2.zero;
            }

            var panel = obj.GetComponent<T>();
            if (panel == null)
            {
                panel = obj.AddComponent<T>();
            }

            if (panel == null)
            {
                Debug.LogError($"[UIManager] 面板预制体 '{panelName}' 上未找到组件 {typeof(T).Name}，且补挂失败，已销毁该对象。");
                GameObject.Destroy(obj);
                return;
            }

            _panelDic[panelName] = panel;
            callBack?.Invoke(panel);
            panel.ShowMe();
            TryPauseGameplayForPanel(panelName);
        });
    }

    /// <summary>隐藏并销毁面板。先从字典移除再销毁，避免 OnDisable 等链里再次调用 HidePanel 导致同一物体被销毁两次。</summary>
    public void HidePanel(string panelName)
    {
        bool hidAnyPanel = false;
        if (!_panelDic.TryGetValue(panelName, out var panel))
            panel = null;
        else
            _panelDic.Remove(panelName);

        if (panel != null && panel.gameObject != null)
        {
            panel.HideMe();
            GameObject.Destroy(panel.gameObject);
            hidAnyPanel = true;
        }

        hidAnyPanel |= DestroyDetachedPanels(panelName, panel);
        if (!hidAnyPanel) return;

        TryResumeGameplayAfterPanelClosed(panelName);
        GameplayUIInputBridge.NotifyPanelHidden(panelName);
    }

    /// <summary>获取已显示的面板，不存在则返回 null</summary>
    public T GetPanel<T>(string name) where T : BasePanel
    {
        return _panelDic.TryGetValue(name, out var panel) ? panel as T : null;
    }

    /// <summary>给 UI 控件添加自定义事件监听</summary>
    public static void AddCustomEventListener(UIBehaviour control, EventTriggerType type,
                                              UnityAction<BaseEventData> callBack)
    {
        var trigger = control.GetComponent<EventTrigger>()
                   ?? control.gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callBack);
        trigger.triggers.Add(entry);
    }

    private static bool IsBlockingPanel(string panelName)
    {
        if (string.IsNullOrEmpty(panelName))
            return false;

        var blockingPanels = PanelNames.BlockingPanels;
        for (int i = 0; i < blockingPanels.Length; i++)
        {
            if (blockingPanels[i] == panelName)
                return true;
        }

        return false;
    }

    private void TryPauseGameplayForPanel(string panelName)
    {
        if (!IsBlockingPanel(panelName))
            return;

        var gsm = GameStateMachine.GetInstance();
        if (gsm == null || gsm.CurrentState != GameStateMachine.State.InLevel)
            return;

        gsm.Pause();
    }

    private void TryResumeGameplayAfterPanelClosed(string panelName)
    {
        if (!IsBlockingPanel(panelName))
            return;

        var gsm = GameStateMachine.GetInstance();
        if (gsm == null)
            return;

        if (gsm.CurrentState is not (GameStateMachine.State.InLevel or GameStateMachine.State.Paused))
            return;

        if (HasAnyBlockingPanelOpen())
            return;

        gsm.Resume();
    }

    private bool HasAnyBlockingPanelOpen()
    {
        var blockingPanels = PanelNames.BlockingPanels;
        for (int i = 0; i < blockingPanels.Length; i++)
        {
            if (_panelDic.TryGetValue(blockingPanels[i], out var panel) &&
                panel != null &&
                panel.gameObject != null &&
                panel.gameObject.activeSelf)
            {
                return true;
            }
        }

        return false;
    }

    private static bool DestroyDetachedPanels(string panelName, BasePanel ignoredPanel)
    {
        if (string.IsNullOrWhiteSpace(panelName))
            return false;

        bool destroyedAny = false;
        var allPanels = Object.FindObjectsByType<BasePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allPanels.Length; i++)
        {
            BasePanel panel = allPanels[i];
            if (panel == null || panel.gameObject == null)
                continue;
            if (panel == ignoredPanel)
                continue;

            string objectName = panel.gameObject.name;
            if (!string.Equals(objectName, panelName, System.StringComparison.Ordinal) &&
                !string.Equals(objectName, panelName + "(Clone)", System.StringComparison.Ordinal))
            {
                continue;
            }

            panel.HideMe();
            GameObject.Destroy(panel.gameObject);
            destroyedAny = true;
        }

        return destroyedAny;
    }

    private static GameObject CreateRuntimeFallbackPanel<T>(string panelName) where T : BasePanel
    {
        var obj = new GameObject(panelName, typeof(RectTransform));
        obj.AddComponent<CanvasRenderer>();
        obj.AddComponent<T>();
        return obj;
    }

}
}
