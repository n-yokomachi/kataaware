using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    public static partial class BuildFurniture
    {
        // ---- 仕事の机 ------------------------------------------------------------------
        //
        // 机・右の卓・PC は、前の Kenney の物と同じ広さと天板の高さに作る。モニター（Room/Monitors）の柱は天板の奥に立ち、
        // 煙草の箱と灰皿（別の物）は天板の左の手前、ジャケット（別の物）は右の卓の椅子の側の縁から垂れている。どれも動かさない

        /// <summary>机の広さ（前の机の見た目と当たりの広さ）と天板の上面</summary>
        const float DeskLong = 1.682f;
        const float DeskDeep = 0.894f;
        const float DeskTop = 0.652f;

        /// <summary>
        /// 仕事の机（前が +z。置くと椅子の側を向く）。艶のある黒い石の天板、つや消しの黒い鉄の脚（両脇の門の形と足の棒、奥の貫）、奥の目隠しの板、
        /// 天板の下の奥のケーブルの受け。モニターの柱の脇の穴から受けを通って、右の端から床の PC へ、左の端から床を這ってサーバーラックへ降りるケーブルの束。
        /// 天板に鍵盤とマウスとマウスパッド
        /// </summary>
        static void Desk(FurnitureKit kit)
        {
            const float L = DeskLong, D = DeskDeep, Top = DeskTop, T = 0.03f;
            var metal = Sw(Hue.RackBlack);
            kit.Box("DeskTop", new Vector3(0f, Top - T * 0.5f, 0f), Quaternion.identity, new Vector3(L, T, D), BlackTop, NoBottom);
            foreach (var sx in new[] { -1f, 1f })
            {
                var x = sx * (L * 0.5f - 0.06f);
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("DeskPost", new Vector3(x, (Top - T) * 0.5f, sz * (D * 0.5f - 0.06f)), Quaternion.identity, new Vector3(0.04f, Top - T, 0.04f), metal, NoBottom);
                kit.Box("DeskFoot", new Vector3(x, 0.015f, 0f), Quaternion.identity, new Vector3(0.05f, 0.03f, D - 0.06f), metal, NoBottom);
                kit.Box("DeskRail", new Vector3(x, Top - T - 0.02f, 0f), Quaternion.identity, new Vector3(0.04f, 0.04f, D - 0.16f), metal, (FurnitureKit.Sides)61);
            }
            kit.Box("DeskStretcher", new Vector3(0f, 0.42f, -(D * 0.5f - 0.06f)), Quaternion.identity, new Vector3(L - 0.16f, 0.04f, 0.03f), metal, FurnitureKit.Sides.All);
            kit.Box("DeskModesty", new Vector3(0f, Top - T - 0.17f, -(D * 0.5f - 0.09f)), Quaternion.identity, new Vector3(L - 0.18f, 0.28f, 0.012f), Whole(Uv(LaminateArea)), (FurnitureKit.Sides)13);
            // ケーブルの受け（奥の、天板の下）
            const float trayY = Top - T - 0.10f;
            const float trayZ = -0.30f;
            kit.Box("CableTray", new Vector3(0f, trayY, trayZ), Quaternion.identity, new Vector3(L - 0.40f, 0.008f, 0.12f), metal, (FurnitureKit.Sides)11);
            kit.Box("CableTrayLip", new Vector3(0f, trayY + 0.03f, trayZ + 0.06f), Quaternion.identity, new Vector3(L - 0.40f, 0.06f, 0.006f), metal, (FurnitureKit.Sides)11);
            kit.Lathe("DeskGrommet", new Vector3(0.10f, Top, -0.33f), Quaternion.identity, new[] { new Vector2(0.03f, 0.001f), new Vector2(0.02f, 0.001f) }, 6, Sw(Hue.Black));
            // ケーブルの束。モニターの柱の脇の穴から受けへ降り、受けに沿って右と左の端へ（四本）
            var colours = new[] { Hue.CableBlack, Hue.CableGrey, Hue.CableBlue, Hue.CableBlack };
            for (var k = 0; k < colours.Length; k++)
            {
                var o = (k - 1.5f) * 0.008f;
                var toRight = k < 2;
                // 右（置くと PC の側。机の −x）と左（サーバーラックの側。+x）
                var end = toRight ? -1f : 1f;
                var pts = new List<Vector3>
                {
                    new Vector3(0.10f + o, Top + 0.002f, -0.33f),
                    new Vector3(0.10f + o, Top - T - 0.02f, -0.33f),
                    new Vector3(0.10f + end * 0.15f, trayY + 0.012f + (k % 2) * 0.006f, trayZ + o),
                    new Vector3(end * (L * 0.5f - 0.25f), trayY + 0.012f + (k % 3) * 0.005f, trayZ + o),
                    new Vector3(end * (L * 0.5f - 0.12f), trayY - 0.06f, trayZ - 0.04f + o),
                    new Vector3(end * (L * 0.5f - 0.10f), 0.10f, -0.36f + o),
                    new Vector3(end * (L * 0.5f - 0.06f), 0.006f, -0.34f + o),
                };
                if (toRight)
                {
                    pts.Add(new Vector3(-(L * 0.5f + 0.06f), 0.006f, -0.28f + o * 2f));
                    pts.Add(new Vector3(-(L * 0.5f + 0.14f), 0.08f + k * 0.02f, -0.20f));
                }
                else
                {
                    pts.Add(new Vector3(L * 0.5f + 0.20f, 0.006f, -0.30f + o * 2f));
                    pts.Add(new Vector3(L * 0.5f + 0.45f, 0.006f, -0.18f + o * 2f));
                }
                kit.Tube("DeskCable", pts, 0.0045f, 3, Sw(colours[k]));
            }
            // 鍵盤（前の置き場の真ん中のまま）と、奥から受けの穴へ這うケーブル
            Keyboard(kit, new Vector3(-0.05f, Top, 0.26f), 2f);
            kit.Tube("KeyboardCable", new[] { new Vector3(0.01f, Top + 0.012f, 0.203f), new Vector3(0.02f, Top + 0.004f, 0.13f), new Vector3(0.07f, Top + 0.004f, -0.12f), new Vector3(0.10f, Top + 0.004f, -0.30f) }, 0.003f, 3, Sw(Hue.CableBlack));
            // マウスパッドとマウス（落ち着いた黒。前の紫の灯りはやめた。パッドは艶の無い濃い灰の布で、黒い石の天板とマウスの間に差を残す）
            kit.Box("MousePad", new Vector3(-0.52f, Top + 0.0015f, 0.26f), Quaternion.identity, new Vector3(0.28f, 0.003f, 0.23f), Sw(Hue.PadCloth), NoBottom);
            // マウス（前へ低くなる背。前後に二つ割りの角の立った箱。艶のある黒で、パッドの上で照りが形を読ませる）
            kit.RoundBox("Mouse", new Vector3(-0.52f, Top + 0.02f, 0.27f), R(0f, -6f, 0f), new Vector3(0.062f, 0.034f, 0.108f), 0f, Sw(Hue.PianoBlack),
                (p, c) => { if (c.y > 0f) p.y -= 0.008f * (c.z + 1f) * 0.5f * c.y; p.x *= 1f - 0.12f * c.y * c.y; return p; }, 0, 0.06f, NoBottom, 45f);
        }

        /// <summary>
        /// 小さな鍵盤（オーナー「キーボードがちょっとゲーミング感強かったので、HHKBの墨を再現してほしい」「キーボードは無刻印でいい」。見た目を寄せるだけで、名前や印は入れない）。
        /// 60% の並び（幅 0.294・奥行き 0.110、矢印キー無し）。原点は足元の真ん中、前（+z）が使う人。墨色の筐体は前が低く奥が高い楔（前 1.3 cm・奥 2.5 cm）、
        /// その上に五列のキーを列ごとの箱で載せる。列ごとに高さと傾きを変えた段付き（数字の列が使う人の側へ最も傾き、空白の列は奥へ倒れる）で、
        /// 段と筐体の傾きは形、キーの割り付けの溝は絵と法線の絵（<see cref="PaintKeys"/>）。キーは無刻印の墨色、光る所は無い
        /// </summary>
        static void Keyboard(FurnitureKit kit, Vector3 at, float yaw)
        {
            const float W = 0.294f, D = 0.110f, front = 0.013f, back = 0.025f, U = 0.01905f;
            using (kit.At(at, yaw))
            {
                var sumi = Sw(Hue.Sumi);
                kit.RoundBox("KeyboardCase", new Vector3(0f, front * 0.5f, 0f), Quaternion.identity, new Vector3(W, front, D), 0f, sumi,
                    (p, c) => { if (c.y > 0f) p.y += (back - front) * (0.5f - p.z / D); return p; }, 0, NoStep, NoBottom, 30f);
                var slope = Mathf.Atan2(back - front, D) * Mathf.Rad2Deg;
                var sculpt = new[] { 10f, 4f, 0f, -4f, -9f };
                var high = new[] { 0.0115f, 0.0105f, 0.0098f, 0.0102f, 0.0108f };
                var side = Sw(Hue.KeySumi);
                for (var row = 0; row < 5; row++)
                {
                    var z = (row - 2) * U;
                    var plate = front + (back - front) * (0.5f - z / D);
                    var span = KeyRowSpan(row);
                    var wide = span.y / 4f * U;
                    // キーの並び（キー幅 15）を筐体の真ん中に。使う人から見た左（+x）の端から span.x（空白の列はキー幅 2）だけ右（−x）へ寄せて並べる
                    var cx = 7.5f * U - span.x / 4f * U - wide * 0.5f;
                    var top = Whole(Uv(KeysArea, KeysArea.width - span.x - span.y, row * 4, span.y, 4));
                    var face = Whole(Uv(KeysArea, KeysArea.width - span.x - span.y, 20 + row, span.y, 1));
                    using (kit.At(new Vector3(cx, plate - 0.002f, z), R(slope + sculpt[row], 0f, 0f)))
                        kit.Box6("KeyRow", new Vector3(0f, high[row] * 0.5f, 0f), Quaternion.identity, new Vector3(wide - 0.0015f, high[row], U - 0.0026f),
                            new[] { side, side, side, top, side, face }, Open);
                }
            }
        }

        /// <summary>
        /// 右の卓（前が +z。置くと椅子の側を向く）。前の卓と同じ広さ（長さ 1.068・奥行き 0.44）と天板の高さ 0.769。
        /// 艶のある黒い天板は椅子の側へ 9 cm 張り出し（ジャケットがその縁から垂れる）、下につや消しの黒の引き出し二杯と取っ手、四本の脚、下の棚に本二冊
        /// </summary>
        static void SideTable(FurnitureKit kit)
        {
            const float L = 1.068f, D = 0.44f, Top = 0.769f, T = 0.03f;
            kit.Box("SideTop", new Vector3(0f, Top - T * 0.5f, 0f), Quaternion.identity, new Vector3(L, T, D), BlackTop, FurnitureKit.Sides.All);
            const float bodyDeep = D - 0.10f;
            const float bodyZ = -0.04f;
            const float bodyHigh = 0.20f;
            kit.Box6("SideBody", new Vector3(0f, Top - T - bodyHigh * 0.5f, bodyZ), Quaternion.identity, new Vector3(L - 0.04f, bodyHigh, bodyDeep), Carcass(true), (FurnitureKit.Sides)61);
            var front = bodyZ + bodyDeep * 0.5f;
            foreach (var sx in new[] { -1f, 1f })
            {
                var cx = sx * (L - 0.04f) * 0.25f;
                Panel(kit, "SideDrawer", new Vector3(cx, Top - T - bodyHigh * 0.5f, front + 0.009f), new Vector3((L - 0.04f) * 0.5f - 2f * DoorGap, bodyHigh - 2f * DoorGap, 0.018f));
                Pull(kit, new Vector3(cx, Top - T - 0.06f, front + 0.018f), true, 0.14f);
            }
            var metal = Sw(Hue.RackBlack);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("SideLeg", new Vector3(sx * (L * 0.5f - 0.04f), (Top - T - bodyHigh) * 0.5f, bodyZ + sz * (bodyDeep * 0.5f - 0.02f)), Quaternion.identity, new Vector3(0.035f, Top - T - bodyHigh, 0.035f), metal, NoBottom);
            kit.Box("SideShelf", new Vector3(0f, 0.12f, bodyZ), Quaternion.identity, new Vector3(L - 0.08f, 0.018f, bodyDeep - 0.02f), Whole(Uv(LaminateArea)), FurnitureKit.Sides.All);
            Book(kit, new Vector3(-0.25f, 0.129f + 0.012f, bodyZ), R(0f, 6f, 90f), new Vector3(0.024f, 0.21f, 0.15f), 9, (FurnitureKit.Sides)59);
            Book(kit, new Vector3(-0.25f, 0.129f + 0.035f, bodyZ + 0.01f), R(0f, -8f, 90f), new Vector3(0.022f, 0.19f, 0.13f), 22, (FurnitureKit.Sides)59);
        }

        /// <summary>机の右の床の PC（前が +z）。黒い箱、前の面の通気の溝と紫の灯りの線と電源の輪、机の側の面の暗い硝子（中の紫の扇）、脚</summary>
        static void Tower(FurnitureKit kit)
        {
            const float W = 0.24f, H = 0.52f, D = 0.46f;
            kit.Box("TowerCase", new Vector3(0f, 0.02f + (H - 0.02f) * 0.5f, 0f), Quaternion.identity, new Vector3(W, H - 0.02f, D), Sw(Hue.RackBlack), FurnitureKit.Sides.All);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("TowerFoot", new Vector3(sx * (W * 0.5f - 0.03f), 0.01f, sz * (D * 0.5f - 0.05f)), Quaternion.identity, new Vector3(0.03f, 0.02f, 0.05f), Sw(Hue.Rubber), (FurnitureKit.Sides)60);
            kit.Decal("TowerFront", new Vector3(0f, 0.02f + (H - 0.02f) * 0.5f, D * 0.5f + 0.001f), new Vector3(-(W - 0.03f) * 0.5f, 0f, 0f), new Vector3(0f, (H - 0.08f) * 0.5f, 0f), Uv(PcFrontArea));
            // 机の側（置くと −x 側。前が +z の時の +x）の硝子
            kit.Decal("TowerGlass", new Vector3(W * 0.5f + 0.001f, 0.02f + (H - 0.02f) * 0.5f, 0f), new Vector3(0f, 0f, -(D - 0.06f) * 0.5f), new Vector3(0f, (H - 0.08f) * 0.5f, 0f), Uv(PcSideArea));
        }

        // ---- コート掛け ----------------------------------------------------------------

        /// <summary>
        /// 床に立つコート掛け（前の Kenney の物と同じ高さ 1.54 m）。黒い重い台、黒い柱と頭の玉、下の段に ±x・±z の四本の腕（先が上へ反り、玉が付く）、
        /// 上の段に斜め四本の短い腕。下の段の東の腕に丈の長い暗いコート、上の段の北東の腕につばのある帽子（オーナー「ハンガーのバッグ・マフラーを削除し、コート、ハットをかけて」）。
        /// **下の段の腕の形は場面 3 のジャケットの掛け方（<see cref="BuildConnect"/> の FindHook）が読む。** 腕は柱から 0.247 m、腕の先の下の面は床から 1.197 m（前の物とほぼ同じ）。
        /// 廊下の口を向く西の腕は、場面 3・5・7 でジャケットが掛かるので空けておく
        /// </summary>
        static void CoatRack(FurnitureKit kit)
        {
            var black = Sw(Hue.RackBlack);
            kit.Lathe("RackBase", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.20f, 0f), new Vector2(0.20f, 0.018f), new Vector2(0.17f, 0.034f), new Vector2(0.03f, 0.048f) }, 10, black);
            kit.Lathe("RackPole", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.018f, 0.045f), new Vector2(0.015f, 1.50f) }, 6, black);
            kit.Lathe("RackFinial", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.015f, 1.50f), new Vector2(0.022f, 1.518f), new Vector2(0f, 1.54f) }, 6, black);
            // 下の段（高さの 7〜8 割）
            var lowS = new[] { 0.012f, 0.10f, 0.19f, 0.225f, 0.238f, 0.236f };
            var lowY = new[] { 1.150f, 1.170f, 1.195f, 1.205f, 1.225f, 1.243f };
            // 廊下の口を向く西の腕（k = 3）だけは前の細かさのまま。場面 3 のジャケットの掛け方（FindHook）がこの腕の先の頂点から掛ける所を読むので、
            // 掛ける所 (0.733, 1.193, −1.050) と調べる対象 coat を動かさない
            for (var k = 0; k < 4; k++) Arm(kit, k * 90f, lowS, lowY, 0.008f, 0.011f, k == 3);
            // 上の段（高さの 9 割より上）。斜めに
            var highS = new[] { 0.012f, 0.08f, 0.14f, 0.165f, 0.172f };
            var highY = new[] { 1.400f, 1.415f, 1.437f, 1.452f, 1.470f };
            for (var k = 0; k < 4; k++) Arm(kit, 45f + k * 90f, highS, highY, 0.007f, 0.010f, false);
            // 東の下の腕に、丈の長い暗いコート（襟の吊り紐を腕に掛け、前を東へ向けて垂らす）
            var hook = Vector3.right * 0.217f + Vector3.up * 1.197f;
            using (kit.At(hook, 90f)) Coat(kit);
            // 北東の上の腕の先に、つばのある帽子（頭の内側を腕の先の玉に引っ掛け、少し傾く）
            var tip = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward * 0.172f + Vector3.up * 1.47f;
            using (kit.At(tip + Vector3.down * 0.105f, Quaternion.Euler(0f, 45f, 0f) * Quaternion.Euler(-18f, 0f, 0f))) Hat(kit);
        }

        /// <summary>
        /// 丈の長いコート（原点は襟の後ろの吊り紐＝掛ける所、前が +z、下へ 1.02 m）。暗い灰茶のウール。
        /// 輪郭（吊り紐から急に落ちる撫で肩、裾の広がり、身頃の脇の外へ垂れる筒の袖と袖口、首の後ろに立つ厚みのある襟、前の V の襟の板）は mesh、
        /// 面の上の起伏（打ち合わせの線と釦・腰の雨蓋のポケット・背の縫い目と半ベルトと釦・裾の割れ・裾の襞）は前と背の絵と法線の絵（<see cref="PaintCoat"/>）
        /// </summary>
        static void Coat(FurnitureKit kit)
        {
            var wool = Sw(Hue.CoatWool);
            const float top = -0.02f, length = 1.02f, deep = 0.13f, zc = 0.075f;
            const float half = 0.22f;
            var faces = new[] { wool, wool, wool, wool, Whole(Uv(CoatBackArea)), Whole(Uv(CoatFrontArea)) };
            // 身頃。t は上から下への割合（0 が襟の付け根、1 が裾）
            kit.RoundBox("CoatBody", new Vector3(0f, top - length * 0.5f, zc), Quaternion.identity, new Vector3(half * 2f, length, deep), 0.045f, wool,
                (p, c) =>
                {
                    var t = (1f - c.y) * 0.5f;
                    // 撫で肩（吊り紐の所で幅 0.17 m、肩の山まで 15 cm で広がる）、腰で少し締まり、裾で広がる
                    var wide = t < 0.15f ? Mathf.Lerp(0.38f, 1f, Mathf.Sin(t / 0.15f * Mathf.PI * 0.5f))
                        : t < 0.45f ? Mathf.Lerp(1f, 0.96f, (t - 0.15f) / 0.3f) : Mathf.Lerp(0.96f, 1.14f, (t - 0.45f) / 0.55f);
                    p.x *= wide;
                    // 吊られた所は薄く
                    p.z *= Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(t / 0.12f));
                    return p;
                }, 1, 0.16f, FurnitureKit.Sides.All, 60f, faces);
            var front = zc + deep * 0.5f;
            // 首の後ろに立つ襟（脇は前へ回り込む。厚みは形のまま）
            kit.RoundBox("CoatCollar", new Vector3(0f, -0.05f, 0.035f), R(-8f, 0f, 0f), new Vector3(0.20f, 0.08f, 0.045f), 0f, wool,
                (p, c) => { p.z += 3.5f * p.x * p.x; return p; }, 0, 0.07f, FurnitureKit.Sides.All, 60f);
            // 前の V の襟（襟の脇から打ち合わせへ下りる板）
            foreach (var s in new[] { -1f, 1f })
                kit.Box("CoatLapel", new Vector3(s * 0.050f, -0.25f, front + 0.008f), R(0f, 0f, -s * 20f), new Vector3(0.07f, 0.22f, 0.016f), wool, NoBack);
            // 袖。肩の山から身頃の脇の外へ少し出て垂れ、肘で少し前へ折れる。袖口は少し太い筒で閉じる
            foreach (var s in new[] { -1f, 1f })
            {
                var path = new[]
                {
                    new Vector3(s * 0.170f, -0.115f, 0.078f), new Vector3(s * 0.212f, -0.22f, 0.086f), new Vector3(s * 0.240f, -0.40f, 0.094f), new Vector3(s * 0.240f, -0.64f, 0.110f),
                };
                kit.TubeR("CoatSleeve", path, new[] { 0.030f, 0.054f, 0.052f, 0.047f }, 6, wool, true, 60f);
                kit.TubeR("CoatCuff", new[] { new Vector3(s * 0.240f, -0.62f, 0.110f), new Vector3(s * 0.238f, -0.69f, 0.112f) }, new[] { 0.051f, 0.051f }, 6, wool, true, 60f);
            }
        }

        /// <summary>つばのある帽子（原点はつばの付け根の真ん中、上が +y）。つまんだ頭、帯、ぐるりのつば</summary>
        static void Hat(FurnitureKit kit)
        {
            var felt = Sw(Hue.HatFelt);
            kit.Lathe("HatCrown", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.092f, 0f), new Vector2(0.088f, 0.085f), new Vector2(0.050f, 0.117f), new Vector2(0f, 0.108f) }, 8, felt);
            kit.Lathe("HatBand", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.094f, 0.004f), new Vector2(0.093f, 0.03f) }, 8, Sw(Hue.HatBand));
            kit.Lathe("HatBrim", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.09f, 0.002f), new Vector2(0.166f, 0.010f), new Vector2(0.09f, -0.004f) }, 10, felt);
        }

        /// <summary>
        /// コート掛けの腕一本。yaw の向きへ、柱からの隔たり s と高さ y の点を通る筒（四角い断面）と、先の玉。
        /// fine なら前の細かさ（点の間を刻んだ五角の筒と八角の玉）で作る（ジャケットを掛ける腕。掛ける所を頂点から読まれる）
        /// </summary>
        static void Arm(FurnitureKit kit, float yaw, float[] s, float[] y, float radius, float ball, bool fine)
        {
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var pts = new List<Vector3>();
            for (var i = 0; i < s.Length; i++) pts.Add(dir * s[i] + Vector3.up * y[i]);
            var tip = pts[pts.Count - 1];
            if (fine)
            {
                kit.Tube("RackArm", Smooth(pts, 2), radius, 5, Sw(Hue.RackBlack));
                kit.Lathe("RackArmTip", tip + Vector3.down * ball, Quaternion.identity, new[] { new Vector2(0.001f, 0f), new Vector2(ball * 0.8f, ball * 0.35f), new Vector2(ball, ball), new Vector2(ball * 0.8f, ball * 1.65f), new Vector2(0f, ball * 2f) }, 8, Sw(Hue.RackBlack));
                return;
            }
            kit.Tube("RackArm", pts, radius, 4, Sw(Hue.RackBlack));
            kit.Lathe("RackArmTip", tip + Vector3.down * ball, Quaternion.identity, new[] { new Vector2(0.001f, 0f), new Vector2(ball, ball), new Vector2(0f, ball * 2f) }, 5, Sw(Hue.RackBlack));
        }

        // ---- 台所の天井の灯り ----------------------------------------------------------

        /// <summary>台所の天井の灯りの芯（灯具の原点から下へ）</summary>
        const float KitchenGlowDrop = 0.12f;

        /// <summary>台所の天井に付ける細長い灯具（長さ 1.0・幅 0.14・厚み 0.05。原点は天井に付く上の面の真ん中）。黒い枠と、下の乳白の光る板</summary>
        static void KitchenLight(FurnitureKit kit)
        {
            kit.Box("KitchenLightBody", new Vector3(0f, -0.025f, 0f), Quaternion.identity, new Vector3(1.0f, 0.05f, 0.14f), Sw(Hue.RackBlack), (FurnitureKit.Sides)61);
            kit.Decal("KitchenLightDiffuser", new Vector3(0f, -0.0505f, 0f), new Vector3(-0.47f, 0f, 0f), new Vector3(0f, 0f, 0.052f), new Rect(SwatchUv(Hue.Diffuser), Vector2.zero));
        }
    }
}
