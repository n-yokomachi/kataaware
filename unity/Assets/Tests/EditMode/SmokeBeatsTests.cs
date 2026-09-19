using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SmokeBeatsTests
    {
        /// <summary>間を挟まないときの、i 服目に吸い始める時刻</summary>
        static float Plain(int i)
        {
            return SmokeBeats.FirstDragAt + SmokeBeats.Cycle * i;
        }

        [Test]
        public void TheLidOpensBeforeTheFlame()
        {
            Assert.Less(SmokeBeats.ClickAt, SmokeBeats.FlameAt);
            Assert.Less(SmokeBeats.FlameAt, SmokeBeats.FirstDragAt, "火が点いてから吸う");
        }

        [Test]
        public void SheBreathesInBeforeSheBreathesOut()
        {
            for (var i = 0; i < 4; i++)
                Assert.Less(SmokeBeats.DragAt(i, 4), SmokeBeats.BlowAt(i, 4));
        }

        [Test]
        public void TheDragsComeOneCycleApart()
        {
            // 最後の一服には間が入るので、途中どうしで測る
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.DragAt(2, 5) - SmokeBeats.DragAt(1, 5), 1e-4f);
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.BlowAt(3, 5) - SmokeBeats.BlowAt(2, 5), 1e-4f);
        }

        [Test]
        public void OneBlowFinishesBeforeTheNextDrag()
        {
            Assert.LessOrEqual(SmokeBeats.BlowAt(0, 3) + SmokeBeats.BlowSeconds, SmokeBeats.DragAt(1, 3),
                "吐き終わってから次を吸う");
        }

        [Test]
        public void TheCardComesJustAfterSheBreathesOut()
        {
            for (var i = 0; i < SmokeBeats.Drags; i++)
            {
                Assert.AreEqual(SmokeBeats.CardAfterBlow,
                    SmokeBeats.CardAt(i, SmokeBeats.Drags) - SmokeBeats.BlowAt(i, SmokeBeats.Drags), 1e-4f);
                Assert.Less(SmokeBeats.CardAt(i, SmokeBeats.Drags),
                    SmokeBeats.BlowAt(i, SmokeBeats.Drags) + SmokeBeats.BlowSeconds,
                    "まだ吐いている最中に暗くなる");
            }
        }

        [Test]
        public void TheWholeThingCoversEveryDrag()
        {
            var total = SmokeBeats.Total(SmokeBeats.Drags);
            var last = SmokeBeats.BlowAt(SmokeBeats.Drags - 1, SmokeBeats.Drags) + SmokeBeats.BlowSeconds;
            Assert.Greater(total, last, "最後に吐き終わってからも間がある");
            Assert.AreEqual(SmokeBeats.TailSeconds, total - last, 1e-4f);
        }

        [Test]
        public void NoDragsMeansNothingToWaitFor()
        {
            Assert.AreEqual(SmokeBeats.FirstDragAt, SmokeBeats.Total(0), 1e-4f);
        }

        [Test]
        public void MoreDragsTakeLonger()
        {
            Assert.Greater(SmokeBeats.Total(3), SmokeBeats.Total(2));
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.Total(3) - SmokeBeats.Total(2), 1e-3f);
        }

        [Test]
        public void SheSmokesThreeTimes()
        {
            Assert.AreEqual(3, SmokeBeats.Drags);
        }

        [Test]
        public void OnlyTheLastOneIsTheLast()
        {
            Assert.IsTrue(SmokeBeats.IsLast(2, 3));
            Assert.IsFalse(SmokeBeats.IsLast(1, 3));
            Assert.IsFalse(SmokeBeats.IsLast(0, 0), "吸わないなら最後も無い");
        }

        [Test]
        public void SheTakesABreathBeforeTheLastDrag()
        {
            Assert.AreEqual(Plain(0), SmokeBeats.DragAt(0, 3), 1e-4f, "途中の一服はそのまま");
            Assert.AreEqual(SmokeBeats.LastPauseSeconds, SmokeBeats.DragAt(2, 3) - Plain(2), 1e-4f);
        }

        [Test]
        public void SheHoldsItLongerBeforeTheLastBlow()
        {
            var held = SmokeBeats.DragSeconds + SmokeBeats.HoldSeconds;
            Assert.AreEqual(held, SmokeBeats.BlowAt(0, 3) - SmokeBeats.DragAt(0, 3), 1e-4f, "途中はそのまま");
            Assert.AreEqual(held + SmokeBeats.LastPauseSeconds,
                SmokeBeats.BlowAt(2, 3) - SmokeBeats.DragAt(2, 3), 1e-4f);
        }

        [Test]
        public void ThePausesPushTheEndingBack()
        {
            var plain = Plain(SmokeBeats.Drags - 1) + SmokeBeats.DragSeconds + SmokeBeats.HoldSeconds
                + SmokeBeats.BlowSeconds + SmokeBeats.TailSeconds;
            Assert.AreEqual(SmokeBeats.LastPauseSeconds * 2f, SmokeBeats.Total(SmokeBeats.Drags) - plain, 1e-4f);
        }

        [Test]
        public void TheSmokeStartsOnlyAfterTheFlameHasSounded()
        {
            var flame = 1.49f;
            Assert.AreEqual(SmokeBeats.FlameAt + flame, SmokeBeats.SmokeAt(flame), 1e-4f);
            Assert.Greater(SmokeBeats.SmokeAt(flame), SmokeBeats.FlameAt, "火の音が鳴りきってから煙が出る");
            Assert.LessOrEqual(SmokeBeats.SmokeAt(flame), SmokeBeats.FirstDragAt + 1e-3f, "最初の一服には間に合う");
        }

        [Test]
        public void WithoutAClipItFallsBackToTheEstimate()
        {
            Assert.AreEqual(SmokeBeats.FlameAt + SmokeBeats.FlameSeconds, SmokeBeats.SmokeAt(0f), 1e-4f);
        }

        [Test]
        public void SettleKeepsTheTimetableWhenTheLiftIsQuick()
        {
            // 予定より早く明けたら、予定どおりに独白へ移る
            Assert.That(SmokeBeats.Settle(10f, 14f, 1.1f), Is.EqualTo(14f).Within(1e-4f));
        }

        [Test]
        public void SettleWaitsAfterASlowLift()
        {
            // 予定を過ぎてから明けたら、明け終わりからひと呼吸おく
            Assert.That(SmokeBeats.Settle(16f, 14f, 1.1f), Is.EqualTo(17.1f).Within(1e-4f));
        }

        [Test]
        public void SettleNeverGoesBackwards()
        {
            Assert.That(SmokeBeats.Settle(16f, 14f, -5f), Is.EqualTo(16f).Within(1e-4f));
        }
    }
}
