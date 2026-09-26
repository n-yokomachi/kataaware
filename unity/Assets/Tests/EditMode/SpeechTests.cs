using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SpeechTests
    {
        static void Splits(string line, string who, string said)
        {
            string w, s;
            Assert.IsTrue(Speech.Split(line, out w, out s), line);
            Assert.AreEqual(who, w);
            Assert.AreEqual(said, s);
        }

        static void Keeps(string line)
        {
            string w, s;
            Assert.IsFalse(Speech.Split(line, out w, out s), line);
            Assert.AreEqual("", w);
            Assert.AreEqual(line ?? "", s);
        }

        [Test]
        public void ANameAndALineComeApart()
        {
            Splits("ハンナ「メイ！　忘れもの！　上がっておいで！」", "ハンナ", "メイ！　忘れもの！　上がっておいで！");
            Splits("買い手A「やあ、今日の品ぞろえは？」", "買い手A", "やあ、今日の品ぞろえは？");
            Splits("私「どうも」", "私", "どうも");
        }

        [Test]
        public void QuotesInsideTheLineStay()
        {
            Splits("ハンナ「『水筒』って言ったでしょ」", "ハンナ", "『水筒』って言ったでしょ");
            Splits("メイ「「いってきます」って言ったもん」", "メイ", "「いってきます」って言ったもん");
        }

        [Test]
        public void AnUnnamedLineIsLeftAsItIs()
        {
            Keeps("世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている");
            Keeps("「グレビル・ストリート」と書かれている");
            Keeps("「……」");
        }

        [Test]
        public void ALineThatClosesEarlyIsNotSpeech()
        {
            Keeps("看板「ホルボーン」と書かれている");
            Keeps("ハンナ「一」と「二」");
        }

        [Test]
        public void ALongHeadIsNotAName()
        {
            Keeps("人間が本来具有する目や耳といった感覚器官や、「ナーヴ・ターミナル」");
            Keeps("これはとても長い頭の地の文なので名前ではない「台詞」");
        }

        [Test]
        public void RubyAndPunctuationAreNotNames()
        {
            Keeps("｜倫敦《ロンドン》「雨」");
            Keeps("そう、「雨」");
        }

        [Test]
        public void EmptyIsSafe()
        {
            Keeps("");
            Keeps(null);
            Assert.AreEqual("", Speech.Who(null));
            Assert.AreEqual("ハンナ", Speech.Who("ハンナ「水筒！」"));
        }

        [Test]
        public void TrailingSpaceIsForgiven()
        {
            Splits("メイ「えーなにー？」\n", "メイ", "えーなにー？");
        }
    }
}
