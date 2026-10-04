using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class MirrorSmokingTests
    {
        [Test]
        public void TheCigaretteRestsOnTheLipsWhileDragging()
        {
            for (var u = MirrorSmoking.LipsAt; u < MirrorSmoking.LeaveAt; u += 0.1f)
                Assert.AreEqual(1f, MirrorSmoking.At(u).lips, 1e-5f, "時刻 " + u);
            for (var u = MirrorSmoking.AsideAt; u < MirrorSmoking.Period; u += 0.1f)
                Assert.AreEqual(0f, MirrorSmoking.At(u).lips, 1e-5f, "時刻 " + u);
        }

        [Test]
        public void TheHandMovesWithoutJumps()
        {
            var last = MirrorSmoking.At(0f).lips;
            for (var t = 0.02f; t < MirrorSmoking.Period * 2f; t += 0.02f)
            {
                var now = MirrorSmoking.At(t).lips;
                Assert.Less(Mathf.Abs(now - last), 0.05f, "時刻 " + t);
                last = now;
            }
        }

        [Test]
        public void TheEmberGlowsOnlyWhileDragging()
        {
            Assert.Greater(MirrorSmoking.At((MirrorSmoking.LipsAt + MirrorSmoking.LeaveAt) * 0.5f).glow, 1.5f, "吸っている間は強まる");
            Assert.AreEqual(1f, MirrorSmoking.At(MirrorSmoking.AsideAt + 1f).glow, 1e-5f, "脇に持っている間は戻る");
        }

        [Test]
        public void TheBreathComesOutAfterTheCigaretteLeavesTheLips()
        {
            Assert.GreaterOrEqual(MirrorSmoking.BreathFrom, MirrorSmoking.LeaveAt, "吸い終えてから吐く");
            Assert.LessOrEqual(MirrorSmoking.BreathTo, MirrorSmoking.Period, "一巡りの中で吐き終える");
            for (var u = 0f; u < MirrorSmoking.Period; u += 0.05f)
            {
                var b = MirrorSmoking.At(u).breath;
                if (u < MirrorSmoking.BreathFrom || u >= MirrorSmoking.BreathTo) Assert.AreEqual(0f, b, 1e-5f, "時刻 " + u);
                else Assert.GreaterOrEqual(b, 0f, "時刻 " + u);
            }
            Assert.Greater(MirrorSmoking.At((MirrorSmoking.BreathFrom + MirrorSmoking.BreathTo) * 0.5f).breath, 0.5f);
        }

        [Test]
        public void TheCycleRepeats()
        {
            for (var t = 0f; t < MirrorSmoking.Period; t += 0.37f)
            {
                var a = MirrorSmoking.At(t);
                var b = MirrorSmoking.At(t + MirrorSmoking.Period * 3f);
                Assert.AreEqual(a.lips, b.lips, 1e-4f);
                Assert.AreEqual(a.glow, b.glow, 1e-4f);
                Assert.AreEqual(a.breath, b.breath, 1e-4f);
            }
        }

        [Test]
        public void TheReflectionStartsWithTheCigaretteAsideAndTheSmokeAlreadyDrifting()
        {
            var start = MirrorSmoking.At(MirrorSmoking.StartAt);
            Assert.AreEqual(0f, start.lips, 1e-5f, "出始めは脇に持っている");
            Assert.AreEqual(0f, start.breath, 1e-5f, "出始めに吐いてはいない");
            Assert.Greater(MirrorSmoking.History, 6f, "前の一巡りの煙が漂っている形から");
            Assert.Less(MirrorSmoking.Period - MirrorSmoking.StartAt, 2f, "出てから 2 秒以内に唇へ運び始める");
        }
    }
}
