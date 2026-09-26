namespace HalfAware
{
    /// <summary>
    /// タイトルの画面の背景。場面ごとの画角で前もって撮った絵（設計書 5 節の表）。
    /// 並びは <see cref="TitleScreen"/> の絵と音の並びと揃える
    /// </summary>
    public enum TitleBackdrop
    {
        /// <summary>自室（場面 1・3・5・7、セーブが無い時）。<c>room_1</c></summary>
        Room,
        /// <summary>路地裏（場面 2）。<c>alley_1</c></summary>
        Alley,
        /// <summary>潜る（場面 4）。記憶 0（メイ）の色味を掛けて撮る。<c>dive_3</c></summary>
        Dive,
        /// <summary>車内（場面 8）。ガレージ。<c>drive_1</c></summary>
        Drive,
        /// <summary>村の朝（場面 9・10、クリアした後）。<c>village_a1</c></summary>
        VillageMorning,
        /// <summary>村の夕方（場面 6）。<c>village_a1</c> の画角の夕方</summary>
        VillageEvening,
    }

    /// <summary>タイトルの画面の背景と、起動の表示の日時と場所を選ぶ</summary>
    public static class TitleBackdrops
    {
        public const int Count = 6;

        /// <summary>
        /// 背景を選ぶ。クリアの印があれば朝の村（いちばん新しいセーブより先に見る）。
        /// 無ければ、いちばん新しいセーブの場面。セーブが無ければ自室
        /// </summary>
        public static TitleBackdrop Pick(bool cleared, SaveData newest)
        {
            if (cleared) return TitleBackdrop.VillageMorning;
            return newest == null ? TitleBackdrop.Room : OfStage(newest.stage);
        }

        /// <summary>場面の番号の背景。村は場面 6 なら夕方、9・10 なら朝</summary>
        public static TitleBackdrop OfStage(int stage)
        {
            switch (stage)
            {
                case 2: return TitleBackdrop.Alley;
                case 4: return TitleBackdrop.Dive;
                case 6: return TitleBackdrop.VillageEvening;
                case 8: return TitleBackdrop.Drive;
                case 9:
                case 10: return TitleBackdrop.VillageMorning;
                default: return TitleBackdrop.Room;
            }
        }

        /// <summary>明るい背景か。沈め方を弱めて、時刻の感じを残す</summary>
        public static bool Bright(TitleBackdrop b)
        {
            return b == TitleBackdrop.VillageMorning;
        }

        /// <summary>絵のファイルの名（拡張子なし）。<c>Assets/Textures/Title/</c> に置く</summary>
        public static string FileName(TitleBackdrop b)
        {
            switch (b)
            {
                case TitleBackdrop.Alley: return "alley_1";
                case TitleBackdrop.Dive: return "dive_3";
                case TitleBackdrop.Drive: return "drive_1";
                case TitleBackdrop.VillageMorning: return "village_a1_morning";
                case TitleBackdrop.VillageEvening: return "village_a1_evening";
                default: return "room_1";
            }
        }

        /// <summary>撮る時に開くシーン</summary>
        public static string SceneOf(TitleBackdrop b)
        {
            switch (b)
            {
                case TitleBackdrop.Alley: return "Alley";
                case TitleBackdrop.Dive: return "Dive";
                case TitleBackdrop.Drive: return "Drive";
                case TitleBackdrop.VillageMorning:
                case TitleBackdrop.VillageEvening: return "Village";
                default: return "Room";
            }
        }

        /// <summary>
        /// 起動の表示の最後の行（日時と場所）。背景の場面の物を <see cref="ConsolePlace"/> から取る。
        /// 潜る（場面 4）は、背景に撮った記憶の日時と場所（divingLine、組み立てで記憶の一覧から拾う）
        /// </summary>
        public static string BootPlace(bool cleared, SaveData newest, string divingLine)
        {
            if (cleared) return ConsolePlace.For("Village");
            if (newest == null) return ConsolePlace.For("Room");
            if (newest.stage == 4 && !string.IsNullOrEmpty(divingLine)) return divingLine;
            return ConsolePlace.ForStage(newest.stage);
        }
    }
}
