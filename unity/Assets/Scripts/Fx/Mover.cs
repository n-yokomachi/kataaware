using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 記憶の中で人や鳩を一直線に動かす。
    ///
    /// **線は二本まで。** 一本目の終わり（<see cref="To"/>）から二本目の先（<see cref="Next"/>）へ続けて動ける。
    /// 駆け出して戻ってくる人（記憶 9 の孫息子）のため。二本目の秒（<see cref="NextSpan"/>）が 0 なら一本だけ。
    /// 同じ人に Mover を二つ付けると、後から置いた方が先の方の位置を毎フレーム上書きするので、一つにまとめる。
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
        [Tooltip("何行目の台詞が出たら動き出すか。-1 なら記憶の時計で動く")]
        [SerializeField] int cue = -1;
        [Tooltip("二本目の線の先。一本目の終わり（to）からここへ。Take からのローカル")]
        [SerializeField] Vector3 next;
        [Tooltip("二本目の線を動き出す秒。一本目と同じ時計で数える")]
        [SerializeField] float nextAt;
        [Tooltip("二本目の線を動いているあいだの秒。0 なら二本目は無い")]
        [SerializeField] float nextSpan;

        public Vector3 From { get { return from; } }
        public Vector3 To { get { return to; } }
        public float At { get { return at; } }
        public float Span { get { return span; } }
        public Vector3 Next { get { return next; } }
        public float NextAt { get { return nextAt; } }
        public float NextSpan { get { return nextSpan; } }
        /// <summary>二本目があるか</summary>
        public bool Returns { get { return nextSpan > 0f; } }
        /// <summary>動き終わる所。二本目があればその先</summary>
        public Vector3 End { get { return Returns ? next : to; } }
        /// <summary>動き終わる秒</summary>
        public float Until { get { return Returns ? Mathf.Max(at + span, nextAt + nextSpan) : at + span; } }

        /// <summary>
        /// 動き出す合図。これだけの行数が出たら動き始める。-1 なら記憶の時計で動く。
        ///
        /// **会話が歩いて進むのに、人だけ秒で動くのはちぐはぐ。** 台詞はプレイヤーが
        /// 点へ入るまで待つのに、娘は記憶が始まって 6 秒で階段を駆け上がっていた。
        /// まだ話しかけられてもいないうちに動き出す人を見て、意図した動きなのかと問われた。
        /// 合図を持つ者は、その行が出てから数え始める
        /// </summary>
        public int Cue { get { return cue; } }

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

        /// <summary>t 秒の位置。始まる前は開始位置、終わった後は終了位置（二本目があればその先）</summary>
        public Vector3 Where(float t)
        {
            if (Returns && t >= nextAt) return Leg(to, next, t, nextAt, nextSpan);
            return Leg(from, to, t, at, span);
        }

        Vector3 Leg(Vector3 a, Vector3 b, float t, float start, float length)
        {
            if (length <= 0f) return t < start ? a : b;
            var k = Mathf.Clamp01((t - start) / length);
            return Vector3.Lerp(a, b, ease ? Mathf.SmoothStep(0f, 1f, k) : k);
        }
    }
}
