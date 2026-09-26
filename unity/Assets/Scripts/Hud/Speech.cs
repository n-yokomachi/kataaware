namespace HalfAware
{
    /// <summary>
    /// 「ハンナ「メイ！」」の形の一行を、話した人の名前と台詞に分ける。
    /// 字幕の名前の行と、コンソールのログの名前の列がこれを使う。
    ///
    /// 分けるのは、行の頭に名前があり、その直後の「と行の尻の」が組になっている行だけ。
    /// 「「グレビル・ストリート」と書かれている」のように鉤括弧で始まる文や、
    /// 鉤括弧が途中で閉じて地の文が続く行は、そのまま台詞として返す
    /// </summary>
    public static class Speech
    {
        /// <summary>名前として認める長さ。これより長い頭は地の文とみなす</summary>
        public const int NameMax = 12;

        /// <summary>名前に入らない字。約物と、ルビの書式の字</summary>
        const string NotInName = "、。！？…・「」『』（）｜《》　 ";

        /// <summary>
        /// 分けられたら true。名前と台詞（外側の鉤括弧を外したもの）を返す。
        /// 分けられなければ false で、who は空、said は元の行のまま
        /// </summary>
        public static bool Split(string line, out string who, out string said)
        {
            who = string.Empty;
            said = line ?? string.Empty;
            if (string.IsNullOrEmpty(line)) return false;
            var body = line.TrimEnd();
            var open = body.IndexOf('「');
            if (open <= 0 || open > NameMax) return false;
            if (body[body.Length - 1] != '」') return false;
            for (var i = 0; i < open; i++)
                if (NotInName.IndexOf(body[i]) >= 0 || char.IsWhiteSpace(body[i])) return false;
            // 頭の「が行の尻の」で閉じているか。途中で閉じて地の文が続くなら台詞ではない
            var depth = 0;
            for (var i = open; i < body.Length; i++)
            {
                if (body[i] == '「') depth++;
                else if (body[i] == '」')
                {
                    depth--;
                    if (depth == 0 && i != body.Length - 1) return false;
                }
            }
            if (depth != 0) return false;
            who = body.Substring(0, open);
            said = body.Substring(open + 1, body.Length - open - 2);
            return true;
        }

        /// <summary>話した人の名前だけ。分けられなければ空</summary>
        public static string Who(string line)
        {
            string who, said;
            return Split(line, out who, out said) ? who : string.Empty;
        }
    }
}
