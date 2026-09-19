using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SubtitleWrapTests
    {
        [Test]
        public void ShortLinesAreLeftAlone()
        {
            var text = "煙草が切れた";
            Assert.AreEqual(text, SubtitleBox.Wrap(text, 40));
        }

        [Test]
        public void NothingIsLeftAlone()
        {
            Assert.AreEqual(null, SubtitleBox.Wrap(null));
            Assert.AreEqual("", SubtitleBox.Wrap(""));
        }

        [Test]
        public void SomethingAlreadyBrokenIsLeftAlone()
        {
            var text = "一行目\n二行目";
            Assert.AreEqual(text, SubtitleBox.Wrap(text));
        }

        [Test]
        public void ALineThatFitsStaysOnOne()
        {
            var text = "世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている";
            Assert.AreEqual(1, SubtitleBox.LineCount(SubtitleBox.Wrap(text, 80)), "幅に収まるなら割らない");
        }

        [Test]
        public void ALongLineBecomesTwo()
        {
            var text = "世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている";
            var made = SubtitleBox.Wrap(text, 40);
            Assert.AreEqual(2, SubtitleBox.LineCount(made));
            Assert.AreEqual(text, made.Replace("\n", ""), "字は 1 つも落とさない");
        }

        [Test]
        public void TheTwoHalvesComeOutEven()
        {
            var text = "滅多にないことだが潜り込んだ他人の記憶が薬などでトリップしていると";
            var parts = SubtitleBox.Wrap(text, 40).Split('\n');
            Assert.AreEqual(2, parts.Length);
            var a = ListFormat.Units(parts[0]);
            var b = ListFormat.Units(parts[1]);
            Assert.LessOrEqual(System.Math.Abs(a - b), 14, "半々に近いところで割る");
        }

        [Test]
        public void ItPrefersToBreakAfterAComma()
        {
            var text = "抜け出した後の自己同定のために、鏡を見ることは大切だからそうしている";
            var parts = SubtitleBox.Wrap(text, 40).Split('\n');
            Assert.AreEqual(2, parts.Length);
            StringAssert.EndsWith("、", parts[0]);
        }

        [Test]
        public void ALineNeverStartsWithClosingPunctuation()
        {
            var text = "大小の差こそあれ、他人の記憶を観た後はいつもこうだ。仕方がないことだ。";
            var parts = SubtitleBox.Wrap(text, 40).Split('\n');
            if (parts.Length < 2) return;
            Assert.AreNotEqual('。', parts[1][0]);
            Assert.AreNotEqual('、', parts[1][0]);
        }
    }
}
