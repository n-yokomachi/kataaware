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
