namespace HalfAware
{
    /// <summary>
    /// 場面 2 の対象につける id。シーンを組む側（BuildAlley）と、
    /// 文面を書き出す側（WriteAlleyScript）と、進行を動かす側（AlleyDirector）で
    /// 同じ綴りを使う。文字列を三か所に散らすと、どれかを直し忘れて黙る
    /// </summary>
    public static class AlleyIds
    {
        /// <summary>通りの看板。i 枚目</summary>
        public static string Sign(int i)
        {
            return "street.sign" + i;
        }

        /// <summary>通りの看板を読んだときに出す段。i 段目</summary>
        public static string Page(int i)
        {
            return "street.page" + i;
        }

        /// <summary>通りの看板か。どれから読んでも同じ列から引くので、綴りだけで見分ける</summary>
        public static bool IsSign(string id)
        {
            return id != null && id.StartsWith("street.sign");
        }

        /// <summary>
        /// 看板の id から何枚目かを取る。看板でなければ -1。
        /// 板と地の文はこの番号で 1 対 1 に紐づく
        /// </summary>
        public static int SignNumber(string id)
        {
            if (!IsSign(id)) return -1;
            int n;
            return int.TryParse(id.Substring("street.sign".Length), out n) ? n : -1;
        }

        /// <summary>看板を読んだときに出す段か。段には調べる対象が紐づかない</summary>
        public static bool IsPage(string id)
        {
            return id != null && id.StartsWith("street.page");
        }

        /// <summary>小路の口の表示板</summary>
        public const string Board = "yard.board";

        /// <summary>自分の露店の看板</summary>
        public const string StallSign = "stall.sign";

        /// <summary>自分の露店のテーブル。場面 2 の必須</summary>
        public const string Table = "stall.table";
    }
}
