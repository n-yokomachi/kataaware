using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>テスト用の調べる対象。必要な項目だけ設定する</summary>
    public sealed class FakeItem : IInteractable
    {
        public string Id { get; set; }
        public Vector3 Position { get; set; }
        public float Radius { get; set; } = InteractionPicker.DefaultRadius;
        public bool Required { get; set; }
        public bool Once { get; set; } = true;
        public IReadOnlyList<string> After { get; set; } = new string[0];
        public string Label { get; set; } = "調べる";
        public IReadOnlyList<string> Lines { get; set; } = new string[0];
        public Dictionary<string, string[]> Hints { get; } = new Dictionary<string, string[]>();

        public FakeItem(string id, Vector3 position)
        {
            Id = id;
            Position = position;
        }

        public IReadOnlyList<string> HintFor(string afterId)
        {
            string[] lines;
            return Hints.TryGetValue(afterId, out lines) ? lines : null;
        }
    }

    public class InteractionPickerTests
    {
        static readonly Vector3 Cam = new Vector3(0f, 1.6f, 0f);
        static readonly Vector3 Fwd = Vector3.forward;

        static FakeItem At(string id, float x, float z) => new FakeItem(id, new Vector3(x, 1.6f, z));

        static HashSet<string> Done(params string[] ids) => new HashSet<string>(ids);

        static IInteractable Pick(ICollection<string> done, params IInteractable[] items) =>
            InteractionPicker.Select(Cam, Fwd, items, done);

        [Test]
        public void PicksTheNearestItemInsideTheRadiusAndTheViewCone()
        {
            var picked = Pick(Done(), At("near", 0f, 1f), At("far", 0f, 1.8f), At("behind", 0f, -1f), At("out", 0f, 5f));
            Assert.That(picked.Id, Is.EqualTo("near"));
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
    }
}
