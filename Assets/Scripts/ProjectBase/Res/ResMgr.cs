using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectBase
{
/// <summary>
/// 资源加载模块
/// 1.异步加载
/// 2.委托和 lambda表达式
/// 3.协程
/// 4.泛型
/// </summary>
public class ResMgr : BaseManager<ResMgr>
{
    /// <summary>
    /// 同步加载资源本体，不自动实例化。
    /// </summary>
    public T LoadAsset<T>(string name) where T : Object
    {
        if (string.IsNullOrEmpty(name))
            return null;

        return Resources.Load<T>(name);
    }

    //同步加载资源
    public T Load<T>(string name) where T:Object
    {
        T res = LoadAsset<T>(name);
        //如果对象是一个GameObject类型的 我把他实例化后 再返回出去 外部 直接使用即可
        if (res is GameObject)
            return GameObject.Instantiate(res);
        else//TextAsset AudioClip
            return res;
    }


    //异步加载资源
    public void LoadAsync<T>(string name, UnityAction<T> callback) where T:Object
    {
        //开启异步加载的协程
        MonoMgr.GetInstance().StartCoroutine(ReallyLoadAsync<T>(name, callback));
    }

    /// <summary>
    /// 异步加载资源本体，不自动实例化。
    /// </summary>
    public void LoadAssetAsync<T>(string name, UnityAction<T> callback) where T : Object
    {
        MonoMgr.GetInstance().StartCoroutine(ReallyLoadAssetAsync<T>(name, callback));
    }

    //真正的协同程序函数  用于 开启异步加载对应的资源
    private IEnumerator ReallyLoadAsync<T>(string name, UnityAction<T> callback) where T : Object
    {
        yield return ReallyLoadAssetAsync<T>(name, asset =>
        {
            if (asset is GameObject)
                callback?.Invoke(GameObject.Instantiate(asset) as T);
            else
                callback?.Invoke(asset);
        });
    }

    private IEnumerator ReallyLoadAssetAsync<T>(string name, UnityAction<T> callback) where T : Object
    {
        if (string.IsNullOrEmpty(name))
        {
            callback?.Invoke(null);
            yield break;
        }

        ResourceRequest r = Resources.LoadAsync<T>(name);
        yield return r;
        callback?.Invoke(r.asset as T);
    }


}
}
