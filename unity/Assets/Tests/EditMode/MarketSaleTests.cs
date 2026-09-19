using NUnit.Framework;

namespace HalfAware.Tests
{
    public class MarketSaleTests
    {
        [Test]
        public void ThreeBuyersComeInTurn()
        {
            Assert.That(MarketSale.Count, Is.EqualTo(3));
            for (var i = 0; i < MarketSale.Count; i++)
                Assert.That(MarketSale.Lines(i), Is.Not.Empty, "買い手 " + i + " の台詞が空");
        }

        [Test]
        public void EachBuyerSpeaksAndSoDoI()
        {
            // 話者の付いていない行があると、誰の台詞か読めなくなる
            for (var i = 0; i < MarketSale.Count; i++)
                foreach (var line in MarketSale.Lines(i))
                    Assert.That(line.StartsWith("私「") || line.StartsWith("買い手"),
                        "話者が付いていない: " + line);
        }

        [Test]
        public void ChipsOnlyEverGoDown()
        {
            var before = MarketSale.Chips;
            for (var i = 0; i < MarketSale.Count; i++)
            {
                var left = MarketSale.Left(i);
                Assert.That(left, Is.LessThan(before), "買い手 " + i + " で減っていない");
                Assert.That(left, Is.GreaterThanOrEqualTo(0));
                before = left;
            }
        }

        [Test]
        public void EveryBuyerTakesAtLeastOne()
        {
            for (var i = 0; i < MarketSale.Count; i++)
                Assert.That(MarketSale.Took(i), Is.GreaterThan(0));
        }

        [Test]
        public void SellsOutAfterTheLastBuyer()
        {
            Assert.That(MarketSale.Left(MarketSale.Count), Is.EqualTo(0));
        }

        [Test]
        public void StartsWithWhatSheTookFromTheChips()
        {
            Assert.That(MarketSale.Chips, Is.EqualTo(6));
            Assert.That(MarketSale.Left(-1), Is.EqualTo(6));
        }

        [Test]
        public void ClosesWithTheTallyAndTheWayHome()
        {
            Assert.That(MarketSale.Closing, Is.Not.Empty);
            Assert.That(MarketSale.Closing[0], Does.Contain("6枚"));
            Assert.That(MarketSale.Closing[MarketSale.Closing.Count - 1], Is.EqualTo("帰ろう"));
        }

        [Test]
        public void TheCountSheQuotesMatchesTheChips()
        {
            // 「男が3枚、女が3枚」は自室で見た一覧（男 3・女 3）と揃っている
            Assert.That(MarketSale.Lines(0)[1], Does.Contain("男が3枚"));
            Assert.That(MarketSale.Lines(0)[1], Does.Contain("女が3枚"));
        }
    }
}
