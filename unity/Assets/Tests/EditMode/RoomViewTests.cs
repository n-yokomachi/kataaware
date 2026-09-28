using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>自室の窓の外の景色の寸法と時刻（シナリオ設計 5 節「窓の外」）</summary>
    public sealed class RoomViewTests
    {
        /// <summary>目のカメラの far。自室の Player/Main Camera の値</summary>
        const float CameraFar = 1000f;

        [Test]
        public void TheRoomIsOnTheFifthFloor()
        {
            // 日本の数え方で 5 階。4 階分（1 階 3 m）下に地面
            Assert.AreEqual(-12f, RoomView.Ground, 1e-4f);
        }

        [Test]
        public void EachWindowSeesAboutSeventySevenDegreesEitherSide()
        {
            var north = RoomView.Reach(false);
            var east = RoomView.Reach(true);
            Assert.AreEqual(-77.5f, north.x, 0.5f);
            Assert.AreEqual(77.5f, north.y, 0.5f);
            Assert.AreEqual(12.5f, east.x, 0.5f);
            Assert.AreEqual(167.5f, east.y, 0.5f);
        }

        [Test]
        public void TheBuiltArcCoversEveryDirectionSeenThroughTheWindows()
        {
            // 部屋の中の立てる所から、あらゆる向きへ目を振り、窓を抜けて見える向きがどれも組んだ幅の内にある
            var worstLow = 999f;
            var worstHigh = -999f;
            var worstUp = -999f;
            var seen = 0;
            // 体は壁から 0.3 m（当たりの半径）まで寄れ、目はそこから 0.22 m 前に出る
            var spots = new[] { -2.6f, -2.2f, -1.8f, -1.4f, -1.0f, -0.6f, -0.2f, 0.2f, 0.6f, 1.0f, 1.4f, 1.8f, 2.2f, 2.6f, 2.82f };
            foreach (var x in spots)
                foreach (var z in spots)
                    foreach (var eyeY in new[] { 1.31f, 1.65f })
                    {
                        var eye = new Vector3(x, eyeY, z);
                        for (var az = -180f; az < 180f; az += 2f)
                            for (var el = -80f; el <= 80f; el += 4f)
                            {
                                var dir = Quaternion.Euler(-el, az, 0f) * Vector3.forward;
                                foreach (var east in new[] { false, true })
                                {
                                    float a, e;
                                    if (!RoomView.Through(east, eye, dir, out a, out e)) continue;
                                    seen++;
                                    if (a < RoomView.ArcFrom) a += 360f;
                                    worstLow = Mathf.Min(worstLow, a);
                                    worstHigh = Mathf.Max(worstHigh, a);
                                    worstUp = Mathf.Max(worstUp, e);
                                }
                            }
                    }
            Assert.Greater(seen, 1000, "窓を抜ける向きが一つも無い");
            Assert.GreaterOrEqual(worstLow, RoomView.ArcFrom, "北の窓の左の端が組んだ幅の外");
            Assert.LessOrEqual(worstHigh, RoomView.ArcTo, "東の窓の右の端が組んだ幅の外");
            // 空の球は天頂まで張るが、窓から見上げられるのは仰角 70 度ほどまで
            Assert.Less(worstUp, 75f);
        }

        [Test]
        public void TheSkyStaysInsideTheCameraFar()
        {
            // 目は部屋の中を 3 m ほど動くだけ
            var far = RoomView.OnSky(RoomView.ArcFrom, RoomView.SkyBottom).magnitude + 4.3f;
            Assert.Less(far, CameraFar);
            Assert.Less(RoomView.OnSky(40f, 90f).magnitude + 4.3f, CameraFar);
        }

        [Test]
        public void TheSkyPictureGivesTheHorizonMoreRows()
        {
            Assert.AreEqual(0f, RoomView.SkyV(RoomView.SkyBottom), 1e-5f);
            Assert.AreEqual(1f, RoomView.SkyV(90f), 1e-5f);
            // 仰角 20 度までに縦の 7 割
            Assert.AreEqual(0.7f, RoomView.SkyV(20f), 1e-5f);
            // 行き帰りで同じ仰角に戻る
            foreach (var e in new[] { -14f, -3f, 0f, 2.5f, 19f, 20f, 45f, 90f })
                Assert.AreEqual(e, RoomView.SkyElevation(RoomView.SkyV(e)), 1e-3f, "仰角 " + e);
            Assert.AreEqual(0f, RoomView.SkyU(RoomView.ArcFrom), 1e-5f);
            Assert.AreEqual(1f, RoomView.SkyU(RoomView.ArcTo), 1e-5f);
        }

        [Test]
        public void TheRoomIsDuskAndItsCopiesAreNight()
        {
            Assert.AreEqual(RoomView.Hour.Dusk, RoomView.HourOf("Room"));
            Assert.AreEqual(RoomView.Hour.Night, RoomView.HourOf("Connect"));
            Assert.AreEqual(RoomView.Hour.Night, RoomView.HourOf("Rest"));
            Assert.AreEqual(RoomView.Hour.Night, RoomView.HourOf("Notice"));
        }
    }
}
