using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>ジャケットを着る・脱ぐ間合い（場面 1 で着る、場面 3 でコートハンガーに掛ける）</summary>
    public sealed class JacketBeatsTests
    {
        // JacketOn.wav の長さと、組み立てが入れる既定の値
        const float Sound = 4.77f;
        const float SwapBefore = 0.6f;

        [Test]
        public void TheSwapFallsInsideTheSoundNearItsEnd()
        {
            var beats = new JacketBeats(Sound, SwapBefore);
            Assert.That(beats.SwapAt, Is.EqualTo(Sound - SwapBefore).Within(1e-5f));
            Assert.That(beats.SwapAt, Is.GreaterThan(0f));
            Assert.That(beats.SwapAt, Is.LessThan(Sound));
        }

        [Test]
        public void PuttingOnShowsTheJacketOnlyAfterTheSwap()
        {
            var beats = new JacketBeats(Sound, SwapBefore);
            Assert.That(beats.WornPuttingOn(0f), Is.False);
            Assert.That(beats.WornPuttingOn(beats.SwapAt - 0.01f), Is.False);
            Assert.That(beats.WornPuttingOn(beats.SwapAt), Is.True);
            Assert.That(beats.WornPuttingOn(Sound), Is.True);
        }

        [Test]
        public void TakingOffHidesTheJacketFromTheSwap()
        {
            var beats = new JacketBeats(Sound, SwapBefore);
            Assert.That(beats.WornTakingOff(0f), Is.True);
            Assert.That(beats.WornTakingOff(beats.SwapAt), Is.False);
        }

        [Test]
        public void WithoutASoundSheSwapsAtOnce()
        {
            var beats = new JacketBeats(0f, SwapBefore);
            Assert.That(beats.SwapAt, Is.EqualTo(0f));
            Assert.That(beats.WornTakingOff(0f), Is.False);
            Assert.That(beats.WornPuttingOn(0f), Is.True);
        }
    }
}
