using NUnit.Framework;

namespace HalfAware.Tests
{
    public class DazeTests
    {
        [Test]
        public void StartsClear()
        {
            var daze = new Daze();
            Assert.That(daze.Blur, Is.EqualTo(0f));
            Assert.That(daze.Wobble, Is.EqualTo(0f));
            Assert.That(daze.IsClear, Is.True);
        }

        [Test]
        public void HoldKeepsTheStrengthAcrossTicks()
        {
            var daze = new Daze();
            daze.Hold(1f, 0.5f);
            daze.Tick(10f);
            Assert.That(daze.Blur, Is.EqualTo(1f));
            Assert.That(daze.Wobble, Is.EqualTo(0.5f));
            Assert.That(daze.IsClear, Is.False);
        }

        [Test]
        public void DecayFallsLinearlyToZero()
        {
            var daze = new Daze();
            daze.Decay(1f, 0.4f, 4f);
            Assert.That(daze.Blur, Is.EqualTo(1f));
            daze.Tick(1f);
            Assert.That(daze.Blur, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(daze.Wobble, Is.EqualTo(0.3f).Within(1e-4f));
            daze.Tick(2f);
            Assert.That(daze.Blur, Is.EqualTo(0.25f).Within(1e-4f));
            daze.Tick(1f);
            Assert.That(daze.IsClear, Is.True);
        }

        [Test]
        public void DecayStaysAtZeroAfterTheEnd()
        {
            var daze = new Daze();
            daze.Decay(1f, 1f, 2f);
            daze.Tick(5f);
            daze.Tick(5f);
            Assert.That(daze.Blur, Is.EqualTo(0f));
            Assert.That(daze.Wobble, Is.EqualTo(0f));
        }

        [Test]
        public void DecayWithoutTimeClearsAtOnce()
        {
            var daze = new Daze();
            daze.Hold(1f, 1f);
            daze.Decay(1f, 1f, 0f);
            Assert.That(daze.IsClear, Is.True);
        }

        [Test]
        public void HoldAfterDecayStopsTheFall()
        {
            var daze = new Daze();
            daze.Decay(1f, 1f, 4f);
            daze.Tick(2f);
            daze.Hold(1f, 1f);
            daze.Tick(10f);
            Assert.That(daze.Blur, Is.EqualTo(1f));
        }

        [Test]
        public void ClearStopsEverything()
        {
            var daze = new Daze();
            daze.Decay(1f, 1f, 4f);
            daze.Clear();
            Assert.That(daze.IsClear, Is.True);
            daze.Tick(1f);
            Assert.That(daze.IsClear, Is.True);
        }
    }
}
