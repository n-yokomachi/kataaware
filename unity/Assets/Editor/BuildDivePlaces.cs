using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の五つの場所。
    ///
    /// **どれも暗がりと色味で誤魔化せる最小の箱にする。** 記憶は他人の頭から抜いてきた絵で、
    /// 隅々まで見えている必要がない。天井と壁を暗く、床だけ少し明るく、灯りは一つ。
    /// 明るさと色は記憶ごとに Volume が寄せるので、ここは形だけを持つ。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 目印になる点は const にして両方から引く
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 団地 ------------------------------------------------------------
        //
        // 外階段を一本、まっすぐ上へ通す。折り返しにしないのは、記憶 0 の子どもが
        // 下から三階まで一息に駆け上がるのを、道筋を折らずに鍵打ちで書けるようにするため。
        // 三階の廊下に戸口が二つ並び、左が記憶 0・1 の家、右が記憶 8・15 の家

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
        /// <summary>右の戸口の真ん中。記憶 8 のジョルジョと、記憶 15 のエレナの家</summary>
        public const float DoorB = 4.2f;
        const float DoorHalf = 0.45f;
        const float DoorHigh = 2.0f;

        /// <summary>右の家の中。テレビの前</summary>
        public static readonly Vector3 EstateTv = new Vector3(5.3f, EstateTop + 0.85f, -18.3f);

        // ---- 公園 ------------------------------------------------------------

        /// <summary>左のベンチ。記憶 2 のアルベルトが掛けている</summary>
        public static readonly Vector3 BenchA = new Vector3(0f, 0f, 0f);
        /// <summary>右のベンチ。記憶 9 のローザが餌の袋を畳んでいる</summary>
        public static readonly Vector3 BenchB = new Vector3(2.6f, 0f, 0f);
        /// <summary>門。ベンチから真っ直ぐ +z</summary>
        public const float GateZ = 9.4f;
        const float GateHalf = 1.3f;
        /// <summary>池の縁の中心と半径。鳩はこの手前に集まる</summary>
        public static readonly Vector3 Pond = new Vector3(-4.2f, 0f, 4.6f);
        const float PondR = 2.8f;
        /// <summary>鳩の群れの真ん中</summary>
        public static readonly Vector3 Flock = new Vector3(0.6f, 0f, 3.6f);

        // ---- 電車 ------------------------------------------------------------

        /// <summary>車両の半幅</summary>
        const float CarHalf = 1.3f;
        /// <summary>車両の半分の長さ。一両ぶんで 8 m</summary>
        const float CarLong = 4f;
        /// <summary>天井</summary>
        const float CarHigh = 2.3f;
        /// <summary>
        /// 吊り革の棒の高さと、中心からの寄り。
        /// **棒は目より高く通す。** 1.95 に下げていたときは、輪がちょうど 1.58 の目の前に垂れて、
        /// 掴まっている人の視界を黒い四角が半分塞いでいた
        /// </summary>
        public const float StrapY = 2.05f;
        public const float StrapX = 0.55f;
        /// <summary>座席の座面</summary>
        public const float SeatY = 0.42f;
        /// <summary>座席の中心の x。両側にある</summary>
        public const float SeatX = 1.02f;

        // ---- 台所 ------------------------------------------------------------

        /// <summary>台所と玄関を分ける壁。戸口はここに開く</summary>
        public const float KitchenDoorZ = 0f;
        /// <summary>流しの前。記憶 5 のマークが皿を洗っている</summary>
        public static readonly Vector3 Sink = new Vector3(-1.1f, 0f, 2.35f);
        /// <summary>玄関のドアの前</summary>
        public static readonly Vector3 Entrance = new Vector3(-1.95f, 0f, -1.95f);
        /// <summary>階段の下端と上端。玄関の +x 側を上がる</summary>
        public const float StairX = 1.9f;
        public const float StairFoot = -0.6f;
        public const float StairHead = -4.4f;
        const float HouseHigh = 2.6f;

        // ---- 教室 ------------------------------------------------------------

        /// <summary>黒板の面</summary>
        public const float BoardZ = 4.92f;
        /// <summary>教壇の前。記憶 7 のリーがチョークを持っている</summary>
        public static readonly Vector3 Chalk = new Vector3(0f, 0.15f, 4.0f);
        /// <summary>机の列の x と、行の z。記憶 13 と 14 はこの並びに座る</summary>
        public static readonly float[] DeskX = { -2.4f, -0.8f, 0.8f, 2.4f };
        public static readonly float[] DeskZ = { 2.0f, 0.4f, -1.2f };
        /// <summary>机の天板。座る人はここから 0.55 m 後ろ</summary>
        public const float DeskY = 0.72f;
        const float ClassHigh = 3.0f;

        // ---- 組み立て --------------------------------------------------------

        static Transform Places(Transform root)
        {
            var parent = Child(root, "Places");
            Clear(parent);
            Estate(Spot(parent, DiveIds.Estate));
            Park(Spot(parent, DiveIds.Park));
            Train(Spot(parent, DiveIds.Train));
            Kitchen(Spot(parent, DiveIds.Kitchen));
            Classroom(Spot(parent, DiveIds.Classroom));
            // 潜るまでは全部伏せる。DiveDirector が一つだけ起こす
            for (var i = 0; i < parent.childCount; i++) parent.GetChild(i).gameObject.SetActive(false);
            return parent;
        }

        static Transform Spot(Transform parent, string id)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = PlaceOrigin(id);
            return go.transform;
        }

        // ---- 団地の外階段 ------------------------------------------------------

        /// <summary>
        /// 記憶 0・1・8・15 の舞台。外階段と三階の廊下、右の家の中まで。
        ///
        /// 左の家（戸口 A）は中まで作らず、戸口の奥に浅い窪みと灯りだけを置く。
        /// 記憶 0 が振り返ったときに母が逆光の影になるのは、この奥の灯りのため
        /// </summary>
        static void Estate(Transform place)
        {
            var slab = new Bank { Texel = 0.35f };
            // 地面。階段の下と建物の足元だけあればよい
            slab.FaceY(0f, -6f, 10f, -20f, 7f, 1);
            // 踊り場と三階の廊下
            slab.Box(new Vector3(0f, Floor - 0.09f, EstateLanding1), new Vector3(StairHalf * 2f, 0.18f, LandingDeep));
            slab.Box(new Vector3(0f, Floor * 2f - 0.09f, EstateLanding2), new Vector3(StairHalf * 2f, 0.18f, LandingDeep));
            slab.Box(new Vector3(3.2f, EstateTop - 0.09f, EstateWalk), new Vector3(7.6f, 0.18f, LandingDeep));
            Flightup(slab, EstateFoot, 0f);
            Flightup(slab, EstateLanding1 - LandingDeep * 0.5f, Floor);
            Flightup(slab, EstateLanding2 - LandingDeep * 0.5f, Floor * 2f);
            // 右の家の床
            slab.FaceY(EstateTop, 3.0f, 7.0f, -18.6f, EstateFace, 1);
            Emit(place, "EstateFloor", slab, "Floor", true);

            var wall = new Bank { Texel = 0.3f };
            // 階段が背にしている壁。ここが無いと、階段が虚空に掛かっているように見える
            wall.FaceX(-0.9f, EstateFace, 3f, 0f, 11.2f, 1);
            // 建物の面。三階に戸口を二つ開ける
            var holes = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(DoorA - DoorHalf, DoorA + DoorHalf, EstateTop, EstateTop + DoorHigh),
                new Vector4(DoorB - DoorHalf, DoorB + DoorHalf, EstateTop, EstateTop + DoorHigh),
            };
            wall.FaceZHoles(EstateFace, -1.2f, 7.4f, 0f, 11.2f, 1, holes);
            // 左の戸口の奥の窪み
            wall.FaceX(DoorA - 0.75f, -16.4f, EstateFace, EstateTop, EstateTop + 2.2f, 1);
            wall.FaceX(DoorA + 0.75f, -16.4f, EstateFace, EstateTop, EstateTop + 2.2f, -1);
            wall.FaceY(EstateTop + 2.2f, DoorA - 0.75f, DoorA + 0.75f, -16.4f, EstateFace, -1);
            wall.FaceY(EstateTop, DoorA - 0.75f, DoorA + 0.75f, -16.4f, EstateFace, 1);
            // 右の家。四方と天井
            wall.FaceX(3.0f, -18.6f, EstateFace, EstateTop, EstateTop + 2.5f, 1);
            wall.FaceX(7.0f, -18.6f, EstateFace, EstateTop, EstateTop + 2.5f, -1);
            wall.FaceZ(-18.6f, 3.0f, 7.0f, EstateTop, EstateTop + 2.5f, 1);
            wall.FaceZ(EstateFace - 0.02f, 3.0f, 7.0f, EstateTop, EstateTop + 2.5f, -1);
            Emit(place, "EstateWall", wall, "Wall", true);

            var sky = new Bank { Texel = 0.3f };
            sky.FaceY(EstateTop + 2.5f, 3.0f, 7.0f, -18.6f, EstateFace, -1);
            sky.FaceY(EstateTop + 2.2f, DoorA - 0.75f, DoorA + 0.75f, -16.4f, EstateFace, -1);
            Emit(place, "EstateCeiling", sky, "Ceiling");

            var rail = new Bank { Texel = 0.5f };
            Handrail(rail, EstateFoot, 0f);
            Bar(rail, EstateLanding1 - LandingDeep * 0.5f, EstateLanding1 + LandingDeep * 0.5f, Floor);
            Handrail(rail, EstateLanding1 - LandingDeep * 0.5f, Floor);
            Bar(rail, EstateLanding2 - LandingDeep * 0.5f, EstateLanding2 + LandingDeep * 0.5f, Floor * 2f);
            Handrail(rail, EstateLanding2 - LandingDeep * 0.5f, Floor * 2f);
            // 三階の廊下の外側。**階段の口（x -0.6〜0.6）は空ける。**
            // 手すりに当たりを入れたので、廊下の長さぶん通すと階段を上がってきた先が塞がる。
            // 一度通した版では、上がり切ったところで壁に突き当たって廊下へ出られなかった
            for (var i = 0; i < 2; i++)
            {
                var y = EstateTop + 0.5f + i * 0.45f;
                rail.Box(new Vector3(3.8f, y, EstateWalk + LandingDeep * 0.5f), new Vector3(6.4f, 0.05f, 0.05f));
            }
            for (var i = 0; i <= 5; i++)
                rail.Box(new Vector3(StairHalf + i * 1.28f, EstateTop + 0.48f, EstateWalk + LandingDeep * 0.5f),
                    new Vector3(0.05f, 0.96f, 0.05f));
            Emit(place, "EstateRail", rail, "Rail", true);

            // 戸はどちらも開いている。記憶 0 は母が戸口に立ち、記憶 8 は新聞を持って中へ入り、
            // 記憶 15 は中から夫を迎える。閉めた戸を置くと、その三つが同じ場所で成り立たなくなる
            var leaf = new Bank { Texel = 0.4f };
            leaf.Box(new Vector3(DoorA + 0.92f, EstateTop + DoorHigh * 0.5f, EstateFace + 0.06f),
                new Vector3(0.86f, DoorHigh, 0.05f));
            leaf.Box(new Vector3(DoorB + 0.92f, EstateTop + DoorHigh * 0.5f, EstateFace + 0.06f),
                new Vector3(0.86f, DoorHigh, 0.05f));
            Emit(place, "EstateDoor", leaf, "Door");

            var soft = new Bank { Texel = 0.5f };
            soft.Box(new Vector3(4.9f, EstateTop + 0.22f, -16.6f), new Vector3(1.9f, 0.44f, 0.72f));
            soft.Box(new Vector3(4.9f, EstateTop + 0.52f, -16.28f), new Vector3(1.9f, 0.6f, 0.14f));
            Emit(place, "EstateSofa", soft, "Cloth");

            var set = new Bank { Texel = 0.5f };
            set.Box(new Vector3(EstateTv.x, EstateTop + 0.25f, EstateTv.z - 0.14f), new Vector3(1.0f, 0.5f, 0.42f));
            set.Box(EstateTv + new Vector3(0f, 0f, -0.05f), new Vector3(0.82f, 0.54f, 0.14f));
            Emit(place, "EstateTvSet", set, "Timber");

            // 戸口の奥とテレビ。灯りではなく光る面で済ませる。
            // 記憶 0 の母も記憶 15 の夫も、この光の手前に立つので影にしか見えない
            Pane(place, "EstateHallLight", new Vector3(DoorA, EstateTop + 1.1f, -16.35f),
                new Vector2(1.4f, 2.1f), Vector3.forward, Glow("GlowHall", new Color(1f, 0.90f, 0.74f), 0.55f));
            Pane(place, "EstateTv", EstateTv + new Vector3(0f, 0f, 0.06f),
                new Vector2(0.72f, 0.44f), Vector3.forward, Glow("GlowTv", new Color(0.72f, 0.82f, 1f), 1.35f));

            // 地面の端から先は虚空なので、見えない仕切りで囲う。
            // 建物の中まで歩ける必要は無いが、裏へ回れても困らないので地面ごと囲う
            Ring(place, "EstateFence", new Vector2(-6f, 10f), new Vector2(-20f, 7f), 3.5f);
            // 階段と三階の廊下の外側は手すりが受け持つ。EstateRail に当たりを入れてあるので、
            // ここへ仕切りを重ねると階段を上がってきた先が塞がる。
            // **廊下の両端だけは手すりが無い。** 歩いて確かめたら、廊下を +x へ歩き切って
            // 三階から地面へ落ちた。絵に手すりを増やさずに止めたいので、仕切りで塞ぐ
            Fence(place, "EstateWalkEndA", new Vector3(-0.7f, EstateTop + 0.75f, EstateWalk),
                new Vector3(0.2f, 1.5f, LandingDeep));
            Fence(place, "EstateWalkEndB", new Vector3(7.1f, EstateTop + 0.75f, EstateWalk),
                new Vector3(0.2f, 1.5f, LandingDeep));
            // 朝の低い日射し。階段の側から当てるので、三階の戸口に立つ人は逆光になる
            var sun = Lamp(place, "Morning", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(30f, -148f, 0f), new Color(1f, 0.95f, 0.86f), 1.7f, 10f);
            sun.shadows = LightShadows.Soft;
            // 右の家はテレビの光だけでは足りない。朝の窓明かりぶんをここで補う
            Lamp(place, "RoomB", LightType.Point, new Vector3(5.0f, EstateTop + 2.1f, -16.6f),
                Vector3.zero, new Color(0.80f, 0.84f, 0.95f), 1.5f, 6f);
        }

        /// <summary>一階ぶんの段。踏み面と蹴込みを一段ずつ張る</summary>
        static void Flightup(Bank b, float zFoot, float yFoot)
        {
            var d = Flight / StepCount;
            var r = Floor / StepCount;
            for (var i = 0; i < StepCount; i++)
            {
                b.FaceZ(zFoot - i * d, -StairHalf, StairHalf, yFoot + i * r, yFoot + (i + 1) * r, 1);
                b.FaceY(yFoot + (i + 1) * r, -StairHalf, StairHalf, zFoot - (i + 1) * d, zFoot - i * d, 1);
            }
            // 段の裏。下から見上げたときに段が宙に浮いて見えないように、斜めの板を一枚通す
            var dir = new Vector3(0f, Floor, -Flight).normalized;
            b.Box(new Vector3(0f, yFoot + Floor * 0.5f - 0.24f, zFoot - Flight * 0.5f),
                new Vector3(StairHalf * 2f, 0.3f, Mathf.Sqrt(Flight * Flight + Floor * Floor)),
                Quaternion.LookRotation(dir, Vector3.up));
        }

        /// <summary>斜めの手すり。段と同じ勾配で二本通し、柱を立てる</summary>
        static void Handrail(Bank b, float zFoot, float yFoot)
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
            for (var i = 0; i <= 3; i++)
            {
                var k = i / 3f;
                b.Box(new Vector3(StairHalf, yFoot + Floor * k + 0.48f, zFoot - Flight * k),
                    new Vector3(0.05f, 0.96f, 0.05f));
            }
        }

        /// <summary>踊り場の手すり。水平に二本</summary>
        static void Bar(Bank b, float z0, float z1, float y)
        {
            for (var i = 0; i < 2; i++)
                b.Box(new Vector3(StairHalf, y + 0.5f + i * 0.45f, (z0 + z1) * 0.5f),
                    new Vector3(0.05f, 0.05f, z1 - z0));
            b.Box(new Vector3(StairHalf, y + 0.48f, z0 + 0.05f), new Vector3(0.05f, 0.96f, 0.05f));
            b.Box(new Vector3(StairHalf, y + 0.48f, z1 - 0.05f), new Vector3(0.05f, 0.96f, 0.05f));
        }

        // ---- 公園 --------------------------------------------------------------

        /// <summary>
        /// 記憶 2・3・9・10 の舞台。ベンチ二つから門まで真っ直ぐ歩けるようにしてある。
        /// 池は縁の低い壁だけで、水は暗い面を一枚。遠くの木は板で済ませる
        /// </summary>
        static void Park(Transform place)
        {
            var dirt = new Bank { Texel = 0.25f };
            dirt.FaceY(0f, -9f, 9f, -9f, 13f, 1);
            Emit(place, "ParkGround", dirt, "Ground", true);

            var wood = new Bank { Texel = 0.5f };
            Bench(wood, BenchA);
            Bench(wood, BenchB);
            Emit(place, "ParkBench", wood, "Timber");

            var stone = new Bank { Texel = 0.4f };
            // 池の縁。ベンチの側だけ弧を描いていればよい
            for (var i = 0; i < 12; i++)
            {
                var a = Mathf.Deg2Rad * (150f + i * 12f);
                var b2 = Mathf.Deg2Rad * (150f + (i + 1) * 12f);
                var p = Pond + new Vector3(Mathf.Cos(a) * PondR, 0f, Mathf.Sin(a) * PondR);
                var q = Pond + new Vector3(Mathf.Cos(b2) * PondR, 0f, Mathf.Sin(b2) * PondR);
                var mid = (p + q) * 0.5f;
                var run = (q - p);
                stone.Box(new Vector3(mid.x, 0.16f, mid.z), new Vector3(0.36f, 0.32f, run.magnitude + 0.04f),
                    Quaternion.LookRotation(run.normalized, Vector3.up));
            }
            stone.Box(new Vector3(-GateHalf, 1.2f, GateZ), new Vector3(0.35f, 2.4f, 0.35f));
            stone.Box(new Vector3(GateHalf, 1.2f, GateZ), new Vector3(0.35f, 2.4f, 0.35f));
            Emit(place, "ParkStone", stone, "Wall", true);

            var pool = new Bank { Texel = 0.2f };
            pool.FaceY(0.06f, Pond.x - PondR, Pond.x + PondR, Pond.z - PondR * 0.4f, Pond.z + PondR, 1);
            Emit(place, "ParkWater", pool, "Water");

            // 遠くの木。板を並べるだけで、近づけないところに置く
            var far = new Bank { Texel = 0.2f };
            for (var i = 0; i < 9; i++)
            {
                var x = -8f + i * 2f;
                far.Box(new Vector3(x, 2.6f, -7.5f), new Vector3(2.2f, 5.2f, 0.12f));
            }
            for (var i = 0; i < 5; i++)
                far.Box(new Vector3(-8.4f, 2.6f, -4f + i * 3.4f), new Vector3(0.12f, 5.2f, 2.2f));
            NoShadow(Emit(place, "ParkTrees", far, "Leaf"));

            // 鳩は場所ではなく記憶の側に置く（BuildDiveTakes.Doves）。
            // 飛び立つ秒が記憶ごとに違うので、場所に置くと四つの記憶で同じ瞬間に飛ぶことになる

            Ring(place, "ParkFence", new Vector2(-9f, 9f), new Vector2(-9f, 13f), 3.5f);

            // 午後の低い日。**門の側（+z）から差す。** 門へ歩く人はこれで逆光になり、
            // 遠景の板の影も場所の外へ伸びる。逆向きに当てていたときは、板の影が
            // 十数メートルの帯になってベンチから池までを丸ごと夜にしていた
            var sun = Lamp(place, "Afternoon", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(26f, 188f, 0f), new Color(1f, 0.92f, 0.76f), 2.2f, 10f);
            sun.shadows = LightShadows.Soft;
        }

        static void Bench(Bank b, Vector3 at)
        {
            b.Box(at + new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.07f, 0.46f));
            b.Box(at + new Vector3(0f, 0.72f, -0.24f), new Vector3(1.8f, 0.42f, 0.06f));
            for (var i = 0; i < 2; i++)
                b.Box(at + new Vector3((i == 0 ? -1f : 1f) * 0.78f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.42f));
        }

        // ---- 電車 --------------------------------------------------------------

        /// <summary>
        /// 記憶 4・11 の舞台。一両ぶんの箱に座席二列と吊り革。
        /// 窓の外は光る帯で済ませる。流れるのは <c>WindowStream</c> の受け持ちで、
        /// 出来るまではこの帯が止まったまま光っている
        /// </summary>
        static void Train(Transform place)
        {
            var deck = new Bank { Texel = 0.5f };
            deck.FaceY(0f, -CarHalf, CarHalf, -CarLong, CarLong, 1);
            Emit(place, "TrainFloor", deck, "Floor", true);

            var shell = new Bank { Texel = 0.4f };
            // 側壁。窓の帯を抜く
            var holes = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(-CarLong + 0.4f, CarLong - 0.4f, 1.0f, 1.8f),
            };
            shell.FaceXHoles(-CarHalf, -CarLong, CarLong, 0f, CarHigh, 1, holes);
            shell.FaceXHoles(CarHalf, -CarLong, CarLong, 0f, CarHigh, -1, holes);
            shell.FaceZ(-CarLong, -CarHalf, CarHalf, 0f, CarHigh, 1);
            shell.FaceZ(CarLong, -CarHalf, CarHalf, 0f, CarHigh, -1);
            Emit(place, "TrainShell", shell, "Wall", true);

            var roof = new Bank { Texel = 0.4f };
            roof.FaceY(CarHigh, -CarHalf, CarHalf, -CarLong, CarLong, -1);
            Emit(place, "TrainRoof", roof, "Ceiling");

            var seat = new Bank { Texel = 0.5f };
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * SeatX;
                seat.Box(new Vector3(x, SeatY, 0f), new Vector3(0.52f, 0.08f, CarLong * 2f - 1.2f));
                seat.Box(new Vector3(x + (i == 0 ? -0.22f : 0.22f), 0.78f, 0f),
                    new Vector3(0.08f, 0.64f, CarLong * 2f - 1.2f));
            }
            Emit(place, "TrainSeat", seat, "Cloth");

            var pole = new Bank { Texel = 0.5f };
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * StrapX;
                pole.Box(new Vector3(x, StrapY, 0f), new Vector3(0.04f, 0.04f, CarLong * 2f - 0.8f));
                for (var k = 0; k < 9; k++)
                {
                    var z = -3.2f + k * 0.8f;
                    pole.Box(new Vector3(x, StrapY - 0.12f, z), new Vector3(0.02f, 0.24f, 0.02f));
                    pole.Box(new Vector3(x, StrapY - 0.30f, z), new Vector3(0.11f, 0.13f, 0.02f));
                }
            }
            Emit(place, "TrainPole", pole, "Rail");

            // 窓の外。夕方の街の灯りは一色の帯で足りる
            var stream = Glow("GlowStream", new Color(0.86f, 0.80f, 0.62f), 0.45f);
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * (CarHalf + 0.03f);
                Pane(place, "TrainWindow" + i, new Vector3(x, 1.4f, 0f), new Vector2(CarLong * 2f - 0.8f, 0.8f),
                    i == 0 ? Vector3.right : Vector3.left, stream);
            }

            Lamp(place, "Fluorescent", LightType.Point, new Vector3(0f, CarHigh - 0.15f, 0f),
                Vector3.zero, new Color(0.90f, 0.94f, 1f), 4.5f, 11f);
            Pane(place, "TrainTube", new Vector3(0f, CarHigh - 0.03f, 0f), new Vector2(0.22f, CarLong * 2f - 1.4f),
                Vector3.down, Glow("GlowTube", new Color(0.94f, 0.97f, 1f), 1.6f));
        }

        // ---- 台所と玄関と階段 ----------------------------------------------------

        /// <summary>
        /// 記憶 5・6・12 の舞台。台所と玄関を戸口で分け、玄関の脇から二階へ階段を上げる。
        /// 二階は作らない。階段の上は暗がりで、足音だけが降りてくる
        /// </summary>
        static void Kitchen(Transform place)
        {
            var deck = new Bank { Texel = 0.4f };
            deck.FaceY(0f, -2.5f, 2.5f, -5f, 3.2f, 1);
            KitchenSteps(deck);
            Emit(place, "KitchenFloor", deck, "Floor", true);

            var wall = new Bank { Texel = 0.35f };
            wall.FaceZ(3.2f, -2.5f, 2.5f, 0f, HouseHigh, -1);
            wall.FaceZ(-5f, -2.5f, 2.5f, 0f, 4.2f, 1);
            wall.FaceX(2.5f, -5f, 3.2f, 0f, 4.2f, -1);
            // 玄関のドアを -x の壁に開ける。外の光はこの向こうから来る
            var holes = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(-2.4f, -1.5f, 0f, DoorHigh),
            };
            wall.FaceXHoles(-2.5f, -5f, 3.2f, 0f, HouseHigh, 1, holes);
            // 台所と玄関を分ける壁。戸口が一つ
            var gap = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(-0.55f, 0.55f, 0f, 2.05f),
            };
            wall.FaceZHoles(KitchenDoorZ, -2.5f, 2.5f, 0f, HouseHigh, 1, gap);
            wall.FaceZHoles(KitchenDoorZ - 0.08f, -2.5f, 2.5f, 0f, HouseHigh, -1, gap);
            // 階段の吹き抜け。玄関の +x 側だけ天井が抜ける
            wall.FaceX(1.35f, -5f, KitchenDoorZ, HouseHigh, 4.2f, 1);
            Emit(place, "KitchenWall", wall, "Wall", true);

            var roof = new Bank { Texel = 0.35f };
            roof.FaceY(HouseHigh, -2.5f, 2.5f, KitchenDoorZ, 3.2f, -1);
            roof.FaceY(HouseHigh, -2.5f, 1.35f, -5f, KitchenDoorZ, -1);
            roof.FaceY(4.2f, 1.35f, 2.5f, -5f, KitchenDoorZ, -1);
            Emit(place, "KitchenCeiling", roof, "Ceiling");

            var fit = new Bank { Texel = 0.5f };
            // 流しと吊り棚。記憶 5 はこの前で手を洗い、記憶 6 は棚を顎で指される
            fit.Box(new Vector3(-1.05f, 0.45f, 2.85f), new Vector3(2.5f, 0.9f, 0.62f));
            fit.Box(new Vector3(-1.05f, 1.9f, 3.0f), new Vector3(2.5f, 0.7f, 0.36f));
            fit.Box(new Vector3(1.3f, 0.45f, 2.85f), new Vector3(1.2f, 0.9f, 0.62f));
            fit.Box(new Vector3(-2.0f, 0.02f, -1.95f), new Vector3(0.72f, 0.04f, 0.46f));
            Emit(place, "KitchenFittings", fit, "Timber");

            var tap = new Bank { Texel = 0.5f };
            tap.Box(new Vector3(-1.05f, 0.88f, 2.85f), new Vector3(0.7f, 0.06f, 0.44f));
            tap.Box(new Vector3(-1.05f, 1.06f, 3.05f), new Vector3(0.04f, 0.32f, 0.04f));
            KitchenRail(tap);
            Emit(place, "KitchenMetal", tap, "Rail");

            // 開いたまま壁へ寄せた戸。三つの記憶がどれもここから外へ出るので、閉めた戸は置かない
            var leaf = new Bank { Texel = 0.4f };
            leaf.Box(new Vector3(-2.42f, DoorHigh * 0.5f, -0.95f), new Vector3(0.05f, DoorHigh, 0.86f));
            Emit(place, "KitchenDoor", leaf, "Door");

            // 玄関のドアの向こうは白い光の板だけで、床はそこで切れている。
            // 三つの記憶がどれもこの戸口から出ていくので、絵としては開けたまま残し、
            // 抜けられないように見えない仕切りだけを立てる
            Fence(place, "KitchenDoorway", new Vector3(-2.55f, DoorHigh * 0.5f, -1.95f),
                new Vector3(0.2f, DoorHigh, 1.1f));

            // ドアの外。開けると白く飛ぶ朝の光
            Pane(place, "KitchenOutside", new Vector3(-2.72f, 1.05f, -1.95f), new Vector2(1.1f, 2.1f),
                Vector3.right, Glow("GlowMorning", new Color(0.92f, 0.95f, 1f), 1.8f));

            Lamp(place, "Bulb", LightType.Point, new Vector3(-0.3f, HouseHigh - 0.25f, 1.1f),
                Vector3.zero, new Color(0.86f, 0.90f, 1f), 6.5f, 14f);
            Pane(place, "KitchenBulb", new Vector3(-0.3f, HouseHigh - 0.04f, 1.1f), new Vector2(0.5f, 0.5f),
                Vector3.down, Glow("GlowBulb", new Color(1f, 0.97f, 0.90f), 1.5f));
        }

        /// <summary>二階へ上がる段。玄関の +x 側を -z へ上がる</summary>
        static void KitchenSteps(Bank b)
        {
            const int n = 15;
            var d = (StairFoot - StairHead) / n;
            var r = HouseHigh / n;
            for (var i = 0; i < n; i++)
            {
                b.FaceZ(StairFoot - i * d, 1.35f, 2.5f, i * r, (i + 1) * r, 1);
                b.FaceY((i + 1) * r, 1.35f, 2.5f, StairFoot - (i + 1) * d, StairFoot - i * d, 1);
            }
            b.FaceY(HouseHigh, 1.35f, 2.5f, -5f, StairHead, 1);
        }

        /// <summary>階段の手すり。段と同じ勾配で一本</summary>
        static void KitchenRail(Bank b)
        {
            var run = StairFoot - StairHead;
            var dir = new Vector3(0f, HouseHigh, -run).normalized;
            b.Box(new Vector3(1.42f, HouseHigh * 0.5f + 0.9f, (StairFoot + StairHead) * 0.5f),
                new Vector3(0.05f, 0.05f, Mathf.Sqrt(run * run + HouseHigh * HouseHigh)),
                Quaternion.LookRotation(dir, Vector3.up));
            for (var i = 0; i <= 3; i++)
            {
                var k = i / 3f;
                b.Box(new Vector3(1.42f, HouseHigh * k + 0.45f, StairFoot - run * k), new Vector3(0.05f, 0.9f, 0.05f));
            }
        }

        // ---- 教室 --------------------------------------------------------------

        /// <summary>
        /// 記憶 7・13・14 の舞台。机の列が四つ三行、黒板と教壇、窓は白い板。
        /// 記憶 14 は遠くがぼやける体なので、黒板の字は初めから書かない
        /// </summary>
        static void Classroom(Transform place)
        {
            var deck = new Bank { Texel = 0.4f };
            deck.FaceY(0f, -4f, 4f, -4f, 5f, 1);
            deck.Box(new Vector3(0f, 0.075f, 4.05f), new Vector3(6f, 0.15f, 1.1f));
            Emit(place, "ClassFloor", deck, "Floor", true);

            var wall = new Bank { Texel = 0.35f };
            wall.FaceZ(5f, -4f, 4f, 0f, ClassHigh, -1);
            wall.FaceZ(-4f, -4f, 4f, 0f, ClassHigh, 1);
            wall.FaceX(4f, -4f, 5f, 0f, ClassHigh, -1);
            // 窓は三枚。抜いた先に光る板を置く
            var holes = new System.Collections.Generic.List<Vector4>();
            for (var i = 0; i < 3; i++)
                holes.Add(new Vector4(-2.6f + i * 2.3f, -2.6f + i * 2.3f + 1.8f, 1.0f, 2.5f));
            wall.FaceXHoles(-4f, -4f, 5f, 0f, ClassHigh, 1, holes);
            Emit(place, "ClassWall", wall, "Wall", true);

            var roof = new Bank { Texel = 0.35f };
            roof.FaceY(ClassHigh, -4f, 4f, -4f, 5f, -1);
            Emit(place, "ClassCeiling", roof, "Ceiling");

            var slate = new Bank { Texel = 0.4f };
            slate.Box(new Vector3(0f, 1.65f, BoardZ), new Vector3(5.2f, 1.2f, 0.08f));
            Emit(place, "ClassBoard", slate, "Board");

            // 黒板の縁とチョーク受け。板は壁とほとんど同じ暗さなので、
            // 明るい縁が無いと黒板がそこに在ることが読み取れない
            var trim = new Bank { Texel = 0.5f };
            trim.Box(new Vector3(0f, 1.04f, BoardZ - 0.06f), new Vector3(5.3f, 0.06f, 0.14f));
            trim.Box(new Vector3(0f, 2.28f, BoardZ - 0.02f), new Vector3(5.3f, 0.06f, 0.10f));
            Emit(place, "ClassTrim", trim, "Rail");

            var wood = new Bank { Texel = 0.5f };
            for (var c = 0; c < DeskX.Length; c++)
                for (var r = 0; r < DeskZ.Length; r++)
                    Desk(wood, new Vector3(DeskX[c], 0f, DeskZ[r]));
            Emit(place, "ClassDesk", wood, "Timber");

            var pane = Glow("GlowNoon", new Color(0.96f, 0.97f, 1f), 1.6f);
            for (var i = 0; i < 3; i++)
                Pane(place, "ClassWindow" + i, new Vector3(-4.06f, 1.75f, -2.6f + i * 2.3f + 0.9f),
                    new Vector2(1.8f, 1.5f), Vector3.right, pane);

            Lamp(place, "Noon", LightType.Point, new Vector3(-0.8f, ClassHigh - 0.2f, 1.8f),
                Vector3.zero, new Color(0.94f, 0.96f, 1f), 10f, 18f);
            Pane(place, "ClassLamp", new Vector3(-0.8f, ClassHigh - 0.04f, 1.8f), new Vector2(1.2f, 0.3f),
                Vector3.down, Glow("GlowTube", new Color(0.94f, 0.97f, 1f), 1.6f));
        }

        static void Desk(Bank b, Vector3 at)
        {
            b.Box(at + new Vector3(0f, DeskY, 0f), new Vector3(0.62f, 0.04f, 0.44f));
            for (var i = 0; i < 4; i++)
                b.Box(at + new Vector3((i % 2 == 0 ? -1f : 1f) * 0.27f, DeskY * 0.5f, (i < 2 ? -1f : 1f) * 0.18f),
                    new Vector3(0.04f, DeskY, 0.04f));
            b.Box(at + new Vector3(0f, 0.44f, -0.56f), new Vector3(0.4f, 0.04f, 0.36f));
            b.Box(at + new Vector3(0f, 0.66f, -0.72f), new Vector3(0.4f, 0.4f, 0.04f));
            for (var i = 0; i < 4; i++)
                b.Box(at + new Vector3((i % 2 == 0 ? -1f : 1f) * 0.17f, 0.22f, -0.56f + (i < 2 ? -1f : 1f) * 0.15f),
                    new Vector3(0.03f, 0.44f, 0.03f));
        }

        // ---- 道具 --------------------------------------------------------------

        /// <summary>
        /// 光る板を一枚。
        ///
        /// **Quad は法線が -z。** 角度で渡すと裏表を取り違えて板が消えるので（場面 3 で踏んだ）、
        /// 受け取るのは「どちら側から見えてほしいか」の向きにして、回すのはこちらで引き受ける
        /// </summary>
        static Transform Pane(Transform parent, string name, Vector3 at, Vector2 size, Vector3 facing, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<MeshCollider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            // 真下や真上を向く板は、上向きを world の上に取ると回しようが無くなって消える
            var aim = facing.normalized;
            go.transform.localRotation = Quaternion.LookRotation(-aim,
                Mathf.Abs(aim.y) > 0.99f ? Vector3.forward : Vector3.up);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}
