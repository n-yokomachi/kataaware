using NUnit.Framework;

namespace HalfAware.Tests
{
    public class ListFormatTests
    {
        /// <summary>寄せの無い素の組み方を見るときは、帯の幅を 0 にして呼ぶ</summary>
        const float NoRoom = 0f;

        [Test]
        public void OneLineIsNotAList()
        {
            Assert.IsFalse(ListFormat.IsList("メモリが6枚。今日の分だ"));
            Assert.IsFalse(ListFormat.IsList(null));
        }

        [Test]
        public void SeveralLinesWithoutBreaksAreNotAList()
        {
            Assert.IsFalse(ListFormat.IsList("ひとつめ\nふたつめ"), "区切りが無ければただの文");
        }

        [Test]
        public void SeveralLinesSplitBySpacesAreAList()
        {
            Assert.IsTrue(ListFormat.IsList("2166/08/13　5枚\n2166/08/14　4枚"));
            Assert.IsTrue(ListFormat.IsList("08/15 #1 男 41\n08/15 #2 女 23"));
        }

        [Test]
        public void FullWidthCountsAsTwo()
        {
            Assert.AreEqual(2, ListFormat.Units("あ"));
            Assert.AreEqual(1, ListFormat.Units("a"));
            Assert.AreEqual(4, ListFormat.Units("5枚a"), "半角 1 + 全角 2 + 半角 1");
            Assert.AreEqual(0, ListFormat.Units(""));
        }

        [Test]
        public void ColumnsGetTheSameStartOnEveryRow()
        {
            var text = "条件　名前を呼ばれた時刻\n候補の簡易抽出　56,232,318";
            var made = ListFormat.Compose(text, NoRoom);
            // いちばん長い見出し「候補の簡易抽出」は全角 7 文字 = 半角 14、そこへ間を 2 つ足す
            var expected = "<pos=" + ((14 + ListFormat.Gap) * 0.5f).ToString("0.##") + "em>";
            Assert.AreEqual(2, Count(made, expected), "どちらの行も同じ位置から値が始まる");
        }

        [Test]
        public void TheFirstColumnHasNoTagWhenNothingIsCentred()
        {
            var made = ListFormat.Compose("条件　あ\n対象　い", NoRoom);
            StringAssert.StartsWith("条件<pos=", made);
        }

        [Test]
        public void ARoomyBandPushesTheWholeTableToTheMiddle()
        {
            var text = "条件　あ\n対象　い";
            var made = ListFormat.Compose(text, 60f);
            StringAssert.StartsWith("<pos=", made, "1 列目から位置を置いて真ん中へ寄せる");
        }

        [Test]
        public void EveryWordSurvives()
        {
            var text = "08/15 #1 男 41 『ディエゴ』 3分40秒\n08/15 #2 女 23 『ミア』 2分05秒";
            var made = ListFormat.Compose(text, 60f);
            foreach (var word in new[] { "08/15", "#1", "男", "41", "『ディエゴ』", "3分40秒", "『ミア』" })
                StringAssert.Contains(word, made);
            Assert.AreEqual(2, Count(made, "\n") + 1, "行数は変わらない");
        }

        [Test]
        public void ItDropsColumnsUntilItFits()
        {
            var text = "対象　防壁なし　距離 ランダム　期間 2156年3月2日\n条件　名前を呼ばれた時刻";
            var wide = ListFormat.Compose(text, 200f);
            var tight = ListFormat.Compose(text, 12f);
            Assert.Greater(Count(wide, "<pos="), Count(tight, "<pos="), "狭ければ列を減らす");
            StringAssert.Contains("距離 ランダム", tight, "減らした分は元のまま残る");
        }

        [Test]
        public void ItNeverDropsBelowTwoColumns()
        {
            var made = ListFormat.Compose("あ い う\nか き", 1f);
            foreach (var row in made.Split('\n')) Assert.AreEqual(1, Count(row, "<pos="));
        }

        [Test]
        public void ARowWithFewerCellsJustStopsEarly()
        {
            var made = ListFormat.Compose("あ い う\nか き", NoRoom + 200f);
            var rows = made.Split('\n');
            Assert.AreEqual(3, Count(rows[0], "<pos="), "寄せの分も入れて 3 つ");
            Assert.AreEqual(2, Count(rows[1], "<pos="));
        }

        [Test]
        public void SplittingKeepsTheTailWhole()
        {
            var cells = ListFormat.Split("対象　防壁なし　距離 ランダム", 2);
            Assert.AreEqual(2, cells.Count);
            Assert.AreEqual("対象", cells[0]);
            Assert.AreEqual("防壁なし　距離 ランダム", cells[1]);
        }

        static int Count(string text, string needle)
        {
            var n = 0;
            var at = 0;
            while ((at = text.IndexOf(needle, at)) >= 0) { n++; at += needle.Length; }
            return n;
        }
    }
}
