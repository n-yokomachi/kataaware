using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 台詞の合図で手から手へ渡る持ち物。渡す側の手の物は合図で消え、受け取る側の手の物は合図で現れる。
    ///
    /// **物そのものは動かさない。** 手の骨に付けた二つの形のうち、どちらを見せるかを替えるだけ。
    /// 記憶 10・11 の餌の袋は、ローザの「餌の袋持ってて」で祖母の手から孫の手へ移る。
    /// 主の手は描かないので、主が渡す側（記憶 10）では孫の手に現れるだけ、
    /// 主が受け取る側（記憶 11）では祖母の手から消えるだけになる。
    ///
    /// <see cref="Mover"/> と同じく自分の時計を持たない。出た行数を DiveDirector から渡してもらう
    /// </summary>
    public sealed class Handed : MonoBehaviour
    {
        [Tooltip("何行目の台詞が出たら渡るか。これだけの行数が出たら替わる")]
        [SerializeField] int cue = 1;
        [Tooltip("合図で現れる（受け取る側）。切れば合図で消える（渡す側）")]
        [SerializeField] bool arrives = true;

        public int Cue { get { return cue; } }
        public bool Arrives { get { return arrives; } }

        /// <summary>頭から流し直すので、有効になった瞬間は合図の前の形に戻しておく</summary>
        void OnEnable()
        {
            Show(0);
        }

        /// <summary>出た行数 spoken を渡して、見せるかを替える</summary>
        public void Show(int spoken)
        {
            var on = spoken >= cue ? arrives : !arrives;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }
    }
}
