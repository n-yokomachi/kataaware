using NUnit.Framework;

namespace HalfAware.Tests
{
    public class SmokePuffsTests
    {
        [Test]
        public void NothingBurnsBeforeItIsLit()
        {
            Assert.IsFalse(SmokePuffs.Lit(12f, -1f, 8f), "火を点けていない");
        }

        [Test]
        public void ItSmokesWhileItLasts()
        {
            Assert.IsTrue(SmokePuffs.Lit(10f, 10f, 8f));
            Assert.IsTrue(SmokePuffs.Lit(17.9f, 10f, 8f));
        }

        [Test]
        public void ItStopsAtTheEnd()
        {
            Assert.IsFalse(SmokePuffs.Lit(18f, 10f, 8f));
            Assert.IsFalse(SmokePuffs.Lit(40f, 10f, 8f));
        }

        [Test]
        public void NotBeforeItStarted()
        {
            Assert.IsFalse(SmokePuffs.Lit(9.9f, 10f, 8f));
        }
    }
}
