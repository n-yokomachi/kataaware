using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 片割れの家と、小さな前庭、脇の煉瓦の小路の格子戸（設計書 2 節の 4）。
    ///
    /// **石の二階建てで、寝室が二つか三つのふつうの一戸建て。** 間口 8 m・奥行き 6.2 m の一重の棟に、
    /// 急勾配（50 度）の石版の屋根を切妻で載せる。コッツウォルズの家の形を三つ大きく置く:
    /// **妻の立ち上がりと笠石**、**両妻の煙突**、**石の桟と水切りの付いた窓**。
    /// 家の中は作らない。窓の奥に暗い室内の面を置き、両脇にカーテンを下げて、空っぽに見えないようにする。
    ///
    /// **開ける物は形を分けておく。** 裏口の戸（場面 10 で開く）と格子戸は、丁番の所を原点にした
    /// 子にしてあり、y で回せば開く。いまはどちらも閉じた形で置く。格子戸は調べて開ける（<see cref="SwingGate"/>、
    /// 設計書 7 節）。閉じている間は口を塞ぐ当たりが効き、開けると切れる。
    /// 戸口の奥は深さ 1.2 m の暗がりにしてあるので、開けても家の中の虚空は見えない
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- 家の寸法 -------------------------------------------------------------------

        public const float HouseWest = -1.5f;
        public const float HouseEast = 6.5f;
        /// <summary>家の正面の壁。路地の芯から 9 m、前庭の石垣から 5.6 m 下がる</summary>
        public const float HouseFront = 9.0f;
        /// <summary>家の裏の壁。裏庭はここから奥へ 20 m</summary>
        public const float HouseRear = 15.2f;
        /// <summary>軒の高さ。二階建て</summary>
        const float Eaves = 5.0f;
        /// <summary>屋根の勾配。度。コッツウォルズの石版の屋根は急</summary>
        const float Pitch = 50f;
        /// <summary>妻の壁の厚み。屋根の上に立ち上がって笠石を載せる</summary>
        const float Gable = 0.4f;
        /// <summary>妻の立ち上がりの、屋根の面からの高さ</summary>
        const float Parapet = 0.3f;
        /// <summary>軒の出</summary>
        const float Overhang = 0.28f;

        static float RidgeZ { get { return (HouseFront + HouseRear) * 0.5f; } }
        static float Ridge { get { return Eaves + (HouseRear - HouseFront) * 0.5f * Mathf.Tan(Pitch * Mathf.Deg2Rad); } }

        /// <summary>玄関の戸の芯の x。前庭の小路の芯</summary>
        public const float FrontDoorX = 2.5f;
        /// <summary>裏口の戸の芯の x。テラスに面する</summary>
        public const float BackDoorX = 3.9f;

        /// <summary>脇の煉瓦の小路の芯の x（前庭の石垣から格子戸まで）</summary>
        public const float SidePathX = -4.2f;
        /// <summary>小路の幅</summary>
        public const float PathWide = 1.1f;
        /// <summary>格子戸の線。家の西の脇を仕切る石の塀と、その中の戸</summary>
        public const float GateZ = 12.0f;
        /// <summary>格子戸の柱の内の縁。戸は東の柱に丁番を持ち、西へ閉じる</summary>
        public const float GatePostWest = -4.80f;
        public const float GatePostEast = -3.60f;
        const float GatePost = 0.12f;

        /// <summary>前庭の石垣の厚みと高さ</summary>
        const float FrontWallThick = 0.45f;
        const float FrontWallHigh = 0.85f;

        // 窓。(芯の位置, 幅, 下, 上, 仕切りの数)。正面と裏は x、妻は z
        static readonly Vector4[] FrontWindows =
        {
            new Vector4(0.30f, 1.30f, 0.85f, 2.05f), new Vector4(4.75f, 1.30f, 0.85f, 2.05f),
            new Vector4(0.30f, 1.10f, 3.20f, 4.30f), new Vector4(FrontDoorX, 0.80f, 3.25f, 4.25f), new Vector4(4.75f, 1.10f, 3.20f, 4.30f),
        };
        static readonly Vector4[] RearWindows =
        {
            new Vector4(-0.20f, 1.30f, 0.90f, 2.05f), new Vector4(1.90f, 1.00f, 1.05f, 2.05f), new Vector4(5.60f, 0.70f, 1.10f, 1.95f),
            new Vector4(-0.20f, 1.10f, 3.20f, 4.30f), new Vector4(1.90f, 0.60f, 3.40f, 4.20f), new Vector4(4.70f, 1.10f, 3.20f, 4.30f),
        };
        static readonly Vector4[] WestWindows =
        {
            new Vector4(12.1f, 0.70f, 3.30f, 4.20f),
        };

        // ---- 組み立て -------------------------------------------------------------------

        static void House(Transform parent, Banks b)
        {
            HouseWalls(b);
            HouseRoof(b);
            HouseChimneys(b);
            foreach (var w in FrontWindows) WindowZ(b, HouseFront, -1, w);
            foreach (var w in RearWindows) WindowZ(b, HouseRear, 1, w);
            foreach (var w in WestWindows) WindowX(b, HouseWest, -1, w);
            FrontDoor(parent, b);
            BackDoor(parent, b);
            HouseRain(b);
            FrontGarden(b);
            SideGate(parent, b);
            HouseBounds(Child(parent, "Bounds"));
        }

        // ---- 壁と屋根 -------------------------------------------------------------------

        /// <summary>四方の壁の外の面。窓と戸の抜けを避けて張る。妻は軒より上を三角と立ち上がりで</summary>
        static void HouseWalls(Banks b)
        {
            var front = new List<Vector4>();
            foreach (var w in FrontWindows) front.Add(Hole(w));
            front.Add(new Vector4(FrontDoorX - 0.5f, FrontDoorX + 0.5f, 0f, 2.1f));
            b.Stone.FaceZHoles(HouseFront, HouseWest, HouseEast, 0f, Eaves, -1, front);

            var rear = new List<Vector4>();
            foreach (var w in RearWindows) rear.Add(Hole(w));
            rear.Add(new Vector4(BackDoorX - 0.47f, BackDoorX + 0.47f, 0f, 2.05f));
            b.Stone.FaceZHoles(HouseRear, HouseWest, HouseEast, 0f, Eaves, 1, rear);

            var west = new List<Vector4>();
            foreach (var w in WestWindows) west.Add(Hole(w));
            b.Stone.FaceXHoles(HouseWest, HouseFront, HouseRear, 0f, Eaves, -1, west);
            b.Stone.FaceX(HouseEast, HouseFront, HouseRear, 0f, Eaves, 1);

            // 妻。軒から上の三角に、屋根の面から Parapet だけ立ち上がる帯を足す
            foreach (var x in new[] { HouseWest, HouseEast })
            {
                var o = x < 1f ? -1f : 1f;
                var f0 = new Vector3(x, Eaves, HouseFront);
                var r0 = new Vector3(x, Eaves, HouseRear);
                var top = new Vector3(x, Ridge + Parapet, RidgeZ);
                var f1 = new Vector3(x, Eaves + Parapet, HouseFront);
                var r1 = new Vector3(x, Eaves + Parapet, HouseRear);
                Face(b.Stone, f0, r0, r1, f1, Vector3.right * o);
                Face(b.Stone, f1, r1, top, top, Vector3.right * o);
                // 立ち上がりの内の面（屋根の上に見える）
                var xi = x - o * Gable;
                RoofRise(b.Stone, xi, -o);
                // 笠石。立ち上がりの頭に斜面に沿って。軒の端に持ち送りの石（kneeler）
                var xc = x - o * Gable * 0.5f;
                Beam(b.Dressed, new Vector3(xc, Eaves + Parapet + 0.02f, HouseFront - 0.06f),
                    new Vector3(xc, Ridge + Parapet + 0.06f, RidgeZ), Gable + 0.12f, 0.12f);
                Beam(b.Dressed, new Vector3(xc, Eaves + Parapet + 0.02f, HouseRear + 0.06f),
                    new Vector3(xc, Ridge + Parapet + 0.06f, RidgeZ), Gable + 0.12f, 0.12f);
                // 持ち送りの石は軒の出の先まで伸ばし、屋根の端の切り口を隠す
                var kz = 0.3f + Overhang + 0.05f;
                b.Dressed.Box(new Vector3(xc + o * 0.03f, Eaves - 0.1f, HouseFront + 0.3f - kz * 0.5f), new Vector3(Gable + 0.16f, 0.8f, kz));
                b.Dressed.Box(new Vector3(xc + o * 0.03f, Eaves - 0.1f, HouseRear - 0.3f + kz * 0.5f), new Vector3(Gable + 0.16f, 0.8f, kz));
                // 頂の飾り
                b.Dressed.Box(new Vector3(xc, Ridge + Parapet + 0.24f, RidgeZ), new Vector3(0.22f, 0.30f, 0.22f));
                b.Dressed.Box(new Vector3(xc, Ridge + Parapet + 0.24f, RidgeZ), new Vector3(0.22f, 0.30f, 0.22f), Quaternion.Euler(0f, 45f, 0f));
            }

            // 正面と裏の隅石。角を少し出した大きめの石を互い違いに積む。320×180 では角の明るい縁として効く
            foreach (var x in new[] { HouseWest, HouseEast })
                foreach (var z in new[] { HouseFront, HouseRear })
                {
                    var ox = x < 1f ? 1f : -1f;
                    var oz = z < 12f ? 1f : -1f;
                    for (var i = 0; i < 12; i++)
                    {
                        var y = 0.2f + i * 0.4f;
                        var longX = i % 2 == 0;
                        var sx = longX ? 0.46f : 0.26f;
                        var sz = longX ? 0.26f : 0.46f;
                        b.Dressed.Box(new Vector3(x + ox * (sx * 0.5f - 0.02f), y, z + oz * (sz * 0.5f - 0.02f)), new Vector3(sx, 0.36f, sz));
                    }
                }
        }

        /// <summary>屋根の面の上に見える、妻の立ち上がりの内の面。x は内の面の位置、sign はその面の向き</summary>
        static void RoofRise(Bank b, float x, float sign)
        {
            var t = Mathf.Tan(Pitch * Mathf.Deg2Rad);
            var zs = new[] { HouseFront - Overhang, RidgeZ, HouseRear + Overhang };
            for (var k = 0; k < 2; k++)
            {
                var za = zs[k];
                var zb = zs[k + 1];
                var ya = RoofY(za, t);
                var yb = RoofY(zb, t);
                Face(b, new Vector3(x, ya, za), new Vector3(x, yb, zb), new Vector3(x, yb + Parapet, zb), new Vector3(x, ya + Parapet, za),
                    Vector3.right * sign);
            }
        }

        static float RoofY(float z, float tan)
        {
            var run = Mathf.Min(z - HouseFront, HouseRear - z);
            return Eaves + run * tan;
        }

        /// <summary>
        /// 石版の屋根。妻の立ち上がりの内側に、正面と裏の二つの斜面。
        /// 軒は Overhang だけ出して、軒先の小口と軒裏も張る。棟に棟石
        /// </summary>
        static void HouseRoof(Banks b)
        {
            var t = Mathf.Tan(Pitch * Mathf.Deg2Rad);
            var x0 = HouseWest + Gable;
            var x1 = HouseEast - Gable;
            foreach (var side in new[] { -1f, 1f })
            {
                var zEave = side < 0f ? HouseFront - Overhang : HouseRear + Overhang;
                var yEave = RoofY(zEave, t);
                var eave0 = new Vector3(x0, yEave, zEave);
                var eave1 = new Vector3(x1, yEave, zEave);
                var ridge0 = new Vector3(x0, Ridge, RidgeZ);
                var ridge1 = new Vector3(x1, Ridge, RidgeZ);
                var outward = new Vector3(0f, 1f, side / t).normalized;
                Face(b.Slate, eave0, eave1, ridge1, ridge0, outward);
                // 軒先の小口と軒裏
                var drop = new Vector3(0f, -0.14f, 0f);
                Face(b.Slate, eave0 + drop, eave1 + drop, eave1, eave0, new Vector3(0f, 0f, side));
                var wallZ = side < 0f ? HouseFront : HouseRear;
                Face(b.Dark, eave0 + drop, eave1 + drop, new Vector3(x1, yEave - 0.14f, wallZ), new Vector3(x0, yEave - 0.14f, wallZ), Vector3.down);
                // 雨樋
                b.Iron.Box(new Vector3((x0 + x1) * 0.5f, yEave - 0.16f, zEave + side * 0.05f), new Vector3(x1 - x0 + 0.1f, 0.11f, 0.12f));
            }
            // 棟石
            b.Dressed.Box(new Vector3((x0 + x1) * 0.5f, Ridge + 0.04f, RidgeZ), new Vector3(x1 - x0, 0.16f, 0.30f));
        }

        /// <summary>両妻の煙突。妻の立ち上がりの上に石の胴を立て、笠石と土管を二本ずつ</summary>
        static void HouseChimneys(Banks b)
        {
            foreach (var x in new[] { HouseWest + 0.42f, HouseEast - 0.42f })
            {
                var foot = Ridge - 0.9f;
                var top = Ridge + 1.35f;
                b.Stone.Box(new Vector3(x, (foot + top) * 0.5f, RidgeZ), new Vector3(0.84f, top - foot, 0.66f));
                b.Dressed.Box(new Vector3(x, top + 0.06f, RidgeZ), new Vector3(0.98f, 0.12f, 0.80f));
                b.Dressed.Box(new Vector3(x, top - 0.28f, RidgeZ), new Vector3(0.92f, 0.08f, 0.74f));
                Prism(b.Clay, new Vector3(x - 0.18f, top + 0.12f, RidgeZ), 0.11f, 0.38f, 8, 0.09f);
                Prism(b.Clay, new Vector3(x + 0.18f, top + 0.12f, RidgeZ), 0.11f, 0.30f, 8, 0.09f);
            }
        }

        /// <summary>雨樋から下りる竪樋。正面の西と、裏の東（雨水の樽へ）</summary>
        static void HouseRain(Banks b)
        {
            b.Iron.Box(new Vector3(HouseWest + 0.25f, Eaves * 0.5f, HouseFront - 0.1f), new Vector3(0.08f, Eaves, 0.08f));
            b.Iron.Box(new Vector3(HouseEast - 0.25f, Eaves * 0.5f + 0.45f, HouseRear + 0.1f), new Vector3(0.08f, Eaves - 0.9f, 0.08f));
            b.Iron.Box(new Vector3(HouseEast - 0.25f, 0.98f, HouseRear + 0.3f), new Vector3(0.08f, 0.08f, 0.40f));
        }

        // ---- 窓と戸 ---------------------------------------------------------------------

        /// <summary>窓の値 (芯, 幅, 下, 上) を、壁の穴 (横の始め, 終わり, 下, 上) に直す</summary>
        static Vector4 Hole(Vector4 w)
        {
            return new Vector4(w.x - w.y * 0.5f, w.x + w.y * 0.5f, w.z, w.w);
        }

        /// <summary>窓の奥行き。室内の面までの深さ</summary>
        const float WindowDeep = 0.34f;

        /// <summary>
        /// z が一定の壁の窓。sign は壁の外の向き（正面は -1、裏は +1）。
        ///
        /// 石の抜け（側・下・上の面）の奥に暗い室内の面、その手前の両脇にカーテン。
        /// 開口の中に白い窓枠と、幅のある窓は真ん中に石の桟。外には石の窓台と、上に水切り（hood mould）
        /// </summary>
        static void WindowZ(Banks b, float z, int sign, Vector4 w)
        {
            var x0 = w.x - w.y * 0.5f;
            var x1 = w.x + w.y * 0.5f;
            var y0 = w.z;
            var y1 = w.w;
            var s = (float)sign;
            var inner = z - s * WindowDeep;
            // 抜けの面。外から見て内を向く
            Face(b.Dressed, new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x0, y1, inner), new Vector3(x0, y0, inner), Vector3.right);
            Face(b.Dressed, new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x1, y1, inner), new Vector3(x1, y0, inner), Vector3.left);
            Face(b.Dressed, new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y0, inner), new Vector3(x0, y0, inner), Vector3.up);
            Face(b.Dressed, new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y1, inner), new Vector3(x0, y1, inner), Vector3.down);
            // 室内
            Face(b.Dark, new Vector3(x0, y0, inner), new Vector3(x1, y0, inner), new Vector3(x1, y1, inner), new Vector3(x0, y1, inner), Vector3.forward * s);
            // カーテン。両脇に下げ、上に短い垂れ
            var cz = z - s * (WindowDeep - 0.07f);
            var cw = Mathf.Max(0.16f, w.y * 0.24f);
            b.Cloth.Box(new Vector3(x0 + cw * 0.5f + 0.02f, (y0 + y1) * 0.5f - 0.02f, cz), new Vector3(cw, y1 - y0 - 0.08f, 0.04f));
            b.Cloth.Box(new Vector3(x1 - cw * 0.5f - 0.02f, (y0 + y1) * 0.5f - 0.02f, cz), new Vector3(cw, y1 - y0 - 0.08f, 0.04f));
            b.Cloth.Box(new Vector3(w.x, y1 - 0.08f, cz), new Vector3(w.y - 0.04f, 0.12f, 0.04f));
            // 窓枠
            var fz = z - s * 0.12f;
            b.Paint.Box(new Vector3(w.x, y0 + 0.03f, fz), new Vector3(w.y, 0.06f, 0.05f));
            b.Paint.Box(new Vector3(w.x, y1 - 0.03f, fz), new Vector3(w.y, 0.06f, 0.05f));
            b.Paint.Box(new Vector3(x0 + 0.03f, (y0 + y1) * 0.5f, fz), new Vector3(0.06f, y1 - y0, 0.05f));
            b.Paint.Box(new Vector3(x1 - 0.03f, (y0 + y1) * 0.5f, fz), new Vector3(0.06f, y1 - y0, 0.05f));
            // 桟の横。上の欄間を仕切る
            b.Paint.Box(new Vector3(w.x, y0 + (y1 - y0) * 0.68f, fz), new Vector3(w.y, 0.04f, 0.04f));
            if (w.y > 0.9f)
                b.Dressed.Box(new Vector3(w.x, (y0 + y1) * 0.5f, z - s * 0.08f), new Vector3(0.11f, y1 - y0, 0.16f));
            else
                b.Paint.Box(new Vector3(w.x, (y0 + y1) * 0.5f, fz), new Vector3(0.04f, y1 - y0, 0.04f));
            // 窓台と水切り
            b.Dressed.Box(new Vector3(w.x, y0 - 0.04f, z + s * 0.04f), new Vector3(w.y + 0.16f, 0.09f, 0.14f));
            b.Dressed.Box(new Vector3(w.x, y1 + 0.10f, z + s * 0.04f), new Vector3(w.y + 0.26f, 0.08f, 0.10f));
            b.Dressed.Box(new Vector3(x0 - 0.09f, y1 + 0.02f, z + s * 0.04f), new Vector3(0.08f, 0.18f, 0.10f));
            b.Dressed.Box(new Vector3(x1 + 0.09f, y1 + 0.02f, z + s * 0.04f), new Vector3(0.08f, 0.18f, 0.10f));
        }

        /// <summary>x が一定の壁（西の妻）の窓。WindowZ を 90 度回した形</summary>
        static void WindowX(Banks b, float x, int sign, Vector4 w)
        {
            var z0 = w.x - w.y * 0.5f;
            var z1 = w.x + w.y * 0.5f;
            var y0 = w.z;
            var y1 = w.w;
            var s = (float)sign;
            var inner = x - s * WindowDeep;
            Face(b.Dressed, new Vector3(x, y0, z0), new Vector3(x, y1, z0), new Vector3(inner, y1, z0), new Vector3(inner, y0, z0), Vector3.forward);
            Face(b.Dressed, new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(inner, y1, z1), new Vector3(inner, y0, z1), Vector3.back);
            Face(b.Dressed, new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(inner, y0, z1), new Vector3(inner, y0, z0), Vector3.up);
            Face(b.Dressed, new Vector3(x, y1, z0), new Vector3(x, y1, z1), new Vector3(inner, y1, z1), new Vector3(inner, y1, z0), Vector3.down);
            Face(b.Dark, new Vector3(inner, y0, z0), new Vector3(inner, y0, z1), new Vector3(inner, y1, z1), new Vector3(inner, y1, z0), Vector3.right * s);
            var cx = x - s * (WindowDeep - 0.07f);
            var cw = Mathf.Max(0.16f, w.y * 0.24f);
            b.Cloth.Box(new Vector3(cx, (y0 + y1) * 0.5f, z0 + cw * 0.5f + 0.02f), new Vector3(0.04f, y1 - y0 - 0.08f, cw));
            b.Cloth.Box(new Vector3(cx, (y0 + y1) * 0.5f, z1 - cw * 0.5f - 0.02f), new Vector3(0.04f, y1 - y0 - 0.08f, cw));
            var fx = x - s * 0.12f;
            b.Paint.Box(new Vector3(fx, y0 + 0.03f, w.x), new Vector3(0.05f, 0.06f, w.y));
            b.Paint.Box(new Vector3(fx, y1 - 0.03f, w.x), new Vector3(0.05f, 0.06f, w.y));
            b.Paint.Box(new Vector3(fx, (y0 + y1) * 0.5f, z0 + 0.03f), new Vector3(0.05f, y1 - y0, 0.06f));
            b.Paint.Box(new Vector3(fx, (y0 + y1) * 0.5f, z1 - 0.03f), new Vector3(0.05f, y1 - y0, 0.06f));
            b.Paint.Box(new Vector3(fx, (y0 + y1) * 0.5f, w.x), new Vector3(0.04f, y1 - y0, 0.04f));
            b.Dressed.Box(new Vector3(x + s * 0.04f, y0 - 0.04f, w.x), new Vector3(0.14f, 0.09f, w.y + 0.16f));
            b.Dressed.Box(new Vector3(x + s * 0.04f, y1 + 0.10f, w.x), new Vector3(0.10f, 0.08f, w.y + 0.26f));
        }

        /// <summary>
        /// 戸口の抜け。z が一定の壁に、石の抜けと、奥 1.2 m の暗がり（戸を開けたときに見える玄関の奥）。
        /// 外には石の戸枠を少し出す
        /// </summary>
        static void Doorway(Banks b, float z, int sign, float cx, float wide, float high)
        {
            var x0 = cx - wide * 0.5f;
            var x1 = cx + wide * 0.5f;
            var s = (float)sign;
            var inner = z - s * 1.2f;
            Face(b.Dressed, new Vector3(x0, 0f, z), new Vector3(x0, high, z), new Vector3(x0, high, inner), new Vector3(x0, 0f, inner), Vector3.right);
            Face(b.Dressed, new Vector3(x1, 0f, z), new Vector3(x1, high, z), new Vector3(x1, high, inner), new Vector3(x1, 0f, inner), Vector3.left);
            Face(b.Dressed, new Vector3(x0, high, z), new Vector3(x1, high, z), new Vector3(x1, high, inner), new Vector3(x0, high, inner), Vector3.down);
            Face(b.Dark, new Vector3(x0, 0.01f, z), new Vector3(x1, 0.01f, z), new Vector3(x1, 0.01f, inner), new Vector3(x0, 0.01f, inner), Vector3.up);
            Face(b.Dark, new Vector3(x0, 0f, inner), new Vector3(x1, 0f, inner), new Vector3(x1, high, inner), new Vector3(x0, high, inner), Vector3.forward * s);
            // 石の戸枠と楣石
            b.Dressed.Box(new Vector3(x0 - 0.1f, high * 0.5f, z + s * 0.03f), new Vector3(0.2f, high, 0.08f));
            b.Dressed.Box(new Vector3(x1 + 0.1f, high * 0.5f, z + s * 0.03f), new Vector3(0.2f, high, 0.08f));
            b.Dressed.Box(new Vector3(cx, high + 0.12f, z + s * 0.03f), new Vector3(wide + 0.44f, 0.24f, 0.08f));
            // 踏み石
            b.Flag.Box(new Vector3(cx, 0.06f, z + s * 0.28f), new Vector3(wide + 0.5f, 0.12f, 0.56f));
        }

        /// <summary>
        /// 戸の板を一枚、丁番の所を原点にした子として置く。戸は子の +x へ伸び、子の -z が外。
        /// 羽目板四枚と、鉄の取っ手と丁番。y を回せば開く（開く向きは外から見て内へ）
        /// </summary>
        static Transform Leaf(Transform parent, string name, Vector3 hinge, float yaw, float wide, float high)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hinge;
            pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var door = new Bank { Texel = 0.5f };
            var iron = new Bank { Texel = 0.5f };
            door.Box(new Vector3(wide * 0.5f, high * 0.5f, 0f), new Vector3(wide, high, 0.05f));
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                    door.Box(new Vector3(wide * (0.28f + 0.44f * i), high * (0.27f + 0.46f * j), -0.03f),
                        new Vector3(wide * 0.32f, high * 0.36f, 0.02f));
            iron.Box(new Vector3(wide - 0.1f, high * 0.47f, -0.06f), new Vector3(0.03f, 0.16f, 0.04f));
            iron.Box(new Vector3(wide * 0.5f, high * 0.62f, -0.045f), new Vector3(0.10f, 0.12f, 0.02f));
            iron.Box(new Vector3(0.12f, high * 0.2f, -0.03f), new Vector3(0.24f, 0.03f, 0.02f));
            iron.Box(new Vector3(0.12f, high * 0.8f, -0.03f), new Vector3(0.24f, 0.03f, 0.02f));
            Emit(pivot, name + "Leaf", door, DoorMat(), false);
            Emit(pivot, name + "Iron", iron, IronMat(), false);
            return pivot;
        }

        /// <summary>玄関。石の戸枠に、上へ石の庇を持ち送りで。戸は閉じる</summary>
        static void FrontDoor(Transform parent, Banks b)
        {
            const float wide = 1.0f;
            const float high = 2.1f;
            Doorway(b, HouseFront, -1, FrontDoorX, wide, high);
            // 丁番は西の抜けの縁。子の -z（外）が路地の側を向くよう、そのまま置く
            Leaf(parent, "FrontDoor", new Vector3(FrontDoorX - wide * 0.5f + 0.02f, 0.01f, HouseFront + 0.14f), 0f, wide - 0.04f, high - 0.02f);
            // 石の庇
            b.Dressed.Box(new Vector3(FrontDoorX, high + 0.42f, HouseFront - 0.3f), new Vector3(wide + 0.8f, 0.12f, 0.62f));
            foreach (var dx in new[] { -0.55f, 0.55f })
            {
                b.Dressed.Box(new Vector3(FrontDoorX + dx, high + 0.26f, HouseFront - 0.14f), new Vector3(0.12f, 0.22f, 0.28f));
                b.Dressed.Box(new Vector3(FrontDoorX + dx, high + 0.12f, HouseFront - 0.07f), new Vector3(0.12f, 0.12f, 0.14f));
            }
            // 吊り鉢。庇の西の端から
            var hook = new Vector3(FrontDoorX - 0.75f, high + 0.36f, HouseFront - 0.42f);
            b.Iron.Box(hook + Vector3.down * 0.18f, new Vector3(0.015f, 0.36f, 0.015f));
            Prism(b.Iron, hook + Vector3.down * 0.58f, 0.10f, 0.20f, 8, 0.19f);
            HangingBasketAt = hook + Vector3.down * 0.40f;
        }

        /// <summary>吊り鉢の縁の高さの中心。花は BuildVillagePlants が載せる</summary>
        static Vector3 HangingBasketAt;

        /// <summary>
        /// 裏口。場面 10 で開く。戸の子（BackDoor）は丁番を原点にしてあり、y を回すと内へ開く。
        /// 上に石版の小さな庇
        /// </summary>
        static void BackDoor(Transform parent, Banks b)
        {
            const float wide = 0.94f;
            const float high = 2.05f;
            Doorway(b, HouseRear, 1, BackDoorX, wide, high);
            // 裏の壁の外は +z。子の -z を外へ向けるので 180 度回し、丁番を東の縁に置く
            Leaf(parent, "BackDoor", new Vector3(BackDoorX + wide * 0.5f - 0.02f, 0.01f, HouseRear - 0.14f), 180f, wide - 0.04f, high - 0.02f);
            var t = Quaternion.Euler(-18f, 0f, 0f);
            b.Slate.Box(new Vector3(BackDoorX, high + 0.40f, HouseRear + 0.32f), new Vector3(wide + 0.7f, 0.08f, 0.72f), t);
            foreach (var dx in new[] { -0.5f, 0.5f })
                b.Iron.Box(new Vector3(BackDoorX + dx, high + 0.22f, HouseRear + 0.18f), new Vector3(0.05f, 0.05f, 0.40f), Quaternion.Euler(-40f, 0f, 0f));
        }

        // ---- 前庭 -----------------------------------------------------------------------

        /// <summary>
        /// 前庭。路地の側は、野石を低く積んだ足元の上に刈り込んだイチイの生け垣（家の囲いは生け垣を主にする。
        /// オーナー、2026-09-26）。玄関までの煉瓦の小路、窓の花箱、玄関脇のツゲの玉。
        /// 垣の口は二つ。玄関の小路と、家の西の脇へ回る小路。口の脇の野石の柱は残す
        /// </summary>
        static void FrontGarden(Banks b)
        {
            var z0 = NorthEdge;
            var z1 = NorthEdge + FrontWallThick;
            var gaps = new[]
            {
                new Vector2(PlotWest - 0.3f, SidePathX - PathWide * 0.5f - 0.05f),
                new Vector2(SidePathX + PathWide * 0.5f + 0.05f, FrontDoorX - PathWide * 0.5f - 0.05f),
                new Vector2(FrontDoorX + PathWide * 0.5f + 0.05f, PlotEast + 0.35f),
            };
            foreach (var g in gaps)
            {
                // 足元の野石は低く（0.4 m）。その上にイチイ（1.2 m まで）
                b.Stone.Box(new Vector3((g.x + g.y) * 0.5f, 0.2f, (z0 + z1) * 0.5f), new Vector3(g.y - g.x, 0.4f, FrontWallThick));
                Clipped(b, null, null, Shrub.Yew, new Vector3(g.x + 0.05f, 0.38f, (z0 + z1) * 0.5f), new Vector3(g.y - 0.05f, 0.38f, (z0 + z1) * 0.5f),
                    1.2f - 0.38f, FrontWallThick - 0.06f, 7 + (int)(g.x * 3f));
            }
            // 口の脇の柱
            foreach (var x in new[] { SidePathX - PathWide * 0.5f - 0.25f, SidePathX + PathWide * 0.5f + 0.25f,
                FrontDoorX - PathWide * 0.5f - 0.25f, FrontDoorX + PathWide * 0.5f + 0.25f })
            {
                b.Stone.Box(new Vector3(x, 0.55f, (z0 + z1) * 0.5f), new Vector3(0.5f, 1.1f, 0.55f));
                b.Dressed.Box(new Vector3(x, 1.15f, (z0 + z1) * 0.5f), new Vector3(0.6f, 0.1f, 0.65f));
                b.Dressed.Box(new Vector3(x, 1.28f, (z0 + z1) * 0.5f), new Vector3(0.26f, 0.2f, 0.26f), Quaternion.Euler(0f, 45f, 0f));
            }

            // 玄関の小路
            b.Brick.FaceY(0.025f, FrontDoorX - PathWide * 0.5f, FrontDoorX + PathWide * 0.5f, z0, HouseFront - 0.55f, 1);

            // 窓の花箱。一階の二つの窓の台に
            foreach (var w in FrontWindows)
            {
                if (w.z > 1.5f) continue;
                b.Paint.Box(new Vector3(w.x, w.z + 0.08f, HouseFront - 0.20f), new Vector3(w.y + 0.1f, 0.20f, 0.24f));
                b.Soil.Box(new Vector3(w.x, w.z + 0.18f, HouseFront - 0.20f), new Vector3(w.y, 0.02f, 0.18f));
            }
            // 玄関の脇のツゲの玉を鉢に
            foreach (var dx in new[] { -0.85f, 0.85f })
            {
                var foot = new Vector3(FrontDoorX + dx, 0f, HouseFront - 0.55f);
                Prism(b.Clay, foot, 0.2f, 0.36f, 8, 0.25f);
                Ball(b.Hedge, foot + Vector3.up * 0.66f, 0.30f);
            }

            // 前庭の横の境。西は家 B の庭との低い生け垣、東は低い生け垣
            Hedge(b, new Vector3(PlotWest, 0f, z1), new Vector3(PlotWest, 0f, GateZ), 1.0f, 0.6f, 3);
            // 東は放牧地との境。家どうしの境と同じく高く
            Clipped(b, null, null, Shrub.Yew, new Vector3(PlotEast, 0f, z1), new Vector3(PlotEast, 0f, HouseFront), 1.6f, 0.7f, 5);
        }

        /// <summary>
        /// 格子戸。脇の小路を仕切る石の塀と、白く塗った木の門。上に格子の欄間を渡す。
        ///
        /// 戸の板は子（SideGate）にしてあり、東の柱の丁番を原点に、y を回すと開く（庭の側へ）。
        /// **いまは閉じた形で置き、通れる扱いにして当たりを入れない**（設計書 4 節）
        /// </summary>
        static void SideGate(Transform parent, Banks b)
        {
            const float wall = 1.75f;
            const float thick = 0.36f;
            // 塀。西の境から柱まで、柱から家の西の壁まで
            var spans = new[]
            {
                new Vector2(PlotWest - 0.3f, GatePostWest - GatePost),
                new Vector2(GatePostEast + GatePost, HouseWest),
            };
            foreach (var s in spans)
            {
                b.Stone.Box(new Vector3((s.x + s.y) * 0.5f, wall * 0.5f, GateZ), new Vector3(s.y - s.x, wall, thick));
                b.Dressed.Box(new Vector3((s.x + s.y) * 0.5f, wall + 0.05f, GateZ), new Vector3(s.y - s.x + 0.04f, 0.10f, thick + 0.08f));
            }
            // 東の脇（家の東の壁と境の間）も同じ塀で閉じる
            b.Stone.Box(new Vector3((HouseEast + PlotEast + 0.35f) * 0.5f, wall * 0.5f, GateZ), new Vector3(PlotEast + 0.35f - HouseEast, wall, thick));
            b.Dressed.Box(new Vector3((HouseEast + PlotEast + 0.35f) * 0.5f, wall + 0.05f, GateZ), new Vector3(PlotEast + 0.39f - HouseEast, 0.10f, thick + 0.08f));

            // 柱と欄間
            const float postHigh = 2.35f;
            var pw = GatePostWest - GatePost * 0.5f;
            var pe = GatePostEast + GatePost * 0.5f;
            foreach (var x in new[] { pw, pe })
            {
                b.Paint.Box(new Vector3(x, postHigh * 0.5f, GateZ), new Vector3(GatePost, postHigh, GatePost));
                b.Paint.Box(new Vector3(x, postHigh + 0.04f, GateZ), new Vector3(GatePost + 0.05f, 0.08f, GatePost + 0.05f));
            }
            // 欄間の梁。両端を柱の外へ伸ばして、上から見て門の形を立てる
            b.Paint.Box(new Vector3((pw + pe) * 0.5f, postHigh - 0.06f, GateZ), new Vector3(pe - pw + 0.7f, 0.10f, 0.08f));
            b.Paint.Box(new Vector3((pw + pe) * 0.5f, postHigh - 0.42f, GateZ), new Vector3(pe - pw, 0.05f, 0.05f));
            Lattice(b.Paint, new Vector3(pw + 0.06f, postHigh - 0.40f, GateZ), new Vector3(pe - 0.06f, postHigh - 0.40f, GateZ), 0.30f, 0.16f);

            // 戸の板。丁番は東の柱の内の縁
            var leafWide = GatePostEast - GatePostWest - 0.03f;
            var pivot = new GameObject("SideGate").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(GatePostEast - 0.01f, 0f, GateZ);
            pivot.localRotation = Quaternion.identity;
            var paint = new Bank { Texel = 0.5f };
            var iron = new Bank { Texel = 0.5f };
            GateLeaf(paint, iron, leafWide);
            Emit(pivot, "SideGatePaint", paint, PaintMat(), false);
            Emit(pivot, "SideGateIron", iron, IronMat(), false);
            GateLatch(parent, pivot);
        }

        /// <summary>格子戸を調べる対象の id。文面は <see cref="VillageScriptPath"/></summary>
        public const string GateId = "village.gate";
        const string VillageScriptPath = "Assets/Data/VillageScript.asset";
        /// <summary>開ける音。木の格子戸の軋み（Pixabay の Creaky Wooden Gate Opens）を頭から終わりまで鳴らす</summary>
        const string GateSoundPath = VillageAudioImport.GatePath;

        /// <summary>
        /// 格子戸を調べて開ける仕掛け（設計書 7 節）。<see cref="SwingGate"/> に、調べる対象（戸の真ん中）と、
        /// 閉じている間の当たり（戸の口を塞ぐ箱）と、開ける音を繋ぐ。案内の文は村の文面（VillageScript）の label
        /// </summary>
        static void GateLatch(Transform parent, Transform leaf)
        {
            var latch = new GameObject("GateLatch");
            latch.transform.SetParent(parent, false);
            latch.transform.localPosition = new Vector3((GatePostWest + GatePostEast) * 0.5f, 1.15f, GateZ);
            var item = latch.AddComponent<Interactable>();
            var iso = new UnityEditor.SerializedObject(item);
            iso.FindProperty("id").stringValue = GateId;
            iso.FindProperty("script").objectReferenceValue = VillageScript();
            iso.FindProperty("radius").floatValue = 2.2f;
            iso.FindProperty("required").boolValue = false;
            iso.FindProperty("once").boolValue = true;
            iso.ApplyModifiedPropertiesWithoutUndo();

            var shut = new GameObject("GateShut");
            shut.transform.SetParent(latch.transform, false);
            shut.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            var box = shut.AddComponent<BoxCollider>();
            box.size = new Vector3(GatePostEast - GatePostWest, 2.2f, 0.25f);

            var src = latch.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0.8f;
            src.minDistance = 2f;
            src.maxDistance = 20f;
            src.volume = 0.7f;
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(GateSoundPath);
            if (clip == null) Debug.LogWarning("格子戸の音が無い: " + GateSoundPath);

            var gate = latch.AddComponent<SwingGate>();
            var so = new UnityEditor.SerializedObject(gate);
            so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>();
            so.FindProperty("hud").objectReferenceValue = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            so.FindProperty("item").objectReferenceValue = item;
            so.FindProperty("leaf").objectReferenceValue = leaf;
            so.FindProperty("openYaw").floatValue = 100f;
            so.FindProperty("seconds").floatValue = 1.0f;
            so.FindProperty("shut").objectReferenceValue = box;
            so.FindProperty("source").objectReferenceValue = src;
            so.FindProperty("clip").objectReferenceValue = clip;
            // 0 は切らずに終わりまで。開けるだけの音なので、閉める所を切り落とす要が無い
            so.FindProperty("clipLength").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>村の文面。無ければ作り、格子戸の項目を書き直す</summary>
        static RoomScript VillageScript()
        {
            var script = UnityEditor.AssetDatabase.LoadAssetAtPath<RoomScript>(VillageScriptPath);
            if (script == null)
            {
                script = ScriptableObject.CreateInstance<RoomScript>();
                UnityEditor.AssetDatabase.CreateAsset(script, VillageScriptPath);
            }
            var so = new UnityEditor.SerializedObject(script);
            var entries = so.FindProperty("entries");
            entries.arraySize = 1;
            var e = entries.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("id").stringValue = GateId;
            e.FindPropertyRelative("label").stringValue = "開ける";
            e.FindPropertyRelative("lines").arraySize = 0;
            e.FindPropertyRelative("hints").arraySize = 0;
            e.FindPropertyRelative("choice").FindPropertyRelative("question").stringValue = "";
            e.FindPropertyRelative("choice").FindPropertyRelative("afterYes").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.EditorUtility.SetDirty(script);
            return script;
        }

        /// <summary>
        /// 格子戸の板。原点は丁番（東）で、板は -x へ伸びる。
        /// 下は縦の羽目、上は斜めの格子、頭の桟はゆるい弧
        /// </summary>
        static void GateLeaf(Bank paint, Bank iron, float wide)
        {
            const float foot = 0.08f;
            const float mid = 0.92f;
            const float top = 1.52f;
            const float rise = 0.12f;
            // 框
            paint.Box(new Vector3(-0.035f, (foot + top) * 0.5f, 0f), new Vector3(0.07f, top - foot, 0.05f));
            paint.Box(new Vector3(-wide + 0.035f, (foot + top) * 0.5f, 0f), new Vector3(0.07f, top - foot, 0.05f));
            paint.Box(new Vector3(-wide * 0.5f, foot + 0.04f, 0f), new Vector3(wide, 0.08f, 0.05f));
            paint.Box(new Vector3(-wide * 0.5f, mid, 0f), new Vector3(wide, 0.07f, 0.05f));
            // 頭の弧。三つの桟で
            var pts = new[]
            {
                new Vector3(-wide, top, 0f), new Vector3(-wide * 0.72f, top + rise * 0.8f, 0f),
                new Vector3(-wide * 0.28f, top + rise * 0.8f, 0f), new Vector3(0f, top, 0f),
            };
            for (var i = 0; i + 1 < pts.Length; i++) Beam(paint, pts[i], pts[i + 1], 0.07f, 0.05f);
            // 下の羽目。縦の細板を隙間を空けて
            var n = Mathf.RoundToInt(wide / 0.11f);
            for (var i = 1; i < n; i++)
                paint.Box(new Vector3(-wide * i / n, (foot + mid) * 0.5f, 0.01f), new Vector3(0.07f, mid - foot, 0.025f));
            // 上の格子
            Lattice(paint, new Vector3(-wide + 0.07f, mid + 0.04f, 0f), new Vector3(-0.07f, mid + 0.04f, 0f), top - mid - 0.02f, 0.15f);
            // 掛け金と丁番
            iron.Box(new Vector3(-wide + 0.10f, 1.02f, -0.04f), new Vector3(0.16f, 0.04f, 0.03f));
            iron.Box(new Vector3(-0.12f, 0.30f, -0.035f), new Vector3(0.24f, 0.03f, 0.02f));
            iron.Box(new Vector3(-0.12f, 1.30f, -0.035f), new Vector3(0.24f, 0.03f, 0.02f));
        }

        /// <summary>
        /// 斜めの格子。a から b の水平な下辺の上に、高さ high の菱形の網を細い角材で組む。
        /// step は菱形の間隔
        /// </summary>
        static void Lattice(Bank b, Vector3 a, Vector3 c, float high, float step)
        {
            var run = c - a;
            var len = run.magnitude;
            var dir = run / len;
            var n = Mathf.CeilToInt((len + high) / step);
            for (var i = -n; i <= n; i++)
            {
                foreach (var sgn in new[] { 1f, -1f })
                {
                    // 下辺の s から 45 度で上がる線を、四角の中に切り取る
                    var s0 = i * step;
                    var p0 = new Vector2(s0, 0f);
                    var p1 = new Vector2(s0 + sgn * high, high);
                    if (!Clip(ref p0, ref p1, len, high)) continue;
                    Beam(b, a + dir * p0.x + Vector3.up * p0.y, a + dir * p1.x + Vector3.up * p1.y, 0.025f, 0.02f);
                }
            }
        }

        /// <summary>線分を [0, len]×[0, high] の四角へ切り取る。残らなければ false</summary>
        static bool Clip(ref Vector2 p0, ref Vector2 p1, float len, float high)
        {
            var t0 = 0f;
            var t1 = 1f;
            var d = p1 - p0;
            float[] pp = { -d.x, d.x, -d.y, d.y };
            float[] qq = { p0.x, len - p0.x, p0.y, high - p0.y };
            for (var k = 0; k < 4; k++)
            {
                if (Mathf.Abs(pp[k]) < 1e-6f) { if (qq[k] < 0f) return false; continue; }
                var t = qq[k] / pp[k];
                if (pp[k] < 0f) t0 = Mathf.Max(t0, t);
                else t1 = Mathf.Min(t1, t);
            }
            if (t0 >= t1 - 1e-3f) return false;
            var a = p0 + d * t0;
            var e = p0 + d * t1;
            p0 = a;
            p1 = e;
            return (p1 - p0).magnitude > 0.03f;
        }

        /// <summary>
        /// 野石の塀の頭の、縦に立てて詰めた笠石（cock and hen）。高いのと低いのを混ぜる。
        /// **石を隙間なく詰め、塀と同じ石で積む。** 明るい石を間を空けて並べたら、遠目に白い杭の柵に見えた
        /// </summary>
        static void CockAndHen(Banks b, Vector3 from, Vector3 to, float thick)
        {
            var run = to - from;
            var len = run.magnitude;
            if (len < 0.05f) return;
            var dir = run / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var n = Mathf.RoundToInt(len / 0.09f);
            for (var i = 0; i < n; i++)
            {
                var at = from + dir * ((i + 0.5f) * len / n);
                var tall = i % 3 == 0 ? 0.22f : 0.15f + Hash(41, i) * 0.03f;
                b.Stone.Box(at + Vector3.up * (tall * 0.5f), new Vector3(thick, tall, len / n - 0.008f), rot);
            }
        }

        /// <summary>刈り込んだ生け垣。箱の上に株ごとの箱を背を振って重ね、頭を不揃いにする</summary>
        static void Hedge(Banks b, Vector3 from, Vector3 to, float high, float thick, int seed)
        {
            var run = to - from;
            var len = run.magnitude;
            if (len < 0.05f) return;
            var dir = run / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            b.Hedge.Box((from + to) * 0.5f + Vector3.up * (high * 0.5f), new Vector3(thick, high, len), rot);
            var n = Mathf.Max(1, Mathf.RoundToInt(len / 0.7f));
            for (var i = 0; i < n; i++)
            {
                var at = from + dir * (len * (i + 0.5f) / n);
                var bump = 0.06f + Hash(seed, i) * 0.14f;
                b.Hedge.Box(at + Vector3.up * (high + bump * 0.5f - 0.02f), new Vector3(thick * 0.86f, bump, len / n * 0.9f), rot);
            }
        }

        /// <summary>丸く刈ったツゲの玉。向きを変えた箱を二つ重ねて角を丸める</summary>
        static void Ball(Bank b, Vector3 centre, float r)
        {
            b.Box(centre, Vector3.one * (r * 1.6f));
            b.Box(centre, new Vector3(r * 1.9f, r * 1.25f, r * 1.9f), Quaternion.Euler(0f, 45f, 0f));
            b.Box(centre, new Vector3(r * 1.25f, r * 1.9f, r * 1.25f), Quaternion.Euler(45f, 0f, 45f));
        }

        // ---- 当たり ---------------------------------------------------------------------

        /// <summary>家・前庭の塀・格子戸の塀と柱・前庭の横の生け垣</summary>
        static void HouseBounds(Transform parent)
        {
            Block(parent, "House", new Vector3((HouseWest + HouseEast) * 0.5f, 3f, (HouseFront + HouseRear) * 0.5f),
                new Vector3(HouseEast - HouseWest, 6f, HouseRear - HouseFront));
            var zc = NorthEdge + FrontWallThick * 0.5f;
            Wall(parent, "FrontWallW", new Vector3(PlotWest - 0.3f, 0f, zc), new Vector3(SidePathX - PathWide * 0.5f - 0.2f, 0f, zc), 1.2f, FrontWallThick);
            Wall(parent, "FrontWallM", new Vector3(SidePathX + PathWide * 0.5f + 0.2f, 0f, zc), new Vector3(FrontDoorX - PathWide * 0.5f - 0.2f, 0f, zc), 1.2f, FrontWallThick);
            Wall(parent, "FrontWallE", new Vector3(FrontDoorX + PathWide * 0.5f + 0.2f, 0f, zc), new Vector3(PlotEast + 0.3f, 0f, zc), 1.2f, FrontWallThick);
            Wall(parent, "GateWallW", new Vector3(PlotWest - 0.3f, 0f, GateZ), new Vector3(GatePostWest, 0f, GateZ), 2.2f, 0.4f);
            Wall(parent, "GateWallE", new Vector3(GatePostEast, 0f, GateZ), new Vector3(HouseWest, 0f, GateZ), 2.2f, 0.4f);
            Wall(parent, "SideWallE", new Vector3(HouseEast, 0f, GateZ), new Vector3(PlotEast + 0.35f, 0f, GateZ), 2.2f, 0.4f);
            Wall(parent, "FrontHedgeW", new Vector3(PlotWest, 0f, NorthEdge), new Vector3(PlotWest, 0f, GateZ), 2.2f, 0.6f);
            Wall(parent, "FrontHedgeE", new Vector3(PlotEast, 0f, NorthEdge), new Vector3(PlotEast, 0f, HouseFront), 2.2f, 0.6f);
        }
    }
}
