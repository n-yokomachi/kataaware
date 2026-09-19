using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ジャックを抜く一連の間。手首へ視線を落として刺さっているところを見せ、
    /// 左手を伸ばし、掴み、引き抜き、ケーブルが張るところまで前へ出して見せ、最後に手を戻す。
    /// 骨そのものは触らず、どの段でどれだけ曲げるかだけを持つ
    /// </summary>
    public struct PullTimeline
    {
        /// <summary>手首のジャックへ視線を落とす</summary>
        public const float LookSeconds = 1.05f;
        /// <summary>刺さっているところを見せたまま止める</summary>
        public const float StareSeconds = 0.75f;
        /// <summary>左手を右手首へ伸ばす</summary>
        public const float ReachSeconds = 0.70f;
        /// <summary>掴んで止まる</summary>
        public const float GripSeconds = 0.35f;
        /// <summary>引き抜く</summary>
        public const float PullSeconds = 0.55f;
        /// <summary>抜いたジャックを前へ出す。ケーブルが張って見えるところまで</summary>
        public const float ShowSeconds = 0.70f;
        /// <summary>ケーブルごと見せたまま止める</summary>
        public const float CableSeconds = 1.40f;
        /// <summary>手を戻す</summary>
        public const float ReturnSeconds = 0.75f;

        public static float ReachAt { get { return LookSeconds + StareSeconds; } }
        public static float GripAt { get { return ReachAt + ReachSeconds; } }
        public static float PullAt { get { return GripAt + GripSeconds; } }
        public static float ShowAt { get { return PullAt + PullSeconds; } }
        public static float LetGoAt { get { return ShowAt + ShowSeconds + CableSeconds; } }
        public static float Total { get { return LetGoAt + ReturnSeconds; } }

        /// <summary>
        /// 0 から 1 へ上がって、戻しの間に 0 へ下がる形。
        /// at から seconds かけて上がり、LetGoAt から ReturnSeconds かけて下がる
        /// </summary>
        static float Swell(float t, float at, float seconds)
        {
            if (t <= at) return 0f;
            if (t < at + seconds) return Mathf.SmoothStep(0f, 1f, (t - at) / seconds);
            if (t < LetGoAt) return 1f;
            if (t >= Total) return 0f;
            return Mathf.SmoothStep(1f, 0f, (t - LetGoAt) / ReturnSeconds);
        }

        /// <summary>手首のジャックへ視線を寄せる強さ。0 で元の向き、1 でジャックの真正面</summary>
        public static float Aim(float t)
        {
            return Swell(t, 0f, LookSeconds);
        }

        /// <summary>右手首へ伸ばす姿勢の強さ</summary>
        public static float Reach(float t)
        {
            return Swell(t, ReachAt, ReachSeconds);
        }

        /// <summary>引き抜いて手を退ける姿勢の強さ</summary>
        public static float Lift(float t)
        {
            return Swell(t, PullAt, PullSeconds);
        }

        /// <summary>抜いたジャックを前へ出す姿勢の強さ</summary>
        public static float Show(float t)
        {
            return Swell(t, ShowAt, ShowSeconds);
        }

        /// <summary>左手がジャックを掴んでいる間。ここでジャックは左手に付いて動く</summary>
        public static bool Held(float t)
        {
            return t >= GripAt && t < LetGoAt;
        }

        /// <summary>引き抜けた後。音が鳴り、刺さっていた印も消える</summary>
        public static bool Out(float t)
        {
            return t >= PullAt;
        }

        /// <summary>見せ終わって置いた後</summary>
        public static bool LetGo(float t)
        {
            return t >= LetGoAt;
        }

        /// <summary>
        /// 視線がまだジャックを追ってよいか。手を離すとジャックは肘掛けへ移るので、
        /// そのまま追うと視線が右下へ飛ぶ。離した後は最後に見ていた先を保つ
        /// </summary>
        public static bool Follows(float t)
        {
            return !LetGo(t);
        }

        public static bool Done(float t)
        {
            return t >= Total;
        }
    }
}
