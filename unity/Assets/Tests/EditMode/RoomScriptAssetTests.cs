using NUnit.Framework;
using UnityEditor;

namespace HalfAware.Tests
{
    /// <summary>場面 1 の文面のアセットが、シナリオ設計書 4 節のとおりに入っているかを見る</summary>
    public class RoomScriptAssetTests
    {
        const string Path = "Assets/Data/RoomScript.asset";

        static RoomScript Load()
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            Assert.That(script, Is.Not.Null, Path + " が無い");
            return script;
        }

        [Test]
        public void HoldsEveryIdOfScene1()
        {
            Assert.That(Load().Ids(), Is.EquivalentTo(new[]
            {
                "jack", "cigarette", "chips", "terminal", "door", "ashtray", "cigarette-box", "clipboard",
            }));
        }

        // 何ページに割るかは見た目の都合で変わるので、行の総数で見る。
        // 二択の後に出す文も、シナリオの一部なので数に入れる。
        // 煙草だけ 0 行。吸い終わりの独白は RoomIntroDirector が言う
        [TestCase("jack", 1)]
        [TestCase("cigarette", 0)]
        [TestCase("chips", 7)]
        [TestCase("terminal", 11)]
        [TestCase("door", 1)]
        [TestCase("ashtray", 1)]
        [TestCase("cigarette-box", 1)]
        [TestCase("clipboard", 4)]
        public void KeepsTheLineCountOfTheScenario(string id, int lines)
        {
            var entry = Load().Find(id);
            Assert.That(entry.id, Is.EqualTo(id));
            var count = 0;
            foreach (var page in entry.Lines) count += SubtitleBox.LineCount(page);
            foreach (var page in entry.choice.AfterYes) count += SubtitleBox.LineCount(page);
            Assert.That(count, Is.EqualTo(lines));
        }

        // 見た目や進行が変わる 3 つだけ二択を出す
        [TestCase("chips", "チップを抜く")]
        [TestCase("terminal", "スリープを解除する")]
        [TestCase("door", "部屋を出る")]
        public void AsksBeforeItChangesAnything(string id, string question)
        {
            var entry = Load().Find(id);
            Assert.That(entry.Asks, Is.True, id + " は二択を出す");
            Assert.That(entry.choice.question, Is.EqualTo(question));
        }

        [TestCase("jack")]
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
        public void TellsTheDoorWhichPrerequisiteIsMissing()
        {
            var door = Load().Find("door");
            Assert.That(door.HintFor("chips"), Is.Not.Null.And.Not.Empty);
            Assert.That(door.HintFor("terminal"), Is.Not.Null.And.Not.Empty);
            Assert.That(door.HintFor("jack"), Is.Null);
        }
    }
}
