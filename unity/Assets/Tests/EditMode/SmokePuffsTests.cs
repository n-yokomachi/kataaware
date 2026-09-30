using NUnit.Framework;
using UnityEngine;

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

        // ---- 一筋を流す空気の流れ（場面 1、2026-09-29） ----------------------------------

        [Test]
        public void TheDraftStaysWithinItsSwing()
        {
            for (var t = 0f; t < 400f; t += 0.7f)
            {
                var d = SmokePuffs.Draft(t);
                Assert.That(d.x, Is.InRange(-28f, 28f), "向きの揺らぎは左右 28 度まで");
                Assert.That(d.y, Is.InRange(0.7f, 1.3f), "強さの揺らぎは 3 割まで");
            }
        }

        [Test]
        public void TheDraftChangesSlowly()
        {
            // 毎こまでがくがく振れない（0.1 秒で 1 度未満）
            for (var t = 0f; t < 200f; t += 1.3f)
            {
                Assert.Less(Mathf.Abs(SmokePuffs.Draft(t + 0.1f).x - SmokePuffs.Draft(t).x), 1f);
                Assert.Less(Mathf.Abs(SmokePuffs.Draft(t + 0.1f).y - SmokePuffs.Draft(t).y), 0.02f);
            }
        }

        [Test]
        public void TheDraftTurnsOverTime()
        {
            // 同じ向きのままではない（時間がたつと左右へ振れる）
            float lo = 99f, hi = -99f;
            for (var t = 0f; t < 120f; t += 0.5f)
            {
                var x = SmokePuffs.Draft(t).x;
                lo = Mathf.Min(lo, x);
                hi = Mathf.Max(hi, x);
            }
            Assert.Greater(hi - lo, 10f);
            Assert.AreEqual(SmokePuffs.Draft(37.2f), SmokePuffs.Draft(37.2f), "同じ時刻なら同じ流れ");
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
