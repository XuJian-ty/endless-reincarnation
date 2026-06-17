using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 仅滚轮滚动的 Scroll Rect：不显示水平滚动条、不支持鼠标拖动，只支持鼠标滚轮垂直滚动。
    /// 用法：在 Scroll View 根物体上，用本组件**替换**原有的 Scroll Rect（或先移除 Scroll Rect，再添加本组件并重新绑定 Content、Viewport、Vertical Scrollbar）。
    /// </summary>
    public class ScrollRectWheelOnly : ScrollRect
    {
        protected override void Start()
        {
            base.Start();
            horizontal = false;
            if (horizontalScrollbar != null)
                horizontalScrollbar.gameObject.SetActive(false);
            else
            {
                var hor = transform.Find("Scrollbar Horizontal");
                if (hor != null) hor.gameObject.SetActive(false);
            }
        }

        public override void OnBeginDrag(PointerEventData eventData) { }
        public override void OnDrag(PointerEventData eventData) { }
        public override void OnEndDrag(PointerEventData eventData) { }
    }
}
