using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室の窓の外の街並みの形（<see cref="BuildRoomView"/>）。倫敦の落ち着いた住宅街。
    ///
    /// <list type="table">
    /// <item><term>通り</term><description>部屋の北の壁の外を東西の通り（A）が、東の壁の外を南北の通り（北は B、南は C）が走り、
    /// 部屋の北東の角の先で交わる。どの通りも車道 6.4 m・歩道 2.4 m・家の前の半地下の明かり取り 1.6 m。
    /// 北は 56 m おき、南は 44 m おきに東西の通り、東西は 56 m おきに南北の通り。北の二本目の東西の通り（F）が川沿いの道で、
    /// その北は川（<c>BuildRoomViewLandmarks.cs</c>）。南北の通りはみな F で終わる</description></item>
    /// <item><term>家並み</term><description>通りに面した三階建ての連続住宅（間口 5 m・奥行き 10 m・軒 10 m・棟 13.1 m）を、
    /// 区画の南北の縁に一列ずつ。家の正面・裏・屋根・妻壁・煙突。近い家（80 m まで）には煙突の壺・柵・玄関の石段、
    /// 150 m までは煙突の胴だけ</description></item>
    /// <item><term>特別な区画</term><description>北東の角のパブ、B の両側の家並み（B を向く）、東の窓の前の広場
    /// （柵と生け垣と木と小径）、広場の東の家並み（広場を向く）、広場の南の古い集合住宅、北西の教会（座って始めた目から北の窓に入る向き）、
    /// 南東の遠い公営住宅の塔。北西の D と F の間の二区画は、名所への見通しを空ける芝の公園</description></item>
    /// <item><term>通りの物</term><description>街灯（柱・灯り・暈・路面の溜まり）、止めてある車、街路樹、横断歩道と灯りの柱、
    /// 赤い電話ボックス、中央の破線と交差点の近くの二重の黄線</description></item>
    /// </list>
    ///
    /// **重さのために面を一つの mesh にまとめる。** ふつうの面（街並みのアトラス）・暈（足し算）・空の三つだけ。
    /// 面の向きの陰りと街灯の照り返しは頂点色に焼く（シェーダーは灯りを受けない）
    /// </summary>
    public static partial class BuildRoomView
    {
        const float G = RoomView.Ground;
        const float KerbHigh = 0.12f;
        const float RoadHalf = 3.2f;
        const float PavementWide = 2.4f;
        const float AreaWide = 1.6f;
        /// <summary>通りの中心から家の正面まで</summary>
        const float FrontOffset = RoadHalf + PavementWide + AreaWide;
        const float HouseHigh = 10f;
        const float HouseDeep = 10f;
        const float RoofRise = 3.1f;
        /// <summary>煙突の壺・柵・石段まで作る遠さと、煙突の胴まで作る遠さ</summary>
        const float NearDetail = 80f;
        const float MidDetail = 150f;
        /// <summary>通りを張る端</summary>
        const float StreetEnd = 240f;

        /// <summary>東西の通りの中心の z。北の半分と、南の半分（南は C より東だけ）</summary>
        static readonly float[] NorthStreets = { 8.8f, 64.8f, 120.8f, 176.8f, 232.8f };
        static readonly float[] SouthStreets = { 8.8f, -35.2f, -79.2f, -123.2f, -167.2f, -211.2f, -255.2f };
        /// <summary>南北の通りの中心の x。8.8 が B・C、64.8 が広場の東の E</summary>
        static readonly float[] CrossStreets = { -271.2f, -215.2f, -159.2f, -103.2f, -47.2f, 8.8f, 64.8f, 120.8f, 176.8f, 232.8f };
        const float StreetA = 8.8f;
        const float StreetB = 8.8f;
        const float StreetE = 64.8f;
        const float StreetJ = -79.2f;
        const float StreetMid = -35.2f;

        /// <summary>広場の柵の内（x は C の東の歩道の縁から E の西の歩道の縁、z は J の北の歩道の縁から A の南の歩道の縁）</summary>
        static readonly Rect Square = Rect.MinMaxRect(StreetB + RoadHalf + PavementWide, StreetJ + RoadHalf + PavementWide,
            StreetE - RoadHalf - PavementWide, StreetA - RoadHalf - PavementWide);

        /// <summary>角のパブの敷地</summary>
        static readonly Rect PubLot = Rect.MinMaxRect(StreetB + FrontOffset, StreetA + FrontOffset, StreetB + FrontOffset + 10f, StreetA + FrontOffset + 10f);

        /// <summary>教会の塔の真ん中。座って始めた目（1.5, 1.31, 1.42）から左へ 60 度（北の窓の抜けの真ん中）、80 m</summary>
        static readonly Vector2 ChurchTowerAt = new Vector2(-68f, 42f);

        /// <summary>日の沈んだ側（面の陰りに使う）</summary>
        static readonly Vector3 Sunward = new Vector3(Mathf.Sin(SunAzimuth * Mathf.Deg2Rad), 0f, Mathf.Cos(SunAzimuth * Mathf.Deg2Rad));

        static readonly Color LampWarm = new Color(1.0f, 0.72f, 0.42f);
        static readonly Color[] CarColours =
        {
            new Color(0.16f, 0.20f, 0.34f), new Color(0.62f, 0.62f, 0.64f), new Color(0.40f, 0.10f, 0.10f),
            new Color(0.82f, 0.82f, 0.80f), new Color(0.10f, 0.10f, 0.11f), new Color(0.18f, 0.30f, 0.22f),
            new Color(0.46f, 0.44f, 0.38f), new Color(0.30f, 0.34f, 0.40f),
        };

        // ---- 面を溜める入れ物 ------------------------------------------------------

        /// <summary>面を溜めて一枚の mesh に焼く。位置・uv・頂点色だけ（法線は持たない。シェーダーが灯りを受けない）</summary>
        sealed class Pile
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector2> u = new List<Vector2>();
            readonly List<Color32> c = new List<Color32>();
            readonly List<int> t = new List<int>();

            public int Tris { get { return t.Count / 3; } }
            public int Verts { get { return v.Count; } }

            /// <summary>四隅で一枚。a→b→c→d で回り、uv は a が左下・b が右下・c が右上・d が左上</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Rect uv, Color ca, Color cb, Color ccol, Color cd)
            {
                var i = v.Count;
                v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
                u.Add(new Vector2(uv.xMin, uv.yMin));
                u.Add(new Vector2(uv.xMax, uv.yMin));
                u.Add(new Vector2(uv.xMax, uv.yMax));
                u.Add(new Vector2(uv.xMin, uv.yMax));
                c.Add(ca); c.Add(cb); c.Add(ccol); c.Add(cd);
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }

            /// <summary>別の入れ物の面を写して足す。位置は map で動かし、頂点色の α（霞の効き）を haze にする</summary>
            public void Append(Pile other, System.Func<Vector3, Vector3> map, byte haze)
            {
                var start = v.Count;
                foreach (var p in other.v) v.Add(map(p));
                u.AddRange(other.u);
                foreach (var col in other.c) c.Add(new Color32(col.r, col.g, col.b, haze));
                foreach (var i in other.t) t.Add(start + i);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 cc, Vector2 ua, Vector2 ub, Vector2 uc, Color ca, Color cb, Color ccol)
            {
                var i = v.Count;
                v.Add(a); v.Add(b); v.Add(cc);
                u.Add(ua); u.Add(ub); u.Add(uc);
                c.Add(ca); c.Add(cb); c.Add(ccol);
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
            }

            public Mesh Bake(string name)
            {
                var mesh = new Mesh();
                mesh.name = name;
                if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetUVs(0, u);
                mesh.SetColors(c);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>組み上がる物一式と、その途中で使う物</summary>
        sealed class Town
        {
            public Pile Solid = new Pile();
            public Pile Glow = new Pile();
            public readonly Pile Sky = new Pile();
            public readonly List<Vector3> Lamps = new List<Vector3>();
            public readonly Random R = new Random(Seed);
            public int Houses;
            public int Trees;
            public int Cars;
            public int Landmarks;
        }

        // ---- 見える所 --------------------------------------------------------------

        /// <summary>窓から見える向きの幅の内で、組む遠さの内か</summary>
        static bool Seen(Vector3 p)
        {
            var flat = new Vector2(p.x, p.z);
            if (flat.magnitude > RoomView.TownReach) return false;
            var az = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
            if (az < RoomView.ArcFrom) az += 360f;
            return az <= RoomView.ArcTo;
        }

        static float Reach(Vector3 p)
        {
            return new Vector2(p.x, p.z).magnitude;
        }

        // ---- 陰り ----------------------------------------------------------------

        /// <summary>
        /// 頂点色。上を向く面ほど空の明るさを受け、日の沈んだ側（西北西）を向く面ほど明るい。
        /// 近くの街灯より低い所は、街灯の側を向くほど暖かく照らされる
        /// </summary>
        static Color Shade(Town town, Vector3 n, Vector3 p)
        {
            var up = Mathf.Clamp01(n.y);
            var side = Mathf.Clamp01(n.x * Sunward.x + n.z * Sunward.z);
            var s = 0.62f + 0.2f * up + 0.16f * side;
            var c = new Color(s, s, s * 1.02f);
            foreach (var lamp in town.Lamps)
            {
                var d = lamp - p;
                var flat = new Vector2(d.x, d.z).magnitude;
                if (flat > 16f) continue;
                var f = 1f - flat / 16f;
                f *= f;
                var toward = flat > 0.01f ? Mathf.Clamp01((n.x * d.x + n.z * d.z) / flat + 0.35f) : 1f;
                if (n.y > 0.5f) toward = 0.6f;
                var below = p.y < lamp.y + 0.5f ? 1f : 0.35f;
                c += LampWarm * (0.38f * f * toward * below);
            }
            return c;
        }

        /// <summary>陰りを付けた四隅の面</summary>
        static void Face(Town town, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uv, Vector3 n)
        {
            town.Solid.Quad(a, b, c, d, uv, Shade(town, n, a), Shade(town, n, b), Shade(town, n, c), Shade(town, n, d));
        }

        static void Face(Town town, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Sw s, Vector3 n)
        {
            Face(town, a, b, c, d, Uv(s), n);
        }

        /// <summary>色を掛けた面（車の塗り）</summary>
        static void Face(Town town, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Sw s, Vector3 n, Color tint)
        {
            town.Solid.Quad(a, b, c, d, Uv(s), Shade(town, n, a) * tint, Shade(town, n, b) * tint, Shade(town, n, c) * tint, Shade(town, n, d) * tint);
        }

        static void Tri(Town town, Vector3 a, Vector3 b, Vector3 c, Rect uv, Vector3 n)
        {
            town.Solid.Tri(a, b, c, new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.center.x, uv.yMax),
                Shade(town, n, a), Shade(town, n, b), Shade(town, n, c));
        }

        /// <summary>
        /// 向きを持つ箱。底は張らない。上の面は top、横の四面は side。tint を掛ける
        /// </summary>
        static void Box(Town town, Vector3 centre, Vector3 size, float yaw, Rect side, Rect top, Color tint)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var h = size * 0.5f;
            System.Func<float, float, float, Vector3> at = (x, y, z) => centre + rot * new Vector3(h.x * x, h.y * y, h.z * z);
            var right = rot * Vector3.right;
            var fwd = rot * Vector3.forward;
            Quad(town, at(-1, -1, 1), at(1, -1, 1), at(1, 1, 1), at(-1, 1, 1), side, fwd, tint);
            Quad(town, at(1, -1, -1), at(-1, -1, -1), at(-1, 1, -1), at(1, 1, -1), side, -fwd, tint);
            Quad(town, at(1, -1, 1), at(1, -1, -1), at(1, 1, -1), at(1, 1, 1), side, right, tint);
            Quad(town, at(-1, -1, -1), at(-1, -1, 1), at(-1, 1, 1), at(-1, 1, -1), side, -right, tint);
            Quad(town, at(-1, 1, 1), at(1, 1, 1), at(1, 1, -1), at(-1, 1, -1), top, Vector3.up, tint);
        }

        static void Box(Town town, Vector3 centre, Vector3 size, float yaw, Sw side, Sw top)
        {
            Box(town, centre, size, yaw, Uv(side), Uv(top), Color.white);
        }

        static void Quad(Town town, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uv, Vector3 n, Color tint)
        {
            town.Solid.Quad(a, b, c, d, uv, Shade(town, n, a) * tint, Shade(town, n, b) * tint, Shade(town, n, c) * tint, Shade(town, n, d) * tint);
        }

        // ---- 組む順 ----------------------------------------------------------------

        /// <summary>街並み・暈・空を組む。街灯を先に決める（家の頂点色が街灯の照り返しを読む）</summary>
        static Town MakeTown()
        {
            var town = new Town();
            PlaceLamps(town);
            Ground(town);
            Streets(town);
            Blocks(town);
            SquareGarden(town);
            Pub(town);
            Church(town);
            // 南東の遠い公営住宅の塔。東の窓の O2 と重ならない南南東に
            TowerBlock(town, new Vector2(92.8f, -145.2f), new Vector2(14f, 30f), 72f, 4);
            StreetTrees(town);
            Cars(town);
            Crossing(town);
            PhoneBox(town, new Vector3(33.5f, G + KerbHigh, StreetA + RoadHalf + PavementWide * 0.55f));
            LampPosts(town);
            FarLamps(town);
            Landmarks(town);
            SkyDome(town);
            return town;
        }

        // ---- 地面と通り --------------------------------------------------------------

        /// <summary>
        /// 地面。庭の土と草の色で一枚。通りと家はこの上に載る。
        /// 車道より 12 cm 下げる（WebGL の深さの細かさでは、200 m 先で 5 cm ほどの差が潰れて車道と揺らぎ合う）
        /// </summary>
        static void Ground(Town town)
        {
            var e = StreetEnd;
            // 北は川沿いの遊歩道の手前まで（川と北岸は River が、遊歩道も River が張る）
            FlatFan(town, SouthOfRiver(Rect.MinMaxRect(-e, -e, e, e)), G - 0.12f, Sw.Garden);
        }

        /// <summary>
        /// 東西の通り z がある x の範囲。川沿いの遊歩道の手前で止め、D より北は公園の西の縁より西だけ
        /// </summary>
        static Vector2 EastWestSpan(float z)
        {
            Vector2 span;
            if (z >= StreetA - 0.01f) span = new Vector2(-StreetEnd, StreetEnd);
            else if (Mathf.Abs(z - StreetMid) < 0.01f) span = new Vector2(StreetE, StreetEnd);
            else span = new Vector2(StreetB, StreetEnd);
            if (z > StreetD + 0.01f) span.y = Mathf.Min(span.y, ParkWestStreet);
            // 川の座標の z が遊歩道の手前 10 m に届く x（川は東ほど手前に来る）
            var a = RoomView.Turn * Mathf.Deg2Rad;
            var wet = (RiverSouth - RiverWalk - 10f - z * Mathf.Cos(a)) / Mathf.Sin(a);
            span.y = Mathf.Min(span.y, wet);
            return span.x < span.y ? span : new Vector2(0f, 0f);
        }

        /// <summary>
        /// 南北の通り x がある z の範囲。西の通りは A より北だけ（南西は自分の建物の陰で見えない）。
        /// 公園の西の縁より東の通りは D で終わり、ほかは川沿いの遊歩道の手前で止める
        /// </summary>
        static Vector2 NorthSouthSpan(float x)
        {
            var span = x < StreetB - 0.01f ? new Vector2(StreetA, 232.8f) : new Vector2(-StreetEnd, 232.8f);
            if (x > ParkWestStreet + 0.01f) span.y = StreetD;
            var a = RoomView.Turn * Mathf.Deg2Rad;
            var wet = (RiverSouth - RiverWalk - 10f - x * Mathf.Sin(a)) / Mathf.Cos(a);
            span.y = Mathf.Min(span.y, wet);
            return span.x < span.y ? span : new Vector2(0f, 0f);
        }

        static void Streets(Town town)
        {
            var eastWest = new List<float>(NorthStreets);
            foreach (var z in SouthStreets) if (!eastWest.Contains(z)) eastWest.Add(z);
            foreach (var z in eastWest)
            {
                var span = EastWestSpan(z);
                if (span.x >= span.y || Mathf.Abs(z) > StreetEnd) continue;
                var cuts = new List<float>();
                foreach (var x in CrossStreets)
                {
                    var ns = NorthSouthSpan(x);
                    if (z >= ns.x - 0.01f && z <= ns.y + 0.01f && x >= span.x - 0.01f && x <= span.y + 0.01f) cuts.Add(x);
                }
                Street(town, true, z, span, cuts);
            }
            foreach (var x in CrossStreets)
            {
                var span = NorthSouthSpan(x);
                if (Mathf.Abs(x) > StreetEnd) continue;
                var cuts = new List<float>();
                foreach (var z in eastWest)
                {
                    var ew = EastWestSpan(z);
                    if (x >= ew.x - 0.01f && x <= ew.y + 0.01f && z >= span.x - 0.01f && z <= span.y + 0.01f) cuts.Add(z);
                }
                Street(town, false, x, span, cuts);
            }
            Markings(town);
        }

        /// <summary>
        /// 通りを一本。車道は切らずに通し（交わる所は重なるが同じ色）、歩道と縁石は交わる通りの車道の所で切る。
        /// 近い通りには縁石の立ち上がりを付ける
        /// </summary>
        static void Street(Town town, bool eastWest, float centre, Vector2 span, List<float> cuts)
        {
            System.Func<float, float, float, Vector3> at = (along, across, y) =>
                eastWest ? new Vector3(along, y, centre + across) : new Vector3(centre + across, y, along);
            var near = Mathf.Abs(centre) < 110f;
            var road = near ? Sw.Road : Sw.RoadFar;
            Face(town, at(span.x, -RoadHalf, G), at(span.y, -RoadHalf, G), at(span.y, RoadHalf, G), at(span.x, RoadHalf, G), road, Vector3.up);
            cuts.Sort();
            var pieces = new List<Vector2>();
            var from = span.x;
            foreach (var c in cuts)
            {
                if (c - RoadHalf > from) pieces.Add(new Vector2(from, c - RoadHalf));
                from = Mathf.Max(from, c + RoadHalf);
            }
            if (span.y > from) pieces.Add(new Vector2(from, span.y));
            var top = G + KerbHigh;
            foreach (var p in pieces)
                foreach (var side in new[] { -1f, 1f })
                {
                    var inner = side * RoadHalf;
                    var outer = side * (RoadHalf + PavementWide);
                    var a = at(p.x, Mathf.Min(inner, outer), top);
                    var b = at(p.y, Mathf.Min(inner, outer), top);
                    var c = at(p.y, Mathf.Max(inner, outer), top);
                    var d = at(p.x, Mathf.Max(inner, outer), top);
                    Face(town, a, b, c, d, Sw.Pave, Vector3.up);
                    if (!near) continue;
                    var kerbNormal = eastWest ? new Vector3(0f, 0f, -side) : new Vector3(-side, 0f, 0f);
                    Face(town, at(p.x, inner, G), at(p.y, inner, G), at(p.y, inner, top), at(p.x, inner, top), Sw.Kerb, kerbNormal);
                }
        }

        /// <summary>車道の中央の破線（A・B・C・E の近い所）と、A と C の交差点の近くの二重の黄線</summary>
        static void Markings(Town town)
        {
            var y = G + 0.03f;
            foreach (var line in new[] { new Vector3(1f, StreetA, 0f), new Vector3(0f, StreetB, 0f), new Vector3(0f, StreetE, 0f) })
            {
                var eastWest = line.x > 0.5f;
                for (var s = -130f; s < 130f; s += 9f)
                {
                    var mid = s + 1.5f;
                    if (Crossed(eastWest, line.y, mid)) continue;
                    // 南北の通りは川沿いの F で終わる
                    // 南北の通りは D で終わる（その北は公園）
                    if (!eastWest && s + 3f > StreetD - RoadHalf) continue;
                    if (Wet(new Vector3(eastWest ? s : line.y, G, eastWest ? line.y : s), 8f)) continue;
                    var a = eastWest ? new Vector3(s, y, line.y - 0.06f) : new Vector3(line.y - 0.06f, y, s);
                    var b = eastWest ? new Vector3(s + 3f, y, line.y - 0.06f) : new Vector3(line.y - 0.06f, y, s + 3f);
                    var c = eastWest ? new Vector3(s + 3f, y, line.y + 0.06f) : new Vector3(line.y + 0.06f, y, s + 3f);
                    var d = eastWest ? new Vector3(s, y, line.y + 0.06f) : new Vector3(line.y + 0.06f, y, s);
                    if (!Seen(a)) continue;
                    Face(town, a, b, c, d, Sw.White, Vector3.up);
                }
            }
            // 交差点から 16 m の縁石沿いに二重の黄線
            foreach (var dir in new[] { -1f, 1f })
                foreach (var side in new[] { -1f, 1f })
                    for (var k = 0; k < 2; k++)
                    {
                        var off = side * (RoadHalf - 0.22f - k * 0.16f);
                        var s0 = StreetB + dir * (RoadHalf + 0.6f);
                        var s1 = StreetB + dir * (RoadHalf + 16f);
                        var lo = Mathf.Min(s0, s1);
                        var hi = Mathf.Max(s0, s1);
                        Face(town, new Vector3(lo, y, StreetA + off - 0.05f), new Vector3(hi, y, StreetA + off - 0.05f),
                            new Vector3(hi, y, StreetA + off + 0.05f), new Vector3(lo, y, StreetA + off + 0.05f), Sw.Yellow, Vector3.up);
                        Face(town, new Vector3(StreetB + off - 0.05f, y, lo), new Vector3(StreetB + off + 0.05f, y, lo),
                            new Vector3(StreetB + off + 0.05f, y, hi), new Vector3(StreetB + off - 0.05f, y, hi), Sw.Yellow, Vector3.up);
                    }
        }

        /// <summary>通りの上の位置 s が、交わる通りの車道の中か</summary>
        static bool Crossed(bool eastWest, float centre, float s)
        {
            if (eastWest)
            {
                foreach (var x in CrossStreets)
                {
                    var ns = NorthSouthSpan(x);
                    if (centre >= ns.x && centre <= ns.y && Mathf.Abs(s - x) < RoadHalf + 1f) return true;
                }
                return false;
            }
            foreach (var z in NorthStreets) if (Mathf.Abs(s - z) < RoadHalf + 1f) return true;
            foreach (var z in SouthStreets)
            {
                var ew = EastWestSpan(z);
                if (centre >= ew.x && centre <= ew.y && Mathf.Abs(s - z) < RoadHalf + 1f) return true;
            }
            return false;
        }

        // ---- 区画と家並み ----------------------------------------------------------

        static void Blocks(Town town)
        {
            for (var i = 0; i + 1 < CrossStreets.Length; i++)
            {
                var xa = CrossStreets[i];
                var xb = CrossStreets[i + 1];
                for (var j = 0; j + 1 < NorthStreets.Length; j++) Block(town, xa, xb, NorthStreets[j], NorthStreets[j + 1]);
                if (xa < StreetB - 0.01f) continue;
                for (var j = 0; j + 1 < SouthStreets.Length; j++)
                {
                    var zb = SouthStreets[j];
                    var za = SouthStreets[j + 1];
                    // 広場の区画は A から J まで。間の通り（-35.2）は E より東だけ
                    if (Mathf.Abs(xa - StreetB) < 0.01f && (Mathf.Abs(zb - StreetA) < 0.01f || Mathf.Abs(zb - StreetMid) < 0.01f)) continue;
                    Block(town, xa, xb, za, zb);
                }
            }
        }

        /// <summary>区画を一つ。ふつうは南の縁と北の縁に一列ずつ、どちらも通りを向く</summary>
        static void Block(Town town, float xa, float xb, float za, float zb)
        {
            var centre = new Vector3((xa + xb) * 0.5f, G, (za + zb) * 0.5f);
            // F より北は川、北西の D と F の間の二区画は見通しの公園
            if (Open(centre, 0f)) return;
            // 区画の一番近い角が組む遠さの外なら何も置かない
            var nearest = new Vector3(Mathf.Clamp(0f, xa, xb), G, Mathf.Clamp(0f, za, zb));
            if (Reach(nearest) > RoomView.TownReach) return;
            var wall = RowWall(town.R);
            var roof = RowRoof(town.R);
            var west = xa + FrontOffset;
            var east = xb - FrontOffset;
            var south = za + FrontOffset;
            var north = zb - FrontOffset;

            // B の両側の最初の区画。B を向く家並みを足し、北東の角はパブ
            if (Mathf.Abs(za - StreetA) < 0.01f && Mathf.Abs(xa - StreetB) < 0.01f)
            {
                Row(town, new Vector3(PubLot.xMax, G, south), Vector3.back, east - PubLot.xMax, wall, roof, false, true);
                Row(town, new Vector3(east, G, north), Vector3.forward, east - west, RowWall(town.R), roof, true, true);
                Row(town, new Vector3(west, G, north - HouseDeep), Vector3.left, north - HouseDeep - (south + HouseDeep), RowWall(town.R), roof, false, false);
                Gardens(town, west + HouseDeep, east, south + HouseDeep, north - HouseDeep);
                return;
            }
            if (Mathf.Abs(za - StreetA) < 0.01f && Mathf.Abs(xb - StreetB) < 0.01f)
            {
                Row(town, new Vector3(west, G, south), Vector3.back, east - west, wall, roof, true, true);
                Row(town, new Vector3(east, G, north), Vector3.forward, east - west, RowWall(town.R), roof, true, true);
                Row(town, new Vector3(east, G, south + HouseDeep), Vector3.right, north - HouseDeep - (south + HouseDeep), RowWall(town.R), roof, false, false);
                Gardens(town, west, east - HouseDeep, south + HouseDeep, north - HouseDeep);
                return;
            }
            // 教会の区画。A に面した南の列だけ残し、奥を教会と墓地にする
            if (Mathf.Abs(za - StreetA) < 0.01f && Mathf.Abs(xb - (-47.2f)) < 0.01f)
            {
                Row(town, new Vector3(west, G, south), Vector3.back, east - west, wall, roof, true, true);
                return;
            }
            // 公営住宅の塔の区画は塔だけ
            if (Contains(xa, xb, za, zb, 92.8f, -145.2f)) return;
            // 広場の東の二つの区画。E を向く家並み（広場を向く）を足し、東西の列は奥から
            if (Mathf.Abs(xa - StreetE) < 0.01f && za >= StreetJ - 0.01f && zb <= StreetA + 0.01f)
            {
                Row(town, new Vector3(west, G, north), Vector3.left, north - south, wall, roof, true, true);
                var from = west + HouseDeep + 4f;
                Row(town, new Vector3(from, G, south), Vector3.back, east - from, RowWall(town.R), roof, true, true);
                Row(town, new Vector3(east, G, north), Vector3.forward, east - from, RowWall(town.R), roof, true, true);
                Gardens(town, from, east, south + HouseDeep, north - HouseDeep);
                return;
            }
            // 広場の南。J に面した北の列は四階建ての古い集合住宅
            if (Mathf.Abs(xa - StreetB) < 0.01f && Mathf.Abs(zb - StreetJ) < 0.01f)
            {
                Flats(town, new Vector3(east, G, north), Vector3.forward, east - west);
                Row(town, new Vector3(west, G, south), Vector3.back, east - west, wall, roof, true, true);
                return;
            }

            var depth = north - south;
            Row(town, new Vector3(west, G, south), Vector3.back, east - west, wall, roof, true, true);
            if (depth >= HouseDeep * 2f + 4f)
                Row(town, new Vector3(east, G, north), Vector3.forward, east - west, RowWall(town.R), roof, true, true);
            Gardens(town, west, east, south + HouseDeep, north - HouseDeep);
        }

        static bool Contains(float xa, float xb, float za, float zb, float x, float z)
        {
            return x > xa && x < xb && z > za && z < zb;
        }

        /// <summary>家並みの壁の種類（黄色い煉瓦 35%・赤い煉瓦 20%・漆喰 15%・煤けた煉瓦 30%）</summary>
        static int RowWall(Random r)
        {
            var roll = F(r);
            return roll < 0.35f ? 0 : roll < 0.55f ? 1 : roll < 0.7f ? 2 : 3;
        }

        static int RowRoof(Random r)
        {
            var roll = F(r);
            return roll < 0.4f ? 0 : roll < 0.7f ? 1 : roll < 0.9f ? 2 : 3;
        }

        /// <summary>
        /// 家並みを一列。start は通りから見た左の端の、正面の線の地面。face は正面の向き（通りの側）。
        /// 間口を 5 m 前後に割って家を並べ、両端に妻壁を立てる
        /// </summary>
        static void Row(Town town, Vector3 start, Vector3 face, float length, int wall, int roof, bool gableStart, bool gableEnd)
        {
            if (length < 4f) return;
            var along = Vector3.Cross(face, Vector3.up);
            var count = Mathf.Max(1, Mathf.RoundToInt(length / 5.1f));
            var wide = length / count;
            for (var k = 0; k < count; k++)
            {
                var o = start + along * (k * wide);
                var centre = o + along * (wide * 0.5f) - face * (HouseDeep * 0.5f);
                // 種を家ごとに同じだけ使う（見えない家を飛ばしても、隣の家の絵が変わらない）
                var type = F(town.R) < 0.12f ? RowWall(town.R) : wall;
                var front = type + 4 * town.R.Next(Fronts / 4);
                var back = town.R.Next(Backs);
                var roll = F(town.R);
                var houseRoof = roll < 0.1f ? 4 : roll < 0.18f ? 5 : roof;
                if (!Seen(centre) || Open(centre, 6f)) continue;
                var reach = Reach(centre);
                var lod = reach < NearDetail ? 0 : reach < MidDetail ? 1 : 2;
                House(town, o, along, face, wide, front, back, houseRoof, lod, k == 0 && gableStart, k == count - 1 && gableEnd);
            }
        }

        /// <summary>
        /// 家を一軒。o は通りから見た左下の角。正面・裏・前後の屋根、端なら妻壁と妻の三角。
        /// 左の戸境（o）に煙突の胴を立て、近い家には壺を載せ、前に明かり取り・柵・石段を置く
        /// </summary>
        static void House(Town town, Vector3 o, Vector3 along, Vector3 face, float wide, int front, int back, int roof, int lod, bool gableA, bool gableB)
        {
            town.Houses++;
            var up = Vector3.up;
            var h = HouseHigh;
            var a = o;
            var b = o + along * wide;
            var ta = a + up * h;
            var tb = b + up * h;
            var ba = a - face * HouseDeep;
            var bb = b - face * HouseDeep;
            var tba = ba + up * h;
            var tbb = bb + up * h;
            var ra = a - face * (HouseDeep * 0.5f) + up * (h + RoofRise);
            var rb = ra + along * wide;
            Face(town, a, b, tb, ta, Uv(FrontCell(front)), face);
            Face(town, bb, ba, tba, tbb, Uv(BackCell(back)), -face);
            var slope = (face * RoofRise + up * HouseDeep * 0.5f).normalized;
            var backSlope = (-face * RoofRise + up * HouseDeep * 0.5f).normalized;
            Face(town, ta, tb, rb, ra, Uv(RoofCell(roof)), slope);
            Face(town, tbb, tba, ra, rb, Uv(RoofCell(roof == 4 || roof == 5 ? 0 : roof)), backSlope);
            var gable = Uv(GableCell(front % 2));
            if (gableA)
            {
                Face(town, ba, a, ta, tba, gable, -along);
                Tri(town, tba, ta, ra, Uv(Sw.Brick), -along);
            }
            if (gableB)
            {
                Face(town, b, bb, tbb, tb, gable, along);
                Tri(town, tb, tbb, rb, Uv(Sw.Brick), along);
            }
            if (lod <= 1)
            {
                Stack(town, a - face * (HouseDeep * 0.5f), face, lod);
                if (gableB) Stack(town, b - face * (HouseDeep * 0.5f), face, lod);
            }
            if (lod > 1) return;
            // 明かり取り（半地下の前の暗い溝）
            var y = G + 0.02f;
            Face(town, a + Vector3.up * 0.02f, b + Vector3.up * 0.02f, b + face * AreaWide + Vector3.up * 0.02f, a + face * AreaWide + Vector3.up * 0.02f, Sw.Area, Vector3.up);
            if (lod > 0) return;
            // 柵
            var rail = G + KerbHigh;
            var ea = a + face * AreaWide;
            var eb = b + face * AreaWide;
            Face(town, new Vector3(eb.x, rail, eb.z), new Vector3(ea.x, rail, ea.z), new Vector3(ea.x, rail + 1.0f, ea.z), new Vector3(eb.x, rail + 1.0f, eb.z), Uv(Railing), face);
            // 玄関の石段（明かり取りに渡した橋）
            var doorLeft = (front / 4) % 2 == 0;
            var door = o + along * (wide * (doorLeft ? 7.5f : 24.5f) / 32f) + face * (AreaWide * 0.5f);
            var yaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            Box(town, new Vector3(door.x, y + 0.24f, door.z), new Vector3(1.3f, 0.48f, AreaWide), yaw, Sw.Stone, Sw.Stone);
        }

        /// <summary>戸境の煙突。棟を跨ぐ煉瓦の胴と、近い家には素焼きの壺</summary>
        static void Stack(Town town, Vector3 ridgeFoot, Vector3 face, int lod)
        {
            var yaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            var ridge = G + HouseHigh + RoofRise;
            var centre = new Vector3(ridgeFoot.x, ridge + 0.35f, ridgeFoot.z);
            Box(town, centre, new Vector3(0.8f, 1.9f, 1.5f), yaw, Sw.Brick, Sw.Lead);
            if (lod > 0) return;
            var across = face;
            var count = 2 + town.R.Next(3);
            for (var k = 0; k < count; k++)
            {
                var off = (k - (count - 1) * 0.5f) * 0.34f;
                var p = centre + across * off + Vector3.up * (0.95f + 0.22f);
                Box(town, p, new Vector3(0.22f, 0.44f, 0.22f), yaw, Sw.Pot, Sw.Black);
            }
        }

        /// <summary>裏庭に木を少し。近い区画だけ</summary>
        static void Gardens(Town town, float x0, float x1, float z0, float z1)
        {
            if (z1 - z0 < 5f || x1 - x0 < 5f) return;
            var count = 1 + town.R.Next(3);
            for (var k = 0; k < count; k++)
            {
                var p = new Vector3(Mathf.Lerp(x0 + 2f, x1 - 2f, F(town.R)), G, Mathf.Lerp(z0 + 2f, z1 - 2f, F(town.R)));
                var size = 5f + F(town.R) * 3f;
                if (!Seen(p) || Reach(p) > 130f || Open(p, 4f)) continue;
                Tree(town, p, size, size * 0.9f, 2.4f);
            }
        }

        // ---- 特別な建物 ----------------------------------------------------------

        /// <summary>北東の角のパブ。三階建て、一階は深い緑に大きな磨りガラスの窓。寄棟の屋根</summary>
        static void Pub(Town town)
        {
            var lot = PubLot;
            var h = HouseHigh;
            var sw = new Vector3(lot.xMin, G, lot.yMin);
            var se = new Vector3(lot.xMax, G, lot.yMin);
            var nw = new Vector3(lot.xMin, G, lot.yMax);
            var ne = new Vector3(lot.xMax, G, lot.yMax);
            var up = Vector3.up * h;
            Face(town, sw, se, se + up, sw + up, Uv(PubSouth), Vector3.back);
            Face(town, nw, sw, sw + up, nw + up, Uv(PubWest), Vector3.left);
            // 寄棟。棟は東西に短く
            var r0 = new Vector3(lot.xMin + 3f, G + h + 3f, lot.center.y);
            var r1 = new Vector3(lot.xMax - 3f, G + h + 3f, lot.center.y);
            var slate = Uv(RoofCell(1));
            Face(town, sw + up, se + up, r1, r0, slate, (Vector3.back * 3f + Vector3.up * 5f).normalized);
            Face(town, ne + up, nw + up, r0, r1, slate, (Vector3.forward * 3f + Vector3.up * 5f).normalized);
            Tri(town, nw + up, sw + up, r0, slate, (Vector3.left * 3f + Vector3.up * 3f).normalized);
            Tri(town, se + up, ne + up, r1, slate, (Vector3.right * 3f + Vector3.up * 3f).normalized);
            Box(town, new Vector3(lot.center.x + 1.5f, G + h + 3.2f, lot.center.y), new Vector3(0.9f, 1.8f, 1.6f), 0f, Sw.Brick, Sw.Lead);
            // 窓から歩道へこぼれる灯り
            Pool(town, new Vector3(lot.xMin + 2.5f, G + KerbHigh + 0.04f, lot.yMin - 2.4f), 5f, new Color(0.55f, 0.36f, 0.18f));
            Pool(town, new Vector3(lot.xMax - 2.5f, G + KerbHigh + 0.04f, lot.yMin - 2.4f), 5f, new Color(0.55f, 0.36f, 0.18f));
            Pool(town, new Vector3(lot.xMin - 2.4f, G + KerbHigh + 0.04f, lot.center.y), 5f, new Color(0.55f, 0.36f, 0.18f));
        }

        /// <summary>
        /// 教会。身廊は東西に長く、東の端に塔と尖塔。塔は座って始めた目から北の窓の抜けの真ん中（左へ 60 度）に来て、
        /// 尖塔の先が窓の上の縁の下に収まる（34 m）
        /// </summary>
        static void Church(Town town)
        {
            var t = ChurchTowerAt;
            var tower = 7f;
            var towerHigh = 20f;
            var spire = 14f;
            var naveWide = 12f;
            var naveLong = 25.5f;
            var wallHigh = 11f;
            var x1 = t.x - tower * 0.5f;
            var x0 = x1 - naveLong;
            var z0 = t.y - naveWide * 0.5f;
            var z1 = t.y + naveWide * 0.5f;
            var up = Vector3.up * wallHigh;
            var bays = 4;
            var bay = naveLong / bays;
            var wall = Uv(ChurchWall);
            for (var k = 0; k < bays; k++)
            {
                var xa = x0 + k * bay;
                var xb = xa + bay;
                Face(town, new Vector3(xa, G, z0), new Vector3(xb, G, z0), new Vector3(xb, G, z0) + up, new Vector3(xa, G, z0) + up, wall, Vector3.back);
                Face(town, new Vector3(xb, G, z1), new Vector3(xa, G, z1), new Vector3(xa, G, z1) + up, new Vector3(xb, G, z1) + up, wall, Vector3.forward);
            }
            // 西と東の妻（東は塔の両脇が覗く）
            Face(town, new Vector3(x0, G, z1), new Vector3(x0, G, z0), new Vector3(x0, G, z0) + up, new Vector3(x0, G, z1) + up, wall, Vector3.left);
            Face(town, new Vector3(x1, G, z0), new Vector3(x1, G, z1), new Vector3(x1, G, z1) + up, new Vector3(x1, G, z0) + up, wall, Vector3.right);
            var rise = 6.5f;
            var ridge0 = new Vector3(x0, G + wallHigh + rise, t.y);
            var ridge1 = new Vector3(x1, G + wallHigh + rise, t.y);
            Tri(town, new Vector3(x0, G + wallHigh, z1), new Vector3(x0, G + wallHigh, z0), ridge0, Uv(ChurchWall), Vector3.left);
            Tri(town, new Vector3(x1, G + wallHigh, z0), new Vector3(x1, G + wallHigh, z1), ridge1, Uv(ChurchWall), Vector3.right);
            var slate = Uv(RoofCell(1));
            Face(town, new Vector3(x0, G + wallHigh, z0), new Vector3(x1, G + wallHigh, z0), ridge1, ridge0, slate, (Vector3.back * rise + Vector3.up * 6f).normalized);
            Face(town, new Vector3(x1, G + wallHigh, z1), new Vector3(x0, G + wallHigh, z1), ridge0, ridge1, slate, (Vector3.forward * rise + Vector3.up * 6f).normalized);
            // 塔と尖塔
            var h = tower * 0.5f;
            var c = new Vector3(t.x, G, t.y);
            var corners = new[] { c + new Vector3(-h, 0f, -h), c + new Vector3(h, 0f, -h), c + new Vector3(h, 0f, h), c + new Vector3(-h, 0f, h) };
            var normals = new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
            var tall = Vector3.up * towerHigh;
            var tip = c + Vector3.up * (towerHigh + spire);
            for (var k = 0; k < 4; k++)
            {
                var p = corners[k];
                var q = corners[(k + 1) % 4];
                Face(town, p, q, q + tall, p + tall, Uv(ChurchTower), normals[k]);
                Tri(town, p + tall, q + tall, tip, Uv(Spire), (normals[k] * 4f + Vector3.up).normalized);
            }
            // 墓地の芝と木
            Face(town, new Vector3(-96f, G + 0.01f, 28f), new Vector3(-54f, G + 0.01f, 28f), new Vector3(-54f, G + 0.01f, 58f), new Vector3(-96f, G + 0.01f, 58f), Sw.Lawn, Vector3.up);
            foreach (var p in new[] { new Vector2(-92f, 31f), new Vector2(-80f, 53f), new Vector2(-58f, 31f), new Vector2(-60f, 54f), new Vector2(-88f, 55f) })
                Tree(town, new Vector3(p.x, G, p.y), 6.5f, 6f, 2.6f);
        }

        /// <summary>
        /// 公営住宅の塔（遠景）。一辺を 6 m の区画、4 階ごとの段に割り、コンクリートの升を貼る。屋上に機械室
        /// </summary>
        static void TowerBlock(Town town, Vector2 at, Vector2 size, float yaw, int levels)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var hx = size.x * 0.5f;
            var hz = size.y * 0.5f;
            var c = new Vector3(at.x, G, at.y);
            var corners = new[] { new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz) };
            var levelHigh = 13f;
            for (var k = 0; k < 4; k++)
            {
                var p = c + rot * corners[k];
                var q = c + rot * corners[(k + 1) % 4];
                var n = Vector3.Cross(q - p, Vector3.up).normalized;
                var span = Vector3.Distance(p, q);
                var bays = Mathf.Max(1, Mathf.RoundToInt(span / 6f));
                for (var b = 0; b < bays; b++)
                    for (var l = 0; l < levels; l++)
                    {
                        var a = Vector3.Lerp(p, q, b / (float)bays) + Vector3.up * (l * levelHigh);
                        var e = Vector3.Lerp(p, q, (b + 1f) / bays) + Vector3.up * (l * levelHigh);
                        Face(town, a, e, e + Vector3.up * levelHigh, a + Vector3.up * levelHigh, Uv(FlatsCell(2 + (b + l + k) % 2)), -n);
                    }
            }
            var top = G + levels * levelHigh;
            var roof = new Vector3[4];
            for (var k = 0; k < 4; k++) roof[k] = c + rot * corners[k] + Vector3.up * (levels * levelHigh);
            Face(town, roof[0], roof[1], roof[2], roof[3], Sw.FlatRoof, Vector3.up);
            Box(town, new Vector3(at.x, top + 1.8f, at.y), new Vector3(5f, 3.6f, 5f), yaw, Sw.Wall, Sw.FlatRoof);
        }

        /// <summary>四階建ての古い集合住宅の列。6 m の区画に赤煉瓦の升、奥行き 14 m、平らな屋根</summary>
        static void Flats(Town town, Vector3 start, Vector3 face, float length)
        {
            var along = Vector3.Cross(face, Vector3.up);
            var bays = Mathf.Max(1, Mathf.RoundToInt(length / 6f));
            var wide = length / bays;
            var high = 13f;
            var deep = 14f;
            var up = Vector3.up * high;
            for (var k = 0; k < bays; k++)
            {
                var a = start + along * (k * wide);
                var b = a + along * wide;
                if (!Seen(a)) continue;
                Face(town, a, b, b + up, a + up, Uv(FlatsCell(k % 2)), face);
                Face(town, b - face * deep, a - face * deep, a - face * deep + up, b - face * deep + up, Uv(FlatsCell((k + 1) % 2)), -face);
                Face(town, a + up, b + up, b - face * deep + up, a - face * deep + up, Sw.FlatRoof, Vector3.up);
                // 屋上の低い胸壁
                Face(town, a + up, b + up, b + up + Vector3.up * 0.8f, a + up + Vector3.up * 0.8f, Sw.Stone, face);
                town.Houses++;
            }
            var end = start + along * length;
            Face(town, start - face * deep, start, start + up, start - face * deep + up, Uv(GableCell(1)), -along);
            Face(town, end, end - face * deep, end - face * deep + up, end + up, Uv(GableCell(1)), along);
        }

        // ---- 広場 ------------------------------------------------------------------

        /// <summary>
        /// 東の窓の前の広場。柵と内側の生け垣で囲み、芝に十字の小径と真ん中の円い花壇、縁に沿って木、低い街灯を四つ
        /// </summary>
        static void SquareGarden(Town town)
        {
            var s = Square;
            var y = G + KerbHigh;
            Face(town, new Vector3(s.xMin, y + 0.01f, s.yMin), new Vector3(s.xMax, y + 0.01f, s.yMin), new Vector3(s.xMax, y + 0.01f, s.yMax), new Vector3(s.xMin, y + 0.01f, s.yMax), Sw.Lawn, Vector3.up);
            var mid = s.center;
            // 小径
            Face(town, new Vector3(mid.x - 1.3f, y + 0.03f, s.yMin), new Vector3(mid.x + 1.3f, y + 0.03f, s.yMin), new Vector3(mid.x + 1.3f, y + 0.03f, s.yMax), new Vector3(mid.x - 1.3f, y + 0.03f, s.yMax), Sw.Gravel, Vector3.up);
            Face(town, new Vector3(s.xMin, y + 0.03f, mid.y - 1.3f), new Vector3(s.xMax, y + 0.03f, mid.y - 1.3f), new Vector3(s.xMax, y + 0.03f, mid.y + 1.3f), new Vector3(s.xMin, y + 0.03f, mid.y + 1.3f), Sw.Gravel, Vector3.up);
            // 真ん中の円い花壇（八角）
            var ring = new Vector3[8];
            for (var k = 0; k < 8; k++)
            {
                var a = (k + 0.5f) * Mathf.PI / 4f;
                ring[k] = new Vector3(mid.x + Mathf.Cos(a) * 5f, y + 0.05f, mid.y + Mathf.Sin(a) * 5f);
            }
            var centre = new Vector3(mid.x, y + 0.05f, mid.y);
            for (var k = 0; k < 8; k++)
                town.Solid.Tri(centre, ring[k], ring[(k + 1) % 8], Uv(Sw.Hedge).center, Uv(Sw.Hedge).center, Uv(Sw.Hedge).center,
                    Shade(town, Vector3.up, centre), Shade(town, Vector3.up, ring[k]), Shade(town, Vector3.up, ring[(k + 1) % 8]));
            // 柵と生け垣。門（各辺の真ん中）は空ける
            var edges = new[]
            {
                new Vector4(s.xMin, s.yMin, s.xMin, s.yMax),
                new Vector4(s.xMin, s.yMax, s.xMax, s.yMax),
                new Vector4(s.xMax, s.yMax, s.xMax, s.yMin),
                new Vector4(s.xMax, s.yMin, s.xMin, s.yMin),
            };
            foreach (var e in edges)
            {
                var p = new Vector3(e.x, y, e.y);
                var q = new Vector3(e.z, y, e.w);
                var length = Vector3.Distance(p, q);
                var dir = (q - p) / length;
                var inward = Vector3.Cross(Vector3.up, dir);
                var pieces = Mathf.CeilToInt(length / 5f);
                for (var k = 0; k < pieces; k++)
                {
                    var a = p + dir * (length * k / pieces);
                    var b = p + dir * (length * (k + 1) / pieces);
                    var at = (Vector3.Distance(p, (a + b) * 0.5f)) / length;
                    if (Mathf.Abs(at - 0.5f) < 0.04f) continue;
                    if (!Seen(a)) continue;
                    var outward = -inward;
                    Face(town, a, b, b + Vector3.up * 1.2f, a + Vector3.up * 1.2f, Uv(Railing), outward);
                    var hc = (a + b) * 0.5f + inward * 0.9f + Vector3.up * 0.55f;
                    var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    Box(town, hc, new Vector3(1.1f, 1.1f, Vector3.Distance(a, b)), yaw, Sw.Hedge, Sw.Hedge);
                }
            }
            // 縁に沿った木と、芝の中の木
            var trees = 0;
            // 北の縁は東の窓の斜め前（北西の角）を空けて始める
            for (var x = s.xMin + 5f; x < s.xMax - 3f; x += 11f)
            {
                trees++;
                if (x > s.xMin + 10f) Tree(town, new Vector3(x, y, s.yMax - 4.5f), 7f + (trees % 3), 7f, 3f);
                Tree(town, new Vector3(x + 5f, y, s.yMin + 4.5f), 7f + ((trees + 1) % 3), 7f, 3f);
            }
            for (var z = s.yMin + 12f; z < s.yMax - 8f; z += 12f)
            {
                trees++;
                // 東の窓の真正面（z 0 の辺り）は低くして、広場の向こうの家並みと遠い街を塞がない
                var low = Mathf.Abs(z) < 12f;
                Tree(town, new Vector3(s.xMin + 4.5f, y, z), low ? 5f : 7.5f, low ? 4.5f : 7f, low ? 2f : 3f);
                Tree(town, new Vector3(s.xMax - 4.5f, y, z + 5f), 7.5f, 7f, 3f);
            }
            foreach (var p in new[] { new Vector2(mid.x - 12f, mid.y + 14f), new Vector2(mid.x + 11f, mid.y - 13f), new Vector2(mid.x + 13f, mid.y + 20f), new Vector2(mid.x - 10f, mid.y - 22f) })
                Tree(town, new Vector3(p.x, y, p.y), 8.5f, 8f, 3.2f);
        }

        // ---- 木 ------------------------------------------------------------------

        /// <summary>
        /// 木を一本。幹の細い箱と、樹冠の札を十字に二枚と水平に一枚（上から見下ろすと丸く見える）
        /// </summary>
        static void Tree(Town town, Vector3 root, float crown, float high, float clear)
        {
            town.Trees++;
            var cell = Uv(CanopyCell(town.R.Next(3)));
            var yaw = F(town.R) * 180f;
            Box(town, root + Vector3.up * (clear * 0.5f + 0.3f), new Vector3(0.35f, clear + 0.6f, 0.35f), yaw, Sw.Trunk, Sw.Trunk);
            var bottom = root.y + clear;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            for (var k = 0; k < 2; k++)
            {
                var across = rot * Quaternion.Euler(0f, k * 90f, 0f) * Vector3.right * (crown * 0.5f);
                var n = Vector3.Cross(across, Vector3.up).normalized;
                var c = new Vector3(root.x, bottom, root.z);
                Face(town, c - across, c + across, c + across + Vector3.up * high, c - across + Vector3.up * high, cell, n);
            }
            var mid = new Vector3(root.x, bottom + high * 0.55f, root.z);
            var r0 = rot * new Vector3(-crown * 0.5f, 0f, -crown * 0.5f);
            var r1 = rot * new Vector3(crown * 0.5f, 0f, -crown * 0.5f);
            Face(town, mid + r0, mid + r1, mid - r0, mid - r1, cell, Vector3.up);
        }

        /// <summary>
        /// 街路樹。B の両側の歩道（横断歩道の所は空ける）、A の北の歩道（北の窓の正面は空ける）、E の両側
        /// </summary>
        static void StreetTrees(Town town)
        {
            var west = StreetB - RoadHalf - PavementWide * 0.45f;
            var east = StreetB + RoadHalf + PavementWide * 0.45f;
            // B は D で終わる（その北は公園）
            for (var z = 26f; z < 56f; z += 12.5f)
            {
                Tree(town, new Vector3(west, G + KerbHigh, z), 6f, 6.5f, 3f);
                Tree(town, new Vector3(east, G + KerbHigh, z + 6f), 6f, 6.5f, 3f);
            }
            var northPave = StreetA + RoadHalf + PavementWide * 0.45f;
            for (var x = -119.5f; x < 128f; x += 15f)
            {
                if (x > -12f && x < 36f) continue;
                Tree(town, new Vector3(x, G + KerbHigh, northPave), 6f, 6.5f, 3f);
            }
            for (var z = -66f; z < 60f; z += 14f)
            {
                if (Mathf.Abs(z - StreetA) < 10f || Mathf.Abs(z - StreetMid) < 10f) continue;
                var p = new Vector3(StreetE + RoadHalf + PavementWide * 0.45f, G + KerbHigh, z);
                if (!Open(p, 4f)) Tree(town, p, 6.5f, 6.5f, 3f);
            }
        }

        // ---- 車 ------------------------------------------------------------------

        /// <summary>縁石沿いに止めてある車。A と B・C と E に少し。交差点と横断歩道の近くは空ける</summary>
        static void Cars(Town town)
        {
            var lanes = new[]
            {
                new Vector4(1f, StreetA - RoadHalf + 1.1f, -110f, 120f),
                new Vector4(1f, StreetA + RoadHalf - 1.1f, -110f, 120f),
                new Vector4(0f, StreetB - RoadHalf + 1.1f, -110f, StreetD - 8f),
                new Vector4(0f, StreetB + RoadHalf - 1.1f, -110f, StreetD - 8f),
                new Vector4(0f, StreetE - RoadHalf + 1.1f, -70f, 70f),
                new Vector4(0f, StreetE + RoadHalf - 1.1f, -70f, 70f),
            };
            foreach (var lane in lanes)
            {
                var eastWest = lane.x > 0.5f;
                for (var s = lane.z; s < lane.w; s += 5.6f)
                {
                    var roll = F(town.R);
                    var colour = CarColours[town.R.Next(CarColours.Length)];
                    if (roll > 0.22f) continue;
                    if (Mathf.Abs(s - (eastWest ? StreetB : StreetA)) < 16f) continue;
                    if (!eastWest && lane.y < 30f && s > 12f && s < 26f) continue;
                    if (Crossed(eastWest, lane.y, s)) continue;
                    var p = eastWest ? new Vector3(s, G, lane.y) : new Vector3(lane.y, G, s);
                    if (!Seen(p) || Open(p, 4f)) continue;
                    Car(town, p, eastWest ? 90f : 0f, colour);
                }
            }
        }

        /// <summary>車を一台。車体と屋根の箱（塗りの色を掛ける）と、足回りの暗い箱</summary>
        static void Car(Town town, Vector3 at, float yaw, Color colour)
        {
            town.Cars++;
            Box(town, at + Vector3.up * 0.14f, new Vector3(1.55f, 0.28f, 3.6f), yaw, Uv(Sw.Black), Uv(Sw.Black), Color.white);
            Box(town, at + Vector3.up * 0.62f, new Vector3(1.76f, 0.68f, 4.2f), yaw, Uv(Sw.Paint), Uv(Sw.Paint), colour);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box(town, at + Vector3.up * 1.2f + rot * new Vector3(0f, 0f, -0.25f), new Vector3(1.5f, 0.5f, 2.2f), yaw, Uv(Sw.CarGlass), Uv(Sw.Paint), colour);
        }

        // ---- 横断歩道と電話ボックス --------------------------------------------------

        /// <summary>B の交差点の北の横断歩道。白い縞と、両側の歩道の灯りの柱（黒白の縞の柱に橙の球）</summary>
        static void Crossing(Town town)
        {
            var z0 = StreetA + FrontOffset + 1.5f;
            var z1 = z0 + 3f;
            var y = G + 0.03f;
            var stripes = Mathf.RoundToInt(RoadHalf * 2f / 0.5f);
            for (var k = 0; k < stripes; k += 2)
            {
                var x0 = StreetB - RoadHalf + k * 0.5f;
                Face(town, new Vector3(x0, y, z0), new Vector3(x0 + 0.5f, y, z0), new Vector3(x0 + 0.5f, y, z1), new Vector3(x0, y, z1), Sw.White, Vector3.up);
            }
            foreach (var x in new[] { StreetB - RoadHalf - 0.5f, StreetB + RoadHalf + 0.5f })
            {
                var foot = new Vector3(x, G + KerbHigh, (z0 + z1) * 0.5f);
                Box(town, foot + Vector3.up * 1.2f, new Vector3(0.12f, 2.4f, 0.12f), 0f, Uv(Stripe), Uv(Sw.Black), Color.white);
                var globe = foot + Vector3.up * 2.55f;
                Box(town, globe, new Vector3(0.36f, 0.36f, 0.36f), 0f, Sw.BeaconLit, Sw.BeaconLit);
                Halo(town, globe, 1.6f, new Color(1f, 0.55f, 0.15f) * 0.9f);
            }
        }

        static void PhoneBox(Town town, Vector3 foot)
        {
            Box(town, foot + Vector3.up * 1.25f, new Vector3(0.9f, 2.5f, 0.9f), 0f, Uv(Phone), Uv(Sw.PhoneRed), Color.white);
            Halo(town, foot + Vector3.up * 1.3f, 2.2f, new Color(0.9f, 0.85f, 0.7f) * 0.35f);
        }

        // ---- 街灯 ------------------------------------------------------------------

        /// <summary>街灯の頭の位置を決める。家の頂点色（照り返し）が読むので、組む最初に</summary>
        static void PlaceLamps(Town town)
        {
            var head = G + KerbHigh + 5.9f;
            var nPave = StreetA + RoadHalf + 0.45f;
            var sPave = StreetA - RoadHalf - 0.45f;
            // 街路樹（15 m おき）のちょうど間に
            foreach (var x in new[] { -112f, -82f, -52f, -22f, 26f, 53f, 83f, 113f }) Lamp(town, new Vector3(x, head, nPave));
            foreach (var x in new[] { -67f, -37f, 41f, 71f, 101f }) Lamp(town, new Vector3(x, head, sPave));
            var wPave = StreetB - RoadHalf - 0.45f;
            var ePave = StreetB + RoadHalf + 0.45f;
            foreach (var z in new[] { -92f, -62f, -32f, 32f, 57f, 82f, 107f }) Lamp(town, new Vector3(wPave, head, z));
            foreach (var z in new[] { -77f, -47f, -17f, 38f, 63f, 88f, 113f }) Lamp(town, new Vector3(ePave, head, z));
            foreach (var z in new[] { -62f, -2f, 58f }) Lamp(town, new Vector3(StreetE - RoadHalf - 0.45f, head, z));
            foreach (var z in new[] { -45f, 25f }) Lamp(town, new Vector3(StreetE + RoadHalf + 0.45f, head, z));
            foreach (var x in new[] { 22f, 52f }) Lamp(town, new Vector3(x, head, StreetJ + RoadHalf + 0.45f));
            foreach (var x in new[] { -30f, 40f }) Lamp(town, new Vector3(x, head, NorthStreets[1] - RoadHalf - 0.45f));
            // 広場の中の低い街灯
            var m = Square.center;
            var low = G + KerbHigh + 3.6f;
            foreach (var p in new[] { new Vector2(m.x + 3f, m.y + 3f), new Vector2(m.x - 3f, m.y - 3f), new Vector2(m.x, Square.yMax - 6f), new Vector2(Square.xMin + 6f, m.y) })
                town.Lamps.Add(new Vector3(p.x, low, p.y));
        }

        static void Lamp(Town town, Vector3 head)
        {
            if (!Seen(head) || Open(head, 4f)) return;
            town.Lamps.Add(head);
        }

        /// <summary>街灯の柱と灯りと暈と、路面の溜まり</summary>
        static void LampPosts(Town town)
        {
            foreach (var head in town.Lamps)
            {
                var foot = new Vector3(head.x, G + KerbHigh, head.z);
                var high = head.y - foot.y;
                Box(town, foot + Vector3.up * (high * 0.5f - 0.3f), new Vector3(0.14f, high - 0.6f, 0.14f), 0f, Sw.Iron, Sw.Iron);
                Box(town, head, new Vector3(0.42f, 0.55f, 0.42f), 45f, Sw.LampLit, Sw.LampLit);
                Box(town, head + Vector3.up * 0.33f, new Vector3(0.5f, 0.1f, 0.5f), 45f, Sw.Iron, Sw.Iron);
                Halo(town, head, high > 5f ? 2.2f : 1.6f, LampWarm);
                Pool(town, foot + Vector3.up * 0.05f, high > 5f ? 9f : 6f, new Color(0.5f, 0.34f, 0.2f));
            }
        }

        /// <summary>
        /// 遠い通りの街灯。柱は立てず、灯りと暈だけを 32 m おきに互い違いに置く。夜に遠くの通りが灯りの点線になる
        /// </summary>
        static void FarLamps(Town town)
        {
            var head = G + KerbHigh + 5.9f;
            foreach (var z in NorthStreets)
            {
                var span = EastWestSpan(z);
                for (var x = span.x; x < span.y; x += 32f)
                {
                    var side = Mathf.RoundToInt(x / 32f) % 2 == 0 ? 1f : -1f;
                    FarLamp(town, new Vector3(x, head, z + side * (RoadHalf + 0.45f)));
                }
            }
            foreach (var z in SouthStreets)
            {
                var span = EastWestSpan(z);
                for (var x = span.x + 16f; x < span.y; x += 32f)
                {
                    var side = Mathf.RoundToInt(x / 32f) % 2 == 0 ? 1f : -1f;
                    FarLamp(town, new Vector3(x, head, z + side * (RoadHalf + 0.45f)));
                }
            }
            foreach (var x in CrossStreets)
            {
                var span = NorthSouthSpan(x);
                for (var z = span.x + 8f; z < span.y; z += 32f)
                {
                    var side = Mathf.RoundToInt(z / 32f) % 2 == 0 ? 1f : -1f;
                    FarLamp(town, new Vector3(x + side * (RoadHalf + 0.45f), head, z));
                }
            }
        }

        static void FarLamp(Town town, Vector3 head)
        {
            if (!Seen(head) || Reach(head) < 120f || Open(head, 4f)) return;
            Box(town, head, new Vector3(0.5f, 0.6f, 0.5f), 45f, Sw.LampLit, Sw.LampLit);
            Halo(town, head, 3.2f, LampWarm);
        }

        // ---- 暈 ------------------------------------------------------------------

        /// <summary>灯りの暈。縦の札を十字に二枚と、水平に一枚（見下ろしても丸く見える）</summary>
        static void Halo(Town town, Vector3 at, float size, Color colour)
        {
            var h = size * 0.5f;
            var full = new Rect(0f, 0f, 1f, 1f);
            town.Glow.Quad(at + new Vector3(-h, -h, 0f), at + new Vector3(h, -h, 0f), at + new Vector3(h, h, 0f), at + new Vector3(-h, h, 0f), full, colour, colour, colour, colour);
            town.Glow.Quad(at + new Vector3(0f, -h, -h), at + new Vector3(0f, -h, h), at + new Vector3(0f, h, h), at + new Vector3(0f, h, -h), full, colour, colour, colour, colour);
            town.Glow.Quad(at + new Vector3(-h, 0f, -h), at + new Vector3(h, 0f, -h), at + new Vector3(h, 0f, h), at + new Vector3(-h, 0f, h), full, colour, colour, colour, colour);
        }

        /// <summary>路面の灯りの溜まり。水平の札を一枚</summary>
        static void Pool(Town town, Vector3 at, float size, Color colour)
        {
            var h = size * 0.5f;
            var full = new Rect(0f, 0f, 1f, 1f);
            town.Glow.Quad(at + new Vector3(-h, 0f, -h), at + new Vector3(h, 0f, -h), at + new Vector3(h, 0f, h), at + new Vector3(-h, 0f, h), full, colour, colour, colour, colour);
        }

        // ---- 空 ------------------------------------------------------------------

        /// <summary>空の球のうち、窓から見える向きの幅だけ。仰角の輪は地平の近くを細かく</summary>
        static readonly float[] SkyRings = { RoomView.SkyBottom, -8f, -4f, -2f, -1f, 0f, 1f, 2f, 3f, 4f, 6f, 8f, 11f, 15f, 20f, 27f, 36f, 48f, 62f, 76f, 90f };

        static void SkyDome(Town town)
        {
            const float step = 5f;
            var white = Color.white;
            for (var az = RoomView.ArcFrom; az < RoomView.ArcTo - 0.01f; az += step)
            {
                var az1 = Mathf.Min(az + step, RoomView.ArcTo);
                for (var k = 0; k + 1 < SkyRings.Length; k++)
                {
                    var e0 = SkyRings[k];
                    var e1 = SkyRings[k + 1];
                    var uv = Rect.MinMaxRect(RoomView.SkyU(az), RoomView.SkyV(e0), RoomView.SkyU(az1), RoomView.SkyV(e1));
                    town.Sky.Quad(RoomView.OnSky(az, e0), RoomView.OnSky(az1, e0), RoomView.OnSky(az1, e1), RoomView.OnSky(az, e1), uv, white, white, white, white);
                }
            }
        }
    }
}
