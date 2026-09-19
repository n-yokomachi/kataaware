using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class RainCoverTests
    {
        static Bounds[] Roof()
        {
            return new[] { new Bounds(new Vector3(0f, 3f, 0f), new Vector3(4f, 6f, 4f)) };
        }

        [Test]
        public void UnderTheRoofIsCovered()
        {
            Assert.That(RainCover.Covered(new Vector3(0f, 1.6f, 0f), Roof()), Is.True);
        }

        [Test]
        public void OutsideTheRoofIsNot()
        {
            Assert.That(RainCover.Covered(new Vector3(9f, 1.6f, 0f), Roof()), Is.False);
            Assert.That(RainCover.Covered(new Vector3(0f, 1.6f, 9f), Roof()), Is.False);
        }

        [Test]
        public void NoRoofAtAllIsNotCovered()
        {
            Assert.That(RainCover.Covered(Vector3.zero, null), Is.False);
            Assert.That(RainCover.Covered(Vector3.zero, new Bounds[0]), Is.False);
        }

        [Test]
        public void SoundStaysFullOutside()
        {
            Assert.That(RainCover.Volume(0.30f, 0.35f, 0f), Is.EqualTo(0.30f).Within(1e-5f));
        }

        [Test]
        public void SoundDropsUnderTheRoof()
        {
            Assert.That(RainCover.Volume(0.30f, 0.35f, 1f), Is.EqualTo(0.105f).Within(1e-5f));
        }

        [Test]
        public void SoundSlidesBetweenTheTwo()
        {
            var half = RainCover.Volume(0.30f, 0.35f, 0.5f);
            Assert.That(half, Is.GreaterThan(0.105f));
            Assert.That(half, Is.LessThan(0.30f));
        }

        [Test]
        public void StepsOutsideTheRangeAreHeldIn()
        {
            Assert.That(RainCover.Volume(0.30f, 0.35f, -1f), Is.EqualTo(0.30f).Within(1e-5f));
            Assert.That(RainCover.Volume(0.30f, 0.35f, 2f), Is.EqualTo(0.105f).Within(1e-5f));
        }
    }
}
