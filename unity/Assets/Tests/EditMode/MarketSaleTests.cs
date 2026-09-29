using NUnit.Framework;
using UnityEditor;

namespace HalfAware.Tests
{
    /// <summary>
    /// 売り買いの形（<see cref="MarketSale"/>）。台詞は台詞の原稿から写した文面のアセット（AlleyScript）から引く
    /// </summary>
    public class MarketSaleTests
    {
        static RoomScript Script()
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>("Assets/Data/AlleyScript.asset");
            Assert.That(script, Is.Not.Null, "場面 2 の文面のアセットが無い");
            return script;
        }

        [Test]
        public void ThreeBuyersComeInTurn()
        {
            Assert.That(MarketSale.Count, Is.EqualTo(3));
            for (var i = 0; i < MarketSale.Count; i++)
                Assert.That(MarketSale.Lines(Script(), i), Is.Not.Empty, "買い手 " + i + " の台詞が空");
        }

        [Test]
        public void EachBuyerSpeaksAndSoDoI()
        {
            // 話者の付いていない行があると、誰の台詞か読めなくなる
            for (var i = 0; i < MarketSale.Count; i++)
                foreach (var line in MarketSale.Lines(Script(), i))
                    Assert.That(line.StartsWith("私「") || line.StartsWith("買い手"),
                        "話者が付いていない: " + line);
        }

        [Test]
        public void TheSmokesComeWithTheSecondBuyersLastWord()
        {
            // 煙草を置くのは、その買い手の最後の台詞の行（原稿の注記「上の行で、買い手Bが煙草をテーブルに置く」）
            Assert.IsNull(MarketSale.PutsSmokes(Script(), 0));
            Assert.AreEqual("買い手B「はいはい、じゃあこれな」", MarketSale.PutsSmokes(Script(), 1));
            Assert.IsNull(MarketSale.PutsSmokes(Script(), 2));
            Assert.AreEqual("a「3」", MarketSale.LastOwnLine(new[] { "a「1」", "b「2」", "a「3」", "b「4」" }));
            Assert.IsNull(MarketSale.LastOwnLine(new[] { "地の文" }), "頭の行に話者が無ければ決まらない");
        }

        [Test]
        public void OnlyTheThirdBuyerWearsHeels()
        {
            Assert.IsFalse(MarketSale.Woman(0));
            Assert.IsFalse(MarketSale.Woman(1));
            Assert.IsTrue(MarketSale.Woman(2), "買い手 C（女）はヒールの足音");
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
            var closing = MarketSale.Closing(Script());
            Assert.That(closing, Is.Not.Empty);
            Assert.That(closing[0], Does.Contain("6枚"));
            Assert.That(closing[closing.Count - 1], Is.EqualTo("帰ろう"));
        }

        [Test]
        public void TheCountSheQuotesMatchesTheChips()
        {
            // 「男が3枚、女が3枚」は自室で見た一覧（男 3・女 3）と揃っている
            Assert.That(MarketSale.Lines(Script(), 0)[1], Does.Contain("男が3枚"));
            Assert.That(MarketSale.Lines(Script(), 0)[1], Does.Contain("女が3枚"));
        }
    }
}
