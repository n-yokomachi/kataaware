using NUnit.Framework;

namespace HalfAware.Tests
{
    public class FootstepsTests
    {
        [Test]
        public void StandingStillMakesNoSound()
        {
            var walked = 0f;
            Assert.AreEqual(0, Footsteps.Advance(ref walked, 0f));
            Assert.AreEqual(0f, walked, 1e-5f);
        }

        [Test]
        public void HalfAStrideIsNotYetAStep()
        {
            var walked = 0f;
            Assert.AreEqual(0, Footsteps.Advance(ref walked, Footsteps.Stride * 0.5f));
            Assert.AreEqual(Footsteps.Stride * 0.5f, walked, 1e-5f, "残りは持ち越す");
        }

        [Test]
        public void TheRestCarriesOverIntoTheNextStep()
        {
            var walked = 0f;
            Footsteps.Advance(ref walked, Footsteps.Stride * 0.6f);
            Assert.AreEqual(1, Footsteps.Advance(ref walked, Footsteps.Stride * 0.6f));
            Assert.AreEqual(Footsteps.Stride * 0.2f, walked, 1e-4f);
        }

        [Test]
        public void ALongJumpForwardCountsEveryStep()
        {
            var walked = 0f;
            Assert.AreEqual(3, Footsteps.Advance(ref walked, Footsteps.Stride * 3.4f));
            Assert.AreEqual(Footsteps.Stride * 0.4f, walked, 1e-4f);
        }

        [Test]
        public void GoingNowhereBackwardsIsIgnored()
        {
            var walked = 0.3f;
            Assert.AreEqual(0, Footsteps.Advance(ref walked, -2f));
            Assert.AreEqual(0.3f, walked, 1e-5f, "積んだ分は減らさない");
        }

        [Test]
        public void TwoStepsMakeOneWalkCycle()
        {
            Assert.AreEqual(1.76f, Footsteps.Stride * 2f, 1e-3f, "歩きの一巡は 1.76 m");
        }
    }
}
