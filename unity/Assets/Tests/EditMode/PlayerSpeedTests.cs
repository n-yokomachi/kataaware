using NUnit.Framework;

namespace HalfAware.Tests
{
    public class PlayerSpeedTests
    {
        [Test]
        public void ShiftMakesHerFaster()
        {
            Assert.AreEqual(PlayerController.WalkSpeed, PlayerController.Speed(false), 1e-4f);
            Assert.AreEqual(PlayerController.RunSpeed, PlayerController.Speed(true), 1e-4f);
            Assert.Greater(PlayerController.Speed(true), PlayerController.Speed(false));
        }

        // 場面 4 は記憶ごとに借りる体が違う。倍率は基準の速さに掛かる
        [Test]
        public void TheBorrowedBodyScalesTheWalk()
        {
            Assert.AreEqual(PlayerController.WalkSpeed * 0.6f, PlayerController.Speed(false, 0.6f), 1e-4f);
            Assert.AreEqual(PlayerController.RunSpeed * 1.6f, PlayerController.Speed(true, 1.6f), 1e-4f);
        }

        // 一覧に倍率を書き忘れると 0 になる。そのまま掛けると一歩も歩けない
        [Test]
        public void AMissingScaleFallsBackToTheBaseSpeed()
        {
            Assert.AreEqual(PlayerController.WalkSpeed, PlayerController.Speed(false, 0f), 1e-4f);
            Assert.AreEqual(PlayerController.WalkSpeed, PlayerController.Speed(false, -1f), 1e-4f);
        }

        [Test]
        public void RunningIsNotSoFastItBreaksTheRoom()
        {
            // 自室は 6 メートル四方。走っても 2 秒で横断しない速さに留める
            Assert.Less(PlayerController.RunSpeed, 4f);
            Assert.Greater(PlayerController.RunSpeed, PlayerController.WalkSpeed * 1.5f);
        }
    }
}
