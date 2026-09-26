using NUnit.Framework;

namespace HalfAware.Tests
{
    public class ArrivalClockTests
    {
        /// <summary>止まる音 11.4 秒、走行音を 5.7 秒で下げきる、間 0.6 秒、ドアから村まで 2 秒</summary>
        static ArrivalTimes Times()
        {
            return new ArrivalTimes { stop = 11.4f, settle = 5.7f, gap = 0.6f, hold = 2f };
        }

        /// <summary>seconds 秒ぶん、細かく刻んで進める</summary>
        static void Run(ArrivalClock clock, ArrivalTimes times, float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += 0.1f) clock.Tick(0.1f, times);
        }

        [Test]
        public void NothingHappensBeforeTheBlack()
        {
            var clock = new ArrivalClock();
            Run(clock, Times(), 600f);
            Assert.AreEqual(ArrivalBeat.Waiting, clock.Beat, "余韻が明けるまで何も始めない");
            Assert.IsFalse(clock.TakeStop());
            Assert.IsFalse(clock.TakeDoor());
            Assert.AreEqual(1f, clock.Level(Times()), "走行音はそのまま");
        }

        [Test]
        public void TheStopSoundPlaysOnceAtTheBlack()
        {
            var clock = new ArrivalClock();
            clock.Begin();
            Assert.AreEqual(ArrivalBeat.Stopping, clock.Beat);
            Assert.IsTrue(clock.Underway);
            Assert.IsTrue(clock.TakeStop(), "黒へ入ったその場で鳴らす");
            Assert.IsFalse(clock.TakeStop(), "二度は鳴らさない");
            Assert.IsFalse(clock.TakeDoor(), "ドアはまだ");
        }

        [Test]
        public void BeginningTwiceDoesNotRestart()
        {
            var clock = new ArrivalClock();
            clock.Begin();
            clock.TakeStop();
            Run(clock, Times(), 5f);
            clock.Begin();
            Assert.IsFalse(clock.TakeStop(), "止まる音を鳴らし直さない");
            Assert.AreEqual(Times().stop + Times().gap + Times().hold - 5f, clock.Left(Times()), 1e-3f);
        }

        [Test]
        public void TheDoorWaitsForTheStopSoundAndTheGap()
        {
            var clock = new ArrivalClock();
            clock.Begin();
            clock.TakeStop();
            Run(clock, Times(), 11.3f);
            Assert.AreEqual(ArrivalBeat.Stopping, clock.Beat, "止まる音が鳴り終わるまで待つ");
            Run(clock, Times(), 0.2f);
            Assert.AreEqual(ArrivalBeat.Pause, clock.Beat);
            Assert.IsFalse(clock.TakeDoor(), "間のうちはまだ閉めない");
            Run(clock, Times(), 0.6f);
            Assert.AreEqual(ArrivalBeat.Door, clock.Beat);
            Assert.IsTrue(clock.TakeDoor(), "間が明けたら閉める");
            Assert.IsFalse(clock.TakeDoor(), "二度は鳴らさない");
        }

        [Test]
        public void TheVillageComesTwoSecondsAfterTheDoor()
        {
            var times = new ArrivalTimes { stop = 1f, settle = 1f, gap = 0f, hold = 2f };
            var clock = new ArrivalClock();
            clock.Begin();
            clock.Tick(1f, times);
            Assert.AreEqual(ArrivalBeat.Door, clock.Beat, "間が 0 なら止まる音の終わりにそのまま閉める");
            Assert.IsTrue(clock.TakeDoor());
            clock.Tick(1.99f, times);
            Assert.AreEqual(ArrivalBeat.Door, clock.Beat);
            Assert.AreEqual(0.01f, clock.Left(times), 1e-4f);
            clock.Tick(0.01f, times);
            Assert.AreEqual(ArrivalBeat.Arrived, clock.Beat);
            Assert.IsFalse(clock.Underway);
            Assert.AreEqual(0f, clock.Left(times));
        }

        [Test]
        public void TimeLeftCountsDownToTheVillage()
        {
            var times = Times();
            var clock = new ArrivalClock();
            Assert.AreEqual(14f, clock.Left(times), 1e-4f, "始まる前は一連の長さ");
            clock.Begin();
            Assert.AreEqual(14f, clock.Left(times), 1e-4f);
            Run(clock, times, 11.7f);
            Assert.AreEqual(ArrivalBeat.Pause, clock.Beat);
            Assert.AreEqual(2.3f, clock.Left(times), 1e-3f);
            Run(clock, times, 2.4f);
            Assert.AreEqual(ArrivalBeat.Arrived, clock.Beat);
            Assert.AreEqual(0f, clock.Left(times));
        }

        [Test]
        public void AllZeroArrivesInOneTick()
        {
            var times = new ArrivalTimes();
            var clock = new ArrivalClock();
            clock.Begin();
            clock.Tick(0f, times);
            Assert.AreEqual(ArrivalBeat.Arrived, clock.Beat, "秒数が 0 でも詰まらない");
            Assert.IsTrue(clock.TakeStop(), "止まる音は鳴らす");
            Assert.IsTrue(clock.TakeDoor(), "ドアも鳴らす");
            Assert.AreEqual(0f, clock.Level(times), "下げる秒数が 0 ならその場で消す");
        }

        [Test]
        public void NegativeSecondsLendNothing()
        {
            var times = new ArrivalTimes { stop = -3f, settle = 1f, gap = -1f, hold = 2f };
            var clock = new ArrivalClock();
            clock.Begin();
            clock.Tick(0.5f, times);
            Assert.AreEqual(ArrivalBeat.Door, clock.Beat);
            Assert.AreEqual(1.5f, clock.Left(times), 1e-4f, "打ち間違いの負の数で後ろの段が延びも縮みもしない");
        }

        [Test]
        public void RoadAndWindSettleSmoothly()
        {
            var times = Times();
            var clock = new ArrivalClock();
            clock.Begin();
            Assert.AreEqual(1f, clock.Level(times), 1e-5f, "黒へ入った瞬間は元のまま");
            clock.Tick(0.1f, times);
            Assert.Greater(clock.Level(times), 0.99f, "頭は緩める");
            clock.Tick(2.75f, times);
            Assert.AreEqual(0.5f, clock.Level(times), 1e-3f, "半分の時に半分");
            clock.Tick(2.75f, times);
            Assert.Less(clock.Level(times), 0.01f, "尻も緩める");
            clock.Tick(0.2f, times);
            Assert.AreEqual(0f, clock.Level(times), "下げきったら 0 のまま");
        }
    }
}
