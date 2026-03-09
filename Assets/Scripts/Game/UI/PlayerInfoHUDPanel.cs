using UnityEngine;
using UnityEngine.UI;
using Game.Domain;
using Game.GameFlow;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 玩家信息HUD面板：左上角常驻显示，不影响游戏操作。
    /// 优化：从 ILevelUIModel 取数，事件/脏标记驱动刷新，缓存控件引用，避免每帧全量 GetControl。
    /// </summary>
    public class PlayerInfoHUDPanel : BasePanel
    {
        // ── 缓存控件（Awake 后填充）──────────────────────────────────────
        private Text _playerNameText;
        private Text _levelText;
        private Slider _hpSlider;
        private Text _hpText;
        private Slider _mpSlider;
        private Text _mpText;
        private Slider _expSlider;
        private Text _expText;
        private Text _levelIndexText;
        private Text _difficultyText;

        private bool _dirty = true;
        private float _lastHp, _lastMp, _lastExp, _lastExpToNext;
        private int _lastLevel;
        private int _lastLevelIndex;
        private int _lastDifficulty;
        private string _lastPlayerName;

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
            _levelIndexText = GetControl<Text>("Text_LevelIndex");
            _difficultyText = GetControl<Text>("Text_Difficulty");
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            _dirty = true;
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
        }

        private void OnDisable()
        {
            var model = LevelUIModelLocator.Get();
            if (model != null)
            {
                model.UnsubscribeInventoryChanged(MarkDirty);
            }
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
            int levelIndex = run?.levelIndex ?? 0;
            int difficulty = run?.difficulty ?? 0;
            string playerName = model.CurrentPlayerName ?? "玩家";

            if (!_dirty && _lastHp == hp && _lastMp == mp && _lastExp == exp && _lastExpToNext == expToNext
                && _lastLevel == level && _lastLevelIndex == levelIndex && _lastDifficulty == difficulty
                && _lastPlayerName == playerName)
                return;

            _lastHp = hp; _lastMp = mp; _lastExp = exp; _lastExpToNext = expToNext;
            _lastLevel = level; _lastLevelIndex = levelIndex; _lastDifficulty = difficulty;
            _lastPlayerName = playerName;
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

            if (_hpSlider != null) { _hpSlider.maxValue = stats.MaxHp; _hpSlider.value = player.CurrentHp; }
            if (_hpText != null)   _hpText.text = $"{player.CurrentHp:F0}/{stats.MaxHp:F0}";

            if (_mpSlider != null) { _mpSlider.maxValue = stats.MaxMp; _mpSlider.value = player.CurrentMp; }
            if (_mpText != null)   _mpText.text = $"{player.CurrentMp:F0}/{stats.MaxMp:F0}";

            int expCap = player.Exp.ExpToNext;
            if (_expSlider != null) { _expSlider.maxValue = expCap; _expSlider.value = player.Exp.Exp; }
            if (_expText != null)   _expText.text = $"{player.Exp.Exp}/{expCap}";

            if (_levelIndexText != null && run != null) _levelIndexText.text = $"关卡： {run.levelIndex}";
            if (_difficultyText != null && run != null) _difficultyText.text = $"难度： {run.difficulty}";
        }
    }
}
