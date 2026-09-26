using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class DiveEntryTests
    {
        static DiveEntry[] TwoEntries()
        {
            return new[]
            {
                new DiveEntry { seen = new[] { new Seen { name = "A", target = 1 } } },
                new DiveEntry { seen = new[] { new Seen { name = "B", target = 0 } } },
            };
        }

        [Test]
        public void ClosedWhenEveryTargetIsInRange()
        {
            Assert.IsTrue(DiveRoster.Closed(TwoEntries()));
        }

        [Test]
        public void NotClosedWhenATargetIsNegative()
        {
            var all = TwoEntries();
            all[0].seen[0].target = -1;
            Assert.IsFalse(DiveRoster.Closed(all));
        }

        [Test]
        public void NotClosedWhenATargetIsPastTheEndOfTheList()
        {
            var all = TwoEntries();
            all[1].seen[0].target = 2;
            Assert.IsFalse(DiveRoster.Closed(all));
        }

        [Test]
        public void ANullSeenDoesNotFail()
        {
            var all = new[] { new DiveEntry { seen = null } };
            Assert.IsTrue(DiveRoster.Closed(all));
        }

        // ---- 会話の決まり（設計書 7 節） -------------------------------------

        /// <summary>一行を組む。相手が null なら相手を持たない行</summary>
        static Said L(string line, string partner)
        {
            return new Said { line = line, partner = partner };
        }

        /// <summary>ハンナの記憶の形。一行目のあとに老人と三行、娘と四行</summary>
        static Said[] Hanna()
        {
            return new[]
            {
                L("ジョルジョ「ハンナさん」", null),
                L("ハンナ「おはようございます」", "Neighbour"),
                L("ジョルジョ「今日は遅いんだね」", "Neighbour"),
                L("ハンナ「ええ、午後からで」", "Neighbour"),
                L("メイ「ママ！」", "Daughter"),
                L("ハンナ「どうしたの」", "Daughter"),
                L("メイ「体操着、わすれた」", "Daughter"),
                L("ハンナ「……もう」", "Daughter"),
            };
        }

        // 一行目は名を呼ぶ声で、記憶に入った瞬間に出て、決まった秒で消える。送らない
        [Test]
        public void TheCallIsTheFirstLineUntilItsSecondsRunOut()
        {
            var said = Hanna();
            Assert.That(DiveEntry.Calling(said, 0f, 9f), Is.EqualTo("ジョルジョ「ハンナさん」"));
            Assert.That(DiveEntry.Calling(said, 8.99f, 9f), Is.EqualTo("ジョルジョ「ハンナさん」"));
            Assert.That(DiveEntry.Calling(said, 9f, 9f), Is.Null);
            Assert.That(DiveEntry.Calling(said, 30f, 9f), Is.Null);
        }

        [Test]
        public void NothingIsCalledWhenNothingIsSaid()
        {
            Assert.That(DiveEntry.Calling(null, 0f, 9f), Is.Null);
            Assert.That(DiveEntry.Calling(new Said[0], 0f, 9f), Is.Null);
        }

        // 一行目は人を選んで始めるものではないので、相手を書いてあっても会話に数えない
        [Test]
        public void TheFirstLineIsNeverPartOfATalk()
        {
            var said = new[] { L("母「メイ！」", "Mother"), L("メイ「えー」", "Mother") };
            var talks = DiveEntry.Exchanges(said);
            Assert.That(talks.Length, Is.EqualTo(1));
            Assert.That(talks[0].lines, Is.EqualTo(new[] { 1 }));
        }

        // 二行目から、相手が同じ行が続く所が一つの会話になる
        [Test]
        public void LinesWithTheSamePartnerInARowMakeOneTalk()
        {
            var talks = DiveEntry.Exchanges(Hanna());
            Assert.That(talks.Length, Is.EqualTo(2));
            Assert.That(talks[0].partner, Is.EqualTo("Neighbour"));
            Assert.That(talks[0].lines, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(talks[1].partner, Is.EqualTo("Daughter"));
            Assert.That(talks[1].lines, Is.EqualTo(new[] { 4, 5, 6, 7 }));
        }

        // 会話は並びの順にしか始められない。先の相手に目を留めても何も起きない
        [Test]
        public void TalksStartOnlyInOrder()
        {
            var talks = DiveEntry.Exchanges(Hanna());
            Assert.That(DiveEntry.Next(talks, 0), Is.EqualTo("Neighbour"));
            Assert.That(DiveEntry.CanTalk(talks, 0, "Neighbour"), Is.True);
            Assert.That(DiveEntry.CanTalk(talks, 0, "Daughter"), Is.False);
            Assert.That(DiveEntry.Next(talks, 1), Is.EqualTo("Daughter"));
            Assert.That(DiveEntry.CanTalk(talks, 1, "Neighbour"), Is.False);
            Assert.That(DiveEntry.CanTalk(talks, 1, "Daughter"), Is.True);
            Assert.That(DiveEntry.Next(talks, 2), Is.Null);
            Assert.That(DiveEntry.CanTalk(talks, 2, "Daughter"), Is.False);
        }

        // 会話の相手には、その人との会話が済んでから板が出る。
        // 老人との会話を終えれば、娘との会話がまだでも老人へは潜れる
        [Test]
        public void APartnerMayBeDivedOnlyAfterTheirTalk()
        {
            var talks = DiveEntry.Exchanges(Hanna());
            Assert.That(DiveEntry.MayDive(talks, 0, "Neighbour"), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 0, "Daughter"), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 1, "Neighbour"), Is.True);
            Assert.That(DiveEntry.MayDive(talks, 1, "Daughter"), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 2, "Daughter"), Is.True);
        }

        [Test]
        public void ATalkIsFinishedOnlyOnceItHasBeenHeardToTheEnd()
        {
            var talks = DiveEntry.Exchanges(Hanna());
            Assert.That(DiveEntry.Finished(talks, 0, "Neighbour"), Is.False);
            Assert.That(DiveEntry.Finished(talks, 1, "Neighbour"), Is.True);
            Assert.That(DiveEntry.Finished(talks, 1, "Daughter"), Is.False);
            Assert.That(DiveEntry.Finished(talks, 2, "Daughter"), Is.True);
        }

        [Test]
        public void EveryTalkIsDoneOnlyAfterTheLastOne()
        {
            var talks = DiveEntry.Exchanges(Hanna());
            Assert.That(DiveEntry.AllDone(talks, 0), Is.False);
            Assert.That(DiveEntry.AllDone(talks, 1), Is.False);
            Assert.That(DiveEntry.AllDone(talks, 2), Is.True);
        }

        // 会話が一つだけの記憶（メイ・ジョルジョ・エレナの形）
        [Test]
        public void AMemoryWithASingleTalk()
        {
            var said = new[]
            {
                L("エレナ「ジョルジョ」", null),
                L("ジョルジョ「ああ」", "Wife"),
                L("エレナ「新聞、来てる？」", "Wife"),
            };
            var talks = DiveEntry.Exchanges(said);
            Assert.That(talks.Length, Is.EqualTo(1));
            Assert.That(DiveEntry.Next(talks, 0), Is.EqualTo("Wife"));
            Assert.That(DiveEntry.MayDive(talks, 0, "Wife"), Is.False);
            Assert.That(DiveEntry.AllDone(talks, 0), Is.False);
            Assert.That(DiveEntry.Next(talks, 1), Is.Null);
            Assert.That(DiveEntry.MayDive(talks, 1, "Wife"), Is.True);
            Assert.That(DiveEntry.AllDone(talks, 1), Is.True);
        }

        // 会話を持たない人（ジョルジョの記憶の隣の母）は、記憶の会話が全部済むまで板が出ない
        [Test]
        public void SomeoneWithoutATalkWaitsForEveryTalk()
        {
            var talks = DiveEntry.Exchanges(Hanna());
            Assert.That(DiveEntry.Partner(talks, "Mother"), Is.False);
            Assert.That(DiveEntry.CanTalk(talks, 0, "Mother"), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 0, "Mother"), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 1, "Mother"), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 2, "Mother"), Is.True);
        }

        // 済ませる会話が無い人を「済んだ」とは言わない。板を出すかは MayDive が決める
        [Test]
        public void SomeoneWithoutATalkHasNothingFinished()
        {
            var talks = DiveEntry.Exchanges(Hanna());
            Assert.That(DiveEntry.Finished(talks, 2, "Mother"), Is.False);
        }

        // 相手を持たない行は会話に数えず、流さない。前後が同じ相手なら一つの会話のまま
        [Test]
        public void LinesWithoutAPartnerAreNeitherCountedNorSaid()
        {
            var said = new[]
            {
                L("呼ぶ声", null),
                L("一", "A"),
                L("抜け", null),
                L("二", "A"),
                L("抜け", ""),
                L("三", "B"),
            };
            var talks = DiveEntry.Exchanges(said);
            Assert.That(talks.Length, Is.EqualTo(2));
            Assert.That(talks[0].partner, Is.EqualTo("A"));
            Assert.That(talks[0].lines, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(talks[1].partner, Is.EqualTo("B"));
            Assert.That(talks[1].lines, Is.EqualTo(new[] { 5 }));
        }

        // 同じ人へ戻る会話。その人の板は、その人との最後の会話が済むまで出ない
        [Test]
        public void ComingBackToTheSamePersonKeepsTheirPanelUntilTheLastTalk()
        {
            var said = new[]
            {
                L("呼ぶ声", null),
                L("一", "A"),
                L("二", "B"),
                L("三", "A"),
            };
            var talks = DiveEntry.Exchanges(said);
            Assert.That(talks.Length, Is.EqualTo(3));
            Assert.That(DiveEntry.Next(talks, 1), Is.EqualTo("B"));
            Assert.That(DiveEntry.CanTalk(talks, 1, "A"), Is.False);
            Assert.That(DiveEntry.Finished(talks, 1, "A"), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 1, "A"), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 2, "B"), Is.True);
            Assert.That(DiveEntry.CanTalk(talks, 2, "A"), Is.True);
            Assert.That(DiveEntry.MayDive(talks, 2, "A"), Is.False);
            Assert.That(DiveEntry.Finished(talks, 3, "A"), Is.True);
            Assert.That(DiveEntry.MayDive(talks, 3, "A"), Is.True);
        }

        // 二行目から相手を持たない記憶（団地の四本のほかの十二本）は、板がすぐ出る
        [Test]
        public void AMemoryWithNoTalkOpensEveryPanelAtOnce()
        {
            var said = new[] { L("ローザ「アルベルト、帰りましょう」", null), L("ソフィア「おじいちゃん、手」", null) };
            var talks = DiveEntry.Exchanges(said);
            Assert.That(talks, Is.Empty);
            Assert.That(DiveEntry.Next(talks, 0), Is.Null);
            Assert.That(DiveEntry.AllDone(talks, 0), Is.True);
            Assert.That(DiveEntry.MayDive(talks, 0, "Granddaughter"), Is.True);
            Assert.That(DiveEntry.CanTalk(talks, 0, "Granddaughter"), Is.False);
        }

        [Test]
        public void NoSaidMeansNoTalk()
        {
            Assert.That(DiveEntry.Exchanges(null), Is.Empty);
            Assert.That(DiveEntry.Exchanges(new Said[0]), Is.Empty);
            Assert.That(DiveEntry.Exchanges(new[] { L("呼ぶ声", null) }), Is.Empty);
        }

        // 名前の無い人には何も出さない
        [Test]
        public void NobodyWithoutANameIsDivedOrTalkedTo()
        {
            var talks = DiveEntry.Exchanges(Hanna());
            Assert.That(DiveEntry.MayDive(talks, 2, null), Is.False);
            Assert.That(DiveEntry.MayDive(talks, 2, ""), Is.False);
            Assert.That(DiveEntry.CanTalk(talks, 0, null), Is.False);
        }

        // ---- 〔区切り〕 ---------------------------------------------------------

        /// <summary>区切りの後の行を組む</summary>
        static Said C(string line, string partner)
        {
            return new Said { line = line, partner = partner, cut = true };
        }

        /// <summary>メイの記憶の形。階段の下で母と二行、〔区切り〕、三階の戸口で母と二行</summary>
        static Said[] Mei()
        {
            return new[]
            {
                L("ハンナ「メイ！」", null),
                L("メイ「えー」", "Mother"),
                L("ハンナ「水筒！」", "Mother"),
                C("ハンナ「毎日でしょ」", "Mother"),
                L("メイ「いってきます」", "Mother"),
            };
        }

        // 区切りでは、同じ相手でも会話を二つに分ける。区切りの後の会話は区切りの番号を持つ
        [Test]
        public void ACutSplitsATalkWithTheSamePartner()
        {
            var talks = DiveEntry.Exchanges(Mei());
            Assert.That(talks.Length, Is.EqualTo(2));
            Assert.That(talks[0].lines, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(talks[0].Cut, Is.False);
            Assert.That(talks[0].stop, Is.EqualTo(-1));
            Assert.That(talks[1].lines, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(talks[1].Cut, Is.True);
            Assert.That(talks[1].stop, Is.EqualTo(0));
        }

        // 区切りの前の会話を終えても、区切りの後の会話が済むまでは、その相手に板を出さない
        [Test]
        public void ThePanelWaitsForTheTalkAfterTheCut()
        {
            var talks = DiveEntry.Exchanges(Mei());
            Assert.That(DiveEntry.MayDive(talks, 1, "Mother"), Is.False);
            Assert.That(DiveEntry.CanTalk(talks, 1, "Mother"), Is.True);
            Assert.That(DiveEntry.MayDive(talks, 2, "Mother"), Is.True);
        }

        // 区切りは記憶の頭から数える。相手の替わる所に付いた区切りも数え、
        // 区切りの無い会話を挟んでも番号は続く（記憶 13 のダニエル）
        [Test]
        public void CutsAreCountedFromTheTopOfTheMemory()
        {
            var said = new[]
            {
                L("リンダ「ダニエル」", null),
                L("ダニエル「分かってる」", "Mother"),
                C("ダニエル「おはよ」", "Father"),
                L("リンダ「ランチ」", "Mother"),
                C("ダニエル「いってきます」", "Mother"),
            };
            var talks = DiveEntry.Exchanges(said);
            Assert.That(talks.Length, Is.EqualTo(4));
            Assert.That(talks[0].stop, Is.EqualTo(-1));
            Assert.That(talks[1].stop, Is.EqualTo(0));
            Assert.That(talks[1].partner, Is.EqualTo("Father"));
            Assert.That(talks[2].stop, Is.EqualTo(-1));
            Assert.That(talks[3].stop, Is.EqualTo(1));
            Assert.That(talks[3].lines, Is.EqualTo(new[] { 4 }));
        }

        // 相手を持たない行に付いた区切りは、次の相手を持つ行へ持ち越す
        [Test]
        public void ACutOnALineWithoutAPartnerCarriesOver()
        {
            var said = new[]
            {
                L("呼ぶ声", null),
                L("一", "A"),
                C("流さない", null),
                L("二", "A"),
            };
            var talks = DiveEntry.Exchanges(said);
            Assert.That(talks.Length, Is.EqualTo(2));
            Assert.That(talks[1].lines, Is.EqualTo(new[] { 3 }));
            Assert.That(talks[1].stop, Is.EqualTo(0));
        }
    }
}
