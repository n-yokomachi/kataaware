using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>場面 2 の売り買いで、買い手が視界の左右の外から歩いてきて止まる（<see cref="BuyerWalk"/>）決まり</summary>
    public class BuyerWalkTests
    {
        static readonly Vector3[] Path = { new Vector3(3f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(0f, 0f, -0.5f) };

        [Test]
        public void ThePathLengthAddsUp()
        {
            Assert.AreEqual(2f + Mathf.Sqrt(1.25f), BuyerWalk.Length(Path), 1e-4f);
        }

        [Test]
        public void WalkingAlongThePathEndsAtTheStop()
        {
            Vector3 heading;
            Assert.That(Vector3.Distance(BuyerWalk.Along(Path, 0f, out heading), Path[0]), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(heading, Vector3.left), Is.LessThan(1e-5f), "頭の区間は左へ");
            Assert.That(Vector3.Distance(BuyerWalk.Along(Path, 1f, out heading), new Vector3(2f, 0f, -1f)), Is.LessThan(1e-5f));
            var end = BuyerWalk.Along(Path, BuyerWalk.Length(Path), out heading);
            Assert.That(Vector3.Distance(end, Path[2]), Is.LessThan(1e-4f), "尻で止まる所");
            Assert.That(Vector3.Distance(BuyerWalk.Along(Path, 99f, out heading), Path[2]), Is.LessThan(1e-5f), "越えても止まる所に留まる");
            Assert.That(Vector3.Distance(BuyerWalk.Along(Path, -1f, out heading), Path[0]), Is.LessThan(1e-5f));
        }

        [Test]
        public void SlowsDownBeforeTheStop()
        {
            Assert.AreEqual(1.3f, BuyerWalk.SpeedAt(2f, 1.3f, 0.8f, 0.45f), 1e-5f, "遠いうちは緩めない");
            Assert.AreEqual(1.3f * 0.45f, BuyerWalk.SpeedAt(0f, 1.3f, 0.8f, 0.45f), 1e-5f, "止まる所で一番遅い");
            var was = float.MaxValue;
            for (var d = 0.8f; d >= 0f; d -= 0.05f)
            {
                var v = BuyerWalk.SpeedAt(d, 1.3f, 0.8f, 0.45f);
                Assert.That(v, Is.LessThanOrEqualTo(was + 1e-5f), "近づくほど遅く");
                was = v;
            }
        }

        [Test]
        public void ArrivesInAboutThreeSeconds()
        {
            // 3.8 m を 1.3 m/s、止まる前の 0.8 m で 0.45 倍まで緩める
            var t = BuyerWalk.Seconds(3.8f, 1.3f, 0.8f, 0.45f);
            Assert.That(t, Is.InRange(3.0f, 3.6f));
            Assert.AreEqual(3.8f / 1.3f, BuyerWalk.Seconds(3.8f, 1.3f, 0f, 0.45f), 0.02f, "緩めなければ長さ÷速さ");
        }

        [Test]
        public void AFootLandsWhenTheCyclePassesItsContact()
        {
            Assert.IsTrue(BuyerWalk.Crossed(0.10f, 0.20f, 0.15f));
            Assert.IsFalse(BuyerWalk.Crossed(0.10f, 0.20f, 0.25f));
            Assert.IsTrue(BuyerWalk.Crossed(0.95f, 0.05f, 0.02f), "周期の頭へ戻る");
            Assert.IsTrue(BuyerWalk.Crossed(0.95f, 0.05f, 0.97f), "周期の頭へ戻る前");
            Assert.IsFalse(BuyerWalk.Crossed(0.95f, 0.05f, 0.5f));
            Assert.IsFalse(BuyerWalk.Crossed(0.3f, 0.3f, 0.3f), "進んでいなければ鳴らさない");
        }

        [Test]
        public void TheFeetLandOncePerStep()
        {
            // 左の着地 0.1、右の着地 0.6 の周期を 1 こま 0.07 ずつ 3 周進めると、左右で 3 回ずつ
            var left = 0;
            var right = 0;
            var was = 0f;
            for (var k = 1; k <= 43; k++)
            {
                var now = Mathf.Repeat(k * 0.07f, 1f);
                if (BuyerWalk.Crossed(was, now, 0.1f)) left++;
                if (BuyerWalk.Crossed(was, now, 0.6f)) right++;
                was = now;
            }
            Assert.AreEqual(3, left);
            Assert.AreEqual(3, right);
        }
    }
}
