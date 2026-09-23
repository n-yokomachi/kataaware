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
    /// **日本の町に固有の物は置かない**（設計書 9.1 節）。電柱と電線、戸建ての瓦屋根、神社の森、
    /// 給水塔、鉄塔と送電線、団地の棟の列、高架の道路の遮音壁、川の堤防、山の稜線は外した。
    /// 残っているのは駅前のビルと高架の線路だけで、どちらもロンドンの街並みに組み直す
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

        // 高架の線路。ビルが避けるので、置く前から決めておく
        static readonly Vector3 RailFrom = EstateFarCentre + new Vector3(130f, 0f, 360f);
        static readonly Vector3 RailTo = EstateFarCentre + new Vector3(215f, 0f, -330f);
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

            TownCentre(t);
            TownRail(t);
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
