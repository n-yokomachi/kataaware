using NUnit.Framework;

namespace HalfAware.Tests
{
    public class HeadTurnTests
    {
        static HeadTurn Seated()
        {
            return new HeadTurn { Limit = 90f };
        }

        [Test]
        public void SeatedTheHeadTakesTheTurn()
        {
            var h = Seated();
            Assert.IsTrue(h.Add(30f), "座っている間は首が受ける");
            Assert.AreEqual(30f, h.Yaw, 1e-4f);
        }

        [Test]
        public void TheHeadWillNotGoPastTheLimitEitherWay()
        {
            var h = Seated();
            for (var i = 0; i < 20; i++) h.Add(20f);
            Assert.AreEqual(90f, h.Yaw, 1e-4f);
            for (var i = 0; i < 40; i++) h.Add(-20f);
            Assert.AreEqual(-90f, h.Yaw, 1e-4f);
        }

        [Test]
        public void WithNoLimitTheBodyTakesIt()
        {
            var h = new HeadTurn();
            Assert.IsFalse(h.Add(30f), "制限が無ければ呼び手が体を回す");
            Assert.AreEqual(0f, h.Yaw, 1e-4f);
        }

        [Test]
        public void SettingDirectlyStaysInsideTheLimit()
        {
            var h = Seated();
            h.Set(140f);
            Assert.AreEqual(90f, h.Yaw, 1e-4f);
            h.Set(-140f);
            Assert.AreEqual(-90f, h.Yaw, 1e-4f);
            h.Set(12f);
            Assert.AreEqual(12f, h.Yaw, 1e-4f);
        }

        [Test]
        public void ReleasingHandsTheTurnOverAndStopsLimiting()
        {
            var h = Seated();
            h.Add(48f);
            var carried = h.Release();
            Assert.AreEqual(48f, carried, 1e-4f, "溜めた分を体へ渡す");
            Assert.AreEqual(0f, h.Yaw, 1e-4f);
            Assert.IsFalse(h.Limited);
            Assert.IsFalse(h.Add(30f), "解いた後は体が回る");
        }

        [Test]
        public void HalfATurnMeansNinetyEachWay()
        {
            Assert.AreEqual(90f, HeadTurn.DefaultLimit, 1e-4f, "左右あわせて 180 度");
        }
    }
}
