using NUnit.Framework;

namespace HalfAware.Tests
{
    public class TakenTests
    {
        [Test]
        public void TheNamedItemTakesIt()
        {
            Assert.IsTrue(Taken.Triggers("chips", "chips", false));
        }

        [Test]
        public void AnotherItemLeavesItAlone()
        {
            Assert.IsFalse(Taken.Triggers("ashtray", "chips", false));
        }

        [Test]
        public void ItOnlyHappensOnce()
        {
            Assert.IsFalse(Taken.Triggers("chips", "chips", true), "もう持っていった後は動かない");
        }

        [Test]
        public void WithNothingNamedItNeverFires()
        {
            Assert.IsFalse(Taken.Triggers("chips", "", false));
            Assert.IsFalse(Taken.Triggers("chips", null, false));
        }
    }
}
