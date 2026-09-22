using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 記憶の中の見え方。目の疲れと、画面の角の白い膜。
    ///
    /// どちらも一人目では出ないことを押さえておく。
    /// 潜った瞬間から世界が溶けているのは差し戻された作りで、
    /// 気づかないうちに戻ると同じ指摘をもう一度受けることになる
    /// </summary>
    public sealed class MemoryLookTests
    {
        // ---- 目の疲れ ----------------------------------------------------------

        [Test]
        public void TheFirstBodySeesAsClearlyAsHerOwn()
        {
            Assert.AreEqual(0f, HostBody.StrainOf(DiveChain.CutStart), 1e-4f);
        }

        [Test]
        public void TheEyesGiveOutJustAsTheCutComesOfAge()
        {
            Assert.AreEqual(1f, HostBody.StrainOf(1f), 1e-4f);
        }

        [Test]
        public void TheFirstHalfOfTheNightCostsHerNothing()
        {
            Assert.AreEqual(0f, HostBody.StrainOf(HostBody.Quiet), 1e-4f);
            var chain = new DiveChain(16, new[] { 0, 1, 2, 4, 5, 7 }, 8, new System.Random(1));
            for (var i = 1; i <= 4; i++)
            {
                chain.Hop(i);
                Assert.AreEqual(0f, HostBody.StrainOf(chain.CutSize), 1e-4f,
                    "四人目までは素のままであるべき: " + i + " 人目");
            }
        }

        [Test]
        public void AfterThatEachNewHeadCostsHerMoreSight()
        {
            var chain = new DiveChain(16, new[] { 0, 1, 2, 4, 5, 7 }, 8, new System.Random(1));
            for (var i = 1; i <= 4; i++) chain.Hop(i);
            var before = HostBody.StrainOf(chain.CutSize);
            for (var i = 5; i <= 8; i++)
            {
                chain.Hop(i);
                var now = HostBody.StrainOf(chain.CutSize);
                Assert.Greater(now, before, "渡るたびに疲れが増えていない: " + i + " 人目");
                before = now;
            }
            Assert.AreEqual(1f, before, 1e-4f);
        }

        [Test]
        public void GoingBackToTheSameHeadCostsHerNothing()
        {
            var chain = new DiveChain(16, new[] { 0, 1, 2, 4, 5, 7 }, 8, new System.Random(1));
            for (var i = 1; i <= 6; i++) chain.Hop(i);
            var before = HostBody.StrainOf(chain.CutSize);
            var hops = chain.Hops;
            for (var k = 0; k < 5; k++) { chain.Hop(3); chain.Hop(5); }
            Assert.AreEqual(hops, chain.Hops, "同じ人へ戻っただけで人数が増えている");
            Assert.AreEqual(before, HostBody.StrainOf(chain.CutSize), 1e-4f,
                "同じ人へ戻っただけで目が疲れている");
        }

        [Test]
        public void NoAmountOfWalkingPushesTheStrainPastItself()
        {
            Assert.AreEqual(0f, HostBody.StrainOf(0f), 1e-4f);
            Assert.AreEqual(1f, HostBody.StrainOf(4f), 1e-4f);
        }

        // ---- 角の白い膜 --------------------------------------------------------

        static ScreenHaze Fresh()
        {
            var go = new GameObject("Haze", typeof(ScreenHaze));
            go.hideFlags = HideFlags.HideAndDontSave;
            var haze = go.GetComponent<ScreenHaze>();
            haze.Amount = 1f;
            return haze;
        }

        static void Drop(ScreenHaze haze)
        {
            if (haze != null) Object.DestroyImmediate(haze.gameObject);
        }

        [Test]
        public void WhatSheIsLookingAtStaysUntouched()
        {
            var haze = Fresh();
            Assert.AreEqual(0f, haze.Veil(0f), 1e-4f);
            Drop(haze);
        }

        [Test]
        public void TheCornersGoWhiterThanTheEdges()
        {
            var haze = Fresh();
            var edge = haze.Veil(1f);
            var corner = haze.Veil(1.414f);
            Assert.Greater(corner, edge);
            Assert.Greater(edge, 0f);
            Drop(haze);
        }

        [Test]
        public void TheWhiteNeverSwallowsTheCorner()
        {
            var haze = Fresh();
            Assert.LessOrEqual(haze.Veil(1.414f), 0.5f, "角が白すぎて字も絵も読めない");
            Drop(haze);
        }

        [Test]
        public void TurningItOffLeavesNothingBehind()
        {
            var haze = Fresh();
            haze.Amount = 0f;
            Assert.AreEqual(0f, haze.Veil(1.414f), 1e-4f);
            Drop(haze);
        }

        [Test]
        public void TheVeilOnlyEverThickensOutwards()
        {
            var haze = Fresh();
            var before = -1f;
            for (var reach = 0f; reach <= 1.42f; reach += 0.02f)
            {
                var now = haze.Veil(reach);
                Assert.GreaterOrEqual(now, before, "外へ向かって薄くなった所がある: " + reach);
                before = now;
            }
            Drop(haze);
        }
    }
}
