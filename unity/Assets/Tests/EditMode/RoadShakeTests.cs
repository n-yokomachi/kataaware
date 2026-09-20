using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class RoadShakeTests
    {
        /// <summary>舗装の粗さ。DriveBand.rough の帯 0〜2 と同じ値</summary>
        const float Tarmac = 1.0f;
        /// <summary>未舗装の粗さ。帯 4 と同じ値</summary>
        const float Dirt = 4.5f;
        const float Shift = 0.004f;
        /// <summary>DriveWorld の shakeRate と同じ値。試す数と実際に走る数を離さない</summary>
        const float Rate = 1.0f;

        static RoadShake At(float travelled, float rough)
        {
            var shake = new RoadShake();
            shake.Tick(travelled, rough, Shift, Rate);
            return shake;
        }

        [Test]
        public void TheSameDistanceAlwaysGivesTheSameShake()
        {
            // 位相は距離だけから出る。時間や呼ばれた回数が混ざると、
            // 帯を跨いで走り直したときに同じ道が違う揺れ方をする
            var shake = new RoadShake();
            shake.Tick(37.5f, Tarmac, Shift, Rate);
            var first = shake.Offset;
            shake.Tick(120f, Tarmac, Shift, Rate);
            shake.Tick(37.5f, Tarmac, Shift, Rate);
            Assert.AreEqual(first, shake.Offset, "同じ距離なら同じずれ");
        }

        [Test]
        public void StandingStillDoesNotChangeTheShake()
        {
            // 走っていない間は位相が進まない。ガレージで待っているあいだ車が跳ねない
            var shake = new RoadShake();
            shake.Tick(0f, Tarmac, Shift, Rate);
            var still = shake.Offset;
            for (var i = 0; i < 20; i++) shake.Tick(0f, Tarmac, Shift, Rate);
            Assert.AreEqual(still, shake.Offset, "距離が増えなければずれも動かない");
        }

        [Test]
        public void TheShakeNeverGoesFurtherThanTheStatedWidth()
        {
            // shift は「粗さ 1 のときの振れ幅」なので、これを超えると
            // Inspector の数字と画面の揺れが合わなくなる
            for (var step = 0; step <= 4000; step++)
            {
                var travelled = step * 0.37f;
                var o = At(travelled, Dirt).Offset;
                var cap = Shift * Dirt;
                var at = " travelled=" + travelled;
                Assert.LessOrEqual(Mathf.Abs(o.y), cap + 1e-5f, "上下は振れ幅を超えない" + at);
                Assert.LessOrEqual(Mathf.Abs(o.x), cap * RoadShake.SideShare + 1e-5f, "左右は上下より小さい" + at);
            }
        }

        [Test]
        public void TheShakeUsesMostOfTheStatedWidth()
        {
            // 上の上限だけでは、まったく揺れなくても通ってしまう。
            // 一周ぶん通せば振れ幅の大半までは使うこと
            var most = 0f;
            for (var step = 0; step <= 4000; step++)
                most = Mathf.Max(most, Mathf.Abs(At(step * 0.37f, Tarmac).Offset.y));
            Assert.Greater(most, Shift * 0.85f, "振れ幅の 85% までは使う");
        }

        [Test]
        public void RoughnessScalesTheShakeStraightThrough()
        {
            // 未舗装の帯で粗さ 4.5 を渡せば、揺れもちょうど 4.5 倍になる。
            // 途中で丸めたり頭打ちにしたりすると、帯ごとの値が効かなくなる
            for (var step = 0; step < 50; step++)
            {
                var travelled = step * 1.7f;
                var smooth = At(travelled, Tarmac).Offset;
                var rough = At(travelled, Dirt).Offset;
                Assert.AreEqual(smooth.x * Dirt, rough.x, 1e-6f, "左右も粗さのぶんだけ");
                Assert.AreEqual(smooth.y * Dirt, rough.y, 1e-6f, "上下も粗さのぶんだけ");
            }
        }

        [Test]
        public void SmoothGroundDoesNotShakeAtAll()
        {
            // 粗さ 0 は「揺らさない」の意。DriveWorld は走っていない間これを渡す
            for (var step = 0; step < 30; step++)
                Assert.AreEqual(Vector3.zero, At(step * 2.3f, 0f).Offset, "粗さ 0 では揺れない");
        }

        [Test]
        public void NegativeRoughnessIsTakenAsNoShake()
        {
            // 負の粗さを入れ違えて渡されたとき、位相を反転させて揺らし続けない
            Assert.AreEqual(Vector3.zero, At(19f, -3f).Offset, "負の粗さは 0 と同じ");
        }

        [Test]
        public void NoWidthMeansNoShake()
        {
            var shake = new RoadShake();
            shake.Tick(19f, Dirt, 0f, Rate);
            Assert.AreEqual(Vector3.zero, shake.Offset, "振れ幅 0 では揺れない");
            shake.Tick(19f, Dirt, -0.01f, Rate);
            Assert.AreEqual(Vector3.zero, shake.Offset, "負の振れ幅も 0 と同じ");
        }

        [Test]
        public void TheEyeNeverMovesForeAndAft()
        {
            // 前後に動かすと、計器盤が寄ったり離れたりして乗り物酔いに近くなる
            for (var step = 0; step < 200; step++)
                Assert.AreEqual(0f, At(step * 0.91f, Dirt).Offset.z, 1e-7f, "前後には動かさない");
        }

        [Test]
        public void TwiceTheRateIsTwiceTheDistance()
        {
            // 位相は距離と速さの積でしかない。ここが崩れると、
            // 速さの違う帯で同じ凹凸を踏んでいるように見えなくなる
            var slow = new RoadShake();
            var fast = new RoadShake();
            for (var step = 1; step < 40; step++)
            {
                slow.Tick(step * 2f, Tarmac, Shift, Rate);
                fast.Tick(step * 1f, Tarmac, Shift, Rate * 2f);
                Assert.AreEqual(slow.Offset.x, fast.Offset.x, 1e-6f, "左右は距離と速さの積で決まる");
                Assert.AreEqual(slow.Offset.y, fast.Offset.y, 1e-6f, "上下は距離と速さの積で決まる");
            }
        }

        [Test]
        public void AFrozenRateLeavesTheEyeWhereItStarted()
        {
            // 速さ 0 は位相が進まないだけで、走り出しのずれのまま止まる
            var shake = new RoadShake();
            shake.Tick(0f, Tarmac, Shift, Rate);
            var start = shake.Offset;
            shake.Tick(500f, Tarmac, Shift, 0f);
            Assert.AreEqual(start, shake.Offset, "速さ 0 なら走り出しのまま");
        }

        [Test]
        public void ShortStretchesOfRoadDoNotLookTheSame()
        {
            // 数メートルのあいだに上下が動かないと、画面は止まって見える。
            // 16 m/s なら 4 m はおよそ 0.25 秒ぶん
            var low = float.PositiveInfinity;
            var high = float.NegativeInfinity;
            for (var step = 0; step <= 40; step++)
            {
                var y = At(step * 0.1f, Tarmac).Offset.y;
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }
            Assert.Greater(high - low, Shift * 0.2f, "4 m のあいだに振れ幅の 2 割は動く");
        }
    }
}
