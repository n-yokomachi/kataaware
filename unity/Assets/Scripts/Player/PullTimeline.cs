using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ジャックを抜く一連の間。左手を右手首へ伸ばし、掴み、引き抜き、戻す。
    /// 骨そのものは触らず、どの段でどれだけ曲げるかだけを持つ
    /// </summary>
    public struct PullTimeline
    {
        /// <summary>左手を右手首へ伸ばす</summary>
        public const float ReachSeconds = 0.60f;
        /// <summary>掴んで止まる</summary>
        public const float GripSeconds = 0.22f;
        /// <summary>引き抜く</summary>
        public const float PullSeconds = 0.48f;
        /// <summary>手を戻す</summary>
        public const float ReturnSeconds = 0.75f;

        public static float GripAt { get { return ReachSeconds; } }
        public static float PullAt { get { return ReachSeconds + GripSeconds; } }
        public static float LetGoAt { get { return PullAt + PullSeconds; } }
        public static float Total { get { return LetGoAt + ReturnSeconds; } }

        /// <summary>右手首へ伸ばす姿勢の強さ</summary>
        public static float Reach(float t)
        {
            if (t <= 0f) return 0f;
            if (t < ReachSeconds) return Mathf.SmoothStep(0f, 1f, t / ReachSeconds);
            if (t < LetGoAt) return 1f;
            if (t >= Total) return 0f;
            return Mathf.SmoothStep(1f, 0f, (t - LetGoAt) / ReturnSeconds);
        }

        /// <summary>引き抜いて手を退ける姿勢の強さ</summary>
        public static float Lift(float t)
        {
            if (t <= PullAt) return 0f;
            if (t < LetGoAt) return Mathf.SmoothStep(0f, 1f, (t - PullAt) / PullSeconds);
            if (t >= Total) return 0f;
            return Mathf.SmoothStep(1f, 0f, (t - LetGoAt) / ReturnSeconds);
        }

        /// <summary>左手がジャックを掴んでいる間。ここでジャックは左手に付いて動く</summary>
        public static bool Held(float t)
        {
            return t >= GripAt && t < LetGoAt;
        }

        /// <summary>抜き終わって置いた後</summary>
        public static bool LetGo(float t)
        {
            return t >= LetGoAt;
        }

        public static bool Done(float t)
        {
            return t >= Total;
        }
    }
}
