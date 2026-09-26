using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 札（<see cref="ChoiceView"/>）の並べ方と当たり。見せる物は持たず、寸法だけを出す。
    ///
    /// 上から、問い・細い線・札の列の順に並べ、まわりを板で囲む。札は横に一列で、
    /// 板の幅は札の列（と問い）に合わせて伸びる。選ぶ物が三つ以上でも列が崩れない。
    ///
    /// **寸法は粗い画面（<see cref="UiLens"/>、既定の粗さ 0.75）の 1 画素 = <see cref="Dot"/> で決める。**
    /// コンソールと字幕と同じ決まりで、いちばん小さい字（問い）でも 11 Dot（粗い画面の中で縦 10 画素ほど）。
    /// 案（作業場 menu/choice_designs.html の 3）の CSS を手本にし、余白はコンソールと同じく案より広く取る。
    /// 寸法はどれも Dot の整数倍にして、細い線と字が粗い画素の境目に揃うようにする。
    ///
    /// 矩形はどれも板の中の座標（板の真ん中が原点、上が +y）
    /// </summary>
    public static class ChoiceLayout
    {
        /// <summary>粗い画面の 1 画素。1280×720 のキャンバスで、既定の粗さ（0.75 で 720×405 相当）のとき</summary>
        public const float Dot = 1280f / 720f;

        /// <summary>問いの字。いちばん小さい字</summary>
        public const float QuestionFont = 11f * Dot;
        /// <summary>札の字</summary>
        public const float CardFont = 12f * Dot;
        /// <summary>字の間。CSS の 0.12 em と 0.15 em</summary>
        public const float QuestionSpacing = 12f;
        public const float CardSpacing = 15f;

        /// <summary>板の上の縁から問いまで</summary>
        const int PadTop = 13;
        const int QuestionRow = 14;
        /// <summary>問いから線まで</summary>
        const int LineGap = 7;
        /// <summary>線の太さ</summary>
        public const int LineDots = 1;
        /// <summary>線から札まで</summary>
        const int CardsGap = 10;
        /// <summary>札の高さ。12 Dot の字の素の行送り（1.45 em）に上下の余白</summary>
        const int CardRow = 22;
        /// <summary>札から板の下の縁まで</summary>
        const int PadBottom = 14;
        /// <summary>板の左右の余白。札の列と問いの両脇にこれだけ空ける</summary>
        const int Side = 28;
        /// <summary>札の中の字の両脇</summary>
        const int CardPad = 14;
        /// <summary>札の幅の下限。一字の札でも括弧が字に寄りすぎない</summary>
        const int CardLeast = 44;
        /// <summary>札のあいだ。CSS の 70 px</summary>
        const int Gap = 48;
        /// <summary>札が多くて板が上限を超えるとき、ここまで詰める</summary>
        const int GapLeast = 14;
        /// <summary>板の幅の下限。案の 320 px</summary>
        const int Least = 256;
        /// <summary>板の幅の上限。画面の幅の 7 割ほど（キャンバス 1280 の 0.72）</summary>
        const int Most = 518;
        /// <summary>問いの下の線の長さ。板の幅に対する割合（案では 170 / 320）</summary>
        const float LineShare = 0.53f;
        /// <summary>線が問いの字より両脇へはみ出す長さ</summary>
        const int LineOver = 12;

        /// <summary>板の真ん中を、画面の真ん中からどれだけ上に置くか。案の top 37 % に合わせる</summary>
        public const float Lift = 18f * Dot;

        /// <summary>板の高さ。Dot の奇数倍にして、画面の真ん中（405 の半分）に置いても縁が画素の境目に乗るようにする</summary>
        public const int HeightDots = PadTop + QuestionRow + LineGap + LineDots + CardsGap + CardRow + PadBottom;

        /// <summary>並べた結果。矩形は板の中の座標</summary>
        public struct Frame
        {
            public Vector2 size;
            public Rect question;
            public Rect line;
            public Rect[] cards;
        }

        /// <summary>
        /// 問いの字の幅と、札の字の幅（どちらもキャンバスの単位）から並べる。
        /// 札の幅は字に合わせ、札の列を板の真ん中に置く
        /// </summary>
        public static Frame Lay(float questionWidth, IReadOnlyList<float> labelWidths)
        {
            var n = labelWidths != null ? labelWidths.Count : 0;
            var widths = new int[n];
            var sum = 0;
            for (var i = 0; i < n; i++)
            {
                widths[i] = Mathf.Max(CardLeast, Dots(labelWidths[i]) + CardPad * 2);
                sum += widths[i];
            }
            var question = Dots(questionWidth);
            // 札が多くて板が上限を超えるなら、隙間から詰める
            var gap = Gap;
            if (n > 1 && sum + gap * (n - 1) + Side * 2 > Most)
                gap = Mathf.Max(GapLeast, (Most - Side * 2 - sum) / (n - 1));
            var row = sum + (n > 1 ? gap * (n - 1) : 0);
            var w = Mathf.Max(Least, Mathf.Max(row, question) + Side * 2);
            if (w % 2 == 1) w++;
            var h = HeightDots;

            var f = new Frame();
            f.size = new Vector2(w, h) * Dot;
            var left = -w / 2;
            // 高さは奇数なので、真ん中は画素の半ばにある。405 の画面の真ん中（202.5）に置くと縁が境目に乗る
            var y = h * 0.5f - PadTop - QuestionRow;
            f.question = Box(left + Side, y, w - Side * 2, QuestionRow);
            var line = Mathf.Min(w - Side * 2, Mathf.Max(Mathf.RoundToInt(w * LineShare), question + LineOver * 2));
            if (line % 2 == 1) line++;
            y -= LineGap + LineDots;
            f.line = Box(-line / 2, y, line, LineDots);
            y -= CardsGap + CardRow;
            f.cards = new Rect[n];
            // 札の列は板の左の縁から整数の Dot だけ寄せる。列の幅が奇数でも札の縁が画素の境目に乗る
            var x = left + (w - row) / 2;
            for (var i = 0; i < n; i++)
            {
                f.cards[i] = Box(x, y, widths[i], CardRow);
                x += widths[i] + gap;
            }
            return f;
        }

        /// <summary>板の中の点 local に重なる札の番号。どの札にも重ならなければ -1</summary>
        public static int Hit(Frame frame, Vector2 local)
        {
            if (frame.cards == null) return -1;
            for (var i = 0; i < frame.cards.Length; i++)
                if (frame.cards[i].Contains(local)) return i;
            return -1;
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
