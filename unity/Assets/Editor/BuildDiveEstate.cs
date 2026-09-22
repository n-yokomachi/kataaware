using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の団地。
    ///
    /// **一目でどこか分かることを条件に組む。** 記憶は他人の頭から抜いてきた絵なので
    /// 隅々まで見えている必要はないが、床と壁だけの箱では何の場所か読めない。
    /// ここで団地だと言わせているのは三つ。**外階段の段鼻の線**、**等間隔に並ぶ戸口**、
    /// **隣の棟の窓の列**。どれも等間隔の繰り返しで、細かい小物より先に目へ入る。
    ///
    /// **その三つの上に密度を重ねる。** 一層ぶんの廊下に戸が三つ並ぶだけでは
    /// 事務所の通路と見分けが付かないので、廊下を三層重ね、共用部（消火器・掲示板・
    /// 配電盤・非常灯）と各戸の暮らしの気配（傘立て・植木鉢・自転車・新聞）を置く。
    ///
    /// **住戸は戸の内側にまず玄関がある。** 戸を開けて居間が丸見えになるのは
    /// 集合住宅の住戸ではないので、三和土・上がり框・下駄箱・短い廊下を通してから居間へ入る。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 人と主の立ち位置になる点は const にして両方から見る。
    ///
    /// 道具の名前は <c>Estate</c> で始める。五つの場所が同じ partial class を分け合っていて、
    /// <c>Bar</c> のような短い名前は他の場所と衝突する
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 団地 ------------------------------------------------------------
        //
        // 外階段は半階ごとに折り返す。集合住宅の階段は一階ぶんを一息に上がる形ではなく、
        // 半階上がって踊り場で 180 度向きを変え、また半階上がる。三階まで六本。
        //
        // **折り返しの向きは廊下が決めている。** 階の高さに来る踊り場はそのまま廊下なので、
        // 一本おきに建物の面の側へ戻ってくる。地面から入る口も、だから面の側にある。
        // 東の一本が上り始め、西の一本が上り終わり。二本のあいだには中壁が立つ。
        // 三階の廊下に戸口が三つ並び、左が記憶 0・1 の家、真ん中が記憶 8・15 の家

        /// <summary>階の高さ。m</summary>
        public const float Floor = 2.8f;
        /// <summary>半階ぶんの階段が z へ伸びる長さ。1.4 m 上がって 2.1 m 進むので勾配は 34 度</summary>
        const float Flight = 2.1f;
        /// <summary>半階ぶんの段数。蹴上げ 17.5 cm・踏み面 26 cm で、実際の集合住宅の階段に近い</summary>
        const int StepCount = 8;
        /// <summary>廊下の奥行き。階の高さの踊り場でもある</summary>
        const float LandingDeep = 1.4f;
        /// <summary>折り返しの踊り場の奥行き。階段一本の幅より広くないと向きが変えられない</summary>
        const float StairTurn = 1.3f;
        /// <summary>階段一本の幅</summary>
        const float StairWide = 1.1f;
        /// <summary>二本のあいだの中壁。厚みの半分。ここが抜けていると隣の一本へ落ちる</summary>
        const float StairSpine = 0.1f;
        /// <summary>階段の井戸の西の端。建物の西の面と揃える</summary>
        const float StairWest = -1.2f;
        /// <summary>階段の井戸の東の端</summary>
        const float StairEast = StairWest + StairWide * 2f + StairSpine * 2f;              // 1.2
        /// <summary>西の一本の真ん中。上りの終わりはいつもこちら</summary>
        const float StairWestMid = StairWest + StairWide * 0.5f;                           // -0.65
        /// <summary>東の一本の真ん中。上りの始まりはいつもこちら</summary>
        const float StairEastMid = StairEast - StairWide * 0.5f;                           // 0.65

        /// <summary>三階の廊下の真ん中</summary>
        public const float EstateWalk = -14.1f;
        /// <summary>建物の面。戸口はここに開く</summary>
        public const float EstateFace = EstateWalk - LandingDeep * 0.5f;                   // -14.8
        /// <summary>三階の高さ</summary>
        public const float EstateTop = Floor * 3f;                                         // 8.4
        /// <summary>折り返しの踊り場の北の縁。階段が廊下から南へ出る線（<see cref="WalkFront"/>）から一本ぶん</summary>
        const float StairTurnFar = EstateWalk + LandingDeep * 0.5f + Flight;               // -11.3
        /// <summary>折り返しの踊り場の真ん中</summary>
        public const float EstateTurn = StairTurnFar + StairTurn * 0.5f;                   // -10.65
        /// <summary>階段の井戸の南の端</summary>
        const float StairEnd = StairTurnFar + StairTurn;                                   // -10.0
        /// <summary>
        /// 階の高さの踊り場。折り返しなので廊下と同じ z に来る。
        /// 記憶 0 の鍵打ちが見ているが、いまは先頭の一打しか使われていない
        /// </summary>
        public const float EstateLanding1 = EstateWalk;
        /// <summary>二つ目。同じ井戸の同じ側へ戻るので z も同じ</summary>
        public const float EstateLanding2 = EstateWalk;

        /// <summary>左の戸口の真ん中。記憶 0 の母と、記憶 1 のハンナの家</summary>
        public const float DoorA = 1.6f;
        /// <summary>真ん中の戸口。記憶 8 のジョルジョと、記憶 15 のエレナの家</summary>
        public const float DoorB = 4.2f;
        /// <summary>
        /// 右の戸口。A と B の間隔をそのまま伸ばす。
        /// 戸口が二つでは並びに見えず、三つ目が入って初めて等間隔の列として読める。
        /// 中は作らないので、ここだけ戸が閉まっている
        /// </summary>
        public const float DoorC = DoorB + (DoorB - DoorA);                                // 6.8
        const float DoorHalf = 0.45f;
        const float DoorHigh = 2.0f;

        /// <summary>廊下の右端。三つ目の戸口のぶんだけ、元より伸びている</summary>
        const float WalkRight = 7.8f;
        /// <summary>廊下の左端。階段の井戸まで伸ばす。折り返しの踊り場が廊下そのものだから</summary>
        const float WalkLeft = StairWest;                                                  // -1.2
        /// <summary>建物の面の右端</summary>
        const float WallRight = 8.1f;
        /// <summary>廊下の手すり側の縁。階段の井戸が南へ出る線でもある</summary>
        const float WalkFront = EstateWalk + LandingDeep * 0.5f;                           // -13.4
        /// <summary>廊下の天井の下面。四階の床でもある</summary>
        const float WalkRoof = EstateTop + 2.3f;

        /// <summary>右の家の間口と奥行き</summary>
        const float RoomX0 = 3.0f;
        const float RoomX1 = 7.0f;
        const float RoomBack = -18.6f;
        const float RoomRoof = EstateTop + 2.5f;

        /// <summary>隣の棟の面。窓の列はここに並ぶ</summary>
        const float BlockFace = 9.5f;

        /// <summary>右の家の中。テレビの前</summary>
        public static readonly Vector3 EstateTv = new Vector3(5.3f, EstateTop + 0.85f, -18.3f);

        // ---- 玄関 --------------------------------------------------------------
        //
        // 三和土は床より下げる。**下げる側を選んだのは、鍵打ちが床を EstateTop で見ているため。**
        // 居間の側を持ち上げると、中を歩く鍵打ちの目の高さがそのぶん低くなる

        /// <summary>三和土が床より下がる量</summary>
        const float EstateSunk = 0.15f;
        /// <summary>上がり框の z。三和土はここから戸口まで</summary>
        const float HallSill = -15.55f;
        /// <summary>玄関と居間を仕切る壁の真ん中</summary>
        const float HallWall = -15.90f;
        /// <summary>仕切りの厚みの半分</summary>
        const float HallSkin = 0.06f;
        /// <summary>
        /// 居間への抜けの両端。
        ///
        /// **戸口の正面から外して西へ寄せる。** 抜けが戸口の真正面にあると、
        /// 廊下に立ったまま居間の奥まで見通せてしまう。
        /// 幅は、記憶 8 の主と記憶 15 の夫がここを通るので、二人の通り道を含めた分だけ取る
        /// </summary>
        const float HallGap0 = 3.30f;
        const float HallGap1 = 4.55f;
        /// <summary>三和土の東端</summary>
        const float HallDoma = 4.70f;
        /// <summary>玄関の東の壁。この奥は物入れで、中は作らない</summary>
        const float HallEast = 5.15f;
        /// <summary>抜けの高さ。垂れ壁がここから天井まで</summary>
        const float HallHead = 1.98f;

        /// <summary>左の戸口の奥の窪み</summary>
        const float PorchX0 = DoorA - 0.75f;
        const float PorchX1 = DoorA + 0.75f;
        const float PorchBack = -16.4f;
        /// <summary>左の戸口の上がり框</summary>
        const float PorchSill = -15.60f;

        /// <summary>
        /// 素材ごとの入れ物をひとまとめに。
        ///
        /// 団地は置く物が多く、入れ物を一つずつ引数で持ち回ると、
        /// 道具を増やすたびに呼ぶ側と受ける側で数が合わなくなる
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
        }


        // ---- 団地の外階段 ------------------------------------------------------

        /// <summary>
        /// 記憶 0・1・8・15 の舞台。外階段と三層ぶんの廊下、右の家の中まで。
        ///
        /// 左の家（戸口 A）は居間まで作らず、玄関の土間と下駄箱、その奥に灯りだけを置く。
        /// 記憶 0 が振り返ったときに母が逆光の影になるのは、この奥の灯りのため
        /// </summary>
        static void Estate(Transform place)
        {
            // 素材ごとに一つずつ。溜めてから最後に一枚へ焼く
            var crete = EstatePaint("EstateCrete", new Color(0.400f, 0.394f, 0.372f), 0.06f);
            var skin = EstatePaint("EstateSkin", new Color(0.208f, 0.204f, 0.196f), 0.05f);
            var board = EstatePaint("EstateBoard", new Color(0.300f, 0.268f, 0.205f), 0.06f);
            // 三和土は廊下のコンクリートより暗く、少しだけ艶がある。
            // 同じ色にすると、戸を跨いで下りた段差が絵の上で消える
            var tiles = EstatePaint("EstateTile", new Color(0.176f, 0.170f, 0.162f), 0.18f);
            var sheet = EstatePaint("EstatePaper", new Color(0.520f, 0.512f, 0.492f), 0.04f);
            var rust = EstatePaint("EstateRed", new Color(0.330f, 0.086f, 0.070f), 0.20f);
            var pale = Glow("EstatePale", new Color(0.86f, 0.87f, 0.90f), 0.62f);
            var bulbs = Glow("EstateBulb", new Color(1f, 0.93f, 0.80f), 1.50f);
            var exits = Glow("EstateExit", new Color(0.34f, 0.92f, 0.50f), 0.85f);
            var haze = Glow("EstateFar", new Color(0.62f, 0.64f, 0.70f), 0.46f);

            var b = new EstateBanks();
            EstateShell(b);
            EstateLower(b);
            EstateCommon(b);
            EstateLives(b);
            EstateGear(b);
            EstateYard(b);
            EstateRoom(b);
            EstatePorch(b);
            EstateDoorway(b, DoorA, true);
            EstateDoorway(b, DoorB, true);
            EstateDoorway(b, DoorC, false);
            EstateBlockWall(place, b);

            EstateEmit(place, "EstateFloor", b.Slab, crete, true);
            EstateEmit(place, "EstateWall", b.Wall, skin, true);
            EstateEmit(place, "EstateRail", b.Rail, Mat("Rail"), true);
            EstateEmit(place, "EstateRoomFloor", b.Board, board, true);
            EstateEmit(place, "EstateDoma", b.Tile, tiles, true);
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

            // 戸口の奥とテレビ。灯りではなく光る面で済ませる。
            // 記憶 0 の母も記憶 15 の夫も、この光の手前に立つので影にしか見えない
            Pane(place, "EstateHallLight", new Vector3(DoorA, EstateTop + 1.1f, PorchBack + 0.05f),
                new Vector2(1.4f, 2.1f), Vector3.forward, Glow("GlowHall", new Color(1f, 0.90f, 0.74f), 0.55f));
            Pane(place, "EstateTv", EstateTv + new Vector3(0f, 0f, 0.06f),
                new Vector2(0.72f, 0.44f), Vector3.forward, Glow("GlowTv", new Color(0.72f, 0.82f, 1f), 1.35f));
            // 部屋の蛍光灯。廊下の電球と同じ色にして、この場所の灯りを一色に揃える
            Pane(place, "EstateTube", new Vector3(5.0f, RoomRoof - 0.12f, -17.3f),
                new Vector2(1.15f, 0.2f), Vector3.down, bulbs);
            // 玄関の灯り。ここが暗いままだと、戸口から覗いた絵が黒い穴になる
            Pane(place, "EstateHallBulb", new Vector3(4.0f, RoomRoof - 0.10f, -15.2f),
                new Vector2(0.60f, 0.40f), Vector3.down, bulbs);

            // 地面の端から先は虚空なので、見えない仕切りで囲う。
            // 建物の中まで歩ける必要は無いが、裏へ回れても困らないので地面ごと囲う
            Ring(place, "EstateFence", new Vector2(-13.6f, 24.6f), new Vector2(-19.6f, BlockFace - 0.5f), 3.5f);
            // 階段と廊下の外側は手すりが受け持つ。EstateRail に当たりを入れてあるので、
            // ここへ仕切りを重ねると階段を上がってきた先が塞がる。
            // **西の端には何も重ねない。** 折り返しの階段はここから廊下へ出てくるので、
            // 落ちる先を閉じているのは井戸の西の壁のほう
            //
            // 東の端は戸境の壁が閉じているが、壁は面が二枚きりで、
            // 走り込んだときに抜けることがある。仕切りを重ねて押さえる
            Fence(place, "EstateWalkEndB", new Vector3(WalkRight + 0.1f, EstateTop + 0.75f, EstateWalk),
                new Vector3(0.2f, 1.5f, LandingDeep));

            EstateLamps(place);
        }

        /// <summary>
        /// 灯り。朝の日射しと、廊下の電球と、部屋の中。
        ///
        /// 数を増やすと WebGL で持たないので、点は一階ぶんの廊下と部屋の中だけに置き、
        /// 下の階の廊下は光る面で済ませる
        /// </summary>
        static void EstateLamps(Transform place)
        {
            // 朝の低い日射し。階段の側から当てるので、三階の戸口に立つ人は逆光になる
            var sun = Lamp(place, "Morning", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(30f, -148f, 0f), new Color(1f, 0.95f, 0.86f), 2.0f, 10f);
            sun.shadows = LightShadows.Soft;
            // 日の当たらない側の下限。環境光は五つの場所で分け合っていて、ここだけ上げられない。
            // これが無いと、日陰へ入った途端に人の輪郭しか見えなくなる
            Lamp(place, "Fill", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(20f, 22f, 0f), new Color(0.66f, 0.72f, 0.86f), 0.55f, 10f);
            // 廊下の裸電球。3 m 間隔で、灯りの下だけ床が明るい
            for (var i = 0; i < 3; i++)
                Lamp(place, "Bulb" + i, LightType.Point, new Vector3(0.9f + i * 3f, EstateTop + 1.98f, EstateWalk),
                    Vector3.zero, new Color(1f, 0.90f, 0.76f), 2.2f, 3.6f);
            // 玄関。三和土と下駄箱にだけ届けばよいので、届く先を短く切る
            Lamp(place, "RoomBHall", LightType.Point, new Vector3(4.0f, RoomRoof - 0.40f, -15.2f),
                Vector3.zero, new Color(1f, 0.92f, 0.82f), 3.4f, 4.4f);
            // 右の家は蛍光灯の下。テレビの光だけでは卓も座布団も沈む
            Lamp(place, "RoomB", LightType.Point, new Vector3(5.0f, RoomRoof - 0.35f, -17.3f),
                Vector3.zero, new Color(1f, 0.93f, 0.84f), 3.4f, 7f);
            // テレビは光る面だけでは床に届かない。記憶 15 の最後の鍵打ちが
            // 「テレビの光が床に当たっている」を見るので、青い灯りを一つ据える
            Lamp(place, "TvGlow", LightType.Point, EstateTv + new Vector3(0f, -0.15f, 0.55f),
                Vector3.zero, new Color(0.62f, 0.76f, 1f), 1.8f, 3.6f);
        }

        // ---- 建物の地 ----------------------------------------------------------

        /// <summary>
        /// 地面・階段・三層ぶんの廊下・建物の面。
        ///
        /// 廊下を一層ではなく三層重ねるのは、階数が読めるようにするため。
        /// 手すりの向こうに下の階の床の縁と手すりが並んで見えると、
        /// 「三階建ての集合住宅の三階にいる」が小物ひとつ置かずに伝わる
        /// </summary>
        static void EstateShell(EstateBanks b)
        {
            // 地面。隣の棟の足元まで伸ばす。ここで切ると、棟が虚空に浮いて見える
            b.Slab.FaceY(0f, -14f, 25f, -20f, BlockFace, 1);
            // 各階の廊下。階の高さに来る踊り場は廊下そのものなので、
            // 西の端を井戸の幅まで伸ばす。下の二層もこれで階段と繋がる
            for (var f = 1; f <= 3; f++)
                b.Slab.Box(new Vector3((WalkRight + WalkLeft) * 0.5f, Floor * f - 0.09f, EstateWalk),
                    new Vector3(WalkRight - WalkLeft, 0.18f, LandingDeep));
            // 折り返しの踊り場。半階ぶん上がった先に、井戸の幅いっぱいで渡す
            for (var h = 0; h < 3; h++)
                b.Slab.Box(new Vector3((StairWest + StairEast) * 0.5f, Floor * h + Floor * 0.5f - 0.09f, EstateTurn),
                    new Vector3(StairEast - StairWest, 0.18f, StairTurn));
            // 最上階の廊下の天井。裸電球を吊るす面が要る。頭上 2.3 m なので当たりが入っていても触らない
            b.Slab.Box(new Vector3((WalkRight + WalkLeft) * 0.5f, WalkRoof + 0.1f, EstateWalk),
                new Vector3(WalkRight - WalkLeft, 0.2f, LandingDeep + 0.3f));

            // 六本の段。東の一本で半階上がり、折り返して西の一本でまた半階上がる。
            // 段鼻の明るい線は b.Bright へ集める
            for (var h = 0; h < 3; h++)
            {
                var y = Floor * h;
                EstateFlight(b.Slab, b.Bright, StairEastMid, WalkFront, y, Flight);
                EstateFlight(b.Slab, b.Bright, StairWestMid, StairTurnFar, y + Floor * 0.5f, -Flight);
            }

            // 階段の井戸の西の壁。建物の西の面をそのまま南へ伸ばす。
            // 面を一枚立てるだけでは、庭から回り込んだときに裏側が抜けて見える
            b.Wall.Box(new Vector3(StairWest - 0.12f, 5.6f, (EstateFace + StairEnd) * 0.5f),
                new Vector3(0.24f, 11.2f, StairEnd - EstateFace));
            // 二本のあいだの中壁。**折り返しの踊り場までは届かせない。** 届くと向きが変えられない。
            // 手すりで済ませないのは、隣の一本とは半階ぶんの段差があって、落ちれば 2.8 m だから
            b.Wall.Box(new Vector3(0f, 4.7f, (WalkFront + StairTurnFar) * 0.5f),
                new Vector3(StairSpine * 2f, 9.4f, Flight));
            // 建物の面。最上階に戸口を二つ開ける。三つ目は閉まっているので抜かない
            var holes = new List<Vector4>
            {
                new Vector4(DoorA - DoorHalf, DoorA + DoorHalf, EstateTop, EstateTop + DoorHigh),
                new Vector4(DoorB - DoorHalf, DoorB + DoorHalf, EstateTop, EstateTop + DoorHigh),
            };
            b.Wall.FaceZHoles(EstateFace, -1.2f, WallRight, 0f, 11.2f, 1, holes);
            // 建物の小口と陸屋根。面を一枚立てただけでは、建物が紙の書き割りに見える。
            // 地面を歩いて裏へ回れるので、四方と屋根を閉じて一つの塊にする
            const float back = RoomBack - 0.15f;
            b.Wall.FaceX(WallRight, back, EstateFace, 0f, 11.2f, 1);
            b.Wall.FaceX(-1.2f, back, EstateFace, 0f, 11.2f, -1);
            b.Wall.FaceZ(back, -1.2f, WallRight, 0f, 11.2f, -1);
            b.Wall.FaceY(11.2f, -1.2f, WallRight, back, EstateFace, 1);
            // 陸屋根の立ち上がり。屋上の縁が真っ平らだと、建物の頭が切り落とされて見える
            b.Wall.Box(new Vector3((WallRight - 1.2f) * 0.5f, 11.45f, EstateFace - 0.12f),
                new Vector3(WallRight + 1.2f, 0.5f, 0.24f));
            b.Wall.Box(new Vector3((WallRight - 1.2f) * 0.5f, 11.45f, back + 0.12f),
                new Vector3(WallRight + 1.2f, 0.5f, 0.24f));
            b.Wall.Box(new Vector3(WallRight - 0.12f, 11.45f, (EstateFace + back) * 0.5f),
                new Vector3(0.24f, 0.5f, EstateFace - back));
            b.Wall.Box(new Vector3(-1.08f, 11.45f, (EstateFace + back) * 0.5f),
                new Vector3(0.24f, 0.5f, EstateFace - back));
            // 階段の井戸の西の壁にも同じ立ち上がりを載せる。
            // 建物の縁だけが厚くて井戸の縁が薄いと、井戸が後から付け足した板に見える
            b.Wall.Box(new Vector3(StairWest - 0.12f, 11.45f, (EstateFace + StairEnd) * 0.5f),
                new Vector3(0.36f, 0.5f, StairEnd - EstateFace));

            // 右の家。四方と天井。**内側の面にも戸口を抜く。**
            // 抜かずに張ると、廊下から戸口を通り抜けられず、中からは壁が一枚塞いでいるように見える
            b.Wall.FaceX(RoomX0, RoomBack, EstateFace, EstateTop - EstateSunk, RoomRoof, 1);
            b.Wall.FaceX(RoomX1, RoomBack, EstateFace, EstateTop, RoomRoof, -1);
            b.Wall.FaceZ(RoomBack, RoomX0, RoomX1, EstateTop, RoomRoof, 1);
            // 内側の抜けは三和土の底まで下ろす。床と同じ高さで止めると、
            // 戸を開けたときに三和土の手前へ 0.15 m の壁が残って、跨げない敷居になる
            b.Wall.FaceZHoles(EstateFace - 0.02f, RoomX0, RoomX1, EstateTop - EstateSunk, RoomRoof, -1,
                new List<Vector4>
                {
                    new Vector4(DoorB - DoorHalf, DoorB + DoorHalf, EstateTop - EstateSunk, EstateTop + DoorHigh),
                });

            // 天井。上を向いた面は日射しを受けないので、暗いままでよい
            b.Shade.FaceY(RoomRoof, RoomX0, RoomX1, RoomBack, EstateFace, -1);
            b.Shade.FaceY(EstateTop + 2.2f, PorchX0, PorchX1, PorchBack, EstateFace, -1);

            // 井戸の西の壁の窓。一枚の板のままだと、階段の隣に何も無い崖が立っているように見える。
            // 高さは西の一本の段の面から 0.8 m 上。段を上がりながら覗ける位置に来る
            for (var h = 0; h < 3; h++)
            {
                var y = 2.3f + h * Floor;
                EstateHole(b, y, EstateTurn, h == 1);
                EstateHole(b, y + 0.6f, (WalkFront + StairTurnFar) * 0.5f, false);
            }

            // 階数の札。折り返しで上がってくると、階の高さの踊り場は廊下の西の端に来る。
            // そこの壁に貼れば、上がり切ったところと降り始めるところの両方で読める
            EstateSign(b, Floor + 1.45f, EstateWalk, 2);
            EstateSign(b, Floor * 2f + 1.45f, EstateWalk, 3);

            // 階段の手すり。東の一本の外側と、折り返しの踊り場の東と南を回す。
            // 西の一本の外側は井戸の壁、内側は中壁なので、手すりは要らない
            for (var h = 0; h < 3; h++)
            {
                var y = Floor * h;
                EstateSlope(b.Rail, StairEast, WalkFront, y, Flight);
                EstateRailZ(b.Rail, StairEast, StairTurnFar, StairEnd, y + Floor * 0.5f);
                EstateRailX(b.Rail, StairEnd, StairWest, StairEast, y + Floor * 0.5f);
            }
            // 最上階の廊下の外側。**階段が上がってくる西の一本（x -1.2〜-0.1）だけ空ける。**
            // 手すりに当たりを入れたので、廊下の長さぶん通すと階段を上がってきた先が塞がる。
            // 一度通した版では、上がり切ったところで壁に突き当たって廊下へ出られなかった。
            // 逆に東の一本の上は塞ぐ。ここから先は段が無く、下の段まで 2.8 m 落ちる
            EstateRailX(b.Rail, WalkFront, -StairSpine, WalkRight, EstateTop);
            // 廊下の東の端。**見えない仕切りではなく壁で閉じる。**
            // 歩いて確かめたら廊下を +x へ歩き切って落ちたので、そこに戸境の壁を立てた
            b.Wall.FaceX(WalkRight + 0.15f, EstateFace, WalkFront, EstateTop, WalkRoof, -1);
            b.Wall.FaceX(WalkRight + 0.15f, EstateFace, WalkFront, EstateTop, WalkRoof, 1);
        }

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

        // ---- 階数の札 ----------------------------------------------------------

        /// <summary>
        /// 階数の札。明るい板に、数字と F を暗い棒で組む。
        ///
        /// 文字を貼るには TextMeshPro の面が要るが、この場所は面を一枚に焼いてしまうので、
        /// 棒を七本並べる組み方にして mesh のまま持たせる
        /// </summary>
        static void EstateSign(EstateBanks b, float y, float z, int floor)
        {
            b.Bright.Box(new Vector3(StairWest + 0.022f, y, z), new Vector3(0.03f, 0.40f, 0.36f));
            EstateGlyph(b.Shade, y, z - 0.09f, floor == 2 ? 91 : 79);
            EstateGlyph(b.Shade, y, z + 0.09f, 113);
        }

        /// <summary>
        /// 七つの棒で一文字。<paramref name="bits"/> は下から a・b・c・d・e・f・g の順。
        ///
        /// 壁は x が一定の面なので、文字は +z へ読む向きに並べる
        /// </summary>
        static void EstateGlyph(Bank b, float y, float z, int bits)
        {
            const float x = StairWest + 0.045f;
            const float thin = 0.02f;
            const float hw = 0.055f;
            const float hh = 0.115f;
            const float bar = 0.028f;
            var wide = new Vector3(thin, bar, hw * 2f);
            var tall = new Vector3(thin, hh, bar);
            if ((bits & 1) != 0) b.Box(new Vector3(x, y + hh, z), wide);
            if ((bits & 2) != 0) b.Box(new Vector3(x, y + hh * 0.5f, z + hw), tall);
            if ((bits & 4) != 0) b.Box(new Vector3(x, y - hh * 0.5f, z + hw), tall);
            if ((bits & 8) != 0) b.Box(new Vector3(x, y - hh, z), wide);
            if ((bits & 16) != 0) b.Box(new Vector3(x, y - hh * 0.5f, z - hw), tall);
            if ((bits & 32) != 0) b.Box(new Vector3(x, y + hh * 0.5f, z - hw), tall);
            if ((bits & 64) != 0) b.Box(new Vector3(x, y, z), wide);
        }

        // ---- 下の階 ------------------------------------------------------------

        /// <summary>
        /// 下の階の廊下。手すり・戸口・窓・暮らしの物を、最上階と同じ並びで重ねる。
        ///
        /// **同じ形を繰り返すのが要。** 階ごとに違う物を置くと繰り返しが崩れて、
        /// 三階建ての一棟ではなく、高さの違う三つの通路が並んでいるように見える
        /// </summary>
        static void EstateLower(EstateBanks b)
        {
            for (var f = 0; f <= 2; f++)
            {
                var y = Floor * f;
                // 地面の階は廊下ではなく地面なので、床の縁も手すりも要らない
                if (f > 0)
                {
                    EstateWalkRail(b.Rail, y);
                    b.Wall.FaceX(WalkRight + 0.15f, EstateFace, WalkFront, y, y + Floor - 0.18f, -1);
                    b.Wall.FaceX(WalkRight + 0.15f, EstateFace, WalkFront, y, y + Floor - 0.18f, 1);
                    // 廊下の天井の下面は上の階の床板。その縁へ庇の線を一本回すと、階の境が読める
                    b.Shade.Box(new Vector3((WalkRight + WalkLeft) * 0.5f, y + Floor - 0.24f, WalkFront - 0.03f),
                        new Vector3(WalkRight - WalkLeft, 0.08f, 0.08f));
                }
                for (var c = 0; c < 3; c++) EstateFlatDoor(b, DoorA + c * (DoorB - DoorA), y);
                // 戸口と戸口の間の窓。戸だけが並ぶと壁が間延びする
                for (var c = 0; c < 2; c++)
                {
                    // 戸口の脇のメーターと郵便受けを避けて、その間へ収まる幅にする
                    var x = DoorA + 1.33f + c * (DoorB - DoorA);
                    var into = (f == 1 && c == 1) ? b.Lit : b.Shade;
                    into.FaceZ(EstateFace + 0.03f, x - 0.32f, x + 0.32f, y + 1.06f, y + 2.06f, 1);
                    b.Gear.Box(new Vector3(x, y + 1.56f, EstateFace + 0.05f), new Vector3(0.68f, 0.05f, 0.05f));
                    b.Gear.Box(new Vector3(x, y + 1.56f, EstateFace + 0.05f), new Vector3(0.05f, 1.04f, 0.05f));
                }
                if (f == 0) continue;
                // 室外機の列。階ごとに同じ場所へ並べると、面の繰り返しがもう一段強くなる
                for (var c = 0; c < 3; c++)
                {
                    var x = 2.45f + c * 2.3f;
                    b.Gear.Box(new Vector3(x, y + 0.30f, EstateFace + 0.24f), new Vector3(0.78f, 0.56f, 0.34f));
                    b.Shade.Box(new Vector3(x, y + 0.30f, EstateFace + 0.42f), new Vector3(0.60f, 0.38f, 0.02f));
                }
                // 物干し竿と洗濯物。下の階にも人が住んでいることにする
                b.Gear.Box(new Vector3(5.6f, y + 1.85f, WalkFront - 0.11f), new Vector3(2.6f, 0.045f, 0.045f));
                for (var i = 0; i < 4; i++)
                    b.Linen.Box(new Vector3(4.6f + i * 0.62f, y + 1.50f, WalkFront - 0.11f),
                        new Vector3(0.44f, 0.62f, 0.02f));
                // 非常灯。緑の小さな面が階ごとに一つ点いていると、共用部の廊下に見える
                b.Green.Box(new Vector3(0.35f, y + Floor - 0.45f, EstateFace + 0.12f), new Vector3(0.26f, 0.13f, 0.03f));
                b.Gear.Box(new Vector3(0.35f, y + Floor - 0.45f, EstateFace + 0.07f), new Vector3(0.32f, 0.19f, 0.08f));
                // 自転車とダンボール。床に物が出ているのが、事務所の通路と分ける
                EstateBike(b, 7.0f, y, EstateFace + 0.45f);
                // 置く先は戸口 A と B のあいだ。階段の井戸へ置くと、折り返して向きを変える足元が埋まる
                for (var i = 0; i < 2; i++)
                    b.Set.Box(new Vector3(2.95f, y + 0.18f + i * 0.34f, EstateFace + 0.30f),
                        new Vector3(0.52f, 0.32f, 0.40f));
            }
        }

        /// <summary>
        /// 下の階の廊下の手すり。**階段の井戸ぶん（x -1.2〜1.2）は空ける。**
        ///
        /// 折り返しの階段はこの階で一度廊下へ戻り、東の一本へ乗り換えてまた上がる。
        /// 井戸の口を塞ぐと、上がってきた先と上がる先の両方が閉じる
        /// </summary>
        static void EstateWalkRail(Bank rail, float y)
        {
            EstateRailX(rail, WalkFront, StairEast, WalkRight, y);
        }

        /// <summary>
        /// 下の階の戸口。抜かずに面へ描くだけの戸。
        ///
        /// 中を作らない階なので、枠と戸と番号板と郵便受けだけで済ませる。
        /// 最上階の戸口と同じ幅・同じ高さにして、見上げたときに列が揃って見えるようにする
        /// </summary>
        static void EstateFlatDoor(EstateBanks b, float x, float y)
        {
            b.Shade.FaceZ(EstateFace + 0.02f, x - DoorHalf, x + DoorHalf, y, y + DoorHigh, 1);
            b.Leaf.Box(new Vector3(x, y + DoorHigh * 0.5f, EstateFace + 0.05f),
                new Vector3(DoorHalf * 2f - 0.06f, DoorHigh - 0.06f, 0.05f));
            b.Gear.Box(new Vector3(x - DoorHalf - 0.05f, y + (DoorHigh + 0.1f) * 0.5f, EstateFace + 0.05f),
                new Vector3(0.10f, DoorHigh + 0.1f, 0.09f));
            b.Gear.Box(new Vector3(x + DoorHalf + 0.05f, y + (DoorHigh + 0.1f) * 0.5f, EstateFace + 0.05f),
                new Vector3(0.10f, DoorHigh + 0.1f, 0.09f));
            b.Gear.Box(new Vector3(x, y + DoorHigh + 0.05f, EstateFace + 0.05f),
                new Vector3(DoorHalf * 2f + 0.2f, 0.10f, 0.09f));
            // 上の換気口と下の新聞受け。鉄の一枚板では倉庫の戸に見える
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(x, y + 1.78f + i * 0.07f, EstateFace + 0.08f),
                    new Vector3(0.44f, 0.035f, 0.02f));
            b.Shade.Box(new Vector3(x, y + 0.32f, EstateFace + 0.08f), new Vector3(0.34f, 0.05f, 0.02f));
            b.Gear.Box(new Vector3(x + 0.30f, y + 1.00f, EstateFace + 0.10f), new Vector3(0.05f, 0.05f, 0.11f));
            b.Bright.Box(new Vector3(x - 0.75f, y + 1.80f, EstateFace + 0.05f), new Vector3(0.24f, 0.15f, 0.02f));
            b.Gear.Box(new Vector3(x - 0.75f, y + 1.28f, EstateFace + 0.06f), new Vector3(0.32f, 0.24f, 0.12f));
            b.Gear.Box(new Vector3(x + 0.80f, y + 1.42f, EstateFace + 0.09f), new Vector3(0.36f, 0.46f, 0.18f));
            b.Bright.Box(new Vector3(x + 0.80f, y + 1.50f, EstateFace + 0.185f), new Vector3(0.18f, 0.18f, 0.02f));
        }

        /// <summary>自転車。輪と骨組みだけの影。前後の輪が二つ見えれば自転車に読める</summary>
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

        // ---- 共用部 ------------------------------------------------------------

        /// <summary>
        /// 最上階の廊下の共用部。消火器・掲示板・配電盤・非常灯・点検口。
        ///
        /// **住戸の物ではなく建物の物を置く。** 傘立てや植木鉢は住んでいる人の物で、
        /// それだけでは長屋にも見える。誰の物でもない物が並んで初めて、
        /// 管理された集合住宅の共用廊下になる。
        /// 置く先は階段の口の側へ寄せる。記憶 0 の母子も記憶 1 のハンナも戸口の前で撮るので、
        /// そちらへ置くと人の足元が物で埋まる
        /// </summary>
        static void EstateCommon(EstateBanks b)
        {
            // 掲示板。硝子の入った枠に紙が何枚か。壁でいちばん明るい四角になる
            b.Gear.Box(new Vector3(StairWest + 0.03f, EstateTop + 1.38f, -14.05f), new Vector3(0.05f, 0.74f, 0.92f));
            b.Shade.Box(new Vector3(StairWest + 0.055f, EstateTop + 1.38f, -14.05f), new Vector3(0.02f, 0.64f, 0.82f));
            for (var i = 0; i < 4; i++)
                b.Paper.Box(new Vector3(StairWest + 0.065f, EstateTop + 1.24f + (i % 2) * 0.30f,
                        -13.80f - (i / 2) * 0.42f), new Vector3(0.01f, 0.24f, 0.32f));

            // 消火器。赤い縦長がひとつあるだけで、廊下が共用部だと分かる。
            // 置く先は井戸の東の隅。上がり切ったところは西の一本の出口なので、そちらは空けておく
            b.Red.Box(new Vector3(1.05f, EstateTop + 0.30f, -14.52f), new Vector3(0.19f, 0.54f, 0.19f));
            b.Gear.Box(new Vector3(1.05f, EstateTop + 0.60f, -14.52f), new Vector3(0.09f, 0.10f, 0.09f));
            b.Gear.Box(new Vector3(1.05f, EstateTop + 0.02f, -14.52f), new Vector3(0.30f, 0.04f, 0.30f));
            b.Paper.Box(new Vector3(1.16f, EstateTop + 0.34f, -14.52f), new Vector3(0.01f, 0.16f, 0.13f));

            // 配電盤と量水器の扉。面のいちばん端に寄せて、戸口の列を邪魔しない
            b.Gear.Box(new Vector3(0.38f, EstateTop + 1.42f, EstateFace + 0.09f), new Vector3(0.52f, 0.72f, 0.16f));
            b.Shade.Box(new Vector3(0.38f, EstateTop + 1.42f, EstateFace + 0.18f), new Vector3(0.42f, 0.60f, 0.02f));
            b.Bright.Box(new Vector3(0.38f, EstateTop + 1.72f, EstateFace + 0.19f), new Vector3(0.20f, 0.09f, 0.02f));
            b.Gear.Box(new Vector3(0.38f, EstateTop + 0.36f, EstateFace + 0.11f), new Vector3(0.46f, 0.52f, 0.20f));

            // 非常灯。緑は場面 3 の端末と同じ色で、この場面でここにしか無い
            b.Gear.Box(new Vector3(0.90f, WalkRoof - 0.30f, EstateFace + 0.08f), new Vector3(0.34f, 0.20f, 0.10f));
            b.Green.Box(new Vector3(0.90f, WalkRoof - 0.30f, EstateFace + 0.14f), new Vector3(0.27f, 0.14f, 0.03f));

            // 天井の点検口。四本の細い棒で四角を描くだけ。
            // 天井が一枚の板のままだと、頭上に何も無い場所になる
            EstateHatch(b.Gear, 2.10f, WalkRoof - 0.015f, -14.25f, 0.56f);
            EstateHatch(b.Gear, 6.00f, WalkRoof - 0.015f, -14.25f, 0.56f);
            // 床の点検口。等間隔で三つ並ぶと、床の繰り返しがもう一段増える。
            // 手すり寄りに置くのは、面側は室外機と傘立てで埋まっているため
            for (var c = 0; c < 3; c++)
                EstateHatch(b.Gear, 1.0f + c * 2.6f, EstateTop + 0.012f, -13.72f, 0.44f);
        }

        /// <summary>点検口の枠。四辺を細い棒で囲うだけ</summary>
        static void EstateHatch(Bank b, float x, float y, float z, float side)
        {
            var half = side * 0.5f;
            b.Box(new Vector3(x, y, z - half), new Vector3(side, 0.03f, 0.04f));
            b.Box(new Vector3(x, y, z + half), new Vector3(side, 0.03f, 0.04f));
            b.Box(new Vector3(x - half, y, z), new Vector3(0.04f, 0.03f, side));
            b.Box(new Vector3(x + half, y, z), new Vector3(0.04f, 0.03f, side));
        }

        // ---- 暮らしの気配 ------------------------------------------------------

        /// <summary>
        /// 最上階の各戸の前に出ている物。傘立て・植木鉢・ダンボール・新聞。
        ///
        /// **人の立ち位置を避けて戸と戸のあいだへ寄せる。** 記憶 0 の母子は戸口 A の前、
        /// 記憶 8 の主は戸口 B の前を通るので、そこへ物を置くと足元が埋まる
        /// </summary>
        static void EstateLives(EstateBanks b)
        {
            // 傘立てと傘。戸口 A と B のあいだ
            b.Gear.Box(new Vector3(2.60f, EstateTop + 0.24f, EstateFace + 0.20f), new Vector3(0.28f, 0.48f, 0.26f));
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(2.54f + i * 0.06f, EstateTop + 0.58f, EstateFace + 0.20f + i * 0.02f),
                    new Vector3(0.05f, 0.76f, 0.05f));

            // 植木鉢。高さを変えて三つ。同じ高さで並べると棚に見える
            var pots = new[] { 2.06f, 2.32f, 0.62f };
            var tall = new[] { 0.26f, 0.19f, 0.30f };
            for (var i = 0; i < pots.Length; i++)
            {
                b.Red.Box(new Vector3(pots[i], EstateTop + tall[i] * 0.5f, EstateFace + 0.22f),
                    new Vector3(0.24f, tall[i], 0.24f));
                b.Soft.Box(new Vector3(pots[i], EstateTop + tall[i] + 0.16f, EstateFace + 0.22f),
                    new Vector3(0.30f, 0.34f, 0.28f));
            }

            // ダンボールの山。戸口 C の脇。潰した板を一枚立て掛ける
            for (var i = 0; i < 3; i++)
                b.Set.Box(new Vector3(7.50f - i * 0.04f, EstateTop + 0.17f + i * 0.33f, EstateFace + 0.32f),
                    new Vector3(0.50f, 0.32f, 0.42f));
            b.Set.Box(new Vector3(7.10f, EstateTop + 0.46f, EstateFace + 0.30f),
                new Vector3(0.62f, 0.92f, 0.04f), Quaternion.Euler(12f, 0f, 0f));

            // 新聞受けに差さったままの新聞。ここだけ白く、戸口の位置をもう一度拾わせる
            b.Paper.Box(new Vector3(DoorA - 0.75f, EstateTop + 1.40f, EstateFace + 0.14f),
                new Vector3(0.20f, 0.26f, 0.05f), Quaternion.Euler(0f, 0f, -14f));
            b.Paper.Box(new Vector3(DoorC - 0.75f, EstateTop + 1.39f, EstateFace + 0.13f),
                new Vector3(0.20f, 0.24f, 0.05f), Quaternion.Euler(0f, 0f, 9f));
        }

        /// <summary>
        /// 最上階の廊下に出ている大きな物。物干し竿と洗濯物、室外機、洗濯機、裸電球。
        ///
        /// 戸口と手すりだけの廊下は事務所の通路に見える。人が住んでいる証しをここで置く。
        /// 床に置く物は、開いた戸の前を避けて戸と戸のあいだへ寄せる
        /// </summary>
        static void EstateGear(EstateBanks b)
        {
            // 物干し竿。戸口 C の前に寄せて、廊下を +x へ見通したときの奥行きに掛ける
            b.Gear.Box(new Vector3(6.05f, EstateTop + 1.90f, WalkFront - 0.11f), new Vector3(2.5f, 0.045f, 0.045f));
            for (var i = 0; i < 4; i++)
            {
                var x = 5.0f + i * 0.6f;
                if (i == 2) b.Soft.Box(new Vector3(x, EstateTop + 1.62f, WalkFront - 0.11f), new Vector3(0.34f, 0.5f, 0.02f));
                else b.Linen.Box(new Vector3(x, EstateTop + 1.55f, WalkFront - 0.11f), new Vector3(0.46f, 0.66f, 0.02f));
            }

            // 室外機。羽根の線を三本入れると、ただの箱と見分けが付く
            b.Gear.Box(new Vector3(3.3f, EstateTop + 0.30f, EstateFace + 0.25f), new Vector3(0.80f, 0.58f, 0.36f));
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(3.3f, EstateTop + 0.18f + i * 0.12f, EstateFace + 0.44f),
                    new Vector3(0.62f, 0.05f, 0.02f));

            // 洗濯機
            b.Linen.Box(new Vector3(5.8f, EstateTop + 0.43f, EstateFace + 0.34f), new Vector3(0.60f, 0.86f, 0.56f));
            b.Shade.Box(new Vector3(5.8f, EstateTop + 0.87f, EstateFace + 0.34f), new Vector3(0.44f, 0.03f, 0.40f));
            b.Bright.Box(new Vector3(5.8f, EstateTop + 0.80f, EstateFace + 0.63f), new Vector3(0.36f, 0.06f, 0.02f));

            // 裸電球。コードは細く暗く、球だけ光らせる
            for (var i = 0; i < 3; i++)
            {
                var x = 0.9f + i * 3f;
                b.Gear.Box(new Vector3(x, EstateTop + 2.18f, EstateWalk), new Vector3(0.025f, 0.24f, 0.025f));
                b.Lit.Box(new Vector3(x, EstateTop + 1.98f, EstateWalk), new Vector3(0.12f, 0.16f, 0.12f));
            }
        }

        // ---- 手すりの外 --------------------------------------------------------

        /// <summary>
        /// 手すりの向こう。電柱と電線、駐輪場の屋根、物干し台、低い棟、室外機の列。
        ///
        /// **高さの違う物を手前から奥へ重ねる。** 隣の棟の書き割りだけでは、
        /// 手すりの外が一枚の絵になって距離が出ない。
        /// 電線は目の高さのすぐ下を横切るので、廊下を歩くあいだ景色がいちばん大きく動く
        /// </summary>
        static void EstateYard(EstateBanks b)
        {
            // 電柱。三本を等間隔で、廊下の目の高さより少し上まで
            var poles = new[] { -4.5f, 8.5f, 21.5f };
            foreach (var x in poles)
            {
                b.Gear.Box(new Vector3(x, 4.6f, -3.2f), new Vector3(0.24f, 9.2f, 0.24f));
                for (var i = 0; i < 2; i++)
                    b.Gear.Box(new Vector3(x, 8.30f + i * 0.55f, -3.2f), new Vector3(0.09f, 0.09f, 1.7f));
                b.Gear.Box(new Vector3(x, 6.30f, -3.2f), new Vector3(0.52f, 0.62f, 0.44f));
            }
            // 電線。撓みが無いと物差しを渡したように見える
            var hang = new[] { -14f, -4.5f, 8.5f, 21.5f, 25f };
            for (var i = 0; i + 1 < hang.Length; i++)
            {
                for (var w = 0; w < 3; w++)
                {
                    var y = 8.24f + (w - 1) * 0.28f;
                    var z = -3.2f + (w - 1) * 0.62f;
                    EstateWire(b.Gear, new Vector3(hang[i], y, z), new Vector3(hang[i + 1], y, z), 0.34f, 0.035f);
                }
            }
            // もう一本を奥へ渡す。線が一面だけだと、手前と奥の区別が付かない
            EstateWire(b.Gear, new Vector3(-14f, 9.10f, 1.4f), new Vector3(25f, 8.90f, 1.4f), 0.9f, 0.035f);

            // 駐輪場。屋根が水平に一枚あると、地面との間に距離が生まれる
            b.Gear.Box(new Vector3(5.3f, 2.34f, -8.5f), new Vector3(8.2f, 0.09f, 2.6f));
            for (var i = 0; i < 16; i++)
                b.Shade.Box(new Vector3(1.45f + i * 0.51f, 2.40f, -8.5f), new Vector3(0.05f, 0.05f, 2.6f));
            for (var i = 0; i < 6; i++)
                b.Gear.Box(new Vector3(1.5f + (i % 3) * 3.8f, 1.17f, i < 3 ? -9.6f : -7.4f),
                    new Vector3(0.12f, 2.34f, 0.12f));
            for (var i = 0; i < 7; i++) EstateBike(b, 2.0f + i * 1.05f, 0f, -8.4f);

            // 物干し台。地面の上に洗濯物が一枚あると、そこが暮らしの庭だと分かる
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(13.0f + i * 2.2f, 0.90f, -5.0f), new Vector3(0.10f, 1.80f, 0.10f));
            b.Gear.Box(new Vector3(14.1f, 1.76f, -5.0f), new Vector3(2.4f, 0.05f, 0.05f));
            for (var i = 0; i < 4; i++)
                b.Linen.Box(new Vector3(13.3f + i * 0.54f, 1.42f, -5.0f), new Vector3(0.42f, 0.62f, 0.02f));

            // 低い棟。隣の棟より手前に低い塊があると、奥行きが二段になる
            b.Wall.Box(new Vector3(16.0f, 1.70f, -11.4f), new Vector3(8.0f, 3.40f, 4.4f));
            b.Wall.Box(new Vector3(16.0f, 3.52f, -11.4f), new Vector3(8.4f, 0.24f, 4.8f));
            for (var i = 0; i < 4; i++)
                b.Shade.FaceZ(-9.18f, 13.1f + i * 1.9f, 14.1f + i * 1.9f, 1.10f, 2.30f, -1);
            // 室外機の列。低い棟の脇に並べる。同じ箱が等間隔で並ぶ形がもう一つ増える
            for (var i = 0; i < 5; i++)
            {
                var x = 10.6f + i * 0.98f;
                b.Gear.Box(new Vector3(x, 0.32f, -9.0f), new Vector3(0.80f, 0.58f, 0.36f));
                b.Shade.Box(new Vector3(x, 0.32f, -8.81f), new Vector3(0.62f, 0.40f, 0.02f));
            }
        }

        /// <summary>
        /// 撓んだ線を一本。真っ直ぐな棒では電線に見えないので、
        /// 途中の点を拾って短い棒で繋ぐ
        /// </summary>
        static void EstateWire(Bank b, Vector3 from, Vector3 to, float sag, float thick)
        {
            const int span = 6;
            var last = from;
            for (var i = 1; i <= span; i++)
            {
                var k = i / (float)span;
                var at = Vector3.Lerp(from, to, k);
                at.y -= sag * 4f * k * (1f - k);
                var dir = at - last;
                if (dir.sqrMagnitude > 1e-6f)
                    b.Box((last + at) * 0.5f, new Vector3(thick, thick, dir.magnitude),
                        Quaternion.LookRotation(dir, Vector3.up));
                last = at;
            }
        }

        // ---- 隣の棟 ------------------------------------------------------------

        /// <summary>
        /// 隣の棟。窓の四角が等間隔に並ぶだけの書き割り。
        ///
        /// **灯りを当てない。** 面を Unlit で持たせると、朝日が回らない側でも値が決まり、
        /// 夜明けの薄い空を背にした輪郭がそのまま出る。当たりも影も持たせない。
        /// バルコニーの帯と屋上の塔屋を重ねるのは、窓の列だけでは面が平らに沈むため
        /// </summary>
        static void EstateBlockWall(Transform place, EstateBanks b)
        {
            var slabs = new Bank { Texel = 0.4f };
            slabs.FaceZ(BlockFace, -13.5f, 24.5f, 0f, 15.6f, -1);
            NoShadow(EstateEmit(place, "EstateBlock", slabs,
                Glow("EstateBlock", new Color(0.50f, 0.53f, 0.60f), 0.40f), false));

            var panes = new Bank { Texel = 0.4f };
            // 階ごとの水平の帯。窓の列だけだと面が平らすぎて、棟の高さが出ない
            for (var j = 0; j < 5; j++)
                panes.FaceZ(BlockFace - 0.12f, -13.5f, 24.5f, 1.22f + j * Floor, 1.38f + j * Floor, -1);
            for (var i = 0; i < 14; i++)
            {
                for (var j = 0; j < 5; j++)
                {
                    var x = -11.2f + i * 2.6f;
                    var y = 1.6f + j * Floor;
                    // 数枚だけ灯りが入っている。全部暗いと廃墟に、全部明るいと看板に見える
                    var into = (i * 5 + j * 3) % 13 == 0 ? b.Lit : panes;
                    into.FaceZ(BlockFace - 0.16f, x - 0.72f, x + 0.72f, y, y + 1.15f, -1);
                    // バルコニーの手すり。**窓より明るい側に置く。**
                    // 暗い帯にすると窓と繋がって、棟ぜんたいが黒い塊になる。
                    // 朝日の当たる手すりが明るく、その下の影が暗いと、階の帯が横へ通る
                    b.Far.Box(new Vector3(x, y - 0.42f, BlockFace - 0.42f), new Vector3(1.90f, 0.86f, 0.10f));
                    panes.Box(new Vector3(x, y - 0.84f, BlockFace - 0.22f), new Vector3(1.94f, 0.08f, 0.44f));
                    // 竿と洗濯物。明るい手すりの前に暗い形が下がると、棟に人が住んでいることになる
                    if ((i + j) % 3 != 0) continue;
                    panes.Box(new Vector3(x, y + 0.02f, BlockFace - 0.50f), new Vector3(1.70f, 0.05f, 0.05f));
                    for (var k = 0; k < 3; k++)
                        panes.Box(new Vector3(x - 0.5f + k * 0.5f, y - 0.26f, BlockFace - 0.50f),
                            new Vector3(0.36f, 0.52f, 0.02f));
                }
            }
            // 屋上。立ち上がりと塔屋と水槽。棟の頭が切り落とされて見えないように
            panes.Box(new Vector3(5.5f, 15.85f, BlockFace - 0.18f), new Vector3(38f, 0.5f, 0.36f));
            panes.Box(new Vector3(-2.5f, 16.9f, BlockFace - 0.6f), new Vector3(4.2f, 2.6f, 1.2f));
            panes.Box(new Vector3(12.5f, 18.1f, BlockFace - 0.6f), new Vector3(3.0f, 1.8f, 1.2f));
            for (var i = 0; i < 4; i++)
                panes.Box(new Vector3(11.4f + (i % 2) * 2.2f, 16.7f, BlockFace - 0.3f - (i / 2) * 0.6f),
                    new Vector3(0.16f, 3.0f, 0.16f));
            NoShadow(EstateEmit(place, "EstateBlockPane", panes, Mat("Ceiling"), false));

            // さらに奥の棟。手前の棟に隠れて頭だけが出る。
            // 遠いほど薄く見えるので、手前の棟より明るい色にして距離を出す
            b.Far.FaceZ(14f, -13f, 24f, 9f, 19.5f, -1);
            for (var i = 0; i < 12; i++)
                b.Far.FaceZ(13.96f, -11.5f + i * 3f, -10.2f + i * 3f, 16.4f, 17.5f, -1);
        }

        // ---- 右の家 ------------------------------------------------------------

        /// <summary>
        /// 右の家（記憶 8・15）。玄関と居間。
        ///
        /// 座って見る絵なので、目の高さ 1.15 m から卓とテレビが同時に入るように寄せる
        /// </summary>
        static void EstateRoom(EstateBanks b)
        {
            EstateGenkan(b);

            // 床板。廊下と同じ土間の色では、戸を跨いで中へ入ったことが伝わらない。
            // 三和土の抜けたところを避けて二枚に分ける
            b.Board.FaceY(EstateTop, RoomX0, HallDoma, RoomBack, HallSill, 1);
            b.Board.FaceY(EstateTop, HallDoma, RoomX1, RoomBack, EstateFace, 1);

            b.Soft.Box(new Vector3(4.9f, EstateTop + 0.22f, -16.6f), new Vector3(1.9f, 0.44f, 0.72f));
            b.Soft.Box(new Vector3(4.9f, EstateTop + 0.52f, -16.28f), new Vector3(1.9f, 0.6f, 0.14f));

            b.Set.Box(new Vector3(EstateTv.x, EstateTop + 0.25f, EstateTv.z - 0.14f), new Vector3(1.0f, 0.5f, 0.42f));
            b.Set.Box(EstateTv + new Vector3(0f, 0f, -0.05f), new Vector3(0.82f, 0.54f, 0.14f));

            // 卓。天板と脚
            b.Set.Box(new Vector3(4.95f, EstateTop + 0.37f, -17.62f), new Vector3(1.10f, 0.06f, 0.66f));
            for (var i = 0; i < 4; i++)
                b.Set.Box(new Vector3(4.95f + ((i & 1) == 0 ? -0.48f : 0.48f), EstateTop + 0.17f,
                    -17.62f + ((i & 2) == 0 ? -0.26f : 0.26f)), new Vector3(0.07f, 0.34f, 0.07f));

            // 座布団
            b.Soft.Box(new Vector3(4.05f, EstateTop + 0.05f, -17.62f), new Vector3(0.56f, 0.10f, 0.56f));
            b.Soft.Box(new Vector3(5.88f, EstateTop + 0.05f, -17.58f), new Vector3(0.56f, 0.10f, 0.56f));

            // カーテン。襞は板の前後をずらして出す。テレビの前だけ開けておく。
            // **廊下の洗濯物と同じ明るい布では駄目。** 戸口の正面から抜けを覗いたとき、
            // 奥のカーテンがいちばん明るい面になって、居間が真っ先に目へ入る
            b.Gear.Box(new Vector3(4.95f, EstateTop + 2.12f, RoomBack + 0.12f), new Vector3(3.9f, 0.05f, 0.05f));
            for (var i = 0; i < 5; i++)
                b.Soft.Box(new Vector3(3.15f + i * 0.32f, EstateTop + 1.12f, RoomBack + 0.10f + (i % 2) * 0.06f),
                    new Vector3(0.30f, 1.92f, 0.05f));
            for (var i = 0; i < 3; i++)
                b.Soft.Box(new Vector3(6.00f + i * 0.32f, EstateTop + 1.12f, RoomBack + 0.10f + (i % 2) * 0.06f),
                    new Vector3(0.30f, 1.92f, 0.05f));

            // 箪笥。**抜けを覗いた線の先へ置く。** 玄関から見通せる帯をここで塞ぐので、
            // 戸口の正面から見えるのは玄関と、その奥の家具の側面までになる
            b.Set.Box(new Vector3(3.30f, EstateTop + 0.75f, -17.30f), new Vector3(0.56f, 1.50f, 0.90f));
            for (var i = 0; i < 4; i++)
                b.Shade.Box(new Vector3(3.59f, EstateTop + 0.28f + i * 0.34f, -17.30f),
                    new Vector3(0.02f, 0.05f, 0.76f));
            b.Gear.Box(new Vector3(3.30f, EstateTop + 1.62f, -17.44f), new Vector3(0.20f, 0.24f, 0.16f));
            b.Soft.Box(new Vector3(3.30f, EstateTop + 1.60f, -17.10f), new Vector3(0.26f, 0.20f, 0.22f));

            // 幅木。壁の裾に線が一本通らないと、居間の壁が塗っただけの面に見える
            EstateSkirt(b, RoomX0 + 0.02f, RoomBack, HallSill, true);
            EstateSkirt(b, RoomX1 - 0.02f, RoomBack, EstateFace, true);
            EstateSkirt(b, RoomBack + 0.02f, RoomX0, RoomX1, false);
            EstateSkirt(b, HallWall - HallSkin - 0.02f, HallGap1, RoomX1, false);
            EstateSkirt(b, HallWall - HallSkin - 0.02f, RoomX0, HallGap0, false);

            // 蛍光灯の笠。光る面そのものは Estate が板で置く
            b.Shade.Box(new Vector3(5.0f, RoomRoof - 0.05f, -17.3f), new Vector3(1.3f, 0.1f, 0.32f));
        }

        /// <summary>
        /// 右の家の玄関。三和土・上がり框・下駄箱・短い廊下・居間への抜け。
        ///
        /// **抜けを戸口の正面から西へ外す。** 廊下に立って戸口の正面から覗いたとき、
        /// 目に入るのは三和土と框と壁で、居間は抜けの脇にしか見えない。
        /// 記憶 15 のエレナは居間に座って北西を向くので、その線だけが抜けを通って
        /// 三和土と下駄箱まで届く。仕切りを東へ寄せると、今度はエレナから玄関が見えなくなる
        /// </summary>
        static void EstateGenkan(EstateBanks b)
        {
            // 三和土と、框と東側の蹴上げ
            b.Tile.FaceY(EstateTop - EstateSunk, RoomX0, HallDoma, HallSill, EstateFace, 1);
            b.Tile.FaceZ(HallSill, RoomX0, HallDoma, EstateTop - EstateSunk, EstateTop, 1);
            b.Tile.FaceX(HallDoma, HallSill, EstateFace, EstateTop - EstateSunk, EstateTop, -1);
            // 上がり框。当たりは蹴上げの面が持つので、この縁には入れない
            b.Set.Box(new Vector3((RoomX0 + HallDoma) * 0.5f, EstateTop - 0.09f, HallSill + 0.035f),
                new Vector3(HallDoma - RoomX0, 0.18f, 0.07f));

            // 居間との仕切り。抜けの上は垂れ壁で閉じる
            var gap = new List<Vector4>
            {
                new Vector4(HallGap0, HallGap1, EstateTop, EstateTop + HallHead),
            };
            b.Wall.FaceZHoles(HallWall + HallSkin, RoomX0, RoomX1, EstateTop, RoomRoof, 1, gap);
            b.Wall.FaceZHoles(HallWall - HallSkin, RoomX0, RoomX1, EstateTop, RoomRoof, -1, gap);
            b.Wall.FaceX(HallGap0, HallWall - HallSkin, HallWall + HallSkin, EstateTop, EstateTop + HallHead, 1);
            b.Wall.FaceX(HallGap1, HallWall - HallSkin, HallWall + HallSkin, EstateTop, EstateTop + HallHead, -1);
            b.Wall.FaceY(EstateTop + HallHead, HallGap0, HallGap1, HallWall - HallSkin, HallWall + HallSkin, -1);
            // 玄関の東の壁。この奥は物入れで、中は作らない
            b.Wall.Box(new Vector3(HallEast, (EstateTop + RoomRoof) * 0.5f, (EstateFace + HallWall) * 0.5f),
                new Vector3(0.12f, RoomRoof - EstateTop, EstateFace - HallWall));
            // 抜けの縁。木の枠が回ると、壁に空いた穴ではなく建具の抜けに見える
            for (var i = 0; i < 2; i++)
                b.Set.Box(new Vector3(i == 0 ? HallGap0 : HallGap1, EstateTop + HallHead * 0.5f, HallWall),
                    new Vector3(0.05f, HallHead, HallSkin * 2f + 0.03f));
            b.Set.Box(new Vector3((HallGap0 + HallGap1) * 0.5f, EstateTop + HallHead, HallWall),
                new Vector3(HallGap1 - HallGap0 + 0.1f, 0.05f, HallSkin * 2f + 0.03f));

            // 下駄箱。戸口の正面から見える東の箱と、エレナから見える西の箱
            b.Set.Box(new Vector3(4.92f, EstateTop + 0.47f, -15.17f), new Vector3(0.38f, 0.94f, 0.66f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(4.73f, EstateTop + 0.25f + i * 0.44f, -15.17f),
                    new Vector3(0.02f, 0.36f, 0.58f));
            b.Bright.Box(new Vector3(4.92f, EstateTop + 0.97f, -15.05f), new Vector3(0.20f, 0.03f, 0.14f));
            b.Gear.Box(new Vector3(4.92f, EstateTop + 1.06f, -15.32f), new Vector3(0.16f, 0.22f, 0.16f));
            b.Soft.Box(new Vector3(4.92f, EstateTop + 1.28f, -15.32f), new Vector3(0.26f, 0.28f, 0.24f));
            // **目の高さの物を仕切りの面へ集める。** 廊下に立って戸口の正面から覗くと、
            // 視界の下半分は戸枠に切られて三和土まで届かない。
            // 玄関だと分かる物は、奥の壁の目の高さに掛かっていないと絵に入らない。
            // 姿見は西の下駄箱の上、上着は東の壁際
            b.Set.Box(new Vector3(3.16f, EstateTop + 1.42f, HallWall + HallSkin + 0.02f),
                new Vector3(0.50f, 0.74f, 0.04f));
            b.Bright.Box(new Vector3(3.16f, EstateTop + 1.42f, HallWall + HallSkin + 0.05f),
                new Vector3(0.42f, 0.66f, 0.02f));
            b.Gear.Box(new Vector3(4.87f, EstateTop + 1.80f, HallWall + HallSkin + 0.06f),
                new Vector3(0.40f, 0.04f, 0.04f));
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(4.80f + i * 0.14f, EstateTop + 1.74f, HallWall + HallSkin + 0.06f),
                    new Vector3(0.03f, 0.10f, 0.03f));
            b.Soft.Box(new Vector3(4.80f, EstateTop + 1.30f, HallWall + HallSkin + 0.09f),
                new Vector3(0.34f, 0.86f, 0.14f));
            b.Soft.Box(new Vector3(4.94f, EstateTop + 1.36f, HallWall + HallSkin + 0.08f),
                new Vector3(0.28f, 0.74f, 0.12f));
            // 玄関マット。上から覗いたとき、三和土と框の境がここで一度切れる
            b.Soft.Box(new Vector3(4.15f, EstateTop - EstateSunk + 0.015f, -15.08f),
                new Vector3(0.78f, 0.03f, 0.42f));

            b.Set.Box(new Vector3(3.16f, EstateTop + 0.45f, -15.69f), new Vector3(0.28f, 0.90f, 0.22f));
            b.Red.Box(new Vector3(3.16f, EstateTop + 0.98f, -15.69f), new Vector3(0.16f, 0.16f, 0.16f));
            b.Soft.Box(new Vector3(3.16f, EstateTop + 1.16f, -15.69f), new Vector3(0.26f, 0.24f, 0.22f));

            // 靴。**主と夫の通り道を外して置く。** 通り道に置くと、二人が靴を踏んで歩く
            EstateShoes(b, 3.18f, -15.38f, 8f);
            EstateShoes(b, 3.60f, -15.41f, -6f);
            EstateShoes(b, 4.52f, -15.44f, 14f);

            // 仕切りの居間側。**居間から見ると、ここは幅 4 m の塗っただけの面になる。**
            // 記憶 15 は座って玄関の方を向くので、視界の半分をこの壁が占める
            // 掛ける高さは座った目から 30 度以内に収める。壁の高いところへ掛けると、
            // 立って歩く絵には入っても、座って撮る記憶 15 の絵には一つも入らない
            b.Gear.Box(new Vector3(4.90f, EstateTop + 1.48f, HallWall - HallSkin - 0.03f),
                new Vector3(0.28f, 0.28f, 0.05f));
            b.Bright.Box(new Vector3(4.90f, EstateTop + 1.48f, HallWall - HallSkin - 0.06f),
                new Vector3(0.22f, 0.22f, 0.02f));
            b.Set.Box(new Vector3(5.30f, EstateTop + 1.30f, HallWall - HallSkin - 0.03f),
                new Vector3(0.44f, 0.34f, 0.04f));
            b.Shade.Box(new Vector3(5.30f, EstateTop + 1.30f, HallWall - HallSkin - 0.06f),
                new Vector3(0.34f, 0.24f, 0.02f));
            b.Paper.Box(new Vector3(5.86f, EstateTop + 1.52f, HallWall - HallSkin - 0.03f),
                new Vector3(0.34f, 0.46f, 0.02f));
            b.Bright.Box(new Vector3(4.68f, EstateTop + 1.15f, HallWall - HallSkin - 0.03f),
                new Vector3(0.10f, 0.14f, 0.02f));

            // 傘立て。三和土の隅
            b.Gear.Box(new Vector3(3.14f, EstateTop + 0.07f, -14.98f), new Vector3(0.24f, 0.44f, 0.24f));
            for (var i = 0; i < 2; i++)
                b.Shade.Box(new Vector3(3.10f + i * 0.08f, EstateTop + 0.42f, -14.98f + i * 0.03f),
                    new Vector3(0.05f, 0.74f, 0.05f));
        }

        /// <summary>
        /// 幅木を一本。<paramref name="alongZ"/> が真なら x が一定の壁、偽なら z が一定の壁
        /// </summary>
        static void EstateSkirt(EstateBanks b, float at, float from, float to, bool alongZ)
        {
            var mid = (from + to) * 0.5f;
            var run = Mathf.Abs(to - from);
            b.Set.Box(alongZ ? new Vector3(at, EstateTop + 0.05f, mid) : new Vector3(mid, EstateTop + 0.05f, at),
                alongZ ? new Vector3(0.03f, 0.10f, run) : new Vector3(run, 0.10f, 0.03f));
        }

        /// <summary>靴を一足。三和土の上に並ぶ小さな暗い塊が、土間を土間として読ませる</summary>
        static void EstateShoes(EstateBanks b, float x, float z, float turn)
        {
            var rot = Quaternion.Euler(0f, turn, 0f);
            for (var i = 0; i < 2; i++)
            {
                var at = new Vector3(x + (i == 0 ? -0.09f : 0.09f), EstateTop - EstateSunk + 0.05f, z);
                b.Shade.Box(at, new Vector3(0.11f, 0.10f, 0.26f), rot);
                b.Shade.Box(at + new Vector3(0f, 0.04f, -0.07f), new Vector3(0.11f, 0.08f, 0.10f), rot);
            }
        }

        /// <summary>
        /// 左の戸口の奥（記憶 0・1）。ここも玄関から始める。
        ///
        /// **母の立つところだけ床の高さのまま残す。** 記憶 0 は子どもの目から母の脚を見上げる
        /// 鍵打ちがあり、三和土をそこまで下げると母が 0.15 m 浮く。
        /// 玄関のすのこを敷いて、母はその上に立っていることにする
        /// </summary>
        static void EstatePorch(EstateBanks b)
        {
            b.Tile.FaceY(EstateTop - EstateSunk, PorchX0, PorchX1, PorchSill, EstateFace, 1);
            b.Tile.FaceZ(PorchSill, PorchX0, PorchX1, EstateTop - EstateSunk, EstateTop, 1);
            b.Wall.FaceY(EstateTop, PorchX0, PorchX1, PorchBack, PorchSill, 1);
            b.Wall.FaceX(PorchX0, PorchBack, EstateFace, EstateTop - EstateSunk, EstateTop + 2.2f, 1);
            b.Wall.FaceX(PorchX1, PorchBack, EstateFace, EstateTop - EstateSunk, EstateTop + 2.2f, -1);
            b.Wall.FaceZ(PorchBack, PorchX0, PorchX1, EstateTop, EstateTop + 2.2f, 1);
            b.Set.Box(new Vector3(DoorA, EstateTop - 0.09f, PorchSill + 0.035f),
                new Vector3(PorchX1 - PorchX0, 0.18f, 0.07f));

            // すのこ。板の隙間を暗い線で入れておかないと、ただの踏み台に見える
            b.Set.Box(new Vector3(1.70f, EstateTop - 0.075f, -15.12f), new Vector3(0.80f, 0.15f, 0.50f));
            for (var i = 0; i < 4; i++)
                b.Shade.Box(new Vector3(1.70f, EstateTop + 0.002f, -15.31f + i * 0.126f),
                    new Vector3(0.80f, 0.02f, 0.02f));

            // 下駄箱。戸口の抜けに掛からないよう西の壁へ寄せる
            b.Set.Box(new Vector3(1.00f, EstateTop + 0.325f, -15.18f), new Vector3(0.28f, 0.95f, 0.72f));
            b.Gear.Box(new Vector3(1.00f, EstateTop + 0.86f, -15.18f), new Vector3(0.18f, 0.14f, 0.20f));
            EstateShoes(b, 1.28f, -15.42f, -10f);
            EstateShoes(b, 2.10f, -15.38f, 16f);

            // 奥の物。光る面の手前へ影になる物を置く。
            // 何も無いと、記憶 1 が戸口へ向いたとき、視界の半分が真っ白な板になる
            b.Shade.Box(new Vector3(DoorA, EstateTop + 0.26f, -16.18f), new Vector3(1.20f, 0.52f, 0.30f));
            b.Shade.Box(new Vector3(DoorA, EstateTop + 1.86f, -16.24f), new Vector3(1.34f, 0.07f, 0.22f));
            b.Soft.Box(new Vector3(DoorA - 0.36f, EstateTop + 1.36f, -16.24f), new Vector3(0.36f, 0.92f, 0.12f));
            b.Soft.Box(new Vector3(DoorA + 0.40f, EstateTop + 1.44f, -16.24f), new Vector3(0.30f, 0.76f, 0.12f));
        }

        // ---- 戸口 --------------------------------------------------------------

        /// <summary>
        /// 戸口ひとつ。枠・番号板・郵便受け・メーター・表札・呼び鈴まで。
        ///
        /// 壁に開けた穴だけでは戸口に見えない。枠と、脇に並ぶ小物が付いて初めて
        /// 「人が出入りする戸」として読める。三つとも同じ組み合わせにして、並びを強める。
        ///
        /// 左と真ん中の戸は開いている。記憶 0 は母が戸口に立ち、記憶 8 は新聞を持って中へ入り、
        /// 記憶 15 は中から夫を迎える。閉めた戸を置くと、その三つが同じ場所で成り立たなくなる。
        /// 右は中を作っていないので閉めておく
        /// </summary>
        static void EstateDoorway(EstateBanks b, float x, bool open)
        {
            const float jamb = DoorHalf + 0.06f;
            b.Gear.Box(new Vector3(x - jamb, EstateTop + (DoorHigh + 0.12f) * 0.5f, EstateFace + 0.05f),
                new Vector3(0.12f, DoorHigh + 0.12f, 0.1f));
            b.Gear.Box(new Vector3(x + jamb, EstateTop + (DoorHigh + 0.12f) * 0.5f, EstateFace + 0.05f),
                new Vector3(0.12f, DoorHigh + 0.12f, 0.1f));
            b.Gear.Box(new Vector3(x, EstateTop + DoorHigh + 0.06f, EstateFace + 0.05f),
                new Vector3(DoorHalf * 2f + 0.24f, 0.12f, 0.1f));

            // 番号板。廊下でいちばん明るい小さな四角になるので、戸口の位置が遠くからでも拾える
            b.Bright.Box(new Vector3(x - 0.75f, EstateTop + 1.80f, EstateFace + 0.05f), new Vector3(0.24f, 0.15f, 0.02f));
            // 表札と呼び鈴。番号板の下へ縦に重ねる。
            // 戸の右は開いた戸が倒れてくる側なので、住戸の札はすべて左へ寄せる
            b.Paper.Box(new Vector3(x - 0.75f, EstateTop + 1.64f, EstateFace + 0.05f),
                new Vector3(0.22f, 0.10f, 0.02f), Quaternion.Euler(0f, 0f, -3f));
            b.Gear.Box(new Vector3(x - 0.75f, EstateTop + 1.46f, EstateFace + 0.06f), new Vector3(0.09f, 0.13f, 0.03f));
            b.Bright.Box(new Vector3(x - 0.75f, EstateTop + 1.47f, EstateFace + 0.08f), new Vector3(0.04f, 0.04f, 0.02f));
            // 郵便受け
            b.Gear.Box(new Vector3(x - 0.75f, EstateTop + 1.28f, EstateFace + 0.06f), new Vector3(0.32f, 0.24f, 0.12f));
            b.Shade.Box(new Vector3(x - 0.75f, EstateTop + 1.34f, EstateFace + 0.125f), new Vector3(0.22f, 0.04f, 0.02f));
            // メーターの箱。硝子の面だけ明るい
            b.Gear.Box(new Vector3(x + 0.80f, EstateTop + 1.42f, EstateFace + 0.09f), new Vector3(0.36f, 0.46f, 0.18f));
            b.Bright.Box(new Vector3(x + 0.80f, EstateTop + 1.50f, EstateFace + 0.185f), new Vector3(0.18f, 0.18f, 0.02f));

            if (open)
            {
                // **開いた戸はメーターの箱より手前へ置く。** 面に貼り付けると、
                // 戸の板の中からメーターの角が生えてくる
                EstateLeaf(b, x + 0.92f, 0.43f, EstateFace + 0.26f);
                b.Gear.Box(new Vector3(x + 1.26f, EstateTop + 1.0f, EstateFace + 0.31f), new Vector3(0.05f, 0.05f, 0.12f));
                // 丁番。戸が壁から浮いて見えないように、枠との間を三つで繋ぐ
                for (var i = 0; i < 3; i++)
                    b.Gear.Box(new Vector3(x + 0.51f, EstateTop + 0.40f + i * 0.62f, EstateFace + 0.16f),
                        new Vector3(0.07f, 0.16f, 0.22f));
            }
            else
            {
                EstateLeaf(b, x, DoorHalf, EstateFace + 0.05f);
                b.Gear.Box(new Vector3(x + 0.32f, EstateTop + 1.0f, EstateFace + 0.1f), new Vector3(0.05f, 0.05f, 0.12f));
            }
        }

        /// <summary>
        /// 戸一枚。鉄扉として読ませる。
        ///
        /// 平らな板のままでは物置の戸に見えるので、上に換気口、下に新聞受け、
        /// 目の高さに覗き穴を入れる。どれも住戸の戸にしか付かない
        /// </summary>
        static void EstateLeaf(EstateBanks b, float cx, float half, float z)
        {
            b.Leaf.Box(new Vector3(cx, EstateTop + DoorHigh * 0.5f, z), new Vector3(half * 2f, DoorHigh, 0.05f));
            var face = z + 0.026f;
            // 面を囲う細い線。鉄扉の折り返しの縁
            b.Shade.Box(new Vector3(cx, EstateTop + 1.06f, face), new Vector3(half * 2f - 0.10f, 1.52f, 0.015f));
            // 換気口。細い羽根が三枚
            for (var i = 0; i < 3; i++)
                b.Shade.Box(new Vector3(cx, EstateTop + 1.80f + i * 0.07f, face + 0.01f),
                    new Vector3(half * 1.1f, 0.035f, 0.02f));
            // 新聞受けと、差さったままの新聞
            b.Shade.Box(new Vector3(cx, EstateTop + 0.34f, face + 0.01f), new Vector3(half * 0.86f, 0.05f, 0.02f));
            b.Gear.Box(new Vector3(cx, EstateTop + 0.34f, face), new Vector3(half * 1.0f, 0.13f, 0.015f));
            // 覗き穴。目の高さの黒い点ひとつで、そこが住戸の戸になる
            b.Shade.Box(new Vector3(cx, EstateTop + 1.52f, face + 0.01f), new Vector3(0.045f, 0.045f, 0.02f));
            b.Gear.Box(new Vector3(cx, EstateTop + 1.52f, face), new Vector3(0.07f, 0.07f, 0.015f));
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
