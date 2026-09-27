using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class MusicMixTests
    {
        /// <summary>seconds 秒ぶん、細かく刻んで進める</summary>
        static void Run(MusicMix mix, float seconds, float step = 0.02f)
        {
            var left = seconds;
            while (left > 1e-5f)
            {
                var dt = Mathf.Min(step, left);
                mix.Tick(dt);
                left -= dt;
            }
        }

        static MusicMix Playing(MusicCue cue)
        {
            var mix = new MusicMix();
            mix.Play(cue, 3f, 6f);
            mix.Front.Fresh = false;
            Run(mix, 3f);
            return mix;
        }

        [Test]
        public void TheCurveKeepsPowerAcrossACrossFade()
        {
            for (var t = 0f; t <= 1f; t += 0.05f)
            {
                var up = MusicVoice.Curve(0f, 1f, t);
                var down = MusicVoice.Curve(1f, 0f, t);
                Assert.AreEqual(1f, up * up + down * down, 1e-4f, "入る曲と出る曲の電力の和は 1");
            }
            Assert.AreEqual(Mathf.Sin(Mathf.PI / 4f), MusicVoice.Curve(0f, 1f, 0.5f), 1e-4f, "0 から 1 は sin の四分の一");
            Assert.AreEqual(0f, MusicVoice.Curve(0f, 1f, 0f), 1e-6f);
            Assert.AreEqual(1f, MusicVoice.Curve(0f, 1f, 1f), 1e-6f);
            Assert.AreEqual(0f, MusicVoice.Curve(1f, 0f, 1f), 1e-6f, "消えきる所で 0");
        }

        [Test]
        public void FromSilenceItFadesIn()
        {
            var mix = new MusicMix();
            Assert.AreEqual(MusicStart.Faded, mix.Play(MusicCue.Dive, 3f, 6f));
            Assert.IsTrue(mix.Front.Fresh, "音源をまだ鳴らしていない");
            Assert.AreEqual(MusicCue.Dive, mix.Current);
            Assert.AreEqual(0f, mix.Front.Gain);
            Run(mix, 1.5f);
            Assert.AreEqual(Mathf.Sin(Mathf.PI / 4f), mix.Front.Gain, 1e-3f, "半分の所");
            Run(mix, 1.5f);
            Assert.AreEqual(1f, mix.Front.Gain, 1e-5f);
            Assert.IsFalse(mix.Front.Fading);
        }

        [Test]
        public void TheSameCueKeepsPlayingWithoutGoingBackToTheHead()
        {
            var mix = Playing(MusicCue.Dive);
            Assert.AreEqual(MusicStart.Kept, mix.Play(MusicCue.Dive, 3f, 6f), "人を渡っても頭へ戻さない");
            Assert.IsFalse(mix.Front.Fresh, "鳴らし直さない");
            Assert.AreEqual(1f, mix.Front.Gain);
            Assert.IsFalse(mix.Back.Active);
        }

        [Test]
        public void AnotherCueCrossFadesAndKeepsThePower()
        {
            var mix = Playing(MusicCue.Dive);
            var was = mix.Front;
            Assert.AreEqual(MusicStart.Crossed, mix.Play(MusicCue.DiveLate, 3f, 6f));
            Assert.AreSame(was, mix.Back, "前の曲は後ろへ回る");
            Assert.AreEqual(MusicCue.DiveLate, mix.Current);
            for (var t = 0f; t < 6f - 1e-4f; t += 0.5f)
            {
                var a = mix.Back.Gain;
                var b = mix.Front.Gain;
                Assert.AreEqual(1f, a * a + b * b, 1e-3f, "渡る間も電力は 1（中が凹まない）: " + t);
                Run(mix, 0.5f);
            }
            Assert.IsFalse(mix.Back.Active, "消えきった前の曲は止まる");
            Assert.AreEqual(1f, mix.Front.Gain, 1e-5f);
        }

        [Test]
        public void FadeOutReachesZeroExactlyOnTime()
        {
            var mix = Playing(MusicCue.Drive);
            mix.FadeOut(12f);
            Assert.AreEqual(MusicCue.None, mix.Current, "消えていく曲は、いまの曲に数えない");
            Run(mix, 11.9f);
            Assert.IsTrue(mix.Front.Active);
            Assert.Less(mix.Front.Gain, 0.02f);
            Assert.Greater(mix.Front.Gain, 0f);
            Run(mix, 0.1f);
            Assert.IsFalse(mix.Sounding, "12 秒で消えきって止まる");
        }

        [Test]
        public void StopIsImmediate()
        {
            var mix = Playing(MusicCue.Garden);
            mix.Stop();
            Assert.IsFalse(mix.Sounding, "フェードしない");
            Assert.AreEqual(0f, mix.Front.Gain);
        }

        [Test]
        public void FadeOutOfZeroSecondsStops()
        {
            var mix = Playing(MusicCue.Garden);
            mix.FadeOut(0f);
            Assert.IsFalse(mix.Sounding);
        }

        [Test]
        public void AShorterFadeOutWinsOverALongerOne()
        {
            var mix = Playing(MusicCue.Notice);
            mix.FadeOut(2f);
            Run(mix, 1f);
            mix.FadeOut(10f);
            Run(mix, 1f);
            Assert.IsFalse(mix.Sounding, "早く消えきる方のまま");
        }

        [Test]
        public void WaitingForTheNextCueHoldsBothFades()
        {
            var mix = Playing(MusicCue.Dive);
            mix.Play(MusicCue.DiveLate, 3f, 6f);
            mix.Front.Waiting = true;
            Run(mix, 3f);
            Assert.AreEqual(1f, mix.Back.Gain, 1e-5f, "読み込みを待つ間は前の曲を消し始めない（無音の隙間を作らない）");
            Assert.AreEqual(0f, mix.Front.Gain);
            mix.Front.Waiting = false;
            Run(mix, 3f);
            Assert.AreEqual(Mathf.Cos(Mathf.PI / 4f), mix.Back.Gain, 1e-3f, "読み終わってから数える");
            Assert.AreEqual(Mathf.Sin(Mathf.PI / 4f), mix.Front.Gain, 1e-3f);
        }

        [Test]
        public void ANewCueDuringAFadeOutFadesInAndLetsTheOldOneFinish()
        {
            var mix = Playing(MusicCue.Reunion);
            mix.FadeOut(10f);
            Run(mix, 2f);
            Assert.AreEqual(MusicStart.Faded, mix.Play(MusicCue.Ending, 3f, 6f), "消えていく途中からは、渡らずに入る");
            Run(mix, 3f);
            Assert.AreEqual(1f, mix.Front.Gain, 1e-5f, "新しい曲は fadeIn の 3 秒で上がる");
            Assert.IsTrue(mix.Back.Active, "前の曲は自分の消え方のまま");
            Run(mix, 5f);
            Assert.IsFalse(mix.Back.Active, "頼んだ 10 秒で消えきる");
        }

        [Test]
        public void TheFadingCueComesBackWhenAskedAgain()
        {
            var mix = Playing(MusicCue.Village);
            mix.FadeOut(10f);
            Run(mix, 2f);
            var low = mix.Front.Gain;
            Assert.AreEqual(MusicStart.Kept, mix.Play(MusicCue.Village, 3f, 6f));
            Assert.AreEqual(low, mix.Front.Gain, 1e-5f, "今の大きさから上げ直す");
            Run(mix, 3f);
            Assert.AreEqual(1f, mix.Front.Gain, 1e-5f);
            Assert.AreEqual(MusicCue.Village, mix.Current);
        }

        [Test]
        public void AThirdCueCutsTheOneStillFadingBehind()
        {
            var mix = Playing(MusicCue.Dive);
            mix.Play(MusicCue.DiveLate, 3f, 6f);
            Run(mix, 2f);
            mix.Play(MusicCue.Garden, 3f, 6f);
            Assert.AreEqual(MusicCue.DiveLate, mix.Back.Cue, "二つまで。いちばん古い曲はそこで切る");
            Assert.AreEqual(MusicCue.Garden, mix.Front.Cue);
        }

        [Test]
        public void NoneDoesNothing()
        {
            var mix = Playing(MusicCue.Dive);
            Assert.AreEqual(MusicStart.Kept, mix.Play(MusicCue.None, 3f, 6f));
            Assert.AreEqual(MusicCue.Dive, mix.Current);
        }
    }
}
