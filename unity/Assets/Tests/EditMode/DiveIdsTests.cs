using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class DiveIdsTests
    {
        [Test]
        public void ThereAreFivePlaces()
        {
            CollectionAssert.AreEqual(
                new[] { "estate", "park", "train", "kitchen", "classroom" },
                DiveIds.Places);
        }

        [Test]
        public void TheListIsTheSixRowsOnTheMonitorInOrder()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 4, 5, 7 }, DiveIds.Listed);
        }
    }
}
