using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public class CharacterAnimationEntry
    {
        [InspectorLabel("动画ID")]
        public string animationId = "";

        [InspectorLabel("动画片段")]
        public AnimationClip clip;
    }

    [Serializable]
    public class CharacterAnimationGroupDefinition
    {
        [InspectorLabel("分组ID")]
        public string groupId = "default";

        [InspectorLabel("分组名称")]
        public string groupName = "默认分组";

        [InspectorLabel("动画列表")]
        public List<CharacterAnimationEntry> entries = new List<CharacterAnimationEntry>();
    }

    [CreateAssetMenu(menuName = "游戏/配置/动画库", fileName = "动画库")]
    public class CharacterAnimationLibrarySO : ScriptableObject
    {
        [InspectorLabel("动画分组")]
        public List<CharacterAnimationGroupDefinition> groups = new List<CharacterAnimationGroupDefinition>();

        public CharacterAnimationEntry GetEntry(string animationId)
        {
            if (string.IsNullOrWhiteSpace(animationId) || groups == null)
                return null;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group?.entries == null)
                    continue;

                for (int i = 0; i < group.entries.Count; i++)
                {
                    var entry = group.entries[i];
                    if (entry != null && entry.animationId == animationId)
                        return entry;
                }
            }

            return null;
        }
    }
}
