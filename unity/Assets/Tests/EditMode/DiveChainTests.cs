using System;
using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class DiveChainTests
    {
        static DiveChain Fresh() { return new DiveChain(16, new[] { 0, 1, 2, 4, 5, 7 }, 8, new Random(1)); }

        [Test]
        public void SheStartsWithTheTopOfTheList()
        {
            var chain = Fresh();
            Assert.AreEqual(0, chain.Current);
            Assert.AreEqual(0, chain.Hops);
        }

        [Test]
        public void TheSameHeadTwiceIsStillOneHead()
        {
            var chain = Fresh();
            chain.Hop(4);
            chain.Hop(9);
            Assert.AreEqual(2, chain.Hops);
            chain.Hop(4);
            chain.Hop(9);
            chain.Hop(4);
            Assert.AreEqual(2, chain.Hops, "一度潜った人へ戻っただけで人数が増えている");
            Assert.AreEqual(4, chain.Current);
        }

        [Test]
        public void TheCutStopsGrowingWhenSheOnlyRetracesHerSteps()
        {
            var chain = Fresh();
            chain.Hop(4);
            var size = chain.CutSize;
            for (var i = 0; i < 6; i++) chain.Hop(4);
            Assert.AreEqual(size, chain.CutSize, 1e-4f);
            Assert.IsFalse(chain.CanCut);
        }

        [Test]
        public void HoppingGoesWhereSheChose()
        {
            var chain = Fresh();
            chain.Hop(9);
            Assert.AreEqual(9, chain.Current);
            Assert.AreEqual(1, chain.Hops);
        }

        [Test]
        public void WhenSheDoesNotChooseTheTerminalWalksTheList()
        {
            var chain = Fresh();
            chain.Next();
            Assert.AreEqual(1, chain.Current);
            chain.Next();
            Assert.AreEqual(2, chain.Current);
        }

        [Test]
        public void TheTerminalSkipsWhereSheHasAlreadyBeen()
        {
            var chain = Fresh();
            chain.Hop(1);
            chain.Next();
            Assert.AreEqual(2, chain.Current);
        }

        [Test]
        public void OnceTheListIsSpentTheTerminalDrawsFromTheRest()
        {
            var chain = Fresh();
            for (var i = 0; i < 5; i++) chain.Next();
            chain.Next();
            Assert.IsFalse(Array.IndexOf(new[] { 0, 1, 2, 4, 5, 7 }, chain.Current) >= 0);
        }

        [Test]
        public void SheCanGoBackToSomeoneByChoice()
        {
            var chain = Fresh();
            chain.Hop(1);
            chain.Hop(0);
            Assert.AreEqual(0, chain.Current);
        }

        [Test]
        public void CutIsSmallAndDeadUntilTheEighthHop()
        {
            var chain = Fresh();
            Assert.IsFalse(chain.CanCut);
            Assert.AreEqual(0.4f, chain.CutSize, 1e-4f);
            for (var i = 0; i < 7; i++) chain.Hop(i + 1);
            Assert.IsFalse(chain.CanCut);
            Assert.Less(chain.CutSize, 1f);
            chain.Hop(9);
            Assert.IsTrue(chain.CanCut);
            Assert.AreEqual(1f, chain.CutSize, 1e-4f);
        }

        [Test]
        public void CutOnlyGrows()
        {
            var chain = Fresh();
            var last = chain.CutSize;
            for (var i = 0; i < 12; i++)
            {
                chain.Next();
                Assert.GreaterOrEqual(chain.CutSize, last);
                last = chain.CutSize;
            }
        }
    }
}
