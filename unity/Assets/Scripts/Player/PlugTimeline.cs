using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ジャックを挿す一連の間。肘掛けのジャックへ視線を落とし、左手を伸ばして掴み、
    /// 右手首の前へ運んで挿し、挿さったところを見せてから手を戻す。
    /// 骨そのものは触らず、どの段でどれだけ曲げるかだけを持つ。
    ///
    /// <see cref="PullTimeline"/> の裏返しだが、段の数も意味も違うので別に持つ。
    /// 一つにまとめると、抜く側を詰めたときに挿す側が黙って動く
    /// </summary>
    public struct PlugTimeline
    {
        /// <summary>肘掛けのジャックへ視線を落とす</summary>
        public const float LookSeconds = 0.90f;
        /// <summary>左手を伸ばす</summary>
        public const float ReachSeconds = 0.70f;
        /// <summary>掴んで止まる</summary>
        public const float GripSeconds = 0.35f;
        /// <summary>右手首の前へ運ぶ</summary>
        public const float CarrySeconds = 0.85f;
        /// <summary>挿す</summary>
        public const float PushSeconds = 0.45f;
        /// <summary>挿さったところを見せたまま止める</summary>
        public const float SettleSeconds = 0.60f;
        /// <summary>手を戻す</summary>
        public const float ReturnSeconds = 0.75f;

        public static float GripAt { get { return LookSeconds + ReachSeconds; } }
        public static float CarryAt { get { return GripAt + GripSeconds; } }
        public static float PushAt { get { return CarryAt + CarrySeconds; } }
        /// <summary>挿さった瞬間。音が鳴り、ジャックは左手から手首へ移る</summary>
        public static float InAt { get { return PushAt + PushSeconds; } }
        public static float LetGoAt { get { return InAt + SettleSeconds; } }
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

        /// <summary>
        /// 視線をジャックへ寄せる強さ。0 で元の向き、1 でジャックの真正面。
        ///
        /// **上がったら戻さない。** 手は肘掛けへ戻すが、視線は挿したところを
        /// 見たまま置いておく。元の向きへ引き戻すと、挿し終わりに首だけが
        /// 勝手に振れて、誰かに向き直させられたように見える
        /// </summary>
        public static float Aim(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= LookSeconds) return 1f;
            return Mathf.SmoothStep(0f, 1f, t / LookSeconds);
        }

        /// <summary>左手を肘掛けへ伸ばす姿勢の強さ</summary>
        public static float Reach(float t) { return Swell(t, LookSeconds, ReachSeconds); }

        /// <summary>掴んだジャックを手首の前へ運ぶ姿勢の強さ</summary>
        public static float Carry(float t) { return Swell(t, CarryAt, CarrySeconds); }

        /// <summary>挿し込む姿勢の強さ</summary>
        public static float Push(float t) { return Swell(t, PushAt, PushSeconds); }

        /// <summary>左手がジャックを掴んでいる間。ここでジャックは左手について動く</summary>
        public static bool Held(float t) { return t >= GripAt && t < InAt; }

        /// <summary>挿さった後。音が鳴り、ジャックは手首につく</summary>
        public static bool In(float t) { return t >= InAt; }

        /// <summary>見せ終わって手を戻し始めた後</summary>
        public static bool LetGo(float t) { return t >= LetGoAt; }

        /// <summary>視線がまだジャックを追ってよいか。手を戻し始めたら追わない</summary>
        public static bool Follows(float t) { return !LetGo(t); }

        public static bool Done(float t) { return t >= Total; }
    }
}
