using System.Collections.Generic;
using System.Text;

namespace HalfAware
{
    /// <summary>
    /// 並びになっている文を表に組む。空白で区切られた塊を列と見なし、列ごとに頭を揃える。
    /// 帯に収まらなければ列を減らし、余った分は最後の列へ元のまま残す。
    /// 文面そのものには手を入れず、出すときだけ整える。
    ///
    /// **列の区切りは、半角の空白 2 つ以上か全角の空白**（台詞の原稿の決まり、docs/scenario/README.md、オーナー、2026-09-28）。
    /// 半角の空白 1 つは同じ列の中（「30 pcs」「2156/03/02 - 2156/03/03」「｜Name-call time&lt;…&gt;」が一つの列）。
    /// ルビと傍点は列の中で一まとまりに扱い、列の幅は親字とルビの広い方で取る（<see cref="Ruby.Width"/>）。
    /// 数で始まる塊だけの列（年齢・長さ・枚数）は、尻を揃える。原稿で空白を足して尻を揃えてある所。
    ///
    /// 幅は半角いくつぶんで数える（半角 1 字 = 0.5 em）。英字は実際にはそれより広い（「Female」で 3.15 em ほど）ので、
    /// 出す側が字の実寸を測れる時は measure で渡す（<see cref="ListView"/>）。列の幅は、測った幅と数えた幅（ルビを含む）の広い方
    /// </summary>
    public static class ListFormat
    {
        /// <summary>列と列のあいだ。半角いくつぶん</summary>
        public const int Gap = 2;

        /// <summary>これより多くは列にしない</summary>
        public const int MaxColumns = 8;

        /// <summary>全角 1 文字ぶんの幅。半角 2 つで 1em</summary>
        const float EmPerUnit = 0.5f;

        /// <summary>帯の幅のうち、表に使ってよい割合。数え方の誤差ぶんを残す</summary>
        const float Margin = 0.92f;

        /// <summary>
        /// リストのページの印。ページの頭に置く（幅の無い空白 U+200B。画面には出ず、<see cref="Ruby.Normalize"/> が落とす）。
        /// 台詞の原稿で **リスト** と書いたページは、並びの形によらず画面の真ん中の枠に出す。1 列だけの行
        /// （走査条件の「条件: 名前を呼ばれた時刻」。半角の空白 1 つは列の中）でも枠に出すよう、写す道具（<see cref="Manuscript"/>）がこの印を付ける
        /// </summary>
        public const char Mark = '\u200B';

        /// <summary>
        /// 看板のページの印。ページの頭に置く（語をつなぐ幅の無い字 U+2060。画面には出ず、<see cref="Ruby.Normalize"/> が落とす）。
        /// 台詞の原稿で **看板** と書いたページ（英語に日本語訳のルビ、オーナー、2026-09-29）は、リストと同じ画面の真ん中の枠に出し、
        /// 列に組まずに行ごとに真ん中へ寄せる（<see cref="ComposeSign"/>）。写す道具（<see cref="Manuscript"/>）がこの印を付ける
        /// </summary>
        public const char SignMark = '\u2060';

        /// <summary>看板のページか。頭に <see cref="SignMark"/> がある</summary>
        public static bool IsSign(string text)
        {
            return !string.IsNullOrEmpty(text) && text[0] == SignMark;
        }

        /// <summary>
        /// 表に組むか（画面の真ん中の枠に出すか）。頭に <see cref="Mark"/> か <see cref="SignMark"/> があるか、
        /// 2 行以上あって、どれかの行が 2 つ以上の塊に分かれていること。
        /// 1 行だけの文や、区切りの無い文はそのまま出す
        /// </summary>
        public static bool IsList(string text)
        {
            if (!string.IsNullOrEmpty(text) && (text[0] == Mark || text[0] == SignMark)) return true;
            var lines = Lines(text);
            if (lines.Count < 2) return false;
            foreach (var line in lines) if (Split(line, MaxColumns).Count >= 2) return true;
            return false;
        }

        /// <summary>
        /// 列の頭を揃えた形にする。roomEm は帯の横幅で、そこに収まる列数まで畳み、
        /// 余れば表ごと真ん中へ寄せる。TextMeshPro の pos で置くので、出す側は左寄せにしておく
        /// </summary>
        public static string Compose(string text, float roomEm)
        {
            return Compose(text, roomEm, true);
        }

        /// <summary>
        /// 列の頭を揃えた形にする。roomEm に収まる列数まで畳むのは同じ。
        /// center が false なら、余っても真ん中へ寄せず左に置く（表の幅に合わせて枠を伸ばす、リストの枠 <see cref="ListView"/>）。
        /// 返すのは、原稿の書き方を直した形（<see cref="Ruby.Normalize"/>）。ルビの書式には出す側が <see cref="Ruby.Expand(string)"/> で直す
        /// </summary>
        public static string Compose(string text, float roomEm, bool center)
        {
            return Compose(text, roomEm, center, null);
        }

