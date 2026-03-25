using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public sealed class HutaoDialogueOptionData
    {
        [InspectorLabel("答复内容")]
        [TextArea(2, 4)]
        public string replyText = "";

        [InspectorLabel("结束对话")]
        public bool endConversation;

        [InspectorLabel("结束后打开商店")]
        public bool openShopPanelOnEnd;
    }

    [Serializable]
    public sealed class HutaoDialogueRoundData
    {
        [InspectorLabel("胡桃台词")]
        [TextArea(2, 5)]
        public string hutaoLine = "";

        [InspectorLabel("答复选项")]
        public List<HutaoDialogueOptionData> options = new List<HutaoDialogueOptionData>();
    }

    /// <summary>
    /// 胡桃商店对话配置：按轮填写胡桃台词与玩家答复选项。
    /// 对话从胡桃台词开始，按 E 后进入本轮答复选择；确认答复后进入下一轮或结束。
    /// </summary>
    [CreateAssetMenu(menuName = "游戏/配置/胡桃商店对话", fileName = "胡桃商店对话")]
    public sealed class HutaoShopDialogueConfigSO : ScriptableObject
    {
        [InspectorLabel("角色名")]
        public string speakerName = "胡桃";

        [InspectorLabel("对话轮次")]
        public List<HutaoDialogueRoundData> rounds = new List<HutaoDialogueRoundData>();

        public bool HasRounds => rounds != null && rounds.Count > 0;

        public HutaoDialogueRoundData GetRound(int index)
        {
            if (rounds == null || index < 0 || index >= rounds.Count)
                return null;

            return rounds[index];
        }
    }
}

