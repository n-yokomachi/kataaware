using NUnit.Framework;

namespace HalfAware.Tests
{
    public class StandUpTests
    {
        static StandUp Seated() => new StandUp(1.1f, 1.6f, 0.6f);

        [Test]
        public void StartsSeated()
        {
            var s = Seated();
            Assert.That(s.EyeHeight, Is.EqualTo(1.1f));
            Assert.That(s.Standing, Is.False);
        }

        [Test]
        public void StaysSeatedUntilReleased()
        {
            var s = Seated();
            s.Tick(1f, false, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.1f));
            Assert.That(s.Standing, Is.False);
        }

        [Test]
        public void DoesNotStartWhileFrozen()
        {
            var s = Seated();
            s.Tick(1f, true, true);
            Assert.That(s.EyeHeight, Is.EqualTo(1.1f));
            Assert.That(s.Standing, Is.False);
        }

        [Test]
        public void RisesOverTheGivenSeconds()
        {
            var s = Seated();
            s.Tick(0.3f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.35f).Within(1e-4f));
            Assert.That(s.Standing, Is.False);
            s.Tick(0.3f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(s.Standing, Is.True);
        }

        [Test]
        public void KeepsRisingOnceStartedEvenIfFrozen()
        {
            var s = Seated();
            s.Tick(0.3f, true, false);
            s.Tick(0.3f, true, true);
            Assert.That(s.Standing, Is.True);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f).Within(1e-4f));
        }

        [Test]
        public void DoesNotOvershoot()
        {
            var s = Seated();
            s.Tick(10f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f));
            s.Tick(10f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f));
        }

        [Test]
        public void RisesAtOnceWithoutTime()
        {
            var s = new StandUp(1.1f, 1.6f, 0f);
            s.Tick(0.016f, true, false);
            Assert.That(s.EyeHeight, Is.EqualTo(1.6f));
            Assert.That(s.Standing, Is.True);
        }
        [Test]
        public void TheProgressFollowsTheEyeHeight()
        {
            Assert.AreEqual(0f, StandUp.Progress(1.1f, 1.1f, 1.6f), 1e-4f);
            Assert.AreEqual(1f, StandUp.Progress(1.6f, 1.1f, 1.6f), 1e-4f);
            Assert.AreEqual(0.5f, StandUp.Progress(1.35f, 1.1f, 1.6f), 1e-4f);
        }

        [Test]
        public void TheProgressStaysInsideItsRange()
        {
            Assert.AreEqual(0f, StandUp.Progress(0.4f, 1.1f, 1.6f), 1e-4f);
            Assert.AreEqual(1f, StandUp.Progress(9f, 1.1f, 1.6f), 1e-4f);
        }

        [Test]
        public void NoHeightToGainIsAlreadyThere()
        {
            Assert.AreEqual(1f, StandUp.Progress(1.6f, 1.6f, 1.6f), 1e-4f);
        }
    }
}
