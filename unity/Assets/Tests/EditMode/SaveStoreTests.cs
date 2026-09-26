using System;
using NUnit.Framework;

namespace HalfAware.Tests
{
    /// <summary>セーブの読み書き・いちばん新しいセーブ・クリアの印・場面をまたぐ状態（設計書 5 節）</summary>
    public class SaveStoreTests
    {
        MemoryBox box;

        [SetUp]
        public void Swap()
        {
            box = new MemoryBox();
            SaveStore.Box = box;
            DiveHandoff.Clear();
        }

        [TearDown]
        public void Restore()
        {
            SaveStore.Box = null;
            DiveHandoff.Clear();
        }

        static SaveData Head(int stage, string scene, string hour = "")
        {
            return new SaveData { stage = stage, scene = scene, hour = hour };
        }

        static DateTime At(int minute)
        {
            return new DateTime(2026, 9, 27, 5, minute, 0, DateTimeKind.Utc);
        }

        [Test]
        public void WhatIsWrittenReadsBack()
        {
            var d = Head(9, "Village", StageMap.Morning);
            d.diveHops = 4;
            d.fromDive = true;
            SaveStore.Write(SaveSlot.Second, d, At(3));
            var back = SaveStore.Read(SaveSlot.Second);
            Assert.IsNotNull(back);
            Assert.AreEqual(9, back.stage);
            Assert.AreEqual("Village", back.scene);
            Assert.AreEqual(StageMap.Morning, back.hour);
            Assert.AreEqual(4, back.diveHops);
            Assert.IsTrue(back.fromDive);
            Assert.AreEqual(At(3).Ticks, back.writtenTicks);
            StringAssert.IsMatch(@"^\d{4}/\d{2}/\d{2} \d{2}:\d{2}$", back.written);
            Assert.AreEqual(SaveData.CurrentVersion, back.version);
            // 渡した物には日時を書き込まない
            Assert.AreEqual(0, d.writtenTicks);
        }

        [Test]
        public void WritingFlushesAndSlotsDoNotMix()
        {
            SaveStore.Write(SaveSlot.Auto, Head(1, "Room"), At(1));
            Assert.AreEqual(1, box.Flushes);
            SaveStore.Write(SaveSlot.First, Head(2, "Alley"), At(2));
            Assert.AreEqual(2, box.Flushes);
            Assert.AreEqual(1, SaveStore.Read(SaveSlot.Auto).stage);
            Assert.AreEqual(2, SaveStore.Read(SaveSlot.First).stage);
            Assert.IsNull(SaveStore.Read(SaveSlot.Second));
            Assert.IsNull(SaveStore.Read(SaveSlot.Third));
            Assert.AreNotEqual(SaveStore.KeyOf(SaveSlot.Auto), SaveStore.KeyOf(SaveSlot.First));
        }

        [Test]
        public void BrokenOrUnknownSavesReadAsEmpty()
        {
            box.Set(SaveStore.KeyOf(SaveSlot.First), "{ not json");
            Assert.IsNull(SaveStore.Read(SaveSlot.First));
            box.Set(SaveStore.KeyOf(SaveSlot.Second), "{\"stage\":0,\"scene\":\"Room\"}");
            Assert.IsNull(SaveStore.Read(SaveSlot.Second));
            box.Set(SaveStore.KeyOf(SaveSlot.Third), "{\"stage\":3,\"scene\":\"\"}");
            Assert.IsNull(SaveStore.Read(SaveSlot.Third));
            Assert.IsFalse(SaveStore.Any());
            Assert.IsNull(SaveStore.Newest());
        }

        [Test]
        public void ForgettingASlotEmptiesOnlyThatSlot()
        {
            SaveStore.Write(SaveSlot.First, Head(2, "Alley"), At(1));
            SaveStore.Write(SaveSlot.Third, Head(3, "Connect"), At(2));
            SaveStore.Forget(SaveSlot.First);
            Assert.IsNull(SaveStore.Read(SaveSlot.First));
            Assert.IsNotNull(SaveStore.Read(SaveSlot.Third));
        }

