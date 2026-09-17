using System.Collections.Generic;

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

        /// <summary>新しいものが下に来る形で 1 つの文字列にする。最大 max 行</summary>
        public string Compose(int max)
        {
            if (lines.Count == 0) return string.Empty;
            var from = max > 0 && lines.Count > max ? lines.Count - max : 0;
            return string.Join("\n", lines.GetRange(from, lines.Count - from).ToArray());
        }

        public void Clear()
        {
            lines.Clear();
        }
    }
}
