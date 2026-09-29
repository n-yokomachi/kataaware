using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 窓の外の倫敦の名所（<see cref="BuildRoomView"/>）。部屋はテムズ川の南岸のバーモンジー（<see cref="RoomView.Latitude"/>）で、
    /// 部屋の北の窓は本物の北北西を向く（<see cref="RoomView.Turn"/>）。名所は本物の方角の並びのまま、窓から形が読める大きさと遠さに詰めて置く
    /// （向きと遠さは <see cref="RoomView.Landmarks"/>）。
    ///
    /// <list type="table">
    /// <item><term>北の窓</term><description>左からザ・シャード、セント・ポール大聖堂、ウォーキートーキー、タワーブリッジ（二本の塔が窓の真ん中の桟を挟む）、
    /// ガーキン、ロンドン塔（窓から B の通りの抜けの先に見える）</description></item>
    /// <item><term>東の窓</term><description>カナリー・ワーフの塔の群れと O2</description></item>
    /// <item><term>川</term><description>本物のように東西に流れる川を、部屋の向きに合わせて回した「川の座標」（<see cref="RoomView.RiverLocal"/>）で組み、景色へ回して置く。
    /// D の通りの北は川まで芝の公園で、窓からロンドン塔とタワーブリッジへの見通しを空ける。川の北岸の低い丘（タワー・ヒル）にロンドン塔</description></item>
    /// </list>
    ///
    /// **遠い名所は縮めて近くに置く。** 目のカメラの far は 1000 m、空の球は 450 m。本当の遠さに置くと空の球の向こうになるので、
    /// 決めた遠さで組んでから、目（床の上 1.65 m）を中心に 420 m まで縮める（<see cref="Far"/>）。窓から見た形と大きさは変わらない。
    /// 縮めた物は霞を控える（頂点色の α）。ロンドン塔とタワーブリッジは縮めずに置く。
    ///
    /// 石の名所は投光で照らす（夕暮れは点いたばかり、夜は強く）。ロンドン塔は明るいクリームの石、タワーブリッジは灰色がかった石と青い歩道橋で、
    /// 二つが一つの城の群れに見えないよう色を分ける。塔の頂・橋の歩道橋の灯り・航空障害灯はどちらの時刻も灯る
    /// </summary>
    public static partial class BuildRoomView
    {
        const float StreetD = 64.8f;
        /// <summary>川の南岸と北岸の護岸の縁（川の座標の z）</summary>
        const float RiverSouth = RoomView.RiverSouth;
        const float RiverNorth = RoomView.RiverNorth;
        /// <summary>南岸の川沿いの遊歩道の幅</summary>
        const float RiverWalk = 8f;
        /// <summary>川面の高さ</summary>
        const float Water = G - 2.5f;
        /// <summary>タワー・ヒル。ロンドン塔の立つ丘の高さ（地面から）</summary>
        const float TowerHill = 4f;
        /// <summary>縮めた名所を置く遠さ。空の球（450 m）の内</summary>
        const float FarPlace = 420f;
        /// <summary>縮めた名所と、縮めない近い名所の霞の効き（頂点色の α）</summary>
        const byte FarHaze = 80;
        const byte NearHaze = 90;
        /// <summary>公園の西の縁（北西の南北の通り）。ここから東の D の通りの北は、川まで芝の公園</summary>
        const float ParkWestStreet = -103.2f;

        /// <summary>目。縮めるときの中心</summary>
        static readonly Vector3 Eye = new Vector3(0f, G + 13.65f, 0f);

        /// <summary>川か、川沿いの遊歩道か（川の座標で南岸から margin 手前より北）。家並み・通り・街灯・木を置かない</summary>
        static bool Wet(Vector3 p, float margin)
        {
            return RoomView.RiverLocal(p).y > RiverSouth - RiverWalk - margin;
        }

        /// <summary>見通しの公園か（D の通りの北、公園の西の縁より東）</summary>
        static bool InPark(Vector3 p)
        {
            return p.z > StreetD + RoadHalf + PavementWide - 0.01f && p.x > ParkWestStreet + RoadHalf + PavementWide - 0.01f;
        }

        /// <summary>家並み・通りを置けない所（川・遊歩道・公園）</summary>
        static bool Open(Vector3 p, float margin)
        {
            return Wet(p, margin) || InPark(p);
        }

        /// <summary>川の座標の面を景色へ回して、名所の霞の効きで足す</summary>
        static void InRiverFrame(Town town, byte haze, System.Action build)
        {
            var keepSolid = town.Solid;
            var keepGlow = town.Glow;
            town.Solid = new Pile();
            town.Glow = new Pile();
            build();
            keepSolid.Append(town.Solid, RoomView.RiverWorld, haze);
            keepGlow.Append(town.Glow, RoomView.RiverWorld, haze);
            town.Solid = keepSolid;
            town.Glow = keepGlow;
        }

        static Vector3 At(float azimuth, float distance, float y = G)
        {
            var a = azimuth * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * distance, y, Mathf.Cos(a) * distance);
        }

        static RoomView.Landmark Mark(string name)
        {
            foreach (var m in RoomView.Landmarks) if (m.Name == name) return m;
            throw new System.Exception("名所が無い: " + name);
        }

        // ---- 組む順 ----------------------------------------------------------------

        static void Landmarks(Town town)
        {
            River(town);
            ParkLawn(town);
            TowerOfLondon(town);
            TowerBridge(town);
            Far(town, Mark(RoomView.Shard), Shard);
            Far(town, Mark(RoomView.StPauls), StPauls);
            Far(town, Mark(RoomView.WalkieTalkie), WalkieTalkie);
            Far(town, Mark(RoomView.Gherkin), Gherkin);
            // カナリー・ワーフは塔ごとに遠さが違うので、一本ずつ縮める
            var wharf = Mark(RoomView.CanaryWharf);
            Far(town, wharf.Azimuth - 1.6f, wharf.Distance - 20f, c => Wharf(town, c, 22f, 20f, 200f, GlassTower, true));
            Far(town, wharf.Azimuth - 3.2f, wharf.Distance + 60f, c => Wharf(town, c, 15f, 15f, 150f, CanadaFace, false));
            Far(town, wharf.Azimuth + 1.6f, wharf.Distance + 20f, c => Wharf(town, c, 22f, 20f, 200f, GlassTower, false));
            Far(town, wharf.Azimuth + 3.5f, wharf.Distance + 100f, c => Wharf(town, c, 17f, 17f, 230f, GlassTower, false));
            Far(town, wharf.Azimuth, wharf.Distance, c => CanadaSquare(town, c));
            Far(town, Mark(RoomView.O2), O2);
        }

        /// <summary>
        /// 遠い名所を一つ。決めた向きと遠さの所に本当の大きさで組み、目を中心に <see cref="FarPlace"/> まで縮めて、街並みへ足す
        /// </summary>
        static void Far(Town town, RoomView.Landmark mark, System.Action<Town, Vector3> build)
        {
            Far(town, mark.Azimuth, mark.Distance, c => build(town, c));
        }

        static void Far(Town town, float azimuth, float distance, System.Action<Vector3> build)
        {
            var keepSolid = town.Solid;
            var keepGlow = town.Glow;
            town.Solid = new Pile();
            town.Glow = new Pile();
            build(At(azimuth, distance));
            var s = Mathf.Min(1f, FarPlace / distance);
            System.Func<Vector3, Vector3> shrink = p => Eye + (p - Eye) * s;
            keepSolid.Append(town.Solid, shrink, FarHaze);
            keepGlow.Append(town.Glow, shrink, FarHaze);
            town.Solid = keepSolid;
            town.Glow = keepGlow;
            town.Landmarks++;
        }

        // ---- 形の道具 --------------------------------------------------------------

        /// <summary>
        /// n 角の回転体。profile は (半径, 高さ) を下から。高さは centre からの y。
        /// cell の横を一周に、縦を profile の道のりに沿って貼る。外から見て左から右へ絵が進む
        /// </summary>
        static void Lathe(Town town, Vector3 centre, Vector2[] profile, int sides, Rect cell, float yaw = 0f)
        {
            var len = new float[profile.Length];
            for (var i = 1; i < profile.Length; i++) len[i] = len[i - 1] + Vector2.Distance(profile[i - 1], profile[i]);
            var total = Mathf.Max(len[profile.Length - 1], 1e-3f);
            var step = 360f / sides;
            System.Func<float, float, float, Vector3> at = (r, h, a) =>
                centre + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * r, h, Mathf.Cos(a * Mathf.Deg2Rad) * r);
            for (var i = 0; i + 1 < profile.Length; i++)
                for (var k = 0; k < sides; k++)
                {
                    var a0 = yaw - k * step;
                    var a1 = a0 - step;
                    var mid = (a0 + a1) * 0.5f * Mathf.Deg2Rad;
                    var dr = profile[i + 1].x - profile[i].x;
                    var dh = profile[i + 1].y - profile[i].y;
                    var n = new Vector3(Mathf.Sin(mid) * dh, -dr, Mathf.Cos(mid) * dh).normalized;
                    var uv = Rect.MinMaxRect(cell.xMin + cell.width * k / sides, cell.yMin + cell.height * len[i] / total,
                        cell.xMin + cell.width * (k + 1) / sides, cell.yMin + cell.height * len[i + 1] / total);
                    Face(town, at(profile[i].x, profile[i].y, a0), at(profile[i].x, profile[i].y, a1),
                        at(profile[i + 1].x, profile[i + 1].y, a1), at(profile[i + 1].x, profile[i + 1].y, a0), uv, n);
                }
        }

        /// <summary>
        /// 向きを持つ四角い塊。centre は底の真ん中、半分の幅 hx・奥行き hz、高さ y0〜y1。
        /// 横の面は bay m ごと・level m ごとに cell を一枚ずつ貼る（0 なら一面に一枚）。上の面は top
        /// </summary>
        static void Mass(Town town, Vector3 centre, float hx, float hz, float y0, float y1, float yaw, Rect cell, float bay, float level, Rect top)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var corners = new[] { new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz) };
            var levels = level > 0f ? Mathf.Max(1, Mathf.RoundToInt((y1 - y0) / level)) : 1;
            for (var k = 0; k < 4; k++)
            {
                var p = centre + rot * corners[k];
                var q = centre + rot * corners[(k + 1) % 4];
                var span = Vector3.Distance(p, q);
                var bays = bay > 0f ? Mathf.Max(1, Mathf.RoundToInt(span / bay)) : 1;
                var n = Vector3.Cross(Vector3.up, q - p).normalized;
                for (var b = 0; b < bays; b++)
                    for (var l = 0; l < levels; l++)
                    {
                        var a = Vector3.Lerp(p, q, b / (float)bays);
                        var e = Vector3.Lerp(p, q, (b + 1f) / bays);
                        var lo = Mathf.Lerp(y0, y1, l / (float)levels);
                        var hi = Mathf.Lerp(y0, y1, (l + 1f) / levels);
                        Face(town, new Vector3(a.x, centre.y + lo, a.z), new Vector3(e.x, centre.y + lo, e.z),
                            new Vector3(e.x, centre.y + hi, e.z), new Vector3(a.x, centre.y + hi, a.z), cell, n);
                    }
            }
            var t = new Vector3[4];
            for (var k = 0; k < 4; k++) t[k] = centre + rot * corners[k] + Vector3.up * y1;
            Face(town, t[0], t[1], t[2], t[3], top, Vector3.up);
        }

        /// <summary>四角錐。base は底の真ん中</summary>
        static void Pyramid(Town town, Vector3 foot, float hx, float hz, float high, float yaw, Rect cell)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var c = new[] { foot + rot * new Vector3(-hx, 0f, -hz), foot + rot * new Vector3(hx, 0f, -hz), foot + rot * new Vector3(hx, 0f, hz), foot + rot * new Vector3(-hx, 0f, hz) };
            var tip = foot + Vector3.up * high;
            for (var k = 0; k < 4; k++)
            {
                var p = c[k];
                var q = c[(k + 1) % 4];
                var n = (Vector3.Cross(Vector3.up, q - p).normalized * high + Vector3.up * hx).normalized;
                Tri(town, p, q, tip, cell, n);
            }
        }

        /// <summary>二点を結ぶ細い角材（吊り鎖・マスト）。端の面は張らない</summary>
        static void Beam(Town town, Vector3 a, Vector3 b, float wide, Rect cell)
        {
            var dir = b - a;
            if (dir.sqrMagnitude < 1e-6f) return;
            var rot = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
            var h = wide * 0.5f;
            var offsets = new[] { new Vector3(-h, -h, 0f), new Vector3(h, -h, 0f), new Vector3(h, h, 0f), new Vector3(-h, h, 0f) };
            for (var k = 0; k < 4; k++)
            {
                var o0 = rot * offsets[k];
                var o1 = rot * offsets[(k + 1) % 4];
                var n = (o0 + o1).normalized;
                Face(town, a + o0, a + o1, b + o1, b + o0, cell, n);
            }
        }

        // ---- 川と公園 --------------------------------------------------------------

        /// <summary>多角形を半平面で切る（Sutherland–Hodgman）。inside が 0 以上の側を残す</summary>
        static List<Vector2> Clip(List<Vector2> poly, System.Func<Vector2, float> inside)
        {
            var result = new List<Vector2>();
            for (var i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                var fa = inside(a);
                var fb = inside(b);
                if (fa >= 0f) result.Add(a);
                if (fa >= 0f != fb >= 0f) result.Add(Vector2.Lerp(a, b, fa / (fa - fb)));
            }
            return result;
        }

        /// <summary>凸な多角形を y の高さの水平な扇に張る</summary>
        static void FlatFan(Town town, List<Vector2> poly, float y, Sw s)
        {
            if (poly.Count < 3) return;
            var uv = Uv(s).center;
            var c0 = new Vector3(poly[0].x, y, poly[0].y);
            for (var i = 1; i + 1 < poly.Count; i++)
            {
                var b = new Vector3(poly[i].x, y, poly[i].y);
                var c = new Vector3(poly[i + 1].x, y, poly[i + 1].y);
                town.Solid.Tri(c0, b, c, uv, uv, uv, Shade(town, Vector3.up, c0), Shade(town, Vector3.up, b), Shade(town, Vector3.up, c));
            }
        }

        /// <summary>景色の中の四角のうち、川沿いの遊歩道より手前の多角形</summary>
        static List<Vector2> SouthOfRiver(Rect square)
        {
            var poly = new List<Vector2> { new Vector2(square.xMin, square.yMin), new Vector2(square.xMax, square.yMin), new Vector2(square.xMax, square.yMax), new Vector2(square.xMin, square.yMax) };
            return Clip(poly, p => RiverSouth - RiverWalk - RoomView.RiverLocal(new Vector3(p.x, 0f, p.y)).y);
        }

        /// <summary>
        /// テムズ川。川の座標で組んで景色へ回す。川面（40 m ごとに川面の升）、両岸の護岸、南岸の遊歩道、北岸の岸壁とその先の地面、
        /// 両岸の遊歩道の灯り（柱は立てない）
        /// </summary>
        static void River(Town town)
        {
            InRiverFrame(town, 255, () =>
            {
                const float e = 320f;
                for (var x = -e; x < e; x += 40f)
                    for (var z = RiverSouth; z < RiverNorth - 0.01f; z += 58f)
                    {
                        var z1 = Mathf.Min(z + 58f, RiverNorth);
                        Face(town, new Vector3(x, Water, z), new Vector3(x + 40f, Water, z), new Vector3(x + 40f, Water, z1), new Vector3(x, Water, z1), Uv(WaterFace), Vector3.up);
                    }
                for (var x = -e; x < e; x += 16f)
                {
                    Face(town, new Vector3(x, Water, RiverSouth), new Vector3(x + 16f, Water, RiverSouth), new Vector3(x + 16f, G, RiverSouth), new Vector3(x, G, RiverSouth), Uv(Embankment), Vector3.forward);
                    Face(town, new Vector3(x + 16f, Water, RiverNorth), new Vector3(x, Water, RiverNorth), new Vector3(x, G, RiverNorth), new Vector3(x + 16f, G, RiverNorth), Uv(Embankment), Vector3.back);
                }
                var top = G + KerbHigh;
                Face(town, new Vector3(-e, top, RiverSouth - RiverWalk), new Vector3(e, top, RiverSouth - RiverWalk), new Vector3(e, top, RiverSouth), new Vector3(-e, top, RiverSouth), Sw.Pave, Vector3.up);
                Face(town, new Vector3(-e, top, RiverNorth), new Vector3(e, top, RiverNorth), new Vector3(e, top, RiverNorth + 10f), new Vector3(-e, top, RiverNorth + 10f), Sw.Pave, Vector3.up);
                Face(town, new Vector3(-e, G - 0.12f, RiverNorth + 10f), new Vector3(e, G - 0.12f, RiverNorth + 10f), new Vector3(e, G - 0.12f, 440f), new Vector3(-e, G - 0.12f, 440f), Sw.Garden, Vector3.up);
                for (var x = -e + 10f; x < e; x += 28f)
                {
                    var head = new Vector3(x, G + KerbHigh + 5.5f, RiverSouth - 1.2f);
                    Box(town, head, new Vector3(0.45f, 0.55f, 0.45f), 45f, Sw.LampLit, Sw.LampLit);
                    Halo(town, head, 2.6f, LampWarm);
                    var far = new Vector3(x + 14f, G + KerbHigh + 5.5f, RiverNorth + 2f);
                    Box(town, far, new Vector3(0.45f, 0.55f, 0.45f), 45f, Sw.LampLit, Sw.LampLit);
                    Halo(town, far, 2.6f, LampWarm);
                }
            });
        }

        /// <summary>
        /// 見通しの公園。D の通りの北、公園の西の縁より東を、川沿いの遊歩道まで芝にする。川へ向かう小径と、低い木
        /// （梢は 8 m に届かない。窓から名所の足元を見通す線の下）
        /// </summary>
        static void ParkLawn(Town town)
        {
            var west = ParkWestStreet + RoadHalf + PavementWide;
            var south = StreetD + RoadHalf + PavementWide;
            FlatFan(town, SouthOfRiver(Rect.MinMaxRect(west, south, StreetEnd, 420f)), G + 0.04f, Sw.Lawn);
            var across = RoomView.RiverWorld(Vector3.forward);
            var side = Vector3.Cross(Vector3.up, across) * 1.2f;
            foreach (var x in new[] { -60f, 12f, 70f })
            {
                var a = new Vector3(x, G + 0.07f, south);
                var k = 0f;
                while (!Wet(a + across * (k + 4f), 0f) && k < 240f) k += 4f;
                var b = a + across * k;
                Face(town, a - side, b - side, b + side, a + side, Sw.Gravel, Vector3.up);
            }
            for (var x = west + 6f; x < StreetEnd; x += 15f)
            {
                var p = new Vector3(x, G, south + 4f);
                if (!Wet(p, 6f)) Tree(town, p, 5.5f, 4.5f, 2.2f);
            }
            var along = RoomView.RiverWorld(Vector3.right);
            var start = RoomView.RiverWorld(new Vector3(-320f, G, RiverSouth - RiverWalk - 4f));
            for (var t = 0f; t < 640f; t += 17f)
            {
                var p = start + along * t;
                if (p.x > west + 3f && p.z > south + 3f && Mathf.Abs(p.x) < StreetEnd) Tree(town, p, 5f, 4.5f, 2.2f);
            }
        }

        // ---- ロンドン塔 --------------------------------------------------------------

        /// <summary>
        /// ロンドン塔。川の座標で組む。川の北岸の低い丘の上に、低い外の城壁（角に円い稜堡）、一段高い内の城壁（角に円い塔、辺の中ほどに四角い塔）、
        /// 真ん中にホワイト・タワー。城壁は天守よりずっと低く、天守が一段高く抜けて見える。
        /// 窓からは B の通りの抜けの先（家並みに遮られない向き）に見え、丘の足元まで見通せる
        /// </summary>
        static void TowerOfLondon(Town town)
        {
            InRiverFrame(town, NearHaze, () =>
            {
                var mark = Mark(RoomView.TowerOfLondon);
                var local = RoomView.RiverLocal(At(mark.Azimuth, mark.Distance));
                var c = new Vector3(local.x, G, local.y);
                var hill = G + TowerHill;
                // 丘。上の芝と、四方へ下りる斜面
                var hx = 44f;
                var hz = 38f;
                var foot = 4f;
                var t = new[] { new Vector3(c.x - hx, hill, c.z - hz), new Vector3(c.x + hx, hill, c.z - hz), new Vector3(c.x + hx, hill, c.z + hz), new Vector3(c.x - hx, hill, c.z + hz) };
                var f = new[] { new Vector3(c.x - hx - foot, G, c.z - hz - foot), new Vector3(c.x + hx + foot, G, c.z - hz - foot), new Vector3(c.x + hx + foot, G, c.z + hz + foot), new Vector3(c.x - hx - foot, G, c.z + hz + foot) };
                Face(town, t[0], t[1], t[2], t[3], Sw.Lawn, Vector3.up);
                for (var k = 0; k < 4; k++)
                {
                    var n = (Vector3.Cross(Vector3.up, t[(k + 1) % 4] - t[k]).normalized + Vector3.up).normalized;
                    Face(town, f[k], f[(k + 1) % 4], t[(k + 1) % 4], t[k], Sw.Lawn, n);
                }
                var ground = new Vector3(c.x, hill, c.z);
                // 外の城壁と稜堡（低い）、内の城壁と塔（一段高い）、真ん中の天守。
                // 天守の南の面は窓の方へ少し回す（川に対して 6.5 度）。真正面から少しだけずらし、奥の二つの小塔の頭が脇に覗く
                var keepSolid = town.Solid;
                town.Solid = new Pile();
                Walls(town, ground, 38f, 32f, 4f, 3f, 5.5f, 6f, false);
                Walls(town, ground, 30f, 26f, 6f, 3f, 4.5f, 9f, true);
                WhiteTower(town, ground);
                var turn = Quaternion.Euler(0f, -6.5f, 0f);
                keepSolid.Append(town.Solid, p => ground + turn * (p - ground), NearHaze);
                town.Solid = keepSolid;
            });
            town.Landmarks++;
        }

        /// <summary>
        /// 四角い城壁。辺を 20 m ほどの区切りの厚い壁（頭を狭間に抜いた城壁の升）にし、角に円い塔。
        /// squares なら辺の中ほどに四角い塔も立てる
        /// </summary>
        static void Walls(Town town, Vector3 centre, float hx, float hz, float high, float thick, float drum, float drumHigh, bool squares)
        {
            var corners = new[] { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz) };
            for (var k = 0; k < 4; k++)
            {
                var p = new Vector3(centre.x + corners[k].x, centre.y, centre.z + corners[k].y);
                var q = new Vector3(centre.x + corners[(k + 1) % 4].x, centre.y, centre.z + corners[(k + 1) % 4].y);
                var span = Vector3.Distance(p, q);
                var dir = (q - p) / span;
                var pieces = Mathf.Max(1, Mathf.RoundToInt(span / 20f));
                var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90f;
                for (var i = 0; i < pieces; i++)
                {
                    var mid = p + dir * (span * (i + 0.5f) / pieces);
                    Mass(town, mid, span / pieces * 0.5f, thick * 0.5f, 0f, high, yaw, Uv(CurtainWall), 0f, 0f, Uv(Sw.Stone));
                }
                Lathe(town, p, new[] { new Vector2(drum, 0f), new Vector2(drum, drumHigh) }, 10, Uv(DrumTower));
                Lathe(town, p + Vector3.up * drumHigh, new[] { new Vector2(drum, 0f), new Vector2(0f, 0.01f) }, 10, Uv(Sw.Stone));
                if (!squares) continue;
                var half = p + (q - p) * 0.5f;
                Mass(town, half, 4f, 4f, 0f, drumHigh - 1f, yaw, Uv(DrumTower), 0f, 0f, Uv(Sw.Stone));
            }
        }

        /// <summary>
        /// ホワイト・タワー。明るいクリームの石の角張った天守。南の面の幅と胸壁までの高さはほぼ同じ（46 × 40 m、胸壁まで 44 m。小塔の頭まで入れると幅と高さが 1.2 対 1 ほど）。
        /// 窓は小さな半円の窓がまばら（升の絵）。南の面を川へ向ける。四隅の小塔は胸壁の上へ 10 m 抜け、鉛の玉ねぎ形の屋根と金の風見を載せる。北東の小塔だけ円い
        /// </summary>
        static void WhiteTower(Town town, Vector3 ground)
        {
            const float hx = 23f;
            const float hz = 20f;
            const float high = 44f;
            const float turret = 5.5f;
            const float turretHigh = 54f;
            Mass(town, ground, hx, hz, 0f, high, 0f, Uv(WhiteTowerFace), 0f, 0f, Uv(Sw.Lead));
            var corners = new[] { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz) };
            foreach (var k in corners)
            {
                var foot = ground + new Vector3(k.x, 0f, k.y);
                var round = k.x > 0f && k.y > 0f;
                if (round) Lathe(town, foot, new[] { new Vector2(turret * 1.2f, 0f), new Vector2(turret * 1.2f, turretHigh) }, 12, Uv(TurretFace));
                else Mass(town, foot, turret, turret, 0f, turretHigh, 0f, Uv(TurretFace), 0f, 0f, Uv(Sw.Lead));
                // 玉ねぎ形の屋根と風見
                var cap = foot + Vector3.up * turretHigh;
                var r = round ? turret * 1.2f : turret * 1.15f;
                Lathe(town, cap, new[]
                {
                    new Vector2(r, 0f), new Vector2(r * 1.14f, 1.6f), new Vector2(r * 1.06f, 3.8f), new Vector2(r * 0.7f, 6.2f),
                    new Vector2(r * 0.32f, 8.4f), new Vector2(r * 0.12f, 9.6f), new Vector2(0f, 10.2f),
                }, 8, Uv(Sw.LeadRoof));
                Mass(town, cap + Vector3.up * 10f, 0.8f, 0.8f, 0f, 3.6f, 0f, Uv(Sw.Gold), 0f, 0f, Uv(Sw.Gold));
            }
        }

        // ---- タワーブリッジ --------------------------------------------------------------

        /// <summary>
        /// タワーブリッジ。川の座標で組む。川の中の二本の塔（灰色がかった石張り、四隅の小塔と鉛の尖り屋根）、塔の間の低い跳ね橋の道と、
        /// 高い所の二本の歩道橋（青い鋼の格子）、両岸の小さな塔へ下る吊り鎖。軸は川を渡る向きで、窓からは斜めに見え、
        /// 二本の塔が北の窓の真ん中の桟を挟んで左右に立ち、間に歩道橋が渡る
        /// </summary>
        static void TowerBridge(Town town)
        {
            InRiverFrame(town, NearHaze, () =>
            {
                var bx = RoomView.BridgeX;
                var towers = new[] { RoomView.BridgeSouthTower, RoomView.BridgeNorthTower };
                var deck = G + 9f;
                var walkLow = G + 38f;
                var walkHigh = G + 43f;
                foreach (var z in towers)
                {
                    var foot = new Vector3(bx, Water, z);
                    Mass(town, foot, 13f, 11f, 0f, G + 1f - Water, 0f, Uv(Embankment), 8f, 0f, Uv(Sw.Stone));
                    var body = new Vector3(bx, G + 1f, z);
                    Mass(town, body, 10f, 8f, 0f, 43f, 0f, Uv(BridgeTower), 0f, 0f, Uv(Sw.Lead));
                    foreach (var k in new[] { new Vector2(-8.5f, -6.5f), new Vector2(8.5f, -6.5f), new Vector2(8.5f, 6.5f), new Vector2(-8.5f, 6.5f) })
                    {
                        var turret = new Vector3(bx + k.x, G + 40f, z + k.y);
                        Mass(town, turret, 2f, 2f, 0f, 12f, 0f, Uv(BridgeTower), 0f, 0f, Uv(Sw.Lead));
                        Pyramid(town, turret + Vector3.up * 12f, 2.2f, 2.2f, 7f, 0f, Uv(Sw.LeadRoof));
                    }
                    Pyramid(town, new Vector3(bx, G + 44f, z), 7.5f, 5.5f, 12f, 0f, Uv(Sw.LeadRoof));
                    Mass(town, new Vector3(bx, G + 55f, z), 0.8f, 0.8f, 0f, 7f, 0f, Uv(Sw.Gold), 0f, 0f, Uv(Sw.Gold));
                }
                var inner0 = towers[0] + 8f;
                var inner1 = towers[1] - 8f;
                // 跳ね橋の道と、塔の間の二本の歩道橋
                Mass(town, new Vector3(bx, deck - 2f, (inner0 + inner1) * 0.5f), 7f, (inner1 - inner0) * 0.5f, 0f, 2f, 0f, Uv(Sw.BridgeBlue), 0f, 0f, Uv(Sw.Road));
                foreach (var side in new[] { -6.5f, 6.5f })
                    Mass(town, new Vector3(bx + side, walkLow, (inner0 + inner1) * 0.5f), 1.4f, (inner1 - inner0) * 0.5f, 0f, walkHigh - walkLow, 0f,
                        Uv(BridgeWalk), 22f, 0f, Uv(Sw.Lead));
                // 両岸の小さな塔と、そこへ下る吊り鎖、脇の径間の道
                var shores = new[] { RiverSouth - 6f, RiverNorth + 6f };
                for (var i = 0; i < 2; i++)
                {
                    var shore = shores[i];
                    var from = towers[i] + (i == 0 ? -8f : 8f);
                    Mass(town, new Vector3(bx, G, shore), 5f, 5f, 0f, 20f, 0f, Uv(BridgeTower), 0f, 0f, Uv(Sw.Lead));
                    Pyramid(town, new Vector3(bx, G + 20f, shore), 5.4f, 5.4f, 7f, 0f, Uv(Sw.LeadRoof));
                    Mass(town, new Vector3(bx, deck - 2f, (from + shore) * 0.5f), 7f, Mathf.Abs(shore - from) * 0.5f, 0f, 2f, 0f, Uv(Sw.BridgeBlue), 0f, 0f, Uv(Sw.Road));
                    foreach (var side in new[] { -8f, 8f })
                    {
                        var x = bx + side;
                        var chain = new[]
                        {
                            new Vector3(x, walkLow, from), new Vector3(x, G + 26f, Mathf.Lerp(from, shore, 0.28f)),
                            new Vector3(x, G + 15f, Mathf.Lerp(from, shore, 0.6f)), new Vector3(x, G + 19f, shore),
                        };
                        for (var k = 0; k + 1 < chain.Length; k++) Beam(town, chain[k], chain[k + 1], 1.6f, Uv(Sw.BridgeBlue));
                    }
                }
                // 歩道橋と塔の頂の灯り
                foreach (var side in new[] { -6.5f, 6.5f })
                    for (var k = 0; k <= 3; k++)
                        Halo(town, new Vector3(bx + side, walkLow - 0.5f, Mathf.Lerp(inner0, inner1, k / 3f)), 6f, new Color(0.5f, 0.45f, 0.9f));
                foreach (var z in towers) Halo(town, new Vector3(bx, G + 30f, z - 8.5f), 14f, new Color(0.4f, 0.45f, 0.65f));
            });
            town.Landmarks++;
        }

        // ---- 遠い名所（決めた遠さで本当の大きさ。Far が縮める） ----------------------------

        /// <summary>ザ・シャード（310 m）。細る八角の錐に、頂の開いた骨組みの光</summary>
        static void Shard(Town town, Vector3 c)
        {
            Lathe(town, c, new[]
            {
                new Vector2(32f, 0f), new Vector2(30f, 40f), new Vector2(26f, 100f), new Vector2(20f, 160f), new Vector2(13f, 220f),
                new Vector2(7f, 265f), new Vector2(3.4f, 290f), new Vector2(1.5f, 302f),
            }, 8, Uv(ShardFace), 22.5f);
            for (var k = 0; k < 4; k++)
            {
                var a = (k * 90f + 22.5f) * Mathf.Deg2Rad;
                var foot = c + new Vector3(Mathf.Sin(a) * 3f, 286f, Mathf.Cos(a) * 3f);
                Beam(town, foot, c + new Vector3(Mathf.Sin(a) * 2.2f, 312f, Mathf.Cos(a) * 2.2f), 2.4f, Uv(Sw.TipLamp));
            }
            Halo(town, c + Vector3.up * 300f, 60f, new Color(0.6f, 0.62f, 0.75f));
            Mass(town, c + Vector3.up * 312f, 2.5f, 2.5f, 0f, 5f, 0f, Uv(Sw.RedLamp), 0f, 0f, Uv(Sw.RedLamp));
        }

        /// <summary>
        /// セント・ポール大聖堂（本物の 1.2 倍）。東西に長い身廊と翼廊、西の二本の塔、列柱のドラムに鉛のドーム、頂の明かり取りと金の十字
        /// </summary>
        static void StPauls(Town town, Vector3 c)
        {
            const float k = 1.2f;
            var stone = Uv(CathedralFace);
            Mass(town, c, 70f * k, 18f * k, 0f, 30f * k, 0f, stone, 14f, 15f * k, Uv(Sw.Lead));
            Mass(town, c, 18f * k, 40f * k, 0f, 30f * k, 0f, stone, 14f, 15f * k, Uv(Sw.Lead));
            foreach (var side in new[] { -13f * k, 13f * k })
            {
                var tower = c + new Vector3(-64f * k, 0f, side);
                Mass(town, tower, 6f * k, 6f * k, 0f, 62f * k, 0f, stone, 0f, 15f * k, Uv(Sw.Lead));
                Lathe(town, tower + Vector3.up * 62f * k, new[] { new Vector2(5f * k, 0f), new Vector2(3f * k, 5f * k), new Vector2(0f, 10f * k) }, 8, Uv(Sw.Lead));
            }
            Lathe(town, c + Vector3.up * 30f * k, new[] { new Vector2(22f * k, 0f), new Vector2(22f * k, 22f * k) }, 16, Uv(DrumFace));
            Lathe(town, c + Vector3.up * 52f * k, new[] { new Vector2(19.5f * k, 0f), new Vector2(19.5f * k, 6f * k) }, 16, stone);
            Lathe(town, c + Vector3.up * 58f * k, new[]
            {
                new Vector2(19.5f * k, 0f), new Vector2(19f * k, 5f * k), new Vector2(17.2f * k, 10f * k), new Vector2(14f * k, 15f * k),
                new Vector2(9.5f * k, 20f * k), new Vector2(5f * k, 23.5f * k), new Vector2(3.2f * k, 25f * k),
            }, 16, Uv(DomeFace));
            Lathe(town, c + Vector3.up * 83f * k, new[]
            {
                new Vector2(3.2f * k, 0f), new Vector2(3.2f * k, 9f * k), new Vector2(2.2f * k, 11f * k), new Vector2(1.1f * k, 15f * k), new Vector2(0.6f * k, 17f * k),
            }, 8, Uv(Sw.Portland));
            Mass(town, c + Vector3.up * 100f * k, 1.6f * k, 1.6f * k, 0f, 3f * k, 0f, Uv(Sw.Gold), 0f, 0f, Uv(Sw.Gold));
            Mass(town, c + Vector3.up * 103f * k, 0.5f * k, 0.5f * k, 0f, 4f * k, 0f, Uv(Sw.Gold), 0f, 0f, Uv(Sw.Gold));
            Halo(town, c + Vector3.up * 75f * k, 70f, new Color(0.35f, 0.3f, 0.22f));
        }

        /// <summary>ウォーキートーキー（160 m）。上ほど張り出す四角い塔と、頂の空中庭園。広い面を窓へ向ける</summary>
        static void WalkieTalkie(Town town, Vector3 c)
        {
            var yaw = Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg;
            var sections = new[]
            {
                new Vector3(0f, 21f, 14f), new Vector3(50f, 23f, 15f), new Vector3(95f, 26f, 16.5f), new Vector3(125f, 29.5f, 17.5f),
                new Vector3(148f, 30.5f, 18f), new Vector3(160f, 29f, 17f),
            };
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var total = sections[sections.Length - 1].x;
            var cell = Uv(WalkieFace);
            for (var i = 0; i + 1 < sections.Length; i++)
            {
                var s0 = sections[i];
                var s1 = sections[i + 1];
                var lo = new[] { new Vector3(-s0.y, 0f, -s0.z), new Vector3(s0.y, 0f, -s0.z), new Vector3(s0.y, 0f, s0.z), new Vector3(-s0.y, 0f, s0.z) };
                var hi = new[] { new Vector3(-s1.y, 0f, -s1.z), new Vector3(s1.y, 0f, -s1.z), new Vector3(s1.y, 0f, s1.z), new Vector3(-s1.y, 0f, s1.z) };
                var uv = Rect.MinMaxRect(cell.xMin, cell.yMin + cell.height * s0.x / total, cell.xMax, cell.yMin + cell.height * s1.x / total);
                for (var k = 0; k < 4; k++)
                {
                    var a = c + rot * lo[k] + Vector3.up * s0.x;
                    var b = c + rot * lo[(k + 1) % 4] + Vector3.up * s0.x;
                    var d = c + rot * hi[k] + Vector3.up * s1.x;
                    var e = c + rot * hi[(k + 1) % 4] + Vector3.up * s1.x;
                    Face(town, a, b, e, d, uv, Vector3.Cross(Vector3.up, b - a).normalized);
                }
            }
            var top = sections[sections.Length - 1];
            var roof = new[] { new Vector3(-top.y, 0f, -top.z), new Vector3(top.y, 0f, -top.z), new Vector3(top.y, 0f, top.z), new Vector3(-top.y, 0f, top.z) };
            Face(town, c + rot * roof[0] + Vector3.up * top.x, c + rot * roof[1] + Vector3.up * top.x, c + rot * roof[2] + Vector3.up * (top.x - 8f), c + rot * roof[3] + Vector3.up * (top.x - 8f), Uv(Sw.Lead), Vector3.up);
            Halo(town, c + Vector3.up * 150f, 50f, new Color(0.3f, 0.55f, 0.35f));
        }

        /// <summary>ガーキン（180 m）。砲弾形の回転体に、頂のガラスの丸屋根の灯り</summary>
        static void Gherkin(Town town, Vector3 c)
        {
            Lathe(town, c, new[]
            {
                new Vector2(24.5f, 0f), new Vector2(27f, 20f), new Vector2(28.2f, 50f), new Vector2(27.5f, 80f), new Vector2(25.5f, 110f),
                new Vector2(21.5f, 140f), new Vector2(15.5f, 163f), new Vector2(8.5f, 178f), new Vector2(3f, 186f), new Vector2(0f, 189f),
            }, 12, Uv(GherkinFace));
            Halo(town, c + Vector3.up * 182f, 36f, new Color(0.35f, 0.6f, 0.7f));
        }

        /// <summary>ワン・カナダ・スクエア（235 m）。ステンレスの四角い塔に四角錐の頂と、頂の点滅灯</summary>
        static void CanadaSquare(Town town, Vector3 c)
        {
            Mass(town, c, 25f, 25f, 0f, 235f, 0f, Uv(CanadaFace), 25f, 59f, Uv(Sw.Lead));
            Pyramid(town, c + Vector3.up * 235f, 25f, 25f, 20f, 0f, Uv(Sw.Portland));
            Mass(town, c + Vector3.up * 254f, 3f, 3f, 0f, 6f, 0f, Uv(Sw.TipLamp), 0f, 0f, Uv(Sw.TipLamp));
            Halo(town, c + Vector3.up * 256f, 50f, new Color(0.8f, 0.85f, 1f));
        }

        /// <summary>カナリー・ワーフのほかの塔。logo なら頂の近くに赤い看板</summary>
        static void Wharf(Town town, Vector3 c, float hx, float hz, float high, RectInt cell, bool logo)
        {
            var yaw = Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg;
            Mass(town, c, hx, hz, 0f, high, yaw, Uv(cell), 22f, 50f, Uv(Sw.FlatRoof));
            if (logo)
            {
                var face = c - new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)) * (hz + 0.5f);
                Mass(town, face + Vector3.up * (high - 18f), 14f, 1f, 0f, 9f, yaw, Uv(Sw.RedLamp), 0f, 0f, Uv(Sw.RedLamp));
            }
            Mass(town, c + Vector3.up * high, 2.5f, 2.5f, 0f, 4f, 0f, Uv(Sw.RedLamp), 0f, 0f, Uv(Sw.RedLamp));
        }

        /// <summary>O2（膜の径 330 m、高さ 50 m）。白い膜の低いドームと、外へ傾いた黄色いマスト 12 本、マストの頂の赤い灯り</summary>
        static void O2(Town town, Vector3 c)
        {
            Lathe(town, c, new[]
            {
                new Vector2(165f, 0f), new Vector2(155f, 12f), new Vector2(132f, 27f), new Vector2(95f, 40f), new Vector2(50f, 48f), new Vector2(0f, 50f),
            }, 24, Uv(O2Face));
            for (var k = 0; k < 12; k++)
            {
                var a = (k * 30f + 15f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var foot = c + dir * 130f + Vector3.up * 20f;
                var head = c + dir * 142f + Vector3.up * 100f;
                Beam(town, foot, head, 9f, Uv(Sw.O2Yellow));
                Mass(town, head, 6f, 6f, 0f, 8f, 0f, Uv(Sw.RedLamp), 0f, 0f, Uv(Sw.RedLamp));
            }
        }
    }
}
