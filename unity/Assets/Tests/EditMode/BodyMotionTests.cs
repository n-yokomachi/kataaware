using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class BodyMotionTests
    {
        [Test]
        public void StandingStillReadsAsZero()
        {
            Assert.AreEqual(0f, BodyMotion.GroundSpeed(Vector3.zero), 1e-4f);
        }

        [Test]
        public void FallingAloneIsNotWalking()
        {
            Assert.AreEqual(0f, BodyMotion.GroundSpeed(new Vector3(0f, -9.8f, 0f)), 1e-4f,
                "落ちているだけで歩き出さない");
        }

        [Test]
        public void ATwitchBelowTheThresholdIsStill()
        {
            Assert.AreEqual(0f, BodyMotion.GroundSpeed(new Vector3(0.05f, 0f, 0.05f)), 1e-4f);
        }

        [Test]
        public void WalkingReportsTheHorizontalSpeed()
        {
            var v = new Vector3(0f, -9.8f, 2.6f);
            Assert.AreEqual(2.6f, BodyMotion.GroundSpeed(v), 1e-3f, "縦は数えない");
        }

        [Test]
        public void DiagonalUsesTheLength()
        {
            var v = new Vector3(3f, 0f, 4f);
            Assert.AreEqual(5f, BodyMotion.GroundSpeed(v), 1e-3f);
        }

        [Test]
        public void TheCycleMatchesTheWalkAtItsOwnPace()
        {
            Assert.AreEqual(1f, BodyMotion.Cycle(BodyMotion.NominalWalk), 1e-4f,
                "基準の速さなら等倍で回る");
        }

        [Test]
        public void TheCycleQuickensWithTheStride()
        {
            var slow = BodyMotion.Cycle(0.8f);
            var fast = BodyMotion.Cycle(1.6f);
            Assert.Less(slow, fast);
            Assert.AreEqual(1.6f / 0.8f, fast / slow, 1e-3f, "速さに比例して足を運ぶ");
        }

        [Test]
        public void TheCycleStaysInsideItsBounds()
        {
            Assert.AreEqual(BodyMotion.SlowestCycle, BodyMotion.Cycle(0f), 1e-4f);
            Assert.AreEqual(BodyMotion.FastestCycle, BodyMotion.Cycle(99f), 1e-4f);
        }
    }
}
