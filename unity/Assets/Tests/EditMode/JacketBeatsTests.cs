using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>ジャケットを着る・脱ぐ間合い（場面 1 で着る、場面 3 で脱いでから座る）</summary>
    public sealed class JacketBeatsTests
    {
        // JacketOn.wav の長さと、組み立てが入れる既定の値
        const float Sound = 4.77f;
        const float SwapBefore = 0.6f;
        const float SitSeconds = 1.4f;

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

        // 場面 3: 座るのは脱いだ後。脱ぐ前に腰が沈み始めてはいけない
        [Test]
        public void SheSitsOnlyAfterTheJacketIsOff()
        {
            var beats = new JacketBeats(Sound, SwapBefore);
            for (var t = 0f; t <= beats.Seated(SitSeconds) + 0.5f; t += 0.01f)
            {
                if (beats.Sit(t, SitSeconds) > 0f)
                    Assert.That(beats.WornTakingOff(t), Is.False, t + " 秒目に、まだ着たまま腰を下ろし始めている");
            }
        }

        [Test]
        public void SittingStartsWhenTheSoundEndsAndFinishesAfterSitSeconds()
        {
            var beats = new JacketBeats(Sound, SwapBefore);
            Assert.That(beats.Sit(Sound - 0.01f, SitSeconds), Is.EqualTo(0f));
            Assert.That(beats.Sit(Sound + SitSeconds * 0.5f, SitSeconds), Is.InRange(0.01f, 0.99f));
            Assert.That(beats.Sit(beats.Seated(SitSeconds), SitSeconds), Is.EqualTo(1f));
            Assert.That(beats.Seated(SitSeconds), Is.EqualTo(Sound + SitSeconds).Within(1e-5f));
        }

        [Test]
        public void WithoutASoundSheSwapsAtOnceAndStillSitsAfterward()
        {
            var beats = new JacketBeats(0f, SwapBefore);
            Assert.That(beats.SwapAt, Is.EqualTo(0f));
            Assert.That(beats.WornTakingOff(0f), Is.False);
            Assert.That(beats.Sit(0f, SitSeconds), Is.EqualTo(0f));
            Assert.That(beats.Sit(SitSeconds, SitSeconds), Is.EqualTo(1f));
        }
    }
}
