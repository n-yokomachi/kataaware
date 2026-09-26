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
        [Tooltip("二本目の線を動き出す合図。何行目の台詞が出たら数え始めるか。-1 なら一本目と同じ時計で数える")]
        [SerializeField] int nextCue = -1;
        [Tooltip("動き出す秒で向きを変える。その場で振り向く人のため")]
        [SerializeField] bool turns;
        [Tooltip("動き出す前の向き。度。Take からのローカル")]
        [SerializeField] float yawFrom;
        [Tooltip("動き出してからの向き。度。Take からのローカル")]
        [SerializeField] float yawTo;

        public Vector3 From { get { return from; } }
        public Vector3 To { get { return to; } }
        public float At { get { return at; } }
        public float Span { get { return span; } }
        public Vector3 Next { get { return next; } }
        public float NextAt { get { return nextAt; } }
        public float NextSpan { get { return nextSpan; } }
        /// <summary>動き出す秒で向きを変えるか</summary>
        public bool Turns { get { return turns; } }
        public float YawFrom { get { return yawFrom; } }
        public float YawTo { get { return yawTo; } }
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

        /// <summary>
        /// 二本目の線の合図。これだけの行数が出たら二本目を数え始める。-1 なら一本目と同じ時計。
        ///
        /// **駆け出す線と戻ってくる線は、別の行で動き出す。** 記憶 10 の孫息子は、夫とのやりとりのあとで
        /// 鳩を追って駆け出し、名を呼ばれて「今行くって」と返してから戻ってくる。
        /// 二本とも一つの合図の時計で数えると、呼ばれる前に戻ってくるか、返事の後もしばらく池にいる
        /// </summary>
        public int NextCue { get { return nextCue; } }

        /// <summary>頭から流し直すので、有効になった瞬間は開始位置に戻しておく</summary>
        void OnEnable()
        {
            Play(0f);
        }

        /// <summary>記憶の頭からの秒を渡して置き直す</summary>
        public void Play(float t)
        {
            Play(t, t);
        }

        /// <summary>
        /// 一本目の時計 t と、二本目の合図からの秒 after を渡して置き直す。
        /// 二本目が一本目と同じ時計（<see cref="NextCue"/> が -1）なら after は見ない。
        /// 二本目の合図がまだなら after は負
        /// </summary>
        public void Play(float t, float after)
        {
            transform.localPosition = nextCue < 0 ? Where(t) : Where(t, after);
            // **根の向きは一息に替える。** 模型は PersonMotion がこまの頭ごとに根の向きへ寄せるので
            // （TurnPerTick）、振り向きはそちらで段々に回って見える
            if (turns) transform.localRotation = Quaternion.Euler(0f, t < at ? yawFrom : yawTo, 0f);
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

        /// <summary>二本目を別の合図で数えるときの位置。合図の前（after が負）は一本目の線の上</summary>
        public Vector3 Where(float t, float after)
        {
            if (Returns && after >= 0f) return Leg(to, next, after, nextAt, nextSpan);
            return Leg(from, to, t, at, span);
        }

        /// <summary>
        /// いま線の上を動いているか。一本目の時計 t と、二本目の合図からの秒 after（合図の前は負）で見る。
        /// 動き出す前と動き終えた後は止まっている
        /// </summary>
        public bool Moving(float t, float after)
        {
            if (t >= at && t < at + span) return true;
            if (!Returns) return false;
            var second = nextCue < 0 ? t : after;
            return second >= nextAt && second < nextAt + nextSpan;
        }

        Vector3 Leg(Vector3 a, Vector3 b, float t, float start, float length)
        {
            if (length <= 0f) return t < start ? a : b;
            var k = Mathf.Clamp01((t - start) / length);
            return Vector3.Lerp(a, b, ease ? Mathf.SmoothStep(0f, 1f, k) : k);
        }
    }
}
