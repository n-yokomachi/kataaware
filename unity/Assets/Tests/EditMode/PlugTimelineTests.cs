using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class PlugTimelineTests
    {
        [Test]
        public void TheStagesRunInOrder()
        {
            Assert.Less(PlugTimeline.GripAt, PlugTimeline.CarryAt);
            Assert.Less(PlugTimeline.CarryAt, PlugTimeline.PushAt);
            Assert.Less(PlugTimeline.PushAt, PlugTimeline.InAt);
            Assert.Less(PlugTimeline.InAt, PlugTimeline.LetGoAt);
            Assert.Less(PlugTimeline.LetGoAt, PlugTimeline.Total);
        }

        [Test]
        public void EveryBendStartsAndEndsAtNothing()
        {
            Assert.AreEqual(0f, PlugTimeline.Reach(0f), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Carry(0f), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Push(0f), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Reach(PlugTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Carry(PlugTimeline.Total), 1e-4f);
            Assert.AreEqual(0f, PlugTimeline.Push(PlugTimeline.Total), 1e-4f);
        }

        /// <summary>手は肘掛けへ戻すが、視線は挿したところを見たまま置く</summary>
        [Test]
        public void HerEyesStayOnItAfterTheHandGoesBack()
        {
            Assert.AreEqual(0f, PlugTimeline.Aim(0f), 1e-4f);
            Assert.AreEqual(1f, PlugTimeline.Aim(PlugTimeline.LookSeconds), 1e-4f);
            Assert.AreEqual(1f, PlugTimeline.Aim(PlugTimeline.LetGoAt), 1e-4f);
            Assert.AreEqual(1f, PlugTimeline.Aim(PlugTimeline.Total), 1e-4f);
            Assert.AreEqual(1f, PlugTimeline.Aim(PlugTimeline.Total + 5f), 1e-4f);
        }

        [Test]
        public void HerEyesTurnWithoutJumping()
        {
            var last = 0f;
            for (var t = 0f; t <= PlugTimeline.LookSeconds; t += 0.01f)
            {
                var now = PlugTimeline.Aim(t);
                Assert.GreaterOrEqual(now, last - 1e-4f);
                Assert.LessOrEqual(now - last, 0.05f);
                last = now;
            }
        }

        [Test]
        public void EveryWeightIsFullWhileSheIsStillPushing()
        {
            var t = PlugTimeline.InAt + 0.1f;
            Assert.AreEqual(1f, PlugTimeline.Aim(t), 1e-3f);
            Assert.AreEqual(1f, PlugTimeline.Reach(t), 1e-3f);
            Assert.AreEqual(1f, PlugTimeline.Carry(t), 1e-3f);
            Assert.AreEqual(1f, PlugTimeline.Push(t), 1e-3f);
        }

        [Test]
        public void SheHoldsItFromTheGripUntilItIsIn()
        {
            Assert.IsFalse(PlugTimeline.Held(PlugTimeline.GripAt - 0.01f));
            Assert.IsTrue(PlugTimeline.Held(PlugTimeline.GripAt + 0.01f));
            Assert.IsTrue(PlugTimeline.Held(PlugTimeline.InAt - 0.01f));
            Assert.IsFalse(PlugTimeline.Held(PlugTimeline.InAt + 0.01f));
        }

        [Test]
        public void ItIsInOnlyAfterThePush()
        {
            Assert.IsFalse(PlugTimeline.In(PlugTimeline.InAt - 0.01f));
            Assert.IsTrue(PlugTimeline.In(PlugTimeline.InAt));
        }

        [Test]
        public void HerEyesLeaveItOnceSheLetsGo()
        {
            Assert.IsTrue(PlugTimeline.Follows(PlugTimeline.LetGoAt - 0.01f));
            Assert.IsFalse(PlugTimeline.Follows(PlugTimeline.LetGoAt));
        }

        [Test]
        public void ItIsDoneAtTheEnd()
        {
            Assert.IsFalse(PlugTimeline.Done(PlugTimeline.Total - 0.01f));
            Assert.IsTrue(PlugTimeline.Done(PlugTimeline.Total));
        }

        [Test]
        public void TheRightWristIsUpBeforeTheLeftHandReaches()
        {
            Assert.AreEqual(1f, PlugTimeline.RightHand(PlugTimeline.LookSeconds), 1e-4f, "視線を落とし終える時には、右の手首が上がって肘掛けが空いている");
            Assert.AreEqual(1f, PlugTimeline.RightHand(PlugTimeline.InAt), 1e-4f, "挿す間は上げたまま");
            Assert.AreEqual(0f, PlugTimeline.RightHand(PlugTimeline.Total), 1e-4f, "手を戻す間に下ろす");
        }

        [Test]
        public void TheFingersComeOffTheJackFirst()
        {
            Assert.AreEqual(0f, PlugTimeline.Release(PlugTimeline.LetGoAt), 1e-4f, "離すまでは抜かない");
            Assert.AreEqual(1f, PlugTimeline.Release(PlugTimeline.LetGoAt + PlugTimeline.ReturnSeconds * 0.25f), 1e-4f, "戻しの初めに指を抜ききる");
            Assert.Greater(PlugTimeline.Reach(PlugTimeline.LetGoAt + PlugTimeline.ReturnSeconds * 0.25f), 0.8f, "抜ききる時、手はまだ挿した所の近く");
        }
    }
}
