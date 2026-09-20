using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 場面 8 の対象につける id。シーンを組む側（BuildDrive）と、
    /// 文面を書き出す側（WriteDriveScript）と、進行を動かす側（DriveDirector）で
    /// 同じ綴りを使う。文字列を三か所に散らすと、どれかを直し忘れて黙る。
    ///
    /// 帯はここでは 0 から数える。設計書が「帯 1」と呼ぶものが 0 番にあたる
    /// </summary>
    public static class DriveIds
    {
        /// <summary>ガレージ。運転席のドアを調べると乗り込む</summary>
        public const string Door = "garage.door";
        /// <summary>ガレージ。シャッターの開閉ボタン。読むだけで、何も動かない</summary>
        public const string Button = "garage.button";

        /// <summary>倫敦の市街のきっかけ。助手席のメモリーチップの束</summary>
        public const string Chips = "drive.chips";
        /// <summary>
        /// 夜の高速のきっかけ。ダッシュボードの上の煙草。
        /// 調べると火を点けて一服し、窓を開けてから独白が出る
        /// </summary>
        public const string Cigar = "drive.cigar";
        /// <summary>朝靄の未舗装路のきっかけ。窓を開けると場面 9 へ</summary>
        public const string Window = "drive.window";

        static readonly string[] triggers = { Chips, Cigar, Window };

        /// <summary>
        /// 帯の順に並べたきっかけの id。BuildDrive と WriteDriveScript が同じ並びを使う。
        /// 中身を書き換えられないよう読み取りだけで渡す（MarketSale と同じ構え）
        /// </summary>
        public static IReadOnlyList<string> Triggers { get { return triggers; } }

        // **車内に任意の対象は置かない。** ラジオも燃料計も上着のポケットも、
        // 物としては車内に残っているが調べられない。走っているあいだ調べられるのは
        // その景色のきっかけひとつだけで、任意の対象はガレージの garage.button だけになる

        /// <summary>i 番目の帯で出す独白の段</summary>
        public static string Page(int i)
        {
            return "drive.band" + i;
        }

        /// <summary>独白の段か。段には調べる対象が紐づかない</summary>
        public static bool IsPage(string id)
        {
            return id != null && id.StartsWith("drive.band");
        }
    }
}
