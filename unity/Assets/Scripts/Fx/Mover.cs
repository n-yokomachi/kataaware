using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 記憶の中で人や鳩を一直線に動かす。
    ///
    /// **自分の時計を持たない。** 記憶の再生位置は DiveDirector が握っていて、
    /// 同じ人へ戻れば頭から流し直すので、ここが Update で数えると主の体とずれる。
    /// 経過秒は外から渡してもらう。
    ///
    /// 位置は Take のローカル。場所ごと動かしても付いてくるようにするため
    /// </summary>
    public sealed class Mover : MonoBehaviour
    {
        [Tooltip("開始位置。Take からのローカル")]
        [SerializeField] Vector3 from;
        [Tooltip("終了位置。Take からのローカル")]
        [SerializeField] Vector3 to;
        [Tooltip("記憶の頭から数えて、動き出す秒")]
        [SerializeField] float at;
        [Tooltip("動いているあいだの秒")]
        [SerializeField] float span = 1f;
        [Tooltip("端を滑らかに繋ぐ。鳩の飛び立ちのように弾けるものは切る")]
        [SerializeField] bool ease = true;
        [Tooltip("足元を床へ下ろす。飛ぶものは切る")]
        [SerializeField] bool ground = true;

        public Vector3 From { get { return from; } }
        public Vector3 To { get { return to; } }
        public float At { get { return at; } }
        public float Span { get { return span; } }

        /// <summary>頭から流し直すので、有効になった瞬間は開始位置に戻しておく</summary>
        void OnEnable()
        {
            Play(0f);
        }

        /// <summary>記憶の頭からの秒を渡して置き直す</summary>
        public void Play(float t)
        {
            transform.localPosition = Where(t);
            if (ground) Land();
        }

        /// <summary>
        /// 足元を真下の床へ下ろす。
        ///
        /// **一直線では段を追えない。** 階段を降りる人も、教壇から降りる人も、
        /// 始まりと終わりを結んだ線の途中では段板から浮いたり沈んだりする。
        /// 実測で最大 1.64 m 宙に立っていた。線は前後左右だけに使い、
        /// 上下はその場の床に任せる
        /// </summary>
        void Land()
        {
            RaycastHit floor;
            var from = transform.position + Vector3.up * Reach;
            if (!Physics.Raycast(from, Vector3.down, out floor, Reach + Drop)) return;
            var at = transform.position;
            transform.position = new Vector3(at.x, floor.point.y, at.z);
        }

        /// <summary>床を探し始める高さ。頭の上の荷棚や天井を拾わないよう、足元のすぐ上から</summary>
        const float Reach = 0.3f;
        /// <summary>そこから下へ探す長さ。これより下に何も無ければ、線のままにしておく</summary>
        const float Drop = 4f;

        /// <summary>t 秒の位置。始まる前は開始位置、終わった後は終了位置</summary>
        public Vector3 Where(float t)
        {
            if (span <= 0f) return t < at ? from : to;
            var k = Mathf.Clamp01((t - at) / span);
            return Vector3.Lerp(from, to, ease ? Mathf.SmoothStep(0f, 1f, k) : k);
        }
    }
}
