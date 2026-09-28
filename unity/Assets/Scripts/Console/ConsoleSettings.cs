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

        /// <summary>戻る。E・Enter か押すと枠を閉じる。タイトルの画面の枠だけ（思い出すの枠に揃える）</summary>
        Back,
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

        public static SettingRow Back(string label)
        {
            return new SettingRow(SettingKind.Back, label, null);
        }

        /// <summary>選べる行か。小見出しは選べない</summary>
        public bool Selectable { get { return Kind != SettingKind.Heading; } }
    }

    /// <summary>
    /// 設定の枠（設計書 1 節・5 節）に並べる行の表。**項目を足す時はここに一行足す**
    /// （値そのものは <see cref="GameSettings"/> に足す）。コンソールとタイトルの画面が同じ表を使う（タイトルの画面は最後に「戻る」を足す）。
    /// 選ぶ・動かすは <see cref="SettingsList"/>、見せ方と操作は <see cref="SettingsPanel"/>
    /// </summary>
    public static class ConsoleSettings
    {
        /// <summary>枠の見出しの行。ほかの枠（「記憶する　　E で書く」など）に揃えて、操作を添える</summary>
        public const string Title = ConsoleMenu.SettingsLabel + "　　←→ で変える";
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

        /// <summary><see cref="Rows"/> の最後に「戻る」の行を足した表。タイトルの画面の枠に使う</summary>
        public static SettingRow[] WithBack(string label)
        {
            var list = new SettingRow[Rows.Length + 1];
            System.Array.Copy(Rows, list, Rows.Length);
            list[Rows.Length] = SettingRow.Back(label);
            return list;
        }

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
    /// 設定の枠の行の選びと、値の動かし（見せ方は持たない）。コンソール（<see cref="ConsoleMenu"/> が持つ）と
    /// タイトルの画面が、それぞれ一つずつ持つ。上下で選べる行（小見出しは飛ばす）を選び、つまみの行なら左右で値を動かす
    /// </summary>
    public sealed class SettingsList
    {
        readonly SettingRow[] rows;

        public SettingsList(SettingRow[] rows)
        {
            this.rows = rows ?? new SettingRow[0];
        }

        /// <summary>行の数（小見出しも一つと数える）</summary>
        public int Count { get { return rows.Length; } }

        public SettingRow RowAt(int row)
        {
            return row >= 0 && row < rows.Length ? rows[row] : null;
        }

        /// <summary>選んでいる行（0 始まり）。選べる行が無ければ -1</summary>
        public int Row { get; private set; }

        /// <summary>開いた時の形。いちばん上の選べる行（小見出しの次）を選ぶ</summary>
        public void Open()
        {
            Row = Next(-1, 1);
        }

        /// <summary>その行を選べるか。小見出しは選べない</summary>
        public bool Usable(int row)
        {
            return row >= 0 && row < rows.Length && rows[row].Selectable;
        }

        /// <summary>from から step の向きで、次に選べる行。無ければ from（from が選べない行なら -1）</summary>
        int Next(int from, int step)
        {
            for (var r = from + step; r >= 0 && r < rows.Length; r += step)
                if (Usable(r)) return r;
            return Usable(from) ? from : -1;
        }

        /// <summary>上下。-1 で上、+1 で下。両端で止まり、小見出しは飛ばす</summary>
        public void MoveRow(int step)
        {
            if (step == 0) return;
            var n = Mathf.Abs(step);
            for (var i = 0; i < n; i++) Row = Next(Row, step > 0 ? 1 : -1);
        }

        /// <summary>カーソルが重なった行を選ぶ。小見出しなら何もしない</summary>
        public void HoverRow(int row)
        {
            if (Usable(row)) Row = row;
        }

        /// <summary>選んでいる行。選べる行が無ければ null</summary>
        public SettingRow Selected { get { return Usable(Row) ? rows[Row] : null; } }

        /// <summary>選んでいる、つまみの行が動かす値。つまみの行を選んでいなければ null</summary>
        public SettingDial Dial
        {
            get
            {
                var row = Selected;
                return row != null ? row.Dial : null;
            }
        }

        /// <summary>選んでいるつまみを step 刻みだけ動かす。両端で止まる。つまみの行でなければ false</summary>
        public bool Nudge(int step)
        {
            var dial = Dial;
            if (dial == null || step == 0) return false;
            dial.Nudge(step);
            return true;
        }

        /// <summary>「既定に戻す」を選んでいれば、全部の値を既定へ戻して true</summary>
        public bool Reset()
        {
            var row = Selected;
            if (row == null || row.Kind != SettingKind.Reset) return false;
            GameSettings.ResetAll();
            return true;
        }

        /// <summary>「戻る」を選んでいるか</summary>
        public bool AtBack
        {
            get
            {
                var row = Selected;
                return row != null && row.Kind == SettingKind.Back;
            }
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
