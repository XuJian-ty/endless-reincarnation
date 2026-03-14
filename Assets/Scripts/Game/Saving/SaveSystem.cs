using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Game;
using Game.Data;
using ProjectBase;

namespace Game.Saving
{
    /// <summary>
    /// 存档系统：存档数量不限，按 id 存储，每份存档含玩家名字；索引存于 saves_index.json。
    /// </summary>
    public class SaveSystem : BaseManager<SaveSystem>
    {
        public const int CurrentVersion = 1;

        private ISaveStorage _storage;
        private List<SaveEntry> _index = new List<SaveEntry>();
        private static string IndexPath => Path.Combine(Application.persistentDataPath, "saves_index.json");

        public ISaveStorage Storage
        {
            get => _storage ??= new FileSaveStorage();
            set => _storage = value;
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
            if (data.run.levelBgmVolume <= 0f) data.run.levelBgmVolume = 1f;
            if (data.run.levelBgmTrackIndex < 0) data.run.levelBgmTrackIndex = 0;
            if (data.run.soundEffectsVolume < 0f) data.run.soundEffectsVolume = 1f;
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
            var data = NewGameRun(1, 1);
            data.playerName = string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim();
            string id = Guid.NewGuid().ToString("N");
            Save(id, data);
            return id;
        }

        /// <summary>创建一个全新的 RunData（新游戏用），并包装成 SaveData</summary>
        public static SaveData NewGameRun(int levelIndex = 1, int difficulty = 1)
        {
            var save = new SaveData
            {
                version = CurrentVersion,
                run = new RunData
                {
                    levelIndex       = levelIndex,
                    difficulty       = difficulty,
                    player           = new PlayerSaveData { level = 1, exp = 0 },
                    inventory        = new InventorySaveData(),
                    itemCounts       = new ItemCountSaveData { gold = 0, talentPoints = 0 },
                    equippedWeapon   = null,
                    talentPoints     = 0,
                    buffIds          = new List<string>(),
                    unlockedSkillIds = new List<string>(),
                    defeatedBossIds  = new List<string>(),
                    isGameCleared    = false,
                    checkpoint       = null,
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

            SetStarterStack(run.inventory.slots, 0, Domain.PlayerModel.ItemIds.PotionHp, 3);
            SetStarterStack(run.inventory.slots, 1, Domain.PlayerModel.ItemIds.PotionMp, 3);
            SetStarterStack(run.inventory.slots, 2, Domain.PlayerModel.ItemIds.Nectar, 1);

            WeaponDatabaseSO weaponDb = ConfigManager.GetInstance()?.GetWeaponDatabase();
            if (weaponDb == null)
                return;

            WeaponInstance starterSword = weaponDb.CreateMinimumRoll(WeaponType.MeleeSword, WeaponRarity.Common);
            WeaponInstance starterGun = weaponDb.CreateMinimumRoll(WeaponType.RangedGun, WeaponRarity.Common);

            run.equippedWeapon = starterSword;
            if (starterGun != null && run.inventory.slots.Count > 3)
                run.inventory.slots[3].weapon = starterGun;
        }

        private static void SetStarterStack(List<InventorySlotSave> slots, int index, string itemId, int count)
        {
            if (slots == null || index < 0 || index >= slots.Count || string.IsNullOrWhiteSpace(itemId) || count <= 0)
                return;

            slots[index].weapon = null;
            slots[index].itemId = itemId;
            slots[index].count = count;
        }
    }
}
