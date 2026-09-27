namespace HalfAware
{
    /// <summary>
    /// TAB のコンソールのデバッグで並べる場面。動作確認のため、遊んでいる途中でも場面を移れる。
    /// 並べて数字を振るだけで、並べて見せるのはコンソール（<see cref="ImplantConsole"/>）、読み込みは呼び手が行う
    /// </summary>
    public static class SceneMenu
    {
        /// <summary>表に出す名。物語の順に並べる</summary>
        public static readonly string[] Titles = { "自室", "路地裏", "自室・接続", "潜る", "小休止", "庭の記憶", "自室・気づき", "車内", "村" };

        /// <summary>
        /// 読み込むシーンの名。Titles と同じ並び。
        /// ここに挙げた名は組み立ての一覧にも入っていること。
        /// 入っていないと、数字を押した先で読み込みが落ちる
        /// </summary>
        public static readonly string[] Scenes = { "Room", "Alley", "Connect", "Dive", "Rest", Garden, "Notice", "Drive", "Village" };

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

        // ---- 同じシーンの別の場面 ----------------------------------------------------
        //
        // 村（Village）は、夕方なら場面 6（庭の記憶）、朝なら場面 9。一覧には別の行として並べ、
        // 庭の記憶の行はシーンの名ではなく別の名（Garden）で持つ。読むのは村で、読む前に印（GardenHandoff）を立てる

        /// <summary>庭の記憶の行の名。シーンの名ではない。読むのは <see cref="GardenScene"/></summary>
        public const string Garden = "Garden";

        /// <summary>庭の記憶の行が読むシーン</summary>
        public const string GardenScene = "Village";

        /// <summary>一覧の名から、読むシーンの名。庭の記憶の行は村</summary>
        public static string SceneOf(string entry)
        {
            return entry == Garden ? GardenScene : entry;
        }

        /// <summary>一覧の名のシーンを読む前に呼ぶ。庭の記憶の行なら村を場面 6 として開く印を立て、ほかの行なら下ろす</summary>
        public static void Prepare(string entry)
        {
            GardenHandoff.Pending = entry == Garden;
        }

        /// <summary>
        /// いまいる場面の一覧の名。シーンの名 scene と、場面 6 を流しているか（<see cref="GardenHandoff.Active"/>）から。
        /// 村で場面 6 を流していれば庭の記憶の行、そうでなければシーンの名のまま
        /// </summary>
        public static string Here(string scene, bool garden)
        {
            return scene == GardenScene && garden ? Garden : scene;
        }
    }
}
