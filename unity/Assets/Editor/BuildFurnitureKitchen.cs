using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    public static partial class BuildFurniture
    {
        // ---- 冷蔵庫 --------------------------------------------------------------------

        /// <summary>
        /// 上下二枚の扉の冷蔵庫（幅 0.60・奥行き 0.66・高さ 1.78）。ヘアラインの鋼の筐体、上の冷凍と下の冷蔵の扉（継ぎ目 1.5 cm）、
        /// 右の縦の取っ手、左の蝶番、冷凍の扉に温度の表示、下の吸気の格子。扉に磁石で留めたメモとテイクアウトのメニュー
        /// </summary>
        static void Fridge(FurnitureKit kit)
        {
            const float W = 0.60f, H = 1.78f;
            var steel = SteelBrushed;
            kit.RoundBox("FridgeBody", new Vector3(0f, 0.08f + (H - 0.08f) * 0.5f, -0.03f), Quaternion.identity, new Vector3(W, H - 0.08f, 0.60f), 0.015f, steel, null, 1, 0.5f, NoBottom);
            kit.Box("FridgeKick", new Vector3(0f, 0.04f, -0.05f), new Vector3(W - 0.04f, 0.08f, 0.56f), Sw(Hue.SteelDark), NoBottom);
            kit.Decal("FridgeGrille", new Vector3(0f, 0.045f, 0.2305f), new Vector3(-(W - 0.08f) * 0.5f, 0f, 0f), new Vector3(0f, 0.025f, 0f), Uv(GrilleArea));
            // 扉（前の面 z 0.3275）
            const float front = 0.30f;
            kit.RoundBox("FreezerDoor", new Vector3(0f, 1.475f, front), Quaternion.identity, new Vector3(W - 0.005f, 0.60f, 0.055f), 0.012f, Sw(Hue.PianoBlack), null, 1, 1f);
            kit.RoundBox("FridgeDoor", new Vector3(0f, 0.625f, front), Quaternion.identity, new Vector3(W - 0.005f, 1.07f, 0.055f), 0.012f, Sw(Hue.PianoBlack), null, 1, 1f);
            var face = front + 0.0275f;
            // 取っ手（右の縦の棒と、扉へ留める足）
            foreach (var span in new[] { new Vector2(1.22f, 1.50f), new Vector2(0.78f, 1.10f) })
            {
                kit.RoundBox("FridgeHandle", new Vector3(W * 0.5f - 0.045f, (span.x + span.y) * 0.5f, face + 0.032f), Quaternion.identity, new Vector3(0.022f, span.y - span.x, 0.018f), 0.008f, Sw(Hue.Chrome), null, 1, 1f);
                foreach (var y in new[] { span.x + 0.02f, span.y - 0.02f })
                    kit.Box("FridgeHandleFoot", new Vector3(W * 0.5f - 0.045f, y, face + 0.013f), new Vector3(0.016f, 0.02f, 0.026f), Sw(Hue.Chrome));
            }
            // 蝶番
            foreach (var y in new[] { 1.785f, 1.168f, 0.085f })
                kit.Box("FridgeHinge", new Vector3(-W * 0.5f + 0.03f, y, face - 0.02f), new Vector3(0.04f, 0.014f, 0.05f), Sw(Hue.SteelDark));
            // 温度の表示
            kit.Decal("FridgeDisplay", new Vector3(-0.13f, 1.63f, face + 0.0015f), new Vector3(-0.045f, 0f, 0f), new Vector3(0f, 0.0225f, 0f), Uv(FridgeDisplayArea));
            // 磁石のメモとメニュー（少し傾けて貼る）
            Note(kit, new Vector3(-0.09f, 1.02f, face + 0.002f), 0.09f, 0.12f, 4f, Uv(MemoArea), Hue.MagnetRed);
            Note(kit, new Vector3(0.10f, 0.83f, face + 0.0025f), 0.10f, 0.135f, -3f, Uv(MenuArea), Hue.MagnetYellow);
            kit.Lathe("Magnet", new Vector3(0.06f, 1.34f, face), R(90f, 0f, 0f), new[] { new Vector2(0.012f, 0f), new Vector2(0.012f, 0.008f) }, 8, Sw(Hue.MagnetBlue), false, true);
        }

        /// <summary>扉に貼った紙（幅 w・高さ h、tilt 度だけ傾ける）と、上の縁の磁石</summary>
        static void Note(FurnitureKit kit, Vector3 at, float w, float h, float tilt, Rect uv, Hue magnet)
        {
            var rot = Quaternion.Euler(0f, 0f, tilt);
            kit.Decal("Note", at, rot * new Vector3(-w * 0.5f, 0f, 0f), rot * new Vector3(0f, h * 0.5f, 0f), uv);
            kit.Lathe("Magnet", at + rot * new Vector3(0f, h * 0.5f - 0.012f, 0f), R(90f, 0f, 0f), new[] { new Vector2(0.011f, 0f), new Vector2(0.011f, 0.009f) }, 8, Sw(magnet), false, true);
        }

        // ---- 台所 ----------------------------------------------------------------------

        /// <summary>台の高さ（天板の上面）と奥行き</summary>
        const float CounterTop = 0.91f;
        const float KitchenDeep = 0.60f;
        /// <summary>戸と引き出しの前板（つや消しの黒の一枚。繰り返さない）</summary>
        static Tile Front { get { return Whole(Uv(LaminateArea)); } }

        /// <summary>戸と戸の隙間の半分。隙間から暗い箱の前の面が覗いて、戸の割り付けが粗い画面でも読める</summary>
        const float DoorGap = 0.004f;

        /// <summary>箱の面の貼り方。戸の付く面（front なら +z、でなければ −z）だけ暗く、ほかは化粧板（繰り返さない一枚）</summary>
        static Tile[] Carcass(bool front)
        {
            var lam = Whole(Uv(LaminateArea));
            var dark = Sw(Hue.Black);
            return new[] { lam, lam, lam, lam, front ? lam : dark, front ? dark : lam };
        }

        /// <summary>天井の下面（換気扇の煙突の頭）</summary>
        const float Ceiling = RoomPlan.Ceiling;

        /// <summary>
        /// 壁沿いの台所（長さ length、奥行き 0.60。原点は足元の真ん中、前が +z、背が壁）。左（西）から、IH と換気扇のフード、抽斗の列、流し（蛇口と水切りかご）、残りは戸棚。
        /// 化粧板の扉と引き出しに金属の取っ手、木の天板、奥へ引っ込めた台輪。流しと抽斗の上の壁に調味料の棚（二段）、右の上に吊り戸棚。
        /// 天板の上に電気ケトルとコーヒーメーカー、右の端に電子レンジ
        /// </summary>
        static void Kitchen(FurnitureKit kit, float length)
        {
            var L = Mathf.Max(2.0f, length);
            var x0 = -L * 0.5f;
            const float back = -KitchenDeep * 0.5f;
            const float doorFront = 0.27f;
            // 箱と台輪
            kit.Box6("KitchenCarcass", new Vector3(0f, 0.10f + 0.39f, back + 0.28f), Quaternion.identity, new Vector3(L, 0.78f, 0.56f), Carcass(true), NoBottom);
            kit.Box("KitchenPlinth", new Vector3(0f, 0.05f, back + 0.25f), new Vector3(L - 0.01f, 0.10f, 0.50f), Sw(Hue.Black), NoBottom);
            // 区画
            var hob = new Vector2(x0, x0 + 0.60f);
            var drawers = new Vector2(hob.y, hob.y + 0.45f);
            var sink = new Vector2(drawers.y, drawers.y + 0.80f);
            var rest = new Vector2(sink.y, L * 0.5f);
            // 戸と引き出し
            Doors(kit, hob.x, hob.y, 1, true, doorFront);
            DrawerStack(kit, drawers.x, drawers.y, doorFront);
            Doors(kit, sink.x, sink.y, 2, false, doorFront);
            var restDoors = Mathf.Max(1, Mathf.RoundToInt((rest.y - rest.x) / 0.6f));
            if (rest.y - rest.x > 0.2f) Doors(kit, rest.x, rest.y, restDoors, false, doorFront);
            // 天板（流しの口を抜いて四枚）
            var basin = new Vector4(sink.x + 0.06f, sink.x + 0.46f, -0.16f, 0.18f);
            const float topY = CounterTop - 0.015f;
            var top = BlackTop;
            kit.RoundBox("Worktop", new Vector3((x0 + basin.x) * 0.5f, topY, 0.005f), Quaternion.identity, new Vector3(basin.x - x0, 0.03f, KitchenDeep + 0.01f), 0.005f, top, null, 1, 1f);
            kit.RoundBox("Worktop", new Vector3((basin.y + L * 0.5f) * 0.5f, topY, 0.005f), Quaternion.identity, new Vector3(L * 0.5f - basin.y, 0.03f, KitchenDeep + 0.01f), 0.005f, top, null, 1, 1f);
            kit.RoundBox("Worktop", new Vector3((basin.x + basin.y) * 0.5f, topY, (basin.w + 0.31f) * 0.5f), Quaternion.identity, new Vector3(basin.y - basin.x, 0.03f, 0.31f - basin.w), 0.005f, top, null, 1, 1f);
            kit.RoundBox("Worktop", new Vector3((basin.x + basin.y) * 0.5f, topY, (back + basin.z) * 0.5f), Quaternion.identity, new Vector3(basin.y - basin.x, 0.03f, basin.z - back), 0.005f, top, null, 1, 1f);
            Sink(kit, basin, sink);
            Hob(kit, hob);
            // 天板の上の物
            Kettle(kit, new Vector3(drawers.x + 0.13f, CounterTop, -0.12f), 200f);
            CoffeeMaker(kit, new Vector3(drawers.x + 0.33f, CounterTop, -0.14f), 0f);
            if (rest.y - rest.x >= 0.5f) Microwave(kit, new Vector3(rest.y - 0.27f, CounterTop, -0.08f), -4f);
            // 壁の物
            Shelves(kit, drawers.x + 0.02f, sink.y - 0.02f);
            if (rest.y - rest.x >= 0.45f) WallCabinet(kit, rest.x + 0.02f, rest.y);
        }

        /// <summary>戸（count 枚に割る）。handleTop なら取っ手を上の縁に横に（コンロの下）、でなければ戸の合わせ目の側に縦に</summary>
        static void Doors(FurnitureKit kit, float from, float to, int count, bool handleTop, float front)
        {
            var w = (to - from) / count;
            for (var i = 0; i < count; i++)
            {
                var a = from + w * i + DoorGap;
                var b = from + w * (i + 1) - DoorGap;
                var cx = (a + b) * 0.5f;
                if (handleTop)
                {
                    // コンロの下は、上に薄い引き出し一段
                    kit.RoundBox("KitchenDrawerFront", new Vector3(cx, 0.80f, front + 0.009f), Quaternion.identity, new Vector3(b - a, 0.14f, 0.018f), 0.005f, Front, null, 1, 1f);
                    Pull(kit, new Vector3(cx, 0.84f, front + 0.018f), true, 0.14f);
                    kit.RoundBox("KitchenDoor", new Vector3(cx, 0.41f, front + 0.009f), Quaternion.identity, new Vector3(b - a, 0.62f, 0.018f), 0.005f, Front, null, 1, 1f);
                    Pull(kit, new Vector3(cx, 0.66f, front + 0.018f), true, 0.14f);
                    continue;
                }
                kit.RoundBox("KitchenDoor", new Vector3(cx, 0.49f, front + 0.009f), Quaternion.identity, new Vector3(b - a, 0.775f, 0.018f), 0.005f, Front, null, 1, 1f);
                // 取っ手は合わせ目の側（二枚なら内寄り、一枚なら右寄り）
                var hx = count == 2 ? (i == 0 ? b - 0.04f : a + 0.04f) : b - 0.04f;
                Pull(kit, new Vector3(hx, 0.76f, front + 0.018f), false, 0.12f);
            }
        }

        static void DrawerStack(FurnitureKit kit, float from, float to, float front)
        {
            var heights = new[] { 0.155f, 0.25f, 0.365f };
            var y = 0.87f;
            var cx = (from + to) * 0.5f;
            foreach (var h in heights)
            {
                var mid = y - h * 0.5f;
                kit.RoundBox("KitchenDrawerFront", new Vector3(cx, mid, front + 0.009f), Quaternion.identity, new Vector3(to - from - 2f * DoorGap, h - 2f * DoorGap, 0.018f), 0.005f, Front, null, 1, 1f);
                Pull(kit, new Vector3(cx, y - 0.045f, front + 0.018f), true, 0.14f);
                y -= h;
            }
        }

        /// <summary>棒の取っ手（横か縦）。足二本で戸から浮かせる</summary>
        static void Pull(FurnitureKit kit, Vector3 at, bool horizontal, float length)
        {
            var along = horizontal ? Vector3.right : Vector3.up;
            kit.Tube("Pull", new[] { at + along * (-length * 0.5f) + Vector3.forward * 0.022f, at + along * (length * 0.5f) + Vector3.forward * 0.022f }, 0.0055f, 6, Sw(Hue.Chrome), true);
            foreach (var s in new[] { -1f, 1f })
                kit.Tube("PullFoot", new[] { at + along * (s * (length * 0.5f - 0.012f)), at + along * (s * (length * 0.5f - 0.012f)) + Vector3.forward * 0.022f }, 0.004f, 5, Sw(Hue.Chrome));
        }

        /// <summary>流し。鋼の槽（内へ向いた五枚）と縁、排水口、グースネックの蛇口とレバー、右の水切りの板と、かご（皿・マグ・コップ）</summary>
        static void Sink(FurnitureKit kit, Vector4 basin, Vector2 module)
        {
            var steel = Sw(Hue.Steel);
            const float bottom = 0.72f;
            var x0 = basin.x; var x1 = basin.y; var z0 = basin.z; var z1 = basin.w;
            kit.Begin("SinkBasin", 30f);
            Inner(kit, new Vector3(x0, bottom, z0), new Vector3(x1, bottom, z0), new Vector3(x1, bottom, z1), new Vector3(x0, bottom, z1), Vector3.up, steel);
            Inner(kit, new Vector3(x0, bottom, z0), new Vector3(x1, bottom, z0), new Vector3(x1, CounterTop, z0), new Vector3(x0, CounterTop, z0), Vector3.forward, steel);
            Inner(kit, new Vector3(x0, bottom, z1), new Vector3(x1, bottom, z1), new Vector3(x1, CounterTop, z1), new Vector3(x0, CounterTop, z1), Vector3.back, steel);
            Inner(kit, new Vector3(x0, bottom, z0), new Vector3(x0, bottom, z1), new Vector3(x0, CounterTop, z1), new Vector3(x0, CounterTop, z0), Vector3.right, steel);
            Inner(kit, new Vector3(x1, bottom, z0), new Vector3(x1, bottom, z1), new Vector3(x1, CounterTop, z1), new Vector3(x1, CounterTop, z0), Vector3.left, steel);
            kit.End();
            kit.Lathe("SinkDrain", new Vector3((x0 + x1) * 0.5f, bottom + 0.001f, (z0 + z1) * 0.5f), Quaternion.identity, new[] { new Vector2(0.03f, 0f), new Vector2(0f, 0f) }, 10, Sw(Hue.SteelDark));
            // 縁
            const float rim = 0.012f;
            kit.Box("SinkRim", new Vector3((x0 + x1) * 0.5f, CounterTop + 0.002f, z0 - rim * 0.5f), new Vector3(x1 - x0 + 2f * rim, 0.004f, rim), steel, NoBottom);
            kit.Box("SinkRim", new Vector3((x0 + x1) * 0.5f, CounterTop + 0.002f, z1 + rim * 0.5f), new Vector3(x1 - x0 + 2f * rim, 0.004f, rim), steel, NoBottom);
            kit.Box("SinkRim", new Vector3(x0 - rim * 0.5f, CounterTop + 0.002f, (z0 + z1) * 0.5f), new Vector3(rim, 0.004f, z1 - z0), steel, NoBottom);
            kit.Box("SinkRim", new Vector3(x1 + rim * 0.5f, CounterTop + 0.002f, (z0 + z1) * 0.5f), new Vector3(rim, 0.004f, z1 - z0), steel, NoBottom);
            // 蛇口
            var tx = (x0 + x1) * 0.5f;
            var chrome = Sw(Hue.Chrome);
            kit.Lathe("TapBase", new Vector3(tx, CounterTop, -0.225f), Quaternion.identity, new[] { new Vector2(0.026f, 0f), new Vector2(0.024f, 0.012f), new Vector2(0.018f, 0.05f) }, 10, chrome, false, false);
            kit.Tube("TapNeck", new[] { new Vector3(tx, CounterTop + 0.04f, -0.225f), new Vector3(tx, CounterTop + 0.20f, -0.225f), new Vector3(tx, CounterTop + 0.265f, -0.19f), new Vector3(tx, CounterTop + 0.265f, -0.13f), new Vector3(tx, CounterTop + 0.23f, -0.085f), new Vector3(tx, CounterTop + 0.19f, -0.08f) }, 0.011f, 7, chrome, true);
            kit.Tube("TapLever", new[] { new Vector3(tx + 0.02f, CounterTop + 0.045f, -0.225f), new Vector3(tx + 0.07f, CounterTop + 0.06f, -0.215f) }, 0.006f, 5, chrome, true);
            // 水切りの板とかご
            var dx0 = x1 + 0.03f;
            var dx1 = Mathf.Min(module.y - 0.02f, dx0 + 0.30f);
            kit.Box("Drainer", new Vector3((dx0 + dx1) * 0.5f, CounterTop + 0.003f, 0.0f), new Vector3(dx1 - dx0, 0.006f, 0.34f), steel, NoBottom);
            DishRack(kit, new Vector3((dx0 + dx1) * 0.5f, CounterTop + 0.006f, -0.01f), dx1 - dx0 - 0.02f);
        }

        /// <summary>内を向いた四角（槽の面）</summary>
        static void Inner(FurnitureKit kit, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want, Tile tile)
        {
            var uv = tile.Uv.position;
            kit.QFacing(FurnitureKit.Atlas, kit.V(a, uv), kit.V(b, uv), kit.V(c, uv), kit.V(d, uv), want);
        }

        /// <summary>水切りかご（細い線の籠）に、立てた皿三枚・伏せたマグ・コップ</summary>
        static void DishRack(FurnitureKit kit, Vector3 at, float wide)
        {
            var wire = Sw(Hue.WireWhite);
            var hx = wide * 0.5f;
            const float hz = 0.15f, high = 0.11f;
            using (kit.At(at, 0f))
            {
                foreach (var y in new[] { 0.01f, high })
                    kit.Tube("RackWire", new[] { new Vector3(-hx, y, -hz), new Vector3(hx, y, -hz), new Vector3(hx, y, hz), new Vector3(-hx, y, hz), new Vector3(-hx, y, -hz) }, 0.003f, 4, wire);
                for (var i = 0; i <= 4; i++)
                {
                    var x = Mathf.Lerp(-hx, hx, i / 4f);
                    kit.Tube("RackWire", new[] { new Vector3(x, 0.01f, -hz), new Vector3(x, 0f, -hz * 0.5f), new Vector3(x, 0f, hz * 0.5f), new Vector3(x, 0.01f, hz) }, 0.0025f, 4, wire);
                    kit.Tube("RackWire", new[] { new Vector3(x, 0.01f, hz), new Vector3(x, high, hz) }, 0.0025f, 4, wire);
                    kit.Tube("RackWire", new[] { new Vector3(x, 0.01f, -hz), new Vector3(x, high, -hz) }, 0.0025f, 4, wire);
                }
                // 皿（縦に立てて、少し後ろへ倒す）
                for (var i = 0; i < 3; i++)
                {
                    var x = -hx + 0.035f + i * 0.028f;
                    kit.Lathe("Plate", new Vector3(x, 0.115f, -0.02f), R(0f, 0f, 90f) * R(-8f, 0f, 0f), new[] { new Vector2(0f, -0.006f), new Vector2(0.075f, -0.004f), new Vector2(0.11f, 0.008f) }, 10, Sw(Hue.PlateWhite), false, false);
                    kit.Lathe("Plate", new Vector3(x, 0.115f, -0.02f), R(0f, 0f, 90f) * R(-8f, 0f, 0f), new[] { new Vector2(0.11f, 0.008f), new Vector2(0.10f, 0.009f), new Vector2(0f, -0.002f) }, 10, Sw(Hue.PlateWhite), false, false);
                }
                // 伏せたマグとコップ
                using (kit.At(new Vector3(hx - 0.06f, 0.10f, 0.06f), R(180f, 30f, 0f)))
                    kit.Lathe("RackMug", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.034f, 0f), new Vector2(0.038f, 0.004f), new Vector2(0.039f, 0.09f), new Vector2(0.033f, 0.09f) }, 10, Sw(Hue.Ceramic), true, false);
                kit.Lathe("RackGlass", new Vector3(hx - 0.06f, 0.012f, -0.06f), Quaternion.identity, new[] { new Vector2(0.028f, 0f), new Vector2(0.033f, 0.11f), new Vector2(0.03f, 0.11f), new Vector2(0.026f, 0.01f) }, 10, Sw(Hue.GlassClear), true, false);
            }
        }

        /// <summary>IH の天板（黒い硝子に輪の絵）と、上の換気扇のフード（前へ張り出す箱と、天井へ上る煙突。下に網と明かり）</summary>
        static void Hob(FurnitureKit kit, Vector2 module)
        {
            var cx = (module.x + module.y) * 0.5f;
            var glass = Sw(Hue.GlassDark);
            kit.Box6("Hob", new Vector3(cx, CounterTop + 0.003f, 0.0f), Quaternion.identity, new Vector3(0.56f, 0.006f, 0.50f),
                new[] { glass, glass, glass, Whole(Uv(HobArea)), glass, glass }, NoBottom);
            var steel = SteelBrushed;
            const float hoodLow = 1.56f;
            kit.RoundBox("Hood", new Vector3(cx, hoodLow + 0.06f, -0.06f), Quaternion.identity, new Vector3(0.60f, 0.12f, 0.48f), 0.01f, steel, null, 1, 0.5f, FurnitureKit.Sides.All);
            kit.Decal("HoodFilter", new Vector3(cx, hoodLow - 0.001f, -0.06f), new Vector3(0.25f, 0f, 0f), new Vector3(0f, 0f, -0.2f), Uv(FilterArea));
            kit.Box("HoodFlue", new Vector3(cx, (hoodLow + 0.12f + Ceiling) * 0.5f, -0.19f), new Vector3(0.26f, Ceiling - hoodLow - 0.12f, 0.22f), steel, NoBottom);
        }

        /// <summary>電気ケトル（黒い台、細る胴、注ぎ口、取っ手、青く灯る釦）</summary>
        static void Kettle(FurnitureKit kit, Vector3 at, float yaw)
        {
            using (kit.At(at, yaw))
            {
                var body = Sw(Hue.Enamel);
                kit.Lathe("KettleBase", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.085f, 0f), new Vector2(0.088f, 0.018f), new Vector2(0.08f, 0.02f) }, 12, Sw(Hue.PlasticDark), false, true);
                kit.Lathe("Kettle", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.075f, 0.02f), new Vector2(0.078f, 0.05f), new Vector2(0.068f, 0.18f), new Vector2(0.055f, 0.215f), new Vector2(0.04f, 0.225f), new Vector2(0f, 0.23f) }, 12, body);
                kit.Tube("KettleSpout", new[] { new Vector3(0f, 0.17f, 0.06f), new Vector3(0f, 0.19f, 0.09f), new Vector3(0f, 0.205f, 0.105f) }, 0.014f, 6, body, true);
                kit.Tube("KettleHandle", new[] { new Vector3(0f, 0.19f, -0.058f), new Vector3(0f, 0.20f, -0.10f), new Vector3(0f, 0.16f, -0.115f), new Vector3(0f, 0.08f, -0.108f), new Vector3(0f, 0.05f, -0.075f) }, 0.012f, 6, Sw(Hue.PlasticDark), true);
                kit.Decal("KettleSwitch", new Vector3(0f, 0.045f, -0.112f), new Vector3(0.008f, 0f, 0f), new Vector3(0f, 0.008f, 0f), Uv(KettleSwitchArea));
            }
        }

        /// <summary>ドリップ式のコーヒーメーカー（黒い台と塔、頭、硝子のポットに残った珈琲、赤い電源の灯り）</summary>
        static void CoffeeMaker(FurnitureKit kit, Vector3 at, float yaw)
        {
            using (kit.At(at, yaw))
            {
                var black = Sw(Hue.PlasticDark);
                kit.RoundBox("CoffeeBase", new Vector3(0f, 0.02f, 0.02f), Quaternion.identity, new Vector3(0.18f, 0.04f, 0.24f), 0.01f, black, null, 1, 1f, NoBottom);
                kit.RoundBox("CoffeeTower", new Vector3(0f, 0.17f, -0.07f), Quaternion.identity, new Vector3(0.18f, 0.30f, 0.09f), 0.015f, black, null, 1, 1f, NoBottom);
                kit.RoundBox("CoffeeHead", new Vector3(0f, 0.285f, 0.03f), Quaternion.identity, new Vector3(0.18f, 0.07f, 0.13f), 0.02f, black, null, 1, 1f);
                kit.Decal("CoffeeFace", new Vector3(0f, 0.09f, -0.0245f), new Vector3(-0.07f, 0f, 0f), new Vector3(0f, 0.08f, 0f), Uv(CoffeeFaceArea));
                kit.Lathe("CoffeePot", new Vector3(0f, 0.04f, 0.045f), Quaternion.identity, new[] { new Vector2(0.06f, 0f), new Vector2(0.068f, 0.05f), new Vector2(0.055f, 0.12f), new Vector2(0.045f, 0.14f) }, 12, Sw(Hue.GlassCoffee), true, false);
                kit.Lathe("CoffeePotLid", new Vector3(0f, 0.04f, 0.045f), Quaternion.identity, new[] { new Vector2(0.047f, 0.14f), new Vector2(0.047f, 0.155f), new Vector2(0f, 0.16f) }, 10, black);
                kit.Tube("CoffeePotHandle", new[] { new Vector3(0f, 0.16f, 0.105f), new Vector3(0f, 0.16f, 0.14f), new Vector3(0f, 0.08f, 0.14f), new Vector3(0f, 0.07f, 0.11f) }, 0.008f, 5, black);
            }
        }

        /// <summary>電子レンジ（黒い筐体、点の網の窓の戸、右の操作の盤と緑の時計、戸の取っ手）</summary>
        static void Microwave(FurnitureKit kit, Vector3 at, float yaw)
        {
            using (kit.At(at, yaw))
            {
                const float W = 0.46f, H = 0.27f, D = 0.36f;
                kit.RoundBox("MicrowaveBody", new Vector3(0f, H * 0.5f + 0.012f, 0f), Quaternion.identity, new Vector3(W, H, D), 0.012f, Sw(Hue.SteelDark), null, 1, 1f, NoBottom);
                foreach (var sx in new[] { -1f, 1f })
                    foreach (var sz in new[] { -1f, 1f })
                        kit.Box("MicrowaveFoot", new Vector3(sx * (W * 0.5f - 0.04f), 0.006f, sz * (D * 0.5f - 0.04f)), new Vector3(0.03f, 0.012f, 0.03f), Sw(Hue.Rubber), NoBottom);
                var face = D * 0.5f + 0.001f;
                kit.Decal("MicrowaveDoor", new Vector3(-0.055f, H * 0.5f + 0.012f, face), new Vector3(-0.165f, 0f, 0f), new Vector3(0f, 0.12f, 0f), Uv(MicrowaveDoorArea));
                kit.Decal("MicrowavePanel", new Vector3(0.17f, H * 0.5f + 0.012f, face), new Vector3(-0.05f, 0f, 0f), new Vector3(0f, 0.12f, 0f), Uv(MicrowavePanelArea));
                kit.RoundBox("MicrowaveHandle", new Vector3(0.10f, H * 0.5f + 0.012f, face + 0.012f), Quaternion.identity, new Vector3(0.016f, 0.20f, 0.02f), 0.006f, Sw(Hue.Chrome), null, 1, 1f);
            }
        }

        /// <summary>
        /// 調味料の棚（壁に付けた木の板二段と黒い金物）。下の段に香辛料の瓶の列・塩・胡椒の挽き器・茶色のソース・ケチャップ・醤油・油と酢の瓶、
        /// 上の段に紅茶の缶・珈琲の缶・砂糖の瓶・紅茶の箱・香辛料の小袋
        /// </summary>
        static void Shelves(FurnitureKit kit, float from, float to)
        {
            var back = -KitchenDeep * 0.5f;
            const float deep = 0.18f;
            var cx = (from + to) * 0.5f;
            var levels = new[] { 1.30f, 1.62f };
            foreach (var y in levels)
            {
                kit.Box("SpiceShelf", new Vector3(cx, y - 0.0125f, back + deep * 0.5f), new Vector3(to - from, 0.025f, deep), Laminate);
                foreach (var bx in new[] { from + 0.08f, to - 0.08f })
                {
                    kit.Box("ShelfBracket", new Vector3(bx, y - 0.045f, back + 0.006f), new Vector3(0.02f, 0.07f, 0.012f), Sw(Hue.Black));
                    kit.Box("ShelfBracket", new Vector3(bx, y - 0.031f, back + 0.07f), R(-35f, 0f, 0f), new Vector3(0.012f, 0.012f, 0.14f), Sw(Hue.Black));
                }
            }
            var z = back + deep * 0.5f;
            var y0 = levels[0];
            // 下の段: 香辛料の瓶の列（中身の色と札）
            var spices = new[] { Hue.Paprika, Hue.Turmeric, Hue.Herbs, Hue.Cumin, Hue.Pepper, Hue.Paprika };
            var spiceLabels = new[] { 2, 3, 4, 5, 1, 2 };
            var x0 = from + 0.05f;
            for (var i = 0; i < spices.Length; i++)
                Jar(kit, new Vector3(x0 + i * 0.05f, y0, z + 0.03f - (i % 2) * 0.05f), 0.021f, 0.085f, spices[i], Hue.LidBlack, spiceLabels[i]);
            var x = x0 + spices.Length * 0.05f + 0.03f;
            Jar(kit, new Vector3(x, y0, z), 0.034f, 0.12f, Hue.Salt, Hue.LidWhite, 0);
            x += 0.08f;
            // 胡椒の挽き器（背の高い黒い木）
            kit.Lathe("PepperMill", new Vector3(x, y0, z + 0.02f), Quaternion.identity, new[] { new Vector2(0.024f, 0f), new Vector2(0.027f, 0.03f), new Vector2(0.019f, 0.12f), new Vector2(0.024f, 0.15f), new Vector2(0.016f, 0.18f), new Vector2(0.005f, 0.19f), new Vector2(0f, 0.19f) }, 8, Sw(Hue.Walnut), true, false);
            x += 0.06f;
            Bottle(kit, new Vector3(x, y0, z), 0.030f, 0.15f, 0.013f, 0.045f, Hue.BrownSauce, Hue.LidBlack, 6);
            x += 0.07f;
            Bottle(kit, new Vector3(x, y0, z + 0.01f), 0.032f, 0.16f, 0.015f, 0.02f, Hue.Ketchup, Hue.LidRed, 7);
            x += 0.07f;
            Bottle(kit, new Vector3(x, y0, z - 0.02f), 0.024f, 0.13f, 0.009f, 0.04f, Hue.Soy, Hue.LidRed, 8);
            x += 0.06f;
            Bottle(kit, new Vector3(x, y0, z + 0.02f), 0.030f, 0.22f, 0.010f, 0.07f, Hue.Oil, Hue.Cork, 9);
            x += 0.07f;
            if (x < to - 0.04f) Bottle(kit, new Vector3(x, y0, z - 0.02f), 0.027f, 0.19f, 0.010f, 0.06f, Hue.Vinegar, Hue.LidBlack, -1);
            // 上の段
            var y1 = levels[1];
            x = from + 0.07f;
            Tin(kit, new Vector3(x, y1, z), 0.045f, 0.15f, Uv(TeaTinArea), Hue.TinGold);
            x += 0.11f;
            Tin(kit, new Vector3(x, y1, z + 0.01f), 0.05f, 0.13f, Uv(CoffeeTinArea), Hue.Black);
            x += 0.11f;
            Jar(kit, new Vector3(x, y1, z), 0.04f, 0.13f, Hue.Sugar, Hue.LidWhite, -1);
            x += 0.12f;
            var paper = Sw(Hue.Cardboard);
            kit.Box6("TeaBox", new Vector3(x, y1 + 0.06f, z), R(0f, -6f, 0f), new Vector3(0.14f, 0.12f, 0.09f),
                new[] { paper, paper, paper, paper, paper, Whole(Uv(TeaBoxArea)) }, NoBottom);
            x += 0.12f;
            for (var i = 0; i < 3 && x < to - 0.03f; i++, x += 0.045f)
                kit.Box6("Sachet", new Vector3(x, y1 + 0.045f, z + 0.02f), R(-12f, 0f, 4f - i * 5f), new Vector3(0.06f, 0.08f, 0.008f),
                    new[] { Sw(Hue.Steel), Sw(Hue.Steel), Sw(Hue.Steel), Sw(Hue.Steel), Sw(Hue.Steel), Whole(Uv(SachetArea, i * 6, 0, 6, 8)) }, NoBottom);
        }

        /// <summary>硝子の瓶（中身の色）と蓋、札（label が負なら札無し）</summary>
        static void Jar(FurnitureKit kit, Vector3 at, float r, float h, Hue contents, Hue lid, int label)
        {
            kit.Lathe("Jar", at, Quaternion.identity, new[] { new Vector2(r * 0.9f, 0f), new Vector2(r, 0.006f), new Vector2(r, h - 0.02f), new Vector2(r * 0.85f, h - 0.012f) }, 8, Sw(contents), true, false);
            kit.Lathe("JarLid", at, Quaternion.identity, new[] { new Vector2(r * 0.88f, h - 0.014f), new Vector2(r * 0.9f, h), new Vector2(0f, h + 0.002f) }, 8, Sw(lid));
            if (label >= 0)
                kit.Lathe("JarLabel", at, Quaternion.identity, new[] { new Vector2(r + 0.0015f, h * 0.25f), new Vector2(r + 0.0015f, h * 0.7f) }, 8, Whole(Uv(JarLabelArea, label * 8, 0, 8, 8)), false, false, false, 60f, -110f, 220f);
        }

        /// <summary>瓶（胴・肩・首）と蓋、札（label が負なら札無し）</summary>
        static void Bottle(FurnitureKit kit, Vector3 at, float r, float h, float neck, float neckLong, Hue body, Hue cap, int label)
        {
            var shoulder = h - neckLong;
            kit.Lathe("Bottle", at, Quaternion.identity, new[] { new Vector2(r * 0.92f, 0f), new Vector2(r, 0.008f), new Vector2(r, shoulder - 0.02f), new Vector2(neck * 1.2f, shoulder + 0.01f), new Vector2(neck, h - 0.012f) }, 8, Sw(body), true, false);
            kit.Lathe("BottleCap", at, Quaternion.identity, new[] { new Vector2(neck * 1.15f, h - 0.014f), new Vector2(neck * 1.15f, h), new Vector2(0f, h + 0.002f) }, 8, Sw(cap));
            if (label >= 0)
                kit.Lathe("BottleLabel", at, Quaternion.identity, new[] { new Vector2(r + 0.0015f, shoulder * 0.25f), new Vector2(r + 0.0015f, shoulder * 0.8f) }, 8, Whole(Uv(JarLabelArea, label * 8, 0, 8, 8)), false, false, false, 60f, -110f, 220f);
        }

        /// <summary>筒の缶（胴に巻いた絵と、蓋）</summary>
        static void Tin(FurnitureKit kit, Vector3 at, float r, float h, Rect wrap, Hue lid)
        {
            kit.Lathe("Tin", at, Quaternion.identity, new[] { new Vector2(r, 0f), new Vector2(r, h - 0.012f) }, 10, Whole(wrap), false, false);
            kit.Lathe("TinLid", at, Quaternion.identity, new[] { new Vector2(r + 0.002f, h - 0.014f), new Vector2(r + 0.002f, h), new Vector2(0f, h) }, 10, Sw(lid));
        }

        /// <summary>吊り戸棚（化粧板の戸と、下の縁の取っ手）</summary>
        static void WallCabinet(FurnitureKit kit, float from, float to)
        {
            var back = -KitchenDeep * 0.5f;
            const float deep = 0.33f, low = 1.46f, high = 2.16f;
            var cx = (from + to) * 0.5f;
            kit.Box6("WallCabinet", new Vector3(cx, (low + high) * 0.5f, back + deep * 0.5f - 0.009f), Quaternion.identity, new Vector3(to - from, high - low, deep - 0.018f), Carcass(true));
            var count = Mathf.Max(1, Mathf.RoundToInt((to - from) / 0.5f));
            var w = (to - from) / count;
            for (var i = 0; i < count; i++)
            {
                var a = from + w * i + DoorGap;
                var b = from + w * (i + 1) - DoorGap;
                kit.RoundBox("WallCabinetDoor", new Vector3((a + b) * 0.5f, (low + high) * 0.5f, back + deep - 0.009f), Quaternion.identity, new Vector3(b - a, high - low - 2f * DoorGap, 0.018f), 0.005f, Front, null, 1, 1f);
                var hx = count == 1 ? a + 0.04f : (i % 2 == 0 ? b - 0.04f : a + 0.04f);
                Pull(kit, new Vector3(hx, low + 0.08f, back + deep), false, 0.10f);
            }
        }

        // ---- ごみ箱 --------------------------------------------------------------------

        /// <summary>ペダルのごみ箱（鋼の胴、磨いた蓋、黒い台とペダル）</summary>
        static void Bin(FurnitureKit kit)
        {
            kit.Lathe("BinFoot", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.125f, 0f), new Vector2(0.13f, 0.02f), new Vector2(0.128f, 0.025f) }, 14, Sw(Hue.Black));
            kit.Lathe("Bin", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.126f, 0.02f), new Vector2(0.13f, 0.42f) }, 14, SteelBrushed);
            kit.Lathe("BinLid", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.134f, 0.415f), new Vector2(0.135f, 0.43f), new Vector2(0.12f, 0.448f), new Vector2(0.07f, 0.46f), new Vector2(0f, 0.463f) }, 14, Sw(Hue.Chrome));
            kit.RoundBox("BinPedal", new Vector3(0f, 0.025f, 0.14f), R(-10f, 0f, 0f), new Vector3(0.09f, 0.012f, 0.06f), 0.004f, Sw(Hue.Black), null, 1, 1f);
        }

        // ---- 作業台と丸椅子 ------------------------------------------------------------

        /// <summary>
        /// 作業台（長さ length、奥行き 0.60、天板の上面 0.93）。台所の側（−z）に戸と取っ手、丸椅子の側（+z）は天板を 0.15 張り出して膝を入れる。
        /// 天板にまな板と布巾
        /// </summary>
        static void WorkCounter(FurnitureKit kit, float length)
        {
            var L = Mathf.Max(0.8f, length);
            const float top = 0.93f;
            kit.Box6("CounterCarcass", new Vector3(0f, 0.10f + 0.40f, -0.075f), Quaternion.identity, new Vector3(L - 0.02f, 0.80f, 0.45f), Carcass(false), NoBottom);
            kit.Box("CounterPlinth", new Vector3(0f, 0.05f, -0.06f), new Vector3(L - 0.04f, 0.10f, 0.38f), Sw(Hue.Black), NoBottom);
            var count = Mathf.Max(1, Mathf.RoundToInt(L / 0.55f));
            var w = (L - 0.02f) / count;
            for (var i = 0; i < count; i++)
            {
                var cx = -L * 0.5f + 0.01f + w * (i + 0.5f);
                using (kit.At(new Vector3(0f, 0f, -0.30f), 180f))
                {
                    kit.RoundBox("CounterDoor", new Vector3(-cx, 0.49f, 0.009f), Quaternion.identity, new Vector3(w - 2f * DoorGap, 0.775f, 0.018f), 0.005f, Front, null, 1, 1f);
                    Pull(kit, new Vector3(-cx + w * 0.5f - 0.04f, 0.76f, 0.018f), false, 0.12f);
                }
            }
            kit.RoundBox("CounterTop", new Vector3(0f, top - 0.0175f, 0.0f), Quaternion.identity, new Vector3(L, 0.035f, 0.60f), 0.006f, BlackTop, null, 1, 1f);
            // まな板と布巾
            kit.RoundBox("ChoppingBoard", new Vector3(-L * 0.25f, top + 0.009f, -0.05f), R(0f, 8f, 0f), new Vector3(0.38f, 0.018f, 0.26f), 0.006f, Walnut, null, 1, 0.5f);
            kit.RoundBox("TeaTowel", new Vector3(L * 0.2f, top + 0.006f, -0.08f), R(0f, -14f, 0f), new Vector3(0.22f, 0.012f, 0.15f), 0.005f, Cotton,
                (p, c) => { p.y += 0.003f * Mathf.Sin(p.x * 60f) * (c.y > 0f ? 1f : 0f); return p; }, 1, 0.05f, NoBottom);
        }

        /// <summary>丸椅子（木の丸い座、黒い鉄の四本の脚は開き、足掛けの輪）</summary>
        static void Stool(FurnitureKit kit)
        {
            const float seat = 0.66f;
            kit.Lathe("StoolSeat", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.16f, seat - 0.035f), new Vector2(0.172f, seat - 0.02f), new Vector2(0.17f, seat - 0.004f), new Vector2(0.16f, seat), new Vector2(0f, seat + 0.004f) }, 16, BlackTop, true, false);
            var metal = Sw(Hue.Black);
            for (var k = 0; k < 4; k++)
            {
                var a = (45f + k * 90f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                kit.Tube("StoolLeg", new[] { dir * 0.11f + Vector3.up * (seat - 0.035f), dir * 0.20f }, 0.012f, 6, metal, true);
            }
            var ring = new List<Vector3>();
            for (var k = 0; k <= 16; k++)
            {
                var a = k / 16f * Mathf.PI * 2f;
                ring.Add(new Vector3(Mathf.Sin(a) * 0.175f, 0.25f, Mathf.Cos(a) * 0.175f));
            }
            kit.Tube("StoolRing", ring, 0.008f, 5, metal);
        }
    }
}
