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
        /// つや消しの黒の本棚（幅 0.90・奥行き 0.30・高さ 1.85、棚は 5 段）。段ごとに本を詰める: 立てた本の並び（背の色・高さ・厚み・奥行きがばらばら、
        /// 端の一冊は傾く）、寝かせて積んだ本、空のチップケースの列、隙間。背表紙は 32 種の絵から選ぶ。
        /// 本の出入り（高さ・奥行き・傾き）は一冊ずつの箱のまま、見えない面（棚に付く底・奥の背）は張らない。チップケースの列は一つの箱に背の並びの絵
        /// </summary>
        static void Bookcase(FurnitureKit kit)
        {
            const float W = BookcaseWide, D = BookcaseDeep, H = BookcaseHigh;
            var wood = Whole(Uv(LaminateArea));
            kit.Box("BookcaseSide", new Vector3(-W * 0.5f + 0.01f, H * 0.5f, 0f), Quaternion.identity, new Vector3(0.02f, H, D), wood, NoBottom);
            kit.Box("BookcaseSide", new Vector3(W * 0.5f - 0.01f, H * 0.5f, 0f), Quaternion.identity, new Vector3(0.02f, H, D), wood, NoBottom);
            kit.Box("BookcaseTop", new Vector3(0f, H - 0.01f, 0f), Quaternion.identity, new Vector3(W, 0.02f, D), wood, FurnitureKit.Sides.All);
            kit.Box("BookcasePlinth", new Vector3(0f, 0.04f, -0.005f), Quaternion.identity, new Vector3(W - 0.04f, 0.08f, D - 0.01f), wood, Open);
            kit.Box("BookcaseBack", new Vector3(0f, H * 0.5f, -D * 0.5f + 0.005f), Quaternion.identity, new Vector3(W - 0.04f, H - 0.02f, 0.01f), wood, FurnitureKit.Sides.Front);
            var levels = new[] { 0.08f, 0.43f, 0.78f, 1.13f, 1.48f };
            for (var i = 1; i < levels.Length; i++)
                kit.Box("BookcaseShelf", new Vector3(0f, levels[i] - 0.01f, 0f), Quaternion.identity, new Vector3(W - 0.04f, 0.02f, D - 0.01f), wood, (FurnitureKit.Sides)11);
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
                        var spine = Spine(level, n, k);
                        // 背を前へ向けたまま寝かせる（高さの軸を横へ）。下の面（寝かせると −x）と奥は隠れる
                        var turn = R(0f, (Hash(k, n + 20) - 0.5f) * 10f, 90f);
                        Book(kit, new Vector3(x + h * 0.5f + 0.005f, y + t * 0.5f, front - d * 0.5f - 0.01f * Hash(k, 2)), turn, new Vector3(t, h, d), spine, (FurnitureKit.Sides)43);
                        y += t;
                    }
                    x += wide + 0.02f;
                }
                else if (pick < 0.22f && space > 0.12f && level >= 1 && level <= 3)
                {
                    // 空のチップケースの列（一つの箱に、背の並びの絵）
                    var count = 3 + Mathf.FloorToInt(Hash(level, n + 13) * 4f);
                    var fit = Mathf.Min(count, Mathf.FloorToInt((right - 0.016f - x) / 0.014f));
                    if (fit > 0)
                    {
                        CaseRow(kit, new Vector3(x, floor, front - 0.045f), fit, (level * 3 + n) % 8);
                        x += fit * 0.014f;
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
                        var spine = Spine(level, n, k);
                        var lean = k == run - 1 && Hash(n, level + 37) > 0.55f && right - x > 0.08f;
                        if (lean)
                        {
                            var angle = 14f + 10f * Hash(n, level + 41);
                            var shift = Mathf.Sin(angle * Mathf.Deg2Rad) * h * 0.5f;
                            Book(kit, new Vector3(x + t * 0.5f + shift, floor + h * 0.5f * Mathf.Cos(angle * Mathf.Deg2Rad) + t * 0.5f * Mathf.Sin(angle * Mathf.Deg2Rad), front - d * 0.5f), R(0f, 0f, -angle), new Vector3(t, h, d), spine, NoBack);
                            x += t + 2f * shift + 0.01f;
                        }
                        else
                        {
                            Book(kit, new Vector3(x + t * 0.5f, floor + h * 0.5f, front - d * 0.5f - 0.012f * Hash(k, n + 43)), Quaternion.identity, new Vector3(t, h, d), spine, Open);
                            x += t + 0.0015f;
                        }
                    }
                }
                n++;
                if (n > 60) break;
            }
        }

        /// <summary>段ごとの色の組（背表紙の色の組の番号を三つ）。同じ段の中で、塊二つずつ同じ組にまとめて並べる</summary>
        static readonly int[][] ShelfColours =
        {
            new[] { 5, 0, 6 }, new[] { 2, 3, 1 }, new[] { 4, 7, 0 }, new[] { 1, 5, 2 }, new[] { 6, 3, 4 },
        };

        /// <summary>段 level の n 番目の塊の k 冊目の背表紙（オーナー「ある程度の色のまとまりごとに並べて」）。色の組は段と塊で決め、組の中の変わりは乱れで選ぶ</summary>
        static int Spine(int level, int n, int k)
        {
            var row = ShelfColours[level % ShelfColours.Length];
            var family = row[(n / 2) % row.Length];
            return family * SpineVariants + Mathf.FloorToInt(Hash(k + n * 7, level + 13) * SpineVariants) % SpineVariants;
        }

        /// <summary>本一冊。size は (厚み, 高さ, 奥行き)。前（+z）が背表紙、左右が表紙の色、上下が頁の小口。sides は張る面</summary>
        static void Book(FurnitureKit kit, Vector3 centre, Quaternion rot, Vector3 size, int spine, FurnitureKit.Sides sides)
        {
            var cover = SpineCover(spine);
            var pages = Whole(Uv(PagesArea));
            kit.Box6("Book", centre, rot, size, new[] { cover, cover, pages, pages, pages, Whole(SpineUv(spine)) }, sides);
        }

        /// <summary>立てて並べた空のチップケース count 本（一つの箱。前の面にケースの背の並び、上は煙色）。at は左の端の底の前寄り</summary>
        static void CaseRow(FurnitureKit kit, Vector3 at, int count, int first)
        {
            var smoke = Sw(Hue.CaseSmoke);
            var w = count * 0.014f;
            var k0 = Mathf.Clamp(first, 0, CaseRowArea.width / 4 - count);
            var face = Whole(Uv(CaseRowArea, k0 * 4, 0, count * 4, 12));
            kit.Box6("ChipCase", at + new Vector3(w * 0.5f, 0.05f, 0f), Quaternion.identity, new Vector3(w - 0.0015f, 0.10f, 0.08f), new[] { smoke, smoke, smoke, smoke, smoke, face }, Open);
        }

        /// <summary>寝かせて積んだ空のチップケース count 枚（一つの箱。前と脇にケースの縁の段の絵、上に札）。at は底の真ん中</summary>
        static void CaseStack(FurnitureKit kit, Vector3 at, float yaw, int count, int label)
        {
            var high = count * 0.012f;
            var edge = Whole(Uv(CaseStackArea, 0, 0, 16, Mathf.Min(12, count * 2)));
            var top = Whole(Uv(ChipLabelArea, (label % 6) * 8, 0, 8, 6));
            kit.Box6("ChipCase", at + Vector3.up * (high * 0.5f), R(0f, yaw, 0f), new Vector3(0.08f, high, 0.10f), new[] { edge, edge, edge, top, edge, edge }, NoBottom);
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
                    kit.Box("ShelfPost", new Vector3(sx * (W * 0.5f - 0.0125f), H * 0.5f, sz * (D * 0.5f - 0.0125f)), Quaternion.identity, new Vector3(0.025f, H, 0.025f), steel, NoBottom);
            var levels = new[] { 0.06f, 0.38f, 0.70f, 1.02f, 1.19f };
            foreach (var y in levels)
                kit.Box("ShelfBoard", new Vector3(0f, y - 0.0075f, 0f), Quaternion.identity, new Vector3(W - 0.01f, 0.015f, D - 0.01f), steel, FurnitureKit.Sides.All);
            var card = Sw(Hue.Cardboard);
            var boxFront = Whole(Uv(BoxLabelArea));
            // 一段目: 段ボール二箱
            kit.Box6("StockBox", new Vector3(-0.115f, levels[0] + 0.1f, 0.0f), R(0f, 3f, 0f), new Vector3(0.21f, 0.20f, 0.24f), new[] { card, card, card, card, card, boxFront }, NoBottom);
            kit.Box6("StockBox", new Vector3(0.115f, levels[0] + 0.09f, 0.01f), R(0f, -4f, 0f), new Vector3(0.20f, 0.18f, 0.23f), new[] { card, card, card, card, card, boxFront }, NoBottom);
            // 二段目: 引き出しの箱（4 × 4）
            var dark = Sw(Hue.PlasticDark);
            kit.Box("DrawerCase", new Vector3(0f, levels[1] + 0.12f, -0.01f), Quaternion.identity, new Vector3(0.40f, 0.24f, 0.22f), dark, NoBottom);
            for (var i = 0; i < 4; i++)
                for (var j = 0; j < 4; j++)
                {
                    var cx = -0.15f + i * 0.1f;
                    var cy = levels[1] + 0.03f + j * 0.06f;
                    kit.Decal("DrawerFront", new Vector3(cx, cy, 0.1005f), new Vector3(-0.045f, 0f, 0f), new Vector3(0f, 0.027f, 0f), Uv(DrawerLabelArea, ((i + j) % 4) * 6, 0, 6, 4));
                }
            // 三段目: 立てたケースの列（9 本ずつ三つの塊）
            var x = -W * 0.5f + 0.03f;
            for (var g = 0; g < 3; g++)
            {
                var count = g == 2 ? Mathf.Min(9, Mathf.FloorToInt((W * 0.5f - 0.03f - x) / 0.0145f)) : 9;
                if (count <= 0) break;
                CaseRow(kit, new Vector3(x, levels[2], 0.05f), count, g * 4);
                x += count * 0.0145f + 0.012f;
            }
            // 四段目: 寝かせたケースの山と、ばらのチップの浅い箱（チップは箱の上の絵）
            CaseStack(kit, new Vector3(-0.14f + 0.04f, levels[3], 0.07f), 3f, 4, 0);
            CaseStack(kit, new Vector3(-0.03f + 0.04f, levels[3], 0.07f), -5f, 6, 1);
            kit.Box6("ChipTray", new Vector3(0.14f, levels[3] + 0.015f, 0.0f), Quaternion.identity, new Vector3(0.16f, 0.03f, 0.12f), new[] { dark, dark, dark, Whole(Uv(ChipTrayArea)), dark, dark }, NoBottom);
        }

        // ---- サーバーラック --------------------------------------------------------------

        /// <summary>
        /// サーバーラック（幅 0.55・奥行き 0.60・高さ 1.25、キャスター付き）。黒い柱と枠、孔のある側板、前に並ぶ機械
        /// （下から UPS・目隠し・ストレージ・サーバ二台・1U・パッチパネル・スイッチ）、スイッチの口から右の柱に沿って床へ垂れて後ろへ抜けるケーブルの束。
        /// 機械の箱の段（前の面の出入り）は mesh、前の面の細かい機器と柱へ留める耳は絵。点滅する灯りの小さな面は leds へ（点滅の絵の列を引く）
        /// </summary>
        static void ServerRack(FurnitureKit kit, FurnitureKit leds)
        {
            const float W = 0.55f, D = 0.60f, H = 1.25f;
            var black = Sw(Hue.RackBlack);
            const float lift = 0.06f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    kit.Box("RackPost", new Vector3(sx * (W * 0.5f - 0.02f), lift + (H - lift) * 0.5f, sz * (D * 0.5f - 0.02f)), Quaternion.identity, new Vector3(0.04f, H - lift, 0.04f), black, NoBottom);
                    kit.Box("RackCaster", new Vector3(sx * (W * 0.5f - 0.05f), 0.03f, sz * (D * 0.5f - 0.05f)), Quaternion.identity, new Vector3(0.024f, 0.06f, 0.06f), Sw(Hue.Rubber), NoBottom);
                }
            kit.Box("RackTop", new Vector3(0f, H - 0.02f, 0f), Quaternion.identity, new Vector3(W, 0.04f, D), black, FurnitureKit.Sides.All);
            // 前の柱の内の、機械を留める孔の並んだ板
            foreach (var sx in new[] { -1f, 1f })
                kit.Box("RackRail", new Vector3(sx * 0.235f, (lift + H) * 0.5f, 0.2665f), Quaternion.identity, new Vector3(0.022f, H - lift - 0.08f, 0.006f), Sw(Hue.SteelDark), FurnitureKit.Sides.Front);
            kit.Box("RackBottom", new Vector3(0f, lift + 0.02f, 0f), Quaternion.identity, new Vector3(W, 0.04f, D), black, FurnitureKit.Sides.All);
            var vent = Whole(Uv(RackArea, 0, 48, 64, 16));
            foreach (var sx in new[] { -1f, 1f })
                kit.Box6("RackSide", new Vector3(sx * (W * 0.5f - 0.005f), (lift + H) * 0.5f, 0f), Quaternion.identity, new Vector3(0.01f, H - lift - 0.08f, D - 0.08f),
                    new[] { vent, vent, black, black, black, black }, FurnitureKit.Sides.Left | FurnitureKit.Sides.Right);
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
                        new[] { black, black, black, black, black, face }, (FurnitureKit.Sides)11);
                    Blinkers(leds, u.Name, y, high, faceZ + 0.0015f);
                }
                if (u.Name == "Switch") switchY = y + high * 0.5f;
                if (u.Name == "Patch") patchY = y + high * 0.5f;
                y += high;
            }
            // 上の段の棚板。閉じた古いノートと、外付けの箱（灯り一つ）
            var shelfY = y + U;
            kit.Box("RackShelf", new Vector3(0f, shelfY + 0.005f, faceZ - 0.2f), Quaternion.identity, new Vector3(0.48f, 0.01f, 0.40f), black, (FurnitureKit.Sides)11);
            kit.Box("RackLaptop", new Vector3(-0.06f, shelfY + 0.022f, faceZ - 0.16f), R(0f, 6f, 0f), new Vector3(0.33f, 0.024f, 0.23f), Sw(Hue.PlasticGrey), NoBottom);
            kit.Box("RackDrive", new Vector3(0.17f, shelfY + 0.045f, faceZ - 0.12f), R(0f, -4f, 0f), new Vector3(0.07f, 0.07f, 0.16f), Sw(Hue.PlasticDark), NoBottom);
            leds.Decal("Led", new Vector3(0.17f + 0.02f, shelfY + 0.06f, faceZ - 0.12f + 0.0805f), new Vector3(-0.0035f, 0f, 0f), new Vector3(0f, 0.0025f, 0f), new Rect(BlinkUv(9), Vector2.zero));
            // ケーブルの束。スイッチとパッチパネルの口から前へ出て、右の柱の前を床へ降り、後ろへ抜ける（一本ずつの色の線。断面は三角）
            var colours = new[] { Hue.CableBlue, Hue.CableYellow, Hue.CableGrey, Hue.CableBlue, Hue.CableBlack, Hue.CableRed, Hue.CableBlue };
            for (var k = 0; k < colours.Length; k++)
            {
                var sx = -0.17f + k * 0.045f;
                var sy = k % 2 == 0 ? switchY : patchY;
                var bundleX = 0.215f + (k % 3) * 0.008f;
                var bundleZ = faceZ + 0.045f + (k / 3) * 0.008f;
                kit.Tube("RackCable", new[]
                {
                    new Vector3(sx, sy, faceZ + 0.005f),
                    new Vector3(sx, sy - 0.005f, faceZ + 0.03f),
                    new Vector3(Mathf.Lerp(sx, bundleX, 0.6f), sy - 0.05f - k * 0.004f, bundleZ),
                    new Vector3(bundleX, sy - 0.12f, bundleZ),
                    new Vector3(bundleX, (sy + lift) * 0.5f, bundleZ + 0.004f * Mathf.Sin(k)),
                    new Vector3(bundleX, 0.05f, bundleZ),
                    new Vector3(bundleX + 0.01f, 0.006f, bundleZ - 0.04f),
                    new Vector3(W * 0.5f + 0.03f, 0.006f, 0.05f - k * 0.01f),
                    new Vector3(W * 0.5f + 0.06f, 0.006f, -D * 0.5f + 0.02f),
                }, 0.0035f, 3, Sw(colours[k]));
            }
            // 束ねる帯
            foreach (var by in new[] { 0.3f, 0.6f })
                kit.Box("CableTie", new Vector3(0.223f, by, faceZ + 0.049f), Quaternion.identity, new Vector3(0.032f, 0.012f, 0.022f), Sw(Hue.Black), (FurnitureKit.Sides)56);
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

        /// <summary>靴置き（長さ length・奥行き 0.28・高さ 0.34。黒い鉄の枠に木の桟の二段）。上の段に革の短靴とスニーカー、下の段にブーツ</summary>
        static void ShoeRack(FurnitureKit kit, float length)
        {
            var L = Mathf.Max(0.6f, length);
            const float D = 0.28f, H = 0.34f;
            var metal = Sw(Hue.Black);
            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("ShoeRackLeg", new Vector3(sx * (L * 0.5f - 0.01f), H * 0.5f, sz * (D * 0.5f - 0.01f)), Quaternion.identity, new Vector3(0.02f, H, 0.02f), metal, NoBottom);
                foreach (var y in new[] { 0.06f, H - 0.01f })
                    kit.Box("ShoeRackRail", new Vector3(sx * (L * 0.5f - 0.01f), y, 0f), Quaternion.identity, new Vector3(0.02f, 0.02f, D - 0.02f), metal, FurnitureKit.Sides.All);
            }
            var slat = Whole(Uv(LaminateArea));
            foreach (var y in new[] { 0.075f, H + 0.006f })
                for (var k = 0; k < 3; k++)
                    kit.Box("ShoeRackSlat", new Vector3(0f, y, -D * 0.5f + 0.05f + k * 0.1f), Quaternion.identity, new Vector3(L - 0.04f, 0.012f, 0.07f), slat, FurnitureKit.Sides.All);
            var top = H + 0.012f;
            var s = L / 0.8f;
            Shoe(kit, new Vector3(-0.27f * s, top, 0.0f), 4f, Hue.LeatherBrown, false, 0.27f);
            Shoe(kit, new Vector3(-0.15f * s, top, 0.01f), -3f, Hue.LeatherBrown, false, 0.27f);
            Shoe(kit, new Vector3(0.10f * s, top, -0.01f), 8f, Hue.SneakerWhite, false, 0.26f);
            Shoe(kit, new Vector3(0.23f * s, top, 0.02f), -5f, Hue.SneakerWhite, false, 0.26f);
            Shoe(kit, new Vector3(-0.10f, 0.081f, 0.0f), 2f, Hue.LeatherBlack, true, 0.29f);
            Shoe(kit, new Vector3(0.04f, 0.081f, -0.01f), -6f, Hue.LeatherBlack, true, 0.29f);
        }

        /// <summary>靴の片方（前が +z）。底と、つま先へ低く細る甲（丸めた角。輪郭の丸みを残す）。boot なら踵の側を高く</summary>
        static void Shoe(FurnitureKit kit, Vector3 at, float yaw, Hue upper, bool boot, float length)
        {
            using (kit.At(at, yaw))
            {
                kit.Box("ShoeSole", new Vector3(0f, 0.012f, 0f), Quaternion.identity, new Vector3(0.095f, 0.024f, length), Sw(upper == Hue.SneakerWhite ? Hue.SneakerWhite : Hue.Sole), NoBottom);
                var high = boot ? 0.16f : 0.075f;
                kit.RoundBox("ShoeUpper", new Vector3(0f, 0.024f + high * 0.5f, -0.01f), Quaternion.identity, new Vector3(0.09f, high, length - 0.03f), 0.025f, Sw(upper),
                    (p, c) =>
                    {
                        var toe = Mathf.Clamp01((c.z + 0.2f) / 1.2f);
                        if (c.y > -0.5f) p.y -= (c.y + 0.5f) / 1.5f * high * (boot ? 0.72f : 0.5f) * toe * toe;
                        p.x *= 1f - 0.18f * toe * toe;
                        return p;
                    }, 1, NoStep, NoBottom, 55f);
                if (!boot) kit.Decal("ShoeOpening", new Vector3(0f, 0.024f + high + 0.001f, -0.06f), new Vector3(0.02f, 0f, 0f), new Vector3(0f, 0f, 0.04f), new Rect(SwatchUv(Hue.Black), Vector2.zero));
            }
        }

        // ---- 傘立て ----------------------------------------------------------------------

        /// <summary>傘立て（黒い鉄の筒）に、閉じた長い傘（紺。湿った艶）と、帯で束ねた折り畳みの傘（黒）</summary>
        static void UmbrellaStand(FurnitureKit kit)
        {
            var metal = Sw(Hue.SteelDark);
            kit.Lathe("Stand", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.10f, 0f), new Vector2(0.106f, 0.48f), new Vector2(0.098f, 0.48f), new Vector2(0.094f, 0.10f) }, 8, metal, false, false);
            Umbrella(kit, new Vector3(0.02f, 0.03f, 0.01f), R(-6f, 0f, 7f), 0.82f, 0.030f, Hue.UmbrellaNavy, true);
            Umbrella(kit, new Vector3(-0.04f, 0.03f, -0.03f), R(5f, 0f, -9f), 0.55f, 0.034f, Hue.UmbrellaBlack, false);
        }

        /// <summary>閉じた傘。石突き、畳んだ布（六つの襞で細る）、帯、柄（長い傘は J の字、折り畳みはまっすぐ）</summary>
        static void Umbrella(FurnitureKit kit, Vector3 at, Quaternion rot, float length, float girth, Hue cloth, bool crook)
        {
            using (kit.At(at, rot))
            {
                var canopyLow = 0.05f;
                var canopyHigh = length - (crook ? 0.16f : 0.12f);
                kit.Lathe("UmbrellaTip", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.005f, canopyLow), new Vector2(girth * 0.5f, canopyLow + 0.02f) }, 4, Sw(Hue.Chrome));
                // 畳んだ布。襞の山と谷を交互に、下から上へ太って、頭で絞る
                kit.Begin("UmbrellaCloth", 40f);
                const int folds = 6;
                var rows = new[] { canopyLow + 0.02f, canopyLow + 0.2f, canopyHigh - 0.1f, canopyHigh };
                var radius = new[] { girth * 0.5f, girth * 0.9f, girth * 0.9f, girth * 0.35f };
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
                kit.Lathe("UmbrellaStrap", Vector3.zero, Quaternion.identity, new[] { new Vector2(girth * 0.95f, canopyHigh - 0.2f), new Vector2(girth * 0.95f, canopyHigh - 0.17f) }, 6, Sw(cloth));
                kit.Lathe("UmbrellaShaft", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.005f, canopyHigh), new Vector2(0.005f, canopyHigh + 0.03f) }, 4, Sw(Hue.Chrome));
                if (crook)
                    kit.Tube("UmbrellaHandle", new List<Vector3> { new Vector3(0f, canopyHigh + 0.02f, 0f), new Vector3(0f, length - 0.03f, 0f), new Vector3(0.03f, length, 0f), new Vector3(0.06f, length - 0.03f, 0f), new Vector3(0.062f, length - 0.07f, 0f) }, 0.011f, 4, Sw(Hue.WoodFoot), true);
                else
                    kit.Lathe("UmbrellaHandle", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.013f, canopyHigh + 0.02f), new Vector2(0.016f, canopyHigh + 0.06f), new Vector2(0.014f, length - 0.01f), new Vector2(0f, length) }, 5, Sw(Hue.Rubber));
            }
        }
    }
}
