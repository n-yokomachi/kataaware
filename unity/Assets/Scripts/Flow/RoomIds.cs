namespace HalfAware
{
    /// <summary>
    /// 場面 1（自室・導入）の調べる対象。
    /// シーンと文面のアセットと演出で同じ綴りを使うので、綴りはここ 1 箇所にまとめる
    /// </summary>
    public static class RoomIds
    {
        /// <summary>右の手首のインプラントジャック。抜くと眩暈が薄れていく</summary>
        public const string Jack = "jack";
        /// <summary>煙草。取ると吸い終わるまで自動で進む</summary>
        public const string Cigarette = "cigarette";
        /// <summary>左の肘掛けのジャケット。座ったまま着る。着ると立ち上がれる</summary>
        public const string Jacket = "jacket";
        /// <summary>抜き差し台のメモリ（チップ）</summary>
        public const string Chips = "chips";
        /// <summary>端末（モニター）。映り込みと走査条件</summary>
        public const string Terminal = "terminal";
        /// <summary>ドア。部屋を出る</summary>
        public const string Door = "door";
        /// <summary>灰皿（任意）</summary>
        public const string Ashtray = "ashtray";
        /// <summary>煙草の箱（任意、吸った後）</summary>
        public const string CigaretteBox = "cigarette-box";
        /// <summary>売り上げのメモ（任意）</summary>
        public const string Clipboard = "clipboard";

        /// <summary>これを調べると立ち上がって歩けるようになる</summary>
        public const string StandAfter = Jacket;

        /// <summary>文面のアセットに入っている id</summary>
        public static readonly string[] All = { Jack, Cigarette, Jacket, Chips, Terminal, Door, Ashtray, CigaretteBox, Clipboard };
    }
}
