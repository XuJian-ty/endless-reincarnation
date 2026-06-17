using System;
using Game.Domain;
using Game;
using Game.GameFlow;
using Game.Saving;
using ProjectBase;

namespace Game.UI
{
    /// <summary>Level-scoped UI model wrapping GameStateMachine and inventory refresh event forwarding.</summary>
    public sealed class LevelUIModel : ILevelUIModel, IDisposable
    {
        private readonly GameStateMachine _gsm;
        private Action _inventoryChangedCallback;
        private bool _inventoryListenerRegistered;

        public LevelUIModel(GameStateMachine gsm)
        {
            _gsm = gsm ?? throw new ArgumentNullException(nameof(gsm));
        }

        public PlayerModel Player => _gsm?.Player;
        public RunData CurrentRun => _gsm?.CurrentRun;
        public string CurrentPlayerName => _gsm?.CurrentPlayerName ?? "玩家";
        public string CurrentPortraitId => _gsm?.CurrentPortraitId ?? SaveSystem.DefaultPortraitId;

        public void SubscribeInventoryChanged(Action callback)
        {
            if (callback == null) return;

            // De-duplicate identical callbacks to avoid accidental double subscription.
            _inventoryChangedCallback -= callback;
            _inventoryChangedCallback += callback;
            EnsureInventoryListenerRegistered();
        }

        public void UnsubscribeInventoryChanged(Action callback)
        {
            if (callback == null) return;
            if (_inventoryChangedCallback == null) return;

            _inventoryChangedCallback -= callback;
            if (_inventoryChangedCallback == null)
                EnsureInventoryListenerUnregistered();
        }

        public void Dispose()
        {
            _inventoryChangedCallback = null;
            EnsureInventoryListenerUnregistered();
        }

        private void OnInventoryChanged()
        {
            _inventoryChangedCallback?.Invoke();
        }

        private void EnsureInventoryListenerRegistered()
        {
            if (_inventoryListenerRegistered)
                return;

            EventCenter.GetInstance().AddEventListener(GameEvents.InventoryChanged, OnInventoryChanged);
            _inventoryListenerRegistered = true;
        }

        private void EnsureInventoryListenerUnregistered()
        {
            if (!_inventoryListenerRegistered)
                return;

            var ec = EventCenter.GetInstance();
            ec?.RemoveEventListener(GameEvents.InventoryChanged, OnInventoryChanged);
            _inventoryListenerRegistered = false;
        }
    }
}
