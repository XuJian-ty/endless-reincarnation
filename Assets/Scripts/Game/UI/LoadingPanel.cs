using UnityEngine;
using UnityEngine.UI;
using Game;
using ProjectBase;

namespace Game.UI
{
    /// <summary>
    /// 加载进度面板：底部显示进度条。
    /// 结构：填充图在下 → 遮罩图在上（通过 RectMask2D 从左到右裁剪）
    /// 效果：从左到右逐渐裁掉遮罩图、露出填充图。
    /// </summary>
    public class LoadingPanel : BasePanel
    {
        [Header("进度显示")]
        [SerializeField] private Text progressText;

        [Header("层级结构")]
        [Tooltip("填充图容器")]
        [SerializeField] private RectTransform fillRect;
        [Tooltip("裁切容器，带 RectMask2D")]
        [SerializeField] private RectTransform clipRect;

        private RectTransform progressBarRect;
        private RectTransform coverImageRect;

        protected override void Awake()
        {
            base.Awake();
            // 获取 ProgressBar 的 RectTransform
            if (fillRect != null)
                progressBarRect = fillRect.parent as RectTransform;

            // 获取 clipRect 内的遮罩图
            if (clipRect != null && clipRect.childCount > 0)
                coverImageRect = clipRect.GetChild(0) as RectTransform;
        }

        private void OnEnable()
        {
            EventCenter.GetInstance().AddEventListener<float>(GameEvents.SceneLoadProgress, OnProgress);
        }

        private void OnDisable()
        {
            var ec = EventCenter.GetInstance();
            if (ec != null) ec.RemoveEventListener<float>(GameEvents.SceneLoadProgress, OnProgress);
        }

        private void OnProgress(float progress)
        {
            float p = Mathf.Clamp01(progress);

            // 强制更新 Canvas 布局，确保获取正确的尺寸
            if (progressBarRect != null)
                Canvas.ForceUpdateCanvases();

            // 填充容器：铺满整个进度条 [0, 1]
            if (fillRect != null)
            {
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(1f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            // 裁切容器（带 RectMask2D）：从 [0, 1] 缩小到 [1, 1]
            if (clipRect != null)
            {
                EnsureRectMask2D(clipRect);
                clipRect.anchorMin = new Vector2(p, 0f);
                clipRect.anchorMax = new Vector2(1f, 1f);
                clipRect.offsetMin = Vector2.zero;
                clipRect.offsetMax = Vector2.zero;
            }

            // 遮罩图保持固定大小，不随 clipRect 缩放
            if (coverImageRect != null && progressBarRect != null)
            {
                // 设置锚点为左下角，Pivot 也设为左下角
                coverImageRect.anchorMin = Vector2.zero;
                coverImageRect.anchorMax = Vector2.zero;
                coverImageRect.pivot = Vector2.zero;

                // 固定大小（等于整个进度条宽度）
                coverImageRect.sizeDelta = new Vector2(progressBarRect.rect.width, progressBarRect.rect.height);

                // 固定位置（对齐到进度条左边）
                float offsetX = -p * progressBarRect.rect.width;
                coverImageRect.anchoredPosition = new Vector2(offsetX, 0f);
            }

            if (progressText != null) progressText.text = $"{Mathf.FloorToInt(p * 100f)}%";
        }

        private static void EnsureRectMask2D(RectTransform rect)
        {
            if (rect != null && rect.GetComponent<RectMask2D>() == null)
                rect.gameObject.AddComponent<RectMask2D>();
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
            // 延迟一帧，等待 Canvas 完成布局
            StartCoroutine(InitializeProgressBar());
        }

        private System.Collections.IEnumerator InitializeProgressBar()
        {
            yield return null; // 等待一帧
            OnProgress(0f);
        }

        public override void HideMe()
        {
            gameObject.SetActive(false);
        }
    }
}
