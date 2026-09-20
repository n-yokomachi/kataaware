using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class ScreenBootTests
    {
        [Test]
        public void TheScreenIsDarkBeforeItStarts()
        {
            Assert.AreEqual(0f, ScreenBoot.Level(0f), 1e-4f);
            Assert.AreEqual(0f, ScreenBoot.Level(-1f), 1e-4f);
        }

        [Test]
        public void TheScreenIsFullAtTheEnd()
        {
            Assert.AreEqual(1f, ScreenBoot.Level(ScreenBoot.Total), 1e-4f);
            Assert.AreEqual(1f, ScreenBoot.Level(ScreenBoot.Total + 5f), 1e-4f);
        }

        [Test]
        public void TheLevelNeverLeavesNoughtToOne()
        {
            for (var t = 0f; t <= ScreenBoot.Total + 0.5f; t += 0.01f)
            {
                var v = ScreenBoot.Level(t);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void ItBlinksTwiceBeforeItSettles()
        {
            // 瞬きの間は暗いところへ何度も落ちる。落ちた回数で数える
            var dips = 0;
            var lit = false;
            for (var t = 0f; t < ScreenBoot.FlickerSeconds; t += 0.005f)
            {
                var on = ScreenBoot.Level(t) > 0.5f;
                if (on && !lit) dips++;
                lit = on;
            }
            Assert.AreEqual(2, dips);
        }

        [Test]
        public void ItRisesWithoutFallingOnceTheFlickerIsOver()
        {
            var last = ScreenBoot.Level(ScreenBoot.FlickerSeconds);
            for (var t = ScreenBoot.FlickerSeconds; t <= ScreenBoot.Total; t += 0.01f)
            {
                var now = ScreenBoot.Level(t);
                Assert.GreaterOrEqual(now, last - 1e-4f);
                last = now;
            }
        }
    }
}
