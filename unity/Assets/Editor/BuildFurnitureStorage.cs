using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    public static partial class BuildFurniture
    {
        // ---- 本棚 ----------------------------------------------------------------------

        const float BookcaseWide = 0.90f;
        const float BookcaseDeep = 0.30f;
        const float BookcaseHigh = 1.85f;

        /// <summary>
        /// 濃い木の本棚（幅 0.90・奥行き 0.30・高さ 1.85、棚は 5 段）。段ごとに本を詰める: 立てた本の並び（背の色・高さ・厚み・奥行きがばらばら、
        /// 端の一冊は傾く）、寝かせて積んだ本、空のチップケースの列、隙間。背表紙は 32 種の絵から選ぶ
        /// </summary>
        static void Bookcase(FurnitureKit kit)
        {
            const float W = BookcaseWide, D = BookcaseDeep, H = BookcaseHigh;
            var wood = Walnut;
            kit.Box("BookcaseSide", new Vector3(-W * 0.5f + 0.01f, H * 0.5f, 0f), new Vector3(0.02f, H, D), wood, NoBottom);
            kit.Box("BookcaseSide", new Vector3(W * 0.5f - 0.01f, H * 0.5f, 0f), new Vector3(0.02f, H, D), wood, NoBottom);
            kit.Box("BookcaseTop", new Vector3(0f, H - 0.01f, 0f), new Vector3(W, 0.02f, D), wood);
            kit.Box("BookcasePlinth", new Vector3(0f, 0.04f, -0.005f), new Vector3(W - 0.04f, 0.08f, D - 0.01f), wood, NoBottom);
            kit.Box("BookcaseBack", new Vector3(0f, H * 0.5f, -D * 0.5f + 0.005f), new Vector3(W - 0.04f, H - 0.02f, 0.01f), wood);
            var levels = new[] { 0.08f, 0.43f, 0.78f, 1.13f, 1.48f };
            for (var i = 1; i < levels.Length; i++)
                kit.Box("BookcaseShelf", new Vector3(0f, levels[i] - 0.01f, 0f), new Vector3(W - 0.04f, 0.02f, D - 0.01f), wood);
            for (var i = 0; i < levels.Length; i++)
            {
                var ceiling = i + 1 < levels.Length ? levels[i + 1] - 0.02f : H - 0.02f;
                FillShelf(kit, levels[i], ceiling - levels[i], -W * 0.5f + 0.022f, W * 0.5f - 0.022f, D * 0.5f - 0.012f, i);
            }
        }

        /// <summary>棚一段に本を詰める。floor は棚の上面、room は上の棚までの高さ、front は本の前を揃える所</summary>
        static void FillShelf(FurnitureKit kit, float floor, float room, float left, float right, float front, int level)
        {
            var x = left;
            var n = 0;
            while (x < right - 0.015f)
            {
                var pick = Hash(level * 31 + n, 7);
                var space = right - x;
                if (pick < 0.12f && space > 0.2f && n > 0)
                {
                    // 寝かせて積んだ本
                    var count = 2 + Mathf.FloorToInt(Hash(level, n + 3) * 4f);
                    var y = floor;
                    var wide = 0.17f + 0.07f * Hash(n, level + 5);
                    for (var k = 0; k < count; k++)
                    {
                        var t = 0.022f + 0.02f * Hash(k, n + level * 9);
                        var h = wide - 0.02f * Hash(k + 4, n);
                        var d = 0.14f + 0.06f * Hash(k + 8, n);
                        var spine = Mathf.FloorToInt(Hash(k + n * 5, level + 11) * 32f);
                        // 背を前へ向けたまま寝かせる（高さの軸を横へ）
                        var turn = R(0f, (Hash(k, n + 20) - 0.5f) * 10f, 90f);
                        Book(kit, new Vector3(x + h * 0.5f + 0.005f, y + t * 0.5f, front - d * 0.5f - 0.01f * Hash(k, 2)), turn, new Vector3(t, h, d), spine);
                        y += t;
                    }
                    x += wide + 0.02f;
                }
                else if (pick < 0.22f && space > 0.12f && level >= 1 && level <= 3)
                {
                    // 空のチップケースの列
                    var count = 3 + Mathf.FloorToInt(Hash(level, n + 13) * 4f);
                    for (var k = 0; k < count && x < right - 0.016f; k++)
                    {
                        ChipCase(kit, new Vector3(x + 0.007f, floor, front - 0.045f), R(0f, 0f, 0f), (level + k) % 6, true);
                        x += 0.014f;
                    }
                    x += 0.01f;
                }
                else if (pick < 0.30f && space > 0.08f)
                {
                    x += 0.03f + 0.05f * Hash(n, level + 17);
                }
                else
                {
                    // 立てた本の並び。終わりの一冊は右へ倒れかかる
                    var run = 2 + Mathf.FloorToInt(Hash(n, level + 19) * 6f);
                    for (var k = 0; k < run && x < right - 0.02f; k++)
                    {
                        var t = Mathf.Min(right - x - 0.002f, 0.016f + 0.034f * Hash(k * 3 + n, level + 23));
                        if (t < 0.012f) break;
                        var h = Mathf.Min(room - 0.01f, 0.17f + 0.11f * Hash(k + n * 7, level + 29));
                        var d = 0.13f + 0.08f * Hash(k + 11, n + level);
                        var spine = Mathf.FloorToInt(Hash(k + n * 3, level * 5 + 31) * 32f);
                        var lean = k == run - 1 && Hash(n, level + 37) > 0.55f && right - x > 0.08f;
                        if (lean)
                        {
                            var angle = 14f + 10f * Hash(n, level + 41);
                            var shift = Mathf.Sin(angle * Mathf.Deg2Rad) * h * 0.5f;
                            Book(kit, new Vector3(x + t * 0.5f + shift, floor + h * 0.5f * Mathf.Cos(angle * Mathf.Deg2Rad) + t * 0.5f * Mathf.Sin(angle * Mathf.Deg2Rad), front - d * 0.5f), R(0f, 0f, -angle), new Vector3(t, h, d), spine);
                            x += t + 2f * shift + 0.01f;
                        }
                        else
                        {
                            Book(kit, new Vector3(x + t * 0.5f, floor + h * 0.5f, front - d * 0.5f - 0.012f * Hash(k, n + 43)), Quaternion.identity, new Vector3(t, h, d), spine);
                            x += t + 0.0015f;
                        }
                    }
                }
                n++;
                if (n > 60) break;
            }
        }

        /// <summary>本一冊。size は (厚み, 高さ, 奥行き)。前（+z）が背表紙、左右が表紙の色、上下が頁の小口</summary>
        static void Book(FurnitureKit kit, Vector3 centre, Quaternion rot, Vector3 size, int spine)
        {
            var cover = SpineCover(spine);
            var pages = Whole(Uv(PagesArea));
            kit.Box6("Book", centre, rot, size, new[] { cover, cover, pages, pages, pages, Whole(SpineUv(spine)) });
        }

        /// <summary>空のメモリチップのケース（煙色の薄い箱、背に札）。standing なら背を前へ立てる。x は左の面、y は底</summary>
        static void ChipCase(FurnitureKit kit, Vector3 at, Quaternion rot, int label, bool standing)
        {
            var smoke = Sw(Hue.CaseSmoke);
            var spine = Whole(Uv(CaseSpineArea, (label % 6) * 4, 0, 4, 12));
            if (standing)
                kit.Box6("ChipCase", at + rot * new Vector3(0f, 0.05f, 0f), rot, new Vector3(0.012f, 0.10f, 0.08f), new[] { smoke, smoke, smoke, smoke, smoke, spine });
            else
                kit.Box6("ChipCase", at + rot * new Vector3(0f, 0.006f, 0f), rot, new Vector3(0.08f, 0.012f, 0.10f),
                    new[] { smoke, smoke, smoke, Whole(Uv(ChipLabelArea, (label % 6) * 8, 0, 8, 6)), smoke, smoke });
        }

        // ---- チップの在庫棚 ----------------------------------------------------------------

        /// <summary>
        /// 空のメモリチップの在庫棚（幅 0.50・奥行き 0.28・高さ 1.20 の鋼の棚、四段）。下から: 札を貼った段ボール二箱（未使用の在庫）、
        /// 札の付いた小さな引き出しの箱、背に札を貼って立てたケースの列、寝かせて積んだケースの山と、ばらのチップを入れた浅い箱
        /// </summary>
        static void ChipShelf(FurnitureKit kit)
        {
            const float W = 0.50f, D = 0.28f, H = 1.20f;
            var steel = Sw(Hue.SteelDark);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("ShelfPost", new Vector3(sx * (W * 0.5f - 0.0125f), H * 0.5f, sz * (D * 0.5f - 0.0125f)), new Vector3(0.025f, H, 0.025f), steel, NoBottom);
            var levels = new[] { 0.06f, 0.38f, 0.70f, 1.02f, 1.19f };
            foreach (var y in levels)
                kit.Box("ShelfBoard", new Vector3(0f, y - 0.0075f, 0f), new Vector3(W - 0.01f, 0.015f, D - 0.01f), steel);
            var card = Sw(Hue.Cardboard);
            var boxFront = Whole(Uv(BoxLabelArea));
            // 一段目: 段ボール二箱
            kit.Box6("StockBox", new Vector3(-0.115f, levels[0] + 0.1f, 0.0f), R(0f, 3f, 0f), new Vector3(0.21f, 0.20f, 0.24f), new[] { card, card, card, card, card, boxFront });
            kit.Box6("StockBox", new Vector3(0.115f, levels[0] + 0.09f, 0.01f), R(0f, -4f, 0f), new Vector3(0.20f, 0.18f, 0.23f), new[] { card, card, card, card, card, boxFront });
            // 二段目: 引き出しの箱（4 × 4）
            var dark = Sw(Hue.PlasticDark);
            kit.Box("DrawerCase", new Vector3(0f, levels[1] + 0.12f, -0.01f), new Vector3(0.40f, 0.24f, 0.22f), dark);
            for (var i = 0; i < 4; i++)
                for (var j = 0; j < 4; j++)
                {
                    var cx = -0.15f + i * 0.1f;
                    var cy = levels[1] + 0.03f + j * 0.06f;
                    kit.Decal("DrawerFront", new Vector3(cx, cy, 0.1005f), new Vector3(-0.045f, 0f, 0f), new Vector3(0f, 0.027f, 0f), Uv(DrawerLabelArea, ((i + j) % 4) * 6, 0, 6, 4));
                }
            // 三段目: 立てたケースの列
            var x = -W * 0.5f + 0.03f;
            for (var k = 0; k < 28 && x < W * 0.5f - 0.03f; k++)
            {
                ChipCase(kit, new Vector3(x + 0.006f, levels[2], 0.05f), Quaternion.identity, k, true);
                x += 0.0145f;
                if (k % 9 == 8) x += 0.012f;
            }
            // 四段目: 寝かせたケースの山と、ばらのチップの浅い箱
            for (var s = 0; s < 2; s++)
                for (var k = 0; k < 4 + s * 2; k++)
                    ChipCase(kit, new Vector3(-0.14f + s * 0.11f, levels[3] + k * 0.012f, 0.02f), R(0f, (Hash(k, s) - 0.5f) * 12f, 0f), k + s, false);
            kit.Box("ChipTray", new Vector3(0.14f, levels[3] + 0.015f, 0.0f), new Vector3(0.16f, 0.03f, 0.12f), dark);
            for (var k = 0; k < 7; k++)
                kit.Box("LooseChip", new Vector3(0.09f + Hash(k, 60) * 0.1f, levels[3] + 0.031f + k * 0.0015f, -0.04f + Hash(k, 61) * 0.08f), R(0f, Hash(k, 62) * 180f, 0f), new Vector3(0.03f, 0.003f, 0.022f),
                    k % 3 == 0 ? Sw(Hue.CaseBlue) : Sw(Hue.PlasticGrey));
        }

        // ---- サーバーラック --------------------------------------------------------------

        /// <summary>
        /// サーバーラック（幅 0.55・奥行き 0.60・高さ 1.25、キャスター付き）。黒い柱と枠、孔のある側板、前に並ぶ機械
        /// （下から UPS・目隠し・ストレージ・サーバ二台・1U・パッチパネル・スイッチ）、スイッチの口から右の柱に沿って床へ垂れて後ろへ抜けるケーブルの束。
        /// 点滅する灯りの小さな面は leds へ（点滅の絵の列を引く）
        /// </summary>
        static void ServerRack(FurnitureKit kit, FurnitureKit leds)
        {
            const float W = 0.55f, D = 0.60f, H = 1.25f;
            var black = Sw(Hue.RackBlack);
            const float lift = 0.06f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    kit.Box("RackPost", new Vector3(sx * (W * 0.5f - 0.02f), lift + (H - lift) * 0.5f, sz * (D * 0.5f - 0.02f)), new Vector3(0.04f, H - lift, 0.04f), black, NoBottom);
                    kit.Lathe("RackCaster", new Vector3(sx * (W * 0.5f - 0.05f), 0.03f, sz * (D * 0.5f - 0.05f)), R(0f, 0f, 90f), new[] { new Vector2(0.028f, -0.012f), new Vector2(0.03f, 0f), new Vector2(0.028f, 0.012f) }, 8, Sw(Hue.Rubber), true, true);
                }
            kit.Box("RackTop", new Vector3(0f, H - 0.02f, 0f), new Vector3(W, 0.04f, D), black);
            // 前の柱の内の、機械を留める孔の並んだ板
            foreach (var sx in new[] { -1f, 1f })
                kit.Box("RackRail", new Vector3(sx * 0.235f, (lift + H) * 0.5f, 0.2665f), new Vector3(0.022f, H - lift - 0.08f, 0.006f), Sw(Hue.SteelDark));
            kit.Box("RackBottom", new Vector3(0f, lift + 0.02f, 0f), new Vector3(W, 0.04f, D), black);
            var vent = Whole(Uv(RackArea, 0, 48, 64, 16));
            foreach (var sx in new[] { -1f, 1f })
                kit.Box6("RackSide", new Vector3(sx * (W * 0.5f - 0.005f), (lift + H) * 0.5f, 0f), Quaternion.identity, new Vector3(0.01f, H - lift - 0.08f, D - 0.08f),
                    new[] { vent, vent, black, black, black, black });
            // 機械（前の面 z 0.265。U = 0.0445 m）
            const float U = 0.0445f;
            const float faceZ = 0.265f;
            var units = new[]
            {
                new { Name = "Ups", U = 2, Y = 0, Deep = 0.45f },
                new { Name = "Blank", U = 1, Y = 36, Deep = 0.05f },
                new { Name = "Storage", U = 2, Y = 40, Deep = 0.50f },
                new { Name = "ServerA", U = 2, Y = 8, Deep = 0.52f },
                new { Name = "ServerB", U = 2, Y = 16, Deep = 0.52f },
                new { Name = "Server1U", U = 1, Y = 24, Deep = 0.50f },
                new { Name = "Gap", U = 1, Y = -1, Deep = 0f },
                new { Name = "Patch", U = 1, Y = 32, Deep = 0.12f },
                new { Name = "Switch", U = 1, Y = 28, Deep = 0.30f },
                new { Name = "Gap", U = 2, Y = -1, Deep = 0f },
                new { Name = "ServerA", U = 2, Y = 8, Deep = 0.52f },
            };
            var y = lift + 0.045f;
            var switchY = 0f;
            var patchY = 0f;
            foreach (var u in units)
            {
                var high = u.U * U;
                if (u.Y >= 0)
                {
                    var face = Whole(Uv(RackArea, 0, u.Y, 64, u.U == 2 ? 8 : 4));
                    kit.Box6("Rack" + u.Name, new Vector3(0f, y + high * 0.5f, faceZ - u.Deep * 0.5f), Quaternion.identity, new Vector3(0.48f, high - 0.002f, u.Deep),
                        new[] { black, black, black, black, black, face });
                    // 耳（前の柱へ留める板）
                    kit.Box("RackEar", new Vector3(-0.235f, y + high * 0.5f, faceZ + 0.002f), new Vector3(0.03f, high - 0.004f, 0.004f), Sw(Hue.SteelDark));
                    kit.Box("RackEar", new Vector3(0.235f, y + high * 0.5f, faceZ + 0.002f), new Vector3(0.03f, high - 0.004f, 0.004f), Sw(Hue.SteelDark));
                    Blinkers(leds, u.Name, y, high, faceZ + 0.0015f);
                }
                if (u.Name == "Switch") switchY = y + high * 0.5f;
                if (u.Name == "Patch") patchY = y + high * 0.5f;
                y += high;
            }
            // ケーブルの束。スイッチとパッチパネルの口から前へ出て、右の柱の前を床へ降り、後ろへ抜ける
            var colours = new[] { Hue.CableBlue, Hue.CableYellow, Hue.CableGrey, Hue.CableBlue, Hue.CableBlack, Hue.CableRed, Hue.CableBlue };
            for (var k = 0; k < colours.Length; k++)
            {
                var sx = -0.17f + k * 0.045f;
                var sy = k % 2 == 0 ? switchY : patchY;
                var bundleX = 0.215f + (k % 3) * 0.008f;
                var bundleZ = faceZ + 0.045f + (k / 3) * 0.008f;
                var pts = new List<Vector3>
                {
                    new Vector3(sx, sy, faceZ + 0.005f),
                    new Vector3(sx, sy - 0.005f, faceZ + 0.03f),
                    new Vector3(Mathf.Lerp(sx, bundleX, 0.6f), sy - 0.05f - k * 0.004f, bundleZ),
                    new Vector3(bundleX, sy - 0.12f, bundleZ),
                    new Vector3(bundleX, (sy + lift) * 0.5f, bundleZ + 0.004f * Mathf.Sin(k)),
                    new Vector3(bundleX, 0.05f, bundleZ),
                    new Vector3(bundleX + 0.01f, 0.006f, bundleZ - 0.04f),
                    new Vector3(W * 0.5f + 0.03f, 0.006f, 0.05f - k * 0.01f),
                    new Vector3(W * 0.5f + 0.06f, 0.006f, -D * 0.5f - 0.05f),
                };
                kit.Tube("RackCable", Smooth(pts, 3), 0.0035f, 4, Sw(colours[k]));
            }
            // 束ねる帯
            foreach (var by in new[] { 0.3f, 0.6f })
                kit.Box("CableTie", new Vector3(0.223f, by, faceZ + 0.049f), new Vector3(0.032f, 0.012f, 0.022f), Sw(Hue.Black));
        }

        /// <summary>機械ごとの点滅する灯り（点滅の絵の列 0〜15 を割り当てる）</summary>
        static void Blinkers(FurnitureKit leds, string unit, float y, float high, float z)
        {
            var spots = new List<Vector3>();
            switch (unit)
            {
                case "Switch":
                    for (var k = 0; k < 12; k++) spots.Add(new Vector3(0.19f - (4 + k * 4 + 1) * 0.0075f, y + high * 0.7f, k % 6));
                    break;
                case "ServerA":
                    spots.Add(new Vector3(0.20f, y + high * 0.3f, 1));
                    spots.Add(new Vector3(0.20f, y + high * 0.55f, 7));
                    spots.Add(new Vector3(-0.16f, y + high * 0.3f, 3));
                    break;
                case "ServerB":
                    spots.Add(new Vector3(0.19f, y + high * 0.35f, 2));
                    spots.Add(new Vector3(-0.19f, y + high * 0.6f, 12));
                    break;
                case "Storage":
                    for (var k = 0; k < 6; k++) spots.Add(new Vector3(0.15f - k * 0.05f, y + high * 0.3f, k % 2 == 0 ? 0 : 4));
                    break;
                case "Server1U":
                    spots.Add(new Vector3(-0.2f, y + high * 0.5f, 5));
                    break;
                case "Ups":
                    spots.Add(new Vector3(-0.2f, y + high * 0.6f, 10));
                    spots.Add(new Vector3(-0.18f, y + high * 0.6f, 8));
                    break;
            }
            foreach (var s in spots)
            {
                var uv = BlinkUv((int)s.z);
                leds.Decal("Led", new Vector3(s.x, s.y, z), new Vector3(-0.0035f, 0f, 0f), new Vector3(0f, 0.0025f, 0f), new Rect(uv, Vector2.zero), false, FurnitureKit.Atlas);
            }
        }

        /// <summary>Catmull-Rom で点の間を刻む。端の点は通る</summary>
        static List<Vector3> Smooth(List<Vector3> pts, int stepsPerSpan)
        {
            var o = new List<Vector3>();
            var n = pts.Count;
            var steps = (n - 1) * stepsPerSpan;
            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps * (n - 1);
                var k = Mathf.Min(Mathf.FloorToInt(t), n - 2);
                var f = t - k;
                var p0 = pts[Mathf.Max(0, k - 1)];
                var p1 = pts[k];
                var p2 = pts[k + 1];
                var p3 = pts[Mathf.Min(n - 1, k + 2)];
                var f2 = f * f;
                var f3 = f2 * f;
                o.Add(0.5f * (2f * p1 + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f2 + (-p0 + 3f * p1 - 3f * p2 + p3) * f3));
            }
            return o;
        }

        // ---- 靴置き ----------------------------------------------------------------------

        /// <summary>靴置き（長さ 0.80・奥行き 0.30・高さ 0.48。黒い鉄の枠に木の桟の三段）。下にスニーカーとブーツ、中に革の短靴</summary>
        static void ShoeRack(FurnitureKit kit)
        {
            const float L = 0.80f, D = 0.30f;
            var metal = Sw(Hue.Black);
            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("ShoeRackLeg", new Vector3(sx * (L * 0.5f - 0.01f), 0.24f, sz * (D * 0.5f - 0.01f)), new Vector3(0.02f, 0.48f, 0.02f), metal, NoBottom);
                foreach (var y in new[] { 0.06f, 0.27f, 0.47f })
                    kit.Box("ShoeRackRail", new Vector3(sx * (L * 0.5f - 0.01f), y, 0f), new Vector3(0.02f, 0.02f, D - 0.02f), metal);
            }
            foreach (var y in new[] { 0.075f, 0.285f, 0.485f })
                for (var k = 0; k < 3; k++)
                    kit.RoundBox("ShoeRackSlat", new Vector3(0f, y, -D * 0.5f + 0.05f + k * 0.1f), Quaternion.identity, new Vector3(L - 0.04f, 0.012f, 0.07f), 0.003f, Oak, null, 1, 0.5f);
            // 靴
            Shoe(kit, new Vector3(-0.28f, 0.081f, 0.0f), 4f, Hue.SneakerWhite, false, 0.27f);
            Shoe(kit, new Vector3(-0.16f, 0.081f, 0.01f), -3f, Hue.SneakerWhite, false, 0.27f);
            Shoe(kit, new Vector3(0.10f, 0.081f, -0.01f), 2f, Hue.LeatherBlack, true, 0.29f);
            Shoe(kit, new Vector3(0.24f, 0.081f, 0.0f), -6f, Hue.LeatherBlack, true, 0.29f);
            Shoe(kit, new Vector3(-0.08f, 0.291f, 0.0f), 3f, Hue.LeatherBrown, false, 0.28f);
            Shoe(kit, new Vector3(0.04f, 0.291f, 0.01f), -2f, Hue.LeatherBrown, false, 0.28f);
        }

        /// <summary>靴の片方（前が +z）。底と、つま先へ低く細る甲。boot なら踵の側を高く</summary>
        static void Shoe(FurnitureKit kit, Vector3 at, float yaw, Hue upper, bool boot, float length)
        {
            using (kit.At(at, yaw))
            {
                kit.RoundBox("ShoeSole", new Vector3(0f, 0.012f, 0f), Quaternion.identity, new Vector3(0.095f, 0.024f, length), 0.01f, Sw(upper == Hue.SneakerWhite ? Hue.SneakerWhite : Hue.Sole), null, 1, 1f, NoBottom);
                var high = boot ? 0.16f : 0.075f;
                kit.RoundBox("ShoeUpper", new Vector3(0f, 0.024f + high * 0.5f, -0.01f), Quaternion.identity, new Vector3(0.09f, high, length - 0.03f), 0.03f, Sw(upper),
                    (p, c) =>
                    {
                        var toe = Mathf.Clamp01((c.z + 0.2f) / 1.2f);
                        if (c.y > -0.5f) p.y -= (c.y + 0.5f) / 1.5f * high * (boot ? 0.72f : 0.5f) * toe * toe;
                        p.x *= 1f - 0.18f * toe * toe;
                        return p;
                    }, 1, 0.06f, NoBottom);
                if (!boot) kit.Decal("ShoeOpening", new Vector3(0f, 0.024f + high + 0.001f, -0.06f), new Vector3(0.02f, 0f, 0f), new Vector3(0f, 0f, 0.04f), new Rect(SwatchUv(Hue.Black), Vector2.zero));
            }
        }

        // ---- 傘立て ----------------------------------------------------------------------

        /// <summary>傘立て（黒い鉄の筒）に、閉じた長い傘（紺。湿った艶）と、帯で束ねた折り畳みの傘（黒）</summary>
        static void UmbrellaStand(FurnitureKit kit)
        {
            var metal = Sw(Hue.SteelDark);
            kit.Lathe("Stand", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.10f, 0f), new Vector2(0.105f, 0.47f), new Vector2(0.108f, 0.48f), new Vector2(0.098f, 0.48f), new Vector2(0.094f, 0.03f), new Vector2(0f, 0.03f) }, 14, metal, true, false);
            Umbrella(kit, new Vector3(0.02f, 0.03f, 0.01f), R(-6f, 0f, 7f), 0.82f, 0.030f, Hue.UmbrellaNavy, true);
            Umbrella(kit, new Vector3(-0.04f, 0.03f, -0.03f), R(5f, 0f, -9f), 0.55f, 0.034f, Hue.UmbrellaBlack, false);
        }

        /// <summary>閉じた傘。石突き、畳んだ布（八つの襞で細る）、帯、柄（長い傘は J の字、折り畳みはまっすぐ）</summary>
        static void Umbrella(FurnitureKit kit, Vector3 at, Quaternion rot, float length, float girth, Hue cloth, bool crook)
        {
            using (kit.At(at, rot))
            {
                var canopyLow = 0.05f;
                var canopyHigh = length - (crook ? 0.16f : 0.12f);
                kit.Lathe("UmbrellaTip", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.004f, 0f), new Vector2(0.006f, canopyLow), new Vector2(girth * 0.5f, canopyLow + 0.02f) }, 6, Sw(Hue.Chrome));
                // 畳んだ布。襞の山と谷を交互に、下から上へ太って、頭で絞る
                kit.Begin("UmbrellaCloth", 40f);
                const int folds = 8;
                var rows = new[] { canopyLow + 0.02f, canopyLow + 0.2f, canopyHigh - 0.25f, canopyHigh - 0.05f, canopyHigh };
                var radius = new[] { girth * 0.5f, girth * 0.9f, girth, girth * 0.8f, girth * 0.35f };
                var uv = SwatchUv(cloth);
                var idx = new int[rows.Length, folds * 2 + 1];
                for (var i = 0; i < rows.Length; i++)
                    for (var s = 0; s <= folds * 2; s++)
                    {
                        var a = (float)s / (folds * 2) * Mathf.PI * 2f + i * 0.08f;
                        var r = radius[i] * (s % 2 == 0 ? 1f : 0.62f);
                        idx[i, s] = kit.V(new Vector3(Mathf.Sin(a) * r, rows[i], Mathf.Cos(a) * r), uv);
                    }
                for (var i = 0; i + 1 < rows.Length; i++)
                    for (var s = 0; s < folds * 2; s++)
                    {
                        var a = (s + 0.5f) / (folds * 2) * Mathf.PI * 2f;
                        kit.QFacing(FurnitureKit.Atlas, idx[i, s], idx[i + 1, s], idx[i + 1, s + 1], idx[i, s + 1], new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
                    }
                kit.End();
                kit.Lathe("UmbrellaStrap", Vector3.zero, Quaternion.identity, new[] { new Vector2(girth * 0.95f, canopyHigh - 0.2f), new Vector2(girth * 0.95f, canopyHigh - 0.17f) }, 8, Sw(cloth));
                kit.Lathe("UmbrellaShaft", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.005f, canopyHigh), new Vector2(0.005f, canopyHigh + 0.03f) }, 6, Sw(Hue.Chrome));
                if (crook)
                    kit.Tube("UmbrellaHandle", Smooth(new List<Vector3> { new Vector3(0f, canopyHigh + 0.02f, 0f), new Vector3(0f, length - 0.03f, 0f), new Vector3(0.03f, length, 0f), new Vector3(0.06f, length - 0.03f, 0f), new Vector3(0.062f, length - 0.07f, 0f) }, 3), 0.011f, 6, Sw(Hue.WoodFoot), true);
                else
                    kit.Lathe("UmbrellaHandle", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.013f, canopyHigh + 0.02f), new Vector2(0.016f, canopyHigh + 0.06f), new Vector2(0.014f, length - 0.01f), new Vector2(0f, length) }, 8, Sw(Hue.Rubber));
            }
        }
    }
}
