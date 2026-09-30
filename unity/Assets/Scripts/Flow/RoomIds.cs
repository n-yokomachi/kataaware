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
        /// <summary>椅子の右の卓に置いたジャケット。座ったまま、モニターの後に着る。着ると立ち上がれる</summary>
        public const string Jacket = "jacket";
        /// <summary>抜き差し台のメモリ（チップ）</summary>
        public const string Chips = "chips";
        /// <summary>端末（モニター）。煙草の後に座ったまま調べる。映り込みと走査条件</summary>
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

        /// <summary>
        /// 調べる順（前提）。ジャック → 煙草 → モニター（座ったまま。煙草の煙が立ち続ける）→ ジャケット（着て立つ）→ 歩いて調べる物 → ドア。
        /// 灰皿と箱は煙草の後に座ったまま。どの前提にも文を持たないので、前提が済むまで印が出ない（<see cref="InteractionPicker"/>）。
        /// 2026-09-29 にモニターをジャケットの前に移した（オーナー「モニターへのインタラクトを必須にし、モニターへのインタラクトが終わったら
        /// 煙草の煙を止め、ジャケットへのインタラクトを有効化、という順にしよう。これによってモニター調べるためにもう一度座り直すっていうのをなくす」）。
        /// Room.unity の Interactable の after はこの表から書く（PlaceProtagonist.Jacket・PlaceRoomOrder）
        /// </summary>
        public static string[] After(string id)
        {
            switch (id)
            {
                case Cigarette: return new[] { Jack };
                case Terminal:
                case Ashtray:
                case CigaretteBox: return new[] { Cigarette };
                case Jacket: return new[] { Terminal };
                case Chips:
                case Clipboard: return new[] { Jacket };
                // ドアは前提の先頭に文を持たない id（ジャケット）を置いて、着るまで弾く
                case Door: return new[] { Jacket, Chips, Terminal };
                default: return new string[0];
            }
        }
    }
}
