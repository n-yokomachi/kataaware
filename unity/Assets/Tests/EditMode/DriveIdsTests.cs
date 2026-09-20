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
            foreach (var id in new[] { DriveIds.Door, DriveIds.Button })
                Assert.IsFalse(DriveIds.IsPage(id), "対象が段に見えている: " + id);
        }

        [Test]
        public void TheTriggersAreAllDifferent()
        {
            var all = DriveIds.Triggers;
            // 設計書の改訂で 5 つから 3 つになった（2026-09-16-scenario-design.md 7 節）
            Assert.AreEqual(3, all.Count, "景色は 3 つ");
            CollectionAssert.AllItemsAreUnique(all);
            Assert.AreEqual(DriveIds.Window, all[all.Count - 1], "最後の景色のきっかけは窓");
        }
    }
}
