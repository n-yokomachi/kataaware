using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class RoadRingTests
    {
        const int Tiles = 8;
        const float Length = 20f;
        /// <summary>BuildDrive.Behind と同じ値。試す寸法と実際に敷く寸法を離さない</summary>
        const float Behind = -70f;
        /// <summary>BuildDrive.Ahead と同じ値。Behind と合わせて環一周が 180 m になる</summary>
        const float Ahead = 110f;

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
        public void TheRingStaysEvenlySpacedThroughTheWrap()
        {
            // TilesSitOneLengthApart は travelled = 0 だけを見るので、環が回り込むところを
            // 一度も踏まない。隙間は回り込んだ瞬間に開くので、一周ぶん通して見ないと気づけない
            var n = RoadRing.Needed(Ahead, Length, Behind);
            var span = n * Length;
            var zs = new float[n];
            // 刻みをタイルの長さの約数から外して、継ぎ目のあらゆる位相を通す
            for (var step = -200; step <= 400; step++)
            {
                var travelled = step * 0.97f;
                for (var i = 0; i < n; i++) zs[i] = RoadRing.Slot(i, n, Length, travelled, Behind);
                System.Array.Sort(zs);
                var at = " travelled=" + travelled;
                Assert.GreaterOrEqual(zs[0], Behind, "後ろの端より手前には来ない" + at);
                Assert.Less(zs[n - 1], Behind + span, "前の端は越えない" + at);
                for (var i = 1; i < n; i++)
                    Assert.AreEqual(Length, zs[i] - zs[i - 1], 0.0001f, "隙間も重なりも無い" + at);
            }
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
            // 前 110 と後ろ 70 でちょうど 180 m。1 枚 20 m なら 9 枚
            Assert.AreEqual(9, RoadRing.Needed(Ahead, Length, Behind));
            Assert.GreaterOrEqual(RoadRing.Needed(Ahead, Length, Behind) * Length, Ahead - Behind,
                "環の長さが見える範囲を覆っていないと、前の端に穴が空く");
            // 沿道の間隔はどれも環一周を割り切る数にしてある。一周が 180 から動くと
            // BuildDriveLand の間隔が全部成り立たなくなるので、ここで釘を打っておく
            Assert.AreEqual(180f, RoadRing.Needed(Ahead, Length, Behind) * Length, 0.0001f,
                "環一周は 180 m。沿道の間隔（60 / 45 / 36 / 30 / 20 / 12）の最小公倍数");

            // 寸法を変えても成り立たなければいけない関係。9 や 2 という数そのものではなく、
            // 「環の長さが見える範囲に届く」ことを見る
            foreach (var w in new[] { new Vector3(Ahead, Length, Behind), new Vector3(1f, 100f, -1f),
                                      new Vector3(60f, 7f, -12f), new Vector3(200f, 33f, -5f) })
                Assert.GreaterOrEqual(RoadRing.Needed(w.x, w.y, w.z) * w.y, w.x - w.z,
                    "環の長さが見える範囲に届いていない");
        }

        [Test]
        public void AShortRoadStillNeedsTwoTiles()
        {
            Assert.AreEqual(2, RoadRing.Needed(1f, 100f, -1f), "1 枚だと送った瞬間に消える");
            Assert.AreEqual(0, RoadRing.Needed(140f, 0f, Behind), "長さが 0 なら敷きようがない");
        }
    }
}
