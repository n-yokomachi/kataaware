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
    }
}
