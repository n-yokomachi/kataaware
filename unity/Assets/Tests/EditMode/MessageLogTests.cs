using NUnit.Framework;

namespace HalfAware.Tests
{
    public class MessageLogTests
    {
        [Test]
        public void ItStartsEmpty()
        {
            var log = new MessageLog();
            Assert.IsTrue(log.IsEmpty);
            Assert.AreEqual(string.Empty, log.Compose(10));
        }

        [Test]
        public void ItKeepsWhatWasSaidInOrder()
        {
            var log = new MessageLog();
            log.Add("一");
            log.Add("二");
            log.Add("三");
            Assert.AreEqual("一\n二\n三", log.Compose(10));
        }

        [Test]
        public void EmptyLinesAreNotKept()
        {
            var log = new MessageLog();
            log.Add("");
            log.Add(null);
            log.Add("これは残る");
            Assert.AreEqual(1, log.Count);
        }

        [Test]
        public void ComposeShowsTheLatestWhenItIsLong()
        {
            var log = new MessageLog();
            for (var i = 0; i < 10; i++) log.Add("行" + i);
            Assert.AreEqual("行7\n行8\n行9", log.Compose(3), "新しい方を残す");
        }

        [Test]
        public void ItForgetsTheOldestPastTheLimit()
        {
            var log = new MessageLog();
            for (var i = 0; i < MessageLog.Keep + 12; i++) log.Add("行" + i);
            Assert.AreEqual(MessageLog.Keep, log.Count);
            Assert.AreEqual("行12", log.Lines[0], "古い方から捨てる");
        }

        [Test]
        public void ManyAtOnceGoInTogether()
        {
            var log = new MessageLog();
            log.AddRange(new[] { "あ", "い" });
            log.AddRange(null);
            Assert.AreEqual(2, log.Count);
        }

        [Test]
        public void ClearingLeavesNothing()
        {
            var log = new MessageLog();
            log.AddRange(new[] { "あ", "い" });
            log.Clear();
            Assert.IsTrue(log.IsEmpty);
        }
    }
}
