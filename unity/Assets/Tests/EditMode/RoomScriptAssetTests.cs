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

        // 煙草だけ 0 行。吸い終わりの独白は RoomIntroDirector が言う
        [TestCase("jack", 1)]
        [TestCase("cigarette", 0)]
        [TestCase("chips", 7)]
        [TestCase("terminal", 11)]
        [TestCase("door", 1)]
        [TestCase("ashtray", 1)]
        [TestCase("cigarette-box", 1)]
        [TestCase("clipboard", 2)]
        public void KeepsTheLineCountOfTheScenario(string id, int lines)
        {
            var entry = Load().Find(id);
            Assert.That(entry.id, Is.EqualTo(id));
            Assert.That(entry.Lines.Count, Is.EqualTo(lines));
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
