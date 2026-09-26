using NUnit.Framework;

namespace HalfAware.Tests
{
    public class ConsoleMenuTests
    {
        [Test]
        public void TheButtonsReadAsDecided()
        {
            CollectionAssert.AreEqual(new[] { "記憶する", "思い出す", "目を閉じる", "デバッグ" }, ConsoleMenu.Labels);
            Assert.AreEqual(ConsoleMenu.Labels.Length, System.Enum.GetValues(typeof(ConsoleAction)).Length);
        }

        [Test]
        public void ItOpensOnTheFirstButton()
        {
            var m = new ConsoleMenu();
            m.Move(2);
            m.Reset();
            Assert.AreEqual(0, m.Index);
            Assert.AreEqual(ConsoleAction.Remember, m.Selected);
            Assert.IsFalse(m.Listing);
        }

        [Test]
        public void LeftAndRightStopAtTheEnds()
        {
            var m = new ConsoleMenu();
            m.Move(-1);
            Assert.AreEqual(0, m.Index);
            m.Move(1);
            m.Move(1);
            Assert.AreEqual(ConsoleAction.CloseEyes, m.Selected);
            m.Move(5);
            Assert.AreEqual(ConsoleAction.Debug, m.Selected);
        }

        [Test]
        public void HoverSelectsAndIgnoresStrays()
        {
            var m = new ConsoleMenu();
            m.Hover(2);
            Assert.AreEqual(2, m.Index);
            m.Hover(-1);
            m.Hover(9);
            Assert.AreEqual(2, m.Index);
        }

        [Test]
        public void RememberOpensTheThreeManualSlots()
        {
            var m = new ConsoleMenu();
            Assert.AreEqual(ConsoleAction.Remember, m.Decide("Room"));
            Assert.AreEqual(ConsolePanel.Remember, m.Panel);
            Assert.IsFalse(m.Listing);
            Assert.AreEqual(ConsoleMenu.RememberRows, m.Rows);
            Assert.AreEqual(0, m.Row);
            Assert.AreEqual(SaveSlot.First, m.RowSlot);
            // 空きでも書ける
            m.MoveRow(1);
            m.MoveRow(1);
            m.MoveRow(1);
            Assert.AreEqual(SaveSlot.Third, m.RowSlot);
            m.HoverRow(1);
            Assert.AreEqual(SaveSlot.Second, m.RowSlot);
            // もう一度決めると閉じる
            m.Decide("Room");
            Assert.AreEqual(ConsolePanel.None, m.Panel);
            Assert.IsNull(m.RowSlot);
        }

        [Test]
        public void RecallSkipsTheEmptySlots()
        {
            var m = new ConsoleMenu();
            m.Fill(SaveSlot.Auto, false);
            m.Fill(SaveSlot.First, true);
            m.Fill(SaveSlot.Second, false);
            m.Fill(SaveSlot.Third, true);
            m.Hover((int)ConsoleAction.Recall);
            Assert.AreEqual(ConsoleAction.Recall, m.Decide("Room"));
            Assert.AreEqual(ConsolePanel.Recall, m.Panel);
            Assert.AreEqual(ConsoleMenu.RecallRows, m.Rows);
            // いちばん上の読める行から
            Assert.AreEqual(SaveSlot.First, m.RowSlot);
            m.MoveRow(1);
            Assert.AreEqual(SaveSlot.Third, m.RowSlot);
            m.MoveRow(1);
            Assert.AreEqual(SaveSlot.Third, m.RowSlot);
            m.MoveRow(-1);
            Assert.AreEqual(SaveSlot.First, m.RowSlot);
            m.MoveRow(-1);
            Assert.AreEqual(SaveSlot.First, m.RowSlot);
            // 空きには重ねても選ばれない
            m.HoverRow(0);
            m.HoverRow(2);
            Assert.AreEqual(SaveSlot.First, m.RowSlot);
            Assert.IsFalse(m.Usable(0));
            Assert.IsTrue(m.Usable(3));
        }

