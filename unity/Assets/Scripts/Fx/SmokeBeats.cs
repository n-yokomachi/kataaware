using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 一本吸い終わるまでの間合い。ジッポで火を点け、吸い、止め、吐く、を決めた回数だけ繰り返す。
    /// 音も煙もカードもこの時刻表に合わせるので、ずれない。
    ///
    /// **火はジッポで点ける**（オーナー、2026-09-28「ライターの音にZippoの開閉音を追加する。…開いて、点火して、閉じるまでの音が入っている。
    /// 点火したタイミングで、煙草に火が移った音を鳴らして」）。ジッポの音（<c>Zippo.wav</c>）は開く・点く・閉じるが一つに入っていて、
    /// 点いた瞬間（<see cref="StrikeInZippo"/>）から煙草に火が移った音（<c>CigaretteLit.wav</c>）を鳴らし、煙もそこから立ちはじめる。
    ///
    /// **火を点けてから吸い始めるまでに、向き直す間（turn）を挟める。** 場面 1 は煙草を調べた向きのまま火を点け、
    /// ジッポの音と火が移った音が鳴りきってから座り始めの向きへ戻してから吸う（<see cref="RoomIntroDirector"/>）。場面 5 は挟まない（0）
    /// </summary>
    public static class SmokeBeats
    {
        /// <summary>火を点け始めて（場面 1 は煙草を取った 1 ページを送って）から、ジッポを開け始めるまで</summary>
        public const float ZippoAt = 0.30f;

        /// <summary>
        /// ジッポの音の中で、フリントを擦って火が点く時刻。音の頭から。
        /// 切り出しの中の 0.954 秒（蓋を開ける金属音の後、フリントを擦る雑音の塊の頭。<c>tools/make-ambience.sh smoke</c> が測って出す）。
        /// **素材か切り出しを替えたら測り直す**
        /// </summary>
        public const float StrikeInZippo = 0.954f;

        /// <summary>ジッポの音の長さ（開く・点く・閉じる）。<c>Zippo.wav</c> の実尺。素材を替えたら合わせる（ZippoSoundTests が確かめる）</summary>
        public const float ZippoSeconds = 3.691f;

        /// <summary>煙草に火が移った音の長さ。<c>CigaretteLit.wav</c> の実尺。素材を替えたら合わせる</summary>
        public const float LitSeconds = 3.142f;

        /// <summary>吸っている長さ。素材の長さに合わせてある</summary>
        public const float DragSeconds = 3.70f;
        /// <summary>肺に留めている長さ</summary>
        public const float HoldSeconds = 0.55f;
        /// <summary>吐いている長さ。素材の長さに合わせてある</summary>
        public const float BlowSeconds = 3.35f;
        /// <summary>吐き終わってから次に吸うまで</summary>
        public const float RestSeconds = 1.60f;

        /// <summary>何服で一本にするか。2026-09-28 にオーナーの指示で 3 から 2 に減らした（「吸う音吐く音は１回ずつ減らす」）</summary>
        public const int Drags = 2;

        /// <summary>吐き始めてから画面が暗くなるまで</summary>
        public const float CardAfterBlow = 0.50f;

        /// <summary>最後に吐き終わってから独白までの間</summary>
        public const float TailSeconds = 1.40f;

        /// <summary>
        /// 最後の一服だけ、吸う前と吐く前に置く間。
        /// ここで一拍おくと、終わりに向かっているのが伝わる
        /// </summary>
        public const float LastPauseSeconds = 1.70f;

        /// <summary>ジッポに火が点き、煙草に火が移った音を鳴らす時刻。煙もここから立ちはじめる</summary>
        public static float LitAt { get { return ZippoAt + StrikeInZippo; } }

        /// <summary>煙が立ちはじめる時刻。火が点いたところから</summary>
        public static float SmokeAt { get { return LitAt; } }

        /// <summary>ジッポの音（閉じるまで）と火が移った音が、どちらも鳴りきった時刻。向き直す間を挟むなら、ここから向き直し始める</summary>
        public static float TurnAt { get { return Mathf.Max(ZippoAt + ZippoSeconds, LitAt + LitSeconds); } }

        /// <summary>向き直す間を挟まないときの、最初の一服の時刻</summary>
        public static float FirstDragAt { get { return TurnAt; } }

        public static float Cycle { get { return DragSeconds + HoldSeconds + BlowSeconds + RestSeconds; } }

        /// <summary>最後の一服か</summary>
        public static bool IsLast(int i, int drags)
        {
            return drags > 0 && i == drags - 1;
        }

        /// <summary>
        /// i 服目に吸い始める時刻。0 から数える。turn は火の音が鳴りきってから吸い始めるまでに挟む、向き直す間（秒）。
        /// 最後の一服だけ、吸い始める前に一拍おく
        /// </summary>
        public static float DragAt(int i, int drags, float turn = 0f)
        {
            var at = FirstDragAt + Mathf.Max(0f, turn) + Cycle * Mathf.Max(0, i);
            return IsLast(i, drags) ? at + LastPauseSeconds : at;
        }

        /// <summary>i 服目を吐き始める時刻。最後の一服だけ、吐く前にもう一拍おく</summary>
        public static float BlowAt(int i, int drags, float turn = 0f)
        {
            var at = DragAt(i, drags, turn) + DragSeconds + HoldSeconds;
            return IsLast(i, drags) ? at + LastPauseSeconds : at;
        }

        /// <summary>i 服目のカードを出す時刻。吐き始めてすぐ暗くする（場面 1 は最後の一服にだけ出す）</summary>
        public static float CardAt(int i, int drags, float turn = 0f)
        {
            return BlowAt(i, drags, turn) + CardAfterBlow;
        }

        /// <summary>
        /// 明けきってからひと呼吸おく。カードから明けるのに時刻表より手間取ったら、
        /// 明け終わったところから数え直す。独白が明けに食い込まないように
        /// </summary>
        public static float Settle(float now, float scheduled, float breath)
        {
            var after = now + Mathf.Max(0f, breath);
            return scheduled > after ? scheduled : after;
        }

        /// <summary>drags 服ぶんを吸い終えて、独白に移るまでの長さ</summary>
        public static float Total(int drags, float turn = 0f)
        {
            if (drags <= 0) return FirstDragAt + Mathf.Max(0f, turn);
            return BlowAt(drags - 1, drags, turn) + BlowSeconds + TailSeconds;
        }
    }
}
