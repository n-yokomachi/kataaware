using NUnit.Framework;

namespace HalfAware.Tests
{
    public class DriveIdsTests
    {
        [Test]
        public void ThePagesAreNumbered()
        {
            Assert.AreEqual("drive.band0", DriveIds.Page(0));
            Assert.AreEqual("drive.band3", DriveIds.Page(3));
        }

        [Test]
        public void ItTellsAPageFromAnObject()
        {
            Assert.IsTrue(DriveIds.IsPage("drive.band0"));
            Assert.IsFalse(DriveIds.IsPage(DriveIds.Chips), "対象は段ではない");
            Assert.IsFalse(DriveIds.IsPage(null));
            foreach (var id in DriveIds.Triggers)
                Assert.IsFalse(DriveIds.IsPage(id), "きっかけの対象が段に見えている: " + id);
            foreach (var id in new[] { DriveIds.Door, DriveIds.Radio, DriveIds.Pocket, DriveIds.Fuel })
                Assert.IsFalse(DriveIds.IsPage(id), "対象が段に見えている: " + id);
        }

        [Test]
        public void TheTriggersAreAllDifferent()
        {
            var all = DriveIds.Triggers;
            Assert.AreEqual(5, all.Count, "帯は 5 つ");
            CollectionAssert.AllItemsAreUnique(all);
            Assert.AreEqual(DriveIds.Window, all[4], "最後の帯のきっかけは窓");
        }
    }
}
