using System;

namespace HalfAware
{
    /// <summary>セーブの置き場。自動が一つ、手動が三つ（設計書 5 節）</summary>
    public enum SaveSlot
    {
        /// <summary>場面の頭に着いたら黙って書く</summary>
        Auto = 0,
        First = 1,
        Second = 2,
        Third = 3,
    }

    /// <summary>
    /// セーブ一つの中身。**残すのは場面の頭。** 読むと、その場面の頭から始まる（設計書 5 節）。
    ///
    /// 場面をまたいで持ち越す状態（いまは場面 4 から場面 5 へ渡す <see cref="DiveHandoff"/> だけ）も入れる。
    /// ログは入れない（場面ごとに消える物なので）。
    /// 持ち越す状態を増やしたら、ここに項目を足し、<see cref="SaveStore.Capture"/> と
    /// <see cref="SaveStore.Restore"/> にも足す
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        /// <summary>形の版。項目の意味を変えたら上げる</summary>
        public int version = CurrentVersion;

        /// <summary>場面の番号（1〜10）</summary>
        public int stage;

        /// <summary>場面の頭の Unity のシーンの名</summary>
        public string scene = string.Empty;

        /// <summary>村の時刻（<see cref="StageMap.Morning"/>・<see cref="StageMap.Evening"/>）。村でなければ空</summary>
        public string hour = string.Empty;

        /// <summary>場面 4 で切断するまでに渡り歩いた人数（<see cref="DiveHandoff.Hops"/>）</summary>
        public int diveHops;

        /// <summary>場面 4 から切断して来たか（<see cref="DiveHandoff.FromDive"/>）</summary>
        public bool fromDive;

        /// <summary>書いた日時。その土地の時刻で「2026/09/27 14:05」。行に出す</summary>
        public string written = string.Empty;

        /// <summary>書いた日時の UTC の Ticks。いちばん新しいセーブを選ぶのに使う</summary>
        public long writtenTicks;

        public SaveData Copy()
        {
            return (SaveData)MemberwiseClone();
        }
    }
}
