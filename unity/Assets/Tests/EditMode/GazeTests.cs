using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>調べた物へ目を向ける角度（Gaze）。PlayerController の Yaw・Pitch と同じ取り方（+z が 0 で東回り、下が正）</summary>
    public class GazeTests
    {
        [Test]
        public void StraightAheadIsZero()
        {
            var a = Gaze.Angles(Vector3.zero, new Vector3(0f, 0f, 5f));
            Assert.AreEqual(0f, a.x, 1e-3f);
            Assert.AreEqual(0f, a.y, 1e-3f);
        }

        [Test]
        public void EastIsNinetyAndUpIsNegative()
        {
            var east = Gaze.Angles(Vector3.zero, new Vector3(3f, 0f, 0f));
            Assert.AreEqual(90f, east.x, 1e-3f);
            var up = Gaze.Angles(Vector3.zero, new Vector3(0f, 2f, 2f));
            Assert.AreEqual(-45f, up.y, 1e-3f, "見上げるのは負");
            var down = Gaze.Angles(Vector3.zero, new Vector3(0f, -1f, 1f));
            Assert.AreEqual(45f, down.y, 1e-3f, "見下ろすのは正");
        }

        /// <summary>目の置き場から見て、向きの真ん中に target が来ているか（度のずれ）</summary>
        static float Miss(Vector3 foot, float bodyYaw, Vector3 eye, Vector2 a, Vector3 target)
        {
            var at = foot + Quaternion.Euler(0f, bodyYaw, 0f) * eye;
            var forward = Quaternion.Euler(a.y, a.x, 0f) * Vector3.forward;
            return Vector3.Angle(forward, target - at);
        }

        [Test]
        public void TurningTheBodyCarriesTheEyeAndStillCentresTheThing()
        {
            // 目は体の前へ 0.22 m。真横の近い物は、今の目の置き場から測った向きへ回すと、目が回ったぶん外れる
            var foot = Vector3.zero;
            var eye = new Vector3(0f, 1.6f, 0.22f);
            var target = new Vector3(0.8f, 1.1f, 0f);
            var a = Gaze.Toward(foot, 0f, true, eye, target);
            Assert.That(Miss(foot, a.x, eye, a, target), Is.LessThan(0.05f), "体を回したあとの目から見て真ん中に来る");
            var once = Gaze.Angles(foot + eye, target);
            Assert.That(Miss(foot, once.x, eye, once, target), Is.GreaterThan(1f), "今の目の置き場から測ると外れる");
        }

        [Test]
        public void SeatedTheEyeStaysWhereTheBodyPutsIt()
        {
            // 座っていて首だけ振るなら、目の置き場は体の向きのまま動かない
            var foot = new Vector3(1f, 0f, 2f);
            var eye = new Vector3(0f, 1.1f, 0.22f);
            var target = new Vector3(-0.5f, 0.8f, 2.6f);
            var a = Gaze.Toward(foot, 30f, false, eye, target);
            Assert.That(Miss(foot, 30f, eye, a, target), Is.LessThan(0.05f));
        }

        [Test]
        public void BlendTakesTheShortWayRound()
        {
            var mid = Gaze.Blend(new Vector2(350f, 0f), new Vector2(10f, 20f), 0.5f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, mid.x), 1e-3f, "350 度から 10 度へは 0 度を通る");
            Assert.AreEqual(10f, mid.y, 1e-3f);
            var end = Gaze.Blend(new Vector2(350f, 0f), new Vector2(10f, 20f), 2f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(end.x, 10f), 1e-3f, "寄せすぎない");
        }

        [Test]
        public void ChaseClosesMostOfTheGapInOneLag()
        {
            var now = new Vector2(0f, 0f);
            var want = new Vector2(10f, -10f);
            var step = Gaze.Chase(now, want, 0.12f, 0.12f);
            Assert.AreEqual(10f * (1f - Mathf.Exp(-1f)), step.x, 1e-3f);
            for (var i = 0; i < 60; i++) now = Gaze.Chase(now, want, 1f / 60f, 0.12f);
            Assert.AreEqual(10f, now.x, 0.01f, "一秒追えば追い付く");
            Assert.AreEqual(-10f, now.y, 0.01f);
        }

        [Test]
        public void ChaseWithNoLagSnaps()
        {
            var want = new Vector2(33f, 4f);
            Assert.AreEqual(want, Gaze.Chase(Vector2.zero, want, 0.016f, 0f));
        }
    }
}
