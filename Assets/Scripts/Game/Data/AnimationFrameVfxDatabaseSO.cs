using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 单条动画帧特效数据：与动画事件传入的「特效名」一一对应，用于该帧播放特效与音效。
    /// SO 无法可靠引用预制体子物体，故用挂点名；运行时在角色实例上 Find(挂点名) 取得挂点。
    /// </summary>
    [Serializable]
    public class AnimationFrameVfxEntry
    {
        [Header("与动画事件对应")]
        [InspectorLabel("特效名")]
        [Tooltip("必须与动画事件中传入的名称完全匹配，例如 'Slash1', 'HitSpark'")]
        public string effectName = "";

        [Header("特效与音效")]
        [InspectorLabel("特效")]
        [Tooltip("拖入特效预制体（如 VFX、粒子系统），可为空")]
        public GameObject effectPrefab;

        [InspectorLabel("音效")]
        [Tooltip("拖入 AudioClip，可为空")]
        public AudioClip soundClip;

        [Header("挂点")]
        [InspectorLabel("挂点名")]
        [Tooltip("角色/武器下的子物体名，运行时用 transform.Find(挂点名) 取得挂点；留空则用角色根节点")]
        public string mountPointName = "";

        /// <summary>是否配置了特效预制体</summary>
        public bool HasEffect => effectPrefab != null;
        /// <summary>是否配置了音效</summary>
        public bool HasSound => soundClip != null;
    }

    /// <summary>
    /// 动画帧特效数据配置库：动画某一帧调用播放特效方法时传入「特效名」，据此取特效、音效与挂点名。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/动画帧特效数据配置库", fileName = "动画帧特效数据配置库")]
    public class AnimationFrameVfxDatabaseSO : ScriptableObject
    {
        [InspectorLabel("特效数据列表")]
        [Tooltip("每条对应一个动画事件传入的特效名，含特效预制体、音效、挂点名（角色子物体名）")]
        public List<AnimationFrameVfxEntry> entries = new List<AnimationFrameVfxEntry>();

        /// <summary>根据特效名取一条；动画事件传入特效名后调用此方法。</summary>
        public AnimationFrameVfxEntry GetEntry(string effectName)
        {
            if (entries == null || string.IsNullOrEmpty(effectName)) return null;
            foreach (var e in entries)
                if (e.effectName == effectName) return e;
            return null;
        }
    }
}
