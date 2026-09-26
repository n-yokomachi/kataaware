using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HalfAware
{
    /// <summary>セーブを置く所。ゲームでは PlayerPrefs、テストでは手元の辞書</summary>
    public interface ISaveBox
    {
        /// <summary>無ければ null</summary>
        string Get(string key);
        void Set(string key, string value);
        void Remove(string key);
        /// <summary>書いた物を確かに残す</summary>
        void Flush();
    }

    /// <summary>
    /// PlayerPrefs に置く。WebGL ではブラウザの中（IndexedDB）に落ちる。
    /// **書いたら必ず <see cref="Flush"/>（PlayerPrefs.Save）。** WebGL では、呼ばないとタブを閉じた時に消える
    /// </summary>
    public sealed class PrefsBox : ISaveBox
    {
        public string Get(string key)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public void Set(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }

        public void Remove(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        public void Flush()
        {
            PlayerPrefs.Save();
        }
    }

    /// <summary>手元の辞書に置く。テストと、エディタで撮る時に使う（PlayerPrefs を汚さない）</summary>
    public sealed class MemoryBox : ISaveBox
    {
        readonly Dictionary<string, string> kept = new Dictionary<string, string>();

        /// <summary>Flush を呼ばれた数</summary>
        public int Flushes { get; private set; }

        public string Get(string key)
        {
            string value;
            return kept.TryGetValue(key, out value) ? value : null;
        }

        public void Set(string key, string value)
        {
            kept[key] = value;
        }

        public void Remove(string key)
        {
            kept.Remove(key);
        }

        public void Flush()
        {
            Flushes++;
        }
    }

    /// <summary>
    /// セーブの読み書きと、クリアの印（設計書 5 節）。見せ方も場面の読み込みも持たない（それは <see cref="SaveFlow"/>）。
    ///
    /// - セーブは JSON にして、置き場ごとの鍵に書く。自動 1 つ、手動 3 つ
    /// - クリアの印は、セーブとは別の鍵に持つ。一度付いたら消えない（消すのはエディタのメニューで確かめる時だけ）
    /// </summary>
    public static class SaveStore
    {
        public const string SaveKey = "HalfAware.Save.";
        public const string ClearedKey = "HalfAware.Cleared";

        /// <summary>読む時に並べる順。自動、手動 1・2・3</summary>
        public static readonly SaveSlot[] All = { SaveSlot.Auto, SaveSlot.First, SaveSlot.Second, SaveSlot.Third };

        /// <summary>記憶する（手動で書く）で選べる置き場</summary>
        public static readonly SaveSlot[] Manual = { SaveSlot.First, SaveSlot.Second, SaveSlot.Third };

        public const string AutoName = "自動";
        public const string Empty = "空き";

        static ISaveBox box;

        /// <summary>置き場。既定は PlayerPrefs。テストや撮影では手元の辞書に差し替え、終えたら null で戻す</summary>
        public static ISaveBox Box
        {
            get
            {
                if (box == null) box = new PrefsBox();
                return box;
            }
            set { box = value; }
        }

        public static string KeyOf(SaveSlot slot)
        {
            return SaveKey + (slot == SaveSlot.Auto ? "Auto" : ((int)slot).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>行の頭に出す置き場の名。「自動」「1」「2」「3」</summary>
        public static string NameOf(SaveSlot slot)
        {
            return slot == SaveSlot.Auto ? AutoName : ((int)slot).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>読む。無い、壊れている、知らない場面なら null</summary>
        public static SaveData Read(SaveSlot slot)
        {
            var json = Box.Get(KeyOf(slot));
            if (string.IsNullOrEmpty(json)) return null;
            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
            return Usable(data) ? data : null;
        }

        /// <summary>読んで始められる物か。番号が範囲の中で、シーンの名がある</summary>
        public static bool Usable(SaveData data)
        {
            return data != null && data.stage >= StageMap.First && data.stage <= StageMap.Last
                && !string.IsNullOrEmpty(data.scene);
        }

        /// <summary>書く。書いた日時は now で付ける（写しに付けるので、渡した物は変わらない）</summary>
        public static SaveData Write(SaveSlot slot, SaveData data, DateTime utcNow)
        {
            var d = data.Copy();
            d.version = SaveData.CurrentVersion;
            d.written = Stamp(LocalClock.ToLocal(utcNow));
            d.writtenTicks = utcNow.Ticks;
            Box.Set(KeyOf(slot), JsonUtility.ToJson(d));
            Box.Flush();
            return d;
        }

        public static SaveData Write(SaveSlot slot, SaveData data)
        {
            return Write(slot, data, DateTime.UtcNow);
        }

        /// <summary>消す。エディタのメニューとテストから使う。ゲームの中では消さない</summary>
        public static void Forget(SaveSlot slot)
        {
            Box.Remove(KeyOf(slot));
            Box.Flush();
        }

        /// <summary>行に出す日時の書式</summary>
        public static string Stamp(DateTime local)
        {
            return local.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>自動と手動のうち、最後に書いた物。一つも無ければ null。同じ時なら自動を先に採る</summary>
        public static SaveData Newest()
        {
            SaveData best = null;
            for (var i = 0; i < All.Length; i++)
            {
                var d = Read(All[i]);
                if (d == null) continue;
                if (best == null || d.writtenTicks > best.writtenTicks) best = d;
            }
            return best;
        }

        /// <summary>読めるセーブが一つでもあるか</summary>
        public static bool Any()
        {
            for (var i = 0; i < All.Length; i++)
                if (Read(All[i]) != null) return true;
            return false;
        }

        // ---- クリアの印 ------------------------------------------------------

        /// <summary>物語を最後まで見たか</summary>
        public static bool Cleared
        {
            get { return Box.Get(ClearedKey) == "1"; }
        }

        /// <summary>
        /// クリアの印を付ける。一度付いたら消えない。場面 10 の終わり（物語の最後）から呼ぶ。
        /// 付けた後のタイトルの画面は、いちばん新しいセーブより先に朝の村を背景にする
        /// </summary>
        public static void MarkCleared()
        {
            if (Cleared) return;
            Box.Set(ClearedKey, "1");
            Box.Flush();
        }

        /// <summary>印を外す。**確かめ専用**（エディタのメニュー）。ゲームからは呼ばない</summary>
        public static void ForgetCleared()
        {
            Box.Remove(ClearedKey);
            Box.Flush();
        }

        // ---- 場面をまたぐ状態 --------------------------------------------------

        /// <summary>いまの場面をまたぐ状態を拾って、場面の頭のセーブを作る。日時はまだ付けない</summary>
        public static SaveData Capture(int stage, string scene, string hour)
        {
            return new SaveData
            {
                stage = stage,
                scene = scene ?? string.Empty,
                hour = hour ?? string.Empty,
                diveHops = DiveHandoff.Hops,
                fromDive = DiveHandoff.FromDive,
            };
        }

        /// <summary>セーブの場面をまたぐ状態を、持ち越す所へ戻す。シーンを読む前に呼ぶ</summary>
        public static void Restore(SaveData data)
        {
            DiveHandoff.Hops = data.diveHops;
            DiveHandoff.FromDive = data.fromDive;
        }
    }
}
