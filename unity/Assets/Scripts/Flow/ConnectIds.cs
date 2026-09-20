namespace HalfAware
{
    /// <summary>
    /// 場面 3(自室・接続)の調べる対象。
    /// シーンと文面のアセットで同じ綴りを使うので、綴りはここ 1 箇所にまとめる
    /// </summary>
    public static class ConnectIds
    {
        /// <summary>ソファの売上メモ</summary>
        public const string Note = "note";
        /// <summary>椅子。調べると座る</summary>
        public const string Chair = "chair";
        /// <summary>肘掛けのジャック。調べると手首へ挿す</summary>
        public const string Jack = "jack";
        /// <summary>モニター。走査条件と候補が並ぶ</summary>
        public const string Monitor = "monitor";
        /// <summary>リストを送る。独白が続く</summary>
        public const string List = "list";
        /// <summary>潜る。二択で確かめて場面を閉じる</summary>
        public const string Dive = "dive";

        /// <summary>調べる順。組み立てはこの順に after を張る</summary>
        public static readonly string[] Order = { Note, Chair, Jack, Monitor, List, Dive };
    }
}
