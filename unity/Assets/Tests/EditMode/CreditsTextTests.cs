using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>終わりのクレジットの本文の読み方（CreditsText）と、場面へ写した本文が md と揃っているか</summary>
    public class CreditsTextTests
    {
        const string Sample =
            "# 見出し\n\n- 説明\n\n---\n\n## HALF AWARE\nかたあはれ\n\n## Created by\nyoko\n\n## Music\n\"HALF AWARE\"\nGenerated with Suno\n\n\n## \nMade with Unity\n\n---\n\n## 決めてほしいこと\n1. 何か\n";

        [Test]
        public void ReadsOnlyBetweenTheFences()
        {
            var body = CreditsText.Body(Sample);
            StringAssert.StartsWith("\n## HALF AWARE", body);
            StringAssert.DoesNotContain("決めてほしいこと", body);
            StringAssert.DoesNotContain("説明", body);
            Assert.AreEqual(string.Empty, CreditsText.Body("---\nひとつだけ\n"), "柵が一つなら読まない");
        }

        [Test]
        public void FirstHeadingIsTheTitleAndItsLineTheReading()
        {
            var rows = CreditsText.FromMarkdown(Sample);
            Assert.AreEqual(CreditsText.Kind.Title, rows[0].kind);
            Assert.AreEqual("HALF AWARE", rows[0].text);
            Assert.AreEqual(CreditsText.Kind.Reading, rows[1].kind);
            Assert.AreEqual("かたあはれ", rows[1].text);
            Assert.AreEqual(CreditsText.Kind.Gap, rows[2].kind);
            Assert.AreEqual(CreditsText.Kind.Heading, rows[3].kind);
            Assert.AreEqual("Created by", rows[3].text);
            Assert.AreEqual(CreditsText.Kind.Line, rows[4].kind);
        }

        [Test]
        public void BlankLinesCollapseAndEmptyHeadingKeepsItsSlot()
        {
            var rows = CreditsText.FromMarkdown(Sample);
            for (var i = 1; i < rows.Count; i++)
                Assert.IsFalse(rows[i].kind == CreditsText.Kind.Gap && rows[i - 1].kind == CreditsText.Kind.Gap, "空きが続いている: " + i);
            Assert.AreNotEqual(CreditsText.Kind.Gap, rows[0].kind);
            Assert.AreNotEqual(CreditsText.Kind.Gap, rows[rows.Count - 1].kind);
            var last = rows[rows.Count - 1];
            Assert.AreEqual("Made with Unity", last.text);
            Assert.AreEqual(CreditsText.Kind.Heading, rows[rows.Count - 2].kind, "「## 」だけの行は字の無い見出し");
            Assert.AreEqual(string.Empty, rows[rows.Count - 2].text);
        }

        [Test]
        public void QuotesAndMarksStayAsWritten()
        {
            var rows = CreditsText.FromMarkdown(Sample);
            Assert.IsTrue(rows.Exists(r => r.text == "\"HALF AWARE\"" && r.kind == CreditsText.Kind.Line));
        }

        /// <summary>
        /// 場面へ写した本文（Assets/Data/EndingCredits.txt）が docs/release/credits.md と揃っているか。
        /// md を直したのに組み直していなければ落ちる（HalfAware/Write the ending credits か Build the ending を走らせる）
        /// </summary>
        [Test]
        public void CopiedBodyMatchesTheMarkdown()
        {
            var md = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "release", "credits.md"));
            Assume.That(File.Exists(md), "md が無い: " + md);
            var copy = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/EndingCredits.txt");
            Assert.IsNotNull(copy, "本文の写しが無い。HalfAware/Build the ending を走らせる");
            var want = CreditsText.Body(File.ReadAllText(md, Encoding.UTF8)).Replace("\r\n", "\n");
            Assert.AreEqual(want, copy.text.Replace("\r\n", "\n"), "md を直した後に組み直していない（HalfAware/Write the ending credits）");
        }
    }
}
