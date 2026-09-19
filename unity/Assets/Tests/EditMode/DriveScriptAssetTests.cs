using NUnit.Framework;
using UnityEditor;

namespace HalfAware.Tests
{
    public class DriveScriptAssetTests
    {
        const string Path = "Assets/Data/DriveScript.asset";

        static RoomScript Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            Assert.IsNotNull(asset, "文面のアセットが無い。HalfAware/Write the drive script を走らせる");
            return asset;
        }

        [Test]
        public void EveryTriggerHasItsOwnLines()
        {
            var script = Load();
            foreach (var id in DriveIds.Triggers)
            {
                var entry = script.Find(id);
                Assert.AreEqual(id, entry.id, id + " が文面に無い");
                Assert.Greater(entry.Lines.Count, 0, id + " に文が無い");
                Assert.IsNotEmpty(entry.Label, id + " に印の文が無い");
            }
        }

        [Test]
        public void EveryBandHasItsPage()
        {
            var script = Load();
            for (var i = 0; i < DriveIds.Triggers.Count; i++)
            {
                var page = script.Find(DriveIds.Page(i));
                Assert.AreEqual(DriveIds.Page(i), page.id, i + " 帯の段が文面に無い");
                Assert.Greater(page.Lines.Count, 0, i + " 帯の段に文が無い");
            }
        }

        [Test]
        public void TheGarageDoorIsThere()
        {
            var entry = Load().Find(DriveIds.Door);
            Assert.AreEqual(DriveIds.Door, entry.id);
            Assert.IsNotEmpty(entry.Label);
        }

        [Test]
        public void NoIdIsUsedTwice()
        {
            var ids = Load().Ids();
            CollectionAssert.AllItemsAreUnique(ids);
        }
    }
}
