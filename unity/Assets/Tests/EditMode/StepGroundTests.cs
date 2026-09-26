using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class StepGroundTests
    {
        static readonly Vector2[] Bend = { new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(3f, 4f) };

        [Test]
        public void TheBandCoversTheLineAndItsHalfWidth()
        {
            Assert.IsTrue(StepGround.InBand(new Vector2(0f, 2f), Bend, 0.55f), "芯の上");
            Assert.IsTrue(StepGround.InBand(new Vector2(0.5f, 2f), Bend, 0.55f), "半幅の内");
            Assert.IsFalse(StepGround.InBand(new Vector2(0.6f, 2f), Bend, 0.55f), "半幅の外");
            Assert.IsTrue(StepGround.InBand(new Vector2(2f, 4.4f), Bend, 0.55f), "曲がった先の辺");
        }

        [Test]
        public void TheBendIsNotLeftWithAGap()
        {
            // 曲がりの外の角。二つの辺のどちらにも直角には入らないが、継ぎ目から半幅の内
            Assert.IsTrue(StepGround.InBand(new Vector2(-0.3f, 4.3f), Bend, 0.55f));
        }

        [Test]
        public void TheEndsAreCutSquare()
        {
            Assert.IsFalse(StepGround.InBand(new Vector2(0f, -0.2f), Bend, 0.55f), "始まりの手前は入れない");
            Assert.IsFalse(StepGround.InBand(new Vector2(3.2f, 4f), Bend, 0.55f), "終わりの先は入れない");
        }

        [Test]
        public void ThePatchWinsOverTheGroundAndAnEmptyPatchFallsBack()
        {
            var soft = new[] { AudioClip.Create("soft", 1, 1, 44100, false) };
            var hard = new[] { AudioClip.Create("hard", 1, 1, 44100, false) };
            try
            {
                var parts = new[]
                {
                    new StepGround.Patch { line = Bend, half = 0.55f, clips = hard },
                    new StepGround.Patch { box = new Rect(5f, 0f, 2f, 2f), clips = new AudioClip[0] },
                };
                Assert.AreSame(hard, StepGround.Pick(new Vector2(0f, 1f), soft, parts), "帯の中は帯の音");
                Assert.AreSame(soft, StepGround.Pick(new Vector2(1.5f, 1f), soft, parts), "どこにも入らなければ当たりの音");
                Assert.AreSame(soft, StepGround.Pick(new Vector2(6f, 1f), soft, parts), "音の空な区画は当たりの音へ落とす");
                Assert.AreEqual(0, StepGround.Pick(new Vector2(9f, 9f), null, parts).Length, "当たりの音も無ければ空。既定の音に任せる");
            }
            finally
            {
                Object.DestroyImmediate(soft[0]);
                Object.DestroyImmediate(hard[0]);
            }
        }
    }
}
