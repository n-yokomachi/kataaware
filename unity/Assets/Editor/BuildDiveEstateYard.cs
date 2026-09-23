using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公営住宅（council estate）の外。手すりの外の景色と、隣の棟から中の棟まで。その先は書き割り。
    ///
    /// **舞台はロンドン。** 日本の団地として組んでいた頃の電柱と電線、室外機、集合ポスト、物干し台、
    /// 駐輪場、ガードレール、砂場、給水塔は外した（設計書 9.1 節「敷地と遠景の中身（ロンドン）」）。
    ///
    /// **手すりの向こうは背景ではなく距離の作り。** 棟を一つ立てただけでは、
    /// 廊下を歩いても景色が動かず、一枚の書き割りになる。
    /// 高さと細かさの違う物を、三つの層に分けて重ねる。
    ///
    /// <list type="table">
    /// <item><term>手前 (z -15 〜 9.5)</term><description>
    /// 敷地と道路。芝生とプラタナス、低い煉瓦の塀、車止め、白線の駐車場と車、煉瓦のごみ置き場、
    /// 柵に囲われた遊び場、背の高い街灯、赤い郵便ポスト、バス停と標識、縁石沿いの黄色い二重線。
    /// 形が細かく、廊下を歩くと画面の上でいちばん大きく動く</description></item>
    /// <item><term>中 (中心から 60 m まで)</term><description>
    /// 同じ型のデッキアクセスの棟、塔状の高層棟、煙突の並ぶ煉瓦の長屋（<c>BuildDiveEstateMid.cs</c>）</description></item>
    /// <item><term>奥 (中心から 80 m より先)</term><description>
    /// 組まずに撮って貼る書き割り（<c>BuildDiveEstateFar.cs</c>）。本物の地面は中心から 80 m の
    /// 正十六角形で切れ、そこから先の地面も街並みも、板の輪に貼った絵が持つ</description></item>
    /// </list>
    ///
    /// **敷地の物は自前の入れ物とマテリアルで出す。** 棟の入れ物（<see cref="EstateBanks"/>）へ混ぜると、
    /// 棟を組み直す担当と敷地を組み直す担当の頂点とマテリアルが一つの mesh に絡む。
    /// 入れ物は <see cref="YardBanks"/>、マテリアルは <c>EstateYard*</c> の名前で持つ。
    ///
    /// **棟に関わる位置は <see cref="EstateFace"/> と <see cref="Floor"/> などの定数から導く。**
    /// 実寸で決まっている物（塀 0.9 m、車 4 m、ポスト 1.5 m、街灯 8 m）はそのまま書く
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 敷地の寸法 --------------------------------------------------------

        /// <summary>敷地の西の端。駐車場の西の塀</summary>
        const float YardWest = -13.4f;
        /// <summary>敷地の東の端。ごみ置き場と遊び場の東の塀</summary>
        const float YardEast = 24.4f;
        /// <summary>敷地と歩道の境。低い煉瓦の塀がここを通る</summary>
        const float YardEdge = -0.9f;
        /// <summary>道路の手前の縁石</summary>
        const float RoadNear = 2.4f;
        /// <summary>道路の向こうの縁石。ここから隣の棟の足元まで歩道</summary>
        const float RoadFar = 7.2f;
        /// <summary>道路の真ん中の線</summary>
        const float RoadMid = (RoadNear + RoadFar) * 0.5f;

        /// <summary>敷地の南の端。見えない仕切りの少し内側</summary>
        const float YardSouth = EstateFace + 0.2f;                                         // -14.6
        /// <summary>下のメゾネットの前庭の北の縁（棟の担当の前庭の塀）。棟の面から 3 m</summary>
        const float YardGardens = EstateFace + 3.0f;                                       // -11.8
        /// <summary>前庭の門の前を東西に通る歩道の北の縁。ここから北が芝生</summary>
        const float YardPath = YardGardens + 1.0f;                                         // -10.8
        /// <summary>
        /// 棟の東の妻の外面と、外階段の西の面（棟の担当の値を写したもの）。
        /// 敷地の物はこの内側へ食い込ませない
        /// </summary>
        const float YardBlockEast = 17.6f;
        const float YardStairWest = -1.2f;
        /// <summary>階段の下から敷地の門まで北へ通る小道の東の縁</summary>
        const float YardWalkEast = 2.4f;

        // ---- 入れ物 ------------------------------------------------------------

        /// <summary>
        /// 敷地の素材ごとの入れ物。溜めてから <see cref="Emit"/> で一枚ずつ焼く。
        ///
        /// 灯りを受ける Lit の地の色。**どれも実物より暗く置く**（<see cref="Tone"/> と同じ考え）。
        /// 記憶ごとの色味は Volume が寄せるので、地を明るく置くと白く飛ぶ
        /// </summary>
        sealed class YardBanks
        {
            public readonly Bank Brick = new Bank { Texel = 0.5f };
            public readonly Bank Grass = new Bank { Texel = 0.3f };
            public readonly Bank Tarmac = new Bank { Texel = 0.3f };
            public readonly Bank Kerb = new Bank { Texel = 0.5f };
            public readonly Bank Line = new Bank { Texel = 0.5f };
            public readonly Bank Yellow = new Bank { Texel = 0.5f };
            public readonly Bank Red = new Bank { Texel = 0.5f };
            public readonly Bank Iron = new Bank { Texel = 0.5f };
            public readonly Bank Pole = new Bank { Texel = 0.5f };
            public readonly Bank Glass = new Bank { Texel = 0.5f };
            public readonly Bank Leaf = new Bank { Texel = 0.4f };
            public readonly Bank Bark = new Bank { Texel = 0.5f };
            public readonly Bank Crown = new Bank { Texel = 1f };
            public readonly Bank Rubber = new Bank { Texel = 0.3f };
            public readonly Bank Timber = new Bank { Texel = 0.5f };
            // 中の層（BuildDiveEstateMid.cs）の棟と長屋
            public readonly Bank NextBrick = new Bank { Texel = 0.3f };
            public readonly Bank NextCrete = new Bank { Texel = 0.3f };
            public readonly Bank Stock = new Bank { Texel = 0.3f };
            public readonly Bank Slate = new Bank { Texel = 0.3f };
            public readonly Bank Pane = new Bank { Texel = 0.5f };
            public readonly Bank Lit = new Bank { Texel = 0.5f };

            public void Emit(Transform place)
            {
                // 煉瓦はロンドンの公営住宅の赤茶。縁石と笠石は明るいコンクリート
                EstateEmit(place, "EstateYardBrick", Brick, EstatePaint("EstateYardBrick", new Color(0.36f, 0.19f, 0.13f), 0.05f), false);
                // 三月の頭の芝。まだ色が浅い
                EstateEmit(place, "EstateYardGrass", Grass, EstatePaint("EstateYardGrass", new Color(0.20f, 0.26f, 0.11f), 0.03f), false);
                EstateEmit(place, "EstateYardTarmac", Tarmac, EstatePaint("EstateYardTarmac", new Color(0.12f, 0.12f, 0.125f), 0.10f), false);
                EstateEmit(place, "EstateYardKerb", Kerb, EstatePaint("EstateYardKerb", new Color(0.50f, 0.49f, 0.46f), 0.06f), false);
                EstateEmit(place, "EstateYardLine", Line, EstatePaint("EstateYardLine", new Color(0.78f, 0.78f, 0.75f), 0.10f), false);
                EstateEmit(place, "EstateYardYellow", Yellow, EstatePaint("EstateYardYellow", new Color(0.72f, 0.55f, 0.08f), 0.10f), false);
                // 郵便ポストの赤。艶を持たせて、日の側で縁が光るように
                EstateEmit(place, "EstateYardRed", Red, EstatePaint("EstateYardRed", new Color(0.55f, 0.05f, 0.04f), 0.45f), false);
                EstateEmit(place, "EstateYardIron", Iron, EstatePaint("EstateYardIron", new Color(0.05f, 0.052f, 0.055f), 0.35f), false);
                EstateEmit(place, "EstateYardPole", Pole, EstatePaint("EstateYardPole", new Color(0.42f, 0.43f, 0.44f), 0.35f), false);
                EstateEmit(place, "EstateYardGlass", Glass, EstatePaint("EstateYardGlass", new Color(0.16f, 0.19f, 0.21f), 0.85f), false);
                // 植え込みの常緑。名前は前からの EstateLeaf のまま
                EstateEmit(place, "EstateLeaf", Leaf, EstatePaint("EstateLeaf", new Color(0.118f, 0.152f, 0.098f), 0.05f), false);
                // プラタナスの幹は皮が剥げて白っぽい斑になる。遠目には明るい灰色の柱
                EstateEmit(place, "EstateYardBark", Bark, EstatePaint("EstateYardBark", new Color(0.46f, 0.44f, 0.37f), 0.04f), false);
                // 三月の頭はまだ葉が無い。樹冠は枝を描いた絵を札に貼り、枝の隙間から向こうを透かす
                NoShadow(EstateEmit(place, "EstateYardCrown", Crown, YardCrownMat(), false));
                // 遊び場の下の柔らかい舗装。煉瓦より明るい赤土色
                EstateEmit(place, "EstateYardRubber", Rubber, EstatePaint("EstateYardRubber", new Color(0.36f, 0.14f, 0.09f), 0.04f), false);
                EstateEmit(place, "EstateYardTimber", Timber, Mat("Timber"), false);

                // 中の層の棟と長屋。**影を落とさない。** 日は棟の向こう（北北東）の低い所にあるので、
                // 落とすと隣の棟の影が道路を越えて敷地の半分まで伸び、朝の庭が日陰に沈む。
                // 前の隣の棟（灯りを受けない書き割り）も影を落としていなかったので、庭の明るさはそれに揃える
                NoShadow(EstateEmit(place, "EstateNextBrick", NextBrick, MidPaint("EstateNextBrick", new Color(0.36f, 0.22f, 0.17f), 0.35f, 0.05f), false));
                NoShadow(EstateEmit(place, "EstateNextCrete", NextCrete, MidPaint("EstateNextCrete", new Color(0.56f, 0.55f, 0.52f), 0.35f, 0.05f), false));
                // 長屋のストック煉瓦。黄色がかった灰茶
                NoShadow(EstateEmit(place, "EstateMidStock", Stock, MidPaint("EstateMidStock", new Color(0.46f, 0.37f, 0.25f), 0.35f, 0.05f), false));
                NoShadow(EstateEmit(place, "EstateMidSlate", Slate, MidPaint("EstateMidSlate", new Color(0.21f, 0.22f, 0.25f), 0.30f, 0.30f), false));
                NoShadow(EstateEmit(place, "EstateMidPane", Pane, MidPaint("EstateMidPane", new Color(0.07f, 0.08f, 0.09f), 0.30f, 0.70f), false));
                // 朝の七時。灯りの入った窓が少し
                NoShadow(EstateEmit(place, "EstateMidLit", Lit, Glow("EstateMidLit", new Color(1f, 0.86f, 0.62f), 0.90f), false));
            }
        }

        /// <summary>
        /// 棟の担当の <c>Estate()</c> から呼ばれる口。**ここでは何も溜めない。**
        /// 敷地は自前のマテリアルを場所の Transform に出すので、<see cref="EstateBlockWall"/> の側
        /// （<see cref="EstateGrounds"/>）で組む。呼び口の名前と引数は棟の担当と取り決めてあるので残す
        /// </summary>
        static void EstateYard(EstateBanks b)
        {
        }

        // ---- 手すりの外・手前の層 ----------------------------------------------

        /// <summary>
        /// 敷地と道路をまとめて組む。出すのは <see cref="EstateBlockWall"/>。
        ///
        /// 一目で分かる形を三つ大きく置く。**低い煉瓦の塀**、**背の高い街灯の列**、**葉の落ちたプラタナス**。
        /// 街灯と木は廊下の目の高さ（<see cref="EstateTop"/> + 1.62）の下から上まで跨ぐので、
        /// 廊下を歩くと、隣の棟の前を横切るように動く
        /// </summary>
        static void EstateGrounds(YardBanks y)
        {
            YardSurface(y);
            YardWall(y);
            YardParking(y);
            YardBins(y);
            YardPlay(y);
            YardGreen(y);
            YardRoad(y);
            YardStreet(y);
        }

        /// <summary>
        /// 地面の塗り分け。棟の担当の地面（コンクリート）の上に、芝と歩道の目地を 1 cm 浮かせて重ねる。
        ///
        /// **舗装はコンクリートの地のまま残す。** 階段の下から門へ出る小道と、前庭の門の前の歩道は、
        /// 芝を張らない所がそのまま道になる
        /// </summary>
        static void YardSurface(YardBanks y)
        {
            // 芝。小道の東から東の塀まで。遊び場の下は上から別の面が重なる
            y.Grass.FaceY(0.010f, YardWalkEast + 0.5f, YardEast - 0.2f, YardPath + 0.5f, YardEdge - 0.6f, 1);
            // 棟の東の妻の脇、ごみ置き場の西
            y.Grass.FaceY(0.010f, YardBlockEast + 0.3f, 19.4f, YardSouth, YardGardens, 1);

            // 手前の歩道の敷石の目地。1.8 m ごと。等間隔の線が、歩いたときに足元ほど速く流れる
            for (var x = -13.5f; x < 25f; x += 1.8f)
                y.Tarmac.FaceY(0.008f, x - 0.025f, x + 0.025f, YardEdge + 0.2f, RoadNear - 0.2f, 1);
            y.Tarmac.FaceY(0.008f, -14f, 25f, 0.62f, 0.67f, 1);
            // 向こうの歩道
            for (var x = -13.5f; x < 25f; x += 1.8f)
                y.Tarmac.FaceY(0.008f, x - 0.025f, x + 0.025f, RoadFar + 0.2f, BlockFace, 1);
        }

        /// <summary>
        /// 低い煉瓦の塀。敷地の北（歩道との境）と東西を回る。
        /// 3 m ごとに少し高い柱を立て、上に明るい笠石を載せる。**等間隔の柱**が、塀を塀に見せる。
        /// 駐車場の入口と、階段の下から出る小道の門は開けておく
        /// </summary>
        static void YardWall(YardBanks y)
        {
            const float high = 0.9f;
            const float thick = 0.33f;
            // 北の塀。開ける所を除いて区切る
            var runs = new[]
            {
                new Vector2(YardWest, -12.8f),                     // 駐車場の入口の西
                new Vector2(-9.6f, YardStairWest + 0.3f),          // 入口の東から門の西まで
                new Vector2(YardWalkEast - 0.3f, YardEast),        // 門の東から東の端まで
            };
            foreach (var r in runs)
            {
                y.Brick.Box(new Vector3((r.x + r.y) * 0.5f, high * 0.5f, YardEdge), new Vector3(r.y - r.x, high, thick));
                y.Kerb.Box(new Vector3((r.x + r.y) * 0.5f, high + 0.035f, YardEdge), new Vector3(r.y - r.x + 0.04f, 0.07f, thick + 0.08f));
                // 両端と 3 m ごとの柱
                var count = Mathf.Max(1, Mathf.RoundToInt((r.y - r.x) / 3f));
                for (var i = 0; i <= count; i++)
                    YardPier(y, new Vector3(Mathf.Lerp(r.x + 0.22f, r.y - 0.22f, i / (float)count), 0f, YardEdge));
            }
            // 東西の塀。北の塀から南の端まで
            foreach (var x in new[] { YardWest, YardEast })
            {
                const float z0 = YardSouth;
                const float z1 = YardEdge;
                y.Brick.Box(new Vector3(x, high * 0.5f, (z0 + z1) * 0.5f), new Vector3(thick, high, z1 - z0));
                y.Kerb.Box(new Vector3(x, high + 0.035f, (z0 + z1) * 0.5f), new Vector3(thick + 0.08f, 0.07f, z1 - z0 + 0.04f));
                for (var i = 0; i <= 4; i++)
                    YardPier(y, new Vector3(x, 0f, Mathf.Lerp(z0 + 0.22f, z1 - 0.22f, i / 4f)));
            }

            // 門の車止め。車が小道へ入らないように、開けた所に三本
            for (var i = 0; i < 3; i++)
                YardBollard(y, new Vector3(Mathf.Lerp(YardStairWest + 0.6f, YardWalkEast - 0.6f, i / 2f), 0f, YardEdge));

            // 団地の名前の板。門の東、塀の内側に二本の脚で立てる。字は読めなくてよい
            const float boardX = YardWalkEast + 1.3f;
            const float boardZ = YardEdge - 1.0f;
            for (var i = 0; i < 2; i++)
                y.Iron.Box(new Vector3(boardX - 0.85f + i * 1.7f, 0.75f, boardZ), new Vector3(0.08f, 1.5f, 0.08f));
            y.Line.Box(new Vector3(boardX, 1.22f, boardZ), new Vector3(2.0f, 0.62f, 0.05f));
            y.Iron.Box(new Vector3(boardX, 1.40f, boardZ + 0.03f), new Vector3(1.8f, 0.12f, 0.02f));
        }

        /// <summary>塀の柱。塀より 15 cm 高く、少し太い</summary>
        static void YardPier(YardBanks y, Vector3 foot)
        {
            y.Brick.Box(foot + new Vector3(0f, 0.525f, 0f), new Vector3(0.46f, 1.05f, 0.46f));
            y.Kerb.Box(foot + new Vector3(0f, 1.09f, 0f), new Vector3(0.54f, 0.08f, 0.54f));
        }

        /// <summary>黒い鋳物の車止め。胴と、少し太い頭と、首の輪</summary>
        static void YardBollard(YardBanks y, Vector3 foot)
        {
            y.Iron.Box(foot + new Vector3(0f, 0.45f, 0f), new Vector3(0.13f, 0.90f, 0.13f));
            y.Iron.Box(foot + new Vector3(0f, 0.93f, 0f), new Vector3(0.17f, 0.08f, 0.17f));
            y.Iron.Box(foot + new Vector3(0f, 0.72f, 0f), new Vector3(0.16f, 0.04f, 0.16f));
        }

        /// <summary>
        /// 駐車場と車。棟の西、外階段の脇。
        ///
        /// **暗い舗装に白線が等間隔で並ぶ形を、細かい作り込みより先に置く。**
        /// 南の列が四区画、北の列が二区画。二つの列のあいだを通路が通り、西の端から北へ抜けて
        /// 塀の切れ目から道路へ出る。北東の角は芝を張って木を一本。車は五台。
        /// 空いた区画があると、白線の升目が読める
        /// </summary>
        static void YardParking(YardBanks y)
        {
            const float x0 = YardWest + 0.2f;             // -13.2
            const float x1 = YardStairWest - 0.4f;        // -1.6
            const float bay = 2.5f;
            const float southA = YardSouth + 0.2f;        // -14.4
            const float northA = southA + 4.8f;           // -9.6
            const float southB = northA + 4.0f;           // -5.6
            const float northB = YardEdge - 0.6f;         // -1.5
            y.Tarmac.FaceY(0.012f, x0, x1, YardSouth, YardEdge - 0.2f, 1);
            // 入口から道路まで、歩道を渡る舗装
            y.Tarmac.FaceY(0.012f, -12.8f, -9.6f, YardEdge - 0.2f, RoadNear, 1);

            // 南の列。区画の脇の線と、奥を閉じる線
            for (var i = 0; i <= 4; i++)
            {
                var x = -13.0f + i * bay;
                y.Line.FaceY(0.02f, x - 0.05f, x + 0.05f, southA, northA, 1);
            }
            y.Line.FaceY(0.02f, -13.0f, -13.0f + 4f * bay, southA - 0.05f, southA + 0.05f, 1);
            // 北の列。西の端は道路へ抜ける通路にする。東の端の一区画ぶんは芝を張って木を植える
            for (var i = 0; i <= 2; i++)
            {
                var x = -9.4f + i * bay;
                y.Line.FaceY(0.02f, x - 0.05f, x + 0.05f, southB, northB, 1);
            }
            y.Line.FaceY(0.02f, -9.4f, -9.4f + 2f * bay, northB - 0.05f, northB + 0.05f, 1);
            y.Grass.FaceY(0.02f, -9.4f + 2f * bay + 0.2f, x1 - 0.1f, southB + 0.2f, YardEdge - 0.3f, 1);
            y.Kerb.Box(new Vector3((-9.4f + 2f * bay + x1) * 0.5f, 0.06f, southB + 0.1f), new Vector3(x1 - (-9.4f + 2f * bay), 0.12f, 0.2f));
            y.Kerb.Box(new Vector3(-9.4f + 2f * bay + 0.1f, 0.06f, (southB + YardEdge) * 0.5f), new Vector3(0.2f, 0.12f, YardEdge - southB));

            // 車止め。駐車場と小道の境に等間隔で
            for (var i = 0; i < 7; i++)
                YardBollard(y, new Vector3(x1 + 0.2f, 0f, Mathf.Lerp(southA + 0.8f, northB - 0.3f, i / 6f)));

            // 入口の脇の札（住人の車だけ、の類い）。白地に黒い帯
            y.Pole.Box(new Vector3(-9.2f, 1.1f, YardEdge - 0.5f), new Vector3(0.07f, 2.2f, 0.07f));
            y.Line.Box(new Vector3(-9.2f, 1.95f, YardEdge - 0.46f), new Vector3(0.50f, 0.66f, 0.03f));
            y.Iron.Box(new Vector3(-9.2f, 2.20f, YardEdge - 0.44f), new Vector3(0.44f, 0.08f, 0.01f));

            // 五台。色を分けるのは、同じ色が並ぶと一つの塊に見えるため
            const float zA = (southA + northA) * 0.5f + 0.2f;
            const float zB = (southB + northB) * 0.5f;
            YardCar(y, y.Red, -13.0f + bay * 0.5f, zA);
            YardCar(y, y.Pole, -13.0f + bay * 1.5f, zA);
            YardCar(y, y.Iron, -13.0f + bay * 3.5f, zA);
            YardCar(y, y.Line, -9.4f + bay * 0.5f, zB);
            YardCar(y, y.Pole, -9.4f + bay * 1.5f, zB);
        }

        /// <summary>
        /// 車一台。ロンドンの路上に多い小さなハッチバック。地と客室の箱に、窓の帯と車輪を重ねる。
        /// 窓の帯が無いと、白い箱が冷蔵庫に見える
        /// </summary>
        static void YardCar(YardBanks y, Bank paint, float x, float z)
        {
            paint.Box(new Vector3(x, 0.55f, z), new Vector3(1.72f, 0.62f, 4.05f));
            paint.Box(new Vector3(x, 1.10f, z - 0.25f), new Vector3(1.56f, 0.56f, 2.50f));
            y.Glass.Box(new Vector3(x, 1.14f, z - 0.25f), new Vector3(1.60f, 0.36f, 2.54f));
            // 前の窓は斜めに寝かせる。立てると箱に見える
            y.Glass.Box(new Vector3(x, 1.06f, z + 1.10f), new Vector3(1.50f, 0.05f, 0.62f), Quaternion.Euler(-32f, 0f, 0f));
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                    y.Iron.Box(new Vector3(x + (i * 2 - 1) * 0.78f, 0.30f, z + (j * 2 - 1) * 1.30f), new Vector3(0.22f, 0.60f, 0.60f));
            // 灯り。前は白、後ろは赤
            y.Line.Box(new Vector3(x, 0.72f, z + 2.03f), new Vector3(1.40f, 0.12f, 0.02f));
            y.Red.Box(new Vector3(x, 0.78f, z - 2.03f), new Vector3(1.40f, 0.12f, 0.02f));
        }

        /// <summary>
        /// ごみ置き場。棟の東の妻の脇に、煉瓦で三方を囲い、前に黒い鉄の扉。
        /// 中に大きな共用のごみ箱（車輪付きの金属の箱）を三つ、外に一つと家庭用の小さい箱を二つ。
        /// 公営住宅の裏手の顔になる
        /// </summary>
        static void YardBins(YardBanks y)
        {
            const float x0 = 19.8f;
            const float x1 = 23.8f;
            const float z0 = YardSouth;
            const float z1 = YardGardens + 0.4f;          // -11.4
            const float high = 1.8f;
            y.Brick.Box(new Vector3((x0 + x1) * 0.5f, high * 0.5f, z0 + 0.15f), new Vector3(x1 - x0 + 0.3f, high, 0.3f));
            y.Brick.Box(new Vector3(x0, high * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.3f, high, z1 - z0));
            y.Brick.Box(new Vector3(x1, high * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.3f, high, z1 - z0));
            y.Kerb.Box(new Vector3((x0 + x1) * 0.5f, high + 0.04f, z0 + 0.15f), new Vector3(x1 - x0 + 0.4f, 0.08f, 0.4f));
            y.Kerb.Box(new Vector3(x0, high + 0.04f, (z0 + z1) * 0.5f), new Vector3(0.4f, 0.08f, z1 - z0));
            y.Kerb.Box(new Vector3(x1, high + 0.04f, (z0 + z1) * 0.5f), new Vector3(0.4f, 0.08f, z1 - z0));
            // 扉。西の一枚は閉まり、東の一枚は外へ開いたまま
            const float leaf = (x1 - x0) * 0.5f - 0.2f;
            y.Iron.Box(new Vector3(x0 + 0.15f + leaf * 0.5f, 0.95f, z1), new Vector3(leaf, 1.55f, 0.05f));
            var hinge = new Vector3(x1 - 0.15f, 0.95f, z1);
            var open = Quaternion.Euler(0f, 70f, 0f);
            y.Iron.Box(hinge + open * new Vector3(-leaf * 0.5f, 0f, 0f), new Vector3(leaf, 1.55f, 0.05f), open);
            // 共用のごみ箱
            for (var i = 0; i < 3; i++)
                YardEurobin(y, new Vector3(x0 + 0.85f + i * 1.15f, 0f, z0 + 1.1f));
            // 一つは回収の日に扉の前へ出したまま
            YardEurobin(y, new Vector3((x0 + x1) * 0.5f - 0.4f, 0f, z1 + 0.9f));
            // 家庭用の小さい箱。蓋の色を変える
            for (var i = 0; i < 2; i++)
            {
                var at = new Vector3(x0 - 0.7f - i * 0.72f, 0f, z1 - 0.35f);
                y.Iron.Box(at + new Vector3(0f, 0.50f, 0f), new Vector3(0.58f, 0.95f, 0.70f));
                (i == 0 ? y.Leaf : y.Pole).Box(at + new Vector3(0f, 1.00f, 0f), new Vector3(0.62f, 0.06f, 0.74f));
            }
        }

        /// <summary>車輪付きの大きなごみ箱。灰色の胴に、黒く丸い蓋</summary>
        static void YardEurobin(YardBanks y, Vector3 foot)
        {
            y.Pole.Box(foot + new Vector3(0f, 0.72f, 0f), new Vector3(1.10f, 1.08f, 0.95f));
            y.Iron.Box(foot + new Vector3(0f, 1.30f, 0f), new Vector3(1.16f, 0.10f, 1.00f));
            y.Iron.Box(foot + new Vector3(0f, 1.38f, 0f), new Vector3(1.00f, 0.08f, 0.70f));
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                    y.Iron.Box(foot + new Vector3((i * 2 - 1) * 0.42f, 0.09f, (j * 2 - 1) * 0.34f), new Vector3(0.12f, 0.18f, 0.12f));
        }

        /// <summary>
        /// 柵に囲われた遊び場。敷地の東、芝の上。
        ///
        /// **黒い鉄の柵で四方を囲う。** 支柱が等間隔に並ぶ柵が、ここが子どもの遊び場だと先に伝える。
        /// 中は柔らかい赤土色の舗装に、滑り台の付いた塔、ぶらんこ、揺れる乗り物。外にベンチ
        /// </summary>
        static void YardPlay(YardBanks y)
        {
            const float x0 = 12.8f;
            const float x1 = 22.8f;
            const float z0 = YardPath + 1.2f;             // -9.6
            const float z1 = YardEdge - 2.3f;             // -3.2
            y.Rubber.FaceY(0.02f, x0, x1, z0, z1, 1);

            // 柵。高さ 1 m、縦の桟を 0.25 m ごと。西の辺に門を開ける
            const float high = 1.0f;
            const float gate0 = -6.9f;
            const float gate1 = -5.9f;
            YardRailing(y, new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), high);
            YardRailing(y, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), high);
            YardRailing(y, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), high);
            YardRailing(y, new Vector3(x0, 0f, z0), new Vector3(x0, 0f, gate0), high);
            YardRailing(y, new Vector3(x0, 0f, gate1), new Vector3(x0, 0f, z1), high);

            // 塔と滑り台。台は 1.4 m、屋根は赤
            const float tx = 15.4f;
            const float tz = -5.2f;
            const float deck = 1.4f;
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                    y.Pole.Box(new Vector3(tx + (i * 2 - 1) * 0.65f, 1.35f, tz + (j * 2 - 1) * 0.65f), new Vector3(0.10f, 2.7f, 0.10f));
            y.Timber.Box(new Vector3(tx, deck, tz), new Vector3(1.4f, 0.08f, 1.4f));
            y.Red.Box(new Vector3(tx, 2.78f, tz), new Vector3(1.7f, 0.16f, 1.7f));
            y.Red.Box(new Vector3(tx, 2.98f, tz), new Vector3(1.0f, 0.24f, 1.0f));
            // 台の腰の板。黄色
            y.Yellow.Box(new Vector3(tx, deck + 0.35f, tz + 0.66f), new Vector3(1.3f, 0.5f, 0.04f));
            y.Yellow.Box(new Vector3(tx, deck + 0.35f, tz - 0.66f), new Vector3(1.3f, 0.5f, 0.04f));
            // 滑り台。東へ下りる
            var slope = Quaternion.Euler(0f, 0f, -26f);
            y.Yellow.Box(new Vector3(tx + 1.95f, deck * 0.5f + 0.05f, tz), new Vector3(2.8f, 0.06f, 0.62f), slope);
            y.Yellow.Box(new Vector3(tx + 1.95f, deck * 0.5f + 0.22f, tz + 0.32f), new Vector3(2.8f, 0.28f, 0.04f), slope);
            y.Yellow.Box(new Vector3(tx + 1.95f, deck * 0.5f + 0.22f, tz - 0.32f), new Vector3(2.8f, 0.28f, 0.04f), slope);
            // 梯子。西から上がる
            for (var i = 0; i < 2; i++)
                y.Pole.Box(new Vector3(tx - 1.05f, deck * 0.5f, tz + (i * 2 - 1) * 0.28f), new Vector3(0.06f, deck + 0.2f, 0.06f),
                    Quaternion.Euler(0f, 0f, -14f));
            for (var i = 0; i < 4; i++)
                y.Pole.Box(new Vector3(tx - 1.2f + i * 0.08f, 0.3f + i * 0.3f, tz), new Vector3(0.05f, 0.05f, 0.56f));

            // ぶらんこ。A の字の脚を二組と、上の梁。座面を二つ吊る
            const float sx = 20.3f;
            const float sz = -7.6f;
            const float beam = 2.3f;
            for (var i = 0; i < 2; i++)
            {
                var x = sx + (i * 2 - 1) * 1.5f;
                YardBeam(y.Red, new Vector3(x, 0f, sz - 0.8f), new Vector3(x, beam, sz), 0.10f);
                YardBeam(y.Red, new Vector3(x, 0f, sz + 0.8f), new Vector3(x, beam, sz), 0.10f);
            }
            y.Red.Box(new Vector3(sx, beam, sz), new Vector3(3.1f, 0.12f, 0.12f));
            for (var i = 0; i < 2; i++)
            {
                var x = sx + (i * 2 - 1) * 0.6f;
                y.Iron.Box(new Vector3(x - 0.2f, beam - 0.85f, sz), new Vector3(0.02f, 1.7f, 0.02f));
                y.Iron.Box(new Vector3(x + 0.2f, beam - 0.85f, sz), new Vector3(0.02f, 1.7f, 0.02f));
                y.Iron.Box(new Vector3(x, 0.50f, sz), new Vector3(0.50f, 0.05f, 0.22f));
            }
            // 揺れる乗り物。ばねの上に黄色い胴
            foreach (var at in new[] { new Vector3(18.4f, 0f, -4.3f), new Vector3(14.0f, 0f, -8.4f) })
            {
                y.Iron.Box(at + new Vector3(0f, 0.22f, 0f), new Vector3(0.14f, 0.44f, 0.14f));
                y.Yellow.Box(at + new Vector3(0f, 0.58f, 0f), new Vector3(0.28f, 0.34f, 0.70f));
                y.Yellow.Box(at + new Vector3(0f, 0.86f, 0.28f), new Vector3(0.24f, 0.24f, 0.18f));
            }

            // ベンチ。柵の外、門の脇で遊び場を向く
            YardBench(y, new Vector3(x0 - 1.4f, 0f, -4.3f), 90f);
            YardBench(y, new Vector3(x0 - 1.4f, 0f, -8.3f), 90f);
        }

        /// <summary>
        /// 黒い鉄の柵を一辺。支柱を 2 m ごと、縦の桟を 0.25 m ごと、上と下に横の桟。
        /// 桟は上下の蓋の無い四面だけの細い柱にして頂点を抑える
        /// </summary>
        static void YardRailing(YardBanks y, Vector3 a, Vector3 b, float high)
        {
            var d = b - a;
            var len = d.magnitude;
            if (len < 0.1f) return;
            var rot = Quaternion.LookRotation(d / len, Vector3.up);
            y.Iron.Box((a + b) * 0.5f + Vector3.up * (high - 0.03f), new Vector3(0.05f, 0.05f, len), rot);
            y.Iron.Box((a + b) * 0.5f + Vector3.up * 0.12f, new Vector3(0.04f, 0.04f, len), rot);
            var posts = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
            for (var i = 0; i <= posts; i++)
                y.Iron.Box(Vector3.Lerp(a, b, i / (float)posts) + Vector3.up * (high * 0.5f + 0.04f), new Vector3(0.07f, high + 0.08f, 0.07f));
            var bars = Mathf.Max(1, Mathf.RoundToInt(len / 0.25f));
            const float t = 0.012f;
            for (var i = 1; i < bars; i++)
            {
                var at = Vector3.Lerp(a, b, i / (float)bars);
                y.Iron.FaceX(at.x + t, at.z - t, at.z + t, 0.12f, high - 0.03f, 1);
                y.Iron.FaceX(at.x - t, at.z - t, at.z + t, 0.12f, high - 0.03f, -1);
                y.Iron.FaceZ(at.z + t, at.x - t, at.x + t, 0.12f, high - 0.03f, 1);
                y.Iron.FaceZ(at.z - t, at.x - t, at.x + t, 0.12f, high - 0.03f, -1);
            }
        }

        /// <summary>ベンチ。木の座面と背もたれに黒い鉄の脚。yaw は座った人の向き（+z が 0）</summary>
        static void YardBench(YardBanks y, Vector3 foot, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (a, b, c) => foot + rot * new Vector3(a, b, c);
            for (var i = 0; i < 3; i++)
                y.Timber.Box(p(0f, 0.44f, -0.12f + i * 0.13f), new Vector3(1.8f, 0.04f, 0.11f), rot);
            for (var i = 0; i < 2; i++)
                y.Timber.Box(p(0f, 0.62f + i * 0.16f, -0.27f), new Vector3(1.8f, 0.11f, 0.04f), rot);
            for (var i = 0; i < 2; i++)
            {
                var x = (i * 2 - 1) * 0.72f;
                y.Iron.Box(p(x, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.46f), rot);
                y.Iron.Box(p(x, 0.66f, -0.29f), new Vector3(0.06f, 0.44f, 0.05f), rot);
            }
        }

        /// <summary>二点を結ぶ角材。脚や枝のように斜めに渡す物に使う</summary>
        static void YardBeam(Bank b, Vector3 from, Vector3 to, float thick)
        {
            var d = to - from;
            if (d.sqrMagnitude < 1e-6f) return;
            var up = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            b.Box((from + to) * 0.5f, new Vector3(thick, thick, d.magnitude), Quaternion.LookRotation(d, up));
        }

        /// <summary>
        /// 植え込みと木。
        ///
        /// **木はプラタナス。** ロンドンの通りと公営住宅の芝にいちばん多い大きな落葉樹で、
        /// 三月の頭はまだ葉が無い。白っぽい斑の幹が 3 m ほどで枝分かれし、枝の先の細い枝の塊が
        /// 遠目には茶色がかった灰色の靄に見える。道路沿いに三本、駐車場の角に一本。
        ///
        /// **樹冠は廊下の目の高さを跨ぐ。** 廊下から見ると、樹冠の上半分が隣の棟の窓の列に重なり、
        /// 歩くと窓の列の前を横切る。低い木だと敷地の地面に沈んで、距離の手掛かりにならない
        /// </summary>
        static void YardGreen(YardBanks y)
        {
            // 常緑の低木。塀の内側に、区切りを入れて
            for (var i = 0; i < 4; i++)
                y.Leaf.Box(new Vector3(6.0f + i * 1.9f, 0.45f, YardEdge - 0.75f), new Vector3(1.5f, 0.9f, 0.9f));
            y.Leaf.Box(new Vector3(YardWest + 0.9f, 0.55f, YardEdge - 0.9f), new Vector3(1.1f, 1.1f, 1.1f));
            y.Leaf.Box(new Vector3(YardEast - 0.9f, 0.6f, YardEdge - 0.9f), new Vector3(1.2f, 1.2f, 1.2f));

            // 道路沿いのプラタナス。歩道の植え桝に
            YardPlane(y, new Vector3(-6.0f, 0f, 1.25f), 11.0f, 3);
            YardPlane(y, new Vector3(8.6f, 0f, 1.25f), 12.0f, 5);
            YardPlane(y, new Vector3(23.0f, 0f, 1.25f), 10.5f, 7);
            // 駐車場の北東の角。芝は廊下の正面に木を立てず、開けたままにする
            YardPlane(y, new Vector3(-3.1f, 0f, -3.4f), 9.0f, 13);
            // 植え桝の縁
            foreach (var x in new[] { -6.0f, 8.6f, 23.0f })
            {
                y.Kerb.Box(new Vector3(x, 0.03f, 0.62f), new Vector3(1.3f, 0.06f, 0.08f));
                y.Kerb.Box(new Vector3(x, 0.03f, 1.88f), new Vector3(1.3f, 0.06f, 0.08f));
            }
        }

        /// <summary>
        /// プラタナスを一本。白っぽい幹を立て、太い枝を三本だけ箱で出し、
        /// その上の樹冠は枝を描いた絵（<see cref="YardCrownPicture"/>）を貼った札を三枚、60 度ずつ回して交差させる。
        ///
        /// **樹冠を箱で埋めない。** 葉の無い木は、枝の隙間から向こうが透けて見えるのが形の要。
        /// 箱の塊を重ねると、どう並べても立方体が宙に浮いた絵になる。枝の形は絵の α に持たせ、
        /// 抜いた所から隣の棟の窓の列が覗くようにする
        /// </summary>
        static void YardPlane(YardBanks y, Vector3 foot, float high, int seed)
        {
            var rnd = new System.Random(seed);
            var fork = high * 0.30f;
            y.Bark.Box(foot + new Vector3(0f, fork * 0.5f, 0f), new Vector3(0.38f, fork, 0.38f));
            // 太い枝。札の根元と幹を繋ぐ
            for (var k = 0; k < 3; k++)
            {
                var a = (k + (float)rnd.NextDouble() * 0.5f) / 3f * Mathf.PI * 2f;
                var tip = foot + new Vector3(Mathf.Cos(a) * high * 0.10f, fork + high * 0.16f, Mathf.Sin(a) * high * 0.10f);
                YardBeam(y.Bark, foot + Vector3.up * (fork - 0.2f), tip, 0.2f);
            }
            // 樹冠の札。幅は丈の 0.8 倍、根元は枝分かれの少し下
            var wide = high * 0.80f;
            var bottom = fork - 0.3f;
            var turn = (float)rnd.NextDouble() * 60f;
            // 絵は左右を反転して使い分け、三枚が同じ枝ぶりに見えないようにする
            for (var k = 0; k < 3; k++)
            {
                var dir = Quaternion.Euler(0f, turn + k * 60f, 0f) * Vector3.right * (wide * 0.5f);
                var flip = (k + seed) % 2 == 0;
                var u0 = flip ? 1f : 0f;
                var u1 = flip ? 0f : 1f;
                var b0 = foot - dir + Vector3.up * bottom;
                var b1 = foot + dir + Vector3.up * bottom;
                y.Crown.Patch(b0, b1, b1 + Vector3.up * (high - bottom), b0 + Vector3.up * (high - bottom),
                    new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f));
            }
        }

        /// <summary>樹冠の絵の置き場と、描き方の版。版を変えると次の組み直しで描き直す</summary>
        const string YardCrownPath = DiveTextures + "EstatePlane.png";
        const string YardCrownSign = "plane4|256";
        const int YardCrownSize = 256;

        /// <summary>
        /// 樹冠の札のマテリアル。遠景の板と同じ <c>HalfAware/Backdrop</c>（灯りも霞も受けず、α の境で抜き、裏表を描く）。
        /// 木は中心から 60 m より手前にしか無く、霞は 2% に届かないので、霞を受けなくても浮かない
        /// </summary>
        static Material YardCrownMat()
        {
            const string path = Materials + "EstateYardCrown.mat";
            var shader = Shader.Find("HalfAware/Backdrop");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = "EstateYardCrown";
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", YardCrownPicture());
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cutoff", 0.5f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 葉の落ちたプラタナスの樹冠の絵を描く。下の真ん中から太い枝が四本伸び、二つ三つに分かれながら細くなり、
        /// 先に実の房（小さな丸）が下がる。α が枝の形で、色は枝の灰色がかった茶色。
        ///
        /// 描いた絵は取り込みの userData に版を持ち、版が同じなら描き直さない（空の絵と同じ作り）
        /// </summary>
        static Texture2D YardCrownPicture()
        {
            var importer = AssetImporter.GetAtPath(YardCrownPath) as TextureImporter;
            if (importer == null || importer.userData != YardCrownSign || !System.IO.File.Exists(YardCrownPath))
            {
                const int n = YardCrownSize;
                var alpha = new float[n * n];
                var rnd = new System.Random(41);
                System.Action<Vector2, float, float, float, int> grow = null;
                grow = (from, angle, length, thick, depth) =>
                {
                    var dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    var to = from + dir * length;
                    // 樹冠の輪郭（丸みのある楕円）の外へ出る枝は、輪郭の所で止めてそれ以上分けない。
                    // 絵の縁で切ると、樹冠の上と横が定規で引いたように平らになる
                    var stop = false;
                    if (YardCrownOut(to))
                    {
                        float lo = 0f, hi = 1f;
                        for (var i = 0; i < 12; i++)
                        {
                            var mid = (lo + hi) * 0.5f;
                            if (YardCrownOut(from + dir * length * mid)) hi = mid; else lo = mid;
                        }
                        to = from + dir * length * lo;
                        stop = true;
                    }
                    YardCrownLine(alpha, n, from, to, thick);
                    if (depth == 0 || stop)
                    {
                        if (rnd.NextDouble() < 0.25) YardCrownDot(alpha, n, to + new Vector2(0f, -0.012f), 0.010f);
                        return;
                    }
                    var kids = depth > 3 ? 2 : 2 + rnd.Next(0, 2);
                    for (var k = 0; k < kids; k++)
                    {
                        var turn = (k - (kids - 1) * 0.5f) * 0.55f + ((float)rnd.NextDouble() - 0.5f) * 0.35f;
                        // 上へ向かうほど立ち、横へ出た枝は少し垂れる
                        var lean = angle * 0.85f + turn;
                        grow(to, lean, length * (0.70f + (float)rnd.NextDouble() * 0.12f), Mathf.Max(thick * 0.66f, 0.0045f), depth - 1);
                    }
                };
                var root = new Vector2(0.5f, 0f);
                var limbs = new[] { -0.95f, -0.35f, 0.3f, 0.9f };
                foreach (var a in limbs)
                    grow(root, a + ((float)rnd.NextDouble() - 0.5f) * 0.2f, 0.26f + (float)rnd.NextDouble() * 0.05f, 0.030f, 6);

                var pic = new Texture2D(n, n, TextureFormat.RGBA32, false, false);
                var px = new Color32[n * n];
                for (var i = 0; i < px.Length; i++)
                {
                    // 枝の色。下の太い所ほど少し明るい灰色、上の細い所ほど茶色がかる
                    var yy = (i / n) / (float)n;
                    var r = Mathf.Lerp(0.42f, 0.36f, yy);
                    var g = Mathf.Lerp(0.40f, 0.31f, yy);
                    var b = Mathf.Lerp(0.36f, 0.26f, yy);
                    px[i] = new Color32(Byte(r), Byte(g), Byte(b), Byte(Mathf.Clamp01(alpha[i])));
                }
                pic.SetPixels32(px);
                pic.Apply();
                if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive"))
                    AssetDatabase.CreateFolder("Assets/Textures", "Dive");
                System.IO.File.WriteAllBytes(YardCrownPath, pic.EncodeToPNG());
                Object.DestroyImmediate(pic);
                AssetDatabase.ImportAsset(YardCrownPath);
                importer = AssetImporter.GetAtPath(YardCrownPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = true;
                    // 遠くで mipmap の段が下がっても、抜く境（0.5）で残る枝の面積を保つ。保たないと枝が消える
                    importer.mipMapsPreserveCoverage = true;
                    importer.alphaTestReferenceValue = 0.5f;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.maxTextureSize = n;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.userData = YardCrownSign;
                    importer.SaveAndReimport();
                }
                Debug.Log("樹冠の絵を描いた: " + YardCrownPath);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(YardCrownPath);
        }

        /// <summary>樹冠の輪郭の外か。根元の近く（下の三割）は幹から出たばかりの枝なので見ない</summary>
        static bool YardCrownOut(Vector2 p)
        {
            if (p.y < 0.3f) return p.x < 0.02f || p.x > 0.98f || p.y < 0f;
            var e = new Vector2((p.x - 0.5f) / 0.48f, (p.y - 0.5f) / 0.48f);
            return e.sqrMagnitude > 1f;
        }

        /// <summary>太さのある線を α に足す。縁は画素に掛かった分だけ</summary>
        static void YardCrownLine(float[] alpha, int n, Vector2 a, Vector2 b, float thick)
        {
            var half = Mathf.Max(thick * n * 0.5f, 0.6f);
            var pa = a * n;
            var pb = b * n;
            var x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, pb.x) - half - 1));
            var x1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(pa.x, pb.x) + half + 1));
            var y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, pb.y) - half - 1));
            var y1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(pa.y, pb.y) + half + 1));
            var ab = pb - pa;
            var len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var t = Mathf.Clamp01(Vector2.Dot(p - pa, ab) / len2);
                    var d = (p - (pa + ab * t)).magnitude;
                    var cover = Mathf.Clamp01(half + 0.5f - d);
                    var i = y * n + x;
                    if (cover > alpha[i]) alpha[i] = cover;
                }
        }

        /// <summary>小さな丸（プラタナスの実の房）を α に足す</summary>
        static void YardCrownDot(float[] alpha, int n, Vector2 at, float radius)
        {
            YardCrownLine(alpha, n, at, at + new Vector2(0f, 0.001f), radius * 2f);
        }

        /// <summary>
        /// 道路。縁石、縁石沿いの黄色い二重線、真ん中の白い破線、バス停の前の黄色い囲み。
        ///
        /// **等間隔の繰り返しを重ねる。** 真ん中の破線、歩道の目地、街灯の柱。
        /// 違う間隔で流れると、廊下を歩いたときの視差がいちばん強く出る
        /// </summary>
        static void YardRoad(YardBanks y)
        {
            const float west = -14f;
            const float east = 25f;
            y.Tarmac.FaceY(0.012f, west, east, RoadNear, RoadFar, 1);
            // 縁石。歩道より 12 cm 高い帯
            y.Kerb.Box(new Vector3((west + east) * 0.5f, 0.06f, RoadNear - 0.1f), new Vector3(east - west, 0.12f, 0.2f));
            // 向こうの縁石は横丁の口で切る
            y.Kerb.Box(new Vector3((west + SidePaveWest) * 0.5f, 0.06f, RoadFar + 0.1f), new Vector3(SidePaveWest - west, 0.12f, 0.2f));
            y.Kerb.Box(new Vector3((SidePaveEast + east) * 0.5f, 0.06f, RoadFar + 0.1f), new Vector3(east - SidePaveEast, 0.12f, 0.2f));

            // 真ん中の白い破線。4 m 引いて 5 m 空ける
            for (var x = -13.5f; x < east; x += 9f)
                y.Line.FaceY(0.02f, x, Mathf.Min(x + 4f, east), RoadMid - 0.05f, RoadMid + 0.05f, 1);

            // バス停の前の囲み。この中は二重線を引かない
            const float stop0 = 8.4f;
            const float stop1 = 21.6f;
            const float stopFar = RoadMid - 0.3f;
            y.Yellow.FaceY(0.02f, stop0, stop1, stopFar - 0.05f, stopFar + 0.05f, 1);
            for (var x = stop0; x < stop1; x += 1.0f)
                y.Yellow.FaceY(0.02f, x, x + 0.6f, RoadNear + 0.05f, RoadNear + 0.15f, 1);
            y.Yellow.FaceY(0.02f, stop0 - 0.05f, stop0 + 0.05f, RoadNear, stopFar, 1);
            y.Yellow.FaceY(0.02f, stop1 - 0.05f, stop1 + 0.05f, RoadNear, stopFar, 1);
            // 「BUS STOP」の字。読めなくてよいので、字の幅の四角を並べる
            for (var i = 0; i < 7; i++)
            {
                if (i == 3) continue;
                var x = 12.2f + i * 0.85f;
                y.Yellow.FaceY(0.02f, x, x + 0.1f, RoadNear + 0.6f, RoadNear + 1.6f, 1);
                y.Yellow.FaceY(0.02f, x + 0.5f, x + 0.6f, RoadNear + 0.6f, RoadNear + 1.6f, 1);
                y.Yellow.FaceY(0.02f, x + 0.1f, x + 0.5f, RoadNear + 1.5f, RoadNear + 1.6f, 1);
                y.Yellow.FaceY(0.02f, x + 0.1f, x + 0.5f, RoadNear + 0.6f, RoadNear + 0.7f, 1);
            }

            // 縁石沿いの黄色い二重線。手前はバス停の囲みを避ける
            YardDoubleYellow(y, west, stop0, RoadNear + 0.25f);
            YardDoubleYellow(y, stop1, east, RoadNear + 0.25f);
            YardDoubleYellow(y, west, SidePaveWest, RoadFar - 0.25f - 0.28f);
            YardDoubleYellow(y, SidePaveEast, east, RoadFar - 0.25f - 0.28f);
        }

        /// <summary>黄色い二重線を一組。z0 は手前の線の手前の縁</summary>
        static void YardDoubleYellow(YardBanks y, float x0, float x1, float z0)
        {
            y.Yellow.FaceY(0.02f, x0, x1, z0, z0 + 0.08f, 1);
            y.Yellow.FaceY(0.02f, x0, x1, z0 + 0.20f, z0 + 0.28f, 1);
        }

        /// <summary>
        /// 歩道の物。背の高い今どきの街灯、赤い郵便ポスト、バス停の屋根と標柱、標識。
        ///
        /// **街灯は 8 m。** 廊下の目の高さより少し上に灯具が来るので、廊下から見ると空か隣の棟の窓を背にする。
        /// 14 m ごとに三本。等間隔の柱の列が、道路の向きと距離を読ませる
        /// </summary>
        static void YardStreet(YardBanks y)
        {
            const float lampZ = RoadNear - 0.45f;
            foreach (var x in new[] { -8.2f, 5.8f, 19.8f })
                YardLamp(y, new Vector3(x, 0f, lampZ), 8.0f);
            // 向こうの歩道にも、間をずらして二本
            foreach (var x in new[] { -1.2f, 12.8f })
                YardLamp(y, new Vector3(x, 0f, RoadFar + 0.45f), -8.0f);

            // 赤い郵便ポスト。門の東、縁石の近く
            YardPillarBox(y, new Vector3(4.4f, 0f, 1.35f));

            // バス停。背を敷地の塀へ向け、道路の側を開けた屋根
            const float bx0 = 12.6f;
            const float bx1 = 16.2f;
            const float back = 0.05f;
            const float front = 1.45f;
            const float roof = 2.45f;
            foreach (var x in new[] { bx0, bx1 })
                foreach (var z in new[] { back, front })
                    y.Pole.Box(new Vector3(x, roof * 0.5f, z), new Vector3(0.08f, roof, 0.08f));
            y.Pole.Box(new Vector3((bx0 + bx1) * 0.5f, roof + 0.05f, (back + front) * 0.5f), new Vector3(bx1 - bx0 + 0.3f, 0.10f, front - back + 0.35f));
            y.Glass.Box(new Vector3((bx0 + bx1) * 0.5f, 1.25f, back), new Vector3(bx1 - bx0, 1.9f, 0.03f));
            y.Glass.Box(new Vector3(bx0, 1.25f, (back + front) * 0.5f - 0.15f), new Vector3(0.03f, 1.9f, front - back - 0.3f));
            // 東の端の広告の板。明るい面が一枚あると、遠目にもバス停だと分かる
            y.Line.Box(new Vector3(bx1, 1.30f, (back + front) * 0.5f - 0.1f), new Vector3(0.12f, 1.8f, 1.2f));
            y.Iron.Box(new Vector3((bx0 + bx1) * 0.5f - 0.3f, 0.48f, back + 0.25f), new Vector3(1.6f, 0.05f, 0.30f));
            // 標柱。縁石の際に立て、上に白い札と赤い帯
            const float flagX = bx0 - 1.0f;
            const float flagZ = RoadNear - 0.3f;
            y.Pole.Box(new Vector3(flagX, 1.55f, flagZ), new Vector3(0.08f, 3.1f, 0.08f));
            y.Line.Box(new Vector3(flagX, 2.85f, flagZ + 0.05f), new Vector3(0.05f, 0.46f, 0.46f));
            y.Red.Box(new Vector3(flagX, 2.85f, flagZ + 0.05f), new Vector3(0.06f, 0.08f, 0.50f));
            y.Line.Box(new Vector3(flagX, 1.70f, flagZ + 0.05f), new Vector3(0.06f, 0.55f, 0.30f));

            // 標識。東の端に、白地に赤い輪の丸（速さの制限）
            YardRoundSign(y, new Vector3(YardEast + 0.4f, 0f, RoadNear - 0.3f), 2.4f);
            // 向こうの歩道の西にもう一つ
            YardRoundSign(y, new Vector3(-10.4f, 0f, RoadFar + 0.3f), 2.4f);

            // 通りの名前の札。白地に黒い縁で、低い二本の脚。駐車場の入口の東
            const float plateX = -8.0f;
            const float plateZ = YardEdge + 0.35f;
            for (var i = 0; i < 2; i++)
                y.Iron.Box(new Vector3(plateX - 0.55f + i * 1.1f, 0.35f, plateZ), new Vector3(0.06f, 0.7f, 0.06f));
            y.Iron.Box(new Vector3(plateX, 0.62f, plateZ), new Vector3(1.26f, 0.30f, 0.03f));
            y.Line.Box(new Vector3(plateX, 0.62f, plateZ + 0.02f), new Vector3(1.18f, 0.22f, 0.02f));
        }

        /// <summary>
        /// 背の高い街灯。細い柱の頭から道路の側へ短い腕を出し、平たい灯具を付ける。
        /// <paramref name="high"/> が負なら腕を -z（手前）へ出す
        /// </summary>
        static void YardLamp(YardBanks y, Vector3 foot, float high)
        {
            var toward = high > 0f ? 1f : -1f;
            var h = Mathf.Abs(high);
            y.Pole.Box(foot + new Vector3(0f, 0.4f, 0f), new Vector3(0.24f, 0.8f, 0.24f));
            y.Pole.Box(foot + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.13f, h, 0.13f));
            y.Pole.Box(foot + new Vector3(0f, h - 0.05f, toward * 0.45f), new Vector3(0.07f, 0.07f, 0.9f));
            y.Iron.Box(foot + new Vector3(0f, h - 0.10f, toward * 1.15f), new Vector3(0.30f, 0.11f, 0.75f));
            y.Line.Box(foot + new Vector3(0f, h - 0.16f, toward * 1.15f), new Vector3(0.22f, 0.02f, 0.62f));
        }

        /// <summary>
        /// 赤い郵便ポスト。向きを変えた箱を三つ重ねて丸い胴にし、少し張り出した丸い頭と黒い台を付ける。
        /// 投函口は黒い細い帯
        /// </summary>
        static void YardPillarBox(YardBanks y, Vector3 foot)
        {
            y.Iron.Box(foot + new Vector3(0f, 0.05f, 0f), new Vector3(0.62f, 0.10f, 0.62f));
            for (var k = 0; k < 3; k++)
            {
                var rot = Quaternion.Euler(0f, k * 30f, 0f);
                y.Red.Box(foot + new Vector3(0f, 0.72f, 0f), new Vector3(0.52f, 1.24f, 0.52f), rot);
                y.Red.Box(foot + new Vector3(0f, 1.38f, 0f), new Vector3(0.62f, 0.08f, 0.62f), rot);
                y.Red.Box(foot + new Vector3(0f, 1.47f, 0f), new Vector3(0.46f, 0.10f, 0.46f), rot);
            }
            y.Red.Box(foot + new Vector3(0f, 1.55f, 0f), new Vector3(0.26f, 0.08f, 0.26f));
            y.Iron.Box(foot + new Vector3(0f, 1.12f, -0.27f), new Vector3(0.30f, 0.05f, 0.02f));
            y.Line.Box(foot + new Vector3(0f, 0.88f, -0.27f), new Vector3(0.16f, 0.12f, 0.02f));
        }

        /// <summary>丸い標識。柱の上に、赤い輪（向きを変えた箱を重ねた八角）と白い丸</summary>
        static void YardRoundSign(YardBanks y, Vector3 foot, float high)
        {
            y.Pole.Box(foot + new Vector3(0f, high * 0.5f, 0f), new Vector3(0.07f, high, 0.07f));
            var at = foot + new Vector3(0f, high + 0.3f, 0.04f);
            for (var k = 0; k < 2; k++)
            {
                var rot = Quaternion.Euler(0f, 0f, k * 45f);
                y.Red.Box(at, new Vector3(0.58f, 0.58f, 0.03f), rot);
                y.Line.Box(at + new Vector3(0f, 0f, 0.02f), new Vector3(0.40f, 0.40f, 0.02f), rot);
            }
        }

        // ---- 手すりの外・中と奥の層 --------------------------------------------

        /// <summary>
        /// 棟の担当の <c>Estate()</c> から呼ばれる口。敷地（手前）と中の層（隣の棟・高層棟・長屋）を
        /// 一つの入れ物に溜めて出し、遠い地面と奥の書き割りの輪を置く。
        ///
        /// **敷地と中の層は同じ入れ物を分け合う。** 同じ色の物（戸の赤、縁石、街灯の柱）を一枚の mesh に
        /// まとめて、描く回数を増やさない。呼び口の名前と引数は棟の担当と取り決めてあるので残す
        /// </summary>
        static void EstateBlockWall(Transform place, EstateBanks b)
        {
            var y = new YardBanks();
            EstateGrounds(y);
            EstateMid(y);
            y.Emit(place);

            // 遠い地面。敷地の地面は隣の棟の足元（BlockFace）で切れているので、その先を一枚で塞ぐ。
            // 塞がないと棟の脇に空の色がそのまま抜けて、棟が虚空に立って見える。
            // 敷地の地面より少し下へ置くので、手前では地面に隠れる。
            //
            // **縁は書き割りの輪と同じ中心・同じ向きの正多角形にする**（BuildDiveEstateFar.cs）。
            // そうすると撮った絵の中の地面の始まりが、どの板でも同じ高さの一本の線になり、
            // 中心から見て本物の地面の縁とちょうど繋がる
            var land = new Bank { Texel = 0.35f };
            land.FanY(new Vector3(EstateFarCentre.x, -0.05f, EstateFarCentre.z), EstateFarRim());
            // 書き割りの地面も同じマテリアルで撮る
            NoShadow(EstateEmit(place, "EstateLand", land, EstateLandMat(), false));

            // 60 m より先は組まない。撮った絵を板の輪に貼る
            EstateBackdrop(place);
        }

        /// <summary>
        /// 手前（敷地）と中の層の頂点の数を、組み直さずに数える。測る道具（<c>CheckDiveEstateView</c>）が使う
        /// </summary>
        internal static int[] EstateOutsideCounts()
        {
            var near = new YardBanks();
            EstateGrounds(near);
            var mid = new YardBanks();
            EstateMid(mid);
            return new[] { YardVerts(near), YardVerts(mid) };
        }

        static int YardVerts(YardBanks y)
        {
            var field = typeof(Bank).GetField("verts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var n = 0;
            foreach (var f in typeof(YardBanks).GetFields())
            {
                var bank = f.GetValue(y) as Bank;
                if (bank != null) n += ((System.Collections.IList)field.GetValue(bank)).Count;
            }
            return n;
        }
    }
}
