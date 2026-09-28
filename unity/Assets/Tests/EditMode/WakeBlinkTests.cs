using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>場面 1 の冒頭の明け（黒のまま置き、少し開きかけて閉じ、一拍おいてからゆっくり明けきる）</summary>
    public class WakeBlinkTests
    {
        // RoomIntroDirector の既定の値
        static WakeBlink Make()
        {
            return new WakeBlink(5f, 0.9f, 0.35f, 0.25f, 0.55f, 0.8f, 2.6f);
        }

        [Test]
        public void ItStaysBlackWhileOnlyTheBreathIsHeard()
        {
            var b = Make();
            Assert.AreEqual(1f, b.Level(0f), 1e-5f);
            Assert.AreEqual(1f, b.Level(2.5f), 1e-5f);
            Assert.AreEqual(1f, b.Level(4.99f), 1e-5f, "5 秒ほどは暗転のまま");
        }

        [Test]
        public void ItOpensOnlyALittleAndClosesAgain()
        {
            var b = Make();
            var least = 1f;
            for (var t = b.PeekAt; t < b.OpenAt; t += 0.01f) least = System.Math.Min(least, b.Level(t));
            Assert.AreEqual(0.65f, least, 1e-3f, "開きかけても 0.65 の濃さまで");
            Assert.AreEqual(0.65f, b.Level(b.PeekAt + b.Peek + b.Hold * 0.5f), 1e-4f, "開きかけたまま少し止まる");
            Assert.AreEqual(1f, b.Level(b.ShutAt + b.Shut + b.Pause * 0.5f), 1e-4f, "閉じて一拍おく間は真っ黒");
        }

        [Test]
        public void ThenItOpensSlowlyAllTheWay()
        {
            var b = Make();
            var last = 1f;
            for (var t = b.OpenAt; t <= b.Total; t += 0.05f)
            {
                var now = b.Level(t);
                Assert.LessOrEqual(now, last + 1e-5f, "明ける間は濃くならない");
                last = now;
            }
            Assert.AreEqual(0f, b.Level(b.Total), 1e-5f);
            Assert.AreEqual(0f, b.Level(b.Total + 10f), 1e-5f);
            Assert.IsTrue(b.Done(b.Total));
            Assert.IsFalse(b.Done(b.Total - 0.01f));
        }

        [Test]
        public void TheBlinkTakesAboutFiveSecondsAfterTheDark()
        {
            var b = Make();
            Assert.AreEqual(5f, b.PeekAt, 1e-5f);
            Assert.AreEqual(5.1f, b.Total - b.PeekAt, 1e-4f);
        }

        [Test]
        public void ZeroSecondsJumpStraightThrough()
        {
            var b = new WakeBlink(0f, 0f, 0.5f, 0f, 0f, 0f, 0f);
            Assert.AreEqual(0f, b.Total, 1e-5f);
            Assert.AreEqual(0f, b.Level(0f), 1e-5f, "何も置かなければ、はじめから明けている");
            Assert.IsTrue(b.Done(0f));
        }

        [Test]
        public void NegativeSecondsAndGlimpseAreClamped()
        {
            var b = new WakeBlink(-1f, -1f, 2f, -1f, -1f, -1f, -1f);
            Assert.AreEqual(0f, b.Total, 1e-5f);
            Assert.AreEqual(1f, b.Glimpse, 1e-5f);
        }
    }
}
