using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 粗い画面（<see cref="UiLens"/>）で描く Canvas の当たり。
    /// マウスの位置は画面の座標で来るが、Canvas を描くカメラは粗い画面の大きさなので、
    /// そのままでは当たりの位置がずれる。位置を粗い画面の座標へ直してから、ふだんどおり調べる
    /// </summary>
    public sealed class LensRaycaster : GraphicRaycaster
    {
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            var kept = eventData.position;
            eventData.position = UiLens.ToLens(kept);
            try
            {
                base.Raycast(eventData, resultAppendList);
            }
            finally
            {
                eventData.position = kept;
            }
        }
    }
}
