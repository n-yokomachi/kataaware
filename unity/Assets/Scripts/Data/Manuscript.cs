using System;
using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 台詞の原稿（docs/scenario/*.md）を読む。書き方は docs/scenario/README.md（オーナー、2026-09-28）。
    ///
    /// 見出し（## と ###）ごとに一まとまり（<see cref="Section"/>）にし、その中の
    /// - `**対象の名前**: …` を印に出す字
    /// - `- 「…」` の 1 行を 1 ページ（外側の「」を外し、`&lt;br/&gt;` は改行にする。ルビと傍点の書き方はそのまま）
    /// - `**リスト**` の後のコードブロックを 1 ページ（行を改行で繋ぎ、頭にリストの印 <see cref="ListFormat.Mark"/> を付ける）
    /// - `**二択**: 問い（説明）` を二択の問い（説明の括弧は落とす）
    /// - `「はい」の後:` から後のページを、「はい」の後の文
    /// - `**暗転のカード**` の後の 1 ページを、カード
    /// として拾う。`※` の行（演出の注記）・`---`・空行は読み飛ばす。最初の ## より前（原稿の説明）も読み飛ばす。
    ///
    /// **拾えない行は黙って捨てずに、行の番号を添えて <see cref="ManuscriptException"/> で知らせる。**
    /// 見出しと場面の中の行き先（id）の対応は、写す側（<see cref="RoomManuscript"/>）が持つ
    /// </summary>
    public sealed class Manuscript
    {
        /// <summary>見出し一つ分</summary>
        public sealed class Section
        {
            /// <summary>見出しの字（## や ### を除いたもの）。例: 「ジャック（必須）」</summary>
            public string Heading;
            /// <summary>見出しの頭。最初の「（」より前。例: 「ジャック」</summary>
            public string Stem;
            /// <summary>見出しの行の番号（1 から）</summary>
            public int Line;
            /// <summary>対象の名前。無ければ null</summary>
            public string Label;
            /// <summary>読ませるページ。リストのページも順のまま入る</summary>
            public readonly List<string> Pages = new List<string>();
            /// <summary>二択の問い。無ければ null</summary>
            public string Question;
            /// <summary>「はい」の後に出すページ</summary>
            public readonly List<string> AfterYes = new List<string>();
            /// <summary>暗転のカード。無ければ null</summary>
            public string Card;

            /// <summary>中身が何も無いか（まとめの見出し「任意の対象」など）</summary>
            public bool Empty
            {
                get { return Label == null && Pages.Count == 0 && Question == null && AfterYes.Count == 0 && Card == null; }
            }
        }

        readonly List<Section> sections = new List<Section>();

        public IReadOnlyList<Section> Sections { get { return sections; } }

        /// <summary>見出しの頭が stem の物。無ければ null</summary>
        public Section Find(string stem)
        {
            foreach (var s in sections) if (s.Stem == stem) return s;
            return null;
        }

        /// <summary>1 ページに直す。外側の「」を外し、`&lt;br/&gt;` を改行にする</summary>
        public static string Page(string said)
        {
            if (said == null) return null;
            return said.Replace("<br/>", "\n").Replace("<br />", "\n").Replace("<br>", "\n");
        }

        const string LabelKey = "**対象の名前**:";
        const string ChoiceKey = "**二択**:";
        const string ListKey = "**リスト**";
        const string CardKey = "**暗転のカード**";
        const string AfterYesKey = "「はい」の後:";
        const string Fence = "```";

        enum Into { Pages, AfterYes, Card }

        /// <summary>原稿の中身 text を読む。読めない行があれば <see cref="ManuscriptException"/></summary>
        public static Manuscript Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            var made = new Manuscript();
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            Section current = null;
            var into = Into.Pages;
            for (var n = 0; n < lines.Length; n++)
            {
                var number = n + 1;
                var raw = lines[n];
                var line = raw.Trim();
                if (line.StartsWith("## ") || line.StartsWith("### "))
                {
                    var heading = line.Substring(line.IndexOf(' ') + 1).Trim();
                    current = new Section { Heading = heading, Stem = StemOf(heading), Line = number };
                    foreach (var other in made.sections)
                        if (other.Stem == current.Stem) throw new ManuscriptException(number, "見出しが二つある: " + heading);
                    made.sections.Add(current);
                    into = Into.Pages;
                    continue;
                }
                // 最初の ## より前は、原稿そのものの説明
                if (current == null) continue;
                if (line.Length == 0 || line.StartsWith("※") || line == "---") continue;
                if (line.StartsWith("#")) throw new ManuscriptException(number, "読めない見出し: " + line);
                if (line.StartsWith(LabelKey))
                {
                    current.Label = line.Substring(LabelKey.Length).Trim();
                    continue;
                }
                if (line.StartsWith(ChoiceKey))
                {
                    var question = line.Substring(ChoiceKey.Length).Trim();
                    var note = question.IndexOf('（');
                    if (note > 0) question = question.Substring(0, note).Trim();
                    if (question.Length == 0) throw new ManuscriptException(number, "二択の問いが空");
                    current.Question = question;
                    continue;
                }
                if (line == AfterYesKey)
                {
                    into = Into.AfterYes;
                    continue;
                }
                if (line == CardKey)
                {
                    into = Into.Card;
                    continue;
                }
                if (line == ListKey)
                {
                    // 空行を挟んでよい。次のコードブロックを 1 ページにする
                    var at = n + 1;
                    while (at < lines.Length && lines[at].Trim().Length == 0) at++;
                    if (at >= lines.Length || !lines[at].Trim().StartsWith(Fence))
                        throw new ManuscriptException(number, "**リスト** の後にコードブロックが無い");
                    var rows = new List<string>();
                    var end = at + 1;
                    while (end < lines.Length && lines[end].Trim() != Fence)
                    {
                        rows.Add(lines[end].TrimEnd());
                        end++;
                    }
                    if (end >= lines.Length) throw new ManuscriptException(at + 1, "コードブロックが閉じていない");
                    while (rows.Count > 0 && rows[rows.Count - 1].Length == 0) rows.RemoveAt(rows.Count - 1);
                    if (rows.Count == 0) throw new ManuscriptException(at + 1, "リストが空");
                    // 頭にリストの印を付ける。1 列だけの行でも画面の真ん中の枠に出す（ListFormat.Mark）
                    Add(current, into, ListFormat.Mark + string.Join("\n", rows.ToArray()), number);
                    n = end;
                    continue;
                }
                if (line.StartsWith("- 「") && line.EndsWith("」"))
                {
                    Add(current, into, Page(line.Substring(3, line.Length - 4)), number);
                    continue;
                }
                if (line.StartsWith("\"") && line.EndsWith("\""))
                    throw new ManuscriptException(number, "オーナーからの指示の行（\"…\"）が残っている。写す前に読んで、原稿から消す: " + line);
                throw new ManuscriptException(number, "読めない行: " + line);
            }
            return made;
        }

        static void Add(Section section, Into into, string page, int number)
        {
            switch (into)
            {
                case Into.AfterYes:
                    section.AfterYes.Add(page);
                    break;
                case Into.Card:
                    if (section.Card != null) throw new ManuscriptException(number, "暗転のカードは 1 ページだけ");
                    section.Card = page;
                    break;
                default:
                    section.Pages.Add(page);
                    break;
            }
        }

        /// <summary>見出しの頭。最初の全角か半角の括弧より前</summary>
        public static string StemOf(string heading)
        {
            var at = heading.IndexOfAny(new[] { '（', '(' });
            return (at > 0 ? heading.Substring(0, at) : heading).Trim();
        }
    }

    /// <summary>原稿を読めなかった。行の番号（1 から）を持つ</summary>
    public sealed class ManuscriptException : Exception
    {
        public readonly int Line;

        public ManuscriptException(int line, string message) : base(line > 0 ? line + " 行目: " + message : message)
        {
            Line = line;
        }
    }
}
