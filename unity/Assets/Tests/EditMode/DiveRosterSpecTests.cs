using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 場面 4 の会話が、設計書 7 節と一字一句合っているかを見る。
    /// **設計書が正で、コードはその写し。** 7 節を読んで、記憶ごとに一行目・行・相手の替わる所・〔区切り〕を比べる。
    ///
    /// 相手の名前は、設計書は人の名前（ハンナ）、一覧は記憶に置く人の名前（Mother）で書くので、
    /// 字では比べない。記憶ごとに「同じ人はいつも同じ名前、違う人は違う名前」になっているかを見る
    /// </summary>
    public class DiveRosterSpecTests
    {
        const string RosterPath = "Assets/Data/DiveRoster.asset";
        const string SpecPath = "../docs/superpowers/specs/2026-09-21-dive-scene-design.md";

        /// <summary>設計書 7 節の記憶一本</summary>
        sealed class Written
        {
            public string title;
            public string first;
            public readonly List<string> lines = new List<string>();
            /// <summary>行ごとの会話の番号（0 始まり）</summary>
            public readonly List<int> group = new List<int>();
            /// <summary>会話ごとの相手（設計書の人の名前）</summary>
            public readonly List<string> partner = new List<string>();
            /// <summary>会話ごとに、〔区切り〕の後か</summary>
            public readonly List<bool> cut = new List<bool>();
        }

        static List<Written> Read()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SpecPath));
            Assert.That(File.Exists(path), Is.True, "設計書が無い: " + path);
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            var head = text.IndexOf("\n## 7. ");
            var tail = text.IndexOf("\n## 8. ");
            Assert.That(head, Is.GreaterThan(0), "設計書に 7 節が無い");
            Assert.That(tail, Is.GreaterThan(head), "設計書に 8 節が無い");
            var section = text.Substring(head, tail - head);

            var all = new List<Written>();
            Written now = null;
            var pending = false;
            foreach (var raw in section.Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.StartsWith("### "))
                {
                    now = new Written { title = line.Substring(4) };
                    all.Add(now);
                    pending = false;
                    continue;
                }
                if (now == null) continue;
                if (line.StartsWith("一行目: ")) { now.first = line.Substring("一行目: ".Length); continue; }
                if (line.StartsWith("**相手: "))
                {
                    var end = line.IndexOf("**", 2);
                    now.partner.Add(line.Substring("**相手: ".Length, end - "**相手: ".Length));
                    now.cut.Add(pending);
                    pending = false;
                    continue;
                }
                if (line.StartsWith("〔区切り〕")) { pending = true; continue; }
                if (line.StartsWith("- ") && now.partner.Count > 0)
                {
                    now.lines.Add(line.Substring(2));
                    now.group.Add(now.partner.Count - 1);
                }
            }
            return all;
        }

        static DiveRoster Load()
        {
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(RosterPath);
            Assert.That(roster, Is.Not.Null, RosterPath + " が無い。HalfAware/Write the dive roster を走らせる");
            return roster;
        }

        [Test]
        public void TheSpecHasSixteenMemories()
        {
            var written = Read();
            Assert.That(written.Count, Is.EqualTo(16));
            foreach (var w in written)
            {
                Assert.That(w.first, Is.Not.Null.And.Not.Empty, w.title + " に一行目が無い");
                Assert.That(w.lines, Is.Not.Empty, w.title + " に会話が無い");
            }
        }

        // 一行目と、二行目からの行が、設計書 7 節と一字一句同じ（全角の空白・句読点・記号も）
        [Test]
        public void EveryLineIsCopiedWordForWord()
        {
            var written = Read();
            var roster = Load();
            Assert.That(roster.Count, Is.EqualTo(written.Count));
            for (var i = 0; i < written.Count; i++)
            {
                var w = written[i];
                var said = roster[i].said;
                Assert.That(said[0].line, Is.EqualTo(w.first), (i + 1) + ". の一行目");
                Assert.That(said.Length - 1, Is.EqualTo(w.lines.Count), (i + 1) + ". の行数");
                for (var k = 0; k < w.lines.Count; k++)
                    Assert.That(said[k + 1].line, Is.EqualTo(w.lines[k]), (i + 1) + ". の " + (k + 2) + " 行目");
            }
        }

        // 会話の分かれ目（相手の替わる所と〔区切り〕）が設計書と同じ。どの行も相手を持つ
        [Test]
        public void TalksSplitWhereTheSpecSplitsThem()
        {
            var written = Read();
            var roster = Load();
            for (var i = 0; i < written.Count; i++)
            {
                var w = written[i];
                var said = roster[i].said;
                for (var k = 1; k < said.Length; k++)
                    Assert.That(said[k].Partnered, Is.True, (i + 1) + ". の " + (k + 1) + " 行目に相手が無い");
                var talks = DiveEntry.Exchanges(said);
                Assert.That(talks.Length, Is.EqualTo(w.partner.Count), (i + 1) + ". の会話の数");
                for (var g = 0; g < talks.Length; g++)
                {
                    var lines = new List<int>();
                    for (var k = 0; k < w.group.Count; k++) if (w.group[k] == g) lines.Add(k + 1);
                    Assert.That(talks[g].lines, Is.EqualTo(lines.ToArray()), (i + 1) + ". の " + (g + 1) + " 番目の会話の行");
                    Assert.That(talks[g].Cut, Is.EqualTo(w.cut[g]), (i + 1) + ". の " + (g + 1) + " 番目の会話の〔区切り〕");
                }
            }
        }

        // 同じ人はいつも同じ名前、違う人は違う名前で相手を持つ
        [Test]
        public void EachPartnerKeepsOneName()
        {
            var written = Read();
            var roster = Load();
            for (var i = 0; i < written.Count; i++)
            {
                var w = written[i];
                var talks = DiveEntry.Exchanges(roster[i].said);
                var there = new Dictionary<string, string>();
                var back = new Dictionary<string, string>();
                for (var g = 0; g < talks.Length && g < w.partner.Count; g++)
                {
                    var spec = w.partner[g].Split('（')[0];
                    var code = talks[g].partner;
                    string was;
                    if (there.TryGetValue(spec, out was))
                        Assert.That(code, Is.EqualTo(was), (i + 1) + ". の " + spec + " の名前が揺れている");
                    else there[spec] = code;
                    if (back.TryGetValue(code, out was))
                        Assert.That(spec, Is.EqualTo(was), (i + 1) + ". の " + code + " に二人が当たっている");
                    else back[code] = spec;
                }
            }
        }
    }
}
