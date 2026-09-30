using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SmokePuffsTests
    {
        [Test]
        public void NothingBurnsBeforeItIsLit()
        {
            Assert.IsFalse(SmokePuffs.Lit(12f, -1f, 8f), "火を点けていない");
        }

        [Test]
        public void ItSmokesWhileItLasts()
        {
            Assert.IsTrue(SmokePuffs.Lit(10f, 10f, 8f));
            Assert.IsTrue(SmokePuffs.Lit(17.9f, 10f, 8f));
        }

        [Test]
        public void ItStopsAtTheEnd()
        {
            Assert.IsFalse(SmokePuffs.Lit(18f, 10f, 8f));
            Assert.IsFalse(SmokePuffs.Lit(40f, 10f, 8f));
        }

        [Test]
        public void NotBeforeItStarted()
        {
            Assert.IsFalse(SmokePuffs.Lit(9.9f, 10f, 8f));
        }

        // ---- 吸い終えた後に立ち続けた煙を、細くして止める（場面 1、2026-09-29） ----------------

        [Test]
        public void TheLingeringSmokeThinsOutRatherThanStoppingAtOnce()
        {
            Assert.AreEqual(1f, SmokePuffs.Thin(0f, 4f), 1e-4f, "始めは今のまま");
            Assert.AreEqual(0.5f, SmokePuffs.Thin(2f, 4f), 1e-4f);
            Assert.AreEqual(0f, SmokePuffs.Thin(4f, 4f), 1e-4f, "秒が来たら 0");
            Assert.AreEqual(0f, SmokePuffs.Thin(9f, 4f), 1e-4f);
            // なだらかに減る（両端が緩い）
            Assert.Greater(SmokePuffs.Thin(0.4f, 4f), 0.97f);
            Assert.Less(SmokePuffs.Thin(3.6f, 4f), 0.03f);
            var last = 1f;
            for (var t = 0f; t <= 4f; t += 0.25f)
            {
                var k = SmokePuffs.Thin(t, 4f);
                Assert.LessOrEqual(k, last + 1e-6f, "戻らない");
                last = k;
            }
        }

        [Test]
        public void ZeroSecondsStopsAtOnce()
        {
            Assert.AreEqual(0f, SmokePuffs.Thin(0f, 0f), 1e-4f);
        }

        [Test]
        public void ThePuffsGetThinnerAsTheyFade()
        {
            Assert.AreEqual(1f, SmokePuffs.Size(1f), 1e-4f);
            Assert.AreEqual(0.5f, SmokePuffs.Size(0f), 1e-4f, "消える前は半分の太さ");
            Assert.AreEqual(0.75f, SmokePuffs.Size(0.5f), 1e-4f);
        }
    }
}