        [Test]
        public void SlotsHaveTheirNames()
        {
            Assert.AreEqual("自動", SaveStore.NameOf(SaveSlot.Auto));
            Assert.AreEqual("1", SaveStore.NameOf(SaveSlot.First));
            Assert.AreEqual("3", SaveStore.NameOf(SaveSlot.Third));
            CollectionAssert.AreEqual(new[] { SaveSlot.Auto, SaveSlot.First, SaveSlot.Second, SaveSlot.Third }, SaveStore.All);
            CollectionAssert.AreEqual(new[] { SaveSlot.First, SaveSlot.Second, SaveSlot.Third }, SaveStore.Manual);
        }

        // ---- いちばん新しいセーブ ------------------------------------------------

        [Test]
        public void TheNewestIsTheLastWrittenOfAutoAndManual()
        {
            Assert.IsNull(SaveStore.Newest());
            SaveStore.Write(SaveSlot.Auto, Head(5, "Rest"), At(10));
            SaveStore.Write(SaveSlot.First, Head(2, "Alley"), At(20));
            SaveStore.Write(SaveSlot.Third, Head(4, "Dive"), At(15));
            Assert.AreEqual(2, SaveStore.Newest().stage);
            // 自動が後から書かれれば自動
            SaveStore.Write(SaveSlot.Auto, Head(8, "Drive"), At(30));
            Assert.AreEqual(8, SaveStore.Newest().stage);
            Assert.IsTrue(SaveStore.Any());
        }

        [Test]
        public void OnATieTheAutoSaveWins()
        {
            SaveStore.Write(SaveSlot.Second, Head(2, "Alley"), At(5));
            SaveStore.Write(SaveSlot.Auto, Head(3, "Connect"), At(5));
            Assert.AreEqual(3, SaveStore.Newest().stage);
        }

        // ---- クリアの印 --------------------------------------------------------

        [Test]
        public void TheClearMarkIsKeptApartFromTheSaves()
        {
            Assert.IsFalse(SaveStore.Cleared);
            SaveStore.MarkCleared();
            Assert.IsTrue(SaveStore.Cleared);
            // もう一度付けても変わらない
            SaveStore.MarkCleared();
            Assert.IsTrue(SaveStore.Cleared);
            // セーブを書いても消しても、印は残る
            SaveStore.Write(SaveSlot.Auto, Head(1, "Room"), At(1));
            foreach (var slot in SaveStore.All) SaveStore.Forget(slot);
            Assert.IsTrue(SaveStore.Cleared);
            foreach (var slot in SaveStore.All) Assert.AreNotEqual(SaveStore.ClearedKey, SaveStore.KeyOf(slot));
            // 印はセーブとして読めない
            Assert.IsNull(SaveStore.Newest());
        }

        [Test]
        public void TheClearMarkComesOffOnlyByHand()
        {
            SaveStore.MarkCleared();
            SaveStore.ForgetCleared();
            Assert.IsFalse(SaveStore.Cleared);
        }

        // ---- 場面をまたぐ状態 ----------------------------------------------------

        [Test]
        public void TheHandoffFromTheDiveIsCapturedAndRestored()
        {
            DiveHandoff.Hops = 6;
            DiveHandoff.FromDive = true;
            var head = SaveStore.Capture(5, "Rest", null);
            Assert.AreEqual(5, head.stage);
            Assert.AreEqual("Rest", head.scene);
            Assert.AreEqual(string.Empty, head.hour);
            Assert.AreEqual(6, head.diveHops);
            Assert.IsTrue(head.fromDive);
            SaveStore.Write(SaveSlot.First, head, At(1));
            DiveHandoff.Clear();
            SaveStore.Restore(SaveStore.Read(SaveSlot.First));
            Assert.AreEqual(6, DiveHandoff.Hops);
            Assert.IsTrue(DiveHandoff.FromDive);
        }

        [Test]
        public void TheLogIsNotSaved()
        {
            var json = UnityEngine.JsonUtility.ToJson(new SaveData());
            StringAssert.DoesNotContain("log", json.ToLowerInvariant());
        }
    }
}
