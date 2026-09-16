using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>字幕の待ち行列。行を積み、E かクリックで 1 行ずつ送る。最後の行を送ると空になる</summary>
    public sealed class SubtitleQueue
    {
        readonly List<string> lines = new List<string>();
        int index;

        /// <summary>今出している行。無ければ null</summary>
        public string Current => index < lines.Count ? lines[index] : null;

        public bool IsTalking => Current != null;

        /// <summary>待っている行の後ろに積む。空なら何も変わらない</summary>
        public void Enqueue(IEnumerable<string> newLines)
        {
            lines.AddRange(newLines);
        }

        /// <summary>次の行へ。最後の行だったら空にする</summary>
        public void Advance()
        {
            if (index + 1 >= lines.Count)
            {
                Clear();
                return;
            }
            index++;
        }

        public void Clear()
        {
            lines.Clear();
            index = 0;
        }
    }
}
