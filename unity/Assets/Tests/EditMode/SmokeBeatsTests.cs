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
        public void AShortSmokeHasNoDrags()
        {
            Assert.AreEqual(0, SmokeBeats.Drags(0.5f));
            Assert.AreEqual(0, SmokeBeats.Drags(SmokeBeats.BlowAt(0) + SmokeBeats.BlowSeconds - 0.01f));
        }

        [Test]
        public void ALongerSmokeFitsMore()
        {
            // 境目そのものは浮動小数の丸めで揺れるので、わずかに内側で見る
            Assert.AreEqual(1, SmokeBeats.Drags(SmokeBeats.BlowAt(0) + SmokeBeats.BlowSeconds + 1e-3f));
            Assert.AreEqual(2, SmokeBeats.Drags(SmokeBeats.BlowAt(1) + SmokeBeats.BlowSeconds + 1e-3f));
            Assert.GreaterOrEqual(SmokeBeats.Drags(60f), 4, "1 分なら何服も入る");
        }

        [Test]
        public void TheCountNeverFalls()
        {
            var last = 0;
            for (var s = 0f; s < 30f; s += 0.25f)
            {
                var n = SmokeBeats.Drags(s);
                Assert.GreaterOrEqual(n, last);
                last = n;
            }
        }
    }
}
