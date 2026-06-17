using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectBase
{

public interface IEventInfo
{

}

public class EventInfo<T> : IEventInfo
{
    public UnityAction<T> actions;

    public EventInfo( UnityAction<T> action)
    {
        actions += action;
    }
}

public class EventInfo : IEventInfo
{
    public UnityAction actions;

    public EventInfo(UnityAction action)
    {
        actions += action;
    }
}


/// <summary>
/// 事件中心 单例模式对象
/// 1.Dictionary 2.委托 3.观察者设计模式 4.泛型
/// 游戏逻辑建议使用 Game.GameEvents 中的常量作为 name，避免魔法字符串。
/// </summary>
public class EventCenter : BaseManager<EventCenter>
{
    //key —— 事件的名字（比如：怪物死亡，玩家死亡，通关 等等）
    //value —— 对应的是 监听这个事件 对应的委托函数们
    private Dictionary<string, IEventInfo> eventDic = new Dictionary<string, IEventInfo>();

    /// <summary>
    /// 添加事件监听
    /// </summary>
    /// <param name="name">事件的名字</param>
    /// <param name="action">准备用来处理事件 的委托函数</param>
    public void AddEventListener<T>(string name, UnityAction<T> action)
    {
        if( eventDic.ContainsKey(name) )
        {
            var info = eventDic[name] as EventInfo<T>;
            if (info != null)
                info.actions += action;
            else
                Debug.LogError($"[EventCenter] 事件 '{name}' 已注册为其他类型，无法添加 {typeof(T).Name} 监听。");
        }
        else
        {
            eventDic.Add(name, new EventInfo<T>( action ));
        }
    }

    /// <summary>
    /// 监听不需要参数传递的事件
    /// </summary>
    /// <param name="name"></param>
    /// <param name="action"></param>
    public void AddEventListener(string name, UnityAction action)
    {
        if (eventDic.ContainsKey(name))
        {
            var info = eventDic[name] as EventInfo;
            if (info != null)
                info.actions += action;
            else
                Debug.LogError($"[EventCenter] 事件 '{name}' 已注册为带参类型，无法添加无参监听。");
        }
        else
        {
            eventDic.Add(name, new EventInfo(action));
        }
    }


    /// <summary>
    /// 移除对应的事件监听
    /// </summary>
    /// <param name="name">事件的名字</param>
    /// <param name="action">对应之前添加的委托函数</param>
    public void RemoveEventListener<T>(string name, UnityAction<T> action)
    {
        if (eventDic.ContainsKey(name))
        {
            var info = eventDic[name] as EventInfo<T>;
            if (info != null)
            {
                info.actions -= action;
                if (info.actions == null)
                    eventDic.Remove(name);
            }
        }
    }

    public void RemoveEventListener(string name, UnityAction action)
    {
        if (eventDic.ContainsKey(name))
        {
            var info = eventDic[name] as EventInfo;
            if (info != null)
            {
                info.actions -= action;
                if (info.actions == null)
                    eventDic.Remove(name);
            }
        }
    }

    /// <summary>
    /// 事件触发
    /// </summary>
    /// <param name="name">哪一个名字的事件触发了</param>
    public void EventTrigger<T>(string name, T info)
    {
        if (eventDic.ContainsKey(name))
        {
            var ei = eventDic[name] as EventInfo<T>;
            if (ei?.actions != null)
                ei.actions.Invoke(info);
            else if (ei == null)
                Debug.LogWarning($"[EventCenter] 事件 '{name}' 类型不匹配：期望 {eventDic[name]?.GetType().Name}，实际触发 EventInfo<{typeof(T).Name}>。");
        }
    }

    public void EventTrigger(string name)
    {
        if (eventDic.ContainsKey(name))
        {
            var ei = eventDic[name] as EventInfo;
            if (ei?.actions != null)
                ei.actions.Invoke();
            else if (ei == null)
                Debug.LogWarning($"[EventCenter] 事件 '{name}' 类型不匹配：期望带参事件，实际触发无参。");
        }
    }

    /// <summary>
    /// 清空事件中心
    /// 主要用在 场景切换时
    /// </summary>
    public void Clear()
    {
        eventDic.Clear();
    }

    public void Clear(string name)
    {
        if (!string.IsNullOrEmpty(name))
            eventDic.Remove(name);
    }
}
}
