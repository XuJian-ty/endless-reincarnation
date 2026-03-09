using UnityEngine;
using Game.Data;
using ProjectBase;

namespace Game.GameFlow
{
    /// <summary>
    /// 关卡 BGM 由 GameStateMachine.LoadGame 在 LoadSceneAsyn 回调中统一播放（进度条加满、进入关卡后），
    /// 不在此组件 Start 中播放，避免与加载时机重复或提前播放。此脚本保留可为将来扩展用。
    /// </summary>
    public class LevelBgmApplier : MonoBehaviour
    {
    }
}
