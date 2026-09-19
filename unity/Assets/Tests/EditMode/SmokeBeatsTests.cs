using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SmokeBeatsTests
    {
        [Test]
        public void TheLidOpensBeforeTheFlame()
        {
            Assert.Less(SmokeBeats.ClickAt, SmokeBeats.FlameAt);
            Assert.Less(SmokeBeats.FlameAt, SmokeBeats.FirstDragAt, "火が点いてから吸う");
        }

        [Test]
        public void SheBreathesInBeforeSheBreathesOut()
        {
            for (var i = 0; i < 4; i++)
                Assert.Less(SmokeBeats.DragAt(i), SmokeBeats.BlowAt(i));
        }

        [Test]
        public void TheDragsComeOneCycleApart()
        {
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.DragAt(2) - SmokeBeats.DragAt(1), 1e-4f);
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.BlowAt(3) - SmokeBeats.BlowAt(2), 1e-4f);
        }

        [Test]
        public void OneBlowFinishesBeforeTheNextDrag()
        {
            Assert.LessOrEqual(SmokeBeats.BlowAt(0) + SmokeBeats.BlowSeconds, SmokeBeats.DragAt(1),
                "吐き終わってから次を吸う");
        }

        [Test]
        public void TheCardComesJustAfterSheBreathesOut()
        {
            for (var i = 0; i < SmokeBeats.Drags; i++)
            {
                Assert.AreEqual(SmokeBeats.CardAfterBlow, SmokeBeats.CardAt(i) - SmokeBeats.BlowAt(i), 1e-4f);
                Assert.Less(SmokeBeats.CardAt(i), SmokeBeats.BlowAt(i) + SmokeBeats.BlowSeconds,
                    "まだ吐いている最中に暗くなる");
            }
        }

        [Test]
        public void TheWholeThingCoversEveryDrag()
        {
            var total = SmokeBeats.Total(SmokeBeats.Drags);
            var last = SmokeBeats.BlowAt(SmokeBeats.Drags - 1) + SmokeBeats.BlowSeconds;
            Assert.Greater(total, last, "最後に吐き終わってからも間がある");
            Assert.AreEqual(SmokeBeats.TailSeconds, total - last, 1e-4f);
        }

        [Test]
        public void NoDragsMeansNothingToWaitFor()
        {
            Assert.AreEqual(SmokeBeats.FirstDragAt, SmokeBeats.Total(0), 1e-4f);
        }

        [Test]
        public void MoreDragsTakeLonger()
        {
            Assert.Greater(SmokeBeats.Total(3), SmokeBeats.Total(2));
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.Total(3) - SmokeBeats.Total(2), 1e-3f);
        }

        [Test]
        public void SheSmokesThreeTimes()
        {
            Assert.AreEqual(3, SmokeBeats.Drags);
        }
    }
}
