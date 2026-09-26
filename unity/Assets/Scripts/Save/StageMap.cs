namespace HalfAware
{
    /// <summary>
    /// 物語の場面の番号（1〜10）と、Unity のシーンの対応（設計書 5 節）。セーブはこの番号で持つ。
    ///
    /// 一つのシーンが二つの場面を持つことがある。村（<c>Village</c>）は朝なら場面 9、夕方なら場面 6。
    /// その区別は村の時刻（<see cref="VillageHour.Hour"/> の名）で付ける。
    ///
    /// **まだ無い場面は表に載せない。** 場面 6（庭の記憶、村の夕方）・7（自室・気づき）・10（対面）を作ったら、
    /// <see cref="Rows"/> に一行足す。場面 10 のように、同じシーンの途中で場面が替わるものは、
    /// 替わった所から <see cref="SaveFlow.EnterStage"/> を呼ぶ
    /// </summary>
    public static class StageMap
    {
        public const int First = 1;
        public const int Last = 10;

        /// <summary>村の時刻の名。<see cref="VillageHour.Hour"/> の名と揃える</summary>
        public const string Morning = "Morning";
        public const string Evening = "Evening";

        /// <summary>場面の名。番号で引く（0 は使わない）。デバッグの一覧（<see cref="SceneMenu.Titles"/>）と揃える</summary>
        static readonly string[] Titles =
        {
            "",
            "自室",
            "路地裏",
            "自室・接続",
            "潜る",
            "小休止",
            "庭の記憶",
            "自室・気づき",
            "車内",
            "村",
            "対面",
        };

        struct Row
        {
            public int stage;
            public string scene;
            /// <summary>空ならどの時刻でも当たる</summary>
            public string hour;

            public Row(int stage, string scene, string hour)
            {
                this.stage = stage;
                this.scene = scene;
                this.hour = hour;
            }
        }

        /// <summary>
        /// いまある場面。上から引いて最初に当たった物を採る。
        /// 場面 6 を作るときは <c>new Row(6, "Village", Evening)</c> のように足す
        /// </summary>
        static readonly Row[] Rows =
        {
            new Row(1, "Room", ""),
            new Row(2, "Alley", ""),
            new Row(3, "Connect", ""),
            new Row(4, "Dive", ""),
            new Row(5, "Rest", ""),
            new Row(8, "Drive", ""),
            new Row(9, "Village", Morning),
        };

        /// <summary>シーンの名と村の時刻から、場面の番号。表に無ければ 0（タイトルの画面など、セーブしない所）</summary>
        public static int StageOf(string scene, string hour)
        {
            if (string.IsNullOrEmpty(scene)) return 0;
            for (var i = 0; i < Rows.Length; i++)
            {
                var r = Rows[i];
                if (r.scene != scene) continue;
                if (r.hour.Length > 0 && r.hour != (hour ?? string.Empty)) continue;
                return r.stage;
            }
            return 0;
        }

        /// <summary>場面の頭のシーンの名。まだ無い場面なら null</summary>
        public static string SceneOf(int stage)
        {
            for (var i = 0; i < Rows.Length; i++)
                if (Rows[i].stage == stage) return Rows[i].scene;
            return null;
        }

        /// <summary>場面の頭の村の時刻。村でない場面や、まだ無い場面は空</summary>
        public static string HourOf(int stage)
        {
            for (var i = 0; i < Rows.Length; i++)
                if (Rows[i].stage == stage) return Rows[i].hour;
            return string.Empty;
        }

        /// <summary>場面の名。範囲の外なら空</summary>
        public static string TitleOf(int stage)
        {
            return stage >= First && stage <= Last ? Titles[stage] : string.Empty;
        }

        /// <summary>その場面から始められるか（シーンがあるか）</summary>
        public static bool Playable(int stage)
        {
            return SceneOf(stage) != null;
        }
    }
}
