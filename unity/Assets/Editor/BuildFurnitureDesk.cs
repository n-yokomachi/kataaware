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
            kit.RoundBox("DeskTop", new Vector3(0f, Top - T * 0.5f, 0f), Quaternion.identity, new Vector3(L, T, D), 0.006f, BlackTop, null, 1, 1f);
            foreach (var sx in new[] { -1f, 1f })
            {
                var x = sx * (L * 0.5f - 0.06f);
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("DeskPost", new Vector3(x, (Top - T) * 0.5f, sz * (D * 0.5f - 0.06f)), new Vector3(0.04f, Top - T, 0.04f), metal, NoBottom);
                kit.Box("DeskFoot", new Vector3(x, 0.015f, 0f), new Vector3(0.05f, 0.03f, D - 0.06f), metal, NoBottom);
                kit.Box("DeskRail", new Vector3(x, Top - T - 0.02f, 0f), new Vector3(0.04f, 0.04f, D - 0.16f), metal);
            }
            kit.Box("DeskStretcher", new Vector3(0f, 0.42f, -(D * 0.5f - 0.06f)), new Vector3(L - 0.16f, 0.04f, 0.03f), metal);
            kit.Box("DeskModesty", new Vector3(0f, Top - T - 0.17f, -(D * 0.5f - 0.09f)), new Vector3(L - 0.18f, 0.28f, 0.012f), Laminate);
            // ケーブルの受け（奥の、天板の下）
            const float trayY = Top - T - 0.10f;
            const float trayZ = -0.30f;
            kit.Box("CableTray", new Vector3(0f, trayY, trayZ), new Vector3(L - 0.40f, 0.008f, 0.12f), metal, NoBottom);
            kit.Box("CableTrayLip", new Vector3(0f, trayY + 0.03f, trayZ + 0.06f), new Vector3(L - 0.40f, 0.06f, 0.006f), metal);
            kit.Lathe("DeskGrommet", new Vector3(0.10f, Top, -0.33f), Quaternion.identity, new[] { new Vector2(0.03f, 0.001f), new Vector2(0.02f, 0.001f) }, 10, Sw(Hue.Black));
            // ケーブルの束。モニターの柱の脇の穴から受けへ降り、受けに沿って右と左の端へ
            var colours = new[] { Hue.CableBlack, Hue.CableBlack, Hue.CableGrey, Hue.CableBlue, Hue.CableBlack };
            for (var k = 0; k < colours.Length; k++)
            {
                var o = (k - 2) * 0.007f;
                var toRight = k < 3;
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
                kit.Tube("DeskCable", Smooth(pts, 2), 0.0045f, 4, Sw(colours[k]));
            }
            // 鍵盤とマウス（前の置き場のまま）
            var keys = Whole(Uv(KeysArea));
            var dark = Sw(Hue.PlasticGrey);
            kit.Box6("Keyboard", new Vector3(-0.05f, Top + 0.011f, 0.26f), R(2f, 2f, 0f), new Vector3(0.44f, 0.022f, 0.14f), new[] { dark, dark, dark, keys, dark, dark }, NoBottom);
            kit.Tube("KeyboardCable", Smooth(new List<Vector3> { new Vector3(-0.05f, Top + 0.012f, 0.19f), new Vector3(-0.04f, Top + 0.004f, 0.10f), new Vector3(0.06f, Top + 0.004f, -0.12f), new Vector3(0.10f, Top + 0.004f, -0.30f) }, 3), 0.003f, 4, Sw(Hue.CableBlack));
            kit.Box("MousePad", new Vector3(-0.52f, Top + 0.0015f, 0.26f), new Vector3(0.28f, 0.003f, 0.23f), Sw(Hue.PlasticGrey), NoBottom);
            kit.RoundBox("Mouse", new Vector3(-0.52f, Top + 0.02f, 0.27f), R(0f, -6f, 0f), new Vector3(0.062f, 0.034f, 0.108f), 0.02f, Sw(Hue.PianoBlack),
                (p, c) => { if (c.y > 0f) p.y -= 0.008f * (c.z + 1f) * 0.5f * c.y; return p; }, 1, 1f, NoBottom);
            kit.Decal("MouseGlow", new Vector3(-0.52f, Top + 0.0372f, 0.30f), new Vector3(0.004f, 0f, 0f), new Vector3(0f, 0f, 0.018f), new Rect(SwatchUv(Hue.LedViolet), Vector2.zero));
        }

        /// <summary>
        /// 右の卓（前が +z。置くと椅子の側を向く）。前の卓と同じ広さ（長さ 1.068・奥行き 0.44）と天板の高さ 0.769。
        /// 艶のある黒い天板は椅子の側へ 9 cm 張り出し（ジャケットがその縁から垂れる）、下につや消しの黒の引き出し二杯と取っ手、四本の脚、下の棚に本二冊
        /// </summary>
        static void SideTable(FurnitureKit kit)
        {
            const float L = 1.068f, D = 0.44f, Top = 0.769f, T = 0.03f;
            kit.RoundBox("SideTop", new Vector3(0f, Top - T * 0.5f, 0f), Quaternion.identity, new Vector3(L, T, D), 0.006f, BlackTop, null, 1, 1f);
            const float bodyDeep = D - 0.10f;
            const float bodyZ = -0.04f;
            const float bodyHigh = 0.20f;
            kit.Box6("SideBody", new Vector3(0f, Top - T - bodyHigh * 0.5f, bodyZ), Quaternion.identity, new Vector3(L - 0.04f, bodyHigh, bodyDeep), Carcass(true));
            var front = bodyZ + bodyDeep * 0.5f;
            foreach (var sx in new[] { -1f, 1f })
            {
                var cx = sx * (L - 0.04f) * 0.25f;
                kit.RoundBox("SideDrawer", new Vector3(cx, Top - T - bodyHigh * 0.5f, front + 0.009f), Quaternion.identity, new Vector3((L - 0.04f) * 0.5f - 2f * DoorGap, bodyHigh - 2f * DoorGap, 0.018f), 0.005f, Front, null, 1, 1f);
                Pull(kit, new Vector3(cx, Top - T - 0.06f, front + 0.018f), true, 0.14f);
            }
            var metal = Sw(Hue.RackBlack);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("SideLeg", new Vector3(sx * (L * 0.5f - 0.04f), (Top - T - bodyHigh) * 0.5f, bodyZ + sz * (bodyDeep * 0.5f - 0.02f)), new Vector3(0.035f, Top - T - bodyHigh, 0.035f), metal, NoBottom);
            kit.Box("SideShelf", new Vector3(0f, 0.12f, bodyZ), new Vector3(L - 0.08f, 0.018f, bodyDeep - 0.02f), Laminate);
            Book(kit, new Vector3(-0.25f, 0.129f + 0.012f, bodyZ), R(0f, 6f, 90f), new Vector3(0.024f, 0.21f, 0.15f), 9);
            Book(kit, new Vector3(-0.25f, 0.129f + 0.035f, bodyZ + 0.01f), R(0f, -8f, 90f), new Vector3(0.022f, 0.19f, 0.13f), 22);
        }

        /// <summary>机の右の床の PC（前が +z）。黒い箱、前の面の通気の溝と紫の灯りの線と電源の輪、机の側の面の暗い硝子（中の紫の扇）、脚</summary>
        static void Tower(FurnitureKit kit)
        {
            const float W = 0.24f, H = 0.52f, D = 0.46f;
            kit.RoundBox("TowerCase", new Vector3(0f, 0.02f + (H - 0.02f) * 0.5f, 0f), Quaternion.identity, new Vector3(W, H - 0.02f, D), 0.008f, Sw(Hue.RackBlack), null, 1, 1f, NoBottom);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    kit.Box("TowerFoot", new Vector3(sx * (W * 0.5f - 0.03f), 0.01f, sz * (D * 0.5f - 0.05f)), new Vector3(0.03f, 0.02f, 0.05f), Sw(Hue.Rubber), NoBottom);
            kit.Decal("TowerFront", new Vector3(0f, 0.02f + (H - 0.02f) * 0.5f, D * 0.5f + 0.001f), new Vector3(-(W - 0.03f) * 0.5f, 0f, 0f), new Vector3(0f, (H - 0.08f) * 0.5f, 0f), Uv(PcFrontArea));
            // 机の側（置くと −x 側。前が +z の時の +x）の硝子
            kit.Decal("TowerGlass", new Vector3(W * 0.5f + 0.001f, 0.02f + (H - 0.02f) * 0.5f, 0f), new Vector3(0f, 0f, -(D - 0.06f) * 0.5f), new Vector3(0f, (H - 0.08f) * 0.5f, 0f), Uv(PcSideArea));
        }

        // ---- コート掛け ----------------------------------------------------------------

        /// <summary>
        /// 床に立つコート掛け（前の Kenney の物と同じ高さ 1.54 m）。黒い重い台、黒い柱と頭の玉、下の段に ±x・±z の四本の腕（先が上へ反り、玉が付く）、
        /// 上の段に斜め四本の短い腕。上の段の北東の腕に毛糸のマフラー、下の段の東の腕に帆布のトートバッグ。
        /// **下の段の腕の形は場面 3 のジャケットの掛け方（<see cref="BuildConnect"/> の FindHook）が読む。** 腕は柱から 0.247 m、腕の先の下の面は床から 1.197 m（前の物とほぼ同じ）。
        /// 廊下の口を向く西の腕は、場面 3・5・7 でジャケットが掛かるので空けておく
        /// </summary>
        static void CoatRack(FurnitureKit kit)
        {
            var black = Sw(Hue.RackBlack);
            kit.Lathe("RackBase", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.20f, 0f), new Vector2(0.20f, 0.018f), new Vector2(0.17f, 0.034f), new Vector2(0.03f, 0.048f) }, 16, black);
            kit.Lathe("RackPole", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.018f, 0.045f), new Vector2(0.015f, 1.50f) }, 8, black);
            kit.Lathe("RackFinial", Vector3.zero, Quaternion.identity, new[] { new Vector2(0.015f, 1.50f), new Vector2(0.022f, 1.515f), new Vector2(0.019f, 1.532f), new Vector2(0f, 1.54f) }, 8, black);
            // 下の段（高さの 7〜8 割）
            var lowS = new[] { 0.012f, 0.10f, 0.19f, 0.225f, 0.238f, 0.236f };
            var lowY = new[] { 1.150f, 1.170f, 1.195f, 1.205f, 1.225f, 1.243f };
            for (var k = 0; k < 4; k++) Arm(kit, k * 90f, lowS, lowY, 0.008f, 0.011f);
            // 上の段（高さの 9 割より上）。斜めに
            var highS = new[] { 0.012f, 0.08f, 0.14f, 0.165f, 0.172f };
            var highY = new[] { 1.400f, 1.415f, 1.437f, 1.452f, 1.470f };
            for (var k = 0; k < 4; k++) Arm(kit, 45f + k * 90f, highS, highY, 0.007f, 0.010f);
            // 北東の上の腕に掛けたマフラー（腕を挟んで前と後ろへ垂れる）
            var dir = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            var across = Vector3.Cross(Vector3.up, dir).normalized;
            var hook = dir * 0.12f + Vector3.up * 1.445f;
            kit.Sheet("Scarf", (u, v) =>
            {
                var along = dir * ((u - 0.5f) * 0.16f);
                Vector3 p;
                if (v < 0.45f) p = hook + across * (0.018f + 0.01f * (0.45f - v)) + Vector3.down * ((0.45f - v) / 0.45f * 0.44f);
                else if (v < 0.55f)
                {
                    var a = (v - 0.45f) / 0.1f * Mathf.PI;
                    p = hook + across * (0.018f * Mathf.Cos(a)) + Vector3.up * (0.012f * Mathf.Sin(a));
                }
                else p = hook - across * (0.018f + 0.012f * (v - 0.55f)) + Vector3.down * ((v - 0.55f) / 0.45f * 0.52f);
                var sway = Mathf.Sin(v * 9f + u * 2f) * 0.01f;
                return p + along + dir * sway;
            }, 3, 12, Wool, true, 70f);
            // 東の下の腕のトートバッグ（持ち手の輪二本と、垂れた袋）
            var east = Vector3.right;
            var at = east * 0.20f + Vector3.up * 1.20f;
            foreach (var s in new[] { -1f, 1f })
            {
                var loop = new List<Vector3>
                {
                    at + Vector3.forward * (s * 0.012f) + Vector3.up * 0.012f,
                    at + Vector3.forward * (s * 0.05f) + Vector3.down * 0.08f + east * 0.02f * s,
                    at + Vector3.forward * (s * 0.10f) + Vector3.down * 0.22f + east * 0.01f,
                };
                kit.Tube("ToteHandle", Smooth(loop, 3), 0.006f, 4, Sw(Hue.Canvas));
                kit.Tube("ToteHandle", Smooth(new List<Vector3> { loop[0], at + Vector3.forward * (s * 0.05f) + Vector3.down * 0.08f + east * 0.05f, at + Vector3.forward * (s * 0.10f) + Vector3.down * 0.22f + east * 0.04f }, 3), 0.006f, 4, Sw(Hue.Canvas));
            }
            kit.RoundBox("Tote", at + east * 0.028f + Vector3.down * 0.40f, R(0f, 90f, 4f), new Vector3(0.34f, 0.37f, 0.06f), 0.02f, Sw(Hue.Canvas),
                (p, c) =>
                {
                    p = Puff(new Vector3(0.0f, 0.0f, 0.02f))(p, c);
                    if (c.y > 0.6f) p.z *= 0.6f;
                    p.x += 0.006f * Mathf.Sin(p.y * 30f);
                    return p;
                }, 1, 0.12f);
        }

        /// <summary>コート掛けの腕一本。yaw の向きへ、柱からの隔たり s と高さ y の点を通る筒と、先の玉</summary>
        static void Arm(FurnitureKit kit, float yaw, float[] s, float[] y, float radius, float ball)
        {
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var pts = new List<Vector3>();
            for (var i = 0; i < s.Length; i++) pts.Add(dir * s[i] + Vector3.up * y[i]);
            kit.Tube("RackArm", Smooth(pts, 2), radius, 5, Sw(Hue.RackBlack));
            var tip = pts[pts.Count - 1];
            kit.Lathe("RackArmTip", tip + Vector3.down * ball, Quaternion.identity, new[] { new Vector2(0.001f, 0f), new Vector2(ball * 0.8f, ball * 0.35f), new Vector2(ball, ball), new Vector2(ball * 0.8f, ball * 1.65f), new Vector2(0f, ball * 2f) }, 8, Sw(Hue.RackBlack));
        }

        // ---- 台所の天井の灯り ----------------------------------------------------------

        /// <summary>台所の天井の灯りの芯（灯具の原点から下へ）</summary>
        const float KitchenGlowDrop = 0.12f;

        /// <summary>台所の天井に付ける細長い灯具（長さ 1.0・幅 0.14・厚み 0.05。原点は天井に付く上の面の真ん中）。黒い枠と、下の乳白の光る板</summary>
        static void KitchenLight(FurnitureKit kit)
        {
            kit.RoundBox("KitchenLightBody", new Vector3(0f, -0.025f, 0f), Quaternion.identity, new Vector3(1.0f, 0.05f, 0.14f), 0.008f, Sw(Hue.RackBlack), null, 1, 1f);
            kit.Decal("KitchenLightDiffuser", new Vector3(0f, -0.0505f, 0f), new Vector3(-0.47f, 0f, 0f), new Vector3(0f, 0f, 0.052f), new Rect(SwatchUv(Hue.Diffuser), Vector2.zero));
        }
    }
}
