using UnityEngine;
using UnityEngine.Events;

namespace ProjectBase
{
/// <summary>
/// Mono的管理者
/// 1.声明周期函数
/// 2.事件 
/// 3.协程
/// </summary>
public class MonoController : MonoBehaviour {

    private event UnityAction updateEvent;
    private event UnityAction lateUpdateEvent;
    private event UnityAction fixedUpdateEvent;

	private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
	}
	
	// Update is called once per frame
    void Update () {
        if (updateEvent != null)
            updateEvent();
    }

    private void LateUpdate()
    {
        if (lateUpdateEvent != null)
            lateUpdateEvent();
    }

    private void FixedUpdate()
    {
        if (fixedUpdateEvent != null)
            fixedUpdateEvent();
    }

    /// <summary>
    /// 给外部提供的 添加帧更新事件的函数
    /// </summary>
    /// <param name="fun"></param>
    public void AddUpdateListener(UnityAction fun)
    {
        updateEvent += fun;
    }

    /// <summary>
    /// 提供给外部 用于移除帧更新事件函数
    /// </summary>
    /// <param name="fun"></param>
    public void RemoveUpdateListener(UnityAction fun)
    {
        updateEvent -= fun;
    }

    public void AddLateUpdateListener(UnityAction fun)
    {
        lateUpdateEvent += fun;
    }

    public void RemoveLateUpdateListener(UnityAction fun)
    {
        lateUpdateEvent -= fun;
    }

    public void AddFixedUpdateListener(UnityAction fun)
    {
        fixedUpdateEvent += fun;
    }

    public void RemoveFixedUpdateListener(UnityAction fun)
    {
        fixedUpdateEvent -= fun;
    }

    public Coroutine RunCoroutine(System.Collections.IEnumerator routine)
    {
        return routine != null ? StartCoroutine(routine) : null;
    }

    public void StopManagedCoroutine(Coroutine routine)
    {
        if (routine != null)
            StopCoroutine(routine);
    }

    public void StopManagedCoroutine(string methodName)
    {
        if (!string.IsNullOrEmpty(methodName))
            StopCoroutine(methodName);
    }

    public void StopAllManagedCoroutines()
    {
        StopAllCoroutines();
    }
}
}
