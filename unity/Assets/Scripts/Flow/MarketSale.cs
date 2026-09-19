using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 露店でチップを売るくだり。シナリオ 6.4 の台詞をそのまま持つ。
    /// 買い手ひとりぶんの台詞と、その買い手が去ったあとテーブルに残る枚数を対にする。
    /// 台詞と枚数を同じところに置いておかないと、暗転のたびにずれる
    /// </summary>
    public static class MarketSale
    {
        /// <summary>はじめにテーブルへ置く枚数。場面 1 で抜いたぶん</summary>
        public const int Chips = 6;

        /// <summary>買い手ひとりぶん</summary>
        public struct Buyer
        {
            /// <summary>順に流す台詞</summary>
            public string[] lines;
            /// <summary>この買い手が去ったあと、テーブルに残る枚数</summary>
            public int left;
        }

        static readonly Buyer[] buyers =
        {
            new Buyer
            {
                lines = new[]
                {
                    "買い手A「やあ、今日の品ぞろえは？」",
                    "私「男が3枚、女が3枚」",
                    "買い手A「若い女のはあるかな、なければ男」",
                    "私「どっちもあるよ、好きな方を持っていって」",
                    "買い手A「へっへ、いつも助かるよ。・・・OK、送金した。それじゃあ」",
                },
                left = 4,
            },
            new Buyer
            {
                lines = new[]
                {
                    "買い手B「よう、いつものあるか？」",
                    "私「ちゃんと取り置いてるよ、どうぞ」",
                    "買い手B「支払いもいつもので？」",
                    "私「ああ、助かるよ。丁度切らしちゃって」",
                    "買い手B「はいはい、じゃあこれな（煙草を2,3個テーブルに置く）」",
                    "私「どうも」",
                },
                left = 2,
            },
            new Buyer
            {
                lines = new[]
                {
                    "買い手C「初めてなんだけど、子どものはあるかしら？」",
                    "私「１枚なら。えっと、男の子、8歳、24分38秒」",
                    "買い手C「それでいいわ。支払いは・・・ああ、しまった、現金でもいい？」",
                    "私「ええ、いいですよ」",
                },
                left = 1,
            },
        };

        /// <summary>締め。最後の買い手が去ってから出す</summary>
        static readonly string[] closing =
        {
            "6枚あったメモリーチップは、それなりに高価な品であるにも関わらず30分後には売り切れた。これであと一週間は何もしなくても食っていける",
            "帰ろう",
        };

        public static int Count { get { return buyers.Length; } }

        public static IReadOnlyList<string> Closing { get { return closing; } }

        /// <summary>i 人目の台詞。範囲の外なら空</summary>
        public static IReadOnlyList<string> Lines(int i)
        {
            return i >= 0 && i < buyers.Length ? buyers[i].lines : new string[0];
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

        /// <summary>i 人目が持っていく枚数</summary>
        public static int Took(int i)
        {
            return Left(i - 1) - Left(i);
        }
    }
}
