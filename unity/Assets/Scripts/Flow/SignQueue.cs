using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 通りの看板。どれから読んでも、決めた順に流れる。
    /// 読むたびに次の段へ進み、最後まで行ったら最後の段をもう一度出す。
    /// 黙られると壊れて見えるので、空を返して終わりにはしない
    /// </summary>
    public sealed class SignQueue
    {
        static readonly string[] Nothing = new string[0];

        readonly IReadOnlyList<IReadOnlyList<string>> pages;
        int at;

        public SignQueue(IReadOnlyList<IReadOnlyList<string>> pages)
        {
            this.pages = pages;
        }

        /// <summary>次に出す段の番号。0 から数える</summary>
        public int At { get { return at; } }

        /// <summary>段の数</summary>
        public int Count { get { return pages == null ? 0 : pages.Count; } }

        /// <summary>最後まで読んだか</summary>
        public bool Read { get { return at >= Count; } }

        /// <summary>
        /// 次の段。最後まで読んだあとは、最後の段を返し続ける。
        /// 段がひとつも無ければ空
        /// </summary>
        public IReadOnlyList<string> Next()
        {
            if (Count == 0) return Nothing;
            if (at >= Count) return pages[Count - 1] ?? Nothing;
            var page = pages[at] ?? Nothing;
            at++;
            return page;
        }

        public void Rewind()
        {
            at = 0;
        }
    }
}
