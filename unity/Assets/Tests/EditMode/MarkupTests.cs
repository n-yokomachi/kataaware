using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>
    /// 台詞の原稿の書き方（docs/scenario/README.md、オーナー、2026-09-28）を、字幕の窓・リストの枠・カード・ログで組めるか。
    /// 親字の決め方、傍点、改行、列の区切り、TextMeshPro の書式の素通し
    /// </summary>
    public class MarkupTests
    {
        // ---- ルビの親字 --------------------------------------------------

        [TestCase("身体<からだ>に籠っていた", "｜身体《からだ》に籠っていた")]
        [TestCase("今日のメモリ<記憶>チップ", "今日の｜メモリ《記憶》チップ")]
        [TestCase("『双鶴<シュアンフゥ>』という名", "『｜双鶴《シュアンフゥ》』という名")]
        [TestCase("Stock<在庫>  47 pcs", "｜Stock《在庫》  47 pcs")]
        [TestCase("｜No firewall<防壁なし>", "｜No firewall《防壁なし》")]
        [TestCase("Condition<条件>:  ｜Name-call time<名前を呼ばれた時刻>", "｜Condition《条件》:  ｜Name-call time《名前を呼ばれた時刻》")]
        [TestCase("｜Quick-filter hits<候補の簡易抽出結果>:  526,232,318", "｜Quick-filter hits《候補の簡易抽出結果》:  526,232,318")]
        [TestCase("19時35分　倫敦<ロンドン>　自室", "19時35分　｜倫敦《ロンドン》　自室")]
        public void TheBaseIsTheRunOfTheSameKindJustBefore(string written, string normalized)
        {
            Assert.AreEqual(normalized, Ruby.Normalize(written));
        }

        [Test]
        public void KanjiRunsTakeTheRepeatMarks()
        {
            // 々〆ヶ は漢字の続き。仮名が挟まれば、そこで切れる
            Assert.AreEqual("｜人々《ひとびと》", Ruby.Normalize("人々<ひとびと>"));
            Assert.AreEqual("｜三ヶ月《さんかげつ》", Ruby.Normalize("三ヶ月<さんかげつ>"));
            Assert.AreEqual("の｜記憶《きおく》", Ruby.Normalize("の記憶<きおく>"));
        }

        [Test]
        public void KatakanaRunsTakeTheLongVowelMark()
        {
            Assert.AreEqual("は｜ターミナル《端末》", Ruby.Normalize("はターミナル<端末>"));
            // 中黒は続きに入れない。区切りたくなければ ｜ で頭を決める
            Assert.AreEqual("ナーヴ・｜ターミナル《端末》", Ruby.Normalize("ナーヴ・ターミナル<端末>"));
            Assert.AreEqual("｜ナーヴ・ターミナル《端末》", Ruby.Normalize("｜ナーヴ・ターミナル<端末>"));
        }

        [Test]
        public void LatinRunsTakeDigitsHyphensAndApostrophes()
        {
            Assert.AreEqual("the ｜Name-call《呼名》", Ruby.Normalize("the Name-call<呼名>"));
            Assert.AreEqual("a ｜driver's《運転手の》", Ruby.Normalize("a driver's<運転手の>"));
            Assert.AreEqual("｜R2《アールツー》", Ruby.Normalize("R2<アールツー>"));
        }

        [Test]
        public void TheOldWayStillReads()
        {
            // 場面 3 などが使う「｜親字《るび》」はそのまま
            const string old = "この｜倫敦《ロンドン》の路地";
            Assert.AreEqual(old, Ruby.Normalize(old));
            Assert.AreEqual(Ruby.Expand(old), Ruby.Expand("この倫敦<ロンドン>の路地"));
            Assert.AreEqual("この倫敦の路地", Ruby.Plain("この倫敦<ロンドン>の路地"));
        }

        [Test]
        public void NormalizingTwiceChangesNothing()
        {
            const string text = "身体<からだ>に<dot>他人</dot>の<br/>｜No firewall<防壁なし>";
            var once = Ruby.Normalize(text);
            Assert.AreEqual(once, Ruby.Normalize(once));
        }

        [Test]
        public void ARubyWithNothingBeforeItIsLeftAsWritten()
        {
            // 直前が約物か空白で、｜ も無ければ親字が決まらない
            Assert.AreEqual("「<るび>」", Ruby.Normalize("「<るび>」"));
        }

        // ---- 書式の素通し --------------------------------------------------

        [TestCase("<size=30>")]
        [TestCase("</size>")]
        [TestCase("<voffset=1.05em>")]
        [TestCase("<space=-2em>")]
        [TestCase("<pos=12.5em>")]
        [TestCase("<b>")]
        [TestCase("<#ffcc00>")]
        [TestCase("<sprite=\"MouseLeft\" name=\"large\" tint=1>")]
        [TestCase("<nobr>")]
        public void TextMeshProTagsAreNotRuby(string tag)
        {
            var text = "倫敦" + tag + "自室";
            Assert.AreEqual(text, Ruby.Normalize(text), "書式の名前に当たる物はルビにしない");
        }

        [Test]
        public void TheCardKeepsItsSizeAndGetsItsRuby()
        {
            var card = "<size=30>2166年8月15日 19時35分　倫敦<ロンドン>　自室</size>";
            var made = Ruby.Expand(card);
            StringAssert.StartsWith("<size=30>2166年8月15日 19時35分　", made);
            StringAssert.EndsWith("倫敦　自室</size>", made);
            StringAssert.Contains("ロンドン</size></voffset>", made, "ルビの書式に直る");
            Assert.AreEqual("<size=30>2166年8月15日 19時35分　倫敦　自室</size>", Ruby.Plain(card));
        }

        [Test]
        public void ExpandingTheExpandedChangesNothing()
        {
            var made = Ruby.Expand("身体<からだ>に");
            Assert.AreEqual(made, Ruby.Expand(made), "書式に直した後の文をもう一度通しても、書式をルビと取り違えない");
        }

        // ---- 傍点 --------------------------------------------------------

        [Test]
        public void DotsGoOnEveryCharacter()
        {
            Assert.AreEqual("｜他《﹅》｜人《﹅》の", Ruby.Normalize("<dot>他人</dot>の"));
            Assert.AreEqual("｜a《﹅》 ｜b《﹅》", Ruby.Normalize("<dot>a b</dot>"), "空白には打たない");
        }

        [Test]
        public void DotsAreDrawnAsSmallMarksCentredOverEachCharacter()
        {
            var made = Ruby.Expand("<dot>他人</dot>は");
            StringAssert.Contains(Ruby.DotGlyph, made);
            StringAssert.DoesNotContain(Ruby.DotMark, made, "﹅ は印で、画面には ・ を打つ");
            StringAssert.Contains("<voffset=" + Num(Ruby.DotLift) + "em>", made);
            StringAssert.Contains("<size=" + Num(Ruby.DotScale * 100f) + "%>", made);
            Assert.AreEqual("他人は", Ruby.Plain("<dot>他人</dot>は"));
            Assert.IsTrue(Ruby.Has("<dot>他人</dot>は"), "傍点のある文も行を開ける");
        }

        [Test]
        public void TheDotSitsJustAboveTheBaseGlyphs()
        {
            // 点の下の端が、漢字の上の端よりルビと同じ隙間だけ上。ルビより上へは出ない（行の開け方はルビのぶんで足りる）
            Assert.AreEqual(Ruby.BaseTop + Ruby.Gap, Ruby.DotLift + Ruby.DotBottom, 1e-4f);
            Assert.Less(Ruby.DotLift + 0.486f * Ruby.DotScale, Ruby.Lift + Ruby.RubyTop * Ruby.Scale);
        }

        // ---- 改行と折り返し --------------------------------------------------

        [Test]
        public void BreaksBecomeNewLines()
        {
            Assert.AreEqual("一行目\n二行目", Ruby.Normalize("一行目<br/>二行目"));
            Assert.AreEqual("一行目\n二行目", Ruby.Normalize("一行目<br>二行目"));
            Assert.AreEqual(2, SubtitleBox.LineCount(SubtitleBox.Wrap("一行目<br/>二行目", 40)));
        }

        [Test]
        public void AWrittenBreakIsKeptAndOnlyTheLongLineIsWrapped()
        {
            // 1 行目は窓に入り、2 行目だけが窓の幅を超える
            var text = "短い一行目、<br/>" + "世間ではホロコンソールが人気だが、私はもっぱら物理モニターを好んで使っている。";
            var made = SubtitleBox.Wrap(text, 40).Split('\n');
            Assert.AreEqual(3, made.Length, "書いた改行はそのまま、長い行だけを二つに割る");
            Assert.AreEqual("短い一行目、", made[0]);
            foreach (var line in made) Assert.LessOrEqual(ListFormat.Units(line), 40, line);
        }

        [Test]
        public void WrappingCountsOnlyTheBaseOfARuby()
        {
            // 「身体」のルビは幅に数えない。20 字ちょうどは割らない
            var text = "身体<からだ>" + new string('あ', 18);
            Assert.AreEqual(1, SubtitleBox.LineCount(SubtitleBox.Wrap(text, 40)));
        }

        [Test]
        public void WrappingNeverCutsThroughATagOrADottedRun()
        {
            var text = "<size=30>" + new string('あ', 25) + "</size>" + "<dot>他人の記憶に潜った後</dot>" + new string('い', 25);
            foreach (var line in SubtitleBox.Wrap(text, 40).Split('\n'))
            {
                Assert.AreEqual(line.Split('<').Length, line.Split('>').Length, "書式の途中で切れている: " + line);
                int f, t, rf, rt;
                for (var i = line.IndexOf(Ruby.Head); i >= 0; i = line.IndexOf(Ruby.Head, i + 1))
                    Assert.IsTrue(Ruby.Group(line, i, out f, out t, out rf, out rt), "傍点の途中で切れている: " + line);
            }
        }

        // ---- 表の列 ------------------------------------------------------

        [Test]
        public void OneHalfWidthSpaceStaysInsideTheColumn()
        {
            var cells = ListFormat.Split("2166/08/10  Bought<仕入>  30 pcs  Stock<在庫>  47 pcs", ListFormat.MaxColumns);
            Assert.AreEqual(5, cells.Count);
            Assert.AreEqual("30 pcs", cells[2]);
            Assert.AreEqual("｜Stock《在庫》", cells[3]);
            cells = ListFormat.Split("Period<期間>:  2156/03/02 - 2156/03/03", ListFormat.MaxColumns);
            Assert.AreEqual(2, cells.Count);
            Assert.AreEqual("2156/03/02 - 2156/03/03", cells[1]);
            cells = ListFormat.Split("Condition<条件>:  ｜Name-call time<名前を呼ばれた時刻>", ListFormat.MaxColumns);
            Assert.AreEqual(2, cells.Count);
            Assert.AreEqual("｜Name-call time《名前を呼ばれた時刻》", cells[1]);
        }

        [Test]
        public void FullWidthSpacesAndDoubleSpacesBothSeparate()
        {
            Assert.AreEqual(3, ListFormat.Split("女　6　『メイ』", 8).Count);
            Assert.AreEqual(3, ListFormat.Split("08/15  #1    Male<男>", 8).Count);
            Assert.AreEqual(1, ListFormat.Split("08/15 #1 Male", 8).Count, "半角の空白 1 つでは切らない");
        }

        [Test]
        public void ASpacedLineWithSingleSpacesIsNotAList()
        {
            Assert.IsFalse(ListFormat.IsList("30 pcs\n5 pcs"));
            Assert.IsTrue(ListFormat.IsList("Sold<売却>  5 pcs\nSold<売却>  4 pcs"));
        }

        [Test]
        public void ARubyColumnIsAsWideAsItsBaseOrItsRubyWhicheverIsWider()
        {
            // 「倫敦」（4）のルビ「ロンドン」は 8 × 0.7 = 5.6 → 6。英字の「Stock」（5）のルビ「在庫」は 2.8 → 3
            Assert.AreEqual(6, Ruby.Width("倫敦<ロンドン>"));
            Assert.AreEqual(5, Ruby.Width("Stock<在庫>"));
            Assert.AreEqual(4, Ruby.Width("<dot>他人</dot>"), "傍点は親字の幅");
            Assert.AreEqual(4, Ruby.Width("<size=30>自室</size>"), "書式は幅に数えない");
        }

        [Test]
        public void ColumnsLineUpWithRubies()
        {
            var made = ListFormat.Compose("倫敦<ロンドン>  a\n自室  b", 0f, false);
            // 1 列目の幅は「倫敦」のルビの 6。2 列目はそこへ間の 2 を足した 8 = 4em から
            var rows = made.Split('\n');
            StringAssert.Contains("<pos=4em>a", rows[0]);
            StringAssert.Contains("<pos=4em>b", rows[1]);
        }

        [Test]
        public void ARubyAtTheHeadOfAColumnDoesNotHangIntoTheColumnBefore()
        {
            // 列の頭（<pos=…> の直後）は行の頭と同じ扱い。広いルビを前の列の上へ掛けない
            var made = Ruby.Expand(ListFormat.Compose("a  倫敦<ロンドン>\nbb  自室", 0f, false));
            StringAssert.Contains("<pos=2em><voffset=", made);
        }

        [Test]
        public void NumberColumnsAreAlignedAtTheirEnds()
        {
            // 原稿で「41」と「 8」のように尻を揃えてある列は、尻を揃える
            var made = ListFormat.Compose("Male<男>    41  a\nMale<男>     8  b", 0f, false).Split('\n');
            StringAssert.Contains("<pos=3em>41", made[0]);
            StringAssert.Contains("<pos=3.5em>8", made[1]);
            // 数で始まらない塊が混ざる列は頭を揃える
            var left = ListFormat.Compose("x  41\ny  abc", 0f, false).Split('\n');
            StringAssert.Contains("<pos=1.5em>41", left[0]);
            StringAssert.Contains("<pos=1.5em>abc", left[1]);
        }

        [Test]
        public void TheScene3And7ListsStillBreakTheSameWay()
        {
            // 場面 3・7 のリスト（今は日本語で、全角の空白で区切ってある）は、今までどおりの列に分かれる
            Assert.AreEqual(new[] { "女", "6", "『メイ』", "2156/03/02 07:14" }, ListFormat.Split("女　6　『メイ』　2156/03/02 07:14", 8).ToArray());
            Assert.AreEqual(new[] { "記憶の日時", "2163年8月16日19時45分" }, ListFormat.Split("記憶の日時　2163年8月16日19時45分", 8).ToArray());
            Assert.AreEqual(new[] { "対象", "防壁なし", "距離 ランダム", "期間 2156年3月2日～2156年3月3日" },
                ListFormat.Split("対象　防壁なし　距離 ランダム　期間 2156年3月2日～2156年3月3日", 8).ToArray());
        }

        static string Num(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
