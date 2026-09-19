using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 字幕のウインドウの大きさ。1 ページに何行入っているかで高さを決め、
    /// 入りきらないぶんは字を小さくして収める。既定は 2 行
    /// </summary>
    public static class SubtitleBox
    {
        /// <summary>何も入っていなくてもこの行数ぶんの高さを取る</summary>
        public const int BaseRows = 2;

        /// <summary>これを超えたら字を小さくする</summary>
        public const int ShrinkOver = 4;

        /// <summary>小さくする限度。これ以上は縮めない</summary>
        public const float MinScale = 0.70f;

        /// <summary>この行数を超えたら、それ以上はウインドウを伸ばさない</summary>
        public const int MaxRows = 8;

        /// <summary>改行で区切った行数。空なら 0</summary>
        public static int LineCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var n = 1;
            for (var i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
            return n;
        }

        /// <summary>ウインドウが取る行数。既定を下回らず、上限も超えない</summary>
        public static int Rows(string text)
        {
            return Mathf.Clamp(LineCount(text), BaseRows, MaxRows);
        }

        /// <summary>字の大きさの倍率。行数が増えるほど小さくする</summary>
        public static float FontScale(string text)
        {
            var lines = LineCount(text);
            if (lines <= ShrinkOver) return 1f;
            return Mathf.Max(MinScale, (float)ShrinkOver / lines);
        }

        /// <summary>
        /// 1 行に入る幅が分からないときの目安。半角いくつぶん。
        /// ふだんはウインドウの実寸から求めた値を渡す
        /// </summary>
        public const int WrapOver = 80;

        /// <summary>割り口として良い字。この後ろで切る</summary>
        const string BreakAfter = "、。！？…」』）";

        /// <summary>行の頭に置きたくない字</summary>
        const string NeverStarts = "、。！？…」』）";

        /// <summary>行の尻に置きたくない字。開き括弧だけ残すと次の行と切れて読めない</summary>
        const string NeverEnds = "「『（";

        /// <summary>この字の後ろは切りやすい。助詞で切ると文が途切れて見えない</summary>
        const string BreakAfterKana = "をにはがのへとでもや";

        /// <summary>
        /// 片仮名の語と欧字の語。この並びの途中では切らない。
        /// 「ファイアウォ／ール」のような割り方は、読めはしても目に付く
        /// </summary>
        static bool Joined(char a, char b)
        {
            return Runs(a) && Runs(b);
        }

        static bool Runs(char c)
        {
            if (c >= '゠' && c <= 'ヿ') return true;    // 片仮名・長音符・中黒
            if (c >= '0' && c <= '9') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= 'a' && c <= 'z') return true;
            if (c >= '０' && c <= '９') return true;    // 全角の数字
            if (c >= 'Ａ' && c <= 'Ｚ') return true;
            if (c >= 'ａ' && c <= 'ｚ') return true;
            return false;
        }

        /// <summary>漢字どうしの境目。熟語を割りやすいので、切るなら他を先に探す</summary>
        static bool Kanji(char c)
        {
            return c >= '一' && c <= '鿿';
        }

        /// <summary>
        /// 平仮名どうしの境目。「ほと／んど」のような割り方になりやすい。
        /// 助詞の後ろは別に見るので、ここでは残りだけを避ける
        /// </summary>
        static bool Hira(char c)
        {
            return c >= 'ぁ' && c <= 'ゟ';
        }

        /// <summary>
        /// この行の切り口を選ぶ。start から fits に収まる範囲で、
        /// 幅が want に近いところ。strict なら語の途中と熟語の境目を避ける。
        /// 見つからなければ -1
        /// </summary>
        static int Pick(string text, int start, int carried, int fits, int want, bool strict)
        {
            var best = -1;
            var bestGap = int.MaxValue;
            var at = carried;
            for (var i = start; i < text.Length - 1; i++)
            {
                at += ListFormat.Units(text.Substring(i, 1));
                if (at - carried > fits) break;                       // この行にはもう入らない
                var a = text[i];
                var b = text[i + 1];
                if (NeverStarts.IndexOf(b) >= 0) continue;            // 行頭に来る字を避ける
                if (NeverEnds.IndexOf(a) >= 0) continue;              // 行末に残す字を避ける
                if (strict && Joined(a, b)) continue;                 // 語の途中で切らない
                var gap = Mathf.Abs(at - want);
                // 句読点の直後は優先する。多少 狙いから外れても切りたい
                var particle = BreakAfterKana.IndexOf(a) >= 0;
                if (BreakAfter.IndexOf(a) >= 0) gap -= 8;
                else if (particle) gap -= 3;
                if (strict && Kanji(a) && Kanji(b)) gap += 5;
                if (strict && !particle && Hira(a) && Hira(b)) gap += 5;
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = i + 1;
            }
            return best;
        }

        /// <summary>
        /// 長い 1 行を割る。すでに改行があるものと短いものはそのまま
        /// </summary>
        public static string Wrap(string text)
        {
            return Wrap(text, WrapOver);
        }

        /// <summary>
        /// 1 行に fits ぶんしか入らないとして、収まる行数まで割る。
        ///
        /// 割る数は先に決めてしまい、各行が同じくらいの長さになる position を狙う。
        /// 貪欲に詰めると最後の行だけ極端に短くなって、字幕としてみっともない。
        /// 切り口は句読点の後ろを優先し、行頭に来ると困る字と行末に残すと困る字は避ける
        /// </summary>
        public static string Wrap(string text, int fits)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (LineCount(text) > 1) return text;
            if (fits <= 0) return text;
            var units = ListFormat.Units(text);
            if (units <= fits) return text;

            var rows = Mathf.CeilToInt((float)units / fits);
            var target = (float)units / rows;

            var made = new System.Text.StringBuilder();
            var start = 0;      // 今の行の頭
            var carried = 0;    // 切り終えたぶんの幅
            for (var row = 1; row < rows; row++)
            {
                var want = Mathf.RoundToInt(target * row);
                // まず語を割らずに探し、どうしても無ければ禁則だけ守って切る
                var best = Pick(text, start, carried, fits, want, true);
                if (best <= start) best = Pick(text, start, carried, fits, want, false);
                if (best <= start || best >= text.Length) break;
                made.Append(text, start, best - start).Append('\n');
                carried += ListFormat.Units(text.Substring(start, best - start));
                start = best;
            }
            made.Append(text, start, text.Length - start);
            return made.ToString();
        }
    }
}
