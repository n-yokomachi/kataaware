using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ジャックを抜く一連の間。手首へ視線を落として刺さっているところを見せ、
    /// 左手を伸ばし、掴み、抜き、ケーブルが張るところまで前へ出して見せ、
    /// 置き場（右の肘掛けの内の縁）まで運んで置き、最後に手を戻す。
    /// 骨そのものは触らず、どの段でどれだけ寄せるかだけを持つ
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
        /// <summary>置き場まで運ぶ</summary>
        public const float PlaceSeconds = 0.80f;
        /// <summary>手を戻す</summary>
        public const float ReturnSeconds = 0.75f;

        public static float ReachAt { get { return LookSeconds + StareSeconds; } }
        public static float GripAt { get { return ReachAt + ReachSeconds; } }
        public static float PullAt { get { return GripAt + GripSeconds; } }
        public static float ShowAt { get { return PullAt + PullSeconds; } }
        public static float PlaceAt { get { return ShowAt + ShowSeconds + CableSeconds; } }
        public static float LetGoAt { get { return PlaceAt + PlaceSeconds; } }
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

        /// <summary>
        /// 右の手首を目の前へ出す強さ。視線と一緒に上げ、ジャックを置き場に置くまで上げたままにし、手を戻す間に肘掛けへ下ろす。
        /// 先に下ろすと、右の前腕が肘掛けを塞ぎ、置き場（右の肘掛けの後ろ寄り）へ運ぶ左手と重なった
        /// </summary>
        public static float RightHand(float t)
        {
            return Aim(t);
        }

        /// <summary>右手首へ伸ばす姿勢の強さ</summary>
        public static float Reach(float t)
        {
            return Swell(t, ReachAt, ReachSeconds);
        }

        /// <summary>抜く強さ</summary>
        public static float Lift(float t)
        {
            return Swell(t, PullAt, PullSeconds);
        }

        /// <summary>抜いたジャックを前へ出す強さ</summary>
        public static float Show(float t)
        {
            return Swell(t, ShowAt, ShowSeconds);
        }

        /// <summary>置き場へ運ぶ強さ</summary>
        public static float Place(float t)
        {
            return Swell(t, PlaceAt, PlaceSeconds);
        }

        /// <summary>
        /// 置いて離した手を、ジャックの尻の側へ抜く強さ。手を戻す間の初めの 2 割 5 分で抜ききる。
        /// 抜ききるまでは手の向きを変えない（開いた指が置いたジャックを横切らない）
        /// </summary>
        public static float Release(float t)
        {
            if (t <= LetGoAt) return 0f;
            return Mathf.SmoothStep(0f, 1f, (t - LetGoAt) / (ReturnSeconds * 0.25f));
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

        /// <summary>置き場に置いた後</summary>
        public static bool LetGo(float t)
        {
            return t >= LetGoAt;
        }

        /// <summary>
        /// 視線がまだジャックを追ってよいか。前へ出して見せている間までは追い、置き場へ運び始めたら追わない。
        /// 置き場（右の肘掛け）まで追うと、視線が右の真下へ落ちて、首より上を映さない体の襟ぐりの中が見える。
        /// 追うのをやめた後は最後に見ていた先を保ち、手を離したら元の向きへ帰る
        /// </summary>
        public static bool Follows(float t)
        {
            return t < PlaceAt;
        }

        public static bool Done(float t)
        {
            return t >= Total;
        }
    }
}
