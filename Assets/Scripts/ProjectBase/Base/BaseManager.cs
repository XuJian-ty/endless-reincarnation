namespace ProjectBase
{
/// <summary>
/// 非 MonoBehaviour 单例基类（线程安全双重检查锁定）。
/// 适用于不需要挂载到 GameObject 的管理器类（如 SaveSystem、EventCenter 等）。
/// </summary>
public class BaseManager<T> where T : new()
{
    private static T _instance;
    private static readonly object _lock = new object();

    public static T GetInstance()
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new T();
            }
        }
        return _instance;
    }
}
}
