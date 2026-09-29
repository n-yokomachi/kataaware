using NUnit.Framework;

namespace HalfAware.Tests
{
    public class RubyTests
    {
        /// <summary>Ruby と同じ書き方の数（小数 3 桁まで、末尾の 0 は落とす）</summary>
        static string Num(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Space(float em)
        {
            return "<space=" + Num(em) + "em>";
        }

        [Test]
        public void NothingToRubyLeavesTheTextAlone()
        {
            Assert.AreEqual("倫敦", Ruby.Over("倫敦", null));
            Assert.AreEqual("倫敦", Ruby.Over("倫敦", ""));
            Assert.AreEqual("", Ruby.Over("", "ロンドン"));
        }

        [Test]
        public void TheBaseTextSurvivesIntact()
        {
            StringAssert.Contains("倫敦", Ruby.Over("倫敦", "ロンドン"));
            StringAssert.Contains("ロンドン", Ruby.Over("倫敦", "ロンドン"));
        }

        /// <summary>ルビの頭（親字の頭からの em）。広いルビでは親字の前を空けたあとの所から測る</summary>
        static void Layout(float baseEm, float rubyEm, out float start, out float back, out float pad)
        {
            var over = System.Math.Max(0f, (rubyEm - baseEm) * 0.5f);
            pad = System.Math.Max(0f, over - Ruby.Hang);
            start = pad + (baseEm - rubyEm) * 0.5f;
            back = start + rubyEm - pad;
        }

        [Test]
        public void ItStepsBackByWhatTheRubyAdvanced()
        {
            // 倫敦 は全角 2 字 = 2 em。ロンドン は全角 4 字 = 4 em を Scale 倍（0.7 で 2.8 em）。
            // 戻すのは、ルビの頭から進んだぶん。親字の幅（2 em）で戻すと親字がずれる
            float start, back, pad;
            Layout(2f, 4f * Ruby.Scale, out start, out back, out pad);
            var made = Ruby.Over("倫敦", "ロンドン");
            StringAssert.Contains(Space(-back), made);
            StringAssert.DoesNotContain("<space=0em>", made);
        }

        [Test]
        public void ShortRubySitsInTheMiddle()
        {
            // 生活基盤 = 4 em、きばん = 3 字 × Scale。左右に余るぶんの半分だけ寄せてから、寄せたぶんと進んだぶんを戻す
            var ruby = 3f * Ruby.Scale;
            var lead = (4f - ruby) * 0.5f;
            var made = Ruby.Over("生活基盤", "きばん");
            StringAssert.StartsWith(Space(lead), made, "寄せる空きはルビの外に、親字の em で置く");
            StringAssert.Contains(Space(-(lead + ruby)), made);
            StringAssert.EndsWith("生活基盤", made, "ルビが狭ければ後ろは空けない");
        }

        [Test]
        public void WideRubyIsCentredAndHangsOverTheNeighbours()
        {
            // 羅府 = 2 em、ロサンゼルス = 6 字 × Scale（0.7 で 4.2 em）。片側へ 1.1 em はみ出す。
            // 両脇の字の上へ Hang（0.5 em）まで掛け、残り 0.6 em を親字の前と後ろに同じだけ空ける
            var ruby = 6f * Ruby.Scale;
            float start, back, pad;
            Layout(2f, ruby, out start, out back, out pad);
            Assert.That(pad, Is.GreaterThan(0f));
            Assert.AreEqual(pad + 1f, start + ruby * 0.5f, 1e-4f, "ルビの真ん中が親字の真ん中に揃う");
            var made = Ruby.Over("羅府", "ロサンゼルス");
            StringAssert.StartsWith(Space(start), made, "前の字の上へ掛けるので、頭は戻す");
            StringAssert.Contains(Space(-back), made);
            StringAssert.EndsWith(Space(pad), made, "後ろも前と同じだけ空ける");
        }

        [Test]
        public void SlightlyWideRubyOnlyHangsAndLeavesNoGap()
        {
            // 最小の生活基盤 = 7 em、Minimum Infrastructure = 字の送りの実寸で 11.11 em × Scale（0.7 で 7.78 em）。
            // はみ出すのは片側 0.39 em で Hang の内なので、親字の前後は空けない
            var ruby = Ruby.Em("Minimum Infrastructure") * Ruby.Scale;
            Assert.AreEqual(11.11f, Ruby.Em("Minimum Infrastructure"), 1e-3f);
            float start, back, pad;
            Layout(7f, ruby, out start, out back, out pad);
            Assert.AreEqual(0f, pad, 1e-5f);
            var made = Ruby.Over("最小の生活基盤", "Minimum Infrastructure");
            StringAssert.Contains(Space(-back), made);
            StringAssert.EndsWith("最小の生活基盤", made);
            StringAssert.DoesNotContain("<space=-7em>", made, "親字の幅で戻すと、ルビの長さぶんずれる");
        }

        [Test]
        public void TheWidthIsTheFontsAdvance()
        {
            // 全角は 1 em。半角の英数は Noto Sans JP の送り（半角 1 字 = 0.5 em ではない）
            Assert.AreEqual(2f, Ruby.Em("倫敦"), 1e-5f);
            Assert.AreEqual(1f, Ruby.Em("・"), 1e-5f);
            Assert.AreEqual(0.728f, Ruby.Em("H"), 1e-5f);
            Assert.AreEqual(0.224f, Ruby.Em(" "), 1e-5f);
            // 空白・&・. を含む英語の親字。TMP で並べて測った幅は 8.99 em（2026-09-29）
            Assert.AreEqual(8.987f, Ruby.Em("H. Goodchild & Son"), 1e-3f);
            Assert.AreEqual(0f, Ruby.Em(ListFormat.Mark.ToString() + ListFormat.SignMark), "印は幅を持たない");
        }

        [Test]
        public void TheRubyOverALatinBaseSitsInItsMiddle()
        {
            // Pawnbrokers は字の送りで 6.18 em（TMP で並べると字の組の詰めが入って 6.10 em）。
            // 半角 11 字 × 0.5 = 5.5 em と数えていた頃は、ルビが 0.3 em 左へ寄った。質屋 = 2 字 × Scale
            var baseEm = Ruby.Em("Pawnbrokers");
            Assert.AreEqual(6.182f, baseEm, 1e-3f);
            var ruby = 2f * Ruby.Scale;
            var lead = (baseEm - ruby) * 0.5f;
            var made = Ruby.Over("Pawnbrokers", "質屋");
            StringAssert.StartsWith(Space(lead), made);
            StringAssert.Contains(Space(-(lead + ruby)), made);
            // ルビの真ん中（lead + ruby / 2）が親字の真ん中（baseEm / 2）
            Assert.AreEqual(baseEm * 0.5f, lead + ruby * 0.5f, 1e-4f);
        }

        [Test]
        public void ALatinRubyStepsBackByItsOwnAdvance()
        {
            // 英字のルビ（Minimum Infrastructure）も、戻すのは実際に進んだ幅。半角いくつで数えると、後ろの親字がずれる
            float start, back, pad;
            Layout(7f, Ruby.Em("Minimum Infrastructure") * Ruby.Scale, out start, out back, out pad);
            StringAssert.Contains(Space(-back), Ruby.Over("最小の生活基盤", "Minimum Infrastructure"));
        }

        [Test]
        public void CentredLinesHangTheRubyForwardEvenAtTheHead()
        {
            // 看板の枠は行ごとに真ん中へ寄せる。行の頭でもルビを親字の真ん中に揃え、前へ出るぶんは Span が返す
            var line = "｜Est. 1871<創業1871年>";
            var made = Ruby.Expand(line, 0.9f, 1f, true);
            StringAssert.StartsWith("<space=-", made, "行の頭でも前へ掛ける");
            StringAssert.StartsWith("<voffset=", Ruby.Expand(line, 0.9f, 1f), "字幕では行の頭のルビを前へ掛けない（前のまま）");
            float lead, tail;
            var advance = Ruby.Span(line, 0.9f, true, out lead, out tail);
            var baseEm = Ruby.Em("Est. 1871");
            var rubyEm = Ruby.Em("創業1871年") * 0.9f;
            Assert.AreEqual(baseEm, advance, 1e-4f, "はみ出しが Hang の内なら、親字の前後は空けない");
            Assert.AreEqual((rubyEm - baseEm) * 0.5f, lead, 1e-4f);
            Assert.AreEqual(lead, tail, 1e-4f, "両脇へ同じだけ");
        }

        [Test]
        public void AtTheHeadOfALineTheRubyDoesNotHangForward()
        {
            // 行の頭では前に字が無い。ルビの頭を親字の頭に揃え、後ろの字の上へだけ掛ける
            var head = Ruby.Expand("｜倫敦《ロンドン》の朝");
            StringAssert.StartsWith("<voffset=", head, "行の頭のルビは前へ戻さない");
            var second = Ruby.Expand("寒い\n｜倫敦《ロンドン》の朝");
            StringAssert.Contains("\n<voffset=", second, "改行の後も行の頭");
            var mid = Ruby.Expand("この｜倫敦《ロンドン》の朝");
            StringAssert.Contains("この<space=-", mid, "行の中ほどでは前の字の上へ掛けて真ん中に揃える");
        }

        [Test]
        public void TheRubyIsBigEnoughToReadOnTheCoarseScreen()
        {
            // 台詞は粗い画面の中で 13 画素。ルビの仮名が 9 画素を下回ると潰れて読めない（オーナー、2026-09-27）
            Assert.That(13f * Ruby.Scale, Is.GreaterThanOrEqualTo(9f));
            Assert.That(Ruby.Scale, Is.LessThan(1f), "親字より一回り小さい");
        }

        [Test]
        public void TheRubySitsClearOfTheBaseGlyphs()
        {
            // ルビの字のいちばん低い所（小さい仮名）が、漢字の上の端より隙間ぶん上にある
            var rubyBottom = Ruby.Lift - Ruby.RubyDip * Ruby.Scale;
            Assert.That(rubyBottom, Is.GreaterThanOrEqualTo(Ruby.BaseTop + 0.05f), "ルビが親字にかぶる");
        }

        [Test]
        public void TheNextLinesRubyClearsTheLineAbove()
        {
            // 行を開けたあとの行送りから、下の行のルビの上の端を引いても、上の行の親字の下の端より下にならない
            var advance = Ruby.Advance + Ruby.ExtraLineSpacing * 0.01f;
            var rubyTop = Ruby.Lift + Ruby.RubyTop * Ruby.Scale;
            Assert.That(advance - rubyTop, Is.GreaterThanOrEqualTo(Ruby.BaseBottom + 0.05f), "下の行のルビが上の行にかぶる");
        }

        [Test]
        public void ItRaisesAndShrinks()
        {
            var made = Ruby.Over("倫敦", "ロンドン");
            // 持ち上げる高さは TMP で測って決めるものなので、定数を見る
            StringAssert.Contains("<voffset=" + Num(Ruby.Lift) + "em>", made);
            StringAssert.Contains("<size=" + Num(Ruby.Scale * 100f) + "%>", made);
            StringAssert.Contains("</size></voffset>", made);
        }

        [Test]
        public void ExpandCanTryAnotherSize()
        {
            var made = Ruby.Expand("｜倫敦《ロンドン》", 0.6f, 0.95f);
            StringAssert.Contains("<size=60%>", made);
            StringAssert.Contains("<voffset=0.95em>", made);
        }
    }
}
