using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公営住宅（council estate）。ロンドンの 1960〜70 年代の、デッキアクセスのメゾネット棟。
    ///
    /// **一目でどこか分かることを条件に組む。** ここで公営住宅だと言わせているのは三つ。
    /// **折り返す外階段**、**デッキに並ぶ色の違う玄関と、その脇の台所の窓**、
    /// そして敷地の担当が組む**隣の棟と塔状の高層棟**。
    ///
    /// **棟は四階建てで、二層ずつのメゾネットが上下に重なる。** 一・二階は地面から入る下の住戸、
    /// 三・四階は三階のデッキから入る上の住戸。プレイヤーが外階段で上がるのは三階のデッキまで。
    /// 四階（寝室の階）はデッキの上へ張り出して、デッキに屋根を掛ける。この型の典型で、
    /// 手すりの外に四階の窓の帯が浮いて見えると、それだけで「空中の通り」になる。
    ///
    /// **日本の団地の物は置かない。** 三和土・下駄箱・押し入れ・室外機・階数の札・物干し竿は、
    /// 倫敦の網の先に見えない（設計書 2 節「取り下げたこと」）。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）と敷地（<c>BuildDiveEstateYard.cs</c>）が
    /// 同じ数を見るので、両方から見る点は const にしてここに置く。
    /// 住戸の中は <c>BuildDiveEstateFlat.cs</c>（躯体）と <c>BuildDiveEstateRooms.cs</c>（暮らし）。
    ///
    /// 道具の名前は <c>Estate</c> で始める。五つの場所が同じ partial class を分け合っていて、
    /// <c>Bar</c> のような短い名前は他の場所と衝突する
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 外階段 ------------------------------------------------------------
        //
        // 外階段は半階ごとに折り返す。半階上がって踊り場で 180 度向きを変え、また半階上がる。
        // 三階まで四本。**折り返しの向きはデッキが決めている。** 階の高さに来る踊り場は
        // デッキと同じ z にあるので、一本おきに棟の面の側へ戻ってくる。
        // 東の一本が上り始め、西の一本が上り終わり。二本のあいだには中壁が立つ。
        // 階段の塔は棟の西の端にあり、その南（棟の中）は、ごみ置き場とダストシュートの入る塊

        /// <summary>階の高さ。m</summary>
        public const float Floor = 2.8f;
        /// <summary>半階ぶんの階段が z へ伸びる長さ。1.4 m 上がって 2.1 m 進むので勾配は 34 度</summary>
        const float Flight = 2.1f;
        /// <summary>半階ぶんの段数。蹴上げ 17.5 cm・踏み面 26 cm</summary>
        const int StepCount = 8;
        /// <summary>
        /// デッキの幅。階の高さの踊り場の奥行きでもある。
        /// ロンドンのデッキは、人がすれ違い、戸の前に鉢を置いても通れる幅を取っていた。
        /// 1.4 m では日本の開放廊下の幅で、空中の通りに見えない
        /// </summary>
        const float LandingDeep = 2.0f;
        /// <summary>折り返しの踊り場の奥行き。階段一本の幅より広くないと向きが変えられない</summary>
        const float StairTurn = 1.3f;
        /// <summary>階段一本の幅</summary>
        const float StairWide = 1.1f;
        /// <summary>二本のあいだの中壁。厚みの半分。ここが抜けていると隣の一本へ落ちる</summary>
        const float StairSpine = 0.1f;
        /// <summary>中壁の頭。三階まで上がり切った先で、床から 1 m</summary>
        const float StairSpineTop = EstateTop + 1f;                                        // 6.6
        /// <summary>階段の井戸の西の端。棟の西の妻壁の内側と揃える</summary>
        const float StairWest = -1.2f;
        /// <summary>階段の井戸の東の端。ここから東に住戸が並ぶ</summary>
        const float StairEast = StairWest + StairWide * 2f + StairSpine * 2f;              // 1.2
        /// <summary>西の一本の真ん中。上りの終わりはいつもこちら</summary>
        const float StairWestMid = StairWest + StairWide * 0.5f;                           // -0.65
        /// <summary>東の一本の真ん中。上りの始まりはいつもこちら</summary>
        const float StairEastMid = StairEast - StairWide * 0.5f;                           // 0.65

        /// <summary>三階のデッキの幅の真ん中</summary>
        public const float EstateWalk = -13.8f;
        /// <summary>棟の北の面（デッキ側）。玄関はここに開く</summary>
        public const float EstateFace = EstateWalk - LandingDeep * 0.5f;                   // -14.8
        /// <summary>
        /// 外階段で上がる層の数。地面が一階なので、二層上がって三階のデッキに着く。
        /// 四階は住戸の中の階段でしか上がれない
        /// </summary>
        const int EstateFloors = 2;
        /// <summary>
        /// 三階（デッキ）の床の高さ。
        /// **公開の const で、団地の他のファイルがここを見ている。** 値だけ変える
        /// </summary>
        public const float EstateTop = Floor * EstateFloors;                               // 5.6
        /// <summary>デッキの手すり側の縁。階段の井戸が北へ出る線でもある</summary>
        const float WalkFront = EstateFace + LandingDeep;                                  // -12.8
        /// <summary>折り返しの踊り場の南の縁</summary>
        const float StairTurnFar = WalkFront + Flight;                                     // -10.7
        /// <summary>折り返しの踊り場の真ん中</summary>
        public const float EstateTurn = StairTurnFar + StairTurn * 0.5f;                   // -10.05
        /// <summary>階段の井戸の北の端</summary>
        const float StairEnd = StairTurnFar + StairTurn;                                   // -9.4
        /// <summary>
        /// 階の高さの踊り場。折り返しなのでデッキと同じ z に来る。
        /// 記憶 0 の鍵打ちが見ている
        /// </summary>
        public const float EstateLanding1 = EstateWalk;
        /// <summary>二つ目。同じ井戸の同じ側へ戻るので z も同じ</summary>
        public const float EstateLanding2 = EstateWalk;

        // ---- 棟の高さ ----------------------------------------------------------

        /// <summary>床板の厚み。下の階の天井はここだけ低い</summary>
        const float SlabThick = 0.3f;
        /// <summary>四階（寝室の階）の床の高さ</summary>
        const float EstateUpper = EstateTop + Floor;                                       // 8.4
        /// <summary>デッキの天井。四階の床板の下面で、三階の住戸の天井も同じ高さ</summary>
        const float WalkRoof = EstateUpper - SlabThick;                                    // 8.1
        /// <summary>陸屋根の面</summary>
        const float EstateRoof = EstateUpper + Floor;                                      // 11.2
        /// <summary>四階の天井。陸屋根の床板の下面</summary>
        const float UpperRoof = EstateRoof - SlabThick;                                    // 10.9
        /// <summary>陸屋根の立ち上がりの真ん中。高さ 0.5 m の帯を屋根の縁へ回す</summary>
        const float EstateParapet = EstateRoof + 0.25f;                                    // 11.45

        // ---- 棟の平面 ----------------------------------------------------------

        /// <summary>住戸の間口。戸境の壁の真ん中から真ん中まで</summary>
        const float FlatWide = 5.4f;
        /// <summary>住戸の奥行き。デッキ側の面から南の面の外まで</summary>
        const float FlatDeep = 8.6f;
        /// <summary>A の西の縁。階段の井戸のすぐ東から住戸を並べる</summary>
        const float FlatWest = StairEast;                                                  // 1.2
        /// <summary>棟の南の面の外</summary>
        const float BlockBack = EstateFace - FlatDeep;                                     // -23.4
        /// <summary>棟の西の妻壁の外。階段の塔の西の壁もここ</summary>
        const float BlockWest = StairWest - 0.24f;                                         // -1.44
        /// <summary>デッキの東の端。三戸ぶん</summary>
        const float WalkRight = FlatWest + FlatWide * 3f;                                  // 17.4
        /// <summary>デッキの西の端。三階の踊り場もデッキの一部</summary>
        const float WalkLeft = StairWest;                                                  // -1.2
        /// <summary>棟の東の妻壁の外</summary>
        const float WallRight = WalkRight + 0.2f;                                          // 17.6
        /// <summary>外壁（デッキ側と南）の厚み</summary>
        const float FaceSkin = 0.25f;
        /// <summary>戸境の壁の厚みの半分</summary>
        const float PartyHalf = 0.1f;
        /// <summary>
        /// 住戸の西の縁から玄関の戸の真ん中まで。戸は住戸の東寄り、台所の窓は西寄り。
        /// 開いた戸は東へ振り出すので（BuildDiveTakes の Ajar）、窓の前に掛からない側に戸を寄せる
        /// </summary>
        const float DoorAt = 4.55f;

        /// <summary>A の戸口の真ん中。記憶 0 の母と、記憶 1 のハンナの家</summary>
        public const float DoorA = FlatWest + DoorAt;                                      // 5.75
        /// <summary>B の戸口。記憶 8 のジョルジョと、記憶 15 のエレナの家</summary>
        public const float DoorB = DoorA + FlatWide;                                       // 11.15
        /// <summary>
        /// C の戸口。中は作らないので、ここだけ戸が閉まっている。
        /// 戸口が二つでは並びに見えず、三つ目が入って初めて等間隔の列として読める
        /// </summary>
        public const float DoorC = DoorB + FlatWide;                                       // 16.55
        /// <summary>
        /// 玄関の戸口の幅の半分。壁から壁まで 1.0 m で、体の中心が動ける幅は 0.32 m 残る。
        /// 0.9 m では 0.22 しかなく、体が戸口の縁に擦れて入りにくかった
        /// </summary>
        const float DoorHalf = 0.5f;
        const float DoorHigh = 2.0f;

        /// <summary>下の住戸の前庭の北の縁。低い煉瓦の塀と門がここに並ぶ</summary>
        const float GardenEdge = EstateFace + 3.0f;                                        // -11.8
        /// <summary>階段の下から北へ出る小道の東の縁。ここから東が A の下の前庭</summary>
        const float PathEast = FlatWest + 1.2f;                                            // 2.4

        /// <summary>隣の棟の面。敷地の担当が見ている</summary>
        const float BlockFace = 9.5f;

        /// <summary>デッキの灯りの x。三階の踊り場の上と、各戸の台所の窓と戸口のあいだ</summary>
        static readonly float[] DeckLamps = { 0f, FlatWest + 2.7f, FlatWest + FlatWide + 2.7f, FlatWest + FlatWide * 2f + 2.7f };

        // ---- 住戸 B の内側 -------------------------------------------------------
        //
        // 名前は日本の間取りの頃のまま残す（BuildDiveTakes が見ている）。値は新しい棟に合わせる

        /// <summary>B の西の内壁の面</summary>
        const float RoomX0 = FlatWest + FlatWide + PartyHalf;                              // 6.7
        /// <summary>B の東の内壁の面</summary>
        const float RoomX1 = FlatWest + FlatWide * 2f - PartyHalf;                         // 11.9
        /// <summary>B の南の内壁の面（居間と奥の寝室の窓の壁）</summary>
        const float RoomBack = BlockBack + FaceSkin;                                       // -23.15
        /// <summary>B の三階の天井</summary>
        const float RoomRoof = WalkRoof;                                                   // 8.1

        /// <summary>B の居間のテレビの画面の真ん中。東の戸境の壁の前で、西を向く</summary>
        public static readonly Vector3 EstateTv = new Vector3(RoomX1 - 0.13f, EstateTop + 0.86f, EstateFace - 7.85f);

        // ---- 玄関 --------------------------------------------------------------
        //
        // 日本の玄関の名前は残し、意味だけ新しい間取りへ移す。**段差は無い。**
        // デッキの床と住戸の床は同じ高さで、戸口を跨いでそのまま廊下へ入る

        /// <summary>玄関の床が下がる量。段差は付けないので 0</summary>
        const float EstateSunk = 0f;
        /// <summary>戸口の内側の縁（デッキ側の外壁の内側の面）。ここから内が廊下</summary>
        const float HallSill = EstateFace - FaceSkin;                                      // -15.05
        /// <summary>A の戸口の内側の縁。B と同じ</summary>
        const float PorchSill = HallSill;
        /// <summary>廊下の突き当たりの、居間の北の壁の真ん中</summary>
        const float HallWall = EstateFace - LoungeWall;                                    // -19.45
        /// <summary>住戸の中の仕切りの厚みの半分</summary>
        const float HallSkin = 0.05f;
        /// <summary>住戸の中の戸口の高さ。仕切りはここから天井まで垂れ壁になる</summary>
        const float HallHead = 2.0f;
        /// <summary>B の居間の戸口の両端。廊下の幅そのまま</summary>
        const float HallGap0 = FlatWest + FlatWide + LaneWest;                             // 9.95
        const float HallGap1 = FlatWest + FlatWide + LaneEast;                             // 10.85
        /// <summary>B の玄関の東の壁（戸境の壁の内側）</summary>
        const float HallEast = RoomX1;                                                     // 11.9
        /// <summary>A の廊下の西と東の面</summary>
        const float PorchX0 = FlatWest + LaneWest;                                         // 4.55
        const float PorchX1 = FlatWest + LaneEast;                                         // 5.45
        /// <summary>A の廊下の突き当たり（居間の北の壁の真ん中）</summary>
        const float PorchBack = HallWall;                                                  // -19.45

        /// <summary>
        /// 素材ごとの入れ物をひとまとめに。
        ///
        /// 団地は置く物が多く、入れ物を一つずつ引数で持ち回ると、
        /// 道具を増やすたびに呼ぶ側と受ける側で数が合わなくなる。
        /// **敷地の担当もここを使うので、フィールドは消さない。** 足すのはよい
        /// </summary>
        sealed class EstateBanks
        {
            public readonly Bank Slab = new Bank { Texel = 0.35f };
            public readonly Bank Wall = new Bank { Texel = 0.3f };
            public readonly Bank Rail = new Bank { Texel = 0.5f };
            public readonly Bank Gear = new Bank { Texel = 0.5f };
            public readonly Bank Leaf = new Bank { Texel = 0.4f };
            public readonly Bank Soft = new Bank { Texel = 0.5f };
            public readonly Bank Linen = new Bank { Texel = 0.5f };
            public readonly Bank Set = new Bank { Texel = 0.5f };
            public readonly Bank Board = new Bank { Texel = 0.35f };
            public readonly Bank Tile = new Bank { Texel = 0.5f };
            public readonly Bank Bright = new Bank { Texel = 0.5f };
            public readonly Bank Shade = new Bank { Texel = 0.3f };
            public readonly Bank Lit = new Bank { Texel = 0.4f };
            public readonly Bank Paper = new Bank { Texel = 0.5f };
            public readonly Bank Red = new Bank { Texel = 0.5f };
            public readonly Bank Green = new Bank { Texel = 0.5f };
            public readonly Bank Far = new Bank { Texel = 0.4f };

            // ---- ロンドンの棟で足したもの ----
            /// <summary>打ち放しのコンクリート。床板の縁・柱・腰壁・階段の塔</summary>
            public readonly Bank Cast = new Bank { Texel = 0.35f };
            /// <summary>煉瓦の壁</summary>
            public readonly Bank Facing = new Bank { Texel = 0.3f };
            /// <summary>中を作らない窓の硝子</summary>
            public readonly Bank Glaze = new Bank { Texel = 0.5f };
            /// <summary>白く塗った窓枠と戸の枠。住戸の中では白い物（冷蔵庫・浴槽・布）</summary>
            public readonly Bank Frame = new Bank { Texel = 0.5f };
            /// <summary>A と B の内壁。二つの家は壁の色から違う</summary>
            public readonly Bank WallA = new Bank { Texel = 0.4f };
            public readonly Bank WallB = new Bank { Texel = 0.4f };
            /// <summary>A と B の敷き込みの絨毯。住戸の中の階段も覆う</summary>
            public readonly Bank CarpetA = new Bank { Texel = 0.5f };
            public readonly Bank CarpetB = new Bank { Texel = 0.5f };
            /// <summary>住戸の中の天井</summary>
            public readonly Bank Ceil = new Bank { Texel = 0.4f };
            /// <summary>明るい色。A の家の子どもの物と、戸の塗り分け</summary>
            public readonly Bank Yellow = new Bank { Texel = 0.5f };
            public readonly Bank Blue = new Bank { Texel = 0.5f };
            /// <summary>くすんだ緑。B の肘掛け椅子と浴室、C の戸</summary>
            public readonly Bank Moss = new Bank { Texel = 0.5f };
            /// <summary>前庭の芝と植木の葉</summary>
            public readonly Bank Turf = new Bank { Texel = 0.5f };
        }

        // ---- 組み立て ----------------------------------------------------------

        /// <summary>
        /// 記憶 0・1・8・15 の舞台。外階段・棟の外側・デッキ・前庭と、A と B の二層ぶんの中まで
        /// </summary>
        static void Estate(Transform place)
        {
            // 素材ごとに一つずつ。溜めてから最後に一枚へ焼く
            var crete = EstatePaint("EstateCrete", new Color(0.400f, 0.394f, 0.372f), 0.06f);
            var skin = EstatePaint("EstateSkin", new Color(0.208f, 0.204f, 0.196f), 0.05f);
            var board = EstatePaint("EstateBoard", new Color(0.300f, 0.268f, 0.205f), 0.06f);
            // 台所と浴室の床と壁の陶板。白に寄せる
            var tiles = EstatePaint("EstateTile", new Color(0.620f, 0.640f, 0.620f), 0.35f);
            var sheet = EstatePaint("EstatePaper", new Color(0.520f, 0.512f, 0.492f), 0.04f);
            var rust = EstatePaint("EstateRed", new Color(0.420f, 0.090f, 0.070f), 0.20f);
            var cast = EstatePaint("EstateCast", new Color(0.470f, 0.460f, 0.430f), 0.05f);
            // 煉瓦は茶に寄った赤。60〜70 年代の公営住宅でよく使われた焼きの濃い煉瓦
            var brick = EstatePaint("EstateFacing", new Color(0.380f, 0.225f, 0.165f), 0.04f);
            var glass = EstatePaint("EstateGlaze", new Color(0.085f, 0.105f, 0.125f), 0.88f);
            var paint = EstatePaint("EstateFrame", new Color(0.700f, 0.700f, 0.660f), 0.20f);
            // A は淡い黄色の壁と青緑の絨毯、B は古い花柄の壁紙の地の色と錆色の絨毯
            var wallA = EstatePaint("EstateWallA", new Color(0.730f, 0.690f, 0.560f), 0.05f);
            var wallB = EstatePaint("EstateWallB", new Color(0.620f, 0.520f, 0.430f), 0.04f);
            var carpetA = EstatePaint("EstateCarpetA", new Color(0.300f, 0.400f, 0.420f), 0.03f);
            var carpetB = EstatePaint("EstateCarpetB", new Color(0.420f, 0.270f, 0.160f), 0.03f);
            var ceil = EstatePaint("EstateCeil", new Color(0.680f, 0.680f, 0.650f), 0.02f);
            var yellow = EstatePaint("EstateYellow", new Color(0.760f, 0.580f, 0.180f), 0.10f);
            var blue = EstatePaint("EstateBlue", new Color(0.200f, 0.350f, 0.580f), 0.12f);
            var moss = EstatePaint("EstateMoss", new Color(0.300f, 0.350f, 0.180f), 0.10f);
            var turf = EstatePaint("EstateTurf", new Color(0.220f, 0.300f, 0.130f), 0.03f);
            var pale = Glow("EstatePale", new Color(0.86f, 0.87f, 0.90f), 0.62f);
            var bulbs = Glow("EstateBulb", new Color(1f, 0.93f, 0.80f), 1.50f);
            var exits = Glow("EstateExit", new Color(0.34f, 0.92f, 0.50f), 0.85f);
            var haze = Glow("EstateFar", new Color(0.62f, 0.64f, 0.70f), 0.46f);

            var b = new EstateBanks();
            EstateStair(b);
            EstateBlock(b);
            EstateFront(b);
            EstateBack(b);
            EstateGround(b);
            EstateFlat(b, 0, b.WallA, b.CarpetA);
            EstateFlat(b, 1, b.WallB, b.CarpetB);
            EstateHomeA(b);
            EstateRoom(b);
            EstateYard(b);
            EstateBlockWall(place, b);

            EstateEmit(place, "EstateFloor", b.Slab, crete, true);
            EstateEmit(place, "EstateWall", b.Wall, skin, true);
            EstateEmit(place, "EstateRail", b.Rail, Mat("Rail"), true);
            EstateEmit(place, "EstateRoomFloor", b.Board, board, true);
            EstateEmit(place, "EstateTile", b.Tile, tiles, true);
            EstateEmit(place, "EstateCast", b.Cast, cast, true);
            EstateEmit(place, "EstateFacing", b.Facing, brick, true);
            EstateEmit(place, "EstateWallA", b.WallA, wallA, true);
            EstateEmit(place, "EstateWallB", b.WallB, wallB, true);
            EstateEmit(place, "EstateCarpetA", b.CarpetA, carpetA, true);
            EstateEmit(place, "EstateCarpetB", b.CarpetB, carpetB, true);
            EstateEmit(place, "EstateGlaze", b.Glaze, glass, false);
            EstateEmit(place, "EstateFrame", b.Frame, paint, false);
            EstateEmit(place, "EstateCeil", b.Ceil, ceil, false);
            EstateEmit(place, "EstateYellow", b.Yellow, yellow, false);
            EstateEmit(place, "EstateBlue", b.Blue, blue, false);
            EstateEmit(place, "EstateMoss", b.Moss, moss, false);
            EstateEmit(place, "EstateTurf", b.Turf, turf, false);
            EstateEmit(place, "EstateDoor", b.Leaf, Mat("Door"), false);
            EstateEmit(place, "EstateGear", b.Gear, Mat("Rail"), false);
            EstateEmit(place, "EstateSoft", b.Soft, Mat("Cloth"), false);
            EstateEmit(place, "EstateLinen", b.Linen, crete, false);
            EstateEmit(place, "EstateTimber", b.Set, Mat("Timber"), false);
            EstateEmit(place, "EstatePaper", b.Paper, sheet, false);
            EstateEmit(place, "EstateRed", b.Red, rust, false);
            // 段鼻に当たりを入れると、階段が一段ごとに凸凹になって上がれなくなる
            EstateEmit(place, "EstatePale", b.Bright, pale, false);
            EstateEmit(place, "EstateShade", b.Shade, Mat("Ceiling"), false);
            NoShadow(EstateEmit(place, "EstateLit", b.Lit, bulbs, false));
            NoShadow(EstateEmit(place, "EstateExit", b.Green, exits, false));
            NoShadow(EstateEmit(place, "EstateFar", b.Far, haze, false));

            // テレビの画面。灯りではなく光る面で済ませる。
            // A は朝の子ども番組が点いたまま、B は老夫婦が朝のニュースを流している
            var tv = Glow("GlowTv", new Color(0.72f, 0.82f, 1f), 1.35f);
            // A の画面は子ども番組の明るい色。B と同じ白い青では、部屋の中でいちばん白い板になって浮く
            Pane(place, "EstateTvA", new Vector3(FlatWest + 5.175f, EstateTop + 0.86f, EstateFace - 6.9f),
                new Vector2(0.98f, 0.55f), Vector3.left, Glow("GlowTvA", new Color(0.46f, 0.72f, 0.62f), 0.85f));
            Pane(place, "EstateTv", EstateTv, new Vector2(0.82f, 0.46f), Vector3.left, tv);

            // 地面の端から先は虚空なので、見えない仕切りで囲う。
            // **南の縁は棟の北の面のすぐ裏。** 棟の裏へは回らせない。南の面は遠くから見せるだけ
            Ring(place, "EstateFence", new Vector2(-13.6f, 24.6f), new Vector2(EstateFace - 0.2f, BlockFace - 0.5f), 3.5f);
            // デッキの東の端は妻壁が閉じているが、走り込んだときに抜けないよう仕切りを重ねる
            Fence(place, "EstateWalkEndB", new Vector3(WalkRight + 0.1f, EstateTop + 0.75f, EstateWalk),
                new Vector3(0.2f, 1.5f, LandingDeep));

            EstateLamps(place);
        }

        /// <summary>
        /// 朝の日射しの向き（Euler）。**空の絵の日もこの向きに描く**（<see cref="EstateSunward"/>）。
        /// ここだけ直すと、空の明るい所と影の向きが食い違う。組み直せば空の絵も焼き直される
        /// </summary>
        static readonly Vector3 EstateMorningAim = new Vector3(30f, -148f, 0f);

        /// <summary>
        /// 灯り。朝の日射しと、デッキの灯りと、A と B の中。
        ///
        /// 数を増やすと WebGL で持たないので、住戸の中は一つの階に一つか二つに絞る。
        /// 影を落とさない点の灯りは壁を抜けるので、四階は踊り場の一つで寝室二つと浴室まで届かせる
        /// </summary>
        static void EstateLamps(Transform place)
        {
            // 朝の低い日射し。階段の側から当てるので、三階の戸口に立つ人は逆光になる
            var sun = Lamp(place, "Morning", LightType.Directional, new Vector3(0f, 12f, 0f),
                EstateMorningAim, new Color(1f, 0.95f, 0.86f), 2.0f, 10f);
            sun.shadows = LightShadows.Soft;
            // 日の当たらない側の下限。日と反対の空（南南西の低い所）から来る青い光の代わり。
            // 環境光も空から取った青にしてあるが（EstateAmbient）、部屋の中まで同じだけ明るくなるので弱く置いている。
            // これが無いと、日陰へ入った途端に人の輪郭しか見えなくなる
            Lamp(place, "Fill", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(20f, 22f, 0f), new Color(0.66f, 0.72f, 0.86f), 0.55f, 10f);

            // デッキの灯り。三階の踊り場の上に一つと、一戸に一つ（台所の窓と戸口のあいだの天井）。
            // 朝なので点いていなくてもよいが、四階の床の下は日が入らず、戸の前が沈む。
            // 踊り場は三方を壁に囲まれていて、灯りが無いとダストシュートの口が見えない
            for (var i = 0; i < DeckLamps.Length; i++)
                Lamp(place, "Bulb" + i, LightType.Point, new Vector3(DeckLamps[i], WalkRoof - 0.25f, EstateWalk),
                    Vector3.zero, new Color(1f, 0.92f, 0.80f), 1.8f, 4.2f);

            for (var unit = 0; unit < 2; unit++)
            {
                var ox = FlatWest + FlatWide * unit;
                var tag = unit == 0 ? "A" : "B";
                foreach (var lamp in FlatLamps)
                    Lamp(place, "Room" + tag + lamp.name, LightType.Point,
                        new Vector3(ox + lamp.at.x, lamp.at.y, FlatZ(lamp.at.z)),
                        Vector3.zero, new Color(1f, 0.93f, 0.84f), lamp.power, lamp.range);
            }
            // テレビの光は面だけでは床に届かない。B の居間に青い灯りを一つ据える
            Lamp(place, "TvGlow", LightType.Point, EstateTv + new Vector3(-0.55f, -0.15f, 0f),
                Vector3.zero, new Color(0.62f, 0.76f, 1f), 1.6f, 3.4f);
        }

        // ---- 外階段の塔 --------------------------------------------------------

        /// <summary>
        /// 外階段。四本の段と折り返しの踊り場、二階の踊り場、中壁と西の壁、手すり、塔の屋根。
        ///
        /// 三階の踊り場はデッキの床板の一部なので、ここでは張らない（<see cref="EstateBlock"/>）。
        /// 二階は踊り場だけで、デッキは無い
        /// </summary>
        static void EstateStair(EstateBanks b)
        {
            // 二階の踊り場。階段の井戸の幅だけ
            b.Slab.Box(new Vector3((StairWest + StairEast) * 0.5f, Floor - SlabThick * 0.5f, (EstateFace + WalkFront) * 0.5f),
                new Vector3(StairEast - StairWest, SlabThick, LandingDeep));
            // 折り返しの踊り場。半階ぶん上がった先に、井戸の幅いっぱいで渡す
            for (var h = 0; h < EstateFloors; h++)
                b.Slab.Box(new Vector3((StairWest + StairEast) * 0.5f, Floor * h + Floor * 0.5f - 0.09f, EstateTurn),
                    new Vector3(StairEast - StairWest, 0.18f, StairTurn));

            // 四本の段。東の一本で半階上がり、折り返して西の一本でまた半階上がる。
            // 段鼻の明るい線は b.Bright へ集める
            for (var h = 0; h < EstateFloors; h++)
            {
                var y = Floor * h;
                EstateFlight(b.Slab, b.Bright, StairEastMid, WalkFront, y, Flight);
                EstateFlight(b.Slab, b.Bright, StairWestMid, StairTurnFar, y + Floor * 0.5f, -Flight);
            }

            // 塔の西の壁。棟の西の妻壁をそのまま北へ伸ばす。打ち放しのコンクリート
            b.Cast.Box(new Vector3((BlockWest + StairWest) * 0.5f, WalkRoof * 0.5f, (EstateFace + StairEnd) * 0.5f),
                new Vector3(StairWest - BlockWest, WalkRoof, StairEnd - EstateFace));
            // 二本のあいだの中壁。**折り返しの踊り場までは届かせない。** 届くと向きが変えられない。
            // 手すりで済ませないのは、隣の一本とは半階ぶんの段差があって、落ちれば 2.8 m だから
            b.Cast.Box(new Vector3(0f, StairSpineTop * 0.5f, (WalkFront + StairTurnFar) * 0.5f),
                new Vector3(StairSpine * 2f, StairSpineTop, Flight));

            // 塔の屋根。三階の踊り場の上は四階の張り出しが覆うので、その北だけ
            b.Cast.Box(new Vector3((BlockWest + StairEast) * 0.5f, WalkRoof + SlabThick * 0.5f, (WalkFront + StairEnd) * 0.5f),
                new Vector3(StairEast - BlockWest, SlabThick, StairEnd - WalkFront));
            // 屋根の縁の立ち上がり。北と東と西。縁が真っ平らだと塔の頭が切り落とされて見える
            b.Cast.Box(new Vector3((BlockWest + StairEast) * 0.5f, EstateUpper + 0.22f, StairEnd - 0.1f),
                new Vector3(StairEast - BlockWest, 0.44f, 0.2f));
            b.Cast.Box(new Vector3(StairEast - 0.1f, EstateUpper + 0.22f, (WalkFront + StairEnd) * 0.5f),
                new Vector3(0.2f, 0.44f, StairEnd - WalkFront));
            b.Cast.Box(new Vector3(BlockWest + 0.12f, EstateUpper + 0.22f, (WalkFront + StairEnd) * 0.5f),
                new Vector3(0.24f, 0.44f, StairEnd - WalkFront));

            // 井戸の西の壁の窓。一枚の板のままだと、階段の隣に何も無い崖が立っているように見える。
            // 高さは西の一本の段の面から 0.8 m 上。段を上がりながら覗ける位置に来る
            for (var h = 0; h < EstateFloors; h++)
            {
                var y = 2.3f + h * Floor;
                EstateHole(b, y, EstateTurn, h == 1);
                EstateHole(b, y + 0.6f, (WalkFront + StairTurnFar) * 0.5f, false);
            }

            // 階段の手すり。東の一本の外側と、折り返しの踊り場の東と北を回す。
            // 西の一本の外側は塔の壁、内側は中壁なので、手すりは要らない
            for (var h = 0; h < EstateFloors; h++)
            {
                var y = Floor * h;
                EstateSlope(b.Rail, StairEast, WalkFront, y, Flight);
                EstateRailZ(b.Rail, StairEast, StairTurnFar, StairEnd, y + Floor * 0.5f);
                EstateRailX(b.Rail, StairEnd, StairWest, StairEast, y + Floor * 0.5f);
            }

            // 二階の踊り場の東の縁。デッキと同じ、コンクリートの腰壁に鉄の手すり
            EstateParapetZ(b, StairEast - 0.075f, EstateFace, WalkFront, Floor);

            // ダストシュートの投入口。二階と三階の踊り場の奥の壁（階段の裏の塊）に一つずつ。
            // デッキアクセスの棟では、ごみは各階の口から地面のごみ置き場へ落とした
            for (var f = 1; f <= EstateFloors; f++) EstateChute(b, Floor * f);
        }

        /// <summary>
        /// ダストシュートの投入口。壁から出た鉄の箱と、手前へ倒して開ける蓋と取っ手
        /// </summary>
        static void EstateChute(EstateBanks b, float y)
        {
            const float x = -0.55f;
            var z = EstateFace;
            b.Gear.Box(new Vector3(x, y + 0.98f, z + 0.07f), new Vector3(0.62f, 0.56f, 0.14f));
            // 蓋。少しだけ手前へ傾けて、倒して開ける口だと読ませる
            b.Shade.Box(new Vector3(x, y + 1.02f, z + 0.15f), new Vector3(0.50f, 0.38f, 0.03f), Quaternion.Euler(-8f, 0f, 0f));
            b.Gear.Box(new Vector3(x, y + 1.24f, z + 0.20f), new Vector3(0.30f, 0.04f, 0.05f));
            // 口の上の札。字は読めなくてよい
            b.Bright.Box(new Vector3(x, y + 1.42f, z + 0.012f), new Vector3(0.34f, 0.10f, 0.02f));
        }

        // ---- 棟の外側 ----------------------------------------------------------

        /// <summary>
        /// 棟の塊。地面・デッキと天井・縁の梁・柱・腰壁と手すり・北と南の面・妻壁・陸屋根。
        ///
        /// **コンクリートの骨組みに煉瓦を張る。** 床板の縁・柱・腰壁はコンクリートの灰、
        /// そのあいだを煉瓦で埋める。南の面は階ごとの床の帯と戸境の柱型が格子になり、
        /// どの戸が同じ住戸なのかが外から数えられる
        /// </summary>
        static void EstateBlock(EstateBanks b)
        {
            // 地面。隣の棟の足元まで、棟の裏まで伸ばす。南の面を外から撮ったとき、棟が虚空に浮かない
            b.Slab.FaceY(0f, -14f, 25f, -32f, BlockFace, 1);

            // デッキ。三階の踊り場から東の端まで一枚で通す
            b.Slab.Box(new Vector3((WalkLeft + WalkRight) * 0.5f, EstateTop - SlabThick * 0.5f, (EstateFace + WalkFront) * 0.5f),
                new Vector3(WalkRight - WalkLeft, SlabThick, LandingDeep));
            // デッキの天井。四階の床板の下面
            b.Cast.FaceY(WalkRoof, WalkLeft, WalkRight, EstateFace, WalkFront, -1);
            // 縁の梁と、四階の床板の縁。張り出しの重さを受ける梁が天井から一段下がる
            b.Cast.Box(new Vector3((WalkLeft + WallRight) * 0.5f, WalkRoof - 0.15f, WalkFront - 0.15f),
                new Vector3(WallRight - WalkLeft, 0.3f, 0.3f));
            b.Cast.FaceZ(WalkFront, WalkLeft, WallRight, WalkRoof, EstateUpper, 1);

            // デッキの縁。コンクリートの腰壁に鉄の手すり。
            // **西は外階段の東の一本の上から始める。** 西の一本はここへ上がってくるので塞がない
            EstateParapetX(b, WalkFront - 0.075f, -StairSpine, WalkRight, EstateTop);

            // デッキの縁の柱。戸境の線ごとに一本、地面から梁まで。
            // 等間隔に立つ柱が、下から見上げても、デッキを歩いても、この棟の拍子になる
            for (var i = 0; i < 3; i++)
            {
                var x = FlatWest + FlatWide * i;
                b.Cast.Box(new Vector3(x, (WalkRoof - 0.3f) * 0.5f, WalkFront - 0.15f),
                    new Vector3(0.3f, WalkRoof - 0.3f, 0.3f));
            }

            // 北の面（デッキ側）。A と B の三階だけ、戸口と台所の窓を抜く
            var north = new List<Vector4>();
            for (var unit = 0; unit < 2; unit++)
            {
                var ox = FlatWest + FlatWide * unit;
                north.Add(new Vector4(ox + DoorAt - DoorHalf, ox + DoorAt + DoorHalf, EstateTop, EstateTop + DoorHigh));
                north.Add(new Vector4(ox + KitchenWin0, ox + KitchenWin1, EstateTop + KitchenSill, EstateTop + KitchenHead));
            }
            b.Facing.FaceZHoles(EstateFace, FlatWest, WalkRight, 0f, WalkRoof, 1, north);
            // 階段の裏の塊の面。踊り場の奥の壁になるので、塔と同じコンクリート
            b.Cast.FaceZ(EstateFace, StairWest, FlatWest, 0f, WalkRoof, 1);

            // 四階の北の面。デッキの縁の真上に立つ。A と B の表の寝室の窓を抜く
            var upper = new List<Vector4>();
            for (var unit = 0; unit < 2; unit++)
            {
                var ox = FlatWest + FlatWide * unit;
                upper.Add(new Vector4(ox + FrontWin0, ox + FrontWin1, EstateUpper + BedSill, EstateUpper + BedHead));
            }
            b.Facing.FaceZHoles(WalkFront, WalkLeft, WalkRight, EstateUpper, UpperRoof, 1, upper);

            // 南の面。A と B の居間の窓と、奥の寝室の二つの窓を抜く
            var south = new List<Vector4>();
            for (var unit = 0; unit < 2; unit++)
            {
                var ox = FlatWest + FlatWide * unit;
                south.Add(new Vector4(ox + LoungeWin0, ox + LoungeWin1, EstateTop + LoungeSill, EstateTop + LoungeHead));
                south.Add(new Vector4(ox + RearWin0, ox + RearWin1, EstateUpper + BedSill, EstateUpper + BedHead));
                south.Add(new Vector4(ox + RearWin2, ox + RearWin3, EstateUpper + BedSill, EstateUpper + BedHead));
            }
            b.Facing.FaceZHoles(BlockBack, StairWest, WalkRight, 0f, UpperRoof, -1, south);
            // 南の面の床の帯と、戸境の柱型。帯は面から 6 cm、柱型は 10 cm 出して重ならないようにする
            foreach (var level in new[] { Floor, EstateTop, EstateUpper })
                b.Cast.Box(new Vector3((BlockWest + WallRight) * 0.5f, level - SlabThick * 0.5f, BlockBack - 0.03f),
                    new Vector3(WallRight - BlockWest, SlabThick, 0.06f));
            for (var i = 0; i <= 3; i++)
            {
                var x = FlatWest + FlatWide * i;
                b.Cast.Box(new Vector3(x, UpperRoof * 0.5f, BlockBack - 0.05f), new Vector3(0.24f, UpperRoof, 0.1f));
            }

            // 妻壁。西は階段の裏の塊ごと、東はデッキの端を閉じる所まで
            b.Facing.Box(new Vector3((BlockWest + StairWest) * 0.5f, UpperRoof * 0.5f, (BlockBack + EstateFace) * 0.5f),
                new Vector3(StairWest - BlockWest, UpperRoof, EstateFace - BlockBack));
            b.Facing.Box(new Vector3((BlockWest + StairWest) * 0.5f, (WalkRoof + UpperRoof) * 0.5f, (EstateFace + WalkFront) * 0.5f),
                new Vector3(StairWest - BlockWest, UpperRoof - WalkRoof, WalkFront - EstateFace));
            b.Facing.Box(new Vector3((WalkRight + WallRight) * 0.5f, UpperRoof * 0.5f, (BlockBack + WalkFront) * 0.5f),
                new Vector3(WallRight - WalkRight, UpperRoof, WalkFront - BlockBack));

            // 陸屋根。縁を面から 6 cm 出して、屋根の線を一本通す
            b.Cast.Box(new Vector3((BlockWest + WallRight) * 0.5f, EstateRoof - SlabThick * 0.5f, (BlockBack + WalkFront) * 0.5f),
                new Vector3(WallRight - BlockWest + 0.12f, SlabThick, WalkFront - BlockBack + 0.12f));
            // 屋上の立ち上がりと笠木
            var run = WallRight - BlockWest;
            var deep = WalkFront - BlockBack;
            var midX = (BlockWest + WallRight) * 0.5f;
            var midZ = (BlockBack + WalkFront) * 0.5f;
            b.Facing.Box(new Vector3(midX, EstateParapet, WalkFront - 0.12f), new Vector3(run, 0.5f, 0.24f));
            b.Facing.Box(new Vector3(midX, EstateParapet, BlockBack + 0.12f), new Vector3(run, 0.5f, 0.24f));
            b.Facing.Box(new Vector3(BlockWest + 0.12f, EstateParapet, midZ), new Vector3(0.24f, 0.5f, deep - 0.48f));
            b.Facing.Box(new Vector3(WallRight - 0.12f, EstateParapet, midZ), new Vector3(0.24f, 0.5f, deep - 0.48f));
            b.Cast.Box(new Vector3(midX, EstateRoof + 0.53f, WalkFront - 0.12f), new Vector3(run + 0.04f, 0.06f, 0.30f));
            b.Cast.Box(new Vector3(midX, EstateRoof + 0.53f, BlockBack + 0.12f), new Vector3(run + 0.04f, 0.06f, 0.30f));
            // 屋上の水槽の小屋。平らな屋根の線に一つだけ出っ張りがあると、遠くからも棟の頭が読める
            b.Facing.Box(new Vector3(0.9f, EstateRoof + 1.0f, -18.0f), new Vector3(4.2f, 2.0f, 3.6f));
            b.Cast.Box(new Vector3(0.9f, EstateRoof + 2.05f, -18.0f), new Vector3(4.4f, 0.1f, 3.8f));
        }

        /// <summary>
        /// 北の面（デッキ側）の小物。戸口・台所の窓・メーターの物入れ・二階の窓・デッキの灯り。
        ///
        /// **戸は一戸ずつ色が違う。** 下の住戸は黄・緑・赤、上は A が青、B が赤、C が緑。
        /// A と B の戸の板は記憶が掛けるので（BuildDiveTakes の Shut / Ajar）、ここでは枠と欄間と番地だけ。
        /// 戸の色は <see cref="EstateDoorPaint"/> が返す
        /// </summary>
        static void EstateFront(EstateBanks b)
        {
            var lower = new[] { b.Yellow, b.Moss, b.Red };
            for (var unit = 0; unit < 3; unit++)
            {
                var ox = FlatWest + FlatWide * unit;
                // 地面の階（下のメゾネットの玄関と台所）
                EstateDoorway(b, ox + DoorAt, 0f, lower[unit], 2 + unit * 2);
                EstateBlind(b, ox + KitchenWin0, ox + KitchenWin1, KitchenSill, KitchenHead, EstateFace, 1, true);
                EstateMeter(b, ox + MeterAt, 0f);
                // 二階。窓だけ
                EstateBlind(b, ox + 0.8f, ox + 2.6f, Floor + 0.9f, Floor + 2.0f, EstateFace, 1, true);
                EstateBlind(b, ox + 3.4f, ox + 5.0f, Floor + 0.9f, Floor + 2.0f, EstateFace, 1, true);
                // 三階（デッキ）
                EstateMeter(b, ox + MeterAt, EstateTop);
                if (unit == 2)
                {
                    EstateDoorway(b, ox + DoorAt, EstateTop, b.Moss, 16);
                    EstateBlind(b, ox + KitchenWin0, ox + KitchenWin1, EstateTop + KitchenSill, EstateTop + KitchenHead,
                        EstateFace, 1, true);
                    EstateBlind(b, ox + FrontWin0, ox + FrontWin1, EstateUpper + BedSill, EstateUpper + BedHead,
                        WalkFront, 1, true);
                }
                else EstateDoorway(b, ox + DoorAt, EstateTop, null, 12 + unit * 2);
                // 戸の前の足拭き。デッキの側に置く
                b.Soft.Box(new Vector3(ox + DoorAt, EstateTop + 0.012f, EstateFace + 0.42f), new Vector3(0.78f, 0.024f, 0.46f));
            }
            // デッキの灯り。天井から下がる箱と、光る乳白の面。点の灯り（EstateLamps）と同じ位置
            foreach (var x in DeckLamps)
            {
                b.Gear.Box(new Vector3(x, WalkRoof - 0.05f, EstateWalk), new Vector3(0.40f, 0.10f, 0.26f));
                b.Lit.Box(new Vector3(x, WalkRoof - 0.11f, EstateWalk), new Vector3(0.32f, 0.03f, 0.18f));
            }

            // B の老夫婦の鉢植え。戸の向かいの腰壁の際に三つ並べる。
            // デッキで人が手を掛けているのはここだけにする。戸の脇はメーターの物入れと開いた戸が占める
            for (var i = 0; i < 3; i++)
            {
                var x = DoorB - 0.55f + i * 0.40f;
                var leaf = 0.22f + (i % 2) * 0.14f;
                b.Red.Box(new Vector3(x, EstateTop + 0.14f, WalkFront - 0.36f), new Vector3(0.28f, 0.28f, 0.28f));
                b.Turf.Box(new Vector3(x, EstateTop + 0.28f + leaf * 0.5f, WalkFront - 0.36f), new Vector3(0.32f, leaf, 0.30f));
            }
            // A の子どもの補助輪つきの自転車。台所の窓の西、階段を上がってすぐの所
            EstateKidBike(b, FlatWest + 0.45f, EstateTop, EstateFace + 0.30f);

            // 階段の裏の塊の地面の階。ごみ置き場の両開きの鉄の戸。ダストシュートの落ちる先
            b.Cast.Box(new Vector3(-0.40f, 1.10f, EstateFace + 0.03f), new Vector3(1.50f, 2.20f, 0.06f));
            b.Gear.Box(new Vector3(-0.40f, 1.05f, EstateFace + 0.06f), new Vector3(1.34f, 2.08f, 0.04f));
            b.Shade.FaceZ(EstateFace + 0.082f, -0.415f, -0.385f, 0.02f, 2.08f, 1);
            for (var i = 0; i < 5; i++)
            {
                b.Shade.FaceZ(EstateFace + 0.082f, -1.00f, -0.50f, 1.40f + i * 0.10f, 1.43f + i * 0.10f, 1);
                b.Shade.FaceZ(EstateFace + 0.082f, -0.30f, 0.20f, 1.40f + i * 0.10f, 1.43f + i * 0.10f, 1);
            }
        }

        /// <summary>
        /// 南の面の小物。下の住戸の居間の窓と庭への戸、二階の窓、C の三・四階の窓、階段の裏の塊の小窓
        /// </summary>
        static void EstateBack(EstateBanks b)
        {
            for (var unit = 0; unit < 3; unit++)
            {
                var ox = FlatWest + FlatWide * unit;
                EstateBlind(b, ox + 0.6f, ox + 3.4f, 0.8f, 2.2f, BlockBack, -1, true);
                // 庭へ出る硝子戸。敷居は地面の高さなので窓台は付けない
                EstateBlind(b, ox + 4.0f, ox + 4.9f, 0.05f, 2.1f, BlockBack, -1, false);
                EstateBlind(b, ox + 0.8f, ox + 2.6f, Floor + 0.9f, Floor + 2.0f, BlockBack, -1, true);
                EstateBlind(b, ox + 3.4f, ox + 5.0f, Floor + 0.9f, Floor + 2.0f, BlockBack, -1, true);
                if (unit != 2) continue;
                EstateBlind(b, ox + LoungeWin0, ox + LoungeWin1, EstateTop + LoungeSill, EstateTop + LoungeHead, BlockBack, -1, true);
                EstateBlind(b, ox + RearWin0, ox + RearWin1, EstateUpper + BedSill, EstateUpper + BedHead, BlockBack, -1, true);
                EstateBlind(b, ox + RearWin2, ox + RearWin3, EstateUpper + BedSill, EstateUpper + BedHead, BlockBack, -1, true);
            }
            for (var f = 1; f <= 3; f++)
                EstateBlind(b, -0.40f, 0.40f, Floor * f + 1.0f, Floor * f + 1.9f, BlockBack, -1, false);
        }

        /// <summary>
        /// 地面の階の前。下の住戸の前庭・塀・門・小道と、階段の下から北へ出る小道。
        ///
        /// 前庭はデッキの下から一歩外まで。**塀は腰より低く。** 高い塀で囲うと、
        /// 階段の下から見たときに一階の玄関が並んでいることが読めない
        /// </summary>
        static void EstateGround(EstateBanks b)
        {
            const float wallHigh = 0.72f;
            const float thick = 0.22f;
            // 階段の下と、そこから北へ出る小道。敷地の門まで敷地の担当が繋ぐ
            b.Cast.FaceY(0.03f, StairWest, StairEast, EstateFace, WalkFront, 1);
            b.Cast.FaceY(0.03f, FlatWest, PathEast, EstateFace, GardenEdge, 1);

            for (var unit = 0; unit < 3; unit++)
            {
                var ox = FlatWest + FlatWide * unit;
                // 西の塀の芯。A は小道との境なので小道の東の縁の内側、B と C は戸境の線の上
                var side = unit == 0 ? PathEast + thick * 0.5f : ox;
                var gate0 = ox + DoorAt - 0.50f;
                var gate1 = ox + DoorAt + 0.50f;
                // 北の塀。門のところだけ空ける。東の端は隣の前庭の西の塀（C は妻壁）まで
                EstateLowWall(b, side - thick * 0.5f, gate0, GardenEdge - thick * 0.5f, wallHigh, true);
                EstateLowWall(b, gate1, ox + FlatWide, GardenEdge - thick * 0.5f, wallHigh, true);
                // 門柱。塀より一段高い
                for (var i = 0; i < 2; i++)
                    b.Facing.Box(new Vector3(i == 0 ? gate0 - 0.15f : gate1 + 0.15f, 0.46f, GardenEdge - thick * 0.5f),
                        new Vector3(0.30f, 0.92f, 0.30f));
                // 西の塀（隣の前庭との境）。A は小道との境
                EstateLowWall(b, EstateFace, GardenEdge - thick, side, wallHigh, false);
                // 小道。門から玄関まで、戸の幅で。敷石の目地を横に入れる
                b.Cast.FaceY(0.03f, ox + DoorAt - DoorHalf, ox + DoorAt + DoorHalf, EstateFace, GardenEdge - thick, 1);
                for (var i = 1; i < 5; i++)
                    b.Shade.FaceY(0.034f, ox + DoorAt - DoorHalf, ox + DoorAt + DoorHalf,
                        EstateFace + i * 0.55f - 0.01f, EstateFace + i * 0.55f + 0.01f, 1);
                // 芝。小道の両脇
                var east = unit == 2 ? WalkRight : ox + FlatWide - thick * 0.5f;
                b.Turf.FaceY(0.02f, side + thick * 0.5f, ox + DoorAt - DoorHalf, EstateFace, GardenEdge - thick, 1);
                b.Turf.FaceY(0.02f, ox + DoorAt + DoorHalf, east, EstateFace, GardenEdge - thick, 1);
                // ごみの缶と、塀際の低い植え込み
                b.Gear.Box(new Vector3(ox + 3.55f, 0.36f, EstateFace + 0.45f), new Vector3(0.46f, 0.72f, 0.46f));
                b.Gear.Box(new Vector3(ox + 3.55f, 0.75f, EstateFace + 0.45f), new Vector3(0.52f, 0.06f, 0.52f));
                b.Turf.Box(new Vector3(ox + 1.9f, 0.35f, GardenEdge - thick - 0.35f), new Vector3(1.6f, 0.70f, 0.55f));
            }
        }

        /// <summary>
        /// 前庭の低い塀。煉瓦の本体とコンクリートの笠木。
        /// <paramref name="alongX"/> が真なら x へ伸び（<paramref name="from"/>〜<paramref name="to"/> は x、
        /// <paramref name="at"/> は z）、偽なら z へ伸びる（<paramref name="at"/> は x）
        /// </summary>
        static void EstateLowWall(EstateBanks b, float from, float to, float at, float high, bool alongX)
        {
            if (to - from < 0.05f) return;
            const float thick = 0.22f;
            var mid = (from + to) * 0.5f;
            var run = to - from;
            if (alongX)
            {
                b.Facing.Box(new Vector3(mid, high * 0.5f, at), new Vector3(run, high, thick));
                b.Cast.Box(new Vector3(mid, high + 0.03f, at), new Vector3(run, 0.06f, thick + 0.06f));
            }
            else
            {
                b.Facing.Box(new Vector3(at, high * 0.5f, mid), new Vector3(thick, high, run));
                b.Cast.Box(new Vector3(at, high + 0.03f, mid), new Vector3(thick + 0.06f, 0.06f, run));
            }
        }

        /// <summary>
        /// x へ伸びる、コンクリートの腰壁に鉄の手すり。デッキの縁と二階の踊り場で同じ形を使う。
        ///
        /// 腰壁は 0.8 m、その上に鉄の手すりを 0.3 m。腰壁だけで一段目の足掛かりにならない高さを取る
        /// </summary>
        static void EstateParapetX(EstateBanks b, float z, float x0, float x1, float y)
        {
            b.Cast.Box(new Vector3((x0 + x1) * 0.5f, y + 0.4f, z), new Vector3(x1 - x0, 0.8f, 0.15f));
            b.Rail.Box(new Vector3((x0 + x1) * 0.5f, y + 1.08f, z), new Vector3(x1 - x0, 0.05f, 0.06f));
            var count = Mathf.Max(2, Mathf.RoundToInt((x1 - x0) / 1.2f) + 1);
            for (var i = 0; i < count; i++)
                b.Rail.Box(new Vector3(Mathf.Lerp(x0 + 0.05f, x1 - 0.05f, i / (float)(count - 1)), y + 0.94f, z),
                    new Vector3(0.04f, 0.28f, 0.04f));
        }

        /// <summary>z へ伸びる、コンクリートの腰壁に鉄の手すり</summary>
        static void EstateParapetZ(EstateBanks b, float x, float z0, float z1, float y)
        {
            b.Cast.Box(new Vector3(x, y + 0.4f, (z0 + z1) * 0.5f), new Vector3(0.15f, 0.8f, z1 - z0));
            b.Rail.Box(new Vector3(x, y + 1.08f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.05f, z1 - z0));
            var count = Mathf.Max(2, Mathf.RoundToInt((z1 - z0) / 1.2f) + 1);
            for (var i = 0; i < count; i++)
                b.Rail.Box(new Vector3(x, y + 0.94f, Mathf.Lerp(z0 + 0.05f, z1 - 0.05f, i / (float)(count - 1))),
                    new Vector3(0.04f, 0.28f, 0.04f));
        }

        // ---- 戸と窓 ------------------------------------------------------------

        /// <summary>
        /// 戸の色。上の住戸は A が青、B が赤、C が緑。下の住戸は黄・緑・赤。
        ///
        /// A と B の戸の板は記憶ごとに掛ける（BuildDiveTakes の Shut / Ajar）。
        /// 板の色をここに合わせれば、デッキに並ぶ戸が一戸ずつ違う色になる
        /// </summary>
        public static Material EstateDoorPaint(float x)
        {
            if (Mathf.Abs(x - DoorA) < 0.5f) return AssetDatabase.LoadAssetAtPath<Material>(Materials + "EstateBlue.mat");
            if (Mathf.Abs(x - DoorB) < 0.5f) return AssetDatabase.LoadAssetAtPath<Material>(Materials + "EstateRed.mat");
            return AssetDatabase.LoadAssetAtPath<Material>(Materials + "EstateMoss.mat");
        }

        /// <summary>
        /// 戸口ひとつ。白い枠・欄間の硝子・番地。
        ///
        /// <paramref name="leaf"/> が null の戸口は板を置かない。A と B の戸で、
        /// 記憶が <c>Shut</c> と <c>Ajar</c> で板を掛ける。建物の面には穴が抜いてあり、
        /// 番地は欄間の硝子に入れる。丁番だけ残すのは、開いた板が壁から浮いて見えないため。
        ///
        /// <paramref name="leaf"/> を渡した戸口は、閉めた戸を面の手前へ据える（C と下の住戸）。
        /// **郵便受けの口と番地は戸そのものに付ける。** ロンドンの玄関の戸は、腰の高さに
        /// 横長の郵便受けの口、その上に真鍮の番地とノッカーが付く
        /// </summary>
        static void EstateDoorway(EstateBanks b, float x, float y, Bank leaf, int number)
        {
            var z = EstateFace;
            const float jamb = DoorHalf + 0.06f;
            const float fan = 0.42f;
            // 枠。縦框は欄間の上まで通す
            for (var i = 0; i < 2; i++)
                b.Frame.Box(new Vector3(i == 0 ? x - jamb : x + jamb, y + (DoorHigh + fan + 0.08f) * 0.5f, z + 0.05f),
                    new Vector3(0.12f, DoorHigh + fan + 0.08f, 0.10f));
            b.Frame.Box(new Vector3(x, y + DoorHigh + 0.04f, z + 0.05f), new Vector3(DoorHalf * 2f, 0.08f, 0.10f));
            b.Frame.Box(new Vector3(x, y + DoorHigh + fan + 0.04f, z + 0.05f), new Vector3(jamb * 2f + 0.12f, 0.08f, 0.10f));
            // 欄間の硝子
            b.Glaze.FaceZ(z + 0.012f, x - DoorHalf, x + DoorHalf, y + DoorHigh + 0.08f, y + DoorHigh + fan, 1);

            if (leaf == null)
            {
                // 番地は欄間の硝子に白で入れる
                EstateDigits(b.Bright, x, y + DoorHigh + 0.25f, z + 0.016f, 1, number, 0.20f);
                // 丁番だけ。板は記憶が掛けるので、ここには置かない
                for (var i = 0; i < 3; i++)
                    b.Gear.Box(new Vector3(x + 0.51f, y + 0.40f + i * 0.62f, z + 0.16f), new Vector3(0.07f, 0.16f, 0.22f));
                return;
            }

            // 閉めた戸。面の手前へ据える
            leaf.Box(new Vector3(x, y + DoorHigh * 0.5f, z + 0.045f), new Vector3(DoorHalf * 2f, DoorHigh, 0.05f));
            // 鏡板。下に二枚、上に硝子の細い窓
            for (var i = 0; i < 2; i++)
                leaf.Box(new Vector3(x + (i == 0 ? -0.2f : 0.2f), y + 0.50f, z + 0.075f), new Vector3(0.30f, 0.62f, 0.015f));
            b.Glaze.FaceZ(z + 0.072f, x - 0.12f, x + 0.12f, y + 1.40f, y + 1.85f, 1);
            // 郵便受けの口。真鍮の板に横長の口
            b.Gear.Box(new Vector3(x, y + 1.00f, z + 0.078f), new Vector3(0.30f, 0.09f, 0.012f));
            b.Shade.FaceZ(z + 0.085f, x - 0.12f, x + 0.12f, y + 0.985f, y + 1.015f, 1);
            // 番地とノッカー
            EstateDigits(b.Bright, x, y + 1.24f, z + 0.074f, 1, number, 0.12f);
            b.Gear.Box(new Vector3(x, y + 1.30f + 0.12f, z + 0.085f), new Vector3(0.10f, 0.03f, 0.03f));
            // 把手と鍵
            b.Gear.Box(new Vector3(x - 0.34f, y + 1.02f, z + 0.10f), new Vector3(0.05f, 0.05f, 0.08f));
            b.Gear.Box(new Vector3(x - 0.34f, y + 1.20f, z + 0.075f), new Vector3(0.05f, 0.07f, 0.02f));
        }

        /// <summary>
        /// 中を作らない窓。面へ硝子を貼り、白い枠を細い帯で重ね、下に窓台を出す。
        ///
        /// 面に穴を抜かないのは、中が空の箱だと覗いたときに虚空が見えるため。
        /// <paramref name="nets"/> が真なら、硝子の下半分にレースの布を透かす。
        /// ロンドンの公営住宅の窓は、どの家も下半分にレースを掛けていた
        /// </summary>
        static void EstateBlind(EstateBanks b, float x0, float x1, float y0, float y1, float z, int sign, bool nets)
        {
            var s = sign > 0 ? 1f : -1f;
            b.Glaze.FaceZ(z + s * 0.010f, x0, x1, y0, y1, sign);
            if (nets) b.Paper.FaceZ(z + s * 0.012f, x0 + 0.05f, x1 - 0.05f, y0 + 0.05f, (y0 + y1) * 0.5f, sign);
            const float w = 0.06f;
            var f = z + s * 0.016f;
            b.Frame.FaceZ(f, x0 - w, x1 + w, y1, y1 + w, sign);
            b.Frame.FaceZ(f, x0 - w, x1 + w, y0 - w, y0, sign);
            b.Frame.FaceZ(f, x0 - w, x0, y0, y1, sign);
            b.Frame.FaceZ(f, x1, x1 + w, y0, y1, sign);
            // 縦の桟。幅のある窓は二つ、狭い窓は一つ
            var count = x1 - x0 > 2.2f ? 2 : 1;
            for (var i = 1; i <= count; i++)
            {
                var m = Mathf.Lerp(x0, x1, i / (float)(count + 1));
                b.Frame.FaceZ(f, m - w * 0.5f, m + w * 0.5f, y0, y1, sign);
            }
            // 上の横の桟。倒して開ける小窓
            if (y1 - y0 > 0.9f) b.Frame.FaceZ(f, x0, x1, y1 - 0.34f, y1 - 0.34f + w, sign);
            // 窓台
            if (y0 > 0.3f)
                b.Cast.Box(new Vector3((x0 + x1) * 0.5f, y0 - w - 0.03f, z + s * 0.05f), new Vector3(x1 - x0 + 0.2f, 0.06f, 0.10f));
        }

        /// <summary>
        /// メーターの物入れ。戸の脇の壁に、電気とガスのメーターを収めた細い鉄の戸。
        /// 上に通気の羽根を三本
        /// </summary>
        static void EstateMeter(EstateBanks b, float x, float y)
        {
            var z = EstateFace;
            b.Gear.Box(new Vector3(x, y + 0.92f, z + 0.02f), new Vector3(0.64f, 1.72f, 0.04f));
            for (var i = 0; i < 3; i++)
                b.Shade.FaceZ(z + 0.042f, x - 0.22f, x + 0.22f, y + 1.52f + i * 0.08f, y + 1.55f + i * 0.08f, 1);
            b.Shade.FaceZ(z + 0.042f, x + 0.20f, x + 0.24f, y + 0.95f, y + 1.05f, 1);
        }

        /// <summary>
        /// 番地の数字。七つの棒で一文字を組み、面へ貼る。
        ///
        /// 文字を貼るには TextMeshPro の面が要るが、この場所は面を一枚に焼いてしまうので、
        /// 棒を面で並べる組み方にして mesh のまま持たせる。面は表からしか見えないので箱にしない。
        /// <paramref name="sign"/> が 1 なら北（+z）を向き、読む人から見て左が +x
        /// </summary>
        static void EstateDigits(Bank b, float cx, float cy, float z, int sign, int number, float high)
        {
            var text = number.ToString();
            var wide = high * 0.55f;
            var gap = high * 0.25f;
            var span = text.Length * wide + (text.Length - 1) * gap;
            // 読む人から見て左から並べる。北を向く面では、左が +x
            var dir = sign > 0 ? -1f : 1f;
            var start = cx - dir * (span * 0.5f - wide * 0.5f);
            for (var i = 0; i < text.Length; i++)
            {
                var x = start + dir * i * (wide + gap);
                EstateGlyph(b, x, cy, z, sign, text[i] - '0', wide, high);
            }
        }

        /// <summary>七つの棒で一文字。棒の並びは a（上）b（右上）c（右下）d（下）e（左下）f（左上）g（真ん中）</summary>
        static void EstateGlyph(Bank b, float cx, float cy, float z, int sign, int digit, float wide, float high)
        {
            int[] codes = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
            if (digit < 0 || digit > 9) return;
            var bits = codes[digit];
            var t = high * 0.14f;
            var hw = wide * 0.5f;
            var hh = high * 0.5f;
            // 読む人から見た右。北を向く面では -x
            var right = sign > 0 ? -1f : 1f;
            System.Action<float, float, float, float> bar = (u0, u1, v0, v1) =>
            {
                var a = cx + right * u0;
                var c = cx + right * u1;
                b.FaceZ(z, Mathf.Min(a, c), Mathf.Max(a, c), cy + v0, cy + v1, sign);
            };
            if ((bits & 1) != 0) bar(-hw, hw, hh - t, hh);
            if ((bits & 2) != 0) bar(hw - t, hw, 0f, hh);
            if ((bits & 4) != 0) bar(hw - t, hw, -hh, 0f);
            if ((bits & 8) != 0) bar(-hw, hw, -hh, -hh + t);
            if ((bits & 16) != 0) bar(-hw, -hw + t, -hh, 0f);
            if ((bits & 32) != 0) bar(-hw, -hw + t, 0f, hh);
            if ((bits & 64) != 0) bar(-hw, hw, -t * 0.5f, t * 0.5f);
        }

        // ---- 外階段の道具 ------------------------------------------------------

        /// <summary>
        /// 半階ぶんの段。踏み面と蹴込みを一段ずつ張り、角へ段鼻を跨がせる。
        ///
        /// <paramref name="run"/> は z へ伸びる長さで、符号が上がっていく向きを持つ。
        /// 折り返しで一本ごとに向きが裏返るので、蹴込みの表裏と段鼻の乗る辺をここで振り分ける
        /// </summary>
        static void EstateFlight(Bank b, Bank nose, float xMid, float zFoot, float yFoot, float run)
        {
            var rise = Floor * 0.5f;
            var d = run / StepCount;
            var r = rise / StepCount;
            var half = StairWide * 0.5f;
            // 蹴込みは上がってくる側へ向ける。取り違えると段が裏返って、下から見上げたとき消える
            var facing = run > 0f ? -1 : 1;
            var lip = run > 0f ? -0.008f : 0.008f;
            for (var i = 0; i < StepCount; i++)
            {
                var z0 = zFoot + d * i;
                var z1 = zFoot + d * (i + 1);
                b.FaceZ(z0, xMid - half, xMid + half, yFoot + i * r, yFoot + (i + 1) * r, facing);
                b.FaceY(yFoot + (i + 1) * r, xMid - half, xMid + half,
                    Mathf.Min(z0, z1), Mathf.Max(z0, z1), 1);
                // 段鼻は角へ跨がせる。踏み面の上に載せるだけでは、見上げたとき蹴込みしか見えず、
                // 段が数えられない。下から見上げる絵がこの場所のいちばん長い絵なので、そこを優先する
                nose.Box(new Vector3(xMid, yFoot + (i + 1) * r - 0.006f, z0 + lip),
                    new Vector3(StairWide - 0.04f, 0.05f, 0.075f));
            }
            // 段の裏。下から見上げたときに段が宙に浮いて見えないように、斜めの板を一枚通す
            var dir = new Vector3(0f, rise, run).normalized;
            b.Box(new Vector3(xMid, yFoot + rise * 0.5f - 0.24f, zFoot + run * 0.5f),
                new Vector3(StairWide, 0.28f, Mathf.Sqrt(run * run + rise * rise)),
                Quaternion.LookRotation(dir, Vector3.up));
        }

        /// <summary>斜めの手すり。段と同じ勾配で二本通し、支柱を 0.9 m 間隔で立てる</summary>
        static void EstateSlope(Bank b, float xRail, float zFoot, float yFoot, float run)
        {
            var rise = Floor * 0.5f;
            var dir = new Vector3(0f, rise, run).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var len = Mathf.Sqrt(run * run + rise * rise);
            for (var i = 0; i < 2; i++)
            {
                var lift = 0.55f + i * 0.45f;
                b.Box(new Vector3(xRail, yFoot + rise * 0.5f + lift, zFoot + run * 0.5f),
                    new Vector3(0.05f, 0.05f, len), rot);
            }
            var count = Mathf.FloorToInt(len / 0.9f) + 1;
            for (var i = 0; i < count; i++)
            {
                var k = (0.45f + i * 0.9f) / len;
                if (k > 1f) break;
                b.Box(new Vector3(xRail, yFoot + rise * k + 0.48f, zFoot + run * k),
                    new Vector3(0.05f, 0.96f, 0.05f));
            }
        }

        /// <summary>z へ伸びる水平の手すり。二本と、0.9 m 間隔に均した支柱</summary>
        static void EstateRailZ(Bank b, float x, float z0, float z1, float y)
        {
            for (var i = 0; i < 2; i++)
                b.Box(new Vector3(x, y + 0.5f + i * 0.45f, (z0 + z1) * 0.5f),
                    new Vector3(0.05f, 0.05f, z1 - z0));
            EstatePosts(b, new Vector3(x, y + 0.48f, z0 + 0.05f), new Vector3(x, y + 0.48f, z1 - 0.05f));
        }

        /// <summary>x へ伸びる水平の手すり</summary>
        static void EstateRailX(Bank b, float z, float x0, float x1, float y)
        {
            for (var i = 0; i < 2; i++)
                b.Box(new Vector3((x0 + x1) * 0.5f, y + 0.5f + i * 0.45f, z),
                    new Vector3(x1 - x0, 0.05f, 0.05f));
            EstatePosts(b, new Vector3(x0 + 0.05f, y + 0.48f, z), new Vector3(x1 - 0.05f, y + 0.48f, z));
        }

        /// <summary>
        /// 手すりの支柱を端から端へ並べる。
        ///
        /// 間隔を 0.9 m に決め打ちせず端で割り切るのは、折り返しの踊り場のように
        /// 1.3 m しか無い縁でも、両端に必ず一本ずつ立つようにするため
        /// </summary>
        static void EstatePosts(Bank b, Vector3 from, Vector3 to)
        {
            var count = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(from, to) / 0.9f) + 1);
            for (var i = 0; i < count; i++)
                b.Box(Vector3.Lerp(from, to, i / (float)(count - 1)), new Vector3(0.05f, 0.96f, 0.05f));
        }

        /// <summary>井戸の西の壁の窓。硝子の面と縦の桟だけ</summary>
        static void EstateHole(EstateBanks b, float y, float z, bool lit)
        {
            var into = lit ? b.Lit : b.Shade;
            into.FaceX(StairWest + 0.02f, z - 0.55f, z + 0.55f, y, y + 1.15f, 1);
            b.Gear.Box(new Vector3(StairWest + 0.04f, y + 0.575f, z), new Vector3(0.03f, 1.19f, 0.05f));
        }

        /// <summary>
        /// 自転車。輪と骨組みだけの影。前後の輪が二つ見えれば自転車に読める。
        /// **敷地の担当が駐輪の列に使っている。** 形を変えるときは知らせる
        /// </summary>
        static void EstateBike(EstateBanks b, float x, float y, float z)
        {
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(x - 0.48f + i * 0.96f, y + 0.31f, z), new Vector3(0.60f, 0.60f, 0.04f));
            b.Gear.Box(new Vector3(x, y + 0.56f, z), new Vector3(0.92f, 0.05f, 0.05f));
            b.Gear.Box(new Vector3(x - 0.10f, y + 0.42f, z), new Vector3(0.05f, 0.34f, 0.05f));
            b.Gear.Box(new Vector3(x + 0.44f, y + 0.78f, z), new Vector3(0.05f, 0.40f, 0.05f));
            b.Gear.Box(new Vector3(x + 0.44f, y + 0.98f, z), new Vector3(0.05f, 0.05f, 0.42f));
            b.Shade.Box(new Vector3(x - 0.34f, y + 0.72f, z), new Vector3(0.26f, 0.07f, 0.12f));
            b.Gear.Box(new Vector3(x + 0.30f, y + 0.72f, z), new Vector3(0.34f, 0.26f, 0.22f));
        }

        /// <summary>子どもの自転車。赤い骨組みと小さな輪、後ろに補助輪</summary>
        static void EstateKidBike(EstateBanks b, float x, float y, float z)
        {
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(x - 0.30f + i * 0.60f, y + 0.19f, z), new Vector3(0.36f, 0.36f, 0.04f));
            b.Red.Box(new Vector3(x, y + 0.36f, z), new Vector3(0.58f, 0.05f, 0.05f));
            b.Red.Box(new Vector3(x + 0.28f, y + 0.50f, z), new Vector3(0.04f, 0.30f, 0.04f));
            b.Gear.Box(new Vector3(x + 0.28f, y + 0.66f, z), new Vector3(0.04f, 0.04f, 0.34f));
            b.Shade.Box(new Vector3(x - 0.14f, y + 0.46f, z), new Vector3(0.18f, 0.05f, 0.10f));
            b.Shade.Box(new Vector3(x - 0.30f, y + 0.08f, z + 0.14f), new Vector3(0.14f, 0.14f, 0.03f));
        }

        // ---- 団地だけの道具 ----------------------------------------------------

        /// <summary>
        /// 団地だけの地の色。
        ///
        /// <see cref="Mat"/> の表は五つの場所で分け合っていて、団地のために明るくすると
        /// 他の四つまで白く飛ぶ。床を明るくして輪郭を拾わせるぶんは、ここで自前に持つ
        /// </summary>
        static Material EstatePaint(string name, Color col, float smooth)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>マテリアルを直に渡して焼く。<see cref="Mat"/> の表に無い色を使うため</summary>
        static Transform EstateEmit(Transform parent, string name, Bank bank, Material mat, bool collide)
        {
            var made = bank.Emit(parent, name, mat, collide, Generated);
            return made != null ? made.transform : null;
        }
    }
}
