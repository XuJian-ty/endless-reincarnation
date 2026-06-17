using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game;
using Game.Data;
using Game.GameFlow;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 按键配置面板：根据「按键重绑定配置」动态生成每一行，无需在预制体里手动摆行。
    /// 只需改配置文件即可增删按键项；KeyList 需带 VerticalLayoutGroup，行由代码创建并加入。
    /// 关闭按钮 Btn_Close 可放在 Bg 子物体下。
    /// 按键配置按当前存档独立保存，在哪个存档调节的只对哪个存档有效。
    /// </summary>
    public class KeyConfigPanel : BasePanel
    {
        [Header("输入与列表")]
        [SerializeField] private InputActionAsset inputActionAsset;
        [SerializeField] private Transform keyListRoot;
        [Tooltip("可选：行预制体（需挂 KeyConfigRow，子物体含 Label/KeyText/Btn_Rebind/Btn_Reset）。不填则用代码生成简易行。")]
        [SerializeField] private GameObject rowPrefab;

        private InputActionMap _gameplayMap;
        private InputActionMap _uiMap;
        private readonly List<KeyConfigRow> _rows = new List<KeyConfigRow>();
        private KeyRebindConfigSO _rebindConfig;

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            EnsureContentHeightFitsAllRows();
            StartCoroutine(EnsureContentHeightNextFrame());
        }
        private System.Collections.IEnumerator EnsureContentHeightNextFrame()
        {
            yield return null;
            EnsureContentHeightFitsAllRows();
        }
        public override void HideMe() => gameObject.SetActive(false);

        protected override void Awake()
        {
            base.Awake();
            RegisterClick("Btn_Close", () => UIManager.GetInstance().HidePanel(PanelNames.KeyConfig));
            if (inputActionAsset != null)
            {
                _gameplayMap = inputActionAsset.FindActionMap("Gameplay");
                _uiMap = inputActionAsset.FindActionMap("UI");
                LoadOverridesFromCurrentSave();
                BuildRows();
            }
        }

        private void OnDestroy()
        {
            _rows.Clear();
        }

        private void BuildRows()
        {
            if (_gameplayMap == null || keyListRoot == null) return;

            _rebindConfig = ConfigManager.GetInstance()?.GetKeyRebindConfig();
            if (_rebindConfig?.entries == null || _rebindConfig.entries.Count == 0)
            {
                Debug.LogWarning("[KeyConfigPanel] 未找到按键重绑定配置（Resources/配置/按键重绑定配置），请通过菜单创建或检查配置。");
                return;
            }

            _rows.Clear();
            ClearRowChildren();

            for (int i = 0; i < _rebindConfig.entries.Count; i++)
            {
                var entry = _rebindConfig.entries[i];
                var action = _gameplayMap?.FindAction(entry.actionName) ?? _uiMap?.FindAction(entry.actionName);
                if (action == null)
                {
                    Debug.LogWarning($"[KeyConfigPanel] 未找到 Action：{entry.actionName}，跳过：{entry.displayName}");
                    continue;
                }

                int bindingIndex = FindBindingIndex(action, entry.bindingPart);
                if (bindingIndex < 0)
                {
                    Debug.LogWarning($"[KeyConfigPanel] 未找到绑定：{entry.actionName}({entry.bindingPart})，跳过：{entry.displayName}");
                    continue;
                }

                GameObject rowGo = CreateOneRow();
                if (rowGo == null) continue;

                var row = rowGo.GetComponent<KeyConfigRow>();
                if (row == null) { Destroy(rowGo); continue; }

                row.Set(entry.displayName, action, bindingIndex, OnRebindClickedWithRow, OnResetClicked);
                _rows.Add(row);
            }

            EnsureContentHeightFitsAllRows();
        }

        private void ClearRowChildren()
        {
            if (keyListRoot == null) return;
            for (int i = keyListRoot.childCount - 1; i >= 0; i--)
            {
                var c = keyListRoot.GetChild(i);
                if (Application.isPlaying)
                    Destroy(c.gameObject);
                else
                    DestroyImmediate(c.gameObject);
            }
        }

        /// <summary>创建一行：优先用 rowPrefab 或 Resources/UI/KeyConfigRow；否则用代码生成简易行。</summary>
        private GameObject CreateOneRow()
        {
            GameObject prefab = rowPrefab != null ? rowPrefab : Resources.Load<GameObject>("UI/KeyConfigRow");
            if (prefab != null)
            {
                var go = Instantiate(prefab, keyListRoot);
                go.SetActive(true);
                return go;
            }
            return CreateRowByCode();
        }

        private GameObject CreateRowByCode()
        {
            var rowGo = new GameObject("KeyConfigRow");
            rowGo.transform.SetParent(keyListRoot, false);

            var rect = rowGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0, 28f);

            var le = rowGo.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;
            le.flexibleWidth = 1f;

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            Text labelText = CreateText(rowGo.transform, "Label", "动作名", 120f);
            Text keyText = CreateText(rowGo.transform, "KeyText", "键位", 100f);
            Button rebindBtn = CreateButton(rowGo.transform, "Btn_Rebind", "重绑", 60f);
            Button resetBtn = CreateButton(rowGo.transform, "Btn_Reset", "恢复默认", 72f);

            var row = rowGo.AddComponent<KeyConfigRow>();
            row.SetLabelAndKey(labelText, keyText, rebindBtn, resetBtn);

            return rowGo;
        }

        private static Text CreateText(Transform parent, string name, string defaultContent, float preferredWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = 0f;
            var text = go.AddComponent<Text>();
            text.text = defaultContent;
            text.fontSize = 14;
            text.supportRichText = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, float preferredWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = 24f;
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var btn = go.AddComponent<Button>();
            var child = new GameObject("Text");
            child.transform.SetParent(go.transform, false);
            var childRect = child.AddComponent<RectTransform>();
            childRect.anchorMin = Vector2.zero;
            childRect.anchorMax = Vector3.one;
            childRect.offsetMin = Vector2.zero;
            childRect.offsetMax = Vector2.zero;
            var childText = child.AddComponent<Text>();
            childText.text = label;
            childText.fontSize = 12;
            childText.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        private void EnsureContentHeightFitsAllRows()
        {
            if (keyListRoot == null) return;
            var rt = keyListRoot as RectTransform;
            if (rt == null) return;
            int n = keyListRoot.childCount;
            if (n == 0) return;
            for (int i = 0; i < n; i++)
            {
                var c = keyListRoot.GetChild(i) as RectTransform;
                if (c != null) LayoutRebuilder.ForceRebuildLayoutImmediate(c);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                var c = keyListRoot.GetChild(i) as RectTransform;
                if (c != null) total += c.rect.height;
            }
            var vlg = keyListRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                if (n > 1) total += vlg.spacing * (n - 1);
                total += vlg.padding.top + vlg.padding.bottom;
            }
            if (total > 0 && rt.rect.height < total)
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
        }

        private static int FindBindingIndex(InputAction action, string compositePart)
        {
            if (string.IsNullOrEmpty(compositePart))
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (!action.bindings[i].isComposite)
                        return i;
                }
                return 0;
            }

            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].isPartOfComposite && action.bindings[i].name == compositePart)
                    return i;
            }
            return -1;
        }

        private const string RebindPrompt = "按任意键...";

        private void OnRebindClickedWithRow(InputAction action, int bindingIndex, KeyConfigRow row)
        {
            if (action == null || row == null) return;

            row.SetKeyTextPrompt(RebindPrompt);
            foreach (var r in _rows) r.SetButtonsInteractable(false);
            action.Disable();

            var op = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(operation =>
                {
                    SaveOverrides();
                    foreach (var r in _rows) { r.RefreshKeyText(); r.SetButtonsInteractable(true); }
                    operation.Dispose();
                    action.Enable();
                })
                .OnCancel(operation =>
                {
                    foreach (var r in _rows) { r.RefreshKeyText(); r.SetButtonsInteractable(true); }
                    operation.Dispose();
                    action.Enable();
                });
            op.Start();
        }

        private void OnResetClicked(InputAction action, int bindingIndex)
        {
            if (action == null) return;
            action.RemoveBindingOverride(bindingIndex);
            SaveOverrides();
            foreach (var row in _rows)
                row.RefreshKeyText();
        }

        private void SaveOverrides()
        {
            var list = new List<string>();
            foreach (var map in new[] { _gameplayMap, _uiMap })
            {
                if (map == null) continue;
                foreach (var action in map.actions)
                {
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        var binding = action.bindings[i];
                        string over = binding.overridePath;
                        if (!string.IsNullOrEmpty(over))
                            list.Add($"{action.id},{i},{over}");
                    }
                }
            }
            var gsm = GameStateMachine.GetInstance();
            if (gsm != null && !string.IsNullOrEmpty(gsm.CurrentSaveId))
                gsm.SetKeyConfigOverrides(list.Count == 0 ? "" : string.Join(";", list));
        }

        private void LoadOverridesFromCurrentSave()
        {
            var gsm = GameStateMachine.GetInstance();
            string raw = gsm != null ? gsm.GetKeyConfigOverrides() : "";
            if (inputActionAsset != null && !string.IsNullOrEmpty(raw))
                ApplyOverridesTo(inputActionAsset, raw);
        }

        /// <summary>将存档中的按键覆盖应用到 InputActionAsset（关卡加载时由 GameplayInputEvents 调用，使当前存档的键位生效）。</summary>
        public static void ApplyOverridesTo(InputActionAsset asset, string raw)
        {
            if (asset == null || string.IsNullOrEmpty(raw)) return;
            var gameplayMap = asset.FindActionMap("Gameplay");
            var uiMap = asset.FindActionMap("UI");
            foreach (string seg in raw.Split(';'))
            {
                var parts = seg.Split(new[] { ',' }, 3);
                if (parts.Length < 3) continue;
                InputAction action = null;
                foreach (var map in new[] { gameplayMap, uiMap })
                {
                    if (map == null) continue;
                    foreach (var a in map.actions)
                    {
                        if (a.id.ToString() == parts[0]) { action = a; break; }
                    }
                    if (action != null) break;
                }
                if (action == null) continue;
                if (!int.TryParse(parts[1], out int idx) || idx < 0 || idx >= action.bindings.Count) continue;
                action.ApplyBindingOverride(idx, parts[2]);
            }
        }
    }
}
