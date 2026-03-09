using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Saving
{
    /// <summary>
    /// 本地文件存档（按 id 存为 save_{id}.json）：先写 .tmp 再替换，降低写入中断导致损坏的风险。
    /// </summary>
    public class FileSaveStorage : ISaveStorage
    {
        private static string SanitizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "default";
            var invalid = Path.GetInvalidFileNameChars();
            var arr = id.ToCharArray();
            for (int i = 0; i < arr.Length; i++)
                if (Array.IndexOf(invalid, arr[i]) >= 0) arr[i] = '_';
            return new string(arr);
        }

        private static string GetPath(string id, string suffix)
        {
            return Path.Combine(Application.persistentDataPath, $"save_{SanitizeId(id)}{suffix}");
        }

        public bool Exists(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return File.Exists(GetPath(id, ".json"));
        }

        public void Write(string id, SaveData data)
        {
            if (string.IsNullOrEmpty(id) || data == null) return;
            string path = GetPath(id, ".json");
            string tmpPath = GetPath(id, ".tmp");
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(tmpPath, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmpPath, path);
            }
            catch (Exception e)
            {
                if (File.Exists(tmpPath)) try { File.Delete(tmpPath); } catch { }
                throw new InvalidOperationException($"Save write failed: {e.Message}", e);
            }
        }

        public SaveData Read(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string path = GetPath(id, ".json");
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<SaveData>(json);
        }

        public void Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            string path = GetPath(id, ".json");
            string tmpPath = GetPath(id, ".tmp");
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }
}
