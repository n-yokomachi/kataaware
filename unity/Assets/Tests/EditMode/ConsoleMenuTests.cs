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
        public void TheEmptyButtonsOnlySayTheyAreNotReady()
        {
            var m = new ConsoleMenu();
            Assert.AreEqual(ConsoleAction.Remember, m.Decide("Room"));
            Assert.IsFalse(m.Listing);
            StringAssert.Contains("記憶する", ConsoleMenu.Note(ConsoleAction.Remember));
            StringAssert.Contains(ConsoleMenu.NotYet, ConsoleMenu.Note(ConsoleAction.Recall));
            StringAssert.Contains("目を閉じる", ConsoleMenu.Note(ConsoleAction.CloseEyes));
            Assert.IsNull(ConsoleMenu.Note(ConsoleAction.Debug));
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
