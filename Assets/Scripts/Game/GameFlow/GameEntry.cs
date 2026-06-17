using UnityEngine;
using Game.Data;
using Game.UI;
using Game;
using ProjectBase;

namespace Game.GameFlow
{
    /// <summary>
    /// 游戏入口（挂在主菜单场景的永久对象上）。配置由 ConfigManager 提供，无需挂载。
    /// </summary>
    public class GameEntry : MonoBehaviour
    {
        private void Start()
        {
            var levelGrowth = ConfigManager.GetInstance().GetLevelGrowth();
            if (levelGrowth == null)
                Debug.LogError("[GameEntry] Resources/配置/玩家成长 未创建，请用菜单「游戏→合并配置→一键创建全部配置」生成。");

            DropdownScrollForwarder.EnsureExistsInScene();

            UIManager.GetInstance().ShowPanel<MainMenuBackgroundPanel>(
                Game.UI.PanelNames.MainMenuBackground,
                Game.UI.PanelLayers.MainMenuBackground);
            // 主菜单面板在用户「点击任意键继续」后由 MainMenuBackgroundPanel 再显示
        }
    }
}
