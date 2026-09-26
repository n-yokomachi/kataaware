using NUnit.Framework;

namespace HalfAware.Tests
{
    public class VillageAmbienceTests
    {
        [Test]
        public void MorningLayersTheVillageOverTheWheat()
        {
            VillageAmbience.Mix(VillageHour.Hour.Morning, 0.6f, 0.5f, 0.7f, out var village, out var wheat);
            Assert.AreEqual(0.6f, village, 1e-6f, "朝は朝の村を鳴らす");
            Assert.AreEqual(0.5f, wheat, 1e-6f, "朝の麦の風は朝の値。夕方の値は使わない");
        }

        [Test]
        public void EveningKeepsOnlyTheWheat()
        {
            VillageAmbience.Mix(VillageHour.Hour.Evening, 0.6f, 0.5f, 0.7f, out var village, out var wheat);
            Assert.AreEqual(0f, village, 1e-6f, "夕方は朝の村を鳴らさない");
            Assert.AreEqual(0.7f, wheat, 1e-6f, "夕方の麦の風は夕方の値");
        }

        [Test]
        public void StartsFromTheOwnersStartingPoint()
        {
            Assert.AreEqual(0.6f, VillageAmbience.DefaultMorningVillage, 1e-6f);
            Assert.AreEqual(0.5f, VillageAmbience.DefaultMorningWheat, 1e-6f);
            Assert.AreEqual(0.6f, VillageAmbience.DefaultEveningWheat, 1e-6f);
        }

        [Test]
        public void EasesAcrossInFollowSeconds()
        {
            // 0 から 1 までを 2 秒で動ききる速さ。0.6 から 0 へは 1.2 秒
            var level = 0.6f;
            level = VillageAmbience.Ease(level, 0f, 0.5f, 2f);
            Assert.AreEqual(0.35f, level, 1e-5f);
            level = VillageAmbience.Ease(level, 0f, 1f, 2f);
            Assert.AreEqual(0f, level, 1e-6f, "行き過ぎない");
            Assert.AreEqual(0.6f, VillageAmbience.Ease(0f, 0.6f, 0.1f, 0f), 1e-6f, "秒数が 0 ならその場で");
        }
    }
}
