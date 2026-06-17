namespace Game.Saving
{
    /// <summary>
    /// 存档读写抽象（按存档 id），便于替换为云存档或测试用内存存储。
    /// </summary>
    public interface ISaveStorage
    {
        bool Exists(string id);
        void Write(string id, SaveData data);
        SaveData Read(string id);
        void Delete(string id);
    }
}
