using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公営住宅。上のメゾネット二戸の暮らし。躯体は <c>BuildDiveEstateFlat.cs</c>。
    ///
    /// **二つの家は暮らしぶりで分ける。** A は母ハンナと娘メイの二人。明るい黄色と青、
    /// 床に出たままの玩具、ソファとテレビ、白い食卓に朝食の途中。
    /// B は年を取った夫婦ジョルジョとエレナ。肘掛け椅子が二つ暖炉を向き、
    /// 家族の写真を並べたサイドボード、レースの敷物、壁の時計、窓辺の植木、
    /// テーブルクロスの掛かった食卓。壁紙と絨毯の色から違う。
    ///
    /// 家具には当たりを入れない。入れても歩く先が変わらないわりに、
    /// 引っ掛かって抜け出せない隅が増える（<see cref="Emit"/> の決まり）。
    /// **戸口の前と廊下と階段の上り口には物を置かない。** 記憶の人がそこに立つ
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 日本の間取りの頃の名前 --------------------------------------------
        //
        // BuildDiveTakes が見ているので、名前は残して値を新しい間取りへ移す

        /// <summary>B の廊下の突き当たり（居間の北の壁の真ん中）</summary>
        const float RoomEnd = HallWall;                                                     // -19.45
        /// <summary>B の廊下の西の面</summary>
        const float RoomHall0 = HallGap0;                                                   // 9.95
        /// <summary>B の廊下の東の面（階段との仕切り）</summary>
        const float RoomHall1 = HallGap1;                                                   // 10.85
        /// <summary>B の廊下の真ん中の線。玄関から居間の戸口まで真っ直ぐ通る</summary>
        const float RoomWalk = (RoomHall0 + RoomHall1) * 0.5f;                              // 10.4
        /// <summary>A の西と東の内壁の面、南の内壁、三階の天井</summary>
        const float FlatX0 = FlatWest + PartyHalf;                                          // 1.3
        const float FlatX1 = FlatWest + FlatWide - PartyHalf;                               // 6.5
        const float FlatBack = RoomBack;                                                    // -23.15
        const float FlatRoof = WalkRoof;                                                    // 8.1
        /// <summary>A の家の中の仕切りの厚みの半分。B と同じ</summary>
        const float FlatSkin = HallSkin;

        // ---- A（母ハンナと娘メイ） ----------------------------------------------

        /// <summary>A の家。三階の玄関・台所・食卓・居間と、四階の娘の部屋・母の寝室・浴室</summary>
        static void EstateHomeA(EstateBanks b)
        {
            const int u = 0;
            const float f3 = EstateTop;
            const float f4 = EstateUpper;

            // ---- 玄関 ----
            // 足拭き・コート掛け・子どもの長靴。玄関の戸の正面から見えるのはここと階段まで
            FlatThing(b.Red, u, DoorAt, 0.60f, f3, new Vector3(0.80f, 0.015f, 0.50f));
            EstateHooks(b, u, 0.80f);
            FlatThing(b.Blue, u, LaneWest + 0.10f, 0.58f, f3 + 0.70f, new Vector3(0.16f, 0.92f, 0.40f));
            FlatThing(b.Yellow, u, LaneWest + 0.09f, 1.02f, f3 + 1.02f, new Vector3(0.13f, 0.56f, 0.30f));
            FlatThing(b.Red, u, LaneWest + 0.12f, 1.12f, f3, new Vector3(0.18f, 0.30f, 0.24f));
            for (var i = 0; i < 2; i++)
                FlatThing(b.Red, u, 3.52f + i * 0.12f, 0.45f, f3, new Vector3(0.09f, 0.20f, 0.17f));
            // 階段の手すり壁に貼った娘の絵。貼る高さは子どもの目
            FlatThing(b.Paper, u, LaneEast - 0.01f, 2.00f, f3 + 0.92f, new Vector3(0.012f, 0.26f, 0.21f), 4f);
            FlatThing(b.Paper, u, LaneEast - 0.01f, 2.55f, f3 + 1.05f, new Vector3(0.012f, 0.22f, 0.26f), -5f);
            // 廊下の西の壁の写真。母と娘
            EstateFrameOn(b, u, LaneWest + 0.01f, 3.05f, f3 + 1.45f, 0.30f, 0.38f, 1);

            // ---- 台所 ----
            // 白い戸棚に黄色の天板。デッキ側の窓の下に流し、西の壁に沿ってコンロ
            EstateKitchenRun(b, u, b.Frame, b.Yellow);
            // 背の高い冷蔵庫。扉に娘の絵と磁石。窓の下の並びと仕切りのあいだに収める
            FlatThing(b.Frame, u, 2.80f, 0.58f, f3, new Vector3(0.56f, 1.80f, 0.64f));
            b.Shade.FaceZ(FlatZ(0.905f), FlatWest + 2.54f, FlatWest + 3.06f, f3 + 1.10f, f3 + 1.12f, -1);
            FlatThing(b.Gear, u, 2.56f, 0.92f, f3 + 1.20f, new Vector3(0.03f, 0.40f, 0.03f));
            FlatThing(b.Paper, u, 2.90f, 0.905f, f3 + 1.25f, new Vector3(0.22f, 0.28f, 0.01f), 6f);
            FlatThing(b.Paper, u, 2.74f, 0.905f, f3 + 0.70f, new Vector3(0.20f, 0.24f, 0.01f), -4f);
            FlatThing(b.Blue, u, 3.00f, 0.91f, f3 + 1.52f, new Vector3(0.05f, 0.05f, 0.02f));
            FlatThing(b.Red, u, 2.66f, 0.91f, f3 + 1.00f, new Vector3(0.05f, 0.05f, 0.02f));
            // やかん・トースター・果物の鉢・弁当箱
            FlatThing(b.Red, u, 0.40f, 1.25f, f3 + 0.88f, new Vector3(0.16f, 0.22f, 0.16f));
            FlatThing(b.Gear, u, 0.40f, 1.60f, f3 + 0.88f, new Vector3(0.18f, 0.18f, 0.28f));
            FlatThing(b.Yellow, u, 0.75f, 0.55f, f3 + 0.88f, new Vector3(0.26f, 0.10f, 0.26f));
            FlatThing(b.Blue, u, 2.20f, 0.55f, f3 + 0.88f, new Vector3(0.24f, 0.10f, 0.16f));
            // 窓の巻き上げた日除け
            FlatThing(b.Yellow, u, (KitchenWin0 + KitchenWin1) * 0.5f, 0.21f, f3 + KitchenHead - 0.12f,
                new Vector3(KitchenWin1 - KitchenWin0, 0.09f, 0.07f));
            // 仕切りの板。予定表と、娘の絵
            FlatThing(b.Set, u, KitchenWall - HallSkin - 0.01f, 3.00f, f3 + 1.10f, new Vector3(0.02f, 0.62f, 0.80f));
            FlatThing(b.Paper, u, KitchenWall - HallSkin - 0.025f, 2.82f, f3 + 1.18f, new Vector3(0.01f, 0.42f, 0.30f));
            FlatThing(b.Paper, u, KitchenWall - HallSkin - 0.025f, 3.20f, f3 + 1.28f, new Vector3(0.01f, 0.28f, 0.24f), 8f);

            // ---- 食卓 ----
            // 配膳の小窓の向こう。白い天板に朝食の途中。娘の椅子にだけ座布団を重ねる
            EstateTable(b, u, b.Frame, 1.60f, 4.50f, 1.10f, 0.80f);
            EstateChair(b, b.Yellow, b.Frame, u, 0.75f, 4.50f, f3, 90f);
            FlatThing(b.Yellow, u, 0.75f, 4.50f, f3 + 0.49f, new Vector3(0.36f, 0.10f, 0.36f));
            EstateChair(b, b.Blue, b.Frame, u, 2.45f, 4.50f, f3, 270f);
            EstateChair(b, b.Red, b.Frame, u, 1.60f, 5.20f, f3, 0f);
            FlatThing(b.Frame, u, 1.25f, 4.45f, f3 + 0.72f, new Vector3(0.16f, 0.06f, 0.16f));
            FlatThing(b.Frame, u, 1.95f, 4.55f, f3 + 0.72f, new Vector3(0.16f, 0.06f, 0.16f));
            FlatThing(b.Yellow, u, 1.60f, 4.25f, f3 + 0.72f, new Vector3(0.20f, 0.28f, 0.07f));
            FlatThing(b.Paper, u, 1.35f, 4.75f, f3 + 0.72f, new Vector3(0.30f, 0.01f, 0.22f), 10f);
            FlatThing(b.Red, u, 1.52f, 4.70f, f3 + 0.73f, new Vector3(0.10f, 0.012f, 0.012f), 30f);
            FlatThing(b.Blue, u, 1.56f, 4.66f, f3 + 0.73f, new Vector3(0.10f, 0.012f, 0.012f), -20f);

            // ---- 居間 ----
            // ソファは西の壁、テレビは東の壁。あいだに黄色の敷物と、出したままの玩具
            FlatThing(b.Blue, u, 0.55f, 6.90f, f3, new Vector3(0.80f, 0.42f, 1.90f));
            FlatThing(b.Blue, u, 0.22f, 6.90f, f3 + 0.42f, new Vector3(0.18f, 0.44f, 1.90f));
            FlatThing(b.Blue, u, 0.55f, 5.90f, f3, new Vector3(0.82f, 0.62f, 0.16f));
            FlatThing(b.Blue, u, 0.55f, 7.90f, f3, new Vector3(0.82f, 0.62f, 0.16f));
            FlatThing(b.Yellow, u, 0.40f, 6.40f, f3 + 0.42f, new Vector3(0.12f, 0.34f, 0.36f), 8f);
            FlatThing(b.Red, u, 0.40f, 7.40f, f3 + 0.42f, new Vector3(0.12f, 0.32f, 0.34f), -6f);
            FlatThing(b.Red, u, 0.62f, 7.72f, f3 + 0.42f, new Vector3(0.70f, 0.04f, 0.40f), 4f);
            FlatThing(b.Yellow, u, 2.45f, 6.90f, f3, new Vector3(2.30f, 0.012f, 2.30f));
            FlatThing(b.Set, u, 2.10f, 6.90f, f3, new Vector3(0.55f, 0.40f, 1.00f));
            FlatThing(b.Red, u, 2.05f, 6.70f, f3 + 0.40f, new Vector3(0.09f, 0.10f, 0.09f));
            FlatThing(b.Shade, u, 2.15f, 7.10f, f3 + 0.40f, new Vector3(0.05f, 0.02f, 0.16f));
            // テレビ台と薄いテレビ。画面の光る面は Estate が置く
            FlatThing(b.Frame, u, 5.05f, 6.90f, f3, new Vector3(0.45f, 0.45f, 1.50f));
            FlatThing(b.Shade, u, 5.21f, 6.90f, f3 + 0.55f, new Vector3(0.05f, 0.62f, 1.05f));
            // 玩具。積み木・熊のぬいぐるみ・ボール・玩具の箱
            FlatThing(b.Red, u, 2.95f, 6.30f, f3, new Vector3(0.10f, 0.10f, 0.10f));
            FlatThing(b.Blue, u, 3.08f, 6.44f, f3, new Vector3(0.10f, 0.10f, 0.10f), 20f);
            FlatThing(b.Yellow, u, 2.97f, 6.34f, f3 + 0.10f, new Vector3(0.10f, 0.10f, 0.10f), 35f);
            FlatThing(b.Yellow, u, 3.25f, 7.45f, f3, new Vector3(0.22f, 0.24f, 0.16f));
            FlatThing(b.Yellow, u, 3.25f, 7.45f, f3 + 0.24f, new Vector3(0.16f, 0.15f, 0.14f));
            FlatThing(b.Red, u, 3.70f, 6.10f, f3, new Vector3(0.18f, 0.18f, 0.18f), 45f);
            FlatThing(b.Red, u, 4.35f, 8.05f, f3, new Vector3(0.70f, 0.45f, 0.45f));
            FlatThing(b.Blue, u, 4.35f, 8.05f, f3 + 0.45f, new Vector3(0.72f, 0.04f, 0.47f));
            // 絵本の棚。食卓の張り出しとソファのあいだ
            FlatThing(b.Frame, u, 0.28f, 5.30f, f3, new Vector3(0.34f, 0.90f, 0.70f));
            for (var i = 0; i < 5; i++)
                FlatThing(i % 3 == 0 ? b.Red : i % 3 == 1 ? b.Blue : b.Yellow, u, 0.30f, 5.02f + i * 0.13f, f3 + 0.52f,
                    new Vector3(0.22f, 0.26f - (i % 2) * 0.04f, 0.04f));
            // 床の灯りとカーテン
            FlatThing(b.Gear, u, 0.40f, 8.05f, f3, new Vector3(0.04f, 1.50f, 0.04f));
            FlatThing(b.Frame, u, 0.40f, 8.05f, f3 + 1.40f, new Vector3(0.36f, 0.30f, 0.36f));
            EstateCurtains(b, u, b.Yellow, LoungeWin0, LoungeWin1, FlatBackIn, f3, LoungeHead, 1);
            // 壁の写真。ソファの上
            EstateFrameOn(b, u, FlatIn + 0.01f, 6.90f, f3 + 1.40f, 0.42f, 0.56f, 1);
            // 窓辺の小さな鉢
            FlatThing(b.Red, u, 3.90f, 8.30f, f3 + LoungeSill, new Vector3(0.12f, 0.12f, 0.12f));
            FlatThing(b.Turf, u, 3.90f, 8.30f, f3 + LoungeSill + 0.12f, new Vector3(0.10f, 0.14f, 0.10f));

            // ---- 娘の部屋（表の寝室。デッキの上へ張り出した側） ----
            EstateBed(b, u, b.Frame, b.Yellow, b.Red, 0.55f, -0.72f, f4, 0.92f, true);
            FlatThing(b.Moss, u, 0.62f, -1.20f, f4 + 0.44f, new Vector3(0.18f, 0.22f, 0.14f));
            FlatThing(b.Blue, u, 0.42f, -1.00f, f4 + 0.44f, new Vector3(0.14f, 0.18f, 0.12f), 20f);
            FlatThing(b.Frame, u, 5.00f, -1.10f, f4, new Vector3(0.58f, 1.80f, 1.20f));
            FlatThing(b.Gear, u, 4.70f, -1.10f, f4 + 1.00f, new Vector3(0.02f, 0.20f, 0.03f));
            FlatThing(b.Blue, u, 2.50f, -0.40f, f4, new Vector3(1.60f, 0.012f, 1.40f));
            FlatThing(b.Frame, u, 2.70f, 0.52f, f4, new Vector3(0.80f, 0.52f, 0.52f));
            EstateChair(b, b.Blue, b.Frame, u, 2.70f, 0.02f, f4, 180f, 0.75f);
            FlatThing(b.Paper, u, 2.55f, 0.50f, f4 + 0.52f, new Vector3(0.30f, 0.01f, 0.22f), -8f);
            // 玩具の箱。戸の板を寄せる東の脇は空けておく
            FlatThing(b.Yellow, u, 4.80f, 0.70f, f4, new Vector3(0.60f, 0.40f, 0.40f));
            FlatThing(b.Paper, u, FlatIn + 0.01f, -0.40f, f4 + 1.20f, new Vector3(0.01f, 0.50f, 0.40f));
            FlatThing(b.Paper, u, 1.50f, BedWall - HallSkin - 0.01f, f4 + 1.10f, new Vector3(0.42f, 0.52f, 0.01f), 3f);
            EstateCurtains(b, u, b.Blue, FrontWin0, FrontWin1, BedFrontIn, f4, BedHead, -1);

            // ---- 母の寝室（奥の寝室） ----
            EstateBed(b, u, b.Set, b.Blue, b.Set, 1.12f, 6.95f, f4, 1.45f, false);
            FlatThing(b.Set, u, 0.30f, 7.95f, f4, new Vector3(0.40f, 0.50f, 0.40f));
            FlatThing(b.Frame, u, 0.30f, 7.95f, f4 + 0.50f, new Vector3(0.18f, 0.28f, 0.18f));
            FlatThing(b.Frame, u, 0.40f, 4.50f, f4, new Vector3(0.60f, 2.00f, 1.60f));
            FlatThing(b.Shade, u, 0.71f, 4.50f, f4 + 0.10f, new Vector3(0.01f, 1.80f, 0.01f));
            FlatThing(b.Set, u, 4.10f, 8.12f, f4, new Vector3(1.10f, 0.80f, 0.42f));
            for (var i = 0; i < 3; i++)
                b.Shade.FaceZ(FlatZ(7.905f), FlatWest + 3.60f, FlatWest + 4.60f, f4 + 0.25f + i * 0.22f, f4 + 0.27f + i * 0.22f, 1);
            FlatThing(b.Yellow, u, 2.80f, 4.00f, f4, new Vector3(0.45f, 0.45f, 0.35f));
            FlatThing(b.Linen, u, 2.80f, 4.00f, f4 + 0.45f, new Vector3(0.40f, 0.08f, 0.30f));
            EstateChair(b, b.Frame, b.Frame, u, 2.60f, 7.80f, f4, 300f);
            FlatThing(b.Red, u, 2.60f, 7.80f, f4 + 0.49f, new Vector3(0.36f, 0.06f, 0.36f), 12f);
            EstateCurtains(b, u, b.Yellow, RearWin0, RearWin1, FlatBackIn, f4, BedHead, 1);
            EstateCurtains(b, u, b.Yellow, RearWin2, RearWin3, FlatBackIn, f4, BedHead, 1);

            // ---- 浴室 ----
            EstateBath(b, u, b.Frame, b.Blue, b.Yellow);
        }

        // ---- B（老夫婦ジョルジョとエレナ） --------------------------------------

        /// <summary>
        /// B の家。三階の玄関・台所・食卓・居間と、四階の夫婦の寝室・裁縫の部屋・浴室。
        ///
        /// **居間の家具は暖炉を向く。** 肘掛け椅子を二つ並べ、暖炉の隅にテレビ。
        /// 西の壁のサイドボードに家族の写真を並べ、その上に時計。窓は下までレースで覆う
        /// </summary>
        static void EstateRoom(EstateBanks b)
        {
            const int u = 1;
            const float f3 = EstateTop;
            const float f4 = EstateUpper;

            // ---- 玄関 ----
            FlatThing(b.Moss, u, DoorAt, 0.60f, f3, new Vector3(0.80f, 0.015f, 0.50f));
            EstateHooks(b, u, 0.80f);
            FlatThing(b.Soft, u, LaneWest + 0.10f, 0.58f, f3 + 0.62f, new Vector3(0.17f, 1.00f, 0.42f));
            FlatThing(b.Red, u, LaneWest + 0.10f, 1.02f, f3 + 0.70f, new Vector3(0.16f, 0.92f, 0.38f));
            FlatThing(b.Shade, u, LaneWest + 0.09f, 0.80f, f3 + 1.64f, new Vector3(0.18f, 0.07f, 0.24f));
            // 傘立てと杖
            FlatThing(b.Gear, u, 3.52f, 0.40f, f3, new Vector3(0.22f, 0.45f, 0.22f));
            FlatThing(b.Shade, u, 3.50f, 0.38f, f3 + 0.20f, new Vector3(0.04f, 0.62f, 0.04f));
            FlatThing(b.Set, u, 3.56f, 0.44f, f3 + 0.10f, new Vector3(0.03f, 0.84f, 0.03f), 0f);
            // 玄関の小卓と花瓶、壁の鏡。戸口のすぐ東、階段の上り口の手前
            FlatThing(b.Set, u, 5.15f, 0.55f, f3, new Vector3(0.28f, 0.80f, 0.50f));
            FlatThing(b.Blue, u, 5.15f, 0.48f, f3 + 0.80f, new Vector3(0.10f, 0.20f, 0.10f));
            FlatThing(b.Yellow, u, 5.15f, 0.48f, f3 + 1.00f, new Vector3(0.20f, 0.14f, 0.20f));
            FlatThing(b.Gear, u, 5.15f, 0.68f, f3 + 0.80f, new Vector3(0.14f, 0.02f, 0.10f));
            FlatThing(b.Set, u, FlatInner - 0.015f, 0.55f, f3 + 1.15f, new Vector3(0.03f, 0.62f, 0.46f));
            FlatThing(b.Glaze, u, FlatInner - 0.035f, 0.55f, f3 + 1.19f, new Vector3(0.01f, 0.54f, 0.38f));
            // 廊下の細長い敷物と、壁の写真
            FlatThing(b.Red, u, (LaneWest + LaneEast) * 0.5f, 2.95f, f3, new Vector3(0.62f, 0.012f, 2.70f));
            EstateFrameOn(b, u, LaneWest + 0.01f, 2.60f, f3 + 1.45f, 0.36f, 0.28f, 1);
            EstateFrameOn(b, u, LaneWest + 0.01f, 3.20f, f3 + 1.50f, 0.28f, 0.22f, 1);

            // ---- 台所 ----
            // 緑の戸棚に白い天板。据え置きのコンロに直火のコーヒー沸かし
            EstateKitchenRun(b, u, b.Moss, b.Frame);
            FlatThing(b.Frame, u, 2.80f, 0.58f, f3, new Vector3(0.56f, 0.90f, 0.62f));
            FlatThing(b.Shade, u, 2.80f, 0.58f, f3 + 0.90f, new Vector3(0.48f, 0.012f, 0.52f));
            b.Shade.FaceZ(FlatZ(0.895f), FlatWest + FlatWide + 2.58f, FlatWest + FlatWide + 3.02f, f3 + 0.20f, f3 + 0.62f, -1);
            FlatThing(b.Gear, u, 2.70f, 0.52f, f3 + 0.91f, new Vector3(0.10f, 0.20f, 0.10f));
            FlatThing(b.Red, u, 2.80f, 0.90f, f3 + 0.40f, new Vector3(0.30f, 0.40f, 0.02f));
            // 冷蔵庫。西の並びの南の端
            FlatThing(b.Frame, u, 0.40f, 3.20f, f3, new Vector3(0.60f, 1.60f, 0.62f));
            FlatThing(b.Gear, u, 0.72f, 3.00f, f3 + 1.00f, new Vector3(0.03f, 0.36f, 0.03f));
            // ラジオ・パンの缶・菓子の缶・布巾
            FlatThing(b.Red, u, 0.40f, 1.25f, f3 + 0.88f, new Vector3(0.20f, 0.16f, 0.30f));
            FlatThing(b.Frame, u, 0.40f, 1.70f, f3 + 0.88f, new Vector3(0.24f, 0.20f, 0.34f));
            FlatThing(b.Red, u, 0.95f, 0.55f, f3 + 0.88f, new Vector3(0.20f, 0.10f, 0.20f));
            // 窓の下半分のレース
            FlatThing(b.Paper, u, (KitchenWin0 + KitchenWin1) * 0.5f, 0.17f, f3 + KitchenSill + 0.02f,
                new Vector3(KitchenWin1 - KitchenWin0 - 0.08f, 0.55f, 0.01f));
            // 仕切りの暦と、聖人の小さな絵
            FlatThing(b.Paper, u, KitchenWall - HallSkin - 0.01f, 2.95f, f3 + 1.10f, new Vector3(0.01f, 0.48f, 0.34f));
            EstateFrameOn(b, u, KitchenWall - HallSkin - 0.01f, 3.40f, f3 + 1.60f, 0.24f, 0.18f, -1);

            // ---- 食卓 ----
            // テーブルクロスを床近くまで垂らし、真ん中にレースの敷物と果物の鉢
            FlatThing(b.Frame, u, 1.60f, 4.50f, f3 + 0.36f, new Vector3(1.30f, 0.40f, 0.90f));
            FlatThing(b.Paper, u, 1.60f, 4.50f, f3 + 0.76f, new Vector3(0.52f, 0.006f, 0.52f));
            FlatThing(b.Yellow, u, 1.60f, 4.50f, f3 + 0.766f, new Vector3(0.24f, 0.08f, 0.24f));
            FlatThing(b.Red, u, 1.64f, 4.46f, f3 + 0.84f, new Vector3(0.08f, 0.08f, 0.08f));
            FlatThing(b.Blue, u, 1.20f, 4.40f, f3 + 0.766f, new Vector3(0.08f, 0.22f, 0.08f));
            FlatThing(b.Red, u, 1.20f, 4.40f, f3 + 0.99f, new Vector3(0.18f, 0.12f, 0.18f));
            for (var i = 0; i < 4; i++)
                FlatThing(b.Set, u, 1.60f + ((i & 1) == 0 ? -0.58f : 0.58f), 4.50f + ((i & 2) == 0 ? -0.38f : 0.38f), f3,
                    new Vector3(0.05f, 0.36f, 0.05f));
            EstateChair(b, b.Red, b.Set, u, 0.72f, 4.50f, f3, 90f);
            EstateChair(b, b.Red, b.Set, u, 2.48f, 4.50f, f3, 270f);
            EstateChair(b, b.Red, b.Set, u, 1.60f, 5.25f, f3, 0f);

            // ---- 居間 ----
            // 東の壁に暖炉。白い枠に木の炉棚、電気の火の赤い棒、上に鏡
            FlatThing(b.Frame, u, 5.13f, 6.20f, f3, new Vector3(0.34f, 1.12f, 1.40f));
            FlatThing(b.Shade, u, 4.955f, 6.20f, f3 + 0.10f, new Vector3(0.01f, 0.56f, 0.72f));
            for (var i = 0; i < 2; i++)
                FlatThing(b.Lit, u, 4.945f, 6.20f, f3 + 0.26f + i * 0.14f, new Vector3(0.01f, 0.03f, 0.54f));
            FlatThing(b.Frame, u, 4.84f, 6.20f, f3, new Vector3(0.26f, 0.04f, 1.50f));
            FlatThing(b.Set, u, 5.10f, 6.20f, f3 + 1.12f, new Vector3(0.44f, 0.05f, 1.62f));
            FlatThing(b.Set, u, FlatInner - 0.02f, 6.20f, f3 + 1.30f, new Vector3(0.03f, 0.70f, 0.96f));
            FlatThing(b.Glaze, u, FlatInner - 0.04f, 6.20f, f3 + 1.35f, new Vector3(0.01f, 0.60f, 0.86f));
            // 炉棚の上の写真と燭台
            FlatThing(b.Gear, u, 5.16f, 5.72f, f3 + 1.17f, new Vector3(0.03f, 0.22f, 0.17f));
            FlatThing(b.Paper, u, 5.14f, 5.72f, f3 + 1.19f, new Vector3(0.005f, 0.17f, 0.12f));
            FlatThing(b.Gear, u, 5.16f, 6.66f, f3 + 1.17f, new Vector3(0.03f, 0.18f, 0.22f));
            FlatThing(b.Paper, u, 5.14f, 6.66f, f3 + 1.19f, new Vector3(0.005f, 0.13f, 0.17f));
            for (var i = 0; i < 2; i++)
                FlatThing(b.Gear, u, 5.12f, 6.02f + i * 0.36f, f3 + 1.17f, new Vector3(0.05f, 0.24f, 0.05f));
            // テレビ。暖炉の南の隅の台に、薄い画面を一枚
            FlatThing(b.Set, u, 5.05f, 7.85f, f3, new Vector3(0.45f, 0.55f, 0.80f));
            FlatThing(b.Shade, u, 5.20f, 7.85f, f3 + 0.62f, new Vector3(0.05f, 0.52f, 0.90f));
            FlatThing(b.Paper, u, 5.00f, 7.60f, f3 + 0.55f, new Vector3(0.22f, 0.006f, 0.22f));
            // 肘掛け椅子を二つ。どちらも暖炉とテレビを向く
            EstateArmchair(b, b.Moss, u, 3.05f, 5.90f, f3, 90f);
            EstateArmchair(b, b.Moss, u, 3.05f, 7.30f, f3, 90f);
            // 小卓。レースの敷物、新聞、老眼鏡、茶碗
            FlatThing(b.Set, u, 3.95f, 6.60f, f3, new Vector3(0.50f, 0.44f, 0.60f));
            FlatThing(b.Frame, u, 3.95f, 6.60f, f3 + 0.44f, new Vector3(0.34f, 0.004f, 0.34f));
            FlatThing(b.Paper, u, 3.90f, 6.45f, f3 + 0.445f, new Vector3(0.30f, 0.02f, 0.22f), 12f);
            FlatThing(b.Shade, u, 4.05f, 6.72f, f3 + 0.445f, new Vector3(0.12f, 0.015f, 0.04f));
            FlatThing(b.Frame, u, 3.88f, 6.80f, f3 + 0.445f, new Vector3(0.12f, 0.07f, 0.12f));
            FlatThing(b.Red, u, 3.90f, 6.60f, f3, new Vector3(2.20f, 0.012f, 2.60f));
            // 西の壁のサイドボード。家族の写真を大きさを揃えずに並べ、レースを敷く
            FlatThing(b.Set, u, 0.33f, 6.40f, f3, new Vector3(0.46f, 0.82f, 1.80f));
            for (var i = 0; i < 3; i++)
                b.Shade.FaceX(FlatWest + FlatWide + 0.565f, FlatZ(7.25f - i * 0.58f + 0.01f), FlatZ(7.25f - i * 0.58f - 0.01f),
                    f3 + 0.08f, f3 + 0.72f, 1);
            FlatThing(b.Frame, u, 0.33f, 6.40f, f3 + 0.82f, new Vector3(0.32f, 0.006f, 1.50f));
            var heights = new[] { 0.26f, 0.20f, 0.32f, 0.18f, 0.24f };
            for (var i = 0; i < heights.Length; i++)
            {
                var d = 5.75f + i * 0.33f;
                FlatThing(b.Gear, u, 0.28f, d, f3 + 0.826f, new Vector3(0.03f, heights[i], heights[i] * 0.8f), (i - 2) * 6f);
                FlatThing(b.Paper, u, 0.30f, d, f3 + 0.846f, new Vector3(0.005f, heights[i] - 0.05f, heights[i] * 0.8f - 0.05f),
                    (i - 2) * 6f);
            }
            // 壁の時計。サイドボードの上。二人きりの家で音のする物はこれとテレビだけ
            FlatThing(b.Set, u, FlatIn + 0.02f, 6.40f, f3 + 1.42f, new Vector3(0.04f, 0.38f, 0.38f));
            FlatThing(b.Bright, u, FlatIn + 0.045f, 6.40f, f3 + 1.46f, new Vector3(0.01f, 0.30f, 0.30f));
            FlatThing(b.Shade, u, FlatIn + 0.052f, 6.40f, f3 + 1.60f, new Vector3(0.005f, 0.11f, 0.015f));
            FlatThing(b.Shade, u, FlatIn + 0.052f, 6.44f, f3 + 1.60f, new Vector3(0.005f, 0.015f, 0.08f));
            EstateFrameOn(b, u, FlatIn + 0.01f, 5.75f, f3 + 1.55f, 0.30f, 0.24f, 1);
            EstateFrameOn(b, u, FlatIn + 0.01f, 7.05f, f3 + 1.50f, 0.36f, 0.28f, 1);
            // 植木。窓辺の大きな鉢と、窓台の小さな鉢
            FlatThing(b.Red, u, 0.50f, 7.95f, f3, new Vector3(0.36f, 0.34f, 0.36f));
            FlatThing(b.Turf, u, 0.50f, 7.95f, f3 + 0.34f, new Vector3(0.62f, 0.78f, 0.62f));
            for (var i = 0; i < 3; i++)
            {
                var x = 1.30f + i * 1.10f;
                FlatThing(b.Red, u, x, 8.30f, f3 + LoungeSill, new Vector3(0.14f, 0.13f, 0.14f));
                FlatThing(b.Turf, u, x, 8.30f, f3 + LoungeSill + 0.13f, new Vector3(0.18f, 0.18f + (i % 2) * 0.10f, 0.16f));
            }
            // 窓を下まで覆うレースと、両脇の厚いカーテン。背の高い灯り
            FlatThing(b.Paper, u, (LoungeWin0 + LoungeWin1) * 0.5f, 8.19f, f3 + LoungeSill + 0.02f,
                new Vector3(LoungeWin1 - LoungeWin0 - 0.10f, LoungeHead - LoungeSill - 0.36f, 0.01f));
            EstateCurtains(b, u, b.Moss, LoungeWin0, LoungeWin1, FlatBackIn, f3, LoungeHead, 1);
            FlatThing(b.Gear, u, 2.50f, 8.00f, f3, new Vector3(0.04f, 1.45f, 0.04f));
            FlatThing(b.Yellow, u, 2.50f, 8.00f, f3 + 1.36f, new Vector3(0.38f, 0.30f, 0.38f));

            // ---- 裁縫の部屋（表の寝室） ----
            EstateBed(b, u, b.Set, b.Red, b.Set, 0.55f, -0.72f, f4, 0.92f, true);
            FlatThing(b.Set, u, 5.00f, -1.10f, f4, new Vector3(0.58f, 1.90f, 1.20f));
            FlatThing(b.Soft, u, 5.00f, -1.10f, f4 + 1.90f, new Vector3(0.44f, 0.20f, 0.70f));
            FlatThing(b.Set, u, 2.80f, 0.55f, f4, new Vector3(0.90f, 0.72f, 0.50f));
            FlatThing(b.Frame, u, 2.80f, 0.55f, f4 + 0.72f, new Vector3(0.42f, 0.28f, 0.20f));
            FlatThing(b.Blue, u, 2.45f, 0.50f, f4 + 0.72f, new Vector3(0.10f, 0.10f, 0.10f));
            EstateChair(b, b.Red, b.Set, u, 2.80f, 0.02f, f4, 180f);
            // 布地の箱。戸の板を寄せる東の脇は空けておく
            for (var i = 0; i < 2; i++)
                FlatThing(b.Paper, u, 4.80f, 0.65f, f4 + i * 0.36f, new Vector3(0.52f - i * 0.06f, 0.36f, 0.44f), i * 8f);
            EstateCurtains(b, u, b.Red, FrontWin0, FrontWin1, BedFrontIn, f4, BedHead, -1);

            // ---- 夫婦の寝室（奥の寝室） ----
            EstateBed(b, u, b.Set, b.Frame, b.Set, 1.12f, 6.95f, f4, 1.45f, false);
            FlatThing(b.Red, u, 1.85f, 6.95f, f4 + 0.47f, new Vector3(0.42f, 0.06f, 1.46f));
            for (var i = 0; i < 2; i++)
            {
                var d = i == 0 ? 5.95f : 7.95f;
                FlatThing(b.Set, u, 0.30f, d, f4, new Vector3(0.40f, 0.52f, 0.40f));
                FlatThing(b.Yellow, u, 0.30f, d, f4 + 0.52f, new Vector3(0.22f, 0.26f, 0.22f));
            }
            FlatThing(b.Set, u, 0.42f, 4.50f, f4, new Vector3(0.62f, 2.00f, 1.70f));
            FlatThing(b.Shade, u, 0.735f, 4.50f, f4 + 0.10f, new Vector3(0.01f, 1.80f, 0.01f));
            FlatThing(b.Set, u, 5.05f, 7.20f, f4, new Vector3(0.45f, 0.75f, 1.00f));
            FlatThing(b.Set, u, FlatInner - 0.02f, 7.20f, f4 + 0.85f, new Vector3(0.03f, 0.74f, 0.64f));
            FlatThing(b.Glaze, u, FlatInner - 0.04f, 7.20f, f4 + 0.90f, new Vector3(0.01f, 0.64f, 0.54f));
            FlatThing(b.Frame, u, 5.00f, 7.00f, f4 + 0.75f, new Vector3(0.20f, 0.004f, 0.20f));
            FlatThing(b.Red, u, 2.20f, 6.40f, f4, new Vector3(0.10f, 0.06f, 0.24f));
            FlatThing(b.Red, u, 2.20f, 6.62f, f4, new Vector3(0.10f, 0.06f, 0.24f));
            EstateChair(b, b.Set, b.Set, u, 2.80f, 4.10f, f4, 200f);
            FlatThing(b.Moss, u, 2.80f, 4.10f, f4 + 0.49f, new Vector3(0.36f, 0.08f, 0.36f), 20f);
            EstateCurtains(b, u, b.Red, RearWin0, RearWin1, FlatBackIn, f4, BedHead, 1);
            EstateCurtains(b, u, b.Red, RearWin2, RearWin3, FlatBackIn, f4, BedHead, 1);
            for (var i = 0; i < 2; i++)
            {
                var a = i == 0 ? RearWin0 : RearWin2;
                var z = i == 0 ? RearWin1 : RearWin3;
                FlatThing(b.Paper, u, (a + z) * 0.5f, 8.19f, f4 + BedSill + 0.02f,
                    new Vector3(z - a - 0.10f, BedHead - BedSill - 0.40f, 0.01f));
            }

            // ---- 浴室 ----
            // 緑の浴槽と洗面台と便器。年寄りの家なので、浴槽の脇に掴まる棒
            EstateBath(b, u, b.Moss, b.Frame, b.Red);
            FlatThing(b.Gear, u, FlatIn + 0.04f, 2.20f, f4 + 0.85f, new Vector3(0.04f, 0.04f, 0.70f));
        }

        // ---- 家具の道具 --------------------------------------------------------

        /// <summary>
        /// 向きを持つ家具の部品を一つ。(u, d) が家具の真ん中、<paramref name="yaw"/> が家具の前の向き
        /// （0 が北 = デッキの側、90 が東）。<paramref name="local"/> は家具の中の位置で、
        /// x が前を向いて右、y が床から、z が前
        /// </summary>
        static void EstateAt(Bank bank, int unit, float u, float d, float floor, float yaw, Vector3 local, Vector3 size)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var at = new Vector3(FlatWest + FlatWide * unit + u, floor, FlatZ(d));
            bank.Box(at + rot * local, size, rot);
        }

        /// <summary>椅子ひとつ。座と背と、左右の脚の板。<paramref name="grow"/> で子ども用に縮める</summary>
        static void EstateChair(EstateBanks b, Bank seat, Bank frame, int unit, float u, float d, float floor, float yaw,
            float grow = 1f)
        {
            EstateAt(seat, unit, u, d, floor, yaw, new Vector3(0f, 0.465f, 0f) * grow, new Vector3(0.42f, 0.05f, 0.42f) * grow);
            EstateAt(frame, unit, u, d, floor, yaw, new Vector3(0f, 0.72f, -0.19f) * grow, new Vector3(0.42f, 0.46f, 0.04f) * grow);
            for (var i = 0; i < 2; i++)
                EstateAt(frame, unit, u, d, floor, yaw, new Vector3(i == 0 ? -0.18f : 0.18f, 0.22f, 0f) * grow,
                    new Vector3(0.04f, 0.44f, 0.38f) * grow);
        }

        /// <summary>
        /// 肘掛け椅子。厚い座と高い背、左右の肘。背と肘にレースの敷物を掛ける
        /// </summary>
        static void EstateArmchair(EstateBanks b, Bank cloth, int unit, float u, float d, float floor, float yaw)
        {
            EstateAt(cloth, unit, u, d, floor, yaw, new Vector3(0f, 0.24f, 0.04f), new Vector3(0.60f, 0.48f, 0.66f));
            EstateAt(cloth, unit, u, d, floor, yaw, new Vector3(0f, 0.56f, -0.32f), new Vector3(0.84f, 1.02f, 0.18f));
            for (var i = 0; i < 2; i++)
            {
                var x = i == 0 ? -0.35f : 0.35f;
                EstateAt(cloth, unit, u, d, floor, yaw, new Vector3(x, 0.32f, 0.02f), new Vector3(0.14f, 0.64f, 0.76f));
                EstateAt(b.Frame, unit, u, d, floor, yaw, new Vector3(x, 0.645f, 0.14f), new Vector3(0.15f, 0.01f, 0.24f));
            }
            EstateAt(b.Frame, unit, u, d, floor, yaw, new Vector3(0f, 0.92f, -0.225f), new Vector3(0.42f, 0.24f, 0.01f));
        }

        /// <summary>
        /// 寝台。枠・布団・枕・頭板。頭は北（<paramref name="single"/> が真、表の寝室）か西（奥の寝室）へ寄せる
        /// </summary>
        static void EstateBed(EstateBanks b, int unit, Bank frame, Bank cover, Bank head, float u, float d, float floor,
            float wide, bool single)
        {
            if (single)
            {
                FlatThing(frame, unit, u, d, floor, new Vector3(wide, 0.30f, 1.95f));
                FlatThing(cover, unit, u + 0.01f, d + 0.16f, floor + 0.30f, new Vector3(wide + 0.02f, 0.14f, 1.62f));
                FlatThing(b.Frame, unit, u, d - 0.72f, floor + 0.30f, new Vector3(wide * 0.66f, 0.12f, 0.34f));
                FlatThing(head, unit, u, d - 1.00f, floor, new Vector3(wide + 0.04f, 0.85f, 0.05f));
            }
            else
            {
                FlatThing(frame, unit, u, d, floor, new Vector3(2.00f, 0.32f, wide));
                FlatThing(cover, unit, u + 0.12f, d, floor + 0.32f, new Vector3(1.78f, 0.14f, wide + 0.03f));
                for (var i = 0; i < 2; i++)
                    FlatThing(b.Frame, unit, u - 0.76f, d + (i == 0 ? -0.35f : 0.35f), floor + 0.32f,
                        new Vector3(0.34f, 0.12f, wide * 0.38f));
                FlatThing(head, unit, u - 0.99f, d, floor, new Vector3(0.05f, 0.95f, wide + 0.05f));
            }
        }

        /// <summary>
        /// 台所の造り付け。デッキ側の窓の下の並び（流し）と、西の壁の並び（コンロ）、吊り戸棚
        /// </summary>
        static void EstateKitchenRun(EstateBanks b, int unit, Bank doors, Bank top)
        {
            const float f3 = EstateTop;
            var ox = FlatWest + FlatWide * unit;
            // 窓の下の並び。冷蔵庫（またはコンロ）の手前で止める
            FlatThing(doors, unit, 1.30f, 0.56f, f3, new Vector3(2.40f, 0.86f, 0.60f));
            FlatThing(top, unit, 1.30f, 0.57f, f3 + 0.86f, new Vector3(2.42f, 0.04f, 0.64f));
            for (var i = 0; i < 3; i++)
                b.Shade.FaceZ(FlatZ(0.865f), ox + 0.68f + i * 0.60f, ox + 0.70f + i * 0.60f, f3 + 0.08f, f3 + 0.80f, -1);
            // 流しと蛇口
            FlatThing(b.Gear, unit, (KitchenWin0 + KitchenWin1) * 0.5f, 0.55f, f3 + 0.90f, new Vector3(0.55f, 0.012f, 0.42f));
            FlatThing(b.Gear, unit, (KitchenWin0 + KitchenWin1) * 0.5f, 0.32f, f3 + 0.90f, new Vector3(0.04f, 0.24f, 0.04f));
            // 西の壁の並び。コンロと天火
            FlatThing(doors, unit, 0.40f, 1.80f, f3, new Vector3(0.60f, 0.86f, 1.90f));
            FlatThing(top, unit, 0.41f, 1.80f, f3 + 0.86f, new Vector3(0.62f, 0.04f, 1.90f));
            FlatThing(b.Shade, unit, 0.40f, 2.25f, f3 + 0.90f, new Vector3(0.50f, 0.012f, 0.52f));
            for (var i = 0; i < 4; i++)
                FlatThing(b.Gear, unit, 0.28f + (i & 1) * 0.24f, 2.12f + (i >> 1) * 0.26f, f3 + 0.912f,
                    new Vector3(0.14f, 0.006f, 0.14f));
            b.Shade.FaceX(ox + 0.705f, FlatZ(2.50f), FlatZ(2.00f), f3 + 0.12f, f3 + 0.66f, 1);
            FlatThing(b.Gear, unit, 0.72f, 2.25f, f3 + 0.70f, new Vector3(0.03f, 0.03f, 0.40f));
            // 吊り戸棚と換気のフード
            FlatThing(doors, unit, 0.27f, 1.60f, f3 + 1.45f, new Vector3(0.34f, 0.68f, 1.50f));
            FlatThing(b.Gear, unit, 0.30f, 2.25f, f3 + 1.55f, new Vector3(0.40f, 0.14f, 0.55f));
        }

        /// <summary>食卓。天板と四本の脚</summary>
        static void EstateTable(EstateBanks b, int unit, Bank top, float u, float d, float wide, float deep)
        {
            const float f3 = EstateTop;
            FlatThing(top, unit, u, d, f3 + 0.70f, new Vector3(wide, 0.04f, deep));
            for (var i = 0; i < 4; i++)
                FlatThing(b.Gear, unit, u + ((i & 1) == 0 ? -1f : 1f) * (wide * 0.5f - 0.06f),
                    d + ((i & 2) == 0 ? -1f : 1f) * (deep * 0.5f - 0.06f), f3, new Vector3(0.04f, 0.70f, 0.04f));
        }

        /// <summary>
        /// 玄関のコート掛け。玄関の西の壁（台所との仕切り）の、戸口の脇の高さ 1.62 m に木の板と鉤
        /// </summary>
        static void EstateHooks(EstateBanks b, int unit, float d)
        {
            FlatThing(b.Set, unit, LaneWest + 0.015f, d, EstateTop + 1.60f, new Vector3(0.03f, 0.08f, 0.86f));
            for (var i = 0; i < 4; i++)
                FlatThing(b.Gear, unit, LaneWest + 0.05f, d - 0.33f + i * 0.22f, EstateTop + 1.62f,
                    new Vector3(0.06f, 0.03f, 0.02f));
        }

        /// <summary>
        /// 壁の額。x が一定の壁に掛ける。<paramref name="facing"/> は額の表の向き（x の符号）
        /// </summary>
        static void EstateFrameOn(EstateBanks b, int unit, float u, float d, float y, float high, float wide, int facing)
        {
            FlatThing(b.Gear, unit, u + facing * 0.015f, d, y - high * 0.5f, new Vector3(0.03f, high, wide));
            FlatThing(b.Paper, unit, u + facing * 0.032f, d, y - high * 0.5f + 0.04f,
                new Vector3(0.005f, high - 0.08f, wide - 0.08f));
        }

        /// <summary>
        /// 窓の両脇に寄せたカーテンと、上の棒。<paramref name="inward"/> は部屋の側の向き（d の符号）
        /// </summary>
        static void EstateCurtains(EstateBanks b, int unit, Bank cloth, float u0, float u1, float wallD, float floor,
            float head, int inward)
        {
            var d = wallD - inward * 0.08f;
            FlatThing(b.Gear, unit, (u0 + u1) * 0.5f, d, floor + head + 0.06f, new Vector3(u1 - u0 + 0.50f, 0.03f, 0.03f));
            for (var i = 0; i < 2; i++)
                FlatThing(cloth, unit, i == 0 ? u0 - 0.10f : u1 + 0.10f, d, floor + 0.05f,
                    new Vector3(0.36f, head + 0.01f, 0.07f));
        }

        /// <summary>
        /// 浴室。西の壁の浴槽、南の仕切りの洗面台と鏡、便器、浴槽の脇の陶板、足拭き、手拭い
        /// </summary>
        static void EstateBath(EstateBanks b, int unit, Bank suite, Bank towel, Bank mat)
        {
            const float f4 = EstateUpper;
            var ox = FlatWest + FlatWide * unit;
            // 浴槽の周りの壁の陶板。腰から肩の高さまで
            b.Tile.FaceX(ox + FlatIn + 0.005f, FlatZ(BathWall - HallSkin), FlatZ(BedWall + HallSkin), f4 + 0.55f, f4 + 1.45f, 1);
            FlatThing(suite, unit, 0.47f, 2.20f, f4, new Vector3(0.74f, 0.56f, 1.72f));
            FlatThing(b.Tile, unit, 0.47f, 2.20f, f4 + 0.50f, new Vector3(0.56f, 0.06f, 1.52f));
            FlatThing(b.Gear, unit, 0.20f, 3.00f, f4 + 0.62f, new Vector3(0.10f, 0.05f, 0.04f));
            // 洗面台と鏡
            FlatThing(suite, unit, 1.90f, 3.28f, f4, new Vector3(0.16f, 0.74f, 0.16f));
            FlatThing(suite, unit, 1.90f, 3.19f, f4 + 0.72f, new Vector3(0.54f, 0.14f, 0.38f));
            FlatThing(b.Gear, unit, 1.90f, 3.33f, f4 + 0.86f, new Vector3(0.04f, 0.12f, 0.04f));
            FlatThing(b.Glaze, unit, 1.90f, BathWall - HallSkin - 0.012f, f4 + 1.05f, new Vector3(0.50f, 0.60f, 0.02f));
            // 便器と水槽
            FlatThing(suite, unit, 2.80f, 3.08f, f4, new Vector3(0.38f, 0.42f, 0.55f));
            FlatThing(suite, unit, 2.80f, 3.32f, f4 + 0.42f, new Vector3(0.40f, 0.36f, 0.14f));
            // 手拭い掛け。表の寝室との仕切りの面。廊下との仕切りには戸口が開いている
            FlatThing(b.Gear, unit, 1.50f, BedWall + HallSkin + 0.05f, f4 + 1.10f, new Vector3(0.56f, 0.03f, 0.03f));
            FlatThing(towel, unit, 1.37f, BedWall + HallSkin + 0.07f, f4 + 0.62f, new Vector3(0.26f, 0.50f, 0.04f));
            FlatThing(mat, unit, 1.65f, BedWall + HallSkin + 0.07f, f4 + 0.72f, new Vector3(0.22f, 0.40f, 0.04f));
            // 足拭き
            FlatThing(mat, unit, 1.10f, 2.20f, f4, new Vector3(0.50f, 0.012f, 0.80f));
        }
    }
}
