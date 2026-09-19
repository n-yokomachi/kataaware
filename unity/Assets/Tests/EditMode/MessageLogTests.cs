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
            Assert.AreEqual(string.Empty, log.Compose(10, 0));
        }

        [Test]
        public void TheNewestComesFirstWithABlankLineBetween()
        {
            var log = new MessageLog();
            log.Add("一");
            log.Add("二");
            log.Add("三");
            Assert.AreEqual("三\n\n二\n\n一", log.Compose(10, 0));
        }

        [Test]
        public void GoingBackSkipsTheNewestOnes()
        {
            var log = new MessageLog();
            for (var i = 0; i < 6; i++) log.Add("行" + i);
            Assert.AreEqual("行3\n\n行2", log.Compose(2, 2), "新しい方から 2 つ飛ばす");
        }

        [Test]
        public void ItSaysHowManyOlderOnesAreLeft()
        {
            var log = new MessageLog();
            for (var i = 0; i < 10; i++) log.Add("行" + i);
            Assert.AreEqual(7, log.Older(3, 0));
            Assert.AreEqual(5, log.Older(3, 2));
            Assert.AreEqual(0, log.Older(20, 0), "全部出ていれば残りは無い");
        }

        [Test]
        public void GoingBackPastTheOldestStops()
        {
            var log = new MessageLog();
            log.Add("一");
            log.Add("二");
            Assert.AreEqual("一", log.Compose(5, 99), "範囲を外れても落ちない");
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
            Assert.AreEqual("行9\n\n行8\n\n行7", log.Compose(3, 0), "新しい方を残す");
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
