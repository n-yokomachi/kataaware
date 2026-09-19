using NUnit.Framework;

namespace HalfAware.Tests
{
    public class DriveRouteTests
    {
        static DriveBand Band(string name, string trigger)
        {
            return new DriveBand
            {
                name = name,
                trigger = trigger,
                speed = 22f,
                rough = 1f,
                afterglow = 5f,
                black = 0.8f,
                fadeIn = 1.4f,
            };
        }

        [Test]
        public void AnEmptyRouteHasNoBands()
        {
            var route = new DriveRoute(null);
            Assert.AreEqual(0, route.Count);
            Assert.IsNull(route.At(0).trigger, "範囲の外は空の帯");
        }

        [Test]
        public void ItKeepsTheBandsInOrder()
        {
            var route = new DriveRoute(new[] { Band("夜", "a"), Band("朝", "b") });
            Assert.AreEqual(2, route.Count);
            Assert.AreEqual("夜", route.At(0).name);
            Assert.AreEqual("朝", route.At(1).name);
        }

        [Test]
        public void TheLastBandHasNothingAfterIt()
        {
            var route = new DriveRoute(new[] { Band("夜", "a"), Band("朝", "b") });
            Assert.IsFalse(route.IsLast(0));
            Assert.IsTrue(route.IsLast(1), "最後の帯の次は無い");
        }

        [Test]
        public void OutOfRangeIsTreatedAsTheLast()
        {
            var route = new DriveRoute(new[] { Band("夜", "a") });
            Assert.IsTrue(route.IsLast(9), "範囲を外れても落ちない");
            Assert.IsTrue(route.IsLast(-1));
        }

        [Test]
        public void ItFindsWhichBandATriggerBelongsTo()
        {
            var route = new DriveRoute(new[] { Band("夜", "a"), Band("朝", "b") });
            Assert.AreEqual(1, route.BandOf("b"));
            Assert.AreEqual(-1, route.BandOf("c"), "どの帯のきっかけでもない");
            Assert.AreEqual(-1, route.BandOf(null));
        }
    }
}
