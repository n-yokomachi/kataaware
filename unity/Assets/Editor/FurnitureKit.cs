using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// アトラスの中の貼り方。<see cref="Metre"/> が正なら絵の升を Metre ごとに繰り返し、0 なら面いっぱいに一枚、負なら一点（色の升）
    /// </summary>
    public struct Tile
    {
        public Rect Uv;
        public float Metre;

        public Tile(Rect uv, float metre)
        {
            Uv = uv;
            Metre = metre;
        }

        public bool Flat { get { return Metre < 0f; } }

        /// <summary>升の中の割合 (fu, fv) を uv へ</summary>
        public Vector2 At(float fu, float fv)
        {
            return new Vector2(Uv.x + Uv.width * fu, Uv.y + Uv.height * fv);
        }
    }

    /// <summary>
    /// 自室の家具（<see cref="BuildFurniture"/>）の面を溜めて mesh に焼く入れ物。
    ///
    /// 部品（Begin〜End）ごとに、同じ位置の頂点の法線を、折れの角（crease）より緩い面どうしだけ均す（椅子の <c>BuildChair.Shop</c> と同じ考え）。
    /// 面の組はアトラスのマテリアル（<see cref="Atlas"/>）と、点滅する灯りのマテリアル（<see cref="Blink"/>）の二つ。
    /// 置き場は <see cref="At"/> で重ねて持ち、形を組む関数はどれもその中の座標で書く
    /// </summary>
    public sealed class FurnitureKit
    {
        public const int Atlas = 0, Blink = 1, Count = 2;

        /// <summary>面を作った向きのまま置くか、部品ごとに外へ向けて揃えるか</summary>
        public enum Orient { None, Each, Whole }

        /// <summary>角を丸めた箱の、張る面</summary>
        [Flags]
        public enum Sides { Bottom = 1, Top = 2, Back = 4, Front = 8, Left = 16, Right = 32, All = 63, NoBottom = 62 }

        /// <summary>丸めた後の位置 p（部品の中の座標）と、箱の面の上の割合 cube（各軸 −1〜1）から、動かした位置を返す</summary>
        public delegate Vector3 Deform(Vector3 p, Vector3 cube);

        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> norms = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] tris = new List<int>[Count];
        readonly List<int> part = new List<int>();
        readonly Dictionary<long, int> shared = new Dictionary<long, int>();
        readonly Stack<Matrix4x4> frames = new Stack<Matrix4x4>();
        Matrix4x4 frame = Matrix4x4.identity;
        int start;
        float crease;
        string piece;

        public int TriangleCount { get; private set; }
        /// <summary>部品の名前ごとの三角の数（重さの内訳を見るため）</summary>
        public readonly Dictionary<string, int> PieceTriangles = new Dictionary<string, int>();
        public int VertexCount { get { return verts.Count; } }

        public FurnitureKit()
        {
            for (var i = 0; i < Count; i++) tris[i] = new List<int>();
        }

        // ---- 置き場 ----------------------------------------------------------------

        public struct Scope : IDisposable
        {
            readonly FurnitureKit kit;
            public Scope(FurnitureKit kit) { this.kit = kit; }
            public void Dispose() { kit.frame = kit.frames.Pop(); }
        }

        /// <summary>今の置き場の中に、at へ rot で向けた置き場を重ねる。using で抜けると戻る</summary>
        public Scope At(Vector3 at, Quaternion rot)
        {
            frames.Push(frame);
            frame = frame * Matrix4x4.TRS(at, rot, Vector3.one);
            return new Scope(this);
        }

        public Scope At(Vector3 at, float yaw = 0f)
        {
            return At(at, Quaternion.Euler(0f, yaw, 0f));
        }

        // ---- 面 ----------------------------------------------------------------------

        public void Begin(string name, float creaseDegrees)
        {
            piece = name;
            crease = creaseDegrees;
            start = verts.Count;
            part.Clear();
            shared.Clear();
        }

        /// <summary>頂点ひとつ（今の置き場の中の座標）</summary>
        public int V(Vector3 p, Vector2 uv)
        {
            verts.Add(frame.MultiplyPoint3x4(p));
            uvs.Add(uv);
            norms.Add(Vector3.zero);
            return verts.Count - 1;
        }

        /// <summary>同じ面の張りの中で、位置と uv が同じ頂点を使い回す（<see cref="Unshare"/> で区切る）</summary>
        public int VShared(Vector3 p, Vector2 uv)
        {
            var w = frame.MultiplyPoint3x4(p);
            unchecked
            {
                var key = ((long)Mathf.RoundToInt(w.x * 2000f) * 73856093L) ^ ((long)Mathf.RoundToInt(w.y * 2000f) * 19349663L)
                    ^ ((long)Mathf.RoundToInt(w.z * 2000f) * 83492791L) ^ ((long)Mathf.RoundToInt(uv.x * 8192f) * 50331653L)
                    ^ ((long)Mathf.RoundToInt(uv.y * 8192f) * 12582917L);
                int i;
                if (shared.TryGetValue(key, out i) && (verts[i] - w).sqrMagnitude < 1e-8f && (uvs[i] - uv).sqrMagnitude < 1e-10f) return i;
                verts.Add(w);
                uvs.Add(uv);
                norms.Add(Vector3.zero);
                shared[key] = verts.Count - 1;
                return verts.Count - 1;
            }
        }

        public void Unshare() { shared.Clear(); }

        public Vector3 Pos(int i) { return verts[i]; }

        public void T(int sub, int a, int b, int c)
        {
            part.Add(sub); part.Add(a); part.Add(b); part.Add(c);
        }

        public void Q(int sub, int a, int b, int c, int d)
        {
            T(sub, a, b, c);
            T(sub, a, c, d);
        }

        /// <summary>面の表が want（今の置き場の中の向き）になるよう並びを揃えて置く</summary>
        public void TFacing(int sub, int a, int b, int c, Vector3 want)
        {
            var w = frame.MultiplyVector(want);
            var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, w) < 0f) T(sub, a, c, b);
            else T(sub, a, b, c);
        }

        public void QFacing(int sub, int a, int b, int c, int d, Vector3 want)
        {
            TFacing(sub, a, b, c, want);
            TFacing(sub, a, c, d, want);
        }

        public void End(Orient orient = Orient.None)
        {
            var count = part.Count / 4;
            if (orient != Orient.None && count > 0)
            {
                var centroid = Vector3.zero;
                for (var i = start; i < verts.Count; i++) centroid += verts[i];
                if (verts.Count > start) centroid /= verts.Count - start;
                var sum = 0f;
                for (var f = 0; f < count; f++)
                {
                    var a = verts[part[f * 4 + 1]];
                    var b = verts[part[f * 4 + 2]];
                    var c = verts[part[f * 4 + 3]];
                    var d = Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centroid);
                    if (orient == Orient.Each && d < 0f) Swap(f);
                    sum += d;
                }
                if (orient == Orient.Whole && sum < 0f) for (var f = 0; f < count; f++) Swap(f);
            }

            // 面の法線（面積の重み付き）を、同じ位置の頂点のあいだで折れの角より緩いものだけ均す
            var faceN = new Vector3[count];
            var byKey = new Dictionary<Vector3Int, List<int>>();
            var own = new Dictionary<int, List<int>>();
            for (var f = 0; f < count; f++)
            {
                var ia = part[f * 4 + 1];
                var ib = part[f * 4 + 2];
                var ic = part[f * 4 + 3];
                faceN[f] = Vector3.Cross(verts[ib] - verts[ia], verts[ic] - verts[ia]);
                foreach (var v in new[] { ia, ib, ic })
                {
                    List<int> list;
                    var key = Key(verts[v]);
                    if (!byKey.TryGetValue(key, out list)) byKey[key] = list = new List<int>();
                    list.Add(f);
                    if (!own.TryGetValue(v, out list)) own[v] = list = new List<int>();
                    list.Add(f);
                }
            }
            var cos = Mathf.Cos(crease * Mathf.Deg2Rad);
            foreach (var pair in own)
            {
                var mine = Vector3.zero;
                foreach (var f in pair.Value) mine += faceN[f];
                if (mine.sqrMagnitude < 1e-18f)
                    foreach (var f in byKey[Key(verts[pair.Key])]) mine += faceN[f];
                var m = mine.normalized;
                var sum = Vector3.zero;
                foreach (var f in byKey[Key(verts[pair.Key])])
                {
                    if (faceN[f].sqrMagnitude < 1e-18f) continue;
                    if (Vector3.Dot(faceN[f].normalized, m) >= cos - 1e-4f) sum += faceN[f];
                }
                norms[pair.Key] = sum.sqrMagnitude > 1e-18f ? sum.normalized : (m.sqrMagnitude > 0f ? m : Vector3.up);
            }
            for (var f = 0; f < count; f++)
            {
                var sub = part[f * 4];
                for (var k = 1; k <= 3; k++) tris[sub].Add(part[f * 4 + k]);
            }
            TriangleCount += count;
            int had;
            PieceTriangles.TryGetValue(piece ?? "", out had);
            PieceTriangles[piece ?? ""] = had + count;
            part.Clear();
            shared.Clear();
        }

        void Swap(int f)
        {
            var t = part[f * 4 + 2];
            part[f * 4 + 2] = part[f * 4 + 3];
            part[f * 4 + 3] = t;
        }

        static Vector3Int Key(Vector3 p)
        {
            return new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
        }

        /// <summary>溜めた面を mesh にする。点滅の面が無ければ面の組は一つ</summary>
        public Mesh Bake(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            var subs = tris[Blink].Count > 0 ? 2 : 1;
            mesh.subMeshCount = subs;
            for (var i = 0; i < subs; i++) mesh.SetTriangles(tris[i], i, false);
            mesh.RecalculateBounds();
            // 法線の絵を引くための接線（uv の u の向き）。色の升（uv が一点）の面は向きが決まらないが、そこの法線の絵は平らなので陰影は変わらない
            mesh.RecalculateTangents();
            return mesh;
        }

        public int SubCount { get { return tris[Blink].Count > 0 ? 2 : 1; } }

        // ---- 形 ----------------------------------------------------------------------

        /// <summary>
        /// 角を丸めた箱。面ごとに升目に割り、箱の面の点を内の箱へ寄せてから丸めの半径だけ押し出す（丸めの帯は bend 本に割る）。
        /// 升目は step（m）ごとと、絵の繰り返しの境で切るので、升ひとつが絵の升ひとつを越えない。deform で膨らみや窪みを付ける
        /// </summary>
        public void RoundBox(string name, Vector3 centre, Quaternion rot, Vector3 size, float radius, Tile tile,
            Deform deform = null, int bend = 2, float step = 0.25f, Sides sides = Sides.All, float creaseDegrees = 55f, Tile[] faces = null)
        {
            Begin(name, creaseDegrees);
            var h = size * 0.5f;
            var r = Mathf.Max(0f, Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f));
            var samples = new[] { Samples(h.x, r, bend, step, tile), Samples(h.y, r, bend, step, tile), Samples(h.z, r, bend, step, tile) };
            var own = tile;
            var hs = new[] { h.x, h.y, h.z };
            for (var axis = 0; axis < 3; axis++)
                foreach (var sign in new[] { -1, 1 })
                {
                    var flag = axis == 0 ? (sign < 0 ? Sides.Left : Sides.Right) : axis == 1 ? (sign < 0 ? Sides.Bottom : Sides.Top) : (sign < 0 ? Sides.Back : Sides.Front);
                    if ((sides & flag) == 0) continue;
                    // 面の横と縦。横の面は縦を y に取る
                    int ua, va;
                    if (axis == 0) { ua = 2; va = 1; }
                    else if (axis == 1) { ua = 0; va = 2; }
                    else { ua = 0; va = 1; }
                    var su = samples[ua];
                    var sv = samples[va];
                    tile = faces != null ? faces[axis * 2 + (sign > 0 ? 1 : 0)] : own;
                    var normal = Vector3.zero;
                    normal[axis] = sign;
                    Unshare();
                    for (var i = 0; i + 1 < su.Count; i++)
                        for (var j = 0; j + 1 < sv.Count; j++)
                        {
                            var ids = new int[4];
                            var cu = new[] { su[i], su[i + 1], su[i + 1], su[i] };
                            var cv = new[] { sv[j], sv[j], sv[j + 1], sv[j + 1] };
                            for (var k = 0; k < 4; k++)
                            {
                                var p = Vector3.zero;
                                p[axis] = sign * hs[axis];
                                p[ua] = cu[k];
                                p[va] = cv[k];
                                var cube = new Vector3(p.x / Mathf.Max(1e-6f, h.x), p.y / Mathf.Max(1e-6f, h.y), p.z / Mathf.Max(1e-6f, h.z));
                                var q = new Vector3(Mathf.Clamp(p.x, -h.x + r, h.x - r), Mathf.Clamp(p.y, -h.y + r, h.y - r), Mathf.Clamp(p.z, -h.z + r, h.z - r));
                                var d = p - q;
                                var rounded = r > 0f && d.sqrMagnitude > 1e-12f ? q + d.normalized * r : p;
                                if (deform != null) rounded = deform(rounded, cube);
                                var uv = CellUv(tile, cu[k] + hs[ua], cv[k] + hs[va], su[i] + hs[ua], sv[j] + hs[va], 2f * hs[ua], 2f * hs[va]);
                                ids[k] = VShared(centre + rot * rounded, uv);
                            }
                            QFacing(Atlas, ids[0], ids[1], ids[2], ids[3], rot * normal);
                        }
                }
            End();
        }

        /// <summary>角を丸めない箱（面ごとに一枚。絵の繰り返しの境では割る）</summary>
        public void Box(string name, Vector3 centre, Quaternion rot, Vector3 size, Tile tile, Sides sides = Sides.All)
        {
            RoundBox(name, centre, rot, size, 0f, tile, null, 0, 100f, sides, 20f);
        }

        public void Box(string name, Vector3 centre, Vector3 size, Tile tile, Sides sides = Sides.All)
        {
            Box(name, centre, Quaternion.identity, size, tile, sides);
        }

        /// <summary>面ごとに貼り方を変える箱。faces は 左(−x)・右(+x)・下・上・後ろ(−z)・前(+z) の順。繰り返す貼り方は使わない（升目を割らない）</summary>
        public void Box6(string name, Vector3 centre, Quaternion rot, Vector3 size, Tile[] faces, Sides sides = Sides.All)
        {
            RoundBox(name, centre, rot, size, 0f, faces[0], null, 0, 100f, sides, 20f, faces);
        }

        /// <summary>軸の上の升目の切れ目。丸めの帯・step ごと・絵の繰り返しの境</summary>
        static List<float> Samples(float h, float r, int bend, float step, Tile tile)
        {
            var s = new List<float> { -h, h };
            if (r > 0f)
            {
                var n = Mathf.Max(1, bend);
                for (var k = 0; k < n; k++)
                {
                    // 丸めの帯の 0〜45 度を n に割る（箱の面の上では tan で並ぶ）
                    var a = 45f * k / n * Mathf.Deg2Rad;
                    var off = r * (1f - Mathf.Tan(a));
                    s.Add(-h + off);
                    s.Add(h - off);
                }
            }
            var inner = h - r;
            if (step > 0f && inner > 0f)
            {
                var cells = Mathf.Max(1, Mathf.CeilToInt(2f * inner / step));
                for (var k = 1; k < cells; k++) s.Add(-inner + 2f * inner * k / cells);
            }
            if (tile.Metre > 0f)
                for (var x = -h + tile.Metre; x < h - 1e-4f; x += tile.Metre) s.Add(x);
            s.Sort();
            var o = new List<float>();
            foreach (var v in s) if (o.Count == 0 || v - o[o.Count - 1] > 1e-5f) o.Add(v);
            return o;
        }

        /// <summary>升目のひとつの隅の uv。a・b は面の端からの長さ、a0・b0 は升の端（どの繰り返しの升に入るかを決める）</summary>
        static Vector2 CellUv(Tile tile, float a, float b, float a0, float b0, float wide, float high)
        {
            if (tile.Flat) return tile.Uv.position;
            if (tile.Metre > 0f)
            {
                var ku = Mathf.Floor(a0 / tile.Metre + 1e-4f);
                var kv = Mathf.Floor(b0 / tile.Metre + 1e-4f);
                return tile.At(Mathf.Clamp01(a / tile.Metre - ku), Mathf.Clamp01(b / tile.Metre - kv));
            }
            return tile.At(wide > 0f ? a / wide : 0f, high > 0f ? b / high : 0f);
        }

        /// <summary>
        /// 回した形。profile は (半径, 高さ) を下から上へ。横は sides に割り、uv は横が周を、縦が輪郭に沿った長さを升いっぱいに。
        /// inward なら内の面（器の内側）。caps は下と上の蓋
        /// </summary>
        public void Lathe(string name, Vector3 centre, Quaternion rot, IList<Vector2> profile, int sides, Tile tile,
            bool capBottom = false, bool capTop = false, bool inward = false, float creaseDegrees = 50f, float from = 0f, float sweep = 360f)
        {
            Begin(name, creaseDegrees);
            var n = profile.Count;
            var total = 0f;
            var lengths = new float[n];
            for (var i = 1; i < n; i++)
            {
                total += Vector2.Distance(profile[i - 1], profile[i]);
                lengths[i] = total;
            }
            var closed = Mathf.Abs(sweep - 360f) < 0.01f;
            var cols = sides + 1;
            var idx = new int[n, cols];
            for (var i = 0; i < n; i++)
                for (var s = 0; s < cols; s++)
                {
                    var a = (from + sweep * s / sides) * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Sin(a) * profile[i].x, profile[i].y, Mathf.Cos(a) * profile[i].x);
                    // 回した形は繰り返さず、周と輪郭を升いっぱいに張る（瓶の札のように一周で一枚）
                    var uv = tile.Flat ? tile.Uv.position : tile.At((float)s / sides, total > 0f ? lengths[i] / total : 0f);
                    idx[i, s] = V(centre + rot * p, uv);
                }
            for (var i = 0; i + 1 < n; i++)
                for (var s = 0; s < sides; s++)
                {
                    var a0 = (from + sweep * (s + 0.5f) / sides) * Mathf.Deg2Rad;
                    var radial = rot * new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                    // 輪郭の向きから外の向きを出す（皿のように寝た面でも表が外を向く）
                    var along = profile[i + 1] - profile[i];
                    var outward2 = new Vector2(along.y, -along.x);
                    if (outward2.sqrMagnitude < 1e-12f) outward2 = new Vector2(1f, 0f);
                    var want = radial * outward2.x + rot * Vector3.up * outward2.y;
                    if (inward) want = -want;
                    QFacing(Atlas, idx[i, s], idx[i + 1, s], idx[i + 1, s + 1], idx[i, s + 1], want);
                }
            if (capBottom) Cap(centre, rot, profile[0], tile, false, sides, from, sweep, closed);
            if (capTop) Cap(centre, rot, profile[n - 1], tile, true, sides, from, sweep, closed);
            End();
        }

        void Cap(Vector3 centre, Quaternion rot, Vector2 ring, Tile tile, bool top, int sides, float from, float sweep, bool closed)
        {
            var uv = tile.Flat ? tile.Uv.position : tile.At(0.5f, top ? 1f : 0f);
            var c = V(centre + rot * new Vector3(0f, ring.y, 0f), uv);
            var ids = new int[sides + 1];
            for (var s = 0; s <= sides; s++)
            {
                var a = (from + sweep * s / sides) * Mathf.Deg2Rad;
                ids[s] = V(centre + rot * new Vector3(Mathf.Sin(a) * ring.x, ring.y, Mathf.Cos(a) * ring.x), uv);
            }
            var want = rot * (top ? Vector3.up : Vector3.down);
            for (var s = 0; s < sides; s++) TFacing(Atlas, c, ids[s], ids[s + 1], want);
        }

        /// <summary>筒。path に沿って輪を張る（輪の向きは前の輪から滑らかに運ぶ）。uv はどの点も同じ（色の升）</summary>
        public void Tube(string name, IList<Vector3> path, float radius, int sides, Tile tile, bool caps = false, float creaseDegrees = 70f, int sub = Atlas)
        {
            var radii = new List<float>();
            for (var i = 0; i < path.Count; i++) radii.Add(radius);
            TubeR(name, path, radii, sides, tile, caps, creaseDegrees, sub);
        }

        public void TubeR(string name, IList<Vector3> path, IList<float> radii, int sides, Tile tile, bool caps = false, float creaseDegrees = 70f, int sub = Atlas)
        {
            Begin(name, creaseDegrees);
            var n = path.Count;
            var uv = tile.Flat ? tile.Uv.position : tile.At(0.5f, 0.5f);
            var tangents = new Vector3[n];
            for (var i = 0; i < n; i++) tangents[i] = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
            var normal = Vector3.Cross(tangents[0], Mathf.Abs(tangents[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var idx = new int[n, sides];
            for (var i = 0; i < n; i++)
            {
                if (i > 0) normal = (normal - Vector3.Dot(normal, tangents[i]) * tangents[i]).normalized;
                var bin = Vector3.Cross(tangents[i], normal);
                for (var s = 0; s < sides; s++)
                {
                    var a = (float)s / sides * Mathf.PI * 2f;
                    idx[i, s] = V(path[i] + (normal * Mathf.Cos(a) + bin * Mathf.Sin(a)) * radii[i], uv);
                }
            }
            for (var i = 0; i < n - 1; i++)
                for (var s = 0; s < sides; s++)
                {
                    var s1 = (s + 1) % sides;
                    var mid = 0.5f * (path[i] + path[i + 1]);
                    var quad = 0.25f * (Local(idx[i, s]) + Local(idx[i + 1, s]) + Local(idx[i + 1, s1]) + Local(idx[i, s1]));
                    QFacing(sub, idx[i, s], idx[i + 1, s], idx[i + 1, s1], idx[i, s1], quad - mid);
                }
            if (caps)
                foreach (var end in new[] { 0, n - 1 })
                {
                    var c = V(path[end], uv);
                    var ids = new int[sides];
                    for (var s = 0; s < sides; s++) ids[s] = V(Local(idx[end, s]), uv);
                    var outward = end == 0 ? -tangents[0] : tangents[n - 1];
                    for (var s = 0; s < sides; s++) TFacing(sub, c, ids[s], ids[(s + 1) % sides], outward);
                }
            End();
        }

        /// <summary>置いた頂点の位置を、今の置き場の中の座標へ戻す</summary>
        Vector3 Local(int i)
        {
            return frame.inverse.MultiplyPoint3x4(verts[i]);
        }

        /// <summary>絵を貼る板。halfRight は u の増える向きの半分、halfUp は v の増える向きの半分。twoSided なら裏にも同じ絵</summary>
        public void Decal(string name, Vector3 centre, Vector3 halfRight, Vector3 halfUp, Rect uv, bool twoSided = false, int sub = Atlas)
        {
            Begin(name, 0f);
            var normal = Vector3.Cross(halfUp, halfRight);
            Face(centre, halfRight, halfUp, uv, normal, sub);
            if (twoSided) Face(centre, halfRight, halfUp, uv, -normal, sub);
            End();
        }

        void Face(Vector3 centre, Vector3 halfRight, Vector3 halfUp, Rect uv, Vector3 normal, int sub)
        {
            var a = V(centre - halfRight - halfUp, new Vector2(uv.xMin, uv.yMin));
            var b = V(centre + halfRight - halfUp, new Vector2(uv.xMax, uv.yMin));
            var c = V(centre + halfRight + halfUp, new Vector2(uv.xMax, uv.yMax));
            var d = V(centre - halfRight + halfUp, new Vector2(uv.xMin, uv.yMax));
            QFacing(sub, a, b, c, d, normal);
        }

        /// <summary>
        /// 升目に張った一枚。at(u, v) は 0〜1 の割合から位置を返す。表は du × dv の向き（twoSided なら裏も張る）。
        /// uv は升いっぱいに（u が横、v が縦）
        /// </summary>
        public void Sheet(string name, Func<float, float, Vector3> at, int nu, int nv, Tile tile, bool twoSided, float creaseDegrees = 60f)
        {
            Begin(name, creaseDegrees);
            foreach (var back in twoSided ? new[] { false, true } : new[] { false })
            {
                var idx = new int[nu + 1, nv + 1];
                for (var i = 0; i <= nu; i++)
                    for (var j = 0; j <= nv; j++)
                    {
                        var u = (float)i / nu;
                        var v = (float)j / nv;
                        idx[i, j] = V(at(u, v), tile.Flat ? tile.Uv.position : tile.At(u, v));
                    }
                for (var i = 0; i < nu; i++)
                    for (var j = 0; j < nv; j++)
                    {
                        if (back) Q(Atlas, idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j]);
                        else Q(Atlas, idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1]);
                    }
            }
            End();
        }
    }
}
