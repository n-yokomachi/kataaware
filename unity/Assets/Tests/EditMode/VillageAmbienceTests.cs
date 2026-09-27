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
            Assert.AreEqual(0.4f, VillageAmbience.DefaultMorningVillage, 1e-6f, "鳥の声は風とのバランスを見て抑えめにした（オーナー、2026-09-27）");
            Assert.AreEqual(0.5f, VillageAmbience.DefaultMorningWheat, 1e-6f);
            Assert.AreEqual(0.6f, VillageAmbience.DefaultEveningWheat, 1e-6f);
        }

        [Test]
        public void TheMorningWheatFadesAlongTheLaneAndComesBack()
        {
            const float from = VillageAmbience.DefaultWheatFadeFrom;
            const float to = VillageAmbience.DefaultWheatFadeTo;
            Assert.AreEqual(1f, VillageAmbience.Reach(-76.3f, from, to), 1e-6f, "車を降りた所はそのまま");
            Assert.AreEqual(1f, VillageAmbience.Reach(from, from, to), 1e-6f, "薄れ始めまではそのまま");
            Assert.AreEqual(0.5f, VillageAmbience.Reach((from + to) * 0.5f, from, to), 1e-5f, "間の真ん中で半分");
            Assert.AreEqual(0f, VillageAmbience.Reach(to, from, to), 1e-6f, "消えきる所から先は鳴らさない");
            Assert.AreEqual(0f, VillageAmbience.Reach(-4.2f, from, to), 1e-6f, "片割れの家の前でも鳴らさない");
            var there = VillageAmbience.Reach(-40f, from, to);
            VillageAmbience.Reach(-10f, from, to);
            Assert.AreEqual(there, VillageAmbience.Reach(-40f, from, to), 1e-6f, "立ち位置だけで決まる。戻れば同じ大きさ");
            Assert.AreEqual(VillageAmbience.Reach(-40f, from, to), VillageAmbience.Reach(-40f, to, from), 1e-6f, "端の順が逆でも同じ向き");
        }

        [Test]
        public void TheFadeStartsSoonAfterLeavingTheCarAndEndsAtTheFirstHouse()
        {
            // 車を降りた所（BuildVillage.ArriveAt.x -76.3）から歩き出してすぐ薄れ始め、
            // いちばん手前の家（家 C、路地の南の x -52.6〜-42.4）の西の端で消えきる
            const float arriveX = -76.3f;
            const float houseCWest = -52.6f;
            Assert.That(VillageAmbience.DefaultWheatFadeFrom, Is.GreaterThan(arriveX), "降りた所よりは東");
            Assert.That(VillageAmbience.DefaultWheatFadeFrom, Is.LessThan(houseCWest), "いちばん手前の家より手前");
            Assert.That(VillageAmbience.DefaultWheatFadeFrom - arriveX, Is.LessThan(10f), "歩き出してすぐ薄れ始める");
            Assert.AreEqual(houseCWest, VillageAmbience.DefaultWheatFadeTo, 1e-6f, "いちばん手前の家にたどり着く頃には消えきる");
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
