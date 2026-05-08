using Game.Data;
using UnityEngine;
using UnityEngine.UI;
using Game.Domain;
using Game.GameFlow;
using Game.Social;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 玩家信息HUD面板：左上角常驻显示，不影响游戏操作。
    /// 优化：从 ILevelUIModel 取数，事件/脏标记驱动刷新，缓存控件引用，避免每帧全量 GetControl。
    /// </summary>
    public class PlayerInfoHUDPanel : BasePanel
    {
        [SerializeField] private Sprite socialHubButtonBackground;

        // ── 缓存控件（Awake 后填充）──────────────────────────────────────
        private Text _playerNameText;
        private Text _levelText;
        private Slider _hpSlider;
        private Text _hpText;
        private Slider _mpSlider;
        private Text _mpText;
        private Slider _expSlider;
        private Text _expText;
        private Text _goldText;
        private Text _levelIndexText;
        private Text _difficultyText;
        private Image _portraitImage;
        private Button _portraitButton;
        private Button _socialHubButton;
        private bool _socialButtonBuilt;

        private bool _dirty = true;
        private float _lastHp, _lastMp, _lastExp, _lastExpToNext;
        private int _lastLevel;
        private int _lastGold;
        private int _lastLevelIndex;
        private int _lastDifficulty;
        private string _lastPlayerName;
        private string _lastPortraitId;

        protected override void Awake()
        {
            base.Awake();
            CacheControls();
        }

        private void CacheControls()
        {
            _playerNameText = GetControl<Text>("Text_PlayerName");
            _levelText      = GetControl<Text>("Text_Level");
            _hpSlider       = GetControl<Slider>("Slider_HP");
            _hpText         = GetControl<Text>("Text_HP");
            _mpSlider       = GetControl<Slider>("Slider_MP");
            _mpText         = GetControl<Text>("Text_MP");
            _expSlider      = GetControl<Slider>("Slider_Exp");
            _expText        = GetControl<Text>("Text_Exp");
            _goldText       = GetControl<Text>("GoldText");
            _levelIndexText = GetControl<Text>("Text_LevelIndex");
            _difficultyText = GetControl<Text>("Text_Difficulty");
            _portraitImage  = GetControl<Image>("Img_Avatar");
            EnsurePortraitButton();
            EnsureSocialButton();
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            _dirty = true;
            RefreshSocialButton();
            RefreshIfDirty();
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            var model = LevelUIModelLocator.Get();
            if (model != null)
            {
                model.SubscribeInventoryChanged(MarkDirty);
            }

            SocialSession.GetInstance().SessionChanged += RefreshSocialButton;
            RefreshSocialButton();
        }

        private void OnDisable()
        {
            var model = LevelUIModelLocator.Get();
            if (model != null)
            {
                model.UnsubscribeInventoryChanged(MarkDirty);
            }

            SocialSession.GetInstance().SessionChanged -= RefreshSocialButton;
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            var model = LevelUIModelLocator.Get();
            if (model?.Player == null) return;

            var stats = model.Player.Stats;
            var run   = model.CurrentRun;
            float hp  = model.Player.CurrentHp;
            float mp  = model.Player.CurrentMp;
            float exp = model.Player.Exp.Exp;
            float expToNext = model.Player.Exp.ExpToNext;
            int level = model.Player.Exp.Level;
            int gold = model.Player.Gold;
            int levelIndex = run?.levelIndex ?? 0;
            int difficulty = run?.difficulty ?? 0;
            string playerName = model.CurrentPlayerName ?? "玩家";
            string portraitId = model.CurrentPortraitId;

            if (!_dirty && _lastHp == hp && _lastMp == mp && _lastExp == exp && _lastExpToNext == expToNext
                && _lastLevel == level && _lastGold == gold && _lastLevelIndex == levelIndex && _lastDifficulty == difficulty
                && _lastPlayerName == playerName && _lastPortraitId == portraitId)
                return;

            _lastHp = hp; _lastMp = mp; _lastExp = exp; _lastExpToNext = expToNext;
            _lastLevel = level; _lastGold = gold; _lastLevelIndex = levelIndex; _lastDifficulty = difficulty;
            _lastPlayerName = playerName;
            _lastPortraitId = portraitId;
            _dirty = false;
            RefreshAll(model);
        }

        private void RefreshIfDirty()
        {
            if (!_dirty) return;
            var model = LevelUIModelLocator.Get();
            if (model?.Player == null) return;
            RefreshAll(model);
            _dirty = false;
        }

        private void RefreshAll(ILevelUIModel model)
        {
            var player = model.Player;
            var stats  = player.Stats;
            var run    = model.CurrentRun;

            string displayName = model.CurrentPlayerName ?? "玩家";
            if (_playerNameText != null) _playerNameText.text = displayName;
            if (_levelText != null)      _levelText.text      = $"Lv.{player.Exp.Level}";

            if (_portraitImage != null)
            {
                Sprite portrait = PlayerPortraitUtility.LoadPortraitSprite(model.CurrentPortraitId);
                if (portrait != null)
                {
                    _portraitImage.sprite = portrait;
                    _portraitImage.preserveAspect = true;
                }
            }

            if (_hpSlider != null) { _hpSlider.maxValue = stats.MaxHp; _hpSlider.value = player.CurrentHp; }
            if (_hpText != null)   _hpText.text = $"{player.CurrentHp:F0}/{stats.MaxHp:F0}";

            if (_mpSlider != null) { _mpSlider.maxValue = stats.MaxMp; _mpSlider.value = player.CurrentMp; }
            if (_mpText != null)   _mpText.text = $"{player.CurrentMp:F0}/{stats.MaxMp:F0}";

            int expCap = player.Exp.ExpToNext;
            if (_expSlider != null) { _expSlider.maxValue = expCap; _expSlider.value = player.Exp.Exp; }
            if (_expText != null)   _expText.text = $"{player.Exp.Exp}/{expCap}";
            if (_goldText != null)  _goldText.text = $"金币：{player.Gold}";

            if (_levelIndexText != null && run != null) _levelIndexText.text = $"关卡： {run.levelIndex}";
            if (_difficultyText != null && run != null) _difficultyText.text = $"难度： {run.difficulty}";
        }

        private void EnsurePortraitButton()
        {
            if (_portraitImage == null || _portraitButton != null)
                return;

            _portraitImage.raycastTarget = true;
            _portraitButton = _portraitImage.GetComponent<Button>();
            if (_portraitButton == null)
                _portraitButton = _portraitImage.gameObject.AddComponent<Button>();

            _portraitButton.transition = Selectable.Transition.None;
            _portraitButton.targetGraphic = _portraitImage;
            _portraitButton.onClick.RemoveAllListeners();
            _portraitButton.onClick.AddListener(() =>
            {
                UIManager.GetInstance()?.ShowPanel<PlayerProfilePanel>(PanelNames.PlayerProfile, PanelLayers.PlayerProfile);
            });
        }

        private void EnsureSocialButton()
        {
            if (_socialButtonBuilt)
                return;

            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            GameObject buttonObject = new GameObject("Btn_SocialHub", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f, 24f);
            rect.sizeDelta = socialHubButtonBackground != null
                ? new Vector2(58f, 56f)
                : new Vector2(112f, 44f);

            Image image = buttonObject.GetComponent<Image>();
            if (socialHubButtonBackground != null)
            {
                image.sprite = socialHubButtonBackground;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else
            {
                image.color = new Color(0.18f, 0.45f, 0.72f, 0.92f);
            }
            image.raycastTarget = true;

            _socialHubButton = buttonObject.GetComponent<Button>();
            _socialHubButton.targetGraphic = image;
            _socialHubButton.onClick.AddListener(() =>
            {
                UIManager.GetInstance().ShowPanel<SocialHubPanel>(PanelNames.SocialHub, PanelLayers.SocialHub);
            });

            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.GetComponent<Text>();
            label.text = "社交";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = socialHubButtonBackground != null ? 10 : 20;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = socialHubButtonBackground != null ? Color.black : Color.white;
            label.raycastTarget = false;

            _socialButtonBuilt = true;
            RefreshSocialButton();
        }

        private void RefreshSocialButton()
        {
            if (_socialHubButton == null)
                return;

            bool loggedIn = SocialSession.GetInstance().IsLoggedIn;
            _socialHubButton.interactable = loggedIn;

            Image image = _socialHubButton.targetGraphic as Image;
            if (image != null)
            {
                bool hasCustomBackground = image.sprite == socialHubButtonBackground && socialHubButtonBackground != null;
                image.color = hasCustomBackground
                    ? (loggedIn ? Color.white : new Color(0.65f, 0.65f, 0.65f, 0.7f))
                    : (loggedIn ? new Color(0.18f, 0.45f, 0.72f, 0.92f) : new Color(0.25f, 0.28f, 0.32f, 0.7f));
            }
        }
    }
}
