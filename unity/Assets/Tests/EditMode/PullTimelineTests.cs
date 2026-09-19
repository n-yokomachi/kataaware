using NUnit.Framework;

namespace HalfAware.Tests
{
    public class PullTimelineTests
    {
        [Test]
        public void ItStartsWithTheHandsWhereTheyWere()
        {
            Assert.AreEqual(0f, PullTimeline.Aim(0f), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Reach(0f), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Lift(0f), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Show(0f), 1e-4f);
            Assert.IsFalse(PullTimeline.Held(0f));
            Assert.IsFalse(PullTimeline.Done(0f));
        }

        [Test]
        public void SheLooksAtHerWristBeforeSheReachesForIt()
        {
            Assert.AreEqual(1f, PullTimeline.Aim(PullTimeline.LookSeconds), 1e-4f, "先に視線が届く");
            Assert.AreEqual(0f, PullTimeline.Reach(PullTimeline.LookSeconds), 1e-4f, "見せている間は手を出さない");
            Assert.Greater(PullTimeline.ReachAt, PullTimeline.LookSeconds, "見せたまま止める間がある");
        }

        [Test]
        public void TheHandIsThereWhenItGrips()
        {
            Assert.AreEqual(1f, PullTimeline.Reach(PullTimeline.GripAt), 1e-4f, "掴む時には届いている");
            Assert.IsTrue(PullTimeline.Held(PullTimeline.GripAt));
        }

        [Test]
        public void NothingLiftsBeforeTheGrip()
        {
            Assert.AreEqual(0f, PullTimeline.Lift(PullTimeline.GripAt), 1e-4f, "掴む前に引かない");
            Assert.AreEqual(0f, PullTimeline.Lift(PullTimeline.PullAt), 1e-4f);
        }

        [Test]
        public void ItComesOutWhenThePullStarts()
        {
            Assert.IsFalse(PullTimeline.Out(PullTimeline.PullAt - 1e-3f), "掴んでいる間はまだ刺さっている");
            Assert.IsTrue(PullTimeline.Out(PullTimeline.PullAt));
            Assert.IsTrue(PullTimeline.Out(PullTimeline.Total));
        }

        [Test]
        public void SheOnlyHoldsItOutOnceItIsFree()
        {
            Assert.AreEqual(0f, PullTimeline.Show(PullTimeline.PullAt), 1e-4f, "抜く前に前へ出さない");
            Assert.AreEqual(0f, PullTimeline.Show(PullTimeline.ShowAt), 1e-4f);
            Assert.AreEqual(1f, PullTimeline.Lift(PullTimeline.ShowAt), 1e-4f, "抜き切ってから出す");
            Assert.AreEqual(1f, PullTimeline.Show(PullTimeline.ShowAt + PullTimeline.ShowSeconds), 1e-4f);
        }

        [Test]
        public void TheCableStaysInSightForAWhile()
        {
            var out_ = PullTimeline.ShowAt + PullTimeline.ShowSeconds;
            Assert.AreEqual(PullTimeline.CableSeconds, PullTimeline.LetGoAt - out_, 1e-4f);
            Assert.AreEqual(1f, PullTimeline.Show(PullTimeline.LetGoAt - 1e-4f), 1e-3f, "置くまで出したまま");
            Assert.AreEqual(1f, PullTimeline.Aim(PullTimeline.LetGoAt - 1e-4f), 1e-3f, "視線もジャックを追ったまま");
        }

        [Test]
        public void ThePullIsFinishedBeforeTheHandLetsGo()
        {
            Assert.AreEqual(1f, PullTimeline.Lift(PullTimeline.LetGoAt - 1e-4f), 1e-3f);
            Assert.IsTrue(PullTimeline.Held(PullTimeline.LetGoAt - 1e-4f));
            Assert.IsFalse(PullTimeline.Held(PullTimeline.LetGoAt), "見せ終わりで手を離す");
            Assert.IsTrue(PullTimeline.LetGo(PullTimeline.LetGoAt));
        }

        [Test]
        public void EverythingIsBackAtTheEnd()
        {
            Assert.AreEqual(0f, PullTimeline.Aim(PullTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Reach(PullTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Lift(PullTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Show(PullTimeline.Total), 1e-4f);
            Assert.IsTrue(PullTimeline.Done(PullTimeline.Total));
            Assert.AreEqual(0f, PullTimeline.Reach(PullTimeline.Total + 5f), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Show(PullTimeline.Total + 5f), 1e-4f);
        }

        [Test]
        public void NoneOfItOvershoots()
        {
            for (var t = 0f; t <= PullTimeline.Total + 0.5f; t += 0.01f)
            {
                Assert.GreaterOrEqual(PullTimeline.Aim(t), -1e-4f);
                Assert.LessOrEqual(PullTimeline.Aim(t), 1f + 1e-4f);
                Assert.GreaterOrEqual(PullTimeline.Reach(t), -1e-4f);
                Assert.LessOrEqual(PullTimeline.Reach(t), 1f + 1e-4f);
                Assert.GreaterOrEqual(PullTimeline.Lift(t), -1e-4f);
                Assert.LessOrEqual(PullTimeline.Lift(t), 1f + 1e-4f);
                Assert.GreaterOrEqual(PullTimeline.Show(t), -1e-4f);
                Assert.LessOrEqual(PullTimeline.Show(t), 1f + 1e-4f);
            }
        }

        [Test]
        public void TheStagesComeInOrder()
        {
            Assert.Less(PullTimeline.LookSeconds, PullTimeline.ReachAt);
            Assert.Less(PullTimeline.ReachAt, PullTimeline.GripAt);
            Assert.Less(PullTimeline.GripAt, PullTimeline.PullAt);
            Assert.Less(PullTimeline.PullAt, PullTimeline.ShowAt);
            Assert.Less(PullTimeline.ShowAt, PullTimeline.LetGoAt);
            Assert.Less(PullTimeline.LetGoAt, PullTimeline.Total);
        }

        [Test]
        public void ItTakesLongEnoughToRead()
        {
            // 見せる間を挟んだので、以前の 2 秒では足りない
            Assert.Greater(PullTimeline.Total, 5f);
            Assert.Less(PullTimeline.Total, 9f);
        }
    }
}
