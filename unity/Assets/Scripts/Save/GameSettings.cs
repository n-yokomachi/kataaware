using System.Globalization;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 設定の一つの値（倍率など）。範囲・刻み・既定を持ち、書くたびに範囲へ収めて刻みへ揃える。
    /// 読み書きは <see cref="GameSettings.Box"/>。初めて読む時に鍵から読み、書くとすぐ鍵へ書く（Flush は <see cref="GameSettings.Commit"/>）。
    /// 見せ方は持たない（コンソールの設定の枠、<see cref="ConsoleSettings"/>）
    /// </summary>
    public sealed class SettingDial
    {
        /// <summary>PlayerPrefs の鍵</summary>
        public readonly string Key;
        public readonly float Min;
        public readonly float Max;
        public readonly float Step;
        public readonly float Default;
        /// <summary>値の後ろに付ける字。倍率なら「×」</summary>
        public readonly string Suffix;

        float value;
        bool loaded;

        public SettingDial(string key, float min, float max, float step, float fallback, string suffix)
        {
            Key = key;
            Min = min;
            Max = max;
            Step = step;
            Default = fallback;
            Suffix = suffix ?? string.Empty;
        }

        /// <summary>刻みの数。範囲の両端を含めて Steps + 1 個の値をとる</summary>
        public int Steps { get { return Mathf.RoundToInt((Max - Min) / Step); } }

        /// <summary>いまの値。書くと範囲へ収めて刻みへ揃え、鍵へ書く</summary>
        public float Value
        {
            get
            {
                Load();
                return value;
            }
            set
            {
                Load();
                var v = Snap(value);
                if (v == this.value) return;
                this.value = v;
                GameSettings.Put(Key, v.ToString("0.###", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>範囲へ収めて、いちばん近い刻みへ揃える。刻みの真ん中は偶数の刻みへ寄る（Mathf.RoundToInt）</summary>
        public float Snap(float v)
        {
            if (float.IsNaN(v)) v = Default;
            v = Mathf.Clamp(v, Min, Max);
            var n = Mathf.Clamp(Mathf.RoundToInt((v - Min) / Step), 0, Steps);
            // 0.25 + n × 0.05 の float の端数（1.0000001 など）を落として、書いた字と読み直した値を揃える
            return (float)System.Math.Round(Min + n * (double)Step, 4);
        }

        /// <summary>steps 刻みだけ動かす。両端で止まる</summary>
        public void Nudge(int steps)
        {
            if (steps == 0) return;
            Value = Value + steps * Step;
        }

        /// <summary>既定へ戻す</summary>
        public void Reset()
        {
            Value = Default;
        }

        /// <summary>範囲の中の位置。Min が 0、Max が 1</summary>
        public float Fraction(float v)
        {
            return Max > Min ? Mathf.Clamp01((v - Min) / (Max - Min)) : 0f;
        }

        /// <summary>範囲の中の位置 t（0〜1）の値。刻みへ揃える</summary>
        public float AtFraction(float t)
        {
            return Snap(Min + Mathf.Clamp01(t) * (Max - Min));
        }

        /// <summary>値の字。「1.00×」</summary>
        public string Text(float v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture) + Suffix;
        }

        /// <summary>鍵に書いてあった字から値。無い・読めない字は既定</summary>
        public float Parse(string text)
        {
            float v;
            if (string.IsNullOrEmpty(text) || !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                || float.IsNaN(v) || float.IsInfinity(v))
                return Snap(Default);
            return Snap(v);
        }

        void Load()
        {
            if (loaded) return;
            loaded = true;
            value = Parse(GameSettings.Box.Get(Key));
        }

        /// <summary>覚えている値を捨てる。次に読む時に鍵から読み直す</summary>
        internal void Forget()
        {
            loaded = false;
        }
    }

    /// <summary>
    /// 遊ぶ人が変える設定の値（コンソールの設定の枠、設計書 1 節）。
    ///
    /// - PlayerPrefs の <see cref="KeyPrefix"/> の下に、値ごとの鍵で書く。WebGL ではブラウザの中（IndexedDB）に残る
    /// - **セーブとは別に持つ。** 鍵も別（<see cref="SaveStore.SaveKey"/>・<see cref="SaveStore.ClearedKey"/> と重ならない）なので、
    ///   セーブを消しても残り、思い出すでも変わらない
    /// - 起動の後、最初に読む時に鍵から読む。動かすとすぐ値が変わり（見回しは毎フレーム読む）、
    ///   Flush（PlayerPrefs.Save）は設定の枠を離れる時とコンソールを閉じる時にまとめて一度（<see cref="Commit"/>）。
    ///   WebGL の PlayerPrefs.Save はブラウザの中へ書き出すので、つまみを動かすフレームごとには呼ばない
    /// </summary>
    public static class GameSettings
    {
        public const string KeyPrefix = "HalfAware.Settings.";

        /// <summary>
        /// カメラの速さ。見回し（マウスとゲームパッドの右スティック）の速さに掛ける倍率。
        /// 1 が今の速さ（<see cref="PlayerController.BaseLookSensitivity"/>）。0.25〜2 倍を 0.05 刻み
        /// </summary>
        public static readonly SettingDial LookScale = new SettingDial(KeyPrefix + "LookScale", 0.25f, 2f, 0.05f, 1f, "×");

        /// <summary>持っている値の全部。値を足したらここにも足す（読み直しと既定に戻すが回る）</summary>
        public static readonly SettingDial[] All = { LookScale };

        static ISaveBox box;
        static bool dirty;

        /// <summary>置き場。既定は PlayerPrefs。テストや撮影では手元の辞書に差し替え、終えたら null で戻す。差し替えると値を読み直す</summary>
        public static ISaveBox Box
        {
            get
            {
                if (box == null) box = new PrefsBox();
                return box;
            }
            set
            {
                box = value;
                dirty = false;
                Forget();
            }
        }

        /// <summary>鍵へ書いたが、まだ Flush していないか</summary>
        public static bool Unsaved { get { return dirty; } }

        internal static void Put(string key, string text)
        {
            Box.Set(key, text);
            dirty = true;
        }

        /// <summary>書いた物を確かに残す（PlayerPrefs.Save）。書いていなければ何もしない</summary>
        public static void Commit()
        {
            if (!dirty) return;
            dirty = false;
            Box.Flush();
        }

        /// <summary>全部の値を既定へ戻す</summary>
        public static void ResetAll()
        {
            for (var i = 0; i < All.Length; i++) All[i].Reset();
        }

        /// <summary>覚えている値を捨てる。次に読む時に鍵から読み直す（起動し直したのと同じ）</summary>
        public static void Forget()
        {
            for (var i = 0; i < All.Length; i++) All[i].Forget();
        }

        /// <summary>再生を始めるたびに読み直す。ドメインを読み直さない設定でも、前の再生の値を持ち越さない</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Fresh()
        {
            box = null;
            dirty = false;
            Forget();
        }
    }
}
