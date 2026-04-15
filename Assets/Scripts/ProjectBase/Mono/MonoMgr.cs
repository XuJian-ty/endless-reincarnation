using System.ComponentModel;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectBase
{
/// <summary>
/// 1.可以提供给外部添加帧更新事件的方法
/// 2.可以提供给外部添加 协程的方法
/// </summary>
public class MonoMgr : BaseManager<MonoMgr>
{
    private MonoController controller;

    public MonoMgr()
    {
        EnsureController();
    }

    /// <summary>
    /// 给外部提供的 添加帧更新事件的函数
    /// </summary>
    /// <param name="fun"></param>
    public void AddUpdateListener(UnityAction fun)
    {
        EnsureController();
        controller.AddUpdateListener(fun);
    }

    /// <summary>
    /// 提供给外部 用于移除帧更新事件函数
    /// </summary>
    /// <param name="fun"></param>
    public void RemoveUpdateListener(UnityAction fun)
    {
        EnsureController();
        controller.RemoveUpdateListener(fun);
    }

    public void AddLateUpdateListener(UnityAction fun)
    {
        EnsureController();
        controller.AddLateUpdateListener(fun);
    }

    public void RemoveLateUpdateListener(UnityAction fun)
    {
        EnsureController();
        controller.RemoveLateUpdateListener(fun);
    }

    public void AddFixedUpdateListener(UnityAction fun)
    {
        EnsureController();
        controller.AddFixedUpdateListener(fun);
    }

    public void RemoveFixedUpdateListener(UnityAction fun)
    {
        EnsureController();
        controller.RemoveFixedUpdateListener(fun);
    }

    public Coroutine StartCoroutine(IEnumerator routine)
    {
        EnsureController();
        return controller.RunCoroutine(routine);
    }

    public Coroutine StartCoroutine(string methodName, [DefaultValue("null")] object value)
    {
        EnsureController();
        return controller.StartCoroutine(methodName, value);
    }

    public Coroutine StartCoroutine(string methodName)
    {
        EnsureController();
        return controller.StartCoroutine(methodName);
    }

    public void StopCoroutine(Coroutine routine)
    {
        if (routine == null)
            return;

        EnsureController();
        controller.StopManagedCoroutine(routine);
    }

    public void StopCoroutine(string methodName)
    {
        if (string.IsNullOrEmpty(methodName))
            return;

        EnsureController();
        controller.StopManagedCoroutine(methodName);
    }

    public void StopAllCoroutines()
    {
        EnsureController();
        controller.StopAllManagedCoroutines();
    }

    private void EnsureController()
    {
        if (controller != null)
            return;

        controller = Object.FindFirstObjectByType<MonoController>();
        if (controller != null)
            return;

        //保证了MonoController对象的唯一性
        GameObject obj = new GameObject("MonoController");
        controller = obj.AddComponent<MonoController>();
    }
}
}
