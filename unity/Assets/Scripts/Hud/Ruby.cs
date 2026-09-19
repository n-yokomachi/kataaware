using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ルビ。TextMeshPro にルビの仕組みは無いので、書式の指定だけで組む。
    ///
    /// 小さくした字を持ち上げて先に置き、進んだぶんちょうど戻してから親字を重ねる。
    /// 戻す量を「親字の幅」ではなく「実際に進んだ幅」にするのが肝で、
    /// ここを取り違えるとルビの長さ次第で親字がずれる。
    ///
    /// 幅は ListFormat.Units（半角いくつぶん）で測る。全角 1 字 = 2、半角 1 字 = 1。
    /// em になおすと半分なので、ルビの進む幅は Units / 2 × Scale
    /// </summary>
    public static class Ruby
    {
        /// <summary>ルビの大きさ。親字に対する割合</summary>
        public const float Scale = 0.5f;

        /// <summary>持ち上げる高さ。親字の em に対する割合</summary>
        public const float Lift = 0.62f;

        /// <summary>
        /// text にルビを振る。どちらかが空なら text をそのまま返す。
        /// ルビが親字より広いときは、はみ出すぶんを後ろへ足して次の字と重ならないようにする
        /// </summary>
        public static string Over(string text, string ruby)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(ruby)) return text ?? string.Empty;

            var baseEm = ListFormat.Units(text) * 0.5f;
            var rubyEm = ListFormat.Units(ruby) * 0.5f * Scale;
            if (baseEm <= 0f || rubyEm <= 0f) return text;

            var lead = Mathf.Max(0f, (baseEm - rubyEm) * 0.5f);   // 親字の真ん中へ寄せる
            var back = lead + rubyEm;                             // ルビで進んだぶん
            var trail = Mathf.Max(0f, rubyEm - baseEm);           // 親字からはみ出したぶん

            var made = new System.Text.StringBuilder();
            made.Append(Tag("<voffset=", Lift, "em>"));
            made.Append("<size=").Append(Num(Scale * 100f)).Append("%>");
            // この <space> は小さくした側の em で効くので、親字の em になおして渡す
            if (lead > 0.001f) made.Append(Tag("<space=", lead / Scale, "em>"));
            made.Append(ruby);
            made.Append("</size></voffset>");
            made.Append(Tag("<space=", -back, "em>"));
            made.Append(text);
            if (trail > 0.001f) made.Append(Tag("<space=", trail, "em>"));
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
                made.Append(Over(text.Substring(baseFrom, baseTo - baseFrom),
                                 text.Substring(rubyFrom, rubyTo - rubyFrom)));
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
