using System;
using System.Collections.Generic;
using Game.UI;
using ProjectBase;
using UnityEngine;

namespace Game.GameFlow
{
    public static class BuffSelectionService
    {
        public static bool ShowThreeChoices(Action<string> onSelected)
        {
            var buffConfig = ConfigManager.GetInstance()?.GetBuffConfig();
            var availableBuffIds = buffConfig != null
                ? new List<string>(buffConfig.GetAllBuffIds())
                : new List<string>();
            if (availableBuffIds.Count < 3)
            {
                Debug.LogWarning("[BuffSelectionService] Buff 配置少于 3 条，无法打开三选一面板。");
                return false;
            }

            UIManager uiManager = UIManager.GetInstance();
            if (uiManager == null)
            {
                Debug.LogWarning("[BuffSelectionService] UIManager 未初始化，无法打开三选一面板。");
                return false;
            }

            var choices = new List<string>(3);
            for (int i = 0; i < 3; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, availableBuffIds.Count);
                choices.Add(availableBuffIds[randomIndex]);
                availableBuffIds.RemoveAt(randomIndex);
            }

            uiManager.ShowPanel<BuffSelectPanel>(
                PanelNames.BuffSelect,
                PanelLayers.BuffSelect,
                panel => panel.ShowWithBuffs(choices, onSelected));
            return true;
        }
    }
}
