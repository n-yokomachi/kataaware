using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 面を溜めて 1 枚の mesh に焼く入れ物。素材ごとに 1 つ持つ。
    /// 箱を並べるだけだと絵が伸びてしまうので、ここで実寸に合わせた uv を振る。
    /// 立方体を並べる作りから、通りを 1 枚の皮として張る作りへ移すためのもの
    /// </summary>
    public sealed class Bank
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        /// <summary>1 メートルあたり絵を何回繰り返すか</summary>
        public float Texel = 0.5f;

        public int Count { get { return tris.Count / 3; } }

        /// <summary>
        /// 4 隅で面を 1 枚。a→b→c→d は表から見て左回り。
        /// uv は a を原点に、ab を横、ad を縦として実寸から振る
        /// </summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uOffset = 0f, float vOffset = 0f)
        {
            var i = verts.Count;
            var wide = Vector3.Distance(a, b) * Texel;
            var high = Vector3.Distance(a, d) * Texel;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(new Vector2(uOffset, vOffset));
            uvs.Add(new Vector2(uOffset + wide, vOffset));
            uvs.Add(new Vector2(uOffset + wide, vOffset + high));
            uvs.Add(new Vector2(uOffset, vOffset + high));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>
        /// x が一定の面。sign が 1 なら法線は +X。
        /// z を横、y を縦に取るので、壁の絵はまっすぐ立つ
        /// </summary>
        public void FaceX(float x, float z0, float z1, float y0, float y1, int sign)
        {
            if (z1 <= z0 || y1 <= y0) return;
            if (sign > 0)
                Quad(new Vector3(x, y0, z1), new Vector3(x, y0, z0), new Vector3(x, y1, z0), new Vector3(x, y1, z1),
                    z1 * Texel, y0 * Texel);
            else
                Quad(new Vector3(x, y0, z0), new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0),
                    z0 * Texel, y0 * Texel);
        }

        /// <summary>z が一定の面。sign が 1 なら法線は +Z</summary>
        public void FaceZ(float z, float x0, float x1, float y0, float y1, int sign)
        {
            if (x1 <= x0 || y1 <= y0) return;
            if (sign > 0)
                Quad(new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z),
                    x0 * Texel, y0 * Texel);
            else
                Quad(new Vector3(x1, y0, z), new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z),
                    x1 * Texel, y0 * Texel);
        }

        /// <summary>y が一定の面。sign が 1 なら法線は +Y（上を向く）</summary>
        public void FaceY(float y, float x0, float x1, float z0, float z1, int sign)
        {
            if (x1 <= x0 || z1 <= z0) return;
            if (sign > 0)
                Quad(new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0),
                    x0 * Texel, z1 * Texel);
            else
                Quad(new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1),
                    x0 * Texel, z0 * Texel);
        }

        /// <summary>箱ひとつ。6 面とも外を向く</summary>
        public void Box(Vector3 centre, Vector3 size)
        {
            var h = size * 0.5f;
            var x0 = centre.x - h.x; var x1 = centre.x + h.x;
            var y0 = centre.y - h.y; var y1 = centre.y + h.y;
            var z0 = centre.z - h.z; var z1 = centre.z + h.z;
            FaceX(x1, z0, z1, y0, y1, 1);
            FaceX(x0, z0, z1, y0, y1, -1);
            FaceZ(z1, x0, x1, y0, y1, 1);
            FaceZ(z0, x0, x1, y0, y1, -1);
            FaceY(y1, x0, x1, z0, z1, 1);
            FaceY(y0, x0, x1, z0, z1, -1);
        }

        /// <summary>向きを持つ箱。手足を曲げて置いたり、転がった物を散らすのに使う</summary>
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

        /// <summary>
        /// 穴の空いた壁。x が一定の面に、窓の抜けを避けて桟と欄間を張る。
        /// holes は (z0, z1, y0, y1) の並び
        /// </summary>
        public void FaceXHoles(float x, float z0, float z1, float y0, float y1, int sign, List<Vector4> holes)
        {
            if (holes == null || holes.Count == 0) { FaceX(x, z0, z1, y0, y1, sign); return; }
            // 高さを穴の上下で帯に割り、帯ごとに横を切る
            var lines = new List<float> { y0, y1 };
            foreach (var hole in holes)
            {
                if (hole.z > y0 && hole.z < y1) lines.Add(hole.z);
                if (hole.w > y0 && hole.w < y1) lines.Add(hole.w);
            }
            lines.Sort();
            for (var i = 0; i + 1 < lines.Count; i++)
            {
                var a = lines[i]; var b = lines[i + 1];
                if (b - a < 1e-4f) continue;
                var mid = (a + b) * 0.5f;
                var cuts = new List<float> { z0, z1 };
                foreach (var hole in holes)
                {
                    if (mid <= hole.z || mid >= hole.w) continue;
                    if (hole.x > z0 && hole.x < z1) cuts.Add(hole.x);
                    if (hole.y > z0 && hole.y < z1) cuts.Add(hole.y);
                }
                cuts.Sort();
                for (var j = 0; j + 1 < cuts.Count; j++)
                {
                    var c = cuts[j]; var d = cuts[j + 1];
                    if (d - c < 1e-4f) continue;
                    var cz = (c + d) * 0.5f;
                    var inside = false;
                    foreach (var hole in holes)
                    {
                        if (cz > hole.x && cz < hole.y && mid > hole.z && mid < hole.w) { inside = true; break; }
                    }
                    if (!inside) FaceX(x, c, d, a, b, sign);
                }
            }
        }

        /// <summary>溜めた面を mesh にして、場面に置く</summary>
        public GameObject Emit(Transform parent, string name, Material mat, bool collide, string assetDir)
        {
            if (tris.Count == 0) return null;
            var mesh = new Mesh();
            mesh.name = name;
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var path = assetDir + name.Replace('.', '_') + ".asset";
            ProcMesh.Save(mesh, path);
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = saved;
            return go;
        }
    }
}
