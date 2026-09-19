using NUnit.Framework;

namespace HalfAware.Tests
{
    public class BandClockTests
    {
        static DriveBand Band()
        {
            return new DriveBand
            {
                name = "夜の高速",
                trigger = "drive.log",
                speed = 22f,
                rough = 1f,
                afterglow = 5f,
                black = 0.8f,
                fadeIn = 1.4f,
            };
        }

        /// <summary>seconds 秒ぶん、細かく刻んで進める</summary>
        static void Run(BandClock clock, DriveBand band, float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.1f) clock.Tick(0.1f, band);
        }

        [Test]
        public void ItStartsByJustDriving()
        {
            var clock = new BandClock();
            Assert.AreEqual(DriveBeat.Running, clock.Beat);
        }

        [Test]
        public void TimeAloneNeverEndsTheBand()
        {
            var clock = new BandClock();
            Run(clock, Band(), 600f);
            Assert.AreEqual(DriveBeat.Running, clock.Beat, "調べるまで何時間でも走っていられる");
        }

        [Test]
        public void ExaminingTheTriggerStartsTheTalking()
        {
            var clock = new BandClock();
            clock.Trigger();
            Assert.AreEqual(DriveBeat.Talking, clock.Beat);
        }

        [Test]
        public void TalkingDoesNotTimeOut()
        {
            var clock = new BandClock();
            clock.Trigger();
            Run(clock, Band(), 600f);
            Assert.AreEqual(DriveBeat.Talking, clock.Beat, "読む速さはプレイヤー次第");
        }

        [Test]
        public void FinishingTheLinesOpensTheAfterglow()
        {
            var clock = new BandClock();
            clock.Trigger();
            clock.Spoken();
            Assert.AreEqual(DriveBeat.Afterglow, clock.Beat, "送り切ってもすぐには暗転しない");
        }

        [Test]
        public void TheAfterglowHoldsForItsSeconds()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 4.5f);
            Assert.AreEqual(DriveBeat.Afterglow, clock.Beat, "5 秒に満たないうちは黙って走ったまま");
            Run(clock, band, 1.0f);
            Assert.AreEqual(DriveBeat.Black, clock.Beat);
        }

        [Test]
        public void ItGoesBlackInOneStep()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 4.9f);
            Assert.AreEqual(0f, clock.Dark(band), 0.0001f, "余韻のあいだは一切暗くならない");
            Run(clock, band, 0.3f);
            Assert.AreEqual(DriveBeat.Black, clock.Beat);
            Assert.AreEqual(1f, clock.Dark(band), 0.0001f, "黒へは切り替えで入る。フェードアウトしない");
        }

        [Test]
        public void ItComesUpGradually()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 5.1f + 0.9f);          // 余韻 5 と黒 0.8 を越える
            Assert.AreEqual(DriveBeat.FadingIn, clock.Beat);
            var first = clock.Dark(band);
            Assert.Less(first, 1f, "黒から続けて明け始めている。越えた分が捨てられていない");
            Run(clock, band, 0.7f);
            var later = clock.Dark(band);
            Assert.Less(later, first, "少しずつ明るくなる");
            Assert.Greater(later, 0f, "まだ明け切っていない");
        }

        [Test]
        public void WhenItHasComeUpItIsDrivingAgain()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 5.1f + 0.9f + 1.5f);
            Assert.AreEqual(DriveBeat.Running, clock.Beat);
            Assert.AreEqual(0f, clock.Dark(band), 0.0001f);
        }

        [Test]
        public void ZeroSecondsDoesNotHang()
        {
            var clock = new BandClock();
            var band = new DriveBand { afterglow = 0f, black = 0f, fadeIn = 0f };
            clock.Trigger();
            clock.Spoken();
            clock.Tick(0.016f, band);
            Assert.AreEqual(DriveBeat.Running, clock.Beat, "全部 0 でも一巡して走りに戻る");
            Assert.IsTrue(clock.TakeSwap(), "黒が 0 秒でも入れ替えは知らせる");
        }

        [Test]
        public void ItReportsWhenTheSceneryShouldBeSwapped()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 4.9f);
            Assert.IsFalse(clock.TakeSwap(), "まだ黒くない");
            Run(clock, band, 0.3f);
            Assert.IsTrue(clock.TakeSwap(), "黒へ入った一度だけ知らせる");
            Assert.IsFalse(clock.TakeSwap(), "二度は知らせない");
        }

        [Test]
        public void ResettingMidBlackoutThrowsTheBlackoutAway()
        {
            var clock = new BandClock();
            var band = Band();
            clock.Trigger();
            clock.Spoken();
            Run(clock, band, 5.2f);
            Assert.AreEqual(DriveBeat.Black, clock.Beat);
            clock.Reset();
            Assert.AreEqual(DriveBeat.Running, clock.Beat, "組み直すと黒は消える。帯を跨ぐたびに呼んではいけない");
            Assert.AreEqual(0f, clock.Dark(band), 0.0001f);
            Assert.IsFalse(clock.TakeSwap(), "知らせも一緒に消える");
        }

        [Test]
        public void OneLongFrameLandsInTheRightPlace()
        {
            var clock = new BandClock();
            var band = Band();                       // 余韻 5.0 / 黒 0.8 / 明け 1.4
            clock.Trigger();
            clock.Spoken();
            clock.Tick(6.0f, band);                  // 一息に 2 段ぶん越える
            Assert.AreEqual(DriveBeat.FadingIn, clock.Beat);
            // 6.0 - 5.0 - 0.8 = 0.2 だけ明けが進んでいる
            Assert.AreEqual(1f - 0.2f / 1.4f, clock.Dark(band), 0.001f, "越えた分が持ち越されている");
        }
    }
}
