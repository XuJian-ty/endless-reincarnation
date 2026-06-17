using System;
using System.Collections;
using ProjectBase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 游戏开场剧情漫画面板：按任意键逐张翻页，结束后继续关卡原流程。
    /// </summary>
    public class OpeningStoryComicPanel : BasePanel
    {
        [Header("剧情图片")]
        [SerializeField] private Sprite firstPage;
        [SerializeField] private Sprite secondPage;
        [SerializeField] private Sprite thirdPage;

        [Header("提示文字")]
        [SerializeField] private string continuePrompt = "按下任意键继续";

        private Image _pageImage;
        private Text _promptText;
        private Sprite[] _pages;
        private int _pageIndex;
        private Action _onComplete;

        protected override void Awake()
        {
            EnsureRuntimeUi();
            base.Awake();
        }

        public void Play(Action onComplete)
        {
            _onComplete = onComplete;
            _pages = new[] { firstPage, secondPage, thirdPage };
            _pageIndex = 0;

            if (!HasAllPages())
            {
                Debug.LogError("[OpeningStoryComicPanel] 开场剧情漫画需要配置 3 张图片。");
                StartCoroutine(CompleteNextFrame());
                return;
            }

            gameObject.SetActive(true);
            ShowCurrentPage();
        }

        public override void ShowMe()
        {
            gameObject.SetActive(true);
        }

        public override void HideMe()
        {
            _onComplete = null;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_onComplete == null)
                return;

            if (!WasContinuePressed())
                return;

            _pageIndex++;
            if (_pageIndex >= _pages.Length)
            {
                Complete();
                return;
            }

            ShowCurrentPage();
        }

        private bool HasAllPages()
        {
            if (_pages == null || _pages.Length != 3)
                return false;

            for (int i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] == null)
                    return false;
            }

            return true;
        }

        private void ShowCurrentPage()
        {
            if (_pageImage != null)
                _pageImage.sprite = _pages[_pageIndex];

            if (_promptText != null)
                _promptText.text = continuePrompt ?? string.Empty;
        }

        private void Complete()
        {
            Action callback = _onComplete;
            _onComplete = null;
            UIManager.GetInstance()?.HidePanel(PanelNames.OpeningStoryComic);
            callback?.Invoke();
        }

        private IEnumerator CompleteNextFrame()
        {
            yield return null;
            Complete();
        }

        private static bool WasContinuePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;

            Mouse mouse = Mouse.current;
            if (mouse != null &&
                (mouse.leftButton.wasPressedThisFrame ||
                 mouse.rightButton.wasPressedThisFrame ||
                 mouse.middleButton.wasPressedThisFrame))
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null &&
                   (gamepad.buttonSouth.wasPressedThisFrame ||
                    gamepad.buttonEast.wasPressedThisFrame ||
                    gamepad.buttonWest.wasPressedThisFrame ||
                    gamepad.buttonNorth.wasPressedThisFrame ||
                    gamepad.startButton.wasPressedThisFrame);
        }

        private void EnsureRuntimeUi()
        {
            if (_pageImage != null && _promptText != null)
                return;

            RectTransform rootRect = transform as RectTransform;
            RuntimeOverlayPanelBuilder.EnsureFullScreenRoot(rootRect);

            GameObject imageObject = new GameObject("Img_Page", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(transform, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            _pageImage = imageObject.GetComponent<Image>();
            _pageImage.color = Color.white;
            _pageImage.preserveAspect = false;
            _pageImage.raycastTarget = true;

            GameObject promptObject = new GameObject("Text_ContinuePrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            promptObject.transform.SetParent(transform, false);
            RectTransform promptRect = promptObject.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(1f, 0f);
            promptRect.anchorMax = new Vector2(1f, 0f);
            promptRect.pivot = new Vector2(1f, 0f);
            promptRect.anchoredPosition = new Vector2(-56f, 56f);
            promptRect.sizeDelta = new Vector2(360f, 64f);

            _promptText = promptObject.GetComponent<Text>();
            _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _promptText.text = continuePrompt;
            _promptText.fontSize = 30;
            _promptText.alignment = TextAnchor.MiddleRight;
            _promptText.color = Color.white;
            _promptText.fontStyle = FontStyle.Bold;
            _promptText.raycastTarget = false;
            _promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _promptText.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
