using UnityEngine;

namespace HalfAware
{
    /// <summary>設定の枠の行の種類</summary>
    public enum SettingKind
    {
        /// <summary>項目の区切りの小見出し（「カメラ」）。選べない</summary>
        Heading,

        /// <summary>つまみ（スライダー）で値を動かす項目</summary>
        Dial,

        /// <summary>既定に戻す。E・Enter か押すと、全部の値を既定へ戻す</summary>
        Reset,
    }

    /// <summary>設定の枠の一行</summary>
    public sealed class SettingRow
    {
        public readonly SettingKind Kind;
        public readonly string Label;
        /// <summary>つまみの行が動かす値。ほかの行は null</summary>
        public readonly SettingDial Dial;

        SettingRow(SettingKind kind, string label, SettingDial dial)
        {
            Kind = kind;
            Label = label;
            Dial = dial;
        }

        public static SettingRow Heading(string label)
        {
            return new SettingRow(SettingKind.Heading, label, null);
        }

        public static SettingRow Of(string label, SettingDial dial)
        {
            return new SettingRow(SettingKind.Dial, label, dial);
        }

        public static SettingRow Reset(string label)
        {
            return new SettingRow(SettingKind.Reset, label, null);
        }

        /// <summary>選べる行か。小見出しは選べない</summary>
        public bool Selectable { get { return Kind != SettingKind.Heading; } }
    }

    /// <summary>
    /// コンソールの設定の枠（設計書 1 節）に並べる行の表。**項目を足す時はここに一行足す**
    /// （値そのものは <see cref="GameSettings"/> に足す）。選ぶ・動かすは <see cref="ConsoleMenu"/>、見せ方は <see cref="ImplantConsole"/>
    /// </summary>
    public static class ConsoleSettings
    {
        public const string Title = ConsoleMenu.SettingsLabel;
        public const string Camera = "カメラ";
        public const string LookSpeed = "カメラの速さ";
        public const string ResetLabel = "既定に戻す";

        /// <summary>上から並べる行</summary>
        public static readonly SettingRow[] Rows =
        {
            SettingRow.Heading(Camera),
            SettingRow.Of(LookSpeed, GameSettings.LookScale),
            SettingRow.Reset(ResetLabel),
        };

        /// <summary>
        /// マウスで押した所から、つまみの位置（0〜1）。x は当たりの中の割合（左の端 0・右の端 1、<see cref="ConsolePointer.Held"/>）、
        /// width は当たりの幅、inset は当たりの両端の、溝の外の幅（つまみの半分。端の値でもつまみが当たりからはみ出さない）
        /// </summary>
        public static float TrackAt(float x, float width, float inset)
        {
            var span = width - inset * 2f;
            if (span <= 0f) return 0f;
            return Mathf.Clamp01((Mathf.Clamp01(x) * width - inset) / span);
        }
    }

    /// <summary>
    /// 押し続けで続けて動かす（設定の枠の左右）。押した時に一つ動き、<see cref="Delay"/> 秒押し続けると
    /// <see cref="Every"/> 秒ごとに一つずつ動く。秒は unscaled で渡す（コンソールを開いている間は Time.timeScale が 0）。
    /// 押した時から数えるので、枠を開く前から押していた鍵では動かない
    /// </summary>
    public sealed class HoldRepeat
    {
        /// <summary>続けて動き出すまでの秒</summary>
        public const float Delay = 0.4f;

        /// <summary>続けて動く間の、一つの秒。0.25〜2 倍の端から端（35 刻み）を 2 秒ほどで渡る</summary>
        public const float Every = 0.05f;

        int held;
        float next;

        /// <summary>
        /// 1 フレームぶん。pressed はこのフレームで押した向き（-1・0・1）、down はいま押している向き。
        /// 動かす向き（-1・0・1）を返す
        /// </summary>
        public int Step(int pressed, int down, float now)
        {
            if (pressed != 0)
            {
                held = pressed;
                next = now + Delay;
                return pressed;
            }
            if (held == 0 || down != held)
            {
                held = 0;
                return 0;
            }
            if (now < next) return 0;
            // 遅いフレームでも一フレームに一つだけ動かす
            next = Mathf.Max(next + Every, now);
            return held;
        }

        /// <summary>押し続けを忘れる。次は押した時から</summary>
        public void Release()
        {
            held = 0;
        }
    }
}
