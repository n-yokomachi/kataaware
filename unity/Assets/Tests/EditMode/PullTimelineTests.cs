using NUnit.Framework;

namespace HalfAware.Tests
{
    public class PullTimelineTests
    {
        [Test]
        public void ItStartsWithTheHandsWhereTheyWere()
        {
            Assert.AreEqual(0f, PullTimeline.Reach(0f), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Lift(0f), 1e-4f);
            Assert.IsFalse(PullTimeline.Held(0f));
            Assert.IsFalse(PullTimeline.Done(0f));
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
        public void ThePullIsFinishedBeforeTheHandLetsGo()
        {
            Assert.AreEqual(1f, PullTimeline.Lift(PullTimeline.LetGoAt - 1e-4f), 1e-3f);
            Assert.IsTrue(PullTimeline.Held(PullTimeline.LetGoAt - 1e-4f));
            Assert.IsFalse(PullTimeline.Held(PullTimeline.LetGoAt), "抜き終わりで手を離す");
            Assert.IsTrue(PullTimeline.LetGo(PullTimeline.LetGoAt));
        }

        [Test]
        public void EverythingIsBackAtTheEnd()
        {
            Assert.AreEqual(0f, PullTimeline.Reach(PullTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Lift(PullTimeline.Total), 1e-4f);
            Assert.IsTrue(PullTimeline.Done(PullTimeline.Total));
            Assert.AreEqual(0f, PullTimeline.Reach(PullTimeline.Total + 5f), 1e-4f);
            Assert.AreEqual(0f, PullTimeline.Lift(PullTimeline.Total + 5f), 1e-4f);
        }

        [Test]
        public void TheReachNeverOvershoots()
        {
            for (var t = 0f; t <= PullTimeline.Total + 0.5f; t += 0.01f)
            {
                Assert.GreaterOrEqual(PullTimeline.Reach(t), -1e-4f);
                Assert.LessOrEqual(PullTimeline.Reach(t), 1f + 1e-4f);
                Assert.GreaterOrEqual(PullTimeline.Lift(t), -1e-4f);
                Assert.LessOrEqual(PullTimeline.Lift(t), 1f + 1e-4f);
            }
        }

        [Test]
        public void TheWholeThingIsOverInAboutTwoSeconds()
        {
            Assert.Greater(PullTimeline.Total, 1.5f);
            Assert.Less(PullTimeline.Total, 2.5f);
        }
    }
}
