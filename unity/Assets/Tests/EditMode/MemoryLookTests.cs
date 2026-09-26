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

        static DiveChain Chain() { return new DiveChain(16, new[] { 0, 1, 2, 4, 5, 7 }, 8, new System.Random(1)); }

        [Test]
        public void NothingBlursWhileTheWayOutIsStillShut()
        {
            var chain = Chain();
            for (var i = 1; i <= 8; i++)
            {
                Assert.AreEqual(0f, chain.Past, 1e-4f, "まだ切断が押せないのに曇っている: " + chain.Hops + " 人目");
                chain.Hop(i);
            }
            Assert.IsTrue(chain.CanCut);
            Assert.AreEqual(0f, chain.Past, 1e-4f, "押せるようになった瞬間はまだ素のまま");
        }

        [Test]
        public void AfterTheWayOutOpensEachNewHeadCostsHerSight()
        {
            var chain = Chain();
            for (var i = 1; i <= 8; i++) chain.Hop(i);
            var before = chain.Past;
            for (var i = 9; i <= 15; i++)
            {
                chain.Hop(i);
                Assert.Greater(chain.Past, before, "押せた後も曇りが増えていない: " + chain.Hops + " 人目");
                before = chain.Past;
            }
        }

        /// <summary>
        /// 疲れが出きるのは、`切断` が押せるようになってからもう一度同じ人数を渡ったとき。
        ///
        /// 名簿が十六人なので、実際の場面 4 では渡れて十五人、疲れは 0.88 止まりになる。
        /// 目が完全に溶ける前にプレイヤーが帰る作りで、それでよい
        /// </summary>
        [Test]
        public void TheEyesOnlyGiveOutAfterTwiceTheWayOut()
        {
            var wide = new DiveChain(32, new[] { 0, 1, 2, 4, 5, 7 }, 8, new System.Random(1));
            for (var i = 1; i <= 16; i++) wide.Hop(i);
            Assert.AreEqual(16, wide.Hops);
            Assert.AreEqual(1f, wide.Past, 1e-4f);

            var roster = Chain();
            for (var i = 1; i <= 15; i++) roster.Hop(i);
            Assert.AreEqual(0.875f, roster.Past, 1e-3f, "十六人の名簿では 0.88 で止まる");
        }

        [Test]
        public void GoingBackToTheSameHeadCostsHerNothing()
        {
            var chain = Chain();
            for (var i = 1; i <= 10; i++) chain.Hop(i);
            var hops = chain.Hops;
            var before = chain.Past;
            for (var k = 0; k < 5; k++) { chain.Hop(3); chain.Hop(5); }
            Assert.AreEqual(hops, chain.Hops, "同じ人へ戻っただけで人数が増えている");
            Assert.AreEqual(before, chain.Past, 1e-4f, "同じ人へ戻っただけで目が疲れている");
        }

        [Test]
        public void TheStrainNeverRunsPastItself()
        {
            var wide = new DiveChain(32, new[] { 0, 1, 2, 4, 5, 7 }, 8, new System.Random(1));
            for (var i = 1; i < 32; i++) wide.Hop(i);
            Assert.AreEqual(1f, wide.Past, 1e-4f);
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
        public void TheVeilStaysFaint()
        {
            // 角 0.34・辺の真ん中 0.20 では四方が白く濁ると差し戻され、半分に下げた
            var haze = Fresh();
            Assert.AreEqual(0.17f, haze.Veil(1.414f), 0.01f, "角の白が濃い");
            Assert.AreEqual(0.10f, haze.Veil(1f), 0.01f, "辺の真ん中の白が濃い");
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
