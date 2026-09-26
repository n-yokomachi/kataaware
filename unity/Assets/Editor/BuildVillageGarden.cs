using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 片割れの家の裏庭（設計書 2 節の 5）。幅 14 m・奥行き 20 m ほど。家の側から奥へ、
    /// テラス → 芝 → 花の縁 → 煉瓦の小路とアーチ → 奥の角の東屋、と並べ、東の端に菜園と温室と物置を寄せる。
    ///
    /// **一目で分かる形を三つ大きく置く**（下調べ 4 節「見せ方の手がかり」）:
    /// **白いパラソルと黒い鉄の卓と椅子**、**弧を描く煉瓦の小路とバラのアーチ**、**奥の角の白い東屋**。
    /// 温室・物置・菜園・物干し・鳥の水盤は、この三つが立ってから隙間を埋める物として置く。
    ///
    /// **豪邸にしない**（設計書 1 節）。整形式の花壇・彫像・噴水・長い眺めの軸は置かない。
    /// 芝は真ん中の一枚だけで、小路は一本の弧。物干しと堆肥箱と巻いたホースで、暮らしている庭に見せる。
    ///
    /// 花と葉の札はここでは置かない（<c>BuildVillagePlants.cs</c>）。ここで決めた花の縁の線
    /// （<see cref="WestBorderFront"/> など）を、札の側も当たりの側も読む
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- 裏庭の寸法 -----------------------------------------------------------------

        /// <summary>
        /// 煉瓦の小路の芯。前庭の石垣の口から格子戸を抜け、テラスの西を通り、ゆるく S を描いて奥の東屋へ入る。
        /// 間を <see cref="PathSamples"/> で滑らかに補う
        /// </summary>
        static readonly Vector2[] PathLine =
        {
            new Vector2(SidePathX, NorthEdge), new Vector2(SidePathX, GateZ), new Vector2(SidePathX, HouseRear),
            new Vector2(-4.25f, 18.6f), new Vector2(-4.00f, 21.0f), new Vector2(-3.50f, 23.2f), new Vector2(-3.25f, 25.3f),
            new Vector2(-3.30f, 27.5f), new Vector2(-3.70f, 29.5f), new Vector2(-4.40f, 31.1f), new Vector2(-5.10f, 32.0f),
        };

        /// <summary>テラスの広がり。裏口の前の敷石。西の縁は小路の東の縁</summary>
        const float TerraceWest = SidePathX + PathWide * 0.5f;
        const float TerraceNorth = 18.6f;
        const float TerraceTop = 0.1f;
        /// <summary>テラスから芝へ下りる段の西と東</summary>
        const float StepWest = 0.3f;
        const float StepEast = 1.7f;

        /// <summary>白いパラソルの卓。テラスの西の端、家の西の角より西に置き、路地から家 B との間越しに傘が覗くようにする</summary>
        public static readonly Vector3 TableAt = new Vector3(-1.8f, TerraceTop, 16.9f);

        /// <summary>芝。西の縁は小路との花の縁、東の縁は東の花の縁</summary>
        const float LawnWest = -2.0f;
        const float LawnEast = 3.6f;
        const float LawnSouth = 19.4f;
        const float LawnNorth = 30.2f;
        /// <summary>芝の奥の、リンゴの木の立つ入り込み</summary>
        const float AlcoveEast = 1.4f;

        /// <summary>東の花の縁の奥の縁。その先が菜園</summary>
        const float EastBorderBack = 4.65f;

        /// <summary>アーチの立つ所（小路の上）</summary>
        const float ArchZ = 25.3f;

        /// <summary>東屋。奥の西の角。四方が開き、北に腰掛け</summary>
        public static readonly Vector3 GazeboAt = new Vector3(-5.3f, 0f, 33.45f);
        const float GazeboHalf = 1.35f;

        /// <summary>温室と物置と堆肥箱。奥の東</summary>
        const float GlassWest = 1.6f, GlassEast = 4.2f, GlassSouth = 31.4f, GlassNorth = 34.6f;
        const float ShedWest = 4.6f, ShedEast = 6.9f, ShedSouth = 30.8f, ShedNorth = 34.6f;

        /// <summary>菜園の畝。木枠の二つ</summary>
        static readonly Rect[] VegBeds = { new Rect(4.95f, 19.8f, 1.75f, 3.2f), new Rect(4.95f, 23.6f, 1.75f, 3.2f) };

        /// <summary>リンゴの木。芝の奥の入り込みに一本</summary>
        public static readonly Vector3 AppleAt = new Vector3(-0.3f, 0f, 32.4f);
        /// <summary>鳥の水盤と物干し</summary>
        static readonly Vector3 BathAt = new Vector3(-1.35f, 0f, 22.6f);
        static readonly Vector3 AirerAt = new Vector3(2.75f, 0f, 28.7f);

        // ---- 組み立て -------------------------------------------------------------------

        static void Garden(Transform parent, Banks b)
        {
            Paths(b);
            Terrace(b);
            Parasol(b, TableAt);
            Boundaries(b);
            Arch(b);
            Gazebo(b);
            Greenhouse(b);
            Shed(b);
            Veg(b);
            Oddments(b);
            AppleTrunk(b);
            Espalier(b);
            GardenBounds(Child(parent, "Bounds"));
        }

        // ---- 小路とテラス ---------------------------------------------------------------

        /// <summary>小路の芯を 0.35 m ごとに補った点。Catmull-Rom で角を丸める</summary>
        static List<Vector2> PathSamples()
        {
            var dense = new List<Vector2>();
            for (var i = 0; i + 1 < PathLine.Length; i++)
            {
                var p0 = PathLine[Mathf.Max(0, i - 1)];
                var p1 = PathLine[i];
                var p2 = PathLine[i + 1];
                var p3 = PathLine[Mathf.Min(PathLine.Length - 1, i + 2)];
                var n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.35f));
                for (var k = 0; k < n; k++)
                {
                    var t = k / (float)n;
                    var t2 = t * t;
                    var t3 = t2 * t;
                    dense.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            dense.Add(PathLine[PathLine.Length - 1]);
            return dense;
        }

        /// <summary>補った点の控え。地面の絵を描くとき画素ごとに引くので、一度だけ作る</summary>
        static List<Vector2> pathCache;

        /// <summary>小路の芯の x を z から引く。z は小路に沿って増えるだけなので、z で引ける</summary>
        static float PathX(float z)
        {
            if (pathCache == null) pathCache = PathSamples();
            var s = pathCache;
            if (z <= s[0].y) return s[0].x;
            for (var i = 0; i + 1 < s.Count; i++)
                if (z <= s[i + 1].y)
                    return Mathf.Lerp(s[i].x, s[i + 1].x, Mathf.InverseLerp(s[i].y, s[i + 1].y, z));
            return s[s.Count - 1].x;
        }

        /// <summary>
        /// 煉瓦の小路。芯に沿って帯を張り、煉瓦の段が小路を横切る向きに uv を振る。
        /// 両縁に煉瓦を立てて並べた縁取り
        /// </summary>
        static void Paths(Banks b)
        {
            var s = PathSamples();
            var run = 0f;
            const float h = 0.03f;
            for (var i = 0; i + 1 < s.Count; i++)
            {
                var a = s[i];
                var c = s[i + 1];
                var na = Across(s, i);
                var nc = Across(s, i + 1);
                var step = Vector2.Distance(a, c);
                var la = a - na * (PathWide * 0.5f);
                var ra = a + na * (PathWide * 0.5f);
                var lc = c - nc * (PathWide * 0.5f);
                var rc = c + nc * (PathWide * 0.5f);
                // 煉瓦の段が小路を横切るよう、u を幅、v を芯に沿った長さで振る。
                // 左の縁 → 先の左 → 先の右 → 右の縁の順が上を向く（Bank の表の向き）
                b.Brick.Patch(new Vector3(la.x, h, la.y), new Vector3(lc.x, h, lc.y), new Vector3(rc.x, h, rc.y), new Vector3(ra.x, h, ra.y),
                    new Vector2(0f, run), new Vector2(0f, run + step), new Vector2(PathWide, run + step), new Vector2(PathWide, run));
                // 縁取りの煉瓦。テラスに接する所は立てない
                var mid = (a + c) * 0.5f;
                var inTerrace = mid.y > HouseRear && mid.y < TerraceNorth;
                Beam(b.Brick, new Vector3(la.x, 0.05f, la.y), new Vector3(lc.x, 0.05f, lc.y), 0.10f, 0.08f);
                if (!inTerrace) Beam(b.Brick, new Vector3(ra.x, 0.05f, ra.y), new Vector3(rc.x, 0.05f, rc.y), 0.10f, 0.08f);
                run += step;
            }
            // 玄関の小路は前庭で張ってある（FrontGarden）
        }

        /// <summary>小路の i 番目の点で、進む向きの右を指す単位の向き（上から見て）</summary>
        static Vector2 Across(List<Vector2> s, int i)
        {
            var a = s[Mathf.Max(0, i - 1)];
            var c = s[Mathf.Min(s.Count - 1, i + 1)];
            var d = (c - a).normalized;
            return new Vector2(d.y, -d.x);
        }

        /// <summary>
        /// テラス。裏口の前の敷石を地面から 10 cm 上げ、縁に石の見切り。芝へ下りる所に一段
        /// </summary>
        static void Terrace(Banks b)
        {
            b.Flag.FaceY(TerraceTop, TerraceWest, HouseEast, HouseRear, TerraceNorth, 1);
            b.Dressed.FaceZ(TerraceNorth, TerraceWest, HouseEast, 0f, TerraceTop, 1);
            b.Dressed.FaceX(TerraceWest, HouseRear, TerraceNorth, 0f, TerraceTop, -1);
            b.Dressed.Box(new Vector3((TerraceWest + HouseEast) * 0.5f, TerraceTop + 0.02f, TerraceNorth - 0.07f), new Vector3(HouseEast - TerraceWest, 0.04f, 0.14f));
            b.Flag.Box(new Vector3((StepWest + StepEast) * 0.5f, 0.05f, TerraceNorth + 0.2f), new Vector3(StepEast - StepWest, 0.1f, 0.4f));
        }

        /// <summary>
        /// 白いパラソルと黒い鉄の卓と椅子三脚。傘は八角の円錐に、縁の垂れ。
        /// 下調べ 4 節の一つ目: 白い円錐と、その下の黒い鉄の脚の対比が遠目にも立つ
        /// </summary>
        static void Parasol(Banks b, Vector3 at)
        {
            const int sides = 8;
            const float r = 1.30f;
            const float rim = 2.12f;
            const float apex = 2.62f;
            var top = at + Vector3.up * apex;
            for (var i = 0; i < sides; i++)
            {
                var a0 = Mathf.PI * 2f * i / sides;
                var a1 = Mathf.PI * 2f * (i + 1) / sides;
                var p0 = at + new Vector3(Mathf.Cos(a0) * r, rim, Mathf.Sin(a0) * r);
                var p1 = at + new Vector3(Mathf.Cos(a1) * r, rim, Mathf.Sin(a1) * r);
                var mid = (a0 + a1) * 0.5f;
                var outward = new Vector3(Mathf.Cos(mid), 1.4f, Mathf.Sin(mid));
                Face(b.Paint, p0, p1, top, top, outward);
                Face(b.Paint, p0, p1, top, top, -outward);
                // 縁の垂れ
                var d = Vector3.down * 0.14f;
                Face(b.Paint, p0 + d, p1 + d, p1, p0, new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid)));
                Face(b.Paint, p0 + d, p1 + d, p1, p0, -new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid)));
                // 骨
                b.Paint.Box((p0 + top) * 0.5f + Vector3.down * 0.03f, new Vector3(0.02f, 0.02f, Vector3.Distance(p0, top)),
                    Quaternion.LookRotation(top - p0, Vector3.up));
            }
            b.Paint.Box(at + Vector3.up * (apex + 0.08f), new Vector3(0.06f, 0.16f, 0.06f));
            b.Paint.Box(at + Vector3.up * (apex * 0.5f), new Vector3(0.04f, apex, 0.04f));

            // 卓。**白く塗った鉄**（設計書 7 節）。丸い天板を八角で、三本の脚は外へ反らせる
            Prism(b.Paint, at + Vector3.up * 0.70f, 0.46f, 0.03f, 10);
            Prism(b.Paint, at + Vector3.up * 0.62f, 0.10f, 0.08f, 6);
            for (var i = 0; i < 3; i++)
            {
                var a = Mathf.PI * 2f * i / 3f + 0.3f;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Beam(b.Paint, at + Vector3.up * 0.64f + o * 0.06f, at + Vector3.up * 0.30f + o * 0.22f, 0.03f, 0.03f);
                Beam(b.Paint, at + Vector3.up * 0.30f + o * 0.22f, at + o * 0.34f, 0.03f, 0.03f);
            }
            // 椅子。卓を囲んで三脚、卓へ向ける。白
            foreach (var deg in new[] { 200f, 320f, 80f })
            {
                var a = deg * Mathf.Deg2Rad;
                var foot = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.82f;
                Chair(b.Paint, foot, Quaternion.LookRotation(at - foot, Vector3.up));
            }
        }

        /// <summary>
        /// 白い鉄のビストロの椅子。丸い座面、四本の脚、背。rot の +z が前（卓の側）。
        ///
        /// **背は後ろの二本の脚をそのまま上へ伸ばして作る。** 背の柱を座面の外に別に立てたら、
        /// 背もたれが座面から離れて宙に浮いて見えた（オーナーの指摘）。
        /// 後ろの脚は床から座面の縁を通って背の頭まで一本で通し、座面の縁の輪と背の横木で繋ぐ
        /// </summary>
        static void Chair(Bank b, Vector3 foot, Quaternion rot)
        {
            const float seat = 0.45f;
            const float r = 0.21f;
            Prism(b, foot + Vector3.up * (seat - 0.03f), r, 0.03f, 8);
            var back = rot * Vector3.back;
            var side = rot * Vector3.right;
            var fwd = -back;
            // 前の二本の脚。座面の縁の下から床へ少し開いて
            foreach (var s in new[] { -1f, 1f })
            {
                var rim = foot + (fwd * 0.7f + side * s * 0.7f).normalized * (r - 0.03f) + Vector3.up * (seat - 0.03f);
                Beam(b, rim, foot + (fwd * 0.7f + side * s * 0.7f).normalized * (r + 0.02f), 0.024f, 0.024f);
            }
            // 後ろの二本。床から座面の縁を通り、背の頭まで一本で
            var tops = new Vector3[2];
            var k = 0;
            foreach (var s in new[] { -1f, 1f })
            {
                var dir = (back * 0.75f + side * s * 0.66f).normalized;
                var floor = foot + dir * (r + 0.03f);
                var rim = foot + dir * (r - 0.02f) + Vector3.up * (seat - 0.02f);
                var top = foot + dir * (r - 0.01f) + back * 0.07f + Vector3.up * 0.93f;
                Beam(b, floor, rim, 0.024f, 0.024f);
                Beam(b, rim, top, 0.024f, 0.024f);
                tops[k++] = top;
            }
            // 背の頭の横木（ゆるい弧を二つの桟で）と、中ほどの横木、真ん中の縦の飾り
            var crown = (tops[0] + tops[1]) * 0.5f + back * 0.03f + Vector3.up * 0.03f;
            Beam(b, tops[0], crown, 0.03f, 0.03f);
            Beam(b, crown, tops[1], 0.03f, 0.03f);
            var mid0 = Vector3.Lerp(tops[0], foot + (back * 0.75f - side * 0.66f).normalized * (r - 0.02f) + Vector3.up * seat, 0.5f);
            var mid1 = Vector3.Lerp(tops[1], foot + (back * 0.75f + side * 0.66f).normalized * (r - 0.02f) + Vector3.up * seat, 0.5f);
            Beam(b, mid0, mid1, 0.02f, 0.02f);
            Beam(b, foot + back * (r - 0.02f) + Vector3.up * seat, crown, 0.02f, 0.02f);
            // 脚の下の輪（脚どうしを繋ぐ桟）
            Prism(b, foot + Vector3.up * 0.16f, r * 0.8f, 0.015f, 8);
        }

        // ---- 境界 -----------------------------------------------------------------------

        /// <summary>
        /// 境。西（家 B の庭との境）は板の塀、東は野石の塀、奥（北）は生け垣。その向こうに麦畑。
        /// 塀と生け垣は目の高さより少し上で止め、奥の麦畑の丘の頭が見えるようにする
        /// </summary>
        static void Boundaries(Banks b)
        {
            // 西の板の塀。柱を 2.4 m ごとに、下に地覆の板、頭に笠木
            const float fence = 1.8f;
            b.Boards.Box(new Vector3(PlotWest - 0.03f, fence * 0.5f, (GateZ + PlotNorth) * 0.5f), new Vector3(0.05f, fence, PlotNorth - GateZ));
            b.Boards.Box(new Vector3(PlotWest - 0.03f, fence + 0.03f, (GateZ + PlotNorth) * 0.5f), new Vector3(0.12f, 0.05f, PlotNorth - GateZ));
            var posts = Mathf.RoundToInt((PlotNorth - GateZ) / 2.4f);
            for (var i = 0; i <= posts; i++)
            {
                var z = Mathf.Lerp(GateZ + 0.2f, PlotNorth, i / (float)posts);
                b.Boards.Box(new Vector3(PlotWest + 0.03f, (fence + 0.05f) * 0.5f, z), new Vector3(0.10f, fence + 0.05f, 0.10f));
            }
            b.Boards.Box(new Vector3(PlotWest + 0.01f, 0.08f, (GateZ + PlotNorth) * 0.5f), new Vector3(0.05f, 0.16f, PlotNorth - GateZ));

            // 東の野石の塀。頭に縦の笠石
            const float wall = 1.7f;
            const float thick = 0.4f;
            var ex = PlotEast + thick * 0.5f;
            b.Stone.Box(new Vector3(ex, wall * 0.5f, (GateZ + PlotNorth + 1f) * 0.5f), new Vector3(thick, wall, PlotNorth + 1f - GateZ));
            CockAndHen(b, new Vector3(ex, wall, GateZ), new Vector3(ex, wall, PlotNorth + 1f), thick - 0.04f);

            // 奥の生け垣。西の塀から東の塀まで
            Hedge(b, new Vector3(PlotWest - 0.3f, 0f, PlotNorth + 0.5f), new Vector3(PlotEast, 0f, PlotNorth + 0.5f), 1.9f, 1.0f, 11);
        }

        // ---- アーチ ---------------------------------------------------------------------

        /// <summary>
        /// 小路に掛けるアーチ。白く塗った細い柱を片側二本ずつ、頭は半円の弧を前後二本、その間に桟。
        /// 横は格子。バラとクレマチスは札の側（BuildVillagePlants.ArchPlants）が這わせる
        /// </summary>
        static void Arch(Banks b)
        {
            ArchFrame(b.Paint, out _, out _);
        }

        /// <summary>アーチの芯・横の向き・進む向きを返しつつ骨を組む</summary>
        static void ArchFrame(Bank b, out Vector3 centre, out Vector3 across)
        {
            centre = new Vector3(PathX(ArchZ), 0f, ArchZ);
            var ahead3 = new Vector3(PathX(ArchZ + 0.3f), 0f, ArchZ + 0.3f) - new Vector3(PathX(ArchZ - 0.3f), 0f, ArchZ - 0.3f);
            ahead3.Normalize();
            across = Vector3.Cross(Vector3.up, ahead3).normalized;
            const float half = 0.78f;
            const float legs = 2.1f;
            const float depth = 0.26f;
            for (var f = -1; f <= 1; f += 2)
            {
                var fo = ahead3 * (depth * f);
                for (var s = -1; s <= 1; s += 2)
                {
                    var foot = centre + across * (half * s) + fo;
                    b.Box(foot + Vector3.up * (legs * 0.5f), new Vector3(0.06f, legs, 0.06f), Quaternion.LookRotation(ahead3));
                }
                // 頭の弧
                const int seg = 7;
                for (var k = 0; k < seg; k++)
                {
                    var a0 = Mathf.PI * k / seg;
                    var a1 = Mathf.PI * (k + 1) / seg;
                    var p0 = centre + fo + across * (Mathf.Cos(a0) * half) + Vector3.up * (legs + Mathf.Sin(a0) * half * 0.9f);
                    var p1 = centre + fo + across * (Mathf.Cos(a1) * half) + Vector3.up * (legs + Mathf.Sin(a1) * half * 0.9f);
                    Beam(b, p0, p1, 0.05f, 0.05f);
                }
            }
            // 前後の弧を繋ぐ桟
            for (var k = 0; k <= 6; k++)
            {
                var a = Mathf.PI * k / 6f;
                var p = centre + across * (Mathf.Cos(a) * half) + Vector3.up * (legs + Mathf.Sin(a) * half * 0.9f);
                Beam(b, p - ahead3 * depth, p + ahead3 * depth, 0.035f, 0.035f);
            }
            // 横の格子
            for (var s = -1; s <= 1; s += 2)
            {
                var foot = centre + across * (half * s) - ahead3 * depth;
                Lattice(b, foot + Vector3.up * 0.3f, foot + ahead3 * (depth * 2f) + Vector3.up * 0.3f, legs - 0.4f, 0.17f);
            }
        }

        // ---- 東屋 -----------------------------------------------------------------------

        /// <summary>
        /// 東屋。四隅の白い柱に、方形の石版の屋根と頭の飾り。床は板張りで一段上げる。
        /// 西と北と東の下半分に格子の腰、上の隅に飾りの方杖。北の内に腰掛け。南は小路から入る口
        /// </summary>
        static void Gazebo(Banks b)
        {
            var c = GazeboAt;
            const float h = GazeboHalf;
            const float eaves = 2.35f;
            const float peak = 3.55f;
            const float over = 0.28f;
            b.Boards.Box(c + Vector3.up * 0.07f, new Vector3(h * 2f + 0.1f, 0.14f, h * 2f + 0.1f));
            var corners = new[] { new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h), new Vector3(h, 0f, h), new Vector3(-h, 0f, h) };
            foreach (var k in corners)
            {
                b.Paint.Box(c + k * 0.96f + Vector3.up * (eaves * 0.5f), new Vector3(0.12f, eaves, 0.12f));
                b.Paint.Box(c + k * 0.96f + Vector3.up * 0.2f, new Vector3(0.18f, 0.12f, 0.18f));
            }
            // 軒の桁
            for (var i = 0; i < 4; i++)
            {
                var p = c + corners[i] * 0.96f + Vector3.up * (eaves - 0.08f);
                var q = c + corners[(i + 1) % 4] * 0.96f + Vector3.up * (eaves - 0.08f);
                Beam(b.Paint, p, q, 0.10f, 0.16f);
                // 方杖。上の隅から斜めに
                var dir = (q - p).normalized;
                Beam(b.Paint, p + dir * 0.06f + Vector3.down * 0.45f, p + dir * 0.5f, 0.05f, 0.05f);
                Beam(b.Paint, q - dir * 0.06f + Vector3.down * 0.45f, q - dir * 0.5f, 0.05f, 0.05f);
            }
            // 屋根。方形の四枚。裏は白く塗った天井
            var apex = c + Vector3.up * peak;
            for (var i = 0; i < 4; i++)
            {
                var p = c + corners[i] * ((h + over) / h) + Vector3.up * (eaves - 0.02f);
                var q = c + corners[(i + 1) % 4] * ((h + over) / h) + Vector3.up * (eaves - 0.02f);
                var mid = (p + q) * 0.5f - c;
                mid.y = 0f;
                var outward = mid.normalized + Vector3.up * 1.2f;
                Face(b.Slate, p, q, apex, apex, outward);
                Face(b.Paint, p, q, apex + Vector3.down * 0.1f, apex + Vector3.down * 0.1f, -outward);
                Face(b.Paint, p + Vector3.down * 0.1f, q + Vector3.down * 0.1f, q, p, mid.normalized);
            }
            b.Paint.Box(apex + Vector3.up * 0.1f, new Vector3(0.14f, 0.22f, 0.14f));
            b.Paint.Box(apex + Vector3.up * 0.3f, new Vector3(0.1f, 0.1f, 0.1f), Quaternion.Euler(45f, 0f, 45f));
            // 腰の格子と手すり。西・北・東
            var sides = new[] { new[] { 3, 0 }, new[] { 2, 3 }, new[] { 1, 2 } };
            foreach (var sd in sides)
            {
                var p = c + corners[sd[0]] * 0.96f;
                var q = c + corners[sd[1]] * 0.96f;
                Beam(b.Paint, p + Vector3.up * 0.92f, q + Vector3.up * 0.92f, 0.08f, 0.06f);
                Lattice(b.Paint, p + Vector3.up * 0.16f + (q - p).normalized * 0.07f, q + Vector3.up * 0.16f - (q - p).normalized * 0.07f, 0.72f, 0.16f);
            }
            // 北の腰掛け
            var bz = c.z + h - 0.35f;
            b.Boards.Box(new Vector3(c.x, 0.46f, bz), new Vector3(h * 1.7f, 0.05f, 0.42f));
            b.Boards.Box(new Vector3(c.x, 0.30f, bz + 0.18f), new Vector3(h * 1.7f, 0.30f, 0.04f));
            foreach (var dx in new[] { -h * 0.75f, 0f, h * 0.75f })
                b.Boards.Box(new Vector3(c.x + dx, 0.23f, bz), new Vector3(0.06f, 0.46f, 0.36f));
            // 腰掛けの上のクッション。暮らしの跡
            b.Cloth.Box(new Vector3(c.x - 0.3f, 0.52f, bz - 0.02f), new Vector3(0.5f, 0.07f, 0.36f));
        }

        // ---- 温室と物置 -----------------------------------------------------------------

        /// <summary>
        /// ヴィクトリア風の小さな温室。煉瓦の腰壁に白い枠とガラス、棟に飾りの鉄の縁と両端の尖り。
        /// 南の妻に戸。中に棚と鉢とトマト（札）
        /// </summary>
        static void Greenhouse(Banks b)
        {
            const float dwarf = 0.6f;
            const float eaves = 1.95f;
            const float ridge = 2.8f;
            var x0 = GlassWest; var x1 = GlassEast; var z0 = GlassSouth; var z1 = GlassNorth;
            var xm = (x0 + x1) * 0.5f;
            const float doorHalf = 0.38f;
            // 腰壁
            b.Brick.Box(new Vector3(x0 + 0.1f, dwarf * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.2f, dwarf, z1 - z0));
            b.Brick.Box(new Vector3(x1 - 0.1f, dwarf * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.2f, dwarf, z1 - z0));
            b.Brick.Box(new Vector3(xm, dwarf * 0.5f, z1 - 0.1f), new Vector3(x1 - x0, dwarf, 0.2f));
            b.Brick.Box(new Vector3((x0 + xm - doorHalf) * 0.5f, dwarf * 0.5f, z0 + 0.1f), new Vector3(xm - doorHalf - x0, dwarf, 0.2f));
            b.Brick.Box(new Vector3((x1 + xm + doorHalf) * 0.5f, dwarf * 0.5f, z0 + 0.1f), new Vector3(x1 - xm - doorHalf, dwarf, 0.2f));
            b.Dressed.Box(new Vector3(x0 + 0.1f, dwarf + 0.03f, (z0 + z1) * 0.5f), new Vector3(0.26f, 0.06f, z1 - z0 + 0.06f));
            b.Dressed.Box(new Vector3(x1 - 0.1f, dwarf + 0.03f, (z0 + z1) * 0.5f), new Vector3(0.26f, 0.06f, z1 - z0 + 0.06f));
            // ガラス。側の二面、北の妻、南の妻（戸の上と脇）、屋根の二面
            var g = b.Glass;
            g.FaceX(x0 + 0.1f, z0 + 0.1f, z1 - 0.1f, dwarf + 0.06f, eaves, -1);
            g.FaceX(x1 - 0.1f, z0 + 0.1f, z1 - 0.1f, dwarf + 0.06f, eaves, 1);
            g.FaceZ(z1 - 0.1f, x0 + 0.1f, x1 - 0.1f, dwarf + 0.06f, eaves, 1);
            Face(g, new Vector3(x0 + 0.1f, eaves, z1 - 0.1f), new Vector3(x1 - 0.1f, eaves, z1 - 0.1f), new Vector3(xm, ridge, z1 - 0.1f), new Vector3(xm, ridge, z1 - 0.1f), Vector3.forward);
            g.FaceZ(z0 + 0.1f, x0 + 0.1f, xm - doorHalf, dwarf + 0.06f, eaves, -1);
            g.FaceZ(z0 + 0.1f, xm + doorHalf, x1 - 0.1f, dwarf + 0.06f, eaves, -1);
            Face(g, new Vector3(x0 + 0.1f, eaves, z0 + 0.1f), new Vector3(x1 - 0.1f, eaves, z0 + 0.1f), new Vector3(xm, ridge, z0 + 0.1f), new Vector3(xm, ridge, z0 + 0.1f), Vector3.back);
            // 戸（ガラスの板を少し開けて）
            Face(g, new Vector3(xm - doorHalf, 0.05f, z0 + 0.1f), new Vector3(xm + doorHalf, 0.05f, z0 + 0.1f), new Vector3(xm + doorHalf, eaves - 0.05f, z0 + 0.1f), new Vector3(xm - doorHalf, eaves - 0.05f, z0 + 0.1f), Vector3.back);
            foreach (var side in new[] { -1f, 1f })
            {
                var xe = side < 0f ? x0 + 0.02f : x1 - 0.02f;
                Face(g, new Vector3(xe, eaves, z0 + 0.02f), new Vector3(xe, eaves, z1 - 0.02f), new Vector3(xm, ridge, z1 - 0.02f), new Vector3(xm, ridge, z0 + 0.02f),
                    new Vector3(side, 1.2f, 0f));
            }
            // 白い枠。隅の柱、腰と軒の桁、縦の桟、屋根の垂木、棟
            var p = b.Paint;
            foreach (var x in new[] { x0 + 0.1f, x1 - 0.1f })
                foreach (var z in new[] { z0 + 0.1f, z1 - 0.1f })
                    p.Box(new Vector3(x, (dwarf + eaves) * 0.5f, z), new Vector3(0.07f, eaves - dwarf, 0.07f));
            foreach (var x in new[] { x0 + 0.1f, x1 - 0.1f })
            {
                p.Box(new Vector3(x, eaves, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.07f, z1 - z0));
                var bars = Mathf.RoundToInt((z1 - z0) / 0.55f);
                for (var i = 1; i < bars; i++)
                {
                    var z = Mathf.Lerp(z0 + 0.1f, z1 - 0.1f, i / (float)bars);
                    p.Box(new Vector3(x, (dwarf + eaves) * 0.5f, z), new Vector3(0.04f, eaves - dwarf, 0.04f));
                    Beam(p, new Vector3(x, eaves, z), new Vector3(xm, ridge, z), 0.04f, 0.04f);
                }
                Beam(p, new Vector3(x, eaves, z0 + 0.1f), new Vector3(xm, ridge, z0 + 0.1f), 0.06f, 0.06f);
                Beam(p, new Vector3(x, eaves, z1 - 0.1f), new Vector3(xm, ridge, z1 - 0.1f), 0.06f, 0.06f);
            }
            foreach (var z in new[] { z0 + 0.1f, z1 - 0.1f })
            {
                p.Box(new Vector3(xm, eaves, z), new Vector3(x1 - x0 - 0.2f, 0.06f, 0.06f));
                for (var i = 1; i < 4; i++)
                {
                    var x = Mathf.Lerp(x0 + 0.1f, x1 - 0.1f, i / 4f);
                    if (z < z0 + 0.2f && Mathf.Abs(x - xm) < doorHalf + 0.02f) continue;
                    p.Box(new Vector3(x, (dwarf + eaves) * 0.5f, z), new Vector3(0.04f, eaves - dwarf, 0.04f));
                }
            }
            p.Box(new Vector3(xm - doorHalf, (0.05f + eaves) * 0.5f, z0 + 0.08f), new Vector3(0.06f, eaves, 0.06f));
            p.Box(new Vector3(xm + doorHalf, (0.05f + eaves) * 0.5f, z0 + 0.08f), new Vector3(0.06f, eaves, 0.06f));
            p.Box(new Vector3(xm, ridge + 0.03f, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.08f, z1 - z0));
            // 棟の飾りの縁と、両端の尖り
            var crest = Mathf.RoundToInt((z1 - z0) / 0.16f);
            for (var i = 0; i <= crest; i++)
            {
                var z = Mathf.Lerp(z0 + 0.05f, z1 - 0.05f, i / (float)crest);
                b.Paint.Box(new Vector3(xm, ridge + 0.13f, z), new Vector3(0.02f, 0.12f, 0.02f));
            }
            b.Paint.Box(new Vector3(xm, ridge + 0.19f, (z0 + z1) * 0.5f), new Vector3(0.02f, 0.02f, z1 - z0));
            foreach (var z in new[] { z0 + 0.05f, z1 - 0.05f })
            {
                b.Paint.Box(new Vector3(xm, ridge + 0.22f, z), new Vector3(0.05f, 0.40f, 0.05f));
                b.Paint.Box(new Vector3(xm, ridge + 0.38f, z), new Vector3(0.08f, 0.08f, 0.08f), Quaternion.Euler(45f, 0f, 45f));
            }
            // 中の棚と鉢
            foreach (var x in new[] { x0 + 0.5f, x1 - 0.5f })
            {
                b.Boards.Box(new Vector3(x, 0.80f, (z0 + z1) * 0.5f + 0.1f), new Vector3(0.62f, 0.04f, z1 - z0 - 0.6f));
                for (var i = 0; i < 4; i++)
                    Prism(b.Clay, new Vector3(x + (i % 2 == 0 ? -0.12f : 0.12f), 0.82f, z0 + 0.7f + i * 0.55f), 0.09f, 0.13f, 6, 0.11f);
            }
            GreenhousePots.Clear();
            foreach (var x in new[] { x0 + 0.5f, x1 - 0.5f })
                for (var i = 0; i < 4; i++)
                    GreenhousePots.Add(new Vector3(x + (i % 2 == 0 ? -0.12f : 0.12f), 0.95f, z0 + 0.7f + i * 0.55f));
        }

        /// <summary>温室の棚の鉢の頭。トマトの札を載せる</summary>
        static readonly List<Vector3> GreenhousePots = new List<Vector3>();

        /// <summary>
        /// 物置。縦板張りの切妻に、黒いフェルトの屋根。南の妻に戸、西に窓。
        /// 窓の奥の作業台に鉢と如雨露が見える
        /// </summary>
        static void Shed(Banks b)
        {
            const float eaves = 1.95f;
            const float ridge = 2.55f;
            var x0 = ShedWest; var x1 = ShedEast; var z0 = ShedSouth; var z1 = ShedNorth;
            var xm = (x0 + x1) * 0.5f;
            var wz0 = 32.1f; var wz1 = 33.4f; const float wy0 = 1.0f; const float wy1 = 1.6f;
            b.Boards.FaceXHoles(x0, z0, z1, 0f, eaves, -1, new List<Vector4> { new Vector4(wz0, wz1, wy0, wy1) });
            b.Boards.FaceX(x1, z0, z1, 0f, eaves, 1);
            b.Boards.FaceZ(z1, x0, x1, 0f, eaves, 1);
            b.Boards.FaceZHoles(z0, x0, x1, 0f, eaves, -1, new List<Vector4> { new Vector4(xm - 0.4f, xm + 0.4f, 0f, 1.8f) });
            foreach (var z in new[] { z0, z1 })
            {
                var s = z < 32f ? -1f : 1f;
                Face(b.Boards, new Vector3(x0, eaves, z), new Vector3(x1, eaves, z), new Vector3(xm, ridge, z), new Vector3(xm, ridge, z), Vector3.forward * s);
            }
            // 屋根。軒を出す
            foreach (var side in new[] { -1f, 1f })
            {
                var xe = side < 0f ? x0 - 0.15f : x1 + 0.15f;
                var ye = eaves - 0.15f * (ridge - eaves) / (xm - x0);
                Face(b.Iron, new Vector3(xe, ye, z0 - 0.15f), new Vector3(xe, ye, z1 + 0.15f), new Vector3(xm, ridge + 0.02f, z1 + 0.15f), new Vector3(xm, ridge + 0.02f, z0 - 0.15f),
                    new Vector3(side, 2f, 0f));
                Face(b.Iron, new Vector3(xe, ye - 0.04f, z0 - 0.15f), new Vector3(xe, ye - 0.04f, z1 + 0.15f), new Vector3(xm, ridge - 0.02f, z1 + 0.15f), new Vector3(xm, ridge - 0.02f, z0 - 0.15f),
                    new Vector3(-side, -2f, 0f));
            }
            // 隅の見切りと破風の板
            foreach (var x in new[] { x0, x1 })
                foreach (var z in new[] { z0, z1 })
                    b.Boards.Box(new Vector3(x, eaves * 0.5f, z), new Vector3(0.08f, eaves, 0.08f));
            // 戸
            b.Door.Box(new Vector3(xm, 0.9f, z0 + 0.03f), new Vector3(0.78f, 1.8f, 0.04f));
            b.Door.Box(new Vector3(xm, 0.3f, z0 - 0.01f), new Vector3(0.78f, 0.08f, 0.03f));
            b.Door.Box(new Vector3(xm, 1.5f, z0 - 0.01f), new Vector3(0.78f, 0.08f, 0.03f));
            b.Door.Box(new Vector3(xm, 0.9f, z0 - 0.01f), new Vector3(0.9f, 0.07f, 0.03f), Quaternion.Euler(0f, 0f, 52f));
            b.Iron.Box(new Vector3(xm + 0.3f, 1.0f, z0 - 0.04f), new Vector3(0.03f, 0.12f, 0.03f));
            // 窓。奥に暗がり、その手前に作業台と鉢
            const float deep = 0.7f;
            Face(b.Dark, new Vector3(x0 + deep, wy0, wz0), new Vector3(x0 + deep, wy0, wz1), new Vector3(x0 + deep, wy1, wz1), new Vector3(x0 + deep, wy1, wz0), Vector3.left);
            Face(b.Boards, new Vector3(x0, wy0, wz0), new Vector3(x0, wy1, wz0), new Vector3(x0 + deep, wy1, wz0), new Vector3(x0 + deep, wy0, wz0), Vector3.forward);
            Face(b.Boards, new Vector3(x0, wy0, wz1), new Vector3(x0, wy1, wz1), new Vector3(x0 + deep, wy1, wz1), new Vector3(x0 + deep, wy0, wz1), Vector3.back);
            Face(b.Dark, new Vector3(x0, wy1, wz0), new Vector3(x0, wy1, wz1), new Vector3(x0 + deep, wy1, wz1), new Vector3(x0 + deep, wy1, wz0), Vector3.down);
            b.Boards.Box(new Vector3(x0 + deep * 0.5f, wy0 - 0.02f, (wz0 + wz1) * 0.5f), new Vector3(deep, 0.04f, wz1 - wz0));
            for (var i = 0; i < 3; i++)
                Prism(b.Clay, new Vector3(x0 + 0.35f, wy0, wz0 + 0.3f + i * 0.35f), 0.07f, 0.11f, 6, 0.09f);
            b.Paint.Box(new Vector3(x0 - 0.02f, (wy0 + wy1) * 0.5f, (wz0 + wz1) * 0.5f), new Vector3(0.04f, wy1 - wy0, 0.04f));
            b.Paint.Box(new Vector3(x0 - 0.02f, wy1 + 0.03f, (wz0 + wz1) * 0.5f), new Vector3(0.06f, 0.06f, wz1 - wz0 + 0.12f));
            b.Paint.Box(new Vector3(x0 - 0.02f, wy0 - 0.03f, (wz0 + wz1) * 0.5f), new Vector3(0.08f, 0.06f, wz1 - wz0 + 0.12f));
        }

        // ---- 菜園と細々した物 -----------------------------------------------------------

        /// <summary>木枠の畝二つ。土と、キャベツの玉の並び。スイートピーとレタスは札の側</summary>
        static void Veg(Banks b)
        {
            foreach (var r in VegBeds)
            {
                const float h = 0.28f;
                b.Bark.Box(new Vector3(r.xMin + 0.03f, h * 0.5f, r.center.y), new Vector3(0.06f, h, r.height));
                b.Bark.Box(new Vector3(r.xMax - 0.03f, h * 0.5f, r.center.y), new Vector3(0.06f, h, r.height));
                b.Bark.Box(new Vector3(r.center.x, h * 0.5f, r.yMin + 0.03f), new Vector3(r.width, h, 0.06f));
                b.Bark.Box(new Vector3(r.center.x, h * 0.5f, r.yMax - 0.03f), new Vector3(r.width, h, 0.06f));
                b.Soil.FaceY(h - 0.04f, r.xMin + 0.06f, r.xMax - 0.06f, r.yMin + 0.06f, r.yMax - 0.06f, 1);
            }
            // 手前の畝にキャベツ
            var bed = VegBeds[0];
            for (var i = 0; i < 5; i++)
                for (var j = 0; j < 2; j++)
                {
                    var at = new Vector3(bed.xMin + 0.45f + j * 0.8f, 0.24f, bed.yMin + 0.4f + i * 0.6f);
                    Prism(b.Hedge, at, 0.17f, 0.12f, 6, 0.21f);
                    Prism(b.Hedge, at + Vector3.up * 0.12f, 0.21f, 0.10f, 6, 0.08f);
                }
            // 奥の畝の東の縁に、スイートピーの支柱の列（竹を立てて上で結ぶ）
            var far = VegBeds[1];
            for (var i = 0; i <= 6; i++)
            {
                var z = Mathf.Lerp(far.yMin + 0.2f, far.yMax - 0.2f, i / 6f);
                Beam(b.Bark, new Vector3(far.xMax - 0.35f, 0.2f, z), new Vector3(far.xMax - 0.22f, 2.0f, z), 0.02f, 0.02f);
                Beam(b.Bark, new Vector3(far.xMax - 0.10f, 0.2f, z), new Vector3(far.xMax - 0.22f, 2.0f, z), 0.02f, 0.02f);
            }
            Beam(b.Bark, new Vector3(far.xMax - 0.22f, 1.95f, far.yMin + 0.1f), new Vector3(far.xMax - 0.22f, 1.95f, far.yMax - 0.1f), 0.025f, 0.025f);
        }

        /// <summary>堆肥箱・雨水の樽・巻いたホース・鳥の水盤・物干し・テラスの鉢</summary>
        static void Oddments(Banks b)
        {
            // 堆肥箱。板を隙間を空けて積んだ二つの升。前は開けて中の堆肥を見せる
            const float cx0 = 5.35f, cx1 = 6.9f, cz0 = 27.9f, cz1 = 29.1f;
            for (var i = 0; i < 6; i++)
            {
                var y = 0.08f + i * 0.14f;
                // 色だけの板にする。縦板の絵を横長の細い板に貼ると、縦の目地が並んで煉瓦に見えた
                b.Bark.Box(new Vector3((cx0 + cx1) * 0.5f, y, cz1), new Vector3(cx1 - cx0, 0.1f, 0.03f));
                foreach (var x in new[] { cx0, (cx0 + cx1) * 0.5f, cx1 })
                    b.Bark.Box(new Vector3(x, y, (cz0 + cz1) * 0.5f), new Vector3(0.03f, 0.1f, cz1 - cz0));
            }
            b.Soil.Box(new Vector3((cx0 * 3f + cx1) * 0.25f, 0.28f, (cz0 + cz1) * 0.5f + 0.05f), new Vector3((cx1 - cx0) * 0.5f - 0.08f, 0.56f, cz1 - cz0 - 0.2f));
            b.Soil.Box(new Vector3((cx0 + cx1 * 3f) * 0.25f, 0.18f, (cz0 + cz1) * 0.5f + 0.05f), new Vector3((cx1 - cx0) * 0.5f - 0.08f, 0.36f, cz1 - cz0 - 0.2f));

            // 雨水の樽。家の裏の東の隅、竪樋の下。オークの樽に鉄の箍
            var butt = new Vector3(HouseEast - 0.40f, TerraceTop, HouseRear + 0.50f);
            Prism(b.Boards, butt, 0.34f, 0.92f, 10, 0.32f);
            b.Water.FanY(butt + Vector3.up * 0.90f, Ring(butt, 0.30f, 10));
            foreach (var y in new[] { 0.18f, 0.74f })
                Prism(b.Iron, butt + Vector3.up * y, 0.355f, 0.04f, 10);

            // 巻いたホース。裏の壁の掛け金に
            var reel = new Vector3(5.25f, 1.0f, HouseRear + 0.12f);
            b.Iron.Box(reel, new Vector3(0.08f, 0.08f, 0.16f));
            for (var k = 0; k < 3; k++)
            {
                var r = 0.20f - k * 0.02f;
                const int seg = 10;
                for (var i = 0; i < seg; i++)
                {
                    var a0 = Mathf.PI * 2f * i / seg;
                    var a1 = Mathf.PI * 2f * (i + 1) / seg;
                    var p0 = reel + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r - 0.14f, 0.05f + k * 0.035f);
                    var p1 = reel + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r - 0.14f, 0.05f + k * 0.035f);
                    Beam(b.Door, p0, p1, 0.035f, 0.035f);
                }
            }
            // ホースの端を樽のほうへ垂らす
            Beam(b.Door, reel + new Vector3(0.18f, -0.28f, 0.1f), new Vector3(5.6f, TerraceTop + 0.02f, HouseRear + 0.5f), 0.035f, 0.035f);
            Beam(b.Door, new Vector3(5.6f, TerraceTop + 0.02f, HouseRear + 0.5f), new Vector3(5.3f, TerraceTop + 0.02f, HouseRear + 1.1f), 0.035f, 0.035f);

            // 鳥の水盤。石の台に浅い鉢
            Prism(b.Dressed, BathAt, 0.22f, 0.08f, 8);
            Prism(b.Dressed, BathAt + Vector3.up * 0.08f, 0.11f, 0.62f, 8, 0.09f);
            Prism(b.Dressed, BathAt + Vector3.up * 0.70f, 0.18f, 0.08f, 10, 0.34f);
            b.Water.FanY(BathAt + Vector3.up * 0.785f, Ring(BathAt, 0.29f, 10));

            // 物干し。回る傘の骨に綱を張り、シーツとタオルを掛ける
            Airer(b, AirerAt);

            // テラスの鉢。ペラルゴニウムの札は BuildVillagePlants.Pots が載せる
            TerracePots.Clear();
            foreach (var at in new[]
            {
                new Vector3(TerraceWest + 0.3f, TerraceTop, HouseRear + 0.35f), new Vector3(BackDoorX - 0.75f, TerraceTop, HouseRear + 0.3f),
                new Vector3(BackDoorX + 0.72f, TerraceTop, HouseRear + 0.3f), new Vector3(HouseEast - 0.35f, TerraceTop, TerraceNorth - 0.4f),
                new Vector3(StepWest - 0.3f, TerraceTop, TerraceNorth - 0.35f), new Vector3(StepEast + 0.3f, TerraceTop, TerraceNorth - 0.35f),
            })
            {
                Prism(b.Clay, at, 0.17f, 0.30f, 8, 0.22f);
                Prism(b.Clay, at + Vector3.up * 0.28f, 0.235f, 0.05f, 8);
                b.Soil.FanY(at + Vector3.up * 0.31f, Ring(at, 0.20f, 8));
                TerracePots.Add(at + Vector3.up * 0.3f);
            }
        }

        /// <summary>テラスの鉢の土の高さの中心</summary>
        static readonly List<Vector3> TerracePots = new List<Vector3>();

        static Vector2[] Ring(Vector3 c, float r, int n)
        {
            var rim = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var a = Mathf.PI * 2f * i / n;
                rim[i] = new Vector2(c.x + Mathf.Cos(a) * r, c.z + Mathf.Sin(a) * r);
            }
            return rim;
        }

        /// <summary>回転式の物干し。細い柱に四本の腕、腕の間に三周の綱、洗濯物を三枚</summary>
        static void Airer(Banks b, Vector3 at)
        {
            const float pole = 1.75f;
            const float reach = 1.25f;
            b.Dressed.Box(at + Vector3.up * 0.9f, new Vector3(0.04f, 1.8f, 0.04f));
            var tips = new Vector3[4];
            for (var i = 0; i < 4; i++)
            {
                var a = Mathf.PI * 0.5f * i + 0.35f;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                tips[i] = at + o * reach + Vector3.up * (pole + 0.18f);
                Beam(b.Dressed, at + Vector3.up * (pole + 0.05f), tips[i], 0.025f, 0.025f);
                Beam(b.Dressed, at + Vector3.up * 1.35f, at + o * 0.55f + Vector3.up * (pole + 0.10f), 0.018f, 0.018f);
            }
            for (var k = 1; k <= 3; k++)
            {
                var f = k / 3f;
                for (var i = 0; i < 4; i++)
                {
                    var p = Vector3.Lerp(at + Vector3.up * (pole + 0.05f), tips[i], f);
                    var q = Vector3.Lerp(at + Vector3.up * (pole + 0.05f), tips[(i + 1) % 4], f);
                    Beam(b.Paint, p, q, 0.008f, 0.008f);
                }
            }
            // 洗濯物。外の綱にシーツ、中の綱にタオル二枚
            Laundry(b.Paint, Vector3.Lerp(at + Vector3.up * (pole + 0.05f), tips[0], 0.95f), Vector3.Lerp(at + Vector3.up * (pole + 0.05f), tips[1], 0.95f), 0.9f, 0.1f);
            Laundry(b.Cloth, Vector3.Lerp(at + Vector3.up * (pole + 0.05f), tips[2], 0.62f), Vector3.Lerp(at + Vector3.up * (pole + 0.05f), tips[3], 0.62f), 0.55f, 0.25f);
            Laundry(b.Door, Vector3.Lerp(at + Vector3.up * (pole + 0.05f), tips[1], 0.62f), Vector3.Lerp(at + Vector3.up * (pole + 0.05f), tips[2], 0.62f), 0.45f, 0.35f);
        }

        /// <summary>綱の a から b の間に、丈 drop の布を一枚。縁を片寄せて掛ける</summary>
        static void Laundry(Bank b, Vector3 a, Vector3 c, float drop, float inset)
        {
            var p = Vector3.Lerp(a, c, inset);
            var q = Vector3.Lerp(a, c, 1f - inset);
            var n = Vector3.Cross(q - p, Vector3.up);
            Face(b, p + Vector3.down * drop, q + Vector3.down * drop, q, p, n);
            Face(b, p + Vector3.down * drop, q + Vector3.down * drop, q, p, -n);
        }

        // ---- 果樹 -----------------------------------------------------------------------

        /// <summary>リンゴの木の幹と枝。樹冠の札は BuildVillagePlants.Trees</summary>
        static void AppleTrunk(Banks b)
        {
            var foot = AppleAt;
            Beam(b.Bark, foot, foot + new Vector3(0.05f, 1.35f, 0.02f), 0.20f, 0.18f);
            for (var i = 0; i < 5; i++)
            {
                var a = Mathf.PI * 2f * i / 5f + 0.4f;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var from = foot + new Vector3(0.05f, 1.25f, 0.02f);
                // 枝は樹冠の札の内に収める。外へ出ると、葉の隙間から黒い棒が突き出して見える
                var to = foot + o * (0.55f + Hash(71, i) * 0.3f) + Vector3.up * (1.9f + Hash(73, i) * 0.4f);
                Beam(b.Bark, from, to, 0.07f, 0.06f);
            }
        }

        /// <summary>
        /// 塀に沿わせたエスパリエ。西の板の塀の格子戸の脇に、幹を一本立て、三段の枝を水平に左右へ伸ばす。
        /// 葉と実は札の側（BuildVillagePlants.Trees）
        /// </summary>
        static void Espalier(Banks b)
        {
            var x = PlotWest + 0.14f;
            var zc = EspalierZ;
            Beam(b.Bark, new Vector3(x, 0f, zc), new Vector3(x, 1.6f, zc), 0.08f, 0.08f);
            foreach (var y in EspalierTiers)
                Beam(b.Bark, new Vector3(x, y, zc - EspalierHalf), new Vector3(x, y, zc + EspalierHalf), 0.05f, 0.05f);
            // 支えの針金
            foreach (var y in EspalierTiers)
                Beam(b.Iron, new Vector3(x - 0.06f, y + 0.02f, zc - EspalierHalf - 0.3f), new Vector3(x - 0.06f, y + 0.02f, zc + EspalierHalf + 0.3f), 0.008f, 0.008f);
        }

        const float EspalierZ = 15.4f;
        const float EspalierHalf = 2.2f;
        static readonly float[] EspalierTiers = { 0.55f, 1.0f, 1.45f };

        // ---- 当たり ---------------------------------------------------------------------

        /// <summary>
        /// 裏庭の当たり。塀と生け垣、東屋の腰と腰掛け、温室と物置、菜園と堆肥箱、卓と椅子、樽、水盤、物干しの柱。
        /// 花の縁の当たりは札の側で線を引く（BuildVillagePlants.BorderBounds）
        /// </summary>
        static void GardenBounds(Transform parent)
        {
            Wall(parent, "FenceWest", new Vector3(PlotWest, 0f, GateZ), new Vector3(PlotWest, 0f, PlotNorth + 1f), 2.4f, 0.4f);
            Wall(parent, "WallEast", new Vector3(PlotEast + 0.2f, 0f, GateZ), new Vector3(PlotEast + 0.2f, 0f, PlotNorth + 1f), 2.4f, 0.4f);
            Wall(parent, "HedgeNorth", new Vector3(PlotWest, 0f, PlotNorth + 0.3f), new Vector3(PlotEast, 0f, PlotNorth + 0.3f), 2.4f, 0.6f);
            // 東屋。入口の南を開け、西・北・東の腰を止める
            var c = GazeboAt;
            var h = GazeboHalf * 0.96f;
            Wall(parent, "GazeboW", c + new Vector3(-h, 0f, -h), c + new Vector3(-h, 0f, h), 1.2f, 0.15f);
            Wall(parent, "GazeboN", c + new Vector3(-h, 0f, h), c + new Vector3(h, 0f, h), 1.2f, 0.9f);
            Wall(parent, "GazeboE", c + new Vector3(h, 0f, -h), c + new Vector3(h, 0f, h), 1.2f, 0.15f);
            Block(parent, "GazeboPostSW", c + new Vector3(-h, 1f, -h), new Vector3(0.2f, 2f, 0.2f));
            Block(parent, "GazeboPostSE", c + new Vector3(h, 1f, -h), new Vector3(0.2f, 2f, 0.2f));
            Block(parent, "Greenhouse", new Vector3((GlassWest + GlassEast) * 0.5f, 1.2f, (GlassSouth + GlassNorth) * 0.5f),
                new Vector3(GlassEast - GlassWest, 2.4f, GlassNorth - GlassSouth));
            Block(parent, "Shed", new Vector3((ShedWest + ShedEast) * 0.5f, 1.2f, (ShedSouth + ShedNorth) * 0.5f),
                new Vector3(ShedEast - ShedWest, 2.4f, ShedNorth - ShedSouth));
            for (var i = 0; i < VegBeds.Length; i++)
                Block(parent, "Veg" + i, new Vector3(VegBeds[i].center.x, 0.5f, VegBeds[i].center.y), new Vector3(VegBeds[i].width, 1f, VegBeds[i].height));
            Block(parent, "Compost", new Vector3(6.12f, 0.5f, 28.5f), new Vector3(1.6f, 1f, 1.3f));
            Block(parent, "Table", TableAt + Vector3.up * 0.5f, new Vector3(1.9f, 1f, 1.9f));
            Block(parent, "Butt", new Vector3(HouseEast - 0.40f, 0.6f, HouseRear + 0.50f), new Vector3(0.8f, 1.2f, 0.8f));
            Block(parent, "Bath", BathAt + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 0.6f));
            Block(parent, "Airer", AirerAt + Vector3.up * 1f, new Vector3(0.2f, 2f, 0.2f));
            Block(parent, "Apple", AppleAt + Vector3.up * 1f, new Vector3(0.4f, 2f, 0.4f));
        }
    }
}
