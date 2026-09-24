using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SeatVisitTests
    {
        const float Go = 1.8f;
        const float Back = 1.6f;

        [Test]
        public void StaysAwayUntilStarted()
        {
            var v = new SeatVisit(Go, Back);
            v.Tick(1f, true, true);
            Assert.AreEqual(SeatVisit.Phase.Away, v.Now);
            Assert.AreEqual(0f, v.Seat);
            Assert.IsFalse(v.Locked);
            Assert.IsFalse(v.Frozen(true));
        }

        [Test]
        public void GoesToTheSeatInGoSecondsWithSoftEnds()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            Assert.AreEqual(SeatVisit.Phase.Going, v.Now);
            Assert.AreEqual(0f, v.Seat, 1e-5f);
            Assert.IsTrue(v.Frozen(true), "移している間は字幕を送らせない");
            Assert.IsTrue(v.Locked);
            v.Tick(Go * 0.1f, true, false);
            Assert.Less(v.Seat, 0.1f, "動き出しはなだらか");
            v.Tick(Go * 0.4f, true, false);
            Assert.AreEqual(0.5f, v.Seat, 1e-4f, "半分の時に半分");
            v.Tick(Go * 0.5f + 1e-3f, true, false);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now);
            Assert.AreEqual(1f, v.Seat);
        }

        [Test]
        public void ReadsWhileTalkingAndUntilTheReflectionIsGone()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go + 0.01f, true, false);
            v.Tick(5f, true, true);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now);
            Assert.IsFalse(v.Frozen(true), "読んでいる間は字幕を送れる");
            Assert.IsTrue(v.Locked, "読んでいる間も見回しと歩きは止める");
            v.Tick(0.5f, false, true);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now, "映り込みが消えるまでは戻らない");
            Assert.IsTrue(v.Frozen(false), "字幕が切れた後は、ほかの対象を選ばせない");
            v.Tick(0.1f, false, false);
            Assert.AreEqual(SeatVisit.Phase.Leaving, v.Now);
        }

        [Test]
        public void ComesBackInBackSecondsAndFrees()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go + 0.01f, true, false);
            v.Tick(0.1f, false, false);
            Assert.AreEqual(SeatVisit.Phase.Leaving, v.Now);
            Assert.AreEqual(1f, v.Seat, 1e-5f);
            Assert.IsTrue(v.Frozen(false));
            v.Tick(Back * 0.5f, false, false);
            Assert.AreEqual(0.5f, v.Seat, 1e-4f);
            v.Tick(Back * 0.5f + 1e-3f, false, false);
            Assert.AreEqual(SeatVisit.Phase.Away, v.Now);
            Assert.AreEqual(0f, v.Seat);
            Assert.IsFalse(v.Locked);
        }

        [Test]
        public void StartIsIgnoredWhileBusy()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go * 0.5f, true, false);
            var seat = v.Seat;
            v.Start();
            Assert.AreEqual(SeatVisit.Phase.Going, v.Now);
            Assert.AreEqual(seat, v.Seat, 1e-6f, "移している途中で頭へ戻らない");
        }
    }
}
