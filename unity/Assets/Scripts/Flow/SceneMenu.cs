namespace HalfAware
{
    /// <summary>
    /// TAB のコンソールのデバッグで並べる場面。動作確認のため、遊んでいる途中でも場面を移れる。
    /// 並べて数字を振るだけで、並べて見せるのはコンソール（<see cref="ImplantConsole"/>）、読み込みは呼び手が行う
    /// </summary>
    public static class SceneMenu
    {
        /// <summary>表に出す名。物語の順に並べる</summary>
        public static readonly string[] Titles = { "自室", "路地裏", "自室・接続", "潜る", "小休止", "車内", "村" };

        /// <summary>
        /// 読み込むシーンの名。Titles と同じ並び。
        /// ここに挙げた名は組み立ての一覧にも入っていること。
        /// 入っていないと、数字を押した先で読み込みが落ちる
        /// </summary>
        public static readonly string[] Scenes = { "Room", "Alley", "Connect", "Dive", "Rest", "Drive", "Village" };

        /// <summary>押せる数字の数</summary>
        public static int Count { get { return Scenes.Length; } }

        /// <summary>押された数字に当たるシーンの名。当たりが無ければ null</summary>
        public static string Pick(int digit)
        {
            var i = digit - 1;
            return i >= 0 && i < Scenes.Length ? Scenes[i] : null;
        }

        /// <summary>
        /// 今いる場面を押しても何も起きないので、移る先だけを返す。
        /// 同じ場面や当たりの無い数字なら null
        /// </summary>
        public static string Target(int digit, string here)
        {
            var name = Pick(digit);
            return name != null && name != here ? name : null;
        }
    }
}
