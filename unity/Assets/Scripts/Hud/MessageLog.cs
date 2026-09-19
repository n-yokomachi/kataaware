using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 出した文のログ。読み飛ばしても後から追えるように、古い順に溜めておく。
    /// 同じ文が続けて入ることはあるが、それも起きたとおりに残す
    /// </summary>
    public sealed class MessageLog
    {
        /// <summary>これより多くは持たない。古い方から捨てる</summary>
        public const int Keep = 80;

        readonly List<string> lines = new List<string>();

        public IReadOnlyList<string> Lines { get { return lines; } }

        public int Count { get { return lines.Count; } }

        public bool IsEmpty { get { return lines.Count == 0; } }

        /// <summary>1 行をログに残す。空の行は入れない</summary>
        public void Add(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            lines.Add(line);
            if (lines.Count > Keep) lines.RemoveRange(0, lines.Count - Keep);
        }

        /// <summary>まとめてログに残す</summary>
        public void AddRange(IEnumerable<string> newLines)
        {
            if (newLines == null) return;
            foreach (var line in newLines) Add(line);
        }

        /// <summary>1 頁に出す文の数</summary>
        public const int Page = 8;

        /// <summary>
        /// 新しいものから並べて 1 つの文字列にする。文と文のあいだは 1 行空ける。
        ///
        /// back は新しい方からいくつ飛ばすか。さかのぼるのに使う。
        /// 範囲を外れたら、あるところまでで止める
        /// </summary>
        public string Compose(int max, int back)
        {
            if (lines.Count == 0) return string.Empty;
            if (max <= 0) max = lines.Count;
            back = Mathf.Clamp(back, 0, Mathf.Max(0, lines.Count - 1));
            var newest = lines.Count - 1 - back;
            var made = new System.Text.StringBuilder();
            for (var i = newest; i >= 0 && newest - i < max; i--)
            {
                if (made.Length > 0) made.Append("\n\n");
                made.Append(lines[i]);
            }
            return made.ToString();
        }

        /// <summary>さかのぼれる残りの文の数</summary>
        public int Older(int max, int back)
        {
            if (max <= 0) return 0;
            return Mathf.Max(0, lines.Count - back - max);
        }

        public void Clear()
        {
            lines.Clear();
        }
    }
}
