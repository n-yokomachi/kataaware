using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 字幕のウインドウの大きさ。1 ページに何行入っているかで高さを決め、
    /// 入りきらないぶんは字を小さくして収める。既定は 2 行
    /// </summary>
    public static class SubtitleBox
    {
        /// <summary>何も入っていなくてもこの行数ぶんの高さを取る</summary>
        public const int BaseRows = 2;

        /// <summary>これを超えたら字を小さくする</summary>
        public const int ShrinkOver = 4;

        /// <summary>小さくする限度。これ以上は縮めない</summary>
        public const float MinScale = 0.70f;

        /// <summary>この行数を超えたら、それ以上はウインドウを伸ばさない</summary>
        public const int MaxRows = 8;

        /// <summary>改行で区切った行数。空なら 0</summary>
        public static int LineCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var n = 1;
            for (var i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
            return n;
        }

        /// <summary>ウインドウが取る行数。既定を下回らず、上限も超えない</summary>
        public static int Rows(string text)
        {
            return Mathf.Clamp(LineCount(text), BaseRows, MaxRows);
        }

        /// <summary>字の大きさの倍率。行数が増えるほど小さくする</summary>
        public static float FontScale(string text)
        {
            var lines = LineCount(text);
            if (lines <= ShrinkOver) return 1f;
            return Mathf.Max(MinScale, (float)ShrinkOver / lines);
        }
    }
}
