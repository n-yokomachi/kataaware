using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class ChoiceTests
    {
        [Test]
        public void ItOpensOnYes()
        {
            var c = new Choice("チップを抜く");
            Assert.AreEqual(0, c.Index);
            Assert.IsTrue(c.Accepted);
            Assert.AreEqual(Choice.Yes, c.Label);
        }

        [Test]
        public void RightMovesToNoAndLeftComesBack()
        {
            var c = new Choice("チップを抜く");
            c.Move(1);
            Assert.IsFalse(c.Accepted);
            Assert.AreEqual(Choice.No, c.Label);
            c.Move(-1);
            Assert.IsTrue(c.Accepted);
        }

        [Test]
        public void ItStopsAtBothEnds()
        {
            var c = new Choice("問い");
            c.Move(-1);
            Assert.AreEqual(0, c.Index);
            c.Move(1);
            c.Move(1);
            c.Move(1);
            Assert.AreEqual(1, c.Index);
        }

        [Test]
        public void TheDefaultIsYesThenNo()
        {
            var c = new Choice("問い");
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual(Choice.Yes, c.Options[0]);
            Assert.AreEqual(Choice.No, c.Options[1]);
        }

        [Test]
        public void ItHoldsAnyNumberOfOptions()
        {
            var c = new Choice("行き先", new[] { "北", "東", "南", "西" });
            Assert.AreEqual(4, c.Count);
            for (var i = 0; i < 6; i++) c.Move(1);
            Assert.AreEqual(3, c.Index, "右の端で止まる");
            Assert.AreEqual("西", c.Label);
            Assert.IsFalse(c.Accepted);
        }

        [Test]
        public void EmptyOptionsFallBackToYesAndNo()
        {
            var c = new Choice("問い", new string[0]);
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual(Choice.Yes, c.Label);
        }

        [Test]
        public void HoverChoosesTheCardAndIgnoresTheOutside()
        {
            var c = new Choice("問い", new[] { "一", "二", "三" });
            c.Hover(2);
            Assert.AreEqual(2, c.Index);
            c.Hover(-1);
            c.Hover(3);
            Assert.AreEqual(2, c.Index, "札の外は選びを変えない");
        }

        [Test]
        public void HoldingAKeyMovesOnlyOnce()
        {
            var c = new Choice("問い", new[] { "一", "二", "三" });
            c.Tilt(1);
            c.Tilt(1);
            c.Tilt(1);
            Assert.AreEqual(1, c.Index, "倒したままでは流れない");
            c.Tilt(0);
            c.Tilt(1);
            Assert.AreEqual(2, c.Index, "離して倒し直すと一つ動く");
            c.Tilt(-1);
            Assert.AreEqual(1, c.Index, "逆へ倒すとすぐ動く");
        }

        [Test]
        public void TheSideKeysTurnIntoSteps()
        {
            Assert.AreEqual(1, PlayerController.SideStep(new Vector2(1f, 0f)));
            Assert.AreEqual(-1, PlayerController.SideStep(new Vector2(-1f, 0f)));
            Assert.AreEqual(0, PlayerController.SideStep(new Vector2(0.3f, 1f)), "前へ歩くだけでは動かない");
        }
    }
}
