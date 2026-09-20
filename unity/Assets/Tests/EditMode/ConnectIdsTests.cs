using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class ConnectIdsTests
    {
        [Test]
        public void TheOrderHoldsEveryId()
        {
            CollectionAssert.AreEqual(
                new[] { "note", "chair", "jack", "monitor", "list", "dive" },
                ConnectIds.Order);
        }

        [Test]
        public void NoIdRepeats()
        {
            CollectionAssert.AllItemsAreUnique(ConnectIds.Order);
        }
    }
}
