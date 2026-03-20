using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Game.Data;
using Game.Saving;
using Game.GameFlow;
using ProjectBase;

namespace Game.UI
{
    /// <summary>挂在存档列表每一行上，用于识别“存档选项”并携带 saveId（Blocker 右键射线检测用）。</summary>
    public class SaveListRowMarker : MonoBehaviour
    {
        public string SaveId { get; set; }
    }

    /// <summary>
    /// 存档选择面板：加载存档时弹出，仅列出已存在的存档，每项显示玩家名字和关卡；左键点击加载，右键点击弹出存档菜单（重置/删除）。
    /// 右键点到存档行会打开/切换到该存档的菜单；只有右键点到非存档区域时才关闭菜单。左键点菜单外仍会关闭。
    /// 子控件约定：Content（带 VerticalLayoutGroup 的父物体）、Btn_Close；可选 SaveContextMenuPanel（内含 Blocker、MenuBody、Btn_Reset、Btn_Delete），不挂则运行时自动创建。
    /// </summary>
    public class SaveListPanel : BasePanel
    {
        [SerializeField] private GameObject rowPrefab;
        [Tooltip("列表根（如 Scroll View 下的 Content）；留空则脚本 Find(\"Content\")")]
        [SerializeField] private Transform contentRoot;

        private LevelGrowthSO _levelGrowth;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private GameObject _contextMenuPanel;
        private Button _btnReset;
        private Button _btnDelete;
        private string _contextSaveId;

        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () =>
            {
                UIManager.GetInstance().HidePanel(PanelNames.SaveList);
                UIManager.GetInstance().ShowPanel<MainMenuPanel>(
                    PanelNames.MainMenu, PanelLayers.MainMenu,
                    panel => panel.Init(_levelGrowth));
            });
            EnsureSaveContextMenu();
        }

        private void EnsureSaveContextMenu()
        {
            _contextMenuPanel = transform.Find("SaveContextMenuPanel")?.gameObject;
            if (_contextMenuPanel != null)
            {
                var blocker = _contextMenuPanel.transform.Find("Blocker");
                _btnReset = _contextMenuPanel.transform.Find("MenuBody/Btn_Reset")?.GetComponent<Button>();
                _btnDelete = _contextMenuPanel.transform.Find("MenuBody/Btn_Delete")?.GetComponent<Button>();
                if (blocker != null)
                    SetupBlockerPointerDown(blocker);
                if (_btnReset != null) _btnReset.onClick.AddListener(OnResetSave);
                if (_btnDelete != null) _btnDelete.onClick.AddListener(OnDeleteSave);
                _contextMenuPanel.SetActive(false);
                return;
            }
            CreateSaveContextMenu();
        }

        private void CreateSaveContextMenu()
        {
            _contextMenuPanel = new GameObject("SaveContextMenuPanel");
            _contextMenuPanel.transform.SetParent(transform, false);
            var rt = _contextMenuPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(_contextMenuPanel.transform, false);
            var blockRt = blocker.AddComponent<RectTransform>();
            blockRt.anchorMin = Vector2.zero;
            blockRt.anchorMax = Vector2.one;
            blockRt.offsetMin = Vector2.zero;
            blockRt.offsetMax = Vector2.zero;
            var blockImg = blocker.AddComponent<Image>();
            blockImg.color = new Color(0, 0, 0, 0.01f);
            blockImg.raycastTarget = true;
            var blockTrigger = blocker.AddComponent<EventTrigger>();
            var blockEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            blockEntry.callback.AddListener(data => OnBlockerPointerDown(data as PointerEventData));
            blockTrigger.triggers.Add(blockEntry);

            var menuBody = new GameObject("MenuBody");
            menuBody.transform.SetParent(_contextMenuPanel.transform, false);
            var bodyRt = menuBody.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRt.sizeDelta = new Vector2(200, 100);
            var bodyImg = menuBody.AddComponent<Image>();
            bodyImg.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            bodyImg.raycastTarget = true;

            _btnReset = CreateContextButton(menuBody.transform, "Btn_Reset", "重置", new Vector2(0, 15));
            _btnDelete = CreateContextButton(menuBody.transform, "Btn_Delete", "删除", new Vector2(0, -15));
            _btnReset.onClick.AddListener(OnResetSave);
            _btnDelete.onClick.AddListener(OnDeleteSave);

            _contextMenuPanel.SetActive(false);
        }

        private static Button CreateContextButton(Transform parent, string name, string label, Vector2 anchoredY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredY;
            rt.sizeDelta = new Vector2(160, 36);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            var btn = go.AddComponent<Button>();
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 18;
            return btn;
        }

        private void ShowSaveContextMenu(string saveId, Vector2 screenPos)
        {
            _contextSaveId = saveId;
            if (_contextMenuPanel == null) return;
            var body = _contextMenuPanel.transform.Find("MenuBody");
            if (body != null)
            {
                var panelRt = _contextMenuPanel.GetComponent<RectTransform>();
                if (panelRt != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRt, screenPos, null, out var local))
                    body.GetComponent<RectTransform>().anchoredPosition = local;
            }
            _contextMenuPanel.SetActive(true);
        }

        private void SetupBlockerPointerDown(Transform blocker)
        {
            var trigger = blocker.GetComponent<EventTrigger>() ?? blocker.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.RemoveAll(e => e.eventID == EventTriggerType.PointerDown);
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(data => OnBlockerPointerDown(data as PointerEventData));
            trigger.triggers.Add(entry);
        }

        /// <summary>左键：关闭菜单。右键：若射线命中存档行则打开该存档菜单，否则关闭。</summary>
        private void OnBlockerPointerDown(PointerEventData ped)
        {
            if (ped == null) { HideSaveContextMenu(); return; }
            if (ped.button == PointerEventData.InputButton.Left)
            {
                HideSaveContextMenu();
                return;
            }
            if (ped.button == PointerEventData.InputButton.Right)
            {
                // 菜单在上层会挡住列表，先暂时关闭菜单层射线再检测，才能命中下面的存档行
                var graphics = _contextMenuPanel != null ? _contextMenuPanel.GetComponentsInChildren<Graphic>(true) : null;
                if (graphics != null)
                {
                    foreach (var g in graphics) g.raycastTarget = false;
                }
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, results);
                if (graphics != null)
                {
                    foreach (var g in graphics) g.raycastTarget = true;
                }
                foreach (var r in results)
                {
                    var marker = r.gameObject.GetComponentInParent<SaveListRowMarker>();
                    if (marker != null)
                    {
                        ShowSaveContextMenu(marker.SaveId, ped.position);
                        return;
                    }
                }
                HideSaveContextMenu();
            }
        }

        private void HideSaveContextMenu()
        {
            if (_contextMenuPanel != null)
                _contextMenuPanel.SetActive(false);
            _contextSaveId = null;
        }

        private void OnResetSave()
        {
            if (string.IsNullOrEmpty(_contextSaveId)) return;
            SaveSystem.GetInstance().ResetSave(_contextSaveId);
            HideSaveContextMenu();
            RefreshList();
        }

        private void OnDeleteSave()
        {
            if (string.IsNullOrEmpty(_contextSaveId)) return;
            SaveSystem.GetInstance().Delete(_contextSaveId);
            HideSaveContextMenu();
            RefreshList();
        }

        public void Init(LevelGrowthSO levelGrowth)
        {
            _levelGrowth = levelGrowth;
            RefreshList();
        }

        private void RefreshList()
        {
            Transform content = contentRoot != null ? contentRoot : transform.Find("Content");
            if (content == null)
            {
                Debug.LogWarning("[SaveListPanel] 未找到 Content，请指定 Content Root 或在面板下建 Content");
                return;
            }

            foreach (var go in _rows)
            {
                if (go != null) Destroy(go);
            }
            _rows.Clear();

            var entries = SaveSystem.GetInstance().GetAllSaveEntries();
            if (entries == null || entries.Count == 0) return;

            if (rowPrefab == null)
            {
                Debug.LogWarning("[SaveListPanel] 请在 Inspector 指定 Row Prefab（一行：Button + Text 显示玩家名）");
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                GameObject row = Instantiate(rowPrefab, content);
                row.SetActive(true);
                _rows.Add(row);

                var text = row.GetComponentInChildren<Text>();
                if (text != null)
                    text.text = $"{entry.playerName} 关卡{entry.levelIndex}";

                string id = entry.id;
                var marker = row.GetComponent<SaveListRowMarker>() ?? row.AddComponent<SaveListRowMarker>();
                marker.SaveId = id;
                var btn = row.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnSelectSave(id));
                }

                var trigger = row.GetComponent<EventTrigger>();
                if (trigger == null) trigger = row.AddComponent<EventTrigger>();
                var rightClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                rightClick.callback.AddListener(data =>
                {
                    var ped = (PointerEventData)data;
                    if (ped.button == PointerEventData.InputButton.Right)
                        ShowSaveContextMenu(id, ped.position);
                });
                trigger.triggers.Add(rightClick);
            }
        }

        private void OnSelectSave(string saveId)
        {
            UIManager.GetInstance().HidePanel(PanelNames.SaveList);
            UIManager.GetInstance().HidePanel(PanelNames.MainMenu);
            // 不隐藏 MainMenuBackground，加载时保留主界面背景 + LoadingPanel
            UIManager.GetInstance().ShowPanel<LoadingPanel>(PanelNames.Loading, PanelLayers.Loading, _ =>
            {
                bool ok = GameStateMachine.GetInstance().LoadGame(saveId, _levelGrowth, () =>
                {
                    UIManager.GetInstance().HidePanel(PanelNames.Loading);
                    UIManager.GetInstance().HidePanel(PanelNames.MainMenuBackground);
                });
                if (!ok)
                {
                    UIManager.GetInstance().HidePanel(PanelNames.Loading);
                    Debug.LogWarning("[SaveListPanel] 加载存档失败");
                }
            });
        }

        public override void ShowMe() => gameObject.SetActive(true);
        public override void HideMe()
        {
            HideSaveContextMenu();
            gameObject.SetActive(false);
        }
    }
}
