using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>エンディングの景色の帯を、曲の秒で選ぶ（EndingBand.At）</summary>
    public class EndingBandTests
    {
        static EndingBand[] Bands(params float[] from)
        {
            var b = new EndingBand[from.Length];
            for (var i = 0; i < from.Length; i++) b[i] = new EndingBand { name = "b" + i, from = from[i] };
            return b;
        }

        [Test]
        public void PicksTheLastBandThatHasBegun()
        {
            var b = Bands(0f, 36f, 77f);
            Assert.AreEqual(0, EndingBand.At(b, -5f), "曲の前は最初の帯");
            Assert.AreEqual(0, EndingBand.At(b, 0f));
            Assert.AreEqual(0, EndingBand.At(b, 35.99f));
            Assert.AreEqual(1, EndingBand.At(b, 36f), "ドロップで替わる");
            Assert.AreEqual(1, EndingBand.At(b, 76.9f));
            Assert.AreEqual(2, EndingBand.At(b, 500f), "最後の帯は曲の終わりまで");
        }

        [Test]
        public void DroppingABandLengthensThePreviousOne()
        {
            // 帯を外す（行を消す）と、前の帯がそのぶん長くなる
            var b = Bands(0f, 36f, 104f);
            Assert.AreEqual(1, EndingBand.At(b, 80f));
        }

        [Test]
        public void NoBandsIsMinusOne()
        {
            Assert.AreEqual(-1, EndingBand.At(new EndingBand[0], 10f));
            Assert.AreEqual(-1, EndingBand.At(null, 10f));
        }

        [Test]
        public void TheDropSwitchesTheSceneryWithTheSound()
        {
            // 組み立ての表（BuildEndingLand.Route）の二つ目は、ドロップと同じ秒から
            var beats = new EndingBeats();
            Assert.AreEqual(beats.creditsFrom, 36f, 0.05f);
            Assert.Less(beats.switchFrom + beats.switchFade, 36.012f);
        }
    }
}
