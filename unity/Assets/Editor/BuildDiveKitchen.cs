using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の台所（設計書 9.1 節「kitchen（台所）」「台所の作り込み」）。
    /// ヴィクトリア朝のテラスハウスの一階。3 月の 06:55〜06:56、日の出の直後。
    ///
    /// **一目で分かる形は三つ。流し台と吊り戸棚・窓から床へ落ちる朝の四角い光・階段。**
    /// 等間隔に並ぶものとして、階段の手すりの支柱（一段に二本）と段ごとの絨毯押さえの金物、
    /// 流し台の下の扉の列、壁のタイルの目地を置く。小物（やかん・トースター・冷蔵庫のメモ・朝食・昼食の袋）はそのあと。
    ///
    /// **家の間取り。** 通りは西。玄関の戸は西の面の奥まったポーチにあり、開けると白く飛ぶ。
    /// 戸を入ると廊下が東へ伸び、突き当たりで二階への階段が南へ上がる。廊下の北の戸口の奥が台所で、
    /// 台所の東の窓の外が裏庭。廊下の南の戸口の奥が居間（通りに面した表の部屋）。
    /// 戸口・階段・玄関の位置は記憶の人と鍵打ちが決めていて（<c>BuildDiveTakes.cs</c>）、家の方をそれに合わせてある。
    /// 前は場面 3 の部屋を写した形で、三和土と揃えた靴があった。日本に固有の物は置かない。
    ///
    /// **朝の光は本物の日の灯りで落とす。** 日（Morning）は東南東の仰角 27 度から差す、影を落とす Directional。
    /// 家の外側を影だけ落とす殻（<see cref="KitchenShadowShell"/>）で囲い、東の壁に窓の穴だけを開けておくと、
    /// 床の四角・窓の桟の影・窓台の鉢の影が、空の絵の日と同じ向きで勝手に出る。
    /// 前は光る板を床に四枚寝かせて描いていたが、窓の桟の割り付けと日の向きを別々に持つので、食い違いが直せなかった。
    ///
    /// **公営住宅と同じ三段で距離を作る。**
    /// <list type="table">
    /// <item><term>近く（このファイル）</term><description>家の中。台所・廊下・階段・玄関・ポーチ・居間</description></item>
    /// <item><term>中（<c>BuildDiveKitchenYard.cs</c>）</term><description>窓の外の裏庭。木の塀、小屋、物干し、隣の家の裏の張り出し</description></item>
    /// <item><term>遠く（<c>BuildDiveKitchenFar.cs</c>）</term><description>朝の空と、撮って貼る隣の並びの裏側と煙突</description></item>
    /// </list>
    ///
    /// 寸法は場所のローカル。+x が東、+z が北。鍵打ちが見る点は public のまま残す。
    /// 道具の名前は <c>Kitchen</c> で始める。公営住宅の <c>KitchenWall</c>・<c>KitchenWin0</c> などとは別の名前にしてある
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 目印（鍵打ちが見る） ------------------------------------------------

        /// <summary>台所と廊下を分ける壁の台所の側の面。戸口はここに開く</summary>
        public const float KitchenDoorZ = 0f;
        /// <summary>流しの前。記憶 5 のマークが皿を洗っている</summary>
        public static readonly Vector3 Sink = new Vector3(-1.1f, 0f, 2.35f);
        /// <summary>玄関の戸の前</summary>
        public static readonly Vector3 Entrance = new Vector3(-1.95f, 0f, -1.95f);
        /// <summary>階段の真ん中の線と、下端と上端。廊下の東の端を南へ上がる</summary>
        public const float StairX = 1.9f;
        public const float StairFoot = -0.6f;
        public const float StairHead = -4.4f;
        /// <summary>一階の床から二階の床まで。一階の天井もこの高さ</summary>
        const float HouseHigh = 2.6f;

        // ---- 間取り --------------------------------------------------------------

        /// <summary>台所の北の壁（流しの背）と、東の壁（裏庭の側）、西の壁（表の側）の内側の面</summary>
        const float KitchenBack = 3.2f;
        const float KitchenEast = 2.5f;
        const float KitchenWest = -2.5f;
        /// <summary>東の外壁の厚み。窓の穴の奥行きになる</summary>
        const float KitchenRearSkin = 0.25f;
        /// <summary>台所と廊下を分ける壁の廊下の側の面</summary>
        const float KitchenHallNorth = KitchenDoorZ - 0.1f;
        /// <summary>台所の戸口の幅の半分と高さ。戸口の人（記憶 5 の妻・6 のリンダ・12 の母）はこの中に立つ</summary>
        const float KitchenGap = 0.55f;
        const float KitchenGapHigh = 2.05f;
        /// <summary>廊下と居間を分ける壁。廊下の側と居間の側の面</summary>
        const float KitchenHallSouth = -2.75f;
        const float KitchenLoungeNorth = -2.85f;
        /// <summary>居間の南の壁と東の壁の内側の面</summary>
        const float KitchenLoungeSouth = -5.0f;
        const float KitchenLoungeEast = 1.25f;
        /// <summary>居間の戸口の西と東の縁</summary>
        const float KitchenLoungeGap0 = -0.75f;
        const float KitchenLoungeGap1 = 0.15f;
        /// <summary>
        /// 階段の井戸の西の縁（開いた側の手すりの線）。階段は西の縁から東の外壁まで
        /// </summary>
        const float KitchenStairWest = 1.35f;
        /// <summary>段の数。一段の蹴上げと踏み面</summary>
        const int KitchenRisers = 15;
        const float KitchenRiseH = HouseHigh / KitchenRisers;                               // 0.173
        const float KitchenGo = (StairFoot - StairHead) / KitchenRisers;                   // 0.253
        /// <summary>
        /// 階段の上が二階へ抜ける所の北の縁。ここより北（段の下の方）は二階の床が被さり、天井は一階の高さのまま。
        /// 段の途中から見下ろすと、この縁の下から廊下と台所の戸口が見える
        /// </summary>
        const float KitchenWell = -1.5f;
        /// <summary>階段の井戸の天井。二階の天井の高さ</summary>
        const float KitchenVoidTop = 5.0f;
        /// <summary>玄関の戸の開口。z の両端と戸の高さ、上の明かり取りの上端</summary>
        const float KitchenFront0 = -2.4f;
        const float KitchenFront1 = -1.5f;
        const float HallDoorHigh = 2.0f;
        const float KitchenFanTop = 2.45f;
        /// <summary>
        /// 玄関の外のポーチ（奥まった戸の前）。記憶 12 の同級生（-3.15, -1.95）がここに立つ。
        /// 奥の口は白く飛ぶ光で塞ぎ、その手前で見えない仕切りが止める
        /// </summary>
        const float PorchEdge = -3.65f;
        const float PorchZ0 = -2.75f;
        const float PorchZ1 = -1.15f;
        const float KitchenPorchHigh = 2.4f;
        /// <summary>腰壁の上端（長押の下）</summary>
        const float KitchenDadoY = 0.92f;

        // ---- 台所の造り付け ------------------------------------------------------

        /// <summary>流し台の並びの天板の高さ・手前の面・西と東の端</summary>
        const float CounterHigh = 0.9f;
        const float CounterFace = 2.65f;
        const float CounterX0 = -2.4f;
        const float CounterX1 = 1.55f;
        /// <summary>天板の上面</summary>
        const float KitchenTop = CounterHigh + 0.04f;
        /// <summary>陶器の流し（ベルファスト・シンク）の西と東の外の縁。<see cref="Sink"/> の x を真ん中に</summary>
        const float KitchenBowl0 = -1.42f;
        const float KitchenBowl1 = -0.78f;
        /// <summary>吊り戸棚の東の端。その東はコンロのフードと開いた棚</summary>
        const float KitchenHangX1 = -0.25f;
        /// <summary>冷蔵庫の西と東の縁、手前の面</summary>
        const float KitchenFridge0 = 1.62f;
        const float KitchenFridge1 = 2.40f;
        const float KitchenFridgeFace = 2.52f;
        /// <summary>
        /// 南の壁（戸口の東）の低い戸棚。昼食の袋はこの上にある。
        /// 戸口に立つ妻と母が、ここから取って手渡す（設計書 6 節の 6・7・13）
        /// </summary>
        const float KitchenSide0 = 0.85f;
        const float KitchenSide1 = 2.30f;
        const float KitchenSideDeep = 0.50f;
        /// <summary>卓の真ん中。人の通る筋（台所の真ん中）を空けたいので西の壁へ寄せる</summary>
        static readonly Vector3 KitchenTablePos = new Vector3(-1.7f, 0f, 1.3f);

        /// <summary>
        /// 東の壁の窓。裏庭を向く上げ下げ窓。床の四角はこの寸法と日の向きで決まる
        /// </summary>
        const float SashZ0 = 0.7f;
        const float SashZ1 = 1.9f;
        const float SashY0 = 0.95f;
        const float SashY1 = 2.15f;

        /// <summary>
        /// 朝の日の向き（Euler）。**空の絵の日もこの向きに描く**（<see cref="KitchenSunward"/>）。
        ///
        /// 日の出の直後の本当の日は地平から 2 度ほどで、裏の並びの屋根に隠れて窓へは届かない。
        /// 低い朝日のまま床に四角を落とすため、仰角を 27 度まで上げ、方角は東南東（96 度）に取る。
        /// 窓台の内側の縁（高さ 0.95）の光は窓から 1.9 m、窓の上の外の縁（2.15）の光は 4.2 m の所に落ち、
        /// 台所の真ん中に長さ 2.1 m の四角ができる。卓（西の壁の際）には掛からない
        /// </summary>
        static readonly Vector3 KitchenMorningAim = new Vector3(27f, 276f, 0f);

        /// <summary>
        /// 素材ごとの入れ物。台所は置く物が多いので、公営住宅と同じく束ねて持ち回る（<see cref="EstateBanks"/>）
        /// </summary>
        sealed class KitchenBanks
        {
            // 当たりを入れる面
            /// <summary>台所の床の化粧板</summary>
            public readonly Bank Lino = new Bank { Texel = 0.5f };
            /// <summary>廊下とポーチの赤い素焼きの床タイル。黒のタイルは <see cref="Black"/> で上に重ねる</summary>
            public readonly Bank Quarry = new Bank { Texel = 0.5f };
            /// <summary>居間の床板</summary>
            public readonly Bank Boards = new Bank { Texel = 0.4f };
            /// <summary>台所の壁</summary>
            public readonly Bank Walls = new Bank { Texel = 0.35f };
            /// <summary>廊下と階段の壁紙</summary>
            public readonly Bank Paper = new Bank { Texel = 0.35f };
            /// <summary>廊下の腰壁</summary>
            public readonly Bank Dado = new Bank { Texel = 0.35f };
            /// <summary>居間の壁と暖炉の張り出し</summary>
            public readonly Bank Lounge = new Bank { Texel = 0.35f };
            /// <summary>階段の段・踊り場・手すり・親柱。濃く染めた木</summary>
            public readonly Bank Treads = new Bank { Texel = 0.4f };
            // 当たりを入れない面
            public readonly Bank Ceil = new Bank { Texel = 0.35f };
            /// <summary>白く塗った木部。幅木・長押・枠・戸・手すりの支柱・ラジエーター・冷蔵庫・皿</summary>
            public readonly Bank White = new Bank { Texel = 0.5f };
            /// <summary>陶器。壁のタイル・流し・ポーチの腰のタイル</summary>
            public readonly Bank Ceramic = new Bank { Texel = 0.5f };
            /// <summary>目地と紙。タイルの目地・メモ・封筒・額の絵</summary>
            public readonly Bank Grout = new Bank { Texel = 0.5f };
            /// <summary>台所の戸棚の扉</summary>
            public readonly Bank Cupboard = new Bank { Texel = 0.5f };
            /// <summary>明るい木と紙袋。天板・卓・椅子・昼食の袋・玄関マット</summary>
            public readonly Bank Oak = new Bank { Texel = 0.5f };
            public readonly Bank Steel = new Bank { Texel = 0.5f };
            public readonly Bank Brass = new Bank { Texel = 0.5f };
            /// <summary>階段の絨毯の帯と、居間の敷物・カーテン・やかん</summary>
            public readonly Bank Runner = new Bank { Texel = 0.5f };
            /// <summary>黒。廊下の黒いタイル・暖炉・天火の戸・電子レンジの窓</summary>
            public readonly Bank Black = new Bank { Texel = 0.5f };
            public readonly Bank Cloth = new Bank { Texel = 0.5f };
            /// <summary>玄関に掛けた上着。Cloth より沈めた紺</summary>
            public readonly Bank Coat = new Bank { Texel = 0.5f };
            public readonly Bank Blue = new Bank { Texel = 0.5f };
            public readonly Bank Yellow = new Bank { Texel = 0.5f };
            public readonly Bank Moss = new Bank { Texel = 0.5f };
            public readonly Bank Turf = new Bank { Texel = 0.5f };
            public readonly Bank Glass = new Bank { Texel = 0.5f };
            /// <summary>自分で光る面。玄関の上の明かり取りと、居間のカーテンの隙間</summary>
            public readonly Bank Glow = new Bank { Texel = 0.5f };
            /// <summary>影だけを落とす殻（<see cref="KitchenShadowShell"/>）</summary>
            public readonly Bank Shade = new Bank { Texel = 0.2f };
        }

        // ---- 組み立て --------------------------------------------------------------

        /// <summary>
        /// 記憶 5・6・12 の舞台
        /// </summary>
        static void Kitchen(Transform place)
        {
            var b = new KitchenBanks();
            // 近く
            KitchenShell(b);
            KitchenHall(b);
            KitchenStair(b);
            KitchenFrontDoor(b);
            KitchenPorch(b);
            KitchenLounge(b);
            KitchenCounter(b);
            KitchenBelfast(b);
            KitchenWorktop(b);
            KitchenFridge(b);
            KitchenTable(b);
            KitchenSideUnit(b);
            KitchenWindow(b);
            KitchenShadowShell(b);

            // 地の色。台所にしか無い色だけ新しく持ち、白い木部・陶器・差し色は公営住宅の住戸と分け合う
            var lino = EstatePaint("KitchenLino", new Color(0.500f, 0.490f, 0.440f), 0.30f);
            var quarry = EstatePaint("KitchenQuarry", new Color(0.400f, 0.160f, 0.100f), 0.22f);
            var paper = EstatePaint("KitchenPaper", new Color(0.400f, 0.450f, 0.370f), 0.04f);
            var cupboard = EstatePaint("KitchenCupboard", new Color(0.440f, 0.520f, 0.520f), 0.12f);
            var oak = EstatePaint("KitchenOak", new Color(0.460f, 0.330f, 0.200f), 0.18f);
            var brass = EstatePaint("KitchenBrass", new Color(0.520f, 0.400f, 0.170f), 0.55f);

            EstateEmit(place, "KitchenFloor", b.Lino, lino, true);
            EstateEmit(place, "KitchenQuarry", b.Quarry, quarry, true);
            EstateEmit(place, "KitchenBoards", b.Boards, KitchenShared("EstateBoard"), true);
            EstateEmit(place, "KitchenWall", b.Walls, KitchenShared("EstateWallA"), true);
            EstateEmit(place, "KitchenPaper", b.Paper, paper, true);
            EstateEmit(place, "KitchenDado", b.Dado, KitchenShared("EstateCarpetB"), true);
            EstateEmit(place, "KitchenLounge", b.Lounge, KitchenShared("EstateWallB"), true);
            // 段に当たりを入れる。段鼻は段より 2 cm 出ているだけなので、上がるときに引っ掛からない
            EstateEmit(place, "KitchenTreads", b.Treads, Mat("Timber"), true);
            EstateEmit(place, "KitchenCeiling", b.Ceil, KitchenShared("EstateCeil"), false);
            EstateEmit(place, "KitchenWhite", b.White, KitchenShared("EstateFrame"), false);
            EstateEmit(place, "KitchenCeramic", b.Ceramic, KitchenShared("EstateTile"), false);
            EstateEmit(place, "KitchenGrout", b.Grout, KitchenShared("EstatePaper"), false);
            EstateEmit(place, "KitchenFittings", b.Cupboard, cupboard, false);
            EstateEmit(place, "KitchenOak", b.Oak, oak, false);
            EstateEmit(place, "KitchenMetal", b.Steel, Mat("Rail"), false);
            EstateEmit(place, "KitchenBrass", b.Brass, brass, false);
            EstateEmit(place, "KitchenRunner", b.Runner, KitchenShared("EstateRed"), false);
            EstateEmit(place, "KitchenBlack", b.Black, Mat("Ceiling"), false);
            EstateEmit(place, "KitchenCloth", b.Cloth, Mat("Cloth"), false);
            EstateEmit(place, "KitchenCoat", b.Coat, Mat("Coat"), false);
            EstateEmit(place, "KitchenBlue", b.Blue, KitchenShared("EstateBlue"), false);
            EstateEmit(place, "KitchenYellow", b.Yellow, KitchenShared("EstateYellow"), false);
            EstateEmit(place, "KitchenMoss", b.Moss, KitchenShared("EstateMoss"), false);
            EstateEmit(place, "KitchenTurf", b.Turf, KitchenShared("EstateTurf"), false);
            EstateEmit(place, "KitchenGlass", b.Glass, KitchenShared("EstateGlaze"), false);
            NoShadow(EstateEmit(place, "KitchenGlow", b.Glow, Glow("GlowMorning", new Color(0.92f, 0.95f, 1f), 1.8f), false));
            var shell = EstateEmit(place, "KitchenShade", b.Shade, Mat("Wall"), false);
            if (shell != null) shell.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            // ポーチの奥。開けると白く飛ぶ朝の光で、その先は無い
            Pane(place, "KitchenOutside", new Vector3(PorchEdge + 0.02f, KitchenPorchHigh * 0.5f, (PorchZ0 + PorchZ1) * 0.5f),
                new Vector2(PorchZ1 - PorchZ0, KitchenPorchHigh), Vector3.right, Glow("GlowMorning", new Color(0.92f, 0.95f, 1f), 1.8f));

            // 中（窓の外の裏庭と隣の家の張り出し）
            var y = new YardBanks();
            KitchenYard(y);
            // 樹冠の札は公園と同じ沈めた色。朝日を背にした枝が、明るい段ボールの切り抜きに見えないように
            y.Emit(place, "Kitchen", ParkCrownMat());

            // 遠く（本物の地面の縁と、撮って貼った書き割りの輪）
            FarLand(place, KitchenRing, "KitchenLand", EstateLandMat());
            Backdrop(place, KitchenRing, "HalfAware/Shoot the kitchen backdrop");

            KitchenBlocks(place);
            KitchenLights(place);
        }

        /// <summary>公営住宅の住戸と分け合う地の色。組み直しのたびに色を結び直すのは公営住宅の側なので、ここでは読むだけ</summary>
        static Material KitchenShared(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            return m != null ? m : Mat("Wall");
        }

        /// <summary>
        /// 灯り。朝の日と、窓から入る空の青い光と、廊下・階段・居間の下限。
        ///
        /// 環境光は空の絵から取った三色（<see cref="KitchenPlaceSky"/>）が受け持つ。
        /// 台所の天井の灯りは朝の白い光に寄せる。暖かいのは日の当たる所だけにしたい（設計書「青白い朝」）
        /// </summary>
        static void KitchenLights(Transform place)
        {
            var sun = Lamp(place, "Morning", LightType.Directional, new Vector3(0f, 12f, 0f),
                KitchenMorningAim, new Color(1f, 0.84f, 0.64f), 2.6f, 10f);
            sun.shadows = LightShadows.Soft;

            // 窓から入る空の光。影は落とさない。窓の側の半分を青白く持ち上げる
            var sky = Lamp(place, "WindowSky", LightType.Spot, new Vector3(2.3f, 1.95f, (SashZ0 + SashZ1) * 0.5f),
                new Vector3(32f, 270f, 0f), new Color(0.78f, 0.86f, 1f), 5.5f, 7.5f);
            sky.spotAngle = 125f;
            sky.innerSpotAngle = 60f;

            // 日と反対の空（西の高い所）から来る青い光の代わり。影は落とさない。
            // 日を背にした庭の物（塀の表・小屋の戸・物干しの洗濯物）が、窓から黒い影だけに見えないように。
            // 家の中の西を向く面（窓の壁）も少し持ち上げるが、空の光が窓から入るのと同じ向きなので嘘にならない
            Lamp(place, "KitchenFill", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(38f, 80f, 0f), new Color(0.66f, 0.74f, 0.92f), 0.5f, 10f);

            // 台所の天井の灯り。吊った笠の中から下へ。点の灯りにすると笠の上の天井が白く飛んだ
            var bulb = Lamp(place, "Bulb", LightType.Spot, new Vector3(-0.3f, 2.08f, 1.35f),
                new Vector3(90f, 0f, 0f), new Color(0.92f, 0.94f, 1f), 3.2f, 5.5f);
            bulb.spotAngle = 150f;
            bulb.innerSpotAngle = 70f;
            // 床の四角の照り返し。日の当たった床から天井と壁の上の方へ返る、少し暖かい光。
            // これが無いと、笠で天井を照らさなくなった分、部屋の上半分が沈む
            // 床から上へ向けた広い spot にする。点の灯りでは床の上に白い溜まりができて、四角の形を消した
            var bounce = Lamp(place, "SunBounce", LightType.Spot, new Vector3(-0.4f, 0.05f, 1.6f),
                new Vector3(-90f, 0f, 0f), new Color(1f, 0.92f, 0.80f), 1.6f, 5.0f);
            bounce.spotAngle = 165f;
            bounce.innerSpotAngle = 90f;

            // 玄関。開いた戸の白い外の光がここまでは届いている
            Lamp(place, "HallGlow", LightType.Point, new Vector3(-2.15f, 1.7f, -1.95f),
                Vector3.zero, new Color(0.90f, 0.94f, 1f), 4.0f, 5.0f);
            // 階段の上。二階の窓から落ちてくる朝の光。上がるほど明るく、廊下は暗がりのまま
            Lamp(place, "StairGlow", LightType.Point, new Vector3(1.9f, 4.4f, -3.6f),
                Vector3.zero, new Color(0.84f, 0.89f, 1f), 3.0f, 5.5f);
            // 居間。カーテンを引いたままで、隙間から入る光だけ
            Lamp(place, "LoungeGlow", LightType.Point, new Vector3(-1.9f, 1.7f, -3.9f),
                Vector3.zero, new Color(0.80f, 0.86f, 1f), 1.4f, 4.2f);
        }

        // ---- 殻 ------------------------------------------------------------------

        /// <summary>
        /// 床・壁・天井。台所・廊下・居間・階段の井戸・ポーチ。
        /// 壁の厚みは戸口の所だけ持たせ、あとは内側の面だけを張る。外から見る所は無い
        /// </summary>
        static void KitchenShell(KitchenBanks b)
        {
            // 床
            b.Lino.FaceY(0f, KitchenWest, KitchenEast, KitchenDoorZ, KitchenBack, 1);
            b.Quarry.FaceY(0f, KitchenWest, KitchenEast, KitchenHallSouth, KitchenHallNorth, 1);
            b.Boards.FaceY(0f, KitchenWest, KitchenLoungeEast, KitchenLoungeSouth, KitchenLoungeNorth, 1);
            // 戸口の敷居
            b.Treads.FaceY(0.006f, -KitchenGap, KitchenGap, KitchenHallNorth, KitchenDoorZ, 1);
            b.Treads.FaceY(0.006f, KitchenLoungeGap0, KitchenLoungeGap1, KitchenLoungeNorth, KitchenHallSouth, 1);
            KitchenHallTiles(b);

            // 台所の壁。東に窓、南に戸口
            b.Walls.FaceZ(KitchenBack, KitchenWest, KitchenEast, 0f, HouseHigh, -1);
            b.Walls.FaceX(KitchenWest, KitchenDoorZ, KitchenBack, 0f, HouseHigh, 1);
            b.Walls.FaceXHoles(KitchenEast, KitchenDoorZ, KitchenBack, 0f, HouseHigh, -1,
                new List<Vector4> { new Vector4(SashZ0, SashZ1, SashY0, SashY1) });
            b.Walls.FaceZHoles(KitchenDoorZ, KitchenWest, KitchenEast, 0f, HouseHigh, 1,
                new List<Vector4> { new Vector4(-KitchenGap, KitchenGap, 0f, KitchenGapHigh) });
            // 戸口の抱き。壁の厚みを白い枠で塞ぐ
            b.White.FaceX(-KitchenGap, KitchenHallNorth, KitchenDoorZ, 0f, KitchenGapHigh, 1);
            b.White.FaceX(KitchenGap, KitchenHallNorth, KitchenDoorZ, 0f, KitchenGapHigh, -1);
            b.White.FaceY(KitchenGapHigh, -KitchenGap, KitchenGap, KitchenHallNorth, KitchenDoorZ, -1);
            KitchenSkirtZ(b, KitchenBack - 0.01f, KitchenWest, CounterX0, -1);
            KitchenSkirtX(b, KitchenWest + 0.01f, KitchenDoorZ, KitchenBack, 1);
            KitchenSkirtX(b, KitchenEast - 0.01f, KitchenDoorZ, KitchenFridgeFace, -1);
            KitchenSkirtZ(b, KitchenDoorZ + 0.01f, KitchenWest, -KitchenGap - 0.08f, 1);
            KitchenSkirtZ(b, KitchenDoorZ + 0.01f, KitchenGap + 0.08f, KitchenSide0, 1);
            // 回り縁
            KitchenCorniceZ(b, KitchenBack - 0.03f, KitchenWest, KitchenEast, -1);
            KitchenCorniceZ(b, KitchenDoorZ + 0.03f, KitchenWest, KitchenEast, 1);
            KitchenCorniceX(b, KitchenWest + 0.03f, KitchenDoorZ, KitchenBack, 1);
            KitchenCorniceX(b, KitchenEast - 0.03f, KitchenDoorZ, KitchenBack, -1);

            // 天井。廊下は階段の井戸の北の縁まで、そこから南は階段の上が二階へ抜ける
            b.Ceil.FaceY(HouseHigh, KitchenWest, KitchenEast, KitchenDoorZ, KitchenBack, -1);
            b.Ceil.FaceY(HouseHigh, KitchenWest, KitchenStairWest, KitchenHallSouth, KitchenHallNorth, -1);
            b.Ceil.FaceY(HouseHigh, KitchenStairWest, KitchenEast, KitchenWell, KitchenHallNorth, -1);
            b.Ceil.FaceY(KitchenVoidTop, KitchenStairWest, KitchenEast, KitchenLoungeSouth, KitchenWell, -1);
            b.Ceil.FaceY(HouseHigh, KitchenWest, KitchenLoungeEast, KitchenLoungeSouth, KitchenLoungeNorth, -1);
        }

        /// <summary>
        /// 廊下の床の黒いタイル。赤い素焼きの床の上に 2 mm 浮かせ、30 cm 角の市松に並べる。
        /// ヴィクトリア朝の家の玄関から奥へ続く、赤と黒の床。階段の下に隠れる所は並べない
        /// </summary>
        static void KitchenHallTiles(KitchenBanks b)
        {
            const float tile = 0.3f;
            var nx = Mathf.CeilToInt((KitchenEast - KitchenWest) / tile);
            var nz = Mathf.CeilToInt((KitchenHallNorth - KitchenHallSouth) / tile);
            for (var i = 0; i < nx; i++)
                for (var k = 0; k < nz; k++)
                {
                    if (((i + k) & 1) == 1) continue;
                    var x0 = KitchenWest + i * tile;
                    var x1 = Mathf.Min(x0 + tile, KitchenEast);
                    var z1 = KitchenHallNorth - k * tile;
                    var z0 = Mathf.Max(z1 - tile, KitchenHallSouth);
                    if (x0 >= KitchenStairWest && z1 <= StairFoot) continue;
                    b.Black.FaceY(0.002f, x0, x1, z0, z1, 1);
                }
        }

        // ---- 廊下 ----------------------------------------------------------------

        /// <summary>
        /// 廊下の壁。腰から下は濃い色の腰壁、長押の白い帯、その上に壁紙。足元に白い幅木、天井に回り縁。
        /// 階段の東の壁は腰壁と長押が段に沿って上がる。
        /// 小物（コート掛け・ラジエーター・額・玄関マットと郵便・靴）もここ
        /// </summary>
        static void KitchenHall(KitchenBanks b)
        {
            var kitchenGap = new List<Vector4> { new Vector4(-KitchenGap, KitchenGap, 0f, KitchenGapHigh) };
            var loungeGap = new List<Vector4> { new Vector4(KitchenLoungeGap0, KitchenLoungeGap1, 0f, KitchenGapHigh) };
            var frontGap = new List<Vector4> { new Vector4(KitchenFront0, KitchenFront1, 0f, KitchenFanTop) };
            KitchenBandZ(b, KitchenHallNorth, KitchenWest, KitchenEast, -1, kitchenGap);
            KitchenBandZ(b, KitchenHallSouth, KitchenWest, KitchenStairWest, 1, loungeGap);
            KitchenBandX(b, KitchenWest, KitchenHallSouth, KitchenHallNorth, 1, frontGap);
            // 居間の戸口の抱き
            b.White.FaceX(KitchenLoungeGap0, KitchenLoungeNorth, KitchenHallSouth, 0f, KitchenGapHigh, 1);
            b.White.FaceX(KitchenLoungeGap1, KitchenLoungeNorth, KitchenHallSouth, 0f, KitchenGapHigh, -1);
            b.White.FaceY(KitchenGapHigh, KitchenLoungeGap0, KitchenLoungeGap1, KitchenLoungeNorth, KitchenHallSouth, -1);

            // 戸口の額縁。台所の側・廊下の側・居間の戸口の両側
            KitchenArchitraveZ(b, KitchenDoorZ + 0.012f, -KitchenGap, KitchenGap, KitchenGapHigh, 1);
            KitchenArchitraveZ(b, KitchenHallNorth - 0.012f, -KitchenGap, KitchenGap, KitchenGapHigh, -1);
            KitchenArchitraveZ(b, KitchenHallSouth + 0.012f, KitchenLoungeGap0, KitchenLoungeGap1, KitchenGapHigh, 1);
            KitchenArchitraveZ(b, KitchenLoungeNorth - 0.012f, KitchenLoungeGap0, KitchenLoungeGap1, KitchenGapHigh, -1);

            // 階段の井戸の壁。東は外壁、西は二階の床の縁から上と、上の段の脇（居間の東の壁の裏）、南は踊り場の奥
            b.Paper.FaceX(KitchenEast, KitchenLoungeSouth, KitchenHallNorth, 0f, KitchenVoidTop, -1);
            b.Paper.FaceX(KitchenStairWest, KitchenLoungeSouth, KitchenWell, HouseHigh, KitchenVoidTop, 1);
            b.Paper.FaceX(KitchenStairWest, KitchenLoungeSouth, KitchenHallSouth, 0f, HouseHigh, 1);
            b.Paper.FaceZ(KitchenWell, KitchenStairWest, KitchenEast, HouseHigh, KitchenVoidTop, -1);
            b.Paper.FaceZ(KitchenLoungeSouth, KitchenStairWest, KitchenEast, HouseHigh, KitchenVoidTop, 1);
            // 階段の脇の腰壁。東の壁は段の下（床の高さ）から、西の壁は開いた手すりが終わる所から
            KitchenStairDado(b, KitchenEast - 0.004f, -1, KitchenHallNorth, StairHead);
            KitchenStairDado(b, KitchenStairWest + 0.004f, 1, KitchenHallSouth, StairHead);
            // 踊り場の幅木と、二階へ上がった所の戸（閉めたまま）
            KitchenSkirtZ(b, KitchenLoungeSouth + 0.01f, KitchenStairWest, KitchenEast, 1, HouseHigh);
            KitchenSkirtX(b, KitchenStairWest + 0.01f, KitchenLoungeSouth, StairHead, 1, HouseHigh);
            KitchenSkirtX(b, KitchenEast - 0.01f, KitchenLoungeSouth, StairHead, -1, HouseHigh);
            KitchenDoorLeafZ(b, KitchenLoungeSouth + 0.022f, StairX, HouseHigh, 0.78f, 1);
            KitchenArchitraveZ(b, KitchenLoungeSouth + 0.012f, StairX - 0.40f, StairX + 0.40f, HouseHigh + 1.97f, 1, HouseHigh);

            KitchenHallThings(b);
        }

        /// <summary>
        /// 廊下の小物。
        /// どれも人の歩く筋（台所の戸口から玄関へ斜めに下る線と、階段の下から台所の戸口へ）から外してある
        /// </summary>
        static void KitchenHallThings(KitchenBanks b)
        {
            // コート掛け。台所の壁の廊下の側、玄関の戸が開いて寄る所の東。板に真鍮の鉤を四つ
            const float hz = KitchenHallNorth;
            b.Treads.Box(new Vector3(-1.75f, 1.66f, hz - 0.015f), new Vector3(0.95f, 0.09f, 0.03f));
            for (var i = 0; i < 4; i++)
                b.Brass.Box(new Vector3(-2.10f + i * 0.24f, 1.66f, hz - 0.05f), new Vector3(0.025f, 0.03f, 0.07f));
            // 長いコート・紺の上着・襟巻き・鞄。掛けた形に見せる作りは KitchenGarment・KitchenBag に任せる
            KitchenGarment(b.Cloth, -2.10f, hz, 1.63f, 0.95f, 0.32f, 0.44f);
            KitchenGarment(b.Coat, -1.86f, hz, 1.59f, 0.52f, 0.24f, 0.32f);
            b.Runner.Box(new Vector3(-1.52f, 1.22f, hz - 0.07f), new Vector3(0.10f, 0.80f, 0.03f));
            KitchenBag(b.Treads, b.Brass, -1.38f, -1.34f, hz);
            // 靴。コートの下に二足、踵を壁へ向けて脱いだまま
            KitchenShoe(b.Black, new Vector3(-2.18f, 0f, hz - 0.30f), 8f);
            KitchenShoe(b.Black, new Vector3(-2.02f, 0f, hz - 0.28f), -4f);
            KitchenShoe(b.White, new Vector3(-1.78f, 0f, hz - 0.33f), 14f);
            KitchenShoe(b.White, new Vector3(-1.62f, 0f, hz - 0.30f), 2f);

            // 玄関マットと、郵便受けの口から落ちた封筒
            b.Oak.Box(new Vector3(-2.18f, 0.008f, -1.95f), new Vector3(0.52f, 0.016f, 0.80f));
            b.Grout.Box(new Vector3(-2.26f, 0.019f, -1.86f), new Vector3(0.11f, 0.004f, 0.22f), Quaternion.Euler(0f, 18f, 0f));
            b.Grout.Box(new Vector3(-2.12f, 0.020f, -2.02f), new Vector3(0.16f, 0.004f, 0.23f), Quaternion.Euler(0f, -9f, 0f));

            // ラジエーター。居間の壁の廊下の側、玄関の脇。上に額を二つ
            KitchenRadiatorZ(b, KitchenHallSouth + 0.05f, -2.25f, -1.30f, 1);
            KitchenFrameZ(b, KitchenHallSouth + 0.01f, -1.98f, 1.62f, 0.38f, 0.30f, 1, b.Moss);
            KitchenFrameZ(b, KitchenHallSouth + 0.01f, -1.50f, 1.70f, 0.30f, 0.24f, 1, b.Blue);

            // 階段の壁の額。段に沿って三つ上がる
            for (var i = 0; i < 3; i++)
            {
                var z = -1.3f - i * 1.0f;
                KitchenFrameX(b, KitchenEast - 0.01f, z, KitchenPitch(z) + 1.45f, 0.36f, 0.28f, -1, i == 1 ? b.Blue : b.Moss);
            }

            // 台所の壁の廊下の側、階段の足元の上。額を三つ。階段を降りてくると正面に来る
            KitchenFrameZ(b, KitchenHallNorth - 0.01f, 1.55f, 1.60f, 0.56f, 0.42f, -1, b.Yellow);
            KitchenFrameZ(b, KitchenHallNorth - 0.01f, 2.12f, 1.72f, 0.30f, 0.24f, -1, b.Moss);
            KitchenFrameZ(b, KitchenHallNorth - 0.01f, 0.95f, 1.70f, 0.26f, 0.32f, -1, b.Blue);
            // 廊下の吊り灯り。点けていない。真鍮の枠に曇り硝子の提灯
            b.Brass.Box(new Vector3(-1.0f, 2.48f, -1.45f), new Vector3(0.012f, 0.24f, 0.012f));
            b.Brass.Box(new Vector3(-1.0f, 2.34f, -1.45f), new Vector3(0.22f, 0.04f, 0.22f));
            b.White.Box(new Vector3(-1.0f, 2.20f, -1.45f), new Vector3(0.17f, 0.24f, 0.17f));
            b.Brass.Box(new Vector3(-1.0f, 2.07f, -1.45f), new Vector3(0.14f, 0.03f, 0.14f));
        }

        /// <summary>靴一足の片方。踵から爪先へ、向き <paramref name="yaw"/>（+z が 0）</summary>
        static void KitchenShoe(Bank bank, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            bank.Box(at + rot * new Vector3(0f, 0.035f, 0f), new Vector3(0.10f, 0.07f, 0.28f), rot);
            bank.Box(at + rot * new Vector3(0f, 0.085f, -0.08f), new Vector3(0.09f, 0.05f, 0.11f), rot);
        }

        /// <summary>
        /// コート掛けに掛けた上着ひとつ。鉤に掛かって盛り上がる肩・両袖・裾の広がり・前身頃の皺を
        /// 箱を重ねて表す。単純な直方体のままだと近くで見たときに平たい板にしか見えない。
        /// hx は鉤の x、hz は壁の z、top は肩の峰の高さ、drop は峰から裾までの丈、
        /// chestWide は胸の幅、hemWide は裾の幅（chestWide より広く取ると裾が広がって見える）
        /// </summary>
        static void KitchenGarment(Bank cloth, float hx, float hz, float top, float drop, float chestWide, float hemWide)
        {
            var chestY = top - drop * 0.34f;
            var hemY = top - drop;
            // 肩の峰と、袖口へ落ちる傾き
            cloth.Box(new Vector3(hx, top, hz - 0.08f), new Vector3(chestWide * 0.66f, 0.08f, 0.13f));
            cloth.Box(new Vector3(hx - chestWide * 0.34f, top - 0.06f, hz - 0.08f),
                new Vector3(chestWide * 0.34f, 0.08f, 0.12f), Quaternion.Euler(0f, 0f, 30f));
            cloth.Box(new Vector3(hx + chestWide * 0.34f, top - 0.06f, hz - 0.08f),
                new Vector3(chestWide * 0.34f, 0.08f, 0.12f), Quaternion.Euler(0f, 0f, -30f));
            // 胸から裾。裾のほうを一段前へ出して広げる
            cloth.Box(new Vector3(hx, chestY, hz - 0.12f), new Vector3(chestWide, drop * 0.44f, 0.15f));
            cloth.Box(new Vector3(hx, hemY + drop * 0.14f, hz - 0.16f), new Vector3(hemWide, drop * 0.34f, 0.19f));
            // 両袖。上腕と前腕の二段に割って、外へ振れながら垂れる
            cloth.Box(new Vector3(hx - chestWide * 0.52f, chestY + drop * 0.02f, hz - 0.11f),
                new Vector3(chestWide * 0.26f, drop * 0.30f, 0.12f), Quaternion.Euler(0f, 0f, 9f));
            cloth.Box(new Vector3(hx - chestWide * 0.58f, hemY + drop * 0.18f, hz - 0.08f),
                new Vector3(chestWide * 0.22f, drop * 0.24f, 0.10f), Quaternion.Euler(0f, 0f, 15f));
            cloth.Box(new Vector3(hx + chestWide * 0.52f, chestY + drop * 0.01f, hz - 0.11f),
                new Vector3(chestWide * 0.26f, drop * 0.28f, 0.12f), Quaternion.Euler(0f, 0f, -8f));
            cloth.Box(new Vector3(hx + chestWide * 0.58f, hemY + drop * 0.16f, hz - 0.08f),
                new Vector3(chestWide * 0.22f, drop * 0.22f, 0.10f), Quaternion.Euler(0f, 0f, -14f));
            // 前身頃の皺。裾寄りの面から二筋、浮かせて立てる
            cloth.Box(new Vector3(hx - chestWide * 0.20f, hemY + drop * 0.30f, hz - 0.24f),
                new Vector3(0.05f, drop * 0.26f, 0.02f), Quaternion.Euler(0f, 0f, 8f));
            cloth.Box(new Vector3(hx + chestWide * 0.16f, hemY + drop * 0.18f, hz - 0.25f),
                new Vector3(0.05f, drop * 0.22f, 0.02f), Quaternion.Euler(0f, 0f, -6f));
        }

        /// <summary>
        /// コート掛けに掛けた鞄。肩紐で鉤から下げた形にする。革の色は階段の <see cref="KitchenBanks.Treads"/> と同じ
        /// （落ち着いた濃い茶）。留め金だけ真鍮
        /// </summary>
        static void KitchenBag(Bank leather, Bank brass, float hookX, float bodyX, float hz)
        {
            var hook = new Vector3(hookX, 1.64f, hz - 0.06f);
            var body = new Vector3(bodyX, 0.98f, hz - 0.13f);
            leather.Box(body, new Vector3(0.30f, 0.32f, 0.15f));
            leather.Box(body + new Vector3(0f, 0.15f, -0.05f), new Vector3(0.32f, 0.14f, 0.05f)); // 蓋
            brass.Box(body + new Vector3(0f, 0.02f, -0.075f), new Vector3(0.05f, 0.045f, 0.02f)); // 留め金
            // 肩紐。鉤から鞄の両肩の点まで、向きを合わせた細い箱で結ぶ
            foreach (var side in new[] { -1f, 1f })
            {
                var corner = body + new Vector3(side * 0.14f, 0.17f, 0.01f);
                var mid = (hook + corner) * 0.5f;
                var dir = (corner - hook).normalized;
                var rot = Quaternion.FromToRotation(Vector3.up, dir);
                var len = Vector3.Distance(hook, corner);
                leather.Box(mid, new Vector3(0.035f, len, 0.035f), rot);
            }
        }

        // ---- 階段 ----------------------------------------------------------------

        /// <summary>
        /// 二階へ上がる段。廊下の東の端を南へ、十五段で踊り場へ。
        ///
        /// **段鼻と絨毯の帯と、等間隔の支柱。** 段は濃く染めた木で、真ん中に赤い絨毯の帯を上から下まで通し、
        /// 段ごとに真鍮の押さえの棒で留める。開いた西の側は、一段に二本ずつ白い支柱を立てて、上に濃い木の手すり。
        /// 支柱の繰り返しと、段ごとに光る押さえの棒で、暗がりでも段が数えられる。
        /// 手すりは親柱から上の段まで一本で通し、居間の壁の脇（開いた側の終わりより上）では壁に沿う。
        /// 開いた側の段の下は、白く塗った腰板と小さな物入れの戸で塞ぐ
        /// </summary>
        static void KitchenStair(KitchenBanks b)
        {
            const float x0 = KitchenStairWest;
            const float x1 = KitchenEast;
            const float mid = (x0 + x1) * 0.5f;
            const float wide = x1 - x0;
            // 絨毯の帯の両の縁
            const float r0 = 1.58f;
            const float r1 = 2.27f;
            for (var i = 0; i < KitchenRisers; i++)
            {
                var z = StairFoot - i * KitchenGo;
                var y0 = i * KitchenRiseH;
                var y1 = (i + 1) * KitchenRiseH;
                b.Treads.FaceZ(z, x0, x1, y0, y1, 1);
                b.Treads.FaceY(y1, x0, x1, z - KitchenGo, z, 1);
                // 段鼻。踏み面の縁を蹴込みより前へ 2 cm 出す
                b.Treads.Box(new Vector3(mid, y1 - 0.012f, z + 0.008f), new Vector3(wide, 0.024f, 0.04f));
                // 絨毯の帯と押さえの棒
                b.Runner.FaceZ(z + 0.031f, r0, r1, y0, y1 - 0.024f, 1);
                b.Runner.FaceY(y1 + 0.004f, r0, r1, z - KitchenGo, z + 0.03f, 1);
                b.Brass.Box(new Vector3(mid, y1 + 0.016f, z - KitchenGo + 0.016f), new Vector3(r1 - r0 + 0.10f, 0.016f, 0.016f));
            }
            // 踊り場の床と絨毯
            b.Treads.FaceY(HouseHigh, x0, x1, KitchenLoungeSouth, StairHead, 1);
            b.Runner.FaceY(HouseHigh + 0.004f, r0, r1, KitchenLoungeSouth, StairHead + 0.03f, 1);

            // 開いた側の段の脇の板（桁）と、その下の腰板
            var footA = new Vector3(x0 - 0.02f, KitchenPitch(StairFoot), StairFoot);
            var headA = new Vector3(x0 - 0.02f, KitchenPitch(KitchenHallSouth), KitchenHallSouth);
            YardBeam(b.White, footA + new Vector3(0f, 0.02f, 0f), headA + new Vector3(0f, 0.02f, 0f), 0.05f);
            b.White.Box((footA + headA) * 0.5f + new Vector3(0f, -0.05f, 0f), new Vector3(0.04f, 0.22f, (headA - footA).magnitude),
                Quaternion.LookRotation(headA - footA, Vector3.up));
            MidQuad(b.White, new Vector3(x0, 0f, StairFoot), new Vector3(x0, 0f, KitchenHallSouth),
                new Vector3(x0, KitchenPitch(KitchenHallSouth) - 0.16f, KitchenHallSouth),
                new Vector3(x0, KitchenPitch(KitchenHallSouth) - 0.16f, KitchenHallSouth), Vector3.left);
            // 物入れの戸と腰板の枠
            b.White.Box(new Vector3(x0 - 0.012f, 0.46f, -2.28f), new Vector3(0.02f, 0.86f, 0.62f));
            b.Grout.FaceX(x0 - 0.023f, -2.56f, -2.00f, 0.08f, 0.10f, -1);
            b.Grout.FaceX(x0 - 0.023f, -2.56f, -2.00f, 0.84f, 0.86f, -1);
            b.Brass.Box(new Vector3(x0 - 0.04f, 0.50f, -2.05f), new Vector3(0.04f, 0.04f, 0.04f));
            // 手すりの支柱。一段に二本
            const float post = 1.43f;
            for (var i = 0; i < KitchenRisers; i++)
                for (var k = 0; k < 2; k++)
                {
                    var z = StairFoot - (i + 0.25f + 0.5f * k) * KitchenGo;
                    if (z < KitchenHallSouth + 0.05f) continue;
                    var foot = (i + 1) * KitchenRiseH;
                    var top = KitchenPitch(z) + 0.86f;
                    b.White.Box(new Vector3(post, (foot + top) * 0.5f, z), new Vector3(0.034f, top - foot, 0.034f));
                }
            // 手すり。親柱の頭の下から踊り場まで一本
            var railFoot = new Vector3(post, KitchenPitch(StairFoot - 0.05f) + 0.9f, StairFoot - 0.05f);
            var railHead = new Vector3(post, KitchenPitch(StairHead + 0.1f) + 0.9f, StairHead + 0.1f);
            YardBeam(b.Treads, railFoot, railHead, 0.06f);
            // 親柱。四角い柱に笠と玉
            b.Treads.Box(new Vector3(post, 0.62f, StairFoot + 0.02f), new Vector3(0.10f, 1.24f, 0.10f));
            b.Treads.Box(new Vector3(post, 1.27f, StairFoot + 0.02f), new Vector3(0.14f, 0.05f, 0.14f));
            b.Treads.Box(new Vector3(post, 1.34f, StairFoot + 0.02f), new Vector3(0.08f, 0.08f, 0.08f), Quaternion.Euler(0f, 45f, 0f));
            b.Treads.Box(new Vector3(post, 1.34f, StairFoot + 0.02f), new Vector3(0.08f, 0.08f, 0.08f), Quaternion.Euler(45f, 0f, 45f));
        }

        /// <summary>段鼻を結んだ線の高さ。手すり・腰壁・額は段からこの線で測る</summary>
        static float KitchenPitch(float z)
        {
            return Mathf.Clamp(KitchenRiseH + (StairFoot - z) * (KitchenRiseH / KitchenGo), 0f, HouseHigh);
        }

        /// <summary>
        /// 階段の脇の壁の腰壁と長押。段に沿って斜めに上がる。
        /// <paramref name="x"/> は壁の面、<paramref name="sign"/> は部屋の側の向き（x の符号）。
        /// <paramref name="fromZ"/> から <paramref name="toZ"/> まで。段の足元より北は床の高さで水平に
        /// </summary>
        static void KitchenStairDado(KitchenBanks b, float x, int sign, float fromZ, float toZ)
        {
            var n = sign > 0 ? Vector3.right : Vector3.left;
            var s = sign > 0 ? 1f : -1f;
            // 段の足元より北の水平の所
            if (fromZ > StairFoot)
            {
                b.Dado.FaceX(x, StairFoot, fromZ, 0f, KitchenDadoY, sign);
                b.White.Box(new Vector3(x + s * 0.012f, KitchenDadoY + 0.02f, (StairFoot + fromZ) * 0.5f), new Vector3(0.025f, 0.045f, fromZ - StairFoot));
                b.White.Box(new Vector3(x + s * 0.01f, 0.09f, (StairFoot + fromZ) * 0.5f), new Vector3(0.02f, 0.18f, fromZ - StairFoot));
            }
            var z0 = Mathf.Min(fromZ, StairFoot);
            var lo0 = KitchenPitch(z0) - 0.05f;
            var lo1 = KitchenPitch(toZ) - 0.05f;
            var hi = KitchenDadoY - KitchenRiseH + 0.05f;
            var a = new Vector3(x, lo0, z0);
            var c = new Vector3(x, lo1, toZ);
            MidQuad(b.Dado, a, c, c + Vector3.up * hi, a + Vector3.up * hi, n);
            // 長押と、段に沿った幅木（壁の桁）
            YardBeam(b.White, a + Vector3.up * (hi + 0.02f) + n * 0.012f, c + Vector3.up * (hi + 0.02f) + n * 0.012f, 0.035f);
            YardBeam(b.White, a + Vector3.up * 0.09f + n * 0.012f, c + Vector3.up * 0.09f + n * 0.012f, 0.03f);
            b.White.Box((a + c) * 0.5f + Vector3.up * 0.06f + n * 0.01f, new Vector3(0.02f, 0.2f, (c - a).magnitude),
                Quaternion.LookRotation(c - a, Vector3.up));
        }

        // ---- 玄関とポーチ ----------------------------------------------------------

        /// <summary>
        /// 玄関の戸と明かり取り。
        ///
        /// **戸は内へ開けて、北の脇の壁へ寄せてある。** 三つの記憶がどれもこの戸から出ていくので、閉めた戸は置かない。
        /// 戸の内側は白く塗り、四枚の鏡板と、真鍮の郵便受けの口の内側の蓋、錠。
        /// 上の明かり取りは外の白い光で光り、ロンドンの家の習いどおり番地を硝子に書いてある。
        /// 本物は外から読む向きで、内からは裏返しに見えるが、320×180 では裏返しの数字が読めない崩れた字に見えたので、内から読む向きに書く
        /// </summary>
        static void KitchenFrontDoor(KitchenBanks b)
        {
            const float x = KitchenWest;
            const float mid = (KitchenFront0 + KitchenFront1) * 0.5f;
            const float wide = KitchenFront1 - KitchenFront0;
            // 枠と、戸と明かり取りのあいだの横木
            for (var i = 0; i < 2; i++)
            {
                var z = i == 0 ? KitchenFront0 - 0.035f : KitchenFront1 + 0.035f;
                b.White.Box(new Vector3(x, KitchenFanTop * 0.5f, z), new Vector3(0.14f, KitchenFanTop, 0.07f));
                b.White.Box(new Vector3(x + 0.08f, (KitchenFanTop + 0.05f) * 0.5f, i == 0 ? z - 0.04f : z + 0.04f), new Vector3(0.02f, KitchenFanTop + 0.05f, 0.08f));
            }
            b.White.Box(new Vector3(x, HallDoorHigh + 0.04f, mid), new Vector3(0.14f, 0.08f, wide));
            b.White.Box(new Vector3(x, KitchenFanTop + 0.025f, mid), new Vector3(0.14f, 0.05f, wide + 0.14f));
            b.White.Box(new Vector3(x + 0.08f, KitchenFanTop + 0.05f, mid), new Vector3(0.02f, 0.1f, wide + 0.3f));
            // 明かり取り。光る硝子に細い縦の桟を二本と番地
            b.Glow.FaceX(x + 0.005f, KitchenFront0, KitchenFront1, HallDoorHigh + 0.08f, KitchenFanTop, 1);
            for (var i = 1; i <= 2; i++)
                b.White.Box(new Vector3(x + 0.01f, (HallDoorHigh + 0.08f + KitchenFanTop) * 0.5f, KitchenFront0 + wide * i / 3f),
                    new Vector3(0.02f, KitchenFanTop - HallDoorHigh - 0.08f, 0.025f));
            KitchenDigitsX(b.Black, x + 0.012f, mid, HallDoorHigh + 0.265f, 1, 27, 0.16f);

            // 戸。北の丁番から内へ開き、北の脇の壁に寄る
            const float leaf = 0.88f;
            const float lx = x + 0.045f;
            var lz = KitchenFront1 + 0.04f + leaf * 0.5f;
            b.White.Box(new Vector3(lx, HallDoorHigh * 0.5f - 0.005f, lz), new Vector3(0.05f, HallDoorHigh - 0.01f, leaf));
            // 鏡板。上に二枚、下に二枚。内の面に細い縁を回す
            for (var i = 0; i < 4; i++)
            {
                var pz = lz + (i % 2 == 0 ? -0.2f : 0.2f);
                var py = i < 2 ? 1.42f : 0.50f;
                var ph = i < 2 ? 0.78f : 0.62f;
                KitchenPanelX(b.Grout, lx + 0.026f, pz, py, 0.32f, ph, 1);
            }
            // 郵便受けの口の内側の蓋・錠・把手
            b.Brass.Box(new Vector3(lx + 0.03f, 0.98f, lz), new Vector3(0.012f, 0.08f, 0.30f));
            b.Brass.Box(new Vector3(lx + 0.04f, 1.30f, lz + leaf * 0.5f - 0.10f), new Vector3(0.04f, 0.09f, 0.07f));
            b.Brass.Box(new Vector3(lx + 0.05f, 1.02f, lz + leaf * 0.5f - 0.09f), new Vector3(0.05f, 0.04f, 0.04f));
            // 戸当たりの丁番
            for (var i = 0; i < 3; i++)
                b.Brass.Box(new Vector3(x + 0.03f, 0.25f + i * 0.75f, KitchenFront1 + 0.02f), new Vector3(0.03f, 0.10f, 0.02f));
        }

        /// <summary>
        /// 戸の外のポーチ。床は赤と黒の小さなタイル、両脇の壁は腰まで陶器のタイル、上は白い塗り。
        /// 奥の口は白く飛ぶ光で閉じる（<see cref="Kitchen"/> の KitchenOutside）。**段差は付けない**
        /// </summary>
        static void KitchenPorch(KitchenBanks b)
        {
            b.Quarry.FaceY(0f, PorchEdge, KitchenWest, PorchZ0, PorchZ1, 1);
            const float tile = 0.16f;
            var nx = Mathf.CeilToInt((KitchenWest - PorchEdge) / tile);
            var nz = Mathf.CeilToInt((PorchZ1 - PorchZ0) / tile);
            for (var i = 0; i < nx; i++)
                for (var k = 0; k < nz; k++)
                {
                    if (((i + k) & 1) == 1) continue;
                    var x0 = PorchEdge + i * tile;
                    var z0 = PorchZ0 + k * tile;
                    b.Black.FaceY(0.002f, x0, Mathf.Min(x0 + tile, KitchenWest), z0, Mathf.Min(z0 + tile, PorchZ1), 1);
                }
            // 両脇の壁。腰まで陶器のタイル、上は白
            b.Ceramic.FaceZ(PorchZ0, PorchEdge, KitchenWest, 0f, 1.05f, 1);
            b.Ceramic.FaceZ(PorchZ1, PorchEdge, KitchenWest, 0f, 1.05f, -1);
            b.White.FaceZ(PorchZ0, PorchEdge, KitchenWest, 1.05f, KitchenPorchHigh, 1);
            b.White.FaceZ(PorchZ1, PorchEdge, KitchenWest, 1.05f, KitchenPorchHigh, -1);
            b.Treads.Box(new Vector3((PorchEdge + KitchenWest) * 0.5f, 1.07f, PorchZ0 + 0.015f), new Vector3(KitchenWest - PorchEdge, 0.04f, 0.03f));
            b.Treads.Box(new Vector3((PorchEdge + KitchenWest) * 0.5f, 1.07f, PorchZ1 - 0.015f), new Vector3(KitchenWest - PorchEdge, 0.04f, 0.03f));
            b.Ceil.FaceY(KitchenPorchHigh, PorchEdge, KitchenWest, PorchZ0, PorchZ1, -1);
            // 戸の面の外側（ポーチから振り返ったときの）
            b.White.FaceXHoles(KitchenWest, PorchZ0, PorchZ1, 0f, KitchenPorchHigh, -1,
                new List<Vector4> { new Vector4(KitchenFront0, KitchenFront1, 0f, KitchenFanTop) });
        }

        // ---- 居間 ----------------------------------------------------------------

        /// <summary>
        /// 居間。廊下の南の戸口から覗く、通りに面した表の部屋。朝なのでカーテンは閉めたまま。
        ///
        /// **戸口の正面に暖炉を据える。** 記憶 6 のリンダは戸口から廊下の方を向いて始まり、
        /// その目の先に居間の戸口がある。黒い鋳物の炉と緑のタイル、白い炉棚と鏡、置き時計。
        /// ソファは通りの側の窓の下、肘掛け椅子は暖炉の脇。戸は内へ開けて東の壁の際に寄せる
        /// </summary>
        static void KitchenLounge(KitchenBanks b)
        {
            const float n = KitchenLoungeNorth;
            const float s = KitchenLoungeSouth;
            const float e = KitchenLoungeEast;
            const float w = KitchenWest;
            b.Lounge.FaceZHoles(n, w, e, 0f, HouseHigh, -1,
                new List<Vector4> { new Vector4(KitchenLoungeGap0, KitchenLoungeGap1, 0f, KitchenGapHigh) });
            b.Lounge.FaceZ(s, w, e, 0f, HouseHigh, 1);
            b.Lounge.FaceX(w, s, n, 0f, HouseHigh, 1);
            b.Lounge.FaceX(e, s, n, 0f, HouseHigh, -1);
            KitchenSkirtZ(b, s + 0.01f, w, e, 1);
            KitchenSkirtX(b, w + 0.01f, s, n, 1);
            KitchenSkirtX(b, e - 0.01f, s, n, -1);
            KitchenSkirtZ(b, n - 0.01f, w, KitchenLoungeGap0 - 0.08f, -1);
            KitchenSkirtZ(b, n - 0.01f, KitchenLoungeGap1 + 0.08f, e, -1);
            // 額縁の長押（ピクチャーレール）
            b.White.Box(new Vector3((w + e) * 0.5f, 2.2f, s + 0.012f), new Vector3(e - w, 0.035f, 0.025f));
            b.White.Box(new Vector3(w + 0.012f, 2.2f, (s + n) * 0.5f), new Vector3(0.025f, 0.035f, n - s));
            b.White.Box(new Vector3(e - 0.012f, 2.2f, (s + n) * 0.5f), new Vector3(0.025f, 0.035f, n - s));

            // 暖炉。煙突の張り出しに、黒い鋳物の炉・緑のタイル・白い炉棚・鏡
            const float cx = -0.30f;
            const float breast = s + 0.34f;
            b.Lounge.Box(new Vector3(cx, HouseHigh * 0.5f, (s + breast) * 0.5f), new Vector3(1.6f, HouseHigh, breast - s));
            b.Black.Box(new Vector3(cx, 0.50f, breast + 0.01f), new Vector3(0.86f, 1.0f, 0.02f));
            b.Black.Box(new Vector3(cx, 0.30f, breast + 0.06f), new Vector3(0.46f, 0.26f, 0.10f));
            for (var i = 0; i < 4; i++)
                b.Steel.Box(new Vector3(cx, 0.22f + i * 0.06f, breast + 0.11f), new Vector3(0.44f, 0.012f, 0.012f));
            for (var i = 0; i < 2; i++)
                b.Moss.Box(new Vector3(cx + (i == 0 ? -0.36f : 0.36f), 0.55f, breast + 0.022f), new Vector3(0.12f, 0.80f, 0.01f));
            for (var i = 0; i < 2; i++)
                b.White.Box(new Vector3(cx + (i == 0 ? -0.66f : 0.66f), 0.60f, breast + 0.05f), new Vector3(0.16f, 1.20f, 0.10f));
            b.White.Box(new Vector3(cx, 1.12f, breast + 0.05f), new Vector3(1.48f, 0.18f, 0.10f));
            b.White.Box(new Vector3(cx, 1.235f, breast + 0.09f), new Vector3(1.66f, 0.05f, 0.22f));
            b.Black.Box(new Vector3(cx, 0.02f, breast + 0.24f), new Vector3(1.30f, 0.04f, 0.46f));
            // 炉棚の上。置き時計・燭台・葉書、鏡
            b.Treads.Box(new Vector3(cx, 1.36f, breast + 0.08f), new Vector3(0.22f, 0.20f, 0.09f));
            b.White.Box(new Vector3(cx, 1.38f, breast + 0.126f), new Vector3(0.12f, 0.12f, 0.004f));
            for (var i = 0; i < 2; i++)
                b.Brass.Box(new Vector3(cx + (i == 0 ? -0.55f : 0.55f), 1.37f, breast + 0.08f), new Vector3(0.04f, 0.22f, 0.04f));
            b.Grout.Box(new Vector3(cx - 0.30f, 1.33f, breast + 0.06f), new Vector3(0.10f, 0.14f, 0.004f), Quaternion.Euler(-12f, 8f, 0f));
            b.Grout.Box(new Vector3(cx + 0.28f, 1.32f, breast + 0.06f), new Vector3(0.12f, 0.12f, 0.004f), Quaternion.Euler(-12f, -6f, 0f));
            b.Treads.Box(new Vector3(cx, 1.92f, breast + 0.015f), new Vector3(0.86f, 0.70f, 0.03f));
            b.Glass.Box(new Vector3(cx, 1.92f, breast + 0.032f), new Vector3(0.74f, 0.58f, 0.004f));

            // 東の脇の窪みの本棚
            for (var i = 0; i < 4; i++)
                b.Oak.Box(new Vector3(0.87f, 0.45f + i * 0.45f, s + 0.16f), new Vector3(0.74f, 0.025f, 0.30f));
            var spines = new[] { b.Blue, b.Runner, b.Moss, b.Yellow, b.Cloth };
            for (var i = 0; i < 3; i++)
                for (var k = 0; k < 7; k++)
                {
                    var high = 0.22f + ((k * 7 + i * 3) % 5) * 0.025f;
                    spines[(k + i * 2) % spines.Length].Box(new Vector3(0.58f + k * 0.075f, 0.465f + i * 0.45f + high * 0.5f, s + 0.15f),
                        new Vector3(0.06f, high, 0.20f));
                }

            // 通りの側の窓。カーテンを引いたままで、真ん中と上の隙間から白い光
            const float wz0 = -4.55f;
            const float wz1 = -3.25f;
            const float wm = (wz0 + wz1) * 0.5f;
            b.Runner.Box(new Vector3(w + 0.08f, 1.20f, (wz0 - 0.06f + wm - 0.03f) * 0.5f), new Vector3(0.06f, 2.10f, wm - 0.03f - wz0 + 0.06f));
            b.Runner.Box(new Vector3(w + 0.08f, 1.20f, (wm + 0.03f + wz1 + 0.06f) * 0.5f), new Vector3(0.06f, 2.10f, wz1 + 0.06f - wm - 0.03f));
            b.Glow.FaceX(w + 0.03f, wm - 0.04f, wm + 0.04f, 0.9f, 2.25f, 1);
            b.Glow.FaceX(w + 0.03f, wz0, wz1, 2.25f, 2.31f, 1);
            b.Brass.Box(new Vector3(w + 0.1f, 2.34f, wm), new Vector3(0.03f, 0.03f, wz1 - wz0 + 0.5f));
            // ソファ。窓の下、暖炉の方を向く。南の窪み（テレビ）に掛からない長さ
            const float sx = w + 0.40f;
            const float sz = -3.75f;
            b.Moss.Box(new Vector3(sx + 0.05f, 0.22f, sz), new Vector3(0.70f, 0.44f, 1.40f));
            b.Moss.Box(new Vector3(sx - 0.22f, 0.62f, sz), new Vector3(0.18f, 0.44f, 1.40f));
            for (var i = 0; i < 2; i++)
                b.Moss.Box(new Vector3(sx + 0.05f, 0.52f, sz + (i == 0 ? -0.65f : 0.65f)), new Vector3(0.74f, 0.20f, 0.14f));
            b.Yellow.Box(new Vector3(sx - 0.05f, 0.58f, sz - 0.45f), new Vector3(0.14f, 0.32f, 0.34f), Quaternion.Euler(0f, 0f, -12f));
            // 肘掛け椅子。暖炉の東の脇、北西を向く
            var chair = new Vector3(0.62f, 0f, -3.75f);
            var rot = Quaternion.Euler(0f, 225f, 0f);
            b.Blue.Box(chair + rot * new Vector3(0f, 0.24f, 0.04f), new Vector3(0.66f, 0.48f, 0.66f), rot);
            b.Blue.Box(chair + rot * new Vector3(0f, 0.62f, -0.30f), new Vector3(0.70f, 0.80f, 0.16f), rot);
            for (var i = 0; i < 2; i++)
                b.Blue.Box(chair + rot * new Vector3(i == 0 ? -0.30f : 0.30f, 0.34f, 0.02f), new Vector3(0.12f, 0.68f, 0.70f), rot);
            // 低い卓と敷物、新聞
            b.Oak.Box(new Vector3(-1.05f, 0.40f, -3.85f), new Vector3(0.80f, 0.04f, 0.50f));
            for (var i = 0; i < 4; i++)
                b.Oak.Box(new Vector3(-1.05f + ((i & 1) == 0 ? -0.36f : 0.36f), 0.19f, -3.85f + ((i & 2) == 0 ? -0.21f : 0.21f)),
                    new Vector3(0.04f, 0.38f, 0.04f));
            b.Grout.Box(new Vector3(-1.12f, 0.425f, -3.82f), new Vector3(0.32f, 0.01f, 0.26f), Quaternion.Euler(0f, 12f, 0f));
            b.Runner.FaceY(0.006f, -2.10f, 0.20f, -4.50f, -3.15f, 1);
            // 背の高い灯り（点けていない）とテレビ
            b.Brass.Box(new Vector3(w + 0.20f, 0.75f, n - 0.12f), new Vector3(0.03f, 1.5f, 0.03f));
            b.Grout.Box(new Vector3(w + 0.20f, 1.55f, n - 0.12f), new Vector3(0.24f, 0.22f, 0.24f));
            b.Treads.Box(new Vector3(-1.75f, 0.25f, s + 0.24f), new Vector3(0.90f, 0.50f, 0.42f));
            b.Black.Box(new Vector3(-1.75f, 0.84f, s + 0.20f), new Vector3(0.86f, 0.52f, 0.05f));

            // 戸。内へ開けて東の脇（戸口の東の縁の丁番）から南へ寄せる
            b.White.Box(new Vector3(KitchenLoungeGap1 - 0.03f, 1.0f, n - 0.42f), new Vector3(0.04f, 2.0f, 0.80f));
            b.Brass.Box(new Vector3(KitchenLoungeGap1 - 0.06f, 1.0f, n - 0.74f), new Vector3(0.03f, 0.05f, 0.05f));
            // 天井の飾りと、吊り灯り（点けていない）
            b.White.Box(new Vector3(-0.6f, HouseHigh - 0.01f, -3.9f), new Vector3(0.40f, 0.02f, 0.40f), Quaternion.Euler(0f, 45f, 0f));
            b.Black.Box(new Vector3(-0.6f, HouseHigh - 0.25f, -3.9f), new Vector3(0.01f, 0.46f, 0.01f));
            b.Grout.Box(new Vector3(-0.6f, HouseHigh - 0.55f, -3.9f), new Vector3(0.34f, 0.16f, 0.34f));
        }

        // ---- 台所 ----------------------------------------------------------------

        /// <summary>
        /// 流し台の並び。北の壁に沿って、西から戸棚・陶器の流し・引き出し・天火・戸棚。
        /// その上に吊り戸棚（西半分）と、コンロのフードと開いた棚（東半分）。壁は天板から吊り戸棚の下までタイル。
        ///
        /// **扉の継ぎ目が等間隔の繰り返しになる。** 台所を台所として読ませるのは、
        /// 蛇口の形よりもこの縦線の並びのほうが早い
        /// </summary>
        static void KitchenCounter(KitchenBanks b)
        {
            const float z0 = CounterFace;
            const float z1 = KitchenBack;
            // 胴。流しの下は低く止め、陶器の流しを載せる
            KitchenBody(b.Cupboard, CounterX0, KitchenBowl0 - 0.03f, 0.1f, CounterHigh, z0, z1);
            KitchenBody(b.Cupboard, KitchenBowl0 - 0.03f, KitchenBowl1 + 0.03f, 0.1f, 0.62f, z0, z1);
            KitchenBody(b.Cupboard, KitchenBowl1 + 0.03f, CounterX1, 0.1f, CounterHigh, z0, z1);
            // 蹴込み
            b.Black.Box(new Vector3((CounterX0 + CounterX1) * 0.5f, 0.05f, z0 + 0.04f), new Vector3(CounterX1 - CounterX0, 0.1f, 0.02f));

            // 扉と引き出し。区切りは西から: 戸棚二枚・流しの下二枚・引き出し三段・天火・戸棚二枚
            KitchenLeaf(b, CounterX0, -1.93f, 0.12f, 0.86f, true);
            KitchenLeaf(b, -1.93f, KitchenBowl0 - 0.03f, 0.12f, 0.86f, false);
            KitchenLeaf(b, KitchenBowl0 - 0.03f, Sink.x, 0.12f, 0.60f, true);
            KitchenLeaf(b, Sink.x, KitchenBowl1 + 0.03f, 0.12f, 0.60f, false);
            for (var i = 0; i < 3; i++)
                KitchenDrawer(b, KitchenBowl1 + 0.03f, -0.16f, 0.12f + i * 0.25f, 0.12f + (i + 1) * 0.25f - 0.01f);
            // 天火。黒い硝子の戸に銀の把手、上に目盛りの帯
            b.Black.Box(new Vector3(0.145f, 0.44f, z0 - 0.015f), new Vector3(0.58f, 0.56f, 0.03f));
            b.Steel.Box(new Vector3(0.145f, 0.70f, z0 - 0.045f), new Vector3(0.44f, 0.02f, 0.02f));
            b.Steel.Box(new Vector3(0.145f, 0.81f, z0 - 0.01f), new Vector3(0.58f, 0.12f, 0.02f));
            for (var i = 0; i < 4; i++)
                b.Black.Box(new Vector3(-0.05f + i * 0.13f, 0.81f, z0 - 0.025f), new Vector3(0.035f, 0.035f, 0.012f));
            KitchenLeaf(b, 0.45f, 1.00f, 0.12f, 0.86f, true);
            KitchenLeaf(b, 1.00f, CounterX1, 0.12f, 0.86f, false);

            // 吊り戸棚。扉は三枚、取っ手は下の縁へ。リンダが開けるのは流しの上の二枚（記憶 6 の 11 秒）
            const float hangY0 = 1.50f;
            const float hangY1 = 2.25f;
            const float hz0 = 2.86f;
            b.Cupboard.Box(new Vector3((CounterX0 + KitchenHangX1) * 0.5f, (hangY0 + hangY1) * 0.5f, (hz0 + z1) * 0.5f),
                new Vector3(KitchenHangX1 - CounterX0, hangY1 - hangY0, z1 - hz0));
            const int leaves = 3;
            var lw = (KitchenHangX1 - CounterX0) / leaves;
            for (var i = 0; i < leaves; i++)
            {
                var a = CounterX0 + i * lw;
                b.Cupboard.Box(new Vector3(a + lw * 0.5f, (hangY0 + hangY1) * 0.5f, hz0 - 0.012f), new Vector3(lw - 0.012f, hangY1 - hangY0 - 0.02f, 0.024f));
                b.Grout.FaceZ(hz0 - 0.0245f, a + 0.05f, a + lw - 0.05f, hangY0 + 0.06f, hangY0 + 0.075f, -1);
                b.Steel.Box(new Vector3(a + lw * 0.5f, hangY0 + 0.035f, hz0 - 0.035f), new Vector3(lw * 0.45f, 0.02f, 0.02f));
            }
            b.White.Box(new Vector3((CounterX0 + KitchenHangX1) * 0.5f, hangY1 + 0.025f, (hz0 + z1) * 0.5f - 0.02f),
                new Vector3(KitchenHangX1 - CounterX0 + 0.04f, 0.05f, z1 - hz0 + 0.04f));
            // フード。天火の上
            b.Steel.Box(new Vector3(0.145f, 1.62f, 2.97f), new Vector3(0.62f, 0.10f, 0.46f));
            b.Steel.Box(new Vector3(0.145f, 1.95f, 3.08f), new Vector3(0.26f, 0.56f, 0.24f));
            // 開いた棚。瓶と缶
            b.Oak.Box(new Vector3((0.50f + CounterX1) * 0.5f, 1.66f, 3.08f), new Vector3(CounterX1 - 0.50f, 0.03f, 0.24f));
            var jars = new[] { b.Glass, b.Yellow, b.Runner, b.White, b.Blue, b.Glass };
            for (var i = 0; i < jars.Length; i++)
            {
                var h = 0.14f + (i % 3) * 0.04f;
                jars[i].Box(new Vector3(0.62f + i * 0.155f, 1.675f + h * 0.5f, 3.08f), new Vector3(0.10f, h, 0.10f));
            }

            // 壁のタイル。天板から吊り戸棚の下まで、フードと棚の所は棚の下まで
            const float tz = z1 - 0.005f;
            b.Ceramic.FaceZ(tz, CounterX0, KitchenHangX1, KitchenTop, hangY0, -1);
            b.Ceramic.FaceZ(tz, KitchenHangX1, CounterX1, KitchenTop, 1.645f, -1);
            // 目地。横は 15 cm ごと、縦は 30 cm ごとに段ごとに半分ずらす
            for (var row = 0; row < 5; row++)
            {
                var y = KitchenTop + (row + 1) * 0.15f;
                var top = KitchenTop + row * 0.15f;
                var roof = row < 3 ? hangY0 : 1.645f;
                if (top >= roof) break;
                if (y < roof) b.Grout.FaceZ(tz - 0.002f, CounterX0, CounterX1, y - 0.006f, y, -1);
                var shift = (row & 1) == 0 ? 0f : 0.15f;
                for (var x = CounterX0 + 0.3f - shift; x < CounterX1; x += 0.3f)
                {
                    if (x <= CounterX0 + 0.01f) continue;
                    var cap = x < KitchenHangX1 ? hangY0 : 1.645f;
                    b.Grout.FaceZ(tz - 0.002f, x - 0.003f, x + 0.003f, top, Mathf.Min(top + 0.15f, cap), -1);
                }
            }
        }

        /// <summary>戸棚の胴。x0〜x1、y0〜y1、z0（手前）〜z1</summary>
        static void KitchenBody(Bank bank, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            bank.Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f + 0.02f), new Vector3(x1 - x0, y1 - y0, z1 - z0 - 0.04f));
        }

        /// <summary>
        /// 下の扉一枚。框の縁に細い影の線を回して鏡板に見せる。
        /// <paramref name="handleEast"/> なら取っ手は東の縁、偽なら西の縁
        /// </summary>
        static void KitchenLeaf(KitchenBanks b, float x0, float x1, float y0, float y1, bool handleEast)
        {
            const float z = CounterFace;
            b.Cupboard.Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z - 0.01f), new Vector3(x1 - x0 - 0.01f, y1 - y0, 0.02f));
            b.Grout.FaceZ(z - 0.0205f, x0 + 0.07f, x1 - 0.07f, y1 - 0.08f, y1 - 0.07f, -1);
            b.Grout.FaceZ(z - 0.0205f, x0 + 0.07f, x1 - 0.07f, y0 + 0.07f, y0 + 0.08f, -1);
            var hx = handleEast ? x1 - 0.06f : x0 + 0.06f;
            b.Steel.Box(new Vector3(hx, y1 - 0.14f, z - 0.04f), new Vector3(0.02f, 0.12f, 0.02f));
        }

        /// <summary>引き出し一段。横長の取っ手を真ん中に</summary>
        static void KitchenDrawer(KitchenBanks b, float x0, float x1, float y0, float y1)
        {
            const float z = CounterFace;
            b.Cupboard.Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z - 0.01f), new Vector3(x1 - x0 - 0.01f, y1 - y0, 0.02f));
            b.Steel.Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f + 0.04f, z - 0.035f), new Vector3(0.16f, 0.02f, 0.02f));
        }

        /// <summary>
        /// 陶器の流し（ベルファスト・シンク）と、橋渡しの混合栓。
        ///
        /// **白い陶器の鉢の前の面を戸棚より前へ出す。** イギリスの古い家の台所でいちばん見分けやすい形。
        /// 鉢の中に青い洗い桶を置き、皿を立てかける。記憶 5 のマークは鍵打ちの頭でこの中を見下ろしている
        /// </summary>
        static void KitchenBelfast(KitchenBanks b)
        {
            const float x0 = KitchenBowl0;
            const float x1 = KitchenBowl1;
            const float z0 = CounterFace - 0.03f;
            const float z1 = 3.12f;
            const float rim = KitchenTop - 0.01f;
            const float lip = 0.05f;
            const float floor = 0.68f;
            // 前の面（エプロン）と外の脇
            b.Ceramic.FaceZ(z0, x0, x1, 0.62f, rim, -1);
            b.Ceramic.FaceX(x0, z0, z1, 0.62f, rim, -1);
            b.Ceramic.FaceX(x1, z0, z1, 0.62f, rim, 1);
            b.Ceramic.FaceY(0.62f, x0, x1, z0, z1, -1);
            // 縁
            b.Ceramic.FaceY(rim, x0, x1, z0, z0 + lip, 1);
            b.Ceramic.FaceY(rim, x0, x1, z1 - lip, z1, 1);
            b.Ceramic.FaceY(rim, x0, x0 + lip, z0 + lip, z1 - lip, 1);
            b.Ceramic.FaceY(rim, x1 - lip, x1, z0 + lip, z1 - lip, 1);
            // 鉢の内側
            b.Ceramic.FaceZ(z0 + lip, x0 + lip, x1 - lip, floor, rim, 1);
            b.Ceramic.FaceZ(z1 - lip, x0 + lip, x1 - lip, floor, rim, -1);
            b.Ceramic.FaceX(x0 + lip, z0 + lip, z1 - lip, floor, rim, 1);
            b.Ceramic.FaceX(x1 - lip, z0 + lip, z1 - lip, floor, rim, -1);
            b.Ceramic.FaceY(floor, x0 + lip, x1 - lip, z0 + lip, z1 - lip, 1);
            // 洗い桶と、湯の面、立てかけた皿と茶碗、刷毛
            b.Blue.Box(new Vector3(Sink.x, 0.77f, 2.89f), new Vector3(0.44f, 0.18f, 0.34f));
            b.Glass.FaceY(0.861f, Sink.x - 0.20f, Sink.x + 0.20f, 2.74f, 3.04f, 1);
            for (var i = 0; i < 3; i++)
                KitchenPlate(b.White, new Vector3(Sink.x - 0.12f + i * 0.08f, 0.89f, 2.93f), Vector3.right, 0.11f, 16f - i * 4f);
            b.Yellow.Box(new Vector3(Sink.x + 0.13f, 0.89f, 2.82f), new Vector3(0.08f, 0.10f, 0.08f));
            b.Yellow.Box(new Vector3(Sink.x - 0.20f, 0.90f, 2.80f), new Vector3(0.03f, 0.03f, 0.20f), Quaternion.Euler(-30f, 20f, 0f));
            // 混合栓。後ろの縁に柱を二本、橋渡しの横木、白鳥の首の吐水口
            const float tz = 3.12f;
            for (var i = 0; i < 2; i++)
            {
                var x = Sink.x + (i == 0 ? -0.10f : 0.10f);
                b.Steel.Box(new Vector3(x, KitchenTop + 0.07f, tz), new Vector3(0.035f, 0.14f, 0.035f));
                b.Steel.Box(new Vector3(x, KitchenTop + 0.16f, tz), new Vector3(0.09f, 0.02f, 0.02f));
                b.Steel.Box(new Vector3(x, KitchenTop + 0.16f, tz), new Vector3(0.02f, 0.02f, 0.09f));
            }
            b.Steel.Box(new Vector3(Sink.x, KitchenTop + 0.10f, tz), new Vector3(0.22f, 0.03f, 0.03f));
            b.Steel.Box(new Vector3(Sink.x, KitchenTop + 0.19f, tz), new Vector3(0.03f, 0.18f, 0.03f));
            b.Steel.Box(new Vector3(Sink.x, KitchenTop + 0.28f, tz - 0.09f), new Vector3(0.03f, 0.03f, 0.20f), Quaternion.Euler(12f, 0f, 0f));
            b.Steel.Box(new Vector3(Sink.x, KitchenTop + 0.24f, tz - 0.19f), new Vector3(0.028f, 0.08f, 0.028f));
        }

        /// <summary>
        /// 天板と、その上の物。水切り・洗剤・コンロの火口・やかん・トースター・パン入れ・刃物立て・ラジオ
        /// </summary>
        static void KitchenWorktop(KitchenBanks b)
        {
            const float z0 = CounterFace - 0.02f;
            const float z1 = KitchenBack;
            const float y = CounterHigh + 0.02f;
            const float t = 0.04f;
            // 天板。流しの所を抜いて四枚
            b.Oak.Box(new Vector3((CounterX0 + KitchenBowl0) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(KitchenBowl0 - CounterX0, t, z1 - z0));
            b.Oak.Box(new Vector3((KitchenBowl1 + CounterX1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3(CounterX1 - KitchenBowl1, t, z1 - z0));
            b.Oak.Box(new Vector3(Sink.x, y, 3.16f), new Vector3(KitchenBowl1 - KitchenBowl0, t, 0.08f));
            // 水切りの籠。流しの東の天板に、銀の針金の籠と立てた皿
            const float dx0 = -0.70f;
            const float dx1 = -0.22f;
            b.Steel.Box(new Vector3((dx0 + dx1) * 0.5f, KitchenTop + 0.01f, 2.93f), new Vector3(dx1 - dx0, 0.02f, 0.36f));
            for (var i = 0; i < 7; i++)
                b.Steel.Box(new Vector3(dx0 + 0.03f + i * 0.07f, KitchenTop + 0.07f, 2.80f), new Vector3(0.008f, 0.12f, 0.008f));
            for (var i = 0; i < 7; i++)
                b.Steel.Box(new Vector3(dx0 + 0.03f + i * 0.07f, KitchenTop + 0.07f, 3.06f), new Vector3(0.008f, 0.12f, 0.008f));
            b.Steel.Box(new Vector3((dx0 + dx1) * 0.5f, KitchenTop + 0.13f, 2.80f), new Vector3(dx1 - dx0, 0.008f, 0.008f));
            b.Steel.Box(new Vector3((dx0 + dx1) * 0.5f, KitchenTop + 0.13f, 3.06f), new Vector3(dx1 - dx0, 0.008f, 0.008f));
            for (var i = 0; i < 4; i++)
                KitchenPlate(b.White, new Vector3(dx0 + 0.08f + i * 0.07f, KitchenTop + 0.12f, 2.93f), Vector3.right, 0.11f, 8f);
            b.Blue.Box(new Vector3(dx1 - 0.08f, KitchenTop + 0.065f, 2.92f), new Vector3(0.08f, 0.09f, 0.08f));
            // 洗剤の瓶
            b.Moss.Box(new Vector3(dx0 + 0.02f, KitchenTop + 0.10f, 3.10f), new Vector3(0.06f, 0.20f, 0.05f));
            // コンロの火口。天火の上に四つ
            b.Black.Box(new Vector3(0.145f, KitchenTop + 0.004f, 2.92f), new Vector3(0.56f, 0.008f, 0.48f));
            for (var i = 0; i < 4; i++)
                b.Steel.Box(new Vector3(0.145f + ((i & 1) == 0 ? -0.14f : 0.14f), KitchenTop + 0.012f, 2.92f + ((i & 2) == 0 ? -0.12f : 0.12f)),
                    new Vector3(0.14f, 0.01f, 0.14f), Quaternion.Euler(0f, 45f, 0f));
            // やかん。赤い胴に黒い台、上の持ち手と注ぎ口
            var kettle = new Vector3(0.66f, KitchenTop, 2.95f);
            b.Black.Box(kettle + new Vector3(0f, 0.01f, 0f), new Vector3(0.18f, 0.02f, 0.18f));
            b.Runner.Box(kettle + new Vector3(0f, 0.13f, 0f), new Vector3(0.17f, 0.22f, 0.15f));
            b.Runner.Box(kettle + new Vector3(0f, 0.13f, 0f), new Vector3(0.15f, 0.22f, 0.17f), Quaternion.Euler(0f, 45f, 0f));
            b.Black.Box(kettle + new Vector3(0.03f, 0.26f, 0f), new Vector3(0.14f, 0.03f, 0.04f));
            b.Black.Box(kettle + new Vector3(0.09f, 0.18f, 0f), new Vector3(0.03f, 0.14f, 0.04f));
            b.Runner.Box(kettle + new Vector3(-0.10f, 0.20f, 0f), new Vector3(0.07f, 0.04f, 0.04f), Quaternion.Euler(0f, 0f, 25f));
            // トースター。銀の箱に黒い口が二つ
            var toaster = new Vector3(1.05f, KitchenTop, 2.95f);
            b.Steel.Box(toaster + new Vector3(0f, 0.095f, 0f), new Vector3(0.28f, 0.19f, 0.17f));
            for (var i = 0; i < 2; i++)
                b.Black.Box(toaster + new Vector3(0f, 0.192f, i == 0 ? -0.035f : 0.035f), new Vector3(0.22f, 0.004f, 0.025f));
            b.Black.Box(toaster + new Vector3(0.145f, 0.12f, 0f), new Vector3(0.01f, 0.03f, 0.03f));
            // パン入れ
            b.Cupboard.Box(new Vector3(1.38f, KitchenTop + 0.10f, 2.98f), new Vector3(0.28f, 0.20f, 0.26f));
            b.Steel.Box(new Vector3(1.38f, KitchenTop + 0.16f, 2.845f), new Vector3(0.10f, 0.015f, 0.015f));
            // 刃物立てとラジオ。西の端
            b.Treads.Box(new Vector3(-2.25f, KitchenTop + 0.11f, 3.05f), new Vector3(0.12f, 0.22f, 0.14f), Quaternion.Euler(-15f, 0f, 0f));
            b.Black.Box(new Vector3(-2.25f, KitchenTop + 0.24f, 3.08f), new Vector3(0.08f, 0.08f, 0.06f));
            b.Runner.Box(new Vector3(-1.85f, KitchenTop + 0.08f, 3.06f), new Vector3(0.30f, 0.16f, 0.10f));
            b.Grout.Box(new Vector3(-1.90f, KitchenTop + 0.08f, 3.008f), new Vector3(0.14f, 0.10f, 0.004f));
            b.Steel.Box(new Vector3(-1.75f, KitchenTop + 0.25f, 3.06f), new Vector3(0.008f, 0.20f, 0.008f), Quaternion.Euler(0f, 0f, 20f));
            // 茶と砂糖の缶
            b.Yellow.Box(new Vector3(-1.60f, KitchenTop + 0.08f, 3.08f), new Vector3(0.10f, 0.16f, 0.10f));
            b.Blue.Box(new Vector3(-1.48f, KitchenTop + 0.07f, 3.08f), new Vector3(0.10f, 0.14f, 0.10f));
        }

        /// <summary>
        /// 冷蔵庫。背の高い白い箱に扉を二枚、左の縁に取っ手。上の扉に磁石で留めたメモ・献立の紙・写真・学校の手紙
        /// </summary>
        static void KitchenFridge(KitchenBanks b)
        {
            const float x0 = KitchenFridge0;
            const float x1 = KitchenFridge1;
            const float z0 = KitchenFridgeFace;
            const float mid = (x0 + x1) * 0.5f;
            b.White.Box(new Vector3(mid, 0.9f, (z0 + KitchenBack) * 0.5f), new Vector3(x1 - x0, 1.8f, KitchenBack - z0));
            b.Grout.FaceZ(z0 - 0.002f, x0 + 0.01f, x1 - 0.01f, 1.10f, 1.115f, -1);
            b.Grout.FaceZ(z0 - 0.002f, x0 + 0.01f, x1 - 0.01f, 0.06f, 0.07f, -1);
            b.Steel.Box(new Vector3(x0 + 0.06f, 1.35f, z0 - 0.03f), new Vector3(0.025f, 0.34f, 0.025f));
            b.Steel.Box(new Vector3(x0 + 0.06f, 0.88f, z0 - 0.03f), new Vector3(0.025f, 0.28f, 0.025f));
            // メモと磁石
            const float f = z0 - 0.004f;
            KitchenNote(b.Grout, b.Runner, new Vector3(mid - 0.14f, 1.52f, f), 0.18f, 0.24f, 4f);
            KitchenNote(b.Grout, b.Blue, new Vector3(mid + 0.16f, 1.46f, f), 0.21f, 0.30f, -3f);
            KitchenNote(b.Yellow, b.Runner, new Vector3(mid + 0.10f, 1.22f, f), 0.08f, 0.08f, 9f);
            KitchenNote(b.Yellow, b.Blue, new Vector3(mid - 0.20f, 1.26f, f), 0.08f, 0.08f, -6f);
            KitchenNote(b.Grout, b.Yellow, new Vector3(mid - 0.02f, 0.84f, f), 0.20f, 0.14f, 2f);
            // 写真
            b.White.Box(new Vector3(mid + 0.02f, 1.68f, f - 0.002f), new Vector3(0.12f, 0.09f, 0.004f), Quaternion.Euler(0f, 0f, -5f));
            b.Moss.Box(new Vector3(mid + 0.02f, 1.68f, f - 0.005f), new Vector3(0.10f, 0.07f, 0.002f), Quaternion.Euler(0f, 0f, -5f));
        }

        /// <summary>冷蔵庫の扉に留めた紙一枚と、上の縁の磁石</summary>
        static void KitchenNote(Bank sheet, Bank magnet, Vector3 at, float w, float h, float tilt)
        {
            var rot = Quaternion.Euler(0f, 0f, tilt);
            sheet.Box(at, new Vector3(w, h, 0.003f), rot);
            magnet.Box(at + rot * new Vector3(0f, h * 0.5f - 0.02f, -0.008f), new Vector3(0.035f, 0.035f, 0.014f), rot);
        }

        /// <summary>
        /// 卓と椅子二脚、朝食の途中。皿とトーストの耳・二つの茶碗・ティーポット・牛乳・マーマレード・シリアルの箱・畳んだ新聞。
        /// 壁に時計（6 時 55 分）
        /// </summary>
        static void KitchenTable(KitchenBanks b)
        {
            var t = KitchenTablePos;
            b.Oak.Box(t + new Vector3(0f, 0.74f, 0f), new Vector3(1.0f, 0.04f, 0.72f));
            b.Oak.Box(t + new Vector3(0f, 0.69f, 0f), new Vector3(0.92f, 0.06f, 0.64f));
            for (var i = 0; i < 4; i++)
                b.Oak.Box(t + new Vector3((i & 1) == 0 ? -0.44f : 0.44f, 0.36f, (i & 2) == 0 ? -0.29f : 0.29f), new Vector3(0.06f, 0.72f, 0.06f));
            KitchenChair(b, t + new Vector3(0f, 0f, -0.68f), 0f);
            KitchenChair(b, t + new Vector3(0.05f, 0f, 0.70f), 172f);
            // 朝食。南の席は済んで、北の席は途中
            const float top = 0.76f;
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var plate = t + new Vector3(0.05f * side, top, 0.18f * side);
                KitchenDiscY(b.White, plate + new Vector3(0f, 0.012f, 0f), 0.13f, 16);
                b.White.Box(plate + new Vector3(0f, 0.005f, 0f), new Vector3(0.14f, 0.01f, 0.14f), Quaternion.Euler(0f, 22f, 0f));
                b.Oak.Box(plate + new Vector3(0.03f, 0.02f, 0.02f), new Vector3(0.09f, 0.015f, 0.03f), Quaternion.Euler(0f, 30f * side, 0f));
                b.Steel.Box(plate + new Vector3(0.15f, 0.004f, 0f), new Vector3(0.02f, 0.006f, 0.18f));
            }
            b.Blue.Box(t + new Vector3(0.30f, top + 0.05f, -0.16f), new Vector3(0.08f, 0.10f, 0.08f));
            b.Yellow.Box(t + new Vector3(-0.28f, top + 0.05f, 0.22f), new Vector3(0.08f, 0.10f, 0.08f));
            b.Moss.Box(t + new Vector3(-0.30f, top + 0.08f, -0.08f), new Vector3(0.16f, 0.16f, 0.16f), Quaternion.Euler(0f, 30f, 0f));
            b.Moss.Box(t + new Vector3(-0.21f, top + 0.12f, -0.08f), new Vector3(0.07f, 0.03f, 0.03f), Quaternion.Euler(0f, 30f, 25f));
            b.White.Box(t + new Vector3(0.18f, top + 0.07f, 0.02f), new Vector3(0.07f, 0.14f, 0.07f));
            b.Yellow.Box(t + new Vector3(0.02f, top + 0.045f, -0.02f), new Vector3(0.07f, 0.09f, 0.07f));
            b.Runner.Box(t + new Vector3(-0.38f, top + 0.15f, 0.06f), new Vector3(0.07f, 0.30f, 0.20f), Quaternion.Euler(0f, 12f, 0f));
            b.Grout.Box(t + new Vector3(0.32f, top + 0.012f, 0.16f), new Vector3(0.30f, 0.02f, 0.22f), Quaternion.Euler(0f, -14f, 0f));

            // 吊った灯り。天井から紐と、白い笠と、笠の中の光る口
            b.Black.Box(new Vector3(-0.3f, HouseHigh - 0.2f, 1.35f), new Vector3(0.012f, 0.40f, 0.012f));
            b.White.Box(new Vector3(-0.3f, 2.17f, 1.35f), new Vector3(0.16f, 0.07f, 0.16f));
            b.White.Box(new Vector3(-0.3f, 2.10f, 1.35f), new Vector3(0.36f, 0.08f, 0.36f), Quaternion.Euler(0f, 45f, 0f));
            b.White.Box(new Vector3(-0.3f, 2.10f, 1.35f), new Vector3(0.36f, 0.08f, 0.36f));
            b.Glow.FaceY(2.055f, -0.42f, -0.18f, 1.23f, 1.47f, -1);

            // 南の壁（戸口の西）の皿の棚。背板に三段の棚、皿を立てて並べる。卓の南の椅子の上
            const float rz = KitchenDoorZ;
            b.Oak.Box(new Vector3(-1.55f, 1.72f, rz + 0.01f), new Vector3(1.10f, 0.72f, 0.02f));
            for (var i = 0; i < 3; i++)
                b.Oak.Box(new Vector3(-1.55f, 1.40f + i * 0.28f, rz + 0.10f), new Vector3(1.10f, 0.025f, 0.18f));
            b.Oak.Box(new Vector3(-2.09f, 1.72f, rz + 0.10f), new Vector3(0.025f, 0.72f, 0.18f));
            b.Oak.Box(new Vector3(-1.01f, 1.72f, rz + 0.10f), new Vector3(0.025f, 0.72f, 0.18f));
            for (var i = 0; i < 5; i++)
            {
                KitchenPlate(i % 2 == 0 ? b.White : b.Blue, new Vector3(-1.93f + i * 0.19f, 1.53f, rz + 0.06f), Vector3.forward, 0.10f, 10f);
                KitchenPlate(b.White, new Vector3(-1.93f + i * 0.19f, 1.81f, rz + 0.06f), Vector3.forward, 0.09f, 10f);
            }
            for (var i = 0; i < 4; i++)
                (i % 2 == 0 ? b.Yellow : b.Moss).Box(new Vector3(-1.85f + i * 0.2f, 1.47f, rz + 0.12f), new Vector3(0.08f, 0.09f, 0.08f));

            // 西の壁の時計。白い文字盤に黒い縁と針。6 時 55 分
            var face = new Vector3(KitchenWest + 0.035f, 1.95f, t.z);
            KitchenDiscX(b.Black, face + new Vector3(0.024f, 0f, 0f), 0.16f, 20);
            KitchenDiscX(b.White, face + new Vector3(0.027f, 0f, 0f), 0.135f, 20);
            b.Black.Box(face, new Vector3(0.05f, 0.22f, 0.22f), Quaternion.Euler(45f, 0f, 0f));
            KitchenHand(b.Black, face + new Vector3(0.032f, 0f, 0f), 207.5f, 0.07f, 0.014f);
            KitchenHand(b.Black, face + new Vector3(0.035f, 0f, 0f), 330f, 0.105f, 0.010f);
            // 暦。時計の北
            b.Grout.Box(new Vector3(KitchenWest + 0.012f, 1.45f, t.z + 0.62f), new Vector3(0.004f, 0.42f, 0.30f));
            b.Runner.Box(new Vector3(KitchenWest + 0.016f, 1.60f, t.z + 0.62f), new Vector3(0.004f, 0.12f, 0.30f));
        }

        /// <summary>
        /// 時計の針。x が一定の壁の上で、12 時から時計回りに <paramref name="degrees"/> 度。
        /// 部屋の側（+x）から見て 3 時が北（+z）
        /// </summary>
        static void KitchenHand(Bank bank, Vector3 centre, float degrees, float length, float thick)
        {
            var r = degrees * Mathf.Deg2Rad;
            var dir = new Vector3(0f, Mathf.Cos(r), Mathf.Sin(r));
            bank.Box(centre + dir * (length * 0.5f), new Vector3(thick, 0.004f, length), Quaternion.LookRotation(dir, Vector3.right));
        }

        /// <summary>椅子一脚。座・背の三本の桟・四本の脚。<paramref name="yaw"/> は座る人の向き（+z が 0）</summary>
        static void KitchenChair(KitchenBanks b, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => at + rot * new Vector3(x, y, z);
            b.Oak.Box(p(0f, 0.44f, 0f), new Vector3(0.40f, 0.035f, 0.40f), rot);
            for (var i = 0; i < 4; i++)
                b.Oak.Box(p((i & 1) == 0 ? -0.17f : 0.17f, 0.22f, (i & 2) == 0 ? -0.17f : 0.17f), new Vector3(0.035f, 0.44f, 0.035f), rot);
            for (var i = 0; i < 2; i++)
                b.Oak.Box(p(i == 0 ? -0.17f : 0.17f, 0.68f, -0.18f), new Vector3(0.035f, 0.48f, 0.035f), rot);
            b.Oak.Box(p(0f, 0.88f, -0.18f), new Vector3(0.40f, 0.06f, 0.035f), rot);
            for (var i = 0; i < 3; i++)
                b.Oak.Box(p(-0.09f + i * 0.09f, 0.66f, -0.18f), new Vector3(0.02f, 0.40f, 0.02f), rot);
        }

        /// <summary>
        /// 南の壁（戸口の東）の低い戸棚。上に昼食の袋を二つ、果物の鉢、電子レンジ。壁に献立の板と額
        /// </summary>
        static void KitchenSideUnit(KitchenBanks b)
        {
            const float x0 = KitchenSide0;
            const float x1 = KitchenSide1;
            const float z0 = KitchenDoorZ;
            const float z1 = KitchenDoorZ + KitchenSideDeep;
            b.Cupboard.Box(new Vector3((x0 + x1) * 0.5f, (0.1f + CounterHigh) * 0.5f, (z0 + z1) * 0.5f - 0.02f),
                new Vector3(x1 - x0, CounterHigh - 0.1f, z1 - z0 - 0.04f));
            b.Black.Box(new Vector3((x0 + x1) * 0.5f, 0.05f, z1 - 0.06f), new Vector3(x1 - x0, 0.1f, 0.02f));
            b.Oak.Box(new Vector3((x0 + x1) * 0.5f, CounterHigh + 0.02f, (z0 + z1) * 0.5f + 0.01f), new Vector3(x1 - x0 + 0.02f, 0.04f, z1 - z0 + 0.02f));
            var n = 3;
            var lw = (x1 - x0) / n;
            for (var i = 0; i < n; i++)
            {
                var a = x0 + i * lw;
                b.Cupboard.Box(new Vector3(a + lw * 0.5f, 0.49f, z1 - 0.03f), new Vector3(lw - 0.01f, 0.74f, 0.02f));
                b.Grout.FaceZ(z1 - 0.0195f, a + 0.07f, a + lw - 0.07f, 0.79f, 0.80f, 1);
                b.Grout.FaceZ(z1 - 0.0195f, a + 0.07f, a + lw - 0.07f, 0.18f, 0.19f, 1);
                b.Steel.Box(new Vector3(a + lw - 0.07f, 0.72f, z1), new Vector3(0.02f, 0.12f, 0.02f));
            }
            // 昼食の袋。茶色の紙袋の口を折って、戸口の側に二つ
            KitchenLunch(b, new Vector3(0.98f, KitchenTop, 0.30f), 8f, 0.26f);
            KitchenLunch(b, new Vector3(1.20f, KitchenTop, 0.27f), -5f, 0.23f);
            // 果物の鉢
            b.White.Box(new Vector3(1.55f, KitchenTop + 0.04f, 0.26f), new Vector3(0.26f, 0.08f, 0.26f), Quaternion.Euler(0f, 45f, 0f));
            b.Yellow.Box(new Vector3(1.51f, KitchenTop + 0.10f, 0.24f), new Vector3(0.18f, 0.05f, 0.06f), Quaternion.Euler(0f, 30f, 10f));
            b.Runner.Box(new Vector3(1.60f, KitchenTop + 0.11f, 0.30f), new Vector3(0.08f, 0.08f, 0.08f));
            b.Moss.Box(new Vector3(1.55f, KitchenTop + 0.11f, 0.18f), new Vector3(0.08f, 0.08f, 0.08f));
            // 電子レンジ。白い箱に黒い窓と、右の操作の帯
            var wave = new Vector3(2.02f, KitchenTop, 0.25f);
            b.White.Box(wave + new Vector3(0f, 0.15f, 0f), new Vector3(0.48f, 0.29f, 0.36f));
            b.Black.Box(wave + new Vector3(-0.05f, 0.15f, 0.181f), new Vector3(0.30f, 0.19f, 0.004f));
            b.Grout.Box(new Vector3(wave.x + 0.17f, wave.y + 0.15f, wave.z + 0.181f), new Vector3(0.08f, 0.22f, 0.004f));
            // 壁の献立の板と額
            b.Treads.Box(new Vector3(1.25f, 1.55f, KitchenDoorZ + 0.015f), new Vector3(0.62f, 0.44f, 0.03f));
            b.Oak.Box(new Vector3(1.25f, 1.55f, KitchenDoorZ + 0.032f), new Vector3(0.56f, 0.38f, 0.004f));
            KitchenNote(b.Grout, b.Runner, new Vector3(1.10f, 1.60f, KitchenDoorZ + 0.038f), 0.16f, 0.20f, -4f);
            KitchenNote(b.Grout, b.Blue, new Vector3(1.36f, 1.54f, KitchenDoorZ + 0.038f), 0.14f, 0.18f, 6f);
            KitchenFrameZ(b, KitchenDoorZ + 0.01f, 2.05f, 1.65f, 0.34f, 0.26f, 1, b.Moss);
        }

        /// <summary>昼食の袋一つ。紙袋の胴と、折った口</summary>
        static void KitchenLunch(KitchenBanks b, Vector3 at, float yaw, float high)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            b.Oak.Box(at + rot * new Vector3(0f, high * 0.5f, 0f), new Vector3(0.16f, high, 0.10f), rot);
            b.Oak.Box(at + rot * new Vector3(0f, high + 0.015f, 0.01f), new Vector3(0.15f, 0.03f, 0.08f), rot * Quaternion.Euler(20f, 0f, 0f));
            b.Grout.Box(at + rot * new Vector3(0f, high * 0.55f, -0.051f), new Vector3(0.08f, 0.05f, 0.003f), rot);
        }

        /// <summary>
        /// 東の窓。裏庭を向く上げ下げ窓。窓の穴の抱きと上枠、内の窓台、上下二枚の框と縦の桟、
        /// 窓台の鉢と、巻き上げた日除け、下のラジエーター。**硝子は入れない**（公営住宅の住戸と同じ）。
        /// 朝の光と外の景色をそのまま通す
        /// </summary>
        static void KitchenWindow(KitchenBanks b)
        {
            const float x0 = KitchenEast;
            const float x1 = KitchenEast + KitchenRearSkin;
            const float midZ = (SashZ0 + SashZ1) * 0.5f;
            const float wide = SashZ1 - SashZ0;
            // 抱きと上枠
            b.Walls.FaceZ(SashZ0, x0, x1, SashY0, SashY1, 1);
            b.Walls.FaceZ(SashZ1, x0, x1, SashY0, SashY1, -1);
            b.Walls.FaceY(SashY1, x0, x1, SashZ0, SashZ1, -1);
            b.White.FaceY(SashY0, x0, x1, SashZ0, SashZ1, 1);
            // 内の窓台
            b.White.Box(new Vector3(x0 + 0.02f, SashY0 - 0.02f, midZ), new Vector3(0.18f, 0.04f, wide + 0.16f));
            KitchenArchitraveX(b, x0 - 0.012f, SashZ0, SashZ1, SashY0, SashY1, -1);
            // 窓。外の面から 8 cm 奥に、上の框と下の框を少しずらして重ねる
            const float fx = x1 - 0.08f;
            const float w = 0.05f;
            const float meet = (SashY0 + SashY1) * 0.5f;
            b.White.Box(new Vector3(fx, SashY1 - w * 0.5f, midZ), new Vector3(0.06f, w, wide));
            b.White.Box(new Vector3(fx - 0.04f, SashY0 + w * 0.5f + 0.02f, midZ), new Vector3(0.06f, w + 0.04f, wide));
            b.White.Box(new Vector3(fx, (SashY0 + SashY1) * 0.5f, SashZ0 + w * 0.5f), new Vector3(0.06f, SashY1 - SashY0, w));
            b.White.Box(new Vector3(fx, (SashY0 + SashY1) * 0.5f, SashZ1 - w * 0.5f), new Vector3(0.06f, SashY1 - SashY0, w));
            b.White.Box(new Vector3(fx, meet + 0.02f, midZ), new Vector3(0.05f, 0.04f, wide));
            b.White.Box(new Vector3(fx - 0.04f, meet - 0.02f, midZ), new Vector3(0.05f, 0.04f, wide));
            b.White.Box(new Vector3(fx, (meet + SashY1) * 0.5f, midZ), new Vector3(0.04f, SashY1 - meet, 0.03f));
            b.White.Box(new Vector3(fx - 0.04f, (SashY0 + meet) * 0.5f, midZ), new Vector3(0.04f, meet - SashY0, 0.03f));
            b.Brass.Box(new Vector3(fx - 0.07f, meet, midZ), new Vector3(0.03f, 0.02f, 0.06f));
            // 巻き上げた日除け
            b.White.Box(new Vector3(x0 - 0.03f, SashY1 + 0.10f, midZ), new Vector3(0.06f, 0.06f, wide + 0.1f));
            b.Brass.Box(new Vector3(x0 - 0.04f, SashY1 - 0.08f, midZ), new Vector3(0.01f, 0.18f, 0.01f));
            // 窓台の鉢。香草の鉢と小さな陶器の鉢。日を受けて床の四角に影を落とす
            b.Runner.Box(new Vector3(x0 + 0.03f, SashY0 + 0.06f, SashZ0 + 0.25f), new Vector3(0.12f, 0.12f, 0.12f));
            for (var i = 0; i < 5; i++)
                b.Turf.Box(new Vector3(x0 + 0.03f, SashY0 + 0.20f, SashZ0 + 0.25f), new Vector3(0.03f, 0.20f, 0.10f),
                    Quaternion.Euler(0f, i * 36f, 18f - (i % 2) * 36f));
            b.White.Box(new Vector3(x0 + 0.02f, SashY0 + 0.05f, SashZ1 - 0.22f), new Vector3(0.09f, 0.10f, 0.09f));
            b.Moss.Box(new Vector3(x0 + 0.02f, SashY0 + 0.15f, SashZ1 - 0.22f), new Vector3(0.05f, 0.12f, 0.05f));
            // ラジエーター。窓の下
            KitchenRadiatorX(b, x0 - 0.05f, SashZ0 + 0.05f, SashZ1 - 0.05f, -1);
        }

        // ---- 影の殻 ----------------------------------------------------------------

        /// <summary>
        /// 影だけを落とす殻。家の東の外壁（窓の穴だけ開ける）・南の外壁・屋根を厚い箱で囲う。
        ///
        /// 部屋の壁は内側の面を一枚張っているだけなので、外から来る日の影を落とせない（裏を向いた面は影の勘定から外れる）。
        /// 描かない殻を外に被せて、日の光が家の中に入るのは台所の窓からだけにする。
        /// 日は東南東の上から来るので、北と西の面は要らない
        /// </summary>
        static void KitchenShadowShell(KitchenBanks b)
        {
            const float e0 = KitchenEast;
            const float e1 = KitchenEast + 0.3f;
            const float n = KitchenBack + 0.25f;
            const float s0 = KitchenLoungeSouth - 0.3f;
            const float top = KitchenVoidTop + 0.3f;
            const float west = PorchEdge - 0.2f;
            // 東の外壁。窓の穴の上下左右
            KitchenSolid(b.Shade, e0, e1, -0.1f, SashY0, s0, n);
            KitchenSolid(b.Shade, e0, e1, SashY1, top, s0, n);
            KitchenSolid(b.Shade, e0, e1, SashY0, SashY1, s0, SashZ0);
            KitchenSolid(b.Shade, e0, e1, SashY0, SashY1, SashZ1, n);
            // 南の外壁と屋根
            KitchenSolid(b.Shade, west, e1, -0.1f, top, s0, KitchenLoungeSouth);
            KitchenSolid(b.Shade, west, e1, KitchenVoidTop, top, s0, n);
        }

        /// <summary>軸に沿った箱を端の値で</summary>
        static void KitchenSolid(Bank bank, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            bank.Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0));
        }

        // ---- 当たり ----------------------------------------------------------------

        /// <summary>
        /// 大きな家具の当たり。
        ///
        /// mesh の当たりを家具ごとに入れると引っ掛かる隅が増えるので、抜けられると困る物だけを箱で囲う。
        /// 記憶の鍵打ちの立ち位置は、どれもこの箱の外に取ってある
        /// </summary>
        static void KitchenBlocks(Transform place)
        {
            Fence(place, "KitchenCounterBlock", new Vector3((CounterX0 + CounterX1) * 0.5f, KitchenTop * 0.5f, (CounterFace - 0.03f + KitchenBack) * 0.5f),
                new Vector3(CounterX1 - CounterX0, KitchenTop, KitchenBack - CounterFace + 0.03f));
            Fence(place, "KitchenFridgeBlock", new Vector3((KitchenFridge0 + KitchenFridge1) * 0.5f, 0.9f, (KitchenFridgeFace + KitchenBack) * 0.5f),
                new Vector3(KitchenFridge1 - KitchenFridge0, 1.8f, KitchenBack - KitchenFridgeFace));
            Fence(place, "KitchenSideBlock", new Vector3((KitchenSide0 + KitchenSide1) * 0.5f, KitchenTop * 0.5f, KitchenDoorZ + KitchenSideDeep * 0.5f),
                new Vector3(KitchenSide1 - KitchenSide0, KitchenTop, KitchenSideDeep));
            Fence(place, "KitchenTableBlock", KitchenTablePos + new Vector3(0f, 0.38f, 0f), new Vector3(1.0f, 0.76f, 0.72f));
            // 窓の穴。窓台は腰より高いが、走り込んだときに外へ抜けないように
            Fence(place, "KitchenSashBlock", new Vector3(KitchenEast + KitchenRearSkin * 0.5f, (SashY0 + SashY1) * 0.5f, (SashZ0 + SashZ1) * 0.5f),
                new Vector3(KitchenRearSkin, SashY1 - SashY0, SashZ1 - SashZ0));
            // 階段の開いた側。段の脇から廊下へ落ちず、段の下へも潜り込まない
            Fence(place, "KitchenStairSide", new Vector3(KitchenStairWest - 0.02f, 0.8f, (StairFoot + KitchenHallSouth) * 0.5f),
                new Vector3(0.04f, 1.6f, StairFoot - KitchenHallSouth));
            // ポーチの奥。白い光の手前で止める
            Fence(place, "KitchenPorchEnd", new Vector3(PorchEdge + 0.12f, KitchenPorchHigh * 0.5f, (PorchZ0 + PorchZ1) * 0.5f),
                new Vector3(0.2f, KitchenPorchHigh, PorchZ1 - PorchZ0 + 0.1f));
            // 居間の家具
            Fence(place, "KitchenLoungeSofa", new Vector3(KitchenWest + 0.40f, 0.4f, -3.75f), new Vector3(0.80f, 0.8f, 1.45f));
            Fence(place, "KitchenLoungeHearth", new Vector3(-0.30f, 0.6f, KitchenLoungeSouth + 0.30f), new Vector3(1.66f, 1.2f, 0.60f));
            Fence(place, "KitchenLoungeChair", new Vector3(0.62f, 0.4f, -3.75f), new Vector3(0.80f, 0.8f, 0.80f));
            Fence(place, "KitchenLoungeTable", new Vector3(-1.05f, 0.21f, -3.85f), new Vector3(0.80f, 0.42f, 0.50f));
            Fence(place, "KitchenLoungeShelf", new Vector3(0.87f, 0.9f, KitchenLoungeSouth + 0.16f), new Vector3(0.76f, 1.8f, 0.32f));
        }

        // ---- 道具 ------------------------------------------------------------------

        /// <summary>
        /// 廊下の z が一定の壁を一面。腰壁・長押・壁紙・幅木・回り縁。<paramref name="holes"/> は戸口（x0, x1, y0, y1）
        /// </summary>
        static void KitchenBandZ(KitchenBanks b, float z, float x0, float x1, int sign, List<Vector4> holes)
        {
            var s = sign > 0 ? 1f : -1f;
            b.Dado.FaceZHoles(z, x0, x1, 0f, KitchenDadoY, sign, holes);
            b.Paper.FaceZHoles(z, x0, x1, KitchenDadoY, HouseHigh, sign, holes);
            foreach (var r in KitchenSpans(x0, x1, holes, 0.05f))
                b.White.Box(new Vector3((r.x + r.y) * 0.5f, 0.09f, z + s * 0.01f), new Vector3(r.y - r.x, 0.18f, 0.02f));
            foreach (var r in KitchenSpans(x0, x1, holes, KitchenDadoY + 0.02f))
                b.White.Box(new Vector3((r.x + r.y) * 0.5f, KitchenDadoY + 0.02f, z + s * 0.012f), new Vector3(r.y - r.x, 0.045f, 0.025f));
            KitchenCorniceZ(b, z + s * 0.03f, x0, x1, sign);
        }

        /// <summary>廊下の x が一定の壁を一面。<paramref name="holes"/> は（z0, z1, y0, y1）</summary>
        static void KitchenBandX(KitchenBanks b, float x, float z0, float z1, int sign, List<Vector4> holes)
        {
            var s = sign > 0 ? 1f : -1f;
            b.Dado.FaceXHoles(x, z0, z1, 0f, KitchenDadoY, sign, holes);
            b.Paper.FaceXHoles(x, z0, z1, KitchenDadoY, HouseHigh, sign, holes);
            foreach (var r in KitchenSpans(z0, z1, holes, 0.05f))
                b.White.Box(new Vector3(x + s * 0.01f, 0.09f, (r.x + r.y) * 0.5f), new Vector3(0.02f, 0.18f, r.y - r.x));
            foreach (var r in KitchenSpans(z0, z1, holes, KitchenDadoY + 0.02f))
                b.White.Box(new Vector3(x + s * 0.012f, KitchenDadoY + 0.02f, (r.x + r.y) * 0.5f), new Vector3(0.025f, 0.045f, r.y - r.x));
            KitchenCorniceX(b, x + s * 0.03f, z0, z1, sign);
        }

        /// <summary>u0〜u1 のうち、高さ y で穴に掛からない区間</summary>
        static List<Vector2> KitchenSpans(float u0, float u1, List<Vector4> holes, float y)
        {
            var gaps = new List<Vector2>();
            if (holes != null)
                foreach (var h in holes)
                    if (y >= h.z && y < h.w) gaps.Add(new Vector2(h.x, h.y));
            return ParkSplit(u0, u1, gaps.ToArray());
        }

        /// <summary>幅木。z が一定の壁。<paramref name="floor"/> は床の高さ</summary>
        static void KitchenSkirtZ(KitchenBanks b, float z, float x0, float x1, int sign, float floor = 0f)
        {
            if (x1 - x0 < 0.01f) return;
            b.White.Box(new Vector3((x0 + x1) * 0.5f, floor + 0.09f, z), new Vector3(x1 - x0, 0.18f, 0.02f));
        }

        static void KitchenSkirtX(KitchenBanks b, float x, float z0, float z1, int sign, float floor = 0f)
        {
            if (z1 - z0 < 0.01f) return;
            b.White.Box(new Vector3(x, floor + 0.09f, (z0 + z1) * 0.5f), new Vector3(0.02f, 0.18f, z1 - z0));
        }

        /// <summary>回り縁。天井の際の白い帯</summary>
        static void KitchenCorniceZ(KitchenBanks b, float z, float x0, float x1, int sign)
        {
            b.White.Box(new Vector3((x0 + x1) * 0.5f, HouseHigh - 0.05f, z), new Vector3(x1 - x0, 0.10f, 0.06f));
        }

        static void KitchenCorniceX(KitchenBanks b, float x, float z0, float z1, int sign)
        {
            b.White.Box(new Vector3(x, HouseHigh - 0.05f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.10f, z1 - z0));
        }

        /// <summary>戸口の額縁。z が一定の壁の面に、両脇と上の三本</summary>
        static void KitchenArchitraveZ(KitchenBanks b, float z, float x0, float x1, float high, int sign, float floor = 0f)
        {
            const float w = 0.07f;
            b.White.Box(new Vector3(x0 - w * 0.5f, floor + (high - floor + w) * 0.5f, z), new Vector3(w, high - floor + w, 0.024f));
            b.White.Box(new Vector3(x1 + w * 0.5f, floor + (high - floor + w) * 0.5f, z), new Vector3(w, high - floor + w, 0.024f));
            b.White.Box(new Vector3((x0 + x1) * 0.5f, high + w * 0.5f, z), new Vector3(x1 - x0 + w * 2f, w, 0.024f));
        }

        /// <summary>窓の額縁。x が一定の壁の面に四辺</summary>
        static void KitchenArchitraveX(KitchenBanks b, float x, float z0, float z1, float y0, float y1, int sign)
        {
            const float w = 0.06f;
            b.White.Box(new Vector3(x, (y0 + y1) * 0.5f, z0 - w * 0.5f), new Vector3(0.024f, y1 - y0 + w * 2f, w));
            b.White.Box(new Vector3(x, (y0 + y1) * 0.5f, z1 + w * 0.5f), new Vector3(0.024f, y1 - y0 + w * 2f, w));
            b.White.Box(new Vector3(x, y1 + w * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.024f, w, z1 - z0));
        }

        /// <summary>閉めた戸一枚。z が一定の壁の前に、四枚の鏡板の縁と真鍮の把手</summary>
        static void KitchenDoorLeafZ(KitchenBanks b, float z, float x, float floor, float wide, int sign)
        {
            var s = sign > 0 ? 1f : -1f;
            b.White.Box(new Vector3(x, floor + 0.985f, z), new Vector3(wide, 1.97f, 0.04f));
            for (var i = 0; i < 4; i++)
            {
                var px = x + (i % 2 == 0 ? -0.17f : 0.17f);
                var py = floor + (i < 2 ? 1.42f : 0.52f);
                var ph = i < 2 ? 0.80f : 0.64f;
                KitchenPanelZ(b.Grout, z + s * 0.021f, px, py, 0.28f, ph, sign);
            }
            b.Brass.Box(new Vector3(x - wide * 0.5f + 0.07f, floor + 1.0f, z + s * 0.05f), new Vector3(0.05f, 0.05f, 0.05f));
        }

        /// <summary>戸の鏡板の縁を一つ。z が一定の面に四辺の細い線</summary>
        static void KitchenPanelZ(Bank bank, float z, float cx, float cy, float w, float h, int sign)
        {
            const float t = 0.018f;
            bank.FaceZ(z, cx - w * 0.5f, cx + w * 0.5f, cy - h * 0.5f, cy - h * 0.5f + t, sign);
            bank.FaceZ(z, cx - w * 0.5f, cx + w * 0.5f, cy + h * 0.5f - t, cy + h * 0.5f, sign);
            bank.FaceZ(z, cx - w * 0.5f, cx - w * 0.5f + t, cy - h * 0.5f, cy + h * 0.5f, sign);
            bank.FaceZ(z, cx + w * 0.5f - t, cx + w * 0.5f, cy - h * 0.5f, cy + h * 0.5f, sign);
        }

        /// <summary>戸の鏡板の縁を一つ。x が一定の面に四辺の細い線</summary>
        static void KitchenPanelX(Bank bank, float x, float cz, float cy, float w, float h, int sign)
        {
            const float t = 0.018f;
            bank.FaceX(x, cz - w * 0.5f, cz + w * 0.5f, cy - h * 0.5f, cy - h * 0.5f + t, sign);
            bank.FaceX(x, cz - w * 0.5f, cz + w * 0.5f, cy + h * 0.5f - t, cy + h * 0.5f, sign);
            bank.FaceX(x, cz - w * 0.5f, cz - w * 0.5f + t, cy - h * 0.5f, cy + h * 0.5f, sign);
            bank.FaceX(x, cz + w * 0.5f - t, cz + w * 0.5f, cy - h * 0.5f, cy + h * 0.5f, sign);
        }

        /// <summary>
        /// 円一枚（皿・時計の文字盤）。<paramref name="u"/> と <paramref name="v"/> が円の面を張る二つの向き、
        /// <paramref name="normal"/> の側を表にする扇。裏表を描きたい物（立てた皿）は二度呼ぶ
        /// </summary>
        static void KitchenRound(Bank bank, Vector3 centre, Vector3 u, Vector3 v, float radius, int sides, Vector3 normal)
        {
            for (var k = 0; k < sides; k++)
            {
                var a0 = k * Mathf.PI * 2f / sides;
                var a1 = (k + 1) * Mathf.PI * 2f / sides;
                var p0 = centre + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * radius;
                var p1 = centre + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * radius;
                MidQuad(bank, centre, p0, p1, p1, normal);
            }
        }

        /// <summary>x が一定の壁に貼る円（時計の文字盤）。+x を向く</summary>
        static void KitchenDiscX(Bank bank, Vector3 centre, float radius, int sides)
        {
            KitchenRound(bank, centre, Vector3.up, Vector3.forward, radius, sides, Vector3.right);
        }

        /// <summary>寝かせた円（皿）。上を向く</summary>
        static void KitchenDiscY(Bank bank, Vector3 centre, float radius, int sides)
        {
            KitchenRound(bank, centre, Vector3.right, Vector3.forward, radius, sides, Vector3.up);
        }

        /// <summary>
        /// 立てた皿一枚。<paramref name="facing"/> の向きの面に、少し傾けて。両面を描く
        /// </summary>
        static void KitchenPlate(Bank bank, Vector3 centre, Vector3 facing, float radius, float lean)
        {
            var n = Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, facing)) * facing.normalized;
            var u = Vector3.Cross(n, Vector3.up).normalized;
            var v = Vector3.Cross(u, n).normalized;
            KitchenRound(bank, centre + n * 0.003f, u, v, radius, 14, n);
            KitchenRound(bank, centre - n * 0.003f, u, v, radius, 14, -n);
        }

        /// <summary>パネルのラジエーター。z が一定の壁の前。上の縁に格子の筋、両脇に床へ下りる管</summary>
        static void KitchenRadiatorZ(KitchenBanks b, float z, float x0, float x1, int sign)
        {
            var s = sign > 0 ? 1f : -1f;
            b.White.Box(new Vector3((x0 + x1) * 0.5f, 0.46f, z + s * 0.02f), new Vector3(x1 - x0, 0.56f, 0.06f));
            for (var x = x0 + 0.06f; x < x1 - 0.03f; x += 0.06f)
                b.Grout.FaceZ(z + s * 0.051f, x - 0.004f, x + 0.004f, 0.22f, 0.70f, sign);
            b.Steel.Box(new Vector3(x0 + 0.04f, 0.09f, z + s * 0.03f), new Vector3(0.025f, 0.18f, 0.025f));
            b.Steel.Box(new Vector3(x1 - 0.04f, 0.09f, z + s * 0.03f), new Vector3(0.025f, 0.18f, 0.025f));
        }

        static void KitchenRadiatorX(KitchenBanks b, float x, float z0, float z1, int sign)
        {
            var s = sign > 0 ? 1f : -1f;
            b.White.Box(new Vector3(x + s * 0.02f, 0.46f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.56f, z1 - z0));
            for (var z = z0 + 0.06f; z < z1 - 0.03f; z += 0.06f)
                b.Grout.FaceX(x + s * 0.051f, z - 0.004f, z + 0.004f, 0.22f, 0.70f, sign);
            b.Steel.Box(new Vector3(x + s * 0.03f, 0.09f, z0 + 0.04f), new Vector3(0.025f, 0.18f, 0.025f));
            b.Steel.Box(new Vector3(x + s * 0.03f, 0.09f, z1 - 0.04f), new Vector3(0.025f, 0.18f, 0.025f));
        }

        /// <summary>額一つ。z が一定の壁。木の縁に紙と、絵の色の四角</summary>
        static void KitchenFrameZ(KitchenBanks b, float z, float x, float y, float high, float wide, int sign, Bank paint)
        {
            var s = sign > 0 ? 1f : -1f;
            b.Treads.Box(new Vector3(x, y, z + s * 0.012f), new Vector3(wide, high, 0.024f));
            b.Grout.FaceZ(z + s * 0.025f, x - wide * 0.5f + 0.035f, x + wide * 0.5f - 0.035f, y - high * 0.5f + 0.035f, y + high * 0.5f - 0.035f, sign);
            paint.FaceZ(z + s * 0.026f, x - wide * 0.5f + 0.07f, x + wide * 0.5f - 0.07f, y - high * 0.5f + 0.07f, y + high * 0.5f - 0.07f, sign);
        }

        static void KitchenFrameX(KitchenBanks b, float x, float z, float y, float high, float wide, int sign, Bank paint)
        {
            var s = sign > 0 ? 1f : -1f;
            b.Treads.Box(new Vector3(x + s * 0.012f, y, z), new Vector3(0.024f, high, wide));
            b.Grout.FaceX(x + s * 0.025f, z - wide * 0.5f + 0.035f, z + wide * 0.5f - 0.035f, y - high * 0.5f + 0.035f, y + high * 0.5f - 0.035f, sign);
            paint.FaceX(x + s * 0.026f, z - wide * 0.5f + 0.07f, z + wide * 0.5f - 0.07f, y - high * 0.5f + 0.07f, y + high * 0.5f - 0.07f, sign);
        }

        /// <summary>
        /// 番地の数字を x が一定の面へ。公営住宅の <see cref="EstateGlyph"/> と同じ七つの棒で、面の向きだけ x にしたもの。
        /// <paramref name="sign"/> は面の向き（x の符号）。数字は内（+x の側）から読む向きに並べる
        /// </summary>
        static void KitchenDigitsX(Bank b, float x, float cz, float cy, int sign, int number, float high)
        {
            var text = number.ToString();
            var wide = high * 0.55f;
            var gap = high * 0.25f;
            var span = text.Length * wide + (text.Length - 1) * gap;
            // 内から読む人（+x の側から -x を見る）の右は +z
            const float right = 1f;
            var start = cz - right * (span * 0.5f - wide * 0.5f);
            int[] codes = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
            for (var i = 0; i < text.Length; i++)
            {
                var c = start + right * i * (wide + gap);
                var digit = text[i] - '0';
                if (digit < 0 || digit > 9) continue;
                var bits = codes[digit];
                var t = high * 0.14f;
                var hw = wide * 0.5f;
                var hh = high * 0.5f;
                System.Action<float, float, float, float> bar = (u0, u1, v0, v1) =>
                {
                    var a = c + right * u0;
                    var e = c + right * u1;
                    b.FaceX(x, Mathf.Min(a, e), Mathf.Max(a, e), cy + v0, cy + v1, sign);
                };
                if ((bits & 1) != 0) bar(-hw, hw, hh - t, hh);
                if ((bits & 2) != 0) bar(hw - t, hw, 0f, hh);
                if ((bits & 4) != 0) bar(hw - t, hw, -hh, 0f);
                if ((bits & 8) != 0) bar(-hw, hw, -hh, -hh + t);
                if ((bits & 16) != 0) bar(-hw, -hw + t, -hh, 0f);
                if ((bits & 32) != 0) bar(-hw, -hw + t, 0f, hh);
                if ((bits & 64) != 0) bar(-hw, hw, -t * 0.5f, t * 0.5f);
            }
        }
    }
}
