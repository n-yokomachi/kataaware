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
