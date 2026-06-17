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
        [Header("水印遮挡")]
        [SerializeField] private Sprite watermarkCoverSprite;
        [SerializeField] private Vector2 watermarkCoverSize = new Vector2(320f, 96f);
        [SerializeField] private Vector2 watermarkCoverOffset = new Vector2(-42f, 42f);
        [SerializeField] private Color watermarkCoverColor = new Color(0.75f, 0.86f, 1f, 0.92f);
        [SerializeField] private bool showTopLeftCover = true;
        [SerializeField] private Sprite topLeftCoverSprite;
        [SerializeField] private Vector2 topLeftCoverSize = new Vector2(320f, 96f);
        [SerializeField] private Vector2 topLeftCoverOffset = new Vector2(42f, -42f);
        [SerializeField] private Color topLeftCoverColor = new Color(0.75f, 0.86f, 1f, 0.92f);

        private RawImage _videoImage;
        private Image _watermarkCoverImage;
        private Image _topLeftCoverImage;
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
            if (_videoImage != null && _videoPlayer != null && _watermarkCoverImage != null && (!showTopLeftCover || _topLeftCoverImage != null))
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

            _watermarkCoverImage = CreateCoverImage(
                "Img_WatermarkCover",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                watermarkCoverOffset,
                watermarkCoverSize,
                watermarkCoverSprite,
                watermarkCoverColor);

            if (showTopLeftCover)
            {
                _topLeftCoverImage = CreateCoverImage(
                    "Img_TopLeftCover",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    topLeftCoverOffset,
                    topLeftCoverSize,
                    topLeftCoverSprite,
                    topLeftCoverColor);
            }

            _videoPlayer = gameObject.GetComponent<VideoPlayer>();
            if (_videoPlayer == null)
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();

            _videoPlayer.playOnAwake = false;
            _videoPlayer.waitForFirstFrame = true;
            _videoPlayer.errorReceived -= OnVideoErrorReceived;
            _videoPlayer.errorReceived += OnVideoErrorReceived;
            EnsureVideoTarget();
        }

        private Image CreateCoverImage(
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Sprite sprite,
            Color color)
        {
            GameObject coverObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coverObject.transform.SetParent(transform, false);

            RectTransform coverRect = coverObject.GetComponent<RectTransform>();
            coverRect.anchorMin = anchorMin;
            coverRect.anchorMax = anchorMax;
            coverRect.pivot = pivot;
            coverRect.anchoredPosition = anchoredPosition;
            coverRect.sizeDelta = size;

            Image coverImage = coverObject.GetComponent<Image>();
            coverImage.sprite = sprite;
            coverImage.type = Image.Type.Simple;
            coverImage.color = color;
            coverImage.raycastTarget = false;
            return coverImage;
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
