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

        /// <summary>路地裏の看板くらいの長さ。2 行では到底収まらない</summary>
        const string Long =
            "一世紀前まではモダニズムからポストモダニズムへの過渡期にあった建築物が大窓を並べ、" +
            "落ち着いた都会の一角をなしていたというこの通りも、今ではそのほとんどに、" +
            "瞼の裏に焼き付くような極彩色のネオンが熱帯植物のように絡みついている";

        [Test]
        public void ALongLineIsBrokenAsOftenAsItNeeds()
        {
            var parts = SubtitleBox.Wrap(Long, 40).Split('\n');
            Assert.That(parts.Length, Is.GreaterThan(2), "2 行で止めない");
            Assert.AreEqual(Long, string.Join("", parts), "字は 1 つも落とさない");
        }

        [Test]
        public void NoLineRunsPastTheWidth()
        {
            foreach (var line in SubtitleBox.Wrap(Long, 40).Split('\n'))
                Assert.LessOrEqual(ListFormat.Units(line), 40, "幅からはみ出した: " + line);
        }

        [Test]
        public void TheLinesComeOutEvenlyLong()
        {
            var parts = SubtitleBox.Wrap(Long, 40).Split('\n');
            var least = int.MaxValue;
            var most = 0;
            foreach (var line in parts)
            {
                var n = ListFormat.Units(line);
                if (n < least) least = n;
                if (n > most) most = n;
            }
            Assert.LessOrEqual(most - least, 16, "最後の行だけ極端に短くしない");
        }

        [Test]
        public void KatakanaWordsAreNotSplitDownTheMiddle()
        {
            var text = "あれがナーヴ・ターミナルのジャックに入り込むと、ファイアウォールを立てていない人間は抜かれる";
            foreach (var line in SubtitleBox.Wrap(text, 40).Split('\n'))
            {
                StringAssert.DoesNotEndWith("ファイアウォ", line);
                StringAssert.DoesNotEndWith("ナー", line);
                StringAssert.DoesNotEndWith("ジャ", line);
            }
        }

        [Test]
        public void ItLooksElsewhereBeforeSplittingACompound()
        {
            // 「産業革命」のような熟語の真ん中より、助詞の後ろで切ってほしい
            var text = "倫敦を発端とした近年のインプラント革命は世界にとって再びの産業革命でもあった";
            foreach (var line in SubtitleBox.Wrap(text, 40).Split('\n'))
                StringAssert.DoesNotEndWith("産業革", line);
        }

        [Test]
        public void ALineNeverEndsWithAnOpeningBracket()
        {
            var text = "買い手は短いものを選べるが、主人公は仕事として「最後まで観る」ので説明はしない";
            foreach (var line in SubtitleBox.Wrap(text, 24).Split('\n'))
            {
                Assert.AreNotEqual('「', line[line.Length - 1]);
                Assert.AreNotEqual('『', line[line.Length - 1]);
            }
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
