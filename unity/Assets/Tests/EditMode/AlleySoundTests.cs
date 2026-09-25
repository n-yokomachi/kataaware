using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class AlleySoundTests
    {
        // 路地裏と同じ並び。通りは x が -4.9 より東、小路は x -16〜-4.9・z 33.4〜36.6、ヤードは x -32〜-16・z 26〜44
        static Bounds Lane()
        {
            return new Bounds(new Vector3(-10.45f, 2f, 35f), new Vector3(11.1f, 6f, 3.2f));
        }

        static Bounds Yard()
        {
            return new Bounds(new Vector3(-24f, 2f, 35f), new Vector3(16f, 6f, 18f));
        }

        [Test]
        public void TheStreetIsEverythingOutside()
        {
            Assert.AreEqual(AlleyPlace.Street, AlleySound.Where(new Vector3(0f, 0.1f, -2.9f), Lane(), Yard()));
            Assert.AreEqual(AlleyPlace.Street, AlleySound.Where(new Vector3(-2f, 0.1f, 35f), Lane(), Yard()));
        }

        [Test]
        public void TheLaneAndTheYardAreFoundByTheirBounds()
        {
            Assert.AreEqual(AlleyPlace.Lane, AlleySound.Where(new Vector3(-10f, 0.1f, 35f), Lane(), Yard()));
            Assert.AreEqual(AlleyPlace.Yard, AlleySound.Where(new Vector3(-28f, 0.1f, 41f), Lane(), Yard()));
        }

        [Test]
        public void TheYardMouthCountsAsTheYard()
        {
            Assert.AreEqual(AlleyPlace.Yard, AlleySound.Where(new Vector3(-16f, 0.1f, 35f), Lane(), Yard()));
        }

        [Test]
        public void EachPlaceGetsItsOwnLevel()
        {
            Assert.AreEqual(0.25f, AlleySound.Level(AlleyPlace.Street, 0.25f, 0.1f, 0.7f), 1e-6f);
            Assert.AreEqual(0.1f, AlleySound.Level(AlleyPlace.Lane, 0.25f, 0.1f, 0.7f), 1e-6f);
            Assert.AreEqual(0.7f, AlleySound.Level(AlleyPlace.Yard, 0.25f, 0.1f, 0.7f), 1e-6f);
        }

        [Test]
        public void DepthRunsFromTheStreetEndToTheYardMouth()
        {
            Assert.AreEqual(0f, AlleySound.Depth(new Vector3(-4.95f, 0.1f, 35f), Lane(), Yard()), 0.01f);
            Assert.AreEqual(0.5f, AlleySound.Depth(new Vector3(-10.45f, 0.1f, 35f), Lane(), Yard()), 0.01f);
            Assert.AreEqual(1f, AlleySound.Depth(new Vector3(-15.99f, 0.1f, 35f), Lane(), Yard()), 0.01f);
        }

        [Test]
        public void DepthIsFullInTheYardAndNoneOnTheStreet()
        {
            Assert.AreEqual(1f, AlleySound.Depth(new Vector3(-28f, 0.1f, 41f), Lane(), Yard()), 1e-6f);
            Assert.AreEqual(0f, AlleySound.Depth(new Vector3(0f, 0.1f, 35f), Lane(), Yard()), 1e-6f);
        }

        [Test]
        public void EasingMovesTowardTheTargetWithoutJumping()
        {
            var v = AlleySound.Ease(0.1f, 0.7f, 0.1f, 1f);
            Assert.That(v, Is.GreaterThan(0.1f).And.LessThan(0.7f), "境で急に変わらない");
        }

        [Test]
        public void EasingNeverOvershoots()
        {
            var v = 0.1f;
            for (var i = 0; i < 600; i++) v = AlleySound.Ease(v, 0.7f, 0.05f, 1f);
            Assert.That(v, Is.LessThanOrEqualTo(0.7f));
            Assert.AreEqual(0.7f, v, 1e-3f);
        }

        [Test]
        public void EasingTakesAboutTheGivenSeconds()
        {
            // seconds かけて差の 6 割ほど（1 - 1/e）が詰まる
            var v = AlleySound.Ease(0f, 1f, 1f, 1f);
            Assert.AreEqual(1f - Mathf.Exp(-1f), v, 1e-5f);
        }

        [Test]
        public void NoTimeMeansNoChange()
        {
            Assert.AreEqual(0.25f, AlleySound.Ease(0.25f, 0.7f, 0f, 1f), 1e-6f);
        }

        [Test]
        public void NoBlendJumpsStraightToTheTarget()
        {
            Assert.AreEqual(0.7f, AlleySound.Ease(0.25f, 0.7f, 0.016f, 0f), 1e-6f);
        }
    }
}
