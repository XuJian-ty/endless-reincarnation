using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectBase
{
/// <summary>
/// 面板基类：自动收集子控件，支持按钮点击注册（推荐）或重写 OnClick。
/// </summary>
public class BasePanel : MonoBehaviour
{
    private Dictionary<string, List<UIBehaviour>> controlDic = new Dictionary<string, List<UIBehaviour>>();
    private Dictionary<string, Action> _clickHandlers;

    protected virtual void Awake()
    {
        FindChildrenControl<Button>();
        FindChildrenControl<Image>();
        FindChildrenControl<Text>();
        FindChildrenControl<Toggle>();
        FindChildrenControl<Slider>();
        FindChildrenControl<ScrollRect>();
        FindChildrenControl<InputField>();
        FindChildrenControl<Dropdown>();
    }

    /// <summary>注册按钮点击回调，推荐在 Awake 中 base.Awake() 之后调用。未注册的按钮仍会走 OnClick(btnName)。</summary>
    protected void RegisterClick(string btnName, Action callback)
    {
        if (callback == null) return;
        if (_clickHandlers == null) _clickHandlers = new Dictionary<string, Action>();
        _clickHandlers[btnName] = callback;
    }

    /// <summary>
    /// 显示自己
    /// </summary>
    public virtual void ShowMe()
    {
    }

    /// <summary>
    /// 隐藏自己
    /// </summary>
    public virtual void HideMe()
    {
    }

    /// <summary>未通过 RegisterClick 注册的按钮会调用此方法，子类可重写。</summary>
    protected virtual void OnClick(string btnName)
    {
    }

    protected virtual void OnValueChanged(string toggleName, bool value)
    {

    }

    /// <summary>
    /// 得到对应名字的对应控件脚本
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="controlName"></param>
    /// <returns></returns>
    protected T GetControl<T>(string controlName) where T : UIBehaviour
    {
        if(controlDic.ContainsKey(controlName))
        {
            for( int i = 0; i <controlDic[controlName].Count; ++i )
            {
                if (controlDic[controlName][i] is T)
                    return controlDic[controlName][i] as T;
            }
        }

        return null;
    }

    /// <summary>
    /// 找到子对象的对应控件
    /// </summary>
    /// <typeparam name="T"></typeparam>
    private void FindChildrenControl<T>() where T:UIBehaviour
    {
        T[] controls = this.GetComponentsInChildren<T>();
        for (int i = 0; i < controls.Length; ++i)
        {
            string objName = controls[i].gameObject.name;
            if (controlDic.ContainsKey(objName))
                controlDic[objName].Add(controls[i]);
            else
                controlDic.Add(objName, new List<UIBehaviour>() { controls[i] });
            if (controls[i] is Button btn)
            {
                btn.onClick.AddListener(() =>
                {
                    if (_clickHandlers != null && _clickHandlers.TryGetValue(objName, out var action))
                        action.Invoke();
                    else
                        OnClick(objName);
                });
            }
            //如果是单选框或者多选框
            else if(controls[i] is Toggle)
            {
                (controls[i] as Toggle).onValueChanged.AddListener((value) =>
                {
                    OnValueChanged(objName, value);
                });
            }
        }
    }
}
}
