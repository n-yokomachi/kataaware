using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class ArmReachTests
    {
        const float Upper = 0.25f;
        const float Lower = 0.23f;

        [Test]
        public void TheElbowKeepsBothBonesTheirLength()
        {
            var a = Vector3.zero;
            var target = new Vector3(0.1f, -0.3f, 0.2f);
            var e = ArmReach.Elbow(a, Upper, Lower, target, new Vector3(1f, -1f, -1f));
            Assert.AreEqual(Upper, Vector3.Distance(a, e), 1e-4f, "上腕の長さは変わらない");
            Assert.AreEqual(Lower, Vector3.Distance(e, target), 1e-4f, "肘から狙いまでが前腕の長さ");
        }

        [Test]
        public void TheElbowGoesTowardThePole()
        {
            var a = Vector3.zero;
            var target = new Vector3(0f, -0.4f, 0f);
            var right = ArmReach.Elbow(a, Upper, Lower, target, new Vector3(1f, -0.2f, 0f));
            var left = ArmReach.Elbow(a, Upper, Lower, target, new Vector3(-1f, -0.2f, 0f));
            Assert.Greater(right.x, 0f, "pole が右なら肘は右へ出る");
            Assert.Less(left.x, 0f, "pole が左なら肘は左へ出る");
        }

        [Test]
        public void OutOfReachItStretchesTowardTheTarget()
        {
            var a = Vector3.zero;
            var target = new Vector3(0f, 0f, 2f);
            var e = ArmReach.Elbow(a, Upper, Lower, target, Vector3.up);
            Assert.AreEqual(Upper, Vector3.Distance(a, e), 1e-4f);
            Assert.Greater(e.z, Upper * 0.99f, "伸ばしきって狙いの方を向く");
        }

        [Test]
        public void SolveBringsTheHandOntoTheTarget()
        {
            var root = new GameObject("shoulder").transform;
            var elbow = new GameObject("elbow").transform;
            var hand = new GameObject("hand").transform;
            try
            {
                elbow.SetParent(root, false);
                elbow.localPosition = new Vector3(0f, -Upper, 0f);
                hand.SetParent(elbow, false);
                hand.localPosition = new Vector3(0f, -Lower, 0f);
                var target = new Vector3(0.15f, -0.25f, 0.2f);
                ArmReach.Solve(root, elbow, hand, target, new Vector3(0.5f, -0.2f, -0.5f), 1f);
                Assert.AreEqual(0f, Vector3.Distance(hand.position, target), 1e-4f, "手が狙いに届く");
                Assert.AreEqual(Upper, Vector3.Distance(root.position, elbow.position), 1e-4f, "骨の長さは変わらない");
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void MoveWithNoWeightLeavesTheArmAlone()
        {
            var root = new GameObject("shoulder").transform;
            var elbow = new GameObject("elbow").transform;
            var hand = new GameObject("hand").transform;
            try
            {
                elbow.SetParent(root, false);
                elbow.localPosition = new Vector3(0f, -Upper, 0f);
                hand.SetParent(elbow, false);
                hand.localPosition = new Vector3(0f, -Lower, 0f);
                var before = hand.position;
                ArmReach.Move(root, elbow, hand, new Vector3(0.2f, 0f, 0.2f), Quaternion.identity, Vector3.right, 0f);
                Assert.AreEqual(0f, Vector3.Distance(hand.position, before), 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void TwistAngleReadsTheTurnAboutTheAxis()
        {
            var axis = new Vector3(0.2f, 1f, 0.1f).normalized;
            Assert.AreEqual(40f, ArmReach.TwistAngle(Quaternion.AngleAxis(40f, axis), axis), 1e-3f);
            Assert.AreEqual(-70f, ArmReach.TwistAngle(Quaternion.AngleAxis(-70f, axis), axis), 1e-3f);
            var across = Vector3.Cross(axis, Vector3.right).normalized;
            Assert.AreEqual(0f, ArmReach.TwistAngle(Quaternion.AngleAxis(50f, across), axis), 1e-3f, "軸に直交する回しはひねりに数えない");
        }

        /// <summary>肩・前腕・手の三本。前腕の軸は y の負の向き。束ねた姿勢は今の向き（ひねり 0）</summary>
        static Transform[] Chain(out ArmReach.Rest rest)
        {
            var root = new GameObject("shoulder").transform;
            var elbow = new GameObject("elbow").transform;
            var hand = new GameObject("hand").transform;
            elbow.SetParent(root, false);
            elbow.localPosition = new Vector3(0f, -Upper, 0f);
            hand.SetParent(elbow, false);
            hand.localPosition = new Vector3(0f, -Lower, 0f);
            rest = new ArmReach.Rest { lower = elbow.localRotation, hand = hand.localRotation, valid = true };
            return new[] { root, elbow, hand };
        }

        [Test]
        public void UntwistMovesHalfTheTurnIntoTheForearmAndKeepsTheHand()
        {
            ArmReach.Rest rest;
            var c = Chain(out rest);
            try
            {
                c[2].localRotation = Quaternion.AngleAxis(80f, Vector3.down);
                var keep = c[2].rotation;
                ArmReach.Untwist(c[1], c[2], rest, 0.5f);
                float forearm, wrist;
                ArmReach.Twists(c[1], c[2], rest, out forearm, out wrist);
                Assert.AreEqual(40f, forearm, 0.5f, "前腕が半分持つ");
                Assert.AreEqual(40f, wrist, 0.5f, "手首に半分残る");
                Assert.Less(Quaternion.Angle(keep, c[2].rotation), 0.01f, "手の世界の向きは変わらない");
            }
            finally
            {
                Object.DestroyImmediate(c[0].gameObject);
            }
        }

        [Test]
        public void CarryReachesEachEndWithItsOwnWrist()
        {
            ArmReach.Rest rest;
            var c = Chain(out rest);
            try
            {
                var pole = new Vector3(0.5f, -0.2f, -0.5f);
                var a = new Vector3(0.10f, -0.30f, 0.20f);
                var b = new Vector3(-0.05f, -0.25f, 0.30f);
                var ra = Quaternion.Euler(20f, 30f, 10f);
                var rb = Quaternion.Euler(-30f, 60f, 40f);
                ArmReach.Carry(c[0], c[1], c[2], b, a, ra, b, rb, pole, 1f);
                Assert.AreEqual(0f, Vector3.Distance(c[2].position, b), 1e-4f, "運び終わりで B に届く");
                Assert.Less(Quaternion.Angle(c[2].rotation, rb), 0.5f, "運び終わりで B の向き");
                ArmReach.Carry(c[0], c[1], c[2], a, a, ra, b, rb, pole, 0f);
                Assert.AreEqual(0f, Vector3.Distance(c[2].position, a), 1e-4f, "運び始めは A");
                Assert.Less(Quaternion.Angle(c[2].rotation, ra), 0.5f, "運び始めは A の向き");
            }
            finally
            {
                Object.DestroyImmediate(c[0].gameObject);
            }
        }

        [Test]
        public void MoveAtFullWeightLandsOnTheTarget()
        {
            ArmReach.Rest rest;
            var c = Chain(out rest);
            try
            {
                var target = new Vector3(0.12f, -0.28f, 0.22f);
                var turn = Quaternion.Euler(10f, -40f, 25f);
                ArmReach.Move(c[0], c[1], c[2], target, turn, new Vector3(0.5f, -0.2f, -0.5f), 1f);
                Assert.AreEqual(0f, Vector3.Distance(c[2].position, target), 1e-4f);
                Assert.Less(Quaternion.Angle(c[2].rotation, turn), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(c[0].gameObject);
            }
        }

        [Test]
        public void HandForPutsTheHoldOnTheWorldPose()
        {
            var holdLocal = new Vector3(0.02f, -0.1f, 0.03f);
            var holdRotation = Quaternion.Euler(10f, 20f, 30f);
            var worldPosition = new Vector3(1f, 2f, 3f);
            var worldRotation = Quaternion.Euler(-40f, 5f, 70f);
            Vector3 hp;
            Quaternion hr;
            ArmReach.HandFor(worldPosition, worldRotation, holdLocal, holdRotation, out hp, out hr);
            Assert.AreEqual(0f, Vector3.Distance(hp + hr * holdLocal, worldPosition), 1e-5f, "置き所が狙いの位置に来る");
            Assert.AreEqual(0f, Quaternion.Angle(hr * holdRotation, worldRotation), 1e-3f, "置き所が狙いの向きに来る");
        }
    }
}
