namespace HalfAware
{
    /// <summary>
    /// 場面 2 の対象につける id。シーンを組む側（BuildAlley）と、
    /// 文面を原稿から写す側（AlleyManuscript）と、進行を動かす側（AlleyDirector）で
    /// 同じ綴りを使う。文字列を三か所に散らすと、どれかを直し忘れて黙る
    /// </summary>
    public static class AlleyIds
    {
        /// <summary>通りの看板の数。BuildAlley が立てる数（StreetSignCount）と、原稿の看板の見出しの数と揃える</summary>
        public const int Signs = 5;

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

        /// <summary>場面の頭の独白（原稿の「冒頭」）。明けきってから出す。今は原稿に行が無く、空</summary>
        public const string Opening = "street.opening";

        /// <summary>売り買いの i 人目の買い手とのやり取り（原稿の「買い手A」から順に）</summary>
        public static string Buyer(int i)
        {
            return "sale.buyer" + i;
        }

        /// <summary>売り買いの締め。最後の買い手が去ってから出す</summary>
        public const string Closing = "sale.closing";

        /// <summary>
        /// 演出が自分で出す文か（調べる対象が紐づかない）。看板の段・冒頭の独白・買い手とのやり取り・締め
        /// </summary>
        public static bool IsSaid(string id)
        {
            return IsPage(id) || id == Opening || id == Closing || (id != null && id.StartsWith("sale.buyer"));
        }

        /// <summary>文面のアセットの項目の並び。原稿を写す道具（HalfAware/Apply the scenario (alley)）がこの順に書く</summary>
        public static string[] All
        {
            get
            {
                var all = new System.Collections.Generic.List<string> { Opening };
                for (var i = 0; i < Signs; i++) all.Add(Sign(i));
                for (var i = 0; i < Signs; i++) all.Add(Page(i));
                all.Add(Board);
                all.Add(StallSign);
                all.Add(Table);
                for (var i = 0; i < MarketSale.Count; i++) all.Add(Buyer(i));
                all.Add(Closing);
                return all.ToArray();
            }
        }
    }
}
