using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>場面 2 の群衆のその場の動き（<see cref="CrowdIdle"/>）と歩く人（<see cref="CrowdWalk"/>）の決まり</summary>
    public class CrowdMotionTests
    {
        [Test]
        public void BreathRisesAndFallsWithinOne()
        {
            for (var t = 0f; t < 20f; t += 0.37f)
                Assert.That(Mathf.Abs(CrowdIdle.Breath(t, 4.2f)), Is.LessThanOrEqualTo(1f));
            Assert.AreEqual(CrowdIdle.Breath(1.3f, 4.2f), CrowdIdle.Breath(1.3f + 4.2f, 4.2f), 1e-4f, "周期で戻る");
            Assert.AreEqual(0f, CrowdIdle.Breath(3f, 0f), "周期が 0 なら動かない");
        }

        [Test]
        public void TheHeadHoldsAndThenMoves()
        {
            // 周期の頭の方は止まっていて、終わりの move 割だけで次の向きへ移る
            const float period = 7f;
            const float move = 0.3f;
            var still = CrowdIdle.Shift(0.1f, period, 3, move);
            Assert.AreEqual(still, CrowdIdle.Shift(period * 0.6f, period, 3, move), 1e-5f, "移る前は止まっている");
            Assert.AreNotEqual(still, CrowdIdle.Shift(period * 0.95f, period, 3, move), "終わりの方で移る");
            Assert.AreEqual(CrowdIdle.Shift(period * 1.1f, period, 3, move), CrowdIdle.Shift(period * 0.999f, period, 3, move), 0.02f, "移りきった向きから次の周期が始まる");
            for (var t = 0f; t < 60f; t += 0.23f)
                Assert.That(Mathf.Abs(CrowdIdle.Shift(t, period, 3, move)), Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void EachPersonLooksSomewhereElse()
        {
            Assert.AreEqual(CrowdIdle.Hash(5, 2), CrowdIdle.Hash(5, 2), "毎回同じ");
            Assert.AreNotEqual(CrowdIdle.Hash(5, 2), CrowdIdle.Hash(6, 2));
            Assert.AreNotEqual(CrowdIdle.Hash(5, 2), CrowdIdle.Hash(5, 3));
            var low = 1f;
            var high = -1f;
            for (var i = 0; i < 200; i++)
            {
                var h = CrowdIdle.Hash(i, 7);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
            Assert.That(low, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(high, Is.LessThanOrEqualTo(1f));
            Assert.That(high - low, Is.GreaterThan(1.5f), "-1〜1 に散らばる");
        }

        [Test]
        public void TheGestureComesHoldsAndGoes()
        {
            const float period = 10f, move = 0.7f, hold = 1.5f;
            Assert.AreEqual(0f, CrowdIdle.Envelope(0f, period, move, hold), 1e-5f);
            Assert.AreEqual(1f, CrowdIdle.Envelope(move + hold * 0.5f, period, move, hold), 1e-5f, "形で止まる");
            Assert.AreEqual(0f, CrowdIdle.Envelope(move * 2f + hold + 1f, period, move, hold), 1e-5f, "戻ってからは焼いた姿勢");
            Assert.AreEqual(CrowdIdle.Envelope(1.2f, period, move, hold), CrowdIdle.Envelope(1.2f + period, period, move, hold), 1e-5f, "周期で繰り返す");
            for (var t = 0f; t < 30f; t += 0.11f)
            {
                var w = CrowdIdle.Envelope(t, period, move, hold);
                Assert.That(w, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void ItBakesTenTimesASecond()
        {
            Assert.AreEqual(10f, CrowdIdle.Fps);
            Assert.IsFalse(CrowdIdle.Due(0.05f));
            Assert.IsTrue(CrowdIdle.Due(0.1f));
        }

        [Test]
        public void ThePathLengthAddsUp()
        {
            var path = new[] { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) };
            Assert.AreEqual(7f, CrowdWalk.Length(path), 1e-5f);
        }

        [Test]
        public void TheWalkerStopsOnlyForSomeoneInTheWay()
        {
            var at = Vector3.zero;
            var heading = Vector3.forward;
            Assert.IsTrue(CrowdWalk.Blocked(at, heading, new Vector3(0.3f, 0f, 1.0f), 1.5f), "進む先の 1 m、横へ 0.3 m");
            Assert.IsFalse(CrowdWalk.Blocked(at, heading, new Vector3(0f, 0f, -1.0f), 1.5f), "後ろ");
            Assert.IsFalse(CrowdWalk.Blocked(at, heading, new Vector3(1.2f, 0f, 1.0f), 1.5f), "横へ 1.2 m は塞がない");
            Assert.IsFalse(CrowdWalk.Blocked(at, heading, new Vector3(0f, 0f, 2.0f), 1.5f), "1.5 m より先");
            Assert.IsFalse(CrowdWalk.Blocked(at, Vector3.zero, new Vector3(0f, 0f, 1.0f), 1.5f), "止まっている時は向きが無い");
        }
    }
}
