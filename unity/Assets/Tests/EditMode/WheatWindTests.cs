using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class WheatWindTests
    {
        /// <summary>区切り 1 つの長さ。BuildDrive.TileLength と同じ値。組み立ては Editor 側なので写す</summary>
        const float Slice = 20f;
        /// <summary>株の背。重みが 1 に届く高さそのもの</summary>
        static readonly float Tall = WheatWind.High;

        [Test]
        public void TheRootDoesNotMove()
        {
            // 根が動くと株ごと横へ滑って、生えているのではなく浮いて見える
            for (var t = 0f; t < 6f; t += 0.7f)
            {
                var at = WheatWind.Offset(3.9f, 7.25f, 0f, t);
                Assert.AreEqual(0f, at.x, 1e-6f, "根は横へ動かない");
                Assert.AreEqual(0f, at.y, 1e-6f, "根は前後へも動かない");
            }
        }

        [Test]
        public void TheTipNeverGoesFurtherThanReach()
        {
            // 見直し（CheckDrive.OnRoad）が麦の食い込みを測るのにこの幅を足す。
            // 実際の振れがこれを越えると、轍へ倒れ込むのを見逃す
            var most = 0f;
            for (var t = 0f; t < 30f; t += 0.05f)
                for (var x = -40f; x <= 40f; x += 1.7f)
                    for (var z = 0f; z < Slice; z += 1.3f)
                        most = Mathf.Max(most, Mathf.Abs(WheatWind.Offset(x, z, Tall, t).x));
            Assert.LessOrEqual(most, WheatWind.Reach + 1e-4f, "穂先は Reach を越えない");
            // 名ばかりの上限では意味が無い。実際にそこそこまで振れていることも見る
            Assert.Greater(most, WheatWind.Reach * 0.85f, "Reach が実際の振れよりずっと大きい");
        }

        [Test]
        public void TheWaveJoinsUpAcrossSlices()
        {
            // 位相は区切りの中の z から取る。区切りの長さで波が割り切れないと、
            // 20 m ごとに畑が一列だけ違う向きへ倒れる
            for (var t = 0f; t < 5f; t += 0.9f)
                for (var x = -30f; x <= 30f; x += 6.1f)
                {
                    var near = WheatWind.Offset(x, 0f, Tall, t);
                    var far = WheatWind.Offset(x, Slice, Tall, t);
                    Assert.AreEqual(near.x, far.x, 1e-4f, "区切りの継ぎ目で横の振れが揃う");
                    Assert.AreEqual(near.y, far.y, 1e-4f, "区切りの継ぎ目で前後の振れも揃う");
                }
        }

        [Test]
        public void TallerStalksLeanFurther()
        {
            // 背の低い株まで穂先と同じだけ倒れると、畑が一枚の板のように傾く
            var t = 2.3f;
            var low = Mathf.Abs(WheatWind.Offset(5.2f, 3.4f, 0.45f, t).x);
            var high = Mathf.Abs(WheatWind.Offset(5.2f, 3.4f, Tall, t).x);
            Assert.Less(low, high, "低いところほど振れが浅い");
        }

        [Test]
        public void TheWeightStopsClimbingAboveTheStalk()
        {
            // 背より高いところを渡されても重みは 1 で頭打ちにする。
            // 天井が無いと、背の高い株だけが跳ね上がる
            Assert.AreEqual(1f, WheatWind.Weight(Tall), 1e-6f);
            Assert.AreEqual(1f, WheatWind.Weight(Tall * 3f), 1e-6f);
            Assert.Less(WheatWind.Weight(Tall * 0.5f), 1f, "背の半分では重みが 1 に届かない");
            Assert.AreEqual(0f, WheatWind.Weight(-1f), 1e-6f);
        }

        [Test]
        public void TheFieldKeepsMoving()
        {
            // 微風になびいているのが要り用なので、止まっていては困る。
            // 一周ぶん（2π / Rate ≒ 2.9 秒）のあいだに必ず揺り返しが来る
            var first = WheatWind.Offset(7.3f, 11.25f, Tall, 0f).x;
            var moved = false;
            for (var t = 0.1f; t < 2f * Mathf.PI / WheatWind.Rate; t += 0.05f)
                if (Mathf.Abs(WheatWind.Offset(7.3f, 11.25f, Tall, t).x - first) > WheatWind.Amp * 0.5f)
                    moved = true;
            Assert.IsTrue(moved, "一揺れのあいだに穂先が振れ幅の半分以上動く");
        }

        [Test]
        public void TheSameMomentAlwaysGivesTheSameLean()
        {
            // シェーダーは同じ式を毎フレーム引き直す。時刻と位置だけで決まっていないと、
            // 見直しの見立ても、オーナーが撮った絵も当てにならなくなる
            var a = WheatWind.Offset(12.6f, 5.5f, 0.98f, 4.25f);
            var b = WheatWind.Offset(12.6f, 5.5f, 0.98f, 4.25f);
            Assert.AreEqual(a, b);
        }
    }
}
