using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class InteractionPickerTests
    {
        static readonly Vector3 Cam = new Vector3(0f, 1.6f, 0f);
        static readonly Vector3 Fwd = Vector3.forward;

        static FakeItem At(string id, float x, float z) => new FakeItem(id, new Vector3(x, 1.6f, z));

        static HashSet<string> Done(params string[] ids) => new HashSet<string>(ids);

        // 場面 1 の座った目（首だけ振るので、どこを狙っても同じ所）と、左の机の煙草の箱と灰皿の置き場（Room.unity の Interactable）
        static readonly Vector3 RoomEye = new Vector3(1.50f, 1.309f, 1.42f);
        static readonly Vector3 RoomBox = new Vector3(0.80f, 0.70f, 2.10f);
        static readonly Vector3 RoomAshtray = new Vector3(0.78f, 0.70f, 2.30f);

        static IInteractable Pick(ICollection<string> done, params IInteractable[] items) =>
            InteractionPicker.Select(Cam, Fwd, items, done);

        [Test]
        public void PicksTheNearestItemInsideTheRadiusAndTheViewCone()
        {
            var picked = Pick(Done(), At("far", 0f, 1.8f), At("near", 0f, 1f), At("behind", 0f, -1f), At("out", 0f, 5f));
            Assert.That(picked.Id, Is.EqualTo("near"));
        }

        [Test]
        public void PicksTheItemAtTheCentreOfTheViewOverANearerOneToTheSide()
        {
            // 狙った物を選ぶ（2026-09-29）。手前に別の物があっても、視線の真ん中にある方が勝つ。
            // x=0.25, z=1 は視線から約 14°
            var picked = Pick(Done(), At("side", 0.25f, 1f), At("aimed", 0f, 1.6f));
            Assert.That(picked.Id, Is.EqualTo("aimed"));
        }

        [Test]
        public void PicksTheNearerOfTwoItemsAlmostInLine()
        {
            // 角度の差が TieAngle（約 3°）の内なら、近い方。x=0.03, z=1 は約 1.7°
            var picked = Pick(Done(), At("far", 0f, 1.8f), At("near", 0.03f, 1f));
            Assert.That(picked.Id, Is.EqualTo("near"));
        }

        [Test]
        public void PicksTheCloserToTheCentreWhenTheAnglesDifferByMoreThanTheTie()
        {
            // x=0.1, z=1 は約 5.7°、x=0.02, z=1.8 は約 0.6°。差が TieAngle を越えるので、遠くても真ん中の方
            var picked = Pick(Done(), At("near", 0.1f, 1f), At("far", 0.02f, 1.8f));
            Assert.That(picked.Id, Is.EqualTo("far"));
        }

        [Test]
        public void TheOrderOfTheItemsDoesNotChangeThePick()
        {
            var a = At("a", 0.25f, 1f);
            var b = At("b", 0f, 1.6f);
            Assert.That(Pick(Done(), a, b).Id, Is.EqualTo("b"));
            Assert.That(Pick(Done(), b, a).Id, Is.EqualTo("b"));
        }

        [Test]
        public void SeatedAtTheDeskTheAshtrayIsPickedWhenAimedAtEvenThoughTheBoxIsNearer()
        {
            // 場面 1 の座った目から、左の机の煙草の箱（1.15 m）と灰皿（1.29 m）は約 7° 離れて並ぶ。
            // 前は近い方が勝ち、灰皿を狙っても箱が選ばれていた
            var box = new FakeItem("cigarette-box", RoomBox);
            var ashtray = new FakeItem("ashtray", RoomAshtray);
            var items = new IInteractable[] { box, ashtray };
            Assert.AreSame(ashtray, InteractionPicker.Select(RoomEye, (RoomAshtray - RoomEye).normalized, items, Done()));
            Assert.AreSame(box, InteractionPicker.Select(RoomEye, (RoomBox - RoomEye).normalized, items, Done()));
        }

        [Test]
        public void IgnoresItemsBehindTheCamera()
        {
            Assert.That(Pick(Done(), At("behind", 0f, -1f)), Is.Null);
        }

        [Test]
        public void IgnoresItemsOutsideTheRadius()
        {
            Assert.That(Pick(Done(), At("out", 0f, 5f)), Is.Null);
        }

        [Test]
        public void PicksAnItemAtTheEyeRegardlessOfTheViewDirection()
        {
            Assert.That(InteractionPicker.Select(Cam, Vector3.back, new[] { At("here", 0f, 0f) }, Done()).Id, Is.EqualTo("here"));
        }

        [Test]
        public void AcceptsItemsInsideTheViewConeAndRejectsItemsJustOutsideIt()
        {
            // MaxAngle は 0.7 rad（約 40°）。x=0.6, z=1 は約 31°、x=0.9, z=1 は約 42°
            var inside = At("in", 0.6f, 1f);
            var outside = At("out", 0.9f, 1f);
            Assert.That(Pick(Done(), inside).Id, Is.EqualTo("in"));
            Assert.That(Pick(Done(), outside), Is.Null);
            Assert.That(InteractionPicker.Select(Cam, Fwd, new[] { outside }, Done(), 0.8f).Id, Is.EqualTo("out"));
        }

        [Test]
        public void HonoursAPerItemRadius()
        {
            var wide = At("w", 0f, 5f);
            wide.Radius = 6f;
            Assert.That(Pick(Done(), wide).Id, Is.EqualTo("w"));
        }

        [Test]
        public void SkipsItemsAlreadyExaminedWhenOnceIsSet()
        {
            var picked = Pick(Done("near"), At("near", 0f, 1f), At("far", 0f, 1.8f));
            Assert.That(picked.Id, Is.EqualTo("far"));
        }

        [Test]
        public void KeepsRepeatableItemsSelectable()
        {
            var rep = At("r", 0f, 1f);
            rep.Once = false;
            Assert.That(Pick(Done("r"), rep).Id, Is.EqualTo("r"));
        }

        [Test]
        public void HidesItemsWhosePrerequisitesAreNotDone()
        {
            var gated = At("g", 0f, 1f);
            gated.After = new[] { "x" };
            Assert.That(Pick(Done(), gated), Is.Null);
            Assert.That(Pick(Done("x"), gated).Id, Is.EqualTo("g"));
        }

        [Test]
        public void KeepsAGatedItemSelectableWhenItHasAHintForTheUnmetPrerequisite()
        {
            var hinted = At("h", 0f, 1f);
            hinted.After = new[] { "x" };
            hinted.Hints["x"] = new[] { "先に x" };
            Assert.That(Pick(Done(), hinted).Id, Is.EqualTo("h"));
        }

        [Test]
        public void HidesAGatedItemWhenTheUnmetPrerequisiteHasNoHint()
        {
            var partly = At("p", 0f, 1f);
            partly.After = new[] { "x", "y" };
            partly.Hints["x"] = new[] { "先に x" };
            Assert.That(Pick(Done("x"), partly), Is.Null);
        }

        [Test]
        public void UsesTheHintOfTheFirstUnmetPrerequisiteOnly()
        {
            var later = At("l", 0f, 1f);
            later.After = new[] { "x", "y" };
            later.Hints["y"] = new[] { "先に y" };
            Assert.That(Pick(Done(), later), Is.Null);
            Assert.That(Pick(Done("x"), later).Id, Is.EqualTo("l"));
        }

        [Test]
        public void TreatsAnEmptyHintListAsNoHint()
        {
            var empty = At("e", 0f, 1f);
            empty.After = new[] { "x" };
            empty.Hints["x"] = new string[0];
            Assert.That(Pick(Done(), empty), Is.Null);
        }

        [Test]
        public void UnmetPrerequisiteReturnsTheFirstMissingIdOrNull()
        {
            var item = At("d", 0f, 0f);
            item.After = new[] { "a", "b" };
            Assert.That(InteractionPicker.UnmetPrerequisite(item, Done()), Is.EqualTo("a"));
            Assert.That(InteractionPicker.UnmetPrerequisite(item, Done("a")), Is.EqualTo("b"));
            Assert.That(InteractionPicker.UnmetPrerequisite(item, Done("a", "b")), Is.Null);
            Assert.That(InteractionPicker.UnmetPrerequisite(At("n", 0f, 0f), Done()), Is.Null);
        }

        [Test]
        public void SkipsAnItemThatIsNotInThePlaceRightNow()
        {
            var near = new FakeItem("arm", new Vector3(0f, 0f, 1f)) { Active = false };
            var far = new FakeItem("desk", new Vector3(0f, 0f, 1.6f));
            var picked = InteractionPicker.Select(Vector3.zero, Vector3.forward,
                new IInteractable[] { near, far }, new HashSet<string>());
            Assert.AreSame(far, picked, "伏せている対象は、近くても選ばれない");
        }

        [Test]
        public void PicksItBackUpOnceItIsInThePlaceAgain()
        {
            var near = new FakeItem("arm", new Vector3(0f, 0f, 1f)) { Active = false };
            var far = new FakeItem("desk", new Vector3(0f, 0f, 1.6f));
            var items = new IInteractable[] { near, far };
            near.Active = true;
            var picked = InteractionPicker.Select(Vector3.zero, Vector3.forward, items, new HashSet<string>());
            Assert.AreSame(near, picked);
        }
    }
}
