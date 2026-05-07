using System;
using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ProjectBase
{
/// <summary>
/// 场景切换模块
/// 知识点
/// 1.场景异步加载
/// 2.协程
/// 3.委托
/// </summary>
public class ScenesMgr : BaseManager<ScenesMgr>
{
    /// <summary>
    /// 切换场景 同步
    /// </summary>
    /// <param name="name"></param>
    public void LoadScene(string name, UnityAction fun)
    {
        //场景同步加载
        SceneManager.LoadScene(name);
        //加载完成过后 才会去执行fun
        fun?.Invoke();
    }

    /// <summary>
    /// 提供给外部的 异步加载的接口方法。
    /// <param name="animateProgress">true：进关卡时用进度条动画（每 0.02s +1%，到 100% 再等 0.3s）；false：不播动画，加载完等 0.3s 即切场景（用于返回主菜单等）。</param>
    /// </summary>
    public void LoadSceneAsyn(string name, UnityAction fun, bool animateProgress = true)
    {
        LoadSceneAsync(name, fun, animateProgress);
    }

    /// <summary>
    /// 异步切换场景。
    /// </summary>
    public void LoadSceneAsync(string name, UnityAction fun, bool animateProgress = true)
    {
        MonoMgr.GetInstance().StartCoroutine(ReallyLoadSceneAsyn(name, fun, animateProgress));
    }

    /// <summary>
    /// 异步加载场景，但在进度条达到 100% 后等待外部放行再激活场景。用于联机场景切换屏障。
    /// </summary>
    public void LoadSceneAsynWaitingForActivation(string name, Func<bool> canActivate, UnityAction readyToActivate, UnityAction fun, bool animateProgress = true)
    {
        MonoMgr.GetInstance().StartCoroutine(ReallyLoadSceneAsynWaitingForActivation(name, canActivate, readyToActivate, fun, animateProgress));
    }

    /// <summary>
    /// 协程异步加载场景。进度条一开始加载即停止当前背景音乐；新场景在 allowSceneActivation=true 激活后再由各场景逻辑播放对应 BGM。
    /// animateProgress 为 true 时：每 0.02 秒更新进度条，到 100% 后等 0.3 秒再激活场景。
    /// animateProgress 为 false 时：等场景加载到 0.9 后等 0.3 秒再激活（用于返回主菜单等）。
    /// </summary>
    private IEnumerator ReallyLoadSceneAsyn(string name, UnityAction fun, bool animateProgress)
    {
        MusicMgr.GetInstance().StopBKMusic();
        AsyncOperation ao = SceneManager.LoadSceneAsync(name);
        if (ao == null)
        {
            Debug.LogError($"[ScenesMgr] 场景 '{name}' 无法加载（未加入 Build Settings 或不存在）。请通过 File → Build Settings → Scenes In Build 添加该场景。");
            EventCenter.GetInstance().EventTrigger(GameEvents.SceneLoadProgress, 1f);
            fun?.Invoke();
            yield break;
        }

        ao.allowSceneActivation = false;
        const float finalWait = 0.3f;
        const float progressScale = 1f / 0.9f;

        if (animateProgress)
        {
            const float updateInterval = 0.02f;
            const float stepPercent = 0.01f;
            float displayProgress = 0f;

            while (displayProgress < 1f)
            {
                yield return new WaitForSecondsRealtime(updateInterval);

                float sceneProgress = Mathf.Min(1f, ao.progress * progressScale);
                float nextBar = Mathf.Min(displayProgress + stepPercent, 1f);

                if (nextBar < sceneProgress)
                    displayProgress = nextBar;
                else
                    displayProgress = sceneProgress;

                EventCenter.GetInstance().EventTrigger(GameEvents.SceneLoadProgress, displayProgress);
            }
        }
        else
        {
            while (ao.progress < 0.9f)
                yield return null;
            EventCenter.GetInstance().EventTrigger(GameEvents.SceneLoadProgress, 1f);
        }

        yield return new WaitForSecondsRealtime(finalWait);
        ao.allowSceneActivation = true;
        while (!ao.isDone)
            yield return null;
        fun?.Invoke();
    }

    private IEnumerator ReallyLoadSceneAsynWaitingForActivation(string name, Func<bool> canActivate, UnityAction readyToActivate, UnityAction fun, bool animateProgress)
    {
        MusicMgr.GetInstance().StopBKMusic();
        AsyncOperation ao = SceneManager.LoadSceneAsync(name);
        if (ao == null)
        {
            Debug.LogError($"[ScenesMgr] 场景 '{name}' 无法加载（未加入 Build Settings 或不存在）。请通过 File → Build Settings → Scenes In Build 添加该场景。");
            EventCenter.GetInstance().EventTrigger(GameEvents.SceneLoadProgress, 1f);
            fun?.Invoke();
            yield break;
        }

        ao.allowSceneActivation = false;
        const float finalWait = 0.3f;
        const float progressScale = 1f / 0.9f;

        if (animateProgress)
        {
            const float updateInterval = 0.02f;
            const float stepPercent = 0.01f;
            float displayProgress = 0f;

            while (displayProgress < 1f)
            {
                yield return new WaitForSecondsRealtime(updateInterval);

                float sceneProgress = Mathf.Min(1f, ao.progress * progressScale);
                float nextBar = Mathf.Min(displayProgress + stepPercent, 1f);

                if (nextBar < sceneProgress)
                    displayProgress = nextBar;
                else
                    displayProgress = sceneProgress;

                EventCenter.GetInstance().EventTrigger(GameEvents.SceneLoadProgress, displayProgress);
            }
        }
        else
        {
            while (ao.progress < 0.9f)
                yield return null;
            EventCenter.GetInstance().EventTrigger(GameEvents.SceneLoadProgress, 1f);
        }

        readyToActivate?.Invoke();
        while (canActivate != null && !canActivate())
            yield return null;

        yield return new WaitForSecondsRealtime(finalWait);
        ao.allowSceneActivation = true;
        while (!ao.isDone)
            yield return null;
        fun?.Invoke();
    }
}
}
