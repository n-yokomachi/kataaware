using UnityEngine;

namespace HalfAware
{
    /// <summary>コンソールのボタン。並びの順</summary>
    public enum ConsoleAction
    {
        /// <summary>記憶する（セーブ）</summary>
        Remember,

        /// <summary>思い出す（ロード）</summary>
        Recall,

        /// <summary>目を閉じる（タイトルへ戻る）</summary>
        CloseEyes,

        /// <summary>デバッグ。場面の一覧を出す</summary>
        Debug,
    }

    /// <summary>ボタンの下に開く枠。ログの枠の中の左上に重ねる</summary>
    public enum ConsolePanel
    {
        None,
        /// <summary>記憶する。手動の 3 つから書く所を選ぶ</summary>
        Remember,
        /// <summary>思い出す。自動と手動の 4 つから読む物を選ぶ。空きは選べない</summary>
        Recall,
        /// <summary>デバッグの場面の一覧</summary>
        Scenes,
    }

    /// <summary>
    /// コンソールのボタンの選ぶ・決めると、ボタンの下に開く枠（記憶する・思い出す・デバッグ）の行の選び。
    /// 見せ方は持たない（<see cref="ImplantConsole"/>）。
    ///
    /// 左右で選び、決めると <see cref="ConsoleAction"/> を返す。枠を持つボタンを決めると枠を開き、
    /// もう一度決めるか、ほかのボタンへ左右で移ると閉じる。枠を開いている間は、上下で行を選ぶ
    /// </summary>
    public sealed class ConsoleMenu
    {
        public const string Remember = "記憶する";
        public const string Recall = "思い出す";
        /// <summary>タイトルへ戻る。名前はここだけで持つ</summary>
        public const string CloseEyes = "目を閉じる";
        public const string DebugLabel = "デバッグ";

        /// <summary>ボタンの字。<see cref="ConsoleAction"/> と同じ並び</summary>
        public static readonly string[] Labels = { Remember, Recall, CloseEyes, DebugLabel };

        /// <summary>記憶する・思い出すの行の数</summary>
        public const int RememberRows = 3;
        public const int RecallRows = 4;

        // ---- 知らせ（頭の行の真ん中に少し出す） -----------------------------

        public const string Remembered = "記憶した";
        /// <summary>場面の頭が無い所（タイトルの画面や、表に無い場面）</summary>
        public const string CannotRemember = "ここでは記憶できない";
        public const string CannotRecall = "思い出せない";
        public const string NoTitle = "タイトルの画面が無い";

        /// <summary>いま選んでいるボタン</summary>
        public int Index { get; private set; }

        public ConsoleAction Selected { get { return (ConsoleAction)Index; } }

        /// <summary>いま開いている枠</summary>
        public ConsolePanel Panel { get; private set; }

        /// <summary>デバッグの場面の一覧を出しているか</summary>
        public bool Listing { get { return Panel == ConsolePanel.Scenes; } }

        /// <summary>
        /// 枠で選んでいる行（0 始まり）。場面の一覧なら <see cref="SceneMenu.Scenes"/> の番号、
        /// 記憶するなら手動の 1〜3、思い出すなら自動・1・2・3。選べる行が無ければ -1
        /// </summary>
        public int Row { get; private set; }

        readonly bool[] filled = new bool[RecallRows];

        /// <summary>思い出すの行（自動・1・2・3）のうち、読める物。コンソールが枠を開く前に入れる</summary>
        public void Fill(SaveSlot slot, bool usable)
        {
            var i = (int)slot;
            if (i >= 0 && i < filled.Length) filled[i] = usable;
        }

        public bool Filled(int row)
        {
            return row >= 0 && row < filled.Length && filled[row];
        }

        /// <summary>開いたときの形。いちばん左を選び、枠は閉じておく</summary>
        public void Reset()
        {
            Index = 0;
            Panel = ConsolePanel.None;
            Row = 0;
        }

        /// <summary>左右の入力。-1 で左、+1 で右。両端で止まる。枠のボタンから離れたら枠を閉じる</summary>
        public void Move(int step)
        {
            if (step == 0) return;
            Index = Mathf.Clamp(Index + step, 0, Labels.Length - 1);
            if (PanelOf(Selected) != Panel) Panel = ConsolePanel.None;
        }

