using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 場面 2 の台詞の原稿（docs/scenario/02-alley.md）から、ゲームの文面を組む。**原稿を正とし、文面はここを通して写す**
    /// （<c>HalfAware/Apply the scenario (alley)</c> が文面のアセット AlleyScript へ書く）。場面 1 の <see cref="RoomManuscript"/> と同じ作り。
    ///
    /// 見出しと行き先の対応は <see cref="Table"/> が持つ。原稿に表に無い見出しが来たら、あるいは表の見出しが原稿に無ければ、
    /// 黙って捨てずに <see cref="ManuscriptException"/> で知らせる。
    ///
    /// - 通りの看板は、看板の文面（原稿の **看板**、英語に日本語訳のルビ）を看板の項目（<see cref="AlleyIds.Sign"/>）に、その後の独白を段（<see cref="AlleyIds.Page"/>）に分ける。
    ///   調べるとまず看板の文面が画面の真ん中の枠に出て（<see cref="ListFormat.SignMark"/>）、続けて演出（<see cref="AlleyDirector"/>）が段を積む
    /// - 小路の入口の案内板は、看板の文面と独白を一つの項目に並べる
    /// - 買い手とのやり取り（`- 話者「…」`）と締めは、演出が自分で出す文（<see cref="AlleyIds.Buyer"/>・<see cref="AlleyIds.Closing"/>、<see cref="MarketSale"/>）
    /// </summary>
    public static class AlleyManuscript
    {
        /// <summary>原稿のファイル。リポジトリの頭から</summary>
        public const string Path = "docs/scenario/02-alley.md";

        /// <summary>見出しの中身の行き先</summary>
        public enum Goes
        {
            /// <summary>見出しをまとめるだけ（「通りの看板」「自分の露店」「売り買い」）。中身を持たない</summary>
            Group,
            /// <summary>演出が出す文。ページだけを持つ（冒頭の独白・買い手とのやり取り・締め）</summary>
            Said,
            /// <summary>通りの看板。対象の名前と看板の文面は看板の項目へ、その後のページは段へ</summary>
            Sign,
            /// <summary>看板の文面で始まる調べる対象（小路の入口の案内板）。対象の名前と、看板の文面から続くページ</summary>
            Board,
            /// <summary>調べる対象。対象の名前・ページ・二択</summary>
            Item,
        }

        public struct Row
        {
            /// <summary>見出しの頭（「（」より前）</summary>
            public string Stem;
            /// <summary>文面のアセットの id。まとめは null</summary>
            public string Id;
            public Goes Goes;
            /// <summary>通りの看板の番号。看板でなければ -1</summary>
            public int Sign;

            public Row(string stem, string id, Goes goes, int sign = -1)
            {
                Stem = stem;
                Id = id;
                Goes = goes;
                Sign = sign;
            }
        }

        /// <summary>見出しと行き先。原稿の見出しを足したり名前を変えたりしたら、ここも合わせる</summary>
        public static readonly Row[] Table =
        {
            new Row("冒頭", AlleyIds.Opening, Goes.Said),
            new Row("通りの看板", null, Goes.Group),
            new Row("グレビル・ストリートの看板", AlleyIds.Sign(0), Goes.Sign, 0),
            new Row("薬局の看板", AlleyIds.Sign(1), Goes.Sign, 1),
            new Row("ナーヴ・ターミナルの店の看板", AlleyIds.Sign(2), Goes.Sign, 2),
            new Row("質屋の看板", AlleyIds.Sign(3), Goes.Sign, 3),
            new Row("ナノマシンの注意の看板", AlleyIds.Sign(4), Goes.Sign, 4),
            new Row("小路の入口の案内板", AlleyIds.Board, Goes.Board),
            new Row("自分の露店", null, Goes.Group),
            new Row("看板", AlleyIds.StallSign, Goes.Item),
            new Row("テーブル", AlleyIds.Table, Goes.Item),
            new Row("売り買い", null, Goes.Group),
            new Row("買い手A", AlleyIds.Buyer(0), Goes.Said),
            new Row("買い手B", AlleyIds.Buyer(1), Goes.Said),
            new Row("買い手C", AlleyIds.Buyer(2), Goes.Said),
            new Row("締め", AlleyIds.Closing, Goes.Said),
        };

        /// <summary>写す文面。項目は <see cref="AlleyIds.All"/> の順。前提の文（hints）は持たない</summary>
        public sealed class Text
        {
            public ScriptEntry[] Entries;

            public ScriptEntry Find(string id)
            {
                return ScriptEntry.Find(Entries, id);
            }
        }

        /// <summary>原稿の中身 manuscript から、写す文面を組む。読めない所があれば <see cref="ManuscriptException"/></summary>
        public static Text Read(string manuscript)
        {
            var m = Manuscript.Parse(manuscript);
            var problems = new List<string>();
            foreach (var s in m.Sections)
                if (RowOf(s.Stem) == null) problems.Add(s.Line + " 行目: 表に無い見出し「" + s.Heading + "」（AlleyManuscript.Table に足す）");
            foreach (var row in Table)
                if (m.Find(row.Stem) == null) problems.Add("原稿に無い見出し「" + row.Stem + "」（原稿か AlleyManuscript.Table を直す）");
            foreach (var id in AlleyIds.All)
            {
                var found = AlleyIds.IsPage(id);
                foreach (var row in Table) found |= row.Id == id;
                if (!found) problems.Add("表に行き先の無い id: " + id);
            }

            var entries = new Dictionary<string, ScriptEntry>();
            foreach (var row in Table)
            {
                var s = m.Find(row.Stem);
                if (s == null) continue;
                if (s.AfterYes.Count > 0 || s.Card != null)
                    problems.Add(s.Line + " 行目: 「" + s.Heading + "」に「はい」の後や暗転のカードがある（場面 2 では使わない）");
                var signs = 0;
                foreach (var page in s.Pages) if (ListFormat.IsSign(page)) signs++;
                switch (row.Goes)
                {
                    case Goes.Group:
                        if (!s.Empty) problems.Add(s.Line + " 行目: まとめの見出し「" + s.Heading + "」の直下に中身がある（中身は ### の見出しの下に書く）");
                        break;
                    case Goes.Said:
                        if (s.Label != null || s.Question != null)
                            problems.Add(s.Line + " 行目: 「" + s.Heading + "」はページだけを持つ");
                        if (signs > 0) problems.Add(s.Line + " 行目: 「" + s.Heading + "」に看板がある（看板は看板と案内板の見出しの下だけ）");
                        if (row.Id != AlleyIds.Opening && s.Pages.Count == 0) problems.Add(s.Line + " 行目: 「" + s.Heading + "」にページが無い");
                        entries[row.Id] = Entry(row.Id, null, s.Pages, null);
                        break;
                    case Goes.Sign:
                    case Goes.Board:
                        if (s.Question != null) problems.Add(s.Line + " 行目: 「" + s.Heading + "」は二択を持たない");
                        if (signs != 1 || !ListFormat.IsSign(s.Pages[0]))
                        {
                            problems.Add(s.Line + " 行目: 「" + s.Heading + "」は **看板** で始め、看板は一つだけ");
                            break;
                        }
                        if (string.IsNullOrEmpty(s.Label)) problems.Add(s.Line + " 行目: 「" + s.Heading + "」に対象の名前が無い");
                        if (row.Goes == Goes.Board)
                        {
                            entries[row.Id] = Entry(row.Id, s.Label, s.Pages, null);
                            break;
                        }
                        entries[row.Id] = Entry(row.Id, s.Label, s.Pages.GetRange(0, 1), null);
                        entries[AlleyIds.Page(row.Sign)] = Entry(AlleyIds.Page(row.Sign), string.Empty, s.Pages.GetRange(1, s.Pages.Count - 1), null);
                        break;
                    default:
                        if (string.IsNullOrEmpty(s.Label)) problems.Add(s.Line + " 行目: 「" + s.Heading + "」に対象の名前が無い");
                        if (signs > 0) problems.Add(s.Line + " 行目: 「" + s.Heading + "」に看板がある（看板は看板と案内板の見出しの下だけ）");
                        entries[row.Id] = Entry(row.Id, s.Label, s.Pages, s.Question);
                        break;
                }
            }
            // 煙草を置く行は、その買い手の最後の台詞（MarketSale.PutsSmokes）。頭の行に話者が無いと決まらない
            for (var i = 0; i < MarketSale.Count; i++)
            {
                ScriptEntry e;
                if (!entries.TryGetValue(AlleyIds.Buyer(i), out e)) continue;
                if (e.lines.Length > 0 && string.IsNullOrEmpty(Speech.Who(e.lines[0])))
                    problems.Add("買い手 " + i + " のやり取りの頭の行に話者が無い（`- 買い手A「…」` の形で書く）: " + e.lines[0]);
            }
            if (problems.Count > 0) throw new ManuscriptException(0, "原稿を写せない（" + Path + "）\n" + string.Join("\n", problems.ToArray()));

            var all = AlleyIds.All;
            var text = new Text { Entries = new ScriptEntry[all.Length] };
            for (var i = 0; i < all.Length; i++) text.Entries[i] = entries[all[i]];
            return text;
        }

        static ScriptEntry Entry(string id, string label, List<string> pages, string question)
        {
            var e = new ScriptEntry
            {
                id = id,
                label = label ?? string.Empty,
                lines = pages.ToArray(),
                hints = new ScriptHint[0],
            };
            e.choice.question = question ?? string.Empty;
            e.choice.afterYes = new string[0];
            return e;
        }

        static Row? RowOf(string stem)
        {
            foreach (var row in Table) if (row.Stem == stem) return row;
            return null;
        }
    }
}
