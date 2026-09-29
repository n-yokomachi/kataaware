using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 場面 2 の文面のアセットが、台詞の原稿（docs/scenario/02-alley.md）のとおりに入っているかを見る。
    /// 原稿を直して写し忘れていたら（HalfAware/Apply the scenario (alley)）、ここで落ちる
    /// </summary>
    public class AlleyScriptAssetTests
    {
        const string Path = "Assets/Data/AlleyScript.asset";

        static RoomScript Load()
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            Assert.That(script, Is.Not.Null, Path + " が無い");
            return script;
        }

        static AlleyManuscript.Text Manuscript()
        {
            var file = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", AlleyManuscript.Path));
            return AlleyManuscript.Read(File.ReadAllText(file, Encoding.UTF8));
        }

        [Test]
        public void HoldsEveryIdOfScene2()
        {
            Assert.That(Load().Ids(), Is.EqualTo(AlleyIds.All), "AlleyIds.All の順");
        }

        [Test]
        public void MatchesTheManuscript()
        {
            var script = Load();
            var text = Manuscript();
            foreach (var id in AlleyIds.All)
            {
                var want = text.Find(id);
                var have = script.Find(id);
                Assert.AreEqual(want.label, have.label, id + " の対象の名前");
                Assert.AreEqual(want.lines, have.Lines, id + " のページ（写し忘れていないか）");
                Assert.AreEqual(want.choice.question, have.choice.question, id + " の二択");
            }
        }

        [Test]
        public void OnlyTheTableAsks()
        {
            foreach (var id in AlleyIds.All)
                Assert.AreEqual(id == AlleyIds.Table, Load().Find(id).Asks, id);
        }
    }
}
