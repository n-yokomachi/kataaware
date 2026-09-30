using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 露店でチップを売るくだり。買い手ひとりぶんの形（去ったあとテーブルに残る枚数・女か・置いていく煙草）を持つ。
    /// 形と枚数を同じところに置いておかないと、暗転のたびにずれる。
    ///
    /// **台詞はここに持たない。** 台詞の原稿（docs/scenario/02-alley.md の「買い手A」〜「買い手C」と「締め」）を正とし、
    /// 写す道具（<c>HalfAware/Apply the scenario (alley)</c>、<see cref="AlleyManuscript"/>）が文面のアセット（AlleyScript）の
    /// <see cref="AlleyIds.Buyer"/>・<see cref="AlleyIds.Closing"/> へ書く。ここはアセットから引くだけ
    /// </summary>
    public static class MarketSale
    {
        /// <summary>はじめにテーブルへ置く枚数。場面 1 で抜いたぶん</summary>
        public const int Chips = 6;

        /// <summary>買い手ひとりぶん</summary>
        struct Buyer
        {
            /// <summary>この買い手が去ったあと、テーブルに残る枚数</summary>
            public int left;
            /// <summary>女か。ヒールの足音で歩いてくる（組み立てが <see cref="BuyerWalk"/> に写す）</summary>
            public bool woman;
            /// <summary>置く煙草の数。置かないなら 0</summary>
            public int smokes;

            public Buyer(int left, bool woman, int smokes)
            {
                this.left = left;
                this.woman = woman;
                this.smokes = smokes;
            }
        }

        /// <summary>原稿の「買い手A」「買い手B」「買い手C」の順</summary>
        static readonly Buyer[] buyers =
        {
            new Buyer(4, false, 0),
            // 煙草で払う常連。支払いの台詞（原稿の注記「上の行で、買い手Bが煙草をテーブルに置く」）で卓に置く
            new Buyer(2, false, 6),
            new Buyer(1, true, 0),
        };

        public static int Count { get { return buyers.Length; } }

        static readonly string[] NoLines = new string[0];

        /// <summary>i 人目とのやり取り。文面に無い・範囲の外なら空</summary>
        public static IReadOnlyList<string> Lines(RoomScript script, int i)
        {
            if (script == null || i < 0 || i >= buyers.Length) return NoLines;
            var entry = script.Find(AlleyIds.Buyer(i));
            return entry.id == null ? NoLines : entry.Lines;
        }

        /// <summary>締め。最後の買い手が去ってから出す。文面に無ければ空</summary>
        public static IReadOnlyList<string> Closing(RoomScript script)
        {
            if (script == null) return NoLines;
            var entry = script.Find(AlleyIds.Closing);
            return entry.id == null ? NoLines : entry.Lines;
        }

        /// <summary>
        /// i 人目が去ったあと、テーブルに残る枚数。
        /// 締めまで行けば売り切れて 0 になる
        /// </summary>
        public static int Left(int i)
        {
            if (i < 0) return Chips;
            if (i >= buyers.Length) return 0;
            return buyers[i].left;
        }

        /// <summary>i 人目は女か</summary>
        public static bool Woman(int i)
        {
            return i >= 0 && i < buyers.Length && buyers[i].woman;
        }

        /// <summary>
        /// i 人目が煙草を置く行。置かなければ null。この行が字幕に出たところで卓に煙草を出す。
        ///
        /// **その買い手の最後の台詞の行**（やり取りの頭の行と同じ話者の、いちばん後ろの行）。支払いを置いて去る所で、
        /// 原稿では「買い手B「はいはい、じゃあこれな」」の後に注記がある。行の文面は原稿から引くので、ここに台詞を書かない
        /// </summary>
        public static string PutsSmokes(RoomScript script, int i)
        {
            if (Smokes(i) <= 0) return null;
            return LastOwnLine(Lines(script, i));
        }

        /// <summary>lines の頭の行の話者が、いちばん後ろで話す行。頭の行に話者が無ければ null</summary>
        public static string LastOwnLine(IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count == 0) return null;
            var who = Speech.Who(lines[0]);
            if (string.IsNullOrEmpty(who)) return null;
            for (var k = lines.Count - 1; k >= 0; k--)
                if (Speech.Who(lines[k]) == who) return lines[k];
            return null;
        }

        /// <summary>i 人目が置く煙草の数</summary>
        public static int Smokes(int i)
        {
            return i >= 0 && i < buyers.Length ? buyers[i].smokes : 0;
        }

        /// <summary>
        /// i 人目が来る前に、卓に出ている煙草の数。前の買い手が置いていった物は、売り切れるまで卓に残る。
        /// 思い出して i 人目から続ける時に、卓をその形にするのに使う
        /// </summary>
        public static int SmokesBefore(int i)
        {
            var most = 0;
            for (var j = 0; j < i && j < buyers.Length; j++) most = System.Math.Max(most, buyers[j].smokes);
            return most;
        }

        /// <summary>i 人目が持っていく枚数</summary>
        public static int Took(int i)
        {
            return Left(i - 1) - Left(i);
        }
    }
}
