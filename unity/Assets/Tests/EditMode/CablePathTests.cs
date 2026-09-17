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
    }
}
