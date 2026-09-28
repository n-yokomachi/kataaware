using NUnit.Framework;
using UnityEditor;

namespace HalfAware.Tests
{
    public class SmokeBeatsTests
    {
        /// <summary>間を挟まないときの、i 服目に吸い始める時刻</summary>
        static float Plain(int i)
        {
            return SmokeBeats.FirstDragAt + SmokeBeats.Cycle * i;
        }

        // ---- ジッポ（オーナー、2026-09-28） ------------------------------------------

        [Test]
        public void TheZippoOpensThenCatchesThenTheDragsFollow()
        {
            Assert.Greater(SmokeBeats.ZippoAt, 0f, "くわえてから開ける");
            Assert.Greater(SmokeBeats.LitAt, SmokeBeats.ZippoAt, "開けてから点く");
            Assert.Less(SmokeBeats.LitAt, SmokeBeats.ZippoAt + SmokeBeats.ZippoSeconds, "点くのはジッポの音の途中（閉じる前）");
            Assert.Less(SmokeBeats.LitAt, SmokeBeats.FirstDragAt, "火が点いてから吸う");
        }

        [Test]
        public void TheCigaretteCatchesTheMomentTheZippoStrikes()
        {
            // 点火したタイミングで、煙草に火が移った音を鳴らす。煙もそこから
            Assert.AreEqual(SmokeBeats.ZippoAt + SmokeBeats.StrikeInZippo, SmokeBeats.LitAt, 1e-4f);
            Assert.AreEqual(SmokeBeats.LitAt, SmokeBeats.SmokeAt, 1e-4f);
        }

        [Test]
        public void SheTurnsOnlyAfterBothSoundsHaveEnded()
        {
            // ジッポの音（閉じるまで）と火が移った音が鳴り終わってから向き直す
            Assert.GreaterOrEqual(SmokeBeats.TurnAt, SmokeBeats.ZippoAt + SmokeBeats.ZippoSeconds - 1e-4f);
            Assert.GreaterOrEqual(SmokeBeats.TurnAt, SmokeBeats.LitAt + SmokeBeats.LitSeconds - 1e-4f);
            Assert.AreEqual(SmokeBeats.FirstDragAt, SmokeBeats.TurnAt, 1e-4f, "向き直さなければ、そこで吸い始める");
        }

        [TestCase("Assets/Audio/Zippo.wav", SmokeBeats.ZippoSeconds)]
        [TestCase("Assets/Audio/CigaretteLit.wav", SmokeBeats.LitSeconds)]
        public void TheTimetableKnowsTheLengthOfTheSounds(string path, float seconds)
        {
            // 素材を替えたら時刻表も合わせる。Web では鳴らす前に長さが読めないことがあるので、長さは時刻表が持つ
            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(path);
            Assert.That(clip, Is.Not.Null, path);
            Assert.AreEqual(seconds, clip.length, 0.01f, path);
        }

        [Test]
        public void TheStrikeFallsBetweenTheLidOpeningAndClosing()
        {
            // 切り出しの中で、蓋を開ける音は 0.35 秒、閉じる音は 2.43 秒から。点火はそのあいだ
            Assert.That(SmokeBeats.StrikeInZippo, Is.GreaterThan(0.5f).And.LessThan(2.4f));
        }

        // ---- 吸う・吐く ----------------------------------------------------------

        [Test]
        public void SheBreathesInBeforeSheBreathesOut()
        {
            for (var i = 0; i < 4; i++)
                Assert.Less(SmokeBeats.DragAt(i, 4), SmokeBeats.BlowAt(i, 4));
        }

