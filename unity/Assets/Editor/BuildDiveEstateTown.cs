using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 団地の遠景を撮るためだけに組む街並み。`HalfAware/Shoot the estate backdrop` の中で組み、
    /// 16 枚を撮り終えたらその場で捨てる。シーンにもアセットにも残らない。
    ///
    /// **撮り終えたら捨てるので、頂点の数は気にしない。** 細かく組むほど画が良くなる。
    /// 窓の並びは等間隔にする。等間隔の繰り返しが、遠くでも団地や街だと読ませる。
    ///
    /// **置くのは本物の地面の縁（<see cref="EstateFarGround"/>）より外だけ。** 内側に置くと、
    /// 板に貼ったときに足元が本物の地面に埋まる。高さは中心から仰角 12.5 度まで
    /// （<see cref="TownCap"/>）。それより高いと板の上端で切れる。
    ///
    /// 方角ごとの置き方は、団地の外に実際にありそうな形に寄せた。
    /// <list type="table">
    /// <item><term>西・北西・南西</term><description>同じ団地の続き。平行に並ぶ五階建ての棟の列と、奥に十一階建て。給水塔</description></item>
    /// <item><term>北東（日の方角）</term><description>駅前。高さのまちまちなビルが固まる。日を背にした影の塊になる</description></item>
    /// <item><term>東〜南</term><description>戸建ての屋根の並び。電柱と電線、神社の森、学校、中層のマンション</description></item>
    /// <item><term>東の奥</term><description>高架の線路と電車、工場の鋸屋根と煙突、川の堤防の線</description></item>
    /// <item><term>南</term><description>高架の道路。遮音壁が一本の帯になる</description></item>
    /// <item><term>北と南西</term><description>鉄塔と送電線が二筋</description></item>
    /// <item><term>いちばん奥</term><description>低い山の稜線。霞にほとんど溶けて、形だけが残る</description></item>
    /// </list>
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>絵の中の街並みを置いてよい一番近い距離。本物の地面の縁の少し外</summary>
        const float TownNear = EstateFarGround + 6f;
        /// <summary>中心から見た仰角の上限。度</summary>
        const float TownRise = 12.5f;

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

        // 高架の線路と高架の道路。家を避けるので、置く前から決めておく
        static readonly Vector3 RailFrom = EstateFarCentre + new Vector3(130f, 0f, 360f);
        static readonly Vector3 RailTo = EstateFarCentre + new Vector3(215f, 0f, -330f);
        static readonly Vector3 RoadFrom = EstateFarCentre + new Vector3(-460f, 0f, -205f);
        static readonly Vector3 RoadTo = EstateFarCentre + new Vector3(460f, 0f, -250f);

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

            var keep = new List<Vector4>();   // 家を置かない円。x, z, 半径
            TownDanchi(t);
            TownCentre(t);
            TownRail(t);
            TownRoad(t);
            TownPylons(t);
            TownWater(t);
            TownSchool(t, keep);
            TownFactory(t, keep);
            TownMansions(t, keep);
            TownShrine(t, keep);
            TownHouses(t, keep);
            TownLevee(t);
            TownHills(t);
            t.Emit();
            return root;
        }

        static void TownPaints(Town t)
        {
            t.Paint("danchi", new Color(0.50f, 0.50f, 0.48f));
            t.Paint("trim", new Color(0.60f, 0.60f, 0.58f));
            t.Paint("rail", new Color(0.68f, 0.68f, 0.66f));
            t.Paint("glass", new Color(0.11f, 0.12f, 0.15f), 0.45f);
            t.Paint("shade", new Color(0.16f, 0.17f, 0.19f));
            t.Paint("door", new Color(0.30f, 0.33f, 0.36f));
            t.Paint("houseA", new Color(0.72f, 0.68f, 0.60f));
            t.Paint("houseB", new Color(0.56f, 0.58f, 0.60f));
            t.Paint("houseC", new Color(0.64f, 0.57f, 0.49f));
            t.Paint("roofGrey", new Color(0.24f, 0.26f, 0.31f));
            t.Paint("roofBrown", new Color(0.33f, 0.25f, 0.20f));
            t.Paint("roofRed", new Color(0.44f, 0.23f, 0.18f));
            t.Paint("roofBlue", new Color(0.21f, 0.28f, 0.37f));
            t.Paint("office", new Color(0.52f, 0.55f, 0.59f));
            t.Paint("officeB", new Color(0.60f, 0.59f, 0.55f));
            t.Paint("sign", new Color(0.86f, 0.84f, 0.78f));
            t.Paint("steel", new Color(0.40f, 0.42f, 0.45f));
            t.Paint("concrete", new Color(0.56f, 0.56f, 0.54f));
            t.Paint("train", new Color(0.72f, 0.74f, 0.75f));
            t.Paint("band", new Color(0.22f, 0.46f, 0.32f));
            t.Paint("tree", new Color(0.13f, 0.18f, 0.11f));
            t.Paint("grass", new Color(0.30f, 0.36f, 0.22f));
            t.Paint("hill", new Color(0.21f, 0.27f, 0.20f));
            t.Paint("sand", new Color(0.62f, 0.57f, 0.47f));
            t.Paint("chimney", new Color(0.66f, 0.64f, 0.62f));
            t.Paint("chimneyRed", new Color(0.60f, 0.20f, 0.15f));
            t.Paint("wire", new Color(0.18f, 0.19f, 0.21f));
        }

        // ---- 同じ団地の続き --------------------------------------------------------

        /// <summary>
        /// 西の半分に、棟の列を平行に並べる。向きは自分たちの棟と同じ（長手が x、廊下が北、ベランダが南）。
        /// 列ごとに少しずつ端をずらして、升目を切ったように揃いすぎないようにする
        /// </summary>
        static void TownDanchi(Town t)
        {
            var rnd = new System.Random(11);
            for (var k = -7; k <= 8; k++)
            {
                var z = EstateFarCentre.z + k * 36f + (k % 2) * 4f;
                var x = EstateFarCentre.x - 390f + ((k + 7) % 3) * 13f;
                while (x < EstateFarCentre.x + 70f)
                {
                    var len = 44f + rnd.Next(0, 4) * 8f;
                    var foot = new Vector3(x + len * 0.5f, 0f, z);
                    var reach = TownReach(foot);
                    var az = TownAz(foot);
                    var nearest = reach - len * 0.5f - 7f;
                    var west = az > 190f || az < 16f;
                    if (west && nearest >= TownNear && reach < 430f)
                    {
                        var floors = reach > 170f && rnd.NextDouble() < 0.2 ? 11 : (rnd.NextDouble() < 0.3 ? 4 : 5);
                        while (floors > 3 && 0.6f + floors * 2.8f + 0.7f > TownCap(nearest)) floors--;
                        TownSlab(t, foot, 0f, len, floors, rnd);
                    }
                    x += len + 16f + rnd.Next(0, 3) * 6f;
                }
            }
        }

        /// <summary>
        /// 団地の棟を一つ。南にベランダの列、北に開いた廊下と戸の列、端に階段の塔。
        /// 窓も戸も間口ごとに等間隔
        /// </summary>
        static void TownSlab(Town t, Vector3 foot, float yaw, float len, int floors, System.Random rnd)
        {
            const float deep = 10.5f;
            const float storey = 2.8f;
            var high = 0.6f + floors * storey;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => foot + rot * new Vector3(x, y, z);
            var south = rot * Vector3.back;
            var north = rot * Vector3.forward;

            t["danchi"].Box(p(0f, high * 0.5f, 0f), new Vector3(len, high, deep), rot);
            t["trim"].Box(p(0f, high + 0.35f, 0f), new Vector3(len + 0.3f, 0.7f, deep + 0.3f), rot);
            var bays = Mathf.Max(3, Mathf.RoundToInt(len / 3.2f));
            var bay = len / bays;
            for (var f = 0; f < floors; f++)
            {
                var y = 0.6f + f * storey;
                // 南。ベランダの床と手すり、その奥の窓
                t["trim"].Box(p(0f, y + 0.05f, -deep * 0.5f - 0.6f), new Vector3(len, 0.18f, 1.2f), rot);
                t["rail"].Box(p(0f, y + 0.62f, -deep * 0.5f - 1.15f), new Vector3(len, 0.95f, 0.12f), rot);
                for (var b = 0; b < bays; b++)
                {
                    var x = -len * 0.5f + bay * (b + 0.5f);
                    t["glass"].Face(p(x, y + 1.35f, -deep * 0.5f - 0.02f), south, bay * 0.74f, 1.75f);
                    if (b % 2 == 0)
                        t["trim"].Box(p(-len * 0.5f + bay * b, y + 1.4f, -deep * 0.5f - 0.6f), new Vector3(0.14f, 2.6f, 1.2f), rot);
                }
                // 北。廊下の奥の暗がりと、手すりと、戸
                t["shade"].Face(p(0f, y + 1.25f, deep * 0.5f + 0.02f), north, len, 2.1f);
                t["trim"].Box(p(0f, y + 0.05f, deep * 0.5f + 0.6f), new Vector3(len, 0.18f, 1.2f), rot);
                t["rail"].Box(p(0f, y + 0.58f, deep * 0.5f + 1.15f), new Vector3(len, 0.9f, 0.12f), rot);
                for (var b = 0; b < bays; b += 2)
                {
                    var x = -len * 0.5f + bay * (b + 0.5f);
                    t["door"].Face(p(x, y + 1.0f, deep * 0.5f + 0.04f), north, 0.9f, 1.9f);
                }
            }
            // 階段の塔と、妻の小窓
            t["danchi"].Box(p(len * 0.5f - 3.2f, (high + 1.6f) * 0.5f, deep * 0.5f + 2.4f), new Vector3(3.4f, high + 1.6f, 3.2f), rot);
            for (var f = 0; f < floors; f++)
            {
                var y = 0.6f + f * storey + 1.4f;
                t["glass"].Face(p(len * 0.5f + 0.02f, y, -1.8f), rot * Vector3.right, 0.8f, 1.0f);
                t["glass"].Face(p(-len * 0.5f - 0.02f, y, -1.8f), rot * Vector3.left, 0.8f, 1.0f);
            }
            // 屋上の塔屋
            t["trim"].Box(p(-len * 0.25f, high + 1.4f, 0.5f), new Vector3(4.2f, 2.2f, 4.2f), rot);
            if (rnd.NextDouble() < 0.5)
                t["shade"].Box(p(len * 0.2f, high + 1.1f, -1f), new Vector3(3f, 1.6f, 2.2f), rot);
        }

        // ---- 駅前 ------------------------------------------------------------------

        /// <summary>
        /// 北東の奥、日の方角に、高さのまちまちなビルを固める。
        /// 日を背にするので、見えるのは日陰の面と霞の中の輪郭
        /// </summary>
        static void TownCentre(Town t)
        {
            var rnd = new System.Random(31);
            var placed = new List<Vector3>();
            for (var n = 0; n < 160 && placed.Count < 42; n++)
            {
                var az = 20f + (float)rnd.NextDouble() * 44f;
                var dist = 235f + (float)rnd.NextDouble() * 200f;
                var at = TownAt(dist, az);
                var w = 14f + (float)rnd.NextDouble() * 18f;
                var d = 14f + (float)rnd.NextDouble() * 16f;
                var r = Mathf.Max(w, d) * 0.6f;
                var clash = false;
                foreach (var q in placed)
                    if (new Vector2(q.x - at.x, q.z - at.z).magnitude < q.y + r + 4f) { clash = true; break; }
                if (clash || TownOff(at, RailFrom, RailTo) < r + 10f) continue;
                var cap = TownCap(dist - r) - 1f;
                var u = (float)rnd.NextDouble();
                var high = Mathf.Min(cap, 14f + u * u * 72f);
                TownTower(t, at, 8f + (float)rnd.NextDouble() * 6f - 3f, w, d, high, rnd);
                placed.Add(new Vector3(at.x, r, at.z));
            }
        }

        /// <summary>ビルを一つ。帯の窓か、間口ごとの窓。屋上に塔屋、時々看板</summary>
        static void TownTower(Town t, Vector3 foot, float yaw, float w, float d, float high, System.Random rnd)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => foot + rot * new Vector3(x, y, z);
            var skin = rnd.NextDouble() < 0.5 ? "office" : "officeB";
            t[skin].Box(p(0f, high * 0.5f, 0f), new Vector3(w, high, d), rot);
            var ribbon = rnd.NextDouble() < 0.5;
            const float storey = 3.4f;
            var floors = Mathf.FloorToInt((high - 2f) / storey);
            var faces = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            foreach (var f in faces)
            {
                var n = rot * f;
                var span = Mathf.Abs(f.z) > 0.5f ? w : d;
                var depth = Mathf.Abs(f.z) > 0.5f ? d : w;
                var bays = Mathf.Max(2, Mathf.RoundToInt(span / 3.2f));
                var bay = span / bays;
                for (var k = 0; k < floors; k++)
                {
                    var y = 2.6f + k * storey;
                    var centre = p(f.x * depth * 0.5f, y, f.z * depth * 0.5f) + n * 0.03f;
                    if (ribbon) { t["glass"].Face(centre, n, span - 1.2f, 1.5f); continue; }
                    var across = Vector3.Cross(Vector3.up, -n);
                    for (var b = 0; b < bays; b++)
                        t["glass"].Face(centre + across * (-span * 0.5f + bay * (b + 0.5f)), n, bay * 0.6f, 1.6f);
                }
            }
            t["trim"].Box(p(0f, high + 0.4f, 0f), new Vector3(w + 0.3f, 0.8f, d + 0.3f), rot);
            t["shade"].Box(p(w * 0.15f, high + 1.8f, 0f), new Vector3(w * 0.35f, 2.8f, d * 0.4f), rot);
            if (rnd.NextDouble() < 0.35)
                t["sign"].Box(p(0f, high + 3.4f, -d * 0.2f), new Vector3(w * 0.7f, 3.2f, 0.4f), rot);
        }

        // ---- 高架 ------------------------------------------------------------------

        /// <summary>
        /// 高架の線路。東を南北に通す。橋脚が等間隔に並び、上に電車が一編成止まっている
        /// </summary>
        static void TownRail(Town t)
        {
            const float deck = 10f;
            var along = RailTo - RailFrom;
            var length = new Vector2(along.x, along.z).magnitude;
            var dir = along / length;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var side = Vector3.Cross(Vector3.up, dir);
            const float step = 30f;
            for (var s = 0f; s < length; s += step)
            {
                var mid = RailFrom + dir * (s + step * 0.5f);
                t["concrete"].Box(mid + Vector3.up * deck, new Vector3(11f, 1.6f, step), rot);
                t["concrete"].Box(mid + Vector3.up * (deck + 1.4f) + side * 5.3f, new Vector3(0.3f, 1.4f, step), rot);
                t["concrete"].Box(mid + Vector3.up * (deck + 1.4f) - side * 5.3f, new Vector3(0.3f, 1.4f, step), rot);
                var pier = RailFrom + dir * s;
                t["concrete"].Box(pier + Vector3.up * (deck * 0.5f), new Vector3(3.2f, deck, 2.4f), rot);
                t["concrete"].Box(pier + Vector3.up * (deck - 1.2f), new Vector3(9f, 1.4f, 2.6f), rot);
                // 架線の柱。二本おき
                if (Mathf.RoundToInt(s / step) % 2 == 0)
                {
                    t["steel"].Box(pier + Vector3.up * (deck + 4f) + side * 5f, new Vector3(0.4f, 7f, 0.4f), rot);
                    t["steel"].Box(pier + Vector3.up * (deck + 7.3f), new Vector3(10.4f, 0.35f, 0.35f), rot);
                }
            }
            t["wire"].Box(RailFrom + along * 0.5f + Vector3.up * (deck + 6.4f), new Vector3(0.25f, 0.25f, length), rot);

            // 電車。北東の空の下に一編成
            var best = 0f;
            var bestGap = float.MaxValue;
            for (var s = 0f; s < length; s += 5f)
            {
                var gap = Mathf.Abs(Mathf.DeltaAngle(TownAz(RailFrom + dir * s), 58f));
                if (gap < bestGap) { bestGap = gap; best = s; }
            }
            for (var c = 0; c < 8; c++)
            {
                var at = RailFrom + dir * (best + (c - 3.5f) * 20.4f) + Vector3.up * (deck + 0.8f);
                t["train"].Box(at + Vector3.up * 1.95f, new Vector3(2.9f, 3.5f, 20f), rot);
                t["band"].Box(at + Vector3.up * 1.2f, new Vector3(2.96f, 0.5f, 20f), rot);
                t["glass"].Box(at + Vector3.up * 2.45f, new Vector3(2.94f, 1.0f, 18.6f), rot);
            }
        }

        /// <summary>高架の道路。南を東西に通す。遮音壁が一本の帯になって地平に沿う</summary>
        static void TownRoad(Town t)
        {
            const float deck = 12f;
            var along = RoadTo - RoadFrom;
            var length = new Vector2(along.x, along.z).magnitude;
            var dir = along / length;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var side = Vector3.Cross(Vector3.up, dir);
            const float step = 40f;
            for (var s = 0f; s < length; s += step)
            {
                var mid = RoadFrom + dir * (s + step * 0.5f);
                if (TownReach(mid) < TownNear + 20f) continue;
                t["concrete"].Box(mid + Vector3.up * deck, new Vector3(22f, 2f, step), rot);
                t["trim"].Box(mid + Vector3.up * (deck + 2.5f) + side * 10.8f, new Vector3(0.3f, 3f, step), rot);
                t["trim"].Box(mid + Vector3.up * (deck + 2.5f) - side * 10.8f, new Vector3(0.3f, 3f, step), rot);
                var pier = RoadFrom + dir * s;
                t["concrete"].Box(pier + Vector3.up * (deck * 0.5f), new Vector3(3.5f, deck, 3.5f), rot);
                t["steel"].Box(pier + Vector3.up * (deck + 5f) + side * 9.5f, new Vector3(0.3f, 7f, 0.3f), rot);
            }
        }

        // ---- 鉄塔と送電線 ------------------------------------------------------------

        static void TownPylons(Town t)
        {
            TownLine(t, EstateFarCentre + new Vector3(-600f, 0f, 255f), EstateFarCentre + new Vector3(600f, 0f, 175f), 8, 44f);
            TownLine(t, EstateFarCentre + new Vector3(-560f, 0f, -60f), EstateFarCentre + new Vector3(-80f, 0f, -540f), 5, 40f);
        }

        /// <summary>鉄塔を等間隔に並べ、腕木の先どうしを撓んだ線で繋ぐ</summary>
        static void TownLine(Town t, Vector3 from, Vector3 to, int count, float high)
        {
            var dir = (to - from).normalized;
            var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var tips = new List<Vector3[]>();
            for (var i = 0; i < count; i++)
            {
                var at = Vector3.Lerp(from, to, i / (float)(count - 1));
                var h = Mathf.Min(high, TownCap(TownReach(at) - 6f) - 1f);
                tips.Add(TownPylon(t, at, yaw, h));
            }
            for (var i = 0; i + 1 < tips.Count; i++)
                for (var k = 0; k < tips[i].Length; k++)
                {
                    var a = tips[i][k];
                    var b = tips[i + 1][k];
                    var sag = Vector3.Distance(a, b) * 0.035f;
                    TownSag(t["wire"], a, b, sag, 0.6f);
                }
        }

        /// <summary>
        /// 鉄塔を一基。四本の脚が上へ窄まり、横の筋交いと、三段の腕木。
        /// 返すのは線を掛ける点（腕木の先六つと、頂の一つ）
        /// </summary>
        static Vector3[] TownPylon(Town t, Vector3 foot, float yaw, float high)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => foot + rot * new Vector3(x, y, z);
            var s = t["steel"];
            var baseHalf = high * 0.12f;
            var topHalf = high * 0.03f;
            System.Func<float, float> half = y => Mathf.Lerp(baseHalf, topHalf, y / high);
            for (var cx = -1; cx <= 1; cx += 2)
                for (var cz = -1; cz <= 1; cz += 2)
                    s.Beam(p(cx * baseHalf, 0f, cz * baseHalf), p(cx * topHalf, high, cz * topHalf), 0.5f);
            // 横の筋交いと斜めの筋交い
            for (var k = 0; k < 6; k++)
            {
                var y0 = high * k / 6f;
                var y1 = high * (k + 1) / 6f;
                var h0 = half(y0);
                var h1 = half(y1);
                s.Beam(p(-h1, y1, -h1), p(h1, y1, -h1), 0.3f);
                s.Beam(p(-h1, y1, h1), p(h1, y1, h1), 0.3f);
                s.Beam(p(-h1, y1, -h1), p(-h1, y1, h1), 0.3f);
                s.Beam(p(h1, y1, -h1), p(h1, y1, h1), 0.3f);
                s.Beam(p(-h0, y0, -h0), p(h1, y1, -h1), 0.25f);
                s.Beam(p(h0, y0, -h0), p(-h1, y1, -h1), 0.25f);
                s.Beam(p(-h0, y0, h0), p(h1, y1, h1), 0.25f);
                s.Beam(p(h0, y0, h0), p(-h1, y1, h1), 0.25f);
            }
            var tips = new List<Vector3>();
            var arms = new[] { 0.64f, 0.78f, 0.92f };
            var reach = new[] { 0.24f, 0.20f, 0.16f };
            for (var k = 0; k < arms.Length; k++)
            {
                var y = high * arms[k];
                var r = high * reach[k];
                s.Beam(p(-r, y, 0f), p(r, y, 0f), 0.55f);
                s.Beam(p(-r, y, 0f), p(-half(y), y + high * 0.06f, 0f), 0.3f);
                s.Beam(p(r, y, 0f), p(half(y), y + high * 0.06f, 0f), 0.3f);
                // 碍子の房。線はその下に掛かる
                s.Beam(p(-r, y, 0f), p(-r, y - 2.2f, 0f), 0.35f);
                s.Beam(p(r, y, 0f), p(r, y - 2.2f, 0f), 0.35f);
                tips.Add(p(-r, y - 2.2f, 0f));
                tips.Add(p(r, y - 2.2f, 0f));
            }
            s.Beam(p(0f, high, 0f), p(0f, high + 3f, 0f), 0.4f);
            tips.Add(p(0f, high + 3f, 0f));
            return tips.ToArray();
        }

        /// <summary>撓んだ線。途中を十に切って短い棒で繋ぐ</summary>
        static void TownSag(Sketch s, Vector3 a, Vector3 b, float sag, float thick)
        {
            const int span = 10;
            var last = a;
            for (var i = 1; i <= span; i++)
            {
                var k = i / (float)span;
                var at = Vector3.Lerp(a, b, k);
                at.y -= sag * 4f * k * (1f - k);
                s.Beam(last, at, thick);
                last = at;
            }
        }

        // ---- 給水塔・学校・工場・煙突 ------------------------------------------------

        static void TownWater(Town t)
        {
            // 団地の給水塔。四本脚の上に箱の水槽
            var a = TownAt(150f, 242f);
            var h = Mathf.Min(24f, TownCap(144f) - 9f);
            for (var cx = -1; cx <= 1; cx += 2)
                for (var cz = -1; cz <= 1; cz += 2)
                    t["concrete"].Beam(a + new Vector3(cx * 4.5f, 0f, cz * 4.5f), a + new Vector3(cx * 3f, h, cz * 3f), 0.8f);
            for (var k = 1; k <= 3; k++)
            {
                var y = h * k / 4f;
                t["concrete"].Box(a + Vector3.up * y, new Vector3(8.6f - k * 0.4f, 0.5f, 8.6f - k * 0.4f), Quaternion.identity);
            }
            t["trim"].Box(a + Vector3.up * (h + 3.5f), new Vector3(9f, 7f, 9f), Quaternion.identity);
            t["shade"].Box(a + Vector3.up * (h + 7.3f), new Vector3(6f, 0.6f, 6f), Quaternion.identity);

            // 茸の形の給水塔。柱の上に丸い水槽。丸は向きを変えた箱を重ねて出す
            var b = TownAt(260f, 160f);
            var c = Mathf.Min(28f, TownCap(252f) - 10f);
            for (var k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(0f, k * 22.5f, 0f);
                t["concrete"].Box(b + Vector3.up * (c * 0.5f), new Vector3(4.2f, c, 4.2f), rot);
                t["trim"].Box(b + Vector3.up * (c + 1.6f), new Vector3(11f, 3.2f, 11f), rot);
                t["trim"].Box(b + Vector3.up * (c + 4.6f), new Vector3(14f, 2.8f, 14f), rot);
                t["trim"].Box(b + Vector3.up * (c + 6.6f), new Vector3(12f, 1.2f, 12f), rot);
            }
        }

        static void TownSchool(Town t, List<Vector4> keep)
        {
            var at = TownAt(168f, 128f);
            var rot = Quaternion.Euler(0f, 8f, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => at + rot * new Vector3(x, y, z);
            keep.Add(new Vector4(at.x, at.z, 62f, 0f));
            // 校舎。四階建て、南に窓の帯
            const float high = 14.8f;
            t["houseA"].Box(p(0f, high * 0.5f, 18f), new Vector3(74f, high, 12.5f), rot);
            for (var f = 0; f < 4; f++)
            {
                var y = 1.8f + f * 3.4f;
                t["glass"].Face(p(0f, y, 18f - 6.28f), rot * Vector3.back, 70f, 1.8f);
                t["glass"].Face(p(0f, y, 18f + 6.28f), rot * Vector3.forward, 70f, 1.4f);
                t["trim"].Box(p(0f, y - 1.2f, 18f - 6.6f), new Vector3(74f, 0.25f, 0.8f), rot);
            }
            t["trim"].Box(p(0f, high + 0.4f, 18f), new Vector3(74.4f, 0.8f, 12.9f), rot);
            // 時計のついた塔屋
            t["houseA"].Box(p(-6f, high + 2.6f, 18f), new Vector3(6f, 5.2f, 6f), rot);
            t["sign"].Face(p(-6f, high + 3.2f, 18f - 3.03f), rot * Vector3.back, 2.2f, 2.2f);
            // 体育館。低い切妻
            t["houseB"].Box(p(30f, 5f, -14f), new Vector3(34f, 10f, 26f), rot);
            t["roofBlue"].Gable(p(30f, 10f, -14f), 35f, 27f, 3.2f, 8f);
            for (var k = 0; k < 6; k++)
                t["glass"].Face(p(15f + k * 6f, 7.2f, -14f - 13.02f), rot * Vector3.back, 3.6f, 1.6f);
            // 校庭
            t["sand"].Flat(p(-8f, 0.03f, -14f), 58f, 40f, 8f);
        }

        static void TownFactory(Town t, List<Vector4> keep)
        {
            var at = TownAt(262f, 96f);
            var rot = Quaternion.Euler(0f, 8f, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => at + rot * new Vector3(x, y, z);
            keep.Add(new Vector4(at.x, at.z, 48f, 0f));
            const float wall = 8f;
            t["houseB"].Box(p(0f, wall * 0.5f, 0f), new Vector3(70f, wall, 44f), rot);
            // 鋸屋根。北向きの縦の硝子と、南へ下る斜面を七つ
            const int teeth = 7;
            var depth = 44f / teeth;
            for (var k = 0; k < teeth; k++)
            {
                var z0 = -22f + k * depth;
                var z1 = z0 + depth;
                t["glass"].Face(p(0f, wall + 1.6f, z1), rot * Vector3.forward, 70f, 3.2f);
                t["roofGrey"].Slope(p(-35f, wall, z0), p(35f, wall, z0), p(35f, wall + 3.2f, z1), p(-35f, wall + 3.2f, z1));
            }
            // 煙突。上の三つの帯を赤と白に
            var stack = TownAt(380f, 116f);
            var h = Mathf.Min(62f, TownCap(377f) - 1f);
            t["chimney"].Box(stack + Vector3.up * (h * 0.5f), new Vector3(5f, h, 5f), Quaternion.identity);
            for (var k = 0; k < 3; k++)
                t["chimneyRed"].Box(stack + Vector3.up * (h - 2f - k * 8f), new Vector3(5.2f, 4f, 5.2f), Quaternion.identity);
            t["steel"].Box(stack + Vector3.up * (h * 0.5f) + new Vector3(4f, 0f, 0f), new Vector3(0.4f, h, 0.4f), Quaternion.identity);
        }

        /// <summary>戸建ての中に、中層のマンションを散らす。屋根の並びから一段高い塊が出る</summary>
        static void TownMansions(Town t, List<Vector4> keep)
        {
            var spots = new[]
            {
                new Vector2(112f, 88f), new Vector2(150f, 44f), new Vector2(205f, 112f), new Vector2(128f, 170f),
                new Vector2(240f, 140f), new Vector2(196f, 70f), new Vector2(290f, 178f), new Vector2(104f, 200f),
            };
            var rnd = new System.Random(47);
            foreach (var s in spots)
            {
                var at = TownAt(s.x, s.y);
                if (TownOff(at, RailFrom, RailTo) < 28f || TownOff(at, RoadFrom, RoadTo) < 30f) continue;
                var len = 26f + rnd.Next(0, 3) * 8f;
                var floors = 6 + rnd.Next(0, 6);
                while (floors > 3 && 0.6f + floors * 2.8f + 0.7f > TownCap(s.x - len * 0.5f)) floors--;
                TownSlab(t, at, 8f, len, floors, rnd);
                keep.Add(new Vector4(at.x, at.z, len * 0.5f + 10f, 0f));
            }
        }

        /// <summary>神社の森。背の高い木が固まって、屋根の並びの中に黒い塊を作る</summary>
        static void TownShrine(Town t, List<Vector4> keep)
        {
            var at = TownAt(118f, 150f);
            keep.Add(new Vector4(at.x, at.z, 24f, 0f));
            var rnd = new System.Random(59);
            for (var k = 0; k < 26; k++)
            {
                var a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var r = Mathf.Sqrt((float)rnd.NextDouble()) * 18f;
                TownTree(t, at + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), 11f + (float)rnd.NextDouble() * 8f, rnd);
            }
            t["roofBrown"].Gable(at + new Vector3(0f, 5f, -20f), 9f, 7f, 3f, 0f);
        }

        /// <summary>木を一本。幹と、ずらして重ねた葉の塊</summary>
        static void TownTree(Town t, Vector3 foot, float high, System.Random rnd)
        {
            t["tree"].Box(foot + Vector3.up * (high * 0.3f), new Vector3(0.5f, high * 0.6f, 0.5f), Quaternion.identity);
            var spread = high * 0.45f;
            for (var k = 0; k < 3; k++)
            {
                var y = high * (0.55f + k * 0.16f);
                var s = spread * (1f - k * 0.25f);
                var yaw = (float)rnd.NextDouble() * 90f;
                t["tree"].Box(foot + new Vector3(((float)rnd.NextDouble() - 0.5f) * 1.5f, y, ((float)rnd.NextDouble() - 0.5f) * 1.5f),
                    new Vector3(s, high * 0.22f, s * 0.9f), Quaternion.Euler(0f, yaw, 0f));
            }
        }

        // ---- 戸建ての屋根の並び ----------------------------------------------------

        /// <summary>
        /// 東から南にかけて、升目の区画に戸建てを並べる。四区画ごとに道を一本通し、
        /// 道沿いに電柱と電線。区画のいくつかは庭木か空き地にして、屋根の並びに息継ぎを作る
        /// </summary>
        static void TownHouses(Town t, List<Vector4> keep)
        {
            var rnd = new System.Random(23);
            const float gridYaw = 8f;
            var rot = Quaternion.Euler(0f, gridYaw, 0f);
            var roofs = new[] { "roofGrey", "roofGrey", "roofBrown", "roofRed", "roofBlue" };
            var walls = new[] { "houseA", "houseB", "houseC" };
            var poles = new Dictionary<long, Vector3>();
            for (var i = -26; i <= 26; i++)
                for (var j = -24; j <= 24; j++)
                {
                    var gx = i * 14f + Mathf.Floor(i / 4f) * 7f;
                    var gz = j * 16f + Mathf.Floor(j / 3f) * 7f;
                    var at = EstateFarCentre + rot * new Vector3(gx, 0f, gz);
                    var reach = TownReach(at);
                    var az = TownAz(at);
                    if (reach < TownNear + 9f || reach > 330f) continue;
                    if (az < 17f || az > 192f) continue;
                    if (az < 66f && reach > 226f) continue;   // 駅前
                    if (TownOff(at, RailFrom, RailTo) < 16f || TownOff(at, RoadFrom, RoadTo) < 22f) continue;
                    var blocked = false;
                    foreach (var k in keep)
                        if (new Vector2(at.x - k.x, at.z - k.y).magnitude < k.z) { blocked = true; break; }
                    if (blocked) continue;

                    // 区画の角の電柱。道に面した列だけ
                    if (((i % 4) + 4) % 4 == 3 && j % 2 == 0)
                    {
                        var pole = EstateFarCentre + rot * new Vector3(gx + 8.2f, 0f, gz - 7f);
                        t["steel"].Box(pole + Vector3.up * 5.5f, new Vector3(0.35f, 11f, 0.35f), rot);
                        t["steel"].Box(pole + Vector3.up * 10.2f, new Vector3(1.8f, 0.2f, 0.2f), rot);
                        poles[((long)i << 32) ^ (uint)j] = pole + Vector3.up * 10f;
                        Vector3 back;
                        if (poles.TryGetValue(((long)i << 32) ^ (uint)(j - 2), out back))
                            TownSag(t["wire"], back, pole + Vector3.up * 10f, 0.5f, 0.3f);
                    }

                    var roll = rnd.NextDouble();
                    if (roll < 0.08) continue;
                    if (roll < 0.16) { TownTree(t, at, 6f + (float)rnd.NextDouble() * 4f, rnd); continue; }
                    TownHouse(t, at, gridYaw + rnd.Next(0, 2) * 90f, walls[rnd.Next(walls.Length)], roofs[rnd.Next(roofs.Length)], rnd);
                    if (rnd.NextDouble() < 0.25)
                        TownTree(t, at + rot * new Vector3(5.2f, 0f, 5.5f), 4f + (float)rnd.NextDouble() * 3f, rnd);
                }
        }

        /// <summary>戸建てを一軒。箱の上に切妻（十軒に一軒は陸屋根）。長い面に窓を二つずつ</summary>
        static void TownHouse(Town t, Vector3 foot, float yaw, string wall, string roof, System.Random rnd)
        {
            var w = 7.4f + (float)rnd.NextDouble() * 2.2f;
            var d = 6.6f + (float)rnd.NextDouble() * 1.8f;
            var roll = rnd.NextDouble();
            var floors = roll < 0.82 ? 2 : (roll < 0.94 ? 1 : 3);
            var high = floors * 2.8f + 0.4f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            System.Func<float, float, float, Vector3> p = (x, y, z) => foot + rot * new Vector3(x, y, z);
            t[wall].Box(p(0f, high * 0.5f, 0f), new Vector3(w, high, d), rot);
            if (rnd.NextDouble() < 0.9)
                t[roof].Gable(p(0f, high, 0f), w + 0.9f, d + 0.9f, (d + 0.9f) * 0.3f, yaw);
            else
                t["trim"].Box(p(0f, high + 0.3f, 0f), new Vector3(w + 0.2f, 0.6f, d + 0.2f), rot);
            for (var f = 0; f < floors; f++)
            {
                var y = 1.5f + f * 2.8f;
                for (var k = -1; k <= 1; k += 2)
                {
                    t["glass"].Face(p(k * w * 0.24f, y, -d * 0.5f - 0.02f), rot * Vector3.back, 1.5f, 1.1f);
                    t["glass"].Face(p(k * w * 0.24f, y, d * 0.5f + 0.02f), rot * Vector3.forward, 1.1f, 0.9f);
                }
            }
        }

        // ---- 堤防と山 --------------------------------------------------------------

        /// <summary>東の奥の川の堤防。低い台形の土手が地平に沿って一本の線になる。上に並木と街灯</summary>
        static void TownLevee(Town t)
        {
            var from = EstateFarCentre + new Vector3(330f, 0f, 480f);
            var to = EstateFarCentre + new Vector3(365f, 0f, -480f);
            var dir = (to - from).normalized;
            var side = Vector3.Cross(Vector3.up, dir);   // 中心から見て手前が -side
            var length = Vector3.Distance(from, to);
            const float high = 7f;
            const float crest = 3f;
            const float foot = 17f;
            var rnd = new System.Random(71);
            const float step = 40f;
            for (var s = 0f; s < length; s += step)
            {
                var a = from + dir * s;
                var b = from + dir * Mathf.Min(s + step, length);
                // 手前の斜面、天端、奥の斜面
                t["grass"].Slope(a - side * foot, b - side * foot, b - side * crest + Vector3.up * high, a - side * crest + Vector3.up * high);
                t["concrete"].Slope(a - side * crest + Vector3.up * high, b - side * crest + Vector3.up * high,
                    b + side * crest + Vector3.up * high, a + side * crest + Vector3.up * high);
                t["grass"].Slope(a + side * crest + Vector3.up * high, b + side * crest + Vector3.up * high, b + side * foot, a + side * foot);
                var mid = (a + b) * 0.5f + Vector3.up * high;
                if (rnd.NextDouble() < 0.7) TownTree(t, mid - side * 2f, 7f + (float)rnd.NextDouble() * 3f, rnd);
                t["steel"].Box(mid + side * 2.4f + Vector3.up * 3.5f, new Vector3(0.25f, 7f, 0.25f), Quaternion.identity);
            }
        }

        /// <summary>
        /// 低い山の稜線。中心を囲む弧に、手前の裾から奥の尾根へ上る斜面を張る。
        /// 400 m より先なので、霞にほとんど溶けて形だけが残る
        /// </summary>
        static void TownHills(Town t)
        {
            TownRidge(t, 160f, 352f, 430f, 545f, 34f, 52f, 3);
            TownRidge(t, 38f, 160f, 470f, 590f, 22f, 36f, 9);
        }

        static void TownRidge(Town t, float azFrom, float azTo, float near, float far, float low, float swell, int seed)
        {
            const float step = 3f;
            System.Func<float, float> ridge = az =>
            {
                var n = SkyPaint.Noise(new Vector3(az * 0.045f, 0.5f, seed), seed) * 0.65f
                    + SkyPaint.Noise(new Vector3(az * 0.16f, 1.5f, seed), seed + 1) * 0.35f;
                return Mathf.Min(low + swell * n, TownCap(far) - 2f);
            };
            for (var az = azFrom; az < azTo; az += step)
            {
                var a0 = az;
                var a1 = Mathf.Min(az + step, azTo);
                var bl = TownAt(near, a0);
                var br = TownAt(near, a1);
                var tl = TownAt(far, a0) + Vector3.up * ridge(a0);
                var tr = TownAt(far, a1) + Vector3.up * ridge(a1);
                // 中心から見て右が角度の増える側
                t["hill"].Quad(br, bl, tl, tr);
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
