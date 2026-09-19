using NUnit.Framework;

namespace HalfAware.Tests
{
    public class ChoiceTests
    {
        [Test]
        public void ItOpensOnYes()
        {
            var c = new Choice("チップを抜く");
            Assert.AreEqual(0, c.Index);
            Assert.IsTrue(c.Accepted);
        }

        [Test]
        public void RightMovesToNoAndLeftComesBack()
        {
            var c = new Choice("チップを抜く");
            c.Move(1);
            Assert.IsFalse(c.Accepted);
            c.Move(-1);
            Assert.IsTrue(c.Accepted);
        }

        [Test]
        public void ItStopsAtBothEnds()
        {
            var c = new Choice("問い");
            c.Move(-1);
            Assert.AreEqual(0, c.Index);
            c.Move(1);
            c.Move(1);
            c.Move(1);
            Assert.AreEqual(1, c.Index);
        }

        [Test]
        public void TheQuestionSitsAboveTheTwoOptions()
        {
            var text = Choice.Compose("チップを抜く", 0);
            Assert.AreEqual(2, SubtitleBox.LineCount(text), "問いと二択で 2 行");
            StringAssert.StartsWith("チップを抜く", text);
            StringAssert.Contains(Choice.Yes, text);
            StringAssert.Contains(Choice.No, text);
        }

        [Test]
        public void TheCursorSitsOnWhatIsChosen()
        {
            var yes = Choice.Compose("問い", 0);
            var no = Choice.Compose("問い", 1);
            StringAssert.Contains(Choice.Cursor + Choice.Yes, yes);
            StringAssert.Contains(Choice.Cursor + Choice.No, no);
            Assert.IsFalse(yes.Contains(Choice.Cursor + Choice.No));
            Assert.IsFalse(no.Contains(Choice.Cursor + Choice.Yes));
        }

        [Test]
        public void WithNoQuestionOnlyTheOptionsShow()
        {
            var text = Choice.Compose("", 0);
            Assert.AreEqual(1, SubtitleBox.LineCount(text));
        }
    }
}
