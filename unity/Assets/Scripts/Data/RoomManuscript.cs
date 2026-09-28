using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の台詞の原稿（docs/scenario/01-room.md）から、ゲームの文面を組む。**原稿を正とし、文面はここを通して写す**
    /// （<c>HalfAware/Apply the scenario (room)</c> が文面のアセット RoomScript と、<see cref="RoomIntroDirector"/> の Inspector の文へ書く）。
    ///
    /// 見出しと行き先の対応は <see cref="Table"/> が持つ。原稿に表に無い見出しが来たら、あるいは表の見出しが原稿に無ければ、
    /// 黙って捨てずに <see cref="ManuscriptException"/> で知らせる。
    /// 調べる順（前提）は台詞ではないので、ここでは持たない（場面の Interactable の after。<c>PlaceProtagonistJacket</c>）
    /// </summary>
    public static class RoomManuscript
    {
        /// <summary>原稿のファイル。リポジトリの頭から</summary>
        public const string Path = "docs/scenario/01-room.md";

        /// <summary>カードの字の大きさ。前に手で組んでいたカードと同じ。台詞ではないので原稿には書かない</summary>
        public const string CardOpen = "<size=30>";
        public const string CardShut = "</size>";

        /// <summary>見出しの中身の行き先</summary>
        public enum Goes
        {
            /// <summary>見出しをまとめるだけ（「任意の対象」）。中身を持たない</summary>
            Group,
            /// <summary>起き上がり終えてから言う独白（<see cref="RoomIntroDirector"/> の firstLines）</summary>
            Opening,
            /// <summary>調べる対象。文面のアセットの id の項目へ（対象の名前・ページ・二択・「はい」の後）</summary>
            Item,
            /// <summary>ジャケット。対象の名前は文面のアセットへ、ページは着る音の後の独白（<see cref="RoomIntroDirector"/> の afterJacketLines）へ</summary>
            Jacket,
        }

        public struct Row
        {
            /// <summary>見出しの頭（「（」より前）</summary>
            public string Stem;
            /// <summary>文面のアセットの id。まとめと冒頭は null</summary>
            public string Id;
            public Goes Goes;

            public Row(string stem, string id, Goes goes)
            {
                Stem = stem;
                Id = id;
                Goes = goes;
            }
        }

        /// <summary>見出しと行き先。原稿の見出しを足したり名前を変えたりしたら、ここも合わせる</summary>
        public static readonly Row[] Table =
        {
            new Row("冒頭", null, Goes.Opening),
            new Row("ジャック", RoomIds.Jack, Goes.Item),
            new Row("煙草", RoomIds.Cigarette, Goes.Item),
            new Row("ジャケット", RoomIds.Jacket, Goes.Jacket),
            new Row("メモリハブ", RoomIds.Chips, Goes.Item),
            new Row("モニター", RoomIds.Terminal, Goes.Item),
            new Row("ドア", RoomIds.Door, Goes.Item),
            new Row("任意の対象", null, Goes.Group),
            new Row("灰皿", RoomIds.Ashtray, Goes.Item),
            new Row("煙草の箱", RoomIds.CigaretteBox, Goes.Item),
            new Row("メモ", RoomIds.Clipboard, Goes.Item),
        };

        /// <summary>写す文面</summary>
        public sealed class Text
        {
            /// <summary>文面のアセットの項目。<see cref="RoomIds.All"/> の順。前提の文（hints）は持たない</summary>
            public ScriptEntry[] Entries;
            /// <summary>起き上がり終えてから言う独白</summary>
            public string[] Opening;
            /// <summary>暗転のカード。字の大きさの書式（<see cref="CardOpen"/>）を付けた形</summary>
            public string Card;
            /// <summary>ジャケットを着た後の独白</summary>
            public string[] AfterJacket;

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
                if (RowOf(s.Stem) == null) problems.Add(s.Line + " 行目: 表に無い見出し「" + s.Heading + "」（RoomManuscript.Table に足す）");
            foreach (var row in Table)
                if (m.Find(row.Stem) == null) problems.Add("原稿に無い見出し「" + row.Stem + "」（原稿か RoomManuscript.Table を直す）");
            foreach (var id in RoomIds.All)
            {
                var found = false;
                foreach (var row in Table) found |= row.Id == id;
                if (!found) problems.Add("表に行き先の無い id: " + id);
            }

            var text = new Text { Opening = new string[0], AfterJacket = new string[0] };
            var entries = new Dictionary<string, ScriptEntry>();
            foreach (var row in Table)
            {
                var s = m.Find(row.Stem);
                if (s == null) continue;
                switch (row.Goes)
                {
                    case Goes.Group:
                        if (!s.Empty) problems.Add(s.Line + " 行目: まとめの見出し「" + s.Heading + "」の直下に中身がある（中身は ### の見出しの下に書く）");
                        break;
                    case Goes.Opening:
                        if (s.Label != null || s.Question != null || s.AfterYes.Count > 0 || s.Card != null)
                            problems.Add(s.Line + " 行目: 「" + s.Heading + "」はページだけを持つ");
                        text.Opening = s.Pages.ToArray();
                        break;
                    case Goes.Jacket:
                        if (s.Question != null || s.AfterYes.Count > 0 || s.Card != null)
                            problems.Add(s.Line + " 行目: 「" + s.Heading + "」は対象の名前とページだけを持つ");
                        text.AfterJacket = s.Pages.ToArray();
                        entries[row.Id] = Entry(row.Id, s, false, problems);
                        break;
                    default:
                        entries[row.Id] = Entry(row.Id, s, true, problems);
                        break;
                }
                if (s.Card != null)
                {
                    if (text.Card != null) problems.Add(s.Line + " 行目: 暗転のカードが二つある");
                    else text.Card = CardOpen + s.Card + CardShut;
                }
            }
            if (text.Card == null) problems.Add("暗転のカードが無い");
            if (problems.Count > 0) throw new ManuscriptException(0, "原稿を写せない（" + Path + "）\n" + string.Join("\n", problems.ToArray()));

            text.Entries = new ScriptEntry[RoomIds.All.Length];
            for (var i = 0; i < RoomIds.All.Length; i++) text.Entries[i] = entries[RoomIds.All[i]];
            return text;
        }

        static ScriptEntry Entry(string id, Manuscript.Section s, bool pages, List<string> problems)
        {
            if (string.IsNullOrEmpty(s.Label)) problems.Add(s.Line + " 行目: 「" + s.Heading + "」に対象の名前が無い");
            var e = new ScriptEntry
            {
                id = id,
                label = s.Label ?? string.Empty,
                lines = pages ? s.Pages.ToArray() : new string[0],
                hints = new ScriptHint[0],
            };
            e.choice.question = s.Question ?? string.Empty;
            e.choice.afterYes = s.AfterYes.ToArray();
            return e;
        }

        static Row? RowOf(string stem)
        {
            foreach (var row in Table) if (row.Stem == stem) return row;
            return null;
        }
    }
}
