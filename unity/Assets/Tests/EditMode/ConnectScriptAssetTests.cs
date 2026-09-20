using NUnit.Framework;
using UnityEditor;

namespace HalfAware.Tests
{
    /// <summary>場面 3 の文面のアセットが、シナリオ設計書 7.2 のとおりに入っているかを見る</summary>
    public class ConnectScriptAssetTests
    {
        const string Path = "Assets/Data/ConnectScript.asset";

        static RoomScript Load()
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            Assert.That(script, Is.Not.Null, Path + " が無い。HalfAware/Write the connect script を走らせる");
            return script;
        }

        [Test]
        public void TheScriptAssetIsThere()
        {
            Assert.That(Load().Ids(), Is.Not.Empty, Path + " が空");
        }

        [Test]
        public void HoldsEveryIdOfScene3()
        {
            Assert.That(Load().Ids(), Is.EquivalentTo(ConnectIds.Order));
        }

        // 二択を出した対象は「はい」を選ぶまで済んだことにならない。
        // 潜る以外が誤って二択を出すと、調べても段が進まなくなる
        [Test]
        public void OnlyTheDiveAsks()
        {
            var script = Load();
            var dive = script.Find(ConnectIds.Dive);
            Assert.That(dive.Asks, Is.True, ConnectIds.Dive + " は二択を出す");
            Assert.That(dive.choice.question, Is.EqualTo("潜る"));

            foreach (var id in script.Ids())
            {
                if (id == ConnectIds.Dive) continue;
                Assert.That(script.Find(id).Asks, Is.False, id + " が二択を出そうとしている");
            }
        }

        // ルビは青空文庫の記法のまま持つ。書式の指定を文面へ書くと、
        // 字幕の折り返しがタグを字数に数えて行が崩れる
        [Test]
        public void TheLondonLineKeepsItsRubyAsPlainText()
        {
            var script = Load();
            var line = script.Find(ConnectIds.List).Lines[3];
            StringAssert.Contains(Ruby.Head.ToString(), line);

            int baseFrom, baseTo, rubyFrom, rubyTo;
            var at = line.IndexOf(Ruby.Head);
            Assert.That(Ruby.Group(line, at, out baseFrom, out baseTo, out rubyFrom, out rubyTo), Is.True,
                "ルビの指定が「｜親字《るび》」の形になっていない");
            Assert.That(line.Substring(baseFrom, baseTo - baseFrom), Is.EqualTo("倫敦"));
            Assert.That(line.Substring(rubyFrom, rubyTo - rubyFrom), Is.EqualTo("ロンドン"));

            foreach (var id in script.Ids())
                foreach (var page in script.Find(id).Lines)
                    Assert.That(page, Does.Not.Contain("<"), id + " の文面に書式の指定が入っている");
        }

        // 枚数を文字で持つと、露店の売れ行きを直したときにここだけ取り残される
        [Test]
        public void TheNoteEndsWithWhatSheSoldToday()
        {
            var sold = MarketSale.Chips - MarketSale.Left(MarketSale.Count);
            var lines = Load().Find(ConnectIds.Note).Lines;
            Assert.That(lines.Count, Is.GreaterThan(0), ConnectIds.Note + " に文が無い");
            StringAssert.EndsWith("2166/08/15　" + sold + "枚", lines[lines.Count - 1]);
        }

        // 1 ページに入りきらない並びは、字幕の窓が伸びきって下が切れる
        [Test]
        public void EveryPageFitsInOneWindow()
        {
            var script = Load();
            foreach (var id in script.Ids())
            {
                var entry = script.Find(id);
                foreach (var page in entry.Lines)
                    Assert.That(SubtitleBox.LineCount(page), Is.LessThanOrEqualTo(SubtitleBox.MaxRows), id);
                foreach (var page in entry.choice.AfterYes)
                    Assert.That(SubtitleBox.LineCount(page), Is.LessThanOrEqualTo(SubtitleBox.MaxRows), id);
            }
        }
    }
}
