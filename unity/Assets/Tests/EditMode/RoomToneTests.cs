using NUnit.Framework;

namespace HalfAware.Tests
{
    public class RoomToneTests
    {
        [Test]
        public void KeepsTheVolumeWhileTheSceneIsGoing()
        {
            Assert.AreEqual(0.5f, RoomTone.Target(0.5f, false, 0f), 1e-6f);
            Assert.AreEqual(0.5f, RoomTone.Target(0.5f, false, 1f), 1e-6f, "場面の頭の黒からの明けには合わせない");
        }

        [Test]
        public void FollowsTheFadeWhenTheSceneCloses()
        {
            Assert.AreEqual(0.5f, RoomTone.Target(0.5f, true, 0f), 1e-6f, "暗転が始まるまではそのまま");
            Assert.AreEqual(0.25f, RoomTone.Target(0.5f, true, 0.5f), 1e-6f);
            Assert.AreEqual(0f, RoomTone.Target(0.5f, true, 1f), 1e-6f, "黒くなりきったら消える");
        }

        [Test]
        public void EasesDownInFollowSecondsEvenOnACut()
        {
            // 黒へ切り替えても、ぷつりと落とさず follow 秒かけて絞る
            var level = 0.5f;
            level = RoomTone.Ease(level, 0f, 0.5f, 0.15f, 0.3f);
            Assert.AreEqual(0.25f, level, 1e-5f, "半分の時に半分");
            level = RoomTone.Ease(level, 0f, 0.5f, 0.2f, 0.3f);
            Assert.AreEqual(0f, level, 1e-6f, "行き過ぎない");
            Assert.AreEqual(0.3f, RoomTone.Ease(0.5f, 0.3f, 0.5f, 1f, 0f), 1e-6f, "秒数が 0 ならその場で");
        }
    }
}
