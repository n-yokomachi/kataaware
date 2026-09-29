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
    /// 真ん中を揃える幅は、字の送りの実寸（<see cref="Em"/>、Noto Sans JP の送り）で測る。ルビの進む幅は Em(ルビ) × Scale。
    /// 前は半角 1 字 = 0.5 em と数えていたので、英字の親字（大文字は 0.6〜0.8 em）の上のルビが半文字ほど左へ寄った（2026-09-29）。
    /// 表の列の幅（<see cref="Width"/>）は今も半角いくつで数える（列の幅は、出す側が親字の実寸を測って広い方を取る、<see cref="ListFormat"/>）
    ///
    /// 傍点（圏点）も同じ仕組みで、字ごとに点を乗せる（<see cref="DotGlyph"/>）。
    /// 文面への書き方は「｜親字《るび》」と、台詞の原稿の「親字&lt;るび&gt;」「&lt;dot&gt;…&lt;/dot&gt;」の二つ（<see cref="Normalize"/>）
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

        /// <summary>ルビの大きさ scale で振る時の、持ち上げる高さ。<see cref="Lift"/> と同じ式（リストの枠は字幕より大きなルビを振る、<see cref="ListLayout.RubyScale"/>）</summary>
        public static float LiftFor(float scale)
        {
            return BaseTop + Gap + RubyDip * scale;
        }

        /// <summary>ルビの大きさ scale で振る時に足す行間。字の大きさに対する百分率。<see cref="ExtraLineSpacing"/> と同じ式</summary>
        public static float SpacingFor(float scale)
        {
            return (LiftFor(scale) + RubyTop * scale + BaseBottom + Gap - Advance) * 100f;
        }

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
            return Over(text, ruby, scale, lift, lineStart, 0f);
        }

        /// <summary>
        /// 親字が &lt;size=数&gt; の中にある時は、その大きさ size（0 なら無し）を渡す。ルビの大きさを数で書く。
        ///
        /// **TextMeshPro の &lt;size=70%&gt; は、外側の &lt;size&gt; ではなく字の既定の大きさに対して効く。**
        /// カード（既定 40、&lt;size=30&gt; で包む）で 70% と書くと、ルビが 21 ではなく 28 になり、戻す幅がずれて親字が右へずれた（2026-09-28）。
        /// &lt;space&gt; と &lt;voffset&gt; の em は今の大きさで効くので、そちらはそのまま
        /// </summary>
        public static string Over(string text, string ruby, float scale, float lift, bool lineStart, float size)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(ruby)) return text ?? string.Empty;
            if (scale <= 0f) return text;

            var baseEm = Em(text);
            var rubyEm = Em(ruby) * scale;
            if (baseEm <= 0f || rubyEm <= 0f) return text;

            float pre, start, post;
            Place(baseEm, rubyEm, lineStart, out pre, out start, out post);
            var back = start + rubyEm - pre;            // ルビの尻から親字の頭まで戻すぶん

            var made = new System.Text.StringBuilder();
            // ルビの外に置く <space> は親字の em で効く
            if (Mathf.Abs(start) > 0.001f) made.Append(Tag("<space=", start, "em>"));
            made.Append(Tag("<voffset=", lift, "em>"));
            if (size > 0f) made.Append("<size=").Append(Num(scale * size)).Append(">");
            else made.Append("<size=").Append(Num(scale * 100f)).Append("%>");
            made.Append(ruby);
            made.Append("</size></voffset>");
            made.Append(Tag("<space=", -back, "em>"));
            made.Append(text);
            if (post > 0.001f) made.Append(Tag("<space=", post, "em>"));
            return made.ToString();
        }

        /// <summary>
        /// 親字の幅 baseEm とルビの幅 rubyEm（どちらも親字の em）から、ルビの置き方を決める。
        /// pre は親字の前に空けるぶん、start はルビの頭（親字の前の所から。負なら前の字の上へ掛かる）、post は親字の後ろに空けるぶん
        /// </summary>
        static void Place(float baseEm, float rubyEm, bool lineStart, out float pre, out float start, out float post)
        {
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
        }

        // ---- 字の送り ------------------------------------------------------------

        /// <summary>
        /// 半角の英数と約物（U+0020〜U+007E）の送り。1000 分の 1 em。
        /// Noto Sans JP（Assets/Fonts/NotoSansJP-Regular.otf）の hmtx から写した（2026-09-29）。
        /// TMP の字の送り（フォントのアセットは同じフォントから作ってある）と、字の組の詰め（カーニング）を除いて一致する。
        /// 詰めは「To」で 0.06 em ほどで、真ん中を揃えるには効かないので数えない
        /// </summary>
        static readonly short[] Ascii =
        {
            224, 323, 474, 555, 555, 921, 680, 278, 338, 338, 467, 555, 278, 347, 278, 392,     //  !"#$%&'()*+,-./
            555, 555, 555, 555, 555, 555, 555, 555, 555, 555, 278, 278, 555, 555, 555, 474,     // 0123456789:;<=>?
            946, 608, 657, 638, 688, 589, 552, 689, 728, 293, 535, 646, 543, 812, 723, 742,     // @ABCDEFGHIJKLMNO
            633, 742, 635, 596, 599, 721, 575, 878, 573, 531, 603, 338, 392, 338, 555, 559,     // PQRSTUVWXYZ[\]^_
            606, 563, 618, 510, 620, 554, 325, 564, 607, 275, 275, 552, 284, 926, 610, 606,     // `abcdefghijklmno
            620, 620, 388, 468, 377, 607, 521, 802, 498, 521, 475, 338, 270, 338, 555,          // pqrstuvwxyz{|}~
        };

        /// <summary>
        /// text を並べた幅。親字の em で。**書式もルビの書き方もそのまま字として数える**（出る字だけを渡す）。
        /// 半角の英数と約物は Noto Sans JP の送り（<see cref="Ascii"/>）、全角の字（漢字・仮名・全角の約物）は 1 em、
        /// 半角の片仮名は 0.5 em、ほかの字（アクセント付きの英字など）は英字の並みの 0.56 em
        /// </summary>
        public static float Em(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var n = 0f;
            for (var i = 0; i < text.Length; i++) n += Em(text[i]);
            return n;
        }

        /// <summary>字 c の送り。em</summary>
        public static float Em(char c)
        {
            if (c >= ' ' && c <= '~') return Ascii[c - ' '] * 0.001f;
            if (c == '\n' || c == '\r' || c == '\u200B' || c == '\u2060') return 0f;
            // 和文の組版で全角に数える約物のうち、Noto Sans JP では詰めてある物（ListFormat.Units は全角に数える）
            switch (c)
            {
                case '‘': case '’': return 0.278f;
                case '“': case '”': return 0.474f;
                case '–': return 0.536f;
                case '—': return 0.894f;
                case '·': return 0.561f;
            }
            if (c >= 'ｦ' && c <= 'ﾟ') return 0.5f;
            if (char.IsLowSurrogate(c)) return 0f;          // 対の後ろ。前の字で 1 em 数えた
            if (char.IsHighSurrogate(c)) return 1f;
            return ListFormat.Units(c.ToString()) == 2 ? 1f : 0.56f;
        }

        /// <summary>
        /// 1 行 line（原稿の書き方か、前からの書き方）を <see cref="Expand(string,float,float,bool)"/> で組んだ時の、進む幅（em）。
        /// ルビが行の頭より前へ掛かるぶんを lead に、行の尻より後ろへ出るぶんを tail に返す（どちらも 0 以上）。
        /// 行を真ん中に置く時（看板の枠、<see cref="ListFormat.ComposeSign"/>）に、ルビまで含めた幅で寄せるのに使う
        /// </summary>
        public static float Span(string line, float scale, bool centred, out float lead, out float tail)
        {
            lead = 0f;
            tail = 0f;
            line = Normalize(line);
            if (string.IsNullOrEmpty(line)) return 0f;
            var x = 0f;
            var right = 0f;
            var i = 0;
            while (i < line.Length)
            {
                var tag = TagEnd(line, i);
                if (tag > i)
                {
                    i = tag + 1;
                    continue;
                }
                int baseFrom, baseTo, rubyFrom, rubyTo;
                if (Group(line, i, out baseFrom, out baseTo, out rubyFrom, out rubyTo))
                {
                    var baseEm = Em(line.Substring(baseFrom, baseTo - baseFrom));
                    var ruby = line.Substring(rubyFrom, rubyTo - rubyFrom);
                    var dot = ruby == DotMark;
                    var rubyEm = dot ? Em(DotGlyph) * DotScale : Em(ruby) * scale;
                    float pre, start, post;
                    Place(baseEm, rubyEm, !dot && !centred && LineStart(line, i), out pre, out start, out post);
                    lead = Mathf.Max(lead, -(x + start));
                    right = Mathf.Max(right, x + start + rubyEm);
                    x += pre + baseEm + post;
                    i = rubyTo + 1;
                    continue;
                }
                x += Em(line[i]);
                i++;
            }
            tail = Mathf.Max(0f, right - x);
            return x;
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
        //
        // 書き方は二つある。どちらも読める。
        // - 前からの書き方「｜親字《るび》」。青空文庫の記法。場面 3 などの文面が使っている
        // - 台詞の原稿の書き方（docs/scenario/README.md、オーナー、2026-09-28）。
        //   `親字<るび>`（親字は直前の同じ種類の字の並び。`｜` があればそこから）、`<dot>…</dot>` は傍点、`<br/>` は改行
        // 原稿の書き方は、まず Normalize で前からの書き方に直してから扱う。傍点は、字ごとに「｜字《﹅》」のルビに直す。
        // TextMeshPro の書式（`<size=…>` など。コードが足す物と、カードに付ける物）は、名前で見分けてそのまま通す

        /// <summary>親字の頭。青空文庫の記法に合わせてある</summary>
        public const char Head = '｜';
        /// <summary>ルビの頭</summary>
        public const char Open = '《';
        /// <summary>ルビの尻</summary>
        public const char Shut = '》';

        /// <summary>傍点を表すルビ。「｜字《﹅》」は、その字の上に点（<see cref="DotGlyph"/>）を打つ</summary>
        public const string DotMark = "﹅";

        /// <summary>
        /// 傍点に打つ字。
        /// Noto Sans JP の「﹅」（U+FE45）は縦組み用の形で、読点のような大きな点が字の枠の左上に寄っていて、横組みの上に乗せると字の左へずれて見える。
        /// 「・」（U+30FB）は字の枠の真ん中の小さな丸なので、こちらを打つ（2026-09-28）
        /// </summary>
        public const string DotGlyph = "・";

        /// <summary>
        /// 傍点の大きさ。親字に対する割合。「・」の点は字の枠の 0.212 em しか無く、親字と同じ大きさ（1）では
        /// 粗い画面の台詞（13 画素）の中で 2 画素ほどにしかならなかった。1.25 倍で 0.265 em、3.4 画素にする（2026-09-28）。
        /// 字の送りも 1.25 em になるが、親字の真ん中に揃えて両脇へ 0.125 em ずつ掛けるだけなので（<see cref="Hang"/> の内）、点の間隔は親字の間隔のまま
        /// </summary>
        public const float DotScale = 1.25f;

        /// <summary>「・」の点の径。字の大きさに対する em（Noto Sans JP、2026-09-28）</summary>
        public const float DotInk = 0.212f;

        /// <summary>「・」の下の端。ベースラインから（em、字の大きさ 1 の時）。TMP の字の寸法（Noto Sans JP、2026-09-28）</summary>
        public const float DotBottom = 0.274f;

        /// <summary>傍点を持ち上げる高さ。点の下の端を、漢字の上の端（<see cref="BaseTop"/>）からルビと同じ隙間だけ上に置く（点を大きくしても、字との隙間は同じ）</summary>
        public const float DotLift = BaseTop + Gap - DotBottom * DotScale;

        /// <summary>
        /// 文面には「｜親字《るび》」か「親字&lt;るび&gt;」の形で書いておく。
        /// 書式の指定をそのまま持たせると、字幕の折り返しがタグを字数に数えてしまう。
        /// 折り返してから Expand で書式に直す
        /// </summary>
        public static string Expand(string text)
        {
            return Expand(text, Scale, Lift);
        }

        /// <summary>大きさ scale と持ち上げる高さ lift を渡して直す。大きさを比べて撮るときに使う（傍点は <see cref="DotScale"/> のまま）</summary>
        public static string Expand(string text, float scale, float lift)
        {
            return Expand(text, scale, lift, false);
        }

        /// <summary>
        /// centred が true なら、行の頭でもルビを親字の真ん中に揃える（広いルビは前の空きへ掛ける）。
        /// 行ごとに真ん中へ寄せて並べる看板の枠（<see cref="ListFormat.ComposeSign"/>）が使う。行の頭より前へ出るぶんは、並べる側が空けておく（<see cref="Span"/>）
        /// </summary>
        public static string Expand(string text, float scale, float lift, bool centred)
        {
            text = Normalize(text);
            if (string.IsNullOrEmpty(text) || text.IndexOf(Head) < 0) return text;
            var made = new System.Text.StringBuilder(text.Length + 64);
            // 今かかっている <size=数>。数で書いた物だけを追う（% と em は既定の大きさに対して効くので 0 にして、ルビも % で書く）
            var sizes = new System.Collections.Generic.List<float>();
            var i = 0;
            while (i < text.Length)
            {
                var tag = TagEnd(text, i);
                if (tag > i)
                {
                    Track(text.Substring(i + 1, tag - i - 1), sizes);
                    made.Append(text, i, tag + 1 - i);
                    i = tag + 1;
                    continue;
                }
                int baseFrom, baseTo, rubyFrom, rubyTo;
                if (!Group(text, i, out baseFrom, out baseTo, out rubyFrom, out rubyTo))
                {
                    made.Append(text[i]);
                    i++;
                    continue;
                }
                var baseText = text.Substring(baseFrom, baseTo - baseFrom);
                var ruby = text.Substring(rubyFrom, rubyTo - rubyFrom);
                var size = sizes.Count > 0 ? sizes[sizes.Count - 1] : 0f;
                // 傍点は行の頭でも親字の真ん中に打つ
                if (ruby == DotMark) made.Append(Over(baseText, DotGlyph, DotScale, DotLift, false, size));
                else made.Append(Over(baseText, ruby, scale, lift, !centred && LineStart(text, i), size));
                i = rubyTo + 1;
            }
            return made.ToString();
        }

        /// <summary>書式 inner が &lt;size=…&gt; か &lt;/size&gt; なら、かかっている大きさの積み上げ sizes を進める</summary>
        static void Track(string inner, System.Collections.Generic.List<float> sizes)
        {
            var t = inner.Trim().ToLowerInvariant();
            if (t == "/size")
            {
                if (sizes.Count > 0) sizes.RemoveAt(sizes.Count - 1);
                return;
            }
            if (!t.StartsWith("size=")) return;
            var value = t.Substring(5).Trim().Trim('"');
            if (value.EndsWith("px")) value = value.Substring(0, value.Length - 2);
            var n = 0f;
            var plain = value.Length > 0 && value[0] != '+' && value[0] != '-'
                && float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out n);
            sizes.Add(plain ? n : 0f);
        }

        /// <summary>
        /// at が行の頭か。文の頭・改行の直後と、表の列の頭（<see cref="ListFormat"/> が置く &lt;pos=…&gt; の直後）。
        /// 列の頭も、前の列の字の上へルビを掛けない（列の幅は親字とルビの広い方で取ってある、<see cref="Width"/>）
        /// </summary>
        static bool LineStart(string text, int at)
        {
            if (at == 0 || text[at - 1] == '\n') return true;
            if (text[at - 1] != '>') return false;
            var open = text.LastIndexOf('<', at - 1);
            return open >= 0 && string.CompareOrdinal(text, open, "<pos=", 0, 5) == 0;
        }

        /// <summary>ルビと傍点を落として親字だけにする。ログや照合に使う</summary>
        public static string Plain(string text)
        {
            text = Normalize(text);
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

        /// <summary>ルビか傍点が一つでもあるか。ある文は行を開ける（<see cref="ExtraLineSpacing"/>）</summary>
        public static bool Has(string text)
        {
            text = Normalize(text);
            if (string.IsNullOrEmpty(text)) return false;
            for (var at = text.IndexOf(Head); at >= 0; at = text.IndexOf(Head, at + 1))
            {
                int baseFrom, baseTo, rubyFrom, rubyTo;
                if (Group(text, at, out baseFrom, out baseTo, out rubyFrom, out rubyTo)) return true;
            }
            return false;
        }

        /// <summary>
        /// 画面に出る幅。半角いくつぶん（<see cref="ListFormat.Units"/> と同じ数え方）。
        /// ルビと傍点は親字とルビ（傍点）の広い方、TextMeshPro の書式は 0 と数える。表の列の幅を揃えるのに使う
        /// </summary>
        public static int Width(string text)
        {
            return Width(text, Scale);
        }

        /// <summary>ルビの大きさ scale で振る時の幅（リストの枠、<see cref="ListLayout.RubyScale"/>）</summary>
        public static int Width(string text, float scale)
        {
            text = Normalize(text);
            if (string.IsNullOrEmpty(text)) return 0;
            var n = 0;
            var i = 0;
            while (i < text.Length)
            {
                int baseFrom, baseTo, rubyFrom, rubyTo;
                if (Group(text, i, out baseFrom, out baseTo, out rubyFrom, out rubyTo))
                {
                    var ruby = text.Substring(rubyFrom, rubyTo - rubyFrom);
                    // 傍点の点は小さく、親字の真ん中に乗るだけなので、幅は親字のまま
                    var over = ruby == DotMark ? 0f : ListFormat.Units(ruby) * scale;
                    n += Mathf.Max(ListFormat.Units(text.Substring(baseFrom, baseTo - baseFrom)), Mathf.CeilToInt(over - 1e-3f));
                    i = rubyTo + 1;
                    continue;
                }
                var shut = TagEnd(text, i);
                if (shut > i)
                {
                    i = shut + 1;
                    continue;
                }
                n += ListFormat.Units(text.Substring(i, 1));
                i++;
            }
            return n;
        }

        /// <summary>
        /// at から始まるルビの指定を読む。「｜親字《るび》」の形だけを見る（原稿の書き方は先に <see cref="Normalize"/> で直しておく）。
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

        /// <summary>
        /// at から TextMeshPro の書式（&lt;size=…&gt; など）が始まっていれば、その &gt; の位置。書式でなければ -1。
        /// ルビ（&lt;るび&gt;）と改行・傍点（&lt;br/&gt;・&lt;dot&gt;）は書式に数えない
        /// </summary>
        public static int TagEnd(string text, int at)
        {
            if (at >= text.Length || text[at] != '<') return -1;
            var shut = text.IndexOf('>', at + 1);
            if (shut < 0) return -1;
            return IsTag(text.Substring(at + 1, shut - at - 1)) ? shut : -1;
        }

        // ---- 原稿の書き方を直す -------------------------------------------

        /// <summary>
        /// TextMeshPro の書式の名前。**これに当たる &lt;…&gt; はルビにしない。**
        /// ほかに、= を含む物・/ で始まる物（閉じ）・# で始まる物（色）も書式とみなす
        /// </summary>
        static readonly string[] TagNames =
        {
            "a", "action", "align", "allcaps", "alpha", "b", "br", "class", "color", "cspace", "font", "font-weight",
            "gradient", "i", "indent", "line-height", "line-indent", "link", "lowercase", "margin", "margin-left",
            "margin-right", "mark", "material", "mspace", "nbsp", "nobr", "noparse", "page", "pos", "rotate", "s",
            "shy", "size", "smallcaps", "space", "sprite", "strikethrough", "style", "sub", "sup", "u", "uppercase",
            "voffset", "width", "zwj", "zwsp",
        };

        /// <summary>&lt;…&gt; の中身 inner が TextMeshPro の書式か</summary>
        public static bool IsTag(string inner)
        {
            if (string.IsNullOrEmpty(inner)) return false;
            var t = inner.Trim();
            if (t.Length == 0) return false;
            if (t[0] == '/' || t[0] == '#') return !IsDotOrBreak(t);
            if (t.IndexOf('=') >= 0) return true;
            var end = 0;
            while (end < t.Length && t[end] != ' ' && t[end] != '"') end++;
            var name = t.Substring(0, end).ToLowerInvariant();
            if (IsDotOrBreak(name)) return false;
            return System.Array.IndexOf(TagNames, name) >= 0;
        }

        static bool IsDotOrBreak(string name)
        {
            name = name.Replace(" ", string.Empty).ToLowerInvariant();
            return name == "dot" || name == "/dot" || name == "br" || name == "br/";
        }

        /// <summary>
        /// 原稿の書き方（docs/scenario/README.md）を、前からの書き方に直す。前からの書き方と書式はそのまま通す。何度かけても同じ。
        /// - `&lt;br/&gt;`（`&lt;br&gt;`）は改行
        /// - `&lt;dot&gt;…&lt;/dot&gt;` は傍点。囲んだ字ごとに「｜字《﹅》」にする（空白と改行には打たない）
        /// - それ以外の `&lt;…&gt;` はルビ。親字は、`｜` があればそこから、無ければ直前の同じ種類の字の並び（<see cref="Kind"/>）。
        ///   親字が決まらない（直前が約物や空白）物は、書いたまま残す
        /// </summary>
        public static string Normalize(string text)
        {
            // リストと看板のページの印（ListFormat.Mark・SignMark）は、枠に組むかを決めるだけの物。組む時と出す時には落とす
            if (!string.IsNullOrEmpty(text) && (text[0] == ListFormat.Mark || text[0] == ListFormat.SignMark)) text = text.Substring(1);
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return text;
            var made = new System.Text.StringBuilder(text.Length + 32);
            var head = -1;      // 親字の頭の ｜ を置いた made の位置。ルビを待っている間だけ
            var dot = false;
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == Head)
                {
                    int baseFrom, baseTo, rubyFrom, rubyTo;
                    if (Group(text, i, out baseFrom, out baseTo, out rubyFrom, out rubyTo))
                    {
                        made.Append(text, i, rubyTo + 1 - i);
                        i = rubyTo + 1;
                        head = -1;
                        continue;
                    }
                    head = made.Length;
                    made.Append(c);
                    i++;
                    continue;
                }
                if (c == '\n')
                {
                    head = -1;
                    made.Append(c);
                    i++;
                    continue;
                }
                if (c == '<')
                {
                    var shut = text.IndexOf('>', i + 1);
                    if (shut > i + 1)
                    {
                        var inner = text.Substring(i + 1, shut - i - 1);
                        var name = inner.Replace(" ", string.Empty).ToLowerInvariant();
                        if (name == "br" || name == "br/")
                        {
                            made.Append('\n');
                            head = -1;
                            i = shut + 1;
                            continue;
                        }
                        if (name == "dot" || name == "/dot")
                        {
                            dot = name == "dot";
                            i = shut + 1;
                            continue;
                        }
                        if (IsTag(inner))
                        {
                            made.Append(text, i, shut + 1 - i);
                            i = shut + 1;
                            continue;
                        }
                        var from = head >= 0 ? head + 1 : RunStart(made);
                        if (from < made.Length)
                        {
                            if (head < 0) made.Insert(from, Head);
                            made.Append(Open).Append(inner).Append(Shut);
                            head = -1;
                            i = shut + 1;
                            continue;
                        }
                    }
                    // 親字の無い <…> と、閉じの無い < は書いたまま
                }
                if (dot && c != ' ' && c != '　')
                {
                    var n = char.IsHighSurrogate(c) && i + 1 < text.Length ? 2 : 1;
                    made.Append(Head).Append(text, i, n).Append(Open).Append(DotMark).Append(Shut);
                    i += n;
                    continue;
                }
                made.Append(c);
                i++;
            }
            return made.ToString();
        }

        /// <summary>親字になる字の種類。直前の同じ種類の字の並びが親字になる</summary>
        public enum Kind
        {
            None,
            /// <summary>漢字。々〆〇ヵヶを含む</summary>
            Kanji,
            /// <summary>片仮名。ーを含む</summary>
            Katakana,
            Hiragana,
            /// <summary>英字。数字・ハイフン・アポストロフィを含む</summary>
            Latin,
        }

        public static Kind KindOf(char c)
        {
            if (c == '々' || c == '〆' || c == '〇' || c == 'ヵ' || c == 'ヶ') return Kind.Kanji;
            if ((c >= '㐀' && c <= '䶿') || (c >= '一' && c <= '鿿') || (c >= '豈' && c <= '﫿')) return Kind.Kanji;
            if (char.IsSurrogate(c)) return Kind.Kanji;     // 拡張の漢字（𠮷 など）
            if ((c >= 'ァ' && c <= 'ヺ') || c == 'ー' || c == 'ヽ' || c == 'ヾ' || (c >= 'ㇰ' && c <= 'ㇿ')
                || (c >= 'ｦ' && c <= 'ﾟ')) return Kind.Katakana;
            if ((c >= 'ぁ' && c <= 'ゖ') || c == 'ゝ' || c == 'ゞ') return Kind.Hiragana;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) return Kind.Latin;
            if (c == '-' || c == '\'' || c == '’') return Kind.Latin;
            if ((c >= 'À' && c <= 'ɏ') && c != '×' && c != '÷') return Kind.Latin;
            if ((c >= 'Ａ' && c <= 'Ｚ') || (c >= 'ａ' && c <= 'ｚ') || (c >= '０' && c <= '９')) return Kind.Latin;
            return Kind.None;
        }

        /// <summary>made の尻の、同じ種類の字の並びの頭。尻の字がどの種類でもなければ made.Length</summary>
        static int RunStart(System.Text.StringBuilder made)
        {
            var end = made.Length;
            if (end == 0) return end;
            var kind = KindOf(made[end - 1]);
            if (kind == Kind.None) return end;
            var at = end - 1;
            while (at > 0 && KindOf(made[at - 1]) == kind) at--;
            return at;
        }
    }
}
