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
        /// <summary>玄関先のコートハンガー。調べるとジャケットを脱いで掛ける</summary>
        public const string Coat = "coat";
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

        /// <summary>調べる対象のすべて（おおよその順）。メモとコートハンガーはどちらが先でもよい</summary>
        public static readonly string[] Order = { Note, Coat, Chair, Jack, Monitor, List, Dive };

        static readonly string[] None = new string[0];

        /// <summary>
        /// 対象を選べるようになる前に済ませておく対象（組み立てがシーンの after に張る）。
        /// 椅子はメモとコートハンガーの後（ジャケットを掛けてから座る）。
        /// ジャックとモニターは after ではなく、座り終え・挿し終えたところで ConnectDirector が開く
        /// </summary>
        public static string[] After(string id)
        {
            switch (id)
            {
                case Chair: return new[] { Note, Coat };
                case List: return new[] { Monitor };
                case Dive: return new[] { List };
                default: return None;
            }
        }
    }
}
