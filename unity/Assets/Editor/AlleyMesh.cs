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
        readonly List<Vector2> roots = new List<Vector2>();
        readonly List<Vector3> norms = new List<Vector3>();
        readonly List<int> tris = new List<int>();

        /// <summary>1 メートルあたり絵を何回繰り返すか</summary>
        public float Texel = 0.5f;

        /// <summary>
        /// 根からの高さを uv1 に持たせる。麦だけが立てる。
        ///
        /// 地面が起伏すると、頂点の y だけでは根からの高さが分からない。丘の上の株は
        /// y が丸ごと持ち上がるので、y を高さとして読むシェーダーは株ぜんたいを
        /// 穂先として扱い、撓むかわりに横へ滑る。
        /// 立てているあいだ、uv1 には (根からの高さ。m, 株の背に対する割合) が入る
        /// </summary>
        public bool Rooted;
        /// <summary>根の高さ。Rooted のあいだ、置く物ごとに書き換える</summary>
        public float RootY;
        /// <summary>その物の背。割合を出すのに使う。0 以下なら割合は 0</summary>
        public float RootHigh = 1f;

        /// <summary>
        /// 札の法線を上へ倒す割合。麦だけが使う。
        ///
        /// 札は面が立っているので、そのままの法線では上を向く成分が無い。
        /// 低い朝日は立った穂の横面にも天にも当たるので、真横を向いた法線で塗ると
        /// 畑が板を並べたように平らに沈む。1.0 なら上と横が半々になる
        /// </summary>
        public float CardLift = 1f;

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
            Root(a); Root(b); Root(c); Root(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>
        /// 四隅で面を 1 枚。uv は呼ぶ側が渡す。
        ///
        /// Quad は隅どうしの距離から uv を振るので、起伏のある面を升目に割ると、
        /// 斜面のぶんだけ uv が伸びて、隣の升と継ぎ目でずれる。
        /// 地面のように升を並べて張る面はこちらを使う
        /// </summary>
        public void Patch(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            var i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            Root(a); Root(b); Root(c); Root(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>
        /// 立てた札 1 枚。α で形を抜く絵を貼って、麦のように透けて見える株にする。
        ///
        /// **箱では麦にならない。** 絵をどれだけ描き込んでも輪郭は箱のままで、
        /// 天面が平らに切れ、株のあいだが覗けない。形を絵の α に持たせれば、
        /// 穂先の凸凹も株の隙間も絵の側が持つ。三角も箱の 12 枚から 2 枚へ減る。
        ///
        /// root は札の下辺の中、across は下辺の半分、up は札の丈。
        /// 横の uv は実寸から Texel で繰り返し、uOffset だけずらす。ずらさないと、
        /// 並べた札がどれも同じ株の並びになって、畑が反復模様に見える。
        /// 縦の uv は必ず 0→1。絵が根から穂先までを 1 枚に収めているので、
        /// 札の丈が違っても根が下端、穂先が上端に来る
        /// </summary>
        public void Card(Vector3 root, Vector3 across, Vector3 up, float uOffset)
        {
            var a = root - across;
            var b = root + across;
            var c = b + up;
            var d = a + up;
            var wide = across.magnitude * 2f * Texel;
            var face = Vector3.Cross(across, up);
            if (face.sqrMagnitude < 1e-12f) return;
            var n = (face.normalized + Vector3.up * CardLift).normalized;
            var i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(new Vector2(uOffset, 0f));
            uvs.Add(new Vector2(uOffset + wide, 0f));
            uvs.Add(new Vector2(uOffset + wide, 1f));
            uvs.Add(new Vector2(uOffset, 1f));
            norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);
            Root(a); Root(b); Root(c); Root(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>
        /// 立てた札 1 枚に、アトラスの升を一つ貼る。村の庭の花と葉の札（BuildVillagePlants.cs）が使う。
        ///
        /// <see cref="Card"/> と同じく root は下辺の中、across は下辺の半分、up は札の丈で、
        /// 法線は面の向きを <see cref="CardLift"/> だけ上へ倒す。uv は横に繰り返さず、
        /// <paramref name="uvMin"/>（左下）から <paramref name="uvMax"/>（右上）までをそのまま貼る
        /// </summary>
        public void AtlasCard(Vector3 root, Vector3 across, Vector3 up, Vector2 uvMin, Vector2 uvMax)
        {
            var a = root - across;
            var b = root + across;
            var c = b + up;
            var d = a + up;
            var face = Vector3.Cross(across, up);
            if (face.sqrMagnitude < 1e-12f) return;
            var n = (face.normalized + Vector3.up * CardLift).normalized;
            var i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(new Vector2(uvMin.x, uvMin.y));
            uvs.Add(new Vector2(uvMax.x, uvMin.y));
            uvs.Add(new Vector2(uvMax.x, uvMax.y));
            uvs.Add(new Vector2(uvMin.x, uvMax.y));
            norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);
            Root(a); Root(b); Root(c); Root(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        void Root(Vector3 v)
        {
            if (!Rooted) return;
            var up = v.y - RootY;
            roots.Add(new Vector2(up, RootHigh > 0f ? Mathf.Clamp01(up / RootHigh) : 0f));
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

        /// <summary>
        /// 水平に寝かせた多角形。中心と縁の点から扇に張る。
        /// 四角を並べると床に黒い四角が乗っているようにしか見えないので、
        /// 水たまりのような自然な輪郭はこれで作る。縁は上から見て左回りに渡す
        /// </summary>
        public void FanY(Vector3 centre, Vector2[] rim)
        {
            if (rim == null || rim.Length < 3) return;
            var c = verts.Count;
            verts.Add(centre);
            uvs.Add(new Vector2(centre.x * Texel, centre.z * Texel));
            for (var i = 0; i < rim.Length; i++)
            {
                verts.Add(new Vector3(rim[i].x, centre.y, rim[i].y));
                uvs.Add(new Vector2(rim[i].x * Texel, rim[i].y * Texel));
            }
            for (var i = 0; i < rim.Length; i++)
            {
                var a = c + 1 + i;
                var b = c + 1 + (i + 1) % rim.Length;
                // 上を向かせる。FaceY と同じく、上から見て右回りが表
                tris.Add(c); tris.Add(b); tris.Add(a);
            }
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

        /// <summary>
        /// 穴の空いた壁の、z が一定の面。FaceXHoles と同じ考えで、
        /// holes は (x0, x1, y0, y1) の並び
        /// </summary>
        public void FaceZHoles(float z, float x0, float x1, float y0, float y1, int sign, List<Vector4> holes)
        {
            if (holes == null || holes.Count == 0) { FaceZ(z, x0, x1, y0, y1, sign); return; }
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
                var cuts = new List<float> { x0, x1 };
                foreach (var hole in holes)
                {
                    if (mid <= hole.z || mid >= hole.w) continue;
                    if (hole.x > x0 && hole.x < x1) cuts.Add(hole.x);
                    if (hole.y > x0 && hole.y < x1) cuts.Add(hole.y);
                }
                cuts.Sort();
                for (var j = 0; j + 1 < cuts.Count; j++)
                {
                    var c = cuts[j]; var d = cuts[j + 1];
                    if (d - c < 1e-4f) continue;
                    var cx = (c + d) * 0.5f;
                    var inside = false;
                    foreach (var hole in holes)
                    {
                        if (cx > hole.x && cx < hole.y && mid > hole.z && mid < hole.w) { inside = true; break; }
                    }
                    if (!inside) FaceZ(z, c, d, a, b, sign);
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
            if (roots.Count == verts.Count) mesh.SetUVs(1, roots);
            mesh.SetTriangles(tris, 0);
            // 札は面の向きどおりの法線では平らに沈むので、置く側が法線を決めている。
            // 札を 1 枚でも混ぜたら数が合わなくなるので、全部が札のときだけ使う
            if (norms.Count == verts.Count) mesh.SetNormals(norms);
            else mesh.RecalculateNormals();
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