        /// <summary>
        /// measure は塊の親字だけの幅（em）を字の実寸で測る物（無ければ半角いくつで数える）。
        /// 列の幅は、測った幅と、ルビを含めて数えた幅（<see cref="Ruby.Width"/>）の広い方
        /// </summary>
        public static string Compose(string text, float roomEm, bool center, System.Func<string, float> measure)
        {
            return Compose(text, roomEm, center, measure, Ruby.Scale);
        }

        /// <summary>rubyScale はルビを振る大きさ（リストの枠は字幕より大きい、<see cref="ListLayout.RubyScale"/>）。列の幅に数えるルビの幅がこれで決まる</summary>
        public static string Compose(string text, float roomEm, bool center, System.Func<string, float> measure, float rubyScale)
        {
            text = Ruby.Normalize(text);
            // 幅は字の実寸ではなく半角いくつで数えているので、少し余裕を見る
            var safe = roomEm * Margin;
            var cols = Most(text);
            var widths = Widths(text, cols, measure, rubyScale);
            // 収まるところまで列を減らす。減らした分は最後の列に元のまま残る
            while (cols > 2 && Total(widths) > safe)
            {
                cols--;
                widths = Widths(text, cols, measure, rubyScale);
            }
            var table = Total(widths);
            var indent = center && safe > 0f && table < safe ? (safe - table) * 0.5f : 0f;
            var right = Numbers(text, cols);

            var at = new float[widths.Count];
            for (var i = 1; i < widths.Count; i++) at[i] = at[i - 1] + widths[i - 1] + Gap * EmPerUnit;

            var sb = new StringBuilder();
            var lines = Lines(text);
            for (var r = 0; r < lines.Count; r++)
            {
                if (r > 0) sb.Append('\n');
                var cells = Split(lines[r], cols);
                for (var i = 0; i < cells.Count; i++)
                {
                    var em = at[i];
                    if (i < right.Length && right[i]) em += widths[i] - Measure(cells[i], measure, rubyScale);
                    var x = em + indent;
                    if (x > 0.004f) sb.Append("<pos=").Append(x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append("em>");
                    sb.Append(cells[i]);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 看板のページ（<see cref="SignMark"/>）を、行ごとに真ん中へ寄せた形にする。列には組まない（英語の看板の行は、半角の空白 1 つで語が続く）。
        /// 行の幅は、ルビを親字の真ん中に揃えて（行の頭でも、<see cref="Ruby.Expand(string,float,float,bool)"/> の centred）ルビまで含めて測り（<see cref="Ruby.Span"/>）、
        /// いちばん広い行の幅 widthEm の中へ &lt;pos&gt; で置く。ルビが親字より広い行は、ルビの頭が枠の中に収まるだけ親字を内へ寄せる。
        /// 返すのは原稿の書き方を直した形。出す側は <see cref="Ruby.Expand(string,float,float,bool)"/> を centred で掛ける
        /// </summary>
        public static string ComposeSign(string text, float rubyScale, out float widthEm)
        {
            var lines = Lines(text);
            var spans = new float[lines.Count];
            var leads = new float[lines.Count];
            widthEm = 0f;
            for (var i = 0; i < lines.Count; i++)
            {
                float lead, tail;
                var advance = Ruby.Span(lines[i], rubyScale, true, out lead, out tail);
                leads[i] = lead;
                spans[i] = lead + advance + tail;
                if (spans[i] > widthEm) widthEm = spans[i];
            }
            var sb = new StringBuilder();
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                var x = (widthEm - spans[i]) * 0.5f + leads[i];
                if (x > 0.004f) sb.Append("<pos=").Append(x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append("em>");
                sb.Append(lines[i]);
            }
            return sb.ToString();
        }

        /// <summary>columns 列で組んだときの横幅。em</summary>
        public static float WidthEm(string text, int columns)
        {
            return Total(Widths(Ruby.Normalize(text), columns, null, Ruby.Scale));
        }

        /// <summary>塊の幅（em）。数えた幅（ルビを含む）と、measure があれば測った親字の幅の広い方</summary>
        static float Measure(string cell, System.Func<string, float> measure, float rubyScale)
        {
            var counted = Ruby.Width(cell, rubyScale) * EmPerUnit;
            if (measure == null) return counted;
            return System.Math.Max(counted, measure(Ruby.Plain(cell)));
        }

        /// <summary>いちばん多く分かれている行の塊の数</summary>
        public static int Most(string text)
        {
            var most = 1;
            foreach (var line in Lines(text))
            {
                var n = Split(line, MaxColumns).Count;
                if (n > most) most = n;
            }
            return most;
        }

        /// <summary>半角いくつぶんの幅か。全角は 2 つぶん。書式もルビも字として数える（画面に出る幅は <see cref="Ruby.Width"/>）</summary>
        public static int Units(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var n = 0;
            for (var i = 0; i < text.Length; i++) n += Wide(text[i]) ? 2 : 1;
            return n;
        }

        /// <summary>
        /// 1 行を最大 max 個の塊に切る。max 個目には、そこから先が元の空白のまま残る。
        /// 「対象　防壁なし　距離 ランダム」を 2 つで切れば「対象」と残り全部になる。
        /// 区切りは全角の空白か、半角の空白 2 つ以上。半角の空白 1 つは塊の中。ルビ・傍点・書式の途中では切らない
        /// </summary>
        public static List<string> Split(string line, int max)
        {
            var list = new List<string>();
            line = Ruby.Normalize(line);
            if (string.IsNullOrEmpty(line)) return list;
            var i = 0;
            while (i < line.Length)
            {
                while (i < line.Length && IsSpace(line[i])) i++;
                if (i >= line.Length) break;
                if (list.Count == max - 1)
                {
                    list.Add(line.Substring(i));
                    return list;
                }
                var start = i;
                while (i < line.Length && !Breaks(line, i)) i = Step(line, i);
                list.Add(line.Substring(start, i - start));
            }
            return list;
        }

        /// <summary>at で塊が切れるか。全角の空白、半角の空白 2 つ（か半角の空白の後に全角の空白）、行の尻の空白</summary>
        static bool Breaks(string line, int at)
        {
            var c = line[at];
            if (c == '　') return true;
            if (c != ' ') return false;
            return at + 1 >= line.Length || IsSpace(line[at + 1]);
        }

        /// <summary>at の次に見る所。ルビ・傍点・書式は一まとまりで飛ばす</summary>
        static int Step(string line, int at)
        {
            int baseFrom, baseTo, rubyFrom, rubyTo;
            if (Ruby.Group(line, at, out baseFrom, out baseTo, out rubyFrom, out rubyTo)) return rubyTo + 1;
            var tag = Ruby.TagEnd(line, at);
            return tag > at ? tag + 1 : at + 1;
        }

        static List<float> Widths(string text, int columns, System.Func<string, float> measure, float rubyScale)
        {
            var widths = new List<float>();
            foreach (var line in Lines(text))
            {
                var cells = Split(line, columns);
                for (var i = 0; i < cells.Count; i++)
                {
                    var w = Measure(cells[i], measure, rubyScale);
                    if (i < widths.Count) { if (w > widths[i]) widths[i] = w; }
                    else widths.Add(w);
                }
            }
            return widths;
        }

        /// <summary>
        /// 列ごとに、尻を揃えるか。その列のどの塊も数で始まる時だけ真。
        /// 原稿では「41」と「 8」、「32m40s」と「 3m32s」、「30 pcs」と「 5 pcs」のように、空白を足して尻を揃えてある
        /// </summary>
        static bool[] Numbers(string text, int columns)
        {
            var right = new bool[columns];
            var seen = new bool[columns];
            for (var i = 0; i < columns; i++) right[i] = true;
            foreach (var line in Lines(text))
            {
                var cells = Split(line, columns);
                for (var i = 0; i < cells.Count && i < columns; i++)
                {
                    seen[i] = true;
                    var c = cells[i][0];
                    if (c < '0' || c > '9') right[i] = false;
                }
            }
            for (var i = 0; i < columns; i++) right[i] &= seen[i];
            return right;
        }

        static float Total(List<float> widths)
        {
            var total = 0f;
            for (var i = 0; i < widths.Count; i++)
            {
                if (i > 0) total += Gap * EmPerUnit;
                total += widths[i];
            }
            return total;
        }

        static bool IsSpace(char c)
        {
            return c == ' ' || c == '　';
        }

        /// <summary>行に分ける。原稿の書き方（&lt;br/&gt; とルビ）は先に直す</summary>
        static List<string> Lines(string text)
        {
            var list = new List<string>();
            text = Ruby.Normalize(text);
            if (string.IsNullOrEmpty(text)) return list;
            var start = 0;
            for (var i = 0; i <= text.Length; i++)
            {
                if (i != text.Length && text[i] != '\n') continue;
                list.Add(text.Substring(start, i - start));
                start = i + 1;
            }
            return list;
        }

        /// <summary>全角として数える字か</summary>
        static bool Wide(char c)
        {
            if (c == '　') return true;
            // 和文の組版で全角に置かれる約物。ダッシュ・三点リーダ・引用符
            if (c == '―' || c == '—' || c == '…'
                || c == '‘' || c == '’' || c == '“' || c == '”') return true;
            if (c < 'ᄀ') return false;
            return c <= 'ᅟ'                          // ハングル字母
                || (c >= '⺀' && c <= '꓏')       // 漢字・かな・記号
                || (c >= '가' && c <= '힣')       // ハングル
                || (c >= '豈' && c <= '﫿')       // 互換漢字
                || (c >= '︰' && c <= '﹯')       // 縦組み用の約物
                || (c >= '＀' && c <= '｠')       // 全角の英数と記号
                || (c >= '￠' && c <= '￦');
        }
    }
}
