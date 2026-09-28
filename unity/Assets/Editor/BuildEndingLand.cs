using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// エンディングの景色の帯（シナリオ設計 13.1.1）。帯ごとに時刻を進めて、一日の旅に見せる。
    ///
    /// 場面 8 と同じく、車は原点に置いたままで道と沿道を区切り（20 m）の環にして手前へ流す（<see cref="DriveWorld"/>・<see cref="RoadRing"/>）。
    /// 帯ごとに沿道の入れ物（Roadsides/BandN）を持ち、道もその中に置く（帯ごとに路面が違う）。環の枚数も帯ごと（見晴らしの良い帯ほど前を長く）。
    /// 遠景（月・星・遠くの丘や村）は環に乗せず、動かない入れ物（Backdrops/BandN）に置く。遠い物ほど動かないので、そのままで正しく見える。
    ///
    /// **帯の並び・入る曲の秒・走り・空と光は <see cref="Route"/> の一か所。** 帯を外すなら行を消して組み直す（前の帯がそのぶん長くなる）。
    /// 入る秒は曲の区切り（make-ambience.sh の 10 節で調べた波形。低音の抜けた溜めの後の頭の打ち）に合わせてある。
    ///
    /// 描く回数をおさえるため、単色の物（地面・岩・石垣・幹・羊・泡）は一枚の色見本の絵（<see cref="Swatch"/>）を貼った一つのマテリアルに焼く。
    /// 区切りあたりの描く回数は、路面・色見本・札（花と葉）と、海や湖や麦の帯ではその面の三つか四つ
    /// </summary>
    public static partial class BuildEndingLand
    {
        /// <summary>区切り 1 枚の長さ。場面 8 と同じ（環の計算に丸めが入らない数）</summary>
        public const float TileLength = 20f;
        /// <summary>車の後ろにここまで残す。どの帯も同じ（DriveWorld が一つの値で持つ）</summary>
        public const float Behind = -70f;

        /// <summary>道の半幅。土の道の二本の轍（絵の 0.3 と 0.7）が車輪の間隔（1.69 m）に乗る幅</summary>
        public const float RoadHalf = 2.0f;

        public enum Scene { Forest, Coast, Moor, Wheat, Pasture, Beech, Lake }

        /// <summary>帯の一行</summary>
        public struct Leg
        {
            public string name;
            public Scene scene;
            /// <summary>この帯へ替わる曲の秒</summary>
            public float from;
            public float speed;
            public float rough;
            /// <summary>環の区切りの枚数。前は <c>slices × 20 - 70</c> m まで敷く</summary>
            public int slices;
            public DriveSky sky;
        }

        // ---- 時刻ごとの空と光（どれも仮置き） ----------------------------------------------
        //
        // 日の向き（aim）は光の進む向き。y が 330 なら後ろの右から差す（前を向いた目にも右の窓から見る目にも、日の当たった面が手前を向く）。
        // 帯を進むほど日が回って低くなり、夕暮れで前の左から橙に差し、夜は月（前の右）と前照灯と計器の灯りだけ

        /// <summary>1. 朝の森。後ろの右の 48 度から。緑を帯びた薄い霞</summary>
        public static readonly DriveSky MorningForest = new DriveSky
        {
            sky = new Color(0.78f, 0.86f, 0.90f), haze = new Color(0.80f, 0.84f, 0.78f), density = 0.006f, mist = true,
            sun = new Color(1.00f, 0.96f, 0.86f), power = 2.3f, aim = new Vector3(48f, 330f, 0f),
            lift = new Color(0.55f, 0.60f, 0.64f), ground = new Color(0.26f, 0.25f, 0.18f), beam = 0f,
        };

        /// <summary>2. 昼前の海辺。日が高く、空が青い。霞は薄く、水平線で空に溶ける</summary>
        public static readonly DriveSky LateMorningCoast = new DriveSky
        {
            sky = new Color(0.52f, 0.70f, 0.90f), haze = new Color(0.78f, 0.85f, 0.90f), density = 0.0028f, mist = true,
            sun = new Color(1.00f, 0.97f, 0.90f), power = 2.4f, aim = new Vector3(56f, 320f, 0f),
            lift = new Color(0.58f, 0.66f, 0.76f), ground = new Color(0.30f, 0.30f, 0.26f), beam = 0f,
        };

        /// <summary>3. 昼の荒野。日はほぼ真上から。少し白ちゃけた空</summary>
        public static readonly DriveSky NoonMoor = new DriveSky
        {
            sky = new Color(0.62f, 0.74f, 0.88f), haze = new Color(0.80f, 0.84f, 0.88f), density = 0.0035f, mist = true,
            sun = new Color(1.00f, 0.98f, 0.92f), power = 2.4f, aim = new Vector3(64f, 300f, 0f),
            lift = new Color(0.58f, 0.64f, 0.72f), ground = new Color(0.30f, 0.27f, 0.26f), beam = 0f,
        };

        /// <summary>4. 昼過ぎの麦畑。日は右から少し低く、暖かい</summary>
        public static readonly DriveSky AfternoonWheat = new DriveSky
        {
            sky = new Color(0.60f, 0.72f, 0.86f), haze = new Color(0.84f, 0.84f, 0.80f), density = 0.0035f, mist = true,
            sun = new Color(1.00f, 0.94f, 0.80f), power = 2.3f, aim = new Vector3(44f, 290f, 0f),
            lift = new Color(0.56f, 0.60f, 0.66f), ground = new Color(0.32f, 0.29f, 0.22f), beam = 0f,
        };

        /// <summary>5. 夕方前の丘。日が傾いて金色。影が長い</summary>
        public static readonly DriveSky LateAfternoonPasture = new DriveSky
        {
            sky = new Color(0.66f, 0.72f, 0.82f), haze = new Color(0.88f, 0.82f, 0.70f), density = 0.004f, mist = true,
            sun = new Color(1.00f, 0.84f, 0.60f), power = 2.1f, aim = new Vector3(24f, 260f, 0f),
            lift = new Color(0.50f, 0.52f, 0.60f), ground = new Color(0.30f, 0.26f, 0.20f), beam = 0f,
        };

        /// <summary>6. 夕暮れの並木。低い橙の日が前の左から幹のあいだを抜けて、道に縞の影を落とす</summary>
        public static readonly DriveSky DuskBeech = new DriveSky
        {
            sky = new Color(0.86f, 0.62f, 0.44f), haze = new Color(0.80f, 0.56f, 0.40f), density = 0.006f, mist = true,
            sun = new Color(1.00f, 0.62f, 0.34f), power = 2.2f, aim = new Vector3(8f, 150f, 0f),
            lift = new Color(0.40f, 0.34f, 0.38f), ground = new Color(0.20f, 0.14f, 0.10f), beam = 0f,
        };

        /// <summary>
        /// 7. 夜の湖。月（前の右、低い）の青い明かりと、前照灯と計器の灯り。真っ暗にはしない（環境光を青く残す）
        /// </summary>
        public static readonly DriveSky NightLake = new DriveSky
        {
            sky = new Color(0.050f, 0.070f, 0.130f), haze = new Color(0.060f, 0.080f, 0.140f), density = 0.0025f, mist = true,
            // 月の明かりは月の円（遠景。前の右 22 度・11 度の高さ）から差す。湖の艶の照り返しが月の光の道と重なる
            sun = new Color(0.62f, 0.70f, 0.95f), power = 0.40f, aim = new Vector3(11f, 202f, 0f),
            lift = new Color(0.16f, 0.19f, 0.28f), ground = new Color(0.05f, 0.06f, 0.08f), beam = 0.22f,
        };

        /// <summary>
        /// 帯の並び。**秒は曲の区切り。** ドロップ（36.0）・76.95（溜めの後の頭）・104.45（短い溜めの後）・128.85（静かな段へ）・
        /// 159.45（溜めの後の頭）・183.6（刻みの抜ける終わりの段）。最後の湖はフェードアウトまで。
        /// 3〜5（ヒース・麦畑・石垣）はどれを外しても流れが通る。
        /// **時刻ごとの空（上の MorningForest ほか）より後に置く。** 静的な値は書いた順に決まるので、前に置くと空がどれも 0 のまま入る
        /// </summary>
        public static readonly Leg[] Route =
        {
            new Leg { name = "村の近くの森（朝）", scene = Scene.Forest, from = 0f, speed = 9f, rough = 2.5f, slices = 9, sky = MorningForest },
            new Leg { name = "海辺の崖の道（昼前）", scene = Scene.Coast, from = 36.0f, speed = 11f, rough = 2.0f, slices = 12, sky = LateMorningCoast },
            new Leg { name = "ヒースの荒野（昼）", scene = Scene.Moor, from = 76.95f, speed = 11f, rough = 2.5f, slices = 12, sky = NoonMoor },
            new Leg { name = "麦畑の丘の一本道（昼過ぎ）", scene = Scene.Wheat, from = 104.45f, speed = 11f, rough = 3.0f, slices = 12, sky = AfternoonWheat },
            new Leg { name = "石垣と羊の丘（夕方前）", scene = Scene.Pasture, from = 128.85f, speed = 10f, rough = 2.5f, slices = 12, sky = LateAfternoonPasture },
            new Leg { name = "ブナの並木（夕暮れ）", scene = Scene.Beech, from = 159.45f, speed = 10f, rough = 1.0f, slices = 9, sky = DuskBeech },
            new Leg { name = "湖沿いの道（夜）", scene = Scene.Lake, from = 183.6f, speed = 9f, rough = 1.0f, slices = 12, sky = NightLake },
        };

        // ---- 組み立て --------------------------------------------------------------------

        /// <summary>Roadsides（帯ごとの沿道。環に乗る）と Backdrops（帯ごとの遠景。動かない）を組む</summary>
        public static void Build(Transform root, StringBuilder note)
        {
            materials = null;
            var sides = Child(root, "Roadsides");
            Clear(sides);
            var far = Child(root, "Backdrops");
            Clear(far);
            var old = root.Find("Road");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            for (var b = 0; b < Route.Length; b++)
            {
                var leg = Route[b];
                var band = Child(sides, "Band" + b);
                var back = Child(far, "Band" + b);
                var stats = new Stats();
                for (var i = 0; i < leg.slices; i++)
                {
                    var slice = new GameObject("Slice" + i).transform;
                    slice.SetParent(band, false);
                    slice.localPosition = new Vector3(0f, 0f, RoadRing.Slot(i, leg.slices, TileLength, 0f, Behind));
                    var rnd = new System.Random(1309 + b * 104729 + i * 7919);
                    var s = new Slice { index = i, count = leg.slices, rnd = rnd, stats = stats, name = leg.scene + "_" + i };
                    switch (leg.scene)
                    {
                        case Scene.Forest: Woodland(s); break;
                        case Scene.Coast: Cliffs(s); break;
                        case Scene.Moor: Moorland(s); break;
                        case Scene.Wheat: Harvest(s); break;
                        case Scene.Pasture: Wolds(s); break;
                        case Scene.Beech: Beech(s); break;
                        case Scene.Lake: Lake(s); break;
                    }
                    s.Emit(slice);
                }
                Backdrop(leg.scene, back, stats);
                band.gameObject.SetActive(b == 0);
                back.gameObject.SetActive(b == 0);
                note.AppendFormat("帯 {0} {1}: 曲の {2:0.00} 秒から、区切り {3} 枚（前 {4:0} m）、札 {5}・三角 {6}（区切りあたり 札 {7:0}・三角 {8:0}）、描く回数 {9}（区切りあたり {10:0.0}）、遠景の三角 {11}・描く回数 {12}",
                    b + 1, leg.name, leg.from, leg.slices, leg.slices * TileLength + Behind, stats.cards, stats.tris,
                    stats.cards / (float)leg.slices, stats.tris / (float)leg.slices, stats.calls, stats.calls / (float)leg.slices, stats.far, stats.farCalls).AppendLine();
            }
        }

        /// <summary>帯ごとの数え</summary>
        sealed class Stats
        {
            public int cards;
            public int tris;
            public int far;
            /// <summary>描く回数（区切りの物の和。遠景は別）</summary>
            public int calls;
            public int farCalls;
        }

        /// <summary>
        /// 区切り一枚ぶんの入れ物。路面・色見本・札・水と麦の面を溜めて、最後に一度に焼く。
        /// 区切りの中の z は 0〜20（原点は手前の端。RoadRing の決まり）
        /// </summary>
        sealed class Slice
        {
            public int index;
            public int count;
            public System.Random rnd;
            public Stats stats;
            public string name;
            public readonly Paint paint = new Paint();
            public readonly Bank flora = new Bank { Texel = 1f, Rooted = true, CardLift = 1.4f };
            public readonly Bank road = new Bank { Texel = 1f };
            public readonly Bank water = new Bank { Texel = 1f };
            public readonly Bank wheat = new Bank { Texel = 1.25f, Rooted = true, CardLift = 1.0f };
            public readonly Bank field = new Bank { Texel = 0.15f, Rooted = false };
            /// <summary>野の花と木の札（EndingWild.png）。作り込んだ帯はこちらだけを使う</summary>
            public readonly Bank wild = new Bank { Texel = 1f, Rooted = true, CardLift = 1.4f };
            /// <summary>林の光の筋（加算）</summary>
            public readonly Bank shafts = new Bank { Texel = 1f };
            /// <summary>海辺の帯の海</summary>
            public readonly Bank sea = new Bank { Texel = 1f };
            /// <summary>荒野・麦畑・石垣の丘の札（EndingField.png）</summary>
            public readonly Bank farm = new Bank { Texel = 1f, Rooted = true, CardLift = 1.4f };
            /// <summary>石垣の面（村の石垣と同じ石灰岩の絵。2 m で一回り）</summary>
            public readonly Bank stone = new Bank { Texel = 0.5f };
            /// <summary>荒野の道の近くの地面（EndingHeathGround.png。4 m で一回り）</summary>
            public readonly Bank heath = new Bank { Texel = 0.25f };
            public Material roadMat;

            /// <summary>環の中での z（区切りの頭が環の頭から何 m か）。起伏の位相を環の一周で閉じるのに使う</summary>
            public float Offset { get { return index * TileLength; } }

            /// <summary>環一周の長さ</summary>
            public float Span { get { return count * TileLength; } }

            public float Next() { return (float)rnd.NextDouble(); }
            public float Range(float a, float b) { return Mathf.Lerp(a, b, (float)rnd.NextDouble()); }

            public void Emit(Transform slice)
            {
                var m = Materials();
                var dir = BuildEnding.Generated + name + "_";
                stats.cards += flora.Count / 2 + wheat.Count / 2 + wild.Count / 2 + farm.Count / 2;
                stats.tris += flora.Count + wheat.Count + paint.Count + road.Count + water.Count + field.Count + wild.Count + shafts.Count + sea.Count
                    + farm.Count + stone.Count + heath.Count;
                stats.calls += (flora.Count > 0 ? 1 : 0) + (wheat.Count > 0 ? 1 : 0) + (paint.Count > 0 ? 1 : 0) + (road.Count > 0 ? 1 : 0)
                    + (water.Count > 0 ? 1 : 0) + (field.Count > 0 ? 1 : 0) + (wild.Count > 0 ? 1 : 0) + (shafts.Count > 0 ? 1 : 0) + (sea.Count > 0 ? 1 : 0)
                    + (farm.Count > 0 ? 1 : 0) + (stone.Count > 0 ? 1 : 0) + (heath.Count > 0 ? 1 : 0);
                road.Emit(slice, "Road", roadMat != null ? roadMat : m.road, false, dir);
                var mesh = paint.Bake(dir + "Land.asset");
                if (mesh != null)
                {
                    var go = new GameObject("Land");
                    go.transform.SetParent(slice, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = m.swatch;
                }
                water.Emit(slice, "Water", m.water, false, dir);
                field.Emit(slice, "Field", m.field, false, dir);
                wheat.Emit(slice, "Wheat", m.wheat, false, dir);
                flora.Emit(slice, "Flora", m.flora, false, dir);
                wild.Emit(slice, "Wild", m.wild, false, dir);
                sea.Emit(slice, "Sea", m.sea, false, dir);
                farm.Emit(slice, "Farm", m.farm, false, dir);
                stone.Emit(slice, "Stone", m.stone, false, dir);
                heath.Emit(slice, "Heath", m.heath, false, dir);
                var beams = shafts.Emit(slice, "Shafts", m.shaft, false, dir);
                if (beams != null) beams.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ---- 色見本 ----------------------------------------------------------------------

        /// <summary>色見本の升。絵（EndingSwatch.png）の横の並び</summary>
        public enum Swatch
        {
            Grass, Floor, Bark, Rock, RockDark, Foam, Turf, Asphalt, Stone, StoneDark, Wool, Face,
            Heath, HeathDark, Litter, Pasture, Roof, Wall, HillFar, HillNight, Reed, Sand, Line, Moon, Star, Lamp,
            BeechBark, Wood, Moss, Granite, GraniteDark, Slate,
            // ここから絵の上の行（<see cref="SwatchTexture"/>）
            HeathBloom, HeathOld, HeathYoung, Burnt, Bracken, MoorGrass, Stubble, StubbleDark, Straw, StrawEnd,
            Honey, StoneSlate, Gritstone, WheatFar, Gate, Lush, HillMoor, HedgeDark,
        }

        /// <summary>升の色（sRGB）。Swatch と同じ並び</summary>
        static readonly Color32[] Tones =
        {
            new Color32(92, 118, 52, 255),   // 草の縁
            new Color32(70, 66, 40, 255),    // 森の地
            new Color32(112, 106, 98, 255),  // 幹（灰褐）
            new Color32(150, 142, 124, 255), // 崖の岩
            new Color32(98, 94, 88, 255),    // 崖の岩の陰
            new Color32(236, 240, 238, 255), // 波の泡
            new Color32(110, 130, 62, 255),  // 崖の上の芝
            new Color32(66, 66, 70, 255),    // 舗装
            new Color32(156, 150, 136, 255), // 石垣
            new Color32(118, 112, 102, 255), // 石垣の陰
            new Color32(226, 222, 208, 255), // 羊の毛
            new Color32(54, 50, 48, 255),    // 羊の顔
            new Color32(86, 60, 82, 255),    // ヒース
            new Color32(72, 54, 66, 255),    // ヒースの陰
            new Color32(126, 90, 54, 255),   // 落ち葉
            new Color32(98, 128, 60, 255),   // 牧草
            new Color32(72, 66, 64, 255),    // 屋根
            new Color32(176, 160, 130, 255), // 壁（コッツウォルズの石）
            new Color32(76, 96, 84, 255),    // 遠い丘
            new Color32(18, 24, 34, 255),    // 夜の遠い岸
            new Color32(84, 92, 54, 255),    // 葦
            new Color32(176, 164, 132, 255), // 砂利の岸
            new Color32(204, 200, 184, 255), // 白線
            new Color32(255, 250, 232, 255), // 月
            new Color32(236, 240, 255, 255), // 星
            new Color32(255, 204, 128, 255), // 窓の灯り
            new Color32(150, 146, 136, 255), // ブナの幹（滑らかな灰）
            new Color32(104, 82, 58, 255),   // 木の杭と道しるべ
            new Color32(84, 104, 52, 255),   // 苔
            new Color32(146, 140, 126, 255), // 石垣の花崗岩
            new Color32(104, 100, 92, 255),  // 石垣の花崗岩の陰
            new Color32(92, 96, 100, 255),   // 石垣の粘板岩
            new Color32(98, 62, 94, 255),    // 盛りのヒース（藤色がかった紫。昼の日と霞で持ち上がるので暗めに置く）
            new Color32(86, 70, 66, 255),    // 伸びたヒース（茶を帯びる）
            new Color32(80, 84, 54, 255),    // 焼いた後の若いヒース（緑がかる。明るいと芝生に見えた）
            new Color32(62, 52, 46, 255),    // 焼いた跡
            new Color32(100, 138, 54, 255),  // ワラビ
            new Color32(126, 120, 82, 255),  // ムーアグラス
            new Color32(206, 188, 130, 255), // 刈った畑の株
            new Color32(176, 156, 100, 255), // 刈った畑の株の陰
            new Color32(228, 200, 122, 255), // 藁の束
            new Color32(204, 174, 106, 255), // 藁の束の端（渦）
            new Color32(200, 172, 118, 255), // コッツウォルズの石（蜂蜜色）
            new Color32(108, 104, 98, 255),  // 石版の屋根
            new Color32(122, 114, 102, 255), // ヨークシャーの砂岩
            new Color32(202, 172, 92, 255),  // 遠くの麦畑
            new Color32(170, 162, 142, 255), // 風に晒された木の門
            new Color32(116, 142, 68, 255),  // 濃い牧草
            new Color32(116, 88, 112, 255),  // 遠くの荒野の丘
            new Color32(56, 76, 40, 255),    // 遠くの生け垣
        };

        /// <summary>
        /// 色見本だけを貼る面の入れ物。頂点ごとに升の真ん中の uv を持たせ、面ごとに色を変える（Bank は実寸から uv を振るので使えない）。
        /// 巻きは Bank と同じ（表から見て右回り）
        /// </summary>
        sealed class Paint
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> t = new List<int>();

            public int Count { get { return t.Count / 3; } }

            static Vector2 Uv(Swatch c)
            {
                // 初めの 32 升は絵の下の三行（真ん中の 0.5 はどちらの行でも同じ色）、33 升目からは上の行
                var i = (int)c;
                return i < SwatchWide ? new Vector2((i + 0.5f) / SwatchWide, 0.5f) : new Vector2((i - SwatchWide + 0.5f) / SwatchWide, 0.875f);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Swatch col)
            {
                var i = v.Count;
                var u = Uv(col);
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(u); uv.Add(u); uv.Add(u); uv.Add(u);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Swatch col)
            {
                var i = v.Count;
                var u = Uv(col);
                v.Add(a); v.Add(b); v.Add(c);
                uv.Add(u); uv.Add(u); uv.Add(u);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            /// <summary>上を向く水平の四角。x0 &lt; x1、z0 &lt; z1</summary>
            public void Flat(float x0, float x1, float z0, float z1, float y, Swatch col)
            {
                Quad(new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0), col);
            }

            /// <summary>箱。6 面とも外を向く</summary>
            public void Box(Vector3 centre, Vector3 size, float yaw, Swatch col, Swatch side)
            {
                var r = Quaternion.Euler(0f, yaw, 0f);
                var h = size * 0.5f;
                System.Func<float, float, float, Vector3> P = (x, y, z) => centre + r * new Vector3(h.x * x, h.y * y, h.z * z);
                Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), col);
                Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), side);
                Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), side);
                Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), side);
                Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), side);
            }

            /// <summary>多角柱。上へ r1 まで細る。top が色見本の升なら天に蓋をする。lean は天の輪を横へずらす量（傾いた幹）</summary>
            public void Prism(Vector3 foot, float r0, float r1, float high, int sides, float turn, Swatch col, Swatch? top, System.Random jitter = null, float rough = 0f,
                Vector3 lean = default(Vector3))
            {
                var ring0 = new Vector3[sides];
                var ring1 = new Vector3[sides];
                for (var i = 0; i < sides; i++)
                {
                    var a = (turn + 360f * i / sides) * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var j0 = jitter != null ? 1f + ((float)jitter.NextDouble() - 0.5f) * rough : 1f;
                    var j1 = jitter != null ? 1f + ((float)jitter.NextDouble() - 0.5f) * rough : 1f;
                    var hj = jitter != null ? ((float)jitter.NextDouble() - 0.5f) * rough * high * 0.3f : 0f;
                    ring0[i] = foot + d * r0 * j0;
                    ring1[i] = foot + d * r1 * j1 + Vector3.up * (high + hj) + lean;
                }
                for (var i = 0; i < sides; i++)
                {
                    var n = (i + 1) % sides;
                    Quad(ring0[n], ring0[i], ring1[i], ring1[n], col);
                }
                if (top.HasValue)
                {
                    var c = Vector3.zero;
                    foreach (var p in ring1) c += p;
                    c /= sides;
                    for (var i = 0; i < sides; i++) Tri(c, ring1[(i + 1) % sides], ring1[i], top.Value);
                }
            }

            /// <summary>
            /// 寝かせた丸太。a から b へ、半径 r の sides 角柱。上を向く面（法線の上向きが 0.35 を越える面）を moss、ほかを col で塗る。
            /// 両端は cap（無ければ col）の輪で塞ぐ
            /// </summary>
            public void Log(Vector3 a, Vector3 b, float r, int sides, Swatch col, Swatch moss, Swatch? cap = null)
            {
                var axis = (b - a).normalized;
                var e1 = Vector3.Cross(axis, Vector3.up).sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, axis).normalized : Vector3.right;
                // Prism と同じ巻き（輪の e1 → e2 が軸から見て Prism の x → z と同じ回り）
                var e2 = Vector3.Cross(e1, axis);
                var ring0 = new Vector3[sides];
                var ring1 = new Vector3[sides];
                var outward = new Vector3[sides];
                for (var i = 0; i < sides; i++)
                {
                    var t = 2f * Mathf.PI * i / sides;
                    var d = e1 * Mathf.Cos(t) + e2 * Mathf.Sin(t);
                    ring0[i] = a + d * r;
                    ring1[i] = b + d * r * 0.9f;
                    outward[i] = d;
                }
                for (var i = 0; i < sides; i++)
                {
                    var n = (i + 1) % sides;
                    var up = (outward[i] + outward[n]).normalized.y;
                    Quad(ring0[n], ring0[i], ring1[i], ring1[n], up > 0.35f ? moss : col);
                }
                for (var i = 0; i < sides; i++)
                {
                    var n = (i + 1) % sides;
                    Tri(b, ring1[n], ring1[i], cap ?? col);
                    Tri(a, ring0[i], ring0[n], cap ?? col);
                }
            }

            /// <summary>溜めた面を mesh にして path へ置き、読み直して返す。面が無ければ null</summary>
            public Mesh Bake(string path)
            {
                if (t.Count == 0) return null;
                var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
                if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                ProcMesh.Save(mesh, path);
                return AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
        }

        /// <summary>色見本の絵の横の画素数</summary>
        const int SwatchWide = 32;
        /// <summary>色見本の絵の縦の画素数。下の三行が初めの 32 升（どれも同じ）、上の一行が 33 升目から</summary>
        const int SwatchHigh = 4;

        // ---- アトラスの升（村の庭と同じ絵。BuildVillagePlants の Cells と Sizes から、使う物だけ） -------------

        const int AtlasUnit = 128;
        const int AtlasWide = 1024;
        const int AtlasHigh = 2048;

        enum Wild
        {
            HollyPink, HollyWhite, EchPink, EchWhite, Aster, Rudbeckia, DahliaPink,
            Catmint, Geranium, Mantle, Filler, Ivy, Sage, Potato,
            Roses, Honeysuckle, Oak, Yew, Lavender, Apple,
        }

        /// <summary>升の (x, y, 幅, 高さ)。make-garden.py の CELLS と同じ値。y は絵の上から</summary>
        static readonly int[,] Cells =
        {
            { 0, 0, 1, 3 }, { 1, 0, 1, 3 }, { 7, 0, 1, 2 }, { 0, 3, 1, 2 }, { 1, 3, 1, 2 }, { 6, 0, 1, 2 }, { 5, 0, 1, 2 },
            { 4, 2, 2, 1 }, { 6, 2, 2, 1 }, { 6, 4, 2, 1 }, { 4, 5, 2, 1 }, { 6, 5, 2, 1 }, { 0, 5, 2, 1 }, { 3, 12, 2, 1 },
            { 0, 6, 2, 2 }, { 4, 6, 2, 2 }, { 6, 8, 2, 2 }, { 6, 10, 2, 2 }, { 6, 3, 2, 1 }, { 6, 6, 2, 2 },
        };

        /// <summary>株の丈・幅・札の枚数（BuildVillagePlants の Sizes と同じ値）</summary>
        static readonly Vector3[] Sizes =
        {
            new Vector3(2.00f, 0.72f, 2), new Vector3(1.90f, 0.70f, 2), new Vector3(0.95f, 0.50f, 2), new Vector3(0.90f, 0.48f, 2),
            new Vector3(0.95f, 0.55f, 2), new Vector3(0.85f, 0.50f, 2), new Vector3(1.25f, 0.66f, 2),
            new Vector3(0.48f, 0.95f, 3), new Vector3(0.42f, 0.85f, 3), new Vector3(0.36f, 0.75f, 3), new Vector3(0.45f, 0.90f, 3),
            new Vector3(0.60f, 1.20f, 1), new Vector3(0.42f, 0.80f, 3), new Vector3(0.62f, 1.24f, 3),
            new Vector3(1.00f, 1.00f, 1), new Vector3(1.00f, 1.00f, 1), new Vector3(6.0f, 7.0f, 3), new Vector3(5.6f, 4.4f, 3),
            new Vector3(0.55f, 0.95f, 3), new Vector3(3.00f, 3.20f, 3),
        };

        static void CellUv(Wild k, out Vector2 min, out Vector2 max)
        {
            var i = (int)k;
            const float insetU = 1.5f / AtlasWide;
            const float insetV = 1.5f / AtlasHigh;
            const float u = (float)AtlasUnit / AtlasWide;
            const float v = (float)AtlasUnit / AtlasHigh;
            var x0 = Cells[i, 0] * u;
            var x1 = (Cells[i, 0] + Cells[i, 2]) * u;
            var y1 = 1f - Cells[i, 1] * v;
            var y0 = 1f - (Cells[i, 1] + Cells[i, 3]) * v;
            min = new Vector2(x0 + insetU, y0 + insetV);
            max = new Vector2(x1 - insetU, y1 - insetV);
        }

        /// <summary>株を一つ。札を向きから等しい角度で回して交差させる（村の庭の Clump と同じ）</summary>
        static void Clump(Bank f, Wild k, Vector3 foot, float scale, float yaw, Vector3 lean)
        {
            Vector2 min, max;
            CellUv(k, out min, out max);
            var size = Sizes[(int)k];
            var high = size.x * scale;
            var wide = size.y * scale;
            var cards = Mathf.Max(1, (int)size.z);
            f.RootY = foot.y;
            f.RootHigh = high;
            var up = (Vector3.up + lean).normalized * high;
            for (var c = 0; c < cards; c++)
            {
                var a = (yaw + 180f * c / cards) * Mathf.Deg2Rad;
                var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (wide * 0.5f);
                f.AtlasCard(foot + Vector3.down * 0.03f, across, up, min, max);
            }
        }

        static Wild Pick(System.Random rnd, Wild[] kinds, float[] weights)
        {
            var sum = 0f;
            foreach (var w in weights) sum += w;
            var r = (float)rnd.NextDouble() * sum;
            for (var i = 0; i < kinds.Length; i++)
            {
                r -= weights[i];
                if (r <= 0f) return kinds[i];
            }
            return kinds[kinds.Length - 1];
        }

        // ---- マテリアル ------------------------------------------------------------------

        sealed class Mats
        {
            public Material flora, swatch, road, asphalt, water, field, wheat, glow, wild, shaft, sea, farm, stone, heath, cloudLow, cloudHigh;
        }

        static Mats materials;

        static Mats Materials()
        {
            if (materials != null) return materials;
            materials = new Mats
            {
                flora = FloraMat(),
                swatch = SwatchMat(false),
                glow = SwatchMat(true),
                road = RoadMat(),
                water = WaterMat(),
                wild = WildMat(),
                shaft = ShaftMat(),
                sea = SeaMat(),
                farm = FarmMat(),
                stone = StoneMat(),
                heath = HeathGroundMat(),
                cloudLow = CloudMat("EndingCloudLow", new Color(1f, 1f, 1f, 0.88f), 0.16f, 0.34f),
                cloudHigh = CloudMat("EndingCloudHigh", new Color(0.95f, 0.97f, 1f, 0.55f), 0.28f, 0.52f),
                // 麦は場面 8 の麦（HalfAware/Wheat）をそのまま使う。場面 8 と同じ畑の絵と色
                wheat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Drive/Wheat.mat"),
                field = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Drive/FieldCrop.mat"),
            };
            if (materials.wheat == null) Debug.LogWarning("場面 8 の麦のマテリアルが無い: Assets/Materials/Drive/Wheat.mat（先に HalfAware/Build the drive）");
            return materials;
        }

        /// <summary>
        /// 花と葉の札のマテリアル。村の庭（VillageFlora.mat）の写し。絵は同じアトラスで、揺れと明るみだけこの場面の値にする。
        /// 札は道と一緒に流れて世界の位置が変わるので、揺れの位相（世界の位置から出す）が走る速さで回る。揺れは小さくおさえる
        /// </summary>
        static Material FloraMat()
        {
            const string path = BuildEnding.Materials + "EndingFlora.mat";
            var shader = Shader.Find("HalfAware/Foliage");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "EndingFlora" };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != shader) m.shader = shader;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Village/VillageFlora.png");
            if (atlas == null) Debug.LogWarning("花と葉のアトラスが無い: Assets/Textures/Village/VillageFlora.png");
            m.SetTexture("_BaseMap", atlas);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Wrap", 0.5f);
            m.SetFloat("_Glow", 0.30f);
            m.SetFloat("_Shade", 0.35f);
            m.SetFloat("_SkyLift", 0.25f);
            m.SetFloat("_Sway", 0.03f);
            m.SetFloat("_SwayRate", 1.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>路面。村の路面の絵（二本の轍と真ん中の草の筋）を、道に沿って繰り返す</summary>
        static Material RoadMat()
        {
            var m = Lit("EndingRoad", Color.white, 0.06f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Village/VillageRoad.png");
            if (tex == null) Debug.LogWarning("路面の絵が無い: Assets/Textures/Village/VillageRoad.png");
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 色見本のマテリアル。glow なら Unlit（月・星・窓の灯り。灯りを増やさずに自分で光って見せる）
        /// </summary>
        static Material SwatchMat(bool glow)
        {
            var name = glow ? "EndingGlow" : "EndingSwatch";
            var path = BuildEnding.Materials + name + ".mat";
            var shader = Shader.Find(glow ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", SwatchTexture());
            m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>水（海と湖）。色と、細かい波の明暗の絵（<see cref="WaterTexture"/>）。艶で日の照り返しを拾う</summary>
        static Material WaterMat()
        {
            var m = Lit("EndingWater", new Color(0.10f, 0.30f, 0.38f), 0.80f);
            m.SetTexture("_BaseMap", WaterTexture());
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Lit(string name, Color col, float smooth)
        {
            var path = BuildEnding.Materials + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 色見本の絵。1 画素ずつの升を横に並べる。最近傍・ミップ無し（隣の升が滲まない）。
        /// 32 升を越えた分は上の行に置く。下の三行は初めの 32 升を繰り返し、升の uv の縦を 0.5 のまま保つ
        /// （前からの帯のメッシュの uv が変わらない）
        /// </summary>
        static Texture2D SwatchTexture()
        {
            var tex = new Texture2D(SwatchWide, SwatchHigh, TextureFormat.RGBA32, false, false);
            var px = new Color32[SwatchWide * SwatchHigh];
            for (var y = 0; y < SwatchHigh; y++)
                for (var i = 0; i < SwatchWide; i++)
                {
                    var k = y == SwatchHigh - 1 ? SwatchWide + i : i;
                    px[y * SwatchWide + i] = k < Tones.Length ? Tones[k] : new Color32(255, 0, 255, 255);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return SavePng(tex, BuildEnding.Textures + "EndingSwatch.png", FilterMode.Point, TextureWrapMode.Clamp, true);
        }

        /// <summary>水の細かい波。横に長い明暗の筋を散らした 64×64 の繰り返しの絵（平均はほぼ白）</summary>
        static Texture2D WaterTexture()
        {
            const int n = 64;
            var rnd = new System.Random(4242);
            var px = new Color32[n * n];
            for (var i = 0; i < px.Length; i++) px[i] = new Color32(222, 222, 222, 255);
            for (var k = 0; k < 90; k++)
            {
                var y = rnd.Next(n);
                var x = rnd.Next(n);
                var len = 4 + rnd.Next(14);
                var hi = rnd.NextDouble() < 0.5;
                for (var d = 0; d < len; d++)
                {
                    var xx = (x + d) % n;
                    px[y * n + xx] = hi ? new Color32(255, 255, 255, 255) : new Color32(176, 180, 184, 255);
                }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(px);
            tex.Apply();
            return SavePng(tex, BuildEnding.Textures + "EndingWater.png", FilterMode.Point, TextureWrapMode.Repeat, false);
        }

        /// <summary>絵を path へ置き（中身が変わった時だけ書く）、取り込みを決めて読み直す。tex は捨てる</summary>
        static Texture2D SavePng(Texture2D tex, string path, FilterMode filter, TextureWrapMode wrap, bool linearSwatch)
        {
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            var full = Path.Combine(Directory.GetCurrentDirectory(), path);
            var same = File.Exists(full) && File.ReadAllBytes(full).Length == bytes.Length && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(full), bytes);
            if (!same) File.WriteAllBytes(full, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.mipmapEnabled || imp.filterMode != filter || imp.wrapMode != wrap || imp.textureCompression != TextureImporterCompression.Uncompressed))
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.mipmapEnabled = false;
                imp.filterMode = filter;
                imp.wrapMode = wrap;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}
