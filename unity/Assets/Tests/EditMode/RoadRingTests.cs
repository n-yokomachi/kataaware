using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class RoadRingTests
    {
        const int Tiles = 8;
        const float Length = 20f;
        const float Behind = -30f;

        [Test]
        public void TilesSitOneLengthApart()
        {
            var zs = new float[Tiles];
            for (var i = 0; i < Tiles; i++) zs[i] = RoadRing.Slot(i, Tiles, Length, 0f, Behind);
            System.Array.Sort(zs);
            for (var i = 1; i < Tiles; i++)
                Assert.AreEqual(Length, zs[i] - zs[i - 1], 0.0001f, "隙間も重なりも無い");
        }

        [Test]
        public void TheWholeRingCoversTheSpan()
        {
            var zs = new float[Tiles];
            for (var i = 0; i < Tiles; i++) zs[i] = RoadRing.Slot(i, Tiles, Length, 7.3f, Behind);
            System.Array.Sort(zs);
            Assert.GreaterOrEqual(zs[0], Behind, "後ろの端より手前には来ない");
            Assert.Less(zs[Tiles - 1], Behind + Tiles * Length, "前の端は越えない");
        }

        [Test]
        public void DrivingOneTileLengthBringsTheRingBack()
        {
            for (var i = 0; i < Tiles; i++)
            {
                var start = RoadRing.Slot(i, Tiles, Length, 0f, Behind);
                var later = RoadRing.Slot(i, Tiles, Length, Tiles * Length, Behind);
                Assert.AreEqual(start, later, 0.0001f, "一周すると並びが元に戻る");
            }
        }

        [Test]
        public void TilesMoveTowardsTheCar()
        {
            var before = RoadRing.Slot(3, Tiles, Length, 0f, Behind);
            var after = RoadRing.Slot(3, Tiles, Length, 2f, Behind);
            Assert.Less(after, before, "走ると手前へ寄ってくる");
        }

        [Test]
        public void GoingBackwardsDoesNotBreakIt()
        {
            var z = RoadRing.Slot(2, Tiles, Length, -45f, Behind);
            Assert.GreaterOrEqual(z, Behind);
            Assert.Less(z, Behind + Tiles * Length);
        }

        [Test]
        public void ARingOfNothingIsHarmless()
        {
            Assert.AreEqual(Behind, RoadRing.Slot(0, 0, Length, 5f, Behind), 0.0001f);
            Assert.AreEqual(Behind, RoadRing.Slot(0, Tiles, 0f, 5f, Behind), 0.0001f);
        }

        [Test]
        public void ItCountsHowManyTilesReachAhead()
        {
            // 前 140 と後ろ 30 で 170 m。1 枚 20 m なら 9 枚
            Assert.AreEqual(9, RoadRing.Needed(140f, Length, Behind));
            Assert.GreaterOrEqual(RoadRing.Needed(140f, Length, Behind) * Length, 140f - Behind,
                "環の長さが見える範囲を覆っていないと、前の端に穴が空く");
        }

        [Test]
        public void AShortRoadStillNeedsTwoTiles()
        {
            Assert.AreEqual(2, RoadRing.Needed(1f, 100f, -1f), "1 枚だと送った瞬間に消える");
            Assert.AreEqual(0, RoadRing.Needed(140f, 0f, Behind), "長さが 0 なら敷きようがない");
        }
    }
}
