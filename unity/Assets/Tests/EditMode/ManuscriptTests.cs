using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 台詞の原稿（docs/scenario/*.md）の読み方（<see cref="Manuscript"/>）と、場面 1 の原稿から写す文面（<see cref="RoomManuscript"/>）。
    /// 写す文面は、実際の docs/scenario/01-room.md を読ませて確かめる
    /// </summary>
    public class ManuscriptTests
    {
        static string RoomFile
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", RoomManuscript.Path)); }
        }

        static RoomManuscript.Text Room()
        {
            Assert.That(File.Exists(RoomFile), Is.True, RoomFile + " が無い");
            return RoomManuscript.Read(File.ReadAllText(RoomFile, Encoding.UTF8));
        }

        // ---- 読み方 --------------------------------------------------------

        const string Sample =
            "# 原稿\n" +
            "\n" +
            "- 説明の行は読み飛ばす\n" +
            "\n" +
            "---\n" +
            "\n" +
            "## 灰皿（任意）\n" +
            "\n" +
            "**対象の名前**: 灰皿\n" +
            "\n" +
            "※ 演出の注記\n" +
            "\n" +
            "- 「一行目。<br/>二行目。」\n" +
            "- 「『声』と身体<からだ>」\n" +
            "\n" +
            "**リスト**\n" +
            "\n" +
            "```\n" +
            "a  b\n" +
            "c  d\n" +
            "```\n" +
            "\n" +
            "**二択**: 片づける（「はい」で消える）\n" +
            "\n" +
            "「はい」の後:\n" +
            "\n" +
            "- 「片づけた。」\n" +
            "\n" +
            "### カード\n" +
            "\n" +
            "**暗転のカード**\n" +
            "\n" +
            "- 「2166年　倫敦<ロンドン>」\n";

        [Test]
        public void ItReadsEveryKindOfLine()
        {
            var m = Manuscript.Parse(Sample);
            Assert.AreEqual(2, m.Sections.Count, "最初の ## より前は読み飛ばす");
            var s = m.Find("灰皿");
            Assert.AreEqual("灰皿（任意）", s.Heading);
            Assert.AreEqual("灰皿", s.Label);
            Assert.AreEqual(new[] { "一行目。\n二行目。", "『声』と身体<からだ>", ListFormat.Mark + "a  b\nc  d" }, s.Pages.ToArray(),
                "外側の「」を外し、<br/> を改行に。リストは行を改行で繋いで 1 ページにし、頭にリストの印。ルビの書き方はそのまま");
            Assert.AreEqual("片づける", s.Question, "二択の説明の括弧は落とす");
            Assert.AreEqual(new[] { "片づけた。" }, s.AfterYes.ToArray());
            Assert.AreEqual("2166年　倫敦<ロンドン>", m.Find("カード").Card);
        }

        [Test]
        public void ALineItCannotReadIsAnError()
        {
            var e = Assert.Throws<ManuscriptException>(() => Manuscript.Parse("## 灰皿\n\nなにか書いた行\n"));
            Assert.AreEqual(3, e.Line);
        }

        [Test]
        public void AnInstructionForClaireIsAnError()
        {
            // "…" で括った文はオーナーから CLaiRE への指示。写さずに知らせる
            var e = Assert.Throws<ManuscriptException>(() => Manuscript.Parse("## 灰皿\n\n\"ここを直して\"\n"));
            StringAssert.Contains("指示", e.Message);
        }

        [Test]
        public void AListWithoutACodeBlockIsAnError()
        {
            Assert.Throws<ManuscriptException>(() => Manuscript.Parse("## メモ\n\n**リスト**\n\n- 「a」\n"));
            Assert.Throws<ManuscriptException>(() => Manuscript.Parse("## メモ\n\n**リスト**\n```\na  b\n"));
        }

        [Test]
        public void AHeadingTwiceIsAnError()
        {
            Assert.Throws<ManuscriptException>(() => Manuscript.Parse("## 灰皿\n## 灰皿（任意）\n"));
        }

        [Test]
        public void AHeadingNotInTheTableIsAnError()
        {
            var text = File.ReadAllText(RoomFile, Encoding.UTF8) + "\n## 窓\n\n**対象の名前**: 窓\n\n- 「外が見える。」\n";
            var e = Assert.Throws<ManuscriptException>(() => RoomManuscript.Read(text));
            StringAssert.Contains("表に無い見出し「窓」", e.Message);
        }

        [Test]
        public void AHeadingMissingFromTheManuscriptIsAnError()
        {
            var text = File.ReadAllText(RoomFile, Encoding.UTF8).Replace("### 灰皿", "### 吸い殻入れ");
            var e = Assert.Throws<ManuscriptException>(() => RoomManuscript.Read(text));
            StringAssert.Contains("原稿に無い見出し「灰皿」", e.Message);
        }

        // ---- 場面 1 の原稿 ----------------------------------------------------

        [Test]
        public void TheRoomManuscriptCoversEveryIdInOrder()
        {
            var text = Room();
            Assert.AreEqual(RoomIds.All.Length, text.Entries.Length);
            for (var i = 0; i < RoomIds.All.Length; i++) Assert.AreEqual(RoomIds.All[i], text.Entries[i].id);
        }

        [TestCase(RoomIds.Jack, "ケーブルを抜く")]
        [TestCase(RoomIds.Cigarette, "煙草を取る")]
        [TestCase(RoomIds.Jacket, "ジャケットを着る")]
        [TestCase(RoomIds.Chips, "メモリハブ")]
        [TestCase(RoomIds.Terminal, "モニター")]
        [TestCase(RoomIds.Door, "ドア")]
        [TestCase(RoomIds.Ashtray, "灰皿")]
        [TestCase(RoomIds.CigaretteBox, "煙草の箱")]
        [TestCase(RoomIds.Clipboard, "メモ")]
        public void EveryItemHasItsLabel(string id, string label)
        {
            Assert.AreEqual(label, Room().Find(id).label);
        }

        [Test]
        public void TheJackReadsAsWritten()
        {
            var jack = Room().Find(RoomIds.Jack);
            Assert.AreEqual(3, jack.lines.Length);
            Assert.AreEqual("手首のインプラントジャックからナーヴ・ターミナルの外部ストレージケーブルを引き抜くと同時に、\n" +
                "身体<からだ>に籠っていた熱が抜けていくように感じて心地よい。", jack.lines[0]);
            StringAssert.StartsWith("<dot>他人の記憶に潜った後</dot>は大小の差こそあれ", jack.lines[2]);
        }

        [Test]
        public void TheOpeningAndTheJacketGoToTheDirector()
        {
            var text = Room();
            Assert.AreEqual(new[] { "『………ううっ』", "『今回のは酔いが酷いな…』", "『一旦ここまでにしておこう…』" }, text.Opening);
            Assert.AreEqual(2, text.AfterJacket.Length);
            Assert.AreEqual("煙草が切れてしまったので外出しなければ。\nついでに今日回収した分のメモリ<記憶>も売ってしまおう。", text.AfterJacket[1]);
            Assert.AreEqual(0, text.Find(RoomIds.Jacket).lines.Length, "ジャケットのページは着る音の後に演出が出す");
        }

        [Test]
        public void TheCigaretteHasOnePageAndTheCard()
        {
            var text = Room();
            Assert.AreEqual(new[] { "煙草を手に取り、いつか2ドルで買ったボロボロのジッポで火を点ける。" }, text.Find(RoomIds.Cigarette).lines);
            Assert.AreEqual(RoomManuscript.CardOpen + "2166年8月15日 19時35分　倫敦<ロンドン>　自室" + RoomManuscript.CardShut, text.Card);
        }

        [Test]
        public void TheBoxKeepsTheBrokenSentenceAsWritten()
        {
            // 「。の箱だけ。」のような崩しも原稿どおり
            Assert.AreEqual(new[] { "『双鶴<シュアンフゥ>』という名の中国産紙巻き煙草。の箱だけ。今は空っぽだ。かなしい。" },
                Room().Find(RoomIds.CigaretteBox).lines);
        }

        [Test]
        public void TheChipsListIsOnePageAfterTheLine()
        {
            var chips = Room().Find(RoomIds.Chips);
            Assert.AreEqual(2, chips.lines.Length);
            Assert.AreEqual(6, SubtitleBox.LineCount(chips.lines[1]));
            StringAssert.StartsWith(ListFormat.Mark + "08/15  #1  Male    41  32m40s  『Diego』\n", chips.lines[1]);
            Assert.IsTrue(ListFormat.IsList(chips.lines[1]));
            Assert.AreEqual("メモリチップを抜く", chips.choice.question);
            Assert.AreEqual(0, chips.choice.afterYes.Length);
        }

        [Test]
        public void TheMonitorShowsTheConditionsAfterYes()
        {
            var monitor = Room().Find(RoomIds.Terminal);
            Assert.AreEqual(6, monitor.lines.Length);
            Assert.AreEqual("こうして画面の反射で自分の顔が見られるからだ。", monitor.lines[TerminalReflection.FromPage],
                "2 ページ目と同時に顔が映る（原稿の注記）");
            Assert.AreEqual("スリープを解除する", monitor.choice.question);
            Assert.AreEqual(2, monitor.choice.afterYes.Length);
            Assert.AreEqual(5, SubtitleBox.LineCount(monitor.choice.afterYes[1]));
            StringAssert.StartsWith(ListFormat.Mark + "条件: 名前を呼ばれた時刻\n", monitor.choice.afterYes[1]);
            // 「条件: 名前を呼ばれた時刻」は半角の空白 1 つなので 1 列。それでもリストの印で画面の真ん中の枠に出す
            Assert.AreEqual(1, ListFormat.Most(monitor.choice.afterYes[1]));
            Assert.IsTrue(ListFormat.IsList(monitor.choice.afterYes[1]));
            Assert.IsFalse(ListFormat.IsList(monitor.choice.afterYes[0]), "リストの前の 1 ページは字幕の窓");
        }

        [Test]
        public void TheListMarkNeverShows()
        {
            // 印は表に組むかを決めるだけ。組んだ物・ログ・親字だけの形には出ない
            var page = Room().Find(RoomIds.Terminal).choice.afterYes[1];
            Assert.AreEqual(-1, ListFormat.Compose(page, ListLayout.RoomEm, false).IndexOf(ListFormat.Mark));
            Assert.AreEqual(-1, Ruby.Expand(page).IndexOf(ListFormat.Mark));
            Assert.AreEqual(-1, Ruby.Plain(page).IndexOf(ListFormat.Mark));
            StringAssert.StartsWith("条件: 名前を呼ばれた時刻", ListFormat.Compose(page, ListLayout.RoomEm, false));
        }

        [Test]
        public void TheDoorAsksToLeaveHome()
        {
            var door = Room().Find(RoomIds.Door);
            Assert.AreEqual("家を出る", door.choice.question);
            Assert.AreEqual(new[] { "さて、煙草の補充ついでに今日のメモリチップを売りに行こう。" }, door.lines);
        }

        [Test]
        public void TheMemoIsOnePageList()
        {
            var memo = Room().Find(RoomIds.Clipboard);
            Assert.AreEqual(2, memo.lines.Length);
            Assert.AreEqual(ListFormat.Mark + "2166/08/10  仕入  30枚  在庫  47枚\n" +
                "2166/08/11  売却   5枚  在庫  42枚\n" +
                "2166/08/13  売却   4枚  在庫  38枚\n" +
                "2166/08/15", memo.lines[1]);
            // 「 5枚」は「30枚」と尻を揃える（数で始まる塊だけの列）
            var rows = ListFormat.Compose(memo.lines[1], ListLayout.RoomEm, false).Split('\n');
            Assert.AreEqual(Pos(rows[0], "30枚") + 0.5f, Pos(rows[1], "5枚"), 1e-3f);
        }

        /// <summary>line の中で text の直前に置いた pos の値（em）</summary>
        static float Pos(string line, string text)
        {
            var at = line.IndexOf("em>" + text);
            Assert.GreaterOrEqual(at, 0, text + " が無い: " + line);
            var from = line.LastIndexOf("<pos=", at) + 5;
            return float.Parse(line.Substring(from, at - from), System.Globalization.CultureInfo.InvariantCulture);
        }

        [Test]
        public void TheConsoleClockFollowsTheCard()
        {
            // コンソールの頭の時刻（場面 1・2）は、原稿のカードの時刻の後。カードを 18時35分 から 19時35分 に直したので 1 時間ずらした（2026-09-28）
            var card = System.Text.RegularExpressions.Regex.Match(Room().Card, @"(\d+)時(\d+)分");
            Assert.That(card.Success, Is.True, Room().Card);
            var at = int.Parse(card.Groups[1].Value) * 60 + int.Parse(card.Groups[2].Value);
            var room = Minutes(ConsolePlace.For("Room"));
            var alley = Minutes(ConsolePlace.For("Alley"));
            Assert.That(room, Is.GreaterThan(at), "一服したあと");
            Assert.That(room - at, Is.LessThan(30), "一服とジャケットのぶんだけ後");
            Assert.That(alley, Is.GreaterThan(room), "路地裏は部屋を出た後");
            Assert.That(Minutes(ConsolePlace.For("Connect")), Is.GreaterThan(alley), "場面 3 は路地裏から戻った後");
        }

        static int Minutes(string head)
        {
            var m = System.Text.RegularExpressions.Regex.Match(head, @"(\d\d):(\d\d)");
            Assert.That(m.Success, Is.True, head);
            return int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
        }

        [Test]
        public void NothingCarriesHintsAnyMore()
        {
            // 前提が未達の時の文は原稿に無い。どれも持たない（ドアは必須が残っている間、狙っても何も出ない）
            foreach (var e in Room().Entries) Assert.AreEqual(0, e.hints.Length, e.id);
        }
    }
}
