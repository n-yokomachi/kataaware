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
            // 一覧の終わりはエンディング。行の数は場面を足すたびに増えるので、数えて引く
            var last = SceneMenu.Count - 1;
            m.HoverRow(last);
            Assert.AreEqual("Ending", m.RowTarget("Room"));
            m.MoveRow(1);
            Assert.AreEqual(last, m.Row);
            m.HoverRow(99);
            Assert.AreEqual(last, m.Row);
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

        // ---- 右クリック --------------------------------------------------------

        [Test]
        public void TabOrRightClickOpensButNotOnTheTitle()
        {
            Assert.IsTrue(ConsoleMenu.Opens(true, false, false));
            Assert.IsTrue(ConsoleMenu.Opens(false, true, false));
            Assert.IsFalse(ConsoleMenu.Opens(false, false, false));
            Assert.IsFalse(ConsoleMenu.Opens(true, false, true));
            Assert.IsFalse(ConsoleMenu.Opens(false, true, true));
        }

        [Test]
        public void BackClosesTheTopLayerFirst()
        {
            var m = new ConsoleMenu();
            m.Fill(SaveSlot.First, true);
            m.Decide("Room");
            Assert.IsNull(m.Commit());
            Assert.IsTrue(m.Asking);
            // 確かめ → 記憶するの枠（同じ行のまま） → 閉じる
            Assert.IsTrue(m.Back());
            Assert.IsFalse(m.Asking);
            Assert.AreEqual(ConsolePanel.Remember, m.Panel);
            Assert.AreEqual(SaveSlot.First, m.RowSlot);
            Assert.IsTrue(m.Back());
            Assert.AreEqual(ConsolePanel.None, m.Panel);
            Assert.IsFalse(m.Back());
        }

        [Test]
        public void BackFromEachPanelReturnsToTheButtons()
        {
            foreach (var action in new[] { ConsoleAction.Remember, ConsoleAction.Recall, ConsoleAction.Debug })
            {
                var m = new ConsoleMenu();
                m.Fill(SaveSlot.Auto, true);
                m.Hover((int)action);
                m.Decide("Room");
                Assert.AreEqual(ConsoleMenu.PanelOf(action), m.Panel, action.ToString());
                Assert.IsTrue(m.Back(), action.ToString());
                Assert.AreEqual(ConsolePanel.None, m.Panel, action.ToString());
                Assert.AreEqual((int)action, m.Index, action.ToString());
                Assert.IsFalse(m.Back(), action.ToString());
            }
        }

        // ---- 上書きの確かめ ----------------------------------------------------

        [Test]
        public void AnEmptySlotIsWrittenWithoutAsking()
        {
            var m = new ConsoleMenu();
            m.Fill(SaveSlot.First, false);
            m.Decide("Room");
            Assert.AreEqual(SaveSlot.First, m.Commit());
            Assert.IsFalse(m.Asking);
            Assert.IsNull(m.Asked);
        }

        [Test]
        public void AFilledSlotAsksAndStartsOnNo()
        {
            var m = new ConsoleMenu();
            m.Fill(SaveSlot.Second, true);
            m.Decide("Room");
            m.MoveRow(1);
            Assert.IsNull(m.Commit());
            Assert.IsTrue(m.Asking);
            Assert.AreEqual(SaveSlot.Second, m.Asked);
            Assert.AreEqual(ConsoleMenu.NoIndex, m.Answer);
            // いいえ: 書かずに記憶するの枠へ戻る
            Assert.IsNull(m.Confirm());
            Assert.IsFalse(m.Asking);
            Assert.AreEqual(ConsolePanel.Remember, m.Panel);
            Assert.AreEqual(SaveSlot.Second, m.RowSlot);
        }

        [Test]
        public void YesOverwrites()
        {
            var m = new ConsoleMenu();
            m.Fill(SaveSlot.Third, true);
            m.Decide("Room");
            m.HoverRow(2);
            m.Commit();
            m.MoveAnswer(-1);
            Assert.AreEqual(ConsoleMenu.YesIndex, m.Answer);
            Assert.AreEqual(SaveSlot.Third, m.Confirm());
            Assert.IsFalse(m.Asking);
            Assert.IsNull(m.Confirm());
        }

        [Test]
        public void OnlyTheRememberPanelAsks()
        {
            var m = new ConsoleMenu();
            m.Fill(SaveSlot.Auto, true);
            m.Fill(SaveSlot.First, true);
            // ボタンのまま・思い出す・デバッグでは確かめない
            Assert.IsNull(m.Commit());
            m.Hover((int)ConsoleAction.Recall);
            m.Decide("Room");
            Assert.IsNull(m.Commit());
            m.Hover((int)ConsoleAction.Debug);
            m.Decide("Room");
            Assert.IsNull(m.Commit());
            Assert.IsFalse(m.Asking);
        }

        [Test]
        public void WhileAskingOnlyTheAnswerMoves()
        {
            var m = new ConsoleMenu();
            m.Fill(SaveSlot.First, true);
            m.Decide("Room");
            m.Commit();
            // 左右は札を選び、両端で止まる。ボタン・行は動かず、枠も閉じない
            m.Move(-1);
            Assert.AreEqual(ConsoleMenu.YesIndex, m.Answer);
            m.Move(-1);
            Assert.AreEqual(ConsoleMenu.YesIndex, m.Answer);
            m.Move(1);
            m.Move(1);
            Assert.AreEqual(ConsoleMenu.NoIndex, m.Answer);
            Assert.AreEqual(ConsoleAction.Remember, m.Selected);
            m.Hover(3);
            m.HoverRow(2);
            m.MoveRow(1);
            m.Decide("Room");
            Assert.AreEqual(0, m.Index);
            Assert.AreEqual(0, m.Row);
            Assert.AreEqual(ConsolePanel.Remember, m.Panel);
            Assert.IsTrue(m.Asking);
            m.HoverAnswer(ConsoleMenu.YesIndex);
            Assert.AreEqual(ConsoleMenu.YesIndex, m.Answer);
            m.HoverAnswer(5);
            Assert.AreEqual(ConsoleMenu.YesIndex, m.Answer);
        }

        [Test]
        public void ReopeningForgetsTheQuestion()
        {
            var m = new ConsoleMenu();
            m.Fill(SaveSlot.First, true);
            m.Decide("Room");
            m.Commit();
            m.Reset();
            Assert.IsFalse(m.Asking);
            Assert.AreEqual(ConsolePanel.None, m.Panel);
        }

        // ---- 知らせ ------------------------------------------------------------

        [Test]
        public void ANoteGoesAfterAWhile()
        {
            var n = new ConsoleNote();
            Assert.IsFalse(n.Visible);
            n.Say(ConsoleMenu.Remembered, 10f, 1);
            Assert.IsTrue(n.Visible);
            Assert.AreEqual(1f, n.Alpha(10f));
            n.Step(10f + ConsoleNote.Seconds - 0.1f, 2, false);
            Assert.IsTrue(n.Visible);
            Assert.That(n.Alpha(10f + ConsoleNote.Seconds - 0.1f), Is.InRange(0.01f, 0.99f));
            n.Step(10f + ConsoleNote.Seconds, 3, false);
            Assert.IsFalse(n.Visible);
            Assert.AreEqual(0f, n.Alpha(10f));
        }

        [Test]
        public void ANoteGoesOnTheNextInputButNotTheOneThatMadeIt()
        {
            var n = new ConsoleNote();
            n.Say(ConsoleMenu.CannotRecall, 5f, 7);
            n.Step(5f, 7, true);
            Assert.IsTrue(n.Visible);
            n.Step(5.1f, 8, false);
            Assert.IsTrue(n.Visible);
            n.Step(5.2f, 9, true);
            Assert.IsFalse(n.Visible);
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
