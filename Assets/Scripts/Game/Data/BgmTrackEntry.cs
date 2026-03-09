using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// BGM 曲目配置项：显示名（下拉框与界面展示用）+ 对应的 AudioClip。主菜单与关卡 BGM 配置共用。
    /// </summary>
    [Serializable]
    public class BgmTrackEntry
    {
        [InspectorLabel("曲目名称")]
        [Tooltip("下拉框里显示的标签，可自定如「主菜单」「关卡1」等")]
        public string displayName = "曲目1";

        [InspectorLabel("音频")]
        [Tooltip("拖入对应的 AudioClip")]
        public AudioClip clip;
    }
}
