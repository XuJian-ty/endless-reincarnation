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
        /// <summary>存档头像资源标识。当前默认使用 UI图片/天依。 </summary>
        public string  portraitId;
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
        public WeaponInstance    meleeEquippedWeapon;
        public WeaponInstance    rangedEquippedWeapon;
        public int              currentAttackMode;
        public List<string> buffIds          = new List<string>();
        /// <summary>为兼容旧字段名仍保留 unlockedSkillIds，实际存储的是玩家已解锁技能/被动的 actionId。</summary>
        public List<string> unlockedSkillIds = new List<string>();
        /// <summary>技能树槽位到主动技能 actionId 的映射。索引即槽位索引，空字符串表示该槽位未装备技能。</summary>
        public List<string> equippedSkillActionIds = new List<string>();
        /// <summary>技能动作到当前所选变异 skillId 的映射；缺省时回退到动作配置中的默认 skillId。</summary>
        public List<SkillMutationSelectionSave> selectedSkillMutations = new List<SkillMutationSelectionSave>();
        /// <summary>已经使用专属道具永久解锁的技能变异方向。</summary>
        public List<SkillMutationUnlockSave> unlockedSkillMutations = new List<SkillMutationUnlockSave>();
        public List<string> defeatedBossIds  = new List<string>();
        public bool         isGameCleared;

        /// <summary>按键配置覆盖（格式同 KeyConfigPanel：actionId,index,path;...），仅对当前存档有效</summary>
        public string keyConfigOverrides = "";

        /// <summary>上次击败 Boss 时的快照，用于死亡复活回滚</summary>
        public RunData       checkpoint;
        /// <summary>进入新关卡后是否需要弹出 Buff 三选一；不再借用 levelSnapshot 是否为空来判断。</summary>
        public bool          pendingBuffSelection;
        /// <summary>当前关卡选择的 Buff；复活或重打本关时会移除并重新选择。</summary>
        public string        currentLevelBuffId;
        /// <summary>当前关卡内的场景快照；为 null 表示新进关卡</summary>
        public LevelSnapshot levelSnapshot;
    }

    [Serializable]
    public class SkillMutationSelectionSave
    {
        public string actionId;
        public string skillId;
    }

    [Serializable]
    public class SkillMutationUnlockSave
    {
        public string actionId;
        public string skillId;
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
        /// <summary>统一 50 格背包。</summary>
        public List<InventorySlotSave> slots = new List<InventorySlotSave>();
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
        public int   runtimeSnapshotVersion;
        public int   seed;
        public float playerX, playerY, playerZ;
        public float playerYaw;
        public List<EnemySnapshot>  enemies      = new List<EnemySnapshot>();
        public List<ChestSnapshotSave> chests = new List<ChestSnapshotSave>();
        public List<string>         openedChestIds = new List<string>();
        public List<GroundDropSave> groundDrops  = new List<GroundDropSave>();
        public List<ShopSnapshotSave> shops = new List<ShopSnapshotSave>();
        public GlobalSpawnerSnapshotSave globalSpawner;
        public List<LocalSpawnerSnapshotSave> localSpawners = new List<LocalSpawnerSnapshotSave>();
        public RogueliteRegionFlowSnapshotSave regionFlow;
        public LevelDirectorSnapshotSave levelDirector;
    }

    [Serializable]
    public class ChestSnapshotSave
    {
        public string snapshotId;
        public string prefabId;
        public float x, y, z;
        public float yaw;
    }

    [Serializable]
    public class EnemySnapshot
    {
        public string id;
        public string runtimeId;
        public int    enemyType; // 0近战 1远程 2精英 3守卫者 4Boss
        public float  x, y, z;
        public float  yaw;
        public float  currentHp;
        public float  currentPoise;
        public float  hurtRemainingTime;
        public float  idleRemainingTime;
        public float  decisionLockRemainingTime;
        public bool   countsAsLevelBoss;

        /// <summary>AI 状态：当前意图类型，枚举值见 EnemyIntentType。</summary>
        public int intentType;
        /// <summary>当前意图是否带目标点。</summary>
        public bool hasTarget;
        /// <summary>当前意图目标点的 XZ。</summary>
        public float lastTargetX, lastTargetZ;
        public List<EnemySkillCooldownSave> skillCooldowns = new List<EnemySkillCooldownSave>();
    }

    [Serializable]
    public class EnemySkillCooldownSave
    {
        public int slot;
        public float remainingTime;
    }

    [Serializable]
    public class GroundDropSave
    {
        public string itemType; // "gold" / "weapon" / "potion" 等
        public string payload;  // JSON 或 id
        public float  x, y, z;
    }

    [Serializable]
    public class ShopSnapshotSave
    {
        public string shopId;
        public List<ShopOfferSave> offers = new List<ShopOfferSave>();
    }

    [Serializable]
    public class ShopOfferSave
    {
        public bool isWeapon;
        public string stackItemId;
        public string weaponId;
        public WeaponRarity weaponRarity;
        public int remainingCount;
    }

    [Serializable]
    public class SpawnTaskRequestSave
    {
        public int taskIndex;
        public int spawnCount;
        public int remainingRetries;
        public int taskSeed;
    }

    [Serializable]
    public class GlobalSpawnerSnapshotSave
    {
        public string spawnerId;
        public bool isFinished;
        public float taskSpawnTimer;
        public List<SpawnTaskRequestSave> pendingTasks = new List<SpawnTaskRequestSave>();
    }

    [Serializable]
    public class LocalSpawnerSnapshotSave
    {
        public string spawnerId;
        public bool isActivated;
        public bool initialTaskStarted;
        public bool isFinished;
        public int executedTaskCount;
        public int attemptCount;
        public float taskTimer;
        public List<int> plannedSpawnCounts = new List<int>();
    }

    [Serializable]
    public class RogueliteRegionFlowSnapshotSave
    {
        public string flowId;
        public string currentRegionId;
        public List<string> enteredRegionIds = new List<string>();
        public List<string> clearedRegionIds = new List<string>();
        public List<string> buffClaimedRegionIds = new List<string>();
    }

    [Serializable]
    public class LevelDirectorSnapshotSave
    {
        public string directorId;
        public bool bossSpawned;
        public bool bossDefeated;
        public bool hasPendingBossSpawn;
        public float bossSpawnRemainingTime;
        public string defeatedBossId;
        public bool hasPendingBossResult;
        public bool bossResultPanelOpen;
        public float bossResultRemainingTime;
    }
}
