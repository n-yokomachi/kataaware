using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// リストの枠（<see cref="ListView"/>）の並べ方。見せる物は持たず、寸法だけを出す。
    ///
    /// 上から、表（<see cref="ListFormat"/> が列の頭を揃えた行）と、右下の送りの印を置き、まわりを板で囲む。
    /// 板は二択の札（<see cref="ChoiceLayout"/>）と同じ見た目で、同じ所（画面の真ん中から少し上）に浮かべる。
    /// 板の幅は表の幅に合わせて伸びる。表が板の幅の下限より狭い時は、表ごと板の真ん中へ寄せる
    /// （行ごとに寄せると列の頭がぶれるので、表の塊のまま寄せる。字幕の窓で出していた頃と同じ）。
    ///
    /// **寸法は粗い画面の 1 画素 = <see cref="Dot"/> で決め、どれも Dot の整数倍にする**（二択と同じ決まり）。
    /// 表の字は札の字と同じ 12 Dot、送りの印は字幕の送りの印と同じ 11 Dot。
    /// 表に使える幅は、字幕の窓の行の幅（キャンバス 1280 の 0.72、518 Dot）と同じにする。列の畳み方が字幕の窓で出していた頃と変わらない。
    ///
    /// 矩形はどれも板の中の座標（板の真ん中が原点、上が +y）
    /// </summary>
    public static class ListLayout
    {
        public const float Dot = ChoiceLayout.Dot;

        /// <summary>表の字。二択の札の字と同じ</summary>
        public const float RowFont = ChoiceLayout.CardFont;
        /// <summary>送りの印の字。字幕の送りの印と同じ 11 Dot</summary>
        public const float HintFont = 11f * Dot;

        /// <summary>板の上の縁から表まで</summary>
        const int PadTop = 13;
        /// <summary>表から送りの印まで</summary>
        const int HintGap = 4;
        /// <summary>送りの印の行の高さ</summary>
        public const int HintRow = 14;
        /// <summary>送りの印から板の下の縁まで</summary>
        const int PadBottom = 8;
        /// <summary>板の左右の余白。二択と同じ</summary>
        const int Side = 28;
        /// <summary>送りの印を右の縁から離す幅。表より少し内へ寄せる</summary>
        const int HintSide = 16;
        /// <summary>板の幅の下限。二択と同じ</summary>
        const int Least = 256;
        /// <summary>表に使える幅。字幕の窓の行の幅（1280 × 0.72 のキャンバスの単位）と同じ</summary>
        public const int RoomDots = 518;
        /// <summary>板の幅の上限。表に使える幅に左右の余白を足した所（画面の幅の 8 割）</summary>
        public const int Most = RoomDots + Side * 2;

        /// <summary>板の真ん中を、画面の真ん中からどれだけ上に置くか。二択と同じ</summary>
        public const float Lift = ChoiceLayout.Lift;

        /// <summary>表に使える幅。em（表の字の大きさで割った数）。<see cref="ListFormat.Compose(string,float,bool)"/> に渡す</summary>
        public static float RoomEm { get { return RoomDots * Dot / RowFont; } }

        /// <summary>並べた結果。矩形は板の中の座標</summary>
        public struct Frame
        {
            public Vector2 size;
            public Rect table;
            public Rect hint;
        }

        /// <summary>
        /// 表の字の幅と高さ、送りの印の幅（どれもキャンバスの単位）から並べる。
        /// 表は板の上に（狭ければ表ごと真ん中へ寄せて）、送りの印は右下に置く
        /// </summary>
        public static Frame Lay(float tableWidth, float tableHeight, float hintWidth)
        {
            var tw = Mathf.Min(Dots(tableWidth), RoomDots);
            var th = Mathf.Max(1, Dots(tableHeight));
            var hw = Mathf.Max(1, Dots(hintWidth));
            var w = Mathf.Max(Least, Mathf.Max(tw, hw + HintSide - Side) + Side * 2);
            w = Mathf.Min(w, Most);
            if (w % 2 == 1) w++;
            var h = PadTop + th + HintGap + HintRow + PadBottom;
            // 高さは奇数にする。405 の画面の真ん中（202.5）に置いても縁が画素の境目に乗る（二択と同じ）
            if (h % 2 == 0) h++;

            var f = new Frame();
            f.size = new Vector2(w, h) * Dot;
            var left = -w / 2;
            var top = h / 2f;
            // 板が表より広い（幅の下限で広げた）時は、余りを左右に分けて表ごと真ん中へ
            var spare = Mathf.Max(0, w - Side * 2 - tw) / 2;
            f.table = Box(left + Side + spare, top - PadTop - th, tw, th);
            f.hint = Box(w / 2 - HintSide - hw, -top + PadBottom, hw, HintRow);
            return f;
        }

        /// <summary>キャンバスの単位の長さを、Dot の整数に切り上げる</summary>
        static int Dots(float units)
        {
            return units <= 0f ? 0 : Mathf.CeilToInt(units / Dot - 1e-3f);
        }

        static Rect Box(float x, float y, int w, int h)
        {
            return new Rect(x * Dot, y * Dot, w * Dot, h * Dot);
        }
    }
}
