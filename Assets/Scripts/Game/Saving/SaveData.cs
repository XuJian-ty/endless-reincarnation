using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Saving
{
    /// <summary>
    /// 存档列表项（仅用于索引，不包含完整 RunData）。
    /// </summary>
    [Serializable]
    public class SaveEntry
    {
        public string id;
        public string playerName;
        public int    levelIndex;
        public long   lastModifiedTicks;
    }

    /// <summary>
    /// 存档根结构，用于 JSON 序列化（Newtonsoft.Json）。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int     version = 1;
        /// <summary>玩家在起名面板输入的名字，用于存档列表展示</summary>
        public string  playerName;
        public RunData run;
    }

    /// <summary>
    /// 一局游戏的全部运行时数据。
    /// </summary>
    [Serializable]
    public class RunData
    {
        public int  levelIndex = 1;
        public int  difficulty = 1;

        public PlayerSaveData    player    = new PlayerSaveData();
        public InventorySaveData inventory = new InventorySaveData();
        public ItemCountSaveData itemCounts;
        public WeaponInstance    equippedWeapon;
        /// <summary>兼容旧存档；新存档用 itemCounts.talentPoints</summary>
        public int talentPoints;
        public List<string> buffIds          = new List<string>();
        public List<string> unlockedSkillIds = new List<string>();
        public List<string> defeatedBossIds  = new List<string>();
        public bool         isGameCleared;

        /// <summary>游戏关卡内背景音乐是否开启，仅对当前存档有效</summary>
        public bool levelBgmEnabled = true;
        /// <summary>游戏关卡内背景音乐音量 0～1，仅对当前存档有效</summary>
        public float levelBgmVolume = 1f;
        /// <summary>游戏关卡内背景音乐曲目下拉索引，仅对当前存档有效</summary>
        public int levelBgmTrackIndex = 0;
        /// <summary>音效（除关卡 BGM 外）是否开启，仅对当前存档有效</summary>
        public bool soundEffectsEnabled = true;
        /// <summary>音效音量 0～1，仅对当前存档有效</summary>
        public float soundEffectsVolume = 1f;
        /// <summary>按键配置覆盖（格式同 KeyConfigPanel：actionId,index,path;...），仅对当前存档有效</summary>
        public string keyConfigOverrides = "";

        /// <summary>上次击败 Boss 时的快照，用于死亡复活回滚</summary>
        public RunData       checkpoint;
        /// <summary>当前关卡内的场景快照；为 null 表示新进关卡</summary>
        public LevelSnapshot levelSnapshot;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public int   level     = 1;
        public int   exp;
        public int   gold;
        public float currentHp = 100f;
        public float currentMp = 100f;
    }

    /// <summary>物品数量存档：金币、天赋点、以及仅数量的物品（药剂、仙露等）。</summary>
    [Serializable]
    public class ItemCountSaveData
    {
        public int gold;
        public int talentPoints;
        public List<ItemStackSave> items = new List<ItemStackSave>();
    }

    /// <summary>单格存档：武器或堆叠物二选一，与 InventorySlot 对应。</summary>
    [Serializable]
    public class InventorySlotSave
    {
        public WeaponInstance weapon;
        public string itemId;
        public int count;
    }

    [Serializable]
    public class InventorySaveData
    {
        /// <summary>统一 50 格背包；若为 null 或数量不足 50，按旧格式 weapons+stacks 兼容读取。</summary>
        public List<InventorySlotSave> slots = new List<InventorySlotSave>();
        /// <summary>兼容旧存档</summary>
        public List<WeaponInstance> weapons = new List<WeaponInstance>();
        public List<ItemStackSave> stacks = new List<ItemStackSave>();
    }

    [Serializable]
    public class ItemStackSave
    {
        public string itemId;
        public int    count;
    }

    [Serializable]
    public class LevelSnapshot
    {
        public int   seed;
        public float playerX, playerY, playerZ;
        public float playerYaw;
        public List<EnemySnapshot>  enemies      = new List<EnemySnapshot>();
        public List<string>         openedChestIds = new List<string>();
        public List<GroundDropSave> groundDrops  = new List<GroundDropSave>();
    }

    [Serializable]
    public class EnemySnapshot
    {
        public string id;
        public int    enemyType; // 0近战 1远程 2精英 3守卫者 4Boss
        public float  x, y, z;
        public float  currentHp;

        /// <summary>AI 状态：当前意图类型（0=None,1=Patrol,2=Chase,3=CastSkill,4=Hurt,5=Idle,6=Dead）</summary>
        public int intentType;
        /// <summary>是否正在追击（有目标）</summary>
        public bool hasTarget;
        /// <summary>丢失目标或存档时记录的最后目标 XZ（Y 可选）</summary>
        public float lastTargetX, lastTargetZ;
    }

    [Serializable]
    public class GroundDropSave
    {
        public string itemType; // "gold" / "weapon" / "potion" 等
        public string payload;  // JSON 或 id
        public float  x, y, z;
    }
}
