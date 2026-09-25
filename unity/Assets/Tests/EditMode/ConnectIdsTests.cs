using NUnit.Framework;

namespace HalfAware.Tests
{
    public sealed class ConnectIdsTests
    {
        [Test]
        public void TheOrderHoldsEveryId()
        {
            CollectionAssert.AreEqual(
                new[] { "note", "coat", "chair", "jack", "monitor", "list", "dive" },
                ConnectIds.Order);
        }

        [Test]
        public void NoIdRepeats()
        {
            CollectionAssert.AllItemsAreUnique(ConnectIds.Order);
        }

        // ジャケットをコートハンガーに掛けてから座る。メモも座る前
        [Test]
        public void SheSitsOnlyAfterHangingTheJacket()
        {
            CollectionAssert.Contains(ConnectIds.After(ConnectIds.Chair), ConnectIds.Coat);
            CollectionAssert.Contains(ConnectIds.After(ConnectIds.Chair), ConnectIds.Note);
        }

        // メモとコートハンガーは、どちらを先に調べてもよい
        [Test]
        public void TheNoteAndTheCoatWaitForNothing()
        {
            Assert.That(ConnectIds.After(ConnectIds.Note), Is.Empty);
            Assert.That(ConnectIds.After(ConnectIds.Coat), Is.Empty);
        }

        // 前提はどれも場面 3 の id で、自分を待たない
        [Test]
        public void EveryPrerequisiteIsAnIdOfTheScene()
        {
            foreach (var id in ConnectIds.Order)
                foreach (var before in ConnectIds.After(id))
                {
                    CollectionAssert.Contains(ConnectIds.Order, before, id + " の前提");
                    Assert.That(before, Is.Not.EqualTo(id));
                }
        }
    }
}
