using System.Text.RegularExpressions;

namespace HalfAware
{
    /// <summary>
    /// コンソールの頭の行の左に出す、いまの日時と場所（設計書 3 節）。場面ごとに持たせる。
    ///
    /// 記憶の中（場面 4・6）は、いまの時刻ではなく「潜行中」と記憶の日時と場所を出す。
    /// 記憶は潜るたびに替わるので、そちらは DiveDirector が記憶を流し始めるたびに
    /// <see cref="ImplantConsole.SetPlace"/> で渡す
    /// </summary>
    public static class ConsolePlace
    {
        public const string Diving = "潜行中";

        /// <summary>場面ごとの日時と場所。<see cref="SceneMenu.Scenes"/> と同じ名で引く</summary>
        static readonly string[,] Table =
        {
            // 場面 1。冒頭のカードの 18:35 から一服したあと
            { "Room", "2166/08/15 18:42", "倫敦・自室" },
            // 場面 2
            { "Alley", "2166/08/15 19:26", "倫敦・路地裏" },
            // 場面 3。路地裏から戻った夜
            { "Connect", "2166/08/15 21:04", "倫敦・自室" },
            // 場面 4 の頭。記憶を流し始めたら DiveDirector が記憶の日時へ差し替える
            { "Dive", "", "" },
            // 場面 5。潜って戻ってきた直後
            { "Rest", "2166/08/15 21:52", "倫敦・自室" },
            // 場面 8
            { "Drive", "2166/08/15 23:40", "車内" },
            // 場面 9・10。朝
            { "Village", "2166/08/16 06:20", "村" },
        };

        /// <summary>記憶の場所の名。DiveIds の順</summary>
        static readonly string[,] Places =
        {
            { DiveIds.Estate, "団地" },
            { DiveIds.Park, "公園" },
            { DiveIds.Train, "電車" },
            { DiveIds.Kitchen, "台所" },
            { DiveIds.Classroom, "教室" },
        };

        /// <summary>日時と場所のあいだ</summary>
        public const string Gap = "　";

        /// <summary>場面の名から頭の行の左。表に無い場面は場面の名だけ</summary>
        public static string For(string scene)
        {
            for (var i = 0; i < Table.GetLength(0); i++)
            {
                if (Table[i, 0] != scene) continue;
                if (scene == "Dive") return Diving;
                return Table[i, 1] + Gap + Table[i, 2];
            }
            return scene ?? string.Empty;
        }

        /// <summary>
        /// 記憶の中の頭の行。row は場面 3 のモニターの列と同じ書式（「女　6　『メイ』　2156/03/02 07:14」）で、
        /// そこから日時を拾う。place は DiveIds の場所
        /// </summary>
        public static string Dive(string row, string place)
        {
            var stamp = Stamp(row);
            var name = PlaceName(place);
            var line = Diving;
            if (stamp.Length > 0) line += Gap + stamp;
            if (name.Length > 0) line += Gap + name;
            return line;
        }

        /// <summary>文の中の「年/月/日 時:分」。無ければ空</summary>
        public static string Stamp(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var m = Regex.Match(text, @"\d{4}/\d{2}/\d{2} \d{1,2}:\d{2}");
            return m.Success ? m.Value : string.Empty;
        }

        public static string PlaceName(string place)
        {
            for (var i = 0; i < Places.GetLength(0); i++)
                if (Places[i, 0] == place) return Places[i, 1];
            return string.Empty;
        }
    }
}
