using NUnit.Framework;

namespace HalfAware.Tests
{
    public class DriveSoundFieldTests
    {
        [Test]
        public void RisesToTheVolumeInRiseSeconds()
        {
            // 0.8 まで 3 秒で上げきる
            var level = DriveSound.Rise(0f, 0.8f, 0.8f, 1.5f, 3f);
            Assert.AreEqual(0.4f, level, 1e-5f, "半分の時に半分");
            level = DriveSound.Rise(level, 0.8f, 0.8f, 2f, 3f);
            Assert.AreEqual(0.8f, level, 1e-6f, "行き過ぎない");
            Assert.AreEqual(0.8f, DriveSound.Rise(0f, 0.8f, 0.8f, 0.02f, 0f), 1e-6f, "秒数が 0 ならその場で");
        }
    }
}
