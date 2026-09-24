using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class CablePathTests
    {
        static Vector3[] Run(Vector3 a, Vector3 b, float length, int points)
        {
            var into = new Vector3[points];
            CablePath.Sag(a, b, length, into);
            return into;
        }

        [Test]
        public void TheEndsStayWhereTheyAre()
        {
            var p = Run(new Vector3(1f, 2f, 3f), new Vector3(-1f, 0f, 1f), 9f, 8);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), p[0]);
            Assert.AreEqual(new Vector3(-1f, 0f, 1f), p[7]);
        }

        [Test]
        public void PulledTautItRunsStraight()
        {
            var a = Vector3.zero;
            var b = new Vector3(1f, 0f, 0f);
            var p = Run(a, b, 1f, 5);
            for (var i = 0; i < p.Length; i++)
                Assert.AreEqual(0f, p[i].y, 1e-5f, "張りきっていれば垂れない");
        }

        [Test]
        public void SlackHangsBelowTheLine()
        {
            var a = Vector3.zero;
            var b = new Vector3(1f, 0f, 0f);
            var p = Run(a, b, 1.4f, 9);
            Assert.Less(p[4].y, -0.05f, "真ん中が下がる");
        }

        [Test]
        public void MoreSlackHangsDeeper()
        {
            var a = Vector3.zero;
            var b = new Vector3(1f, 0f, 0f);
            var shallow = Run(a, b, 1.2f, 9)[4].y;
            var deep = Run(a, b, 1.8f, 9)[4].y;
            Assert.Less(deep, shallow);
        }

        [Test]
        public void StretchedBeyondItsLengthItDoesNotBowUpwards()
        {
            var p = Run(Vector3.zero, new Vector3(3f, 0f, 0f), 1f, 7);
            for (var i = 0; i < p.Length; i++) Assert.AreEqual(0f, p[i].y, 1e-5f);
        }

        [Test]
        public void ASinglePointIsJustTheStart()
        {
            var p = Run(new Vector3(2f, 1f, 0f), Vector3.zero, 5f, 1);
            Assert.AreEqual(new Vector3(2f, 1f, 0f), p[0]);
        }

        [Test]
        public void TheCurveKeepsItsEnds()
        {
            var into = new Vector3[12];
            var a = new Vector3(0f, 0f, 0f);
            var b = new Vector3(0.3f, 0.2f, 0.1f);
            CablePath.Curve(a, Vector3.up, b, Vector3.forward, 0.6f, 0.05f, into);
            Assert.AreEqual(0f, Vector3.Distance(a, into[0]), 1e-5f);
            Assert.AreEqual(0f, Vector3.Distance(b, into[11]), 1e-5f);
        }

        [Test]
        public void TheCurveLeavesEachEndAlongItsAxis()
        {
            var into = new Vector3[40];
            var a = Vector3.zero;
            var b = new Vector3(0.4f, 0f, 0f);
            // 張りきった長さにして、垂れを掛けずに向きだけを見る
            CablePath.Curve(a, Vector3.up, b, Vector3.up, 0.1f, 0.08f, into);
            var first = (into[1] - into[0]).normalized;
            var last = (into[38] - into[39]).normalized;
            Assert.Greater(Vector3.Dot(first, Vector3.up), 0.7f, "差込口からは軸の向きへ出る");
            Assert.Greater(Vector3.Dot(last, Vector3.up), 0.7f, "ジャックの尻からも軸の向きへ出る");
        }

        [Test]
        public void TheReelAlwaysLeavesTheSameSlack()
        {
            var near = new Vector3[17];
            var far = new Vector3[17];
            // 端から互いの向きへ出すと、垂れる前の道筋は両端を結ぶまっすぐな線になる
            var b1 = new Vector3(0.12f, 0f, 0f);
            var b2 = new Vector3(0.6f, 0f, 0f);
            CablePath.Reel(Vector3.zero, Vector3.right, b1, Vector3.left, 0.03f, 0.03f, near);
            CablePath.Reel(Vector3.zero, Vector3.right, b2, Vector3.left, 0.03f, 0.03f, far);
            var want = 0.03f * CablePath.SagShare;
            Assert.AreEqual(want, -near[8].y, 1e-4f, "端が近くても余りのぶんだけ垂れる");
            Assert.AreEqual(want, -far[8].y, 1e-4f, "端が遠くても同じだけ垂れる");
        }
    }
}
