using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectBase
{
/// <summary>
/// 抽屉数据  池子中的一列容器
/// </summary>
public class PoolData
{
    private readonly string _drawerName;
    //抽屉中 对象挂载的父节点
    public GameObject fatherObj;
    //对象的容器
    public Stack<GameObject> poolStack;

    public PoolData(string drawerName, GameObject poolObj)
    {
        _drawerName = drawerName;
        //给我们的抽屉 创建一个父对象 并且把他作为我们pool(衣柜)对象的子物体
        fatherObj = new GameObject(drawerName);
        fatherObj.transform.parent = poolObj.transform;
        poolStack = new Stack<GameObject>();
        RefreshDrawerName();
    }

    /// <summary>
    /// 往抽屉里面 压都东西
    /// </summary>
    /// <param name="obj"></param>
    public void PushObj(GameObject obj)
    {
        if (obj == null)
            return;

        //失活 让其隐藏
        obj.SetActive(false);
        //存起来
        poolStack.Push(obj);
        //设置父对象
        obj.transform.SetParent(fatherObj.transform, false);
        RefreshDrawerName();
    }

    /// <summary>
    /// 从抽屉里面 取东西
    /// </summary>
    /// <returns></returns>
    public GameObject GetObj()
    {
        while (poolStack.Count > 0)
        {
            GameObject obj = poolStack.Pop();
            if (obj == null)
            {
                RefreshDrawerName();
                continue;
            }

            //激活 让其显示
            obj.SetActive(true);
            //断开了父子关系
            obj.transform.SetParent(null, false);
            RefreshDrawerName();
            return obj;
        }

        RefreshDrawerName();
        return null;
    }

    public bool HasAvailableObject()
    {
        while (poolStack.Count > 0 && poolStack.Peek() == null)
            poolStack.Pop();

        RefreshDrawerName();
        return poolStack.Count > 0;
    }

    public void Clear()
    {
        while (poolStack.Count > 0)
        {
            GameObject obj = poolStack.Pop();
            if (obj != null)
                Object.Destroy(obj);
        }

        if (fatherObj != null)
            Object.Destroy(fatherObj);
    }

    private void RefreshDrawerName()
    {
        if (fatherObj == null)
            return;

        fatherObj.name = $"{_drawerName} ({poolStack.Count})";
    }
}

/// <summary>
/// 缓存池模块。
/// 兼容旧的按 Resources 路径名取还对象写法，同时补充同步获取、指定父节点和预热接口。
/// </summary>
public class PoolMgr : BaseManager<PoolMgr>
{
    private const string PoolRootName = "[Pool]";
    //缓存池容器 （衣柜）
    public Dictionary<string, PoolData> poolDic = new Dictionary<string, PoolData>();
    private readonly Dictionary<string, GameObject> _prefabSourceDic = new Dictionary<string, GameObject>();

    private GameObject poolObj;

    /// <summary>
     /// 往外拿东西
     /// </summary>
     /// <param name="name"></param>
     /// <returns></returns>
    public void GetObj(string name, UnityAction<GameObject> callBack)
    {
        GetObj(name, null, callBack);
    }

    /// <summary>
    /// 主动初始化缓存池根节点，便于在层级面板中提前看到对象池。
    /// </summary>
    public void EnsureInitialized()
    {
        EnsurePoolRoot();
    }

    /// <summary>
    /// 异步获取对象，可指定父节点。
    /// </summary>
    public void GetObj(string name, Transform parent, UnityAction<GameObject> callBack)
    {
        EnsurePoolRoot();
        //有抽屉 并且抽屉里有东西
        if (TryGetPooledObject(name, parent, out GameObject pooledObject))
        {
            callBack?.Invoke(pooledObject);
        }
        else
        {
            //通过异步加载资源 创建对象给外部用
            ResMgr.GetInstance().LoadAsync<GameObject>(name, (o) =>
            {
                if (o == null)
                {
                    Debug.LogWarning($"[PoolMgr] 未找到可加载对象：{name}");
                    callBack?.Invoke(null);
                    return;
                }

                o.name = name;
                PrepareSpawnedObject(o, parent);
                callBack?.Invoke(o);
            });

            //obj = GameObject.Instantiate(Resources.Load<GameObject>(name));
            //把对象名字改的和池子名字一样
            //obj.name = name;
        }
    }

    /// <summary>
    /// 同步获取对象，可指定父节点。
    /// </summary>
    public GameObject GetObjSync(string name, Transform parent = null)
    {
        EnsurePoolRoot();
        if (TryGetPooledObject(name, parent, out GameObject pooledObject))
            return pooledObject;

        GameObject obj = ResMgr.GetInstance().Load<GameObject>(name);
        if (obj == null)
        {
            Debug.LogWarning($"[PoolMgr] 未找到可同步加载对象：{name}");
            return null;
        }

        obj.name = name;
        PrepareSpawnedObject(obj, parent);
        return obj;
    }

    /// <summary>
    /// 直接按 prefab 获取对象，适合运行时已持有预制体引用的场景。
    /// </summary>
    public GameObject GetObjSync(GameObject prefab, Transform parent = null)
    {
        if (prefab == null)
            return null;

        EnsurePoolRoot();
        string key = RegisterPrefab(prefab);
        if (TryGetPooledObject(key, parent, out GameObject pooledObject))
            return pooledObject;

        GameObject obj = Object.Instantiate(prefab, parent);
        obj.name = prefab.name;
        PrepareSpawnedObject(obj, parent);
        return obj;
    }

    /// <summary>
    /// 换暂时不用的东西给我
    /// </summary>
    public void PushObj(string name, GameObject obj)
    {
        if (string.IsNullOrEmpty(name) || obj == null)
            return;

        EnsurePoolRoot();
        GetOrCreatePoolData(name).PushObj(obj);
    }

    /// <summary>
    /// 按 prefab 归还对象。
    /// </summary>
    public void PushObj(GameObject prefab, GameObject obj)
    {
        if (prefab == null || obj == null)
            return;

        PushObj(RegisterPrefab(prefab), obj);
    }

    /// <summary>
    /// 预热指定数量的对象，避免首次使用时集中创建。
    /// </summary>
    public void Prewarm(string name, int count)
    {
        if (string.IsNullOrEmpty(name) || count <= 0)
            return;

        EnsurePoolRoot();
        PoolData data = GetOrCreatePoolData(name);
        int needCreate = count - CountAvailable(name);
        for (int i = 0; i < needCreate; i++)
        {
            GameObject obj = ResMgr.GetInstance().Load<GameObject>(name);
            if (obj == null)
            {
                Debug.LogWarning($"[PoolMgr] 预热失败，未找到对象：{name}");
                return;
            }

            obj.name = name;
            data.PushObj(obj);
        }
    }

    /// <summary>
    /// 预热指定 prefab 的对象池。
    /// </summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        string key = RegisterPrefab(prefab);
        EnsurePoolRoot();
        PoolData data = GetOrCreatePoolData(key);
        int needCreate = count - CountAvailable(key);
        for (int i = 0; i < needCreate; i++)
        {
            GameObject obj = Object.Instantiate(prefab);
            obj.name = prefab.name;
            data.PushObj(obj);
        }
    }

    /// <summary>
    /// 当前池中可直接取出的对象数量。
    /// </summary>
    public int CountAvailable(string name)
    {
        if (!poolDic.TryGetValue(name, out PoolData data) || data == null)
            return 0;

        while (data.poolStack.Count > 0 && data.poolStack.Peek() == null)
            data.poolStack.Pop();

        return data.poolStack.Count;
    }

    /// <summary>
    /// 清空某一列池对象。
    /// </summary>
    public void Clear(string name)
    {
        if (!poolDic.TryGetValue(name, out PoolData data) || data == null)
            return;

        data.Clear();
        poolDic.Remove(name);
        _prefabSourceDic.Remove(name);
    }


    /// <summary>
    /// 清空缓存池的方法 
    /// 主要用在 场景切换时
    /// </summary>
    public void Clear()
    {
        foreach (KeyValuePair<string, PoolData> pair in poolDic)
        {
            if (pair.Value != null)
                pair.Value.Clear();
        }

        poolDic.Clear();
        _prefabSourceDic.Clear();
        if (poolObj != null)
            Object.Destroy(poolObj);
        poolObj = null;
    }

    private bool TryGetPooledObject(string name, Transform parent, out GameObject instance)
    {
        instance = null;
        if (string.IsNullOrEmpty(name))
            return false;

        if (!poolDic.TryGetValue(name, out PoolData data) || data == null || !data.HasAvailableObject())
            return false;

        instance = data.GetObj();
        if (instance == null)
            return false;

        PrepareSpawnedObject(instance, parent);
        return true;
    }

    private static void PrepareSpawnedObject(GameObject obj, Transform parent)
    {
        if (obj == null)
            return;

        obj.transform.SetParent(parent, false);
        obj.SetActive(true);
    }

    private void EnsurePoolRoot()
    {
        if (poolObj != null)
            return;

        poolObj = GameObject.Find(PoolRootName);
        if (poolObj != null)
            return;

        poolObj = new GameObject(PoolRootName);
        Object.DontDestroyOnLoad(poolObj);
    }

    private PoolData GetOrCreatePoolData(string name)
    {
        if (!poolDic.TryGetValue(name, out PoolData data) || data == null)
        {
            data = new PoolData(name, poolObj);
            poolDic[name] = data;
        }

        return data;
    }

    private string RegisterPrefab(GameObject prefab)
    {
        string key = GetPrefabPoolKey(prefab);
        _prefabSourceDic[key] = prefab;
        return key;
    }

    private static string GetPrefabPoolKey(GameObject prefab)
    {
        string prefabName = prefab != null && !string.IsNullOrEmpty(prefab.name)
            ? prefab.name
            : "GameObject";
        return $"prefab:{prefabName}_{prefab.GetInstanceID()}";
    }
}
}
