namespace HalfAware
{
    /// <summary>
    /// 村（<c>Village.unity</c>）を、場面 6（庭の記憶、夕方）として開くかを場面をまたいで渡す。
    ///
    /// 村は場面 6 と場面 9（車から降りる朝）が同じシーンを使う。シーンを読み込み直すと前の場面の物は何も残らないので、
    /// どちらで入ったかだけを静的に持ち越す（<see cref="DiveHandoff"/> と同じ作り）。
    /// 立てるのは、場面 5 のモニターの「潜る」（<see cref="RestDirector"/>）、場面 6 のセーブを思い出す時（<see cref="SaveFlow.Resume"/>）、
    /// デバッグの一覧の「庭の記憶」（<see cref="SceneMenu.Prepare"/>）。読むのは村に置いた <see cref="GardenMemoryDirector"/> で、
    /// 読んだら下ろす（<see cref="Take"/>）。立っていなければ、村は場面 9 のまま始まる
    /// </summary>
    public static class GardenHandoff
    {
        /// <summary>場面の番号</summary>
        public const int Stage = 6;

        /// <summary>次に村を読んだら場面 6 として始める</summary>
        public static bool Pending;

        /// <summary>いま場面 6 を流している。デバッグの一覧が「いまいる場面」を出すのに読む</summary>
        public static bool Active;

        /// <summary>立っていれば下ろして true。村の演出が Awake で一度だけ読む</summary>
        public static bool Take()
        {
            var was = Pending;
            Pending = false;
            return was;
        }

        /// <summary>初めから始め直すときに戻す</summary>
        public static void Clear()
        {
            Pending = false;
            Active = false;
        }
    }
}
