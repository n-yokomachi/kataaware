using System.Text;

namespace HalfAware
{
    /// <summary>
    /// 場面 6（庭の記憶）の文字の値。右上の見出し・女性の板・名を呼ぶ一行
    /// （シナリオ設計書 10 節）。
    ///
    /// **片割れの名と年はまだ決めていない。仮の値をここ一か所に置く。** 決まったら <see cref="TwinName"/> と
    /// <see cref="TwinAge"/> を書き換える。見出し・板・名を呼ぶ一行はどれもここから組むので、ほかは直さなくてよい
    /// </summary>
    public static class GardenMemory
    {
        /// <summary>片割れ（記憶の主）の名。**仮**</summary>
        public const string TwinName = "ローラ";

        /// <summary>片割れの、この記憶の時の年。**仮**</summary>
        public const int TwinAge = 29;

        /// <summary>記憶の日時。原作の「ここ数年の記憶」からの仮置き（10.3 節）</summary>
        public const string Stamp = "2163/08/16 19:45";

        /// <summary>記憶の場所の名。コンソールの頭の行に出す</summary>
        public const string Place = "村";

        /// <summary>
        /// 女性（過去の主人公）の名の、文字化けした形。主人公は記憶を失くしているので、板の名は読めない（10.2 節の 4）。
        /// UTF-8 の字を Shift_JIS で読んだときに出る字の並びに似せた。**仮**
        /// </summary>
        public const string Garbled = "縺ｵ繧後°";

        /// <summary>名を呼ぶ一行の元の文。{0} に片割れの名が入る。字幕には崩した形（<see cref="Call"/>）しか出ない。**仮**</summary>
        public const string CallSource = "{0}、ちょっと待ってて。済んだら、お茶にしよう";

        /// <summary>右上の見出し。場面 4 の見出しと同じ書式（「女　6　『メイ』　2156/03/02 07:14」）</summary>
        public static string Row
        {
            get { return "女　" + TwinAge + "　『" + TwinName + "』　" + Stamp; }
        }

        /// <summary>
        /// 女性の板の一行目。板は名前の鉤括弧までを出す（<see cref="HoloPanel"/>）。
        /// 年は片割れと同じ（同じ日に生まれた二人）。名だけが化けている
        /// </summary>
        public static string WomanRow
        {
            get { return "女　" + TwinAge + "　『" + Garbled + "』"; }
        }

        /// <summary>名を呼ぶ一行の、字幕に出す崩した形。名前の行は出さない（話し手の名が無いので、字幕は台詞だけの形で出る）</summary>
        public static string Call
        {
            get { return "「" + Garble(string.Format(CallSource, TwinName), TwinName) + "」"; }
        }

        // ---- 字の崩し（仮） ---------------------------------------------------------
        //
        // 10.3 節「字の形は見えるがところどころ欠けて読めない形を仮に置く」。字ごとに、そのまま残す・画の欠けた字に替える・
        // 空ける、のどれかにする。どれにするかは字の並びの番号から決めるので、何度出しても同じ形になる。
        // **片割れの名の字は必ず空ける。** 聞き取れなかった言葉は片割れの名で、場面 10 で初めて聞こえる。
        // 残した字から名を読めてはいけない。
        //
        // 色や透明度の書式で欠けを描くと、字幕の折り返し（SubtitleBox.Wrap）が書式の字まで数えて途中で割るので、字だけで作る

        /// <summary>崩さない字。句読点と括弧と空白</summary>
        const string Kept = "、。！？…「」『』（）　 ";

        /// <summary>空けた所に置く字（全角の空白）</summary>
        const char Blank = '　';

        /// <summary>画の欠けた字。左の字から画を抜くと右の字に見える組</summary>
        static readonly string[,] Broken =
        {
            { "た", "ナ" }, { "な", "ナ" }, { "に", "こ" }, { "ほ", "は" }, { "ま", "よ" },
            { "て", "ー" }, { "う", "つ" }, { "ぐ", "く" }, { "お", "ぉ" }, { "よ", "ょ" },
            { "待", "寺" }, { "済", "斉" }, { "茶", "艹" }, { "見", "目" }, { "咲", "口" },
            { "ロ", "コ" }, { "ラ", "フ" }, { "リ", "ノ" }, { "ち", "ら" }, { "ょ", "ｮ" },
        };

        /// <summary>残す割合と、欠けた字に替える割合（残りは空ける）</summary>
        public const float KeepShare = 0.40f;
        public const float BreakShare = 0.30f;

        /// <summary>
        /// line を崩す。hidden（片割れの名）の字はすべて空け、句読点と括弧はそのまま、ほかの字は
        /// 残す・欠けた字に替える（替える字が無ければ空ける）・空ける、を字の番号で決める。長さは変えない
        /// </summary>
        public static string Garble(string line, string hidden)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            var nameAt = string.IsNullOrEmpty(hidden) ? -1 : line.IndexOf(hidden, System.StringComparison.Ordinal);
            var sb = new StringBuilder(line.Length);
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (Kept.IndexOf(c) >= 0) { sb.Append(c); continue; }
                if (nameAt >= 0 && i >= nameAt && i < nameAt + hidden.Length) { sb.Append(Blank); continue; }
                var h = Hash(i);
                if (h < KeepShare) { sb.Append(c); continue; }
                if (h < KeepShare + BreakShare)
                {
                    var b = BrokenOf(c);
                    sb.Append(b.Length > 0 ? b : Blank.ToString());
                    continue;
                }
                sb.Append(Blank);
            }
            return sb.ToString();
        }

        /// <summary>c の画の欠けた字。組が無ければ空</summary>
        public static string BrokenOf(char c)
        {
            var s = c.ToString();
            for (var i = 0; i < Broken.GetLength(0); i++)
                if (Broken[i, 0] == s) return Broken[i, 1];
            return string.Empty;
        }

        /// <summary>字の番号から 0〜1 の値。組み直しても同じ値になる</summary>
        static float Hash(int i)
        {
            unchecked
            {
                var h = (uint)(i * 747796405 + 2891336453);
                h = ((h >> (int)((h >> 28) + 4)) ^ h) * 277803737u;
                h = (h >> 22) ^ h;
                return (h % 1000u) / 1000f;
            }
        }
    }
}
