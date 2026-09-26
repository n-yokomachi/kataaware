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
    /// セーブ一つの中身（設計書 5 節）。
    ///
    /// - **自動**は場面の頭を残す。読むと、その場面の頭から始まる
    /// - **手動（記憶する）**は、押した時の場面の中の状態（<see cref="memo"/>）も残す。読むと、そこから続ける。
    ///   台詞・二択・演出の途中で記憶した時は、その直前の、自由に動ける所を残す（<see cref="SceneMemory"/>）
    ///
    /// 場面をまたいで持ち越す状態（いまは場面 4 から場面 5 へ渡す <see cref="DiveHandoff"/> だけ）も入れる。
    /// ログは入れない（場面ごとに消える物なので）。
    /// 持ち越す状態を増やしたら、ここに項目を足し、<see cref="SaveStore.Capture"/> と
    /// <see cref="SaveStore.Restore"/> にも足す
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// 1: 場面の頭だけ。2: 手動は場面の中の状態（<see cref="within"/>・<see cref="memo"/>）も持つ（2026-09-27）。
        /// 1 の物には <see cref="within"/> が無いので false に読め、場面の頭から始まる
        /// </summary>
        public const int CurrentVersion = 2;

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

        /// <summary>
        /// 場面の中の状態を持っているか。false なら場面の頭から始まる（自動、前の形のセーブ、
        /// 場面に入ってから自由に動ける所へまだ一度も来ていない時の記憶する）。
        /// JsonUtility は入れ子の物を null のまま書けないので、<see cref="memo"/> の有無はこれで見る
        /// </summary>
        public bool within;

        /// <summary>場面の中の状態。<see cref="within"/> が true の時だけ使う</summary>
        public SceneMemo memo = new SceneMemo();

        public SaveData Copy()
        {
            return (SaveData)MemberwiseClone();
        }
    }
}
