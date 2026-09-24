using NUnit.Framework;

namespace HalfAware.Tests
{
    public class PitchLimitTests
    {
        [Test]
        public void SheCannotLookFurtherDownThanTheLimit()
        {
            Assert.AreEqual(PlayerController.PitchDownLimit, PlayerController.ClampPitch(60f), 1e-4f);
            Assert.AreEqual(40f, PlayerController.PitchDownLimit, 1e-4f, "胸が画面に入り始める角（立って 47 度）より手前");
        }

        [Test]
        public void LookingUpKeepsItsOldRange()
        {
            Assert.AreEqual(-PlayerController.PitchUpLimit, PlayerController.ClampPitch(-120f), 1e-4f);
            Assert.AreEqual(-50f, PlayerController.ClampPitch(-50f), 1e-4f, "目覚めの天井を仰ぐ角はそのまま");
        }

        [Test]
        public void AnglesInsideAreLeftAlone()
        {
            Assert.AreEqual(25f, PlayerController.ClampPitch(25f), 1e-4f);
            Assert.AreEqual(0f, PlayerController.ClampPitch(0f), 1e-4f);
        }
    }
}
