using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>場面 4 の板の `潜る`・`切断` を、ホイールと上下の矢印で選ぶ</summary>
    public sealed class DiveWheelTests
    {
        const int Dive = 0;
        const int Cut = 1;

        [Test]
        public void TheWheelGivesOneStepEitherWay()
        {
            Assert.AreEqual(1, PlayerController.WheelStep(120f), "奥へ一刻み（Windows の量）");
            Assert.AreEqual(1, PlayerController.WheelStep(1f), "奥へ一刻み（揃えた量）");
            Assert.AreEqual(-1, PlayerController.WheelStep(-120f), "手前へ一刻み");
            Assert.AreEqual(0, PlayerController.WheelStep(0f));
            Assert.AreEqual(0, PlayerController.WheelStep(0.005f), "かすかな揺れは数えない");
        }

        [Test]
        public void OneNotchDownGoesToCutAndOneUpComesBack()
        {
            var last = 0;
            var at = DiveDirector.Pick(Dive, -1, ref last);
            Assert.AreEqual(Cut, at, "手前へ一刻みで下の `切断`");
            at = DiveDirector.Pick(at, 0, ref last);
            Assert.AreEqual(Cut, at, "回していない間は動かない");
            at = DiveDirector.Pick(at, 1, ref last);
            Assert.AreEqual(Dive, at, "奥へ一刻みで上の `潜る`");
        }

        [Test]
        public void ANotchSpreadOverFramesMovesOnlyOnce()
        {
            var last = 0;
            var at = DiveDirector.Pick(Cut, 1, ref last);
            Assert.AreEqual(Dive, at);
            // 同じ刻みの続きが次のフレームにも来る。その間に選びが外から戻されても、刻みの続きでは動かさない
            Assert.AreEqual(Cut, DiveDirector.Pick(Cut, 1, ref last));
            Assert.AreEqual(Cut, DiveDirector.Pick(Cut, 0, ref last));
            Assert.AreEqual(Dive, DiveDirector.Pick(Cut, 1, ref last), "止まってから回し直せば動く");
        }

        [Test]
        public void TheGuideTellsTheWheel()
        {
            StringAssert.Contains("ホイール", DiveDirector.Axis);
            StringAssert.Contains("↑↓", DiveDirector.Axis);
        }
    }
}
