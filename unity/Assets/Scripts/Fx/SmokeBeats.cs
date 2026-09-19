using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 一本吸い終わるまでの間合い。火を点け、吸い、止め、吐く、を繰り返す。
    /// 音も煙もこの時刻表に合わせるので、ずれない
    /// </summary>
    public static class SmokeBeats
    {
        /// <summary>くわえてから蓋を開けるまで</summary>
        public const float ClickAt = 0.30f;
        /// <summary>「カチン」から「シュボッ」まで</summary>
        public const float FlameAfterClick = 0.34f;
        /// <summary>火が点いてから最初の一服まで</summary>
        public const float FirstDragAfterFlame = 0.85f;

        /// <summary>吸っている長さ。素材の長さに合わせてある</summary>
        public const float DragSeconds = 1.70f;
        /// <summary>肺に留めている長さ</summary>
        public const float HoldSeconds = 0.55f;
        /// <summary>吐いている長さ。素材の長さに合わせてある</summary>
        public const float BlowSeconds = 2.25f;
        /// <summary>吐き終わってから次に吸うまで</summary>
        public const float RestSeconds = 2.10f;

        public static float FlameAt { get { return ClickAt + FlameAfterClick; } }
        public static float FirstDragAt { get { return FlameAt + FirstDragAfterFlame; } }
        public static float Cycle { get { return DragSeconds + HoldSeconds + BlowSeconds + RestSeconds; } }

        /// <summary>i 服目に吸い始める時刻。0 から数える</summary>
        public static float DragAt(int i)
        {
            return FirstDragAt + Cycle * Mathf.Max(0, i);
        }

        /// <summary>i 服目を吐き始める時刻</summary>
        public static float BlowAt(int i)
        {
            return DragAt(i) + DragSeconds + HoldSeconds;
        }

        /// <summary>seconds 秒のあいだに、吐き終わりまで収まる服の数</summary>
        public static int Drags(float seconds)
        {
            var n = 0;
            while (BlowAt(n) + BlowSeconds <= seconds) n++;
            return n;
        }
    }
}
