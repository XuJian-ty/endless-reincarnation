# 存档模块 (Saving)

- **SaveData.cs**：存档用 DTO（SaveData、RunData、PlayerSaveData、InventorySaveData、LevelSnapshot 等），均使用 `[Serializable]` 与 public 字段，供 Newtonsoft.Json 序列化。
- **SaveSystem.cs**：单例，6 槽位，`Save(slot, data)` / `Load(slot)` / `HasSave(slot)` / `Delete(slot)`；`NewGameRun()` 生成新游戏用的空 RunData。

**依赖**：Newtonsoft.Json。已通过 `Packages/manifest.json` 添加 `com.unity.nuget.newtonsoft-json`。若未安装，请在 Package Manager 中添加该包。
