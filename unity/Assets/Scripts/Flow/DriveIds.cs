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

        /// <summary>帯 0 のきっかけ。助手席のメモリーチップの束</summary>
        public const string Chips = "drive.chips";
        /// <summary>帯 1 のきっかけ。アクセスログの写し</summary>
        public const string Log = "drive.log";
        /// <summary>帯 2 のきっかけ。ルームミラー</summary>
        public const string Mirror = "drive.mirror";
        /// <summary>帯 3 のきっかけ。メーターの脇の写真立て</summary>
        public const string Photo = "drive.photo";
        /// <summary>帯 4 のきっかけ。窓を開けると場面 9 へ</summary>
        public const string Window = "drive.window";

        static readonly string[] triggers = { Chips, Log, Mirror, Photo, Window };

        /// <summary>
        /// 帯の順に並べたきっかけの id。BuildDrive と WriteDriveScript が同じ並びを使う。
        /// 中身を書き換えられないよう読み取りだけで渡す（MarketSale と同じ構え）
        /// </summary>
        public static IReadOnlyList<string> Triggers { get { return triggers; } }

        /// <summary>ラジオ。帯を問わず置く、読んでも帯が進まない対象</summary>
        public const string Radio = "drive.radio";
        /// <summary>上着のポケット。同じく帯は進まない</summary>
        public const string Pocket = "drive.pocket";
        /// <summary>給油計。同じく帯は進まない</summary>
        public const string Fuel = "drive.fuel";

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
