using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 路地と村の家並み（設計書 2 節の 1・2）。二段目。
    ///
    /// 路地は z 0 を東西に走り、舗装しない。西の端（車の着く所）の麦畑のあいだの未舗装路が、そのまま村の中まで続く。
    /// <see cref="VillageWest"/> から東が村で、北に家 A（ハチミツ色の石・妻を路地へ向けた二階建て）、
    /// 家 B（白い漆喰の壁の茅葺き）、片割れの家。南に家 C（赤煉瓦の二階建て）、電話ボックス、農場の門と麦畑、
    /// 家 D（石の平屋）。
    ///
    /// **同じ家の繰り返しに見せない。** 壁（石・漆喰・赤煉瓦）、屋根（石版・茅）、階（平屋・二階建て）、
    /// 棟の向き（路地に沿う・妻を路地へ向ける）、戸の色、窓の数、煙突の位置、玄関の庇の形を家ごとに変える。
    /// 家の形は <see cref="Cottage"/> に寸法だけ持たせ、<see cref="Raise"/> が片割れの家の部品
    /// （<see cref="WindowZ"/>・<see cref="WindowX"/>・<see cref="Doorway"/>・<see cref="CockAndHen"/>）で組む。
    /// 家の中は作らない。窓の奥は暗い面とカーテン（片割れの家と同じ）。
    ///
    /// **日本の村に見せない。** 瓦・板塀・電柱・側溝・白いガードレールは置かない。
    /// 路肩は芝で、縁は野石の塀と刈り込んだ生け垣。赤い電話ボックスと塀の郵便ポスト、黒と白の道標
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- 並び（設計書 2 節の 1・2） --------------------------------------------------

        /// <summary>
        /// 村の西の縁。ここより東は家並みの生け垣、西は車の着く所の野石の塀と、麦畑のあいだの未舗装路。
        /// 路地は舗装せず、未舗装路がそのまま村の中まで続く
        /// </summary>
        public const float VillageWest = -64f;
        /// <summary>未舗装路の西の端。書き割りの地面の中まで伸ばす</summary>
        const float TrackWest = -150f;

        /// <summary>路地の北の家並みの奥の生け垣。片割れの敷地の奥（<see cref="PlotNorth"/>）と揃える</summary>
        const float BackHedge = PlotNorth + 0.5f;
        /// <summary>路地の南の家並みの裏の生け垣。その先は麦畑</summary>
        const float SouthHedge = -18f;

        /// <summary>家 A と家 B の敷地の境、家 B と片割れの敷地の境。家 A と家 B の間はフットパス</summary>
        const float PlotAWest = -48f;
        const float PlotAEast = -26.2f;
        const float PlotBWest = -24.4f;
        const float PlotBEast = PlotWest - 0.4f;
        /// <summary>南の敷地の境。家 C、農場の門の畑、家 D</summary>
        const float PlotCWest = -56f;
        const float PlotCEast = -38f;
        const float PlotDWest = -19f;
        const float PlotDEast = -3f;
        /// <summary>南の農場の門の口（x の範囲）</summary>
        const float FarmGateWest = -30.4f;
        const float FarmGateEast = -27.0f;
        /// <summary>車の着く所の農場の門（北）の口</summary>
        const float ArriveGateWest = -73.6f;
        const float ArriveGateEast = -70.2f;

        /// <summary>電話ボックスと、郵便ポストを埋めた塀と、ベンチ。どれも南の路肩</summary>
        public static readonly Vector3 PhoneBoxAt = new Vector3(-35.4f, 0f, -2.72f);
        static readonly Vector3 PostBoxAt = new Vector3(-37.3f, 0f, -3.62f);
        static readonly Vector3 BenchAt = new Vector3(-23.2f, 0f, -2.85f);
        /// <summary>道標。未舗装路が舗装に入る所の北の路肩</summary>
        static readonly Vector3 FingerpostAt = new Vector3(VillageWest + 1.2f, 0f, 2.85f);
        /// <summary>路肩に停めた車。家 A の前の北の路肩に、東を向けて</summary>
        static readonly Vector3 ParkedCarAt = new Vector3(-37.8f, 0f, 1.95f);
        /// <summary>
        /// 場面 8 の車の止まった姿。**道の脇に寄せて停める**（設計書 7 節）。東を向け、左（北）の車輪を路肩の芝に乗せ、
        /// 路地の南の 3 m を空ける。車の着く所の農場の門の口（x -73.6 から東）は塞がない
        /// </summary>
        public static readonly Vector3 DriveCarAt = new Vector3(-77.0f, 0f, 1.9f);

        /// <summary>壁の素材</summary>
        enum WallKind { Stone, Render, Brick }
        /// <summary>玄関の庇の形</summary>
        enum Hood { Canopy, Porch, Case, Thatch }

        /// <summary>
        /// 家一軒の寸法。路地の北の家は正面が -z（路地）を、南の家は +z を向く。
        /// 窓は (芯, 幅, 下, 上)。正面と裏は芯が x、妻（西と東）は芯が z
        /// </summary>
        sealed class Cottage
        {
            public string Name;
            /// <summary>+1 なら路地の北（正面は -z）、-1 なら南（正面は +z）</summary>
            public int Side = 1;
            public float West, East;
            /// <summary>正面の壁の、路地の芯からの隔たり</summary>
            public float Front;
            public float Deep;
            public float Eaves;
            public float Pitch;
            /// <summary>妻を路地へ向ける（棟が z に沿う）</summary>
            public bool GableFront;
            public WallKind Wall;
            public bool Thatch;
            /// <summary>妻の立ち上がりと笠石（コッツウォルズの石の家）</summary>
            public bool Parapet;
            /// <summary>煙突の位置。棟に沿った座標（棟が x に沿えば x、z に沿えば z）</summary>
            public float[] Chimneys = new float[0];
            public Vector4[] FrontWindows = new Vector4[0];
            public Vector4[] RearWindows = new Vector4[0];
            public Vector4[] WestWindows = new Vector4[0];
            public Vector4[] EastWindows = new Vector4[0];
            public float DoorX;
            public int DoorColour;
            public Hood Hood;
            /// <summary>一階の窓に花箱を付ける</summary>
            public bool WindowBoxes;

            public float ZF { get { return Side * Front; } }
            public float ZR { get { return Side * (Front + Deep); } }
            public float ZMin { get { return Mathf.Min(ZF, ZR); } }
            public float ZMax { get { return Mathf.Max(ZF, ZR); } }
            /// <summary>正面の壁の外の向き（z の符号）</summary>
            public int Out { get { return -Side; } }
        }

        /// <summary>家 A。ハチミツ色の石の二階建て。妻を路地へ向け、東に平屋の翼。戸は濃い赤</summary>
        static Cottage HouseA()
        {
            return new Cottage
            {
                Name = "A", Side = 1, West = -42.0f, East = -35.4f, Front = 6.6f, Deep = 8.6f,
                Eaves = 4.9f, Pitch = 52f, GableFront = true, Wall = WallKind.Stone, Parapet = true,
                Chimneys = new[] { 14.2f },
                FrontWindows = new[] { new Vector4(-38.7f, 1.30f, 0.85f, 2.05f), new Vector4(-38.7f, 1.00f, 3.25f, 4.25f), new Vector4(-38.7f, 0.55f, 5.9f, 6.6f) },
                RearWindows = new[] { new Vector4(-39.8f, 1.1f, 0.9f, 2.0f), new Vector4(-37.4f, 0.8f, 3.3f, 4.2f) },
                WestWindows = new[] { new Vector4(9.2f, 1.2f, 0.85f, 2.0f), new Vector4(12.6f, 1.2f, 0.85f, 2.0f), new Vector4(10.9f, 0.9f, 3.25f, 4.2f) },
                EastWindows = new[] { new Vector4(12.9f, 0.8f, 3.3f, 4.2f) },
                // 戸は東の翼の正面に（WingA）。本体の正面は窓だけ
                DoorX = float.NaN, DoorColour = SwOxblood, Hood = Hood.Porch, WindowBoxes = true,
            };
        }

        /// <summary>家 A の東の平屋の翼。棟は路地に沿う。玄関はここ</summary>
        static Cottage WingA()
        {
            return new Cottage
            {
                Name = "AWing", Side = 1, West = -35.4f, East = -30.8f, Front = 8.4f, Deep = 5.2f,
                Eaves = 2.7f, Pitch = 46f, Wall = WallKind.Stone,
                Chimneys = new float[0],
                FrontWindows = new[] { new Vector4(-31.9f, 1.0f, 0.9f, 1.95f) },
                RearWindows = new[] { new Vector4(-33.2f, 1.0f, 0.95f, 1.9f) },
                EastWindows = new[] { new Vector4(11.0f, 0.7f, 1.0f, 1.9f) },
                DoorX = -34.0f, DoorColour = SwOxblood, Hood = Hood.Porch, WindowBoxes = true,
            };
        }

        /// <summary>家 B。白い漆喰の壁に茅葺き。低い軒の一階半。戸は青みの灰</summary>
        static Cottage HouseB()
        {
            return new Cottage
            {
                Name = "B", Side = 1, West = -22.2f, East = -13.2f, Front = 6.2f, Deep = 5.4f,
                Eaves = 2.75f, Pitch = 52f, Wall = WallKind.Render, Thatch = true,
                Chimneys = new[] { -21.5f, -13.9f },
                FrontWindows = new[] { new Vector4(-20.1f, 1.1f, 0.8f, 1.85f), new Vector4(-15.4f, 1.1f, 0.8f, 1.85f), new Vector4(-13.95f, 0.5f, 0.95f, 1.75f) },
                RearWindows = new[] { new Vector4(-19.6f, 1.0f, 0.85f, 1.85f), new Vector4(-15.6f, 0.9f, 0.9f, 1.8f) },
                WestWindows = new[] { new Vector4(8.9f, 0.8f, 3.2f, 4.1f) },
                EastWindows = new[] { new Vector4(8.9f, 0.8f, 3.2f, 4.1f), new Vector4(8.9f, 0.9f, 0.9f, 1.8f) },
                DoorX = -17.6f, DoorColour = SwBlueGrey, Hood = Hood.Thatch, WindowBoxes = false,
            };
        }

        /// <summary>家 C。赤煉瓦の二階建て。左右対称に窓を並べ、真ん中の戸に白い戸枠。戸は紺</summary>
        static Cottage HouseC()
        {
            return new Cottage
            {
                Name = "C", Side = -1, West = -52.6f, East = -42.4f, Front = 7.2f, Deep = 6.0f,
                Eaves = 5.3f, Pitch = 38f, Wall = WallKind.Brick,
                Chimneys = new[] { -52.1f, -42.9f },
                FrontWindows = new[]
                {
                    new Vector4(-50.4f, 1.1f, 0.9f, 2.3f), new Vector4(-44.6f, 1.1f, 0.9f, 2.3f),
                    new Vector4(-50.4f, 1.0f, 3.3f, 4.6f), new Vector4(-47.5f, 1.0f, 3.3f, 4.6f), new Vector4(-44.6f, 1.0f, 3.3f, 4.6f),
                },
                RearWindows = new[] { new Vector4(-50.0f, 1.0f, 0.9f, 2.2f), new Vector4(-45.0f, 1.0f, 3.3f, 4.5f) },
                EastWindows = new[] { new Vector4(-10.2f, 0.9f, 1.0f, 2.2f) },
                DoorX = -47.5f, DoorColour = SwNavy, Hood = Hood.Case, WindowBoxes = true,
            };
        }

        /// <summary>家 D。石の平屋。長く低い棟に、石の壁の小さな玄関の張り出し。戸は黄土色</summary>
        static Cottage HouseD()
        {
            return new Cottage
            {
                Name = "D", Side = -1, West = -16.6f, East = -5.6f, Front = 6.6f, Deep = 5.2f,
                Eaves = 2.8f, Pitch = 48f, Wall = WallKind.Stone,
                Chimneys = new[] { -16.1f, -9.6f },
                FrontWindows = new[]
                {
                    new Vector4(-14.9f, 1.1f, 0.85f, 1.95f), new Vector4(-12.9f, 1.1f, 0.85f, 1.95f),
                    new Vector4(-8.2f, 1.1f, 0.85f, 1.95f), new Vector4(-6.6f, 0.6f, 1.0f, 1.8f),
                },
                RearWindows = new[] { new Vector4(-13.4f, 1.0f, 0.9f, 1.9f), new Vector4(-8.6f, 1.0f, 0.9f, 1.9f) },
                WestWindows = new[] { new Vector4(-9.2f, 0.7f, 2.9f, 3.7f) },
                DoorX = -10.6f, DoorColour = SwOchre, Hood = Hood.Porch, WindowBoxes = true,
            };
        }

        // ---- 組み立て -------------------------------------------------------------------

        /// <summary>路地の家並み・前庭・村の物・車の着く所。家と塀は片割れの家と同じ入れ物に溜めて一枚に焼く</summary>
        static void Lane(Transform parent, Banks b)
        {
            WindowPlants.Clear();
            Baskets.Clear();
            DoorRoses.Clear();
            WallIvy.Clear();
            var bounds = Child(parent, "Bounds");
            var houses = new[] { HouseA(), WingA(), HouseB(), HouseC(), HouseD() };
            foreach (var c in houses) Raise(b, c, bounds);
            LaneFronts(b, bounds);
            PhoneBox(b, PhoneBoxAt, bounds);
            PostBox(b, PostBoxAt);
            Bench(b, BenchAt, bounds);
            Fingerpost(b, FingerpostAt);
            ParkedCar(b, ParkedCarAt, 90f, SwCarPaint, bounds);
            DriveCar(parent, bounds);
        }

        // ---- 家 -------------------------------------------------------------------------

        static Bank WallBank(Banks b, WallKind k)
        {
            return k == WallKind.Brick ? b.RedBrick : k == WallKind.Render ? b.Render : b.Stone;
        }

        /// <summary>
        /// 家を一軒建てる。壁（窓と戸の抜け）・妻・屋根・煙突・窓・戸と庇・竪樋・当たり。
        /// 花箱と吊り鉢とバラの位置は溜めておき、札の側（<see cref="LanePlants"/>）が植える
        /// </summary>
        static void Raise(Banks b, Cottage c, Transform bounds)
        {
            var wall = WallBank(b, c.Wall);
            var t = Mathf.Tan(c.Pitch * Mathf.Deg2Rad);
            var hasDoor = !float.IsNaN(c.DoorX);
            const float doorWide = 0.95f;
            const float doorHigh = 2.05f;

            // 壁。正面と裏は z が一定、西と東は x が一定
            var front = new List<Vector4>();
            foreach (var w in c.FrontWindows) front.Add(Hole(w));
            if (hasDoor) front.Add(new Vector4(c.DoorX - doorWide * 0.5f, c.DoorX + doorWide * 0.5f, 0f, doorHigh));
            var rear = new List<Vector4>();
            foreach (var w in c.RearWindows) rear.Add(Hole(w));
            var west = new List<Vector4>();
            foreach (var w in c.WestWindows) west.Add(Hole(w));
            var east = new List<Vector4>();
            foreach (var w in c.EastWindows) east.Add(Hole(w));
            wall.FaceZHoles(c.ZF, c.West, c.East, 0f, c.Eaves, c.Out, front);
            wall.FaceZHoles(c.ZR, c.West, c.East, 0f, c.Eaves, -c.Out, rear);
            wall.FaceXHoles(c.West, c.ZMin, c.ZMax, 0f, c.Eaves, -1, west);
            wall.FaceXHoles(c.East, c.ZMin, c.ZMax, 0f, c.Eaves, 1, east);
            // 壁の根の石の帯（腰の水切り）。壁と地面の境の継ぎ目を隠す
            b.Dressed.Box(new Vector3((c.West + c.East) * 0.5f, 0.09f, c.ZF + c.Out * 0.03f), new Vector3(c.East - c.West + 0.08f, 0.18f, 0.08f));

            foreach (var w in c.FrontWindows) WindowZ(b, c.ZF, c.Out, w);
            foreach (var w in c.RearWindows) WindowZ(b, c.ZR, -c.Out, w);
            foreach (var w in c.WestWindows) WindowX(b, c.West, -1, w);
            foreach (var w in c.EastWindows) WindowX(b, c.East, 1, w);

            Roof(b, c, wall, t);

            if (hasDoor) Door(b, c, doorWide, doorHigh);

            // 花箱。一階の窓の台に
            if (c.WindowBoxes)
                foreach (var w in c.FrontWindows)
                {
                    if (w.z > 1.5f) continue;
                    var z = c.ZF + c.Out * 0.20f;
                    b.Boards.Box(new Vector3(w.x, w.z + 0.08f, z), new Vector3(w.y + 0.1f, 0.20f, 0.24f));
                    b.Soil.Box(new Vector3(w.x, w.z + 0.18f, z), new Vector3(w.y, 0.02f, 0.18f));
                    for (var k = 0; k < 3; k++)
                        WindowPlants.Add(new Vector3(w.x - w.y * 0.35f + k * w.y * 0.35f, w.z + 0.17f, z));
                }

            // 竪樋。正面の片方の隅に
            if (!c.Thatch)
            {
                var gx = c.Side > 0 ? c.West + 0.25f : c.East - 0.25f;
                b.Iron.Box(new Vector3(gx, c.Eaves * 0.5f, c.ZF + c.Out * 0.1f), new Vector3(0.08f, c.Eaves, 0.08f));
            }

            Block(bounds, "House" + c.Name, new Vector3((c.West + c.East) * 0.5f, 3f, (c.ZMin + c.ZMax) * 0.5f),
                new Vector3(c.East - c.West, 6f, c.ZMax - c.ZMin));
        }

        /// <summary>
        /// 屋根と妻と煙突。棟が路地に沿うか（x）、路地へ向くか（z）で組み替える。
        /// 石の家は妻を屋根の上へ立ち上げて笠石を載せ、ほかは屋根を妻の外へ少し出す（けらば）。
        /// 茅は厚みを持たせ、軒を深く出して、棟に押さえの帯を巻く
        /// </summary>
        static void Roof(Banks b, Cottage c, Bank wall, float t)
        {
            // 棟の向きに合わせた座標。a が棟に沿い、r が棟を横切る
            var alongX = !c.GableFront;
            var a0 = alongX ? c.West : c.ZMin;
            var a1 = alongX ? c.East : c.ZMax;
            var r0 = alongX ? c.ZMin : c.West;
            var r1 = alongX ? c.ZMax : c.East;
            var rc = (r0 + r1) * 0.5f;
            var ridge = c.Eaves + (r1 - r0) * 0.5f * t;
            System.Func<float, float, float, Vector3> P = (a, y, r) => alongX ? new Vector3(a, y, r) : new Vector3(r, y, a);
            System.Func<float, float, float, Vector3> D = (da, dy, dr) => alongX ? new Vector3(da, dy, dr) : new Vector3(dr, dy, da);

            // 妻。軒から上の三角
            const float parapet = 0.3f;
            const float gable = 0.4f;
            foreach (var ae in new[] { a0, a1 })
            {
                var o = ae == a0 ? -1f : 1f;
                var top = c.Parapet ? ridge + parapet : ridge;
                var e0 = P(ae, c.Eaves, r0);
                var e1 = P(ae, c.Eaves, r1);
                if (c.Parapet)
                {
                    var f0 = P(ae, c.Eaves + parapet, r0);
                    var f1 = P(ae, c.Eaves + parapet, r1);
                    Face(wall, e0, e1, f1, f0, D(o, 0f, 0f));
                    Face(wall, f0, f1, P(ae, top, rc), P(ae, top, rc), D(o, 0f, 0f));
                    // 立ち上がりの内の面
                    var ai = ae - o * gable;
                    foreach (var rr in new[] { new Vector2(r0 - 0.28f, rc), new Vector2(rc, r1 + 0.28f) })
                    {
                        var ya = c.Eaves + (Mathf.Min(rr.x - r0, r1 - rr.x)) * t;
                        var yb = c.Eaves + (Mathf.Min(rr.y - r0, r1 - rr.y)) * t;
                        Face(wall, P(ai, ya, rr.x), P(ai, yb, rr.y), P(ai, yb + parapet, rr.y), P(ai, ya + parapet, rr.x), D(-o, 0f, 0f));
                    }
                    // 笠石と、軒の端の持ち送りの石と、頂の飾り
                    var ac = ae - o * gable * 0.5f;
                    Beam(b.Dressed, P(ac, c.Eaves + parapet + 0.02f, r0 - 0.06f), P(ac, ridge + parapet + 0.06f, rc), gable + 0.12f, 0.12f);
                    Beam(b.Dressed, P(ac, c.Eaves + parapet + 0.02f, r1 + 0.06f), P(ac, ridge + parapet + 0.06f, rc), gable + 0.12f, 0.12f);
                    var kz = 0.63f;
                    b.Dressed.Box(P(ac + o * 0.03f, c.Eaves - 0.1f, r0 + 0.3f - kz * 0.5f), D(gable + 0.16f, 0.8f, kz));
                    b.Dressed.Box(P(ac + o * 0.03f, c.Eaves - 0.1f, r1 - 0.3f + kz * 0.5f), D(gable + 0.16f, 0.8f, kz));
                    b.Dressed.Box(P(ac, ridge + parapet + 0.24f, rc), new Vector3(0.22f, 0.30f, 0.22f));
                }
                else
                {
                    Face(wall, e0, e1, P(ae, ridge, rc), P(ae, ridge, rc), D(o, 0f, 0f));
                }
            }

            // 斜面。石の家は妻の内側まで、ほかはけらばを出す
            var over = c.Thatch ? 0.55f : 0.28f;
            var verge = c.Parapet ? -gable : c.Thatch ? 0.35f : 0.22f;
            var ra = a0 - verge;
            var rb = a1 + verge;
            var roof = c.Thatch ? b.Thatch : b.Slate;
            var thick = c.Thatch ? 0.32f : 0.14f;
            foreach (var side in new[] { -1f, 1f })
            {
                var re = side < 0f ? r0 - over : r1 + over;
                var ye = c.Eaves - over * t;
                var e0 = P(ra, ye, re);
                var e1 = P(rb, ye, re);
                var k0 = P(ra, ridge, rc);
                var k1 = P(rb, ridge, rc);
                var outward = D(0f, 1f, side * t);
                // 厚みは面の法線に沿って上へ持ち上げる
                var lift = outward.normalized * thick;
                Face(roof, e0 + lift, e1 + lift, k1 + lift, k0 + lift, outward);
                // 軒先の小口と軒裏
                Face(roof, e0, e1, e1 + lift, e0 + lift, D(0f, 0f, side));
                var wallR = side < 0f ? r0 : r1;
                Face(b.Dark, e0, e1, P(rb, c.Eaves - 0.02f, wallR), P(ra, c.Eaves - 0.02f, wallR), Vector3.down);
                // けらばの小口（妻の外へ出した屋根の端）
                if (!c.Parapet)
                {
                    Face(roof, e0, k0, k0 + lift, e0 + lift, D(-1f, 0f, 0f));
                    Face(roof, e1, k1, k1 + lift, e1 + lift, D(1f, 0f, 0f));
                    // けらばの裏。屋根の裏が空から覗かないように
                    Face(b.Dark, e0, k0, P(a0, ridge - 0.02f, rc), P(a0, c.Eaves - 0.02f, wallR), Vector3.down);
                    Face(b.Dark, e1, k1, P(a1, ridge - 0.02f, rc), P(a1, c.Eaves - 0.02f, wallR), Vector3.down);
                }
                if (!c.Thatch)
                    b.Iron.Box(P((ra + rb) * 0.5f, ye - 0.08f, re + side * 0.05f), D(rb - ra + 0.1f, 0.11f, 0.12f));
            }
            if (c.Thatch)
            {
                // 棟の押さえ。棟に沿って太い帯を巻き、その裾を波形に切る代わりに、斜面に一段厚い帯を重ねる
                var ridgeUp = ridge + thick / Mathf.Cos(c.Pitch * Mathf.Deg2Rad);
                Beam(b.Thatch, P(ra - 0.05f, ridgeUp, rc), P(rb + 0.05f, ridgeUp, rc), 0.55f, 0.55f);
                foreach (var side in new[] { -1f, 1f })
                {
                    var n = D(0f, 1f, side * t).normalized;
                    var down = D(0f, -t, side).normalized * 0.9f;
                    var mid = P((ra + rb) * 0.5f, ridgeUp, rc) + down + n * 0.1f;
                    b.Thatch.Box(mid, new Vector3(0.9f, 0.12f, rb - ra + 0.1f), Quaternion.LookRotation(alongX ? Vector3.right : Vector3.forward, n));
                }
            }
            else
            {
                b.Dressed.Box(P((ra + rb) * 0.5f, ridge + thick + 0.04f, rc), D(rb - ra, 0.16f, 0.30f));
            }

            // 煙突。棟の上に胴を立て、笠と土管を
            var stack = c.Wall == WallKind.Brick ? b.RedBrick : b.Stone;
            foreach (var ca in c.Chimneys)
            {
                var foot = ridge - 1.0f;
                var topY = ridge + (c.Thatch ? 1.6f : 1.3f);
                var at = P(ca, (foot + topY) * 0.5f, rc);
                stack.Box(at, D(0.8f, topY - foot, 0.62f));
                b.Dressed.Box(P(ca, topY + 0.06f, rc), D(0.94f, 0.12f, 0.76f));
                Prism(b.Clay, P(ca - 0.17f, topY + 0.12f, rc), 0.11f, 0.36f, 8, 0.09f);
                Prism(b.Clay, P(ca + 0.17f, topY + 0.12f, rc), 0.11f, 0.28f, 8, 0.09f);
            }
        }

        /// <summary>
        /// 玄関。抜けの奥の暗がりと石の戸枠、色を塗った戸の板、庇。
        /// 戸は開かない（他人の家なので）。戸の色は <see cref="Swatch"/> の升で持つ
        /// </summary>
        static void Door(Banks b, Cottage c, float wide, float high)
        {
            Doorway(b, c.ZF, c.Out, c.DoorX, wide, high);
            var s = (float)c.Out;
            var z = c.ZF - s * 0.14f;
            Tint(b.Swatch, new Vector3(c.DoorX, high * 0.5f, z), new Vector3(wide - 0.04f, high - 0.02f, 0.05f), Quaternion.identity, c.DoorColour);
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                    Tint(b.Swatch, new Vector3(c.DoorX - wide * 0.22f + i * wide * 0.44f, high * (0.27f + 0.46f * j), z + s * 0.03f),
                        new Vector3(wide * 0.32f, high * 0.36f, 0.02f), Quaternion.identity, c.DoorColour);
            b.Iron.Box(new Vector3(c.DoorX + wide * 0.36f, high * 0.47f, z + s * 0.05f), new Vector3(0.04f, 0.14f, 0.04f));
            b.Iron.Box(new Vector3(c.DoorX, high * 0.62f, z + s * 0.045f), new Vector3(0.12f, 0.12f, 0.02f));
            var zo = c.ZF + s * 0.02f;
            switch (c.Hood)
            {
                case Hood.Porch:
                {
                    // 切妻の小さなポーチ。二本の柱に石版の屋根、中は吹き放し
                    var depth = 1.1f;
                    var zp = c.ZF + s * depth;
                    foreach (var dx in new[] { -0.75f, 0.75f })
                        b.Boards.Box(new Vector3(c.DoorX + dx, 1.15f, zp - s * 0.06f), new Vector3(0.12f, 2.3f, 0.12f));
                    b.Boards.Box(new Vector3(c.DoorX, 2.33f, zp - s * 0.06f), new Vector3(1.7f, 0.12f, 0.12f));
                    foreach (var dx in new[] { -0.75f, 0.75f })
                        b.Boards.Box(new Vector3(c.DoorX + dx, 2.33f, (c.ZF + zp) * 0.5f), new Vector3(0.1f, 0.1f, depth));
                    var apex = new Vector3(c.DoorX, 3.1f, 0f);
                    foreach (var sx in new[] { -1f, 1f })
                    {
                        var e0 = new Vector3(c.DoorX + sx * 1.0f, 2.35f, c.ZF + s * 0.02f);
                        var e1 = new Vector3(c.DoorX + sx * 1.0f, 2.35f, zp + s * 0.15f);
                        var k0 = new Vector3(c.DoorX, apex.y, c.ZF + s * 0.02f);
                        var k1 = new Vector3(c.DoorX, apex.y, zp + s * 0.15f);
                        Face(b.Slate, e0, e1, k1, k0, new Vector3(sx, 1.3f, 0f));
                        Face(b.Dark, e0, e1, k1, k0, new Vector3(-sx, -1.3f, 0f));
                    }
                    // 妻の三角の板
                    Face(b.Boards, new Vector3(c.DoorX - 0.9f, 2.36f, zp + s * 0.1f), new Vector3(c.DoorX + 0.9f, 2.36f, zp + s * 0.1f),
                        new Vector3(c.DoorX, 3.02f, zp + s * 0.1f), new Vector3(c.DoorX, 3.02f, zp + s * 0.1f), new Vector3(0f, 0f, s));
                    b.Flag.Box(new Vector3(c.DoorX, 0.05f, (c.ZF + zp) * 0.5f + s * 0.1f), new Vector3(1.9f, 0.1f, depth + 0.2f));
                    DoorRoses.Add(new Vector4(c.DoorX, c.ZF + s * 0.07f, s, 0.8f));
                    Baskets.Add(new Vector3(c.DoorX + 0.75f, 2.0f, zp - s * 0.06f));
                    b.Iron.Box(new Vector3(c.DoorX + 0.75f, 2.2f, zp - s * 0.06f), new Vector3(0.015f, 0.3f, 0.015f));
                    Prism(b.Iron, new Vector3(c.DoorX + 0.75f, 1.84f, zp - s * 0.06f), 0.10f, 0.20f, 8, 0.19f);
                    break;
                }
                case Hood.Case:
                {
                    // 白い戸枠。付け柱と、上に三角の破風と扇の欄間
                    foreach (var dx in new[] { -0.68f, 0.68f })
                        b.Paint.Box(new Vector3(c.DoorX + dx, 1.25f, zo + s * 0.06f), new Vector3(0.2f, 2.5f, 0.12f));
                    b.Paint.Box(new Vector3(c.DoorX, 2.58f, zo + s * 0.08f), new Vector3(1.66f, 0.16f, 0.18f));
                    Face(b.Paint, new Vector3(c.DoorX - 0.86f, 2.66f, zo + s * 0.15f), new Vector3(c.DoorX + 0.86f, 2.66f, zo + s * 0.15f),
                        new Vector3(c.DoorX, 3.05f, zo + s * 0.15f), new Vector3(c.DoorX, 3.05f, zo + s * 0.15f), new Vector3(0f, 0f, s));
                    b.Paint.Box(new Vector3(c.DoorX, 2.2f, zo + s * 0.01f), new Vector3(wide, 0.3f, 0.04f));
                    b.Flag.Box(new Vector3(c.DoorX, 0.08f, c.ZF + s * 0.35f), new Vector3(1.6f, 0.16f, 0.7f));
                    Baskets.Add(new Vector3(c.DoorX + 1.25f, 2.25f, c.ZF + s * 0.35f));
                    b.Iron.Box(new Vector3(c.DoorX + 1.25f, 2.75f, c.ZF + s * 0.2f), new Vector3(0.03f, 0.03f, 0.4f));
                    b.Iron.Box(new Vector3(c.DoorX + 1.25f, 2.55f, c.ZF + s * 0.36f), new Vector3(0.015f, 0.4f, 0.015f));
                    Prism(b.Iron, new Vector3(c.DoorX + 1.25f, 2.1f, c.ZF + s * 0.36f), 0.10f, 0.20f, 8, 0.19f);
                    break;
                }
                case Hood.Thatch:
                {
                    // 茅の小さな庇。戸の上に厚い茅の帯を斜めに
                    var q = Quaternion.Euler(s * 28f, 0f, 0f);
                    b.Thatch.Box(new Vector3(c.DoorX, high + 0.42f, c.ZF + s * 0.38f), new Vector3(wide + 0.9f, 0.22f, 0.9f), q);
                    foreach (var dx in new[] { -0.6f, 0.6f })
                        b.Boards.Box(new Vector3(c.DoorX + dx, high + 0.18f, c.ZF + s * 0.3f), new Vector3(0.08f, 0.08f, 0.6f), Quaternion.Euler(-s * 35f, 0f, 0f));
                    DoorRoses.Add(new Vector4(c.DoorX, c.ZF + s * 0.07f, s, 0.9f));
                    break;
                }
                default:
                {
                    b.Dressed.Box(new Vector3(c.DoorX, high + 0.42f, c.ZF + s * 0.3f), new Vector3(wide + 0.8f, 0.12f, 0.62f));
                    break;
                }
            }
        }

        // ---- 前庭と路地の縁 ---------------------------------------------------------------

        /// <summary>
        /// 路地の縁の囲いと門。北は家 A・フットパス・家 B、南は家 C・農場の門・家 D。
        ///
        /// **家の囲いは植物の生け垣を主にする**（オーナー、2026-09-26）。路地に面した前庭も、家と家のあいだも、
        /// 裏庭のまわりも刈り込んだ生け垣で囲い、樹種（イチイ・ブナ・イボタ・サンザシ）と高さを家ごとに変える。
        /// 前庭は 1.1〜1.35 m、家どうしの境は 1.7〜1.9 m。野石は門の脇の低い柱と、生け垣の足元（家 D）にだけ残す。
        /// 車の着く所の野石の塀と、郵便ポストを埋めた塀の一区切りは残す（畑の境で、家の囲いではない）。
        /// 当たりは垣と、口を塞ぐ見えない箱
        /// </summary>
        static void LaneFronts(Banks b, Transform bounds)
        {
            var zn0 = NorthEdge;
            var zn1 = NorthEdge + FrontWallThick;
            var zs0 = -NorthEdge;
            var zs1 = -NorthEdge - FrontWallThick;
            // 前庭の垣の芯。路肩の芝の縁から 0.35 m 内
            var zn = NorthEdge + 0.35f;
            var zs = -NorthEdge - 0.35f;

            // 北の西の端（車の着く所）。野石の塀と農場の門。村の西の縁から東はサンザシの生け垣
            FieldWall(b, bounds, "WallNW0", new Vector3(LaneWest, 0f, (zn0 + zn1) * 0.5f), new Vector3(ArriveGateWest - 0.25f, 0f, (zn0 + zn1) * 0.5f));
            FieldWall(b, bounds, "WallNW1", new Vector3(ArriveGateEast + 0.25f, 0f, (zn0 + zn1) * 0.5f), new Vector3(VillageWest, 0f, (zn0 + zn1) * 0.5f));
            FieldGate(b, bounds, "GateArrive", ArriveGateWest, ArriveGateEast, (zn0 + zn1) * 0.5f, true);
            Clipped(b, bounds, "HedgeNW", Shrub.Thorn, new Vector3(VillageWest, 0f, zn + 0.1f), new Vector3(PlotAWest, 0f, zn + 0.1f), 1.5f, 0.9f, 11);

            // 家 A。路地の側はブナ（1.3 m）、口は翼の玄関へ一つ。西の境はイチイ（1.9 m）、東はフットパスとの生け垣
            var aDoor = -34.0f;
            FrontHedge(b, bounds, "FrontA", Shrub.Beech, PlotAWest, PlotAEast, zn, 1.3f, 0.75f, aDoor, false, 0f);
            Clipped(b, bounds, "SideA", Shrub.Yew, new Vector3(PlotAWest, 0f, zn + 0.4f), new Vector3(PlotAWest, 0f, BackHedge), 1.9f, 0.9f, 13);
            b.Flag.FaceY(0.03f, aDoor - 0.55f, aDoor + 0.55f, zn1, 8.4f - 1.1f, 1);

            // フットパス。家 A と家 B の間を北の畑へ抜ける細い道。両脇はサンザシの生け垣。口に木の小門（キッシングゲート）
            var fpW = PlotAEast;
            var fpE = PlotBWest;
            Clipped(b, bounds, "HedgeFpW", Shrub.Thorn, new Vector3(fpW, 0f, zn1), new Vector3(fpW, 0f, BackHedge), 1.8f, 0.7f, 21);
            Clipped(b, bounds, "HedgeFpE", Shrub.Thorn, new Vector3(fpE, 0f, zn1), new Vector3(fpE, 0f, BackHedge), 1.8f, 0.7f, 23);
            KissingGate(b, (fpW + fpE) * 0.5f, zn0 + 0.2f);
            Block(bounds, "ShutFootpath", new Vector3((fpW + fpE) * 0.5f, 1.1f, zn0 + 0.25f), new Vector3(fpE - fpW, 2.2f, 0.4f));
            // 道の土
            b.Soil.FaceY(0.015f, fpW + 0.35f, fpE - 0.35f, zn0, BackHedge - 0.3f, 1);

            // 家 B。路地の側はイボタ（1.1 m）。**東の脇の庭の垣は低くする**（設計書 2 節の 3）。
            // その上から片割れの裏庭の白いパラソルが覗く。片割れとの境の裏庭の側（板の塀の手前）はブナ
            var bDoor = -17.6f;
            FrontHedge(b, bounds, "FrontB", Shrub.Privet, PlotBWest, -12.4f, zn, 1.1f, 0.7f, bDoor, false, 0f);
            Clipped(b, bounds, "FrontB2", Shrub.Privet, new Vector3(-12.4f, 0f, zn), new Vector3(PlotBEast + 0.1f, 0f, zn), 0.72f, 0.6f, 35);
            Clipped(b, null, null, Shrub.Beech, new Vector3(PlotBEast - 0.3f, 0f, GateZ + 0.3f), new Vector3(PlotBEast - 0.3f, 0f, BackHedge - 0.5f), 1.8f, 0.7f, 37);
            b.Flag.FaceY(0.03f, bDoor - 0.5f, bDoor + 0.5f, zn0 + 0.7f, 6.2f - 0.3f, 1);

            // 南の西の端（車の着く所）は野石の塀。村の西の縁から東はサンザシの生け垣
            FieldWall(b, bounds, "WallSW", new Vector3(LaneWest, 0f, (zs0 + zs1) * 0.5f), new Vector3(VillageWest, 0f, (zs0 + zs1) * 0.5f));
            Clipped(b, bounds, "HedgeSW", Shrub.Thorn, new Vector3(VillageWest, 0f, zs - 0.1f), new Vector3(PlotCWest, 0f, zs - 0.1f), 1.5f, 0.9f, 15);

            // 家 C。路地の側はイボタ（1.15 m）に、赤煉瓦の門柱と白い木戸。西の境はイチイ
            var cDoor = -47.5f;
            FrontHedge(b, bounds, "FrontC", Shrub.Privet, PlotCWest, PlotCEast, zs, 1.15f, 0.7f, cDoor, true, 0f);
            Clipped(b, bounds, "SideCW", Shrub.Yew, new Vector3(PlotCWest, 0f, zs - 0.4f), new Vector3(PlotCWest, 0f, SouthHedge), 1.8f, 0.9f, 17);
            b.Flag.FaceY(0.03f, cDoor - 0.55f, cDoor + 0.55f, -7.2f + 0.7f, zs1, 1);

            // 農場の門の畑。電話ボックスの後ろに、郵便ポストを埋めた野石の塀を一区切りだけ残し、その先はサンザシの生け垣
            FieldWall(b, bounds, "WallPost", new Vector3(PlotCEast, 0f, (zs0 + zs1) * 0.5f), new Vector3(-36.1f, 0f, (zs0 + zs1) * 0.5f));
            Clipped(b, bounds, "HedgeFarm0", Shrub.Thorn, new Vector3(-36.1f, 0f, zs - 0.1f), new Vector3(FarmGateWest - 0.4f, 0f, zs - 0.1f), 1.4f, 0.9f, 19);
            Clipped(b, bounds, "HedgeFarm1", Shrub.Thorn, new Vector3(FarmGateEast + 0.4f, 0f, zs - 0.1f), new Vector3(PlotDWest, 0f, zs - 0.1f), 1.4f, 0.9f, 29);
            FieldGate(b, bounds, "GateFarm", FarmGateWest, FarmGateEast, (zs0 + zs1) * 0.5f, false);
            // 家 C と畑の境、畑と家 D の境
            Clipped(b, bounds, "HedgeCE", Shrub.Thorn, new Vector3(PlotCEast, 0f, zs1), new Vector3(PlotCEast, 0f, SouthHedge), 1.8f, 0.9f, 41);
            Clipped(b, bounds, "HedgeDW", Shrub.Beech, new Vector3(PlotDWest, 0f, zs1), new Vector3(PlotDWest, 0f, SouthHedge), 1.7f, 0.9f, 43);

            // 家 D。低い野石の足元（0.45 m）の上にブナ（1.35 m まで）。口が一つ
            var dDoor = -10.6f;
            FrontHedge(b, bounds, "FrontD", Shrub.Beech, PlotDWest, PlotDEast, zs, 1.35f, 0.75f, dDoor, false, 0.45f);
            b.Flag.FaceY(0.03f, dDoor - 0.55f, dDoor + 0.55f, -6.6f - 1.1f + 0.2f, zs1, 1);
            Clipped(b, bounds, "HedgeDE", Shrub.Yew, new Vector3(PlotDEast, 0f, zs1), new Vector3(PlotDEast, 0f, SouthHedge), 1.9f, 0.9f, 45);
            // 家 D の東から路地の東の端の先（教会の墓地の塀）まで。放牧地との生け垣。路地の東の端より先は粗く
            Clipped(b, bounds, "HedgeSE", Shrub.Thorn, new Vector3(PlotDEast, 0f, zs - 0.1f), new Vector3(LaneEast + 1f, 0f, zs - 0.1f), 1.5f, 0.9f, 47);
            // 路地の東の端から教会の墓地の塀までは低く刈る。墓地の塀と墓石を路地から見せる
            FieldHedge(b, new Vector3(LaneEast + 1f, 0f, zs - 0.1f), new Vector3(ChurchYardWest, 0f, zs - 0.1f), 1.0f, 0.9f, 147);
            // 片割れの敷地の東から路地の東の端の先まで。北の放牧地の生け垣
            Clipped(b, bounds, "HedgeNE", Shrub.Thorn, new Vector3(PlotEast + 0.4f, 0f, zn + 0.1f), new Vector3(LaneEast + 1f, 0f, zn + 0.1f), 1.5f, 0.9f, 49);
            FieldHedge(b, new Vector3(LaneEast + 1f, 0f, zn + 0.1f), new Vector3(ChurchYardWest, 0f, zn + 0.1f), 1.0f, 0.9f, 149);

            // 南の家の裏の生け垣
            FieldHedge(b, new Vector3(PlotCWest, 0f, SouthHedge), new Vector3(PlotCEast, 0f, SouthHedge), 1.7f, 0.9f, 51);
            FieldHedge(b, new Vector3(PlotDWest, 0f, SouthHedge), new Vector3(PlotDEast, 0f, SouthHedge), 1.7f, 0.9f, 53);

            // 北の家の裏の生け垣と、家 B と片割れの敷地の境の奥（片割れの側は板の塀が立つ）
            FieldHedge(b, new Vector3(PlotAWest, 0f, BackHedge), new Vector3(PlotAEast, 0f, BackHedge), 1.9f, 1.0f, 55);
            FieldHedge(b, new Vector3(PlotBWest, 0f, BackHedge), new Vector3(PlotBEast, 0f, BackHedge), 1.9f, 1.0f, 57);
        }

        /// <summary>生け垣の樹種。刈り込んだ家の囲いに使う物</summary>
        enum Shrub { Thorn, Yew, Beech, Privet }

        static Bank ShrubBank(Banks b, Shrub s)
        {
            return s == Shrub.Yew ? b.Yew : s == Shrub.Beech ? b.Beech : s == Shrub.Privet ? b.Privet : b.Hedge;
        }

        /// <summary>
        /// 刈り込んだ生け垣を一続き。根は from.y、背は high。頭の凸凹は 0.9 m ごと（片割れの前庭の <see cref="Hedge"/> の 0.7 m より粗い）。
        /// footing があれば、根に野石の低い足元を積む。bounds があれば当たりも置く
        /// </summary>
        static void Clipped(Banks b, Transform bounds, string name, Shrub s, Vector3 from, Vector3 to, float high, float thick, int seed, float footing = 0f)
        {
            var run = to - from;
            run.y = 0f;
            var len = run.magnitude;
            if (len < 0.05f) return;
            var dir = run / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var bank = ShrubBank(b, s);
            var mid = (from + to) * 0.5f;
            if (footing > 0f)
            {
                b.Stone.Box(mid + Vector3.up * (footing * 0.5f), new Vector3(thick + 0.12f, footing, len), rot);
                b.Dressed.Box(mid + Vector3.up * (footing + 0.03f), new Vector3(thick + 0.18f, 0.06f, len + 0.04f), rot);
            }
            var y0 = footing > 0f ? footing + 0.04f : 0f;
            bank.Box(mid + Vector3.up * ((y0 + high) * 0.5f), new Vector3(thick, high - y0, len), rot);
            var n = Mathf.Max(1, Mathf.RoundToInt(len / 0.9f));
            for (var i = 0; i < n; i++)
            {
                var at = from + dir * (len * (i + 0.5f) / n);
                var bump = 0.05f + Hash(seed, i) * 0.12f;
                bank.Box(new Vector3(at.x, from.y + high + bump * 0.5f - 0.02f, at.z), new Vector3(thick * 0.86f, bump, len / n * 0.92f), rot);
            }
            if (bounds != null) Wall(bounds, name, new Vector3(from.x, 0f, from.z), new Vector3(to.x, 0f, to.z), 2.2f, thick + (footing > 0f ? 0.12f : 0f));
        }

        /// <summary>
        /// 前庭の路地の側の生け垣。door の x に口を開け、口の脇に低い門柱（野石か赤煉瓦）と白い木戸。
        /// footing があれば垣の根に野石の低い足元を積む
        /// </summary>
        static void FrontHedge(Banks b, Transform bounds, string name, Shrub s, float x0, float x1, float z, float high, float thick,
            float door, bool brick, float footing)
        {
            var gap0 = door - PathWide * 0.5f - 0.35f;
            var gap1 = door + PathWide * 0.5f + 0.35f;
            Clipped(b, bounds, name + "W", s, new Vector3(x0, 0f, z), new Vector3(gap0 - 0.2f, 0f, z), high, thick, (int)(x0 * 7f), footing);
            Clipped(b, bounds, name + "E", s, new Vector3(gap1 + 0.2f, 0f, z), new Vector3(x1, 0f, z), high, thick, (int)(x1 * 7f), footing);
            foreach (var x in new[] { gap0, gap1 })
            {
                if (brick)
                {
                    b.RedBrick.Box(new Vector3(x, 0.6f, z), new Vector3(0.44f, 1.2f, 0.44f));
                    b.Dressed.Box(new Vector3(x, 1.24f, z), new Vector3(0.52f, 0.08f, 0.52f));
                    b.Dressed.Box(new Vector3(x, 1.36f, z), new Vector3(0.24f, 0.16f, 0.24f));
                }
                else
                {
                    b.Stone.Box(new Vector3(x, 0.5f, z), new Vector3(0.45f, 1.0f, 0.5f));
                    b.Dressed.Box(new Vector3(x, 1.04f, z), new Vector3(0.55f, 0.08f, 0.6f));
                }
            }
            PicketGate(b, gap0 + 0.22f, gap1 - 0.22f, z);
            Block(bounds, name + "Shut", new Vector3(door, 1.1f, z), new Vector3(gap1 - gap0 + 0.4f, 2.2f, thick));
        }

        /// <summary>
        /// 畑との境の野石の塀。空積みで少し高く（1.2 m）、頭に縦の笠石。当たりも置く。
        /// high を渡せば高さを変えられる（敷地の横の境）
        /// </summary>
        static void FieldWall(Banks b, Transform bounds, string name, Vector3 from, Vector3 to, float high = 1.15f)
        {
            var run = to - from;
            var len = run.magnitude;
            if (len < 0.05f) return;
            var rot = Quaternion.LookRotation(run / len, Vector3.up);
            const float thick = 0.55f;
            // 根を少し広げる。空積みの塀は下が厚い
            b.Stone.Box((from + to) * 0.5f + Vector3.up * (high * 0.5f), new Vector3(thick, high, len), rot);
            b.Stone.Box((from + to) * 0.5f + Vector3.up * 0.2f, new Vector3(thick + 0.12f, 0.4f, len), rot);
            // 路地に沿う塀は 0.36 m ごと、奥へ伸びる敷地の横の塀は 0.45 m ごと
            var along = Mathf.Abs(run.x) > Mathf.Abs(run.z);
            Coping(b, from + Vector3.up * high, to + Vector3.up * high, thick - 0.1f, along ? 0.36f : 0.45f);
            Wall(bounds, name, from, to, 2.2f, thick);
        }

        /// <summary>
        /// 野石の塀の頭の縦の笠石を、片割れの前庭の <see cref="CockAndHen"/> より粗く（0.2 m ごと）。
        /// 路地の縁の塀は長いので、石を一つずつ立てると三角が嵩む。隙間なく詰め、塀と同じ石で積むのは同じ
        /// </summary>
        static void Coping(Banks b, Vector3 from, Vector3 to, float thick, float piece = 0.2f)
        {
            var run = to - from;
            var len = run.magnitude;
            if (len < 0.05f) return;
            var dir = run / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var n = Mathf.Max(1, Mathf.RoundToInt(len / piece));
            for (var i = 0; i < n; i++)
            {
                var at = from + dir * ((i + 0.5f) * len / n);
                var tall = i % 3 == 0 ? 0.21f : 0.14f + Hash(43, i) * 0.04f;
                b.Stone.Box(at + Vector3.up * (tall * 0.5f), new Vector3(thick, tall, len / n - 0.01f), rot);
            }
        }

        /// <summary>
        /// 農場の門。石の門柱の間に、亜鉛めっきの灰色の五本桟の門と斜めの筋交い。閉じてある。
        /// open なら少し開いた向きに振る（車の着く所の門）
        /// </summary>
        static void FieldGate(Banks b, Transform bounds, string name, float x0, float x1, float z, bool open)
        {
            foreach (var x in new[] { x0 - 0.12f, x1 + 0.12f })
            {
                b.Stone.Box(new Vector3(x, 0.7f, z), new Vector3(0.55f, 1.4f, 0.6f));
                b.Dressed.Box(new Vector3(x, 1.44f, z), new Vector3(0.62f, 0.1f, 0.66f));
            }
            var wide = x1 - x0 - 0.1f;
            var yaw = open ? -28f : 0f;
            var hinge = new Vector3(x0 + 0.05f, 0f, z);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, Vector3> at = (u, y) => hinge + rot * new Vector3(u, y, 0f);
            // 端の縦桟二本と、五本の横桟、筋交い
            Tint(b.Swatch, at(0.04f, 0.62f), new Vector3(0.08f, 1.2f, 0.08f), rot, SwGalv);
            Tint(b.Swatch, at(wide - 0.04f, 0.62f), new Vector3(0.06f, 1.16f, 0.06f), rot, SwGalv);
            for (var i = 0; i < 5; i++)
                Tint(b.Swatch, at(wide * 0.5f, 0.22f + i * 0.23f), new Vector3(wide, 0.05f, 0.04f), rot, SwGalv);
            var d0 = at(0.08f, 0.2f);
            var d1 = at(wide * 0.62f, 1.14f);
            Tint(b.Swatch, (d0 + d1) * 0.5f, new Vector3(0.05f, 0.05f, (d1 - d0).magnitude), Quaternion.LookRotation(d1 - d0, Vector3.up), SwGalv);
            Block(bounds, name, new Vector3((x0 + x1) * 0.5f, 1.1f, z), new Vector3(x1 - x0, 2.2f, 0.6f));
            // 門の札。フットパスの黄色い矢印（門の柱に）
            WayMark(b, new Vector3(x1 + 0.12f, 1.12f, z - Mathf.Sign(z) * 0.31f), Mathf.Sign(z) > 0f ? 180f : 0f);
        }

        /// <summary>前庭の白い木戸。縦の細板を並べ、上を少し弧にする。閉じてある</summary>
        static void PicketGate(Banks b, float x0, float x1, float z)
        {
            var wide = x1 - x0;
            b.Paint.Box(new Vector3((x0 + x1) * 0.5f, 0.3f, z), new Vector3(wide, 0.07f, 0.04f));
            b.Paint.Box(new Vector3((x0 + x1) * 0.5f, 0.78f, z), new Vector3(wide, 0.07f, 0.04f));
            var n = Mathf.Max(3, Mathf.RoundToInt(wide / 0.12f));
            for (var i = 0; i < n; i++)
            {
                var x = x0 + wide * (i + 0.5f) / n;
                var u = (x - (x0 + x1) * 0.5f) / (wide * 0.5f);
                var h = 0.95f + 0.08f * (1f - u * u);
                b.Paint.Box(new Vector3(x, 0.08f + h * 0.5f, z + 0.03f), new Vector3(0.07f, h, 0.025f));
            }
        }

        /// <summary>フットパスの口の木の小門（キッシングゲート）。U の字の柵の中で振る戸。黄色い矢印の札を柱に</summary>
        static void KissingGate(Banks b, float x, float z)
        {
            foreach (var dx in new[] { -0.55f, 0.55f })
                b.Boards.Box(new Vector3(x + dx, 0.6f, z), new Vector3(0.12f, 1.2f, 0.12f));
            b.Boards.Box(new Vector3(x, 1.05f, z), new Vector3(1.2f, 0.08f, 0.06f));
            b.Boards.Box(new Vector3(x, 0.55f, z), new Vector3(1.2f, 0.08f, 0.06f));
            foreach (var dz in new[] { 0.45f, 0.9f })
            {
                b.Boards.Box(new Vector3(x - 0.55f, 0.6f, z + dz), new Vector3(0.1f, 1.2f, 0.1f));
                b.Boards.Box(new Vector3(x + 0.55f, 0.6f, z + dz), new Vector3(0.1f, 1.2f, 0.1f));
            }
            b.Boards.Box(new Vector3(x - 0.55f, 1.05f, z + 0.45f), new Vector3(0.06f, 0.08f, 0.9f));
            b.Boards.Box(new Vector3(x + 0.55f, 1.05f, z + 0.45f), new Vector3(0.06f, 0.08f, 0.9f));
            WayMark(b, new Vector3(x - 0.55f, 1.0f, z - 0.07f), 180f);
        }

        /// <summary>フットパスの札。丸い白地に黄色い矢印。yaw は札の表の向き（+z が 0）</summary>
        static void WayMark(Banks b, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Tint(b.Swatch, at, new Vector3(0.14f, 0.14f, 0.012f), rot, SwWhite);
            Tint(b.Swatch, at + rot * new Vector3(0f, 0.01f, 0.008f), new Vector3(0.03f, 0.09f, 0.008f), rot, SwYellow);
            Tint(b.Swatch, at + rot * new Vector3(0f, 0.05f, 0.008f), new Vector3(0.07f, 0.03f, 0.008f), rot * Quaternion.Euler(0f, 0f, 45f), SwYellow);
            Tint(b.Swatch, at + rot * new Vector3(0f, 0.05f, 0.008f), new Vector3(0.07f, 0.03f, 0.008f), rot * Quaternion.Euler(0f, 0f, -45f), SwYellow);
        }

        // ---- 村の物 -----------------------------------------------------------------------

        /// <summary>
        /// 赤い電話ボックス（K6）。コンクリートの台、四方に小さな升目のガラス、上の帯の白い札、
        /// 頭は四方の浅い弧の屋根（王冠の意匠の代わりに頭の飾り）。路地の側に戸
        /// </summary>
        static void PhoneBox(Banks b, Vector3 at, Transform bounds)
        {
            const float w = 0.91f;
            const float body = 2.35f;
            b.Dressed.Box(at + Vector3.up * 0.05f, new Vector3(w + 0.18f, 0.1f, w + 0.18f));
            var foot = at + Vector3.up * 0.1f;
            // 四隅の柱と、腰と頭の帯
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    Tint(b.Swatch, foot + new Vector3(sx * (w * 0.5f - 0.06f), body * 0.5f, sz * (w * 0.5f - 0.06f)), new Vector3(0.13f, body, 0.13f), Quaternion.identity, SwRed);
            Tint(b.Swatch, foot + Vector3.up * 0.12f, new Vector3(w, 0.24f, w), Quaternion.identity, SwRed);
            Tint(b.Swatch, foot + Vector3.up * (body - 0.22f), new Vector3(w, 0.3f, w), Quaternion.identity, SwRed);
            // 四方の面。ガラスの升目を暗い板と赤い桟で
            for (var k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(0f, k * 90f, 0f);
                var n = rot * Vector3.forward;
                var c = foot + n * (w * 0.5f - 0.04f) + Vector3.up * 1.2f;
                Tint(b.Swatch, c, new Vector3(w - 0.22f, 1.72f, 0.02f), rot, SwPane);
                for (var i = 0; i <= 3; i++)
                    Tint(b.Swatch, c + rot * new Vector3((i / 3f - 0.5f) * (w - 0.22f), 0f, 0.02f), new Vector3(0.035f, 1.72f, 0.03f), rot, SwRed);
                for (var j = 0; j <= 8; j++)
                    Tint(b.Swatch, c + Vector3.up * ((j / 8f - 0.5f) * 1.72f) + n * 0.02f, new Vector3(w - 0.22f, 0.035f, 0.03f), rot, SwRed);
                // 上の帯の白い札（TELEPHONE）
                Tint(b.Swatch, foot + n * (w * 0.5f + 0.005f) + Vector3.up * (body - 0.2f), new Vector3(w * 0.62f, 0.12f, 0.02f), rot, SwWhite);
            }
            // 屋根。四方の浅い弧を斜めの面で、頭に飾り
            var top = foot + Vector3.up * body;
            for (var k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(0f, k * 90f, 0f);
                var n = rot * Vector3.forward;
                var r = rot * Vector3.right;
                var p0 = top + n * (w * 0.5f + 0.03f) - r * (w * 0.5f + 0.03f);
                var p1 = top + n * (w * 0.5f + 0.03f) + r * (w * 0.5f + 0.03f);
                var q = top + Vector3.up * 0.26f;
                TintFace(b.Swatch, p0, p1, q + n * 0.12f + r * 0.12f, q + n * 0.12f - r * 0.12f, n + Vector3.up, SwRed);
                TintFace(b.Swatch, p0 + Vector3.down * 0.1f, p1 + Vector3.down * 0.1f, p1, p0, n, SwRed);
            }
            Tint(b.Swatch, top + Vector3.up * 0.28f, new Vector3(0.28f, 0.06f, 0.28f), Quaternion.identity, SwRed);
            Tint(b.Swatch, top + Vector3.up * 0.36f, new Vector3(0.12f, 0.12f, 0.12f), Quaternion.Euler(0f, 45f, 0f), SwRed);
            // 戸の取っ手
            b.Iron.Box(foot + new Vector3(0.28f, 1.1f, w * 0.5f + 0.03f), new Vector3(0.03f, 0.22f, 0.04f));
            Block(bounds, "PhoneBox", at + Vector3.up * 1.2f, new Vector3(w + 0.2f, 2.4f, w + 0.2f));
        }

        /// <summary>
        /// 塀に埋めた赤い郵便ポスト。農場の門の畑の野石の塀の西の端を柱のように高く積み、
        /// その路地の側の面に、赤い箱の口と札を埋める
        /// </summary>
        static void PostBox(Banks b, Vector3 at)
        {
            // 塀を 1.5 m まで積み上げた柱
            b.Stone.Box(at + new Vector3(0f, 0.75f, 0f), new Vector3(1.0f, 1.5f, 0.7f));
            b.Dressed.Box(at + new Vector3(0f, 1.54f, 0f), new Vector3(1.08f, 0.08f, 0.78f));
            var face = at + new Vector3(0f, 0f, 0.36f);
            Tint(b.Swatch, face + Vector3.up * 1.02f, new Vector3(0.46f, 0.62f, 0.05f), Quaternion.identity, SwRed);
            Tint(b.Swatch, face + new Vector3(0f, 1.16f, 0.03f), new Vector3(0.3f, 0.035f, 0.02f), Quaternion.identity, SwBlack);
            Tint(b.Swatch, face + new Vector3(0f, 0.98f, 0.03f), new Vector3(0.22f, 0.12f, 0.015f), Quaternion.identity, SwWhite);
            Tint(b.Swatch, face + new Vector3(0f, 1.27f, 0.03f), new Vector3(0.14f, 0.05f, 0.015f), Quaternion.identity, SwGold);
        }

        /// <summary>木のベンチ。鉄の脚に板の座と背。北（路地）を向ける</summary>
        static void Bench(Banks b, Vector3 at, Transform bounds)
        {
            const float len = 1.6f;
            for (var i = 0; i < 3; i++)
                Tint(b.Swatch, at + new Vector3(0f, 0.45f, -0.12f + i * 0.13f), new Vector3(len, 0.04f, 0.1f), Quaternion.identity, SwBench);
            var back = Quaternion.Euler(-12f, 0f, 0f);
            for (var i = 0; i < 2; i++)
                Tint(b.Swatch, at + new Vector3(0f, 0.68f + i * 0.16f, -0.22f), new Vector3(len, 0.1f, 0.03f), back, SwBench);
            foreach (var dx in new[] { -0.7f, 0.7f })
            {
                b.Iron.Box(at + new Vector3(dx, 0.22f, 0.12f), new Vector3(0.05f, 0.44f, 0.05f));
                b.Iron.Box(at + new Vector3(dx, 0.45f, -0.22f), new Vector3(0.05f, 0.9f, 0.05f), back);
                b.Iron.Box(at + new Vector3(dx, 0.62f, 0f), new Vector3(0.05f, 0.04f, 0.4f));
            }
            // 背に小さな真鍮の札（誰かを偲ぶベンチ）
            Tint(b.Swatch, at + new Vector3(0f, 0.84f, -0.2f), new Vector3(0.18f, 0.05f, 0.02f), back, SwGold);
            Block(bounds, "Bench", at + new Vector3(0f, 0.5f, -0.02f), new Vector3(len + 0.1f, 1f, 0.55f));
        }

        /// <summary>
        /// フィンガーポスト（道標）。白く塗った柱の頭に黒い玉、行き先の腕を三本（村へ・畑の道へ・フットパス）。
        /// 腕は白地で、黒い字の代わりに黒い細い帯
        /// </summary>
        static void Fingerpost(Banks b, Vector3 at)
        {
            Tint(b.Swatch, at + Vector3.up * 1.2f, new Vector3(0.11f, 2.4f, 0.11f), Quaternion.identity, SwWhite);
            Tint(b.Swatch, at + Vector3.up * 2.47f, new Vector3(0.16f, 0.14f, 0.16f), Quaternion.identity, SwBlack);
            Tint(b.Swatch, at + Vector3.up * 2.47f, new Vector3(0.12f, 0.16f, 0.12f), Quaternion.Euler(0f, 45f, 0f), SwBlack);
            // 腕は 320×180 でも板に見える大きさにする。細くすると街灯の腕に見えた
            foreach (var arm in new[] { new Vector3(90f, 2.12f, 0.95f), new Vector3(270f, 2.12f, 0.85f), new Vector3(15f, 1.78f, 0.75f) })
            {
                var rot = Quaternion.Euler(0f, arm.x, 0f);
                var c = at + Vector3.up * arm.y + rot * new Vector3(0f, 0f, arm.z * 0.5f + 0.05f);
                Tint(b.Swatch, c, new Vector3(0.04f, 0.24f, arm.z), rot, SwWhite);
                Tint(b.Swatch, c + rot * new Vector3(0f, 0.04f, 0f), new Vector3(0.045f, 0.04f, arm.z * 0.72f), rot, SwBlack);
                Tint(b.Swatch, c + rot * new Vector3(0f, -0.05f, 0f), new Vector3(0.045f, 0.03f, arm.z * 0.5f), rot, SwBlack);
                // 先を尖らせる
                Tint(b.Swatch, at + Vector3.up * arm.y + rot * new Vector3(0f, 0f, arm.z + 0.05f), new Vector3(0.04f, 0.17f, 0.17f), rot * Quaternion.Euler(45f, 0f, 0f), SwWhite);
            }
        }

        /// <summary>
        /// 路肩に停めた小さなハッチバック。塗装・暗いガラス・黒いタイヤ・灯り。yaw は向き（+z が 0）。
        /// 車の左の車輪を路肩の芝に載せる停め方
        /// </summary>
        static void ParkedCar(Banks b, Vector3 at, float yaw, int paint, Transform bounds)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => at + rot * new Vector3(x, y, z);
            Tint(b.Swatch, p(0f, 0.55f, 0f), new Vector3(1.70f, 0.58f, 3.95f), rot, paint);
            // 客室。前と後ろを傾けたガラス
            Tint(b.Swatch, p(0f, 1.1f, -0.35f), new Vector3(1.52f, 0.52f, 2.0f), rot, SwCarGlass);
            Tint(b.Swatch, p(0f, 1.38f, -0.40f), new Vector3(1.5f, 0.05f, 1.9f), rot, paint);
            Tint(b.Swatch, p(0f, 1.06f, 0.72f), new Vector3(1.46f, 0.05f, 0.78f), rot * Quaternion.Euler(-36f, 0f, 0f), SwCarGlass);
            Tint(b.Swatch, p(0f, 1.1f, -1.42f), new Vector3(1.46f, 0.05f, 0.5f), rot * Quaternion.Euler(58f, 0f, 0f), SwCarGlass);
            // 窓の間の柱
            foreach (var sx in new[] { -0.77f, 0.77f })
            {
                Tint(b.Swatch, p(sx, 1.1f, 0.3f), new Vector3(0.05f, 0.55f, 0.08f), rot * Quaternion.Euler(-30f, 0f, 0f), paint);
                Tint(b.Swatch, p(sx, 1.1f, -0.4f), new Vector3(0.05f, 0.55f, 0.1f), rot, paint);
                Tint(b.Swatch, p(sx, 1.1f, -1.3f), new Vector3(0.05f, 0.55f, 0.22f), rot, paint);
            }
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                {
                    var w = p((i * 2 - 1) * 0.74f, 0.31f, (j * 2 - 1) * 1.28f);
                    Tint(b.Swatch, w, new Vector3(0.2f, 0.6f, 0.6f), rot, SwTyre);
                    Tint(b.Swatch, w + rot * new Vector3((i * 2 - 1) * 0.11f, 0f, 0f), new Vector3(0.02f, 0.3f, 0.3f), rot, SwChrome);
                }
            Tint(b.Swatch, p(0f, 0.62f, 1.98f), new Vector3(1.4f, 0.14f, 0.02f), rot, SwChrome);
            Tint(b.Swatch, p(-0.58f, 0.74f, 1.975f), new Vector3(0.3f, 0.12f, 0.02f), rot, SwWhite);
            Tint(b.Swatch, p(0.58f, 0.74f, 1.975f), new Vector3(0.3f, 0.12f, 0.02f), rot, SwWhite);
            Tint(b.Swatch, p(-0.62f, 0.82f, -1.975f), new Vector3(0.22f, 0.16f, 0.02f), rot, SwRed);
            Tint(b.Swatch, p(0.62f, 0.82f, -1.975f), new Vector3(0.22f, 0.16f, 0.02f), rot, SwRed);
            Tint(b.Swatch, p(0f, 0.5f, -1.99f), new Vector3(0.5f, 0.11f, 0.02f), rot, SwWhite);
            var box = new GameObject("ParkedCar");
            box.transform.SetParent(bounds, false);
            box.transform.localPosition = p(0f, 0.8f, 0f);
            box.transform.localRotation = rot;
            box.AddComponent<BoxCollider>().size = new Vector3(1.9f, 1.6f, 4.1f);
        }

        // ---- 車の着く所 ---------------------------------------------------------------------

        /// <summary>
        /// 場面 8 の車の止まった姿。場面 8 が焼いた車体の mesh（<c>Assets/Models/generated/drive/</c>）と
        /// マテリアルをそのまま使い、素材ごとに車体とドアを一枚にまとめて置く。
        /// 車内の内張り・計器・紙・水滴は外からはほとんど見えないので置かない（内張りだけで三角が 1,600 ある）。当たりは箱一つ
        /// </summary>
        static void DriveCar(Transform parent, Transform bounds)
        {
            var root = Child(parent, "DriveCar");
            Clear(root);
            root.localPosition = DriveCarAt;
            root.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var parts = new[]
            {
                new[] { "CarBody", "CarBody", "DoorBody" }, new[] { "CarSteel", "CarSteel", "DoorSteel" },
                new[] { "CarGap", "CarGap", "DoorGap" }, new[] { "CarGlass", "CarGlass", "DoorGlass" },
                new[] { "CarTyre", "CarTyre" },
                new[] { "CarLamp", "CarLamp" }, new[] { "CarSeat", "CarSeat" },
            };
            var made = 0;
            foreach (var part in parts)
            {
                var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Drive/" + part[0] + ".mat");
                var list = new List<CombineInstance>();
                for (var i = 1; i < part.Length; i++)
                {
                    var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/generated/drive/" + part[i] + ".asset");
                    if (m != null) list.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
                }
                if (mat == null || list.Count == 0) continue;
                var mesh = new Mesh { name = "DriveCar" + part[0] };
                mesh.CombineMeshes(list.ToArray(), true, true);
                mesh.RecalculateBounds();
                var path = Generated + "DriveCar" + part[0] + ".asset";
                ProcMesh.Save(mesh, path);
                var go = new GameObject("DriveCar" + part[0]);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                made++;
            }
            if (made == 0) Debug.LogWarning("場面 8 の車の mesh が無い。HalfAware/Build the drive で一度組むと置ける");
            // 影を落とすのは車体と鉄と車輪だけ。細かな部品まで落とすと、影の描く回数が車だけで 8 回になる
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (!(r.name.EndsWith("CarBody") || r.name.EndsWith("CarSteel") || r.name.EndsWith("CarTyre")))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var box = new GameObject("DriveCar");
            box.transform.SetParent(bounds, false);
            box.transform.localPosition = DriveCarAt + Vector3.up * 1f;
            box.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            box.AddComponent<BoxCollider>().size = new Vector3(2.1f, 2f, 5.0f);
        }

        // ---- 札の側へ渡す位置 ---------------------------------------------------------------

        /// <summary>花箱の株の足元</summary>
        static readonly List<Vector3> WindowPlants = new List<Vector3>();
        /// <summary>吊り鉢の縁の高さの中心</summary>
        static readonly List<Vector3> Baskets = new List<Vector3>();
        /// <summary>玄関のまわりのバラ。(戸の芯の x, 壁の z, 外の向き, 大きさ)</summary>
        static readonly List<Vector4> DoorRoses = new List<Vector4>();
        /// <summary>壁と塀のアイビー。(足元, 横の半分の向き)</summary>
        static readonly List<KeyValuePair<Vector3, Vector3>> WallIvy = new List<KeyValuePair<Vector3, Vector3>>();
    }
}
