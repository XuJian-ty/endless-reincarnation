using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectBase
{
public class MusicMgr : BaseManager<MusicMgr>
{
    private sealed class ManagedSoundSource
    {
        public AudioSource source;
        public bool destroyComponentWhenStopped;
    }

    private const string AudioRootName = "MusicMgrRoot";
    private const string BkMusicObjectName = "BkMusic";
    private const string SoundObjectName = "Sound";

    private GameObject _audioRoot = null;
    //唯一的背景音乐组件
    private AudioSource bkMusic = null;
    //音乐大小
    private float bkValue = 1;

    //音效依附对象
    private GameObject soundObj = null;
    //音效列表
    private readonly List<ManagedSoundSource> soundList = new List<ManagedSoundSource>();
    //音效大小
    private float soundValue = 1;

    public MusicMgr()
    {
        MonoMgr.GetInstance().AddUpdateListener(Update);
    }

    private void Update()
    {
        for( int i = soundList.Count - 1; i >=0; --i )
        {
            ManagedSoundSource managedSource = soundList[i];
            AudioSource source = managedSource != null ? managedSource.source : null;
            if (source == null)
            {
                soundList.RemoveAt(i);
                continue;
            }

            if(!source.isPlaying)
            {
                if (managedSource.destroyComponentWhenStopped)
                    GameObject.Destroy(source);
                soundList.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 播放背景音乐（按 Resources 路径名异步加载）。
    /// </summary>
    public void PlayBkMusic(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        EnsureAudioRoots();
        ResMgr.GetInstance().LoadAsync<AudioClip>("Music/BK/" + name, (clip) =>
        {
            if (clip == null || bkMusic == null)
                return;

            bkMusic.clip = clip;
            bkMusic.loop = true;
            bkMusic.volume = bkValue;
            bkMusic.Play();
        });
    }

    /// <summary>
    /// 播放背景音乐（直接使用 AudioClip，无需放 Resources）。Inspector 里拖入的片段用此接口。
    /// </summary>
    public void PlayBkMusic(AudioClip clip)
    {
        if (clip == null) return;
        EnsureAudioRoots();
        bkMusic.clip = clip;
        bkMusic.loop = true;
        bkMusic.volume = bkValue;
        bkMusic.Play();
    }

    /// <summary>
    /// 暂停背景音乐
    /// </summary>
    public void PauseBKMusic()
    {
        if (bkMusic == null)
            return;
        bkMusic.Pause();
    }

    /// <summary>
    /// 停止背景音乐
    /// </summary>
    public void StopBKMusic()
    {
        if (bkMusic == null)
            return;
        bkMusic.Stop();
    }

    /// <summary>
    /// 改变背景音乐 音量大小
    /// </summary>
    /// <param name="v"></param>
    public void ChangeBKValue(float v)
    {
        bkValue = v;
        if (bkMusic == null)
            return;
        bkMusic.volume = bkValue;
    }

    /// <summary>
    /// 播放音效
    /// </summary>
    public void PlaySound(string name, bool isLoop, UnityAction<AudioSource> callBack = null)
    {
        if (string.IsNullOrEmpty(name))
            return;

        EnsureAudioRoots();
        //当音效资源异步加载结束后 再添加一个音效
        ResMgr.GetInstance().LoadAsync<AudioClip>("Music/Sound/" + name, (clip) =>
        {
            if (clip == null || soundObj == null)
            {
                callBack?.Invoke(null);
                return;
            }

            AudioSource source = soundObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = isLoop;
            source.volume = soundValue;
            source.playOnAwake = false;
            source.Play();
            RegisterSoundSource(source, true);
            callBack?.Invoke(source);
        });
    }

    /// <summary>
    /// 注册外部创建的游戏音效 AudioSource，使其受音效设置面板控制。
    /// </summary>
    public void RegisterExternalSoundSource(AudioSource source)
    {
        RegisterSoundSource(source, false);
    }

    /// <summary>
    /// 改变音效声音大小
    /// </summary>
    /// <param name="value"></param>
    public void ChangeSoundValue( float value )
    {
        soundValue = value;
        for (int i = 0; i < soundList.Count; ++i)
        {
            AudioSource source = soundList[i] != null ? soundList[i].source : null;
            if (source != null)
                source.volume = value;
        }
    }

    /// <summary>
    /// 停止音效
    /// </summary>
    public void StopSound(AudioSource source)
    {
        if(source == null)
            return;

        int index = FindSoundSourceIndex(source);
        if( index >= 0 )
        {
            soundList.RemoveAt(index);
            source.Stop();
            GameObject.Destroy(source);
        }
    }

    public float GetSoundValue()
    {
        return soundValue;
    }

    private void EnsureAudioRoots()
    {
        if (_audioRoot == null)
        {
            _audioRoot = GameObject.Find(AudioRootName);
            if (_audioRoot == null)
            {
                _audioRoot = new GameObject(AudioRootName);
                GameObject.DontDestroyOnLoad(_audioRoot);
            }
        }

        if (bkMusic == null)
        {
            Transform existingBk = _audioRoot.transform.Find(BkMusicObjectName);
            GameObject obj = existingBk != null ? existingBk.gameObject : new GameObject(BkMusicObjectName);
            obj.transform.SetParent(_audioRoot.transform, false);
            bkMusic = obj.GetComponent<AudioSource>();
            if (bkMusic == null)
                bkMusic = obj.AddComponent<AudioSource>();
            bkMusic.playOnAwake = false;
        }

        if (soundObj == null)
        {
            Transform existingSound = _audioRoot.transform.Find(SoundObjectName);
            soundObj = existingSound != null ? existingSound.gameObject : new GameObject(SoundObjectName);
            soundObj.transform.SetParent(_audioRoot.transform, false);
        }
    }

    private void RegisterSoundSource(AudioSource source, bool destroyComponentWhenStopped)
    {
        if (source == null)
            return;

        EnsureAudioRoots();
        int existingIndex = FindSoundSourceIndex(source);
        if (existingIndex >= 0)
        {
            soundList[existingIndex].destroyComponentWhenStopped = destroyComponentWhenStopped;
            source.volume = soundValue;
            return;
        }

        source.volume = soundValue;
        soundList.Add(new ManagedSoundSource
        {
            source = source,
            destroyComponentWhenStopped = destroyComponentWhenStopped,
        });
    }

    private int FindSoundSourceIndex(AudioSource source)
    {
        if (source == null)
            return -1;

        for (int i = 0; i < soundList.Count; i++)
        {
            ManagedSoundSource managedSource = soundList[i];
            if (managedSource != null && managedSource.source == source)
                return i;
        }

        return -1;
    }
}
}
