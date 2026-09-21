namespace HalfAware
{
    /// <summary>
    /// 場面 4 の場所と、場面 3 のモニターの列。
    /// 列の並びは `WriteConnectScript` の monitor の三ページ目と同じ順で、
    /// 端末が次を選ぶときはこの順に辿る
    /// </summary>
    public static class DiveIds
    {
        public const string Estate = "estate";
        public const string Park = "park";
        public const string Train = "train";
        public const string Kitchen = "kitchen";
        public const string Classroom = "classroom";

        public static readonly string[] Places = { Estate, Park, Train, Kitchen, Classroom };

        /// <summary>列に載っている記憶の、一覧での番号（0 始まり）。メイ・ハンナ・アルベルト・エミリー・マーク・リー</summary>
        public static readonly int[] Listed = { 0, 1, 2, 4, 5, 7 };
    }
}