        [Test]
        public void TheDragsComeOneCycleApart()
        {
            // 最後の一服には間が入るので、途中どうしで測る
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.DragAt(2, 5) - SmokeBeats.DragAt(1, 5), 1e-4f);
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.BlowAt(3, 5) - SmokeBeats.BlowAt(2, 5), 1e-4f);
        }

        [Test]
        public void OneBlowFinishesBeforeTheNextDrag()
        {
            Assert.LessOrEqual(SmokeBeats.BlowAt(0, 3) + SmokeBeats.BlowSeconds, SmokeBeats.DragAt(1, 3),
                "吐き終わってから次を吸う");
        }

        [Test]
        public void TheCardComesJustAfterSheBreathesOut()
        {
            for (var i = 0; i < SmokeBeats.Drags; i++)
            {
                Assert.AreEqual(SmokeBeats.CardAfterBlow,
                    SmokeBeats.CardAt(i, SmokeBeats.Drags) - SmokeBeats.BlowAt(i, SmokeBeats.Drags), 1e-4f);
                Assert.Less(SmokeBeats.CardAt(i, SmokeBeats.Drags),
                    SmokeBeats.BlowAt(i, SmokeBeats.Drags) + SmokeBeats.BlowSeconds,
                    "まだ吐いている最中に暗くなる");
            }
        }

        [Test]
        public void TheWholeThingCoversEveryDrag()
        {
            var total = SmokeBeats.Total(SmokeBeats.Drags);
            var last = SmokeBeats.BlowAt(SmokeBeats.Drags - 1, SmokeBeats.Drags) + SmokeBeats.BlowSeconds;
            Assert.Greater(total, last, "最後に吐き終わってからも間がある");
            Assert.AreEqual(SmokeBeats.TailSeconds, total - last, 1e-4f);
        }

        [Test]
        public void NoDragsMeansNothingToWaitFor()
        {
            Assert.AreEqual(SmokeBeats.FirstDragAt, SmokeBeats.Total(0), 1e-4f);
        }

        [Test]
        public void MoreDragsTakeLonger()
        {
            Assert.Greater(SmokeBeats.Total(3), SmokeBeats.Total(2));
            Assert.AreEqual(SmokeBeats.Cycle, SmokeBeats.Total(3) - SmokeBeats.Total(2), 1e-3f);
        }

        [Test]
        public void SheSmokesTwice()
        {
            // 2026-09-28 にオーナーの指示で 3 から 2 に減らした（「吸う音吐く音は１回ずつ減らす」）
            Assert.AreEqual(2, SmokeBeats.Drags);
        }

        // ---- 向き直す間（場面 1） ------------------------------------------------

        [Test]
        public void TheTurnPushesEveryDragAndBlowBackByTheSameAmount()
        {
            const float turn = 2.9f;
            for (var i = 0; i < SmokeBeats.Drags; i++)
            {
                Assert.AreEqual(turn, SmokeBeats.DragAt(i, SmokeBeats.Drags, turn) - SmokeBeats.DragAt(i, SmokeBeats.Drags), 1e-4f);
                Assert.AreEqual(turn, SmokeBeats.BlowAt(i, SmokeBeats.Drags, turn) - SmokeBeats.BlowAt(i, SmokeBeats.Drags), 1e-4f);
                Assert.AreEqual(turn, SmokeBeats.CardAt(i, SmokeBeats.Drags, turn) - SmokeBeats.CardAt(i, SmokeBeats.Drags), 1e-4f);
            }
            Assert.AreEqual(turn, SmokeBeats.Total(SmokeBeats.Drags, turn) - SmokeBeats.Total(SmokeBeats.Drags), 1e-4f);
            Assert.GreaterOrEqual(SmokeBeats.DragAt(0, SmokeBeats.Drags, turn), SmokeBeats.TurnAt + turn - 1e-4f, "向き直し終えてから吸う");
        }

        [Test]
        public void ANegativeTurnIsNoTurn()
        {
            Assert.AreEqual(SmokeBeats.DragAt(0, 2), SmokeBeats.DragAt(0, 2, -1f), 1e-4f);
            Assert.AreEqual(SmokeBeats.Total(2), SmokeBeats.Total(2, -1f), 1e-4f);
        }

        [Test]
        public void TheOnlyCardComesJustAfterTheLastBlow()
        {
            // 場面 1 は二服目を吐いたところで一度だけ暗くする。一服目の吐きより後、吐いている最中
            const float turn = 2.9f;
            var last = SmokeBeats.Drags - 1;
            var card = SmokeBeats.CardAt(last, SmokeBeats.Drags, turn);
            Assert.Greater(card, SmokeBeats.BlowAt(0, SmokeBeats.Drags, turn) + SmokeBeats.BlowSeconds, "一服目は暗くならない");
            Assert.Less(card, SmokeBeats.BlowAt(last, SmokeBeats.Drags, turn) + SmokeBeats.BlowSeconds, "吐いている最中に暗くなる");
        }

        [Test]
        public void OnlyTheLastOneIsTheLast()
        {
            Assert.IsTrue(SmokeBeats.IsLast(2, 3));
            Assert.IsFalse(SmokeBeats.IsLast(1, 3));
            Assert.IsFalse(SmokeBeats.IsLast(0, 0), "吸わないなら最後も無い");
        }

        [Test]
        public void SheTakesABreathBeforeTheLastDrag()
        {
            Assert.AreEqual(Plain(0), SmokeBeats.DragAt(0, 3), 1e-4f, "途中の一服はそのまま");
            Assert.AreEqual(SmokeBeats.LastPauseSeconds, SmokeBeats.DragAt(2, 3) - Plain(2), 1e-4f);
        }

        [Test]
        public void SheHoldsItLongerBeforeTheLastBlow()
        {
            var held = SmokeBeats.DragSeconds + SmokeBeats.HoldSeconds;
            Assert.AreEqual(held, SmokeBeats.BlowAt(0, 3) - SmokeBeats.DragAt(0, 3), 1e-4f, "途中はそのまま");
            Assert.AreEqual(held + SmokeBeats.LastPauseSeconds,
                SmokeBeats.BlowAt(2, 3) - SmokeBeats.DragAt(2, 3), 1e-4f);
        }

        [Test]
        public void ThePausesPushTheEndingBack()
        {
            var plain = Plain(SmokeBeats.Drags - 1) + SmokeBeats.DragSeconds + SmokeBeats.HoldSeconds
                + SmokeBeats.BlowSeconds + SmokeBeats.TailSeconds;
            Assert.AreEqual(SmokeBeats.LastPauseSeconds * 2f, SmokeBeats.Total(SmokeBeats.Drags) - plain, 1e-4f);
        }

        [Test]
        public void SettleKeepsTheTimetableWhenTheLiftIsQuick()
        {
            // 予定より早く明けたら、予定どおりに独白へ移る
            Assert.That(SmokeBeats.Settle(10f, 14f, 1.1f), Is.EqualTo(14f).Within(1e-4f));
        }

        [Test]
        public void SettleWaitsAfterASlowLift()
        {
            // 予定を過ぎてから明けたら、明け終わりからひと呼吸おく
            Assert.That(SmokeBeats.Settle(16f, 14f, 1.1f), Is.EqualTo(17.1f).Within(1e-4f));
        }

        [Test]
        public void SettleNeverGoesBackwards()
        {
            Assert.That(SmokeBeats.Settle(16f, 14f, -5f), Is.EqualTo(16f).Within(1e-4f));
        }
    }
}
