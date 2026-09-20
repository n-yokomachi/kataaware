using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 消えている画面が灯るまでの明るさ。
    ///
    /// **一息に明るくしない。** 一息に上げると、画面が点いたのではなく
    /// マテリアルの色が変わっただけに見える。古い物理モニターらしく二度瞬かせてから上げる
    /// </summary>
    public struct ScreenBoot
    {
        /// <summary>瞬いている間</summary>
        public const float FlickerSeconds = 0.42f;
        /// <summary>瞬きの後、明るさを上げきるまで</summary>
        public const float RiseSeconds = 0.85f;

        public static float Total { get { return FlickerSeconds + RiseSeconds; } }

        /// <summary>瞬き 1 回ぶんの長さ</summary>
        const float Blink = FlickerSeconds * 0.5f;

        /// <summary>
        /// t 秒での明るさ。0 が消灯、1 が点きっぱなし。
        /// 瞬きの間は前半だけ点く矩形を 2 つ並べ、そのあとは滑らかに上げる
        /// </summary>
        public static float Level(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= Total) return 1f;
            if (t < FlickerSeconds)
            {
                // 前半で点き、後半で落ちる。これを 2 回
                var into = t - Blink * Mathf.Floor(t / Blink);
                return into < Blink * 0.45f ? 0.8f : 0f;
            }
            return Mathf.SmoothStep(0f, 1f, (t - FlickerSeconds) / RiseSeconds);
        }

        /// <summary>もう点いているか</summary>
        public static bool Lit(float t) { return t >= Total; }
    }
}
