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

    /// <summary>
    /// コンソールのボタンの選ぶ・決めると、デバッグの場面の一覧の選び。見せ方は持たない（<see cref="ImplantConsole"/>）。
    ///
    /// 左右で選び、決めると <see cref="ConsoleAction"/> を返す。デバッグを決めると場面の一覧を開き、
    /// もう一度決めるか、ほかのボタンへ移ると閉じる。
    /// 記憶する・思い出す・目を閉じるは、ボタンだけ先に置いてある。中身は設計書 5 節が決まってから
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

        /// <summary>中身がまだ無いボタンを押したときに添える字</summary>
        public const string NotYet = "まだ使えない";

        /// <summary>いま選んでいるボタン</summary>
        public int Index { get; private set; }

        public ConsoleAction Selected { get { return (ConsoleAction)Index; } }

        /// <summary>デバッグの場面の一覧を出しているか</summary>
        public bool Listing { get; private set; }

        /// <summary>場面の一覧で選んでいる行。<see cref="SceneMenu.Scenes"/> の番号（0 始まり）</summary>
        public int Row { get; private set; }

        /// <summary>開いたときの形。いちばん左を選び、一覧は閉じておく</summary>
        public void Reset()
        {
            Index = 0;
            Listing = false;
            Row = 0;
        }

        /// <summary>左右の入力。-1 で左、+1 で右。両端で止まる。デバッグから離れたら一覧を閉じる</summary>
        public void Move(int step)
        {
            if (step == 0) return;
            Index = Mathf.Clamp(Index + step, 0, Labels.Length - 1);
            if (Selected != ConsoleAction.Debug) Listing = false;
        }

        /// <summary>カーソルが重なったボタンを選ぶ。一覧は閉じない（一覧へ手を運ぶ途中で消えると困る）</summary>
        public void Hover(int index)
        {
            if (index < 0 || index >= Labels.Length) return;
            Index = index;
        }

        /// <summary>
        /// 決める。デバッグなら一覧を開け閉めし、開いたときは here の場面の行を選んでおく。
        /// ほかのボタンなら一覧を閉じる
        /// </summary>
        public ConsoleAction Decide(string here)
        {
            var action = Selected;
            if (action == ConsoleAction.Debug)
            {
                Listing = !Listing;
                if (Listing) Row = Mathf.Max(0, System.Array.IndexOf(SceneMenu.Scenes, here));
            }
            else Listing = false;
            return action;
        }

        /// <summary>一覧の上下。-1 で上、+1 で下。両端で止まる</summary>
        public void MoveRow(int step)
        {
            if (!Listing || step == 0) return;
            Row = Mathf.Clamp(Row + step, 0, SceneMenu.Count - 1);
        }

        /// <summary>カーソルが重なった一覧の行を選ぶ</summary>
        public void HoverRow(int row)
        {
            if (!Listing || row < 0 || row >= SceneMenu.Count) return;
            Row = row;
        }

        /// <summary>一覧で選んでいる行の移り先。今いる場面なら null（<see cref="SceneMenu.Target"/>）</summary>
        public string RowTarget(string here)
        {
            return Listing ? SceneMenu.Target(Row + 1, here) : null;
        }

        /// <summary>一段戻る。一覧を出していれば閉じて true。出していなければ false（コンソールを閉じる）</summary>
        public bool Back()
        {
            if (!Listing) return false;
            Listing = false;
            return true;
        }

        /// <summary>中身がまだ無いボタンを押したときの知らせ。デバッグなら null</summary>
        public static string Note(ConsoleAction action)
        {
            if (action == ConsoleAction.Debug) return null;
            return Labels[(int)action] + "　―　" + NotYet;
        }
    }
}
