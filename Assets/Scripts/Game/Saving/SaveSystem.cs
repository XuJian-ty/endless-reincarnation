using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Game;
using Game.Data;
using Game.Social;
using ProjectBase;

namespace Game.Saving
{
    /// <summary>
    /// 存档系统：存档数量不限，按 id 存储，每份存档含玩家名字；索引存于 saves_index.json。
    /// </summary>
    public class SaveSystem : BaseManager<SaveSystem>
    {
        public const int CurrentVersion = 6;
        public const string DefaultPortraitId = "UI图片/天依";

        private ISaveStorage _storage;
        private RemoteSaveStorage _remoteStorage;
        private FileSaveStorage _fileStorage;
        private List<SaveEntry> _index = new List<SaveEntry>();
        private static string IndexPath => Path.Combine(GetCurrentUserSaveRoot(), "saves_index.json");

        public ISaveStorage Storage
        {
            get => _storage ??= CreateDefaultStorage();
            set => _storage = value;
        }

        public SaveSystem()
        {
            SocialSession.GetInstance().SessionChanged += HandleSessionChanged;
        }

        private ISaveStorage CreateDefaultStorage()
        {
            if (SocialSession.GetInstance().IsOnlineAccount)
                return _remoteStorage ??= new RemoteSaveStorage();

            return _fileStorage ??= new FileSaveStorage();
        }

        private void HandleSessionChanged()
        {
            _storage = null;
            _index.Clear();
        }

        public static string GetCurrentUserSaveRoot()
        {
            string root = Path.Combine(Application.persistentDataPath, "social_saves", GetCurrentUserStorageKey());
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            return root;
        }

        private void LoadIndex()
        {
            _index.Clear();
            if (!File.Exists(IndexPath)) return;
            try
            {
                string json = File.ReadAllText(IndexPath);
                var list = JsonConvert.DeserializeObject<List<SaveEntry>>(json);
                if (list != null) _index = list;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Load index failed: {e.Message}");
            }
        }

