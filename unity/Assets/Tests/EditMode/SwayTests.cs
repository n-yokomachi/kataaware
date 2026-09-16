using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class SwayTests
    {
        const float Shift = 0.03f;
        const float Tilt = 1f;

        [Test]
        public void StaysStillWithoutStrength()
        {
            var sway = new Sway();
            sway.Tick(12.3f, 0f, Shift, Tilt);
            Assert.That(sway.Offset, Is.EqualTo(Vector3.zero));
            Assert.That(sway.Tilt, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TreatsNegativeStrengthAsStill()
        {
            var sway = new Sway();
            sway.Tick(4f, -3f, Shift, Tilt);
            Assert.That(sway.Offset, Is.EqualTo(Vector3.zero));
            Assert.That(sway.Tilt, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void NeverLeavesTheGivenAmplitude()
        {
            var sway = new Sway();
            for (var t = 0f; t < 120f; t += 0.05f)
            {
                sway.Tick(t, 1f, Shift, Tilt);
                Assert.That(Mathf.Abs(sway.Offset.x), Is.LessThanOrEqualTo(Shift + 1e-4f), "t=" + t);
                Assert.That(Mathf.Abs(sway.Offset.y), Is.LessThanOrEqualTo(Shift + 1e-4f), "t=" + t);
                Assert.That(Mathf.Abs(sway.Tilt.x), Is.LessThanOrEqualTo(Tilt + 1e-4f), "t=" + t);
                Assert.That(Mathf.Abs(sway.Tilt.y), Is.LessThanOrEqualTo(Tilt + 1e-4f), "t=" + t);
            }
        }

        [Test]
        public void NeverMovesForwardOrBack()
        {
            var sway = new Sway();
            sway.Tick(7.5f, 1f, Shift, Tilt);
            Assert.That(sway.Offset.z, Is.EqualTo(0f));
        }

        [Test]
        public void KeepsMovingAsTimePasses()
        {
            var sway = new Sway();
            sway.Tick(0f, 1f, Shift, Tilt);
            var first = sway.Offset;
            sway.Tick(1.5f, 1f, Shift, Tilt);
            Assert.That(sway.Offset, Is.Not.EqualTo(first));
        }

        [Test]
        public void ScalesWithStrength()
        {
            var full = new Sway();
            full.Tick(3.3f, 1f, Shift, Tilt);
            var half = new Sway();
            half.Tick(3.3f, 0.5f, Shift, Tilt);
            Assert.That(half.Offset.x, Is.EqualTo(full.Offset.x * 0.5f).Within(1e-5f));
            Assert.That(half.Tilt.y, Is.EqualTo(full.Tilt.y * 0.5f).Within(1e-5f));
        }

        [Test]
        public void ReachesEnoughOfTheAmplitudeToBeSeen()
        {
            // 振れ幅の半分以上に届く時刻があること。小さすぎて見えない動きにならないための歯止め
            var sway = new Sway();
            var peak = 0f;
            for (var t = 0f; t < 60f; t += 0.05f)
            {
                sway.Tick(t, 1f, Shift, Tilt);
                peak = Mathf.Max(peak, Mathf.Abs(sway.Offset.y));
            }
            Assert.That(peak, Is.GreaterThan(Shift * 0.5f));
        }
    }
}
