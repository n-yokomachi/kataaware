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
        }

        /// <summary>t 秒の位置。始まる前は開始位置、終わった後は終了位置</summary>
        public Vector3 Where(float t)
        {
            if (span <= 0f) return t < at ? from : to;
            var k = Mathf.Clamp01((t - at) / span);
            return Vector3.Lerp(from, to, ease ? Mathf.SmoothStep(0f, 1f, k) : k);
        }
    }
}
