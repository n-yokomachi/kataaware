using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    public static partial class BuildFurniture
    {
        // どの形も、床の物は足元の真ん中を原点に、前が +z。壁の物は壁に付く背の真ん中を原点に、前が +z

        static Quaternion R(float x, float y, float z) { return Quaternion.Euler(x, y, z); }

        const FurnitureKit.Sides NoBottom = FurnitureKit.Sides.NoBottom;

        /// <summary>割りの刻み（m）を取らない。面の割りは丸めの帯と絵の繰り返しの境だけ</summary>
        const float NoStep = 100f;

        /// <summary>膨らみ。面の真ん中ほど外へ（a は軸ごとの深さ）</summary>
        static FurnitureKit.Deform Puff(Vector3 a)
        {
            return (p, c) => p + new Vector3(
                c.x * (1f - c.y * c.y) * (1f - c.z * c.z) * a.x,
                c.y * (1f - c.x * c.x) * (1f - c.z * c.z) * a.y,
                c.z * (1f - c.x * c.x) * (1f - c.y * c.y) * a.z);
        }

        /// <summary>上の面の窪み。(x, z) を真ん中に、横 rx・前後 rz の広がりで depth だけ沈める（下の面は動かさない）</summary>
        static Vector3 Dent(Vector3 p, Vector3 c, float x, float z, float rx, float rz, float depth)
        {
            if (c.y <= -0.3f) return p;
            var k = Mathf.Exp(-((p.x - x) * (p.x - x) / (rx * rx) + (p.z - z) * (p.z - z) / (rz * rz)));
            p.y -= depth * k * Mathf.Clamp01((c.y + 0.3f) / 1.3f);
            return p;
        }

        // ---- ソファ --------------------------------------------------------------------

        public const float SofaLong = 2.05f;
        public const float SofaDeep = 0.90f;
        /// <summary>座のクッションの上面（沈む前）</summary>
        const float SofaSeat = 0.47f;

        /// <summary>
        /// 黒い革張りのソファ。黒い短い脚、台、背の枠、丸く膨らむ肘、座のクッション二つ（寝ている体の窪み）、背のクッション二つ（寄りかかった皺）。
        /// 左の肘に枕、右寄りに丸めた毛布（座の前の縁から裾が垂れる）。ソファで寝ている。
        /// クッションと肘の厚みと丸み（輪郭）は mesh の丸め（帯二本）のまま、面の割りは丸めの帯と革の絵の繰り返しの境だけにする（面の中の細かい割りを除いた）。
        /// 台と背の枠の隠れる面（クッションの下・壁の側）は張らない
        /// </summary>
        static void Sofa(FurnitureKit kit)
        {
            const float L = SofaLong, D = SofaDeep;
            var fabric = Fabric;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    kit.Lathe("SofaFoot", new Vector3(sx * (L * 0.5f - 0.09f), 0f, sz * (D * 0.5f - 0.09f)), Quaternion.identity,
                        new[] { new Vector2(0.017f, 0f), new Vector2(0.026f, 0.072f) }, 5, Sw(Hue.WoodFoot), true, false);
            // 台（y 0.07〜0.29）。上はクッション、脇は肘、背は背の枠に隠れるので、前だけ張る
            kit.RoundBox("SofaBase", new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(L - 0.02f, 0.22f, D - 0.04f), 0.03f, FabricBig,
                Puff(new Vector3(0.004f, 0f, 0.008f)), 1, NoStep, FurnitureKit.Sides.Front);
            // 背の枠（y 0.07〜0.87、少し後ろへ倒す。背は壁の側）
            kit.RoundBox("SofaBackFrame", new Vector3(0f, 0.47f, -D * 0.5f + 0.09f), R(-4f, 0f, 0f), new Vector3(L - 0.04f, 0.80f, 0.18f), 0.06f, FabricBig,
                Puff(new Vector3(0.006f, 0.01f, 0.012f)), 1, NoStep, FurnitureKit.Sides.Top | FurnitureKit.Sides.Front | FurnitureKit.Sides.Left | FurnitureKit.Sides.Right);
            // 肘（y 0.07〜0.67）。上の縁を外へ巻く
            foreach (var s in new[] { -1f, 1f })
            {
                var side = s;
                kit.RoundBox("SofaArm", new Vector3(side * (L * 0.5f - 0.105f), 0.37f, 0f), Quaternion.identity, new Vector3(0.21f, 0.60f, D - 0.02f), 0.085f, fabric,
                    (p, c) =>
                    {
                        p = Puff(new Vector3(0.016f, 0.02f, 0.01f))(p, c);
                        if (c.y > 0.5f) p.x += side * 0.014f * (c.y - 0.5f) * 2f * (1f - c.z * c.z);
                        return p;
                    }, 2, NoStep, FurnitureKit.Sides.Top | FurnitureKit.Sides.Front | FurnitureKit.Sides.Left | FurnitureKit.Sides.Right);
            }
            // 座のクッション（y 0.29〜0.47）。寝た体の窪み: 肩（全体の x −0.45）と腰（x 0）
            var cushionW = (L - 0.42f) * 0.5f - 0.004f;
            foreach (var s in new[] { -1f, 1f })
            {
                var cx = s * (cushionW * 0.5f + 0.002f);
                kit.RoundBox("SofaSeatCushion", new Vector3(cx, 0.38f, 0.09f), Quaternion.identity, new Vector3(cushionW, 0.18f, 0.72f), 0.055f, fabric,
                    (p, c) =>
                    {
                        p = Puff(new Vector3(0.010f, 0.022f, 0.012f))(p, c);
                        p = Dent(p, c, -0.45f - cx, 0.04f, 0.20f, 0.22f, 0.020f);
                        p = Dent(p, c, 0.02f - cx, 0.05f, 0.22f, 0.24f, 0.030f);
                        p = Dent(p, c, 0.50f - cx, 0.08f, 0.26f, 0.20f, 0.014f);
                        return p;
                    }, 2, NoStep, NoBottom);
            }
            // 背のクッション（下の後ろの縁を座の後ろに沈め、上を後ろへ 12 度倒す）。真ん中に寄りかかった横の窪み
            foreach (var s in new[] { -1f, 1f })
            {
                var cx = s * (cushionW * 0.5f + 0.002f);
                kit.RoundBox("SofaBackCushion", new Vector3(cx, 0.715f, -0.224f), R(-12f, 0f, 0f), new Vector3(cushionW - 0.01f, 0.50f, 0.20f), 0.07f, fabric,
                    (p, c) =>
                    {
                        p = Puff(new Vector3(0.012f, 0.016f, 0.042f))(p, c);
                        if (c.z > 0f) p.z -= 0.014f * Mathf.Exp(-(c.y + 0.15f) * (c.y + 0.15f) / 0.02f) * (1f - c.x * c.x) * c.z;
                        return p;
                    }, 2, NoStep, FurnitureKit.Sides.Top | FurnitureKit.Sides.Front | FurnitureKit.Sides.Left | FurnitureKit.Sides.Right);
            }
            // 枕（左の肘に寄せて、肘の方を持ち上げる。頭の窪み）
            kit.RoundBox("SofaPillow", new Vector3(-L * 0.5f + 0.40f, SofaSeat + 0.045f, 0.07f), R(0f, 6f, -16f), new Vector3(0.36f, 0.13f, 0.52f), 0.06f, Cotton,
                (p, c) =>
                {
                    p = Puff(new Vector3(0.02f, 0.045f, 0.02f))(p, c);
                    p = Dent(p, c, 0.02f, 0.01f, 0.10f, 0.13f, 0.035f);
                    return p;
                }, 2, NoStep, NoBottom);
            // 丸めた毛布（二つの塊）
            var knit = Tiled(WoolArea, 0.22f);
            FurnitureKit.Deform folds = (p, c) =>
            {
                p = Puff(new Vector3(0.02f, 0.04f, 0.02f))(p, c);
                p.y += (0.022f * Mathf.Sin(p.x * 17f + p.z * 9f) + 0.012f * Mathf.Sin(p.z * 26f - p.x * 7f)) * (c.y > 0f ? 1f : 0.4f);
                p.x += 0.012f * Mathf.Sin(p.y * 31f + p.z * 11f);
                p.z += 0.010f * Mathf.Sin(p.y * 27f - p.x * 13f);
                return p;
            };
            kit.RoundBox("Blanket", new Vector3(0.42f, SofaSeat + 0.07f, 0.06f), R(0f, 18f, 3f), new Vector3(0.62f, 0.20f, 0.56f), 0.075f, knit, folds, 1, 0.25f, NoBottom);
            kit.RoundBox("BlanketFold", new Vector3(0.20f, SofaSeat + 0.15f, 0.14f), R(4f, -24f, -7f), new Vector3(0.44f, 0.11f, 0.38f), 0.05f, knit, folds, 1, 0.25f, NoBottom);
            // 座の前の縁から垂れる裾
            kit.Sheet("BlanketTail", (u, v) =>
            {
                float z, y;
                if (v < 0.35f) { z = Mathf.Lerp(0.18f, 0.44f, v / 0.35f); y = SofaSeat + 0.012f; }
                else if (v < 0.5f)
                {
                    var a = (v - 0.35f) / 0.15f * 90f * Mathf.Deg2Rad;
                    z = 0.44f + 0.045f * Mathf.Sin(a);
                    y = SofaSeat - 0.033f + 0.045f * Mathf.Cos(a);
                }
                else { z = 0.485f + 0.015f * (v - 0.5f); y = Mathf.Lerp(SofaSeat - 0.033f, 0.19f, (v - 0.5f) / 0.5f); }
                var x = Mathf.Lerp(0.08f, 0.60f, u) + 0.018f * Mathf.Sin(v * 7f + u * 3f);
                z += 0.014f * Mathf.Sin(u * Mathf.PI * 4f) * (v > 0.45f ? 1f : 0.25f);
                return new Vector3(x, y, z);
            }, 4, 6, Wool, true, 70f);
        }

        // ---- ラグ ----------------------------------------------------------------------

        public const float RugWide = 1.8f;
        public const float RugLong = 3.1f;

        /// <summary>毛足の長いラグ（柄と房は無し）。厚み 2 cm の上の面を毛の揺らぎで波打たせ（毛並みは絵の法線）、縁に毛羽を立てる。長い辺が z</summary>
        static void Rug(FurnitureKit kit, float wide, float lng)
        {
            FurnitureKit.Deform pile = (p, c) =>
            {
                if (c.y > 0.5f) p.y += 0.005f * (Mathf.PerlinNoise(p.x * 9f + 3.1f, p.z * 9f + 7.7f) - 0.5f);
                return p;
            };
            kit.RoundBox("RugPile", new Vector3(0f, 0.01f, 0f), Quaternion.identity, new Vector3(wide, 0.02f, lng), 0f, Fur, pile, 0, NoStep, NoBottom, 70f);
            // 縁の毛羽。辺に沿って 6 cm ごとに、外と上へ跳ねた小さな房（外と上を向く二枚）
            kit.Begin("RugFluff", 75f);
            var hw = wide * 0.5f;
            var hl = lng * 0.5f;
            var sides = new[]
            {
                new { From = new Vector3(-hw, 0f, -hl), To = new Vector3(hw, 0f, -hl), Out = Vector3.back },
                new { From = new Vector3(hw, 0f, -hl), To = new Vector3(hw, 0f, hl), Out = Vector3.right },
                new { From = new Vector3(hw, 0f, hl), To = new Vector3(-hw, 0f, hl), Out = Vector3.forward },
                new { From = new Vector3(-hw, 0f, hl), To = new Vector3(-hw, 0f, -hl), Out = Vector3.left },
            };
            var i = 0;
            foreach (var s in sides)
            {
                var len = Vector3.Distance(s.From, s.To);
                var t = (s.To - s.From).normalized;
                var n = Mathf.FloorToInt(len / 0.06f);
                for (var k = 0; k < n; k++, i++)
                {
                    var e = Vector3.Lerp(s.From, s.To, (k + 0.5f) / n);
                    var h1 = Hash(i, 1);
                    var h2 = Hash(i, 2);
                    var h3 = Hash(i, 3);
                    var r0 = e - t * 0.03f + Vector3.up * 0.004f - s.Out * 0.004f;
                    var r1 = e + t * 0.03f + Vector3.up * 0.004f - s.Out * 0.004f;
                    var r2 = e - s.Out * 0.022f + Vector3.up * 0.021f;
                    var tip = e + s.Out * (0.008f + 0.012f * h1) + Vector3.up * (0.013f + 0.012f * h2) + t * (h3 - 0.5f) * 0.03f;
                    var uv = Fur.At(0.1f + 0.8f * h2, 0.1f + 0.8f * h3);
                    var a = kit.V(r0, uv);
                    var b = kit.V(r1, uv);
                    var c = kit.V(r2, uv);
                    var d = kit.V(tip, uv);
                    var mid = (r0 + r1 + r2) / 3f;
                    kit.TFacing(FurnitureKit.Atlas, a, b, d, (r0 + r1 + tip) / 3f - mid);
                    kit.TFacing(FurnitureKit.Atlas, b, c, d, (r1 + r2 + tip) / 3f - mid);
                }
            }
            kit.End();
        }

        // ---- ローテーブル ----------------------------------------------------------------

        /// <summary>低いテーブル（1.0 × 0.5 m、高さ 0.40）。艶のある黒い石の天板、つや消しの黒の細る脚、下の棚に雑誌二冊、天板にマグ（飲みかけの珈琲）。メモは別の物（Room/Binder）を載せる</summary>
        static void LowTable(FurnitureKit kit)
        {
            const float L = 1.0f, W = 0.5f, H = 0.40f;
            kit.Box("TableTop", new Vector3(0f, H - 0.0175f, 0f), Quaternion.identity, new Vector3(L, 0.035f, W), BlackTop, NoBottom);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    kit.Lathe("TableLeg", new Vector3(sx * (L * 0.5f - 0.07f), 0f, sz * (W * 0.5f - 0.07f)), Quaternion.identity,
                        new[] { new Vector2(0.013f, 0f), new Vector2(0.021f, H - 0.035f) }, 4, Whole(Uv(LaminateArea)));
            kit.Box("TableShelf", new Vector3(0f, 0.13f, 0f), Quaternion.identity, new Vector3(L - 0.12f, 0.018f, W - 0.13f), Whole(Uv(LaminateArea)), NoBottom);
            var pages = Sw(Hue.Paper);
            kit.Box6("Magazine", new Vector3(-0.14f, 0.142f, 0.0f), R(0f, 8f, 0f), new Vector3(0.21f, 0.006f, 0.28f),
                new[] { pages, pages, pages, Whole(Uv(MagazineArea, 0, 0, 12, 16)), pages, pages }, NoBottom);
            kit.Box6("Magazine", new Vector3(-0.10f, 0.1475f, 0.02f), R(0f, -12f, 0f), new Vector3(0.21f, 0.005f, 0.28f),
                new[] { pages, pages, pages, Whole(Uv(MagazineArea, 12, 0, 12, 16)), pages, pages }, NoBottom);
            Mug(kit, new Vector3(0.30f, H, 0.10f), 150f);
        }

        /// <summary>マグ（艶のある黒い陶器、飲みかけの珈琲、取っ手。オーナー「テーブルのマグカップも黒に」）</summary>
        static void Mug(FurnitureKit kit, Vector3 at, float yaw)
        {
            using (kit.At(at, yaw))
            {
                var glaze = Sw(Hue.PianoBlack);
                kit.Lathe("Mug", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.036f, 0f), new Vector2(0.040f, 0.093f), new Vector2(0.035f, 0.094f), new Vector2(0.035f, 0.066f) }, 8, glaze);
                kit.Lathe("MugCoffee", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.035f, 0.066f), new Vector2(0f, 0.066f) }, 8, Sw(Hue.Coffee));
                kit.Tube("MugHandle", new[] { new Vector3(0.038f, 0.078f, 0f), new Vector3(0.064f, 0.068f, 0f), new Vector3(0.062f, 0.030f, 0f), new Vector3(0.038f, 0.022f, 0f) }, 0.0065f, 4, glaze);
            }
        }

        // ---- フロアランプ ----------------------------------------------------------------

        /// <summary>電球の高さ（灯りの置き場）</summary>
        const float LampBulbY = 1.36f;

        /// <summary>フロアランプ。黒い重い台、真鍮の柱、布の円錐台の傘（内も張る。光る）、電球、傘を吊る細い腕</summary>
        static void FloorLamp(FurnitureKit kit)
        {
            kit.Lathe("LampBase", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.151f, 0f), new Vector2(0.15f, 0.014f), new Vector2(0.03f, 0.038f) }, 10, Sw(Hue.Black));
            kit.Lathe("LampPole", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.011f, 0.036f), new Vector2(0.011f, 1.30f) }, 6, Sw(Hue.Brass));
            kit.Lathe("LampCollar", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.016f, 1.28f), new Vector2(0.016f, 1.31f) }, 6, Sw(Hue.Brass), false, true);
            var shade = new[] { new Vector2(0.21f, 1.22f), new Vector2(0.165f, 1.50f) };
            kit.Lathe("LampShade", Vector3.zero, Quaternion.identity, shade, 12, Sw(Hue.LampShade));
            kit.Lathe("LampShadeInside", Vector3.zero, Quaternion.identity, shade, 12, Sw(Hue.LampShade), false, false, true);
            kit.Lathe("LampBulb", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.012f, 1.31f), new Vector2(0.034f, 1.35f), new Vector2(0.026f, 1.405f), new Vector2(0f, 1.415f) }, 6, Sw(Hue.Bulb));
            // 傘を吊る腕（柱の先から傘の上の縁へ三本）
            kit.Tube("LampHarp", new[] { new Vector3(0f, 1.31f, 0f), new Vector3(0f, 1.49f, 0f) }, 0.004f, 3, Sw(Hue.Brass));
            for (var k = 0; k < 3; k++)
            {
                var a = k * 120f * Mathf.Deg2Rad;
                kit.Tube("LampSpoke", new[] { new Vector3(0f, 1.49f, 0f), new Vector3(Mathf.Sin(a) * 0.162f, 1.49f, Mathf.Cos(a) * 0.162f) }, 0.0025f, 3, Sw(Hue.Brass));
            }
        }

        // ---- エアコン ----------------------------------------------------------------------

        /// <summary>
        /// 壁掛けのエアコン（0.82 × 0.28 m、奥行き 0.21）。黒に近い灰の筐体（丸めた角。背は壁）、顔の絵（数字の表示と灯り）、下の吹き出しの口と羽、右の端から天井へ上る配管の覆い
        /// （下へ降ろすとソファの上の絵の組に掛かる）。mountY は置く高さ（配管の覆いの長さを天井まで取る）
        /// </summary>
        static void AirCon(FurnitureKit kit, float mountY)
        {
            const float W = 0.82f, H = 0.28f, D = 0.21f;
            var white = Sw(Hue.AcWhite);
            kit.RoundBox("AcBody", new Vector3(0f, 0f, D * 0.5f), Quaternion.identity, new Vector3(W, H, D), 0.035f, white, null, 1, NoStep, NoBack);
            kit.Decal("AcFront", new Vector3(0f, 0.035f, D + 0.0012f), new Vector3(-(W - 0.08f) * 0.5f, 0f, 0f), new Vector3(0f, 0.085f, 0f), Uv(AcFrontArea));
            kit.Box("AcSlot", new Vector3(0f, -H * 0.5f + 0.042f, D - 0.004f), Quaternion.identity, new Vector3(W - 0.12f, 0.03f, 0.012f), Sw(Hue.Slot), NoBack);
            kit.Box("AcFlap", new Vector3(0f, -H * 0.5f + 0.02f, D - 0.004f), R(38f, 0f, 0f), new Vector3(W - 0.12f, 0.006f, 0.055f), white, FurnitureKit.Sides.All);
            var rise = Mathf.Max(0.05f, RoomPlan.Ceiling - (mountY + H * 0.5f));
            kit.Box("AcPipeCover", new Vector3(W * 0.5f - 0.07f, H * 0.5f + rise * 0.5f, 0.034f), Quaternion.identity, new Vector3(0.075f, rise, 0.066f), white, Open);
        }

        // ---- 鉢植え --------------------------------------------------------------------

        /// <summary>
        /// 大きな鉢植え（フィドルリーフ・フィグのような、幅の広い葉を重ねた木）。濃い釉の鉢と受け皿、土、三本の幹、
        /// 幹ごとに螺旋に付く葉（下ほど大きく垂れ、上ほど立つ。主脈で少し折れ、先が垂れる）。萎れた葉・枯れ葉は無し。
        /// 葉の広がり（輪郭）は mesh に残し、葉一枚は付け根・幅の広い所の左右と主脈・先の五点の両面（主脈と側脈は葉の絵と法線）
        /// </summary>
        static void Plant(FurnitureKit kit, float spread)
        {
            var pot = Sw(Hue.PotDark);
            kit.Lathe("PlantSaucer", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.18f, 0f), new Vector2(0.19f, 0.025f), new Vector2(0.15f, 0.018f) }, 10, pot);
            kit.Lathe("PlantPot", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.14f, 0.015f), new Vector2(0.175f, 0.30f), new Vector2(0.19f, 0.32f), new Vector2(0.19f, 0.345f), new Vector2(0.172f, 0.345f) }, 10, pot);
            kit.Lathe("PlantSoil", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.172f, 0.30f), new Vector2(0f, 0.305f) }, 10, Sw(Hue.Soil));
            var leaf = Whole(Uv(LeafArea));
            var stems = new[]
            {
                new[] { new Vector3(-0.03f, 0.30f, 0.02f), new Vector3(-0.06f, 0.70f, 0.04f), new Vector3(-0.10f, 1.10f, 0.01f), new Vector3(-0.12f, 1.55f, -0.03f) },
                new[] { new Vector3(0.04f, 0.30f, -0.02f), new Vector3(0.07f, 0.65f, -0.06f), new Vector3(0.12f, 0.98f, -0.09f), new Vector3(0.15f, 1.32f, -0.10f) },
                new[] { new Vector3(0.01f, 0.30f, 0.05f), new Vector3(0.03f, 0.60f, 0.10f), new Vector3(0.02f, 0.88f, 0.16f), new Vector3(0.05f, 1.15f, 0.20f) },
            };
            for (var s = 0; s < stems.Length; s++)
            {
                // 幹の傾きを spread だけ寄せる（葉の広がりを細くして、壁の角に収める）
                var path = new Vector3[stems[s].Length];
                for (var i = 0; i < path.Length; i++) path[i] = new Vector3(stems[s][i].x * spread, stems[s][i].y, stems[s][i].z * spread);
                var radii = new List<float>();
                for (var i = 0; i < path.Length; i++) radii.Add(Mathf.Lerp(0.016f, 0.006f, (float)i / (path.Length - 1)));
                kit.TubeR("PlantStem", path, radii, 4, Sw(Hue.Stem));
                var count = 14 + s * 2;
                for (var k = 0; k < count; k++)
                {
                    var f = 0.2f + 0.8f * k / (count - 1);
                    var at = Along(path, f);
                    var azimuth = (k * 137.5f + s * 57f) * Mathf.Deg2Rad;
                    var elev = Mathf.Lerp(-8f, 42f, f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(elev), Mathf.Sin(elev), Mathf.Cos(azimuth) * Mathf.Cos(elev));
                    var len = Mathf.Lerp(0.36f, 0.24f, f) * (0.9f + 0.2f * Hash(k, s + 40)) * Mathf.Lerp(1f, spread, 0.9f);
                    Leaf(kit, at + dir * 0.015f, dir, len, len * 0.62f, Mathf.Lerp(0.35f, 0.12f, f), leaf);
                }
            }
        }

        /// <summary>幹の点の並びの上の割合 f の位置</summary>
        static Vector3 Along(Vector3[] path, float f)
        {
            var t = Mathf.Clamp01(f) * (path.Length - 1);
            var i = Mathf.Min(Mathf.FloorToInt(t), path.Length - 2);
            return Vector3.Lerp(path[i], path[i + 1], t - i);
        }

        /// <summary>
        /// 葉一枚。付け根 at から dir へ長さ len、幅 wide（付け根から六割の所が最も広い）。主脈で少し折れ（両脇が持ち上がる）、先が droop だけ垂れる。
        /// 付け根・左右の幅の所・主脈の幅の所・先の五点で、表と裏の四枚ずつ（uv は葉の絵の横と縦）
        /// </summary>
        static void Leaf(FurnitureKit kit, Vector3 at, Vector3 dir, float len, float wide, float droop, Tile tile)
        {
            var flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            var side = Vector3.Cross(Vector3.up, flat.normalized).normalized;
            var up = Vector3.Cross(dir, side).normalized;
            if (up.y < 0f) up = -up;
            const float wideAt = 0.6f;
            System.Func<float, float, Vector3> at2 = (u, v) =>
            {
                var s = (u - 0.5f) * 2f;
                var half = wide * 0.5f;
                return at + dir * (len * v) + side * (s * half) + up * (Mathf.Abs(s) * half * 0.35f) - Vector3.up * (droop * v * v * len);
            };
            kit.Begin("Leaf", 80f);
            foreach (var back in new[] { false, true })
            {
                var b = kit.V(at2(0.5f, 0f), tile.At(0.5f, 0f));
                var l = kit.V(at2(0f, wideAt), tile.At(0f, wideAt));
                var m = kit.V(at2(0.5f, wideAt) - up * 0.004f, tile.At(0.5f, wideAt));
                var r = kit.V(at2(1f, wideAt), tile.At(1f, wideAt));
                var t = kit.V(at2(0.5f, 1f), tile.At(0.5f, 1f));
                if (!back)
                {
                    kit.T(FurnitureKit.Atlas, b, m, l); kit.T(FurnitureKit.Atlas, b, r, m);
                    kit.T(FurnitureKit.Atlas, l, m, t); kit.T(FurnitureKit.Atlas, m, r, t);
                }
                else
                {
                    kit.T(FurnitureKit.Atlas, b, l, m); kit.T(FurnitureKit.Atlas, b, m, r);
                    kit.T(FurnitureKit.Atlas, l, t, m); kit.T(FurnitureKit.Atlas, m, t, r);
                }
            }
            kit.End(FurnitureKit.Orient.None);
        }

        // ---- 額 ------------------------------------------------------------------------

        /// <summary>額の枠の幅</summary>
        const float FrameBorder = 0.022f;

        /// <summary>絵そのものの大きさ（幅, 高さ）。比は表の値（元の絵の幅 ÷ 高さ。置き場の表のソファの上の組も同じ値から出す）、長い辺が Long</summary>
        static Vector2 PictureSize(int k)
        {
            var pic = Pictures[k];
            var a = pic.Aspect;
            return a >= 1f ? new Vector2(pic.Long, pic.Long / a) : new Vector2(pic.Long * a, pic.Long);
        }

        /// <summary>額の外の大きさ（幅, 高さ）</summary>
        static Vector2 FrameSize(int k)
        {
            return PictureSize(k) + Vector2.one * (2f * FrameBorder);
        }

        /// <summary>額入りの絵（背の真ん中が原点、前が +z）。細い黒い枠と、枠の前から 1.4 cm 奥の絵（裏板は絵と壁に隠れるので張らない）</summary>
        static void Frame(FurnitureKit kit, int k)
        {
            var size = PictureSize(k);
            var w = size.x;
            var h = size.y;
            const float M = FrameBorder, Depth = 0.026f;
            var moulding = Sw(Hue.FrameBlack);
            kit.Box("FrameTop", new Vector3(0f, (h + M) * 0.5f, Depth * 0.5f), Quaternion.identity, new Vector3(w + 2f * M, M, Depth), moulding, NoBack);
            kit.Box("FrameBottom", new Vector3(0f, -(h + M) * 0.5f, Depth * 0.5f), Quaternion.identity, new Vector3(w + 2f * M, M, Depth), moulding, NoBack);
            kit.Box("FrameLeft", new Vector3(-(w + M) * 0.5f, 0f, Depth * 0.5f), Quaternion.identity, new Vector3(M, h, Depth), moulding, NoBack);
            kit.Box("FrameRight", new Vector3((w + M) * 0.5f, 0f, Depth * 0.5f), Quaternion.identity, new Vector3(M, h, Depth), moulding, NoBack);
            // 前（+z）から見て絵が裏返らないよう、u は −x へ
            kit.Decal("Canvas", new Vector3(0f, 0f, 0.012f), new Vector3(-w * 0.5f, 0f, 0f), new Vector3(0f, h * 0.5f, 0f), PictureUv[k]);
        }
    }
}
