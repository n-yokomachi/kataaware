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

        [Test]
        public void RunningIsNotSoFastItBreaksTheRoom()
        {
            // 自室は 6 メートル四方。走っても 2 秒で横断しない速さに留める
            Assert.Less(PlayerController.RunSpeed, 4f);
            Assert.Greater(PlayerController.RunSpeed, PlayerController.WalkSpeed * 1.5f);
        }
    }
}
