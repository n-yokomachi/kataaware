using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>年齢の区分。骨の縮尺はこれで決まる</summary>
    public enum AgeBand { Toddler, Child, Teen, Adult, Elder }

    /// <summary>
    /// 記憶の中の人ひとり。どの記憶に出ても同じ見た目にする（設計書 9.5 節）。
    /// 記憶 0 の母と記憶 8 の隣の母親は、どちらもハンナ（34）
    /// </summary>
    [Serializable]
    public struct Person
    {
        [Tooltip("コードで引く名前")]
        public string id;
        [Tooltip("一覧の行の『』の中")]
        public string name;
        [Tooltip("自分の記憶の番号（0 始まり）。板の潜る先と同じ")]
        public int entry;
        public int age;
        public bool female;
        [Tooltip("どこの人か")]
        public string from;
        [Tooltip("立った背。m。頭の上（髪）から靴の裏まで、背を丸めた人は丸めたまま測る")]
        public float height;
        [Tooltip("背の丸み。度。年寄りだけ")]
        public float curl;

        public AgeBand Band { get { return DiveCast.BandOf(age); } }
    }

    /// <summary>
    /// 場面 4 の記憶に出る人の一覧。設計書 6 節の十六人で、倫敦の網に繋がった人々。
    /// アジア・欧州・米国・中南米に散っている。
    ///
    /// 記憶の中で板の出る相手は、一覧（<see cref="DiveRoster"/>）の Seen の飛び先で誰かが決まる。
    /// 通りすがり（公園を横切るプリヤ、デッキを通るローザ、戸口で待つアイシャ）も同じ人として持つ。
    ///
    /// **見た目は Rocketbox の模型が持つ**（<c>BuildDiveCast</c>・<c>BuildDivePeople</c>、設計メモ 2026-09-26）。
    /// ここに持つのは id・名前・年・性・出身・背（height）・背の丸み（curl）だけで、背の丸みは動きを置いたあとに一度だけ掛ける。
    /// 年齢の比（<see cref="ProportionOf"/>）も区分から直には掛けない。骨の縮尺は <c>RocketboxMemory.Proportion</c> が持つ
    /// （10 代の頭の比だけそこから借りる）。二重に掛けると体つきが崩れる
    /// </summary>
    public static class DiveCast
    {
        public static AgeBand BandOf(int age)
        {
            if (age <= 4) return AgeBand.Toddler;
            if (age <= 12) return AgeBand.Child;
            if (age <= 19) return AgeBand.Teen;
            if (age < 60) return AgeBand.Adult;
            return AgeBand.Elder;
        }

        // ---- 年齢の骨 -----------------------------------------------------------

        /// <summary>
        /// 年齢の区分ごとの骨の縮尺。模型（大人）に対する比で、背はあとから全体の縮尺で合わせる。
        ///
        /// 子どもは頭を大きく、手と脚を短く。幼児（4 歳以下）はさらに寸詰まり。
        /// いまの十六人に幼児はいない（ルーカスを 3 歳から 11 歳に上げた）。
        /// 大人を縮めただけでは、頭と背の比も脚と背の比も大人のままになる
        /// </summary>
        public struct Proportion
        {
            public float head;
            public float arm;
            public float leg;
        }

        public static Proportion ProportionOf(AgeBand band)
        {
            switch (band)
            {
                case AgeBand.Toddler: return new Proportion { head = 1.42f, arm = 0.80f, leg = 0.72f };
                case AgeBand.Child: return new Proportion { head = 1.26f, arm = 0.88f, leg = 0.83f };
                case AgeBand.Teen: return new Proportion { head = 1.03f, arm = 1f, leg = 1f };
                default: return new Proportion { head = 1f, arm = 1f, leg = 1f };
            }
        }

        // ---- 十六人 ---------------------------------------------------------------

        static Person[] people;

        /// <summary>十六人。並びは自分の記憶の番号（設計書 6 節の 1〜16 から一つ引いた数）</summary>
        public static Person[] People
        {
            get
            {
                if (people == null) people = Make();
                return people;
            }
        }

        /// <summary>自分の記憶の番号で引く。板の潜る先（Seen.target）から人を決めるのに使う</summary>
        public static bool TryByEntry(int entry, out Person person)
        {
            var all = People;
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].entry != entry) continue;
                person = all[i];
                return true;
            }
            person = default(Person);
            return false;
        }

        public static bool TryById(string id, out Person person)
        {
            var all = People;
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].id != id) continue;
                person = all[i];
                return true;
            }
            person = default(Person);
            return false;
        }

        static Person[] Make()
        {
            return new[]
            {
                // 公営住宅の A。中国系の母と娘
                Who("Mei", "メイ", 0, 6, true, "中国系（ロンドン）", 1.10f, 0f),
                Who("Hanna", "ハンナ", 1, 34, true, "中国系（ロンドン）", 1.66f, 0f),

                // 公園の一家。コロンビアから来た祖父母と孫二人（兄のルーカス 11 と妹のソフィア 7）
                Who("Albert", "アルベルト", 2, 78, false, "コロンビア", 1.80f, 20f),
                Who("Sofia", "ソフィア", 3, 7, true, "コロンビア", 1.16f, 0f),

                // 電車。米国から来た学生と、その後輩
                Who("Emily", "エミリー", 4, 19, true, "米国", 1.69f, 0f),

                // 台所の一家。米国から越してきた。夫は勤め人、妻も出かける支度
                Who("Mark", "マーク", 5, 52, false, "米国", 1.83f, 0f),
                Who("Linda", "リンダ", 6, 49, true, "米国", 1.69f, 0f),

                // 教室の先生。韓国系
                Who("Lee", "リー", 7, 41, false, "韓国系（ロンドン）", 1.81f, 0f),

                // 公営住宅の B。イタリアから来た老夫婦
                Who("Giorgio", "ジョルジョ", 8, 66, false, "イタリア", 1.73f, 12f),
                Who("Rosa", "ローザ", 9, 72, true, "コロンビア", 1.57f, 16f),
                // ルーカスは 11 歳。3 歳を 10〜12 歳の模型から縮めて作ると 7〜8 歳にしか見えなかったので、
                // 年の方を模型（Rocketbox の男子 01、背 1.43 m）に合わせた（設計メモ 9 節の 8）
                Who("Lucas", "ルーカス", 10, 11, false, "コロンビア", 1.43f, 0f),

                Who("Priya", "プリヤ", 11, 18, true, "インド系（ロンドン）", 1.62f, 0f),

                // 台所の一家の息子
                Who("Daniel", "ダニエル", 12, 15, false, "米国", 1.76f, 0f),

                // 教室の二人。同じ学校
                Who("Aisha", "アイシャ", 13, 16, true, "ソマリア系（ロンドン）", 1.62f, 0f),
                Who("Mateo", "マテオ", 14, 16, false, "メキシコ", 1.72f, 0f),

                Who("Elena", "エレナ", 15, 63, true, "イタリア", 1.58f, 11f),
            };
        }

        static Person Who(string id, string name, int entry, int age, bool female, string from, float height, float curl)
        {
            return new Person
            {
                id = id,
                name = name,
                entry = entry,
                age = age,
                female = female,
                from = from,
                height = height,
                curl = curl,
            };
        }
    }
}
