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
        public void TheDazeWaitsUntilTheCutOpens()
        {
            var chain = Fresh();
            Assert.AreEqual(0f, DiveDirector.Dizziness(chain, 0.8f), 1e-4f, "最初の一人から眩暈が出ている");
            for (var i = 0; i < 8; i++)
            {
                chain.Hop(i + 1);
                Assert.AreEqual(0f, DiveDirector.Dizziness(chain, 0.8f), 1e-4f, chain.Hops + " 人目で眩暈が出ている");
            }
            Assert.IsTrue(chain.CanCut);
            chain.Hop(10);
            Assert.Greater(DiveDirector.Dizziness(chain, 0.8f), 0f, "押せるようになった後も眩暈が出ない");
        }

        [Test]
        public void TheDazeTopsOutWhereTheRosterEnds()
        {
            var chain = Fresh();
            for (var i = 1; i < 16; i++) chain.Hop(i);
            Assert.AreEqual(15, chain.Hops);
            Assert.AreEqual(0.875f * 0.8f, DiveDirector.Dizziness(chain, 0.8f), 1e-4f, "十六人の名簿では 0.7 で止まる");
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
