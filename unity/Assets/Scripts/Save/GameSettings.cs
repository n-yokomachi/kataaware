using System;
using System.Globalization;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 設定の一つの値の共通の形。鍵と、既定へ戻す・鍵から読み直す。
    /// 値の形は二つ: 範囲と刻みのある数（<see cref="SettingDial"/>、つまみ）と、名の中から一つを選ぶ物（<see cref="SettingChoice"/>）
    /// </summary>
    public abstract class SettingValue
    {
        /// <summary>PlayerPrefs の鍵</summary>
        public readonly string Key;

        /// <summary>値が変わった（書いた・既定に戻した）。鍵から読み直しただけでは呼ばない</summary>
        public event Action Changed;

        protected SettingValue(string key)
        {
            Key = key;
        }

        /// <summary>値が変わったことを知らせる</summary>
        protected void Tell()
        {
            if (Changed != null) Changed();
        }

        /// <summary>既定へ戻す</summary>
        public abstract void Reset();

        /// <summary>覚えている値を捨てる。次に読む時に鍵から読み直す</summary>
        internal abstract void Forget();
    }

    /// <summary>
    /// 設定の一つの値（倍率・割合など）。範囲・刻み・既定を持ち、書くたびに範囲へ収めて刻みへ揃える。
    /// 読み書きは <see cref="GameSettings.Box"/>。初めて読む時に鍵から読み、書くとすぐ鍵へ書く（Flush は <see cref="GameSettings.Tick"/>・<see cref="GameSettings.Commit"/>）。
    /// 値が変わったら <see cref="SettingValue.Changed"/> で知らせる（画面の加工の強さを、その場で効かせる）。
    /// 見せ方は持たない（コンソールの設定の枠、<see cref="ConsoleSettings"/>）
    /// </summary>
    public sealed class SettingDial : SettingValue
    {
        public readonly float Min;
        public readonly float Max;
        public readonly float Step;
        public readonly float Default;
        /// <summary>値の後ろに付ける字。倍率なら「×」、割合なら「%」</summary>
        public readonly string Suffix;
        /// <summary>値の字の書式。倍率なら "0.00"、割合なら "0"</summary>
        public readonly string Format;
        /// <summary>字にする前に値へ掛ける数。倍率なら 1、割合（0〜1 を「50%」と出す）なら 100</summary>
        public readonly float Shown;

        float value;
        bool loaded;

        /// <summary>倍率の形。値をそのまま「1.50×」のように出す</summary>
        public SettingDial(string key, float min, float max, float step, float fallback, string suffix)
            : this(key, min, max, step, fallback, suffix, "0.00", 1f)
        {
        }

        public SettingDial(string key, float min, float max, float step, float fallback, string suffix, string format, float shown) : base(key)
        {
            Min = min;
            Max = max;
            Step = step;
            Default = fallback;
            Suffix = suffix ?? string.Empty;
            Format = string.IsNullOrEmpty(format) ? "0.00" : format;
            Shown = shown;
        }

        /// <summary>割合の形。0〜1 を 0.05 刻み（5% 刻み）で持ち、「50%」のように出す</summary>
        public static SettingDial Percent(string key, float fallback)
        {
            return new SettingDial(key, 0f, 1f, 0.05f, fallback, "%", "0", 100f);
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
                Tell();
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
        public override void Reset()
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

        /// <summary>値の字。倍率なら「1.00×」、割合なら「50%」</summary>
        public string Text(float v)
        {
            return (v * Shown).ToString(Format, CultureInfo.InvariantCulture) + Suffix;
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
        internal override void Forget()
        {
            loaded = false;
        }
    }

    /// <summary>
    /// 設定の、いくつかの名の中から一つを選ぶ値（画面のフィルターの型など）。左右で一つずつ動かし、両端で止まる。
    /// 鍵には選んだ物の符丁（<see cref="Ids"/>）を書く。数で書かないのは、並びを入れ替えたり間に足したりしても、残した選びがずれないようにするため。
    /// 読み書きと Flush は <see cref="SettingDial"/> と同じ。値が変わったら <see cref="SettingValue.Changed"/> で知らせる（その場で画面へ効かせる）
    /// </summary>
    public sealed class SettingChoice : SettingValue
    {
        /// <summary>鍵に書く符丁。並びの順</summary>
        public readonly string[] Ids;
        /// <summary>設定の枠に出す名。並びの順</summary>
        public readonly string[] Labels;
        /// <summary>既定の番号</summary>
        public readonly int Default;

        int value;
        bool loaded;

        public SettingChoice(string key, string[] ids, string[] labels, int fallback) : base(key)
        {
            Ids = ids ?? new string[0];
            Labels = labels ?? Ids;
            Default = Mathf.Clamp(fallback, 0, Mathf.Max(0, Ids.Length - 1));
        }

        /// <summary>選べる物の数</summary>
        public int Count { get { return Ids.Length; } }

        /// <summary>いまの番号。書くと範囲へ収めて、鍵へ符丁を書く</summary>
        public int Value
        {
            get
            {
                Load();
                return value;
            }
            set
            {
                Load();
                var v = Clamp(value);
                if (v == this.value) return;
                this.value = v;
                if (Count > 0) GameSettings.Put(Key, Ids[v]);
                Tell();
            }
        }

        /// <summary>範囲へ収める</summary>
        public int Clamp(int v)
        {
            return Mathf.Clamp(v, 0, Mathf.Max(0, Count - 1));
        }

        /// <summary>steps 個だけ動かす。両端で止まる</summary>
        public void Nudge(int steps)
        {
            if (steps == 0) return;
            Value = Value + steps;
        }

        /// <summary>次の物へ。最後の次は最初へ回る（マウスで名を押した時）</summary>
        public void Cycle()
        {
            if (Count == 0) return;
            Value = (Value + 1) % Count;
        }

        public override void Reset()
        {
            Value = Default;
        }

        /// <summary>番号 v の名</summary>
        public string Text(int v)
        {
            return Count > 0 ? Labels[Clamp(v)] : string.Empty;
        }

        /// <summary>鍵に書いてあった符丁から番号。無い・知らない符丁は既定</summary>
        public int Parse(string text)
        {
            if (!string.IsNullOrEmpty(text))
                for (var i = 0; i < Ids.Length; i++)
                    if (Ids[i] == text) return i;
            return Default;
        }

        void Load()
        {
            if (loaded) return;
            loaded = true;
            value = Parse(GameSettings.Box.Get(Key));
        }

        internal override void Forget()
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
    /// - 起動の後、最初に読む時に鍵から読む。動かすとすぐ値が変わる（見回しは毎フレーム読む）
    /// - **動かしたら、その場で残す。** 動きが止まってから <see cref="SaveDelay"/> 秒で Flush（PlayerPrefs.Save）する（<see cref="Tick"/>）。
    ///   設定の枠を開いたままブラウザのタブを閉じても残る。WebGL の PlayerPrefs.Save はブラウザの中へ書き出すので、
    ///   つまみを掴んで動かしている間や押し続けている間の、フレームごとには呼ばない。コンソールを閉じる時は待たずに書く（<see cref="Commit"/>）
    /// </summary>
    public static class GameSettings
    {
        public const string KeyPrefix = "HalfAware.Settings.";

        /// <summary>
        /// カメラの速さ。見回し（マウスとゲームパッドの右スティック）の速さに掛ける倍率。
        /// 1 が元の速さ（マウスは <see cref="PlayerController.BaseLookSensitivity"/>、スティックは <see cref="PlayerController.StickDegreesPerSecond"/>）。
        /// 0.25〜2 倍を 0.05 刻み。既定は 1.5 倍（オーナー、2026-09-29「デフォルトのカメラ速度を1.5に」。前は 1 倍）
        /// </summary>
        public static readonly SettingDial LookScale = new SettingDial(KeyPrefix + "LookScale", 0.25f, 2f, 0.05f, DefaultLookScale, "×");

        /// <summary>カメラの速さの既定の倍率。「既定に戻す」もここへ戻す</summary>
        public const float DefaultLookScale = 1.5f;

        /// <summary>
        /// 画面のフィルターの型（<see cref="ScreenFilter"/>）。「標準」（粗い画面に規則的な点）と「減色＋ディザ」（色の数を絞り、散らばった点で埋める）。
        /// 既定は標準（オーナー、2026-10-05「ゲームにかけているフィルターだけど、この動画と同じようなパターンも作れる？設定から変更できるといい」）
        /// </summary>
        public static readonly SettingChoice Filter = new SettingChoice(KeyPrefix + "Filter", ScreenFilter.Ids, ScreenFilter.Labels, (int)ScreenFilterKind.Standard);

        /// <summary>
        /// 減色の強さ。「減色＋ディザ」で、色を色の組へどこまで寄せるか（<see cref="ScreenFilter.TintName"/>、色の寄せ）。
        /// 0〜100% を 5% 刻み。0% で元の色のまま、100% で色の組へ寄せきる。標準の型では効かない
        /// （オーナー、2026-10-05「減色やディザの度合いが強すぎる」「設定から調整できるようにして」）
        /// </summary>
        public static readonly SettingDial FilterTint = SettingDial.Percent(KeyPrefix + "FilterTint", ScreenFilter.DefaultTint);

        /// <summary>
        /// ディザの強さ。「減色＋ディザ」の点の明暗差（<see cref="ScreenFilter.DotsName"/>、点の濃さ）。
        /// 0〜100% を 5% 刻み。0% で点を打たない、100% で色の組の二色の明暗差のまま。標準の型では効かない
        /// </summary>
        public static readonly SettingDial FilterDots = SettingDial.Percent(KeyPrefix + "FilterDots", ScreenFilter.DefaultDots);

        /// <summary>持っている値の全部。値を足したらここにも足す（読み直しと既定に戻すが回る）</summary>
        public static readonly SettingValue[] All = { LookScale, Filter, FilterTint, FilterDots };

        /// <summary>動きが止まってから Flush するまでの秒</summary>
        public const float SaveDelay = 0.5f;

        static ISaveBox box;
        static bool dirty;
        /// <summary>鍵へ書いた数と、<see cref="Tick"/> が最後に見た数。違えば、その後に動いた</summary>
        static int changes;
        static int seen;
        static float settleAt;

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
                Settle();
                Forget();
            }
        }

        /// <summary>鍵へ書いたが、まだ Flush していないか</summary>
        public static bool Unsaved { get { return dirty; } }

        internal static void Put(string key, string text)
        {
            Box.Set(key, text);
            dirty = true;
            changes++;
        }

        /// <summary>
        /// 一フレームぶん。now は unscaled の秒（コンソールを開いている間は Time.timeScale が 0）。
        /// 鍵へ書いてから <see cref="SaveDelay"/> 秒、次の書き込みが無ければ Flush する。動き続けている間は待ち直す
        /// </summary>
        public static void Tick(float now)
        {
            if (!dirty) return;
            if (changes != seen)
            {
                seen = changes;
                settleAt = now + SaveDelay;
                return;
            }
            if (now >= settleAt) Commit();
        }

        /// <summary>書いた物を待たずに確かに残す（PlayerPrefs.Save）。書いていなければ何もしない</summary>
        public static void Commit()
        {
            if (!dirty) return;
            Settle();
            Box.Flush();
        }

        static void Settle()
        {
            dirty = false;
            seen = changes;
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
            Settle();
            Forget();
        }
    }
}
