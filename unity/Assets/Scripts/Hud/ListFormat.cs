using System.Collections.Generic;
using System.Text;

namespace HalfAware
{
    /// <summary>
    /// 並びになっている文を表に組む。空白で区切られた塊を列と見なし、列ごとに頭を揃える。
    /// 帯に収まらなければ列を減らし、余った分は最後の列へ元のまま残す。
    /// 文面そのものには手を入れず、出すときだけ整える
    /// </summary>
    public static class ListFormat
    {
        /// <summary>列と列のあいだ。半角いくつぶん</summary>
        public const int Gap = 2;

        /// <summary>これより多くは列にしない</summary>
        public const int MaxColumns = 8;

        /// <summary>全角 1 文字ぶんの幅。半角 2 つで 1em</summary>
        const float EmPerUnit = 0.5f;

        /// <summary>
        /// 表に組むか。2 行以上あって、どれかの行が 2 つ以上の塊に分かれていること。
        /// 1 行だけの文や、区切りの無い文はそのまま出す
        /// </summary>
        public static bool IsList(string text)
        {
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
            var cols = Most(text);
            var widths = Widths(text, cols);
            // 収まるところまで列を減らす。減らした分は最後の列に元のまま残る
            while (cols > 2 && Total(widths) > roomEm)
            {
                cols--;
                widths = Widths(text, cols);
            }
            var table = Total(widths);
            var indent = roomEm > 0f && table < roomEm ? (roomEm - table) * 0.5f : 0f;

            var at = new int[widths.Count];
            for (var i = 1; i < widths.Count; i++) at[i] = at[i - 1] + widths[i - 1] + Gap;

            var sb = new StringBuilder();
            var lines = Lines(text);
            for (var r = 0; r < lines.Count; r++)
            {
                if (r > 0) sb.Append('\n');
                var cells = Split(lines[r], cols);
                for (var i = 0; i < cells.Count; i++)
                {
                    var x = at[i] * EmPerUnit + indent;
                    if (i > 0 || indent > 0f) sb.Append("<pos=").Append(x.ToString("0.##")).Append("em>");
                    sb.Append(cells[i]);
                }
            }
            return sb.ToString();
        }

        /// <summary>columns 列で組んだときの横幅。em</summary>
        public static float WidthEm(string text, int columns)
        {
            return Total(Widths(text, columns));
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

        /// <summary>半角いくつぶんの幅か。全角は 2 つぶん</summary>
        public static int Units(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var n = 0;
            for (var i = 0; i < text.Length; i++) n += Wide(text[i]) ? 2 : 1;
            return n;
        }

        /// <summary>
        /// 1 行を最大 max 個の塊に切る。max 個目には、そこから先が元の空白のまま残る。
        /// 「対象　防壁なし　距離 ランダム」を 2 つで切れば「対象」と残り全部になる
        /// </summary>
        public static List<string> Split(string line, int max)
        {
            var list = new List<string>();
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
                while (i < line.Length && !IsSpace(line[i])) i++;
                list.Add(line.Substring(start, i - start));
            }
            return list;
        }

        static List<int> Widths(string text, int columns)
        {
            var widths = new List<int>();
            foreach (var line in Lines(text))
            {
                var cells = Split(line, columns);
                for (var i = 0; i < cells.Count; i++)
                {
                    var w = Units(cells[i]);
                    if (i < widths.Count) { if (w > widths[i]) widths[i] = w; }
                    else widths.Add(w);
                }
            }
            return widths;
        }

        static float Total(List<int> widths)
        {
            var total = 0;
            for (var i = 0; i < widths.Count; i++)
            {
                if (i > 0) total += Gap;
                total += widths[i];
            }
            return total * EmPerUnit;
        }

        static bool IsSpace(char c)
        {
            return c == ' ' || c == '　';
        }

        static List<string> Lines(string text)
        {
            var list = new List<string>();
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
                || (c >= '豈' && c <= '﫿')       // 互換漢字
                || (c >= '︰' && c <= '﹯')       // 縦組み用の約物
                || (c >= '＀' && c <= '｠')       // 全角の英数と記号
                || (c >= '￠' && c <= '￦');
        }
    }
}
