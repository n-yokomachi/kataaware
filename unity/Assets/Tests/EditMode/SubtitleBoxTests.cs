using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SubtitleBoxTests
    {
        [Test]
        public void NothingHasNoLines()
        {
            Assert.AreEqual(0, SubtitleBox.LineCount(null));
            Assert.AreEqual(0, SubtitleBox.LineCount(""));
        }

        [Test]
        public void LinesAreCountedByTheBreaks()
        {
            Assert.AreEqual(1, SubtitleBox.LineCount("ひとつ"));
            Assert.AreEqual(3, SubtitleBox.LineCount("ひとつ\nふたつ\nみっつ"));
        }

        [Test]
        public void TheWindowNeverShrinksBelowTwoRows()
        {
            Assert.AreEqual(2, SubtitleBox.Rows(""));
            Assert.AreEqual(2, SubtitleBox.Rows("一行だけ"));
            Assert.AreEqual(2, SubtitleBox.Rows("一行\n二行"));
        }

        [Test]
        public void TheWindowGrowsWithTheLines()
        {
            Assert.AreEqual(3, SubtitleBox.Rows("一\n二\n三"));
            Assert.AreEqual(6, SubtitleBox.Rows("一\n二\n三\n四\n五\n六"));
        }

        [Test]
        public void TheWindowStopsGrowingAtSomePoint()
        {
            var many = string.Join("\n", new string[20]);
            Assert.AreEqual(SubtitleBox.MaxRows, SubtitleBox.Rows(many));
        }

        [Test]
        public void ShortBlocksKeepTheirSize()
        {
            Assert.AreEqual(1f, SubtitleBox.FontScale("一行"), 1e-4f);
            Assert.AreEqual(1f, SubtitleBox.FontScale("一\n二\n三\n四"), 1e-4f);
        }

        [Test]
        public void LongBlocksAreSetSmaller()
        {
            var six = "一\n二\n三\n四\n五\n六";
            Assert.Less(SubtitleBox.FontScale(six), 1f, "六行は小さくして収める");
            Assert.GreaterOrEqual(SubtitleBox.FontScale(six), SubtitleBox.MinScale);
        }

        [Test]
        public void ItNeverShrinksPastTheLimit()
        {
            var many = string.Join("\n", new string[40]);
            Assert.AreEqual(SubtitleBox.MinScale, SubtitleBox.FontScale(many), 1e-4f);
        }

        // ---- 一定の行の間（オーナー、2026-09-29） ------------------------------------

        [Test]
        public void TheLinePitchAlwaysMakesRoomForARuby()
        {
            // 行の送りは、ルビの有る無しにかかわらず、下の行のルビが上の行の字にかからない間
            // オーナーの「行間はもう少し空けて」で 2.0 em（台詞の字で 26 Dot）
            Assert.AreEqual(2.0f, SubtitleBox.LineEm, 1e-4f);
            Assert.That(SubtitleBox.LineEm, Is.GreaterThanOrEqualTo(Ruby.Advance + Ruby.ExtraLineSpacing * 0.01f), "ルビのための行間を含む");
            var rubyTop = Ruby.Lift + Ruby.RubyTop * Ruby.Scale;
            Assert.That(SubtitleBox.LineEm - rubyTop, Is.GreaterThanOrEqualTo(Ruby.BaseBottom + 0.05f));
            // ルビの上の隙間（上の行の字の下の端まで）は、下の隙間（親字の上の端まで）より広い。ルビが上の行に付いて見えない
            var above = SubtitleBox.LineEm - rubyTop - Ruby.BaseBottom;
            var below = Ruby.Lift - Ruby.RubyDip * Ruby.Scale - Ruby.BaseTop;
            Assert.That(above, Is.GreaterThan(below));
        }

        [Test]
        public void TheFirstLineLeavesRoomForARubyAboveIt()
        {
            // 1 行目のベースラインは、ルビの字の上の線（持ち上げた高さ ＋ ルビの大きさの ascent）の下。TMP で測って 1.80 em
            Assert.AreEqual(1.804f, SubtitleBox.TopEm, 0.005f);
            Assert.That(SubtitleBox.TopEm, Is.GreaterThan(Ruby.Lift + Ruby.RubyTop * Ruby.Scale), "ルビの字の上の端が枠の上に出ない");
        }

        [Test]
        public void TheBodyGrowsByTheSamePitchEveryLine()
        {
            // TMP で 3 行を組んで測った高さ（行の送り 1.745 em で 5.582 em ＝ 1 行目 1.80 ＋ 1.745 × 2 ＋ 下の線 0.288）と同じ式で、送りを 2.0 em にした 6.092 em
            Assert.AreEqual(1.804f + 2.0f * 2f + 0.288f, SubtitleBox.BodyEm(3), 0.005f);
            for (var rows = 2; rows < SubtitleBox.MaxRows; rows++)
                Assert.AreEqual(SubtitleBox.LineEm, SubtitleBox.BodyEm(rows + 1) - SubtitleBox.BodyEm(rows), 1e-4f);
        }

        [Test]
        public void TheFrameFixesTheLineHeightAndReservesTheRuby()
        {
            var made = SubtitleBox.Frame("一行目\n二行目");
            StringAssert.StartsWith("<line-height=2em>", made);
            StringAssert.Contains(SubtitleBox.Headroom, made);
            StringAssert.EndsWith("一行目\n二行目", made);
            Assert.AreEqual(null, SubtitleBox.Frame(null));
            Assert.AreEqual("", SubtitleBox.Frame(""));
        }

        [Test]
        public void EightRowsStillFitOnTheScreen()
        {
            // 上限 8 行で、字を小さくした窓（名前の行 ＋ 字の枠 ＋ 下の余白）が画面（粗い画面の 405 Dot）の半分ほどに収まる
            const float dot = 1280f / 720f;
            var text = 13f * dot * SubtitleBox.MinScale;
            var band = (12f + 16f + 4f) * dot + SubtitleBox.BodyEm(SubtitleBox.MaxRows) * text + HudView.SubtitleBottom;
            Assert.That(band / dot, Is.LessThan(405f * 0.5f));
        }
    }
}
