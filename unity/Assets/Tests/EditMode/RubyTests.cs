using NUnit.Framework;

namespace HalfAware.Tests
{
    public class RubyTests
    {
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

        [Test]
        public void ItStepsBackByWhatTheRubyAdvanced()
        {
            // 倫敦 は全角 2 字 = 2 em。ロンドン は全角 4 字 = 4 em を半分にして 2 em。
            // ちょうど重なるので寄せない。戻すのは進んだ 2 em
            StringAssert.Contains("<space=-2em>", Ruby.Over("倫敦", "ロンドン"));
            StringAssert.DoesNotContain("<space=0em>", Ruby.Over("倫敦", "ロンドン"));
        }

        [Test]
        public void ShortRubySitsInTheMiddle()
        {
            // 生活基盤 = 4 em、きばん = 3 字 = 3 em を半分にして 1.5 em。
            // 左右に 1.25 em ずつ余るので、寄せてから 2.75 em 戻す
            var made = Ruby.Over("生活基盤", "きばん");
            // 小さくした側の em で渡すので 1.25 / 0.5 = 2.5
            StringAssert.Contains("<space=2.5em>", made);
            StringAssert.Contains("<space=-2.75em>", made);
        }

        [Test]
        public void RubyWiderThanTheBasePushesWhatFollows()
        {
            // 羅府 = 2 em、ロサンゼルス = 6 字 × 0.5 = 3 em → 1 em はみ出す
            var made = Ruby.Over("羅府", "ロサンゼルス");
            StringAssert.Contains("<space=-3em>", made);
            StringAssert.EndsWith("<space=1em>", made);
        }

        [Test]
        public void ItNeverLeadsWhenTheRubyIsTheWiderOne()
        {
            var made = Ruby.Over("羅府", "ロサンゼルス");
            Assert.That(made.IndexOf("<space=-"), Is.LessThan(made.IndexOf("羅府")), "戻すのは親字の前");
            StringAssert.DoesNotContain("<space=0em>", made);
        }

        [Test]
        public void LatinRubyIsMeasuredAsHalfWidth()
        {
            // 最小の生活基盤 = 7 em、Minimum Infrastructure = 22 半角 × 0.5 × 0.5 = 5.5 em
            var made = Ruby.Over("最小の生活基盤", "Minimum Infrastructure");
            StringAssert.Contains("<space=-6.25em>", made);   // 0.75 寄せ + 5.5
            StringAssert.DoesNotContain("<space=-7em>", made, "親字の幅で戻すと、ルビの長さぶんずれる");
        }

        [Test]
        public void ItRaisesAndShrinks()
        {
            var made = Ruby.Over("倫敦", "ロンドン");
            StringAssert.Contains("<voffset=0.62em>", made);
            StringAssert.Contains("<size=50%>", made);
            StringAssert.Contains("</size></voffset>", made);
        }
    }
}
