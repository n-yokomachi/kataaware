using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>エンディングの段取りの秒（EndingBeats）。曲の秒から、黒・入れ替え・目・クレジットが決まる</summary>
    public class EndingBeatsTests
    {
        [Test]
        public void BlackUntilTheSongThenOpensAndClosesWithTheTail()
        {
            var b = new EndingBeats();
            Assert.AreEqual(1f, b.Cover(-1f), 1e-5f, "曲の前は黒");
            Assert.AreEqual(1f, b.Cover(0f), 1e-5f, "曲の頭から明け始める");
            Assert.AreEqual(0.5f, b.Cover(b.openFade * 0.5f), 1e-5f);
            Assert.AreEqual(0f, b.Cover(b.openFade), 1e-5f);
            Assert.AreEqual(0f, b.Cover(b.fadeOutFrom - 0.01f), 1e-5f);
            Assert.AreEqual(0.5f, b.Cover((b.fadeOutFrom + b.fadeOutTo) * 0.5f), 1e-5f);
            Assert.AreEqual(1f, b.Cover(b.fadeOutTo), 1e-5f);
            Assert.AreEqual(1f, b.Cover(b.EndAt + 10f), 1e-5f);
        }

        [Test]
        public void SwitchIsDoneBeforeTheDropHits()
        {
            var b = new EndingBeats();
            Assert.Less(b.switchFrom + b.switchFade, b.dropAt, "ドロップの頭の打ちは素の版で鳴る");
            Assert.Greater(b.switchFrom, 35.18f, "低音の抜けた溜め（35.18 秒から）の中で入れ替える");
            Assert.AreEqual(0f, b.Open(b.switchFrom - 0.001f), 1e-5f);
            Assert.AreEqual(1f, b.Open(b.switchFrom + b.switchFade), 1e-5f);
            // 走行音は入れ替えと同じ時刻に消える
            for (var t = 30f; t < 40f; t += 0.01f)
                Assert.AreEqual(1f, b.Open(t) + b.Road(t), 1e-5f);
        }

        [Test]
        public void ZeroFadeStillSwitches()
        {
            var b = new EndingBeats { switchFade = 0f, openFade = 0f };
            Assert.AreEqual(0f, b.Open(b.switchFrom - 0.001f));
            Assert.AreEqual(1f, b.Open(b.switchFrom));
            Assert.AreEqual(0f, b.Cover(0f), "明けに秒が無ければその場で明ける");
        }

        [Test]
        public void GlanceTurnsOnceAndComesBack()
        {
            var b = new EndingBeats();
            Assert.AreEqual(0f, b.Glance(b.glanceAt), 1e-5f);
            Assert.AreEqual(0.5f, b.Glance(b.glanceAt + b.glanceTurn * 0.5f), 1e-4f, "回るのは Face と同じなめらかさ");
            Assert.AreEqual(1f, b.Glance(b.glanceAt + b.glanceTurn + b.glanceHold * 0.5f), 1e-5f);
            Assert.AreEqual(0f, b.Glance(b.GlanceEnd), 1e-5f);
            Assert.AreEqual(0f, b.Glance(b.GlanceEnd + 30f), 1e-5f, "一度だけ");
            Assert.AreEqual(PlayerController.FaceSeconds, b.glanceTurn, 1e-5f);
            Assert.Less(b.GlanceEnd, b.switchFrom, "目は入れ替えの前に前へ戻っている");
        }

        [Test]
        public void GlanceStartsFiveSecondsAfterOpening()
        {
            Assert.AreEqual(5f, new EndingBeats().glanceAt, 1e-5f);
        }

        [Test]
        public void CreditsRollFromTheDropAndSettleBeforeTheFade()
        {
            var b = new EndingBeats();
            Assert.IsFalse(b.Rolling(b.creditsFrom - 0.01f));
            Assert.IsTrue(b.Rolling(b.creditsFrom));
            Assert.AreEqual(0f, b.Roll(b.creditsFrom), 1e-5f);
            Assert.AreEqual(1f, b.Roll(b.creditsTo), 1e-5f);
            Assert.AreEqual(1f, b.Roll(b.fadeOutFrom), 1e-5f, "最後の行で止まってからフェードアウト");
            Assert.Less(b.creditsTo, b.fadeOutFrom);
            Assert.IsFalse(b.Rolling(b.fadeOutTo));
            Assert.AreEqual(b.creditsFrom, b.dropAt, 0.05f, "クレジットはドロップと一緒に入る");
        }

        [Test]
        public void EndsAfterTheBlackAndIgnitionTailFades()
        {
            var b = new EndingBeats();
            Assert.IsFalse(b.Finished(b.fadeOutTo));
            Assert.IsTrue(b.Finished(b.fadeOutTo + b.titleAfter));
            Assert.AreEqual(1f, b.IgnitionTail(0f), 1e-5f);
            Assert.AreEqual(0f, b.IgnitionTail(b.ignitionFade), 1e-5f);
            Assert.AreEqual(b.ignitionAt + b.openAfter, b.OpenAt, 1e-5f);
        }
    }
}
