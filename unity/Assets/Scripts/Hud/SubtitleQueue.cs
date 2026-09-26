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

        /// <summary>
        /// これまでに積んだ行の数。空にしても減らない。
        /// 調べた物の文を積んだ直後にこれを覚えておくと、<see cref="Passed"/> がそこへ届いたときに、その文を読み終えたと分かる
        /// </summary>
        public int Queued { get; private set; }

        /// <summary>これまでに送り終えた行の数。空にしたら積んだぶん全部を送り終えたことにする</summary>
        public int Passed { get; private set; }

        /// <summary>待っている行の後ろに積む。空なら何も変わらない</summary>
        public void Enqueue(IEnumerable<string> newLines)
        {
            var before = lines.Count;
            lines.AddRange(newLines);
            Queued += lines.Count - before;
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
            Passed++;
        }

        public void Clear()
        {
            lines.Clear();
            index = 0;
            Passed = Queued;
        }
    }
}
