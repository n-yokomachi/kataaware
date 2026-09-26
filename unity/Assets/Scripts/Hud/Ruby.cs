using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ルビ。TextMeshPro にルビの仕組みは無いので、書式の指定だけで組む。
    ///
    /// 小さくした字を持ち上げて先に置き、進んだぶんちょうど戻してから親字を重ねる。
    /// 戻す量を「親字の幅」ではなく「実際に進んだ幅」にするのが肝で、
    /// ここを取り違えるとルビの長さ次第で親字がずれる。
    /// ルビの真ん中は親字の真ん中に揃える（広いルビは両脇の字の上へ掛ける、<see cref="Hang"/>）。
    ///
    /// 幅は ListFormat.Units（半角いくつぶん）で測る。全角 1 字 = 2、半角 1 字 = 1。
    /// em になおすと半分なので、ルビの進む幅は Units / 2 × Scale
    /// </summary>
    public static class Ruby
    {
        /// <summary>
        /// ルビの大きさ。親字に対する割合。
        ///
        /// **粗い画面の中で読める大きさにする。** 字幕の台詞は粗い画面（<see cref="UiLens"/>、0.75）の中で 13 画素。
        /// 半分（6.5 画素）では仮名が潰れて読めなかった（オーナー、2026-09-27）。
        /// 0.7 で 9 画素。字の下限（10 画素ほど、設計書 2.5 節）の少し下だが、親字より一回り小さいことは保つ
        /// </summary>
        public const float Scale = 0.7f;

        // ---- TMP に並べさせて測った Noto Sans JP の寸法（em）---------------------
        //
        // 字の形の外枠（characterInfo の上下）をベースラインから測った。2026-09-27

        /// <summary>漢字の上の端。ベースラインから</summary>
        public const float BaseTop = 0.862f;
        /// <summary>漢字の下の端。ベースラインから下へ</summary>
        public const float BaseBottom = 0.103f;
        /// <summary>仮名の上の端。ルビの字の em で</summary>
        public const float RubyTop = 0.843f;
        /// <summary>小さい仮名（ィ）の下の端が、ベースラインから下へ出るぶん。ルビの字の em で</summary>
        public const float RubyDip = 0.1f;
        /// <summary>ルビと親字、ルビと上の行の親字のあいだに空ける隙間</summary>
        public const float Gap = 0.06f;
        /// <summary>素の行送り</summary>
        public const float Advance = 1.448f;

        /// <summary>
        /// 持ち上げる高さ。親字の em に対する割合。
        /// 漢字の上の端（<see cref="BaseTop"/>）に隙間を空け、ルビの字がベースラインの下へ出るぶん（<see cref="RubyDip"/>）を足す
        /// </summary>
        public const float Lift = BaseTop + Gap + RubyDip * Scale;

        /// <summary>
        /// ルビのある文に足す行間。字の大きさに対する百分率。
        ///
        /// ルビの上の端はベースラインから <see cref="Lift"/> + <see cref="RubyTop"/> × <see cref="Scale"/> em になる。
        /// 素の行送り（<see cref="Advance"/>）のままだと、下の行のルビが上の行の親字（下の端 <see cref="BaseBottom"/>）にかぶる。
        /// かぶらずに隙間が空くまで送る
        /// </summary>
        public const float ExtraLineSpacing = (Lift + RubyTop * Scale + BaseBottom + Gap - Advance) * 100f;

        /// <summary>
        /// ルビが親字より広いとき、両脇の字の上へ掛けてよい幅。em（親字の）。片側の値。
        ///
        /// ルビは親字の上の端より上に浮いているので、両脇の字の上へ掛けてもかぶらない。
        /// はみ出すぶんを全部、親字の後ろの空きにしていた頃は、ルビを大きくすると「千葉市　　や」「羅府　　　に」と
        /// 親字の後ろにだけ大きな穴が空いた。ルビは親字の真ん中に揃え、掛けきれないぶんだけ親字の前後を同じだけ空ける
        /// </summary>
        public const float Hang = 0.5f;

        /// <summary>
        /// text にルビを振る。どちらかが空なら text をそのまま返す。
        /// ルビの真ん中を親字の真ん中に揃える。ルビが親字より広いときは、両脇の字の上へ <see cref="Hang"/> まで掛け、
        /// それでもはみ出すぶんだけ親字の前後を空けて、次の字のルビと重ならないようにする
        /// </summary>
        public static string Over(string text, string ruby)
        {
            return Over(text, ruby, Scale, Lift, false);
        }

        /// <summary>
        /// 大きさ scale と持ち上げる高さ lift を渡して振る。大きさを比べて撮るときに使う。
        ///
        /// lineStart は、親字が行の頭にあるか。**行の頭では、広いルビを前へ掛けない。** 前に字が無いので、
        /// 掛けるとルビだけが行の頭より左へ出て、名前の行や上の行と頭が揃わなくなる。
        /// そのときはルビの頭を親字の頭に揃え、後ろの字の上へだけ掛ける
        /// </summary>
        public static string Over(string text, string ruby, float scale, float lift, bool lineStart)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(ruby)) return text ?? string.Empty;
            if (scale <= 0f) return text;

            var baseEm = ListFormat.Units(text) * 0.5f;
            var rubyEm = ListFormat.Units(ruby) * 0.5f * scale;
            if (baseEm <= 0f || rubyEm <= 0f) return text;

            float pre, start, post;
            if (rubyEm <= baseEm)
            {
                // 狭いルビは親字の真ん中へ寄せる
                pre = 0f;
                start = (baseEm - rubyEm) * 0.5f;
                post = 0f;
            }
            else if (lineStart)
            {
                pre = 0f;
                start = 0f;
                post = Mathf.Max(0f, rubyEm - baseEm - Hang);
            }
            else
            {
                var over = (rubyEm - baseEm) * 0.5f;    // ルビが親字から片側へはみ出すぶん
                pre = Mathf.Max(0f, over - Hang);       // 掛けきれず、親字の前後に空けるぶん
                post = pre;
                start = pre - over;                     // ルビの頭（負なら前の字の上へ掛かる）
            }
            var back = start + rubyEm - pre;            // ルビの尻から親字の頭まで戻すぶん

            var made = new System.Text.StringBuilder();
            // ルビの外に置く <space> は親字の em で効く
            if (Mathf.Abs(start) > 0.001f) made.Append(Tag("<space=", start, "em>"));
            made.Append(Tag("<voffset=", lift, "em>"));
            made.Append("<size=").Append(Num(scale * 100f)).Append("%>");
            made.Append(ruby);
            made.Append("</size></voffset>");
            made.Append(Tag("<space=", -back, "em>"));
            made.Append(text);
            if (post > 0.001f) made.Append(Tag("<space=", post, "em>"));
            return made.ToString();
        }

        static string Tag(string open, float value, string close)
        {
            return open + Num(value) + close;
        }

        static string Num(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---- 文面への書き方 --------------------------------------------

        /// <summary>親字の頭。青空文庫の記法に合わせてある</summary>
        public const char Head = '｜';
        /// <summary>ルビの頭</summary>
        public const char Open = '《';
        /// <summary>ルビの尻</summary>
        public const char Shut = '》';

        /// <summary>
        /// 文面には「｜親字《るび》」の形で書いておく。
        /// 書式の指定をそのまま持たせると、字幕の折り返しがタグを字数に数えてしまう。
        /// 折り返してから Expand で書式に直す
        /// </summary>
        public static string Expand(string text)
        {
            return Expand(text, Scale, Lift);
        }

        /// <summary>大きさ scale と持ち上げる高さ lift を渡して直す。大きさを比べて撮るときに使う</summary>
        public static string Expand(string text, float scale, float lift)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Head) < 0) return text;
            var made = new System.Text.StringBuilder(text.Length + 64);
            var i = 0;
            while (i < text.Length)
            {
                int baseFrom, baseTo, rubyFrom, rubyTo;
                if (!Group(text, i, out baseFrom, out baseTo, out rubyFrom, out rubyTo))
                {
                    made.Append(text[i]);
                    i++;
                    continue;
                }
                var lineStart = i == 0 || text[i - 1] == '\n';
                made.Append(Over(text.Substring(baseFrom, baseTo - baseFrom),
                                 text.Substring(rubyFrom, rubyTo - rubyFrom), scale, lift, lineStart));
                i = rubyTo + 1;
            }
            return made.ToString();
        }

        /// <summary>ルビを落として親字だけにする。ログや照合に使う</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Head) < 0) return text;
            var made = new System.Text.StringBuilder(text.Length);
            var i = 0;
            while (i < text.Length)
            {
                int baseFrom, baseTo, rubyFrom, rubyTo;
                if (!Group(text, i, out baseFrom, out baseTo, out rubyFrom, out rubyTo))
                {
                    made.Append(text[i]);
                    i++;
                    continue;
                }
                made.Append(text, baseFrom, baseTo - baseFrom);
                i = rubyTo + 1;
            }
            return made.ToString();
        }

        /// <summary>
        /// at から始まるルビの指定を読む。「｜親字《るび》」の形だけを見る。
        /// 親字もルビも空でないときだけ真
        /// </summary>
        public static bool Group(string text, int at, out int baseFrom, out int baseTo, out int rubyFrom, out int rubyTo)
        {
            baseFrom = baseTo = rubyFrom = rubyTo = -1;
            if (at >= text.Length || text[at] != Head) return false;
            var open = text.IndexOf(Open, at + 1);
            if (open < 0 || open == at + 1) return false;
            var shut = text.IndexOf(Shut, open + 1);
            if (shut < 0 || shut == open + 1) return false;
            // 親字のあいだに別の指定が挟まっていたら、それは書き損じ
            if (text.IndexOf(Head, at + 1, open - at - 1) >= 0) return false;
            baseFrom = at + 1;
            baseTo = open;
            rubyFrom = open + 1;
            rubyTo = shut;
            return true;
        }
    }
}
