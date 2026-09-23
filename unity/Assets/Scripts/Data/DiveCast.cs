using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 人の部位。模型の部位ごとのマテリアルを、この役で人ごとの色に塗り替える。
    ///
    /// **顔は作り込まない**（設計書 9.5 節）。目と眉は模型の小さな部位のまま、色だけ合わせる
    /// </summary>
    public enum Part
    {
        Skin,
        /// <summary>肌の影の部位（M_Casual の顔の下半分）。肌より少し暗く</summary>
        SkinShade,
        /// <summary>髪。M_Worker の口ひげもここ</summary>
        Hair,
        Brow,
        Eye,
        /// <summary>上に着ている物。上着・セーター・パーカー・ワンピースの上半分</summary>
        Top,
        /// <summary>上着の下のシャツ</summary>
        Under,
        Tie,
        /// <summary>上の小さな飾り。首飾り・ベストの縞</summary>
        TopTrim,
        /// <summary>ズボン・スカート</summary>
        Bottom,
        /// <summary>下の小さな飾り。M_Worker の膝当てとポケット</summary>
        BottomTrim,
        /// <summary>模型で素肌になっている脚と足首。タイツや靴下の色にする。既定は肌のまま</summary>
        Legwear,
        Shoe,
        Sole,
        /// <summary>M_Worker の帽子</summary>
        Cap,
    }

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
        [Tooltip("どこの人か。肌と髪の色の拠り所")]
        public string from;
        [Tooltip("Quaternius の模型。DiveCast.Models のどれか")]
        public string model;
        [Tooltip("立った背。m。頭の上（髪）から靴の裏まで、背を丸めた人は丸めたまま測る")]
        public float height;
        [Tooltip("体の太さ。1 で模型のまま。胴と手足の幅と厚みに掛ける")]
        public float girth;
        [Tooltip("背の丸み。度。年寄りだけ")]
        public float curl;
        [Tooltip("部位ごとの色。Part の並び")]
        public Color[] colors;

        public Color this[Part part] { get { return colors[(int)part]; } }

        public AgeBand Band { get { return DiveCast.BandOf(age); } }
    }

    /// <summary>
    /// 場面 4 の記憶に出る人の一覧。設計書 6 節の十六人で、倫敦の網に繋がった人々。
    /// アジア・欧州・米国・中南米に散っているので、肌と髪の色もそれに合わせて散らす。
    ///
    /// 記憶の中で板の出る相手は、一覧（<see cref="DiveRoster"/>）の Seen の飛び先で誰かが決まる。
    /// 通りすがり（公園を横切るプリヤ、デッキを通るローザ、戸口で待つアイシャ）も同じ人として持つ。
    ///
    /// 服の模型は日常に合う七つだけ（<see cref="Models"/>）。冒険家・宇宙服・パンクは使わない
    /// </summary>
    public static class DiveCast
    {
        /// <summary>使ってよい模型。ロンドンの日常に合う身なりだけ</summary>
        public static readonly string[] Models =
        {
            "M_Casual", "M_Hoodie", "M_Suit", "M_Worker", "W_Casual", "W_Formal", "W_Suit",
        };

        /// <summary>日常に合わないので使わない模型</summary>
        public static readonly string[] Unfit =
        {
            "M_Adventurer", "W_Adventurer", "W_SciFi", "M_Punk", "W_Punk",
        };

        public static readonly int PartCount = Enum.GetValues(typeof(Part)).Length;

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
        /// 子どもは頭を大きく、手と脚を短く。三歳はさらに寸詰まり。
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
                case AgeBand.Toddler: return new Proportion { head = 1.42f, arm = 0.82f, leg = 0.76f };
                case AgeBand.Child: return new Proportion { head = 1.26f, arm = 0.90f, leg = 0.87f };
                case AgeBand.Teen: return new Proportion { head = 1.03f, arm = 1f, leg = 1f };
                default: return new Proportion { head = 1f, arm = 1f, leg = 1f };
            }
        }

        // ---- 模型の部位 -----------------------------------------------------------

        /// <summary>
        /// 模型の部位。"模型/レンダラー" ごとに、サブメッシュの並びで役を持つ。
        /// どの部位が何かは、焼いた形の高さと前後の広がりで見て決めた
        /// （例: M_Hoodie の脚の Skin は脛で、半ズボンから出ている）
        /// </summary>
        static readonly Dictionary<string, Part[]> parts = new Dictionary<string, Part[]>
        {
            { "M_Casual/Casual2_Body", new[] { Part.Top, Part.Skin } },
            { "M_Casual/Casual2_Feet", new[] { Part.Sole, Part.Shoe } },
            { "M_Casual/Casual2_Head", new[] { Part.Skin, Part.SkinShade, Part.Brow, Part.Eye, Part.Hair } },
            { "M_Casual/Casual2_Legs", new[] { Part.Bottom } },

            { "M_Hoodie/Casual_Body", new[] { Part.Top, Part.Skin } },
            { "M_Hoodie/Casual_Feet", new[] { Part.Sole, Part.Shoe } },
            { "M_Hoodie/Casual_Head", new[] { Part.Skin, Part.Brow, Part.Eye, Part.Hair } },
            { "M_Hoodie/Casual_Legs", new[] { Part.Legwear, Part.Bottom } },

            { "M_Suit/Suit_Body", new[] { Part.Top, Part.Under, Part.Tie, Part.Skin } },
            { "M_Suit/Suit_Feet", new[] { Part.Shoe } },
            { "M_Suit/Suit_Head", new[] { Part.Skin, Part.Hair, Part.Brow, Part.Eye } },
            { "M_Suit/Suit_Legs", new[] { Part.Bottom } },

            // 黄色い縞は仕事着のベストの反射帯、黄色い帽子は安全帽。
            // どちらも日常の色に塗り替えて、編んだベストと鳥打帽に見せる
            { "M_Worker/Worker_Body", new[] { Part.TopTrim, Part.Top, Part.Under, Part.Skin } },
            { "M_Worker/Worker_Feet", new[] { Part.Sole, Part.Shoe } },
            // "Eyebrows" のマテリアルは眉と、帽子の下から出る横の髪をまとめて持つ。髪の色にする
            { "M_Worker/Worker_Head", new[] { Part.Skin, Part.Hair, Part.Cap, Part.Eye, Part.Hair } },
            { "M_Worker/Worker_Legs", new[] { Part.BottomTrim, Part.Bottom } },

            { "W_Casual/Casual_Body", new[] { Part.Top, Part.Skin } },
            { "W_Casual/Casual_Feet", new[] { Part.Shoe, Part.Legwear } },
            { "W_Casual/Casual_Head", new[] { Part.Skin, Part.Hair, Part.Brow, Part.Eye } },
            { "W_Casual/Casual_Legs", new[] { Part.Bottom } },

            // 眉と目が一つのマテリアル（Brown）。目の色にする
            { "W_Formal/Formad_Head", new[] { Part.Skin, Part.Hair, Part.Eye } },
            { "W_Formal/Formal_Body", new[] { Part.Top, Part.Skin, Part.TopTrim } },
            { "W_Formal/Formal_Feet", new[] { Part.Legwear, Part.Shoe } },
            { "W_Formal/Formal_Legs", new[] { Part.Legwear, Part.Bottom } },

            { "W_Suit/Suit_Body", new[] { Part.Top, Part.Under, Part.Skin } },
            { "W_Suit/Suit_Feet", new[] { Part.Shoe, Part.Legwear } },
            { "W_Suit/Suit_Head", new[] { Part.Skin, Part.Hair, Part.Brow, Part.Eye } },
            { "W_Suit/Suit_Legs", new[] { Part.Bottom } },
        };

        /// <summary>模型のレンダラーのサブメッシュが何の部位か。知らない組なら false</summary>
        public static bool TryPartOf(string model, string renderer, int submesh, out Part part)
        {
            part = Part.Skin;
            Part[] row;
            if (!parts.TryGetValue(model + "/" + renderer, out row)) return false;
            if (submesh < 0 || submesh >= row.Length) return false;
            part = row[submesh];
            return true;
        }

        /// <summary>
        /// 部位の艶。布は艶を落とし、靴と目だけ少し光らせる。
        /// 320×180 では艶の点が顔や服の模様より先に目に入るので、全体に控えめ
        /// </summary>
        public static float GlossOf(Part part)
        {
            switch (part)
            {
                case Part.Skin:
                case Part.SkinShade: return 0.28f;
                case Part.Hair: return 0.30f;
                case Part.Eye: return 0.55f;
                case Part.Shoe: return 0.35f;
                case Part.Tie: return 0.20f;
                default: return 0.08f;
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
            // 色はどれも sRGB。暗い場所でも部位の境が読めるよう、上と下の明るさを離しておく
            return new[]
            {
                // 公営住宅の A。中国系の母と娘。娘は黄色、母は深い赤
                Who("Mei", "メイ", 0, 6, true, "中国系（ロンドン）", "W_Casual", 1.10f, 0.96f, 0f,
                    Rgb(0xEBC7A6), Rgb(0x17120F))
                    .Put(Part.Top, Rgb(0xE0B63A)).Put(Part.Bottom, Rgb(0x3F5A8C))
                    .Put(Part.Shoe, Rgb(0xD9728C)).Put(Part.Legwear, Rgb(0xECECEC)).Done(),
                Who("Hanna", "ハンナ", 1, 34, true, "中国系（ロンドン）", "W_Casual", 1.66f, 0.96f, 0f,
                    Rgb(0xE6C2A0), Rgb(0x141110))
                    .Put(Part.Top, Rgb(0x8C2F39)).Put(Part.Bottom, Rgb(0x232A3D))
                    .Put(Part.Shoe, Rgb(0x1C1A1A)).Put(Part.Legwear, Rgb(0x2A2A2E)).Done(),

                // 公園の一家。コロンビアから来た祖父母と孫二人。
                // 祖父は鳥打帽と編んだベスト（M_Worker の安全帽と反射ベストを塗り替える）
                Who("Albert", "アルベルト", 2, 78, false, "コロンビア", "M_Worker", 1.80f, 0.94f, 16f,
                    Rgb(0xA87A59), Rgb(0xD9D6CF))
                    .Put(Part.Top, Rgb(0x3B4A36)).Put(Part.TopTrim, Rgb(0x3B4A36)).Put(Part.Under, Rgb(0xC9CCC4))
                    .Put(Part.Bottom, Rgb(0x4A4238)).Put(Part.BottomTrim, Rgb(0x40392F))
                    .Put(Part.Shoe, Rgb(0x2B1D14)).Put(Part.Sole, Rgb(0x1A1512)).Put(Part.Cap, Rgb(0x5B5247)).Done(),
                Who("Sofia", "ソフィア", 3, 7, true, "コロンビア", "W_Formal", 1.16f, 0.96f, 0f,
                    Rgb(0xBD8F6B), Rgb(0x2E1C12))
                    .Put(Part.Top, Rgb(0xB8322F)).Put(Part.Bottom, Rgb(0xB8322F)).Put(Part.TopTrim, Rgb(0xEDE6D8))
                    .Put(Part.Legwear, Rgb(0xE8E4DC)).Put(Part.Shoe, Rgb(0x1F2A4D)).Done(),

                // 電車。米国から来た学生と、その後輩
                Who("Emily", "エミリー", 4, 19, true, "米国", "W_Casual", 1.69f, 0.90f, 0f,
                    Rgb(0xF2D1BD), Rgb(0x7A3418))
                    .Put(Part.Top, Rgb(0x2F5C45)).Put(Part.Bottom, Rgb(0x1A1A1F))
                    .Put(Part.Shoe, Rgb(0xDCDCD6)).Put(Part.Legwear, Rgb(0xDCDCD6)).Done(),

                // 台所の一家。米国から越してきた。夫は勤め人、妻も出かける支度
                Who("Mark", "マーク", 5, 52, false, "米国", "M_Suit", 1.83f, 1.12f, 0f,
                    Rgb(0x543829), Rgb(0x262422))
                    .Put(Part.Top, Rgb(0x1F2A44)).Put(Part.Under, Rgb(0xD8DCE0)).Put(Part.Tie, Rgb(0x6E1E2A))
                    .Put(Part.Bottom, Rgb(0x1F2A44)).Put(Part.Shoe, Rgb(0x151313)).Done(),
                Who("Linda", "リンダ", 6, 49, true, "米国", "W_Suit", 1.69f, 1.08f, 0f,
                    Rgb(0x664533), Rgb(0x1A1512))
                    .Put(Part.Top, Rgb(0x9C7A55)).Put(Part.Under, Rgb(0xE8E0CC)).Put(Part.Bottom, Rgb(0x2F3036))
                    .Put(Part.Shoe, Rgb(0x3A2618)).Put(Part.Legwear, Rgb(0x3A2E2A)).Done(),

                // 教室の先生。韓国系
                Who("Lee", "リー", 7, 41, false, "韓国系（ロンドン）", "M_Casual", 1.81f, 1.00f, 0f,
                    Rgb(0xE0BA96), Rgb(0x121010))
                    .Put(Part.Top, Rgb(0x8AA6C8)).Put(Part.Bottom, Rgb(0x6A604C))
                    .Put(Part.Shoe, Rgb(0x1E1B19)).Put(Part.Sole, Rgb(0x2A2724)).Done(),

                // 公営住宅の B。イタリアから来た老夫婦
                // 背広の模型を、編んだカーディガンと開襟のシャツに塗り替える（ネクタイはシャツの色で消す）。
                // M_Casual の髪は後ろで束ねた形で、年寄りの短い白髪に見えない
                Who("Giorgio", "ジョルジョ", 8, 66, false, "イタリア", "M_Suit", 1.73f, 1.10f, 9f,
                    Rgb(0xCCA17D), Rgb(0xB8B5AE))
                    .Put(Part.Top, Rgb(0x6B5A3E)).Put(Part.Under, Rgb(0xB9C0C6)).Put(Part.Tie, Rgb(0xB9C0C6))
                    .Put(Part.Bottom, Rgb(0x55585C)).Put(Part.Shoe, Rgb(0x3B281B)).Done(),
                Who("Rosa", "ローザ", 9, 72, true, "コロンビア", "W_Formal", 1.57f, 1.08f, 13f,
                    Rgb(0xB38561), Rgb(0xA8A4A0))
                    .Put(Part.Top, Rgb(0x2E6E73)).Put(Part.Bottom, Rgb(0x2E6E73)).Put(Part.TopTrim, Rgb(0xC9A45C))
                    .Put(Part.Legwear, Rgb(0x3A3236)).Put(Part.Shoe, Rgb(0x1A1818)).Done(),
                Who("Lucas", "ルーカス", 10, 3, false, "コロンビア", "M_Hoodie", 0.98f, 1.04f, 0f,
                    Rgb(0xC29470), Rgb(0x3A2616))
                    .Put(Part.Top, Rgb(0xD9632B)).Put(Part.Bottom, Rgb(0x2A3560))
                    .Put(Part.Legwear, Rgb(0x7A7F8A)).Put(Part.Shoe, Rgb(0x3F8C5A)).Put(Part.Sole, Rgb(0xE8E8E0)).Done(),

                Who("Priya", "プリヤ", 11, 18, true, "インド系（ロンドン）", "W_Casual", 1.62f, 0.90f, 0f,
                    Rgb(0x94694D), Rgb(0x0F0C0B))
                    .Put(Part.Top, Rgb(0xC99A2E)).Put(Part.Bottom, Rgb(0x4A6490))
                    .Put(Part.Shoe, Rgb(0xE2E2DC)).Put(Part.Legwear, Rgb(0xE2E2DC)).Done(),

                // 台所の一家の息子。寝癖のまま出ていくので、制服の上にパーカー。
                // 半ズボンの模型なので、脛を制服のズボンと同じ色にして長いズボンに見せる
                Who("Daniel", "ダニエル", 12, 15, false, "米国", "M_Hoodie", 1.76f, 0.90f, 0f,
                    Rgb(0x5C3D2B), Rgb(0x16110E))
                    .Put(Part.Top, Rgb(0x44474D)).Put(Part.Bottom, Rgb(0x17181B)).Put(Part.Legwear, Rgb(0x17181B))
                    .Put(Part.Shoe, Rgb(0x121212)).Put(Part.Sole, Rgb(0xE6E6E0)).Done(),

                // 教室の二人。同じ学校の制服（臙脂の上着、白いシャツ、灰色のズボン）
                Who("Aisha", "アイシャ", 13, 16, true, "ソマリア系（ロンドン）", "W_Suit", 1.62f, 0.90f, 0f,
                    Rgb(0x734F3B), Rgb(0x100C0A))
                    .Put(Part.Top, Rgb(0x5A1A26)).Put(Part.Under, Rgb(0xE6E6E2)).Put(Part.Bottom, Rgb(0x2A2B30))
                    .Put(Part.Shoe, Rgb(0x121212)).Put(Part.Legwear, Rgb(0x1E1E22)).Done(),
                Who("Mateo", "マテオ", 14, 16, false, "メキシコ", "M_Suit", 1.72f, 0.90f, 0f,
                    Rgb(0xAD805C), Rgb(0x24170F))
                    .Put(Part.Top, Rgb(0x5A1A26)).Put(Part.Under, Rgb(0xE6E6E2)).Put(Part.Tie, Rgb(0xC9A13A))
                    .Put(Part.Bottom, Rgb(0x2A2B30)).Put(Part.Shoe, Rgb(0x121212)).Done(),

                Who("Elena", "エレナ", 15, 63, true, "イタリア", "W_Formal", 1.58f, 1.10f, 8f,
                    Rgb(0xD6AB87), Rgb(0x5A3A2C))
                    .Put(Part.Top, Rgb(0x5E3A5A)).Put(Part.Bottom, Rgb(0x5E3A5A)).Put(Part.TopTrim, Rgb(0xC9A45C))
                    .Put(Part.Legwear, Rgb(0x8C6E5E)).Put(Part.Shoe, Rgb(0x2A1E18)).Done(),
            };
        }

        static Color Rgb(int hex)
        {
            return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);
        }

        static Kit Who(string id, string name, int entry, int age, bool female, string from, string model,
            float height, float girth, float curl, Color skin, Color hair)
        {
            return new Kit(new Person
            {
                id = id,
                name = name,
                entry = entry,
                age = age,
                female = female,
                from = from,
                model = model,
                height = height,
                girth = girth,
                curl = curl,
            }, skin, hair);
        }

        /// <summary>
        /// 色を部位ごとに置いていく。置かなかった部位は、肌と髪と服から決まる色で埋める。
        /// 眉は髪を少し暗く、肌の影は肌を少し暗く、タイツと靴下は置かなければ素肌のまま
        /// </summary>
        sealed class Kit
        {
            Person person;
            readonly Color[] colors = new Color[PartCount];
            readonly bool[] set = new bool[PartCount];

            public Kit(Person person, Color skin, Color hair)
            {
                this.person = person;
                Put(Part.Skin, skin);
                Put(Part.Hair, hair);
            }

            public Kit Put(Part part, Color color)
            {
                colors[(int)part] = color;
                set[(int)part] = true;
                return this;
            }

            public Person Done()
            {
                var skin = colors[(int)Part.Skin];
                var hair = colors[(int)Part.Hair];
                Fill(Part.SkinShade, skin * 0.88f);
                Fill(Part.Brow, hair * 0.75f);
                Fill(Part.Eye, new Color(0.09f, 0.07f, 0.06f));
                Fill(Part.Top, new Color(0.35f, 0.35f, 0.37f));
                Fill(Part.Under, new Color(0.86f, 0.86f, 0.84f));
                Fill(Part.Tie, colors[(int)Part.Top] * 0.7f);
                Fill(Part.TopTrim, colors[(int)Part.Top]);
                Fill(Part.Bottom, new Color(0.22f, 0.22f, 0.24f));
                Fill(Part.BottomTrim, colors[(int)Part.Bottom] * 0.85f);
                Fill(Part.Legwear, skin);
                Fill(Part.Shoe, new Color(0.10f, 0.09f, 0.09f));
                Fill(Part.Sole, new Color(0.72f, 0.70f, 0.66f));
                Fill(Part.Cap, hair);
                for (var i = 0; i < colors.Length; i++) colors[i].a = 1f;
                person.colors = colors;
                return person;
            }

            void Fill(Part part, Color color)
            {
                if (set[(int)part]) return;
                colors[(int)part] = color;
                set[(int)part] = true;
            }
        }
    }
}
