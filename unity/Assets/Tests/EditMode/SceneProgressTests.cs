using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class SceneProgressTests
    {
        static FakeItem Item(string id, bool required = false)
        {
            var item = new FakeItem(id, Vector3.zero);
            item.Required = required;
            item.Lines = new[] { id + " の文" };
            return item;
        }

        [Test]
        public void CollectsRequiredIdsOnly()
        {
            var p = new SceneProgress(new[] { Item("a", true), Item("b"), Item("c", true) });
            Assert.That(p.Required, Is.EqualTo(new[] { "a", "c" }));
        }

        [Test]
        public void IsCompleteWhenEveryRequiredIdIsDone()
        {
            var a = Item("a", true);
            var c = Item("c", true);
            var p = new SceneProgress(new[] { a, Item("b"), c });
            Assert.That(p.IsComplete, Is.False);
            p.Examine(a);
            Assert.That(p.IsComplete, Is.False);
            p.Examine(c);
            Assert.That(p.IsComplete, Is.True);
        }

        [Test]
        public void IsCompleteWithoutRequiredItems()
        {
            Assert.That(new SceneProgress(new[] { Item("b") }).IsComplete, Is.True);
        }

        [Test]
        public void ExamineReturnsTheLinesAndMarksTheItemDone()
        {
            var a = Item("a");
            var p = new SceneProgress(new[] { a });
            Assert.That(p.Examine(a), Is.EqualTo(new[] { "a の文" }));
            Assert.That(p.Done, Does.Contain("a"));
        }

        [Test]
        public void ExamineWithAnUnmetPrerequisiteReturnsTheHintOnlyAndDoesNotMarkDone()
        {
            var door = Item("door");
            door.After = new[] { "terminal" };
            door.Hints["terminal"] = new[] { "先に端末" };
            var p = new SceneProgress(new[] { door });
            Assert.That(p.Examine(door), Is.EqualTo(new[] { "先に端末" }));
            Assert.That(p.Done, Does.Not.Contain("door"));
        }

        [Test]
        public void ExamineWithAnUnmetPrerequisiteAndNoHintReturnsNoLines()
        {
            var door = Item("door");
            door.After = new[] { "terminal" };
            var p = new SceneProgress(new[] { door });
            Assert.That(p.Examine(door), Is.Empty);
            Assert.That(p.Done, Does.Not.Contain("door"));
        }

        [Test]
        public void ExamineMarksAGatedItemDoneOnceItsPrerequisiteIsDone()
        {
            var door = Item("door");
            door.After = new[] { "terminal" };
            door.Hints["terminal"] = new[] { "先に端末" };
            var terminal = Item("terminal");
            var p = new SceneProgress(new[] { terminal, door });
            Assert.That(p.Examine(door), Is.EqualTo(new[] { "先に端末" }));
            p.Examine(terminal);
            Assert.That(p.Examine(door), Is.EqualTo(new[] { "door の文" }));
            Assert.That(p.Done, Does.Contain("door"));
        }
        [Test]
        public void SomethingThatAsksIsNotDoneUntilItIsConfirmed()
        {
            var chips = Item("chips", true);
            chips.Asks = true;
            chips.Question = "チップを抜く";
            chips.AfterYes = new[] { "抜いた" };
            var p = new SceneProgress(new[] { chips });
            var said = p.Examine(chips);
            Assert.That(said, Is.EqualTo(chips.Lines), "文はその場で出る");
            Assert.That(p.Done, Does.Not.Contain("chips"), "はいを選ぶまでは済んでいない");
            Assert.That(p.IsComplete, Is.False);
        }

        [Test]
        public void ConfirmingFinishesItAndReturnsWhatFollows()
        {
            var chips = Item("chips", true);
            chips.Asks = true;
            chips.AfterYes = new[] { "抜いた" };
            var p = new SceneProgress(new[] { chips });
            p.Examine(chips);
            var said = p.Confirm(chips);
            Assert.That(said, Is.EqualTo(new[] { "抜いた" }));
            Assert.That(p.Done, Does.Contain("chips"));
            Assert.That(p.IsComplete, Is.True);
        }

        [Test]
        public void SayingNoLeavesItAsItWas()
        {
            var chips = Item("chips", true);
            chips.Asks = true;
            var p = new SceneProgress(new[] { chips });
            p.Examine(chips);
            // いいえのときは Confirm を呼ばない
            Assert.That(p.Done, Does.Not.Contain("chips"));
            Assert.That(p.Examine(chips), Is.EqualTo(chips.Lines), "もう一度調べられる");
        }

        [Test]
        public void SomethingThatDoesNotAskIsDoneAtOnce()
        {
            var ashtray = Item("ashtray");
            var p = new SceneProgress(new[] { ashtray });
            p.Examine(ashtray);
            Assert.That(p.Done, Does.Contain("ashtray"));
        }
    }
}
