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
        /// <summary>潜る（場面 4）。記憶 0（メイ）の団地の外観。記憶の色味を掛けて撮る。<c>dive_estate</c></summary>
        Dive,
        /// <summary>車内（場面 8）。ガレージ。<c>drive_1</c></summary>
        Drive,
        /// <summary>村の朝（場面 9・10、クリアした後）。<c>village_a1</c></summary>
        VillageMorning,
        /// <summary>村の夕方（場面 6）。<c>village_a1</c> の画角の夕方</summary>
        VillageEvening,
    }

    /// <summary>背景の場所の環境音の一本。file は <c>Assets/Audio/</c> の中の名（拡張子なし）、volume はふだんの大きさ</summary>
    public struct TitleSound
    {
        public readonly string File;
        public readonly float Volume;

        public TitleSound(string file, float volume)
        {
            File = file;
            Volume = volume;
        }
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

        /// <summary>夕方の村の沈め方の弱さ。朝と、ふつうの沈め方のあいだより朝寄り</summary>
        public const float EveningLight = 0.75f;

        /// <summary>
        /// 背景の沈め方をどれだけ弱めるか。0 でふつう（夜の場面）、1 で朝の村の弱さ。
        /// 村は時刻の感じを残す。朝は明るさ、夕方は夕日に照らされた家の壁とアーチの花の暖かい色
        /// （庭はオーナーが最重要とする場所。ふつうに沈めると夕日の色が消え、家もアーチも暗い塊になる）
        /// </summary>
        public static float Light(TitleBackdrop b)
        {
            switch (b)
            {
                case TitleBackdrop.VillageMorning: return 1f;
                case TitleBackdrop.VillageEvening: return EveningLight;
                default: return 0f;
            }
        }

        /// <summary>路地裏の雨の大きさ。場面 2 の Player/RainSound と同じ値（BuildAlley の RainSound）</summary>
        public const float AlleyRain = 0.30f;

        /// <summary>
        /// 村の背景（<c>village_a1</c>）の目の東西の位置（世界の x）。裏庭のアーチのトンネルの北の端の先
        /// （設計書 5 節の表、<c>CheckVillage.GardenViews</c> の g1_title）。朝の麦の風の残りをここで見る
        /// </summary>
        public const float VillageEyeX = -2.889f;

        /// <summary>
        /// 背景の場所の環境音。その場面で流している輪を、その場面の中と同じ大きさで重ねて流す（オーナー、2026-09-27）。
        /// 自室は部屋の空気（Player/RoomTone、<see cref="RoomTone.DefaultVolume"/>）。
        /// 路地裏は、場面 2 の通りの所の釣り合い（雑踏 <see cref="CrowdNoise.StreetDefault"/> と雨 <see cref="AlleyRain"/>）。
        /// 村は <see cref="VillageAmbience"/> と同じ。朝は朝の村の輪と麦の風の輪を重ねるが、麦の風は背景の目の位置
        /// （<see cref="VillageEyeX"/>）での残り（<see cref="VillageAmbience.Reach"/>）を掛け、消えていれば流さない。
        /// 夕方は麦の風だけ。潜る・車内（ガレージ）は無音。
        /// 音のファイルは <c>Assets/Audio/</c> の <c>名.wav</c>
        /// </summary>
        public static TitleSound[] SoundsOf(TitleBackdrop b)
        {
            switch (b)
            {
                case TitleBackdrop.Room: return new[] { new TitleSound("RoomTone", RoomTone.DefaultVolume) };
                case TitleBackdrop.Alley:
                    return new[] { new TitleSound("CrowdLoop", CrowdNoise.StreetDefault), new TitleSound("RainLoop", AlleyRain) };
                case TitleBackdrop.VillageMorning:
                    {
                        var village = new TitleSound("VillageMorning", VillageAmbience.DefaultMorningVillage);
                        var wheat = VillageAmbience.DefaultMorningWheat * VillageAmbience.Reach(VillageEyeX,
                            VillageAmbience.DefaultWheatFadeFrom, VillageAmbience.DefaultWheatFadeTo);
                        return wheat > 0f ? new[] { village, new TitleSound("WheatWind", wheat) } : new[] { village };
                    }
                case TitleBackdrop.VillageEvening: return new[] { new TitleSound("WheatWind", VillageAmbience.DefaultEveningWheat) };
                default: return new TitleSound[0];
            }
        }

        /// <summary>絵のファイルの名（拡張子なし）。<c>Assets/Textures/Title/</c> に置く</summary>
        public static string FileName(TitleBackdrop b)
        {
            switch (b)
            {
                case TitleBackdrop.Alley: return "alley_1";
                case TitleBackdrop.Dive: return "dive_estate";
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
