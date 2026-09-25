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
            v.Tick(1f, true, false, true);
            Assert.AreEqual(SeatVisit.Phase.Away, v.Now);
            Assert.AreEqual(0f, v.Seat);
            Assert.IsFalse(v.Locked);
            Assert.IsFalse(v.Frozen(true, false));
        }

        [Test]
        public void GoesToTheSeatInGoSecondsWithSoftEnds()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            Assert.AreEqual(SeatVisit.Phase.Going, v.Now);
            Assert.AreEqual(0f, v.Seat, 1e-5f);
            Assert.IsTrue(v.Frozen(true, false), "移している間は字幕を送らせない");
            Assert.IsTrue(v.Locked);
            v.Tick(Go * 0.1f, true, false, false);
            Assert.Less(v.Seat, 0.1f, "動き出しはなだらか");
            v.Tick(Go * 0.4f, true, false, false);
            Assert.AreEqual(0.5f, v.Seat, 1e-4f, "半分の時に半分");
            v.Tick(Go * 0.5f + 1e-3f, true, false, false);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now);
            Assert.AreEqual(1f, v.Seat);
        }

        [Test]
        public void ReadsWhileTalkingAndUntilTheReflectionIsGone()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go + 0.01f, true, false, false);
            v.Tick(5f, true, false, true);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now);
            Assert.IsFalse(v.Frozen(true, false), "読んでいる間は字幕を送れる");
            Assert.IsTrue(v.Locked, "読んでいる間も見回しと歩きは止める");
            v.Tick(0.5f, false, false, true);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now, "映り込みが消えるまでは戻らない");
            Assert.IsTrue(v.Frozen(false, false), "字幕が切れた後は、ほかの対象を選ばせない");
            v.Tick(0.1f, false, false, false);
            Assert.AreEqual(SeatVisit.Phase.Leaving, v.Now);
        }

        [Test]
        public void StaysSeatedThroughTheChoiceAndTheLinesAfterYes()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go + 0.01f, true, false, false);
            // 独白を読み終えて二択が出た。映り込みは二択の間に消える
            v.Tick(0.3f, false, true, true);
            v.Tick(5f, false, true, false);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now, "二択の間は戻らない");
            Assert.AreEqual(1f, v.Seat);
            Assert.IsFalse(v.Frozen(false, true), "二択の間は止めない（止めると答えられない）");
            // 「はい」: 解除の文が続く
            v.Tick(0.1f, true, false, false);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now, "解除の文も座ったまま読む");
            Assert.IsFalse(v.Frozen(true, false));
            v.Tick(0.1f, false, false, false);
            Assert.AreEqual(SeatVisit.Phase.Leaving, v.Now, "解除の文を読み終えたら戻る");
        }

        [Test]
        public void ComesBackWhenTheChoiceIsDeclined()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go + 0.01f, true, false, false);
            v.Tick(2f, false, true, false);
            // 「いいえ」: 二択を閉じて、続く文は無い
            v.Tick(0.02f, false, false, false);
            Assert.AreEqual(SeatVisit.Phase.Leaving, v.Now, "二択を閉じたところで戻る");
        }

        [Test]
        public void WaitsForTheReflectionAfterTheChoiceIsDeclined()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go + 0.01f, true, false, false);
            // すぐに「いいえ」と答えて、映り込みがまだ消えきっていない
            v.Tick(0.1f, false, true, true);
            v.Tick(0.02f, false, false, true);
            Assert.AreEqual(SeatVisit.Phase.Reading, v.Now, "映り込みが消えるまでは戻らない");
            Assert.IsTrue(v.Frozen(false, false), "待つ間は、ほかの対象を選ばせない");
            v.Tick(0.5f, false, false, false);
            Assert.AreEqual(SeatVisit.Phase.Leaving, v.Now);
        }

        [Test]
        public void ComesBackInBackSecondsAndFrees()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go + 0.01f, true, false, false);
            v.Tick(0.1f, false, false, false);
            Assert.AreEqual(SeatVisit.Phase.Leaving, v.Now);
            Assert.AreEqual(1f, v.Seat, 1e-5f);
            Assert.IsTrue(v.Frozen(false, false));
            Assert.IsTrue(v.Frozen(false, true), "戻している間は二択が出ていても止める");
            v.Tick(Back * 0.5f, false, false, false);
            Assert.AreEqual(0.5f, v.Seat, 1e-4f);
            v.Tick(Back * 0.5f + 1e-3f, false, false, false);
            Assert.AreEqual(SeatVisit.Phase.Away, v.Now);
            Assert.AreEqual(0f, v.Seat);
            Assert.IsFalse(v.Locked);
        }

        [Test]
        public void StartIsIgnoredWhileBusy()
        {
            var v = new SeatVisit(Go, Back);
            v.Start();
            v.Tick(Go * 0.5f, true, false, false);
            var seat = v.Seat;
            v.Start();
            Assert.AreEqual(SeatVisit.Phase.Going, v.Now);
            Assert.AreEqual(seat, v.Seat, 1e-6f, "移している途中で頭へ戻らない");
        }
    }
}
