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
                Assert.IsNotEmpty(entry.label, id + " に印の文が無い");
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
            Assert.IsNotEmpty(entry.label);
        }

        [Test]
        public void NoIdIsUsedTwice()
        {
            var ids = Load().Ids();
            CollectionAssert.AllItemsAreUnique(ids);
        }

        // ガレージの扉だけ、乗り込めば後戻りできないので二択で確かめる（場面 1 の扉・場面 2 のテーブルと同じ扱い）。
        // それ以外が誤って二択を出すと SceneProgress.Examine が Done に加えなくなり、
        // きっかけの対象（drive.window など）を調べても帯や場面が終わらなくなる
        [Test]
        public void OnlyTheDoorAsks()
        {
            var script = Load();
            var door = script.Find(DriveIds.Door);
            Assert.That(door.Asks, Is.True, DriveIds.Door + " は二択を出す");
            Assert.That(door.choice.question, Is.EqualTo("車に乗り込む"));

            foreach (var id in script.Ids())
            {
                if (id == DriveIds.Door) continue;
                Assert.IsFalse(script.Find(id).Asks, id + " が二択を出そうとしている");
            }
        }

        // NoIdIsUsedTwice は重複が無いことしか見ない。id の集合そのものを固定して、
        // ラジオや上着のポケットのような、どの帯にも属さない対象が抜け落ちるのに気づけるようにする
        [Test]
        public void ItHoldsEveryIdOfScene8()
        {
            var want = new System.Collections.Generic.List<string> { DriveIds.Door, DriveIds.Button };
            for (var i = 0; i < DriveIds.Triggers.Count; i++)
            {
                want.Add(DriveIds.Triggers[i]);
                want.Add(DriveIds.Page(i));
            }
            want.Add(DriveIds.Radio);
            want.Add(DriveIds.Pocket);
            want.Add(DriveIds.Fuel);
            Assert.That(Load().Ids(), Is.EquivalentTo(want));
        }
    }
}