        [Test]
        public void RecallWithNothingSavedSelectsNothing()
        {
            var m = new ConsoleMenu();
            m.Hover((int)ConsoleAction.Recall);
            m.Decide("Room");
            Assert.AreEqual(ConsolePanel.Recall, m.Panel);
            Assert.AreEqual(-1, m.Row);
            Assert.IsNull(m.RowSlot);
            m.MoveRow(1);
            Assert.AreEqual(-1, m.Row);
        }

        [Test]
        public void ClosingTheEyesOpensNoPanel()
        {
            var m = new ConsoleMenu();
            m.Hover((int)ConsoleAction.Remember);
            m.Decide("Room");
            m.Hover((int)ConsoleAction.CloseEyes);
            Assert.AreEqual(ConsoleAction.CloseEyes, m.Decide("Room"));
            Assert.AreEqual(ConsolePanel.None, m.Panel);
        }

        [Test]
        public void MovingToAnotherButtonClosesThePanel()
        {
            var m = new ConsoleMenu();
            m.Decide("Room");
            Assert.AreEqual(ConsolePanel.Remember, m.Panel);
            m.Move(1);
            Assert.AreEqual(ConsolePanel.None, m.Panel);
        }

        [Test]
        public void DebugOpensTheSceneListOnWhereYouAre()
        {
            var m = new ConsoleMenu();
            m.Hover(3);
            Assert.AreEqual(ConsoleAction.Debug, m.Decide("Dive"));
            Assert.IsTrue(m.Listing);
            Assert.AreEqual(3, m.Row);
            // 今いる場面へは飛ばない
            Assert.IsNull(m.RowTarget("Dive"));
            m.MoveRow(1);
            Assert.AreEqual("Rest", m.RowTarget("Dive"));
            // もう一度決めると閉じる
            m.Decide("Dive");
            Assert.IsFalse(m.Listing);
        }

        [Test]
        public void TheSceneListFollowsTheMouseAndStopsAtTheEnds()
        {
            var m = new ConsoleMenu();
            m.Hover(3);
            m.Decide("Room");
            m.HoverRow(6);
            Assert.AreEqual("Village", m.RowTarget("Room"));
            m.MoveRow(1);
            Assert.AreEqual(6, m.Row);
            m.HoverRow(99);
            Assert.AreEqual(6, m.Row);
            m.MoveRow(-10);
            Assert.AreEqual(0, m.Row);
            Assert.IsNull(m.RowTarget("Room"));
        }

        [Test]
        public void MovingOffDebugOrBackingOutClosesTheList()
        {
            var m = new ConsoleMenu();
            m.Hover(3);
            m.Decide("Room");
            Assert.IsTrue(m.Back());
            Assert.IsFalse(m.Listing);
            Assert.IsFalse(m.Back());
            m.Decide("Room");
            m.Move(-1);
            Assert.IsFalse(m.Listing);
        }

        [Test]
        public void HoveringAnotherButtonKeepsTheListOpen()
        {
            var m = new ConsoleMenu();
            m.Hover(3);
            m.Decide("Room");
            m.Hover(1);
            Assert.IsTrue(m.Listing);
        }

        // ---- 頭の行 ------------------------------------------------------------

        [Test]
        public void EveryListedSceneHasAPlace()
        {
            foreach (var scene in SceneMenu.Scenes)
                Assert.AreNotEqual(scene, ConsolePlace.For(scene), scene);
            StringAssert.StartsWith("2166/08/15", ConsolePlace.For("Room"));
            StringAssert.Contains("自室", ConsolePlace.For("Room"));
            StringAssert.Contains("村", ConsolePlace.For("Village"));
            Assert.AreEqual(ConsolePlace.Diving, ConsolePlace.For("Dive"));
        }

        [Test]
        public void AMemoryShowsDivingWithItsTimeAndPlace()
        {
            var line = ConsolePlace.Dive("女　6　『メイ』　2156/03/02 07:14", DiveIds.Estate);
            Assert.AreEqual("潜行中　2156/03/02 07:14　団地", line);
            Assert.AreEqual("潜行中", ConsolePlace.Dive(null, "nowhere"));
        }
    }
}
