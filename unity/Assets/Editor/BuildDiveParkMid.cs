using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公園の中の層。柵の外の通りと、向かいの煉瓦のテラスハウス（設計書 9.1 節「公園の作り込み」の「中」）。
    /// 中心（<see cref="ParkFarCentre"/>）から 60 m ほどまで。その先は書き割り（<c>BuildDiveParkFar.cs</c>）。
    ///
    /// **公園は四方を通りに囲まれた、ロンドンのスクエアの形にする。** 柵の外に歩道、縁石、車道、向かいの歩道、
    /// 低い塀の前庭、その奥にヴィクトリア朝の二〜三階建てのテラスハウスが並ぶ。
    /// 門の正面だけは横丁が奥へ伸びて、突き当たりの家並みまで通りが見通せる。
    /// 午後の日はこの横丁の上の低い所にあるので、門へ向かう人は家並みの輪郭と煙突の列を逆光で見る。
    ///
    /// 通りの断面は四辺とも同じ（柵の線から外へ）。
    /// <list type="table">
    /// <item><term>0〜2.6 m</term><description>公園の側の歩道。縁石沿いは黄色い二重線（公園の脇には停めさせない）</description></item>
    /// <item><term>2.7〜9.7 m</term><description>車道。真ん中に白い破線。向こう側に停めた車</description></item>
    /// <item><term>9.9〜13.4 m</term><description>向こうの歩道。街灯・街路樹・郵便ポスト・バス停</description></item>
    /// <item><term>13.4〜14.8 m</term><description>前庭の低い塀と生垣。その奥が家の表</description></item>
    /// </list>
    ///
    /// **家並みは公営住宅の中の層の長屋（<see cref="MidTerrace"/>）で組む。** 黄色いストック煉瓦、白い窓枠、
    /// 一階の張り出し窓、色の違う玄関、スレートの屋根、戸境ごとの煙突の束。
    /// マテリアルも同じ物（<c>EstateMid*</c>）を使い、影は落とさない。日は家並みの向こうの低い所にあるので、
    /// 落とすと向かいの家の影が通りを越えて公園の半分まで伸び、午後の公園が日陰に沈む
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 通りの断面（柵の線から外へ） ------------------------------------------

        /// <summary>公園の側の歩道の外の縁（縁石の線）</summary>
        const float StreetKerb = 2.6f;
        /// <summary>車道の手前と向こうの縁</summary>
        const float StreetRoad0 = 2.7f;
        const float StreetRoad1 = 9.7f;
        /// <summary>向こうの歩道の手前と奥の縁。奥の縁に前庭の低い塀が立つ</summary>
        const float StreetPave0 = 9.9f;
        const float StreetPave1 = 13.4f;
        /// <summary>家の表の壁。前庭の塀から 1.4 m 奥（<see cref="MidTerrace"/> の塀の位置）</summary>
        const float StreetFront = 14.8f;
        /// <summary>通りを伸ばす端。四隅の先まで</summary>
        const float StreetReach = 36f;

        /// <summary>門の外の歩道の縁石の線。門を出た人はここで止まる（<see cref="ParkFences"/>）</summary>
        const float ParkKerbZ = GateZ + StreetKerb;                                        // 11.9

        /// <summary>門の正面の横丁の車道の西と東、歩道の外の縁</summary>
        const float ParkLaneWest = -2.35f;
        const float ParkLaneEast = 2.65f;
        const float ParkLanePaveWest = ParkLaneWest - 2.0f;
        const float ParkLanePaveEast = ParkLaneEast + 2.0f;
        /// <summary>横丁の突き当たり。ここを東西に抜ける通りの向こうに、横丁を塞ぐ家並みが立つ</summary>
        const float ParkLaneEnd = 53.2f;

        /// <summary>
        /// 通りの一辺の向き。<paramref name="alongX"/> が真なら通りは x に沿い、柵の線は z = <paramref name="line"/>、
        /// 外は <paramref name="outward"/> の向き（+1 か -1）。偽なら z に沿い、柵の線は x = line
        /// </summary>
        struct StreetSide
        {
            public bool alongX;
            public float line;
            public float outward;

            /// <summary>通りに沿う u と、柵の線から外への v で、平らな面を一枚</summary>
            public void Flat(Bank b, float y, float u0, float u1, float v0, float v1)
            {
                var a = line + outward * v0;
                var c = line + outward * v1;
                if (alongX) b.FaceY(y, u0, u1, Mathf.Min(a, c), Mathf.Max(a, c), 1);
                else b.FaceY(y, Mathf.Min(a, c), Mathf.Max(a, c), u0, u1, 1);
            }

            /// <summary>通りに沿う細長い箱。v は箱の真ん中、d は外への厚み</summary>
            public void Strip(Bank b, float u0, float u1, float v, float d, float y, float h)
            {
                var c = line + outward * v;
                var mid = (u0 + u1) * 0.5f;
                if (alongX) b.Box(new Vector3(mid, y, c), new Vector3(u1 - u0, h, d));
                else b.Box(new Vector3(c, y, mid), new Vector3(d, h, u1 - u0));
            }

            /// <summary>u と v から場所のローカルの点</summary>
            public Vector3 At(float u, float v)
            {
                var c = line + outward * v;
                return alongX ? new Vector3(u, 0f, c) : new Vector3(c, 0f, u);
            }
        }

        // ---- 組む ----------------------------------------------------------------

        /// <summary>中の層をまとめて組む。公園と同じ入れ物に溜め、出すのは呼ぶ側</summary>
        static void ParkMid(YardBanks y)
        {
            var north = new StreetSide { alongX = true, line = GateZ, outward = 1f };
            var south = new StreetSide { alongX = true, line = ParkSouth, outward = -1f };
            var east = new StreetSide { alongX = false, line = ParkEast, outward = 1f };
            var west = new StreetSide { alongX = false, line = ParkWest, outward = -1f };
            // 東西の通りの車道は、南北の通りの車道の縁まで。歩道と路面の線は南北の通りの縁石まで
            var roadN = GateZ + StreetRoad1;
            var roadS = ParkSouth - StreetRoad1;
            var kerbN = GateZ + StreetKerb;
            var kerbS = ParkSouth - StreetKerb;
            // 東西の通りが南北の通りへ口を開ける所（南北の通りの公園の側の歩道を切る）
            var mouthE = new Vector2(ParkEast + StreetRoad0 - 0.1f, ParkEast + StreetRoad1 + 0.1f);
            var mouthW = new Vector2(ParkWest - StreetRoad1 - 0.1f, ParkWest - StreetRoad0 + 0.1f);
            var none = new Vector2[0];

            // 門の側。向こうの歩道は横丁の口で切る
            ParkStreet(y, north, new Vector2(-StreetReach, StreetReach), new Vector2(-StreetReach, StreetReach),
                new[] { mouthW, mouthE }, new[] { new Vector2(ParkLanePaveWest, ParkLanePaveEast) });
            ParkStreet(y, south, new Vector2(-StreetReach, StreetReach), new Vector2(-StreetReach, StreetReach),
                new[] { mouthW, mouthE }, none);
            ParkStreet(y, east, new Vector2(roadS, roadN), new Vector2(kerbS, kerbN), none, none);
            ParkStreet(y, west, new Vector2(roadS, roadN), new Vector2(kerbS, kerbN), none, none);

            ParkLane(y);
            ParkHouseRows(y);
            ParkStreetThings(y, north, south, east, west);
        }

        /// <summary>
        /// 通りの一辺。公園の側の歩道・縁石・車道・向こうの縁石・向こうの歩道と、路面の線。
        /// <paramref name="road"/> は車道を敷く u の範囲、<paramref name="side"/> は歩道と路面の線の u の範囲。
        /// <paramref name="nearGaps"/> は公園の側の歩道と縁石を切る所（横の通りの口）、
        /// <paramref name="farGaps"/> は向こうの歩道と縁石を切る所（横丁の口）
        /// </summary>
        static void ParkStreet(YardBanks y, StreetSide s, Vector2 road, Vector2 side, Vector2[] nearGaps, Vector2[] farGaps)
        {
            foreach (var r in ParkSplit(side.x, side.y, nearGaps))
            {
                s.Flat(y.Kerb, 0.012f, r.x, r.y, 0f, StreetKerb);
                s.Strip(y.Kerb, r.x, r.y, StreetKerb, 0.2f, 0.06f, 0.12f);
                // 公園の脇には停めさせない。縁石沿いの黄色い二重線
                s.Flat(y.Yellow, 0.02f, r.x, r.y, StreetRoad0 + 0.20f, StreetRoad0 + 0.28f);
                s.Flat(y.Yellow, 0.02f, r.x, r.y, StreetRoad0 + 0.40f, StreetRoad0 + 0.48f);
            }
            // 車道。真ん中に白い破線。4 m 引いて 5 m 空ける
            s.Flat(y.Tarmac, 0.012f, road.x, road.y, StreetRoad0 - 0.1f, StreetRoad1 + 0.1f);
            var midV = (StreetRoad0 + StreetRoad1) * 0.5f;
            for (var u = side.x + 0.5f; u < side.y; u += 9f)
                s.Flat(y.Line, 0.02f, u, Mathf.Min(u + 4f, side.y), midV - 0.05f, midV + 0.05f);
            // 向こうの歩道と縁石。停める所の白い線
            foreach (var r in ParkSplit(side.x, side.y, farGaps))
            {
                s.Flat(y.Kerb, 0.012f, r.x, r.y, StreetPave0 - 0.1f, StreetPave1);
                s.Strip(y.Kerb, r.x, r.y, StreetRoad1 + 0.1f, 0.2f, 0.06f, 0.12f);
                s.Flat(y.Line, 0.02f, r.x + 1f, r.y - 1f, StreetRoad1 - 2.1f, StreetRoad1 - 2.0f);
            }
        }

        /// <summary>u0〜u1 から gaps を抜いた残りの区間</summary>
        static System.Collections.Generic.List<Vector2> ParkSplit(float u0, float u1, Vector2[] gaps)
        {
            var all = new System.Collections.Generic.List<Vector2> { new Vector2(u0, u1) };
            foreach (var g in gaps)
            {
                var next = new System.Collections.Generic.List<Vector2>();
                foreach (var r in all)
                {
                    if (g.y <= r.x || g.x >= r.y) { next.Add(r); continue; }
                    if (g.x > r.x) next.Add(new Vector2(r.x, g.x));
                    if (g.y < r.y) next.Add(new Vector2(g.y, r.y));
                }
                all = next;
            }
            return all;
        }

        /// <summary>
        /// 門の正面の横丁。車道・両側の歩道と縁石・口の譲れの線と黄色い二重線・東の縁石沿いに停めた車。
        /// 突き当たりで東西の通りに当たる
        /// </summary>
        static void ParkLane(YardBanks y)
        {
            var z0 = GateZ + StreetRoad1;
            y.Tarmac.FaceY(0.013f, ParkLaneWest, ParkLaneEast, z0, ParkLaneEnd + 3.4f, 1);
            y.Kerb.FaceY(0.012f, ParkLanePaveWest, ParkLaneWest - 0.1f, z0 + 0.2f, ParkLaneEnd, 1);
            y.Kerb.FaceY(0.012f, ParkLaneEast + 0.1f, ParkLanePaveEast, z0 + 0.2f, ParkLaneEnd, 1);
            y.Kerb.Box(new Vector3(ParkLaneWest - 0.1f, 0.06f, (z0 + ParkLaneEnd) * 0.5f + 0.1f), new Vector3(0.2f, 0.12f, ParkLaneEnd - z0 - 0.2f));
            y.Kerb.Box(new Vector3(ParkLaneEast + 0.1f, 0.06f, (z0 + ParkLaneEnd) * 0.5f + 0.1f), new Vector3(0.2f, 0.12f, ParkLaneEnd - z0 - 0.2f));
            // 口の譲れの線。二本の白い破線
            for (var x = ParkLaneWest + 0.1f; x < ParkLaneEast - 0.2f; x += 0.9f)
            {
                y.Line.FaceY(0.022f, x, x + 0.6f, z0 + 0.35f, z0 + 0.5f, 1);
                y.Line.FaceY(0.022f, x, x + 0.6f, z0 + 0.75f, z0 + 0.9f, 1);
            }
            YardDoubleYellowZ(y, ParkLaneWest + 0.25f, z0 + 1f, z0 + 12f);
            YardDoubleYellowZ(y, ParkLaneEast - 0.25f - 0.28f, z0 + 1f, z0 + 12f);
            // 突き当たりを東西に抜ける通り
            y.Tarmac.FaceY(0.012f, -14f, 16f, ParkLaneEnd, ParkLaneEnd + 3.4f, 1);
            y.Kerb.FaceY(0.012f, -14f, 16f, ParkLaneEnd + 3.4f, ParkLaneEnd + 4.6f, 1);
            y.Kerb.Box(new Vector3(1f, 0.06f, ParkLaneEnd + 3.5f), new Vector3(30f, 0.12f, 0.2f));
            // 停めた車
            ParkCar(y, y.Line, new Vector3(ParkLaneEast - 1.0f, 0f, z0 + 16f), 0f);
            ParkCar(y, y.Red, new Vector3(ParkLaneEast - 1.0f, 0f, z0 + 21.5f), 180f);
            ParkCar(y, y.Iron, new Vector3(ParkLaneEast - 1.0f, 0f, z0 + 30f), 0f);
        }

        /// <summary>
        /// テラスハウスの列。四辺の向こうの歩道の奥と、門の正面の横丁の両側と突き当たり。
        /// 二階建てと三階建てを辺ごとに振り分け、屋根の線の高さに段を付ける。
        /// 四辺の列は角で通りへ口を開ける。門の側の通りは両端を家並みで塞ぎ、ほかの三辺の角の先は書き割りの屋根の海へ抜けて見える。
        ///
        /// **窓に灯りは入れない**（<see cref="MidTerrace"/> の lamps）。公営住宅の朝七時の作りのままだと、
        /// 午後 3 時台なのに窓がいくつか灯っていて、夕方に見えた
        /// </summary>
        static void ParkHouseRows(YardBanks y)
        {
            var nz = GateZ + StreetFront;                                                  // 24.1
            // 門の側。横丁の西と東
            MidTerrace(y, new Vector3(ParkLanePaveWest - 1.4f - HouseWide * 5f, 0f, nz), 180f, 5, 2, 61, false);
            MidTerrace(y, new Vector3(ParkLanePaveEast + 1.4f, 0f, nz), 180f, 5, 3, 67, false);
            // 横丁の両側。門の側の列の裏から突き当たりまで
            MidTerrace(y, new Vector3(ParkLanePaveWest - 1.4f, 0f, nz + 9.2f), 90f, 4, 2, 71, false);
            MidTerrace(y, new Vector3(ParkLanePaveEast + 1.4f, 0f, ParkLaneEnd), 270f, 4, 2, 73, false);
            // 横丁の突き当たり。東西の通りの向こうで、横丁を塞ぐ
            MidTerrace(y, new Vector3(-11.3f, 0f, ParkLaneEnd + 4.6f + 1.4f), 180f, 5, 3, 101, false);
            // 門の側の通りの西と東の突き当たり。塞がないと、門の脇から通りの先の何も無い地面が霞の奥まで見通せた
            MidTerrace(y, new Vector3(-StreetReach - 1.5f, 0f, GateZ - 1.7f), 90f, 3, 2, 103, false);
            MidTerrace(y, new Vector3(StreetReach + 1.5f, 0f, GateZ - 1.7f + HouseWide * 3f), 270f, 3, 2, 107, false);
            // 東と西。東西の通りの間だけ
            MidTerrace(y, new Vector3(ParkEast + StreetFront, 0f, GateZ + 1.8f), 270f, 5, 3, 79, false);
            MidTerrace(y, new Vector3(ParkWest - StreetFront, 0f, GateZ + 1.8f - HouseWide * 5f), 90f, 5, 2, 83, false);
            // 南。三階建てと二階建て
            var sz = ParkSouth - StreetFront;                                              // -26.8
            MidTerrace(y, new Vector3(28f, 0f, sz), 0f, 5, 3, 89, false);
            MidTerrace(y, new Vector3(3f, 0f, sz), 0f, 6, 2, 97, false);
        }

        /// <summary>
        /// 通りの物。赤い郵便ポスト、バス停、背の高い今どきの街灯、街路樹、停めた車。
        /// どれも公園の柵の外に置き、門の前の歩道（歩いて出られる所）は開けておく
        /// </summary>
        static void ParkStreetThings(YardBanks y, StreetSide north, StreetSide south, StreetSide east, StreetSide west)
        {
            // 赤い郵便ポスト。門の西、公園の側の歩道の縁石寄り
            YardPillarBox(y, north.At(-4.8f, 1.9f));
            // バス停。門の側の向こうの歩道、横丁の西。屋根の背を前庭の塀へ向ける
            ParkBusStop(y, north, -13.6f, -10.0f);

            // 街灯。向こうの歩道の縁石寄り、腕は車道へ
            foreach (var u in new[] { -21f, 12.5f, 30f })
                MidLamp(y, north.At(u, StreetRoad1 + 0.55f), Vector3.back);
            foreach (var u in new[] { -24f, 4f, 24f })
                MidLamp(y, south.At(u, StreetRoad1 + 0.55f), Vector3.forward);
            MidLamp(y, east.At(-6f, StreetRoad1 + 0.55f), Vector3.left);
            MidLamp(y, west.At(3f, StreetRoad1 + 0.55f), Vector3.right);

            // 街路樹。向こうの歩道に、葉の無いプラタナス
            YardPlane(y, north.At(-25.5f, StreetRoad1 + 1.3f), 12f, 59);
            YardPlane(y, north.At(19.5f, StreetRoad1 + 1.3f), 12.5f, 61);
            YardPlane(y, south.At(-12f, StreetRoad1 + 1.3f), 11.5f, 67);
            YardPlane(y, south.At(15f, StreetRoad1 + 1.3f), 12f, 71);
            YardPlane(y, east.At(4f, StreetRoad1 + 1.3f), 11f, 73);
            YardPlane(y, west.At(-7f, StreetRoad1 + 1.3f), 12f, 79);

            // 停めた車。向こうの縁石沿い。色を分けるのは、同じ色が並ぶと一つの塊に見えるため
            var park = StreetRoad1 - 1.0f;
            var paints = new[] { y.Red, y.Pole, y.Line, y.Iron, y.Leaf };
            var k = 0;
            foreach (var u in new[] { -30f, -24.5f, -19f, 7.5f, 13f, 24f })
                ParkCar(y, paints[k++ % paints.Length], north.At(u, park), k % 2 == 0 ? 90f : 270f);
            foreach (var u in new[] { -20f, -9f, 8f, 19.5f })
                ParkCar(y, paints[k++ % paints.Length], south.At(u, park), k % 2 == 0 ? 90f : 270f);
            foreach (var u in new[] { 6f, -1f, -9f })
                ParkCar(y, paints[k++ % paints.Length], east.At(u, park), k % 2 == 0 ? 0f : 180f);
            foreach (var u in new[] { 4f, -8f })
                ParkCar(y, paints[k++ % paints.Length], west.At(u, park), k % 2 == 0 ? 0f : 180f);
        }

        /// <summary>
        /// 車一台。<see cref="YardCar"/> と同じ形を、向き <paramref name="yaw"/>（+z が 0、東回り）に回して置く。
        /// 通りに沿って縦列に停めるので、向きを持たせる
        /// </summary>
        static void ParkCar(YardBanks y, Bank paint, Vector3 at, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (a, b, c) => at + rot * new Vector3(a, b, c);
            paint.Box(p(0f, 0.55f, 0f), new Vector3(1.72f, 0.62f, 4.05f), rot);
            paint.Box(p(0f, 1.10f, -0.25f), new Vector3(1.56f, 0.56f, 2.50f), rot);
            y.Glass.Box(p(0f, 1.14f, -0.25f), new Vector3(1.60f, 0.36f, 2.54f), rot);
            // 前の窓は斜めに寝かせる。立てると箱に見える
            y.Glass.Box(p(0f, 1.06f, 1.10f), new Vector3(1.50f, 0.05f, 0.62f), rot * Quaternion.Euler(-32f, 0f, 0f));
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                    y.Iron.Box(p((i * 2 - 1) * 0.78f, 0.30f, (j * 2 - 1) * 1.30f), new Vector3(0.22f, 0.60f, 0.60f), rot);
            y.Line.Box(p(0f, 0.72f, 2.03f), new Vector3(1.40f, 0.12f, 0.02f), rot);
            y.Red.Box(p(0f, 0.78f, -2.03f), new Vector3(1.40f, 0.12f, 0.02f), rot);
        }

        /// <summary>
        /// バス停。向こうの歩道に、背を前庭の塀へ向け、車道の側を開けた屋根。
        /// 公営住宅の敷地の前のバス停（<see cref="YardStreet"/>）と同じ形を、通りの向きに合わせて置く。
        /// 縁石の際に標柱、車道にバス停の前の黄色い囲み
        /// </summary>
        static void ParkBusStop(YardBanks y, StreetSide s, float u0, float u1)
        {
            const float roof = 2.45f;
            var back = StreetPave1 - 0.35f;
            var front = StreetPave1 - 1.75f;
            foreach (var u in new[] { u0, u1 })
                foreach (var v in new[] { back, front })
                    y.Pole.Box(s.At(u, v) + Vector3.up * (roof * 0.5f), new Vector3(0.08f, roof, 0.08f));
            s.Strip(y.Pole, u0 - 0.15f, u1 + 0.15f, (back + front) * 0.5f, back - front + 0.35f, roof + 0.05f, 0.10f);
            s.Strip(y.Glass, u0, u1, back, 0.03f, 1.25f, 1.9f);
            // 西の端の袖の硝子と、東の端の広告の板
            var side = s.At(u0, (back + front) * 0.5f + 0.15f);
            y.Glass.Box(side + Vector3.up * 1.25f, new Vector3(0.03f, 1.9f, back - front - 0.3f));
            var board = s.At(u1, (back + front) * 0.5f + 0.1f);
            y.Line.Box(board + Vector3.up * 1.30f, new Vector3(0.12f, 1.8f, 1.2f));
            s.Strip(y.Iron, (u0 + u1) * 0.5f - 1.1f, (u0 + u1) * 0.5f + 0.5f, back - 0.25f, 0.30f, 0.48f, 0.05f);
            // 標柱。上に白い札と赤い帯
            var flag = s.At(u0 - 1.0f, StreetPave0 + 0.3f);
            y.Pole.Box(flag + Vector3.up * 1.55f, new Vector3(0.08f, 3.1f, 0.08f));
            y.Line.Box(flag + Vector3.up * 2.85f, new Vector3(0.46f, 0.46f, 0.05f));
            y.Red.Box(flag + Vector3.up * 2.85f, new Vector3(0.50f, 0.08f, 0.06f));
            // 車道の黄色い囲み
            s.Flat(y.Yellow, 0.02f, u0 - 2f, u1 + 2f, StreetRoad1 - 2.55f, StreetRoad1 - 2.45f);
            s.Flat(y.Yellow, 0.02f, u0 - 2.05f, u0 - 1.95f, StreetRoad1 - 2.5f, StreetRoad1);
            s.Flat(y.Yellow, 0.02f, u1 + 1.95f, u1 + 2.05f, StreetRoad1 - 2.5f, StreetRoad1);
        }
    }
}
