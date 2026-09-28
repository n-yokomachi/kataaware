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

        // ---- 行の間（オーナー、2026-09-29「字幕でルビがあるときだけ行間が空いたように見えないように、あらかじめルビを含めた行間を常に開けるようにして」） ----
        //
        // 行の間は、ルビの有る無しにかかわらず、いつもルビが入る分を含めた一定の間にする。
        // TMP は、行の頭より高い字（持ち上げたルビ）が行の途中に来ると、その行を下げて上の行とかぶらないようにする。
        // ルビのある行だけが下がり、1 行目にルビがあれば文ごと下がって、ルビのあるページだけ行が広がって見えた。
        // 行の高さを <line-height> で決め打ちにして下げさせず、1 行目の上には見えない字をルビの高さに置いて、いつもルビの分を空ける（Frame）

        /// <summary>
        /// ルビと上の行の字のあいだに、さらに空ける間（em）。ルビのための行間（<see cref="Ruby.ExtraLineSpacing"/>）だけでは、
        /// 粗い画面でルビの上の端が上の行の字の下の端と 1 画素しか離れず、ルビが上の行に付いて見えた（2026-09-29）。0.1 em（台詞の字で 1.3 画素）足して、
        /// ルビの上の隙間を下の隙間（親字との間）より広くする
        /// </summary>
        public const float RubyClear = 0.1f;

        /// <summary>行の送り（em）。素の行送り（<see cref="Ruby.Advance"/>）に、ルビのための行間（<see cref="Ruby.ExtraLineSpacing"/>）と <see cref="RubyClear"/> を足した一定の間</summary>
        public const float LineEm = Ruby.Advance + Ruby.ExtraLineSpacing * 0.01f + RubyClear;

        /// <summary>字の上の端の線（ascent）。Noto Sans JP の字の寸法で 1.16 em（TMP の faceInfo、74.24 / 64）</summary>
        public const float AscentEm = 1.16f;

        /// <summary>字の下の端の線（descent）。Noto Sans JP で 0.288 em（18.432 / 64）</summary>
        public const float DescentEm = 0.288f;

        /// <summary>
        /// 字幕の枠の上の縁から 1 行目のベースラインまで（em）。持ち上げたルビの字の上の線（ルビの大きさの ascent ＋ 持ち上げる高さ）。
        /// ルビが無くてもここまで空ける（1 行目の上のルビが名前の行や地の上の縁にかからない）。TMP で測って 1.80 em
        /// </summary>
        public const float TopEm = Ruby.Lift + AscentEm * Ruby.Scale;

        /// <summary>rows 行ぶんの字の枠の高さ（em）。1 行目の上のルビの分 ＋ 行の送り × (rows − 1) ＋ 最後の行の下の端の線</summary>
        public static float BodyEm(int rows)
        {
            return TopEm + LineEm * Mathf.Max(0, rows - 1) + DescentEm;
        }

        /// <summary>1 行目の上にルビの高さを取る、見えない字。ルビと同じ大きさと高さに置き、進んだぶんを戻す</summary>
        public static readonly string Headroom =
            "<voffset=" + Num(Ruby.Lift) + "em><size=" + Num(Ruby.Scale * 100f) + "%><alpha=#00>あ<alpha=#FF></size></voffset>" +
            "<space=" + Num(-Ruby.Scale) + "em>";

        /// <summary>
        /// 書式に直した台詞 expanded（<see cref="Ruby.Expand(string)"/> の後）を、一定の行の間で組む形にする。
        /// 行の高さを決め打ちにし（<see cref="LineEm"/>）、1 行目の上にルビの高さを取る（<see cref="Headroom"/>）。空ならそのまま
        /// </summary>
        public static string Frame(string expanded)
        {
            if (string.IsNullOrEmpty(expanded)) return expanded;
            return "<line-height=" + Num(LineEm) + "em>" + Headroom + expanded;
        }

        static string Num(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

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
        /// 字ごとの幅（半角いくつぶん）と、その後ろで切ってはいけないかを拾う。
        ///
        /// ルビと傍点の指定（｜親字《るび》。原稿の書き方は先に Ruby.Normalize で直しておく）は、幅を親字だけで数える。
        /// 記号とルビは画面に出ないので 0。TextMeshPro の書式（&lt;size=…&gt; など）も 0。
        /// 指定の途中で切ると親字とルビが離れてしまうので、そこは繋いでおく
        /// </summary>
        static void Scan(string text, out int[] wide, out bool[] joined)
        {
            wide = new int[text.Length];
            joined = new bool[text.Length];
            var i = 0;
            while (i < text.Length)
            {
                var tag = Ruby.TagEnd(text, i);
                if (tag > i)
                {
                    // 書式の途中では切らない
                    for (var k = i; k < tag; k++) joined[k] = true;
                    i = tag + 1;
                    continue;
                }
                int baseFrom, baseTo, rubyFrom, rubyTo;
                if (!Ruby.Group(text, i, out baseFrom, out baseTo, out rubyFrom, out rubyTo))
                {
                    wide[i] = ListFormat.Units(text.Substring(i, 1));
                    i++;
                    continue;
                }
                for (var k = i; k <= rubyTo; k++)
                {
                    wide[k] = k >= baseFrom && k < baseTo ? ListFormat.Units(text.Substring(k, 1)) : 0;
                    joined[k] = k < rubyTo;
                }
                // 指定の直前でも切らない。行末に ｜ だけが残ると読めない
                if (i > 0) joined[i - 1] = true;
                i = rubyTo + 1;
            }
        }

        /// <summary>
        /// この行の切り口を選ぶ。start から fits に収まる範囲で、
        /// 幅が want に近いところ。strict なら語の途中と熟語の境目を避ける。
        /// 切った後の残り（全体 total から切り口までを引いた幅）が room を超える所は選ばない（残りの行に入らない）。
        /// 見つからなければ -1
        /// </summary>
        static int Pick(string text, int[] wide, bool[] joined,
            int start, int carried, int fits, int want, bool strict, int total, int room)
        {
            var best = -1;
            var bestGap = int.MaxValue;
            var at = carried;
            for (var i = start; i < text.Length - 1; i++)
            {
                at += wide[i];
                if (at - carried > fits) break;                       // この行にはもう入らない
                if (total - at > room) continue;                      // 残りが後の行に入らない
                if (joined[i]) continue;                              // ルビの指定の途中では切らない
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
        /// 長い行を割る。短いものはそのまま
        /// </summary>
        public static string Wrap(string text)
        {
            return Wrap(text, WrapOver);
        }

        /// <summary>
        /// 1 行に fits ぶんしか入らないとして、はみ出す行を収まる行数まで割る。
        ///
        /// **書いてある改行（原稿の &lt;br/&gt;）には従う。** 改行で区切った行ごとに見て、窓の幅を超える行だけを割る。
        /// 原稿の書き方（ルビ・傍点・&lt;br/&gt;）は先に前からの書き方に直す（<see cref="Ruby.Normalize"/>）。返すのも直した形。
        ///
        /// 割る数は先に決めてしまい、各行が同じくらいの長さになる position を狙う。
        /// 貪欲に詰めると最後の行だけ極端に短くなって、字幕としてみっともない。
        /// 切り口は句読点の後ろを優先し、行頭に来ると困る字と行末に残すと困る字は避ける
        /// </summary>
        public static string Wrap(string text, int fits)
        {
            text = Ruby.Normalize(text);
            if (string.IsNullOrEmpty(text)) return text;
            if (fits <= 0) return text;
            if (LineCount(text) == 1) return WrapLine(text, fits);
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++) lines[i] = WrapLine(lines[i], fits);
            return string.Join("\n", lines);
        }

        /// <summary>改行の無い 1 行を、fits に収まる行数まで割る</summary>
        static string WrapLine(string text, int fits)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int[] wide;
            bool[] joined;
            Scan(text, out wide, out joined);
            var units = 0;
            for (var i = 0; i < wide.Length; i++) units += wide[i];
            if (units <= fits) return text;

            // 行数はまず幅で割り切れる数。句読点を優先して切った結果、最後の行が入りきらない時だけ 1 行増やす
            var least = Mathf.CeilToInt((float)units / fits);
            string made = null;
            for (var rows = least; rows <= least + 2; rows++)
            {
                bool fitted;
                made = Cut(text, wide, joined, units, fits, rows, out fitted);
                if (fitted) break;
            }
            return made;
        }

        /// <summary>rows 行に割る。各行が同じくらいの長さになる所を狙う。最後の行まで fits に入ったら fitted</summary>
        static string Cut(string text, int[] wide, bool[] joined, int units, int fits, int rows, out bool fitted)
        {
            var target = (float)units / rows;
            var made = new System.Text.StringBuilder();
            var start = 0;      // 今の行の頭
            var carried = 0;    // 切り終えたぶんの幅
            for (var row = 1; row < rows; row++)
            {
                var want = Mathf.RoundToInt(target * row);
                var room = fits * (rows - row);
                // まず語を割らずに探し、どうしても無ければ禁則だけ守って切る。残りが後の行に入る所を先に探す
                var best = Pick(text, wide, joined, start, carried, fits, want, true, units, room);
                if (best <= start) best = Pick(text, wide, joined, start, carried, fits, want, false, units, room);
                if (best <= start) best = Pick(text, wide, joined, start, carried, fits, want, false, units, int.MaxValue);
                if (best <= start || best >= text.Length) break;
                made.Append(text, start, best - start).Append('\n');
                for (var k = start; k < best; k++) carried += wide[k];
                start = best;
            }
            made.Append(text, start, text.Length - start);
            fitted = units - carried <= fits;
            return made.ToString();
        }
    }
}
