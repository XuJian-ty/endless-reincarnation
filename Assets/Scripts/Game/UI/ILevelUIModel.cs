using System;
using Game.Domain;
using Game.Saving;

namespace Game.UI
{
    /// <summary>
    /// 关卡内 UI 数据源：由 LevelBootstrapper 在进关卡时注册，面板通过 UIManager.GetLevelUIModel() 获取。
    /// 减少面板对 GameStateMachine 单例的直接依赖，便于测试与数据驱动。
    /// </summary>
    public interface ILevelUIModel
    {
        PlayerModel Player { get; }
        RunData CurrentRun { get; }
        string CurrentPlayerName { get; }

        /// <summary>订阅背包/属性变更，用于 HUD、背包面板等刷新。</summary>
        void SubscribeInventoryChanged(Action callback);
        void UnsubscribeInventoryChanged(Action callback);
    }
}