        /// <summary>カーソルが重なったボタンを選ぶ。枠は閉じない（枠へ手を運ぶ途中で消えると困る）</summary>
        public void Hover(int index)
        {
            if (index < 0 || index >= Labels.Length) return;
            Index = index;
        }

        /// <summary>ボタンが開く枠。枠を持たないボタンは None</summary>
        public static ConsolePanel PanelOf(ConsoleAction action)
        {
            switch (action)
            {
                case ConsoleAction.Remember: return ConsolePanel.Remember;
                case ConsoleAction.Recall: return ConsolePanel.Recall;
                case ConsoleAction.Debug: return ConsolePanel.Scenes;
                default: return ConsolePanel.None;
            }
        }

        /// <summary>
        /// 決める。枠を持つボタンなら枠を開け閉めする。開いたときは、場面の一覧なら here の場面の行、
        /// 記憶するなら 1、思い出すならいちばん上の読める行を選んでおく。枠を持たないボタンなら枠を閉じる
        /// </summary>
        public ConsoleAction Decide(string here)
        {
            var action = Selected;
            var want = PanelOf(action);
            if (want == ConsolePanel.None || want == Panel)
            {
                Panel = ConsolePanel.None;
                return action;
            }
            Panel = want;
            switch (want)
            {
                case ConsolePanel.Scenes: Row = Mathf.Max(0, System.Array.IndexOf(SceneMenu.Scenes, here)); break;
                case ConsolePanel.Recall: Row = Next(-1, 1); break;
                default: Row = 0; break;
            }
            return action;
        }

        /// <summary>いま開いている枠の行の数</summary>
        public int Rows
        {
            get
            {
                switch (Panel)
                {
                    case ConsolePanel.Scenes: return SceneMenu.Count;
                    case ConsolePanel.Remember: return RememberRows;
                    case ConsolePanel.Recall: return RecallRows;
                    default: return 0;
                }
            }
        }

        /// <summary>その行を選べるか。思い出すの空きは選べない</summary>
        public bool Usable(int row)
        {
            if (row < 0 || row >= Rows) return false;
            return Panel != ConsolePanel.Recall || Filled(row);
        }

        /// <summary>from から step の向きで、次に選べる行。無ければ from（from が選べない行なら -1）</summary>
        int Next(int from, int step)
        {
            for (var r = from + step; r >= 0 && r < Rows; r += step)
                if (Usable(r)) return r;
            return Usable(from) ? from : -1;
        }

        /// <summary>枠の上下。-1 で上、+1 で下。両端で止まる。思い出すでは空きを飛ばす</summary>
        public void MoveRow(int step)
        {
            if (Panel == ConsolePanel.None || step == 0) return;
            var n = Mathf.Abs(step);
            for (var i = 0; i < n; i++) Row = Next(Row, step > 0 ? 1 : -1);
        }

        /// <summary>カーソルが重なった枠の行を選ぶ。選べない行なら何もしない</summary>
        public void HoverRow(int row)
        {
            if (!Usable(row)) return;
            Row = row;
        }

        /// <summary>一覧で選んでいる行の移り先。今いる場面なら null（<see cref="SceneMenu.Target"/>）</summary>
        public string RowTarget(string here)
        {
            return Listing ? SceneMenu.Target(Row + 1, here) : null;
        }

        /// <summary>記憶する・思い出すで選んでいる置き場。選んでいなければ null</summary>
        public SaveSlot? RowSlot
        {
            get
            {
                if (!Usable(Row)) return null;
                if (Panel == ConsolePanel.Remember) return (SaveSlot)(Row + 1);
                if (Panel == ConsolePanel.Recall) return (SaveSlot)Row;
                return null;
            }
        }

        /// <summary>一段戻る。枠を出していれば閉じて true。出していなければ false（コンソールを閉じる）</summary>
        public bool Back()
        {
            if (Panel == ConsolePanel.None) return false;
            Panel = ConsolePanel.None;
            return true;
        }
    }
}
