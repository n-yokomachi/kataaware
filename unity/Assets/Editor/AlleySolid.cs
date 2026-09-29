using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 路地裏を組む途中で、先に置いた物の面（三角）を持っておき、新しく置く板や管がそれを貫くかを見る（<see cref="BuildAlley"/> のネオン）。
    ///
    /// 路地裏の物はほとんどが当たり判定を持たない焼いた mesh で、室外機や庇のような大きな箱は角にしか頂点が無い。
    /// 頂点が箱に入っているかを数える見方（BuildAlley.Occupied）では、看板の面を室外機が串刺しにしていても、室外機の角が
    /// 看板の薄い箱の外にあるので見つからなかった（オーナー、2026-09-29「小路に入る手前の近くにネオンと別の物体が干渉している」）。
    /// ここは三角と箱の分離軸で見るので、角がどこにあっても貫いていれば分かる
    /// </summary>
    public sealed class AlleySolid
    {
        /// <summary>三角を z の 1 m ごとの束に分けて持つ。通りは z に長いので、z で引けば見る数が 1/80 ほどになる</summary>
        const float Bucket = 1f;

        readonly List<Vector3> corners = new List<Vector3>();
        readonly List<Bounds> boxes = new List<Bounds>();
        readonly Dictionary<int, List<int>> byZ = new Dictionary<int, List<int>>();
        readonly Bounds band;

        /// <summary>持っている三角の数</summary>
        public int Triangles { get { return boxes.Count; } }

        /// <summary>band の中に掛かる三角だけを持つ（看板と管の掛かる通りの壁ぎわ）</summary>
        public AlleySolid(Bounds band)
        {
            this.band = band;
        }

        /// <summary>root の直下の names の束の中の、見える mesh の三角を足す</summary>
        public void AddGroups(Transform root, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                var t = root.Find(name);
                if (t == null) continue;
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) Add(r);
            }
        }

        /// <summary>r の mesh の三角を足す（世界の座で）</summary>
        public void Add(MeshRenderer r)
        {
            if (r == null || !r.bounds.Intersects(band)) return;
            var f = r.GetComponent<MeshFilter>();
            if (f == null || f.sharedMesh == null) return;
            var mesh = f.sharedMesh;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var at = r.transform.localToWorldMatrix;
            var w = new Vector3[verts.Length];
            for (var i = 0; i < verts.Length; i++) w[i] = at.MultiplyPoint3x4(verts[i]);
            for (var t = 0; t < tris.Length; t += 3)
            {
                var a = w[tris[t]];
                var b = w[tris[t + 1]];
                var c = w[tris[t + 2]];
                var box = new Bounds(a, Vector3.zero);
                box.Encapsulate(b);
                box.Encapsulate(c);
                if (!box.Intersects(band)) continue;
                var index = boxes.Count;
                boxes.Add(box);
                corners.Add(a);
                corners.Add(b);
                corners.Add(c);
                for (var k = Key(box.min.z); k <= Key(box.max.z); k++)
                {
                    List<int> list;
                    if (!byZ.TryGetValue(k, out list)) byZ[k] = list = new List<int>();
                    list.Add(index);
                }
            }
        }

        static int Key(float z)
        {
            return Mathf.FloorToInt(z / Bucket);
        }

        /// <summary>
        /// 中心 centre・向き rotation・半分の大きさ half の箱を、どれかの三角が貫くか（面が触れているだけのものは数えない）
        /// </summary>
        public bool Pierces(Vector3 centre, Quaternion rotation, Vector3 half)
        {
            return Count(centre, rotation, half, 1) > 0;
        }

        /// <summary>箱を貫く三角の数。most まで数えたら止める</summary>
        public int Count(Vector3 centre, Quaternion rotation, Vector3 half, int most)
        {
            // 触れているだけの物（壁に沿わせた管と壁の面など）は数えない
            var h = new Vector3(Mathf.Max(0f, half.x - 0.003f), Mathf.Max(0f, half.y - 0.003f), Mathf.Max(0f, half.z - 0.003f));
            var extent = new Vector3(
                Mathf.Abs((rotation * new Vector3(h.x, 0f, 0f)).x) + Mathf.Abs((rotation * new Vector3(0f, h.y, 0f)).x) + Mathf.Abs((rotation * new Vector3(0f, 0f, h.z)).x),
                Mathf.Abs((rotation * new Vector3(h.x, 0f, 0f)).y) + Mathf.Abs((rotation * new Vector3(0f, h.y, 0f)).y) + Mathf.Abs((rotation * new Vector3(0f, 0f, h.z)).y),
                Mathf.Abs((rotation * new Vector3(h.x, 0f, 0f)).z) + Mathf.Abs((rotation * new Vector3(0f, h.y, 0f)).z) + Mathf.Abs((rotation * new Vector3(0f, 0f, h.z)).z));
            var world = new Bounds(centre, extent * 2f);
            var back = Quaternion.Inverse(rotation);
            var seen = new HashSet<int>();
            var n = 0;
            for (var k = Key(world.min.z); k <= Key(world.max.z); k++)
            {
                List<int> list;
                if (!byZ.TryGetValue(k, out list)) continue;
                foreach (var i in list)
                {
                    if (!seen.Add(i)) continue;
                    if (!boxes[i].Intersects(world)) continue;
                    var a = back * (corners[i * 3] - centre);
                    var b = back * (corners[i * 3 + 1] - centre);
                    var c = back * (corners[i * 3 + 2] - centre);
                    if (!Crosses(a, b, c, h)) continue;
                    n++;
                    if (n >= most) return n;
                }
            }
            return n;
        }

        /// <summary>
        /// 三角（箱の中心からの座）が、原点を中心に半分の大きさ h の軸に沿った箱と重なるか。分離軸の見方（Akenine-Möller）
        /// </summary>
        public static bool Crosses(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 h)
        {
            // 箱の三つの面の向き
            if (Mathf.Max(v0.x, Mathf.Max(v1.x, v2.x)) < -h.x || Mathf.Min(v0.x, Mathf.Min(v1.x, v2.x)) > h.x) return false;
            if (Mathf.Max(v0.y, Mathf.Max(v1.y, v2.y)) < -h.y || Mathf.Min(v0.y, Mathf.Min(v1.y, v2.y)) > h.y) return false;
            if (Mathf.Max(v0.z, Mathf.Max(v1.z, v2.z)) < -h.z || Mathf.Min(v0.z, Mathf.Min(v1.z, v2.z)) > h.z) return false;
            // 三角の面
            var e0 = v1 - v0;
            var e1 = v2 - v1;
            var e2 = v0 - v2;
            var normal = Vector3.Cross(e0, e1);
            if (normal.sqrMagnitude < 1e-14f) return false;     // 潰れた三角
            if (!Overlap(normal, v0, v1, v2, h)) return false;
            // 辺と箱の軸の外積の九つ
            var edges = new[] { e0, e1, e2 };
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            foreach (var e in edges)
                foreach (var ax in axes)
                {
                    var axis = Vector3.Cross(ax, e);
                    if (axis.sqrMagnitude < 1e-12f) continue;
                    if (!Overlap(axis, v0, v1, v2, h)) return false;
                }
            return true;
        }

        /// <summary>軸 axis へ映した三角と箱が重なるか</summary>
        static bool Overlap(Vector3 axis, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 h)
        {
            var p0 = Vector3.Dot(v0, axis);
            var p1 = Vector3.Dot(v1, axis);
            var p2 = Vector3.Dot(v2, axis);
            var r = h.x * Mathf.Abs(axis.x) + h.y * Mathf.Abs(axis.y) + h.z * Mathf.Abs(axis.z);
            return !(Mathf.Min(p0, Mathf.Min(p1, p2)) > r || Mathf.Max(p0, Mathf.Max(p1, p2)) < -r);
        }
    }
}
