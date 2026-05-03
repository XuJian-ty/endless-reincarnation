using System.Collections.Generic;

namespace Game.Saving
{
    /// <summary>
    /// 可直接提供存档列表索引的存储实现（如服务端存档）。
    /// </summary>
    public interface IIndexedSaveStorage
    {
        List<SaveEntry> GetAllEntries();
    }
}
