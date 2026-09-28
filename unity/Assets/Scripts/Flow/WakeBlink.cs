using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の冒頭の明け（オーナー、2026-09-28「5秒くらいは暗転のままループ再生。五秒にゆっくり瞬きをするような画面の暗転の開け方をする」）。
    /// 黒のまま置き、少しだけ開きかけて閉じ、一拍おいてから、ゆっくり明けきる。
    ///
    /// **瞼の形は作らない。** 画面の黒い層（<see cref="HudView.SetFade"/>）の濃さだけで見せる。
    /// 以前に瞼が上下から閉じる形を試したが、目の形を作り込んでも安っぽく見えた（シナリオ設計 5 節）。
    ///
    /// 秒は冒頭（黒のまま置く間の頭）から数える。濃さは 1 が真っ黒、0 が透明
    /// </summary>
    public readonly struct WakeBlink
    {
        /// <summary>黒のまま置く秒。呼吸だけが聞こえる</summary>
        public readonly float Dark;
        /// <summary>開きかけるのにかける秒</summary>
        public readonly float Peek;
        /// <summary>開きかけて、どこまで明けるか。0〜1。黒の層を 1 − Glimpse まで薄くする</summary>
        public readonly float Glimpse;
        /// <summary>開きかけたまま止める秒</summary>
        public readonly float Hold;
        /// <summary>閉じるのにかける秒</summary>
        public readonly float Shut;
        /// <summary>閉じてから明け始めるまでの一拍。秒</summary>
        public readonly float Pause;
        /// <summary>明けきるのにかける秒</summary>
        public readonly float Open;

        public WakeBlink(float dark, float peek, float glimpse, float hold, float shut, float pause, float open)
        {
            Dark = Mathf.Max(0f, dark);
            Peek = Mathf.Max(0f, peek);
            Glimpse = Mathf.Clamp01(glimpse);
            Hold = Mathf.Max(0f, hold);
            Shut = Mathf.Max(0f, shut);
            Pause = Mathf.Max(0f, pause);
            Open = Mathf.Max(0f, open);
        }

        /// <summary>開きかけ始める時刻</summary>
        public float PeekAt => Dark;

        /// <summary>閉じ始める時刻</summary>
        public float ShutAt => Dark + Peek + Hold;

        /// <summary>明け始める時刻</summary>
        public float OpenAt => ShutAt + Shut + Pause;

        /// <summary>明けきる時刻</summary>
        public float Total => OpenAt + Open;

        /// <summary>t 秒目に明けきっているか</summary>
        public bool Done(float t) => t >= Total;

        /// <summary>t 秒目の黒の層の濃さ。1 が真っ黒</summary>
        public float Level(float t)
        {
            var low = 1f - Glimpse;
            if (t < PeekAt) return 1f;
            if (t < PeekAt + Peek) return Mathf.Lerp(1f, low, Ease(t - PeekAt, Peek));
            if (t < ShutAt) return low;
            if (t < ShutAt + Shut) return Mathf.Lerp(low, 1f, Ease(t - ShutAt, Shut));
            if (t < OpenAt) return 1f;
            if (t < Total) return 1f - Ease(t - OpenAt, Open);
            return 0f;
        }

        /// <summary>seconds 秒かける動きの、t 秒目の進み。両端をなだらかに</summary>
        static float Ease(float t, float seconds)
        {
            return seconds > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds)) : 1f;
        }
    }
}
