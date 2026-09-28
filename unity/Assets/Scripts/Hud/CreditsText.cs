using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 終わりのクレジットの本文を読む。書式は <c>docs/release/credits.md</c> の「---」と「---」のあいだ
    /// （オーナーが添削するファイル。シナリオ設計 13 節）。
    /// <list type="bullet">
    /// <item><c>## 見出し</c> は見出し（しっぽり明朝）。<c>## </c> だけの行は字の無い見出し（ほかの節と同じ間合いで、字だけが無い）</item>
    /// <item>ほかの行は名前や素材の行（Noto Sans JP）</item>
    /// <item>空の行は区切り。続けて何行空けても一つ</item>
    /// <item>**いちばん初めの見出しは題**（HALF AWARE）。その下の空行までの行は題の読み（かたあはれ）。
    /// この二つだけは粗い画面に描かず、タイトルの画面と同じくくっきり出す</item>
    /// </list>
    /// 組み立て（<c>BuildEnding</c>）がこれで並べて場面へ焼く。md を直したら組み直す
    /// </summary>
    public static class CreditsText
    {
        public enum Kind
        {
            /// <summary>題（いちばん初めの見出し）</summary>
            Title,
            /// <summary>題の読み（題の下の行）</summary>
            Reading,
            /// <summary>見出し</summary>
            Heading,
            /// <summary>名前や素材の行</summary>
            Line,
            /// <summary>区切りの空き</summary>
            Gap,
        }

        public struct Row
        {
            public Kind kind;
            public string text;

            public Row(Kind kind, string text)
            {
                this.kind = kind;
                this.text = text;
            }

            public override string ToString() { return kind + ":" + text; }
        }

        /// <summary>本文を挟む柵の行</summary>
        public const string Fence = "---";

        /// <summary>見出しの頭</summary>
        public const string HeadingMark = "##";

        /// <summary>
        /// md の中の本文（初めの柵の行と、次の柵の行のあいだ）。柵が二つ揃っていなければ空。
        /// 柵の外（ファイルの頭の説明と、後ろの「決めてほしいこと」）は読まない
        /// </summary>
        public static string Body(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return string.Empty;
            var lines = Lines(markdown);
            var from = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != Fence) continue;
                if (from < 0) { from = i; continue; }
                return string.Join("\n", lines, from + 1, i - from - 1);
            }
            return string.Empty;
        }

        /// <summary>本文を並びに直す。頭と尻の区切りは落とす</summary>
        public static List<Row> Parse(string body)
        {
            var rows = new List<Row>();
            if (string.IsNullOrEmpty(body)) return rows;
            var titled = false;
            var inTitle = false;
            foreach (var raw in Lines(body))
            {
                var line = raw.TrimEnd();
                if (line.Trim().Length == 0)
                {
                    inTitle = false;
                    Gap(rows);
                    continue;
                }
                if (line.StartsWith(HeadingMark))
                {
                    var text = line.Substring(HeadingMark.Length).Trim();
                    inTitle = false;
                    // 字の無い見出し（「## 」だけの行）も見出しの一段として残す。ほかの節と同じ間合いで、字だけが無い
                    if (text.Length > 0 && !titled)
                    {
                        titled = true;
                        inTitle = true;
                        rows.Add(new Row(Kind.Title, text));
                        continue;
                    }
                    rows.Add(new Row(Kind.Heading, text));
                    continue;
                }
                rows.Add(new Row(inTitle ? Kind.Reading : Kind.Line, line.Trim()));
            }
            while (rows.Count > 0 && rows[rows.Count - 1].kind == Kind.Gap) rows.RemoveAt(rows.Count - 1);
            return rows;
        }

        /// <summary>md から本文を読んで並べる</summary>
        public static List<Row> FromMarkdown(string markdown)
        {
            return Parse(Body(markdown));
        }

        static void Gap(List<Row> rows)
        {
            if (rows.Count == 0 || rows[rows.Count - 1].kind == Kind.Gap) return;
            rows.Add(new Row(Kind.Gap, string.Empty));
        }

        static string[] Lines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }
    }
}
