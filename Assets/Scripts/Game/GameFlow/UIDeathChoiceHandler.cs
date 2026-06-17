using System;
using Game.Domain;
using Game.UI;
using ProjectBase;

namespace Game.GameFlow
{
    /// <summary>
    /// 关卡内死亡二选一 UI 桥接：由流程层请求，转成面板展示。
    /// </summary>
    public sealed class UIDeathChoiceHandler : IDeathChoiceHandler
    {
        public void RequestDeathChoice(Action onReviveWithNectar, Action onGiveUp)
        {
            var gsm = GameStateMachine.GetInstance();
            var ui = UIManager.GetInstance();
            if (gsm == null || ui == null)
            {
                onGiveUp?.Invoke();
                return;
            }

            bool canRevive = gsm.CanReviveWithNectar();
            int nectarCount = gsm.Player?.GetItemCount(PlayerModel.ItemIds.Nectar) ?? 0;

            ui.ShowPanel<DeathChoicePanel>(
                PanelNames.DeathChoice,
                PanelLayers.DeathChoice,
                panel => panel.ShowChoice(canRevive, nectarCount, onReviveWithNectar, onGiveUp));
        }
    }
}
