using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// BGM の置き場を、エディタで組んで秒を直に回して確かめる（遊ばない。音源は鳴らさず、大きさだけを見る）。
    /// あわせて、場面の演出がどこで何を頼むかの決まり（場面 4・7・8、村の入口、場面を移った時）を確かめる
    /// </summary>
    public class MusicBedTests
    {
        MusicBed bed;
        MusicTable table;
        AudioClip dive;
        AudioClip late;

        [SetUp]
        public void SetUp()
        {
            dive = AudioClip.Create("TestDive", 44100, 2, 44100, false);
            late = AudioClip.Create("TestLate", 44100, 2, 44100, false);
            table = ScriptableObject.CreateInstance<MusicTable>();
            table.tracks = new[]
            {
                new MusicTable.Track { cue = MusicCue.Dive, clip = dive, volume = 0.5f, fadeIn = 2f, crossFade = 4f },
                new MusicTable.Track { cue = MusicCue.DiveLate, clip = late, volume = 0.8f, fadeIn = 2f, crossFade = 4f },
                // 曲をまだ当てていない行
                new MusicTable.Track { cue = MusicCue.Village, clip = null },
            };
            bed = MusicBed.Make(table);
        }

        [TearDown]
        public void TearDown()
        {
            if (bed != null) bed.Dispose();
            Object.DestroyImmediate(table);
            Object.DestroyImmediate(dive);
            Object.DestroyImmediate(late);
        }

        static void Run(MusicBed bed, float seconds, float step = 0.02f)
        {
            var left = seconds;
            while (left > 1e-5f)
            {
                var dt = Mathf.Min(step, left);
                bed.Step(dt);
                left -= dt;
            }
        }

        [Test]
        public void TheSourceVolumeIsTheTrackVolumeTimesTheFade()
        {
            bed.Request(MusicCue.Dive, -1f);
            var src = bed.Source(bed.Mix.FrontIndex);
            Assert.AreSame(dive, src.clip);
            Assert.IsTrue(src.loop);
            Assert.AreEqual(0f, src.volume, 1e-5f, "0 から入る");
            Run(bed, 1f);
            Assert.AreEqual(0.5f * Mathf.Sin(Mathf.PI / 4f), src.volume, 2e-3f, "曲の大きさ 0.5 に、fadeIn 2 秒の半分の所");
            Run(bed, 1f);
            Assert.AreEqual(0.5f, src.volume, 1e-4f);
        }

        [Test]
        public void CrossFadingMovesToTheOtherSource()
        {
            bed.Request(MusicCue.Dive, -1f);
            Run(bed, 2f);
            var first = bed.Mix.FrontIndex;
            bed.Request(MusicCue.DiveLate, -1f);
            var second = bed.Mix.FrontIndex;
            Assert.AreNotEqual(first, second);
            Assert.AreSame(late, bed.Source(second).clip);
            Run(bed, 2f);
            Assert.AreEqual(0.5f * Mathf.Cos(Mathf.PI / 4f), bed.Source(first).volume, 2e-3f, "前の曲は crossFade 4 秒の半分");
            Assert.AreEqual(0.8f * Mathf.Sin(Mathf.PI / 4f), bed.Source(second).volume, 2e-3f);
            Run(bed, 2f);
            Assert.IsNull(bed.Source(first).clip, "消えきった前の曲は外す");
            Assert.AreEqual(0.8f, bed.Source(second).volume, 1e-4f);
        }

        [Test]
        public void AnEmptyTrackPlaysNothingAndLeavesTheMusicAlone()
        {
            bed.Request(MusicCue.Village, -1f);
            Assert.IsFalse(bed.Mix.Sounding, "曲を当てていなければ何も鳴らさない");
            bed.Request(MusicCue.Dive, -1f);
            Run(bed, 2f);
            bed.Request(MusicCue.Village, -1f);
            Assert.AreEqual(MusicCue.Dive, bed.Mix.Current, "鳴っている曲はそのまま");
        }

        [Test]
        public void HaltSilencesAtOnce()
        {
            bed.Request(MusicCue.Dive, -1f);
            Run(bed, 2f);
            bed.Halt();
            Assert.IsFalse(bed.Mix.Sounding);
            for (var i = 0; i < 2; i++)
            {
                Assert.AreEqual(0f, bed.Source(i).volume, "同じフレームのうちに 0");
                Assert.IsNull(bed.Source(i).clip);
            }
        }

        [Test]
        public void TheSnapAtTheCutIsOverWithinAFewFrames()
        {
            bed.Request(MusicCue.DiveLate, -1f);
            Run(bed, 2f);
            bed.Fade(MusicBed.Snap);
            Run(bed, 1f / 60f * 3f, 1f / 60f);
            Assert.IsFalse(bed.Mix.Sounding, "裂け目の消えは 3 フレームのうちに終わる");
        }

        [Test]
        public void AfterASceneLoadTheMusicStaysOnlyIfSomeoneAskedOrItWasCarried()
        {
            Assert.IsTrue(MusicBed.Keeps(10, 10, false), "読んだフレームに頼まれた（Awake・Start）");
            Assert.IsTrue(MusicBed.Keeps(11, 10, false), "最初の場面の Start が次のフレームに来た");
            Assert.IsFalse(MusicBed.Keeps(9, 10, false), "前の場面で頼まれた曲は、新しい場面の曲ではない");
            Assert.IsTrue(MusicBed.Keeps(9, 10, true), "Carry した時は越えて流す");
        }

        [Test]
        public void TheVillageEntryPlaysOnlyInTheMorningVillage()
        {
            Assert.AreEqual(MusicCue.Village, MusicTable.Arrival("Village", false, "Village"), "場面 9 の村");
            Assert.AreEqual(MusicCue.None, MusicTable.Arrival("Village", true, "Village"), "場面 6 の夕方の庭では鳴らさない");
            Assert.AreEqual(MusicCue.None, MusicTable.Arrival("Drive", false, "Village"));
            Assert.AreEqual(MusicCue.None, MusicTable.Arrival("Village", false, ""), "村の名が空なら何もしない");
        }

        [Test]
        public void DiveSwitchesToTheLateCueExactlyWhenCutOpensAndNeverGoesBack()
        {
            const int cutAfter = 8;
            var chain = new DiveChain(16, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, cutAfter, new System.Random(1));
            Assert.AreEqual(MusicCue.Dive, DiveDirector.Music(chain.CanCut), "最初の記憶は前半の曲");
            var warmedAt = -1;
            for (var i = 1; i <= 10; i++)
            {
                var before = DiveDirector.Music(chain.CanCut);
                if (warmedAt < 0 && DiveDirector.Readies(chain.CanCut, chain.Hops, cutAfter)) warmedAt = chain.Hops;
                chain.Hop(i);
                var after = DiveDirector.Music(chain.CanCut);
                if (chain.Hops < cutAfter) Assert.AreEqual(MusicCue.Dive, after, "押せるようになるまでは前半のまま: " + chain.Hops);
                else Assert.AreEqual(MusicCue.DiveLate, after, "押せるようになった記憶から Parkside: " + chain.Hops);
                if (chain.Hops == cutAfter) Assert.AreEqual(MusicCue.Dive, before, "渡るのはこの一度");
            }
            Assert.AreEqual(cutAfter - 1, warmedAt, "押せるようになる一つ手前で Parkside を読み始める");
            chain.Hop(0);
            Assert.AreEqual(MusicCue.DiveLate, DiveDirector.Music(chain.CanCut), "前に潜った人へ戻っても前半へは戻らない");
        }

        [Test]
        public void TheDriveMusicRunsFromTheChipsBandOn()
        {
            Assert.IsFalse(DriveDirector.Scored(0, 0), "チップの帯は、調べて独白が出るまで流さない");
            Assert.IsTrue(DriveDirector.Scored(1, 0), "思い出して後の帯から始める時は流しておく");
            Assert.IsTrue(DriveDirector.Scored(2, 0));
            Assert.IsFalse(DriveDirector.Scored(2, -1), "チップの帯が無ければ流さない");
        }

        [Test]
        public void TheDriveMusicIsSilentWhenTheDoorShuts()
        {
            var times = new ArrivalTimes { stop = 11.4f, settle = 5.7f, gap = 0.6f, hold = 2f };
            var clock = new ArrivalClock();
            var mix = new MusicMix();
            mix.Play(MusicCue.Drive, 3f, 6f);
            mix.Front.Fresh = false;
            for (var t = 0f; t < 3f; t += 0.02f) mix.Tick(0.02f);
            Assert.AreEqual(12f, DriveDirector.MusicFade(times), 1e-4f, "止まる音 + 間");

            // 黒へ入ったフレーム（DriveDirector.Arrive(0)）で消し始める
            clock.Begin();
            clock.Tick(0f, times);
            Assert.IsTrue(clock.TakeStop());
            mix.FadeOut(DriveDirector.MusicFade(times));
            var dt = 1f / 60f;
            var frames = 0;
            while (!clock.TakeDoor())
            {
                clock.Tick(dt, times);
                mix.Tick(dt);
                Assert.Less(++frames, 2000);
                if (clock.Beat == ArrivalBeat.Door) break;
            }
            Assert.Less(mix.Front.Gain, 0.01f, "ドアの音の所では、もうほとんど 0");
            mix.Stop();
            Assert.IsFalse(mix.Sounding, "ドアの音と同じ瞬間に止める");
        }

        [Test]
        public void TheNoticeMusicFadesAcrossTheExitSoundAndTheBlack()
        {
            Assert.AreEqual(2f, SceneFlow.ExitSpan(true, 1.45f, true, 0.55f), 1e-5f, "場面 7 のドア: 出がけの音 1.45 秒 + 黒 0.55 秒");
            Assert.AreEqual(0.55f, SceneFlow.ExitSpan(false, 1.45f, true, 0.55f), 1e-5f, "音が無ければ待たない");
            Assert.AreEqual(1.45f, SceneFlow.ExitSpan(true, 1.45f, false, 0.55f), 1e-5f, "黒へ落とさなければ待たない");
            Assert.AreEqual(0f, SceneFlow.ExitSpan(true, -1f, true, -1f), "負の秒は 0");
        }
    }
}
