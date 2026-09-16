using NUnit.Framework;

namespace HalfAware.Tests
{
    public class WakeUpTests
    {
        const float Seat = 1.10f;
        const float Drop = 0.18f;
        const float StartPitch = -50f;
        const float Seconds = 2f;

        static WakeUp Make()
        {
            return new WakeUp(Seat, Drop, StartPitch, Seconds);
        }

        [Test]
        public void StartsLowAndLookingUpAtTheCeiling()
        {
            var w = Make();
            Assert.AreEqual(Seat - Drop, w.EyeHeight, 1e-4f);
            Assert.AreEqual(StartPitch, w.Pitch, 1e-4f);
            Assert.Less(w.Pitch, 0f, "背を預けているので上を向いて始まる");
            Assert.IsFalse(w.Done);
        }

        [Test]
        public void EndsAtTheSeatedPoseLookingLevel()
        {
            var w = Make();
            for (var i = 0; i < 40; i++) w.Tick(0.05f);
            Assert.AreEqual(Seat, w.EyeHeight, 1e-4f);
            Assert.AreEqual(0f, w.Pitch, 1e-4f);
            Assert.IsTrue(w.Done);
        }

        [Test]
        public void RisesWithoutEverDropping()
        {
            var w = Make();
            var last = w.EyeHeight;
            var lastTilt = UnityEngine.Mathf.Abs(w.Pitch);
            for (var i = 0; i < 40; i++)
            {
                w.Tick(0.05f);
                Assert.GreaterOrEqual(w.EyeHeight + 1e-5f, last, "目線が下がった");
                Assert.LessOrEqual(UnityEngine.Mathf.Abs(w.Pitch) - 1e-5f, lastTilt, "視線が戻らず振れた");
                last = w.EyeHeight;
                lastTilt = UnityEngine.Mathf.Abs(w.Pitch);
            }
        }

        [Test]
        public void NeverGoesPastTheSeatedHeight()
        {
            var w = Make();
            for (var i = 0; i < 80; i++)
            {
                w.Tick(0.05f);
                Assert.LessOrEqual(w.EyeHeight, Seat + 1e-5f);
                Assert.LessOrEqual(w.Pitch, 1e-5f, "水平を越えて下を向かない");
                Assert.GreaterOrEqual(w.Pitch, StartPitch - 1e-5f);
            }
        }

        [Test]
        public void StaysStillOnceItIsDone()
        {
            var w = Make();
            for (var i = 0; i < 40; i++) w.Tick(0.05f);
            var h = w.EyeHeight;
            w.Tick(5f);
            Assert.AreEqual(h, w.EyeHeight, 1e-5f);
            Assert.AreEqual(0f, w.Pitch, 1e-5f);
        }

        [Test]
        public void ZeroSecondsIsAlreadyUp()
        {
            var w = new WakeUp(Seat, Drop, StartPitch, 0f);
            Assert.IsTrue(w.Done);
            Assert.AreEqual(Seat, w.EyeHeight, 1e-4f);
            Assert.AreEqual(0f, w.Pitch, 1e-4f);
        }
    }
}
