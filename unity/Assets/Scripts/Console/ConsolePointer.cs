using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HalfAware
{
    /// <summary>
    /// コンソールの部品へのマウスの出入り・押す・掴んで動かすを、組み立てた側へ渡すだけの中継。
    /// uGUI の Button や Scrollbar は使わない。あれは選ばれると矢印の入力を自分で取り、
    /// コンソールの左右・上下と二重に動く
    /// </summary>
    public sealed class ConsolePointer : MonoBehaviour,
        IPointerEnterHandler, IPointerClickHandler, IPointerDownHandler, IDragHandler
    {
        public Action Entered;
        public Action Clicked;
        /// <summary>押した・掴んで動かした所。部品の中の位置（左下が 0、右上が 1）</summary>
        public Action<Vector2> Held;

        public void OnPointerEnter(PointerEventData e)
        {
            if (Entered != null) Entered();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Clicked != null) Clicked();
        }

        public void OnPointerDown(PointerEventData e)
        {
            Hold(e);
        }

        public void OnDrag(PointerEventData e)
        {
            Hold(e);
        }

        void Hold(PointerEventData e)
        {
            if (Held == null || e.button != PointerEventData.InputButton.Left) return;
            var rect = (RectTransform)transform;
            Vector2 local;
            // 粗い画面（UiLens）で描いているので、画面の座標をそちらへ直してから測る
            var at = e.pressEventCamera != null ? UiLens.ToLens(e.position) : e.position;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, at, e.pressEventCamera, out local)) return;
            var r = rect.rect;
            Held(new Vector2(
                r.width > 0f ? Mathf.Clamp01((local.x - r.xMin) / r.width) : 0f,
                r.height > 0f ? Mathf.Clamp01((local.y - r.yMin) / r.height) : 0f));
        }
    }
}
