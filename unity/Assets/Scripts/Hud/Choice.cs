using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// はい / いいえ の二択。文を読み終えた後に出し、「はい」で先へ進める。
    /// 「いいえ」なら何も起きず、対象はもう一度選べるまま残る
    /// </summary>
    public sealed class Choice
    {
        public const string Yes = "はい";
        public const string No = "いいえ";

        /// <summary>選んでいる方に付ける印</summary>
        public const string Cursor = "▶ ";

        public string Question { get; private set; }

        /// <summary>0 が「はい」、1 が「いいえ」</summary>
        public int Index { get; private set; }

        public bool Accepted { get { return Index == 0; } }

        public Choice(string question)
        {
            Question = question ?? "";
            Index = 0;
        }

        /// <summary>左右の入力。-1 で「はい」へ、+1 で「いいえ」へ。両端で止まる</summary>
        public void Move(int step)
        {
            Index = Mathf.Clamp(Index + step, 0, 1);
        }

        /// <summary>画面に出す文字列。問いの下に二択を並べる</summary>
        public string Compose()
        {
            return Compose(Question, Index);
        }

        public static string Compose(string question, int index)
        {
            var yes = (index == 0 ? Cursor : "　　") + Yes;
            var no = (index == 1 ? Cursor : "　　") + No;
            var head = string.IsNullOrEmpty(question) ? "" : question + "\n";
            return head + yes + "　　" + no;
        }
    }
}
