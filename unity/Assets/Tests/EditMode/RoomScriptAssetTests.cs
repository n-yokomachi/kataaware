using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 場面 1 の文面のアセットが、台詞の原稿（docs/scenario/01-room.md）のとおりに入っているかを見る。
    /// 原稿を直して写し忘れていたら（HalfAware/Apply the scenario (room)）、ここで落ちる
    /// </summary>
    public class RoomScriptAssetTests
    {
        const string Path = "Assets/Data/RoomScript.asset";

        static RoomScript Load()
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            Assert.That(script, Is.Not.Null, Path + " が無い");
            return script;
        }

        static RoomManuscript.Text Manuscript()
        {
            var file = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", RoomManuscript.Path));
            return RoomManuscript.Read(File.ReadAllText(file, Encoding.UTF8));
        }

        [Test]
        public void HoldsEveryIdOfScene1()
        {
            Assert.That(Load().Ids(), Is.EqualTo(RoomIds.All), "RoomIds.All の順");
        }

        [Test]
        public void MatchesTheManuscript()
        {
            var script = Load();
            var text = Manuscript();
            foreach (var id in RoomIds.All)
            {
                var want = text.Find(id);
                var have = script.Find(id);
                Assert.AreEqual(want.label, have.label, id + " の対象の名前");
                Assert.AreEqual(want.lines, have.Lines, id + " のページ（写し忘れていないか）");
                Assert.AreEqual(want.choice.question, have.choice.question, id + " の二択");
                Assert.AreEqual(want.choice.afterYes, have.choice.AfterYes, id + " の「はい」の後");
            }
        }

        // 見た目や進行が変わる 3 つだけ二択を出す
        [TestCase("chips")]
        [TestCase("terminal")]
        [TestCase("door")]
        public void AsksBeforeItChangesAnything(string id)
        {
            Assert.That(Load().Find(id).Asks, Is.True, id + " は二択を出す");
        }

        [TestCase("jack")]
        [TestCase("cigarette")]
        [TestCase("jacket")]
        [TestCase("ashtray")]
        [TestCase("cigarette-box")]
        [TestCase("clipboard")]
        public void EverythingElseJustSpeaks(string id)
        {
            Assert.That(Load().Find(id).Asks, Is.False, id + " は二択を出さない");
        }

        // 長い並びは 1 ページにまとめて出す
        [TestCase("chips", 6)]
        public void ShowsTheLongListInOnePage(string id, int rows)
        {
            var entry = Load().Find(id);
            var longest = 0;
            foreach (var page in entry.Lines) longest = System.Math.Max(longest, SubtitleBox.LineCount(page));
            Assert.That(longest, Is.EqualTo(rows));
        }

        [Test]
        public void GivesEveryEntryItsOwnLabel()
        {
            var script = Load();
            foreach (var id in script.Ids())
            {
                Assert.That(script.Find(id).label, Is.Not.Empty, id);
            }
        }

        [Test]
        public void TheJacketHasNoPagesOfItsOwn()
        {
            // 着た後の独白は着る音の後に RoomIntroDirector が言う
            Assert.That(Load().Find(RoomIds.Jacket).Lines, Is.Empty);
        }

        /// <summary>
        /// 必須が残っている間、ドアは何も言わない（オーナー、2026-09-28「必須インタラクトのものが残っている場合、ドアのインタラクトを表示しないこと」）。
        /// 前提の文が無いので、ドアを狙っても印が出ず、調べられない（InteractionPicker.Select）
        /// </summary>
        [Test]
        public void TheDoorSaysNothingWhileSomethingIsLeft()
        {
            var door = Load().Find("door");
            foreach (var id in new[] { RoomIds.Jacket, RoomIds.Chips, RoomIds.Terminal, RoomIds.Jack })
                Assert.That(door.HintFor(id), Is.Null, id);
        }
    }
}
