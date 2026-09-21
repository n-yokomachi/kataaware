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
        // 外階段を一本、まっすぐ上へ通す。折り返しにしないのは、記憶 0 の子どもが
        // 下から三階まで一息に駆け上がるのを、道筋を折らずに鍵打ちで書けるようにするため。
        // 三階の廊下に戸口が三つ並び、左が記憶 0・1 の家、真ん中が記憶 8・15 の家

        /// <summary>階の高さ。m</summary>
        public const float Floor = 2.8f;
        /// <summary>一階ぶんの階段が z へ伸びる長さ。勾配は 34 度で、実際の外階段に近い</summary>
        const float Flight = 4.2f;
        /// <summary>踊り場の奥行き</summary>
        const float LandingDeep = 1.4f;
        /// <summary>階段の半幅。幅 1.2 m</summary>
        const float StairHalf = 0.6f;
        /// <summary>一階ぶんの段数</summary>
        const int StepCount = 16;

        /// <summary>階段の下端。ここから -z へ上がる</summary>
        public const float EstateFoot = 2.0f;
        /// <summary>一つ目の踊り場の真ん中</summary>
        public const float EstateLanding1 = EstateFoot - Flight - LandingDeep * 0.5f;      // -2.9
        /// <summary>二つ目の踊り場の真ん中</summary>
        public const float EstateLanding2 = EstateLanding1 - LandingDeep * 0.5f - Flight - LandingDeep * 0.5f; // -8.5
        /// <summary>三階の廊下の真ん中</summary>
        public const float EstateWalk = EstateLanding2 - LandingDeep * 0.5f - Flight - LandingDeep * 0.5f;     // -14.1
        /// <summary>建物の面。戸口はここに開く</summary>
        public const float EstateFace = EstateWalk - LandingDeep * 0.5f;                   // -14.8
        /// <summary>三階の高さ</summary>
        public const float EstateTop = Floor * 3f;                                         // 8.4

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
        /// <summary>建物の面の右端</summary>
        const float WallRight = 8.1f;
        /// <summary>廊下の手すり側の縁</summary>
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


        // ---- 団地の外階段 ------------------------------------------------------

        /// <summary>
        /// 記憶 0・1・8・15 の舞台。外階段と三階の廊下、右の家の中まで。
        ///
        /// 左の家（戸口 A）は中まで作らず、戸口の奥に浅い窪みと灯りだけを置く。
        /// 記憶 0 が振り返ったときに母が逆光の影になるのは、この奥の灯りのため
        /// </summary>
        static void Estate(Transform place)
        {
            // 素材ごとに一つずつ。溜めてから最後に一枚へ焼く
            var crete = EstatePaint("EstateCrete", new Color(0.400f, 0.394f, 0.372f), 0.06f);
            var skin = EstatePaint("EstateSkin", new Color(0.208f, 0.204f, 0.196f), 0.05f);
            var pale = Glow("EstatePale", new Color(0.86f, 0.87f, 0.90f), 0.62f);
            var bulbs = Glow("EstateBulb", new Color(1f, 0.93f, 0.80f), 1.50f);

            var slab = new Bank { Texel = 0.35f };
            // 地面。隣の棟の足元まで伸ばす。ここで切ると、棟が虚空に浮いて見える
            slab.FaceY(0f, -14f, 25f, -20f, BlockFace, 1);
            // 踊り場と三階の廊下
            slab.Box(new Vector3(0f, Floor - 0.09f, EstateLanding1), new Vector3(StairHalf * 2f, 0.18f, LandingDeep));
            slab.Box(new Vector3(0f, Floor * 2f - 0.09f, EstateLanding2), new Vector3(StairHalf * 2f, 0.18f, LandingDeep));
            slab.Box(new Vector3((WalkRight - StairHalf) * 0.5f, EstateTop - 0.09f, EstateWalk),
                new Vector3(WalkRight + StairHalf, 0.18f, LandingDeep));
            // 廊下の天井。裸電球を吊るす面が要る。頭上 2.3 m なので当たりが入っていても触らない
            slab.Box(new Vector3((WalkRight - StairHalf) * 0.5f, WalkRoof + 0.1f, EstateWalk),
                new Vector3(WalkRight + StairHalf, 0.2f, LandingDeep + 0.3f));

            // 段鼻と番号板と硝子。廊下でいちばん明るい細かい面をここへ集める
            var bright = new Bank { Texel = 0.5f };
            EstateFlight(slab, bright, EstateFoot, 0f);
            EstateFlight(slab, bright, EstateLanding1 - LandingDeep * 0.5f, Floor);
            EstateFlight(slab, bright, EstateLanding2 - LandingDeep * 0.5f, Floor * 2f);
            EstateEmit(place, "EstateFloor", slab, crete, true);

            var wall = new Bank { Texel = 0.3f };
            // 階段が背にしている壁。ここが無いと、階段が虚空に掛かっているように見える
            wall.FaceX(-0.9f, EstateFace, 3f, 0f, 11.2f, 1);
            // 建物の面。三階に戸口を二つ開ける。三つ目は閉まっているので抜かない
            var holes = new List<Vector4>
            {
                new Vector4(DoorA - DoorHalf, DoorA + DoorHalf, EstateTop, EstateTop + DoorHigh),
                new Vector4(DoorB - DoorHalf, DoorB + DoorHalf, EstateTop, EstateTop + DoorHigh),
            };
            wall.FaceZHoles(EstateFace, -1.2f, WallRight, 0f, 11.2f, 1, holes);
            // 左の戸口の奥の窪み。奥を塞いでおかないと、窪みへ入った先が虚空になる
            wall.FaceX(DoorA - 0.75f, -16.4f, EstateFace, EstateTop, EstateTop + 2.2f, 1);
            wall.FaceX(DoorA + 0.75f, -16.4f, EstateFace, EstateTop, EstateTop + 2.2f, -1);
            wall.FaceZ(-16.4f, DoorA - 0.75f, DoorA + 0.75f, EstateTop, EstateTop + 2.2f, 1);
            wall.FaceY(EstateTop, DoorA - 0.75f, DoorA + 0.75f, -16.4f, EstateFace, 1);
            // 右の家。四方と天井。**内側の面にも戸口を抜く。**
            // 抜かずに張ると、廊下から戸口を通り抜けられず、中からは壁が一枚塞いでいるように見える
            wall.FaceX(RoomX0, RoomBack, EstateFace, EstateTop, RoomRoof, 1);
            wall.FaceX(RoomX1, RoomBack, EstateFace, EstateTop, RoomRoof, -1);
            wall.FaceZ(RoomBack, RoomX0, RoomX1, EstateTop, RoomRoof, 1);
            wall.FaceZHoles(EstateFace - 0.02f, RoomX0, RoomX1, EstateTop, RoomRoof, -1,
                new List<Vector4> { new Vector4(DoorB - DoorHalf, DoorB + DoorHalf, EstateTop, EstateTop + DoorHigh) });
            EstateEmit(place, "EstateWall", wall, skin, true);

            var shade = new Bank { Texel = 0.3f };
            shade.FaceY(RoomRoof, RoomX0, RoomX1, RoomBack, EstateFace, -1);
            shade.FaceY(EstateTop + 2.2f, DoorA - 0.75f, DoorA + 0.75f, -16.4f, EstateFace, -1);
            var lit = new Bank { Texel = 0.4f };
            // 下の階の窓。三階の戸口と同じ x に並べる。階段の下から見上げたとき、
            // 戸口の列がそのまま下へ続いているのが見えて、建物の高さが出る
            for (var f = 0; f < 2; f++)
            {
                for (var c = 0; c < 3; c++)
                {
                    var y = 1.3f + f * Floor;
                    var x = DoorA + c * (DoorB - DoorA);
                    var into = (f == 0 && c == 1) ? lit : shade;
                    into.FaceZ(EstateFace + 0.03f, x - 0.65f, x + 0.65f, y, y + 1.25f, 1);
                }
            }
            // 階段脇の壁の窓。一枚の板のままだと、階段の隣に何も無い崖が立っているように見える
            for (var f = 0; f < 4; f++)
            {
                for (var c = 0; c < 4; c++)
                {
                    var y = 1.5f + f * Floor;
                    var z = -12f + c * 3.8f;
                    var into = (f == 1 && c == 3) ? lit : shade;
                    into.FaceX(-0.88f, z - 0.62f, z + 0.62f, y, y + 1.15f, 1);
                }
            }

            var rail = new Bank { Texel = 0.5f };
            EstateSlope(rail, EstateFoot, 0f);
            EstateBar(rail, EstateLanding1 - LandingDeep * 0.5f, EstateLanding1 + LandingDeep * 0.5f, Floor);
            EstateSlope(rail, EstateLanding1 - LandingDeep * 0.5f, Floor);
            EstateBar(rail, EstateLanding2 - LandingDeep * 0.5f, EstateLanding2 + LandingDeep * 0.5f, Floor * 2f);
            EstateSlope(rail, EstateLanding2 - LandingDeep * 0.5f, Floor * 2f);
            // 三階の廊下の外側。**階段の口（x -0.6〜0.6）は空ける。**
            // 手すりに当たりを入れたので、廊下の長さぶん通すと階段を上がってきた先が塞がる。
            // 一度通した版では、上がり切ったところで壁に突き当たって廊下へ出られなかった
            for (var i = 0; i < 2; i++)
            {
                var y = EstateTop + 0.5f + i * 0.45f;
                rail.Box(new Vector3((WalkRight + StairHalf) * 0.5f, y, WalkFront),
                    new Vector3(WalkRight - StairHalf, 0.05f, 0.05f));
            }
            // 支柱は 0.9 m 間隔。一本の帯だけでは、廊下ではなく宙に張った線に見える
            for (var i = 0; i < 8; i++)
                rail.Box(new Vector3(0.65f + i * 0.9f, EstateTop + 0.48f, WalkFront),
                    new Vector3(0.05f, 0.96f, 0.05f));
            rail.Box(new Vector3(WalkRight - 0.05f, EstateTop + 0.48f, WalkFront), new Vector3(0.05f, 0.96f, 0.05f));
            EstateEmit(place, "EstateRail", rail, Mat("Rail"), true);

            // 左と真ん中の戸は開いている。記憶 0 は母が戸口に立ち、記憶 8 は新聞を持って中へ入り、
            // 記憶 15 は中から夫を迎える。閉めた戸を置くと、その三つが同じ場所で成り立たなくなる。
            // 右は中を作っていないので閉めておく
            var leaf = new Bank { Texel = 0.4f };
            var gear = new Bank { Texel = 0.5f };
            EstateDoorway(gear, leaf, bright, shade, DoorA, true);
            EstateDoorway(gear, leaf, bright, shade, DoorB, true);
            EstateDoorway(gear, leaf, bright, shade, DoorC, false);
            EstateEmit(place, "EstateDoor", leaf, Mat("Door"), false);

            var soft = new Bank { Texel = 0.5f };
            var linen = new Bank { Texel = 0.5f };
            EstateGear(gear, linen, soft, shade, bright, lit);

            var set = new Bank { Texel = 0.5f };
            EstateRoom(place, set, soft, linen, gear, shade);

            EstateEmit(place, "EstateGear", gear, Mat("Rail"), false);
            EstateEmit(place, "EstateSoft", soft, Mat("Cloth"), false);
            EstateEmit(place, "EstateLinen", linen, crete, false);
            EstateEmit(place, "EstateTimber", set, Mat("Timber"), false);
            // 段鼻に当たりを入れると、階段が一段ごとに凸凹になって上がれなくなる
            EstateEmit(place, "EstatePale", bright, pale, false);

            EstateBlockWall(place, lit);
            EstateEmit(place, "EstateShade", shade, Mat("Ceiling"), false);
            NoShadow(EstateEmit(place, "EstateLit", lit, bulbs, false));

            // 戸口の奥とテレビ。灯りではなく光る面で済ませる。
            // 記憶 0 の母も記憶 15 の夫も、この光の手前に立つので影にしか見えない
            Pane(place, "EstateHallLight", new Vector3(DoorA, EstateTop + 1.1f, -16.35f),
                new Vector2(1.4f, 2.1f), Vector3.forward, Glow("GlowHall", new Color(1f, 0.90f, 0.74f), 0.55f));
            Pane(place, "EstateTv", EstateTv + new Vector3(0f, 0f, 0.06f),
                new Vector2(0.72f, 0.44f), Vector3.forward, Glow("GlowTv", new Color(0.72f, 0.82f, 1f), 1.35f));
            // 部屋の蛍光灯。廊下の電球と同じ色にして、この場所の灯りを一色に揃える
            Pane(place, "EstateTube", new Vector3(5.0f, RoomRoof - 0.12f, -16.9f),
                new Vector2(1.15f, 0.2f), Vector3.down, bulbs);

            // 地面の端から先は虚空なので、見えない仕切りで囲う。
            // 建物の中まで歩ける必要は無いが、裏へ回れても困らないので地面ごと囲う
            Ring(place, "EstateFence", new Vector2(-13.6f, 24.6f), new Vector2(-19.6f, BlockFace - 0.5f), 3.5f);
            // 階段と三階の廊下の外側は手すりが受け持つ。EstateRail に当たりを入れてあるので、
            // ここへ仕切りを重ねると階段を上がってきた先が塞がる。
            // **廊下の両端だけは手すりが無い。** 歩いて確かめたら、廊下を +x へ歩き切って
            // 三階から地面へ落ちた。絵に手すりを増やさずに止めたいので、仕切りで塞ぐ
            Fence(place, "EstateWalkEndA", new Vector3(-0.7f, EstateTop + 0.75f, EstateWalk),
                new Vector3(0.2f, 1.5f, LandingDeep));
            Fence(place, "EstateWalkEndB", new Vector3(WalkRight + 0.1f, EstateTop + 0.75f, EstateWalk),
                new Vector3(0.2f, 1.5f, LandingDeep));

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
            // 右の家は蛍光灯の下。テレビの光だけでは卓も座布団も沈む
            Lamp(place, "RoomB", LightType.Point, new Vector3(5.0f, RoomRoof - 0.35f, -17.1f),
                Vector3.zero, new Color(1f, 0.93f, 0.84f), 3.4f, 7f);
            // テレビは光る面だけでは床に届かない。記憶 15 の最後の鍵打ちが
            // 「テレビの光が床に当たっている」を見るので、青い灯りを一つ据える
            Lamp(place, "TvGlow", LightType.Point, EstateTv + new Vector3(0f, -0.15f, 0.55f),
                Vector3.zero, new Color(0.62f, 0.76f, 1f), 1.8f, 3.6f);
        }

        /// <summary>
        /// 戸口ひとつ。枠・番号板・取っ手・郵便受け・メーターの箱まで。
        ///
        /// 壁に開けた穴だけでは戸口に見えない。枠と、脇に並ぶ小物が付いて初めて
        /// 「人が出入りする戸」として読める。三つとも同じ組み合わせにして、並びを強める
        /// </summary>
        static void EstateDoorway(Bank gear, Bank leaf, Bank bright, Bank shade, float x, bool open)
        {
            const float jamb = DoorHalf + 0.06f;
            gear.Box(new Vector3(x - jamb, EstateTop + (DoorHigh + 0.12f) * 0.5f, EstateFace + 0.05f),
                new Vector3(0.12f, DoorHigh + 0.12f, 0.1f));
            gear.Box(new Vector3(x + jamb, EstateTop + (DoorHigh + 0.12f) * 0.5f, EstateFace + 0.05f),
                new Vector3(0.12f, DoorHigh + 0.12f, 0.1f));
            gear.Box(new Vector3(x, EstateTop + DoorHigh + 0.06f, EstateFace + 0.05f),
                new Vector3(DoorHalf * 2f + 0.24f, 0.12f, 0.1f));

            // 番号板。廊下でいちばん明るい小さな四角になるので、戸口の位置が遠くからでも拾える
            bright.Box(new Vector3(x - 0.75f, EstateTop + 1.80f, EstateFace + 0.05f), new Vector3(0.24f, 0.15f, 0.02f));
            // 郵便受け
            gear.Box(new Vector3(x - 0.75f, EstateTop + 1.28f, EstateFace + 0.06f), new Vector3(0.32f, 0.24f, 0.12f));
            shade.Box(new Vector3(x - 0.75f, EstateTop + 1.34f, EstateFace + 0.125f), new Vector3(0.22f, 0.04f, 0.02f));
            // メーターの箱。硝子の面だけ明るい
            gear.Box(new Vector3(x + 0.80f, EstateTop + 1.42f, EstateFace + 0.09f), new Vector3(0.36f, 0.46f, 0.18f));
            bright.Box(new Vector3(x + 0.80f, EstateTop + 1.50f, EstateFace + 0.185f), new Vector3(0.18f, 0.18f, 0.02f));

            if (open)
            {
                leaf.Box(new Vector3(x + 0.92f, EstateTop + DoorHigh * 0.5f, EstateFace + 0.06f),
                    new Vector3(0.86f, DoorHigh, 0.05f));
                gear.Box(new Vector3(x + 1.26f, EstateTop + 1.0f, EstateFace + 0.11f), new Vector3(0.05f, 0.05f, 0.12f));
            }
            else
            {
                leaf.Box(new Vector3(x, EstateTop + DoorHigh * 0.5f, EstateFace + 0.05f),
                    new Vector3(DoorHalf * 2f, DoorHigh, 0.05f));
                gear.Box(new Vector3(x + 0.32f, EstateTop + 1.0f, EstateFace + 0.1f), new Vector3(0.05f, 0.05f, 0.12f));
            }
        }

        /// <summary>
        /// 廊下に出ている暮らしの物。物干し竿と洗濯物、室外機、洗濯機、裸電球、戸口の奥。
        ///
        /// 戸口と手すりだけの廊下は事務所の通路に見える。人が住んでいる証しをここで置く。
        /// 床に置く物は、開いた戸の前を避けて戸と戸のあいだへ寄せる
        /// </summary>
        static void EstateGear(Bank gear, Bank linen, Bank soft, Bank shade, Bank bright, Bank lit)
        {
            // 物干し竿。戸口 C の前に寄せて、廊下を +x へ見通したときの奥行きに掛ける
            gear.Box(new Vector3(6.05f, EstateTop + 1.90f, WalkFront - 0.11f), new Vector3(2.5f, 0.045f, 0.045f));
            for (var i = 0; i < 4; i++)
            {
                var x = 5.0f + i * 0.6f;
                if (i == 2) soft.Box(new Vector3(x, EstateTop + 1.62f, WalkFront - 0.11f), new Vector3(0.34f, 0.5f, 0.02f));
                else linen.Box(new Vector3(x, EstateTop + 1.55f, WalkFront - 0.11f), new Vector3(0.46f, 0.66f, 0.02f));
            }

            // 室外機。羽根の線を三本入れると、ただの箱と見分けが付く
            gear.Box(new Vector3(3.3f, EstateTop + 0.30f, EstateFace + 0.25f), new Vector3(0.80f, 0.58f, 0.36f));
            for (var i = 0; i < 3; i++)
                shade.Box(new Vector3(3.3f, EstateTop + 0.18f + i * 0.12f, EstateFace + 0.44f),
                    new Vector3(0.62f, 0.05f, 0.02f));

            // 洗濯機
            linen.Box(new Vector3(5.8f, EstateTop + 0.43f, EstateFace + 0.34f), new Vector3(0.60f, 0.86f, 0.56f));
            shade.Box(new Vector3(5.8f, EstateTop + 0.87f, EstateFace + 0.34f), new Vector3(0.44f, 0.03f, 0.40f));
            bright.Box(new Vector3(5.8f, EstateTop + 0.80f, EstateFace + 0.63f), new Vector3(0.36f, 0.06f, 0.02f));

            // 左の戸口の奥。光る面の手前へ影になる物を置く。
            // 何も無いと、記憶 1 が戸口へ向いたとき、視界の半分が真っ白な板になる
            shade.Box(new Vector3(DoorA, EstateTop + 0.26f, -16.18f), new Vector3(1.20f, 0.52f, 0.30f));
            shade.Box(new Vector3(DoorA, EstateTop + 1.86f, -16.24f), new Vector3(1.34f, 0.07f, 0.22f));
            soft.Box(new Vector3(DoorA - 0.36f, EstateTop + 1.36f, -16.24f), new Vector3(0.36f, 0.92f, 0.12f));
            soft.Box(new Vector3(DoorA + 0.40f, EstateTop + 1.44f, -16.24f), new Vector3(0.30f, 0.76f, 0.12f));

            // 裸電球。コードは細く暗く、球だけ光らせる
            for (var i = 0; i < 3; i++)
            {
                var x = 0.9f + i * 3f;
                gear.Box(new Vector3(x, EstateTop + 2.18f, EstateWalk), new Vector3(0.025f, 0.24f, 0.025f));
                lit.Box(new Vector3(x, EstateTop + 1.98f, EstateWalk), new Vector3(0.12f, 0.16f, 0.12f));
            }
        }

        /// <summary>
        /// 隣の棟。窓の四角が等間隔に並ぶだけの書き割り。
        ///
        /// **灯りを当てない。** 面を Unlit で持たせると、朝日が回らない側でも値が決まり、
        /// 夜明けの薄い空を背にした輪郭がそのまま出る。当たりも影も持たせない
        /// </summary>
        static void EstateBlockWall(Transform place, Bank lit)
        {
            var slabs = new Bank { Texel = 0.4f };
            slabs.FaceZ(BlockFace, -13.5f, 24.5f, 0f, 15.6f, -1);
            NoShadow(EstateEmit(place, "EstateBlock", slabs,
                Glow("EstateBlock", new Color(0.50f, 0.53f, 0.60f), 0.30f), false));

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
                    var into = (i * 5 + j * 3) % 13 == 0 ? lit : panes;
                    into.FaceZ(BlockFace - 0.16f, x - 0.72f, x + 0.72f, y, y + 1.15f, -1);
                }
            }
            NoShadow(EstateEmit(place, "EstateBlockPane", panes, Mat("Ceiling"), false));
        }

        /// <summary>
        /// 右の家の中（記憶 15）。テレビ・卓・座布団・カーテン・天井の蛍光灯。
        ///
        /// 座って見る絵なので、目の高さ 1.15 m から卓とテレビが同時に入るように寄せる
        /// </summary>
        static void EstateRoom(Transform place, Bank set, Bank soft, Bank linen, Bank gear, Bank shade)
        {
            // 床板。廊下と同じ土間の色では、戸を跨いで中へ入ったことが伝わらない
            var board = new Bank { Texel = 0.35f };
            board.FaceY(EstateTop, RoomX0, RoomX1, RoomBack, EstateFace, 1);
            EstateEmit(place, "EstateRoomFloor", board,
                EstatePaint("EstateBoard", new Color(0.300f, 0.268f, 0.205f), 0.06f), true);

            soft.Box(new Vector3(4.9f, EstateTop + 0.22f, -16.6f), new Vector3(1.9f, 0.44f, 0.72f));
            soft.Box(new Vector3(4.9f, EstateTop + 0.52f, -16.28f), new Vector3(1.9f, 0.6f, 0.14f));

            set.Box(new Vector3(EstateTv.x, EstateTop + 0.25f, EstateTv.z - 0.14f), new Vector3(1.0f, 0.5f, 0.42f));
            set.Box(EstateTv + new Vector3(0f, 0f, -0.05f), new Vector3(0.82f, 0.54f, 0.14f));

            // 卓。天板と脚
            set.Box(new Vector3(4.95f, EstateTop + 0.37f, -17.62f), new Vector3(1.10f, 0.06f, 0.66f));
            for (var i = 0; i < 4; i++)
                set.Box(new Vector3(4.95f + ((i & 1) == 0 ? -0.48f : 0.48f), EstateTop + 0.17f,
                    -17.62f + ((i & 2) == 0 ? -0.26f : 0.26f)), new Vector3(0.07f, 0.34f, 0.07f));

            // 座布団
            soft.Box(new Vector3(4.05f, EstateTop + 0.05f, -17.62f), new Vector3(0.56f, 0.10f, 0.56f));
            soft.Box(new Vector3(5.88f, EstateTop + 0.05f, -17.58f), new Vector3(0.56f, 0.10f, 0.56f));

            // カーテン。襞は板の前後をずらして出す。テレビの前だけ開けておく
            gear.Box(new Vector3(4.95f, EstateTop + 2.12f, RoomBack + 0.12f), new Vector3(3.9f, 0.05f, 0.05f));
            for (var i = 0; i < 5; i++)
                linen.Box(new Vector3(3.15f + i * 0.32f, EstateTop + 1.12f, RoomBack + 0.10f + (i % 2) * 0.06f),
                    new Vector3(0.30f, 1.92f, 0.05f));
            for (var i = 0; i < 3; i++)
                linen.Box(new Vector3(6.00f + i * 0.32f, EstateTop + 1.12f, RoomBack + 0.10f + (i % 2) * 0.06f),
                    new Vector3(0.30f, 1.92f, 0.05f));

            // 蛍光灯の笠。光る面そのものは Estate が板で置く
            shade.Box(new Vector3(5.0f, RoomRoof - 0.05f, -16.9f), new Vector3(1.3f, 0.1f, 0.32f));
        }

        /// <summary>一階ぶんの段。踏み面と蹴込みを一段ずつ張り、角へ段鼻を跨がせる</summary>
        static void EstateFlight(Bank b, Bank nose, float zFoot, float yFoot)
        {
            var d = Flight / StepCount;
            var r = Floor / StepCount;
            for (var i = 0; i < StepCount; i++)
            {
                b.FaceZ(zFoot - i * d, -StairHalf, StairHalf, yFoot + i * r, yFoot + (i + 1) * r, 1);
                b.FaceY(yFoot + (i + 1) * r, -StairHalf, StairHalf, zFoot - (i + 1) * d, zFoot - i * d, 1);
                // 段鼻は角へ跨がせる。踏み面の上に載せるだけでは、見上げたとき蹴込みしか見えず、
                // 段が数えられない。下から見上げる絵がこの場所のいちばん長い絵なので、そこを優先する
                nose.Box(new Vector3(0f, yFoot + (i + 1) * r - 0.006f, zFoot - i * d + 0.008f),
                    new Vector3(StairHalf * 2f - 0.04f, 0.05f, 0.075f));
            }
            // 段の裏。下から見上げたときに段が宙に浮いて見えないように、斜めの板を一枚通す
            var dir = new Vector3(0f, Floor, -Flight).normalized;
            b.Box(new Vector3(0f, yFoot + Floor * 0.5f - 0.24f, zFoot - Flight * 0.5f),
                new Vector3(StairHalf * 2f, 0.3f, Mathf.Sqrt(Flight * Flight + Floor * Floor)),
                Quaternion.LookRotation(dir, Vector3.up));
        }

        /// <summary>斜めの手すり。段と同じ勾配で二本通し、支柱を 0.9 m 間隔で立てる</summary>
        static void EstateSlope(Bank b, float zFoot, float yFoot)
        {
            var dir = new Vector3(0f, Floor, -Flight).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var len = Mathf.Sqrt(Flight * Flight + Floor * Floor);
            for (var i = 0; i < 2; i++)
            {
                var lift = 0.55f + i * 0.45f;
                b.Box(new Vector3(StairHalf, yFoot + Floor * 0.5f + lift, zFoot - Flight * 0.5f),
                    new Vector3(0.05f, 0.05f, len), rot);
            }
            var count = Mathf.FloorToInt(len / 0.9f) + 1;
            for (var i = 0; i < count; i++)
            {
                var k = (0.45f + i * 0.9f) / len;
                if (k > 1f) break;
                b.Box(new Vector3(StairHalf, yFoot + Floor * k + 0.48f, zFoot - Flight * k),
                    new Vector3(0.05f, 0.96f, 0.05f));
            }
        }

        /// <summary>踊り場の手すり。水平に二本と、間隔を階段に合わせた支柱</summary>
        static void EstateBar(Bank b, float z0, float z1, float y)
        {
            for (var i = 0; i < 2; i++)
                b.Box(new Vector3(StairHalf, y + 0.5f + i * 0.45f, (z0 + z1) * 0.5f),
                    new Vector3(0.05f, 0.05f, z1 - z0));
            b.Box(new Vector3(StairHalf, y + 0.48f, z0 + 0.05f), new Vector3(0.05f, 0.96f, 0.05f));
            b.Box(new Vector3(StairHalf, y + 0.48f, (z0 + z1) * 0.5f), new Vector3(0.05f, 0.96f, 0.05f));
            b.Box(new Vector3(StairHalf, y + 0.48f, z1 - 0.05f), new Vector3(0.05f, 0.96f, 0.05f));
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
