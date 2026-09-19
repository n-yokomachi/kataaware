using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 一本吸い終わるまでの間合い。火を点け、吸い、止め、吐く、を決めた回数だけ繰り返す。
    /// 音も煙もカードもこの時刻表に合わせるので、ずれない
    /// </summary>
    public static class SmokeBeats
    {
        /// <summary>くわえてから蓋を開けるまで</summary>
        public const float ClickAt = 0.30f;
        /// <summary>「カチン」から火が点くまで</summary>
        public const float FlameAfterClick = 0.34f;
        /// <summary>火が点いてから最初の一服まで。火の音が鳴りきる長さ</summary>
        public const float FirstDragAfterFlame = 1.55f;

        /// <summary>吸っている長さ。素材の長さに合わせてある</summary>
        public const float DragSeconds = 3.70f;
        /// <summary>肺に留めている長さ</summary>
        public const float HoldSeconds = 0.55f;
        /// <summary>吐いている長さ。素材の長さに合わせてある</summary>
        public const float BlowSeconds = 3.35f;
        /// <summary>吐き終わってから次に吸うまで</summary>
        public const float RestSeconds = 1.60f;

        /// <summary>何服で一本にするか</summary>
        public const int Drags = 3;

        /// <summary>吐き始めてから画面が暗くなるまで</summary>
        public const float CardAfterBlow = 0.50f;

        /// <summary>最後に吐き終わってから独白までの間</summary>
        public const float TailSeconds = 1.40f;

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

        /// <summary>i 服目のカードを出す時刻。吐き始めてすぐ暗くする</summary>
        public static float CardAt(int i)
        {
            return BlowAt(i) + CardAfterBlow;
        }

        /// <summary>drags 服ぶんを吸い終えて、独白に移るまでの長さ</summary>
        public static float Total(int drags)
        {
            if (drags <= 0) return FirstDragAt;
            return BlowAt(drags - 1) + BlowSeconds + TailSeconds;
        }
    }
}
