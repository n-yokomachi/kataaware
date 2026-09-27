namespace HalfAware
{
    /// <summary>
    /// 村（<c>Village.unity</c>）を、場面 10（対面）の頭（卓の前）から開くかを場面をまたいで渡す（<see cref="GardenHandoff"/> と同じ作り）。
    ///
    /// 場面 9 と 10 は同じ村の朝で、ふだんは読み込みを挟まずに続く（卓の前に立つと <see cref="ReunionDirector"/> が場面 10 に替える）。
    /// 村を読み直して場面 10 から始めるのは、場面 10 のセーブを思い出す時（<see cref="SaveFlow.Resume"/>）と、
    /// デバッグの一覧の「対面」（<see cref="SceneMenu.Prepare"/>）だけ。読むのは村に置いた <see cref="ReunionDirector"/> で、読んだら下ろす
    /// </summary>
    public static class ReunionHandoff
    {
        /// <summary>場面の番号</summary>
        public const int Stage = 10;

        /// <summary>次に村を読んだら場面 10 の頭（卓の前）から始める</summary>
        public static bool Pending;

        /// <summary>いま場面 10 を流している。セーブの流れが場面の番号を決めるのと、デバッグの一覧が「いまいる場面」を出すのに読む</summary>
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
