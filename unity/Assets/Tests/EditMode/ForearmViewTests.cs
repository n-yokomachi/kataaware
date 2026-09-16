using NUnit.Framework;

namespace HalfAware.Tests
{
    public class ForearmViewTests
    {
        const float ShowBelow = 35f;
        const float Hysteresis = 8f;

        [Test]
        public void HiddenWhenLookingAhead()
        {
            Assert.IsFalse(ForearmView.ShouldShow(0f, false, ShowBelow, Hysteresis));
        }

        [Test]
        public void HiddenWhenLookingUp()
        {
            Assert.IsFalse(ForearmView.ShouldShow(-60f, false, ShowBelow, Hysteresis));
        }

        [Test]
        public void ShowsOnceThePitchPassesTheThreshold()
        {
            Assert.IsTrue(ForearmView.ShouldShow(36f, false, ShowBelow, Hysteresis));
        }

        [Test]
        public void StaysUpUntilWellBackAboveTheThreshold()
        {
            Assert.IsTrue(ForearmView.ShouldShow(30f, true, ShowBelow, Hysteresis));
            Assert.IsFalse(ForearmView.ShouldShow(26f, true, ShowBelow, Hysteresis));
        }

        [Test]
        public void DoesNotFlickerRightAtTheThreshold()
        {
            // しきい値の上で視線が細かく揺れても、切り替わるのは最初の 1 回だけ
            var shown = false;
            var flips = 0;
            for (var i = 0; i < 20; i++)
            {
                var next = ForearmView.ShouldShow(ShowBelow + (i % 2 == 0 ? 0.01f : -0.01f), shown, ShowBelow, Hysteresis);
                if (next != shown) flips++;
                shown = next;
            }
            Assert.AreEqual(1, flips, "揺れのたびに点いたり消えたりしない");
            Assert.IsTrue(shown, "一度出たら出たまま");
        }

        [Test]
        public void ComesBackDownWhenHeLooksUpProperly()
        {
            var shown = ForearmView.ShouldShow(40f, false, ShowBelow, Hysteresis);
            Assert.IsTrue(shown);
            shown = ForearmView.ShouldShow(10f, shown, ShowBelow, Hysteresis);
            Assert.IsFalse(shown, "しっかり顔を上げれば消える");
        }

        [Test]
        public void ZeroHysteresisStillWorks()
        {
            Assert.IsTrue(ForearmView.ShouldShow(36f, false, ShowBelow, 0f));
            Assert.IsFalse(ForearmView.ShouldShow(34f, true, ShowBelow, 0f));
        }
    }
}
