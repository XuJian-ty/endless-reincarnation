using System;
using System.Collections;
using ProjectBase;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.UI
{
    /// <summary>
    /// Boss 胜利过场面板：播放视频，结束后停在最后一帧，等待结算面板覆盖显示。
    /// </summary>
    public class BossVictoryCutscenePanel : BasePanel
    {
        private RawImage _videoImage;
        private VideoPlayer _videoPlayer;
        private RenderTexture _renderTexture;
        private Coroutine _playRoutine;
        private Action _onComplete;
        private bool _videoErrorReceived;

        protected override void Awake()
        {
            EnsureRuntimeUi();
            base.Awake();
        }

        public void Play(VideoClip clip, Action onComplete)
        {
            _onComplete = onComplete;
            gameObject.SetActive(true);

            if (clip == null)
            {
                Complete();
                return;
            }

            if (_playRoutine != null)
                StopCoroutine(_playRoutine);

            _playRoutine = StartCoroutine(PlayRoutine(clip));
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
        }

        public override void HideMe()
        {
            StopPlayback();
            gameObject.SetActive(false);
        }

        private IEnumerator PlayRoutine(VideoClip clip)
        {
            EnsureVideoTarget();

            _videoPlayer.Stop();
            _videoPlayer.clip = clip;
            _videoPlayer.isLooping = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            _videoErrorReceived = false;
            _videoPlayer.Prepare();

            float prepareDeadline = Time.realtimeSinceStartup + 10f;
            while (!_videoPlayer.isPrepared && !_videoErrorReceived && Time.realtimeSinceStartup < prepareDeadline)
                yield return null;

            if (!_videoPlayer.isPrepared || _videoErrorReceived)
            {
                Debug.LogWarning($"[BossVictoryCutscenePanel] Boss 胜利过场视频准备失败：{clip.name}");
                _playRoutine = null;
                Complete();
                yield break;
            }

            _videoPlayer.Play();

            while (_videoPlayer.isPlaying && !_videoErrorReceived)
                yield return null;

            if (_videoErrorReceived)
            {
                _playRoutine = null;
                Complete();
                yield break;
            }

            _videoPlayer.Pause();
            _playRoutine = null;
            Complete();
        }

        private void Complete()
        {
            Action callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        private void StopPlayback()
        {
            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            if (_videoPlayer != null)
                _videoPlayer.Stop();

            _onComplete = null;
        }

        private void EnsureRuntimeUi()
        {
            if (_videoImage != null && _videoPlayer != null)
                return;

            RectTransform rootRect = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(rootRect);

            GameObject videoObject = new GameObject("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            videoObject.transform.SetParent(transform, false);

            RectTransform videoRect = videoObject.GetComponent<RectTransform>();
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;

            _videoImage = videoObject.GetComponent<RawImage>();
            _videoImage.color = Color.white;
            _videoImage.raycastTarget = true;

            _videoPlayer = gameObject.GetComponent<VideoPlayer>();
            if (_videoPlayer == null)
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();

            _videoPlayer.playOnAwake = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.errorReceived -= OnVideoErrorReceived;
            _videoPlayer.errorReceived += OnVideoErrorReceived;
            EnsureVideoTarget();
        }

        private void OnVideoErrorReceived(VideoPlayer source, string message)
        {
            _videoErrorReceived = true;
            Debug.LogWarning($"[BossVictoryCutscenePanel] Boss 胜利过场视频播放失败：{message}");
        }

        private void EnsureVideoTarget()
        {
            if (_renderTexture == null)
                _renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);

            if (_videoImage != null)
                _videoImage.texture = _renderTexture;

            if (_videoPlayer != null)
                _videoPlayer.targetTexture = _renderTexture;
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }
    }
}