        private void WriteIndex()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_index, Formatting.Indented);
                File.WriteAllText(IndexPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Write index failed: {e.Message}");
            }
        }

        /// <summary>获取所有存档列表项（按最后修改时间倒序）</summary>
        public List<SaveEntry> GetAllSaveEntries()
        {
            if (Storage is IIndexedSaveStorage indexedStorage)
            {
                List<SaveEntry> entries = indexedStorage.GetAllEntries() ?? new List<SaveEntry>();
                entries.Sort((a, b) => b.lastModifiedTicks.CompareTo(a.lastModifiedTicks));
                return entries;
            }

            LoadIndex();
            _index.Sort((a, b) => b.lastModifiedTicks.CompareTo(a.lastModifiedTicks));
            return new List<SaveEntry>(_index);
        }

        public bool HasSave(string id) => id != null && Storage.Exists(id);

        /// <summary>保存到指定 id；若 data 为 null 则删除该存档</summary>
        public void Save(string id, SaveData data)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (data == null)
            {
                Storage.Delete(id);
                LoadIndex();
                _index.RemoveAll(e => e.id == id);
                WriteIndex();
                return;
            }
            data.version = CurrentVersion;
            try
            {
                Storage.Write(id, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Save {id} failed: {e.Message}");
                return;
            }
            LoadIndex();
            var entry = _index.Find(e => e.id == id);
            if (entry == null)
            {
                entry = new SaveEntry { id = id };
                _index.Add(entry);
            }
            entry.playerName = data.playerName ?? "";
            entry.levelIndex = data.run?.levelIndex ?? 1;
            entry.lastModifiedTicks = DateTime.UtcNow.Ticks;
            WriteIndex();
        }

        /// <summary>从指定 id 读取；无文件或反序列化失败返回 null</summary>
        public SaveData Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try
            {
                var data = Storage.Read(id);
                if (data == null) return null;
                if (data.version > CurrentVersion)
                    Debug.LogWarning($"[SaveSystem] 存档版本 {data.version} > 当前版本 {CurrentVersion}，可能有兼容问题。");
                if (data.version < CurrentVersion)
                    MigrateSaveData(data);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load {id} failed: {e.Message}");
                return null;
            }
        }

        private static void MigrateSaveData(SaveData data)
        {
            if (data == null || data.run == null) return;
            if (data.version < 1)
            {
                data.run.player    ??= new PlayerSaveData();
                data.run.inventory ??= new InventorySaveData();
                data.run.buffIds          ??= new List<string>();
                data.run.unlockedSkillIds ??= new List<string>();
                data.run.defeatedBossIds  ??= new List<string>();
            }
            if (data.version < 2)
            {
                if (data.run.meleeEquippedWeapon == null
                    && data.run.rangedEquippedWeapon == null
                    && data.run.equippedWeapon != null)
                {
                    PlayerAttackMode legacyAttackMode = PlayerAttackModeUtility.GetAttackModeForWeaponType(data.run.equippedWeapon.type);
                    if (legacyAttackMode == PlayerAttackMode.Ranged)
                        data.run.rangedEquippedWeapon = data.run.equippedWeapon;
                    else
                        data.run.meleeEquippedWeapon = data.run.equippedWeapon;
                }

                if (!Enum.IsDefined(typeof(PlayerAttackMode), data.run.currentAttackMode))
                {
                    if (data.run.rangedEquippedWeapon != null && data.run.meleeEquippedWeapon == null)
                        data.run.currentAttackMode = (int)PlayerAttackMode.Ranged;
                    else
                        data.run.currentAttackMode = (int)PlayerAttackMode.Melee;
                }
            }
            if (data.version < 3)
            {
                data.run.selectedSkillMutations ??= new List<SkillMutationSelectionSave>();
            }
            if (data.version < 4)
            {
                // v4 起关卡内音频设置改为本机设备偏好，不再保存在存档里。
            }
            if (data.version < 5)
            {
                if (string.IsNullOrWhiteSpace(data.portraitId))
                    data.portraitId = DefaultPortraitId;
            }
            if (data.run.levelSnapshot != null)
            {
                data.run.levelSnapshot.enemies ??= new List<EnemySnapshot>();
                data.run.levelSnapshot.openedChestIds ??= new List<string>();
                data.run.levelSnapshot.groundDrops ??= new List<GroundDropSave>();
                data.run.levelSnapshot.shops ??= new List<ShopSnapshotSave>();
                data.run.levelSnapshot.localSpawners ??= new List<LocalSpawnerSnapshotSave>();

                for (int i = 0; i < data.run.levelSnapshot.enemies.Count; i++)
                    data.run.levelSnapshot.enemies[i].skillCooldowns ??= new List<EnemySkillCooldownSave>();

                if (data.run.levelSnapshot.globalSpawner != null)
                    data.run.levelSnapshot.globalSpawner.pendingTasks ??= new List<SpawnTaskRequestSave>();

                for (int i = 0; i < data.run.levelSnapshot.localSpawners.Count; i++)
                    data.run.levelSnapshot.localSpawners[i].plannedSpawnCounts ??= new List<int>();
            }
            if (string.IsNullOrWhiteSpace(data.portraitId))
                data.portraitId = DefaultPortraitId;
            if (data.run.keyConfigOverrides == null) data.run.keyConfigOverrides = "";
            data.version = CurrentVersion;
        }

        public void Delete(string id) => Save(id, null);

        /// <summary>重置指定存档：数据清空为初始状态，该存档仍保留在列表中。</summary>
        public void ResetSave(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            string playerName = "新存档";
            var entries = GetAllSaveEntries();
            var entry = entries?.Find(e => e.id == id);
            if (entry != null && !string.IsNullOrEmpty(entry.playerName))
                playerName = entry.playerName;
            var data = NewGameRun(1, 1);
            data.playerName = playerName;
            Save(id, data);
        }

        /// <summary>深拷贝 RunData（JSON 往返），用于 Checkpoint。</summary>
        public static RunData CloneRunData(RunData run)
        {
            if (run == null) return null;
            var backup = run.checkpoint;
            run.checkpoint = null;
            try
            {
                string json = JsonConvert.SerializeObject(run);
                var clone = JsonConvert.DeserializeObject<RunData>(json);
                if (clone != null) clone.checkpoint = null;
                return clone;
            }
            finally
            {
                run.checkpoint = backup;
            }
        }

        /// <summary>创建一份新存档（第一关、难度 1），写入并加入索引，返回新存档 id</summary>
        public string CreateNewSave(string playerName)
        {
            return CreateNewSave(playerName, 1, 1);
        }

        /// <summary>创建一份新存档（指定关卡与难度），写入并加入索引，返回新存档 id</summary>
        public string CreateNewSave(string playerName, int levelIndex, int difficulty = 1)
        {
            var data = NewGameRun(Mathf.Max(1, levelIndex), Mathf.Max(1, difficulty));
            data.playerName = NormalizePlayerName(playerName);
            data.portraitId = DefaultPortraitId;
            string id = Guid.NewGuid().ToString("N");
            Save(id, data);
            return id;
        }

        /// <summary>按玩家名和关卡读取最近一份匹配存档；不存在则创建并返回。</summary>
        public SaveData GetOrCreateSaveForPlayerAndLevel(string playerName, int levelIndex, out string saveId)
        {
            string normalizedPlayerName = NormalizePlayerName(playerName);
            int normalizedLevelIndex = Mathf.Max(1, levelIndex);
            var entries = GetAllSaveEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                SaveEntry entry = entries[i];
                if (entry == null
                    || entry.levelIndex != normalizedLevelIndex
                    || !string.Equals(entry.playerName, normalizedPlayerName, StringComparison.Ordinal)
                    || !HasSave(entry.id))
                {
                    continue;
                }

                SaveData existing = Load(entry.id);
                if (existing?.run == null)
                    continue;

                saveId = entry.id;
                return existing;
            }

            SaveData created = NewGameRun(normalizedLevelIndex, 1);
            created.playerName = normalizedPlayerName;
            created.portraitId = DefaultPortraitId;
            saveId = Guid.NewGuid().ToString("N");
            Save(saveId, created);
            return created;
        }

        /// <summary>创建一个全新的 RunData（新游戏用），并包装成 SaveData</summary>
        public static SaveData NewGameRun(int levelIndex = 1, int difficulty = 1)
        {
            var save = new SaveData
            {
                version = CurrentVersion,
                portraitId = DefaultPortraitId,
                run = new RunData
                {
                    levelIndex       = levelIndex,
                    difficulty       = difficulty,
                    player           = new PlayerSaveData { level = 1, exp = 0 },
                    inventory        = new InventorySaveData(),
                    itemCounts       = new ItemCountSaveData(),
                    equippedWeapon   = null,
                    meleeEquippedWeapon = null,
                    rangedEquippedWeapon = null,
                    currentAttackMode = (int)PlayerAttackMode.Melee,
                    buffIds          = new List<string>(),
                    unlockedSkillIds = new List<string>(),
                    selectedSkillMutations = new List<SkillMutationSelectionSave>(),
                    defeatedBossIds  = new List<string>(),
                    isGameCleared    = false,
                    checkpoint       = null,
                    pendingBuffSelection = true,
                    currentLevelBuffId = null,
                    levelSnapshot    = null
                }
            };

            ApplyStarterLoadout(save.run);
            return save;
        }

        private static void ApplyStarterLoadout(RunData run)
        {
            if (run == null)
                return;

            run.inventory ??= new InventorySaveData();
            run.inventory.slots ??= new List<InventorySlotSave>();
            run.inventory.slots.Clear();
            for (int i = 0; i < Domain.PlayerModel.SlotCount; i++)
                run.inventory.slots.Add(new InventorySlotSave());

            ApplyConfiguredStarterItems(run);

            WeaponDatabaseSO weaponDb = ConfigManager.GetInstance()?.GetWeaponDatabase();
            if (weaponDb == null)
                return;

            WeaponInstance starterSword = weaponDb.CreateMinimumRoll(WeaponType.MeleeSword, WeaponRarity.Common);
            WeaponInstance starterGun = weaponDb.CreateMinimumRoll(WeaponType.RangedGun, WeaponRarity.Common);

            run.equippedWeapon = starterSword;
            run.meleeEquippedWeapon = starterSword;
            run.rangedEquippedWeapon = starterGun;
            run.currentAttackMode = (int)PlayerAttackMode.Melee;
        }

        private static string NormalizePlayerName(string playerName)
        {
            return string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim();
        }

        private static string GetCurrentUserStorageKey()
        {
            SocialSession session = SocialSession.GetInstance();
            if (session.IsGuest)
                return "guest_local";

            SocialUserInfo currentUser = session.CurrentUser;
            if (currentUser != null && !string.IsNullOrWhiteSpace(currentUser.userId))
                return SanitizePathSegment(currentUser.userId.Trim());

            Debug.LogError("[SaveSystem] 当前未登录账号，不能访问账号存档目录。");
            return "_missing_user";
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "_missing_user";

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static void ApplyConfiguredStarterItems(RunData run)
        {
            run.itemCounts ??= new ItemCountSaveData();
            run.itemCounts.gold = 0;
            run.itemCounts.talentPoints = 0;
            run.itemCounts.items ??= new List<ItemStackSave>();
            run.itemCounts.items.Clear();

            PlayerInitialItemConfigSO config = ConfigManager.GetInstance()?.GetPlayerInitialItemConfig();
            if (config == null)
            {
                Debug.LogError("[SaveSystem] 缺少角色初始道具配置，新角色不会获得初始道具。");
                return;
            }

            if (config.initialItems == null)
            {
                Debug.LogError("[SaveSystem] 角色初始道具配置的初始道具列表为空。");
                return;
            }

            var usedItemIds = new HashSet<string>(StringComparer.Ordinal);
            int nextSlotIndex = 0;
            for (int i = 0; i < config.initialItems.Count; i++)
            {
                PlayerInitialItemEntry entry = config.initialItems[i];
                if (entry == null || entry.count == 0)
                    continue;

                if (entry.count < 0 || string.IsNullOrWhiteSpace(entry.itemId))
                {
                    Debug.LogError($"[SaveSystem] 角色初始道具配置第 {i + 1} 项无效。");
                    continue;
                }

                string itemId = entry.itemId.Trim();
                if (!usedItemIds.Add(itemId))
                {
                    Debug.LogError($"[SaveSystem] 角色初始道具配置包含重复物品 ID：{itemId}");
                    continue;
                }

                if (itemId == Domain.PlayerModel.ItemIds.Gold)
                {
                    run.itemCounts.gold = entry.count;
                    continue;
                }

                if (itemId == Domain.PlayerModel.ItemIds.TalentPoint)
                {
                    run.itemCounts.talentPoints = entry.count;
                    continue;
                }

                nextSlotIndex = AddStarterStack(run.inventory.slots, nextSlotIndex, itemId, entry.count);
                if (nextSlotIndex < 0)
                    return;
            }
        }

        private static int AddStarterStack(List<InventorySlotSave> slots, int nextSlotIndex, string itemId, int count)
        {
            int remaining = count;
            while (remaining > 0)
            {
                if (nextSlotIndex >= slots.Count)
                {
                    Debug.LogError($"[SaveSystem] 角色初始道具超过背包容量，无法写入物品：{itemId}");
                    return -1;
                }

                int stackCount = Math.Min(remaining, Domain.InventorySlot.MaxStack);
                slots[nextSlotIndex].weapon = null;
                slots[nextSlotIndex].itemId = itemId;
                slots[nextSlotIndex].count = stackCount;
                remaining -= stackCount;
                nextSlotIndex++;
            }

            return nextSlotIndex;
        }
    }
}
