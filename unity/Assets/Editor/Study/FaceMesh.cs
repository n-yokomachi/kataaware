using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 頭の形を測る道具と、段 2・段 3 の形（頭へ沿わせた顔の面、髪、目の面）。
    /// 座標はすべて顔の面の入れ物（FacePlate、頭の骨の子で倍率 0.01）の中で持つ。
    /// 入れ物の中の 1 は 1 m で、向きは模型と同じ（+z が顔の正面、+x が本人の右）
    /// </summary>
    public static class FaceMesh
    {
        /// <summary>Suit_Head の面の番号。0 = 肌、1 = 髪、2 = 眉、3 = 目</summary>
        public const int SkinSub = 0, HairSub = 1, BrowSub = 2, EyeSub = 3;

        /// <summary>頭のメッシュを今の姿勢で焼き、面の入れ物の中の座標にして返す</summary>
        public static Vector3[] BakeHead(GameObject her, Transform plate, out Mesh baked)
        {
            var head = her.transform.Find("Suit_Head");
            var smr = head.GetComponent<SkinnedMeshRenderer>();
            baked = new Mesh();
            baked.hideFlags = HideFlags.HideAndDontSave;
            smr.BakeMesh(baked, true);
            // BakeMesh(…, true) の頂点は、レンダラーの localToWorldMatrix でワールドへ移る（倍率 100 を二重に掛けない）
            var toWorld = smr.transform.localToWorldMatrix;
            var toPlate = plate.worldToLocalMatrix * toWorld;
            var v = baked.vertices;
            var o = new Vector3[v.Length];
            for (var i = 0; i < v.Length; i++) o[i] = toPlate.MultiplyPoint3x4(v[i]);
            return o;
        }

        /// <summary>
        /// 正面から見た深さの地図。x, y の格子の各点で、指定した面のうち一番手前（z が大きい）の z。
        /// 当たらない点は NaN
        /// </summary>
        public sealed class Depth
        {
            public float x0, y0, step;
            public int nx, ny;
            public float[,] z;

            public float At(float x, float y)
            {
                var fx = (x - x0) / step;
                var fy = (y - y0) / step;
                var ix = Mathf.Clamp(Mathf.FloorToInt(fx), 0, nx - 2);
                var iy = Mathf.Clamp(Mathf.FloorToInt(fy), 0, ny - 2);
                var tx = Mathf.Clamp01(fx - ix);
                var ty = Mathf.Clamp01(fy - iy);
                float a = z[ix, iy], b = z[ix + 1, iy], c = z[ix, iy + 1], d = z[ix + 1, iy + 1];
                // 穴があれば、ある値だけで埋める
                var sum = 0f; var w = 0f;
                if (!float.IsNaN(a)) { sum += a * (1 - tx) * (1 - ty); w += (1 - tx) * (1 - ty); }
                if (!float.IsNaN(b)) { sum += b * tx * (1 - ty); w += tx * (1 - ty); }
                if (!float.IsNaN(c)) { sum += c * (1 - tx) * ty; w += (1 - tx) * ty; }
                if (!float.IsNaN(d)) { sum += d * tx * ty; w += tx * ty; }
                return w > 1e-5f ? sum / w : float.NaN;
            }
        }

        public static Depth DepthMap(Vector3[] verts, Mesh baked, int[] subs, float x0, float x1, float y0, float y1, float step)
        {
            var d = new Depth { x0 = x0, y0 = y0, step = step };
            d.nx = Mathf.CeilToInt((x1 - x0) / step) + 1;
            d.ny = Mathf.CeilToInt((y1 - y0) / step) + 1;
            d.z = new float[d.nx, d.ny];
            for (var i = 0; i < d.nx; i++)
                for (var j = 0; j < d.ny; j++)
                    d.z[i, j] = float.NaN;
            foreach (var s in subs)
            {
                var tris = baked.GetTriangles(s);
                for (var t = 0; t < tris.Length; t += 3)
                {
                    Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                    var minx = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
                    var maxx = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                    var miny = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                    var maxy = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    var i0 = Mathf.Max(0, Mathf.CeilToInt((minx - x0) / step));
                    var i1 = Mathf.Min(d.nx - 1, Mathf.FloorToInt((maxx - x0) / step));
                    var j0 = Mathf.Max(0, Mathf.CeilToInt((miny - y0) / step));
                    var j1 = Mathf.Min(d.ny - 1, Mathf.FloorToInt((maxy - y0) / step));
                    var den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(den) < 1e-12f) continue;
                    for (var i = i0; i <= i1; i++)
                        for (var j = j0; j <= j1; j++)
                        {
                            float px = x0 + i * step, py = y0 + j * step;
                            var l1 = ((b.y - c.y) * (px - c.x) + (c.x - b.x) * (py - c.y)) / den;
                            var l2 = ((c.y - a.y) * (px - c.x) + (a.x - c.x) * (py - c.y)) / den;
                            var l3 = 1f - l1 - l2;
                            if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                            var z = l1 * a.z + l2 * b.z + l3 * c.z;
                            if (float.IsNaN(d.z[i, j]) || z > d.z[i, j]) d.z[i, j] = z;
                        }
                }
            }
            return d;
        }

        /// <summary>面の番号 s の頂点の範囲と重心</summary>
        public static Bounds SubBounds(Vector3[] verts, Mesh baked, int s, out Vector3 mean)
        {
            var tris = baked.GetTriangles(s);
            var b = new Bounds(verts[tris[0]], Vector3.zero);
            mean = Vector3.zero;
            foreach (var i in tris) { b.Encapsulate(verts[i]); mean += verts[i]; }
            mean /= tris.Length;
            return b;
        }

        /// <summary>頭の形を文字で返す。深さの地図（5 mm 格子、mm）と、元の目・眉の位置</summary>
        public static string Survey(GameObject her, Transform plate)
        {
            Mesh baked;
            var v = BakeHead(her, plate, out baked);
            try
            {
                var sb = new StringBuilder();
                foreach (var s in new[] { SkinSub, HairSub, BrowSub, EyeSub })
                {
                    Vector3 mean;
                    var b = SubBounds(v, baked, s, out mean);
                    sb.AppendFormat(CultureInfo.InvariantCulture, "sub {0}: min {1} max {2} mean {3}\n", s, F(b.min), F(b.max), F(mean));
                }
                // 目と眉は左右二つずつ。x の正負で分けて重心を出す
                foreach (var s in new[] { BrowSub, EyeSub })
                {
                    var tris = baked.GetTriangles(s);
                    Vector3 l = Vector3.zero, r = Vector3.zero; int nl = 0, nr = 0;
                    float lx0 = 9, lx1 = -9, ly0 = 9, ly1 = -9;
                    foreach (var i in tris)
                    {
                        if (v[i].x < 0) { l += v[i]; nl++; lx0 = Mathf.Min(lx0, v[i].x); lx1 = Mathf.Max(lx1, v[i].x); ly0 = Mathf.Min(ly0, v[i].y); ly1 = Mathf.Max(ly1, v[i].y); }
                        else { r += v[i]; nr++; }
                    }
                    sb.AppendFormat(CultureInfo.InvariantCulture, "sub {0} 左(−x) {1} 右(+x) {2} 左の幅 x {3:0.000}..{4:0.000} y {5:0.000}..{6:0.000}\n",
                        s, F(l / Mathf.Max(1, nl)), F(r / Mathf.Max(1, nr)), lx0, lx1, ly0, ly1);
                }
                var d = DepthMap(v, baked, new[] { SkinSub, BrowSub, EyeSub }, -0.10f, 0.10f, -0.13f, 0.09f, 0.005f);
                sb.Append("深さ（mm、行は y の上から、列は x = -100..100 mm を 10 mm ごと）\n");
                for (var j = d.ny - 1; j >= 0; j -= 2)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "y{0,5:0}:", (d.y0 + j * d.step) * 1000f);
                    for (var i = 0; i < d.nx; i += 2)
                    {
                        var z = d.z[i, j];
                        sb.Append(float.IsNaN(z) ? "    ." : string.Format(CultureInfo.InvariantCulture, "{0,5:0}", z * 1000f));
                    }
                    sb.Append("\n");
                }
                return sb.ToString();
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        static string F(Vector3 p)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", p.x, p.y, p.z);
        }

        // ---- 段 2: 頭へ沿わせた顔の面 -----------------------------------------

        /// <summary>段 2 の面の輪郭（楕円）。中心と半径（m）</summary>
        public static readonly Vector2 OvalCentre = new Vector2(0f, -0.015f);
        public const float OvalA = 0.086f, OvalB = 0.100f;

        /// <summary>段 2 の絵が覆う矩形。UV はこの矩形へ真っ直ぐ当てる</summary>
        public static readonly Rect Area2 = new Rect(-0.088f, -0.117f, 0.176f, 0.204f);

        /// <summary>格子の数。4.4 mm ごと</summary>
        public const int GridX = 41, GridY = 47;

        /// <summary>面を頭の肌から浮かせる量（m）</summary>
        public const float Lift = 0.0015f;

        /// <summary>楕円の中なら 1 未満</summary>
        public static float Oval(float x, float y)
        {
            var dx = (x - OvalCentre.x) / OvalA;
            var dy = (y - OvalCentre.y) / OvalB;
            return dx * dx + dy * dy;
        }

        static float Gauss(float dx, float dy)
        {
            return Mathf.Exp(-(dx * dx + dy * dy));
        }

        /// <summary>
        /// 頂点で付ける顔の起伏（m、手前が正）。鼻・眉の張り・眼窩・目の丸み・頬・唇・顎。
        /// 模型の鼻は付け根から y −55 mm まで続く長い稜で、これを外して鼻先を y −40 mm に置き直す
        /// </summary>
        public static float Relief(float x, float y, FacePaint.Layout l)
        {
            var ax = Mathf.Abs(x);
            var z = 0f;
            // 鼻。付け根（y +6 mm）から鼻先（y −40 mm）へ高く広く、鼻の下（y −47 mm）で肌へ戻る
            const float yb = 0.006f, yt = -0.040f, yn = -0.047f;
            if (y <= yb + 0.004f && y >= yn - 0.002f)
            {
                float h, hw;
                if (y >= yt)
                {
                    var along = Mathf.Clamp01((yb - y) / (yb - yt));
                    h = 0.0030f + 0.0090f * Mathf.Pow(along, 1.4f);
                    hw = 0.0045f + 0.0040f * along;
                    if (y > yb) h *= Mathf.Clamp01(1f - (y - yb) / 0.004f);
                }
                else
                {
                    var below = Mathf.Clamp01((yt - y) / (yt - yn));
                    h = 0.0120f * (1f - Mathf.Pow(below, 0.8f));
                    hw = 0.0085f;
                }
                var cross = Mathf.Max(0f, 1f - (x / hw) * (x / hw));
                z += h * Mathf.Pow(cross, 1.2f);
            }
            // 小鼻
            z += 0.0040f * Gauss((ax - 0.0085f) / 0.0045f, (y + 0.043f) / 0.0040f);
            // 眉の張り
            z += 0.0020f * Mathf.Exp(-Mathf.Pow((y - 0.016f) / 0.006f, 2f)) * Mathf.Clamp01((0.064f - ax) / 0.012f);
            // 眼窩と、目の丸み
            z -= 0.0035f * Gauss((ax - l.eyeX) / 0.019f, (y - l.eyeY) / 0.012f);
            z += 0.0022f * Gauss((ax - l.eyeX) / 0.012f, (y - l.eyeY) / 0.0065f);
            // 頬
            z += 0.0030f * Gauss((ax - 0.045f) / 0.020f, (y + 0.035f) / 0.018f);
            // 唇と、その上下
            z += 0.0022f * Gauss(x / 0.016f, (y - (l.mouthY + 0.0035f)) / 0.0035f);
            z += 0.0028f * Gauss(x / 0.013f, (y - (l.mouthY - 0.0045f)) / 0.0035f);
            z -= 0.0010f * Gauss(x / 0.014f, (y - l.mouthY) / 0.0012f);
            z -= 0.0012f * Gauss(x / 0.012f, (y - (l.mouthY - 0.012f)) / 0.004f);
            // 顎
            z += 0.0025f * Gauss(x / 0.014f, (y + 0.100f) / 0.010f);
            return z;
        }

        /// <summary>
        /// 段 2 の顔の面を作る。頭の肌の深さを滑らかにした地（模型の鼻は外す）に、<see cref="Relief"/> を載せる。
        /// 縁は頭の肌の角ばった面の手前 1.5 mm に沿わせ、縁から内へ起伏を効かせる。
        /// 楕円の外と、頭の正面から外れた所（深さ 0.165 m 未満）には三角を張らない
        /// </summary>
        public static Mesh BuildPlate2(GameObject her, Transform plate, FacePaint.Layout l)
        {
            Mesh baked;
            var v = BakeHead(her, plate, out baked);
            try
            {
                const float step = 0.0025f;
                var raw = DepthMap(v, baked, new[] { SkinSub }, Area2.xMin - 0.01f, Area2.xMax + 0.01f, Area2.yMin - 0.01f, Area2.yMax + 0.01f, step);
                var hair = DepthMap(v, baked, new[] { HairSub }, Area2.xMin - 0.01f, Area2.xMax + 0.01f, Area2.yMin - 0.01f, Area2.yMax + 0.01f, step);
                // 模型の鼻を外す。|x| < 18 mm の稜を、両脇の値を結ぶ緩い弧に置き換える
                var flat = new float[raw.nx, raw.ny];
                for (var j = 0; j < raw.ny; j++)
                    for (var i = 0; i < raw.nx; i++)
                    {
                        var x = raw.x0 + i * step; var y = raw.y0 + j * step;
                        flat[i, j] = raw.z[i, j];
                        if (Mathf.Abs(x) < 0.018f && y > -0.075f && y < 0.018f)
                        {
                            var zl = raw.At(-0.018f, y); var zr = raw.At(0.018f, y);
                            if (float.IsNaN(zl) || float.IsNaN(zr)) continue;
                            var t = (x + 0.018f) / 0.036f;
                            flat[i, j] = Mathf.Lerp(zl, zr, t) + 0.0020f * (1f - (x / 0.018f) * (x / 0.018f));
                        }
                    }
                // 6 mm ほどでぼかす
                var smooth = new Depth { x0 = raw.x0, y0 = raw.y0, step = step, nx = raw.nx, ny = raw.ny, z = new float[raw.nx, raw.ny] };
                const int k = 5;
                for (var j = 0; j < raw.ny; j++)
                    for (var i = 0; i < raw.nx; i++)
                    {
                        float s = 0, w = 0;
                        for (var dj = -k; dj <= k; dj++)
                            for (var di = -k; di <= k; di++)
                            {
                                int ii = i + di, jj = j + dj;
                                if (ii < 0 || jj < 0 || ii >= raw.nx || jj >= raw.ny) continue;
                                var zz = flat[ii, jj];
                                if (float.IsNaN(zz)) continue;
                                var g = Mathf.Exp(-(di * di + dj * dj) / 6.0f);
                                s += zz * g; w += g;
                            }
                        smooth.z[i, j] = w > 0 ? s / w : float.NaN;
                    }

                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var index = new int[GridX, GridY];
                for (var j = 0; j < GridY; j++)
                    for (var i = 0; i < GridX; i++)
                    {
                        var x = Area2.xMin + Area2.width * i / (GridX - 1);
                        var y = Area2.yMin + Area2.height * j / (GridY - 1);
                        index[i, j] = -1;
                        var o = Oval(x, y);
                        var zr = raw.At(x, y);
                        if (o > 1.02f || float.IsNaN(zr) || zr < 0.165f) continue;
                        var zs = smooth.At(x, y);
                        if (float.IsNaN(zs)) zs = zr;
                        // 縁の帯（楕円の外側 25 %）は、頭の角ばった面の手前へ寄せる
                        var rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.98f, Mathf.Sqrt(o)));
                        var inner = zs + Lift + Relief(x, y, l);
                        var edge = Mathf.Max(zr, zs) + Lift;
                        var z = Mathf.Lerp(inner, edge, rim);
                        // 髪が肌を覆っている所（額の脇に流した前髪）では、面を髪の奥へ下げる。
                        // そのままだと肌から 1.5 mm 浮かせた面が、肌に貼り付いた前髪を突き抜ける
                        var zh = hair.At(x, y);
                        if (!float.IsNaN(zh) && zh > zr - 0.002f) z = Mathf.Min(z, zh - 0.0008f);
                        // どこでも頭の肌より手前に
                        z = Mathf.Max(z, zr + 0.0003f);
                        index[i, j] = verts.Count;
                        verts.Add(new Vector3(x, y, z));
                        uvs.Add(new Vector2((x - Area2.xMin) / Area2.width, (y - Area2.yMin) / Area2.height));
                    }
                var tris = new List<int>();
                for (var j = 0; j < GridY - 1; j++)
                    for (var i = 0; i < GridX - 1; i++)
                    {
                        int a = index[i, j], b = index[i + 1, j], c = index[i, j + 1], d = index[i + 1, j + 1];
                        if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                        // 表は +z。法線は cross(b − a, c − a) で +z を向く
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(b); tris.Add(d); tris.Add(c);
                    }
                var m = new Mesh { name = "FacePlate2" };
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        /// <summary>
        /// 頭のメッシュの写しから、段 2 の面に覆われる肌の三角（頂点が三つとも楕円の 97 % の内で、z が 0.18 m より手前）と、
        /// 元の眉と目（2 番と 3 番）を外す。面の下で頭の鼻や眉が突き抜けないように
        /// </summary>
        public static Mesh CutHead(GameObject her, Transform plate)
        {
            var smr = her.transform.Find("Suit_Head").GetComponent<SkinnedMeshRenderer>();
            Mesh baked;
            var v = BakeHead(her, plate, out baked);
            try
            {
                var copy = Object.Instantiate(smr.sharedMesh);
                copy.name = smr.sharedMesh.name + " (face cut)";
                copy.hideFlags = HideFlags.HideAndDontSave;
                var skin = copy.GetTriangles(SkinSub);
                var keep = new List<int>();
                for (var t = 0; t < skin.Length; t += 3)
                {
                    var covered = true;
                    for (var k = 0; k < 3; k++)
                    {
                        var p = v[skin[t + k]];
                        if (Oval(p.x, p.y) > 0.97f * 0.97f || p.z < 0.18f) { covered = false; break; }
                    }
                    if (!covered) { keep.Add(skin[t]); keep.Add(skin[t + 1]); keep.Add(skin[t + 2]); }
                }
                copy.SetTriangles(keep, SkinSub);
                copy.SetTriangles(new int[0], BrowSub);
                copy.SetTriangles(new int[0], EyeSub);
                return copy;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        /// <summary>段 2・3 の頭の組み替え。頭の顔の三角を外した写しへ差し替え、髪を整える</summary>
        public static void Restyle(FaceSubject who, GameObject her, Transform plate)
        {
            var smr = her.transform.Find("Suit_Head").GetComponent<SkinnedMeshRenderer>();
            var cut = CutHead(her, plate);
            who.Made.Add(cut);
            smr.sharedMesh = cut;
        }

        // ---- 段 3: 目の面 -----------------------------------------------------

        /// <summary>目の玉の半径と、見える帽子の広がり（m）</summary>
        public const float BallR = 0.020f, BallHalfX = 0.0165f, BallHalfY = 0.0085f;

        /// <summary>目の玉の前の面が、面の目の中心より奥へ下がる量（m）</summary>
        public const float BallSink = 0.0025f;

        /// <summary>
        /// 目の玉の帽子。球の一部を 13×9 の格子で。UV は右目の絵（<see cref="FacePaint.Ball"/>）の矩形へ当て、
        /// 左目は u を裏返す（虹彩のずれも鏡に映る）
        /// </summary>
        public static Mesh Ball(float s, float front, FacePaint.Layout l, float half, Depth plateDepth)
        {
            const int nx = 13, ny = 9;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var cx = s * l.eyeX; var cy = l.eyeY;
            var cz = front - BallR;
            for (var j = 0; j < ny; j++)
                for (var i = 0; i < nx; i++)
                {
                    var dx = Mathf.Lerp(-BallHalfX, BallHalfX, i / (float)(nx - 1));
                    var dy = Mathf.Lerp(-BallHalfY, BallHalfY, j / (float)(ny - 1));
                    var z = cz + Mathf.Sqrt(Mathf.Max(0f, BallR * BallR - dx * dx - dy * dy));
                    // どこでも面の 0.8 mm 奥に。瞼の下で目の玉が面を突き抜けないように
                    var zp = plateDepth.At(cx + dx, cy + dy);
                    if (!float.IsNaN(zp)) z = Mathf.Min(z, zp - 0.0008f);
                    verts.Add(new Vector3(cx + dx, cy + dy, z));
                    var u = 0.5f + s * dx / (2f * half);
                    uvs.Add(new Vector2(u, 0.5f + dy / (2f * half)));
                }
            var tris = new List<int>();
            for (var j = 0; j < ny - 1; j++)
                for (var i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            var m = new Mesh { name = s > 0 ? "EyeBallR" : "EyeBallL" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>面のメッシュを正面から見た深さの地図（目の周り）</summary>
        public static Depth PlateDepth(Mesh plateMesh, float x, float y, float half)
        {
            return DepthMap(plateMesh.vertices, plateMesh, new[] { 0 }, x - half, x + half, y - half, y + half, 0.0005f);
        }
    }
}
