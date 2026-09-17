using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class MarkedTests
    {
        static HashSet<string> Done(params string[] ids)
        {
            return new HashSet<string>(ids);
        }

        [Test]
        public void SomethingYouCanExamineGetsAPin()
        {
            var item = new FakeItem("ashtray", Vector3.zero);
            Assert.IsTrue(InteractionPicker.Marked(item, Done()));
        }

        [Test]
        public void WhatIsDoneLosesItsPin()
        {
            var item = new FakeItem("ashtray", Vector3.zero);
            Assert.IsFalse(InteractionPicker.Marked(item, Done("ashtray")));
        }

        [Test]
        public void SomethingYouCanExamineAgainKeepsIt()
        {
            var item = new FakeItem("window", Vector3.zero) { Once = false };
            Assert.IsTrue(InteractionPicker.Marked(item, Done("window")), "何度でも調べられる物は印が残る");
        }

        [Test]
        public void WhatIsNotThereYetHasNoPin()
        {
            var item = new FakeItem("jack", Vector3.zero) { Active = false };
            Assert.IsFalse(InteractionPicker.Marked(item, Done()));
        }

        [Test]
        public void APrerequisiteHoldsThePinBack()
        {
            var item = new FakeItem("door", Vector3.zero) { After = new[] { "cigarette" } };
            Assert.IsFalse(InteractionPicker.Marked(item, Done()));
            Assert.IsTrue(InteractionPicker.Marked(item, Done("cigarette")));
        }

        [Test]
        public void AHintDoesNotEarnAPin()
        {
            var item = new FakeItem("door", Vector3.zero) { After = new[] { "cigarette" } };
            item.Hints["cigarette"] = new[] { "その前に一服したい" };
            Assert.IsFalse(InteractionPicker.Marked(item, Done()), "文が出るだけの物には用が無い");
        }

        [Test]
        public void NothingIsNotMarked()
        {
            Assert.IsFalse(InteractionPicker.Marked(null, Done()));
        }
    }
}
