using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 公営住宅の遠景を撮るためだけに組む、ロンドンの街並み。`HalfAware/Shoot the estate backdrop` の中で組み、
    /// 16 枚を撮り終えたらその場で捨てる。シーンにもアセットにも残らない。
    ///
    /// **撮り終えたら捨てるので、頂点の数は気にしない。** 細かく組むほど画が良くなる。
    /// 窓と煙突は等間隔にする。等間隔の繰り返しが、遠くでも長屋や高層棟だと読ませる。
    ///
    /// **置くのは本物の地面の縁（<see cref="EstateFarGround"/>）より外だけ。** 内側に置くと、
    /// 板に貼ったときに足元が本物の地面に埋まる。高さは中心から仰角 12.5 度まで
    /// （<see cref="TownCap"/>）。それより高いと板の上端で切れる。
    ///
    /// **日本の町に固有の物は置かない**（設計書 9.1 節）。電柱と電線、戸建ての瓦屋根、神社の森、
    /// 給水塔、鉄塔と送電線、団地の棟の列、高架の道路の遮音壁、川の堤防、山の稜線は外した。
    ///
    /// 方角ごとの置き方。廊下（三階）の目の高さからは、中の層（隣の棟・長屋・高層棟）が地平から
    /// 5〜10 度までを塞ぐので、その上へ頭を出す高い物を、中の層の低い所の向こうへ置いた。
    /// <list type="table">
    /// <item><term>ぐるり</term><description>煙突の並ぶ煉瓦の長屋の屋根の海。升目の通りに沿って二〜三階の長屋の列、
    /// 列の前に通り、裏に細長い庭。ところどころに葉の無い街路樹</description></item>
    /// <item><term>北北東（日の方角）の奥</term><description>シティの高いビルとクレーン。霞にほとんど溶けて、輪郭だけが残る</description></item>
    /// <item><term>北東</term><description>教会の尖塔</description></item>
    /// <item><term>東</term><description>煉瓦のアーチが続く鉄道の高架を南北に。東の少し南に電車が一編成。
    /// 高架の向こうにガスタンクの骨組みが二つ</description></item>
    /// <item><term>西・北西・南</term><description>高層棟（塔状の棟と、板状の棟）。団地がいくつも固まる</description></item>
    /// </list>
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>絵の中の街並みを置いてよい一番近い距離。本物の地面の縁の少し外</summary>
        const float TownNear = EstateFarGround + 6f;
        /// <summary>中心から見た仰角の上限。度</summary>
        const float TownRise = 12.5f;
        /// <summary>長屋の屋根の海を敷く一番遠い距離。地平まで屋根が切れ目なく続くように、霞が 9 割に届く所まで</summary>
        const float TownRoofs = 480f;
        /// <summary>これより遠い長屋は窓と裏の張り出しを省く。霞が 6 割を超えて、細かさが画に出ない</summary>
        const float TownFine = 320f;

        /// <summary>中心から距離 d の所に置ける物の高さの上限</summary>
        static float TownCap(float d)
        {
            return EstateFarEye + d * Mathf.Tan(TownRise * Mathf.Deg2Rad);
        }

        /// <summary>中心からの距離と角度（+z が 0、東回り）で場所のローカルの点</summary>
        static Vector3 TownAt(float dist, float az)
        {
            return EstateFarCentre + BackdropRing.Heading(az) * dist;
        }

        static float TownReach(Vector3 p)
        {
            return new Vector2(p.x - EstateFarCentre.x, p.z - EstateFarCentre.z).magnitude;
        }

        /// <summary>中心から見た角度。0〜360</summary>
        static float TownAz(Vector3 p)
        {
            var a = Mathf.Atan2(p.x - EstateFarCentre.x, p.z - EstateFarCentre.z) * Mathf.Rad2Deg;
            return a < 0f ? a + 360f : a;
        }

        /// <summary>点から線分までの水平の距離</summary>
        static float TownOff(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            var ap = new Vector2(p.x - a.x, p.z - a.z);
            var t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (ap - ab * t).magnitude;
        }

        // 鉄道の高架。長屋が避けるので、置く前から決めておく。東を南北に通す
        static readonly Vector3 RailFrom = EstateFarCentre + new Vector3(150f, 0f, 430f);
        static readonly Vector3 RailTo = EstateFarCentre + new Vector3(178f, 0f, -430f);

        // ---- 組む ------------------------------------------------------------------

        /// <summary>
        /// 街並みを組む。作った mesh とマテリアルは <paramref name="made"/> へ入れる。捨てるのは呼ぶ側
        /// </summary>
        static GameObject EstateTown(Transform place, List<Object> made)
        {
            var root = new GameObject("EstateTown");
            root.hideFlags = HideFlags.HideAndDontSave;
            // 場所の子にはしない。シーンの中身に触れずに済む
            root.transform.SetPositionAndRotation(place.position, place.rotation);
            var t = new Town(root.transform, made);
            TownPaints(t);

            // 地面。本物の遠い地面と同じマテリアルにする。縁の所で色が揃う
            t.Use("ground", EstateLandMat());
            t["ground"].Disc(new Vector3(EstateFarCentre.x, -0.05f, EstateFarCentre.z), 3000f, 64);

            // 長屋を置かない円。x, z, 半径
            var keep = new List<Vector3>();
            TownViaduct(t);
            TownGasholders(t, keep);
            TownChurch(t, keep);
            TownCity(t);
            TownTowers(t, keep);
            TownRoofSea(t, keep);
            t.Emit();
            return root;
        }

        static void TownPaints(Town t)
        {
            // ロンドンのストック煉瓦（黄色がかった灰茶）と、赤煉瓦
            t.Paint("stock", new Color(0.54f, 0.45f, 0.33f));
            t.Paint("stockDark", new Color(0.44f, 0.37f, 0.28f));
            t.Paint("brick", new Color(0.48f, 0.29f, 0.22f));
            t.Paint("sooty", new Color(0.36f, 0.24f, 0.20f));
            t.Paint("slate", new Color(0.25f, 0.26f, 0.29f));
            t.Paint("trim", new Color(0.76f, 0.75f, 0.71f));
            t.Paint("pot", new Color(0.56f, 0.32f, 0.22f));
            t.Paint("glass", new Color(0.11f, 0.12f, 0.15f), 0.45f);
            t.Paint("shade", new Color(0.16f, 0.17f, 0.19f));
            t.Paint("concrete", new Color(0.58f, 0.58f, 0.56f));
            t.Paint("concreteDark", new Color(0.46f, 0.47f, 0.48f));
            t.Paint("stone", new Color(0.64f, 0.62f, 0.57f));
            t.Paint("lead", new Color(0.40f, 0.42f, 0.44f));
            t.Paint("iron", new Color(0.20f, 0.23f, 0.22f));
            t.Paint("train", new Color(0.80f, 0.80f, 0.78f));
            t.Paint("band", new Color(0.86f, 0.44f, 0.12f));
            t.Paint("city", new Color(0.52f, 0.58f, 0.64f), 0.6f);
            t.Paint("cityDark", new Color(0.34f, 0.38f, 0.43f), 0.6f);
            t.Paint("crane", new Color(0.84f, 0.62f, 0.10f));
            t.Paint("twig", new Color(0.40f, 0.36f, 0.32f));
            t.Paint("grass", new Color(0.30f, 0.36f, 0.22f));
            t.Paint("road", new Color(0.21f, 0.21f, 0.22f));
        }

        // ---- 鉄道の高架 --------------------------------------------------------------

        /// <summary>
        /// 煉瓦のアーチが続く鉄道の高架。9 m ごとに橋脚を立て、あいだに半円のアーチを一つずつ。
        /// アーチの輪は短い箱を半円に並べ、その上の壁は輪の上から床まで細い柱を並べて埋める。
        /// 床の上に低い胸壁と、架線の無い線路。東の少し南に電車が一編成
        /// </summary>
        static void TownViaduct(Town t)
        {
            const float deck = 9f;
            const float bay = 9f;
            const float pier = 1.6f;
            const float wide = 9.5f;
            const float spring = 4.4f;
            var along = RailTo - RailFrom;
            var length = new Vector2(along.x, along.z).magnitude;
            var dir = along / length;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var side = Vector3.Cross(Vector3.up, dir);
            var radius = (bay - pier) * 0.5f;
            const int steps = 8;
            for (var s = 0f; s < length; s += bay)
            {
                var at = RailFrom + dir * s;
                if (TownReach(at) < TownNear + 10f) continue;
                // 橋脚
                t["sooty"].Box(at + Vector3.up * (deck * 0.5f), new Vector3(wide, deck, pier), rot);
                // アーチの輪と、その上の壁
                var mid = at + dir * (bay * 0.5f);
                for (var k = 0; k < steps; k++)
                {
                    var a0 = Mathf.PI * k / steps;
                    var a1 = Mathf.PI * (k + 1) / steps;
                    var p0 = mid - dir * (Mathf.Cos(a0) * radius) + Vector3.up * (spring + Mathf.Sin(a0) * radius);
                    var p1 = mid - dir * (Mathf.Cos(a1) * radius) + Vector3.up * (spring + Mathf.Sin(a1) * radius);
                    // 輪の一片。高架の幅いっぱいの箱を、弧に沿って傾ける
                    var f = p1 - p0;
                    t["brick"].Box((p0 + p1) * 0.5f, new Vector3(wide, 0.7f, f.magnitude + 0.1f), Quaternion.LookRotation(f, Vector3.Cross(f, side)));
                    var top = Mathf.Max(p0.y, p1.y);
                    var c = (p0 + p1) * 0.5f;
                    var w = Vector3.Distance(new Vector3(p0.x, 0f, p0.z), new Vector3(p1.x, 0f, p1.z)) + 0.05f;
                    t["brick"].Box(new Vector3(c.x, (top + deck) * 0.5f, c.z), new Vector3(wide, deck - top, Mathf.Max(w, 0.2f)), rot);
                }
                // 床と胸壁
                t["sooty"].Box(mid + Vector3.up * (deck + 0.3f), new Vector3(wide + 0.4f, 0.6f, bay), rot);
                t["brick"].Box(mid + Vector3.up * (deck + 1.1f) + side * (wide * 0.5f), new Vector3(0.4f, 1.0f, bay), rot);
                t["brick"].Box(mid + Vector3.up * (deck + 1.1f) - side * (wide * 0.5f), new Vector3(0.4f, 1.0f, bay), rot);
            }

            // 電車。高架が長屋の屋根の上に見える所（中心から見て東の少し南）に一編成、五両
            var best = 0f;
            var bestGap = float.MaxValue;
            for (var s = 0f; s < length; s += 5f)
            {
                var gap = Mathf.Abs(Mathf.DeltaAngle(TownAz(RailFrom + dir * s), 96f));
                if (gap < bestGap) { bestGap = gap; best = s; }
            }
            for (var c = 0; c < 5; c++)
            {
                var at = RailFrom + dir * (best + (c - 2f) * 20.4f) + side * 1.9f + Vector3.up * (deck + 0.6f);
                t["train"].Box(at + Vector3.up * 1.9f, new Vector3(2.8f, 3.4f, 20f), rot);
                t["glass"].Box(at + Vector3.up * 2.35f, new Vector3(2.84f, 0.9f, 18.6f), rot);
                t["band"].Box(at + Vector3.up * 1.35f, new Vector3(2.86f, 0.35f, 20f), rot);
                // 戸。片側に三つずつ
                for (var d = 0; d < 3; d++)
                    t["band"].Box(at + Vector3.up * 1.95f + dir * (-6f + d * 6f), new Vector3(2.88f, 2.0f, 1.3f), rot);
            }
        }

        // ---- ガスタンクの骨組み ----------------------------------------------------------

        /// <summary>
        /// ガスタンクの骨組み（もう使われていない、円い鉄の枠だけのもの）を二つ。
        /// 円に並べた柱と、三段の輪の梁と、柱のあいだの斜めの筋交い
        /// </summary>
        static void TownGasholders(Town t, List<Vector3> keep)
        {
            TownGasholder(t, TownAt(232f, 58f), 30f, 16, keep);
            TownGasholder(t, TownAt(268f, 70f), 22f, 12, keep);
        }

        static void TownGasholder(Town t, Vector3 foot, float radius, int columns, List<Vector3> keep)
        {
            var high = Mathf.Min(40f, TownCap(TownReach(foot) - radius) - 2f);
            keep.Add(new Vector3(foot.x, radius + 12f, foot.z));
            var s = t["iron"];
            const int tiers = 3;
            for (var i = 0; i < columns; i++)
            {
                var a0 = i * Mathf.PI * 2f / columns;
                var a1 = (i + 1) * Mathf.PI * 2f / columns;
                var p0 = foot + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = foot + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                s.Beam(p0, p0 + Vector3.up * high, 0.9f);
                for (var k = 1; k <= tiers; k++)
                {
                    var y = high * k / tiers;
                    s.Beam(p0 + Vector3.up * y, p1 + Vector3.up * y, k == tiers ? 0.9f : 0.6f);
                    var y0 = high * (k - 1) / tiers;
                    s.Beam(p0 + Vector3.up * y0, p1 + Vector3.up * y, 0.3f);
                    s.Beam(p1 + Vector3.up * y0, p0 + Vector3.up * y, 0.3f);
                }
            }
            // 足元の低い囲い
            t["sooty"].Disc(foot + Vector3.up * 0.02f, radius, 24);
        }

        // ---- 教会の尖塔 ------------------------------------------------------------------

        /// <summary>教会。石の身廊と、四角い塔の上の八角の尖塔。尖塔は向きを変えた細い箱を重ねて窄める</summary>
        static void TownChurch(Town t, List<Vector3> keep)
        {
            var at = TownAt(205f, 38f);
            keep.Add(new Vector3(at.x, 26f, at.z));
            var yaw = 20f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => at + rot * new Vector3(x, y, z);
            var cap = TownCap(TownReach(at) - 6f) - 1f;
            // 身廊
            t["stone"].Box(p(0f, 7f, -14f), new Vector3(12f, 14f, 30f), rot);
            t["slate"].Gable(p(0f, 14f, -14f), 30f, 13f, 6f, yaw + 90f);
            // 塔
            const float tower = 22f;
            t["stone"].Box(p(0f, tower * 0.5f, 3f), new Vector3(7f, tower, 7f), rot);
            for (var k = 0; k < 4; k++)
                t["stone"].Box(p((k % 2 * 2 - 1) * 3.6f, tower + 1.2f, 3f + (k / 2 * 2 - 1) * 3.6f), new Vector3(1.0f, 2.4f, 1.0f), rot);
            t["shade"].Face(p(0f, tower - 4f, -0.55f), rot * Vector3.back, 1.4f, 3.2f);
            // 尖塔。八つの段で窄める
            var spire = Mathf.Max(8f, cap - tower);
            for (var k = 0; k < 8; k++)
            {
                var y0 = tower + spire * k / 8f;
                var w = Mathf.Lerp(5.6f, 0.4f, (k + 0.5f) / 8f);
                for (var r = 0; r < 2; r++)
                    t["lead"].Box(p(0f, y0 + spire / 16f, 3f), new Vector3(w, spire / 8f + 0.05f, w), Quaternion.Euler(0f, yaw + r * 45f, 0f));
            }
        }

        // ---- シティ ----------------------------------------------------------------------

        /// <summary>
        /// シティの高いビル。北北東の奥、日の方角に固める。ガラスの箱の塔と、先の尖った三角の塔、
        /// 卵形の塔、段々の塔。脇に塔の形のクレーンを四本。霞にほとんど溶けて、日を背にした輪郭だけが残る
        /// </summary>
        static void TownCity(Town t)
        {
            var rnd = new System.Random(31);
            var placed = new List<Vector3>();
            for (var n = 0; n < 200 && placed.Count < 22; n++)
            {
                var az = 8f + (float)rnd.NextDouble() * 34f;
                var dist = 360f + (float)rnd.NextDouble() * 120f;
                var at = TownAt(dist, az);
                var w = 16f + (float)rnd.NextDouble() * 16f;
                var r = w * 0.75f;
                var clash = false;
                foreach (var q in placed)
                    if (new Vector2(q.x - at.x, q.z - at.z).magnitude < q.y + r + 3f) { clash = true; break; }
                if (clash) continue;
                var cap = TownCap(dist - r) - 1f;
                var u = (float)rnd.NextDouble();
                var high = Mathf.Min(cap, 40f + u * u * 70f);
                var kind = placed.Count == 3 ? 1 : placed.Count == 7 ? 2 : placed.Count % 5 == 4 ? 3 : 0;
                TownCityTower(t, at, (float)rnd.NextDouble() * 40f, w, high, kind);
                placed.Add(new Vector3(at.x, r, at.z));
            }
            // クレーン
            for (var k = 0; k < 4; k++)
            {
                var dist = 380f + k * 22f;
                var at = TownAt(dist, 14f + k * 8.5f);
                var high = TownCap(dist) - 3f;
                TownCrane(t, at, high, 30f + k * 50f);
            }
        }

        /// <summary>
        /// ビルを一つ。kind 0 はガラスの箱（階ごとの横の帯）、1 は先の尖った三角の塔、
        /// 2 は丸い卵形の塔、3 は上で段になって細る塔
        /// </summary>
        static void TownCityTower(Town t, Vector3 foot, float yaw, float w, float high, int kind)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            if (kind == 1)
            {
                // 三角の塔。階ごとに窄める
                for (var k = 0; k < 16; k++)
                {
                    var y0 = high * k / 16f;
                    var s = Mathf.Lerp(w * 1.2f, 1.2f, (k + 0.5f) / 16f);
                    t["city"].Box(foot + Vector3.up * (y0 + high / 32f), new Vector3(s, high / 16f + 0.05f, s), rot);
                }
                return;
            }
            if (kind == 2)
            {
                // 卵形。丸は向きを変えた箱を重ねて出す
                for (var k = 0; k < 12; k++)
                {
                    var y0 = high * k / 12f;
                    var v = (k + 0.5f) / 12f;
                    var s = w * (0.55f + 0.55f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 1f, v))) * (1f - v * 0.35f);
                    for (var r = 0; r < 2; r++)
                        t["cityDark"].Box(foot + Vector3.up * (y0 + high / 24f), new Vector3(s, high / 12f + 0.05f, s), Quaternion.Euler(0f, yaw + r * 45f, 0f));
                }
                return;
            }
            var body = kind == 3 ? high * 0.7f : high;
            t[kind == 3 ? "cityDark" : "city"].Box(foot + Vector3.up * (body * 0.5f), new Vector3(w, body, w * 0.8f), rot);
            if (kind == 3)
                t["cityDark"].Box(foot + Vector3.up * (body + (high - body) * 0.5f), new Vector3(w * 0.6f, high - body, w * 0.5f), rot);
            // 階ごとの横の帯
            for (var y = 4f; y < body - 1f; y += 4f)
                t["glass"].Box(foot + Vector3.up * y, new Vector3(w + 0.1f, 0.6f, w * 0.8f + 0.1f), rot);
        }

        /// <summary>塔の形のクレーン。細い柱と、水平の腕と、後ろの錘</summary>
        static void TownCrane(Town t, Vector3 foot, float high, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var s = t["crane"];
            s.Box(foot + Vector3.up * (high * 0.5f), new Vector3(1.6f, high, 1.6f), rot);
            s.Box(foot + Vector3.up * (high - 1f) + rot * Vector3.forward * 18f, new Vector3(1.2f, 1.2f, 44f), rot);
            s.Box(foot + Vector3.up * (high + 3f), new Vector3(1.0f, 6f, 1.0f), rot);
            t["concreteDark"].Box(foot + Vector3.up * (high - 1.5f) - rot * Vector3.forward * 7f, new Vector3(3f, 2.5f, 4f), rot);
        }

        // ---- 高層棟 --------------------------------------------------------------------

        /// <summary>
        /// 高層棟。西から北西と、南に、団地をいくつか固める。塔状の棟（正方形の平面に二十階前後）と、
        /// 板状の棟（長い面にデッキの帯が横に通る）
        /// </summary>
        static void TownTowers(Town t, List<Vector3> keep)
        {
            var spots = new[]
            {
                // 距離、角度、階、塔か板か
                new Vector4(150f, 262f, 18f, 0f), new Vector4(185f, 248f, 21f, 0f), new Vector4(215f, 275f, 22f, 0f),
                new Vector4(175f, 300f, 9f, 1f), new Vector4(250f, 312f, 24f, 0f), new Vector4(230f, 330f, 20f, 0f),
                new Vector4(170f, 205f, 8f, 1f), new Vector4(210f, 190f, 19f, 0f), new Vector4(260f, 222f, 23f, 0f),
                new Vector4(190f, 138f, 16f, 0f), new Vector4(290f, 160f, 22f, 0f), new Vector4(120f, 235f, 7f, 1f),
            };
            var rnd = new System.Random(53);
            foreach (var s in spots)
            {
                var at = TownAt(s.x, s.y);
                var yaw = (float)rnd.NextDouble() * 30f - 15f;
                if (s.w < 0.5f)
                {
                    const float wide = 18f;
                    var floors = (int)s.z;
                    while (floors > 6 && 1.2f + floors * 2.7f + 3f > TownCap(s.x - wide * 0.7f)) floors--;
                    TownPoint(t, at, yaw, wide, floors, rnd);
                    keep.Add(new Vector3(at.x, 15f, at.z));
                }
                else
                {
                    var len = 60f + rnd.Next(0, 3) * 12f;
                    var floors = (int)s.z;
                    while (floors > 4 && floors * 2.8f + 1f > TownCap(s.x - len * 0.5f)) floors--;
                    // 長手を中心から見て横に向け、デッキの面を中心の側へ
                    TownDeckBlock(t, at, s.y + yaw, len, floors);
                    keep.Add(new Vector3(at.x, len * 0.5f + 4f, at.z));
                }
            }
        }

        /// <summary>塔状の棟。コンクリートの床の帯が階ごとに横に通り、各面に四つずつ窓。屋上に機械室</summary>
        static void TownPoint(Town t, Vector3 foot, float yaw, float wide, int floors, System.Random rnd)
        {
            const float storey = 2.7f;
            var high = 1.2f + floors * storey;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var skin = rnd.NextDouble() < 0.5 ? "concrete" : "concreteDark";
            t[skin].Box(foot + Vector3.up * (high * 0.5f), new Vector3(wide, high, wide), rot);
            var faces = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            for (var f = 0; f < floors; f++)
            {
                var y = 1.2f + f * storey;
                t["concrete"].Box(foot + Vector3.up * y, new Vector3(wide + 0.3f, 0.3f, wide + 0.3f), rot);
                foreach (var n0 in faces)
                {
                    var n = rot * n0;
                    var across = Vector3.Cross(Vector3.up, -n);
                    for (var i = 0; i < 4; i++)
                        t["glass"].Face(foot + n * (wide * 0.5f + 0.03f) + across * (-wide * 0.5f + wide * (i + 0.5f) / 4f) + Vector3.up * (y + 1.3f),
                            n, wide * 0.17f, 1.3f);
                }
            }
            t["concreteDark"].Box(foot + Vector3.up * (high + 1.6f), new Vector3(wide * 0.4f, 3.2f, wide * 0.3f), rot);
        }

        /// <summary>板状の棟。長い面に三階ごとのデッキの明るい帯と、その奥の暗い帯。両端に階段の塔</summary>
        static void TownDeckBlock(Town t, Vector3 foot, float yaw, float len, int floors)
        {
            const float deep = 11f;
            const float storey = 2.8f;
            var high = floors * storey + 0.6f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => foot + rot * new Vector3(x, y, z);
            t["brick"].Box(p(0f, high * 0.5f, 0f), new Vector3(len, high, deep), rot);
            for (var f = 0; f < floors; f++)
            {
                var y = f * storey;
                if (f % 2 == 0)
                {
                    // デッキ。明るい腰壁の帯と、奥の暗がり
                    t["shade"].Box(p(0f, y + storey * 0.55f, -deep * 0.5f - 0.02f), new Vector3(len - 1f, storey * 0.7f, 0.1f), rot);
                    t["concrete"].Box(p(0f, y + 0.55f, -deep * 0.5f - 1.0f), new Vector3(len, 1.1f, 0.3f), rot);
                }
                else
                {
                    for (var x = -len * 0.5f + 2f; x < len * 0.5f - 2f; x += 3.6f)
                        t["glass"].Face(p(x, y + 1.5f, -deep * 0.5f - 0.03f), rot * Vector3.back, 2.2f, 1.2f);
                }
                for (var x = -len * 0.5f + 2f; x < len * 0.5f - 2f; x += 3.6f)
                    t["glass"].Face(p(x, y + 1.5f, deep * 0.5f + 0.03f), rot * Vector3.forward, 2.2f, 1.2f);
            }
            t["concrete"].Box(p(-len * 0.5f - 2f, (high + 2f) * 0.5f, 0f), new Vector3(4f, high + 2f, 6f), rot);
            t["concrete"].Box(p(len * 0.5f + 2f, (high + 2f) * 0.5f, 0f), new Vector3(4f, high + 2f, 6f), rot);
        }

        // ---- 煙突の並ぶ長屋の屋根の海 --------------------------------------------------------

        /// <summary>
        /// 升目の通りに沿って長屋の列を敷き詰める。列は 26 m ごと（前に通り、裏に細長い庭）、
        /// 長さ 20〜60 m ごとに横道で切る。二階建てが多く、三階建てが混じる。
        /// 他の物（高架・ガスタンク・教会・高層棟）の近くと、中心から <see cref="TownNear"/> より内は空ける
        /// </summary>
        static void TownRoofSea(Town t, List<Vector3> keep)
        {
            var rnd = new System.Random(23);
            const float gridYaw = 14f;
            var rot = Quaternion.Euler(0f, gridYaw, 0f);
            const float pitch = 26f;
            for (var j = -20; j <= 20; j++)
            {
                var z = j * pitch;
                var x = -TownRoofs - 20f + (float)rnd.NextDouble() * 20f;
                while (x < TownRoofs)
                {
                    var len = 20f + rnd.Next(0, 5) * 10f;
                    var mid = EstateFarCentre + rot * new Vector3(x + len * 0.5f, 0f, z);
                    var from = EstateFarCentre + rot * new Vector3(x, 0f, z);
                    var to = EstateFarCentre + rot * new Vector3(x + len, 0f, z);
                    x += len + 7f + rnd.Next(0, 2) * 6f;
                    // 列のどこかが近すぎる、遠すぎる、他の物に掛かるなら置かない
                    if (Mathf.Min(TownReach(from), Mathf.Min(TownReach(to), TownReach(mid))) < TownNear + 8f) continue;
                    if (TownOff(EstateFarCentre, from, to) < TownNear + 8f) continue;
                    if (TownReach(mid) > TownRoofs) continue;
                    if (TownOff(mid, RailFrom, RailTo) < len * 0.5f + 12f) continue;
                    var blocked = false;
                    foreach (var k in keep)
                        if (TownOff(new Vector3(k.x, 0f, k.z), from, to) < k.y) { blocked = true; break; }
                    if (blocked) continue;
                    var storeys = rnd.NextDouble() < 0.28 ? 3 : 2;
                    var skin = rnd.NextDouble() < 0.7 ? "stock" : (rnd.NextDouble() < 0.5 ? "brick" : "stockDark");
                    TownTerrace(t, mid, gridYaw, len, storeys, skin, TownReach(mid) < TownFine, rnd);
                }
            }
        }

        /// <summary>
        /// 長屋を一列。<paramref name="mid"/> は列の表の壁の真ん中。表は向き yaw の -z（手前）、裏に庭。
        /// 壁の塊にスレートの切妻を一本通し、5 m ごとの戸境に煙突の束。表に階ごとの窓と白い帯、
        /// 一階に張り出し窓。裏に一軒ずつ細長い張り出し（アウトリガー）
        /// </summary>
        static void TownTerrace(Town t, Vector3 mid, float yaw, float len, int storeys, string skin, bool fine, System.Random rnd)
        {
            const float deep = 9f;
            const float house = 5f;
            var eaves = storeys * 3f + 0.4f;
            var ridge = eaves + 2.8f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => mid + rot * new Vector3(x, y, z);
            var front = rot * Vector3.back;
            var back = rot * Vector3.forward;

            // 前の通りと、裏の庭
            t["road"].Flat(p(0f, 0.02f, -deep * 0.5f - 7f), len + 8f, 8f, yaw);
            t["grass"].Flat(p(0f, 0.02f, deep * 0.5f + 7f), len, 9f, yaw);

            t[skin].Box(p(0f, eaves * 0.5f, 0f), new Vector3(len, eaves, deep), rot);
            t["slate"].Gable(p(0f, eaves, 0f), len + 0.3f, deep + 0.6f, 2.8f, yaw);
            t["trim"].Box(p(0f, eaves - 0.15f, -deep * 0.5f - 0.02f), new Vector3(len, 0.3f, 0.08f), rot);
            if (storeys > 1)
                t["trim"].Box(p(0f, 3.1f, -deep * 0.5f - 0.02f), new Vector3(len, 0.14f, 0.06f), rot);

            var houses = Mathf.Max(1, Mathf.RoundToInt(len / house));
            var step = len / houses;
            for (var h = 0; h <= houses; h++)
            {
                var x = -len * 0.5f + h * step;
                TownChimney(t, p(x, ridge, 0f), rot, skin);
                if (h == houses) break;
                if (!fine) continue;
                var c = x + step * 0.5f;
                // 表の窓。一階は張り出し窓、上の階は二つずつ
                t[skin].Box(p(c - step * 0.14f, 1.4f, -deep * 0.5f - 0.35f), new Vector3(step * 0.44f, 2.8f, 0.7f), rot);
                t["glass"].Face(p(c - step * 0.14f, 1.6f, -deep * 0.5f - 0.72f), front, step * 0.36f, 1.5f);
                t["trim"].Box(p(c - step * 0.14f, 2.9f, -deep * 0.5f - 0.35f), new Vector3(step * 0.48f, 0.2f, 0.78f), rot);
                t["shade"].Face(p(c + step * 0.3f, 1.15f, -deep * 0.5f - 0.03f), front, 0.95f, 2.2f);
                for (var s = 1; s < storeys; s++)
                    for (var i = 0; i < 2; i++)
                        t["glass"].Face(p(c + (i * 2 - 1) * step * 0.2f, s * 3f + 1.6f, -deep * 0.5f - 0.03f), front, 0.9f, 1.6f);
                // 裏の張り出し。二階建て、片流れの屋根
                const float outDeep = 4.5f;
                var outHigh = Mathf.Min(eaves, 6.2f);
                t[skin].Box(p(c - step * 0.22f, outHigh * 0.5f, deep * 0.5f + outDeep * 0.5f), new Vector3(step * 0.46f, outHigh, outDeep), rot);
                t["slate"].Box(p(c - step * 0.22f, outHigh + 0.25f, deep * 0.5f + outDeep * 0.5f), new Vector3(step * 0.5f, 0.5f, outDeep + 0.3f), rot);
                for (var s = 0; s < storeys; s++)
                    t["glass"].Face(p(c + step * 0.2f, s * 3f + 1.6f, deep * 0.5f + 0.03f), back, 0.9f, 1.4f);
            }
            // 街路樹。四列に一本くらい
            if (rnd.NextDouble() < 0.3)
                TownTree(t, p((float)rnd.NextDouble() * len - len * 0.5f, 0f, -deep * 0.5f - 3.5f), 10f + (float)rnd.NextDouble() * 5f, rnd);
        }

        /// <summary>戸境の煙突の束。棟の上へ突き出す煉瓦の角柱と、素焼きの煙突を四本</summary>
        static void TownChimney(Town t, Vector3 ridgeAt, Quaternion rot, string skin)
        {
            t[skin].Box(ridgeAt + Vector3.up * 0.4f, new Vector3(0.8f, 2.4f, 1.6f), rot);
            t["trim"].Box(ridgeAt + Vector3.up * 1.62f, new Vector3(0.9f, 0.14f, 1.7f), rot);
            for (var i = 0; i < 4; i++)
                t["pot"].Box(ridgeAt + rot * new Vector3(0f, 1.95f, -0.57f + i * 0.38f), new Vector3(0.24f, 0.55f, 0.24f), rot);
        }

        /// <summary>葉の無い街路樹を一本。幹と、ずらして重ねた細い枝の塊</summary>
        static void TownTree(Town t, Vector3 foot, float high, System.Random rnd)
        {
            t["twig"].Box(foot + Vector3.up * (high * 0.3f), new Vector3(0.5f, high * 0.6f, 0.5f), Quaternion.identity);
            var spread = high * 0.42f;
            for (var k = 0; k < 3; k++)
            {
                var y = high * (0.55f + k * 0.16f);
                var s = spread * (1f - k * 0.25f);
                var yaw = (float)rnd.NextDouble() * 90f;
                t["twig"].Box(foot + new Vector3(((float)rnd.NextDouble() - 0.5f) * 1.5f, y, ((float)rnd.NextDouble() - 0.5f) * 1.5f),
                    new Vector3(s, high * 0.22f, s * 0.9f), Quaternion.Euler(0f, yaw, 0f));
            }
        }

        // ---- 入れ物 ----------------------------------------------------------------

        /// <summary>マテリアルごとの <see cref="Sketch"/> を束ねる。作った物はすべて made へ</summary>
        sealed class Town
        {
            readonly Transform root;
            readonly List<Object> made;
            readonly Dictionary<string, Sketch> parts = new Dictionary<string, Sketch>();
            readonly Dictionary<string, Material> paints = new Dictionary<string, Material>();

            public Town(Transform root, List<Object> made)
            {
                this.root = root;
                this.made = made;
            }

            public Sketch this[string name]
            {
                get
                {
                    Sketch s;
                    if (!parts.TryGetValue(name, out s)) { s = new Sketch(); parts[name] = s; }
                    return s;
                }
            }

            /// <summary>灯りを受けるマテリアルを一枚作る。アセットにはしない</summary>
            public void Paint(string name, Color col, float smooth = 0.08f)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.hideFlags = HideFlags.HideAndDontSave;
                m.name = "Town" + name;
                m.SetColor("_BaseColor", col);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", 0f);
                made.Add(m);
                paints[name] = m;
            }

            /// <summary>既にあるマテリアルをそのまま使う。捨てない</summary>
            public void Use(string name, Material m)
            {
                paints[name] = m;
            }

            public void Emit()
            {
                foreach (var kv in parts)
                {
                    Material m;
                    if (!paints.TryGetValue(kv.Key, out m) || m == null) continue;
                    var mesh = kv.Value.Bake("Town" + kv.Key);
                    if (mesh == null) continue;
                    made.Add(mesh);
                    var go = new GameObject("Town" + kv.Key);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = m;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
        }

        /// <summary>
        /// 面を溜めて mesh にする、撮るためだけの入れ物。<see cref="Bank"/> と違ってアセットに書き出さない。
        /// 面の四隅は <see cref="Bank"/> と同じく、表から見て右下・左下・左上・右上の順（右回り）
        /// </summary>
        sealed class Sketch
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                var i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            /// <summary>斜面や地面のような四角。隅は <see cref="Quad"/> と同じ順に渡す</summary>
            public void Slope(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                // 上を向く面は、上から見て右回りになるように渡す。逆なら裏返す
                var n = Vector3.Cross(b - a, c - a);
                if (n.y < 0f) Quad(d, c, b, a);
                else Quad(a, b, c, d);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            /// <summary>向きを持つ箱</summary>
            public void Box(Vector3 centre, Vector3 size, Quaternion rot)
            {
                var h = size * 0.5f;
                System.Func<float, float, float, Vector3> c = (sx, sy, sz) =>
                    centre + rot * new Vector3(h.x * sx, h.y * sy, h.z * sz);
                Quad(c(1, -1, 1), c(1, -1, -1), c(1, 1, -1), c(1, 1, 1));
                Quad(c(-1, -1, -1), c(-1, -1, 1), c(-1, 1, 1), c(-1, 1, -1));
                Quad(c(-1, 1, 1), c(1, 1, 1), c(1, 1, -1), c(-1, 1, -1));
                Quad(c(-1, -1, -1), c(1, -1, -1), c(1, -1, 1), c(-1, -1, 1));
                Quad(c(-1, -1, 1), c(1, -1, 1), c(1, 1, 1), c(-1, 1, 1));
                Quad(c(1, -1, -1), c(-1, -1, -1), c(-1, 1, -1), c(1, 1, -1));
            }

            /// <summary>二点を結ぶ角材</summary>
            public void Beam(Vector3 a, Vector3 b, float thick)
            {
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) return;
                var up = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
                Box((a + b) * 0.5f, new Vector3(thick, thick, d.magnitude), Quaternion.LookRotation(d, up));
            }

            /// <summary>
            /// 外を向いた面に貼る四角（窓・戸）。<paramref name="normal"/> は面の向き
            /// </summary>
            public void Face(Vector3 centre, Vector3 normal, float w, float h)
            {
                var n = normal.normalized;
                var right = Vector3.Cross(Vector3.up, -n).normalized * (w * 0.5f);
                var up = Vector3.up * (h * 0.5f);
                Quad(centre + right - up, centre - right - up, centre - right + up, centre + right + up);
            }

            /// <summary>
            /// 切妻の屋根。<paramref name="eave"/> は軒の高さの真ん中、長手は向き yaw の x
            /// </summary>
            public void Gable(Vector3 eave, float len, float wid, float rise, float yaw)
            {
                var rot = Quaternion.Euler(0f, yaw, 0f);
                var l = len * 0.5f;
                var w = wid * 0.5f;
                System.Func<float, float, float, Vector3> p = (x, y, z) => eave + rot * new Vector3(x, y, z);
                Quad(p(-l, 0f, w), p(l, 0f, w), p(l, rise, 0f), p(-l, rise, 0f));
                Quad(p(l, 0f, -w), p(-l, 0f, -w), p(-l, rise, 0f), p(l, rise, 0f));
                Tri(p(l, 0f, w), p(l, 0f, -w), p(l, rise, 0f));
                Tri(p(-l, 0f, -w), p(-l, 0f, w), p(-l, rise, 0f));
                Quad(p(-l, 0f, -w), p(l, 0f, -w), p(l, 0f, w), p(-l, 0f, w));
            }

            /// <summary>地面に寝かせた四角。上を向く</summary>
            public void Flat(Vector3 centre, float w, float d, float yaw)
            {
                var rot = Quaternion.Euler(0f, yaw, 0f);
                var a = centre + rot * new Vector3(-w * 0.5f, 0f, d * 0.5f);
                var b = centre + rot * new Vector3(w * 0.5f, 0f, d * 0.5f);
                var c = centre + rot * new Vector3(w * 0.5f, 0f, -d * 0.5f);
                var e = centre + rot * new Vector3(-w * 0.5f, 0f, -d * 0.5f);
                Quad(a, b, c, e);
            }

            /// <summary>地面の円盤。上を向く</summary>
            public void Disc(Vector3 centre, float radius, int sides)
            {
                for (var k = 0; k < sides; k++)
                {
                    var a0 = k * 360f / sides;
                    var a1 = (k + 1) * 360f / sides;
                    var p0 = centre + BackdropRing.Heading(a0) * radius;
                    var p1 = centre + BackdropRing.Heading(a1) * radius;
                    // 上から見て右回り（角度の増える向き）が表
                    Tri(centre, p0, p1);
                }
            }

            public Mesh Bake(string name)
            {
                if (tris.Count == 0) return null;
                var mesh = new Mesh();
                mesh.name = name;
                mesh.hideFlags = HideFlags.HideAndDontSave;
                if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
