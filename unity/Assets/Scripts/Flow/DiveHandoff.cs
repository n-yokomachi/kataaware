namespace HalfAware
{
    /// <summary>
    /// 場面 4 から場面 5 へ渡す値。
    ///
    /// シーンを読み込み直すと場面 4 のものは何も残らないので、
    /// 渡った人数だけを静的に持ち越す。場面 5 はこれで眩暈の濃さを決める。
    /// 場面 5 を単体で開いたときは <see cref="FromDive"/> が false のままなので、
    /// そちらは薄い眩暈で始まる
    /// </summary>
    public static class DiveHandoff
    {
        /// <summary>切断するまでに渡り歩いた人数</summary>
        public static int Hops;

        /// <summary>場面 4 から切断して来たか</summary>
        public static bool FromDive;

        /// <summary>初めから始め直すときに戻す。動作確認から呼ぶ</summary>
        public static void Clear()
        {
            Hops = 0;
            FromDive = false;
        }
    }
}
