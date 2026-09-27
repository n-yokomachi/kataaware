namespace HalfAware
{
    /// <summary>
    /// 場面 9 の終わりと場面 10（対面）の文字の値（シナリオ設計書 12 節）。
    ///
    /// **どれも仮。** 卓の前の独白・主人公の名・片割れの言葉・最後の一文は、後で決める（12.3 節）。タイトルと終わりのクレジットはエンディング（13 節）が受け持つ。
    /// 決まったらここを書き換える。演出（<see cref="ReunionDirector"/>）はどれもここから組む。
    /// 片割れの名は場面 6 と同じく <see cref="GardenMemory.TwinName"/> の一か所
    /// </summary>
    public static class Reunion
    {
        /// <summary>卓の前に立った時の独白（場面 9 の終わり）。庭の記憶で見た場所を、現実で見るのは初めて、という筋。**仮**</summary>
        public const string TableLine = "記憶で見た場所を、この目で見るのは初めてだった";

        /// <summary>主人公の名。まだ決めていない。**仮**</summary>
        public const string HeroineName = "（主人公の名）";

        /// <summary>片割れが言う言葉。{0} に主人公の名。**仮**（仮に、名を呼ぶ形）</summary>
        public const string TwinSaysSource = "{0}";

        /// <summary>最後の独白の一文。**仮**（候補のもう一つは「私を覚えていた人は、私と同じ顔をしていた」）</summary>
        public const string LastLine = "あの庭で振り返ったのは、私だった";

        /// <summary>
        /// 庭で聞き取れなかった言葉。庭の記憶の名を呼ぶ一行（<see cref="GardenMemory.CallSource"/>）を崩さずに出す。
        /// 名前の行は出さない（字幕は話し手の名を分けない形。場面 6 の崩れた一行と同じ帯）
        /// </summary>
        public static string Voice
        {
            get { return "「" + string.Format(GardenMemory.CallSource, GardenMemory.TwinName) + "」"; }
        }

        /// <summary>片割れの言葉。名前の行は片割れの名（庭で聞こえた名）</summary>
        public static string TwinSays
        {
            get { return GardenMemory.TwinName + "「" + string.Format(TwinSaysSource, HeroineName) + "」"; }
        }
    }
}
