using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>自室の間取り（シナリオ設計 5 節「間取り」）</summary>
    public sealed class RoomPlanTests
    {
        /// <summary>体の当たりの半径（Player の CharacterController）</summary>
        const float Body = 0.3f;

        static bool Within(Rect inner, Rect outer)
        {
            return inner.xMin >= outer.xMin - 1e-4f && inner.xMax <= outer.xMax + 1e-4f
                && inner.yMin >= outer.yMin - 1e-4f && inner.yMax <= outer.yMax + 1e-4f;
        }

        [Test]
        public void TheLdkHoldsTheKitchenTheLivingAndTheWorkCorner()
        {
            Assert.AreEqual(8f, RoomPlan.Ldk.width, 1e-4f);
            Assert.AreEqual(4.5f, RoomPlan.Ldk.height, 1e-4f);
            foreach (var r in new[] { RoomPlan.Kitchen, RoomPlan.Counter, RoomPlan.Living, RoomPlan.Work })
                Assert.IsTrue(Within(r, RoomPlan.Ldk), r.ToString());
            // 仕事の区画は前の部屋のまま（机・モニター・椅子・右の卓・東の窓）
            Assert.AreEqual(Rect.MinMaxRect(0f, 0.3f, 3f, 3f), RoomPlan.Work);
            // 作業台は台所と居間の間
            Assert.Greater(RoomPlan.Counter.yMin, RoomPlan.Kitchen.yMax);
            Assert.LessOrEqual(RoomPlan.Counter.yMax, RoomPlan.Living.yMin + 1e-4f);
        }

        [Test]
        public void TheSouthWingSitsUnderTheLdkAroundTheHall()
        {
            var wing = new[] { RoomPlan.Hall, RoomPlan.Bath, RoomPlan.Toilet, RoomPlan.Store, RoomPlan.Bedroom };
            foreach (var r in wing)
            {
                Assert.LessOrEqual(r.yMax, RoomPlan.Ldk.yMin + 1e-4f, r.ToString());
                Assert.GreaterOrEqual(r.yMin, RoomPlan.Hall.yMin - 1e-4f, r.ToString());
                Assert.GreaterOrEqual(r.xMin, RoomPlan.Ldk.xMin - 1e-4f, r.ToString());
                Assert.LessOrEqual(r.xMax, RoomPlan.Ldk.xMax + 1e-4f, r.ToString());
            }
            // 廊下は LDK の南の壁に届く
            Assert.AreEqual(RoomPlan.Ldk.yMin, RoomPlan.Hall.yMax, 1e-4f);
            // 風呂・トイレ・物入れは廊下の西に北から、寝室は東に。重ならない
            Assert.AreEqual(RoomPlan.Hall.xMin, RoomPlan.Bath.xMax, 1e-4f);
            Assert.AreEqual(RoomPlan.Bath.yMin, RoomPlan.Toilet.yMax, 1e-4f);
            Assert.AreEqual(RoomPlan.Toilet.yMin, RoomPlan.Store.yMax, 1e-4f);
            Assert.AreEqual(RoomPlan.Hall.xMax, RoomPlan.Bedroom.xMin, 1e-4f);
            Assert.AreEqual(RoomPlan.Ldk.xMax, RoomPlan.Bedroom.xMax, 1e-4f);
        }

        [Test]
        public void TheBodyPassesTheHallIntoTheLdk()
        {
            // 廊下の中を、体の当たりの半径だけ壁から離れて通れる。玄関の内側から廊下の口を抜けて LDK まで
            var clear = RoomPlan.Hall.width - RoomPlan.Wall;
            Assert.GreaterOrEqual(clear - 2f * Body, 0.3f, "廊下の幅に体の左右の遊びが無い");
            for (var z = RoomPlan.EntranceStand.y; z <= RoomPlan.Ldk.yMin + 1f; z += 0.05f)
                Assert.IsTrue(RoomPlan.Open(new Vector2(RoomPlan.Hall.center.x, z), Body), "廊下の真ん中の z " + z + " で壁に当たる");
            // 閉じた部屋の中と外は歩けない
            Assert.IsFalse(RoomPlan.Open(RoomPlan.Bath.center, 0f));
            Assert.IsFalse(RoomPlan.Open(RoomPlan.Bedroom.center, 0f));
            Assert.IsFalse(RoomPlan.Open(new Vector2(-4f, -3f), 0f));
            // 仕事の区画の立ち位置（椅子と机の間）と場面 1 の座った所は LDK の中
            Assert.IsTrue(RoomPlan.Open(new Vector2(1.5f, 1.68f), Body));
            Assert.IsTrue(RoomPlan.Open(new Vector2(1.5f, 1.2f), Body));
        }

        [Test]
        public void TheConnectSceneStartsInsideTheEntranceFacingTheHall()
        {
            var stand = RoomPlan.EntranceStand;
            var e = RoomPlan.Entrance;
            Assert.IsTrue(RoomPlan.Open(stand, Body), "玄関の内側の立ち位置が壁に当たる");
            // 玄関のドアの内側の面（壁の面）から、体の半径より離れている
            var face = e.Centre.y + RoomPlan.Wall * 0.5f;
            Assert.Greater(stand.y - face, Body + 0.05f);
            Assert.Less(stand.y - face, 0.6f, "玄関から離れすぎ");
            Assert.AreEqual(e.Centre.x, stand.x, 1e-4f);
            Assert.IsTrue(RoomPlan.Hall.Contains(stand));
        }

        [Test]
        public void EachDoorFitsItsWallWithoutOverlapping()
        {
            Assert.AreEqual(5, RoomPlan.Doors.Length, "玄関と、風呂・トイレ・物入れ・寝室");
            var e = RoomPlan.Entrance;
            Assert.IsTrue(e.AlongX, "玄関は南の端の壁");
            Assert.AreEqual(RoomPlan.Hall.yMin, e.Centre.y, 1e-4f);
            // 廊下の中の幅（壁の面の間）に枠が収まる
            Assert.LessOrEqual(e.Wide, RoomPlan.Hall.width - RoomPlan.Wall + 1e-4f);
            for (var i = 1; i < RoomPlan.Doors.Length; i++)
            {
                var d = RoomPlan.Doors[i];
                Assert.IsFalse(d.AlongX, d.Name + " は廊下の脇の壁");
                Assert.AreEqual(RoomPlan.Hall.center.x, d.Centre.x + d.Inward.x * RoomPlan.Hall.width * 0.5f, 1e-4f, d.Name + " の向きが廊下へ向いていない");
                var span = d.Span;
                // 廊下の脇の壁の面の内（南の端の壁の面から、北の端の LDK の南の壁の面まで）
                Assert.GreaterOrEqual(span.x, RoomPlan.Hall.yMin + RoomPlan.Wall * 0.5f - 1e-4f, d.Name);
                Assert.LessOrEqual(span.y, RoomPlan.Ldk.yMin - RoomPlan.Wall * 0.5f + 1e-4f, d.Name);
                for (var j = i + 1; j < RoomPlan.Doors.Length; j++)
                {
                    var o = RoomPlan.Doors[j];
                    if (Mathf.Abs(o.Centre.x - d.Centre.x) > 1e-3f) continue;
                    var s = o.Span;
                    Assert.IsTrue(s.x >= span.y + 0.1f || s.y <= span.x - 0.1f, d.Name + " と " + o.Name + " の枠が重なる");
                }
            }
            // 閉じた部屋はどれも、自分の区画の廊下側の辺にドアがある
            foreach (var pair in new[] { ("DoorBath", RoomPlan.Bath), ("DoorToilet", RoomPlan.Toilet), ("DoorStore", RoomPlan.Store), ("DoorBedroom", RoomPlan.Bedroom) })
            {
                var d = System.Array.Find(RoomPlan.Doors, x => x.Name == pair.Item1);
                Assert.IsNotNull(d.Name, pair.Item1);
                Assert.GreaterOrEqual(d.Centre.y, pair.Item2.yMin, pair.Item1);
                Assert.LessOrEqual(d.Centre.y, pair.Item2.yMax, pair.Item1);
            }
        }

        [Test]
        public void TheDoorItemIsInFrontOfTheEntranceAndReachable()
        {
            var at = RoomPlan.DoorItemAt;
            var e = RoomPlan.Entrance;
            Assert.AreEqual(e.Centre.x, at.x, 1e-4f);
            Assert.Greater(at.z, e.Centre.y + RoomPlan.Wall * 0.5f, "壁の面より手前");
            Assert.IsTrue(RoomPlan.Open(new Vector2(at.x, at.z), 0f));
            // 廊下に立った目（立った目の高さ）から、調べる対象の半径 2 m の内に入る
            var eye = new Vector3(RoomPlan.EntranceStand.x, PlayerController.StandingEyeHeight, RoomPlan.EntranceStand.y);
            Assert.Less(Vector3.Distance(eye, at), 2f);
        }

        [Test]
        public void TheWindowsSitInTheirWalls()
        {
            Assert.AreEqual(3, RoomPlan.Windows.Length);
            var north = new System.Collections.Generic.List<Vector2>();
            foreach (var w in RoomPlan.Windows)
            {
                var span = w.Span;
                var from = w.East ? RoomPlan.Ldk.yMin : RoomPlan.Ldk.xMin;
                var to = w.East ? RoomPlan.Ldk.yMax : RoomPlan.Ldk.xMax;
                Assert.GreaterOrEqual(span.x, from + RoomPlan.Wall, w.Name);
                Assert.LessOrEqual(span.y, to - RoomPlan.Wall, w.Name);
                if (!w.East) north.Add(span);
            }
            Assert.AreEqual(2, north.Count);
            Assert.IsTrue(north[1].y < north[0].x || north[1].x > north[0].y, "北の二つの窓が重なる");
            // 前からの窓は今の所のまま（x −1.78〜−0.82、東は z −0.98〜−0.02）
            Assert.AreEqual(-1.78f, RoomPlan.NorthWindow.Span.x, 1e-4f);
            Assert.AreEqual(-0.82f, RoomPlan.NorthWindow.Span.y, 1e-4f);
            Assert.AreEqual(-0.98f, RoomPlan.EastWindow.Span.x, 1e-4f);
            // 居間の窓はオーナーの決めた x −4.4〜−3.2 の真ん中
            Assert.AreEqual(-3.8f, RoomPlan.WestWindow.Centre, 1e-4f);
            Assert.IsTrue(RoomPlan.Living.xMin < RoomPlan.WestWindow.Span.x && RoomPlan.WestWindow.Span.y < RoomPlan.Living.xMax);
        }
    }
}
