using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 選ぶ物を横に並べた問い。文を読み終えた後に出し、既定の はい / いいえ では「はい」で先へ進める。
    /// 「いいえ」なら何も起きず、対象はもう一度選べるまま残る。
    ///
    /// 選ぶ物はいくつでも持てる（今の呼び手は二択だけ）。並びの左から 0, 1, 2…。
    /// 見せ方は <see cref="ChoiceView"/>、マウスの出入りは <see cref="ChoicePointer"/>
    /// </summary>
    public sealed class Choice
    {
        public const string Yes = "はい";
        public const string No = "いいえ";

        /// <summary>選んでいる方に付ける印。場面 4 の板と画面の下の案内が使う</summary>
        public const string Cursor = "▶ ";

        static readonly string[] YesNo = { Yes, No };

        readonly string[] options;
        /// <summary>前のフレームの左右の倒れ具合。倒した瞬間だけ動かすのに使う</summary>
        int held;

        public string Question { get; private set; }

        /// <summary>選ぶ物。左から並べる順</summary>
        public IReadOnlyList<string> Options { get { return options; } }

        public int Count { get { return options.Length; } }

        /// <summary>選んでいる物の番号。0 が左端（既定の二択では「はい」）</summary>
        public int Index { get; private set; }

        /// <summary>選んでいる物の文字</summary>
        public string Label { get { return options[Index]; } }

        /// <summary>左端（既定の二択では「はい」）を選んでいるか</summary>
        public bool Accepted { get { return Index == 0; } }

        /// <summary>はい / いいえ の二択</summary>
        public Choice(string question) : this(question, YesNo)
        {
        }

        /// <summary>選ぶ物を並べた問い。空なら はい / いいえ にする。左端を選んだ状態で始まる</summary>
        public Choice(string question, IReadOnlyList<string> options)
        {
            Question = question ?? "";
            if (options == null || options.Count == 0) options = YesNo;
            this.options = new string[options.Count];
            for (var i = 0; i < options.Count; i++) this.options[i] = options[i] ?? "";
            Index = 0;
        }

        /// <summary>左右の入力。-1 で左へ、+1 で右へ。両端で止まる</summary>
        public void Move(int step)
        {
            Index = Mathf.Clamp(Index + step, 0, options.Length - 1);
        }

        /// <summary>
        /// 左右の倒れ具合（<see cref="PlayerController.ChoiceStep"/>）を毎フレーム渡す。
        /// 倒した瞬間にだけ一つ動かす。倒したままでは流れない
        /// </summary>
        public void Tilt(int step)
        {
            if (step != 0 && step != held) Move(step);
            held = step;
        }

        /// <summary>マウスが札に入った。範囲の外なら何もしない</summary>
        public void Hover(int index)
        {
            if (index >= 0 && index < options.Length) Index = index;
        }
    }
}
